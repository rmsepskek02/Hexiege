// ============================================================================
// MapChunkAssembler.cs
// 맵 데이터(canonical 바이트 배열)를 "조각(chunk)"으로 나누고, 받은 조각을 다시
// 하나로 합치는 순수 계산 클래스.
//
// ── 이 파일이 왜 NetworkBehaviour 가 아닌가 (아주 중요) ─────────────────────
//   이 클래스는 Infrastructure 폴더에 있지만 **평범한 C# 클래스**다.
//   UnityEngine 도, Unity.Netcode 도 참조하지 않는다. 일부러 그렇게 만들었다.
//
//   이유 1) 실제로 실행해서 검증할 수 있게 하려고.
//           Unity 타입을 하나라도 쓰면 이 저장소 환경(mcs/mono)에서 컴파일조차 되지 않아
//           "코드를 썼지만 한 번도 돌려 본 적이 없는" 상태로 남는다.
//           반대로 순수 C# 이면 조각 2개 이상·중복·역순 같은 입력을 만들어
//           실제로 돌려 보고 기대값과 대조할 수 있다.
//   이유 2) 실전에서는 조각이 거의 항상 1개이기 때문.
//           맵 canonical 바이트는 약 323~343바이트인데 한 번에 보낼 수 있는 크기가
//           그보다 훨씬 크다. 그래서 "조각 여러 개" 경로는 실제 경기에서 거의 안 돈다.
//           그런 코드일수록 **최소 한 번은 실제로 실행돼 본 적이 있어야** 한다.
//
//   ⚠️ 그러므로 이 파일에 `using UnityEngine;` 이나 `using Unity.Netcode;` 를
//      추가하지 마라. 추가하는 순간 위 두 가지가 동시에 무너진다.
//      로그도 이 파일에서 남기지 않는다(GameLog 는 Application 계층이고, 여기에 끌어들이면
//      역시 단독 컴파일이 깨진다). 로그는 이 클래스를 쓰는 NetworkMapTransfer 가 남긴다.
//
// ── 이 클래스가 지키는 가장 중요한 계약 ────────────────────────────────────
//   🔴 **완성되기 전에는 데이터를 밖으로 내보내지 않는다.**
//   조각이 하나라도 모자라면 <see cref="TryGetAssembled"/> 는 false 를 돌려주고
//   payload 는 null 이다. 내부 버퍼를 그대로 노출하는 프로퍼티도 두지 않는다.
//   "반쯤 채워진 맵"이 밖으로 새어 나가면 절반만 그려진 맵으로 경기가 시작될 수 있고,
//   그것이 규칙 16 이 "모든 조각이 모여 선언된 크기와 일치한 뒤에야 다음으로 넘어간다"고
//   못 박은 이유다. **API 모양 자체로** 그 실수를 불가능하게 만들어 둔다.
//
// ── 이 클래스가 하지 "않는" 일 (경계를 분명히 한다) ────────────────────────
//   - nonce(전송 회차 식별자) 관리         → NetworkMapTransfer 소유
//   - timeout / 재전송 / ACK 대기          → NetworkMapTransfer 소유
//   - 해시 대조                            → NetworkMapTransfer 가 Domain 코덱을 호출
//   - 조각 크기를 몇 바이트로 할지 결정     → NetworkMapTransfer 소유(실측 후 확정)
//   이 클래스는 "넘겨받은 숫자대로 자르고 합치는" 계산만 한다.
//
// Infrastructure 레이어 — 단, 이 파일만은 Unity 의존 0건인 순수 C# 이다.
// ============================================================================

using System;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 조각 하나를 받아들인 결과.
    /// 호출부(NetworkMapTransfer)가 "무시했다 / 받았다 / 이상해서 버렸다"를
    /// 구분해 로그로 남길 수 있게 이유까지 나눠 둔다.
    /// </summary>
    public enum MapChunkAcceptResult
    {
        /// <summary>정상적으로 버퍼에 채워 넣었다.</summary>
        Accepted,

        /// <summary>
        /// 같은 index 조각을 이미 받아 두었기에 무시했다(오류가 아니다).
        /// 신뢰성 전송이라도 재전송이 겹치면 같은 조각이 두 번 도착할 수 있다.
        /// </summary>
        DuplicateIgnored,

        /// <summary>index 가 0 미만이거나 조각 개수 이상이라 버렸다.</summary>
        RejectedIndexOutOfRange,

        /// <summary>data 가 null 이거나 길이 0 이라 버렸다.</summary>
        RejectedNullOrEmpty,

        /// <summary>
        /// 그 index 자리에 들어와야 할 길이와 실제 길이가 달라 버렸다.
        /// (마지막 조각만 짧고 나머지는 모두 chunkSize 여야 한다)
        /// </summary>
        RejectedLengthMismatch
    }

    /// <summary>
    /// 맵 canonical 바이트를 조각으로 나누고(정적 메서드), 받은 조각을 재조립한다(인스턴스).
    ///
    /// 사용 흐름(F 단계에서 NetworkMapTransfer 가 이렇게 쓴다):
    ///   [보내는 쪽] MapChunkAssembler.Split(payload, chunkSize) → 조각 배열 → 조각마다 RPC 1번
    ///   [받는 쪽]   new MapChunkAssembler(totalBytes, chunkSize)
    ///               → 조각 도착할 때마다 Accept(index, data)
    ///               → IsComplete 가 true 가 된 뒤에야 TryGetAssembled(out payload)
    /// </summary>
    public sealed class MapChunkAssembler
    {
        // ====================================================================
        // 불변 정보 (생성자에서 확정 — 도중에 바뀌지 않는다)
        // ====================================================================

        /// <summary>보내는 쪽이 "전부 합치면 이만큼"이라고 선언한 전체 바이트 수.</summary>
        private readonly int _totalBytes;

        /// <summary>조각 하나의 최대 크기. 마지막 조각만 이보다 작을 수 있다.</summary>
        private readonly int _chunkSize;

        /// <summary>조각 총 개수(= 올림 나눗셈 결과).</summary>
        private readonly int _chunkCount;

        /// <summary>
        /// 조각을 채워 넣을 버퍼. 크기는 _totalBytes 로 미리 잡아 둔다.
        /// 🔴 이 배열은 절대 밖으로 그대로 내보내지 않는다(부분 데이터 유출 금지).
        /// </summary>
        private readonly byte[] _buffer;

        /// <summary>index 번째 조각을 이미 받았는지 표시하는 체크 표. 중복 판정에 쓴다.</summary>
        private readonly bool[] _received;

        // ====================================================================
        // 진행 상태
        // ====================================================================

        /// <summary>지금까지 실제로 받아들인(중복·거부 제외) 조각 수.</summary>
        private int _receivedChunkCount;

        /// <summary>지금까지 받아들인 조각들의 바이트 수 합계.</summary>
        private int _receivedByteCount;

        // ====================================================================
        // 생성자
        // ====================================================================

        /// <summary>
        /// 재조립기를 만든다. 받는 쪽은 MapPrepareBegin 메시지로 넘어온
        /// totalBytes / chunkSize 를 그대로 넣어 준다.
        /// </summary>
        /// <param name="totalBytes">전체 바이트 수. 1 이상이어야 한다.</param>
        /// <param name="chunkSize">조각 하나의 최대 크기. 1 이상이어야 한다.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 둘 중 하나라도 1 미만이면 던진다.
        /// 0바이트 맵이나 0바이트 조각은 "정상 입력"이 아니라 상위 로직의 버그이므로,
        /// 조용히 빈 맵을 완성했다고 말하지 않고 그 자리에서 터뜨린다.
        /// </exception>
        public MapChunkAssembler(int totalBytes, int chunkSize)
        {
            if (totalBytes < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalBytes), totalBytes, "전체 바이트 수는 1 이상이어야 한다.");
            }

            if (chunkSize < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSize), chunkSize, "조각 크기는 1 이상이어야 한다.");
            }

            _totalBytes = totalBytes;
            _chunkSize = chunkSize;
            _chunkCount = CalculateChunkCount(totalBytes, chunkSize);
            _buffer = new byte[totalBytes];
            _received = new bool[_chunkCount];
            _receivedChunkCount = 0;
            _receivedByteCount = 0;
        }

        // ====================================================================
        // 읽기 전용 진행 상황 (로그·재전송 판단용 — 데이터 자체는 노출하지 않는다)
        // ====================================================================

        /// <summary>선언된 전체 바이트 수.</summary>
        public int TotalBytes { get { return _totalBytes; } }

        /// <summary>조각 하나의 최대 크기.</summary>
        public int ChunkSize { get { return _chunkSize; } }

        /// <summary>조각 총 개수.</summary>
        public int ChunkCount { get { return _chunkCount; } }

        /// <summary>지금까지 받아들인 조각 수.</summary>
        public int ReceivedChunkCount { get { return _receivedChunkCount; } }

        /// <summary>지금까지 받아들인 바이트 수 합계.</summary>
        public int ReceivedByteCount { get { return _receivedByteCount; } }

        /// <summary>아직 못 받은 조각 수.</summary>
        public int MissingChunkCount { get { return _chunkCount - _receivedChunkCount; } }

        /// <summary>
        /// 전부 모였는가.
        /// 🔴 조각 수와 **바이트 합계**를 둘 다 본다. 규칙 16 의
        /// "모든 조각이 모여 **선언된 크기와 일치**한 뒤에야"를 그대로 옮긴 것이다.
        /// (조각 수만 세면 길이가 틀어진 채 "다 왔다"고 말할 여지가 남는다.
        ///  Accept 가 길이를 이미 검사하므로 이론상 중복이지만, 이 조건이
        ///  마지막 관문이므로 방어를 한 겹 더 둔다.)
        /// </summary>
        public bool IsComplete
        {
            get { return _receivedChunkCount == _chunkCount && _receivedByteCount == _totalBytes; }
        }

        /// <summary>
        /// 아직 못 받은 조각 중 가장 앞선 index. 전부 받았으면 -1.
        /// F 단계의 "1회 재전송"에서 무엇을 다시 달라고 할지 정할 때 쓸 수 있다.
        /// </summary>
        public int FirstMissingIndex
        {
            get
            {
                for (int i = 0; i < _chunkCount; i++)
                {
                    if (!_received[i])
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        // ====================================================================
        // 조각 받기
        // ====================================================================

        /// <summary>
        /// 조각 하나를 받아들인다. 도착 순서는 상관없다(역순이어도 index 자리에 채워 넣는다).
        ///
        /// ⚠️ 중복 조각의 **내용은 비교하지 않는다.** 먼저 온 것을 그대로 두고 뒤엣것을 버린다.
        ///    내용이 어긋나 있다면 마지막에 해시 대조에서 반드시 걸리므로, 여기서
        ///    바이트 비교를 또 하는 것은 같은 검사를 두 번 하는 셈이다.
        /// </summary>
        /// <param name="index">조각 번호(0 부터 ChunkCount-1).</param>
        /// <param name="data">조각 내용. 내부에 **복사해서** 보관한다(아래 주석 참조).</param>
        /// <returns>받아들였는지, 무시했는지, 왜 버렸는지.</returns>
        public MapChunkAcceptResult Accept(int index, byte[] data)
        {
            if (index < 0 || index >= _chunkCount)
            {
                return MapChunkAcceptResult.RejectedIndexOutOfRange;
            }

            if (data == null || data.Length == 0)
            {
                return MapChunkAcceptResult.RejectedNullOrEmpty;
            }

            int expectedLength = GetChunkLength(_totalBytes, _chunkSize, index);
            if (data.Length != expectedLength)
            {
                return MapChunkAcceptResult.RejectedLengthMismatch;
            }

            // 중복 검사는 길이 검사 뒤에 둔다.
            // 순서를 바꾸면 "이미 받은 자리에 길이가 이상한 조각이 왔다"를
            // 그냥 중복으로 삼켜 버려 이상 징후가 로그에서 사라진다.
            if (_received[index])
            {
                return MapChunkAcceptResult.DuplicateIgnored;
            }

            // 🔴 반드시 복사한다. NGO 가 넘겨주는 배열은 다음 메시지에서 재사용될 수 있어
            //    참조만 들고 있으면 나중에 내용이 통째로 바뀌어 있을 수 있다.
            Array.Copy(data, 0, _buffer, index * _chunkSize, data.Length);

            _received[index] = true;
            _receivedChunkCount++;
            _receivedByteCount += data.Length;

            return MapChunkAcceptResult.Accepted;
        }

        // ====================================================================
        // 완성본 꺼내기
        // ====================================================================

        /// <summary>
        /// 완성됐을 때만 합쳐진 바이트 배열을 돌려준다.
        ///
        /// 🔴 완성 전에는 **false 를 돌려주고 payload 는 null 이다.**
        ///    이 클래스에는 "지금까지 받은 것만이라도 달라"는 API 가 아예 없다.
        ///    부분 데이터를 쓰는 실수를 문법 차원에서 막기 위한 설계다(규칙 16).
        /// </summary>
        /// <param name="payload">
        /// 완성본의 **복사본**. 호출부가 나중에 이 배열을 건드려도 내부 버퍼는 안전하다.
        /// </param>
        /// <returns>완성됐으면 true.</returns>
        public bool TryGetAssembled(out byte[] payload)
        {
            if (!IsComplete)
            {
                payload = null;
                return false;
            }

            byte[] copy = new byte[_totalBytes];
            Array.Copy(_buffer, 0, copy, 0, _totalBytes);
            payload = copy;
            return true;
        }

        // ====================================================================
        // 조각 나누기 (보내는 쪽이 쓰는 정적 메서드 — 상태가 없다)
        // ====================================================================

        /// <summary>
        /// 전체 바이트 수와 조각 크기로 조각 개수를 구한다(올림 나눗셈).
        /// 예: 343바이트를 100바이트씩 → 4개(100/100/100/43).
        /// </summary>
        public static int CalculateChunkCount(int totalBytes, int chunkSize)
        {
            if (totalBytes < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalBytes), totalBytes, "전체 바이트 수는 1 이상이어야 한다.");
            }

            if (chunkSize < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSize), chunkSize, "조각 크기는 1 이상이어야 한다.");
            }

            // (a + b - 1) / b 는 정수 올림 나눗셈의 관용구다.
            // 실수 나눗셈 + Math.Ceiling 을 쓰지 않는 이유는 부동소수 오차를 끌어들이지 않기 위해서다.
            return (totalBytes + chunkSize - 1) / chunkSize;
        }

        /// <summary>
        /// index 번째 조각이 가져야 할 길이. 마지막 조각만 chunkSize 보다 짧을 수 있다.
        /// </summary>
        public static int GetChunkLength(int totalBytes, int chunkSize, int index)
        {
            int count = CalculateChunkCount(totalBytes, chunkSize);
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, "조각 번호가 조각 개수 범위를 벗어났다.");
            }

            int offset = index * chunkSize;
            int remain = totalBytes - offset;
            return remain < chunkSize ? remain : chunkSize;
        }

        /// <summary>
        /// payload 를 chunkSize 단위로 잘라 조각 배열을 만든다.
        /// 반환 배열의 i 번째 원소가 곧 index=i 조각이다.
        /// </summary>
        /// <param name="payload">자를 원본. null 이거나 길이 0 이면 예외.</param>
        /// <param name="chunkSize">조각 하나의 최대 크기(1 이상).</param>
        public static byte[][] Split(byte[] payload, int chunkSize)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.Length < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(payload), payload.Length, "자를 데이터가 비어 있다.");
            }

            int count = CalculateChunkCount(payload.Length, chunkSize);
            byte[][] chunks = new byte[count][];

            for (int i = 0; i < count; i++)
            {
                int length = GetChunkLength(payload.Length, chunkSize, i);
                byte[] chunk = new byte[length];
                Array.Copy(payload, i * chunkSize, chunk, 0, length);
                chunks[i] = chunk;
            }

            return chunks;
        }
    }
}
