#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Core;
using Hexiege.Domain;
using Unity.Netcode;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// B2 development-only observer. It evaluates the production movement reducer immediately
    /// before a Legacy movement writer and correlates that exact decision with the position and
    /// rotation written by the same frame. It receives values only and therefore cannot mutate a
    /// Transform, path, Animator, NetworkTransform, or gameplay state.
    /// </summary>
    internal static class UnitMovementShadowObserver
    {
        private const string ObserverSchemaVersion = "b2-movement-shadow-v5";
        private const int MaximumTrackedUnits = 256;
        private const int MaximumLogLines = 768;
        private const int ReservedTerminalLogLines = 32;
        private const int MaximumNonTerminalLogLines =
            MaximumLogLines - ReservedTerminalLogLines;
        private const int CoverageManifestPayloadLimit = 700;
        private const int CoverageManifestCompletedLineByteLimit = 1000;
        private const double PeriodicSummarySeconds = 10d;
        private const float PositionDeltaTolerance = 0.00001f;
        private const float RotationDeltaToleranceDegrees = 0.001f;

        private static readonly Dictionary<ulong, UnitState> States =
            new Dictionary<ulong, UnitState>();
        private static readonly Dictionary<string, CoverageAggregate> CoverageAggregates =
            new Dictionary<string, CoverageAggregate>();
        private static readonly HashSet<string> Coverage = new HashSet<string>();
        private static readonly HashSet<string> TargetPriorityCoverage =
            new HashSet<string>();
        private static readonly HashSet<string> TargetPriorityEvidence =
            new HashSet<string>();
        private static readonly HashSet<string> PhaseEvidence =
            new HashSet<string>();
        private static readonly HashSet<string> EndpointEvidence =
            new HashSet<string>();
        private static readonly HashSet<string> MismatchEvidence =
            new HashSet<string>();

        private static NetworkManager _networkManager;
        private static bool _active;
        private static bool _isServer;
        private static bool _terminalWritten;
        private static string _runId;
        private static string _sharedSessionKey;
        private static double _startedAt;
        private static double _lastPeriodicAt;
        private static long _frameSequence;
        private static int _logCount;
        private static int _logDropCount;
        private static int _terminalEvidenceLogCount;
        private static int _terminalEvidenceOverflowCount;
        private static int _coverageManifestPreflightFailures;
        private static int _representativeDetailsSuppressed;
        private static int _reducerInvocations;
        private static int _acceptedDecisions;
        private static int _invalidDecisions;
        private static int _duplicateDecisions;
        private static int _staleDecisions;
        private static int _alignDecisions;
        private static int _moveDecisions;
        private static int _targetAcquirePriorityDecisions;
        private static int _endpointNoIntentDecisions;
        private static int _legacyMovedWhileShadowAlign;
        private static int _legacyStoppedWhileShadowMove;
        private static int _shadowMoveWriterExpectedAdvance;
        private static int _shadowMoveWriterExpectedNoAdvance;
        private static int _legacyRotationWhileNoMove;
        private static int _legacyRotationWhileEndpointNoIntent;
        private static int _illegalShadowDecisions;
        private static int _scopeErrors;
        private static int _unknownMismatches;
        private static int _clientReducerInvocations;
        private static int _identityReuseRecoveries;
        private static int _exceptionCount;

        internal static void BeginSession(NetworkManager networkManager, bool isServer)
        {
            if (_active)
                Reset();

            Reset();
            _networkManager = networkManager;
            _isServer = isServer;
            _active = networkManager != null && networkManager.IsListening;
            _runId = Guid.NewGuid().ToString("N");
            _sharedSessionKey = CreateSharedSessionKey();
            _startedAt = ReadServerTime();
            _lastPeriodicAt = _startedAt;

            if (!_active)
                return;

            Log(
                LogLevel.Info,
                "BEGIN",
                $"role={(_isServer ? "host" : "client")}, isServer={_isServer}, " +
                $"isHost={networkManager.IsHost}, isFlipped={ViewConverter.IsFlipped}, " +
                $"serverTime={FormatDouble(_startedAt)}, " +
                $"buildVersion={UnityEngine.Application.version}, " +
                $"contractSchema={UnitMovementReducer.ContractSchemaVersion}, " +
                $"observerSchema={ObserverSchemaVersion}, " +
                $"observerMode={(_isServer ? "server-read-only" : "client-sentinel")}");
        }

        internal static void RetireUnit(int unitId, ulong networkObjectId)
        {
            if (!_active || !_isServer)
                return;
            if (!States.TryGetValue(networkObjectId, out UnitState state)
                || state.UnitId != unitId)
                return;

            States.Remove(networkObjectId);
        }

        internal static LegacyFrameToken BeginLegacyFrame(
            int unitId,
            ulong networkObjectId,
            UnitType unitType,
            TeamId team,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementIntentReason intentReason,
            Vector3 positionBefore,
            Quaternion rotationBefore,
            Vector3 desiredTarget,
            bool targetAcquirePriority,
            float expectedPositionDeltaWorld)
        {
            if (!_active)
                return default;

            if (!_isServer)
            {
                _clientReducerInvocations++;
                Log(
                    LogLevel.Error,
                    "client-reducer-invocation-blocked",
                    $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                    $"intentReason={intentReason}");
                return default;
            }

            try
            {
                // NGO NetworkObjectId zero is a valid first spawned object identity.
                if (unitId < 0)
                {
                    _scopeErrors++;
                    Log(
                        LogLevel.Error,
                        "invalid-identity",
                        $"unitId={unitId}, networkObjectId={networkObjectId}");
                    return default;
                }

                if (!States.TryGetValue(networkObjectId, out UnitState state))
                {
                    if (States.Count >= MaximumTrackedUnits)
                    {
                        _scopeErrors++;
                        Log(
                            LogLevel.Error,
                            "tracking-capacity-reached",
                            $"maximumTrackedUnits={MaximumTrackedUnits}, " +
                            $"unitId={unitId}, networkObjectId={networkObjectId}");
                        return default;
                    }

                    state = new UnitState(unitId, networkObjectId, unitType, team);
                    States.Add(networkObjectId, state);
                }
                else if (state.UnitId != unitId)
                {
                    _identityReuseRecoveries++;
                    Log(
                        LogLevel.Warn,
                        "identity-scope-recovered",
                        $"unitId={unitId}, retiredUnitId={state.UnitId}, " +
                        $"networkObjectId={networkObjectId}");
                    state = new UnitState(
                        unitId,
                        networkObjectId,
                        unitType,
                        team);
                    States[networkObjectId] = state;
                }

                if (!IsFinite(expectedPositionDeltaWorld)
                    || expectedPositionDeltaWorld < 0f)
                {
                    _invalidDecisions++;
                    Log(
                        LogLevel.Error,
                        "expected-position-delta-invalid",
                        $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                        $"expectedPositionDeltaWorld=" +
                        $"{FormatFloat(expectedPositionDeltaWorld)}");
                    return default;
                }

                double serverTime = ReadServerTime();
                MovementAdapterEvaluation adapterEvaluation =
                    EvaluateMovementAdapter(
                        state.Reducer,
                        commandRevision,
                        segmentRevision,
                        intentReason,
                        positionBefore,
                        rotationBefore,
                        desiredTarget,
                        targetAcquirePriority,
                        expectedPositionDeltaWorld,
                        serverTime);
                if (adapterEvaluation.ReducerInvoked)
                    _reducerInvocations++;
                UnitMovementReducerStatus status = adapterEvaluation.Status;
                UnitMovementDecision decision = adapterEvaluation.Decision;
                CountStatus(status);
                bool acceptedOrDuplicate =
                    status == UnitMovementReducerStatus.Accepted
                    || status == UnitMovementReducerStatus.Duplicate;
                if (!acceptedOrDuplicate || !decision.IsValid)
                {
                    Log(
                        LogLevel.Error,
                        "reducer-rejected",
                        Correlation(
                            state,
                            commandRevision,
                            segmentRevision,
                            intentReason,
                            serverTime)
                        + $", reducerStatus={status}, positionXZ={FormatVector(positionBefore)}, " +
                          $"desiredTargetXZ={FormatVector(desiredTarget)}, " +
                          $"sourceIntentReason={intentReason}, " +
                          $"evaluatedIntentReason=" +
                          $"{adapterEvaluation.EvaluatedIntentReason}, " +
                          $"evaluatedHasIntent={adapterEvaluation.EvaluatedHasIntent}, " +
                          $"endpointNormalized={adapterEvaluation.EndpointNormalized}");
                    return default;
                }

                if (status == UnitMovementReducerStatus.Accepted)
                    _acceptedDecisions++;
                if (decision.Phase == UnitMovementPhase.AlignToMove)
                    _alignDecisions++;
                if (decision.Phase == UnitMovementPhase.Move)
                    _moveDecisions++;
                if (adapterEvaluation.EndpointNormalized)
                    _endpointNoIntentDecisions++;
                if ((decision.Phase == UnitMovementPhase.AlignToMove
                        || decision.Phase == UnitMovementPhase.NoIntent
                        || decision.Phase == UnitMovementPhase.Invalid)
                    && decision.AllowsMovement)
                {
                    _illegalShadowDecisions++;
                }

                long sequence = ++_frameSequence;
                state.LastIssuedFrameSequence = sequence;
                string coverageKey =
                    $"{unitType}|{team}|{intentReason}";
                Coverage.Add(coverageKey);
                CoverageAggregate coverageAggregate =
                    GetCoverageAggregate(coverageKey);
                coverageAggregate.ObservedFrames++;
                if (decision.Phase == UnitMovementPhase.AlignToMove)
                    coverageAggregate.AlignFrames++;
                if (decision.Phase == UnitMovementPhase.Move)
                    coverageAggregate.MoveFrames++;
                if (adapterEvaluation.EndpointNormalized)
                    coverageAggregate.EndpointNoIntent++;
                bool phaseChanged =
                    !state.HasLastPhase || state.LastPhase != decision.Phase;
                string previousPhase =
                    state.HasLastPhase ? state.LastPhase.ToString() : "Unobserved";
                string phaseEvidenceKey =
                    $"{intentReason}|{previousPhase}->{decision.Phase}";
                bool firstPhaseEvidence =
                    phaseChanged && PhaseEvidence.Add(phaseEvidenceKey);
                state.HasLastPhase = true;
                state.LastPhase = decision.Phase;
                state.ObservedFrames++;

                if (adapterEvaluation.EndpointNormalized
                    && EndpointEvidence.Add(intentReason.ToString()))
                {
                    Log(
                        LogLevel.Info,
                        "endpoint-no-intent",
                        Correlation(
                            state,
                            commandRevision,
                            segmentRevision,
                            intentReason,
                            serverTime)
                        + $", frameSequence={sequence}, phase={decision.Phase}, " +
                          $"positionXZ={FormatVector(positionBefore)}, " +
                          $"desiredTargetXZ={FormatVector(desiredTarget)}, " +
                          $"expectedPositionDeltaWorld=" +
                          $"{FormatFloat(expectedPositionDeltaWorld)}, " +
                          $"sourceIntentReason={intentReason}, " +
                          $"evaluatedIntentReason={adapterEvaluation.EvaluatedIntentReason}, " +
                          $"evaluatedHasIntent={adapterEvaluation.EvaluatedHasIntent}, " +
                          $"endpointNormalized={adapterEvaluation.EndpointNormalized}");
                }

                if (phaseChanged && firstPhaseEvidence)
                {
                    Log(
                        LogLevel.Info,
                        "phase-transition",
                        Correlation(
                            state,
                            commandRevision,
                            segmentRevision,
                            intentReason,
                            serverTime)
                        + $", frameSequence={sequence}, phase={decision.Phase}, " +
                          $"previousPhase={previousPhase}, " +
                          $"shadowAllowsMovement={decision.AllowsMovement}, " +
                          $"yawErrorDegrees={FormatDouble(decision.YawErrorDegrees)}, " +
                          $"positionXZ={FormatVector(positionBefore)}, " +
                          $"simulationFacingXZ=" +
                          $"{FormatDirection(adapterEvaluation.SimulationFacing)}, " +
                          $"desiredTargetXZ={FormatVector(desiredTarget)}, " +
                          $"desiredMoveDirectionXZ=" +
                          $"{FormatDirection(adapterEvaluation.EvaluatedDesiredMoveDirection)}, " +
                          $"sourceIntentReason={intentReason}, " +
                          $"evaluatedIntentReason={adapterEvaluation.EvaluatedIntentReason}, " +
                          $"evaluatedHasIntent={adapterEvaluation.EvaluatedHasIntent}, " +
                          $"endpointNormalized={adapterEvaluation.EndpointNormalized}, " +
                          $"targetAcquirePriority={targetAcquirePriority}");
                }
                else if (phaseChanged)
                {
                    _representativeDetailsSuppressed++;
                }

                MaybeWritePeriodicSummary(serverTime);
                return new LegacyFrameToken(
                    sequence,
                    networkObjectId,
                    positionBefore,
                    rotationBefore,
                    commandRevision,
                    segmentRevision,
                    intentReason,
                    decision,
                    expectedPositionDeltaWorld,
                    adapterEvaluation.EvaluatedIntentReason,
                    adapterEvaluation.EvaluatedHasIntent,
                    adapterEvaluation.SimulationFacing,
                    adapterEvaluation.EvaluatedDesiredMoveDirection,
                    adapterEvaluation.EndpointNormalized);
            }
            catch (Exception exception)
            {
                _exceptionCount++;
                Log(
                    LogLevel.Error,
                    "observer-exception",
                    $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                    $"exceptionType={exception.GetType().Name}, message={Sanitize(exception.Message)}");
                return default;
            }
        }

        internal static void CompleteLegacyFrame(
            LegacyFrameToken token,
            Vector3 positionAfter,
            Quaternion rotationAfter)
        {
            if (!_active || !token.IsValid)
                return;

            if (!States.TryGetValue(token.NetworkObjectId, out UnitState state)
                || state.LastIssuedFrameSequence != token.FrameSequence)
            {
                _scopeErrors++;
                Log(
                    LogLevel.Error,
                    "frame-scope-mismatch",
                    $"networkObjectId={token.NetworkObjectId}, " +
                    $"frameSequence={token.FrameSequence}");
                return;
            }

            if (!IsFinite(positionAfter.x)
                || !IsFinite(positionAfter.z)
                || !IsFinite(rotationAfter.x)
                || !IsFinite(rotationAfter.y)
                || !IsFinite(rotationAfter.z)
                || !IsFinite(rotationAfter.w))
            {
                _invalidDecisions++;
                Log(
                    LogLevel.Error,
                    "post-writer-pose-invalid",
                    Correlation(
                        state,
                        token.CommandRevision,
                        token.SegmentRevision,
                        token.SourceIntentReason,
                        ReadServerTime())
                    + $", frameSequence={token.FrameSequence}, " +
                      $"positionXZ={FormatVector(positionAfter)}, " +
                      $"rotation=({FormatFloat(rotationAfter.x)};" +
                      $"{FormatFloat(rotationAfter.y)};" +
                      $"{FormatFloat(rotationAfter.z)};" +
                      $"{FormatFloat(rotationAfter.w)})");
                return;
            }

            Vector2 before = new Vector2(token.PositionBefore.x, token.PositionBefore.z);
            Vector2 after = new Vector2(positionAfter.x, positionAfter.z);
            float positionDelta = Vector2.Distance(before, after);
            float rotationDelta =
                Quaternion.Angle(token.RotationBefore, rotationAfter);
            bool legacyMoved = positionDelta > PositionDeltaTolerance;
            bool legacyRotated = rotationDelta > RotationDeltaToleranceDegrees;
            string mismatch = "None";
            string coverageKey =
                $"{state.UnitType}|{state.Team}|{token.SourceIntentReason}";
            CoverageAggregate coverageAggregate =
                GetCoverageAggregate(coverageKey);

            if (token.Decision.AllowsMovement)
            {
                if (token.ExpectedPositionDeltaWorld > PositionDeltaTolerance)
                {
                    _shadowMoveWriterExpectedAdvance++;
                    coverageAggregate.WriterExpectedAdvance++;
                }
                else
                {
                    _shadowMoveWriterExpectedNoAdvance++;
                    coverageAggregate.WriterExpectedNoAdvance++;
                }
            }

            if (!token.Decision.AllowsMovement && legacyMoved)
            {
                mismatch = "LegacyMovedWhileShadowAlign";
                _legacyMovedWhileShadowAlign++;
                coverageAggregate.LegacyMovedWhileAlign++;
            }
            else if (token.ExpectedPositionDeltaWorld > PositionDeltaTolerance
                && token.Decision.AllowsMovement
                && !legacyMoved)
            {
                mismatch = "LegacyStoppedWhileShadowMove";
                _legacyStoppedWhileShadowMove++;
                coverageAggregate.LegacyStoppedWhileMove++;
            }
            else if (!token.Decision.AllowsMovement && legacyRotated)
            {
                if (token.EndpointNormalized)
                {
                    _legacyRotationWhileEndpointNoIntent++;
                    coverageAggregate.EndpointRotation++;
                }
                else
                {
                    // Rotation while aligning is expected. Keep it as classified coverage rather
                    // than an unknown mismatch or a reducer failure.
                    mismatch = "LegacyRotationWhileShadowAlign";
                    _legacyRotationWhileNoMove++;
                }
            }

            bool mismatchChanged =
                !string.Equals(state.LastMismatch, mismatch, StringComparison.Ordinal);
            if (mismatchChanged)
            {
                string previousMismatch = state.LastMismatch;
                state.LastMismatch = mismatch;
                string mismatchEvidenceKey =
                    $"{token.SourceIntentReason}|" +
                    $"{previousMismatch}->{mismatch}";
                if (MismatchEvidence.Add(mismatchEvidenceKey))
                {
                    Log(
                        mismatch == "None" ? LogLevel.Info : LogLevel.Warn,
                        mismatch == "None" ? "mismatch-cleared" : "legacy-divergence",
                        Correlation(
                            state,
                            token.CommandRevision,
                            token.SegmentRevision,
                            token.SourceIntentReason,
                            ReadServerTime())
                        + $", frameSequence={token.FrameSequence}, " +
                          $"phase={token.Decision.Phase}, " +
                          $"shadowAllowsMovement={token.Decision.AllowsMovement}, " +
                          $"yawErrorDegrees={FormatDouble(token.Decision.YawErrorDegrees)}, " +
                          $"legacyPositionDeltaWorld={FormatFloat(positionDelta)}, " +
                          $"expectedPositionDeltaWorld=" +
                          $"{FormatFloat(token.ExpectedPositionDeltaWorld)}, " +
                          $"legacyRotationDeltaDegrees={FormatFloat(rotationDelta)}, " +
                          $"legacyMoved={legacyMoved}, mismatchKind={mismatch}");
                }
                else
                {
                    _representativeDetailsSuppressed++;
                }
            }

            state.CompletedFrames++;
            state.LastCompletedFrameSequence = token.FrameSequence;
            state.LastLegacyMoved = legacyMoved;
            state.LastLegacyPositionDelta = positionDelta;
        }

        internal static void ObserveTargetAcquirePriority(LegacyFrameToken token)
        {
            if (!_active || !_isServer || !token.IsValid)
                return;
            if (!States.TryGetValue(token.NetworkObjectId, out UnitState state)
                || state.LastCompletedFrameSequence != token.FrameSequence)
            {
                _scopeErrors++;
                return;
            }

            _reducerInvocations++;
            UnitMovementReducerStatus status = state.Reducer.Evaluate(
                state.Reducer.Snapshot.Revision,
                token.CommandRevision,
                token.SegmentRevision,
                token.EvaluatedIntentReason,
                token.EvaluatedHasIntent,
                token.EvaluatedSimulationFacing,
                token.EvaluatedDesiredMoveDirection,
                targetAcquirePriority: true,
                ReadServerTime(),
                out UnitMovementDecision decision);
            CountStatus(status);
            if (status != UnitMovementReducerStatus.Accepted
                || !decision.IsValid
                || decision.Phase != UnitMovementPhase.NoIntent
                || decision.AllowsMovement
                || !decision.IsTargetAcquirePriority)
            {
                _illegalShadowDecisions++;
                Log(
                    LogLevel.Error,
                    "target-acquire-priority-failed",
                    Correlation(
                        state,
                        token.CommandRevision,
                        token.SegmentRevision,
                        token.SourceIntentReason,
                        ReadServerTime())
                    + $", reducerStatus={status}, phase={decision.Phase}, " +
                      $"shadowAllowsMovement={decision.AllowsMovement}");
                return;
            }

            _acceptedDecisions++;
            _targetAcquirePriorityDecisions++;
            string coverageKey =
                $"{state.UnitType}|{state.Team}|{token.SourceIntentReason}";
            GetCoverageAggregate(coverageKey).TargetAcquirePriority++;
            bool firstTargetCoverage = TargetPriorityCoverage.Add(coverageKey);
            bool firstTargetEvidence =
                TargetPriorityEvidence.Add(token.SourceIntentReason.ToString());
            if (firstTargetEvidence)
            {
                Log(
                    LogLevel.Info,
                    "target-acquire-priority",
                    Correlation(
                        state,
                        token.CommandRevision,
                        token.SegmentRevision,
                        token.SourceIntentReason,
                        ReadServerTime())
                    + $", frameSequence={token.FrameSequence}, phase={decision.Phase}, " +
                      $"shadowAllowsMovement={decision.AllowsMovement}, " +
                      $"sourceIntentReason={token.SourceIntentReason}, " +
                      $"evaluatedIntentReason={token.EvaluatedIntentReason}, " +
                      $"evaluatedHasIntent={token.EvaluatedHasIntent}, " +
                      $"endpointNormalized={token.EndpointNormalized}, " +
                      $"legacyMovedBeforeAcquire={state.LastLegacyMoved}, " +
                      $"legacyPositionDeltaWorld={FormatFloat(state.LastLegacyPositionDelta)}");
            }
            else if (firstTargetCoverage)
            {
                _representativeDetailsSuppressed++;
            }
        }

        internal static void EndSession(string reason)
        {
            if (!_active || _terminalWritten)
            {
                Reset();
                return;
            }

            _terminalWritten = true;
            var orderedCoverage = new List<string>(Coverage);
            for (int index = 0; index < orderedCoverage.Count; index++)
                orderedCoverage[index] = "coverage|" + orderedCoverage[index];
            foreach (string targetPriority in TargetPriorityCoverage)
                orderedCoverage.Add("target-priority|" + targetPriority);
            foreach (KeyValuePair<string, CoverageAggregate> pair in CoverageAggregates)
                orderedCoverage.Add($"stats|{pair.Key}|{pair.Value.ToManifestEntry()}");
            orderedCoverage.Sort(StringComparer.Ordinal);
            string coverageManifest = orderedCoverage.Count == 0
                ? "none"
                : string.Join(";", orderedCoverage);
            string coverageSha256 = ComputeSha256Hex(coverageManifest);
            WriteCoverageChunks(orderedCoverage);
            LogMandatoryEvidence(
                LogLevel.Info,
                "decision-summary",
                $"reducerInvocations={_reducerInvocations}, " +
                $"acceptedDecisions={_acceptedDecisions}, " +
                $"invalidDecisions={_invalidDecisions}, " +
                $"duplicateDecisions={_duplicateDecisions}, " +
                $"staleDecisions={_staleDecisions}, alignDecisions={_alignDecisions}, " +
                $"moveDecisions={_moveDecisions}, " +
                $"endpointNoIntentDecisions={_endpointNoIntentDecisions}, " +
                $"targetAcquirePriorityDecisions={_targetAcquirePriorityDecisions}");
            LogMandatoryEvidence(
                LogLevel.Info,
                "writer-summary",
                $"legacyMovedWhileShadowAlign={_legacyMovedWhileShadowAlign}, " +
                $"legacyStoppedWhileShadowMove={_legacyStoppedWhileShadowMove}, " +
                $"shadowMoveWriterExpectedAdvance={_shadowMoveWriterExpectedAdvance}, " +
                $"shadowMoveWriterExpectedNoAdvance={_shadowMoveWriterExpectedNoAdvance}, " +
                $"legacyRotationWhileShadowAlign={_legacyRotationWhileNoMove}, " +
                $"legacyRotationWhileEndpointNoIntent=" +
                $"{_legacyRotationWhileEndpointNoIntent}, " +
                $"endpointNoIntentDecisions={_endpointNoIntentDecisions}, " +
                $"phaseEvidenceCount={PhaseEvidence.Count}, " +
                $"mismatchEvidenceCount={MismatchEvidence.Count}, " +
                $"representativeDetailsSuppressed={_representativeDetailsSuppressed}");
            LogFinalTerminal(
                HasTerminalFailure() ? LogLevel.Error : LogLevel.Info,
                "END",
                $"reason={reason}, role={(_isServer ? "host" : "client")}, " +
                $"serverTime={FormatDouble(ReadServerTime())}, trackedUnits={States.Count}, " +
                $"coverageCount={Coverage.Count}, " +
                $"manifestEntryCount={orderedCoverage.Count}, " +
                $"coverageSha256={coverageSha256}, " +
                $"invalidDecisions={_invalidDecisions}, duplicateDecisions={_duplicateDecisions}, " +
                $"staleDecisions={_staleDecisions}, illegalShadowDecisions={_illegalShadowDecisions}, " +
                $"endpointNoIntentDecisions={_endpointNoIntentDecisions}, " +
                $"scopeErrors={_scopeErrors}, unknownMismatches={_unknownMismatches}, " +
                $"clientReducerInvocations={_clientReducerInvocations}, " +
                $"identityReuseRecoveries={_identityReuseRecoveries}, " +
                $"simulationRootWriteAttempts=0, exceptions={_exceptionCount}, " +
                $"droppedLogs={_logDropCount}, " +
                $"coverageManifestPreflightFailures={_coverageManifestPreflightFailures}, " +
                $"terminalEvidenceOverflow={_terminalEvidenceOverflowCount}, " +
                $"verdict={(HasTerminalFailure() ? "FAIL" : "EVIDENCE")}");
            Reset();
        }

        private static MovementAdapterEvaluation EvaluateMovementAdapter(
            UnitMovementReducer reducer,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementIntentReason sourceIntentReason,
            Vector3 position,
            Quaternion rotation,
            Vector3 desiredTarget,
            bool targetAcquirePriority,
            float expectedPositionDeltaWorld,
            double serverTime)
        {
            Vector3 facingVector = rotation * Vector3.forward;
            UnitMovementEvaluation evaluation =
                UnitMovementEvaluationAdapter.Evaluate(
                reducer,
                commandRevision,
                segmentRevision,
                sourceIntentReason,
                position.x,
                position.z,
                facingVector.x,
                facingVector.z,
                desiredTarget.x,
                desiredTarget.z,
                targetAcquirePriority,
                expectedPositionDeltaWorld,
                serverTime);
            return new MovementAdapterEvaluation(
                evaluation.Status,
                evaluation.Decision,
                evaluation.EvaluatedIntentReason,
                evaluation.EvaluatedHasIntent,
                evaluation.SimulationFacing,
                evaluation.DesiredMoveDirection,
                evaluation.EndpointNormalized,
                evaluation.ReducerInvoked);
        }

        internal static EndpointAdapterValidationResult
            EvaluateEndpointAdapterForValidation(
                float positionX,
                float positionZ,
                float desiredTargetX,
                float desiredTargetZ,
                float expectedPositionDeltaWorld,
                bool observeTargetAcquirePriority)
        {
            var reducer = new UnitMovementReducer();
            MovementAdapterEvaluation initial = EvaluateMovementAdapter(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                new Vector3(positionX, 0f, positionZ),
                Quaternion.identity,
                new Vector3(desiredTargetX, 0f, desiredTargetZ),
                targetAcquirePriority: false,
                expectedPositionDeltaWorld,
                serverTime: 1d);
            UnitMovementSnapshot initialSnapshot = reducer.Snapshot;

            UnitMovementReducerStatus targetStatus =
                UnitMovementReducerStatus.InvalidInput;
            UnitMovementDecision targetDecision = default;
            UnitMovementSnapshot targetSnapshot = default;
            if (observeTargetAcquirePriority
                && (initial.Status == UnitMovementReducerStatus.Accepted
                    || initial.Status == UnitMovementReducerStatus.Duplicate)
                && initial.Decision.IsValid)
            {
                targetStatus = reducer.Evaluate(
                    reducer.Snapshot.Revision,
                    1UL,
                    1UL,
                    initial.EvaluatedIntentReason,
                    initial.EvaluatedHasIntent,
                    initial.SimulationFacing,
                    initial.EvaluatedDesiredMoveDirection,
                    targetAcquirePriority: true,
                    observedServerTime: 2d,
                    out targetDecision);
                targetSnapshot = reducer.Snapshot;
            }

            return new EndpointAdapterValidationResult(
                initial,
                initialSnapshot,
                targetStatus,
                targetDecision,
                targetSnapshot);
        }

        internal static EndpointLifecycleValidationResult
            EvaluateEndpointLifecycleForValidation()
        {
            var reducer = new UnitMovementReducer();
            Vector3 origin = Vector3.zero;
            Quaternion identity = Quaternion.identity;
            MovementAdapterEvaluation move = EvaluateMovementAdapter(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                origin,
                identity,
                Vector3.forward,
                false,
                0.1f,
                1d);
            ulong revisionAfterMove = reducer.Snapshot.Revision;
            MovementAdapterEvaluation endpoint = EvaluateMovementAdapter(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                new Vector3(4.5f, 0f, -12.99f),
                identity,
                new Vector3(4.5f, 0f, -12.99f),
                false,
                0f,
                2d);
            ulong revisionAfterEndpoint = reducer.Snapshot.Revision;
            MovementAdapterEvaluation duplicateEndpoint = EvaluateMovementAdapter(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                new Vector3(4.5f, 0f, -12.99f),
                identity,
                new Vector3(4.5f, 0f, -12.99f),
                false,
                0f,
                2d);
            ulong revisionAfterDuplicate = reducer.Snapshot.Revision;
            UnitMovementReducerStatus priorityStatus = reducer.Evaluate(
                reducer.Snapshot.Revision,
                1UL,
                1UL,
                endpoint.EvaluatedIntentReason,
                endpoint.EvaluatedHasIntent,
                endpoint.SimulationFacing,
                endpoint.EvaluatedDesiredMoveDirection,
                true,
                3d,
                out UnitMovementDecision priorityDecision);
            ulong revisionAfterPriority = reducer.Snapshot.Revision;
            double twelveDegrees = 12d * (Math.PI / 180d);
            MovementAdapterEvaluation resumeAlign = EvaluateMovementAdapter(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                origin,
                identity,
                new Vector3(
                    (float)Math.Sin(twelveDegrees),
                    0f,
                    (float)Math.Cos(twelveDegrees)),
                false,
                0.1f,
                4d);
            ulong revisionAfterResumeAlign = reducer.Snapshot.Revision;
            double nineDegrees = 9d * (Math.PI / 180d);
            MovementAdapterEvaluation resumeMove = EvaluateMovementAdapter(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                origin,
                identity,
                new Vector3(
                    (float)Math.Sin(nineDegrees),
                    0f,
                    (float)Math.Cos(nineDegrees)),
                false,
                0.1f,
                5d);

            return new EndpointLifecycleValidationResult(
                move,
                endpoint,
                duplicateEndpoint,
                priorityStatus,
                priorityDecision,
                resumeAlign,
                resumeMove,
                revisionAfterMove,
                revisionAfterEndpoint,
                revisionAfterDuplicate,
                revisionAfterPriority,
                revisionAfterResumeAlign,
                reducer.Snapshot.Revision,
                reducer.Snapshot.CommandRevision,
                reducer.Snapshot.SegmentRevision);
        }

        internal static float PositionDeltaToleranceForValidation
            => PositionDeltaTolerance;

        private static void CountStatus(UnitMovementReducerStatus status)
        {
            switch (status)
            {
                case UnitMovementReducerStatus.Accepted:
                    return;
                case UnitMovementReducerStatus.Duplicate:
                    _duplicateDecisions++;
                    return;
                case UnitMovementReducerStatus.StaleRevision:
                case UnitMovementReducerStatus.StaleCommandRevision:
                case UnitMovementReducerStatus.StaleSegmentRevision:
                    _staleDecisions++;
                    return;
                default:
                    _invalidDecisions++;
                    return;
            }
        }

        private static bool HasTerminalFailure()
        {
            return _invalidDecisions > 0
                || _staleDecisions > 0
                || _illegalShadowDecisions > 0
                || _scopeErrors > 0
                || _unknownMismatches > 0
                || _clientReducerInvocations > 0
                || _exceptionCount > 0
                || _logDropCount > 0
                || _coverageManifestPreflightFailures > 0
                || _terminalEvidenceOverflowCount > 0;
        }

        private static void MaybeWritePeriodicSummary(double serverTime)
        {
            if (serverTime - _lastPeriodicAt < PeriodicSummarySeconds)
                return;

            _lastPeriodicAt = serverTime;
            Log(
                LogLevel.Info,
                "periodic-summary",
                $"serverTime={FormatDouble(serverTime)}, trackedUnits={States.Count}, " +
                $"coverageCount={Coverage.Count}, reducerInvocations={_reducerInvocations}, " +
                $"acceptedDecisions={_acceptedDecisions}, alignDecisions={_alignDecisions}, " +
                $"moveDecisions={_moveDecisions}, " +
                $"targetAcquirePriorityDecisions={_targetAcquirePriorityDecisions}, " +
                $"legacyMovedWhileShadowAlign={_legacyMovedWhileShadowAlign}, " +
                $"legacyStoppedWhileShadowMove={_legacyStoppedWhileShadowMove}, " +
                $"shadowMoveWriterExpectedAdvance={_shadowMoveWriterExpectedAdvance}, " +
                $"shadowMoveWriterExpectedNoAdvance={_shadowMoveWriterExpectedNoAdvance}, " +
                $"illegalShadowDecisions={_illegalShadowDecisions}, scopeErrors={_scopeErrors}, " +
                $"unknownMismatches={_unknownMismatches}, droppedLogs={_logDropCount}");
        }

        private static string Correlation(
            UnitState state,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementIntentReason intentReason,
            double serverTime)
        {
            return
                $"unitId={state.UnitId}, networkObjectId={state.NetworkObjectId}, " +
                $"unitType={state.UnitType}, team={state.Team}, " +
                $"commandRevision={commandRevision}, segmentRevision={segmentRevision}, " +
                $"intentReason={intentReason}, serverTime={FormatDouble(serverTime)}, " +
                $"unityFrame={Time.frameCount}";
        }

        private static CoverageAggregate GetCoverageAggregate(string coverageKey)
        {
            if (!CoverageAggregates.TryGetValue(
                coverageKey,
                out CoverageAggregate aggregate))
            {
                aggregate = new CoverageAggregate();
                CoverageAggregates.Add(coverageKey, aggregate);
            }

            return aggregate;
        }

        private static void Log(LogLevel level, string message, string data)
        {
            if (_logCount >= MaximumNonTerminalLogLines)
            {
                _logDropCount++;
                return;
            }

            _logCount++;
            RuntimeLogger.Log(
                level,
                "Network",
                nameof(UnitMovementShadowObserver),
                $"[UAS-MOVE-SHADOW] {message}",
                Decorate(data));
        }

        private static void LogMandatoryEvidence(
            LogLevel level,
            string message,
            string data)
        {
            if (_terminalEvidenceLogCount >= ReservedTerminalLogLines - 1)
            {
                _terminalEvidenceOverflowCount++;
                return;
            }

            _terminalEvidenceLogCount++;
            _logCount++;
            RuntimeLogger.Log(
                level,
                "Network",
                nameof(UnitMovementShadowObserver),
                $"[UAS-MOVE-SHADOW] {message}",
                Decorate(data));
        }

        private static void LogFinalTerminal(
            LogLevel level,
            string message,
            string data)
        {
            _terminalEvidenceLogCount++;
            _logCount++;
            RuntimeLogger.Log(
                level,
                "Network",
                nameof(UnitMovementShadowObserver),
                $"[UAS-MOVE-SHADOW] {message}",
                Decorate(data));
        }

        private static string Decorate(string data)
        {
            return
                $"runId={_runId}, sharedSessionKey={_sharedSessionKey}, " +
                $"sessionStartedAt={FormatDouble(_startedAt)}, {data}";
        }

        private static double ReadServerTime()
        {
            if (_networkManager == null || !_networkManager.IsListening)
                return double.NaN;
            return _networkManager.ServerTime.Time;
        }

        private static string CreateSharedSessionKey()
        {
            NetworkGameManager gameManager =
                UnityEngine.Object.FindFirstObjectByType<NetworkGameManager>();
            string lobbyId = gameManager?.CurrentLobby?.Id;
            if (string.IsNullOrEmpty(lobbyId))
                return "unavailable";

            byte[] lobbyBytes = Encoding.UTF8.GetBytes(lobbyId);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(lobbyBytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static void WriteCoverageChunks(
            IReadOnlyList<string> orderedCoverage)
        {
            IReadOnlyList<string> payloads;
            try
            {
                payloads = BuildCoverageManifestPayloads(
                    orderedCoverage);
                for (int index = 0; index < payloads.Count; index++)
                {
                    if (GetCompletedCoverageManifestLineByteCount(payloads[index])
                        >= CoverageManifestCompletedLineByteLimit)
                    {
                        throw new InvalidOperationException(
                            "completed coverage-manifest line exceeded its UTF-8 byte limit");
                    }
                }
            }
            catch (InvalidOperationException exception)
            {
                _coverageManifestPreflightFailures++;
                LogMandatoryEvidence(
                    LogLevel.Error,
                    "coverage-manifest-preflight-failed",
                    $"entryCount={orderedCoverage.Count}, " +
                    $"reason={Sanitize(exception.Message)}");
                return;
            }

            for (int index = 0; index < payloads.Count; index++)
            {
                LogMandatoryEvidence(
                    LogLevel.Info,
                    "coverage-manifest",
                    payloads[index]);
            }
        }

        internal static IReadOnlyList<string> BuildCoverageManifestPayloadsForValidation(
            IReadOnlyList<string> orderedCoverage)
        {
            return BuildCoverageManifestPayloads(orderedCoverage);
        }

        internal static string BuildCoverageManifestCompletedLineForValidation(
            string payload)
        {
            return BuildCoverageManifestCompletedLine(
                payload,
                new string('f', 32),
                new string('f', 64),
                "1234567890.123456");
        }

        internal static int CoverageManifestPayloadLimitForValidation
            => CoverageManifestPayloadLimit;

        internal static int MaximumCoverageManifestChunksForValidation
            => ReservedTerminalLogLines - 3;

        private static IReadOnlyList<string> BuildCoverageManifestPayloads(
            IReadOnlyList<string> orderedCoverage)
        {
            if (orderedCoverage == null)
                throw new InvalidOperationException("coverage entries are null");

            int assumedChunkCount = 1;
            int maximumChunks = ReservedTerminalLogLines - 3;
            for (int attempt = 0; attempt < maximumChunks; attempt++)
            {
                List<string> entryChunks = PackCoverageEntries(
                    orderedCoverage,
                    assumedChunkCount);
                if (entryChunks.Count > maximumChunks)
                {
                    throw new InvalidOperationException(
                        "coverage manifest exceeds reserved terminal log capacity");
                }

                if (entryChunks.Count != assumedChunkCount)
                {
                    assumedChunkCount = entryChunks.Count;
                    continue;
                }

                var payloads = new List<string>(entryChunks.Count);
                for (int index = 0; index < entryChunks.Count; index++)
                {
                    string payload = BuildCoverageManifestPayload(
                        index + 1,
                        entryChunks.Count,
                        entryChunks[index]);
                    if (payload.Length > CoverageManifestPayloadLimit
                        || Encoding.UTF8.GetByteCount(payload)
                            > CoverageManifestPayloadLimit)
                    {
                        throw new InvalidOperationException(
                            "coverage manifest payload exceeded its character or UTF-8 byte limit");
                    }

                    payloads.Add(payload);
                }

                return payloads;
            }

            throw new InvalidOperationException(
                "coverage manifest chunk count did not converge");
        }

        private static List<string> PackCoverageEntries(
            IReadOnlyList<string> orderedCoverage,
            int assumedChunkCount)
        {
            if (orderedCoverage.Count == 0)
            {
                return new List<string>
                {
                    "none"
                };
            }

            var chunks = new List<string>();
            var current = new StringBuilder();
            for (int index = 0; index < orderedCoverage.Count; index++)
            {
                string entry = orderedCoverage[index];
                if (string.IsNullOrEmpty(entry)
                    || entry.IndexOf(';') >= 0
                    || entry.IndexOf('\r') >= 0
                    || entry.IndexOf('\n') >= 0)
                {
                    throw new InvalidOperationException(
                        "coverage manifest contains an invalid entry");
                }

                string candidate = current.Length == 0
                    ? entry
                    : current + ";" + entry;
                string candidatePayload = BuildCoverageManifestPayload(
                    chunks.Count + 1,
                    assumedChunkCount,
                    candidate);
                bool fits =
                    candidatePayload.Length <= CoverageManifestPayloadLimit
                    && Encoding.UTF8.GetByteCount(candidatePayload)
                        <= CoverageManifestPayloadLimit;
                if (fits)
                {
                    current.Clear();
                    current.Append(candidate);
                    continue;
                }

                if (current.Length == 0)
                {
                    throw new InvalidOperationException(
                        "a coverage manifest entry exceeds the payload limit");
                }

                chunks.Add(current.ToString());
                current.Clear();
                current.Append(entry);
                string singleEntryPayload = BuildCoverageManifestPayload(
                    chunks.Count + 1,
                    assumedChunkCount,
                    current.ToString());
                if (singleEntryPayload.Length > CoverageManifestPayloadLimit
                    || Encoding.UTF8.GetByteCount(singleEntryPayload)
                        > CoverageManifestPayloadLimit)
                {
                    throw new InvalidOperationException(
                        "a coverage manifest entry exceeds the payload limit");
                }
            }

            if (current.Length > 0)
                chunks.Add(current.ToString());
            return chunks;
        }

        private static string BuildCoverageManifestPayload(
            int chunkIndex,
            int chunkCount,
            string entries)
        {
            int payloadCharacters = 0;
            int payloadBytes = 0;
            string payload = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                payload =
                    $"chunk={chunkIndex}/{chunkCount}, " +
                    $"payloadChars={payloadCharacters}, " +
                    $"payloadUtf8Bytes={payloadBytes}, entries={entries}";
                int actualCharacters = payload.Length;
                int actualBytes = Encoding.UTF8.GetByteCount(payload);
                if (actualCharacters == payloadCharacters
                    && actualBytes == payloadBytes)
                {
                    return payload;
                }

                payloadCharacters = actualCharacters;
                payloadBytes = actualBytes;
            }

            throw new InvalidOperationException(
                "coverage manifest payload metadata did not converge");
        }

        private static int GetCompletedCoverageManifestLineByteCount(string payload)
        {
            string completedLine = BuildCoverageManifestCompletedLine(
                payload,
                _runId,
                _sharedSessionKey,
                FormatDouble(_startedAt));
            return Encoding.UTF8.GetByteCount(completedLine);
        }

        private static string BuildCoverageManifestCompletedLine(
            string payload,
            string runId,
            string sharedSessionKey,
            string sessionStartedAt)
        {
            string decorated =
                $"runId={runId}, sharedSessionKey={sharedSessionKey}, " +
                $"sessionStartedAt={sessionStartedAt}, {payload}";
            return
                "[23:59:59.999] [INFO] " +
                "[Network/UnitMovementShadowObserver] " +
                "[UAS-MOVE-SHADOW] coverage-manifest | " +
                decorated;
        }

        private static string ComputeSha256Hex(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte item in hash)
                    builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({FormatFloat(value.x)};{FormatFloat(value.z)})";
        }

        private static string FormatDirection(ActionDirectionXZ value)
        {
            return $"({FormatDouble(value.X)};{FormatDouble(value.Z)})";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string FormatDouble(double value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "none"
                : value.Replace("\r", " ").Replace("\n", " ");
        }

        private static void Reset()
        {
            States.Clear();
            CoverageAggregates.Clear();
            Coverage.Clear();
            TargetPriorityCoverage.Clear();
            TargetPriorityEvidence.Clear();
            PhaseEvidence.Clear();
            EndpointEvidence.Clear();
            MismatchEvidence.Clear();
            _networkManager = null;
            _active = false;
            _isServer = false;
            _terminalWritten = false;
            _runId = null;
            _sharedSessionKey = null;
            _startedAt = 0d;
            _lastPeriodicAt = 0d;
            _frameSequence = 0L;
            _logCount = 0;
            _logDropCount = 0;
            _terminalEvidenceLogCount = 0;
            _terminalEvidenceOverflowCount = 0;
            _coverageManifestPreflightFailures = 0;
            _representativeDetailsSuppressed = 0;
            _reducerInvocations = 0;
            _acceptedDecisions = 0;
            _invalidDecisions = 0;
            _duplicateDecisions = 0;
            _staleDecisions = 0;
            _alignDecisions = 0;
            _moveDecisions = 0;
            _targetAcquirePriorityDecisions = 0;
            _endpointNoIntentDecisions = 0;
            _legacyMovedWhileShadowAlign = 0;
            _legacyStoppedWhileShadowMove = 0;
            _shadowMoveWriterExpectedAdvance = 0;
            _shadowMoveWriterExpectedNoAdvance = 0;
            _legacyRotationWhileNoMove = 0;
            _legacyRotationWhileEndpointNoIntent = 0;
            _illegalShadowDecisions = 0;
            _scopeErrors = 0;
            _unknownMismatches = 0;
            _clientReducerInvocations = 0;
            _identityReuseRecoveries = 0;
            _exceptionCount = 0;
        }

        internal readonly struct MovementAdapterEvaluation
        {
            internal UnitMovementReducerStatus Status { get; }
            internal UnitMovementDecision Decision { get; }
            internal UnitMovementIntentReason EvaluatedIntentReason { get; }
            internal bool EvaluatedHasIntent { get; }
            internal ActionDirectionXZ SimulationFacing { get; }
            internal ActionDirectionXZ EvaluatedDesiredMoveDirection { get; }
            internal bool EndpointNormalized { get; }
            internal bool ReducerInvoked { get; }

            internal MovementAdapterEvaluation(
                UnitMovementReducerStatus status,
                UnitMovementDecision decision,
                UnitMovementIntentReason evaluatedIntentReason,
                bool evaluatedHasIntent,
                ActionDirectionXZ simulationFacing,
                ActionDirectionXZ evaluatedDesiredMoveDirection,
                bool endpointNormalized,
                bool reducerInvoked)
            {
                Status = status;
                Decision = decision;
                EvaluatedIntentReason = evaluatedIntentReason;
                EvaluatedHasIntent = evaluatedHasIntent;
                SimulationFacing = simulationFacing;
                EvaluatedDesiredMoveDirection = evaluatedDesiredMoveDirection;
                EndpointNormalized = endpointNormalized;
                ReducerInvoked = reducerInvoked;
            }

            internal static MovementAdapterEvaluation Invalid()
                => new MovementAdapterEvaluation(
                    UnitMovementReducerStatus.InvalidInput,
                    default,
                    UnitMovementIntentReason.None,
                    false,
                    default,
                    default,
                    false,
                    false);
        }

        internal readonly struct EndpointAdapterValidationResult
        {
            internal UnitMovementReducerStatus Status { get; }
            internal UnitMovementPhase Phase { get; }
            internal bool DecisionIsValid { get; }
            internal UnitMovementIntentReason EvaluatedIntentReason { get; }
            internal bool EvaluatedHasIntent { get; }
            internal bool EvaluatedDesiredDirectionIsValid { get; }
            internal bool EndpointNormalized { get; }
            internal ulong RevisionAfterInitial { get; }
            internal ulong CommandRevisionAfterInitial { get; }
            internal ulong SegmentRevisionAfterInitial { get; }
            internal UnitMovementIntentReason SnapshotIntentReason { get; }
            internal bool SnapshotHasIntent { get; }
            internal UnitMovementReducerStatus TargetStatus { get; }
            internal UnitMovementPhase TargetPhase { get; }
            internal bool TargetDecisionIsValid { get; }
            internal bool TargetAcquirePriority { get; }
            internal UnitMovementIntentReason TargetSnapshotIntentReason { get; }
            internal bool TargetSnapshotHasIntent { get; }
            internal bool TargetSnapshotDesiredDirectionIsValid { get; }

            internal EndpointAdapterValidationResult(
                MovementAdapterEvaluation initial,
                UnitMovementSnapshot initialSnapshot,
                UnitMovementReducerStatus targetStatus,
                UnitMovementDecision targetDecision,
                UnitMovementSnapshot targetSnapshot)
            {
                Status = initial.Status;
                Phase = initial.Decision.Phase;
                DecisionIsValid = initial.Decision.IsValid;
                EvaluatedIntentReason = initial.EvaluatedIntentReason;
                EvaluatedHasIntent = initial.EvaluatedHasIntent;
                EvaluatedDesiredDirectionIsValid =
                    initial.EvaluatedDesiredMoveDirection.IsValid;
                EndpointNormalized = initial.EndpointNormalized;
                RevisionAfterInitial = initialSnapshot.Revision;
                CommandRevisionAfterInitial = initialSnapshot.CommandRevision;
                SegmentRevisionAfterInitial = initialSnapshot.SegmentRevision;
                SnapshotIntentReason = initialSnapshot.IntentReason;
                SnapshotHasIntent = initialSnapshot.HasIntent;
                TargetStatus = targetStatus;
                TargetPhase = targetDecision.Phase;
                TargetDecisionIsValid = targetDecision.IsValid;
                TargetAcquirePriority =
                    targetDecision.IsTargetAcquirePriority;
                TargetSnapshotIntentReason = targetSnapshot.IntentReason;
                TargetSnapshotHasIntent = targetSnapshot.HasIntent;
                TargetSnapshotDesiredDirectionIsValid =
                    targetSnapshot.DesiredMoveDirection.IsValid;
            }
        }

        internal readonly struct EndpointLifecycleValidationResult
        {
            internal UnitMovementReducerStatus MoveStatus { get; }
            internal UnitMovementPhase MovePhase { get; }
            internal UnitMovementReducerStatus EndpointStatus { get; }
            internal UnitMovementPhase EndpointPhase { get; }
            internal bool EndpointNormalized { get; }
            internal UnitMovementIntentReason EndpointEvaluatedIntentReason { get; }
            internal bool EndpointEvaluatedHasIntent { get; }
            internal UnitMovementReducerStatus DuplicateEndpointStatus { get; }
            internal UnitMovementReducerStatus PriorityStatus { get; }
            internal UnitMovementPhase PriorityPhase { get; }
            internal bool PriorityIsTargetAcquire { get; }
            internal UnitMovementReducerStatus ResumeAlignStatus { get; }
            internal UnitMovementPhase ResumeAlignPhase { get; }
            internal double ResumeAlignYawErrorDegrees { get; }
            internal UnitMovementReducerStatus ResumeMoveStatus { get; }
            internal UnitMovementPhase ResumeMovePhase { get; }
            internal double ResumeMoveYawErrorDegrees { get; }
            internal ulong RevisionAfterMove { get; }
            internal ulong RevisionAfterEndpoint { get; }
            internal ulong RevisionAfterDuplicate { get; }
            internal ulong RevisionAfterPriority { get; }
            internal ulong RevisionAfterResumeAlign { get; }
            internal ulong RevisionAfterResumeMove { get; }
            internal ulong FinalCommandRevision { get; }
            internal ulong FinalSegmentRevision { get; }

            internal EndpointLifecycleValidationResult(
                MovementAdapterEvaluation move,
                MovementAdapterEvaluation endpoint,
                MovementAdapterEvaluation duplicateEndpoint,
                UnitMovementReducerStatus priorityStatus,
                UnitMovementDecision priorityDecision,
                MovementAdapterEvaluation resumeAlign,
                MovementAdapterEvaluation resumeMove,
                ulong revisionAfterMove,
                ulong revisionAfterEndpoint,
                ulong revisionAfterDuplicate,
                ulong revisionAfterPriority,
                ulong revisionAfterResumeAlign,
                ulong revisionAfterResumeMove,
                ulong finalCommandRevision,
                ulong finalSegmentRevision)
            {
                MoveStatus = move.Status;
                MovePhase = move.Decision.Phase;
                EndpointStatus = endpoint.Status;
                EndpointPhase = endpoint.Decision.Phase;
                EndpointNormalized = endpoint.EndpointNormalized;
                EndpointEvaluatedIntentReason =
                    endpoint.EvaluatedIntentReason;
                EndpointEvaluatedHasIntent = endpoint.EvaluatedHasIntent;
                DuplicateEndpointStatus = duplicateEndpoint.Status;
                PriorityStatus = priorityStatus;
                PriorityPhase = priorityDecision.Phase;
                PriorityIsTargetAcquire =
                    priorityDecision.IsTargetAcquirePriority;
                ResumeAlignStatus = resumeAlign.Status;
                ResumeAlignPhase = resumeAlign.Decision.Phase;
                ResumeAlignYawErrorDegrees =
                    resumeAlign.Decision.YawErrorDegrees;
                ResumeMoveStatus = resumeMove.Status;
                ResumeMovePhase = resumeMove.Decision.Phase;
                ResumeMoveYawErrorDegrees =
                    resumeMove.Decision.YawErrorDegrees;
                RevisionAfterMove = revisionAfterMove;
                RevisionAfterEndpoint = revisionAfterEndpoint;
                RevisionAfterDuplicate = revisionAfterDuplicate;
                RevisionAfterPriority = revisionAfterPriority;
                RevisionAfterResumeAlign = revisionAfterResumeAlign;
                RevisionAfterResumeMove = revisionAfterResumeMove;
                FinalCommandRevision = finalCommandRevision;
                FinalSegmentRevision = finalSegmentRevision;
            }
        }

        internal readonly struct LegacyFrameToken
        {
            internal long FrameSequence { get; }
            internal ulong NetworkObjectId { get; }
            internal Vector3 PositionBefore { get; }
            internal Quaternion RotationBefore { get; }
            internal ulong CommandRevision { get; }
            internal ulong SegmentRevision { get; }
            internal UnitMovementIntentReason SourceIntentReason { get; }
            internal UnitMovementDecision Decision { get; }
            internal float ExpectedPositionDeltaWorld { get; }
            internal UnitMovementIntentReason EvaluatedIntentReason { get; }
            internal bool EvaluatedHasIntent { get; }
            internal ActionDirectionXZ EvaluatedSimulationFacing { get; }
            internal ActionDirectionXZ EvaluatedDesiredMoveDirection { get; }
            internal bool EndpointNormalized { get; }
            internal bool IsValid => FrameSequence > 0L;

            internal LegacyFrameToken(
                long frameSequence,
                ulong networkObjectId,
                Vector3 positionBefore,
                Quaternion rotationBefore,
                ulong commandRevision,
                ulong segmentRevision,
                UnitMovementIntentReason sourceIntentReason,
                UnitMovementDecision decision,
                float expectedPositionDeltaWorld,
                UnitMovementIntentReason evaluatedIntentReason,
                bool evaluatedHasIntent,
                ActionDirectionXZ evaluatedSimulationFacing,
                ActionDirectionXZ evaluatedDesiredMoveDirection,
                bool endpointNormalized)
            {
                FrameSequence = frameSequence;
                NetworkObjectId = networkObjectId;
                PositionBefore = positionBefore;
                RotationBefore = rotationBefore;
                CommandRevision = commandRevision;
                SegmentRevision = segmentRevision;
                SourceIntentReason = sourceIntentReason;
                Decision = decision;
                ExpectedPositionDeltaWorld = expectedPositionDeltaWorld;
                EvaluatedIntentReason = evaluatedIntentReason;
                EvaluatedHasIntent = evaluatedHasIntent;
                EvaluatedSimulationFacing = evaluatedSimulationFacing;
                EvaluatedDesiredMoveDirection = evaluatedDesiredMoveDirection;
                EndpointNormalized = endpointNormalized;
            }
        }

        private sealed class UnitState
        {
            internal readonly int UnitId;
            internal readonly ulong NetworkObjectId;
            internal readonly UnitType UnitType;
            internal readonly TeamId Team;
            internal readonly UnitMovementReducer Reducer = new UnitMovementReducer();
            internal long LastIssuedFrameSequence;
            internal int ObservedFrames;
            internal int CompletedFrames;
            internal long LastCompletedFrameSequence;
            internal bool LastLegacyMoved;
            internal float LastLegacyPositionDelta;
            internal bool HasLastPhase;
            internal UnitMovementPhase LastPhase;
            internal string LastMismatch = "None";

            internal UnitState(
                int unitId,
                ulong networkObjectId,
                UnitType unitType,
                TeamId team)
            {
                UnitId = unitId;
                NetworkObjectId = networkObjectId;
                UnitType = unitType;
                Team = team;
            }
        }

        private sealed class CoverageAggregate
        {
            internal int ObservedFrames;
            internal int AlignFrames;
            internal int MoveFrames;
            internal int EndpointNoIntent;
            internal int TargetAcquirePriority;
            internal int WriterExpectedAdvance;
            internal int WriterExpectedNoAdvance;
            internal int LegacyMovedWhileAlign;
            internal int LegacyStoppedWhileMove;
            internal int EndpointRotation;

            internal string ToManifestEntry()
            {
                return
                    $"frames={ObservedFrames},align={AlignFrames},move={MoveFrames}," +
                    $"endpointNoIntent={EndpointNoIntent}," +
                    $"targetPriority={TargetAcquirePriority}," +
                    $"writerExpectedAdvance={WriterExpectedAdvance}," +
                    $"writerExpectedNoAdvance={WriterExpectedNoAdvance}," +
                    $"legacyMovedWhileAlign={LegacyMovedWhileAlign}," +
                    $"legacyStoppedWhileMove={LegacyStoppedWhileMove}," +
                    $"endpointRotation={EndpointRotation}";
            }
        }
    }
}
#endif
