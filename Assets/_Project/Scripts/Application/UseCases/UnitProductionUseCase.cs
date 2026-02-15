// ============================================================================
// UnitProductionUseCase.cs
// 배럭별 유닛 생산 큐, 타이머, 자동/수동 모드를 관리하는 핵심 UseCase.
//
// 생산 흐름:
//   1. 배럭 배치 시 RegisterBarracks() → ProductionState 생성
//   2. 플레이어가 수동 큐 추가(EnqueueUnit) 또는 자동 토글(ToggleAutoProduction)
//   3. Tick(dt) 매 프레임 호출 → TryStartNext로 다음 생산 결정
//   4. 골드/인구 검증 → 골드 차감 → 타이머 시작 (스폰 타일 미확인)
//   5. 타이머 완료 → CompleteProduction → 스폰 타일 확인
//      - 스폰 가능: 유닛 생성 + 랠리포인트 이벤트
//      - 스폰 불가: 대기 (매 프레임 재시도, 다음 생산은 스폰 완료 후 시작)
//
// 수동 vs 자동:
//   수동: 탭 → ManualQueue에 추가 (최대 3). 자동 모드 해제.
//   자동: 롱프레스 → AutoTypes에 토글. AutoTypes 순환 반복.
//   수동 시작 시 현재 자동 생산 중이면 취소(골드 환불 없음).
//
// Application 레이어 — Domain에 의존.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class UnitProductionUseCase
    {
        private readonly HexGrid _grid;
        private readonly UnitSpawnUseCase _unitSpawn;
        private readonly ResourceUseCase _resource;
        private readonly PopulationUseCase _population;
        private readonly BuildingPlacementUseCase _buildingPlacement;

        // 배럭 Id → ProductionState
        private readonly Dictionary<int, ProductionState> _states = new Dictionary<int, ProductionState>();

        public UnitProductionUseCase(
            HexGrid grid,
            UnitSpawnUseCase unitSpawn,
            ResourceUseCase resource,
            PopulationUseCase population,
            BuildingPlacementUseCase buildingPlacement)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _resource = resource;
            _population = population;
            _buildingPlacement = buildingPlacement;
        }

        // ====================================================================
        // 배럭 등록/해제
        // ====================================================================

        /// <summary>
        /// 배럭 건물이 배치될 때 호출. ProductionState를 생성하여 등록.
        /// </summary>
        public void RegisterBarracks(BuildingData barracks)
        {
            if (barracks == null || barracks.Type != BuildingType.Barracks) return;
            if (_states.ContainsKey(barracks.Id)) return;

            _states[barracks.Id] = new ProductionState(barracks.Id, barracks.Team, barracks.Position);
        }

        /// <summary>
        /// 배럭이 파괴될 때 호출. ProductionState 제거.
        /// </summary>
        public void UnregisterBarracks(int barracksId)
        {
            _states.Remove(barracksId);
        }

        // ====================================================================
        // 생산 큐 관리
        // ====================================================================

        /// <summary>
        /// 수동 생산: 큐에 유닛 추가.
        /// 골드/인구 즉시 검증 → 골드 즉시 차감 → 큐에 추가.
        /// 자동 모드 해제 + 현재 자동 생산 취소.
        /// 큐가 가득 차면 false 반환.
        /// </summary>
        public bool EnqueueUnit(int barracksId, UnitType type)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            // 총 생산 수 = 현재 생산 중(1) + 대기 큐. MaxQueueSize(3)를 초과하면 거부.
            int totalCount = state.ManualQueue.Count + (state.CurrentProducing.HasValue ? 1 : 0);
            if (totalCount >= ProductionState.MaxQueueSize) return false;

            // 골드 확인 + 즉시 차감
            int cost = UnitProductionStats.GetGoldCost(type);
            if (!_resource.CanAfford(state.Team, cost)) return false;

            // 인구 확인
            int popCost = UnitProductionStats.GetPopulationCost(type);
            if (!_population.HasPopulation(state.Team, popCost)) return false;

            _resource.SpendGold(state.Team, cost);

            // 자동 모드 해제
            if (state.IsAutoMode)
            {
                state.IsAutoMode = false;
                state.AutoTypes.Clear();
                state.AutoIndex = 0;

                // 현재 자동 생산 중이면 취소 + 골드 환불
                if (state.CurrentProducing.HasValue)
                {
                    int refund = UnitProductionStats.GetGoldCost(state.CurrentProducing.Value);
                    _resource.AddGold(state.Team, refund);

                    state.CurrentProducing = null;
                    state.ElapsedTime = 0f;
                    state.RequiredTime = 0f;
                }
            }

            state.ManualQueue.Add(type);
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// 자동 생산: 유닛 타입 토글 (ON/OFF).
        /// 이미 등록되어 있으면 해제, 없으면 추가.
        /// </summary>
        public bool ToggleAutoProduction(int barracksId, UnitType type)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            // 수동 큐가 있으면 자동 전환 불가
            if (state.ManualQueue.Count > 0) return false;

            int idx = state.AutoTypes.IndexOf(type);
            if (idx >= 0)
            {
                // 이미 등록 → 해제
                state.AutoTypes.RemoveAt(idx);
                if (state.AutoTypes.Count == 0)
                {
                    state.IsAutoMode = false;
                    // 현재 자동 생산 중이면 취소
                    if (state.CurrentProducing.HasValue)
                    {
                        state.CurrentProducing = null;
                        state.ElapsedTime = 0f;
                        state.RequiredTime = 0f;
                    }
                }
                // AutoIndex 보정
                if (state.AutoIndex >= state.AutoTypes.Count)
                    state.AutoIndex = 0;
            }
            else
            {
                // 미등록 → 추가
                state.AutoTypes.Add(type);
                state.IsAutoMode = true;
            }

            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// 생산 큐에서 슬롯 취소. 골드 100% 환불.
        /// slotIndex 0 = 현재 생산 중, 1~2 = 대기 큐.
        /// </summary>
        public bool CancelQueueAt(int barracksId, int slotIndex)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            UnitType? cancelledType = null;

            if (slotIndex == 0)
            {
                // 현재 생산 중인 유닛 취소
                if (!state.CurrentProducing.HasValue) return false;

                cancelledType = state.CurrentProducing.Value;
                state.CurrentProducing = null;
                state.ElapsedTime = 0f;
                state.RequiredTime = 0f;
            }
            else
            {
                // 대기 큐에서 제거 (slotIndex 1 → ManualQueue[0], 2 → ManualQueue[1])
                int queueIndex = slotIndex - 1;
                if (queueIndex < 0 || queueIndex >= state.ManualQueue.Count) return false;

                cancelledType = state.ManualQueue[queueIndex];
                state.ManualQueue.RemoveAt(queueIndex);
            }

            // 골드 100% 환불
            if (cancelledType.HasValue)
            {
                int refund = UnitProductionStats.GetGoldCost(cancelledType.Value);
                _resource.AddGold(state.Team, refund);
            }

            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary> 배럭의 생산 상태 조회. 없으면 null. </summary>
        public ProductionState GetState(int barracksId)
        {
            _states.TryGetValue(barracksId, out var state);
            return state;
        }

        // ====================================================================
        // 랠리 포인트
        // ====================================================================

        /// <summary>
        /// 랠리 포인트 설정.
        /// 배럭 자신의 타일에 설정하면 랠리포인트 해제 (해제 방법).
        /// </summary>
        public void SetRallyPoint(int barracksId, HexCoord target)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return;

            // 배럭 자신의 타일에 설정 → 랠리포인트 해제
            if (target == state.BarracksPosition)
            {
                ClearRallyPoint(barracksId);
                return;
            }

            state.RallyPoint = target;
            GameEvents.OnRallyPointChanged.OnNext(
                new RallyPointChangedEvent(barracksId, target));
        }

        /// <summary> 랠리 포인트 해제. </summary>
        public void ClearRallyPoint(int barracksId)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return;
            state.RallyPoint = null;
            GameEvents.OnRallyPointChanged.OnNext(
                new RallyPointChangedEvent(barracksId, null));
        }

        // ====================================================================
        // 매 프레임 업데이트
        // ====================================================================

        /// <summary>
        /// 매 프레임 ProductionTicker에서 호출.
        /// 모든 배럭의 생산 상태를 진행.
        /// </summary>
        public void Tick(float deltaTime)
        {
            foreach (var state in _states.Values)
            {
                if (state.CurrentProducing == null)
                    TryStartNext(state);
                else
                    TickProduction(state, deltaTime);
            }
        }

        // ====================================================================
        // 내부 로직
        // ====================================================================

        /// <summary>
        /// 다음 생산 대상을 결정하고 시작.
        /// 수동 큐: 골드는 EnqueueUnit 시점에 이미 차감됨 → 즉시 시작.
        /// 자동 모드: 여기서 골드/인구 검증 + 차감.
        /// 스폰 타일은 여기서 확인하지 않음 (생산 완료 시 확인).
        /// </summary>
        private void TryStartNext(ProductionState state)
        {
            UnitType? nextType = null;
            bool isManual = false;

            // 1. 수동 큐 우선
            if (state.ManualQueue.Count > 0)
            {
                nextType = state.ManualQueue[0];
                isManual = true;
            }
            // 2. 자동 모드
            else if (state.IsAutoMode && state.AutoTypes.Count > 0)
            {
                nextType = state.AutoTypes[state.AutoIndex];
            }

            if (!nextType.HasValue) return;

            UnitType type = nextType.Value;

            // 자동 모드일 때만 골드/인구 검증 + 차감 (수동은 EnqueueUnit에서 이미 처리)
            if (!isManual)
            {
                int cost = UnitProductionStats.GetGoldCost(type);
                if (!_resource.CanAfford(state.Team, cost)) return;

                int popCost = UnitProductionStats.GetPopulationCost(type);
                if (!_population.HasPopulation(state.Team, popCost)) return;

                _resource.SpendGold(state.Team, cost);
            }

            // 수동 큐에서 제거 (자동은 순환이므로 제거하지 않음)
            if (isManual)
                state.ManualQueue.RemoveAt(0);

            // 생산 시작
            state.CurrentProducing = type;
            state.ElapsedTime = 0f;
            state.RequiredTime = UnitProductionStats.GetProductionTime(type);

            GameEvents.OnProductionStarted.OnNext(
                new ProductionStartedEvent(state.BarracksId, type));
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(state.BarracksId));
        }

        /// <summary>
        /// 생산 타이머 진행. 완료 시 유닛 스폰.
        /// ElapsedTime은 RequiredTime을 초과하지 않도록 캡 처리.
        /// (스폰 타일이 없으면 생산 완료 상태에서 대기, Progress가 1.0 유지)
        /// </summary>
        private void TickProduction(ProductionState state, float deltaTime)
        {
            state.ElapsedTime += deltaTime;
            if (state.ElapsedTime > state.RequiredTime)
                state.ElapsedTime = state.RequiredTime;

            if (state.ElapsedTime >= state.RequiredTime)
            {
                CompleteProduction(state);
            }
        }

        /// <summary>
        /// 생산 완료 처리.
        /// 배럭 인접 빈 타일에 유닛 생성 + OnUnitProduced 이벤트 발행.
        /// </summary>
        private void CompleteProduction(ProductionState state)
        {
            UnitType type = state.CurrentProducing.Value;

            // 스폰 위치 결정 (배럭 인접 이동 가능 타일)
            HexCoord? spawnTile = FindSpawnTile(state.BarracksPosition);
            if (!spawnTile.HasValue)
            {
                // 스폰 불가 시 대기 (다음 프레임에 재시도)
                return;
            }

            // 유닛 생성
            UnitData unit = _unitSpawn.SpawnUnit(type, state.Team, spawnTile.Value);
            if (unit == null)
            {
                // 스폰 실패 시 대기
                return;
            }

            // 생산 상태 초기화
            state.CurrentProducing = null;
            state.ElapsedTime = 0f;
            state.RequiredTime = 0f;

            // 자동 모드: 다음 타입으로 인덱스 순환
            if (state.IsAutoMode && state.AutoTypes.Count > 0)
            {
                state.AutoIndex = (state.AutoIndex + 1) % state.AutoTypes.Count;
            }

            // 이벤트 발행 (ProductionTicker가 랠리포인트 이동 처리)
            GameEvents.OnUnitProduced.OnNext(
                new UnitProducedEvent(unit, state.RallyPoint));
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(state.BarracksId));
        }

        /// <summary>
        /// 배럭 인접 6타일 중 이동 가능하고 유닛이 없는 빈 타일을 찾아 반환.
        /// </summary>
        private HexCoord? FindSpawnTile(HexCoord barracksPos)
        {
            var walkable = _grid.GetWalkableNeighborCoords(barracksPos);
            foreach (var coord in walkable)
            {
                if (_unitSpawn.GetUnitAt(coord) == null)
                    return coord;
            }
            return null;
        }
    }
}
