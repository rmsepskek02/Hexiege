// ============================================================================
// AreaDotDamageExecutor.cs
// 타입 B — "범위 지속 피해(장판)" 스킬 실행기(규칙 12).
//
// 무엇을 하나(초급자용 설명):
//   조준한 지점(중심)의 원형 반경 안에 있는 적 유닛에게 지속시간 동안 도트(DoT) 피해를 준다.
//   실제 반경 수집·DoT 부여는 UnitCombatUseCase의 "스킬 출처 장판 DoT 경로"가 담당하고,
//   DoT 틱은 기존 TickTimedEffects가 그대로 소비한다(추가 틱 루프 불필요).
//   이 실행기는 스킬 데이터에서 반경·초당 피해·지속을 꺼내 그 경로를 호출하기만 한다.
//
// 방어 상호작용(확정 9-3):
//   타입 B DoT는 방어력 미적용(무감쇄)이다 — DoT 틱 sink가 방어 감쇄 경로를 우회한다(규칙과 일치).
//   즉 타입 A(감쇄)와 방어 상호작용이 다른 것은 의도된 설계다.
//
// Application 레이어 — Domain 의존.
// ============================================================================

using UnityEngine;

namespace Hexiege.Application
{
    /// <summary>
    /// 타입 B(범위 지속 피해 장판) 실행기. 조준 좌표 중심 원형 반경 안 적 유닛에게 DoT를 부여한다.
    /// </summary>
    public sealed class AreaDotDamageExecutor : ISkillExecutor
    {
        /// <summary>
        /// 범위 지속 피해(장판)를 실행한다. 지점 지정 스킬이므로 조준 좌표(HasAim)가 반드시 있어야 한다.
        /// </summary>
        /// <param name="ctx">스킬 데이터·조준 좌표·효과 적용 델리게이트 컨텍스트.</param>
        public void Execute(SkillActivationContext ctx)
        {
            if (ctx == null || ctx.Skill == null) return;

            // 타입 B는 지점 지정 스킬 — 조준 좌표가 없으면 발동할 지점이 없으므로 무시(방어적).
            if (!ctx.HasAim) return;

            float dps = ctx.Skill.DamagePerSecond;
            float duration = ctx.Skill.Duration;
            // 초당 피해/지속이 유효하지 않으면 DoT가 성립하지 않으므로 무시.
            if (dps <= 0f || duration <= 0f) return;

            float radius = Mathf.Max(0f, ctx.Skill.Radius);

            // 실제 반경 수집·DoT 부여는 UnitCombatUseCase가 담당(서버 권위). 틱은 TickTimedEffects가 소비.
            ctx.ApplyAreaDot(ctx.AimCoord, radius, dps, duration);
        }
    }
}
