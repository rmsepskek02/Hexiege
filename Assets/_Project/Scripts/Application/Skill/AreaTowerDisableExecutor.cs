// ============================================================================
// AreaTowerDisableExecutor.cs
// 「지점 범위 적 방어탑 무력화」(EMP) 스킬 실행기. 2026-11-04 EMP Task 커밋 1(Plan 3-1 M1).
//
// 무엇을 하나(유니티 초급자용 설명):
//   플레이어가 지도의 한 지점을 골라 EMP를 쓰면, 그 지점을 중심으로 한 원(반경 = 스킬 데이터의 Radius) 안에
//   "지금" 서 있는 적 방어탑이 정해진 시간(스킬 데이터의 Duration) 동안 공격을 멈춘다.
//   이 실행기는 스킬 데이터에서 반경 · 시간을 꺼내 컨텍스트의 「범위 안 적 방어탑 무력화」 다리를 부르기만 한다.
//   실제로 탑을 고르고 상태를 거는 일은 BuildingStatusEffectSystem이 한다(서버 권위 — 스킬 규칙 25).
//   (타입 A · B 실행기가 ctx.ApplyInstantAreaDamage / ApplyAreaDot을 부르는 것과 같은 역할 분담이다.)
//
// 지키는 결정:
//   · 대상은 "발동 순간" 원 안의 적 탑이다(상위 결정 22 · 사용자 Q3 「V5 · V6 동시 시작, 지연 0」).
//     → 착탄 지연(ImpactDelay)은 이 타입에서 쓰지 않는다. 지연 장치는 타입 A 다리에만 있다.
//   · 유닛에게는 아무 영향이 없다 — 피해도 상태도 주지 않는다(TotalDamage 값은 읽지 않는다).
//   · 쿨다운은 이 실행기와 무관하게 SkillActivationUseCase가 발동 순간 시작한다(스킬 규칙 3).
//
// Application 레이어 — Domain 의존. UnityEngine은 Mathf만 사용.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 지점 범위 적 방어탑 무력화(EMP, <see cref="SkillMechanicType.AreaTowerDisable"/>) 실행기.
    /// 조준 지점 원형 반경 안의 적 방어탑을 지속시간 동안 공격 불가로 만든다.
    /// </summary>
    public sealed class AreaTowerDisableExecutor : ISkillExecutor
    {
        /// <summary>
        /// 범위 무력화를 실행한다. 지점 지정 스킬이므로 조준 좌표(HasAim)가 반드시 있어야 한다.
        /// </summary>
        /// <param name="ctx">스킬 데이터 · 시전 팀 · 조준 좌표 · 무력화 다리를 담은 컨텍스트.</param>
        public void Execute(SkillActivationContext ctx)
        {
            if (ctx == null || ctx.Skill == null) return;

            // 지점 지정 스킬 — 조준 좌표가 없으면 무력화할 지점이 없으므로 무시(방어적).
            //   (자산의 조준 방식이 "지점"이 아니게 잘못 설정된 경우에도 여기서 조용히 멈춘다.)
            if (!ctx.HasAim) return;

            // 무력화 시간이 없으면 상태가 성립하지 않으므로 무시(자산 미설정 방어). !(x > 0)은 NaN도 거른다.
            float duration = ctx.Skill.Duration;
            if (!(duration > 0f)) return;

            // 반경이 0 이하면 아무 탑도 걸리지 않으므로 무시.
            float radius = Mathf.Max(0f, ctx.Skill.Radius);
            if (!(radius > 0f)) return;

            // 실제 대상 선정(적 · 생존 · AutoTower · 타일 중심 거리)과 상태 부여는 서버 권위 서비스가 담당.
            ctx.DisableEnemyTowersInArea(ctx.AimWorld, radius, duration);
        }
    }
}
