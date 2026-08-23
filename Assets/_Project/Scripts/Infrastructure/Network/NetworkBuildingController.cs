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

        /// <summary> 게임 서비스 참조. UseCase 접근에 사용. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화. </summary>
        private IGameServices _services;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 IGameServices 획득.
        /// GameServicesLocator에서 꺼내므로 Bootstrap에 직접 의존하지 않는다.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _services = GameServicesLocator.Current;
            if (_services == null)
            {
                // 스폰은 계속되지만(요청 전이라 아직 기능이 죽지는 않았다) 이후 모든 건물 RPC 가 같은 자리에서 죽는다.
                // NGO 스폰 타이밍은 회선 상태에 좌우돼 플레이어 기기에서만 어긋날 수 있고 통지 경로가 없다 → 운영.
                GameLog.Ops.Warn(LogEvent.NetworkControllerSpawnedWithoutGameServices, "Network", nameof(NetworkBuildingController),
                                 "스폰 시점에 GameServicesLocator 에서 IGameServices 를 얻지 못했다");
            }

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "스폰 완료",
                             $"IsServer={IsServer}");
        }

        // ====================================================================
        // 외부 호출용 래퍼 메서드 — UI 등 Presentation 레이어에서 사용
        // ====================================================================
        //
        // 왜 래퍼 메서드인가?
        //   UI(Presentation)가 ServerRpc 메서드(*ServerRpc 접미사)를 직접 호출하면
        //   "내가 어떤 RPC를 써야 하는지"까지 알아야 하고 NGO API에 직접 결합된다.
        //   래퍼 메서드는 UI에게 일반 메서드 호출 형태를 제공하여 결합도를 낮춘다.
        //   내부 구현이 ServerRpc인지, 다른 메커니즘인지 UI가 신경 쓰지 않아도 된다.
        //
        // ====================================================================

        /// <summary>
        /// 건물 배치 요청 — UI 래퍼.
        /// 실제로는 RequestBuildServerRpc로 위임된다.
        /// </summary>
        /// <param name="buildingType">배치할 건물 종류.</param>
        /// <param name="team">요청 팀.</param>
        /// <param name="q">배치 좌표 Q.</param>
        /// <param name="r">배치 좌표 R.</param>
        public void RequestBuild(BuildingType buildingType, TeamId team, int q, int r)
        {
            RequestBuildServerRpc((int)buildingType, (int)team, q, r);
        }

        /// <summary>
        /// 건물 업그레이드 요청 — UI 래퍼.
        /// 실제로는 RequestUpgradeServerRpc로 위임된다.
        /// </summary>
        /// <param name="buildingId">업그레이드할 건물 Id.</param>
        public void RequestUpgrade(int buildingId)
        {
            RequestUpgradeServerRpc(buildingId);
        }

        /// <summary>
        /// 건물 철거 요청 — UI 래퍼.
        /// 실제로는 RequestDemolishServerRpc로 위임된다.
        /// </summary>
        /// <param name="buildingId">철거할 건물 Id.</param>
        public void RequestDemolish(int buildingId)
        {
            RequestDemolishServerRpc(buildingId);
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

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "건물 배치 요청 수신",
                             $"ClientId={senderClientId}, BuildingType={buildingTypeInt}, Team={teamIndex}, Q={q}, R={r}");

            // ----------------------------------------------------------------
            // 1. 부트스트래퍼 및 UseCase 존재 확인
            // ----------------------------------------------------------------
            // _services는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "RequestBuildServerRpc — IGameServices 를 얻지 못했다. 이후 모든 건물 요청이 같은 자리에서 죽는다",
                                  $"Request=Build, ClientId={senderClientId}");
                SendBuildFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _services.GetBuildingPlacement();
            ResourceUseCase resource = _services.GetResource();

            if (buildingPlacement == null || resource == null)
            {
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "RequestBuildServerRpc — UseCase 가 null 이다. 맵이 아직 로드되지 않았을 수 있다",
                                  $"Request=Build, ClientId={senderClientId}");
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
                // 요청만 거부하고 게임은 계속된다 → Warn. 다만 정상 클라이언트는 보낼 수 없는 요청이다.
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkBuildingController),
                                 "건물 배치 거부 — 요청 팀이 발신자의 팀과 다르다",
                                 $"Request=Build, Reason=Team, ClientId={senderClientId}, RequestedTeam={team}, ExpectedTeam={expectedTeam}");
                SendBuildFailed(senderClientId, "팀 불일치");
                return;
            }

            // ----------------------------------------------------------------
            // 4. 골드 충분 여부 확인
            // ----------------------------------------------------------------
            int cost = GetBuildingCost(buildingType, team);
            if (!resource.CanAfford(team, cost))
{
                // BuildingPlacementUI 가 CanAfford 로 이미 막았는데 서버가 거부했다 = 클라·서버 골드 상태 불일치.
                GameLog.Ops.Warn(LogEvent.ServerRejectedInsufficientResource, "Network", nameof(NetworkBuildingController),
                                 "건물 배치 거부 — 골드 부족(클라이언트가 이미 막았어야 한다)",
                                 $"Request=Build, Reason=Gold, Team={team}, Required={cost}, Current={resource.GetGold(team)}");
                SendBuildFailed(senderClientId, "골드 부족");
                return;
            }

            // ----------------------------------------------------------------
            // 5. 서버에서 UseCase 실행 (검증 포함)
            //    PlaceBuilding 내부에서 타일 존재·IsWalkable·팀 소유 검증 실행.
            // ----------------------------------------------------------------
            // 골드 차감을 UseCase 실행 전에 수행 (UseCase가 검증을 포함하므로 실패 시 환불 필요)
            // 실제로는 UseCase 실행 후 차감이 더 안전하지만 PlaceBuilding이 실패 시 null 반환
            // 종족별 건물 HP가 다르므로 GameRaceContext에서 해당 팀의 종족을 조회하여 전달
            RaceId race = team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            BuildingData placed = buildingPlacement.PlaceBuilding(buildingType, team, coord, race);
            if (placed == null)
            {
                // 검증을 통과한 뒤 실행이 실패했다 = 맵 상태 불일치.
                // "배치 위치 오류"는 BuildFailedClientRpc 의 토스트 매핑에 없어 플레이어에게 통지되지 않는다.
                GameLog.Ops.Warn(LogEvent.ServerActionExecutionFailed, "Network", nameof(NetworkBuildingController),
                                 "서버 측 건물 배치 실행 실패 — 검증 통과 후 실패했다(맵 상태 불일치)",
                                 $"Request=Build, BuildingType={buildingType}, Team={team}, Q={q}, R={r}");
                SendBuildFailed(senderClientId, "배치 위치 오류");
                return;
            }

            // ----------------------------------------------------------------
            // 6. 골드 차감 (서버에서만 실행)
            // ----------------------------------------------------------------
            resource.SpendGold(team, cost);
            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "서버: 건물 배치 성공",
                             $"BuildingId={placed.Id}, BuildingType={buildingType}, Team={team}, Q={q}, R={r}, SpentGold={cost}");

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

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "SpawnBuildingClientRpc 수신",
                             $"BuildingId={buildingId}, BuildingType={buildingTypeInt}, Team={teamIndex}, Q={q}, R={r}");

            // UseCase 접근
            // _services는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "SpawnBuildingClientRpc — IGameServices 를 얻지 못했다. 이 건물이 클라이언트 화면에 나타나지 않는다",
                                  $"Request=SpawnBuilding, BuildingId={buildingId}");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _services.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "SpawnBuildingClientRpc — BuildingPlacementUseCase 가 null 이다",
                                  $"Request=SpawnBuilding, BuildingId={buildingId}");
                return;
            }

            // 파라미터 변환
            BuildingType buildingType = (BuildingType)buildingTypeInt;
            TeamId team = (TeamId)teamIndex;
            HexCoord coord = new HexCoord(q, r);

            // 서버와 동일한 Id로 BuildingData 재생성 + 이벤트 발행
            // PlaceBuildingWithId 내부에서 GameEvents.OnBuildingPlaced 발행
            // → BuildingFactory가 프리팹을 생성함
            // 종족별 건물 HP가 다르므로 GameRaceContext에서 해당 팀의 종족을 조회하여 전달
            RaceId race = team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            BuildingData result = buildingPlacement.PlaceBuildingWithId(buildingId, buildingType, team, coord, race);
            if (result == null)
            {
                // ⚠️ 레벨 승격(Warn → Error, LogAudit.md §3-2 :272).
                //    재시도 경로가 없다. 이 건물은 이 클라이언트에서 영구히 누락된다
                //    → "복구되었나?" 아니오 → Error.
                GameLog.Ops.Error(LogEvent.ClientStateSyncApplyFailed, "Network", nameof(NetworkBuildingController),
                                  "SpawnBuildingClientRpc — PlaceBuildingWithId 실패. 이 건물은 클라이언트에서 영구히 누락된다",
                                  $"Request=SpawnBuilding, BuildingId={buildingId}");
                return;
            }

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "클라이언트: 건물 재생성 완료",
                             $"BuildingId={result.Id}, BuildingType={buildingType}, Team={team}");
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
        /// 클라이언트는 GameEvents.OnToastRequested 이벤트를 발행하여 ToastUI에 표시한다.
        ///
        /// reason 문자열을 ToastKey로 매핑하여 토스트 표시:
        ///   "골드 부족" → ToastKey.GoldInsufficient
        ///   그 외(맵 로드 중/팀 불일치/배치 위치 오류/서버 초기화 오류) → 키가 없으므로 로그만 남기고 표시 생략.
        ///   (각 사유별 토스트 키가 추가되면 매핑을 확장한다.)
        /// </summary>
        /// <param name="reason">실패 이유</param>
        /// <param name="clientRpcParams">대상 클라이언트 파라미터</param>
        [ClientRpc]
        private void BuildFailedClientRpc(string reason, ClientRpcParams clientRpcParams = default)
        {
            // 개발인 이유(원칙 1): 같은 거부를 서버 쪽 RPC 들이 이미 더 상세히 남긴다.
            // 양쪽을 운영으로 두면 한 사건이 두 번 집계된다.
            GameLog.Dev.Warn("Network", nameof(NetworkBuildingController), "클라이언트: 건물 요청 실패 알림 수신",
                             $"Reason={reason}");

            // reason → ToastKey 매핑. 매핑이 정의된 사유만 토스트 발행.
            if (reason == "골드 부족")
                GameEvents.OnToastRequested.OnNext(ToastKey.GoldInsufficient);
            // 향후 ToastKey가 늘어나면 여기에 매핑을 추가한다.
        }

        // ====================================================================
        // 내부 유틸리티
        // ====================================================================

        /// <summary>
        /// 건물 타입별 골드 비용 반환.
        /// BuildingPlacementUI.GetBuildingCost와 동일한 로직을 서버에서도 검증.
        /// </summary>
        /// <param name="type">건물 종류</param>
        /// <param name="team">요청한 팀 (종족 조회를 위해 필요)</param>
        /// <returns>골드 비용.</returns>
        private int GetBuildingCost(BuildingType type, TeamId team)
        {
            // 해당 팀의 종족 조회
            RaceId race = team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            // BuildingStats 도메인 클래스를 통해 종족별/건물별 비용 조회
            return BuildingStats.GetGoldCost(type, race);
        }

        // ====================================================================
        // 업그레이드 — ServerRpc / ClientRpc
        // ====================================================================

        /// <summary>
        /// 건물 업그레이드 요청. 클라이언트의 업그레이드 버튼 클릭 시 호출.
        /// 서버에서 소유권·골드를 다시 검증한 후 UpgradeBuilding 실행,
        /// 성공 시 UpgradeBuildingClientRpc로 모든 클라이언트에 동기화.
        /// </summary>
        /// <param name="buildingId">업그레이드 대상 건물 Id.</param>
        /// <param name="rpcParams">서버 RPC 파라미터(발신자 ClientId 포함).</param>
        [ServerRpc(RequireOwnership = false)]
        public void RequestUpgradeServerRpc(int buildingId, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "건물 업그레이드 요청 수신",
                             $"ClientId={senderClientId}, BuildingId={buildingId}");

            // _services는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "RequestUpgradeServerRpc — IGameServices 를 얻지 못했다. 이후 모든 건물 요청이 같은 자리에서 죽는다",
                                  $"Request=Upgrade, ClientId={senderClientId}, BuildingId={buildingId}");
                SendBuildFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _services.GetBuildingPlacement();
            ResourceUseCase resource = _services.GetResource();

            if (buildingPlacement == null || resource == null)
            {
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "RequestUpgradeServerRpc — UseCase 가 null 이다",
                                  $"Request=Upgrade, ClientId={senderClientId}, BuildingId={buildingId}");
                SendBuildFailed(senderClientId, "맵 로드 중");
                return;
            }

            // 1) 건물 조회 + 존재 확인
            BuildingData old = buildingPlacement.GetBuilding(buildingId);
            if (old == null)
            {
                // 클라이언트는 존재하는 건물의 패널만 열 수 있다 → 서버에 없다는 것은 상태 불일치다.
                GameLog.Ops.Warn(LogEvent.ServerRejectedTargetNotFound, "Network", nameof(NetworkBuildingController),
                                 "업그레이드 거부 — 대상 건물이 서버에 없다",
                                 $"Request=Upgrade, ClientId={senderClientId}, BuildingId={buildingId}");
                SendBuildFailed(senderClientId, "건물 없음");
                return;
            }

            // 2) 소유권 검증 (발신자 ClientId → 기대 팀 매핑과 건물 팀이 일치해야 함)
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (old.Team != expectedTeam)
            {
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkBuildingController),
                                 "업그레이드 거부 — 건물 소유 팀과 발신자의 팀이 다르다",
                                 $"Request=Upgrade, Reason=Ownership, ClientId={senderClientId}, BuildingId={buildingId}, BuildingTeam={old.Team}, ExpectedTeam={expectedTeam}");
                SendBuildFailed(senderClientId, "소유권 불일치");
                return;
            }

            // 3) 업그레이드 가능 여부
            BuildingType? nextOpt = BuildingTypeHelper.GetNextStage(old.Type);
            if (!nextOpt.HasValue)
            {
                // UI 가 최고 단계에서 버튼을 숨기고 클릭 시 CanUpgrade 로 재확인까지 한다 → 여기 도달하면 불일치다.
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkBuildingController),
                                 "업그레이드 거부 — 이미 최고 단계라 다음 단계가 없다",
                                 $"Request=Upgrade, Reason=MaxStage, ClientId={senderClientId}, BuildingId={buildingId}, BuildingType={old.Type}");
                SendBuildFailed(senderClientId, "최고 단계");
                return;
            }

            // 4) 골드 검증
            int upgradeCost = BuildingStats.GetUpgradeCost(old.Type);
            if (!resource.CanAfford(old.Team, upgradeCost))
            {
                // LogRules.md 1.2 조합표가 이 자리를 Warn + 운영 예시로 그대로 들었다
                // (클라이언트가 CanAfford 로 이미 막았는데 서버가 거부 = 클라·서버 골드 상태 불일치).
                GameLog.Ops.Warn(LogEvent.ServerRejectedInsufficientResource, "Network", nameof(NetworkBuildingController),
                                 "업그레이드 거부 — 골드 부족(클라이언트가 이미 막았어야 한다)",
                                 $"Request=Upgrade, Reason=Gold, Team={old.Team}, Required={upgradeCost}, Current={resource.GetGold(old.Team)}");
                SendBuildFailed(senderClientId, "골드 부족");
                return;
            }

            // 5) 서버 측 업그레이드 실행 (BuildingData 교체 + 이벤트 발행)
            RaceId race = old.Team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            BuildingData newBuilding = buildingPlacement.UpgradeBuilding(buildingId, race);
            if (newBuilding == null)
            {
                // 검증 4단계를 모두 통과한 뒤 실행이 실패했다 = 서버 내부 상태 이상.
                GameLog.Ops.Warn(LogEvent.ServerActionExecutionFailed, "Network", nameof(NetworkBuildingController),
                                 "서버 업그레이드 실행 실패 — 검증을 모두 통과한 뒤 실패했다",
                                 $"Request=Upgrade, BuildingId={buildingId}, BuildingType={old.Type}, Team={old.Team}");
                SendBuildFailed(senderClientId, "업그레이드 실패");
                return;
            }

            // 6) 골드 차감 (서버에서만)
            resource.SpendGold(old.Team, upgradeCost);

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "서버: 건물 업그레이드 성공",
                             $"OldBuildingId={buildingId}, NewBuildingId={newBuilding.Id}, NewBuildingType={newBuilding.Type}");

            // 7) 모든 클라이언트에 동기화 명령 전파
            UpgradeBuildingClientRpc(
                buildingId,
                newBuilding.Id,
                (int)newBuilding.Type,
                (int)newBuilding.Team,
                newBuilding.Position.Q,
                newBuilding.Position.R);
        }

        /// <summary>
        /// 서버 업그레이드 성공 시 모든 클라이언트에 동기화.
        /// 서버는 이미 UpgradeBuilding으로 처리했으므로 IsServer 분기에서 건너뜀.
        /// </summary>
        [ClientRpc]
        private void UpgradeBuildingClientRpc(
            int oldBuildingId,
            int newBuildingId,
            int newTypeInt,
            int teamIndex,
            int q,
            int r)
        {
            // 서버는 이미 처리됨 — 중복 적용 방지
            if (IsServer) return;

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "UpgradeBuildingClientRpc 수신",
                             $"OldBuildingId={oldBuildingId}, NewBuildingId={newBuildingId}, NewBuildingType={newTypeInt}");

            // _services는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "UpgradeBuildingClientRpc — IGameServices 를 얻지 못했다. 업그레이드가 클라이언트에 반영되지 않는다",
                                  $"Request=UpgradeSync, OldBuildingId={oldBuildingId}, NewBuildingId={newBuildingId}");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _services.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "UpgradeBuildingClientRpc — BuildingPlacementUseCase 가 null 이다",
                                  $"Request=UpgradeSync, OldBuildingId={oldBuildingId}, NewBuildingId={newBuildingId}");
                return;
            }

            BuildingType newType = (BuildingType)newTypeInt;
            TeamId team = (TeamId)teamIndex;

            // 종족별 HP 결정에 사용
            RaceId race = team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            // 서버와 동일한 Id로 새 BuildingData 재생성 + 이벤트 발행
            // (q,r 은 디버깅/검증용 — UseCase 내부에서는 기존 BuildingData의 Position을 그대로 사용)
            BuildingData result = buildingPlacement.UpgradeBuildingWithId(oldBuildingId, newBuildingId, newType, race);
            if (result == null)
            {
                // ⚠️ 레벨 승격(Warn → Error, LogAudit.md §3-2 :490).
                //    :272 와 동일 — 재시도가 없어 이 클라이언트의 건물 상태가 영구히 갈린다.
                GameLog.Ops.Error(LogEvent.ClientStateSyncApplyFailed, "Network", nameof(NetworkBuildingController),
                                  "UpgradeBuildingClientRpc — UpgradeBuildingWithId 실패. 클라이언트 건물 상태가 영구히 갈린다",
                                  $"Request=UpgradeSync, OldBuildingId={oldBuildingId}, NewBuildingId={newBuildingId}");
                return;
            }

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "클라이언트: 업그레이드 동기화 완료",
                             $"NewBuildingId={result.Id}, NewBuildingType={newType}");
        }

        // ====================================================================
        // 철거 — ServerRpc / ClientRpc
        // ====================================================================

        /// <summary>
        /// 건물 철거 요청. 클라이언트의 철거 버튼 클릭 시 호출.
        ///
        /// 서버 검증 순서:
        ///   1) 건물 존재 확인
        ///   2) Castle(본기지) 철거 불가 — Castle은 절대 철거할 수 없음
        ///   3) 소유권 확인 — 요청한 클라이언트가 해당 건물의 팀 소유주여야 함
        /// 검증 통과 시:
        ///   - 생산 건물이면 CancelAllQueue → 생산 큐 전체 취소 및 골드 환불
        ///   - 건설 비용 50% 골드 환불
        ///   - RemoveBuilding → 건물 도메인 상태 제거 + 이벤트 발행 (프리팹 제거)
        ///   - DemolishBuildingClientRpc → 모든 클라이언트에 동기화
        ///
        /// 근거: RequestBuildServerRpc / RequestUpgradeServerRpc 패턴과 동일한 구조.
        /// </summary>
        /// <param name="buildingId">철거할 건물 Id</param>
        /// <param name="rpcParams">서버 RPC 파라미터 (발신자 ClientId 포함)</param>
        [ServerRpc(RequireOwnership = false)]
        public void RequestDemolishServerRpc(int buildingId, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "건물 철거 요청 수신",
                             $"ClientId={senderClientId}, BuildingId={buildingId}");

            // _services는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "RequestDemolishServerRpc — IGameServices 를 얻지 못했다. 이후 모든 건물 요청이 같은 자리에서 죽는다",
                                  $"Request=Demolish, ClientId={senderClientId}, BuildingId={buildingId}");
                SendBuildFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _services.GetBuildingPlacement();
            ResourceUseCase resource = _services.GetResource();

            if (buildingPlacement == null || resource == null)
            {
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "RequestDemolishServerRpc — UseCase 가 null 이다",
                                  $"Request=Demolish, ClientId={senderClientId}, BuildingId={buildingId}");
                SendBuildFailed(senderClientId, "맵 로드 중");
                return;
            }

            // 1) 건물 존재 확인
            BuildingData building = buildingPlacement.GetBuilding(buildingId);
            if (building == null)
            {
                // 업그레이드 :385 와 동일 근거 — 클라이언트는 존재하는 건물의 패널만 열 수 있다.
                GameLog.Ops.Warn(LogEvent.ServerRejectedTargetNotFound, "Network", nameof(NetworkBuildingController),
                                 "철거 거부 — 대상 건물이 서버에 없다",
                                 $"Request=Demolish, ClientId={senderClientId}, BuildingId={buildingId}");
                SendBuildFailed(senderClientId, "건물 없음");
                return;
            }

            // 2) Castle(본기지) 철거 불가 — 본기지는 절대 철거할 수 없음
            if (building.Type == BuildingType.Castle)
            {
                // InputHandler 가 Castle 클릭 시 패널 자체를 열지 않는다(CanShowActionPanel == false)
                // → 여기 도달했다는 것은 정상 클라이언트가 보낼 수 없는 요청이라는 뜻이다.
                // GameSystemRules_Buildings.md 건물 철거 시스템 규칙 1(철거 가능 범위)의 서버 측 강제 지점.
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkBuildingController),
                                 "철거 거부 — Castle 은 철거할 수 없다",
                                 $"Request=Demolish, Reason=CastleProtected, ClientId={senderClientId}, BuildingId={buildingId}");
                SendBuildFailed(senderClientId, "철거 불가");
                return;
            }

            // 3) 소유권 검증 (발신자 ClientId → 기대 팀 매핑과 건물 팀이 일치해야 함)
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (building.Team != expectedTeam)
            {
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkBuildingController),
                                 "철거 거부 — 건물 소유 팀과 발신자의 팀이 다르다",
                                 $"Request=Demolish, Reason=Ownership, ClientId={senderClientId}, BuildingId={buildingId}, BuildingTeam={building.Team}, ExpectedTeam={expectedTeam}");
                SendBuildFailed(senderClientId, "소유권 불일치");
                return;
            }

            // 4) 생산 건물이면 생산 큐 전체 취소 + 골드 환불 (Rule 5: 이미 차감된 항목 전액 환불)
            // CancelAllQueue 내부에서 골드 환불과 이벤트 발행이 함께 처리된다.
            if (BuildingTypeHelper.IsProductionBuilding(building.Type))
            {
                UnitProductionUseCase production = _services.GetUnitProduction();
                if (production != null)
                    production.CancelAllQueue(buildingId);
            }

            // 5) 건설 비용 50% 환불 (건설 비용 누적합의 절반 반환)
            // 2단계 건물이면 1단계 건설비 + 업그레이드 비용의 합산의 50%를 환불한다.
            RaceId race = building.Team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            int totalInvested = BuildingStats.GetTotalInvestedCost(building.Type, race);
            int refund = totalInvested / 2;
            resource.AddGold(building.Team, refund);

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "서버: 건물 철거 처리",
                             $"BuildingId={buildingId}, BuildingType={building.Type}, Team={building.Team}, Refund={refund}");

            // 6) 서버 도메인 상태 제거
            // DemolishBuilding = OnBuildingDied 발행(서버 BuildingFactory가 GO 제거) + RemoveBuilding(도메인 딕셔너리 제거 + 타일 복구)
            buildingPlacement.DemolishBuilding(buildingId);

            // 7) 모든 클라이언트에 동기화 명령 전파
            DemolishBuildingClientRpc(buildingId);
        }

        /// <summary>
        /// 서버 철거 처리 완료 후 모든 클라이언트에 도메인 상태 동기화.
        /// 서버는 이미 RemoveBuilding()으로 처리했으므로 IsServer 분기에서 건너뜀.
        /// 클라이언트는 RemoveBuilding()을 호출하여 동일한 건물을 도메인에서 제거한다.
        /// 이벤트 발행으로 BuildingFactory가 프리팹을 제거한다.
        ///
        /// 근거: SpawnBuildingClientRpc / UpgradeBuildingClientRpc 패턴과 동일한 구조.
        /// </summary>
        /// <param name="buildingId">철거된 건물 Id</param>
        [ClientRpc]
        private void DemolishBuildingClientRpc(int buildingId)
        {
            // 서버는 이미 처리됨 — 중복 적용 방지
            if (IsServer) return;

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "DemolishBuildingClientRpc 수신",
                             $"BuildingId={buildingId}");

            // _services는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "DemolishBuildingClientRpc — IGameServices 를 얻지 못했다. 철거가 클라이언트에 반영되지 않는다",
                                  $"Request=DemolishSync, BuildingId={buildingId}");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _services.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkBuildingController),
                                  "DemolishBuildingClientRpc — BuildingPlacementUseCase 가 null 이다",
                                  $"Request=DemolishSync, BuildingId={buildingId}");
                return;
            }

            // 클라이언트 도메인 상태 제거
            // DemolishBuilding = OnBuildingDied 발행(BuildingFactory가 GO 제거) + RemoveBuilding(도메인 딕셔너리 제거 + 타일 복구)
            buildingPlacement.DemolishBuilding(buildingId);

            GameLog.Dev.Info("Network", nameof(NetworkBuildingController), "클라이언트: 건물 철거 동기화 완료",
                             $"BuildingId={buildingId}");
        }
}

}
