// ============================================================================
// NetworkUpgradeController.cs
// 네트워크 모드에서 연구소 유닛 강화(연구) 요청을 서버로 중계하고,
// 완료된 강화 레벨을 양 클라이언트에 동기화한다.
//
// 흐름(GameSystemRules_Upgrade.md 규칙 8·9):
//   1. 클라이언트 UI → RequestResearch(group, stat, buildingId, team)
//        → RequestResearchServerRpc(...)
//   2. 서버: 팀 소유권·최대레벨·진행중·골드 검증 → UnitUpgradeUseCase.TryStartResearch()
//        - 실패: 요청 클라에게만 ResearchFailedClientRpc(토스트).
//        - 성공: 요청 클라에게만 ResearchStartedClientRpc(진행 UI 표시 — 진행 상태는 소유 클라만).
//   3. 서버: 매 서버 틱 UnitUpgradeUseCase.TickResearch() (NetworkCombatController가 구동)
//        → 완료 시 OnResearchCompleted 훅 발화 →
//   4. 서버: ResearchLevelClientRpc(team, group, stat, level)를 **양 클라 브로드캐스트**
//        (완료 효과는 양쪽에 적용되어야 한다 — 상대 강화 유닛이 내 화면에서도 올바른 데미지를 내야 하므로).
//      클라이언트는 UnitUpgradeUseCase.SetLevel로 레벨을 반영.
//
// 배치(사용자 Unity 작업):
//   씬에 빈 GameObject "NetworkUpgradeController" 생성 → NetworkObject + 이 스크립트 부착.
//   NetworkManager의 씬 오브젝트(자동 스폰)로 설정. GameBootstrapper._networkUpgradeController에 연결.
//
// 아키텍처: Infrastructure 레이어 — NetworkBehaviour 허용. RPC 접미사 ServerRpc/ClientRpc 준수.
//   Application(UnitUpgradeUseCase)은 Netcode를 모르며, 이 컨트롤러가 구동/브로드캐스트한다(의존성 역전).
// ============================================================================

using System;
using Unity.Netcode;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 연구소 유닛 강화(연구) 네트워크 컨트롤러.
    /// 클라이언트 연구 요청을 서버에서 검증·착수하고, 완료 레벨을 양 클라이언트에 동기화한다.
    /// </summary>
    public class NetworkUpgradeController : NetworkBehaviour
    {
        /// <summary> 게임 서비스 참조. UseCase 접근용(IGameServices로 추상화). </summary>
        private IGameServices _services;

        // OnResearchCompleted 구독 핸들러 참조(해제용). 서버에서만 구독한다.
        private Action<TeamId, UpgradeGroup, UnitUpgradeStat, int> _completedHandler;

        // ====================================================================
        // 생명주기
        // ====================================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // 스폰 시점에 1차 캐시. 아직 GameServicesLocator 등록 전이면 null 일 수 있는데,
            // 그 경우 이후 RPC 처리에서 ResolveServices()가 지연 재조회로 복구한다(스폰 레이스 방지).
            if (ResolveServices() == null)
            {
                // [운영] 축 A=Warn / 축 B=운영 — 배치 1-A 와 같은 사건이므로 같은 키를 쓴다.
                //   축 A: ServerRpc 처리 경로가 ResolveServices() 로 지연 재조회하여 요청 자체는 계속 처리된다 → Warn.
                //   축 B: NGO 스폰 타이밍은 회선 상태에 좌우돼 플레이어 기기에서만 어긋날 수 있고(①),
                //         플레이어에게 알리는 경로가 없다(②) → 운영.
                // ⚠️ 잠정 판정 — 축 A 를 Error 로 올릴 여지가 있다.
                //    바로 아래 return 때문에 서버의 OnResearchCompleted 구독(69~70행)이 통째로 건너뛰어지고,
                //    그 구독은 이 자리 말고 어디에서도 다시 시도되지 않는다(파일 전체에서 _completedHandler 는
                //    OnNetworkSpawn 에서만 대입된다). 즉 "요청은 살아나지만 완료 브로드캐스트는 죽는" 부분 손상이다.
                //    로그 외 코드를 바꾸지 않는다는 이번 배치 원칙에 따라 원래 레벨(Warning)을 유지하고 보고만 한다.
                GameLog.Ops.Warn(LogEvent.NetworkControllerSpawnedWithoutGameServices,
                                 "Network", nameof(NetworkUpgradeController),
                                 "스폰 시점에 IGameServices 를 얻지 못했다 — 사용 시점 재조회로 복구를 시도한다",
                                 $"IsServer={IsServer}");
                return;
            }

            // 서버만 완료 훅을 구독하여 양 클라 브로드캐스트를 트리거한다.
            // (클라이언트는 브로드캐스트를 받는 쪽이므로 구독하지 않는다.)
            if (IsServer)
            {
                UnitUpgradeUseCase upgrade = _services.GetUpgradeUseCase();
                if (upgrade != null)
                {
                    _completedHandler = OnResearchCompletedOnServer;
                    upgrade.OnResearchCompleted += _completedHandler;
                }
            }

            // [개발] 스폰 진입 흔적 → Info / 개발.
            GameLog.Dev.Info("Network", nameof(NetworkUpgradeController), "네트워크 스폰", $"IsServer={IsServer}");
        }

        public override void OnNetworkDespawn()
        {
            // 구독 해제(재경기/씬 전환 시 중복 구독 방지).
            if (_completedHandler != null && _services != null)
            {
                UnitUpgradeUseCase upgrade = _services.GetUpgradeUseCase();
                if (upgrade != null) upgrade.OnResearchCompleted -= _completedHandler;
                _completedHandler = null;
            }
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// IGameServices 를 지연 해석한다.
        ///
        /// 왜 필요한가(핵심):
        ///   _services 는 OnNetworkSpawn 에서 1회만 캐시된다. 그런데 이 컨트롤러가
        ///   GameServicesLocator.Register(GameBootstrapper) 보다 먼저 스폰되면(씬 오브젝트 스폰 레이스)
        ///   _services 가 null 로 굳어 버린다. 그러면 특히 클라이언트에서 완료 브로드캐스트
        ///   (ResearchLevelClientRpc)가 _services 의존 때문에 조기 반환되어, 레벨 반영과
        ///   OnUpgradeChanged 발행이 누락된다 → 소유 클라의 연구 패널이 진행 레이어에 갇힌다(버그 B / 멀티 자연회복).
        ///   착수 알림(ResearchStartedClientRpc)·취소 알림(ResearchCanceledClientRpc)은 GameEvents 를
        ///   직접 발행해 _services 의존이 없으므로 정상 동작하는데, 완료만 _services 를 타서 비대칭으로 깨졌던 것.
        ///
        /// 따라서 캐시가 null 이면 사용 시점(연구 완료는 게임 시작 후 최소 15초 뒤이므로 이미 등록됨)에
        /// GameServicesLocator.Current 로 재조회해 복구한다.
        /// </summary>
        private IGameServices ResolveServices()
        {
            if (_services == null) _services = GameServicesLocator.Current;
            return _services;
        }

        // ====================================================================
        // 외부 호출용 래퍼 — Presentation(ResearchPanelUI)에서 사용
        // ====================================================================

        /// <summary>
        /// 연구 착수 요청 — UI 래퍼. 실제로는 RequestResearchServerRpc로 위임된다.
        /// </summary>
        /// <param name="group">강화 그룹(Regen은 그룹 무시).</param>
        /// <param name="stat">강화 스탯.</param>
        /// <param name="buildingId">착수한 연구소 건물 Id.</param>
        /// <param name="team">요청 팀.</param>
        public void RequestResearch(UpgradeGroup group, UnitUpgradeStat stat, int buildingId, TeamId team)
        {
            RequestResearchServerRpc((int)group, (int)stat, buildingId, (int)team);
        }

        /// <summary>
        /// 연구 취소 요청 — UI 래퍼(진행 본문의 [취소] 버튼). 실제로는 RequestCancelResearchServerRpc로 위임된다.
        /// 건물은 유지하고, 해당 연구소가 진행 중인 연구만 중단·환불한다.
        /// </summary>
        /// <param name="buildingId">연구를 취소할 연구소 건물 Id.</param>
        /// <param name="team">요청 팀.</param>
        public void RequestCancelResearch(int buildingId, TeamId team)
        {
            RequestCancelResearchServerRpc(buildingId, (int)team);
        }

        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 연구 착수 요청. 서버가 팀 소유권·골드·진행중·최대레벨을 검증하고 타이머를 시작한다.
        /// RequireOwnership=false: 어느 클라이언트든 호출 가능(팀 검증은 내부에서 수행).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestResearchServerRpc(int groupInt, int statInt, int buildingId, int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            IGameServices services = ResolveServices();
            if (services == null)
            {
                SendResearchFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            UnitUpgradeUseCase upgrade = services.GetUpgradeUseCase();
            ResourceUseCase resource = services.GetResource();
            if (upgrade == null || resource == null)
            {
                SendResearchFailed(senderClientId, "맵 로드 중");
                return;
            }

            var group = (UpgradeGroup)groupInt;
            var stat = (UnitUpgradeStat)statInt;
            var team = (TeamId)teamIndex;

            // 팀 소유권 검증: Host(ClientId=0)=Blue, Client(그 외)=Red.
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (team != expectedTeam)
            {
                SendResearchFailed(senderClientId, "팀 불일치");
                return;
            }

            // 서버 권위: 검증 + 골드 차감 + 타이머 시작(내부에서 최대레벨/진행중/골드 검증).
            bool started = upgrade.TryStartResearch(team, group, stat, buildingId, resource);
            if (!started)
            {
                SendResearchFailed(senderClientId, "연구 불가");
                return;
            }

            // 성공: 요청 클라에게만 진행 상태(트랙·타이머) 전송(진행 UI는 소유 클라만 — 규칙 8).
            //   착수 직후 _active에 기록된 전체 시간을 읽어 보낸다. buildingId도 함께 보내
            //   클라가 "이 연구소가 연구 중"임을 알고 진행 레이어로 전환할 수 있게 한다.
            upgrade.TryGetProgress(team, group, stat, out _, out float total);
            SendResearchStarted(senderClientId, groupInt, statInt, buildingId, total);
        }

        // ====================================================================
        // ServerRpc — 연구 취소(건물 유지)
        // ====================================================================

        /// <summary>
        /// 연구 취소 요청. 서버가 팀 소유권을 검증하고, 해당 연구소가 진행 중인 연구를
        /// 중단·환불한다(건물은 유지). 성공 시 요청 클라에게 취소를 통지해 진행 표시를 정리한다.
        /// RequireOwnership=false: 어느 클라이언트든 호출 가능(팀 검증은 내부에서 수행).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestCancelResearchServerRpc(int buildingId, int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            IGameServices services = ResolveServices();
            if (services == null) return;

            UnitUpgradeUseCase upgrade = services.GetUpgradeUseCase();
            ResourceUseCase resource = services.GetResource();
            if (upgrade == null || resource == null) return;

            var team = (TeamId)teamIndex;

            // 팀 소유권 검증: Host(ClientId=0)=Blue, Client(그 외)=Red.
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (team != expectedTeam) return;

            // 서버 권위: 취소 + 골드 100% 환불(NetworkResourceSync가 클라에 골드 반영).
            bool canceled = upgrade.CancelResearchByBuilding(buildingId, resource);
            if (!canceled) return;

            // 요청 클라의 로컬 진행 표시를 정리하도록 통지(소유 클라만 진행 표시를 갖는다).
            SendResearchCanceled(senderClientId, teamIndex);
        }

        // ====================================================================
        // ClientRpc — 서버 → 클라이언트
        // ====================================================================

        // 완료 레벨 — 양 클라 브로드캐스트(효과 양쪽 적용).
        private void OnResearchCompletedOnServer(TeamId team, UpgradeGroup group, UnitUpgradeStat stat, int level)
        {
            ResearchLevelClientRpc((int)team, (int)group, (int)stat, level);
        }

        /// <summary>
        /// 완료된 강화 레벨을 모든 클라이언트에 반영(브로드캐스트). 서버는 이미 반영됨 → 스킵.
        ///
        /// 버그 수정(완료 후 진행 레이어→매트릭스 복귀 실패 / 멀티 자연회복 미완료·취소불가):
        ///   과거에는 _services 가 null(스폰 레이스)이면 여기서 조기 반환하여 SetLevel(→OnUpgradeChanged)이
        ///   누락되었고, 소유 클라의 연구 패널이 진행 레이어에 갇혔다(그 사이 서버는 이미 완료 → 이후 취소도 불가).
        ///   착수/취소 알림은 GameEvents 를 직접 발행해 정상 동작했으나 완료만 _services 를 타서 비대칭으로 깨졌다.
        ///   → (1) ResolveServices()로 서비스를 지연 재조회하여 레벨을 정상 반영하고,
        ///     (2) 서비스가 끝내 null 이어도 UI 가 갇히지 않도록 완료를 직접 통지(OnUpgradeChanged)한다.
        /// </summary>
        [ClientRpc]
        private void ResearchLevelClientRpc(int teamIndex, int groupInt, int statInt, int level)
        {
            if (IsServer) return; // 서버는 TickResearch에서 이미 레벨을 올렸다.

            var team = (TeamId)teamIndex;

            // 서비스 지연 해석(스폰 레이스로 _services가 null일 수 있음).
            UnitUpgradeUseCase upgrade = ResolveServices()?.GetUpgradeUseCase();
            if (upgrade != null)
            {
                // SetLevel 내부에서 진행 레코드 제거 + OnUpgradeChanged 발행
                //   → 소유 클라의 연구 패널이 진행 레이어를 비우고 매트릭스로 복귀한다.
                upgrade.SetLevel(team, (UpgradeGroup)groupInt, (UnitUpgradeStat)statInt, level);
            }
            else
            {
                // 서비스 미해결 시에도 UI 가 진행 레이어에 갇히지 않도록 완료를 직접 통지한다
                //   (취소 통지 ResearchCanceledClientRpc 와 동일한 안전장치 — 서비스 의존 없음).
                GameEvents.OnUpgradeChanged.OnNext(team);
            }
        }

        // 진행 시작 알림 — 요청 클라에게만.
        private void SendResearchStarted(ulong targetClientId, int groupInt, int statInt, int buildingId, float total)
        {
            ClientRpcParams p = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } }
            };
            ResearchStartedClientRpc(groupInt, statInt, buildingId, total, p);
        }

        /// <summary>
        /// 연구 착수 성공을 요청 클라에게만 알린다(진행 UI 표시용). 서버(호스트)는 로컬 상태로 이미 안다.
        /// UI는 이 값으로 진행 바/트랙 잠금을 표시하고, buildingId로 진행 레이어를 연구소에 귀속시킨다.
        /// 실제 완료는 ResearchLevelClientRpc가 확정한다.
        /// </summary>
        [ClientRpc]
        private void ResearchStartedClientRpc(int groupInt, int statInt, int buildingId, float total, ClientRpcParams p = default)
        {
            // UI가 로컬 진행 표시를 시작하도록 이벤트를 발행한다.
            GameEvents.OnResearchStartedLocal.OnNext(
                new ResearchStartedLocalEvent((UpgradeGroup)groupInt, (UnitUpgradeStat)statInt, total, buildingId));
        }

        // 취소 통지 — 요청 클라에게만.
        private void SendResearchCanceled(ulong targetClientId, int teamIndex)
        {
            ClientRpcParams p = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } }
            };
            ResearchCanceledClientRpc(teamIndex, p);
        }

        /// <summary>
        /// 연구 취소를 요청 클라에게 통지한다. 클라는 OnUpgradeChanged를 로컬 발행해
        /// 로컬 진행 표시를 소거하고 매트릭스 레이어로 되돌린다(골드 환불은 NetworkResourceSync가 반영).
        /// 서버(호스트)는 CancelResearchByBuilding 내부에서 이미 OnUpgradeChanged를 발행했으므로 스킵한다.
        /// </summary>
        [ClientRpc]
        private void ResearchCanceledClientRpc(int teamIndex, ClientRpcParams p = default)
        {
            if (IsServer) return; // 호스트는 서버 측에서 이미 처리됨(이중 발행 방지).
            GameEvents.OnUpgradeChanged.OnNext((TeamId)teamIndex);
        }

        // 실패 알림 — 요청 클라에게만.
        private void SendResearchFailed(ulong targetClientId, string reason)
        {
            ClientRpcParams p = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } }
            };
            ResearchFailedClientRpc(reason, p);
        }

        /// <summary>
        /// 연구 착수 실패 알림(요청 클라에게만). 골드 부족이면 토스트 표시.
        /// </summary>
        [ClientRpc]
        private void ResearchFailedClientRpc(string reason, ClientRpcParams p = default)
        {
            // [개발] 축 A=Warn / 축 B=개발.
            //   축 A: 요청 하나만 거부됐을 뿐 게임은 그대로 진행된다 → Warn.
            //   축 B: 팀 불일치·서버 초기화 오류·맵 로드 중 어느 사유든 에디터 2인 구성으로 재현된다(① 아니오).
            //   선례: NetworkProductionController.EnqueueFailedClientRpc(980행) 도 같은 구조로 Dev.Warn 이다.
            //   ⚠️ 다만 그 선례와 다른 점이 하나 있다 — NetworkProductionController 는 서버 쪽 거부 지점에도
            //      운영 로그가 있는데, 이 파일의 서버 쪽 거부 지점(154·162·174·182행)에는 로그가 한 줄도 없다.
            //      로그 신규 추가는 이번 배치(기존 로그 이관) 범위 밖이라 보고만 한다.
            GameLog.Dev.Warn("Network", nameof(NetworkUpgradeController), "클라이언트: 연구 착수 실패 알림 수신",
                             $"Reason={reason}");
            if (reason == "연구 불가")
                GameEvents.OnToastRequested.OnNext(ToastKey.GoldInsufficient);
        }
    }
}
