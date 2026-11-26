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
                // [운영] 축 A=Warn / 축 B=운영 — 배치 1-A 와 같은 사건이므로 같은 키를 쓴다.
                //   축 A: 여기서 return 하지 않고 첫 ServerRpc 의 ResolveServices() 가 복구한다 → Warn.
                //   축 B: NGO 스폰 타이밍은 회선 상태에 좌우돼 플레이어 기기에서만 어긋날 수 있고(①),
                //         플레이어에게 알리는 경로가 없다(②) → 운영.
                GameLog.Ops.Warn(LogEvent.NetworkControllerSpawnedWithoutGameServices,
                                 "Network", nameof(NetworkMistShrineController),
                                 "스폰 시점에 IGameServices 를 얻지 못했다 — 사용 시점 재조회로 복구를 시도한다",
                                 $"IsServer={IsServer}");
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
            // [2026-11-26 인게임 이탈 판정 — U-5 · Plan 3절 7단계, 2차 방어] 연결 지연·판정 중이면 요청을 보내지 않는다.
            //   지연 덮개가 1차로 터치를 막지만, 덮개가 뜨기 직전에 열려 있던 경로 등으로 여기까지 오면 서버로 보내지 않고 끝낸다.
            //   🔴 「반영하지 않고 재전송하지 않는다」(U-5) — 나중에 다시 보내는 대기열을 두지 않는다. 막은 사실은 CommandBlocked 로그로 남는다.
            if (InGameLinkWatch.TryBlockPlayerCommand(nameof(RequestActivate))) return;
            // [2026-11-26 2차 — 사양 ④] 조작 처리 확인 진단 — 차단 검사를 통과해 「실제로 보내는」 조작만 기록한다(CommandSent).
            //   Client 면 이 안에서 「조작 도장」(번호 · 보낸 시각)을 Host 로 먼저 보낸다. 진단 전용 — 실패해도 아래 RPC 는 그대로 나간다.
            InGameLinkWatch.NoteCommandSent(nameof(NetworkMistShrineController), nameof(RequestActivate));

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
            // [2026-11-26 인게임 이탈 판정 — U-5 · Plan 3절 7단계, 2차 방어] 연결 지연·판정 중이면 요청을 보내지 않는다.
            //   지연 덮개가 1차로 터치를 막지만, 덮개가 뜨기 직전에 열려 있던 경로 등으로 여기까지 오면 서버로 보내지 않고 끝낸다.
            //   🔴 「반영하지 않고 재전송하지 않는다」(U-5) — 나중에 다시 보내는 대기열을 두지 않는다. 막은 사실은 CommandBlocked 로그로 남는다.
            if (InGameLinkWatch.TryBlockPlayerCommand(nameof(RequestToggleAuto))) return;
            // [2026-11-26 2차 — 사양 ④] 조작 처리 확인 진단 — 차단 검사를 통과해 「실제로 보내는」 조작만 기록한다(CommandSent).
            //   Client 면 이 안에서 「조작 도장」(번호 · 보낸 시각)을 Host 로 먼저 보낸다. 진단 전용 — 실패해도 아래 RPC 는 그대로 나간다.
            InGameLinkWatch.NoteCommandSent(nameof(NetworkMistShrineController), nameof(RequestToggleAuto));

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
            // [2026-11-26 2차 — 사양 ④] 조작 처리 확인 진단 — Host 가 이 요청을 처리하기 시작한 순간(CommandApplied).
            //   보낸 쪽 CommandSent 와 같은 번호(Seq)로 짝을 맞춰 「보낸 뒤 몇 ms 만에 처리됐나 · 처리 당시 연결 상태」를 남긴다.
            //   진단 전용 — 아래 검증 · 처리는 한 글자도 바뀌지 않는다. 감시기가 없으면(에디터 검증 도구 등) 아무것도 하지 않는다.
            InGameLinkWatch.NoteCommandApplied(nameof(NetworkMistShrineController), nameof(RequestActivate), rpcParams.Receive.SenderClientId);

            IGameServices services = ResolveServices();
            // [진단 MIST-DIAG] 요청 1회 = CastRequest 1줄(Result=Accepted|Reject*). 아래 검증을 고치지 않고 같은 조건을 읽기만 한다.
            DiagRequest("CastRequest", buildingId, teamIndex, rpcParams, services);
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
            // [2026-11-26 2차 — 사양 ④] 조작 처리 확인 진단 — Host 가 이 요청을 처리하기 시작한 순간(CommandApplied).
            //   보낸 쪽 CommandSent 와 같은 번호(Seq)로 짝을 맞춰 「보낸 뒤 몇 ms 만에 처리됐나 · 처리 당시 연결 상태」를 남긴다.
            //   진단 전용 — 아래 검증 · 처리는 한 글자도 바뀌지 않는다. 감시기가 없으면(에디터 검증 도구 등) 아무것도 하지 않는다.
            InGameLinkWatch.NoteCommandApplied(nameof(NetworkMistShrineController), nameof(RequestToggleAuto), rpcParams.Receive.SenderClientId);

            IGameServices services = ResolveServices();
            // [진단 MIST-DIAG] 요청 1회 = ToggleRequest 1줄(Result=Accepted|Reject*).
            DiagRequest("ToggleRequest", buildingId, teamIndex, rpcParams, services);
            if (services == null) return;

            // 자동 모드를 켜면 곧바로 자동 시전이 일어날 수 있으므로, 여기서도 훅 구독을 보장한다.
            EnsureActivationSubscription();

            if (!ValidateRequest(buildingId, teamIndex, rpcParams, services)) return;

            MistShrineUseCase mist = services.GetMistShrineUseCase();
            if (mist == null) return;

            bool on = mist.ToggleAutoMode(buildingId);
            // [진단 MIST-DIAG] 서버 상태가 바뀐 직후 AutoToggled 1줄(On · 신전별 ToggleNo) — Client AutoModeReceived와 1:1로 맞춰 본다.
            DiagAutoToggled(rpcParams, buildingId, on);
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
                // [운영] 축 A=Warn / 축 B=운영 — 배치 1-A 선례와 같은 사건이므로 같은 키를 쓴다
                //   (NetworkBuildingController:183 · NetworkProductionController:370 등).
                //   축 A: 이 요청 하나만 거부하고 게임은 계속된다 → Warn.
                //   축 B: 정상 클라이언트는 자기 팀이 아닌 요청을 보낼 수 없다 → 상태 불일치나 변조 신호이고,
                //         이 자리에는 플레이어에게 알리는 경로가 없다 → 운영.
                //   key 표기는 기존 거부 로그와 똑같이 맞춘다(Request= / Reason= / ClientId= / RequestedTeam= / ExpectedTeam=).
                //   ⚠️ Reason=Team 은 선행 파일들이 쓰는 값 그대로다. 여기서 새 표기를 만들면 지표가 둘로 갈라진다.
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest,
                                 "Network", nameof(NetworkMistShrineController),
                                 "서버: 팀 불일치 요청 거부",
                                 $"Request=MistShrine, Reason=Team, ClientId={senderClientId}, " +
                                 $"BuildingId={buildingId}, RequestedTeam={(TeamId)teamIndex}, ExpectedTeam={expectedTeam}");
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
            // [진단 MIST-DIAG] 브로드캐스트 송신 1회 = ActivateSent 1줄(CastResult와 같은 CastNo).
            DiagActivateSent(buildingId, cooldownTotal);
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
            // [진단 MIST-DIAG] 수신 1회 = ActivateReceived 1줄(신전별 RecvNo · 덮어쓰기 직전 미러 값 PrevMirrorMs). 미러를 세우기 전에 읽는다.
            DiagActivateReceived(mist, buildingId, cooldownTotal);
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
            // [진단 MIST-DIAG] 수신 1회 = AutoModeReceived 1줄(On · 신전별 AutoRecvNo · 바꾸기 직전 로컬 값 PrevLocal).
            DiagAutoModeReceived(mist, buildingId, on);
            mist?.SetAutoMode(buildingId, on);
        }
        // ====================================================================
        // [진단 MIST-DIAG] MistShrine 멀티 검증용 개발 진단 로그 (Diag=MistShrine, DiagRev=2 — 개발 축)
        //   2026-11-24 task `_Tasks/2026-11-24/11_28_mistshrine-multi-diag` Plan 2절.
        //   Host: CastRequest · ToggleRequest(요청 1회) · ActivateSent(브로드캐스트 1회) · AutoToggled(토글 1회).
        //   Client: ActivateReceived · AutoModeReceived(수신 1회).
        //   RPC 인자는 하나도 늘리지 않는다 — 기기 사이 대조는 양쪽이 따로 센 신전별 순번(CastNo↔RecvNo, ToggleNo↔AutoRecvNo)으로 한다.
        //   모든 메서드에 [Conditional] 두 개 → 릴리스에서는 호출 · 문자열 조립이 통째로 사라진다(LogRules 1.7). 게임 상태는 읽기만 한다.
        // ====================================================================

        private System.Collections.Generic.Dictionary<int, int> _diagRecvNo;      // Client: 신전 Id → 시전 브로드캐스트 수신 수
        private System.Collections.Generic.Dictionary<int, int> _diagToggleNo;    // Host: 신전 Id → 자동 토글 수
        private System.Collections.Generic.Dictionary<int, int> _diagAutoRecvNo;  // Client: 신전 Id → 자동 모드 브로드캐스트 수신 수

        private string DiagRole()
        {
            return IsServer ? "Host" : "Client";
        }

        private static int DiagMs(float seconds)
        {
            return Mathf.RoundToInt(seconds * 1000f);
        }

        private static int DiagNext(ref System.Collections.Generic.Dictionary<int, int> map, int buildingId)
        {
            if (map == null) map = new System.Collections.Generic.Dictionary<int, int>();
            map.TryGetValue(buildingId, out int n);
            n++;
            map[buildingId] = n;
            return n;
        }

        /// <summary>
        /// 시전 · 토글 요청 1회의 판정 결과를 1줄로 남긴다. ValidateRequest와 「같은 조건을 같은 순서로 읽기만」 한다
        /// (ValidateRequest를 두 번 부르면 팀 불일치 운영 로그가 두 번 찍히므로 다시 부르지 않는다).
        /// ⚠️ ValidateRequest의 조건을 바꾸면 이 메서드도 함께 맞춘다.
        /// 🔴 Reject* 0건은 「재검증 PASS」가 아니라 「거부 경로 미발생」이다 — 정상 Client는 쿨다운 중 탭을 패널에서 먼저 버린다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagRequest(string evt, int buildingId, int teamIndex, ServerRpcParams rpcParams, IGameServices services)
        {
            try
            {
                ulong sender = rpcParams.Receive.SenderClientId;
                TeamId expected = (sender == 0) ? TeamId.Blue : TeamId.Red;
                MistShrineUseCase mist = services?.GetMistShrineUseCase();
                string result;
                if (services == null) result = "RejectServices";
                else if ((TeamId)teamIndex != expected) result = "RejectTeam";
                else
                {
                    BuildingPlacementUseCase buildings = services.GetBuildingPlacement();
                    BuildingData b = buildings?.GetBuilding(buildingId);
                    if (buildings == null) result = "RejectNoBuildings";
                    else if (b == null || !b.IsAlive) result = "RejectMissing";
                    else if (b.Type != BuildingType.HealShrine) result = "RejectType";
                    else if (b.Team != expected) result = "RejectOwner";
                    else if (mist == null) result = "RejectUseCase";
                    else result = "Accepted";
                }

                int cdMs = mist != null ? DiagMs(mist.GetCooldownRemaining(buildingId)) : -1;
                bool autoBefore = mist != null && mist.GetAutoMode(buildingId);
                string data = System.FormattableString.Invariant(
                    $"Diag=MistShrine, DiagRev=2, Event={evt}, Role={DiagRole()}, Sender={sender}, BuildingId={buildingId}, RequestedTeam={(TeamId)teamIndex}, Result={result}, ServerCdMs={cdMs}, AutoBefore={autoBefore}");
                if (result == "Accepted")
                    GameLog.Dev.Info("Network", nameof(NetworkMistShrineController), "[MIST-DIAG] " + evt, data);
                else
                    GameLog.Dev.Warn("Network", nameof(NetworkMistShrineController), "[MIST-DIAG] " + evt, data);
            }
            catch (System.Exception)
            {
                // 진단 실패가 요청 처리를 막으면 안 된다 — 삼킨다.
            }
        }

        /// <summary> Host 시전 브로드캐스트 송신 1줄. CastNo는 UseCase의 CastResult Result=Ok와 같은 값이다(작으면 훅 구독 유실 신호). </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagActivateSent(int buildingId, float cooldownTotal)
        {
            try
            {
                MistShrineUseCase mist = ResolveServices()?.GetMistShrineUseCase();
                int castNo = mist != null ? mist.GetDiagCastCount(buildingId) : -1;
                GameLog.Dev.Info("Network", nameof(NetworkMistShrineController), "[MIST-DIAG] ActivateSent",
                    System.FormattableString.Invariant(
                        $"Diag=MistShrine, DiagRev=2, Event=ActivateSent, Role={DiagRole()}, BuildingId={buildingId}, CastNo={castNo}, CdMs={DiagMs(cooldownTotal)}"));
            }
            catch (System.Exception)
            {
            }
        }

        /// <summary>
        /// Client 시전 브로드캐스트 수신 1줄. RecvNo는 이 Client가 이 신전에 대해 센 수신 순번(Host CastNo와 1:1).
        /// PrevMirrorMs = 덮어쓰기 직전 이 Client의 남은 쿨다운 — 자동 연속 시전이면 작은 양수(전송 지연 + 틱 간격 수준), 수 초면 미러 어긋남.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagActivateReceived(MistShrineUseCase mist, int buildingId, float cooldownTotal)
        {
            try
            {
                int recvNo = DiagNext(ref _diagRecvNo, buildingId);
                string data = System.FormattableString.Invariant(
                    $"Diag=MistShrine, DiagRev=2, Event=ActivateReceived, Role={DiagRole()}, BuildingId={buildingId}, RecvNo={recvNo}, CdMs={DiagMs(cooldownTotal)}, PrevMirrorMs={(mist != null ? DiagMs(mist.GetCooldownRemaining(buildingId)) : -1)}, UseCase={(mist != null ? "Ok" : "Missing")}");
                if (mist != null)
                    GameLog.Dev.Info("Network", nameof(NetworkMistShrineController), "[MIST-DIAG] ActivateReceived", data);
                else
                    GameLog.Dev.Warn("Network", nameof(NetworkMistShrineController), "[MIST-DIAG] ActivateReceived", data);
            }
            catch (System.Exception)
            {
            }
        }

        /// <summary> Host 자동 모드 토글 1줄(바뀐 뒤의 On · 신전별 ToggleNo). </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagAutoToggled(ServerRpcParams rpcParams, int buildingId, bool on)
        {
            try
            {
                int toggleNo = DiagNext(ref _diagToggleNo, buildingId);
                GameLog.Dev.Info("Network", nameof(NetworkMistShrineController), "[MIST-DIAG] AutoToggled",
                    System.FormattableString.Invariant(
                        $"Diag=MistShrine, DiagRev=2, Event=AutoToggled, Role={DiagRole()}, Sender={rpcParams.Receive.SenderClientId}, BuildingId={buildingId}, On={on}, ToggleNo={toggleNo}"));
            }
            catch (System.Exception)
            {
            }
        }

        /// <summary> Client 자동 모드 수신 1줄. PrevLocal(바꾸기 직전 로컬 값)이 On과 같으면 미러가 이미 어긋났거나 중복 수신이다 → Warn. </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagAutoModeReceived(MistShrineUseCase mist, int buildingId, bool on)
        {
            try
            {
                int recvNo = DiagNext(ref _diagAutoRecvNo, buildingId);
                bool prev = mist != null && mist.GetAutoMode(buildingId);
                string data = System.FormattableString.Invariant(
                    $"Diag=MistShrine, DiagRev=2, Event=AutoModeReceived, Role={DiagRole()}, BuildingId={buildingId}, On={on}, AutoRecvNo={recvNo}, PrevLocal={prev}, UseCase={(mist != null ? "Ok" : "Missing")}");
                if (mist != null && prev != on)
                    GameLog.Dev.Info("Network", nameof(NetworkMistShrineController), "[MIST-DIAG] AutoModeReceived", data);
                else
                    GameLog.Dev.Warn("Network", nameof(NetworkMistShrineController), "[MIST-DIAG] AutoModeReceived", data);
            }
            catch (System.Exception)
            {
            }
        }
    }
}
