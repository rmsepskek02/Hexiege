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

        // ── 타입 C(전역 상태변경) 필드 — Phase 1에서는 스키마에만 존재(사용은 Phase 2) ──

        /// <summary> (C) 상태 종류(이속 배율/공격 불가/힐 등). Phase 2 사용. 규칙 13. </summary>
        public int StatusKind { get; }

        /// <summary> (C) 강도(이속 배율·힐량 등). Phase 2 사용. 규칙 13. </summary>
        public float Magnitude { get; }

        /// <summary> (C) 아군(true, 긍정)/적(false, 부정) 대상. Phase 2 사용. 규칙 13. </summary>
        public bool TargetsAllies { get; }

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
            bool targetsAllies)
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
        }
    }
}
