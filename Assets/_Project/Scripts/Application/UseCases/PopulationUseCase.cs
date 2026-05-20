// ============================================================================
// PopulationUseCase.cs
// 인구수 계산 UseCase.
//
// GDD 공식:
//   총 인구수 = 보유 타일 수
//   사용 인구 = 건물 수 + 유닛 수 (각 1 인구)
//   가용 인구 = 총 인구 - 사용 인구
//
// [2026-05-20] 성능 최적화 — 팀별 사용 인구수를 Dictionary 캐시로 관리.
// GameEvents.OnUnitSpawned / OnUnitDied / OnBuildingPlaced / OnBuildingDied를 구독하여
// 증감 갱신하므로 GetUsedPopulation 호출 시 O(n) 순회 없이 O(1)로 반환.
//
// Application 레이어 — Domain에 의존. UniRx 사용.
// ============================================================================

using System;
using System.Collections.Generic;
using UniRx;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class PopulationUseCase : IDisposable
    {
        private readonly HexGrid _grid;
        private readonly UnitSpawnUseCase _unitSpawn;
        private readonly BuildingPlacementUseCase _buildingPlacement;

        // [2026-05-20] 팀별 사용 인구수 캐시.
        // 유닛 생성/사망 + 건물 배치/사망 이벤트로 증감 갱신.
        private readonly Dictionary<TeamId, int> _usedPopulationByTeam = new Dictionary<TeamId, int>();

        // 이벤트 구독 해제용 컨테이너 — Dispose 시 일괄 해제하여 누수 방지.
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

        public PopulationUseCase(HexGrid grid, UnitSpawnUseCase unitSpawn,
            BuildingPlacementUseCase buildingPlacement)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _buildingPlacement = buildingPlacement;

            // 이벤트 구독으로 캐시를 자동 갱신한다.
            //   OnUnitSpawned / OnBuildingPlaced: 해당 팀 +1
            //   OnUnitDied / OnBuildingDied: 해당 팀 -1
            GameEvents.OnUnitSpawned
                .Subscribe(e => AddToTeam(e.Unit.Team, +1))
                .AddTo(_subscriptions);

            GameEvents.OnUnitDied
                .Subscribe(e => AddToTeam(e.Unit.Team, -1))
                .AddTo(_subscriptions);

            GameEvents.OnBuildingPlaced
                .Subscribe(e => AddToTeam(e.Building.Team, +1))
                .AddTo(_subscriptions);

            GameEvents.OnBuildingDied
                .Subscribe(e => AddToTeam(e.Building.Team, -1))
                .AddTo(_subscriptions);

            // [참고] OnBuildingUpgraded는 동일 팀에 +1/-1 효과를 동시에 일으키므로 변동 없음.
            //         업그레이드는 도메인 상 OnBuildingPlaced/OnBuildingDied를 별도 발행하지 않으므로
            //         캐시도 그대로 유지하면 된다(인구는 한 칸 = 한 건물).
        }

        /// <summary>
        /// 게임 종료/재경기 시 호출하여 이벤트 구독을 해제한다.
        /// CompositeDisposable로 일괄 해제 — 누락 시 다음 게임에서 중복 카운트 위험.
        /// </summary>
        public void Dispose()
        {
            _subscriptions.Dispose();
            _usedPopulationByTeam.Clear();
        }

        /// <summary> 해당 팀의 최대 인구수 (= 보유 타일 수). </summary>
        public int GetMaxPopulation(TeamId team)
        {
            return _grid.CountTilesOwnedBy(team);
        }

        /// <summary>
        /// 해당 팀의 사용 중인 인구수 (= 건물 수 + 유닛 수).
        /// [2026-05-20] Dictionary 캐시로 O(1) 즉시 반환.
        /// </summary>
        public int GetUsedPopulation(TeamId team)
        {
            return _usedPopulationByTeam.TryGetValue(team, out int count) ? count : 0;
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

        // ====================================================================
        // 내부 헬퍼 — 팀별 사용 인구수 증감
        // ====================================================================

        /// <summary>
        /// 지정 팀의 사용 인구수 카운터에 delta(±1)를 더한다.
        /// 음수가 되지 않도록 0 하한으로 클램프한다(이벤트 누락 등 방어).
        /// </summary>
        private void AddToTeam(TeamId team, int delta)
        {
            if (!_usedPopulationByTeam.TryGetValue(team, out int current))
                current = 0;
            int next = current + delta;
            if (next < 0) next = 0;
            _usedPopulationByTeam[team] = next;
        }
    }
}
