// ============================================================================
// MapProjectionUseCase.cs
// 완성된 맵 설계도(MapDefinition)를 실제 경기용 격자(HexGrid)에 옮겨 붙이는 투영기.
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 클래스가 하는 일 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   MapPreparationUseCase(G 단계)가 만들어 낸 MapDefinition 은 "종이 위의 설계도"다.
//   숫자 배열과 인덱스 목록만 들어 있을 뿐, 게임이 실제로 쓰는 HexGrid/HexTile 은
//   아직 아무것도 모른다. 그 간극을 메우는 것이 이 클래스다.
//
//     설계도(MapDefinition)               실제 격자(HexGrid)
//     ───────────────────────             ────────────────────────────
//     Tiles[index]  (TileKind)     →      HexTile.TileKind
//     StartingMines (인덱스+팀)    →      HexTile.MineKind = BlueStart / RedStart
//     NeutralMines  (인덱스)       →      HexTile.MineKind = Neutral
//     Castles       (인덱스+팀)    →      좌표만 돌려준다(실제 건물 배치는 호출자 몫)
//
//   설계도는 타일을 "row-major 인덱스" 하나로 가리킨다(index = row * Width + col).
//   반면 HexGrid 는 큐브 좌표(HexCoord)로 타일을 찾는다. 그래서 투영이란 결국
//   「인덱스 → (col, row) → 큐브 좌표 → 그 타일의 상태 대입」의 반복이다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 투영 규칙 (TDD 「HexTile 런타임 상태 계약」)
// ─────────────────────────────────────────────────────────────────────────────
//   TileKind    ← MapDefinition.Tiles[index].  경기 중에는 바뀌지 않는다.
//   MineKind    ← 광산 배치 "목록"에서 투영한다.
//                 🔴 광산 위치를 담은 별도의 직렬화 원본을 병행 유지하지 않는다.
//                    목록이 유일한 원본이고, HexTile.MineKind 는 그 사본이다.
//   HasBuilding ← 이 클래스가 건드리지 않는다. 건물 배치·철거·파괴가 정하는
//                 동적 상태이며, 그 경로는 BuildingPlacementUseCase 하나뿐이다.
//   초기 소유권 ← MapDefinition 에 저장하지 않는다(TDD 「초기 소유권 단일 소스」).
//                 성과 시작 광산의 위치에서 파생되며, 실제로는 호출자가 그 두 위치에
//                 건물을 세울 때 BuildingPlacementUseCase 가 자기 타일 + 인접 타일의
//                 소유권을 칠하면서 자연히 만들어진다. InitialMapStateEvaluator(C 단계)가
//                 계산하는 것과 입력이 같으므로 결과도 같다.
//                 그래서 이 클래스는 소유권에 손대지 않고, 성·시작 광산의 "좌표"만
//                 결과로 돌려준다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 이 파일은 순수 C# 이다 — using UnityEngine 이 없다
// ─────────────────────────────────────────────────────────────────────────────
//   MapDefinition · HexGrid · HexTile · HexCoord · TeamId 는 전부 Domain 타입이고
//   Domain 은 Unity 를 참조하지 않는다. 그래서 이 파일도 Unity 없이 컴파일되고
//   실행된다 — 아래 TryRunSelfCheck 를 명령줄에서 그대로 돌려 투영 결과를
//   231칸 전부 대조할 수 있다. Unity 를 끌어들이는 순간 그 검증 수단이 사라지므로
//   로그(GameLog) 호출도 두지 않는다. 실패는 예외가 아니라 결과 값으로 돌려준다.
//
// 근거: _Tasks/2026-09-03/03_14_random-map-phase2-generator/Plan.md §4 H
//       TechnicalDesignDocument.md 「HexTile 런타임 상태 계약」·「초기 소유권 단일 소스」
//       GameSystemRules/GameSystemRules_RandomMap.md 규칙 1 · 규칙 10
//
// Application 레이어 — Domain 의존. Unity/Netcode/Infrastructure 직접 참조 없음.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// "어느 좌표에 어느 팀의 무엇이 놓이는가"를 담는 값. 성과 시작 광산에 쓴다.
    /// MapDefinition 의 MapObjectPlacement 가 타일 인덱스를 쓰는 것과 달리,
    /// 이쪽은 이미 큐브 좌표로 변환된 뒤이므로 호출자가 그대로 쓸 수 있다.
    /// </summary>
    public readonly struct MapTeamPlacement
    {
        /// <summary> 배치 좌표(큐브 좌표). </summary>
        public HexCoord Coord { get; }

        /// <summary> 소속 팀(Blue 또는 Red). </summary>
        public TeamId Team { get; }

        /// <summary> 배치 정보를 만든다. </summary>
        /// <param name="coord">배치 좌표</param>
        /// <param name="team">소속 팀</param>
        public MapTeamPlacement(HexCoord coord, TeamId team)
        {
            Coord = coord;
            Team = team;
        }
    }

    /// <summary>
    /// 투영 결과. 성공 여부와 함께, 호출자가 이어서 해야 할 일(성·시작 채굴소 배치)에
    /// 필요한 좌표 목록을 담는다.
    ///
    /// 🔴 실패는 예외가 아니라 이 객체의 IsSucceeded=false 로 돌아온다.
    ///    투영이 반쯤 진행된 상태로 격자가 남지 않도록, 실패 판정은 전부
    ///    "타일에 대입하기 전"에 끝낸다(아래 Project 의 1단계 검사).
    /// </summary>
    public sealed class MapProjectionResult
    {
        /// <summary> 투영에 성공했으면 true. </summary>
        public bool IsSucceeded { get; }

        /// <summary> 사람이 읽는 실패 사유(성공 시 null). </summary>
        public string FailureReason { get; }

        /// <summary> 실제로 TileKind 를 대입한 타일 수(성공 시 Width * Height). </summary>
        public int ProjectedTileCount { get; }

        /// <summary> 성 배치 좌표 목록(보통 Blue/Red 각 1개). </summary>
        public IReadOnlyList<MapTeamPlacement> Castles { get; }

        /// <summary> 팀 시작 광산 좌표 목록(보통 Blue/Red 각 1개). </summary>
        public IReadOnlyList<MapTeamPlacement> StartingMines { get; }

        /// <summary> 중립 광산 좌표 목록. </summary>
        public IReadOnlyList<HexCoord> NeutralMines { get; }

        /// <summary>
        /// 결과 객체를 만든다. 보통은 Success / Failure 정적 메서드를 쓴다.
        /// </summary>
        /// <param name="isSucceeded">성공 여부</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <param name="projectedTileCount">TileKind 를 대입한 타일 수</param>
        /// <param name="castles">성 배치 좌표 목록</param>
        /// <param name="startingMines">시작 광산 좌표 목록</param>
        /// <param name="neutralMines">중립 광산 좌표 목록</param>
        public MapProjectionResult(bool isSucceeded, string failureReason, int projectedTileCount,
            IReadOnlyList<MapTeamPlacement> castles, IReadOnlyList<MapTeamPlacement> startingMines,
            IReadOnlyList<HexCoord> neutralMines)
        {
            IsSucceeded = isSucceeded;
            FailureReason = failureReason;
            ProjectedTileCount = projectedTileCount;
            Castles = castles ?? EmptyPlacements;
            StartingMines = startingMines ?? EmptyPlacements;
            NeutralMines = neutralMines ?? EmptyCoords;
        }

        // 실패 결과가 null 목록을 들고 다니지 않도록 빈 목록을 재사용한다.
        // (호출자가 foreach 앞에서 null 검사를 하지 않아도 되게 하기 위함.)
        private static readonly MapTeamPlacement[] EmptyPlacements = new MapTeamPlacement[0];
        private static readonly HexCoord[] EmptyCoords = new HexCoord[0];

        /// <summary> 성공 결과를 만든다. </summary>
        /// <param name="projectedTileCount">TileKind 를 대입한 타일 수</param>
        /// <param name="castles">성 배치 좌표 목록</param>
        /// <param name="startingMines">시작 광산 좌표 목록</param>
        /// <param name="neutralMines">중립 광산 좌표 목록</param>
        /// <returns>성공 결과 객체</returns>
        public static MapProjectionResult Success(int projectedTileCount,
            IReadOnlyList<MapTeamPlacement> castles, IReadOnlyList<MapTeamPlacement> startingMines,
            IReadOnlyList<HexCoord> neutralMines)
        {
            return new MapProjectionResult(true, null, projectedTileCount,
                castles, startingMines, neutralMines);
        }

        /// <summary> 실패 결과를 만든다. </summary>
        /// <param name="failureReason">사람이 읽는 실패 사유</param>
        /// <returns>실패 결과 객체</returns>
        public static MapProjectionResult Failure(string failureReason)
        {
            return new MapProjectionResult(false, failureReason, 0, null, null, null);
        }

        /// <summary> 로그 한 줄로 요약한다. </summary>
        /// <returns>사람이 읽는 요약 문자열</returns>
        public override string ToString()
        {
            return "MapProjection " + (IsSucceeded ? "성공" : "실패") +
                " tiles=" + ProjectedTileCount +
                " castles=" + Castles.Count +
                " startMines=" + StartingMines.Count +
                " neutralMines=" + NeutralMines.Count +
                (FailureReason == null ? "" : " reason=" + FailureReason);
        }
    }

    /// <summary>
    /// MapDefinition 을 HexGrid 에 투영한다. 보관하는 상태가 없어 정적 메서드로 둔다
    /// (맵을 다시 로드해도 이전 투영의 흔적이 남을 여지를 애초에 없애기 위함).
    /// </summary>
    public static class MapProjectionUseCase
    {
        // ====================================================================
        // 진입점
        // ====================================================================

        /// <summary>
        /// 설계도를 격자에 투영한다. 격자의 모든 타일에 TileKind 와 MineKind 를 대입하고,
        /// 성·시작 광산·중립 광산의 좌표를 결과로 돌려준다.
        ///
        /// 🔴 실패 검사를 먼저 전부 끝낸 뒤에야 타일에 대입한다.
        ///    중간에 실패하면 격자가 "절반만 새 맵"인 상태로 남아 원인을 찾을 수 없게 된다.
        ///
        /// 🔴 HasBuilding 과 타일 소유권은 건드리지 않는다(위 파일 머리말의 투영 규칙 참조).
        /// </summary>
        /// <param name="definition">투영할 맵 설계도</param>
        /// <param name="grid">투영 대상 격자(이미 같은 크기로 생성돼 있어야 한다)</param>
        /// <returns>투영 결과(실패 시 IsSucceeded=false)</returns>
        public static MapProjectionResult Project(MapDefinition definition, HexGrid grid)
        {
            // ── 1단계: 대입하기 전에 모든 실패 조건을 걸러 낸다 ────────────────
            if (definition == null)
                return MapProjectionResult.Failure("맵 정의가 null 이다.");

            if (grid == null)
                return MapProjectionResult.Failure("격자가 null 이다.");

            if (definition.Width != grid.Width || definition.Height != grid.Height)
            {
                return MapProjectionResult.Failure(
                    "맵 크기와 격자 크기가 다르다: 맵 " + definition.Width + "x" + definition.Height +
                    " vs 격자 " + grid.Width + "x" + grid.Height + ".");
            }

            if (definition.Tiles == null || definition.Tiles.Length != definition.TileCount)
            {
                return MapProjectionResult.Failure(
                    "타일 배열 길이가 맵 크기와 맞지 않는다(기대 " + (definition.Width * definition.Height) + ").");
            }

            // 인덱스 → 좌표 변환을 여기서 한 번만 해 두고, 아래 대입 단계에서 재사용한다.
            // 이렇게 해야 "변환이 두 벌로 갈라져 서로 다른 좌표를 쓰는" 사고를 원천 차단할 수 있다.
            HexCoord[] coordByIndex = new HexCoord[definition.TileCount];
            for (int index = 0; index < definition.TileCount; index++)
            {
                int col = definition.ToCol(index);
                int row = definition.ToRow(index);

                // offset(col,row) → cube 변환은 방향(FlatTop/PointyTop)에 따라 식이 다르다.
                // 맵 정의가 들고 있는 방향을 쓰고, 그 결과가 격자에 실제로 존재하는지로
                // "격자와 맵이 같은 방향으로 만들어졌는가"까지 함께 검사한다.
                HexCoord coord = HexGrid.OffsetToCube(col, row, definition.Orientation);

                if (!grid.HasTile(coord))
                {
                    return MapProjectionResult.Failure(
                        "격자에 없는 좌표다(맵과 격자의 헥스 방향이 다를 수 있다): index=" + index +
                        " col=" + col + " row=" + row + ".");
                }

                coordByIndex[index] = coord;
            }

            // 광산·성 목록의 인덱스가 전부 유효한지 먼저 확인한다.
            var castles = new List<MapTeamPlacement>(definition.Castles.Count);
            if (!TryCollectPlacements(definition.Castles, definition, coordByIndex, "성",
                    castles, out string castleFailure))
            {
                return MapProjectionResult.Failure(castleFailure);
            }

            var startingMines = new List<MapTeamPlacement>(definition.StartingMines.Count);
            if (!TryCollectPlacements(definition.StartingMines, definition, coordByIndex, "시작 광산",
                    startingMines, out string startingMineFailure))
            {
                return MapProjectionResult.Failure(startingMineFailure);
            }

            var neutralMines = new List<HexCoord>(definition.NeutralMines.Count);
            for (int i = 0; i < definition.NeutralMines.Count; i++)
            {
                int tileIndex = definition.NeutralMines[i];
                if (!definition.IsValidIndex(tileIndex))
                {
                    return MapProjectionResult.Failure(
                        "중립 광산의 타일 인덱스가 범위 밖이다: " + tileIndex + ".");
                }

                neutralMines.Add(coordByIndex[tileIndex]);
            }

            // ── 2단계: 여기서부터는 실패하지 않는다. 격자에 실제로 대입한다 ────
            int projectedTileCount = 0;
            for (int index = 0; index < definition.TileCount; index++)
            {
                HexTile tile = grid.GetTile(coordByIndex[index]);

                // 1단계에서 HasTile 로 확인했으므로 여기서 null 이 나올 수는 없다.
                // 그래도 방어적으로 건너뛴다 — 실패로 되돌릴 수 없는 구간이기 때문이다.
                if (tile == null) continue;

                tile.TileKind = definition.Tiles[index];

                // 🔴 MineKind 를 매번 None 으로 되돌린 뒤 목록으로 다시 칠한다.
                //    같은 격자에 다른 맵을 투영해도 이전 맵의 광산이 남지 않게 하기 위함이다
                //    (재경기·맵 전환에서 실제로 일어난다).
                tile.MineKind = MineKind.None;

                projectedTileCount++;
            }

            // 팀 시작 광산 — 팀에 따라 BlueStart / RedStart 로 구분해 칠한다.
            for (int i = 0; i < startingMines.Count; i++)
            {
                MapTeamPlacement placement = startingMines[i];
                HexTile tile = grid.GetTile(placement.Coord);
                if (tile == null) continue;

                tile.MineKind = placement.Team == TeamId.Blue
                    ? MineKind.BlueStart
                    : MineKind.RedStart;
            }

            // 중립 광산 — 전부 같은 종류다.
            for (int i = 0; i < neutralMines.Count; i++)
            {
                HexTile tile = grid.GetTile(neutralMines[i]);
                if (tile == null) continue;

                tile.MineKind = MineKind.Neutral;
            }

            return MapProjectionResult.Success(projectedTileCount, castles, startingMines, neutralMines);
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 배치 목록(인덱스 + 팀)을 좌표 목록으로 옮긴다. 인덱스 범위와 팀 값을 함께 검사한다.
        /// </summary>
        /// <param name="source">원본 배치 목록</param>
        /// <param name="definition">인덱스 유효성 판정에 쓰는 맵 정의</param>
        /// <param name="coordByIndex">인덱스 → 좌표 변환표</param>
        /// <param name="label">실패 사유 문구에 쓸 이름(예: "성")</param>
        /// <param name="destination">결과를 채울 목록</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>전부 유효하면 true</returns>
        private static bool TryCollectPlacements(List<MapObjectPlacement> source,
            MapDefinition definition, HexCoord[] coordByIndex, string label,
            List<MapTeamPlacement> destination, out string failureReason)
        {
            for (int i = 0; i < source.Count; i++)
            {
                MapObjectPlacement placement = source[i];

                if (!definition.IsValidIndex(placement.TileIndex))
                {
                    failureReason = label + " 의 타일 인덱스가 범위 밖이다: " + placement.TileIndex + ".";
                    return false;
                }

                // Neutral 팀의 성이나 시작 광산은 있을 수 없다. 조용히 Red 로 처리되면
                // 화면에서야 이상을 알아차리게 되므로 여기서 실패로 잡는다.
                if (placement.Team != TeamId.Blue && placement.Team != TeamId.Red)
                {
                    failureReason = label + " 의 소속 팀이 Blue/Red 가 아니다: " + placement.Team + ".";
                    return false;
                }

                destination.Add(new MapTeamPlacement(coordByIndex[placement.TileIndex], placement.Team));
            }

            failureReason = null;
            return true;
        }

        // ====================================================================
        // 자체 점검
        // ====================================================================

        /// <summary>
        /// 투영이 실제로 맞게 도는지 스스로 검사한다.
        /// 이 프로젝트에는 테스트 어셈블리가 없으므로 기대값을 코드 안에 둔다
        /// (MapRandomStreams · InitialMapStateEvaluator 등과 같은 방식).
        ///
        /// 검사 항목
        ///   1. 231칸이 전부 투영되고, 모든 칸의 TileKind 가 설계도와 일치한다
        ///   2. 시작 광산이 팀에 맞는 MineKind 로, 중립 광산이 Neutral 로 칠해진다
        ///   3. 광산이 아닌 칸은 전부 MineKind.None 이다
        ///   4. 성·시작 광산 좌표가 인덱스에서 계산한 좌표와 같다
        ///   5. HasBuilding 과 소유권은 투영이 건드리지 않는다
        ///   6. 같은 격자에 다른 맵을 다시 투영하면 이전 맵의 흔적이 남지 않는다
        ///   7. 음성 대조 — 크기 불일치 · 인덱스 범위 밖 · 방향 불일치는 반드시 실패한다
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            int width = MapDefinition.DefaultWidth;   // 11
            int height = MapDefinition.DefaultHeight; // 21

            // ── 검사용 설계도 ①: 규칙적인 무늬를 넣어 "전부 Normal 이라 우연히 맞는" 상황을 배제한다.
            var first = new MapDefinition(width, height);
            for (int index = 0; index < first.TileCount; index++)
            {
                int remainder = index % 5;
                first.Tiles[index] = remainder == 1
                    ? TileKind.NoBuild
                    : (remainder == 2 ? TileKind.Blocked : TileKind.Normal);
            }

            int blueCastleIndex = first.ToIndex(5, 19);
            int redCastleIndex = first.ToIndex(5, 1);
            int blueMineIndex = first.ToIndex(3, 19);
            int redMineIndex = first.ToIndex(7, 1);
            int neutralMineIndexA = first.ToIndex(2, 10);
            int neutralMineIndexB = first.ToIndex(8, 10);

            // 성·광산 자리는 실제 맵과 같게 Normal 로 만든다(위 무늬가 덮었을 수 있다).
            first.Tiles[blueCastleIndex] = TileKind.Normal;
            first.Tiles[redCastleIndex] = TileKind.Normal;
            first.Tiles[blueMineIndex] = TileKind.Normal;
            first.Tiles[redMineIndex] = TileKind.Normal;
            first.Tiles[neutralMineIndexA] = TileKind.Normal;
            first.Tiles[neutralMineIndexB] = TileKind.Normal;

            first.Castles.Add(new MapObjectPlacement(blueCastleIndex, TeamId.Blue));
            first.Castles.Add(new MapObjectPlacement(redCastleIndex, TeamId.Red));
            first.StartingMines.Add(new MapObjectPlacement(blueMineIndex, TeamId.Blue));
            first.StartingMines.Add(new MapObjectPlacement(redMineIndex, TeamId.Red));
            first.NeutralMines.Add(neutralMineIndexA);
            first.NeutralMines.Add(neutralMineIndexB);

            var grid = new HexGrid(width, height, HexOrientation.FlatTop);

            MapProjectionResult result = Project(first, grid);
            if (!result.IsSucceeded)
            {
                failureReason = "정상 투영이 실패했다: " + result.FailureReason;
                return false;
            }

            // 1. 전 칸 대조
            if (result.ProjectedTileCount != width * height)
            {
                failureReason = "투영한 타일 수가 다르다: " + result.ProjectedTileCount +
                    " (기대 " + (width * height) + ").";
                return false;
            }

            if (!TryVerifyProjection(first, grid, out failureReason))
                return false;

            // 4. 성·시작 광산 좌표
            if (result.Castles.Count != 2 || result.StartingMines.Count != 2 ||
                result.NeutralMines.Count != 2)
            {
                failureReason = "배치 목록 개수가 다르다: 성 " + result.Castles.Count +
                    " 시작광산 " + result.StartingMines.Count +
                    " 중립광산 " + result.NeutralMines.Count + ".";
                return false;
            }

            HexCoord expectedBlueCastle = HexGrid.OffsetToCube(5, 19, HexOrientation.FlatTop);
            if (!result.Castles[0].Coord.Equals(expectedBlueCastle) ||
                result.Castles[0].Team != TeamId.Blue)
            {
                failureReason = "Blue 성 좌표/팀이 기대와 다르다.";
                return false;
            }

            // 5. 소유권·HasBuilding 무변경
            foreach (var pair in grid.Tiles)
            {
                if (pair.Value.HasBuilding)
                {
                    failureReason = "투영이 HasBuilding 을 켰다.";
                    return false;
                }

                if (pair.Value.Owner != TeamId.Neutral)
                {
                    failureReason = "투영이 타일 소유권을 바꿨다.";
                    return false;
                }
            }

            // 6. 같은 격자에 다른 맵을 다시 투영 — 이전 흔적이 남으면 안 된다.
            var second = new MapDefinition(width, height);
            for (int index = 0; index < second.TileCount; index++)
                second.Tiles[index] = TileKind.Normal;

            int secondBlueMine = second.ToIndex(4, 18);
            int secondRedMine = second.ToIndex(6, 2);
            second.StartingMines.Add(new MapObjectPlacement(secondBlueMine, TeamId.Blue));
            second.StartingMines.Add(new MapObjectPlacement(secondRedMine, TeamId.Red));

            MapProjectionResult secondResult = Project(second, grid);
            if (!secondResult.IsSucceeded)
            {
                failureReason = "두 번째 투영이 실패했다: " + secondResult.FailureReason;
                return false;
            }

            if (!TryVerifyProjection(second, grid, out failureReason))
                return false;

            // 7. 음성 대조 — 이것들이 통과해 버리면 위의 검사는 아무것도 보장하지 못한다.
            var wrongSize = new MapDefinition(width, height - 1);
            if (Project(wrongSize, grid).IsSucceeded)
            {
                failureReason = "음성 대조 실패: 크기가 다른 맵이 투영됐다.";
                return false;
            }

            var badIndex = new MapDefinition(width, height);
            badIndex.NeutralMines.Add(width * height);
            if (Project(badIndex, grid).IsSucceeded)
            {
                failureReason = "음성 대조 실패: 범위 밖 광산 인덱스가 통과했다.";
                return false;
            }

            var badTeam = new MapDefinition(width, height);
            badTeam.Castles.Add(new MapObjectPlacement(0, TeamId.Neutral));
            if (Project(badTeam, grid).IsSucceeded)
            {
                failureReason = "음성 대조 실패: 중립 팀의 성이 통과했다.";
                return false;
            }

            var pointyGrid = new HexGrid(width, height, HexOrientation.PointyTop);
            if (Project(first, pointyGrid).IsSucceeded)
            {
                failureReason = "음성 대조 실패: 헥스 방향이 다른 격자에 투영됐다.";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 격자의 현재 상태가 설계도와 한 칸도 빠짐없이 일치하는지 확인한다.
        /// (자체 점검 전용 — 231칸을 전부 훑는다.)
        /// </summary>
        /// <param name="definition">비교 기준이 되는 설계도</param>
        /// <param name="grid">확인할 격자</param>
        /// <param name="failureReason">불일치 사유(일치하면 null)</param>
        /// <returns>전부 일치하면 true</returns>
        private static bool TryVerifyProjection(MapDefinition definition, HexGrid grid,
            out string failureReason)
        {
            // 기대하는 MineKind 표를 인덱스 기준으로 미리 만든다.
            MineKind[] expectedMines = new MineKind[definition.TileCount];
            for (int i = 0; i < definition.StartingMines.Count; i++)
            {
                MapObjectPlacement placement = definition.StartingMines[i];
                expectedMines[placement.TileIndex] = placement.Team == TeamId.Blue
                    ? MineKind.BlueStart
                    : MineKind.RedStart;
            }

            for (int i = 0; i < definition.NeutralMines.Count; i++)
                expectedMines[definition.NeutralMines[i]] = MineKind.Neutral;

            for (int index = 0; index < definition.TileCount; index++)
            {
                int col = definition.ToCol(index);
                int row = definition.ToRow(index);
                HexCoord coord = HexGrid.OffsetToCube(col, row, definition.Orientation);
                HexTile tile = grid.GetTile(coord);

                if (tile == null)
                {
                    failureReason = "격자에 타일이 없다: index=" + index + ".";
                    return false;
                }

                if (tile.TileKind != definition.Tiles[index])
                {
                    failureReason = "TileKind 불일치: index=" + index +
                        " 격자=" + tile.TileKind + " 설계도=" + definition.Tiles[index] + ".";
                    return false;
                }

                if (tile.MineKind != expectedMines[index])
                {
                    failureReason = "MineKind 불일치: index=" + index +
                        " 격자=" + tile.MineKind + " 기대=" + expectedMines[index] + ".";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 자체 점검을 실행하고 실패하면 예외를 던진다.
        /// 에디터에서만 실행되며(빌드에서는 호출 자체가 사라진다), 개발 중 회귀를 즉시 드러낸다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void AssertSelfCheck()
        {
            if (!TryRunSelfCheck(out string failureReason))
                throw new System.InvalidOperationException("MapProjectionUseCase 자체 점검 실패: " + failureReason);
        }
    }
}
