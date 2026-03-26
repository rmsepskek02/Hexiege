// ============================================================================
// NetworkUnitMovementController.cs
// 네트워크 모드에서 유닛 이동 명령을 서버로 중계.
//
// NGO NetworkTransform 기반 동기화:
//   [이전] 클라이언트 예측(Client Prediction) + SyncMovementClientRpc
//   [이후] 서버 권위(Server Authority) — 서버만 이동 실행, NetworkTransform이 자동 동기화
//
//   클라이언트가 이동 명령을 입력하면:
//   1. RequestMoveServerRpc만 전송 (로컬 예측 없음)
//   2. 서버에서 팀 소유권 검증 + 경로 계산 → UnitView.MoveTo() 실행
//   3. NetworkTransform이 서버 transform.position을 모든 클라이언트에 자동 보간·동기화
//   4. Walk 애니메이션은 NetworkCombatController의 ClientRpc로 별도 동기화
//
//   클라이언트 예측 제거로 이동 명령 후 약간의 지연(네트워크 RTT)이 발생하지만,
//   RTS 장르에서 수백ms 반응성은 일반적이며 허용 가능한 수준.
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
    /// 클라이언트 입력 → 서버 검증/실행 → NetworkTransform 자동 동기화.
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
        /// 유닛 이동 명령. 서버 권위 방식으로 처리.
        ///
        /// [이전] 로컬 예측 이동 + 서버 검증 + SyncMovementClientRpc
        /// [이후] 서버에 이동 요청만 전송 — 로컬 예측 없음.
        ///        서버가 경로 계산 + MoveTo() 실행 → NetworkTransform이 위치 자동 동기화.
        ///
        /// 싱글플레이라면 이 메서드를 거치지 않고 기존 흐름 사용.
        /// </summary>
        /// <param name="unit">이동할 유닛 데이터</param>
        /// <param name="target">목표 타일 좌표</param>
        /// <param name="unitFactory">사용하지 않음 (기존 API 호환성 유지)</param>
        /// <param name="movementUseCase">사용하지 않음 (기존 API 호환성 유지)</param>
        public void RequestMove(
            UnitData unit,
            HexCoord target,
            UnitFactory unitFactory,
            UnitMovementUseCase movementUseCase)
        {
            if (unit == null) return;

            // 서버에 이동 요청만 전송 (로컬 예측 없음)
            // 서버가 경로 계산 + MoveTo() 실행 → NetworkTransform이 위치 자동 동기화
            RequestMoveServerRpc(unit.Id, target.Q, target.R);

            Debug.Log($"[Network] 이동 요청 전송. UnitId={unit.Id}, Target={target}");
        }

        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 유닛 이동 요청. 클라이언트에서 호출.
        /// 서버에서 팀 소유권 검증 후 경로 계산 → UnitView.MoveTo() 실행.
        /// NetworkTransform이 서버 위치를 모든 클라이언트에 자동 동기화.
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
            // 5. 서버에서 UnitView.MoveTo() 실행
            //    → MoveAlongPath()가 transform.position을 매 프레임 갱신
            //    → NetworkTransform이 위치를 모든 클라이언트에 자동 동기화
            //    → Walk 시작/정지는 GameEvents → NetworkCombatController → ClientRpc로 전파
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
        }
    }
}
