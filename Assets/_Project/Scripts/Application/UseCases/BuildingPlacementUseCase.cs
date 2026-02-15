// ============================================================================
// BuildingPlacementUseCase.cs
// 건물 배치 요청을 처리하는 UseCase.
//
// 흐름:
//   1. GameBootstrapper(Castle 자동 배치) 또는 BuildingPlacementUI(플레이어 배치)가 요청
//   2. 배치 가능 여부 검증 (타일 존재, IsWalkable, 팀 소유)
//   3. BuildingData 인스턴스 생성 (Id 자동 발급)
//   4. 타일 상태 변경: IsWalkable = false, 소유권 설정
//   5. GameEvents.OnBuildingPlaced 이벤트 발행
//   6. BuildingFactory(Infrastructure)가 이벤트를 받아 프리팹 인스턴스 생성
//
// UnitSpawnUseCase 패턴을 따름: Dictionary 관리, 검증, 이벤트 발행.
//
// Application 레이어 — Domain에 의존.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class BuildingPlacementUseCase
    {
        // 타일 조회/상태 변경용 그리드 참조
        private readonly HexGrid _grid;

        // 배치된 모든 건물을 Id로 인덱싱하여 관리.
        private readonly Dictionary<int, BuildingData> _buildings = new Dictionary<int, BuildingData>();

        /// <summary> 현재 존재하는 모든 건물 목록 (읽기 전용). </summary>
        public IReadOnlyDictionary<int, BuildingData> Buildings => _buildings;

        public BuildingPlacementUseCase(HexGrid grid)
        {
            _grid = grid;
        }

        /// <summary>
        /// 건물 배치 요청.
        ///
        /// 검증:
        ///   - 해당 좌표에 타일이 존재하는지
        ///   - 해당 타일이 이동 가능(IsWalkable)한지 (이미 건물 없음)
        ///   - Castle이 아닌 건물은 자기 팀 타일에만 배치 가능
        ///
        /// 성공 시: BuildingData 생성 → 타일 상태 변경 → 이벤트 발행 → BuildingData 반환
        /// 실패 시: null 반환
        /// </summary>
        public BuildingData PlaceBuilding(BuildingType type, TeamId team, HexCoord position)
        {
            // MiningPost는 전용 메서드로 처리 (금광 타일 = 비이동 + 중립)
            if (type == BuildingType.MiningPost)
                return PlaceMiningPost(team, position);

            HexTile tile = _grid.GetTile(position);
            if (tile == null) return null;
            if (!tile.IsWalkable) return null; // 이미 건물이 있거나 이동 불가 타일

            // Castle이 아닌 건물은 자기 팀 타일에만 배치 가능
            if (type != BuildingType.Castle && tile.Owner != team)
                return null;

            return PlaceBuildingInternal(type, team, position, tile);
        }

        /// <summary>
        /// MiningPost 배치. 금광 타일 전용.
        /// 조건: HasGoldMine + 건물 없음 + 인접 타일 중 하나 이상 팀 소유.
        /// </summary>
        private BuildingData PlaceMiningPost(TeamId team, HexCoord position)
        {
            HexTile tile = _grid.GetTile(position);
            if (tile == null) return null;
            if (!tile.HasGoldMine) return null;
            if (GetBuildingAt(position) != null) return null; // 이미 건물 있음

            // 인접 타일 중 하나 이상 팀 소유 필요
            if (!HasAdjacentTeamTile(position, team)) return null;

            return PlaceBuildingInternal(BuildingType.MiningPost, team, position, tile);
        }

        /// <summary>
        /// 시작 시 채굴소 직접 배치 (인접 타일 조건 무시).
        /// GameBootstrapper에서 초기 채굴소 설치에 사용.
        /// </summary>
        public BuildingData PlaceMiningPostDirect(TeamId team, HexCoord position)
        {
            HexTile tile = _grid.GetTile(position);
            if (tile == null) return null;
            if (!tile.HasGoldMine) return null;

            return PlaceBuildingInternal(BuildingType.MiningPost, team, position, tile);
        }

        /// <summary>
        /// 건물 배치 공통 로직. 검증 통과 후 호출.
        /// </summary>
        private BuildingData PlaceBuildingInternal(BuildingType type, TeamId team,
            HexCoord position, HexTile tile)
        {
            int maxHp = BuildingStats.GetMaxHp(type);
            var building = new BuildingData(type, team, position, maxHp);
            _buildings[building.Id] = building;

            // 타일 상태 변경: 이동 불가 + 소유권 설정
            tile.IsWalkable = false;
            _grid.SetOwner(position, team);

            // 인접 타일 소유권 설정 (건물 건설 시 주변 영토 확장)
            var neighbors = _grid.GetNeighbors(position);
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Owner != team)
                {
                    _grid.SetOwner(neighbor.Coord, team);
                    GameEvents.OnTileOwnerChanged.OnNext(
                        new TileOwnerChangedEvent(neighbor.Coord, team));
                }
            }

            // 이벤트 발행 → BuildingFactory가 프리팹 생성, HexTileView가 색상 변경
            GameEvents.OnBuildingPlaced.OnNext(new BuildingPlacedEvent(building));
            GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(position, team));

            return building;
        }

        /// <summary>
        /// 해당 좌표에 건물이 배치 가능한지 확인.
        /// InputHandler에서 건물 배치 팝업 표시 여부 판단에 사용.
        /// </summary>
        public bool CanPlaceBuilding(HexCoord position, TeamId team)
        {
            HexTile tile = _grid.GetTile(position);
            return tile != null && tile.IsWalkable && tile.Owner == team;
        }

        /// <summary>
        /// 특정 건물 타입을 해당 좌표에 배치 가능한지 확인.
        /// MiningPost: 금광 + 건물 없음 + 인접 팀 타일.
        /// 일반 건물: IsWalkable + 팀 소유.
        /// </summary>
        public bool CanPlaceBuildingType(BuildingType type, HexCoord position, TeamId team)
        {
            HexTile tile = _grid.GetTile(position);
            if (tile == null) return false;

            if (type == BuildingType.MiningPost)
            {
                return tile.HasGoldMine
                    && GetBuildingAt(position) == null
                    && HasAdjacentTeamTile(position, team);
            }

            return tile.IsWalkable && tile.Owner == team;
        }

        /// <summary>
        /// 금광 타일에 채굴소 건설 가능한지 확인.
        /// InputHandler에서 금광 클릭 시 팝업 표시 여부 판단에 사용.
        /// </summary>
        public bool CanPlaceMiningPost(HexCoord position, TeamId team)
        {
            return CanPlaceBuildingType(BuildingType.MiningPost, position, team);
        }

        /// <summary>
        /// 인접 타일 중 하나 이상 해당 팀 소유인지 확인.
        /// </summary>
        private bool HasAdjacentTeamTile(HexCoord position, TeamId team)
        {
            var neighbors = _grid.GetNeighbors(position);
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Owner == team)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 좌표에 있는 건물 조회.
        /// O(n) 탐색이지만 건물 수가 적어 문제 없음.
        /// </summary>
        public BuildingData GetBuildingAt(HexCoord position)
        {
            foreach (var kvp in _buildings)
            {
                if (kvp.Value.Position == position)
                    return kvp.Value;
            }
            return null;
        }

        /// <summary>
        /// 건물을 목록에서 제거하고 타일을 이동 가능 상태로 복구.
        /// UnitCombatUseCase에서 건물 HP가 0 이하가 되었을 때 호출.
        /// </summary>
        public bool RemoveBuilding(int buildingId)
        {
            if (_buildings.TryGetValue(buildingId, out var building))
            {
                HexTile tile = _grid.GetTile(building.Position);
                if (tile != null)
                {
                    // 금광 타일은 이동 불가 유지 (금광 오브젝트가 남아있음)
                    if (!tile.HasGoldMine)
                        tile.IsWalkable = true;
                }

                return _buildings.Remove(buildingId);
            }
            return false;
        }

        /// <summary>
        /// 모든 건물 제거. 맵 전환 시 호출.
        /// </summary>
        public void Clear()
        {
            _buildings.Clear();
        }
    }
}
