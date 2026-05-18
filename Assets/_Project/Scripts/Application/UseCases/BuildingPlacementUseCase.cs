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
        /// <param name="type">건물 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">건물 배치 좌표</param>
        /// <param name="race">
        /// 해당 팀의 종족. 종족에 따라 건물 HP가 달라짐.
        /// 호출자(BuildingPlacementUI, NetworkBuildingController, GameBootstrapper)가
        /// GameRaceContext에서 조회하여 전달.
        /// </param>
        public BuildingData PlaceBuilding(BuildingType type, TeamId team, HexCoord position,
            RaceId race = RaceId.Human)
        {
            // MiningPost는 전용 메서드로 처리 (금광 타일 = 비이동 + 중립)
            if (type == BuildingType.MiningPost)
                return PlaceMiningPost(team, position, race);

            HexTile tile = _grid.GetTile(position);
            if (tile == null) return null;
            if (!tile.IsWalkable) return null; // 이미 건물이 있거나 이동 불가 타일

            // Castle이 아닌 건물은 자기 팀 타일에만 배치 가능
            if (type != BuildingType.Castle && tile.Owner != team)
                return null;

            return PlaceBuildingInternal(type, team, position, tile, race);
        }

        /// <summary>
        /// MiningPost 배치. 금광 타일 전용.
        /// 조건: HasGoldMine + 건물 없음 + 인접 타일 중 하나 이상 팀 소유.
        /// </summary>
        /// <param name="team">소속 팀</param>
        /// <param name="position">배치 좌표</param>
        /// <param name="race">종족 (건물 HP 결정에 사용)</param>
        private BuildingData PlaceMiningPost(TeamId team, HexCoord position,
            RaceId race = RaceId.Human)
        {
            HexTile tile = _grid.GetTile(position);
            if (tile == null) return null;
            if (!tile.HasGoldMine) return null;
            if (GetBuildingAt(position) != null) return null; // 이미 건물 있음

            // 인접 타일 중 하나 이상 팀 소유 필요
            if (!HasAdjacentTeamTile(position, team)) return null;

            return PlaceBuildingInternal(BuildingType.MiningPost, team, position, tile, race);
        }

        /// <summary>
        /// 시작 시 채굴소 직접 배치 (인접 타일 조건 무시).
        /// GameBootstrapper에서 초기 채굴소 설치에 사용.
        /// </summary>
        /// <param name="team">소속 팀</param>
        /// <param name="position">배치 좌표</param>
        /// <param name="race">종족 (건물 HP 결정에 사용)</param>
        public BuildingData PlaceMiningPostDirect(TeamId team, HexCoord position,
            RaceId race = RaceId.Human)
        {
            HexTile tile = _grid.GetTile(position);
            if (tile == null) return null;
            if (!tile.HasGoldMine) return null;

            return PlaceBuildingInternal(BuildingType.MiningPost, team, position, tile, race);
        }

        /// <summary>
        /// 네트워크 클라이언트 측 건물 재생성 (ID 동기화 전용).
        /// 서버에서 이미 검증·배치 완료된 건물을 클라이언트에서 도메인 상태에 반영.
        /// 검증 없이 직접 등록하고 타일 상태를 변경하며 이벤트를 발행.
        /// 서버가 전달한 buildingId로 BuildingData를 생성하여 양측 ID를 일치시킴.
        /// </summary>
        /// <param name="buildingId">서버에서 발급한 건물 Id</param>
        /// <param name="type">건물 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">건물 배치 좌표</param>
        /// <param name="race">종족 (건물 HP 결정에 사용)</param>
        /// <returns>생성된 BuildingData. 타일이 없으면 null.</returns>
        public BuildingData PlaceBuildingWithId(int buildingId, BuildingType type,
            TeamId team, HexCoord position, RaceId race = RaceId.Human)
        {
            HexTile tile = _grid.GetTile(position);
            if (tile == null) return null;

            int maxHp = BuildingStats.GetMaxHp(type, race);
            // ID 지정 생성자 사용 — 서버와 동일한 Id로 BuildingData 생성
            var building = new BuildingData(buildingId, type, team, position, maxHp);
            _buildings[building.Id] = building;

            // 타일 상태 변경
            tile.IsWalkable = false;
            _grid.SetOwner(position, team);

            // 인접 타일 소유권 설정
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

            // 이벤트 발행 → BuildingFactory가 프리팹 생성
            GameEvents.OnBuildingPlaced.OnNext(new BuildingPlacedEvent(building));
            GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(position, team));

            return building;
        }

        /// <summary>
        /// 건물 배치 공통 로직. 검증 통과 후 호출.
        /// </summary>
        /// <param name="type">건물 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">배치 좌표</param>
        /// <param name="tile">배치할 타일 참조</param>
        /// <param name="race">종족 (건물 HP 결정에 사용)</param>
        private BuildingData PlaceBuildingInternal(BuildingType type, TeamId team,
            HexCoord position, HexTile tile, RaceId race = RaceId.Human)
        {
            int maxHp = BuildingStats.GetMaxHp(type, race);
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
        /// Id로 건물 조회.
        /// NetworkHealthSync에서 클라이언트 측 HP 동기화 시 사용.
        /// </summary>
        public BuildingData GetBuilding(int buildingId)
        {
            _buildings.TryGetValue(buildingId, out BuildingData building);
            return building;
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
        /// 플레이어 철거 요청으로 건물을 제거한다.
        /// RemoveBuilding과 달리 OnEntityDied 이벤트를 먼저 발행하여
        /// BuildingView(프리팹)가 자신을 Destroy 할 수 있도록 한다.
        ///
        /// 처리 순서:
        ///   1) 건물 존재 확인
        ///   2) OnEntityDied 이벤트 발행 → BuildingView.Destroy(gameObject)
        ///   3) RemoveBuilding 호출 → 도메인 딕셔너리 제거 + 타일 상태 복구
        ///
        /// 근거: UnitCombatUseCase에서 건물 HP=0 파괴 시 동일 패턴 사용.
        ///   (라인 787: OnEntityDied 발행 → 라인 807: RemoveBuilding 호출)
        /// </summary>
        /// <param name="buildingId">철거할 건물 Id</param>
        /// <returns>성공 시 true, 건물이 없으면 false</returns>
        public bool DemolishBuilding(int buildingId)
        {
            // 건물 조회 — 없으면 즉시 false 반환
            if (!_buildings.TryGetValue(buildingId, out var building))
                return false;

            // OnEntityDied 이벤트 발행 → BuildingView가 자신의 GameObject를 Destroy.
            // UnitCombatUseCase의 건물 HP=0 파괴 경로와 동일한 패턴.
            // Castle을 철거하면 GameEndUseCase가 OnEntityDied를 받아 게임 종료를 판정하지만,
            // 철거 호출 전 Castle 여부를 검증하므로 실제로는 Castle이 여기에 도달하지 않는다.
            GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(building));

            // 도메인 딕셔너리에서 제거 + 타일 상태(IsWalkable, 소유권) 복구
            return RemoveBuilding(buildingId);
        }

        /// <summary>
        /// 건물을 목록에서 제거하고 타일을 이동 가능 상태로 복구.
        /// UnitCombatUseCase에서 건물 HP가 0 이하가 되었을 때 호출.
        /// 철거 시에는 OnEntityDied 발행이 포함된 DemolishBuilding을 사용할 것.
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

                    // 채굴소(MiningPost)가 파괴된 경우:
                    // 해당 타일의 소유권을 Neutral로 되돌려서 타일 색상이 중립으로 복귀하도록 처리.
                    // Castle/Barracks는 파괴 시 영토 변화가 필요 없으므로 MiningPost만 처리한다.
                    // HexTileView가 OnTileOwnerChanged 이벤트를 구독하고 있어,
                    // 이벤트 발행만으로 싱글/멀티 양쪽 클라이언트에서 색상이 자동 갱신된다.
                    if (building.Type == BuildingType.MiningPost)
                    {
                        _grid.SetOwner(building.Position, TeamId.Neutral);
                        GameEvents.OnTileOwnerChanged.OnNext(
                            new TileOwnerChangedEvent(building.Position, TeamId.Neutral));
                    }
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

        // ====================================================================
        // 업그레이드
        // ====================================================================

        /// <summary>
        /// 서버/싱글플레이 측 건물 업그레이드 실행.
        /// 기존 BuildingData를 제거하고 다음 단계의 BuildingData로 교체한다.
        /// 타일의 IsWalkable·소유권은 그대로 유지된다(건물이 계속 존재).
        ///
        /// 검증 항목 (외부 골드 검증은 호출자 책임):
        ///   - 건물 존재 여부
        ///   - 다음 단계 존재 여부 (CanUpgrade)
        ///
        /// 흐름:
        ///   1) 기존 BuildingData 조회
        ///   2) NextStage 타입 결정
        ///   3) 기존 BuildingData를 _buildings에서 제거
        ///   4) 새 BuildingType + 새 HP로 BuildingData 생성 (새 Id 자동 발급)
        ///   5) GameEvents.OnBuildingUpgraded 발행 → BuildingFactory가 GO 교체
        /// </summary>
        /// <param name="buildingId">업그레이드 대상 건물 Id.</param>
        /// <param name="race">건물 소유 종족 (새 단계 HP 결정에 사용).</param>
        /// <returns>업그레이드 후 생성된 새 BuildingData. 실패 시 null.</returns>
        public BuildingData UpgradeBuilding(int buildingId, RaceId race = RaceId.Human)
        {
            // 1) 기존 건물 조회
            if (!_buildings.TryGetValue(buildingId, out var oldBuilding))
                return null;

            // 2) 다음 단계 타입 결정
            BuildingType? nextOpt = BuildingTypeHelper.GetNextStage(oldBuilding.Type);
            if (!nextOpt.HasValue) return null;
            BuildingType nextType = nextOpt.Value;

            // 3) 기존 BuildingData 제거. 타일 IsWalkable/Owner는 그대로 유지.
            _buildings.Remove(buildingId);

            // 4) 새 BuildingData 생성. 새 HP 적용. Id는 자동 발급.
            int newMaxHp = BuildingStats.GetMaxHp(nextType, race);
            var newBuilding = new BuildingData(nextType, oldBuilding.Team, oldBuilding.Position, newMaxHp);
            _buildings[newBuilding.Id] = newBuilding;

            // 5) 이벤트 발행 → BuildingFactory가 새 프리팹 생성 후 기존 GO 제거 (빈 타일 방지)
            GameEvents.OnBuildingUpgraded.OnNext(new BuildingUpgradedEvent(buildingId, newBuilding));

            return newBuilding;
        }

        /// <summary>
        /// 네트워크 클라이언트 측 업그레이드 적용 (서버에서 생성된 새 BuildingId를 그대로 사용).
        /// 서버가 검증·실행한 결과를 클라이언트에 동기화할 때 사용.
        /// 검증 없이 직접 도메인 상태를 변경하고 이벤트를 발행.
        /// </summary>
        /// <param name="oldBuildingId">업그레이드 이전 건물 Id (제거 대상).</param>
        /// <param name="newBuildingId">서버가 새로 발급한 건물 Id.</param>
        /// <param name="newType">새 BuildingType (다음 단계).</param>
        /// <param name="race">건물 소유 종족 (HP 결정에 사용).</param>
        /// <returns>생성된 새 BuildingData. 위치 정보가 없으면 null.</returns>
        public BuildingData UpgradeBuildingWithId(int oldBuildingId, int newBuildingId,
            BuildingType newType, RaceId race = RaceId.Human)
        {
            // 위치/팀 정보를 알기 위해 기존 BuildingData가 필요.
            if (!_buildings.TryGetValue(oldBuildingId, out var oldBuilding))
                return null;

            TeamId team = oldBuilding.Team;
            HexCoord position = oldBuilding.Position;

            // 기존 BuildingData 제거. 타일 상태는 그대로 유지.
            _buildings.Remove(oldBuildingId);

            // 서버와 동일한 Id로 새 BuildingData 생성.
            int newMaxHp = BuildingStats.GetMaxHp(newType, race);
            var newBuilding = new BuildingData(newBuildingId, newType, team, position, newMaxHp);
            _buildings[newBuilding.Id] = newBuilding;

            // 이벤트 발행 — BuildingFactory가 새 프리팹 생성 후 기존 GO 제거
            GameEvents.OnBuildingUpgraded.OnNext(new BuildingUpgradedEvent(oldBuildingId, newBuilding));

            return newBuilding;
        }
    }
}
