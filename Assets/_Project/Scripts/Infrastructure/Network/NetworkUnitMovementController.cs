// ============================================================================
// NetworkUnitMovementController.cs
// 네트워크 모드에서 유닛 이동 명령을 서버로 중계하고 상대방 클라이언트에 동기화.
//
// 클라이언트 예측(Client Prediction) 방식:
//   - 로컬에서 즉시 이동 시작 (응답성 확보)
//   - 서버에 이동 요청 전송
//   - 서버 검증 통과 시 → 요청자를 제외한 나머지 클라이언트에 동기화
//   - 서버 검증 거절 시 → 롤백 생략 (Phase 9에서 개선 예정)
//
// 흐름:
//   1. InputHandler → RequestMove(unit, target) 호출
//   2. 로컬: UnitMovementUseCase.RequestMove() + UnitView.MoveTo() (예측)
//   3. 서버 Rpc 전송: RequestMoveServerRpc(unitId, targetQ, targetR)
//   4. 서버: 팀 소유권 검증 + 경로 계산 → SyncMovementClientRpc (요청자 제외 전송)
//   5. 상대 클라이언트: UnitView.MoveTo(path) 호출 → 시각적 이동 반영
//
// 배치:
//   씬에 빈 GameObject "NetworkUnitMovementController" 생성.
//   NetworkObject 컴포넌트 + 이 스크립트를 부착.
//   NetworkManager의 씬 오브젝트로 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 유닛 이동 명령 네트워크 컨트롤러.
    /// 클라이언트 예측으로 로컬 즉시 이동 + 서버 검증 후 상대방 동기화.
    /// </summary>
    public class NetworkUnitMovementController : NetworkBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>GameBootstrapper 참조. UseCase 접근에 사용.</summary>
        private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameBootstrapper를 탐색.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
            if (_bootstrapper == null)
            {
                Debug.LogWarning("[Network] NetworkUnitMovementController: GameBootstrapper를 찾을 수 없습니다.");
            }

            Debug.Log($"[Network] NetworkUnitMovementController 스폰. IsServer={IsServer}");
        }

        // ====================================================================
        // 공개 API — InputHandler에서 호출
        // ====================================================================

        /// <summary>
        /// 유닛 이동 명령. 클라이언트 예측 방식으로 처리.
        /// 1) 로컬에서 즉시 이동 시작 (UnitView.MoveTo)
        /// 2) 서버에 이동 요청 전송 (RequestMoveServerRpc)
        /// 싱글플레이라면 이 메서드를 거치지 않고 기존 흐름 사용.
        /// </summary>
        /// <param name="unit">이동할 유닛 데이터</param>
        /// <param name="target">목표 타일 좌표</param>
        /// <param name="unitFactory">UnitView 조회에 사용할 팩토리</param>
        /// <param name="movementUseCase">경로 계산에 사용할 UseCase</param>
        public void RequestMove(
            UnitData unit,
            HexCoord target,
            UnitFactory unitFactory,
            UnitMovementUseCase movementUseCase)
        {
            if (unit == null || unitFactory == null || movementUseCase == null) return;

            // ----------------------------------------------------------------
            // 1. 로컬 예측: 경로 계산 후 즉시 이동 시작
            // ----------------------------------------------------------------
            List<HexCoord> path = movementUseCase.RequestMove(unit, target);
            if (path == null)
            {
                Debug.Log($"[Network] 유닛 이동 경로 없음. UnitId={unit.Id}, Target={target}");
                return;
            }

            // UnitFactory에서 UnitView를 가져와 로컬 이동 즉시 시작
            GameObject unitObj = unitFactory.GetUnitObject(unit.Id);
            if (unitObj != null)
            {
                var unitView = unitObj.GetComponent<Hexiege.Presentation.UnitView>();
                if (unitView != null)
                {
                    unitView.MoveTo(path);
                    Debug.Log($"[Network] 로컬 예측 이동 시작. UnitId={unit.Id}, Target={target}");
                }
            }

            // ----------------------------------------------------------------
            // 2. 서버에 이동 요청 전송 (서버 검증 + 상대방 동기화)
            // ----------------------------------------------------------------
            RequestMoveServerRpc(unit.Id, target.Q, target.R);
        }

        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 유닛 이동 요청. 클라이언트에서 호출.
        /// 서버에서 팀 소유권 검증 후 경로 계산 → 상대방 클라이언트에 동기화.
        /// RequireOwnership=false: 모든 클라이언트에서 호출 가능.
        /// </summary>
        /// <param name="unitId">이동할 유닛 Id</param>
        /// <param name="targetQ">목표 좌표 Q</param>
        /// <param name="targetR">목표 좌표 R</param>
        /// <param name="rpcParams">서버 RPC 파라미터 (발신자 ClientId 포함)</param>
        [ServerRpc(RequireOwnership = false)]
        public void RequestMoveServerRpc(
            int unitId,
            int targetQ,
            int targetR,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // ----------------------------------------------------------------
            // 1. UseCase 존재 확인
            // ----------------------------------------------------------------
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] RequestMoveServerRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
            UnitMovementUseCase movement = _bootstrapper.GetMovement();
            UnitFactory unitFactory = _bootstrapper.GetUnitFactory();

            if (unitSpawn == null || movement == null)
            {
                Debug.LogError("[Network] RequestMoveServerRpc: UseCase가 null입니다. 맵 로드 전일 수 있습니다.");
                return;
            }

            // ----------------------------------------------------------------
            // 2. 유닛 조회
            // ----------------------------------------------------------------
            UnitData unit = unitSpawn.GetUnit(unitId);
            if (unit == null || !unit.IsAlive)
            {
                Debug.LogWarning($"[Network] RequestMoveServerRpc: 유닛 없음 또는 사망. UnitId={unitId}");
                return;
            }

            // ----------------------------------------------------------------
            // 3. 팀 소유권 검증 (Host=Blue, Client=Red)
            // ----------------------------------------------------------------
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (unit.Team != expectedTeam)
            {
                Debug.LogWarning($"[Network] 팀 불일치로 이동 거부. 발신자={senderClientId}, 유닛팀={unit.Team}, 기대팀={expectedTeam}");
                return;
            }

            // ----------------------------------------------------------------
            // 4. 서버에서 경로 계산
            // ----------------------------------------------------------------
            HexCoord target = new HexCoord(targetQ, targetR);
            List<HexCoord> path = movement.RequestMove(unit, target);

            if (path == null)
            {
                Debug.Log($"[Network] 서버: 경로 없음. UnitId={unitId}, Target={target}");
                return;
            }

            Debug.Log($"[Network] 서버: 이동 경로 계산 완료. UnitId={unitId}, 경로 길이={path.Count}");

            // ----------------------------------------------------------------
            // 5. 서버 자신도 UnitView 이동 시작
            //    (서버 측 UnitFactory가 있다면 시각 처리)
            // ----------------------------------------------------------------
            if (unitFactory != null)
            {
                GameObject serverUnitObj = unitFactory.GetUnitObject(unitId);
                if (serverUnitObj != null)
                {
                    var serverView = serverUnitObj.GetComponent<Hexiege.Presentation.UnitView>();
                    serverView?.MoveTo(path);
                }
            }

            // ----------------------------------------------------------------
            // 6. 경로를 int 배열로 직렬화하여 상대방 클라이언트에 전송
            //    요청자(senderClientId)는 이미 예측 이동 중이므로 제외
            // ----------------------------------------------------------------
            int[] pathQ = new int[path.Count];
            int[] pathR = new int[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                pathQ[i] = path[i].Q;
                pathR[i] = path[i].R;
            }

            // 요청자를 제외한 모든 클라이언트에게 전송
            ClientRpcParams excludeSender = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    // 요청자를 제외: ConnectedClientsIds에서 senderClientId 제거
                    TargetClientIds = GetOtherClientIds(senderClientId)
                }
            };

            SyncMovementClientRpc(unitId, pathQ, pathR, excludeSender);
        }

        // ====================================================================
        // ClientRpc — 서버 → 요청자 제외 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 검증된 이동 경로를 요청자 제외 클라이언트에 전파.
        /// 수신한 클라이언트는 해당 유닛의 UnitView를 찾아 이동 시작.
        /// </summary>
        /// <param name="unitId">이동할 유닛 Id</param>
        /// <param name="pathQ">경로 좌표 Q 배열</param>
        /// <param name="pathR">경로 좌표 R 배열</param>
        /// <param name="clientRpcParams">대상 클라이언트 파라미터 (요청자 제외)</param>
        [ClientRpc]
        private void SyncMovementClientRpc(
            int unitId,
            int[] pathQ,
            int[] pathR,
            ClientRpcParams clientRpcParams = default)
        {
            // 서버는 RequestMoveServerRpc에서 이미 처리 → 중복 방지
            if (IsServer) return;

            Debug.Log($"[Network] SyncMovementClientRpc 수신. UnitId={unitId}, 경로 길이={pathQ.Length}");

            // UseCase / 팩토리 접근
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] SyncMovementClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            UnitFactory unitFactory = _bootstrapper.GetUnitFactory();
            if (unitFactory == null)
            {
                Debug.LogError("[Network] SyncMovementClientRpc: UnitFactory가 null입니다.");
                return;
            }

            // 경로 복원
            List<HexCoord> path = new List<HexCoord>(pathQ.Length);
            for (int i = 0; i < pathQ.Length; i++)
            {
                path.Add(new HexCoord(pathQ[i], pathR[i]));
            }

            // UnitView 조회 후 이동 시작
            GameObject unitObj = unitFactory.GetUnitObject(unitId);
            if (unitObj == null)
            {
                Debug.LogWarning($"[Network] SyncMovementClientRpc: UnitId={unitId}에 해당하는 GameObject 없음.");
                return;
            }

            var unitView = unitObj.GetComponent<Hexiege.Presentation.UnitView>();
            if (unitView == null)
            {
                Debug.LogWarning($"[Network] SyncMovementClientRpc: UnitId={unitId}에 UnitView 컴포넌트 없음.");
                return;
            }

            // 이미 이동 중이어도 새 경로로 덮어씀 (MoveTo 내부에서 기존 코루틴 중단)
            unitView.MoveTo(path);
            Debug.Log($"[Network] 상대방 클라이언트: 유닛 이동 동기화. UnitId={unitId}");
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 현재 연결된 클라이언트 중 excludeId를 제외한 Id 목록 반환.
        /// SyncMovementClientRpc에서 요청자 제외 전송에 사용.
        /// </summary>
        private ulong[] GetOtherClientIds(ulong excludeId)
        {
            if (NetworkManager.Singleton == null) return new ulong[0];

            var connectedIds = NetworkManager.Singleton.ConnectedClientsIds;
            var result = new System.Collections.Generic.List<ulong>();
            foreach (ulong id in connectedIds)
            {
                if (id != excludeId)
                    result.Add(id);
            }
            return result.ToArray();
        }
    }
}
