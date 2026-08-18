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
    internal static class UnitMovementAuthorityObserver
    {
        private const string Schema = "b3-movement-authority-v1";
        private const int MaximumLines = 768;
        private const int ReservedTerminalLines = 32;
        private const int MaximumNormalLines = MaximumLines - ReservedTerminalLines;
        private const int MaximumDetails = 128;
        private const int MaximumManifestChunks = 29;
        private const int PayloadLimit = 700;
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
        private static int _terminalLines;
        private static int _terminalOverflow;
        private static int _manifestPreflightFailures;
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
                    $"role={(_isServer ? "host" : "client")}, observerSchema={Schema}, " +
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
            Vector3 positionBefore,
            Quaternion rotationBefore,
            Vector3 positionAfter,
            Quaternion rotationAfter,
            bool authorityWriterSelected)
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
            if (_details++ < MaximumDetails)
                Log(LogLevel.Error, "movement-publication-failed", $"unitId={unitId}");
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

            ClientReplicationIssue reused = ClassifyClientReplicatedState(
                hasPrevious: false,
                previous: default,
                unitId: 8,
                phase: UnitMovementPhase.Move,
                commandRevision: 1UL,
                segmentRevision: 1UL,
                semanticRevision: 1UL);
            return wrongIdentityRetired && retired
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
                nonManifestTerminalLines: 2,
                out IReadOnlyList<string> chunks);
            if (!manifestValid)
            {
                _manifestPreflightFailures++;
                chunks = Array.Empty<string>();
            }
            if (manifestValid)
            {
                for (int i = 0; i < chunks.Count; i++)
                    LogTerminal(LogLevel.Info, "coverage-manifest", $"chunk={i + 1}/{chunks.Count}, entries={chunks[i]}");
            }

            LogTerminal(manifestValid ? LogLevel.Info : LogLevel.Error, "summary",
                $"manifestChunks={chunks.Count}, manifestPreflightFailures={_manifestPreflightFailures}, " +
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
                $"lifecycleIdentityConflicts={_lifecycleIdentityConflicts}");

            bool failure = _rejected != 0 || _invalid != 0 || _gateFailures != 0
                || _writerSelectionFailures != 0 || _clientWriteAttempts != 0
                || _attackWriterOwnershipConflicts != 0 || _dropped != 0
                || !manifestValid || _manifestPreflightFailures != 0
                || _terminalOverflow != 0 || _clientReplicatedInvalid != 0
                || _lifecycleIdentityConflicts != 0
                || (_isServer && (_frames == 0 || _align == 0 || _move == 0
                    || _noIntent == 0 || entries.Count == 0))
                || (!_isServer && _clientReplicatedSamples == 0);
            LogTerminal(failure ? LogLevel.Error : LogLevel.Info, "END",
                $"reason={reason}, role={(_isServer ? "host" : "client")}, frames={_frames}, " +
                $"align={_align}, move={_move}, noIntent={_noIntent}, endpoint={_endpoint}, " +
                $"targetPriority={_targetPriority}, rejected={_rejected}, invalid={_invalid}, " +
                $"gateFailures={_gateFailures}, writerSelectionFailures={_writerSelectionFailures}, " +
                $"clientRootOrReducerWriteAttempts={_clientWriteAttempts}, " +
                $"attackWriterOwnershipConflicts={_attackWriterOwnershipConflicts}, " +
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
                $"coverageEntries={entries.Count}, coverageSha256={sha}, droppedLogs={_dropped}, " +
                $"manifestPreflightFailures={_manifestPreflightFailures}, " +
                $"terminalOverflow={_terminalOverflow}, " +
                $"verdict={(failure ? "FAIL" : "EVIDENCE")}");
            Reset();
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
            _networkManager = null;
            _active = false;
            _isServer = false;
            _runId = null;
            _sessionKey = null;
            _startedAt = 0d;
            _lines = _dropped = _details = _frames = _align = _move = _noIntent = 0;
            _endpoint = _targetPriority = _rejected = _invalid = _gateFailures = 0;
            _writerSelectionFailures = _clientWriteAttempts = 0;
            _attackWriterOwnershipConflicts = 0;
            _terminalLines = _terminalOverflow = _manifestPreflightFailures = 0;
            _clientReplicatedSamples = _clientReplicatedInvalid = 0;
            _clientReplicatedDuplicates = _clientRevisionZero = 0;
            _clientRevisionRegressions = _clientSameRevisionConflicts = 0;
            _clientInvalidPhase = _clientUnitUninitialized = _clientInvalidScope = 0;
            _clientIdentityConflicts = _clientScopeRegressions = 0;
            _lifecycleIdentityConflicts = 0;
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
