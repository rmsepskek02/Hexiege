using System;
using System.Collections.Generic;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using Hexiege.Presentation;
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// 서버 일정과 확정 결과를 연결하는 Coordinator의 순수 회귀 검증이다.
    /// 비교 모드와 명시적 완료 묶음을 요구하는 production 모드를 모두 검증한다.
    /// 실제 Unity 화면/네트워크 전송 품질은 별도의 adapter 및 실기 검증이 필요하다.
    /// </summary>
    public static class RunAuthoritativeAttackPresentationValidation
    {
        [MenuItem("Hexiege/Diagnostics/Self Validate Authoritative Attack Presentation")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before running presentation regression fixtures.");
            ValidateReorderAndExactlyOnce();
            ValidateIndependentExactKeys();
            ValidateRevisionAndDirection();
            ValidateRetirement();
            ValidateTimingBoundaries();
            ValidateExpiryDiagnosticWindow();
            ValidateMissingSchedule();
            ValidateCapacityAndReset();
            ValidateDamageWriterBundleContract();
            ValidateProductionAdapter();
            ValidateStrictBundleOrderAndRetirement();
            ValidateStrictMissingCompletion();
            ValidateStrictAtomicRejection();
            ValidateTransportLongMatchRetention();
            RunUnitActionSelfValidation.ValidateVfxConnections();
            Debug.Log("[UAS-AUTH-PRESENT] self-validation PASS: comparison and strict complete-bundle coordinator, "
                + "schedule/result reorder, exact-key dedupe/conflict, multi-hit/ordinal/instance isolation, "
                + "commit-versus-impact direction/revision, confirmed result after retire, "
                + "0.10s delay/0.50s catch-up boundary, missing schedule, bounded capacity/reset. "
                + "atomic multi-victim bundle, missing completion, invalid/conflicting bundle rejection, "
                + "1,964-bundle match-lifetime transport retention, "
                + "transport failure terminal evidence, self-contained type/team/HP/Impact snapshot with cacheless, AoE and dead/despawn presentation, "
                + "damage-writer-owned primary snapshot and optional hit-VFX asset classification, "
                + "no marker/tracer dependency, production HitPresentationQueue single emitter with unchanged HP. "
                + "Multiplayer visual validation remains separate.");
        }

        private static void ValidateExpiryDiagnosticWindow()
        {
            // 경계와 그 직후를 분리한다. 관측용 분류는 Coordinator의 판정을 바꾸지 않는다.
            UnitAttackShadowObserver.ClassifyExpiryWindowForValidation(
                10.1d, 10.6d, 10.6d, 10.6d,
                out bool publishLate, out bool receiveLate, out bool decisionLate);
            Require(!publishLate && !receiveLate && !decisionLate,
                "Expiry diagnostic must allow the exact catch-up boundary.");
            UnitAttackShadowObserver.ClassifyExpiryWindowForValidation(
                10.1d, 10.601d, 10.7d, 10.8d,
                out publishLate, out receiveLate, out decisionLate);
            Require(publishLate && receiveLate && decisionLate,
                "Expiry diagnostic must identify publication already past the boundary.");
            UnitAttackShadowObserver.ClassifyExpiryWindowForValidation(
                10.1d, 10.2d, 10.4d, 10.8d,
                out publishLate, out receiveLate, out decisionLate);
            Require(!publishLate && !receiveLate && decisionLate,
                "Expiry diagnostic must distinguish a post-receive decision.");
        }

        private static void ValidateReorderAndExactlyOnce()
        {
            for (int resultFirst = 0; resultFirst < 2; resultFirst++)
            {
                var coordinator = New(out var decisions);
                var schedule = Schedule();
                var result = Result();
                if (resultFirst == 1)
                {
                    Is(coordinator.ObserveResult(result, 10d), AuthoritativePresentationStatus.Accepted,
                        "Reordered confirmed result must be retained.");
                    coordinator.Tick(10.05d);
                    Require(decisions.Count == 0, "A result without schedule cannot become ready.");
                    coordinator.ObserveSchedule(schedule);
                }
                else
                {
                    coordinator.ObserveSchedule(schedule);
                    coordinator.Tick(10.05d);
                    Require(decisions.Count == 0, "A schedule without confirmed result cannot become ready.");
                    coordinator.ObserveResult(result, 10d);
                }
                Is(coordinator.ObserveSchedule(schedule), AuthoritativePresentationStatus.Duplicate,
                    "An identical schedule must dedupe.");
                Is(coordinator.ObserveSchedule(Schedule(impact: 11d)), AuthoritativePresentationStatus.Conflict,
                    "A changed schedule under the same scope must conflict.");
                Is(coordinator.ObserveResult(result, 10.01d), AuthoritativePresentationStatus.Duplicate,
                    "An identical result must dedupe before dispatch.");
                Is(coordinator.ObserveResult(Result(hp: 79), 10.01d), AuthoritativePresentationStatus.Conflict,
                    "A changed result under the same exact key must conflict.");
                coordinator.Tick(10.1d);
                coordinator.Tick(10.2d);
                Is(coordinator.ObserveResult(result, 10.2d), AuthoritativePresentationStatus.Duplicate,
                    "A consumed result tombstone must prevent replay.");
                coordinator.Tick(10.3d);
                Require(decisions.Count == 1 && decisions[0].Status == AuthoritativePresentationStatus.Ready
                    && decisions[0].Result.Equals(result) && coordinator.PendingCount == 0,
                    "Both arrival orders must produce exactly one unchanged ready result.");
            }
        }

        private static void ValidateIndependentExactKeys()
        {
            var coordinator = New(out var decisions);
            var results = new[]
            {
                Result(), Result(hit: 1), Result(ordinal: 1), Result(victim: 21),
                Result(sequence: 2), Result(instance: 2)
            };
            foreach (var result in results)
            {
                coordinator.ObserveSchedule(Schedule(scope: AttackPresentationScope.FromResultKey(result.Key)));
                Is(coordinator.ObserveResult(result, 10d), AuthoritativePresentationStatus.Accepted,
                    "Different exact keys must never collapse into one result.");
            }
            coordinator.Tick(10.1d);
            var keys = new HashSet<AttackResultKey>();
            foreach (var decision in decisions)
            {
                Require(decision.Status == AuthoritativePresentationStatus.Ready && keys.Add(decision.Result.Key),
                    "Multi-hit, ordinal, victim, sequence and instance must each remain isolated.");
            }
            Require(keys.Count == results.Length, "All exact-key comparison candidates must survive.");

            // hit 0 일정으로 hit 1 결과를 대신 승인해서는 안 된다.
            var wrongScope = New(out var wrongDecisions);
            wrongScope.ObserveSchedule(Schedule());
            wrongScope.ObserveResult(Result(hit: 1), 10d);
            wrongScope.Tick(10.1d);
            Require(wrongDecisions.Count == 0, "A neighbouring hit schedule must not authorize this hit.");
        }

        private static void ValidateRevisionAndDirection()
        {
            foreach (ulong revision in new[] { 4UL, 5UL, 7UL })
            {
                var coordinator = New(out var decisions);
                coordinator.ObserveSchedule(Schedule());
                var result = Result(revision: revision, north: true);
                coordinator.ObserveResult(result, 10d);
                coordinator.Tick(10.1d);
                var expected = revision < 5UL
                    ? AuthoritativePresentationStatus.Conflict : AuthoritativePresentationStatus.Ready;
                Require(decisions.Count == 1 && decisions[0].Status == expected,
                    "Impact revision must be at least commit revision, not necessarily identical.");
                Require(decisions[0].Result.AimDirection.Equals(Direction(true)),
                    "Impact direction must remain the confirmed result direction, not commit direction.");
            }
        }

        private static void ValidateRetirement()
        {
            var coordinator = New(out var decisions);
            coordinator.Retire(AttackerInstanceId.None);
            Require(coordinator.RetiredCount == 0 && coordinator.FailureCount == 0,
                "A unit that never opened an attack instance must retire as a normal no-op.");
            coordinator.ObserveSchedule(Schedule());
            coordinator.Retire(new AttackerInstanceId(1));
            Require(coordinator.IsVisualIntentRetired(new AttackerInstanceId(1)),
                "Retirement must close visual intent.");
            Require(decisions.Count == 0, "Retirement must not flush or invent a result.");
            coordinator.ObserveResult(Result(), 10d);
            coordinator.Tick(10.1d);
            Require(decisions.Count == 1 && decisions[0].Status == AuthoritativePresentationStatus.Ready,
                "A confirmed result arriving after death must not be retroactively invalidated.");
            coordinator.Retire(new AttackerInstanceId(1));
            coordinator.Tick(10.2d);
            Require(decisions.Count == 1 && coordinator.RetiredCount == 1,
                "Repeated retirement must neither replay the result nor grow tombstones.");

            var matchLifecycle = New(out _, 200);
            for (ulong instance = 1; instance <= 111; instance++)
                matchLifecycle.Retire(new AttackerInstanceId(instance));
            for (int neverAttacked = 0; neverAttacked < 31; neverAttacked++)
                matchLifecycle.Retire(AttackerInstanceId.None);
            Require(matchLifecycle.RetiredCount == 111
                    && matchLifecycle.FailureCount == 0,
                "A 142-unit match with 31 never-attacking units must retain exactly 111 valid retirements and no false failures.");
        }

        private static void ValidateTimingBoundaries()
        {
            // 0.1은 이진 부동소수점으로 정확하지 않으므로 작은/큰 절대 시각을 함께 검증한다.
            foreach (double impact in new[] { 0d, 1d, 10d, 10000d })
            {
                double due = impact + AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds;
                var coordinator = New(out var decisions);
                coordinator.ObserveSchedule(Schedule(impact: impact));
                coordinator.ObserveResult(Result(impact: impact), impact);
                coordinator.Tick(due - 0.001d);
                Require(decisions.Count == 0, "Presentation must wait the configured 0.10 seconds.");
                coordinator.Tick(due);
                Require(decisions.Count == 1 && decisions[0].Status == AuthoritativePresentationStatus.Ready
                    && decisions[0].IntendedServerTime == due,
                    "The exact due instant must become ready once.");

                foreach (bool beyond in new[] { false, true })
                {
                    var late = New(out var lateDecisions);
                    double arrival = due + AuthoritativeAttackPresentationCoordinator.CatchUpSeconds
                        + (beyond ? 0.001d : 0d);
                    late.ObserveSchedule(Schedule(impact: impact));
                    late.ObserveResult(Result(impact: impact), arrival);
                    late.Tick(arrival);
                    var expected = beyond ? AuthoritativePresentationStatus.Expired : AuthoritativePresentationStatus.Ready;
                    Require(lateDecisions.Count == 1 && lateDecisions[0].Status == expected,
                        "Catch-up permits exactly 0.50s but rejects 0.501s; impact=" + impact);
                    Require(lateDecisions[0].ObservedServerTime == arrival,
                        "Catch-up evidence must preserve actual observation time.");
                }
            }
        }

        private static void ValidateMissingSchedule()
        {
            var coordinator = New(out var decisions);
            coordinator.ObserveResult(Result(), 10d);
            coordinator.Tick(11.999d);
            Require(decisions.Count == 0, "Missing schedule must allow the bounded reorder window.");
            coordinator.Tick(12d);
            Require(decisions.Count == 1 && decisions[0].Status == AuthoritativePresentationStatus.MissingSchedule
                && coordinator.ReadyCount == 0 && coordinator.PendingCount == 0,
                "Missing schedule at 2s must terminally fail, never authorize a visual impact.");
            coordinator.ObserveSchedule(Schedule());
            coordinator.Tick(12.1d);
            Require(decisions.Count == 1, "Late schedule must not resurrect a failed result.");
        }

        private static void ValidateCapacityAndReset()
        {
            var coordinator = New(out var decisions, 1);
            coordinator.ObserveSchedule(Schedule());
            coordinator.ObserveResult(Result(), 10d);
            Is(coordinator.ObserveSchedule(Schedule(scope: Scope(sequence: 2))),
                AuthoritativePresentationStatus.CapacityExceeded, "Schedule capacity must fail explicitly.");
            Is(coordinator.ObserveResult(Result(sequence: 2), 10d),
                AuthoritativePresentationStatus.CapacityExceeded, "Result capacity must fail explicitly.");
            coordinator.Retire(new AttackerInstanceId(1));
            coordinator.Retire(new AttackerInstanceId(2));
            Require(coordinator.ScheduleCount == 1 && coordinator.ResultCount == 1
                && coordinator.RetiredCount == 1 && coordinator.FailureCount == 3,
                "Capacity rejection must not evict known exact-key evidence or silently grow buffers.");
            coordinator.Tick(10.1d);
            Require(decisions.Count == 1 && decisions[0].Status == AuthoritativePresentationStatus.Ready,
                "Capacity failures must preserve the previously accepted result.");
            coordinator.Clear();
            Require(coordinator.ScheduleCount == 0 && coordinator.ResultCount == 0 && coordinator.PendingCount == 0
                && coordinator.RetiredCount == 0 && coordinator.ReadyCount == 0 && coordinator.FailureCount == 0,
                "Explicit match reset must clear state and counters.");
            Is(coordinator.ObserveSchedule(default), AuthoritativePresentationStatus.Invalid,
                "An empty schedule must fail closed.");
            Is(coordinator.ObserveResult(default, 10d), AuthoritativePresentationStatus.Invalid,
                "An empty result must fail closed.");
            Is(coordinator.ObserveResult(Result(), double.NaN), AuthoritativePresentationStatus.Invalid,
                "Non-finite observation time must fail closed.");
        }

        private static void ValidateStrictBundleOrderAndRetirement()
        {
            // 개별 결과, 예약, 완결 묶음의 모든 도착 순서를 같은 시각 조건에서 비교한다.
            int[][] orders =
            {
                new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
            };
            foreach (int[] order in orders)
            foreach (bool retireFirst in new[] { false, true })
            {
                var coordinator = New(out var decisions);
                coordinator.RequireCompleteBundle = true;
                var releases = new List<AttackResultPresentationInput[]>();
                coordinator.BundleReady += results => releases.Add((AttackResultPresentationInput[])results.Clone());
                var primary = Result(revision: 7, north: true);
                var secondary = Result(victim: 21, ordinal: 1, revision: 7, north: true);
                var bundle = new[] { primary, secondary };
                if (retireFirst) coordinator.Retire(new AttackerInstanceId(1));
                foreach (int input in order)
                {
                    if (input == 0) coordinator.ObserveResult(primary, 10d);
                    if (input == 1) coordinator.ObserveSchedule(Schedule());
                    if (input == 2)
                        Is(coordinator.ObserveCompletedBundle(bundle, 10d), AuthoritativePresentationStatus.Accepted,
                            "Complete bundle must accept all valid arrival permutations.");
                    coordinator.Tick(10.05d);
                    Require(releases.Count == 0, "Complete bundle cannot release before its due time.");
                }
                if (!retireFirst) coordinator.Retire(new AttackerInstanceId(1));
                // 호출자가 원본 배열을 변경해도 이미 수락한 manifest는 불변이어야 한다.
                bundle[1] = Result(victim: 99, ordinal: 1);
                coordinator.Tick(10.1d);
                Require(releases.Count == 1 && releases[0].Length == 2
                    && releases[0][0].Equals(primary) && releases[0][1].Equals(secondary)
                    && coordinator.ReadyCount == 2 && coordinator.ReleasedBundleCount == 1,
                    "Both victims must release in one callback with their original Impact revision/direction after retire.");
                Require(decisions.Count == 2 && coordinator.FailureCount == 0,
                    "Valid strict delivery must produce exactly two ready decisions without failures.");
                Is(coordinator.ObserveCompletedBundle(new[] { primary, secondary }, 10.2d),
                    AuthoritativePresentationStatus.Duplicate, "Duplicate completion must not replay the bundle.");
                coordinator.ObserveLegacyEmit(primary.Key, 11d);
                coordinator.ObserveLegacyEmit(secondary.Key, 11d);
                coordinator.Tick(11d);
                Require(releases.Count == 1 && coordinator.ReleasedBundleCount == 1,
                    "Late Legacy/tracer emit observations cannot authorize another release.");
            }
        }

        // Existing production-adapter gate; no new menu or validator is introduced.
        private static void ValidateClockHistoryBounds()
        {
            var history = new ExpiryClockHistory();
            var frame = new AttackClockHistorySample { Stage = AttackClockHistoryStage.Frame };
            var enter = new AttackClockHistorySample { Stage = AttackClockHistoryStage.TickEnter };
            var exit = new AttackClockHistorySample { Stage = AttackClockHistoryStage.TickExit };
            var expired = new AttackClockHistorySample { Stage = AttackClockHistoryStage.Decision,
                Status = (int)AuthoritativePresentationStatus.Expired };
            Require(history.Add(frame, 1, 1, 1) == 0 && !history.Enabled,
                "Disabled history must not allocate or collect.");
            history.Reset(true);
            ulong generation = history.Generation;
            history.Add(enter, 1, 1, 1);
            for (int i = 0; i < 4; i++) history.Add(expired, 1, 1, 1);
            Require(history.WindowCount == 0, "Decision must not freeze before TickExit.");
            history.Add(exit, 1, 1, 1);
            Require(history.WindowCount == 1 && history.WindowRecordCount(0) == 6
                && !history.WindowCoverageUnknown(0), "Same Tick four failures must share complete window.");
            for (int rows = 128; rows <= 129; rows++)
            {
                history.Reset(true);
                for (int i = 0; i < rows - 3; i++) history.Add(frame, 1, 1, 1);
                history.Add(enter, 1, 1, 1); history.Add(expired, 1, 1, 1); history.Add(exit, 1, 1, 1);
                Require(history.WindowFirstSequence(0) == (ulong)(rows - 127)
                    && history.WindowLastSequence(0) == (ulong)rows
                    && history.WindowTruncated(0) == (rows == 129)
                    && history.WindowCoverageUnknown(0) == (rows == 129),
                    "128/129 prior-window truncation must be explicit even when trigger is retained.");
            }
            history.Reset(true);
            for (int i = 0; i < 1025; i++) history.Add(frame, 1, 1, 1);
            Require(history.Count == 1024 && history.Overwritten == 1
                && history.ReadForValidation(0).Sequence == 2, "1024 ring must overwrite exactly oldest row.");
            for (int w = 0; w < 5; w++)
            { history.Add(enter, 1, 1, 1); history.Add(expired, 1, 1, 1); history.Add(exit, 1, 1, 1); }
            Require(history.WindowCount == 4 && history.Suppressed == 1
                && history.WindowOverwrittenAtFreeze(0) == 4 && history.WindowCoverageUnknown(0),
                "Four frozen windows and fifth suppression must retain overwrite evidence.");
            var lines = new List<string>();
            const string identity = "runId=01234567890123456789012345678901, sharedSessionKey=0123456789012345, startedAt=100.123456, ";
            history.WriteAtEnd(lines.Add, identity);
            Require(lines.Count == 1033 && history.OutputBytes <= ExpiryClockHistory.MaximumSessionBytes,
                "Four full windows must use exactly 1033 bounded logical lines.");
            foreach (string line in lines) Require(ExpiryClockHistory.IsLineSafe(line)
                && line.StartsWith(identity), "Every emitted full envelope must preserve identity and byte preflight.");
            history.WriteAtEnd(lines.Add, identity);
            Require(lines.Count == 1033, "EndSession output must be idempotent.");
            history.Reset(true);
            Require(history.Generation > generation && history.Count == 0,
                "Begin reset needs distinct generation with empty numeric store.");
            var rpc = new AttackClockHistorySample { Stage = AttackClockHistoryStage.RpcEntry, UnitId = 10 };
            ulong ticket = history.Add(rpc, double.NaN, double.PositiveInfinity, 1);
            Require(history.HasRpcTicket(ticket, 10) && !history.HasRpcTicket(ticket, 11)
                && !history.HasRpcTicket(0, 10) && (history.ReadForValidation(0).Flags & 262U) == 256U,
                "Unknown association and nonfinite clocks must not become fabricated valid samples.");
            history.Add(enter, 1, 1, 1); history.Add(expired, 1, 1, 1);
            for (int i = 0; i < 129; i++) history.Add(frame, 1, 1, 1);
            history.Add(exit, 1, 1, 1);
            Require(history.WindowFirstSequence(0) > ticket + 2 && history.WindowCoverageUnknown(0),
                "First trigger outside 128 rows is unknown.");
            history.Reset(true);
            history.Add(new AttackClockHistorySample { Stage = AttackClockHistoryStage.EndEnter }, 1, 1, 1);
            history.Add(expired, 1, 1, 1);
            history.Add(new AttackClockHistorySample { Stage = AttackClockHistoryStage.EndExit }, 1, 1, 1);
            Require(history.WindowCoverageUnknown(0), "Complete callbacks are terminal, not invented TickExit coverage.");
            // Exercise real formatter fallback rather than a separate test formatter.
            lines.Clear(); history.WriteAtEnd(lines.Add, identity + new string('x', 650));
            Require(history.FormatFailures > 0 && lines.Count > 0, "Oversized record must take production fallback.");
            foreach (string line in lines) Require(line.StartsWith(identity) && ExpiryClockHistory.IsLineSafe(line),
                "Fallback keeps run/session identity and is itself full-bytepreflighted.");
            history.Reset(true); history.WriteAtEnd(_ => throw new InvalidOperationException(), identity);
            Require(history.DiagnosticFaults == 1, "Diagnostic sink throw must remain bounded and not escape.");
            history.Reset(false);
            Require(!history.Enabled && history.Count == 0 && history.WindowCount == 0,
                "Release/reset must drop all numeric storage.");
            history.Reset(true);
            typeof(ExpiryClockHistory).GetField("_next", System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance).SetValue(history, ulong.MaxValue);
            Require(history.Add(frame, 1, 1, 1) == 0 && history.Saturated && history.Count == 0,
                "Event sequence saturation must stop diagnostic collection without wrapping.");
            history.Reset(true);
            typeof(ExpiryClockHistory).GetField("_operation", System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance).SetValue(history, ulong.MaxValue);
            Require(history.Add(enter, 1, 1, 1) == 0 && history.Saturated,
                "Tick ticket saturation must not wrap into another operation.");
            history.Reset(true);
            typeof(ExpiryClockHistory).GetField("<DiagnosticFaults>k__BackingField", System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance).SetValue(history, int.MaxValue);
            history.RecordDiagnosticFault();
            Require(history.DiagnosticFaults == int.MaxValue && history.CounterSaturated,
                "Bounded failure counter saturation must be explicit, never wrap.");
            history.Reset(false);
        }

        private static void ValidateProductionAdapter()
        {
            ValidateClockHistoryBounds();
            ValidateCanonicalTickClock();
            GameObject owner = null;
            GameObject liveVictimView = null;
            GameObject transientVictimView = null;
            HitPresentationQueue queue = null;
            try
            {
                NetworkContext.Reset();
                NetworkContext.Set(true, true);
                Require(NetworkContext.TryBeginCombatPipelineMatch(CombatPipelineMode.ResultPresentation, 1, "fixture"),
                    "Result presentation mode must latch before adapter initialization.");
                UnitAttackResultPresentationShadowBridge.BeginMatch(true, 10d, 10d);
                UnitAttackResultPresentationShadowBridge.RegisterAuditEligibility(10, AttackPresentationAuditEligibility.Required);
                owner = new GameObject("C3 isolated presentation fixture") { hideFlags = HideFlags.HideAndDontSave };
                queue = owner.AddComponent<HitPresentationQueue>();
                var poses = new MutablePoseProvider();
                liveVictimView = new GameObject("C3 live victim view") { hideFlags = HideFlags.HideAndDontSave };
                poses.SetUnit(20, liveVictimView.transform);
                int actualVfxCalls = 0;
                Vector3 lastVfxPosition = default;
                queue.SetAuthoritativeUnitVfxEmitterForValidation((_, position) =>
                {
                    actualVfxCalls++;
                    lastVfxPosition = position;
                    return true;
                });
                queue.Initialize(null, null, null, null, null, poses);
                var victim = new UnitData(20, UnitType.SpearMan, TeamId.Red, new HexCoord(0, 0),
                    100, 10, 0.5f, 1f);
                var result = Result();
                GameEvents.OnEntityDamaged.OnNext(new EntityDamagedEvent(victim, 80, true, 10, true, false, result.Key));
                GameEvents.OnLocalAttackHit.OnNext(new LocalAttackHitPresentationEvent(10, Scope()));
                Require(queue.AuthoritativeEmits == 0, "Damage event and marker cannot authorize the new emitter.");
                UnitAttackResultPresentationShadowBridge.ObserveSchedule(Schedule());
                UnitAttackResultPresentationShadowBridge.ObserveCompletedBundle(new[] { result }, 10d, 10d);
                UnitAttackResultPresentationShadowBridge.Retire(new AttackerInstanceId(1));
                // canonical 서버 시각을 명시적으로 공급한다. 정확히 10.1을 넣으면
                // 이진 부동소수점에서 due보다 극미세하게 작아질 수 있으므로,
                // 허용 오차 변경 없이 실제 다음 프레임에 해당하는 1ms 뒤를 사용한다.
                UnitAttackResultPresentationShadowBridge.TickCoordinator(10.101d, 10.101d);
                Require(queue.AuthoritativeEmits == 1 && victim.Hp == 100,
                    "Production adapter must emit the confirmed result once after retire without writing HP. "
                    + $"queueEmits={queue.AuthoritativeEmits}, bridgeEmits="
                    + $"{UnitAttackResultPresentationShadowBridge.PresentationEmits}, "
                    + $"vfxCalls={actualVfxCalls}, victimHp={victim.Hp}.");
                GameEvents.OnLocalAttackHit.OnNext(new LocalAttackHitPresentationEvent(10, Scope()));
                UnitAttackResultPresentationShadowBridge.TickCoordinator(10.9d, 10.9d);
                Require(queue.AuthoritativeEmits == 1 && UnitAttackResultPresentationShadowBridge.PresentationEmits == 1,
                    "Late tracer/marker must not replay the consumed production result.");

                int transportFailures = UnitAttackResultPresentationShadowBridge.CompletedBundleTransportFailures;
                Require(UnitAttackResultPresentationShadowBridge.ObserveCompletedBundleTransportResult(true, "unused")
                        && UnitAttackResultPresentationShadowBridge.CompletedBundleTransportFailures == transportFailures,
                    "A successful complete-bundle publish must not create transport failure evidence.");
                Require(!UnitAttackResultPresentationShadowBridge.ObserveCompletedBundleTransportResult(false, "fixture-rejected")
                        && UnitAttackResultPresentationShadowBridge.CompletedBundleTransportFailures == transportFailures + 1,
                    "A rejected complete-bundle publish must reach the terminal transport failure counter.");

                // AoE 피해자 중 하나의 View가 없어도 각 확정 결과의 self-contained 스냅샷으로
                // 묶음 전체를 같은 시점에 정확히 한 번 방출한다. 한 피해자의 사망/Despawn이
                // 다른 피해자 연출까지 재시도 큐에 가두면 안 된다.
                var transientVictim = new UnitData(21, UnitType.SpearMan, TeamId.Red,
                    new HexCoord(1, 0), 100, 10, 0.5f, 1f);
                var readyCompanion = Result(sequence: 2, victim: 20, impact: 11d);
                var transientResult = Result(sequence: 2, victim: 21, ordinal: 1, impact: 11d);
                GameEvents.OnEntityDamaged.OnNext(new EntityDamagedEvent(
                    transientVictim, 80, true, 10, true, false, transientResult.Key));
                UnitAttackResultPresentationShadowBridge.ObserveSchedule(
                    Schedule(impact: 11d, scope: Scope(sequence: 2)));
                UnitAttackResultPresentationShadowBridge.ObserveCompletedBundle(
                    new[] { readyCompanion, transientResult }, 11d, 11d);
                UnitAttackResultPresentationShadowBridge.TickCoordinator(11.101d, 11.101d);
                Require(queue.AuthoritativeEmits == 3 && queue.AuthoritativeViewUnavailable == 0,
                    "An unavailable AoE victim View must not block self-contained bundle presentation.");
                queue.RetryAuthoritativePresentationForValidation(
                    Time.realtimeSinceStartupAsDouble);
                Require(queue.AuthoritativeEmits == 3 && queue.AuthoritativeViewUnavailable == 0,
                    "A completed self-contained AoE bundle must not remain pending or replay.");

                // 사망/Despawn으로 Transform이 사라져도 OnEntityDamaged에서 보존한 데이터와
                // C2 Impact 위치가 있으면 실제 VFX 호출 뒤에만 성공으로 소비한다.
                var deadVictim = new UnitData(22, UnitType.SpearMan, TeamId.Red,
                    new HexCoord(2, 0), 100, 10, 0.5f, 1f);
                var deadResult = Result(sequence: 3, victim: 22, impact: 12d,
                    hasPosition: true, impactX: 3f, impactZ: 4f);
                GameEvents.OnEntityDamaged.OnNext(new EntityDamagedEvent(
                    deadVictim, 0, true, 10, true, false, deadResult.Key));
                UnitAttackResultPresentationShadowBridge.ObserveSchedule(
                    Schedule(impact: 12d, scope: Scope(sequence: 3)));
                UnitAttackResultPresentationShadowBridge.ObserveCompletedBundle(
                    new[] { deadResult }, 12d, 12d);
                UnitAttackResultPresentationShadowBridge.TickCoordinator(12.101d, 12.101d);
                Require(queue.AuthoritativeEmits == 4 && actualVfxCalls >= 3
                    && lastVfxPosition == new Vector3(3f, 0f, 4f),
                    "A cached dead/despawned victim must use the authoritative Impact position and a real VFX call.");

                // 실제 멀티플레이에서는 완료 묶음이 HP 동기화 이벤트보다 먼저 오거나,
                // 치명타 직후 피해자 Domain/View가 이미 제거될 수 있다. 확정 결과가
                // 별도 OnEntityDamaged 캐시 없이도 필수 표현을 만들 수 있어야 한다.
                // 실제 Android Red Client와 같은 조건을 만든다. 서버가 보낸 Impact 위치는
                // Blue 기준 canonical 좌표이고, 화면의 VisualRoot는 맵 중심을 기준으로
                // 반전되어 있으므로 C3 최종 방출 위치도 같은 view 좌표여야 한다.
                NetworkContext.Set(false, true);
                Vector3 flippedMapCenter = new Vector3(10f, 0f, 20f);
                ViewConverter.Setup(true, flippedMapCenter);
                var cachelessResult = Result(sequence: 4, victim: 23, impact: 13d,
                    hasPosition: true, impactX: 5f, impactZ: 6f);
                UnitAttackResultPresentationShadowBridge.ObserveSchedule(
                    Schedule(impact: 13d, scope: Scope(sequence: 4)));
                UnitAttackResultPresentationShadowBridge.ObserveCompletedBundle(
                    new[] { cachelessResult }, 13d, 13d);
                UnitAttackResultPresentationShadowBridge.TickCoordinator(13.101d, 13.101d);
                Vector3 expectedFlippedPosition = new Vector3(15f, 0f, 34f);
                Require(queue.AuthoritativeEmits == 5
                    && lastVfxPosition == expectedFlippedPosition,
                    "A pure flipped client must present the canonical confirmed Impact at the same view-space position as its VisualRoot. "
                    + $"expected={expectedFlippedPosition}, actual={lastVfxPosition}.");
                Require(UnitAttackResultPresentationShadowBridge.PresentationHitVfxConfigured == 5
                        && UnitAttackResultPresentationShadowBridge.PresentationHitVfxEmitted == 5
                        && UnitAttackResultPresentationShadowBridge.PresentationHitVfxSkippedNoAsset == 0,
                    "A configured hit-VFX channel must count only successful real emissions.");
                int skippedNoAsset = UnitAttackResultPresentationShadowBridge.PresentationHitVfxSkippedNoAsset;
                int viewUnavailable = UnitAttackResultPresentationShadowBridge.PresentationViewUnavailable;
                UnitAttackResultPresentationShadowBridge.RecordHitVfxOutcome(false, false);
                Require(UnitAttackResultPresentationShadowBridge.PresentationHitVfxSkippedNoAsset
                            == skippedNoAsset + 1
                        && UnitAttackResultPresentationShadowBridge.PresentationViewUnavailable
                            == viewUnavailable,
                    "A missing optional hit-VFX asset must be SkippedNoAsset, not a presentation failure.");

                // 동일한 production queue에 C2 진단 on/off를 각각 통과시킨다. 진단용
                // 개별 결과의 시계가 앞으로 뛰어도 완료 묶음의 표현 기준점은 불변이다.
                // 진짜 늦은 Tick은 여전히 Expired이며, 허용 시간 확대는 하지 않는다.
                double baselineNow = double.NaN;
                for (int historyEnabled = 0; historyEnabled < 2; historyEnabled++)
                for (int late = 0; late < 2; late++)
                for (int diagnostic = 0; diagnostic < 2; diagnostic++)
                {
                    var history = new ExpiryClockHistory();
                    history.Reset(historyEnabled == 1);
                    Action<AttackClockHistorySample> collectClock = sample =>
                        history.Add(sample, 1000d, 100d, 1);
                    Action<AuthoritativePresentationDecision> collectHistoryDecision = decision =>
                        history.Add(new AttackClockHistorySample { Stage = AttackClockHistoryStage.Decision,
                            Status = (int)decision.Status, Key = decision.Result.Key }, 1000d, 100d, 1);
                    if (historyEnabled == 1)
                    {
                        UnitAttackResultPresentationShadowBridge.ClockHistory += collectClock;
                        UnitAttackResultPresentationShadowBridge.Coordinator.Decided += collectHistoryDecision;
                    }
                    try
                    {
                    UnitAttackResultPresentationShadowBridge.BeginMatch(true, 100d, 1000d);
                    UnitAttackResultPresentationShadowBridge.RegisterAuditEligibility(10,
                        AttackPresentationAuditEligibility.Required);
                    ulong sequence = (ulong)(10 + late * 2 + diagnostic);
                    var isolatedResult = Result(sequence: sequence, impact: 100d,
                        hasPosition: true, impactX: 5f, impactZ: 6f);
                    UnitAttackResultPresentationShadowBridge.ObserveSchedule(
                        Schedule(impact: 100d, scope: Scope(sequence: sequence)));
                    UnitAttackResultPresentationShadowBridge.ObserveCompletedBundle(
                        new[] { isolatedResult }, 100d, 1000d);
                    UnitAttackResultPresentationShadowBridge.ReadClockAnchorDiagnostic(
                        out double anchorServer, out double anchorLocal,
                        out int anchorRevision, out string anchorSource);
                    double beforeNow = UnitAttackResultPresentationShadowBridge.CurrentServerTime;
                    if (diagnostic == 1)
                    {
                        var classifications = new List<PresentationShadowClassification>();
                        Action<PresentationShadowClassification> collectClassification = classifications.Add;
                        UnitAttackResultPresentationShadowBridge.Classified += collectClassification;
                        try
                        {
                            UnitAttackResultPresentationShadowBridge.ObserveResult(
                                Result(sequence: 100 + sequence, impact: 200d), 100.7d, 1000.1d);
                        }
                        finally
                        {
                            UnitAttackResultPresentationShadowBridge.Classified -= collectClassification;
                        }
                        Require(classifications.Count == 1
                                && classifications[0].Status == PresentationShadowResultStatus.Scheduled,
                            "Required C2 input must still reach the comparison scheduler and its unchanged classification.");
                        UnitAttackResultPresentationShadowBridge.ReadClockAnchorDiagnostic(
                            out double afterServer, out double afterLocal,
                            out int afterRevision, out string afterSource);
                        Require(anchorServer == afterServer && anchorLocal == afterLocal
                                && anchorRevision == afterRevision && anchorSource == afterSource
                                && beforeNow == UnitAttackResultPresentationShadowBridge.CurrentServerTime,
                            "C2 comparison diagnostics must not mutate the live presentation clock or its anchor identity.");
                    }
                    Require(anchorSource == "completed-bundle" && anchorRevision == 2,
                        "Normal completed bundles must still update the diagnostic clock anchor.");
                    int beforeEmits = queue.AuthoritativeEmits;
                    int beforeVfx = actualVfxCalls;
                    var decisions = new List<AuthoritativePresentationDecision>();
                    Action<AuthoritativePresentationDecision> collectDecision = decisions.Add;
                    UnitAttackResultPresentationShadowBridge.Coordinator.Decided += collectDecision;
                    try
                    {
                        UnitAttackResultPresentationShadowBridge.TickCoordinator(
                            late == 0 ? 100.101d : 100.601d, late == 0 ? 1000.101d : 1000.601d);
                    }
                    finally
                    {
                        UnitAttackResultPresentationShadowBridge.Coordinator.Decided -= collectDecision;
                    }
                    double now = UnitAttackResultPresentationShadowBridge.CurrentServerTime;
                    if (diagnostic == 0) baselineNow = now;
                    Require(now == baselineNow,
                        "C2 diagnostic on/off must reconstruct the identical coordinator time.");
                    Require(decisions.Count == 1
                            && decisions[0].Status == (late == 0
                                ? AuthoritativePresentationStatus.Ready : AuthoritativePresentationStatus.Expired)
                            && UnitAttackResultPresentationShadowBridge.Coordinator.ReadyCount == (late == 0 ? 1 : 0)
                            && UnitAttackResultPresentationShadowBridge.Coordinator.FailureCount == (late == 0 ? 0 : 1)
                            && queue.AuthoritativeEmits - beforeEmits == (late == 0 ? 1 : 0)
                            && actualVfxCalls - beforeVfx == (late == 0 ? 1 : 0)
                            && (late != 0 || lastVfxPosition == expectedFlippedPosition)
                            && victim.Hp == 100,
                        "C2 on/off must retain the same exactly-once real emitter; genuine late expiry and HP authority remain unchanged.");
                    UnitAttackResultPresentationShadowBridge.TickCoordinator(100.9d, 1000.9d);
                    Require(queue.AuthoritativeEmits - beforeEmits == (late == 0 ? 1 : 0),
                        "Diagnostic isolation must not replay a consumed or expired bundle.");
                    Require(history.WindowCount == (historyEnabled == 1 && late == 1 ? 1 : 0),
                        "History on/off must observe genuine expiry without creating another decision.");
                    }
                    finally
                    {
                        UnitAttackResultPresentationShadowBridge.ClockHistory -= collectClock;
                        UnitAttackResultPresentationShadowBridge.Coordinator.Decided -= collectHistoryDecision;
                        history.Reset(false);
                    }
                }

                // 비교 전용 모드에서는 기존처럼 개별 C2가 clock anchor를 갱신한다.
                NetworkContext.Reset();
                Require(NetworkContext.TryBeginCombatPipelineMatch(
                        CombatPipelineMode.PresentationShadow, 1, "fixture-shadow"),
                    "Shadow comparison mode must latch independently.");
                UnitAttackResultPresentationShadowBridge.BeginMatch(true, 100d, 1000d);
                UnitAttackResultPresentationShadowBridge.RegisterAuditEligibility(10,
                    AttackPresentationAuditEligibility.Required);
                UnitAttackResultPresentationShadowBridge.ObserveResult(Result(impact: 100d), 100.7d, 1000.1d);
                UnitAttackResultPresentationShadowBridge.ReadClockAnchorDiagnostic(
                    out double shadowServer, out double shadowLocal,
                    out int shadowRevision, out string shadowSource);
                Require(shadowServer == 100.7d && shadowLocal == 1000.1d
                        && shadowRevision == 2 && shadowSource == "result"
                        && UnitAttackResultPresentationShadowBridge.CurrentServerTime == 100.7d,
                    "PresentationShadow must retain its existing individual-result clock behavior.");
            }
            finally
            {
                queue?.Dispose();
                if (transientVictimView != null) UnityEngine.Object.DestroyImmediate(transientVictimView);
                if (liveVictimView != null) UnityEngine.Object.DestroyImmediate(liveVictimView);
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                UnitAttackResultPresentationShadowBridge.EndMatch();
                NetworkContext.Reset();
                ViewConverter.Reset();
                UnitAttackResultPresentationShadowBridge.BeginMatch(false);
            }
        }

        private static void ValidateDamageWriterBundleContract()
        {
            Require(WorldPointXZ.TryCreate(3d, 4d, out WorldPointXZ impactPosition),
                "Damage-writer fixture position must be valid.");
            var key = new AttackResultKey(
                new AttackerInstanceId(41UL), new AttackSequenceId(7UL),
                0, (int)EntityKind.Unit, 52, 1, 0);
            Require(AttackImpactResult.TryCreate(
                    9UL, key, 20d, Direction(false), true, impactPosition,
                    AttackImpactOutcome.HitApplied, 15, 85, out AttackImpactResult hit),
                "A writer-owned primary hit fixture must be valid.");
            var observation = new AttackDamageObservation(
                AttackDamageApplyStatus.Applied, 15, 85,
                (int)UnitType.SpearMan, (int)TeamId.Red,
                true, impactPosition);
            var fact = new AppliedAttackPresentationFact(
                (int)EntityKind.Unit, 52, 1, 0, 15, 85,
                (int)UnitType.SpearMan, (int)TeamId.Red,
                true, impactPosition);
            Require(AttackPresentationBundleAssembler.TryCreate(
                    10, hit, AttackDeliveryKind.Hitscan,
                    new AttackDamagePresentationBundleObservation(
                        observation, new[] { fact }, AttackDamagePresentationBundleStatus.Complete),
                    out AttackResultPresentationInput[] inputs)
                    && inputs.Length == 1
                    && inputs[0].HasImpactPosition
                    && inputs[0].ImpactPosition.Equals(impactPosition)
                    && inputs[0].HasPresentationSnapshot,
                "The primary completed bundle must carry the damage writer's exact position/type/team snapshot.");

            var missingSnapshot = new AttackDamageObservation(
                AttackDamageApplyStatus.Applied, 15, 85);
            Require(!missingSnapshot.IsValid
                    && !AttackPresentationBundleAssembler.TryCreate(
                        10, hit, AttackDeliveryKind.Hitscan,
                        new AttackDamagePresentationBundleObservation(
                            missingSnapshot, new[] { fact }, AttackDamagePresentationBundleStatus.Complete),
                        out _),
                "A fact must not repair a primary result whose damage-writer snapshot is missing.");

            Require(AttackImpactResult.TryCreate(
                    10UL, key, 21d, Direction(false), true, impactPosition,
                    AttackImpactOutcome.StatusEffectApplied, 0, 85,
                    out AttackImpactResult statusEffect)
                    && AttackPresentationBundleAssembler.TryCreate(
                        10, statusEffect, AttackDeliveryKind.Hitscan,
                        new AttackDamagePresentationBundleObservation(
                            new AttackDamageObservation(
                                AttackDamageApplyStatus.Applied, 0, 85,
                                (int)UnitType.SpearMan, (int)TeamId.Red,
                                true, impactPosition),
                            Array.Empty<AppliedAttackPresentationFact>(),
                            AttackDamagePresentationBundleStatus.Complete),
                        out AttackResultPresentationInput[] statusInputs)
                    && statusInputs.Length == 1
                    && statusInputs[0].HasPresentationSnapshot,
                "A zero-damage StatusEffectApplied result must remain self-contained without a positive-damage fact.");
        }

        private static void ValidateStrictMissingCompletion()
        {
            var coordinator = New(out var decisions);
            coordinator.RequireCompleteBundle = true;
            int releases = 0;
            coordinator.BundleReady += _ => releases++;
            coordinator.ObserveSchedule(Schedule());
            coordinator.ObserveResult(Result(), 10d);
            coordinator.Tick(10.1d);
            coordinator.Tick(11.999d);
            Require(decisions.Count == 0 && releases == 0,
                "An individual result must not imply AoE completion, even with a valid schedule.");
            coordinator.Tick(12d);
            Require(decisions.Count == 1 && decisions[0].Status == AuthoritativePresentationStatus.MissingBundle
                && releases == 0 && coordinator.ReadyCount == 0,
                "A missing explicit completion must fail MissingBundle without any partial release.");
            coordinator.ObserveCompletedBundle(new[] { Result(), Result(victim: 21, ordinal: 1) }, 12.1d);
            coordinator.Tick(12.1d);
            Require(releases == 0, "An expired partial bundle cannot be resurrected by late completion.");
        }

        private static void ValidateStrictAtomicRejection()
        {
            var coordinator = New(out _);
            coordinator.RequireCompleteBundle = true;
            var primary = Result();
            var secondary = Result(victim: 21, ordinal: 1);
            Is(coordinator.ObserveCompletedBundle(new[] { primary, primary }, 10d),
                AuthoritativePresentationStatus.Conflict, "Repeated exact keys within a manifest must fail.");
            Require(coordinator.ResultCount == 0 && coordinator.BundleCount == 0,
                "Rejected duplicate keys must not partially insert results.");
            Is(coordinator.ObserveCompletedBundle(new[] { primary, Result(hit: 1) }, 10d),
                AuthoritativePresentationStatus.Invalid, "A manifest cannot mix hit scopes.");
            Require(coordinator.ResultCount == 0 && coordinator.BundleCount == 0,
                "Rejected mixed scopes must not partially insert results.");
            var undefinedOutcome = new AttackResultPresentationInput(10, 5UL, secondary.Key,
                AttackDeliveryKind.Hitscan, 10d, Direction(false), false, default,
                (AttackImpactOutcome)255, 0, 80);
            Is(coordinator.ObserveCompletedBundle(new[] { primary, undefinedOutcome }, 10d),
                AuthoritativePresentationStatus.Invalid, "Undefined outcomes must fail before any bundle mutation.");
            Require(coordinator.ResultCount == 0 && coordinator.BundleCount == 0,
                "Invalid trailing result must not leave a valid prefix or incomplete bundle stored.");

            var incompleteVisual = new AttackResultPresentationInput(
                primary.AttackerUnitId, primary.ActionRevision, primary.Key,
                primary.Delivery, primary.ImpactServerTime, primary.AimDirection,
                primary.HasImpactPosition, primary.ImpactPosition, primary.Outcome,
                primary.AppliedAmount, primary.ResultingHp);
            Is(coordinator.ObserveCompletedBundle(new[] { incompleteVisual }, 10d),
                AuthoritativePresentationStatus.Invalid,
                "A visual complete bundle without victim type/team snapshot must fail closed.");
            Require(!AttackPresentationBundleAssembler.TryToNetwork(
                    new[] { incompleteVisual }, out _),
                "Transport must reject an incomplete visual snapshot before publication.");
            Require(coordinator.ResultCount == 0 && coordinator.BundleCount == 0,
                "Incomplete presentation snapshots must not mutate coordinator state.");

            coordinator.ObserveResult(primary, 10d);
            Is(coordinator.ObserveCompletedBundle(new[] { Result(hp: 79), secondary }, 10d),
                AuthoritativePresentationStatus.Conflict, "Manifest payload must agree with the previously confirmed result.");
            Require(coordinator.ResultCount == 1 && coordinator.BundleCount == 0,
                "Conflicting payload must preserve old evidence and reject all new entries.");
            Is(coordinator.ObserveCompletedBundle(new[] { primary, secondary }, 10d),
                AuthoritativePresentationStatus.Accepted, "Valid bundle must remain admissible after rejected input.");
            Is(coordinator.ObserveCompletedBundle(new[] { primary }, 10d),
                AuthoritativePresentationStatus.Conflict, "A completed manifest cannot silently lose a victim.");
            Is(coordinator.ObserveCompletedBundle(new[] { primary, Result(victim: 21, ordinal: 1, hp: 79) }, 10d),
                AuthoritativePresentationStatus.Conflict, "Completed manifest payload is immutable.");
            Require(coordinator.ResultCount == 2 && coordinator.BundleCount == 1,
                "Manifest conflicts must preserve the accepted complete bundle.");
            Is(coordinator.ObserveResult(Result(victim: 99, ordinal: 2), 10d),
                AuthoritativePresentationStatus.Conflict,
                "A completed manifest must reject later same-scope keys outside its explicit membership.");
            Require(coordinator.ResultCount == 2,
                "An undeclared result must not become an unobservable ready candidate.");

            var omitted = new AuthoritativeAttackPresentationCoordinator { RequireCompleteBundle = true };
            omitted.ObserveResult(secondary, 10d);
            Is(omitted.ObserveCompletedBundle(new[] { primary }, 10d),
                AuthoritativePresentationStatus.Conflict,
                "Completion must not omit a previously accepted same-scope result.");
            Require(omitted.BundleCount == 0 && omitted.ResultCount == 1,
                "Incomplete membership must reject atomically and preserve original evidence.");

            var bounded = new AuthoritativeAttackPresentationCoordinator(1) { RequireCompleteBundle = true };
            Is(bounded.ObserveCompletedBundle(new[] { primary, secondary }, 10d),
                AuthoritativePresentationStatus.CapacityExceeded, "Bundle admission must reserve capacity for all victims.");
            Require(bounded.ResultCount == 0 && bounded.BundleCount == 0,
                "Capacity rejection must not admit a partial prefix.");
        }

        private static void ValidateTransportLongMatchRetention()
        {
            // 직전 멀티 실측은 한 경기에서 1,964개 결과를 만들었다. 전송 중복 방지 표가
            // 예전 256개 제한을 유지하면 257번째부터 정상 공격 연출이 전부 사라지므로,
            // 실제 경기 규모를 그대로 재현해 경기 수명 tombstone 보존을 고정한다.
            const int observedMatchBundleCount = 1964;
            var classifier = new NetworkAttackPresentationBundleClassifier();
            NetworkAttackImpactShadowResult[] firstPayload = null;
            for (ulong sequence = 1; sequence <= observedMatchBundleCount; sequence++)
            {
                Require(AttackPresentationBundleAssembler.TryToNetwork(
                        new[] { Result(sequence: sequence) },
                        out NetworkAttackImpactShadowResult[] payload),
                    "A valid result must serialize into one complete transport bundle.");
                Require(payload.Length == 1
                        && payload[0].VictimPresentationType == (int)UnitType.SpearMan
                        && payload[0].VictimTeam == (int)TeamId.Red,
                    "Complete-bundle transport must retain the victim presentation snapshot.");
                if (sequence == 1) firstPayload = payload;
                Require(classifier.Classify(10, payload)
                        == NetworkAttackPresentationBundleStatus.Accepted,
                    "A normal 1,964-bundle match must not hit the transport tombstone capacity.");
            }
            Require(classifier.Classify(10, firstPayload)
                    == NetworkAttackPresentationBundleStatus.Duplicate,
                "An old Reliable retransmission must remain a duplicate for the whole match.");
            classifier.Clear();
            Require(classifier.Classify(10, firstPayload)
                    == NetworkAttackPresentationBundleStatus.Accepted,
                "Match reset must release transport tombstones for the next match.");
        }

        private static void ValidateCanonicalTickClock()
        {
            ValidateReceiptPhaseClock();
            // Captured window 1에서 Bundle anchor와 canonical Tick의 시각축이 달랐다.
            // 기존 Bridge/실제 Presenter 경로를 통과하지만 누락된 원본 RPC 체인을
            // 재구성하지는 않는다. 실제 exact-key 14건 대비는 별도 기존 trace replay에 있다.
            var decisions = new List<AuthoritativePresentationDecision>();
            Action<AuthoritativePresentationDecision> collect = decisions.Add;
            UnitAttackResultPresentationShadowBridge.Coordinator.Decided += collect;
            try
            {
                NetworkContext.Reset(); NetworkContext.Set(true, false);
                Require(NetworkContext.TryBeginCombatPipelineMatch(CombatPipelineMode.ResultPresentation, 1, "canonical-clock"), "Clock fixture mode.");
                UnitAttackResultPresentationShadowBridge.BeginMatch(true, 151d, 2564d);
                UnitAttackResultPresentationShadowBridge.RegisterAuditEligibility(10, AttackPresentationAuditEligibility.Required);
                const double impact = 151.78150362100453d;
                for (ulong i = 1; i <= 14; i++)
                {
                    var result = Result(sequence: i, impact: impact);
                    UnitAttackResultPresentationShadowBridge.ObserveSchedule(Schedule(impact, AttackPresentationScope.FromResultKey(result.Key)));
                    UnitAttackResultPresentationShadowBridge.ObserveCompletedBundle(new[] { result },
                        152.51657693978399d, 2564.548330513891d);
                }
                var clock = new UnitAttackResultPresentationShadow.CanonicalTickClock();
                int queries = 0;
                clock.Bind(() => { queries++; return 151.98334474861622d; });
                Require(clock.Tick(2564.5520280528867d) && queries == 1, "Production Tick reads the canonical provider once.");
                Require(decisions.Count == 14 && decisions.TrueForAll(d => d.Status == AuthoritativePresentationStatus.Ready),
                    "Captured stale Bundle epoch must not expire the 14 seeded results; canonical Tick=151.98334474861622.");
                Require(Math.Abs(UnitAttackResultPresentationShadowBridge.CurrentServerTime - 151.98334474861622d) < 1e-9,
                    "Bundle epoch is diagnostic only.");
                Require(UnitAttackResultPresentationShadowBridge.CanonicalAnchorDeltaSamples == 1
                    && UnitAttackResultPresentationShadowBridge.CanonicalAnchorDeltaMax > 0.536d, "Retain old-anchor versus Tick delta.");
                decisions.Clear();
                var pending = Result(sequence: 30, impact: impact);
                UnitAttackResultPresentationShadowBridge.ObserveSchedule(Schedule(impact, AttackPresentationScope.FromResultKey(pending.Key)));
                UnitAttackResultPresentationShadowBridge.ObserveCompletedBundle(new[] { pending }, 200d, 2564d);
                double prior = UnitAttackResultPresentationShadowBridge.CurrentServerTime;
                int count = UnitAttackResultPresentationShadowBridge.Coordinator.PendingCount;
                foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d })
                {
                    queries = 0; clock.Bind(() => { queries++; return invalid; });
                    Require(!clock.Tick(2564.6d) && queries == 1, "Invalid provider fails closed with one query.");
                }
                clock.Bind(() => throw new InvalidOperationException("fixture supplier fault"));
                Require(!clock.Tick(2564.6d) && UnitAttackResultPresentationShadowBridge.CanonicalClockFaults == 1, "Supplier fault counted separately.");
                clock.Close(); Require(!clock.Tick(2564.6d), "Closed provider cannot Tick.");
                Require(decisions.Count == 0 && UnitAttackResultPresentationShadowBridge.Coordinator.PendingCount == count
                    && UnitAttackResultPresentationShadowBridge.CurrentServerTime == prior, "Invalid/closed clock preserves pending state, with no old-anchor fallback.");
                int oldQueries = 0, newQueries = 0;
                clock.Bind(() => { oldQueries++; return 152d; }); clock.Close();
                clock.Bind(() => { newQueries++; return 152.6d; });
                Require(clock.Tick(2564.7d) && oldQueries == 0 && newQueries == 1, "Explicit rebind cannot query retired supplier.");
                Require(decisions.Count == 1 && decisions[0].Status == AuthoritativePresentationStatus.Expired, "Genuinely late still expires.");
                UnitAttackResultPresentationShadowBridge.EndMatch();
                Require(!clock.Tick(2564.8d) && newQueries == 1, "Inactive bridge performs zero queries."); clock.Close();
                foreach (var mode in new[] { CombatPipelineMode.PresentationShadow, CombatPipelineMode.ResultPresentation })
                {
                    NetworkContext.Reset(); NetworkContext.Set(true, false);
                    Require(NetworkContext.TryBeginCombatPipelineMatch(mode, 1, "clock-mode"), "Clock mode latch.");
                    UnitAttackResultPresentationShadowBridge.BeginMatch(true);
                    queries = 0; clock.Bind(() => { queries++; return 100d; });
                    Action<AttackClockHistorySample> badObserver = _ => throw new InvalidOperationException("diagnostic fixture");
                    UnitAttackResultPresentationShadowBridge.ClockHistory += badObserver;
                    try
                    {
                        Require(clock.Tick(1000d, (_, __) => throw new InvalidOperationException("frame diagnostic fixture"))
                            && queries == 1 && UnitAttackResultPresentationShadowBridge.CurrentServerTime == 100d,
                            "Canonical Tick works before Bundle anchor; throwing diagnostics cannot interfere or requery.");
                        Require(UnitAttackResultPresentationShadowBridge.ClockHistoryFaults == 3, "Frame/Enter/Exit diagnostic failures remain observable.");
                    }
                    finally { UnitAttackResultPresentationShadowBridge.ClockHistory -= badObserver; }
                    UnitAttackResultPresentationShadowBridge.EndMatch(); clock.Close();
                }
                NetworkContext.Reset(); UnitAttackResultPresentationShadowBridge.BeginMatch(false);
                queries = 0; clock.Bind(() => { queries++; return 100d; });
                Require(!clock.Tick(1000d) && queries == 0, "Legacy/solo inactive path has no canonical query.");
                clock.Close();
                // 실제 END formatter와 원래 UTF-8 preflight를 그대로 사용한다.
                // 모든 숫자를 double 최대 문자열로 부풀려 int 집계 상한보다 보수적으로 검사한다.
                var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
                string terminal = (string)typeof(UnitAttackShadowObserver).GetMethod("BuildCanonicalClockTerminalData", flags).Invoke(null, null);
                string worst = System.Text.RegularExpressions.Regex.Replace(terminal,
                    @"(?<==)(?:NaN|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)(?=,|$)",
                    double.MaxValue.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
                string identity = "runId=" + new string('a', 32) + ", sharedSessionKey=" + new string('b', 64) + ", startedAt=2147483647.123456, ";
                Require((bool)typeof(UnitAttackShadowObserver).GetMethod("IsFullLineUtf8Safe", flags)
                    .Invoke(null, new object[] { "coordinator-clock-END", identity + worst }), "Canonical END stays inside actual production byte preflight.");
                var ring = new ExpiryClockHistory(); ring.Reset(true);
                ring.Add(new AttackClockHistorySample { Stage = AttackClockHistoryStage.TickEnter, ArgumentServer = 100d }, 1000d, 100d, 1);
                Require((ring.ReadForValidation(0).Flags & 4096U) != 0, "Supplied canonical evidence is distinguishable from fresh NGO queries.");
                ring.Reset(false);
            }
            finally
            {
                UnitAttackResultPresentationShadowBridge.Coordinator.Decided -= collect;
                UnitAttackResultPresentationShadowBridge.EndMatch(); NetworkContext.Reset();
            }
        }

        // 실제 canonical 공급자를 통과한다. 수신 값이 이전 표본이어도 현재 Tick을
        // 과거로 돌리지 않으며, 100ms 지연과 0.50초 catch-up 경계는 그대로 유지한다.
        public static void ValidateReceiptPhaseClock()
        {
            var decisions = new List<AuthoritativePresentationDecision>();
            Action<AuthoritativePresentationDecision> collect = decisions.Add;
            UnitAttackResultPresentationShadowBridge.Coordinator.Decided += collect;
            var clock = new UnitAttackResultPresentationShadow.CanonicalTickClock();
            try
            {
                foreach (double impact in new[] { 190.127219d, 190.2d })
                {
                    decisions.Clear();
                    NetworkContext.Reset(); NetworkContext.Set(true, false);
                    Require(NetworkContext.TryBeginCombatPipelineMatch(CombatPipelineMode.ResultPresentation, 1, "receipt-phase"), "Receipt phase mode.");
                    UnitAttackResultPresentationShadowBridge.BeginMatch(true, 189.62629d, 348d);
                    UnitAttackResultPresentationShadowBridge.RegisterAuditEligibility(10, AttackPresentationAuditEligibility.Required);
                    var result = Result(impact: impact);
                    UnitAttackResultPresentationShadowBridge.ObserveSchedule(Schedule(impact, AttackPresentationScope.FromResultKey(result.Key)));
                    UnitAttackResultPresentationShadowBridge.ObserveCompletedBundle(new[] { result }, 189.62629d, 348d);
                    int queries = 0;
                    clock.Bind(() => { queries++; return 190.736736d; });
                    Require(clock.Tick(348.4d) && queries == 1, "Actual canonical provider queried exactly once.");
                    Require(decisions.Count == 1 && decisions[0].Status == (impact == 190.127219d
                        ? AuthoritativePresentationStatus.Expired : AuthoritativePresentationStatus.Ready),
                        "Previous receipt sample cannot rescue genuine expiry; legitimate catch-up remains Ready.");
                    Require(UnitAttackResultPresentationShadowBridge.CurrentServerTime == 190.736736d,
                        "Diagnostics cannot replace canonical decision time.");
                    UnitAttackResultPresentationShadowBridge.EndMatch(); clock.Close();
                }
                string sample = UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(189.62629d, 348d, 32876, 190.736736d, 348.4d, 32876, true);
                Require(sample.Contains("sameFrame=True") && sample.Contains("sampleDeltaKnown=True")
                    && sample.Contains("networkLateness=unproven"), "Phase diagnostic preserves uncertainty.");
                Require(UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(double.NaN, 0, 1, 1, 1, 2, true)
                    .Contains("sampleAdvanceResidual=unknown"), "Invalid diagnostic samples cannot become evidence.");
                string lifecycle = UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(189d, 348d, 1, 190d, 349d, 1);
                Require(lifecycle.Contains("decisionClockDomain=lifecycle-or-validation")
                    && lifecycle.Contains("sampleDeltaKnown=False") && !lifecycle.Contains("canonical-decision-sample"),
                    "Lifecycle/validation cannot claim a fresh canonical decision sample.");
                foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d })
                {
                    foreach (string invalidSample in new[] {
                        UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(invalid, 348d, 1, 190d, 349d, 1, true),
                        UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(189d, invalid, 1, 190d, 349d, 1, true),
                        UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(189d, 348d, 1, invalid, 349d, 1, true),
                        UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(189d, 348d, 1, 190d, invalid, 1, true) })
                        Require(invalidSample.Contains("sampleDeltaKnown=False") && !invalidSample.Contains("NaN")
                            && !invalidSample.Contains("Infinity"), "Invalid times must format as unknown, never numeric evidence.");
                }
                foreach (string invalidFrame in new[] {
                    UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(189d, 348d, -1, 190d, 349d, 1, true),
                    UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(189d, 348d, 2, 190d, 349d, 1, true),
                    UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(189d, 349d, 1, 190d, 348d, 1, true) })
                    Require(invalidFrame.Contains("sampleDeltaKnown=False"), "Invalid frame/local order is unknown.");
                var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
                string identity = "runId=" + new string('a', 32) + ", sharedSessionKey=" + new string('b', 64)
                    + ", startedAt=2147483647.123456, ";
                string keyData = "role=client, keyUnit=2147483647, instance=18446744073709551615, sequence=18446744073709551615, hit=2147483647, victimKind=2147483647, victimId=2147483647, effectKind=2147483647, ordinal=2147483647, ";
                foreach (string bounded in new[] { sample, lifecycle,
                    UnitAttackShadowObserver.FormatExpiryPhaseClockDiagnostic(0, 0, 0, double.MaxValue, 1e-200, int.MaxValue, true) })
                    Require((bool)typeof(UnitAttackShadowObserver).GetMethod("IsFullLineUtf8Safe", flags)
                        .Invoke(null, new object[] { "coordinator-expiry-timing-D", identity + keyData + bounded }),
                        "D retains actual full-line UTF-8 preflight budget under the existing 16-detail guard.");
            }
            finally
            {
                clock.Close(); UnitAttackResultPresentationShadowBridge.Coordinator.Decided -= collect;
                UnitAttackResultPresentationShadowBridge.EndMatch(); NetworkContext.Reset();
            }
        }

        private static AuthoritativeAttackPresentationCoordinator New(
            out List<AuthoritativePresentationDecision> decisions, int capacity = 64)
        {
            var collected = new List<AuthoritativePresentationDecision>();
            var coordinator = new AuthoritativeAttackPresentationCoordinator(capacity);
            coordinator.Decided += collected.Add;
            decisions = collected;
            return coordinator;
        }

        private static AttackPresentationScope Scope(ulong instance = 1, ulong sequence = 1, int hit = 0)
            => new AttackPresentationScope(new AttackerInstanceId(instance), new AttackSequenceId(sequence), hit);

        private static AttackPresentationSchedule Schedule(double impact = 10d,
            AttackPresentationScope scope = default)
            => new AttackPresentationSchedule(10, scope.IsValid ? scope : Scope(), 5UL,
                AttackDeliveryKind.Hitscan, impact, Direction(false));

        private static AttackResultPresentationInput Result(ulong instance = 1, ulong sequence = 1,
            int hit = 0, int ordinal = 0, int victim = 20, ulong revision = 5, int hp = 80,
            double impact = 10d, bool north = false, bool hasPosition = true,
            float impactX = 1f, float impactZ = 2f)
        {
            WorldPointXZ impactPosition = default;
            if (hasPosition)
                Require(WorldPointXZ.TryCreate(impactX, impactZ, out impactPosition),
                    "Fixture impact position must be valid.");
            return new AttackResultPresentationInput(10, revision,
                new AttackResultKey(new AttackerInstanceId(instance), new AttackSequenceId(sequence),
                    hit, 1, victim, 0, ordinal), AttackDeliveryKind.Hitscan, impact,
                Direction(north), hasPosition, impactPosition,
                AttackImpactOutcome.HitApplied, 20, hp,
                (int)UnitType.SpearMan, (int)TeamId.Red);
        }

        private sealed class MutablePoseProvider : IPresentationPoseProvider
        {
            private readonly Dictionary<int, Transform> _units = new();

            public void SetUnit(int unitId, Transform value) => _units[unitId] = value;
            public Transform GetUnitTransform(int unitId)
                => _units.TryGetValue(unitId, out Transform value) ? value : null;
            public Transform GetBuildingTransform(int buildingId) => null;
            public Vector3 GetUnitPosition(int unitId)
            {
                Transform value = GetUnitTransform(unitId);
                return value != null ? value.position : Vector3.zero;
            }
            public Vector3 GetBuildingPosition(int buildingId) => Vector3.zero;
        }

        private static ActionDirectionXZ Direction(bool north)
        {
            if (!ActionDirectionXZ.TryCreate(north ? 0d : 1d, north ? 1d : 0d, out var direction))
                throw new InvalidOperationException("Invalid direction fixture.");
            return direction;
        }

        private static void Is(AuthoritativePresentationStatus actual, AuthoritativePresentationStatus expected,
            string message) => Require(actual == expected, message + " actual=" + actual + ", expected=" + expected);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[UAS-AUTH-PRESENT] self-validation FAIL: " + message);
        }
    }
}
