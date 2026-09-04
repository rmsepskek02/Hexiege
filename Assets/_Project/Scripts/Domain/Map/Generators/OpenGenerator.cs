// ============================================================================
// OpenGenerator.cs
// 완전개방형(규칙 4) 맵 생성기.
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 유형이 하는 일 — 그리고 하지 않는 일
// ─────────────────────────────────────────────────────────────────────────────
//   ② 이 유형이 추가하는 차단 지형은 없다.
//      "장애물을 아주 조금 놓는다"가 아니라 아예 놓지 않는다는 뜻이다.
//      완성된 맵에서 막힌 칸은 짝 없는 6칸뿐이며, 그 6칸은 유형과 무관하게
//      SymmetricMapBuilder 가 만들어질 때 이미 고정한 것이다.
//      (회전 상대가 격자 밖으로 나가는 칸이라 대칭을 지킬 수 없어 막아 둔 자리다.)
//
//   ③ 허용 중립 광산 수는 1~6 이다.
//   ④ 광산 금지 구역 없음 — 공통 제외(성·시작 광산·보호 건설 타일·막힌 칸)만 적용된다.
//   ⑤ 건설 불가 구역 없음.
//   ⑥ 필수 통로 없음.
//
//   그래서 이 클래스의 지형 그리기는 "아무것도 하지 않는다"가 정답이며,
//   Terrain 스트림에서 난수를 한 번도 뽑지 않는다.
//   🔴 뽑지 않는 것이 중요하다 — 여기서 의미 없이 한 번이라도 뽑으면 나중에 그 줄을
//      지웠을 때 같은 시드가 다른 맵을 만들게 된다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 4
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 완전개방형(규칙 4) 생성기. 차단 지형을 추가하지 않는다.
    /// </summary>
    public sealed class OpenGenerator : MapArchetypeGeneratorBase
    {
        /// <summary> 이 생성기가 만드는 맵 유형. </summary>
        public override MapType MapType => MapType.FullyOpen;

        /// <summary>
        /// 완전개방형의 지형 그리기. 추가할 차단 지형이 없으므로 아무것도 기록하지 않고,
        /// 금지 구역도 필수 통로도 없는 제약을 돌려준다. 거부되는 경우는 없다.
        /// </summary>
        /// <param name="builder">기록 대상 builder(성·시작 광산은 이미 놓여 있다)</param>
        /// <param name="terrainStream">Terrain 스트림. 이 유형은 한 번도 뽑지 않는다</param>
        /// <param name="constraints">이 시도에서 확정된 유형별 제약</param>
        /// <returns>항상 null(성공)</returns>
        protected override string TryApplyTerrain(SymmetricMapBuilder builder, MapRandom terrainStream,
            out IMapArchetypeConstraints constraints)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (terrainStream == null) throw new ArgumentNullException(nameof(terrainStream));

            constraints = MapArchetypeConstraints.CreateUnrestricted(MapType, builder.Width);
            return null;
        }

        // ====================================================================
        // 자기 검증 — 이 프로젝트에는 유닛 테스트 어셈블리가 없다
        // ====================================================================

        /// <summary>
        /// 완전개방형 결과가 규칙 4대로인지 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            var generator = new OpenGenerator();
            var sides = new[] { MapStartingMineSide.CaseA, MapStartingMineSide.CaseB };

            // 짝 없는 6칸이 정확히 어디인지는 builder 가 정의로 계산해 알려 준다.
            var unpairedTiles = new HashSet<int>(new SymmetricMapBuilder().UnpairedTileIndices);

            if (unpairedTiles.Count != 6)
            {
                failureReason = "[0] 짝 없는 칸이 6칸이 아니다(실제 " + unpairedTiles.Count + "칸).";
                return false;
            }

            for (int seed = 0; seed < 20; seed++)
            {
                for (int s = 0; s < sides.Length; s++)
                {
                    int mineCount = 1 + (seed % 6);

                    var request = new MapGenerationRequest
                    {
                        MapVersion = MapDefinition.CurrentMapVersion,
                        RootSeed = (ulong)seed,
                        AttemptIndex = 0,
                        StartingMineSide = sides[s],
                        NeutralMineCount = mineCount
                    };

                    MapGenerationResult result = generator.Generate(request);

                    if (!result.IsAccepted)
                    {
                        failureReason = "[1] 완전개방형이 거부됐다(시드 " + seed + "): " + result.RejectionReason;
                        return false;
                    }

                    // ── 2. Terrain 스트림을 한 번도 쓰지 않았는가 ────────────
                    if (result.TerrainDrawCount != 0)
                    {
                        failureReason = "[2] 완전개방형이 Terrain 스트림을 " +
                            result.TerrainDrawCount + "번 썼다(0번이어야 한다).";
                        return false;
                    }

                    // ── 3. 규칙 15 — Decoration 스트림을 쓰지 않았는가 ───────
                    if (result.DecorationDrawCount != 0)
                    {
                        failureReason = "[3] Decoration 스트림을 " +
                            result.DecorationDrawCount + "번 썼다(규칙 15에 따라 0번이어야 한다).";
                        return false;
                    }

                    MapDefinition definition = result.Definition;

                    // ── 4. 막힌 칸이 짝 없는 6칸뿐인가 ──────────────────────
                    for (int index = 0; index < definition.TileCount; index++)
                    {
                        bool blocked = definition.Tiles[index] == TileKind.Blocked;
                        bool shouldBeBlocked = unpairedTiles.Contains(index);

                        if (blocked != shouldBeBlocked)
                        {
                            failureReason = "[4] 막힌 칸이 짝 없는 6칸과 다르다(인덱스 " + index +
                                ", 실제 막힘 " + blocked + ").";
                            return false;
                        }
                    }

                    // ── 5. 회전 대칭 전수 확인 ──────────────────────────────
                    if (!TryVerifyRotationalSymmetry(definition, out string symmetryReason))
                    {
                        failureReason = "[5] " + symmetryReason;
                        return false;
                    }

                    // ── 6. 중립 광산 개수 ───────────────────────────────────
                    if (definition.NeutralMines.Count != mineCount)
                    {
                        failureReason = "[6] 중립 광산이 " + mineCount + "개가 아니다(실제 " +
                            definition.NeutralMines.Count + "개).";
                        return false;
                    }

                    // ── 7. 같은 시드 -> 같은 맵 ─────────────────────────────
                    MapGenerationResult again = generator.Generate(request);
                    if (!TryCompareDefinitions(definition, again.Definition, out string diffReason))
                    {
                        failureReason = "[7] 같은 시드인데 다른 맵이 나왔다: " + diffReason;
                        return false;
                    }
                }
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
                throw new InvalidOperationException("OpenGenerator 자기 검증 실패: " + failureReason);
            }
        }
    }
}
