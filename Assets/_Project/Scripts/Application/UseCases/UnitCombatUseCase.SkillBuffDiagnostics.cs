// ============================================================================
// UnitCombatUseCase.SkillBuffDiagnostics.cs
// 강화 스킬(타입 C — 전술 지령 · 화염의 축복 · 숲의 포효 · 숲의 순풍) 진단 로그 [SKILL-BUFF].
// 2026-11-10 지원 스킬 1차 강화 Task(_Tasks/2026-11-10/04_20_support-buff-skills-status-vfx) 구현 단위 ④, Plan 5절 L1~L9.
//
// 무엇을 하나(초급자용 설명):
//   사용자 테스트에서 "공격력이 정말 1.5배가 됐나", "8초 뒤 정말 원래대로 돌아왔나"를 눈이 아니라 로그로 판정하려고,
//   상태가 바뀌는 순간에만 한 줄씩 남긴다(WORKFLOW [4] 「테스트 항목 진단 로그 규칙」).
//     BuffApplied        (L1 · L6 · L7) 서버(싱글 · Host)가 팀 전체에 상태를 건 직후 — 대상 수 · 재적용 수.
//     BuffStatusReceived (L9)           순수 Client가 서버의 상태 RPC를 받아 자기 유닛에 다시 건 직후.
//     BuffAttackSample   (L3 · L5-b)    버프 대상의 "첫 공격"에서 기본 공격력 · 상태 배율 · 실제 공격력(발동마다 1회, 만료 뒤 1회).
//     BuffMoveSample     (L4 · L5-b)    버프 대상의 "첫 이동 배율 조회"에서 이동 배율 · 월드 속도(발동마다 1회, 만료 뒤 1회).
//     BuffExpired        (L5)           대상 전원이 만료되거나 죽어 묶음이 끝난 순간 요약 1줄(실제 걸린 시간 포함).
//     BuffNewUnit        (L8)           버프 중 새로 생긴 같은 팀 유닛(사용자 결정 D1 — 발동 순간의 아군만이라 Applied=False가 정상).
//
//   "묶음(batch)" = 한 번의 적용(팀 하나 · 상태 종류 하나)에 걸린 유닛 목록. (팀, 종류)마다 마지막 묶음 1개만 들고 있다.
//   같은 스킬을 다시 쓰면 새 묶음이 옛 묶음을 대신한다(옛 묶음은 EndReason=Superseded로 끝 줄을 남긴다 — 서버도
//   같은 종류 상태를 통째로 교체하므로 옛 묶음의 유닛은 새 묶음으로 넘어간다).
//
// 지키는 것:
//   - 모든 메서드에 [Conditional] 두 개(UNITY_EDITOR · DEVELOPMENT_BUILD) — 릴리스 빌드에서는 호출 · 인자 계산 · 문자열 조립이
//     통째로 사라진다(LogRules 1.7). 진단은 상태를 절대 바꾸지 않는다(읽기만).
//   - 서버 경로(Host · 싱글 — 발동 처리 · 전투 틱 · 피해 계산) 안에서 돌므로 예외를 밖으로 던지지 않는다(각 메서드가 삼키고 끝낸다).
//   - 매 틱 · 매 프레임 로그 금지(LogRules 1.14-8) — 틱 감시는 계수만 하고, 줄은 상태가 바뀔 때만 남긴다.
//   - 배율 · 시간은 밀리 정수(MagMilli=1500 = 1.5배, DurationMs=8000 = 8초).
//
// Application 레이어 — Domain 의존. NetworkContext(정적 홀더)로 역할만 읽는다. Unity.Netcode 직접 참조 없음.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public partial class UnitCombatUseCase
    {
        // 한 번의 적용(팀 · 종류)에 걸린 유닛 묶음 — 진단 전용.
        private sealed class SkillBuffDiagBatch
        {
            public TeamId Team;
            public StatusEffectKind Kind;
            public float Magnitude;
            public float Duration;
            public bool Authoritative;          // 서버 권위 적용(싱글 · Host)이면 true, 순수 Client 재현이면 false
            public int Sequence = -1;            // 발동 순번(서버만 앎 — SkillActivationUseCase가 꼬리표로 넘긴다). Client는 -1.
            public int Slot = -1;                // 발동 슬롯(서버만 앎). Client는 -1.
            public int BuildingId = -1;          // 시전 건물 Id(서버만 앎). Client는 -1.
            public readonly HashSet<int> Targets = new HashSet<int>();   // 이번 적용에 걸린 유닛 Id
            public readonly HashSet<int> Resolved = new HashSet<int>();  // 만료 또는 사망으로 끝난 유닛 Id
            public int RefreshedCount;           // 걸기 전에 이미 같은 종류가 걸려 있던 유닛 수(재적용 — L6)
            public int ExpiredCount;             // 살아 있는 채로 상태가 사라진 유닛 수
            public int DiedCount;                // 상태가 사라지기 전에 죽은(또는 목록에서 사라진) 유닛 수
            public int NewUnitCount;             // 버프 중 새로 생긴 같은 팀 유닛 수(L8)
            public int NewUnitLogged;            // BuffNewUnit 줄을 남긴 수(상한 NewUnitLogLimit)
            public int MaxUnitIdSeen;            // 새 유닛 판정 기준 — 유닛 Id는 단조 증가라 "지금까지 본 가장 큰 Id"보다 크면 새 유닛
            public float Elapsed;                // 적용 뒤 흐른 시간(초 — 상태 틱의 dt 합)
            public bool Ended;                   // 묶음이 끝났는가(만료 · 대체)
            public string EndReason;             // Expire · Superseded · NoTargets
            public bool AttackSampled, MoveSampled;           // 진행 중 첫 표본을 남겼는가
            public bool AttackAfterSampled, MoveAfterSampled; // 만료 뒤 첫 표본을 남겼는가(L5-b)
        }

        // BuffNewUnit 줄 상한(묶음 1개당). 넘는 수는 BuffExpired의 NewUnitCount로만 센다(스팸 방지).
        private const int SkillBuffNewUnitLogLimit = 4;

        // (팀, 종류) → 마지막 묶음. 키 = 팀 × 64 + 종류(둘 다 작은 정수라 겹치지 않는다).
        private readonly Dictionary<int, SkillBuffDiagBatch> _diagSkillBuffBatches = new Dictionary<int, SkillBuffDiagBatch>();
        // 지금 만들고 있는 묶음(ApplySkillGlobalStatus 안에서만 null이 아니다).
        private SkillBuffDiagBatch _diagSkillBuffBuilding;
        // 서버 발동 꼬리표(SkillActivationUseCase가 실행기 호출 직전에 걸고 직후에 푼다). 이 꼬리표가 있을 때 만든 묶음에 순번 · 슬롯을 붙인다.
        private bool _diagSkillBuffTagSet;
        private int _diagSkillBuffTagSequence = -1, _diagSkillBuffTagSlot = -1, _diagSkillBuffTagBuilding = -1;
        // 틱 감시 중 다 끝난 묶음 · 새 유닛을 잠시 모으는 재사용 버퍼(순회 중 컬렉션 변경 방지).
        private readonly List<int> _diagSkillBuffResolveBuffer = new List<int>(16);

        private static int SkillBuffKey(TeamId team, StatusEffectKind kind) => (int)team * 64 + (int)kind;

        private static int SkillBuffMilli(float value) => Mathf.RoundToInt(value * 1000f);

        private static string SkillBuffRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        // ====================================================================
        // 발동 꼬리표(SkillActivationUseCase → 여기)
        // ====================================================================

        /// <summary>
        /// [진단] 서버 발동 꼬리표를 건다 — 바로 다음 ApplySkillGlobalStatus가 만드는 묶음에 발동 순번 · 슬롯 · 건물을 붙인다.
        /// SkillActivationUseCase가 타입 C 실행기를 부르기 직전에 부른다(에디터 · 개발 빌드 전용).
        /// </summary>
        /// <param name="sequence">발동 순번.</param>
        /// <param name="slot">발동 슬롯(0-based).</param>
        /// <param name="buildingId">시전 건물 Id.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagTagSkillBuffActivation(int sequence, int slot, int buildingId)
        {
            _diagSkillBuffTagSet = true;
            _diagSkillBuffTagSequence = sequence;
            _diagSkillBuffTagSlot = slot;
            _diagSkillBuffTagBuilding = buildingId;
        }

        /// <summary> [진단] 서버 발동 꼬리표를 푼다(실행기 호출 직후 — 다른 경로의 적용에 옛 꼬리표가 붙지 않게). </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagClearSkillBuffActivationTag()
        {
            _diagSkillBuffTagSet = false;
            _diagSkillBuffTagSequence = -1;
            _diagSkillBuffTagSlot = -1;
            _diagSkillBuffTagBuilding = -1;
        }

        // ====================================================================
        // 적용(ApplySkillGlobalStatus 안)
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagBeginSkillBuffBatch(TeamId team, StatusEffectKind kind, float magnitude, float duration, bool authoritative)
        {
            try
            {
                // 회복(HealOverTime)은 상태 시스템이 아니라 HoT로 가므로 이 진단 대상이 아니다.
                if (kind == StatusEffectKind.HealOverTime) { _diagSkillBuffBuilding = null; return; }
                var batch = new SkillBuffDiagBatch
                {
                    Team = team, Kind = kind, Magnitude = magnitude, Duration = duration, Authoritative = authoritative
                };
                if (authoritative && _diagSkillBuffTagSet)
                {
                    batch.Sequence = _diagSkillBuffTagSequence;
                    batch.Slot = _diagSkillBuffTagSlot;
                    batch.BuildingId = _diagSkillBuffTagBuilding;
                }
                _diagSkillBuffBuilding = batch;
            }
            catch
            {
                _diagSkillBuffBuilding = null; // 진단 전용 — 실패해도 적용은 그대로 진행된다.
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagNoteSkillBuffTarget(UnitData unit, StatusEffectKind kind)
        {
            try
            {
                SkillBuffDiagBatch batch = _diagSkillBuffBuilding;
                if (batch == null || unit == null) return;
                batch.Targets.Add(unit.Id);
                // 걸기 "전"에 같은 종류가 이미 있었으면 재적용(남은 시간이 새 지속 시간으로 교체된다 — UnitStatusState).
                if (_statusSystem != null && _statusSystem.HasEffect(unit.Id, kind)) batch.RefreshedCount++;
            }
            catch { /* 진단 전용 */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagEndSkillBuffBatch(bool authoritative)
        {
            SkillBuffDiagBatch batch = _diagSkillBuffBuilding;
            _diagSkillBuffBuilding = null;
            if (batch == null) return;
            try
            {
                // 새 유닛 판정 기준 = 지금 목록의 가장 큰 유닛 Id(어느 팀이든 — Id는 한 카운터에서 단조 증가).
                int maxId = 0;
                foreach (UnitData u in _unitSpawn.Units.Values)
                    if (u != null && u.Id > maxId) maxId = u.Id;
                batch.MaxUnitIdSeen = maxId;

                // 같은 (팀, 종류)의 이전 묶음이 아직 끝나지 않았으면 「대체됨」으로 끝 줄을 남긴다(L6 — 재사용).
                int key = SkillBuffKey(batch.Team, batch.Kind);
                if (_diagSkillBuffBatches.TryGetValue(key, out SkillBuffDiagBatch previous) && !previous.Ended)
                    EndSkillBuffBatch(previous, "Superseded");
                _diagSkillBuffBatches[key] = batch;

                if (authoritative) LogSkillBuffApplied(batch);
                else LogSkillBuffStatusReceived(batch);

                // 대상이 0이면(아군 유닛이 없을 때 사용) 만료를 추적할 것이 없다 — 바로 끝낸다(끝 줄은 남기지 않는다).
                if (batch.Targets.Count == 0)
                {
                    batch.Ended = true;
                    batch.EndReason = "NoTargets";
                }
            }
            catch { /* 진단 전용 */ }
        }

        // ====================================================================
        // 틱 감시(상태 틱이 끝날 때마다 — StatusEffectSystem.NotifyDiagnosticTick)
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagAttachSkillBuffTickListener(StatusEffectSystem statusSystem)
        {
            statusSystem?.SetDiagnosticTickListener(DiagOnSkillBuffStatusTick);
        }

        /// <summary>
        /// [진단] 상태 틱이 끝날 때마다 불린다. 진행 중인 묶음마다 대상 유닛의 만료 · 사망을 세고, 새 유닛을 살피고,
        /// 대상 전원이 끝나면 BuffExpired 1줄을 남긴다. 줄은 상태가 바뀔 때만 남기고 매 틱에는 계수만 한다.
        /// </summary>
        /// <param name="dt">이번 틱 경과 시간(초).</param>
        private void DiagOnSkillBuffStatusTick(float dt)
        {
            if (_diagSkillBuffBatches.Count == 0) return;
            try
            {
                foreach (SkillBuffDiagBatch batch in _diagSkillBuffBatches.Values)
                {
                    if (batch.Ended) continue;
                    batch.Elapsed += dt;

                    // ① 대상 유닛: 죽었으면(또는 목록에서 사라졌으면) 사망, 살아 있는데 상태가 사라졌으면 만료.
                    //   사망을 먼저 본다 — 죽은 유닛의 상태 기록은 만료로 사라지므로(Research 3-6) 상태만 보면 "만료"로 잘못 센다.
                    _diagSkillBuffResolveBuffer.Clear();
                    foreach (int id in batch.Targets)
                    {
                        if (batch.Resolved.Contains(id)) continue;
                        bool alive = _unitSpawn.Units.TryGetValue(id, out UnitData u) && u != null && u.IsAlive;
                        if (!alive) { batch.DiedCount++; _diagSkillBuffResolveBuffer.Add(id); }
                        else if (_statusSystem == null || !_statusSystem.HasEffect(id, batch.Kind))
                        { batch.ExpiredCount++; _diagSkillBuffResolveBuffer.Add(id); }
                    }
                    for (int i = 0; i < _diagSkillBuffResolveBuffer.Count; i++) batch.Resolved.Add(_diagSkillBuffResolveBuffer[i]);
                    _diagSkillBuffResolveBuffer.Clear();

                    // ② 버프 중 새로 생긴 같은 팀 유닛(L8). D1 「발동 순간의 아군만」이라 새 유닛에는 상태가 없어야 정상이다.
                    int maxSeen = batch.MaxUnitIdSeen;
                    foreach (UnitData u in _unitSpawn.Units.Values)
                    {
                        if (u == null || u.Id <= batch.MaxUnitIdSeen) continue;
                        if (u.Id > maxSeen) maxSeen = u.Id;
                        if (u.Team != batch.Team || batch.Targets.Contains(u.Id)) continue;
                        batch.NewUnitCount++;
                        if (batch.NewUnitLogged < SkillBuffNewUnitLogLimit)
                        {
                            batch.NewUnitLogged++;
                            bool applied = _statusSystem != null && _statusSystem.HasEffect(u.Id, batch.Kind);
                            LogSkillBuffNewUnit(batch, u, applied);
                        }
                    }
                    batch.MaxUnitIdSeen = maxSeen;

                    // ③ 대상 전원이 끝났으면 요약 1줄.
                    if (batch.Resolved.Count >= batch.Targets.Count) EndSkillBuffBatch(batch, "Expire");
                }
            }
            catch { /* 진단 전용 — 상태 틱을 막지 않는다 */ }
        }

        private void EndSkillBuffBatch(SkillBuffDiagBatch batch, string reason)
        {
            batch.Ended = true;
            batch.EndReason = reason;
            LogSkillBuffExpired(batch);
        }

        // ====================================================================
        // 표본(EffectiveAttack · GetUnitMoveSpeedMultiplier 안)
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagSampleSkillBuffAttack(UnitData attacker, int baseAttack, float statusMul, int effective)
        {
            if (_diagSkillBuffBatches.Count == 0 || attacker == null) return;
            try
            {
                if (!_diagSkillBuffBatches.TryGetValue(SkillBuffKey(attacker.Team, StatusEffectKind.AttackPowerMul),
                        out SkillBuffDiagBatch batch) || !batch.Targets.Contains(attacker.Id)) return;
                string phase;
                if (!batch.Ended)
                {
                    if (batch.AttackSampled) return;
                    batch.AttackSampled = true;
                    phase = "Active";
                }
                else
                {
                    // 만료 뒤 표본은 "정말 만료로 끝난" 묶음에서만(대체된 묶음의 유닛은 새 묶음에서 다시 센다).
                    if (batch.EndReason != "Expire" || batch.AttackAfterSampled) return;
                    batch.AttackAfterSampled = true;
                    phase = "AfterExpire";
                }
                // 기대값: 진행 중이면 기본 × 강도(반올림), 만료 뒤면 기본 그대로. 기본 = 연구 강화까지 반영된 공격력.
                int expected = phase == "Active" ? Mathf.Max(0, Mathf.RoundToInt(baseAttack * batch.Magnitude)) : baseAttack;
                GameLog.Dev.Info("Skill", nameof(UnitCombatUseCase), "[SKILL-BUFF] BuffAttackSample",
                    System.FormattableString.Invariant(
                        $"Diag=SkillBuff, DiagRev=1, Event=BuffAttackSample, Role={SkillBuffRole()}, Team={batch.Team}, Kind={batch.Kind}, Sequence={batch.Sequence}, Slot={batch.Slot}, Phase={phase}, UnitId={attacker.Id}, UnitType={attacker.Type}, Base={baseAttack}, StatusMulMilli={SkillBuffMilli(statusMul)}, Effective={effective}, Expected={expected}, Match={effective == expected}, ElapsedMs={SkillBuffMilli(batch.Elapsed)}"));
            }
            catch { /* 진단 전용 — 피해 계산을 막지 않는다 */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagSampleSkillBuffMove(UnitData unit, float statusMul, float totalMul)
        {
            if (_diagSkillBuffBatches.Count == 0 || unit == null) return;
            try
            {
                if (!_diagSkillBuffBatches.TryGetValue(SkillBuffKey(unit.Team, StatusEffectKind.MoveSpeedMul),
                        out SkillBuffDiagBatch batch) || !batch.Targets.Contains(unit.Id)) return;
                string phase;
                if (!batch.Ended)
                {
                    if (batch.MoveSampled) return;
                    batch.MoveSampled = true;
                    phase = "Active";
                }
                else
                {
                    if (batch.EndReason != "Expire" || batch.MoveAfterSampled) return;
                    batch.MoveAfterSampled = true;
                    phase = "AfterExpire";
                }
                float researchMul = _upgrade != null ? _upgrade.GetMoveSpeedMultiplier(unit.Team, unit.Type) : 1f;
                // 월드 속도 = 타일 높이 × 칸/초 × 배율 — UnitView 이동 writer와 같은 환산(TileHeight × MoveSpeed × 배율).
                float tile = _mapper != null ? _mapper.TileHeight : 0f;
                float worldSpeed = tile * unit.MoveSpeed * totalMul;
                float worldSpeedNoStatus = tile * unit.MoveSpeed * researchMul;
                GameLog.Dev.Info("Skill", nameof(UnitCombatUseCase), "[SKILL-BUFF] BuffMoveSample",
                    System.FormattableString.Invariant(
                        $"Diag=SkillBuff, DiagRev=1, Event=BuffMoveSample, Role={SkillBuffRole()}, Team={batch.Team}, Kind={batch.Kind}, Sequence={batch.Sequence}, Slot={batch.Slot}, Phase={phase}, UnitId={unit.Id}, UnitType={unit.Type}, BaseSpeedMilli={SkillBuffMilli(unit.MoveSpeed)}, ResearchMulMilli={SkillBuffMilli(researchMul)}, StatusMulMilli={SkillBuffMilli(statusMul)}, TotalMulMilli={SkillBuffMilli(totalMul)}, WorldSpeedmm={SkillBuffMilli(worldSpeed)}, WorldSpeedNoStatusmm={SkillBuffMilli(worldSpeedNoStatus)}, ElapsedMs={SkillBuffMilli(batch.Elapsed)}"));
            }
            catch { /* 진단 전용 — 이동을 막지 않는다 */ }
        }

        // ====================================================================
        // 줄 조립
        // ====================================================================

        private static void LogSkillBuffApplied(SkillBuffDiagBatch b)
        {
            GameLog.Dev.Info("Skill", nameof(UnitCombatUseCase), "[SKILL-BUFF] BuffApplied",
                System.FormattableString.Invariant(
                    $"Diag=SkillBuff, DiagRev=1, Event=BuffApplied, Role={SkillBuffRole()}, Team={b.Team}, Sequence={b.Sequence}, Slot={b.Slot}, BuildingId={b.BuildingId}, Kind={b.Kind}, MagMilli={SkillBuffMilli(b.Magnitude)}, DurationMs={SkillBuffMilli(b.Duration)}, TargetCount={b.Targets.Count}, RefreshedCount={b.RefreshedCount}, MaxUnitIdAtApply={b.MaxUnitIdSeen}"));
        }

        private static void LogSkillBuffStatusReceived(SkillBuffDiagBatch b)
        {
            GameLog.Dev.Info("Skill", nameof(UnitCombatUseCase), "[SKILL-BUFF] BuffStatusReceived",
                System.FormattableString.Invariant(
                    $"Diag=SkillBuff, DiagRev=1, Event=BuffStatusReceived, Role={SkillBuffRole()}, Team={b.Team}, Kind={b.Kind}, MagMilli={SkillBuffMilli(b.Magnitude)}, DurationMs={SkillBuffMilli(b.Duration)}, TargetCount={b.Targets.Count}, RefreshedCount={b.RefreshedCount}, MaxUnitIdAtApply={b.MaxUnitIdSeen}"));
        }

        private static void LogSkillBuffExpired(SkillBuffDiagBatch b)
        {
            GameLog.Dev.Info("Skill", nameof(UnitCombatUseCase), "[SKILL-BUFF] BuffExpired",
                System.FormattableString.Invariant(
                    $"Diag=SkillBuff, DiagRev=1, Event=BuffExpired, Role={SkillBuffRole()}, Team={b.Team}, Kind={b.Kind}, Sequence={b.Sequence}, Slot={b.Slot}, EndReason={b.EndReason}, TargetCount={b.Targets.Count}, ExpiredCount={b.ExpiredCount}, DiedCount={b.DiedCount}, Balanced={b.ExpiredCount + b.DiedCount == b.Targets.Count}, ActualMs={SkillBuffMilli(b.Elapsed)}, DurationMs={SkillBuffMilli(b.Duration)}, NewUnitCount={b.NewUnitCount}"));
        }

        private static void LogSkillBuffNewUnit(SkillBuffDiagBatch b, UnitData unit, bool applied)
        {
            GameLog.Dev.Info("Skill", nameof(UnitCombatUseCase), "[SKILL-BUFF] BuffNewUnit",
                System.FormattableString.Invariant(
                    $"Diag=SkillBuff, DiagRev=1, Event=BuffNewUnit, Role={SkillBuffRole()}, Team={b.Team}, Kind={b.Kind}, Sequence={b.Sequence}, Slot={b.Slot}, UnitId={unit.Id}, UnitType={unit.Type}, Applied={applied}, ElapsedMs={SkillBuffMilli(b.Elapsed)}, Line={b.NewUnitLogged}/{SkillBuffNewUnitLogLimit}"));
        }
    }
}
