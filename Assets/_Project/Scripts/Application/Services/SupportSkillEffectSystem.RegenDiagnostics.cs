// ============================================================================
// SupportSkillEffectSystem.RegenDiagnostics.cs
// 재생의 뿌리(재생 · 받는 회복 배율) 서버 진단 [SKILL-REGEN] (Diag=SkillRegen, DiagRev=1 — 개발 축, Plan S-15).
// 2026-11-17 지원 스킬 새 상태 Task 단계 B — 구현 단위 B-1 · B-2. Plan 6-2-6 B-L1 ~ B-L9 · B-L13.
//
// 남기는 줄(상태가 바뀌는 순간 · 상한이 있는 표본만 — 매 틱 · 매 회복 줄 금지, LogRules 1.14-8):
//   RegenApplied        (B-L1)  효과를 건 직후 1줄 — 묶음마다(재생의 뿌리 1회 = 4줄: 유닛 재생 · 건물 재생 · 유닛 배율 · 건물 배율).
//                               Kind=Regen(MagMilli = 초당 회복량 × 1000, 20 → 20000) / HealAmpMul(MagMilli = 배율 × 1000, 1.5 → 1500) ·
//                               Target=Unit/Building · Targets · DurationMs.
//   RegenTickSample     (B-L2 · B-L7)  유닛마다 「실제로 HP가 오른 첫 재생 주기」 1줄(묶음당 최대 48줄).
//   BuildingRegenSample (B-L3)  같은 내용의 건물 줄.
//                               Base(증폭 전 1초 재생량 = 초당 회복량) · Amplified(배율 뒤 — Heal에 넘긴 값) · Gained(실제로 오른 HP) ·
//                               HpBefore · HpAfter · MaxHp · FullSkipsBefore(이 대상이 그 전에 체력 가득으로 건너뛴 주기 수 — B-L7) ·
//                               Check=Match(Gained = min(Amplified, MaxHp − HpBefore))/Mismatch.
//   HealAmpSample       (B-L2 ~ B-L6)  (대상 · 출처)마다 첫 증폭 1줄(묶음당 최대 48줄).
//                               Source=Natural/Mist/Bloom/HoT/SkillRegen/Wave · Requested(원래) · Amplified(증폭 뒤) · CarryBeforeMilli ·
//                               CarryAfterMilli · ReqTotal · AmpTotal(이 대상 누계) ·
//                               Check=Match(Requested × 배율 + 이전 나머지 = Amplified + 새 나머지, 그리고 ReqTotal × 배율 = AmpTotal + 나머지)/Mismatch.
//   NewTargetHealSample (B-L8)  효과 중 같은 팀의 「대상이 아닌」 유닛 · 건물(발동 뒤 새로 나온 것 · 업그레이드로 Id가 바뀐 건물)이
//                               배율 없이 회복된 첫 3번.
//   RegenEnded          (B-L7 · B-L9 · B-L13)
//                               묶음 끝 합계 1줄 — Reason=Expire · AllTargetsGone · Replaced · GameEnd · NoTargets, ActualMs.
//                               Kind=Regen: Pulses · Heals · BaseTotal · AmpTotal · HealedTotal · FullHpSkips · FullHpSkipTargets · Texts.
//                               Kind=HealAmpMul: 출처별 횟수 · 원래 합 · 증폭 합(Natural/Mist/Bloom/HoT/SkillRegen/Other),
//                               ReqTotal · AmpTotal · CarryLostMilli(= 원래 합 × 배율 − 증폭 합 — 대상마다 1 미만이라 0 이상 · 대상 수 미만) ·
//                               TotalCheck=Match/Mismatch · NewTargetHeals.
//
// 지키는 것: 모든 진입 메서드에 [Conditional] 두 개(릴리스에서 호출 · 인자 계산 · 문자열 조립이 통째로 사라짐) ·
//   서버 회복 경로 안에서 돌므로 예외를 밖으로 던지지 않음(각 메서드가 삼킨다) · 판정 값(HP · 나머지 · 남은 시간)을 바꾸지 않음
//   (묶음 · 기록의 진단 전용 칸만 쓴다).
//
// Application 레이어.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public sealed partial class SupportSkillEffectSystem
    {
        // 묶음 하나가 남길 재생 표본 줄 상한(대상마다 첫 회복 1줄).
        private const int DiagRegenSampleCap = 48;
        // 묶음 하나가 남길 증폭 표본 줄 상한((대상 · 출처)마다 첫 증폭 1줄).
        private const int DiagAmpSampleCap = 48;
        // 묶음 하나가 남길 「새 대상 회복」 표본 줄 상한.
        private const int DiagAmpNewTargetSampleCap = 3;

        // 표본 키: 대상 종류(유닛 1 / 건물 2) · 출처를 Id 앞에 붙여 유닛 Id와 건물 Id가 겹쳐도 섞이지 않게 한다.
        private static long DiagAmpKey(bool isUnit, int id, HealAmpSource source)
            => ((long)(int)source << 40) | ((isUnit ? 1L : 2L) << 32) | (uint)id;

        // 출처 → 진단 합계 배열 칸(범위 밖은 0 = Unspecified 칸).
        private static int DiagAmpIndex(HealAmpSource source)
        {
            int i = (int)source;
            return i >= 0 && i < 8 ? i : 0;
        }

        private static string DiagTypeName(IDamageable target)
            => target is UnitData u ? u.Type.ToString() : target is BuildingData bd ? bd.Type.ToString() : "Unknown";

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagRegenBatchApplied(EffectBatch b)
        {
            GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-REGEN] RegenApplied",
                System.FormattableString.Invariant(
                    $"Diag=SkillRegen, DiagRev=1, Event=RegenApplied, Role={DiagRole()}, Seq={b.Sequence}, Team={b.CasterTeam}, TargetTeam={b.TargetTeam}, Slot={b.Slot}, BuildingId={b.BuildingId}, Kind={b.Kind}, Target={b.Target}, Targets={b.TargetCount}, Refreshed={b.DiagRefreshed}, PrevRemainingMs={DiagMilli(b.DiagPrevRemaining)}, MagMilli={DiagMilli(b.Magnitude)}, DurationMs={DiagMilli(b.Duration)}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagRegenBatchEnded(EffectBatch b, string reason)
        {
            if (b.Kind == StatusEffectKind.Regen)
            {
                GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-REGEN] RegenEnded",
                    System.FormattableString.Invariant(
                        $"Diag=SkillRegen, DiagRev=1, Event=RegenEnded, Role={DiagRole()}, Seq={b.Sequence}, Team={b.CasterTeam}, TargetTeam={b.TargetTeam}, Slot={b.Slot}, Kind={b.Kind}, Target={b.Target}, Reason={reason}, ActualMs={DiagMilli(b.Elapsed)}, DurationMs={DiagMilli(b.Duration)}, MagMilli={DiagMilli(b.Magnitude)}, Targets={b.TargetCount}, DeadTargets={b.DeadTargets}, Upgraded={b.Upgraded}, Replaced={b.Replaced}, Pulses={b.DiagRegenPulses}, Heals={b.DiagRegenHeals}, BaseTotal={b.DiagRegenBaseTotal}, AmpTotal={b.DiagRegenAmpTotal}, HealedTotal={b.DiagRegenHealedTotal}, FullHpSkips={b.DiagRegenFullSkips}, FullHpSkipTargets={b.DiagRegenFullSkipTargets}, Texts={b.DiagRegenTexts}, EventFaults={b.DiagRegenEventFaults}, Samples={b.DiagRegenSamples}, SamplesDropped={b.DiagRegenSamplesDropped}"));
                return;
            }

            // 받는 회복 배율 묶음 — 출처별 합계와 총 대조. Other = Unspecified + Wave(초월 팀과 만나지 않는 출처 · 넘기지 않은 호출).
            long reqTotal = 0, ampTotal = 0;
            for (int i = 0; i < b.DiagAmpReq.Length; i++) { reqTotal += b.DiagAmpReq[i]; ampTotal += b.DiagAmpOut[i]; }
            double lost = reqTotal * (double)b.Magnitude - ampTotal; // 버려진 나머지 합(대상마다 0 이상 1 미만)
            string totalCheck = lost >= -1e-6 && lost < b.TargetCount + 1e-6 ? "Match" : "Mismatch";
            int n = (int)HealAmpSource.Natural, m = (int)HealAmpSource.Mist, bl = (int)HealAmpSource.Bloom,
                h = (int)HealAmpSource.HoT, r = (int)HealAmpSource.SkillRegen, w = (int)HealAmpSource.Wave, u = (int)HealAmpSource.Unspecified;
            GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-REGEN] RegenEnded",
                System.FormattableString.Invariant(
                    $"Diag=SkillRegen, DiagRev=1, Event=RegenEnded, Role={DiagRole()}, Seq={b.Sequence}, Team={b.CasterTeam}, TargetTeam={b.TargetTeam}, Slot={b.Slot}, Kind={b.Kind}, Target={b.Target}, Reason={reason}, ActualMs={DiagMilli(b.Elapsed)}, DurationMs={DiagMilli(b.Duration)}, MagMilli={DiagMilli(b.Magnitude)}, Targets={b.TargetCount}, DeadTargets={b.DeadTargets}, Upgraded={b.Upgraded}, Replaced={b.Replaced}, NaturalCount={b.DiagAmpCount[n]}, NaturalReq={b.DiagAmpReq[n]}, NaturalAmp={b.DiagAmpOut[n]}, MistCount={b.DiagAmpCount[m]}, MistReq={b.DiagAmpReq[m]}, MistAmp={b.DiagAmpOut[m]}, BloomCount={b.DiagAmpCount[bl]}, BloomReq={b.DiagAmpReq[bl]}, BloomAmp={b.DiagAmpOut[bl]}, HoTCount={b.DiagAmpCount[h]}, HoTReq={b.DiagAmpReq[h]}, HoTAmp={b.DiagAmpOut[h]}, RegenCount={b.DiagAmpCount[r]}, RegenReq={b.DiagAmpReq[r]}, RegenAmp={b.DiagAmpOut[r]}, OtherCount={b.DiagAmpCount[w] + b.DiagAmpCount[u]}, ReqTotal={reqTotal}, AmpTotal={ampTotal}, CarryLostMilli={(int)System.Math.Round(lost * 1000d)}, TotalCheck={totalCheck}, SampleMismatch={b.DiagAmpMismatch}, NewTargetHeals={b.DiagNewTargetHeals}, Samples={b.DiagAmpSamples}, SamplesDropped={b.DiagAmpSamplesDropped}"));
        }

        /// <summary> [진단] 재생 주기가 체력 가득으로 건너뛰었다(줄 없음 — 계수만. 합계는 RegenEnded, 대상별 수는 첫 회복 표본에). </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void DiagRegenFullSkip(EffectBatch batch, EffectRecord regen)
        {
            try
            {
                if (batch == null || regen == null) return;
                batch.DiagRegenPulses++;
                batch.DiagRegenFullSkips++;
                if (regen.DiagRegenFullSkips == 0) batch.DiagRegenFullSkipTargets++;
                regen.DiagRegenFullSkips++;
            }
            catch { /* 진단 전용 — 권위 틱을 막지 않는다 */ }
        }

        /// <summary> [진단] 재생 주기가 회복을 시도했다(합계 + 대상별 첫 실제 회복 표본 1줄). </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagRegenPulse(EffectBatch batch, EffectRecord regen, IDamageable target, bool isUnit,
                                    int baseAmount, int amplified, int hpBefore, int gained, bool showText)
        {
            try
            {
                if (batch == null || regen == null || target == null) return;
                batch.DiagRegenPulses++;
                batch.DiagRegenBaseTotal += baseAmount;
                batch.DiagRegenAmpTotal += amplified;
                batch.DiagRegenHealedTotal += gained;
                if (gained > 0) batch.DiagRegenHeals++;
                if (showText) batch.DiagRegenTexts++;
                if (gained <= 0 || regen.DiagRegenSampled) return;
                regen.DiagRegenSampled = true;
                if (batch.DiagRegenSamples >= DiagRegenSampleCap) { batch.DiagRegenSamplesDropped++; return; }
                batch.DiagRegenSamples++;

                int room = target.MaxHp - hpBefore;
                int expectGain = amplified < room ? amplified : room;
                string check = gained == expectGain ? "Match" : "Mismatch";
                string head = isUnit ? "RegenTickSample" : "BuildingRegenSample";
                GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-REGEN] " + head,
                    System.FormattableString.Invariant(
                        $"Diag=SkillRegen, DiagRev=1, Event={head}, Role={DiagRole()}, Seq={batch.Sequence}, TargetTeam={batch.TargetTeam}, Target={(isUnit ? "Unit" : "Building")}, Id={target.Id}, Type={DiagTypeName(target)}, Pulse={regen.RegenPulses}, Base={baseAmount}, Amplified={amplified}, AmpMagMilli={DiagMilli(GetHealAmpMultiplier(target.Id, isUnit))}, Gained={gained}, HpBefore={hpBefore}, HpAfter={target.Hp}, MaxHp={target.MaxHp}, FullSkipsBefore={regen.DiagRegenFullSkips}, Text={showText}, Check={check}, AtMs={DiagMilli(batch.Elapsed)}"));
            }
            catch { /* 진단 전용 — 회복 적용을 막지 않는다 */ }
        }

        /// <summary> [진단] 회복 이벤트 구독자 예외 수(정상 0 — RegenEnded EventFaults=). </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void DiagRegenEventFault(EffectBatch batch)
        {
            if (batch != null) batch.DiagRegenEventFaults++;
        }

        /// <summary>
        /// [진단] 받는 회복 배율이 회복 하나를 증폭했다(AmplifyHeal 안). 출처별 합계를 더하고 (대상 · 출처)마다 첫 표본 1줄을 남긴다.
        /// 에디터 · 개발 빌드 전용 — 릴리스에서는 호출 · 인자 계산이 사라진다. 판정 값은 바꾸지 않는다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagAmpApplied(EffectRecord amp, IDamageable target, bool isUnit, HealAmpSource source,
                                    int requested, int amplified, double carryBefore)
        {
            try
            {
                EffectBatch batch = amp != null ? amp.Batch : null;
                if (batch == null || target == null) return;
                int idx = DiagAmpIndex(source);
                batch.DiagAmpCount[idx]++;
                batch.DiagAmpReq[idx] += requested;
                batch.DiagAmpOut[idx] += amplified;

                // 이번 증폭 대조: 원래 × 배율 + 이전 나머지 = 증폭 + 새 나머지 / 누계 대조: 누계 원래 × 배율 = 누계 증폭 + 나머지.
                double mag = amp.Magnitude;
                bool stepOk = System.Math.Abs(requested * mag + carryBefore - (amplified + amp.AmpCarry)) < 1e-6
                              && amp.AmpCarry >= 0d && amp.AmpCarry < 1d;
                bool totalOk = System.Math.Abs(amp.AmpReqTotal * mag - (amp.AmpOutTotal + amp.AmpCarry)) < 1e-4;
                if (!stepOk || !totalOk) batch.DiagAmpMismatch++;

                if (batch.DiagAmpSampled == null) batch.DiagAmpSampled = new HashSet<long>();
                if (!batch.DiagAmpSampled.Add(DiagAmpKey(isUnit, target.Id, source))) return; // 이 (대상 · 출처)는 이미 표본을 남겼다.
                if (batch.DiagAmpSamples >= DiagAmpSampleCap) { batch.DiagAmpSamplesDropped++; return; }
                batch.DiagAmpSamples++;

                GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-REGEN] HealAmpSample",
                    System.FormattableString.Invariant(
                        $"Diag=SkillRegen, DiagRev=1, Event=HealAmpSample, Role={DiagRole()}, Seq={batch.Sequence}, TargetTeam={batch.TargetTeam}, Source={source}, Target={(isUnit ? "Unit" : "Building")}, Id={target.Id}, Type={DiagTypeName(target)}, Requested={requested}, Amplified={amplified}, MagMilli={DiagMilli(amp.Magnitude)}, CarryBeforeMilli={(int)System.Math.Round(carryBefore * 1000d)}, CarryAfterMilli={(int)System.Math.Round(amp.AmpCarry * 1000d)}, ReqTotal={amp.AmpReqTotal}, AmpTotal={amp.AmpOutTotal}, HpBefore={target.Hp}, MaxHp={target.MaxHp}, Check={(stepOk && totalOk ? "Match" : "Mismatch")}, AtMs={DiagMilli(batch.Elapsed)}"));
            }
            catch { /* 진단 전용 — 회복 적용을 막지 않는다 */ }
        }

        /// <summary>
        /// [진단] 받는 회복 배율이 없는 대상이 회복됐다. 같은 팀 · 같은 대상 종류의 증폭 묶음이 진행 중이면
        /// 「발동 뒤 새로 나온 대상」(새 유닛 · 새 건물 · 업그레이드로 Id가 바뀐 건물 — S-11 제외 대상)으로 센다(B-L8).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagAmpUntargeted(IDamageable target, bool isUnit, HealAmpSource source, int amount)
        {
            try
            {
                if (_activeBatches.Count == 0 || target == null) return;
                SkillEffectTarget kind = isUnit ? SkillEffectTarget.Unit : SkillEffectTarget.Building;
                for (int i = 0; i < _activeBatches.Count; i++)
                {
                    EffectBatch b = _activeBatches[i];
                    if (b.Ended || b.Kind != StatusEffectKind.HealAmpMul || b.Target != kind || b.TargetTeam != target.Team) continue;
                    b.DiagNewTargetHeals++;
                    if (b.DiagNewTargetHeals <= DiagAmpNewTargetSampleCap)
                    {
                        GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-REGEN] NewTargetHealSample",
                            System.FormattableString.Invariant(
                                $"Diag=SkillRegen, DiagRev=1, Event=NewTargetHealSample, Role={DiagRole()}, Seq={b.Sequence}, TargetTeam={b.TargetTeam}, Source={source}, Target={kind}, Id={target.Id}, Type={DiagTypeName(target)}, Requested={amount}, Amplified={amount}, Amp=False, AtMs={DiagMilli(b.Elapsed)}"));
                    }
                    break;
                }
            }
            catch { /* 진단 전용 */ }
        }
    }
}
