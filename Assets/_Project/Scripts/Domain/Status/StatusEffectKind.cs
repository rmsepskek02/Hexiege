// ============================================================================
// StatusEffectKind.cs
// 유닛에 걸리는 "상태효과(버프/디버프/제어/회복)"의 종류를 구분하는 열거형.
//
// 무엇을 구분하나(초급자용 설명):
//   타입 C 스킬(전역 상태변경)은 대상 유닛의 스탯·행동을 일정 시간 동안 바꾼다.
//   버프(공격 강화)·디버프(공격 약화)·제어(둔화/빙결)·회복을 모두 이 하나의 상태변경
//   시스템으로 표현한다(규칙 13). 이 열거형은 "어떤 방식으로 스탯/행동을 바꿀지"를 가리키는 키다.
//
//   ⚠️ 정수 값 고정: 데이터(SkillDefinition._statusKind, int)가 이 열거형 값과 1:1로 매핑되므로
//      기존 멤버의 정수 값을 바꾸지 말 것(직렬화·데이터 안정성). 새 멤버는 뒤에 추가한다.
//
//   각 멤버의 Magnitude(강도) 해석:
//     - MoveSpeedMul   : 이동속도 배율(0<m<1 = 둔화). 예: 0.5 = 이동속도 절반.
//     - AttackPowerMul : 공격력 배율(m>1 = 버프, 0<m<1 = 디버프). 예: 1.5 = 공격력 1.5배.
//     - AttackDisabled : 강도 무시(공격만 봉쇄, 이동은 가능 — 기절 등).
//     - Freeze         : 강도 무시(빙결 = 이동속도 0 + 공격 불가를 하나로 표현 — 규칙 13).
//     - HealOverTime   : 지속 회복 "총량"(HP). 이 종류는 상태 시스템에 저장하지 않고 기존 HoT를
//                        재사용한다(회복 HP는 이미 네트워크로 동기화되므로 상태 배율 합성 대상이 아님).
//
// Domain 레이어 — 순수 C#. UnityEngine / Hexiege.Core 참조 금지.
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// 상태효과(버프/디버프/제어/회복)의 종류. 타입 C 스킬이 부여하며, 유효 스탯 접근자가 배율로 합성한다(규칙 13).
    /// </summary>
    public enum StatusEffectKind
    {
        /// <summary> 없음(기본값). 데이터 미설정 시 무효과. </summary>
        None = 0,

        /// <summary> 이동속도 배율(Magnitude=배율, 0&lt;m&lt;1=둔화). GetUnitMoveSpeedMultiplier에 곱연산 합성. </summary>
        MoveSpeedMul = 1,

        /// <summary> 공격력 배율(Magnitude=배율, m&gt;1=버프/0&lt;m&lt;1=디버프). EffectiveAttack에 곱연산 합성. </summary>
        AttackPowerMul = 2,

        /// <summary> 공격 불가(기절 — 이동은 가능, 공격만 봉쇄). CanAttack 게이트가 false를 반환하게 한다. </summary>
        AttackDisabled = 3,

        /// <summary> 빙결 = 이동속도 0 + 공격 불가(규칙 13). 이동 배율 0 + CanAttack false를 하나로 표현. </summary>
        Freeze = 4,

        /// <summary> 지속 회복(Magnitude=총 회복량 HP). 상태 시스템 미저장 — 기존 HoT(ApplyTimedEffect)로 위임. </summary>
        HealOverTime = 5
    }
}
