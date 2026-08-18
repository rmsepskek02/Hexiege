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

            UnitMovementReducerStatus status = reducer.Evaluate(
                reducer.Snapshot.Revision,
                commandRevision,
                segmentRevision,
                evaluatedIntentReason,
                evaluatedHasIntent,
                simulationFacing,
                desiredDirection,
                targetAcquirePriority,
                serverTime,
                out UnitMovementDecision decision);
            return new UnitMovementEvaluation(
                status,
                decision,
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

    /// <summary>
    /// 곡선 trajectory가 타일 중심을 통과하지 않더라도 A* waypoint의 도메인 의미를
    /// 순서대로 정확히 한 번만 소비하게 하는 순수 checkpoint다.
    /// Presentation adapter는 true를 받은 경우에만 ProcessStep과 OnUnitEnteredTile을
    /// 각각 한 번 실행한다. 중복·건너뛰기·미도달 호출은 상태를 바꾸지 않는다.
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
            decision = UnitMovementDecision.Invalid();

            if (expectedRevision != _revision)
                return UnitMovementReducerStatus.StaleRevision;
            if (_revision == ulong.MaxValue)
                return UnitMovementReducerStatus.Exhausted;
            if (!IsFiniteNonNegative(observedServerTime))
                return UnitMovementReducerStatus.InvalidTime;
            if (commandRevision == 0UL || segmentRevision == 0UL)
                return UnitMovementReducerStatus.InvalidInput;
            if (!Enum.IsDefined(typeof(UnitMovementIntentReason), intentReason))
                return UnitMovementReducerStatus.InvalidInput;

            UnitMovementReducerStatus scopeStatus =
                ValidateScope(commandRevision, segmentRevision);
            if (scopeStatus != UnitMovementReducerStatus.Accepted)
                return scopeStatus;

            if (!simulationFacing.IsValid)
                return UnitMovementReducerStatus.InvalidInput;
            if (hasIntent)
            {
                if (intentReason == UnitMovementIntentReason.None
                    || !desiredMoveDirection.IsValid)
                    return UnitMovementReducerStatus.InvalidInput;
            }
            else if (intentReason != UnitMovementIntentReason.None
                || desiredMoveDirection.IsValid)
            {
                return UnitMovementReducerStatus.InvalidInput;
            }

            if (_hasAcceptedObservation)
            {
                if (observedServerTime < _lastObservedServerTime)
                    return UnitMovementReducerStatus.InvalidTime;

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
                        decision = CreateCurrentDecision();
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
                    return UnitMovementReducerStatus.InvalidInput;

                bool allowsMovement = UnitActionAngleHysteresis.AllowsMovement(
                    yawError, wasMovingInSameSegment);
                acceptedDecision = UnitMovementDecision.CreateMovement(
                    allowsMovement
                        ? UnitMovementPhase.Move
                        : UnitMovementPhase.AlignToMove,
                    yawError,
                    false);
            }

            _commandRevision = commandRevision;
            _segmentRevision = segmentRevision;
            _intentReason = intentReason;
            _phase = acceptedDecision.Phase;
            _hasIntent = hasIntent;
            _simulationFacing = simulationFacing;
            _desiredMoveDirection = desiredMoveDirection;
            _hasYawError = acceptedDecision.HasYawError;
            _yawErrorDegrees = acceptedDecision.YawErrorDegrees;
            _targetAcquirePriority = targetAcquirePriority;
            _lastObservedServerTime = observedServerTime;
            _hasAcceptedObservation = true;
            _revision++;

            decision = acceptedDecision;
            return UnitMovementReducerStatus.Accepted;
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
