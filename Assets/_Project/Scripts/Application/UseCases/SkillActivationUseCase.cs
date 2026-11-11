// ============================================================================
// SkillActivationUseCase.cs
// 스킬 발동 오케스트레이션의 단일 진입점 + 건물 글로벌 쿨다운 상태 보관소.
//
// 무엇을 하나(초급자용 설명):
//   "이 건물의 이 슬롯 스킬을, (필요하면) 이 좌표에 발동해줘"라는 요청 하나를 받아
//   ① 재검증(건물 생존·스킬 건물 여부·슬롯 유효·글로벌 쿨다운·좌표 유효) →
//   ② 실행기(A/B/C) 호출 → ③ 글로벌 쿨다운 설정을 수행한다.
//   피해/DoT의 실제 적용은 UnitCombatUseCase가 담당하고, 여기서는 흐름만 조율한다.
//
// 글로벌 쿨다운(규칙 3):
//   쿨다운은 스킬 단위가 아니라 "건물 단위"로 잠긴다. 한 건물의 어떤 스킬을 써도 그 건물의
//   모든 스킬 슬롯이 쿨다운 동안 함께 잠긴다. 상태는 이 UseCase가 Dictionary<건물Id,남은시간>으로
//   보관하고(도메인 BuildingData를 늘리지 않음 — 응집도 유지), 서버 틱에서 감소시킨다.
//
// 플레이어/AI 공유 진입점(§3-8):
//   Activate는 "누가 요청했는지"(플레이어 터치/AI)와 무관하게 동일 경로다.
//   - 플레이어(싱글): 조준 컨트롤러가 좌표를 만든 뒤 이 Activate를 직접 호출.
//   - 플레이어(멀티): NetworkSkillController가 서버에서 재검증 후 이 Activate를 호출.
//   - AI: 서버 권위 컨텍스트에서 좌표를 직접 계산해 이 Activate를 호출(RPC 왕복 불필요).
//
// 서버 권위(규칙 25·26):
//   Activate는 서버(싱글=로컬 서버/멀티=서버)에서만 호출된다. 클라이언트는 발동을 직접 하지 않고,
//   쿨다운 표시용 로컬 미러(StartCooldownLocal)만 브로드캐스트로 받는다.
//
// 발동 표현(C-1, 2026-11-03 지원 스킬 Task 커밋 3):
//   발동이 성공하면 GameEvents.OnSkillActivationConfirmed(SkillActivationPresentation)를 발행한다.
//   싱글은 화면 구독자가 이것으로 VFX를 재생하고, 멀티 서버는 NetworkSkillController가 이것을 받아
//   ClientRpc로 순수 클라이언트에 전한다. 피해 · 쿨다운 판정과는 무관한 "표현 전용" 이벤트다.
//
// 착탄 지연(포격 지원, 2026-11-03 커밋 4):
//   스킬 데이터의 「착탄 지연」이 0보다 크면 타입 A 피해를 바로 주지 않고 "착탄 예약"으로 저장했다가,
//   권위 전용 틱(TickPendingImpacts)이 그 시간이 지난 순간 기존 피해 경로를 1회 호출한다. 0이면 지금처럼 즉발.
//
// 방어탑 무력화(EMP, 2026-11-04 EMP Task 커밋 1):
//   새 타입 AreaTowerDisable의 실행기가 컨텍스트의 무력화 다리를 부르면, 이 UseCase가 주입받은
//   BuildingStatusEffectSystem(SetBuildingStatusSystem)에 "발동 순간 원 안의 적 방어탑"을 바로 건다(지연 없음).
//   커밋 2(동기화): 실제로 멈춘 탑이 1개 이상이면 GameEvents.OnTowersDisabledConfirmed(팀 · 발동 순번 · 지속 · 탑 Id 목록)를
//   발행한다. 멀티 서버에서는 NetworkSkillController가 이것을 TowersDisabledClientRpc로 순수 클라이언트에 전한다.
//
// 지속 상태 구역(2026-11-11 지속 상태 구역 Task — 서리 감옥 · 수액 늪):
//   새 타입 PersistentStatusZone의 실행기가 컨텍스트의 구역 다리를 부르면, 이 UseCase가 주입받은 SkillZoneSystem
//   (SetSkillZoneSystem)에 구역을 만든다(구역 순번 = 발동 순번). 권위 틱 TickZones · 경기 종료 CloseZones도 여기를 거친다.
//
// 진단 로그(2026-11-09, WORKFLOW [4] 「테스트 항목 진단 로그 규칙」 — 모두 개발 축 · 상태가 바뀌는 순간 1줄):
//   머리 [SKILL-VFX] · Diag=SkillVfx — ActivationConfirmed(발동 확정) · ActivationRejected(사용 불가 거부) ·
//     ImpactScheduled(착탄 예약) · ImpactApplied(실제 착탄 피해 — 시각 · 대상 수 · 사망 수) · ImpactDropped(경기 종료로 버림).
//   머리 [EMP-DIAG] · Diag=EmpTowerDisable — TowersDisabled(EMP 판정 결과 — 원 안 · 밖 탑 수, 아군 탑 수, 원 안 적 유닛 수).
//   (2026-11-10 추가) 머리 [SKILL-BUFF] · Diag=SkillBuff — BuffNoEffect(타입 C가 상태를 걸지 않고 끝남 — Warn, 정상이면 0건).
//     타입 C 실행 직전 UnitCombatUseCase에 발동 꼬리표(순번 · 슬롯 · 건물)를 걸어 그쪽 BuffApplied 줄에 붙게 한다.
//   목록 · 계수 조립은 [Conditional] 헬퍼 안에서만 하므로 릴리스 빌드에서는 호출 · 계산 · 문자열 조립이 모두 사라진다.
//
// Application 레이어 — Domain/Application 의존. Unity.Netcode 직접 참조 없음.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 스킬 발동 재검증·실행·쿨다운을 담당하는 서버 권위 유스케이스.
    /// </summary>
    public sealed class SkillActivationUseCase
    {
        // ── 의존성(조합 루트에서 주입) ──────────────────────────────────────
        private readonly BuildingPlacementUseCase _buildingPlacement;
        // 조준 연속 좌표(도메인 월드)가 맵 경계 안인지 판정하는 함수(규칙 22·26 서버 재검증).
        //   좌표화 이전에는 HexGrid.HasTile(정수 타일)로 판정했으나, 연속 좌표에는 "맵 경계 안 점" 판정이 필요하다.
        //   경계 계산(HexMetrics.IsWithinMapBounds)은 Core에 있고, 조합 루트가 클로저로 주입한다(Core→Application 역전).
        private readonly Func<Vector3, bool> _isWithinMapBounds;
        private readonly UnitCombatUseCase _combat;
        private readonly ISkillDataProvider _dataProvider;
        // 팀 → 종족 변환(로드아웃 조회용). 조합 루트가 GameRaceContext 매핑을 주입(TowerCombatUseCase 선례).
        private readonly Func<TeamId, RaceId> _teamToRace;

        // 스킬 실행기 조회 테이블(타입 A·B·C 실행기가 등록돼 있다). 외부 의존이 없어 내부 생성.
        private readonly SkillExecutorRegistry _registry = new SkillExecutorRegistry();

        // ── 건물 글로벌 쿨다운 상태(규칙 3) ────────────────────────────────
        //   Key=건물 Id, Value=남은 시간(초). 서버 틱(TickCooldowns)이 감소시킨다.
        private readonly Dictionary<int, float> _cooldownRemaining = new Dictionary<int, float>();
        //   쿨다운 오버레이의 radial fill 비율 계산을 위해 발동 시점의 총 쿨다운도 보관한다.
        private readonly Dictionary<int, float> _cooldownTotal = new Dictionary<int, float>();

        // [EMP — 2026-11-04 EMP Task 커밋 1] 건물 상태 서비스(방어탑 무력화 저장소). 조합 루트가 SetBuildingStatusSystem으로 주입.
        //   생성자가 아니라 설정자로 받는 이유: TowerCombatUseCase와 같은 주입 방식으로 맞추고, 미주입(null)일 때
        //   "무력화 다리가 아무 일도 하지 않는다"는 안전한 기본값을 두기 위해서다.
        private BuildingStatusEffectSystem _buildingStatus;

        /// <summary>
        /// 스킬 발동 유스케이스 생성.
        /// </summary>
        /// <param name="buildingPlacement">건물 조회(생존/타입/좌표) UseCase.</param>
        /// <param name="isWithinMapBounds">조준 연속 좌표(도메인 월드)가 맵 경계 안인지 판정(규칙 22·26). null 허용(항상 통과).</param>
        /// <param name="combat">피해/DoT 실제 적용을 담당하는 전투 UseCase.</param>
        /// <param name="dataProvider">종족별 스킬 로드아웃 제공자(Infrastructure 구현).</param>
        /// <param name="teamToRace">팀 → 종족 변환 함수(로드아웃 분기, 규칙 1·6).</param>
        public SkillActivationUseCase(
            BuildingPlacementUseCase buildingPlacement,
            Func<Vector3, bool> isWithinMapBounds,
            UnitCombatUseCase combat,
            ISkillDataProvider dataProvider,
            Func<TeamId, RaceId> teamToRace)
        {
            _buildingPlacement = buildingPlacement;
            _isWithinMapBounds = isWithinMapBounds;
            _combat = combat;
            _dataProvider = dataProvider;
            _teamToRace = teamToRace;
        }

        /// <summary>
        /// 건물 상태 서비스(방어탑 무력화 저장소)를 주입한다(GameBootstrapper에서 맵마다 1회 — 2026-11-04 EMP Task 커밋 1).
        /// 주입하지 않으면 EMP 타입(AreaTowerDisable) 스킬은 발동돼도 탑을 멈추지 않는다(무효과 — 안전한 기본값).
        /// </summary>
        /// <param name="buildingStatus">같은 경기의 건물 상태 서비스. TowerCombatUseCase에도 같은 인스턴스를 주입해야 관문이 그 상태를 본다.</param>
        public void SetBuildingStatusSystem(BuildingStatusEffectSystem buildingStatus)
        {
            _buildingStatus = buildingStatus;
        }

        // [2026-11-11 지속 상태 구역 Task] 구역 서비스(서리 감옥 · 수액 늪 저장소). 조합 루트가 SetSkillZoneSystem으로 주입.
        //   설정자 주입 이유는 위 건물 상태 서비스와 같다(생성자를 바꾸면 Editor 회귀 fixture가 깨진다 · null = 무효과 기본값).
        //   권위 틱(TickZones)과 경기 종료 닫기(CloseZones)도 이 UseCase를 거친다 — 멀티 서버 틱이 IGameServices에
        //   새 getter를 늘리지 않고 기존 GetSkillActivationUseCase()로 닿게 하기 위해서다(Editor fixture 대역 구현 보호).
        private SkillZoneSystem _zoneSystem;

        /// <summary>
        /// 구역 서비스(지속 상태 구역 저장소)를 주입한다(GameBootstrapper에서 맵마다 1회 — 2026-11-11 지속 상태 구역 Task).
        /// 주입하지 않으면 구역 타입(PersistentStatusZone) 스킬은 발동돼도 구역을 만들지 않는다(무효과 — 안전한 기본값).
        /// </summary>
        /// <param name="zoneSystem">같은 경기의 구역 서비스. UnitCombatUseCase에도 같은 인스턴스를 주입해야 접근자가 그 효과를 본다.</param>
        public void SetSkillZoneSystem(SkillZoneSystem zoneSystem)
        {
            _zoneSystem = zoneSystem;
        }

        /// <summary> 주입된 구역 서비스(확인 · 검증용). 없으면 null. </summary>
        public SkillZoneSystem SkillZoneSystem => _zoneSystem;

        /// <summary>
        /// (권위 전용 틱) 지속 상태 구역의 남은 시간 · 구성원을 갱신한다(사용자 결정 Z-4).
        /// 반드시 서버 권위 쪽에서만 부른다 — 싱글 = GameBootstrapper.Update(!IsNetworkMode, 유닛 상태 틱 앞),
        /// 멀티 서버 = NetworkCombatController.TickCombat(유닛 상태 틱 앞). 멀티 순수 클라이언트에서는 부르지 않는다
        /// (쿨다운 틱처럼 클라 미러에 얹으면 안 된다 — 클라이언트에는 구역이 없다).
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void TickZones(float dt)
        {
            _zoneSystem?.Tick(dt);
        }

        /// <summary>
        /// 경기 종료 시 모든 구역을 풀고 이후 새 구역을 막는다(Z-8 — 멱등). 조합 루트가 GameEvents.OnGameEnd에서 부른다.
        /// </summary>
        public void CloseZones()
        {
            _zoneSystem?.Close();
        }

        // ====================================================================
        // 발동(단일 진입점 — 플레이어/AI 공유)
        // ====================================================================

        /// <summary>
        /// 스킬을 발동한다(서버 권위). 재검증을 모두 통과하면 실행기를 호출하고 글로벌 쿨다운을 설정한다.
        /// </summary>
        /// <param name="buildingId">발동한 스킬 건물의 Id.</param>
        /// <param name="skillSlot">발동한 슬롯 번호(0-based, 0~4).</param>
        /// <param name="aimWorld">지점 지정 스킬의 조준 좌표(도메인 월드 Vector3, 연속). 즉시 발동이면 null.</param>
        /// <returns>발동에 성공하면 true(호출자가 쿨다운 브로드캐스트에 사용), 재검증 실패면 false.</returns>
        public bool Activate(int buildingId, int skillSlot, Vector3? aimWorld)
        {
            // ① 건물 생존 재검증(규칙 26).
            BuildingData building = _buildingPlacement?.GetBuilding(buildingId);
            if (building == null || !building.IsAlive) return false;

            // ② 스킬 건물 여부 재검증(비스킬 건물에서의 오발동 차단).
            if (!IsSkillBuilding(building.Type)) return false;

            // ③ 슬롯 유효성 + 스킬 데이터 조회(종족 키 분기 — 규칙 1·6).
            if (_dataProvider == null || _teamToRace == null) return false;
            RaceId race = _teamToRace(building.Team);
            IReadOnlyList<SkillData> loadout = _dataProvider.GetLoadout(race);
            if (loadout == null || skillSlot < 0 || skillSlot >= loadout.Count) return false;
            SkillData skill = loadout[skillSlot];
            if (skill == null) return false;

            // ③-1 「사용 가능」 재검증(2026-11-03 추가 — 지원 스킬 Task 커밋 1, 규칙 25 서버 판정).
            //   기능 코드가 아직 없는 스킬은 데이터에서 「사용 가능」을 꺼 둔다. 그런 스킬이 자리표시로
            //   타입 A 값을 갖고 있으면, 이 검사가 없을 때 A 실행기가 실제로 돌아 폭탄처럼 피해를 주고
            //   쿨다운까지 돈다(Research 5-3). 그래서 실행기 조회 · 쿨다운 설정보다 "앞"에서 거부한다.
            //   여기서 false를 돌려주면 호출자(싱글 패널 / 멀티 NetworkSkillController)는 아무것도 하지 않는다
            //   — 쿨다운이 걸리지 않고, 멀티에서는 성공 알림 ClientRpc도 보내지 않는다.
            //   화면 쪽 패널도 같은 값으로 조준 진입을 막지만(BuildingSkillPanelUI), 클라 입력은 믿지 않으므로
            //   서버에서도 반드시 다시 막는다.
            if (!skill.IsAvailable)
            {
                // [진단 L2] 사용 불가 스킬 거부 1줄 — 쿨다운 · 실행기가 돌지 않았다는 증거(포격 시나리오 2).
                LogActivationRejected(building, skillSlot, skill, "Unavailable");
                return false;
            }

            // ④ 글로벌 쿨다운 만료 재검증(규칙 3·26).
            if (GetCooldownRemaining(buildingId) > 0f) return false;

            // ⑤ 지점 지정 스킬이면 조준 좌표(연속)가 맵 경계 안 점인지 재확인(규칙 22·26).
            //   좌표화 이전의 "유효 타일(HasTile)" 판정 → "맵 경계 안 점(point-in-bounds)" 판정으로 교체.
            //   (재검증 자체는 유지 — 클라 입력 불신뢰 원칙 존속. 판정 기준만 연속 좌표에 맞게 바뀐다.)
            bool hasAim = false;
            Vector3 aim = default;
            if (skill.AimType == SkillAimType.PointTarget)
            {
                if (!aimWorld.HasValue) return false;             // 지점 지정인데 좌표 없음 → 실패
                aim = aimWorld.Value;
                if (_isWithinMapBounds != null && !_isWithinMapBounds(aim)) return false; // 맵 밖 점 → 실패(서버 재검증)
                hasAim = true;
            }

            // ⑥ 실행기 조회 — 스킬의 메커니즘 타입에 맞는 실행기를 레지스트리에서 꺼낸다.
            //    레지스트리에 등록되지 않은 타입이면 바로 아래 null 검사가 false 를 돌려주므로
            //    그 스킬은 발동 자체가 무효가 된다(쿨다운도 돌지 않는다).
            ISkillExecutor executor = _registry.TryGet(skill.Mechanic);
            if (executor == null) return false;

            // ── 여기부터는 실패할 수 있는 검사가 없다 → 이번 발동은 "성공"으로 확정된다.
            //   발동 순번을 지금 매긴다(착탄 예약과 「확정」 이벤트가 같은 순번을 쓰도록 — 커밋 4).
            int sequence = ++_activationSequence;

            // ⑦ 실행 — 피해/DoT의 실제 적용은 UnitCombatUseCase가 담당(델리게이트 주입).
            //   타입 A 즉발 피해 다리는 「착탄 지연」에 따라 둘 중 하나를 넘긴다(2026-11-03 포격 — 커밋 4, Plan D1).
            //     - 지연 0(기존 스킬 · 기본값): 지금까지와 같은 즉시 피해 다리 → 동작 변화 없음.
            //     - 지연 > 0(포격 0.4초 · 이후 메테오 1.5초): 피해를 바로 주지 않고 "착탄 예약"만 저장한다.
            //       예약은 권위 전용 틱(TickPendingImpacts)이 남은 시간을 줄여 0 이하가 되는 순간
            //       기존 ApplySkillInstantAreaDamage를 1회 호출한다 → "착탄 순간 범위 안 대상"이 맞는다.
            //   실행기(InstantAreaDamageExecutor)는 바뀌지 않는다 — 실행기는 "피해를 줘"라고만 하고,
            //   "언제 줄지"는 이 UseCase가 고른 다리가 정한다.
            //   마지막 인자(2026-11-04 EMP Task 커밋 1)는 「범위 안 적 방어탑 무력화」 다리다 — EMP 타입 실행기만 부른다.
            //   다른 타입(A · B · C)의 실행기는 이 다리를 부르지 않으므로 기존 스킬 동작은 그대로다.
            //   (커밋 2) 이 다리는 무력화 「확정」 이벤트에 이번 발동 순번을 실어야 하므로, 순번을 붙잡은 람다로 넘긴다
            //   — 바로 위 타입 A 착탄 예약 다리(SelectInstantAreaDamageBridge)가 순번을 붙잡는 것과 같은 방식이다.
            //   [2026-11-11 지속 상태 구역 Task] 마지막(10번째) 인자로 「구역 만들기」 다리를 더 넘긴다 — 구역 타입 실행기만 부른다.
            //   이번 발동 순번 · 슬롯 · 건물 Id를 붙잡은 람다로 넘긴다(구역 순번 = 발동 순번 — 화면이 발동 알림과 짝짓는 열쇠).
            //   옛 호출(기록용 — 인자 9개, 마지막 줄만 달랐다):
            // var ctx = new SkillActivationContext(
            //     skill, building.Team, buildingId, aim, hasAim,
            //     SelectInstantAreaDamageBridge(skill.ImpactDelay, sequence),
            //     ApplyAreaDotBridge,
            //     ApplyGlobalStatusBridge,
            //     (team, center, radius, duration) => ApplyTowerDisableBridge(team, center, radius, duration, sequence));
            var ctx = new SkillActivationContext(
                skill,
                building.Team,
                buildingId,
                aim,
                hasAim,
                SelectInstantAreaDamageBridge(skill.ImpactDelay, sequence),
                ApplyAreaDotBridge,
                ApplyGlobalStatusBridge,
                (team, center, radius, duration) => ApplyTowerDisableBridge(team, center, radius, duration, sequence),
                (team, center, radius, duration, kind, magnitude, allies) =>
                    CreatePersistentZoneBridge(team, center, radius, duration, kind, magnitude, allies, sequence, skillSlot, buildingId));
            // [2026-11-10 강화 스킬 진단 [SKILL-BUFF]] 타입 C 실행 전후로 "실제로 상태를 걸었는가"를 본다(에디터 · 개발 빌드 전용 —
            //   릴리스에서는 두 호출이 사라진다). 발동 · 쿨다운 · 확정 흐름은 바꾸지 않는다(Plan 단위 ① 문서 담당 제안 — 거부로 바꾸지 않음).
            DiagBeginSkillBuffActivation(skill, sequence, skillSlot, buildingId);
            executor.Execute(ctx);
            DiagEndSkillBuffActivation(building.Team, buildingId, skillSlot, skill, sequence);

            // ⑧ 글로벌 쿨다운 설정(규칙 3). 착탄이 늦어도 쿨다운은 "발동 순간" 시작한다(Plan Q1-c).
            StartCooldownLocal(buildingId, skill.Cooldown);

            // ⑨ 발동 표현 「확정」 발행(C-1 — 2026-11-03 지원 스킬 Task 커밋 3).
            //   쿨다운을 세운 "뒤"에 발행한다 — 멀티 서버의 NetworkSkillController가 이 이벤트 안에서
            //   GetCooldownTotal(buildingId)을 읽어 클라 쿨다운 미러 값으로 함께 보내기 때문이다.
            //   발동 판정은 이미 끝났으므로, 화면 구독자에서 예외가 나도 발동 결과(true)는 바뀌지 않게 감싼다.
            // [진단 L1] 발동 확정 1줄(화면 VFX 줄보다 먼저 남도록 「확정」 발행 바로 앞에 둔다).
            LogActivationConfirmed(building.Team, buildingId, skillSlot, skill, hasAim, aim, sequence);
            PublishActivationConfirmed(building.Team, buildingId, skillSlot, hasAim, aim, skill.ImpactDelay, sequence);
            return true;
        }

        // ====================================================================
        // 발동 표현(C-1) — 화면 연출용 「확정」 이벤트
        // ====================================================================

        // 이 경기에서 성공한 발동 수(발동 순번의 원천). UseCase는 맵을 불러올 때마다 새로 만들어지므로
        //   경기마다 0부터 다시 센다. 첫 발동의 순번은 1이다(0은 "순번 없음"으로 남겨 둔다).
        //   Activate가 모든 검사를 통과한 직후 1 올리고, 그 값을 착탄 예약과 「확정」 이벤트에 함께 쓴다.
        private int _activationSequence;

        /// <summary>
        /// 발동 표현 값을 만들어 GameEvents.OnSkillActivationConfirmed로 발행한다.
        /// 구독자(멀티 서버 브로드캐스트 · 화면 VFX)에서 예외가 나도 서버 발동 흐름은 계속되게 한다.
        /// </summary>
        private void PublishActivationConfirmed(TeamId team, int buildingId, int slot, bool hasAim, Vector3 aim,
                                                float impactDelay, int sequence)
        {
            var presentation = new SkillActivationPresentation(
                buildingId, team, slot, hasAim, aim, sequence, impactDelay);
            try
            {
                GameEvents.OnSkillActivationConfirmed.OnNext(presentation);
            }
            catch (Exception ex)
            {
                // [개발] Warn + 개발. 화면 표현 구독자의 실패라 피해 · 쿨다운(게임 결과)에는 영향이 없다.
                //   어떤 발동에서 났는지 다시 찾을 수 있게 건물 · 슬롯 · 순번을 key=value로 남긴다.
                GameLog.Dev.Warn("Skill", nameof(SkillActivationUseCase),
                                 "스킬 발동 확정 이벤트 구독자 예외 — 발동 결과는 유지",
                                 $"BuildingId={buildingId}, Slot={slot}, Sequence={sequence}, Error={ex.Message}");
            }
        }

        /// <summary>
        /// 팀 · 슬롯으로 그 팀 종족 로드아웃의 스킬 데이터를 찾는다(화면 연출이 반경 · VFX를 얻을 때 사용).
        /// 발동 판정과 같은 팀 → 종족 변환(_teamToRace)과 같은 로드아웃 제공자를 쓰므로,
        /// 서버와 클라이언트(같은 빌드)가 같은 SkillData를 찾는다.
        /// </summary>
        /// <param name="team">스킬을 쓴 팀.</param>
        /// <param name="slot">슬롯 번호(0-based).</param>
        /// <returns>스킬 데이터. 제공자가 없거나 슬롯이 범위 밖이면 null.</returns>
        public SkillData FindSkillData(TeamId team, int slot)
        {
            if (_dataProvider == null || _teamToRace == null) return null;
            IReadOnlyList<SkillData> loadout = _dataProvider.GetLoadout(_teamToRace(team));
            if (loadout == null || slot < 0 || slot >= loadout.Count) return null;
            return loadout[slot];
        }

        // 실행기 → UnitCombatUseCase 다리(델리게이트). 건물/스킬 출처 전용 피해 경로를 호출한다.
        //   center는 도메인 월드 좌표(연속 Vector3)다 — UnitCombatUseCase가 뷰 변환 없이 그대로 판정에 쓴다.
        private void ApplyInstantAreaDamageBridge(TeamId team, Vector3 center, float radius, int rawDamage, int sourceBuildingId)
        {
            _combat?.ApplySkillInstantAreaDamage(team, center, radius, rawDamage, sourceBuildingId);
        }

        // ====================================================================
        // 착탄 예약(지연 판정) — 2026-11-03 포격 지원(커밋 4, Plan D1 · Q1 a~d)
        // ====================================================================
        //
        // 왜 필요한가(초급자용 설명):
        //   포격 VFX는 포탄 7발이 하늘에서 떨어져 0.4초 뒤에 땅에 닿는다. 피해를 발동 순간에 바로 주면
        //   포탄이 닿기도 전에 피해 숫자 · 사망이 먼저 보인다. 그래서 서버가 "0.4초 뒤에 이 지점에 피해"라는
        //   예약만 저장해 두고, 시간이 다 되면 그때 범위 안에 있는 대상에게 피해를 준다.
        //   (0.4초 사이 범위 밖으로 나간 유닛은 안 맞고, 들어온 유닛은 맞는다 — 「착탄 순간 범위 안」 의도.)
        //
        // 지킨 결정(사용자 「추천대로 진행하자」, Plan 7-1 Q1):
        //   a. 착탄 전에 시전 건물이 파괴돼도 착탄은 그대로 일어난다(CannonTower 투사체 선례).
        //      → 착탄 처리에서 건물 생존을 다시 보지 않는다. 시전 건물 Id는 피해 출처 표기에만 쓴다.
        //   b. 경기가 끝나면 남은 예약은 버린다 → ClosePendingImpacts(조합 루트가 OnGameEnd에서 부른다).
        //   c. 쿨다운은 발동 순간 시작한다(Activate ⑧ — 이 예약과 무관).
        //   d. 예약은 이 UseCase 안 순수 C# 목록이고, 처리는 "권위 전용 틱"(TickPendingImpacts)에서만 한다.
        //      쿨다운 틱(TickCooldowns)은 멀티 순수 클라이언트의 표시용 미러에서도 돌기 때문에,
        //      거기에 얹으면 클라이언트도 피해를 주게 된다 → 메서드를 따로 둔다.
        //
        // 대상 범위: 타입 A(즉발 범위 피해) 다리에만 지연을 건다. 타입 B(장판) · C(전역 상태)는
        //   지금처럼 즉시 적용된다(이번 Task는 포격 — 타입 A — 만 다룬다. 메테오도 타입 A로 이 장치를 재사용).

        // 착탄 예약 1건. 값 타입(struct)이라 목록에 넣고 빼도 쓰레기(GC)가 생기지 않는다.
        private struct PendingImpact
        {
            public TeamId Team;           // 시전 팀(아군/적 판정 기준)
            public Vector3 Center;        // 착탄 중심(도메인 월드 좌표)
            public float Radius;          // 피해 반경(월드 단위)
            public int RawDamage;         // 감쇄 전 원본 피해(×10 스케일)
            public int SourceBuildingId;  // 시전 건물 Id(피해 출처 표기 — 착탄 때 건물 생존은 보지 않는다, 결정 a)
            public int Sequence;          // 발동 순번(화면의 발동 표현과 같은 값 — 이후 착탄 연출 짝짓기용)
            public float Remaining;       // 착탄까지 남은 시간(초). 0 이하가 되면 착탄.
            public float Delay;           // 예약할 때의 지연(초). 진단 로그가 "발동 뒤 실제로 몇 ms 만에 맞았나"(Delay − Remaining)를 계산할 때만 쓴다.
        }

        // 아직 착탄하지 않은 예약 목록(예약된 순서대로 들어 있다).
        private readonly List<PendingImpact> _pendingImpacts = new List<PendingImpact>(4);
        // 이번 틱에 착탄할 예약을 잠시 옮겨 담는 재사용 버퍼(피해 적용 중 목록이 바뀌어도 안전하도록 분리).
        private readonly List<PendingImpact> _dueImpactBuffer = new List<PendingImpact>(4);
        // 경기 종료로 예약이 닫혔는지. 닫힌 뒤에는 새 예약도 받지 않고 남은 예약도 처리하지 않는다(결정 b).
        private bool _impactsClosed;

        /// <summary>
        /// 이번 발동이 쓸 타입 A 즉발 피해 다리를 고른다.
        /// 착탄 지연이 0이면 기존 즉시 다리, 0보다 크면 착탄 예약 다리를 돌려준다.
        /// </summary>
        /// <param name="impactDelay">스킬 데이터의 착탄 지연(초). SkillData가 음수 · NaN을 이미 0으로 고쳐 둔 값.</param>
        /// <param name="sequence">이번 발동 순번.</param>
        private Action<TeamId, Vector3, float, int, int> SelectInstantAreaDamageBridge(float impactDelay, int sequence)
        {
            if (!(impactDelay > 0f)) return ApplyInstantAreaDamageBridge; // 기존 즉발 그대로(동작 무변경)

            // 지연이 있는 스킬만 작은 람다를 하나 만든다(발동은 드문 사건이라 할당 1회는 무시할 수준).
            return (team, center, radius, rawDamage, sourceBuildingId) =>
                ScheduleImpact(team, center, radius, rawDamage, sourceBuildingId, impactDelay, sequence);
        }

        /// <summary>
        /// 착탄 예약 1건을 저장한다. 경기 종료로 닫힌 뒤라면 저장하지 않는다(결정 b).
        /// </summary>
        private void ScheduleImpact(TeamId team, Vector3 center, float radius, int rawDamage, int sourceBuildingId,
                                    float delay, int sequence)
        {
            if (_impactsClosed) return;
            _pendingImpacts.Add(new PendingImpact
            {
                Team = team,
                Center = center,
                Radius = radius,
                RawDamage = rawDamage,
                SourceBuildingId = sourceBuildingId,
                Sequence = sequence,
                Remaining = delay,
                Delay = delay
            });
            // [진단 L17] 착탄 예약 1줄(포격 시나리오 1 — 예약 지연 값).
            LogImpactScheduled(team, center, radius, rawDamage, sourceBuildingId, delay, sequence);
        }

        /// <summary>
        /// (권위 전용 틱) 착탄 예약의 남은 시간을 줄이고, 다 된 예약마다 기존 즉발 범위 피해를 1회 적용한다.
        /// 반드시 서버 권위 쪽에서만 호출한다 — 싱글 = GameBootstrapper.Update, 멀티 서버 = NetworkCombatController.TickCombat.
        /// 멀티 순수 클라이언트에서는 호출하지 않는다(클라이언트는 예약 자체가 없고, 피해는 서버 HP 동기화로 받는다).
        /// </summary>
        /// <param name="dt">경과 시간(초). 0 이하 · NaN이면 아무것도 하지 않는다.</param>
        public void TickPendingImpacts(float dt)
        {
            if (_impactsClosed || !(dt > 0f) || _pendingImpacts.Count == 0) return;

            // ① 남은 시간을 줄이고, 다 된 예약은 버퍼로 옮긴다(남은 예약은 순서를 지키며 앞으로 당긴다).
            _dueImpactBuffer.Clear();
            int keep = 0;
            for (int i = 0; i < _pendingImpacts.Count; i++)
            {
                PendingImpact p = _pendingImpacts[i];
                p.Remaining -= dt;
                if (p.Remaining <= 0f) _dueImpactBuffer.Add(p);
                else _pendingImpacts[keep++] = p;
            }
            _pendingImpacts.RemoveRange(keep, _pendingImpacts.Count - keep);

            // ② 착탄 — 예약된 순서대로 기존 피해 경로를 1회씩 호출한다(피해 계산 · 이벤트 · 사망 처리는 그대로).
            //   착탄 피해로 성이 무너지면 그 자리에서 OnGameEnd → ClosePendingImpacts가 불릴 수 있다.
            //   그래서 매 착탄 직전에 닫혔는지 다시 보고, 닫혔으면 나머지는 버린다(결정 b).
            for (int i = 0; i < _dueImpactBuffer.Count; i++)
            {
                if (_impactsClosed)
                {
                    // [진단] 같은 틱의 앞선 착탄으로 경기가 끝나 남은 착탄을 버렸다는 1줄(버린 수).
                    LogImpactDropped(_dueImpactBuffer.Count - i, "ClosedDuringTick");
                    break;
                }
                PendingImpact p = _dueImpactBuffer[i];
                // [진단 L18] 착탄 "직전" 원 안 대상을 판정과 같은 기준으로 붙잡아 둔다(피해 뒤 사망 수를 세기 위해).
                DiagCaptureImpactTargets(p);
                ApplyInstantAreaDamageBridge(p.Team, p.Center, p.Radius, p.RawDamage, p.SourceBuildingId);
                // [진단 L18] 실제 착탄 피해 1줄 — 발동 뒤 몇 ms · 맞은 수 · 죽은 수 · 시전 건물 생존 여부.
                LogImpactApplied(p);
            }
            _dueImpactBuffer.Clear();
        }

        /// <summary>
        /// 경기 종료 시 남은 착탄 예약을 모두 버리고, 이후 새 예약 · 처리를 막는다(결정 b — 멱등).
        /// 조합 루트(GameBootstrapper)가 GameEvents.OnGameEnd에서 호출한다(싱글 · 멀티 서버 공통).
        /// 다음 경기는 맵을 다시 불러오며 이 UseCase가 새로 만들어지므로 다시 열 필요가 없다.
        /// </summary>
        public void ClosePendingImpacts()
        {
            // [진단] 처음 닫힐 때 아직 남은 예약이 있었으면 버린 수를 1줄로 남긴다(경기 종료가 착탄을 막았다는 증거).
            if (!_impactsClosed && _pendingImpacts.Count > 0) LogImpactDropped(_pendingImpacts.Count, "GameEnd");
            _impactsClosed = true;
            _pendingImpacts.Clear();
        }

        /// <summary> 아직 착탄하지 않은 예약 수(확인 · 검증용). </summary>
        public int PendingImpactCount => _pendingImpacts.Count;

        private void ApplyAreaDotBridge(TeamId team, Vector3 center, float radius, float dps, float duration)
        {
            _combat?.ApplySkillAreaDot(team, center, radius, dps, duration);
        }

        // 실행기(타입 C) → UnitCombatUseCase 다리(델리게이트). 대상 팀 유닛 전체에 상태를 부여한다.
        //   raiseNetworkEvent=true: 서버 권위 발동이므로 상태 부여를 멀티 클라에 브로드캐스트하도록 이벤트를 발화한다.
        //   (싱글에서는 구독자가 없어 이벤트가 무시되고, 상태는 이 서버에 이미 적용된다.)
        private void ApplyGlobalStatusBridge(TeamId team, StatusEffectKind kind, float magnitude, float duration)
        {
            // [2026-11-10 진단] 실행기가 이 다리까지 왔다 = 상태 적용을 시도했다(BuffNoEffect 판정용 — 에디터 · 개발 빌드 전용).
            DiagMarkGlobalStatusBridgeCalled();
            _combat?.ApplySkillGlobalStatus(team, kind, magnitude, duration, raiseNetworkEvent: true);
        }

        // ====================================================================
        // 방어탑 무력화(EMP) 다리 — 2026-11-04 EMP Task 커밋 1(Plan 3-1 · 3-2)
        // ====================================================================
        //
        // 실행기(AreaTowerDisableExecutor) → BuildingStatusEffectSystem 다리(델리게이트).
        //   대상은 지금 이 순간 원 안의 적 방어탑이다(발동 순간 스냅샷 — 상위 결정 22, 사용자 Q3 「지연 0」).
        //   그래서 타입 A처럼 착탄 예약을 거치지 않고 바로 서비스에 건다(ImpactDelay를 보지 않는다).
        //   Activate는 서버(싱글 = 로컬 서버 / 멀티 = 서버)에서만 불리므로 이 다리도 서버에서만 돈다(스킬 규칙 25).
        //
        //   서비스가 실제로 무력화(또는 남은 시간 갱신)한 탑 Id를 이 버퍼에 채워 돌려준다(커밋 1).
        //   커밋 2(동기화 — Plan 3-4 N1)부터는 그 목록으로 무력화 「확정」 이벤트(GameEvents.OnTowersDisabledConfirmed)를
        //   발행한다. 화면(V6)과 멀티 서버의 NetworkSkillController(TowersDisabledClientRpc)가 이 이벤트를 받는다.
        //   버퍼는 발동마다 재사용하므로, 이벤트 값(TowersDisabledPresentation)은 생성자에서 새 배열로 복사해 둔다.
        private readonly List<int> _disabledTowerIdBuffer = new List<int>(4);

        private void ApplyTowerDisableBridge(TeamId team, Vector3 center, float radius, float duration, int sequence)
        {
            // 서비스가 주입되지 않았으면(조합 루트 미배선 · 테스트) 아무것도 하지 않는다 — 기존 동작과 같다.
            //   이때는 이벤트도 내지 않는다(멈춘 탑이 없으므로 보여 줄 것도 없다).
            if (_buildingStatus == null)
            {
                // [진단] 배선 누락으로 EMP가 아무 탑도 멈추지 못했다는 Warn 1줄(조용한 무효과를 로그로 드러낸다).
                LogTowerDisableUnwired(team, sequence);
                return;
            }

            int count = _buildingStatus.DisableEnemyTowersInRadius(team, center, radius, duration, _disabledTowerIdBuffer);
            // [진단] 서비스의 진단 기록에 이번 발동 순번을 붙인다(만료 · 파괴 줄이 같은 Sequence를 갖도록).
            _buildingStatus.DiagBindSequence(_disabledTowerIdBuffer, sequence);
            // [진단 L5] EMP 판정 결과 1줄 — 대상이 0개여도 남긴다(빈 땅 EMP · 아군 탑 위 EMP 시나리오의 판정 근거).
            LogTowersDisabled(team, center, radius, duration, sequence, count);

            // 원 안에 적 탑이 하나도 없었으면(또는 경기가 닫혀 거부됐으면) 알릴 것이 없다 — 빈 RPC를 보내지 않는다.
            if (count <= 0 || _disabledTowerIdBuffer.Count == 0) return;

            PublishTowersDisabledConfirmed(team, sequence, duration, _disabledTowerIdBuffer);
        }

        // ====================================================================
        // 지속 상태 구역 다리 — 2026-11-11 지속 상태 구역 Task 구현 단위 ①
        // ====================================================================
        //
        // 실행기(PersistentStatusZoneExecutor) → SkillZoneSystem 다리(델리게이트).
        //   구역 순번 = 이번 발동 순번이다 — 화면은 발동 알림(같은 순번)으로 구역 VFX를 시작하고, 구성원 알림을 그 순번으로 짝짓는다.
        //   Activate는 서버에서만 불리므로 이 다리도 서버에서만 돈다(스킬 규칙 25).
        //   구역 생성 알림 이벤트는 따로 내지 않는다 — 발동 「확정」 이벤트(아래 ⑨)가 그 역할을 한다(사용자 결정 Z-2 N-B).
        private void CreatePersistentZoneBridge(TeamId team, Vector3 center, float radius, float duration, StatusEffectKind kind,
                                                float magnitude, bool targetsAllies, int sequence, int slot, int buildingId)
        {
            if (_zoneSystem == null)
            {
                // [진단] 배선 누락으로 구역이 생기지 않았다는 Warn 1줄(조용한 무효과를 로그로 드러낸다 — EMP 선례).
                LogZoneUnwired(team, slot, sequence);
                return;
            }
            _zoneSystem.CreateZone(sequence, team, slot, buildingId, center, radius, duration, kind, magnitude, targetsAllies);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneUnwired(TeamId team, int slot, int sequence)
        {
            GameLog.Dev.Warn("Skill", nameof(SkillActivationUseCase), "[SKILL-ZONE] ZoneCreateRejected",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=ZoneCreateRejected, Role={DiagRole()}, Seq={sequence}, Team={team}, Slot={slot}, Reason=ZoneSystemMissing"));
        }

        /// <summary>
        /// 무력화 표현 값을 만들어 GameEvents.OnTowersDisabledConfirmed로 발행한다(커밋 2).
        /// 탑 무력화 판정은 이미 끝났으므로, 구독자(멀티 서버 브로드캐스트 · 화면)에서 예외가 나도
        /// 무력화 상태와 발동 흐름(쿨다운 설정 · 발동 「확정」 발행)은 그대로 이어지게 감싼다(PublishActivationConfirmed 선례).
        /// </summary>
        private static void PublishTowersDisabledConfirmed(TeamId team, int sequence, float duration, List<int> towerIds)
        {
            // 생성자가 목록을 새 배열로 복사한다 → 다음 발동이 버퍼를 덮어써도 이 값은 바뀌지 않는다.
            var presentation = new TowersDisabledPresentation(team, sequence, duration, towerIds);
            try
            {
                GameEvents.OnTowersDisabledConfirmed.OnNext(presentation);
            }
            catch (Exception ex)
            {
                // [개발] Warn + 개발. 화면 표현 구독자의 실패라 탑 무력화 판정(게임 결과)에는 영향이 없다.
                //   어떤 발동에서 났는지 다시 찾을 수 있게 팀 · 순번 · 탑 수를 key=value로 남긴다.
                GameLog.Dev.Warn("Skill", nameof(SkillActivationUseCase),
                                 "방어탑 무력화 확정 이벤트 구독자 예외 — 무력화 결과는 유지",
                                 $"Team={team}, Sequence={sequence}, TowerCount={presentation.TowerCount}, Error={ex.Message}");
            }
        }

        // ====================================================================
        // 글로벌 쿨다운 상태 API
        // ====================================================================

        /// <summary>
        /// 지정 건물의 글로벌 쿨다운을 시작(또는 갱신)한다.
        /// 서버 Activate가 발동 성공 시 호출하며, 멀티 클라이언트는 브로드캐스트를 받아 로컬 미러로 호출한다.
        /// </summary>
        /// <param name="buildingId">쿨다운을 걸 건물 Id.</param>
        /// <param name="cooldown">쿨다운 시간(초). 0 이하면 즉시 사용 가능 상태로 정리.</param>
        public void StartCooldownLocal(int buildingId, float cooldown)
        {
            if (cooldown <= 0f)
            {
                _cooldownRemaining.Remove(buildingId);
                _cooldownTotal.Remove(buildingId);
                return;
            }
            _cooldownRemaining[buildingId] = cooldown;
            _cooldownTotal[buildingId] = cooldown;
        }

        /// <summary>
        /// 매 서버 틱(싱글=GameBootstrapper.Update / 멀티 서버=NetworkCombatController.TickCombat,
        /// 멀티 클라=로컬 미러 감소)에서 호출되어 모든 건물 쿨다운을 감소시킨다.
        /// ⚠️ 착탄 예약은 여기서 처리하지 않는다 — 이 틱은 순수 클라이언트 미러에서도 돌기 때문이다.
        ///    착탄은 권위 전용 틱 <see cref="TickPendingImpacts"/>가 따로 처리한다.
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void TickCooldowns(float dt)
        {
            if (dt <= 0f || _cooldownRemaining.Count == 0) return;

            // 순회 중 제거를 위해 만료된 키를 모아 뒤에서 제거한다(컬렉션 변경 예외 회피).
            _expiredBuffer.Clear();
            // Dictionary 키 순회를 위해 키 복사 없이 값만 갱신하고, 만료 키만 별도 수집한다.
            foreach (var kv in _cooldownRemaining)
            {
                _tickKeyBuffer.Add(kv.Key);
            }
            for (int i = 0; i < _tickKeyBuffer.Count; i++)
            {
                int id = _tickKeyBuffer[i];
                float remaining = _cooldownRemaining[id] - dt;
                if (remaining <= 0f)
                    _expiredBuffer.Add(id);
                else
                    _cooldownRemaining[id] = remaining;
            }
            _tickKeyBuffer.Clear();

            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                _cooldownRemaining.Remove(_expiredBuffer[i]);
                _cooldownTotal.Remove(_expiredBuffer[i]);
            }
            _expiredBuffer.Clear();
        }

        // TickCooldowns의 GC 절감용 재사용 버퍼(서버/클라 단일 스레드 갱신이라 재사용 안전).
        private readonly List<int> _tickKeyBuffer = new List<int>(8);
        private readonly List<int> _expiredBuffer = new List<int>(8);

        /// <summary> 지정 건물의 남은 쿨다운(초). 쿨다운이 없으면 0. </summary>
        public float GetCooldownRemaining(int buildingId)
        {
            return _cooldownRemaining.TryGetValue(buildingId, out float r) ? r : 0f;
        }

        /// <summary> 지정 건물의 발동 시점 총 쿨다운(초). 오버레이 radial fill 비율 계산용. 없으면 0. </summary>
        public float GetCooldownTotal(int buildingId)
        {
            return _cooldownTotal.TryGetValue(buildingId, out float t) ? t : 0f;
        }

        /// <summary> 지정 건물이 현재 쿨다운 중인지 여부(규칙 3). </summary>
        public bool IsOnCooldown(int buildingId)
        {
            return GetCooldownRemaining(buildingId) > 0f;
        }

        /// <summary> 게임 재시작/맵 전환 시 쿨다운 상태를 전부 초기화한다(남은 착탄 예약도 함께 버린다). </summary>
        public void ClearAll()
        {
            _cooldownRemaining.Clear();
            _cooldownTotal.Clear();
            // 2026-11-03: 초기화 뒤에 이전 경기의 포탄이 떨어지지 않도록 착탄 예약도 비운다(닫힘 여부는 바꾸지 않는다).
            _pendingImpacts.Clear();
        }

        // ====================================================================
        // 진단 로그(개발 축) — 2026-11-09 EMP · 포격 테스트 진단 로그
        //   모든 메서드에 [Conditional] 두 개를 붙여 릴리스 빌드에서는 호출 · 인자 계산 · 문자열 조립이 통째로 사라진다(LogRules 1.7).
        //   이 UseCase는 서버 권위 쪽(싱글 · Host)에서만 도므로 Role은 Single 또는 Host다.
        // ====================================================================

        // 착탄 직전 원 안 대상(진단 전용 재사용 버퍼 — 판정 버퍼와 따로 둔다).
        private readonly List<UnitData> _diagImpactUnits = new List<UnitData>(16);
        private readonly List<BuildingData> _diagImpactBuildings = new List<BuildingData>(8);
        private readonly System.Text.StringBuilder _diagIds = new System.Text.StringBuilder(64);

        /// <summary> 진단 로그 Role= 값(Single · Host · Client). </summary>
        private static string DiagRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        /// <summary> 월드 단위 · 초 단위 실수를 밀리 단위 정수로(로그 값에 소수점 · 문화권 차이가 끼지 않게). </summary>
        private static int DiagMilli(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogActivationConfirmed(TeamId team, int buildingId, int slot, SkillData skill, bool hasAim,
                                                   Vector3 aim, int sequence)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-VFX] ActivationConfirmed",
                FormattableString.Invariant(
                    $"Diag=SkillVfx, DiagRev=1, Event=ActivationConfirmed, Role={DiagRole()}, Team={team}, BuildingId={buildingId}, Slot={slot}, Mechanic={skill.Mechanic}, Sequence={sequence}, HasAim={hasAim}, AimXmm={DiagMilli(aim.x)}, AimZmm={DiagMilli(aim.z)}, RadiusMm={DiagMilli(skill.Radius)}, CooldownMs={DiagMilli(skill.Cooldown)}, ImpactDelayMs={DiagMilli(skill.ImpactDelay)}, DurationMs={DiagMilli(skill.Duration)}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogActivationRejected(BuildingData building, int slot, SkillData skill, string reason)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-VFX] ActivationRejected",
                FormattableString.Invariant(
                    $"Diag=SkillVfx, DiagRev=1, Event=ActivationRejected, Role={DiagRole()}, Team={building.Team}, BuildingId={building.Id}, Slot={slot}, Mechanic={skill.Mechanic}, Reason={reason}, CooldownStarted=False"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogImpactScheduled(TeamId team, Vector3 center, float radius, int rawDamage, int sourceBuildingId,
                                               float delay, int sequence)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-VFX] ImpactScheduled",
                FormattableString.Invariant(
                    $"Diag=SkillVfx, DiagRev=1, Event=ImpactScheduled, Role={DiagRole()}, Team={team}, BuildingId={sourceBuildingId}, Sequence={sequence}, DelayMs={DiagMilli(delay)}, AimXmm={DiagMilli(center.x)}, AimZmm={DiagMilli(center.z)}, RadiusMm={DiagMilli(radius)}, RawDamage={rawDamage}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagCaptureImpactTargets(PendingImpact p)
        {
            if (_combat == null) { _diagImpactUnits.Clear(); _diagImpactBuildings.Clear(); return; }
            _combat.CollectSkillAreaTargetsForDiagnostics(p.Team, p.Center, p.Radius, _diagImpactUnits, _diagImpactBuildings);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogImpactApplied(PendingImpact p)
        {
            // 피해 "뒤" 사망 수를 센다(착탄 직전에 붙잡은 대상 중 지금 죽은 것).
            int unitKills = 0, buildingKills = 0;
            _diagIds.Clear();
            for (int i = 0; i < _diagImpactUnits.Count; i++)
            {
                UnitData u = _diagImpactUnits[i];
                if (!u.IsAlive) unitKills++;
                if (_diagIds.Length > 0) _diagIds.Append('|');
                _diagIds.Append('U').Append(u.Id);
            }
            for (int i = 0; i < _diagImpactBuildings.Count; i++)
            {
                BuildingData b = _diagImpactBuildings[i];
                if (!b.IsAlive) buildingKills++;
                if (_diagIds.Length > 0) _diagIds.Append('|');
                _diagIds.Append('B').Append(b.Id);
            }
            BuildingData caster = _buildingPlacement?.GetBuilding(p.SourceBuildingId);
            bool casterAlive = caster != null && caster.IsAlive;
            string hitIds = _diagIds.Length == 0 ? "None" : _diagIds.ToString();
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-VFX] ImpactApplied",
                FormattableString.Invariant(
                    $"Diag=SkillVfx, DiagRev=1, Event=ImpactApplied, Role={DiagRole()}, Team={p.Team}, BuildingId={p.SourceBuildingId}, Sequence={p.Sequence}, DelayMs={DiagMilli(p.Delay)}, MsAfterActivation={DiagMilli(p.Delay - p.Remaining)}, UnitHits={_diagImpactUnits.Count}, UnitKills={unitKills}, BuildingHits={_diagImpactBuildings.Count}, BuildingKills={buildingKills}, HitIds={hitIds}, CasterAlive={casterAlive}, RawDamage={p.RawDamage}"));
            _diagImpactUnits.Clear();
            _diagImpactBuildings.Clear();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogImpactDropped(int count, string reason)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-VFX] ImpactDropped",
                FormattableString.Invariant(
                    $"Diag=SkillVfx, DiagRev=1, Event=ImpactDropped, Role={DiagRole()}, Reason={reason}, DroppedCount={count}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogTowersDisabled(TeamId team, Vector3 center, float radius, float duration, int sequence, int disabled)
        {
            // 원 안 · 밖 탑 수(판정과 같은 기준) — 「원 밖 탑은 계속 쏜다」 · 「아군 탑 무영향」 판정 근거.
            _buildingStatus.CountTowersForDiagnostics(team, center, radius,
                out int enemyInRadius, out int enemyOutOfRange, out int allyInRadius);
            // 원 안 적 유닛 수 — 「원 안 적 유닛은 그대로」 판정 근거(EMP는 유닛 상태를 건드리지 않는다).
            int enemyUnits = 0;
            if (_combat != null)
            {
                _combat.CollectSkillAreaTargetsForDiagnostics(team, center, radius, _diagImpactUnits, null);
                enemyUnits = _diagImpactUnits.Count;
                _diagImpactUnits.Clear();
            }
            _diagIds.Clear();
            for (int i = 0; i < _disabledTowerIdBuffer.Count; i++)
            {
                if (i > 0) _diagIds.Append('|');
                _diagIds.Append(_disabledTowerIdBuffer[i]);
            }
            string towerIds = _diagIds.Length == 0 ? "None" : _diagIds.ToString();
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[EMP-DIAG] TowersDisabled",
                FormattableString.Invariant(
                    $"Diag=EmpTowerDisable, DiagRev=1, Event=TowersDisabled, Role={DiagRole()}, Team={team}, Sequence={sequence}, DurationMs={DiagMilli(duration)}, RadiusMm={DiagMilli(radius)}, AimXmm={DiagMilli(center.x)}, AimZmm={DiagMilli(center.z)}, Disabled={disabled}, Refreshed={_buildingStatus.DiagLastRefreshedCount}, PrevRemainingMs={_buildingStatus.DiagLastPrevRemainingMs}, TowerIds={towerIds}, EnemyTowersInRadius={enemyInRadius}, EnemyTowersOutOfRange={enemyOutOfRange}, AllyTowersInRadius={allyInRadius}, EnemyUnitsInRadius={enemyUnits}, EventPublished={disabled > 0}"));
        }

        // ── [2026-11-10 강화 스킬 진단 — 지원 스킬 1차 강화 Task 구현 단위 ④, Plan 5절 L2] ──
        //   타입 C(전역 상태변경)는 데이터 칸(지속 > 0 · 상태 종류)이 하나라도 빠지면 실행기가 "조용히" 끝나는데도
        //   쿨다운 · 발동 확정은 그대로 진행된다(Research 3-7) → 「효과 없이 쿨다운만 도는 스킬」. 그런 발동마다 Warn 1줄을 남긴다.
        //   정상이면 0건이어야 한다(L2). 발동 결과 · 쿨다운은 바꾸지 않는다(진단만).
        //   BuffApplied(적용 직후 대상 수)는 UnitCombatUseCase 쪽 진단이 남긴다 — 여기서는 그 줄에 순번 · 슬롯을 붙이는 꼬리표만 건다.
        private bool _diagGlobalStatusBridgeCalled;

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagBeginSkillBuffActivation(SkillData skill, int sequence, int slot, int buildingId)
        {
            _diagGlobalStatusBridgeCalled = false;
            if (skill == null || skill.Mechanic != SkillMechanicType.GlobalStatusChange) return;
            _combat?.DiagTagSkillBuffActivation(sequence, slot, buildingId);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagMarkGlobalStatusBridgeCalled()
        {
            _diagGlobalStatusBridgeCalled = true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagEndSkillBuffActivation(TeamId team, int buildingId, int slot, SkillData skill, int sequence)
        {
            try
            {
                if (skill == null || skill.Mechanic != SkillMechanicType.GlobalStatusChange) return;
                _combat?.DiagClearSkillBuffActivationTag();
                if (_diagGlobalStatusBridgeCalled) return;
                // 실행기(GlobalStatusChangeExecutor)가 조용히 끝난 이유를 같은 순서로 다시 판정한다(38 · 43행 — 지속 → 종류).
                string reason = !(skill.Duration > 0f) ? "DurationNotPositive"
                    : (StatusEffectKind)skill.StatusKind == StatusEffectKind.None ? "StatusKindNone"
                    : "ExecutorReturned";
                GameLog.Dev.Warn("Skill", nameof(SkillActivationUseCase), "[SKILL-BUFF] BuffNoEffect",
                    FormattableString.Invariant(
                        $"Diag=SkillBuff, DiagRev=1, Event=BuffNoEffect, Role={DiagRole()}, Team={team}, BuildingId={buildingId}, Slot={slot}, Sequence={sequence}, Reason={reason}, DurationMs={DiagMilli(skill.Duration)}, StatusKind={skill.StatusKind}, MagMilli={DiagMilli(skill.Magnitude)}, CooldownStarted=True"));
            }
            catch { /* 진단 전용 — 서버 발동을 막지 않는다 */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTowerDisableUnwired(TeamId team, int sequence)
        {
            GameLog.Dev.Warn("Skill", nameof(SkillActivationUseCase), "[EMP-DIAG] TowersDisabled",
                FormattableString.Invariant(
                    $"Diag=EmpTowerDisable, DiagRev=1, Event=TowersDisabled, Role={DiagRole()}, Team={team}, Sequence={sequence}, Disabled=0, Reason=BuildingStatusSystemMissing"));
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// 스킬 건물(FlightFacility / MagicBuilding)인지 여부.
        /// 종족별 스킬 건물은 정확히 이 두 enum 값 중 하나다(규칙 1 — MagicBuilding은 Spirit/Trans 공유).
        /// </summary>
        /// <param name="type">건물 타입.</param>
        private static bool IsSkillBuilding(BuildingType type)
        {
            return type == BuildingType.FlightFacility || type == BuildingType.MagicBuilding;
        }
    }
}
