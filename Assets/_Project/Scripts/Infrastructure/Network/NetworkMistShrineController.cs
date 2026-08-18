// ============================================================================
// NetworkMistShrineController.cs
// 멀티플레이에서 MistShrine "물안개 시전 / 자동 모드 토글" 요청을 서버로 중계하고,
// 그 결과(쿨다운·자동 모드)를 양 클라이언트에 동기화한다.
//
// 흐름(GameSystemRules_Buildings.md MistShrine 규칙 21·22):
//   [수동 시전]
//     1. 클라이언트 패널 탭 → RequestActivate(buildingId, team) → ActivateMistServerRpc(...)
//     2. 서버: 발신자 팀 검증(ClientId 0 = Blue) + 건물 소유 팀 검증
//              → MistShrineUseCase.Activate(건물 생존·타입·쿨다운을 다시 검증한다 — 클라 입력 불신뢰)
//     3. 시전이 성공하면 UseCase가 OnMistActivated 훅을 발화 →
//        이 컨트롤러(서버)가 MistActivatedClientRpc로 양 클라에 쿨다운을 브로드캐스트
//        → 클라이언트는 StartCooldownLocal로 "표시용 쿨다운 미러"를 세운다.
//
//   [자동 모드 토글]
//     RequestToggleAuto → ToggleAutoServerRpc(팀 검증) → 서버 상태 변경 →
//     AutoModeChangedClientRpc로 양 클라 브로드캐스트 → 클라이언트가 SetAutoMode로 미러링.
//
// ⚠️ 왜 시전 브로드캐스트를 ServerRpc가 직접 하지 않고 "훅(이벤트)"으로 하는가:
//   자동 모드 시전은 **서버가 스스로** 하기 때문에 RPC 왕복이 아예 없다(MistShrineUseCase.TickAutoCast).
//   ServerRpc 안에서만 브로드캐스트하면 자동 시전 때 클라이언트 쿨다운 오버레이가 갱신되지 않는다.
//   그래서 "시전 성공" 자체를 UseCase 훅으로 받아 한 곳에서 브로드캐스트한다(수동·자동 공통 경로).
//   같은 패턴: UnitCombatUseCase.OnGlobalStatusApplied → NetworkSkillController,
//             UnitUpgradeUseCase.OnResearchCompleted → NetworkUpgradeController.
//
// 배치(에디터 셋업 스크립트가 자동 생성):
//   씬에 빈 GameObject "NetworkMistShrineController" 생성 → NetworkObject + 이 스크립트 부착.
//   NetworkManager의 씬 오브젝트(자동 스폰)로 설정. GameBootstrapper._networkMistShrineController에 연결.
//
// 아키텍처: Infrastructure 레이어 — NetworkBehaviour 허용. RPC 접미사 ServerRpc/ClientRpc 준수.
//   INetworkMistShrineController(Application 인터페이스)를 구현해 Presentation이 Netcode를 모르게 한다.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// MistShrine 물안개 힐 네트워크 컨트롤러. 시전·자동 토글을 서버에서 재검증하고 결과를 양 클라에 동기화한다.
    /// </summary>
    public class NetworkMistShrineController : NetworkBehaviour, INetworkMistShrineController
    {
        /// <summary> 게임 서비스 참조(IGameServices로 추상화). 지연 해석. </summary>
        private IGameServices _services;

        /// <summary>
        /// 시전 성공 훅을 구독해 둔 UseCase 인스턴스.
        /// 맵 재로드 때 UseCase가 새로 만들어지므로, "지금 서비스가 주는 인스턴스"와 다르면 다시 구독한다.
        /// </summary>
        private MistShrineUseCase _subscribedUseCase;

        /// <summary> 시전 성공 훅 핸들러(해제용). 서버에서만 구독한다. </summary>
        private System.Action<int, float> _activatedHandler;

        // ====================================================================
        // 생명주기
        // ====================================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // 스폰 시점 1차 캐시. 아직 GameServicesLocator 등록 전이면 null일 수 있고,
            // 그 경우 사용 시점 ResolveServices()가 지연 재조회로 복구한다(스폰 레이스 방지).
            if (ResolveServices() == null)
            {
                Debug.LogWarning("[Network] NetworkMistShrineController: GameServicesLocator에 IGameServices가 없습니다(스폰 레이스 가능 — 사용 시점 재조회로 복구 시도).");
            }

            // 서버만 시전 성공 훅을 구독해 쿨다운 브로드캐스트를 트리거한다.
            //   스폰 레이스로 services가 아직 null이면 여기선 건너뛰고, 첫 ServerRpc에서 복구한다.
            EnsureActivationSubscription();
        }

        public override void OnNetworkDespawn()
        {
            // 훅 구독 해제(재경기/씬 전환 시 중복 구독 방지).
            if (_subscribedUseCase != null && _activatedHandler != null)
                _subscribedUseCase.OnMistActivated -= _activatedHandler;
            _subscribedUseCase = null;
            _activatedHandler = null;

            base.OnNetworkDespawn();
        }

        /// <summary>
        /// IGameServices를 지연 해석한다(NetworkSkillController / NetworkUpgradeController와 동일 패턴).
        /// 씬 오브젝트 스폰 레이스로 _services가 null로 굳는 것을 방지하기 위해 사용 시점에 재조회한다.
        /// </summary>
        private IGameServices ResolveServices()
        {
            if (_services == null) _services = GameServicesLocator.Current;
            return _services;
        }

        /// <summary>
        /// 서버에서 MistShrineUseCase.OnMistActivated 구독을 보장한다(멱등).
        ///
        /// 맵을 다시 로드하면 UseCase 인스턴스가 새로 만들어지므로, 구독해 둔 인스턴스와 현재 인스턴스가
        /// 다르면 이전 구독을 해제하고 새 인스턴스에 다시 건다. 그렇지 않으면 브로드캐스트가 조용히 유실된다.
        /// </summary>
        private void EnsureActivationSubscription()
        {
            if (!IsServer) return;

            MistShrineUseCase mist = ResolveServices()?.GetMistShrineUseCase();
            if (mist == null) return;
            if (ReferenceEquals(mist, _subscribedUseCase)) return; // 이미 이 인스턴스를 구독 중.

            if (_subscribedUseCase != null && _activatedHandler != null)
                _subscribedUseCase.OnMistActivated -= _activatedHandler;

            _activatedHandler = OnMistActivatedOnServer;
            mist.OnMistActivated += _activatedHandler;
            _subscribedUseCase = mist;
        }

        // ====================================================================
        // 외부 호출용 래퍼 — Presentation(MistShrine 패널)에서 사용
        // ====================================================================

        /// <summary>
        /// 물안개 시전 요청 — UI 래퍼. 실제로는 ActivateMistServerRpc로 위임된다.
        /// </summary>
        /// <param name="buildingId">시전할 MistShrine 건물 Id.</param>
        /// <param name="team">요청자가 주장하는 팀(서버가 발신자 ClientId로 재검증한다).</param>
        public void RequestActivate(int buildingId, TeamId team)
        {
            // enum은 NGO 직렬화 편의를 위해 int로 변환해 보낸다(생산/스킬 컨트롤러와 동일 관례).
            ActivateMistServerRpc(buildingId, (int)team);
        }

        /// <summary>
        /// 자동 모드 토글 요청 — UI 래퍼. 실제로는 ToggleAutoServerRpc로 위임된다.
        /// </summary>
        /// <param name="buildingId">대상 MistShrine 건물 Id.</param>
        /// <param name="team">요청자가 주장하는 팀(서버가 발신자 ClientId로 재검증한다).</param>
        public void RequestToggleAuto(int buildingId, TeamId team)
        {
            ToggleAutoServerRpc(buildingId, (int)team);
        }

        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 물안개 시전 요청. 서버가 발신자 팀·건물 소유를 검증한 뒤 MistShrineUseCase.Activate를 호출한다.
        /// (건물 생존·타입·쿨다운은 Activate 내부에서 다시 검증한다 — 규칙 20·22.)
        /// RequireOwnership=false: 어느 클라이언트든 호출 가능(팀 검증은 내부에서 수행).
        /// </summary>
        /// <param name="buildingId">시전할 MistShrine 건물 Id.</param>
        /// <param name="teamIndex">요청자가 주장하는 팀(TeamId를 int로 캐스팅한 값).</param>
        /// <param name="rpcParams">NGO가 채워 주는 호출 정보(발신자 ClientId 확인에 사용).</param>
        [ServerRpc(RequireOwnership = false)]
        public void ActivateMistServerRpc(int buildingId, int teamIndex, ServerRpcParams rpcParams = default)
        {
            IGameServices services = ResolveServices();
            if (services == null) return;

            // 시전 성공 훅 구독 보장(스폰 레이스로 OnNetworkSpawn에서 놓쳤을 경우 여기서 복구).
            //   반드시 Activate(=훅 발화) 전에 구독돼 있어야 쿨다운 브로드캐스트가 유실되지 않는다.
            EnsureActivationSubscription();

            if (!ValidateRequest(buildingId, teamIndex, rpcParams, services)) return;

            // 서버 권위 시전(재검증 포함). 성공하면 OnMistActivated 훅이 발화되어 브로드캐스트된다.
            services.GetMistShrineUseCase()?.Activate(buildingId);
        }

        /// <summary>
        /// 자동 모드 토글 요청. 서버가 검증 후 상태를 바꾸고 결과를 양 클라이언트에 브로드캐스트한다(규칙 18·19).
        /// </summary>
        /// <param name="buildingId">대상 MistShrine 건물 Id.</param>
        /// <param name="teamIndex">요청자가 주장하는 팀(TeamId를 int로 캐스팅한 값).</param>
        /// <param name="rpcParams">NGO가 채워 주는 호출 정보(발신자 ClientId 확인에 사용).</param>
        [ServerRpc(RequireOwnership = false)]
        public void ToggleAutoServerRpc(int buildingId, int teamIndex, ServerRpcParams rpcParams = default)
        {
            IGameServices services = ResolveServices();
            if (services == null) return;

            // 자동 모드를 켜면 곧바로 자동 시전이 일어날 수 있으므로, 여기서도 훅 구독을 보장한다.
            EnsureActivationSubscription();

            if (!ValidateRequest(buildingId, teamIndex, rpcParams, services)) return;

            MistShrineUseCase mist = services.GetMistShrineUseCase();
            if (mist == null) return;

            bool on = mist.ToggleAutoMode(buildingId);
            AutoModeChangedClientRpc(buildingId, on);
        }

        /// <summary>
        /// 두 ServerRpc가 공유하는 요청 검증.
        /// ① 발신자 팀과 요청 팀이 일치하는가(Host=ClientId 0=Blue, 그 외=Red — 프로젝트 전역 관례)
        /// ② 대상 건물이 존재하고 살아 있으며 MistShrine인가
        /// ③ 그 건물이 정말 발신자 팀 소유인가(남의 건물 조작 차단)
        /// </summary>
        /// <param name="buildingId">대상 건물 Id.</param>
        /// <param name="teamIndex">요청자가 주장하는 팀(int).</param>
        /// <param name="rpcParams">발신자 ClientId를 담은 RPC 파라미터.</param>
        /// <param name="services">해석된 게임 서비스.</param>
        /// <returns>모든 검증을 통과하면 true.</returns>
        private bool ValidateRequest(int buildingId, int teamIndex, ServerRpcParams rpcParams, IGameServices services)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;

            if ((TeamId)teamIndex != expectedTeam)
            {
                Debug.LogWarning($"[Network] NetworkMistShrineController: 팀 불일치 요청 무시. sender={senderClientId}, 요청팀={(TeamId)teamIndex}, 기대팀={expectedTeam}");
                return false;
            }

            BuildingPlacementUseCase buildings = services.GetBuildingPlacement();
            if (buildings == null) return false;

            BuildingData building = buildings.GetBuilding(buildingId);
            if (building == null || !building.IsAlive) return false;
            if (building.Type != BuildingType.HealShrine) return false;
            if (building.Team != expectedTeam) return false;

            return true;
        }

        // ====================================================================
        // ClientRpc — 서버 → 클라이언트(양 클라 브로드캐스트)
        // ====================================================================

        /// <summary>
        /// 서버에서 시전이 성공하면(수동·자동 공통) 호출되어 쿨다운을 양 클라에 브로드캐스트한다.
        /// </summary>
        /// <param name="buildingId">시전한 MistShrine 건물 Id.</param>
        /// <param name="cooldownTotal">이번 시전으로 걸린 총 쿨다운(초).</param>
        private void OnMistActivatedOnServer(int buildingId, float cooldownTotal)
        {
            MistActivatedClientRpc(buildingId, cooldownTotal);
        }

        /// <summary>
        /// 시전 성공을 모든 클라이언트에 알려 쿨다운 표시 미러를 세운다.
        /// 서버(호스트)는 Activate에서 이미 쿨다운을 세웠으므로 스킵한다(이중 설정 방지).
        /// (물안개 지속 VFX는 아직 제작되지 않아 이번 범위에서는 쿨다운 미러만 동기화한다.)
        /// </summary>
        /// <param name="buildingId">시전한 MistShrine 건물 Id.</param>
        /// <param name="cooldownTotal">총 쿨다운(초).</param>
        [ClientRpc]
        private void MistActivatedClientRpc(int buildingId, float cooldownTotal)
        {
            if (IsServer) return; // 호스트는 서버 Activate에서 이미 쿨다운을 세웠다.

            MistShrineUseCase mist = ResolveServices()?.GetMistShrineUseCase();
            mist?.StartCooldownLocal(buildingId, cooldownTotal);
        }

        /// <summary>
        /// 자동 모드 변경을 모든 클라이언트에 알려 상태 미러를 맞춘다(패널의 테두리 표시가 이 값을 읽는다).
        /// 서버(호스트)는 ToggleAutoServerRpc에서 이미 바꿨으므로 스킵한다(이중 토글 방지).
        /// </summary>
        /// <param name="buildingId">대상 MistShrine 건물 Id.</param>
        /// <param name="on">변경 후의 자동 모드 상태.</param>
        [ClientRpc]
        private void AutoModeChangedClientRpc(int buildingId, bool on)
        {
            if (IsServer) return; // 호스트는 서버에서 이미 상태를 바꿨다.

            MistShrineUseCase mist = ResolveServices()?.GetMistShrineUseCase();
            mist?.SetAutoMode(buildingId, on);
        }
    }
}
