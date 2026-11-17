// ============================================================================
// LineStrikeExecutor.cs
// 공습 — "직선 연속 폭격" 스킬 실행기(SkillMechanicType.LineStrike).
// (2026-11-17 지원 스킬 2-L 공습 Task — _Tasks/2026-11-17/09_12_support-skill-air-strike, Plan L-1 · P-1 · P-3)
//
// 무엇을 하나(초급자용 설명):
//   플레이어가 정한 시작점(조준 좌표)과 방향으로 직사각형을 만들고, 그 위를 시작점 → 끝점 순서로 여러 번 나눠 폭격하라고
//   발동 UseCase에 "예약"을 맡긴다. 이 실행기 자체는 스킬 데이터에서 길이 · 폭 · 횟수 · 지연 · 간격 · 피해를 꺼내
//   컨텍스트의 「직선 폭격 예약」 다리를 부르기만 한다(포격 실행기가 "피해를 줘"라고만 하는 것과 같은 역할 분담).
//   실제 예약 · 서버 권위 틱 처리 · 시전당 1회 피해 · 맵 밖 잘라내기 · 진단 로그는 SkillActivationUseCase가,
//   직사각형 안 대상 수집 · 피해 적용은 UnitCombatUseCase가 담당한다(서버 권위 — 스킬 규칙 25).
//
//   값 검사(길이 · 폭 · 횟수 · 피해가 0 이하인지)는 받는 쪽(SkillActivationUseCase.ScheduleLineStrikeBridge)이 한다
//   — 잘못된 자산이면 그쪽이 진단 Warn 1줄을 남기고 아무 폭격도 예약하지 않는다(타입 C 「BuffNoEffect」 선례).
//
// Application 레이어 — Domain 의존.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// 공습(직선 연속 폭격) 실행기. 조준 시작점 + 방향의 직사각형 폭격을 예약한다.
    /// </summary>
    public sealed class LineStrikeExecutor : ISkillExecutor
    {
        /// <summary>
        /// 직선 연속 폭격을 예약한다. 지점 지정 스킬이므로 조준 좌표(HasAim)가 반드시 있어야 한다.
        /// 방향은 SkillActivationUseCase.Activate가 이미 재검증 · 정규화한 값(AimDirection)을 쓴다.
        /// </summary>
        /// <param name="ctx">스킬 데이터 · 조준 좌표 · 방향 · 예약 다리 컨텍스트.</param>
        public void Execute(SkillActivationContext ctx)
        {
            if (ctx == null || ctx.Skill == null) return;

            // 지점 지정 스킬 — 시작점이 없으면 폭격할 곳이 없으므로 무시(방어적. 정상 경로에서는 Activate가 미리 막는다).
            if (!ctx.HasAim) return;

            SkillData s = ctx.Skill;
            // 첫 폭격 지연 = 스킬 데이터의 「착탄 지연」 칸(ImpactDelay). 포격과 같은 뜻(발동 → 첫 착탄)이라 칸을 새로 만들지 않았다.
            var plan = new LineStrikePlan(s.LineLength, s.LineWidth, s.LineStrikeCount, s.ImpactDelay, s.LineStrikeInterval,
                                          s.TotalDamage);
            ctx.ScheduleLineStrike(ctx.AimWorld, ctx.AimDirection, plan);
        }
    }
}
