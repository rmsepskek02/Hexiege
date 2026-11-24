// ============================================================================
// SupportSkillEffectSystem.ArmorDiagnostics.cs
// 대지의 갑주(받는 피해 배율) 서버 진단 [SKILL-ARMOR] (Diag=SkillArmor, DiagRev=1 — 개발 축, Plan S-15).
// 2026-11-16 지원 스킬 새 상태 Task 단계 A — 구현 단위 A-2. Plan 7절 A-L1 ~ A-L11.
//
// 남기는 줄(상태가 바뀌는 순간 · 상한이 있는 표본만 — 매 피격 줄 금지):
//   ArmorApplied        (A-L1)        효과를 건 직후 — 대상 수 · 배율 · 지속 · 교체한 대상 수.
//   DamageReducedSample (A-L2~A-L8)   효과 묶음마다 (유닛 · 피해 지점) 쌍의 첫 피격 1줄(묶음당 최대 40줄, 넘으면 SamplesDropped로만 센다).
//                                      Raw(감쇄 전) · AfterDefense(방어 감쇄 뒤 = 기존 계산 그대로) · AfterReduction(배율 뒤 = 실제 HP 피해) ·
//                                      Check=Match(진단이 따로 계산한 「반올림(AfterDefense × 배율) · 최소 1」과 같음)/Mismatch.
//   NewUnitHitSample    (A-L10)       효과 중 같은 팀의 「대상이 아닌 유닛」(발동 뒤 새로 나온 유닛)이 감소 없이 맞은 첫 3번.
//   ArmorEnded          (A-L9 · A-L11) 묶음 끝 — Reason=Expire(8초) · GameEnd · Replaced(재사용으로 교체) · AllTargetsGone(대상 전원 사망) ·
//                                      NoTargets, 실제 시간 · 지점별 감소 횟수 · 새 유닛 피격 수 · 표본 수.
//
// 지키는 것: [Conditional] 두 개(릴리스에서 통째로 사라짐) · 예외를 밖으로 던지지 않음 · 판정 저장소를 바꾸지 않음(묶음의 진단 칸만).
//
// Application 레이어.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public sealed partial class SupportSkillEffectSystem
    {
        // 묶음 하나가 남길 감소 표본 줄 상한(유닛 수 × 피해 지점이 많아도 로그가 넘치지 않게).
        private const int DiagArmorSampleCap = 40;
        // 묶음 하나가 남길 「새 유닛 감소 없음」 표본 줄 상한.
        private const int DiagNewUnitSampleCap = 3;

        partial void DiagArmorBatchApplied(EffectBatch b)
        {
            GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-ARMOR] ArmorApplied",
                System.FormattableString.Invariant(
                    $"Diag=SkillArmor, DiagRev=1, Event=ArmorApplied, Role={DiagRole()}, Seq={b.Sequence}, Team={b.CasterTeam}, TargetTeam={b.TargetTeam}, Slot={b.Slot}, BuildingId={b.BuildingId}, Targets={b.TargetCount}, Refreshed={b.DiagRefreshed}, PrevRemainingMs={DiagMilli(b.DiagPrevRemaining)}, MagMilli={DiagMilli(b.Magnitude)}, DurationMs={DiagMilli(b.Duration)}"));
        }

        partial void DiagArmorBatchEnded(EffectBatch b, string reason)
        {
            int[] c = b.DiagCounts;
            GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-ARMOR] ArmorEnded",
                System.FormattableString.Invariant(
                    $"Diag=SkillArmor, DiagRev=1, Event=ArmorEnded, Role={DiagRole()}, Seq={b.Sequence}, Team={b.CasterTeam}, TargetTeam={b.TargetTeam}, Slot={b.Slot}, Reason={reason}, ActualMs={DiagMilli(b.Elapsed)}, DurationMs={DiagMilli(b.Duration)}, MagMilli={DiagMilli(b.Magnitude)}, Targets={b.TargetCount}, Replaced={b.Replaced}, DeadTargets={b.DeadTargets}, Reduced={b.DiagReduced}, Direct={c[(int)IncomingDamageWriter.Direct]}, Projectile={c[(int)IncomingDamageWriter.Projectile]}, Sweep={c[(int)IncomingDamageWriter.Sweep]}, Wave={c[(int)IncomingDamageWriter.Wave]}, Splash={c[(int)IncomingDamageWriter.Splash]}, Skill={c[(int)IncomingDamageWriter.Skill]}, DoT={c[(int)IncomingDamageWriter.DoT]}, Tower={c[(int)IncomingDamageWriter.Tower]}, TowerShell={c[(int)IncomingDamageWriter.TowerShell]}, NewUnitHits={b.DiagNewUnitHits}, Samples={b.DiagSamples}, SamplesDropped={b.DiagSamplesDropped}"));
        }

        /// <summary>
        /// [진단] 피해 지점 헬퍼(IncomingDamage.Apply)가 유닛 피격마다 부른다. 대지의 갑주 묶음이 하나도 없으면 바로 끝난다.
        /// 걸린 유닛이면 지점별 감소 횟수를 세고 (유닛 · 지점)의 첫 피격만 표본 1줄, 같은 팀의 대상 아닌 유닛이면 「새 유닛 피격」을 센다.
        /// 에디터 · 개발 빌드 전용 — 릴리스에서는 호출 · 인자 계산이 사라진다. 판정 값은 바꾸지 않는다.
        /// </summary>
        /// <param name="unit">피격 유닛.</param>
        /// <param name="writer">피해 지점.</param>
        /// <param name="raw">감쇄 전 피해.</param>
        /// <param name="afterDefense">방어 감쇄 뒤 피해(헬퍼가 받은 값).</param>
        /// <param name="afterReduction">배율 뒤 실제 HP 피해(헬퍼가 TakeDamage에 넘긴 값).</param>
        /// <param name="multiplier">적용한 받는 피해 배율(효과 없으면 1).</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagRecordIncomingDamage(UnitData unit, IncomingDamageWriter writer, int raw, int afterDefense,
                                             int afterReduction, float multiplier)
        {
            try
            {
                if (_activeBatches.Count == 0 || unit == null) return;

                FindMagnitude(_unitEffects, unit.Id, StatusEffectKind.IncomingDamageMul, out EffectBatch batch);
                if (batch != null && !batch.Ended)
                {
                    int index = (int)writer;
                    if (index >= 0 && index < batch.DiagCounts.Length) batch.DiagCounts[index]++;
                    batch.DiagReduced++;

                    if (batch.DiagSampled == null) batch.DiagSampled = new HashSet<long>();
                    long key = ((long)unit.Id << 8) | (long)(index & 0xFF);
                    if (!batch.DiagSampled.Add(key)) return; // 이 (유닛 · 지점)은 이미 표본을 남겼다.
                    if (batch.DiagSamples >= DiagArmorSampleCap) { batch.DiagSamplesDropped++; return; }
                    batch.DiagSamples++;

                    // 진단이 따로 계산한 기대값(10진 정확 계산 — 헬퍼의 정수 계산과 독립): 반올림(감쇄 뒤 × 배율밀리 ÷ 1000) · 최소 1.
                    int mulMilli = DiagMilli(multiplier);
                    int expected = afterDefense;
                    if (afterDefense > 0 && mulMilli >= 0 && mulMilli != 1000)
                    {
                        decimal exact = (decimal)afterDefense * mulMilli / 1000m;
                        expected = (int)System.Math.Round(exact, System.MidpointRounding.AwayFromZero);
                        if (expected < 1) expected = 1;
                    }
                    string check = expected == afterReduction ? "Match" : "Mismatch";
                    GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-ARMOR] DamageReducedSample",
                        System.FormattableString.Invariant(
                            $"Diag=SkillArmor, DiagRev=1, Event=DamageReducedSample, Role={DiagRole()}, Seq={batch.Sequence}, TargetTeam={batch.TargetTeam}, UnitId={unit.Id}, UnitType={unit.Type}, Writer={writer}, Point={DiagPointOf(writer)}, Raw={raw}, AfterDefense={afterDefense}, AfterReduction={afterReduction}, Expected={expected}, Check={check}, MagMilli={mulMilli}, HpAfter={unit.Hp}"));
                    return;
                }

                // 걸리지 않은 유닛 — 같은 팀에 진행 중인 대지의 갑주 묶음이 있으면 「발동 뒤 새로 나온 유닛」(S-11 제외 대상)이다.
                for (int i = 0; i < _activeBatches.Count; i++)
                {
                    EffectBatch b = _activeBatches[i];
                    if (b.Ended || b.Kind != StatusEffectKind.IncomingDamageMul || b.TargetTeam != unit.Team) continue;
                    b.DiagNewUnitHits++;
                    if (b.DiagNewUnitSamples < DiagNewUnitSampleCap)
                    {
                        b.DiagNewUnitSamples++;
                        GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-ARMOR] NewUnitHitSample",
                            System.FormattableString.Invariant(
                                $"Diag=SkillArmor, DiagRev=1, Event=NewUnitHitSample, Role={DiagRole()}, Seq={b.Sequence}, TargetTeam={b.TargetTeam}, UnitId={unit.Id}, UnitType={unit.Type}, Writer={writer}, Point={DiagPointOf(writer)}, Damage={afterReduction}, Reduced=False"));
                    }
                    break;
                }
            }
            catch { /* 진단 전용 — 피해 적용을 막지 않는다 */ }
        }

        // 피해 지점 번호(Research 6-1 표의 ① ~ ⑥).
        private static string DiagPointOf(IncomingDamageWriter writer)
        {
            switch (writer)
            {
                case IncomingDamageWriter.Direct:
                case IncomingDamageWriter.Projectile:
                case IncomingDamageWriter.Sweep:
                case IncomingDamageWriter.Wave: return "P1";
                case IncomingDamageWriter.Splash: return "P2";
                case IncomingDamageWriter.Skill: return "P3";
                case IncomingDamageWriter.DoT: return "P4";
                case IncomingDamageWriter.Tower: return "P5";
                case IncomingDamageWriter.TowerShell: return "P6";
                default: return "P?";
            }
        }
    }
}
