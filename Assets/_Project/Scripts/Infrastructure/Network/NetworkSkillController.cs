// ============================================================================
// NetworkSkillController.cs
// 멀티플레이에서 스킬 발동 요청을 서버로 중계하고, 발동 성공 시 건물 글로벌 쿨다운을
// 양 클라이언트에 동기화(로컬 미러)한다.
//
// 흐름(규칙 25·26):
//   1. 클라이언트(조준 완료) → RequestActivateSkill(buildingId, slot, aimCoord, hasAim)
//        → RequestActivateSkillServerRpc(...)
//   2. 서버: 서비스/UseCase null → 발신자 팀 소유권 + 건물 소유 → SkillActivationUseCase.Activate
//        (Activate 내부에서 건물 생존·쿨다운·유효 타일 재검증 — 클라 입력 불신뢰).
//        - 실패: 아무것도 하지 않음(조용히 무시. 필요 시 향후 실패 토스트 추가).
//        - 성공: Activate가 GameEvents.OnSkillActivationConfirmed(발동 표현 값)를 발행한다.
//          이 컨트롤러는 서버에서 그 이벤트를 구독하고 있다가, 발동 표현 값 + 쿨다운 총량을 실어
//          SkillActivatedClientRpc를 **양 클라 브로드캐스트**한다.
//   3. 클라이언트: 브로드캐스트를 받아 SkillActivationUseCase.StartCooldownLocal로 쿨다운 미러를 세운다
//        (오버레이가 서버와 같은 남은 시간을 표시하도록). 그리고 같은 값을
//        GameEvents.OnSkillActivationReceived로 발행해 화면이 같은 자리에 VFX를 재생하게 한다.
//        서버(호스트)는 Activate에서 쿨다운을 이미 세웠고 화면도 「확정」 이벤트로 이미 재생했으므로 스킵한다.
//
// 2026-11-03 변경(지원 스킬 Task 커밋 3 — C-1 발동 연출 동기화):
//   종전 SkillActivatedClientRpc(buildingId, cooldown)는 "쿨다운 몇 초"만 전했다.
//   이제 "어느 건물(팀)이 몇 번 칸 스킬을 어디에 썼고 몇 번째 발동이며 착탄까지 몇 초"까지 전한다
//   (RPC 이름은 그대로, 인자 모양만 교체 — 순수 클라의 쿨다운 미러 동작은 그대로 유지).
//   RPC 모양이 바뀌었으므로 전투 표현 스키마(UnitAttackShadowProfileResolver)를 6 → 7로 올려
//   구버전 빌드와 섞인 경기는 접속 확인 단계에서 거절된다.
//   요청 RPC(클라 → 서버)는 바꾸지 않았다(스킬 규칙 26 — 좌표만 전송 + 서버 재검증).
//
// 2026-11-04 변경(EMP Task 커밋 2 — 방어탑 무력화 동기화, Plan 3-4 N1):
//   EMP가 서버에서 적 방어탑을 멈추게 하면 SkillActivationUseCase가 GameEvents.OnTowersDisabledConfirmed를 발행한다.
//   서버의 이 컨트롤러가 그것을 구독해 새 RPC TowersDisabledClientRpc(발동 순번, 지속 시간, 탑 Id 배열)를 1회 브로드캐스트하고,
//   순수 클라이언트는 IsServer 가드 뒤에서 같은 값을 GameEvents.OnTowersDisabledReceived로 발행한다(화면 V6용).
//   · 종료 RPC는 없다 — 화면이 받은 지속 시간으로 스스로 끝낸다(사용자 결정).
//   · 클라이언트에는 무력화 상태 사본을 두지 않는다 — 탑 공격 판정은 서버만 하고, 클라가 알아야 하는 곳은 화면뿐이다.
//   · 늦은 접속 스냅샷은 없다(타입 C 상태 동기화와 같음 — 지속 수 초).
//   · 기존 SkillActivatedClientRpc 모양은 바꾸지 않았다(발동 고리 V5는 그대로 그 RPC로 재생된다).
//   새 RPC가 생겼으므로 전투 표현 스키마(UnitAttackShadowProfileResolver)를 7 → 8로 올려
//   이 RPC를 모르는 빌드(스키마 7)와 섞인 경기는 접속 확인 단계에서 거절된다(사용자 결정 Q6).
//
// 2026-11-11 변경(지속 상태 구역 Task 구현 단위 ② — 사용자 결정 Z-2 N-B):
//   서리 감옥 · 수액 늪 구역의 구성원이 바뀐 틱마다 SkillZoneSystem이 GameEvents.OnSkillZoneMembershipConfirmed를 발행한다.
//   서버의 이 컨트롤러가 구독해 새 RPC ZoneMembershipChangedClientRpc(구역 순번, 들어온 Id 배열, 나간 Id 배열)를 브로드캐스트하고,
//   순수 클라이언트는 IsServer 가드 뒤에서 GameEvents.OnSkillZoneMembershipReceived로 발행한다(화면 V4용).
//   구역 생성 · 끝 RPC는 없다(생성 = 발동 표현 RPC, 끝 = 화면 로컬 타이머). 스키마 8 → 9.
//
// 참고 패턴 = NetworkUpgradeController(main 병합 최신 관례):
//   래퍼 → ServerRpc, 팀 소유권(SenderClientId), 브로드캐스트 ClientRpc,
//   GameServicesLocator.Current 지연 해석(스폰 레이스 방지).
//
// 배치(사용자 Unity 작업):
//   씬에 빈 GameObject "NetworkSkillController" 생성 → NetworkObject + 이 스크립트 부착.
//   NetworkManager의 씬 오브젝트(자동 스폰)로 설정. GameBootstrapper._networkSkillController에 연결.
//
// 아키텍처: Infrastructure 레이어 — NetworkBehaviour 허용. RPC 접미사 ServerRpc/ClientRpc 준수.
//   INetworkSkillController(Application 인터페이스)를 구현해 Presentation이 Netcode를 모르게 한다.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using UniRx; // GameEvents(Subject) 구독용 Subscribe(Action) 확장 메서드.
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 스킬 발동 네트워크 컨트롤러. 클라이언트 발동 요청을 서버에서 재검증·실행하고,
    /// 발동 성공 시 건물 글로벌 쿨다운을 양 클라이언트에 동기화한다.
    /// </summary>
    public class NetworkSkillController : NetworkBehaviour, INetworkSkillController
    {
        /// <summary> 게임 서비스 참조(IGameServices로 추상화). 지연 해석. </summary>
        private IGameServices _services;

        // 타입 C 전역 상태 부여 구독 핸들러(해제용). 서버에서만 구독한다.
        //   UnitCombatUseCase.OnGlobalStatusApplied → 이 핸들러 → StatusAppliedClientRpc 브로드캐스트.
        private System.Action<TeamId, StatusEffectKind, float, float> _statusHandler;

        // 발동 표현 「확정」 이벤트 구독 핸들(해제용). 서버에서만 구독한다(2026-11-03 C-1).
        //   GameEvents는 정적 허브라 IGameServices 없이도 구독할 수 있다 → 스폰 레이스와 무관하게 OnNetworkSpawn에서 바로 건다.
        private System.IDisposable _activationConfirmedSubscription;

        // 방어탑 무력화 「확정」 이벤트 구독 핸들(해제용). 서버에서만 구독한다(2026-11-04 EMP Task 커밋 2).
        //   위 발동 표현 구독과 같은 이유로 OnNetworkSpawn에서 바로 건다(정적 GameEvents라 IGameServices가 필요 없다).
        private System.IDisposable _towersDisabledConfirmedSubscription;

        // 지속 상태 구역 구성원 변경 「확정」 이벤트 구독 핸들(해제용). 서버에서만 구독한다(2026-11-11 지속 상태 구역 Task — Z-2 N-B).
        //   위 두 구독과 같은 이유로 OnNetworkSpawn에서 바로 건다(정적 GameEvents라 IGameServices가 필요 없다).
        private System.IDisposable _zoneMembershipConfirmedSubscription;

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
                //   축 A: 여기서 return 하지 않고 EnsureStatusSubscription() 이 첫 발동 시 복구한다 → Warn.
                //   축 B: NGO 스폰 타이밍은 회선 상태에 좌우돼 플레이어 기기에서만 어긋날 수 있고(①),
                //         플레이어에게 알리는 경로가 없다(②) → 운영.
                GameLog.Ops.Warn(LogEvent.NetworkControllerSpawnedWithoutGameServices,
                                 "Network", nameof(NetworkSkillController),
                                 "스폰 시점에 IGameServices 를 얻지 못했다 — 사용 시점 재조회로 복구를 시도한다",
                                 $"IsServer={IsServer}");
            }

            // 서버만 타입 C 상태 부여 훅을 구독해 양 클라 브로드캐스트를 트리거한다.
            //   스폰 레이스로 services가 아직 null이면 여기선 건너뛰고, 첫 발동 시 EnsureStatusSubscription이 복구한다.
            EnsureStatusSubscription();

            // 서버만 발동 표현 「확정」을 구독해 순수 클라이언트에 브로드캐스트한다(C-1).
            //   클라이언트는 Activate를 돌리지 않으므로 「확정」이 발행되지 않는다 — 구독해도 받을 것이 없지만,
            //   의도를 분명히 하려고 서버에서만 건다. 재스폰 대비로 기존 구독이 남아 있으면 먼저 정리한다.
            if (IsServer)
            {
                _activationConfirmedSubscription?.Dispose();
                _activationConfirmedSubscription = GameEvents.OnSkillActivationConfirmed
                    .Subscribe(OnSkillActivationConfirmedOnServer);

                // 방어탑 무력화 「확정」도 서버에서만 구독해 순수 클라이언트에 브로드캐스트한다(EMP 커밋 2).
                //   EMP 판정(SkillActivationUseCase.Activate → BuildingStatusEffectSystem)은 서버에서만 돌기 때문이다.
                _towersDisabledConfirmedSubscription?.Dispose();
                _towersDisabledConfirmedSubscription = GameEvents.OnTowersDisabledConfirmed
                    .Subscribe(OnTowersDisabledConfirmedOnServer);

                // 지속 상태 구역 구성원 변경 「확정」도 서버에서만 구독해 순수 클라이언트에 브로드캐스트한다(2026-11-11 Z-2 N-B).
                //   구역 판정(SkillZoneSystem.Tick)은 서버 권위 틱에서만 돌기 때문이다.
                _zoneMembershipConfirmedSubscription?.Dispose();
                _zoneMembershipConfirmedSubscription = GameEvents.OnSkillZoneMembershipConfirmed
                    .Subscribe(OnZoneMembershipConfirmedOnServer);
            }

            // [개발] 스폰 진입 흔적 → Info / 개발.
            GameLog.Dev.Info("Network", nameof(NetworkSkillController), "네트워크 스폰", $"IsServer={IsServer}");
        }

        public override void OnNetworkDespawn()
        {
            // 상태 부여 구독 해제(재경기/씬 전환 시 중복 구독 방지).
            if (_statusHandler != null)
            {
                UnitCombatUseCase combat = _services?.GetCombatUseCase();
                if (combat != null) combat.OnGlobalStatusApplied -= _statusHandler;
                _statusHandler = null;
            }
            // 발동 표현 「확정」 구독 해제(재경기/씬 전환 시 중복 브로드캐스트 방지 — C-1).
            _activationConfirmedSubscription?.Dispose();
            _activationConfirmedSubscription = null;
            // 방어탑 무력화 「확정」 구독 해제(같은 이유 — EMP 커밋 2).
            _towersDisabledConfirmedSubscription?.Dispose();
            _towersDisabledConfirmedSubscription = null;
            // 구역 구성원 변경 「확정」 구독 해제(같은 이유 — 2026-11-11 지속 상태 구역 Task).
            _zoneMembershipConfirmedSubscription?.Dispose();
            _zoneMembershipConfirmedSubscription = null;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// 서버에서 UnitCombatUseCase.OnGlobalStatusApplied 구독을 보장한다(멱등).
        /// OnNetworkSpawn 스폰 레이스로 놓쳤을 경우 첫 발동 시점에 복구하기 위해 ServerRpc 진입에서도 호출한다.
        /// </summary>
        private void EnsureStatusSubscription()
        {
            if (!IsServer || _statusHandler != null) return;
            UnitCombatUseCase combat = ResolveServices()?.GetCombatUseCase();
            if (combat == null) return;
            _statusHandler = OnGlobalStatusAppliedOnServer;
            combat.OnGlobalStatusApplied += _statusHandler;
        }

        /// <summary>
        /// IGameServices를 지연 해석한다(NetworkUpgradeController.ResolveServices 미러).
        /// 씬 오브젝트 스폰 레이스로 _services가 null로 굳는 것을 방지하기 위해 사용 시점 재조회한다.
        /// </summary>
        private IGameServices ResolveServices()
        {
            if (_services == null) _services = GameServicesLocator.Current;
            return _services;
        }

        // ====================================================================
        // 외부 호출용 래퍼 — Presentation(스킬 패널/조준 컨트롤러)에서 사용
        // ====================================================================

        /// <summary>
        /// 스킬 발동 요청 — UI/조준 래퍼. 실제로는 RequestActivateSkillServerRpc로 위임된다.
        /// 좌표는 지점 지정(타입 A·B)에서만 의미가 있으며, 즉시 발동(타입 C)은 hasAim=false로 보낸다.
        /// </summary>
        /// <param name="buildingId">발동한 스킬 건물 Id.</param>
        /// <param name="skillSlot">발동한 슬롯 번호(0-based).</param>
        /// <param name="aimWorld">조준 좌표(도메인 월드 Vector3, 연속). 지점 지정에서만 사용.</param>
        /// <param name="hasAim">조준 좌표 유효 여부(즉시 발동이면 false).</param>
        public void RequestActivateSkill(int buildingId, int skillSlot, Vector3 aimWorld, bool hasAim)
        {
            // NGO 2.9.2는 Vector3를 기본 직렬화하므로 연속 좌표를 그대로 전송한다(정수 분해 불필요).
            RequestActivateSkillServerRpc(buildingId, skillSlot, aimWorld, hasAim);
        }

        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 스킬 발동 요청. 서버가 팀 소유권을 검증하고 SkillActivationUseCase.Activate를 호출한다.
        /// (건물 생존·쿨다운·좌표 유효성은 Activate 내부에서 재검증한다 — 규칙 26.)
        /// RequireOwnership=false: 어느 클라이언트든 호출 가능(팀 검증은 내부에서 수행).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestActivateSkillServerRpc(int buildingId, int skillSlot, Vector3 aimWorld, bool hasAim,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            IGameServices services = ResolveServices();
            if (services == null) return;

            // 타입 C 상태 부여 구독 보장(스폰 레이스로 OnNetworkSpawn에서 놓쳤을 경우 여기서 복구).
            //   반드시 Activate(=상태 부여·이벤트 발화) 전에 구독돼 있어야 브로드캐스트가 유실되지 않는다.
            EnsureStatusSubscription();

            SkillActivationUseCase skill = services.GetSkillActivationUseCase();
            BuildingPlacementUseCase buildings = services.GetBuildingPlacement();
            if (skill == null || buildings == null) return;

            // 팀 소유권 검증: 요청 건물의 팀이 발신자 팀과 일치해야 한다.
            //   Host(ClientId=0)=Blue, Client(그 외)=Red.
            BuildingData building = buildings.GetBuilding(buildingId);
            if (building == null) return;
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (building.Team != expectedTeam) return;

            // 조준 좌표 구성(지점 지정만). 서버 Activate가 맵 경계 안 점인지 재확인한다(규칙 26).
            Vector3? aim = hasAim ? aimWorld : (Vector3?)null;

            // 서버 권위 발동(재검증 포함).
            //   성공하면 Activate 안에서 OnSkillActivationConfirmed가 발행되고, 이 컨트롤러의
            //   OnSkillActivationConfirmedOnServer가 그것을 받아 양 클라 브로드캐스트를 보낸다.
            //   (종전에는 여기서 직접 SkillActivatedClientRpc(buildingId, cooldown)를 보냈다 —
            //    2026-11-03 C-1에서 브로드캐스트 자리를 「확정」 이벤트 구독으로 옮겼다. 보내는 조건 "발동 성공"은 같다.)
            skill.Activate(buildingId, skillSlot, aim);
        }

        /// <summary>
        /// (서버 전용) 발동 표현 「확정」 이벤트를 받아 모든 클라이언트에 SkillActivatedClientRpc를 보낸다(C-1).
        /// 쿨다운 총량은 Activate가 쿨다운을 세운 뒤에 이벤트를 발행하므로 여기서 읽으면 이번 발동 값이다.
        /// </summary>
        /// <param name="p">서버가 확정한 발동 표현 값.</param>
        private void OnSkillActivationConfirmedOnServer(SkillActivationPresentation p)
        {
            // 스폰이 풀린 뒤(경기 정리 중)에 늦게 들어온 발동이면 RPC를 보낼 수 없다 — NGO 오류 방지.
            if (!IsServer || !IsSpawned) return;

            SkillActivationUseCase skill = ResolveServices()?.GetSkillActivationUseCase();
            float cooldown = skill != null ? skill.GetCooldownTotal(p.BuildingId) : 0f;

            // 팀은 정수로 보낸다(StatusAppliedClientRpc 선례). 좌표는 도메인 좌표 그대로(NGO Vector3 기본 직렬화).
            SkillActivatedClientRpc(p.BuildingId, (int)p.Team, p.Slot, p.HasAim, p.AimWorld,
                                    p.Sequence, p.ImpactDelay, cooldown);
        }

        // ====================================================================
        // ClientRpc — 서버 → 클라이언트(양 클라 브로드캐스트)
        // ====================================================================

        /// <summary>
        /// 스킬 발동 성공을 모든 클라이언트에 알린다.
        /// ① 건물 글로벌 쿨다운을 로컬 미러로 세운다(오버레이가 서버와 같은 남은 시간을 표시).
        /// ② 같은 발동 표현 값을 GameEvents.OnSkillActivationReceived로 발행해 화면이 같은 자리에 VFX를 재생하게 한다.
        /// 서버(호스트)는 Activate에서 쿨다운을 이미 세웠고 화면도 「확정」 이벤트로 이미 재생했으므로 스킵한다
        /// (이중 설정 · 이중 재생 방지 — 공격 표현 AttackEffectClientRpc와 같은 IsServer 가드).
        /// </summary>
        /// <param name="buildingId">발동한 건물 Id.</param>
        /// <param name="teamInt">발동한 팀(정수 — TeamId).</param>
        /// <param name="slot">발동한 슬롯 번호(0-based).</param>
        /// <param name="hasAim">조준 좌표 유효 여부.</param>
        /// <param name="aimWorld">조준 좌표(도메인 월드 — 화면 변환은 받는 화면이 한다).</param>
        /// <param name="sequence">서버 발동 순번.</param>
        /// <param name="impactDelay">착탄 지연(초).</param>
        /// <param name="cooldown">이번 발동의 쿨다운 총량(초) — 쿨다운 미러용.</param>
        [ClientRpc]
        private void SkillActivatedClientRpc(int buildingId, int teamInt, int slot, bool hasAim, Vector3 aimWorld,
                                             int sequence, float impactDelay, float cooldown)
        {
            if (IsServer) return; // 호스트는 서버 Activate에서 이미 쿨다운을 세웠고 「확정」으로 이미 재생했다.

            // [진단 L3] 순수 클라이언트 수신 1줄 — IsServer 가드 "뒤"에 둬서 Host에서는 남지 않는다(같은 사건 중복 금지, LogRules 1.14-9).
            LogActivationReceived(buildingId, teamInt, slot, hasAim, aimWorld, sequence, impactDelay, cooldown);

            // ① 쿨다운 미러(종전과 같은 동작).
            SkillActivationUseCase skill = ResolveServices()?.GetSkillActivationUseCase();
            skill?.StartCooldownLocal(buildingId, cooldown);

            // ② 발동 표현 「수신」 발행. 화면 구독자 예외가 쿨다운 미러나 다른 RPC 처리를 깨지 않게 감싼다.
            var presentation = new SkillActivationPresentation(
                buildingId, (TeamId)teamInt, slot, hasAim, aimWorld, sequence, impactDelay);
            try
            {
                GameEvents.OnSkillActivationReceived.OnNext(presentation);
            }
            catch (System.Exception ex)
            {
                // [개발] Warn + 개발. 화면 표현 구독자의 실패라 게임 결과(피해 · HP · 쿨다운)에는 영향이 없다.
                GameLog.Dev.Warn("Network", nameof(NetworkSkillController),
                                 "스킬 발동 수신 이벤트 구독자 예외 — 쿨다운 미러는 유지",
                                 $"BuildingId={buildingId}, Slot={slot}, Sequence={sequence}, Error={ex.Message}");
            }
        }

        // ====================================================================
        // EMP 방어탑 무력화 — 서버 → 클라 브로드캐스트(2026-11-04 EMP Task 커밋 2, Plan 3-4 N1)
        // ====================================================================

        /// <summary>
        /// (서버 전용) 방어탑 무력화 「확정」 이벤트를 받아 모든 클라이언트에 TowersDisabledClientRpc를 1회 보낸다.
        /// 같은 발동이 낸 SkillActivatedClientRpc(발동 고리 V5)와는 별개의 RPC다 — 발동 표현 RPC 모양은 바꾸지 않는다.
        /// </summary>
        /// <param name="p">서버가 확정한 무력화 표현 값(대상 탑이 1개 이상일 때만 발행된다).</param>
        private void OnTowersDisabledConfirmedOnServer(TowersDisabledPresentation p)
        {
            // 스폰이 풀린 뒤(경기 정리 중)에 늦게 들어온 무력화면 RPC를 보낼 수 없다 — NGO 오류 방지(발동 표현과 같은 가드).
            if (!IsServer || !IsSpawned) return;
            // 대상 탑이 없으면 보낼 것이 없다(발행 쪽에서 이미 거르지만 한 번 더 막는다 — 빈 RPC 방지).
            if (p.TowerCount == 0) return;

            // 팀은 보내지 않는다 — 사용자가 확정한 RPC 모양(순번 · 지속 · 탑 Id 배열)을 그대로 따른다.
            //   받는 화면은 탑 Id로 자기 건물 데이터를 찾아 탑의 팀 · 위치를 얻는다.
            //   int[]는 NGO가 기본 직렬화한다. CopyTowerIds가 새 배열을 만들어 주므로 값 객체 내부는 바뀌지 않는다.
            int[] towerIds = p.CopyTowerIds();
            // [진단 L13] Host 송신 1줄 — 받는 쪽(L14)과 Sequence · TowerIds가 같아야 정상.
            LogTowersDisabledSent(p.Sequence, p.Duration, towerIds);
            TowersDisabledClientRpc(p.Sequence, p.Duration, towerIds);
        }

        /// <summary>
        /// EMP로 무력화된 방어탑 목록을 모든 클라이언트에 알린다(표현 전용 — 클라이언트는 탑 공격을 판정하지 않는다).
        /// 순수 클라이언트는 같은 값을 GameEvents.OnTowersDisabledReceived로 발행해 화면이 같은 탑에 V6를 띄우게 한다.
        /// 서버(호스트)는 「확정」 이벤트로 화면이 이미 시작했으므로 스킵한다(이중 시작 방지 — SkillActivatedClientRpc와 같은 IsServer 가드).
        /// 종료 알림은 없다 — 화면이 받은 지속 시간으로 스스로 끝낸다. 클라이언트에 무력화 상태 사본도 만들지 않는다.
        /// </summary>
        /// <param name="sequence">서버 발동 순번(같은 발동의 SkillActivatedClientRpc 순번과 같다).</param>
        /// <param name="duration">무력화 지속 시간(초).</param>
        /// <param name="towerIds">무력화(또는 남은 시간 갱신)된 탑의 건물 Id 배열.</param>
        [ClientRpc]
        private void TowersDisabledClientRpc(int sequence, float duration, int[] towerIds)
        {
            if (IsServer) return; // 호스트는 서버 쪽 「확정」 이벤트로 화면이 이미 시작했다.

            // [진단 L14] 순수 클라이언트 수신 1줄 — IsServer 가드 "뒤"라 Host에서는 남지 않는다.
            LogTowersDisabledReceived(sequence, duration, towerIds);

            // 전송 값 방어 — 배열이 비었으면 띄울 탑이 없다. 지속 시간 음수 · NaN은 값 객체가 0으로 고친다.
            if (towerIds == null || towerIds.Length == 0) return;

            // 「수신」 채널은 RPC에 팀이 없으므로 CasterTeam을 Neutral로 만든다(값 객체 주석 참조).
            var presentation = new TowersDisabledPresentation(TeamId.Neutral, sequence, duration, towerIds);
            try
            {
                GameEvents.OnTowersDisabledReceived.OnNext(presentation);
            }
            catch (System.Exception ex)
            {
                // [개발] Warn + 개발. 화면 표현 구독자의 실패라 게임 결과(탑 공격 판정은 서버)에는 영향이 없다.
                GameLog.Dev.Warn("Network", nameof(NetworkSkillController),
                                 "방어탑 무력화 수신 이벤트 구독자 예외 — 게임 결과 무영향",
                                 $"Sequence={sequence}, Duration={duration}, TowerCount={towerIds.Length}, Error={ex.Message}");
            }
        }

        // ====================================================================
        // 지속 상태 구역 구성원 변경 — 서버 → 클라 브로드캐스트(2026-11-11 지속 상태 구역 Task 구현 단위 ②, 사용자 결정 Z-2 N-B)
        // ====================================================================
        //
        // 왜 필요한가(초급자용 설명):
        //   서리 감옥 구역 안에서 얼어붙은 유닛 몸에 얼음 이펙트(V4)를 상대 화면에도 같은 유닛에 붙이려면, 클라이언트가
        //   "누가 구역 안인가"를 알아야 한다. 그런데 클라이언트에서는 유닛의 타일 좌표(UnitData.Position)가 이동으로 갱신되지 않아
        //   서버와 같은 기준으로 스스로 셀 수 없다(Research 7절). 그래서 서버가 틱마다 "바뀐 유닛 Id만" 묶어 보낸다(EMP Id 배열 선례).
        //   · 구역 생성 알림은 따로 없다 — 같은 발동의 SkillActivatedClientRpc(발동 표현)가 그 역할이다.
        //   · 구역 끝 알림도 없다 — 화면이 발동 알림을 받은 순간 + 구역 지속 시간으로 스스로 끝낸다(Z-7 「구역 유지」라 일찍 끝나지 않는다).
        //   · 순서: 서버는 첫 구성원을 발동 다음 틱에 보낸다(SkillZoneSystem) → 같은 NetworkObject의 ClientRpc는 보낸 순서대로
        //     도착하므로(신뢰 · 순서 보장 채널) 클라이언트는 발동 알림을 먼저 받는다(위험 R-10).
        //   새 RPC가 생겼으므로 전투 표현 스키마(UnitAttackShadowProfileResolver)를 8 → 9로 올렸다 — 스키마 8 빌드와 섞인 경기는
        //   접속 확인 단계에서 거절된다(두 빌드 모두 새로 설치해야 한다).

        /// <summary>
        /// (서버 전용) 구역 구성원 변경 「확정」 이벤트를 받아 모든 클라이언트에 ZoneMembershipChangedClientRpc를 1회 보낸다.
        /// </summary>
        /// <param name="p">서버가 판정한 구성원 변경(구성원이 바뀐 구역 · 틱마다 1건).</param>
        private void OnZoneMembershipConfirmedOnServer(SkillZoneMembershipPresentation p)
        {
            // 스폰이 풀린 뒤(경기 정리 중)에 늦게 들어온 변경이면 RPC를 보낼 수 없다 — NGO 오류 방지(발동 표현과 같은 가드).
            if (!IsServer || !IsSpawned) return;
            // 바뀐 것이 없으면 보낼 것이 없다(발행 쪽에서 이미 거르지만 한 번 더 막는다 — 빈 RPC 방지).
            if (p.EnteredCount == 0 && p.ExitedCount == 0) return;

            // int[]는 NGO가 기본 직렬화한다. Copy*가 새 배열을 만들어 주므로 값 객체 내부는 바뀌지 않는다.
            int[] entered = p.CopyEnteredIds();
            int[] exited = p.CopyExitedIds();
            try
            {
                ZoneMembershipChangedClientRpc(p.Sequence, entered, exited);
            }
            catch (System.Exception ex)
            {
                // Host 서버 틱 안에서 불린다 — RPC 전송 실패가 서버 판정(구역 효과)을 깨지 않게 삼키고 Warn 1줄만 남긴다.
                GameLog.Dev.Warn("Network", nameof(NetworkSkillController),
                                 "구역 구성원 변경 RPC 전송 실패 — 구역 효과는 유지",
                                 $"Sequence={p.Sequence}, Entered={entered.Length}, Exited={exited.Length}, Error={ex.Message}");
            }
        }

        /// <summary>
        /// 지속 상태 구역 구성원 변경을 모든 클라이언트에 알린다(표현 전용 — 클라이언트는 구역 효과를 판정하지 않는다).
        /// 순수 클라이언트는 같은 값을 GameEvents.OnSkillZoneMembershipReceived로 발행해 화면이 같은 유닛에 얼음 VFX를 붙이게 한다.
        /// 서버(호스트)는 「확정」 이벤트로 화면이 이미 처리했으므로 스킵한다(이중 처리 방지 — 다른 ClientRpc와 같은 IsServer 가드).
        /// 클라이언트에 구역 상태 사본은 만들지 않는다.
        /// </summary>
        /// <param name="sequence">구역 순번(그 구역을 만든 발동의 순번 — SkillActivatedClientRpc 순번과 같다).</param>
        /// <param name="enteredIds">이번에 구역에 들어온 유닛 Id.</param>
        /// <param name="exitedIds">이번에 구역에서 나간 유닛 Id(걸어 나감 + 사망).</param>
        [ClientRpc]
        private void ZoneMembershipChangedClientRpc(int sequence, int[] enteredIds, int[] exitedIds)
        {
            if (IsServer) return; // 호스트는 서버 쪽 「확정」 이벤트로 화면이 이미 처리했다.

            // [진단 L13] 순수 클라이언트 수신 1줄 — IsServer 가드 "뒤"라 Host에서는 남지 않는다(Host ZoneMembersChanged와 Ids 대조).
            LogZoneMembershipReceived(sequence, enteredIds, exitedIds);

            var presentation = new SkillZoneMembershipPresentation(sequence, enteredIds, exitedIds);
            try
            {
                GameEvents.OnSkillZoneMembershipReceived.OnNext(presentation);
            }
            catch (System.Exception ex)
            {
                // [개발] Warn + 개발. 화면 표현 구독자의 실패라 게임 결과(구역 효과는 서버)에는 영향이 없다.
                GameLog.Dev.Warn("Network", nameof(NetworkSkillController),
                                 "구역 구성원 변경 수신 이벤트 구독자 예외 — 게임 결과 무영향",
                                 $"Sequence={sequence}, Entered={(enteredIds != null ? enteredIds.Length : 0)}, Exited={(exitedIds != null ? exitedIds.Length : 0)}, Error={ex.Message}");
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneMembershipReceived(int sequence, int[] enteredIds, int[] exitedIds)
        {
            GameLog.Dev.Info("Network", nameof(NetworkSkillController), "[SKILL-ZONE] ZoneMembershipReceived",
                System.FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=ZoneMembershipReceived, Role=Client, Seq={sequence}, Entered={DiagJoinIds(enteredIds)}, Exited={DiagJoinIds(exitedIds)}"));
        }

        // ====================================================================
        // 진단 로그(개발 축 — 2026-11-09 EMP · 포격 테스트 진단 로그, WORKFLOW [4])
        //   [Conditional] 두 개라 릴리스 빌드에서는 호출 · 문자열 조립까지 사라진다(LogRules 1.7).
        // ====================================================================

        /// <summary> 초 · 월드 단위 실수 → 밀리 단위 정수(로그 값에 소수점 · 문화권 차이가 끼지 않게). </summary>
        private static int DiagMilli(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        /// <summary> 탑 Id 배열 → `a|b|c`(값 안에 `, `를 넣지 않는다 — LogRules 1.4). 비었으면 None. </summary>
        private static string DiagJoinIds(int[] ids)
        {
            if (ids == null || ids.Length == 0) return "None";
            return string.Join("|", ids);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogActivationReceived(int buildingId, int teamInt, int slot, bool hasAim, Vector3 aimWorld,
                                                  int sequence, float impactDelay, float cooldown)
        {
            GameLog.Dev.Info("Network", nameof(NetworkSkillController), "[SKILL-VFX] ActivationReceived",
                System.FormattableString.Invariant(
                    $"Diag=SkillVfx, DiagRev=1, Event=ActivationReceived, Role=Client, Team={(TeamId)teamInt}, BuildingId={buildingId}, Slot={slot}, Sequence={sequence}, HasAim={hasAim}, AimXmm={DiagMilli(aimWorld.x)}, AimZmm={DiagMilli(aimWorld.z)}, CooldownMs={DiagMilli(cooldown)}, ImpactDelayMs={DiagMilli(impactDelay)}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTowersDisabledSent(int sequence, float duration, int[] towerIds)
        {
            GameLog.Dev.Info("Network", nameof(NetworkSkillController), "[EMP-DIAG] TowersDisabledSent",
                System.FormattableString.Invariant(
                    $"Diag=EmpTowerDisable, DiagRev=1, Event=TowersDisabledSent, Role=Host, Sequence={sequence}, DurationMs={DiagMilli(duration)}, TowerCount={(towerIds != null ? towerIds.Length : 0)}, TowerIds={DiagJoinIds(towerIds)}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTowersDisabledReceived(int sequence, float duration, int[] towerIds)
        {
            GameLog.Dev.Info("Network", nameof(NetworkSkillController), "[EMP-DIAG] TowersDisabledReceived",
                System.FormattableString.Invariant(
                    $"Diag=EmpTowerDisable, DiagRev=1, Event=TowersDisabledReceived, Role=Client, Sequence={sequence}, DurationMs={DiagMilli(duration)}, TowerCount={(towerIds != null ? towerIds.Length : 0)}, TowerIds={DiagJoinIds(towerIds)}"));
        }

        // ====================================================================
        // 타입 C — 전역 상태변경 동기화(서버 → 클라 브로드캐스트)
        // ====================================================================

        /// <summary>
        /// 서버에서 타입 C 상태가 부여되면(UnitCombatUseCase.OnGlobalStatusApplied) 호출되어
        /// 상태 부여를 양 클라에 브로드캐스트한다(연구 완료 브로드캐스트와 동일 사유 — 상대 스킬 효과가 내 화면에도 재현).
        /// 회복(HealOverTime)은 HP가 이미 NetworkHealthSync로 동기화되므로 UseCase가 이 이벤트를 발화하지 않는다.
        /// </summary>
        private void OnGlobalStatusAppliedOnServer(TeamId team, StatusEffectKind kind, float magnitude, float duration)
        {
            StatusAppliedClientRpc((int)team, (int)kind, magnitude, duration);
        }

        /// <summary>
        /// 서버가 부여한 타입 C 상태를 각 클라이언트가 자기 유닛에 재현한다(유효 스탯 재계산·연출 기반).
        /// 서버(호스트)는 Activate에서 이미 적용했으므로 스킵한다(이중 적용 방지).
        /// </summary>
        /// <param name="teamInt">대상 팀(정수).</param>
        /// <param name="kindInt">상태 종류(정수 — StatusEffectKind).</param>
        /// <param name="magnitude">강도.</param>
        /// <param name="duration">지속시간(초).</param>
        [ClientRpc]
        private void StatusAppliedClientRpc(int teamInt, int kindInt, float magnitude, float duration)
        {
            if (IsServer) return; // 호스트는 서버 Activate에서 이미 상태를 적용했다.

            UnitCombatUseCase combat = ResolveServices()?.GetCombatUseCase();
            // raiseNetworkEvent=false: 클라 재현이므로 다시 브로드캐스트하지 않는다(무한 전파 방지).
            combat?.ApplySkillGlobalStatus((TeamId)teamInt, (StatusEffectKind)kindInt, magnitude, duration, raiseNetworkEvent: false);
        }
    }
}
