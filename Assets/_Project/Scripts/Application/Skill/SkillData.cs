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

        /// <summary> 스킬 이름(화면 표시용). 지금은 화면에 쓰이지 않는다(추후 UI 작업). null이면 빈 문자열. </summary>
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
        /// 지금은 읽는 코드가 없다 — 서버 지연 판정은 같은 Task 다음 단계에서 연결된다.
        /// </summary>
        public float ImpactDelay { get; }

        /// <summary>
        /// 발동 지점에 재생할 VFX 프리팹(null 허용 — 연출 없음). 아이콘(Sprite)과 같은 "연출 참조" 선례.
        /// 지금은 재생 코드가 없다 — 화면 재생 연결은 같은 Task 다음 단계.
        /// </summary>
        public GameObject ActivationVfxPrefab { get; }

        /// <summary>
        /// VFX 프리팹이 제작된 기준 반경(월드 단위). 재생 배율 = Radius ÷ 이 값(0 이하면 배율 1로 쓰도록 재생 쪽이 처리).
        /// </summary>
        public float VfxReferenceRadius { get; }

        /// <summary>
        /// 스킬 값 객체 생성. 모든 값은 데이터(SkillDefinition SO)에서 복사되어 불변으로 보관된다.
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
            float vfxReferenceRadius)
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
        }
    }
}
