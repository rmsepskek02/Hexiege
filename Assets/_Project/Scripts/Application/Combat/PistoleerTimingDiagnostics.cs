using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// Read-only Pistoleer/Assault/Sniper development observations. Historical name/tag are retained
    /// so existing saved logs and consumers remain compatible. No gameplay decisions live here.
    /// </summary>
    public static class PistoleerTimingDiagnostics
    {
        public const int SampleLimit = 64;
        public const int ActiveLimit = 1024;
        private sealed class Row
        {
            public int Unit, Team, Lines, Episodes, LeaseLines, Type = -1; public ulong Revision, Token;
            public bool Sampled, Retired, SourceDone, ResultDone;
            public readonly HashSet<string> Critical = new HashSet<string>();
        }
        private static readonly Dictionary<AttackPresentationScope, Row> Rows = new Dictionary<AttackPresentationScope, Row>();
        private static readonly Dictionary<AttackPresentationScope, Row> Tombstones = new Dictionary<AttackPresentationScope, Row>();
        private static readonly Queue<AttackPresentationScope> TombstoneOrder = new Queue<AttackPresentationScope>();
        private static readonly Dictionary<string, long> Counts = new Dictionary<string, long>();
        private static string session, run, role;
        private static int blue, red;
        private static long omitted, formatFailures, sampleExcluded, unclassifiedScoped, unscoped;
        private static long population, activeOverflow;
        private static long sampleOmissions, tombstoneEvictions;
        private static int activePeak;
        private static int nativeLeaseUnscoped;
        public static string Number(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
        // This is an observation filter, never an attack admission or policy filter.
        public static bool Observes(UnitType type) => type == UnitType.Pistoleer || type == UnitType.Assault || type == UnitType.Sniper;

        // Same detail formatters are used by production snapshots and the existing menu fixtures.
        public static string NativeLeaseDetail(string observation, string boundary, string clockSource,
            UnitAttackPhasePresentation owner, bool bound, bool sourceOnly, int remaining, int hits)
            => $"observation={observation} boundary={boundary} clockSource={clockSource} ownerInstance={owner.Scope.AttackerInstanceId.Value} ownerSeq={owner.Scope.SequenceId.Value} epoch={owner.EpochId} generation={owner.Lifetime} bound={(bound ? 1 : 0)} sourceOnly={(sourceOnly ? 1 : 0)} remaining={remaining} hits={hits}";
        public static string NativeLeaseHitDetail(string observation, bool found, AttackSourceOccurrence hit,
            bool consumed, long callbackLoop, float callbackMarker)
            => $"observation={observation} found={(found ? 1 : 0)} expectedHit={hit.HitIndex} expectedLoop={hit.Loop} expectedMarker={Number(found ? (float)hit.MarkerOffset : double.NaN)} markerServer={Number(found ? hit.MarkerServerTime : double.NaN)} consumed={(consumed ? 1 : 0)} callbackLoop={callbackLoop} callbackMarker={Number(callbackMarker)}";

        /// <summary>실제 선택한 owner와 최신 cursor를 구별하는 전이 로그 formatter이다.</summary>
        public static string NativeRouteDetail(UnitAttackPhasePresentation owner, string boundary, string reason,
            long loop, float marker, bool pending, int count, int remaining, bool sourceOnly, AttackPresentationScope latest)
        {
            return $"observation={boundary}:{loop}:{Number(marker)} boundary={boundary} reason={reason} epoch={owner.EpochId} generation={owner.Lifetime} target={owner.TargetId} targetIsUnit={(owner.TargetIsUnit ? 1 : 0)} callbackLoop={loop} callbackMarker={Number(marker)} pending={(pending ? 1 : 0)} pendingCount={count} capacity=8 remaining={remaining} sourceOnly={(sourceOnly ? 1 : 0)} latestInstance={latest.AttackerInstanceId.Value} latestSeq={latest.SequenceId.Value}";
        }

        // The very same serializer is used by the menu's raw fixtures and by the runtime sink.
        // Reserve 120 bytes for the file/logcat envelope; never truncate a healthy-looking record.
        public static string Format(string sessionId, string runId, string peer, string stage,
            ulong instance, ulong sequence, ulong revision, int hit, int unit, int team,
            double local, double server, string detail, int unitType = -1)
        {
            string domain = double.IsNaN(server) || double.IsInfinity(server)
                || stage == "WriterBefore" || stage == "WriterAfter" ? "unavailable"
                : stage == "NativeLease" || stage == "NativeLeaseHit" || stage == "NativeRoute" ? "phase-observation"
                : stage == "Command" || stage == "RenderedStart" || stage == "RawMarker" || stage == "Marker"
                    || stage == "VfxAttempt" || stage == "VfxResult" || stage == "TracerDecision"
                    || stage == "SourceStop" || stage == "Retire" || stage == "MarkerSuppressed" || stage == "Decision"
                    ? "presentation-derived" : "NGO-estimate";
            string prefix = $"[PIST-TIME] v=1 session={sessionId} run={runId} role={peer} stage={stage} instance={instance} seq={sequence} rev={revision} hit={hit} unit={unit} team={team} type={unitType} local={Number(local)} server={Number(server)} domain={domain}";
            string line = prefix + " " + detail + " end=1";
            if (Encoding.UTF8.GetByteCount(line) <= 780) return line;
            string fallback = prefix + " formatIncomplete=1 end=1";
            if (Encoding.UTF8.GetByteCount(fallback) <= 780) return fallback;
            // The malformed envelope itself can be oversized. Fail explicitly without
            // truncating a record into something the parser could mistake for healthy.
            return $"[PIST-TIME] v=1 session=unavailable run=unavailable role={peer} stage=Count instance={instance} seq={sequence} rev={revision} hit={hit} unit={unit} team={team} type={unitType} local={Number(local)} server={Number(server)} domain=unavailable formatIncomplete=1 end=1";
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Begin(string sessionId, string runId, bool host, double server)
        {
            Rows.Clear(); Counts.Clear(); blue = red = activePeak = 0; omitted = formatFailures = 0;
            Tombstones.Clear(); TombstoneOrder.Clear(); sampleOmissions = tombstoneEvictions = 0;
            population = activeOverflow = 0;
            sampleExcluded = unclassifiedScoped = unscoped = 0; nativeLeaseUnscoped = 0;
            session = sessionId; run = runId; role = host ? "Host" : "Client";
            Write("Begin", default, null, server, "limit=64 localDomain=realtime serverDomain=NGO-estimate");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Register(UnitData unit, AttackPresentationScope scope, ulong revision, ulong token = 0)
        {
            if (session == null || unit == null || !Observes(unit.Type) || !scope.IsValid) return;
            if (Rows.TryGetValue(scope, out Row existing) || Tombstones.TryGetValue(scope, out existing))
            {
                if (token != 0) existing.Token = token;
                // Result revision can be newer than Commit. Only explicit Command/Schedule
                // registration resolves unknown scope revision; old emitted observations stay raw.
                if (existing.Revision == 0 && revision != 0) existing.Revision = revision;
                return;
            }
            // Independent quotas ensure one team cannot exhaust the other team's sample budget.
            bool isBlue = unit.Team == TeamId.Blue;
            population++;
            if (Rows.Count >= ActiveLimit)
            {
                activeOverflow++;
                Write("ActiveOverflow", scope, new Row { Unit = unit.Id, Team = (int)unit.Team, Type = (int)unit.Type, Revision = revision },
                    double.NaN, $"capacity={ActiveLimit} population={population} overflow={activeOverflow} coverage=incomplete");
                return;
            }
            bool sampled = (isBlue ? blue : red) < 32;
            if (sampled) { if (isBlue) blue++; else red++; }
            Rows.Add(scope, new Row { Unit = unit.Id, Team = (int)unit.Team, Type = (int)unit.Type, Revision = revision, Token = token, Sampled = sampled });
            activePeak = Math.Max(activePeak, Rows.Count);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void NativeLeaseUnavailable(UnitData unit, double clock, string boundary)
        {
            if (session == null || unit == null || !Observes(unit.Type)) return;
            Count("NativeLeaseUnscoped"); omitted++;
            if (++nativeLeaseUnscoped <= 16)
                Write("Unregistered", default, new Row { Unit = unit.Id, Team = (int)unit.Team, Type = (int)unit.Type }, clock,
                    $"coverageUnknown=1 reason=invalid-lease-context event=NativeLease boundary={boundary} unscopedLimit=16");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Event(UnitData unit, string stage, AttackPresentationScope scope, double server, string detail)
        {
            if (unit == null || !Observes(unit.Type) || session == null) return;
            if (!scope.IsValid)
            {
                Count(stage); unscoped++;
                if (stage != "RawMarker")
                    Write(stage, scope, new Row { Unit = unit.Id, Team = (int)unit.Team, Type = (int)unit.Type }, server, detail);
                return;
            }
            if (!Rows.ContainsKey(scope) && !Tombstones.ContainsKey(scope))
            {
                Count(stage);
                {
                    omitted++;
                    // A known observed-type event arrived before registration. Preserve a bounded keyed
                    // unresolved observation, never bind it to the latest unrelated command.
                    if (omitted <= 16) Write("Unregistered", scope, new Row { Unit = unit.Id, Team = (int)unit.Team, Type = (int)unit.Type }, server, "coverageUnknown=1 reason=unregistered-or-retired-evidence-evicted event=" + stage + " " + detail);
                }
                return;
            }
            Record(stage, scope, server, detail);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Scoped(string stage, AttackPresentationScope scope, double server, string detail)
        {
            if (session == null) return;
            // Coordinator also reports other unit types. No classification without UnitData;
            // this separate aggregate must never taint the known observed-type sample's completeness.
            if (!Rows.ContainsKey(scope) && !Tombstones.ContainsKey(scope)) { unclassifiedScoped++; return; }
            Record(stage, scope, server, detail);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void TokenEvent(UnitData unit, string stage, ulong token, int hit, double server, string detail)
        {
            if (unit == null || !Observes(unit.Type) || session == null) return;
            foreach (var pair in Rows)
                if (pair.Value.Unit == unit.Id && pair.Value.Token == token && pair.Key.HitIndex == hit)
                { Record(stage, pair.Key, server, detail); return; }
            foreach (var pair in Tombstones)
                if (pair.Value.Unit == unit.Id && pair.Value.Token == token && pair.Key.HitIndex == hit)
                { Record(stage, pair.Key, server, detail); return; }
            Count(stage);
            omitted++;
            if (omitted <= 16) Write("Unregistered", default, new Row { Unit = unit.Id, Team = (int)unit.Team, Type = (int)unit.Type }, server,
                $"coverageUnknown=1 reason=token-evidence-unavailable token={token} hit={hit} event={stage}");
        }

        private static void Count(string stage) { Counts.TryGetValue(stage, out long count); Counts[stage] = count + 1; }
        private static void Record(string stage, AttackPresentationScope scope, double server, string detail)
        {
            Count(stage);
            if (!Rows.TryGetValue(scope, out Row row) && !Tombstones.TryGetValue(scope, out row)) { omitted++; return; }
            row.Retired |= stage == "Retire" || stage == "Cancel" || stage == "Unavailable" || stage == "SourceStop";
            row.SourceDone |= stage == "VfxResult" || stage == "LateSourceOccurrenceSkipped" || stage == "Cancel";
            row.ResultDone |= stage == "Decision" || stage == "Cancel";
            // Required causal stages are outside both cumulative sampling and the old detail cap.
            // Repeats remain in aggregate counters; each distinct typed retire cause is retained.
            // Sniper needs native timing after the sampling quota too. Once per exact scope;
            // a continuous native epoch need not produce a new RenderedStart for every commit.
            bool critical = row.Type == (int)UnitType.Sniper || (stage != "RawMarker" && stage != "RenderedStart");
            if (critical)
            {
                bool episode = stage == "PhaseHold" || stage == "PhaseResume" || stage == "PhaseClockRegression";
                bool lease = stage == "NativeLease" || stage == "NativeLeaseHit" || stage == "NativeRoute";
                // Bounded per-scope episode evidence; overflow is explicit incompleteness,
                // never a hidden permission cap. Functional hold/resume continues unchanged.
                if (episode && row.Episodes >= 192) { omitted++; return; }
                if (lease && row.LeaseLines >= 192) { omitted++; Count("NativeLeaseOmitted"); return; }
                string key = stage == "Retire" || stage == "NativeOccurrenceRejected" || stage == "NativeEpochClose" || episode || lease
                    ? stage + ":" + detail.Split(' ')[0] : stage;
                if (row.Critical.Add(key)) { if (episode) row.Episodes++; if (lease) row.LeaseLines++; Write(stage, scope, row, server, detail); }
                if ((row.Retired || (row.SourceDone && row.ResultDone)) && Rows.Remove(scope))
                {
                    while (Tombstones.Count >= ActiveLimit)
                    {
                        Tombstones.Remove(TombstoneOrder.Dequeue()); tombstoneEvictions++;
                    }
                    Tombstones[scope] = row; TombstoneOrder.Enqueue(scope);
                }
                if (stage == "Retire" || stage == "Cancel" || stage == "Unavailable" || stage == "SourceStop")
                {
                    var siblings = new List<AttackPresentationScope>();
                    foreach (var pair in Rows)
                        if (pair.Key.AttackerInstanceId == scope.AttackerInstanceId && pair.Key.SequenceId == scope.SequenceId)
                            siblings.Add(pair.Key);
                    foreach (var sibling in siblings)
                        Record(stage, sibling, server, detail + " sequenceClose=1");
                }
                return;
            }
            if (!row.Sampled || !row.Critical.Add(stage)) { sampleOmissions++; return; }
            row.Lines++;
            Write(stage, scope, row, server, detail);
        }
        private static void Write(string stage, AttackPresentationScope scope, Row row, double server, string detail)
        {
            string line = Format(session, run, role, stage, scope.AttackerInstanceId.Value,
                scope.SequenceId.Value, row?.Revision ?? 0, scope.HitIndex, row?.Unit ?? -1,
                row?.Team ?? -1, UnityEngine.Time.realtimeSinceStartupAsDouble, server, detail + $" frame={UnityEngine.Time.frameCount}", row?.Type ?? -1);
            if (line.Contains("formatIncomplete=1")) formatFailures++;
            GameLog.Dev.Info("Combat", nameof(PistoleerTimingDiagnostics), line, "");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void End(double server, string reason)
        {
            if (session == null) return;
            // 종료 때 단순 삭제하면 missing-retire가 가려진다. 남은 scope의 미완료 상태를
            // 먼저 기록한 뒤 ACTIVE를 회수한다. 종료 전 늦은 결과는 tombstone으로 구별한다.
            var remaining = new List<AttackPresentationScope>(Rows.Keys);
            foreach (var scope in remaining)
                if (Rows.TryGetValue(scope, out Row row))
                    Record("Retire", scope, server, $"reason=end-missing-retire sourceDone={(row.SourceDone ? 1 : 0)} resultDone={(row.ResultDone ? 1 : 0)}");
            foreach (var pair in Counts)
                Write("Count", default, null, server, $"event={pair.Key} count={pair.Value} population=known-Pistoleer-Assault-Sniper-hooks");
            Write("End", default, null, server,
                $"sampled={blue + red} blue={blue} red={red} omitted={omitted} sampleExcluded={sampleExcluded} unclassifiedScoped={unclassifiedScoped} unscoped={unscoped} formatFailures={formatFailures} reason={reason} active={Rows.Count} peak={activePeak} capacity={ActiveLimit} population={population} activeOverflow={activeOverflow} sampleOmissions={sampleOmissions} tombstones={Tombstones.Count} tombstoneEvictions={tombstoneEvictions} coverage=critical-plus-sample");
            session = null; Rows.Clear(); Tombstones.Clear(); TombstoneOrder.Clear(); Counts.Clear();
        }
    }
}
