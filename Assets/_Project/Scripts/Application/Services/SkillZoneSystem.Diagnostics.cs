// ============================================================================
// SkillZoneSystem.Diagnostics.cs
// 지속 상태 구역 서버 진단 로그 [SKILL-ZONE] (Diag=SkillZone, DiagRev=1 — 개발 축, 사용자 결정 Z-19).
// 2026-11-11 지속 상태 구역 Task 구현 단위 ⑤ — Plan 5절 「테스트 항목 ↔ 진단 로그」 L1~L12.
//
// 무엇을 하나(초급자용 설명):
//   "구역이 정말 생겼나", "들어온 적이 정말 걸렸나", "4초 뒤 정말 풀렸나"를 눈이 아니라 로그로 판정하려고,
//   상태가 바뀌는 순간에만 한 줄씩 남긴다(WORKFLOW [4] 「테스트 항목 진단 로그 규칙」).
//     ZoneCreated        (L1 · L2)       구역 생성 — 종류 · 강도 · 반경 · 지속 · 발동 순간 구성원 수
//     ZoneCreateRejected                 구역을 만들지 못함(Warn — 정상이면 0건)
//     ZoneMembersChanged (L3 · L6 · L10) 구성원이 바뀐 틱 — Reason=Initial(첫 알림) · Walked(들어옴 · 걸어 나감) · Died(사망)
//     ZoneOverlap        (L8)            같은 스킬 구역이 한 유닛 위에서 처음 겹친 순간 — Applied=Once(효과 1회)
//     ZoneMoveSample     (L5 · L6 · L9)  구역 안 유닛의 첫 이동 배율 조회(Phase=Active) · 걸어 나간 뒤 첫 조회(Phase=AfterExit)
//     ZoneEnded          (L4 · L7 · L11 · L12) 구역 끝 — Reason=Expire · GameEnd, 실제 시간 · 공격 관문에 막힌 수(횟수만)
//
// 지키는 것:
//   - 모든 메서드에 [Conditional] 두 개(UNITY_EDITOR · DEVELOPMENT_BUILD) — 릴리스 빌드에서는 호출 · 인자 계산 · 문자열 조립이
//     통째로 사라진다(LogRules 1.7). 진단은 판정 저장소(구성원 · 효과 합)를 절대 바꾸지 않는다(읽기만).
//   - 서버 경로(발동 처리 · 권위 틱 · 이동 · 공격 판정) 안에서 돌므로 예외를 밖으로 던지지 않는다(각 메서드가 삼킨다).
//   - 매 틱 줄 금지(LogRules 1.14-8) — 공격 관문 차단은 횟수만 세고 구역 끝 줄에 한 번 적는다.
//   - 배율 · 시간은 밀리 정수, 거리는 mm(MagMilli=400 = 0.4배, DurationMs=4000 = 4초, Radiusmm=2000 = 반경 2).
//
// Application 레이어 — NetworkContext(정적 홀더)로 역할만 읽는다. Unity.Netcode 직접 참조 없음.
// ============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public sealed partial class SkillZoneSystem
    {
        // 구역 하나의 진단 기록(개발 빌드에서만 만들어진다 — 판정과 무관).
        private sealed class ZoneDiag
        {
            public readonly HashSet<int> EverMembers = new HashSet<int>();  // 한 번이라도 구성원이었던 유닛(TotalEntered)
            public readonly HashSet<int> MoveSampled = new HashSet<int>();  // 진행 중 이동 표본을 남긴 유닛
            public int PeakMembers;                                          // 가장 많았던 동시 구성원 수
            public int AttackGateBlocks;                                     // 공격 관문에 막힌 횟수(사거리 안 적이 있었을 때만)
            public int Walked, Died;                                         // 걸어 나간 수 · 죽은 수(누적)
            public bool OverlapLogged;                                       // ZoneOverlap 줄을 남겼는가
        }

        // 걸어 나간 뒤 첫 이동 표본을 기다리는 유닛 → 나간 구역 순번(구역이 끝나도 남아 있게 서비스 단위로 둔다).
        private readonly Dictionary<int, int> _diagAfterExit = new Dictionary<int, int>();
        private readonly StringBuilder _diagIds = new StringBuilder(64);

        private static string DiagRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        private static int DiagMilli(float value) => Mathf.RoundToInt(value * 1000f);

        // Id 목록 → `a|b|c`(값 안에 `, `를 넣지 않는다 — LogRules 1.4). 비었으면 None.
        private string DiagJoin(IEnumerable<int> ids)
        {
            _diagIds.Clear();
            if (ids != null)
                foreach (int id in ids)
                {
                    if (_diagIds.Length > 0) _diagIds.Append('|');
                    _diagIds.Append(id);
                }
            return _diagIds.Length == 0 ? "None" : _diagIds.ToString();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagCreateRejected(int sequence, TeamId team, int slot, string reason)
        {
            try
            {
                GameLog.Dev.Warn("Skill", nameof(SkillZoneSystem), "[SKILL-ZONE] ZoneCreateRejected",
                    System.FormattableString.Invariant(
                        $"Diag=SkillZone, DiagRev=1, Event=ZoneCreateRejected, Role={DiagRole()}, Seq={sequence}, Team={team}, Slot={slot}, Reason={reason}"));
            }
            catch { /* 진단 전용 — 서버 발동을 막지 않는다 */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagOnCreated(Zone zone)
        {
            try
            {
                zone.Diag = new ZoneDiag();
                foreach (int id in zone.Members) zone.Diag.EverMembers.Add(id);
                zone.Diag.PeakMembers = zone.Members.Count;
                GameLog.Dev.Info("Skill", nameof(SkillZoneSystem), "[SKILL-ZONE] ZoneCreated",
                    System.FormattableString.Invariant(
                        $"Diag=SkillZone, DiagRev=1, Event=ZoneCreated, Role={DiagRole()}, Seq={zone.Sequence}, Team={zone.CasterTeam}, TargetTeam={zone.TargetTeam}, Slot={zone.Slot}, BuildingId={zone.CasterBuildingId}, Kind={zone.Kind}, MagMilli={DiagMilli(zone.Magnitude)}, Radiusmm={DiagMilli(zone.Radius)}, DurationMs={DiagMilli(zone.Duration)}, AimXmm={DiagMilli(zone.Center.x)}, AimZmm={DiagMilli(zone.Center.z)}, InitialMembers={zone.Members.Count}, InitialIds={DiagJoin(zone.Members)}, ActiveZones={_zones.Count}"));
            }
            catch { /* 진단 전용 */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagOnMembersChanged(Zone zone, bool initial, List<int> entered, List<int> exitedWalked, List<int> exitedDied)
        {
            try
            {
                ZoneDiag d = zone.Diag;
                if (d != null)
                {
                    for (int i = 0; i < entered.Count; i++) d.EverMembers.Add(entered[i]);
                    if (zone.Members.Count > d.PeakMembers) d.PeakMembers = zone.Members.Count;
                    d.Walked += exitedWalked.Count;
                    d.Died += exitedDied.Count;
                }
                // 걸어 나간 유닛은 "나간 뒤 첫 이동 배율"을 한 번 더 본다(L6 · L9 — 원래 속도로 돌아왔나).
                for (int i = 0; i < exitedWalked.Count; i++) _diagAfterExit[exitedWalked[i]] = zone.Sequence;
                for (int i = 0; i < entered.Count; i++) _diagAfterExit.Remove(entered[i]);

                float elapsedMs = DiagMilli(zone.Duration - zone.Remaining);
                if (entered.Count > 0 || exitedWalked.Count > 0)
                {
                    string reason = initial ? "Initial" : "Walked";
                    GameLog.Dev.Info("Skill", nameof(SkillZoneSystem), "[SKILL-ZONE] ZoneMembersChanged",
                        System.FormattableString.Invariant(
                            $"Diag=SkillZone, DiagRev=1, Event=ZoneMembersChanged, Role={DiagRole()}, Seq={zone.Sequence}, Team={zone.CasterTeam}, Slot={zone.Slot}, Kind={zone.Kind}, Reason={reason}, Entered={DiagJoin(entered)}, Exited={DiagJoin(exitedWalked)}, Members={zone.Members.Count}, ElapsedMs={elapsedMs}"));
                }
                if (exitedDied.Count > 0)
                {
                    GameLog.Dev.Info("Skill", nameof(SkillZoneSystem), "[SKILL-ZONE] ZoneMembersChanged",
                        System.FormattableString.Invariant(
                            $"Diag=SkillZone, DiagRev=1, Event=ZoneMembersChanged, Role={DiagRole()}, Seq={zone.Sequence}, Team={zone.CasterTeam}, Slot={zone.Slot}, Kind={zone.Kind}, Reason=Died, Entered=None, Exited={DiagJoin(exitedDied)}, Members={zone.Members.Count}, ElapsedMs={elapsedMs}"));
                }
            }
            catch { /* 진단 전용 — 권위 틱을 막지 않는다 */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagOnOverlap(Zone zone, int unitId)
        {
            try
            {
                if (zone.Diag == null || zone.Diag.OverlapLogged) return;
                zone.Diag.OverlapLogged = true;
                // 같은 스킬의 다른(먼저 효과를 준) 구역 순번을 찾는다.
                int otherSeq = 0;
                for (int i = 0; i < _zones.Count; i++)
                {
                    Zone other = _zones[i];
                    if (ReferenceEquals(other, zone)) break; // 효과는 먼저 만든 구역 순서로 들어간다 — 앞쪽에서 찾는다
                    if (other.CasterTeam == zone.CasterTeam && other.Slot == zone.Slot && other.Members.Contains(unitId))
                    { otherSeq = other.Sequence; break; }
                }
                float appliedMul = zone.Kind == StatusEffectKind.Freeze ? 0f
                    : zone.Kind == StatusEffectKind.MoveSpeedMul ? zone.Magnitude : 1f;
                GameLog.Dev.Info("Skill", nameof(SkillZoneSystem), "[SKILL-ZONE] ZoneOverlap",
                    System.FormattableString.Invariant(
                        $"Diag=SkillZone, DiagRev=1, Event=ZoneOverlap, Role={DiagRole()}, Seq={zone.Sequence}, OtherSeq={otherSeq}, Team={zone.CasterTeam}, Slot={zone.Slot}, Kind={zone.Kind}, UnitId={unitId}, Applied=Once, ZoneMulMilli={DiagMilli(appliedMul)}"));
            }
            catch { /* 진단 전용 */ }
        }

        /// <summary>
        /// [진단 L4] 구역 효과로 공격 관문에 막힌 1회를 센다(줄 없음 — 구역 끝 줄의 AttackGateBlocks로만 남는다).
        /// UnitCombatUseCase.CanAttack의 진단 헬퍼([Conditional])가 "사거리 안에 적이 있는데 구역 때문에 막혔을 때"만 부른다.
        /// </summary>
        /// <param name="unitId">막힌 유닛 Id.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagNoteGateBlocked(int unitId)
        {
            try
            {
                for (int i = 0; i < _zones.Count; i++)
                {
                    Zone zone = _zones[i];
                    if (zone.Diag == null || !zone.Members.Contains(unitId)) continue;
                    if (zone.Kind == StatusEffectKind.Freeze || zone.Kind == StatusEffectKind.AttackDisabled)
                        zone.Diag.AttackGateBlocks++;
                }
            }
            catch { /* 진단 전용 — 공격 판정을 막지 않는다 */ }
        }

        /// <summary>
        /// [진단 L5 · L6 · L9] 이동 배율 조회 표본. 구역 안 유닛의 첫 조회(구역마다 1회 — Phase=Active)와
        /// 걸어 나간 뒤 첫 조회(1회 — Phase=AfterExit)에서만 1줄. UnitCombatUseCase.GetUnitMoveSpeedMultiplier가 부른다.
        /// </summary>
        /// <param name="unit">이동 중인 유닛.</param>
        /// <param name="researchMul">연구 이동 배율.</param>
        /// <param name="statusMul">상태 시스템 이동 배율(순풍 등).</param>
        /// <param name="zoneMul">이 서비스의 구역 이동 배율.</param>
        /// <param name="totalMul">최종 배율(연구 × 상태 × 구역).</param>
        /// <param name="tileHeight">타일 높이(월드 속도 환산 — UnitView 이동 writer와 같은 식).</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagSampleMove(UnitData unit, float researchMul, float statusMul, float zoneMul, float totalMul, float tileHeight)
        {
            if (unit == null || (_zones.Count == 0 && _diagAfterExit.Count == 0)) return;
            try
            {
                for (int i = 0; i < _zones.Count; i++)
                {
                    Zone zone = _zones[i];
                    if (zone.Diag == null || !zone.Members.Contains(unit.Id) || !zone.Diag.MoveSampled.Add(unit.Id)) continue;
                    LogMoveSample(zone.Sequence, zone.Kind, "Active", unit, researchMul, statusMul, zoneMul, totalMul, tileHeight,
                                  DiagMilli(zone.Duration - zone.Remaining));
                }
                if (_diagAfterExit.Count > 0 && _diagAfterExit.TryGetValue(unit.Id, out int exitedSeq) && !IsMemberOf(exitedSeq, unit.Id))
                {
                    _diagAfterExit.Remove(unit.Id);
                    LogMoveSample(exitedSeq, StatusEffectKind.None, "AfterExit", unit, researchMul, statusMul, zoneMul, totalMul, tileHeight, -1);
                }
            }
            catch { /* 진단 전용 — 이동을 막지 않는다 */ }
        }

        private bool IsMemberOf(int sequence, int unitId)
        {
            for (int i = 0; i < _zones.Count; i++)
                if (_zones[i].Sequence == sequence) return _zones[i].Members.Contains(unitId);
            return false;
        }

        private static void LogMoveSample(int sequence, StatusEffectKind kind, string phase, UnitData unit, float researchMul,
                                          float statusMul, float zoneMul, float totalMul, float tileHeight, int elapsedMs)
        {
            // 월드 속도 = 타일 높이 × 칸/초 × 배율(UnitView 이동 writer와 같은 환산 — [SKILL-BUFF] BuffMoveSample 선례).
            float worldSpeed = tileHeight * unit.MoveSpeed * totalMul;
            float worldSpeedNoZone = tileHeight * unit.MoveSpeed * researchMul * statusMul;
            GameLog.Dev.Info("Skill", nameof(SkillZoneSystem), "[SKILL-ZONE] ZoneMoveSample",
                System.FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=ZoneMoveSample, Role={DiagRole()}, Seq={sequence}, Kind={kind}, Phase={phase}, UnitId={unit.Id}, UnitType={unit.Type}, Team={unit.Team}, ResearchMulMilli={DiagMilli(researchMul)}, StatusMulMilli={DiagMilli(statusMul)}, ZoneMulMilli={DiagMilli(zoneMul)}, Mul={DiagMilli(totalMul)}, WorldSpeedmm={DiagMilli(worldSpeed)}, WorldSpeedNoZonemm={DiagMilli(worldSpeedNoZone)}, ElapsedMs={elapsedMs}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagOnEnded(Zone zone, string reason)
        {
            try
            {
                ZoneDiag d = zone.Diag;
                // 시전 건물 생존(Z-7 확인용 — 판정에는 쓰지 않는다). 조회 수단이 없으면 Unknown.
                string casterAlive = "Unknown";
                if (_buildings != null)
                {
                    BuildingData caster = _buildings.GetBuilding(zone.CasterBuildingId);
                    casterAlive = caster != null && caster.IsAlive ? "True" : "False";
                }
                // 경기 종료 때 끝난 구역의 구성원은 "걸어 나감 대기"에 넣지 않는다(이후 표본이 섞이지 않게).
                GameLog.Dev.Info("Skill", nameof(SkillZoneSystem), "[SKILL-ZONE] ZoneEnded",
                    System.FormattableString.Invariant(
                        $"Diag=SkillZone, DiagRev=1, Event=ZoneEnded, Role={DiagRole()}, Seq={zone.Sequence}, Team={zone.CasterTeam}, Slot={zone.Slot}, Kind={zone.Kind}, Reason={reason}, ActualMs={DiagMilli(zone.Duration - zone.Remaining)}, DurationMs={DiagMilli(zone.Duration)}, ReleasedMembers={zone.Members.Count}, MembersAfterEnd=0, TotalEntered={(d != null ? d.EverMembers.Count : -1)}, PeakMembers={(d != null ? d.PeakMembers : -1)}, WalkedOut={(d != null ? d.Walked : -1)}, Died={(d != null ? d.Died : -1)}, AttackGateBlocks={(d != null ? d.AttackGateBlocks : -1)}, CasterAlive={casterAlive}"));
            }
            catch { /* 진단 전용 */ }
        }
    }
}
