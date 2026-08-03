using System;

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
    /// 서버 이동·SimulationFacing을 결정하는 production-grade 순수 reducer다.
    /// B2에서는 Shadow observer가 읽기 전용으로 사용하고, B3에서는 단일 권위 writer가
    /// 같은 결정을 소비한다. Unity, NGO, Transform과 무관하며 writer를 직접 호출하지 않는다.
    /// </summary>
    public sealed class UnitMovementReducer
    {
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
