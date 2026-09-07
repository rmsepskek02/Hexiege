// ============================================================================
// NeutralMineSampler.cs
// 중립 광산을 어디에 놓을지 뽑는 공용 규칙(규칙 3). 맵 유형이 무엇이든 이 코드 하나를 쓴다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 규칙 3 이 정한 순서 그대로다
// ─────────────────────────────────────────────────────────────────────────────
//   1. 놓을 수 없는 칸을 먼저 뺀다
//        · 성 / 시작 광산 / 양 팀의 보호 초기 건설 타일   (InitialMapStateEvaluator)
//        · 막힌 타일                                        (SymmetricMapBuilder 가 아는 값)
//        · 유형별 광산 금지 구역                            (IMapArchetypeConstraints)
//   2. 남은 칸을 180도 회전 대응쌍으로 묶고, 두 칸의 row-major 인덱스 중
//      작은 쪽을 그 쌍의 대표로 삼는다. 대표만 모으면 같은 쌍을 앞뒤로 두 번
//      세는 일이 저절로 사라진다.
//   3. MinePlacement 스트림으로 필요한 수만큼 서로 다른 대표를 균등하게 고른다.
//      중앙이라서 더 잘 뽑히거나 가장자리라서 덜 뽑히는 가중치는 없다.
//   4. 광산 개수가 홀수면, 대응쌍 선택과는 별개로 회전 중심 (5,10) 에 단독 광산을 하나 더 놓는다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 일부러 넣지 않은 것
// ─────────────────────────────────────────────────────────────────────────────
//   · 광산끼리의 최소 간격 / 인접 금지 필터가 없다. 규칙 3은 광산이 서로 붙어
//     군집을 이루는 것을 허용한다. "보기 좋으라고" 간격 필터를 넣으면 확률 분포가
//     규칙과 달라지므로 넣지 않는다.
//   · 자리를 못 찾았을 때 조금씩 옮겨 보는 「수선」 경로가 없다. 규칙 6이
//     이동·수선을 금지하고 시도 전체를 버리라고 정한다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 3 · 규칙 6
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 중립 광산 배치 규칙(규칙 3)의 단일 구현. 맵 유형과 무관하게 이 클래스만 쓴다.
    /// </summary>
    public static class NeutralMineSampler
    {
        /// <summary>
        /// 서로 다른 것을 뽑을 때 같은 것이 또 나와 다시 뽑는 횟수의 상한.
        /// 후보가 뽑을 개수보다 넉넉하면 실제로는 몇 번 안에 끝나며, 이 상한은
        /// 무한 루프로 게임이 멈추는 최악의 경우만 막는 안전장치다.
        /// </summary>
        public const int MaxRedrawCount = 1000;

        // ====================================================================
        // 1. 후보 대응쌍 모으기
        // ====================================================================

        /// <summary>
        /// 중립 광산을 놓을 수 있는 대응쌍의 대표 인덱스를 모은다.
        /// 대표는 쌍을 이루는 두 칸의 row-major 인덱스 중 작은 쪽이며,
        /// 결과는 항상 인덱스 오름차순이라 같은 입력이면 언제나 같은 순서가 나온다.
        /// </summary>
        /// <param name="builder">지형과 성·시작 광산이 이미 놓인 builder</param>
        /// <param name="initialState">성·시작 광산만 놓인 상태로 계산한 초기 상태</param>
        /// <param name="constraints">이 시도에서 확정된 유형별 제약</param>
        /// <param name="results">결과를 담을 목록(안에서 먼저 비운다)</param>
        public static void CollectCandidatePairs(SymmetricMapBuilder builder,
            InitialMapStateEvaluator initialState, IMapArchetypeConstraints constraints,
            List<int> results)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (initialState == null) throw new ArgumentNullException(nameof(initialState));
            if (constraints == null) throw new ArgumentNullException(nameof(constraints));
            if (results == null) throw new ArgumentNullException(nameof(results));

            results.Clear();

            // ProtectedTiles 는 개수와 열거만 되는 형태라 Contains 가 없다.
            // 231칸을 훑으며 매번 처음부터 뒤지지 않도록 한 번만 HashSet 으로 옮긴다.
            HashSet<int> protectedTiles = ToHashSet(initialState.ProtectedTiles);

            for (int row = 0; row < builder.Height; row++)
            {
                for (int col = 0; col < builder.Width; col++)
                {
                    // 회전 중심은 자기 자신이 회전 상대라 "쌍"이 아니다.
                    // 홀수 개일 때만 쓰는 단독 자리이므로 후보 목록에는 넣지 않는다.
                    if (builder.IsCenter(col, row)) continue;

                    // 회전 상대가 격자 밖인 6칸은 항상 막힌 타일이라 애초에 후보가 아니다.
                    if (!builder.HasRotationPartner(col, row)) continue;

                    builder.RotateCoord(col, row, out int rotatedCol, out int rotatedRow);

                    int index = ToIndex(builder, col, row);
                    int rotatedIndex = ToIndex(builder, rotatedCol, rotatedRow);

                    // 대표(작은 쪽)일 때만 진행한다. 큰 쪽에서 다시 만나면 그냥 넘긴다.
                    // 이것이 규칙 3의 "역순 중복 제거"다.
                    if (index > rotatedIndex) continue;

                    // 쌍은 두 자리에 함께 놓이므로 양쪽 모두 놓을 수 있어야 후보가 된다.
                    if (!IsEligible(builder, initialState, constraints, protectedTiles, col, row, index)) continue;
                    if (!IsEligible(builder, initialState, constraints, protectedTiles,
                            rotatedCol, rotatedRow, rotatedIndex)) continue;

                    results.Add(index);
                }
            }
        }

        // ====================================================================
        // 2. 뽑아서 놓기
        // ====================================================================

        /// <summary>
        /// 중립 광산을 규칙 3대로 뽑아 builder 에 놓는다.
        /// 자리를 만들 수 없으면 놓다 만 상태를 남기지 않고 false 를 돌려주며,
        /// 부르는 쪽은 그 시도를 통째로 버린다(규칙 6).
        /// </summary>
        /// <param name="builder">기록 대상 builder</param>
        /// <param name="initialState">성·시작 광산만 놓인 상태로 계산한 초기 상태</param>
        /// <param name="constraints">이 시도에서 확정된 유형별 제약</param>
        /// <param name="neutralMineCount">놓을 중립 광산 개수(0 이상)</param>
        /// <param name="minePlacementStream">MinePlacement 난수 스트림</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 놓았으면 true</returns>
        public static bool TrySample(SymmetricMapBuilder builder, InitialMapStateEvaluator initialState,
            IMapArchetypeConstraints constraints, int neutralMineCount, MapRandom minePlacementStream,
            out string failureReason)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (initialState == null) throw new ArgumentNullException(nameof(initialState));
            if (constraints == null) throw new ArgumentNullException(nameof(constraints));
            if (minePlacementStream == null) throw new ArgumentNullException(nameof(minePlacementStream));

            if (neutralMineCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(neutralMineCount), "중립 광산 개수는 0 이상이어야 한다.");
            }

            if (neutralMineCount == 0)
            {
                failureReason = null;
                return true;
            }

            // 짝수 부분은 대응쌍으로, 남는 하나는 회전 중심에 놓는다(규칙 3 의 4번).
            int pairCount = neutralMineCount / 2;
            bool needsCenterMine = (neutralMineCount % 2) == 1;

            if (needsCenterMine && !IsCenterEligible(builder, initialState, constraints))
            {
                failureReason = "중립 광산 개수가 홀수인데 회전 중심 (" +
                    builder.CenterCol + "," + builder.CenterRow + ") 에 단독 광산을 놓을 수 없다.";
                return false;
            }

            var candidates = new List<int>();
            CollectCandidatePairs(builder, initialState, constraints, candidates);

            if (candidates.Count < pairCount)
            {
                failureReason = "중립 광산 대응쌍 후보가 모자란다(필요 " + pairCount +
                    "쌍, 후보 " + candidates.Count + "쌍).";
                return false;
            }

            var chosenSlots = new List<int>(pairCount);
            if (!TryPickDistinctSlots(minePlacementStream, candidates.Count, pairCount, chosenSlots, out failureReason))
            {
                return false;
            }

            for (int i = 0; i < chosenSlots.Count; i++)
            {
                int index = candidates[chosenSlots[i]];
                builder.SetNeutralMinePair(index % builder.Width, index / builder.Width);
            }

            if (needsCenterMine)
            {
                builder.SetCenterNeutralMine();
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 0 이상 upperExclusive 미만에서 서로 다른 값을 count 개 균등하게 뽑는다.
        /// 이미 나온 값이 또 나오면 "같은 확률 분포에서 다시 뽑는"다(규칙 6의 rejection sampling).
        /// </summary>
        /// <param name="stream">쓸 난수 스트림</param>
        /// <param name="upperExclusive">뽑을 수 있는 값의 개수</param>
        /// <param name="count">뽑을 개수</param>
        /// <param name="results">결과를 담을 목록(안에서 먼저 비운다)</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>다 뽑았으면 true</returns>
        public static bool TryPickDistinctSlots(MapRandom stream, int upperExclusive, int count,
            List<int> results, out string failureReason)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (results == null) throw new ArgumentNullException(nameof(results));

            results.Clear();

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "뽑을 개수는 0 이상이어야 한다.");
            }

            if (count > upperExclusive)
            {
                failureReason = "서로 다른 값을 " + count + "개 뽑아야 하는데 후보가 " +
                    upperExclusive + "개뿐이다.";
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                int redraws = 0;
                while (true)
                {
                    int pick = stream.NextInt(upperExclusive);
                    if (!results.Contains(pick))
                    {
                        results.Add(pick);
                        break;
                    }

                    redraws++;
                    if (redraws > MaxRedrawCount)
                    {
                        failureReason = "서로 다른 값을 뽑으려다 다시 뽑기 상한(" +
                            MaxRedrawCount + "회)을 넘었다.";
                        return false;
                    }
                }
            }

            failureReason = null;
            return true;
        }

        // ====================================================================
        // 내부 도우미
        // ====================================================================

        /// <summary> 열·행을 row-major 인덱스로 바꾼다. </summary>
        /// <param name="builder">격자 크기를 아는 builder</param>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>row-major 인덱스</returns>
        private static int ToIndex(SymmetricMapBuilder builder, int col, int row)
        {
            return row * builder.Width + col;
        }

        /// <summary>
        /// 그 칸 하나에 중립 광산을 놓을 수 있는지 확인한다(규칙 3 의 1번 제외 목록).
        /// </summary>
        /// <param name="builder">지형을 아는 builder</param>
        /// <param name="initialState">초기 상태 계산기</param>
        /// <param name="constraints">유형별 제약</param>
        /// <param name="protectedTiles">보호 타일 집합(미리 만들어 둔 것)</param>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="index">그 칸의 row-major 인덱스</param>
        /// <returns>놓을 수 있으면 true</returns>
        private static bool IsEligible(SymmetricMapBuilder builder, InitialMapStateEvaluator initialState,
            IMapArchetypeConstraints constraints, HashSet<int> protectedTiles, int col, int row, int index)
        {
            // 막힌 타일에는 아무것도 놓지 않는다.
            if (builder.GetTile(col, row) == TileKind.Blocked) return false;

            // 보호 타일 = 성 · 시작 광산 · 양 팀의 보호 초기 건설 타일.
            // 이 한 집합이 규칙 3의 앞 세 항목을 전부 덮는다.
            if (protectedTiles.Contains(index)) return false;

            // 이미 광산이 있는 칸(시작 광산)도 제외한다. 위 보호 집합과 겹치지만,
            // 「광산 위에 광산」이라는 뜻이 명시적으로 막혀 있는 편이 읽기 쉽다.
            if (initialState.GetMineKind(index) != MineKind.None) return false;

            // 유형별 광산 금지 구역.
            if (constraints.IsNeutralMineForbidden(col, row)) return false;

            return true;
        }

        /// <summary>
        /// 회전 중심에 단독 중립 광산을 놓을 수 있는지 확인한다.
        /// </summary>
        /// <param name="builder">지형을 아는 builder</param>
        /// <param name="initialState">초기 상태 계산기</param>
        /// <param name="constraints">유형별 제약</param>
        /// <returns>놓을 수 있으면 true</returns>
        private static bool IsCenterEligible(SymmetricMapBuilder builder,
            InitialMapStateEvaluator initialState, IMapArchetypeConstraints constraints)
        {
            HashSet<int> protectedTiles = ToHashSet(initialState.ProtectedTiles);
            int index = ToIndex(builder, builder.CenterCol, builder.CenterRow);

            return IsEligible(builder, initialState, constraints, protectedTiles,
                builder.CenterCol, builder.CenterRow, index);
        }

        /// <summary> 읽기 전용 모음을 빠른 조회용 집합으로 옮긴다. </summary>
        /// <param name="source">원본 모음</param>
        /// <returns>같은 값을 담은 집합</returns>
        private static HashSet<int> ToHashSet(IReadOnlyCollection<int> source)
        {
            var result = new HashSet<int>();
            foreach (int value in source)
            {
                result.Add(value);
            }
            return result;
        }

        // ====================================================================
        // 자기 검증 — 이 프로젝트에는 유닛 테스트 어셈블리가 없다
        // ====================================================================
        //
        // 🔴 기대값은 "그렇게 나오면 좋겠다"가 아니라 규칙 문서에 적힌 값이거나
        //    격자 기하에서 그대로 따라 나오는 값이다. 검사가 실패하면 검사를 고치지 말고
        //    구현과 규칙 중 어느 쪽이 어긋났는지부터 판단할 것.

        /// <summary>
        /// 규격 맵(11x21)에서 규칙 3의 후보 계산과 배치가 규칙대로인지 확인한다.
        /// 실패하면 사유를 채우고 false 를 돌려준다.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            // ── 1. 후보 대응쌍 수와 보호 타일 수 ────────────────────────────
            //
            // 231칸에서 회전 중심 1칸과 짝 없는 6칸을 빼면 224칸, 곧 112쌍이다.
            // 규칙 2의 보호 타일이 24칸(= 12쌍)이므로 남는 후보는 100쌍이어야 한다.
            // 이 24 는 InitialMapStateEvaluator 가 규격 배치에서 실측한 값과 같다.
            const int expectedProtectedTileCount = 24;
            const int expectedCandidatePairCount = 100;

            var sides = new[] { MapStartingMineSide.CaseA, MapStartingMineSide.CaseB };
            var candidates = new List<int>();

            for (int s = 0; s < sides.Length; s++)
            {
                MapStartingMineSide side = sides[s];

                SymmetricMapBuilder builder = CreateCanonicalBuilder(side);
                InitialMapStateEvaluator initialState = CreateCanonicalInitialState(side);
                IMapArchetypeConstraints constraints =
                    MapArchetypeConstraints.CreateUnrestricted(MapType.FullyOpen, builder.Width);

                if (initialState.ProtectedTiles.Count != expectedProtectedTileCount)
                {
                    failureReason = "[1] 경우 " + side + " 의 보호 타일이 " +
                        expectedProtectedTileCount + "칸이 아니다(실제 " +
                        initialState.ProtectedTiles.Count + "칸).";
                    return false;
                }

                CollectCandidatePairs(builder, initialState, constraints, candidates);

                if (candidates.Count != expectedCandidatePairCount)
                {
                    failureReason = "[1] 경우 " + side + " 의 후보 대응쌍이 " +
                        expectedCandidatePairCount + "쌍이 아니다(실제 " + candidates.Count + "쌍).";
                    return false;
                }

                // 후보가 오름차순이고, 대표가 정말 "작은 쪽"인지 확인한다.
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (i > 0 && candidates[i] <= candidates[i - 1])
                    {
                        failureReason = "[1] 후보 목록이 오름차순이 아니다(자리 " + i + ").";
                        return false;
                    }

                    int index = candidates[i];
                    SymmetricMapBuilder.RotateCoord(index % builder.Width, index / builder.Width,
                        builder.Width, builder.Height, out int rc, out int rr);

                    if (index >= rr * builder.Width + rc)
                    {
                        failureReason = "[1] 대표가 대응쌍 중 작은 쪽이 아니다(인덱스 " + index + ").";
                        return false;
                    }
                }
            }

            // ── 2. 개수 1~6 배치 결과 ───────────────────────────────────────
            //
            // 홀수면 회전 중심 단독 광산이 정확히 하나 들어가고, 나머지는 전부
            // 회전 대응쌍이어야 한다.
            for (int mineCount = 1; mineCount <= 6; mineCount++)
            {
                SymmetricMapBuilder builder = CreateCanonicalBuilder(MapStartingMineSide.CaseA);
                InitialMapStateEvaluator initialState = CreateCanonicalInitialState(MapStartingMineSide.CaseA);
                IMapArchetypeConstraints constraints =
                    MapArchetypeConstraints.CreateUnrestricted(MapType.FullyOpen, builder.Width);

                var stream = new MapRandom(0x51EDCAFEUL + (ulong)mineCount);

                if (!TrySample(builder, initialState, constraints, mineCount, stream, out string sampleReason))
                {
                    failureReason = "[2] 광산 " + mineCount + "개 배치가 실패했다: " + sampleReason;
                    return false;
                }

                MapDefinition definition = builder.Build();
                int centerIndex = builder.CenterRow * builder.Width + builder.CenterCol;

                if (definition.NeutralMines.Count != mineCount)
                {
                    failureReason = "[2] 중립 광산이 " + mineCount + "개가 아니다(실제 " +
                        definition.NeutralMines.Count + "개).";
                    return false;
                }

                var placed = new HashSet<int>(definition.NeutralMines);
                if (placed.Count != mineCount)
                {
                    failureReason = "[2] 같은 타일에 중립 광산이 겹쳤다(광산 " + mineCount + "개).";
                    return false;
                }

                bool hasCenter = placed.Contains(centerIndex);
                bool shouldHaveCenter = (mineCount % 2) == 1;

                if (hasCenter != shouldHaveCenter)
                {
                    failureReason = "[2] 광산 " + mineCount + "개에서 중심 단독 광산 여부가 어긋난다(기대 " +
                        shouldHaveCenter + ", 실제 " + hasCenter + ").";
                    return false;
                }

                foreach (int index in placed)
                {
                    if (index == centerIndex) continue;

                    SymmetricMapBuilder.RotateCoord(index % builder.Width, index / builder.Width,
                        builder.Width, builder.Height, out int rc, out int rr);
                    int rotatedIndex = rr * builder.Width + rc;

                    if (!placed.Contains(rotatedIndex))
                    {
                        failureReason = "[2] 광산 " + mineCount + "개에서 회전 짝이 없는 광산이 있다(인덱스 " +
                            index + ").";
                        return false;
                    }
                }
            }

            // ── 3. 같은 스트림이면 같은 결과 ────────────────────────────────
            {
                MapDefinition first = SampleOnCanonical(5, 0xABCDEF01UL);
                MapDefinition second = SampleOnCanonical(5, 0xABCDEF01UL);

                if (first.NeutralMines.Count != second.NeutralMines.Count)
                {
                    failureReason = "[3] 같은 시드인데 광산 개수가 다르다.";
                    return false;
                }

                for (int i = 0; i < first.NeutralMines.Count; i++)
                {
                    if (first.NeutralMines[i] != second.NeutralMines[i])
                    {
                        failureReason = "[3] 같은 시드인데 광산 자리가 다르다(자리 " + i + ").";
                        return false;
                    }
                }
            }

            // ── 4. 음성 대조 ────────────────────────────────────────────────
            //
            // 후보를 2쌍만 남겨 두고 3쌍이 필요한 개수(6개)를 요구하면 반드시 거부돼야 한다.
            // 이 검사가 없으면 "무엇이든 통과시키는 구현"과 올바른 구현을 구별할 수 없다.
            {
                SymmetricMapBuilder builder = CreateCanonicalBuilder(MapStartingMineSide.CaseA);
                InitialMapStateEvaluator initialState = CreateCanonicalInitialState(MapStartingMineSide.CaseA);
                IMapArchetypeConstraints constraints =
                    MapArchetypeConstraints.CreateUnrestricted(MapType.FullyOpen, builder.Width);

                var all = new List<int>();
                CollectCandidatePairs(builder, initialState, constraints, all);

                for (int i = 2; i < all.Count; i++)
                {
                    int index = all[i];
                    builder.SetPair(index % builder.Width, index / builder.Width, TileKind.Blocked);
                }

                var left = new List<int>();
                CollectCandidatePairs(builder, initialState, constraints, left);

                if (left.Count != 2)
                {
                    failureReason = "[4] 음성 대조 준비가 어긋났다(남은 후보 " + left.Count + "쌍, 기대 2쌍).";
                    return false;
                }

                if (TrySample(builder, initialState, constraints, 6, new MapRandom(7UL), out _))
                {
                    failureReason = "[4] 후보가 2쌍뿐인데 광산 6개 배치가 성공했다.";
                    return false;
                }

                if (!TrySample(builder, initialState, constraints, 4, new MapRandom(7UL), out string okReason))
                {
                    failureReason = "[4] 후보 2쌍으로 광산 4개는 놓을 수 있어야 하는데 실패했다: " + okReason;
                    return false;
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
                throw new InvalidOperationException("NeutralMineSampler 자기 검증 실패: " + failureReason);
            }
        }

        /// <summary> 자기 검증용 — 성·시작 광산만 놓인 규격 builder 를 만든다. </summary>
        /// <param name="side">시작 광산 배치 경우</param>
        /// <returns>새 builder</returns>
        private static SymmetricMapBuilder CreateCanonicalBuilder(MapStartingMineSide side)
        {
            var builder = new SymmetricMapBuilder();
            MapArchetypeGeneratorBase.ApplyCommonLayout(builder, side);
            return builder;
        }

        /// <summary> 자기 검증용 — 같은 규격 배치로 초기 상태 계산기를 만든다. </summary>
        /// <param name="side">시작 광산 배치 경우</param>
        /// <returns>초기 상태 계산기</returns>
        private static InitialMapStateEvaluator CreateCanonicalInitialState(MapStartingMineSide side)
        {
            return new InitialMapStateEvaluator(CreateCanonicalBuilder(side).Build());
        }

        /// <summary> 자기 검증용 — 규격 맵에 광산을 놓고 완성된 정의를 돌려준다. </summary>
        /// <param name="mineCount">놓을 중립 광산 개수</param>
        /// <param name="seed">MinePlacement 스트림 시드</param>
        /// <returns>완성된 맵 정의</returns>
        private static MapDefinition SampleOnCanonical(int mineCount, ulong seed)
        {
            SymmetricMapBuilder builder = CreateCanonicalBuilder(MapStartingMineSide.CaseA);
            InitialMapStateEvaluator initialState = CreateCanonicalInitialState(MapStartingMineSide.CaseA);
            IMapArchetypeConstraints constraints =
                MapArchetypeConstraints.CreateUnrestricted(MapType.FullyOpen, builder.Width);

            if (!TrySample(builder, initialState, constraints, mineCount, new MapRandom(seed), out string reason))
            {
                throw new InvalidOperationException("자기 검증 준비 중 광산 배치에 실패했다: " + reason);
            }

            return builder.Build();
        }
    }
}
