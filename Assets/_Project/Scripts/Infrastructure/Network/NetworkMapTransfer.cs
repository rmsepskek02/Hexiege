// ============================================================================
// NetworkMapTransfer.cs
// Host 가 확정한 맵을 Client 에게 "조각으로 나눠 보내고, 잘 받았다는 답을 받는"
// 왕복 전체를 소유하는 NetworkBehaviour.
//
// 🔴🔴 이 파일은 지금 「뼈대」다 — 맵을 아직 하나도 보내지 않는다. 🔴🔴
//   무작위 맵 3단계는 A~I 로 쪼개져 있고, 이 파일은 그중 **B 단계**의 결과물이다.
//   B 가 하는 일은 딱 셋이다.
//     ① 전송 객체(NetworkObject)가 스폰되고 디스폰되는가
//     ② RPC 3종이 Host ↔ Client 를 실제로 왕복하는가
//     ③ 한 번에 실어 보낼 수 있는 바이트 수가 얼마인가(실측)
//   **진짜 맵 바이트를 얹는 것은 F 단계**이고, 해시 대조는 G, Client 재생성 검증은 H 다.
//   그래서 지금 이 클래스는 **어디에서도 호출되지 않는다.** 그것이 정상이다.
//   (배선 — 누가 이 객체를 스폰하고 누가 전송을 시작하는가 — 은 F·I 단계의 몫이며,
//    GameBootstrapper 계열·BattleViewModel·NetworkGameManager 는 이번에 건드리지 않았다.)
//
// ── 이번 단계(B)가 소유하는 것 / F 이후로 미룬 것 ──────────────────────────
//   [B 에 있다]  스폰·디스폰 수명, 상태 기계와 그 전이 로그, nonce(전송 회차 번호),
//                RPC 3종의 서명과 왕복, MapChunkAssembler 를 이용한 조각 분할·재조립,
//                전송 한도를 재기 위한 프로브(더미 바이트)
//   [F 로 미룸]  진짜 canonical 맵 바이트 싣기, timeout 10초 감시와 1회 재전송,
//                MapHandoff 에 확정 맵 심기, 전송 시작 진입점 배선
//   [G 로 미룸]  SHA-256 해시 대조, 실패 시 로비 유지
//   [H 로 미룸]  Client 가 같은 seed 로 맵을 다시 만들어 대조하는 「D 방식」 검증
//   각 자리에 [F 단계] / [G 단계] / [H 단계] 주석을 달아 두었으니 그걸 따라가면 된다.
//
// ── 왜 Infrastructure 레이어인가 ──────────────────────────────────────────
//   NetworkBehaviour 는 이 프로젝트에서 **Infrastructure 에만** 둘 수 있다.
//   (Application 이 Unity.Netcode 를 직접 참조하는 것도 금지되어 있다.)
//   맵을 "만드는" 쪽은 Application(MapPreparationUseCase, 순수 C#)이고,
//   맵을 "보내는" 쪽이 여기다. 방향이 Infrastructure → Application 이라 역참조가 아니다.
//
// ── ⚠️ 이 파일은 이 저장소 환경에서 컴파일 검증이 불가능하다 ───────────────
//   Unity.Netcode 를 참조하므로 mcs/mono 로 빌드할 수 없다. **Unity 에서만 확인된다.**
//   그래서 "검증 가능한 계산 부분"은 일부러 MapChunkAssembler.cs(순수 C#)로 빼 두었고,
//   그쪽은 조각 1개/여러 개/중복/역순/하나 빠짐/길이 불일치 6가지 입력으로 실제 실행 검증을 마쳤다.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 맵 전송 왕복의 진행 단계.
    /// 상태가 바뀔 때마다 로그를 한 줄 남긴다(LogRules 1.14 금지 8 — 매 틱이 아니라 **전이** 시점에만).
    /// </summary>
    public enum MapTransferState
    {
        /// <summary>아무것도 하고 있지 않다. 스폰 직후의 상태.</summary>
        Idle,

        /// <summary>[Host] 시작을 알리고 조각을 보내는 중이다.</summary>
        Sending,

        /// <summary>[Host] 조각을 다 보냈고 상대의 MapReady 응답을 기다리는 중이다.</summary>
        WaitingReady,

        /// <summary>[Client] 시작 통보를 받아 조각을 모으는 중이다.</summary>
        Receiving,

        /// <summary>한 회차가 정상적으로 끝났다.</summary>
        Completed,

        /// <summary>한 회차가 실패로 끝났다.</summary>
        Failed
    }

    /// <summary>
    /// 맵 전송 전용 NetworkObject. Host 가 동적으로 스폰해서 쓰며,
    /// 최초 경기와 재경기(NewMap)가 **같은 객체·같은 프로토콜**을 공유한다
    /// (GameSystemRules_RandomMap.md 규칙 16 — "씬에 종속되지 않는 공용 전송 경로").
    ///
    /// ⚠️ 이 클래스는 "씬 재로드를 넘어 살아남는 방법"을 스스로 정하지 않는다.
    ///    그 조건은 3단계 A 에서 사용자 실기로 확인하기로 한 항목이고 **아직 확정되지 않았다.**
    ///    확정되기 전에 DontDestroyOnLoad 같은 것을 임의로 넣으면 근거 없는 결정이 박힌다.
    /// </summary>
    public class NetworkMapTransfer : NetworkBehaviour
    {
        // ====================================================================
        // 로그 상수
        // ====================================================================

        /// <summary>
        /// 로그의 System 필드 값.
        ///
        /// ⚠️ 왜 "Map" 이 아니라 "Network" 인가:
        ///    LogRules 1.4 는 System 을 **그 로그가 다루는 「기능」**으로 정하라고 한다.
        ///    이번 단계의 로그는 전부 "전송 객체가 스폰됐다 / RPC 가 왕복했다"에 관한 것이라
        ///    맵 내용이 아니라 네트워크 기능이다. 그래서 Infrastructure/Network 의 다른
        ///    컨트롤러들과 같은 "Network" 를 쓴다.
        ///    🔴 F 단계에서 **맵 내용**(seed·해시·맵 유형)을 다루는 로그를 붙일 때는
        ///       그 줄의 System 을 "Map" 으로 할지 여기서 다시 판정해야 한다.
        ///       2단계 K 가 맵 준비 로그에 "Map" 이라는 값을 이미 만들어 두었다.
        /// </summary>
        private const string TransferLogSystem = "Network";

        // ====================================================================
        // 전송 한도 — 🔴 아직 실측 전이다. 여기 있는 숫자는 전부 「잠정값」이다.
        // ====================================================================

        /// <summary>
        /// 🔴🔴 **실측이 끝났는가. 지금은 false 다.** 🔴🔴
        ///
        /// 아래 조각 크기를 확정하려면 두 숫자를 실제로 재야 하는데, 둘 다
        /// **사용자 실기 2대로만 잴 수 있고 이 저장소 환경에서는 잴 방법이 없다.**
        ///   ① NGO RPC 한 번에 실제로 실려 가는 페이로드 상한
        ///      (씬의 UnityTransport 설정값 m_MaxPayloadSize = 6144 는 **설정값이지 실효 상한이 아니다.**
        ///       NGO 헤더와 FastBufferWriter 오버헤드가 그 안에서 먼저 소비된다.)
        ///   ② Relay 를 경유했을 때의 실효 MTU
        ///      (로컬 127.0.0.1 로 잰 값을 Relay 값으로 삼으면 안 된다.)
        ///
        /// 재는 방법은 아래 RunTransferProbe 가 준비해 두었다.
        /// 실측이 끝나면 ⓐ 이 값을 true 로 바꾸고 ⓑ 조각 크기를 확정값으로 교체하며
        /// ⓒ 두 숫자와 **측정 방법**을 TechnicalDesignDocument.md 「전송 프로토콜」 절에 적는다.
        /// (규칙 16 이 "값은 구현 시 NGO 실측으로 확정하고 근거와 함께 TDD 에 기록한다"고 지시한다.)
        /// </summary>
        public const bool IsChunkSizeMeasured = false;

        /// <summary>
        /// 🔴 **잠정** 조각 크기(바이트). 이름에 Provisional 을 붙인 것은 실수 방지 장치다 —
        /// 이 이름을 쓰는 코드를 읽는 사람이 "아직 안 정해진 값"임을 바로 알아채게 하려는 것이다.
        ///
        /// 왜 하필 1024 인가: **근거가 없다.** 그래서 확정값이 아니라 잠정값이다.
        /// 규칙 16 이 경계한 것이 바로 "근거 없는 숫자를 근거 없는 다른 숫자로 바꾸는 일"이므로,
        /// 실측 전까지는 값을 못 박지 않고 「잠정」이라는 사실 자체를 코드에 남긴다.
        ///
        /// ⚠️ 참고로 실전에서 조각은 거의 항상 **1개**다. 맵 canonical 바이트가
        ///    323~343바이트(공식 319 + 4N + 20D, 장식 D=0)라서 어떤 잠정값을 넣어도
        ///    한 조각에 들어간다. 그래도 조각 구조를 만들어 두는 이유는 규칙 16 이
        ///    조각 전송을 요구하고, 장식이 도입되면 조각 수가 자연히 늘기 때문이다.
        /// </summary>
        public const int ProvisionalChunkSizeBytes = 1024;

        /// <summary>
        /// Host 가 응답을 기다리는 시간(초). 규칙 16 이 **10초**로 정한 값이라 이것은 잠정값이 아니다.
        /// ⚠️ 다만 **시간을 재서 실패로 넘기는 코드는 F 단계 몫**이다. 여기서는 계약만 적어 둔다.
        /// </summary>
        public const int TransferTimeoutSeconds = 10;

        /// <summary>
        /// 같은 준비 작업을 다시 보낼 수 있는 횟수. 규칙 16 이 **1회**로 못 박았다.
        /// ⚠️ 실제 재전송 코드도 F 단계 몫이다.
        /// </summary>
        public const int MaxResendCount = 1;

        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary>현재 진행 단계. 바뀔 때는 반드시 <see cref="SetState"/> 를 거친다(로그를 놓치지 않으려고).</summary>
        private MapTransferState _state = MapTransferState.Idle;

        /// <summary>
        /// 전송 회차 번호. Host 가 회차를 시작할 때마다 1씩 올린다.
        ///
        /// 왜 필요한가(초급자용 설명): 재전송이나 재경기 때문에 **지난 회차의 조각이 뒤늦게**
        /// 도착할 수 있다. 번호가 없으면 그 낡은 조각을 이번 회차 버퍼에 섞어 넣게 되고,
        /// 그러면 해시가 어긋나 원인을 찾기 어려운 실패가 된다. 번호가 다르면 그냥 버린다.
        /// </summary>
        private ulong _activeNonce;

        /// <summary>
        /// [Client 전용] 지금 회차의 조각을 모으는 재조립기.
        /// 🔴 이 객체는 **완성되기 전에는 절대 바이트를 내주지 않는다**(MapChunkAssembler 참조).
        /// 회차가 없을 때는 null 이다.
        /// </summary>
        private MapChunkAssembler _assembler;

        // ====================================================================
        // 읽기 전용 진행 상황 (테스트·로그·F 단계 배선에서 쓴다)
        // ====================================================================

        /// <summary>현재 진행 단계.</summary>
        public MapTransferState State { get { return _state; } }

        /// <summary>현재 전송 회차 번호. 아직 한 번도 시작하지 않았으면 0.</summary>
        public ulong ActiveNonce { get { return _activeNonce; } }

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 호출된다. Host 가 이 프리팹을 Spawn 하면 양쪽에서 각각 한 번씩 불린다.
        ///
        /// 축 판정(LogRules 1.2 — 두 축을 **둘 다** 정해야 한다):
        ///   축 A = Info — 의도된 흐름이다. 문제가 아니다.
        ///   축 B = **개발** — 두 질문 중 ①에서 "아니오"가 나온다.
        ///     ① 플레이어 기기에서만 벌어지는가? → **아니오.** 스폰·디스폰은 에디터 2인 구성
        ///        (에디터 Host + 또 하나의 클라이언트)에서 Relay 없이도 그대로 재현된다.
        ///        회선 품질이나 그 판의 seed 같은 "그 기기에서만 생기는 조건"에 달려 있지 않다.
        ///     → 하나라도 "아니오"면 개발이다. 선례와도 일치한다 —
        ///        「스폰/디스폰/구독완료/RPC 수신 덤프」는 이 프로젝트에서 계속 개발 축으로 판정해 왔다.
        ///   ⚠️ 이 파일에 새로 만든 **운영 키 5종은 F 이후의 사건용**이다(끝내 실패·해시 불일치 등).
        ///      스폰됐다는 사실에 그 키를 붙이면 장애 지표가 정상 사건으로 오염된다.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "맵 전송 객체 스폰 완료",
                             $"Role={(IsServer ? "Host" : "Client")}, IsServer={IsServer}, " +
                             $"NetworkObjectId={NetworkObjectId}, State={_state}");
        }

        /// <summary>
        /// 네트워크 디스폰 시 호출된다. 회차 상태를 남기지 않고 깨끗이 지운다.
        ///
        /// ⚠️ 왜 여기서 반드시 비워야 하는가(초급자용):
        ///    재조립기를 들고 있는 채로 디스폰되면, 다음 회차에 **지난 회차의 절반짜리 버퍼**가
        ///    살아 있을 수 있다. 규칙 16 이 "부분 데이터는 어떤 검증이나 맵 구성에도 쓰지 않는다"고
        ///    못 박은 이유가 그것이다. 참조를 끊어 두는 것이 가장 확실한 예방이다.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "맵 전송 객체 디스폰",
                             $"Role={(IsServer ? "Host" : "Client")}, " +
                             $"Nonce={_activeNonce}, State={_state}");

            ResetSession();

            base.OnNetworkDespawn();
        }

        // ====================================================================
        // 상태 전이
        // ====================================================================

        /// <summary>
        /// 상태를 바꾸고 그 전이를 로그로 한 줄 남긴다.
        ///
        /// 같은 값으로 바꾸는 호출은 **아무 일도 하지 않는다.** 이유는 LogRules 1.14 금지 8 —
        /// 상태가 실제로 바뀌지 않았는데 줄을 남기면 그게 곧 스팸이고, 정작 필요한 줄이 묻힌다.
        /// </summary>
        /// <param name="next">바꿔 넣을 상태.</param>
        /// <param name="reason">왜 바뀌었는지(사람이 읽을 문장). 집계 키가 아니라 메시지 쪽에 들어간다.</param>
        private void SetState(MapTransferState next, string reason)
        {
            if (_state == next)
            {
                return;
            }

            MapTransferState previous = _state;
            _state = next;

            // 축 A = Info(의도된 흐름) · 축 B = 개발(위 OnNetworkSpawn 의 판정과 같은 이유).
            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             $"맵 전송 상태 전이 — {reason}",
                             $"Role={(IsServer ? "Host" : "Client")}, " +
                             $"PreviousState={previous}, State={next}, Nonce={_activeNonce}");
        }

        /// <summary>
        /// 회차 상태(재조립기)를 비운다. 회차가 끝났거나 디스폰될 때 부른다.
        /// nonce 는 **일부러 유지한다** — 낡은 조각을 구분하는 기준이라 0 으로 되돌리면
        /// 지난 회차 조각을 이번 것으로 착각할 수 있다.
        /// </summary>
        private void ResetSession()
        {
            _assembler = null;
        }

        // ====================================================================
        // [Host] 전송 시작 — 🔴 B 단계에서는 「더미 바이트」만 보낸다
        // ====================================================================

        /// <summary>
        /// 🔴 **전송 한도 실측용 프로브.** 진짜 맵이 아니라 **더미 바이트**를 보낸다.
        ///
        /// 이 메서드가 존재하는 이유는 하나다 — 위 IsChunkSizeMeasured 가 말한 두 숫자
        /// (NGO RPC 실효 페이로드 상한 · Relay 실효 MTU)는 **실제로 보내 봐야만** 알 수 있고,
        /// 그건 사용자 실기 2대에서만 가능하기 때문이다.
        ///
        /// 쓰는 법(사용자):
        ///   1) Host·Client 를 연결한다(로컬이면 NGO 실효 상한, Relay 면 Relay MTU 를 재는 것이다).
        ///   2) 하이러키에서 스폰된 이 객체를 고르고 인스펙터 컨텍스트 메뉴 항목을 누른다.
        ///   3) 로그에서 어느 크기까지 MapReady 가 돌아오는지 본다. 돌아오지 않는 첫 크기가 상한이다.
        ///
        /// ⚠️ 실전 경로와 **같은 RPC** 를 쓴다. 일부러 그렇게 했다 — 프로브 전용 RPC 를 따로 만들면
        ///    정작 실전에서 쓰는 경로의 한도를 잰 것이 아니게 된다.
        /// </summary>
        /// <param name="probeBytes">보낼 더미 데이터의 전체 바이트 수(1 이상).</param>
        /// <param name="chunkSize">조각 하나의 최대 크기(1 이상). 기본은 위 잠정값.</param>
        public void RunTransferProbe(int probeBytes, int chunkSize)
        {
            // IsSpawned 를 **앞에** 두는 이유: 단락 평가로, 스폰되지 않은 상태(싱글플레이 등)에서는
            // IsServer 를 아예 건드리지 않게 된다. NetworkManager 가 멈춘 뒤에도 IsServer 가
            // 참일 수 있어서 RPC 발신이 예외로 터지는 사고가 이 프로젝트에 실제로 있었다.
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            if (probeBytes < 1 || chunkSize < 1)
            {
                // 축 A = Warn(요청만 거부하고 객체는 계속 산다) · 축 B = 개발(호출부 실수라 에디터에서 드러난다).
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "전송 프로브 인자가 올바르지 않아 요청을 무시했다",
                                 $"ProbeBytes={probeBytes}, ChunkSize={chunkSize}");
                return;
            }

            _activeNonce++;
            ResetSession();
            SetState(MapTransferState.Sending, "프로브 전송 시작");

            // 더미 데이터. 0 이 아니라 규칙적인 값을 넣는 이유는, 받는 쪽에서 "버퍼가 안 채워진 것"과
            // "0 이 들어온 것"을 눈으로 구분할 수 있게 하기 위해서다.
            byte[] probe = new byte[probeBytes];
            for (int i = 0; i < probe.Length; i++)
            {
                probe[i] = (byte)((i % 251) + 1);
            }

            byte[][] chunks = MapChunkAssembler.Split(probe, chunkSize);

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "전송 프로브 — 시작을 알리고 조각을 보낸다",
                             $"Nonce={_activeNonce}, TotalBytes={probe.Length}, " +
                             $"ChunkSize={chunkSize}, ChunkCount={chunks.Length}");

            MapPrepareBeginClientRpc(_activeNonce, probe.Length, chunkSize);

            for (int i = 0; i < chunks.Length; i++)
            {
                MapChunkClientRpc(_activeNonce, i, chunks[i]);
            }

            SetState(MapTransferState.WaitingReady, "조각을 다 보내고 응답 대기");

            // [F 단계] 여기서 timeout 10초 감시를 걸고, 시간이 지나면 1회 재전송한다.
            //          재전송 시 운영 로그 MapTransferRetried 를 남긴다.
            // [F 단계] 진짜 전송은 이 프로브 대신 canonical 맵 바이트를 받는 메서드가 맡는다.
        }

        /// <summary>
        /// 인스펙터 컨텍스트 메뉴에서 한 번에 실행하기 위한 편의 진입점.
        /// 잠정 조각 크기로 잠정 조각 크기만큼(= 조각 1개) 보낸다.
        /// 한도를 찾을 때는 위 <see cref="RunTransferProbe"/> 를 크기를 키워 가며 부른다.
        /// </summary>
        [ContextMenu("전송 프로브 1회 실행 (잠정 조각 크기)")]
        private void RunTransferProbeWithProvisionalSize()
        {
            RunTransferProbe(ProvisionalChunkSizeBytes, ProvisionalChunkSizeBytes);
        }

        // ====================================================================
        // RPC ① 시작 통보 (Host → Client)
        // ====================================================================

        /// <summary>
        /// [Host → Client] "이제부터 전체 totalBytes 바이트를 chunkSize 단위로 보낸다"는 예고.
        /// 받는 쪽은 이 값으로 재조립기를 만든다.
        ///
        /// ⚠️ 메서드 이름이 반드시 ClientRpc 로 끝나야 한다. NGO 의 규약이고, 어기면 컴파일되지 않는다.
        /// ⚠️ 맨 앞의 서버 되돌림은 **가드**다. Host 는 서버이자 클라이언트라 자기가 보낸 ClientRpc 를
        ///    자기도 받는데, 그대로 두면 Host 가 자기 자신의 조각을 모으고 로그도 같은 사건에 두 줄이 된다
        ///    (LogRules 1.14 금지 9). 가드 자체에는 로그를 넣지 않는다 — 가드에 걸리는 것은 정상 흐름이라
        ///    상태 전이 지점이 아니고, 매번 남기면 금지 8(스팸)에 걸린다.
        /// </summary>
        /// <param name="nonce">전송 회차 번호.</param>
        /// <param name="totalBytes">전부 합쳤을 때의 바이트 수.</param>
        /// <param name="chunkSize">조각 하나의 최대 크기.</param>
        [ClientRpc]
        private void MapPrepareBeginClientRpc(ulong nonce, int totalBytes, int chunkSize)
        {
            if (IsServer)
            {
                return;
            }

            if (totalBytes < 1 || chunkSize < 1)
            {
                // 축 A = Warn(회차를 시작하지 않고 넘어갈 뿐 객체는 산다) · 축 B = 개발
                // (보내는 쪽 코드의 실수로만 도달하는 분기라 에디터에서 드러난다).
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "시작 통보의 크기 값이 올바르지 않아 회차를 시작하지 않았다",
                                 $"Nonce={nonce}, TotalBytes={totalBytes}, ChunkSize={chunkSize}");
                return;
            }

            _activeNonce = nonce;
            _assembler = new MapChunkAssembler(totalBytes, chunkSize);

            SetState(MapTransferState.Receiving, "시작 통보 수신");

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "시작 통보 수신 — 재조립기를 준비했다",
                             $"Nonce={nonce}, TotalBytes={totalBytes}, " +
                             $"ChunkSize={chunkSize}, ChunkCount={_assembler.ChunkCount}");

            // [G 단계] 여기서 기대 해시(Host 가 함께 보낸 값)를 보관해 두었다가
            //          완성 후 대조한다. 불일치면 운영 로그 MapHashMismatch 를 남긴다.
        }

        // ====================================================================
        // RPC ② 조각 (Host → Client)
        // ====================================================================

        /// <summary>
        /// [Host → Client] 조각 하나. 도착 순서는 보장되지 않아도 되고, 같은 조각이 두 번 와도 된다.
        /// 그 처리는 전부 <see cref="MapChunkAssembler"/> 가 맡는다(중복 무시·index 자리 채우기·길이 검사).
        ///
        /// 🔴 완성 판정은 "조각 수"만이 아니라 **바이트 합계가 선언된 크기와 같은가**까지 본다.
        ///    규칙 16 의 "모든 조각이 모여 선언된 크기와 일치한 뒤에야"를 그대로 옮긴 것이며,
        ///    그 판정은 재조립기 안에 들어 있어 이 자리에서 실수로 건너뛸 수 없다.
        /// </summary>
        /// <param name="nonce">전송 회차 번호. 지금 회차와 다르면 낡은 조각이므로 버린다.</param>
        /// <param name="index">조각 번호.</param>
        /// <param name="data">조각 내용.</param>
        [ClientRpc]
        private void MapChunkClientRpc(ulong nonce, int index, byte[] data)
        {
            if (IsServer)
            {
                return;
            }

            if (_assembler == null || nonce != _activeNonce)
            {
                // 낡은 회차의 조각이거나 시작 통보를 못 받은 상태다.
                // 축 A = Warn(이 조각만 버리고 계속 간다) · 축 B = 개발
                // (회차 번호가 어긋나는 것은 에디터 2인 구성에서도 그대로 재현된다).
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "지금 회차의 조각이 아니어서 버렸다",
                                 $"Nonce={nonce}, ActiveNonce={_activeNonce}, ChunkIndex={index}");
                return;
            }

            MapChunkAcceptResult result = _assembler.Accept(index, data);

            if (result != MapChunkAcceptResult.Accepted)
            {
                // 중복은 오류가 아니지만(신뢰성 전송이라도 재전송이 겹치면 생긴다),
                // 길이 불일치·범위 밖은 보내는 쪽과 받는 쪽의 계약이 어긋났다는 신호다.
                // 둘 다 이 조각만 버리고 계속 가므로 축 A = Warn, 축 B = 개발이다.
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "조각을 받아들이지 않았다",
                                 $"Nonce={nonce}, ChunkIndex={index}, Result={result}, " +
                                 $"ReceivedChunkCount={_assembler.ReceivedChunkCount}, " +
                                 $"ChunkCount={_assembler.ChunkCount}");
                return;
            }

            if (!_assembler.IsComplete)
            {
                // 🔴 아직 다 모이지 않았다. 여기서는 **아무것도 꺼내지 않는다.**
                //    조각마다 로그를 남기지도 않는다 — 조각이 많아지면 그게 곧 스팸이다(금지 8).
                return;
            }

            byte[] payload;
            if (!_assembler.TryGetAssembled(out payload))
            {
                // 바로 위에서 완성을 확인했으므로 여기 도달하면 재조립기 자체의 버그다.
                // 축 A = Warn · 축 B = 개발(코드 버그로만 도달하는 분기).
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "완성으로 판정됐는데 결과를 꺼내지 못했다",
                                 $"Nonce={nonce}, TotalBytes={_assembler.TotalBytes}, " +
                                 $"ReceivedBytes={_assembler.ReceivedByteCount}");
                SetState(MapTransferState.Failed, "재조립 결과를 꺼내지 못함");
                MapReadyServerRpc(nonce, false, 0);
                return;
            }

            SetState(MapTransferState.Completed, "조각을 전부 받아 재조립 완료");

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "조각 재조립 완료 — 응답을 보낸다",
                             $"Nonce={nonce}, TotalBytes={payload.Length}, " +
                             $"ChunkCount={_assembler.ChunkCount}");

            // 🔴🔴 B 단계에서는 여기가 끝이다. 받은 바이트는 **더미**이므로 아무 데도 쓰지 않는다. 🔴🔴
            // [G 단계] ① SHA-256 해시를 구해 Host 가 보낸 기대 해시와 대조한다(원본 32바이트로 대조,
            //             로그에는 앞 16자만 싣는다 — 2단계 K 가 정한 규약). 어긋나면 MapHashMismatch.
            //          ② 통과하면 Domain 코덱으로 역직렬화하고 공정성 검증을 돌린다.
            // [H 단계] ③ 같은 seed 로 맵을 다시 만들어 대조한다(D 방식). 실패는 MapClientVerificationFailed.
            // [F 단계] ④ 최종 확정 맵을 MapHandoff 에 심어 전투 씬의 GameBootstrapper 가 읽게 한다.

            MapReadyServerRpc(nonce, true, payload.Length);

            ResetSession();
        }

        // ====================================================================
        // RPC ③ 수신 완료 통보 (Client → Host)
        // ====================================================================

        /// <summary>
        /// [Client → Host] "그 회차 잘 받았다 / 못 받았다"는 답.
        ///
        /// ⚠️ RequireOwnership = false 가 **반드시** 필요하다.
        ///    이 객체는 Host 가 스폰했으므로 소유자도 Host 다. 기본 설정(소유자만 호출 가능)으로 두면
        ///    정작 답을 보내야 할 Client 가 이 RPC 를 부르지 못하고 조용히 막힌다.
        /// </summary>
        /// <param name="nonce">응답하는 회차 번호.</param>
        /// <param name="success">정상적으로 다 받았는가.</param>
        /// <param name="receivedBytes">받은 전체 바이트 수(로그·실측 대조용).</param>
        /// <param name="rpcParams">NGO 가 채워 주는 발신자 정보. 누가 보냈는지 로그에 남기려고 받는다.</param>
        [ServerRpc(RequireOwnership = false)]
        private void MapReadyServerRpc(ulong nonce, bool success, int receivedBytes,
                                       ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (nonce != _activeNonce)
            {
                // 낡은 회차에 대한 답이다. 축 A = Warn(무시하고 계속) · 축 B = 개발.
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "지금 회차의 응답이 아니어서 무시했다",
                                 $"Nonce={nonce}, ActiveNonce={_activeNonce}, ClientId={senderClientId}");
                return;
            }

            SetState(success ? MapTransferState.Completed : MapTransferState.Failed,
                     success ? "응답 수신(성공)" : "응답 수신(실패)");

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "수신 완료 응답을 받았다",
                             $"Nonce={nonce}, ClientId={senderClientId}, " +
                             $"Success={success}, ReceivedBytes={receivedBytes}");

            // [F 단계] 여기서 timeout 감시를 해제하고, 실패면 1회에 한해 재전송한다.
            //          최종 성공은 운영 로그 MapTransferSucceeded, 끝내 실패는 MapTransferFailed 다.
            // [I 단계] 성공했을 때에만 전투 씬 전환 게이트를 연다
            //          (규칙 16 — 해시가 같을 때만 씬 전환을 시작한다).
        }
    }
}
