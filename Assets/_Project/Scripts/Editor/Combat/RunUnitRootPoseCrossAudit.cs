using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// Host/Client가 각자 저장한 UAS-ROOT-POSE 증거를 같은 경기 단위로 결합한다.
    /// 런타임 상태를 변경하지 않는 순수 후처리 도구이며 Android Logcat 접두사는 무시한다.
    /// </summary>
    public static class RunUnitRootPoseCrossAudit
    {
        private const string LogRoot = "Assets/_Project/Docs/_Logs";
        private const float PositionTolerancePerAxis = 0.001f;
        private const float RotationToleranceDegrees = 0.1f;
        private const float FloatComparisonEpsilon = 0.000001f;
        private const int MinimumMatchedUnits = 2;
        private const float MinimumMatchedCoverageRatio = 0.5f;

        [MenuItem("Hexiege/Combat/Diagnostics/Analyze Latest Unit Root Pose Logs")]
        public static void Run()
        {
            try
            {
                string hostText = ReadAllLogs("RuntimeLog_host.txt", out int hostFiles);
                string clientText = ReadAllLogs("RuntimeLog_client.txt", out int clientFiles);
                AuditResult result = Analyze(
                    hostText,
                    clientText);
                string message =
                    $"[UAS-ROOT-CROSS-AUDIT]" +
                    $"[{result.Verdict.ToString().ToUpperInvariant()}] " +
                    $"hostFiles={hostFiles}, clientFiles={clientFiles}, {result.Details}";

                if (result.Verdict == AuditVerdict.Pass)
                    Debug.Log(message);
                else if (result.Verdict == AuditVerdict.Fail)
                    Debug.LogError(message);
                else
                    Debug.LogWarning(message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        [MenuItem("Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit")]
        public static void SelfValidate()
        {
            try
            {
                string host = BuildFixture("host-run", "fixture-key", "host", false, 10d, 0f);
                string client =
                    BuildFixture("client-run", "fixture-key", "client", true, 12d, 0.001f);
                RequireVerdict(
                    Analyze(host, client),
                    AuditVerdict.Pass,
                    "overlapping stable windows/1mm");

                RequireVerdict(
                    Analyze(host + "\n" + host, client + "\n" + client),
                    AuditVerdict.Pass,
                    "identical preserved log copies");

                string lateClient =
                    BuildFixture("client-run", "fixture-key", "client", true, 14d, 0f);
                RequireVerdict(
                    Analyze(host, lateClient),
                    AuditVerdict.Inconclusive,
                    "non-overlapping stable windows");

                string hostWithTemporalOrphan = BuildFixture(
                    "host-run",
                    "fixture-key",
                    "host",
                    false,
                    10d,
                    0f,
                    extraEndpointTimes: new[] { 10d });
                string clientWithTemporalOrphan = BuildFixture(
                    "client-run",
                    "fixture-key",
                    "client",
                    true,
                    12d,
                    0.001f,
                    extraEndpointTimes: new[] { 20d });
                RequireVerdict(
                    Analyze(hostWithTemporalOrphan, clientWithTemporalOrphan),
                    AuditVerdict.Pass,
                    "sufficient overlap with temporal orphan");

                string hostWithOneSidedLoss = BuildFixture(
                    "host-run",
                    "fixture-key",
                    "host",
                    false,
                    10d,
                    0f,
                    extraEndpointTimes: new[] { 10d, 10d, 10d });
                AuditResult oneSidedLossResult =
                    Analyze(hostWithOneSidedLoss, client);
                RequireVerdict(
                    oneSidedLossResult,
                    AuditVerdict.Inconclusive,
                    "one-sided evidence reduces union coverage");
                RequireDetails(
                    oneSidedLossResult,
                    "one-sided evidence reduces union coverage",
                    "reason=insufficient-overlapping-stable-window-coverage",
                    "auditableIdentityCount=5",
                    "hostOnlyEndpoints=3");

                string realShapeHost = BuildFixture(
                    "host-run",
                    "fixture-key",
                    "host",
                    false,
                    10d,
                    0f,
                    sharedEndpointCount: 34,
                    extraEndpointTimes: new[] { 10d, 10d, 10d, 10d, 10d });
                string realShapeClient = BuildFixture(
                    "client-run",
                    "fixture-key",
                    "client",
                    true,
                    12d,
                    0.001f,
                    sharedEndpointCount: 34,
                    extraEndpointTimes: new[] { 20d, 20d });
                AuditResult realShapeResult =
                    Analyze(realShapeHost, realShapeClient);
                RequireVerdict(
                    realShapeResult,
                    AuditVerdict.Pass,
                    "real-match-shaped union coverage");
                RequireDetails(
                    realShapeResult,
                    "real-match-shaped union coverage",
                    "matchedUnits=34",
                    "auditableIdentityCount=39",
                    "temporalUnpairedPairs=2",
                    "hostOnlyEndpoints=3");

                string divergentClient =
                    BuildFixture("client-run", "fixture-key", "client", true, 12d, 0.00101f);
                RequireVerdict(
                    Analyze(host, divergentClient),
                    AuditVerdict.Fail,
                    "position threshold");

                string otherMatch =
                    BuildFixture("client-run", "other-key", "client", true, 12d, 0f);
                RequireVerdict(
                    Analyze(host, otherMatch),
                    AuditVerdict.Inconclusive,
                    "shared session key");

                string droppedClient = BuildFixture(
                    "client-run",
                    "fixture-key",
                    "client",
                    true,
                    12d,
                    0f,
                    logDropCount: 1);
                RequireVerdict(
                    Analyze(host, droppedClient),
                    AuditVerdict.Inconclusive,
                    "dropped evidence");

                string failedClient = BuildFixture(
                    "client-run",
                    "fixture-key",
                    "client",
                    true,
                    12d,
                    0f,
                    localVerdict: "FAIL",
                    errors: 1);
                RequireVerdict(
                    Analyze(host, failedClient),
                    AuditVerdict.Fail,
                    "complete local failure");

                string malformedClient = client.Replace(
                    "networkPositionThreshold=0.001000",
                    "networkPositionThreshold=NaN");
                RequireVerdict(
                    Analyze(host, malformedClient),
                    AuditVerdict.Inconclusive,
                    "malformed endpoint");

                string oneUnitHost = host.Replace(
                    "unitId=2, networkObjectId=102",
                    "unitId=1, networkObjectId=101");
                string oneUnitClient = client.Replace(
                    "unitId=2, networkObjectId=102",
                    "unitId=1, networkObjectId=101");
                oneUnitHost = oneUnitHost
                    .Replace("stableEndpointEventsObserved=2", "stableEndpointEventsObserved=1")
                    .Replace("stableEndpointLogsWritten=2", "stableEndpointLogsWritten=1")
                    .Replace("stableAfterMoveEndpointUnits=2", "stableAfterMoveEndpointUnits=1");
                oneUnitClient = oneUnitClient
                    .Replace("stableEndpointEventsObserved=2", "stableEndpointEventsObserved=1")
                    .Replace("stableEndpointLogsWritten=2", "stableEndpointLogsWritten=1")
                    .Replace("stableAfterMoveEndpointUnits=2", "stableAfterMoveEndpointUnits=1");
                RequireVerdict(
                    Analyze(oneUnitHost, oneUnitClient),
                    AuditVerdict.Inconclusive,
                    "minimum distinct units");

                string ambiguousHost =
                    host + "\n" + BuildFixture(
                        "host-run-2",
                        "fixture-key",
                        "host",
                        false,
                        20d,
                        0f);
                string ambiguousClient =
                    client + "\n" + BuildFixture(
                        "client-run-2",
                        "fixture-key",
                        "client",
                        true,
                        22d,
                        0f);
                RequireVerdict(
                    Analyze(ambiguousHost, ambiguousClient),
                    AuditVerdict.Inconclusive,
                    "ambiguous repeated lobby sessions");

                Debug.Log(
                    "[UAS-ROOT-CROSS-AUDIT] self-validation PASS: " +
                    "same-session gate, overlapping/non-overlapping stable windows, " +
                    "temporal/one-sided union coverage, real-match-shaped coverage, " +
                    "per-axis threshold, " +
                    "distinct-unit minimum, ambiguous sessions, " +
                    "dropped/malformed evidence and local failure.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        internal static AuditResult Analyze(string hostText, string clientText)
        {
            List<RunEvidence> hostRuns = Parse(hostText);
            List<RunEvidence> clientRuns = Parse(clientText);
            List<Tuple<RunEvidence, RunEvidence>> candidates =
                (from host in hostRuns
                 from client in clientRuns
                 where IsAvailableKey(host.SharedSessionKey)
                       && string.Equals(
                           host.SharedSessionKey,
                           client.SharedSessionKey,
                           StringComparison.Ordinal)
                 select Tuple.Create(host, client)).ToList();

            if (candidates.Count == 0)
            {
                return AuditResult.Inconclusive(
                    $"reason=no-common-shared-session-key, " +
                    $"hostRuns={hostRuns.Count}, clientRuns={clientRuns.Count}");
            }

            List<PairGateResult> candidateResults = candidates
                .Select(pair => ValidatePairGate(pair.Item1, pair.Item2))
                .ToList();
            List<PairGateResult> completeCandidates = candidateResults
                .Where(result => result.Verdict != AuditVerdict.Inconclusive)
                .ToList();
            if (completeCandidates.Count == 0)
            {
                string gateReasons = string.Join(
                    ";",
                    candidateResults.Select(result =>
                        $"{result.Host.RunId}+{result.Client.RunId}:{result.Reason}"));
                return AuditResult.Inconclusive(
                    $"reason=no-complete-unique-session-pair, candidates={candidates.Count}, " +
                    $"gateReasons={gateReasons}");
            }
            if (completeCandidates.Count != 1)
            {
                return AuditResult.Inconclusive(
                    $"reason=ambiguous-complete-session-pairs, " +
                    $"completeCandidates={completeCandidates.Count}");
            }

            PairGateResult selected = completeCandidates[0];
            RunEvidence hostRun = selected.Host;
            RunEvidence clientRun = selected.Client;
            if (selected.Verdict == AuditVerdict.Fail)
            {
                return AuditResult.Fail(
                    $"sharedSessionKey={hostRun.SharedSessionKey}, " +
                    $"hostRunId={hostRun.RunId}, clientRunId={clientRun.RunId}, " +
                    $"reason={selected.Reason}");
            }

            var identities = new HashSet<UnitIdentity>(hostRun.Endpoints.Keys);
            identities.UnionWith(clientRun.Endpoints.Keys);
            int matchedUnits = 0;
            int identitiesWithBothEndpoints = 0;
            int overlappingWindowPairs = 0;
            int temporalUnpairedPairs = 0;
            int hostOnlyEndpoints = 0;
            int clientOnlyEndpoints = 0;
            int poseMismatches = 0;
            float maximumAxisDelta = 0f;
            float maximumDistance = 0f;
            float maximumRotationDelta = 0f;
            double maximumQualifiedTimeDelta = 0d;

            foreach (UnitIdentity identity in identities.OrderBy(value => value.UnitId)
                         .ThenBy(value => value.NetworkObjectId))
            {
                List<Endpoint> hostEndpoints = GetEndpoints(hostRun, identity);
                List<Endpoint> clientEndpoints = GetEndpoints(clientRun, identity);
                if (hostEndpoints.Count == 0)
                {
                    clientOnlyEndpoints++;
                    continue;
                }
                if (clientEndpoints.Count == 0)
                {
                    hostOnlyEndpoints++;
                    continue;
                }

                identitiesWithBothEndpoints++;
                Endpoint hostEndpoint = hostEndpoints[0];
                Endpoint clientEndpoint = clientEndpoints[0];
                if (!StableWindowsOverlap(hostEndpoint, clientEndpoint))
                {
                    temporalUnpairedPairs++;
                    continue;
                }

                overlappingWindowPairs++;
                Vector3 delta = hostEndpoint.Position - clientEndpoint.Position;
                float axisDelta = MaximumAxis(delta);
                float distance = delta.magnitude;
                float rotationDelta =
                    Quaternion.Angle(hostEndpoint.Rotation, clientEndpoint.Rotation);
                maximumAxisDelta = Mathf.Max(maximumAxisDelta, axisDelta);
                maximumDistance = Mathf.Max(maximumDistance, distance);
                maximumRotationDelta =
                    Mathf.Max(maximumRotationDelta, rotationDelta);
                maximumQualifiedTimeDelta = Math.Max(
                    maximumQualifiedTimeDelta,
                    Math.Abs(hostEndpoint.ServerTime - clientEndpoint.ServerTime));
                if (PoseWithinTolerance(axisDelta, rotationDelta))
                    matchedUnits++;
                else
                    poseMismatches++;
            }

            int auditableIdentityCount = identities.Count;
            float matchedCoverageRatio = auditableIdentityCount == 0
                ? 0f
                : (float)matchedUnits / auditableIdentityCount;
            string metrics =
                $"sharedSessionKey={hostRun.SharedSessionKey}, " +
                $"hostRunId={hostRun.RunId}, clientRunId={clientRun.RunId}, " +
                $"matchedUnits={matchedUnits}, " +
                $"auditableIdentityCount={auditableIdentityCount}, " +
                $"identitiesWithBothEndpoints={identitiesWithBothEndpoints}, " +
                $"matchedCoverageRatio={matchedCoverageRatio:F3}, " +
                $"overlappingWindowPairs={overlappingWindowPairs}, " +
                $"temporalUnpairedPairs={temporalUnpairedPairs}, " +
                $"hostOnlyEndpoints={hostOnlyEndpoints}, " +
                $"clientOnlyEndpoints={clientOnlyEndpoints}, " +
                $"poseMismatches={poseMismatches}, " +
                $"maxQualifiedTimeDeltaSeconds={maximumQualifiedTimeDelta:F3}, " +
                $"maxAxisDelta={maximumAxisDelta:F6}, " +
                $"maxDistance={maximumDistance:F6}, " +
                $"maxRotationDeltaDeg={maximumRotationDelta:F6}, " +
                $"minimumMatchedCoverageRatio={MinimumMatchedCoverageRatio:F3}, " +
                $"positionTolerancePerAxis={PositionTolerancePerAxis:F6}, " +
                $"rotationToleranceDeg={RotationToleranceDegrees:F6}";

            if (poseMismatches != 0)
                return AuditResult.Fail(metrics);
            if (matchedUnits < MinimumMatchedUnits
                || matchedCoverageRatio < MinimumMatchedCoverageRatio)
            {
                return AuditResult.Inconclusive(
                    "reason=insufficient-overlapping-stable-window-coverage, " +
                    metrics);
            }
            return AuditResult.Pass(metrics);
        }

        private static string ReadAllLogs(string fileName, out int fileCount)
        {
            string fullRoot = Path.GetFullPath(LogRoot);
            string[] paths = Directory.GetFiles(
                    fullRoot,
                    fileName,
                    SearchOption.AllDirectories)
                .OrderBy(File.GetLastWriteTimeUtc)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0)
                throw new FileNotFoundException($"{fileName} was not found below {LogRoot}");
            fileCount = paths.Length;
            return string.Join("\n", paths.Select(File.ReadAllText));
        }

        private static string BuildFixture(
            string runId,
            string sharedSessionKey,
            string role,
            bool isFlipped,
            double endpointTime,
            float xOffset,
            int logDropCount = 0,
            string localVerdict = "PASS",
            int errors = 0,
            int sharedEndpointCount = 2,
            IReadOnlyList<double> extraEndpointTimes = null)
        {
            string flip = isFlipped.ToString();
            string offset = xOffset.ToString("F6", CultureInfo.InvariantCulture);
            string time = endpointTime.ToString("F3", CultureInfo.InvariantCulture);
            var lines = new List<string>
            {
                $"[UAS-ROOT-POSE] BEGIN | runId={runId}, " +
                $"sharedSessionKey={sharedSessionKey}, sessionStartBucket=1, " +
                $"role={role}, isFlipped={flip}"
            };
            for (int index = 0; index < sharedEndpointCount; index++)
            {
                int unitId = index + 1;
                lines.Add(BuildFixtureEndpoint(
                    runId,
                    sharedSessionKey,
                    unitId,
                    (ulong)(100 + unitId),
                    time,
                    offset));
            }
            int endpointCount = sharedEndpointCount;
            if (extraEndpointTimes != null)
            {
                for (int index = 0; index < extraEndpointTimes.Count; index++)
                {
                    int unitId = sharedEndpointCount + index + 1;
                    string extraTime = extraEndpointTimes[index].ToString(
                    "F3",
                    CultureInfo.InvariantCulture);
                    lines.Add(BuildFixtureEndpoint(
                        runId,
                        sharedSessionKey,
                        unitId,
                        (ulong)(100 + unitId),
                        extraTime,
                        offset));
                    endpointCount++;
                }
            }
            lines.Add(
                $"[UAS-ROOT-POSE] summary-END | runId={runId}, " +
                $"sharedSessionKey={sharedSessionKey}, sessionStartBucket=1, " +
                $"role={role}, isFlipped={flip}, " +
                $"verdict={localVerdict}, coveragePassed=True, errors={errors}, " +
                $"stableAfterMoveEndpointUnits={endpointCount}, " +
                $"stableEndpointEventsObserved={endpointCount}, " +
                $"stableEndpointLogsWritten={endpointCount}, " +
                $"logDropCount={logDropCount}, stableEvidenceDropCount=0, " +
                $"evidenceComplete={logDropCount == 0}");
            return string.Join("\n", lines);
        }

        private static string BuildFixtureEndpoint(
            string runId,
            string sharedSessionKey,
            int unitId,
            ulong networkObjectId,
            string time,
            string xOffset)
        {
            double qualifiedTime = double.Parse(time, CultureInfo.InvariantCulture);
            string startTime =
                (qualifiedTime - 3d).ToString("F3", CultureInfo.InvariantCulture);
            return
                $"[UAS-ROOT-POSE] stable-coverage | runId={runId}, " +
                $"sharedSessionKey={sharedSessionKey}, sessionStartBucket=1, " +
                $"unitId={unitId}, " +
                $"networkObjectId={networkObjectId}, serverTime={time}, " +
                $"stableWindowStartServerTime={startTime}, " +
                $"stableQualifiedServerTime={time}, stableDurationSeconds=3.000, " +
                $"stableEndpoint=1, stableAfterMove=True, " +
                $"crossPeerExactEligible=true, " +
                "networkPositionThreshold=0.001000, " +
                "networkRotationThresholdDeg=0.100000, " +
                $"simulationPosition=({xOffset};0.000000;0.000000), " +
                "simulationRotation=(0.000000;0.000000;0.000000;1.000000)";
        }

        private static void RequireVerdict(
            AuditResult result,
            AuditVerdict expected,
            string contract)
        {
            if (result.Verdict != expected)
            {
                throw new InvalidOperationException(
                    $"{contract}: expected {expected}, got {result.Verdict}; " +
                    result.Details);
            }
        }

        private static void RequireDetails(
            AuditResult result,
            string contract,
            params string[] expectedFragments)
        {
            foreach (string expectedFragment in expectedFragments)
            {
                if (result.Details.IndexOf(
                        expectedFragment,
                        StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(
                        $"{contract}: missing details '{expectedFragment}'; " +
                        result.Details);
                }
            }
        }

        private static List<RunEvidence> Parse(string text)
        {
            var runs = new Dictionary<string, RunEvidence>(StringComparer.Ordinal);
            string[] lines = (text ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (line.IndexOf("[UAS-ROOT-POSE]", StringComparison.Ordinal) < 0)
                    continue;
                string runId = ReadValue(line, "runId");
                if (string.IsNullOrEmpty(runId))
                    continue;
                if (!runs.TryGetValue(runId, out RunEvidence run))
                {
                    run = new RunEvidence { RunId = runId };
                    runs.Add(runId, run);
                }

                string sharedKey = ReadValue(line, "sharedSessionKey");
                SetInvariant(run, ref run.SharedSessionKey, sharedKey, "sharedSessionKey");
                long? sessionStartBucket = ReadLong(line, "sessionStartBucket");
                SetInvariant(
                    run,
                    ref run.SessionStartBucket,
                    sessionStartBucket,
                    "sessionStartBucket");

                if (line.IndexOf("[UAS-ROOT-POSE] BEGIN", StringComparison.Ordinal) >= 0)
                {
                    int markerIndex = line.IndexOf(
                        "[UAS-ROOT-POSE]",
                        StringComparison.Ordinal);
                    string signature = line.Substring(markerIndex);
                    if (!run.BeginSignatures.Add(signature))
                        continue;
                    run.BeginCount++;
                    run.BeginRole = ReadValue(line, "role");
                    run.BeginIsFlipped = ReadBool(line, "isFlipped");
                }
                else if (line.IndexOf(
                             "[UAS-ROOT-POSE] summary-END",
                             StringComparison.Ordinal) >= 0)
                {
                    int markerIndex = line.IndexOf(
                        "[UAS-ROOT-POSE]",
                        StringComparison.Ordinal);
                    string signature = line.Substring(markerIndex);
                    if (!run.EndSignatures.Add(signature))
                        continue;
                    run.EndCount++;
                    run.EndLine = lineIndex;
                    run.EndRole = ReadValue(line, "role");
                    run.EndIsFlipped = ReadBool(line, "isFlipped");
                    run.LocalVerdict = ReadValue(line, "verdict");
                    run.CoveragePassed = ReadBool(line, "coveragePassed");
                    run.Errors = ReadInt(line, "errors");
                    run.LogDropCount = ReadInt(line, "logDropCount");
                    run.EvidenceComplete = ReadBool(line, "evidenceComplete");
                    run.StableEndpointEventsObserved =
                        ReadInt(line, "stableEndpointEventsObserved");
                    run.StableEndpointLogsWritten =
                        ReadInt(line, "stableEndpointLogsWritten");
                    run.StableEvidenceDropCount =
                        ReadInt(line, "stableEvidenceDropCount");
                    run.StableAfterMoveEndpointUnits =
                        ReadInt(line, "stableAfterMoveEndpointUnits");
                }
                else if (line.IndexOf(
                             "[UAS-ROOT-POSE] stable-coverage",
                             StringComparison.Ordinal) >= 0
                         && ReadBool(line, "stableAfterMove") == true)
                {
                    int markerIndex = line.IndexOf(
                        "[UAS-ROOT-POSE]",
                        StringComparison.Ordinal);
                    string signature = line.Substring(markerIndex);
                    if (!run.EndpointSignatures.Add(signature))
                        continue;
                    Endpoint endpoint;
                    try
                    {
                        endpoint = ParseEndpoint(line);
                    }
                    catch (FormatException)
                    {
                        run.ParseErrorCount++;
                        continue;
                    }
                    if (!run.Endpoints.TryGetValue(
                            endpoint.Identity,
                            out List<Endpoint> endpoints))
                    {
                        endpoints = new List<Endpoint>();
                        run.Endpoints.Add(endpoint.Identity, endpoints);
                    }
                    endpoints.Add(endpoint);
                }
            }

            foreach (RunEvidence run in runs.Values)
            {
                foreach (List<Endpoint> endpoints in run.Endpoints.Values)
                    endpoints.Sort((left, right) => left.ServerTime.CompareTo(right.ServerTime));
            }
            return runs.Values.ToList();
        }

        private static void SetInvariant(
            RunEvidence run,
            ref string current,
            string candidate,
            string field)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                run.ParseErrorCount++;
                return;
            }
            if (current == null)
                current = candidate;
            else if (!string.Equals(current, candidate, StringComparison.Ordinal))
                run.ParseErrorCount++;
        }

        private static void SetInvariant(
            RunEvidence run,
            ref long? current,
            long? candidate,
            string field)
        {
            if (!candidate.HasValue)
            {
                run.ParseErrorCount++;
                return;
            }
            if (!current.HasValue)
                current = candidate;
            else if (current.Value != candidate.Value)
                run.ParseErrorCount++;
        }

        private static Endpoint ParseEndpoint(string line)
        {
            int? unitId = ReadInt(line, "unitId");
            ulong? networkObjectId = ReadULong(line, "networkObjectId");
            double? serverTime = ReadDouble(line, "serverTime");
            double? stableWindowStart =
                ReadDouble(line, "stableWindowStartServerTime");
            double? stableQualified =
                ReadDouble(line, "stableQualifiedServerTime");
            double? stableDuration = ReadDouble(line, "stableDurationSeconds");
            int? stableEndpoint = ReadInt(line, "stableEndpoint");
            bool? exactEligible = ReadBool(line, "crossPeerExactEligible");
            float? positionThreshold = ReadFloat(line, "networkPositionThreshold");
            float? rotationThreshold =
                ReadFloat(line, "networkRotationThresholdDeg");
            Vector3 position = ReadVector3(line, "simulationPosition");
            Quaternion rotation = ReadQuaternion(line, "simulationRotation");
            if (!unitId.HasValue
                || unitId.Value < 0
                || !networkObjectId.HasValue
                || !serverTime.HasValue
                || !IsFinite(serverTime.Value)
                || !stableWindowStart.HasValue
                || !IsFinite(stableWindowStart.Value)
                || !stableQualified.HasValue
                || !IsFinite(stableQualified.Value)
                || !stableDuration.HasValue
                || !IsFinite(stableDuration.Value)
                || stableDuration.Value < 3d
                || stableQualified.Value < stableWindowStart.Value
                || Math.Abs(
                    stableQualified.Value - stableWindowStart.Value
                    - stableDuration.Value) > 0.002d
                || Math.Abs(serverTime.Value - stableQualified.Value) > 0.002d
                || stableEndpoint != 1
                || exactEligible != true
                || !IsFinite(position)
                || !IsFinite(rotation)
                || !IsFinite(QuaternionSquaredMagnitude(rotation))
                || QuaternionSquaredMagnitude(rotation) < 0.5f
                || QuaternionSquaredMagnitude(rotation) > 1.5f
                || !positionThreshold.HasValue
                || !IsFinite(positionThreshold.Value)
                || positionThreshold.Value <= 0f
                || positionThreshold.Value > PositionTolerancePerAxis
                || !rotationThreshold.HasValue
                || !IsFinite(rotationThreshold.Value)
                || rotationThreshold.Value <= 0f
                || rotationThreshold.Value > RotationToleranceDegrees)
            {
                throw new FormatException("stable endpoint contains invalid identity or pose data");
            }

            return new Endpoint
            {
                Identity = new UnitIdentity(unitId.Value, networkObjectId.Value),
                StableWindowStartServerTime = stableWindowStart.Value,
                ServerTime = stableQualified.Value,
                Position = position,
                Rotation = Quaternion.Normalize(rotation)
            };
        }

        private static PairGateResult ValidatePairGate(
            RunEvidence host,
            RunEvidence client)
        {
            PairGateResult Result(AuditVerdict verdict, string reason) =>
                new PairGateResult(host, client, verdict, reason);

            if (host.BeginCount != 1
                || host.EndCount != 1
                || client.BeginCount != 1
                || client.EndCount != 1)
                return Result(AuditVerdict.Inconclusive, "missing-or-duplicate-BEGIN-or-END");
            if (host.ParseErrorCount != 0 || client.ParseErrorCount != 0)
                return Result(AuditVerdict.Inconclusive, "malformed-endpoint-evidence");
            if (!host.SessionStartBucket.HasValue
                || !client.SessionStartBucket.HasValue
                || Math.Abs(
                    host.SessionStartBucket.Value - client.SessionStartBucket.Value) > 2)
                return Result(AuditVerdict.Inconclusive, "session-start-bucket-mismatch");
            if (!string.Equals(
                    host.BeginRole,
                    host.EndRole,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    client.BeginRole,
                    client.EndRole,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    host.BeginRole,
                    "host",
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    client.BeginRole,
                    "client",
                    StringComparison.OrdinalIgnoreCase))
                return Result(AuditVerdict.Inconclusive, "role-mismatch");
            if (host.BeginIsFlipped != host.EndIsFlipped
                || client.BeginIsFlipped != client.EndIsFlipped
                || host.BeginIsFlipped != false
                || client.BeginIsFlipped != true)
                return Result(AuditVerdict.Inconclusive, "flip-contract-mismatch");
            if (!HasCompleteEndpointEvidence(host)
                || !HasCompleteEndpointEvidence(client))
                return Result(
                    AuditVerdict.Inconclusive,
                    "stable-endpoint-count-or-contract-mismatch");
            if (host.LogDropCount != 0 || client.LogDropCount != 0
                || host.EvidenceComplete != true || client.EvidenceComplete != true)
                return Result(AuditVerdict.Inconclusive, "evidence-dropped");
            if (host.Errors.GetValueOrDefault() > 0
                || client.Errors.GetValueOrDefault() > 0
                || string.Equals(host.LocalVerdict, "FAIL", StringComparison.Ordinal)
                || string.Equals(client.LocalVerdict, "FAIL", StringComparison.Ordinal))
                return Result(AuditVerdict.Fail, "complete-local-failure");
            if (!string.Equals(host.LocalVerdict, "PASS", StringComparison.Ordinal)
                || !string.Equals(client.LocalVerdict, "PASS", StringComparison.Ordinal)
                || host.CoveragePassed != true
                || client.CoveragePassed != true
                || !host.Errors.HasValue
                || !client.Errors.HasValue)
                return Result(AuditVerdict.Inconclusive, "local-coverage-incomplete");
            return Result(AuditVerdict.Pass, null);
        }

        private static bool HasCompleteEndpointEvidence(RunEvidence run)
        {
            int parsedEndpointCount = run.Endpoints.Values.Sum(values => values.Count);
            int parsedDistinctUnits = run.Endpoints.Count;
            return run.StableEndpointEventsObserved.HasValue
                   && run.StableEndpointLogsWritten.HasValue
                   && run.StableEvidenceDropCount.HasValue
                   && run.StableAfterMoveEndpointUnits.HasValue
                   && run.StableEndpointEventsObserved.Value == parsedEndpointCount
                   && run.StableEndpointLogsWritten.Value == parsedEndpointCount
                   && run.StableEvidenceDropCount.Value == 0
                   && run.StableAfterMoveEndpointUnits.Value == parsedDistinctUnits
                   && run.Endpoints.Values.All(values => values.Count == 1);
        }

        private static bool StableWindowsOverlap(Endpoint host, Endpoint client)
        {
            double overlapStart = Math.Max(
                host.StableWindowStartServerTime,
                client.StableWindowStartServerTime);
            double overlapEnd = Math.Min(host.ServerTime, client.ServerTime);
            return overlapStart <= overlapEnd + 0.002d;
        }

        private static float MaximumAxis(Vector3 value)
        {
            return Mathf.Max(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }

        private static bool PoseWithinTolerance(
            float maximumAxisDelta,
            float rotationDelta)
        {
            return maximumAxisDelta
                       <= PositionTolerancePerAxis + FloatComparisonEpsilon
                   && rotationDelta
                       <= RotationToleranceDegrees + FloatComparisonEpsilon;
        }

        private static List<Endpoint> GetEndpoints(
            RunEvidence run,
            UnitIdentity identity)
        {
            return run.Endpoints.TryGetValue(identity, out List<Endpoint> endpoints)
                ? endpoints
                : new List<Endpoint>();
        }

        private static bool IsAvailableKey(string value)
        {
            return !string.IsNullOrEmpty(value)
                   && !string.Equals(value, "unavailable", StringComparison.Ordinal);
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z)
            && IsFinite(value.w);

        private static float QuaternionSquaredMagnitude(Quaternion value) =>
            value.x * value.x
            + value.y * value.y
            + value.z * value.z
            + value.w * value.w;

        private static string ReadValue(string line, string key)
        {
            string marker = key + "=";
            int start = line.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return null;
            start += marker.Length;
            int end = line.IndexOf(", ", start, StringComparison.Ordinal);
            return (end < 0 ? line.Substring(start) : line.Substring(start, end - start)).Trim();
        }

        private static bool? ReadBool(string line, string key)
        {
            return bool.TryParse(ReadValue(line, key), out bool value)
                ? value
                : (bool?)null;
        }

        private static int? ReadInt(string line, string key)
        {
            return int.TryParse(
                ReadValue(line, key),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
                ? value
                : (int?)null;
        }

        private static ulong? ReadULong(string line, string key)
        {
            return ulong.TryParse(
                ReadValue(line, key),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out ulong value)
                ? value
                : (ulong?)null;
        }

        private static long? ReadLong(string line, string key)
        {
            return long.TryParse(
                ReadValue(line, key),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long value)
                ? value
                : (long?)null;
        }

        private static double? ReadDouble(string line, string key)
        {
            return double.TryParse(
                ReadValue(line, key),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value)
                ? value
                : (double?)null;
        }

        private static float? ReadFloat(string line, string key)
        {
            return float.TryParse(
                ReadValue(line, key),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                ? value
                : (float?)null;
        }

        private static Vector3 ReadVector3(string line, string key)
        {
            float[] values = ReadTuple(line, key, 3);
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Quaternion ReadQuaternion(string line, string key)
        {
            float[] values = ReadTuple(line, key, 4);
            return new Quaternion(values[0], values[1], values[2], values[3]);
        }

        private static float[] ReadTuple(string line, string key, int count)
        {
            string value = ReadValue(line, key);
            if (string.IsNullOrEmpty(value)
                || value[0] != '('
                || value[value.Length - 1] != ')')
                throw new FormatException($"{key} tuple is missing");
            string[] parts = value.Substring(1, value.Length - 2).Split(';');
            if (parts.Length != count)
                throw new FormatException($"{key} expected {count} values");
            return parts.Select(part => float.Parse(
                part,
                NumberStyles.Float,
                CultureInfo.InvariantCulture)).ToArray();
        }

        internal enum AuditVerdict
        {
            Pass,
            Fail,
            Inconclusive
        }

        internal sealed class AuditResult
        {
            public AuditVerdict Verdict { get; private set; }
            public string Details { get; private set; }

            public static AuditResult Pass(string details) =>
                Create(AuditVerdict.Pass, details);
            public static AuditResult Fail(string details) =>
                Create(AuditVerdict.Fail, details);
            public static AuditResult Inconclusive(string details) =>
                Create(AuditVerdict.Inconclusive, details);

            private static AuditResult Create(AuditVerdict verdict, string details)
            {
                return new AuditResult { Verdict = verdict, Details = details };
            }
        }

        private sealed class RunEvidence
        {
            public string RunId;
            public string SharedSessionKey;
            public long? SessionStartBucket;
            public string BeginRole;
            public string EndRole;
            public bool? BeginIsFlipped;
            public bool? EndIsFlipped;
            public int BeginCount;
            public int EndCount;
            public int EndLine;
            public int ParseErrorCount;
            public string LocalVerdict;
            public bool? CoveragePassed;
            public int? Errors;
            public int? LogDropCount;
            public bool? EvidenceComplete;
            public int? StableEndpointEventsObserved;
            public int? StableEndpointLogsWritten;
            public int? StableEvidenceDropCount;
            public int? StableAfterMoveEndpointUnits;
            public readonly Dictionary<UnitIdentity, List<Endpoint>> Endpoints =
                new Dictionary<UnitIdentity, List<Endpoint>>();
            public readonly HashSet<string> BeginSignatures =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> EndSignatures =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> EndpointSignatures =
                new HashSet<string>(StringComparer.Ordinal);
        }

        private struct UnitIdentity : IEquatable<UnitIdentity>
        {
            public readonly int UnitId;
            public readonly ulong NetworkObjectId;

            public UnitIdentity(int unitId, ulong networkObjectId)
            {
                UnitId = unitId;
                NetworkObjectId = networkObjectId;
            }

            public bool Equals(UnitIdentity other) =>
                UnitId == other.UnitId && NetworkObjectId == other.NetworkObjectId;
            public override bool Equals(object obj) =>
                obj is UnitIdentity other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return (UnitId * 397) ^ NetworkObjectId.GetHashCode();
                }
            }
        }

        private sealed class Endpoint
        {
            public UnitIdentity Identity;
            public double StableWindowStartServerTime;
            public double ServerTime;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private sealed class PairGateResult
        {
            public readonly RunEvidence Host;
            public readonly RunEvidence Client;
            public readonly AuditVerdict Verdict;
            public readonly string Reason;

            public PairGateResult(
                RunEvidence host,
                RunEvidence client,
                AuditVerdict verdict,
                string reason)
            {
                Host = host;
                Client = client;
                Verdict = verdict;
                Reason = reason;
            }
        }

    }
}
