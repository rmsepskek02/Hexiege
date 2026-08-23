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
    /// B3 production decision/write를 읽기만 하는 bounded observer다. 별도 reducer를
    /// 실행하지 않으며 transform, phase, 경로 또는 NetworkVariable을 쓰지 않는다.
    /// </summary>
    public static class UnitMovementAuthorityObserver
    {
        // v2: rejected/invalid frame의 독립 failure evidence와
        // root 정지 중 Walk 진행 위반을 손실 없이 관측한다.
        // v3: gameplay action ownership gate, retry-stable movement scope commit,
        // held Walk reassertion correction evidence.
        // v4: trajectory-corridor 후보 축소와 제자리 정렬 fallback 사용량을 관측한다.
        // v5: A* path[0] non-walkable start-tile egress frame을 별도로 관측한다.
        // v6: terminal corridor 실패에 Root/Domain/UnitData/path/sample 근거를 보존한다.
        // v7: 이동 중 path-start 보정이 논리 위치를 선행 커밋하지 않는 빌드를 식별한다.
        // v8: route checkpoint 소비와 실제 spatial Root 타일 커밋을 분리해 관측한다.
        // v9: 후보 probe는 기록하지 않고 최종 stage/recoverable repath/fatal만 분리한다.
        /// <summary>
        /// 런타임 증거와 Editor 교차 감사가 함께 사용하는 현재 production schema.
        /// 문자열을 복제하지 않아 오래된 양쪽 빌드가 서로 일치한다는 이유만으로 통과하지 않게 한다.
        /// </summary>
        public const string ProductionSchema = "b3-movement-authority-v9";
        private const int MaximumLines = 768;
        private const int ReservedTerminalLines = 32;
        private const int MaximumNormalLines = MaximumLines - ReservedTerminalLines;
        private const int MaximumDetails = 128;
        private const int MaximumFailureDetails = 64;
        private const int MaximumManifestChunks = 26;
        private const int PayloadLimit = 700;
        private const int NonManifestTerminalLines = 6;
        private const int MaximumFullLineUtf8BytesExclusive = 1000;
        private static readonly Dictionary<string, Coverage> Coverages =
            new Dictionary<string, Coverage>();
        private static readonly HashSet<string> PhaseTransitions =
            new HashSet<string>();
        private static readonly Dictionary<ulong, ServerPhaseSnapshot> LastPhases =
            new Dictionary<ulong, ServerPhaseSnapshot>();
        private static readonly Dictionary<ulong, ClientReplicatedSnapshot>
            ClientReplicatedSnapshots =
                new Dictionary<ulong, ClientReplicatedSnapshot>();

        private static NetworkManager _networkManager;
        private static bool _active;
        private static bool _isServer;
        private static string _runId;
        private static string _sessionKey;
        private static double _startedAt;
        private static int _lines;
        private static int _dropped;
        private static int _details;
        private static int _failureDetails;
        private static int _failureEvidenceOverflow;
        private static int _frames;
        private static int _align;
        private static int _move;
        private static int _noIntent;
        private static int _endpoint;
        private static int _targetPriority;
        private static int _rejected;
        private static int _invalid;
        private static int _gateFailures;
        private static int _writerSelectionFailures;
        private static int _clientWriteAttempts;
        private static int _attackWriterOwnershipConflicts;
        private static int _ignoredServerTargetEvents;
        private static int _heldFrames;
        private static int _stationaryWalkViolations;
        private static int _corridorFullSmooth;
        private static int _corridorReducedSmooth;
        private static int _corridorFullDirect;
        private static int _corridorReducedDirect;
        private static int _corridorStationaryAlignment;
        private static int _corridorRepathRequired;
        private static int _corridorStartTileEgressFrames;
        private static int _spatialTransitionsPlanned;
        private static int _spatialTransitionsCommitted;
        private static int _spatialNoTransitionPlans;
        private static int _spatialTerminalContacts;
        private static int _spatialPreflightFailures;
        private static int _spatialCommitFailures;
        private static int _spatialRecoveries;
        private static int _spatialFinalRecoverableRepaths;
        private static int _spatialRepeatedRecoverableRepaths;
        private static int _spatialFatalPreflights;
        private static int _spatialStagesForCommit;
        private static int _spatialCleanupAfterRecoverable;
        private static int _spatialSameFrameRetries;
        private static int _spatialStageDivergences;
        private static readonly Dictionary<int, string> LastRecoverableSignatures =
            new Dictionary<int, string>();
        private static readonly Dictionary<int, int> LastRecoverableFrames =
            new Dictionary<int, int>();
        private static int _terminalLines;
        private static int _terminalOverflow;
        private static int _manifestPreflightFailures;
        private static int _terminalPreflightFailures;
        private static int _clientReplicatedSamples;
        private static int _clientReplicatedInvalid;
        private static int _clientReplicatedDuplicates;
        private static int _clientRevisionZero;
        private static int _clientRevisionRegressions;
        private static int _clientSameRevisionConflicts;
        private static int _clientInvalidPhase;
        private static int _clientUnitUninitialized;
        private static int _clientInvalidScope;
        private static int _clientIdentityConflicts;
        private static int _clientScopeRegressions;
        private static int _lifecycleIdentityConflicts;
        private static int _adapterFailures;

        internal static void BeginSession(NetworkManager networkManager, bool isServer)
        {
            Reset();
            _networkManager = networkManager;
            _isServer = isServer;
            _active = networkManager != null && networkManager.IsListening;
            _runId = Guid.NewGuid().ToString("N");
            _sessionKey = CreateSharedSessionKey();
            _startedAt = ReadServerTime();
            if (_active)
            {
                Log(LogLevel.Info, "BEGIN",
                    $"role={(_isServer ? "host" : "client")}, observerSchema={ProductionSchema}, " +
                    $"mode=ReducerAuthoritative, serverTime={_startedAt:F6}");
            }
        }

        internal static void ObserveFrame(
            int unitId,
            ulong networkObjectId,
            UnitType unitType,
            TeamId team,
            UnitMovementIntentReason reason,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementEvaluation evaluation,
            Vector3 desiredTarget,
            float expectedPositionDeltaWorld,
            double observedServerTime,
            UnitMovementSnapshot reducerSnapshotBefore,
            Vector3 positionBefore,
            Quaternion rotationBefore,
            Vector3 positionAfter,
            Quaternion rotationAfter,
            bool authorityWriterSelected,
            bool walkAnimationAdvancing)
        {
            if (!_active) return;
            if (!_isServer)
            {
                _clientWriteAttempts++;
                return;
            }

            _frames++;
            if (!authorityWriterSelected)
                _writerSelectionFailures++;

            bool accepted = evaluation.ReducerInvoked
                && (evaluation.Status == UnitMovementReducerStatus.Accepted
                    || evaluation.Status == UnitMovementReducerStatus.Duplicate)
                && evaluation.Decision.IsValid;
            if (!accepted)
            {
                _rejected++;
                if (!evaluation.ReducerInvoked
                    || evaluation.Status == UnitMovementReducerStatus.InvalidInput
                    || evaluation.Status == UnitMovementReducerStatus.InvalidTime)
                    _invalid++;
                LogFailure("movement-decision-rejected",
                    $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                    $"unitType={unitType}, team={team}, reason={reason}, " +
                    $"commandRevision={commandRevision}, segmentRevision={segmentRevision}, " +
                    $"reducerInvoked={evaluation.ReducerInvoked}, reducerStatus={evaluation.Status}, " +
                    $"decisionValid={evaluation.Decision.IsValid}, phase={evaluation.Decision.Phase}, " +
                    $"evaluatedReason={evaluation.EvaluatedIntentReason}, " +
                    $"evaluatedHasIntent={evaluation.EvaluatedHasIntent}, " +
                    $"endpointNormalized={evaluation.EndpointNormalized}, " +
                    $"commitsAcquireCandidate={evaluation.CommitsAcquireCandidate}, " +
                    $"position=({positionBefore.x:F6},{positionBefore.z:F6}), " +
                    $"desiredTarget=({desiredTarget.x:F6},{desiredTarget.z:F6}), " +
                    $"expectedDelta={expectedPositionDeltaWorld:F6}, " +
                    $"observedServerTime={observedServerTime:F9}, " +
                    $"previousAcceptedTime={reducerSnapshotBefore.LastObservedServerTime:F9}, " +
                    $"hadAcceptedObservation={reducerSnapshotBefore.HasAcceptedObservation}, " +
                    $"previousCommandRevision={reducerSnapshotBefore.CommandRevision}, " +
                    $"previousSegmentRevision={reducerSnapshotBefore.SegmentRevision}, " +
                    $"facingValid={evaluation.SimulationFacing.IsValid}, " +
                    $"desiredDirectionValid={evaluation.DesiredMoveDirection.IsValid}");
                return;
            }

            UnitMovementPhase phase = evaluation.Decision.Phase;
            if (phase == UnitMovementPhase.AlignToMove) _align++;
            else if (phase == UnitMovementPhase.Move) _move++;
            else if (phase == UnitMovementPhase.NoIntent) _noIntent++;
            if (evaluation.EndpointNormalized) _endpoint++;
            if (evaluation.Decision.IsTargetAcquirePriority) _targetPriority++;

            float positionDelta = Vector2.Distance(
                new Vector2(positionBefore.x, positionBefore.z),
                new Vector2(positionAfter.x, positionAfter.z));
            bool shouldHoldWalk = UnitMovementPresentationPolicy.ShouldHoldWalk(
                evaluation.Decision.IsValid,
                evaluation.Decision.AllowsMovement,
                evaluation.CommitsAcquireCandidate);
            if (shouldHoldWalk) _heldFrames++;
            if (shouldHoldWalk && walkAnimationAdvancing)
            {
                _stationaryWalkViolations++;
                LogFailure("stationary-walk-animation",
                    $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                    $"unitType={unitType}, team={team}, reason={reason}, " +
                    $"commandRevision={commandRevision}, segmentRevision={segmentRevision}, " +
                    $"phase={phase}, positionDelta={positionDelta:F6}");
            }
            if ((!evaluation.Decision.AllowsMovement
                    && !evaluation.CommitsAcquireCandidate
                    && positionDelta > 0.00001f)
                || (phase == UnitMovementPhase.Move
                    && evaluation.Decision.YawErrorDegrees > 15.000001d)
                || (phase == UnitMovementPhase.AlignToMove
                    && evaluation.Decision.YawErrorDegrees <= 10d))
            {
                _gateFailures++;
            }

            string key = $"{unitType}|{team}|{reason}";
            if (!Coverages.TryGetValue(key, out Coverage coverage))
            {
                coverage = new Coverage();
                Coverages.Add(key, coverage);
            }
            coverage.Frames++;
            if (phase == UnitMovementPhase.AlignToMove) coverage.Align++;
            if (phase == UnitMovementPhase.Move) coverage.Move++;
            if (phase == UnitMovementPhase.NoIntent) coverage.NoIntent++;
            if (evaluation.EndpointNormalized) coverage.Endpoint++;
            if (evaluation.Decision.IsTargetAcquirePriority) coverage.TargetPriority++;

            bool hasPreviousPhase = LastPhases.TryGetValue(
                networkObjectId, out ServerPhaseSnapshot previousSnapshot);
            if (hasPreviousPhase && previousSnapshot.UnitId != unitId)
            {
                _lifecycleIdentityConflicts++;
                if (_details++ < MaximumDetails)
                {
                    Log(LogLevel.Error, "server-lifecycle-identity-crossing",
                        $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                        $"baselineUnitId={previousSnapshot.UnitId}");
                }
                return;
            }

            UnitMovementPhase previous = hasPreviousPhase
                ? previousSnapshot.Phase
                : UnitMovementPhase.Invalid;
            LastPhases[networkObjectId] =
                new ServerPhaseSnapshot(unitId, phase);
            string transition = $"{reason}|{previous}->{phase}";
            if (previous != phase && PhaseTransitions.Add(transition)
                && _details < MaximumDetails)
            {
                _details++;
                Log(LogLevel.Info, "phase-transition",
                    $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                    $"commandRevision={commandRevision}, segmentRevision={segmentRevision}, " +
                    $"reason={reason}, previous={previous}, phase={phase}, " +
                    $"yaw={evaluation.Decision.YawErrorDegrees:F6}, positionDelta={positionDelta:F6}");
            }
        }

        internal static void ObserveLegacyWriterSelection()
        {
            if (_active) _writerSelectionFailures++;
        }

        internal static void ObserveClientWriteAttempt()
        {
            if (_active) _clientWriteAttempts++;
        }

        internal static void ObserveAttackWriterOwnershipConflict(int unitId)
        {
            if (!_active) return;
            _attackWriterOwnershipConflicts++;
            if (_details++ < MaximumDetails)
                Log(LogLevel.Error, "double-writer-attempt", $"unitId={unitId}");
        }

        internal static void ObservePublicationFailure(int unitId)
        {
            if (!_active) return;
            _rejected++;
            LogFailure("movement-publication-failed", $"unitId={unitId}");
        }

        /// <summary>
        /// UnitView adapter가 fail-closed로 중단한 경계를 공용 bounded failure 채널에 기록한다.
        /// 게임 상태를 쓰지 않으며 server authority observer가 활성인 경우에만 집계한다.
        /// </summary>
        internal static void ObserveAdapterFailure(string kind, string context)
        {
            if (!_active || !_isServer) return;
            RecordAdapterFailure(kind, context, emitLog: true);
        }

        private static void RecordAdapterFailure(
            string kind,
            string context,
            bool emitLog)
        {
            IncrementBounded(ref _adapterFailures);
            if (emitLog)
            {
                LogFailure(
                    "movement-adapter-failure",
                    $"kind={kind ?? "unknown"}, context={context ?? "unavailable"}");
                return;
            }

            if (_failureDetails >= MaximumFailureDetails)
                _failureEvidenceOverflow++;
            else
                _failureDetails++;
        }

        internal static string ValidateAdapterFailureStateForValidation(
            int observations)
        {
            if (observations < 0) return "Invalid";
            Reset();
            _active = true;
            _isServer = true;
            for (int index = 0; index < observations; index++)
                RecordAdapterFailure("fixture", "fixture", emitLog: false);
            int details = _failureDetails;
            int overflow = _failureEvidenceOverflow;
            int failures = _adapterFailures;
            bool failure = HasAdapterFailure();
            Reset();
            bool reset = _adapterFailures == 0
                && _failureDetails == 0
                && _failureEvidenceOverflow == 0
                && !_active;
            return $"details={details},overflow={overflow},adapter={failures}," +
                   $"failure={failure},reset={reset}";
        }

        private static bool HasAdapterFailure() =>
            _adapterFailures != 0 || _failureEvidenceOverflow != 0;

        internal static void ObserveCorridorResolution(
            UnitTrajectoryCorridorResolution resolution,
            bool usedStartTileEgress)
        {
            if (!_active || !_isServer) return;

            if (usedStartTileEgress
                && resolution != UnitTrajectoryCorridorResolution.Invalid
                && resolution != UnitTrajectoryCorridorResolution.RepathRequired)
            {
                _corridorStartTileEgressFrames++;
            }

            switch (resolution)
            {
                case UnitTrajectoryCorridorResolution.FullSmooth:
                    _corridorFullSmooth++;
                    break;
                case UnitTrajectoryCorridorResolution.ReducedSmooth:
                    _corridorReducedSmooth++;
                    break;
                case UnitTrajectoryCorridorResolution.FullDirect:
                    _corridorFullDirect++;
                    break;
                case UnitTrajectoryCorridorResolution.ReducedDirect:
                    _corridorReducedDirect++;
                    break;
                case UnitTrajectoryCorridorResolution.StationaryAlignment:
                    _corridorStationaryAlignment++;
                    break;
                case UnitTrajectoryCorridorResolution.RepathRequired:
                    _corridorRepathRequired++;
                    break;
            }
        }

        internal static void ObserveSpatialFinalCandidate(
            int unitId,
            UnitSpatialCandidateVerdict verdict,
            UnitSpatialTransitionPlanStatus status,
            UnitSpatialPreflightResult preflight,
            int transitionCount,
            HexCoord committedPosition,
            HexCoord rootBeforeHex,
            HexCoord candidateHex,
            bool repathRequired)
        {
            if (!_active || !_isServer) return;

            if (verdict == UnitSpatialCandidateVerdict.Accepted
                && preflight.IsSuccess
                && !repathRequired)
            {
                IncrementBounded(ref _spatialStagesForCommit);
                if (status == UnitSpatialTransitionPlanStatus.NoTransition)
                    IncrementBounded(ref _spatialNoTransitionPlans);
                if (status == UnitSpatialTransitionPlanStatus.TerminalContact)
                    IncrementBounded(ref _spatialTerminalContacts);
                return;
            }

            if (repathRequired
                && (verdict == UnitSpatialCandidateVerdict.RouteInvalidated
                    || verdict == UnitSpatialCandidateVerdict.CandidateUnsafe))
            {
                IncrementBounded(ref _spatialFinalRecoverableRepaths);
                string signature = $"{verdict}|{status}|{preflight.Status}|" +
                    $"{preflight.OffendingTile}|{preflight.OffendingTransitionIndex}|" +
                    $"{committedPosition}|{rootBeforeHex}|{candidateHex}";
                if (LastRecoverableSignatures.TryGetValue(unitId, out string previous)
                    && string.Equals(previous, signature, StringComparison.Ordinal))
                {
                    IncrementBounded(ref _spatialRepeatedRecoverableRepaths);
                    if (LastRecoverableFrames.TryGetValue(unitId, out int previousFrame)
                        && previousFrame == Time.frameCount)
                    {
                        IncrementBounded(ref _spatialSameFrameRetries);
                    }
                    LogFailure("spatial-recoverable-repeated",
                        $"unitId={unitId}, verdict={verdict}, status={status}, " +
                        $"preflight={preflight.Status}, offendingTile={preflight.OffendingTile}, " +
                        $"offendingIndex={preflight.OffendingTransitionIndex}, " +
                        $"committedPosition={committedPosition}, rootBeforeHex={rootBeforeHex}, " +
                        $"candidateHex={candidateHex}, sameFrame={previousFrame == Time.frameCount}");
                }
                LastRecoverableSignatures[unitId] = signature;
                LastRecoverableFrames[unitId] = Time.frameCount;
                return;
            }

            IncrementBounded(ref _spatialFatalPreflights);
            IncrementBounded(ref _spatialPreflightFailures);
            LogFailure("spatial-preflight-fatal",
                $"unitId={unitId}, verdict={verdict}, status={status}, " +
                $"preflight={preflight.Status}, offendingTile={preflight.OffendingTile}, " +
                $"offendingIndex={preflight.OffendingTransitionIndex}, " +
                $"transitionCount={transitionCount}, committedPosition={committedPosition}, " +
                $"rootBeforeHex={rootBeforeHex}, candidateHex={candidateHex}");
        }

        internal static void ObserveSpatialCleanupAfterRecoverable(int unitId)
        {
            if (!_active || !_isServer) return;
            IncrementBounded(ref _spatialCleanupAfterRecoverable);
            LogFailure("spatial-cleanup-after-recoverable", $"unitId={unitId}");
        }

        internal static void ObserveSpatialStageDivergence(
            int unitId,
            UnitSpatialCandidateVerdict probeVerdict,
            UnitSpatialCandidateVerdict stageVerdict,
            UnitSpatialPreflightStatus probePreflight,
            UnitSpatialPreflightStatus stagePreflight)
        {
            if (!_active || !_isServer) return;
            IncrementBounded(ref _spatialStageDivergences);
            LogFailure("spatial-probe-stage-divergence",
                $"unitId={unitId}, probeVerdict={probeVerdict}, stageVerdict={stageVerdict}, " +
                $"probePreflight={probePreflight}, stagePreflight={stagePreflight}");
        }

        internal static void ObserveSpatialCommit(
            int unitId,
            int transitionCount,
            HexCoord committedPositionBefore,
            HexCoord committedPositionAfter,
            bool succeeded)
        {
            if (!_active || !_isServer) return;

            if (succeeded)
            {
                AddBounded(
                    ref _spatialTransitionsCommitted,
                    UnitSpatialDiagnosticAccountingPolicy
                        .CommittedDeltaAtCommitResult(transitionCount, true));
                if (UnitSpatialDiagnosticAccountingPolicy
                    .ClearsRecoverableHistory(transitionCount, true))
                {
                    LastRecoverableSignatures.Remove(unitId);
                    LastRecoverableFrames.Remove(unitId);
                }
                return;
            }

            IncrementBounded(ref _spatialCommitFailures);
            LogFailure("spatial-commit-failed",
                $"unitId={unitId}, transitionCount={transitionCount}, " +
                $"committedPositionBefore={committedPositionBefore}, " +
                $"committedPositionAfter={committedPositionAfter}");
        }

        internal static void ObserveSpatialCommitAttempt(
            int unitId,
            int transitionCount,
            HexCoord committedPosition)
        {
            if (!_active || !_isServer) return;

            int plannedDelta = UnitSpatialDiagnosticAccountingPolicy
                .PlannedDeltaAtCommitAttempt(transitionCount);
            if (plannedDelta <= 0)
            {
                IncrementBounded(ref _spatialPreflightFailures);
                LogFailure("spatial-commit-attempt-invalid",
                    $"unitId={unitId}, transitionCount={transitionCount}, " +
                    $"committedPosition={committedPosition}");
                return;
            }

            AddBounded(ref _spatialTransitionsPlanned, plannedDelta);
        }

        private static void IncrementBounded(ref int value)
        {
            if (value < int.MaxValue)
                value++;
        }

        private static void AddBounded(ref int value, int amount)
        {
            if (amount <= 0 || value == int.MaxValue) return;
            value = amount >= int.MaxValue - value
                ? int.MaxValue
                : value + amount;
        }

        internal static void ObserveIgnoredServerTargetEvent(
            int unitId,
            string eventKind)
        {
            if (!_active || !_isServer) return;
            _ignoredServerTargetEvents++;
            if (_details < MaximumDetails)
            {
                _details++;
                Log(LogLevel.Info, "ignored-stale-action-target-event",
                    $"unitId={unitId}, eventKind={eventKind}");
            }
        }

        private static void LogFailure(string message, string data)
        {
            if (_failureDetails >= MaximumFailureDetails)
            {
                _failureEvidenceOverflow++;
                return;
            }

            _failureDetails++;
            Log(LogLevel.Error, message, data);
        }

        internal static void ObserveClientReplicatedState(
            int unitId,
            ulong networkObjectId,
            UnitMovementPhase phase,
            ulong commandRevision,
            ulong segmentRevision,
            ulong semanticRevision)
        {
            if (!_active || _isServer) return;
            _clientReplicatedSamples++;
            bool hasPrevious = ClientReplicatedSnapshots.TryGetValue(
                networkObjectId, out ClientReplicatedSnapshot previous);
            ClientReplicationIssue issue = ClassifyClientReplicatedState(
                hasPrevious,
                previous,
                unitId,
                phase,
                commandRevision,
                segmentRevision,
                semanticRevision);
            bool duplicate = (issue & ClientReplicationIssue.Duplicate) != 0;
            ClientReplicationIssue failures =
                issue & ~ClientReplicationIssue.Duplicate;

            if (duplicate) _clientReplicatedDuplicates++;
            if ((failures & ClientReplicationIssue.RevisionZero) != 0)
                _clientRevisionZero++;
            if ((failures & ClientReplicationIssue.RevisionRegression) != 0)
                _clientRevisionRegressions++;
            if ((failures & ClientReplicationIssue.SameRevisionConflict) != 0)
                _clientSameRevisionConflicts++;
            if ((failures & ClientReplicationIssue.InvalidPhase) != 0)
                _clientInvalidPhase++;
            if ((failures & ClientReplicationIssue.UnitUninitialized) != 0)
                _clientUnitUninitialized++;
            if ((failures & ClientReplicationIssue.InvalidScope) != 0)
                _clientInvalidScope++;
            if ((failures & ClientReplicationIssue.IdentityConflict) != 0)
                _clientIdentityConflicts++;
            if ((failures & ClientReplicationIssue.ScopeRegression) != 0)
                _clientScopeRegressions++;
            if (failures != ClientReplicationIssue.None)
                _clientReplicatedInvalid++;

            if (failures == ClientReplicationIssue.None && !duplicate)
            {
                ClientReplicatedSnapshots[networkObjectId] =
                    new ClientReplicatedSnapshot(
                        unitId,
                        phase,
                        commandRevision,
                        segmentRevision,
                        semanticRevision);
            }

            if (_details++ < MaximumDetails)
            {
                Log(failures == ClientReplicationIssue.None
                        ? LogLevel.Info
                        : LogLevel.Error,
                    "client-replicated-state",
                    $"unitId={unitId}, networkObjectId={networkObjectId}, phase={phase}, " +
                    $"commandRevision={commandRevision}, segmentRevision={segmentRevision}, " +
                    $"semanticRevision={semanticRevision}, classification={issue}");
            }
        }

        internal static bool ShouldObserveClientReplicatedState(
            int unitId,
            ulong semanticRevision)
            => unitId >= 0 && semanticRevision != 0UL;

        internal static void RetireUnitLifecycle(
            int unitId,
            ulong networkObjectId)
        {
            if (!_active) return;

            if (_isServer)
            {
                RetireRecoverableHistory(
                    LastRecoverableSignatures,
                    LastRecoverableFrames,
                    unitId);
                LifecycleRetireResult serverResult = RetireUnitLifecycleCore(
                    LastPhases,
                    networkObjectId,
                    unitId,
                    snapshot => snapshot.UnitId,
                    out int baselineUnitId);
                if (serverResult == LifecycleRetireResult.IdentityConflictRetired)
                {
                    _lifecycleIdentityConflicts++;
                    if (_details++ < MaximumDetails)
                    {
                        Log(LogLevel.Error, "server-lifecycle-retire-conflict",
                            $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                            $"baselineUnitId={baselineUnitId}");
                    }
                }
                return;
            }

            LifecycleRetireResult clientResult = RetireUnitLifecycleCore(
                ClientReplicatedSnapshots,
                networkObjectId,
                unitId,
                snapshot => snapshot.UnitId,
                out int clientBaselineUnitId);
            if (clientResult == LifecycleRetireResult.IdentityConflictRetired)
            {
                _clientReplicatedInvalid++;
                _clientIdentityConflicts++;
                _lifecycleIdentityConflicts++;
                if (_details++ < MaximumDetails)
                {
                    Log(LogLevel.Error, "client-lifecycle-retire-conflict",
                        $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                        $"baselineUnitId={clientBaselineUnitId}");
                }
            }
        }

        private static void RetireRecoverableHistory(
            IDictionary<int, string> signatures,
            IDictionary<int, int> frames,
            int unitId)
        {
            signatures?.Remove(unitId);
            frames?.Remove(unitId);
        }

        private static LifecycleRetireResult RetireUnitLifecycleCore<TSnapshot>(
            IDictionary<ulong, TSnapshot> snapshots,
            ulong networkObjectId,
            int unitId,
            Func<TSnapshot, int> selectUnitId,
            out int baselineUnitId)
        {
            baselineUnitId = -1;
            if (!snapshots.TryGetValue(networkObjectId, out TSnapshot previous))
                return LifecycleRetireResult.NotFound;

            baselineUnitId = selectUnitId(previous);
            snapshots.Remove(networkObjectId);
            return baselineUnitId == unitId
                ? LifecycleRetireResult.Retired
                : LifecycleRetireResult.IdentityConflictRetired;
        }

        private static ClientReplicationIssue ClassifyClientReplicatedState(
            bool hasPrevious,
            ClientReplicatedSnapshot previous,
            int unitId,
            UnitMovementPhase phase,
            ulong commandRevision,
            ulong segmentRevision,
            ulong semanticRevision)
        {
            ClientReplicationIssue issue = ClientReplicationIssue.None;
            if (unitId < 0)
                issue |= ClientReplicationIssue.UnitUninitialized;
            if (hasPrevious && unitId != previous.UnitId)
                issue |= ClientReplicationIssue.IdentityConflict;
            if (!Enum.IsDefined(typeof(UnitMovementPhase), phase)
                || phase == UnitMovementPhase.Invalid)
                issue |= ClientReplicationIssue.InvalidPhase;
            if (commandRevision == 0UL || segmentRevision == 0UL)
                issue |= ClientReplicationIssue.InvalidScope;
            if (semanticRevision == 0UL)
                issue |= ClientReplicationIssue.RevisionZero;
            else if (hasPrevious && semanticRevision < previous.SemanticRevision)
                issue |= ClientReplicationIssue.RevisionRegression;
            else if (hasPrevious && semanticRevision == previous.SemanticRevision)
            {
                if (phase == previous.Phase
                    && unitId == previous.UnitId
                    && commandRevision == previous.CommandRevision
                    && segmentRevision == previous.SegmentRevision)
                    issue |= ClientReplicationIssue.Duplicate;
                else
                    issue |= ClientReplicationIssue.SameRevisionConflict;
            }
            else if (semanticRevision != 0UL
                && hasPrevious
                && semanticRevision > previous.SemanticRevision)
            {
                // NGO는 중간 semantic snapshot을 coalesce할 수 있다. 따라서 클라이언트는
                // 연속 증가(+1)를 강제하지 않고 관찰 가능한 scope의 사전식 단조성만 검증한다.
                if (commandRevision < previous.CommandRevision
                    || (commandRevision == previous.CommandRevision
                        && segmentRevision < previous.SegmentRevision))
                {
                    issue |= ClientReplicationIssue.ScopeRegression;
                }
            }

            return issue;
        }

        // Editor self-validation이 production과 동일한 순수 판정기를 호출하는 reflection seam.
        private static string ClassifyClientReplicatedStateForValidation(
            bool hasPrevious,
            int previousUnitId,
            int previousPhase,
            ulong previousCommandRevision,
            ulong previousSegmentRevision,
            ulong previousSemanticRevision,
            int unitId,
            int phase,
            ulong commandRevision,
            ulong segmentRevision,
            ulong semanticRevision)
        {
            ClientReplicationIssue issue = ClassifyClientReplicatedState(
                hasPrevious,
                new ClientReplicatedSnapshot(
                    previousUnitId,
                    (UnitMovementPhase)previousPhase,
                    previousCommandRevision,
                    previousSegmentRevision,
                    previousSemanticRevision),
                unitId,
                (UnitMovementPhase)phase,
                commandRevision,
                segmentRevision,
                semanticRevision);
            return issue.ToString();
        }

        private static string RetireUnitLifecycleForValidation()
        {
            const ulong networkObjectId = 42UL;
            var snapshots =
                new Dictionary<ulong, ClientReplicatedSnapshot>();
            snapshots.Add(
                networkObjectId,
                new ClientReplicatedSnapshot(
                    7,
                    UnitMovementPhase.Move,
                    2UL,
                    3UL,
                    9UL));

            LifecycleRetireResult wrongIdentity = RetireUnitLifecycleCore(
                snapshots,
                networkObjectId,
                8,
                snapshot => snapshot.UnitId,
                out _);
            bool wrongIdentityRetired =
                wrongIdentity == LifecycleRetireResult.IdentityConflictRetired
                && !snapshots.ContainsKey(networkObjectId);

            snapshots.Add(
                networkObjectId,
                new ClientReplicatedSnapshot(
                    7,
                    UnitMovementPhase.Move,
                    2UL,
                    3UL,
                    9UL));
            LifecycleRetireResult correctIdentity = RetireUnitLifecycleCore(
                snapshots,
                networkObjectId,
                7,
                snapshot => snapshot.UnitId,
                out _);
            bool retired = correctIdentity == LifecycleRetireResult.Retired
                && !snapshots.ContainsKey(networkObjectId);

            var recoverableSignatures = new Dictionary<int, string>
            {
                [7] = "old-lifecycle"
            };
            var recoverableFrames = new Dictionary<int, int>
            {
                [7] = 123
            };
            RetireRecoverableHistory(
                recoverableSignatures,
                recoverableFrames,
                unitId: 7);
            bool recoverableHistoryRetired =
                !recoverableSignatures.ContainsKey(7)
                && !recoverableFrames.ContainsKey(7);

            ClientReplicationIssue reused = ClassifyClientReplicatedState(
                hasPrevious: false,
                previous: default,
                unitId: 8,
                phase: UnitMovementPhase.Move,
                commandRevision: 1UL,
                segmentRevision: 1UL,
                semanticRevision: 1UL);
            return wrongIdentityRetired && retired
                && recoverableHistoryRetired
                && reused == ClientReplicationIssue.None
                    ? "PASS"
                    : "FAIL";
        }

        internal static void EndSession(string reason)
        {
            if (!_active) return;
            var entries = new List<string>();
            foreach (KeyValuePair<string, Coverage> pair in Coverages)
            {
                Coverage value = pair.Value;
                entries.Add($"{pair.Key}|frames={value.Frames}|align={value.Align}|move={value.Move}|noIntent={value.NoIntent}|endpoint={value.Endpoint}|targetPriority={value.TargetPriority}");
            }
            entries.Sort(StringComparer.Ordinal);
            string joined = string.Join("\n", entries);
            string sha = Sha256(joined);
            bool manifestValid = UnitMovementManifestChunker.TryBuild(
                entries,
                PayloadLimit,
                MaximumManifestChunks,
                ReservedTerminalLines,
                nonManifestTerminalLines: NonManifestTerminalLines,
                out IReadOnlyList<string> chunks);
            if (!manifestValid)
            {
                _manifestPreflightFailures++;
                chunks = Array.Empty<string>();
            }
            bool failure = _rejected != 0 || _invalid != 0 || _gateFailures != 0
                || _writerSelectionFailures != 0 || _clientWriteAttempts != 0
                || _attackWriterOwnershipConflicts != 0 || _dropped != 0
                || _stationaryWalkViolations != 0
                || _spatialPreflightFailures != 0
                || _spatialCommitFailures != 0
                || _spatialRecoveries != 0
                || _spatialRepeatedRecoverableRepaths != 0
                || _spatialFatalPreflights != 0
                || _spatialCleanupAfterRecoverable != 0
                || _spatialSameFrameRetries != 0
                || _spatialStageDivergences != 0
                || _spatialTransitionsPlanned != _spatialTransitionsCommitted
                || HasAdapterFailure()
                || !manifestValid || _manifestPreflightFailures != 0
                || _terminalOverflow != 0 || _clientReplicatedInvalid != 0
                || _lifecycleIdentityConflicts != 0
                || (_isServer && (_frames == 0 || _align == 0 || _move == 0
                    || _noIntent == 0 || entries.Count == 0))
                || (!_isServer && _clientReplicatedSamples == 0);

            List<TerminalRecord> records = BuildTerminalRecords(
                chunks,
                entries.Count,
                sha,
                reason,
                failure);
            if (!TryPreflightTerminalRecords(records, out _))
            {
                _terminalPreflightFailures++;
                failure = true;
                records = BuildTerminalRecords(
                    chunks,
                    entries.Count,
                    sha,
                    "terminal-preflight-failed",
                    failure);
            }

            bool terminalSafe = TryPreflightTerminalRecords(records, out _);
            if (!terminalSafe)
            {
                _terminalOverflow++;
                LogTerminal(
                    LogLevel.Error,
                    "END",
                    $"reason=terminal-preflight-unrecoverable, " +
                    $"role={(_isServer ? "host" : "client")}, verdict=FAIL");
                Reset();
                return;
            }

            LogLevel terminalLevel = failure ? LogLevel.Error : LogLevel.Info;
            foreach (TerminalRecord record in records)
                LogTerminal(terminalLevel, record.Message, record.Data);
            Reset();
        }

        private static List<TerminalRecord> BuildTerminalRecords(
            IReadOnlyList<string> chunks,
            int coverageEntries,
            string coverageSha,
            string reason,
            bool failure)
        {
            var records = new List<TerminalRecord>(
                (chunks?.Count ?? 0) + NonManifestTerminalLines);
            if (chunks != null)
            {
                for (int index = 0; index < chunks.Count; index++)
                {
                    records.Add(new TerminalRecord(
                        "coverage-manifest",
                        $"chunk={index + 1}/{chunks.Count}, entries={chunks[index]}"));
                }
            }

            records.Add(new TerminalRecord(
                "terminal-summary-core",
                $"manifestChunks={chunks?.Count ?? 0}, " +
                $"manifestPreflightFailures={_manifestPreflightFailures}, " +
                $"terminalPreflightFailures={_terminalPreflightFailures}, " +
                $"frames={_frames}, align={_align}, move={_move}, noIntent={_noIntent}, " +
                $"endpoint={_endpoint}, targetPriority={_targetPriority}, rejected={_rejected}, " +
                $"invalid={_invalid}, gateFailures={_gateFailures}, " +
                $"writerSelectionFailures={_writerSelectionFailures}, " +
                $"clientRootOrReducerWriteAttempts={_clientWriteAttempts}, " +
                $"attackWriterOwnershipConflicts={_attackWriterOwnershipConflicts}, " +
                $"ignoredServerTargetEvents={_ignoredServerTargetEvents}, " +
                $"heldFrames={_heldFrames}, stationaryWalkViolations={_stationaryWalkViolations}, " +
                $"failureDetails={_failureDetails}, " +
                $"failureEvidenceOverflow={_failureEvidenceOverflow}, " +
                $"adapterFailures={_adapterFailures}, droppedLogs={_dropped}"));
            records.Add(new TerminalRecord(
                "terminal-summary-corridor",
                $"corridorFullSmooth={_corridorFullSmooth}, " +
                $"corridorReducedSmooth={_corridorReducedSmooth}, " +
                $"corridorFullDirect={_corridorFullDirect}, " +
                $"corridorReducedDirect={_corridorReducedDirect}, " +
                $"corridorStationaryAlignment={_corridorStationaryAlignment}, " +
                $"corridorRepathRequired={_corridorRepathRequired}, " +
                $"corridorStartTileEgressFrames={_corridorStartTileEgressFrames}"));
            records.Add(new TerminalRecord(
                "terminal-summary-spatial-commit",
                $"spatialTransitionsPlanned={_spatialTransitionsPlanned}, " +
                $"spatialTransitionsCommitted={_spatialTransitionsCommitted}, " +
                $"spatialNoTransitionPlans={_spatialNoTransitionPlans}, " +
                $"spatialTerminalContacts={_spatialTerminalContacts}, " +
                $"spatialPreflightFailures={_spatialPreflightFailures}, " +
                $"spatialCommitFailures={_spatialCommitFailures}, " +
                $"spatialRecoveries={_spatialRecoveries}, " +
                $"spatialStagesForCommit={_spatialStagesForCommit}"));
            records.Add(new TerminalRecord(
                "terminal-summary-spatial-repath",
                $"spatialFinalRecoverableRepaths={_spatialFinalRecoverableRepaths}, " +
                $"spatialRepeatedRecoverableRepaths={_spatialRepeatedRecoverableRepaths}, " +
                $"spatialFatalPreflights={_spatialFatalPreflights}, " +
                $"spatialCleanupAfterRecoverable={_spatialCleanupAfterRecoverable}, " +
                $"spatialSameFrameRetries={_spatialSameFrameRetries}, " +
                $"spatialStageDivergences={_spatialStageDivergences}"));
            records.Add(new TerminalRecord(
                "terminal-summary-replication",
                $"clientReplicatedSamples={_clientReplicatedSamples}, " +
                $"clientReplicatedInvalid={_clientReplicatedInvalid}, " +
                $"clientReplicatedDuplicates={_clientReplicatedDuplicates}, " +
                $"clientRevisionZero={_clientRevisionZero}, " +
                $"clientRevisionRegressions={_clientRevisionRegressions}, " +
                $"clientSameRevisionConflicts={_clientSameRevisionConflicts}, " +
                $"clientInvalidPhase={_clientInvalidPhase}, " +
                $"clientUnitUninitialized={_clientUnitUninitialized}, " +
                $"clientInvalidScope={_clientInvalidScope}, " +
                $"clientIdentityConflicts={_clientIdentityConflicts}, " +
                $"clientScopeRegressions={_clientScopeRegressions}, " +
                $"lifecycleIdentityConflicts={_lifecycleIdentityConflicts}, " +
                $"coverageEntries={coverageEntries}, coverageSha256={coverageSha}, " +
                $"terminalOverflow={_terminalOverflow}"));
            records.Add(new TerminalRecord(
                "END",
                $"reason={NormalizeTerminalToken(reason)}, " +
                $"role={(_isServer ? "host" : "client")}, " +
                $"verdict={(failure ? "FAIL" : "EVIDENCE")}"));
            return records;
        }

        private static bool TryPreflightTerminalRecords(
            IReadOnlyList<TerminalRecord> records,
            out int maximumUtf8Bytes)
        {
            maximumUtf8Bytes = 0;
            if (records == null || records.Count > ReservedTerminalLines)
                return false;
            for (int index = 0; index < records.Count; index++)
            {
                TerminalRecord record = records[index];
                int bytes = Encoding.UTF8.GetByteCount(FormatFullTerminalLine(
                    record.Message,
                    record.Data,
                    _runId,
                    _sessionKey,
                    _startedAt));
                maximumUtf8Bytes = Math.Max(maximumUtf8Bytes, bytes);
                if (bytes >= MaximumFullLineUtf8BytesExclusive)
                    return false;
            }
            return true;
        }

        private static string FormatFullTerminalLine(
            string message,
            string data,
            string runId,
            string sessionKey,
            double startedAt)
        {
            return
                "[00:00:00.000] [ERROR] [Network/UnitMovementAuthorityObserver] " +
                $"[UAS-MOVE-AUTH] {message} | " +
                $"runId={runId ?? "unavailable"}, " +
                $"sharedSessionKey={sessionKey ?? "unavailable"}, " +
                $"startedAt={startedAt:F6}, {data}";
        }

        private static string NormalizeTerminalToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unavailable";
            string normalized = value.Replace(',', '_').Replace('\r', '_').Replace('\n', '_');
            return normalized.Length <= 64 ? normalized : normalized.Substring(0, 64);
        }

        internal static string ValidateTerminalPreflightForValidation(
            int manifestChunks,
            string payload)
        {
            if (manifestChunks < 0 || payload == null) return "Invalid";
            var records = new List<TerminalRecord>(manifestChunks + NonManifestTerminalLines);
            for (int index = 0; index < manifestChunks; index++)
            {
                records.Add(new TerminalRecord(
                    "coverage-manifest",
                    $"chunk={index + 1}/{manifestChunks}, entries={payload}"));
            }
            records.Add(new TerminalRecord("terminal-summary-core", "value=1"));
            records.Add(new TerminalRecord("terminal-summary-corridor", "value=1"));
            records.Add(new TerminalRecord("terminal-summary-spatial-commit", "value=1"));
            records.Add(new TerminalRecord("terminal-summary-spatial-repath", "value=1"));
            records.Add(new TerminalRecord("terminal-summary-replication", "value=1"));
            records.Add(new TerminalRecord(
                "END",
                "reason=fixture, role=host, verdict=EVIDENCE"));

            string previousRunId = _runId;
            string previousSessionKey = _sessionKey;
            double previousStartedAt = _startedAt;
            _runId = new string('r', 32);
            _sessionKey = new string('s', 64);
            _startedAt = 123456.123456d;
            bool valid = TryPreflightTerminalRecords(records, out int maximumBytes);
            _runId = previousRunId;
            _sessionKey = previousSessionKey;
            _startedAt = previousStartedAt;
            return $"valid={valid},lines={records.Count},maxBytes={maximumBytes}";
        }

        internal static string ValidateProductionTerminalRecordsForValidation()
        {
            Reset();
            _runId = new string('r', 32);
            _sessionKey = new string('s', 64);
            _startedAt = 123456.123456d;
            var chunks = new List<string>(MaximumManifestChunks);
            for (int index = 0; index < MaximumManifestChunks; index++)
                chunks.Add(new string('x', PayloadLimit));
            List<TerminalRecord> records = BuildTerminalRecords(
                chunks,
                coverageEntries: int.MaxValue,
                coverageSha: new string('a', 64),
                reason: new string('r', 64),
                failure: true);
            bool valid = TryPreflightTerminalRecords(records, out int maximumBytes);
            int lines = records.Count;
            Reset();
            bool reset = _runId == null
                && _sessionKey == null
                && _terminalLines == 0
                && _terminalPreflightFailures == 0;
            return $"valid={valid},lines={lines},maxBytes={maximumBytes},reset={reset}";
        }

        private static void Log(LogLevel level, string message, string data)
        {
            if (_lines >= MaximumNormalLines)
            {
                _dropped++;
                return;
            }
            _lines++;
            RuntimeLogger.Log(level, "Network", nameof(UnitMovementAuthorityObserver),
                $"[UAS-MOVE-AUTH] {message}",
                $"runId={_runId}, sharedSessionKey={_sessionKey}, startedAt={_startedAt:F6}, {data}");
        }

        private static void LogTerminal(LogLevel level, string message, string data)
        {
            if (_terminalLines >= ReservedTerminalLines)
            {
                _terminalOverflow++;
                return;
            }
            _terminalLines++;
            _lines++;
            RuntimeLogger.Log(level, "Network", nameof(UnitMovementAuthorityObserver),
                $"[UAS-MOVE-AUTH] {message}",
                $"runId={_runId}, sharedSessionKey={_sessionKey}, startedAt={_startedAt:F6}, {data}");
        }

        private static double ReadServerTime()
            => _networkManager != null && _networkManager.IsListening
                ? _networkManager.ServerTime.Time
                : double.NaN;

        private static string CreateSharedSessionKey()
        {
            NetworkGameManager manager = UnityEngine.Object.FindFirstObjectByType<NetworkGameManager>();
            string lobbyId = manager?.CurrentLobby?.Id;
            return string.IsNullOrEmpty(lobbyId) ? "unavailable" : Sha256(lobbyId);
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte item in hash)
                    text.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static void Reset()
        {
            Coverages.Clear();
            PhaseTransitions.Clear();
            LastPhases.Clear();
            ClientReplicatedSnapshots.Clear();
            LastRecoverableSignatures.Clear();
            LastRecoverableFrames.Clear();
            _networkManager = null;
            _active = false;
            _isServer = false;
            _runId = null;
            _sessionKey = null;
            _startedAt = 0d;
            _lines = _dropped = _details = _frames = _align = _move = _noIntent = 0;
            _failureDetails = _failureEvidenceOverflow = 0;
            _endpoint = _targetPriority = _rejected = _invalid = _gateFailures = 0;
            _writerSelectionFailures = _clientWriteAttempts = 0;
            _attackWriterOwnershipConflicts = 0;
            _ignoredServerTargetEvents = 0;
            _heldFrames = _stationaryWalkViolations = 0;
            _corridorFullSmooth = _corridorReducedSmooth = 0;
            _corridorFullDirect = _corridorReducedDirect = 0;
            _corridorStationaryAlignment = _corridorRepathRequired = 0;
            _corridorStartTileEgressFrames = 0;
            _spatialTransitionsPlanned = _spatialTransitionsCommitted = 0;
            _spatialNoTransitionPlans = _spatialTerminalContacts = 0;
            _spatialPreflightFailures = _spatialCommitFailures = 0;
            _spatialRecoveries = 0;
            _spatialFinalRecoverableRepaths = 0;
            _spatialRepeatedRecoverableRepaths = 0;
            _spatialFatalPreflights = 0;
            _spatialStagesForCommit = 0;
            _spatialCleanupAfterRecoverable = 0;
            _spatialSameFrameRetries = 0;
            _spatialStageDivergences = 0;
            _terminalLines = _terminalOverflow = _manifestPreflightFailures = 0;
            _terminalPreflightFailures = 0;
            _clientReplicatedSamples = _clientReplicatedInvalid = 0;
            _clientReplicatedDuplicates = _clientRevisionZero = 0;
            _clientRevisionRegressions = _clientSameRevisionConflicts = 0;
            _clientInvalidPhase = _clientUnitUninitialized = _clientInvalidScope = 0;
            _clientIdentityConflicts = _clientScopeRegressions = 0;
            _lifecycleIdentityConflicts = 0;
            _adapterFailures = 0;
        }

        [Flags]
        private enum ClientReplicationIssue
        {
            None = 0,
            Duplicate = 1 << 0,
            RevisionZero = 1 << 1,
            RevisionRegression = 1 << 2,
            SameRevisionConflict = 1 << 3,
            InvalidPhase = 1 << 4,
            UnitUninitialized = 1 << 5,
            InvalidScope = 1 << 6,
            IdentityConflict = 1 << 7,
            ScopeRegression = 1 << 8
        }

        private enum LifecycleRetireResult
        {
            NotFound = 0,
            Retired = 1,
            IdentityConflictRetired = 2
        }

        private readonly struct TerminalRecord
        {
            internal readonly string Message;
            internal readonly string Data;

            internal TerminalRecord(string message, string data)
            {
                Message = message;
                Data = data;
            }
        }

        private readonly struct ServerPhaseSnapshot
        {
            internal readonly int UnitId;
            internal readonly UnitMovementPhase Phase;

            internal ServerPhaseSnapshot(int unitId, UnitMovementPhase phase)
            {
                UnitId = unitId;
                Phase = phase;
            }
        }

        private readonly struct ClientReplicatedSnapshot
        {
            internal readonly int UnitId;
            internal readonly UnitMovementPhase Phase;
            internal readonly ulong CommandRevision;
            internal readonly ulong SegmentRevision;
            internal readonly ulong SemanticRevision;

            internal ClientReplicatedSnapshot(
                int unitId,
                UnitMovementPhase phase,
                ulong commandRevision,
                ulong segmentRevision,
                ulong semanticRevision)
            {
                UnitId = unitId;
                Phase = phase;
                CommandRevision = commandRevision;
                SegmentRevision = segmentRevision;
                SemanticRevision = semanticRevision;
            }
        }

        private sealed class Coverage
        {
            internal int Frames;
            internal int Align;
            internal int Move;
            internal int NoIntent;
            internal int Endpoint;
            internal int TargetPriority;
        }
    }
}
#endif
