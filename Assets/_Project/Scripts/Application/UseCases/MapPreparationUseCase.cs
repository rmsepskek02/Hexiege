// ============================================================================
// MapPreparationUseCase.cs
// 「맵 한 판을 준비하는 전체 흐름」을 담당하는 조정자(coordinator).
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 클래스가 하는 일 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   무작위 맵은 「뽑고 → 만들고 → 검사하고 → 안 되면 다시」의 반복이다.
//   그 순서를 한 자리에 모아 둔 것이 이 클래스다.
//
//     1. 64비트 root seed 를 인자로 받는다(직접 만들지 않는다).
//     2. MapSelection 스트림으로 경기당 한 번만 뽑는다.
//          MapType · 중립 광산 수 · 시작 광산 방향(A/B)
//          초기 골드는 뽑지 않고 규칙 3의 표에서 파생한다.
//     3. 시도 0 ~ 99 를 돌며 생성기 → 검증기를 실행한다. 통과하면 끝난다.
//     4. 100회 모두 실패하면 그 유형의 폴백 템플릿을 불러와 검증기를 다시 돌린다.
//        통과하면 그것을 쓰고, 그것마저 실패하면 「맵 준비 실패」다.
//     5. 결과(맵 + 규칙 12의 로그 항목들)를 값으로 돌려준다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 반드시 지켜야 하는 규칙 세 가지
// ─────────────────────────────────────────────────────────────────────────────
//   ① 실패한 시도를 이유로 2번(경기 선택)을 다시 뽑지 않는다.
//      특정 유형·광산 수·A/B 가 더 자주 실패하더라도 재추첨하면 「최초 선택 확률」이
//      왜곡된다(규칙 3 「경기 선택 단계」). 예를 들어 협곡형이 다른 유형보다 검증에
//      자주 걸린다고 해서 실패할 때마다 유형을 다시 뽑으면, 결과적으로 협곡형이
//      나올 확률이 1/5 보다 낮아진다. 재시도에서 바뀌는 것은 지형의 세부 형태와
//      중립 광산 위치뿐이다(그것들은 시도 번호로 파생되는 별도 스트림이 정한다).
//      장식은 최초 구현에서 항상 비어 있다(규칙 15).
//
//   ② 뽑는 순서가 곧 계약이다.
//      「같은 seed 면 같은 맵」이 규칙 12 의 약속이므로, MapSelection 스트림에서
//      무엇을 어떤 순서로 뽑는지가 사양 그 자체다. 순서는 아래 SelectForMatch 의
//      주석에 명시돼 있고, TryRunSelfCheck 의 고정 기대값으로 못 박아 두었다.
//      🔴 순서를 바꾸면 과거의 모든 seed 가 다른 맵이 된다 → MapVersion 을 올려야
//         하는지부터 판단할 것.
//
//   ③ 폴백은 값을 교체하지만 한 가지는 유지한다(규칙 12 · 규칙 3).
//          MapType          유지 (경기 선택 단계의 값)
//          중립 광산 수      템플릿 값으로 교체
//          시작 광산 방향    템플릿 값으로 교체
//          초기 골드        교체된 광산 수 기준으로 규칙 3 표에서 다시 정한다
//
//      🔴 2026-09-14 제거: 종전에는 「테스트 모드 표식 유지」 행이 하나 더 있었고,
//         테스트 모드에서 초기 골드를 5000 으로 덮어쓰기 때문에 해시를 반드시 다시
//         계산해야 한다는 경고가 붙어 있었다. 맵 테스트 모드가 규칙에서 삭제돼
//         (GameSystemRules_RandomMap.md 규칙 3 아래 2026-09-14 개정 블록) 덮어쓰기 자체가
//         사라졌다. 🔴 그래도 BuildSuccess 는 여전히 해시를 다시 계산한다 —
//         교체된 광산 수 때문에 InitialGold 가 달라질 수 있고, 그 값은 canonical
//         바이트에 들어가기 때문이다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 실행 모델 — 동기(synchronous) 함수 하나다
// ─────────────────────────────────────────────────────────────────────────────
//   TDD 의 「생성·검증 실행 모델」대로 초기 구현은 main thread synchronous 다.
//   profiling 근거 없이 비동기화하지 않는다.
//
//   🔴 부르는 쪽(Presentation/Bootstrap)의 책임:
//      로딩 UI 를 띄운 다음 반드시 「한 프레임 넘긴 뒤」에 Prepare 를 부른다.
//      (코루틴이라면 yield return null 을 한 번 넣는다.)
//      같은 프레임에 바로 부르면 로딩 UI 가 화면에 그려지기 전에 이 함수가
//      프레임을 통째로 붙잡아, 사용자에게는 그냥 멈춘 것처럼 보인다.
//      프레임을 넘기는 일 자체는 Unity 의 일이므로 이 클래스가 하지 않는다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 이 파일은 순수 C# 이다 — using UnityEngine 이 없다
// ─────────────────────────────────────────────────────────────────────────────
//   Application 레이어에 UnityEngine 을 쓰는 파일이 이미 있지만(Application/Combat/*),
//   이 조정자만은 일부러 순수하게 둔다. 이유는 실용적이다.
//     · 폴백 템플릿 읽기는 IMapFallbackTemplateSource 뒤에 숨겨 두었으므로
//       Unity 없이도 이 파일 전체를 컴파일하고 실행할 수 있다.
//     · 그래서 「100회 실패 → 폴백」 같은 좀처럼 안 걸리는 경로까지 실제로 돌려
//       확인할 수 있다.
//   로그도 직접 내보내지 않는다(GameLog 호출 0건). 로그 키 추가는 K 단계의 몫이라,
//   규칙 12 가 요구하는 로그 항목을 「값」으로 결과 객체에 담아 돌려주기만 한다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 3 · 12 · 15
//       TechnicalDesignDocument.md 「생성·검증 실행 모델」
//       _Tasks/2026-09-03/03_14_random-map-phase2-generator/Plan.md §4 G
//
// Application 레이어 — Domain 의존. Unity/Netcode/Infrastructure 직접 참조 없음.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 맵 준비가 실패했을 때의 내부 error code(규칙 12 로그 항목).
    /// 🔴 숫자 값은 로그에 그대로 남으므로 바꾸지 말 것. 새 사유는 뒤에 추가한다.
    /// </summary>
    public enum MapPreparationErrorCode
    {
        /// <summary> 실패 없음(맵 준비 성공). </summary>
        None = 0,

        /// <summary> 뽑힌 유형이 허용하는 중립 광산 개수 후보가 하나도 없다(생성기 정의 오류). </summary>
        SelectionMineCountUnavailable = 1,

        /// <summary> 규칙 3의 표가 그 입력에 대한 초기 골드를 정하지 못했다. </summary>
        InitialGoldUndefined = 2,

        /// <summary> 폴백 템플릿 바이트를 읽지 못했다(파일 없음·로드 실패). </summary>
        FallbackTemplateUnavailable = 3,

        /// <summary> 폴백 템플릿 바이트를 해석하지 못했다(형식 불일치·손상). </summary>
        FallbackTemplateUndecodable = 4,

        /// <summary> 폴백 템플릿을 만든 생성기를 다시 돌렸는데 거부됐다(제약을 복원할 수 없다). </summary>
        FallbackTemplateRejected = 5,

        /// <summary> 저장된 폴백 템플릿이 생성기가 다시 만든 결과와 다르다(템플릿이 낡았거나 손상됐다). </summary>
        FallbackTemplateMismatch = 6,

        /// <summary> 폴백 템플릿이 검증기를 통과하지 못했다. </summary>
        FallbackValidationFailed = 7
    }

    /// <summary>
    /// 맵 준비 결과. 완성된 맵과 함께 규칙 12가 요구하는 로그 항목을 「값」으로 담는다.
    ///
    /// 🔴 이 객체는 로그를 찍지 않는다. K 단계가 여기서 값을 꺼내 GameLog 로 내보낸다.
    ///    (3단계 범위인 「전송/재전송 횟수」·「Host/Client 해시 비교 결과」는 들어 있지 않다.)
    /// </summary>
    public sealed class MapPreparationResult
    {
        /// <summary> 맵 준비에 성공했으면 true. false 면 Definition 이 null 이다. </summary>
        public bool IsSucceeded { get; }

        /// <summary> 실패 사유의 내부 error code(성공이면 None). </summary>
        public MapPreparationErrorCode ErrorCode { get; }

        /// <summary> 사람이 읽는 실패 사유(성공이면 null). </summary>
        public string FailureReason { get; }

        /// <summary>
        /// 완성된 맵 정의(실패 시 null). Hash 필드는 이미 채워져 있다.
        ///
        /// ⚠️ 폴백을 썼을 때 Definition.RootSeed 는 「이 경기의 root seed」가 아니라
        ///    「템플릿을 만들 때 채택된 시드」다. 템플릿 바이트를 그대로 쓰는 것이
        ///    Host/Client 가 같은 맵을 얻는 근거이기 때문이다. 경기의 root seed 는
        ///    아래 RootSeed 프로퍼티에 따로 담겨 있다.
        /// </summary>
        public MapDefinition Definition { get; }

        /// <summary> 최종 맵의 canonical 바이트(실패 시 null). 3단계 전송이 이것을 쓴다. </summary>
        public byte[] CanonicalBytes { get; }

        /// <summary> 최종 맵 해시 32바이트(실패 시 null). </summary>
        public byte[] Hash { get; }

        /// <summary> 최종 맵 해시를 로그에 쓰기 좋은 16진 문자열로 바꾼 값(실패 시 null). </summary>
        public string HashHex { get; }

        // ── 규칙 12 로그 필수 항목 ──────────────────────────────────────────

        /// <summary> canonical 형식 버전. </summary>
        public int MapVersion { get; }

        /// <summary> 이 경기의 64비트 root seed(폴백이어도 이 값은 경기의 seed 다). </summary>
        public ulong RootSeed { get; }

        /// <summary> 최종 맵 유형. 폴백을 써도 경기 선택 단계의 값이 그대로 유지된다. </summary>
        public MapType MapType { get; }

        /// <summary> 최종 중립 광산 수. 폴백을 쓰면 템플릿 값으로 교체된다. </summary>
        public int NeutralMineCount { get; }

        /// <summary> 최종 시작 광산 방향. 폴백을 쓰면 템플릿 값으로 교체된다. </summary>
        public MapStartingMineSide StartingMineSide { get; }

        // 🔴 2026-09-14 제거: 여기에 bool MapTestModeEnabled 프로퍼티가 있었다.
        //    맵 테스트 모드가 규칙에서 삭제돼 실어 나를 값이 없어졌다.

        /// <summary> 실제 초기 골드. 폴백을 쓰면 교체된 광산 수 기준으로 다시 정해진다. </summary>
        public int InitialGold { get; }

        /// <summary>
        /// 생성 소요 시간(밀리초). 규칙 12가 정한 측정 구간은 하나뿐이다 —
        /// seed 를 확정한 직후부터 최종 맵이 확정된 순간(검증 통과 또는 폴백 확정)까지의
        /// 누적 실제 경과 시간. 모든 시도의 생성·검증 시간과 폴백 조립·검증 시간이 포함되고,
        /// 맵 전송·해시 비교·씬 로드는 빠진다.
        /// 🔴 시도별로 쪼갠 여러 값을 남기지 않는다 — 이 값 하나뿐이다.
        /// </summary>
        public long ElapsedMilliseconds { get; }

        /// <summary> 실제로 실행한 생성 시도 횟수(1 이상. 폴백까지 갔으면 최대 시도 횟수와 같다). </summary>
        public int AttemptCount { get; }

        /// <summary> 폴백 템플릿을 사용했으면 true. </summary>
        public bool UsedFallback { get; }

        /// <summary>
        /// 결과 객체를 직접 만든다. 보통은 Success / Failure 정적 메서드를 쓴다.
        /// </summary>
        /// <param name="isSucceeded">성공 여부</param>
        /// <param name="errorCode">내부 error code</param>
        /// <param name="failureReason">사람이 읽는 실패 사유</param>
        /// <param name="definition">완성된 맵 정의</param>
        /// <param name="canonicalBytes">canonical 바이트</param>
        /// <param name="hash">최종 맵 해시</param>
        /// <param name="mapVersion">canonical 형식 버전</param>
        /// <param name="rootSeed">경기의 root seed</param>
        /// <param name="mapType">맵 유형</param>
        /// <param name="neutralMineCount">중립 광산 수</param>
        /// <param name="startingMineSide">시작 광산 방향</param>
        /// <param name="initialGold">실제 초기 골드</param>
        /// <param name="elapsedMilliseconds">생성 소요 시간(ms)</param>
        /// <param name="attemptCount">실행한 시도 횟수</param>
        /// <param name="usedFallback">폴백 사용 여부</param>
        public MapPreparationResult(bool isSucceeded, MapPreparationErrorCode errorCode,
            string failureReason, MapDefinition definition, byte[] canonicalBytes, byte[] hash,
            int mapVersion, ulong rootSeed, MapType mapType, int neutralMineCount,
            MapStartingMineSide startingMineSide, int initialGold,
            long elapsedMilliseconds, int attemptCount, bool usedFallback)
        {
            IsSucceeded = isSucceeded;
            ErrorCode = errorCode;
            FailureReason = failureReason;
            Definition = definition;
            CanonicalBytes = canonicalBytes;
            Hash = hash;
            HashHex = hash == null ? null : MapFallbackTemplateFactory.ToHex(hash);
            MapVersion = mapVersion;
            RootSeed = rootSeed;
            MapType = mapType;
            NeutralMineCount = neutralMineCount;
            StartingMineSide = startingMineSide;
            InitialGold = initialGold;
            ElapsedMilliseconds = elapsedMilliseconds;
            AttemptCount = attemptCount;
            UsedFallback = usedFallback;
        }

        /// <summary>
        /// 로그 한 줄로 요약한다(규칙 12 항목 순서대로).
        /// </summary>
        /// <returns>사람이 읽는 요약 문자열</returns>
        public override string ToString()
        {
            return "MapPreparation " + (IsSucceeded ? "성공" : "실패") +
                " ver=" + MapVersion +
                " seed=" + RootSeed +
                " type=" + MapType +
                " mines=" + NeutralMineCount +
                " side=" + StartingMineSide +
                " gold=" + InitialGold +
                " elapsedMs=" + ElapsedMilliseconds +
                " attempts=" + AttemptCount +
                " fallback=" + (UsedFallback ? 1 : 0) +
                " hash=" + (HashHex ?? "-") +
                " error=" + ErrorCode +
                (FailureReason == null ? "" : " reason=" + FailureReason);
        }
    }

    /// <summary>
    /// 맵 한 판을 준비하는 조정자. 싱글플레이에서는 로컬 GameConfig 가 권위이며,
    /// 멀티 Host 권위는 3단계 범위라 이 클래스가 다루지 않는다(규칙 3).
    /// </summary>
    public sealed class MapPreparationUseCase
    {
        private readonly IMapFallbackTemplateSource _fallbackTemplateSource;

        /// <summary>
        /// 조정자를 만든다.
        /// </summary>
        /// <param name="fallbackTemplateSource">폴백 템플릿 바이트를 읽어 오는 구현체(필수)</param>
        public MapPreparationUseCase(IMapFallbackTemplateSource fallbackTemplateSource)
        {
            // 🔴 null 을 허용하지 않는 이유: 폴백 소스가 없다는 것은 「100회 실패했을 때만
            //    조용히 터지는」 가장 찾기 어려운 형태의 구멍이 된다. 조합 시점에 드러내는 편이 낫다.
            _fallbackTemplateSource = fallbackTemplateSource ??
                throw new ArgumentNullException(nameof(fallbackTemplateSource));
        }

        // ====================================================================
        // 진입점
        // ====================================================================

        /// <summary>
        /// 맵 한 판을 준비한다. 동기 함수이며 실패는 예외가 아니라 결과 값으로 돌아온다.
        ///
        /// 🔴 부르기 전에 반드시 한 프레임을 넘길 것.
        ///    이 함수는 최악의 경우 100번의 생성·검증을 한 프레임 안에서 전부 돌린다.
        ///    로딩 UI 를 띄운 그 프레임에 바로 부르면 UI 가 그려지기 전에 화면이 멈춘다.
        ///    (코루틴에서 yield return null 을 한 번 넣은 뒤 부르면 된다.)
        ///
        /// 🔴 root seed 를 이 함수가 만들지 않는 이유:
        ///    어디서 오는지는 부르는 쪽의 몫이다(싱글 = 로컬, 멀티 = Host, 3단계).
        ///    인자로 받아야 결정성이 지켜지고 같은 seed 로 다시 돌려 볼 수 있다.
        /// </summary>
        /// <param name="rootSeed">이 경기의 64비트 root seed</param>
        /// <returns>완성된 맵과 규칙 12 로그 항목을 담은 결과</returns>
        // 🔴 2026-09-14 시그니처 축소: 종전에는 Prepare(ulong rootSeed, bool mapTestModeEnabled) 였다.
        //    두 번째 인자는 GameConfig.MapTestModeEnabled 를 그대로 받아 「초기 골드를 5000 으로
        //    고정하는 갈래」를 켜는 스위치였는데, 그 모드가 규칙에서 삭제돼
        //    (GameSystemRules_RandomMap.md 규칙 3 아래 2026-09-14 개정 블록) 인자를 없앴다.
        public MapPreparationResult Prepare(ulong rootSeed)
        {
            // ── 시간 측정 시작 ──────────────────────────────────────────────
            // 규칙 12: 「seed 를 확정한 직후부터」가 측정 구간의 시작이다. root seed 는
            // 인자로 이미 확정돼 들어오므로 이 함수의 첫 줄이 곧 그 시점이다.
            var stopwatch = Stopwatch.StartNew();

            int mapVersion = MapDefinition.CurrentMapVersion;

            // ── 2. 경기 선택 단계 (경기당 한 번) ────────────────────────────
            MapSelection selection;
            if (!TrySelectForMatch(mapVersion, rootSeed, out selection,
                    out MapPreparationErrorCode selectionError, out string selectionReason))
            {
                stopwatch.Stop();
                return BuildFailure(selectionError, selectionReason, mapVersion, rootSeed,
                    selection, stopwatch.ElapsedMilliseconds, 0, false);
            }

            // ── 3. 시도 0 ~ (MaxAttemptCount-1) ─────────────────────────────
            // 🔴 이 구간에서 selection 은 절대 바뀌지 않는다(위 ① 규칙).
            //    바뀌는 것은 시도 번호뿐이고, 시도 번호가 Terrain/MinePlacement 스트림의
            //    seed 를 바꿔 지형 세부 형태와 중립 광산 위치를 다르게 만든다.
            IMapArchetypeGenerator generator = MapFallbackTemplateFactory.CreateGenerator(selection.MapType);

            int attemptCount = 0;
            for (int attemptIndex = 0; attemptIndex < MapRandomStreams.MaxAttemptCount; attemptIndex++)
            {
                attemptCount++;

                MapGenerationResult generated = generator.Generate(BuildRequest(mapVersion, rootSeed,
                    attemptIndex, selection));

                // 「거부」는 오류가 아니다. 시도 번호를 올려 다시 만든다(규칙 6).
                if (!generated.IsAccepted) continue;

                MapValidationResult validation = MapDefinitionValidator.Validate(
                    generated.Definition, generated.Constraints, generator);

                if (!validation.IsPassed) continue;

                // 최종 맵이 확정된 순간이다 — 여기서 시간 측정을 멈춘다.
                // (해시 계산은 「맵이 확정된 뒤」의 일이라 측정 구간에 넣지 않는다.)
                stopwatch.Stop();

                return BuildSuccess(generated.Definition, mapVersion, rootSeed, selection,
                    stopwatch.ElapsedMilliseconds, attemptCount, false);
            }

            // ── 4. 100회 모두 실패 → 폴백 템플릿 ────────────────────────────
            MapDefinition fallbackDefinition;
            MapSelection fallbackSelection;
            if (!TryPrepareFromFallback(selection, out fallbackDefinition, out fallbackSelection,
                    out MapPreparationErrorCode fallbackError, out string fallbackReason))
            {
                stopwatch.Stop();
                return BuildFailure(fallbackError, fallbackReason, mapVersion, rootSeed,
                    selection, stopwatch.ElapsedMilliseconds, attemptCount, true);
            }

            // 폴백이 확정된 순간이다 — 여기서 시간 측정을 멈춘다.
            stopwatch.Stop();

            return BuildSuccess(fallbackDefinition, mapVersion, rootSeed, fallbackSelection,
                stopwatch.ElapsedMilliseconds, attemptCount, true);
        }

        // ====================================================================
        // 2. 경기 선택 단계
        // ====================================================================

        /// <summary>
        /// 경기 선택 단계에서 확정되는 값 묶음. 재시도 구간에서 절대 바뀌지 않는다.
        /// (폴백 경로에서만 광산 수·시작 광산 방향·초기 골드가 템플릿 값으로 교체된다.)
        /// </summary>
        private struct MapSelection
        {
            /// <summary> 맵 유형. </summary>
            public MapType MapType;

            /// <summary> 중립 광산 개수. </summary>
            public int NeutralMineCount;

            /// <summary> 시작 광산 배치 경우(A/B). </summary>
            public MapStartingMineSide StartingMineSide;

            // 🔴 2026-09-14 제거: 여기에 int TestModeFlag 가 있었다. 모드가 사라져 뽑을 값이 없다.

            /// <summary> 실제 초기 골드. </summary>
            public int InitialGold;
        }

        // ────────────────────────────────────────────────────────────────────
        // 🔴 MapSelection 스트림에서 뽑는 순서 — 이것이 사양이다
        //
        //    ① MapType           : 후보 5종에서 동일 확률로 1개   (NextInt(5))
        //    ② NeutralMineCount  : 그 유형이 허용하는 개수 후보에서 동일 확률로 1개 (Choose)
        //    ③ StartingMineSide  : A/B 를 50:50 으로              (NextBool)
        //    ④ InitialGold       : 뽑지 않는다 — 규칙 3의 표에서 파생한다
        //
        //    ①이 반드시 먼저인 이유: ②의 후보 목록이 유형마다 다르기 때문이다
        //    (협곡형 1~4, 외곽형 2~6, 나머지 1~6). 유형을 모르면 후보를 만들 수 없다.
        //
        //    ④를 뽑지 않는 이유: 규칙 3이 「광산 수 → 골드」 표를 고정해 두었기 때문이다.
        //    난수를 한 번 더 뽑으면 그것만으로 이후 모든 값이 밀려 과거 seed 가 깨진다.
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 경기 선택 단계를 실행한다. 경기당 한 번만 불리며, 재시도 때 다시 부르지 않는다.
        /// </summary>
        /// <param name="mapVersion">canonical 형식 버전</param>
        /// <param name="rootSeed">경기의 root seed</param>
        /// <param name="selection">뽑힌 값 묶음(실패해도 뽑은 데까지는 채워진다)</param>
        /// <param name="errorCode">실패 시 내부 error code</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>선택이 확정되면 true</returns>
        private static bool TrySelectForMatch(int mapVersion, ulong rootSeed,
            out MapSelection selection, out MapPreparationErrorCode errorCode, out string failureReason)
        {
            selection = default(MapSelection);

            MapRandom stream = MapRandomStreams.CreateMatchStream(
                mapVersion, rootSeed, MapRandomStreams.MapSelection);

            // ── ① MapType ───────────────────────────────────────────────────
            // 후보 목록을 여기서 새로 적지 않고 MapFallbackTemplateFactory.TemplateMapTypes 를 쓴다.
            // 그 목록은 「폴백 템플릿이 있는 유형 전부」이므로, 이것으로 뽑으면
            // 「뽑혔는데 폴백 템플릿이 없는 유형」이 구조적으로 생길 수 없다.
            IReadOnlyList<MapType> mapTypes = MapFallbackTemplateFactory.TemplateMapTypes;
            selection.MapType = mapTypes[stream.NextInt(mapTypes.Count)];

            // ── ② NeutralMineCount ──────────────────────────────────────────
            // 허용 범위는 유형마다 다르므로 숫자를 적지 않고 생성기에게 묻는다.
            IMapArchetypeGenerator generator = MapFallbackTemplateFactory.CreateGenerator(selection.MapType);
            List<int> allowedCounts = CollectAllowedMineCounts(generator);

            if (allowedCounts.Count == 0)
            {
                errorCode = MapPreparationErrorCode.SelectionMineCountUnavailable;
                failureReason = selection.MapType + ": 허용하는 중립 광산 개수가 하나도 없다(허용 " +
                    generator.MinNeutralMineCount + "~" + generator.MaxNeutralMineCount + ").";
                return false;
            }

            selection.NeutralMineCount = stream.Choose(allowedCounts);

            // ── ③ StartingMineSide ──────────────────────────────────────────
            // NextBool 은 최상위 비트를 쓰는 정확한 50:50 이다.
            selection.StartingMineSide = stream.NextBool()
                ? MapStartingMineSide.CaseB
                : MapStartingMineSide.CaseA;

            // ── ④ InitialGold (파생, 뽑지 않는다) ───────────────────────────
            selection.InitialGold = MapDefinitionValidator.GetExpectedInitialGold(
                selection.NeutralMineCount);

            if (selection.InitialGold < 0)
            {
                errorCode = MapPreparationErrorCode.InitialGoldUndefined;
                failureReason = "규칙 3의 표가 초기 골드를 정하지 못했다(중립 광산 수 " +
                    selection.NeutralMineCount + ").";
                return false;
            }

            errorCode = MapPreparationErrorCode.None;
            failureReason = null;
            return true;
        }

        /// <summary>
        /// 그 생성기가 허용하는 중립 광산 개수를 오름차순 목록으로 모은다.
        /// 최소~최대 사이가 아닌 개수만 허용하는 유형이 나와도 이 코드는 그대로 맞는다
        /// (IsNeutralMineCountAllowed 로 하나씩 물어보기 때문).
        /// </summary>
        /// <param name="generator">맵 유형별 생성기</param>
        /// <returns>허용되는 개수 목록(오름차순)</returns>
        private static List<int> CollectAllowedMineCounts(IMapArchetypeGenerator generator)
        {
            var allowed = new List<int>();
            for (int count = generator.MinNeutralMineCount; count <= generator.MaxNeutralMineCount; count++)
            {
                if (generator.IsNeutralMineCountAllowed(count)) allowed.Add(count);
            }
            return allowed;
        }

        /// <summary>
        /// 생성기에 넘길 입력값 묶음을 만든다.
        /// </summary>
        /// <param name="mapVersion">canonical 형식 버전</param>
        /// <param name="rootSeed">경기의 root seed</param>
        /// <param name="attemptIndex">시도 번호</param>
        /// <param name="selection">경기 선택 단계의 값</param>
        /// <returns>생성 요청</returns>
        private static MapGenerationRequest BuildRequest(int mapVersion, ulong rootSeed,
            int attemptIndex, MapSelection selection)
        {
            return new MapGenerationRequest
            {
                MapVersion = mapVersion,
                RootSeed = rootSeed,
                AttemptIndex = attemptIndex,
                StartingMineSide = selection.StartingMineSide,
                NeutralMineCount = selection.NeutralMineCount,
                InitialGold = selection.InitialGold
            };
        }

        // ====================================================================
        // 4. 폴백 템플릿
        // ====================================================================

        /// <summary>
        /// 그 유형의 폴백 템플릿을 불러와 최종 맵으로 조립한다.
        ///
        /// 흐름은 다음과 같다.
        ///   1) 템플릿 canonical 바이트를 읽는다(IMapFallbackTemplateSource).
        ///   2) 바이트를 맵 정의로 해석한다(Decode).
        ///   3) 같은 생성기를 같은 입력으로 다시 돌려 「유형별 제약」을 되살린다.
        ///      🔴 제약은 바이트에 들어 있지 않아 되살릴 방법이 이것뿐이다. 검증기는
        ///         제약을 필수 인자로 받으므로 이 단계를 건너뛸 수 없다.
        ///      🔴 겸사겸사 저장된 바이트와 다시 만든 맵이 「완전히 같은지」 대조한다.
        ///         템플릿 파일이 낡거나 손상됐을 때 조용히 다른 맵이 쓰이는 것을 막는다.
        ///   4) 규칙 12대로 값을 교체한다 — 유형은 유지, 광산 수·시작 광산 방향은 템플릿 값,
        ///      초기 골드는 교체된 광산 수 기준으로 다시 결정.
        ///      🔴 2026-09-14 까지는 「테스트 모드 표식도 유지」가 여기 함께 적혀 있었다.
        ///         맵 테스트 모드가 규칙에서 삭제돼 유지할 표식이 없어졌다.
        ///   5) 완화 없이 검증기 전체를 다시 돌린다. 통과해야만 쓴다.
        /// </summary>
        /// <param name="selection">경기 선택 단계의 값(유형만 쓴다)</param>
        /// <param name="definition">완성된 폴백 맵 정의(실패 시 null)</param>
        /// <param name="finalSelection">교체까지 끝난 최종 선택값(실패 시 입력값 그대로)</param>
        /// <param name="errorCode">실패 시 내부 error code</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>폴백 맵을 쓸 수 있으면 true</returns>
        private bool TryPrepareFromFallback(MapSelection selection, out MapDefinition definition,
            out MapSelection finalSelection, out MapPreparationErrorCode errorCode,
            out string failureReason)
        {
            definition = null;
            finalSelection = selection;

            // ── 1) 템플릿 바이트 읽기 ───────────────────────────────────────
            byte[] canonicalBytes;
            if (!_fallbackTemplateSource.TryLoadCanonicalBytes(selection.MapType, out canonicalBytes,
                    out string loadReason) || canonicalBytes == null)
            {
                errorCode = MapPreparationErrorCode.FallbackTemplateUnavailable;
                failureReason = selection.MapType + " 폴백 템플릿을 읽지 못했다: " + (loadReason ?? "사유 없음");
                return false;
            }

            // ── 2) 해석 ─────────────────────────────────────────────────────
            MapDefinition loaded = MapDefinitionCodec.Decode(canonicalBytes);
            if (loaded == null)
            {
                errorCode = MapPreparationErrorCode.FallbackTemplateUndecodable;
                failureReason = selection.MapType + " 폴백 템플릿 바이트를 해석하지 못했다(길이 " +
                    canonicalBytes.Length + ").";
                return false;
            }

            if (loaded.MapType != selection.MapType)
            {
                errorCode = MapPreparationErrorCode.FallbackTemplateMismatch;
                failureReason = "폴백 템플릿의 맵 유형이 다르다(요청 " + selection.MapType +
                    " / 파일 " + loaded.MapType + ").";
                return false;
            }

            // ── 3) 제약 복원 + 바이트 대조 ──────────────────────────────────
            IMapArchetypeGenerator generator = MapFallbackTemplateFactory.CreateGenerator(selection.MapType);

            // 생성기는 허용하지 않는 광산 개수를 받으면 예외를 던진다. 손상된 파일 때문에
            // 예외가 나는 일이 없도록 먼저 물어본다.
            if (!generator.IsNeutralMineCountAllowed(loaded.NeutralMineCount))
            {
                errorCode = MapPreparationErrorCode.FallbackTemplateMismatch;
                failureReason = "폴백 템플릿의 중립 광산 수 " + loaded.NeutralMineCount +
                    " 를 " + selection.MapType + " 생성기가 허용하지 않는다.";
                return false;
            }

            int templateGold = MapDefinitionValidator.GetExpectedInitialGold(loaded.NeutralMineCount);

            if (templateGold < 0)
            {
                errorCode = MapPreparationErrorCode.InitialGoldUndefined;
                failureReason = "폴백 템플릿의 초기 골드를 규칙 3의 표에서 찾지 못했다(중립 광산 수 " +
                    loaded.NeutralMineCount + ").";
                return false;
            }

            var rebuildRequest = new MapGenerationRequest
            {
                MapVersion = loaded.MapVersion,
                RootSeed = loaded.RootSeed,
                AttemptIndex = MapFallbackTemplateFactory.TemplateAttemptIndex,
                StartingMineSide = MapFallbackTemplateFactory.TemplateStartingMineSide,
                NeutralMineCount = loaded.NeutralMineCount,
                InitialGold = templateGold
            };

            MapGenerationResult rebuilt = generator.Generate(rebuildRequest);
            if (!rebuilt.IsAccepted)
            {
                errorCode = MapPreparationErrorCode.FallbackTemplateRejected;
                failureReason = selection.MapType +
                    " 폴백 템플릿을 만든 생성기를 다시 돌렸는데 거부됐다(제약을 복원할 수 없다): " +
                    rebuilt.RejectionReason;
                return false;
            }

            // 목록 순서를 canonical 정렬로 맞춘 뒤 비교한다. Decode 결과는 저장된
            // (이미 정렬된) 순서 그대로이므로, 다시 만든 쪽만 맞춰 주면 된다.
            rebuilt.Definition.SortCanonical();

            if (!MapFallbackTemplateFactory.TryCompareDefinitionsExactly(loaded, rebuilt.Definition,
                    out string diffReason))
            {
                errorCode = MapPreparationErrorCode.FallbackTemplateMismatch;
                failureReason = selection.MapType +
                    " 폴백 템플릿 파일이 생성기가 다시 만든 결과와 다르다(파일이 낡았거나 손상됐다): " +
                    diffReason;
                return false;
            }

            // ── 4) 값 교체 (규칙 12) ────────────────────────────────────────
            //   유지 : MapType(이미 같다)
            //   교체 : 중립 광산 수 · 시작 광산 방향 (템플릿 값)
            //   재결정 : 초기 골드
            //   🔴 2026-09-14 까지는 「테스트 모드 표식」도 유지 대상이었다. 모드가 삭제돼 빠졌다.
            finalSelection.MapType = selection.MapType;
            finalSelection.NeutralMineCount = loaded.NeutralMineCount;
            finalSelection.StartingMineSide = MapFallbackTemplateFactory.TemplateStartingMineSide;

            finalSelection.InitialGold = MapDefinitionValidator.GetExpectedInitialGold(
                loaded.NeutralMineCount);

            if (finalSelection.InitialGold < 0)
            {
                errorCode = MapPreparationErrorCode.InitialGoldUndefined;
                failureReason = "폴백에서 초기 골드를 다시 정하지 못했다(중립 광산 수 " +
                    loaded.NeutralMineCount + ").";
                return false;
            }

            // 🔴 여기서 정의의 값도 함께 바꾼다. 바꾸지 않으면 「검증한 맵」과
            //    「실제로 쓰는 값」이 갈린다. 그리고 이 필드는 canonical 바이트에
            //    들어가므로, 바꾼 뒤에는 해시를 반드시 다시 계산해야 한다
            //    (BuildSuccess 가 항상 다시 계산한다).
            //    🔴 2026-09-14 까지는 바로 위에 loaded.TestModeFlag 를 심는 줄이 하나 더 있었다.
            loaded.InitialGold = finalSelection.InitialGold;

            // ── 5) 완화 없이 검증기 전체 ────────────────────────────────────
            MapValidationResult validation = MapDefinitionValidator.Validate(
                loaded, rebuilt.Constraints, generator);

            if (!validation.IsPassed)
            {
                errorCode = MapPreparationErrorCode.FallbackValidationFailed;
                failureReason = selection.MapType + " 폴백 템플릿이 검증을 통과하지 못했다: " + validation;
                return false;
            }

            definition = loaded;
            errorCode = MapPreparationErrorCode.None;
            failureReason = null;
            return true;
        }

        // ====================================================================
        // 결과 만들기
        // ====================================================================

        /// <summary>
        /// 성공 결과를 만든다. canonical 바이트와 해시를 여기서 계산해 정의에 채운다.
        ///
        /// 🔴 해시는 언제나 여기서 다시 계산한다. InitialGold 가 canonical 바이트에
        ///    포함되는데 폴백 경로에서 그 값이 교체될 수 있기 때문이다.
        ///    (2026-09-14 까지는 「폴백 + 테스트 모드 조합」이 그 대표 사례로 적혀 있었다.
        ///     맵 테스트 모드가 규칙에서 삭제돼 그 사례는 사라졌지만, 다시 계산해야 하는
        ///     이유 자체는 그대로 남는다.)
        /// </summary>
        /// <param name="definition">완성된 맵 정의</param>
        /// <param name="mapVersion">canonical 형식 버전</param>
        /// <param name="rootSeed">경기의 root seed</param>
        /// <param name="selection">최종 선택값</param>
        /// <param name="elapsedMilliseconds">생성 소요 시간(ms)</param>
        /// <param name="attemptCount">실행한 시도 횟수</param>
        /// <param name="usedFallback">폴백 사용 여부</param>
        /// <returns>성공 결과</returns>
        private static MapPreparationResult BuildSuccess(MapDefinition definition, int mapVersion,
            ulong rootSeed, MapSelection selection,
            long elapsedMilliseconds, int attemptCount, bool usedFallback)
        {
            definition.SortCanonical();

            byte[] canonicalBytes = MapDefinitionCodec.Encode(definition);
            byte[] hash = MapDefinitionCodec.ComputeHash(canonicalBytes);
            definition.Hash = hash;

            return new MapPreparationResult(true, MapPreparationErrorCode.None, null,
                definition, canonicalBytes, hash, mapVersion, rootSeed, selection.MapType,
                selection.NeutralMineCount, selection.StartingMineSide,
                selection.InitialGold, elapsedMilliseconds, attemptCount, usedFallback);
        }

        /// <summary>
        /// 실패 결과를 만든다. 맵은 없지만 규칙 12의 로그 항목은 아는 데까지 채워 둔다.
        /// </summary>
        /// <param name="errorCode">내부 error code</param>
        /// <param name="failureReason">사람이 읽는 실패 사유</param>
        /// <param name="mapVersion">canonical 형식 버전</param>
        /// <param name="rootSeed">경기의 root seed</param>
        /// <param name="selection">아는 데까지 채워진 선택값</param>
        /// <param name="elapsedMilliseconds">생성 소요 시간(ms)</param>
        /// <param name="attemptCount">실행한 시도 횟수</param>
        /// <param name="usedFallback">폴백까지 갔으면 true</param>
        /// <returns>실패 결과</returns>
        private static MapPreparationResult BuildFailure(MapPreparationErrorCode errorCode,
            string failureReason, int mapVersion, ulong rootSeed, MapSelection selection,
            long elapsedMilliseconds, int attemptCount, bool usedFallback)
        {
            return new MapPreparationResult(false, errorCode, failureReason, null, null, null,
                mapVersion, rootSeed, selection.MapType, selection.NeutralMineCount,
                selection.StartingMineSide, selection.InitialGold,
                elapsedMilliseconds, attemptCount, usedFallback);
        }

        // ====================================================================
        // 자기 검증 — 이 프로젝트에는 유닛 테스트 어셈블리가 없다
        //
        // 🔴 여기서 「뽑는 순서」를 고정 기대값으로 못 박는다. 순서를 바꾸면 이 검증이
        //    깨지므로, "테스트가 틀렸다"가 아니라 "사양이 바뀌었다"로 읽어야 한다.
        //    값을 고치기 전에 MapVersion 을 올려야 하는지부터 판단할 것.
        //
        // 🔴 폴백 경로도 여기서 실제로 태운다. 폴백은 100회 실패해야만 닿는 자리라
        //    보통 실행에서는 한 번도 돌지 않는다. 그래서 조립 함수를 직접 부른다.
        // ====================================================================

        /// <summary> 자기 검증에 쓰는 고정 root seed. </summary>
        public const ulong SelfCheckRootSeed = 20260907UL;

        /// <summary>
        /// 자기 검증을 실행한다. 실패하면 false 를 돌려주고 사유를 채운다.
        /// (템플릿은 파일이 아니라 Domain 의 팩터리로 메모리에서 만들어 쓰므로
        ///  Unity 없이도 그대로 실행된다.)
        /// </summary>
        /// <param name="failureReason">실패 사유. 성공하면 null</param>
        /// <returns>모든 검증을 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            var templateSource = new InMemoryFallbackTemplateSource();
            if (!templateSource.TryBuildAll(out failureReason)) return false;

            var useCase = new MapPreparationUseCase(templateSource);

            // ── 1. 뽑는 순서 고정 ───────────────────────────────────────────
            // 같은 스트림에서 ①유형 ②광산 수 ③A/B 순서로 뽑는다는 사양을 값으로 고정한다.
            if (!TrySelectForMatch(MapDefinition.CurrentMapVersion, SelfCheckRootSeed,
                    out MapSelection selection,
                    out MapPreparationErrorCode selectionError, out string selectionReason))
            {
                failureReason = "자기 검증 seed 로 경기 선택이 실패했다(" + selectionError + "): " + selectionReason;
                return false;
            }

            if (selection.MapType != SelfCheckExpectedMapType)
            {
                failureReason = "선택 순서가 바뀌었다 — MapType 기대 " + SelfCheckExpectedMapType +
                    " / 실제 " + selection.MapType;
                return false;
            }

            if (selection.NeutralMineCount != SelfCheckExpectedMineCount)
            {
                failureReason = "선택 순서가 바뀌었다 — 중립 광산 수 기대 " + SelfCheckExpectedMineCount +
                    " / 실제 " + selection.NeutralMineCount;
                return false;
            }

            if (selection.StartingMineSide != SelfCheckExpectedSide)
            {
                failureReason = "선택 순서가 바뀌었다 — 시작 광산 방향 기대 " + SelfCheckExpectedSide +
                    " / 실제 " + selection.StartingMineSide;
                return false;
            }

            // 초기 골드는 뽑지 않고 표에서 파생된다.
            int expectedGold = MapDefinitionValidator.GetExpectedInitialGold(
                selection.NeutralMineCount);
            if (selection.InitialGold != expectedGold)
            {
                failureReason = "초기 골드가 규칙 3의 표에서 파생되지 않았다(기대 " + expectedGold +
                    " / 실제 " + selection.InitialGold + ").";
                return false;
            }

            // ── 2. 결정성 — 같은 seed 로 두 번 돌리면 모든 것이 같아야 한다 ──
            MapPreparationResult first = useCase.Prepare(SelfCheckRootSeed);
            MapPreparationResult second = useCase.Prepare(SelfCheckRootSeed);

            if (!first.IsSucceeded)
            {
                failureReason = "자기 검증 seed 로 맵 준비가 실패했다: " + first.FailureReason;
                return false;
            }

            if (!TryCompareResults(first, second, out failureReason)) return false;

            // 선택 단계를 따로 부른 결과와 전체 흐름의 결과가 같은지도 본다.
            if (first.MapType != selection.MapType ||
                first.NeutralMineCount != selection.NeutralMineCount ||
                first.StartingMineSide != selection.StartingMineSide)
            {
                failureReason = "선택 단계 결과와 전체 흐름 결과가 다르다.";
                return false;
            }

            // ── 3. 선택값이 유형의 허용 범위 안인지 (여러 seed) ─────────────
            for (ulong seed = 0UL; seed < 200UL; seed++)
            {
                if (!TrySelectForMatch(MapDefinition.CurrentMapVersion, seed,
                        out MapSelection sample,
                        out MapPreparationErrorCode sampleError, out string sampleReason))
                {
                    failureReason = "seed " + seed + " 선택 실패(" + sampleError + "): " + sampleReason;
                    return false;
                }

                IMapArchetypeGenerator sampleGenerator =
                    MapFallbackTemplateFactory.CreateGenerator(sample.MapType);

                if (!sampleGenerator.IsNeutralMineCountAllowed(sample.NeutralMineCount))
                {
                    failureReason = "seed " + seed + ": 뽑힌 중립 광산 수 " + sample.NeutralMineCount +
                        " 를 " + sample.MapType + " 가 허용하지 않는다.";
                    return false;
                }
            }

            // ── 4. 폴백 경로 ────────────────────────────────────────────────
            // 100회 실패해야만 닿는 자리라 조립 함수를 직접 부른다.
            for (int i = 0; i < MapFallbackTemplateFactory.TemplateMapTypes.Count; i++)
            {
                MapType mapType = MapFallbackTemplateFactory.TemplateMapTypes[i];

                var normalSelection = new MapSelection
                {
                    MapType = mapType,
                    NeutralMineCount = 0,
                    StartingMineSide = MapStartingMineSide.CaseB,
                    InitialGold = -1
                };

                if (!useCase.TryPrepareFromFallback(normalSelection, out MapDefinition normalDefinition,
                        out MapSelection normalFinal, out MapPreparationErrorCode normalError,
                        out string normalReason))
                {
                    failureReason = mapType + " 정상 모드 폴백 조립 실패(" + normalError + "): " + normalReason;
                    return false;
                }

                // 유형은 유지되고, 광산 수·A/B 는 템플릿 값으로 교체돼야 한다.
                if (normalFinal.MapType != mapType)
                {
                    failureReason = mapType + " 폴백이 맵 유형을 바꿨다: " + normalFinal.MapType;
                    return false;
                }

                if (normalFinal.StartingMineSide != MapFallbackTemplateFactory.TemplateStartingMineSide)
                {
                    failureReason = mapType + " 폴백이 시작 광산 방향을 템플릿 값으로 교체하지 않았다: " +
                        normalFinal.StartingMineSide;
                    return false;
                }

                // 폴백은 템플릿의 값을 그대로 쓰므로 바이트가 템플릿과 같아야 한다.
                byte[] rebuiltBytes = MapDefinitionCodec.Encode(normalDefinition);
                if (!templateSource.MatchesStoredBytes(mapType, rebuiltBytes))
                {
                    failureReason = mapType + " 폴백의 canonical 바이트가 템플릿과 다르다.";
                    return false;
                }

                // 🔴 2026-09-14 제거: 여기에 「5. 폴백 + 테스트 모드」 덩어리가 있었다.
                //    테스트 모드로 폴백을 태우면 초기 골드가 5000 으로 덮어써지고
                //    그 결과 canonical 바이트와 해시가 템플릿과 달라져야 한다는 검사였다.
                //    맵 테스트 모드가 규칙에서 삭제돼(규칙 3 아래 2026-09-14 개정 블록)
                //    덮어쓰는 갈래 자체가 없어졌으므로 확인할 대상이 사라졌다.
                //    🔴 그 대신 바로 위 「템플릿과 바이트가 같은가」 검사는 그대로 남아 있어,
                //       폴백 경로가 실제로 돌아간다는 양성 대조는 계속 유지된다.
            }

            failureReason = null;
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // 🔴 아래 세 기대값은 「사양을 값으로 못 박은 것」이다 — 깨지면 코드가 아니라
        //    사양이 바뀐 것이다(위 자기 검증 머리말 참조).
        //
        // 🔴 2026-09-14 갱신 — 원래 값과 갱신 사유를 남긴다.
        //      MapType  : FullyOpen  (바뀌지 않았다)
        //      광산 수   : 2 → **4**
        //      시작 광산 : CaseB → **CaseA**
        //
        //    왜 바뀌었는가(추정이 아니라 실행해서 확인한 값이다):
        //      경기 선택 단계의 난수 스트림은 MapRandomStreams.CreateMatchStream(mapVersion, ...)
        //      처럼 **mapVersion 을 seed 재료로 쓴다.** 맵 테스트 모드 삭제로 canonical 형식이
        //      바뀌어 MapDefinition.CurrentMapVersion 을 1 → 2 로 올렸으므로, 같은 root seed 라도
        //      스트림이 달라지고 뽑히는 값도 달라진다.
        //    🔴 즉 이것은 「뽑는 순서가 바뀐 것」이 아니라 「형식 버전이 바뀐 것」의 결과다.
        //       순서(①유형 ②광산 수 ③A/B)는 한 글자도 바뀌지 않았고, 그 순서가 그대로임은
        //       세 값이 모두 각 유형의 허용 범위 안이라는 점과 결정성 검사로 계속 지켜진다.
        // ────────────────────────────────────────────────────────────────────

        /// <summary> 자기 검증 seed 로 뽑혀야 하는 맵 유형(순서 고정용 기대값). </summary>
        private const MapType SelfCheckExpectedMapType = MapType.FullyOpen;

        /// <summary> 자기 검증 seed 로 뽑혀야 하는 중립 광산 수(순서 고정용 기대값). </summary>
        private const int SelfCheckExpectedMineCount = 4;

        /// <summary> 자기 검증 seed 로 뽑혀야 하는 시작 광산 방향(순서 고정용 기대값). </summary>
        private const MapStartingMineSide SelfCheckExpectedSide = MapStartingMineSide.CaseA;

        /// <summary>
        /// 두 준비 결과가 같은 맵인지(결정성) 확인한다.
        /// 시간은 실행할 때마다 달라지므로 비교하지 않는다.
        /// </summary>
        /// <param name="a">결과 1</param>
        /// <param name="b">결과 2</param>
        /// <param name="failureReason">다른 점(같으면 null)</param>
        /// <returns>같으면 true</returns>
        private static bool TryCompareResults(MapPreparationResult a, MapPreparationResult b,
            out string failureReason)
        {
            if (a.IsSucceeded != b.IsSucceeded || a.ErrorCode != b.ErrorCode)
            {
                failureReason = "같은 seed 인데 성공 여부/에러 코드가 다르다.";
                return false;
            }

            if (a.MapType != b.MapType || a.NeutralMineCount != b.NeutralMineCount ||
                a.StartingMineSide != b.StartingMineSide || a.InitialGold != b.InitialGold ||
                a.AttemptCount != b.AttemptCount || a.UsedFallback != b.UsedFallback)
            {
                failureReason = "같은 seed 인데 선택값 또는 시도 결과가 다르다.";
                return false;
            }

            if (a.CanonicalBytes == null || b.CanonicalBytes == null ||
                a.CanonicalBytes.Length != b.CanonicalBytes.Length)
            {
                failureReason = "같은 seed 인데 canonical 바이트 길이가 다르다.";
                return false;
            }

            for (int i = 0; i < a.CanonicalBytes.Length; i++)
            {
                if (a.CanonicalBytes[i] != b.CanonicalBytes[i])
                {
                    failureReason = "같은 seed 인데 canonical 바이트가 다르다(인덱스 " + i + ").";
                    return false;
                }
            }

            if (!MapDefinitionCodec.HashEquals(a.Hash, b.Hash))
            {
                failureReason = "같은 seed 인데 최종 맵 해시가 다르다.";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 에디터에서만 실행되는 자기 검증. 어긋나면 즉시 예외로 알린다.
        /// 빌드에는 이 호출 자체가 컴파일되지 않는다(Conditional 특성).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void AssertSelfCheck()
        {
            if (!TryRunSelfCheck(out string reason))
            {
                throw new InvalidOperationException("MapPreparationUseCase 자기 검증 실패: " + reason);
            }
        }

        /// <summary>
        /// 자기 검증 전용 템플릿 소스. 파일을 읽는 대신 Domain 의 팩터리로 템플릿을
        /// 그 자리에서 만들어 메모리에 들고 있는다.
        ///
        /// 🔴 이렇게 하면 Unity 도, Resources 폴더도 없이 폴백 경로를 전부 돌려 볼 수 있다.
        ///    실제 런타임에서는 Infrastructure 의 Resources 기반 구현체가 쓰인다.
        /// </summary>
        private sealed class InMemoryFallbackTemplateSource : IMapFallbackTemplateSource
        {
            private readonly Dictionary<MapType, byte[]> _bytesByType = new Dictionary<MapType, byte[]>();
            private readonly Dictionary<MapType, byte[]> _hashByType = new Dictionary<MapType, byte[]>();

            /// <summary>
            /// 5개 유형의 템플릿을 모두 만들어 메모리에 담는다.
            /// </summary>
            /// <param name="failureReason">실패 사유(성공 시 null)</param>
            /// <returns>5개를 모두 만들었으면 true</returns>
            public bool TryBuildAll(out string failureReason)
            {
                if (!MapFallbackTemplateFactory.TryBuildAll(
                        out IReadOnlyList<MapFallbackTemplate> templates, out failureReason))
                {
                    return false;
                }

                for (int i = 0; i < templates.Count; i++)
                {
                    MapFallbackTemplate template = templates[i];
                    _bytesByType[template.MapType] = template.CanonicalBytes;
                    _hashByType[template.MapType] = template.Hash;
                }

                failureReason = null;
                return true;
            }

            /// <summary>
            /// 그 유형의 템플릿 바이트를 돌려준다.
            /// </summary>
            /// <param name="mapType">맵 유형</param>
            /// <param name="canonicalBytes">템플릿 바이트(없으면 null)</param>
            /// <param name="failureReason">실패 사유(성공 시 null)</param>
            /// <returns>있으면 true</returns>
            public bool TryLoadCanonicalBytes(MapType mapType, out byte[] canonicalBytes,
                out string failureReason)
            {
                if (!_bytesByType.TryGetValue(mapType, out canonicalBytes))
                {
                    failureReason = "메모리 템플릿에 " + mapType + " 이 없다.";
                    return false;
                }

                failureReason = null;
                return true;
            }

            /// <summary>
            /// 넘겨받은 바이트가 저장된 템플릿 바이트와 완전히 같은지 확인한다.
            /// </summary>
            /// <param name="mapType">맵 유형</param>
            /// <param name="candidate">비교할 바이트</param>
            /// <returns>같으면 true</returns>
            public bool MatchesStoredBytes(MapType mapType, byte[] candidate)
            {
                if (candidate == null) return false;
                if (!_bytesByType.TryGetValue(mapType, out byte[] stored)) return false;
                if (stored.Length != candidate.Length) return false;

                for (int i = 0; i < stored.Length; i++)
                {
                    if (stored[i] != candidate[i]) return false;
                }
                return true;
            }

            // 🔴 2026-09-14 제거: 여기에 public byte[] GetStoredHash(MapType) 가 있었다.
            //    「테스트 모드 폴백의 해시가 템플릿 해시와 달라졌는가」를 보는 자체 점검 덩어리가
            //    유일한 호출부였고, 그 덩어리가 이번에 함께 삭제돼 호출자가 0건이 됐다.
            //    (해시 저장 자체는 _hashByType 에 남아 있으므로 필요해지면 되살리면 된다.)
        }
    }
}
