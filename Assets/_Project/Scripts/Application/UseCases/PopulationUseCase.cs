// ============================================================================
// PopulationUseCase.cs
// 인구수 계산 UseCase.
//
// GDD 공식:
//   총 인구수 = 보유 타일 수
//   사용 인구 = 건물 수 + 유닛 수 (각 1 인구)
//   가용 인구 = 총 인구 - 사용 인구
//
// 모든 계산은 호출 시점에 실시간으로 수행 (캐싱 없음).
// 타일/유닛/건물 수가 적은 프로토타입/MVP에서는 성능 문제 없음.
//
// Application 레이어 — Domain에 의존.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    public class PopulationUseCase
    {
        private readonly HexGrid _grid;
        private readonly UnitSpawnUseCase _unitSpawn;
        private readonly BuildingPlacementUseCase _buildingPlacement;

        public PopulationUseCase(HexGrid grid, UnitSpawnUseCase unitSpawn,
            BuildingPlacementUseCase buildingPlacement)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _buildingPlacement = buildingPlacement;
        }

        /// <summary> 해당 팀의 최대 인구수 (= 보유 타일 수). </summary>
        public int GetMaxPopulation(TeamId team)
        {
            return _grid.CountTilesOwnedBy(team);
        }

        /// <summary>
        /// 해당 팀의 사용 중인 인구수 (= 건물 수 + 유닛 수).
        /// </summary>
        public int GetUsedPopulation(TeamId team)
        {
            int buildingCount = 0;
            foreach (var kvp in _buildingPlacement.Buildings)
            {
                if (kvp.Value.Team == team && kvp.Value.IsAlive)
                    buildingCount++;
            }

            int unitCount = 0;
            foreach (var kvp in _unitSpawn.Units)
            {
                if (kvp.Value.Team == team && kvp.Value.IsAlive)
                    unitCount++;
            }

            return buildingCount + unitCount;
        }

        /// <summary> 해당 팀의 가용 인구수. </summary>
        public int GetAvailablePopulation(TeamId team)
        {
            return GetMaxPopulation(team) - GetUsedPopulation(team);
        }

        /// <summary> 해당 팀이 필요한 인구를 확보할 수 있는지 확인. </summary>
        public bool HasPopulation(TeamId team, int needed)
        {
            return GetAvailablePopulation(team) >= needed;
        }
    }
}
