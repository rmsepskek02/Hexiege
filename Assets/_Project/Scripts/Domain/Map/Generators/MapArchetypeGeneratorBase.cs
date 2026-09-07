// ============================================================================
// MapArchetypeGeneratorBase.cs
// 모든 맵 유형 생성기가 공유하는 뼈대. 유형마다 다른 것은 "지형을 어떻게 그리는가"
// 하나뿐이고, 나머지 순서는 전부 여기에 한 번만 적혀 있다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 여기서 한 번만 하는 일 (유형마다 베껴 쓰지 않는다)
// ─────────────────────────────────────────────────────────────────────────────
//   1. 난수 스트림 3개(Terrain / MinePlacement / Decoration)를 시도 번호로 새로 만든다
//   2. 성과 시작 광산을 규격 자리에 놓는다                     <- ApplyCommonLayout
//   3. 유형별 지형 그리기를 자식 클래스에게 맡긴다             <- TryApplyTerrain
//   4. 중립 광산을 규칙 3대로 흩뿌린다                          <- NeutralMineSampler
//   5. 맵 정의를 완성하고 상위 정보를 채워 돌려준다
//
//   3번이나 4번이 "이 시도는 못 쓰겠다"고 하면 반쯤 만든 맵을 손보지 않고
//   시도 전체를 거부한 결과를 돌려준다(규칙 6).
//
// ─────────────────────────────────────────────────────────────────────────────
// 성·시작 광산 규격 (규칙 1) — 이 값들이 이 파일에만 있다
// ─────────────────────────────────────────────────────────────────────────────
//   성        : Blue (5,19) / Red (5,1)
//   시작 광산 : 경우 A 는 Blue (3,19) / Red (7,1)
//               경우 B 는 Blue (7,19) / Red (3,1)
//
//   Red 쪽 좌표를 따로 적지 않는 이유: SymmetricMapBuilder 의 대응쌍 기록 API 가
//   180도 회전 자리에 반대 팀 것을 자동으로 놓아 준다. 두 자리를 따로 적으면
//   한쪽만 고쳐 대칭이 깨지는 사고가 언제든 생길 수 있다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 1 · 3 · 6 · 15
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 맵 유형 생성기의 공통 뼈대. 자식 클래스는 TryApplyTerrain 하나만 구현하면 된다.
    /// </summary>
    public abstract class MapArchetypeGeneratorBase : IMapArchetypeGenerator
    {
        // ====================================================================
        // 규칙 1 의 고정 좌표
        // ====================================================================

        /// <summary> 성이 놓이는 열. 격자 한가운데 열이다. </summary>
        public const int CastleCol = 5;

        /// <summary> Blue 성이 놓이는 행. 회전 자리인 1행에는 Red 성이 자동으로 놓인다. </summary>
        public const int BlueCastleRow = 19;

        /// <summary> Blue 시작 광산이 놓이는 행. 회전 자리인 1행에는 Red 것이 자동으로 놓인다. </summary>
        public const int BlueStartingMineRow = 19;

        /// <summary> 경우 A 에서 Blue 시작 광산이 놓이는 열. </summary>
        public const int StartingMineColCaseA = 3;

        /// <summary> 경우 B 에서 Blue 시작 광산이 놓이는 열. </summary>
        public const int StartingMineColCaseB = 7;

        /// <summary> 이 유형이 허용하는 중립 광산 최소 개수의 기본값(규칙 4 · 5 의 ③). </summary>
        public const int DefaultMinNeutralMineCount = 1;

        /// <summary> 이 유형이 허용하는 중립 광산 최대 개수의 기본값(규칙 4 · 5 의 ③). </summary>
        public const int DefaultMaxNeutralMineCount = 6;

        // ====================================================================
        // IMapArchetypeGenerator
        // ====================================================================

        /// <summary> 이 생성기가 만드는 맵 유형. </summary>
        public abstract MapType MapType { get; }

        /// <summary> 이 유형이 허용하는 중립 광산 최소 개수. 유형이 다르면 덮어쓴다. </summary>
        public virtual int MinNeutralMineCount => DefaultMinNeutralMineCount;

        /// <summary> 이 유형이 허용하는 중립 광산 최대 개수. 유형이 다르면 덮어쓴다. </summary>
        public virtual int MaxNeutralMineCount => DefaultMaxNeutralMineCount;

        /// <summary>
        /// 그 중립 광산 개수를 이 유형이 허용하는지 확인한다.
        /// 기본은 최소~최대 사이면 허용이며, "짝수만 허용" 같은 유형이 생기면 덮어쓴다.
        /// </summary>
        /// <param name="neutralMineCount">확인할 중립 광산 개수</param>
        /// <returns>허용하면 true</returns>
        public virtual bool IsNeutralMineCountAllowed(int neutralMineCount)
        {
            return neutralMineCount >= MinNeutralMineCount && neutralMineCount <= MaxNeutralMineCount;
        }

        /// <summary>
        /// 맵 생성을 한 번 시도한다. 각 단계의 순서는 이 메서드가 단일 소스다.
        /// </summary>
        /// <param name="request">이미 뽑혀 있는 입력값 묶음</param>
        /// <returns>시도 결과(거부되면 IsAccepted 가 false)</returns>
        public MapGenerationResult Generate(MapGenerationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.AttemptIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "시도 번호는 0 이상이어야 한다.");
            }

            // 🔴 여기서 예외를 던지는 이유: 중립 광산 개수는 경기 선택 단계가 이미
            //    이 유형의 허용 범위 안에서 뽑아 넘겨야 하는 값이다. 범위 밖이 왔다는 것은
            //    난수 결과가 나빴다는 뜻이 아니라 부르는 쪽 코드가 잘못됐다는 뜻이므로,
            //    조용히 "거부"로 처리해 재시도 루프에 숨기지 않는다.
            if (!IsNeutralMineCountAllowed(request.NeutralMineCount))
            {
                throw new ArgumentOutOfRangeException(nameof(request),
                    "이 유형(" + MapType + ")이 허용하지 않는 중립 광산 개수다(받은 값 " +
                    request.NeutralMineCount + ", 허용 " + MinNeutralMineCount + "~" + MaxNeutralMineCount + ").");
            }

            // ── 1. 시도 전용 난수 스트림 3개 ────────────────────────────────
            // 반드시 시도마다 새로 만든다. 앞 시도의 스트림을 이어 쓰면 "같은 시드면
            // 같은 맵"이라는 약속이 깨진다(MapRandomStreams 의 보장 3번).
            MapRandom terrainStream = MapRandomStreams.CreateAttemptStream(
                request.MapVersion, request.RootSeed, MapRandomStreams.Terrain, request.AttemptIndex);
            MapRandom minePlacementStream = MapRandomStreams.CreateAttemptStream(
                request.MapVersion, request.RootSeed, MapRandomStreams.MinePlacement, request.AttemptIndex);

            // 규칙 15: 최초 구현의 장식은 항상 빈 목록이며 이 스트림에서 한 번도 뽑지 않는다.
            // 스트림을 아예 만들지 않는 대신 만들어만 두는 이유는, 나중에 장식을 넣을 때
            // 앞 스트림들의 뽑기 횟수가 달라져 과거 시드가 다른 맵이 되는 일을 막기 위해서다.
            MapRandom decorationStream = MapRandomStreams.CreateAttemptStream(
                request.MapVersion, request.RootSeed, MapRandomStreams.Decoration, request.AttemptIndex);

            // ── 2. 성 · 시작 광산 (규칙 1) ──────────────────────────────────
            var builder = new SymmetricMapBuilder();
            ApplyCommonLayout(builder, request.StartingMineSide);

            // ── 3. 유형별 지형 ──────────────────────────────────────────────
            string terrainRejection = TryApplyTerrain(builder, terrainStream, out IMapArchetypeConstraints constraints);
            if (terrainRejection != null)
            {
                return MapGenerationResult.Reject(terrainRejection, constraints,
                    terrainStream.DrawCount, minePlacementStream.DrawCount, decorationStream.DrawCount);
            }

            if (constraints == null)
            {
                throw new InvalidOperationException(
                    "지형 생성이 성공했는데 유형별 제약을 돌려주지 않았다(" + MapType + ").");
            }

            // ── 4. 중립 광산 (규칙 3) ───────────────────────────────────────
            InitialMapStateEvaluator initialState = CreateInitialStateProbe(builder, request.StartingMineSide);

            if (!NeutralMineSampler.TrySample(builder, initialState, constraints,
                    request.NeutralMineCount, minePlacementStream, out string mineRejection))
            {
                return MapGenerationResult.Reject(mineRejection, constraints,
                    terrainStream.DrawCount, minePlacementStream.DrawCount, decorationStream.DrawCount);
            }

            // ── 5. 완성 ─────────────────────────────────────────────────────
            MapDefinition definition = builder.Build();
            definition.MapVersion = request.MapVersion;
            definition.RootSeed = request.RootSeed;
            definition.MapType = MapType;
            definition.NeutralMineCount = request.NeutralMineCount;
            definition.TestModeFlag = request.TestModeFlag;
            definition.InitialGold = request.InitialGold;

            // Hash 는 여기서 채우지 않는다. 해시는 canonical 직렬화 바이트에 대해 계산해야
            // 하며, 그 계산은 맵을 내보내는 쪽(코디네이터)의 몫이다.

            return MapGenerationResult.Accept(definition, constraints,
                terrainStream.DrawCount, minePlacementStream.DrawCount, decorationStream.DrawCount);
        }

        // ====================================================================
        // 공통 배치 — 이 프로젝트에서 성과 시작 광산을 놓는 유일한 자리
        // ====================================================================

        /// <summary>
        /// 규칙 1의 성 2개와 시작 광산 2개를 놓는다. 대응쌍 API 를 쓰므로 한 번 부르면
        /// 양 팀 자리가 함께 채워진다.
        /// </summary>
        /// <param name="builder">기록 대상 builder</param>
        /// <param name="side">시작 광산 배치 경우(A 또는 B)</param>
        public static void ApplyCommonLayout(SymmetricMapBuilder builder, MapStartingMineSide side)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            // Blue 성을 (5,19) 에 놓으면 회전 자리인 (5,1) 에 Red 성이 자동으로 놓인다.
            builder.SetCastlePair(CastleCol, BlueCastleRow, TeamId.Blue);

            int startingMineCol = GetBlueStartingMineCol(side);
            builder.SetStartingMinePair(startingMineCol, BlueStartingMineRow, TeamId.Blue);
        }

        /// <summary>
        /// 그 경우에서 Blue 시작 광산이 놓이는 열을 돌려준다.
        /// Red 쪽 열은 회전으로 정해지므로 따로 적지 않는다.
        /// </summary>
        /// <param name="side">시작 광산 배치 경우</param>
        /// <returns>Blue 시작 광산의 열</returns>
        public static int GetBlueStartingMineCol(MapStartingMineSide side)
        {
            switch (side)
            {
                case MapStartingMineSide.CaseA: return StartingMineColCaseA;
                case MapStartingMineSide.CaseB: return StartingMineColCaseB;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side),
                        "시작 광산 배치 경우는 A 또는 B 뿐이다(받은 값: " + side + ").");
            }
        }

        // ====================================================================
        // 자식 클래스가 채우는 부분
        // ====================================================================

        /// <summary>
        /// 유형별 지형을 그린다. 성공하면 null 을 돌려주고, 이 시도를 버려야 하면
        /// 사람이 읽을 거부 사유 문자열을 돌려준다(예외를 던지지 않는다 — 거부는 정상 흐름이다).
        /// </summary>
        /// <param name="builder">기록 대상 builder(성·시작 광산은 이미 놓여 있다)</param>
        /// <param name="terrainStream">Terrain 스트림. 지형 관련 뽑기는 전부 이것으로만 한다</param>
        /// <param name="constraints">이 시도에서 확정된 유형별 제약(성공 시 반드시 채울 것)</param>
        /// <returns>성공이면 null, 거부면 사유</returns>
        protected abstract string TryApplyTerrain(SymmetricMapBuilder builder, MapRandom terrainStream,
            out IMapArchetypeConstraints constraints);

        // ====================================================================
        // 내부 도우미
        // ====================================================================

        /// <summary>
        /// 지금까지 그려진 지형과 성·시작 광산만 담긴 "검사용 사본"을 만들어
        /// 초기 상태 계산기를 돌려준다. 중립 광산 금지 대상(성·시작 광산·양 팀 보호
        /// 초기 건설 타일)을 여기서 얻는다.
        /// </summary>
        /// <param name="source">지금 만들고 있는 builder</param>
        /// <param name="side">시작 광산 배치 경우</param>
        /// <returns>보호 타일 등을 물어볼 수 있는 초기 상태 계산기</returns>
        // ────────────────────────────────────────────────────────────────────
        // 🔴 왜 사본을 따로 만드는가
        //   InitialMapStateEvaluator 는 완성된 MapDefinition 을 받아야 계산할 수 있는데,
        //   SymmetricMapBuilder 는 만드는 중인 정의를 밖으로 내주지 않는다(내주는 순간
        //   생성기가 타일 배열을 직접 만질 수 있게 되어 대칭 보장이 무너진다).
        //   그래서 같은 내용을 대응쌍 API 로 그대로 다시 기록한 별도 builder 를 만들어
        //   그 결과만 계산에 쓴다. 사본에는 중립 광산이 없으므로 "어디에 놓을 수 있는가"를
        //   묻기에 딱 맞는 상태다.
        //
        //   보호 타일 계산을 여기서 베껴 쓰지 않는 이유: 같은 계산이 생성기·검증기·
        //   런타임 초기 배치 세 곳에서 필요하고, 세 곳이 어긋나면 조용히 불공평한 맵이
        //   나온다. 그래서 계산은 InitialMapStateEvaluator 한 곳에만 둔다.
        // ────────────────────────────────────────────────────────────────────
        private static InitialMapStateEvaluator CreateInitialStateProbe(
            SymmetricMapBuilder source, MapStartingMineSide side)
        {
            var probe = new SymmetricMapBuilder();
            ApplyCommonLayout(probe, side);

            for (int row = 0; row < source.Height; row++)
            {
                for (int col = 0; col < source.Width; col++)
                {
                    TileKind kind = source.GetTile(col, row);
                    if (kind == TileKind.Normal) continue;

                    if (source.IsCenter(col, row))
                    {
                        probe.SetCenter(kind);
                        continue;
                    }

                    // 짝 없는 6칸은 두 builder 모두 생성자에서 이미 막힌 타일로 고정했다.
                    // 대응쌍 API 로는 기록할 수 없는 칸이므로 건너뛴다.
                    if (!source.HasRotationPartner(col, row)) continue;

                    probe.SetPair(col, row, kind);
                }
            }

            return new InitialMapStateEvaluator(probe.Build());
        }

        // ====================================================================
        // 자기 검증 공용 도우미 — 모든 유형 생성기의 자기 검증이 함께 쓴다
        // ====================================================================

        /// <summary>
        /// 완성된 맵이 180도 회전 대칭인지 전수 확인한다.
        /// 지형 · 성 · 시작 광산 · 중립 광산을 모두 본다.
        /// </summary>
        /// <param name="definition">확인할 맵 정의</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>대칭이면 true</returns>
        public static bool TryVerifyRotationalSymmetry(MapDefinition definition, out string failureReason)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            int width = definition.Width;
            int height = definition.Height;
            int centerIndex = ((height - 1) / 2) * width + (width - 1) / 2;

            // ── 지형 ─────────────────────────────────────────────────────────
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    SymmetricMapBuilder.RotateCoord(col, row, width, height, out int rc, out int rr);
                    if (!SymmetricMapBuilder.IsInsideGrid(rc, rr, width, height)) continue;

                    int index = row * width + col;
                    int rotatedIndex = rr * width + rc;

                    if (definition.Tiles[index] != definition.Tiles[rotatedIndex])
                    {
                        failureReason = "지형이 회전 짝과 다르다(" + col + "," + row + ").";
                        return false;
                    }
                }
            }

            // ── 성과 시작 광산: 회전 자리에 반대 팀 것이 있어야 한다 ─────────
            if (!TryVerifyPlacementSymmetry(definition, definition.Castles, width, height, "성", out failureReason))
            {
                return false;
            }

            if (!TryVerifyPlacementSymmetry(definition, definition.StartingMines, width, height, "시작 광산",
                    out failureReason))
            {
                return false;
            }

            // ── 중립 광산: 중심 단독을 빼면 모두 짝이 있어야 한다 ────────────
            var neutralMines = new HashSet<int>(definition.NeutralMines);
            foreach (int index in neutralMines)
            {
                if (index == centerIndex) continue;

                SymmetricMapBuilder.RotateCoord(index % width, index / width, width, height,
                    out int rc, out int rr);

                if (!neutralMines.Contains(rr * width + rc))
                {
                    failureReason = "중립 광산에 회전 짝이 없다(인덱스 " + index + ").";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 팀 전용 배치 목록이 회전 대칭인지 확인한다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="placements">확인할 배치 목록</param>
        /// <param name="width">격자 가로</param>
        /// <param name="height">격자 세로</param>
        /// <param name="label">실패 사유에 쓸 이름</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>대칭이면 true</returns>
        private static bool TryVerifyPlacementSymmetry(MapDefinition definition,
            List<MapObjectPlacement> placements, int width, int height, string label,
            out string failureReason)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                MapObjectPlacement placement = placements[i];

                SymmetricMapBuilder.RotateCoord(placement.TileIndex % width, placement.TileIndex / width,
                    width, height, out int rc, out int rr);
                int rotatedIndex = rr * width + rc;

                TeamId expectedTeam = SymmetricMapBuilder.RotateState(placement.Team);
                bool found = false;

                for (int j = 0; j < placements.Count; j++)
                {
                    if (placements[j].TileIndex == rotatedIndex && placements[j].Team == expectedTeam)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    failureReason = label + " 의 회전 짝이 없다(인덱스 " + placement.TileIndex +
                        ", 기대 자리 " + rotatedIndex + ").";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 두 맵 정의가 완전히 같은지 확인한다(같은 시드 재현 검사용).
        /// </summary>
        /// <param name="a">맵 정의 1</param>
        /// <param name="b">맵 정의 2</param>
        /// <param name="failureReason">다른 점(같으면 null)</param>
        /// <returns>같으면 true</returns>
        public static bool TryCompareDefinitions(MapDefinition a, MapDefinition b, out string failureReason)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));

            if (b == null)
            {
                failureReason = "비교 대상이 없다(두 번째 시도가 거부됐을 수 있다).";
                return false;
            }

            if (a.TileCount != b.TileCount)
            {
                failureReason = "격자 크기가 다르다.";
                return false;
            }

            for (int i = 0; i < a.TileCount; i++)
            {
                if (a.Tiles[i] != b.Tiles[i])
                {
                    failureReason = "지형이 다르다(인덱스 " + i + ").";
                    return false;
                }
            }

            if (a.NeutralMines.Count != b.NeutralMines.Count)
            {
                failureReason = "중립 광산 개수가 다르다.";
                return false;
            }

            for (int i = 0; i < a.NeutralMines.Count; i++)
            {
                if (a.NeutralMines[i] != b.NeutralMines[i])
                {
                    failureReason = "중립 광산 자리가 다르다(자리 " + i + ").";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }
    }
}
