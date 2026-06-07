// ============================================================================
// NetworkProductionController.cs
// 네트워크 모드에서 유닛 생산 큐 요청을 서버로 중계하고 생산 완료 시 전체 클라이언트에 동기화.
//
// 흐름:
//   1. 클라이언트 UI → RequestEnqueueServerRpc(barracksId, unitType, teamIndex)
//   2. 서버: 팀 소유권·골드·인구·배럭 존재 검증
//      → UnitProductionUseCase.EnqueueUnit() 실행 (골드 즉시 차감)
//   3. 서버: GameEvents.OnUnitProduced 구독
//      → 유닛 생산 완료 시 SpawnUnitClientRpc로 전파
//   4. 모든 클라이언트: SpawnUnitWithId()로 동일 Id의 UnitData 재생성
//      + GameEvents.OnUnitProduced 발행 → ProductionTicker가 랠리포인트 이동 처리
//
// 생산 Tick 처리:
//   ProductionTicker.Update()에서 서버 전용 분기를 통해
//   클라이언트는 Tick을 실행하지 않고 서버만 실행.
//
// 배치:
//   씬에 빈 GameObject "NetworkProductionController" 생성.
//   NetworkObject 컴포넌트 + 이 스크립트를 부착.
//   NetworkManager의 씬 오브젝트로 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 유닛 생산 네트워크 컨트롤러.
    /// 클라이언트의 생산 큐 요청을 서버에서 검증·실행하고,
    /// 생산 완료 시 모든 클라이언트에 유닛 스폰을 동기화.
    /// </summary>
    public class NetworkProductionController : NetworkBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> GameBootstrapper 참조. UseCase 접근에 사용. </summary>
        private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

        /// <summary> OnUnitProduced 구독 해제용 Disposable. </summary>
        private System.IDisposable _unitProducedSubscription;

        /// <summary> OnProductionStarted 구독 해제용 Disposable. </summary>
        private System.IDisposable _productionStartedSubscription;

        /// <summary> OnProductionQueueChanged 구독 해제용 Disposable (큐 취소 동기화). </summary>
        private System.IDisposable _queueChangedSubscription;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameBootstrapper를 탐색하고,
        /// 서버라면 OnUnitProduced 이벤트를 구독하여 클라이언트에 전파 준비.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
            if (_bootstrapper == null)
            {
                Debug.LogWarning("[Network] NetworkProductionController: GameBootstrapper를 찾을 수 없습니다.");
            }

            Debug.Log($"[Network] NetworkProductionController 스폰. IsServer={IsServer}");

            // 서버만 생산 완료 이벤트를 구독하여 클라이언트에 동기화
            if (IsServer)
            {
                SubscribeToProductionEvents();
            }
        }

        // ====================================================================
        // 외부 호출용 래퍼 메서드 — UI 등 Presentation 레이어에서 사용
        // ====================================================================
        //
        // 왜 래퍼 메서드인가?
        //   ProductionPanelUI 등 UI(Presentation)가 *ServerRpc 메서드를 직접 호출하면
        //   NGO API에 결합된다. 래퍼는 UI에게 일반 메서드 호출 형태를 제공하여
        //   Presentation의 Unity.Netcode 직접 의존을 제거한다.
        //
        //   각 래퍼는 enum 타입을 그대로 받아 내부에서 int 캐스트하여 ServerRpc로 전달한다.
        //
        // ====================================================================

        /// <summary>
        /// 유닛 생산 큐 추가 요청 — UI 래퍼.
        /// 실제로는 RequestEnqueueServerRpc로 위임된다.
        /// </summary>
        /// <param name="barracksId">생산 요청 배럭 Id.</param>
        /// <param name="unitType">생산할 유닛 종류.</param>
        /// <param name="team">요청 팀.</param>
        public void RequestEnqueue(int barracksId, UnitType unitType, TeamId team)
        {
            RequestEnqueueServerRpc(barracksId, (int)unitType, (int)team);
        }

        /// <summary>
        /// 유닛 생산 큐 슬롯 취소 요청 — UI 래퍼.
        /// 실제로는 CancelSlotServerRpc로 위임된다.
        /// </summary>
        /// <param name="barracksId">대상 배럭 Id.</param>
        /// <param name="slotIndex">취소할 큐 슬롯 인덱스(0=생산 중, 1~2=대기 큐).</param>
        /// <param name="team">요청 팀.</param>
        public void RequestCancelSlot(int barracksId, int slotIndex, TeamId team)
        {
            CancelSlotServerRpc(barracksId, slotIndex, (int)team);
        }

        /// <summary>
        /// 랠리포인트 설정 요청 — UI 래퍼.
        /// 실제로는 SetRallyPointServerRpc로 위임된다.
        /// </summary>
        /// <param name="barracksId">대상 배럭 Id.</param>
        /// <param name="q">랠리 타겟 좌표 Q.</param>
        /// <param name="r">랠리 타겟 좌표 R.</param>
        /// <param name="team">요청 팀.</param>
        public void RequestSetRallyPoint(int barracksId, int q, int r, TeamId team)
        {
            SetRallyPointServerRpc(barracksId, q, r, (int)team);
        }

        /// <summary>
        /// 자동 생산 토글 요청 — UI 래퍼.
        /// 실제로는 ToggleAutoServerRpc로 위임된다.
        /// </summary>
        /// <param name="barracksId">대상 배럭 Id.</param>
        /// <param name="unitType">토글 대상 유닛 종류.</param>
        /// <param name="team">요청 팀.</param>
        public void RequestToggleAuto(int barracksId, UnitType unitType, TeamId team)
        {
            ToggleAutoServerRpc(barracksId, (int)unitType, (int)team);
        }

        /// <summary>
        /// 네트워크 디스폰 시 이벤트 구독 해제.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _unitProducedSubscription?.Dispose();
            _unitProducedSubscription = null;
            _productionStartedSubscription?.Dispose();
            _productionStartedSubscription = null;
            _queueChangedSubscription?.Dispose();
            _queueChangedSubscription = null;
        }

        // ====================================================================
        // 이벤트 구독 (서버 전용)
        // ====================================================================

        /// <summary>
        /// 서버에서 GameEvents.OnUnitProduced를 구독하여
        /// 생산 완료 시 SpawnUnitClientRpc를 전송.
        /// 맵 로드 전(UseCase null)에 스폰될 수 있으므로
        /// 이벤트 발행 시점에 UseCase 존재를 재확인.
        /// </summary>
        private void SubscribeToProductionEvents()
        {
            _unitProducedSubscription = GameEvents.OnUnitProduced
                .Subscribe(OnUnitProduced);

            // 생산 시작 이벤트 → 클라이언트에 타이머 시작 알림 (프로그레스 바 동기화)
            _productionStartedSubscription = GameEvents.OnProductionStarted
                .Subscribe(OnProductionStarted);

            // 큐 변경 이벤트 → 클라이언트에 전체 큐 상태 동기화 (취소 등 대응)
            _queueChangedSubscription = GameEvents.OnProductionQueueChanged
                .Subscribe(OnProductionQueueChanged);

            Debug.Log("[Network] NetworkProductionController: 서버 측 생산 이벤트 구독 완료.");
        }

        /// <summary>
        /// 생산 시작 이벤트 핸들러 (서버 전용).
        /// 클라이언트에 생산 시작 정보를 전파하여 프로그레스 바를 동기화.
        /// </summary>
        private void OnProductionStarted(ProductionStartedEvent e)
        {
            if (!IsServer) return;

            UnitProductionUseCase production = _bootstrapper?.GetUnitProduction();
            if (production == null) return;

            var state = production.GetState(e.BarracksId);
            if (state == null) return;

            ProductionStartedClientRpc(
                e.BarracksId,
                (int)e.Type,
                state.RequiredTime);
        }

        /// <summary>
        /// 큐 변경 이벤트 핸들러 (서버 전용).
        /// 클라이언트에 전체 큐 상태를 스냅샷으로 전파.
        ///
        /// [재작성 — 2026-04-19]
        /// 새 단일 PendingQueue 구조에 맞춰 전송 포맷 변경:
        ///   CurrentProducing (Type, IsAuto)
        ///   PendingQueue[0..2] (Type, IsAuto, IsCharged)
        ///   AutoTypes[0..2] (Type)
        ///   AutoCycleIndex
        /// PendingQueue는 슬롯1/슬롯2 + 자동 순환 대기(최대 1개)를 합쳐 최대 3개면 충분하므로
        /// 고정 크기 인자로 직렬화(없는 슬롯은 typeInt=-1).
        /// </summary>
        private void OnProductionQueueChanged(ProductionQueueChangedEvent e)
        {
            if (!IsServer) return;

            UnitProductionUseCase production = _bootstrapper?.GetUnitProduction();
            if (production == null) return;

            var state = production.GetState(e.BarracksId);
            if (state == null) return;

            // CurrentProducing: -1이면 없음
            int currentType = state.CurrentProducing.HasValue ? (int)state.CurrentProducing.Value : -1;

            // PendingQueue: 최대 3개, 각 슬롯마다 (Type, IsAuto, IsCharged) 3필드 전송.
            // 인덱스 0→슬롯1, 1→슬롯2, 2→추가 대기(예: 수동 2개 + 자동 등록 1개의 경우 미차감 자동).
            int p0Type = -1, p1Type = -1, p2Type = -1;
            bool p0Auto = false, p1Auto = false, p2Auto = false;
            bool p0Charged = false, p1Charged = false, p2Charged = false;

            if (state.PendingQueue.Count > 0)
            {
                var s = state.PendingQueue[0];
                p0Type = (int)s.Type; p0Auto = s.IsAuto; p0Charged = s.IsCharged;
            }
            if (state.PendingQueue.Count > 1)
            {
                var s = state.PendingQueue[1];
                p1Type = (int)s.Type; p1Auto = s.IsAuto; p1Charged = s.IsCharged;
            }
            if (state.PendingQueue.Count > 2)
            {
                var s = state.PendingQueue[2];
                p2Type = (int)s.Type; p2Auto = s.IsAuto; p2Charged = s.IsCharged;
            }

            // AutoTypes: 최대 3개, 없는 슬롯은 -1
            int auto0 = state.AutoTypes.Count > 0 ? (int)state.AutoTypes[0] : -1;
            int auto1 = state.AutoTypes.Count > 1 ? (int)state.AutoTypes[1] : -1;
            int auto2 = state.AutoTypes.Count > 2 ? (int)state.AutoTypes[2] : -1;

            SyncQueueStateClientRpc(
                e.BarracksId,
                currentType,
                state.CurrentIsAuto,
                p0Type, p0Auto, p0Charged,
                p1Type, p1Auto, p1Charged,
                p2Type, p2Auto, p2Charged,
                auto0, auto1, auto2,
                state.AutoCycleIndex);
        }

        /// <summary>
        /// 유닛 생산 완료 이벤트 핸들러 (서버 전용).
        /// 생산된 유닛 정보를 모든 클라이언트에 전파.
        /// </summary>
        private void OnUnitProduced(UnitProducedEvent e)
        {
            if (!IsServer) return;

            UnitData unit = e.Unit;
            HexCoord? rally = e.RallyPoint;

            Debug.Log($"[Network] 서버 유닛 생산 완료. UnitId={unit.Id}, Type={unit.Type}, Team={unit.Team}, Pos={unit.Position}");

            // 랠리포인트 좌표 (없으면 0,0으로 전달하고 hasRally=false)
            int rallyQ = rally.HasValue ? rally.Value.Q : 0;
            int rallyR = rally.HasValue ? rally.Value.R : 0;
            bool hasRally = rally.HasValue;

            SpawnUnitClientRpc(
                unit.Id,
                (int)unit.Type,
                (int)unit.Team,
                unit.Position.Q,
                unit.Position.R,
                rallyQ,
                rallyR,
                hasRally);
        }

        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 유닛 생산 큐 추가 요청. 클라이언트 UI에서 호출.
        /// 서버에서 검증 후 UnitProductionUseCase.EnqueueUnit() 실행.
        /// RequireOwnership=false: 모든 클라이언트에서 호출 가능.
        /// </summary>
        /// <param name="barracksId">생산 요청 배럭의 BuildingData Id</param>
        /// <param name="unitTypeInt">UnitType 열거형 정수값</param>
        /// <param name="teamIndex">TeamId 정수값 (Blue=1, Red=2)</param>
        /// <param name="rpcParams">서버 RPC 파라미터 (발신자 ClientId 포함)</param>
        [ServerRpc(RequireOwnership = false)]
        public void RequestEnqueueServerRpc(
            int barracksId,
            int unitTypeInt,
            int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"[Network] 유닛 생산 큐 요청. ClientId={senderClientId}, BarracksId={barracksId}, UnitType={unitTypeInt}, Team={teamIndex}");

            // ----------------------------------------------------------------
            // 1. 부트스트래퍼 및 UseCase 확인
            // ----------------------------------------------------------------
            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] RequestEnqueueServerRpc: GameBootstrapper를 찾을 수 없습니다.");
                SendEnqueueFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            UnitProductionUseCase production = _bootstrapper.GetUnitProduction();
            ResourceUseCase resource = _bootstrapper.GetResource();
            PopulationUseCase population = _bootstrapper.GetPopulation();

            if (production == null || resource == null || population == null)
            {
                Debug.LogError("[Network] RequestEnqueueServerRpc: UseCase가 null입니다. 맵 로드 전일 수 있습니다.");
                SendEnqueueFailed(senderClientId, "맵 로드 중");
                return;
            }

            // ----------------------------------------------------------------
            // 2. 파라미터 변환
            // ----------------------------------------------------------------
            UnitType unitType = (UnitType)unitTypeInt;
            TeamId team = (TeamId)teamIndex;

            // ----------------------------------------------------------------
            // 3. 팀 소유권 검증 (Host=Blue, Client=Red)
            // ----------------------------------------------------------------
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (team != expectedTeam)
            {
                Debug.LogWarning($"[Network] 팀 불일치. 발신자={senderClientId}, 요청팀={team}, 기대팀={expectedTeam}");
                SendEnqueueFailed(senderClientId, "팀 불일치");
                return;
            }

            // ----------------------------------------------------------------
            // 4. 배럭 생산 상태 존재 여부 확인
            //    ProductionState는 OnBuildingPlaced 이벤트로 등록됨
            // ----------------------------------------------------------------
            var state = production.GetState(barracksId);
            if (state == null)
            {
                Debug.LogWarning($"[Network] 배럭 생산 상태 없음. BarracksId={barracksId}");
                SendEnqueueFailed(senderClientId, "배럭 없음");
                return;
            }

            // ----------------------------------------------------------------
            // 5. 골드 확인 (EnqueueUnit 내부에서도 검증하지만 로그를 위해 미리 확인)
            // ----------------------------------------------------------------
            int cost = UnitProductionStats.GetGoldCost(unitType);
            if (!resource.CanAfford(team, cost))
            {
                Debug.LogWarning($"[Network] 골드 부족. 팀={team}, 필요={cost}, 현재={resource.GetGold(team)}");
                SendEnqueueFailed(senderClientId, "골드 부족");
                return;
            }

            // ----------------------------------------------------------------
            // 6. 인구 확인 (EnqueueUnit 내부에서도 검증)
            // ----------------------------------------------------------------
            int popCost = UnitProductionStats.GetPopulationCost(unitType);
            if (!population.HasPopulation(team, popCost))
            {
                Debug.LogWarning($"[Network] 인구 부족. 팀={team}");
                SendEnqueueFailed(senderClientId, "인구 부족");
                return;
            }

            // ----------------------------------------------------------------
            // 7. 서버에서 EnqueueUnit 실행 (골드 즉시 차감, 큐 등록)
            //    EnqueueUnit은 내부에서 CanAfford·HasPopulation을 재확인하므로 안전.
            // ----------------------------------------------------------------
            bool success = production.EnqueueUnit(barracksId, unitType);
            if (!success)
            {
                Debug.LogWarning($"[Network] EnqueueUnit 실패. BarracksId={barracksId}, UnitType={unitType}");
                SendEnqueueFailed(senderClientId, "큐 추가 실패 (큐 가득 참)");
                return;
            }

            Debug.Log($"[Network] 서버: 유닛 생산 큐 추가 성공. BarracksId={barracksId}, UnitType={unitType}, Team={team}");

            // 큐 변경 이벤트는 EnqueueUnit 내부에서 이미 발행됨
            // → OnProductionQueueChanged 구독(서버) → SyncQueueStateClientRpc → 클라이언트 UI 즉시 갱신
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 유닛 생산 완료 후 모든 클라이언트에 UnitData 초기화 정보 전송.
        ///
        /// NGO 전환 후 변경:
        ///   [이전] 클라이언트가 Instantiate(prefab) 직접 실행
        ///   [이후] NGO가 NetworkObject.Spawn()으로 프리팹을 자동 생성.
        ///          이 RPC는 UnitData 생성 + UnitView 초기화만 수행.
        ///          GameObject는 NetworkUnit.OnNetworkSpawn()에서 이미 딕셔너리에 등록됨.
        ///
        /// 서버는 UseCase에서 이미 처리 완료되었으므로 스킵.
        /// 클라이언트는 SpawnUnitWithId로 동일 Id의 UnitData를 재생성하고,
        /// UnitFactory.InitializeUnitView()로 NGO가 생성한 GameObject에 UnitView를 초기화.
        /// GameEvents.OnUnitProduced를 발행하여 ProductionTicker가 랠리 이동을 처리하도록 함.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id</param>
        /// <param name="unitTypeInt">UnitType 열거형 정수값</param>
        /// <param name="teamIndex">TeamId 정수값</param>
        /// <param name="q">스폰 좌표 Q</param>
        /// <param name="r">스폰 좌표 R</param>
        /// <param name="rallyQ">랠리포인트 좌표 Q (hasRally=false면 무시)</param>
        /// <param name="rallyR">랠리포인트 좌표 R (hasRally=false면 무시)</param>
        /// <param name="hasRally">랠리포인트 유무</param>
        [ClientRpc]
        private void SpawnUnitClientRpc(
            int unitId,
            int unitTypeInt,
            int teamIndex,
            int q,
            int r,
            int rallyQ,
            int rallyR,
            bool hasRally)
        {
            // 서버는 이미 UseCase에서 처리 완료 → 중복 방지
            if (IsServer) return;

            Debug.Log($"[Network] SpawnUnitClientRpc 수신. UnitId={unitId}, Type={unitTypeInt}, Team={teamIndex}, Q={q}, R={r}");

            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] SpawnUnitClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
            UnitFactory unitFactory = _bootstrapper.GetUnitFactory();

            if (unitSpawn == null)
            {
                Debug.LogError("[Network] SpawnUnitClientRpc: UnitSpawnUseCase가 null입니다.");
                return;
            }

            // 파라미터 변환
            UnitType unitType = (UnitType)unitTypeInt;
            TeamId team = (TeamId)teamIndex;
            HexCoord spawnCoord = new HexCoord(q, r);

            // UnitData 재생성 (도메인 데이터만 생성, GameObject Instantiate는 하지 않음)
            // SpawnUnitWithId는 내부에서 GameEvents.OnUnitSpawned를 발행하지만,
            // 클라이언트의 UnitFactory.CreateUnitObject()는 NetworkContext 분기에 의해
            // 아무것도 하지 않음 (NGO가 이미 GameObject를 생성했으므로)
            UnitData unit = unitSpawn.SpawnUnitWithId(unitId, unitType, team, spawnCoord);
            if (unit == null)
            {
                Debug.LogWarning($"[Network] SpawnUnitClientRpc: SpawnUnitWithId 실패. UnitId={unitId}");
                return;
            }

            // NGO가 생성한 GameObject에 UnitView 초기화
            // NetworkUnit.OnNetworkSpawn()에서 RegisterUnitObject()로 이미 딕셔너리에 등록됨
            if (unitFactory != null)
            {
                bool initialized = unitFactory.InitializeUnitView(unit);
                if (!initialized)
                {
                    // NetworkUnit.OnNetworkSpawn()이 아직 호출되지 않은 경우
                    // (NGO 프리팹 전달이 SpawnUnitClientRpc보다 늦게 도착)
                    // → 재시도 코루틴으로 대기
                    Debug.LogWarning($"[Network] SpawnUnitClientRpc: UnitView 초기화 지연. UnitId={unitId}. 재시도 대기 중...");
                    StartCoroutine(RetryInitializeUnitView(unitFactory, unit));
                }
            }

            // 랠리포인트 재구성
            HexCoord? rallyPoint = hasRally ? (HexCoord?)new HexCoord(rallyQ, rallyR) : null;

            // OnUnitProduced 발행 → ProductionTicker가 랠리포인트 자동 이동 처리
            // 멀티플레이 클라이언트 경로에는 배럭 Id가 동기화되지 않으며, AI는 싱글플레이 전용이라
            // 이 경로에서 BarracksId를 사용하는 구독자가 없으므로 -1(미지정)을 전달한다.
            GameEvents.OnUnitProduced.OnNext(new UnitProducedEvent(unit, rallyPoint, -1));

            Debug.Log($"[Network] 클라이언트: 유닛 데이터 재생성 완료. UnitId={unit.Id}, Type={unitType}, Team={team}");
        }

        /// <summary>
        /// UnitView 초기화 재시도 코루틴.
        /// NGO가 프리팹을 아직 전달하지 않은 경우 (SpawnUnitClientRpc가 먼저 도착),
        /// NetworkUnit.OnNetworkSpawn()에서 RegisterUnitObject()가 호출될 때까지 대기.
        /// 최대 3초 대기 후 실패 로그 출력.
        /// </summary>
        /// <param name="unitFactory">UnitFactory 참조</param>
        /// <param name="unitData">초기화할 UnitData</param>
        private System.Collections.IEnumerator RetryInitializeUnitView(UnitFactory unitFactory, UnitData unitData)
        {
            float elapsed = 0f;
            float maxWait = 3f; // 최대 3초 대기

            while (elapsed < maxWait)
            {
                yield return null;
                elapsed += UnityEngine.Time.deltaTime;

                if (unitFactory.InitializeUnitView(unitData))
                {
                    Debug.Log($"[Network] RetryInitializeUnitView: 초기화 성공. UnitId={unitData.Id}, 대기 시간={elapsed:F2}초");
                    yield break;
                }
            }

            Debug.LogError($"[Network] RetryInitializeUnitView: {maxWait}초 초과. UnitId={unitData.Id} 초기화 실패. " +
                           "NetworkManager의 Network Prefabs List에 유닛 프리팹이 등록되어 있는지 확인하세요.");
        }

        // ====================================================================
        // ClientRpc — 생산 UI 동기화 (큐/프로그레스)
        // ====================================================================

        /// <summary>
        /// 서버에서 생산 시작 시 클라이언트에 전파.
        /// 클라이언트 측 ProductionState에 타이머 정보를 설정하여
        /// Update()에서 프로그레스 바를 로컬 시뮬레이션 가능하게 함.
        /// </summary>
        [ClientRpc]
        private void ProductionStartedClientRpc(
            int barracksId,
            int unitTypeInt,
            float requiredTime)
        {
            if (IsServer) return;

            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _bootstrapper?.GetUnitProduction();
            if (production == null) return;

            var state = production.GetState(barracksId);
            if (state == null) return;

            // 클라이언트 측 ProductionState에 생산 중 정보 설정 (프로그레스 바 시뮬레이션용)
            state.CurrentProducing = (UnitType)unitTypeInt;
            state.ElapsedTime = 0f;
            state.RequiredTime = requiredTime;

            // UI 갱신 이벤트 발행
            GameEvents.OnProductionStarted.OnNext(
                new ProductionStartedEvent(barracksId, (UnitType)unitTypeInt));
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));

            Debug.Log($"[Network] 클라이언트: 생산 시작 동기화. BarracksId={barracksId}, Type={unitTypeInt}, Time={requiredTime}s");
        }

        /// <summary>
        /// 서버에서 큐 상태 변경 시 전체 스냅샷을 클라이언트에 전파.
        /// 큐 추가, 취소, 생산 완료 등 모든 큐 변경에 대응.
        ///
        /// [재작성 — 2026-04-19]
        /// 새 단일 PendingQueue 구조에 맞춰 전송 포맷 변경:
        ///   - PendingQueue: 최대 3개 슬롯 × (Type, IsAuto, IsCharged)
        ///   - AutoTypes: 최대 3개 (Type만)
        ///   - AutoCycleIndex + CurrentIsAuto 추가
        /// 클라이언트는 전송받은 값으로 ProductionState를 그대로 덮어쓰기.
        /// IsAutoMode는 AutoTypes.Count로 자동 계산되므로 별도 전송 불필요.
        /// </summary>
        /// <param name="barracksId">배럭 Id</param>
        /// <param name="currentTypeInt">현재 생산 중 유닛 타입 (-1=없음)</param>
        /// <param name="currentIsAuto">현재 생산 중 유닛이 자동 경로로 시작되었는지</param>
        /// <param name="p0TypeInt">PendingQueue[0].Type (-1=없음)</param>
        /// <param name="p0IsAuto">PendingQueue[0].IsAuto</param>
        /// <param name="p0IsCharged">PendingQueue[0].IsCharged</param>
        /// <param name="p1TypeInt">PendingQueue[1].Type (-1=없음)</param>
        /// <param name="p1IsAuto">PendingQueue[1].IsAuto</param>
        /// <param name="p1IsCharged">PendingQueue[1].IsCharged</param>
        /// <param name="p2TypeInt">PendingQueue[2].Type (-1=없음)</param>
        /// <param name="p2IsAuto">PendingQueue[2].IsAuto</param>
        /// <param name="p2IsCharged">PendingQueue[2].IsCharged</param>
        /// <param name="auto0TypeInt">AutoTypes[0] (-1=없음)</param>
        /// <param name="auto1TypeInt">AutoTypes[1] (-1=없음)</param>
        /// <param name="auto2TypeInt">AutoTypes[2] (-1=없음)</param>
        /// <param name="autoCycleIndex">AutoCycleIndex (다음 자동 순환 위치)</param>
        [ClientRpc]
        private void SyncQueueStateClientRpc(
            int barracksId,
            int currentTypeInt,
            bool currentIsAuto,
            int p0TypeInt, bool p0IsAuto, bool p0IsCharged,
            int p1TypeInt, bool p1IsAuto, bool p1IsCharged,
            int p2TypeInt, bool p2IsAuto, bool p2IsCharged,
            int auto0TypeInt,
            int auto1TypeInt,
            int auto2TypeInt,
            int autoCycleIndex)
        {
            if (IsServer) return;

            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _bootstrapper?.GetUnitProduction();
            if (production == null) return;

            var state = production.GetState(barracksId);
            if (state == null) return;

            // ── PendingQueue 덮어쓰기 ────────────────────────────────────
            // 클라이언트는 화면 표시용이므로 서버 스냅샷을 그대로 신뢰.
            state.PendingQueue.Clear();
            if (p0TypeInt >= 0)
                state.PendingQueue.Add(new QueueSlot((UnitType)p0TypeInt, p0IsAuto, p0IsCharged));
            if (p1TypeInt >= 0)
                state.PendingQueue.Add(new QueueSlot((UnitType)p1TypeInt, p1IsAuto, p1IsCharged));
            if (p2TypeInt >= 0)
                state.PendingQueue.Add(new QueueSlot((UnitType)p2TypeInt, p2IsAuto, p2IsCharged));

            // ── AutoTypes 덮어쓰기 ───────────────────────────────────────
            // AutoTypes.Count > 0이면 IsAutoMode가 자동으로 true가 됨.
            state.AutoTypes.Clear();
            if (auto0TypeInt >= 0) state.AutoTypes.Add((UnitType)auto0TypeInt);
            if (auto1TypeInt >= 0) state.AutoTypes.Add((UnitType)auto1TypeInt);
            if (auto2TypeInt >= 0) state.AutoTypes.Add((UnitType)auto2TypeInt);
            state.AutoCycleIndex = autoCycleIndex;

            // ── CurrentProducing 동기화 ─────────────────────────────────
            if (currentTypeInt >= 0)
            {
                // ProductionStartedClientRpc에서 이미 타이머가 세팅되었을 수 있으므로
                // CurrentProducing이 비어 있을 때만 설정 (RequiredTime 보존)
                if (!state.CurrentProducing.HasValue)
                    state.CurrentProducing = (UnitType)currentTypeInt;
                state.CurrentIsAuto = currentIsAuto;
            }
            else
            {
                // 생산 종료/취소 상태 — 타이머 초기화
                state.CurrentProducing = null;
                state.CurrentIsAuto = false;
                state.ElapsedTime = 0f;
                state.RequiredTime = 0f;
            }

            // UI 갱신 이벤트 발행 — ProductionPanelUI가 구독 중
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
        }

        // ====================================================================
        // 큐 슬롯 취소 — ServerRpc (BUG-14 수정)
        // ====================================================================

        /// <summary>
        /// 큐 슬롯 취소 요청. 클라이언트 UI에서 호출.
        /// 서버에서 팀 소유권을 검증한 뒤 UnitProductionUseCase.CancelQueueAt()을 실행.
        /// CancelQueueAt 내부에서 골드 환불 + OnProductionQueueChanged 이벤트 발행
        /// → OnProductionQueueChanged 구독(서버) → SyncQueueStateClientRpc로 전체 클라이언트에 자동 전파.
        /// </summary>
        /// <param name="barracksId">취소 대상 배럭의 BuildingData Id</param>
        /// <param name="slotIndex">취소할 큐 슬롯 인덱스 (0=생산 중, 1~2=대기 큐)</param>
        /// <param name="teamIndex">TeamId 정수값 (Blue=1, Red=2)</param>
        /// <param name="rpcParams">서버 RPC 파라미터 (발신자 ClientId 포함)</param>
        [ServerRpc(RequireOwnership = false)]
        public void CancelSlotServerRpc(
            int barracksId,
            int slotIndex,
            int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // 팀 소유권 검증: Host(ClientId=0)=Blue, 그 외=Red
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if ((TeamId)teamIndex != expectedTeam)
            {
                Debug.LogWarning($"[Network] CancelSlotServerRpc: 팀 불일치. ClientId={senderClientId}, 요청팀={teamIndex}, 기대팀={expectedTeam}");
                return;
            }

            // 부트스트래퍼 및 UseCase 확인
            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _bootstrapper?.GetUnitProduction();
            if (production == null)
            {
                Debug.LogWarning("[Network] CancelSlotServerRpc: UnitProductionUseCase가 null.");
                return;
            }

            // 서버에서 CancelQueueAt 실행
            // 내부에서 골드 환불 + OnProductionQueueChanged 이벤트 발행
            // → OnProductionQueueChanged 구독 → SyncQueueStateClientRpc 자동 전파
            bool success = production.CancelQueueAt(barracksId, slotIndex);
            if (!success)
            {
                Debug.LogWarning($"[Network] CancelSlotServerRpc: CancelQueueAt 실패. BarracksId={barracksId}, SlotIndex={slotIndex}");
                return;
            }

            Debug.Log($"[Network] 서버: 큐 슬롯 취소 성공. BarracksId={barracksId}, SlotIndex={slotIndex}");
        }

        // ====================================================================
        // 랠리포인트 설정 — ServerRpc
        // ====================================================================

        /// <summary>
        /// 랠리포인트 설정 요청. 클라이언트 UI에서 호출.
        /// 서버에서 팀 소유권을 검증한 뒤 UnitProductionUseCase.SetRallyPoint()를 실행.
        ///
        /// ClientRpc가 필요 없는 이유:
        ///   랠리포인트는 서버 생산 완료 시 state.RallyPoint를 읽어
        ///   SpawnUnitClientRpc의 hasRally/rallyQ/rallyR 파라미터로 전달됨.
        ///   따라서 서버의 ProductionState.RallyPoint만 정확하면 모든 클라이언트에서 올바르게 동작.
        ///   화면에 표시되는 랠리 마커(OnRallyPointChanged)는 클라이언트 로컬에서
        ///   _production.SetRallyPoint()를 함께 호출하여 표시만 갱신함.
        ///
        /// RequireOwnership=false: 모든 클라이언트에서 호출 가능 (발신자 ClientId로 팀 검증).
        /// </summary>
        /// <param name="barracksId">랠리 설정 대상 배럭의 BuildingData Id</param>
        /// <param name="q">랠리 타겟 타일의 Q 좌표</param>
        /// <param name="r">랠리 타겟 타일의 R 좌표</param>
        /// <param name="teamIndex">TeamId 정수값 (Blue=1, Red=2)</param>
        /// <param name="rpcParams">서버 RPC 파라미터 (발신자 ClientId 포함)</param>
        [ServerRpc(RequireOwnership = false)]
        public void SetRallyPointServerRpc(
            int barracksId,
            int q,
            int r,
            int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // ----------------------------------------------------------------
            // 1. 팀 소유권 검증 (Host=Blue, Client=Red)
            //    다른 팀 배럭의 랠리포인트를 임의 변경하지 못하도록 발신자 팀과 일치 여부 확인.
            // ----------------------------------------------------------------
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if ((TeamId)teamIndex != expectedTeam)
            {
                Debug.LogWarning($"[Network] SetRallyPointServerRpc: 팀 불일치. ClientId={senderClientId}, 요청팀={teamIndex}, 기대팀={expectedTeam}");
                return;
            }

            // ----------------------------------------------------------------
            // 2. 부트스트래퍼 및 UseCase 확인
            //    맵 로드 타이밍 이슈로 초기화가 늦을 수 있으므로 Find로 재시도.
            // ----------------------------------------------------------------
            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _bootstrapper?.GetUnitProduction();
            if (production == null)
            {
                Debug.LogWarning("[Network] SetRallyPointServerRpc: UnitProductionUseCase가 null.");
                return;
            }

            // ----------------------------------------------------------------
            // 3. 서버 상태에 랠리포인트 반영
            //    HexCoord는 Hexiege.Core 네임스페이스의 값 객체.
            //    Application 레이어의 SetRallyPoint가 배럭 존재 여부를 내부에서 처리.
            // ----------------------------------------------------------------
            HexCoord target = new HexCoord(q, r);
            production.SetRallyPoint(barracksId, target);

            Debug.Log($"[Network] 서버: 랠리포인트 설정 완료. BarracksId={barracksId}, Target=({q},{r}), Team={expectedTeam}");
        }

        // ====================================================================
        // 자동 생산 토글 — ServerRpc + ClientRpc
        // ====================================================================

        /// <summary>
        /// 자동 생산 토글 요청. 클라이언트 UI에서 호출.
        /// 서버에서 검증 후 ToggleAutoProduction 실행, 결과를 전체 클라이언트에 동기화.
        /// unitTypeInt 파라미터로 토글 대상 유닛 타입을 지정.
        /// </summary>
        /// <param name="barracksId">배럭 BuildingData Id</param>
        /// <param name="unitTypeInt">토글 대상 유닛 타입 (UnitType 정수값)</param>
        /// <param name="teamIndex">TeamId 정수값</param>
        /// <param name="rpcParams">서버 RPC 파라미터</param>
        [ServerRpc(RequireOwnership = false)]
        public void ToggleAutoServerRpc(
            int barracksId,
            int unitTypeInt,
            int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // 팀 소유권 검증
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if ((TeamId)teamIndex != expectedTeam)
            {
                Debug.LogWarning($"[Network] ToggleAutoServerRpc: 팀 불일치. ClientId={senderClientId}");
                return;
            }

            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _bootstrapper?.GetUnitProduction();
            if (production == null)
            {
                Debug.LogWarning("[Network] ToggleAutoServerRpc: UnitProductionUseCase가 null.");
                return;
            }

            // unitTypeInt를 UnitType으로 변환하여 토글
            UnitType unitType = (UnitType)unitTypeInt;
            bool success = production.ToggleAutoProduction(barracksId, unitType);
            if (!success)
            {
                Debug.LogWarning($"[Network] ToggleAutoServerRpc: ToggleAutoProduction 실패. BarracksId={barracksId}, UnitType={unitType}");
                return;
            }

            var state = production.GetState(barracksId);
            bool isAuto = state?.IsAutoMode ?? false;

            Debug.Log($"[Network] 자동 생산 토글 완료. BarracksId={barracksId}, UnitType={unitType}, IsAuto={isAuto}");
            AutoProductionChangedClientRpc(barracksId, isAuto, unitTypeInt);
        }

        /// <summary>
        /// 자동 생산 상태 변경을 모든 클라이언트에 전파.
        ///
        /// [재작성 — 2026-04-19]
        /// 새 구조에서는 IsAutoMode가 AutoTypes.Count > 0으로 자동 계산되는 읽기 전용 프로퍼티이므로
        /// 여기서 별도로 상태를 변경할 수 없음.
        /// AutoTypes와 PendingQueue 모두 SyncQueueStateClientRpc가 토글과 함께 전파되므로
        /// 이 RPC는 UI 갱신 이벤트 발행 및 로그 출력 용도로만 사용.
        /// </summary>
        /// <param name="barracksId">배럭 Id</param>
        /// <param name="isAuto">토글 후 자동 모드 활성 여부 (로그 전용)</param>
        /// <param name="unitTypeInt">토글 대상 유닛 타입 정수값 (로그 전용)</param>
        [ClientRpc]
        private void AutoProductionChangedClientRpc(int barracksId, bool isAuto, int unitTypeInt)
        {
            // 서버는 ToggleAutoServerRpc에서 이미 처리 완료
            if (IsServer) return;

            UnitType unitType = (UnitType)unitTypeInt;

            // 상태 덮어쓰기는 SyncQueueStateClientRpc가 담당 — 여기서는 UI 갱신 이벤트만 추가 발행.
            // (SyncQueueStateClientRpc가 도착하지 않은 경우 대비해 리프레시 시그널을 한번 더 보냄)
            GameEvents.OnProductionQueueChanged.OnNext(new ProductionQueueChangedEvent(barracksId));

            Debug.Log($"[Network] 클라이언트: 자동 생산 상태 동기화. BarracksId={barracksId}, UnitType={unitType}, IsAuto={isAuto}");
        }

        // ====================================================================
        // 실패 피드백 — 요청자 클라이언트에게만 전송
        // ====================================================================

        /// <summary>
        /// 생산 큐 추가 실패를 요청한 클라이언트에게만 알림.
        /// </summary>
        private void SendEnqueueFailed(ulong targetClientId, string reason)
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };
            EnqueueFailedClientRpc(reason, clientRpcParams);
        }

        /// <summary>
        /// 생산 큐 추가 실패 알림. 요청한 클라이언트에게만 전송.
        /// 클라이언트는 GameEvents.OnToastRequested 이벤트를 발행하여 ToastUI에 표시한다.
        ///
        /// [2026-05-20] reason 문자열을 ToastKey로 매핑:
        ///   "골드 부족" → GoldInsufficient
        ///   "인구 부족"/"인구 한계" → PopulationFull
        ///   "큐 가득" → ProductionQueueFull
        ///   그 외는 로그만 남기고 토스트 생략.
        /// </summary>
        [ClientRpc]
        private void EnqueueFailedClientRpc(string reason, ClientRpcParams clientRpcParams = default)
        {
            Debug.LogWarning($"[Network] 유닛 생산 큐 추가 실패: {reason}");

            // reason → ToastKey 매핑. 매핑이 정의된 사유만 토스트 발행.
            if (reason == "골드 부족")
                GameEvents.OnToastRequested.OnNext(ToastKey.GoldInsufficient);
            else if (reason == "인구 부족" || reason == "인구 한계")
                GameEvents.OnToastRequested.OnNext(ToastKey.PopulationFull);
            else if (reason == "큐 가득")
                GameEvents.OnToastRequested.OnNext(ToastKey.ProductionQueueFull);
        }
    }
}
