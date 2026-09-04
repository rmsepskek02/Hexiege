// ============================================================================
// CanyonGenerator.cs
// 협곡형(규칙 6) 맵 생성기.
//
// ─────────────────────────────────────────────────────────────────────────────
// ① 이 유형이 노리는 것
// ─────────────────────────────────────────────────────────────────────────────
//   중앙에 강한 병목을 만들어 정면 충돌을 강제한다. 우회로가 없다.
//   그래서 "가운데를 좁힌다"가 아니라 "가운데 말고는 길이 없다"가 정확한 표현이다.
//
// ─────────────────────────────────────────────────────────────────────────────
// ② 지형이 만들어지는 순서 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   윗절반만 그리고 아랫절반은 180도 회전 복제로 채운다(대응쌍 기록 API).
//
//   1) 중앙 통로 폭을 3 · 5 · 7 중에서 같은 확률로 하나 뽑는다.
//
//   2) 0~2행은 폭 11 로 전부 열려 있다. 성과 시작 영역이 있는 대역이다.
//
//   3) 3~8행은 가운데 열을 중심으로 아래로 갈수록 좁아진다.
//      3행의 폭은 11 이고, 8행에서 뽑은 통로 폭에 도달한다.
//      폭은 11 → 9 → 7 → 5 → 3 처럼 한 번에 2씩만 줄고, 다시 넓어지지 않는다.
//
//   4) 높이 단계 18~24 는 뽑은 통로 폭 그대로 고정된다. 이곳이 병목이다.
//
//   나머지 절반은 회전 복제이므로 결과를 행 번호로 적으면 이렇게 된다.
//      아래쪽 전체 열림 : 짝수 열 19~20행 · 홀수 열 18~20행
//      아래쪽 좁아지는 구간 : 짝수 열 13~18행 · 홀수 열 12~17행
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 왜 병목을 「9~11행」이 아니라 「높이 단계 18~24」로 잡는가
// ─────────────────────────────────────────────────────────────────────────────
//   9~11행은 회전 대칭이 아니다. 그 대역의 한가운데가 맵 한가운데보다 반 칸
//   위로 어긋나기 때문에, 그대로 쓰면 위쪽 진영과 아래쪽 진영의 병목 두께가
//   달라진다. 높이 단계 18~24 는 42에서 빼는 회전식에 대해 닫혀 있고(18과 24,
//   19와 23, 20과 22 가 짝이고 21은 자기 자신), 두께도 「중앙 3개 행」이라는
//   의도와 일치한다. 실제 타일 수는 39칸이다.
//   (짝수 열 9~12행 6칸씩 24칸 + 홀수 열 9~11행 5칸씩 15칸)
//   자세한 설명과 그 39칸의 실측은 MapBandTable 에 있다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 전환 행을 어떻게 고르는가 — 그리고 왜 「구간」을 고르는가
// ─────────────────────────────────────────────────────────────────────────────
//   3행의 폭이 11 이고 8행의 폭이 통로 폭이므로, 줄어드는 일은 3행과 8행 「사이」에서
//   일어난다. 사이의 칸은 3→4 · 4→5 · 5→6 · 6→7 · 7→8 의 다섯 군데다.
//   그중 서로 다른 곳을 (11 - 통로 폭) 나누기 2 개 만큼 균등하게 골라, 고른 곳을
//   지날 때마다 폭을 2 줄인다.
//
//   ⚠️ 규칙 문서는 이 선택 범위를 「3~8행 중」이라고 적는다. 행 6개가 아니라
//      그 사이의 구간 5개를 고르는 것으로 읽어야 두 조건(3행의 폭이 11 · 8행의
//      폭이 통로 폭)을 동시에 지킬 수 있다. 행 6개에서 고르면 8행을 고른 경우
//      8행에서 통로 폭에 도달하지 못한다.
//
//   통로 폭 3 이면 4군데, 5 면 3군데, 7 이면 2군데를 고른다.
//
// ─────────────────────────────────────────────────────────────────────────────
// ③④⑤⑥
// ─────────────────────────────────────────────────────────────────────────────
//   ③ 허용 중립 광산 수 : 1~4
//   ④ 광산 금지 구역   : 높이 단계 18~24 전체. 다만 중립 광산 개수가 홀수일 때
//                        쓰는 회전 중심의 단독 광산 자리만 예외로 허용한다.
//   ⑤ 건설 불가 구역   : 높이 단계 18~24 의 열린 타일 전부
//   ⑥ 필수 통로       : 높이 단계 18~24 를 지나는 통로 1개(규정 폭 3 이상)
//
//   장식은 만들지 않는다(규칙 15). Decoration 스트림에서 한 번도 뽑지 않는다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 6 · 규칙 15
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 협곡형(규칙 6) 생성기. 중앙을 좁은 통로 하나로 조이고 그 밖은 전부 막는다.
    /// </summary>
    public sealed class CanyonGenerator : MapArchetypeGeneratorBase
    {
        // ====================================================================
        // 규칙 6 의 숫자들
        // ====================================================================

        private static readonly int[] CorridorWidthValues = { 3, 5, 7 };

        /// <summary> 중앙 통로 폭 후보. 세 값이 모두 같은 확률로 뽑힌다. </summary>
        public static IReadOnlyList<int> CorridorWidthCandidates => CorridorWidthValues;

        /// <summary> 폭 11 로 전부 열어 두는 위쪽 대역의 마지막 행. </summary>
        public const int OpenRowMax = 2;

        /// <summary> 좁아지는 구간의 첫 행. 이 행의 폭은 언제나 11 이다. </summary>
        public const int NarrowRowMin = 3;

        /// <summary> 좁아지는 구간의 마지막 행. 이 행에서 통로 폭에 도달한다. </summary>
        public const int NarrowRowMax = 8;

        /// <summary> 좁아지는 구간의 시작 폭(격자 전체 폭). </summary>
        public const int NarrowStartWidth = MapBandTable.Width;

        /// <summary>
        /// 폭이 줄어들 수 있는 「행과 행 사이」의 개수. 3~8행 사이의 다섯 군데다.
        /// </summary>
        public const int TransitionStepCount = NarrowRowMax - NarrowRowMin;

        /// <summary> 이 유형이 허용하는 중립 광산 최소 개수(규칙 6 ③). </summary>
        public const int CanyonMinNeutralMineCount = 1;

        /// <summary> 이 유형이 허용하는 중립 광산 최대 개수(규칙 6 ③). </summary>
        public const int CanyonMaxNeutralMineCount = 4;

        /// <summary> 필수 통로의 이름(로그·실패 사유에 쓴다). </summary>
        public const string CorridorName = "중앙 협곡";

        /// <summary> 이 생성기가 만드는 맵 유형. </summary>
        public override MapType MapType => MapType.Canyon;

        /// <summary> 이 유형이 허용하는 중립 광산 최소 개수. </summary>
        public override int MinNeutralMineCount => CanyonMinNeutralMineCount;

        /// <summary> 이 유형이 허용하는 중립 광산 최대 개수. </summary>
        public override int MaxNeutralMineCount => CanyonMaxNeutralMineCount;

        // ====================================================================
        // 지형 그리기
        // ====================================================================

        /// <summary>
        /// 규칙 6대로 협곡을 그린다. 윗절반만 기록하고 아랫절반은 대응쌍 기록 API 가
        /// 자동으로 채운다.
        /// </summary>
        /// <param name="builder">기록 대상 builder(성·시작 광산은 이미 놓여 있다)</param>
        /// <param name="terrainStream">Terrain 스트림</param>
        /// <param name="constraints">이 시도에서 확정된 유형별 제약</param>
        /// <returns>성공이면 null, 거부면 사유</returns>
        protected override string TryApplyTerrain(SymmetricMapBuilder builder, MapRandom terrainStream,
            out IMapArchetypeConstraints constraints)
        {
            constraints = null;

            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (terrainStream == null) throw new ArgumentNullException(nameof(terrainStream));

            // ── 1. 중앙 통로 폭 ─────────────────────────────────────────────
            int corridorWidth = terrainStream.Choose(CorridorWidthValues);

            // ── 2. 좁아지는 구간의 폭 프로파일 ──────────────────────────────
            int[] widthByRow = BuildWidthProfile(terrainStream, corridorWidth, out string profileReason);
            if (widthByRow == null) return profileReason;

            // ── 3. 지형 기록 ────────────────────────────────────────────────
            MapBandTable.ApplySymmetricTerrain(builder,
                (col, row) => GetTileKind(col, row, corridorWidth, widthByRow));

            // ── 4. ④⑤⑥ 제약 ───────────────────────────────────────────────
            constraints = BuildConstraints(builder, corridorWidth);
            return null;
        }

        /// <summary>
        /// 3~8행의 폭 프로파일을 만든다. 3행은 11 로 시작해 8행에서 통로 폭에 도달하며,
        /// 한 번에 2씩만 줄어든다.
        /// </summary>
        /// <param name="terrainStream">Terrain 스트림</param>
        /// <param name="corridorWidth">뽑힌 중앙 통로 폭</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>행 번호로 찾아 쓰는 폭 배열(실패하면 null)</returns>
        private static int[] BuildWidthProfile(MapRandom terrainStream, int corridorWidth,
            out string failureReason)
        {
            // 줄여야 하는 총량이 (11 - 통로 폭) 이고 한 번에 2씩 주므로,
            // 고를 구간의 개수는 그 값을 2로 나눈 수다.
            int transitionCount = (NarrowStartWidth - corridorWidth) / 2;

            var pickedSteps = new List<int>(transitionCount);
            if (!NeutralMineSampler.TryPickDistinctSlots(terrainStream, TransitionStepCount,
                    transitionCount, pickedSteps, out string pickReason))
            {
                failureReason = "좁아지는 구간의 전환 자리를 고르지 못했다: " + pickReason;
                return null;
            }

            // 고른 번호 i 는 「(3 + i)행과 그 다음 행 사이」를 뜻한다.
            var transitionAfterRow = new HashSet<int>();
            for (int i = 0; i < pickedSteps.Count; i++)
            {
                transitionAfterRow.Add(NarrowRowMin + pickedSteps[i]);
            }

            var widthByRow = new int[NarrowRowMax + 1];
            widthByRow[NarrowRowMin] = NarrowStartWidth;

            for (int row = NarrowRowMin + 1; row <= NarrowRowMax; row++)
            {
                widthByRow[row] = widthByRow[row - 1] - (transitionAfterRow.Contains(row - 1) ? 2 : 0);
            }

            failureReason = null;
            return widthByRow;
        }

        /// <summary>
        /// 좌표 하나의 지형 종류를 정한다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="corridorWidth">중앙 통로 폭</param>
        /// <param name="widthByRow">좁아지는 구간의 폭 프로파일</param>
        /// <returns>그 칸에 놓을 지형 종류</returns>
        private static TileKind GetTileKind(int col, int row, int corridorWidth, int[] widthByRow)
        {
            // 병목은 행이 아니라 높이 단계로 판정한다(파일 머리말의 설명 참조).
            if (MapBandTable.IsWithinCentralBand(col, row))
            {
                return MapBandTable.IsWithinWidth(col, corridorWidth) ? TileKind.NoBuild : TileKind.Blocked;
            }

            // 성과 시작 영역이 있는 위쪽 대역은 폭 11 로 전부 열어 둔다.
            if (row <= OpenRowMax) return TileKind.Normal;

            return MapBandTable.IsWithinWidth(col, widthByRow[row]) ? TileKind.Normal : TileKind.Blocked;
        }

        /// <summary>
        /// 이 시도의 ④⑤⑥ 제약을 만든다.
        /// </summary>
        /// <param name="builder">지형이 이미 기록된 builder</param>
        /// <param name="corridorWidth">중앙 통로 폭</param>
        /// <returns>유형별 제약</returns>
        private static IMapArchetypeConstraints BuildConstraints(SymmetricMapBuilder builder, int corridorWidth)
        {
            var mineForbidden = new List<int>();
            var buildForbidden = new List<int>();
            var corridorTiles = new List<int>();

            for (int row = 0; row < builder.Height; row++)
            {
                for (int col = 0; col < builder.Width; col++)
                {
                    if (!MapBandTable.IsWithinCentralBand(col, row)) continue;

                    int index = row * builder.Width + col;

                    // ④ 병목 안에는 광산 대응쌍을 두지 않는다.
                    //    회전 중심만 예외다 — 중립 광산 개수가 홀수일 때 쓰는 단독 자리이며,
                    //    이 자리는 통로 폭이 3 이상이라 언제나 열려 있다.
                    if (!builder.IsCenter(col, row))
                    {
                        mineForbidden.Add(index);
                    }

                    // ⑤⑥ 병목의 열린 타일은 통로이자 건설 불가 구역이다.
                    if (builder.GetTile(col, row) != TileKind.Blocked)
                    {
                        buildForbidden.Add(index);
                        corridorTiles.Add(index);
                    }
                }
            }

            var corridors = new List<MapCorridorRequirement>
            {
                new MapCorridorRequirement(CorridorName + "(폭 " + corridorWidth + ")", corridorTiles)
            };

            return new MapArchetypeConstraints(MapType.Canyon, builder.Width,
                mineForbidden, buildForbidden, corridors);
        }

        // ====================================================================
        // 폭 프로파일 검사 — 자기 검증과 음성 대조가 함께 쓴다
        // ====================================================================

        /// <summary>
        /// 좁아지는 구간의 폭 프로파일이 규칙 6 ② 를 지키는지 확인한다.
        /// 자기 검증과 음성 대조가 같은 함수를 쓴다 — 검사가 진짜로 무엇을 걸러내는지
        /// 확인하려면 「걸러져야 하는 입력」도 같은 문으로 넣어 봐야 하기 때문이다.
        /// </summary>
        /// <param name="widthByRow">행 번호로 찾아 쓰는 폭 배열</param>
        /// <param name="corridorWidth">도달해야 하는 중앙 통로 폭</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>규칙대로면 true</returns>
        public static bool TryVerifyWidthProfile(int[] widthByRow, int corridorWidth, out string failureReason)
        {
            if (widthByRow == null) throw new ArgumentNullException(nameof(widthByRow));

            if (widthByRow.Length <= NarrowRowMax)
            {
                failureReason = "폭 배열이 " + NarrowRowMax + "행을 담지 못한다.";
                return false;
            }

            if (widthByRow[NarrowRowMin] != NarrowStartWidth)
            {
                failureReason = NarrowRowMin + "행의 폭이 " + NarrowStartWidth + " 이 아니다(실제 " +
                    widthByRow[NarrowRowMin] + ").";
                return false;
            }

            if (widthByRow[NarrowRowMax] != corridorWidth)
            {
                failureReason = NarrowRowMax + "행의 폭이 통로 폭 " + corridorWidth + " 이 아니다(실제 " +
                    widthByRow[NarrowRowMax] + ").";
                return false;
            }

            int transitionCount = 0;

            for (int row = NarrowRowMin + 1; row <= NarrowRowMax; row++)
            {
                int drop = widthByRow[row - 1] - widthByRow[row];

                if (drop < 0)
                {
                    failureReason = row + "행에서 폭이 다시 넓어졌다(단조 감소가 아니다).";
                    return false;
                }

                if (drop > 2)
                {
                    failureReason = row + "행의 폭 감소량이 " + drop + " 이다(최대 2).";
                    return false;
                }

                if ((widthByRow[row] & 1) == 0 || widthByRow[row] < 1)
                {
                    failureReason = row + "행의 폭이 1 이상 홀수가 아니다(실제 " + widthByRow[row] + ").";
                    return false;
                }

                if (drop != 0) transitionCount++;
            }

            int expectedTransitionCount = (NarrowStartWidth - corridorWidth) / 2;
            if (transitionCount != expectedTransitionCount)
            {
                failureReason = "전환 행이 " + expectedTransitionCount + "개가 아니다(실제 " +
                    transitionCount + "개).";
                return false;
            }

            failureReason = null;
            return true;
        }

        // ====================================================================
        // 자기 검증 — 이 프로젝트에는 유닛 테스트 어셈블리가 없다
        // ====================================================================
        //
        // 🔴 기대값은 "그렇게 나오면 좋겠다"가 아니라 규칙 문서에 적힌 값이거나
        //    격자 기하에서 그대로 따라 나오는 값이다. 실패하면 검사를 고치지 말고
        //    구현과 규칙 중 어느 쪽이 어긋났는지부터 판단할 것.

        /// <summary> 통계 확인에 쓰는 서로 다른 시드의 개수. </summary>
        public const int SelfCheckSeedCount = 200;

        /// <summary>
        /// 시드 0~199 에서 통로 폭 3 · 5 · 7 이 각각 몇 번 뽑히는지의 기대값.
        /// 이 알고리즘을 파이썬으로 따로 구현해 돌린 실측값이며, 세 값의 합은 200 이다.
        ///
        /// 🔴 이 수가 어긋나면 검사를 고치지 말 것. 뽑기 순서나 확률이 바뀌었다는 뜻이고,
        ///    그러면 과거 시드가 다른 맵을 만들게 되므로 MapVersion 을 올려야 하는지부터 판단한다.
        /// </summary>
        private static readonly int[] SelfCheckExpectedWidthCountValues = { 73, 57, 70 };

        /// <summary> 통로 폭별 기대 등장 횟수(폭 후보 목록과 같은 순서). </summary>
        public static IReadOnlyList<int> SelfCheckExpectedWidthCounts => SelfCheckExpectedWidthCountValues;

        /// <summary>
        /// 시드 0~199 의 건설 불가 타일 총합 기대값.
        ///
        /// 이 값이 어디서 나왔는가: 병목 39칸 중 열린 칸 수는 통로 폭만으로 정해진다.
        ///   폭 3 이면 11칸(짝수 단계 4개 x 2칸 + 홀수 단계 3개 x 1칸)
        ///   폭 5 이면 17칸(4 x 2 + 3 x 3)
        ///   폭 7 이면 25칸(4 x 4 + 3 x 3)
        /// 위 등장 횟수와 곱해 더하면 73x11 + 57x17 + 70x25 로 3522 가 된다.
        /// 즉 이 수는 생성 코드와 무관하게 손으로 따로 계산해 얻은 값이다.
        /// </summary>
        public const int SelfCheckExpectedNoBuildTotal = 3522;

        /// <summary>
        /// 시드 0~199 의 차단 타일 총합 기대값(유형과 무관하게 고정된 짝 없는 6칸은 제외).
        /// 파이썬 독립 구현으로 얻은 실측값이다.
        /// </summary>
        public const int SelfCheckExpectedBlockedTotal = 11566;

        /// <summary>
        /// 협곡형 결과가 규칙 6대로인지 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            // 높이 단계·대역 표가 먼저 맞아야 이 검사의 기준이 성립한다.
            if (!MapBandTable.TryRunSelfCheck(out string bandReason))
            {
                failureReason = "[0] 대역 표 자기 검증 실패: " + bandReason;
                return false;
            }

            var generator = new CanyonGenerator();
            var unpairedTiles = new HashSet<int>(new SymmetricMapBuilder().UnpairedTileIndices);

            var widthCounts = new int[CorridorWidthValues.Length];
            int noBuildTotal = 0;
            int blockedTotal = 0;

            for (int seed = 0; seed < SelfCheckSeedCount; seed++)
            {
                // 중립 광산 개수 1~4 를 골고루 쓴다(홀수면 회전 중심 단독 광산 경로를 탄다).
                int mineCount = CanyonMinNeutralMineCount + (seed % CanyonMaxNeutralMineCount);

                var request = new MapGenerationRequest
                {
                    MapVersion = MapDefinition.CurrentMapVersion,
                    RootSeed = (ulong)seed,
                    AttemptIndex = 0,
                    StartingMineSide = (seed % 2 == 0) ? MapStartingMineSide.CaseA : MapStartingMineSide.CaseB,
                    NeutralMineCount = mineCount
                };

                MapGenerationResult result = generator.Generate(request);

                if (!result.IsAccepted)
                {
                    failureReason = "[1] 시드 " + seed + " 가 거부됐다: " + result.RejectionReason;
                    return false;
                }

                // ── 2. 규칙 15 — Decoration 스트림을 쓰지 않았는가 ──────────
                if (result.DecorationDrawCount != 0)
                {
                    failureReason = "[2] Decoration 스트림을 " + result.DecorationDrawCount +
                        "번 썼다(규칙 15에 따라 0번이어야 한다).";
                    return false;
                }

                MapDefinition definition = result.Definition;
                int width = definition.Width;

                // ── 3. 회전 대칭 전수 확인(지형 231칸 + 성 + 시작 광산 + 중립 광산) ──
                if (!TryVerifyRotationalSymmetry(definition, out string symmetryReason))
                {
                    failureReason = "[3] 시드 " + seed + ": " + symmetryReason;
                    return false;
                }

                // ── 4. 통로 폭을 결과에서 되읽는다 ──────────────────────────
                //     중앙선(높이 단계 21)은 홀수 열만 있으므로, 그 열린 칸의 개수로
                //     폭을 역산할 수 있다(폭 3 이면 1칸, 5 면 3칸, 7 이면 3칸).
                //     더 확실한 방법은 「열린 열 구간」을 직접 재는 것이라 그렇게 한다.
                if (!TryMeasureCorridorWidth(definition, out int corridorWidth, out string measureReason))
                {
                    failureReason = "[4] 시드 " + seed + ": " + measureReason;
                    return false;
                }

                int widthSlot = IndexOfWidth(corridorWidth);
                if (widthSlot < 0)
                {
                    failureReason = "[4] 시드 " + seed + ": 통로 폭이 3 · 5 · 7 중 하나가 아니다(실제 " +
                        corridorWidth + ").";
                    return false;
                }

                widthCounts[widthSlot]++;

                if (corridorWidth < 3)
                {
                    failureReason = "[4] 시드 " + seed + ": 필수 통로의 규정 폭이 3 미만이다(실제 " +
                        corridorWidth + ").";
                    return false;
                }

                // ── 5. 폭 프로파일을 결과에서 되읽어 규칙 ② 를 확인한다 ─────
                int[] widthByRow = ReadWidthProfile(definition);
                if (!TryVerifyWidthProfile(widthByRow, corridorWidth, out string profileReason))
                {
                    failureReason = "[5] 시드 " + seed + ": " + profileReason;
                    return false;
                }

                // ── 6. 지형 집계와 규격 대조 ────────────────────────────────
                //
                // 231칸 전부를 세되, 「규격대로인가」는 윗절반(0행~가운데 행)에서만 본다.
                // 아랫절반은 회전 복제이고 그 사실은 바로 위 3번에서 전수로 확인했으므로,
                // 윗절반이 규격대로면 아랫절반도 규격대로다.
                // 🔴 아랫절반에 폭 프로파일을 그대로 대 보면 안 된다 — 프로파일은
                //    윗절반의 행 번호로만 정의돼 있다.
                for (int index = 0; index < definition.TileCount; index++)
                {
                    TileKind actual = definition.Tiles[index];

                    if (unpairedTiles.Contains(index))
                    {
                        if (actual != TileKind.Blocked)
                        {
                            failureReason = "[6] 시드 " + seed + ": 짝 없는 칸이 막혀 있지 않다(인덱스 " +
                                index + ").";
                            return false;
                        }
                        continue;
                    }

                    if (actual == TileKind.Blocked) blockedTotal++;
                    else if (actual == TileKind.NoBuild) noBuildTotal++;
                }

                for (int row = 0; row <= MapBandTable.CenterRow; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        int index = row * width + col;
                        if (unpairedTiles.Contains(index)) continue;

                        TileKind expected = GetTileKind(col, row, corridorWidth, widthByRow);
                        if (definition.Tiles[index] != expected)
                        {
                            failureReason = "[6] 시드 " + seed + ": (" + col + "," + row + ") 이 " +
                                expected + " 이어야 하는데 " + definition.Tiles[index] + " 이다.";
                            return false;
                        }
                    }
                }

                // ── 7. ⑤ 병목의 열린 타일이 전부 건설 불가인가 ─────────────
                IMapArchetypeConstraints constraints = result.Constraints;
                int bandOpenCount = 0;

                for (int row = 0; row < definition.Height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        if (!MapBandTable.IsWithinCentralBand(col, row)) continue;

                        int index = row * width + col;
                        bool open = definition.Tiles[index] != TileKind.Blocked;

                        if (open)
                        {
                            bandOpenCount++;

                            if (definition.Tiles[index] != TileKind.NoBuild)
                            {
                                failureReason = "[7] 시드 " + seed + ": 병목의 열린 타일이 건설 불가 타일이 아니다(" +
                                    col + "," + row + ").";
                                return false;
                            }

                            if (!constraints.IsBuildForbidden(col, row))
                            {
                                failureReason = "[7] 시드 " + seed + ": 병목의 열린 타일이 건설 불가 구역에 없다(" +
                                    col + "," + row + ").";
                                return false;
                            }
                        }

                        // ④ 회전 중심만 빼고 병목 전체가 광산 금지여야 한다.
                        bool isCenter = col == MapBandTable.CenterCol && row == MapBandTable.CenterRow;
                        if (constraints.IsNeutralMineForbidden(col, row) == isCenter)
                        {
                            failureReason = "[7] 시드 " + seed + ": 병목의 광산 금지 판정이 규칙과 다르다(" +
                                col + "," + row + ").";
                            return false;
                        }
                    }
                }

                if (bandOpenCount == 0)
                {
                    failureReason = "[7] 시드 " + seed + ": 병목이 통째로 막혔다.";
                    return false;
                }

                // ── 8. ⑥ 필수 통로가 1개이고 그 타일이 전부 열려 있는가 ────
                if (constraints.RequiredCorridors.Count != 1)
                {
                    failureReason = "[8] 시드 " + seed + ": 필수 통로가 1개가 아니다(실제 " +
                        constraints.RequiredCorridors.Count + "개).";
                    return false;
                }

                IReadOnlyList<int> corridorTiles = constraints.RequiredCorridors[0].TileIndices;
                if (corridorTiles.Count != bandOpenCount)
                {
                    failureReason = "[8] 시드 " + seed + ": 필수 통로 타일 수가 병목의 열린 칸 수와 다르다(" +
                        corridorTiles.Count + " vs " + bandOpenCount + ").";
                    return false;
                }

                for (int i = 0; i < corridorTiles.Count; i++)
                {
                    if (definition.Tiles[corridorTiles[i]] == TileKind.Blocked)
                    {
                        failureReason = "[8] 시드 " + seed + ": 필수 통로에 막힌 타일이 있다(인덱스 " +
                            corridorTiles[i] + ").";
                        return false;
                    }
                }

                // ── 9. 중립 광산이 금지 구역 밖에 있는가 ────────────────────
                for (int i = 0; i < definition.NeutralMines.Count; i++)
                {
                    int index = definition.NeutralMines[i];
                    int col = index % width;
                    int row = index / width;

                    if (constraints.IsNeutralMineForbidden(col, row))
                    {
                        failureReason = "[9] 시드 " + seed + ": 광산 금지 구역에 중립 광산이 놓였다(" +
                            col + "," + row + ").";
                        return false;
                    }
                }

                if (definition.NeutralMines.Count != mineCount)
                {
                    failureReason = "[9] 시드 " + seed + ": 중립 광산이 " + mineCount + "개가 아니다(실제 " +
                        definition.NeutralMines.Count + "개).";
                    return false;
                }

                // ── 10. 같은 시드 -> 같은 맵 ────────────────────────────────
                MapGenerationResult again = generator.Generate(request);
                if (!TryCompareDefinitions(definition, again.Definition, out string diffReason))
                {
                    failureReason = "[10] 시드 " + seed + " 를 다시 만들었더니 다른 맵이 나왔다: " + diffReason;
                    return false;
                }
            }

            // ── 11. 통계 ────────────────────────────────────────────────────
            for (int i = 0; i < widthCounts.Length; i++)
            {
                if (widthCounts[i] != SelfCheckExpectedWidthCountValues[i])
                {
                    failureReason = "[11] 통로 폭 " + CorridorWidthValues[i] + " 의 등장 횟수가 기대값 " +
                        SelfCheckExpectedWidthCountValues[i] + " 과 다르다(실제 " + widthCounts[i] + ").";
                    return false;
                }
            }

            if (noBuildTotal != SelfCheckExpectedNoBuildTotal)
            {
                failureReason = "[11] 건설 불가 타일 총합이 기대값 " + SelfCheckExpectedNoBuildTotal +
                    " 과 다르다(실제 " + noBuildTotal + ").";
                return false;
            }

            if (blockedTotal != SelfCheckExpectedBlockedTotal)
            {
                failureReason = "[11] 차단 타일 총합이 기대값 " + SelfCheckExpectedBlockedTotal +
                    " 과 다르다(실제 " + blockedTotal + ").";
                return false;
            }

            // ── 12. 음성 대조 ───────────────────────────────────────────────
            //
            // 「통과해야 할 것」만 넣어 보는 검사는 무엇이든 통과시키는 검사와 구별되지
            // 않는다. 그래서 규칙을 일부러 깨뜨린 입력을 같은 문으로 넣어 본다.
            var brokenProfile = new int[NarrowRowMax + 1];
            brokenProfile[NarrowRowMin] = NarrowStartWidth;
            brokenProfile[4] = 7;   // 11에서 7로 한 번에 4 줄었다 — 인접 감소량 상한 위반
            brokenProfile[5] = 7;
            brokenProfile[6] = 7;
            brokenProfile[7] = 7;
            brokenProfile[8] = 7;

            if (TryVerifyWidthProfile(brokenProfile, 7, out _))
            {
                failureReason = "[12] 음성 대조 실패 — 인접 감소량 4 인 폭 프로파일이 통과했다.";
                return false;
            }

            // 전환 행 개수가 모자란 프로파일도 걸러져야 한다.
            var missingTransition = new int[NarrowRowMax + 1];
            for (int row = NarrowRowMin; row <= NarrowRowMax; row++)
            {
                missingTransition[row] = NarrowStartWidth;
            }

            if (TryVerifyWidthProfile(missingTransition, 7, out _))
            {
                failureReason = "[12] 음성 대조 실패 — 8행에서 통로 폭에 도달하지 못한 프로파일이 통과했다.";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 자기 검증 실패를 에디터에서 즉시 드러낸다. 빌드에는 들어가지 않는다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void AssertSelfCheck()
        {
            if (!TryRunSelfCheck(out string failureReason))
            {
                throw new InvalidOperationException("CanyonGenerator 자기 검증 실패: " + failureReason);
            }
        }

        // ====================================================================
        // 자기 검증용 되읽기 도우미
        // ====================================================================

        /// <summary>
        /// 완성된 맵에서 「병목의 열린 열 구간」을 재어 통로 폭을 되읽는다.
        /// 구간이 연속이 아니거나 가운데 열을 중심으로 대칭이 아니면 실패로 본다.
        /// </summary>
        /// <param name="definition">완성된 맵 정의</param>
        /// <param name="corridorWidth">되읽은 통로 폭</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>되읽었으면 true</returns>
        private static bool TryMeasureCorridorWidth(MapDefinition definition, out int corridorWidth,
            out string failureReason)
        {
            corridorWidth = 0;

            // 열 구간은 열의 홀짝과 무관하게 같아야 하므로, 두 종류의 높이 단계를
            // 모두 훑어 가장 바깥까지 열린 거리를 잰다.
            int maxOpenDistance = -1;
            int minBlockedDistance = definition.Width;

            for (int row = 0; row < definition.Height; row++)
            {
                for (int col = 0; col < definition.Width; col++)
                {
                    if (!MapBandTable.IsWithinCentralBand(col, row)) continue;

                    int distance = col - MapBandTable.CenterCol;
                    if (distance < 0) distance = -distance;

                    if (definition.Tiles[row * definition.Width + col] == TileKind.Blocked)
                    {
                        if (distance < minBlockedDistance) minBlockedDistance = distance;
                    }
                    else
                    {
                        if (distance > maxOpenDistance) maxOpenDistance = distance;
                    }
                }
            }

            if (maxOpenDistance < 0)
            {
                failureReason = "병목에 열린 칸이 하나도 없다.";
                return false;
            }

            if (minBlockedDistance <= maxOpenDistance)
            {
                failureReason = "병목의 열린 구간이 가운데 열 기준 연속이 아니다(열린 최대 거리 " +
                    maxOpenDistance + ", 막힌 최소 거리 " + minBlockedDistance + ").";
                return false;
            }

            corridorWidth = maxOpenDistance * 2 + 1;
            failureReason = null;
            return true;
        }

        /// <summary>
        /// 완성된 맵에서 3~8행의 폭 프로파일을 되읽는다.
        /// 각 행에서 가운데 열로부터 열려 있는 가장 먼 거리를 재어 폭으로 환산한다.
        /// </summary>
        /// <param name="definition">완성된 맵 정의</param>
        /// <returns>행 번호로 찾아 쓰는 폭 배열</returns>
        private static int[] ReadWidthProfile(MapDefinition definition)
        {
            var widthByRow = new int[NarrowRowMax + 1];

            for (int row = NarrowRowMin; row <= NarrowRowMax; row++)
            {
                int maxOpenDistance = -1;

                for (int col = 0; col < definition.Width; col++)
                {
                    if (definition.Tiles[row * definition.Width + col] == TileKind.Blocked) continue;

                    int distance = col - MapBandTable.CenterCol;
                    if (distance < 0) distance = -distance;
                    if (distance > maxOpenDistance) maxOpenDistance = distance;
                }

                widthByRow[row] = maxOpenDistance * 2 + 1;
            }

            return widthByRow;
        }

        /// <summary> 통로 폭이 후보 목록의 몇 번째인지 돌려준다(없으면 -1). </summary>
        /// <param name="corridorWidth">찾을 통로 폭</param>
        /// <returns>후보 목록의 위치, 없으면 -1</returns>
        private static int IndexOfWidth(int corridorWidth)
        {
            for (int i = 0; i < CorridorWidthValues.Length; i++)
            {
                if (CorridorWidthValues[i] == corridorWidth) return i;
            }
            return -1;
        }
    }
}
