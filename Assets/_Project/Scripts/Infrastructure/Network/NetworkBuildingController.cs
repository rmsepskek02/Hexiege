// ============================================================================
// NetworkBuildingController.cs
// 네트워크 모드에서 건물 배치 요청을 서버로 중계하고 모든 클라이언트에 동기화.
//
// 흐름:
//   1. 클라이언트 UI → RequestBuildServerRpc(buildingType, teamIndex, q, r)
//   2. 서버: 팀 소유권·골드·위치 검증 → BuildingPlacementUseCase.PlaceBuilding() 실행
//   3. 서버: 생성된 BuildingData.Id 확보 → SpawnBuildingClientRpc(id, ...) 전파
//   4. 모든 클라이언트: PlaceBuildingWithId()로 동일 Id의 BuildingData 재생성
//      → GameEvents.OnBuildingPlaced 발행 → BuildingFactory가 프리팹 생성
//
// 배치:
//   씬에 빈 GameObject "NetworkBuildingController" 생성.
//   NetworkObject 컴포넌트 + 이 스크립트를 부착.
//   NetworkManager의 씬 오브젝트로 자동 스폰되도록 설정.
//
// 검증 실패 시:
//   요청한 클라이언트에게만 BuildFailedClientRpc를 전송하여 UI 피드백 제공.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 건물 배치 네트워크 컨트롤러.
    /// 클라이언트의 건물 배치 요청을 서버에서 검증·실행하고 모든 클라이언트에 동기화.
    /// </summary>
    public class NetworkBuildingController : NetworkBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> GameBootstrapper 참조. UseCase 접근에 사용. </summary>
        private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameBootstrapper 탐색.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
            if (_bootstrapper == null)
            {
                Debug.LogWarning("[Network] NetworkBuildingController: GameBootstrapper를 찾을 수 없습니다.");
            }

            Debug.Log($"[Network] NetworkBuildingController 스폰. IsServer={IsServer}");
        }

        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 건물 배치 요청. 클라이언트가 UI 버튼을 눌렀을 때 호출.
        /// 서버에서 검증 후 성공 시 SpawnBuildingClientRpc 전파.
        /// RequireOwnership=false: 어느 클라이언트든 호출 가능.
        /// </summary>
        /// <param name="buildingTypeInt">BuildingType 열거형 정수값</param>
        /// <param name="teamIndex">팀 인덱스 (0=Blue, 1=Red, TeamId 정수값)</param>
        /// <param name="q">건물 배치 좌표 Q</param>
        /// <param name="r">건물 배치 좌표 R</param>
        /// <param name="rpcParams">서버 RPC 파라미터 (발신자 ClientId 포함)</param>
        [ServerRpc(RequireOwnership = false)]
        public void RequestBuildServerRpc(
            int buildingTypeInt,
            int teamIndex,
            int q,
            int r,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"[Network] 건물 배치 요청 수신. ClientId={senderClientId}, Type={buildingTypeInt}, Team={teamIndex}, Q={q}, R={r}");

            // ----------------------------------------------------------------
            // 1. 부트스트래퍼 및 UseCase 존재 확인
            // ----------------------------------------------------------------
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] RequestBuildServerRpc: GameBootstrapper를 찾을 수 없습니다.");
                SendBuildFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _bootstrapper.GetBuildingPlacement();
            ResourceUseCase resource = _bootstrapper.GetResource();

            if (buildingPlacement == null || resource == null)
            {
                Debug.LogError("[Network] RequestBuildServerRpc: UseCase가 null입니다. 맵이 아직 로드되지 않았을 수 있습니다.");
                SendBuildFailed(senderClientId, "맵 로드 중");
                return;
            }

            // ----------------------------------------------------------------
            // 2. 파라미터 변환
            // ----------------------------------------------------------------
            BuildingType buildingType = (BuildingType)buildingTypeInt;
            TeamId team = (TeamId)teamIndex;
            HexCoord coord = new HexCoord(q, r);

            // ----------------------------------------------------------------
            // 3. 팀 소유권 검증 (발신자 ClientId → 기대 팀 매핑)
            //    Host(ClientId=0) → Blue, Client(ClientId!=0) → Red
            // ----------------------------------------------------------------
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (team != expectedTeam)
            {
                Debug.LogWarning($"[Network] 팀 불일치. 발신자={senderClientId}, 요청팀={team}, 기대팀={expectedTeam}");
                SendBuildFailed(senderClientId, "팀 불일치");
                return;
            }

            // ----------------------------------------------------------------
            // 4. 골드 충분 여부 확인
            // ----------------------------------------------------------------
            int cost = GetBuildingCost(buildingType);
            if (!resource.CanAfford(team, cost))
            {
                Debug.LogWarning($"[Network] 골드 부족. 팀={team}, 비용={cost}, 현재골드={resource.GetGold(team)}");
                SendBuildFailed(senderClientId, "골드 부족");
                return;
            }

            // ----------------------------------------------------------------
            // 5. 서버에서 UseCase 실행 (검증 포함)
            //    PlaceBuilding 내부에서 타일 존재·IsWalkable·팀 소유 검증 실행.
            // ----------------------------------------------------------------
            // 골드 차감을 UseCase 실행 전에 수행 (UseCase가 검증을 포함하므로 실패 시 환불 필요)
            // 실제로는 UseCase 실행 후 차감이 더 안전하지만 PlaceBuilding이 실패 시 null 반환
            BuildingData placed = buildingPlacement.PlaceBuilding(buildingType, team, coord);
            if (placed == null)
            {
                Debug.LogWarning($"[Network] 서버 측 건물 배치 실패. Type={buildingType}, Team={team}, Coord={coord}");
                SendBuildFailed(senderClientId, "배치 위치 오류");
                return;
            }

            // ----------------------------------------------------------------
            // 6. 골드 차감 (서버에서만 실행)
            // ----------------------------------------------------------------
            resource.SpendGold(team, cost);
            Debug.Log($"[Network] 서버: 건물 배치 성공. Id={placed.Id}, Type={buildingType}, Team={team}, Coord={coord}, 차감골드={cost}");

            // ----------------------------------------------------------------
            // 7. 모든 클라이언트에 동기화 명령 전파
            //    서버에서 생성된 placed.Id를 전달하여 클라이언트에서 동일한 Id 사용.
            // ----------------------------------------------------------------
            SpawnBuildingClientRpc(placed.Id, buildingTypeInt, teamIndex, q, r);
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 건물 배치 성공 후 모든 클라이언트에 건물 재생성 명령 전송.
        /// 서버는 이미 UseCase에서 처리 완료됐으므로 스킵.
        /// 클라이언트는 PlaceBuildingWithId로 동일 Id의 BuildingData를 재생성하고
        /// 이벤트 발행을 통해 BuildingFactory가 프리팹을 생성하도록 함.
        /// </summary>
        /// <param name="buildingId">서버에서 발급된 건물 Id</param>
        /// <param name="buildingTypeInt">BuildingType 열거형 정수값</param>
        /// <param name="teamIndex">팀 인덱스 (TeamId 정수값)</param>
        /// <param name="q">건물 배치 좌표 Q</param>
        /// <param name="r">건물 배치 좌표 R</param>
        [ClientRpc]
        private void SpawnBuildingClientRpc(int buildingId, int buildingTypeInt, int teamIndex, int q, int r)
        {
            // 서버는 이미 PlaceBuilding()에서 처리 완료 → 중복 방지
            if (IsServer) return;

            Debug.Log($"[Network] SpawnBuildingClientRpc 수신. Id={buildingId}, Type={buildingTypeInt}, Team={teamIndex}, Q={q}, R={r}");

            // UseCase 접근
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] SpawnBuildingClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _bootstrapper.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                Debug.LogError("[Network] SpawnBuildingClientRpc: BuildingPlacementUseCase가 null입니다.");
                return;
            }

            // 파라미터 변환
            BuildingType buildingType = (BuildingType)buildingTypeInt;
            TeamId team = (TeamId)teamIndex;
            HexCoord coord = new HexCoord(q, r);

            // 서버와 동일한 Id로 BuildingData 재생성 + 이벤트 발행
            // PlaceBuildingWithId 내부에서 GameEvents.OnBuildingPlaced 발행
            // → BuildingFactory가 프리팹을 생성함
            BuildingData result = buildingPlacement.PlaceBuildingWithId(buildingId, buildingType, team, coord);
            if (result == null)
            {
                Debug.LogWarning($"[Network] SpawnBuildingClientRpc: PlaceBuildingWithId 실패. Id={buildingId}");
                return;
            }

            Debug.Log($"[Network] 클라이언트: 건물 재생성 완료. Id={result.Id}, Type={buildingType}, Team={team}");
        }

        // ====================================================================
        // 실패 피드백 — 요청자 클라이언트에게만 전송
        // ====================================================================

        /// <summary>
        /// 건물 배치 실패를 요청한 클라이언트에게만 알림.
        /// </summary>
        /// <param name="targetClientId">알림 대상 ClientId</param>
        /// <param name="reason">실패 이유 (로그용)</param>
        private void SendBuildFailed(ulong targetClientId, string reason)
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };
            BuildFailedClientRpc(reason, clientRpcParams);
        }

        /// <summary>
        /// 건물 배치 실패 알림. 요청한 클라이언트에게만 전송.
        /// 현재는 로그만 출력. 향후 UI 피드백(토스트 메시지 등)으로 확장 가능.
        /// </summary>
        /// <param name="reason">실패 이유</param>
        /// <param name="clientRpcParams">대상 클라이언트 파라미터</param>
        [ClientRpc]
        private void BuildFailedClientRpc(string reason, ClientRpcParams clientRpcParams = default)
        {
            Debug.LogWarning($"[Network] 건물 배치 실패: {reason}");
            // TODO: UI 피드백 — 토스트 메시지, 버튼 흔들기 효과 등
        }

        // ====================================================================
        // 내부 유틸리티
        // ====================================================================

        /// <summary>
        /// 건물 타입별 골드 비용 반환.
        /// BuildingPlacementUI.GetBuildingCost와 동일한 로직을 서버에서도 검증.
        /// GameBootstrapper.GetConfig()로 ScriptableObject 값을 참조.
        /// </summary>
        /// <param name="type">건물 종류</param>
        /// <returns>골드 비용. Castle/알 수 없는 타입은 0.</returns>
        private int GetBuildingCost(BuildingType type)
        {
            GameConfig config = _bootstrapper != null ? _bootstrapper.GetConfig() : null;

            if (config == null)
            {
                // GameConfig를 찾지 못한 경우 하드코딩 폴백 (Inspector 미연결 안전 처리)
                Debug.LogWarning("[Network] NetworkBuildingController: GameConfig를 찾을 수 없어 기본 비용 사용.");
                return type switch
                {
                    BuildingType.Barracks => 100,
                    BuildingType.MiningPost => 50,
                    _ => 0
                };
            }

            return type switch
            {
                BuildingType.Barracks => config.BarracksCost,
                BuildingType.MiningPost => config.MiningPostCost,
                _ => 0
            };
        }
    }
}
