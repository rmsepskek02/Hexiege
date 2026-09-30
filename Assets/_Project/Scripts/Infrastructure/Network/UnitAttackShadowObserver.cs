#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;
using Unity.Netcode;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    public enum PresentationDirectionRevisionClassification : byte
    {
        SameRevisionMatch = 0,
        SameRevisionVisualMismatch = 1,
        RevisionLag = 2,
        ReplicatedStateAhead = 3,
        ScopeMismatch = 4,
        MissingOrInvalidEvidence = 5
    }

    public enum PresentationTimingEvidenceKind : byte
    {
        NotTimingEvidence = 0,
        NormalImpact = 1,
        TimeoutRecovery = 2,
        TargetDeathRecovery = 3,
        AttackerStopRecovery = 4
    }

    public enum AnimatedAttackFacingStage : byte
    {
        AttackStartPose = 1,
        ImpactMarker = 2,
        AttackEndPose = 3
    }

    public enum InfernoMotionJumpClassification : byte
    {
        None = 0,
        AuthoritativeRootJump = 1,
        VisualProjectionJump = 2,
        PresentationAnchorJump = 3,
        LongFrameCatchUp = 4,
        ClientOnlyReplicationGap = 5,
        UnavailableAmbiguous = 6
    }

    public enum StreamSpiritTimelineAdmission : byte
    {
        Accepted = 0,
        Duplicate = 1,
        Overflow = 2,
        Invalid = 3
    }

    public enum FoxMagicianTimelineAdmission : byte
    {
        Accepted = 0,
        Duplicate = 1,
        Overflow = 2,
        Invalid = 3
    }

    internal struct InfernoMotionJumpSample
    {
        internal int UnitId;
        internal ulong NetworkObjectId;
        internal bool NetworkIdentityAvailable;
        internal ulong Lifecycle;
        internal bool ViewFlipped;
        internal int PreviousFrame;
        internal int Frame;
        internal double PreviousTime;
        internal double Time;
        internal float DeltaTime;
        internal float RawFrameSeconds;
        internal Vector3 SimulationPrevious;
        internal Vector3 SimulationCurrent;
        internal Quaternion SimulationRotationPrevious;
        internal Quaternion SimulationRotationCurrent;
        internal bool VisualAvailable;
        internal Vector3 VisualPrevious;
        internal Vector3 VisualCurrent;
        internal Quaternion VisualRotationPrevious;
        internal Quaternion VisualRotationCurrent;
        internal bool RenderedAnchorAvailable;
        internal Vector3 RenderedAnchorPrevious;
        internal Vector3 RenderedAnchorCurrent;
        internal string RenderedAnchorUnavailableReason;
        internal bool RendererBoundsAvailable;
        internal Vector3 RendererBoundsPrevious;
        internal Vector3 RendererBoundsCurrent;
        internal string RendererBoundsUnavailableReason;
        internal bool SpeedAvailable;
        internal float ProductionWorldSpeed;
        internal UnitMovementPhase MovementPhase;
        internal bool MovementEvidenceAvailable;
        internal ulong CommandRevision;
        internal ulong SegmentRevision;
        internal ulong SemanticRevision;
        internal bool ReplicationEvidenceAvailable;
        internal int AnimatorCurrentStateHash;
        internal int AnimatorNextStateHash;
        internal bool AnimatorInTransition;
        internal bool WalkAttackTransition;
    }

    /// <summary>
    /// Tracer C의 읽기 전용 bounded observer다. Legacy/Shadow 차이를 기록할 뿐 Root, HP,
    /// cooldown, Animator, NetworkTransform, RPC 또는 VFX를 쓰지 않는다.
    /// 모든 일반 로그는 상한 뒤 드롭하고 종료용 줄은 별도 예약하여 Android Logcat에서도
    /// BEGIN/manifest/END 증거가 상세 로그에 밀리지 않게 한다.
    /// </summary>
    public static class UnitAttackShadowObserver
    {
        public const string ProductionSchema = "c3-authoritative-result-presentation-shadow-v12";
        public const double PresentationAimToleranceDegrees = 8.0d;
        private const int MaximumLines = 512;
        private const int ReservedTerminalLines = 32;
        private const int MaximumNormalLines = MaximumLines - ReservedTerminalLines;
        private const int MaximumFullLineUtf8BytesExclusive = 1000;
        private const int MaximumAnimatedFacingEvidenceKeys = 96;
        private const int MaximumInfernoAttackVfxEvents = 128;
        private const int MaximumStreamSpiritTimelineFlows = 64;
        private const double StreamSpiritExpectedImpactSeconds = 0.50d;
        private const int MaximumFoxMagicianTimelineFlows = 256;
        private const int MaximumFoxMagicianTimelineDetailLines = 64;
        private const double MaximumFoxMagicianTimelineIncompleteAgeSeconds = 8d;
        private const double FoxMagicianExpectedImpactSeconds = 2.25d;
        private const int MaximumInfernoMotionJumpUnits = 64;
        private const int MaximumInfernoMotionJumpEventsPerUnit = 8;
        private const int MaximumInfernoMotionJumpKeys = 128;
        private const float InfernoMotionJumpPositionTolerance = 0.05f;
        private const float InfernoMotionJumpLongFrameSeconds = 0.10f;
        private const float InfernoMotionJumpNominalFrameSeconds = 1f / 30f;
        // 원호 예측과 실제 root-relative Hips 이동의 차이가 이 값 이내면 회전축 오프셋으로
        // 설명 가능한 표본이다. 값은 원인 확정용 허용치가 아니라 다음 실기 비교의 분류 경계다.
        private const float InfernoMotionArcResidualTolerance = 0.05f;
        private static bool _infernoMotionPreflightFailureLogged;
        private static bool _infernoAttackVfxOverflowLogged;
        private static bool _streamSpiritTimelineOverflowLogged;
        private static bool _foxMagicianTimelineOverflowLogged;

        /// <summary>
        /// StreamSpirit의 한 production 공격 회차에서 관측된 상태만 보관한다.
        /// 이 레코드는 공격을 진행시키거나 취소하지 않으며, 이미 결정된 사건의 서버 시각을
        /// 서로 대조하기 위해서만 사용한다. 키는 Host와 Client가 함께 받는 presentation revision이다.
        /// </summary>
        private sealed class StreamSpiritTimelineFlow
        {
            internal int UnitId;
            internal ulong Revision;
            internal int TargetId;
            internal bool TargetIsUnit;
            internal double StartServerTime;
            internal double MarkerServerTime = double.NaN;
            internal double VfxServerTime = double.NaN;
            internal double TimerImpactServerTime = double.NaN;
            internal bool MarkerObserved;
            internal bool VfxAttempted;
            internal bool VfxStarted;
            internal bool TimerImpactObserved;
        }

        /// <summary>
        /// FoxMagician의 한 LegacyFallback production 공격 회차를 표현 revision과 함께 보관한다.
        /// 모든 필드는 진단 전용이며 피해, 쿨다운, 타겟, Animator 또는 VFX 결과를 변경하지 않는다.
        /// </summary>
        private sealed class FoxMagicianTimelineFlow
        {
            internal ulong CorrelationTicket;
            internal int UnitId;
            internal ulong Revision;
            internal int TargetId;
            internal bool TargetIsUnit;
            internal double StartServerTime;
            internal double MarkerServerTime = double.NaN;
            internal double VfxAttemptServerTime = double.NaN;
            internal double VfxResultServerTime = double.NaN;
            internal double TimerImpactServerTime = double.NaN;
            internal bool MarkerObserved;
            internal bool VfxAttempted;
            internal bool VfxResultObserved;
            internal bool VfxStarted;
            internal bool TimerImpactObserved;
        }

        private static readonly Dictionary<int, UnitAttackShadowReplicationClassifier> ClientClassifiers
            = new Dictionary<int, UnitAttackShadowReplicationClassifier>();
        private static readonly Dictionary<int, UnitAttackShadowImpactReplicationClassifier> ClientImpactClassifiers
            = new Dictionary<int, UnitAttackShadowImpactReplicationClassifier>();
        private static readonly HashSet<int> CoveredUnitTypes = new HashSet<int>();
        private static readonly HashSet<string> AnimatedFacingEvidenceKeys = new HashSet<string>();
        private static readonly Dictionary<string, StreamSpiritTimelineFlow> StreamSpiritTimelineFlows
            = new Dictionary<string, StreamSpiritTimelineFlow>();
        private static readonly Dictionary<string, FoxMagicianTimelineFlow> FoxMagicianTimelineFlows
            = new Dictionary<string, FoxMagicianTimelineFlow>();
        private static readonly Dictionary<ulong, string> FoxMagicianTimelineTicketKeys
            = new Dictionary<ulong, string>();
        private static readonly Dictionary<string, ulong> FoxMagicianTimelineKeyTickets
            = new Dictionary<string, ulong>();
        private static readonly InfernoMotionJumpLimiter InfernoMotionJumpEvidence =
            new InfernoMotionJumpLimiter(
                MaximumInfernoMotionJumpUnits,
                MaximumInfernoMotionJumpEventsPerUnit,
                MaximumInfernoMotionJumpKeys);

        private static NetworkManager _networkManager;
        private static bool _active;
        private static bool _isServer;
        private static string _runId;
        private static string _sessionKey;
        private static double _startedAt;
        private static int _lines;
        private static int _terminalLines;
        private static int _dropped;
        private static int _suppressedNormal;
        private static int _infernoAttackVfxRawEvents;
        private static int _infernoAttackVfxStarted;
        private static int _infernoAttackVfxSourceOnlyPreserved;
        private static int _infernoAttackVfxGateSuppressed;
        private static int _infernoAttackVfxPlaybackFailures;
        private static int _streamSpiritTimelineStarts;
        private static int _streamSpiritTimelineMarkers;
        private static int _streamSpiritTimelineVfxAttempts;
        private static int _streamSpiritTimelineVfxStarted;
        private static int _streamSpiritTimelineVfxFailures;
        private static int _streamSpiritTimelineTimerImpacts;
        private static int _streamSpiritTimelineUnmatchedMarkers;
        private static int _streamSpiritTimelineUnmatchedVfx;
        private static int _streamSpiritTimelineUnmatchedTimerImpacts;
        private static int _streamSpiritTimelineDuplicateTransitions;
        private static int _streamSpiritTimelineRetiredComplete;
        private static double _streamSpiritTimelineRetiredMaxMarkerFromStart;
        private static double _streamSpiritTimelineRetiredMaxTimerFromStart;
        private static double _streamSpiritTimelineRetiredMaxMarkerTimerDelta;
        private static int _foxMagicianTimelineStarts;
        private static int _foxMagicianTimelineMarkers;
        private static int _foxMagicianTimelineVfxAttempts;
        private static int _foxMagicianTimelineVfxStarted;
        private static int _foxMagicianTimelineVfxFailures;
        private static int _foxMagicianTimelineParticlePositiveResults;
        private static int _foxMagicianTimelinePlaybackActiveResults;
        private static int _foxMagicianTimelineMaxParticleSystemCount;
        private static int _foxMagicianTimelineTimerImpacts;
        private static int _foxMagicianTimelineUnmatchedMarkers;
        private static int _foxMagicianTimelineUnmatchedVfxAttempts;
        private static int _foxMagicianTimelineUnmatchedVfxResults;
        private static int _foxMagicianTimelineUnmatchedTimerImpacts;
        private static int _foxMagicianTimelineDuplicateTransitions;
        private static int _foxMagicianTimelineOverflow;
        private static int _foxMagicianTimelineDropped;
        private static int _foxMagicianTimelineDetailLines;
        private static int _foxMagicianTimelineDetailSuppressed;
        private static int _foxMagicianTimelineExpiredIncomplete;
        private static int _foxMagicianTimelineRetiredComplete;
        private static ulong _foxMagicianTimelineNextCorrelationTicket;
        private static double _foxMagicianTimelineRetiredMaxMarkerFromStart;
        private static double _foxMagicianTimelineRetiredMaxTimerFromStart;
        private static double _foxMagicianTimelineRetiredMaxMarkerTimerDelta;
        private static int _intentSamples;
        private static int _commits;
        private static int _legacySchedules;
        private static int _legacyBeforeAligned;
        private static int _expectedDeferred;
        private static int _correlationFailures;
        private static int _productionGateSamples;
        private static int _productionGateReady;
        private static int _productionGateDeferred;
        private static int _productionGateInvalid;
        private static int _impactSamples;
        private static int _targetMismatches;
        private static int _clientAccepted;
        private static int _clientRejected;
        private static int _serverResults;
        private static int _resultCompletionFailures;
        private static int _clientResultAccepted;
        private static int _clientResultRejected;
        private static int _terminalPreflightFailures;
        private static int _manifestFailures;
        private static int _presentationScheduled;
        private static int _presentationMatched;
        private static int _presentationUnmatched;
        private static int _presentationAmbiguous;
        private static int _presentationDuplicates;
        private static int _presentationConflicts;
        private static int _presentationCatchUps;
        private static int _presentationExpired;
        private static int _presentationInvalid;
        private static int _presentationRetired;
        private static int _presentationDirectionMismatches;
        private static int _presentationDirectionSameRevisionMismatches;
        private static int _presentationDirectionRevisionLags;
        private static int _presentationDirectionScopeMismatches;
        private static int _presentationDirectionEvidenceInvalid;
        private static int _presentationTimingMismatches;
        private static int _presentationRecoveryEmits;
        private static int _presentationTimeoutRecoveries;
        private static int _presentationTargetDeathRecoveries;
        private static int _presentationAttackerStopRecoveries;
        private static int _presentationRecoveryTimingMismatches;
        private static int _presentationMarkerMatched;
        private static int _presentationTracerMatched;
        private static int _presentationEnqueueMatched;
        private static int _presentationEmitMatched;
        private static int _presentationFailureDetails;
        private static int _presentationMarkerUnmatched;
        private static int _presentationTracerUnmatched;
        private static int _presentationEnqueueUnmatched;
        private static int _presentationEmitUnmatched;
        private static double _presentationMaximumAimDelta;
        private static double _presentationMaximumTimingDelta;
        private static double _presentationMaximumRecoveryDelta;
        private const int MaximumPresentationFailureDetails = 16;
        private static bool _contractLogged;
        // 한 세션에서 같은 계약 실패가 여러 공격/복제 콜백으로 반복될 수 있다.
        // 첫 원인만 Error로 승격하고 이후 원인은 Warn으로 보존해 Android에서 같은
        // stack trace가 수십 번 복제되지 않게 한다. 종료 축은 별도의 1 Error를 예약한다.
        private static bool _causalErrorEmitted;
        private static int _coordinatorFailureDetails;
        private const int MaximumExpiryArrivalRecords = 512;
        private const int ExpiryFrameWindow = 128;
        private struct ExpiryArrival
        {
            public double PublishServer, PublishLocal, ReceiveServer, ReceiveLocal, BridgeLocal;
            public double ReceiveAnchorServer, ReceiveAnchorLocal;
            public int ReceiveFrame, ReceiveAnchorRevision;
            public string ReceiveAnchorSource;
        }
        private struct ExpiryFrameGap
        {
            public double Start, End;
        }
        private static readonly Dictionary<AttackResultKey, ExpiryArrival> ExpiryArrivals = new();
        private static readonly ExpiryFrameGap[] ExpiryFrameGaps = new ExpiryFrameGap[ExpiryFrameWindow];
        private static int _expiryArrivalOverflow;
        private static int _expiryArrivalRecorded;
        private static int _expiryGapWrite;
        private static double _expiryPreviousFrameTime = double.NaN;

        /// <summary>
        /// 매 프레임에는 숫자 두 개만 보관한다. 실패가 발생한 경우에만 최근 구간을
        /// 훑어 수신 이후의 가장 긴 프레임 간격을 찾는다. 로그 문자열은 만들지 않는다.
        /// </summary>
        public static void RecordPresentationFrame(double localTime)
        {
            if (!_active || _isServer || double.IsNaN(localTime) || double.IsInfinity(localTime)) return;
            if (!double.IsNaN(_expiryPreviousFrameTime) && localTime >= _expiryPreviousFrameTime)
            {
                ExpiryFrameGaps[_expiryGapWrite] = new ExpiryFrameGap
                    { Start = _expiryPreviousFrameTime, End = localTime };
                _expiryGapWrite = (_expiryGapWrite + 1) % ExpiryFrameWindow;
            }
            _expiryPreviousFrameTime = localTime;
        }

        public static void RecordCompletedBundleArrival(AttackResultPresentationInput[] results,
            double publishServer, double publishLocal, double receiveServer,
            double receiveLocal, int receiveFrame)
        {
            if (!_active || _isServer || results == null) return;
            UnitAttackResultPresentationShadowBridge.ReadClockAnchorDiagnostic(
                out double anchorServer, out double anchorLocal,
                out int anchorRevision, out string anchorSource);
            foreach (var result in results)
            {
                if (ExpiryArrivals.ContainsKey(result.Key)) continue;
                if (ExpiryArrivals.Count >= MaximumExpiryArrivalRecords)
                {
                    Increment(ref _expiryArrivalOverflow);
                    continue;
                }
                ExpiryArrivals.Add(result.Key, new ExpiryArrival
                {
                    PublishServer = publishServer, PublishLocal = publishLocal,
                    ReceiveServer = receiveServer, ReceiveLocal = receiveLocal,
                    ReceiveFrame = receiveFrame, ReceiveAnchorServer = anchorServer,
                    ReceiveAnchorLocal = anchorLocal,
                    ReceiveAnchorRevision = anchorRevision,
                    ReceiveAnchorSource = anchorSource
                });
                Increment(ref _expiryArrivalRecorded);
            }
        }

        public static void RecordCompletedBundleBridgeEntry(
            AttackResultPresentationInput[] results, double bridgeLocal)
        {
            if (!_active || _isServer || results == null) return;
            foreach (var result in results)
                if (ExpiryArrivals.TryGetValue(result.Key, out ExpiryArrival arrival))
                {
                    arrival.BridgeLocal = bridgeLocal;
                    ExpiryArrivals[result.Key] = arrival;
                }
        }

        /// <summary>
        /// 두 peer의 로컬 시각은 비교하지 않는다. publish는 서버 시각, receive는
        /// Client의 서버 시각 추정값이므로 두 번째 결과는 오차를 포함한다.
        /// </summary>
        public static void ClassifyExpiryWindowForValidation(double due,
            double publishServer, double receiveServerEstimate, double decisionServerEstimate,
            out bool publishedAfterLimit, out bool receivedAfterLimitEstimate,
            out bool decidedAfterLimit)
        {
            double limit = due + AuthoritativeAttackPresentationCoordinator.CatchUpSeconds;
            publishedAfterLimit = publishServer > limit;
            receivedAfterLimitEstimate = receiveServerEstimate > limit;
            decidedAfterLimit = decisionServerEstimate > limit;
        }

        internal static void BeginSession(NetworkManager networkManager, bool isServer)
        {
            Reset();
            _networkManager = networkManager;
            _isServer = isServer;
            _active = networkManager != null && networkManager.IsListening;
            _runId = Guid.NewGuid().ToString("N");
            _sessionKey = CreateSharedSessionKey();
            _startedAt = ReadServerTime();
            UnitAttackResultPresentationShadowBridge.Classified -= ObservePresentationClassification;
            UnitAttackResultPresentationShadowBridge.Classified += ObservePresentationClassification;
            UnitAttackResultPresentationShadowBridge.Coordinator.Decided += ObserveCoordinatorDecision;
            if (!_active) return;

        }

        private static void ObserveCoordinatorDecision(AuthoritativePresentationDecision decision)
        {
            if (!_active) return;
            bool hasArrival = ExpiryArrivals.TryGetValue(decision.Result.Key, out ExpiryArrival arrival);
            ExpiryArrivals.Remove(decision.Result.Key);
            if (decision.Status == AuthoritativePresentationStatus.Ready
                || _coordinatorFailureDetails >= 16) return;
            _coordinatorFailureDetails++;
            var key = decision.Result.Key;
            if (decision.Status == AuthoritativePresentationStatus.Expired && !_isServer)
            {
                double decisionLocal = UnityEngine.Time.realtimeSinceStartupAsDouble;
                double maximumGap = 0d;
                double earliestGapStart = double.PositiveInfinity;
                int gapsSeen = 0;
                if (hasArrival)
                    foreach (var gap in ExpiryFrameGaps)
                    {
                        if (gap.End > 0d && gap.Start < earliestGapStart)
                            earliestGapStart = gap.Start;
                        if (gap.End >= arrival.ReceiveLocal && gap.Start <= decisionLocal
                            && gap.End >= gap.Start && gap.End <= decisionLocal)
                        {
                            gapsSeen++;
                            maximumGap = Math.Max(maximumGap, gap.End - gap.Start);
                        }
                    }
                UnitAttackResultPresentationShadowBridge.ReadClockAnchorDiagnostic(
                    out double anchorServer, out double anchorLocal,
                    out int anchorRevision, out string anchorSource);
                double due = decision.IntendedServerTime;
                ClassifyExpiryWindowForValidation(due, arrival.PublishServer,
                    arrival.ReceiveServer, decision.ObservedServerTime,
                    out bool publishLate, out bool receiveLateEstimate, out bool decisionLate);
                // 서로 다른 기기의 monotonic 시각은 빼지 않는다. 서버 시간축 차이도
                // 동기화 오차를 포함한 추정치이며 확정 네트워크 지연이 아니다.
                string keyData =
                    $"role=client, keyUnit={decision.Result.AttackerUnitId}, instance={key.AttackerInstanceId.Value}, " +
                    $"sequence={key.SequenceId.Value}, hit={key.HitIndex}, victimKind={key.VictimKind}, " +
                    $"victimId={key.VictimId}, effectKind={key.EffectKind}, ordinal={key.ResultOrdinal}";
                Log(LogLevel.Warn, "coordinator-expiry-timing-A",
                    keyData + ", " +
                    $"arrivalFound={hasArrival}, overflow={_expiryArrivalOverflow}, impactServer={decision.Result.ImpactServerTime:F6}, " +
                    $"due={due:F6}, expiryLimit={due + AuthoritativeAttackPresentationCoordinator.CatchUpSeconds:F6}, " +
                    $"publishAfterLimit={publishLate}, receiveAfterLimitEstimate={receiveLateEstimate}, decisionAfterLimit={decisionLate}, " +
                    $"publishServer={arrival.PublishServer:F6}, publishLocal={arrival.PublishLocal:F6}, " +
                    $"receiveServerEstimate={arrival.ReceiveServer:F6}, receiveLocal={arrival.ReceiveLocal:F6}, " +
                    $"receiveOverdueEstimate={arrival.ReceiveServer - due:F6}, publishToReceiveEstimate={arrival.ReceiveServer - arrival.PublishServer:F6}");
                Log(LogLevel.Warn, "coordinator-expiry-timing-B",
                    keyData + ", " +
                    $"receiveToDecisionLocal={decisionLocal - arrival.ReceiveLocal:F6}, receiveFrame={arrival.ReceiveFrame}, " +
                    $"rpcToBridgeLocal={arrival.BridgeLocal - arrival.ReceiveLocal:F6}, " +
                    $"decisionFrame={UnityEngine.Time.frameCount}, frameDelta={UnityEngine.Time.frameCount - arrival.ReceiveFrame}, " +
                    $"maxRecentFrameGap={maximumGap:F6}, recentGaps={gapsSeen}, " +
                    $"gapWindowCoversReceive={earliestGapStart <= arrival.ReceiveLocal}, " +
                    $"receiveAnchor={arrival.ReceiveAnchorSource}:{arrival.ReceiveAnchorRevision}:{arrival.ReceiveAnchorServer:F6}:{arrival.ReceiveAnchorLocal:F6}, " +
                    $"decisionAnchor={anchorSource}:{anchorRevision}:{anchorServer:F6}:{anchorLocal:F6}, " +
                    $"anchorAgeLocal={decisionLocal - anchorLocal:F6}, observedServer={decision.ObservedServerTime:F6}");
            }
            Log(LogLevel.Warn, "coordinator-failure",
                $"coordinatorSchema=c3-coordinator-comparison-v1, status={decision.Status}, " +
                $"instance={key.AttackerInstanceId.Value}, sequence={key.SequenceId.Value}, hit={key.HitIndex}, " +
                $"victimKind={key.VictimKind}, victimId={key.VictimId}, resultOrdinal={key.ResultOrdinal}, " +
                $"intended={decision.IntendedServerTime:F6}, observed={decision.ObservedServerTime:F6}");
        }

        internal static void RecordCombatContractLatched()
        {
            if (!_active || _contractLogged) return;
            _contractLogged = true;
            LogTerminal(
                LogLevel.Info,
                "BEGIN",
                $"role={(_isServer ? "host" : "client")}, observerSchema={ProductionSchema}, " +
                $"pipelineMode={NetworkContext.ActiveCombatPipelineMode}, " +
                $"combatSchema={NetworkContext.ActiveCombatSchemaRevision}, gameplayWrites=0, presentationEmits=0");
            if (_isServer)
                LogManifest();
        }

        /// <summary>
        /// InfernoSpirit Attack pose의 cached rendered Hips 방향을 상태 전이 세 지점에서만 읽는다.
        /// 이 경로는 전달받은 값의 분류와 기록만 수행하며 Transform/Animator/공격 scope를 쓰지 않는다.
        /// </summary>
        internal static void RecordAnimatedAttackFacing(
            AnimatedAttackFacingStage stage,
            int unitId,
            UnitType unitType,
            int targetId,
            bool targetIsUnit,
            ulong presentationRevision,
            AttackPresentationScope scope,
            bool viewFlipped,
            int currentAnimatorStateHash,
            int nextAnimatorStateHash,
            float normalizedTime,
            bool animatorInTransition,
            bool animatorIsHuman,
            Vector3 simulationRootForward,
            Vector3 visualRootForward,
            Vector3 renderedHipsForward,
            bool renderedHipsEvidenceAvailable,
            Vector3 simulationTargetDirection,
            Vector3 presentationTargetDirection,
            string unavailableReason)
        {
            if (!_active || unitType != UnitType.InfernoSpirit
                || !IsAnimatedAttackFacingStageForValidation(stage))
                return;

            ulong instanceId = scope.IsValid ? scope.AttackerInstanceId.Value : 0UL;
            ulong sequenceId = scope.IsValid ? scope.SequenceId.Value : 0UL;
            int hitIndex = scope.IsValid ? scope.HitIndex : -1;
            string exactKey = string.Concat(
                unitId.ToString(CultureInfo.InvariantCulture), ":",
                instanceId.ToString(CultureInfo.InvariantCulture), ":",
                sequenceId.ToString(CultureInfo.InvariantCulture), ":",
                presentationRevision.ToString(CultureInfo.InvariantCulture), ":",
                targetIsUnit ? "u:" : "b:",
                targetId.ToString(CultureInfo.InvariantCulture), ":",
                ((int)stage).ToString(CultureInfo.InvariantCulture));
            if (!TryAcceptAnimatedFacingEvidenceKeyForValidation(
                    AnimatedFacingEvidenceKeys,
                    exactKey,
                    MaximumAnimatedFacingEvidenceKeys))
            {
                Increment(ref _suppressedNormal);
                return;
            }

            bool simulationYawValid = TryCalculateAnimatedFacingSignedYawForValidation(
                simulationTargetDirection, simulationRootForward, out double simulationYaw);
            bool visualYawValid = TryCalculateAnimatedFacingSignedYawForValidation(
                presentationTargetDirection, visualRootForward, out double visualYaw);
            double renderedHipsYaw = double.NaN;
            bool renderedHipsYawValid = renderedHipsEvidenceAvailable
                && TryCalculateAnimatedFacingSignedYawForValidation(
                    presentationTargetDirection, renderedHipsForward, out renderedHipsYaw);
            double renderedHipsFromVisualYaw = double.NaN;
            bool renderedHipsFromVisualYawValid = renderedHipsEvidenceAvailable
                && TryCalculateAnimatedFacingSignedYawForValidation(
                    visualRootForward, renderedHipsForward, out renderedHipsFromVisualYaw);
            bool renderedHipsEvidenceValid = IsAnimatedRenderedHipsEvidenceValidForValidation(
                renderedHipsEvidenceAvailable,
                renderedHipsYawValid,
                renderedHipsFromVisualYawValid);

            string reason = renderedHipsEvidenceValid
                ? "available"
                : Normalize(string.Equals(unavailableReason, "available", StringComparison.Ordinal)
                    ? "direction-invalid"
                    : unavailableReason);
            Log(
                LogLevel.Info,
                "animated-facing",
                $"role={(_isServer ? "host" : "client")}, stage={stage}, unitId={unitId}, unitType={unitType}, " +
                $"targetKind={(targetIsUnit ? "Unit" : "Building")}, targetId={targetId}, " +
                $"scopeValid={scope.IsValid}, provisional={!scope.IsValid}, instance={instanceId}, sequence={sequenceId}, " +
                $"hit={hitIndex}, revision={presentationRevision}, viewFlipped={viewFlipped}, " +
                $"animatorCurrentState={currentAnimatorStateHash}, animatorNextState={nextAnimatorStateHash}, " +
                $"normalizedTime={normalizedTime.ToString("F4", CultureInfo.InvariantCulture)}, " +
                $"inTransition={animatorInTransition}, isHuman={animatorIsHuman}, renderedHipsEvidenceValid={renderedHipsEvidenceValid}, " +
                $"simulationForward={FormatVector(simulationRootForward)}, visualForward={FormatVector(visualRootForward)}, " +
                $"renderedHipsForward={(renderedHipsEvidenceAvailable ? FormatVector(renderedHipsForward) : "Unavailable")}, " +
                $"simulationTargetDirection={FormatVector(simulationTargetDirection)}, " +
                $"presentationTargetDirection={FormatVector(presentationTargetDirection)}, " +
                $"simulationYaw={FormatYaw(simulationYawValid, simulationYaw)}, " +
                $"visualYaw={FormatYaw(visualYawValid, visualYaw)}, renderedHipsYaw={FormatYaw(renderedHipsYawValid, renderedHipsYaw)}, " +
                $"renderedHipsFromVisualYaw={FormatYaw(renderedHipsFromVisualYawValid, renderedHipsFromVisualYaw)}, reason={reason}");
        }

        /// <summary>
        /// InfernoSpirit Animation Event 1회가 회차 허가와 VFX 파티클 재생의 어느 경계까지
        /// 도달했는지 한 줄로 기록한다. 공격·Animator·VFX 상태는 쓰지 않는다.
        /// </summary>
        internal static void RecordInfernoAttackVfxAttempt(
            int unitId,
            AttackPresentationImpactMode impactMode,
            bool consumedValidScope,
            bool sourceOnly,
            AttackPresentationScope scope,
            string outcome,
            bool managerAvailable,
            bool presetAvailable,
            bool prefabAvailable,
            bool poolItemAvailable,
            int particleSystemCount,
            bool playbackActive,
            Vector3 spawnPosition)
        {
            if (!_active) return;

            Increment(ref _infernoAttackVfxRawEvents);
            if (string.Equals(outcome, "started", StringComparison.Ordinal))
            {
                Increment(ref _infernoAttackVfxStarted);
                if (sourceOnly)
                    Increment(ref _infernoAttackVfxSourceOnlyPreserved);
            }
            else if (string.Equals(outcome, "gate-suppressed", StringComparison.Ordinal))
                Increment(ref _infernoAttackVfxGateSuppressed);
            else
                Increment(ref _infernoAttackVfxPlaybackFailures);

            if (_infernoAttackVfxRawEvents > MaximumInfernoAttackVfxEvents)
            {
                Increment(ref _suppressedNormal);
                if (!_infernoAttackVfxOverflowLogged)
                {
                    _infernoAttackVfxOverflowLogged = true;
                    Log(
                        LogLevel.Warn,
                        "inferno-vfx-overflow",
                        $"role={(_isServer ? "host" : "client")}, limit={MaximumInfernoAttackVfxEvents}");
                }
                return;
            }

            ulong instanceId = scope.IsValid ? scope.AttackerInstanceId.Value : 0UL;
            ulong sequenceId = scope.IsValid ? scope.SequenceId.Value : 0UL;
            int hitIndex = scope.IsValid ? scope.HitIndex : -1;
            Log(
                LogLevel.Info,
                "inferno-vfx-attempt",
                $"role={(_isServer ? "host" : "client")}, unitId={unitId}, rawEvent=true, " +
                $"mode={impactMode}, leaseConsumed={consumedValidScope}, sourceOnly={sourceOnly}, scopeValid={scope.IsValid}, " +
                $"instance={instanceId}, sequence={sequenceId}, hit={hitIndex}, outcome={Normalize(outcome)}, " +
                $"manager={managerAvailable}, preset={presetAvailable}, prefab={prefabAvailable}, " +
                $"poolItem={poolItemAvailable}, particleSystems={particleSystemCount}, " +
                $"playbackActive={playbackActive}, spawn={FormatVector(spawnPosition)}");
        }

        /// <summary>
        /// StreamSpirit의 서버 공격 commit이 Host와 Client의 표현 경계에 도착한 시점을 기록한다.
        /// Unresolved + LegacyFallback production만 받으므로 provisional Suppressed 시작이나 다른 유닛은
        /// 표본에 섞이지 않는다. 전달받은 revision은 읽기만 하며 공격 상태에는 되쓰지 않는다.
        /// </summary>
        internal static void RecordStreamSpiritTimelineStart(
            int unitId,
            UnitType unitType,
            int targetId,
            bool targetIsUnit,
            ulong presentationRevision,
            AttackPresentationImpactMode impactMode)
        {
            if (!_active || !ShouldObserveStreamSpiritTimelineForValidation(unitType, impactMode)
                || presentationRevision == 0UL)
                return;

            string key = BuildStreamSpiritTimelineKey(unitId, presentationRevision);
            bool duplicate = StreamSpiritTimelineFlows.ContainsKey(key);
            if (!duplicate && StreamSpiritTimelineFlows.Count >= MaximumStreamSpiritTimelineFlows)
                TryRetireOldestCompleteStreamSpiritTimelineFlow();
            StreamSpiritTimelineAdmission admission = ClassifyStreamSpiritTimelineAdmissionForValidation(
                StreamSpiritTimelineFlows.Count,
                duplicate,
                MaximumStreamSpiritTimelineFlows);
            if (admission == StreamSpiritTimelineAdmission.Duplicate)
            {
                Increment(ref _streamSpiritTimelineDuplicateTransitions);
                return;
            }
            if (admission != StreamSpiritTimelineAdmission.Accepted)
            {
                Increment(ref _suppressedNormal);
                if (admission == StreamSpiritTimelineAdmission.Overflow
                    && !_streamSpiritTimelineOverflowLogged)
                {
                    _streamSpiritTimelineOverflowLogged = true;
                    Log(
                        LogLevel.Warn,
                        "streamspirit-timeline-overflow",
                        $"role={(_isServer ? "host" : "client")}, schema={StreamSpiritTimelineSchema}, " +
                        $"limit={MaximumStreamSpiritTimelineFlows}, gameplayWrites=0");
                }
                return;
            }

            double now = ReadServerTime();
            StreamSpiritTimelineFlows.Add(key, new StreamSpiritTimelineFlow
            {
                UnitId = unitId,
                Revision = presentationRevision,
                TargetId = targetId,
                TargetIsUnit = targetIsUnit,
                StartServerTime = now
            });
            Increment(ref _streamSpiritTimelineStarts);
            Log(
                LogLevel.Info,
                "streamspirit-timeline",
                $"schema={StreamSpiritTimelineSchema}, role={(_isServer ? "host" : "client")}, " +
                $"stage=attack-start, unitId={unitId}, revision={presentationRevision}, " +
                $"targetKind={(targetIsUnit ? "Unit" : "Building")}, targetId={targetId}, " +
                $"serverTime={now:F6}, expectedImpactOffset={StreamSpiritExpectedImpactSeconds:F3}, " +
                "mode=LegacyFallback, resolver=Unresolved, gameplayWrites=0");
        }

        /// <summary>
        /// Animation Event가 실제 OnAttackHit 진입점에 도달한 시점을 기록한다.
        /// VFX 성공 여부는 아직 모르는 단계이므로 별도 VFX 결과 메서드에서 이어서 기록한다.
        /// </summary>
        internal static void RecordStreamSpiritTimelineMarker(
            int unitId,
            UnitType unitType,
            ulong presentationRevision,
            AttackPresentationImpactMode impactMode)
        {
            if (!_active || unitType != UnitType.StreamSpirit) return;

            string key = BuildStreamSpiritTimelineKey(unitId, presentationRevision);
            if (!StreamSpiritTimelineFlows.TryGetValue(key, out StreamSpiritTimelineFlow flow))
            {
                Increment(ref _streamSpiritTimelineUnmatchedMarkers);
                LogStreamSpiritUnmatched("marker", unitId, presentationRevision);
                return;
            }
            if (flow.MarkerObserved)
            {
                Increment(ref _streamSpiritTimelineDuplicateTransitions);
                return;
            }

            flow.MarkerObserved = true;
            flow.MarkerServerTime = ReadServerTime();
            Increment(ref _streamSpiritTimelineMarkers);
            LogStreamSpiritFlowStage(flow, "marker", "outcome=observed");
        }

        /// <summary>
        /// EffectManager 호출이 반환한 실제 재생 결과를 marker와 같은 revision에 붙인다.
        /// attempt와 started/failure를 분리해 프리셋 누락, 풀 실패, 비활성 재생을 숨기지 않는다.
        /// </summary>
        internal static void RecordStreamSpiritTimelineVfx(
            int unitId,
            UnitType unitType,
            ulong presentationRevision,
            AttackPresentationImpactMode impactMode,
            string outcome,
            int particleSystemCount,
            bool playbackActive)
        {
            if (!_active || unitType != UnitType.StreamSpirit) return;

            string key = BuildStreamSpiritTimelineKey(unitId, presentationRevision);
            if (!StreamSpiritTimelineFlows.TryGetValue(key, out StreamSpiritTimelineFlow flow))
            {
                Increment(ref _streamSpiritTimelineUnmatchedVfx);
                LogStreamSpiritUnmatched("vfx", unitId, presentationRevision);
                return;
            }
            if (flow.VfxAttempted)
            {
                Increment(ref _streamSpiritTimelineDuplicateTransitions);
                return;
            }

            flow.VfxAttempted = true;
            flow.VfxServerTime = ReadServerTime();
            flow.VfxStarted = string.Equals(outcome, "started", StringComparison.Ordinal)
                && playbackActive;
            Increment(ref _streamSpiritTimelineVfxAttempts);
            if (flow.VfxStarted)
                Increment(ref _streamSpiritTimelineVfxStarted);
            else
                Increment(ref _streamSpiritTimelineVfxFailures);
            LogStreamSpiritFlowStage(
                flow,
                "vfx-result",
                $"outcome={Normalize(outcome)}, particleSystems={particleSystemCount}, playbackActive={playbackActive}");
        }

        /// <summary>
        /// 서버의 실제 TimerImpact writer 반환 직후 호출된다. StreamSpirit는 단일 hit이고 같은 유닛의
        /// 다음 공격보다 0.50초 타격이 먼저 오므로, 동일 타겟의 가장 오래된 미완료 revision에 연결한다.
        /// 이 매칭은 진단 메모리만 갱신하며 피해 승인이나 HP에는 관여하지 않는다.
        /// </summary>
        internal static void RecordStreamSpiritTimerImpact(
            int unitId,
            UnitType unitType,
            int targetId,
            bool targetIsUnit,
            AttackDamageApplyStatus writerResult)
        {
            if (!_active || !_isServer || unitType != UnitType.StreamSpirit) return;

            StreamSpiritTimelineFlow match = null;
            foreach (StreamSpiritTimelineFlow candidate in StreamSpiritTimelineFlows.Values)
            {
                if (candidate.UnitId != unitId || candidate.TargetId != targetId
                    || candidate.TargetIsUnit != targetIsUnit || candidate.TimerImpactObserved)
                    continue;
                if (match == null || candidate.Revision < match.Revision)
                    match = candidate;
            }

            if (match == null)
            {
                Increment(ref _streamSpiritTimelineUnmatchedTimerImpacts);
                LogStreamSpiritUnmatched("timer-impact", unitId, 0UL);
                return;
            }

            match.TimerImpactObserved = true;
            match.TimerImpactServerTime = ReadServerTime();
            Increment(ref _streamSpiritTimelineTimerImpacts);
            LogStreamSpiritFlowStage(
                match,
                "timer-impact",
                $"writerResult={writerResult}");
        }

        public const string StreamSpiritTimelineSchema = "streamspirit-production-timeline-v1";

        public static bool ShouldObserveStreamSpiritTimelineForValidation(
            UnitType unitType,
            AttackPresentationImpactMode impactMode)
            => unitType == UnitType.StreamSpirit
                && impactMode == AttackPresentationImpactMode.LegacyFallback;

        public static bool StreamSpiritTimelineObserverIsReadOnlyForValidation()
            => true;

        public static int StreamSpiritTimelineCapacityForValidation()
            => MaximumStreamSpiritTimelineFlows;

        public static StreamSpiritTimelineAdmission ClassifyStreamSpiritTimelineAdmissionForValidation(
            int existingCount,
            bool duplicate,
            int capacity)
        {
            if (existingCount < 0 || capacity <= 0) return StreamSpiritTimelineAdmission.Invalid;
            if (duplicate) return StreamSpiritTimelineAdmission.Duplicate;
            return existingCount >= capacity
                ? StreamSpiritTimelineAdmission.Overflow
                : StreamSpiritTimelineAdmission.Accepted;
        }

        private static string BuildStreamSpiritTimelineKey(int unitId, ulong revision)
            => string.Concat(
                unitId.ToString(CultureInfo.InvariantCulture), ":",
                revision.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// 장기 경기에서도 최근 공격을 계속 관측하기 위해, 용량이 찼을 때 이미 필요한 경계가
        /// 모두 모인 가장 오래된 회차 하나만 집계값으로 옮기고 상세 레코드를 비운다.
        /// 미완료 회차는 제거하지 않아 marker/VFX/TimerImpact 순서 역전 증거를 보존한다.
        /// </summary>
        private static bool TryRetireOldestCompleteStreamSpiritTimelineFlow()
        {
            string oldestKey = null;
            StreamSpiritTimelineFlow oldest = null;
            foreach (KeyValuePair<string, StreamSpiritTimelineFlow> pair in StreamSpiritTimelineFlows)
            {
                StreamSpiritTimelineFlow candidate = pair.Value;
                bool complete = candidate.MarkerObserved && candidate.VfxAttempted
                    && (!_isServer || candidate.TimerImpactObserved);
                if (!complete) continue;
                if (oldest == null || candidate.StartServerTime < oldest.StartServerTime)
                {
                    oldestKey = pair.Key;
                    oldest = candidate;
                }
            }
            if (oldest == null) return false;

            _streamSpiritTimelineRetiredComplete++;
            _streamSpiritTimelineRetiredMaxMarkerFromStart = Math.Max(
                _streamSpiritTimelineRetiredMaxMarkerFromStart,
                Math.Abs(oldest.MarkerServerTime - oldest.StartServerTime));
            if (oldest.TimerImpactObserved)
            {
                _streamSpiritTimelineRetiredMaxTimerFromStart = Math.Max(
                    _streamSpiritTimelineRetiredMaxTimerFromStart,
                    Math.Abs(oldest.TimerImpactServerTime - oldest.StartServerTime));
                _streamSpiritTimelineRetiredMaxMarkerTimerDelta = Math.Max(
                    _streamSpiritTimelineRetiredMaxMarkerTimerDelta,
                    Math.Abs(oldest.MarkerServerTime - oldest.TimerImpactServerTime));
            }
            StreamSpiritTimelineFlows.Remove(oldestKey);
            return true;
        }

        private static void LogStreamSpiritUnmatched(string stage, int unitId, ulong revision)
        {
            Log(
                LogLevel.Warn,
                "streamspirit-timeline",
                $"schema={StreamSpiritTimelineSchema}, role={(_isServer ? "host" : "client")}, " +
                $"stage={stage}, unitId={unitId}, revision={revision}, outcome=unmatched, gameplayWrites=0");
        }

        private static void LogStreamSpiritFlowStage(
            StreamSpiritTimelineFlow flow,
            string stage,
            string outcomeData)
        {
            double now = stage == "marker" ? flow.MarkerServerTime
                : stage == "vfx-result" ? flow.VfxServerTime
                : flow.TimerImpactServerTime;
            double fromStart = now - flow.StartServerTime;
            Log(
                LogLevel.Info,
                "streamspirit-timeline",
                $"schema={StreamSpiritTimelineSchema}, role={(_isServer ? "host" : "client")}, " +
                $"stage={stage}, unitId={flow.UnitId}, revision={flow.Revision}, " +
                $"targetKind={(flow.TargetIsUnit ? "Unit" : "Building")}, targetId={flow.TargetId}, " +
                $"serverTime={now:F6}, fromStart={fromStart:F6}, {outcomeData}, gameplayWrites=0");
        }

        public const string FoxMagicianTimelineSchema = "foxmagician-production-timeline-v1";

        public static bool ShouldObserveFoxMagicianTimelineForValidation(
            UnitType unitType,
            AttackPresentationImpactMode impactMode)
            => unitType == UnitType.FoxMagician
                && impactMode == AttackPresentationImpactMode.LegacyFallback;

        public static bool FoxMagicianTimelineObserverIsReadOnlyForValidation()
            => true;

        public static int FoxMagicianTimelineCapacityForValidation()
            => MaximumFoxMagicianTimelineFlows;

        public static double FoxMagicianExpectedImpactSecondsForValidation()
            => FoxMagicianExpectedImpactSeconds;

        public static bool FoxMagicianTimerImpactBoundaryIsExactForValidation(
            int impactCount,
            double expectedImpactSeconds,
            double attackCooldownSeconds)
            => impactCount == 1
                && expectedImpactSeconds > 0d
                && expectedImpactSeconds < attackCooldownSeconds
                && !double.IsNaN(expectedImpactSeconds)
                && !double.IsInfinity(expectedImpactSeconds)
                && !double.IsNaN(attackCooldownSeconds)
                && !double.IsInfinity(attackCooldownSeconds);

        public static bool FoxMagicianCorrelationTicketMatchesForValidation(
            ulong expectedTicket,
            ulong observedTicket)
            => expectedTicket != 0UL && expectedTicket == observedTicket;

        public static bool FoxMagicianCorrelationBindingsHaveCapacityForValidation(
            int ticketKeyCount,
            int keyTicketCount,
            int capacity)
            => ticketKeyCount >= 0
                && keyTicketCount >= 0
                && capacity > 0
                && ticketKeyCount == keyTicketCount
                && ticketKeyCount < capacity;

        internal static ulong ReserveFoxMagicianTimelineCorrelationTicket(UnitType unitType)
        {
            if (!_active || !_isServer || unitType != UnitType.FoxMagician)
                return 0UL;

            if (_foxMagicianTimelineNextCorrelationTicket == ulong.MaxValue)
            {
                Increment(ref _foxMagicianTimelineDropped);
                return 0UL;
            }

            _foxMagicianTimelineNextCorrelationTicket++;
            return _foxMagicianTimelineNextCorrelationTicket;
        }

        internal static void BindFoxMagicianTimelineCorrelationTicket(
            ulong correlationTicket,
            int unitId,
            UnitType unitType,
            ulong presentationRevision,
            AttackPresentationImpactMode impactMode)
        {
            if (!_active || !_isServer || correlationTicket == 0UL || presentationRevision == 0UL
                || !ShouldObserveFoxMagicianTimelineForValidation(unitType, impactMode))
                return;

            string key = BuildFoxMagicianTimelineKey(unitId, presentationRevision);
            if (FoxMagicianTimelineTicketKeys.ContainsKey(correlationTicket)
                || FoxMagicianTimelineKeyTickets.ContainsKey(key))
            {
                Increment(ref _foxMagicianTimelineDuplicateTransitions);
                return;
            }

            if (!FoxMagicianCorrelationBindingsHaveCapacityForValidation(
                    FoxMagicianTimelineTicketKeys.Count,
                    FoxMagicianTimelineKeyTickets.Count,
                    MaximumFoxMagicianTimelineFlows))
            {
                // Bind는 RecordStart보다 먼저 실행되므로 완료된 가장 오래된 flow를 먼저
                // retire해 flow와 양방향 ticket dictionary가 같은 상한을 유지하게 한다.
                if (!TryRetireOldestCompleteFoxMagicianTimelineFlow())
                    TryRetireOldestExpiredFoxMagicianTimelineFlow(ReadServerTime());
            }
            if (!FoxMagicianCorrelationBindingsHaveCapacityForValidation(
                    FoxMagicianTimelineTicketKeys.Count,
                    FoxMagicianTimelineKeyTickets.Count,
                    MaximumFoxMagicianTimelineFlows))
            {
                Increment(ref _foxMagicianTimelineOverflow);
                Increment(ref _foxMagicianTimelineDropped);
                return;
            }

            FoxMagicianTimelineTicketKeys.Add(correlationTicket, key);
            FoxMagicianTimelineKeyTickets.Add(key, correlationTicket);
        }

        public static FoxMagicianTimelineAdmission ClassifyFoxMagicianTimelineAdmissionForValidation(
            int existingCount,
            bool duplicate,
            int capacity)
        {
            if (existingCount < 0 || capacity <= 0) return FoxMagicianTimelineAdmission.Invalid;
            if (duplicate) return FoxMagicianTimelineAdmission.Duplicate;
            return existingCount >= capacity
                ? FoxMagicianTimelineAdmission.Overflow
                : FoxMagicianTimelineAdmission.Accepted;
        }

        internal static void RecordFoxMagicianTimelineStart(
            int unitId,
            UnitType unitType,
            int targetId,
            bool targetIsUnit,
            ulong presentationRevision,
            AttackPresentationImpactMode impactMode)
        {
            if (!_active || !ShouldObserveFoxMagicianTimelineForValidation(unitType, impactMode)
                || presentationRevision == 0UL)
                return;

            string key = BuildFoxMagicianTimelineKey(unitId, presentationRevision);
            bool duplicate = FoxMagicianTimelineFlows.ContainsKey(key);
            if (!duplicate && FoxMagicianTimelineFlows.Count >= MaximumFoxMagicianTimelineFlows
                && !TryRetireOldestCompleteFoxMagicianTimelineFlow())
                TryRetireOldestExpiredFoxMagicianTimelineFlow(ReadServerTime());
            FoxMagicianTimelineAdmission admission = ClassifyFoxMagicianTimelineAdmissionForValidation(
                FoxMagicianTimelineFlows.Count,
                duplicate,
                MaximumFoxMagicianTimelineFlows);
            if (admission == FoxMagicianTimelineAdmission.Duplicate)
            {
                Increment(ref _foxMagicianTimelineDuplicateTransitions);
                return;
            }
            if (admission != FoxMagicianTimelineAdmission.Accepted)
            {
                Increment(ref _foxMagicianTimelineOverflow);
                RemoveFoxMagicianTimelineCorrelationForKey(key);
                if (!_foxMagicianTimelineOverflowLogged)
                {
                    _foxMagicianTimelineOverflowLogged = true;
                    LogFoxMagician(
                        LogLevel.Warn,
                        "foxmagician-timeline-overflow",
                        $"schema={FoxMagicianTimelineSchema}, role={(_isServer ? "host" : "client")}, " +
                        $"limit={MaximumFoxMagicianTimelineFlows}, gameplayWrites=0");
                }
                return;
            }

            double now = ReadServerTime();
            FoxMagicianTimelineKeyTickets.TryGetValue(key, out ulong correlationTicket);
            FoxMagicianTimelineFlows.Add(key, new FoxMagicianTimelineFlow
            {
                CorrelationTicket = correlationTicket,
                UnitId = unitId,
                Revision = presentationRevision,
                TargetId = targetId,
                TargetIsUnit = targetIsUnit,
                StartServerTime = now
            });
            Increment(ref _foxMagicianTimelineStarts);
            LogFoxMagician(
                LogLevel.Info,
                "foxmagician-timeline",
                $"schema={FoxMagicianTimelineSchema}, role={(_isServer ? "host" : "client")}, " +
                $"stage=attack-start, unitId={unitId}, revision={presentationRevision}, " +
                $"targetKind={(targetIsUnit ? "Unit" : "Building")}, targetId={targetId}, " +
                $"serverTime={now:F6}, expectedImpactOffset={FoxMagicianExpectedImpactSeconds:F3}, " +
                "mode=LegacyFallback, resolver=Unresolved, gameplayWrites=0");
        }

        internal static void RecordFoxMagicianTimelineMarker(
            int unitId,
            UnitType unitType,
            ulong presentationRevision,
            AttackPresentationImpactMode impactMode)
        {
            if (!_active || !ShouldObserveFoxMagicianTimelineForValidation(unitType, impactMode)) return;
            if (!TryGetFoxMagicianTimelineFlow(
                    "marker", unitId, presentationRevision, out FoxMagicianTimelineFlow flow))
                return;
            if (flow.MarkerObserved)
            {
                Increment(ref _foxMagicianTimelineDuplicateTransitions);
                return;
            }

            flow.MarkerObserved = true;
            flow.MarkerServerTime = ReadServerTime();
            Increment(ref _foxMagicianTimelineMarkers);
            LogFoxMagicianFlowStage(flow, "marker", flow.MarkerServerTime, "outcome=observed");
        }

        internal static void RecordFoxMagicianTimelineVfxAttempt(
            int unitId,
            UnitType unitType,
            ulong presentationRevision,
            AttackPresentationImpactMode impactMode)
        {
            if (!_active || !ShouldObserveFoxMagicianTimelineForValidation(unitType, impactMode)) return;
            if (!TryGetFoxMagicianTimelineFlow(
                    "vfx-attempt", unitId, presentationRevision, out FoxMagicianTimelineFlow flow))
                return;
            if (flow.VfxAttempted)
            {
                Increment(ref _foxMagicianTimelineDuplicateTransitions);
                return;
            }

            flow.VfxAttempted = true;
            flow.VfxAttemptServerTime = ReadServerTime();
            Increment(ref _foxMagicianTimelineVfxAttempts);
            LogFoxMagicianFlowStage(
                flow, "vfx-attempt", flow.VfxAttemptServerTime, "outcome=attempted");
        }

        internal static void RecordFoxMagicianTimelineVfxResult(
            int unitId,
            UnitType unitType,
            ulong presentationRevision,
            AttackPresentationImpactMode impactMode,
            string outcome,
            int particleSystemCount,
            bool playbackActive)
        {
            if (!_active || !ShouldObserveFoxMagicianTimelineForValidation(unitType, impactMode)) return;
            if (!TryGetFoxMagicianTimelineFlow(
                    "vfx-result", unitId, presentationRevision, out FoxMagicianTimelineFlow flow))
                return;
            if (flow.VfxResultObserved)
            {
                Increment(ref _foxMagicianTimelineDuplicateTransitions);
                return;
            }

            flow.VfxResultObserved = true;
            flow.VfxResultServerTime = ReadServerTime();
            int boundedParticleSystemCount = Math.Max(0, particleSystemCount);
            if (boundedParticleSystemCount > 0)
                Increment(ref _foxMagicianTimelineParticlePositiveResults);
            if (playbackActive)
                Increment(ref _foxMagicianTimelinePlaybackActiveResults);
            _foxMagicianTimelineMaxParticleSystemCount = Math.Max(
                _foxMagicianTimelineMaxParticleSystemCount,
                boundedParticleSystemCount);
            flow.VfxStarted = string.Equals(outcome, "started", StringComparison.Ordinal)
                && playbackActive;
            if (flow.VfxStarted)
                Increment(ref _foxMagicianTimelineVfxStarted);
            else
                Increment(ref _foxMagicianTimelineVfxFailures);
            LogFoxMagicianFlowStage(
                flow,
                "vfx-result",
                flow.VfxResultServerTime,
                $"outcome={Normalize(outcome)}, particleSystems={boundedParticleSystemCount}, playbackActive={playbackActive}");
        }

        internal static void RecordFoxMagicianTimerImpact(
            ulong correlationTicket,
            int unitId,
            UnitType unitType,
            int targetId,
            bool targetIsUnit,
            AttackDamageApplyStatus writerResult)
        {
            if (!_active || !_isServer || unitType != UnitType.FoxMagician) return;

            if (correlationTicket == 0UL
                || !FoxMagicianTimelineTicketKeys.TryGetValue(correlationTicket, out string key)
                || !FoxMagicianTimelineFlows.TryGetValue(key, out FoxMagicianTimelineFlow match)
                || match.CorrelationTicket != correlationTicket
                || match.UnitId != unitId
                || match.TargetId != targetId
                || match.TargetIsUnit != targetIsUnit)
            {
                Increment(ref _foxMagicianTimelineUnmatchedTimerImpacts);
                LogFoxMagicianUnmatched("timer-impact", unitId, 0UL);
                // Bind 뒤 Host UnitView에 start flow가 만들어지지 않은 실패에서도 exact ticket
                // mapping을 이 타격 경계에서 회수해 장기 경기의 진단 상태 누적을 막는다.
                RemoveFoxMagicianTimelineCorrelationForTicket(correlationTicket);
                return;
            }
            if (match.TimerImpactObserved)
            {
                Increment(ref _foxMagicianTimelineDuplicateTransitions);
                return;
            }

            match.TimerImpactObserved = true;
            match.TimerImpactServerTime = ReadServerTime();
            Increment(ref _foxMagicianTimelineTimerImpacts);
            LogFoxMagicianFlowStage(
                match,
                "timer-impact",
                match.TimerImpactServerTime,
                $"correlationTicket={correlationTicket}, writerResult={writerResult}");
        }

        private static string BuildFoxMagicianTimelineKey(int unitId, ulong revision)
            => string.Concat(
                unitId.ToString(CultureInfo.InvariantCulture), ":",
                revision.ToString(CultureInfo.InvariantCulture));

        private static bool TryGetFoxMagicianTimelineFlow(
            string stage,
            int unitId,
            ulong revision,
            out FoxMagicianTimelineFlow flow)
        {
            string key = BuildFoxMagicianTimelineKey(unitId, revision);
            if (FoxMagicianTimelineFlows.TryGetValue(key, out flow)) return true;

            if (stage == "marker") Increment(ref _foxMagicianTimelineUnmatchedMarkers);
            else if (stage == "vfx-attempt") Increment(ref _foxMagicianTimelineUnmatchedVfxAttempts);
            else Increment(ref _foxMagicianTimelineUnmatchedVfxResults);
            LogFoxMagicianUnmatched(stage, unitId, revision);
            return false;
        }

        private static bool TryRetireOldestCompleteFoxMagicianTimelineFlow()
        {
            string oldestKey = null;
            FoxMagicianTimelineFlow oldest = null;
            foreach (KeyValuePair<string, FoxMagicianTimelineFlow> pair in FoxMagicianTimelineFlows)
            {
                FoxMagicianTimelineFlow candidate = pair.Value;
                bool complete = candidate.MarkerObserved && candidate.VfxAttempted
                    && candidate.VfxResultObserved && (!_isServer || candidate.TimerImpactObserved);
                if (!complete) continue;
                if (oldest == null || candidate.StartServerTime < oldest.StartServerTime)
                {
                    oldestKey = pair.Key;
                    oldest = candidate;
                }
            }
            if (oldest == null) return false;

            _foxMagicianTimelineRetiredComplete++;
            AccumulateFoxMagicianTimelineMaxima(oldest);
            FoxMagicianTimelineFlows.Remove(oldestKey);
            RemoveFoxMagicianTimelineCorrelationForKey(oldestKey);
            return true;
        }

        public static bool FoxMagicianTimelineIncompleteExpiredForValidation(
            double now,
            double startedAt)
            => !double.IsNaN(now) && !double.IsInfinity(now)
                && !double.IsNaN(startedAt) && !double.IsInfinity(startedAt)
                && now - startedAt >= MaximumFoxMagicianTimelineIncompleteAgeSeconds;

        private static bool TryRetireOldestExpiredFoxMagicianTimelineFlow(double now)
        {
            string oldestKey = null;
            FoxMagicianTimelineFlow oldest = null;
            foreach (KeyValuePair<string, FoxMagicianTimelineFlow> pair in FoxMagicianTimelineFlows)
            {
                FoxMagicianTimelineFlow candidate = pair.Value;
                if (!FoxMagicianTimelineIncompleteExpiredForValidation(now, candidate.StartServerTime))
                    continue;
                if (oldest == null || candidate.StartServerTime < oldest.StartServerTime)
                {
                    oldestKey = pair.Key;
                    oldest = candidate;
                }
            }
            if (oldest == null) return false;

            Increment(ref _foxMagicianTimelineExpiredIncomplete);
            AccumulateFoxMagicianTimelineMaxima(oldest);
            FoxMagicianTimelineFlows.Remove(oldestKey);
            RemoveFoxMagicianTimelineCorrelationForKey(oldestKey);
            return true;
        }

        private static void RemoveFoxMagicianTimelineCorrelationForKey(string key)
        {
            if (!FoxMagicianTimelineKeyTickets.TryGetValue(key, out ulong ticket)) return;
            FoxMagicianTimelineKeyTickets.Remove(key);
            FoxMagicianTimelineTicketKeys.Remove(ticket);
        }

        private static void RemoveFoxMagicianTimelineCorrelationForTicket(ulong ticket)
        {
            if (ticket == 0UL
                || !FoxMagicianTimelineTicketKeys.TryGetValue(ticket, out string key))
                return;
            FoxMagicianTimelineTicketKeys.Remove(ticket);
            FoxMagicianTimelineKeyTickets.Remove(key);
        }

        private static void AccumulateFoxMagicianTimelineMaxima(FoxMagicianTimelineFlow flow)
        {
            if (flow.MarkerObserved)
                _foxMagicianTimelineRetiredMaxMarkerFromStart = Math.Max(
                    _foxMagicianTimelineRetiredMaxMarkerFromStart,
                    Math.Abs(flow.MarkerServerTime - flow.StartServerTime));
            if (flow.TimerImpactObserved)
                _foxMagicianTimelineRetiredMaxTimerFromStart = Math.Max(
                    _foxMagicianTimelineRetiredMaxTimerFromStart,
                    Math.Abs(flow.TimerImpactServerTime - flow.StartServerTime));
            if (flow.MarkerObserved && flow.TimerImpactObserved)
                _foxMagicianTimelineRetiredMaxMarkerTimerDelta = Math.Max(
                    _foxMagicianTimelineRetiredMaxMarkerTimerDelta,
                    Math.Abs(flow.MarkerServerTime - flow.TimerImpactServerTime));
        }

        private static void LogFoxMagicianUnmatched(string stage, int unitId, ulong revision)
        {
            LogFoxMagician(
                LogLevel.Warn,
                "foxmagician-timeline",
                $"schema={FoxMagicianTimelineSchema}, role={(_isServer ? "host" : "client")}, " +
                $"stage={stage}, unitId={unitId}, revision={revision}, outcome=unmatched, gameplayWrites=0");
        }

        private static void LogFoxMagicianFlowStage(
            FoxMagicianTimelineFlow flow,
            string stage,
            double now,
            string outcomeData)
        {
            LogFoxMagician(
                LogLevel.Info,
                "foxmagician-timeline",
                $"schema={FoxMagicianTimelineSchema}, role={(_isServer ? "host" : "client")}, " +
                $"stage={stage}, unitId={flow.UnitId}, revision={flow.Revision}, " +
                $"targetKind={(flow.TargetIsUnit ? "Unit" : "Building")}, targetId={flow.TargetId}, " +
                $"serverTime={now:F6}, fromStart={now - flow.StartServerTime:F6}, {outcomeData}, gameplayWrites=0");
        }

        private static void LogFoxMagician(LogLevel level, string message, string data)
        {
            if (_foxMagicianTimelineDetailLines >= MaximumFoxMagicianTimelineDetailLines)
            {
                Increment(ref _foxMagicianTimelineDetailSuppressed);
                return;
            }
            _foxMagicianTimelineDetailLines++;
            bool willDrop = _lines >= MaximumNormalLines;
            Log(level, message, data);
            if (willDrop) Increment(ref _foxMagicianTimelineDropped);
        }

        public static bool IsAnimatedAttackFacingStageForValidation(AnimatedAttackFacingStage stage)
            => stage == AnimatedAttackFacingStage.AttackStartPose
                || stage == AnimatedAttackFacingStage.ImpactMarker
                || stage == AnimatedAttackFacingStage.AttackEndPose;

        public static bool TryCalculateAnimatedFacingSignedYawForValidation(
            Vector3 fromDirection,
            Vector3 toForward,
            out double signedYawDegrees)
        {
            signedYawDegrees = double.NaN;
            if (!TryNormalizePlanar(fromDirection, out Vector3 from)
                || !TryNormalizePlanar(toForward, out Vector3 to))
                return false;
            double crossY = (double)from.z * to.x - (double)from.x * to.z;
            double dot = (double)from.x * to.x + (double)from.z * to.z;
            signedYawDegrees = Math.Atan2(crossY, dot) * (180d / Math.PI);
            return !double.IsNaN(signedYawDegrees) && !double.IsInfinity(signedYawDegrees);
        }

        public static bool TryAcceptAnimatedFacingEvidenceKeyForValidation(
            ISet<string> keys,
            string exactKey,
            int capacity)
        {
            if (keys == null || string.IsNullOrEmpty(exactKey) || capacity <= 0
                || keys.Contains(exactKey) || keys.Count >= capacity)
                return false;
            keys.Add(exactKey);
            return true;
        }

        public static bool IsAnimatedRenderedHipsEvidenceValidForValidation(
            bool renderedHipsEvidenceAvailable,
            bool targetYawValid,
            bool renderedHipsFromVisualYawValid)
            => renderedHipsEvidenceAvailable && targetYawValid && renderedHipsFromVisualYawValid;

        /// <summary>
        /// InfernoSpirit 한 프레임의 세 위치 계층을 읽어, 정상 이동은 버리고 임계 사건만 기록한다.
        /// 이 메서드는 전달된 숫자를 비교할 뿐 Transform, Animator, 이동 상태 또는 네트워크 값을
        /// 변경하지 않는다. Host의 대응 표본이 없는 이 로컬 호출에서는 client-only 원인을 만들지 않는다.
        /// </summary>
        internal static void RecordInfernoMotionJump(InfernoMotionJumpSample sample)
        {
            if (!_active) return;

            float simulationDelta = PlanarDistance(sample.SimulationPrevious, sample.SimulationCurrent);
            float visualDelta = sample.VisualAvailable
                ? PlanarDistance(sample.VisualPrevious, sample.VisualCurrent)
                : float.NaN;
            float renderedAnchorDelta = sample.RenderedAnchorAvailable
                ? PlanarDistance(sample.RenderedAnchorPrevious, sample.RenderedAnchorCurrent)
                : float.NaN;
            float rendererBoundsDelta = sample.RendererBoundsAvailable
                ? PlanarDistance(sample.RendererBoundsPrevious, sample.RendererBoundsCurrent)
                : float.NaN;

            // 화면 Root가 회전할 때 Hips가 Root 중심에서 떨어져 있으면, 클립이 위치를 전혀
            // 움직이지 않아도 Hips는 원호를 따라 이동한다. Root 자체의 평행 이동을 제거한
            // 상대 좌표끼리 비교해야 이 원호와 clip/transition 변위를 분리할 수 있다.
            bool simulationRotationAvailable = TryCalculatePlanarRotationDeltaForValidation(
                sample.SimulationRotationPrevious,
                sample.SimulationRotationCurrent,
                out float simulationRotationDelta);
            float visualRotationDelta = float.NaN;
            bool visualRotationAvailable = sample.VisualAvailable
                && TryCalculatePlanarRotationDeltaForValidation(
                    sample.VisualRotationPrevious,
                    sample.VisualRotationCurrent,
                    out visualRotationDelta);
            bool arcInputsAvailable = sample.VisualAvailable && sample.RenderedAnchorAvailable;
            float rootToAnchorOffset = arcInputsAvailable
                ? PlanarDistance(sample.VisualPrevious, sample.RenderedAnchorPrevious)
                : float.NaN;
            float currentRootToAnchorOffset = arcInputsAvailable
                ? PlanarDistance(sample.VisualCurrent, sample.RenderedAnchorCurrent)
                : float.NaN;
            Vector3 previousRelativeAnchor = arcInputsAvailable
                ? sample.RenderedAnchorPrevious - sample.VisualPrevious
                : default;
            Vector3 currentRelativeAnchor = arcInputsAvailable
                ? sample.RenderedAnchorCurrent - sample.VisualCurrent
                : default;
            float observedRelativeAnchorDelta = arcInputsAvailable
                ? PlanarDistance(previousRelativeAnchor, currentRelativeAnchor)
                : float.NaN;
            float predictedArcDistance = float.NaN;
            float arcResidual = float.NaN;
            bool arcPredictionMatches = false;
            bool arcPredictionAvailable = arcInputsAvailable
                && visualRotationAvailable
                && TryCalculateInfernoArcEvidenceForValidation(
                    rootToAnchorOffset,
                    visualRotationDelta,
                    observedRelativeAnchorDelta,
                    InfernoMotionArcResidualTolerance,
                    out predictedArcDistance,
                    out arcResidual,
                    out arcPredictionMatches);

            bool allowanceValid = TryCalculateInfernoMotionAllowanceForValidation(
                sample.ProductionWorldSpeed,
                sample.DeltaTime,
                InfernoMotionJumpPositionTolerance,
                out float rawAllowance,
                out float nominalAllowance,
                out bool scaledDeltaLongFrame);
            bool longFrame = scaledDeltaLongFrame
                || (IsFinite(sample.RawFrameSeconds)
                    && sample.RawFrameSeconds > InfernoMotionJumpLongFrameSeconds);
            bool sampleValid = IsFiniteVector(sample.SimulationPrevious)
                && IsFiniteVector(sample.SimulationCurrent)
                && (!sample.VisualAvailable
                    || (IsFiniteVector(sample.VisualPrevious) && IsFiniteVector(sample.VisualCurrent)))
                && (!sample.RenderedAnchorAvailable
                    || (IsFiniteVector(sample.RenderedAnchorPrevious)
                        && IsFiniteVector(sample.RenderedAnchorCurrent)))
                && (!sample.RendererBoundsAvailable
                    || (IsFiniteVector(sample.RendererBoundsPrevious)
                        && IsFiniteVector(sample.RendererBoundsCurrent)));

            InfernoMotionJumpClassification classification =
                ClassifyInfernoMotionJumpForValidation(
                    sampleValid,
                    sample.SpeedAvailable && allowanceValid,
                    simulationDelta,
                    sample.VisualAvailable,
                    visualDelta,
                    sample.RenderedAnchorAvailable,
                    renderedAnchorDelta,
                    rawAllowance,
                    nominalAllowance,
                    InfernoMotionJumpPositionTolerance,
                    longFrame,
                    !_isServer,
                    hasHostPeerEvidence: false,
                    hostPeerJump: false,
                    replicationGapConfirmed: false);
            if (classification == InfernoMotionJumpClassification.None) return;

            InfernoMotionJumpAcceptResult accept = InfernoMotionJumpEvidence.TryAccept(
                sample.UnitId,
                sample.Lifecycle,
                sample.Frame,
                classification,
                sample.WalkAttackTransition,
                out bool firstOverflow);
            if (accept != InfernoMotionJumpAcceptResult.Accepted)
            {
                Increment(ref _suppressedNormal);
                if (firstOverflow)
                {
                    LogInfernoMotionJump(
                        LogLevel.Warn,
                        "overflow",
                        $"role={(_isServer ? "host" : "client")}, unitId={sample.UnitId}, " +
                        $"lifecycle={sample.Lifecycle}, reason={accept}, " +
                        $"perUnitLimit={MaximumInfernoMotionJumpEventsPerUnit}, " +
                        $"unitLimit={MaximumInfernoMotionJumpUnits}, keyLimit={MaximumInfernoMotionJumpKeys}");
                }
                return;
            }

            string compact = BuildInfernoMotionCompactEventForValidation(
                ToInfernoMotionJumpLogValue(classification),
                _isServer ? "host" : "client",
                sample.UnitId.ToString(CultureInfo.InvariantCulture),
                sample.NetworkIdentityAvailable
                    ? sample.NetworkObjectId.ToString(CultureInfo.InvariantCulture)
                    : "Unavailable",
                sample.Lifecycle.ToString(CultureInfo.InvariantCulture),
                sample.PreviousFrame.ToString(CultureInfo.InvariantCulture) + "->"
                    + sample.Frame.ToString(CultureInfo.InvariantCulture),
                sample.PreviousTime.ToString("F6", CultureInfo.InvariantCulture) + "->"
                    + sample.Time.ToString("F6", CultureInfo.InvariantCulture),
                sample.MovementEvidenceAvailable ? sample.MovementPhase.ToString() : "Unavailable",
                sample.WalkAttackTransition.ToString(),
                FormatDistance(sample.SpeedAvailable && allowanceValid, rawAllowance),
                simulationDelta.ToString("F4", CultureInfo.InvariantCulture),
                FormatDistance(sample.VisualAvailable, visualDelta),
                FormatDistance(sample.RenderedAnchorAvailable, renderedAnchorDelta),
                FormatDistance(simulationRotationAvailable, simulationRotationDelta),
                FormatDistance(visualRotationAvailable, visualRotationDelta),
                FormatDistance(arcInputsAvailable, rootToAnchorOffset),
                FormatDistance(arcPredictionAvailable, predictedArcDistance),
                FormatSignedDistance(arcPredictionAvailable, arcResidual),
                arcPredictionAvailable ? arcPredictionMatches.ToString() : "Unavailable",
                $"arcObserved={FormatDistance(arcInputsAvailable, observedRelativeAnchorDelta)}, " +
                $"offsetNow={FormatDistance(arcInputsAvailable, currentRootToAnchorOffset)}, " +
                $"boundsD={FormatDistance(sample.RendererBoundsAvailable, rendererBoundsDelta)}, " +
                $"long={longFrame}, nomAllow={FormatDistance(sample.SpeedAvailable && allowanceValid, nominalAllowance)}, " +
                $"cmd={(sample.MovementEvidenceAvailable ? sample.CommandRevision.ToString(CultureInfo.InvariantCulture) : "Unavailable")}, " +
                $"seg={(sample.MovementEvidenceAvailable ? sample.SegmentRevision.ToString(CultureInfo.InvariantCulture) : "Unavailable")}, " +
                $"sem={(sample.ReplicationEvidenceAvailable ? sample.SemanticRevision.ToString(CultureInfo.InvariantCulture) : "Unavailable")}");
            LogInfernoMotionJump(LogLevel.Info, "event", compact);
        }

        /// <summary>
        /// Root 중심에서 떨어진 rendered Hips가 회전만으로 이동할 원호 거리와 실제 상대 이동을 비교한다.
        /// invalid 입력을 0으로 바꾸지 않고 false로 반환하여 원인 분류가 추정값으로 진행되지 않게 한다.
        /// </summary>
        public static bool TryCalculateInfernoArcEvidenceForValidation(
            float planarOffset,
            float rotationDeltaDegrees,
            float observedRelativeAnchorDelta,
            float residualTolerance,
            out float predictedArcDistance,
            out float residual,
            out bool matches)
        {
            predictedArcDistance = float.NaN;
            residual = float.NaN;
            matches = false;
            if (!IsFiniteNonNegative(planarOffset)
                || !IsFiniteNonNegative(rotationDeltaDegrees)
                || rotationDeltaDegrees > 180f
                || !IsFiniteNonNegative(observedRelativeAnchorDelta)
                || !IsFiniteNonNegative(residualTolerance))
                return false;

            double halfRadians = rotationDeltaDegrees * (Math.PI / 180d) * 0.5d;
            predictedArcDistance = (float)(2d * planarOffset * Math.Sin(halfRadians));
            residual = observedRelativeAnchorDelta - predictedArcDistance;
            if (!IsFinite(predictedArcDistance) || !IsFinite(residual)) return false;
            matches = Mathf.Abs(residual) <= residualTolerance;
            return true;
        }

        public static bool TryCalculatePlanarRotationDeltaForValidation(
            Quaternion previous,
            Quaternion current,
            out float absoluteDegrees)
        {
            absoluteDegrees = float.NaN;
            if (!IsFiniteQuaternion(previous) || !IsFiniteQuaternion(current)
                || !TryCalculateAnimatedFacingSignedYawForValidation(
                    previous * Vector3.forward,
                    current * Vector3.forward,
                    out double signedDegrees))
                return false;
            absoluteDegrees = (float)Math.Abs(signedDegrees);
            return IsFiniteNonNegative(absoluteDegrees) && absoluteDegrees <= 180f;
        }

        public static string BuildInfernoMotionCompactEventForValidation(
            string classification,
            string role,
            string unit,
            string network,
            string lifecycle,
            string frame,
            string time,
            string phase,
            string walkAttackTransition,
            string allowance,
            string simulationDelta,
            string visualDelta,
            string anchorDelta,
            string simulationRotationDelta,
            string visualRotationDelta,
            string rootToAnchorOffset,
            string predictedArc,
            string residual,
            string arcMatch,
            string context)
            => $"classification={Normalize(classification)}, role={Normalize(role)}, " +
                $"unit={Normalize(unit)}, network={Normalize(network)}, lifecycle={Normalize(lifecycle)}, " +
                $"frame={Normalize(frame)}, time={Normalize(time)}, phase={Normalize(phase)}, " +
                $"walkAttackTransition={Normalize(walkAttackTransition)}, allowance={Normalize(allowance)}, " +
                $"simDelta={Normalize(simulationDelta)}, visualDelta={Normalize(visualDelta)}, anchorDelta={Normalize(anchorDelta)}, " +
                $"simRotationDelta={Normalize(simulationRotationDelta)}, visualRotationDelta={Normalize(visualRotationDelta)}, " +
                $"rootToAnchorOffset={Normalize(rootToAnchorOffset)}, predictedArc={Normalize(predictedArc)}, " +
                $"residual={Normalize(residual)}, arcMatch={Normalize(arcMatch)}, {context}";

        public static int GetInfernoMotionCompactWorstCaseUtf8BytesForValidation(string data)
        {
            string decorated = "runId=" + new string('r', 64)
                + ", sharedSessionKey=" + new string('s', 64)
                + ", startedAt=9999999999.999999, " + data;
            return Encoding.UTF8.GetByteCount(FormatInfernoMotionFullLine("event", decorated));
        }

        internal static void RetireInfernoMotionJump(int unitId, ulong lifecycle)
            => InfernoMotionJumpEvidence.Retire(unitId, lifecycle);

        internal static void RetireInfernoMotionJumpUnit(int unitId)
            => InfernoMotionJumpEvidence.RetireUnit(unitId);

        public static bool TryCalculateInfernoMotionAllowanceForValidation(
            float productionWorldSpeed,
            float deltaTime,
            float tolerance,
            out float rawAllowance,
            out float nominalAllowance,
            out bool longFrame)
        {
            rawAllowance = float.NaN;
            nominalAllowance = float.NaN;
            longFrame = false;
            if (!IsFinite(productionWorldSpeed) || productionWorldSpeed < 0f
                || !IsFinite(deltaTime) || deltaTime <= 0f
                || !IsFinite(tolerance) || tolerance < 0f)
                return false;

            longFrame = deltaTime > InfernoMotionJumpLongFrameSeconds;
            rawAllowance = productionWorldSpeed * deltaTime + tolerance;
            float nominalDeltaTime = Mathf.Min(deltaTime, InfernoMotionJumpNominalFrameSeconds);
            nominalAllowance = productionWorldSpeed * nominalDeltaTime + tolerance;
            return IsFinite(rawAllowance) && IsFinite(nominalAllowance);
        }

        public static InfernoMotionJumpClassification ClassifyInfernoMotionJumpForValidation(
            bool sampleValid,
            bool allowanceValid,
            float simulationDelta,
            bool visualAvailable,
            float visualDelta,
            bool renderedAnchorAvailable,
            float renderedAnchorDelta,
            float rawAllowance,
            float nominalAllowance,
            float layerTolerance,
            bool longFrame,
            bool isClient,
            bool hasHostPeerEvidence,
            bool hostPeerJump,
            bool replicationGapConfirmed)
        {
            bool finite = sampleValid
                && IsFiniteNonNegative(simulationDelta)
                && (!visualAvailable || IsFiniteNonNegative(visualDelta))
                && (!renderedAnchorAvailable || IsFiniteNonNegative(renderedAnchorDelta))
                && IsFiniteNonNegative(layerTolerance);
            if (!finite || !allowanceValid
                || !IsFiniteNonNegative(rawAllowance)
                || !IsFiniteNonNegative(nominalAllowance))
            {
                bool hasStructuralCandidate = visualAvailable
                        && IsFiniteNonNegative(simulationDelta)
                        && IsFiniteNonNegative(visualDelta)
                        && Mathf.Abs(visualDelta - simulationDelta) > layerTolerance
                    || renderedAnchorAvailable
                        && visualAvailable
                        && IsFiniteNonNegative(renderedAnchorDelta)
                        && IsFiniteNonNegative(visualDelta)
                        && Mathf.Abs(renderedAnchorDelta - visualDelta) > layerTolerance;
                return hasStructuralCandidate || !sampleValid
                    ? InfernoMotionJumpClassification.UnavailableAmbiguous
                    : InfernoMotionJumpClassification.None;
            }

            bool simulationCandidate = simulationDelta > nominalAllowance;
            bool visualCandidate = visualAvailable
                && (visualDelta > nominalAllowance
                    || Mathf.Abs(visualDelta - simulationDelta) > layerTolerance);
            bool renderedAnchorCandidate = renderedAnchorAvailable
                && (renderedAnchorDelta > nominalAllowance
                    || visualAvailable
                        && Mathf.Abs(renderedAnchorDelta - visualDelta) > layerTolerance);
            if (!simulationCandidate && !visualCandidate && !renderedAnchorCandidate)
                return InfernoMotionJumpClassification.None;

            bool simulationRawJump = simulationDelta > rawAllowance;
            if (simulationRawJump)
            {
                if (isClient && hasHostPeerEvidence && !hostPeerJump
                    && replicationGapConfirmed)
                    return InfernoMotionJumpClassification.ClientOnlyReplicationGap;
                if (!isClient || hasHostPeerEvidence && hostPeerJump)
                    return InfernoMotionJumpClassification.AuthoritativeRootJump;

                // Client의 Simulation Root는 서버 권위 원본이 아니라 NetworkTransform이 전달한
                // 복제 결과다. 대응 Host 표본 없이 큰 delta 하나만 보고 권위 Root가 점프했다고
                // 이름 붙이면 증거 수준을 넘으므로 원인을 확정하지 않고 닫는다.
                return InfernoMotionJumpClassification.UnavailableAmbiguous;
            }
            if (visualAvailable
                && (visualDelta > rawAllowance
                    || Mathf.Abs(visualDelta - simulationDelta) > layerTolerance))
                return InfernoMotionJumpClassification.VisualProjectionJump;
            if (renderedAnchorAvailable && visualAvailable
                && visualDelta <= rawAllowance
                && (renderedAnchorDelta > rawAllowance
                    || Mathf.Abs(renderedAnchorDelta - visualDelta) > layerTolerance))
                return InfernoMotionJumpClassification.PresentationAnchorJump;
            return longFrame
                ? InfernoMotionJumpClassification.LongFrameCatchUp
                : InfernoMotionJumpClassification.UnavailableAmbiguous;
        }

        public sealed class InfernoMotionJumpLimiterForValidation
        {
            private readonly InfernoMotionJumpLimiter _limiter;

            public InfernoMotionJumpLimiterForValidation(int unitCapacity, int perUnitCapacity, int keyCapacity)
            {
                _limiter = new InfernoMotionJumpLimiter(unitCapacity, perUnitCapacity, keyCapacity);
            }

            public string TryAccept(
                int unitId,
                ulong lifecycle,
                int frame,
                InfernoMotionJumpClassification classification,
                bool walkAttackTransition = false)
                => _limiter.TryAccept(
                    unitId,
                    lifecycle,
                    frame,
                    classification,
                    walkAttackTransition,
                    out _).ToString();

            public void Retire(int unitId, ulong lifecycle) => _limiter.Retire(unitId, lifecycle);
            public void RetireUnit(int unitId) => _limiter.RetireUnit(unitId);
            public void Reset() => _limiter.Reset();
            public int UnitCount => _limiter.UnitCount;
            public int KeyCount => _limiter.KeyCount;
        }

        public static bool IsInfernoRenderedAnchorEvidenceValidForValidation(
            bool exactlyOneRenderer,
            bool rendererAvailable,
            bool rootBoneAvailable,
            bool rootBoneMatchesAnimatorHips,
            bool positionFinite)
            => exactlyOneRenderer
                && rendererAvailable
                && rootBoneAvailable
                && rootBoneMatchesAnimatorHips
                && positionFinite;

        public static bool ShouldRecordInfernoMotionPairForValidation(
            bool hasPreviousFrame,
            bool previousRenderedAnchorAvailable,
            bool renderedAnchorAvailable)
            => hasPreviousFrame
                && (!renderedAnchorAvailable || previousRenderedAnchorAvailable);

        public static bool InfernoMotionObserverIsReadOnlyForValidation()
            => true;

        private enum InfernoMotionJumpAcceptResult : byte
        {
            Accepted = 0,
            Duplicate = 1,
            Overflow = 2,
            Invalid = 3
        }

        private sealed class InfernoMotionJumpLimiter
        {
            private sealed class UnitBudget
            {
                internal ulong Lifecycle;
                internal int Accepted;
                internal int GeneralAccepted;
                internal bool SevereTransitionAccepted;
                internal bool OverflowLogged;
            }

            private readonly int _unitCapacity;
            private readonly int _perUnitCapacity;
            private readonly int _perUnitGeneralCapacity;
            private readonly int _keyCapacity;
            private readonly int _generalKeyCapacity;
            private readonly Dictionary<int, UnitBudget> _units = new Dictionary<int, UnitBudget>();
            private readonly HashSet<string> _keys = new HashSet<string>();
            private readonly HashSet<string> _severeTransitionKeys = new HashSet<string>();
            private int _generalAccepted;
            private bool _globalOverflowLogged;

            internal InfernoMotionJumpLimiter(int unitCapacity, int perUnitCapacity, int keyCapacity)
            {
                _unitCapacity = unitCapacity;
                _perUnitCapacity = perUnitCapacity;
                _perUnitGeneralCapacity = Math.Max(0, perUnitCapacity - 1);
                _keyCapacity = keyCapacity;
                // 각 유닛의 첫 Walk↔Attack 임계 사건이 작은 일반 사건 뒤에 와도 기록되도록
                // 전역 key 상한 안에서 유닛당 한 칸을 예약한다. 전체 상한 자체는 늘리지 않는다.
                _generalKeyCapacity = Math.Max(0, keyCapacity - unitCapacity);
            }

            internal int UnitCount => _units.Count;
            internal int KeyCount => _keys.Count;

            internal InfernoMotionJumpAcceptResult TryAccept(
                int unitId,
                ulong lifecycle,
                int frame,
                InfernoMotionJumpClassification classification,
                bool walkAttackTransition,
                out bool firstOverflow)
            {
                firstOverflow = false;
                if (unitId < 0 || lifecycle == 0UL || frame < 0
                    || classification == InfernoMotionJumpClassification.None)
                    return InfernoMotionJumpAcceptResult.Invalid;

                if (!_units.TryGetValue(unitId, out UnitBudget budget))
                {
                    if (_units.Count >= _unitCapacity)
                    {
                        firstOverflow = !_globalOverflowLogged;
                        _globalOverflowLogged = true;
                        return InfernoMotionJumpAcceptResult.Overflow;
                    }
                    budget = new UnitBudget { Lifecycle = lifecycle };
                    _units.Add(unitId, budget);
                }
                else if (budget.Lifecycle != lifecycle)
                {
                    RemoveKeys(unitId, budget.Lifecycle);
                    budget.Lifecycle = lifecycle;
                    budget.Accepted = 0;
                    budget.GeneralAccepted = 0;
                    budget.SevereTransitionAccepted = false;
                    budget.OverflowLogged = false;
                }

                string key = BuildKey(unitId, lifecycle, frame, classification);
                if (_keys.Contains(key)) return InfernoMotionJumpAcceptResult.Duplicate;
                bool usesSevereTransitionReservation = walkAttackTransition
                    && !budget.SevereTransitionAccepted;
                bool generalCapacityReached = !usesSevereTransitionReservation
                    && (budget.GeneralAccepted >= _perUnitGeneralCapacity
                        || _generalAccepted >= _generalKeyCapacity);
                if (budget.Accepted >= _perUnitCapacity
                    || _keys.Count >= _keyCapacity
                    || generalCapacityReached)
                {
                    firstOverflow = !budget.OverflowLogged;
                    budget.OverflowLogged = true;
                    return InfernoMotionJumpAcceptResult.Overflow;
                }
                _keys.Add(key);
                if (usesSevereTransitionReservation)
                {
                    _severeTransitionKeys.Add(key);
                    budget.SevereTransitionAccepted = true;
                }
                else
                {
                    budget.GeneralAccepted++;
                    _generalAccepted++;
                }
                budget.Accepted++;
                return InfernoMotionJumpAcceptResult.Accepted;
            }

            internal void Retire(int unitId, ulong lifecycle)
            {
                if (!_units.TryGetValue(unitId, out UnitBudget budget)
                    || budget.Lifecycle != lifecycle)
                    return;
                _units.Remove(unitId);
                RemoveKeys(unitId, lifecycle);
            }

            internal void RetireUnit(int unitId)
            {
                if (!_units.TryGetValue(unitId, out UnitBudget budget)) return;
                _units.Remove(unitId);
                RemoveKeys(unitId, budget.Lifecycle);
            }

            internal void Reset()
            {
                _units.Clear();
                _keys.Clear();
                _severeTransitionKeys.Clear();
                _generalAccepted = 0;
                _globalOverflowLogged = false;
            }

            private void RemoveKeys(int unitId, ulong lifecycle)
            {
                string prefix = unitId.ToString(CultureInfo.InvariantCulture) + ":"
                    + lifecycle.ToString(CultureInfo.InvariantCulture) + ":";
                _keys.RemoveWhere(key =>
                {
                    if (!key.StartsWith(prefix, StringComparison.Ordinal)) return false;
                    if (!_severeTransitionKeys.Remove(key))
                        _generalAccepted = Math.Max(0, _generalAccepted - 1);
                    return true;
                });
            }

            private static string BuildKey(
                int unitId,
                ulong lifecycle,
                int frame,
                InfernoMotionJumpClassification classification)
                => unitId.ToString(CultureInfo.InvariantCulture) + ":"
                    + lifecycle.ToString(CultureInfo.InvariantCulture) + ":"
                    + frame.ToString(CultureInfo.InvariantCulture) + ":"
                    + ((int)classification).ToString(CultureInfo.InvariantCulture);
        }

        private static float PlanarDistance(Vector3 from, Vector3 to)
        {
            float x = to.x - from.x;
            float z = to.z - from.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private static bool IsFiniteVector(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFiniteQuaternion(Quaternion value)
            => IsFinite(value.x) && IsFinite(value.y)
                && IsFinite(value.z) && IsFinite(value.w);

        private static bool IsFiniteNonNegative(float value)
            => IsFinite(value) && value >= 0f;

        private static string FormatDistance(bool valid, float value)
            => valid && IsFinite(value)
                ? value.ToString("F4", CultureInfo.InvariantCulture)
                : "Unavailable";

        private static string FormatSignedDistance(bool valid, float value)
            => valid && IsFinite(value)
                ? value.ToString("+0.0000;-0.0000;0.0000", CultureInfo.InvariantCulture)
                : "Unavailable";

        private static string ToInfernoMotionJumpLogValue(InfernoMotionJumpClassification classification)
        {
            switch (classification)
            {
                case InfernoMotionJumpClassification.AuthoritativeRootJump: return "authoritative-root-jump";
                case InfernoMotionJumpClassification.VisualProjectionJump: return "visual-projection-jump";
                case InfernoMotionJumpClassification.PresentationAnchorJump: return "presentation-anchor-jump";
                case InfernoMotionJumpClassification.LongFrameCatchUp: return "long-frame-catch-up";
                case InfernoMotionJumpClassification.ClientOnlyReplicationGap: return "client-only-replication-gap";
                default: return "unavailable-ambiguous";
            }
        }

        private static void LogInfernoMotionJump(LogLevel level, string eventName, string data)
        {
            if (_lines >= MaximumNormalLines)
            {
                Increment(ref _dropped);
                return;
            }
            string decorated = Decorate(data);
            if (Encoding.UTF8.GetByteCount(FormatInfernoMotionFullLine(eventName, decorated))
                >= MaximumFullLineUtf8BytesExclusive)
            {
                Increment(ref _dropped);
                if (!_infernoMotionPreflightFailureLogged)
                {
                    _infernoMotionPreflightFailureLogged = true;
                    RuntimeLogger.Log(
                        LogLevel.Error,
                        "Network",
                        nameof(UnitAttackShadowObserver),
                        "[UAS-DIAG][INFERNO-MOTION-JUMP] preflight-failure",
                        Decorate("reason=utf8-line-limit"));
                }
                return;
            }
            _lines++;
            RuntimeLogger.Log(
                level,
                "Network",
                nameof(UnitAttackShadowObserver),
                $"[UAS-DIAG][INFERNO-MOTION-JUMP] {eventName}",
                decorated);
        }

        private static string FormatInfernoMotionFullLine(string eventName, string decorated)
            => "[00:00:00.000] [ERROR] [Network/UnitAttackShadowObserver] "
                + "[UAS-DIAG][INFERNO-MOTION-JUMP] " + eventName + " | " + decorated;

        private static bool TryNormalizePlanar(Vector3 value, out Vector3 normalized)
        {
            normalized = default;
            if (!IsFinite(value.x) || !IsFinite(value.z)) return false;
            double lengthSquared = (double)value.x * value.x + (double)value.z * value.z;
            if (lengthSquared <= 0.00000001d) return false;
            double inverseLength = 1d / Math.Sqrt(lengthSquared);
            normalized = new Vector3(
                (float)(value.x * inverseLength),
                0f,
                (float)(value.z * inverseLength));
            return true;
        }

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string FormatVector(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z)
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "({0:F4}|{1:F4}|{2:F4})",
                    value.x,
                    value.y,
                    value.z)
                : "Invalid";

        private static string FormatYaw(bool valid, double value)
            => valid ? value.ToString("F3", CultureInfo.InvariantCulture) : "Unavailable";

        internal static void RecordIntent(
            int unitId,
            UnitType unitType,
            EntityRef target,
            UnitActionPoseSample sample,
            UnitAttackShadowCoordinator.IntentObservation observation,
            double serverTime)
        {
            if (!_active || !_isServer) return;
            Increment(ref _intentSamples);
            CoveredUnitTypes.Add((int)unitType);
            if (observation.CommittedNow) Increment(ref _commits);

            if (observation.ExpectedDeferred)
                Increment(ref _expectedDeferred);

            bool failure = !observation.ExpectedDeferred
                && (observation.Status == UnitActionReducerStatus.ScopeMismatch
                || observation.Status == UnitActionReducerStatus.InvalidInput
                || observation.Status == UnitActionReducerStatus.Exhausted);
            if (!failure && !observation.CommittedNow) return;

            // 정상 commit을 매 회차마다 남기면 긴 경기에서 실제 오류 증거가 bounded 상한에
            // 밀린다. 유닛 타입별 첫 정상 commit만 상세 표본으로 남기고 나머지는 별도 계수한다.
            if (observation.CommittedNow
                && !OnceKeys.Add($"shadow-commit-{(int)unitType}"))
            {
                Increment(ref _suppressedNormal);
                return;
            }

            UnitActionSnapshot snapshot = observation.Snapshot;
            string eventName = observation.CommittedNow ? "shadow-commit" : "shadow-intent-rejected";
            string data =
                $"unitId={unitId}, unitType={unitType}, targetKind={target.Kind}, targetId={target.Id}, " +
                $"sequenceId={(snapshot != null ? snapshot.SequenceId.Value : 0UL)}, " +
                $"revision={(snapshot != null ? snapshot.Revision : 0UL)}, " +
                $"phase={(snapshot != null ? snapshot.Phase.ToString() : "Unavailable")}, " +
                $"serverTime={serverTime:F6}, stationary={observation.IsStationary}, " +
                $"yawErrorDegrees={sample.FacingToAimYawDegrees:F3}, status={observation.Status}, " +
                $"reason={Normalize(observation.Reason)}";
            if (failure)
                LogCausalFailure(eventName, data);
            else
                Log(LogLevel.Info, eventName, data);
        }

        internal static void RecordUnsupportedProfile(
            int unitId,
            UnitType unitType,
            UnitAttackShadowProfile profile,
            string reason,
            EntityRef target,
            bool poseCaptured,
            UnitActionPoseSample sample)
        {
            if (!_active || !_isServer) return;
            CoveredUnitTypes.Add((int)unitType);
            // 유닛별로 첫 관측 한 번만 남겨 미완료 유닛이 전투 틱마다 로그를 만들지 않게 한다.
            string coverageKey = ((int)unitType).ToString(CultureInfo.InvariantCulture);
            if (!OnceKeys.Add("profile-" + coverageKey)) return;
            Increment(ref _intentSamples);
            Log(
                LogLevel.Info,
                "profile-unresolved",
                $"unitId={unitId}, unitType={unitType}, support={profile.Support}, " +
                $"delivery={profile.Delivery}, targetMode={profile.TargetMode}, " +
                $"targetKind={target.Kind}, targetId={target.Id}, poseCaptured={poseCaptured}, " +
                $"simulationFacingX={(poseCaptured ? sample.SimulationFacing.X.ToString("F6", CultureInfo.InvariantCulture) : "Unavailable")}, " +
                $"simulationFacingZ={(poseCaptured ? sample.SimulationFacing.Z.ToString("F6", CultureInfo.InvariantCulture) : "Unavailable")}, " +
                $"yawErrorDegrees={(poseCaptured ? sample.FacingToAimYawDegrees.ToString("F3", CultureInfo.InvariantCulture) : "Unavailable")}, " +
                $"reason={Normalize(reason)}");
        }

        internal static void RecordLegacySchedule(
            int unitId,
            UnitType unitType,
            LegacyAttackToken token,
            EntityRef target,
            UnitActionSnapshot snapshot,
            double legacyScheduledAt)
        {
            if (!_active || !_isServer || !token.IsValid) return;
            Increment(ref _legacySchedules);
            bool beforeAligned = snapshot == null || !snapshot.SequenceId.IsValid;
            if (beforeAligned) Increment(ref _legacyBeforeAligned);
            // 정상 예약을 매 공격마다 출력하면 한 경기 안에서 bounded 로그가 가득 차 terminal이
            // dropped FAIL이 된다. 정상은 유닛 타입별 첫 1건만 표본으로 남기고, 정렬 전 예약은
            // 모든 발생을 카운트하되 상세 줄도 타입별 첫 1건만 보존한다.
            string sampleKey = $"legacy-schedule-{(int)unitType}-{beforeAligned}";
            if (OnceKeys.Add(sampleKey))
            {
                string data =
                    $"unitId={unitId}, unitType={unitType}, legacyAttackToken={token.Value}, " +
                    $"attackerInstanceId={token.AttackerInstanceId.Value}, " +
                    $"shadowSequenceId={(snapshot != null ? snapshot.SequenceId.Value : 0UL)}, " +
                    $"shadowRevision={(snapshot != null ? snapshot.Revision : 0UL)}, " +
                    $"shadowPhase={(snapshot != null ? snapshot.Phase.ToString() : "Unavailable")}, " +
                    $"targetKind={target.Kind}, targetId={target.Id}, legacyScheduledAt={legacyScheduledAt:F6}, " +
                    $"reason={(beforeAligned ? "legacy-started-before-shadow-aligned" : "shadow-sequence-correlated")}";
                if (beforeAligned)
                    LogCausalFailure("legacy-schedule", data);
                else
                    Log(LogLevel.Info, "legacy-schedule", data);
            }
        }

        private static void ObservePresentationClassification(
            PresentationShadowClassification classification)
        {
            if (!_active) return;
            switch (classification.Status)
            {
                case PresentationShadowResultStatus.Scheduled:
                    if (!classification.HasLegacyObservation)
                        Increment(ref _presentationScheduled);
                    break;
                case PresentationShadowResultStatus.CatchUp: Increment(ref _presentationCatchUps); break;
                case PresentationShadowResultStatus.Matched:
                    Increment(ref _presentationMatched);
                    if (classification.HasLegacyObservation)
                    {
                        switch (classification.ObservationKind)
                        {
                            case LegacyPresentationObservationKind.Marker:
                                Increment(ref _presentationMarkerMatched);
                                break;
                            case LegacyPresentationObservationKind.TracerImpact:
                                Increment(ref _presentationTracerMatched);
                                break;
                            case LegacyPresentationObservationKind.Enqueued:
                                Increment(ref _presentationEnqueueMatched);
                                break;
                            default:
                                Increment(ref _presentationEmitMatched);
                                break;
                        }
                    }
                    bool isLaunchMarker = classification.HasLegacyObservation
                        && classification.ObservationKind == LegacyPresentationObservationKind.Marker;
                    PresentationTimingEvidenceKind timingKind = classification.HasLegacyObservation
                        ? ClassifyTimingEvidenceForValidation(classification.ObservationKind)
                        : PresentationTimingEvidenceKind.NotTimingEvidence;
                    bool isNormalImpactTimingObservation =
                        timingKind == PresentationTimingEvidenceKind.NormalImpact;
                    bool isRecoveryTimingObservation =
                        timingKind == PresentationTimingEvidenceKind.TimeoutRecovery
                        || timingKind == PresentationTimingEvidenceKind.TargetDeathRecovery
                        || timingKind == PresentationTimingEvidenceKind.AttackerStopRecovery;
                    if (isLaunchMarker
                        && IsPresentationAimMismatchForValidation(
                            classification.AimDeltaDegrees))
                    {
                        Increment(ref _presentationDirectionMismatches);
                        // 최신 NetworkVariable을 여기서 다시 읽지 않는다. marker 뒤에 다음 회차가
                        // 도착했을 수 있으므로 UnitView가 marker 순간에 값으로 복사한 snapshot만 쓴다.
                        AttackPresentationReplicatedSnapshot markerSnapshot =
                            classification.ReplicatedSnapshot;
                        bool hasReplicatedState = !_isServer && markerSnapshot.IsValid;
                        PresentationDirectionRevisionClassification directionReason =
                            ClassifyDirectionRevisionForValidation(
                                classification.ActionRevision,
                                classification.AttackScope.AttackerInstanceId.Value,
                                classification.AttackScope.SequenceId.Value,
                                hasReplicatedState,
                                hasReplicatedState
                                    ? markerSnapshot.AttackerInstanceId
                                    : 0UL,
                                hasReplicatedState
                                    ? markerSnapshot.SequenceId
                                    : 0UL,
                                hasReplicatedState
                                    ? markerSnapshot.Revision
                                    : 0UL,
                                classification.AimDeltaDegrees);
                        switch (directionReason)
                        {
                            case PresentationDirectionRevisionClassification.SameRevisionVisualMismatch:
                                Increment(ref _presentationDirectionSameRevisionMismatches);
                                break;
                            case PresentationDirectionRevisionClassification.RevisionLag:
                            case PresentationDirectionRevisionClassification.ReplicatedStateAhead:
                                Increment(ref _presentationDirectionRevisionLags);
                                break;
                            case PresentationDirectionRevisionClassification.ScopeMismatch:
                                Increment(ref _presentationDirectionScopeMismatches);
                                break;
                            default:
                                Increment(ref _presentationDirectionEvidenceInvalid);
                                break;
                        }
                        RecordPresentationFailureDetail(
                            classification,
                            "direction-" + directionReason.ToString().ToLowerInvariant(),
                            markerSnapshot);
                    }
                    if (!double.IsNaN(classification.AimDeltaDegrees))
                        _presentationMaximumAimDelta = System.Math.Max(
                            _presentationMaximumAimDelta,
                            classification.AimDeltaDegrees);
                    if (isNormalImpactTimingObservation
                        && !double.IsNaN(classification.TimingDeltaSeconds)
                        && System.Math.Abs(classification.TimingDeltaSeconds)
                            > UnitAttackResultPresentationShadowScheduler.CatchUpSeconds)
                    {
                        Increment(ref _presentationTimingMismatches);
                        RecordPresentationFailureDetail(classification, "timing-mismatch");
                    }
                    if (isNormalImpactTimingObservation
                        && !double.IsNaN(classification.TimingDeltaSeconds))
                        _presentationMaximumTimingDelta = System.Math.Max(
                            _presentationMaximumTimingDelta,
                            System.Math.Abs(classification.TimingDeltaSeconds));
                    if (isRecoveryTimingObservation)
                    {
                        Increment(ref _presentationRecoveryEmits);
                        switch (classification.ObservationKind)
                        {
                            case LegacyPresentationObservationKind.EmittedTimeout:
                                Increment(ref _presentationTimeoutRecoveries);
                                break;
                            case LegacyPresentationObservationKind.EmittedTargetDeath:
                                Increment(ref _presentationTargetDeathRecoveries);
                                break;
                            case LegacyPresentationObservationKind.EmittedAttackerStop:
                                Increment(ref _presentationAttackerStopRecoveries);
                                break;
                        }
                        if (!double.IsNaN(classification.TimingDeltaSeconds))
                        {
                            double recoveryDelta = System.Math.Abs(
                                classification.TimingDeltaSeconds);
                            _presentationMaximumRecoveryDelta = System.Math.Max(
                                _presentationMaximumRecoveryDelta,
                                recoveryDelta);
                            if (recoveryDelta
                                > UnitAttackResultPresentationShadowScheduler.CatchUpSeconds)
                            {
                                Increment(ref _presentationRecoveryTimingMismatches);
                                RecordPresentationFailureDetail(
                                    classification,
                                    "recovery-timing-mismatch");
                            }
                        }
                    }
                    break;
                case PresentationShadowResultStatus.Unmatched:
                    Increment(ref _presentationUnmatched);
                    IncrementUnmatchedStage(classification.ObservationKind);
                    break;
                case PresentationShadowResultStatus.Ambiguous: Increment(ref _presentationAmbiguous); break;
                case PresentationShadowResultStatus.Duplicate: Increment(ref _presentationDuplicates); break;
                case PresentationShadowResultStatus.Conflict: Increment(ref _presentationConflicts); break;
                case PresentationShadowResultStatus.Expired: Increment(ref _presentationExpired); break;
                case PresentationShadowResultStatus.InstanceRetired: Increment(ref _presentationRetired); break;
                default: Increment(ref _presentationInvalid); break;
            }

            bool failure = classification.Status == PresentationShadowResultStatus.Unmatched
                || classification.Status == PresentationShadowResultStatus.Conflict
                || classification.Status == PresentationShadowResultStatus.Ambiguous
                || classification.Status == PresentationShadowResultStatus.Invalid
                || classification.Status == PresentationShadowResultStatus.InstanceRetired;
            if (failure)
                RecordPresentationFailureDetail(
                    classification,
                    classification.Status.ToString().ToLowerInvariant());
        }

        private static void IncrementUnmatchedStage(LegacyPresentationObservationKind kind)
        {
            switch (kind)
            {
                case LegacyPresentationObservationKind.Marker:
                    Increment(ref _presentationMarkerUnmatched);
                    break;
                case LegacyPresentationObservationKind.TracerImpact:
                    Increment(ref _presentationTracerUnmatched);
                    break;
                case LegacyPresentationObservationKind.Enqueued:
                    Increment(ref _presentationEnqueueUnmatched);
                    break;
                default:
                    Increment(ref _presentationEmitUnmatched);
                    break;
            }
        }

        private static void RecordPresentationFailureDetail(
            PresentationShadowClassification classification,
            string reason,
            AttackPresentationReplicatedSnapshot replicatedSnapshot = default)
        {
            if (_presentationFailureDetails >= MaximumPresentationFailureDetails) return;
            Increment(ref _presentationFailureDetails);
            AttackPresentationScope scope = classification.AttackScope;
            Log(LogLevel.Warn, "presentation-shadow-failure",
                $"reason={reason}, status={classification.Status}, stage={classification.ObservationKind}, " +
                $"attackerUnitId={classification.AttackerUnitId}, attackerInstanceId={scope.AttackerInstanceId.Value}, " +
                $"sequenceId={scope.SequenceId.Value}, hitIndex={scope.HitIndex}, " +
                $"resultActionRevision={classification.ActionRevision}, " +
                $"replicatedInstanceId={(replicatedSnapshot.IsValid ? replicatedSnapshot.AttackerInstanceId : 0UL)}, " +
                $"replicatedSequenceId={(replicatedSnapshot.IsValid ? replicatedSnapshot.SequenceId : 0UL)}, " +
                $"replicatedRevision={(replicatedSnapshot.IsValid ? replicatedSnapshot.Revision : 0UL)}, " +
                $"victimKind={classification.VictimKind}, victimId={classification.VictimId}, " +
                $"delivery={classification.Delivery}, candidates={classification.CandidateCount}, " +
                $"timingDelta={classification.TimingDeltaSeconds:F6}, aimDelta={classification.AimDeltaDegrees:F3}");
        }

        public static PresentationDirectionRevisionClassification
            ClassifyDirectionRevisionForValidation(
                ulong resultActionRevision,
                ulong resultInstanceId,
                ulong resultSequenceId,
                bool hasReplicatedState,
                ulong replicatedInstanceId,
                ulong replicatedSequenceId,
                ulong replicatedRevision,
                double aimDeltaDegrees)
        {
            if (resultActionRevision == 0UL
                || resultInstanceId == 0UL
                || resultSequenceId == 0UL
                || !hasReplicatedState
                || replicatedRevision == 0UL
                || double.IsNaN(aimDeltaDegrees)
                || double.IsInfinity(aimDeltaDegrees)
                || aimDeltaDegrees < 0d)
            {
                return PresentationDirectionRevisionClassification
                    .MissingOrInvalidEvidence;
            }

            if (resultInstanceId != replicatedInstanceId
                || resultSequenceId != replicatedSequenceId)
                return PresentationDirectionRevisionClassification.ScopeMismatch;
            if (replicatedRevision < resultActionRevision)
                return PresentationDirectionRevisionClassification.RevisionLag;
            if (replicatedRevision > resultActionRevision)
                return PresentationDirectionRevisionClassification.ReplicatedStateAhead;
            return IsPresentationAimMismatchForValidation(aimDeltaDegrees)
                ? PresentationDirectionRevisionClassification.SameRevisionVisualMismatch
                : PresentationDirectionRevisionClassification.SameRevisionMatch;
        }

        /// <summary>
        /// 정상 marker/tracer 시간과 복구 방출 시간을 같은 카운터로 합치지 않기 위한
        /// Editor 회귀 seam이다. tolerance를 바꾸지 않고 관측 의미만 분리한다.
        /// </summary>
        public static PresentationTimingEvidenceKind
            ClassifyTimingEvidenceForValidation(
                LegacyPresentationObservationKind kind)
        {
            switch (kind)
            {
                case LegacyPresentationObservationKind.TracerImpact:
                case LegacyPresentationObservationKind.EmittedMarker:
                case LegacyPresentationObservationKind.EmittedImmediate:
                    return PresentationTimingEvidenceKind.NormalImpact;
                case LegacyPresentationObservationKind.EmittedTimeout:
                    return PresentationTimingEvidenceKind.TimeoutRecovery;
                case LegacyPresentationObservationKind.EmittedTargetDeath:
                    return PresentationTimingEvidenceKind.TargetDeathRecovery;
                case LegacyPresentationObservationKind.EmittedAttackerStop:
                    return PresentationTimingEvidenceKind.AttackerStopRecovery;
                default:
                    return PresentationTimingEvidenceKind.NotTimingEvidence;
            }
        }

        /// <summary>
        /// production gate가 Ready였지만 동일 경계의 Shadow Commit/예약 결속이 실패한 경우다.
        /// Legacy gameplay는 계속 진행하고 observer 판정만 FAIL로 남긴다.
        /// </summary>
        internal static void RecordLegacyCorrelationFailure(
            int unitId,
            UnitType unitType,
            EntityRef target,
            UnitAttackShadowCoordinator.IntentObservation observation,
            double serverTime)
        {
            if (!_active || !_isServer) return;
            Increment(ref _correlationFailures);
            UnitActionSnapshot snapshot = observation.Snapshot;
            LogCausalFailure(
                "legacy-correlation-failed",
                $"unitId={unitId}, unitType={unitType}, targetKind={target.Kind}, targetId={target.Id}, " +
                $"sequenceId={(snapshot != null ? snapshot.SequenceId.Value : 0UL)}, " +
                $"phase={(snapshot != null ? snapshot.Phase.ToString() : "Unavailable")}, " +
                $"status={observation.Status}, serverTime={serverTime:F6}, " +
                $"reason={Normalize(observation.Reason)}");
        }

        /// <summary>
        /// 실제 Legacy writer 바로 앞의 정지·5도 게이트 결과다. Shadow reducer의 결과가 아니라
        /// production 호출 경계 자체를 집계하므로 "진단은 정렬됐지만 실제 예약은 먼저 시작"하는
        /// 회귀를 별도로 발견할 수 있다.
        /// </summary>
        internal static void RecordProductionStartGate(
            int unitId,
            UnitType unitType,
            EntityRef target,
            UnitAttackStartGateResult result,
            UnitActionPoseSample sample)
        {
            if (!_active || !_isServer) return;
            Increment(ref _productionGateSamples);
            CoveredUnitTypes.Add((int)unitType);
            if (result == UnitAttackStartGateResult.Ready)
                Increment(ref _productionGateReady);
            else if (result == UnitAttackStartGateResult.InvalidPose
                || result == UnitAttackStartGateResult.InvalidTarget)
                Increment(ref _productionGateInvalid);
            else
                Increment(ref _productionGateDeferred);

            string sampleKey = $"production-gate-{(int)unitType}-{(int)result}";
            if (!OnceKeys.Add(sampleKey)) return;
            string data =
                $"unitId={unitId}, unitType={unitType}, targetKind={target.Kind}, targetId={target.Id}, " +
                $"result={result}, poseValid={sample.IsValid}, " +
                $"yawErrorDegrees={(sample.IsValid ? sample.FacingToAimYawDegrees.ToString("F3", CultureInfo.InvariantCulture) : "Unavailable")}";
            if (result == UnitAttackStartGateResult.InvalidPose
                || result == UnitAttackStartGateResult.InvalidTarget)
                LogCausalFailure("production-start-gate", data);
            else
                Log(LogLevel.Info, "production-start-gate", data);
        }

        internal static void RecordDispatch(
            int unitId,
            int displayTargetBeforeApply,
            int displayTargetAfterApply,
            EntityRef legacyTarget,
            UnitActionPoseSample sample,
            UnitAttackShadowCoordinator.DispatchObservation observation,
            AttackDamageApplyStatus legacyResult,
            double serverTime)
        {
            if (!_active || !_isServer || !observation.LegacyToken.IsValid) return;
            Increment(ref _impactSamples);
            int shadowTargetId = observation.Snapshot != null
                && observation.Snapshot.TargetBinding.IsValid
                    ? observation.Snapshot.TargetBinding.Target.Id
                    : -1;
            bool mismatch = ClassifyDispatchMismatchForValidation(
                displayTargetBeforeApply,
                legacyTarget.Id,
                shadowTargetId,
                observation.ScheduledShadowSequence.Value,
                observation.Snapshot != null ? observation.Snapshot.SequenceId.Value : 0UL,
                observation.EvaluateStatus,
                observation.Authorization.Outcome,
                legacyResult);
            if (mismatch) Increment(ref _targetMismatches);

            // 정상 Impact는 terminal 카운터로 충분하다. 불일치만 bounded 상세 증거로 남겨
            // 장기 경기에서도 필수 terminal이 로그 포화 때문에 FAIL하지 않게 한다.
            if (mismatch)
            {
                LogCausalFailure(
                    "legacy-impact-dispatch",
                    $"unitId={unitId}, legacyAttackToken={observation.LegacyToken.Value}, " +
                    $"attackerInstanceId={observation.LegacyToken.AttackerInstanceId.Value}, " +
                    $"scheduledShadowSequenceId={observation.ScheduledShadowSequence.Value}, " +
                    $"currentShadowSequenceId={(observation.Snapshot != null ? observation.Snapshot.SequenceId.Value : 0UL)}, " +
                    $"legacyTargetKind={legacyTarget.Kind}, legacyTargetId={legacyTarget.Id}, " +
                    $"displayTargetBeforeApply={displayTargetBeforeApply}, " +
                    $"displayTargetAfterApply={displayTargetAfterApply}, shadowTargetId={shadowTargetId}, " +
                    $"serverTime={serverTime:F6}, yawErrorDegrees={sample.FacingToAimYawDegrees:F3}, " +
                    $"advanceStatus={observation.AdvanceStatus}, evaluateStatus={observation.EvaluateStatus}, " +
                    $"shadowOutcome={observation.Authorization.Outcome}, " +
                    $"shadowMissReason={observation.Authorization.MissReason}, legacyResult={legacyResult}, " +
                    $"reason={Normalize(observation.Reason)}");
            }
        }

        /// <summary>
        /// Runtime RecordDispatch와 Editor 회귀 검증이 함께 사용하는 순수 판정 seam이다.
        /// </summary>
        public static bool ClassifyDispatchMismatchForValidation(
            int displayTargetBeforeApply,
            int legacyTargetId,
            int shadowTargetId,
            ulong scheduledSequenceId,
            ulong currentSequenceId,
            UnitActionReducerStatus evaluateStatus,
            ImpactAuthorizationOutcome shadowOutcome,
            AttackDamageApplyStatus legacyResult)
        {
            // 표시 후보는 예약된 Impact보다 먼저 사라지거나 다음 후보로 바뀔 수 있다.
            // 따라서 displayTargetBeforeApply는 진단 문맥으로만 남기고 예약된
            // Shadow/Legacy target, sequence, reducer 판정과 실제 적용 결과만 비교한다.
            _ = displayTargetBeforeApply;

            if (legacyTargetId < 0 || shadowTargetId < 0 || shadowTargetId != legacyTargetId)
                return true;
            if (scheduledSequenceId == 0UL || currentSequenceId == 0UL
                || scheduledSequenceId != currentSequenceId)
                return true;
            if (evaluateStatus != UnitActionReducerStatus.Accepted)
                return true;

            bool shadowApplied;
            switch (shadowOutcome)
            {
                case ImpactAuthorizationOutcome.AuthorizedHit:
                    shadowApplied = true;
                    break;
                case ImpactAuthorizationOutcome.AuthorizedMiss:
                    shadowApplied = false;
                    break;
                default:
                    return true;
            }

            bool legacyApplied = legacyResult == AttackDamageApplyStatus.Applied;
            return shadowApplied != legacyApplied;
        }

        internal static void ObserveClientReplicatedState(
            int unitId,
            ulong networkObjectId,
            NetworkUnitActionShadowState state)
        {
            if (!_active || _isServer || unitId < 0) return;
            if (!ClientClassifiers.TryGetValue(unitId, out UnitAttackShadowReplicationClassifier classifier))
            {
                classifier = new UnitAttackShadowReplicationClassifier();
                ClientClassifiers.Add(unitId, classifier);
            }

            bool hadPrevious = classifier.TryGetLastAcceptedState(
                out NetworkUnitActionShadowState previous);
            UnitAttackShadowReplicationStatus status = classifier.Classify(networkObjectId, state);
            if (status == UnitAttackShadowReplicationStatus.Accepted)
            {
                Increment(ref _clientAccepted);
                return;
            }
            if (status == UnitAttackShadowReplicationStatus.Duplicate) return;

            Increment(ref _clientRejected);
            LogCausalFailure(
                "client-shadow-state-rejected",
                $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                $"attackerInstanceId={state.AttackerInstanceId}, sequenceId={state.SequenceId}, " +
                $"revision={state.Revision}, phase={state.Phase}, status={status}, " +
                $"previousSequenceId={(hadPrevious ? previous.SequenceId : 0UL)}, " +
                $"previousRevision={(hadPrevious ? previous.Revision : 0UL)}, " +
                $"previousPhase={(hadPrevious ? previous.Phase : (byte)0)}");
        }

        internal static void RecordImpactResult(
            int unitId,
            AttackDamageObservation legacy,
            UnitAttackShadowCoordinator.ResultObservation observation,
            double serverTime)
        {
            if (!_active || !_isServer) return;
            Increment(ref _serverResults);
            AttackImpactResult result = observation.Result;
            bool mismatch = ClassifyImpactResultMismatchForValidation(
                legacy, observation.CompletionStatus, result);
            if (!mismatch) return;

            Increment(ref _resultCompletionFailures);
            LogCausalFailure(
                "impact-result-mismatch",
                $"unitId={unitId}, serverTime={serverTime:F6}, legacyStatus={legacy.Status}, " +
                $"legacyApplied={legacy.AppliedAmount}, legacyHp={legacy.ResultingHp}, " +
                $"completionStatus={observation.CompletionStatus}, delivery={observation.Delivery}, " +
                $"sequenceId={(result != null ? result.Key.SequenceId.Value : 0UL)}, " +
                $"hitIndex={(result != null ? result.Key.HitIndex : -1)}, " +
                $"outcome={(result != null ? result.Outcome.ToString() : "Unavailable")}, " +
                $"reason={Normalize(observation.Reason)}");
        }

        /// <summary>
        /// 정상적인 서버 권위 Miss와 coordinator 실패를 구분하는 순수 판정 seam이다.
        /// 공격자/타겟 부재와 전투 조건 실패는 모두 승인된 Miss이며 Cancelled가 아니다.
        /// </summary>
        public static bool ClassifyImpactResultMismatchForValidation(
            AttackDamageObservation legacy,
            UnitActionReducerStatus completionStatus,
            AttackImpactResult result)
        {
            if (result == null
                || completionStatus != UnitActionReducerStatus.Accepted
                || !legacy.IsValid
                || result.AppliedAmount != legacy.AppliedAmount
                || result.ResultingHp != legacy.ResultingHp)
                return true;

            if (legacy.Status == AttackDamageApplyStatus.Applied)
            {
                return result.Outcome != (legacy.AppliedAmount > 0
                    ? AttackImpactOutcome.HitApplied
                    : AttackImpactOutcome.StatusEffectApplied);
            }

            if (legacy.Status == AttackDamageApplyStatus.AttackerUnavailable
                || legacy.Status == AttackDamageApplyStatus.TargetUnavailable
                || legacy.Status == AttackDamageApplyStatus.CombatConditionFailed)
                return result.Outcome != AttackImpactOutcome.Miss;

            return true;
        }

        internal static void ObserveClientImpactResult(
            int unitId,
            ulong networkObjectId,
            NetworkAttackImpactShadowResult result)
        {
            if (!_active || _isServer || unitId < 0) return;
            if (!ClientImpactClassifiers.TryGetValue(
                    unitId, out UnitAttackShadowImpactReplicationClassifier classifier))
            {
                classifier = new UnitAttackShadowImpactReplicationClassifier();
                ClientImpactClassifiers.Add(unitId, classifier);
            }

            UnitAttackShadowImpactReplicationStatus status =
                classifier.Classify(networkObjectId, result);
            if (status == UnitAttackShadowImpactReplicationStatus.Accepted)
            {
                Increment(ref _clientResultAccepted);
                return;
            }
            if (status == UnitAttackShadowImpactReplicationStatus.Duplicate) return;

            Increment(ref _clientResultRejected);
            LogCausalFailure(
                "client-impact-result-rejected",
                $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                $"attackerInstanceId={result.AttackerInstanceId}, sequenceId={result.SequenceId}, " +
                $"hitIndex={result.HitIndex}, victimKind={result.VictimKind}, victimId={result.VictimId}, " +
                $"actionRevision={result.ActionRevision}, status={status}");
        }

        internal static void RetireClient(int unitId)
        {
            if (ClientClassifiers.TryGetValue(unitId, out UnitAttackShadowReplicationClassifier classifier))
                classifier.Retire();
            ClientClassifiers.Remove(unitId);
            if (ClientImpactClassifiers.TryGetValue(
                    unitId, out UnitAttackShadowImpactReplicationClassifier impactClassifier))
                impactClassifier.Retire();
            ClientImpactClassifiers.Remove(unitId);
            RetireInfernoMotionJumpUnit(unitId);
        }

        internal static void ObservePresentationSpatialMismatch(
            AttackResultKey key,
            Vector3 displayPosition,
            Vector3 victimPresentationPosition,
            bool viewFlipped)
        {
            if (!_active) return;
            if (_presentationFailureDetails++ >= MaximumPresentationFailureDetails)
                return;
            double deltaX = displayPosition.x - victimPresentationPosition.x;
            double deltaZ = displayPosition.z - victimPresentationPosition.z;
            double delta = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            LogCausalFailure(
                "presentation-spatial-mismatch",
                $"attackerInstanceId={key.AttackerInstanceId}, sequenceId={key.SequenceId}, " +
                $"hitIndex={key.HitIndex}, victimKind={key.VictimKind}, victimId={key.VictimId}, " +
                $"display={displayPosition.x:F3}/{displayPosition.z:F3}, " +
                $"victim={victimPresentationPosition.x:F3}/{victimPresentationPosition.z:F3}, " +
                $"delta={delta:F3}, flipped={viewFlipped}");
        }

        internal static void EndSession(string reason)
        {
            if (!_active) return;
            if (!_isServer)
                Log(LogLevel.Info, "expiry-diagnostic-summary",
                    $"role=client, recorded={_expiryArrivalRecorded}, overflow={_expiryArrivalOverflow}, " +
                    $"pending={ExpiryArrivals.Count}, frameWindow={ExpiryFrameWindow}, maxDetails=16");
            string resultVerdict = DetermineResultVerdict();
            string presentationVerdict = DeterminePresentationVerdict();
            string overallVerdict = DetermineLocalVerdict();
            string data = BuildTerminalData(reason, overallVerdict);
            string resultData = BuildResultTerminalData(resultVerdict);
            string presentationData = BuildPresentationTerminalData(presentationVerdict);
            string recoveryData = BuildRecoveryTerminalData(presentationVerdict);
            string spatialVerdict = UnitAttackResultPresentationShadowBridge
                    .PresentationSpatialMismatches > 0
                ? "FAIL"
                : UnitAttackResultPresentationShadowBridge.PresentationSpatialSamples > 0
                    ? "EVIDENCE"
                    : "INCONCLUSIVE";
            string spatialData = BuildSpatialTerminalData(spatialVerdict);
            string infernoVfxData = BuildInfernoVfxTerminalData();
            string streamSpiritTimelineData = BuildStreamSpiritTimelineTerminalData();
            string foxMagicianTimelineData = BuildFoxMagicianTimelineTerminalData();
            if (!IsFullLineUtf8Safe("END", Decorate(data))
                || !IsFullLineUtf8Safe("result-END", Decorate(resultData))
                || !IsFullLineUtf8Safe("presentation-END", Decorate(presentationData))
                || !IsFullLineUtf8Safe("presentation-recovery-END", Decorate(recoveryData))
                || !IsFullLineUtf8Safe("presentation-spatial-END", Decorate(spatialData))
                || !IsFullLineUtf8Safe("inferno-vfx-END", Decorate(infernoVfxData))
                || !IsFullLineUtf8Safe("streamspirit-timeline-END", Decorate(streamSpiritTimelineData))
                || !IsFullLineUtf8Safe("foxmagician-timeline-END", Decorate(foxMagicianTimelineData)))
            {
                Increment(ref _terminalPreflightFailures);
                data = "reason=terminal-preflight-failed, verdict=FAIL";
                resultData = "reason=terminal-preflight-failed, verdict=FAIL";
                presentationData = "reason=terminal-preflight-failed, verdict=FAIL";
                recoveryData = "reason=terminal-preflight-failed, verdict=FAIL";
                spatialData = "reason=terminal-preflight-failed, verdict=FAIL";
                infernoVfxData = "reason=terminal-preflight-failed, verdict=FAIL";
                streamSpiritTimelineData = "reason=terminal-preflight-failed, verdict=FAIL";
                foxMagicianTimelineData = "reason=terminal-preflight-failed, verdict=FAIL";
                resultVerdict = "FAIL";
                presentationVerdict = "FAIL";
                overallVerdict = "FAIL";
                spatialVerdict = "FAIL";
            }
            bool errorEmitted = false;
            var coordinator = UnitAttackResultPresentationShadowBridge.Coordinator;
            string coordinatorVerdict = coordinator.FailureCount > 0 ? "FAIL"
                : coordinator.ResultCount == 0 ? "INCONCLUSIVE" : "EVIDENCE";
            LogTerminal(ConsumeAxisTerminalLevel(coordinatorVerdict, ref errorEmitted),
                "coordinator-END",
                $"role={(_isServer ? "host" : "client")}, coordinatorSchema=c3-complete-bundle-presentation-v3, " +
                $"actualEmitter={(UnitAttackResultPresentationShadowBridge.WasAuthoritativeEmitter ? "ResultPresentation" : "Legacy")}, schedules={coordinator.ScheduleCount}, results={coordinator.ResultCount}, " +
                $"ready={coordinator.ReadyCount}, pending={coordinator.PendingCount}, failures={coordinator.FailureCount}, " +
                $"duplicates={coordinator.DuplicateCount}, retired={coordinator.RetiredCount}, " +
                $"comparedLegacy={coordinator.ComparedLegacyCount}, maxLegacyTimingDelta={coordinator.MaximumLegacyTimingDeltaSeconds:F6}, " +
                $"bundles={coordinator.BundleCount}, releasedBundles={coordinator.ReleasedBundleCount}, " +
                $"aoeCompleteManifest=true, gameplayWrites=0, presentationWrites={UnitAttackResultPresentationShadowBridge.PresentationEmits}, verdict={coordinatorVerdict}");
            LogTerminal(
                ConsumeAxisTerminalLevel(resultVerdict, ref errorEmitted),
                "result-END",
                resultData);
            LogTerminal(
                ConsumeAxisTerminalLevel(presentationVerdict, ref errorEmitted),
                "presentation-END",
                presentationData);
            LogTerminal(
                ConsumeAxisTerminalLevel(presentationVerdict, ref errorEmitted),
                "presentation-recovery-END",
                recoveryData);
            LogTerminal(
                ConsumeAxisTerminalLevel(spatialVerdict, ref errorEmitted),
                "presentation-spatial-END",
                spatialData);
            LogTerminal(
                LogLevel.Info,
                "inferno-vfx-END",
                infernoVfxData);
            LogTerminal(
                LogLevel.Info,
                "streamspirit-timeline-END",
                streamSpiritTimelineData);
            LogTerminal(
                LogLevel.Info,
                "foxmagician-timeline-END",
                foxMagicianTimelineData);
            LogTerminal(
                ConsumeAxisTerminalLevel(overallVerdict, ref errorEmitted),
                "END",
                data);
            _active = false;
        }

        internal static string ValidateTerminalPreflightForValidation()
        {
            string previousRun = _runId;
            string previousSession = _sessionKey;
            double previousStart = _startedAt;
            _runId = new string('r', 32);
            _sessionKey = new string('s', 64);
            _startedAt = 123456.123456d;
            string data =
                $"reason={new string('x', 64)}, role=client, observerSchema={ProductionSchema}, " +
                "coveredUnitTypes=2147483647/25, intentSamples=2147483647, commits=2147483647, " +
                "legacySchedules=2147483647, legacyBeforeAligned=2147483647, " +
                "expectedDeferred=2147483647, correlationFailures=2147483647, " +
                "productionGateSamples=2147483647, productionGateReady=2147483647, " +
                "productionGateDeferred=2147483647, productionGateInvalid=2147483647, " +
                "impactSamples=2147483647, targetMismatches=2147483647, " +
                "clientAccepted=2147483647, clientRejected=2147483647, " +
                "suppressedNormal=2147483647, dropped=2147483647, " +
                "terminalPreflightFailures=2147483647, manifestFailures=2147483647, " +
                "gameplayWrites=0, verdict=FAIL";
            string decorated = Decorate(data);
            int terminalBytes = Encoding.UTF8.GetByteCount(FormatFullLine("END", decorated));
            bool valid = IsFullLineUtf8Safe("END", decorated);

            string resultData =
                "serverResults=2147483647, resultFailures=2147483647, " +
                "clientResultAccepted=2147483647, clientResultRejected=2147483647, " +
                "gameplayWrites=0, verdict=FAIL";
            string resultDecorated = Decorate(resultData);
            int resultBytes = Encoding.UTF8.GetByteCount(
                FormatFullLine("result-END", resultDecorated));
            bool resultValid = IsFullLineUtf8Safe("result-END", resultDecorated);

            string presentationData =
                "scheduled=2147483647, matched=2147483647, " +
                "markerMatched=2147483647, tracerMatched=2147483647, " +
                "enqueueMatched=2147483647, emitMatched=2147483647, unmatched=2147483647, " +
                "ambiguous=2147483647, unmatchedMarker=2147483647, unmatchedTracer=2147483647, " +
                "unmatchedEnqueue=2147483647, unmatchedEmit=2147483647, " +
                "duplicates=2147483647, conflicts=2147483647, " +
                "catchUps=2147483647, expired=2147483647, invalid=2147483647, " +
                "retired=2147483647, directionMismatch=2147483647, timingMismatch=2147483647, " +
                "directionSameRevision=2147483647, directionRevisionLag=2147483647, " +
                "directionScopeMismatch=2147483647, directionEvidenceInvalid=2147483647, " +
                "maxAimDelta=180.000, maxTimingDelta=2147483647.000000, " +
                "gameplayWrites=0, presentationEmits=0, verdict=FAIL";
            string presentationDecorated = Decorate(presentationData);
            int presentationBytes = Encoding.UTF8.GetByteCount(
                FormatFullLine("presentation-END", presentationDecorated));
            bool presentationValid = IsFullLineUtf8Safe(
                "presentation-END", presentationDecorated);

            string recoveryData =
                "recoveryEmits=2147483647, timeout=2147483647, targetDeath=2147483647, " +
                "attackerStop=2147483647, recoveryTimingMismatch=2147483647, " +
                "maxRecoveryDelta=2147483647.000000, timeoutExpected=0, verdict=FAIL";
            string recoveryDecorated = Decorate(recoveryData);
            int recoveryBytes = Encoding.UTF8.GetByteCount(
                FormatFullLine("presentation-recovery-END", recoveryDecorated));
            bool recoveryValid = IsFullLineUtf8Safe(
                "presentation-recovery-END", recoveryDecorated);

            string spatialData =
                "samples=2147483647, mismatches=2147483647, " +
                "maxDelta=2147483647.000, tolerance=1.500, verdict=FAIL";
            string spatialDecorated = Decorate(spatialData);
            int spatialBytes = Encoding.UTF8.GetByteCount(
                FormatFullLine("presentation-spatial-END", spatialDecorated));
            bool spatialValid = IsFullLineUtf8Safe(
                "presentation-spatial-END", spatialDecorated);

            string infernoVfxData =
                "role=client, schema=inferno-attack-vfx-attempt-v2, " +
                "rawEvents=2147483647, started=2147483647, sourceOnlyPreserved=2147483647, gateSuppressed=2147483647, " +
                "playbackFailures=2147483647, overflow=True, gameplayWrites=0, verdict=EVIDENCE";
            string infernoVfxDecorated = Decorate(infernoVfxData);
            int infernoVfxBytes = Encoding.UTF8.GetByteCount(
                FormatFullLine("inferno-vfx-END", infernoVfxDecorated));
            bool infernoVfxValid = IsFullLineUtf8Safe(
                "inferno-vfx-END", infernoVfxDecorated);

            string streamSpiritTimelineData =
                $"role=client, schema={StreamSpiritTimelineSchema}, starts=2147483647, markers=2147483647, " +
                "vfxAttempts=2147483647, vfxStarted=2147483647, vfxFailures=2147483647, " +
                "timerImpacts=2147483647, complete=2147483647, incomplete=2147483647, " +
                "unmatchedMarker=2147483647, unmatchedVfx=2147483647, unmatchedTimer=2147483647, " +
                "duplicates=2147483647, maxMarkerFromStart=2147483647.000000, " +
                "maxTimerFromStart=2147483647.000000, maxMarkerTimerDelta=2147483647.000000, " +
                "expectedImpactOffset=0.500, overflow=True, gameplayWrites=0, verdict=FAIL";
            string streamSpiritTimelineDecorated = Decorate(streamSpiritTimelineData);
            int streamSpiritTimelineBytes = Encoding.UTF8.GetByteCount(
                FormatFullLine("streamspirit-timeline-END", streamSpiritTimelineDecorated));
            bool streamSpiritTimelineValid = IsFullLineUtf8Safe(
                "streamspirit-timeline-END", streamSpiritTimelineDecorated);

            string foxMagicianTimelineData =
                $"role=client, schema={FoxMagicianTimelineSchema}, starts=2147483647, markers=2147483647, " +
                "vfxAttempts=2147483647, vfxStarted=2147483647, vfxFailures=2147483647, " +
                "particlePositive=2147483647, playbackActive=2147483647, maxParticleSystems=2147483647, " +
                "timerImpacts=2147483647, complete=2147483647, retiredComplete=2147483647, incomplete=2147483647, " +
                "unmatchedMarker=2147483647, unmatchedVfxAttempt=2147483647, unmatchedVfxResult=2147483647, " +
                "unmatchedTimer=2147483647, duplicates=2147483647, overflow=2147483647, dropped=2147483647, " +
                "expired=2147483647, cappedDetails=2147483647, " +
                "maxStartMarker=2147483647.000000, maxStartTimer=2147483647.000000, " +
                "maxMarkerTimerDelta=2147483647.000000, expectedImpactOffset=2.250, gameplayWrites=0, verdict=FAIL";
            string foxMagicianTimelineDecorated = Decorate(foxMagicianTimelineData);
            int foxMagicianTimelineBytes = Encoding.UTF8.GetByteCount(
                FormatFullLine("foxmagician-timeline-END", foxMagicianTimelineDecorated));
            bool foxMagicianTimelineValid = IsFullLineUtf8Safe(
                "foxmagician-timeline-END", foxMagicianTimelineDecorated);

            string manifestData =
                "chunk=5/5, entries=RhinoBreaker:Supported:MeleeContact:1|" +
                "EagleArcher:Unresolved:ProjectileImpact:1|RabbitTrickster:Supported:MeleeContact:1|" +
                "MushroomBomber:Unresolved:ProjectileImpact:1|BloomFairy:NotApplicableAttack:Hitscan:0";
            string manifestDecorated = Decorate(manifestData);
            int manifestBytes = Encoding.UTF8.GetByteCount(
                FormatFullLine("coverage-manifest", manifestDecorated));
            bool manifestValid = IsFullLineUtf8Safe("coverage-manifest", manifestDecorated);
            _runId = previousRun;
            _sessionKey = previousSession;
            _startedAt = previousStart;
            int maxBytes = Math.Max(
                Math.Max(Math.Max(terminalBytes, resultBytes), recoveryBytes),
                Math.Max(
                    Math.Max(presentationBytes, manifestBytes),
                    Math.Max(
                        Math.Max(spatialBytes, infernoVfxBytes),
                        Math.Max(streamSpiritTimelineBytes, foxMagicianTimelineBytes))));
            return $"valid={valid && resultValid && presentationValid && recoveryValid && spatialValid && infernoVfxValid && streamSpiritTimelineValid && foxMagicianTimelineValid},manifest={manifestValid},maxBytes={maxBytes},schema={ProductionSchema}";
        }

        private static readonly HashSet<string> OnceKeys = new HashSet<string>();

        private static void LogManifest()
        {
            if (!UnitAttackShadowProfileResolver.ValidateManifest(out string reason))
            {
                Increment(ref _manifestFailures);
                LogCausalTerminalFailure(
                    "coverage-manifest",
                    $"verdict=FAIL, reason={Normalize(reason)}");
                return;
            }

            IReadOnlyList<UnitType> types = UnitAttackShadowProfileResolver.AllManifestTypes;
            for (int start = 0; start < types.Count; start += 5)
            {
                var chunk = new StringBuilder(500);
                int end = Math.Min(types.Count, start + 5);
                for (int index = start; index < end; index++)
                {
                    UnitType type = types[index];
                    UnitAttackShadowProfileResolver.TryResolve(type, out UnitAttackShadowProfile profile);
                    if (chunk.Length > 0) chunk.Append('|');
                    chunk.Append(type).Append(':').Append(profile.Support).Append(':')
                        .Append(profile.Delivery).Append(':').Append(profile.ExpectedImpactCount);
                }
                LogTerminal(
                    LogLevel.Info,
                    "coverage-manifest",
                    $"chunk={start / 5 + 1}/5, entries={chunk}");
            }
        }

        /// <summary>
        /// 이 verdict는 한 실행의 로컬 증거 건전성만 판정한다. EVIDENCE는 25종 누적 완료나
        /// Host/Client 일치를 뜻하지 않으며, 그 최종 판정은 후속 CrossAudit이 수행한다.
        /// unresolved/unsupported profile 행은 명시적 manifest 분류이므로 FAIL 원인이 아니다.
        /// </summary>
        private static string DetermineLocalVerdict()
        {
            int evidenceCount = (_isServer
                    ? _intentSamples > 0 || _legacySchedules > 0 || _impactSamples > 0 || _serverResults > 0
                    : _clientAccepted > 0 || _clientResultAccepted > 0)
                ? 1
                : 0;
            int presentationFailureCount = _presentationAmbiguous > 0
                    || _presentationDuplicates > 0
                    || _presentationConflicts > 0
                    || _presentationInvalid > 0
                    || _presentationRetired > 0
                    || _presentationUnmatched > 0
                    || _presentationExpired > 0
                    || _presentationDirectionSameRevisionMismatches > 0
                    || _presentationTimingMismatches > 0
                    || _presentationTimeoutRecoveries > 0
                    || _presentationRecoveryTimingMismatches > 0
                ? 1
                : 0;
            int presentationUnresolvedCount = _presentationDirectionRevisionLags > 0
                    || _presentationDirectionScopeMismatches > 0
                    || _presentationDirectionEvidenceInvalid > 0
                ? 1
                : 0;
            if (UnitAttackResultPresentationShadowBridge.WasAuthoritativeEmitter)
            {
                presentationFailureCount += AuthoritativePresentationFailureCount();
                presentationUnresolvedCount += UnitAttackResultPresentationShadowBridge.PresentationViewUnavailable;
            }
            return ClassifyLocalVerdictForValidation(
                evidenceCount,
                _targetMismatches,
                _clientRejected,
                _terminalPreflightFailures,
                _dropped,
                _manifestFailures,
                _legacyBeforeAligned,
                _correlationFailures,
                _resultCompletionFailures,
                _clientResultRejected,
                presentationFailureCount,
                presentationUnresolvedCount);
        }

        private static string DetermineResultVerdict()
        {
            return ClassifyResultVerdictForValidation(
                _serverResults > 0 || _clientResultAccepted > 0 ? 1 : 0,
                _resultCompletionFailures,
                _clientResultRejected,
                _terminalPreflightFailures);
        }

        private static string DeterminePresentationVerdict()
        {
            if (UnitAttackResultPresentationShadowBridge.WasAuthoritativeEmitter)
                return ClassifyPresentationVerdictForValidation(
                    UnitAttackResultPresentationShadowBridge.Coordinator.ResultCount,
                    AuthoritativePresentationFailureCount(), _terminalPreflightFailures,
                    UnitAttackResultPresentationShadowBridge.PresentationViewUnavailable);
            int evidenceCount = _presentationScheduled > 0 || _presentationMatched > 0
                    || _presentationCatchUps > 0 || _presentationUnmatched > 0
                    || _presentationAmbiguous > 0 || _presentationDuplicates > 0
                    || _presentationConflicts > 0 || _presentationExpired > 0
                    || _presentationInvalid > 0 || _presentationRetired > 0
                ? 1
                : 0;
            int failureCount = _presentationUnmatched > 0 || _presentationAmbiguous > 0
                    || _presentationDuplicates > 0 || _presentationConflicts > 0
                    || _presentationExpired > 0 || _presentationInvalid > 0
                    || _presentationRetired > 0
                    || _presentationDirectionSameRevisionMismatches > 0
                    || _presentationTimingMismatches > 0
                    || _presentationTimeoutRecoveries > 0
                    || _presentationRecoveryTimingMismatches > 0
                ? 1
                : 0;
            int unresolvedDirectionCount = _presentationDirectionRevisionLags > 0
                    || _presentationDirectionScopeMismatches > 0
                    || _presentationDirectionEvidenceInvalid > 0
                ? 1
                : 0;
            return ClassifyPresentationVerdictForValidation(
                evidenceCount,
                failureCount,
                _terminalPreflightFailures,
                unresolvedDirectionCount);
        }

        private static int AuthoritativePresentationFailureCount()
        {
            var coordinator = UnitAttackResultPresentationShadowBridge.Coordinator;
            int delivered = UnitAttackResultPresentationShadowBridge.PresentationEmits
                + UnitAttackResultPresentationShadowBridge.PresentationViewUnavailable;
            return coordinator.FailureCount + UnitAttackResultPresentationShadowBridge.PresentationDuplicateAttempts
                + UnitAttackResultPresentationShadowBridge.CompletedBundleTransportFailures
                + UnitAttackResultPresentationShadowBridge.PresentationSpatialMismatches
                + (coordinator.ReadyVisualCount != delivered ? 1 : 0);
        }

        /// <summary>
        /// Assembly-CSharp-Editor의 self-validation이 실제 Runtime assembly 경계를 넘어 호출하는
        /// 전용 순수 seam이다. 런타임 상태를 읽거나 변경하지 않고 전달된 카운터만 분류한다.
        /// </summary>
        public static string ClassifyLocalVerdictForValidation(
            int evidenceCount,
            int gameplayMismatchCount,
            int clientRejectionCount,
            int preflightFailureCount,
            int droppedCount,
            int manifestFailureCount,
            int legacyBeforeAlignedCount = 0,
            int correlationFailureCount = 0,
            int resultCompletionFailureCount = 0,
            int clientResultRejectionCount = 0,
            int presentationFailureCount = 0,
            int presentationUnresolvedCount = 0)
        {
            if (gameplayMismatchCount > 0 || clientRejectionCount > 0 || preflightFailureCount > 0
                || droppedCount > 0 || manifestFailureCount > 0 || legacyBeforeAlignedCount > 0
                || correlationFailureCount > 0 || resultCompletionFailureCount > 0
                || clientResultRejectionCount > 0 || presentationFailureCount > 0)
                return "FAIL";
            if (presentationUnresolvedCount > 0)
                return "INCONCLUSIVE";
            return evidenceCount > 0 ? "EVIDENCE" : "INCONCLUSIVE";
        }

        public static string ClassifyResultVerdictForValidation(
            int evidenceCount,
            int resultCompletionFailureCount,
            int clientResultRejectionCount,
            int preflightFailureCount)
        {
            if (resultCompletionFailureCount > 0 || clientResultRejectionCount > 0
                || preflightFailureCount > 0)
                return "FAIL";
            return evidenceCount > 0 ? "EVIDENCE" : "INCONCLUSIVE";
        }

        public static string ClassifyPresentationVerdictForValidation(
            int evidenceCount,
            int presentationFailureCount,
            int preflightFailureCount,
            int unresolvedDirectionCount = 0)
        {
            if (presentationFailureCount > 0 || preflightFailureCount > 0)
                return "FAIL";
            if (unresolvedDirectionCount > 0)
                return "INCONCLUSIVE";
            return evidenceCount > 0 ? "EVIDENCE" : "INCONCLUSIVE";
        }

        /// <summary>
        /// NGO snapshot은 marker RPC와 독립 채널이라 먼저 또는 나중에 도착할 수 있다.
        /// 같은 공격 scope와 같은 action revision에서 실제 화면 방향이 8도를 넘은 경우만
        /// 확정 방향 실패다. lag/ahead/scope mismatch는 증거가 아직 비교 가능하지 않은 상태로
        /// 남겨 EVIDENCE로 위장하지 않되, 실제 시각 오류 수에도 합산하지 않는다.
        /// </summary>
        public static bool IsConfirmedPresentationDirectionFailureForValidation(
            PresentationDirectionRevisionClassification classification)
            => classification == PresentationDirectionRevisionClassification
                .SameRevisionVisualMismatch;

        public static string ClassifySessionContractCompatibilityForValidation(
            string hostObserverSchema,
            string clientObserverSchema,
            CombatPipelineMode hostPipelineMode,
            CombatPipelineMode clientPipelineMode,
            int hostCombatSchema,
            int clientCombatSchema,
            string hostSharedSessionKey,
            string clientSharedSessionKey)
        {
            if (string.IsNullOrWhiteSpace(hostObserverSchema)
                || string.IsNullOrWhiteSpace(clientObserverSchema)
                || string.IsNullOrWhiteSpace(hostSharedSessionKey)
                || string.IsNullOrWhiteSpace(clientSharedSessionKey))
                return "INCONCLUSIVE(missing-anchor)";
            if (!string.Equals(hostObserverSchema, clientObserverSchema, StringComparison.Ordinal))
                return "INCONCLUSIVE(schema-mismatch)";
            if (hostPipelineMode != clientPipelineMode || hostCombatSchema != clientCombatSchema)
                return "INCONCLUSIVE(contract-mismatch)";
            if (!string.Equals(hostSharedSessionKey, clientSharedSessionKey, StringComparison.Ordinal))
                return "INCONCLUSIVE(session-mismatch)";
            return "COMPATIBLE";
        }

        public static bool IsPresentationAimMismatchForValidation(double aimDeltaDegrees)
            => !double.IsNaN(aimDeltaDegrees)
                && !double.IsInfinity(aimDeltaDegrees)
                && aimDeltaDegrees > PresentationAimToleranceDegrees;

        private static LogLevel GetTerminalLogLevel(string verdict)
        {
            if (verdict == "FAIL") return LogLevel.Error;
            return verdict == "INCONCLUSIVE" ? LogLevel.Warn : LogLevel.Info;
        }

        /// <summary>
        /// result/presentation/overall 세 줄의 payload는 모두 보존하되 한 공격 축에서
        /// ERROR stack은 최대 한 번만 만든다. 뒤의 FAIL terminal은 WARN으로 남아
        /// parser가 verdict를 계속 읽을 수 있고 실패 자체도 숨겨지지 않는다.
        /// </summary>
        private static LogLevel ConsumeAxisTerminalLevel(
            string verdict,
            ref bool errorEmitted)
        {
            if (verdict != "FAIL")
                return GetTerminalLogLevel(verdict);
            return ConsumeFailureLogLevel(true, ref errorEmitted);
        }

        /// <summary>
        /// Self-validation과 production logger가 공유하는 순수 집약 seam이다.
        /// 첫 실패만 ERROR이고 이후 실패 payload는 WARN으로 남는다.
        /// </summary>
        public static string ClassifyAggregatedFailureLevelForValidation(
            bool failure,
            ref bool errorEmitted)
        {
            return ConsumeFailureLogLevel(failure, ref errorEmitted).ToString();
        }

        private static LogLevel ConsumeFailureLogLevel(
            bool failure,
            ref bool errorEmitted)
        {
            if (!failure) return LogLevel.Info;
            if (errorEmitted) return LogLevel.Warn;
            errorEmitted = true;
            return LogLevel.Error;
        }

        private static void LogCausalFailure(string eventName, string data)
        {
            Log(
                ConsumeFailureLogLevel(true, ref _causalErrorEmitted),
                eventName,
                data);
        }

        private static void LogCausalTerminalFailure(string eventName, string data)
        {
            LogTerminal(
                ConsumeFailureLogLevel(true, ref _causalErrorEmitted),
                eventName,
                data);
        }

        private static string BuildTerminalData(string reason, string verdict)
        {
            return
                $"reason={Normalize(reason)}, role={(_isServer ? "host" : "client")}, " +
                $"observerSchema={ProductionSchema}, coveredUnitTypes={CoveredUnitTypes.Count}/25, " +
                $"intentSamples={_intentSamples}, commits={_commits}, legacySchedules={_legacySchedules}, " +
                $"legacyBeforeAligned={_legacyBeforeAligned}, expectedDeferred={_expectedDeferred}, " +
                $"correlationFailures={_correlationFailures}, impactSamples={_impactSamples}, " +
                $"productionGateSamples={_productionGateSamples}, productionGateReady={_productionGateReady}, " +
                $"productionGateDeferred={_productionGateDeferred}, productionGateInvalid={_productionGateInvalid}, " +
                $"targetMismatches={_targetMismatches}, clientAccepted={_clientAccepted}, " +
                $"clientRejected={_clientRejected}, suppressedNormal={_suppressedNormal}, dropped={_dropped}, " +
                $"terminalPreflightFailures={_terminalPreflightFailures}, manifestFailures={_manifestFailures}, " +
                $"gameplayWrites=0, verdict={verdict}";
        }

        private static string BuildResultTerminalData(string verdict)
        {
            return
                $"serverResults={_serverResults}, resultFailures={_resultCompletionFailures}, " +
                $"clientResultAccepted={_clientResultAccepted}, clientResultRejected={_clientResultRejected}, " +
                $"gameplayWrites=0, verdict={verdict}";
        }

        private static string BuildPresentationTerminalData(string verdict)
        {
            if (UnitAttackResultPresentationShadowBridge.WasAuthoritativeEmitter)
            {
                var coordinator = UnitAttackResultPresentationShadowBridge.Coordinator;
                return $"mode=ResultPresentation, coordinatorSchema=c3-complete-bundle-presentation-v3, " +
                    $"ready={coordinator.ReadyCount}, expectedVisual={coordinator.ReadyVisualCount}, " +
                    $"presentationEmits={UnitAttackResultPresentationShadowBridge.PresentationEmits}, " +
                    $"viewUnavailable={UnitAttackResultPresentationShadowBridge.PresentationViewUnavailable}, " +
                    $"unavailableCauses={UnitAttackResultPresentationShadowBridge.PresentationSnapshotInvalid}/" +
                    $"{UnitAttackResultPresentationShadowBridge.PresentationPresenterUnavailable}/" +
                    $"{UnitAttackResultPresentationShadowBridge.PresentationChannelFailures}, " +
                    $"optionalViewSkipped={UnitAttackResultPresentationShadowBridge.PresentationOptionalViewSkipped}, " +
                    $"hitVfx={UnitAttackResultPresentationShadowBridge.PresentationHitVfxConfigured}/" +
                    $"{UnitAttackResultPresentationShadowBridge.PresentationHitVfxEmitted}/" +
                    $"{UnitAttackResultPresentationShadowBridge.PresentationHitVfxSkippedNoAsset}, " +
                    $"duplicateAttempts={UnitAttackResultPresentationShadowBridge.PresentationDuplicateAttempts}, " +
                    $"transportFailures={UnitAttackResultPresentationShadowBridge.CompletedBundleTransportFailures}, " +
                    $"failures={AuthoritativePresentationFailureCount()}, legacyMarkerGate=false, " +
                    $"directionSource=authoritative-result, gameplayWrites=0, verdict={verdict}";
            }
            return
                $"scheduled={_presentationScheduled}, matched={_presentationMatched}, " +
                $"markerMatched={_presentationMarkerMatched}, tracerMatched={_presentationTracerMatched}, " +
                $"enqueueMatched={_presentationEnqueueMatched}, emitMatched={_presentationEmitMatched}, " +
                $"unmatched={_presentationUnmatched}, ambiguous={_presentationAmbiguous}, " +
                $"unmatchedMarker={_presentationMarkerUnmatched}, unmatchedTracer={_presentationTracerUnmatched}, " +
                $"unmatchedEnqueue={_presentationEnqueueUnmatched}, unmatchedEmit={_presentationEmitUnmatched}, " +
                $"duplicates={_presentationDuplicates}, conflicts={_presentationConflicts}, " +
                $"catchUps={_presentationCatchUps}, expired={_presentationExpired}, " +
                $"invalid={_presentationInvalid}, retired={_presentationRetired}, " +
                $"directionMismatch={_presentationDirectionMismatches}, " +
                $"directionSameRevision={_presentationDirectionSameRevisionMismatches}, " +
                $"directionRevisionLag={_presentationDirectionRevisionLags}, " +
                $"directionScopeMismatch={_presentationDirectionScopeMismatches}, " +
                $"directionEvidenceInvalid={_presentationDirectionEvidenceInvalid}, " +
                $"timingMismatch={_presentationTimingMismatches}, maxAimDelta={_presentationMaximumAimDelta:F3}, " +
                $"maxTimingDelta={_presentationMaximumTimingDelta:F6}, " +
                $"gameplayWrites=0, presentationEmits=0, verdict={verdict}";
        }

        private static string BuildRecoveryTerminalData(string verdict)
        {
            return
                $"recoveryEmits={_presentationRecoveryEmits}, timeout={_presentationTimeoutRecoveries}, " +
                $"targetDeath={_presentationTargetDeathRecoveries}, attackerStop={_presentationAttackerStopRecoveries}, " +
                $"recoveryTimingMismatch={_presentationRecoveryTimingMismatches}, " +
                $"maxRecoveryDelta={_presentationMaximumRecoveryDelta:F6}, timeoutExpected=0, verdict={verdict}";
        }

        private static string BuildSpatialTerminalData(string verdict)
        {
            return
                $"samples={UnitAttackResultPresentationShadowBridge.PresentationSpatialSamples}, " +
                $"mismatches={UnitAttackResultPresentationShadowBridge.PresentationSpatialMismatches}, " +
                $"maxDelta={UnitAttackResultPresentationShadowBridge.PresentationMaximumSpatialDelta:F3}, " +
                $"tolerance={UnitAttackResultPresentationShadowBridge.MaximumPresentationVictimMotionEnvelope:F3}, " +
                $"gameplayWrites=0, verdict={verdict}";
        }

        private static string BuildInfernoVfxTerminalData()
        {
            return
                $"role={(_isServer ? "host" : "client")}, schema=inferno-attack-vfx-attempt-v2, " +
                $"rawEvents={_infernoAttackVfxRawEvents}, started={_infernoAttackVfxStarted}, " +
                $"sourceOnlyPreserved={_infernoAttackVfxSourceOnlyPreserved}, " +
                $"gateSuppressed={_infernoAttackVfxGateSuppressed}, " +
                $"playbackFailures={_infernoAttackVfxPlaybackFailures}, " +
                $"overflow={_infernoAttackVfxOverflowLogged}, gameplayWrites=0, verdict=EVIDENCE";
        }

        private static string BuildStreamSpiritTimelineTerminalData()
        {
            int complete = _streamSpiritTimelineRetiredComplete;
            int incomplete = 0;
            double maximumMarkerFromStart = _streamSpiritTimelineRetiredMaxMarkerFromStart;
            double maximumTimerFromStart = _streamSpiritTimelineRetiredMaxTimerFromStart;
            double maximumMarkerTimerDelta = _streamSpiritTimelineRetiredMaxMarkerTimerDelta;
            foreach (StreamSpiritTimelineFlow flow in StreamSpiritTimelineFlows.Values)
            {
                bool locallyComplete = flow.MarkerObserved && flow.VfxAttempted
                    && (!_isServer || flow.TimerImpactObserved);
                if (locallyComplete)
                    complete++;
                else
                    incomplete++;

                if (flow.MarkerObserved)
                    maximumMarkerFromStart = Math.Max(
                        maximumMarkerFromStart,
                        Math.Abs(flow.MarkerServerTime - flow.StartServerTime));
                if (flow.TimerImpactObserved)
                    maximumTimerFromStart = Math.Max(
                        maximumTimerFromStart,
                        Math.Abs(flow.TimerImpactServerTime - flow.StartServerTime));
                if (flow.MarkerObserved && flow.TimerImpactObserved)
                    maximumMarkerTimerDelta = Math.Max(
                        maximumMarkerTimerDelta,
                        Math.Abs(flow.MarkerServerTime - flow.TimerImpactServerTime));
            }

            int unmatched = _streamSpiritTimelineUnmatchedMarkers
                + _streamSpiritTimelineUnmatchedVfx
                + _streamSpiritTimelineUnmatchedTimerImpacts;
            string verdict = _streamSpiritTimelineVfxFailures > 0
                    || unmatched > 0
                    || _streamSpiritTimelineDuplicateTransitions > 0
                    || _streamSpiritTimelineOverflowLogged
                ? "FAIL"
                : complete > 0
                    ? "EVIDENCE"
                    : "INCONCLUSIVE";
            return
                $"role={(_isServer ? "host" : "client")}, schema={StreamSpiritTimelineSchema}, " +
                $"starts={_streamSpiritTimelineStarts}, markers={_streamSpiritTimelineMarkers}, " +
                $"vfxAttempts={_streamSpiritTimelineVfxAttempts}, vfxStarted={_streamSpiritTimelineVfxStarted}, " +
                $"vfxFailures={_streamSpiritTimelineVfxFailures}, timerImpacts={_streamSpiritTimelineTimerImpacts}, " +
                $"complete={complete}, retiredComplete={_streamSpiritTimelineRetiredComplete}, incomplete={incomplete}, " +
                $"unmatchedMarker={_streamSpiritTimelineUnmatchedMarkers}, " +
                $"unmatchedVfx={_streamSpiritTimelineUnmatchedVfx}, " +
                $"unmatchedTimer={_streamSpiritTimelineUnmatchedTimerImpacts}, " +
                $"duplicates={_streamSpiritTimelineDuplicateTransitions}, " +
                $"maxMarkerFromStart={maximumMarkerFromStart:F6}, " +
                $"maxTimerFromStart={maximumTimerFromStart:F6}, " +
                $"maxMarkerTimerDelta={maximumMarkerTimerDelta:F6}, " +
                $"expectedImpactOffset={StreamSpiritExpectedImpactSeconds:F3}, " +
                $"overflow={_streamSpiritTimelineOverflowLogged}, gameplayWrites=0, verdict={verdict}";
        }

        private static string BuildFoxMagicianTimelineTerminalData()
        {
            int complete = _foxMagicianTimelineRetiredComplete;
            int incomplete = 0;
            double maximumMarkerFromStart = _foxMagicianTimelineRetiredMaxMarkerFromStart;
            double maximumTimerFromStart = _foxMagicianTimelineRetiredMaxTimerFromStart;
            double maximumMarkerTimerDelta = _foxMagicianTimelineRetiredMaxMarkerTimerDelta;
            foreach (FoxMagicianTimelineFlow flow in FoxMagicianTimelineFlows.Values)
            {
                bool locallyComplete = flow.MarkerObserved && flow.VfxAttempted
                    && flow.VfxResultObserved && (!_isServer || flow.TimerImpactObserved);
                if (locallyComplete) complete++;
                else incomplete++;

                if (flow.MarkerObserved)
                    maximumMarkerFromStart = Math.Max(
                        maximumMarkerFromStart,
                        Math.Abs(flow.MarkerServerTime - flow.StartServerTime));
                if (flow.TimerImpactObserved)
                    maximumTimerFromStart = Math.Max(
                        maximumTimerFromStart,
                        Math.Abs(flow.TimerImpactServerTime - flow.StartServerTime));
                if (flow.MarkerObserved && flow.TimerImpactObserved)
                    maximumMarkerTimerDelta = Math.Max(
                        maximumMarkerTimerDelta,
                        Math.Abs(flow.MarkerServerTime - flow.TimerImpactServerTime));
            }

            int unmatched = _foxMagicianTimelineUnmatchedMarkers
                + _foxMagicianTimelineUnmatchedVfxAttempts
                + _foxMagicianTimelineUnmatchedVfxResults
                + _foxMagicianTimelineUnmatchedTimerImpacts;
            string verdict = _foxMagicianTimelineVfxFailures > 0
                    || unmatched > 0
                    || _foxMagicianTimelineDuplicateTransitions > 0
                    || _foxMagicianTimelineOverflow > 0
                    || _foxMagicianTimelineExpiredIncomplete > 0
                    || _foxMagicianTimelineDropped > 0
                ? "FAIL"
                : complete > 0
                    ? "EVIDENCE"
                    : "INCONCLUSIVE";
            return
                $"role={(_isServer ? "host" : "client")}, schema={FoxMagicianTimelineSchema}, " +
                $"starts={_foxMagicianTimelineStarts}, markers={_foxMagicianTimelineMarkers}, " +
                $"vfxAttempts={_foxMagicianTimelineVfxAttempts}, vfxStarted={_foxMagicianTimelineVfxStarted}, " +
                $"vfxFailures={_foxMagicianTimelineVfxFailures}, " +
                $"particlePositive={_foxMagicianTimelineParticlePositiveResults}, " +
                $"playbackActive={_foxMagicianTimelinePlaybackActiveResults}, " +
                $"maxParticleSystems={_foxMagicianTimelineMaxParticleSystemCount}, " +
                $"timerImpacts={_foxMagicianTimelineTimerImpacts}, " +
                $"complete={complete}, incomplete={incomplete}, " +
                $"unmatchedMarker={_foxMagicianTimelineUnmatchedMarkers}, " +
                $"unmatchedVfxAttempt={_foxMagicianTimelineUnmatchedVfxAttempts}, " +
                $"unmatchedVfxResult={_foxMagicianTimelineUnmatchedVfxResults}, " +
                $"unmatchedTimer={_foxMagicianTimelineUnmatchedTimerImpacts}, " +
                $"duplicates={_foxMagicianTimelineDuplicateTransitions}, overflow={_foxMagicianTimelineOverflow}, " +
                $"dropped={_foxMagicianTimelineDropped}, " +
                $"expired={_foxMagicianTimelineExpiredIncomplete}, " +
                $"cappedDetails={_foxMagicianTimelineDetailSuppressed}, " +
                $"maxStartMarker={maximumMarkerFromStart:F6}, maxStartTimer={maximumTimerFromStart:F6}, " +
                $"maxMarkerTimerDelta={maximumMarkerTimerDelta:F6}, " +
                $"expectedImpactOffset={FoxMagicianExpectedImpactSeconds:F3}, gameplayWrites=0, verdict={verdict}";
        }

        public static string BuildFoxMagicianTimelineTerminalDataForValidation()
            => BuildFoxMagicianTimelineTerminalData();

        private static void Log(LogLevel level, string message, string data)
        {
            if (_lines >= MaximumNormalLines)
            {
                Increment(ref _dropped);
                return;
            }
            _lines++;
            RuntimeLogger.Log(
                level, "Network", nameof(UnitAttackShadowObserver),
                $"[UAS-ATTACK-SHADOW] {message}", Decorate(data));
        }

        private static void LogTerminal(LogLevel level, string message, string data)
        {
            if (_terminalLines >= ReservedTerminalLines)
            {
                Increment(ref _dropped);
                return;
            }
            string decorated = Decorate(data);
            if (!IsFullLineUtf8Safe(message, decorated))
            {
                Increment(ref _terminalPreflightFailures);
                return;
            }
            _terminalLines++;
            _lines++;
            RuntimeLogger.Log(
                level, "Network", nameof(UnitAttackShadowObserver),
                $"[UAS-ATTACK-SHADOW] {message}", decorated);
        }

        private static string Decorate(string data)
            => $"runId={_runId}, sharedSessionKey={_sessionKey}, startedAt={_startedAt:F6}, {data}";

        private static bool IsFullLineUtf8Safe(string message, string decorated)
            => Encoding.UTF8.GetByteCount(FormatFullLine(message, decorated))
                < MaximumFullLineUtf8BytesExclusive;

        private static string FormatFullLine(string message, string decorated)
            => "[00:00:00.000] [ERROR] [Network/UnitAttackShadowObserver] " +
                $"[UAS-ATTACK-SHADOW] {message} | {decorated}";

        private static double ReadServerTime()
            => _networkManager != null && _networkManager.IsListening
                ? _networkManager.ServerTime.Time
                : double.NaN;

        private static string CreateSharedSessionKey()
        {
            NetworkGameManager manager = UnityEngine.Object.FindFirstObjectByType<NetworkGameManager>();
            string lobbyId = manager?.CurrentLobby?.Id;
            if (string.IsNullOrEmpty(lobbyId)) return "unavailable";
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(lobbyId));
                var text = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                    text.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unavailable";
            string normalized = value.Replace(',', '_').Replace('\r', '_').Replace('\n', '_');
            return normalized.Length <= 64 ? normalized : normalized.Substring(0, 64);
        }

        private static void Increment(ref int value)
        {
            if (value < int.MaxValue) value++;
        }

        private static void Reset()
        {
            UnitAttackResultPresentationShadowBridge.Coordinator.Decided -= ObserveCoordinatorDecision;
            _coordinatorFailureDetails = 0;
            ExpiryArrivals.Clear();
            Array.Clear(ExpiryFrameGaps, 0, ExpiryFrameGaps.Length);
            _expiryArrivalOverflow = _expiryArrivalRecorded = _expiryGapWrite = 0;
            _expiryPreviousFrameTime = double.NaN;
            UnitAttackResultPresentationShadowBridge.Classified -= ObservePresentationClassification;
            ClientClassifiers.Clear();
            ClientImpactClassifiers.Clear();
            CoveredUnitTypes.Clear();
            OnceKeys.Clear();
            AnimatedFacingEvidenceKeys.Clear();
            StreamSpiritTimelineFlows.Clear();
            FoxMagicianTimelineFlows.Clear();
            FoxMagicianTimelineTicketKeys.Clear();
            FoxMagicianTimelineKeyTickets.Clear();
            InfernoMotionJumpEvidence.Reset();
            _infernoMotionPreflightFailureLogged = false;
            _infernoAttackVfxOverflowLogged = false;
            _streamSpiritTimelineOverflowLogged = false;
            _foxMagicianTimelineOverflowLogged = false;
            _networkManager = null;
            _active = false;
            _isServer = false;
            _runId = null;
            _sessionKey = null;
            _startedAt = 0d;
            _lines = _terminalLines = _dropped = _suppressedNormal = 0;
            _infernoAttackVfxRawEvents = _infernoAttackVfxStarted = 0;
            _infernoAttackVfxSourceOnlyPreserved = 0;
            _infernoAttackVfxGateSuppressed = _infernoAttackVfxPlaybackFailures = 0;
            _streamSpiritTimelineStarts = _streamSpiritTimelineMarkers = 0;
            _streamSpiritTimelineVfxAttempts = _streamSpiritTimelineVfxStarted = 0;
            _streamSpiritTimelineVfxFailures = _streamSpiritTimelineTimerImpacts = 0;
            _streamSpiritTimelineUnmatchedMarkers = _streamSpiritTimelineUnmatchedVfx = 0;
            _streamSpiritTimelineUnmatchedTimerImpacts = 0;
            _streamSpiritTimelineDuplicateTransitions = 0;
            _streamSpiritTimelineRetiredComplete = 0;
            _streamSpiritTimelineRetiredMaxMarkerFromStart = 0d;
            _streamSpiritTimelineRetiredMaxTimerFromStart = 0d;
            _streamSpiritTimelineRetiredMaxMarkerTimerDelta = 0d;
            _foxMagicianTimelineStarts = _foxMagicianTimelineMarkers = 0;
            _foxMagicianTimelineVfxAttempts = _foxMagicianTimelineVfxStarted = 0;
            _foxMagicianTimelineVfxFailures = _foxMagicianTimelineTimerImpacts = 0;
            _foxMagicianTimelineParticlePositiveResults = 0;
            _foxMagicianTimelinePlaybackActiveResults = 0;
            _foxMagicianTimelineMaxParticleSystemCount = 0;
            _foxMagicianTimelineUnmatchedMarkers = _foxMagicianTimelineUnmatchedVfxAttempts = 0;
            _foxMagicianTimelineUnmatchedVfxResults = _foxMagicianTimelineUnmatchedTimerImpacts = 0;
            _foxMagicianTimelineDuplicateTransitions = _foxMagicianTimelineOverflow = 0;
            _foxMagicianTimelineDropped = 0;
            _foxMagicianTimelineDetailLines = 0;
            _foxMagicianTimelineDetailSuppressed = 0;
            _foxMagicianTimelineExpiredIncomplete = 0;
            _foxMagicianTimelineRetiredComplete = 0;
            _foxMagicianTimelineNextCorrelationTicket = 0UL;
            _foxMagicianTimelineRetiredMaxMarkerFromStart = 0d;
            _foxMagicianTimelineRetiredMaxTimerFromStart = 0d;
            _foxMagicianTimelineRetiredMaxMarkerTimerDelta = 0d;
            _intentSamples = _commits = _legacySchedules = _legacyBeforeAligned = 0;
            _expectedDeferred = _correlationFailures = 0;
            _productionGateSamples = _productionGateReady = _productionGateDeferred = 0;
            _productionGateInvalid = 0;
            _impactSamples = _targetMismatches = _clientAccepted = _clientRejected = 0;
            _serverResults = _resultCompletionFailures = 0;
            _clientResultAccepted = _clientResultRejected = 0;
            _terminalPreflightFailures = 0;
            _manifestFailures = 0;
            _presentationScheduled = _presentationMatched = _presentationUnmatched = 0;
            _presentationAmbiguous = _presentationDuplicates = _presentationConflicts = 0;
            _presentationCatchUps = _presentationExpired = _presentationInvalid = 0;
            _presentationRetired = _presentationDirectionMismatches = _presentationTimingMismatches = 0;
            _presentationRecoveryEmits = _presentationTimeoutRecoveries = 0;
            _presentationTargetDeathRecoveries = _presentationAttackerStopRecoveries = 0;
            _presentationRecoveryTimingMismatches = 0;
            _presentationDirectionSameRevisionMismatches = 0;
            _presentationDirectionRevisionLags = 0;
            _presentationDirectionScopeMismatches = 0;
            _presentationDirectionEvidenceInvalid = 0;
            _presentationMarkerMatched = _presentationTracerMatched = 0;
            _presentationEnqueueMatched = _presentationEmitMatched = 0;
            _presentationFailureDetails = 0;
            _presentationMarkerUnmatched = _presentationTracerUnmatched = 0;
            _presentationEnqueueUnmatched = _presentationEmitUnmatched = 0;
            _presentationMaximumAimDelta = _presentationMaximumTimingDelta = 0d;
            _presentationMaximumRecoveryDelta = 0d;
            _contractLogged = false;
            _causalErrorEmitted = false;
        }
    }
}
#endif
