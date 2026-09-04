// ============================================================================
// ThreeLaneGenerator.cs
// 3갈래형(규칙 8) 맵 생성기.
//
// ─────────────────────────────────────────────────────────────────────────────
// ① 이 유형이 노리는 것
// ─────────────────────────────────────────────────────────────────────────────
//   맵 한가운데를 세 개의 레인으로 갈라 놓아, 어느 길로 갈지를 고르게 만든다.
//   레인 사이에는 벽이 있어 중간에 옆 레인으로 건너갈 수 없다.
//
// ─────────────────────────────────────────────────────────────────────────────
// ② 지형 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   분리 길이를 5 · 7 · 9 · 11 중에서 같은 확률로 하나 뽑는다.
//   분리 대역은 「높이 단계 21 에서 그 길이만큼 위아래」다. 길이별 실제 행 범위는
//   MapBandTable 의 표가 단일 소스이며, 외곽형도 같은 표를 읽는다.
//
//   분리 대역 안의 열 구조는 길이와 무관하게 언제나 같다.
//
//      0~2열      3열     4~6열      7열     8~10열
//      왼쪽 레인  벽      가운데 레인  벽     오른쪽 레인
//
//   레인 타일은 건설 불가 타일, 3열과 7열은 차단 타일이다.
//   분리 대역 밖은 11열 모두 열린 일반 타일이며, 별도의 전환 장애물 없이
//   즉시 하나로 합쳐진다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 길이는 높이 단계로 바꾸고, 열 구조는 손대지 않는다
// ─────────────────────────────────────────────────────────────────────────────
//   180도 회전은 열 번호를 10에서 뺀 값으로 보낸다.
//      0과 10 · 1과 9 · 2와 8 · 3과 7 · 4와 6 · 5는 자기 자신
//   그래서 위 열 구조는 회전에 그대로 대응된다. 왼쪽 레인은 오른쪽 레인으로,
//   3열 벽은 7열 벽으로, 가운데 레인은 가운데 레인으로 간다.
//
//   반면 「길이」는 행 번호로 적으면 짝수 열과 홀수 열의 범위가 달라진다.
//   그래서 길이만 높이 단계 기준으로 다루고, 열 구조는 건드리지 않는다.
//
// ─────────────────────────────────────────────────────────────────────────────
// ③④⑤⑥
// ─────────────────────────────────────────────────────────────────────────────
//   ③ 허용 중립 광산 수 : 1~6
//   ④ 광산 금지        : 분리 대역 안에서는 레인 하나에 광산이 최대 1개다.
//                        대역 안의 광산 대응쌍은 회전 대응되는 왼쪽·오른쪽 레인에만
//                        둘 수 있고, 가운데 레인 안쪽은 중립 광산 개수가 홀수일 때
//                        쓰는 회전 중심의 단독 광산 자리만 허용한다.
//                        남는 대응쌍은 합쳐진 위·아래 구역으로 간다.
//   ⑤ 건설 불가        : 분리 대역의 세 레인 전체
//   ⑥ 필수 통로       : 세 레인 각각(규정 폭 정확히 3)
//
//   ⚠️ 「대역 안 대응쌍은 최대 1쌍」을 제약 인터페이스로 표현하는 방법
//      IMapArchetypeConstraints 의 광산 금지 판정은 칸 하나를 받아 참/거짓만 돌려주는
//      함수라, "몇 쌍까지"라는 개수 조건을 그대로 담을 수 없다. 그래서 이 생성기가
//      대역 안의 왼쪽·오른쪽 레인 대응쌍 중 「허용할 한 쌍」을 시도마다 미리 하나
//      뽑아 두고, 나머지 대역 칸은 전부 금지로 표시한다. 결과적으로 대역 안에
//      놓일 수 있는 대응쌍은 많아야 한 쌍이 되어 규칙을 지킨다.
//      이 선택은 시도마다 새로 뽑히므로 앞 시도의 값이 다음 시도로 새지 않는다.
//
//   장식은 만들지 않는다(규칙 15). Decoration 스트림에서 한 번도 뽑지 않는다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 8 · 규칙 15
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 3갈래형(규칙 8) 생성기. 분리 대역을 세 레인과 두 벽으로 나눈다.
    /// </summary>
    public sealed class ThreeLaneGenerator : MapArchetypeGeneratorBase
    {
        // ====================================================================
        // 규칙 8 의 숫자들 — 열 구조는 고정값이다
        // ====================================================================

        /// <summary> 레인 하나의 규정 폭. 세 레인 모두 정확히 이 값이다. </summary>
        public const int LaneWidth = 3;

        /// <summary> 레인이 3개라는 사실(필수 통로 개수와 같다). </summary>
        public const int LaneCount = 3;

        /// <summary> 왼쪽 벽이 서는 열. </summary>
        public const int LeftWallCol = 3;

        /// <summary> 오른쪽 벽이 서는 열. 왼쪽 벽의 회전 상대다. </summary>
        public const int RightWallCol = 7;

        /// <summary> 레인 번호 — 왼쪽. </summary>
        public const int LeftLaneIndex = 0;

        /// <summary> 레인 번호 — 가운데. </summary>
        public const int CenterLaneIndex = 1;

        /// <summary> 레인 번호 — 오른쪽. </summary>
        public const int RightLaneIndex = 2;

        /// <summary> 레인이 아니라 벽이라는 뜻의 레인 번호. </summary>
        public const int WallLaneIndex = -1;

        private static readonly string[] LaneNameValues = { "왼쪽 레인", "가운데 레인", "오른쪽 레인" };

        /// <summary> 필수 통로 이름(레인 번호 순서). </summary>
        public static IReadOnlyList<string> LaneNames => LaneNameValues;

        /// <summary> 이 생성기가 만드는 맵 유형. </summary>
        public override MapType MapType => MapType.ThreeLane;

        // ====================================================================
        // 열 구조
        // ====================================================================

        /// <summary>
        /// 그 열이 어느 레인인지 돌려준다. 벽이면 벽을 뜻하는 번호를 돌려준다.
        /// </summary>
        /// <param name="col">열</param>
        /// <returns>레인 번호, 또는 벽을 뜻하는 번호</returns>
        public static int GetLaneIndex(int col)
        {
            if (col == LeftWallCol || col == RightWallCol) return WallLaneIndex;
            if (col < LeftWallCol) return LeftLaneIndex;
            if (col < RightWallCol) return CenterLaneIndex;
            return RightLaneIndex;
        }

        // ====================================================================
        // 지형 그리기
        // ====================================================================

        /// <summary>
        /// 규칙 8대로 세 갈래를 그린다. 윗절반만 기록하고 아랫절반은 대응쌍 기록 API 가
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

            // ── 1. 분리 길이 ────────────────────────────────────────────────
            int separationLength = terrainStream.Choose(MapBandTable.SeparationLengths);

            // ── 2. 지형 기록 ────────────────────────────────────────────────
            MapBandTable.ApplySymmetricTerrain(builder, (col, row) => GetTileKind(col, row, separationLength));

            // ── 3. ④⑤⑥ 제약 ───────────────────────────────────────────────
            constraints = BuildConstraints(builder, terrainStream, separationLength, out string constraintReason);
            if (constraints == null) return constraintReason;

            return null;
        }

        /// <summary>
        /// 좌표 하나의 지형 종류를 정한다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="separationLength">분리 길이</param>
        /// <returns>그 칸에 놓을 지형 종류</returns>
        private static TileKind GetTileKind(int col, int row, int separationLength)
        {
            // 대역 밖은 전부 열린 일반 타일이다. 전환 장애물은 두지 않는다.
            if (!MapBandTable.IsWithinBand(col, row, separationLength)) return TileKind.Normal;

            return GetLaneIndex(col) == WallLaneIndex ? TileKind.Blocked : TileKind.NoBuild;
        }

        /// <summary>
        /// 이 시도의 ④⑤⑥ 제약을 만든다.
        /// </summary>
        /// <param name="builder">지형이 이미 기록된 builder</param>
        /// <param name="terrainStream">Terrain 스트림(허용할 대역 대응쌍을 하나 뽑는 데 쓴다)</param>
        /// <param name="separationLength">분리 길이</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>유형별 제약(실패하면 null)</returns>
        private static IMapArchetypeConstraints BuildConstraints(SymmetricMapBuilder builder,
            MapRandom terrainStream, int separationLength, out string failureReason)
        {
            // ── 대역 안에서 허용할 대응쌍 하나를 미리 뽑는다 ────────────────
            // 대표는 쌍을 이루는 두 칸의 인덱스 중 작은 쪽이다(규칙 3의 대표 규약과 같다).
            var pairCandidates = new List<int>();

            for (int row = 0; row < builder.Height; row++)
            {
                for (int col = 0; col < builder.Width; col++)
                {
                    if (builder.IsCenter(col, row)) continue;
                    if (!builder.HasRotationPartner(col, row)) continue;
                    if (!MapBandTable.IsWithinBand(col, row, separationLength)) continue;

                    int lane = GetLaneIndex(col);
                    if (lane != LeftLaneIndex && lane != RightLaneIndex) continue;

                    builder.RotateCoord(col, row, out int rotatedCol, out int rotatedRow);

                    int index = row * builder.Width + col;
                    int rotatedIndex = rotatedRow * builder.Width + rotatedCol;
                    if (index > rotatedIndex) continue;

                    pairCandidates.Add(index);
                }
            }

            if (pairCandidates.Count == 0)
            {
                failureReason = "분리 대역 안에 좌·우 레인 대응쌍 후보가 하나도 없다.";
                return null;
            }

            int allowedPairIndex = pairCandidates[terrainStream.NextInt(pairCandidates.Count)];

            SymmetricMapBuilder.RotateCoord(allowedPairIndex % builder.Width, allowedPairIndex / builder.Width,
                builder.Width, builder.Height, out int allowedRotatedCol, out int allowedRotatedRow);
            int allowedPartnerIndex = allowedRotatedRow * builder.Width + allowedRotatedCol;

            // ── 금지 구역과 필수 통로를 모은다 ──────────────────────────────
            var mineForbidden = new List<int>();
            var buildForbidden = new List<int>();
            var laneTiles = new List<int>[LaneCount];

            for (int lane = 0; lane < LaneCount; lane++)
            {
                laneTiles[lane] = new List<int>();
            }

            for (int row = 0; row < builder.Height; row++)
            {
                for (int col = 0; col < builder.Width; col++)
                {
                    if (!MapBandTable.IsWithinBand(col, row, separationLength)) continue;

                    int index = row * builder.Width + col;
                    int lane = GetLaneIndex(col);

                    // ④ 대역 안은 원칙적으로 전부 금지다. 예외는 딱 둘이다.
                    //    · 미리 뽑아 둔 좌·우 레인 대응쌍 두 칸
                    //    · 회전 중심(중립 광산 개수가 홀수일 때 쓰는 가운데 레인의 단독 자리)
                    bool isAllowedPairTile = index == allowedPairIndex || index == allowedPartnerIndex;
                    bool isCenterTile = builder.IsCenter(col, row);

                    if (!isAllowedPairTile && !isCenterTile)
                    {
                        mineForbidden.Add(index);
                    }

                    if (lane == WallLaneIndex) continue;

                    // ⑤⑥ 레인 타일은 통로이자 건설 불가 구역이다.
                    buildForbidden.Add(index);
                    laneTiles[lane].Add(index);
                }
            }

            var corridors = new List<MapCorridorRequirement>(LaneCount);
            for (int lane = 0; lane < LaneCount; lane++)
            {
                corridors.Add(new MapCorridorRequirement(LaneNameValues[lane], laneTiles[lane]));
            }

            failureReason = null;
            return new MapArchetypeConstraints(MapType.ThreeLane, builder.Width,
                mineForbidden, buildForbidden, corridors);
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

        private static readonly int[] SelfCheckExpectedSeparationCountValues = { 53, 58, 50, 39 };

        /// <summary>
        /// 시드 0~199 에서 분리 길이 5 · 7 · 9 · 11 이 각각 몇 번 뽑히는지의 기대값.
        /// 이 알고리즘을 파이썬으로 따로 구현해 돌린 실측값이며, 네 값의 합은 200 이다.
        ///
        /// 🔴 이 수가 어긋나면 검사를 고치지 말 것. 뽑기 순서나 확률이 바뀌었다는 뜻이고,
        ///    그러면 과거 시드가 다른 맵을 만들게 되므로 MapVersion 을 올려야 하는지부터 판단한다.
        /// </summary>
        public static IReadOnlyList<int> SelfCheckExpectedSeparationCounts => SelfCheckExpectedSeparationCountValues;

        /// <summary>
        /// 시드 0~199 의 차단 타일 총합 기대값(짝 없는 6칸은 제외).
        ///
        /// 이 값이 어디서 나왔는가: 차단 타일은 대역 안의 3열과 7열뿐이다. 분리 길이 L 의
        /// 대역에 들어가는 홀수 높이 단계는 L 개이므로 한 열당 L 칸, 두 열이니 2L 칸이다.
        /// 위 등장 횟수와 곱해 더하면 53x5 + 58x7 + 50x9 + 39x11 로 1550 이 되고,
        /// 두 배 하면 3100 이다. 생성 코드와 무관하게 손으로 따로 계산해 얻은 값이다.
        /// </summary>
        public const int SelfCheckExpectedBlockedTotal = 3100;

        /// <summary>
        /// 시드 0~199 의 건설 불가 타일 총합 기대값.
        ///
        /// 이 값이 어디서 나왔는가: 분리 길이 L 의 대역 칸 수는 짝수 높이 단계 (L+1)개에
        /// 6칸씩, 홀수 높이 단계 L 개에 5칸씩이라 11L + 6 칸이다. 여기서 벽 2L 칸을 빼면
        /// 레인 타일이 9L + 6 칸이다. 200장에 대해 더하면 9x1550 + 6x200 으로 15150 이다.
        /// </summary>
        public const int SelfCheckExpectedNoBuildTotal = 15150;

        /// <summary>
        /// 3갈래형 결과가 규칙 8대로인지 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            if (!MapBandTable.TryRunSelfCheck(out string bandReason))
            {
                failureReason = "[0] 대역 표 자기 검증 실패: " + bandReason;
                return false;
            }

            var generator = new ThreeLaneGenerator();
            var unpairedTiles = new HashSet<int>(new SymmetricMapBuilder().UnpairedTileIndices);

            var separationCounts = new int[MapBandTable.SeparationLengths.Count];
            int blockedTotal = 0;
            int noBuildTotal = 0;

            for (int seed = 0; seed < SelfCheckSeedCount; seed++)
            {
                int mineCount = 1 + (seed % 6);

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

                if (result.DecorationDrawCount != 0)
                {
                    failureReason = "[2] Decoration 스트림을 " + result.DecorationDrawCount +
                        "번 썼다(규칙 15에 따라 0번이어야 한다).";
                    return false;
                }

                MapDefinition definition = result.Definition;
                int width = definition.Width;

                // ── 3. 회전 대칭 전수 확인 ──────────────────────────────────
                if (!TryVerifyRotationalSymmetry(definition, out string symmetryReason))
                {
                    failureReason = "[3] 시드 " + seed + ": " + symmetryReason;
                    return false;
                }

                // ── 4. 분리 길이를 결과에서 되읽는다 ────────────────────────
                //     3열이 막혀 있는 높이 단계의 개수가 곧 분리 길이다.
                if (!TryMeasureSeparationLength(definition, out int separationLength, out string measureReason))
                {
                    failureReason = "[4] 시드 " + seed + ": " + measureReason;
                    return false;
                }

                int lengthSlot = IndexOfSeparationLength(separationLength);
                if (lengthSlot < 0)
                {
                    failureReason = "[4] 시드 " + seed + ": 분리 길이가 후보에 없다(실제 " +
                        separationLength + ").";
                    return false;
                }

                separationCounts[lengthSlot]++;

                // ── 5. 대역 안팎의 지형이 규격과 같은가 ─────────────────────
                for (int index = 0; index < definition.TileCount; index++)
                {
                    TileKind actual = definition.Tiles[index];

                    if (unpairedTiles.Contains(index))
                    {
                        if (actual != TileKind.Blocked)
                        {
                            failureReason = "[5] 시드 " + seed + ": 짝 없는 칸이 막혀 있지 않다(인덱스 " +
                                index + ").";
                            return false;
                        }
                        continue;
                    }

                    int col = index % width;
                    int row = index / width;
                    TileKind expected = GetTileKind(col, row, separationLength);

                    if (actual != expected)
                    {
                        failureReason = "[5] 시드 " + seed + ": (" + col + "," + row + ") 이 " +
                            expected + " 이어야 하는데 " + actual + " 이다.";
                        return false;
                    }

                    if (actual == TileKind.Blocked) blockedTotal++;
                    else if (actual == TileKind.NoBuild) noBuildTotal++;
                }

                // ── 6. 벽·레인·대역 밖 규격을 항목별로 다시 본다 ────────────
                if (!TryVerifyLaneStructure(definition, separationLength, unpairedTiles, out string laneReason))
                {
                    failureReason = "[6] 시드 " + seed + ": " + laneReason;
                    return false;
                }

                // ── 7. ⑤⑥ 제약 ────────────────────────────────────────────
                IMapArchetypeConstraints constraints = result.Constraints;

                if (constraints.RequiredCorridors.Count != LaneCount)
                {
                    failureReason = "[7] 시드 " + seed + ": 필수 통로가 " + LaneCount + "개가 아니다(실제 " +
                        constraints.RequiredCorridors.Count + "개).";
                    return false;
                }

                for (int lane = 0; lane < LaneCount; lane++)
                {
                    IReadOnlyList<int> tiles = constraints.RequiredCorridors[lane].TileIndices;

                    if (tiles.Count == 0)
                    {
                        failureReason = "[7] 시드 " + seed + ": " + LaneNameValues[lane] + " 통로가 비었다.";
                        return false;
                    }

                    var laneCols = new HashSet<int>();

                    for (int i = 0; i < tiles.Count; i++)
                    {
                        int col = tiles[i] % width;
                        int row = tiles[i] / width;

                        if (definition.Tiles[tiles[i]] != TileKind.NoBuild)
                        {
                            failureReason = "[7] 시드 " + seed + ": 레인 타일이 건설 불가 타일이 아니다(" +
                                col + "," + row + ").";
                            return false;
                        }

                        if (!constraints.IsBuildForbidden(col, row))
                        {
                            failureReason = "[7] 시드 " + seed + ": 레인 타일이 건설 불가 구역에 없다(" +
                                col + "," + row + ").";
                            return false;
                        }

                        if (GetLaneIndex(col) != lane)
                        {
                            failureReason = "[7] 시드 " + seed + ": 통로 " + lane + " 에 다른 레인의 타일이 있다(" +
                                col + "," + row + ").";
                            return false;
                        }

                        laneCols.Add(col);
                    }

                    // 🔴 레인의 규정 폭은 「정확히 3」이다.
                    if (laneCols.Count != LaneWidth)
                    {
                        failureReason = "[7] 시드 " + seed + ": " + LaneNameValues[lane] + " 의 폭이 " +
                            LaneWidth + " 이 아니다(실제 " + laneCols.Count + "열).";
                        return false;
                    }
                }

                // ── 8. ④ 대역 안 광산이 레인당 최대 1개인가 ────────────────
                if (!TryVerifyMineLimits(definition, constraints, separationLength, out string mineReason))
                {
                    failureReason = "[8] 시드 " + seed + ": " + mineReason;
                    return false;
                }

                if (definition.NeutralMines.Count != mineCount)
                {
                    failureReason = "[8] 시드 " + seed + ": 중립 광산이 " + mineCount + "개가 아니다(실제 " +
                        definition.NeutralMines.Count + "개).";
                    return false;
                }

                // ── 9. 같은 시드 -> 같은 맵 ─────────────────────────────────
                MapGenerationResult again = generator.Generate(request);
                if (!TryCompareDefinitions(definition, again.Definition, out string diffReason))
                {
                    failureReason = "[9] 시드 " + seed + " 를 다시 만들었더니 다른 맵이 나왔다: " + diffReason;
                    return false;
                }
            }

            // ── 10. 통계 ────────────────────────────────────────────────────
            for (int i = 0; i < separationCounts.Length; i++)
            {
                if (separationCounts[i] != SelfCheckExpectedSeparationCountValues[i])
                {
                    failureReason = "[10] 분리 길이 " + MapBandTable.SeparationLengths[i] +
                        " 의 등장 횟수가 기대값 " + SelfCheckExpectedSeparationCountValues[i] +
                        " 과 다르다(실제 " + separationCounts[i] + ").";
                    return false;
                }
            }

            if (blockedTotal != SelfCheckExpectedBlockedTotal)
            {
                failureReason = "[10] 차단 타일 총합이 기대값 " + SelfCheckExpectedBlockedTotal +
                    " 과 다르다(실제 " + blockedTotal + ").";
                return false;
            }

            if (noBuildTotal != SelfCheckExpectedNoBuildTotal)
            {
                failureReason = "[10] 건설 불가 타일 총합이 기대값 " + SelfCheckExpectedNoBuildTotal +
                    " 과 다르다(실제 " + noBuildTotal + ").";
                return false;
            }

            // ── 11. 음성 대조 ───────────────────────────────────────────────
            //
            // 「통과해야 할 것」만 넣어 보는 검사는 무엇이든 통과시키는 검사와 구별되지
            // 않는다. 그래서 규칙을 일부러 깨뜨린 맵을 같은 문으로 넣어 본다.
            if (!TryRunNegativeControl(out string negativeReason))
            {
                failureReason = "[11] " + negativeReason;
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
                throw new InvalidOperationException("ThreeLaneGenerator 자기 검증 실패: " + failureReason);
            }
        }

        // ====================================================================
        // 자기 검증용 도우미
        // ====================================================================

        /// <summary>
        /// 대역 안 벽·레인 구조와 대역 밖 일반 타일 규격을 확인한다.
        /// 자기 검증과 음성 대조가 같은 함수를 쓴다.
        /// </summary>
        /// <param name="definition">확인할 맵 정의</param>
        /// <param name="separationLength">분리 길이</param>
        /// <param name="unpairedTiles">유형과 무관하게 고정된 짝 없는 칸들</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>규격대로면 true</returns>
        public static bool TryVerifyLaneStructure(MapDefinition definition, int separationLength,
            HashSet<int> unpairedTiles, out string failureReason)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (unpairedTiles == null) throw new ArgumentNullException(nameof(unpairedTiles));

            for (int row = 0; row < definition.Height; row++)
            {
                for (int col = 0; col < definition.Width; col++)
                {
                    int index = row * definition.Width + col;
                    if (unpairedTiles.Contains(index)) continue;

                    TileKind actual = definition.Tiles[index];
                    bool inBand = MapBandTable.IsWithinBand(col, row, separationLength);

                    if (!inBand)
                    {
                        if (actual != TileKind.Normal)
                        {
                            failureReason = "대역 밖 (" + col + "," + row + ") 이 일반 타일이 아니다(실제 " +
                                actual + ").";
                            return false;
                        }
                        continue;
                    }

                    if (col == LeftWallCol || col == RightWallCol)
                    {
                        if (actual != TileKind.Blocked)
                        {
                            failureReason = "대역 안 벽 (" + col + "," + row + ") 이 막혀 있지 않다(실제 " +
                                actual + ").";
                            return false;
                        }
                        continue;
                    }

                    if (actual != TileKind.NoBuild)
                    {
                        failureReason = "대역 안 레인 (" + col + "," + row + ") 이 건설 불가 타일이 아니다(실제 " +
                            actual + ").";
                        return false;
                    }
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 분리 대역 안의 중립 광산이 레인당 최대 1개인지, 가운데 레인은 회전 중심만 쓰는지 확인한다.
        /// </summary>
        /// <param name="definition">확인할 맵 정의</param>
        /// <param name="constraints">이 시도의 제약</param>
        /// <param name="separationLength">분리 길이</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>규칙대로면 true</returns>
        private static bool TryVerifyMineLimits(MapDefinition definition, IMapArchetypeConstraints constraints,
            int separationLength, out string failureReason)
        {
            var laneMineCounts = new int[LaneCount];

            for (int i = 0; i < definition.NeutralMines.Count; i++)
            {
                int index = definition.NeutralMines[i];
                int col = index % definition.Width;
                int row = index / definition.Width;

                if (constraints.IsNeutralMineForbidden(col, row))
                {
                    failureReason = "광산 금지 구역에 중립 광산이 놓였다(" + col + "," + row + ").";
                    return false;
                }

                if (!MapBandTable.IsWithinBand(col, row, separationLength)) continue;

                int lane = GetLaneIndex(col);
                if (lane == WallLaneIndex)
                {
                    failureReason = "벽 위에 중립 광산이 놓였다(" + col + "," + row + ").";
                    return false;
                }

                if (lane == CenterLaneIndex &&
                    !(col == MapBandTable.CenterCol && row == MapBandTable.CenterRow))
                {
                    failureReason = "가운데 레인 안쪽에 회전 중심이 아닌 중립 광산이 놓였다(" +
                        col + "," + row + ").";
                    return false;
                }

                laneMineCounts[lane]++;
            }

            for (int lane = 0; lane < LaneCount; lane++)
            {
                if (laneMineCounts[lane] > 1)
                {
                    failureReason = LaneNameValues[lane] + " 안에 중립 광산이 " + laneMineCounts[lane] +
                        "개다(최대 1개).";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 규칙을 일부러 깨뜨린 맵을 검사에 넣어, 검사가 실제로 걸러 내는지 확인한다.
        /// </summary>
        /// <param name="failureReason">음성 대조가 실패한 사유(성공 시 null)</param>
        /// <returns>깨뜨린 입력이 모두 걸러졌으면 true</returns>
        private static bool TryRunNegativeControl(out string failureReason)
        {
            var unpairedTiles = new HashSet<int>(new SymmetricMapBuilder().UnpairedTileIndices);

            // 정상 맵을 하나 만든 다음, 레인 한 칸을 막아 폭을 3 에서 2 로 만든다.
            var generator = new ThreeLaneGenerator();
            var request = new MapGenerationRequest
            {
                MapVersion = MapDefinition.CurrentMapVersion,
                RootSeed = 0UL,
                AttemptIndex = 0,
                StartingMineSide = MapStartingMineSide.CaseA,
                NeutralMineCount = 2
            };

            MapGenerationResult result = generator.Generate(request);
            if (!result.IsAccepted)
            {
                failureReason = "음성 대조를 위한 기준 맵이 거부됐다: " + result.RejectionReason;
                return false;
            }

            MapDefinition definition = result.Definition;
            if (!TryMeasureSeparationLength(definition, out int separationLength, out string measureReason))
            {
                failureReason = "음성 대조를 위한 분리 길이를 되읽지 못했다: " + measureReason;
                return false;
            }

            // 대역 안 레인 타일 하나를 골라 막힌 타일로 바꾼다.
            int brokenIndex = -1;
            for (int row = 0; row < definition.Height && brokenIndex < 0; row++)
            {
                for (int col = 0; col < definition.Width; col++)
                {
                    if (!MapBandTable.IsWithinBand(col, row, separationLength)) continue;
                    if (GetLaneIndex(col) == WallLaneIndex) continue;

                    brokenIndex = row * definition.Width + col;
                    break;
                }
            }

            if (brokenIndex < 0)
            {
                failureReason = "음성 대조에 쓸 레인 타일을 찾지 못했다.";
                return false;
            }

            definition.Tiles[brokenIndex] = TileKind.Blocked;

            if (TryVerifyLaneStructure(definition, separationLength, unpairedTiles, out _))
            {
                failureReason = "음성 대조 실패 — 레인 한 칸이 막힌 맵이 통과했다.";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 완성된 맵에서 분리 길이를 되읽는다. 왼쪽 벽 열이 막혀 있는 칸 수가 곧 분리 길이다.
        /// </summary>
        /// <param name="definition">완성된 맵 정의</param>
        /// <param name="separationLength">되읽은 분리 길이</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>되읽었으면 true</returns>
        private static bool TryMeasureSeparationLength(MapDefinition definition, out int separationLength,
            out string failureReason)
        {
            separationLength = 0;

            for (int row = 0; row < definition.Height; row++)
            {
                if (definition.Tiles[row * definition.Width + LeftWallCol] == TileKind.Blocked)
                {
                    separationLength++;
                }
            }

            if (!MapBandTable.IsSeparationLengthValid(separationLength))
            {
                failureReason = "왼쪽 벽 열의 막힌 칸 수가 분리 길이 후보와 맞지 않다(실제 " +
                    separationLength + "칸).";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary> 분리 길이가 후보 목록의 몇 번째인지 돌려준다(없으면 -1). </summary>
        /// <param name="separationLength">찾을 분리 길이</param>
        /// <returns>후보 목록의 위치, 없으면 -1</returns>
        private static int IndexOfSeparationLength(int separationLength)
        {
            for (int i = 0; i < MapBandTable.SeparationLengths.Count; i++)
            {
                if (MapBandTable.SeparationLengths[i] == separationLength) return i;
            }
            return -1;
        }
    }
}
