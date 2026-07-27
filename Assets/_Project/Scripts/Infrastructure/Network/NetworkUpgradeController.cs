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

            _services = GameServicesLocator.Current;
            if (_services == null)
            {
                Debug.LogWarning("[Network] NetworkUpgradeController: GameServicesLocator에 IGameServices가 없습니다.");
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

            Debug.Log($"[Network] NetworkUpgradeController 스폰. IsServer={IsServer}");
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

            if (_services == null)
            {
                SendResearchFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            UnitUpgradeUseCase upgrade = _services.GetUpgradeUseCase();
            ResourceUseCase resource = _services.GetResource();
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
            //   착수 직후 _active에 기록된 전체 시간을 읽어 보낸다.
            upgrade.TryGetProgress(team, group, stat, out _, out float total);
            SendResearchStarted(senderClientId, groupInt, statInt, total);
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
        /// </summary>
        [ClientRpc]
        private void ResearchLevelClientRpc(int teamIndex, int groupInt, int statInt, int level)
        {
            if (IsServer) return; // 서버는 TickResearch에서 이미 레벨을 올렸다.

            UnitUpgradeUseCase upgrade = _services != null ? _services.GetUpgradeUseCase() : null;
            if (upgrade == null) return;

            upgrade.SetLevel((TeamId)teamIndex, (UpgradeGroup)groupInt, (UnitUpgradeStat)statInt, level);
        }

        // 진행 시작 알림 — 요청 클라에게만.
        private void SendResearchStarted(ulong targetClientId, int groupInt, int statInt, float total)
        {
            ClientRpcParams p = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } }
            };
            ResearchStartedClientRpc(groupInt, statInt, total, p);
        }

        /// <summary>
        /// 연구 착수 성공을 요청 클라에게만 알린다(진행 UI 표시용). 서버(호스트)는 로컬 상태로 이미 안다.
        /// UI는 이 값으로 진행 바/트랙 잠금을 표시한다. 실제 완료는 ResearchLevelClientRpc가 확정한다.
        /// </summary>
        [ClientRpc]
        private void ResearchStartedClientRpc(int groupInt, int statInt, float total, ClientRpcParams p = default)
        {
            // UI가 로컬 진행 표시를 시작하도록 이벤트를 발행한다.
            GameEvents.OnResearchStartedLocal.OnNext(
                new ResearchStartedLocalEvent((UpgradeGroup)groupInt, (UnitUpgradeStat)statInt, total));
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
            Debug.LogWarning($"[Network] 연구 착수 실패: {reason}");
            if (reason == "연구 불가")
                GameEvents.OnToastRequested.OnNext(ToastKey.GoldInsufficient);
        }
    }
}
