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
//   자동: 롱프레스 → AutoEntries에 토글. AutoEntries 순환 반복.
//   수동 시작 시 자동 모드 해제 — 슬롯에 표시된 자동 항목은 ManualQueue로 이관.
//
// 전역 규칙 (5가지):
//   Rule 1: 생산이 취소되면 항상 전액 환불 — 예외 없음
//   Rule 2: 자동생산 취소 시 큐에 등록(슬롯에 표시)된 항목은 그대로 생산 — ManualQueue로 이관
//   Rule 3: 수동생산 시행 시 모든 자동생산 취소 — 인디케이터 OFF, AutoEntries 클리어
//   Rule 4: 생산큐 최대 3개 — CurrentProducing + ManualQueue 기준 (자동 대기 별도)
//   Rule 5: 비용 차감은 슬롯에 표시될 때 — AutoEntry.IsCharged로 차감 여부 추적
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
        ///
        /// Rule 3: 자동 모드 해제.
        /// Rule 2: 슬롯에 표시된(IsCharged=true) 자동 항목은 ManualQueue 앞에 이관.
        ///         큐 풀 대기(IsCharged=false) 항목은 골드 미차감이므로 소멸.
        /// Rule 4: CurrentProducing + ManualQueue 기준 최대 3개 (자동 대기 별도).
        ///         이관 후의 ManualQueue 크기 + CurrentProducing 기준으로 판정.
        /// </summary>
        public bool EnqueueUnit(int barracksId, UnitType type)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            // ── Rule 3 + Rule 2: 자동 모드 해제 + 슬롯 표시 항목 이관 ──
            // 수동 추가 전에 처리해야 ManualQueue 크기를 정확히 판단 가능
            if (state.IsAutoMode)
            {
                // Rule 2: 슬롯에 표시된(IsCharged=true) 자동 항목을 ManualQueue 앞에 이관
                // 슬롯 표시 순서 = AutoEntries에서 AutoIndex 기준 순환 순서
                // 이관 대상: IsCharged=true인 항목만 (골드 이미 차감됨)
                // 이관 순서: 슬롯1(다음) → 슬롯2(그 다음) 순서로 ManualQueue 앞에 삽입
                var chargedEntries = CollectChargedSlotEntries(state);
                int insertIndex = 0;
                foreach (var entry in chargedEntries)
                {
                    state.ManualQueue.Insert(insertIndex, entry.Type);
                    insertIndex++;
                }

                // Rule 3: 자동 모드 해제 + AutoEntries 클리어
                // IsCharged=false 항목은 골드 미차감이므로 환불 불필요, 단순 소멸
                state.IsAutoMode = false;
                state.AutoEntries.Clear();
                state.AutoIndex = 0;
                // CurrentProducing은 건드리지 않음 — 슬롯 0 생산은 완료까지 유지
                // (이미 골드가 차감된 생산이므로 취소 없이 완료 허용)
            }

            // Rule 4: 생산큐는 최대 3개까지 등록 가능
            // CurrentProducing + ManualQueue 기준 (이관된 항목 포함)
            int currentCount = (state.CurrentProducing.HasValue ? 1 : 0) + state.ManualQueue.Count;
            if (currentCount + 1 > ProductionState.MaxQueueSize) return false;

            // 골드 확인 + 즉시 차감
            int cost = UnitProductionStats.GetGoldCost(type);
            if (!_resource.CanAfford(state.Team, cost)) return false;

            // 인구 확인
            int popCost = UnitProductionStats.GetPopulationCost(type);
            if (!_population.HasPopulation(state.Team, popCost)) return false;

            _resource.SpendGold(state.Team, cost);

            // 수동 항목을 큐 맨 뒤에 추가
            state.ManualQueue.Add(type);
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// 자동 생산: 유닛 타입 토글 (ON/OFF).
        /// 이미 등록되어 있으면 해제, 없으면 추가 (최대 3종류).
        ///
        /// Rule 5: 슬롯에 표시 가능하면 등록 시 즉시 골드 차감 (IsCharged=true).
        ///         큐 풀 상태이면 골드 미차감 (IsCharged=false).
        ///
        /// 슬롯 구조:
        ///   슬롯 0 = 현재 생산 중인 유닛 (CurrentProducing)
        ///   슬롯 1~2 = AutoEntries에서 다음 생산 예정
        ///
        /// 슬롯에 즉시 표시 가능 조건:
        ///   CurrentProducing이 있고, 현재 슬롯에 표시된 자동 항목 수(IsCharged 기준) + ManualQueue.Count &lt; 2
        ///   (슬롯1~2 중 빈 자리가 있는 경우)
        ///
        /// 제거 시 규칙:
        ///   - IsCharged=true인 항목 제거: 골드 환불 (Rule 1)
        ///   - IsCharged=false인 항목 제거: 환불 없음
        ///   - 슬롯 0에 해당하는 타입 제거: AutoEntries에서만 제거, 생산은 계속, 환불 없음
        /// </summary>
        public bool ToggleAutoProduction(int barracksId, UnitType type)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            int idx = state.AutoIndexOf(type);
            if (idx >= 0)
            {
                // ── 이미 등록된 타입 → 제거 (취소) ──
                // 버튼 탭 취소는 자동 순환 목록에서 제거하는 것이지 생산 취소가 아님.
                // 현재 생산 중인 유닛(슬롯0)은 계속 유지됨 → Rule 1 환불 조건 미해당.
                // 슬롯 직접 클릭 취소(CancelQueueAt)만 환불 대상.

                // ── Rule 2: 슬롯에 표시된(IsCharged=true) 항목은 ManualQueue로 이관 ──
                // 자동 모드만 취소하고, 이미 골드 차감되어 슬롯에 보이는 항목은 수동 큐로 넘겨서
                // 생산이 계속 진행되도록 한다.
                //
                // 판단 기준:
                //   - removedEntry.Type == CurrentProducing → 슬롯0(현재 생산 중)
                //     → 이미 생산 중이므로 이관 불필요 (생산은 CurrentProducing으로 계속됨)
                //   - 슬롯0이 아님 && IsCharged=true → 슬롯1~2에 표시 중
                //     → ManualQueue로 이관하여 생산 유지 (Rule 2)
                //   - IsCharged=false → 큐 풀 대기 상태, 골드 미차감
                //     → 이관 불필요, 단순 제거
                AutoEntry removedEntry = state.AutoEntries[idx];
                // isSlot0 판단: AutoIndex 위치가 아닌 CurrentProducing 타입 비교로 수행.
                // 이유: "취소 상태"(선행 AutoEntry 제거 후)에서 AutoIndex가 밀려
                //   idx==AutoIndex가 true여도 실제 슬롯0(현재 생산 중)이 아닐 수 있음.
                //   예) Assault 제거 후 AutoIndex=0이 Sniper를 가리키지만,
                //       CurrentProducing=Assault이므로 Sniper는 슬롯1이어야 함.
                bool isSlot0 = state.CurrentProducing.HasValue
                               && removedEntry.Type == state.CurrentProducing.Value;

                if (!isSlot0 && removedEntry.IsCharged)
                {
                    // Rule 2 적용: 슬롯1~2에 표시된 항목 → ManualQueue 맨 뒤에 추가
                    // 골드는 이미 차감되었으므로 환불하지 않고 수동 큐로 이관하여 생산 계속
                    state.ManualQueue.Add(removedEntry.Type);
                }

                // AutoEntries에서 제거 (자동 순환 목록에서만 빠짐)
                state.AutoEntries.RemoveAt(idx);

                // AutoEntries가 비면 자동 모드 해제
                // CurrentProducing은 건드리지 않음 — 슬롯 0 생산은 항상 완료까지 허용
                if (state.AutoEntries.Count == 0)
                {
                    state.IsAutoMode = false;
                    state.AutoIndex = 0;
                }
                else
                {
                    // AutoIndex 보정: 제거된 인덱스가 AutoIndex보다 앞이면 1 감소
                    if (idx < state.AutoIndex)
                        state.AutoIndex -= 1;

                    // 범위 초과 방지
                    if (state.AutoIndex >= state.AutoEntries.Count)
                        state.AutoIndex = 0;
                }
            }
            else
            {
                // ── 미등록 타입 → 추가 ──

                // AutoEntries 최대 3개 제한 (자동 등록 상한)
                if (state.AutoEntries.Count >= 3) return false;

                // Rule 5: 슬롯에 즉시 표시 가능한지 판단하여 골드 차감 여부 결정
                // 슬롯 표시 가능 조건: 이 항목이 슬롯1 또는 슬롯2에 들어갈 수 있는가?
                // CurrentProducing이 없으면 이 항목이 바로 슬롯0(생산 시작)으로 갈 수 있음 → TryStartNext에서 차감
                // CurrentProducing이 있으면 슬롯1~2에 표시될 수 있음
                bool canShowInSlot = CanAutoEntryShowInSlot(state);

                // BUG-15 수정: 현재 슬롯0에서 같은 타입을 생산 중이면 즉시 골드 차감하지 않음.
                // TryStartNext에서 이미 해당 타입의 골드를 차감했으므로,
                // 여기서 또 차감하면 등록→취소→재등록 사이클에서 골드가 중복 차감된다.
                // IsCharged=false로 추가하면 자동 인디케이터 표시에는 문제 없고,
                // 다음 순환 시 TryStartNext에서 정상적으로 골드가 차감된다.
                if (canShowInSlot && type == state.CurrentProducing)
                    canShowInSlot = false;

                bool isCharged = false;
                if (canShowInSlot)
                {
                    // 슬롯에 표시 가능 → 즉시 골드/인구 검증 + 차감
                    int cost = UnitProductionStats.GetGoldCost(type);
                    if (!_resource.CanAfford(state.Team, cost)) return false;

                    int popCost = UnitProductionStats.GetPopulationCost(type);
                    if (!_population.HasPopulation(state.Team, popCost)) return false;

                    _resource.SpendGold(state.Team, cost);
                    isCharged = true;
                }
                // else: 큐 풀 대기 또는 CurrentProducing=null → 골드 미차감, TryStartNext에서 처리

                state.AutoEntries.Add(new AutoEntry(type, isCharged));
                state.IsAutoMode = true;
            }

            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
            return true;
        }

        /// <summary>
        /// 생산 큐에서 슬롯 취소.
        /// slotIndex 0 = 현재 생산 중 (골드 환불), 1~2 = 대기 큐.
        ///
        /// 자동 모드일 때의 슬롯 구조:
        ///   슬롯 0 = CurrentProducing (현재 생산 중, 골드 이미 차감 → 환불)
        ///   슬롯 1~2 = AutoEntries의 다음 생산 예정 항목
        ///     - IsCharged=true(슬롯 표시됨) → 골드 환불 (Rule 1)
        ///     - IsCharged=false(큐 풀 대기) → 환불 없음 (일반적으로 이 경우는 발생하지 않음)
        /// </summary>
        public bool CancelQueueAt(int barracksId, int slotIndex)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return false;

            // ── 자동 모드 분기 ──
            if (state.IsAutoMode)
            {
                if (slotIndex == 0)
                {
                    // 슬롯 0: 현재 생산 중인 유닛 즉시 취소 + 골드 환불 + AutoEntries에서 제거
                    if (!state.CurrentProducing.HasValue) return false;

                    UnitType cancelType = state.CurrentProducing.Value;

                    // AutoEntries에서 현재 생산 중인 타입 제거 (AutoIndex 위치)
                    if (state.AutoEntries.Count > 0 && state.AutoIndex < state.AutoEntries.Count
                        && state.AutoEntries[state.AutoIndex].Type == cancelType)
                    {
                        state.AutoEntries.RemoveAt(state.AutoIndex);
                    }

                    // AutoIndex 보정
                    if (state.AutoEntries.Count == 0)
                    {
                        state.IsAutoMode = false;
                        state.AutoIndex = 0;
                    }
                    else if (state.AutoIndex >= state.AutoEntries.Count)
                    {
                        state.AutoIndex = 0;
                    }

                    // 생산 취소 + 골드 환불 (Rule 1: 생산 취소 시 항상 전액 환불)
                    state.CurrentProducing = null;
                    state.ElapsedTime = 0f;
                    state.RequiredTime = 0f;
                    _resource.AddGold(state.Team, UnitProductionStats.GetGoldCost(cancelType));

                    // ── BUG-19 Bug B 수정: 자동 모드에서 슬롯0 취소 후, 다음 자동 항목 즉시 시작 ──
                    //
                    // [문제]
                    // TryStartNext()는 ManualQueue를 AutoEntries보다 항상 우선 처리한다.
                    // Rule 2로 ManualQueue에 이관된 항목이 있는 상태에서 슬롯0을 취소하면,
                    // TryStartNext()가 ManualQueue 항목을 먼저 꺼내 슬롯0에 올려서
                    // 기대하는 "큐 시프트(슬롯1→슬롯0)" 동작 대신 큐 순서가 역전된다.
                    //
                    // [해결]
                    // 슬롯0 취소 직후, TryStartNext()에 위임하지 않고
                    // AutoEntries[AutoIndex]를 직접 시작하여 ManualQueue 우선 처리를 우회한다.
                    // ManualQueue 항목은 이 자동 항목이 자연 완료된 후 TryStartNext()에서 정상 처리된다.
                    //
                    // [예시] AutoEntries=[Sniper], ManualQueue=[Pistoleer]:
                    //   수정 전: TryStartNext → Pistoleer 시작 → 슬롯0=Pistoleer, 슬롯1=Sniper (역전)
                    //   수정 후: Sniper 직접 시작 → 슬롯0=Sniper, 슬롯1=Pistoleer (정상)
                    if (state.IsAutoMode && state.AutoEntries.Count > 0)
                    {
                        AutoEntry nextAuto = state.AutoEntries[state.AutoIndex];

                        // IsCharged=false인 경우: 큐 풀 대기였던 항목이므로 지금 골드/인구 검증 후 차감
                        if (!nextAuto.IsCharged)
                        {
                            int autoCost = UnitProductionStats.GetGoldCost(nextAuto.Type);
                            int autoPopCost = UnitProductionStats.GetPopulationCost(nextAuto.Type);
                            bool canStart = _resource.CanAfford(state.Team, autoCost)
                                            && _population.HasPopulation(state.Team, autoPopCost);
                            if (canStart)
                            {
                                _resource.SpendGold(state.Team, autoCost);
                                state.AutoEntries[state.AutoIndex] = new AutoEntry(nextAuto.Type, true);
                                nextAuto = state.AutoEntries[state.AutoIndex];
                            }
                            // 자원 부족 시: CurrentProducing=null 유지 → 다음 Tick에서 TryStartNext()가 처리
                            // (이 경우 ManualQueue가 먼저 선택될 수 있으나, 자원 부족 예외 케이스이므로 허용)
                        }

                        if (nextAuto.IsCharged)
                        {
                            // 다음 자동 항목을 직접 시작 (TryStartNext를 거치지 않으므로 ManualQueue 우선 우회)
                            state.CurrentProducing = nextAuto.Type;
                            state.ElapsedTime = 0f;
                            state.RequiredTime = UnitProductionStats.GetProductionTime(nextAuto.Type);

                            GameEvents.OnProductionStarted.OnNext(
                                new ProductionStartedEvent(state.BarracksId, nextAuto.Type));
                        }
                    }
                }
                else
                {
                    // 슬롯 1~2: UpdateQueueSlots의 pending 목록 방식과 동일한 기준으로 취소 대상 결정.
                    //
                    // BUG-18 수정 이후 UpdateQueueSlots는 다음 순서로 슬롯1~2를 채움:
                    //   1) AutoEntries 대기 항목 (AutoIndex 기준으로 현재 이후의 항목)
                    //   2) ManualQueue 항목 (AutoEntries 대기 항목 이후에 배치)
                    //
                    // CancelQueueAt도 동일한 pending 논리를 사용해야 슬롯에 표시된 것과 취소 대상이 일치함.
                    int autoCount = state.AutoEntries.Count;
                    int pendingOffset = slotIndex - 1; // 슬롯1→0, 슬롯2→1

                    // UpdateQueueSlots와 동일한 isNormalAutoState 판단
                    // CurrentProducing=null: 아직 TryStartNext 전 → 정상 상태로 간주
                    bool isNormalAutoState = autoCount > 0 && (
                        !state.CurrentProducing.HasValue
                        || state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value
                    );

                    // AutoEntries 대기 항목 수:
                    //   정상 상태: AutoIndex 위치는 슬롯0 → 그 이후(+1~) 항목이 대기 → 최대 count-1개 (슬롯1~2 기준 최대 2)
                    //   취소 상태: AutoIndex 위치부터 대기 → 최대 count개 (슬롯1~2 기준 최대 2)
                    int autoStartOffset = isNormalAutoState ? 1 : 0;
                    int autoPendingCount = isNormalAutoState
                        ? System.Math.Min(autoCount - 1, 2)
                        : System.Math.Min(autoCount, 2);

                    if (pendingOffset < autoPendingCount)
                    {
                        // pending 위치가 AutoEntries 범위 내 → AutoEntry 취소
                        int targetIdx = (state.AutoIndex + autoStartOffset + pendingOffset) % autoCount;
                        AutoEntry removedEntry = state.AutoEntries[targetIdx];

                        // Rule 1: IsCharged=true(골드 차감됨)이면 환불
                        if (removedEntry.IsCharged)
                        {
                            int refund = UnitProductionStats.GetGoldCost(removedEntry.Type);
                            _resource.AddGold(state.Team, refund);
                        }

                        state.AutoEntries.RemoveAt(targetIdx);

                        // AutoIndex 보정
                        if (state.AutoEntries.Count == 0)
                        {
                            // AutoEntries가 비면 자동 모드 해제
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
                            if (state.AutoIndex >= state.AutoEntries.Count)
                                state.AutoIndex = 0;
                        }
                    }
                    else
                    {
                        // pending 위치가 AutoEntries 범위를 초과 → ManualQueue 항목 취소
                        int manualOffset = pendingOffset - autoPendingCount;
                        if (manualOffset < 0 || manualOffset >= state.ManualQueue.Count) return false;

                        UnitType cancelType = state.ManualQueue[manualOffset];

                        // Rule 1: ManualQueue 항목은 등록 시 골드가 차감됨 → 전액 환불
                        int refund = UnitProductionStats.GetGoldCost(cancelType);
                        _resource.AddGold(state.Team, refund);

                        state.ManualQueue.RemoveAt(manualOffset);
                        // IsAutoMode, AutoEntries, AutoIndex는 변경 없음 — 자동 모드는 계속 유지
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

            // 골드 100% 환불 (Rule 1)
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
        /// 자동 모드:
        ///   - IsCharged=true(이미 차감됨): 즉시 시작, 중복 차감 없음.
        ///   - IsCharged=false(큐 풀 대기): 이 시점에 골드/인구 검증 + 차감.
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
            else if (state.IsAutoMode && state.AutoEntries.Count > 0)
            {
                nextType = state.AutoEntries[state.AutoIndex].Type;
            }

            if (!nextType.HasValue) return;

            UnitType type = nextType.Value;

            // 자동 모드일 때 골드 차감 분기
            if (!isManual)
            {
                AutoEntry entry = state.AutoEntries[state.AutoIndex];

                if (!entry.IsCharged)
                {
                    // IsCharged=false: 큐 풀 대기였던 항목 → 이 시점에 골드/인구 검증 + 차감
                    int cost = UnitProductionStats.GetGoldCost(type);
                    if (!_resource.CanAfford(state.Team, cost)) return;

                    int popCost = UnitProductionStats.GetPopulationCost(type);
                    if (!_population.HasPopulation(state.Team, popCost)) return;

                    _resource.SpendGold(state.Team, cost);

                    // 차감 완료 → IsCharged=true로 갱신
                    state.AutoEntries[state.AutoIndex] = new AutoEntry(type, true);
                }
                // IsCharged=true: 이미 등록 시점에 차감됨 → 중복 차감 없음
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
        /// 자동 모드: 완료 후 다음 대기 항목에 골드 차감 시도 (슬롯 표시 위해).
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

            // 자동 모드: 다음 타입으로 인덱스 순환 + 다음 항목 사전 골드 차감
            if (state.IsAutoMode && state.AutoEntries.Count > 0)
            {
                // 완료된 type이 AutoEntries에 있을 때만 AutoIndex 증가
                // (ToggleAutoProduction으로 취소된 타입은 AutoEntries에 없으므로 AutoIndex 유지)
                if (state.AutoContains(type))
                {
                    // 생산 완료된 항목의 IsCharged 리셋 — 다음 순환 시 골드를 다시 차감하기 위해
                    // IsCharged=true가 그대로 남으면 TryStartNext/TryPreChargeAutoEntries에서
                    // 이미 차감된 것으로 판단하여 다음 순환부터 골드 소모가 발생하지 않음
                    var completedEntry = state.AutoEntries[state.AutoIndex];
                    state.AutoEntries[state.AutoIndex] = new AutoEntry(completedEntry.Type, false);

                    state.AutoIndex = (state.AutoIndex + 1) % state.AutoEntries.Count;
                }

                // 다음 자동 항목이 슬롯에 표시될 수 있으므로 사전 골드 차감 시도
                TryPreChargeAutoEntries(state);
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

        // ====================================================================
        // 자동 생산 골드 차감 헬퍼
        // ====================================================================

        /// <summary>
        /// 새 자동 항목이 슬롯에 즉시 표시 가능한지 판단.
        /// 슬롯 표시 가능 조건:
        ///   - CurrentProducing이 있어야 함 (없으면 TryStartNext에서 바로 슬롯0으로 감)
        ///   - 현재 슬롯1~2에 표시된 항목 수(ManualQueue + IsCharged=true 자동 항목)가 2 미만
        ///
        /// 즉, 큐 풀 상태(CurrentProducing + 슬롯1~2 모두 차 있음)이면 false.
        /// </summary>
        private bool CanAutoEntryShowInSlot(ProductionState state)
        {
            // CurrentProducing이 없으면 TryStartNext에서 바로 생산 시작 → 슬롯 표시 단계 없음
            if (!state.CurrentProducing.HasValue) return false;

            // 슬롯1~2에 이미 표시된 항목 수 계산
            // ManualQueue는 항상 슬롯1~2 우선, 나머지를 IsCharged=true 자동 항목이 채움
            // AutoEntries[AutoIndex]는 슬롯0(현재 생산 중)에 해당하므로 슬롯1~2 집계에서 제외
            int shownCount = state.ManualQueue.Count;
            for (int i = 0; i < state.AutoEntries.Count; i++)
            {
                if (i == state.AutoIndex) continue; // 슬롯0 항목은 슬롯1~2 집계 대상이 아님
                if (state.AutoEntries[i].IsCharged)
                    shownCount++;
            }

            // 슬롯1~2는 최대 2개 (MaxQueueSize - 1 = 2)
            return shownCount < (ProductionState.MaxQueueSize - 1);
        }

        /// <summary>
        /// 생산 완료 후, 다음 슬롯에 표시될 자동 항목에 대해 사전 골드 차감.
        /// IsCharged=false인 항목 중 슬롯에 표시 가능한 것만 차감.
        /// CurrentProducing이 null인 시점(생산 완료 직후)에 호출되므로,
        /// 다음 TryStartNext에서 슬롯0에 올라갈 항목(AutoIndex)은 건너뛰고
        /// 그 다음 항목(슬롯1~2에 표시될 것)만 사전 차감.
        /// </summary>
        private void TryPreChargeAutoEntries(ProductionState state)
        {
            if (!state.IsAutoMode || state.AutoEntries.Count == 0) return;

            // 다음 TryStartNext에서 슬롯0에 올라갈 항목 = AutoEntries[AutoIndex]
            // 그 항목은 TryStartNext에서 차감되므로 여기서는 건너뜀
            // 슬롯1~2에 표시될 항목만 사전 차감 (AutoIndex+1, AutoIndex+2)
            int count = state.AutoEntries.Count;
            int slotsAvailable = ProductionState.MaxQueueSize - 1; // 2개 (슬롯1~2)
            int charged = 0;

            for (int offset = 1; offset <= slotsAvailable && offset < count; offset++)
            {
                int idx = (state.AutoIndex + offset) % count;
                AutoEntry entry = state.AutoEntries[idx];

                if (entry.IsCharged)
                {
                    // 이미 차감됨 → 카운트만 증가
                    charged++;
                    continue;
                }

                // 미차감 항목 → 골드/인구 검증 + 차감 시도
                int cost = UnitProductionStats.GetGoldCost(entry.Type);
                if (!_resource.CanAfford(state.Team, cost)) break;

                int popCost = UnitProductionStats.GetPopulationCost(entry.Type);
                if (!_population.HasPopulation(state.Team, popCost)) break;

                _resource.SpendGold(state.Team, cost);
                state.AutoEntries[idx] = new AutoEntry(entry.Type, true);
                charged++;
            }
        }

        /// <summary>
        /// 수동 생산(EnqueueUnit) 시 호출.
        /// 자동 모드에서 슬롯1~2에 표시된(IsCharged=true) 항목을 수집하여 반환.
        /// 수집 순서: AutoIndex 기준 순환 순서 (슬롯1 → 슬롯2).
        /// CurrentProducing에 해당하는 AutoIndex 위치의 항목은 제외 (슬롯0 = 생산 중).
        /// ManualQueue로 이관될 항목 목록을 만듦.
        /// </summary>
        private List<AutoEntry> CollectChargedSlotEntries(ProductionState state)
        {
            var result = new List<AutoEntry>();
            int count = state.AutoEntries.Count;
            if (count == 0) return result;

            // 슬롯0(=CurrentProducing)에 해당하는 AutoIndex 항목은 건너뜀
            // 슬롯1 = AutoIndex+1, 슬롯2 = AutoIndex+2 순서로 순회
            // IsCharged=true인 것만 이관 대상
            for (int offset = 1; offset < count && offset <= 2; offset++)
            {
                int idx = (state.AutoIndex + offset) % count;
                AutoEntry entry = state.AutoEntries[idx];

                if (entry.IsCharged)
                {
                    result.Add(entry);
                }
            }

            return result;
        }
    }
}
