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

            Debug.Log($"[Network] 건물 배치 요청 수신. ClientId={senderClientId}, Type={buildingTypeInt}, Team={teamIndex}, Q={q}, R={r}");

            // ----------------------------------------------------------------
            // 1. 부트스트래퍼 및 UseCase 존재 확인
            // ----------------------------------------------------------------
            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
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
            int cost = GetBuildingCost(buildingType, team);
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
            // 종족별 건물 HP가 다르므로 GameRaceContext에서 해당 팀의 종족을 조회하여 전달
            RaceId race = team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            BuildingData placed = buildingPlacement.PlaceBuilding(buildingType, team, coord, race);
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
            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
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
            // 종족별 건물 HP가 다르므로 GameRaceContext에서 해당 팀의 종족을 조회하여 전달
            RaceId race = team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            BuildingData result = buildingPlacement.PlaceBuildingWithId(buildingId, buildingType, team, coord, race);
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
        /// 클라이언트는 GameEvents.OnToastRequested 이벤트를 발행하여 ToastUI에 표시한다.
        ///
        /// [2026-05-20] reason 문자열을 ToastKey로 매핑하여 토스트 표시:
        ///   "골드 부족" → ToastKey.GoldInsufficient
        ///   그 외(맵 로드 중/팀 불일치/배치 위치 오류/서버 초기화 오류) → 키가 없으므로 로그만 남기고 표시 생략.
        ///   (각 사유별 토스트 키가 추가되면 매핑을 확장한다.)
        /// </summary>
        /// <param name="reason">실패 이유</param>
        /// <param name="clientRpcParams">대상 클라이언트 파라미터</param>
        [ClientRpc]
        private void BuildFailedClientRpc(string reason, ClientRpcParams clientRpcParams = default)
        {
            Debug.LogWarning($"[Network] 건물 배치 실패: {reason}");

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

            Debug.Log($"[Network] 건물 업그레이드 요청 수신. ClientId={senderClientId}, BuildingId={buildingId}");

            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] RequestUpgradeServerRpc: GameBootstrapper를 찾을 수 없습니다.");
                SendBuildFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _bootstrapper.GetBuildingPlacement();
            ResourceUseCase resource = _bootstrapper.GetResource();

            if (buildingPlacement == null || resource == null)
            {
                Debug.LogError("[Network] RequestUpgradeServerRpc: UseCase가 null입니다.");
                SendBuildFailed(senderClientId, "맵 로드 중");
                return;
            }

            // 1) 건물 조회 + 존재 확인
            BuildingData old = buildingPlacement.GetBuilding(buildingId);
            if (old == null)
            {
                Debug.LogWarning($"[Network] 업그레이드 대상 건물 없음. Id={buildingId}");
                SendBuildFailed(senderClientId, "건물 없음");
                return;
            }

            // 2) 소유권 검증 (발신자 ClientId → 기대 팀 매핑과 건물 팀이 일치해야 함)
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (old.Team != expectedTeam)
            {
                Debug.LogWarning($"[Network] 업그레이드 소유권 불일치. 발신자={senderClientId}, 건물팀={old.Team}, 기대팀={expectedTeam}");
                SendBuildFailed(senderClientId, "소유권 불일치");
                return;
            }

            // 3) 업그레이드 가능 여부
            BuildingType? nextOpt = BuildingTypeHelper.GetNextStage(old.Type);
            if (!nextOpt.HasValue)
            {
                Debug.LogWarning($"[Network] 업그레이드 불가 (최고 단계). Type={old.Type}");
                SendBuildFailed(senderClientId, "최고 단계");
                return;
            }

            // 4) 골드 검증
            int upgradeCost = BuildingStats.GetUpgradeCost(old.Type);
            if (!resource.CanAfford(old.Team, upgradeCost))
            {
                Debug.LogWarning($"[Network] 업그레이드 골드 부족. 팀={old.Team}, 비용={upgradeCost}, 현재={resource.GetGold(old.Team)}");
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
                Debug.LogWarning($"[Network] 서버 업그레이드 실행 실패. Id={buildingId}");
                SendBuildFailed(senderClientId, "업그레이드 실패");
                return;
            }

            // 6) 골드 차감 (서버에서만)
            resource.SpendGold(old.Team, upgradeCost);

            Debug.Log($"[Network] 서버: 업그레이드 성공. oldId={buildingId}, newId={newBuilding.Id}, newType={newBuilding.Type}");

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

            Debug.Log($"[Network] UpgradeBuildingClientRpc 수신. oldId={oldBuildingId}, newId={newBuildingId}, newType={newTypeInt}");

            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] UpgradeBuildingClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _bootstrapper.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                Debug.LogError("[Network] UpgradeBuildingClientRpc: BuildingPlacementUseCase가 null입니다.");
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
                Debug.LogWarning($"[Network] UpgradeBuildingClientRpc: UpgradeBuildingWithId 실패. oldId={oldBuildingId}");
                return;
            }

            Debug.Log($"[Network] 클라이언트: 업그레이드 동기화 완료. newId={result.Id}, newType={newType}");
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

            Debug.Log($"[Network] 건물 철거 요청 수신. ClientId={senderClientId}, BuildingId={buildingId}");

            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] RequestDemolishServerRpc: GameBootstrapper를 찾을 수 없습니다.");
                SendBuildFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _bootstrapper.GetBuildingPlacement();
            ResourceUseCase resource = _bootstrapper.GetResource();

            if (buildingPlacement == null || resource == null)
            {
                Debug.LogError("[Network] RequestDemolishServerRpc: UseCase가 null입니다.");
                SendBuildFailed(senderClientId, "맵 로드 중");
                return;
            }

            // 1) 건물 존재 확인
            BuildingData building = buildingPlacement.GetBuilding(buildingId);
            if (building == null)
            {
                Debug.LogWarning($"[Network] 철거 대상 건물 없음. Id={buildingId}");
                SendBuildFailed(senderClientId, "건물 없음");
                return;
            }

            // 2) Castle(본기지) 철거 불가 — 본기지는 절대 철거할 수 없음
            if (building.Type == BuildingType.Castle)
            {
                Debug.LogWarning($"[Network] Castle은 철거할 수 없습니다. Id={buildingId}");
                SendBuildFailed(senderClientId, "철거 불가");
                return;
            }

            // 3) 소유권 검증 (발신자 ClientId → 기대 팀 매핑과 건물 팀이 일치해야 함)
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (building.Team != expectedTeam)
            {
                Debug.LogWarning($"[Network] 철거 소유권 불일치. 발신자={senderClientId}, 건물팀={building.Team}, 기대팀={expectedTeam}");
                SendBuildFailed(senderClientId, "소유권 불일치");
                return;
            }

            // 4) 생산 건물이면 생산 큐 전체 취소 + 골드 환불 (Rule 5: 이미 차감된 항목 전액 환불)
            // CancelAllQueue 내부에서 골드 환불과 이벤트 발행이 함께 처리된다.
            if (BuildingTypeHelper.IsProductionBuilding(building.Type))
            {
                UnitProductionUseCase production = _bootstrapper.GetUnitProduction();
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

            Debug.Log($"[Network] 서버: 건물 철거 처리. Id={buildingId}, Type={building.Type}, Team={building.Team}, 환불={refund}");

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

            Debug.Log($"[Network] DemolishBuildingClientRpc 수신. Id={buildingId}");

            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시.
            // 누락 시 메서드는 빠르게 종료 — 보호적 재탐색은 하지 않음.
            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] DemolishBuildingClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            BuildingPlacementUseCase buildingPlacement = _bootstrapper.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                Debug.LogError("[Network] DemolishBuildingClientRpc: BuildingPlacementUseCase가 null입니다.");
                return;
            }

            // 클라이언트 도메인 상태 제거
            // DemolishBuilding = OnBuildingDied 발행(BuildingFactory가 GO 제거) + RemoveBuilding(도메인 딕셔너리 제거 + 타일 복구)
            buildingPlacement.DemolishBuilding(buildingId);

            Debug.Log($"[Network] 클라이언트: 건물 철거 동기화 완료. Id={buildingId}");
        }
}

}
