// ============================================================================
// MapDefinitionValidator.cs
// 완성된 맵 한 판(MapDefinition)이 규칙 13(4장)을 지켰는지 확인하는 검증기.
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 파일이 무엇을 하는가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   맵 생성기는 난수로 지형을 그리고 광산을 뿌린다. 난수인 이상 "규칙을 어긴 맵"이
//   나올 수 있다. 이 검증기는 그렇게 만들어진 맵을 마지막에 한 번 훑어보고
//   "이건 써도 되는 맵인가"를 판정한다.
//
//   🔴 이 검증기는 생성기를 조금도 믿지 않는다.
//      "대칭으로 만들었으니 대칭일 것이다" 같은 말을 받아들이지 않고, 완성된
//      MapDefinition 의 타일 배열과 배치 목록만 직접 읽어서 다시 확인한다.
//      생성기 안쪽 상태나 "대칭으로 만들었다"는 플래그는 입력이 아니다.
//      그래서 생성기에 버그가 생겨도 이 검증기가 알아챌 수 있다.
//
//   입력은 세 개이며 셋 다 필수다.
//     · MapDefinition            — 완성된 맵 그 자체
//     · IMapArchetypeConstraints — 그 시도에서 확정된 유형별 제약
//                                  (광산 금지 구역 · 필수 통로 목록 · 규정 폭 · 통로 대역)
//     · IMapArchetypeGenerator   — 「이 유형이 이 중립 광산 개수를 허용하는가」를 묻는 상대
//
//   🔴 생성기를 선택 입력으로 두지 않는 이유:
//      유형별 허용 광산 수(3장 ③)는 생성기만 아는 값이다. 예전에는 생성기를 안 넘기면
//      규칙 3의 공통 범위(1~6)까지만 보고 넘어갔는데, 그러면 부르는 쪽이 한 번 빠뜨리는
//      것만으로 검사가 조용히 약해진다. 필수로 만들어 그 길을 없앴다.
//      생성기에게 묻는 것은 「그 유형의 규칙」 하나뿐이고, 생성기가 만들어 낸 결과(대칭
//      플래그 같은 것)는 여전히 입력이 아니다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 검증 순서는 고정이고, 실패 처리가 항목마다 다르다 (규칙 13)
// ─────────────────────────────────────────────────────────────────────────────
//
//   순서   검증                                                     실패 시
//   ----   ------------------------------------------------------   ------------
//   전제   맵 크기 11 x 21 · 타일 231칸                              판 전체 버림
//    1     모든 맵 요소·장식의 180도 회전 대칭                       판 전체 버림
//          + 짝 없는 6칸이 전부 막힌 타일인지 (고정값 검사)
//    2     유형별 형상 제약(필수 통로 폭 · 광산 수 · 광산 금지 구역)  다시 뽑기
//    3     양 팀 즉시 건설 가능한 고유 타일 정확히 10개               판 전체 버림
//    4     성↔성 도달 가능 · 모든 성에서 모든 광산 덩어리 도달 가능   판 전체 버림
//    5     교차 접근 거리 · 기하 교차 거리                            판 전체 버림
//    6     실제 초기 골드가 규칙 3이 정한 값과 같은가                 판 전체 버림
//
//   · BFS 를 쓰는 4·5번이 가장 비싸서 뒤에 둔다.
//   · 「전제」에 번호가 없고 1번보다 앞인 이유: 크기가 다르면 1~6번의 좌표 계산
//     자체가 성립하지 않는다. 여기서 걸리면 나머지를 아예 돌리지 않는다.
//     1~6 의 번호는 규칙 문서·과거 작업 문서가 그대로 참조하므로 바꾸지 않는다.
//   · 「다시 뽑기」 = 같은 후보 확률을 유지한 채 위반한 부분만 다시 뽑는다.
//     「판 전체 버림」 = 광산을 옮기거나 수선하지 않고 시도 전체를 거부한다.
//
//   🔴 이 검증기는 재시도를 직접 하지 않는다. 두 실패 종류를 결과 값으로 구분해
//      돌려줄 뿐이고, 실제 재시도 절차는 상위 조정자(코디네이터)의 몫이다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 「접근 거리」에 쓰는 네 가지 용어 — 다른 표기를 쓰지 않는다
// ─────────────────────────────────────────────────────────────────────────────
//   성 접근 칸           : 성에 인접한 이동 가능 타일 전체
//   광산 덩어리          : 서로 인접한 광산들을 하나로 묶은 것.
//                          이웃 광산이 없는 광산은 자기 혼자서 덩어리 하나다.
//   광산 덩어리 접근 칸  : 그 덩어리에 속한 광산 중 어느 하나에라도 인접한
//                          이동 가능 타일 전체
//   접근 거리            : 성 접근 칸과 광산 덩어리 접근 칸 사이의 최소 이동 칸 수
//
//   이동 가능 타일(StaticTraversable) 의 정의는 아래 IsStaticTraversable 하나뿐이다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 13(4장) · 규칙 3(2장) ·
//       3장 ⑥(필수 통로 규정 폭)
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 검증 결과의 세 가지 종류. 규칙 13이 정한 두 실패 처리 방식을 그대로 옮긴 것이다.
    /// 숫자 값은 로그에 남을 수 있으므로 바꾸지 말 것.
    /// </summary>
    public enum MapValidationOutcome
    {
        /// <summary> 통과 — 이 맵을 그대로 써도 된다. </summary>
        Passed = 0,

        /// <summary>
        /// 다시 뽑기 — 같은 후보 확률을 유지한 채 위반한 부분만 다시 뽑는다(규칙 13 검증 2번).
        /// </summary>
        Resample = 1,

        /// <summary>
        /// 판 전체 버림 — 광산을 옮기거나 수선하지 않고 시도 전체를 거부한다
        /// (규칙 13 전제 · 1 · 3 · 4 · 5 · 6번).
        /// </summary>
        RejectAttempt = 2
    }

    /// <summary>
    /// 검증 한 번의 결과. 「어떤 처리를 해야 하는가(Outcome)」와 「몇 번 검증에서
    /// 왜 걸렸는가(FailedCheckNumber · FailureReason)」를 함께 담는다.
    /// </summary>
    public readonly struct MapValidationResult
    {
        /// <summary> 처리 방식(통과 / 다시 뽑기 / 판 전체 버림). </summary>
        public MapValidationOutcome Outcome { get; }

        /// <summary>
        /// 걸린 검증의 번호. 「전제」는 0, 규칙 13의 1~6번은 그 번호 그대로,
        /// 통과하면 -1 이다(MapDefinitionValidator 의 상수 참조).
        /// </summary>
        public int FailedCheckNumber { get; }

        /// <summary> 사람이 읽는 실패 사유(통과 시 null). </summary>
        public string FailureReason { get; }

        /// <summary> 통과했으면 true. </summary>
        public bool IsPassed => Outcome == MapValidationOutcome.Passed;

        /// <summary>
        /// 결과를 직접 만든다. 보통은 아래 Pass / Resample / Reject 정적 메서드를 쓴다.
        /// </summary>
        /// <param name="outcome">처리 방식</param>
        /// <param name="failedCheckNumber">걸린 검증 번호</param>
        /// <param name="failureReason">실패 사유</param>
        public MapValidationResult(MapValidationOutcome outcome, int failedCheckNumber, string failureReason)
        {
            Outcome = outcome;
            FailedCheckNumber = failedCheckNumber;
            FailureReason = failureReason;
        }

        /// <summary> 통과 결과를 만든다. </summary>
        /// <returns>통과 결과</returns>
        public static MapValidationResult Pass()
        {
            return new MapValidationResult(MapValidationOutcome.Passed,
                MapDefinitionValidator.PassedCheckNumber, null);
        }

        /// <summary>
        /// 「다시 뽑기」 결과를 만든다.
        /// </summary>
        /// <param name="checkNumber">걸린 검증 번호</param>
        /// <param name="reason">실패 사유(비워 둘 수 없다)</param>
        /// <returns>다시 뽑기 결과</returns>
        public static MapValidationResult Resample(int checkNumber, string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("실패 사유는 비워 둘 수 없다.", nameof(reason));
            }
            return new MapValidationResult(MapValidationOutcome.Resample, checkNumber, reason);
        }

        /// <summary>
        /// 「판 전체 버림」 결과를 만든다.
        /// </summary>
        /// <param name="checkNumber">걸린 검증 번호</param>
        /// <param name="reason">실패 사유(비워 둘 수 없다)</param>
        /// <returns>판 전체 버림 결과</returns>
        public static MapValidationResult Reject(int checkNumber, string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("실패 사유는 비워 둘 수 없다.", nameof(reason));
            }
            return new MapValidationResult(MapValidationOutcome.RejectAttempt, checkNumber, reason);
        }

        /// <summary> 로그에 그대로 넣을 수 있는 한 줄 표현. </summary>
        /// <returns>결과 문자열</returns>
        public override string ToString()
        {
            if (IsPassed) return "통과";
            string kind = Outcome == MapValidationOutcome.Resample ? "다시 뽑기" : "판 전체 버림";
            string where = FailedCheckNumber == MapDefinitionValidator.PrerequisiteCheckNumber
                ? "전제"
                : "검증 " + FailedCheckNumber + "번";
            return kind + " (" + where + "): " + FailureReason;
        }
    }

    /// <summary>
    /// 완성된 MapDefinition 이 규칙 13을 지켰는지 확인하는 검증기.
    /// 상태를 갖지 않는 정적 클래스이며, 입력을 고치지 않는다(읽기만 한다).
    /// </summary>
    public static class MapDefinitionValidator
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary> 통과했을 때 결과에 들어가는 검증 번호. </summary>
        public const int PassedCheckNumber = -1;

        /// <summary> 「전제」(맵 크기 확인)의 검증 번호. 규칙 13의 1~6번보다 앞이라 0 이다. </summary>
        public const int PrerequisiteCheckNumber = 0;

        /// <summary> 규격 가로 타일 수(규칙 1). </summary>
        public const int RequiredWidth = MapDefinition.DefaultWidth;

        /// <summary> 규격 세로 타일 수(규칙 1). </summary>
        public const int RequiredHeight = MapDefinition.DefaultHeight;

        /// <summary> 규격 타일 배열 길이(11 x 21 = 231). </summary>
        public const int RequiredTileCount = RequiredWidth * RequiredHeight;

        /// <summary> 정상 모드에서 허용되는 중립 광산 개수의 하한(규칙 3의 표). </summary>
        public const int MinNeutralMineCount = 1;

        /// <summary> 정상 모드에서 허용되는 중립 광산 개수의 상한(규칙 3의 표). </summary>
        public const int MaxNeutralMineCount = 6;

        /// <summary> 테스트 모드 표식이 이 값이면 정상 모드다. </summary>
        public const int NormalModeFlag = 0;

        /// <summary> 테스트 모드 표식이 이 값이면 테스트 모드다. </summary>
        public const int TestModeFlag = 1;

        /// <summary>
        /// 테스트 모드에서 확정되는 초기 골드(GameConfig.TestStartingGold 와 같은 값, 규칙 3).
        /// 테스트 모드에서는 중립 광산 개수와 무관하게 언제나 이 값이다.
        /// </summary>
        public const int TestModeInitialGold = 5000;

        /// <summary>
        /// 광산이 0개면 통로가 유지해야 하는 이동 가능 폭(규칙 13 「필수 통로 검증」).
        /// </summary>
        public const int TraversableWidthWithoutMine = 3;

        /// <summary>
        /// 광산이 1개일 때 통로가 유지해야 하는 이동 가능 폭(규칙 13 「필수 통로 검증」).
        /// </summary>
        public const int TraversableWidthWithOneMine = 2;

        /// <summary>
        /// 같은 통로에서 「폭을 재는 두 높이 단계의 창(窓)」 하나에 놓일 수 있는 광산의 최대 개수(규칙 13).
        ///
        /// 🔴 단위가 「한 높이 단계」가 아니라 「폭을 재는 창」인 이유 (초급자용 설명):
        ///    폭은 인접한 두 높이 단계의 타일 수를 더해서 잰다(아래 TryCheckCorridor 의 설명 참조).
        ///    그런데 광산만 한 단계에서 세면 단위가 어긋난다. 예를 들어 폭 3 통로의 한 줄에
        ///    광산 2개가 들어가도, 그 2개가 서로 다른 높이 단계에 하나씩 놓이면 단계마다
        ///    1개씩으로 보여 통과해 버린다. 실제로는 통행 폭이 1까지 좁아진 상태다.
        ///    그래서 폭을 재는 창과 똑같은 두 단계에서 광산도 함께 센다.
        /// </summary>
        public const int MaxMinePerCorridorWidthWindow = 1;

        // ────────────────────────────────────────────────────────────────────
        // 🔴 규칙 3의 초기 골드 표는 이 배열 한 곳에만 있다.
        //    0번 칸은 쓰지 않는 자리 채우기다(중립 광산 0개는 규칙상 없다).
        //    표가 바뀌면 여기만 고치면 되고, 검증 6번의 코드는 손대지 않는다.
        // ────────────────────────────────────────────────────────────────────
        private static readonly int[] NormalModeInitialGoldValues = { 0, 700, 600, 500, 400, 300, 200 };

        // ====================================================================
        // 규칙 3 — 초기 골드를 정하는 유일한 자리
        // ====================================================================

        /// <summary>
        /// 그 시점의 모드에 규칙 3이 지정한 초기 골드를 돌려준다.
        ///
        /// 🔴 검증 6번은 「광산 수 표와 대응하는가」가 아니라 「그 시점 모드에 규칙 3이
        ///    지정한 값과 실제 값이 같은가」다. 그래서 모드 갈래가 이 메서드 안에 있고,
        ///    갈래가 나중에 늘어도 검증 6번의 코드는 그대로다.
        /// </summary>
        /// <param name="testModeFlag">테스트 모드 표식(0 = 정상, 1 = 테스트)</param>
        /// <param name="neutralMineCount">중립 광산 개수</param>
        /// <returns>규칙 3이 지정한 초기 골드. 입력이 규칙 범위 밖이면 -1</returns>
        public static int GetExpectedInitialGold(int testModeFlag, int neutralMineCount)
        {
            // 갈래 1 — 테스트 모드: 광산 수와 무관하게 고정값이다.
            if (testModeFlag == TestModeFlag) return TestModeInitialGold;

            // 갈래 2 — 정상 모드: 중립 광산 수에서 표로 파생한다.
            if (testModeFlag != NormalModeFlag) return -1;
            if (neutralMineCount < MinNeutralMineCount || neutralMineCount > MaxNeutralMineCount) return -1;

            return NormalModeInitialGoldValues[neutralMineCount];
        }

        // ====================================================================
        // 진입점
        // ====================================================================

        /// <summary>
        /// 완성된 맵이 규칙 13을 지켰는지 확인한다. 실패는 예외가 아니라 결과 값으로 돌아온다.
        ///
        /// 🔴 생성기는 「빼도 되는 인자」가 아니다.
        ///    유형별 허용 중립 광산 수(3장 ③)는 생성기만 알고 있다. 예전에는 생성기를
        ///    선택 인자로 두고 안 넘기면 규칙 3의 공통 범위(1~6)까지만 봤는데, 그러면
        ///    부르는 쪽이 한 번 빠뜨리는 순간 검사가 조용히 약해진다. 그래서 필수 인자로 만들어
        ///    「빠뜨릴 수 있는 길」 자체를 없앴다(빠뜨리면 컴파일이 되지 않는다).
        /// </summary>
        /// <param name="definition">검증할 맵 정의</param>
        /// <param name="constraints">그 시도에서 확정된 유형별 제약</param>
        /// <param name="generator">이 맵을 만든 생성기(유형별 허용 광산 수를 여기서 묻는다)</param>
        /// <returns>검증 결과(통과 / 다시 뽑기 / 판 전체 버림)</returns>
        public static MapValidationResult Validate(MapDefinition definition,
            IMapArchetypeConstraints constraints, IMapArchetypeGenerator generator)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (constraints == null) throw new ArgumentNullException(nameof(constraints));
            if (generator == null) throw new ArgumentNullException(nameof(generator));

            // ── 전제 — 크기가 규격과 같은가 ─────────────────────────────────
            // 여기서 걸리면 나머지를 돌리지 않는다. 좌표 계산의 전제가 무너졌기 때문이다.
            if (definition.Width != RequiredWidth || definition.Height != RequiredHeight ||
                definition.Tiles.Length != RequiredTileCount)
            {
                return MapValidationResult.Reject(PrerequisiteCheckNumber,
                    "맵 크기가 규격(" + RequiredWidth + "x" + RequiredHeight + " · " + RequiredTileCount +
                    "칸)과 다르다: " + definition.Width + "x" + definition.Height +
                    " · " + definition.Tiles.Length + "칸.");
            }

            // ── 1 ───────────────────────────────────────────────────────────
            if (!TryCheckRotationalSymmetry(definition, out MapValidationResult symmetryFailure))
            {
                return symmetryFailure;
            }

            // ── 2 ───────────────────────────────────────────────────────────
            if (!TryCheckArchetypeShape(definition, constraints, generator, out MapValidationResult shapeFailure))
            {
                return shapeFailure;
            }

            // 3~5번이 함께 쓰는 초기 상태 스냅샷. 여기서 한 번만 만든다.
            var initialState = new InitialMapStateEvaluator(definition);

            // ── 3 ───────────────────────────────────────────────────────────
            if (!TryCheckBuildableTileCount(initialState, out MapValidationResult buildableFailure))
            {
                return buildableFailure;
            }

            // ── 4 ───────────────────────────────────────────────────────────
            if (!TryBuildAccessContext(definition, initialState,
                    out MapAccessContext access, out MapValidationResult accessFailure))
            {
                return accessFailure;
            }

            // ── 5 ───────────────────────────────────────────────────────────
            if (!TryCheckCrossDistances(definition, access, out MapValidationResult crossFailure))
            {
                return crossFailure;
            }

            // ── 6 ───────────────────────────────────────────────────────────
            if (!TryCheckInitialGold(definition, out MapValidationResult goldFailure))
            {
                return goldFailure;
            }

            return MapValidationResult.Pass();
        }

        // ====================================================================
        // 검증 1 — 180도 회전 대칭 + 짝 없는 6칸 고정값 검사
        // ====================================================================

        /// <summary>
        /// 모든 맵 요소·장식이 180도 회전 대칭인지, 그리고 짝 없는 6칸이 전부 막힌 타일인지 확인한다.
        ///
        /// 🔴 짝 없는 6칸은 「대칭 검사에서 제외」가 아니라 「고정값 검사로 대체」다.
        ///    회전 상대가 격자 밖이라 대칭으로는 아무것도 말할 수 없는데, 그냥 빠뜨리면
        ///    그 6칸이 무엇이든 될 수 있어 검증에 구멍이 생긴다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="failure">실패 결과(통과 시 기본값)</param>
        /// <returns>통과하면 true</returns>
        private static bool TryCheckRotationalSymmetry(MapDefinition definition,
            out MapValidationResult failure)
        {
            const int checkNumber = 1;
            int width = definition.Width;
            int height = definition.Height;

            // ── 1-1. 짝 없는 6칸 고정값 검사 ────────────────────────────────
            // 목록은 SymmetricMapBuilder 가 계산해 둔 것을 그대로 쓴다(중복 구현 금지).
            var referenceBuilder = new SymmetricMapBuilder();
            IReadOnlyList<int> unpaired = referenceBuilder.UnpairedTileIndices;

            for (int i = 0; i < unpaired.Count; i++)
            {
                int index = unpaired[i];
                if (definition.Tiles[index] != TileKind.Blocked)
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "짝 없는 칸 (" + definition.ToCol(index) + "," + definition.ToRow(index) +
                        ") 이 막힌 타일이 아니다(실제 " + definition.Tiles[index] + ").");
                    return false;
                }
            }

            // ── 1-2. 타일 종류의 회전 대칭 ──────────────────────────────────
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    SymmetricMapBuilder.RotateCoord(col, row, width, height,
                        out int rotatedCol, out int rotatedRow);
                    if (!SymmetricMapBuilder.IsInsideGrid(rotatedCol, rotatedRow, width, height)) continue;

                    int index = definition.ToIndex(col, row);
                    int rotatedIndex = definition.ToIndex(rotatedCol, rotatedRow);

                    // RotateState 는 "회전 자리에 놓여야 하는 상태"를 정하는 단일 소스다.
                    // 지형에는 팀 개념이 없어 지금은 항등이지만, 그 사실을 여기서 다시
                    // 가정하지 않고 언제나 RotateState 를 거친다.
                    if (definition.Tiles[index] !=
                        SymmetricMapBuilder.RotateState(definition.Tiles[rotatedIndex]))
                    {
                        failure = MapValidationResult.Reject(checkNumber,
                            "타일 회전 대칭 위반 (" + col + "," + row + ")=" + definition.Tiles[index] +
                            " vs (" + rotatedCol + "," + rotatedRow + ")=" + definition.Tiles[rotatedIndex] + ".");
                        return false;
                    }
                }
            }

            // ── 1-3. 성 · 시작 광산의 회전 대칭(위치 + 팀) ──────────────────
            if (!TryCheckPlacementSymmetry(definition, definition.Castles, "성", out failure)) return false;
            if (!TryCheckPlacementSymmetry(definition, definition.StartingMines, "시작 광산", out failure)) return false;

            // ── 1-4. 중립 광산의 회전 대칭 ──────────────────────────────────
            var neutralMines = new HashSet<int>();
            for (int i = 0; i < definition.NeutralMines.Count; i++)
            {
                if (!neutralMines.Add(definition.NeutralMines[i]))
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "중립 광산 목록에 같은 칸이 두 번 들어 있다(인덱스 " + definition.NeutralMines[i] + ").");
                    return false;
                }
            }

            foreach (int index in neutralMines)
            {
                if (!TryGetRotatedIndex(definition, index, out int rotatedIndex))
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "중립 광산이 짝 없는 칸(" + definition.ToCol(index) + "," +
                        definition.ToRow(index) + ")에 놓였다.");
                    return false;
                }

                if (!neutralMines.Contains(rotatedIndex))
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "중립 광산 (" + definition.ToCol(index) + "," + definition.ToRow(index) +
                        ") 의 회전 대응 자리에 광산이 없다.");
                    return false;
                }
            }

            // ── 1-5. 장식의 회전 대칭 ───────────────────────────────────────
            // 최초 구현의 생성기는 장식을 만들지 않지만(규칙 15), 검증기는 처음부터
            // 장식 스키마를 다룬다. 나중에 장식을 넣을 때 검증이 비어 있으면 안 되기 때문이다.
            var decorations = new HashSet<DecorationDefinition>();
            for (int i = 0; i < definition.Decorations.Count; i++)
            {
                decorations.Add(definition.Decorations[i]);
            }

            for (int i = 0; i < definition.Decorations.Count; i++)
            {
                DecorationDefinition decoration = definition.Decorations[i];

                if (!TryGetRotatedIndex(definition, decoration.TileIndex, out int rotatedIndex))
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "장식이 짝 없는 칸(인덱스 " + decoration.TileIndex + ")에 놓였다.");
                    return false;
                }

                DecorationDefinition expected = SymmetricMapBuilder.RotateState(decoration, rotatedIndex);
                if (!decorations.Contains(expected))
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "장식 회전 대칭 위반(원본 타일 " + decoration.TileIndex +
                        ", 기대 자리 " + rotatedIndex + ").");
                    return false;
                }
            }

            failure = default;
            return true;
        }

        /// <summary>
        /// 성·시작 광산처럼 「위치 + 팀」을 갖는 배치 목록이 회전 대칭인지 확인한다.
        /// 회전 자리에는 반대 팀 것이 있어야 한다(RotateState 가 단일 소스).
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="placements">확인할 배치 목록</param>
        /// <param name="label">실패 사유에 쓸 이름</param>
        /// <param name="failure">실패 결과(통과 시 기본값)</param>
        /// <returns>통과하면 true</returns>
        private static bool TryCheckPlacementSymmetry(MapDefinition definition,
            IReadOnlyList<MapObjectPlacement> placements, string label, out MapValidationResult failure)
        {
            const int checkNumber = 1;

            // 같은 칸에 서로 다른 팀 것이 겹쳐 기록되면 "대칭인 척"할 수 있으므로 먼저 막는다.
            var byTile = new Dictionary<int, TeamId>();
            for (int i = 0; i < placements.Count; i++)
            {
                MapObjectPlacement placement = placements[i];

                if (byTile.TryGetValue(placement.TileIndex, out TeamId existing))
                {
                    if (existing != placement.Team)
                    {
                        failure = MapValidationResult.Reject(checkNumber,
                            label + " 이(가) 같은 칸 " + placement.TileIndex + " 에 두 팀으로 기록됐다.");
                        return false;
                    }
                    continue;
                }

                byTile.Add(placement.TileIndex, placement.Team);
            }

            foreach (KeyValuePair<int, TeamId> entry in byTile)
            {
                if (!TryGetRotatedIndex(definition, entry.Key, out int rotatedIndex))
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        label + " 이(가) 짝 없는 칸(인덱스 " + entry.Key + ")에 놓였다.");
                    return false;
                }

                TeamId expectedTeam = SymmetricMapBuilder.RotateState(entry.Value);
                if (!byTile.TryGetValue(rotatedIndex, out TeamId actualTeam) || actualTeam != expectedTeam)
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        label + " 회전 대칭 위반(원본 " + entry.Key + " / " + entry.Value +
                        " -> 기대 자리 " + rotatedIndex + " / " + expectedTeam + ").");
                    return false;
                }
            }

            failure = default;
            return true;
        }

        // ====================================================================
        // 검증 2 — 유형별 형상 제약 (실패하면 「다시 뽑기」)
        // ====================================================================

        /// <summary>
        /// 유형별 형상 제약을 확인한다 — 광산 수 · 광산 금지 구역 · 필수 통로.
        /// 여기서 걸리는 것은 국소 위반이므로 판을 버리지 않고 「다시 뽑기」로 돌려준다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="constraints">유형별 제약</param>
        /// <param name="generator">이 맵을 만든 생성기(유형별 허용 광산 수를 묻는다)</param>
        /// <param name="failure">실패 결과(통과 시 기본값)</param>
        /// <returns>통과하면 true</returns>
        private static bool TryCheckArchetypeShape(MapDefinition definition,
            IMapArchetypeConstraints constraints, IMapArchetypeGenerator generator,
            out MapValidationResult failure)
        {
            const int checkNumber = 2;

            // ── 2-1. 중립 광산 수 ───────────────────────────────────────────
            if (definition.NeutralMines.Count != definition.NeutralMineCount)
            {
                failure = MapValidationResult.Resample(checkNumber,
                    "중립 광산 수가 기록(" + definition.NeutralMineCount + ")과 실제 배치(" +
                    definition.NeutralMines.Count + ")에서 다르다.");
                return false;
            }

            if (definition.NeutralMineCount < MinNeutralMineCount ||
                definition.NeutralMineCount > MaxNeutralMineCount)
            {
                failure = MapValidationResult.Resample(checkNumber,
                    "중립 광산 수 " + definition.NeutralMineCount + " 가 규칙 3의 범위(" +
                    MinNeutralMineCount + "~" + MaxNeutralMineCount + ") 밖이다.");
                return false;
            }

            if (!generator.IsNeutralMineCountAllowed(definition.NeutralMineCount))
            {
                failure = MapValidationResult.Resample(checkNumber,
                    "중립 광산 수 " + definition.NeutralMineCount + " 가 이 유형(" +
                    constraints.MapType + ")이 허용하는 개수가 아니다.");
                return false;
            }

            // ── 2-2. 광산 금지 구역 ─────────────────────────────────────────
            for (int i = 0; i < definition.NeutralMines.Count; i++)
            {
                int index = definition.NeutralMines[i];
                if (!definition.IsValidIndex(index))
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "중립 광산 인덱스 " + index + " 가 격자 밖이다.");
                    return false;
                }

                int col = definition.ToCol(index);
                int row = definition.ToRow(index);

                // 막힌 타일 위의 광산은 어떤 유형에서도 성립하지 않는다(누구도 캘 수 없다).
                if (definition.Tiles[index] == TileKind.Blocked)
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "중립 광산이 막힌 타일 (" + col + "," + row + ") 위에 있다.");
                    return false;
                }

                if (constraints.IsNeutralMineForbidden(col, row))
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "중립 광산이 금지 구역 (" + col + "," + row + ") 에 있다.");
                    return false;
                }
            }

            // ── 2-3. 필수 통로 ──────────────────────────────────────────────
            // 광산은 중립 광산과 시작 광산을 모두 센다. 통로를 막는다는 점에서는 같기 때문이다.
            var mineTiles = new HashSet<int>(definition.NeutralMines);
            for (int i = 0; i < definition.StartingMines.Count; i++)
            {
                mineTiles.Add(definition.StartingMines[i].TileIndex);
            }

            IReadOnlyList<MapCorridorRequirement> corridors = constraints.RequiredCorridors;
            for (int i = 0; i < corridors.Count; i++)
            {
                if (!TryCheckCorridor(definition, corridors[i], mineTiles, out failure)) return false;
            }

            failure = default;
            return true;
        }

        /// <summary>
        /// 필수 통로 하나를 확인한다.
        ///
        /// 광산 배치 「전」 조건 — 통로가 신고한 높이 단계 대역의 모든 단계에서 단면이 규정 폭
        /// 이상이고, 통로의 열린 타일은 모두 건설 불가 타일이다.
        /// 광산 배치 「후」 조건 — 폭을 재는 두 단계의 창에 광산이 없으면 이동 가능 폭 3 이상,
        /// 1개면 2 이상, 2개 이상은 금지다.
        /// 그리고 통로가 시작 단계에서 끝 단계까지 끊기지 않고 이어지는지 BFS 로 본다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="corridor">확인할 필수 통로</param>
        /// <param name="mineTiles">광산이 놓인 타일 전체(중립 + 시작)</param>
        /// <param name="failure">실패 결과(통과 시 기본값)</param>
        /// <returns>통과하면 true</returns>
        // ────────────────────────────────────────────────────────────────────
        // 🔴 「폭」을 왜 인접한 두 높이 단계의 타일 수 합으로 재는가
        // ────────────────────────────────────────────────────────────────────
        //   규칙 문서 3장은 「폭은 열 구간으로 읽는다」고 정한다. 폭 3 은 4·5·6열이다.
        //   그런데 한 높이 단계에는 짝수 열 아니면 홀수 열만 있다. 그래서 폭 3 통로를
        //   한 단계만 잘라 보면 짝수 단계에서는 4·6열 두 칸, 홀수 단계에서는 5열 한 칸뿐이다.
        //   즉 「한 단계의 타일 수」는 절대 3이 되지 않아서 그대로는 폭을 잴 수 없다.
        //
        //   반면 인접한 두 단계는 짝수 열 몫과 홀수 열 몫을 정확히 한 번씩 담는다.
        //   그래서 두 단계의 타일 수를 더하면 그 열 구간의 열 개수, 곧 「폭」이 그대로 나온다.
        //
        //        높이 단계 20 (짝수 열):  . . . ●   ●  . . .     4·6열 -> 2칸
        //        높이 단계 21 (홀수 열):  . . .   ●    . . .     5열   -> 1칸
        //                                 ------------------
        //                                 합 3 = 폭 3
        //
        //   짝을 이룰 단계가 없는 끝 단계는 반대쪽 이웃과 짝을 짓는다.
        //
        // ────────────────────────────────────────────────────────────────────
        // 🔴 훑는 범위는 「통로에 실제로 있는 단계」가 아니라 「통로가 신고한 대역 전체」다
        // ────────────────────────────────────────────────────────────────────
        //   종전에는 통로 타일을 담은 사전(Dictionary)의 열쇠만 정렬해 훑었다. 그러면
        //   타일이 하나도 없는 높이 단계는 「폭 0 으로 걸리는」 것이 아니라 아예 검사
        //   대상에서 빠진다. 한 줄이 통째로 막혀 병목이 생겨도 그 줄이 조용히 사라져
        //   버리는 것이다.
        //   그래서 지금은 corridor.MinHeightStep ~ MaxHeightStep 을 한 단계씩 전부 훑는다.
        //   타일이 없는 단계는 tilesAtStep.Count 가 0 이라 폭이 자연히 좁게 나온다.
        // ────────────────────────────────────────────────────────────────────
        private static bool TryCheckCorridor(MapDefinition definition, MapCorridorRequirement corridor,
            HashSet<int> mineTiles, out MapValidationResult failure)
        {
            const int checkNumber = 2;

            IReadOnlyList<int> tileIndices = corridor.TileIndices;
            if (tileIndices.Count == 0)
            {
                failure = MapValidationResult.Resample(checkNumber,
                    "필수 통로 '" + corridor.Name + "' 에 타일이 하나도 없다.");
                return false;
            }

            // 높이 단계별로 통로 타일을 모으면서, 통로 타일의 지형 조건도 함께 본다.
            var tilesByHeightStep = new Dictionary<int, List<int>>();
            var corridorTiles = new HashSet<int>();

            for (int i = 0; i < tileIndices.Count; i++)
            {
                int index = tileIndices[i];
                if (!definition.IsValidIndex(index))
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "필수 통로 '" + corridor.Name + "' 에 격자 밖 인덱스 " + index + " 가 있다.");
                    return false;
                }

                int col = definition.ToCol(index);
                int row = definition.ToRow(index);
                TileKind kind = definition.Tiles[index];

                if (kind == TileKind.Blocked)
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "필수 통로 '" + corridor.Name + "' 의 칸 (" + col + "," + row + ") 이 막혀 있다.");
                    return false;
                }

                // 규칙 13 「해당 통로의 열린 타일은 모두 건설 불가 타일이다」.
                if (kind != TileKind.NoBuild)
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "필수 통로 '" + corridor.Name + "' 의 열린 칸 (" + col + "," + row +
                        ") 이 건설 불가 타일이 아니다(실제 " + kind + ").");
                    return false;
                }

                corridorTiles.Add(index);

                int heightStep = MapBandTable.GetHeightStep(col, row);

                // 통로 타일이 신고한 대역 밖에 있으면 대역 신고 자체가 틀린 것이다.
                // 이것을 넘어가면 「대역 전체를 훑는다」는 이번 검사의 전제가 무너지므로 여기서 막는다.
                if (heightStep < corridor.MinHeightStep || heightStep > corridor.MaxHeightStep)
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "필수 통로 '" + corridor.Name + "' 의 칸 (" + col + "," + row + ") 의 높이 단계 " +
                        heightStep + " 가 통로가 신고한 대역(" + corridor.MinHeightStep + "~" +
                        corridor.MaxHeightStep + ") 밖이다.");
                    return false;
                }

                if (!tilesByHeightStep.TryGetValue(heightStep, out List<int> bucket))
                {
                    bucket = new List<int>();
                    tilesByHeightStep.Add(heightStep, bucket);
                }
                bucket.Add(index);
            }

            // 통로가 신고한 대역을 처음부터 끝까지 한 단계씩 훑는다.
            // (타일이 없는 단계도 반드시 들어가야 하므로 사전의 열쇠를 쓰지 않는다.)
            var emptyStep = new List<int>();

            for (int step = corridor.MinHeightStep; step <= corridor.MaxHeightStep; step++)
            {
                if (!tilesByHeightStep.TryGetValue(step, out List<int> tilesAtStep))
                {
                    tilesAtStep = emptyStep;
                }

                // 짝을 이룰 이웃 단계: 위쪽을 먼저, 없으면 아래쪽을 쓴다.
                // (대역의 끝 단계는 한쪽에 이웃이 없으므로 반대쪽과 짝을 짓는다.)
                List<int> pairedTiles = emptyStep;
                if (tilesByHeightStep.TryGetValue(step + 1, out List<int> upper))
                {
                    pairedTiles = upper;
                }
                else if (tilesByHeightStep.TryGetValue(step - 1, out List<int> lower))
                {
                    pairedTiles = lower;
                }

                int openWidth = tilesAtStep.Count + pairedTiles.Count;

                // 🔴 광산도 폭과 「똑같은 두 단계」에서 센다. 폭은 두 단계로 재면서 광산만
                //    한 단계에서 세면 단위가 어긋나 한 줄에 광산 2개가 들어간 통로가 통과한다.
                int mineCount = 0;
                for (int t = 0; t < tilesAtStep.Count; t++)
                {
                    if (mineTiles.Contains(tilesAtStep[t])) mineCount++;
                }
                for (int t = 0; t < pairedTiles.Count; t++)
                {
                    if (mineTiles.Contains(pairedTiles[t])) mineCount++;
                }

                if (mineCount > MaxMinePerCorridorWidthWindow)
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "필수 통로 '" + corridor.Name + "' 의 높이 단계 " + step +
                        " 을 기준으로 폭을 재는 두 단계에 광산이 " + mineCount +
                        "개다(허용 " + MaxMinePerCorridorWidthWindow + "개).");
                    return false;
                }

                if (openWidth < corridor.RequiredWidth)
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "필수 통로 '" + corridor.Name + "' 의 높이 단계 " + step + " 단면 폭 " +
                        openWidth + " 이 규정 폭 " + corridor.RequiredWidth + " 보다 좁다.");
                    return false;
                }

                if (corridor.IsExactWidth && openWidth != corridor.RequiredWidth)
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "필수 통로 '" + corridor.Name + "' 의 높이 단계 " + step + " 단면 폭 " +
                        openWidth + " 이 규정 폭 " + corridor.RequiredWidth + " 과 다르다(정확히 그 폭이어야 한다).");
                    return false;
                }

                int requiredTraversableWidth = mineCount == 0
                    ? TraversableWidthWithoutMine
                    : TraversableWidthWithOneMine;

                if (openWidth - mineCount < requiredTraversableWidth)
                {
                    failure = MapValidationResult.Resample(checkNumber,
                        "필수 통로 '" + corridor.Name + "' 의 높이 단계 " + step + " 이동 가능 폭 " +
                        (openWidth - mineCount) + " 이 필요한 " + requiredTraversableWidth + " 보다 좁다(광산 " +
                        mineCount + "개).");
                    return false;
                }
            }

            // ── 연속성 — 시작 단계에서 끝 단계까지 이어지는가, 끊긴 조각은 없는가 ──
            // 통로 타일만으로 BFS 한다. 여기서는 광산을 빼지 않는다. 이 검사가 보는 것은
            // 「통로 지형이 하나로 이어져 있는가」이고, 광산 때문에 실제로 길이 끊겼는지는
            // 검증 4번(성↔성 도달)이 판 전체를 걸고 본다.
            // 시작·끝은 「타일이 실제로 있는」 가장 낮은 단계와 가장 높은 단계다.
            // (대역 전체를 훑는 위 반복문과 달리 여기서는 빈 단계를 기준으로 삼을 수 없다.
            //  통로에 타일이 하나라도 있으면 아래 두 목록은 반드시 채워진다.)
            var startTiles = emptyStep;
            var endTiles = new HashSet<int>();

            for (int step = corridor.MinHeightStep; step <= corridor.MaxHeightStep; step++)
            {
                if (!tilesByHeightStep.TryGetValue(step, out List<int> tilesAtStep)) continue;

                if (startTiles.Count == 0) startTiles = tilesAtStep;

                endTiles.Clear();
                for (int t = 0; t < tilesAtStep.Count; t++)
                {
                    endTiles.Add(tilesAtStep[t]);
                }
            }

            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            for (int i = 0; i < startTiles.Count; i++)
            {
                if (visited.Add(startTiles[i])) queue.Enqueue(startTiles[i]);
            }

            var neighborBuffer = new int[InitialMapStateEvaluator.MaxNeighborCount];
            bool reachedEnd = false;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (endTiles.Contains(current)) reachedEnd = true;

                int neighborCount = InitialMapStateEvaluator.GetNeighborIndices(
                    current, definition.Width, definition.Height, neighborBuffer);

                for (int n = 0; n < neighborCount; n++)
                {
                    int neighbor = neighborBuffer[n];
                    if (!corridorTiles.Contains(neighbor)) continue;
                    if (!visited.Add(neighbor)) continue;
                    queue.Enqueue(neighbor);
                }
            }

            if (!reachedEnd)
            {
                failure = MapValidationResult.Resample(checkNumber,
                    "필수 통로 '" + corridor.Name + "' 가 시작 높이 단계에서 끝 높이 단계까지 이어지지 않는다.");
                return false;
            }

            if (visited.Count != corridorTiles.Count)
            {
                failure = MapValidationResult.Resample(checkNumber,
                    "필수 통로 '" + corridor.Name + "' 에 본체와 끊긴 조각이 있다(이어진 칸 " +
                    visited.Count + " / 전체 " + corridorTiles.Count + ").");
                return false;
            }

            failure = default;
            return true;
        }

        // ====================================================================
        // 검증 3 — 양 팀 즉시 건설 가능한 고유 타일 정확히 10개
        // ====================================================================

        /// <summary>
        /// 양 팀이 각각 정확히 10개의 즉시 건설 가능한 고유 타일을 갖는지 확인한다.
        /// 계산은 InitialMapStateEvaluator 한 곳에만 있고 여기서는 그 판정을 부르기만 한다
        /// (같은 계산이 생성기·검증기·런타임에서 어긋나면 조용히 불공평한 맵이 나온다).
        /// </summary>
        /// <param name="initialState">초기 상태 스냅샷</param>
        /// <param name="failure">실패 결과(통과 시 기본값)</param>
        /// <returns>통과하면 true</returns>
        private static bool TryCheckBuildableTileCount(InitialMapStateEvaluator initialState,
            out MapValidationResult failure)
        {
            const int checkNumber = 3;

            if (!initialState.TryValidateBuildableTileCount(out string reason, out int _, out int _))
            {
                failure = MapValidationResult.Reject(checkNumber, reason);
                return false;
            }

            failure = default;
            return true;
        }

        // ====================================================================
        // 검증 4 — 도달 가능성과 접근 거리 계산
        // ====================================================================

        /// <summary>
        /// 검증 4·5번이 함께 쓰는 계산 결과 묶음.
        /// 「광산 덩어리」 별 접근 거리와, 광산 타일이 어느 덩어리에 속하는지를 담는다.
        /// </summary>
        private sealed class MapAccessContext
        {
            /// <summary> 광산 타일 -> 그 광산이 속한 덩어리 번호. </summary>
            public Dictionary<int, int> ClusterIdByMineTile;

            /// <summary> 덩어리 번호 -> Blue 성에서의 접근 거리(도달 못 하면 int.MaxValue). </summary>
            public int[] BlueAccessDistances;

            /// <summary> 덩어리 번호 -> Red 성에서의 접근 거리(도달 못 하면 int.MaxValue). </summary>
            public int[] RedAccessDistances;
        }

        /// <summary>
        /// 그 타일이 「이동 가능 타일(StaticTraversable)」인지 판정한다. 이 정의는 여기 한 곳뿐이다.
        ///
        ///   이동 가능 = 막힌 타일이 아니고 · 광산이 없고 · 성/시작 채굴소가 없다
        ///
        /// 즉 일반 타일과 건설 불가 타일만 이동 가능하다. 런타임 일반 건물은 맵 정의에
        /// 존재하지 않으므로 계산에 들어가지 않는다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="initialState">초기 상태 스냅샷</param>
        /// <param name="tileIndex">확인할 타일</param>
        /// <returns>이동 가능하면 true</returns>
        private static bool IsStaticTraversable(MapDefinition definition,
            InitialMapStateEvaluator initialState, int tileIndex)
        {
            if (definition.Tiles[tileIndex] == TileKind.Blocked) return false;
            if (initialState.GetMineKind(tileIndex) != MineKind.None) return false;
            if (initialState.HasInitialBuilding(tileIndex)) return false;
            return true;
        }

        /// <summary>
        /// 검증 4번을 수행하면서 검증 5번이 쓸 접근 거리를 함께 계산한다.
        ///
        /// 확인하는 것은 셋이다.
        ///   · 양 팀 모두 성 접근 칸이 하나라도 있는가
        ///   · Blue 성 접근 칸에서 Red 성 접근 칸으로 갈 수 있는가(성↔성 도달)
        ///   · 모든 성이 모든 중립 광산 덩어리의 접근 칸에 도달하는가
        ///     (접근 칸이 하나도 없는 덩어리가 있으면 그 자리에서 판을 버린다)
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="initialState">초기 상태 스냅샷</param>
        /// <param name="context">계산 결과(실패 시 null)</param>
        /// <param name="failure">실패 결과(통과 시 기본값)</param>
        /// <returns>통과하면 true</returns>
        private static bool TryBuildAccessContext(MapDefinition definition,
            InitialMapStateEvaluator initialState, out MapAccessContext context,
            out MapValidationResult failure)
        {
            const int checkNumber = 4;

            context = null;
            int tileCount = definition.TileCount;
            var neighborBuffer = new int[InitialMapStateEvaluator.MaxNeighborCount];

            // ── 4-1. 이동 가능 타일 표 ──────────────────────────────────────
            var traversable = new bool[tileCount];
            for (int i = 0; i < tileCount; i++)
            {
                traversable[i] = IsStaticTraversable(definition, initialState, i);
            }

            // ── 4-2. 성 접근 칸 ─────────────────────────────────────────────
            List<int> blueCastleAccess = CollectCastleAccessTiles(definition, traversable, TeamId.Blue, neighborBuffer);
            List<int> redCastleAccess = CollectCastleAccessTiles(definition, traversable, TeamId.Red, neighborBuffer);

            if (blueCastleAccess.Count == 0 || redCastleAccess.Count == 0)
            {
                failure = MapValidationResult.Reject(checkNumber,
                    "성 접근 칸이 없다(Blue " + blueCastleAccess.Count + "칸 / Red " +
                    redCastleAccess.Count + "칸).");
                return false;
            }

            // ── 4-3. 성 접근 칸 전부를 거리 0 으로 두는 multi-source BFS ────
            int[] blueDistances = RunMultiSourceBfs(definition, traversable, blueCastleAccess, neighborBuffer);
            int[] redDistances = RunMultiSourceBfs(definition, traversable, redCastleAccess, neighborBuffer);

            // ── 4-4. 성↔성 도달 ────────────────────────────────────────────
            bool castleToCastle = false;
            for (int i = 0; i < redCastleAccess.Count; i++)
            {
                if (blueDistances[redCastleAccess[i]] != int.MaxValue)
                {
                    castleToCastle = true;
                    break;
                }
            }

            if (!castleToCastle)
            {
                failure = MapValidationResult.Reject(checkNumber,
                    "Blue 성 접근 칸에서 Red 성 접근 칸으로 도달할 수 없다.");
                return false;
            }

            // ── 4-5. 광산 덩어리 만들기 ─────────────────────────────────────
            // 광산은 중립·시작을 가리지 않고 인접하면 한 덩어리로 묶는다. 채굴소를 지으면
            // 인접 6타일이 그 팀 소유가 되어 붙어 있는 광산으로 연쇄되기 때문이다.
            var mineTiles = new HashSet<int>();
            for (int i = 0; i < definition.NeutralMines.Count; i++)
            {
                mineTiles.Add(definition.NeutralMines[i]);
            }
            for (int i = 0; i < definition.StartingMines.Count; i++)
            {
                mineTiles.Add(definition.StartingMines[i].TileIndex);
            }

            var neutralMineTiles = new HashSet<int>(definition.NeutralMines);

            var clusterIdByMineTile = new Dictionary<int, int>();
            var clusters = new List<List<int>>();

            foreach (int mineTile in mineTiles)
            {
                if (clusterIdByMineTile.ContainsKey(mineTile)) continue;

                int clusterId = clusters.Count;
                var members = new List<int>();
                var queue = new Queue<int>();

                clusterIdByMineTile.Add(mineTile, clusterId);
                queue.Enqueue(mineTile);

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    members.Add(current);

                    int neighborCount = InitialMapStateEvaluator.GetNeighborIndices(
                        current, definition.Width, definition.Height, neighborBuffer);

                    for (int n = 0; n < neighborCount; n++)
                    {
                        int neighbor = neighborBuffer[n];
                        if (!mineTiles.Contains(neighbor)) continue;
                        if (clusterIdByMineTile.ContainsKey(neighbor)) continue;

                        clusterIdByMineTile.Add(neighbor, clusterId);
                        queue.Enqueue(neighbor);
                    }
                }

                clusters.Add(members);
            }

            // ── 4-6. 덩어리별 접근 칸과 접근 거리 ───────────────────────────
            var blueAccessDistances = new int[clusters.Count];
            var redAccessDistances = new int[clusters.Count];

            for (int clusterId = 0; clusterId < clusters.Count; clusterId++)
            {
                List<int> members = clusters[clusterId];

                bool hasNeutralMine = false;
                var accessTiles = new HashSet<int>();

                for (int m = 0; m < members.Count; m++)
                {
                    int mineTile = members[m];
                    if (neutralMineTiles.Contains(mineTile)) hasNeutralMine = true;

                    int neighborCount = InitialMapStateEvaluator.GetNeighborIndices(
                        mineTile, definition.Width, definition.Height, neighborBuffer);

                    for (int n = 0; n < neighborCount; n++)
                    {
                        if (traversable[neighborBuffer[n]]) accessTiles.Add(neighborBuffer[n]);
                    }
                }

                // 🔴 재는 단위는 광산 하나가 아니라 광산 덩어리다.
                //    광산 하나만 보면 접근 칸이 0개여도, 그 덩어리의 접근 칸이 하나라도
                //    있으면 유효하다.
                if (accessTiles.Count == 0)
                {
                    if (hasNeutralMine)
                    {
                        failure = MapValidationResult.Reject(checkNumber,
                            "접근 칸이 하나도 없는 중립 광산 덩어리가 있다(광산 " + members.Count + "개).");
                        return false;
                    }

                    blueAccessDistances[clusterId] = int.MaxValue;
                    redAccessDistances[clusterId] = int.MaxValue;
                    continue;
                }

                int blueDistance = int.MaxValue;
                int redDistance = int.MaxValue;

                foreach (int accessTile in accessTiles)
                {
                    if (blueDistances[accessTile] < blueDistance) blueDistance = blueDistances[accessTile];
                    if (redDistances[accessTile] < redDistance) redDistance = redDistances[accessTile];
                }

                if (hasNeutralMine && (blueDistance == int.MaxValue || redDistance == int.MaxValue))
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "성에서 도달할 수 없는 중립 광산 덩어리가 있다(Blue " +
                        (blueDistance == int.MaxValue ? "불가" : blueDistance.ToString()) + " / Red " +
                        (redDistance == int.MaxValue ? "불가" : redDistance.ToString()) + ").");
                    return false;
                }

                blueAccessDistances[clusterId] = blueDistance;
                redAccessDistances[clusterId] = redDistance;
            }

            context = new MapAccessContext
            {
                ClusterIdByMineTile = clusterIdByMineTile,
                BlueAccessDistances = blueAccessDistances,
                RedAccessDistances = redAccessDistances
            };

            failure = default;
            return true;
        }

        /// <summary>
        /// 그 팀 성에 인접한 이동 가능 타일(= 성 접근 칸)을 모은다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="traversable">이동 가능 타일 표</param>
        /// <param name="team">Blue 또는 Red</param>
        /// <param name="neighborBuffer">이웃 계산용 버퍼(길이 6 이상)</param>
        /// <returns>성 접근 칸 목록(중복 없음)</returns>
        private static List<int> CollectCastleAccessTiles(MapDefinition definition, bool[] traversable,
            TeamId team, int[] neighborBuffer)
        {
            var accessTiles = new HashSet<int>();

            for (int i = 0; i < definition.Castles.Count; i++)
            {
                MapObjectPlacement castle = definition.Castles[i];
                if (castle.Team != team) continue;
                if (!definition.IsValidIndex(castle.TileIndex)) continue;

                int neighborCount = InitialMapStateEvaluator.GetNeighborIndices(
                    castle.TileIndex, definition.Width, definition.Height, neighborBuffer);

                for (int n = 0; n < neighborCount; n++)
                {
                    if (traversable[neighborBuffer[n]]) accessTiles.Add(neighborBuffer[n]);
                }
            }

            return new List<int>(accessTiles);
        }

        /// <summary>
        /// 출발 칸 전부를 거리 0 으로 두고 이동 가능 타일 위에서만 BFS 를 돌린다.
        /// 도달하지 못한 칸의 거리는 int.MaxValue 로 남는다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="traversable">이동 가능 타일 표</param>
        /// <param name="sources">출발 칸 목록</param>
        /// <param name="neighborBuffer">이웃 계산용 버퍼(길이 6 이상)</param>
        /// <returns>타일별 최소 이동 칸 수</returns>
        private static int[] RunMultiSourceBfs(MapDefinition definition, bool[] traversable,
            List<int> sources, int[] neighborBuffer)
        {
            var distances = new int[definition.TileCount];
            for (int i = 0; i < distances.Length; i++)
            {
                distances[i] = int.MaxValue;
            }

            var queue = new Queue<int>();
            for (int i = 0; i < sources.Count; i++)
            {
                if (distances[sources[i]] != int.MaxValue) continue;
                distances[sources[i]] = 0;
                queue.Enqueue(sources[i]);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int neighborCount = InitialMapStateEvaluator.GetNeighborIndices(
                    current, definition.Width, definition.Height, neighborBuffer);

                for (int n = 0; n < neighborCount; n++)
                {
                    int neighbor = neighborBuffer[n];
                    if (!traversable[neighbor]) continue;
                    if (distances[neighbor] != int.MaxValue) continue;

                    distances[neighbor] = distances[current] + 1;
                    queue.Enqueue(neighbor);
                }
            }

            return distances;
        }

        // ====================================================================
        // 검증 5 — 교차 접근 거리 · 기하 교차 거리
        // ====================================================================

        /// <summary>
        /// 중립 광산 하나하나에 대해 두 교차 등식을 확인한다.
        ///
        ///   대응쌍 A : 접근거리(Blue,A) == 접근거리(Red,회전(A)) 그리고
        ///              접근거리(Red,A)  == 접근거리(Blue,회전(A))
        ///   중심   C : 회전(C) == C 이므로 위 두 식이 접근거리(Blue,C) == 접근거리(Red,C) 가 된다
        ///
        /// 같은 두 등식을 장애물을 무시한 기하학적 HexCoord 거리로도 따로 검사한다.
        /// 기하 검사는 광산 좌표 자체로 재므로 덩어리 판정의 영향을 받지 않는다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="access">검증 4번이 만든 접근 거리 묶음</param>
        /// <param name="failure">실패 결과(통과 시 기본값)</param>
        /// <returns>통과하면 true</returns>
        private static bool TryCheckCrossDistances(MapDefinition definition, MapAccessContext access,
            out MapValidationResult failure)
        {
            const int checkNumber = 5;

            int blueCastleTile = -1;
            int redCastleTile = -1;
            for (int i = 0; i < definition.Castles.Count; i++)
            {
                MapObjectPlacement castle = definition.Castles[i];
                if (castle.Team == TeamId.Blue) blueCastleTile = castle.TileIndex;
                else if (castle.Team == TeamId.Red) redCastleTile = castle.TileIndex;
            }

            if (blueCastleTile < 0 || redCastleTile < 0)
            {
                failure = MapValidationResult.Reject(checkNumber, "성이 양 팀 모두 있지 않다.");
                return false;
            }

            for (int i = 0; i < definition.NeutralMines.Count; i++)
            {
                int mineTile = definition.NeutralMines[i];

                if (!TryGetRotatedIndex(definition, mineTile, out int rotatedTile))
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "중립 광산이 짝 없는 칸(인덱스 " + mineTile + ")에 있어 회전 대응을 잴 수 없다.");
                    return false;
                }

                if (!access.ClusterIdByMineTile.TryGetValue(mineTile, out int clusterId) ||
                    !access.ClusterIdByMineTile.TryGetValue(rotatedTile, out int rotatedClusterId))
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "중립 광산 " + mineTile + " 의 회전 대응 자리 " + rotatedTile + " 에 광산이 없다.");
                    return false;
                }

                int blueToMine = access.BlueAccessDistances[clusterId];
                int redToMine = access.RedAccessDistances[clusterId];
                int blueToRotated = access.BlueAccessDistances[rotatedClusterId];
                int redToRotated = access.RedAccessDistances[rotatedClusterId];

                if (blueToMine != redToRotated || redToMine != blueToRotated)
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "교차 접근 거리 위반(광산 " + mineTile + "): Blue " + blueToMine + " / Red " + redToMine +
                        " vs 회전 자리 " + rotatedTile + " Blue " + blueToRotated + " / Red " + redToRotated + ".");
                    return false;
                }

                int geometricBlueToMine = GetGeometricDistance(definition, blueCastleTile, mineTile);
                int geometricRedToMine = GetGeometricDistance(definition, redCastleTile, mineTile);
                int geometricBlueToRotated = GetGeometricDistance(definition, blueCastleTile, rotatedTile);
                int geometricRedToRotated = GetGeometricDistance(definition, redCastleTile, rotatedTile);

                if (geometricBlueToMine != geometricRedToRotated ||
                    geometricRedToMine != geometricBlueToRotated)
                {
                    failure = MapValidationResult.Reject(checkNumber,
                        "기하 교차 거리 위반(광산 " + mineTile + "): Blue " + geometricBlueToMine +
                        " / Red " + geometricRedToMine + " vs 회전 자리 " + rotatedTile + " Blue " +
                        geometricBlueToRotated + " / Red " + geometricRedToRotated + ".");
                    return false;
                }
            }

            failure = default;
            return true;
        }

        /// <summary>
        /// 장애물을 무시한 두 타일 사이의 기하학적 헥스 거리를 잰다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="fromTileIndex">시작 타일</param>
        /// <param name="toTileIndex">끝 타일</param>
        /// <returns>큐브 좌표 기준 거리</returns>
        private static int GetGeometricDistance(MapDefinition definition, int fromTileIndex, int toTileIndex)
        {
            HexCoord from = InitialMapStateEvaluator.OffsetToCube(
                definition.ToCol(fromTileIndex), definition.ToRow(fromTileIndex));
            HexCoord to = InitialMapStateEvaluator.OffsetToCube(
                definition.ToCol(toTileIndex), definition.ToRow(toTileIndex));

            return HexCoord.Distance(from, to);
        }

        // ====================================================================
        // 검증 6 — 실제 초기 골드
        // ====================================================================

        /// <summary>
        /// 실제 초기 골드가 그 시점 모드에 규칙 3이 지정한 값과 같은지 확인한다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="failure">실패 결과(통과 시 기본값)</param>
        /// <returns>통과하면 true</returns>
        private static bool TryCheckInitialGold(MapDefinition definition, out MapValidationResult failure)
        {
            const int checkNumber = 6;

            int expected = GetExpectedInitialGold(definition.TestModeFlag, definition.NeutralMineCount);
            if (expected < 0)
            {
                failure = MapValidationResult.Reject(checkNumber,
                    "규칙 3이 값을 정할 수 없는 입력이다(테스트 모드 표식 " + definition.TestModeFlag +
                    ", 중립 광산 수 " + definition.NeutralMineCount + ").");
                return false;
            }

            if (definition.InitialGold != expected)
            {
                failure = MapValidationResult.Reject(checkNumber,
                    "초기 골드 " + definition.InitialGold + " 가 규칙 3이 지정한 " + expected + " 와 다르다(" +
                    (definition.TestModeFlag == TestModeFlag ? "테스트 모드" : "정상 모드") + ", 중립 광산 " +
                    definition.NeutralMineCount + "개).");
                return false;
            }

            failure = default;
            return true;
        }

        // ====================================================================
        // 공통 도우미
        // ====================================================================

        /// <summary>
        /// 타일 인덱스의 180도 회전 자리를 구한다. 회전 상대가 격자 밖이면 false.
        /// 회전식은 SymmetricMapBuilder.RotateCoord 가 단일 소스이며 여기서 다시 구현하지 않는다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="tileIndex">원본 타일 인덱스</param>
        /// <param name="rotatedTileIndex">회전 자리의 타일 인덱스</param>
        /// <returns>회전 상대가 격자 안이면 true</returns>
        private static bool TryGetRotatedIndex(MapDefinition definition, int tileIndex,
            out int rotatedTileIndex)
        {
            rotatedTileIndex = -1;
            if (!definition.IsValidIndex(tileIndex)) return false;

            SymmetricMapBuilder.RotateCoord(definition.ToCol(tileIndex), definition.ToRow(tileIndex),
                definition.Width, definition.Height, out int rotatedCol, out int rotatedRow);

            if (!SymmetricMapBuilder.IsInsideGrid(rotatedCol, rotatedRow, definition.Width, definition.Height))
            {
                return false;
            }

            rotatedTileIndex = definition.ToIndex(rotatedCol, rotatedRow);
            return true;
        }

        // ====================================================================
        // 자기 검증 — 이 프로젝트에는 유닛 테스트 어셈블리가 없다
        // ====================================================================
        //
        // 🔴 검증 항목마다 「음성 대조군」이 반드시 있다. 통과 케이스만 확인하면
        //    검증기가 아무것도 안 해도 통과하기 때문이다. 아래는 각 검증이
        //    "정확히 그 사유로" 실패하는지를 하나씩 확인한다.
        //
        // 🔴 5번의 음성 대조는 「전체 파이프라인」이 아니라 5번 단계를 직접 부른다.
        //    타일 상태가 대칭이면 이동 그래프도 대칭이므로(참 180도 회전이 인접성을
        //    보존한다), 5번을 깨뜨리는 입력은 반드시 1번에도 걸린다. 그래서 5번을
        //    파이프라인으로 시험하면 1번이 먼저 잡아 5번 코드가 한 줄도 안 돌아간다.
        //    5번은 「1번이 뚫렸을 때를 위한 두 번째 방어선」이며, 그 사실을 여기에 적어 둔다.

        /// <summary> 자기 검증에 쓰는 시드. 값 자체에 의미는 없고 재현성만 있으면 된다. </summary>
        public const ulong SelfCheckRootSeed = 20260904UL;

        /// <summary>
        /// 검증기가 규칙 13대로 동작하는지 양성·음성 양쪽으로 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            // ── P0. 생성기가 만든 맵이 그대로 통과하는가(양성 대조) ─────────
            var openGenerator = new OpenGenerator();
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition openMap,
                    out IMapArchetypeConstraints openConstraints, out failureReason))
            {
                return false;
            }

            MapValidationResult result = Validate(openMap, openConstraints, openGenerator);
            if (!result.IsPassed)
            {
                failureReason = "[P0] 완전개방형 원본이 통과하지 못했다: " + result;
                return false;
            }

            // ── N1. 크기가 규격과 다른 맵 -> 전제에서 실패 ──────────────────
            var wrongSizeMap = new MapDefinition(RequiredWidth - 1, RequiredHeight - 1);
            if (!ExpectFailure(Validate(wrongSizeMap, openConstraints, openGenerator),
                    MapValidationOutcome.RejectAttempt, PrerequisiteCheckNumber, "N1 크기 위반", out failureReason))
            {
                return false;
            }

            // ── N2. 짝 없는 6칸 중 하나를 열었다 -> 1번에서 실패 ────────────
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition n2Map,
                    out IMapArchetypeConstraints n2Constraints, out failureReason))
            {
                return false;
            }
            n2Map.Tiles[new SymmetricMapBuilder().UnpairedTileIndices[0]] = TileKind.Normal;
            if (!ExpectFailure(Validate(n2Map, n2Constraints, openGenerator),
                    MapValidationOutcome.RejectAttempt, 1, "N2 짝 없는 칸 개방", out failureReason))
            {
                return false;
            }

            // ── N3. 대응쌍 한 곳의 타일 상태를 어긋나게 했다 -> 1번에서 실패 ─
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition n3Map,
                    out IMapArchetypeConstraints n3Constraints, out failureReason))
            {
                return false;
            }
            n3Map.Tiles[n3Map.ToIndex(2, 5)] = TileKind.Blocked;
            if (!ExpectFailure(Validate(n3Map, n3Constraints, openGenerator),
                    MapValidationOutcome.RejectAttempt, 1, "N3 대응쌍 불일치", out failureReason))
            {
                return false;
            }

            // ── N4. 필수 통로를 규정 폭 미만으로 좁혔다 -> 2번에서 다시 뽑기 ─
            if (!TryRunCorridorNegativeChecks(out failureReason)) return false;

            // ── N6. 즉시 건설 가능 타일 하나를 막았다 -> 3번에서 실패 ───────
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition n6Map,
                    out IMapArchetypeConstraints n6Constraints, out failureReason))
            {
                return false;
            }
            {
                var evaluator = new InitialMapStateEvaluator(n6Map);
                int target = -1;
                foreach (int index in evaluator.GetUniqueBuildableTiles(TeamId.Blue))
                {
                    if (target < 0 || index < target) target = index;
                }
                if (target < 0)
                {
                    failureReason = "[N6] Blue 고유 건설 가능 타일이 하나도 없어 대조군을 만들 수 없다.";
                    return false;
                }

                TryGetRotatedIndex(n6Map, target, out int rotatedTarget);
                n6Map.Tiles[target] = TileKind.Blocked;
                n6Map.Tiles[rotatedTarget] = TileKind.Blocked;

                if (!ExpectFailure(Validate(n6Map, n6Constraints, openGenerator),
                        MapValidationOutcome.RejectAttempt, 3, "N6 건설 가능 타일 봉쇄", out failureReason))
                {
                    return false;
                }
            }

            // ── N7 · P10 · N8 — 광산 덩어리와 접근 거리 ─────────────────────
            if (!TryRunClusterChecks(out failureReason)) return false;

            // ── N9. 초기 골드가 규칙 3 값과 다르다 -> 6번에서 실패 ──────────
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition n9Map,
                    out IMapArchetypeConstraints n9Constraints, out failureReason))
            {
                return false;
            }
            n9Map.InitialGold += 100;
            if (!ExpectFailure(Validate(n9Map, n9Constraints, openGenerator),
                    MapValidationOutcome.RejectAttempt, 6, "N9 초기 골드 불일치", out failureReason))
            {
                return false;
            }

            // ── N9b. 테스트 모드인데 정상 모드 표 값을 그대로 뒀다 -> 6번 실패 ─
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition n9bMap,
                    out IMapArchetypeConstraints n9bConstraints, out failureReason))
            {
                return false;
            }
            n9bMap.TestModeFlag = TestModeFlag;
            if (!ExpectFailure(Validate(n9bMap, n9bConstraints, openGenerator),
                    MapValidationOutcome.RejectAttempt, 6, "N9b 테스트 모드 표식만 켬", out failureReason))
            {
                return false;
            }

            // ── P9c. 테스트 모드 + 5000 은 광산 수와 무관하게 통과해야 한다 ─
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition p9cMap,
                    out IMapArchetypeConstraints p9cConstraints, out failureReason))
            {
                return false;
            }
            p9cMap.TestModeFlag = TestModeFlag;
            p9cMap.InitialGold = TestModeInitialGold;
            result = Validate(p9cMap, p9cConstraints, openGenerator);
            if (!result.IsPassed)
            {
                failureReason = "[P9c] 테스트 모드 5000 이 통과하지 못했다: " + result;
                return false;
            }

            // ── 규칙 3 표 자체 대조 ─────────────────────────────────────────
            int[] expectedGold = { 700, 600, 500, 400, 300, 200 };
            for (int mineCount = MinNeutralMineCount; mineCount <= MaxNeutralMineCount; mineCount++)
            {
                int actual = GetExpectedInitialGold(NormalModeFlag, mineCount);
                if (actual != expectedGold[mineCount - MinNeutralMineCount])
                {
                    failureReason = "[규칙 3 표] 중립 광산 " + mineCount + "개의 초기 골드가 " +
                        actual + " 다(기대 " + expectedGold[mineCount - MinNeutralMineCount] + ").";
                    return false;
                }

                if (GetExpectedInitialGold(TestModeFlag, mineCount) != TestModeInitialGold)
                {
                    failureReason = "[규칙 3 표] 테스트 모드인데 중립 광산 " + mineCount +
                        "개에서 " + TestModeInitialGold + " 이 아니다.";
                    return false;
                }
            }

            // ── 다섯 유형 전부가 실제로 통과하는가(양성 대조, 통합 검사) ────
            if (!TryRunAllArchetypeChecks(out failureReason)) return false;

            failureReason = null;
            return true;
        }

        /// <summary>
        /// N4 · N5 · N12 · N13 · N14 — 필수 통로 검증의 다섯 가지 음성 대조군.
        /// 규정 폭 미만 · 같은 단계 광산 2개 · 「정확히 3」 위반 ·
        /// 타일이 0개인 높이 단계 · 폭을 재는 두 단계에 광산 2개를 각각 확인한다.
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 기대대로면 true</returns>
        private static bool TryRunCorridorNegativeChecks(out string failureReason)
        {
            var canyonGenerator = new CanyonGenerator();

            // ── N4. 협곡 통로를 가운데 열 하나로 좁혀 규정 폭을 무너뜨린다 ──
            //    통로에서 가운데 열(5열)만 남기고 나머지를 전부 막는다. 5열은 홀수 열이라
            //    홀수 높이 단계에만 존재하므로, 남은 단계들은 짝을 이룰 이웃 단계가 없어
            //    단면 폭이 1 이 된다(규정 폭 3 미만).
            //    🔴 「5열만 남긴다」는 회전에 대해 닫혀 있다(회전은 열 c 를 10-c 로 보내므로
            //       5열은 자기 자신이고 나머지도 나머지로 간다). 그래서 검증 1번은 그대로 통과하고
            //       이 대조군이 정확히 2번만 건드린다.
            if (!TryGenerateForSelfCheck(canyonGenerator, 2, out MapDefinition n4Map,
                    out IMapArchetypeConstraints n4Constraints, out failureReason))
            {
                return false;
            }

            var removed = new HashSet<int>();
            IReadOnlyList<MapCorridorRequirement> canyonCorridors = n4Constraints.RequiredCorridors;
            for (int i = 0; i < canyonCorridors.Count; i++)
            {
                IReadOnlyList<int> corridorTiles = canyonCorridors[i].TileIndices;
                for (int t = 0; t < corridorTiles.Count; t++)
                {
                    int index = corridorTiles[t];
                    if (n4Map.ToCol(index) == MapBandTable.CenterCol) continue;
                    removed.Add(index);
                }
            }

            foreach (int index in removed)
            {
                n4Map.Tiles[index] = TileKind.Blocked;
            }

            IMapArchetypeConstraints n4Narrowed = CreateCorridorOnlyConstraints(n4Map, n4Constraints, removed, -1);
            if (!ExpectFailure(Validate(n4Map, n4Narrowed, canyonGenerator),
                    MapValidationOutcome.Resample, 2, "N4 통로 폭 미달", out failureReason))
            {
                return false;
            }

            // ── N5. 같은 통로·같은 높이 단계에 광산 2개 ─────────────────────
            //    광산 금지 구역 가지와 분리해서 보려고 금지 구역이 없는 제약을 쓴다.
            if (!TryGenerateForSelfCheck(canyonGenerator, 2, out MapDefinition n5Map,
                    out IMapArchetypeConstraints n5Constraints, out failureReason))
            {
                return false;
            }

            var twoMines = new HashSet<int>();
            AddTileAndRotation(n5Map, 4, 10, twoMines);
            AddTileAndRotation(n5Map, 6, 10, twoMines);

            n5Map.NeutralMines.Clear();
            foreach (int index in twoMines)
            {
                n5Map.NeutralMines.Add(index);
            }
            n5Map.NeutralMines.Sort();
            n5Map.NeutralMineCount = n5Map.NeutralMines.Count;
            n5Map.InitialGold = GetExpectedInitialGold(n5Map.TestModeFlag, n5Map.NeutralMineCount);

            IMapArchetypeConstraints n5Free = CreateCorridorOnlyConstraints(n5Map, n5Constraints, null, -1);
            if (!ExpectFailure(Validate(n5Map, n5Free, canyonGenerator),
                    MapValidationOutcome.Resample, 2, "N5 같은 단계 광산 2개", out failureReason))
            {
                return false;
            }

            // ── N12. 3갈래형의 「정확히 3」 위반 — 레인 폭을 4로 넓힌다 ─────
            var threeLaneGenerator = new ThreeLaneGenerator();
            if (!TryGenerateForSelfCheck(threeLaneGenerator, 2, out MapDefinition n12Map,
                    out IMapArchetypeConstraints n12Constraints, out failureReason))
            {
                return false;
            }

            // 왼쪽 벽(3열)과 그 회전 자리인 오른쪽 벽(7열)을 함께 열어야 대칭이 유지된다.
            var widened = new HashSet<int>();
            for (int row = 0; row < n12Map.Height; row++)
            {
                int index = n12Map.ToIndex(ThreeLaneGenerator.LeftWallCol, row);
                if (n12Map.Tiles[index] != TileKind.Blocked) continue;

                widened.Add(index);
                if (TryGetRotatedIndex(n12Map, index, out int rotated)) widened.Add(rotated);
            }

            foreach (int index in widened)
            {
                n12Map.Tiles[index] = TileKind.NoBuild;
            }

            // 넓힌 3열 칸들을 가운데 레인 통로에 더한다(가운데 레인이 폭 4가 된다).
            IMapArchetypeConstraints n12Widened = CreateCorridorOnlyConstraints(
                n12Map, n12Constraints, null, ThreeLaneGenerator.CenterLaneIndex, widened,
                ThreeLaneGenerator.LeftWallCol);

            if (!ExpectFailure(Validate(n12Map, n12Widened, threeLaneGenerator),
                    MapValidationOutcome.Resample, 2, "N12 레인 폭이 정확히 3이 아님", out failureReason))
            {
                return false;
            }

            // ── N13. 회전 대응쌍인 두 행의 짝수 열만 막아 그 두 행을 폭 1 로 만든다 ─
            //
            //    🔴 이 대조군이 지키는 것: 「타일이 하나도 없는 높이 단계」가 검사에서
            //       통째로 빠지지 않는다는 것.
            //       높이 단계 18 은 9행의 짝수 열, 24 는 12행의 짝수 열이다. 두 단계는
            //       42에서 뺀 회전 상대끼리라(18+24=42) 짝수 열을 함께 막으면 회전 대칭이
            //       그대로 유지된다 — 그래서 검증 1번이 아니라 정확히 2번만 시험한다.
            //       (대칭이 깨졌다면 아래 ExpectFailure 가 「검증 1번」을 받아 실패한다.
            //        즉 이 대조군은 대칭 여부까지 스스로 확인하는 셈이다.)
            //       중앙 대역(18~24)은 어떤 분리 길이의 대역에도 들어가므로(MapBandTable
            //       자기 검증 [4]) 3갈래형의 레인 대역 안쪽 단계를 고르는 것이 된다.
            //
            //       막고 나면 9행과 12행에는 홀수 열 한 칸만 남아 그 줄의 폭이 1 이 된다.
            //       종전 코드는 타일이 0개가 된 18·24 단계를 아예 훑지 않아 이 병목을
            //       놓쳤다(남은 단계들은 저마다 폭 3 으로 멀쩡해 보인다).
            if (!TryGenerateForSelfCheck(threeLaneGenerator, 2, out MapDefinition n13Map,
                    out IMapArchetypeConstraints n13Constraints, out failureReason))
            {
                return false;
            }

            var emptiedSteps = new HashSet<int>();
            IReadOnlyList<MapCorridorRequirement> laneCorridors = n13Constraints.RequiredCorridors;
            for (int i = 0; i < laneCorridors.Count; i++)
            {
                IReadOnlyList<int> laneTiles = laneCorridors[i].TileIndices;
                for (int t = 0; t < laneTiles.Count; t++)
                {
                    int index = laneTiles[t];
                    int step = MapBandTable.GetHeightStep(n13Map.ToCol(index), n13Map.ToRow(index));

                    if (step != MapBandTable.CentralBandMinHeightStep &&
                        step != MapBandTable.CentralBandMaxHeightStep)
                    {
                        continue;
                    }

                    emptiedSteps.Add(index);
                }
            }

            if (emptiedSteps.Count == 0)
            {
                failureReason = "[N13] 비울 높이 단계에 레인 타일이 하나도 없어 대조군을 만들 수 없다.";
                return false;
            }

            foreach (int index in emptiedSteps)
            {
                n13Map.Tiles[index] = TileKind.Blocked;
            }

            IMapArchetypeConstraints n13Emptied =
                CreateCorridorOnlyConstraints(n13Map, n13Constraints, emptiedSteps, -1);

            if (!ExpectFailure(Validate(n13Map, n13Emptied, threeLaneGenerator),
                    MapValidationOutcome.Resample, 2, "N13 타일이 0개인 높이 단계", out failureReason))
            {
                return false;
            }

            // ── N14. 한 줄에 회전 대칭으로 광산 2개 ─────────────────────────
            //
            //    🔴 이 대조군이 지키는 것: 광산을 「폭을 재는 두 단계」에서 함께 센다는 것.
            //       (4,10) 은 높이 단계 20, (5,10) 은 21 이다. 폭은 이 두 단계를 더해서 재므로
            //       광산도 같은 두 단계에서 세야 한다. 종전처럼 한 단계에서만 세면 두 광산이
            //       서로 다른 단계에 하나씩 걸려 「단계마다 1개」로 보이고 그대로 통과한다.
            //       실제로는 10행의 4·5·6열 중 둘이 광산이라 통행 폭이 1 까지 좁아진 상태다
            //       (규칙 문서 3장 「폭 3 통로는 광산 1개면 2레인, 2개면 1레인」).
            //
            //       (5,10) 은 회전 중심이라 자기 자신이 회전 상대이고, (4,10) 의 상대는
            //       (6,11) 이다. 그래서 이 세 칸은 회전 대칭이며 검증 1번에 걸리지 않는다.
            //       광산 금지 구역 가지와 분리해서 보려고 N5 와 같이 금지 구역이 없는 제약을 쓴다.
            if (!TryGenerateForSelfCheck(canyonGenerator, 2, out MapDefinition n14Map,
                    out IMapArchetypeConstraints n14Constraints, out failureReason))
            {
                return false;
            }

            var pairedStepMines = new HashSet<int>();
            AddTileAndRotation(n14Map, 4, 10, pairedStepMines);
            AddTileAndRotation(n14Map, MapBandTable.CenterCol, MapBandTable.CenterRow, pairedStepMines);

            n14Map.NeutralMines.Clear();
            foreach (int index in pairedStepMines)
            {
                n14Map.NeutralMines.Add(index);
            }
            n14Map.NeutralMines.Sort();
            n14Map.NeutralMineCount = n14Map.NeutralMines.Count;
            n14Map.InitialGold = GetExpectedInitialGold(n14Map.TestModeFlag, n14Map.NeutralMineCount);

            IMapArchetypeConstraints n14Free = CreateCorridorOnlyConstraints(n14Map, n14Constraints, null, -1);
            if (!ExpectFailure(Validate(n14Map, n14Free, canyonGenerator),
                    MapValidationOutcome.Resample, 2, "N14 폭을 재는 두 단계에 광산 2개", out failureReason))
            {
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// N7 · P10 · N8 — 광산 덩어리와 접근 거리의 대조군.
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 기대대로면 true</returns>
        private static bool TryRunClusterChecks(out string failureReason)
        {
            var openGenerator = new OpenGenerator();

            // 대조군에 쓸 자리: 성과 멀리 떨어진 (1,10) 과 그 회전 자리.
            const int mineCol = 1;
            const int mineRow = 10;

            // ── N7. 광산 덩어리를 막힌 타일로 완전히 둘러쌌다 -> 4번에서 실패 ─
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition n7Map,
                    out IMapArchetypeConstraints n7Constraints, out failureReason))
            {
                return false;
            }

            int mineTile = n7Map.ToIndex(mineCol, mineRow);
            SetNeutralMinePairForSelfCheck(n7Map, mineTile);

            var buffer = new int[InitialMapStateEvaluator.MaxNeighborCount];
            int neighborCount = InitialMapStateEvaluator.GetNeighborIndices(
                mineTile, n7Map.Width, n7Map.Height, buffer);

            for (int i = 0; i < neighborCount; i++)
            {
                n7Map.Tiles[buffer[i]] = TileKind.Blocked;
                if (TryGetRotatedIndex(n7Map, buffer[i], out int rotated))
                {
                    n7Map.Tiles[rotated] = TileKind.Blocked;
                }
            }

            if (!ExpectFailure(Validate(n7Map, n7Constraints, openGenerator),
                    MapValidationOutcome.RejectAttempt, 4, "N7 덩어리 완전 봉쇄", out failureReason))
            {
                return false;
            }

            // ── P10. 덩어리 양성 대조 ───────────────────────────────────────
            //    광산 A 는 막힌 타일과 광산 B 로만 둘러싸였지만 광산 B 에는 접근 칸이 있다.
            //    광산 하나 기준으로 재면 잘못 실패하고, 덩어리 기준으로 재면 통과한다.
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition p10Map,
                    out IMapArchetypeConstraints p10Constraints, out failureReason))
            {
                return false;
            }

            int tileA = p10Map.ToIndex(mineCol, mineRow);
            TryGetRotatedIndex(p10Map, tileA, out int rotatedA);

            neighborCount = InitialMapStateEvaluator.GetNeighborIndices(
                tileA, p10Map.Width, p10Map.Height, buffer);

            int tileB = -1;
            for (int i = 0; i < neighborCount; i++)
            {
                if (buffer[i] == rotatedA) continue;
                tileB = buffer[i];
                break;
            }

            if (tileB < 0)
            {
                failureReason = "[P10] 광산 B 로 쓸 이웃 칸을 찾지 못했다.";
                return false;
            }

            p10Map.NeutralMines.Clear();
            AddNeutralMinePair(p10Map, tileA);
            AddNeutralMinePair(p10Map, tileB);
            p10Map.NeutralMines.Sort();
            p10Map.NeutralMineCount = p10Map.NeutralMines.Count;
            p10Map.InitialGold = GetExpectedInitialGold(p10Map.TestModeFlag, p10Map.NeutralMineCount);

            neighborCount = InitialMapStateEvaluator.GetNeighborIndices(
                tileA, p10Map.Width, p10Map.Height, buffer);

            for (int i = 0; i < neighborCount; i++)
            {
                if (buffer[i] == tileB) continue;
                if (p10Map.NeutralMines.Contains(buffer[i])) continue;

                p10Map.Tiles[buffer[i]] = TileKind.Blocked;
                if (TryGetRotatedIndex(p10Map, buffer[i], out int rotated))
                {
                    p10Map.Tiles[rotated] = TileKind.Blocked;
                }
            }

            MapValidationResult p10Result = Validate(p10Map, p10Constraints, openGenerator);
            if (!p10Result.IsPassed)
            {
                failureReason = "[P10] 덩어리 양성 대조가 통과하지 못했다(광산 하나 기준으로 재고 있을 수 있다): " +
                    p10Result;
                return false;
            }

            // ── N8. 광산을 회전 대응 자리에서 옮겼다 -> 5번 단계에서 실패 ───
            //    파이프라인으로는 1번이 먼저 잡으므로 두 가지를 모두 확인한다.
            if (!TryGenerateForSelfCheck(openGenerator, 2, out MapDefinition n8Map,
                    out IMapArchetypeConstraints n8Constraints, out failureReason))
            {
                return false;
            }

            int movedBase = n8Map.ToIndex(mineCol, mineRow);
            SetNeutralMinePairForSelfCheck(n8Map, movedBase);
            n8Map.NeutralMines.Add(n8Map.ToIndex(mineCol, mineRow - 1));   // 회전 상대가 없는 광산 하나
            n8Map.NeutralMines.Sort();
            n8Map.NeutralMineCount = n8Map.NeutralMines.Count;
            n8Map.InitialGold = GetExpectedInitialGold(n8Map.TestModeFlag, n8Map.NeutralMineCount);

            if (!ExpectFailure(Validate(n8Map, n8Constraints, openGenerator),
                    MapValidationOutcome.RejectAttempt, 1, "N8 광산 이동(파이프라인)", out failureReason))
            {
                return false;
            }

            var n8State = new InitialMapStateEvaluator(n8Map);
            if (!TryBuildAccessContext(n8Map, n8State, out MapAccessContext n8Access,
                    out MapValidationResult n8AccessFailure))
            {
                failureReason = "[N8] 5번 단계를 시험하려 했는데 4번에서 먼저 걸렸다: " + n8AccessFailure;
                return false;
            }

            TryCheckCrossDistances(n8Map, n8Access, out MapValidationResult n8CrossFailure);
            if (n8CrossFailure.Outcome != MapValidationOutcome.RejectAttempt || n8CrossFailure.FailedCheckNumber != 5)
            {
                failureReason = "[N8] 5번 단계가 기대대로 실패하지 않았다: " + n8CrossFailure;
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 다섯 유형 전부가 실제로 검증을 통과하는지 확인한다(양성 대조).
        /// 유형별로 허용하는 중립 광산 개수 전부와 시작 광산 경우 A/B 를 모두 돌린다.
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        private static bool TryRunAllArchetypeChecks(out string failureReason)
        {
            IMapArchetypeGenerator[] generators =
            {
                new OpenGenerator(),
                new ObstacleOpenGenerator(),
                new CanyonGenerator(),
                new OuterGenerator(),
                new ThreeLaneGenerator()
            };

            for (int g = 0; g < generators.Length; g++)
            {
                IMapArchetypeGenerator generator = generators[g];

                for (int mineCount = generator.MinNeutralMineCount;
                     mineCount <= generator.MaxNeutralMineCount;
                     mineCount++)
                {
                    if (!generator.IsNeutralMineCountAllowed(mineCount)) continue;

                    for (int sideIndex = 0; sideIndex < 2; sideIndex++)
                    {
                        var request = new MapGenerationRequest
                        {
                            MapVersion = MapDefinition.CurrentMapVersion,
                            RootSeed = SelfCheckRootSeed,
                            AttemptIndex = 0,
                            StartingMineSide = sideIndex == 0
                                ? MapStartingMineSide.CaseA
                                : MapStartingMineSide.CaseB,
                            NeutralMineCount = mineCount,
                            TestModeFlag = NormalModeFlag,
                            InitialGold = GetExpectedInitialGold(NormalModeFlag, mineCount)
                        };

                        MapGenerationResult generated = generator.Generate(request);
                        if (!generated.IsAccepted)
                        {
                            failureReason = "[통합] " + generator.MapType + " (광산 " + mineCount +
                                "개, 경우 " + request.StartingMineSide + ") 생성이 거부됐다: " +
                                generated.RejectionReason;
                            return false;
                        }

                        MapValidationResult validated =
                            Validate(generated.Definition, generated.Constraints, generator);

                        if (!validated.IsPassed)
                        {
                            failureReason = "[통합] " + generator.MapType + " (광산 " + mineCount +
                                "개, 경우 " + request.StartingMineSide + ") 이 검증을 통과하지 못했다: " +
                                validated;
                            return false;
                        }
                    }
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 자기 검증용으로 맵을 한 판 만든다. 생성 자체가 거부되면 사유를 채우고 false.
        /// </summary>
        /// <param name="generator">쓸 생성기</param>
        /// <param name="neutralMineCount">중립 광산 개수</param>
        /// <param name="definition">만들어진 맵 정의</param>
        /// <param name="constraints">그 시도의 유형별 제약</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>만들어졌으면 true</returns>
        private static bool TryGenerateForSelfCheck(IMapArchetypeGenerator generator, int neutralMineCount,
            out MapDefinition definition, out IMapArchetypeConstraints constraints, out string failureReason)
        {
            var request = new MapGenerationRequest
            {
                MapVersion = MapDefinition.CurrentMapVersion,
                RootSeed = SelfCheckRootSeed,
                AttemptIndex = 0,
                StartingMineSide = MapStartingMineSide.CaseA,
                NeutralMineCount = neutralMineCount,
                TestModeFlag = NormalModeFlag,
                InitialGold = GetExpectedInitialGold(NormalModeFlag, neutralMineCount)
            };

            MapGenerationResult result = generator.Generate(request);
            if (!result.IsAccepted)
            {
                definition = null;
                constraints = null;
                failureReason = "자기 검증용 맵 생성이 거부됐다(" + generator.MapType + "): " + result.RejectionReason;
                return false;
            }

            definition = result.Definition;
            constraints = result.Constraints;
            failureReason = null;
            return true;
        }

        /// <summary>
        /// 결과가 기대한 처리 방식·검증 번호로 실패했는지 확인한다.
        /// </summary>
        /// <param name="result">확인할 결과</param>
        /// <param name="expectedOutcome">기대한 처리 방식</param>
        /// <param name="expectedCheckNumber">기대한 검증 번호</param>
        /// <param name="label">실패 메시지에 쓸 이름</param>
        /// <param name="failureReason">실패 사유(기대대로면 null)</param>
        /// <returns>기대대로면 true</returns>
        private static bool ExpectFailure(MapValidationResult result, MapValidationOutcome expectedOutcome,
            int expectedCheckNumber, string label, out string failureReason)
        {
            if (result.Outcome != expectedOutcome || result.FailedCheckNumber != expectedCheckNumber)
            {
                failureReason = "[" + label + "] 기대한 실패가 나오지 않았다(기대 " + expectedOutcome +
                    " / 검증 " + expectedCheckNumber + "번, 실제 " + result + ").";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary> 자기 검증용 — 좌표와 그 회전 자리를 집합에 함께 넣는다. </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="results">결과를 담을 집합</param>
        private static void AddTileAndRotation(MapDefinition definition, int col, int row, HashSet<int> results)
        {
            int index = definition.ToIndex(col, row);
            results.Add(index);
            if (TryGetRotatedIndex(definition, index, out int rotated)) results.Add(rotated);
        }

        /// <summary> 자기 검증용 — 중립 광산을 대응쌍으로 더한다(목록은 비우지 않는다). </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="tileIndex">원본 타일</param>
        private static void AddNeutralMinePair(MapDefinition definition, int tileIndex)
        {
            if (!definition.NeutralMines.Contains(tileIndex)) definition.NeutralMines.Add(tileIndex);
            if (TryGetRotatedIndex(definition, tileIndex, out int rotated) &&
                !definition.NeutralMines.Contains(rotated))
            {
                definition.NeutralMines.Add(rotated);
            }
        }

        /// <summary> 자기 검증용 — 중립 광산 목록을 대응쌍 하나로 갈아 끼운다. </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="tileIndex">원본 타일</param>
        private static void SetNeutralMinePairForSelfCheck(MapDefinition definition, int tileIndex)
        {
            definition.NeutralMines.Clear();
            AddNeutralMinePair(definition, tileIndex);
            definition.NeutralMines.Sort();
            definition.NeutralMineCount = definition.NeutralMines.Count;
            definition.InitialGold = GetExpectedInitialGold(definition.TestModeFlag, definition.NeutralMineCount);
        }

        /// <summary>
        /// 자기 검증용 — 원래 제약의 필수 통로만 옮겨 담은 새 제약을 만든다.
        /// 금지 구역은 IMapArchetypeConstraints 로 되읽을 수 없으므로 비운 채로 만든다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="source">원래 제약</param>
        /// <param name="removedTiles">통로에서 빼야 하는 타일(없으면 null)</param>
        /// <param name="corridorIndexToWiden">타일을 더할 통로 번호(없으면 -1)</param>
        /// <returns>새 제약</returns>
        private static IMapArchetypeConstraints CreateCorridorOnlyConstraints(MapDefinition definition,
            IMapArchetypeConstraints source, HashSet<int> removedTiles, int corridorIndexToWiden)
        {
            return CreateCorridorOnlyConstraints(definition, source, removedTiles, corridorIndexToWiden, null, -1);
        }

        /// <summary>
        /// 자기 검증용 — 원래 제약의 필수 통로를 손봐 새 제약을 만든다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="source">원래 제약</param>
        /// <param name="removedTiles">모든 통로에서 빼야 하는 타일(없으면 null)</param>
        /// <param name="corridorIndexToWiden">타일을 더할 통로 번호(없으면 -1)</param>
        /// <param name="addedTiles">그 통로에 더할 타일(없으면 null)</param>
        /// <param name="addedTileColumn">더할 타일 중 이 열만 쓴다(음수면 전부)</param>
        /// <returns>새 제약</returns>
        private static IMapArchetypeConstraints CreateCorridorOnlyConstraints(MapDefinition definition,
            IMapArchetypeConstraints source, HashSet<int> removedTiles, int corridorIndexToWiden,
            HashSet<int> addedTiles, int addedTileColumn)
        {
            IReadOnlyList<MapCorridorRequirement> sourceCorridors = source.RequiredCorridors;
            var corridors = new List<MapCorridorRequirement>(sourceCorridors.Count);

            for (int i = 0; i < sourceCorridors.Count; i++)
            {
                MapCorridorRequirement corridor = sourceCorridors[i];
                var tiles = new List<int>();

                for (int t = 0; t < corridor.TileIndices.Count; t++)
                {
                    int index = corridor.TileIndices[t];
                    if (removedTiles != null && removedTiles.Contains(index)) continue;
                    tiles.Add(index);
                }

                if (i == corridorIndexToWiden && addedTiles != null)
                {
                    foreach (int index in addedTiles)
                    {
                        if (addedTileColumn >= 0 && definition.ToCol(index) != addedTileColumn) continue;
                        tiles.Add(index);
                    }
                }

                // 높이 단계 대역은 원래 통로의 것을 그대로 물려받는다. 여기서 대역을 좁히면
                // 대조군이 「대역 전체를 훑는다」는 성질까지 함께 지워 버려 시험이 무의미해진다.
                corridors.Add(new MapCorridorRequirement(corridor.Name, tiles,
                    corridor.RequiredWidth, corridor.IsExactWidth,
                    corridor.MinHeightStep, corridor.MaxHeightStep));
            }

            return new MapArchetypeConstraints(source.MapType, definition.Width, null, null, corridors);
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
                throw new InvalidOperationException("MapDefinitionValidator 자기 검증 실패: " + reason);
            }
        }
    }
}
