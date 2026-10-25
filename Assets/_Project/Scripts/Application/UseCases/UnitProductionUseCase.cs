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
    public partial class UnitProductionUseCase
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
        /// 이 좌표에 랠리포인트(집결 깃발)를 꽂을 수 있는지 판정한다.
        ///
        /// 꽂을 수 없는 곳(false):
        ///   - 격자(맵)에 그 좌표의 타일이 아예 없다 → 맵 바깥을 누른 경우.
        ///   - 타일은 있지만 지형이 「막힌 타일」이다 → 화면에 타일이 그려지지 않은 빈 곳을 누른 경우.
        /// 꽂을 수 있는 곳(true):
        ///   - 그 밖의 모든 타일. 건물·금광이 있는 칸, 건설 불가 지형 칸도 꽂을 수 있다.
        ///     (건물·금광 칸에 꽂으면 유닛은 그 칸 대신 근처의 걸어갈 수 있는 칸으로 간다 —
        ///      그 처리는 ProductionTicker의 랠리 경로 결정 메서드가 맡는다.)
        ///
        /// 왜 지형만 보나요?
        ///   - 지형 종류는 맵이 만들어질 때 정해지고 경기 중에는 바뀌지 않는다.
        ///     그래서 멀티플레이의 Host와 Client가 같은 좌표에 대해 항상 같은 답을 낸다.
        ///     (유닛·건물처럼 네트워크 동기화가 늦게 도착할 수 있는 상태는 일부러 보지 않는다.)
        ///
        /// 누가 부르나요?
        ///   - 입력 쪽: 생산 패널(ProductionPanelUI)이 조준 중 탭한 칸을 미리 걸러 토스트를 띄울 때.
        ///   - 서버 쪽: 네트워크 생산 컨트롤러의 랠리 설정 RPC가 받은 좌표를 다시 검사할 때.
        ///   - 이 클래스 안: SetRallyPoint가 마지막 방어선으로 한 번 더 검사할 때.
        ///   세 곳이 모두 이 메서드 하나를 쓰므로 기준이 갈라지지 않는다.
        ///
        /// ⚠️ 같은 기준이 두 곳에 있다:
        ///   건물 배치 패널의 타일 클릭 판정(GridInteractionUseCase의 지형별 선택 메서드에서
        ///   「선택 불가」로 돌려주는 조건)과 같은 기준이다. 한쪽 기준을 바꾸면 다른 쪽도 함께 맞춘다.
        ///   (근거: GameSystemRules_UI.md 「건물 배치 패널 UI」 규칙 6 — 막힌 타일·빈 공간은 선택 불가)
        /// </summary>
        /// <param name="coord">랠리포인트를 꽂으려는 도메인 헥스 좌표</param>
        /// <returns>꽂을 수 있으면 true, 맵 밖이거나 막힌 타일이면 false</returns>
        public bool CanSetRallyPointAt(HexCoord coord)
        {
            // 격자가 주입되지 않았다면 판정할 근거가 없으므로 안전하게 「불가」로 본다.
            if (_grid == null) return false;

            // 격자에 없는 좌표 = 맵 바깥.
            HexTile tile = _grid.GetTile(coord);
            if (tile == null) return false;

            // 막힌 지형 = 화면에 타일이 없는 빈 곳.
            if (tile.TileKind == TileKind.Blocked) return false;

            return true;
        }

        /// <summary>
        /// 랠리 포인트 설정.
        /// 배럭 자신의 타일에 설정하면 랠리포인트 해제 (해제 방법).
        /// 꽂을 수 없는 좌표(맵 밖·막힌 타일 — CanSetRallyPointAt 참조)는 아무 일도 하지 않고 무시한다.
        /// </summary>
        public void SetRallyPoint(int barracksId, HexCoord target)
        {
            if (!_states.TryGetValue(barracksId, out var state)) return;

            // 마지막 방어선 — 꽂을 수 없는 좌표면 조용히 무시한다.
            //   이 메서드를 부르는 곳은 네 군데다(생산 패널 입력, 서버 RPC, AI의 금광 랠리, 업그레이드 후 랠리 복원).
            //   여기서 한 번 막으면 네 경로가 모두 같은 규칙을 따르게 된다.
            //   - 입력 쪽은 이미 미리 걸러 토스트를 띄웠고, 서버 RPC는 거부 로그를 따로 남긴다.
            //     그래서 여기서는 로그·토스트 없이 그냥 돌아간다.
            //   - AI가 노리는 금광 칸은 막힌 지형이 아니므로 통과한다(판정은 지형만 보고 금광·건물은 보지 않는다).
            //   - 업그레이드 복원 좌표는 예전에 이미 통과한 좌표이고 지형은 경기 중 바뀌지 않으므로 다시 통과한다.
            if (!CanSetRallyPointAt(target)) return;

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

    }
}
