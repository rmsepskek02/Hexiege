using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// Tracer A의 순수 C# 계약을 Unity Editor에서 즉시 점검하는 도구다.
    /// 씬, 프리팹, Animator, NGO 실행 환경 없이도 행동 단계 순서, 공격 회차 증가,
    /// 각도 히스테리시스, 결과 중복 제거와 역순 정렬이 계약대로 동작하는지 확인한다.
    /// 메뉴에서 Hexiege/Combat/Run Unit Action Self Validation을 선택해 실행한다.
    /// </summary>
    public static class RunUnitActionSelfValidation
    {
        [MenuItem("Hexiege/Combat/Run Unit Action Self Validation")]
        public static void Run()
        {
            ValidatePhaseVocabularyAndOrdinals();
            ValidateMonotonicSequences();
            ValidateMovementHysteresis();
            ValidateB2MovementReducer();
            ValidateB3PreparedMovementTransition();
            ValidateB2MovementEndpointAdapter();
            ValidateB2MovementManifestChunking();
            ValidateB3MovementManifestPreflight();
            ValidateB3ClientReplicationClassification();
            ValidateB3MovementPipelineModeLatch();
            ValidateB3RotationWriterOwnership();
            ValidateB3PathStartArrivalCommit();
            ValidateB3SpatialTransitionPolicy();
            ValidateB3SpatialTransitionApplicationPreflight();
            ValidateB3CorridorSamplePolicy();
            ValidateB3CorridorTerminalEvidence();
            ValidateB3TrajectoryCorridorResolver();
            ValidateB3ContinuousLocomotionPlanner();
            ValidateB3FiniteRepathGuard();
            ValidateAttackHysteresis();
            ValidateDuplicateAndReorderedResults();
            ValidateA1ContractsAndReducer();

            Debug.Log("[UAS-DIAG] self-validation PASS: A1 contracts/reducer, A2 pure pose sample, B2 movement reducer command/segment scope and 10/15 hysteresis, endpoint NoIntent normalization/lifecycle, target-acquire priority, duplicate/stale/invalid fail-closed, Android-safe lossless coverage manifest chunks and reserved-terminal preflight, B3 match-fixed movement pipeline mode/rollback, publish-staged reducer prepare/commit atomicity, route checkpoint/spatial Root transition separation, trajectory and non-trajectory reducer-facing synchronization, allocation-stable corridor validation seam, gameplay-gated movement-to-action rotation ownership, retry-stable deferred movement scope commit, arrival-committed adjacent path-start transition, path[0] start-tile egress confinement, bounded terminal corridor Root/Domain/UnitData/path/sample evidence, typed v9 candidate probe/final-stage semantics with staged-versus-commit accounting, recoverable-history retention, reduced/stationary/repath fallback and fatal separation, no same-frame retry, continuous 60-degree trajectory/150-degree reverse/held-progress invariants, objective-scoped finite repath frame/no-progress/environment-reset and duplicate-command suppression, shared production adapter, reducer-direction rotation source, attack-range NoIntent lifecycle, client replication identity/scope monotonic classification and lifecycle retire/reuse including recoverable observer history, phase vocabulary/ordinal, 5/8 attack, cancel/dead, multi-hit/result confirmation, bounded dedupe/reorder, v2 range divergence.");
        }

        private static void ValidateB3SpatialTransitionPolicy()
        {
            var start = new HexCoord(0, 0);
            var first = new HexCoord(1, 0);
            var second = new HexCoord(2, 0);
            var terminal = new HexCoord(3, 0);
            var route = new[] { start, first, second, terminal };
            var output = new HexCoord[4];

            UnitSpatialTransitionPlanStatus status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, start, first, first, second },
                5,
                route,
                false,
                default,
                output,
                out int count);
            Require(status == UnitSpatialTransitionPlanStatus.Ready
                    && count == 2
                    && output[0] == first
                    && output[1] == second,
                "Duplicate Root samples must compact into ordered adjacent transitions.");

            var checkpoint = new UnitPathCheckpointTracker();
            Require(checkpoint.TryConsume(1, true),
                "The route checkpoint fixture must consume its first waypoint.");
            status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, start },
                2,
                route,
                false,
                default,
                output,
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.NoTransition && count == 0,
                "A consumed route checkpoint must not create a spatial transition before Root crosses a boundary.");

            status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, first, terminal },
                3,
                route,
                false,
                default,
                output,
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.NonAdjacentSample && count == 0,
                "A skipped sampled tile must fail closed without partial output.");

            status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, first, second, terminal },
                4,
                route,
                true,
                terminal,
                output,
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.TerminalContact
                    && count == 2
                    && output[0] == first
                    && output[1] == second,
                "A non-walkable logical final tile must remain terminal contact, not an occupied tile.");

            status = UnitSpatialTransitionPolicy.TryBuild(
                first,
                new[] { first, start },
                2,
                route,
                false,
                default,
                output,
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.OutsideLogicalPath && count == 0,
                "A backward A-star spatial transition must fail closed.");

            status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, first, second },
                3,
                route,
                false,
                default,
                new HexCoord[1],
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.InsufficientCapacity && count == 0,
                "An undersized caller buffer must fail closed without partial output.");
        }

        private static void ValidateB3PreparedMovementTransition()
        {
            Require(ActionDirectionXZ.TryCreate(
                    0d, 1d, out ActionDirectionXZ forward),
                "Prepared movement fixture direction must be valid.");
            var reducer = new UnitMovementReducer();
            UnitMovementSnapshot before = reducer.Snapshot;

            UnitMovementReducerStatus preparedStatus = reducer.Prepare(
                before.Revision,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                true,
                forward,
                forward,
                false,
                1d,
                out UnitMovementPreparedTransition prepared);
            Require(preparedStatus == UnitMovementReducerStatus.Accepted
                    && prepared.IsPrepared
                    && reducer.Snapshot.Revision == before.Revision
                    && !reducer.Snapshot.HasAcceptedObservation,
                "Prepare must produce an accepted transition without mutating the reducer.");

            UnitMovementReducerStatus failedStatus = reducer.Prepare(
                before.Revision,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                true,
                forward,
                forward,
                false,
                double.NaN,
                out UnitMovementPreparedTransition failed);
            Require(failedStatus == UnitMovementReducerStatus.InvalidTime
                    && !failed.IsPrepared
                    && reducer.Snapshot.Revision == before.Revision
                    && !reducer.Snapshot.HasAcceptedObservation,
                "Failed and abandoned prepares must leave the reducer unchanged.");

            Require(reducer.CanCommitPrepared(prepared)
                    && reducer.TryCommitPrepared(prepared)
                    && reducer.Snapshot.Revision == before.Revision + 1UL
                    && reducer.Snapshot.HasAcceptedObservation
                    && !reducer.TryCommitPrepared(prepared),
                "A prepared transition must commit exactly once.");

            UnitMovementSnapshot beforeDuplicate = reducer.Snapshot;
            UnitMovementReducerStatus duplicateStatus = reducer.Prepare(
                beforeDuplicate.Revision,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                true,
                forward,
                forward,
                false,
                1d,
                out UnitMovementPreparedTransition duplicate);
            Require(duplicateStatus == UnitMovementReducerStatus.Duplicate
                    && reducer.CanCommitPrepared(duplicate)
                    && reducer.TryCommitPrepared(duplicate)
                    && reducer.Snapshot.Revision == beforeDuplicate.Revision,
                "A duplicate prepared transition must commit as a no-op.");
        }

        private static void ValidateB3SpatialTransitionApplicationPreflight()
        {
            var start = new HexCoord(0, 0);
            var first = new HexCoord(1, 0);
            var second = new HexCoord(2, 0);
            var adjacent = new[] { first, second };

            Require(UnitMovementUseCase.IsValidSpatialTransitionChain(
                    start, adjacent, adjacent.Length),
                "Application spatial preflight must accept an ordered adjacent chain.");
            Require(UnitMovementUseCase.IsValidSpatialTransitionChain(
                    start, adjacent, 0),
                "Application spatial preflight must accept a zero-transition no-op.");
            Require(!UnitMovementUseCase.IsValidSpatialTransitionChain(
                    start, new[] { second }, 1),
                "Application spatial preflight must reject a chain that skips the first boundary.");
            Require(!UnitMovementUseCase.IsValidSpatialTransitionChain(
                    start, adjacent, adjacent.Length + 1),
                "Application spatial preflight must reject an out-of-bounds caller count.");

            UnitSpatialPreflightResult missing =
                UnitMovementUseCase.ClassifySpatialTile(
                    tileExists: false,
                    tileWalkable: false,
                    second,
                    transitionIndex: 1);
            UnitSpatialPreflightResult nonWalkable =
                UnitMovementUseCase.ClassifySpatialTile(
                    tileExists: true,
                    tileWalkable: false,
                    first,
                    transitionIndex: 0);
            var invalidChain = new UnitSpatialPreflightResult(
                UnitSpatialPreflightStatus.InvalidChain, second, 0);
            Require(missing.IsRecoverable
                    && missing.OffendingTile == second
                    && missing.OffendingTransitionIndex == 1
                    && nonWalkable.IsRecoverable
                    && nonWalkable.OffendingTile == first
                    && invalidChain.IsFatal,
                "Missing/non-walkable tiles must retain typed recoverable evidence while invalid chains remain fatal.");
            Require(UnitSpatialCandidatePolicy.Classify(
                        UnitSpatialTransitionPlanStatus.OutsideLogicalPath,
                        UnitSpatialPreflightResult.Success())
                    == UnitSpatialCandidateVerdict.CandidateUnsafe
                    && UnitSpatialCandidatePolicy.Classify(
                        UnitSpatialTransitionPlanStatus.Ready,
                        invalidChain)
                    == UnitSpatialCandidateVerdict.Fatal,
                "OutsideLogicalPath must enter resolver fallback/repath while fatal Application contracts reject.");

            int heldPlanned = UnitSpatialDiagnosticAccountingPolicy
                .PlannedDeltaAtCandidateStage(transitionCount: 2);
            int successfulPlanned = UnitSpatialDiagnosticAccountingPolicy
                .PlannedDeltaAtCommitAttempt(transitionCount: 2);
            int successfulCommitted = UnitSpatialDiagnosticAccountingPolicy
                .CommittedDeltaAtCommitResult(transitionCount: 2, succeeded: true);
            int failedPlanned = UnitSpatialDiagnosticAccountingPolicy
                .PlannedDeltaAtCommitAttempt(transitionCount: 1);
            int failedCommitted = UnitSpatialDiagnosticAccountingPolicy
                .CommittedDeltaAtCommitResult(transitionCount: 1, succeeded: false);
            Require(heldPlanned == 0
                    && successfulPlanned == 2
                    && successfulCommitted == 2
                    && failedPlanned == 1
                    && failedCommitted == 0
                    && UnitSpatialDiagnosticAccountingPolicy.CommitFailureDelta(false) == 1,
                "Accepted staged-but-Held candidates must not count as planned; only actual commit attempts may create planned/committed divergence.");
            Require(!UnitSpatialDiagnosticAccountingPolicy
                        .ClearsRecoverableHistory(transitionCount: 0, succeeded: true)
                    && !UnitSpatialDiagnosticAccountingPolicy
                        .ClearsRecoverableHistory(transitionCount: 2, succeeded: false)
                    && UnitSpatialDiagnosticAccountingPolicy
                        .ClearsRecoverableHistory(transitionCount: 2, succeeded: true),
                "No-transition Accepted stages between repeated failures must retain recoverable history until a real spatial commit succeeds.");
        }

        private static void ValidateB3PathStartArrivalCommit()
        {
            var logicalStart = new HexCoord(0, 0);
            var stagedStart = new HexCoord(1, 0);
            var finalTarget = new HexCoord(2, 0);
            var route = new List<HexCoord> { stagedStart, finalTarget };

            Require(UnitPathStartTransitionPolicy.TryBuildArrivalCommittedPath(
                        logicalStart,
                        stagedStart,
                        route,
                        out List<HexCoord> path),
                "An adjacent forward start must produce an arrival-committed transition path.");
            Require(path.Count == 3
                    && path[0] == logicalStart
                    && path[1] == stagedStart
                    && path[2] == finalTarget,
                "The logical start must remain path[0] until the staged tile is actually reached.");
            Require(!UnitPathStartTransitionPolicy.TryBuildArrivalCommittedPath(
                        logicalStart,
                        finalTarget,
                        new List<HexCoord> { finalTarget },
                        out _),
                "A non-adjacent staged start must fail closed instead of creating a logical jump.");
            Require(!UnitPathStartTransitionPolicy.TryBuildArrivalCommittedPath(
                        logicalStart,
                        stagedStart,
                        new List<HexCoord> { finalTarget },
                        out _),
                "A route that does not begin at the staged start must fail closed.");
        }

        private static void ValidateB3CorridorSamplePolicy()
        {
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 1,
                        matchedPathIndex: 0,
                        isWalkable: false)
                    == UnitTrajectoryCorridorSampleResult.StartTileEgress,
                "A non-walkable path[0] must be a legal origin only while leaving the first segment.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 1,
                        matchedPathIndex: 1,
                        isWalkable: false)
                    == UnitTrajectoryCorridorSampleResult.Blocked,
                "A non-walkable first waypoint must remain blocked.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 2,
                        matchedPathIndex: 0,
                        isWalkable: false)
                    == UnitTrajectoryCorridorSampleResult.OutsideCorridor,
                "The start-tile exception must not permit re-entry after the first segment.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 1,
                        matchedPathIndex: -1,
                        isWalkable: true)
                    == UnitTrajectoryCorridorSampleResult.OutsideCorridor,
                "A walkable tile outside the logical corridor must remain rejected.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 2,
                        matchedPathIndex: 2,
                        isWalkable: true)
                    == UnitTrajectoryCorridorSampleResult.Walkable,
                "A normal walkable corridor sample must remain accepted.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 3,
                        matchedPathIndex: 3,
                        isWalkable: false)
                    == UnitTrajectoryCorridorSampleResult.FinalDestination,
                "The existing non-walkable final destination contract must remain accepted.");
        }

        private static void ValidateB3CorridorTerminalEvidence()
        {
            const BindingFlags flags = BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Static
                | BindingFlags.Instance;
            System.Type viewType = typeof(Hexiege.Presentation.UnitView);
            System.Type evidenceType = viewType.GetNestedType(
                "CorridorRepathEvidence", BindingFlags.NonPublic);
            MethodInfo formatter = viewType.GetMethod(
                "FormatCorridorRepathEvidence",
                BindingFlags.NonPublic | BindingFlags.Static);
            Require(evidenceType != null && formatter != null,
                "The bounded corridor terminal evidence formatter must remain connected to UnitView.");

            object emptyEvidence = System.Activator.CreateInstance(evidenceType);
            string empty = formatter.Invoke(null, new[] { emptyEvidence }) as string;
            Require(empty == "unavailable",
                "Missing corridor terminal evidence must fail closed as unavailable.");

            object evidence = System.Activator.CreateInstance(evidenceType);
            evidenceType.GetField("IsAvailable", flags)?.SetValue(evidence, true);
            evidenceType.GetField("Frame", flags)?.SetValue(evidence, 17);
            evidenceType.GetField("PathCount", flags)?.SetValue(evidence, 3);
            evidenceType.GetField("WaypointIndex", flags)?.SetValue(evidence, 1);
            evidenceType.GetField("MatchedPathIndex", flags)?.SetValue(evidence, -1);
            evidenceType.GetField("FirstAllowedPathIndex", flags)?.SetValue(evidence, 0);
            evidenceType.GetField("LastAllowedPathIndex", flags)?.SetValue(evidence, 2);
            evidenceType.GetField("SampleResult", flags)?.SetValue(
                evidence, UnitTrajectoryCorridorSampleResult.OutsideCorridor);
            string formatted = formatter.Invoke(null, new[] { evidence }) as string;
            Require(!string.IsNullOrEmpty(formatted)
                    && formatted.Contains("frame:17|")
                    && formatted.Contains("rootView:")
                    && formatted.Contains("rootDomain:")
                    && formatted.Contains("rootHex:")
                    && formatted.Contains("unitDataPosition:")
                    && formatted.Contains("sourcePathStart:")
                    && formatted.Contains("sourceWaypointIndex:1/2|")
                    && formatted.Contains("sampleHex:")
                    && formatted.Contains("matchedPathIndex:-1|")
                    && formatted.Contains("allowedPathIndices:0-2|")
                    && formatted.EndsWith("sampleResult:OutsideCorridor"),
                "Corridor terminal evidence must remain a lossless single-line Root/Domain/UnitData/path/sample record.");
        }

        private static void ValidateB3TrajectoryCorridorResolver()
        {
            WorldPointXZ current = default;
            ActionDirectionXZ facing = default;
            WorldPointXZ waypoint = default;
            WorldPointXZ next = default;
            Require(WorldPointXZ.TryCreate(0d, 0d, out current),
                "Corridor resolver current point fixture must be valid.");
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out facing),
                "Corridor resolver facing fixture must be valid.");
            Require(WorldPointXZ.TryCreate(10d, 0d, out waypoint),
                "Corridor resolver waypoint fixture must be valid.");
            Require(WorldPointXZ.TryCreate(10d, 10d, out next),
                "Corridor resolver next waypoint fixture must be valid.");

            UnitTrajectoryCorridorResolution reduced =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 90d,
                    cornerLookAheadDistanceWorld: 20d,
                    step => step.CandidatePosition.X <= 0.250001d,
                    out UnitTrajectoryStep reducedStep);
            Require(reduced == UnitTrajectoryCorridorResolution.ReducedSmooth
                    && reducedStep.IsValid
                    && reducedStep.ConsumedDistanceWorld > 0d
                    && reducedStep.ConsumedDistanceWorld <= 0.250001d
                    && !reducedStep.RequiresStationaryAlignment,
                "A full corridor rejection must first shrink the smooth non-zero movement instead of requesting A* again.");

            UnitTrajectoryCorridorResolution alignment =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 20d,
                    step => step.ConsumedDistanceWorld
                        <= UnitMovementEvaluationAdapter.PositionTolerance,
                    out UnitTrajectoryStep alignmentStep);
            Require(alignment
                        == UnitTrajectoryCorridorResolution.StationaryAlignment
                    && alignmentStep.IsValid
                    && alignmentStep.RequiresStationaryAlignment
                    && alignmentStep.ConsumedDistanceWorld == 0d
                    && alignmentStep.CandidatePosition.Equals(current),
                "When every non-zero candidate leaves the corridor, the safe current pose must rotate in place instead of consuming repath budget.");

            UnitTrajectoryCorridorResolution blocked =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 20d,
                    step => false,
                    out UnitTrajectoryStep blockedStep);
            Require(blocked == UnitTrajectoryCorridorResolution.RepathRequired
                    && !blockedStep.IsValid,
                "A current pose outside the corridor must remain fail-closed and request a real A* repath.");

            int typedReducedCalls = 0;
            UnitTrajectoryCorridorResolution typedReduced =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 90d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(step =>
                    {
                        typedReducedCalls++;
                        return step.CandidatePosition.X <= 0.250001d
                            ? UnitSpatialCandidateVerdict.Accepted
                            : UnitSpatialCandidateVerdict.CandidateUnsafe;
                    }),
                    out UnitTrajectoryStep typedReducedStep,
                    out UnitSpatialCandidateVerdict typedReducedVerdict);
            Require(typedReduced == UnitTrajectoryCorridorResolution.ReducedSmooth
                    && typedReducedVerdict == UnitSpatialCandidateVerdict.Accepted
                    && typedReducedStep.ConsumedDistanceWorld > 0d
                    && typedReducedCalls > 1,
                "Typed OutsideLogicalPath candidates must remain side-effect-free probes until reduced movement is accepted.");

            int typedStationaryCalls = 0;
            UnitTrajectoryCorridorResolution typedStationary =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(step =>
                    {
                        typedStationaryCalls++;
                        return step.ConsumedDistanceWorld
                                <= UnitMovementEvaluationAdapter.PositionTolerance
                            ? UnitSpatialCandidateVerdict.Accepted
                            : UnitSpatialCandidateVerdict.CandidateUnsafe;
                    }),
                    out UnitTrajectoryStep typedStationaryStep,
                    out UnitSpatialCandidateVerdict typedStationaryVerdict);
            Require(typedStationary
                        == UnitTrajectoryCorridorResolution.StationaryAlignment
                    && typedStationaryVerdict == UnitSpatialCandidateVerdict.Accepted
                    && typedStationaryStep.RequiresStationaryAlignment
                    && typedStationaryCalls > 1,
                "Typed unsafe movement candidates must be allowed to fall back to a safe stationary alignment.");

            int typedUnsafeCalls = 0;
            UnitTrajectoryCorridorResolution typedUnsafe =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(_ =>
                    {
                        typedUnsafeCalls++;
                        return UnitSpatialCandidateVerdict.CandidateUnsafe;
                    }),
                    out UnitTrajectoryStep typedUnsafeStep,
                    out UnitSpatialCandidateVerdict typedUnsafeVerdict);
            Require(typedUnsafe == UnitTrajectoryCorridorResolution.RepathRequired
                    && typedUnsafeVerdict
                        == UnitSpatialCandidateVerdict.CandidateUnsafe
                    && !typedUnsafeStep.IsValid
                    && typedUnsafeCalls > 1,
                "OutsideLogicalPath candidates must exhaust bounded fallbacks and then request repath without staging a pose.");

            int routeInvalidatedCalls = 0;
            UnitTrajectoryCorridorResolution routeInvalidated =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 90d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(_ =>
                    {
                        routeInvalidatedCalls++;
                        return UnitSpatialCandidateVerdict.RouteInvalidated;
                    }),
                    out UnitTrajectoryStep routeInvalidatedStep,
                    out UnitSpatialCandidateVerdict routeInvalidatedVerdict);
            Require(routeInvalidated
                        == UnitTrajectoryCorridorResolution.RepathRequired
                    && routeInvalidatedVerdict
                        == UnitSpatialCandidateVerdict.RouteInvalidated
                    && !routeInvalidatedStep.IsValid
                    && routeInvalidatedCalls == 1,
                "Missing/non-walkable route evidence must request repath immediately without reduced or same-frame retry probes.");

            int fatalCalls = 0;
            UnitTrajectoryCorridorResolution fatal =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 90d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(_ =>
                    {
                        fatalCalls++;
                        return UnitSpatialCandidateVerdict.Fatal;
                    }),
                    out UnitTrajectoryStep fatalStep,
                    out UnitSpatialCandidateVerdict fatalVerdict);
            Require(fatal == UnitTrajectoryCorridorResolution.Invalid
                    && fatalVerdict == UnitSpatialCandidateVerdict.Fatal
                    && !fatalStep.IsValid
                    && fatalCalls == 1,
                "Fatal dependency/chain/capacity evidence must reject immediately and remain distinct from recoverable repath.");
        }

        private static void ValidateB3RotationWriterOwnership()
        {
            var ownership = new UnitRootRotationOwnership();
            Require(ownership.Owner == UnitRootRotationWriter.None
                    && ownership.TryAcquireMovement()
                    && ownership.MovementOwnsRoot
                    && !ownership.ActionOwnsRoot,
                "A navigation start must select Movement as the sole Simulation Root rotation writer.");

            Require(ownership.TransferToAction()
                    && ownership.ActionOwnsRoot
                    && !ownership.MovementOwnsRoot
                    && !ownership.TryAcquireMovement(),
                "Movement-to-action entry must atomically transfer ownership and reject movement reacquisition during combat.");

            ownership.ReleaseAction(resumeMovement: true);
            Require(ownership.MovementOwnsRoot
                    && !ownership.ActionOwnsRoot,
                "Action exit with a live navigation objective must return ownership to Movement.");

            Require(!UnitActionRotationEventPolicy.ShouldAcceptTargetEvent(
                        networkActive: true,
                        networkServer: true,
                        actionOwnsRoot: ownership.ActionOwnsRoot)
                    && UnitActionRotationEventPolicy.ShouldAcceptTargetEvent(
                        networkActive: true,
                        networkServer: false,
                        actionOwnsRoot: false)
                    && UnitActionRotationEventPolicy.ShouldAcceptTargetEvent(
                        networkActive: false,
                        networkServer: false,
                        actionOwnsRoot: false)
                    && !UnitActionRotationEventPolicy.ShouldAcceptStopEvent(
                        networkActive: true,
                        networkServer: true)
                    && UnitActionRotationEventPolicy.ShouldAcceptStopEvent(
                        networkActive: true,
                        networkServer: false)
                    && UnitActionRotationEventPolicy.ShouldAcceptStopEvent(
                        networkActive: false,
                        networkServer: false),
                "Delayed server target/stop events must not revive or release gameplay action rotation, while pure clients may retain presentation event handling.");

            Require(ownership.TransferToAction(),
                "A resumed movement writer must be transferable to a later action.");
            ownership.ReleaseAction(resumeMovement: false);
            Require(ownership.Owner == UnitRootRotationWriter.None,
                "Action exit without navigation must release Simulation Root rotation ownership.");

            Require(UnitMovementPresentationPolicy.ShouldHoldWalk(
                        decisionValid: true,
                        allowsMovement: false,
                        commitsAcquireCandidate: false)
                    && !UnitMovementPresentationPolicy.ShouldHoldWalk(
                        decisionValid: true,
                        allowsMovement: true,
                        commitsAcquireCandidate: false)
                    && !UnitMovementPresentationPolicy.ShouldHoldWalk(
                        decisionValid: true,
                        allowsMovement: false,
                        commitsAcquireCandidate: true)
                    && !UnitMovementPresentationPolicy.ShouldHoldWalk(
                        decisionValid: false,
                        allowsMovement: false,
                        commitsAcquireCandidate: false),
                "Walk must stop for stationary alignment/endpoint, resume for displacement, and yield directly to an acquired action.");

            Require(UnitMovementScopePolicy.TryPrepare(
                        currentCommandRevision: 1UL,
                        currentSegmentRevision: 7UL,
                        pendingCommand: false,
                        pendingSegment: true,
                        out ulong preparedCommand,
                        out ulong preparedSegment)
                    && preparedCommand == 1UL
                    && preparedSegment == 8UL
                    && UnitMovementScopePolicy.TryPrepare(
                        currentCommandRevision: 1UL,
                        currentSegmentRevision: 7UL,
                        pendingCommand: false,
                        pendingSegment: true,
                        out ulong retriedCommand,
                        out ulong retriedSegment)
                    && retriedCommand == preparedCommand
                    && retriedSegment == preparedSegment,
                "A planner failure before reducer evaluation must leave the pending segment retry on the same next revision.");

            Require(UnitMovementScopePolicy.TryPrepare(
                        currentCommandRevision: 0UL,
                        currentSegmentRevision: 0UL,
                        pendingCommand: true,
                        pendingSegment: false,
                        out ulong firstCommand,
                        out ulong firstSegment)
                    && firstCommand == 1UL
                    && firstSegment == 1UL,
                "A unit with no accepted movement observation must always present command/segment 1/1 to its first reducer evaluation.");
        }

        private static void ValidateB3FiniteRepathGuard()
        {
            var pathA = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(1, 0),
                new HexCoord(2, 0)
            };
            var pathAClone = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(1, 0),
                new HexCoord(2, 0)
            };
            var pathB = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(1, -1),
                new HexCoord(2, -1)
            };
            var pathC = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(0, 1),
                new HexCoord(1, 1)
            };
            var invalidPath = new List<HexCoord> { new HexCoord(0, 0) };

            Require(UnitPathSignature.TryCreate(pathA, out UnitPathSignature signatureA)
                && UnitPathSignature.TryCreate(pathAClone, out UnitPathSignature signatureAClone)
                && signatureA == signatureAClone,
                "Equal ordered HexCoord paths must produce the same deterministic signature.");

            var guard = new UnitRepathProgressGuard(pathA);
            Require(guard.Evaluate(10, pathAClone)
                    == UnitRepathDecision.AcceptedNextFrame,
                "A repeated path returned once must defer to the next frame instead of terminating the navigation objective.");
            Require(guard.Evaluate(10, pathB)
                    == UnitRepathDecision.RejectedFrameBudget,
                "A repeated-path defer must consume the frame budget so another repath cannot run synchronously.");
            Require(guard.AcceptedInFrame == 1
                    && guard.AcceptedWithoutProgress == 1,
                "A repeated-path defer must consume one bounded no-progress observation.");
            Require(guard.Evaluate(11, pathB)
                    == UnitRepathDecision.AcceptedNextFrame,
                "A distinct path must be accepted after the frame boundary.");
            Require(guard.Evaluate(12, pathA)
                    == UnitRepathDecision.AcceptedNextFrame,
                "An A-to-B-to-A observation must remain bounded without immediately terminating the objective.");
            Require(guard.Evaluate(11, invalidPath)
                    == UnitRepathDecision.RejectedInvalidPath,
                "An invalid path must fail closed.");
            Require(guard.Evaluate(13, pathC)
                    == UnitRepathDecision.AcceptedNextFrame,
                "A distinct path must be accepted after the frame boundary.");
            Require(guard.ObserveProgress(pathC)
                && guard.AcceptedInFrame == 1
                && guard.AcceptedWithoutProgress == 0
                && guard.Evaluate(14, pathA)
                    == UnitRepathDecision.AcceptedNextFrame,
                "Committed progress must reset cycle history without reopening the same-frame budget.");

            var boundedGuard = new UnitRepathProgressGuard(pathA);
            for (int attempt = 0;
                attempt < UnitRepathProgressGuard.MaximumAcceptedWithoutProgress;
                attempt++)
            {
                var uniquePath = new List<HexCoord>
                {
                    new HexCoord(0, 0),
                    new HexCoord(10 + attempt, -attempt),
                    new HexCoord(20 + attempt, -attempt)
                };
                Require(boundedGuard.Evaluate(100 + attempt, uniquePath)
                        == UnitRepathDecision.AcceptedNextFrame,
                    "Distinct paths must remain bounded while allowing finite recovery attempts.");
            }

            var overflowPath = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(99, -99),
                new HexCoord(100, -99)
            };
            Require(boundedGuard.Evaluate(200, overflowPath)
                    == UnitRepathDecision.RejectedNoProgressBudget,
                "Distinct repaths without committed progress must eventually fail closed.");

            var objective = new UnitNavigationObjective(
                new HexCoord(5, 0), pathA, 3UL);
            Require(objective.IsActive
                    && objective.State == UnitNavigationObjectiveState.Navigating
                    && objective.SuppressesDuplicateCommand(
                        new HexCoord(5, 0), 3UL)
                    && !objective.SuppressesDuplicateCommand(
                        new HexCoord(6, 0), 3UL),
                "An active objective must suppress only the same destination in the same environment.");
            objective.MarkWaitingRepath();
            objective.MarkBlocked();
            Require(objective.State == UnitNavigationObjectiveState.Blocked
                    && objective.SuppressesDuplicateCommand(
                        new HexCoord(5, 0), 3UL),
                "A blocked objective must remain active so an external ticker cannot reset its budget.");
            Require(objective.NotifyEnvironmentChanged(4UL)
                    && objective.State == UnitNavigationObjectiveState.WaitingRepath
                    && objective.RepathGuard.AcceptedWithoutProgress == 0
                    && !objective.SuppressesDuplicateCommand(
                        new HexCoord(5, 0), 5UL),
                "A newer walkability revision must reopen the same objective without accepting future revisions.");
            objective.MarkNavigating();
            Require(objective.ObserveProgress(pathB)
                    && objective.State == UnitNavigationObjectiveState.Navigating,
                "Committed movement must keep the objective active and clear no-progress history.");
            objective.Complete();
            Require(!objective.IsActive
                    && objective.State == UnitNavigationObjectiveState.Completed
                    && !objective.SuppressesDuplicateCommand(
                        new HexCoord(5, 0), 4UL),
                "A completed objective must allow a future command to create a new lifecycle.");
        }

        private static void ValidateB3ContinuousLocomotionPlanner()
        {
            // Keep every fixture definitely assigned even when an earlier validation short-circuits.
            // This does not relax the fixture gate: Require still fails unless every TryCreate succeeds.
            WorldPointXZ origin = default;
            WorldPointXZ corner = default;
            WorldPointXZ next = default;
            ActionDirectionXZ north = default;
            Require(WorldPointXZ.TryCreate(0d, 0d, out origin)
                && WorldPointXZ.TryCreate(0d, 1d, out corner)
                && WorldPointXZ.TryCreate(
                    Math.Sin(Math.PI / 3d),
                    1d + Math.Cos(Math.PI / 3d),
                    out next)
                && ActionDirectionXZ.TryCreate(
                    0d, 1d, out north),
                "Continuous locomotion validation inputs must be valid.");

            WorldPointXZ position = origin;
            ActionDirectionXZ facing = north;
            bool observedTurningMove = false;
            bool reachedCorner = false;
            for (int frame = 0; frame < 240 && !reachedCorner; frame++)
            {
                UnitTrajectoryStep step = default;
                Require(UnitServerTrajectoryPlanner.TryPlan(
                        position,
                        facing,
                        corner,
                        true,
                        next,
                        maximumTravelDistanceWorld: 0.01d,
                        maximumTurnDegrees: 4.5d,
                        cornerLookAheadDistanceWorld: 0.35d,
                        out step)
                    && step.IsValid
                    && !step.RequiresStationaryAlignment
                    && step.ConsumedDistanceWorld > 0d,
                    "A normal 60-degree corner must keep non-zero authoritative progress.");

                double movementX = step.CandidatePosition.X - position.X;
                double movementZ = step.CandidatePosition.Z - position.Z;
                ActionDirectionXZ velocity = default;
                Require(ActionDirectionXZ.TryCreate(
                        movementX, movementZ, out velocity)
                    && UnitActionPoseSample.GetYawDegrees(
                        velocity, step.TrajectoryDirection) <= 1d,
                    "Trajectory velocity and SimulationFacing must agree within one degree.");

                if (UnitActionPoseSample.GetYawDegrees(facing, step.TrajectoryDirection) > 0.01d)
                    observedTurningMove = true;
                position = step.CandidatePosition;
                facing = step.TrajectoryDirection;
                reachedCorner = step.ReachedCurrentWaypoint;
            }
            Require(reachedCorner && observedTurningMove,
                "A 60-degree path corner must turn while moving and consume the waypoint.");

            var checkpoints = new UnitPathCheckpointTracker();
            Require(!checkpoints.TryConsume(1, reachedWaypoint: false)
                && !checkpoints.TryConsume(2, reachedWaypoint: true)
                && checkpoints.TryConsume(1, reachedWaypoint: true)
                && !checkpoints.TryConsume(1, reachedWaypoint: true)
                && checkpoints.TryConsume(2, reachedWaypoint: true)
                && checkpoints.NextWaypointIndex == 3,
                "Waypoint checkpoint must reject early/skip/duplicate commits and consume each tile once.");

            WorldPointXZ reverseTarget = default;
            UnitTrajectoryStep reverse = default;
            Require(WorldPointXZ.TryCreate(0d, -1d, out reverseTarget)
                && UnitServerTrajectoryPlanner.TryPlan(
                    origin,
                    north,
                    reverseTarget,
                    false,
                    default,
                    maximumTravelDistanceWorld: 0.1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 0.35d,
                    out reverse)
                && reverse.IsValid
                && reverse.RequiresStationaryAlignment
                && reverse.ConsumedDistanceWorld == 0d
                && reverse.CandidatePosition.Equals(origin),
                "A 150-degree-or-greater reverse must hold position and consume zero progress.");
        }

        private static void ValidateB3ClientReplicationClassification()
        {
            Type observerType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                observerType = assemblies[index].GetType(
                    "Hexiege.Infrastructure.UnitMovementAuthorityObserver",
                    throwOnError: false);
                if (observerType != null)
                    break;
            }

            Require(observerType != null,
                "B3 movement authority observer type was not loaded.");
            MethodInfo classify = observerType.GetMethod(
                "ClassifyClientReplicatedStateForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo retire = observerType.GetMethod(
                "RetireUnitLifecycleForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo shouldObserve = observerType.GetMethod(
                "ShouldObserveClientReplicatedState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(classify != null && retire != null && shouldObserve != null,
                "B3 client replication classification seam is incomplete.");

            string Classify(
                bool hasPrevious,
                int previousUnitId,
                int previousPhase,
                ulong previousCommand,
                ulong previousSegment,
                ulong previousSemantic,
                int unitId,
                int phase,
                ulong command,
                ulong segment,
                ulong semantic)
                => (string)classify.Invoke(
                    null,
                    new object[]
                    {
                        hasPrevious, previousUnitId, previousPhase, previousCommand,
                        previousSegment, previousSemantic, unitId, phase,
                        command, segment, semantic
                    });

            int move = (int)UnitMovementPhase.Move;
            int align = (int)UnitMovementPhase.AlignToMove;
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    7, move, 5UL, 9UL, 6UL) == "None",
                "A coalesced first client snapshot must be accepted without requiring scope 1/1.");
            Require(Classify(true, 7, move, 1UL, 1UL, 4UL,
                    7, move, 1UL, 1UL, 4UL) == "Duplicate",
                "Same revision and same snapshot must be a valid duplicate.");
            Require(Classify(true, 7, move, 1UL, 1UL, 4UL,
                    7, align, 1UL, 1UL, 4UL)
                    .Contains("SameRevisionConflict"),
                "Same revision with a different snapshot must fail as conflict.");
            string identityConflict = Classify(
                true, 7, move, 1UL, 1UL, 4UL,
                8, move, 1UL, 1UL, 4UL);
            Require(identityConflict.Contains("IdentityConflict")
                && identityConflict.Contains("SameRevisionConflict"),
                "A unitId-only change in the same lifecycle must fail closed as identity conflict.");
            Require(Classify(true, 7, move, 1UL, 1UL, 4UL,
                    7, move, 1UL, 1UL, 3UL)
                    .Contains("RevisionRegression"),
                "A decreased semantic revision must fail as regression.");
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    7, move, 1UL, 1UL, 0UL)
                    .Contains("RevisionZero"),
                "A zero semantic revision must have its own failure cause.");
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    7, (int)UnitMovementPhase.Invalid, 1UL, 1UL, 1UL)
                    .Contains("InvalidPhase"),
                "An invalid phase must have its own failure cause.");
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    -1, move, 1UL, 1UL, 1UL)
                    .Contains("UnitUninitialized"),
                "An uninitialized unit must have its own failure cause.");
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    7, move, 0UL, 1UL, 1UL)
                    .Contains("InvalidScope"),
                "A zero command/segment scope must fail closed.");
            Require(Classify(true, 7, move, 3UL, 5UL, 7UL,
                    7, move, 2UL, 99UL, 8UL)
                    .Contains("ScopeRegression"),
                "A decreased command scope must fail as scope regression.");
            Require(Classify(true, 7, move, 3UL, 5UL, 7UL,
                    7, move, 3UL, 4UL, 8UL)
                    .Contains("ScopeRegression"),
                "A decreased segment in the same command must fail as scope regression.");
            Require(Classify(true, 7, move, 3UL, 5UL, 7UL,
                    7, move, 9UL, 27UL, 12UL) == "None",
                "Coalesced command/segment jumps must remain valid when scope is monotonic.");
            Require((string)retire.Invoke(null, null) == "PASS",
                "Retire must remove matching/mismatched completed lifecycle baselines and permit object-id reuse.");
            Require(!(bool)shouldObserve.Invoke(
                    null, new object[] { -1, 9UL })
                && (bool)shouldObserve.Invoke(
                    null, new object[] { 7, 9UL }),
                "A coalesced snapshot must defer while unitId is uninitialized and intake after registration.");
        }

        private static void ValidateB3MovementPipelineModeLatch()
        {
            var host = new UnitMovementPipelineModeLatch();
            var client = new UnitMovementPipelineModeLatch();

            UnitMovementPipelineMode replicatedServerMode =
                UnitMovementPipelineMode.ReducerAuthoritative;
            Require(host.TryBeginMatch(replicatedServerMode)
                && client.TryBeginMatch(replicatedServerMode)
                && host.ActiveMode == UnitMovementPipelineMode.ReducerAuthoritative
                && client.ActiveMode == host.ActiveMode,
                "Host/client must latch the same server-published movement mode.");
            Require(host.TryBeginMatch(UnitMovementPipelineMode.ReducerAuthoritative)
                && !host.TryBeginMatch(UnitMovementPipelineMode.Legacy),
                "Movement mode must reject mid-match mutation and mixed writers.");

            host.EndMatch();
            client.EndMatch();
            Require(host.TryBeginMatch(UnitMovementPipelineMode.Legacy)
                && client.TryBeginMatch(UnitMovementPipelineMode.Legacy),
                "Rollback must select Legacy for the next complete match only.");

            var invalid = new UnitMovementPipelineModeLatch();
            Require(!invalid.TryBeginMatch((UnitMovementPipelineMode)99)
                && !invalid.IsMatchActive,
                "Unknown movement mode must reject match start without silent fallback.");

            var reducer = new UnitMovementReducer();
            UnitMovementEvaluation align = UnitMovementEvaluationAdapter.Evaluate(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                0d,
                0d,
                0d,
                1d,
                1d,
                0d,
                false,
                0.1d,
                1d);
            Require(align.ReducerInvoked
                && align.Status == UnitMovementReducerStatus.Accepted
                && align.Decision.Phase == UnitMovementPhase.AlignToMove
                && !align.Decision.AllowsMovement,
                "Shared production adapter must stop position while aligning.");

            double nineDegrees = 9d * (Math.PI / 180d);
            UnitMovementEvaluation move = UnitMovementEvaluationAdapter.Evaluate(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                0d,
                0d,
                Math.Sin(nineDegrees),
                Math.Cos(nineDegrees),
                0d,
                1d,
                false,
                0.1d,
                2d);
            Require(move.Status == UnitMovementReducerStatus.Accepted
                && move.Decision.Phase == UnitMovementPhase.Move
                && move.Decision.AllowsMovement,
                "Shared production adapter must release movement at the 10-degree gate.");

            UnitMovementEvaluation acquireStop = UnitMovementEvaluationAdapter.Evaluate(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.Chase,
                0d,
                0d,
                0d,
                1d,
                0d,
                2d,
                true,
                0d,
                3d);
            Require(acquireStop.Status == UnitMovementReducerStatus.Accepted
                && acquireStop.Decision.Phase == UnitMovementPhase.NoIntent
                && acquireStop.Decision.IsTargetAcquirePriority
                && !acquireStop.Decision.AllowsMovement,
                "Attack-range acquisition must publish NoIntent instead of stale Move/Align.");
            Require(Math.Abs(move.DesiredMoveDirection.X) < 0.000001d
                && Math.Abs(move.DesiredMoveDirection.Z - 1d) < 0.000001d,
                "Authoritative rotation source must be the reducer-approved desired direction.");
        }

        private static void ValidateB3MovementManifestPreflight()
        {
            IReadOnlyList<string> chunks;
            Require(UnitMovementManifestChunker.TryBuild(
                    new[] { "12345", "67890" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 2,
                    reservedTerminalLines: 4,
                    nonManifestTerminalLines: 2,
                    out chunks)
                && chunks.Count == 2,
                "B3 manifest must accept the exact chunk/terminal boundary.");

            Require(!UnitMovementManifestChunker.TryBuild(
                    new[] { "12345", "67890" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 1,
                    reservedTerminalLines: 4,
                    nonManifestTerminalLines: 2,
                    out chunks),
                "B3 manifest must reject a chunk-count overflow.");
            Require(!UnitMovementManifestChunker.TryBuild(
                    new[] { "12345", "67890" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 2,
                    reservedTerminalLines: 3,
                    nonManifestTerminalLines: 2,
                    out chunks),
                "B3 manifest must reserve summary and END terminal lines.");
            Require(!UnitMovementManifestChunker.TryBuild(
                    new[] { "123456" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 2,
                    reservedTerminalLines: 4,
                    nonManifestTerminalLines: 2,
                    out chunks),
                "B3 manifest must fail closed for an oversized entry.");
            Require(!UnitMovementManifestChunker.TryBuild(
                    new[] { "가나" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 2,
                    reservedTerminalLines: 4,
                    nonManifestTerminalLines: 2,
                    out chunks),
                "B3 manifest limits must be measured in UTF-8 bytes.");
        }

        /// <summary>
        /// 상태머신의 모든 전이를 검증하는 테스트가 아니다.
        /// Tracer A가 합의한 phase 이름과 직렬화 순번만 바뀌지 않았는지 확인한다.
        /// </summary>
        private static void ValidatePhaseVocabularyAndOrdinals()
        {
            UnitActionPhase[] expected =
            {
                UnitActionPhase.Idle,
                UnitActionPhase.Navigate,
                UnitActionPhase.AlignToMove,
                UnitActionPhase.Move,
                UnitActionPhase.AcquireTarget,
                UnitActionPhase.Chase,
                UnitActionPhase.AlignToAttack,
                UnitActionPhase.Windup,
                UnitActionPhase.Impact,
                UnitActionPhase.Recovery,
                UnitActionPhase.Dead
            };

            Require(Enum.GetValues(typeof(UnitActionPhase)).Length == expected.Length,
                "UnitActionPhase member count changed unexpectedly.");

            for (int index = 0; index < expected.Length; index++)
            {
                Require((int)expected[index] == index,
                    $"UnitActionPhase ordering changed at index {index}.");
            }
        }

        private static void ValidateMonotonicSequences()
        {
            var allocator = new AttackSequenceAllocator();
            var firstInstance = new AttackerInstanceId(7UL);
            var secondInstance = new AttackerInstanceId(9UL);
            AttackSequenceId attackerSevenFirst = allocator.Next(firstInstance);
            AttackSequenceId attackerSevenSecond = allocator.Next(firstInstance);
            AttackSequenceId attackerNineFirst = allocator.Next(secondInstance);

            Require(attackerSevenFirst.IsValid, "First sequence must be valid.");
            Require(attackerSevenSecond.CompareTo(attackerSevenFirst) > 0,
                "Sequence must increase for the same attacker.");
            Require(attackerNineFirst.Value == 1UL,
                "Sequence counters must be independent per attacker.");
        }

        private static void ValidateMovementHysteresis()
        {
            Require(UnitActionAngleHysteresis.AllowsMovement(10d, false),
                "Movement must enter at 10 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsMovement(10.001d, false),
                "Movement must not enter above 10 degrees.");
            Require(UnitActionAngleHysteresis.AllowsMovement(15d, true),
                "Movement must remain active at 15 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsMovement(15.001d, true),
                "Movement must stop above 15 degrees.");
            Require(UnitActionAngleHysteresis.AllowsMovement(350d, false),
                "350 degrees must normalize to the 10-degree movement entry boundary.");
            Require(!UnitActionAngleHysteresis.AllowsMovement(double.PositiveInfinity, true),
                "Infinite movement error must fail closed.");
        }

        /// <summary>
        /// B2 이동 reducer가 Unity나 NGO 없이 명령·구간 회차, 10°/15° 경계,
        /// 타겟 획득 우선권과 잘못된 입력의 무변경 거부를 지키는지 검증한다.
        /// </summary>
        private static void ValidateB2MovementReducer()
        {
            Require(ActionDirectionXZ.TryCreate(0d, 1d, out ActionDirectionXZ forward),
                "B2 forward fixture must be valid.");
            ActionDirectionXZ yaw10 = CreateMovementDirection(10d);
            ActionDirectionXZ yaw10Over = CreateMovementDirection(10.001d);
            ActionDirectionXZ yaw12 = CreateMovementDirection(12d);
            ActionDirectionXZ yaw15 = CreateMovementDirection(15d);
            ActionDirectionXZ yaw15Over = CreateMovementDirection(15.001d);

            var entry = new UnitMovementReducer();
            Require(entry.Snapshot.Revision == 1UL
                && entry.Snapshot.Phase == UnitMovementPhase.NoIntent
                && !entry.Snapshot.HasAcceptedObservation,
                "B2 reducer must start at revision 1 with no accepted movement intent.");
            Require(entry.Evaluate(
                    entry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw10, false, 1d,
                    out UnitMovementDecision entryAtTen)
                == UnitMovementReducerStatus.Accepted,
                "B2 movement must enter exactly at 10 degrees.");
            Require(entryAtTen.IsValid
                && entryAtTen.Phase == UnitMovementPhase.Move
                && entryAtTen.AllowsMovement
                && entryAtTen.HasYawError
                && Math.Abs(entryAtTen.YawErrorDegrees - 10d) < 0.000001d,
                "The 10-degree B2 decision must be a valid Move.");

            UnitMovementSnapshot beforeDuplicate = entry.Snapshot;
            Require(entry.Evaluate(
                    beforeDuplicate.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw10, false, 1d,
                    out UnitMovementDecision duplicateDecision)
                == UnitMovementReducerStatus.Duplicate,
                "An exact same-time B2 observation must be classified as duplicate.");
            Require(duplicateDecision.IsValid
                && duplicateDecision.Phase == UnitMovementPhase.Move
                && entry.Snapshot.Revision == beforeDuplicate.Revision,
                "A duplicate must reproduce the current decision without mutation.");

            Require(entry.Evaluate(
                    entry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw15, false, 2d,
                    out UnitMovementDecision remainAtFifteen)
                == UnitMovementReducerStatus.Accepted
                && remainAtFifteen.Phase == UnitMovementPhase.Move,
                "An active B2 Move must remain active exactly at 15 degrees.");
            Require(entry.Evaluate(
                    entry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw15Over, false, 3d,
                    out UnitMovementDecision exitAboveFifteen)
                == UnitMovementReducerStatus.Accepted
                && exitAboveFifteen.Phase == UnitMovementPhase.AlignToMove
                && !exitAboveFifteen.AllowsMovement,
                "An active B2 Move must stop above 15 degrees.");

            var aboveEntry = new UnitMovementReducer();
            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.Chase, true,
                    forward, yaw10Over, false, 1d,
                    out UnitMovementDecision alignAboveTen)
                == UnitMovementReducerStatus.Accepted
                && alignAboveTen.Phase == UnitMovementPhase.AlignToMove
                && !alignAboveTen.AllowsMovement,
                "A new B2 movement scope must align above 10 degrees.");
            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.Chase, true,
                    forward, yaw10, false, 2d,
                    out UnitMovementDecision enterAfterAlign)
                == UnitMovementReducerStatus.Accepted
                && enterAfterAlign.Phase == UnitMovementPhase.Move,
                "AlignToMove must enter Move once error reaches 10 degrees.");

            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 1UL, 2UL,
                    UnitMovementIntentReason.PostCombatResume, true,
                    forward, yaw12, false, 3d,
                    out UnitMovementDecision newSegment)
                == UnitMovementReducerStatus.Accepted
                && newSegment.Phase == UnitMovementPhase.AlignToMove,
                "A new segment must use the 10-degree entry threshold instead of inheriting Move.");
            Require(aboveEntry.Snapshot.CommandRevision == 1UL
                && aboveEntry.Snapshot.SegmentRevision == 2UL
                && aboveEntry.Snapshot.IntentReason == UnitMovementIntentReason.PostCombatResume,
                "B2 snapshot must retain exact command, segment, and intent-reason scope.");

            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 1UL, 2UL,
                    UnitMovementIntentReason.PostCombatResume, true,
                    forward, forward, true, 4d,
                    out UnitMovementDecision targetPriority)
                == UnitMovementReducerStatus.Accepted
                && targetPriority.IsValid
                && targetPriority.IsTargetAcquirePriority
                && targetPriority.Phase == UnitMovementPhase.NoIntent
                && !targetPriority.AllowsMovement
                && !targetPriority.HasYawError,
                "Target acquisition must override even a perfectly aligned movement intent.");

            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 5d,
                    out UnitMovementDecision nextCommand)
                == UnitMovementReducerStatus.Accepted
                && nextCommand.Phase == UnitMovementPhase.Move
                && aboveEntry.Snapshot.CommandRevision == 2UL
                && aboveEntry.Snapshot.SegmentRevision == 1UL,
                "The next command must restart at segment 1 and evaluate independently.");

            UnitMovementSnapshot stable = aboveEntry.Snapshot;
            Require(aboveEntry.Evaluate(
                    stable.Revision - 1UL, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 6d, out UnitMovementDecision staleRevision)
                == UnitMovementReducerStatus.StaleRevision
                && staleRevision.Phase == UnitMovementPhase.Invalid
                && !staleRevision.IsValid,
                "A stale reducer revision must fail closed.");
            Require(aboveEntry.Evaluate(
                    stable.Revision, 1UL, 2UL,
                    UnitMovementIntentReason.PostCombatResume, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.StaleCommandRevision,
                "An old command revision must be rejected.");
            Require(aboveEntry.Evaluate(
                    stable.Revision, 2UL, 0UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "Segment revision zero must be rejected.");
            Require(aboveEntry.Evaluate(
                    stable.Revision, 2UL, 3UL,
                    UnitMovementIntentReason.PendingRepath, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "A skipped segment revision must fail closed.");
            Require(aboveEntry.Evaluate(
                    stable.Revision, 4UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "A skipped command revision must fail closed.");

            var staleSegmentReducer = new UnitMovementReducer();
            Require(staleSegmentReducer.Evaluate(
                    staleSegmentReducer.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 1d, out _)
                == UnitMovementReducerStatus.Accepted
                && staleSegmentReducer.Evaluate(
                    staleSegmentReducer.Snapshot.Revision, 1UL, 2UL,
                    UnitMovementIntentReason.PendingRepath, true,
                    forward, forward, false, 2d, out _)
                == UnitMovementReducerStatus.Accepted
                && staleSegmentReducer.Evaluate(
                    staleSegmentReducer.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 3d, out _)
                == UnitMovementReducerStatus.StaleSegmentRevision,
                "An old segment in the current command must be rejected.");

            UnitMovementSnapshot beforeInvalid = aboveEntry.Snapshot;
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    (UnitMovementIntentReason)999, true,
                    forward, forward, false, 6d,
                    out UnitMovementDecision unknownReason)
                == UnitMovementReducerStatus.InvalidInput
                && unknownReason.Phase == UnitMovementPhase.Invalid
                && !unknownReason.AllowsMovement,
                "An unknown movement intent reason must fail closed.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.None, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "A present intent cannot use the None reason.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.None, false,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "NoIntent must not carry a desired direction.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    default, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "An invalid SimulationFacing must fail closed.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, default, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "An invalid DesiredMoveDirection must fail closed.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, double.NaN, out _)
                == UnitMovementReducerStatus.InvalidTime,
                "NaN server time must fail closed.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw10, false, 5d,
                    out UnitMovementDecision sameTimeChangedPose)
                == UnitMovementReducerStatus.Accepted
                && sameTimeChangedPose.Phase == UnitMovementPhase.Move,
                "Distinct pre/post writer samples at the same server time must both be accepted.");
            UnitMovementSnapshot afterSameTimeChangedPose = aboveEntry.Snapshot;
            Require(aboveEntry.Evaluate(
                    afterSameTimeChangedPose.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 4.999d, out _)
                == UnitMovementReducerStatus.InvalidTime,
                "Backward server time must fail closed.");
            Require(aboveEntry.Snapshot.Revision == afterSameTimeChangedPose.Revision
                && aboveEntry.Snapshot.CommandRevision == afterSameTimeChangedPose.CommandRevision
                && aboveEntry.Snapshot.SegmentRevision == afterSameTimeChangedPose.SegmentRevision
                && aboveEntry.Snapshot.Phase == afterSameTimeChangedPose.Phase,
                "Every rejected B2 observation must leave the latest accepted snapshot unchanged.");

            var noIntent = new UnitMovementReducer();
            Require(noIntent.Evaluate(
                    noIntent.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.None, false,
                    forward, default, false, 1d,
                    out UnitMovementDecision noIntentDecision)
                == UnitMovementReducerStatus.Accepted
                && noIntentDecision.Phase == UnitMovementPhase.NoIntent
                && noIntentDecision.IsValid
                && !noIntentDecision.AllowsMovement,
                "An explicit absence of movement intent must be a valid movement-forbidden decision.");

            UnitMovementReducer exhausted =
                UnitMovementReducer.CreateForValidation(ulong.MaxValue);
            Require(exhausted.Evaluate(
                    exhausted.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 1d, out _)
                == UnitMovementReducerStatus.Exhausted
                && exhausted.Snapshot.Revision == ulong.MaxValue
                && !exhausted.Snapshot.HasAcceptedObservation,
                "B2 reducer revision exhaustion must fail closed without wrapping.");
        }

        private static void ValidateB2MovementManifestChunking()
        {
            Type observerType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                observerType = assemblies[index].GetType(
                    "Hexiege.Infrastructure.UnitMovementShadowObserver",
                    throwOnError: false);
                if (observerType != null)
                    break;
            }

            Require(observerType != null,
                "B2 movement shadow observer type was not loaded.");
            const BindingFlags flags =
                BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo buildPayloads = observerType.GetMethod(
                "BuildCoverageManifestPayloadsForValidation",
                flags);
            MethodInfo buildCompletedLine = observerType.GetMethod(
                "BuildCoverageManifestCompletedLineForValidation",
                flags);
            PropertyInfo payloadLimitProperty = observerType.GetProperty(
                "CoverageManifestPayloadLimitForValidation",
                flags);
            PropertyInfo chunkLimitProperty = observerType.GetProperty(
                "MaximumCoverageManifestChunksForValidation",
                flags);
            Require(buildPayloads != null
                && buildCompletedLine != null
                && payloadLimitProperty != null
                && chunkLimitProperty != null,
                "B2 movement manifest validation seam is incomplete.");

            int payloadLimit = (int)payloadLimitProperty.GetValue(null);
            int maximumChunks = (int)chunkLimitProperty.GetValue(null);
            List<string> expectedEntries =
                BuildRealMatchMovementManifestFixture(4);
            var payloads = (IReadOnlyList<string>)buildPayloads.Invoke(
                null,
                new object[] { expectedEntries });
            Require(payloads.Count > 1 && payloads.Count <= maximumChunks,
                "Four-unit-type manifest exceeded reserved terminal capacity.");

            var reconstructed = new List<string>(expectedEntries.Count);
            for (int index = 0; index < payloads.Count; index++)
            {
                string payload = payloads[index];
                int payloadBytes = Encoding.UTF8.GetByteCount(payload);
                Require(payload.Length <= payloadLimit
                    && payloadBytes <= payloadLimit,
                    "Coverage manifest payload exceeded its safe limit.");
                Require(
                    payload.Contains($"chunk={index + 1}/{payloads.Count}"),
                    "Coverage manifest chunk ordinal is invalid.");
                Require(
                    payload.Contains($"payloadChars={payload.Length}"),
                    "Coverage manifest character metadata is invalid.");
                Require(
                    payload.Contains($"payloadUtf8Bytes={payloadBytes}"),
                    "Coverage manifest UTF-8 metadata is invalid.");

                string completedLine = (string)buildCompletedLine.Invoke(
                    null,
                    new object[] { payload });
                Require(Encoding.UTF8.GetByteCount(completedLine) < 1000,
                    "Completed Android coverage-manifest line reached 1000 UTF-8 bytes.");

                const string entriesMarker = ", entries=";
                int entriesIndex = payload.IndexOf(
                    entriesMarker,
                    StringComparison.Ordinal);
                Require(entriesIndex >= 0,
                    "Coverage manifest entries marker is missing.");
                string entries = payload.Substring(
                    entriesIndex + entriesMarker.Length);
                reconstructed.AddRange(entries.Split(';'));
            }

            Require(reconstructed.Count == expectedEntries.Count,
                "Coverage manifest reconstruction changed the entry count.");
            for (int index = 0; index < expectedEntries.Count; index++)
            {
                Require(
                    string.Equals(
                        reconstructed[index],
                        expectedEntries[index],
                        StringComparison.Ordinal),
                    $"Coverage manifest entry {index} was lost, duplicated, or reordered.");
            }

            var emptyPayloads = (IReadOnlyList<string>)buildPayloads.Invoke(
                null,
                new object[] { Array.Empty<string>() });
            Require(emptyPayloads.Count == 1
                && emptyPayloads[0].EndsWith(
                    ", entries=none",
                    StringComparison.Ordinal)
                && Encoding.UTF8.GetByteCount(
                    (string)buildCompletedLine.Invoke(
                        null,
                        new object[] { emptyPayloads[0] })) < 1000,
                "Empty coverage manifest is not Android-safe.");

            var fiveTypePayloads =
                (IReadOnlyList<string>)buildPayloads.Invoke(
                    null,
                    new object[]
                    {
                        BuildRealMatchMovementManifestFixture(5)
                    });
            Require(fiveTypePayloads.Count <= maximumChunks,
                "A five-unit-type manifest that fits the terminal budget was rejected.");

            var exactBoundaryEntries = new List<string>();
            for (int index = 0; index < maximumChunks; index++)
            {
                exactBoundaryEntries.Add(
                    $"stats|BoundaryUnit{index:D2}|Blue|AStarPath|" +
                    new string('x', 580));
            }

            var exactBoundaryPayloads =
                (IReadOnlyList<string>)buildPayloads.Invoke(
                    null,
                    new object[] { exactBoundaryEntries });
            Require(exactBoundaryPayloads.Count == maximumChunks,
                "The exact reserved-terminal boundary must produce 29 chunks.");
            for (int index = 0; index < exactBoundaryPayloads.Count; index++)
            {
                string payload = exactBoundaryPayloads[index];
                int payloadBytes = Encoding.UTF8.GetByteCount(payload);
                Require(
                    payload.Contains(
                        $"chunk={index + 1}/{maximumChunks}"),
                    "Boundary coverage manifest chunk ordinal is invalid.");
                Require(
                    payload.Contains($"payloadChars={payload.Length}"),
                    "Boundary coverage manifest character metadata is invalid.");
                Require(
                    payload.Contains($"payloadUtf8Bytes={payloadBytes}"),
                    "Boundary coverage manifest UTF-8 metadata is invalid.");
                Require(payload.Length <= payloadLimit
                    && payloadBytes <= payloadLimit,
                    "Boundary coverage manifest payload exceeded its safe limit.");
                string completedLine = (string)buildCompletedLine.Invoke(
                    null,
                    new object[] { payload });
                Require(Encoding.UTF8.GetByteCount(completedLine) < 1000,
                    "Boundary coverage-manifest line reached 1000 UTF-8 bytes.");
            }

            var oversizedEntries = new List<string>();
            for (int index = 0; index < maximumChunks + 1; index++)
            {
                oversizedEntries.Add(
                    $"stats|OverflowUnit{index:D2}|Blue|AStarPath|" +
                    new string('x', 580));
            }

            bool oversizedRejected = false;
            try
            {
                buildPayloads.Invoke(
                    null,
                    new object[] { oversizedEntries });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
                oversizedRejected = true;
            }

            Require(oversizedRejected,
                "A manifest requiring more than the reserved chunks must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { new string('x', payloadLimit + 1) },
                "A single oversized coverage entry must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                null,
                "A null coverage entry list must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { string.Empty },
                "An empty coverage entry must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { "coverage|Unit;injected" },
                "A coverage entry containing a semicolon must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { "coverage|Unit\rinjected" },
                "A coverage entry containing CR must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { "coverage|Unit\ninjected" },
                "A coverage entry containing LF must fail closed.");
        }

        private static void ValidateB2MovementEndpointAdapter()
        {
            Type observerType = FindMovementShadowObserverType();
            const BindingFlags staticFlags =
                BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo evaluateEndpoint = observerType.GetMethod(
                "EvaluateEndpointAdapterForValidation",
                staticFlags);
            MethodInfo evaluateLifecycle = observerType.GetMethod(
                "EvaluateEndpointLifecycleForValidation",
                staticFlags);
            PropertyInfo toleranceProperty = observerType.GetProperty(
                "PositionDeltaToleranceForValidation",
                staticFlags);
            Require(evaluateEndpoint != null
                && evaluateLifecycle != null
                && toleranceProperty != null,
                "B2 endpoint adapter validation seam is incomplete.");
            float tolerance = (float)toleranceProperty.GetValue(null);

            object cannonEndpoint = evaluateEndpoint.Invoke(
                null,
                new object[] { 4.5f, -12.99f, 4.5f, -12.99f, 0f, false });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    cannonEndpoint, "Status")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    cannonEndpoint, "Phase")
                    == UnitMovementPhase.NoIntent
                && ReadValidationProperty<bool>(
                    cannonEndpoint, "DecisionIsValid")
                && ReadValidationProperty<bool>(
                    cannonEndpoint, "EndpointNormalized")
                && ReadValidationProperty<UnitMovementIntentReason>(
                    cannonEndpoint, "EvaluatedIntentReason")
                    == UnitMovementIntentReason.None
                && !ReadValidationProperty<bool>(
                    cannonEndpoint, "EvaluatedHasIntent")
                && !ReadValidationProperty<bool>(
                    cannonEndpoint, "EvaluatedDesiredDirectionIsValid")
                && ReadValidationProperty<ulong>(
                    cannonEndpoint, "RevisionAfterInitial") == 2UL
                && ReadValidationProperty<ulong>(
                    cannonEndpoint, "CommandRevisionAfterInitial") == 1UL
                && ReadValidationProperty<ulong>(
                    cannonEndpoint, "SegmentRevisionAfterInitial") == 1UL,
                "Captured CannonCart endpoint must normalize to explicit NoIntent.");

            object targetEndpoint = evaluateEndpoint.Invoke(
                null,
                new object[] { 4.5f, -12.99f, 4.5f, -12.99f, 0f, true });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    targetEndpoint, "TargetStatus")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    targetEndpoint, "TargetPhase")
                    == UnitMovementPhase.NoIntent
                && ReadValidationProperty<bool>(
                    targetEndpoint, "TargetDecisionIsValid")
                && ReadValidationProperty<bool>(
                    targetEndpoint, "TargetAcquirePriority")
                && ReadValidationProperty<UnitMovementIntentReason>(
                    targetEndpoint, "TargetSnapshotIntentReason")
                    == UnitMovementIntentReason.None
                && !ReadValidationProperty<bool>(
                    targetEndpoint, "TargetSnapshotHasIntent")
                && !ReadValidationProperty<bool>(
                    targetEndpoint, "TargetSnapshotDesiredDirectionIsValid"),
                "Endpoint target acquisition must preserve None/false/default input shape.");

            object normalMovement = evaluateEndpoint.Invoke(
                null,
                new object[] { 0f, 0f, 0f, 1f, 0.1f, false });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    normalMovement, "Status")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    normalMovement, "Phase")
                    == UnitMovementPhase.Move
                && !ReadValidationProperty<bool>(
                    normalMovement, "EndpointNormalized")
                && ReadValidationProperty<UnitMovementIntentReason>(
                    normalMovement, "EvaluatedIntentReason")
                    == UnitMovementIntentReason.AStarPath
                && ReadValidationProperty<bool>(
                    normalMovement, "EvaluatedHasIntent")
                && ReadValidationProperty<bool>(
                    normalMovement, "EvaluatedDesiredDirectionIsValid"),
                "Normal movement input shape must remain unchanged.");

            object zeroExpectedFarTarget = evaluateEndpoint.Invoke(
                null,
                new object[] { 0f, 0f, 0f, 1f, 0f, false });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    zeroExpectedFarTarget, "Status")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    zeroExpectedFarTarget, "Phase")
                    == UnitMovementPhase.Move
                && !ReadValidationProperty<bool>(
                    zeroExpectedFarTarget, "EndpointNormalized"),
                "A distant target with zero writer delta must remain a valid movement intent.");

            object toleranceBoundary = evaluateEndpoint.Invoke(
                null,
                new object[]
                {
                    0f, 0f, tolerance, 0f, tolerance, false
                });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    toleranceBoundary, "Status")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    toleranceBoundary, "Phase")
                    == UnitMovementPhase.NoIntent
                && ReadValidationProperty<bool>(
                    toleranceBoundary, "EndpointNormalized"),
                "Both endpoint values exactly at tolerance must normalize.");

            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[]
                {
                    0f, 0f, tolerance * 0.5f, 0f,
                    tolerance * 2f, false
                },
                "Near endpoint with material expected delta must fail closed.");
            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[] { 0f, 0f, 0f, 0f, tolerance * 2f, false },
                "Exact endpoint with material expected delta must fail closed.");
            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[] { 0f, 0f, 0f, 0f, float.NaN, false },
                "NaN expected delta must fail closed.");
            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[] { 0f, 0f, 0f, 0f, float.PositiveInfinity, false },
                "Infinite expected delta must fail closed.");
            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[] { 0f, 0f, 0f, 0f, -0.001f, false },
                "Negative expected delta must fail closed.");

            object lifecycle = evaluateLifecycle.Invoke(null, null);
            UnitMovementReducerStatus moveStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "MoveStatus");
            UnitMovementPhase movePhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "MovePhase");
            UnitMovementReducerStatus endpointStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "EndpointStatus");
            UnitMovementPhase endpointPhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "EndpointPhase");
            bool endpointNormalized =
                ReadValidationProperty<bool>(
                    lifecycle, "EndpointNormalized");
            UnitMovementReducerStatus duplicateEndpointStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "DuplicateEndpointStatus");
            UnitMovementReducerStatus priorityStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "PriorityStatus");
            UnitMovementPhase priorityPhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "PriorityPhase");
            bool priorityIsTargetAcquire =
                ReadValidationProperty<bool>(
                    lifecycle, "PriorityIsTargetAcquire");
            UnitMovementReducerStatus resumeAlignStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "ResumeAlignStatus");
            UnitMovementPhase resumeAlignPhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "ResumeAlignPhase");
            double resumeAlignYaw =
                ReadValidationProperty<double>(
                    lifecycle, "ResumeAlignYawErrorDegrees");
            UnitMovementReducerStatus resumeMoveStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "ResumeMoveStatus");
            UnitMovementPhase resumeMovePhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "ResumeMovePhase");
            double resumeMoveYaw =
                ReadValidationProperty<double>(
                    lifecycle, "ResumeMoveYawErrorDegrees");
            ulong revisionAfterMove =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterMove");
            ulong revisionAfterEndpoint =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterEndpoint");
            ulong revisionAfterDuplicate =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterDuplicate");
            ulong revisionAfterPriority =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterPriority");
            ulong revisionAfterResumeAlign =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterResumeAlign");
            ulong revisionAfterResumeMove =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterResumeMove");
            ulong finalCommandRevision =
                ReadValidationProperty<ulong>(
                    lifecycle, "FinalCommandRevision");
            ulong finalSegmentRevision =
                ReadValidationProperty<ulong>(
                    lifecycle, "FinalSegmentRevision");
            string lifecycleValues =
                $"move={moveStatus}/{movePhase}, " +
                $"endpoint={endpointStatus}/{endpointPhase}/normalized={endpointNormalized}, " +
                $"duplicate={duplicateEndpointStatus}, " +
                $"priority={priorityStatus}/{priorityPhase}/acquire={priorityIsTargetAcquire}, " +
                $"resumeAlign={resumeAlignStatus}/{resumeAlignPhase}/yaw={resumeAlignYaw:F9}, " +
                $"resumeMove={resumeMoveStatus}/{resumeMovePhase}/yaw={resumeMoveYaw:F9}, " +
                $"revisions={revisionAfterMove}/{revisionAfterEndpoint}/" +
                $"{revisionAfterDuplicate}/{revisionAfterPriority}/" +
                $"{revisionAfterResumeAlign}/{revisionAfterResumeMove}, " +
                $"scope={finalCommandRevision}/{finalSegmentRevision}";
            Require(
                moveStatus == UnitMovementReducerStatus.Accepted
                && movePhase == UnitMovementPhase.Move
                && endpointStatus == UnitMovementReducerStatus.Accepted
                && endpointPhase == UnitMovementPhase.NoIntent
                && endpointNormalized
                && duplicateEndpointStatus
                    == UnitMovementReducerStatus.Duplicate
                && priorityStatus == UnitMovementReducerStatus.Accepted
                && priorityPhase == UnitMovementPhase.NoIntent
                && priorityIsTargetAcquire
                && resumeAlignStatus
                    == UnitMovementReducerStatus.Accepted
                && resumeAlignPhase == UnitMovementPhase.AlignToMove
                && resumeMoveStatus == UnitMovementReducerStatus.Accepted
                && resumeMovePhase == UnitMovementPhase.Move,
                "Move -> endpoint -> priority -> same-scope resume lifecycle is invalid. "
                + lifecycleValues);
            Require(
                revisionAfterMove == 2UL
                && revisionAfterEndpoint == 3UL
                && revisionAfterDuplicate == 3UL
                && revisionAfterPriority == 4UL
                && revisionAfterResumeAlign == 5UL
                && revisionAfterResumeMove == 6UL
                && finalCommandRevision == 1UL
                && finalSegmentRevision == 1UL
                && Math.Abs(resumeAlignYaw - 12d) < 0.001d
                && Math.Abs(resumeMoveYaw - 9d) < 0.001d,
                "Endpoint lifecycle revisions, scope, or 12-to-9 degree entry restart changed. "
                + lifecycleValues);
        }

        private static void RequireEndpointAdapterInvalid(
            MethodInfo evaluateEndpoint,
            object[] arguments,
            string message)
        {
            object result = evaluateEndpoint.Invoke(null, arguments);
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    result, "Status")
                    == UnitMovementReducerStatus.InvalidInput
                && !ReadValidationProperty<bool>(
                    result, "DecisionIsValid")
                && !ReadValidationProperty<bool>(
                    result, "EndpointNormalized"),
                message);
        }

        private static T ReadValidationProperty<T>(
            object value,
            string propertyName)
        {
            PropertyInfo property = value.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(property != null,
                $"Validation property {propertyName} is missing.");
            return (T)property.GetValue(value);
        }

        private static Type FindMovementShadowObserverType()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(
                    "Hexiege.Infrastructure.UnitMovementShadowObserver",
                    throwOnError: false);
                if (type != null)
                    return type;
            }

            throw new InvalidOperationException(
                "B2 movement shadow observer type was not loaded.");
        }

        private static void RequireB2ManifestRejected(
            MethodInfo buildPayloads,
            IReadOnlyList<string> entries,
            string message)
        {
            bool rejected = false;
            try
            {
                buildPayloads.Invoke(
                    null,
                    new object[] { entries });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
                rejected = true;
            }

            Require(rejected, message);
        }

        private static List<string> BuildRealMatchMovementManifestFixture(
            int unitTypeCount)
        {
            string[] unitTypes =
            {
                "LittleKnight",
                "Pistoleer",
                "SpearMan",
                "RhinoBreaker",
                "AncientGuardian"
            };
            string[] teams = { "Blue", "Red" };
            string[] reasons =
            {
                "AStarPath",
                "Chase",
                "PendingRepath",
                "PostCombatResume"
            };
            var entries = new List<string>();
            for (int unitIndex = 0; unitIndex < unitTypeCount; unitIndex++)
            {
                for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
                {
                    for (int reasonIndex = 0;
                        reasonIndex < reasons.Length;
                        reasonIndex++)
                    {
                        string key =
                            $"{unitTypes[unitIndex]}|{teams[teamIndex]}|" +
                            reasons[reasonIndex];
                        entries.Add("coverage|" + key);
                        entries.Add(
                            $"stats|{key}|frames=31693,align=4364,move=27329," +
                            "targetPriority=107,writerExpectedAdvance=24259," +
                            "writerExpectedNoAdvance=3070,legacyMovedWhileAlign=1294," +
                            "legacyStoppedWhileMove=0");
                        if (reasonIndex != 1)
                            entries.Add("target-priority|" + key);
                    }
                }
            }

            entries.Sort(StringComparer.Ordinal);
            return entries;
        }

        private static ActionDirectionXZ CreateMovementDirection(double yawDegrees)
        {
            double radians = yawDegrees * (Math.PI / 180d);
            Require(ActionDirectionXZ.TryCreate(
                    Math.Sin(radians), Math.Cos(radians),
                    out ActionDirectionXZ direction),
                $"B2 direction fixture at {yawDegrees} degrees must be valid.");
            return direction;
        }

        private static void ValidateAttackHysteresis()
        {
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(-5d, false),
                "Attack alignment must enter at an absolute error of 5 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(5.001d, false),
                "Attack alignment must not enter above 5 degrees.");
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(-8d, true),
                "Attack alignment must remain active at an absolute error of 8 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(8.001d, true),
                "Attack alignment must exit above 8 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(double.NaN, true),
                "NaN must fail closed.");
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(355d, false),
                "355 degrees must normalize to the 5-degree attack entry boundary.");
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(352d, true),
                "352 degrees must normalize to the 8-degree attack exit boundary.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(double.NegativeInfinity, true),
                "Infinite attack error must fail closed.");
        }

        private static void ValidateDuplicateAndReorderedResults()
        {
            var instance = new AttackerInstanceId(7UL);
            var sequence = new AttackSequenceId(4UL);
            var buffer = new AttackResultOrderBuffer(instance, sequence, 100d);
            var first = new AttackResultKey(instance, sequence, 0, 1, 30, 2, 0);
            var second = new AttackResultKey(instance, sequence, 1, 1, 30, 2, 0);
            var laterOrdinal = new AttackResultKey(instance, sequence, 1, 1, 30, 2, 1);
            var firstCopy = new AttackResultKey(instance, sequence, 0, 1, 30, 2, 0);
            var otherInstance = new AttackResultKey(new AttackerInstanceId(8UL), sequence, 0, 1, 30, 2, 0);

            Require(first.Equals(firstCopy) && first.GetHashCode() == firstCopy.GetHashCode(),
                "The canonical identity fields must produce equal keys and hashes.");
            Require(!first.Equals(otherInstance) && first.CompareTo(otherInstance) != 0,
                "Attacker instance must participate in identity and ordering.");

            Require(buffer.TryAdd(laterOrdinal, 100.1d) == AttackResultBufferAddStatus.Accepted,
                "First arrival must be accepted.");
            Require(buffer.TryAdd(second, 100.2d) == AttackResultBufferAddStatus.Accepted,
                "Reordered hit must be accepted.");
            Require(buffer.TryAdd(first, 100.3d) == AttackResultBufferAddStatus.Accepted,
                "Earlier reordered hit must be accepted.");
            Require(buffer.TryAdd(second, 100.4d) == AttackResultBufferAddStatus.Duplicate,
                "Duplicate result key must be rejected.");
            Require(buffer.Count == 3, "Duplicate result changed buffer count.");
            Require(buffer.GetAt(0).Equals(first), "First hit was not restored to deterministic order.");
            Require(buffer.GetAt(1).Equals(second), "Second hit was not restored to deterministic order.");
            Require(buffer.GetAt(2).Equals(laterOrdinal), "Result ordinal was not restored to deterministic order.");

            var otherScope = new AttackResultKey(instance, new AttackSequenceId(5UL), 0, 1, 31, 2, 0);
            Require(buffer.TryAdd(otherScope, 100.5d) == AttackResultBufferAddStatus.ScopeMismatch,
                "A result from another attack sequence must be rejected.");
            Require(buffer.TryAdd(otherInstance, 100.5d) == AttackResultBufferAddStatus.ScopeMismatch,
                "A result from another attacker instance must be rejected.");

            buffer.Clear();
            Require(buffer.Count == 0, "Clear must remove all buffered results.");
            Require(buffer.TryAdd(first, 100.6d) == AttackResultBufferAddStatus.Accepted,
                "The fixed scope must remain usable after Clear.");

            var capacityBuffer = new AttackResultOrderBuffer(instance, sequence, 200d);
            for (int ordinal = 0; ordinal < AttackResultOrderBuffer.MaximumResultCount; ordinal++)
            {
                var key = new AttackResultKey(instance, sequence, 0, 1, ordinal, 2, ordinal);
                Require(capacityBuffer.TryAdd(key, 200.5d) == AttackResultBufferAddStatus.Accepted,
                    $"Result {ordinal} within the 64-result limit must be accepted.");
            }

            var overflow = new AttackResultKey(instance, sequence, 0, 1, 999, 2, 64);
            Require(capacityBuffer.TryAdd(overflow, 200.5d) == AttackResultBufferAddStatus.CapacityExceeded,
                "The 65th unique result must be rejected.");

            var expiryBuffer = new AttackResultOrderBuffer(instance, sequence, 300d);
            Require(expiryBuffer.TryAdd(first, 301d) == AttackResultBufferAddStatus.Accepted,
                "A result arriving before expiry must be buffered.");
            Require(expiryBuffer.TryAdd(second, 302d) == AttackResultBufferAddStatus.Expired,
                "A result arriving at the two-second lifetime boundary must be rejected.");
            Require(expiryBuffer.Count == 0,
                "Expiry must discard results that were buffered before the lifetime boundary.");
        }

        /// <summary>
        /// A1에서 추가한 순수 값 계약과 reducer의 핵심 불변을 한 장면이나 네트워크 없이 검증한다.
        /// 실제 피해 성공은 만들지 않고 외부 writer가 돌려준 결과를 확인하는 단계까지만 다룬다.
        /// </summary>
        private static void ValidateA1ContractsAndReducer()
        {
            ValidateA1ValueContractsAndRange();
            ValidateA1ReducerRevisionCommitAndMultiHit();
            ValidateA1ReducerCancellationAndDeath();
            ValidateA1OverflowAndAtomicReset();
            CheckA2PoseContracts();
        }

        /// <summary>A2 pose 값은 Unity 없이 원점·정규화·거리·yaw와 optional 이동 방향을 검증한다.</summary>
        private static void CheckA2PoseContracts()
        {
            var target = new EntityRef(EntityKind.Unit, 0);
            Require(UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    0d, 2d,
                    0d, 2d,
                    false, double.NaN, double.NaN,
                    out UnitActionPoseSample withoutDesired),
                "World origin attacker and optional missing desired direction must remain valid.");
            Require(withoutDesired.IsValid
                && !withoutDesired.HasDesiredMoveDirection
                && !withoutDesired.DesiredMoveDirection.IsValid
                && Math.Abs(withoutDesired.SimulationFacing.Z - 1d) < 0.000001d
                && Math.Abs(withoutDesired.TargetAimDirection.Z - 1d) < 0.000001d
                && Math.Abs(withoutDesired.TargetSquaredDistance - 4d) < 0.000001d
                && Math.Abs(withoutDesired.FacingToAimYawDegrees) < 0.000001d,
                "Pose must normalize facing/aim and calculate squared distance and zero yaw.");

            Require(UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    0d, 1d,
                    2d, 0d,
                    true, 3d, 4d,
                    out UnitActionPoseSample withDesired),
                "Finite pose with an explicit desired move target must be valid.");
            Require(withDesired.HasDesiredMoveDirection
                && Math.Abs(withDesired.DesiredMoveDirection.X - 0.6d) < 0.000001d
                && Math.Abs(withDesired.DesiredMoveDirection.Z - 0.8d) < 0.000001d
                && Math.Abs(withDesired.FacingToAimYawDegrees - 90d) < 0.000001d
                && Math.Abs(withDesired.TargetSquaredDistance - 4d) < 0.000001d,
                "Desired direction must normalize and facing-to-aim yaw must be 90 degrees.");

            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 0d, 1d, 0d, false, 0d, 0d, out _),
                "Zero facing must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 0d, 0d, false, 0d, 0d, out _),
                "A target at the attacker XZ must fail closed because aim is undefined.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, double.NaN, 1d, false, 0d, 0d, out _),
                "NaN target position must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1d, 0d, true, 0d, 0d, out _),
                "An explicitly present zero desired direction must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    EntityRef.None, 0d, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _)
                && !UnitActionPoseSample.TryCreate(
                    new EntityRef((EntityKind)999, 1), 0d, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _),
                "None and unknown target kinds must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, double.NaN, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _)
                && !UnitActionPoseSample.TryCreate(
                    target, double.PositiveInfinity, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _),
                "NaN and infinite attacker coordinates must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1e200d, 1e200d, false, 0d, 0d, out _),
                "Squared target distance overflow must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1d, 0d, true, double.NaN, 1d, out _)
                && !UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1d, 0d, true, double.PositiveInfinity, 1d, out _),
                "Present desired target NaN and infinity must fail closed.");
            Require(UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 0d, -2d, false, 0d, 0d,
                    out UnitActionPoseSample opposite)
                && Math.Abs(opposite.FacingToAimYawDegrees - 180d) < 0.000001d,
                "Opposite facing-to-aim yaw must be 180 degrees.");
        }

        private static void ValidateA1ValueContractsAndRange()
        {
            var zeroIdUnit = new EntityRef(EntityKind.Unit, 0);
            Require(zeroIdUnit.IsValid && !zeroIdUnit.IsNone, "Entity ID 0 must remain valid.");
            Require(EntityRef.None.IsNone && !EntityRef.None.IsValid, "None must be represented by EntityKind.None.");

            Require(!ActionDirectionXZ.TryCreate(0d, 0d, out _), "Zero direction must be rejected.");
            Require(!ActionDirectionXZ.TryCreate(double.NaN, 1d, out _), "NaN direction must be rejected.");
            Require(!ActionDirectionXZ.TryCreate(double.PositiveInfinity, 1d, out _), "Infinite direction must be rejected.");
            Require(ActionDirectionXZ.TryCreate(3d, 4d, out ActionDirectionXZ direction),
                "Finite non-zero direction must be created.");
            Require(Math.Abs(direction.X - 0.6d) < 0.000001d && Math.Abs(direction.Z - 0.8d) < 0.000001d,
                "Direction must be normalized.");
            Require(WorldPointXZ.TryCreate(0d, 0d, out WorldPointXZ origin) && origin.IsValid,
                "World origin must be a valid point.");
            Require(!WorldPointXZ.TryCreate(double.NaN, 0d, out _), "NaN point must be rejected.");

            double[] sourceOffsets = { 0.2d, 0.8d };
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, sourceOffsets, out AttackTimelinePlan timeline),
                "A valid multi-hit timeline must be created.");
            sourceOffsets[0] = 1.5d;
            Require(Math.Abs(timeline.GetImpactOffset(0) - 0.2d) < 0.000001d,
                "Timeline must defensively copy marker offsets.");
            Require(!AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.8d, 0.8d }, out _),
                "Duplicate timeline offsets must be rejected.");
            Require(!AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.8d, 0.2d }, out _),
                "Descending timeline offsets must be rejected.");
            Require(!AttackTimelinePlan.TryCreate(1d, 1d, new[] { 1d }, out _),
                "A marker at the cooldown boundary must be rejected.");
            Require(!AttackTimelinePlan.TryCreate(2d, 0.7d, new[] { 0.8d }, out _),
                "Recovery before the last marker must be rejected.");

            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile melee),
                "The v2 melee range profile must be created.");
            Require(melee.UsesMeleeContact, "AttackRange 0.5 must use v2 MeleeContact.");
            Require(Math.Abs(melee.GetMaximumDistance(EntityKind.Unit) - 0.68d) < 0.000001d,
                "The v2 unit melee boundary must be 0.63 + 0.05.");
            Require(Math.Abs(melee.GetMaximumDistance(EntityKind.Building) - 0.88d) < 0.000001d,
                "The v2 building melee boundary must also include radius 0.20.");
            Require(melee.ContainsSquaredDistance(0.50d * 0.50d, EntityKind.Unit)
                && !(0.50d * 0.50d <= 0.35d * 0.35d),
                "Distance 0.50 must expose v2 true versus Legacy unit 0.35 false.");
            Require(melee.ContainsSquaredDistance(0.70d * 0.70d, EntityKind.Building)
                && !(0.70d * 0.70d <= 0.55d * 0.55d),
                "Distance 0.70 must expose v2 true versus Legacy building 0.55 false.");
            Require(AttackRangeProfile.TryCreate(0.5001d, 1d, out AttackRangeProfile classifierConflict)
                && !classifierConflict.UsesMeleeContact && 0.5001d < 1d,
                "AttackRange 0.5001 must expose the v2 <=0.5 versus Legacy <1 classifier conflict.");
            Require(AttackRangeProfile.TryCreate(1d, 1d, out AttackRangeProfile spearRange)
                && !spearRange.UsesMeleeContact
                && Math.Abs(spearRange.GetMaximumDistance(EntityKind.Unit) - 1.05d) < 0.000001d,
                "Actual SpearMan AttackRange 1 must use the scaled-center v2 range mode.");
            Require(!AttackRangeProfile.TryCreate(double.MaxValue, 2d, out _),
                "Range multiplication overflow must fail closed.");
            Require(!AttackRangeProfile.TryCreate(1e200d, 1d, out _),
                "Range squared-distance overflow must fail closed.");

            var unsupported = new UnitActionSequencer(new AttackerInstanceId(100UL), 0);
            var target = new AttackTargetBinding(AttackTargetMode.TargetLocked, zeroIdUnit);
            UnitActionSnapshot initial = unsupported.Snapshot;
            Require(unsupported.BeginAttackAlignment(
                    initial.Revision, target, AttackDeliveryKind.ProjectileImpact,
                    timeline, melee, direction, 1d) == UnitActionReducerStatus.UnsupportedDelivery,
                "A1 reducer must reject unsupported ProjectileImpact delivery.");
            Require(unsupported.Snapshot.Revision == initial.Revision && initial.Phase == UnitActionPhase.Idle,
                "Rejected delivery must not mutate the immutable initial snapshot.");

            var lockedPoint = new AttackTargetBinding(AttackTargetMode.LockedPoint, zeroIdUnit);
            var homing = new AttackTargetBinding(AttackTargetMode.Homing, zeroIdUnit);
            Require(lockedPoint.IsValid && homing.IsValid,
                "Future target binding vocabulary must remain representable by the contract.");
            Require(unsupported.BeginAttackAlignment(
                    initial.Revision, lockedPoint, AttackDeliveryKind.MeleeContact,
                    timeline, melee, direction, 1d) == UnitActionReducerStatus.UnsupportedTargetBinding,
                "A1 reducer must explicitly reject LockedPoint binding.");
            Require(unsupported.BeginAttackAlignment(
                    initial.Revision, homing, AttackDeliveryKind.MeleeContact,
                    timeline, melee, direction, 1d) == UnitActionReducerStatus.UnsupportedTargetBinding,
                "A1 reducer must explicitly reject Homing binding.");
        }

        private static void ValidateA1ReducerRevisionCommitAndMultiHit()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "Test facing must be valid.");
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.2d, 0.8d }, out AttackTimelinePlan timeline),
                "Test timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile range),
                "Test range must be valid.");

            var target = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 0));
            var otherTarget = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 1));
            var reducer = new UnitActionSequencer(new AttackerInstanceId(200UL), 0);
            UnitActionSnapshot initial = reducer.Snapshot;

            Require(initial.Revision == 1UL && initial.LastConfirmedHitIndex == -1,
                "Initial revision must be 1 and confirmed prefix must be -1.");
            Require(reducer.BeginAttackAlignment(
                    initial.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 10d) == UnitActionReducerStatus.Accepted,
                "Alignment must be accepted.");
            UnitActionSnapshot aligned = reducer.Snapshot;
            Require(aligned.Revision == 2UL && aligned.Phase == UnitActionPhase.AlignToAttack,
                "Accepted alignment must increment revision exactly once.");
            Require(initial.Revision == 1UL && initial.Phase == UnitActionPhase.Idle,
                "Previously captured snapshot must remain immutable.");

            Require(reducer.BeginAttackAlignment(
                    1UL, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 10d) == UnitActionReducerStatus.StaleRevision,
                "Stale revision must fail closed.");
            Require(reducer.Advance(aligned.Revision, 10d) == UnitActionReducerStatus.NoChange
                && reducer.Snapshot.Revision == aligned.Revision,
                "No-op must not increment revision.");
            Require(reducer.Advance(aligned.Revision, 9.9d) == UnitActionReducerStatus.InvalidInput,
                "Backward observed time must fail closed.");
            Require(reducer.Advance(aligned.Revision, double.NaN) == UnitActionReducerStatus.InvalidInput,
                "NaN observed time must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, double.NaN, 10d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "NaN yaw must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 10d, double.PositiveInfinity)
                == UnitActionReducerStatus.InvalidInput,
                "Infinite observed time must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 9.9d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "Commit time before last accepted time must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 10.2d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "Commit time after observed now must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, otherTarget, true, true, true, 0.25d, 5d, 10d, 10.1d)
                == UnitActionReducerStatus.ScopeMismatch,
                "Commit must not replace the aligned target.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5.001d, 10d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "Commit above the 5-degree boundary must fail.");
            Require(reducer.Snapshot.Revision == aligned.Revision,
                "Rejected commit attempts must not mutate revision.");

            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 10d, 10.1d)
                == UnitActionReducerStatus.Accepted,
                "Commit at the 5-degree boundary must succeed.");
            UnitActionSnapshot committed = reducer.Snapshot;
            Require(committed.SequenceId.IsValid && committed.CooldownConsumed
                && committed.TargetBinding == target && committed.Phase == UnitActionPhase.Windup,
                "Commit must consume cooldown, allocate one sequence, and lock the target.");

            Require(reducer.Advance(committed.Revision, 10.9d) == UnitActionReducerStatus.Accepted,
                "A time leap must mark every elapsed hit due in one revision.");
            UnitActionSnapshot due = reducer.Snapshot;
            Require(due.DueHitMask == 3UL, "Both multi-hit markers must become due.");

            Require(reducer.EvaluateImpact(
                    due.Revision, 0, 7, 11, otherTarget, true, true, 0.25d, 8d, facing, 10.91d, out _)
                == UnitActionReducerStatus.ScopeMismatch
                && reducer.Snapshot.TargetBinding == target,
                "Committed TargetLocked binding must reject target transfer without mutation.");

            Require(reducer.EvaluateImpact(
                    due.Revision, 1, 8, 12, target, true, true, 0.25d, 8d, facing, 10.91d, out _)
                == UnitActionReducerStatus.OutOfOrder,
                "Due hits must be decided in HitIndex order.");
            Require(reducer.EvaluateImpact(
                    due.Revision, 0, 7, 11, target, true, true, 0.25d, 8d, facing, 10.91d,
                    out ImpactAuthorization hitAuthorization)
                == UnitActionReducerStatus.Accepted
                && hitAuthorization.Outcome == ImpactAuthorizationOutcome.AuthorizedHit
                && hitAuthorization.Key.Equals(new AttackResultKey(
                    committed.AttackerInstanceId, committed.SequenceId, 0,
                    (int)EntityKind.Unit, 0, 7, 11)),
                "Impact at the 8-degree boundary must authorize a hit.");
            UnitActionSnapshot hitZeroDecided = reducer.Snapshot;
            Require(reducer.EvaluateImpact(
                    hitZeroDecided.Revision, 0, 7, 11, target, true, true, 0.25d, 8d, facing, 10.92d, out _)
                == UnitActionReducerStatus.Duplicate,
                "The same hit must not be authorized twice.");
            Require(reducer.EvaluateImpact(
                    hitZeroDecided.Revision, 1, 8, 12, target, true, true, 0.25d, 8.001d, facing, 10.92d,
                    out ImpactAuthorization missAuthorization)
                == UnitActionReducerStatus.Accepted
                && missAuthorization.Outcome == ImpactAuthorizationOutcome.AuthorizedMiss,
                "A later hit above 8 degrees must miss independently.");
            UnitActionSnapshot bothDecided = reducer.Snapshot;
            Require(bothDecided.Phase == UnitActionPhase.Recovery && bothDecided.DecidedHitMask == 3UL,
                "All independently decided hits must enter Recovery.");

            var hitZeroKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0,
                (int)EntityKind.Unit, 0, 7, 11);
            var hitOneKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 1,
                (int)EntityKind.Unit, 0, 8, 12);
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 10, 40, out AttackImpactResult hitResult),
                "External writer must be able to create a valid applied result.");
            Require(AttackImpactResult.TryCreate(
                    missAuthorization.ActionRevision, hitOneKey, 10.8d, facing, false, default,
                    AttackImpactOutcome.Miss, 0, -1, out AttackImpactResult missResult),
                "External writer must be able to create a valid miss result.");
            Require(!AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 0, 40, out _),
                "A successful result without applied amount must be rejected.");
            var invalidKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0, 0, 0, 0, 0);
            Require(!AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, invalidKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Miss, 0, -1, out _),
                "AttackImpactResult must reject an invalid canonical key.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Evaded, 0, -1, out _),
                "Evaded outcome with zero amount must be valid.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Immune, 0, -1, out _),
                "Immune outcome with zero amount must be valid.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.StatusEffectApplied, 0, 40, out _),
                "A status effect may be applied without direct damage.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Cancelled, 0, -1, out _),
                "Cancelled outcome with zero amount must remain representable.");
            Require(!AttackImpactResult.TryCreate(
                    0UL, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Evaded, 0, -1, out _),
                "Result action revision 0 must be rejected.");

            var wrongEffectKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0,
                (int)EntityKind.Unit, 0, 70, 11);
            var wrongOrdinalKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0,
                (int)EntityKind.Unit, 0, 7, 110);
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, wrongEffectKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 10, 40, out AttackImpactResult wrongEffectResult),
                "EffectKind mutation fixture must remain a structurally valid result.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, wrongOrdinalKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 10, 40, out AttackImpactResult wrongOrdinalResult),
                "ResultOrdinal mutation fixture must remain a structurally valid result.");
            Require(reducer.ConfirmImpactResult(bothDecided.Revision, wrongEffectResult, 10.95d)
                == UnitActionReducerStatus.ScopeMismatch
                && reducer.Snapshot.Revision == bothDecided.Revision,
                "A result with the wrong EffectKind must be rejected without mutation.");
            Require(reducer.ConfirmImpactResult(bothDecided.Revision, wrongOrdinalResult, 10.96d)
                == UnitActionReducerStatus.ScopeMismatch
                && reducer.Snapshot.Revision == bothDecided.Revision,
                "A result with the wrong ResultOrdinal must be rejected without mutation.");

            Require(reducer.ConfirmImpactResult(bothDecided.Revision, missResult, 11d)
                == UnitActionReducerStatus.Accepted && reducer.Snapshot.LastConfirmedHitIndex == -1,
                "Confirming hit 1 first must keep the contiguous prefix at -1.");
            UnitActionSnapshot hitOneConfirmed = reducer.Snapshot;
            Require(reducer.ConfirmImpactResult(hitOneConfirmed.Revision, hitResult, 11.1d)
                == UnitActionReducerStatus.Accepted && reducer.Snapshot.LastConfirmedHitIndex == 1,
                "Confirming hit 0 later must advance the contiguous prefix through hit 1.");
            UnitActionSnapshot confirmed = reducer.Snapshot;
            Require(reducer.ConfirmImpactResult(confirmed.Revision, hitResult, 11.2d)
                == UnitActionReducerStatus.Duplicate,
                "A confirmed result must be idempotent.");
            Require(reducer.Advance(confirmed.Revision, 11.9d) == UnitActionReducerStatus.NoChange,
                "Recovery must wait for the later of cooldown and recovery end.");
            Require(reducer.Advance(confirmed.Revision, 12d) == UnitActionReducerStatus.Accepted
                && reducer.Snapshot.Phase == UnitActionPhase.AcquireTarget,
                "Recovery must finish exactly at max(cooldownEnd,recoveryEnd).");
            UnitActionSnapshot completed = reducer.Snapshot;
            Require(completed.TargetBinding == AttackTargetBinding.None
                && completed.Delivery == AttackDeliveryKind.None
                && !completed.SequenceId.IsValid
                && completed.Timeline == null
                && !completed.RangeProfile.IsValid
                && !completed.SimulationFacing.IsValid
                && !completed.HasSimulationAimDirection
                && !completed.SimulationAimDirection.IsValid,
                "Action end must clear target, plan, sequence, facing, and aim atomically.");
            Require(completed.StartServerTime == 0d
                && completed.CommitServerTime == 0d
                && completed.CooldownEndServerTime == 0d
                && completed.RecoveryEndServerTime == 0d
                && !completed.CooldownConsumed
                && completed.DueHitMask == 0UL
                && completed.DecidedHitMask == 0UL
                && completed.ConfirmedHitMask == 0UL
                && completed.LastConfirmedHitIndex == -1
                && completed.PhaseStartServerTime == 12d,
                "Action end must clear all timing and hit-cycle data while recording the new phase start.");
            Require(reducer.BeginAttackAlignment(
                    completed.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 12.1d) == UnitActionReducerStatus.Accepted,
                "A completed reducer must start the next alignment.");
            UnitActionSnapshot nextAlignment = reducer.Snapshot;
            Require(nextAlignment.Phase == UnitActionPhase.AlignToAttack
                && nextAlignment.StartServerTime == 12.1d
                && nextAlignment.CommitServerTime == 0d
                && nextAlignment.DueHitMask == 0UL
                && nextAlignment.DecidedHitMask == 0UL
                && nextAlignment.ConfirmedHitMask == 0UL
                && nextAlignment.LastConfirmedHitIndex == -1
                && !nextAlignment.HasSimulationAimDirection,
                "The next Align must not inherit any timing, hit, or aim state from the old cycle.");
        }

        private static void ValidateA1ReducerCancellationAndDeath()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "Test facing must be valid.");
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.2d, 0.8d }, out AttackTimelinePlan timeline),
                "Test timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile range),
                "Test range must be valid.");
            var target = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 3));

            var preCommit = new UnitActionSequencer(new AttackerInstanceId(300UL), 3);
            Require(preCommit.BeginAttackAlignment(
                    preCommit.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1d) == UnitActionReducerStatus.Accepted,
                "Pre-commit alignment must start.");
            UnitActionSnapshot beforeUnknownCancel = preCommit.Snapshot;
            Require(preCommit.CancelPreCommit(
                    beforeUnknownCancel.Revision, (PreCommitCancelReason)999, true, 1.05d)
                == UnitActionReducerStatus.InvalidInput,
                "Unknown pre-commit cancellation reason must fail closed.");
            Require(preCommit.Snapshot.Revision == beforeUnknownCancel.Revision
                && preCommit.Snapshot.Phase == beforeUnknownCancel.Phase
                && preCommit.Snapshot.TargetBinding == beforeUnknownCancel.TargetBinding,
                "Unknown cancellation reason must not mutate state.");
            Require(preCommit.CancelPreCommit(
                    preCommit.Snapshot.Revision, PreCommitCancelReason.AttackRangeExited, true, 1.1d)
                == UnitActionReducerStatus.Accepted,
                "AttackRange exit inside LoseRange must enter Chase.");
            Require(preCommit.Snapshot.Phase == UnitActionPhase.Chase
                && preCommit.Snapshot.TargetBinding == target
                && !preCommit.Snapshot.SequenceId.IsValid
                && !preCommit.Snapshot.CooldownConsumed,
                "Pre-commit Chase must retain target without sequence or cooldown.");
            Require(preCommit.BeginAttackAlignment(
                    preCommit.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1.2d) == UnitActionReducerStatus.Accepted,
                "Chase must be able to re-enter alignment.");
            Require(preCommit.CancelPreCommit(
                    preCommit.Snapshot.Revision, PreCommitCancelReason.TargetDead, false, 1.3d)
                == UnitActionReducerStatus.Accepted,
                "Dead target must cancel pre-commit action.");
            Require(preCommit.Snapshot.Phase == UnitActionPhase.AcquireTarget
                && preCommit.Snapshot.TargetBinding == AttackTargetBinding.None
                && !preCommit.Snapshot.CooldownConsumed,
                "Invalid pre-commit target must clear without refund concerns.");

            var committedCancel = new UnitActionSequencer(new AttackerInstanceId(301UL), 3);
            Require(committedCancel.BeginAttackAlignment(
                    committedCancel.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 20d) == UnitActionReducerStatus.Accepted,
                "Committed-cancel fixture must align.");
            Require(committedCancel.CommitAttack(
                    committedCancel.Snapshot.Revision, target, true, true, true,
                    0.25d, 5d, 20d, 20.1d) == UnitActionReducerStatus.Accepted,
                "Committed-cancel fixture must commit.");
            AttackSequenceId committedSequence = committedCancel.Snapshot.SequenceId;
            Require(committedCancel.CancelCommitted(committedCancel.Snapshot.Revision, 20.2d)
                == UnitActionReducerStatus.Accepted,
                "Committed action must support future-hit cancellation.");
            Require(committedCancel.Snapshot.Phase == UnitActionPhase.Recovery
                && committedCancel.Snapshot.CooldownConsumed
                && committedCancel.Snapshot.SequenceId == committedSequence
                && committedCancel.Snapshot.DecidedHitMask == timeline.AllImpactMask,
                "Committed cancellation must suppress future hits without refund or target transfer.");

            var dead = new UnitActionSequencer(new AttackerInstanceId(302UL), 3);
            Require(dead.BeginAttackAlignment(
                    dead.Snapshot.Revision, target, AttackDeliveryKind.Hitscan,
                    timeline, range, facing, 30d) == UnitActionReducerStatus.Accepted,
                "Dead fixture must align.");
            Require(dead.CommitAttack(
                    dead.Snapshot.Revision, target, true, true, true,
                    0.25d, 5d, 30d, 30.1d) == UnitActionReducerStatus.Accepted,
                "Dead fixture must commit.");
            UnitActionSnapshot deadCommitted = dead.Snapshot;
            Require(dead.Advance(deadCommitted.Revision, 30.2d) == UnitActionReducerStatus.Accepted,
                "Dead fixture hit 0 must become due before death.");
            Require(dead.EvaluateImpact(
                    dead.Snapshot.Revision, 0, 9, 13, target, true, true, 0.25d, 0d,
                    facing, 30.21d, out ImpactAuthorization preDeathAuthorization)
                == UnitActionReducerStatus.Accepted,
                "Hit authorized before death must be retained.");
            var preDeathKey = new AttackResultKey(
                deadCommitted.AttackerInstanceId, deadCommitted.SequenceId, 0,
                (int)EntityKind.Unit, 3, 9, 13);
            Require(AttackImpactResult.TryCreate(
                    preDeathAuthorization.ActionRevision, preDeathKey, 30.21d,
                    facing, false, default, AttackImpactOutcome.HitApplied, 5, 45,
                    out AttackImpactResult preDeathResult),
                "Exact pre-death authorization result must be constructible.");
            Require(dead.MarkDead(dead.Snapshot.Revision) == UnitActionReducerStatus.Accepted,
                "MarkDead must be accepted without a time input.");
            UnitActionSnapshot deadSnapshot = dead.Snapshot;
            Require(deadSnapshot.Phase == UnitActionPhase.Dead
                && deadSnapshot.EndReason == UnitActionEndReason.AttackerDead
                && deadSnapshot.TargetBinding == target,
                "Dead must be a terminal snapshot with an explicit reason.");
            Require(dead.ConfirmImpactResult(deadSnapshot.Revision, preDeathResult, 30.3d)
                == UnitActionReducerStatus.Accepted,
                "An exact result authorized before death must still be confirmed after death.");
            var futureKey = new AttackResultKey(
                deadCommitted.AttackerInstanceId, deadCommitted.SequenceId, 1,
                (int)EntityKind.Unit, 3, 9, 14);
            Require(AttackImpactResult.TryCreate(
                    preDeathAuthorization.ActionRevision, futureKey, 30.3d,
                    facing, false, default, AttackImpactOutcome.Miss, 0, -1,
                    out AttackImpactResult unauthorizedFutureResult),
                "Future result fixture must be structurally valid.");
            Require(dead.ConfirmImpactResult(dead.Snapshot.Revision, unauthorizedFutureResult, 30.4d)
                == UnitActionReducerStatus.NotDue,
                "Death must reject a future hit that never received authorization.");
            deadSnapshot = dead.Snapshot;
            Require(dead.Advance(deadSnapshot.Revision, double.NaN) == UnitActionReducerStatus.DeadTerminal,
                "Dead terminal must reject later reducer operations before reading time.");
            Require(dead.MarkDead(deadSnapshot.Revision) == UnitActionReducerStatus.NoChange
                && dead.Snapshot.Revision == deadSnapshot.Revision,
                "Repeated MarkDead must be a no-op without revision growth.");
        }

        private static void ValidateA1OverflowAndAtomicReset()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "Overflow fixture facing must be valid.");
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.2d }, out AttackTimelinePlan timeline),
                "Overflow fixture timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile range),
                "Overflow fixture range must be valid.");
            var target = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 8));

            var revisionExhausted = UnitActionSequencer.CreateForValidation(
                new AttackerInstanceId(400UL), 8, ulong.MaxValue, 0UL);
            UnitActionSnapshot revisionBefore = revisionExhausted.Snapshot;
            Require(revisionExhausted.BeginAttackAlignment(
                    revisionBefore.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1d) == UnitActionReducerStatus.Exhausted,
                "Revision maximum must fail closed instead of wrapping.");
            Require(revisionExhausted.Snapshot.Revision == revisionBefore.Revision
                && revisionExhausted.Snapshot.Phase == revisionBefore.Phase,
                "Revision exhaustion must leave state unchanged.");

            var sequenceExhausted = UnitActionSequencer.CreateForValidation(
                new AttackerInstanceId(401UL), 8, 1UL, ulong.MaxValue);
            Require(sequenceExhausted.BeginAttackAlignment(
                    sequenceExhausted.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 2d) == UnitActionReducerStatus.Accepted,
                "Sequence exhaustion fixture must align.");
            UnitActionSnapshot sequenceBeforeCommit = sequenceExhausted.Snapshot;
            Require(sequenceExhausted.CommitAttack(
                    sequenceBeforeCommit.Revision, target, true, true, true,
                    0.25d, 0d, 2d, 2.1d) == UnitActionReducerStatus.Exhausted,
                "Sequence maximum must fail closed instead of throwing or wrapping.");
            UnitActionSnapshot sequenceAfterCommit = sequenceExhausted.Snapshot;
            Require(sequenceAfterCommit.Revision == sequenceBeforeCommit.Revision
                && sequenceAfterCommit.Phase == sequenceBeforeCommit.Phase
                && !sequenceAfterCommit.SequenceId.IsValid
                && !sequenceAfterCommit.CooldownConsumed,
                "Sequence exhaustion must leave the aligned action unchanged.");

            var allocator = new AttackSequenceAllocator(ulong.MaxValue);
            Require(!allocator.TryNext(new AttackerInstanceId(402UL), out AttackSequenceId exhaustedId)
                && !exhaustedId.IsValid,
                "Allocator TryNext must expose exhaustion without an exception.");

            Require(!UnitActionSequencer.TryCreateShadowCycle(
                    AttackerInstanceId.None, 8, new AttackSequenceId(1UL), out _)
                && !UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(403UL), -1, new AttackSequenceId(1UL), out _)
                && !UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(403UL), 8, AttackSequenceId.None, out _),
                "Runtime shadow factory must reject every invalid identity input.");
            Require(UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(404UL), 8, new AttackSequenceId(1UL), out UnitActionSequencer firstCycle)
                && firstCycle.BeginAttackAlignment(
                    firstCycle.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 3d) == UnitActionReducerStatus.Accepted
                && firstCycle.CommitAttack(
                    firstCycle.Snapshot.Revision, target, true, true, true,
                    0.25d, 0d, 3d, 3d) == UnitActionReducerStatus.Accepted
                && firstCycle.Snapshot.SequenceId.Value == 1UL,
                "Runtime shadow factory must allocate exact sequence 1.");
            Require(UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(405UL), 8, new AttackSequenceId(ulong.MaxValue),
                    out UnitActionSequencer maximumCycle)
                && maximumCycle.BeginAttackAlignment(
                    maximumCycle.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 4d) == UnitActionReducerStatus.Accepted
                && maximumCycle.CommitAttack(
                    maximumCycle.Snapshot.Revision, target, true, true, true,
                    0.25d, 0d, 4d, 4d) == UnitActionReducerStatus.Accepted
                && maximumCycle.Snapshot.SequenceId.Value == ulong.MaxValue,
                "Runtime shadow factory must allocate exact maximum sequence without overflow.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException($"[UAS-DIAG] self-validation FAIL: {message}");
        }
    }
}
