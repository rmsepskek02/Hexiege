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

            // ─── Rule 3 + Rule 2: 자동 모드 해제 ───────────────────────────
            // 수동이 들어오면 자동 모드를 전체 해제해야 하지만,
            // 이미 골드가 차감된 자동 항목은 "수동으로 이관"하여 계속 생산(Rule 2).
            // IsCharged=false 자동 항목은 골드 미차감이므로 단순 제거(환불 불필요).
            if (state.IsAutoMode)
            {
                for (int i = state.PendingQueue.Count - 1; i >= 0; i--)
                {
                    QueueSlot s = state.PendingQueue[i];
                    if (!s.IsAuto) continue; // 수동 항목은 건드리지 않음

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

                // 자동 등록 타입 목록 및 순환 인덱스 초기화
                state.AutoTypes.Clear();
                state.AutoCycleIndex = 0;
                // CurrentProducing은 건드리지 않음 — 이미 진행 중인 생산은 완료까지 유지
            }

            // ─── Rule 4: 큐 상한 체크 ─────────────────────────────────────
            // CurrentProducing 1슬롯 + IsCharged=true 항목들이 곧 "보이는 큐".
            // 이 합이 MaxQueueSize(3)를 넘으면 수동 추가 거부.
            int slotsUsed = (state.CurrentProducing.HasValue ? 1 : 0) + state.ChargedPendingCount();
            if (slotsUsed + 1 > ProductionState.MaxQueueSize) return false;

            // ─── 골드/인구 검증 + 즉시 차감 ───────────────────────────────
            // 수동 항목은 Rule 5에 따라 등록 시 즉시 차감(항상 IsCharged=true).
            int cost = UnitProductionStats.GetGoldCost(type);
            if (!_resource.CanAfford(state.Team, cost)) return false;

            int popCost = UnitProductionStats.GetPopulationCost(type);
            if (!_population.HasPopulation(state.Team, popCost)) return false;

            _resource.SpendGold(state.Team, cost);

            // ─── PendingQueue 맨 뒤에 추가 ────────────────────────────────
            // PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2 불변식 유지.
            // CurrentProducing=null이면 다음 Tick의 TryStartNext가 이 항목을 슬롯0으로 올린다.
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

            // AutoTypes 내 등록 여부 확인
            int typeIndex = state.AutoTypes.IndexOf(type);

            if (typeIndex >= 0)
            {
                // ──────────────────────────────────────────────
                // 등록된 타입 → 제거 (자동 해제)
                // ──────────────────────────────────────────────
                state.AutoTypes.RemoveAt(typeIndex);

                // PendingQueue에서 해당 타입의 자동 항목 처리
                // 뒤에서 앞으로 순회 — 제거 중 인덱스 밀림 방지
                for (int i = state.PendingQueue.Count - 1; i >= 0; i--)
                {
                    QueueSlot s = state.PendingQueue[i];
                    if (!s.IsAuto || s.Type != type) continue;

                    if (s.IsCharged)
                    {
                        // Rule 2 적용: 슬롯에 표시된 항목은 수동으로 이관 (생산 계속, 환불 없음)
                        s.IsAuto = false;
                        state.PendingQueue[i] = s;
                    }
                    else
                    {
                        // 미차감 자동 대기 항목 → 그냥 제거 (환불 없음)
                        state.PendingQueue.RemoveAt(i);
                    }
                }

                // AutoCycleIndex가 AutoTypes 범위를 벗어나지 않도록 보정
                NormalizeAutoCycleIndex(state);

                GameEvents.OnProductionQueueChanged.OnNext(
                    new ProductionQueueChangedEvent(barracksId));
                return true;
            }

            // ──────────────────────────────────────────────
            // 미등록 타입 → 추가 (자동 등록)
            // ──────────────────────────────────────────────

            // 자동 등록 상한(3종류)
            if (state.AutoTypes.Count >= 3) return false;

            // ─── Rule 20 확장: 슬롯0(CurrentProducing)이 같은 타입 수동 항목이면 수동→자동 전환 ───
            // 슬롯0에서 수동으로 A를 생산 중일 때 A를 자동등록하면,
            // 슬롯1에 A를 새로 추가하지 않고 슬롯0 자체를 "자동"으로 전환한다.
            // → 완료 시 CompleteProduction의 wasAuto=true 조건을 만족하여 자동 순환이 시작됨.
            // → 골드 이중 차감 없음 (슬롯0의 골드는 이미 수동 등록 시 차감됨).
            //
            // 예시: 슬롯0에 [Pistoleer(수동, 생산 중)]만 있는 상태에서 Pistoleer 자동등록 버튼을 누르면,
            //       슬롯1에 새 Pistoleer를 추가하지 않고 슬롯0의 IsAuto만 true로 바꾼다.
            //       기존 BUG-15 방어(CurrentIsAuto=true 케이스)와는 상호 배타이므로 충돌 없음.
            if (state.CurrentProducing.HasValue &&
                state.CurrentProducing.Value == type &&
                !state.CurrentIsAuto)
            {
                state.CurrentIsAuto = true;
                state.AutoTypes.Add(type);
                // 새 타입 추가 후 AutoCycleIndex가 범위를 벗어나지 않도록 보정
                NormalizeAutoCycleIndex(state);

                GameEvents.OnProductionQueueChanged.OnNext(
                    new ProductionQueueChangedEvent(barracksId));
                return true;
            }

            // ─── Rule 2-1: PendingQueue 마지막 수동 항목과 같은 타입이면 이관 처리 ─────
            // 사용자가 수동으로 [3,2,1] 등록 후 마지막 '1'에 대해 자동을 활성화하면
            // "마지막 수동을 자동으로 전환" 의도로 해석하여 중복 항목 추가를 방지.
            // 골드는 수동 등록 시 이미 차감되었으므로 추가 차감 없이 IsAuto=true로 바꿈.
            if (state.PendingQueue.Count > 0)
            {
                int lastIdx = state.PendingQueue.Count - 1;
                QueueSlot last = state.PendingQueue[lastIdx];

                if (!last.IsAuto && last.Type == type)
                {
                    last.IsAuto = true;
                    // IsCharged는 그대로 true 유지(이미 차감됨)
                    state.PendingQueue[lastIdx] = last;

                    state.AutoTypes.Add(type);
                    // 새 타입 추가 후에도 AutoCycleIndex가 범위를 벗어나지 않도록 보정
                    NormalizeAutoCycleIndex(state);

                    GameEvents.OnProductionQueueChanged.OnNext(
                        new ProductionQueueChangedEvent(barracksId));
                    return true;
                }
            }

            // ─── Rule 5: 슬롯1/슬롯2에 즉시 표시 가능한지 판정 ─────────────
            // 표시 가능 = CurrentProducing이 있고, PendingQueue의 IsCharged=true 항목이 2개 미만.
            // (슬롯1, 슬롯2 두 칸 중 최소 하나가 비어야 즉시 표시 가능)
            bool canShow = state.CurrentProducing.HasValue && state.ChargedPendingCount() < 2;

            // BUG-15 방지: 현재 자동으로 같은 타입을 생산 중이면 이중 차감 위험 → 미차감 상태로 보류.
            // 다음 순환 시 TryStartNext에서 정상 차감되므로 게임 로직에는 문제 없음.
            if (canShow && state.CurrentIsAuto && state.CurrentProducing == type)
                canShow = false;

            bool isCharged = false;
            if (canShow)
            {
                // 슬롯에 즉시 표시될 수 있으므로 골드/인구 검증 + 차감
                int cost = UnitProductionStats.GetGoldCost(type);
                if (!_resource.CanAfford(state.Team, cost)) return false;

                int popCost = UnitProductionStats.GetPopulationCost(type);
                if (!_population.HasPopulation(state.Team, popCost)) return false;

                _resource.SpendGold(state.Team, cost);
                isCharged = true;
            }
            // else: 큐가 이미 꽉 차 있음 또는 특수 조건 → 차감 없이 대기 큐로 보냄.
            //       TryStartNext나 ChargeVisibleSlots가 슬롯에 올라올 때 차감 처리.

            state.PendingQueue.Add(new QueueSlot(type, true, isCharged));
            state.AutoTypes.Add(type);
            NormalizeAutoCycleIndex(state);

            // ─── 슬롯 깜빡임 버그 방지 ─────────────────────────────────────────────
            // 큐가 완전히 비어있었던 경우(= 현재 생산 중인 유닛이 없음)에는 방금 추가한
            // 아이템이 PendingQueue[0]에 들어가면서 "슬롯1(두 번째 칸)"에 잠시 표시된다.
            // 그 후 다음 Tick()에서 TryStartNext가 이를 슬롯0(첫 번째 칸)으로 올리기 때문에
            // 1프레임 동안 슬롯1 → 슬롯0으로 튀는 깜빡임이 시각적으로 드러난다.
            //
            // 해결: 큐가 비어있었으면 여기서 즉시 TryStartNext를 호출해 슬롯0으로 바로 올린다.
            //   - TryStartNext 내부에서 OnProductionQueueChanged 이벤트를 발행하므로
            //     아래의 중복 이벤트 발행을 피하기 위해 Early Return 처리한다.
            //   - TryStartNext는 골드/인구 검증을 포함하므로, 자원 부족 시 아이템은
            //     PendingQueue에 미차감 상태로 남아 대기한다(기존 동작 유지).
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
            {
                // ─── 슬롯0: 현재 생산 중인 유닛 취소 ───
                if (!state.CurrentProducing.HasValue) return false;

                UnitType cancelType = state.CurrentProducing.Value;

                // [BUG-FIX 2026-04-19] 자동 항목이었는지 여부를 "초기화 전"에 먼저 캡처.
                //   state.CurrentIsAuto = false 라인 이후에 읽으면 항상 false로 판정되어
                //   AutoTypes 제거 분기로 진입하지 못하는 문제 방지.
                bool wasAuto = state.CurrentIsAuto;

                // Rule 1: 현재 생산 중인 유닛은 항상 골드 차감된 상태 → 전액 환불
                _resource.AddGold(state.Team, UnitProductionStats.GetGoldCost(cancelType));

                // 생산 상태 초기화 (다음 Tick에서 TryStartNext가 자연스럽게 다음 항목 선택)
                state.CurrentProducing = null;
                state.CurrentIsAuto = false;
                state.ElapsedTime = 0f;
                state.RequiredTime = 0f;

                // [BUG-19 재검토] 이전 구조에서는 TryStartNext의 ManualQueue 우선 처리로 인해
                //   슬롯0 취소 직후 자동→수동 역전 버그가 있어 직접 다음 항목을 시작했었음.
                //   새 구조에서는 PendingQueue가 이미 올바른 표시 순서를 유지하므로
                //   TryStartNext에 위임해도 안전함 — 다음 Tick에서 PendingQueue[0]이 슬롯0으로 올라감.

                // [BUG-FIX 2026-04-19] 자동 항목이 취소된 경우, AutoTypes에서도 제거해야
                //   다음 Tick의 auto 사이클에서 같은 타입이 재생산되지 않음.
                //   잔여 PendingQueue 내 같은 타입 자동 항목도 Rule 2에 따라 처리됨.
                if (wasAuto)
                {
                    CancelAutoTypeIfNeeded(state, cancelType);
                }

                GameEvents.OnProductionQueueChanged.OnNext(
                    new ProductionQueueChangedEvent(barracksId));
                return true;
            }
            else if (slotIndex == 1)
            {
                // ─── 슬롯1: PendingQueue[0] 취소 ───
                if (state.PendingQueue.Count < 1) return false;

                QueueSlot cancelled = state.PendingQueue[0];

                // Rule 1: IsCharged=true면 환불, false면 환불 없음(미차감이므로)
                if (cancelled.IsCharged)
                {
                    _resource.AddGold(state.Team, UnitProductionStats.GetGoldCost(cancelled.Type));
                }

                state.PendingQueue.RemoveAt(0);

                // 슬롯1이 빠지면서 PendingQueue[1]이 새 슬롯1로, PendingQueue[2]가 새 슬롯2로 승격.
                // 새로 슬롯에 올라온 자동 항목이 IsCharged=false 상태라면 지금 차감해야 함.
                ChargeVisibleSlots(state);

                // [BUG-FIX 2026-04-19] 취소된 항목이 자동이었다면 AutoTypes에서도 제거.
                //   RemoveAt 이후에 호출해도 안전 — cancelled는 이미 값으로 복사된 상태.
                if (cancelled.IsAuto)
                {
                    CancelAutoTypeIfNeeded(state, cancelled.Type);
                }

                GameEvents.OnProductionQueueChanged.OnNext(
                    new ProductionQueueChangedEvent(barracksId));
                return true;
            }
            else if (slotIndex == 2)
            {
                // ─── 슬롯2: PendingQueue[1] 취소 ───
                if (state.PendingQueue.Count < 2) return false;

                QueueSlot cancelled = state.PendingQueue[1];

                // Rule 1
                if (cancelled.IsCharged)
                {
                    _resource.AddGold(state.Team, UnitProductionStats.GetGoldCost(cancelled.Type));
                }

                state.PendingQueue.RemoveAt(1);

                // 슬롯2 제거 시 PendingQueue[2]가 새 슬롯2로 승격 → 해당 항목이 자동 미차감이면 차감.
                ChargeVisibleSlots(state);

                // [BUG-FIX 2026-04-19] 취소된 항목이 자동이었다면 AutoTypes에서도 제거.
                if (cancelled.IsAuto)
                {
                    CancelAutoTypeIfNeeded(state, cancelled.Type);
                }

                GameEvents.OnProductionQueueChanged.OnNext(
                    new ProductionQueueChangedEvent(barracksId));
                return true;
            }

            return false;
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
                        // [2026-05-16] 자원 부족 시 자동 생산 즉시 취소.
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
                    // [2026-05-16] 자원 부족 → 자동 생산 전체 즉시 취소.
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

            // PendingQueue[0]/[1] 중 IsCharged=false 자동 항목이 새로 슬롯에 올라왔다면 지금 차감.
            ChargeVisibleSlots(state);

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
