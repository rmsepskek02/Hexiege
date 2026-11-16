// ============================================================================
// SupportSkillEffectSystem.ShieldDiagnostics.cs
// 신목의 각성(쉴드) 서버 진단 [SKILL-SHIELD] (Diag=SkillShield, DiagRev=1 — 개발 축, Plan S-15).
// 2026-11-16 지원 스킬 새 상태 Task 단계 C — 구현 단위 C-1. Plan 6-1-6 C-L1 ~ C-L8 · C-L13.
//
// 남기는 줄(상태가 바뀌는 순간 · 상한이 있는 표본만 — 매 피격 줄 금지, LogRules 1.14-8):
//   ShieldApplied       (C-L1)  효과를 건 직후 1줄 — Target=Unit / Building 묶음마다(신목의 각성 1회 = 2줄). 대상 수 · 양 · 지속.
//   ShieldAbsorbSample  (C-L2 · C-L3 · C-L6)
//                               대상마다 「첫 흡수」 1줄 + 「깨지는 순간」 1줄(묶음당 최대 48줄, 넘으면 SamplesDropped로만 센다).
//                               Incoming(헬퍼에 들어온 피해 = 방어 감쇄 · 받는 피해 배율 뒤) · Absorbed · ShieldBefore · ShieldAfter · HpDamage ·
//                               Check=Match(Absorbed + HpDamage = Incoming 이고 ShieldAfter = ShieldBefore − Absorbed)/Mismatch.
//   ShieldBroken        (C-L3)  쉴드가 0이 된 대상 1줄(묶음당 최대 32줄 — 넘으면 ShieldEnded Broken=으로만).
//   ShieldExpired       (C-L4)  시간이 다 되어 남은 양을 버린 대상(묶음당 최대 8줄 — 나머지는 ShieldEnded Expired= · Discarded=).
//   ShieldBrokenNotified(C-L8)  서버가 깨진 대상 Id를 화면에 알린 순간 1줄(틱 단위 묶음 — Client ShieldBrokenReceived와 Id 대조).
//   NewTargetHitSample  (C-L7)  효과 중 같은 팀의 「대상이 아닌」 유닛 · 건물(발동 뒤 새로 나온 것 · 업그레이드로 Id가 바뀐 건물)이
//                               쉴드 없이 맞은 첫 3번.
//   ShieldEnded         (C-L5 · C-L13)
//                               묶음 끝 합계 — Reason=Expire · AllBroken · AllTargetsGone · Replaced · GameEnd · NoTargets,
//                               흡수 합 · 깨짐 수 · 만료 수 · 사망/파괴 수 · 업그레이드 수 · 버린 양 · 새 대상 피격 수.
//                               IncomingTotal = AbsorbedTotal + HpDamageTotal 이면 TotalCheck=Match.
//
// 지키는 것: 모든 진입 메서드에 [Conditional] 두 개(릴리스에서 호출 · 인자 계산 · 문자열 조립이 통째로 사라짐) ·
//   서버 피해 경로 안에서 돌므로 예외를 밖으로 던지지 않음(각 메서드가 삼킨다) · 판정 저장소를 바꾸지 않음(묶음의 진단 칸만).
//
// Application 레이어.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public sealed partial class SupportSkillEffectSystem
    {
        // 묶음 하나가 남길 흡수 표본 줄 상한(대상 수가 많아도 로그가 넘치지 않게 — 첫 흡수 + 깨짐 순간).
        private const int DiagShieldAbsorbSampleCap = 48;
        // 묶음 하나가 남길 ShieldBroken 줄 상한.
        private const int DiagShieldBrokenLineCap = 32;
        // 묶음 하나가 남길 ShieldExpired 줄 상한(만료는 대상 전원이 같은 틱에 끝나므로 표본만).
        private const int DiagShieldExpiredLineCap = 8;
        // 묶음 하나가 남길 「새 대상 피격」 표본 줄 상한.
        private const int DiagShieldNewTargetSampleCap = 3;

        // 표본 키: 대상 종류(유닛 1 / 건물 2)를 Id 앞에 붙여 유닛 Id와 건물 Id가 겹쳐도 섞이지 않게 한다.
        private static long DiagShieldKey(bool isUnit, int id) => ((isUnit ? 1L : 2L) << 32) | (uint)id;

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagShieldBatchApplied(EffectBatch b)
        {
            GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-SHIELD] ShieldApplied",
                System.FormattableString.Invariant(
                    $"Diag=SkillShield, DiagRev=1, Event=ShieldApplied, Role={DiagRole()}, Seq={b.Sequence}, Team={b.CasterTeam}, TargetTeam={b.TargetTeam}, Slot={b.Slot}, BuildingId={b.BuildingId}, Target={b.Target}, Targets={b.TargetCount}, Refreshed={b.DiagRefreshed}, PrevRemainingMs={DiagMilli(b.DiagPrevRemaining)}, Amount={b.ShieldAmount}, DurationMs={DiagMilli(b.Duration)}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagShieldBatchEnded(EffectBatch b, string reason)
        {
            string totalCheck = b.DiagIncomingTotal == b.DiagAbsorbedTotal + b.DiagHpDamageTotal ? "Match" : "Mismatch";
            GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-SHIELD] ShieldEnded",
                System.FormattableString.Invariant(
                    $"Diag=SkillShield, DiagRev=1, Event=ShieldEnded, Role={DiagRole()}, Seq={b.Sequence}, Team={b.CasterTeam}, TargetTeam={b.TargetTeam}, Slot={b.Slot}, Target={b.Target}, Reason={reason}, ActualMs={DiagMilli(b.Elapsed)}, DurationMs={DiagMilli(b.Duration)}, Amount={b.ShieldAmount}, Targets={b.TargetCount}, Broken={b.Broken}, Expired={b.Expired}, DeadTargets={b.DeadTargets}, Upgraded={b.Upgraded}, Replaced={b.Replaced}, AbsorbHits={b.DiagAbsorbHits}, IncomingTotal={b.DiagIncomingTotal}, AbsorbedTotal={b.DiagAbsorbedTotal}, HpDamageTotal={b.DiagHpDamageTotal}, TotalCheck={totalCheck}, DiscardedTotal={b.DiagDiscardedTotal}, NewTargetHits={b.DiagNewTargetHits}, Samples={b.DiagAbsorbSamples}, SamplesDropped={b.DiagAbsorbSamplesDropped}"));
        }

        /// <summary>
        /// [진단] 쉴드가 피해를 받은 순간(AbsorbShield 안). 합계를 더하고, 대상별 첫 흡수 · 깨지는 순간만 표본 1줄을 남긴다.
        /// 에디터 · 개발 빌드 전용 — 릴리스에서는 호출 · 인자 계산이 사라진다. 판정 값은 바꾸지 않는다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagShieldAbsorb(EffectBatch batch, IDamageable target, bool isUnit, IncomingDamageWriter writer,
                                      int incoming, int absorbed, int shieldBefore, int shieldAfter, int hpDamage, bool broken)
        {
            try
            {
                if (batch == null || target == null) return;
                batch.DiagIncomingTotal += incoming;
                batch.DiagAbsorbedTotal += absorbed;
                batch.DiagHpDamageTotal += hpDamage;
                if (absorbed > 0) batch.DiagAbsorbHits++;

                if (batch.DiagShieldSampled == null) batch.DiagShieldSampled = new HashSet<long>();
                long key = DiagShieldKey(isUnit, target.Id);
                bool first = batch.DiagShieldSampled.Add(key);
                string check = absorbed + hpDamage == incoming && shieldAfter == shieldBefore - absorbed ? "Match" : "Mismatch";
                string point = DiagPointOf(writer);
                string typeName = target is UnitData u ? u.Type.ToString() : target is BuildingData bd ? bd.Type.ToString() : "Unknown";

                if (first || broken)
                {
                    if (batch.DiagAbsorbSamples >= DiagShieldAbsorbSampleCap) batch.DiagAbsorbSamplesDropped++;
                    else
                    {
                        batch.DiagAbsorbSamples++;
                        string moment = first && broken ? "FirstBreak" : broken ? "Break" : "First";
                        GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-SHIELD] ShieldAbsorbSample",
                            System.FormattableString.Invariant(
                                $"Diag=SkillShield, DiagRev=1, Event=ShieldAbsorbSample, Role={DiagRole()}, Seq={batch.Sequence}, TargetTeam={batch.TargetTeam}, Target={(isUnit ? "Unit" : "Building")}, Id={target.Id}, Type={typeName}, Writer={writer}, Point={point}, Moment={moment}, Incoming={incoming}, Absorbed={absorbed}, ShieldBefore={shieldBefore}, ShieldAfter={shieldAfter}, HpDamage={hpDamage}, HpBefore={target.Hp}, Check={check}, AtMs={DiagMilli(batch.Elapsed)}"));
                    }
                }

                if (broken)
                {
                    if (batch.DiagBrokenLines >= DiagShieldBrokenLineCap) return;
                    batch.DiagBrokenLines++;
                    GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-SHIELD] ShieldBroken",
                        System.FormattableString.Invariant(
                            $"Diag=SkillShield, DiagRev=1, Event=ShieldBroken, Role={DiagRole()}, Seq={batch.Sequence}, TargetTeam={batch.TargetTeam}, Target={(isUnit ? "Unit" : "Building")}, Id={target.Id}, Type={typeName}, Writer={writer}, Point={point}, Incoming={incoming}, Absorbed={absorbed}, HpDamage={hpDamage}, AtMs={DiagMilli(batch.Elapsed)}"));
                }
            }
            catch { /* 진단 전용 — 피해 적용을 막지 않는다 */ }
        }

        /// <summary>
        /// [진단] 쉴드가 없는 대상이 맞았다. 같은 팀 · 같은 대상 종류의 쉴드 묶음이 진행 중이고 그 묶음에서 깨진 대상도 아니면
        /// 「발동 뒤 새로 나온 대상」(새 유닛 · 새 건물 · 업그레이드로 Id가 바뀐 건물 — S-11 제외 대상)으로 센다(C-L7).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagShieldUntargetedHit(IDamageable target, bool isUnit, IncomingDamageWriter writer, int incoming)
        {
            try
            {
                if (_activeBatches.Count == 0 || target == null) return;
                SkillEffectTarget kind = isUnit ? SkillEffectTarget.Unit : SkillEffectTarget.Building;
                long key = DiagShieldKey(isUnit, target.Id);
                for (int i = 0; i < _activeBatches.Count; i++)
                {
                    EffectBatch b = _activeBatches[i];
                    if (b.Ended || b.Kind != StatusEffectKind.Shield || b.Target != kind || b.TargetTeam != target.Team) continue;
                    // 이 묶음에서 이미 흡수 · 깨짐 기록이 있는 대상(깨진 뒤 다시 맞음)은 새 대상이 아니다.
                    if (b.DiagShieldSampled != null && b.DiagShieldSampled.Contains(key)) continue;
                    b.DiagNewTargetHits++;
                    if (b.DiagNewTargetHits <= DiagShieldNewTargetSampleCap)
                    {
                        GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-SHIELD] NewTargetHitSample",
                            System.FormattableString.Invariant(
                                $"Diag=SkillShield, DiagRev=1, Event=NewTargetHitSample, Role={DiagRole()}, Seq={b.Sequence}, TargetTeam={b.TargetTeam}, Target={kind}, Id={target.Id}, Writer={writer}, Point={DiagPointOf(writer)}, Damage={incoming}, Shielded=False"));
                    }
                    break;
                }
            }
            catch { /* 진단 전용 */ }
        }

        /// <summary> [진단] 쉴드가 시간이 다 되어 남은 양을 버렸다(C-L4 — 묶음당 표본 상한). </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagShieldExpired(EffectBatch batch, int targetId, bool isUnit, int discarded)
        {
            try
            {
                if (batch == null || batch.DiagExpiredLines >= DiagShieldExpiredLineCap) return;
                batch.DiagExpiredLines++;
                GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-SHIELD] ShieldExpired",
                    System.FormattableString.Invariant(
                        $"Diag=SkillShield, DiagRev=1, Event=ShieldExpired, Role={DiagRole()}, Seq={batch.Sequence}, TargetTeam={batch.TargetTeam}, Target={(isUnit ? "Unit" : "Building")}, Id={targetId}, Discarded={discarded}, Amount={batch.ShieldAmount}, ActualMs={DiagMilli(batch.Elapsed)}, DurationMs={DiagMilli(batch.Duration)}"));
            }
            catch { /* 진단 전용 — 권위 틱을 막지 않는다 */ }
        }

        /// <summary> [진단] 서버가 깨진 쉴드 대상 Id를 화면(과 멀티 Client)에 알린 순간 1줄(C-L8 — Client ShieldBrokenReceived와 대조). </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void DiagShieldBrokenNotified(ShieldBrokenPresentation p)
        {
            try
            {
                GameLog.Dev.Info("Skill", nameof(SupportSkillEffectSystem), "[SKILL-SHIELD] ShieldBrokenNotified",
                    System.FormattableString.Invariant(
                        $"Diag=SkillShield, DiagRev=1, Event=ShieldBrokenNotified, Role={DiagRole()}, Seq={p.Sequence}, UnitCount={p.UnitCount}, Units={DiagJoin(p.UnitIds)}, BuildingCount={p.BuildingCount}, Buildings={DiagJoin(p.BuildingIds)}"));
            }
            catch { /* 진단 전용 */ }
        }

        // Id 목록 → `a|b|c`(값 안에 `, `를 넣지 않는다 — LogRules 1.4). 비었으면 None. NetworkSkillController.DiagJoinIds와 같은 모양.
        private static string DiagJoin(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0) return "None";
            var sb = new System.Text.StringBuilder(ids.Count * 6);
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(ids[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
