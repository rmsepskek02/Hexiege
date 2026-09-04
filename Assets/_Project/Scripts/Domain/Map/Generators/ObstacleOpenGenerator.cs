// ============================================================================
// ObstacleOpenGenerator.cs
// 장애물 개방형(규칙 5) 맵 생성기.
//
// ─────────────────────────────────────────────────────────────────────────────
// 어떻게 장애물을 뿌리는가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   장애물은 윗절반에서만 뽑고, 아랫절반은 뽑지 않는다. 아랫절반은 윗절반을
//   180도 돌린 그림이 그대로 들어간다(SymmetricMapBuilder 의 대응쌍 기록).
//   그래서 두 진영이 완전히 같은 지형을 갖는다.
//
//   뽑는 단위는 "행"이다. 한 행은 언제나 11칸이라 어느 행을 뽑아도 밀도가 같기
//   때문이다.
//
//     · 3행부터 9행까지 7개 행. 각 행마다 따로 장애물 개수를 0/1/2/3/4 중 하나로
//       뽑는다(각각 20%). 그 개수만큼 그 행의 서로 다른 열을 균등하게 고른다.
//       한 행 안에서 같은 열이 두 번 나오면 안 되므로, 겹치면 다시 뽑는다.
//
//     · 0~2행에는 장애물을 두지 않는다. 성과 시작 영역이 있는 대역이라 여기를
//       막으면 시작하자마자 불리해질 수 있다. 그 회전 상대 대역도 당연히 비운다.
//
//     · 중앙선은 따로 뽑는다. 개수는 0/2/4 중 하나(각각 3분의 1)이고, 중앙선 안의
//       회전 대응쌍으로만 놓는다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 "행"과 "높이 단계"는 다른 말이다 — 헷갈리면 대역이 어긋난다
// ─────────────────────────────────────────────────────────────────────────────
//   · 행(row)      : 배열의 세로 번호. 뽑기는 이 단위로 한다.
//   · 높이 단계    : 실제 높이가 같은 타일을 묶은 진짜 가로선 번호.
//                    행에 2를 곱하고 홀수 열이면 1을 더한 값이며, 1~41 범위다.
//                    21이 중앙선이고, 회전하면 높이 단계 L 은 42-L 로 간다.
//
//   이 둘이 다르기 때문에 두 가지 일이 생긴다.
//
//   ① 중앙선(높이 단계 21)은 "10행 전체"가 아니라 홀수 열의 10행 5칸뿐이다.
//      즉 (1,10) (3,10) (5,10) (7,10) (9,10) 다섯 칸이다.
//      가운데 (5,10) 은 회전해도 자기 자신인 고정점이라 단독 장애물을 두지 않는다.
//      남는 대응쌍은 (1,10)-(9,10) 과 (3,10)-(7,10) 두 쌍뿐이라, 중앙선 장애물
//      개수가 0/2/4 인 것이다.
//
//   ② 0~2행의 회전 상대는 행 번호가 열의 홀짝에 따라 다르다.
//      짝수 열은 19~20행, 홀수 열은 18~20행이다. 그래서 "위쪽 3칸의 상대는
//      아래쪽 3칸"이라고 단순히 말할 수 없다.
//      같은 이유로 3~9행의 회전 상대 대역은 11~18행이 되어, 위쪽 대역(3~9)과
//      행 번호가 정확히 겹치지 않는다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 거부(reject) 규칙
// ─────────────────────────────────────────────────────────────────────────────
//   · 전체 결과에 장애물 대응쌍이 하나도 없으면 이 시도를 거부한다.
//     장애물 개방형인데 장애물이 없으면 완전개방형과 구별되지 않기 때문이다.
//   · 도달성 검증에 걸려도 장애물 수를 줄이거나 자리를 옮기지 않는다.
//     시도 전체를 버리고 다음 시도 번호로 처음부터 다시 만든다(규칙 6).
//
//   ③ 허용 중립 광산 수 1~6 · ④ 광산 금지 구역 없음 · ⑤ 건설 불가 구역 없음
//   ⑥ 필수 통로 없음.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 5 · 규칙 6
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 장애물 개방형(규칙 5) 생성기. 윗절반 3~9행과 중앙선에서만 뽑고 아랫절반은 회전 투영이다.
    /// </summary>
    public sealed class ObstacleOpenGenerator : MapArchetypeGeneratorBase
    {
        // ====================================================================
        // 규칙 5 의 숫자들
        // ====================================================================

        /// <summary> 장애물을 뽑는 첫 행. 0~2행은 성·시작 영역 대역이라 비운다. </summary>
        public const int ObstacleRowMin = 3;

        /// <summary> 장애물을 뽑는 마지막 행. 10행은 중앙선이라 따로 처리한다. </summary>
        public const int ObstacleRowMax = 9;

        /// <summary> 한 행에 놓일 수 있는 장애물의 최대 개수. 0~4 를 각각 20% 로 뽑는다. </summary>
        public const int MaxObstaclePerRow = 4;

        /// <summary>
        /// 중앙선에 놓을 수 있는 대응쌍 개수의 가짓수. 0쌍/1쌍/2쌍을 각각 3분의 1로 뽑으며,
        /// 장애물 개수로는 0개/2개/4개가 된다.
        /// </summary>
        public const int CenterLinePairChoiceCount = 3;

        /// <summary> 장애물을 두지 않는 위쪽 대역의 마지막 행(0~2행). </summary>
        public const int ProtectedTopRowMax = 2;

        /// <summary> 이 생성기가 만드는 맵 유형. </summary>
        public override MapType MapType => MapType.ObstacleOpen;

        // ====================================================================
        // 지형 그리기
        // ====================================================================

        /// <summary>
        /// 규칙 5대로 장애물을 뿌린다. 윗절반과 중앙선에서만 뽑고, 아랫절반은
        /// 대응쌍 기록 API 가 자동으로 채운다.
        /// </summary>
        /// <param name="builder">기록 대상 builder(성·시작 광산은 이미 놓여 있다)</param>
        /// <param name="terrainStream">Terrain 스트림</param>
        /// <param name="constraints">이 시도에서 확정된 유형별 제약</param>
        /// <returns>성공이면 null, 거부면 사유</returns>
        protected override string TryApplyTerrain(SymmetricMapBuilder builder, MapRandom terrainStream,
            out IMapArchetypeConstraints constraints)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (terrainStream == null) throw new ArgumentNullException(nameof(terrainStream));

            constraints = MapArchetypeConstraints.CreateUnrestricted(MapType, builder.Width);

            int placedPairCount = 0;
            var pickedSlots = new List<int>(MaxObstaclePerRow);

            // ── 1. 윗절반 3~9행 ─────────────────────────────────────────────
            for (int row = ObstacleRowMin; row <= ObstacleRowMax; row++)
            {
                // 0 이상 5 미만 = 0/1/2/3/4 를 각각 20% 로 뽑는다.
                int obstacleCount = terrainStream.NextInt(0, MaxObstaclePerRow + 1);

                // 같은 행에서 열이 겹치면 그 자리에서 다시 뽑는다(규칙 6의 rejection sampling).
                if (!NeutralMineSampler.TryPickDistinctSlots(terrainStream, builder.Width, obstacleCount,
                        pickedSlots, out string pickReason))
                {
                    return "행 " + row + " 의 장애물 열을 고르지 못했다: " + pickReason;
                }

                for (int i = 0; i < pickedSlots.Count; i++)
                {
                    // 한 번 부르면 이 칸과 180도 회전 자리에 함께 기록된다.
                    builder.SetPair(pickedSlots[i], row, TileKind.Blocked);
                    placedPairCount++;
                }
            }

            // ── 2. 중앙선 ───────────────────────────────────────────────────
            //
            // 중앙선은 홀수 열의 10행 5칸이다. 그중 가운데는 회전 고정점이라 빼고,
            // 왼쪽 절반의 홀수 열만 대표로 모으면 대응쌍 목록이 된다(11칸이면 1열과 3열).
            var centerLineRepresentativeCols = new List<int>();
            for (int col = 1; col < builder.CenterCol; col += 2)
            {
                centerLineRepresentativeCols.Add(col);
            }

            int centerPairCount = terrainStream.NextInt(0, CenterLinePairChoiceCount);

            if (!NeutralMineSampler.TryPickDistinctSlots(terrainStream, centerLineRepresentativeCols.Count,
                    centerPairCount, pickedSlots, out string centerReason))
            {
                return "중앙선 장애물 자리를 고르지 못했다: " + centerReason;
            }

            for (int i = 0; i < pickedSlots.Count; i++)
            {
                builder.SetPair(centerLineRepresentativeCols[pickedSlots[i]], builder.CenterRow, TileKind.Blocked);
                placedPairCount++;
            }

            // ── 3. 거부 판정 ────────────────────────────────────────────────
            if (placedPairCount == 0)
            {
                return "장애물 대응쌍이 하나도 만들어지지 않았다(장애물 개방형이 아니게 된다).";
            }

            return null;
        }

        // ====================================================================
        // 자기 검증 — 이 프로젝트에는 유닛 테스트 어셈블리가 없다
        // ====================================================================

        /// <summary> 통계 확인에 쓰는 서로 다른 시드의 개수. </summary>
        public const int SelfCheckSeedCount = 200;

        /// <summary>
        /// 시드 0~199 로 만든 맵의 장애물 총합 기대값.
        ///
        /// 이 값이 어디서 나왔는가:
        ///   · 이론값 — 3~9행 7개 행의 평균이 각각 2개이므로 윗절반 14개,
        ///     회전 투영으로 28개, 중앙선 평균 2개를 더해 30개다.
        ///   · 실측값 — 이 알고리즘을 파이썬으로 따로 구현해 시드 0~199 를 돌린 결과가
        ///     총 5904개, 평균 29.52개였다. 이론값 30 과의 차이는 표본 200개의
        ///     흔들림 범위(평균의 표준편차 약 0.54) 안이다.
        ///
        /// 🔴 이 수가 어긋나면 검사를 고치지 말 것. 뽑기 순서나 확률이 바뀌었다는 뜻이고,
        ///    그러면 과거 시드가 다른 맵을 만들게 되므로 MapVersion 을 올려야 하는지부터 판단한다.
        /// </summary>
        public const int SelfCheckExpectedObstacleTotal = 5904;

        /// <summary>
        /// 장애물 개방형 결과가 규칙 5대로인지 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            var generator = new ObstacleOpenGenerator();
            var unpairedTiles = new HashSet<int>(new SymmetricMapBuilder().UnpairedTileIndices);

            int obstacleTotal = 0;

            for (int seed = 0; seed < SelfCheckSeedCount; seed++)
            {
                var request = new MapGenerationRequest
                {
                    MapVersion = MapDefinition.CurrentMapVersion,
                    RootSeed = (ulong)seed,
                    AttemptIndex = 0,
                    StartingMineSide = MapStartingMineSide.CaseA,
                    NeutralMineCount = 2
                };

                MapGenerationResult result = generator.Generate(request);

                if (!result.IsAccepted)
                {
                    failureReason = "[1] 시드 " + seed + " 가 거부됐다: " + result.RejectionReason;
                    return false;
                }

                // ── 규칙 15 — Decoration 스트림을 쓰지 않았는가 ─────────────
                if (result.DecorationDrawCount != 0)
                {
                    failureReason = "[2] Decoration 스트림을 " + result.DecorationDrawCount +
                        "번 썼다(규칙 15에 따라 0번이어야 한다).";
                    return false;
                }

                MapDefinition definition = result.Definition;
                int width = definition.Width;
                int height = definition.Height;
                int centerCol = (width - 1) / 2;
                int centerRow = (height - 1) / 2;

                // ── 회전 대칭 전수 확인 ─────────────────────────────────────
                if (!TryVerifyRotationalSymmetry(definition, out string symmetryReason))
                {
                    failureReason = "[3] 시드 " + seed + ": " + symmetryReason;
                    return false;
                }

                for (int index = 0; index < definition.TileCount; index++)
                {
                    if (definition.Tiles[index] != TileKind.Blocked) continue;

                    // 짝 없는 6칸은 유형과 무관한 고정값이라 장애물로 세지 않는다.
                    if (unpairedTiles.Contains(index)) continue;

                    obstacleTotal++;

                    int col = index % width;
                    int row = index / width;

                    // ── 금지 대역: 0~2행과 그 회전 상대 ─────────────────────
                    SymmetricMapBuilder.RotateCoord(col, row, width, height, out int rc, out int rr);

                    if (row <= ProtectedTopRowMax || rr <= ProtectedTopRowMax)
                    {
                        failureReason = "[4] 시드 " + seed + ": 금지 대역에 장애물이 있다(" +
                            col + "," + row + ").";
                        return false;
                    }

                    // ── 중앙선 규칙 ─────────────────────────────────────────
                    if (row == centerRow)
                    {
                        if (col == centerCol)
                        {
                            failureReason = "[5] 시드 " + seed + ": 회전 고정점 (" + centerCol + "," +
                                centerRow + ") 에 장애물이 있다.";
                            return false;
                        }

                        if ((col & 1) == 0)
                        {
                            failureReason = "[5] 시드 " + seed + ": 중앙선이 아닌 짝수 열 " + centerRow +
                                "행에 장애물이 있다(" + col + "," + row + ").";
                            return false;
                        }

                        // 중앙선 장애물은 반드시 대응쌍으로만 놓인다.
                        if (definition.Tiles[rr * width + rc] != TileKind.Blocked)
                        {
                            failureReason = "[5] 시드 " + seed + ": 중앙선 장애물에 짝이 없다(" +
                                col + "," + row + ").";
                            return false;
                        }
                    }
                }

                // ── 같은 시드 -> 같은 맵 ────────────────────────────────────
                MapGenerationResult again = generator.Generate(request);
                if (!TryCompareDefinitions(definition, again.Definition, out string diffReason))
                {
                    failureReason = "[6] 시드 " + seed + " 를 다시 만들었더니 다른 맵이 나왔다: " + diffReason;
                    return false;
                }
            }

            // ── 통계: 기대 장애물 수 약 30 ──────────────────────────────────
            if (obstacleTotal != SelfCheckExpectedObstacleTotal)
            {
                failureReason = "[7] 시드 " + SelfCheckSeedCount + "개의 장애물 총합이 기대값 " +
                    SelfCheckExpectedObstacleTotal + " 과 다르다(실제 " + obstacleTotal + ", 평균 " +
                    ((double)obstacleTotal / SelfCheckSeedCount) + ").";
                return false;
            }

            // 총합이 딱 맞더라도 "평균이 30 근처인가"를 사람이 읽을 수 있는 형태로 한 번 더 본다.
            // 뒤에 상수만 갈아 끼우고 넘어가는 일을 막기 위한 이중 확인이다.
            double mean = (double)obstacleTotal / SelfCheckSeedCount;
            if (mean < 27.0 || mean > 33.0)
            {
                failureReason = "[7] 장애물 평균이 30 근처가 아니다(실제 " + mean + ").";
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
                throw new InvalidOperationException("ObstacleOpenGenerator 자기 검증 실패: " + failureReason);
            }
        }
    }
}
