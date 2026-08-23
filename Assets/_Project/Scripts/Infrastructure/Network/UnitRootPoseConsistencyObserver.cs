using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Presentation;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Development-only, read-only evidence collector for the Simulation Root / Visual Root
    /// projection contract. NetworkCombatController creates it only for a live NGO session.
    ///
    /// Important constraints:
    /// - sampling happens after WaitForEndOfFrame, at most once per server-time second;
    /// - this class never assigns any Transform property and never calls ProjectNow;
    /// - every record carries UnitId, NetworkObjectId and a server-time bucket so independently
    ///   captured host/client logs can be joined without adding gameplay network traffic;
    /// - exact cross-peer comparison is meaningful only for stable samples. Moving units are
    ///   reported as coverage, but are never claimed to match at an identical instant;
    /// - tracking, logging and lifetime are all bounded to keep diagnostics out of gameplay.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class UnitRootPoseConsistencyObserver : MonoBehaviour
    {
        private const double SampleIntervalSeconds = 1d;
        private const double StableDurationSeconds = 3d;
        private const double SummaryIntervalSeconds = 10d;
        private const double MaximumDurationSeconds = 180d;
        private const int MaximumTrackedUnits = 64;
        private const int MaximumLogLines = 256;
        private const int MaximumNonTerminalLogLines = MaximumLogLines - 1;
        private const float PositionTolerance = 0.001f;
        private const float RotationToleranceDegrees = 0.1f;

        private readonly Dictionary<ulong, UnitState> _states =
            new Dictionary<ulong, UnitState>();
        private readonly HashSet<UnitType> _observedTypes = new HashSet<UnitType>();
        private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

        private NetworkManager _networkManager;
        private Coroutine _routine;
        private bool _isServer;
        private bool _beginWritten;
        private bool _summaryWritten;
        private bool _overflowReported;
        private double _startedAt;
        private double _lastSampleAt = double.NegativeInfinity;
        private double _lastSummaryAt = double.NegativeInfinity;
        private long _lastBucket = long.MinValue;
        private int _logCount;
        private int _logDropCount;
        private int _stableEndpointEventsObserved;
        private int _stableEndpointLogsWritten;
        private int _stableEvidenceDropCount;
        private int _movementEvidenceDropCount;
        private int _sampleCount;
        private int _errorCount;
        private int _structureErrorCount;
        private int _projectionErrorCount;
        private int _readMutationErrorCount;
        private int _blueCoverage;
        private int _redCoverage;
        private float _maximumPositionError;
        private float _maximumRotationError;
        private string _runId;
        private string _sharedSessionKey;
        private long _sessionStartBucket;

        internal void Initialize(NetworkManager networkManager, bool isServer)
        {
            StopInternal(writeSummary: false);
            _networkManager = networkManager;
            _isServer = isServer;
            _beginWritten = false;
            _summaryWritten = false;
            _overflowReported = false;
            _states.Clear();
            _observedTypes.Clear();
            _logCount = 0;
            _logDropCount = 0;
            _stableEndpointEventsObserved = 0;
            _stableEndpointLogsWritten = 0;
            _stableEvidenceDropCount = 0;
            _movementEvidenceDropCount = 0;
            _sampleCount = 0;
            _errorCount = 0;
            _structureErrorCount = 0;
            _projectionErrorCount = 0;
            _readMutationErrorCount = 0;
            _blueCoverage = 0;
            _redCoverage = 0;
            _maximumPositionError = 0f;
            _maximumRotationError = 0f;
            _lastBucket = long.MinValue;
            _runId = System.Guid.NewGuid().ToString("N");
            _sharedSessionKey = CreateSharedSessionKey();

            if (_networkManager == null || !_networkManager.IsListening)
                return;

            _startedAt = ReadServerTime();
            _sessionStartBucket =
                (long)System.Math.Floor(_startedAt / SampleIntervalSeconds);
            _lastSampleAt = double.NegativeInfinity;
            _lastSummaryAt = _startedAt;
            _routine = StartCoroutine(Observe());
        }

        internal void StopAndLogSummary()
        {
            StopInternal(writeSummary: true);
        }

        private void OnDisable()
        {
            // NetworkCombatController normally stops us while the process-wide
            // LogSessionOwner session is still open. This is a safe fallback for an
            // unexpected component disable; this observer never owns the session.
            StopInternal(writeSummary: true);
        }

        private IEnumerator Observe()
        {
            while (_networkManager != null && _networkManager.IsListening)
            {
                yield return _endOfFrame;

                double serverTime = ReadServerTime();
                if (serverTime - _startedAt >= MaximumDurationSeconds)
                {
                    WriteSummary("duration-limit");
                    _routine = null;
                    yield break;
                }

                long bucket = (long)System.Math.Floor(serverTime / SampleIntervalSeconds);
                if (bucket == _lastBucket
                    || serverTime - _lastSampleAt < SampleIntervalSeconds)
                {
                    continue;
                }

                _lastBucket = bucket;
                _lastSampleAt = serverTime;
                SampleUnits(serverTime, bucket);

                if (serverTime - _lastSummaryAt >= SummaryIntervalSeconds)
                {
                    _lastSummaryAt = serverTime;
                    WriteSummary("periodic");
                }
            }

            WriteSummary("network-stopped");
            _routine = null;
        }

        private void SampleUnits(double serverTime, long bucket)
        {
            NetworkUnit[] units = FindObjectsByType<NetworkUnit>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            System.Array.Sort(
                units,
                (left, right) => left.NetworkObjectId.CompareTo(right.NetworkObjectId));

            if (!_beginWritten
                && units.Any(unit => unit != null && unit.IsSpawned && unit.UnitId >= 0)
                && (_isServer ? !ViewConverter.IsFlipped : ViewConverter.IsFlipped))
            {
                _beginWritten = Log(
                    LogLevel.Info,
                    "BEGIN",
                    $"role={(_isServer ? "host" : "client")}, " +
                    $"isFlipped={ViewConverter.IsFlipped}, " +
                    $"serverTime={FormatDouble(serverTime)}");
            }

            foreach (NetworkUnit unit in units)
            {
                if (unit == null || !unit.IsSpawned || unit.UnitId < 0)
                    continue;

                ulong networkObjectId = unit.NetworkObjectId;
                if (!_states.TryGetValue(networkObjectId, out UnitState state))
                {
                    if (_states.Count >= MaximumTrackedUnits)
                    {
                        if (!_overflowReported)
                        {
                            _overflowReported = true;
                            Log(
                                LogLevel.Warn,
                                "tracking-capacity-reached",
                                $"maxUnits={MaximumTrackedUnits}, serverTime={serverTime:F3}, " +
                                $"sampleBucket={bucket}");
                        }
                        continue;
                    }

                    state = new UnitState
                    {
                        UnitId = unit.UnitId,
                        NetworkObjectId = networkObjectId
                    };
                    _states.Add(networkObjectId, state);
                }

                SampleUnit(unit, state, serverTime, bucket);
            }
        }

        private void SampleUnit(
            NetworkUnit unit,
            UnitState state,
            double serverTime,
            long bucket)
        {
            Transform simulationRoot = unit.transform;
            Vector3 simulationPositionBefore = simulationRoot.position;
            Quaternion simulationRotationBefore = simulationRoot.rotation;
            Vector3 simulationScaleBefore = simulationRoot.localScale;

            VisualRootProjector projector = unit.GetComponent<VisualRootProjector>();
            NetworkTransform networkTransform = unit.GetComponent<NetworkTransform>();
            Transform visualRoot = projector != null ? projector.ObservedVisualRoot : null;
            if (!state.StructureChecked)
            {
                state.StructureChecked = true;
                state.StructureValid = InspectStaticStructure(
                    unit,
                    projector,
                    networkTransform,
                    visualRoot,
                    out state.StructureDetails);
            }
            bool structureValid = state.StructureValid;

            Vector3 actualPosition = visualRoot != null
                ? visualRoot.position
                : simulationPositionBefore;
            Quaternion actualRotation = visualRoot != null
                ? visualRoot.rotation
                : simulationRotationBefore;

            Vector3 expectedPosition = simulationPositionBefore;
            Quaternion expectedRotation = simulationRotationBefore;
            if (!_isServer && ViewConverter.IsFlipped)
            {
                expectedPosition = ViewConverter.ToView(expectedPosition);
                expectedRotation =
                    Quaternion.Euler(0f, 180f, 0f) * expectedRotation;
            }

            float positionError = Vector3.Distance(expectedPosition, actualPosition);
            float rotationError = Quaternion.Angle(expectedRotation, actualRotation);
            _maximumPositionError = Mathf.Max(_maximumPositionError, positionError);
            _maximumRotationError = Mathf.Max(_maximumRotationError, rotationError);

            // Re-read the authoritative root after all calculations. There are no Transform
            // writes above; this check makes an accidental future side effect visible.
            bool simulationRootUnchanged =
                Vector3.Distance(simulationPositionBefore, simulationRoot.position)
                    <= PositionTolerance
                && Quaternion.Angle(simulationRotationBefore, simulationRoot.rotation)
                    <= RotationToleranceDegrees
                && Vector3.Distance(simulationScaleBefore, simulationRoot.localScale)
                    <= PositionTolerance;

            bool movedSincePrevious = false;
            if (state.HasPrevious)
            {
                movedSincePrevious =
                    PositionAxesExceedTolerance(
                        state.PreviousSimulationPosition,
                        simulationPositionBefore)
                    || Quaternion.Angle(
                            state.PreviousSimulationRotation,
                            simulationRotationBefore)
                        > RotationToleranceDegrees;
                bool driftedOutsideStableAnchor =
                    state.HasStableSince
                    && (PositionAxesExceedTolerance(
                            state.StableAnchorPosition,
                            simulationPositionBefore)
                        || Quaternion.Angle(
                                state.StableAnchorRotation,
                                simulationRotationBefore)
                            > RotationToleranceDegrees);
                if (movedSincePrevious || driftedOutsideStableAnchor)
                {
                    movedSincePrevious = true;
                    state.HasStableSince = false;
                    state.AwaitingStableEndpoint = true;
                }
                else if (!state.HasStableSince)
                {
                    state.HasStableSince = true;
                    state.StableSince = state.PreviousSampleServerTime;
                    state.StableAnchorPosition = state.PreviousSimulationPosition;
                    state.StableAnchorRotation = state.PreviousSimulationRotation;
                }
            }
            state.HasPrevious = true;
            state.PreviousSimulationPosition = simulationPositionBefore;
            state.PreviousSimulationRotation = simulationRotationBefore;
            state.PreviousSampleServerTime = serverTime;
            state.EverMoved |= movedSincePrevious;
            double stableDuration = state.HasStableSince
                ? serverTime - state.StableSince
                : 0d;
            bool stable = stableDuration >= StableDurationSeconds;
            state.EverStable |= stable;
            state.SampleCount++;
            _sampleCount++;

            UnitView unitView = unit.GetComponent<UnitView>();
            UnitData data = unitView != null ? unitView.Data : null;
            if (data != null)
            {
                state.Team = data.Team.ToString();
                state.Type = data.Type.ToString();
                _observedTypes.Add(data.Type);
                if (!state.TeamCounted)
                {
                    state.TeamCounted = true;
                    if (data.Team == TeamId.Blue) _blueCoverage++;
                    if (data.Team == TeamId.Red) _redCoverage++;
                }
            }

            bool projectionValid =
                positionError <= PositionTolerance
                && rotationError <= RotationToleranceDegrees;
            bool hasError =
                !structureValid || !projectionValid || !simulationRootUnchanged;
            if (hasError)
            {
                _errorCount++;
                if (!structureValid) _structureErrorCount++;
                if (!projectionValid) _projectionErrorCount++;
                if (!simulationRootUnchanged) _readMutationErrorCount++;
            }

            string correlation =
                $"unitId={unit.UnitId}, networkObjectId={unit.NetworkObjectId}, " +
                $"serverTime={FormatDouble(serverTime)}, sampleBucket={bucket}, " +
                $"isFlipped={ViewConverter.IsFlipped}";
            string networkThresholds = networkTransform != null
                ? $", networkPositionThreshold=" +
                  $"{FormatFloat(networkTransform.PositionThreshold)}, " +
                  $"networkRotationThresholdDeg=" +
                  $"{FormatFloat(networkTransform.RotAngleThreshold)}"
                : ", networkPositionThreshold=unavailable, " +
                  "networkRotationThresholdDeg=unavailable";
            correlation += networkThresholds;
            string poses = FormatPoses(
                simulationPositionBefore,
                simulationRotationBefore,
                actualPosition,
                actualRotation,
                expectedPosition,
                expectedRotation);

            if (!state.InitialLogged)
            {
                state.InitialLogged = true;
                Log(
                    hasError ? LogLevel.Error : LogLevel.Info,
                    "sample",
                    $"{correlation}, team={state.Team}, type={state.Type}, " +
                    $"stable=false, moved=false, crossPeerExactEligible=false, " +
                    $"structureValid={structureValid}, " +
                    $"structure={state.StructureDetails}, " +
                    $"positionError={FormatFloat(positionError)}, " +
                    $"rotationErrorDeg={FormatFloat(rotationError)}, " +
                    $"simulationRootUnchanged={simulationRootUnchanged}, {poses}");
            }

            if (movedSincePrevious && !state.MovedLogged)
            {
                state.MovedLogged = true;
                if (!Log(
                        LogLevel.Info,
                        "movement-coverage",
                        $"{correlation}, team={state.Team}, type={state.Type}, " +
                        "moved=true, crossPeerExactEligible=false"))
                {
                    _movementEvidenceDropCount++;
                }
            }

            if (stable
                && state.AwaitingStableEndpoint
                && !state.StableAfterMoveEndpointObserved)
            {
                _stableEndpointEventsObserved++;
                state.AwaitingStableEndpoint = false;
                state.StableAfterMoveEndpointObserved = true;
                if (Log(
                        LogLevel.Info,
                        "stable-coverage",
                        $"{correlation}, team={state.Team}, type={state.Type}, " +
                        $"stable=true, stableWindowStartServerTime=" +
                        $"{FormatDouble(state.StableSince)}, " +
                        $"stableQualifiedServerTime={FormatDouble(serverTime)}, " +
                        $"stableDurationSeconds={FormatDouble(stableDuration)}, " +
                        "stableEndpoint=1, stableAfterMove=true, " +
                        $"crossPeerExactEligible=true, {poses}"))
                {
                    _stableEndpointLogsWritten++;
                }
                else
                {
                    _stableEvidenceDropCount++;
                }
            }

            if (hasError && !state.ErrorActive)
            {
                state.ErrorActive = true;
                Log(
                    LogLevel.Error,
                    "mismatch",
                    $"{correlation}, team={state.Team}, type={state.Type}, " +
                    $"moving={movedSincePrevious}, crossPeerExactEligible=" +
                    $"{stable.ToString().ToLowerInvariant()}, " +
                    $"structureValid={structureValid}, " +
                    $"structure={state.StructureDetails}, " +
                    $"positionError={FormatFloat(positionError)}, " +
                    $"rotationErrorDeg={FormatFloat(rotationError)}, " +
                    $"simulationRootUnchanged={simulationRootUnchanged}, {poses}");
            }
            else if (!hasError && state.ErrorActive)
            {
                state.ErrorActive = false;
                Log(LogLevel.Info, "mismatch-recovered", correlation);
            }
        }

        private void StopInternal(bool writeSummary)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            if (writeSummary)
                WriteSummary("stopped");
            _networkManager = null;
        }

        private static bool InspectStaticStructure(
            NetworkUnit unit,
            VisualRootProjector projector,
            NetworkTransform networkTransform,
            Transform visualRoot,
            out string details)
        {
            Transform root = unit.transform;
            bool rootUnitView = unit.GetComponent<UnitView>() != null;
            bool rootNetworkObject = unit.GetComponent<NetworkObject>() != null;
            bool rootNetworkUnit = unit.GetComponent<NetworkUnit>() == unit;
            bool rootNetworkTransform =
                networkTransform != null && networkTransform.transform == root;
            bool rootProjector =
                projector != null && projector.transform == root;
            bool visualRootDirectChild =
                visualRoot != null
                && visualRoot.parent == root
                && visualRoot.name == "VisualRoot";
            int directVisualRootCount =
                root.GetComponentsInChildren<Transform>(true)
                    .Count(candidate =>
                        candidate.parent == root
                        && candidate.name == "VisualRoot");

            int visualNetworkObjectCount = visualRoot != null
                ? visualRoot.GetComponentsInChildren<NetworkObject>(true).Length
                : -1;
            int visualNetworkBehaviourCount = visualRoot != null
                ? visualRoot.GetComponentsInChildren<NetworkBehaviour>(true).Length
                : -1;
            int rootAnimatorCount = unit.GetComponents<Animator>().Length;
            int rootRendererCount = unit.GetComponents<Renderer>().Length;

            bool valid =
                rootUnitView
                && rootNetworkObject
                && rootNetworkUnit
                && rootNetworkTransform
                && rootProjector
                && visualRootDirectChild
                && directVisualRootCount == 1
                && visualNetworkObjectCount == 0
                && visualNetworkBehaviourCount == 0
                && rootAnimatorCount == 0
                && rootRendererCount == 0;
            details =
                $"rootUnitView={rootUnitView};rootNetworkObject={rootNetworkObject};" +
                $"rootNetworkUnit={rootNetworkUnit};" +
                $"rootNetworkTransform={rootNetworkTransform};" +
                $"rootProjector={rootProjector};" +
                $"visualRootDirectChild={visualRootDirectChild};" +
                $"directVisualRootCount={directVisualRootCount};" +
                $"visualNetworkObjects={visualNetworkObjectCount};" +
                $"visualNetworkBehaviours={visualNetworkBehaviourCount};" +
                $"rootAnimators={rootAnimatorCount};rootRenderers={rootRendererCount}";
            return valid;
        }

        private void WriteSummary(string reason)
        {
            if (_summaryWritten && reason != "periodic")
                return;

            int movedUnits = _states.Values.Count(state => state.EverMoved);
            int stableUnits = _states.Values.Count(state => state.EverStable);
            int stableAfterMoveEndpointUnits =
                _states.Values.Count(state => state.StableAfterMoveEndpointObserved);
            string types = string.Join(
                ";",
                _observedTypes.Select(type => type.ToString()).OrderBy(value => value));
            bool terminal = reason != "periodic";
            bool trackedCoverage = _states.Count >= 2;
            bool bothTeamsCoverage = _blueCoverage > 0 && _redCoverage > 0;
            bool typeCoverage = _observedTypes.Count >= 2;
            bool movedCoverage = movedUnits >= 2;
            bool stableCoverage = stableUnits >= 2;
            bool stableAfterMoveCoverage = stableAfterMoveEndpointUnits >= 2;
            bool sharedSessionKeyAvailable =
                !string.Equals(
                    _sharedSessionKey,
                    "unavailable",
                    System.StringComparison.Ordinal);
            bool evidenceComplete = _logDropCount == 0;
            bool coveragePassed =
                trackedCoverage
                && bothTeamsCoverage
                && typeCoverage
                && movedCoverage
                && stableCoverage
                && stableAfterMoveCoverage
                && sharedSessionKeyAvailable
                && evidenceComplete
                && _beginWritten;
            bool roleFlipValid =
                _isServer
                    ? !ViewConverter.IsFlipped
                    : ViewConverter.IsFlipped;
            string coverageErrors = string.Join(
                ";",
                new[]
                    {
                        trackedCoverage ? null : "trackedUnits<2",
                        bothTeamsCoverage ? null : "bothTeamsMissing",
                        typeCoverage ? null : "representativeTypes<2",
                        movedCoverage ? null : "movedUnits<2",
                        stableCoverage ? null : "stableUnits<2",
                        stableAfterMoveCoverage
                            ? null
                            : "stableAfterMoveEndpointUnits<2",
                        sharedSessionKeyAvailable
                            ? null
                            : "sharedSessionKeyUnavailable",
                        evidenceComplete ? null : "logDropped",
                        _beginWritten ? null : "beginMissing"
                    }
                    .Where(value => value != null));
            string verdict = _errorCount > 0
                ? "FAIL"
                : coveragePassed && roleFlipValid
                    ? "PASS"
                    : "INCOMPLETE";
            string eventName = terminal ? "summary-END" : "summary-periodic";
            string summary =
                $"reason={reason}, role={(_isServer ? "host" : "client")}, " +
                $"isFlipped={ViewConverter.IsFlipped}, " +
                $"roleFlipValid={roleFlipValid}, beginWritten={_beginWritten}, " +
                $"verdict={verdict}, " +
                $"trackedUnits={_states.Count}, samples={_sampleCount}, errors={_errorCount}, " +
                $"structureErrors={_structureErrorCount}, " +
                $"projectionErrors={_projectionErrorCount}, " +
                $"readMutationErrors={_readMutationErrorCount}, " +
                $"blueUnits={_blueCoverage}, redUnits={_redCoverage}, " +
                $"trackedCoverage={trackedCoverage}, " +
                $"bothTeamsCovered={bothTeamsCoverage}, " +
                $"representativeTypeCount={_observedTypes.Count}, types={types}, " +
                $"typeCoverage={typeCoverage}, movedCoverage={movedCoverage}, " +
                $"stableCoverage={stableCoverage}, " +
                $"movedUnits={movedUnits}, stableUnits={stableUnits}, " +
                $"stableAfterMoveEndpointUnits={stableAfterMoveEndpointUnits}, " +
                $"stableAfterMoveCoverage={stableAfterMoveCoverage}, " +
                $"stableDurationRequiredSeconds={FormatDouble(StableDurationSeconds)}, " +
                $"sharedSessionKeyAvailable={sharedSessionKeyAvailable}, " +
                $"stableEndpointEventsObserved={_stableEndpointEventsObserved}, " +
                $"stableEndpointLogsWritten={_stableEndpointLogsWritten}, " +
                $"logDropCount={_logDropCount}, " +
                $"stableEvidenceDropCount={_stableEvidenceDropCount}, " +
                $"movementEvidenceDropCount={_movementEvidenceDropCount}, " +
                $"evidenceComplete={evidenceComplete}, " +
                $"coveragePassed={coveragePassed}, coverageErrors={coverageErrors}, " +
                $"maxPositionError={FormatFloat(_maximumPositionError)}, " +
                $"maxRotationErrorDeg={FormatFloat(_maximumRotationError)}, " +
                $"movingCrossPeerExactComparisons=0, " +
                $"crossAuditRequired=true, b1OverallPass=false, " +
                $"crossJoin=sharedSessionKey+unitId+networkObjectId+" +
                $"overlappingStableServerTimeWindows, " +
                $"postProcessRule=overlapPoseMismatchIsError+" +
                $"temporalOrphanIsCoverage, " +
                $"logsWritten={_logCount}";

            if (terminal)
            {
                LogLevel terminalLevel = verdict == "FAIL"
                    ? LogLevel.Error
                    : verdict == "INCOMPLETE"
                        ? LogLevel.Warn
                        : LogLevel.Info;
                LogTerminalSummary(terminalLevel, eventName, summary);
            }
            else
                Log(_errorCount > 0 ? LogLevel.Error : LogLevel.Info, eventName, summary);

            if (terminal)
                _summaryWritten = true;
        }

        private double ReadServerTime()
        {
            return _networkManager != null
                ? _networkManager.ServerTime.Time
                : 0d;
        }

        private static string FormatPoses(
            Vector3 simulationPosition,
            Quaternion simulationRotation,
            Vector3 visualPosition,
            Quaternion visualRotation,
            Vector3 expectedVisualPosition,
            Quaternion expectedVisualRotation)
        {
            return
                $"simulationPosition={FormatVector(simulationPosition)}, " +
                $"simulationRotation={FormatQuaternion(simulationRotation)}, " +
                $"visualPosition={FormatVector(visualPosition)}, " +
                $"visualRotation={FormatQuaternion(visualRotation)}, " +
                $"expectedVisualPosition={FormatVector(expectedVisualPosition)}, " +
                $"expectedVisualRotation={FormatQuaternion(expectedVisualRotation)}";
        }

        private static bool PositionAxesExceedTolerance(
            Vector3 left,
            Vector3 right)
        {
            Vector3 delta = left - right;
            return Mathf.Abs(delta.x) > PositionTolerance
                   || Mathf.Abs(delta.y) > PositionTolerance
                   || Mathf.Abs(delta.z) > PositionTolerance;
        }

        private static string FormatVector(Vector3 value)
        {
            return
                $"({FormatFloat(value.x)};{FormatFloat(value.y)};{FormatFloat(value.z)})";
        }

        private static string FormatQuaternion(Quaternion value)
        {
            return
                $"({FormatFloat(value.x)};{FormatFloat(value.y)};" +
                $"{FormatFloat(value.z)};{FormatFloat(value.w)})";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string CreateSharedSessionKey()
        {
            NetworkGameManager gameManager =
                FindFirstObjectByType<NetworkGameManager>();
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

        private bool Log(LogLevel level, string message, string data)
        {
            // Reserve one line for the terminal summary. Even a saturated sampling budget must
            // leave a joinable END record before NetworkCombatController closes RuntimeLogger.
            if (_logCount >= MaximumNonTerminalLogLines)
            {
                _logDropCount++;
                return false;
            }
            _logCount++;
            RuntimeLogger.Log(
                level,
                "Network",
                nameof(UnitRootPoseConsistencyObserver),
                $"[UAS-ROOT-POSE] {message}",
                DecorateData(data));
            return true;
        }

        private void LogTerminalSummary(LogLevel level, string message, string data)
        {
            _logCount++;
            RuntimeLogger.Log(
                level,
                "Network",
                nameof(UnitRootPoseConsistencyObserver),
                $"[UAS-ROOT-POSE] {message}",
                DecorateData(data));
        }

        private string DecorateData(string data)
        {
            return
                $"runId={_runId}, sharedSessionKey={_sharedSessionKey}, " +
                $"sessionStartBucket={_sessionStartBucket}, {data}";
        }

        private sealed class UnitState
        {
            public int UnitId;
            public ulong NetworkObjectId;
            public string Team = "unavailable";
            public string Type = "unavailable";
            public bool HasPrevious;
            public Vector3 PreviousSimulationPosition;
            public Quaternion PreviousSimulationRotation;
            public double PreviousSampleServerTime;
            public bool HasStableSince;
            public double StableSince;
            public Vector3 StableAnchorPosition;
            public Quaternion StableAnchorRotation;
            public bool EverMoved;
            public bool EverStable;
            public bool InitialLogged;
            public bool MovedLogged;
            public bool AwaitingStableEndpoint;
            public bool ErrorActive;
            public bool TeamCounted;
            public int SampleCount;
            public bool StructureChecked;
            public bool StructureValid;
            public string StructureDetails;
            public bool StableAfterMoveEndpointObserved;
        }
    }
}
#endif
