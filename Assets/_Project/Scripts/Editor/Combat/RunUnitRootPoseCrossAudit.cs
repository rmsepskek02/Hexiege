using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Infrastructure;
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
        private const double MaximumMovementStartedAtDeltaSeconds = 2d;
        private const string RotationEvidenceSchema =
            "root-rotation-replication-v1";

        [MenuItem("Hexiege/Combat/Diagnostics/Analyze Latest Unit Root Pose Logs")]
        public static void Run()
        {
            // RuntimeLog.txt는 Play Mode 동안 LogSessionOwner가 계속 기록한다. 기록 중인 파일을
            // 교차감사기가 동시에 읽으면 공유 위반 또는 길이가 달라진 스냅샷이 생길 수 있으므로,
            // 분석은 로그 기록이 끝난 Edit Mode에서만 허용한다.
            if (EditorApplication.isPlaying
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[UAS-ROOT-CROSS-AUDIT][INCONCLUSIVE] " +
                    "reason=play-mode-active-stop-play-mode-before-analysis, " +
                    "action=Stop Play Mode and run Analyze Latest Unit Root Pose Logs again.");
                return;
            }

            try
            {
                IReadOnlyList<LogFileInput> files = DiscoverLogFiles();
                DiscoveryAudit discovery = AnalyzeDiscoveredLogs(files);
                AuditResult result = discovery.Result;
                string message =
                    $"[UAS-ROOT-CROSS-AUDIT]" +
                    $"[{result.Verdict.ToString().ToUpperInvariant()}] " +
                    $"discoveredFiles={files.Count}, hostFiles={discovery.HostFiles}, " +
                    $"clientFiles={discovery.ClientFiles}, {result.Details}";

                if (result.Verdict == AuditVerdict.Pass)
                    Debug.Log(message);
                else if (result.Verdict == AuditVerdict.Fail)
                    Debug.LogError(message);
                else
                    Debug.LogWarning(message);
            }
            catch (IOException exception)
            {
                // Edit Mode 전환 직후 다른 도구가 로그를 잠깐 점유해도 예외를 재전파하지 않는다.
                // 로그를 바꾸지 않고 잠시 후 같은 메뉴를 다시 실행할 수 있도록 fail-closed 한다.
                Debug.LogWarning(
                    "[UAS-ROOT-CROSS-AUDIT][INCONCLUSIVE] " +
                    "reason=log-file-read-io-failure, " +
                    $"exceptionType={exception.GetType().Name}, " +
                    "action=Wait for file import/write completion and run analysis again.");
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

                string rotationDivergentClient = ReplaceValueOnFirstLineContaining(
                    client,
                    "[UAS-ROOT-POSE] stable-coverage",
                    "simulationRotation=(0.000000;0.000000;0.000000;1.000000)",
                    "simulationRotation=(0.000000;0.026177;0.000000;0.999657)");
                rotationDivergentClient = ReplaceValueOnFirstLineContaining(
                    rotationDivergentClient,
                    "[UAS-ROOT-POSE] rotation-replication-evidence",
                    "rootRotation=(0.000000;0.000000;0.000000;1.000000)",
                    "rootRotation=(0.000000;0.026177;0.000000;0.999657)");
                AuditResult rotationDivergence = Analyze(host, rotationDivergentClient);
                RequireVerdict(
                    rotationDivergence,
                    AuditVerdict.Fail,
                    "same-revision rotation replication divergence");
                RequireDetails(
                    rotationDivergence,
                    "same-revision rotation replication divergence",
                    "rotationRevisionMatchedMismatches=1",
                    "rotationRevisionDivergentMismatches=0");

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

                string mismatchedStableBucketClient =
                    ReplaceValueOnFirstLineContaining(
                        client,
                        "[UAS-ROOT-POSE] stable-coverage",
                        "sessionStartBucket=1",
                        "sessionStartBucket=2");
                AuditResult mismatchedStableBucket =
                    Analyze(host, mismatchedStableBucketClient);
                RequireVerdict(
                    mismatchedStableBucket,
                    AuditVerdict.Inconclusive,
                    "ROOT stable bucket internal invariant");
                RequireDetails(
                    mismatchedStableBucket,
                    "ROOT stable bucket internal invariant",
                    "reason=no-complete-unique-session-pair",
                    "gateReasons=host-run+client-run:malformed-endpoint-evidence");

                string mismatchedEndBucketClient =
                    ReplaceValueOnFirstLineContaining(
                        client,
                        "[UAS-ROOT-POSE] summary-END",
                        "sessionStartBucket=1",
                        "sessionStartBucket=2");
                AuditResult mismatchedEndBucket =
                    Analyze(host, mismatchedEndBucketClient);
                RequireVerdict(
                    mismatchedEndBucket,
                    AuditVerdict.Inconclusive,
                    "ROOT END bucket internal invariant");
                RequireDetails(
                    mismatchedEndBucket,
                    "ROOT END bucket internal invariant",
                    "reason=no-complete-unique-session-pair",
                    "gateReasons=host-run+client-run:malformed-endpoint-evidence");

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

                var editorHostDeviceClient = new[]
                {
                    new LogFileInput(
                        "_editor/fixture/RuntimeLog.txt",
                        host),
                    new LogFileInput(
                        "device/RuntimeLog_device_20260823_120000.txt",
                        client)
                };
                RequireVerdict(
                    AnalyzeDiscoveredLogs(editorHostDeviceClient).Result,
                    AuditVerdict.Pass,
                    "content role discovery");

                var editorClientDeviceHost = new[]
                {
                    new LogFileInput(
                        "_editor/fixture/RuntimeLog.txt",
                        client),
                    new LogFileInput(
                        "device/RuntimeLog_device7.txt",
                        host)
                };
                RequireVerdict(
                    AnalyzeDiscoveredLogs(editorClientDeviceHost).Result,
                    AuditVerdict.Pass,
                    "role swap and device suffix discovery");

                // 실제 경기에서 ROOT 관찰기는 양측에서 서로 다른 프레임에 시작할 수 있다.
                // ROOT bucket(14/17)이 달라도 권위 MOVE lifecycle 시작이 0.733149초 차이고
                // stable window가 겹치면 동일 경기로 판정해야 한다.
                string delayedRootBucketHost = BuildFixture(
                    "delayed-root-host",
                    "delayed-root-key",
                    "host",
                    false,
                    10d,
                    0f,
                    sessionStartBucket: 14,
                    movementStartedAt: 18.284286d);
                string delayedRootBucketClient = BuildFixture(
                    "delayed-root-client",
                    "delayed-root-key",
                    "client",
                    true,
                    12d,
                    0f,
                    sessionStartBucket: 17,
                    movementStartedAt: 19.017435d);
                RequireVerdict(
                    AnalyzeDiscoveredLogs(new[]
                    {
                        new LogFileInput("_editor/a/RuntimeLog.txt", delayedRootBucketHost),
                        new LogFileInput(
                            "device/RuntimeLog_device.txt",
                            delayedRootBucketClient)
                    }).Result,
                    AuditVerdict.Pass,
                    "asynchronous ROOT start with near MOVE lifecycle start");

                string movementBoundaryHost = BuildFixture(
                    "movement-boundary-host",
                    "movement-boundary-key",
                    "host",
                    false,
                    10d,
                    0f,
                    sessionStartBucket: 14,
                    movementStartedAt: 18d);
                string movementBoundaryClient = BuildFixture(
                    "movement-boundary-client",
                    "movement-boundary-key",
                    "client",
                    true,
                    12d,
                    0f,
                    sessionStartBucket: 17,
                    movementStartedAt: 20d);
                RequireVerdict(
                    AnalyzeDiscoveredLogs(new[]
                    {
                        new LogFileInput("_editor/a/RuntimeLog.txt", movementBoundaryHost),
                        new LogFileInput("device/RuntimeLog_device.txt", movementBoundaryClient)
                    }).Result,
                    AuditVerdict.Pass,
                    "2.000 second MOVE lifecycle boundary");

                string movementOutsideBoundaryClient = BuildFixture(
                    "movement-outside-boundary-client",
                    "movement-boundary-key",
                    "client",
                    true,
                    12d,
                    0f,
                    sessionStartBucket: 17,
                    movementStartedAt: 20.001d);
                AuditResult movementOutsideBoundary = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput("_editor/a/RuntimeLog.txt", movementBoundaryHost),
                    new LogFileInput(
                        "device/RuntimeLog_device.txt",
                        movementOutsideBoundaryClient)
                }).Result;
                RequireVerdict(
                    movementOutsideBoundary,
                    AuditVerdict.Inconclusive,
                    "2.001 second MOVE lifecycle boundary");
                RequireDetails(
                    movementOutsideBoundary,
                    "2.001 second MOVE lifecycle boundary",
                    "reason=no-complete-unique-session-pair",
                    "gateReasons=movement-boundary-host+" +
                    "movement-outside-boundary-client:" +
                    "movement-observer-startedAt-mismatch");

                string dailySwapClient = BuildFixture(
                    "daily-swap-client",
                    "daily-swap-key",
                    "client",
                    true,
                    22d,
                    0f);
                string dailySwapHost = BuildFixture(
                    "daily-swap-host",
                    "daily-swap-key",
                    "host",
                    false,
                    20d,
                    0f);
                DiscoveryAudit mixedRoleDailyFile = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput(
                        "_editor/fixture/RuntimeLog.txt",
                        host + "\n" + dailySwapClient),
                    new LogFileInput(
                        "device/RuntimeLog_device.txt",
                        dailySwapHost)
                });
                RequireVerdict(
                    mixedRoleDailyFile.Result,
                    AuditVerdict.Pass,
                    "mixed role daily append file");

                DiscoveryAudit editorOnly = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput(
                        "_editor/fixture/RuntimeLog.txt",
                        host + "\n" + client)
                });
                RequireVerdict(
                    editorOnly.Result,
                    AuditVerdict.Inconclusive,
                    "device anchor required");
                RequireDetails(
                    editorOnly.Result,
                    "device anchor required",
                    "reason=no-device-anchor");

                DiscoveryAudit duplicateHost = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput("_editor/a/RuntimeLog.txt", host),
                    new LogFileInput("_editor/b/RuntimeLog.txt", host),
                    new LogFileInput("device/RuntimeLog_device.txt", client)
                });
                RequireVerdict(
                    duplicateHost.Result,
                    AuditVerdict.Inconclusive,
                    "duplicate role/session discovery");
                RequireDetails(
                    duplicateHost.Result,
                    "duplicate role/session discovery",
                    "reason=duplicate-role-session-files");

                DiscoveryAudit mismatchedSession = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput("_editor/a/RuntimeLog.txt", host),
                    new LogFileInput("device/RuntimeLog_device.txt", otherMatch)
                });
                RequireVerdict(
                    mismatchedSession.Result,
                    AuditVerdict.Inconclusive,
                    "discovered same-session gate");

                string latestHost = BuildFixture(
                    "latest-host",
                    "latest-key",
                    "host",
                    false,
                    30d,
                    0f);
                string latestClient = BuildFixture(
                    "latest-client",
                    "latest-key",
                    "client",
                    true,
                    32d,
                    0f);
                IReadOnlyList<LogFileInput> latestSelection = SelectLatestCapture(new[]
                {
                    new LogFileInput("_editor/day/RuntimeLog.txt", host + "\n" + latestHost, 1),
                    new LogFileInput("device/RuntimeLog_device_old.txt", client, 2),
                    new LogFileInput("device/RuntimeLog_device_latest.txt", latestClient, 3)
                });
                RequireVerdict(
                    AnalyzeDiscoveredLogs(latestSelection).Result,
                    AuditVerdict.Pass,
                    "latest device capture anchor");

                string schemaMismatchClient = client.Replace(
                    UnitMovementAuthorityObserver.ProductionSchema,
                    "b3-movement-authority-v8");
                DiscoveryAudit schemaMismatch = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput("_editor/a/RuntimeLog.txt", host),
                    new LogFileInput("device/RuntimeLog_device.txt", schemaMismatchClient)
                });
                RequireVerdict(
                    schemaMismatch.Result,
                    AuditVerdict.Inconclusive,
                    "movement observer schema mismatch");
                RequireDetails(
                    schemaMismatch.Result,
                    "movement observer schema mismatch",
                    "reason=movement-observer-schema-mismatch");

                string equallyStaleHost = host.Replace(
                    UnitMovementAuthorityObserver.ProductionSchema,
                    "b3-movement-authority-v8");
                string equallyStaleClient = client.Replace(
                    UnitMovementAuthorityObserver.ProductionSchema,
                    "b3-movement-authority-v8");
                DiscoveryAudit equallyStaleSchema = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput("_editor/a/RuntimeLog.txt", equallyStaleHost),
                    new LogFileInput("device/RuntimeLog_device.txt", equallyStaleClient)
                });
                RequireVerdict(
                    equallyStaleSchema.Result,
                    AuditVerdict.Inconclusive,
                    "equal but stale movement observer schema");
                RequireDetails(
                    equallyStaleSchema.Result,
                    "equal but stale movement observer schema",
                    "reason=non-production-movement-observer-schema");

                string missingSchemaClient = string.Join(
                    "\n",
                    client.Split(new[] { "\n" }, StringSplitOptions.None)
                        .Where(line => line.IndexOf(
                            "[UAS-MOVE-AUTH] BEGIN",
                            StringComparison.Ordinal) < 0));
                DiscoveryAudit missingSchema = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput("_editor/a/RuntimeLog.txt", host),
                    new LogFileInput("device/RuntimeLog_device.txt", missingSchemaClient)
                });
                RequireVerdict(
                    missingSchema.Result,
                    AuditVerdict.Inconclusive,
                    "movement observer schema missing");
                RequireDetails(
                    missingSchema.Result,
                    "movement observer schema missing",
                    "reason=missing-or-ambiguous-movement-observer-schema");

                var malformedMovementStartedAtCases = new[]
                {
                    new
                    {
                        Marker = "[UAS-MOVE-AUTH] BEGIN",
                        Replacement = string.Empty,
                        Contract = "MOVE BEGIN missing startedAt",
                        Reason = "reason=malformed-movement-observer-schema"
                    },
                    new
                    {
                        Marker = "[UAS-MOVE-AUTH] BEGIN",
                        Replacement = "startedAt=NaN, ",
                        Contract = "MOVE BEGIN NaN startedAt",
                        Reason = "reason=malformed-movement-observer-schema"
                    },
                    new
                    {
                        Marker = "[UAS-MOVE-AUTH] BEGIN",
                        Replacement = "startedAt=Infinity, ",
                        Contract = "MOVE BEGIN Infinity startedAt",
                        Reason = "reason=malformed-movement-observer-schema"
                    },
                    new
                    {
                        Marker = "[UAS-MOVE-AUTH] END",
                        Replacement = string.Empty,
                        Contract = "MOVE END missing startedAt",
                        Reason = "reason=mismatched-movement-observer-lifecycle"
                    },
                    new
                    {
                        Marker = "[UAS-MOVE-AUTH] END",
                        Replacement = "startedAt=NaN, ",
                        Contract = "MOVE END NaN startedAt",
                        Reason = "reason=mismatched-movement-observer-lifecycle"
                    },
                    new
                    {
                        Marker = "[UAS-MOVE-AUTH] END",
                        Replacement = "startedAt=Infinity, ",
                        Contract = "MOVE END Infinity startedAt",
                        Reason = "reason=mismatched-movement-observer-lifecycle"
                    }
                };
                foreach (var malformedStartedAtCase in malformedMovementStartedAtCases)
                {
                    string malformedStartedAtClient =
                        ReplaceValueOnFirstLineContaining(
                            client,
                            malformedStartedAtCase.Marker,
                            "startedAt=1.000000, ",
                            malformedStartedAtCase.Replacement);
                    AuditResult malformedStartedAtResult = AnalyzeDiscoveredLogs(new[]
                    {
                        new LogFileInput("_editor/a/RuntimeLog.txt", host),
                        new LogFileInput(
                            "device/RuntimeLog_device.txt",
                            malformedStartedAtClient)
                    }).Result;
                    RequireVerdict(
                        malformedStartedAtResult,
                        AuditVerdict.Inconclusive,
                        malformedStartedAtCase.Contract);
                    RequireDetails(
                        malformedStartedAtResult,
                        malformedStartedAtCase.Contract,
                        malformedStartedAtCase.Reason);
                }

                string truncatedMovementEnd = client.Replace(
                    ", verdict=EVIDENCE",
                    string.Empty);
                DiscoveryAudit truncatedMovementLifecycle = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput("_editor/a/RuntimeLog.txt", host),
                    new LogFileInput("device/RuntimeLog_device.txt", truncatedMovementEnd)
                });
                RequireVerdict(
                    truncatedMovementLifecycle.Result,
                    AuditVerdict.Inconclusive,
                    "truncated movement observer END");
                RequireDetails(
                    truncatedMovementLifecycle.Result,
                    "truncated movement observer END",
                    "reason=malformed-movement-observer-terminal");

                string mismatchedMovementEnd = client.Replace(
                    "[UAS-MOVE-AUTH] END | runId=move-client-run,",
                    "[UAS-MOVE-AUTH] END | runId=move-other-run,");
                DiscoveryAudit mismatchedMovementLifecycle = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput("_editor/a/RuntimeLog.txt", host),
                    new LogFileInput("device/RuntimeLog_device.txt", mismatchedMovementEnd)
                });
                RequireVerdict(
                    mismatchedMovementLifecycle.Result,
                    AuditVerdict.Inconclusive,
                    "mismatched movement observer lifecycle");
                RequireDetails(
                    mismatchedMovementLifecycle.Result,
                    "mismatched movement observer lifecycle",
                    "reason=mismatched-movement-observer-lifecycle");

                string failedMovementEnd = client.Replace(
                    ", verdict=EVIDENCE",
                    ", verdict=FAIL");
                DiscoveryAudit failedMovementLifecycle = AnalyzeDiscoveredLogs(new[]
                {
                    new LogFileInput("_editor/a/RuntimeLog.txt", host),
                    new LogFileInput("device/RuntimeLog_device.txt", failedMovementEnd)
                });
                RequireVerdict(
                    failedMovementLifecycle.Result,
                    AuditVerdict.Fail,
                    "movement observer FAIL END");
                RequireDetails(
                    failedMovementLifecycle.Result,
                    "movement observer FAIL END",
                    "reason=movement-observer-terminal-fail");

                Debug.Log(
                    "[UAS-ROOT-CROSS-AUDIT] self-validation PASS: " +
                    "file discovery/content role swap/device suffix and mandatory device anchor, " +
                    "compact Android-safe ROOT terminal contract, " +
                    "same-session gate with asynchronous ROOT start and 2.000/2.001s MOVE boundary, " +
                    "ROOT bucket internal invariants, duplicate/ambiguous role, " +
                    "movement schema/lifecycle and missing/non-finite startedAt fail-closed, " +
                    "overlapping/non-overlapping stable windows, " +
                    "temporal/one-sided union coverage, real-match-shaped coverage, " +
                    "per-axis threshold, bounded rotation evidence lifecycle and " +
                    "same-revision versus revision-lag mismatch classification, " +
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
            return AnalyzeRuns(hostRuns, clientRuns);
        }

        private static AuditResult AnalyzeRuns(
            IReadOnlyList<RunEvidence> hostRuns,
            IReadOnlyList<RunEvidence> clientRuns,
            bool requireDeviceEditorPair = false)
        {
            List<Tuple<RunEvidence, RunEvidence>> candidates =
                (from host in hostRuns
                 from client in clientRuns
                 where IsAvailableKey(host.SharedSessionKey)
                       && string.Equals(
                           host.SharedSessionKey,
                           client.SharedSessionKey,
                           StringComparison.Ordinal)
                       && (!requireDeviceEditorPair
                           || host.SourceIsDevice != client.SourceIsDevice)
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
            int rotationRevisionMatchedMismatches = 0;
            int rotationRevisionDivergentMismatches = 0;
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
                {
                    poseMismatches++;
                    if (rotationDelta
                        > RotationToleranceDegrees + FloatComparisonEpsilon)
                    {
                        RotationReplicationEvidence hostRotation =
                            hostRun.RotationEvidence[identity][0];
                        RotationReplicationEvidence clientRotation =
                            clientRun.RotationEvidence[identity][0];
                        if (hostRotation.HasSameMovementRevision(clientRotation))
                            rotationRevisionMatchedMismatches++;
                        else
                            rotationRevisionDivergentMismatches++;
                    }
                }
            }

            int auditableIdentityCount = identities.Count;
            float matchedCoverageRatio = auditableIdentityCount == 0
                ? 0f
                : (float)matchedUnits / auditableIdentityCount;
            string movementStartedAtDelta =
                hostRun.MovementObserverStartedAt.HasValue
                && clientRun.MovementObserverStartedAt.HasValue
                    ? Math.Abs(
                            hostRun.MovementObserverStartedAt.Value
                            - clientRun.MovementObserverStartedAt.Value)
                        .ToString("F6", CultureInfo.InvariantCulture)
                    : "not-required";
            string metrics =
                $"sharedSessionKey={hostRun.SharedSessionKey}, " +
                $"hostRunId={hostRun.RunId}, clientRunId={clientRun.RunId}, " +
                $"movementStartedAtDeltaSeconds={movementStartedAtDelta}, " +
                $"matchedUnits={matchedUnits}, " +
                $"auditableIdentityCount={auditableIdentityCount}, " +
                $"identitiesWithBothEndpoints={identitiesWithBothEndpoints}, " +
                $"matchedCoverageRatio={matchedCoverageRatio:F3}, " +
                $"overlappingWindowPairs={overlappingWindowPairs}, " +
                $"temporalUnpairedPairs={temporalUnpairedPairs}, " +
                $"hostOnlyEndpoints={hostOnlyEndpoints}, " +
                $"clientOnlyEndpoints={clientOnlyEndpoints}, " +
                $"poseMismatches={poseMismatches}, " +
                $"rotationRevisionMatchedMismatches=" +
                $"{rotationRevisionMatchedMismatches}, " +
                $"rotationRevisionDivergentMismatches=" +
                $"{rotationRevisionDivergentMismatches}, " +
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

        private static IReadOnlyList<LogFileInput> DiscoverLogFiles()
        {
            string fullRoot = Path.GetFullPath(LogRoot);
            if (!Directory.Exists(fullRoot))
                throw new DirectoryNotFoundException(LogRoot);

            string[] paths = Directory.GetFiles(
                    fullRoot,
                    "RuntimeLog*.txt",
                    SearchOption.AllDirectories)
                .Where(IsSupportedLogPath)
                .OrderBy(File.GetLastWriteTimeUtc)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0)
            {
                throw new FileNotFoundException(
                    $"RuntimeLog.txt or RuntimeLog_device*.txt was not found below {LogRoot}");
            }
            return SelectLatestCapture(paths
                .Select(path => new LogFileInput(
                    path,
                    ReadSharedLogSnapshot(path),
                    File.GetLastWriteTimeUtc(path).Ticks))
                .ToArray());
        }

        private static string ReadSharedLogSnapshot(string path)
        {
            // Edit Mode 전환 직후 OS/Unity가 파일 핸들을 정리하는 짧은 구간에도 읽을 수 있게
            // 쓰기/삭제 공유를 허용한다. Play Mode 자체는 Run()에서 먼저 차단한다.
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, true))
            {
                return reader.ReadToEnd();
            }
        }

        private static IReadOnlyList<LogFileInput> SelectLatestCapture(
            IReadOnlyList<LogFileInput> files)
        {
            var selected = files
                .Where(file => string.Equals(
                    Path.GetFileName(file.Path),
                    "RuntimeLog.txt",
                    StringComparison.Ordinal))
                .ToList();
            LogFileInput latestDevice = files
                .Where(file => Path.GetFileName(file.Path).StartsWith(
                    "RuntimeLog_device",
                    StringComparison.Ordinal))
                .OrderBy(file => file.ModifiedUtcTicks)
                .ThenBy(file => file.Path, StringComparer.Ordinal)
                .LastOrDefault();
            if (latestDevice != null)
                selected.Add(latestDevice);
            return selected;
        }

        private static bool IsSupportedLogPath(string path)
        {
            string fileName = Path.GetFileName(path);
            if (fileName.StartsWith("RuntimeLog_device", StringComparison.Ordinal)
                && fileName.EndsWith(".txt", StringComparison.Ordinal))
                return true;
            if (!string.Equals(fileName, "RuntimeLog.txt", StringComparison.Ordinal))
                return false;

            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/_editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static DiscoveryAudit AnalyzeDiscoveredLogs(
            IReadOnlyList<LogFileInput> files)
        {
            if (files == null || files.Count == 0)
            {
                return DiscoveryAudit.Create(
                    AuditResult.Inconclusive("reason=no-supported-log-files"),
                    0,
                    0);
            }

            var hostRuns = new List<RunEvidence>();
            var clientRuns = new List<RunEvidence>();
            var roleSessionSources = new Dictionary<string, string>(StringComparer.Ordinal);
            var schemasByRoleSession = new Dictionary<string, string>(StringComparer.Ordinal);
            var anchorSharedSessionKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (LogFileInput file in files)
            {
                if (!Path.GetFileName(file.Path).StartsWith(
                        "RuntimeLog_device",
                        StringComparison.Ordinal))
                    continue;
                foreach (RunEvidence run in Parse(file.Text))
                {
                    if (IsAvailableKey(run.SharedSessionKey))
                        anchorSharedSessionKeys.Add(run.SharedSessionKey);
                }
            }
            if (anchorSharedSessionKeys.Count == 0)
            {
                return DiscoveryAudit.Create(
                    AuditResult.Inconclusive("reason=no-device-anchor"),
                    0,
                    0);
            }
            int hostFiles = 0;
            int clientFiles = 0;
            foreach (LogFileInput file in files)
            {
                if (file == null || string.IsNullOrEmpty(file.Path))
                {
                    return DiscoveryAudit.Create(
                        AuditResult.Inconclusive("reason=invalid-discovered-file"),
                        hostFiles,
                        clientFiles);
                }

                List<RunEvidence> runs = Parse(file.Text)
                    .Where(run => anchorSharedSessionKeys.Contains(run.SharedSessionKey))
                    .ToList();
                if (runs.Count == 0)
                    continue;
                bool sourceIsDevice = Path.GetFileName(file.Path).StartsWith(
                    "RuntimeLog_device",
                    StringComparison.Ordinal);
                foreach (RunEvidence run in runs)
                {
                    run.SourceIsDevice = sourceIsDevice;
                }

                Dictionary<string, string> fileSchemas;
                Dictionary<string, double> fileMovementStartedAt;
                bool movementObserverTerminalFailure;
                string schemaError = ParseMovementObserverSchemas(
                    file.Text,
                    anchorSharedSessionKeys,
                    out fileSchemas,
                    out fileMovementStartedAt,
                    out movementObserverTerminalFailure);
                if (schemaError != null)
                {
                    return DiscoveryAudit.Create(
                        AuditResult.Inconclusive(
                            $"reason={schemaError}, file={file.Path}"),
                        hostFiles,
                        clientFiles);
                }
                if (movementObserverTerminalFailure)
                {
                    return DiscoveryAudit.Create(
                        AuditResult.Fail(
                            $"reason=movement-observer-terminal-fail, file={file.Path}"),
                        hostFiles,
                        clientFiles);
                }

                bool fileHasHost = false;
                bool fileHasClient = false;
                foreach (RunEvidence run in runs)
                {
                    if (!TryGetConsistentRole(run, out string role))
                    {
                        return DiscoveryAudit.Create(
                            AuditResult.Inconclusive(
                                $"reason=ambiguous-file-role, file={file.Path}, runId={run.RunId}"),
                            hostFiles,
                            clientFiles);
                    }
                    if (!IsAvailableKey(run.SharedSessionKey))
                    {
                        return DiscoveryAudit.Create(
                            AuditResult.Inconclusive(
                                $"reason=missing-shared-session-key, file={file.Path}, runId={run.RunId}"),
                            hostFiles,
                            clientFiles);
                    }

                    string identity = role + "\n" + run.SharedSessionKey;
                    if (!fileSchemas.TryGetValue(identity, out string observerSchema))
                    {
                        return DiscoveryAudit.Create(
                            AuditResult.Inconclusive(
                                $"reason=missing-or-ambiguous-movement-observer-schema, " +
                                $"file={file.Path}, role={role}, " +
                                $"sharedSessionKey={run.SharedSessionKey}"),
                            hostFiles,
                            clientFiles);
                    }
                    if (!fileMovementStartedAt.TryGetValue(
                            identity,
                            out double movementStartedAt))
                    {
                        return DiscoveryAudit.Create(
                            AuditResult.Inconclusive(
                                $"reason=missing-or-ambiguous-movement-observer-startedAt, " +
                                $"file={file.Path}, role={role}, " +
                                $"sharedSessionKey={run.SharedSessionKey}"),
                            hostFiles,
                            clientFiles);
                    }
                    run.MovementObserverStartedAt = movementStartedAt;
                    if (roleSessionSources.TryGetValue(identity, out string source)
                        && !string.Equals(source, file.Path, StringComparison.Ordinal))
                    {
                        return DiscoveryAudit.Create(
                            AuditResult.Inconclusive(
                                $"reason=duplicate-role-session-files, role={role}, " +
                                $"sharedSessionKey={run.SharedSessionKey}, first={source}, second={file.Path}"),
                            hostFiles,
                            clientFiles);
                    }
                    roleSessionSources[identity] = file.Path;
                    schemasByRoleSession[identity] = observerSchema;
                    if (string.Equals(role, "host", StringComparison.OrdinalIgnoreCase))
                    {
                        hostRuns.Add(run);
                        fileHasHost = true;
                    }
                    else
                    {
                        clientRuns.Add(run);
                        fileHasClient = true;
                    }
                }
                if (fileHasHost) hostFiles++;
                if (fileHasClient) clientFiles++;
            }

            if (hostRuns.Count == 0 || clientRuns.Count == 0)
            {
                return DiscoveryAudit.Create(
                    AuditResult.Inconclusive(
                        $"reason=missing-role-evidence, hostFiles={hostFiles}, clientFiles={clientFiles}"),
                    hostFiles,
                    clientFiles);
            }

            IEnumerable<string> sharedKeys = schemasByRoleSession.Keys
                .Where(identity => identity.StartsWith("host\n", StringComparison.Ordinal))
                .Select(identity => identity.Substring("host\n".Length))
                .Where(key => schemasByRoleSession.ContainsKey("client\n" + key));
            foreach (string sharedKey in sharedKeys)
            {
                string hostSchema = schemasByRoleSession["host\n" + sharedKey];
                string clientSchema = schemasByRoleSession["client\n" + sharedKey];
                if (!string.Equals(hostSchema, clientSchema, StringComparison.Ordinal))
                {
                    return DiscoveryAudit.Create(
                        AuditResult.Inconclusive(
                            $"reason=movement-observer-schema-mismatch, " +
                            $"sharedSessionKey={sharedKey}, hostSchema={hostSchema}, " +
                            $"clientSchema={clientSchema}"),
                        hostFiles,
                        clientFiles);
                }
                if (!string.Equals(
                        hostSchema,
                        UnitMovementAuthorityObserver.ProductionSchema,
                        StringComparison.Ordinal))
                {
                    return DiscoveryAudit.Create(
                        AuditResult.Inconclusive(
                            $"reason=non-production-movement-observer-schema, " +
                            $"sharedSessionKey={sharedKey}, schema={hostSchema}, " +
                            $"requiredSchema={UnitMovementAuthorityObserver.ProductionSchema}"),
                        hostFiles,
                        clientFiles);
                }
            }

            return DiscoveryAudit.Create(
                AnalyzeRuns(hostRuns, clientRuns, requireDeviceEditorPair: true),
                hostFiles,
                clientFiles);
        }

        private static bool TryGetConsistentRole(RunEvidence run, out string role)
        {
            role = null;
            if (!string.Equals(run.BeginRole, run.EndRole, StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(run.BeginRole, "host", StringComparison.OrdinalIgnoreCase))
                role = "host";
            else if (string.Equals(run.BeginRole, "client", StringComparison.OrdinalIgnoreCase))
                role = "client";
            return role != null;
        }

        private static string ParseMovementObserverSchemas(
            string text,
            ISet<string> includedSharedSessionKeys,
            out Dictionary<string, string> schemas,
            out Dictionary<string, double> startedAtByRoleSession,
            out bool terminalFailure)
        {
            schemas = new Dictionary<string, string>(StringComparer.Ordinal);
            startedAtByRoleSession =
                new Dictionary<string, double>(StringComparer.Ordinal);
            terminalFailure = false;
            var lifecycles = new Dictionary<string, MovementObserverLifecycle>(
                StringComparer.Ordinal);
            string[] lines = (text ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                bool isBegin = line.IndexOf(
                    "[UAS-MOVE-AUTH] BEGIN",
                    StringComparison.Ordinal) >= 0;
                bool isEnd = line.IndexOf(
                    "[UAS-MOVE-AUTH] END",
                    StringComparison.Ordinal) >= 0;
                if (!isBegin && !isEnd)
                    continue;

                string role = ReadValue(line, "role");
                string sharedSessionKey = ReadValue(line, "sharedSessionKey");
                if (includedSharedSessionKeys != null
                    && !includedSharedSessionKeys.Contains(sharedSessionKey))
                    continue;
                if ((!string.Equals(role, "host", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(role, "client", StringComparison.OrdinalIgnoreCase))
                    || !IsAvailableKey(sharedSessionKey))
                    return "malformed-movement-observer-schema";

                role = role.ToLowerInvariant();
                string identity = role + "\n" + sharedSessionKey;
                if (isBegin)
                {
                    string runId = ReadValue(line, "runId");
                    string startedAt = ReadValue(line, "startedAt");
                    string schema = ReadValue(line, "observerSchema");
                    if (string.IsNullOrEmpty(runId)
                        || string.IsNullOrEmpty(startedAt)
                        || !double.TryParse(
                            startedAt,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double parsedStartedAt)
                        || !IsFinite(parsedStartedAt)
                        || string.IsNullOrEmpty(schema))
                        return "malformed-movement-observer-schema";
                    if (lifecycles.ContainsKey(identity))
                        return "duplicate-movement-observer-schema";
                    lifecycles.Add(identity, new MovementObserverLifecycle
                    {
                        RunId = runId,
                        SharedSessionKey = sharedSessionKey,
                        StartedAt = startedAt,
                        ParsedStartedAt = parsedStartedAt,
                        Role = role,
                        Schema = schema
                    });
                }
                else
                {
                    if (!lifecycles.TryGetValue(
                            identity,
                            out MovementObserverLifecycle lifecycle))
                        return "missing-or-ambiguous-movement-observer-schema";
                    if (lifecycle.EndSeen)
                        return "mismatched-movement-observer-lifecycle";

                    string endRunId = ReadValue(line, "runId");
                    string endStartedAt = ReadValue(line, "startedAt");
                    string verdict = ReadValue(line, "verdict");
                    if (!string.Equals(
                            lifecycle.RunId,
                            endRunId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            lifecycle.SharedSessionKey,
                            sharedSessionKey,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            lifecycle.StartedAt,
                            endStartedAt,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            lifecycle.Role,
                            role,
                            StringComparison.Ordinal))
                        return "mismatched-movement-observer-lifecycle";
                    if (string.Equals(verdict, "FAIL", StringComparison.Ordinal))
                        terminalFailure = true;
                    else if (!string.Equals(
                                 verdict,
                                 "EVIDENCE",
                                 StringComparison.Ordinal))
                        return "malformed-movement-observer-terminal";
                    lifecycle.EndSeen = true;
                }
            }
            foreach (MovementObserverLifecycle lifecycle in lifecycles.Values)
            {
                if (!lifecycle.EndSeen)
                    return "missing-or-ambiguous-movement-observer-schema";
                string identity = lifecycle.Role + "\n" + lifecycle.SharedSessionKey;
                schemas.Add(identity, lifecycle.Schema);
                startedAtByRoleSession.Add(identity, lifecycle.ParsedStartedAt);
            }
            return null;
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
            IReadOnlyList<double> extraEndpointTimes = null,
            long sessionStartBucket = 1,
            double movementStartedAt = 1d)
        {
            string flip = isFlipped.ToString();
            string offset = xOffset.ToString("F6", CultureInfo.InvariantCulture);
            string time = endpointTime.ToString("F3", CultureInfo.InvariantCulture);
            string moveStart = movementStartedAt.ToString(
                "F6",
                CultureInfo.InvariantCulture);
            var lines = new List<string>
            {
                $"[UAS-MOVE-AUTH] BEGIN | runId=move-{runId}, " +
                $"sharedSessionKey={sharedSessionKey}, startedAt={moveStart}, " +
                $"role={role}, observerSchema={UnitMovementAuthorityObserver.ProductionSchema}, " +
                "mode=ReducerAuthoritative, serverTime=1.000000",
                $"[UAS-ROOT-POSE] BEGIN | runId={runId}, " +
                $"sharedSessionKey={sharedSessionKey}, sessionStartBucket={sessionStartBucket}, " +
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
                    offset,
                    sessionStartBucket));
                lines.Add(BuildFixtureRotationEvidence(
                    runId, sharedSessionKey, role, unitId,
                    (ulong)(100 + unitId), time, sessionStartBucket));
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
                        offset,
                        sessionStartBucket));
                    lines.Add(BuildFixtureRotationEvidence(
                        runId, sharedSessionKey, role, unitId,
                        (ulong)(100 + unitId), extraTime, sessionStartBucket));
                    endpointCount++;
                }
            }
            lines.Add(
                $"[UAS-MOVE-AUTH] END | runId=move-{runId}, " +
                $"sharedSessionKey={sharedSessionKey}, startedAt={moveStart}, " +
                $"reason=fixture, role={role}, verdict=EVIDENCE");
            lines.Add(
                $"[UAS-ROOT-POSE] summary-END | runId={runId}, " +
                $"sharedSessionKey={sharedSessionKey}, sessionStartBucket={sessionStartBucket}, " +
                $"role={role}, isFlipped={flip}, " +
                $"verdict={localVerdict}, coveragePassed=True, errors={errors}, " +
                $"stableAfterMoveEndpointUnits={endpointCount}, " +
                $"stableEndpointEventsObserved={endpointCount}, " +
                $"stableEndpointLogsWritten={endpointCount}, " +
                $"logDropCount={logDropCount}, stableEvidenceDropCount=0, " +
                $"rotationEvidenceSchema=" +
                $"{RotationEvidenceSchema}, " +
                $"rotationEvidenceEventsObserved={endpointCount}, " +
                $"rotationEvidenceLogsWritten={endpointCount}, " +
                "rotationEvidenceDropCount=0, " +
                "rotationEvidencePreflightFailures=0, " +
                "rotationEvidenceComplete=True, " +
                $"evidenceComplete={logDropCount == 0}");
            return string.Join("\n", lines);
        }

        private static string BuildFixtureEndpoint(
            string runId,
            string sharedSessionKey,
            int unitId,
            ulong networkObjectId,
            string time,
            string xOffset,
            long sessionStartBucket)
        {
            double qualifiedTime = double.Parse(time, CultureInfo.InvariantCulture);
            string startTime =
                (qualifiedTime - 3d).ToString("F3", CultureInfo.InvariantCulture);
            return
                $"[UAS-ROOT-POSE] stable-coverage | runId={runId}, " +
                $"sharedSessionKey={sharedSessionKey}, sessionStartBucket={sessionStartBucket}, " +
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

        private static string BuildFixtureRotationEvidence(
            string runId,
            string sharedSessionKey,
            string role,
            int unitId,
            ulong networkObjectId,
            string time,
            long sessionStartBucket)
        {
            double qualifiedTime = double.Parse(time, CultureInfo.InvariantCulture);
            string startTime =
                (qualifiedTime - 3d).ToString("F3", CultureInfo.InvariantCulture);
            string observation = string.Equals(role, "host", StringComparison.Ordinal)
                ? "server-checkpoint"
                : "client-final-root";
            return
                $"[UAS-ROOT-POSE] rotation-replication-evidence | runId={runId}, " +
                $"sharedSessionKey={sharedSessionKey}, sessionStartBucket={sessionStartBucket}, " +
                $"rotationEvidenceSchema=" +
                $"{RotationEvidenceSchema}, " +
                $"observation={observation}, unitId={unitId}, " +
                $"networkObjectId={networkObjectId}, serverTime={time}, " +
                "networkTick=300, movementPhase=NoIntent, commandRevision=1, " +
                "segmentRevision=1, semanticRevision=1, " +
                "rootRotation=(0.000000;0.000000;0.000000;1.000000), " +
                $"stableWindowStartServerTime={startTime}, " +
                $"stableQualifiedServerTime={time}";
        }

        private static string ReplaceValueOnFirstLineContaining(
            string text,
            string lineMarker,
            string oldFragment,
            string newFragment)
        {
            string[] lines = (text ?? string.Empty)
                .Split(new[] { "\n" }, StringSplitOptions.None);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (line.IndexOf(lineMarker, StringComparison.Ordinal) < 0)
                    continue;

                int fragmentIndex = line.IndexOf(oldFragment, StringComparison.Ordinal);
                if (fragmentIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Fixture line '{lineMarker}' does not contain '{oldFragment}'.");
                }

                // 문자열 전체 Replace를 사용하지 않고 선택한 한 줄의 첫 필드만 바꾼다.
                // ROOT와 MOVE 양쪽 또는 BEGIN/END 양쪽이 동시에 변형되는 회귀 오염을 막는다.
                lines[lineIndex] = line.Remove(fragmentIndex, oldFragment.Length)
                    .Insert(fragmentIndex, newFragment);
                return string.Join("\n", lines);
            }

            throw new InvalidOperationException(
                $"Fixture line marker '{lineMarker}' was not found.");
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
                    run.RotationEvidenceSchema =
                        ReadValue(line, "rotationEvidenceSchema");
                    run.RotationEvidenceEventsObserved =
                        ReadInt(line, "rotationEvidenceEventsObserved");
                    run.RotationEvidenceLogsWritten =
                        ReadInt(line, "rotationEvidenceLogsWritten");
                    run.RotationEvidenceDropCount =
                        ReadInt(line, "rotationEvidenceDropCount");
                    run.RotationEvidencePreflightFailures =
                        ReadInt(line, "rotationEvidencePreflightFailures");
                    run.RotationEvidenceComplete =
                        ReadBool(line, "rotationEvidenceComplete");
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
                else if (line.IndexOf(
                             "[UAS-ROOT-POSE] rotation-replication-evidence",
                             StringComparison.Ordinal) >= 0)
                {
                    int markerIndex = line.IndexOf(
                        "[UAS-ROOT-POSE]",
                        StringComparison.Ordinal);
                    string signature = line.Substring(markerIndex);
                    if (!run.RotationEvidenceSignatures.Add(signature))
                        continue;
                    RotationReplicationEvidence evidence;
                    try
                    {
                        evidence = ParseRotationReplicationEvidence(line);
                    }
                    catch (FormatException)
                    {
                        run.ParseErrorCount++;
                        continue;
                    }
                    if (!run.RotationEvidence.TryGetValue(
                            evidence.Identity,
                            out List<RotationReplicationEvidence> evidenceList))
                    {
                        evidenceList = new List<RotationReplicationEvidence>();
                        run.RotationEvidence.Add(evidence.Identity, evidenceList);
                    }
                    evidenceList.Add(evidence);
                }
            }

            foreach (RunEvidence run in runs.Values)
            {
                foreach (List<Endpoint> endpoints in run.Endpoints.Values)
                    endpoints.Sort((left, right) => left.ServerTime.CompareTo(right.ServerTime));
                foreach (List<RotationReplicationEvidence> evidence in
                         run.RotationEvidence.Values)
                {
                    evidence.Sort((left, right) =>
                        left.ServerTime.CompareTo(right.ServerTime));
                }
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
                || !client.SessionStartBucket.HasValue)
                return Result(AuditVerdict.Inconclusive, "missing-session-start-bucket");
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
            if (host.SourceIsDevice != client.SourceIsDevice)
            {
                if (!host.MovementObserverStartedAt.HasValue
                    || !client.MovementObserverStartedAt.HasValue
                    || !IsFinite(host.MovementObserverStartedAt.Value)
                    || !IsFinite(client.MovementObserverStartedAt.Value))
                {
                    return Result(
                        AuditVerdict.Inconclusive,
                        "missing-or-invalid-movement-observer-startedAt");
                }

                double movementStartedAtDelta = Math.Abs(
                    host.MovementObserverStartedAt.Value
                    - client.MovementObserverStartedAt.Value);
                if (movementStartedAtDelta
                    > MaximumMovementStartedAtDeltaSeconds)
                {
                    return Result(
                        AuditVerdict.Inconclusive,
                        "movement-observer-startedAt-mismatch");
                }
            }
            if (!HasAnyOverlappingStableWindow(host, client))
                return Result(AuditVerdict.Inconclusive, "no-overlapping-stable-windows");
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
            int parsedRotationEvidenceCount =
                run.RotationEvidence.Values.Sum(values => values.Count);
            return run.StableEndpointEventsObserved.HasValue
                   && run.StableEndpointLogsWritten.HasValue
                   && run.StableEvidenceDropCount.HasValue
                   && run.StableAfterMoveEndpointUnits.HasValue
                   && run.StableEndpointEventsObserved.Value == parsedEndpointCount
                   && run.StableEndpointLogsWritten.Value == parsedEndpointCount
                   && run.StableEvidenceDropCount.Value == 0
                   && run.StableAfterMoveEndpointUnits.Value == parsedDistinctUnits
                   && run.Endpoints.Values.All(values => values.Count == 1)
                   && string.Equals(
                       run.RotationEvidenceSchema,
                       RotationEvidenceSchema,
                       StringComparison.Ordinal)
                   && run.RotationEvidenceEventsObserved == parsedEndpointCount
                   && run.RotationEvidenceLogsWritten == parsedEndpointCount
                   && run.RotationEvidenceDropCount == 0
                   && run.RotationEvidencePreflightFailures == 0
                   && run.RotationEvidenceComplete == true
                   && parsedRotationEvidenceCount == parsedEndpointCount
                   && run.RotationEvidence.Count == parsedDistinctUnits
                   && run.RotationEvidence.Values.All(values => values.Count == 1)
                   && RotationEvidenceConnectsToEndpoints(run);
        }

        private static bool RotationEvidenceConnectsToEndpoints(RunEvidence run)
        {
            string expectedObservation = string.Equals(
                run.BeginRole,
                "host",
                StringComparison.OrdinalIgnoreCase)
                    ? "server-checkpoint"
                    : "client-final-root";
            foreach (KeyValuePair<UnitIdentity, List<Endpoint>> entry in run.Endpoints)
            {
                if (!run.RotationEvidence.TryGetValue(
                        entry.Key,
                        out List<RotationReplicationEvidence> evidence)
                    || entry.Value.Count != 1
                    || evidence.Count != 1)
                    return false;
                Endpoint endpoint = entry.Value[0];
                RotationReplicationEvidence rotation = evidence[0];
                if (!string.Equals(
                        rotation.Observation,
                        expectedObservation,
                        StringComparison.Ordinal)
                    || Math.Abs(rotation.ServerTime - endpoint.ServerTime) > 0.002d
                    || Math.Abs(
                        rotation.StableWindowStartServerTime
                        - endpoint.StableWindowStartServerTime) > 0.002d
                    || Quaternion.Angle(rotation.RootRotation, endpoint.Rotation)
                        > FloatComparisonEpsilon)
                    return false;
            }
            return true;
        }

        private static RotationReplicationEvidence ParseRotationReplicationEvidence(
            string line)
        {
            int? unitId = ReadInt(line, "unitId");
            ulong? networkObjectId = ReadULong(line, "networkObjectId");
            double? serverTime = ReadDouble(line, "serverTime");
            int? networkTick = ReadInt(line, "networkTick");
            string schema = ReadValue(line, "rotationEvidenceSchema");
            string observation = ReadValue(line, "observation");
            string phaseText = ReadValue(line, "movementPhase");
            ulong? commandRevision = ReadULong(line, "commandRevision");
            ulong? segmentRevision = ReadULong(line, "segmentRevision");
            ulong? semanticRevision = ReadULong(line, "semanticRevision");
            Quaternion rootRotation = ReadQuaternion(line, "rootRotation");
            double? stableWindowStart =
                ReadDouble(line, "stableWindowStartServerTime");
            double? stableQualified =
                ReadDouble(line, "stableQualifiedServerTime");
            bool phaseParsed = Enum.TryParse(
                phaseText,
                ignoreCase: false,
                out UnitMovementPhase phase);
            if (!unitId.HasValue
                || unitId.Value < 0
                || !networkObjectId.HasValue
                || !serverTime.HasValue
                || !IsFinite(serverTime.Value)
                || !networkTick.HasValue
                || networkTick.Value < 0
                || !string.Equals(
                    schema,
                    RotationEvidenceSchema,
                    StringComparison.Ordinal)
                || (observation != "server-checkpoint"
                    && observation != "client-final-root")
                || !phaseParsed
                || phase == UnitMovementPhase.Invalid
                || !commandRevision.HasValue
                || !segmentRevision.HasValue
                || !semanticRevision.HasValue
                || commandRevision.Value == 0UL
                || segmentRevision.Value == 0UL
                || semanticRevision.Value == 0UL
                || !IsFinite(rootRotation)
                || QuaternionSquaredMagnitude(rootRotation) < 0.5f
                || QuaternionSquaredMagnitude(rootRotation) > 1.5f
                || !stableWindowStart.HasValue
                || !IsFinite(stableWindowStart.Value)
                || !stableQualified.HasValue
                || !IsFinite(stableQualified.Value)
                || stableQualified.Value < stableWindowStart.Value
                || Math.Abs(serverTime.Value - stableQualified.Value) > 0.002d)
            {
                throw new FormatException(
                    "rotation evidence contains invalid identity, lifecycle or pose data");
            }

            return new RotationReplicationEvidence
            {
                Identity = new UnitIdentity(unitId.Value, networkObjectId.Value),
                Observation = observation,
                ServerTime = serverTime.Value,
                NetworkTick = networkTick.Value,
                Phase = phase,
                CommandRevision = commandRevision.Value,
                SegmentRevision = segmentRevision.Value,
                SemanticRevision = semanticRevision.Value,
                RootRotation = Quaternion.Normalize(rootRotation),
                StableWindowStartServerTime = stableWindowStart.Value
            };
        }

        private static bool HasAnyOverlappingStableWindow(
            RunEvidence host,
            RunEvidence client)
        {
            foreach (KeyValuePair<UnitIdentity, List<Endpoint>> entry in host.Endpoints)
            {
                if (!client.Endpoints.TryGetValue(
                        entry.Key,
                        out List<Endpoint> clientEndpoints)
                    || entry.Value.Count != 1
                    || clientEndpoints.Count != 1)
                    continue;
                if (StableWindowsOverlap(entry.Value[0], clientEndpoints[0]))
                    return true;
            }
            return false;
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

        private sealed class LogFileInput
        {
            public readonly string Path;
            public readonly string Text;

            public readonly long ModifiedUtcTicks;

            public LogFileInput(string path, string text, long modifiedUtcTicks = 0)
            {
                Path = path;
                Text = text ?? string.Empty;
                ModifiedUtcTicks = modifiedUtcTicks;
            }
        }

        private sealed class DiscoveryAudit
        {
            public AuditResult Result { get; private set; }
            public int HostFiles { get; private set; }
            public int ClientFiles { get; private set; }

            public static DiscoveryAudit Create(
                AuditResult result,
                int hostFiles,
                int clientFiles)
            {
                return new DiscoveryAudit
                {
                    Result = result,
                    HostFiles = hostFiles,
                    ClientFiles = clientFiles
                };
            }
        }

        private sealed class MovementObserverLifecycle
        {
            public string RunId;
            public string SharedSessionKey;
            public string StartedAt;
            public double ParsedStartedAt;
            public string Role;
            public string Schema;
            public bool EndSeen;
        }

        private sealed class RunEvidence
        {
            public string RunId;
            public string SharedSessionKey;
            public bool SourceIsDevice;
            public long? SessionStartBucket;
            public double? MovementObserverStartedAt;
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
            public string RotationEvidenceSchema;
            public int? RotationEvidenceEventsObserved;
            public int? RotationEvidenceLogsWritten;
            public int? RotationEvidenceDropCount;
            public int? RotationEvidencePreflightFailures;
            public bool? RotationEvidenceComplete;
            public readonly Dictionary<UnitIdentity, List<Endpoint>> Endpoints =
                new Dictionary<UnitIdentity, List<Endpoint>>();
            public readonly Dictionary<UnitIdentity, List<RotationReplicationEvidence>>
                RotationEvidence =
                    new Dictionary<UnitIdentity, List<RotationReplicationEvidence>>();
            public readonly HashSet<string> BeginSignatures =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> EndSignatures =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> EndpointSignatures =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> RotationEvidenceSignatures =
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

        private sealed class RotationReplicationEvidence
        {
            public UnitIdentity Identity;
            public string Observation;
            public double ServerTime;
            public int NetworkTick;
            public UnitMovementPhase Phase;
            public ulong CommandRevision;
            public ulong SegmentRevision;
            public ulong SemanticRevision;
            public Quaternion RootRotation;
            public double StableWindowStartServerTime;

            public bool HasSameMovementRevision(RotationReplicationEvidence other)
            {
                return other != null
                    && Phase == other.Phase
                    && CommandRevision == other.CommandRevision
                    && SegmentRevision == other.SegmentRevision
                    && SemanticRevision == other.SemanticRevision;
            }
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
