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
//   타입 C(전역 상태변경) 필드(StatusKind/Magnitude/TargetsAllies)는 스키마에 함께 두며,
//   타입 C 실행기(GlobalStatusChangeExecutor)가 읽어 상태효과를 부여한다. 타입 A·B 실행기는 이 필드를 쓰지 않는다.
//
// 개별 스킬 목록·수치는 이번 범위 밖(데이터로 별도 확정). 이 SO는 스키마만 정의한다.
//
// 2026-11-03 추가 칸(지원 스킬 Task — _Tasks/2026-11-03/05_24_support-skill-foundation-bombardment):
//   이름 · 설명 · 사용 가능 · 착탄 지연(초) · 발동 VFX 프리팹 · VFX 기준 반경.
//   모든 새 칸의 초기값은 "지금 동작 그대로"가 되도록 골랐다(사용 가능 true · 지연 0 · 프리팹 없음).
//   그래서 이 칸이 생기기 전에 만든 자산은 다시 저장하지 않아도 예전과 똑같이 동작한다.
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

        // 스킬 이름 · 설명 칸(2026-11-03 추가 — 지원 스킬 Task 커밋 1).
        //   왜 데이터에 두는가: 스킬 규칙 7(데이터 주도) — 스킬 하나의 정보는 한 자산에 모은다.
        //   지금은 화면에 쓰이지 않는다. 슬롯 위 글자는 아직 BuildingSkillPanelUI의 "테스트용 임시 라벨"이
        //   실행 방식으로 지어 내며, 이 이름 · 설명을 화면에 띄우는 일은 추후 별도 UI 작업(아이콘 + 설명 탭) 범위다.
        //   기존 자산(이 칸이 없던 때 만든 것)은 빈 문자열로 읽힌다 — 동작에는 영향이 없다.
        [Tooltip("스킬 이름(화면 표시용 한글 이름). 지금은 화면에 쓰이지 않고, 추후 UI 작업에서 표시한다.")]
        [SerializeField] private string _displayName = string.Empty;

        [Tooltip("스킬 설명(무엇을 하는 스킬인지 한두 문장). 지금은 화면에 쓰이지 않고, 추후 설명 탭에서 표시한다.")]
        [TextArea(2, 5)]
        [SerializeField] private string _description = string.Empty;

        [Header("사용 가능 여부")]
        // 「사용 가능」 칸(2026-11-03 추가). 기능 코드가 아직 없는 스킬을 로드아웃에 "이름만" 넣어 둘 때 끈다.
        //   false면 서버 발동(SkillActivationUseCase.Activate)이 거부하고 쿨다운도 돌지 않으며,
        //   스킬 패널은 버튼을 눌러도 조준 모드에 들어가지 않는다.
        //   🔴 기본값 true인 이유(사용자 결정 Q2): 이 칸이 생기기 전에 만든 기존 스킬 자산 18개는 파일 안에
        //      이 칸이 없다. Unity는 파일에 없는 칸을 아래 초기값(true)으로 채우므로, 기존 자산은 지금처럼
        //      "사용 가능"으로 동작한다 — 기존 자산을 로드아웃에 다시 넣으면 그대로 되살아난다.
        [Tooltip("체크 해제 시 이 스킬은 눌러도 발동되지 않는다(기능 미구현 스킬용). 서버도 발동을 거부하고 쿨다운이 돌지 않는다.")]
        [SerializeField] private bool _isAvailable = true;

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
        [Tooltip("(C) 상태 종류(정수 코드). GlobalStatusChangeExecutor가 StatusEffectKind로 변환해 사용한다.")]
        [SerializeField] private int _statusKind = 0;

        [Tooltip("(C) 강도(이속 배율·힐량 등, ×10 스케일).")]
        [SerializeField] private float _magnitude = 0f;

        [Tooltip("(C) 대상. true=아군(긍정), false=적(부정).")]
        [SerializeField] private bool _targetsAllies = false;

        [Header("착탄 지연 (타입 A — 포격 · 메테오용)")]
        // 착탄 지연 칸(2026-11-03 추가). "발동한 순간"과 "포탄이 땅에 닿는 순간" 사이의 시간(초).
        //   예: 포격 지원은 포탄이 0.4초 동안 떨어진 뒤 터지므로 0.4.
        //   🔴 지금(커밋 1·2)은 이 값을 읽는 코드가 아직 없다 — 서버가 이 시간만큼 피해를 늦추는 장치는
        //      같은 Task의 다음 단계(커밋 4)에서 붙는다. 그전까지는 값이 무엇이든 지금처럼 즉시 피해다.
        //   0이면 지연 없음(지금과 같은 즉발). 음수는 SkillData에서 0으로 고친다.
        //   기존 자산은 파일에 이 칸이 없어 초기값 0으로 읽힌다 → 동작 무변경.
        [Tooltip("발동부터 착탄(피해)까지 걸리는 시간(초). 0=즉발. 서버 지연 판정은 다음 구현 단계에서 연결된다.")]
        [Min(0f)]
        [SerializeField] private float _impactDelay = 0f;

        [Header("발동 VFX (화면 연출)")]
        // 발동 VFX 칸(2026-11-03 추가). 스킬을 쓴 자리에 띄울 이펙트 프리팹과, 그 프리팹이 어느 반경 기준으로
        //   만들어졌는지(기준 반경)를 적는다. 화면에서는 "실제 반경 ÷ 기준 반경" 배율로 이펙트 크기를 맞춘다.
        //   예: 기준 반경 2.0으로 만든 포격 이펙트를 반경 3.0 스킬에 쓰면 1.5배로 키워 판정 범위와 맞춘다.
        //   🔴 지금(커밋 1·2)은 이 값을 읽어 재생하는 코드가 아직 없다 — 화면 재생 연결은 같은 Task의 다음 단계.
        //   기존 자산은 프리팹 없음(null) · 기준 반경 2.0으로 읽힌다 → 동작 무변경.
        [Tooltip("발동 지점에 재생할 VFX 프리팹(비워도 됨 — 비우면 연출 없음).")]
        [SerializeField] private GameObject _activationVfxPrefab;

        [Tooltip("VFX 프리팹이 제작된 기준 반경(월드 단위). 재생 배율 = 스킬 반경 ÷ 이 값. 0 이하면 배율 1.")]
        [SerializeField] private float _vfxReferenceRadius = 2.0f;

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
                _targetsAllies,
                _displayName,
                _description,
                _isAvailable,
                _impactDelay,
                _activationVfxPrefab,
                _vfxReferenceRadius);
        }
    }
}
