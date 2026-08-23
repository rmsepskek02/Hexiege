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

        /// <summary>게임 서비스 참조. UseCase 접근에 사용. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화.</summary>
        private IGameServices _services;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameBootstrapper를 탐색.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _services = GameServicesLocator.Current;
            if (_services == null)
            {
                // [운영] 스폰은 계속되고 아직 요청 전이라 기능이 죽지는 않았다 → 축 A Warn.
                //   NGO 스폰 타이밍은 회선에 좌우돼 플레이어 기기에서만 어긋날 수 있고 통지 경로가
                //   없다 → 축 B 운영. 선행 판정(NetworkBuildingController:58)과 같은 사건 → 같은 키.
                GameLog.Ops.Warn(LogEvent.NetworkControllerSpawnedWithoutGameServices,
                                 "Network", nameof(NetworkUnitMovementController),
                                 "스폰 시점에 GameServicesLocator 에서 IGameServices 를 얻지 못했다");
            }

            // [개발] 진입 흔적.
            GameLog.Dev.Info("Network", nameof(NetworkUnitMovementController), "네트워크 스폰",
                             $"IsServer={IsServer}");
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
            IUnitFactory unitFactory,
            UnitMovementUseCase movementUseCase)
        {
            if (unit == null) return;

            // 서버에 이동 요청만 전송 (로컬 예측 없음)
            // 서버가 경로 계산 + MoveTo() 실행 → NetworkTransform이 위치 자동 동기화
            RequestMoveServerRpc(unit.Id, target.Q, target.R);

            // [개발] 플레이어가 이동을 지시할 때마다 찍히는 고빈도 로그.
            //   Dev 라서 릴리스에서는 호출과 문자열 보간까지 통째로 사라진다(LogRules 1.7).
            GameLog.Dev.Info("Network", nameof(NetworkUnitMovementController), "이동 요청 전송",
                             $"UnitId={unit.Id}, TargetQ={target.Q}, TargetR={target.R}");
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
            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                // [운영] 이후 모든 이동 요청이 같은 자리에서 죽는다 — 복구 경로가 없다 → Error.
                //   플레이어에게는 "유닛이 안 움직인다" 로만 보이고 통지가 없다 → 운영.
                //   선행 판정(NetworkBuildingController:142 등)과 같은 사건 → 같은 키.
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing,
                                  "Network", nameof(NetworkUnitMovementController),
                                  "RequestMoveServerRpc — IGameServices 를 얻지 못했다. 이후 모든 이동 요청이 같은 자리에서 죽는다",
                                  $"Request=Move, ClientId={senderClientId}, UnitId={unitId}");
                return;
            }

            UnitSpawnUseCase unitSpawn = _services.GetUnitSpawn();
            UnitMovementUseCase movement = _services.GetMovement();
            IUnitFactory unitFactory = _services.GetUnitFactory();

            if (unitSpawn == null || movement == null)
            {
                // [운영] 위와 같은 사건(서버 RPC 처리 중 조합 루트/UseCase 부재) → 같은 키.
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing,
                                  "Network", nameof(NetworkUnitMovementController),
                                  "RequestMoveServerRpc — UseCase 가 null 이다. 맵이 아직 로드되지 않았을 수 있다",
                                  $"Request=Move, ClientId={senderClientId}, UnitId={unitId}");
                return;
            }

            // ----------------------------------------------------------------
            // 2. 유닛 조회
            // ----------------------------------------------------------------
            UnitData unit = unitSpawn.GetUnit(unitId);
            if (unit == null || !unit.IsAlive)
            {
                // [운영] 요청만 거부하고 게임은 계속된다 → 축 A Warn.
                //   클라이언트는 살아 있는 자기 유닛만 선택할 수 있으므로, 서버에 없거나 죽어 있다는
                //   것은 클라·서버 상태 불일치다. 화면에는 아무 안내도 뜨지 않는다 → 축 B 운영.
                //   선행 판정(NetworkBuildingController:385 "대상 건물 없음")과 같은 사건 → 같은 키.
                GameLog.Ops.Warn(LogEvent.ServerRejectedTargetNotFound,
                                 "Network", nameof(NetworkUnitMovementController),
                                 "이동 거부 — 대상 유닛이 서버에 없거나 이미 사망했다",
                                 $"Request=Move, ClientId={senderClientId}, UnitId={unitId}");
                return;
            }

            // ----------------------------------------------------------------
            // 3. 팀 소유권 검증 (Host=Blue, Client=Red)
            // ----------------------------------------------------------------
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (unit.Team != expectedTeam)
            {
                // [운영] 요청만 거부하고 게임은 계속된다 → Warn.
                //   정상 클라이언트는 상대 팀 유닛에 이동 명령을 낼 수 없으므로 변조 탐지 신호로 읽는다.
                //   선행 판정(NetworkBuildingController:171 "팀 불일치")과 같은 사건 → 같은 키.
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest,
                                 "Network", nameof(NetworkUnitMovementController),
                                 "이동 거부 — 유닛 소유 팀과 발신자의 팀이 다르다",
                                 $"Request=Move, Reason=Ownership, ClientId={senderClientId}, " +
                                 $"UnitId={unitId}, UnitTeam={unit.Team}, ExpectedTeam={expectedTeam}");
                return;
            }

            // ----------------------------------------------------------------
            // 4. 서버에서 경로 계산
            // ----------------------------------------------------------------
            HexCoord target = new HexCoord(targetQ, targetR);
            List<HexCoord> path = movement.RequestMove(unit, target);

            if (path == null)
            {
                // [개발] 도달할 수 없는 타일을 찍으면 정상적으로 일어나는 결과다 → 축 A Info(레벨 유지).
                //   에디터에서 그대로 재현된다 → 축 B 개발.
                GameLog.Dev.Info("Network", nameof(NetworkUnitMovementController), "서버: 경로 없음",
                                 $"UnitId={unitId}, TargetQ={target.Q}, TargetR={target.R}");
                return;
            }

            // [개발] 이동 지시마다 찍히는 고빈도 로그.
            GameLog.Dev.Info("Network", nameof(NetworkUnitMovementController), "서버: 이동 경로 계산 완료",
                             $"UnitId={unitId}, PathLength={path.Count}");

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
