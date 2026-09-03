// ============================================================================
// MapRandomStreams.cs
// 맵 생성용 난수 "스트림" 4개의 고정 ID 와 seed 파생 규칙.
//
// ─────────────────────────────────────────────────────────────────────────────
// 스트림이란 무엇이고 왜 4개로 나누는가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   난수를 하나의 발생기에서 순서대로 뽑아 쓰면, 앞쪽에서 몇 개를 뽑았느냐에 따라
//   뒤쪽 결과가 통째로 밀린다. 예를 들어 "장식을 놓는 코드"가 난수를 한 번만 더
//   뽑아도, 그 뒤에 뽑는 "지형"과 "광산 위치"가 전부 달라져 버린다.
//   그러면 장식 기능을 손볼 때마다 지형이 바뀌어 비교·재현이 불가능해진다.
//
//   그래서 책임별로 발생기를 따로 만든다. 각 발생기는 자기 seed 로 시작하고
//   자기 상태만 갖는다(MapRandom 인스턴스 1개 = 스트림 1개).
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 이 설계가 보장해야 하는 네 가지 성질 (TDD 원문 그대로)
// ─────────────────────────────────────────────────────────────────────────────
//   - Decoration의 draw 수나 호출 순서 변경이 Terrain/MinePlacement 결과를 바꾸지 않는다.
//   - Terrain과 MinePlacement도 서로의 draw 수에 결합되지 않는다.
//   - Attempt-N의 draw 수가 Attempt-(N+1)의 시작 상태를 바꾸지 않는다.
//   - 동일 MapVersion + root seed는 동일한 맵 선택, 후보 순서, 검증/폴백 결정과
//     최종 결과를 재현한다.
//
//   어떻게 보장하는가:
//
//   1·2번(스트림끼리 독립)
//       각 스트림의 seed 는 스트림 ID 만 다른 독립 파생값이고, 상태는 MapRandom
//       인스턴스 안에만 있다. 즉 A 스트림에서 몇 번을 뽑든 B 스트림의 상태 변수에는
//       손이 닿지 않는다. "순서를 지키자"는 약속이 아니라 구조로 막는다.
//       (그래서 이 클래스는 스트림을 만들어 주기만 하고, 만들어진 뒤에는
//        스트림끼리 이어 주는 어떤 통로도 두지 않는다.)
//
//   3번(시도끼리 독립)
//       시도 N 의 seed 는 "도메인 seed + 시도 번호"로 그 자리에서 계산한다.
//       앞 시도가 난수를 몇 개 뽑고 끝났는지는 계산식에 들어가지 않으므로
//       시도 N 의 뽑기 횟수가 시도 N+1 의 시작 상태를 건드릴 수 없다.
//       ⚠️ 앞 시도의 발생기를 이어서 쓰면 이 성질이 즉시 깨진다.
//          시도를 새로 시작할 때는 반드시 CreateAttemptStream 으로 새로 만든다.
//
//   4번(재현성)
//       파생에 쓰는 재료는 MapVersion, root seed, 고정 정수 스트림 ID, 시도 번호뿐이다.
//       시간·프레임 수·오브젝트 주소·문자열 해시처럼 실행할 때마다 달라지는 것은
//       재료에 들어가지 않는다. 섞기 함수도 정수 연산만 쓰는 MapRandom.Combine 하나다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 파생 순서 (TDD 「결정적 PRNG 및 독립 스트림 계약」에 고정된 형태)
// ─────────────────────────────────────────────────────────────────────────────
//   domainSeed  = Derive(mapVersion, rootSeed, fixedDomainId)
//   attemptSeed = Derive(domainSeed, attemptIndex)
//
//   이 파일의 구현은 위 두 줄을 그대로 옮긴 것이며, 실제 계산 순서는
//   domainSeed = Combine(Combine(mapVersion, rootSeed), streamId) 다.
//
// 🔴 스트림 이름을 런타임에 문자열 해시로 바꿔 ID 를 만들지 않는다.
//    문자열 해시는 실행할 때마다 값이 달라질 수 있어 재현성을 깨뜨린다.
//    아래 상수처럼 스키마에 박아 둔 정수만 쓴다.
//
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음(양쪽 다 참조하지 않는다).
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 맵 생성용 난수 스트림의 고정 ID 와 seed 파생 함수를 모아 둔 정적 클래스.
    /// </summary>
    public static class MapRandomStreams
    {
        // ====================================================================
        // 스키마 고정 정수 스트림 ID
        // 🔴 값을 바꾸거나 재사용하면 같은 seed 로 다른 맵이 나온다.
        //    새 스트림이 필요하면 기존 값을 건드리지 말고 다음 번호를 추가한다.
        //    0 은 "지정되지 않음"을 뜻하는 자리로 비워 둔다.
        // ====================================================================

        /// <summary>
        /// MapType, 허용 중립 광산 수, 시작 광산 방향(A/B 50:50)을 고르는 스트림.
        /// 경기당 한 번만 쓰며 재시도해도 다시 뽑지 않는다(규칙 3 「경기 선택 단계」).
        /// </summary>
        public const int MapSelection = 1;

        /// <summary> 완전 차단 지형과 건설 불가 구역을 정하는 스트림. </summary>
        public const int Terrain = 2;

        /// <summary> 시작 광산과 중립 광산 배치를 정하는 스트림. </summary>
        public const int MinePlacement = 3;

        /// <summary>
        /// 장식의 종류·변형·회전을 정하는 스트림.
        /// 최초 구현에서는 장식 목록이 항상 비어 있어 이 스트림에서 아무것도 뽑지 않는다(규칙 15).
        /// </summary>
        public const int Decoration = 4;

        /// <summary>
        /// 위 네 개를 순회할 때 쓰는 목록(진단·자기 검증용).
        /// 순서는 ID 오름차순이며, 이 순서가 파생 결과에 영향을 주지는 않는다.
        /// </summary>
        public static readonly int[] AllStreamIds = { MapSelection, Terrain, MinePlacement, Decoration };

        /// <summary> 한 경기에서 허용하는 최대 생성 시도 횟수(규칙 12). 시도 번호는 0 ~ 99. </summary>
        public const int MaxAttemptCount = 100;

        // ====================================================================
        // seed 파생
        // ====================================================================

        /// <summary>
        /// 스트림 하나의 도메인 seed 를 만든다. 경기 내내 바뀌지 않는 값이다.
        /// </summary>
        /// <param name="mapVersion">canonical 형식 버전(MapDefinition.CurrentMapVersion)</param>
        /// <param name="rootSeed">경기의 64비트 최상위 시드</param>
        /// <param name="streamId">이 클래스의 고정 스트림 ID</param>
        /// <returns>해당 스트림의 도메인 seed</returns>
        public static ulong DeriveDomainSeed(int mapVersion, ulong rootSeed, int streamId)
        {
            // (uint) 캐스팅: 음수 int 를 ulong 으로 바로 넓히면 상위 비트가 1 로 채워져
            //               값이 예상과 달라진다. 32비트로 자른 뒤 넓혀 규칙을 고정한다.
            ulong versionAndRoot = MapRandom.Combine((ulong)(uint)mapVersion, rootSeed);
            return MapRandom.Combine(versionAndRoot, (ulong)(uint)streamId);
        }

        /// <summary>
        /// 도메인 seed 와 시도 번호로 그 시도 전용 seed 를 만든다.
        /// 앞 시도에서 난수를 몇 번 뽑았는지는 계산에 들어가지 않는다.
        /// </summary>
        /// <param name="domainSeed">DeriveDomainSeed 결과</param>
        /// <param name="attemptIndex">시도 번호(0 ~ MaxAttemptCount-1)</param>
        /// <returns>그 시도 전용 seed</returns>
        public static ulong DeriveAttemptSeed(ulong domainSeed, int attemptIndex)
        {
            if (attemptIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptIndex), "시도 번호는 0 이상이어야 한다.");
            }
            return MapRandom.Combine(domainSeed, (ulong)(uint)attemptIndex);
        }

        // ====================================================================
        // 스트림 생성
        // ====================================================================

        /// <summary>
        /// 경기 단위 스트림을 만든다. 재시도와 무관하게 경기당 한 번만 쓰는 용도이며,
        /// 현재는 MapSelection 이 유일한 사용처다(규칙 3 「경기 선택 단계」).
        /// </summary>
        /// <param name="mapVersion">canonical 형식 버전</param>
        /// <param name="rootSeed">경기의 64비트 최상위 시드</param>
        /// <param name="streamId">고정 스트림 ID</param>
        /// <returns>새 난수 스트림</returns>
        public static MapRandom CreateMatchStream(int mapVersion, ulong rootSeed, int streamId)
        {
            return new MapRandom(DeriveDomainSeed(mapVersion, rootSeed, streamId));
        }

        /// <summary>
        /// 시도 단위 스트림을 만든다. 시도를 새로 시작할 때마다 반드시 이 메서드로
        /// 새 인스턴스를 만들고, 앞 시도의 인스턴스를 이어서 쓰지 않는다.
        /// </summary>
        /// <param name="mapVersion">canonical 형식 버전</param>
        /// <param name="rootSeed">경기의 64비트 최상위 시드</param>
        /// <param name="streamId">고정 스트림 ID</param>
        /// <param name="attemptIndex">시도 번호(0 ~ MaxAttemptCount-1)</param>
        /// <returns>그 시도 전용 새 난수 스트림</returns>
        public static MapRandom CreateAttemptStream(int mapVersion, ulong rootSeed, int streamId, int attemptIndex)
        {
            ulong domainSeed = DeriveDomainSeed(mapVersion, rootSeed, streamId);
            return new MapRandom(DeriveAttemptSeed(domainSeed, attemptIndex));
        }

        // ====================================================================
        // 검증 벡터 (테스트 벡터)
        //
        // TDD 가 "PRNG 알고리즘, 정수 폭, 오버플로 규칙, seed 파생 순서를 프로젝트
        // 코드와 테스트 벡터로 고정한다" 고 요구한다. 이 프로젝트에는 단위 테스트
        // 어셈블리가 없으므로(2026-09-03 확인: Assets 하위 asmdef 는 외부 패키지 1개뿐),
        // 기대값을 상수로 박아 둔 자기 검증 루틴을 여기에 둔다.
        //
        // 기대값은 같은 알고리즘을 파이썬으로 따로 구현해 계산한 값이다.
        // 🔴 이 값이 깨졌다는 것은 "테스트가 틀렸다"가 아니라 "PRNG 규격이 바뀌었다"는
        //    뜻이다. 값을 고치기 전에 MapVersion 을 올려야 하는지부터 판단한다.
        // ====================================================================

        /// <summary> 검증 벡터에 쓰는 고정 root seed. </summary>
        private const ulong VectorRootSeed = 0x0123456789ABCDEFUL;

        /// <summary> 검증 벡터에 쓰는 고정 MapVersion. </summary>
        private const int VectorMapVersion = 1;

        /// <summary>
        /// 검증 벡터를 실행한다. 실패하면 false 를 돌려주고 사유를 채운다.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 된다.)
        /// </summary>
        /// <param name="failureReason">실패 사유. 성공하면 null</param>
        /// <returns>모든 벡터가 일치하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            // ── 1. 섞기 함수 자체 ────────────────────────────────────────────
            // Mix64(0) 이 0 인 것은 SplitMix64 finalizer 의 알려진 성질이다.
            // 그래서 Combine 은 salt 에 1 을 더하고, 스트림은 상태를 먼저 전진시킨다.
            if (!Check(MapRandom.Mix64(0UL), 0x0000000000000000UL, "Mix64(0)", out failureReason)) return false;
            if (!Check(MapRandom.Mix64(1UL), 0x5692161D100B05E5UL, "Mix64(1)", out failureReason)) return false;
            if (!Check(MapRandom.Combine(0UL, 0UL), 0xE220A8397B1DCDAFUL, "Combine(0,0)", out failureReason)) return false;

            // ── 2. 도메인 seed 파생 순서 ─────────────────────────────────────
            if (!Check(DeriveDomainSeed(VectorMapVersion, VectorRootSeed, MapSelection),
                       0x1B2F2F00FA7AD69CUL, "domainSeed MapSelection", out failureReason)) return false;
            if (!Check(DeriveDomainSeed(VectorMapVersion, VectorRootSeed, Terrain),
                       0x1626569ABECE1769UL, "domainSeed Terrain", out failureReason)) return false;
            if (!Check(DeriveDomainSeed(VectorMapVersion, VectorRootSeed, MinePlacement),
                       0xF68B89A15F89931EUL, "domainSeed MinePlacement", out failureReason)) return false;
            if (!Check(DeriveDomainSeed(VectorMapVersion, VectorRootSeed, Decoration),
                       0xBF73AACBB7A78706UL, "domainSeed Decoration", out failureReason)) return false;

            // ── 3. 시도 seed 파생 순서 ───────────────────────────────────────
            ulong terrainDomain = DeriveDomainSeed(VectorMapVersion, VectorRootSeed, Terrain);
            if (!Check(DeriveAttemptSeed(terrainDomain, 0), 0x4E5F400C26BB210BUL,
                       "attemptSeed Terrain-0", out failureReason)) return false;
            if (!Check(DeriveAttemptSeed(terrainDomain, 1), 0x75BC33FA43E1A9A4UL,
                       "attemptSeed Terrain-1", out failureReason)) return false;
            if (!Check(DeriveAttemptSeed(terrainDomain, MaxAttemptCount - 1), 0xCC6B972591720A76UL,
                       "attemptSeed Terrain-99", out failureReason)) return false;

            // ── 4. 원시 64비트 수열 (정수 폭·오버플로 규칙 고정) ─────────────
            var raw = new MapRandom(0UL);
            if (!Check(raw.NextUInt64(), 0xE220A8397B1DCDAFUL, "NextUInt64 #1", out failureReason)) return false;
            if (!Check(raw.NextUInt64(), 0x6E789E6AA1B965F4UL, "NextUInt64 #2", out failureReason)) return false;
            if (!Check(raw.NextUInt64(), 0x06C45D188009454FUL, "NextUInt64 #3", out failureReason)) return false;

            // ── 5. 균등 정수 뽑기(모듈로 편향 제거 경로 포함) ────────────────
            var ranged = new MapRandom(1234567890123456789UL);
            int[] expectedRanged = { 74, 88, 95, 87, 48 };
            for (int i = 0; i < expectedRanged.Length; i++)
            {
                int actual = ranged.NextInt(100);
                if (actual != expectedRanged[i])
                {
                    failureReason = "NextInt(100) #" + (i + 1) + " 기대 " + expectedRanged[i] + " / 실제 " + actual;
                    return false;
                }
            }

            // ── 6. 후보 목록에서 동일 확률 고르기 ────────────────────────────
            var chooser = new MapRandom(42UL);
            int[] candidates = { 3, 5, 7 };
            int[] expectedChoice = { 5, 5, 3, 3, 5, 3, 5, 7 };
            for (int i = 0; i < expectedChoice.Length; i++)
            {
                int actual = chooser.Choose(candidates);
                if (actual != expectedChoice[i])
                {
                    failureReason = "Choose #" + (i + 1) + " 기대 " + expectedChoice[i] + " / 실제 " + actual;
                    return false;
                }
            }

            // ── 7. 스트림 독립성 (성질 1·2) ──────────────────────────────────
            // Decoration 스트림에서 아무리 많이 뽑아도 Terrain 스트림의 결과는 그대로다.
            int[] terrainBefore = TakeInts(CreateAttemptStream(VectorMapVersion, VectorRootSeed, Terrain, 0), 5, 100);
            int[] expectedTerrain = { 40, 61, 84, 21, 87 };
            for (int i = 0; i < expectedTerrain.Length; i++)
            {
                if (terrainBefore[i] != expectedTerrain[i])
                {
                    failureReason = "Terrain attempt-0 #" + (i + 1) +
                                    " 기대 " + expectedTerrain[i] + " / 실제 " + terrainBefore[i];
                    return false;
                }
            }

            var decoration = CreateAttemptStream(VectorMapVersion, VectorRootSeed, Decoration, 0);
            for (int i = 0; i < 1000; i++) decoration.NextUInt64();

            int[] terrainAfter = TakeInts(CreateAttemptStream(VectorMapVersion, VectorRootSeed, Terrain, 0), 5, 100);
            for (int i = 0; i < terrainAfter.Length; i++)
            {
                if (terrainAfter[i] != terrainBefore[i])
                {
                    failureReason = "Decoration 스트림 소비가 Terrain 결과를 바꿨다(#" + (i + 1) + ")";
                    return false;
                }
            }

            // ── 8. 시도 독립성 (성질 3) ──────────────────────────────────────
            // 시도 0 에서 몇 번을 뽑든 시도 1 의 시작 상태는 그대로다.
            var attempt0 = CreateAttemptStream(VectorMapVersion, VectorRootSeed, Terrain, 0);
            for (int i = 0; i < 777; i++) attempt0.NextUInt64();

            int[] attempt1 = TakeInts(CreateAttemptStream(VectorMapVersion, VectorRootSeed, Terrain, 1), 5, 100);
            int[] expectedAttempt1 = { 88, 94, 3, 51, 97 };
            for (int i = 0; i < expectedAttempt1.Length; i++)
            {
                if (attempt1[i] != expectedAttempt1[i])
                {
                    failureReason = "Terrain attempt-1 #" + (i + 1) +
                                    " 기대 " + expectedAttempt1[i] + " / 실제 " + attempt1[i];
                    return false;
                }
            }

            // ── 9. 스트림 ID 가 서로 다른 seed 를 만드는지 ───────────────────
            var seen = new List<ulong>(AllStreamIds.Length);
            for (int i = 0; i < AllStreamIds.Length; i++)
            {
                ulong seed = DeriveDomainSeed(VectorMapVersion, VectorRootSeed, AllStreamIds[i]);
                if (seen.Contains(seed))
                {
                    failureReason = "서로 다른 스트림 ID 가 같은 도메인 seed 를 만들었다(ID " + AllStreamIds[i] + ")";
                    return false;
                }
                seen.Add(seed);
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 에디터에서만 실행되는 검증 벡터 확인. 어긋나면 즉시 예외로 알린다.
        /// 빌드에는 이 호출 자체가 컴파일되지 않는다(Conditional 특성).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void AssertSelfCheck()
        {
            if (!TryRunSelfCheck(out string reason))
            {
                throw new InvalidOperationException("MapRandom 검증 벡터 불일치: " + reason);
            }
        }

        // ====================================================================
        // 내부 도우미
        // ====================================================================

        /// <summary> 64비트 기대값 비교. 다르면 사유 문자열을 채운다. </summary>
        private static bool Check(ulong actual, ulong expected, string label, out string failureReason)
        {
            if (actual == expected)
            {
                failureReason = null;
                return true;
            }
            failureReason = label + " 기대 0x" + expected.ToString("X16") + " / 실제 0x" + actual.ToString("X16");
            return false;
        }

        /// <summary> 스트림에서 균등 정수를 count 개 뽑아 배열로 돌려준다(검증용). </summary>
        private static int[] TakeInts(MapRandom random, int count, int exclusiveMax)
        {
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = random.NextInt(exclusiveMax);
            }
            return result;
        }
    }
}
