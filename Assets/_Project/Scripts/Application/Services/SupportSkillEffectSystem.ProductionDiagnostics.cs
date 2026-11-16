// ============================================================================
// SupportSkillEffectSystem.ProductionDiagnostics.cs
// 전술 지령 생산 속도(팀 생산 배율) 서버 진단 [SKILL-PROD] (Diag=SkillProd, DiagRev=1 — 개발 축, Plan S-15).
// 2026-11-16 지원 스킬 새 상태 Task 단계 A — 구현 단위 A-3. Plan 7절 A-L15 · A-L18 · A-L21.
//
// 남기는 줄(상태가 바뀌는 순간만):
//   ProdSpeedApplied (A-L15)          효과를 건 직후 — 시전 팀 · 받는 팀 · 배율 · 지속 · 교체 여부(Refreshed=1이면 남은 효과를 교체).
//   ProdSpeedEnded   (A-L18 · A-L21)  효과 끝 — Reason=Expire(8초) · GameEnd · Replaced, 실제 시간(ActualMs).
//   생산 1건의 실제 빨라짐은 UnitProductionUseCase.SpeedDiagnostics.cs의 ProdCompleteSample, 순수 클라이언트 막대 거울은
//   ProductionSpeedMirror의 ProdMirrorApplied / ProdMirrorEnded가 남긴다.
//
// 지키는 것: 호출하는 쪽(SupportSkillEffectSystem.Diagnostics.cs의 DiagBatchApplied/Ended)이 [Conditional] · try/catch로 감싼다.
//
// Application 레이어.
// ============================================================================

namespace Hexiege.Application
{
    public sealed partial class SupportSkillEffectSystem
    {
        partial void DiagProductionBatchApplied(EffectBatch b)
        {
            GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-PROD] ProdSpeedApplied",
                System.FormattableString.Invariant(
                    $"Diag=SkillProd, DiagRev=1, Event=ProdSpeedApplied, Role={DiagRole()}, Seq={b.Sequence}, Team={b.CasterTeam}, TargetTeam={b.TargetTeam}, Slot={b.Slot}, BuildingId={b.BuildingId}, Refreshed={b.DiagRefreshed}, PrevRemainingMs={DiagMilli(b.DiagPrevRemaining)}, MagMilli={DiagMilli(b.Magnitude)}, DurationMs={DiagMilli(b.Duration)}"));
        }

        partial void DiagProductionBatchEnded(EffectBatch b, string reason)
        {
            GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-PROD] ProdSpeedEnded",
                System.FormattableString.Invariant(
                    $"Diag=SkillProd, DiagRev=1, Event=ProdSpeedEnded, Role={DiagRole()}, Seq={b.Sequence}, Team={b.CasterTeam}, TargetTeam={b.TargetTeam}, Slot={b.Slot}, Reason={reason}, ActualMs={DiagMilli(b.Elapsed)}, DurationMs={DiagMilli(b.Duration)}, MagMilli={DiagMilli(b.Magnitude)}"));
        }
    }
}
