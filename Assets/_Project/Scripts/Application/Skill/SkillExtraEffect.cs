// ============================================================================
// SkillExtraEffect.cs
// 스킬 하나가 「주 효과」 말고 더 거는 「추가 효과」 한 줄(대상 · 종류 · 강도)과, 그 대상 종류를 담는 열거형.
// 2026-11-16 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) 단계 A — 구현 단위 A-1(Plan S-13).
//
// 왜 필요한가(초급자용 설명):
//   지금까지 스킬 데이터(SkillData)는 상태를 "하나만" 걸 수 있었다 — 상태 종류(StatusKind) · 강도(Magnitude) 칸이 하나씩뿐이다.
//   그런데 전술 지령은 「공격력 1.5배」(유닛에 거는 효과)와 「생산 속도 1.5배」(팀에 거는 효과)를 함께 건다.
//   (단계 C · B의 신목의 각성 · 재생의 뿌리도 유닛 몫 + 건물 몫 + 회복 증폭처럼 2~3개가 필요하다 — Research 3절 3.)
//   그래서 기존 칸(주 효과)은 그대로 두고, 「추가 효과 목록」을 덧붙인다. 목록의 한 줄이 이 구조체 하나다.
//     예) 전술 지령: 주 효과 = 공격력 배율 1.5(기존 그대로) / 추가 효과 = [ 팀 · 생산 속도 배율 · 1.5 ]
//   지속시간 · 대상 팀(아군/적)은 스킬 데이터의 기존 칸(Duration · TargetsAllies)을 주 효과와 함께 쓴다.
//
// 값은 Infrastructure의 SkillDefinition(ScriptableObject) 「추가 효과 목록」 칸에서 복사된다(정수 코드 → 열거형).
//
// Application 레이어 — Domain(StatusEffectKind) 의존. Unity 의존 없음.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 효과가 붙는 대상의 종류. 데이터(SkillDefinition의 정수 칸)와 1:1로 매핑되므로 기존 값의 정수를 바꾸지 않는다(새 값은 뒤에).
    /// </summary>
    public enum SkillEffectTarget
    {
        /// <summary> 대상 팀의 살아 있는 유닛 각각(발동 순간 — 스킬 규칙 13 「대상 판정 시점」). 예: 받는 피해 배율. </summary>
        Unit = 0,

        /// <summary> 대상 팀의 살아 있는 건물 각각. 단계 C(쉴드) · B(재생)에서 쓴다 — 단계 A에서는 아직 받지 않는다. </summary>
        Building = 1,

        /// <summary> 대상 팀 자체(건물 Id와 무관 — 새로 짓거나 업그레이드한 건물에도 걸린다, Plan S-9). 예: 생산 속도 배율. </summary>
        Team = 2
    }

    /// <summary>
    /// 스킬의 추가 효과 한 줄(불변 값). 대상 종류 · 상태 종류 · 강도를 담는다.
    /// 지속시간과 대상 팀(아군/적)은 같은 스킬의 주 효과 칸(SkillData.Duration · TargetsAllies)을 따른다.
    /// </summary>
    public readonly struct SkillExtraEffect
    {
        /// <summary> 효과가 붙는 대상 종류(유닛 각각 · 건물 각각 · 팀). </summary>
        public SkillEffectTarget Target { get; }

        /// <summary> 상태 종류(예: ProductionSpeedMul). </summary>
        public StatusEffectKind Kind { get; }

        /// <summary> 강도(배율 등). 예: 1.5 = 1.5배. </summary>
        public float Magnitude { get; }

        /// <summary>
        /// 추가 효과 한 줄을 만든다.
        /// </summary>
        /// <param name="target">대상 종류.</param>
        /// <param name="kind">상태 종류.</param>
        /// <param name="magnitude">강도.</param>
        public SkillExtraEffect(SkillEffectTarget target, StatusEffectKind kind, float magnitude)
        {
            Target = target;
            Kind = kind;
            Magnitude = magnitude;
        }
    }
}
