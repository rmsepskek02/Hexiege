using System;

namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>행동 계약에서 참조할 수 있는 엔티티 종류다. None은 참조 없음 자체를 표현한다.</summary>
    public enum EntityKind
    {
        None = 0,
        Unit = 1,
        Building = 2
    }

    /// <summary>
    /// 엔티티 종류와 안정 ID를 묶은 값이다. ID 0도 실제 엔티티로 유효하므로,
    /// 참조 없음은 ID가 아니라 Kind=None으로만 판정한다.
    /// </summary>
    public readonly struct EntityRef : IEquatable<EntityRef>
    {
        public static readonly EntityRef None = new EntityRef(EntityKind.None, 0);

        public EntityKind Kind { get; }
        public int Id { get; }
        public bool IsValid => (Kind == EntityKind.Unit || Kind == EntityKind.Building) && Id >= 0;
        public bool IsNone => Kind == EntityKind.None;

        public EntityRef(EntityKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public bool Equals(EntityRef other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is EntityRef other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Id;
        public static bool operator ==(EntityRef left, EntityRef right) => left.Equals(right);
        public static bool operator !=(EntityRef left, EntityRef right) => !left.Equals(right);
    }

    public enum AttackTargetMode
    {
        None = 0,
        TargetLocked = 1,
        LockedPoint = 2,
        Homing = 3
    }

    /// <summary>공격 회차에 잠글 타겟과 잠금 방식을 함께 보관한다.</summary>
    public readonly struct AttackTargetBinding : IEquatable<AttackTargetBinding>
    {
        public static readonly AttackTargetBinding None
            = new AttackTargetBinding(AttackTargetMode.None, EntityRef.None);

        public AttackTargetMode Mode { get; }
        public EntityRef Target { get; }
        public bool IsValid => (Mode == AttackTargetMode.TargetLocked
                || Mode == AttackTargetMode.LockedPoint
                || Mode == AttackTargetMode.Homing)
            && Target.IsValid;

        public AttackTargetBinding(AttackTargetMode mode, EntityRef target)
        {
            Mode = mode;
            Target = target;
        }

        public bool Equals(AttackTargetBinding other) => Mode == other.Mode && Target == other.Target;
        public override bool Equals(object obj) => obj is AttackTargetBinding other && Equals(other);
        public override int GetHashCode() => ((int)Mode * 397) ^ Target.GetHashCode();
        public static bool operator ==(AttackTargetBinding left, AttackTargetBinding right) => left.Equals(right);
        public static bool operator !=(AttackTargetBinding left, AttackTargetBinding right) => !left.Equals(right);
    }

    public enum AttackDeliveryKind
    {
        None = 0,
        MeleeContact = 1,
        Hitscan = 2,
        ProjectileImpact = 3,
        TravelingArea = 4
    }

    /// <summary>Unity Vector 타입 없이 XZ 평면의 정규화된 방향을 전달하는 값이다.</summary>
    public readonly struct ActionDirectionXZ : IEquatable<ActionDirectionXZ>
    {
        public double X { get; }
        public double Z { get; }
        public bool IsValid { get; }

        private ActionDirectionXZ(double x, double z)
        {
            X = x;
            Z = z;
            IsValid = true;
        }

        public static bool TryCreate(double x, double z, out ActionDirectionXZ direction)
        {
            direction = default;
            if (!ContractNumber.IsFinite(x) || !ContractNumber.IsFinite(z)) return false;
            double lengthSquared = x * x + z * z;
            if (!ContractNumber.IsFinite(lengthSquared) || lengthSquared <= 0d) return false;

            double inverseLength = 1d / Math.Sqrt(lengthSquared);
            direction = new ActionDirectionXZ(x * inverseLength, z * inverseLength);
            return true;
        }

        public bool Equals(ActionDirectionXZ other)
            => IsValid == other.IsValid && X.Equals(other.X) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is ActionDirectionXZ other && Equals(other);
        public override int GetHashCode() => ((X.GetHashCode() * 397) ^ Z.GetHashCode()) * 397 ^ IsValid.GetHashCode();
    }

    /// <summary>Unity Vector 타입 없이 XZ 평면의 월드 위치를 전달하는 값이다.</summary>
    public readonly struct WorldPointXZ : IEquatable<WorldPointXZ>
    {
        public double X { get; }
        public double Z { get; }
        public bool IsValid { get; }

        private WorldPointXZ(double x, double z)
        {
            X = x;
            Z = z;
            IsValid = true;
        }

        public static bool TryCreate(double x, double z, out WorldPointXZ point)
        {
            point = default;
            if (!ContractNumber.IsFinite(x) || !ContractNumber.IsFinite(z)) return false;
            point = new WorldPointXZ(x, z);
            return true;
        }

        public bool Equals(WorldPointXZ other)
            => IsValid == other.IsValid && X.Equals(other.X) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is WorldPointXZ other && Equals(other);
        public override int GetHashCode() => ((X.GetHashCode() * 397) ^ Z.GetHashCode()) * 397 ^ IsValid.GetHashCode();
    }

    /// <summary>
    /// 한 공격 회차의 쿨다운, 회복 종료 오프셋, 타격 오프셋을 검증해 보관한다.
    /// 입력 배열은 복사하므로 생성 뒤 호출자가 원본 배열을 바꿔도 계획은 변하지 않는다.
    /// </summary>
    public sealed class AttackTimelinePlan
    {
        private readonly double[] _impactOffsets;

        public double CooldownSeconds { get; }
        public double RecoveryEndOffset { get; }
        public int ImpactCount => _impactOffsets.Length;
        public ulong AllImpactMask => ImpactCount == 64 ? ulong.MaxValue : (1UL << ImpactCount) - 1UL;

        private AttackTimelinePlan(double cooldownSeconds, double recoveryEndOffset, double[] impactOffsets)
        {
            CooldownSeconds = cooldownSeconds;
            RecoveryEndOffset = recoveryEndOffset;
            _impactOffsets = impactOffsets;
        }

        public double GetImpactOffset(int hitIndex) => _impactOffsets[hitIndex];

        public static bool TryCreate(
            double cooldownSeconds,
            double recoveryEndOffset,
            double[] impactOffsets,
            out AttackTimelinePlan plan)
        {
            plan = null;
            if (!ContractNumber.IsFinite(cooldownSeconds) || cooldownSeconds <= 0d) return false;
            if (!ContractNumber.IsFinite(recoveryEndOffset) || recoveryEndOffset < 0d) return false;
            if (impactOffsets == null || impactOffsets.Length < 1 || impactOffsets.Length > 64) return false;

            var copy = new double[impactOffsets.Length];
            double previous = -1d;
            for (int index = 0; index < impactOffsets.Length; index++)
            {
                double offset = impactOffsets[index];
                if (!ContractNumber.IsFinite(offset) || offset < 0d || offset >= cooldownSeconds) return false;
                if (index > 0 && offset <= previous) return false;
                copy[index] = offset;
                previous = offset;
            }

            if (recoveryEndOffset < copy[copy.Length - 1]) return false;
            plan = new AttackTimelinePlan(cooldownSeconds, recoveryEndOffset, copy);
            return true;
        }
    }

    /// <summary>규칙 v2의 월드 거리 판정을 캡슐화한다.</summary>
    public readonly struct AttackRangeProfile
    {
        public const double MeleeContactDistance = 0.63d;
        public const double RangeEpsilon = 0.05d;
        public const double BuildingTargetRadius = 0.20d;

        public double AttackRange { get; }
        public double TileHeight { get; }
        public bool IsValid { get; }
        public bool UsesMeleeContact => IsValid && AttackRange <= 0.5d;

        private AttackRangeProfile(double attackRange, double tileHeight)
        {
            AttackRange = attackRange;
            TileHeight = tileHeight;
            IsValid = true;
        }

        public static bool TryCreate(double attackRange, double tileHeight, out AttackRangeProfile profile)
        {
            profile = default;
            if (!ContractNumber.IsFinite(attackRange) || attackRange < 0d) return false;
            if (!ContractNumber.IsFinite(tileHeight) || tileHeight <= 0d) return false;

            // 프로필은 생성 시점에 모든 파생 거리 연산까지 검증한다. 이렇게 해야 매우 큰 유한값이
            // 실제 판정 시 Infinity로 바뀌어 사거리가 무제한처럼 동작하는 것을 막을 수 있다.
            double baseDistance = attackRange <= 0.5d
                ? MeleeContactDistance
                : attackRange * tileHeight;
            double unitMaximum = baseDistance + RangeEpsilon;
            double buildingMaximum = unitMaximum + BuildingTargetRadius;
            if (!ContractNumber.IsFinite(baseDistance)
                || !ContractNumber.IsFinite(unitMaximum)
                || !ContractNumber.IsFinite(buildingMaximum)
                || !ContractNumber.IsFinite(unitMaximum * unitMaximum)
                || !ContractNumber.IsFinite(buildingMaximum * buildingMaximum))
                return false;

            profile = new AttackRangeProfile(attackRange, tileHeight);
            return true;
        }

        public double GetMaximumDistance(EntityKind targetKind)
        {
            if (!IsValid || (targetKind != EntityKind.Unit && targetKind != EntityKind.Building))
                return double.NaN;

            double baseDistance = UsesMeleeContact
                ? MeleeContactDistance
                : AttackRange * TileHeight;
            return baseDistance + RangeEpsilon
                + (targetKind == EntityKind.Building ? BuildingTargetRadius : 0d);
        }

        public bool ContainsSquaredDistance(double squaredDistance, EntityKind targetKind)
        {
            if (!ContractNumber.IsFinite(squaredDistance) || squaredDistance < 0d) return false;
            double maximum = GetMaximumDistance(targetKind);
            double maximumSquared = maximum * maximum;
            return ContractNumber.IsFinite(maximumSquared) && squaredDistance <= maximumSquared;
        }
    }

    public enum ImpactAuthorizationOutcome
    {
        None = 0,
        AuthorizedHit = 1,
        AuthorizedMiss = 2
    }

    /// <summary>Reducer가 특정 HitIndex의 판정을 외부 권위 writer에 요청하는 값이다.</summary>
    public readonly struct ImpactAuthorization
    {
        public ulong ActionRevision { get; }
        public AttackResultKey Key { get; }
        public AttackerInstanceId AttackerInstanceId { get; }
        public AttackSequenceId SequenceId { get; }
        public int HitIndex { get; }
        public EntityRef Target { get; }
        public double ImpactServerTime { get; }
        public ActionDirectionXZ AimDirection { get; }
        public ImpactAuthorizationOutcome Outcome { get; }
        public bool IsValid => ActionRevision != 0UL && Key.IsValid
            && AttackerInstanceId.IsValid && SequenceId.IsValid && HitIndex >= 0
            && Target.IsValid && Key.VictimKind == (int)Target.Kind && Key.VictimId == Target.Id
            && ContractNumber.IsFinite(ImpactServerTime) && AimDirection.IsValid
            && Outcome != ImpactAuthorizationOutcome.None;

        internal ImpactAuthorization(
            ulong actionRevision,
            AttackResultKey key,
            EntityRef target,
            double impactServerTime,
            ActionDirectionXZ aimDirection,
            ImpactAuthorizationOutcome outcome)
        {
            ActionRevision = actionRevision;
            Key = key;
            AttackerInstanceId = key.AttackerInstanceId;
            SequenceId = key.SequenceId;
            HitIndex = key.HitIndex;
            Target = target;
            ImpactServerTime = impactServerTime;
            AimDirection = aimDirection;
            Outcome = outcome;
        }
    }

    public enum AttackImpactOutcome
    {
        None = 0,
        HitApplied = 1,
        Miss = 2,
        Cancelled = 3,
        Evaded = 4,
        Immune = 5,
        StatusEffectApplied = 6
    }

    /// <summary>
    /// 외부 서버 권위 writer가 실제 피해 또는 빗나감 결과를 확정한 뒤 만드는 값이다.
    /// Reducer는 성공 피해 결과를 직접 만들지 않고 이 계약을 검증해 확인 상태만 전진시킨다.
    /// </summary>
    public sealed class AttackImpactResult
    {
        public ulong ActionRevision { get; }
        public AttackResultKey Key { get; }
        public double ImpactServerTime { get; }
        public ActionDirectionXZ AuthoritativeAimDirection { get; }
        public bool HasImpactPosition { get; }
        public WorldPointXZ ImpactPosition { get; }
        public AttackImpactOutcome Outcome { get; }
        public int AppliedAmount { get; }
        public int ResultingHp { get; }

        private AttackImpactResult(
            ulong actionRevision,
            AttackResultKey key,
            double impactServerTime,
            ActionDirectionXZ aimDirection,
            bool hasImpactPosition,
            WorldPointXZ impactPosition,
            AttackImpactOutcome outcome,
            int appliedAmount,
            int resultingHp)
        {
            ActionRevision = actionRevision;
            Key = key;
            ImpactServerTime = impactServerTime;
            AuthoritativeAimDirection = aimDirection;
            HasImpactPosition = hasImpactPosition;
            ImpactPosition = impactPosition;
            Outcome = outcome;
            AppliedAmount = appliedAmount;
            ResultingHp = resultingHp;
        }

        public static bool TryCreate(
            ulong actionRevision,
            AttackResultKey key,
            double impactServerTime,
            ActionDirectionXZ aimDirection,
            bool hasImpactPosition,
            WorldPointXZ impactPosition,
            AttackImpactOutcome outcome,
            int appliedAmount,
            int resultingHp,
            out AttackImpactResult result)
        {
            result = null;
            if (actionRevision == 0UL || !key.IsValid
                || !ContractNumber.IsFinite(impactServerTime) || impactServerTime < 0d) return false;
            if (!aimDirection.IsValid || (hasImpactPosition && !impactPosition.IsValid)) return false;
            if (outcome == AttackImpactOutcome.HitApplied)
            {
                if (appliedAmount <= 0 || resultingHp < 0) return false;
            }
            else if (outcome == AttackImpactOutcome.Miss
                || outcome == AttackImpactOutcome.Cancelled
                || outcome == AttackImpactOutcome.Evaded
                || outcome == AttackImpactOutcome.Immune
                || outcome == AttackImpactOutcome.StatusEffectApplied)
            {
                if (appliedAmount != 0 || resultingHp < -1) return false;
            }
            else
            {
                return false;
            }

            result = new AttackImpactResult(
                actionRevision, key, impactServerTime, aimDirection, hasImpactPosition, impactPosition,
                outcome, appliedAmount, resultingHp);
            return true;
        }
    }

    public enum UnitActionEndReason
    {
        None = 0,
        Completed = 1,
        PreCommitCancelled = 2,
        CommittedCancelled = 3,
        AttackerDead = 4
    }

    public enum PreCommitCancelReason
    {
        InvalidTarget = 0,
        TargetDead = 1,
        LoseRangeExited = 2,
        AttackRangeExited = 3
    }

    public enum UnitActionReducerStatus
    {
        Accepted = 0,
        StaleRevision = 1,
        InvalidInput = 2,
        InvalidPhase = 3,
        NoChange = 4,
        UnsupportedDelivery = 5,
        UnsupportedTargetBinding = 6,
        DeadTerminal = 7,
        ScopeMismatch = 8,
        NotDue = 9,
        Duplicate = 10,
        OutOfOrder = 11,
        Exhausted = 12
    }

    /// <summary>
    /// 한 유닛의 현재 서버 행동을 외부에서 변경할 수 없게 복제한 값이다.
    /// 모든 속성은 생성 시 고정되며, reducer가 수락한 관측 가능한 변경마다 Revision이 1 증가한다.
    /// </summary>
    public sealed class UnitActionSnapshot
    {
        public ulong Revision { get; }
        public AttackerInstanceId AttackerInstanceId { get; }
        public int AttackerId { get; }
        public UnitActionPhase Phase { get; }
        public AttackTargetBinding TargetBinding { get; }
        public AttackDeliveryKind Delivery { get; }
        public AttackSequenceId SequenceId { get; }
        public AttackTimelinePlan Timeline { get; }
        public AttackRangeProfile RangeProfile { get; }
        public ActionDirectionXZ SimulationFacing { get; }
        public double PhaseStartServerTime { get; }
        public bool HasSimulationAimDirection { get; }
        public ActionDirectionXZ SimulationAimDirection { get; }
        public double StartServerTime { get; }
        public double CommitServerTime { get; }
        public double CooldownEndServerTime { get; }
        public double RecoveryEndServerTime { get; }
        public bool CooldownConsumed { get; }
        public ulong DueHitMask { get; }
        public ulong DecidedHitMask { get; }
        public ulong ConfirmedHitMask { get; }
        public int LastConfirmedHitIndex { get; }
        public UnitActionEndReason EndReason { get; }

        internal UnitActionSnapshot(
            ulong revision, AttackerInstanceId attackerInstanceId, int attackerId,
            UnitActionPhase phase, AttackTargetBinding targetBinding, AttackDeliveryKind delivery,
            AttackSequenceId sequenceId, AttackTimelinePlan timeline, AttackRangeProfile rangeProfile,
            ActionDirectionXZ simulationFacing, double phaseStartServerTime,
            bool hasSimulationAimDirection, ActionDirectionXZ simulationAimDirection,
            double startServerTime, double commitServerTime,
            double cooldownEndServerTime, double recoveryEndServerTime, bool cooldownConsumed,
            ulong dueHitMask, ulong decidedHitMask, ulong confirmedHitMask,
            int lastConfirmedHitIndex, UnitActionEndReason endReason)
        {
            Revision = revision;
            AttackerInstanceId = attackerInstanceId;
            AttackerId = attackerId;
            Phase = phase;
            TargetBinding = targetBinding;
            Delivery = delivery;
            SequenceId = sequenceId;
            Timeline = timeline;
            RangeProfile = rangeProfile;
            SimulationFacing = simulationFacing;
            PhaseStartServerTime = phaseStartServerTime;
            HasSimulationAimDirection = hasSimulationAimDirection;
            SimulationAimDirection = simulationAimDirection;
            StartServerTime = startServerTime;
            CommitServerTime = commitServerTime;
            CooldownEndServerTime = cooldownEndServerTime;
            RecoveryEndServerTime = recoveryEndServerTime;
            CooldownConsumed = cooldownConsumed;
            DueHitMask = dueHitMask;
            DecidedHitMask = decidedHitMask;
            ConfirmedHitMask = confirmedHitMask;
            LastConfirmedHitIndex = lastConfirmedHitIndex;
            EndReason = endReason;
        }
    }

    internal static class ContractNumber
    {
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
