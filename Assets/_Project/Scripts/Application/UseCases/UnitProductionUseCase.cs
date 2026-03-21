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
//   수동 시작 시 자동 모드 해제 (선불 환불). 슬롯 0 생산은 완료까지 유지.
//
// 핵심 원칙: 생산이 취소되면 항상 전액 환불. 슬롯 0 생산은 모드 전환 시에도 유지.
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
                // 선불 차감된 슬롯들 전부 환불
                for (int i = 0; i < state.AutoPreChargedCount; i++)
                {
                    // 선불 슬롯은 AutoIndex+1부터 시작 (슬롯 0은 현재 생산 중으로 선불 아님)
                    // AutoTypes에서 순서대로 환불 (AutoIndex 기준 슬롯 1, 2)
                    if (state.AutoTypes.Count > 0)
                    {
                        int slotIdx = (state.AutoIndex + 1 + i) % state.AutoTypes.Count;
                        UnitType preChargedType = state.AutoTypes[slotIdx];
                        _resource.AddGold(state.Team, UnitProductionStats.GetGoldCost(preChargedType));
                    }
                }
                state.AutoPreChargedCount = 0;

                state.IsAutoMode = false;
                state.AutoTypes.Clear();
                state.AutoIndex = 0;

                // CurrentProducing은 건드리지 않음 — 슬롯 0 생산은 완료까지 유지
                // (이미 골드가 차감된 생산이므로 취소 없이 완료 허용)
            }

            state.ManualQueue.Add(type);
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// 자동 생산: 유닛 타입 토글 (ON/OFF).
        /// 이미 등록되어 있으면 해제(환불 분기 있음), 없으면 추가(최대 3개, 선불 차감 가능).
        ///
        /// 슬롯 구조:
        ///   슬롯 0 = 현재 생산 중인 유닛 (CurrentProducing)
        ///   슬롯 1 = AutoTypes에서 다음 생산 예정 (선불 차감 대상)
        ///   슬롯 2 = AutoTypes에서 그 다음 생산 예정 (선불 차감 대상)
        ///
        /// 제거 시 환불 규칙:
        ///   - 슬롯 0에 해당하는 타입 제거: AutoTypes에서만 제거, 생산은 계속, 환불 없음
        ///   - 슬롯 1~2에 해당하는 타입 제거: AutoTypes에서 제거 + 선불 골드 환불
        /// </summary>
        public bool ToggleAutoProduction(int barracksId, UnitType type)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            // 수동 큐가 있으면 자동 전환 불가
            if (state.ManualQueue.Count > 0) return false;

            int idx = state.AutoTypes.IndexOf(type);
            if (idx >= 0)
            {
                // ── 이미 등록된 타입 → 제거 (취소) ──

                // 슬롯 0 해당 여부 판단:
                // 제거하려는 타입이 현재 AutoIndex 위치이고 CurrentProducing과 일치하면 슬롯 0
                bool isSlot0 = state.CurrentProducing.HasValue
                    && state.CurrentProducing.Value == type
                    && state.AutoTypes.Count > 0
                    && idx == state.AutoIndex;

                if (isSlot0)
                {
                    // 슬롯 0: AutoTypes에서만 제거, 생산은 계속 진행, 환불 없음
                    state.AutoTypes.RemoveAt(idx);
                }
                else
                {
                    // 슬롯 1 또는 2: 제거 + 선불 골드 환불
                    state.AutoTypes.RemoveAt(idx);

                    if (state.AutoPreChargedCount > 0)
                    {
                        state.AutoPreChargedCount -= 1;
                        int refund = UnitProductionStats.GetGoldCost(type);
                        _resource.AddGold(state.Team, refund);
                    }
                }

                // AutoTypes가 비면 자동 모드 해제
                // CurrentProducing은 건드리지 않음 — 슬롯 0 생산은 항상 완료까지 허용
                if (state.AutoTypes.Count == 0)
                {
                    state.IsAutoMode = false;
                    state.AutoIndex = 0;
                }
                else
                {
                    // AutoIndex 보정: 제거 후 범위 초과 방지
                    if (state.AutoIndex >= state.AutoTypes.Count)
                        state.AutoIndex = 0;
                }
            }
            else
            {
                // ── 미등록 타입 → 추가 ──

                // 최대 3개 제한
                if (state.AutoTypes.Count >= 3) return false;

                // 신규 추가 시 선불 차감 여부 판단:
                // CurrentProducing이 있고, 추가 후 AutoTypes.Count가 2 이상이면
                // 새 슬롯(1 또는 2)에 들어가는 것이므로 골드/인구 검증 후 즉시 차감
                bool needPreCharge = state.CurrentProducing.HasValue;

                if (needPreCharge)
                {
                    int cost = UnitProductionStats.GetGoldCost(type);
                    if (!_resource.CanAfford(state.Team, cost)) return false;

                    int popCost = UnitProductionStats.GetPopulationCost(type);
                    if (!_population.HasPopulation(state.Team, popCost)) return false;

                    _resource.SpendGold(state.Team, cost);
                    state.AutoPreChargedCount += 1;
                }

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
        ///
        /// 자동 모드일 때의 슬롯 구조:
        ///   슬롯 0 = CurrentProducing (현재 생산 중)
        ///   슬롯 1 = AutoTypes[(AutoIndex+1) % count] (다음 생산 예정, 선불 차감됨)
        ///   슬롯 2 = AutoTypes[(AutoIndex+2) % count] (그 다음 생산 예정, 선불 차감됨)
        /// </summary>
        public bool CancelQueueAt(int barracksId, int slotIndex)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            // ── 자동 모드 분기 ──
            if (state.IsAutoMode)
            {
                if (slotIndex == 0)
                {
                    // 슬롯 0: 현재 생산 중인 유닛 즉시 취소 + 골드 환불 + AutoTypes에서 제거
                    if (!state.CurrentProducing.HasValue) return false;

                    UnitType cancelType = state.CurrentProducing.Value;

                    // AutoTypes에서 현재 생산 중인 타입 제거 (AutoIndex 위치)
                    if (state.AutoTypes.Count > 0 && state.AutoIndex < state.AutoTypes.Count
                        && state.AutoTypes[state.AutoIndex] == cancelType)
                    {
                        state.AutoTypes.RemoveAt(state.AutoIndex);
                    }

                    // AutoIndex 보정
                    if (state.AutoTypes.Count == 0)
                    {
                        state.IsAutoMode = false;
                        state.AutoIndex = 0;
                    }
                    else if (state.AutoIndex >= state.AutoTypes.Count)
                    {
                        state.AutoIndex = 0;
                    }

                    // 생산 취소 + 골드 환불
                    state.CurrentProducing = null;
                    state.ElapsedTime = 0f;
                    state.RequiredTime = 0f;
                    _resource.AddGold(state.Team, UnitProductionStats.GetGoldCost(cancelType));
                }
                else
                {
                    // 슬롯 1~2: 해당 위치의 AutoTypes 타입 제거 + 선불 환불
                    // AutoIndex는 현재 생산 중인 타입을 가리킴
                    // 슬롯 1 → AutoTypes[(AutoIndex + 1) % count]
                    // 슬롯 2 → AutoTypes[(AutoIndex + 2) % count]
                    int count = state.AutoTypes.Count;
                    if (count == 0) return false;

                    // 빈 슬롯 방어: 슬롯 1은 AutoTypes가 2개 이상, 슬롯 2는 3개 이상일 때만 유효
                    // 실제로 표시되지 않는 빈 슬롯 클릭 시 잘못된 타입이 취소되는 것을 방지
                    if (slotIndex == 1 && count < 2) return false;
                    if (slotIndex == 2 && count < 3) return false;

                    // AutoIndex는 현재 생산 중인 타입을 가리킴
                    // 슬롯 1 = AutoIndex+1, 슬롯 2 = AutoIndex+2
                    int autoSlotOffset = slotIndex; // 슬롯1=1, 슬롯2=2
                    int targetIdx = (state.AutoIndex + autoSlotOffset) % count;

                    UnitType cancelType = state.AutoTypes[targetIdx];
                    state.AutoTypes.RemoveAt(targetIdx);

                    // AutoIndex 보정
                    if (state.AutoTypes.Count == 0)
                    {
                        // AutoTypes가 비면 자동 모드 해제
                        // CurrentProducing은 건드리지 않음 — 슬롯 0 생산은 완료까지 유지
                        state.IsAutoMode = false;
                        state.AutoIndex = 0;
                    }
                    else
                    {
                        // 제거된 인덱스가 AutoIndex보다 앞이면 AutoIndex를 1 감소 (밀림 보정)
                        if (targetIdx < state.AutoIndex)
                            state.AutoIndex -= 1;

                        // 범위 초과 방지
                        if (state.AutoIndex >= state.AutoTypes.Count)
                            state.AutoIndex = 0;
                    }

                    // 선불 환불 (선불 차감된 슬롯이면 환불)
                    if (state.AutoPreChargedCount > 0)
                    {
                        state.AutoPreChargedCount -= 1;
                        _resource.AddGold(state.Team, UnitProductionStats.GetGoldCost(cancelType));
                    }
                }

                GameEvents.OnProductionQueueChanged.OnNext(
                    new ProductionQueueChangedEvent(barracksId));
                return true;
            }

            // ── 수동 모드 (기존 로직 유지) ──
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

        /// <summary>
        /// 멀티플레이 클라이언트 전용: ElapsedTime만 진행.
        /// 생산 로직(CompleteProduction, TryStartNext)은 실행하지 않음.
        /// 프로그레스 바 시각 갱신 목적. RequiredTime 도달 시 캡 처리.
        /// </summary>
        public void TickProgressOnly(float deltaTime)
        {
            foreach (var state in _states.Values)
            {
                if (state.CurrentProducing == null) continue;

                state.ElapsedTime += deltaTime;
                if (state.ElapsedTime > state.RequiredTime)
                    state.ElapsedTime = state.RequiredTime;
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
                if (state.AutoPreChargedCount > 0)
                {
                    // 이미 ToggleAutoProduction에서 선불 차감됨 → 중복 차감 방지, 카운터만 감소
                    state.AutoPreChargedCount -= 1;
                }
                else
                {
                    // 선불 없음 → 기존대로 골드/인구 검증 후 차감
                    int cost = UnitProductionStats.GetGoldCost(type);
                    if (!_resource.CanAfford(state.Team, cost)) return;

                    int popCost = UnitProductionStats.GetPopulationCost(type);
                    if (!_population.HasPopulation(state.Team, popCost)) return;

                    _resource.SpendGold(state.Team, cost);
                }
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

            // 자동 모드: 다음 타입으로 인덱스 순환 + 새 슬롯 선불 처리
            if (state.IsAutoMode && state.AutoTypes.Count > 0)
            {
                // 완료된 type이 AutoTypes에 있을 때만 AutoIndex 증가
                // (ToggleAutoProduction으로 취소된 타입은 AutoTypes에 없으므로 AutoIndex 유지)
                if (state.AutoTypes.Contains(type))
                    state.AutoIndex = (state.AutoIndex + 1) % state.AutoTypes.Count;

                // 생산 완료로 슬롯 0이 비면서 뒤의 슬롯들이 앞으로 밀림
                // 선불이 필요한 슬롯(슬롯 1~2)이 있으면 골드 선불 차감
                // 예: AutoTypes 3개, 생산 완료 → 새 슬롯 2에 대해 선불 필요
                if (state.AutoTypes.Count >= 2 && state.AutoPreChargedCount < state.AutoTypes.Count - 1)
                {
                    int slotsToPrecharge = (state.AutoTypes.Count - 1) - state.AutoPreChargedCount;
                    for (int i = 0; i < slotsToPrecharge; i++)
                    {
                        // 선불할 슬롯의 AutoTypes 인덱스 계산
                        // AutoPreChargedCount는 이미 선불된 슬롯 수 → 그 다음 슬롯부터
                        int slotAutoIdx = (state.AutoIndex + state.AutoPreChargedCount) % state.AutoTypes.Count;
                        UnitType nextType = state.AutoTypes[slotAutoIdx];
                        int cost = UnitProductionStats.GetGoldCost(nextType);

                        // 골드 부족 시 더 이상 선불 불가 → 중단
                        if (!_resource.CanAfford(state.Team, cost)) break;

                        _resource.SpendGold(state.Team, cost);
                        state.AutoPreChargedCount += 1;
                    }
                }
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
