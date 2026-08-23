using System;
using System.Collections.Generic;
using System.Text;
using Hexiege.Domain;

namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>
    /// Legacy 이동이 현재 사용 중인 이동 목표가 어디에서 왔는지 분류한다.
    /// 값은 진단 상관관계에만 사용하며 경로, 타겟 또는 이동 결과를 변경하지 않는다.
    /// </summary>
    public enum UnitMovementIntentReason
    {
        None = 0,
        AStarPath = 1,
        BlockedRepath = 2,
        PendingRepath = 3,
        Chase = 4,
        PostCombatResume = 5,
        HealerResume = 6
    }

    /// <summary>
    /// 서버 권위 이동 판정 단계다. Invalid는 입력을 신뢰할 수 없어 이동을
    /// 허용하지 않았다는 뜻이며, reducer의 마지막 유효 상태를 덮어쓰지 않는다.
    /// </summary>
    public enum UnitMovementPhase
    {
        NoIntent = 0,
        AlignToMove = 1,
        Move = 2,
        Invalid = 3
    }

    /// <summary>순수 서버 이동 reducer 호출 결과다.</summary>
    public enum UnitMovementReducerStatus
    {
        Accepted = 0,
        Duplicate = 1,
        StaleRevision = 2,
        StaleCommandRevision = 3,
        StaleSegmentRevision = 4,
        InvalidInput = 5,
        InvalidTime = 6,
        Exhausted = 7
    }

    /// <summary>
    /// 서버 Simulation Root의 회전 writer 소유자다. 이동과 공격/힐 같은 행동 writer가
    /// 같은 프레임에 root를 쓰지 않도록 값 하나로 배타성을 표현한다.
    /// </summary>
    public enum UnitRootRotationWriter
    {
        None = 0,
        Movement = 1,
        Action = 2
    }

    /// <summary>
    /// 이동→전투 경계의 회전 writer 인계를 담당하는 순수 상태 객체다.
    /// Unity Transform이나 NGO를 모르며 호출자는 현재 owner만 실제 writer gate로 사용한다.
    /// </summary>
    public sealed class UnitRootRotationOwnership
    {
        public UnitRootRotationWriter Owner { get; private set; }
        public bool MovementOwnsRoot => Owner == UnitRootRotationWriter.Movement;
        public bool ActionOwnsRoot => Owner == UnitRootRotationWriter.Action;

        /// <summary>
        /// 행동 writer가 활성 상태가 아닐 때만 이동 writer가 소유권을 얻는다.
        /// 전투 도중 이동 코루틴이 재개되어도 이 호출은 행동 소유권을 덮어쓰지 않는다.
        /// </summary>
        public bool TryAcquireMovement()
        {
            if (ActionOwnsRoot) return false;
            Owner = UnitRootRotationWriter.Movement;
            return true;
        }

        /// <summary>
        /// 현재 이동 소유 여부와 관계없이 서버 행동 경계에서 Action으로 원자 인계한다.
        /// 반환값은 실제로 Movement→Action 인계가 일어났는지 진단할 때 사용한다.
        /// </summary>
        public bool TransferToAction()
        {
            bool transferredFromMovement = MovementOwnsRoot;
            Owner = UnitRootRotationWriter.Action;
            return transferredFromMovement;
        }

        /// <summary>
        /// 행동 종료 후 살아 있는 이동 코루틴이 있으면 Movement로, 아니면 None으로 반환한다.
        /// </summary>
        public void ReleaseAction(bool resumeMovement)
        {
            if (!ActionOwnsRoot) return;
            Owner = resumeMovement
                ? UnitRootRotationWriter.Movement
                : UnitRootRotationWriter.None;
        }

        public void ReleaseMovement()
        {
            if (MovementOwnsRoot)
                Owner = UnitRootRotationWriter.None;
        }

        public void ReleaseAll()
            => Owner = UnitRootRotationWriter.None;
    }

    /// <summary>
    /// 네트워크 전투 타겟 이벤트가 서버 Simulation Root의 행동 타겟을 갱신해도 되는지 결정한다.
    /// 순수 클라이언트는 표시용 타겟을 항상 받을 수 있지만, 서버/Host는 gameplay 경계가 이미
    /// Action 소유권을 연 뒤에 도착한 이벤트만 받아 지연된 Start/Change 이벤트가 이동 회전을
    /// 다시 공격 회전으로 되살리지 못하게 한다.
    /// </summary>
    public static class UnitActionRotationEventPolicy
    {
        public static bool ShouldAcceptTargetEvent(
            bool networkActive,
            bool networkServer,
            bool actionOwnsRoot)
            => !networkActive || !networkServer || actionOwnsRoot;

        /// <summary>
        /// 네트워크 서버의 행동 종료는 gameplay 루프가 직접 확정한다. ClientRpc에서 되돌아온
        /// Stop 이벤트는 순수 클라이언트 표시만 닫으며, 서버의 새 Action 소유권을 해제하지 않는다.
        /// </summary>
        public static bool ShouldAcceptStopEvent(
            bool networkActive,
            bool networkServer)
            => !networkActive || !networkServer;
    }

    /// <summary>
    /// 이동 command/segment revision을 reducer 호출 전에 예약만 하는 순수 정책이다.
    /// 경로/planner preflight가 실패하면 예약을 버리고, reducer가 실제로 판정을 수락한 뒤에만
    /// UnitView가 현재 revision으로 확정한다. 따라서 reducer가 보지 못한 revision gap이 생기지 않는다.
    /// </summary>
    public static class UnitMovementScopePolicy
    {
        public static bool TryPrepare(
            ulong currentCommandRevision,
            ulong currentSegmentRevision,
            bool pendingCommand,
            bool pendingSegment,
            out ulong preparedCommandRevision,
            out ulong preparedSegmentRevision)
        {
            preparedCommandRevision = currentCommandRevision;
            preparedSegmentRevision = currentSegmentRevision;

            if (pendingCommand)
            {
                if (currentCommandRevision == ulong.MaxValue) return false;
                preparedCommandRevision = currentCommandRevision + 1UL;
                preparedSegmentRevision = 1UL;
                return true;
            }

            if (pendingSegment)
            {
                if (currentCommandRevision == 0UL
                    || currentSegmentRevision == 0UL
                    || currentSegmentRevision == ulong.MaxValue)
                    return false;

                preparedSegmentRevision = currentSegmentRevision + 1UL;
                return true;
            }

            return currentCommandRevision != 0UL
                && currentSegmentRevision != 0UL;
        }
    }

    /// <summary>
    /// 서버 이동 결정과 Walk 재생 여부를 연결하는 순수 정책이다. 위치를 전진하지 않는
    /// AlignToMove/NoIntent 결정에서는 Walk 시간을 멈춰 제자리 걷기를 허용하지 않는다.
    /// </summary>
    public static class UnitMovementPresentationPolicy
    {
        public static bool ShouldHoldWalk(
            bool decisionValid,
            bool allowsMovement,
            bool commitsAcquireCandidate)
            => decisionValid
                && !allowsMovement
                && !commitsAcquireCandidate;
    }

    /// <summary>
    /// 한 번의 서버 이동 판정 결과다. Invalid 판정과 유효한 NoIntent 판정을
    /// 구분할 수 있도록 IsValid와 Phase를 함께 제공한다.
    /// </summary>
    public readonly struct UnitMovementDecision
    {
        public UnitMovementPhase Phase { get; }
        public bool AllowsMovement { get; }
        public bool HasYawError { get; }
        public double YawErrorDegrees { get; }
        public bool IsTargetAcquirePriority { get; }
        public bool IsValid { get; }

        private UnitMovementDecision(
            UnitMovementPhase phase,
            bool hasYawError,
            double yawErrorDegrees,
            bool targetAcquirePriority,
            bool isValid)
        {
            Phase = phase;
            AllowsMovement = isValid && phase == UnitMovementPhase.Move;
            HasYawError = hasYawError;
            YawErrorDegrees = yawErrorDegrees;
            IsTargetAcquirePriority = targetAcquirePriority;
            IsValid = isValid;
        }

        internal static UnitMovementDecision Invalid()
            => new UnitMovementDecision(
                UnitMovementPhase.Invalid, false, 0d, false, false);

        internal static UnitMovementDecision CreateNoIntent(bool targetAcquirePriority)
            => new UnitMovementDecision(
                UnitMovementPhase.NoIntent, false, 0d, targetAcquirePriority, true);

        internal static UnitMovementDecision CreateMovement(
            UnitMovementPhase phase,
            double yawErrorDegrees,
            bool targetAcquirePriority)
            => new UnitMovementDecision(
                phase, true, yawErrorDegrees, targetAcquirePriority, true);
    }

    /// <summary>
    /// 마지막으로 수락한 서버 이동 판정의 순수 값 snapshot이다.
    /// CommandRevision은 외부 이동 명령 회차, SegmentRevision은 같은 명령 안의
    /// 재경로·추격·전투 복귀 구간 회차다.
    /// </summary>
    public readonly struct UnitMovementSnapshot
    {
        public ulong Revision { get; }
        public ulong CommandRevision { get; }
        public ulong SegmentRevision { get; }
        public UnitMovementIntentReason IntentReason { get; }
        public UnitMovementPhase Phase { get; }
        public bool HasIntent { get; }
        public ActionDirectionXZ SimulationFacing { get; }
        public ActionDirectionXZ DesiredMoveDirection { get; }
        public bool HasYawError { get; }
        public double YawErrorDegrees { get; }
        public bool IsTargetAcquirePriority { get; }
        public double LastObservedServerTime { get; }
        public bool HasAcceptedObservation { get; }

        internal UnitMovementSnapshot(
            ulong revision,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementIntentReason intentReason,
            UnitMovementPhase phase,
            bool hasIntent,
            ActionDirectionXZ simulationFacing,
            ActionDirectionXZ desiredMoveDirection,
            bool hasYawError,
            double yawErrorDegrees,
            bool targetAcquirePriority,
            double lastObservedServerTime,
            bool hasAcceptedObservation)
        {
            Revision = revision;
            CommandRevision = commandRevision;
            SegmentRevision = segmentRevision;
            IntentReason = intentReason;
            Phase = phase;
            HasIntent = hasIntent;
            SimulationFacing = simulationFacing;
            DesiredMoveDirection = desiredMoveDirection;
            HasYawError = hasYawError;
            YawErrorDegrees = yawErrorDegrees;
            IsTargetAcquirePriority = targetAcquirePriority;
            LastObservedServerTime = lastObservedServerTime;
            HasAcceptedObservation = hasAcceptedObservation;
        }
    }

    /// <summary>
    /// Unity Transform과 무관한 공통 이동 adapter 결과다. B2 observer와 B3 writer가
    /// 같은 endpoint 정규화와 reducer 입력 생성을 사용하도록 단일 구현으로 유지한다.
    /// </summary>
    public readonly struct UnitMovementEvaluation
    {
        public UnitMovementReducerStatus Status { get; }
        public UnitMovementDecision Decision { get; }
        public UnitMovementIntentReason EvaluatedIntentReason { get; }
        public bool EvaluatedHasIntent { get; }
        public ActionDirectionXZ SimulationFacing { get; }
        public ActionDirectionXZ DesiredMoveDirection { get; }
        public bool EndpointNormalized { get; }
        public bool ReducerInvoked { get; }
        public bool CommitsAcquireCandidate { get; }

        internal UnitMovementEvaluation(
            UnitMovementReducerStatus status,
            UnitMovementDecision decision,
            UnitMovementIntentReason evaluatedIntentReason,
            bool evaluatedHasIntent,
            ActionDirectionXZ simulationFacing,
            ActionDirectionXZ desiredMoveDirection,
            bool endpointNormalized,
            bool reducerInvoked,
            bool commitsAcquireCandidate = false)
        {
            Status = status;
            Decision = decision;
            EvaluatedIntentReason = evaluatedIntentReason;
            EvaluatedHasIntent = evaluatedHasIntent;
            SimulationFacing = simulationFacing;
            DesiredMoveDirection = desiredMoveDirection;
            EndpointNormalized = endpointNormalized;
            ReducerInvoked = reducerInvoked;
            CommitsAcquireCandidate = commitsAcquireCandidate;
        }

        internal static UnitMovementEvaluation Invalid()
            => new UnitMovementEvaluation(
                UnitMovementReducerStatus.InvalidInput,
                UnitMovementDecision.Invalid(),
                UnitMovementIntentReason.None,
                false,
                default,
                default,
                false,
                false);
    }

    /// <summary>
    /// reducer가 mutation 없이 준비한 단일 이동 전이다. 게시가 성공한 뒤에만 원본 reducer가
    /// source revision을 재확인하고 PreparedSnapshot을 커밋한다.
    /// </summary>
    public readonly struct UnitMovementPreparedTransition
    {
        public ulong SourceRevision { get; }
        public UnitMovementReducerStatus Status { get; }
        public UnitMovementSnapshot PreparedSnapshot { get; }
        public UnitMovementDecision Decision { get; }
        public bool IsPrepared => PreparedSnapshot.Revision != 0UL
            && (Status == UnitMovementReducerStatus.Accepted
                || Status == UnitMovementReducerStatus.Duplicate);

        internal UnitMovementPreparedTransition(
            ulong sourceRevision,
            UnitMovementReducerStatus status,
            UnitMovementSnapshot preparedSnapshot,
            UnitMovementDecision decision)
        {
            SourceRevision = sourceRevision;
            Status = status;
            PreparedSnapshot = preparedSnapshot;
            Decision = decision;
        }
    }

    /// <summary>
    /// 월드 XZ pose를 reducer 계약으로 변환하는 유일한 production adapter다.
    /// 위치와 예상 이동량이 모두 endpoint tolerance 안일 때만 NoIntent로 정규화한다.
    /// </summary>
    public static class UnitMovementEvaluationAdapter
    {
        public const double PositionTolerance = 0.00001d;

        public static UnitMovementEvaluation Evaluate(
            UnitMovementReducer reducer,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementIntentReason sourceIntentReason,
            double positionX,
            double positionZ,
            double facingX,
            double facingZ,
            double desiredTargetX,
            double desiredTargetZ,
            bool targetAcquirePriority,
            double expectedPositionDeltaWorld,
            double serverTime)
        {
            UnitMovementEvaluation evaluation = Prepare(
                reducer, commandRevision, segmentRevision, sourceIntentReason,
                positionX, positionZ, facingX, facingZ,
                desiredTargetX, desiredTargetZ, targetAcquirePriority,
                expectedPositionDeltaWorld, serverTime,
                out UnitMovementPreparedTransition transition);
            if (evaluation.ReducerInvoked && transition.IsPrepared)
                reducer.TryCommitPrepared(transition);
            return evaluation;
        }

        public static UnitMovementEvaluation Prepare(
            UnitMovementReducer reducer,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementIntentReason sourceIntentReason,
            double positionX,
            double positionZ,
            double facingX,
            double facingZ,
            double desiredTargetX,
            double desiredTargetZ,
            bool targetAcquirePriority,
            double expectedPositionDeltaWorld,
            double serverTime,
            out UnitMovementPreparedTransition transition)
        {
            transition = default;
            if (reducer == null
                || !IsFiniteNonNegative(expectedPositionDeltaWorld)
                || !IsFinite(positionX)
                || !IsFinite(positionZ)
                || !IsFinite(desiredTargetX)
                || !IsFinite(desiredTargetZ)
                || !ActionDirectionXZ.TryCreate(
                    facingX, facingZ, out ActionDirectionXZ simulationFacing))
            {
                return UnitMovementEvaluation.Invalid();
            }

            double desiredX = desiredTargetX - positionX;
            double desiredZ = desiredTargetZ - positionZ;
            double desiredDistance = Math.Sqrt(desiredX * desiredX + desiredZ * desiredZ);
            if (!IsFiniteNonNegative(desiredDistance))
                return UnitMovementEvaluation.Invalid();

            bool expectedAtEndpoint =
                expectedPositionDeltaWorld <= PositionTolerance;
            bool desiredAtEndpoint = desiredDistance <= PositionTolerance;
            if (desiredAtEndpoint && !expectedAtEndpoint)
                return UnitMovementEvaluation.Invalid();

            bool endpointNormalized = expectedAtEndpoint && desiredAtEndpoint;
            UnitMovementIntentReason evaluatedIntentReason = endpointNormalized
                ? UnitMovementIntentReason.None
                : sourceIntentReason;
            bool evaluatedHasIntent = !endpointNormalized;
            ActionDirectionXZ desiredDirection = default;
            if (evaluatedHasIntent
                && !ActionDirectionXZ.TryCreate(
                    desiredX, desiredZ, out desiredDirection))
            {
                return UnitMovementEvaluation.Invalid();
            }

            UnitMovementReducerStatus status = reducer.Prepare(
                reducer.Snapshot.Revision,
                commandRevision,
                segmentRevision,
                evaluatedIntentReason,
                evaluatedHasIntent,
                simulationFacing,
                desiredDirection,
                targetAcquirePriority,
                serverTime,
                out transition);
            return new UnitMovementEvaluation(
                status,
                transition.Decision,
                evaluatedIntentReason,
                evaluatedHasIntent,
                simulationFacing,
                desiredDirection,
                endpointNormalized,
                true);
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFiniteNonNegative(double value)
            => IsFinite(value) && value >= 0d;
    }

    /// <summary>
    /// 한 서버 틱에 적용할 연속 이동 후보다.
    ///
    /// 이 값은 Unity Transform을 전혀 모르며, 위치 변화 방향과 SimulationFacing으로
    /// 사용할 방향을 하나의 TrajectoryDirection으로 묶는다. 따라서 호출자가 위치만
    /// 먼저 적용하거나 방향만 따로 적용하는 실수를 구조적으로 피할 수 있다.
    /// </summary>
    public readonly struct UnitTrajectoryStep
    {
        public WorldPointXZ CandidatePosition { get; }
        public ActionDirectionXZ TrajectoryDirection { get; }
        public double ConsumedDistanceWorld { get; }
        public bool RequiresStationaryAlignment { get; }
        public bool ReachedCurrentWaypoint { get; }
        public bool IsValid { get; }

        internal UnitTrajectoryStep(
            WorldPointXZ candidatePosition,
            ActionDirectionXZ trajectoryDirection,
            double consumedDistanceWorld,
            bool requiresStationaryAlignment,
            bool reachedCurrentWaypoint,
            bool isValid)
        {
            CandidatePosition = candidatePosition;
            TrajectoryDirection = trajectoryDirection;
            ConsumedDistanceWorld = consumedDistanceWorld;
            RequiresStationaryAlignment = requiresStationaryAlignment;
            ReachedCurrentWaypoint = reachedCurrentWaypoint;
            IsValid = isValid;
        }

        internal static UnitTrajectoryStep Invalid()
            => new UnitTrajectoryStep(default, default, 0d, false, false, false);
    }

    /// <summary>
    /// 서버 A*·추격·재경로·전투 복귀가 함께 사용하는 순수 trajectory planner다.
    ///
    /// 중요한 원칙:
    /// 1) 현재 waypoint 가까이에서는 다음 선분을 미리 보아 60도 방향 점프를 완만하게 만든다.
    /// 2) 한 틱의 방향 변화는 maximumTurnDegrees를 넘지 않는다.
    /// 3) 위치는 계산된 TrajectoryDirection으로만 진행한다. 이 때문에 이동 벡터와
    ///    SimulationFacing은 부동소수점 정규화 오차 외에는 동일하다.
    /// 4) 150도 이상의 반전은 이동하지 않고 정지 정렬 후보를 반환한다.
    ///
    /// logical path corridor 검사는 맵을 아는 adapter가 이 후보를 받은 뒤 수행한다.
    /// planner 자신은 Collider, HexMetrics, 씬 또는 NGO를 참조하지 않는다.
    /// </summary>
    public static class UnitServerTrajectoryPlanner
    {
        public const double DefaultMaximumTurnDegreesPerSecond = 270d;
        public const double ReverseAlignmentDegrees = 150d;

        public static bool TryPlan(
            WorldPointXZ currentPosition,
            ActionDirectionXZ currentFacing,
            WorldPointXZ currentWaypoint,
            bool hasNextWaypoint,
            WorldPointXZ nextWaypoint,
            double maximumTravelDistanceWorld,
            double maximumTurnDegrees,
            double cornerLookAheadDistanceWorld,
            out UnitTrajectoryStep step)
        {
            step = UnitTrajectoryStep.Invalid();
            if (!currentPosition.IsValid
                || !currentFacing.IsValid
                || !currentWaypoint.IsValid
                || (hasNextWaypoint && !nextWaypoint.IsValid)
                || !IsFiniteNonNegative(maximumTravelDistanceWorld)
                || !IsFiniteNonNegative(maximumTurnDegrees)
                || !IsFiniteNonNegative(cornerLookAheadDistanceWorld))
            {
                return false;
            }

            double toWaypointX = currentWaypoint.X - currentPosition.X;
            double toWaypointZ = currentWaypoint.Z - currentPosition.Z;
            double distanceToWaypoint = Math.Sqrt(
                toWaypointX * toWaypointX + toWaypointZ * toWaypointZ);
            if (!IsFiniteNonNegative(distanceToWaypoint)) return false;

            // 마지막 목적지에 이미 도착한 호출은 이동 의도가 아니다. endpoint NoIntent는
            // production adapter가 별도로 게시하므로 여기서는 유효한 0거리 후보를 만든다.
            if (distanceToWaypoint <= UnitMovementEvaluationAdapter.PositionTolerance)
            {
                step = new UnitTrajectoryStep(
                    currentPosition,
                    currentFacing,
                    0d,
                    false,
                    true,
                    true);
                return true;
            }

            if (!ActionDirectionXZ.TryCreate(
                toWaypointX, toWaypointZ, out ActionDirectionXZ toWaypoint))
            {
                return false;
            }

            ActionDirectionXZ desiredTangent = toWaypoint;
            if (hasNextWaypoint
                && cornerLookAheadDistanceWorld
                    > UnitMovementEvaluationAdapter.PositionTolerance)
            {
                double nextX = nextWaypoint.X - currentWaypoint.X;
                double nextZ = nextWaypoint.Z - currentWaypoint.Z;
                if (!ActionDirectionXZ.TryCreate(
                    nextX, nextZ, out ActionDirectionXZ outgoingDirection))
                {
                    return false;
                }

                // look-ahead 구간의 시작에서는 현재 선분을 그대로 따르고, waypoint에
                // 가까워질수록 다음 선분 접선의 비중을 smoothstep으로 높인다.
                double normalized = 1d - Math.Min(
                    1d, distanceToWaypoint / cornerLookAheadDistanceWorld);
                double blend = normalized * normalized * (3d - 2d * normalized);
                double blendedX = toWaypoint.X * (1d - blend)
                    + outgoingDirection.X * blend;
                double blendedZ = toWaypoint.Z * (1d - blend)
                    + outgoingDirection.Z * blend;
                if (!ActionDirectionXZ.TryCreate(
                    blendedX, blendedZ, out desiredTangent))
                {
                    return false;
                }
            }

            double rawYawError = UnitActionPoseSample.GetYawDegrees(
                currentFacing, toWaypoint);
            if (!IsFiniteNonNegative(rawYawError)) return false;

            bool requiresStationaryAlignment =
                rawYawError >= ReverseAlignmentDegrees;
            ActionDirectionXZ trajectoryDirection = requiresStationaryAlignment
                ? RotateTowards(currentFacing, toWaypoint, maximumTurnDegrees)
                : RotateTowards(currentFacing, desiredTangent, maximumTurnDegrees);
            if (!trajectoryDirection.IsValid) return false;

            double travelDistance = requiresStationaryAlignment
                ? 0d
                : maximumTravelDistanceWorld;
            if (!hasNextWaypoint)
                travelDistance = Math.Min(travelDistance, distanceToWaypoint);

            if (!WorldPointXZ.TryCreate(
                currentPosition.X + trajectoryDirection.X * travelDistance,
                currentPosition.Z + trajectoryDirection.Z * travelDistance,
                out WorldPointXZ candidatePosition))
            {
                return false;
            }

            // 중간 waypoint는 중심에 스냅하지 않는다. 현재 위치에서 waypoint로 향하던
            // 평면을 후보가 통과했으면 논리적으로 해당 waypoint를 소비한다.
            // A smoothed corner intentionally does not force the Simulation Root through the
            // waypoint centre. Therefore an intermediate waypoint is consumed at the discrete
            // closest approach: while inside the configured look-ahead zone, the previous frame
            // was still facing toward the waypoint, but this candidate no longer reduces its
            // distance. This is stateless and stable because both distances are measured from the
            // same fixed waypoint. The old pass-plane used the per-frame toWaypoint vector as its
            // normal; that normal rotated every tick and could make a valid curved path orbit the
            // waypoint forever. A zero-progress alignment step can never consume a checkpoint.
            double candidateToWaypointX = currentWaypoint.X - candidatePosition.X;
            double candidateToWaypointZ = currentWaypoint.Z - candidatePosition.Z;
            double candidateDistanceSquared =
                candidateToWaypointX * candidateToWaypointX
                + candidateToWaypointZ * candidateToWaypointZ;
            double currentDistanceSquared = distanceToWaypoint * distanceToWaypoint;
            bool reachedWaypoint;
            if (!hasNextWaypoint)
            {
                // The final endpoint keeps exact convergence; closest-approach consumption is
                // reserved for intermediate path checkpoints only.
                reachedWaypoint = travelDistance >= distanceToWaypoint
                    - UnitMovementEvaluationAdapter.PositionTolerance;
            }
            else
            {
                // A unit that is still rotating from a badly misaligned pose must not consume a
                // checkpoint merely because that temporary step increases its distance. The
                // accepted step must already be following the planned corner tangent (same
                // hemisphere). At the true closest point that tangent legitimately points past
                // the waypoint, so testing currentFacing against toWaypoint would be too strict.
                double followsPlannedTangent =
                    trajectoryDirection.X * desiredTangent.X
                    + trajectoryDirection.Z * desiredTangent.Z;
                bool insideCornerZone = cornerLookAheadDistanceWorld
                        > UnitMovementEvaluationAdapter.PositionTolerance
                    && distanceToWaypoint <= cornerLookAheadDistanceWorld;
                reachedWaypoint = travelDistance
                        > UnitMovementEvaluationAdapter.PositionTolerance
                    && insideCornerZone
                    && followsPlannedTangent > 0d
                    && candidateDistanceSquared >= currentDistanceSquared;
            }

            step = new UnitTrajectoryStep(
                candidatePosition,
                trajectoryDirection,
                travelDistance,
                requiresStationaryAlignment,
                reachedWaypoint,
                true);
            return true;
        }

        private static ActionDirectionXZ RotateTowards(
            ActionDirectionXZ from,
            ActionDirectionXZ to,
            double maximumDegrees)
        {
            if (!from.IsValid || !to.IsValid || !IsFiniteNonNegative(maximumDegrees))
                return default;

            double fromYaw = Math.Atan2(from.X, from.Z);
            double toYaw = Math.Atan2(to.X, to.Z);
            double delta = WrapRadians(toYaw - fromYaw);
            double maximumRadians = maximumDegrees * Math.PI / 180d;
            double applied = Math.Max(-maximumRadians, Math.Min(maximumRadians, delta));
            double resultYaw = fromYaw + applied;
            return ActionDirectionXZ.TryCreate(
                Math.Sin(resultYaw), Math.Cos(resultYaw), out ActionDirectionXZ result)
                ? result
                : default;
        }

        private static double WrapRadians(double radians)
        {
            double twoPi = Math.PI * 2d;
            double wrapped = (radians + Math.PI) % twoPi;
            if (wrapped < 0d) wrapped += twoPi;
            return wrapped - Math.PI;
        }

        private static bool IsFiniteNonNegative(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }

    public enum UnitTrajectoryCorridorSampleResult
    {
        OutsideCorridor = 0,
        Blocked = 1,
        Walkable = 2,
        StartTileEgress = 3,
        FinalDestination = 4
    }

    /// <summary>
    /// 이동 중 새 경로가 실제 Root보다 뒤에서 시작할 때 사용할 도착-커밋 경로 합성 정책이다.
    /// 현재 논리 타일에서 앞쪽 인접 타일까지를 첫 segment로 보존하므로, 호출 측은 앞쪽 타일을
    /// 미리 UnitData.Position에 기록할 필요가 없다. 실제 Root 경계 통과는 route checkpoint와
    /// 독립된 공용 spatial transition API가 커밋한다.
    /// </summary>
    public static class UnitPathStartTransitionPolicy
    {
        public static bool TryBuildArrivalCommittedPath(
            HexCoord logicalStart,
            HexCoord stagedStart,
            IReadOnlyList<HexCoord> routeFromStagedStart,
            out List<HexCoord> path)
        {
            path = null;

            // 비인접 타일까지 한 번에 논리 이동시키는 경로는 만들지 않는다.
            if (HexCoord.Distance(logicalStart, stagedStart) != 1)
                return false;
            if (routeFromStagedStart == null || routeFromStagedStart.Count == 0)
                return false;
            if (routeFromStagedStart[0] != stagedStart)
                return false;

            path = new List<HexCoord>(routeFromStagedStart.Count + 1)
            {
                logicalStart
            };
            for (int i = 0; i < routeFromStagedStart.Count; i++)
            {
                HexCoord waypoint = routeFromStagedStart[i];
                if (path[path.Count - 1] != waypoint)
                    path.Add(waypoint);
            }

            if (path.Count < 2)
            {
                path = null;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// A* path index와 corridor sample의 walkability 의미를 일치시키는 순수 정책이다.
    /// path[0]은 첫 segment에서 빠져나가는 출발점으로만 non-walkable을 허용한다.
    /// </summary>
    public static class UnitTrajectoryCorridorSamplePolicy
    {
        public static UnitTrajectoryCorridorSampleResult Evaluate(
            int pathCount,
            int waypointIndex,
            int matchedPathIndex,
            bool isWalkable)
        {
            if (pathCount < 2
                || waypointIndex < 1
                || waypointIndex >= pathCount
                || matchedPathIndex < 0
                || matchedPathIndex >= pathCount)
            {
                return UnitTrajectoryCorridorSampleResult.OutsideCorridor;
            }

            int firstAllowed = Math.Max(0, waypointIndex - 1);
            int lastAllowed = Math.Min(pathCount - 1, waypointIndex + 1);
            if (matchedPathIndex < firstAllowed || matchedPathIndex > lastAllowed)
                return UnitTrajectoryCorridorSampleResult.OutsideCorridor;

            if (isWalkable)
                return UnitTrajectoryCorridorSampleResult.Walkable;
            if (waypointIndex == 1 && matchedPathIndex == 0)
                return UnitTrajectoryCorridorSampleResult.StartTileEgress;
            if (matchedPathIndex == pathCount - 1)
                return UnitTrajectoryCorridorSampleResult.FinalDestination;

            return UnitTrajectoryCorridorSampleResult.Blocked;
        }
    }

    /// <summary>
    /// planner 후보를 logical path corridor에 맞추기 위해 선택한 fallback이다.
    /// RepathRequired는 현재 pose 자체도 안전하지 않아 실제 A* 재탐색이 필요하다는 뜻이다.
    /// </summary>
    public enum UnitTrajectoryCorridorResolution
    {
        Invalid = 0,
        FullSmooth = 1,
        ReducedSmooth = 2,
        FullDirect = 3,
        ReducedDirect = 4,
        StationaryAlignment = 5,
        RepathRequired = 6
    }

    /// <summary>
    /// 순수 trajectory planner와 맵 adapter의 corridor 판정을 연결한다.
    /// full smooth → 축소 smooth → full direct → 축소 direct → 제자리 정렬 순서로
    /// 안전 후보를 찾고, 현재 pose까지 안전하지 않을 때만 A* 재탐색을 요구한다.
    /// </summary>
    public static class UnitTrajectoryCorridorResolver
    {
        public const int MaximumDistanceReductionAttempts = 8;
        private const double ReductionFactor = 0.5d;

        public static UnitTrajectoryCorridorResolution Resolve(
            WorldPointXZ currentPosition,
            ActionDirectionXZ currentFacing,
            WorldPointXZ currentWaypoint,
            bool hasNextWaypoint,
            WorldPointXZ nextWaypoint,
            double maximumTravelDistanceWorld,
            double maximumTurnDegrees,
            double cornerLookAheadDistanceWorld,
            Func<UnitTrajectoryStep, bool> isStepInsideCorridor,
            out UnitTrajectoryStep resolvedStep)
        {
            resolvedStep = UnitTrajectoryStep.Invalid();
            if (isStepInsideCorridor == null)
                return UnitTrajectoryCorridorResolution.Invalid;

            bool plannedAny = false;
            if (TryPlan(
                    currentPosition,
                    currentFacing,
                    currentWaypoint,
                    hasNextWaypoint,
                    nextWaypoint,
                    maximumTravelDistanceWorld,
                    maximumTurnDegrees,
                    cornerLookAheadDistanceWorld,
                    out UnitTrajectoryStep smoothStep))
            {
                plannedAny = true;
                if (isStepInsideCorridor(smoothStep))
                {
                    resolvedStep = smoothStep;
                    return UnitTrajectoryCorridorResolution.FullSmooth;
                }
            }

            UnitTrajectoryCorridorResolution reducedSmooth = TryReduced(
                currentPosition,
                currentFacing,
                currentWaypoint,
                hasNextWaypoint,
                nextWaypoint,
                maximumTravelDistanceWorld,
                maximumTurnDegrees,
                cornerLookAheadDistanceWorld,
                isStepInsideCorridor,
                out UnitTrajectoryStep reducedSmoothStep,
                out bool reducedSmoothPlanned);
            plannedAny |= reducedSmoothPlanned;
            if (reducedSmooth == UnitTrajectoryCorridorResolution.ReducedSmooth)
            {
                resolvedStep = reducedSmoothStep;
                return reducedSmooth;
            }

            if (TryPlan(
                    currentPosition,
                    currentFacing,
                    currentWaypoint,
                    false,
                    default,
                    maximumTravelDistanceWorld,
                    maximumTurnDegrees,
                    0d,
                    out UnitTrajectoryStep directStep))
            {
                plannedAny = true;
                if (isStepInsideCorridor(directStep))
                {
                    resolvedStep = directStep;
                    return UnitTrajectoryCorridorResolution.FullDirect;
                }
            }

            UnitTrajectoryCorridorResolution reducedDirect = TryReduced(
                currentPosition,
                currentFacing,
                currentWaypoint,
                false,
                default,
                maximumTravelDistanceWorld,
                maximumTurnDegrees,
                0d,
                isStepInsideCorridor,
                out UnitTrajectoryStep reducedDirectStep,
                out bool reducedDirectPlanned);
            plannedAny |= reducedDirectPlanned;
            if (reducedDirect == UnitTrajectoryCorridorResolution.ReducedDirect)
            {
                resolvedStep = reducedDirectStep;
                return reducedDirect;
            }

            // 이동량 0으로 planner를 호출해 maximumTurnDegrees로 제한된 회전 방향을 얻는다.
            // RequiresStationaryAlignment를 강제하면 reducer는 raw waypoint 방향으로 Align을
            // 판정하고, SimulationFacing은 제한된 방향만 commit한다.
            if (TryPlan(
                    currentPosition,
                    currentFacing,
                    currentWaypoint,
                    false,
                    default,
                    0d,
                    maximumTurnDegrees,
                    0d,
                    out UnitTrajectoryStep zeroDistanceStep))
            {
                plannedAny = true;
                var stationaryStep = new UnitTrajectoryStep(
                    zeroDistanceStep.CandidatePosition,
                    zeroDistanceStep.TrajectoryDirection,
                    0d,
                    true,
                    zeroDistanceStep.ReachedCurrentWaypoint,
                    true);
                if (isStepInsideCorridor(stationaryStep))
                {
                    resolvedStep = stationaryStep;
                    return UnitTrajectoryCorridorResolution.StationaryAlignment;
                }
            }

            return plannedAny
                ? UnitTrajectoryCorridorResolution.RepathRequired
                : UnitTrajectoryCorridorResolution.Invalid;
        }

        /// <summary>
        /// typed 후보 verdict를 사용하는 권위 경로다. RouteInvalidated와 Fatal은 첫 후보에서
        /// 즉시 종료하며 거리 축소로 막힌 경계에 점근하지 않는다.
        /// </summary>
        public static UnitTrajectoryCorridorResolution Resolve(
            WorldPointXZ currentPosition,
            ActionDirectionXZ currentFacing,
            WorldPointXZ currentWaypoint,
            bool hasNextWaypoint,
            WorldPointXZ nextWaypoint,
            double maximumTravelDistanceWorld,
            double maximumTurnDegrees,
            double cornerLookAheadDistanceWorld,
            Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict> evaluateCandidate,
            out UnitTrajectoryStep resolvedStep,
            out UnitSpatialCandidateVerdict finalVerdict)
        {
            resolvedStep = UnitTrajectoryStep.Invalid();
            finalVerdict = UnitSpatialCandidateVerdict.Fatal;
            if (evaluateCandidate == null)
                return UnitTrajectoryCorridorResolution.Invalid;

            bool plannedAny = false;
            if (TryPlan(
                    currentPosition, currentFacing, currentWaypoint,
                    hasNextWaypoint, nextWaypoint, maximumTravelDistanceWorld,
                    maximumTurnDegrees, cornerLookAheadDistanceWorld,
                    out UnitTrajectoryStep smoothStep))
            {
                plannedAny = true;
                finalVerdict = evaluateCandidate(smoothStep);
                if (finalVerdict == UnitSpatialCandidateVerdict.Accepted)
                {
                    resolvedStep = smoothStep;
                    return UnitTrajectoryCorridorResolution.FullSmooth;
                }
                if (finalVerdict == UnitSpatialCandidateVerdict.RouteInvalidated)
                    return UnitTrajectoryCorridorResolution.RepathRequired;
                if (finalVerdict == UnitSpatialCandidateVerdict.Fatal)
                    return UnitTrajectoryCorridorResolution.Invalid;
            }

            UnitTrajectoryCorridorResolution reducedSmooth = TryReduced(
                currentPosition, currentFacing, currentWaypoint,
                hasNextWaypoint, nextWaypoint, maximumTravelDistanceWorld,
                maximumTurnDegrees, cornerLookAheadDistanceWorld,
                evaluateCandidate, out UnitTrajectoryStep reducedSmoothStep,
                out bool reducedSmoothPlanned, out finalVerdict);
            plannedAny |= reducedSmoothPlanned;
            if (reducedSmooth == UnitTrajectoryCorridorResolution.ReducedSmooth)
            {
                resolvedStep = reducedSmoothStep;
                return reducedSmooth;
            }
            if (finalVerdict == UnitSpatialCandidateVerdict.RouteInvalidated)
                return UnitTrajectoryCorridorResolution.RepathRequired;
            if (finalVerdict == UnitSpatialCandidateVerdict.Fatal)
                return UnitTrajectoryCorridorResolution.Invalid;

            if (TryPlan(
                    currentPosition, currentFacing, currentWaypoint,
                    false, default, maximumTravelDistanceWorld,
                    maximumTurnDegrees, 0d, out UnitTrajectoryStep directStep))
            {
                plannedAny = true;
                finalVerdict = evaluateCandidate(directStep);
                if (finalVerdict == UnitSpatialCandidateVerdict.Accepted)
                {
                    resolvedStep = directStep;
                    return UnitTrajectoryCorridorResolution.FullDirect;
                }
                if (finalVerdict == UnitSpatialCandidateVerdict.RouteInvalidated)
                    return UnitTrajectoryCorridorResolution.RepathRequired;
                if (finalVerdict == UnitSpatialCandidateVerdict.Fatal)
                    return UnitTrajectoryCorridorResolution.Invalid;
            }

            UnitTrajectoryCorridorResolution reducedDirect = TryReduced(
                currentPosition, currentFacing, currentWaypoint,
                false, default, maximumTravelDistanceWorld,
                maximumTurnDegrees, 0d, evaluateCandidate,
                out UnitTrajectoryStep reducedDirectStep,
                out bool reducedDirectPlanned, out finalVerdict);
            plannedAny |= reducedDirectPlanned;
            if (reducedDirect == UnitTrajectoryCorridorResolution.ReducedDirect)
            {
                resolvedStep = reducedDirectStep;
                return reducedDirect;
            }
            if (finalVerdict == UnitSpatialCandidateVerdict.RouteInvalidated)
                return UnitTrajectoryCorridorResolution.RepathRequired;
            if (finalVerdict == UnitSpatialCandidateVerdict.Fatal)
                return UnitTrajectoryCorridorResolution.Invalid;

            if (TryPlan(
                    currentPosition, currentFacing, currentWaypoint,
                    false, default, 0d, maximumTurnDegrees, 0d,
                    out UnitTrajectoryStep zeroDistanceStep))
            {
                plannedAny = true;
                var stationaryStep = new UnitTrajectoryStep(
                    zeroDistanceStep.CandidatePosition,
                    zeroDistanceStep.TrajectoryDirection,
                    0d,
                    true,
                    zeroDistanceStep.ReachedCurrentWaypoint,
                    true);
                finalVerdict = evaluateCandidate(stationaryStep);
                if (finalVerdict == UnitSpatialCandidateVerdict.Accepted)
                {
                    resolvedStep = stationaryStep;
                    return UnitTrajectoryCorridorResolution.StationaryAlignment;
                }
                if (finalVerdict == UnitSpatialCandidateVerdict.RouteInvalidated)
                    return UnitTrajectoryCorridorResolution.RepathRequired;
                if (finalVerdict == UnitSpatialCandidateVerdict.Fatal)
                    return UnitTrajectoryCorridorResolution.Invalid;
            }

            finalVerdict = UnitSpatialCandidateVerdict.CandidateUnsafe;
            return plannedAny
                ? UnitTrajectoryCorridorResolution.RepathRequired
                : UnitTrajectoryCorridorResolution.Invalid;
        }

        private static UnitTrajectoryCorridorResolution TryReduced(
            WorldPointXZ currentPosition,
            ActionDirectionXZ currentFacing,
            WorldPointXZ currentWaypoint,
            bool hasNextWaypoint,
            WorldPointXZ nextWaypoint,
            double maximumTravelDistanceWorld,
            double maximumTurnDegrees,
            double cornerLookAheadDistanceWorld,
            Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict> evaluateCandidate,
            out UnitTrajectoryStep resolvedStep,
            out bool plannedAny,
            out UnitSpatialCandidateVerdict finalVerdict)
        {
            resolvedStep = UnitTrajectoryStep.Invalid();
            plannedAny = false;
            finalVerdict = UnitSpatialCandidateVerdict.CandidateUnsafe;
            double reducedDistance = maximumTravelDistanceWorld;
            for (int attempt = 0; attempt < MaximumDistanceReductionAttempts; attempt++)
            {
                reducedDistance *= ReductionFactor;
                if (reducedDistance <= UnitMovementEvaluationAdapter.PositionTolerance)
                    break;
                if (!TryPlan(
                        currentPosition, currentFacing, currentWaypoint,
                        hasNextWaypoint, nextWaypoint, reducedDistance,
                        maximumTurnDegrees, cornerLookAheadDistanceWorld,
                        out UnitTrajectoryStep candidate))
                    continue;

                plannedAny = true;
                finalVerdict = evaluateCandidate(candidate);
                if (finalVerdict == UnitSpatialCandidateVerdict.CandidateUnsafe)
                    continue;
                if (finalVerdict != UnitSpatialCandidateVerdict.Accepted)
                    return UnitTrajectoryCorridorResolution.Invalid;

                resolvedStep = candidate;
                return hasNextWaypoint
                    && cornerLookAheadDistanceWorld
                        > UnitMovementEvaluationAdapter.PositionTolerance
                    ? UnitTrajectoryCorridorResolution.ReducedSmooth
                    : UnitTrajectoryCorridorResolution.ReducedDirect;
            }
            return UnitTrajectoryCorridorResolution.Invalid;
        }

        private static UnitTrajectoryCorridorResolution TryReduced(
            WorldPointXZ currentPosition,
            ActionDirectionXZ currentFacing,
            WorldPointXZ currentWaypoint,
            bool hasNextWaypoint,
            WorldPointXZ nextWaypoint,
            double maximumTravelDistanceWorld,
            double maximumTurnDegrees,
            double cornerLookAheadDistanceWorld,
            Func<UnitTrajectoryStep, bool> isStepInsideCorridor,
            out UnitTrajectoryStep resolvedStep,
            out bool plannedAny)
        {
            resolvedStep = UnitTrajectoryStep.Invalid();
            plannedAny = false;
            double reducedDistance = maximumTravelDistanceWorld;
            for (int attempt = 0;
                attempt < MaximumDistanceReductionAttempts;
                attempt++)
            {
                reducedDistance *= ReductionFactor;
                if (reducedDistance
                    <= UnitMovementEvaluationAdapter.PositionTolerance)
                {
                    break;
                }

                if (!TryPlan(
                        currentPosition,
                        currentFacing,
                        currentWaypoint,
                        hasNextWaypoint,
                        nextWaypoint,
                        reducedDistance,
                        maximumTurnDegrees,
                        cornerLookAheadDistanceWorld,
                        out UnitTrajectoryStep candidate))
                {
                    continue;
                }

                plannedAny = true;
                if (!isStepInsideCorridor(candidate))
                    continue;

                resolvedStep = candidate;
                return hasNextWaypoint
                    && cornerLookAheadDistanceWorld
                        > UnitMovementEvaluationAdapter.PositionTolerance
                    ? UnitTrajectoryCorridorResolution.ReducedSmooth
                    : UnitTrajectoryCorridorResolution.ReducedDirect;
            }

            return UnitTrajectoryCorridorResolution.Invalid;
        }

        private static bool TryPlan(
            WorldPointXZ currentPosition,
            ActionDirectionXZ currentFacing,
            WorldPointXZ currentWaypoint,
            bool hasNextWaypoint,
            WorldPointXZ nextWaypoint,
            double maximumTravelDistanceWorld,
            double maximumTurnDegrees,
            double cornerLookAheadDistanceWorld,
            out UnitTrajectoryStep step)
            => UnitServerTrajectoryPlanner.TryPlan(
                currentPosition,
                currentFacing,
                currentWaypoint,
                hasNextWaypoint,
                nextWaypoint,
                maximumTravelDistanceWorld,
                maximumTurnDegrees,
                cornerLookAheadDistanceWorld,
                out step)
                && step.IsValid;
    }

    /// <summary>
    /// 공간 전이 계획 결과다. 경로 checkpoint 진행과 실제 Root 타일 전이는 별도 계약이다.
    /// </summary>
    public enum UnitSpatialTransitionPlanStatus
    {
        Ready = 0,
        NoTransition = 1,
        TerminalContact = 2,
        InvalidInput = 3,
        NonAdjacentSample = 4,
        OutsideLogicalPath = 5,
        InsufficientCapacity = 6
    }

    public enum UnitSpatialPreflightStatus
    {
        Success = 0,
        InvalidUnitOrDependency = 1,
        InvalidChain = 2,
        MissingTile = 3,
        NonWalkable = 4
    }

    /// <summary>
    /// 공간 전이의 Application 사전검증 결과다. MissingTile/NonWalkable은 새 A* 경로로
    /// 회복할 수 있고, 유닛·의존성·인접 chain 위반은 현재 command를 계속할 수 없는 fatal이다.
    /// </summary>
    public readonly struct UnitSpatialPreflightResult
    {
        public UnitSpatialPreflightStatus Status { get; }
        public HexCoord OffendingTile { get; }
        public int OffendingTransitionIndex { get; }
        public bool IsSuccess => Status == UnitSpatialPreflightStatus.Success;
        public bool IsRecoverable => Status == UnitSpatialPreflightStatus.MissingTile
            || Status == UnitSpatialPreflightStatus.NonWalkable;
        public bool IsFatal => !IsSuccess && !IsRecoverable;

        public UnitSpatialPreflightResult(
            UnitSpatialPreflightStatus status,
            HexCoord offendingTile,
            int offendingTransitionIndex)
        {
            Status = status;
            OffendingTile = offendingTile;
            OffendingTransitionIndex = offendingTransitionIndex;
        }

        public static UnitSpatialPreflightResult Success()
            => new UnitSpatialPreflightResult(
                UnitSpatialPreflightStatus.Success, default, -1);
    }

    public enum UnitSpatialCandidateVerdict
    {
        Accepted = 0,
        CandidateUnsafe = 1,
        RouteInvalidated = 2,
        Fatal = 3
    }

    /// <summary>순수 transition 결과와 Application 결과를 resolver용 의미로 합친다.</summary>
    public static class UnitSpatialCandidatePolicy
    {
        public static UnitSpatialCandidateVerdict Classify(
            UnitSpatialTransitionPlanStatus transitionStatus,
            UnitSpatialPreflightResult preflight)
        {
            if (transitionStatus == UnitSpatialTransitionPlanStatus.Ready
                || transitionStatus == UnitSpatialTransitionPlanStatus.NoTransition
                || transitionStatus == UnitSpatialTransitionPlanStatus.TerminalContact)
            {
                if (preflight.IsSuccess)
                    return UnitSpatialCandidateVerdict.Accepted;
                return preflight.IsRecoverable
                    ? UnitSpatialCandidateVerdict.RouteInvalidated
                    : UnitSpatialCandidateVerdict.Fatal;
            }

            if (transitionStatus == UnitSpatialTransitionPlanStatus.OutsideLogicalPath
                || transitionStatus == UnitSpatialTransitionPlanStatus.NonAdjacentSample)
            {
                return UnitSpatialCandidateVerdict.CandidateUnsafe;
            }

            return UnitSpatialCandidateVerdict.Fatal;
        }
    }

    /// <summary>
    /// v9 공간 진단 계수의 순수 의미 계약이다. 후보 stage는 실제 Root/Domain commit 시도가
    /// 아니며, transition 계획량은 Advanced frame의 commit 직전에만 증가한다.
    /// </summary>
    public static class UnitSpatialDiagnosticAccountingPolicy
    {
        public static int PlannedDeltaAtCandidateStage(int transitionCount)
            => 0;

        public static int PlannedDeltaAtCommitAttempt(int transitionCount)
            => transitionCount > 0 ? transitionCount : 0;

        public static int CommittedDeltaAtCommitResult(
            int transitionCount,
            bool succeeded)
            => succeeded && transitionCount > 0 ? transitionCount : 0;

        public static int CommitFailureDelta(bool succeeded)
            => succeeded ? 0 : 1;

        public static bool ClearsRecoverableHistory(
            int transitionCount,
            bool succeeded)
            => succeeded && transitionCount > 0;
    }

    /// <summary>
    /// Root가 한 프레임에 지나간 타일 표본을 실제 인접 타일 전이로 압축한다.
    /// 경로 checkpoint 소비와 무관한 순수 정책이며, 호출자가 제공한 버퍼만 사용한다.
    /// </summary>
    public static class UnitSpatialTransitionPolicy
    {
        public static UnitSpatialTransitionPlanStatus TryBuild(
            HexCoord committedTile,
            IReadOnlyList<HexCoord> sampledTiles,
            int sampleCount,
            IReadOnlyList<HexCoord> logicalPath,
            bool hasNonWalkableTerminalContact,
            HexCoord nonWalkableTerminalTile,
            HexCoord[] transitionBuffer,
            out int transitionCount)
        {
            transitionCount = 0;
            if (sampledTiles == null
                || sampleCount <= 0
                || sampleCount > sampledTiles.Count
                || transitionBuffer == null
                || sampledTiles[0] != committedTile)
            {
                return UnitSpatialTransitionPlanStatus.InvalidInput;
            }

            int logicalPathIndex = -1;
            if (logicalPath != null)
            {
                if (logicalPath.Count == 0)
                    return UnitSpatialTransitionPlanStatus.InvalidInput;

                for (int index = 0; index < logicalPath.Count; index++)
                {
                    if (logicalPath[index] == committedTile)
                    {
                        logicalPathIndex = index;
                        break;
                    }
                }

                if (logicalPathIndex < 0)
                    return UnitSpatialTransitionPlanStatus.OutsideLogicalPath;
            }

            if (hasNonWalkableTerminalContact
                && (logicalPath == null
                    || logicalPath.Count == 0
                    || logicalPath[logicalPath.Count - 1] != nonWalkableTerminalTile))
            {
                return UnitSpatialTransitionPlanStatus.InvalidInput;
            }

            HexCoord previousDistinctTile = committedTile;
            bool terminalReached = false;
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                HexCoord sampledTile = sampledTiles[sampleIndex];
                if (sampledTile == previousDistinctTile)
                    continue;

                if (terminalReached
                    || HexCoord.Distance(previousDistinctTile, sampledTile) != 1)
                {
                    transitionCount = 0;
                    return UnitSpatialTransitionPlanStatus.NonAdjacentSample;
                }

                if (logicalPath != null)
                {
                    int forwardIndex = logicalPathIndex + 1;
                    if (forwardIndex >= logicalPath.Count
                        || logicalPath[forwardIndex] != sampledTile)
                    {
                        transitionCount = 0;
                        return UnitSpatialTransitionPlanStatus.OutsideLogicalPath;
                    }

                    logicalPathIndex = forwardIndex;
                }

                previousDistinctTile = sampledTile;
                if (hasNonWalkableTerminalContact
                    && sampledTile == nonWalkableTerminalTile)
                {
                    terminalReached = true;
                    continue;
                }

                if (transitionCount >= transitionBuffer.Length)
                {
                    transitionCount = 0;
                    return UnitSpatialTransitionPlanStatus.InsufficientCapacity;
                }

                transitionBuffer[transitionCount++] = sampledTile;
            }

            if (terminalReached)
                return UnitSpatialTransitionPlanStatus.TerminalContact;

            return transitionCount > 0
                ? UnitSpatialTransitionPlanStatus.Ready
                : UnitSpatialTransitionPlanStatus.NoTransition;
        }
    }

    /// <summary>
    /// A* waypoint의 route 진행만 순서대로 소비하는 순수 checkpoint다.
    /// 도메인 위치, 위치 인덱스, 이동 이벤트 또는 실제 공간 타일을 변경하지 않는다.
    /// 중복·건너뛰기·미도달 호출은 상태를 바꾸지 않는다.
    /// </summary>
    public sealed class UnitPathCheckpointTracker
    {
        public int NextWaypointIndex { get; private set; }

        public UnitPathCheckpointTracker(int firstWaypointIndex = 1)
        {
            Reset(firstWaypointIndex);
        }

        public bool TryConsume(int waypointIndex, bool reachedWaypoint)
        {
            if (!reachedWaypoint || waypointIndex != NextWaypointIndex)
                return false;
            if (NextWaypointIndex == int.MaxValue)
                return false;

            NextWaypointIndex++;
            return true;
        }

        public void Reset(int firstWaypointIndex = 1)
        {
            NextWaypointIndex = firstWaypointIndex > 0
                ? firstWaypointIndex
                : 1;
        }
    }

    public enum UnitRepathDecision
    {
        AcceptedNextFrame = 0,
        RejectedInvalidPath = 1,
        RejectedRepeatedPath = 2,
        RejectedCycle = 3,
        RejectedFrameBudget = 4,
        RejectedNoProgressBudget = 5,
        RejectedMissingCurrentPosition = 6
    }

    /// <summary>
    /// 하나의 최종 목적지에 대한 서버 navigation 목표 상태다.
    /// 코루틴이나 segment가 바뀌어도 같은 목적지의 진행성 이력을 보존하기 위한 순수 계약이다.
    /// </summary>
    public enum UnitNavigationObjectiveState
    {
        Navigating = 0,
        WaitingRepath = 1,
        Blocked = 2,
        Completed = 3,
        Cancelled = 4
    }

    /// <summary>
    /// 경로 길이와 순서가 있는 HexCoord 전체로 만든 결정적 서명이다.
    /// 런타임 객체 identity나 플랫폼별 GetHashCode에 의존하지 않는다.
    /// </summary>
    public readonly struct UnitPathSignature : IEquatable<UnitPathSignature>
    {
        public int WaypointCount { get; }
        public ulong OrderedHash { get; }
        public bool IsValid { get; }

        private UnitPathSignature(int waypointCount, ulong orderedHash)
        {
            WaypointCount = waypointCount;
            OrderedHash = orderedHash;
            IsValid = true;
        }

        public static bool TryCreate(
            IReadOnlyList<HexCoord> path,
            out UnitPathSignature signature)
        {
            signature = default;
            if (path == null || path.Count < 2)
                return false;

            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offset;
                hash = (hash ^ (uint)path.Count) * prime;

                for (int index = 0; index < path.Count; index++)
                {
                    HexCoord coord = path[index];
                    if (index > 0 && coord == path[index - 1])
                        return false;

                    hash = (hash ^ (uint)coord.Q) * prime;
                    hash = (hash ^ (uint)coord.R) * prime;
                }

                signature = new UnitPathSignature(path.Count, hash);
                return true;
            }
        }

        public bool Equals(UnitPathSignature other)
            => IsValid == other.IsValid
                && WaypointCount == other.WaypointCount
                && OrderedHash == other.OrderedHash;

        public override bool Equals(object obj)
            => obj is UnitPathSignature other && Equals(other);

        public override int GetHashCode()
            => unchecked((WaypointCount * 397) ^ OrderedHash.GetHashCode());

        public static bool operator ==(UnitPathSignature left, UnitPathSignature right)
            => left.Equals(right);

        public static bool operator !=(UnitPathSignature left, UnitPathSignature right)
            => !left.Equals(right);
    }

    /// <summary>
    /// 재탐색이 수렴하지 않아 한 Unity frame 또는 여러 frame을 무한 점유하지 않도록 하는
    /// 순수 진행성 guard다. path를 직접 적용하지 않고 수락/거부만 결정한다.
    /// </summary>
    public sealed class UnitRepathProgressGuard
    {
        public const int MaximumAcceptedWithoutProgress = 8;

        private UnitPathSignature _previousPath;
        private UnitPathSignature _currentPath;
        private long _frameToken = long.MinValue;
        private int _acceptedInFrame;
        private int _acceptedWithoutProgress;

        public UnitPathSignature PreviousPath => _previousPath;
        public UnitPathSignature CurrentPath => _currentPath;
        public int AcceptedInFrame => _acceptedInFrame;
        public int AcceptedWithoutProgress => _acceptedWithoutProgress;

        public UnitRepathProgressGuard(IReadOnlyList<HexCoord> initialPath)
        {
            UnitPathSignature.TryCreate(initialPath, out _currentPath);
        }

        public UnitRepathDecision Evaluate(
            long frameToken,
            IReadOnlyList<HexCoord> candidatePath)
        {
            if (frameToken < 0
                || !UnitPathSignature.TryCreate(candidatePath, out UnitPathSignature candidate))
            {
                return UnitRepathDecision.RejectedInvalidPath;
            }

            if (frameToken != _frameToken)
            {
                _frameToken = frameToken;
                _acceptedInFrame = 0;
            }

            if (_acceptedInFrame >= 1)
                return UnitRepathDecision.RejectedFrameBudget;
            if (_acceptedWithoutProgress >= MaximumAcceptedWithoutProgress)
                return UnitRepathDecision.RejectedNoProgressBudget;

            // 같은 logical path가 한 번 반환됐다는 사실만으로 navigation 목표를 종료하지 않는다.
            // corridor planner와 A* pathfinder는 서로 다른 안전 기준을 사용하므로, 최신 pose에서도
            // 같은 최선 경로가 다시 나오는 정상적인 재평가가 가능하다. A→B→A 역시 즉시 terminal이
            // 아니라 bounded no-progress 관측으로 누적하고 여러 frame의 상한에서만 차단한다.
            if (!_currentPath.IsValid || candidate != _currentPath)
            {
                _previousPath = _currentPath;
                _currentPath = candidate;
            }
            _acceptedInFrame++;
            _acceptedWithoutProgress++;
            return UnitRepathDecision.AcceptedNextFrame;
        }

        public bool ObserveProgress(IReadOnlyList<HexCoord> committedPath)
        {
            if (!UnitPathSignature.TryCreate(
                    committedPath, out UnitPathSignature signature))
            {
                return false;
            }

            _currentPath = signature;
            _previousPath = default;
            _acceptedWithoutProgress = 0;
            return true;
        }

        /// <summary>
        /// 건물 생성·파괴처럼 walkability가 바뀌면 과거 path 반복은 더 이상 같은 환경의
        /// 무진전 증거가 아니다. 현재 signature는 보존하고 반복/예산 이력만 초기화한다.
        /// </summary>
        public void ResetForEnvironmentChange()
        {
            _previousPath = default;
            _frameToken = long.MinValue;
            _acceptedInFrame = 0;
            _acceptedWithoutProgress = 0;
        }
    }

    /// <summary>
    /// 최종 목적지 하나의 진행 상태와 재탐색 guard를 함께 보유한다.
    /// Presentation 코루틴은 이 객체를 소유할 수 있지만 판단 자체는 Unity/NGO와 무관하다.
    /// </summary>
    public sealed class UnitNavigationObjective
    {
        private readonly HexCoord _destination;
        private ulong _environmentRevision;
        private readonly UnitRepathProgressGuard _repathGuard;
        private UnitNavigationObjectiveState _state;

        public HexCoord Destination => _destination;
        public ulong EnvironmentRevision => _environmentRevision;
        public UnitRepathProgressGuard RepathGuard => _repathGuard;
        public UnitNavigationObjectiveState State => _state;
        public bool IsActive => _state == UnitNavigationObjectiveState.Navigating
            || _state == UnitNavigationObjectiveState.WaitingRepath
            || _state == UnitNavigationObjectiveState.Blocked;

        public UnitNavigationObjective(
            HexCoord destination,
            IReadOnlyList<HexCoord> initialPath,
            ulong environmentRevision)
        {
            _destination = destination;
            _environmentRevision = environmentRevision;
            _repathGuard = new UnitRepathProgressGuard(initialPath);
            _state = UnitNavigationObjectiveState.Navigating;
        }

        public bool Matches(HexCoord destination)
            => destination == _destination;

        /// <summary>
        /// 같은 환경에서 같은 목적지로 들어온 외부 MoveTo는 새 command가 아니다.
        /// 완료·취소됐거나 목적지가 달라야 새 objective를 만들 수 있다.
        /// </summary>
        public bool SuppressesDuplicateCommand(
            HexCoord destination,
            ulong environmentRevision)
            => IsActive
                && Matches(destination)
                && environmentRevision <= _environmentRevision;

        public void MarkWaitingRepath()
        {
            if (IsActive)
                _state = UnitNavigationObjectiveState.WaitingRepath;
        }

        public void MarkNavigating()
        {
            if (IsActive)
                _state = UnitNavigationObjectiveState.Navigating;
        }

        public void MarkBlocked()
        {
            if (IsActive)
                _state = UnitNavigationObjectiveState.Blocked;
        }

        public bool ObserveProgress(IReadOnlyList<HexCoord> committedPath)
        {
            if (!IsActive || !_repathGuard.ObserveProgress(committedPath))
                return false;

            _state = UnitNavigationObjectiveState.Navigating;
            return true;
        }

        public bool NotifyEnvironmentChanged(ulong environmentRevision)
        {
            if (!IsActive || environmentRevision <= _environmentRevision)
                return false;

            _environmentRevision = environmentRevision;
            _repathGuard.ResetForEnvironmentChange();
            _state = UnitNavigationObjectiveState.WaitingRepath;
            return true;
        }

        public void Complete()
        {
            if (IsActive)
                _state = UnitNavigationObjectiveState.Completed;
        }

        public void Cancel()
        {
            if (IsActive)
                _state = UnitNavigationObjectiveState.Cancelled;
        }
    }

    /// <summary>Android terminal evidence용 순수 lossless chunk/preflight 계약.</summary>
    public static class UnitMovementManifestChunker
    {
        public static bool TryBuild(
            IReadOnlyList<string> entries,
            int payloadUtf8Limit,
            int maximumChunks,
            int reservedTerminalLines,
            int nonManifestTerminalLines,
            out IReadOnlyList<string> chunks)
        {
            chunks = Array.Empty<string>();
            if (entries == null || payloadUtf8Limit <= 0 || maximumChunks < 0
                || reservedTerminalLines <= 0 || nonManifestTerminalLines < 1)
                return false;

            var result = new List<string>();
            var current = new StringBuilder();
            for (int index = 0; index < entries.Count; index++)
            {
                string entry = entries[index];
                if (string.IsNullOrEmpty(entry)
                    || Encoding.UTF8.GetByteCount(entry) > payloadUtf8Limit)
                    return false;

                string candidate = current.Length == 0
                    ? entry
                    : current.ToString() + ";" + entry;
                if (Encoding.UTF8.GetByteCount(candidate) > payloadUtf8Limit)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    current.Append(entry);
                }
                else
                {
                    if (current.Length > 0) current.Append(';');
                    current.Append(entry);
                }
            }
            if (current.Length > 0) result.Add(current.ToString());

            if (result.Count > maximumChunks
                || result.Count + nonManifestTerminalLines > reservedTerminalLines)
                return false;

            chunks = result;
            return true;
        }
    }

    /// <summary>
    /// 서버 이동·SimulationFacing을 결정하는 production-grade 순수 reducer다.
    /// B2에서는 Shadow observer가 읽기 전용으로 사용하고, B3에서는 단일 권위 writer가
    /// 같은 결정을 소비한다. Unity, NGO, Transform과 무관하며 writer를 직접 호출하지 않는다.
    /// </summary>
    public sealed class UnitMovementReducer
    {
        public const int ContractSchemaRevision = 1;
        public const string ContractSchemaVersion = "b2-movement-reducer-v1";

        private ulong _revision;
        private ulong _commandRevision;
        private ulong _segmentRevision;
        private UnitMovementIntentReason _intentReason;
        private UnitMovementPhase _phase;
        private bool _hasIntent;
        private ActionDirectionXZ _simulationFacing;
        private ActionDirectionXZ _desiredMoveDirection;
        private bool _hasYawError;
        private double _yawErrorDegrees;
        private bool _targetAcquirePriority;
        private double _lastObservedServerTime;
        private bool _hasAcceptedObservation;

        public UnitMovementSnapshot Snapshot => new UnitMovementSnapshot(
            _revision,
            _commandRevision,
            _segmentRevision,
            _intentReason,
            _phase,
            _hasIntent,
            _simulationFacing,
            _desiredMoveDirection,
            _hasYawError,
            _yawErrorDegrees,
            _targetAcquirePriority,
            _lastObservedServerTime,
            _hasAcceptedObservation);

        public UnitMovementReducer()
            : this(1UL)
        {
        }

        private UnitMovementReducer(ulong initialRevision)
        {
            _revision = initialRevision;
            _phase = UnitMovementPhase.NoIntent;
        }

        /// <summary>revision 고갈 경계를 Unity 없이 검증하기 위한 생성 진입점이다.</summary>
        public static UnitMovementReducer CreateForValidation(ulong initialRevision)
            => new UnitMovementReducer(initialRevision);

        /// <summary>
        /// 같은 서버 pose 입력을 신규 이동 규칙으로 판정한다.
        /// targetAcquirePriority는 이동 의도보다 우선하며, 이 경우 유효한 의도가 있어도
        /// NoIntent와 이동 금지를 반환한다.
        /// </summary>
        public UnitMovementReducerStatus Evaluate(
            ulong expectedRevision,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementIntentReason intentReason,
            bool hasIntent,
            ActionDirectionXZ simulationFacing,
            ActionDirectionXZ desiredMoveDirection,
            bool targetAcquirePriority,
            double observedServerTime,
            out UnitMovementDecision decision)
        {
            UnitMovementReducerStatus status = Prepare(
                expectedRevision,
                commandRevision,
                segmentRevision,
                intentReason,
                hasIntent,
                simulationFacing,
                desiredMoveDirection,
                targetAcquirePriority,
                observedServerTime,
                out UnitMovementPreparedTransition transition);
            decision = transition.Decision;
            if (transition.IsPrepared)
                TryCommitPrepared(transition);
            return status;
        }

        public UnitMovementReducerStatus Prepare(
            ulong expectedRevision,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementIntentReason intentReason,
            bool hasIntent,
            ActionDirectionXZ simulationFacing,
            ActionDirectionXZ desiredMoveDirection,
            bool targetAcquirePriority,
            double observedServerTime,
            out UnitMovementPreparedTransition transition)
        {
            transition = default;

            if (expectedRevision != _revision)
                return RejectPrepared(UnitMovementReducerStatus.StaleRevision, out transition);
            if (_revision == ulong.MaxValue)
                return RejectPrepared(UnitMovementReducerStatus.Exhausted, out transition);
            if (!IsFiniteNonNegative(observedServerTime))
                return RejectPrepared(UnitMovementReducerStatus.InvalidTime, out transition);
            if (commandRevision == 0UL || segmentRevision == 0UL)
                return RejectPrepared(UnitMovementReducerStatus.InvalidInput, out transition);
            if (!Enum.IsDefined(typeof(UnitMovementIntentReason), intentReason))
                return RejectPrepared(UnitMovementReducerStatus.InvalidInput, out transition);

            UnitMovementReducerStatus scopeStatus =
                ValidateScope(commandRevision, segmentRevision);
            if (scopeStatus != UnitMovementReducerStatus.Accepted)
                return RejectPrepared(scopeStatus, out transition);

            if (!simulationFacing.IsValid)
                return RejectPrepared(UnitMovementReducerStatus.InvalidInput, out transition);
            if (hasIntent)
            {
                if (intentReason == UnitMovementIntentReason.None
                    || !desiredMoveDirection.IsValid)
                    return RejectPrepared(UnitMovementReducerStatus.InvalidInput, out transition);
            }
            else if (intentReason != UnitMovementIntentReason.None
                || desiredMoveDirection.IsValid)
            {
                return RejectPrepared(UnitMovementReducerStatus.InvalidInput, out transition);
            }

            if (_hasAcceptedObservation)
            {
                if (observedServerTime < _lastObservedServerTime)
                    return RejectPrepared(UnitMovementReducerStatus.InvalidTime, out transition);

                if (observedServerTime == _lastObservedServerTime)
                {
                    if (IsExactDuplicate(
                        commandRevision,
                        segmentRevision,
                        intentReason,
                        hasIntent,
                        simulationFacing,
                        desiredMoveDirection,
                        targetAcquirePriority))
                    {
                        UnitMovementDecision duplicateDecision = CreateCurrentDecision();
                        transition = new UnitMovementPreparedTransition(
                            _revision,
                            UnitMovementReducerStatus.Duplicate,
                            Snapshot,
                            duplicateDecision);
                        return UnitMovementReducerStatus.Duplicate;
                    }
                }
            }

            bool scopeChanged = !_hasAcceptedObservation
                || commandRevision != _commandRevision
                || segmentRevision != _segmentRevision;
            bool wasMovingInSameSegment =
                !scopeChanged && _phase == UnitMovementPhase.Move;

            UnitMovementDecision acceptedDecision;
            if (targetAcquirePriority || !hasIntent)
            {
                acceptedDecision =
                    UnitMovementDecision.CreateNoIntent(targetAcquirePriority);
            }
            else
            {
                double yawError = UnitActionPoseSample.GetYawDegrees(
                    simulationFacing, desiredMoveDirection);
                if (!IsFiniteNonNegative(yawError))
                    return RejectPrepared(UnitMovementReducerStatus.InvalidInput, out transition);

                bool allowsMovement = UnitActionAngleHysteresis.AllowsMovement(
                    yawError, wasMovingInSameSegment);
                acceptedDecision = UnitMovementDecision.CreateMovement(
                    allowsMovement
                        ? UnitMovementPhase.Move
                        : UnitMovementPhase.AlignToMove,
                    yawError,
                    false);
            }

            var preparedSnapshot = new UnitMovementSnapshot(
                _revision + 1UL,
                commandRevision,
                segmentRevision,
                intentReason,
                acceptedDecision.Phase,
                hasIntent,
                simulationFacing,
                desiredMoveDirection,
                acceptedDecision.HasYawError,
                acceptedDecision.YawErrorDegrees,
                targetAcquirePriority,
                observedServerTime,
                true);
            transition = new UnitMovementPreparedTransition(
                _revision,
                UnitMovementReducerStatus.Accepted,
                preparedSnapshot,
                acceptedDecision);
            return UnitMovementReducerStatus.Accepted;
        }

        public bool CanCommitPrepared(UnitMovementPreparedTransition transition)
        {
            if (!transition.IsPrepared || transition.SourceRevision != _revision)
                return false;
            if (transition.Status == UnitMovementReducerStatus.Duplicate)
                return transition.PreparedSnapshot.Revision == _revision;
            return _revision != ulong.MaxValue
                && transition.PreparedSnapshot.Revision == _revision + 1UL;
        }

        public bool TryCommitPrepared(UnitMovementPreparedTransition transition)
        {
            if (!CanCommitPrepared(transition)) return false;
            if (transition.Status == UnitMovementReducerStatus.Duplicate)
                return true;

            UnitMovementSnapshot snapshot = transition.PreparedSnapshot;
            _revision = snapshot.Revision;
            _commandRevision = snapshot.CommandRevision;
            _segmentRevision = snapshot.SegmentRevision;
            _intentReason = snapshot.IntentReason;
            _phase = snapshot.Phase;
            _hasIntent = snapshot.HasIntent;
            _simulationFacing = snapshot.SimulationFacing;
            _desiredMoveDirection = snapshot.DesiredMoveDirection;
            _hasYawError = snapshot.HasYawError;
            _yawErrorDegrees = snapshot.YawErrorDegrees;
            _targetAcquirePriority = snapshot.IsTargetAcquirePriority;
            _lastObservedServerTime = snapshot.LastObservedServerTime;
            _hasAcceptedObservation = snapshot.HasAcceptedObservation;
            return true;
        }

        private UnitMovementReducerStatus RejectPrepared(
            UnitMovementReducerStatus status,
            out UnitMovementPreparedTransition transition)
        {
            transition = new UnitMovementPreparedTransition(
                _revision,
                status,
                Snapshot,
                UnitMovementDecision.Invalid());
            return status;
        }

        private UnitMovementReducerStatus ValidateScope(
            ulong commandRevision,
            ulong segmentRevision)
        {
            if (!_hasAcceptedObservation)
            {
                return commandRevision == 1UL && segmentRevision == 1UL
                    ? UnitMovementReducerStatus.Accepted
                    : UnitMovementReducerStatus.InvalidInput;
            }

            if (commandRevision < _commandRevision)
                return UnitMovementReducerStatus.StaleCommandRevision;

            if (commandRevision == _commandRevision)
            {
                if (segmentRevision < _segmentRevision)
                    return UnitMovementReducerStatus.StaleSegmentRevision;
                if (segmentRevision == _segmentRevision)
                    return UnitMovementReducerStatus.Accepted;
                if (_segmentRevision == ulong.MaxValue
                    || segmentRevision != _segmentRevision + 1UL)
                    return UnitMovementReducerStatus.InvalidInput;
                return UnitMovementReducerStatus.Accepted;
            }

            if (_commandRevision == ulong.MaxValue
                || commandRevision != _commandRevision + 1UL
                || segmentRevision != 1UL)
                return UnitMovementReducerStatus.InvalidInput;

            return UnitMovementReducerStatus.Accepted;
        }

        private bool IsExactDuplicate(
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementIntentReason intentReason,
            bool hasIntent,
            ActionDirectionXZ simulationFacing,
            ActionDirectionXZ desiredMoveDirection,
            bool targetAcquirePriority)
        {
            return commandRevision == _commandRevision
                && segmentRevision == _segmentRevision
                && intentReason == _intentReason
                && hasIntent == _hasIntent
                && simulationFacing.Equals(_simulationFacing)
                && desiredMoveDirection.Equals(_desiredMoveDirection)
                && targetAcquirePriority == _targetAcquirePriority;
        }

        private UnitMovementDecision CreateCurrentDecision()
        {
            if (_phase == UnitMovementPhase.NoIntent)
                return UnitMovementDecision.CreateNoIntent(_targetAcquirePriority);
            if (_phase == UnitMovementPhase.Move
                || _phase == UnitMovementPhase.AlignToMove)
            {
                return UnitMovementDecision.CreateMovement(
                    _phase, _yawErrorDegrees, _targetAcquirePriority);
            }

            return UnitMovementDecision.Invalid();
        }

        private static bool IsFiniteNonNegative(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }
}
