// ============================================================================
// SkillActivationUseCase.ImpactBurn.cs
// 타입 A 착탄 화상(메테오 — 지원 스킬 2-D, 2026-11-18)의 서버 쪽: 착탄 처리 선택 · 진단 [SKILL-METEOR].
// Task: Assets/_Project/Docs/_Tasks/2026-11-18/03_34_support-skill-meteor/ (Plan P-1 · P-2 · P-3 · U-5 · U-9, 6절 T2-L4 · T2-L7).
//
// 무엇을 하나(초급자용 설명):
//   메테오는 새 메커니즘이 아니라 포격과 같은 「타입 A 즉발 범위 피해 + 착탄 지연(1.5초)」이다. 다른 점은 착탄 순간
//   살아 남은 적 유닛에게 화상을 거는 것 하나뿐이다. 그래서 포격이 쓰는 「착탄 예약」(PendingImpact)을 그대로 쓰고,
//   예약에 화상 값 두 칸(초당 피해 · 지속)을 함께 실어 두었다가 착탄 때 화상 단계를 더 부른다.
//     - 화상 칸이 0인 예약(포격): 예전과 똑같이 ApplySkillInstantAreaDamage만 부른다(동작 무변경).
//     - 화상 칸이 있는 예약(메테오): UnitCombatUseCase.ApplySkillInstantAreaDamageWithBurn을 부른다(피해 → 살아 남은 적 유닛 화상).
//   착탄 지연이 0인 타입 A에 화상만 있는 경우도 같은 메서드를 바로 부른다(착탄 지연을 0으로 바꿔도 화상이 남는다 — 위험 R-9).
//
// 지킨 결정:
//   - 착탄 전에 시전 건물이 파괴돼도 착탄 · 화상은 그대로(U-5) — 착탄 처리에서 건물 생존을 보지 않는다(포격 결정 a와 같다).
//   - 경기가 끝나면 남은 예약은 버린다(ClosePendingImpacts — 기존 그대로, 진단 [SKILL-VFX] ImpactDropped).
//   - 착탄 비교에 공습의 작은 여유값(1e-4초)을 넣지 않는다(P-7 — 포격과 같은 비교).
//   - 서버 권위 틱(TickPendingImpacts)에서만 처리한다 — 멀티 순수 Client에는 예약이 없고 피해 · 화상은 동기화로 받는다.
//
// 진단 로그(개발 축 · [Conditional] — 릴리스 빌드에서는 호출 · 문자열 조립이 사라진다. 착탄 1회당 1줄, U-9 존속 = 개발):
//   머리 [SKILL-METEOR] · Diag=SkillMeteor · DiagRev=1
//     - MeteorBurnApplied: 착탄 순간 원 안 적 유닛 · 건물 수, 피해 뒤 생존자 수, 화상이 걸린 수, 이미 화상이라 갱신된 수,
//       화상이 아닌 지속 피해를 덮어쓴 수(실제 경기에서 0), 초당 피해 · 지속 ms, 시전 건물 생존, 발동 뒤 ms.
//   착탄 시각 · 맞은 수는 기존 [SKILL-VFX] ImpactApplied가, 대상별 화상 부여 · 갱신은 기존 TimedDamageEffectChannel 줄
//   (Stage=Grant/Refresh · Source=-1 · Visual=Burn)이 남긴다(Plan 6절).
//
// Application 레이어 — Domain 의존. Unity.Netcode 직접 참조 없음.
// ============================================================================

using System;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public sealed partial class SkillActivationUseCase
    {
        /// <summary>
        /// 다 된 착탄 예약 1건의 피해를 적용한다(TickPendingImpacts에서 부른다).
        /// 화상 칸이 0이면 옛 착탄 호출(ApplyInstantAreaDamageBridge)과 같은 일을 하고, 화상이 있으면 피해 + 화상을 함께 적용한다.
        /// </summary>
        /// <param name="p">착탄할 예약 1건(원형 — 공습 폭격이 아닌 것).</param>
        private void ApplyPendingImpactDamage(PendingImpact p)
        {
            if (!(p.BurnPerSecond > 0f) || !(p.BurnDuration > 0f))
            {
                // 화상 없음(포격) — 예전과 같은 호출(옛 줄 그대로).
                ApplyInstantAreaDamageBridge(p.Team, p.Center, p.Radius, p.RawDamage, p.SourceBuildingId);
                return;
            }
            if (_combat == null) return; // 조합 루트 미배선(테스트) — 옛 다리와 같은 무효과.

            SkillImpactBurnResult result = _combat.ApplySkillInstantAreaDamageWithBurn(
                p.Team, p.Center, p.Radius, p.RawDamage, p.SourceBuildingId, p.BurnPerSecond, p.BurnDuration);
            // [진단 T2-L4 · T2-L7] 착탄 1회당 1줄 — 화상이 살아 남은 적 유닛에게만 걸렸는가 · 시전 건물 생존.
            LogMeteorBurnApplied(p.Team, p.SourceBuildingId, p.Sequence, p.Delay, p.Delay - p.Remaining,
                                 p.BurnPerSecond, p.BurnDuration, result);
        }

        /// <summary>
        /// (착탄 지연 0 + 화상) 지금 바로 피해 + 화상을 적용한다. 타입 A 실행기가 다리를 부른 순간(발동 순간)이 곧 착탄이다.
        /// </summary>
        private void ApplyInstantAreaDamageWithBurnNow(TeamId team, Vector3 center, float radius, int rawDamage, int sourceBuildingId,
                                                       float burnPerSecond, float burnDuration, int sequence)
        {
            if (_combat == null) return;
            SkillImpactBurnResult result = _combat.ApplySkillInstantAreaDamageWithBurn(
                team, center, radius, rawDamage, sourceBuildingId, burnPerSecond, burnDuration);
            LogMeteorBurnApplied(team, sourceBuildingId, sequence, 0f, 0f, burnPerSecond, burnDuration, result);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogMeteorBurnApplied(TeamId team, int sourceBuildingId, int sequence, float delay, float elapsed,
                                          float burnPerSecond, float burnDuration, SkillImpactBurnResult r)
        {
            try
            {
                BuildingData caster = _buildingPlacement?.GetBuilding(sourceBuildingId);
                bool casterAlive = caster != null && caster.IsAlive;
                // BuildingBurn은 언제나 0이다 — 이 코드는 건물에 화상을 걸지 않는다(상위 결정 18). 「원 안 건물이 있었는데 0」을 한 줄로 보이려고 남긴다.
                GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-METEOR] MeteorBurnApplied",
                    FormattableString.Invariant(
                        $"Diag=SkillMeteor, DiagRev=1, Event=MeteorBurnApplied, Role={DiagRole()}, Team={team}, BuildingId={sourceBuildingId}, Seq={sequence}, DelayMs={DiagMilli(delay)}, MsAfterActivation={DiagMilli(elapsed)}, UnitsInCircle={r.UnitsInCircle}, BuildingsInCircle={r.BuildingsInCircle}, Survivors={r.Survivors}, BurnGranted={r.BurnGranted}, Refreshed={r.RefreshedBurn}, ReplacedOther={r.ReplacedOther}, BuildingBurn=0, PerSecond={burnPerSecond:0.###}, DurationMs={DiagMilli(burnDuration)}, CasterAlive={casterAlive}"));
            }
            catch { /* 진단 전용 — 서버 착탄 처리를 막지 않는다 */ }
        }
    }
}
