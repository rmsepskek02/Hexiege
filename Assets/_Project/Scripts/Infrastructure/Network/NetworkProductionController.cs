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

        /// <summary>
        /// 네트워크 디스폰 시 이벤트 구독 해제.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _unitProducedSubscription?.Dispose();
            _unitProducedSubscription = null;
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

            Debug.Log("[Network] NetworkProductionController: 서버 측 OnUnitProduced 구독 완료.");
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
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

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
            // 클라이언트 UI 갱신은 OnProductionQueueChanged를 별도 ClientRpc로 동기화하지 않음
            // (클라이언트는 서버가 발행한 이벤트를 직접 받지 못하므로 UI는 스폰 완료 시 갱신됨)
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 유닛 생산 완료 후 모든 클라이언트에 유닛 스폰 명령 전송.
        /// 서버는 UseCase에서 이미 처리 완료되었으므로 스킵.
        /// 클라이언트는 SpawnUnitWithId로 동일 Id의 UnitData를 재생성하고
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

            // UseCase 접근
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] SpawnUnitClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
            if (unitSpawn == null)
            {
                Debug.LogError("[Network] SpawnUnitClientRpc: UnitSpawnUseCase가 null입니다.");
                return;
            }

            // 파라미터 변환
            UnitType unitType = (UnitType)unitTypeInt;
            TeamId team = (TeamId)teamIndex;
            HexCoord spawnCoord = new HexCoord(q, r);

            // 서버와 동일한 Id로 UnitData 재생성 + OnUnitSpawned 이벤트 발행
            UnitData unit = unitSpawn.SpawnUnitWithId(unitId, unitType, team, spawnCoord);
            if (unit == null)
            {
                Debug.LogWarning($"[Network] SpawnUnitClientRpc: SpawnUnitWithId 실패. UnitId={unitId}");
                return;
            }

            // 랠리포인트 재구성
            HexCoord? rallyPoint = hasRally ? (HexCoord?)new HexCoord(rallyQ, rallyR) : null;

            // OnUnitProduced 발행 → ProductionTicker가 랠리포인트 자동 이동 처리
            GameEvents.OnUnitProduced.OnNext(new UnitProducedEvent(unit, rallyPoint));

            Debug.Log($"[Network] 클라이언트: 유닛 재생성 완료. UnitId={unit.Id}, Type={unitType}, Team={team}");
        }

        // ====================================================================
        // 자동 생산 토글 — ServerRpc + ClientRpc
        // ====================================================================

        /// <summary>
        /// 자동 생산 토글 요청. 클라이언트 UI 롱프레스에서 호출.
        /// 서버에서 검증 후 ToggleAutoProduction 실행, 결과를 전체 클라이언트에 동기화.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ToggleAutoServerRpc(
            int barracksId,
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

            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            UnitProductionUseCase production = _bootstrapper?.GetUnitProduction();
            if (production == null)
            {
                Debug.LogWarning("[Network] ToggleAutoServerRpc: UnitProductionUseCase가 null.");
                return;
            }

            bool success = production.ToggleAutoProduction(barracksId, UnitType.Pistoleer);
            if (!success)
            {
                Debug.LogWarning($"[Network] ToggleAutoServerRpc: ToggleAutoProduction 실패. BarracksId={barracksId}");
                return;
            }

            var state = production.GetState(barracksId);
            bool isAuto = state?.IsAutoMode ?? false;

            Debug.Log($"[Network] 자동 생산 토글 완료. BarracksId={barracksId}, IsAuto={isAuto}");
            AutoProductionChangedClientRpc(barracksId, isAuto);
        }

        /// <summary>
        /// 자동 생산 상태 변경을 모든 클라이언트에 전파.
        /// 클라이언트 측 ProductionState를 직접 동기화하여 UI가 올바른 값을 표시하도록 함.
        /// </summary>
        [ClientRpc]
        private void AutoProductionChangedClientRpc(int barracksId, bool isAuto)
        {
            // 서버는 ToggleAutoServerRpc에서 이미 처리 완료
            if (IsServer) return;

            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            UnitProductionUseCase production = _bootstrapper?.GetUnitProduction();
            if (production == null) return;

            var state = production.GetState(barracksId);
            if (state == null) return;

            // 클라이언트 측 상태 동기화 (서버와 일치시킴)
            state.IsAutoMode = isAuto;
            if (isAuto && !state.AutoTypes.Contains(UnitType.Pistoleer))
                state.AutoTypes.Add(UnitType.Pistoleer);
            else if (!isAuto)
                state.AutoTypes.Clear();

            // 기존 구독을 통해 ProductionPanelUI가 자동으로 갱신
            GameEvents.OnProductionQueueChanged.OnNext(new ProductionQueueChangedEvent(barracksId));

            Debug.Log($"[Network] 클라이언트: 자동 생산 상태 동기화. BarracksId={barracksId}, IsAuto={isAuto}");
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
        /// 현재는 로그만 출력. 향후 UI 피드백으로 확장 가능.
        /// </summary>
        [ClientRpc]
        private void EnqueueFailedClientRpc(string reason, ClientRpcParams clientRpcParams = default)
        {
            Debug.LogWarning($"[Network] 유닛 생산 큐 추가 실패: {reason}");
            // TODO: UI 피드백 — 토스트 메시지 등
        }
    }
}
