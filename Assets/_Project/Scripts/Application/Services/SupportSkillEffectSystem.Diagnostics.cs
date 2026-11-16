// ============================================================================
// SupportSkillEffectSystem.Diagnostics.cs
// 지원 스킬 효과 서비스의 공통 진단(개발 축) — 2026-11-16 지원 스킬 새 상태 Task 단계 A, 구현 단위 A-1.
//
// 무엇을 하나(초급자용 설명):
//   효과를 "걸었다" · "끝났다" · "거부했다"를 눈이 아니라 로그로 판정하려고, 상태가 바뀌는 순간에만 한 줄씩 남긴다
//   (WORKFLOW [4] 「테스트 항목 진단 로그 규칙」 · Plan 7절). 이 파일은 공통 부분만 갖고, 종류별 줄의 모양은 따로 둔다:
//     - 받는 피해 배율 → [SKILL-ARMOR]  (SupportSkillEffectSystem.ArmorDiagnostics.cs — 구현 단위 A-2)
//     - 생산 속도 배율 → [SKILL-PROD]   (SupportSkillEffectSystem.ProductionDiagnostics.cs — 구현 단위 A-3)
//   종류별 줄은 C#의 「partial 메서드」로 연결했다. 구현 파일이 없으면 컴파일러가 호출을 지운다 → 단위별로 나눠 커밋해도 컴파일된다.
//
// 지키는 것:
//   - 모든 진입 메서드에 [Conditional] 두 개(UNITY_EDITOR · DEVELOPMENT_BUILD) — 릴리스 빌드에서는 호출 · 인자 계산 · 문자열 조립이
//     통째로 사라진다(LogRules 1.7). 진단은 판정 저장소를 바꾸지 않는다(묶음의 진단 전용 칸만 쓴다).
//   - 서버 경로(발동 · 권위 틱 · 피해 계산) 안에서 돌므로 예외를 밖으로 던지지 않는다(각 메서드가 삼킨다).
//   - 매 틱 · 매 피격 줄 금지(LogRules 1.14-8) — 적용 · 끝 · 거부 · 상한이 있는 표본만.
//   - 배율 · 시간은 밀리 정수(MagMilli=700 = 0.7배, DurationMs=8000 = 8초).
//
// Application 레이어 — NetworkContext(정적 홀더)로 역할만 읽는다.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public sealed partial class SupportSkillEffectSystem
    {
        /// <summary> 진단 로그 Role= 값(Single · Host · Client). </summary>
        private static string DiagRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        /// <summary> 초 · 배율 실수를 밀리 정수로(로그 값에 소수점 · 문화권 차이가 끼지 않게). </summary>
        private static int DiagMilli(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return -1;
            return Mathf.RoundToInt(value * 1000f);
        }

        // 종류별 줄 — 구현은 종류별 진단 파일(A-2 · A-3)에 있다. 구현이 없으면 호출이 지워진다(partial 메서드 규칙).
        partial void DiagArmorBatchApplied(EffectBatch batch);
        partial void DiagArmorBatchEnded(EffectBatch batch, string reason);
        partial void DiagProductionBatchApplied(EffectBatch batch);
        partial void DiagProductionBatchEnded(EffectBatch batch, string reason);

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagBatchApplied(EffectBatch batch)
        {
            try
            {
                if (batch.Kind == StatusEffectKind.IncomingDamageMul) DiagArmorBatchApplied(batch);
                else if (batch.Kind == StatusEffectKind.ProductionSpeedMul) DiagProductionBatchApplied(batch);
                // [2026-11-16 단계 C] 쉴드 → [SKILL-SHIELD] ShieldApplied(SupportSkillEffectSystem.ShieldDiagnostics.cs).
                else if (batch.Kind == StatusEffectKind.Shield) DiagShieldBatchApplied(batch);
            }
            catch { /* 진단 전용 — 서버 발동을 막지 않는다 */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagBatchEnded(EffectBatch batch, string reason)
        {
            try
            {
                if (batch.Kind == StatusEffectKind.IncomingDamageMul) DiagArmorBatchEnded(batch, reason);
                else if (batch.Kind == StatusEffectKind.ProductionSpeedMul) DiagProductionBatchEnded(batch, reason);
                // [2026-11-16 단계 C] 쉴드 → [SKILL-SHIELD] ShieldEnded(합계 줄).
                else if (batch.Kind == StatusEffectKind.Shield) DiagShieldBatchEnded(batch, reason);
            }
            catch { /* 진단 전용 — 서버 틱을 막지 않는다 */ }
        }

        // 효과를 걸지 못함(Warn — 정상이면 0건). 머리는 종류에 맞춘다(받는 피해 → [SKILL-ARMOR], 생산 → [SKILL-PROD]).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void DiagRejected(int sequence, TeamId casterTeam, int slot, SkillEffectTarget target,
                                         StatusEffectKind kind, float magnitude, float duration, string reason)
        {
            try
            {
                string head, diag;
                if (kind == StatusEffectKind.IncomingDamageMul) { head = "[SKILL-ARMOR]"; diag = "SkillArmor"; }
                else if (kind == StatusEffectKind.ProductionSpeedMul) { head = "[SKILL-PROD]"; diag = "SkillProd"; }
                // [2026-11-16 단계 C] 쉴드 거부(닫힌 뒤 · 잘못된 값 등)는 [SKILL-SHIELD] 머리로 — C-L13 「Close 뒤 ShieldApplied 0」 판정용.
                else if (kind == StatusEffectKind.Shield) { head = "[SKILL-SHIELD]"; diag = "SkillShield"; }
                else { head = "[SKILL-SUPPORT]"; diag = "SkillSupport"; }
                GameLog.Dev.Warn("Skill", nameof(SupportSkillEffectSystem), head + " EffectRejected",
                    System.FormattableString.Invariant(
                        $"Diag={diag}, DiagRev=1, Event=EffectRejected, Role={DiagRole()}, Seq={sequence}, Team={casterTeam}, Slot={slot}, Target={target}, Kind={kind}, MagMilli={DiagMilli(magnitude)}, DurationMs={DiagMilli(duration)}, Reason={reason}"));
            }
            catch { /* 진단 전용 */ }
        }
    }
}
