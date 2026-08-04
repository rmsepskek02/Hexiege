// ============================================================================
// StatusEffect.cs
// 유닛에 걸린 상태효과 1개의 값(종류 · 강도 · 남은 시간 · 출처 팀).
//
// 무엇인가(초급자용 설명):
//   "이 유닛에게 지금 어떤 상태가, 얼마의 강도로, 앞으로 몇 초 더 걸려 있는가"를 담는 작은 값 묶음이다.
//   StatusEffectSystem(Application)이 매 서버 틱에서 RemainingDuration을 줄이고, 0 이하가 되면 제거한다.
//   유효 스탯 접근자(EffectiveAttack/GetUnitMoveSpeedMultiplier/CanAttack)는 이 값을 읽어 배율/게이트를 산출한다.
//
// 왜 RemainingDuration만 mutable인가:
//   상태의 정체성(종류·강도·출처)은 부여 순간 고정이지만, 남은 시간은 매 틱 줄어드는 유일한 가변 상태다.
//   그래서 남은 시간만 set 가능한 프로퍼티로 두고 나머지는 불변(readonly)으로 둔다.
//
// Domain 레이어 — 순수 C#. UnityEngine / Hexiege.Core 참조 금지.
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// 유닛에 걸린 상태효과 1개(종류·강도·남은 시간·출처 팀). 남은 시간만 매 틱 갱신된다.
    /// </summary>
    public sealed class StatusEffect
    {
        /// <summary> 상태 종류(이속 배율/공격 배율/공격 불가/빙결 등). </summary>
        public StatusEffectKind Kind { get; }

        /// <summary> 강도(배율·회복량 등 — 종류별 해석은 <see cref="StatusEffectKind"/> 주석 참조). </summary>
        public float Magnitude { get; }

        /// <summary> 상태를 건 팀(연출/어트리뷰션용). </summary>
        public TeamId SourceTeam { get; }

        /// <summary> 남은 지속시간(초). StatusEffectSystem.Tick이 매 틱 감소시킨다. </summary>
        public float RemainingDuration { get; set; }

        /// <summary>
        /// 상태효과 생성.
        /// </summary>
        /// <param name="kind">상태 종류.</param>
        /// <param name="magnitude">강도(배율·회복량 등).</param>
        /// <param name="duration">지속시간(초).</param>
        /// <param name="sourceTeam">상태를 건 팀.</param>
        public StatusEffect(StatusEffectKind kind, float magnitude, float duration, TeamId sourceTeam)
        {
            Kind = kind;
            Magnitude = magnitude;
            RemainingDuration = duration;
            SourceTeam = sourceTeam;
        }
    }
}
