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

        private static void ValidateProductionAdapter()
        {
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
                // 동기 clock은 anchorServer + (local - anchorLocal)로 재구성된다. 정확히
                // 10.1을 넣으면 이진 부동소수점에서 due보다 극미세하게 작아질 수 있으므로,
                // 허용 오차 변경 없이 실제 다음 프레임에 해당하는 1ms 뒤를 사용한다.
                UnitAttackResultPresentationShadowBridge.TickCoordinator(10.101d);
                Require(queue.AuthoritativeEmits == 1 && victim.Hp == 100,
                    "Production adapter must emit the confirmed result once after retire without writing HP. "
                    + $"queueEmits={queue.AuthoritativeEmits}, bridgeEmits="
                    + $"{UnitAttackResultPresentationShadowBridge.PresentationEmits}, "
                    + $"vfxCalls={actualVfxCalls}, victimHp={victim.Hp}.");
                GameEvents.OnLocalAttackHit.OnNext(new LocalAttackHitPresentationEvent(10, Scope()));
                UnitAttackResultPresentationShadowBridge.TickCoordinator(10.9d);
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
                UnitAttackResultPresentationShadowBridge.TickCoordinator(11.101d);
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
                UnitAttackResultPresentationShadowBridge.TickCoordinator(12.101d);
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
                UnitAttackResultPresentationShadowBridge.TickCoordinator(13.101d);
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
