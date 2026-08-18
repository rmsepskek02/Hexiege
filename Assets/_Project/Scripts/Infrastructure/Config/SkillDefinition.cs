// ============================================================================
// SkillDefinition.cs
// 스킬 1개를 데이터로 저작하는 ScriptableObject(규칙 7).
//
// 무엇인가(초급자용 설명):
//   스킬의 "그릇(틀)"이다. 아이콘·조준 방식·쿨다운·타입·수치(반경/피해/지속/상태)를 Inspector에서
//   채워 넣는다. 코드는 타입별 실행기만 구현하고, 개별 스킬 값은 이 SO로 주입한다.
//   (SpecialAttackConfig / BuildingStatsConfig와 동일한 Infrastructure Config SO 패턴.)
//
//   ⚠️ ×10 스케일 주의(main 병합): 전투 스탯이 ×10로 커졌고 DamageCalculator(K=120)가 그 스케일에
//      맞춰졌다. TotalDamage / DamagePerSecond / Magnitude(힐량 등)는 반드시 ×10 스케일로 저작한다
//      (구 감각 "10 피해" ≈ 신 100). 유닛 공격력(UnitStatsConfig, ×10 반영값)과 같은 축으로 맞출 것.
//
//   타입 C(전역 상태변경) 필드(StatusKind/Magnitude/TargetsAllies)는 스키마에 함께 두되,
//   실제 사용은 Phase 2다(Phase 1엔 타입 A·B만 실행됨).
//
// 개별 스킬 목록·수치는 이번 범위 밖(데이터로 별도 확정). 이 SO는 스키마만 정의한다.
//
// Infrastructure 레이어 — ScriptableObject. Application(SkillData)/Domain(enum) 참조.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 스킬 1개의 데이터 정의(ScriptableObject). Inspector에서 저작하고, 런타임에 SkillData로 변환된다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDefinition", menuName = "Hexiege/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Header("표시")]
        [Tooltip("스킬 버튼에 표시할 아이콘(플레이스홀더 가능, 비워도 됨).")]
        [SerializeField] private Sprite _icon;

        [Header("발동")]
        [Tooltip("조준 방식. Instant=버튼 탭 즉시(타입 C), PointTarget=지점 지정(타입 A·B).")]
        [SerializeField] private SkillAimType _aimType = SkillAimType.PointTarget;

        [Tooltip("발동 후 건물 글로벌 쿨다운(초). 규칙 3 — 이 건물의 모든 스킬이 함께 잠긴다.")]
        [SerializeField] private float _cooldown = 10f;

        [Header("메커니즘")]
        [Tooltip("실행 방식. A=즉발 범위 피해 / B=범위 지속 피해(장판) / C=전역 상태변경(Phase 2).")]
        [SerializeField] private SkillMechanicType _mechanic = SkillMechanicType.InstantAreaDamage;

        [Header("범위/지속 (타입 A·B)")]
        [Tooltip("(A/B) 원형 반경(월드 단위). 헥스 인접 1칸이 대략 1.0.")]
        [SerializeField] private float _radius = 1.0f;

        [Tooltip("(B) DoT 지속시간 / (C) 상태 지속시간(초).")]
        [SerializeField] private float _duration = 3f;

        [Header("피해 (×10 스케일로 저작)")]
        [Tooltip("(A) 즉발 1회 총 피해. ×10 스케일 — 유닛 공격력과 같은 축으로 저작.")]
        [SerializeField] private int _totalDamage = 100;

        [Tooltip("(B) 초당 피해. ×10 스케일.")]
        [SerializeField] private float _damagePerSecond = 20f;

        [Header("상태변경 (타입 C — Phase 2 사용)")]
        [Tooltip("(C) 상태 종류(정수 코드). Phase 2에서 StatusEffectKind와 매핑. Phase 1 미사용.")]
        [SerializeField] private int _statusKind = 0;

        [Tooltip("(C) 강도(이속 배율·힐량 등, ×10 스케일). Phase 2 미사용.")]
        [SerializeField] private float _magnitude = 0f;

        [Tooltip("(C) 대상. true=아군(긍정), false=적(부정). Phase 2 미사용.")]
        [SerializeField] private bool _targetsAllies = false;

        /// <summary>
        /// 이 SO의 값을 Application 레이어 값 객체(SkillData)로 변환한다.
        /// 실행기/유스케이스/UI가 Infrastructure SO에 직접 의존하지 않도록 하는 경계(의존성 역전).
        /// </summary>
        /// <returns>이 정의로 만든 불변 SkillData.</returns>
        public SkillData ToSkillData()
        {
            return new SkillData(
                _icon,
                _aimType,
                _cooldown,
                _mechanic,
                _radius,
                _duration,
                _totalDamage,
                _damagePerSecond,
                _statusKind,
                _magnitude,
                _targetsAllies);
        }
    }
}
