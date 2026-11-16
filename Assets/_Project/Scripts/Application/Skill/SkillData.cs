// ============================================================================
// SkillData.cs
// 스킬 1개의 "실행에 필요한 값"을 담는 Application 레이어 값 객체(불변).
//
// 왜 SkillDefinition(SO)이 아니라 별도 값 객체인가(레이어 규칙):
//   실제 스킬 데이터의 저작(오서링)은 Infrastructure의 ScriptableObject(SkillDefinition)에서
//   한다. 그런데 Application(실행기/유스케이스)이 Infrastructure의 구체 SO 타입을 직접 참조하면
//   "Application → Infrastructure 역참조 금지" 규칙을 어긴다.
//   그래서 ISkillDataProvider(Application 인터페이스)가 SO의 값을 이 SkillData(순수 값 객체)로
//   옮겨 담아 반환하고, 실행기는 SkillData만 본다(의존성 역전).
//
//   ⚠️ ×10 스케일 주의(main 병합): 전투 스탯이 ×10로 커졌고 DamageCalculator(K=120)가 그
//      스케일에 맞춰졌다. TotalDamage / DamagePerSecond / Magnitude(힐량 등)는 반드시
//      ×10 스케일로 저작한다(구 감각 "10 피해" ≈ 신 100). 데이터 저작 시 유닛 공격력과 같은 축.
//
// Application 레이어 — Domain 의존 + UnityEngine.Sprite(연출/아이콘 참조는 허용, SpecialAttackContext 선례).
//   2026-11-03: 발동 VFX 프리팹(UnityEngine.GameObject)도 같은 "연출 참조" 이유로 담는다(실행기는 읽지 않는다).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 스킬 1개의 실행 파라미터를 담는 불변 값 객체.
    /// Infrastructure의 SkillDefinition(SO)에서 값을 복사해 만들어지며, 실행기/유스케이스/UI가 공유한다.
    /// </summary>
    public sealed class SkillData
    {
        /// <summary> 스킬 버튼에 표시할 아이콘(플레이스홀더 가능, null 허용). </summary>
        public Sprite Icon { get; }

        /// <summary> 조준 방식(Instant=즉시 / PointTarget=지점 지정). 규칙 15·16. </summary>
        public SkillAimType AimType { get; }

        /// <summary> 발동 후 건물 글로벌 쿨다운(초). 규칙 3. </summary>
        public float Cooldown { get; }

        /// <summary> 실행 방식(A/B/C). 어떤 실행기를 쓸지 결정. 규칙 11~13. </summary>
        public SkillMechanicType Mechanic { get; }

        /// <summary> (A/B) 원형 반경(월드 단위). 규칙 11·12. </summary>
        public float Radius { get; }

        /// <summary> (B) DoT 지속시간 / (C) 상태 지속시간(초). 규칙 12·13. </summary>
        public float Duration { get; }

        /// <summary> (A) 즉발 1회 총 피해(×10 스케일). 규칙 11. </summary>
        public int TotalDamage { get; }

        /// <summary> (B) 초당 피해(×10 스케일). 규칙 12. </summary>
        public float DamagePerSecond { get; }

        // ── 타입 C(전역 상태변경) 실행기가 읽는 상태 종류·강도·대상 필드 ──

        /// <summary> (C) 상태 종류(이속 배율/공격 불가/힐 등). 실행기가 StatusEffectKind로 변환한다. 규칙 13. </summary>
        public int StatusKind { get; }

        /// <summary> (C) 강도(이속 배율·힐량 등). 전역 상태 적용에 전달한다. 규칙 13. </summary>
        public float Magnitude { get; }

        /// <summary> (C) 아군(true, 긍정)/적(false, 부정) 대상. 실행기가 대상 팀을 선택한다. 규칙 13. </summary>
        public bool TargetsAllies { get; }

        // ── 2026-11-03 추가: 이름 · 설명 · 사용 가능 · 착탄 지연 · 발동 VFX ──
        //   지원 스킬 Task(_Tasks/2026-11-03/05_24_support-skill-foundation-bombardment) 커밋 1에서 생겼다.
        //   값은 SkillDefinition(SO)의 같은 이름 칸에서 복사된다. 기존 자산은 초기값(사용 가능 true · 지연 0 ·
        //   프리팹 없음 · 기준 반경 2.0)으로 읽혀 동작이 바뀌지 않는다.

        /// <summary>
        /// 스킬 이름(화면 표시용). 스킬 패널 버튼 글자로 쓰인다(BuildingSkillPanelUI.ResolveSlotLabel — 2026-11-11부터).
        /// 이름이 비어 있으면(공백뿐 포함) 버튼은 실행 방식으로 지어 낸 임시 라벨로 폴백한다. null이면 빈 문자열.
        /// </summary>
        public string DisplayName { get; }

        /// <summary> 스킬 설명(화면 표시용). 지금은 화면에 쓰이지 않는다(추후 설명 탭). null이면 빈 문자열. </summary>
        public string Description { get; }

        /// <summary>
        /// 사용 가능 여부. false면 서버 발동이 거부되고(쿨다운 안 돎) 패널도 조준에 들어가지 않는다.
        /// 기능 코드가 아직 없는 스킬을 로드아웃에 이름만 넣어 둘 때 false로 둔다.
        /// </summary>
        public bool IsAvailable { get; }

        /// <summary>
        /// 발동부터 착탄(피해)까지의 시간(초). 0이면 즉발. 음수 · NaN은 0으로 고쳐 보관한다.
        /// 0보다 크면 SkillActivationUseCase가 타입 A 피해를 "착탄 예약"으로 저장했다가 이 시간 뒤 서버 틱에서 준다
        /// (2026-11-03 커밋 4에서 연결). 발동 표현(SkillActivationPresentation)에도 같은 값이 실린다.
        /// </summary>
        public float ImpactDelay { get; }

        /// <summary>
        /// 발동 지점에 재생할 VFX 프리팹(null 허용 — 연출 없음). 아이콘(Sprite)과 같은 "연출 참조" 선례.
        /// 화면 쪽 SkillActivationVfxPresenter가 발동 표현 이벤트를 받으면 조준 지점에 재생한다(2026-11-03 커밋 4에서 연결).
        /// 실행기 · 서버 판정은 이 값을 읽지 않는다.
        /// </summary>
        public GameObject ActivationVfxPrefab { get; }

        /// <summary>
        /// VFX 프리팹이 제작된 기준 반경(월드 단위). 재생 배율 = Radius ÷ 이 값(0 이하면 배율 1로 쓰도록 재생 쪽이 처리).
        /// </summary>
        public float VfxReferenceRadius { get; }

        // ── 2026-11-10 추가: 강화 상태 VFX(타입 C — 효과를 받는 유닛 몸에 붙는 반복 이펙트) ──
        //   지원 스킬 1차 강화 Task(_Tasks/2026-11-10/04_20_support-buff-skills-status-vfx) 구현 단위 ①.
        //   값은 SkillDefinition의 같은 이름 칸에서 복사된다. 실행기 · 서버 판정은 이 값을 읽지 않는다(화면 표현 전용).
        //   화면 쪽 UnitBuffVfxPresenter가 발동 표현 이벤트를 받으면 이 프리팹을 대상 유닛마다 붙인다.

        /// <summary>
        /// 효과를 받은 유닛 몸에 붙일 반복 VFX 프리팹(null 허용 — 이펙트 없이 효과만 건다).
        /// 발동 VFX(ActivationVfxPrefab)와 같은 "연출 참조" 선례라 GameObject로 담는다.
        /// </summary>
        public GameObject StatusVfxPrefab { get; }

        /// <summary>
        /// 강화 상태 VFX 크기 배율(프리팹 원본 × 이 값 × 유닛 크기 표 균등 배율). 0 이하 · NaN · 무한대는 1로 고쳐 보관한다.
        /// </summary>
        public float StatusVfxScale { get; }

        /// <summary>
        /// 강화 상태 VFX 위치 보정(유닛 기준 로컬 좌표, 월드 단위). NaN · 무한대가 섞이면 0 벡터로 고쳐 보관한다.
        /// </summary>
        public Vector3 StatusVfxOffset { get; }

        // ── 2026-11-11 추가: 구역 VFX(지속 상태 구역 — 바닥에 깔려 구역이 끝날 때까지 반복 재생되는 이펙트) ──
        //   지속 상태 구역 Task(_Tasks/2026-11-10/16_02_support-skill-persistent-zones) 구현 단위 ③, 사용자 결정 Z-13.
        //   값은 SkillDefinition의 같은 이름 칸에서 복사된다. 실행기 · 서버 판정은 이 값을 읽지 않는다(화면 표현 전용).
        //   크기는 발동 VFX와 같은 「반경 ÷ VfxReferenceRadius」(기준 반경 공유 — Z-13).

        /// <summary>
        /// 구역이 남아 있는 동안 조준 지점 바닥에 반복 재생할 VFX 프리팹(null 허용 — 이펙트 없이 효과만 동작).
        /// 화면 쪽 SkillZoneVfxPresenter가 발동 표현 이벤트를 받으면 재생하고, 구역 지속 시간이 지나면 꼬리 정지한다.
        /// </summary>
        public GameObject ZoneVfxPrefab { get; }

        // ── 2026-11-16 추가: 추가 효과 목록(지원 스킬 새 상태 — 단계 A, Plan S-13) ──
        //   주 효과(StatusKind · Magnitude) 하나로는 「공격력 + 생산 속도」(전술 지령)처럼 효과 둘 이상을 걸 수 없어서 덧붙였다.
        //   값은 SkillDefinition의 「추가 효과 목록」 칸에서 복사된다. 칸이 없는 옛 자산은 빈 목록으로 읽혀 동작이 그대로다.
        //   실행기(GlobalStatusChangeExecutor)가 목록의 줄마다 서버 전용 지원 스킬 효과 서비스로 보낸다.

        /// <summary>
        /// 추가 효과 목록(주 효과 말고 더 거는 효과들). 비어 있을 수 있지만 null은 아니다(호출자가 null 검사 없이 순회).
        /// 지속시간 · 대상 팀은 주 효과와 같다(Duration · TargetsAllies).
        /// </summary>
        public IReadOnlyList<SkillExtraEffect> ExtraEffects { get; }

        // 빈 목록 하나를 모든 스킬이 함께 쓴다(추가 효과가 없는 스킬마다 빈 배열을 새로 만들지 않게).
        private static readonly SkillExtraEffect[] NoExtraEffects = new SkillExtraEffect[0];

        // ── 2026-11-16 추가: 건물 상태 VFX(지원 스킬 새 상태 — 단계 C, Plan 6-1-3 · S-10 · S-12) ──
        //   효과를 받은 건물 위에 효과 시간 동안 반복 재생하는 이펙트(신목의 각성 건물 쉴드 · 단계 B 재생의 뿌리 건물 재생).
        //   값은 SkillDefinition의 「건물 상태 VFX」 칸에서 복사된다. 실행기 · 서버 판정은 이 값을 읽지 않는다(화면 표현 전용).
        //   화면 쪽 BuildingStatusVfxPresenter가 발동 표현 이벤트를 받으면 이 프리팹을 대상 건물마다 붙인다.

        /// <summary>
        /// 효과를 받은 건물 위에 붙일 반복 VFX 프리팹(null 허용 — 이펙트 없이 효과만 건다).
        /// </summary>
        public GameObject BuildingStatusVfxPrefab { get; }

        /// <summary>
        /// 건물 상태 VFX 크기 배율(프리팹 원본 × 이 값 — 건물 종류와 무관한 하나의 배율, S-12). 0 이하 · NaN · 무한대는 1로 고쳐 보관한다.
        /// </summary>
        public float BuildingStatusVfxScale { get; }

        /// <summary>
        /// 건물 상태 VFX 높이 보정(건물 GameObject 위치 기준 Y만, 월드 단위). NaN · 무한대는 0으로 고쳐 보관한다.
        /// X · Z 보정은 두지 않는다 — 상대 진영 건물은 180° 돌아 있어 X · Z 보정은 모델과 어긋난다(EMP 탑 VFX와 같은 이유).
        /// </summary>
        public float BuildingStatusVfxOffsetY { get; }

        /// <summary>
        /// 스킬 값 객체 생성. 모든 값은 데이터(SkillDefinition SO)에서 복사되어 불변으로 보관된다.
        /// 마지막 세 인자(강화 상태 VFX)는 생략하면 "프리팹 없음 · 배율 1 · 보정 0"이다(2026-11-10 추가 — 기존 호출 그대로 컴파일).
        /// 그 뒤 인자(구역 VFX 프리팹, 2026-11-11 추가)도 생략하면 "프리팹 없음"이다.
        /// 맨 뒤 인자(추가 효과 목록, 2026-11-16 추가)도 생략하면 "추가 효과 없음"이다 — 기존 호출은 고치지 않아도 그대로 컴파일된다.
        /// 그 뒤 세 인자(건물 상태 VFX — 2026-11-16 단계 C)도 생략하면 "프리팹 없음 · 배율 1 · 높이 보정 0"이다.
        /// </summary>
        public SkillData(
            Sprite icon,
            SkillAimType aimType,
            float cooldown,
            SkillMechanicType mechanic,
            float radius,
            float duration,
            int totalDamage,
            float damagePerSecond,
            int statusKind,
            float magnitude,
            bool targetsAllies,
            string displayName,
            string description,
            bool isAvailable,
            float impactDelay,
            GameObject activationVfxPrefab,
            float vfxReferenceRadius,
            GameObject statusVfxPrefab = null,
            float statusVfxScale = 1f,
            Vector3 statusVfxOffset = default,
            GameObject zoneVfxPrefab = null,
            IReadOnlyList<SkillExtraEffect> extraEffects = null,
            GameObject buildingStatusVfxPrefab = null,
            float buildingStatusVfxScale = 1f,
            float buildingStatusVfxOffsetY = 0f)
        {
            Icon = icon;
            AimType = aimType;
            Cooldown = cooldown;
            Mechanic = mechanic;
            Radius = radius;
            Duration = duration;
            TotalDamage = totalDamage;
            DamagePerSecond = damagePerSecond;
            StatusKind = statusKind;
            Magnitude = magnitude;
            TargetsAllies = targetsAllies;

            // 문자열은 null 대신 빈 문자열로 보관해 화면 쪽이 null 검사 없이 쓸 수 있게 한다.
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            IsAvailable = isAvailable;
            // Inspector 오입력 방어: "0보다 큰 값"만 지연으로 인정한다.
            //   (NaN > 0 은 false 라서 NaN도 0이 된다 — 별도 검사 불필요.)
            ImpactDelay = impactDelay > 0f ? impactDelay : 0f;
            ActivationVfxPrefab = activationVfxPrefab;
            VfxReferenceRadius = vfxReferenceRadius;

            // 강화 상태 VFX(2026-11-10). Inspector 오입력 방어:
            //   배율은 "0보다 크고 유한한 값"만 인정한다(0 · 음수면 이펙트가 안 보이거나 뒤집히고, NaN은 위치 계산을 망친다).
            //   위치 보정은 세 축이 모두 유한할 때만 인정하고, 아니면 보정 없음(0 벡터)으로 둔다.
            StatusVfxPrefab = statusVfxPrefab;
            StatusVfxScale = statusVfxScale > 0f && !float.IsInfinity(statusVfxScale) ? statusVfxScale : 1f;
            StatusVfxOffset = IsFiniteVector(statusVfxOffset) ? statusVfxOffset : Vector3.zero;

            // 구역 VFX(2026-11-11). 프리팹 참조만 담는다(크기는 발동 VFX 기준 반경을 공유한다 — Z-13).
            ZoneVfxPrefab = zoneVfxPrefab;

            // 추가 효과 목록(2026-11-16). 받은 목록을 새 배열로 복사해 보관한다 —
            //   호출자가 나중에 원본 목록을 바꿔도 이 값 객체는 바뀌지 않는다(불변 값 객체 약속).
            if (extraEffects == null || extraEffects.Count == 0)
            {
                ExtraEffects = NoExtraEffects;
            }
            else
            {
                var copy = new SkillExtraEffect[extraEffects.Count];
                for (int i = 0; i < copy.Length; i++) copy[i] = extraEffects[i];
                ExtraEffects = copy;
            }

            // 건물 상태 VFX(2026-11-16 단계 C). Inspector 오입력 방어는 유닛 상태 VFX와 같은 규칙이다.
            BuildingStatusVfxPrefab = buildingStatusVfxPrefab;
            BuildingStatusVfxScale = buildingStatusVfxScale > 0f && !float.IsInfinity(buildingStatusVfxScale) ? buildingStatusVfxScale : 1f;
            BuildingStatusVfxOffsetY = !float.IsNaN(buildingStatusVfxOffsetY) && !float.IsInfinity(buildingStatusVfxOffsetY)
                ? buildingStatusVfxOffsetY : 0f;
        }

        /// <summary>
        /// 이 스킬이 「건물 각각」에 거는 효과를 하나라도 가졌는가(추가 효과 목록의 대상 = 건물).
        /// 화면의 건물 상태 VFX 장치가 "이 발동에 건물 이펙트를 붙여야 하는가"를 이것으로 판단한다(2026-11-16 단계 C).
        /// 주 효과는 대상이 언제나 유닛이므로 보지 않는다.
        /// </summary>
        /// <returns>건물 대상 효과가 있으면 true.</returns>
        public bool HasBuildingEffect()
        {
            for (int i = 0; i < ExtraEffects.Count; i++)
                if (ExtraEffects[i].Target == SkillEffectTarget.Building && ExtraEffects[i].Kind != StatusEffectKind.None) return true;
            return false;
        }

        /// <summary>
        /// 이 스킬의 건물 대상 효과 가운데 쉴드가 있는가(쉴드 깨짐 알림으로 건물 이펙트를 바로 꺼야 하는가 — 2026-11-16 단계 C).
        /// </summary>
        /// <returns>건물 쉴드가 있으면 true.</returns>
        public bool HasBuildingShield()
        {
            for (int i = 0; i < ExtraEffects.Count; i++)
                if (ExtraEffects[i].Target == SkillEffectTarget.Building && ExtraEffects[i].Kind == StatusEffectKind.Shield) return true;
            return false;
        }

        /// <summary>
        /// 이 스킬이 지정한 상태 종류의 효과를 거는지 찾는다(주 효과 먼저, 없으면 추가 효과 목록에서 첫 줄).
        /// 서버 실행기와 순수 클라이언트 생산 막대 거울(UnitProductionUseCase.MirrorProductionSpeedFromSkill)이
        /// 같은 규칙으로 값을 찾도록 이 메서드 하나를 함께 쓴다(2026-11-16 추가).
        /// 주 효과는 대상이 「유닛」이다(타입 C 주 효과는 대상 팀 유닛 각각에 걸린다).
        /// </summary>
        /// <param name="kind">찾을 상태 종류.</param>
        /// <param name="target">찾은 효과의 대상 종류(못 찾으면 Unit).</param>
        /// <param name="magnitude">찾은 효과의 강도(못 찾으면 0).</param>
        /// <returns>찾았으면 true.</returns>
        public bool TryFindEffect(StatusEffectKind kind, out SkillEffectTarget target, out float magnitude)
        {
            if (kind != StatusEffectKind.None && (StatusEffectKind)StatusKind == kind)
            {
                target = SkillEffectTarget.Unit;
                magnitude = Magnitude;
                return true;
            }
            for (int i = 0; i < ExtraEffects.Count; i++)
            {
                if (ExtraEffects[i].Kind != kind) continue;
                target = ExtraEffects[i].Target;
                magnitude = ExtraEffects[i].Magnitude;
                return true;
            }
            target = SkillEffectTarget.Unit;
            magnitude = 0f;
            return false;
        }

        /// <summary>
        /// 시전 팀으로부터 효과를 받을 팀을 정한다 — 아군 대상이면 시전 팀, 적 대상이면 반대 팀
        /// (실행기 GlobalStatusChangeExecutor · 화면 UnitBuffVfxPresenter와 같은 규칙. Neutral은 그대로).
        /// </summary>
        /// <param name="casterTeam">스킬을 쓴 팀.</param>
        /// <returns>효과를 받을 팀.</returns>
        public TeamId ResolveTargetTeam(TeamId casterTeam)
        {
            if (TargetsAllies) return casterTeam;
            if (casterTeam == TeamId.Blue) return TeamId.Red;
            if (casterTeam == TeamId.Red) return TeamId.Blue;
            return casterTeam;
        }

        // 세 축이 모두 유한한 숫자인지(NaN · 무한대가 아닌지).
        private static bool IsFiniteVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
                && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
                && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }
    }
}
