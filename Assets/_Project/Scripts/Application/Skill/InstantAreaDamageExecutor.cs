// ============================================================================
// InstantAreaDamageExecutor.cs
// 타입 A — "즉발 범위 피해" 스킬 실행기(규칙 11).
//
// 무엇을 하나(초급자용 설명):
//   조준한 지점(중심)의 원형 반경 안에 있는 적 유닛·적 건물에게 1회(즉발) 피해를 준다.
//   실제 반경 수집·방어 감쇄·이벤트 발행·사망 처리는 UnitCombatUseCase의
//   "건물/스킬 출처 전용 즉발 피해 경로"가 담당한다(SkillActivationContext 델리게이트로 호출).
//   이 실행기는 스킬 데이터에서 반경·피해값을 꺼내 그 경로를 호출하기만 한다.
//
// 방어 상호작용(확정 9-3):
//   타입 A 즉발 피해는 DamageCalculator.ApplyDefense(방어 감쇄)를 통과한다(유닛/타워 데미지와 동일 공식).
//   단 스킬은 건물이 시전자라 Tank/CannonCart 건물 2배는 적용하지 않는다(확정 9-2).
//
// Application 레이어 — Domain 의존.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 타입 A(즉발 범위 피해) 실행기. 조준 좌표 중심 원형 반경 안 적에게 1회 피해를 준다.
    /// </summary>
    public sealed class InstantAreaDamageExecutor : ISkillExecutor
    {
        /// <summary>
        /// 즉발 범위 피해를 실행한다. 지점 지정 스킬이므로 조준 좌표(HasAim)가 반드시 있어야 한다.
        /// </summary>
        /// <param name="ctx">스킬 데이터·조준 좌표·효과 적용 델리게이트 컨텍스트.</param>
        public void Execute(SkillActivationContext ctx)
        {
            if (ctx == null || ctx.Skill == null) return;

            // 타입 A는 지점 지정 스킬 — 조준 좌표가 없으면 발동할 지점이 없으므로 무시(방어적).
            if (!ctx.HasAim) return;

            // 원본 피해(×10 스케일). 0 이하이면 의미 없으므로 무시.
            int rawDamage = ctx.Skill.TotalDamage;
            if (rawDamage <= 0) return;

            float radius = Mathf.Max(0f, ctx.Skill.Radius);

            // 실제 반경 수집·감쇄·피해·사망은 UnitCombatUseCase가 담당(서버 권위).
            ctx.ApplyInstantAreaDamage(ctx.AimCoord, radius, rawDamage);
        }
    }
}
