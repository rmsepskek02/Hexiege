// ============================================================================
// UnitProductionUseCase.cs
// 배럭별 유닛 생산 큐, 타이머, 자동/수동 모드를 관리하는 핵심 UseCase.
//
// ──────────────────────────────────────────────────────────────────────────
// [재작성 — 2026-04-19]
// 이중 큐 구조(ManualQueue + AutoEntries)를 단일 PendingQueue(List<QueueSlot>)로 통합.
// PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2 불변식 유지 → isNormalAutoState 제거.
// ──────────────────────────────────────────────────────────────────────────
//
// 생산 흐름:
//   1. 배럭 배치 시 RegisterBarracks() → ProductionState 생성
//   2. 플레이어가 수동 큐 추가(EnqueueUnit) 또는 자동 토글(ToggleAutoProduction)
//   3. Tick(dt) 매 프레임 호출 → TryStartNext로 다음 생산 결정
//   4. 골드/인구 검증 → 골드 차감 → 타이머 시작
//   5. 타이머 완료 → CompleteProduction → 유닛 스폰 + 자동이면 AutoTypes 순환 재추가
//
// 전역 규칙 (5가지 + Rule 2-1):
//   Rule 1   : 슬롯 X 버튼으로 직접 취소하면 항상 전액 환불 (버튼 탭 취소는 환불 없음)
//   Rule 2   : 자동 취소 시 골드 차감된(IsCharged=true) 항목은 수동으로 이관(생산 계속)
//   Rule 3   : 수동 추가 시 자동 모드 전체 해제 (이관 먼저 → AutoTypes 클리어)
//   Rule 4   : 생산 큐 최대 3개 (CurrentProducing + IsCharged=true PendingQueue 합산)
//   Rule 5   : 골드 차감 = 슬롯 표시 시점 (자동 항목). 수동 항목은 등록 시 즉시 차감
//   Rule 2-1 : 자동 등록 타입이 PendingQueue 마지막 수동 항목과 같으면 IsAuto=true로 전환(중복 금지)
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
        /// 배럭(생산건물)이 배치될 때 호출. ProductionState를 생성하여 등록.
        /// 종족별 라인의 모든 단계(예: TrainingCamp/WarAcademy/HumanBarracks 등)를
        /// BuildingTypeHelper.IsProductionBuilding으로 일괄 인식한다.
        /// </summary>
        public void RegisterBarracks(BuildingData barracks)
        {
            if (barracks == null || !BuildingTypeHelper.IsProductionBuilding(barracks.Type)) return;
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
        ///
        /// 처리 순서:
        ///   1) Rule 3 — 자동 모드 해제 (IsCharged=true인 자동 항목은 IsAuto=false로 이관(Rule 2),
        ///      IsCharged=false 자동 항목은 제거, AutoTypes 클리어)
        ///   2) Rule 4 — 큐 상한(3개) 체크: CurrentProducing + IsCharged=true 항목 합산
        ///   3) 골드/인구 즉시 검증 + 차감
        ///   4) PendingQueue 맨 뒤에 수동 항목(IsAuto=false, IsCharged=true) 추가
        /// </summary>
        public bool EnqueueUnit(int barracksId, UnitType type)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            // Rule 3 + Rule 2: 수동 항목이 들어오면 자동 모드 해제
            // (이미 차감된 자동 항목은 수동으로 이관, 미차감 항목은 제거)
            if (state.IsAutoMode)
                DisableAutoMode(state);

            // Rule 4: 큐 상한(3개) 초과 시 추가 거부
            if (!HasQueueCapacity(state)) return false;

            // 골드/인구 즉시 검증 + 차감 (수동 항목은 등록 시 항상 IsCharged=true)
            if (!TryChargeResources(state, type)) return false;

            // PendingQueue 맨 뒤에 수동 항목 추가
            // PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2 불변식 유지.
            state.PendingQueue.Add(new QueueSlot(type, false, true));

            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// 자동 생산: 유닛 타입 토글 (ON/OFF).
        ///
        /// 이미 등록된 타입이면 제거:
        ///   AutoTypes에서 제거 + PendingQueue에서 IsAuto=true & Type==type 항목 처리
        ///     - IsCharged=true → IsAuto=false 전환 (Rule 2: 수동 이관, 생산 계속)
        ///     - IsCharged=false → 제거 (환불 불필요)
        ///
        /// 미등록 타입이면 추가:
        ///   AutoTypes 최대 3개 제한 적용.
        ///   [Rule 2-1] PendingQueue 마지막이 같은 타입 수동 항목이면 IsAuto=true로 전환 후 반환.
        ///   그 외 canShow 판정에 따라 IsCharged=true(즉시 차감) 또는 false(미차감) 상태로 추가.
        /// </summary>
        public bool ToggleAutoProduction(int barracksId, UnitType type)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            // AutoTypes 내 등록 여부로 ON/OFF 분기
            int typeIndex = state.AutoTypes.IndexOf(type);

            if (typeIndex >= 0)
                return UnregisterAutoType(state, barracksId, type, typeIndex);

            return RegisterAutoType(state, barracksId, type);
        }

        /// <summary>
        /// 자동 생산에서 특정 유닛 타입 제거 (자동 해제).
        ///
        /// AutoTypes에서 제거 + PendingQueue 내 해당 타입 자동 항목을 Rule 2에 따라 처리:
        ///   - IsCharged=true → IsAuto=false 전환 (수동 이관, 생산 계속)
        ///   - IsCharged=false → 제거 (환불 불필요)
        /// </summary>
        private bool UnregisterAutoType(
            ProductionState state, int barracksId, UnitType type, int typeIndex)
        {
            state.AutoTypes.RemoveAt(typeIndex);

            // PendingQueue에서 해당 타입의 자동 항목을 Rule 2에 따라 이관 또는 제거.
            // 뒤에서 앞으로 순회 — 제거 중 인덱스 밀림 방지.
            MigrateOrRemoveAutoSlots(state, type);

            NormalizeAutoCycleIndex(state);

            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// 자동 생산에 새 유닛 타입 등록.
        ///
        /// 등록 전 기존 슬롯과의 중복 방지를 위한 전환 처리(Rule 20, Rule 2-1)를 시도하고,
        /// 해당하지 않으면 새 QueueSlot을 PendingQueue에 추가한다.
        /// </summary>
        private bool RegisterAutoType(ProductionState state, int barracksId, UnitType type)
        {
            // 자동 등록 상한(3종류)
            if (state.AutoTypes.Count >= 3) return false;

            // Rule 20 확장: 슬롯0(CurrentProducing)이 같은 타입 수동 항목이면 수동→자동 전환.
            // 슬롯0에서 수동으로 A를 생산 중일 때 A를 자동등록하면,
            // 슬롯1에 A를 새로 추가하지 않고 슬롯0 자체를 "자동"으로 전환한다.
            // → 완료 시 CompleteProduction의 wasAuto=true 조건을 만족하여 자동 순환이 시작됨.
            // → 골드 이중 차감 없음 (슬롯0의 골드는 이미 수동 등록 시 차감됨).
            //
            // PendingQueue가 비어있을 때만 적용한다.
            // 대기 중인 다른 항목이 있으면 슬롯0 전환 대신 AddNewAutoSlot으로 슬롯3에 추가한다.
            // 예: [Assault(슬롯1)] [Pistoleer(슬롯2)] 상태에서 Assault 재등록 →
            //     Pistoleer가 중간에 있으므로 슬롯3 추가가 올바른 동작이다 (중복 아님).
            if (state.PendingQueue.Count == 0 && TryConvertCurrentToAuto(state, barracksId, type))
                return true;

            // Rule 2-1: PendingQueue 마지막 수동 항목이 같은 타입이면 IsAuto=true로 전환.
            // 사용자가 수동으로 [3,2,1] 등록 후 마지막 '1'에 대해 자동을 활성화하면
            // "마지막 수동을 자동으로 전환" 의도로 해석하여 중복 항목 추가 방지.
            if (TryConvertLastPendingToAuto(state, barracksId, type))
                return true;

            // 기존 슬롯 전환 불가 → 새 자동 항목을 PendingQueue에 추가.
            return AddNewAutoSlot(state, barracksId, type);
        }

        /// <summary>
        /// Rule 20 확장: 슬롯0(CurrentProducing)이 같은 타입 수동 항목이면 자동으로 전환.
        /// 전환 성공 시 true 반환.
        /// </summary>
        private bool TryConvertCurrentToAuto(
            ProductionState state, int barracksId, UnitType type)
        {
            if (!state.CurrentProducing.HasValue ||
                state.CurrentProducing.Value != type ||
                state.CurrentIsAuto)
                return false;

            state.CurrentIsAuto = true;
            state.AutoTypes.Add(type);
            NormalizeAutoCycleIndex(state);

            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// Rule 2-1: PendingQueue 마지막 수동 항목이 같은 타입이면 IsAuto=true로 전환.
        /// 골드는 수동 등록 시 이미 차감되었으므로 추가 차감 없음.
        /// 전환 성공 시 true 반환.
        /// </summary>
        private bool TryConvertLastPendingToAuto(
            ProductionState state, int barracksId, UnitType type)
        {
            if (state.PendingQueue.Count == 0) return false;

            int lastIdx = state.PendingQueue.Count - 1;
            QueueSlot last = state.PendingQueue[lastIdx];

            if (last.IsAuto || last.Type != type) return false;

            last.IsAuto = true;
            state.PendingQueue[lastIdx] = last;

            state.AutoTypes.Add(type);
            NormalizeAutoCycleIndex(state);

            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// 새 자동 항목을 PendingQueue에 추가.
        /// Rule 5 판정으로 즉시 표시 가능 여부를 결정하고, 가능하면 골드/인구 차감.
        /// 큐가 비어있었으면 TryStartNext를 즉시 호출하여 슬롯 깜빡임 방지.
        /// </summary>
        private bool AddNewAutoSlot(ProductionState state, int barracksId, UnitType type)
        {
            // Rule 5: 슬롯1/슬롯2에 즉시 표시 가능한지 판정.
            // 표시 가능 = CurrentProducing이 있고, IsCharged=true 항목이 2개 미만.
            bool canShow = state.CurrentProducing.HasValue && state.ChargedPendingCount() < 2;

            // BUG-15 방지: 현재 자동으로 같은 타입을 생산 중이면 이중 차감 위험 → 미차감 상태로 보류.
            if (canShow && state.CurrentIsAuto && state.CurrentProducing == type)
                canShow = false;

            bool isCharged = false;
            if (canShow)
            {
                // 슬롯에 즉시 표시 가능 → 골드/인구 검증 + 차감
                if (!TryChargeResources(state, type)) return false;
                isCharged = true;
            }
            // else: 큐가 꽉 차 있거나 특수 조건 → 미차감 상태로 대기.
            //       TryStartNext 또는 ChargeVisibleSlots가 슬롯 승격 시 차감 처리.

            state.PendingQueue.Add(new QueueSlot(type, true, isCharged));
            state.AutoTypes.Add(type);
            NormalizeAutoCycleIndex(state);

            // 슬롯 깜빡임 버그 방지: 큐가 비어있었으면 즉시 TryStartNext 호출.
            // TryStartNext 내부에서 OnProductionQueueChanged를 발행하므로 중복 방지를 위해 Early Return.
            if (!state.CurrentProducing.HasValue)
            {
                TryStartNext(state);
                return true;
            }

            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// 생산 큐에서 슬롯 취소.
        /// slotIndex 0 = 현재 생산 중 / 1 = 슬롯1 (PendingQueue[0]) / 2 = 슬롯2 (PendingQueue[1]).
        ///
        /// 공통 원칙 (Rule 1): IsCharged=true 항목은 전액 환불.
        ///   - 슬롯0: CurrentProducing은 항상 차감된 상태이므로 전액 환불
        ///   - 슬롯1/2: PendingQueue[0]/[1]은 IsCharged=true인 경우만 환불(Rule 5 참조)
        /// </summary>
        public bool CancelQueueAt(int barracksId, int slotIndex)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            if (slotIndex == 0)
                return CancelCurrentProducing(state, barracksId);

            // 슬롯1=PendingQueue[0], 슬롯2=PendingQueue[1]
            // 두 슬롯의 처리 로직이 동일하므로 pendingIndex로 통합.
            int pendingIndex = slotIndex - 1;
            return CancelPendingSlot(state, barracksId, pendingIndex);
        }

        /// <summary>
        /// 슬롯0(현재 생산 중) 취소.
        /// Rule 1에 따라 전액 환불 + 생산 상태 초기화.
        /// 자동 항목이었으면 AutoTypes에서도 제거.
        /// </summary>
        private bool CancelCurrentProducing(ProductionState state, int barracksId)
        {
            if (!state.CurrentProducing.HasValue) return false;

            UnitType cancelType = state.CurrentProducing.Value;

            // [BUG-FIX 2026-04-19] 자동 항목 여부를 초기화 전에 캡처.
            // state.CurrentIsAuto = false 이후에 읽으면 항상 false가 되어
            // AutoTypes 제거 분기에 진입 못하는 문제를 방지.
            bool wasAuto = state.CurrentIsAuto;

            // Rule 1: 현재 생산 중인 유닛은 항상 골드 차감 상태 → 전액 환불
            _resource.AddGold(state.Team, UnitProductionStats.GetGoldCost(cancelType));

            // 생산 상태 초기화 (다음 Tick에서 TryStartNext가 다음 항목 선택)
            state.CurrentProducing = null;
            state.CurrentIsAuto = false;
            state.ElapsedTime = 0f;
            state.RequiredTime = 0f;

            // 자동 항목 취소 시 AutoTypes에서도 제거 → 재생산 방지.
            // 잔여 PendingQueue 내 같은 타입 자동 항목도 Rule 2에 따라 처리.
            if (wasAuto)
                CancelAutoTypeIfNeeded(state, cancelType);

            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// PendingQueue 내 슬롯 취소 (슬롯1=pendingIndex 0, 슬롯2=pendingIndex 1).
        /// Rule 1에 따라 IsCharged=true면 환불.
        /// 제거 후 ChargeVisibleSlots로 승격된 항목의 차감 처리.
        /// 자동 항목이었으면 AutoTypes에서도 제거.
        /// </summary>
        private bool CancelPendingSlot(ProductionState state, int barracksId, int pendingIndex)
        {
            if (state.PendingQueue.Count <= pendingIndex) return false;

            QueueSlot cancelled = state.PendingQueue[pendingIndex];

            // Rule 1: IsCharged=true면 환불, false면 환불 없음(미차감)
            if (cancelled.IsCharged)
                _resource.AddGold(state.Team, UnitProductionStats.GetGoldCost(cancelled.Type));

            state.PendingQueue.RemoveAt(pendingIndex);

            // 제거로 인해 뒤쪽 항목이 승격 → 승격된 자동 미차감 항목 차감 처리.
            ChargeVisibleSlots(state);

            // [BUG-FIX 2026-04-19] 취소된 항목이 자동이었다면 AutoTypes에서도 제거.
            // RemoveAt 이후 호출해도 안전 — cancelled는 값으로 복사된 상태.
            if (cancelled.IsAuto)
                CancelAutoTypeIfNeeded(state, cancelled.Type);

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

        /// <summary>
        /// 배럭 철거 시 생산 큐 전체를 한 번에 취소하고, 이미 차감된 골드를 전액 환불한다.
        ///
        /// 처리 순서:
        ///   1) 랠리포인트 마커 제거 이벤트 발행 (UnregisterBarracks 전에 호출해야 state에 접근 가능)
        ///   2) state.CurrentProducing 이 있으면 해당 유닛 비용 전액 환불 (항상 IsCharged=true)
        ///   3) state.PendingQueue 전체 순회:
        ///        - IsCharged=true 항목 → 전액 환불
        ///        - IsCharged=false 항목 → 환불 없이 제거 (골드가 아직 차감되지 않았으므로)
        ///   4) 생산 상태 초기화 (CurrentProducing, AutoTypes, PendingQueue 등)
        ///   5) OnProductionQueueChanged 이벤트 발행 → UI 즉시 갱신
        ///   6) UnregisterBarracks 호출 → ProductionState 딕셔너리에서 제거
        ///
        /// 근거: GameSystemRules.md — 건물 철거 시스템 규칙 5
        ///   이미 차감된 항목은 전액 환불, 미차감 항목은 환불 없이 제거.
        /// </summary>
        /// <param name="barracksId">철거할 배럭의 건물 Id</param>
        public void CancelAllQueue(int barracksId)
        {
            // state가 없으면(비생산 건물이거나 이미 해제됨) 즉시 반환
            if (!_states.TryGetValue(barracksId, out var state)) return;

            // ─── Step 1: 랠리포인트 마커 제거 ─────────────────────────────────
            // UnregisterBarracks가 호출되면 state가 사라지므로, 이벤트 발행 전에 먼저 처리.
            ClearRallyPoint(barracksId);

            // ─── Step 2: 현재 생산 중인 유닛 환불 ─────────────────────────────
            // CurrentProducing은 항상 골드가 차감된 상태(IsCharged=true에 해당)이므로 전액 환불.
            if (state.CurrentProducing.HasValue)
            {
                _resource.AddGold(
                    state.Team,
                    UnitProductionStats.GetGoldCost(state.CurrentProducing.Value));
            }

            // ─── Step 3: PendingQueue 전체 환불/제거 ───────────────────────────
            // IsCharged=true 항목은 골드가 이미 차감됐으므로 전액 환불.
            // IsCharged=false 항목은 미차감이므로 환불 없이 제거.
            foreach (var slot in state.PendingQueue)
            {
                if (slot.IsCharged)
                {
                    _resource.AddGold(
                        state.Team,
                        UnitProductionStats.GetGoldCost(slot.Type));
                }
                // IsCharged=false 항목은 골드 차감이 없었으므로 그냥 넘어감
            }

            // ─── Step 4: 생산 상태 전체 초기화 ────────────────────────────────
            // 리스트/필드를 비워두어야 UnregisterBarracks 이후 혹시 남은 참조가 오동작하지 않음.
            state.PendingQueue.Clear();
            state.AutoTypes.Clear();
            state.AutoCycleIndex = 0;
            state.CurrentProducing = null;
            state.CurrentIsAuto = false;
            state.ElapsedTime = 0f;
            state.RequiredTime = 0f;

            // ─── Step 5: UI에 큐 변경 알림 ─────────────────────────────────────
            // 팝업이 아직 열려 있다면 슬롯 이미지를 빈 상태로 즉시 갱신.
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));

            // ─── Step 6: ProductionState 딕셔너리에서 제거 ─────────────────────
            // 이후 Tick()에서 이 배럭의 state에 접근하지 않도록 마지막에 제거.
            UnregisterBarracks(barracksId);
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
                new RallyPointChangedEvent(barracksId, target, state.Team));
        }

        /// <summary> 랠리 포인트 해제. </summary>
        public void ClearRallyPoint(int barracksId)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return;
            state.RallyPoint = null;
            GameEvents.OnRallyPointChanged.OnNext(
                new RallyPointChangedEvent(barracksId, null, state.Team));
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
        ///
        /// 우선순위:
        ///   1) PendingQueue.Count > 0 : PendingQueue[0]을 꺼내 슬롯0으로 올림.
        ///      IsAuto && !IsCharged이면 이 시점에 골드/인구 검증+차감(자원 부족 시 원상복구하고 대기).
        ///   2) IsAutoMode : AutoTypes[AutoCycleIndex % Count]를 직접 생성하여 시작(순환).
        ///
        /// 생산 시작 후 ChargeVisibleSlots()로 PendingQueue[0]/[1] 차감.
        /// </summary>
        private void TryStartNext(ProductionState state)
        {
            if (state.PendingQueue.Count > 0)
            {
                // PendingQueue의 첫 항목을 꺼내 슬롯0으로 올림
                QueueSlot slot = state.PendingQueue[0];
                state.PendingQueue.RemoveAt(0);

                // 자동 항목이면서 아직 미차감이면 이 시점에 검증+차감
                if (slot.IsAuto && !slot.IsCharged)
                {
                    int cost = UnitProductionStats.GetGoldCost(slot.Type);
                    int popCost = UnitProductionStats.GetPopulationCost(slot.Type);

                    if (!_resource.CanAfford(state.Team, cost)
                        || !_population.HasPopulation(state.Team, popCost))
                    {
                        // 자원 부족 시 자동 생산 즉시 취소.
                        //   - 미차감(IsCharged=false) 항목이므로 환불은 불필요.
                        //   - 해당 타입을 AutoTypes에서 제거하고 잔여 자동 항목도 Rule 2에 따라 정리.
                        //   - 큐 변경 이벤트를 발행해 UI가 즉시 갱신되도록 한다.
                        CancelAutoTypeIfNeeded(state, slot.Type);
                        GameEvents.OnProductionQueueChanged.OnNext(
                            new ProductionQueueChangedEvent(state.BarracksId));
                        return;
                    }

                    _resource.SpendGold(state.Team, cost);
                    slot.IsCharged = true;
                }

                // 생산 시작
                state.CurrentProducing = slot.Type;
                state.CurrentIsAuto = slot.IsAuto;
                state.ElapsedTime = 0f;
                state.RequiredTime = UnitProductionStats.GetProductionTime(slot.Type);

                // 슬롯이 한 칸 앞으로 당겨졌으므로 새로 슬롯1/슬롯2로 올라온 항목의 차감을 시도
                ChargeVisibleSlots(state);

                GameEvents.OnProductionStarted.OnNext(
                    new ProductionStartedEvent(state.BarracksId, slot.Type));
                GameEvents.OnProductionQueueChanged.OnNext(
                    new ProductionQueueChangedEvent(state.BarracksId));
                return;
            }

            // PendingQueue가 비었지만 자동 모드라면 AutoTypes에서 직접 순환 생성
            if (state.IsAutoMode)
            {
                int count = state.AutoTypes.Count;
                if (count <= 0) return;

                int idx = ((state.AutoCycleIndex % count) + count) % count; // 음수 방지
                UnitType type = state.AutoTypes[idx];

                int cost = UnitProductionStats.GetGoldCost(type);
                int popCost = UnitProductionStats.GetPopulationCost(type);
                if (!_resource.CanAfford(state.Team, cost)
                    || !_population.HasPopulation(state.Team, popCost))
                {
                    // 자원 부족 → 자동 생산 전체 즉시 취소.
                    //   - AutoTypes / AutoCycleIndex 초기화.
                    //   - PendingQueue의 IsCharged=false 자동 항목 제거(미차감이므로 환불 불필요).
                    //   - IsCharged=true 자동 항목은 Rule 2에 따라 IsAuto=false로 수동 이관(이미 차감 → 환불 없이 생산 계속).
                    //   - 큐 변경 이벤트로 UI 즉시 갱신.
                    state.AutoTypes.Clear();
                    state.AutoCycleIndex = 0;

                    for (int i = state.PendingQueue.Count - 1; i >= 0; i--)
                    {
                        QueueSlot s = state.PendingQueue[i];
                        if (!s.IsAuto) continue; // 수동 항목은 건드리지 않음

                        if (s.IsCharged)
                        {
                            // Rule 2: 이미 차감 → 수동으로 이관
                            s.IsAuto = false;
                            state.PendingQueue[i] = s;
                        }
                        else
                        {
                            // 미차감 자동 대기 항목 → 제거
                            state.PendingQueue.RemoveAt(i);
                        }
                    }

                    GameEvents.OnProductionQueueChanged.OnNext(
                        new ProductionQueueChangedEvent(state.BarracksId));
                    return;
                }

                _resource.SpendGold(state.Team, cost);
                state.AutoCycleIndex = (idx + 1) % count;

                state.CurrentProducing = type;
                state.CurrentIsAuto = true;
                state.ElapsedTime = 0f;
                state.RequiredTime = UnitProductionStats.GetProductionTime(type);

                // AutoTypes가 여러 개면 PendingQueue에 나머지 타입 하나가 들어있을 수 있음.
                // 빈 상태면 이 블록은 아무것도 하지 않음.
                ChargeVisibleSlots(state);

                GameEvents.OnProductionStarted.OnNext(
                    new ProductionStartedEvent(state.BarracksId, type));
                GameEvents.OnProductionQueueChanged.OnNext(
                    new ProductionQueueChangedEvent(state.BarracksId));
            }
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
        /// 유닛 스폰 + 자동 순환 재추가(PendingQueue 끝) + 슬롯 차감 재적용.
        /// 스폰 실패 시 현재 상태 유지(다음 프레임에 재시도).
        /// </summary>
        private void CompleteProduction(ProductionState state)
        {
            UnitType type = state.CurrentProducing.Value;
            bool wasAuto = state.CurrentIsAuto;

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
            state.CurrentIsAuto = false;
            state.ElapsedTime = 0f;
            state.RequiredTime = 0f;

            // 자동 항목이 완료된 경우: 같은 타입을 PendingQueue 끝에 재추가하여 순환을 이어감.
            // AutoTypes에 해당 타입이 아직 남아있을 때만 재추가(자동 취소된 경우 재추가 안 함).
            // 이때 미차감(IsCharged=false)으로 넣어서 슬롯에 올라오는 시점에 차감되도록 함.
            if (wasAuto && state.AutoTypes.Contains(type))
            {
                state.PendingQueue.Add(new QueueSlot(type, true, false));
            }

            // [2026-06-05 깜빡임 수정] AddNewAutoSlot의 2026-04-19 수정과 동일한 패턴.
            // 기존 코드는 여기서 ChargeVisibleSlots + OnProductionQueueChanged를 발행하고,
            // 실제 다음 항목을 슬롯0으로 올리는 TryStartNext는 다음 프레임에야 실행됐다.
            // 그 결과 "슬롯1=비어있음, 슬롯2=대기유닛" 상태가 한 프레임 동안 UI에 노출되어
            // 슬롯2가 1프레임 깜빡이는 버그가 발생했다.
            //
            // 해결: CompleteProduction 직후 TryStartNext를 즉시 호출하여 같은 프레임 안에
            // 슬롯 상태를 정착시킨다.
            //  - ChargeVisibleSlots는 TryStartNext 내부에서 호출되므로 여기서 제거한다.
            //  - OnProductionQueueChanged도 TryStartNext 내부에서 발행되므로 여기서 직접 발행하지 않는다.
            //  - 단, OnUnitProduced(랠리포인트 이동 처리용)는 TryStartNext와 무관하므로 그대로 발행한다.
            // BarracksId를 함께 전달 — AI(AIOpponentController)가 이 값으로
            // 어느 배럭에서 유닛이 나왔는지 식별하여 다음 유닛을 재등록한다(규칙 12).
            GameEvents.OnUnitProduced.OnNext(
                new UnitProducedEvent(unit, state.RallyPoint, state.BarracksId));

            TryStartNext(state);

            // TryStartNext가 아무것도 시작하지 않은 경우(큐 비어있음 + 자동 모드 아님)에는
            // 내부에서 이벤트를 발행하지 않으므로, 여기서 수동으로 발행해 UI를 갱신한다.
            // (생산이 시작됐다면 CurrentProducing에 값이 들어가고 이벤트도 이미 발행된 상태다.)
            if (!state.CurrentProducing.HasValue)
            {
                GameEvents.OnProductionQueueChanged.OnNext(
                    new ProductionQueueChangedEvent(state.BarracksId));
            }
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

        // ====================================================================
        // 슬롯 차감 헬퍼
        // ====================================================================

        /// <summary>
        /// PendingQueue[0]/[1] 중 IsCharged=false인 자동 항목을 시점에 맞춰 차감.
        /// 수동 항목은 항상 IsCharged=true이므로 이 경로에 걸리지 않음.
        ///
        /// 자원 부족 시 break — 해당 위치 이후 항목도 차감하지 않음.
        /// 차감 실패 항목은 IsCharged=false 상태로 남아, 다음 호출 시점에 재시도.
        ///
        /// 호출 시점: TryStartNext 직후, CancelQueueAt(1/2) 직후, CompleteProduction 직후.
        /// </summary>
        private void ChargeVisibleSlots(ProductionState state)
        {
            int limit = state.PendingQueue.Count < 2 ? state.PendingQueue.Count : 2;
            for (int i = 0; i < limit; i++)
            {
                QueueSlot s = state.PendingQueue[i];
                if (s.IsCharged) continue;

                int cost = UnitProductionStats.GetGoldCost(s.Type);
                int popCost = UnitProductionStats.GetPopulationCost(s.Type);

                if (!_resource.CanAfford(state.Team, cost)) break;
                if (!_population.HasPopulation(state.Team, popCost)) break;

                _resource.SpendGold(state.Team, cost);
                s.IsCharged = true;
                state.PendingQueue[i] = s;
            }
        }

        /// <summary>
        /// 슬롯 취소 시 해당 타입이 자동 항목이었으면 AutoTypes에서 제거 + 잔여 자동 항목 Rule 2 처리.
        ///
        /// 호출 규칙:
        ///   slotIndex == 0: wasAuto=CurrentIsAuto 값을 취소 전에 캡처하여 전달
        ///   slotIndex == 1/2: cancelled.IsAuto가 true일 때 전달
        ///
        /// 처리 내용:
        ///   1) AutoTypes에서 해당 타입 제거 (이미 없으면 수동 이관된 항목이므로 조기 반환)
        ///   2) PendingQueue 내 같은 타입의 자동 항목을 Rule 2에 따라 처리
        ///      - IsCharged=true → 수동 이관(IsAuto=false 전환, 환불 없이 생산 계속)
        ///      - IsCharged=false → 단순 제거(미차감이므로 환불 불필요)
        ///   3) AutoCycleIndex를 새 AutoTypes 범위로 보정
        ///
        /// 이 처리가 없으면 자동 항목 슬롯을 취소해도 다음 Tick의 auto 사이클에서 같은 타입이
        /// 다시 PendingQueue에 추가되어 "취소해도 계속 생산되는" 버그가 발생.
        /// </summary>
        private void CancelAutoTypeIfNeeded(ProductionState state, UnitType type)
        {
            // AutoTypes에 없으면 이미 제거되었거나 애초에 수동 항목 → 처리 불필요
            int idx = state.AutoTypes.IndexOf(type);
            if (idx < 0) return;

            // 자동 등록 목록에서 해당 타입 제거 — 다음 auto 사이클에서 재생산되지 않도록
            state.AutoTypes.RemoveAt(idx);

            // PendingQueue에 남아있는 같은 타입의 자동 항목도 함께 정리
            // 뒤에서 앞으로 순회 — RemoveAt 사용 시 인덱스가 밀리는 문제 방지
            for (int i = state.PendingQueue.Count - 1; i >= 0; i--)
            {
                QueueSlot s = state.PendingQueue[i];
                if (!s.IsAuto || s.Type != type) continue;

                if (s.IsCharged)
                {
                    // Rule 2: 이미 골드가 차감된 항목은 수동으로 이관(환불 없이 생산 계속)
                    s.IsAuto = false;
                    state.PendingQueue[i] = s;
                }
                else
                {
                    // 미차감 대기 항목 → 제거 (환불 불필요)
                    state.PendingQueue.RemoveAt(i);
                }
            }

            // AutoTypes 길이가 줄어들었으므로 AutoCycleIndex가 범위를 벗어나지 않도록 보정
            NormalizeAutoCycleIndex(state);
        }

        // ====================================================================
        // 공통 검증/처리 헬퍼
        // ====================================================================

        /// <summary>
        /// Rule 3 + Rule 2: 자동 모드를 전체 해제.
        /// 수동 항목이 들어올 때 호출되어, 자동 모드를 해제하면서
        /// 이미 차감된(IsCharged=true) 자동 항목은 수동으로 이관(Rule 2),
        /// 미차감 자동 항목은 제거한다.
        /// CurrentProducing은 건드리지 않음 — 진행 중인 생산은 완료까지 유지.
        /// </summary>
        private void DisableAutoMode(ProductionState state)
        {
            // PendingQueue 내 자동 항목을 Rule 2에 따라 이관/제거.
            // 모든 자동 타입을 대상으로 하므로 type 필터 없이 전체 순회.
            for (int i = state.PendingQueue.Count - 1; i >= 0; i--)
            {
                QueueSlot s = state.PendingQueue[i];
                if (!s.IsAuto) continue;

                if (s.IsCharged)
                {
                    // Rule 2: 이미 차감된 자동 항목 → 수동으로 이관 (생산 계속)
                    s.IsAuto = false;
                    state.PendingQueue[i] = s;
                }
                else
                {
                    // 미차감 자동 대기 항목 → 제거 (환불 불필요)
                    state.PendingQueue.RemoveAt(i);
                }
            }

            state.AutoTypes.Clear();
            state.AutoCycleIndex = 0;
        }

        /// <summary>
        /// PendingQueue에서 특정 타입의 자동 항목을 Rule 2에 따라 이관 또는 제거.
        /// IsCharged=true → IsAuto=false 전환 (수동 이관, 환불 없음).
        /// IsCharged=false → 제거 (환불 불필요).
        /// 뒤에서 앞으로 순회하여 인덱스 밀림 방지.
        /// </summary>
        private void MigrateOrRemoveAutoSlots(ProductionState state, UnitType type)
        {
            for (int i = state.PendingQueue.Count - 1; i >= 0; i--)
            {
                QueueSlot s = state.PendingQueue[i];
                if (!s.IsAuto || s.Type != type) continue;

                if (s.IsCharged)
                {
                    // Rule 2: 슬롯에 표시된 항목 → 수동으로 이관 (생산 계속, 환불 없음)
                    s.IsAuto = false;
                    state.PendingQueue[i] = s;
                }
                else
                {
                    // 미차감 자동 대기 항목 → 제거 (환불 없음)
                    state.PendingQueue.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Rule 4: 큐에 새 항목을 추가할 여유 공간이 있는지 확인.
        /// CurrentProducing 1슬롯 + IsCharged=true 항목의 합이 MaxQueueSize(3) 미만이면 true.
        /// </summary>
        private bool HasQueueCapacity(ProductionState state)
        {
            int slotsUsed = (state.CurrentProducing.HasValue ? 1 : 0) + state.ChargedPendingCount();
            return slotsUsed + 1 <= ProductionState.MaxQueueSize;
        }

        /// <summary>
        /// 골드/인구 즉시 검증 후 차감.
        /// 자원 부족 시 false 반환 (차감 없음).
        /// </summary>
        private bool TryChargeResources(ProductionState state, UnitType type)
        {
            int cost = UnitProductionStats.GetGoldCost(type);
            if (!_resource.CanAfford(state.Team, cost)) return false;

            int popCost = UnitProductionStats.GetPopulationCost(type);
            if (!_population.HasPopulation(state.Team, popCost)) return false;

            _resource.SpendGold(state.Team, cost);
            return true;
        }

        /// <summary>
        /// AutoCycleIndex가 AutoTypes의 현재 길이 범위 안에 들도록 보정.
        /// AutoTypes가 비어 있으면 0으로 리셋.
        /// </summary>
        private void NormalizeAutoCycleIndex(ProductionState state)
        {
            int count = state.AutoTypes.Count;
            if (count <= 0)
            {
                state.AutoCycleIndex = 0;
                return;
            }
            // 음수나 초과값을 안전하게 범위로 보정
            state.AutoCycleIndex = ((state.AutoCycleIndex % count) + count) % count;
        }
    }
}
