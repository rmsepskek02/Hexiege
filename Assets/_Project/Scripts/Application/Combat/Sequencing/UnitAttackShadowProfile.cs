using System;

namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>
    /// Tracer C가 한 유닛 타입의 공격을 어디까지 안전하게 관측할 수 있는지 나타낸다.
    /// Supported만 UnitActionSequencer에 연결하며, 미완료 공격은 조용히 누락하지 않고
    /// Unresolved 또는 NotApplicableAttack으로 명시한다.
    /// </summary>
    public enum UnitAttackShadowSupport : byte
    {
        None = 0,
        Supported = 1,
        Unresolved = 2,
        NotApplicableAttack = 3
    }

    /// <summary>
    /// C3 정규 채점 대상과 의도적으로 미완성인 manifest coverage를 구분한다.
    /// KnownUnresolved/NotApplicable은 Legacy gameplay나 VFX를 끄지 않고 audit 비교만
    /// 제외한다. Supported 행의 runtime 계약 누락은 RequiredButMissing으로 남아 FAIL이다.
    /// </summary>
    public enum AttackPresentationAuditEligibility : byte
    {
        None = 0,
        Required = 1,
        KnownUnresolved = 2,
        NotApplicable = 3,
        RequiredButMissing = 4
    }

    /// <summary>
    /// 현재 Legacy 코드가 공격 결과를 만드는 방식이다. 목표 Delivery와 분리해 기록하므로
    /// 아직 투사체가 없는 원거리 유닛을 Hitscan으로 잘못 확정하지 않는다.
    /// </summary>
    public enum LegacyAttackExecutionKind : byte
    {
        None = 0,
        TimerImpact = 1,
        TimerImpactWithSecondaryEffect = 2,
        ActiveTravelingArea = 3,
        SeparateHealPipeline = 4
    }

    /// <summary>
    /// 25종 manifest의 한 행이다. Unity, NGO, Animator를 참조하지 않는 순수 값 계약이며
    /// 실제 UnitType enum은 Infrastructure resolver가 UnitTypeValue로 변환한다.
    /// </summary>
    public readonly struct UnitAttackShadowProfile : IEquatable<UnitAttackShadowProfile>
    {
        public int UnitTypeValue { get; }
        public string UnitTypeName { get; }
        public AttackTargetMode TargetMode { get; }
        public AttackDeliveryKind Delivery { get; }
        public LegacyAttackExecutionKind LegacyExecution { get; }
        public UnitAttackShadowSupport Support { get; }
        public int ExpectedImpactCount { get; }
        public bool HasSecondaryResults { get; }
        public string Reason { get; }

        public bool IsValid => UnitTypeValue >= 0
            && !string.IsNullOrWhiteSpace(UnitTypeName)
            && Enum.IsDefined(typeof(AttackTargetMode), TargetMode)
            && Enum.IsDefined(typeof(AttackDeliveryKind), Delivery)
            && Enum.IsDefined(typeof(LegacyAttackExecutionKind), LegacyExecution)
            && Enum.IsDefined(typeof(UnitAttackShadowSupport), Support)
            && Support != UnitAttackShadowSupport.None
            && ExpectedImpactCount >= 0
            && !string.IsNullOrWhiteSpace(Reason);

        public bool CanRunSequencer => IsValid
            && Support == UnitAttackShadowSupport.Supported
            && TargetMode == AttackTargetMode.TargetLocked
            && (Delivery == AttackDeliveryKind.MeleeContact
                || Delivery == AttackDeliveryKind.Hitscan)
            && ExpectedImpactCount > 0;

        public UnitAttackShadowProfile(
            int unitTypeValue,
            string unitTypeName,
            AttackTargetMode targetMode,
            AttackDeliveryKind delivery,
            LegacyAttackExecutionKind legacyExecution,
            UnitAttackShadowSupport support,
            int expectedImpactCount,
            bool hasSecondaryResults,
            string reason)
        {
            UnitTypeValue = unitTypeValue;
            UnitTypeName = unitTypeName;
            TargetMode = targetMode;
            Delivery = delivery;
            LegacyExecution = legacyExecution;
            Support = support;
            ExpectedImpactCount = expectedImpactCount;
            HasSecondaryResults = hasSecondaryResults;
            Reason = reason;
        }

        public bool Equals(UnitAttackShadowProfile other)
        {
            return UnitTypeValue == other.UnitTypeValue
                && UnitTypeName == other.UnitTypeName
                && TargetMode == other.TargetMode
                && Delivery == other.Delivery
                && LegacyExecution == other.LegacyExecution
                && Support == other.Support
                && ExpectedImpactCount == other.ExpectedImpactCount
                && HasSecondaryResults == other.HasSecondaryResults
                && Reason == other.Reason;
        }

        public override bool Equals(object obj)
            => obj is UnitAttackShadowProfile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = UnitTypeValue;
                hash = (hash * 397) ^ (UnitTypeName?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (int)TargetMode;
                hash = (hash * 397) ^ (int)Delivery;
                hash = (hash * 397) ^ (int)LegacyExecution;
                hash = (hash * 397) ^ (int)Support;
                hash = (hash * 397) ^ ExpectedImpactCount;
                return (hash * 397) ^ HasSecondaryResults.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Legacy 피해 예약과 Shadow 공격 회차의 상관관계를 분리하는 토큰이다.
    /// 이 번호는 AttackSequenceId가 아니며 정렬 성공 여부와 무관하게 Legacy 예약만 식별한다.
    /// </summary>
    public readonly struct LegacyAttackToken : IEquatable<LegacyAttackToken>
    {
        public static readonly LegacyAttackToken None = new LegacyAttackToken(AttackerInstanceId.None, 0UL);

        public AttackerInstanceId AttackerInstanceId { get; }
        public ulong Value { get; }
        public bool IsValid => AttackerInstanceId.IsValid && Value != 0UL;

        public LegacyAttackToken(AttackerInstanceId attackerInstanceId, ulong value)
        {
            AttackerInstanceId = attackerInstanceId;
            Value = value;
        }

        public bool Equals(LegacyAttackToken other)
            => AttackerInstanceId == other.AttackerInstanceId && Value == other.Value;
        public override bool Equals(object obj) => obj is LegacyAttackToken other && Equals(other);
        public override int GetHashCode() => (AttackerInstanceId.GetHashCode() * 397) ^ Value.GetHashCode();
        public static bool operator ==(LegacyAttackToken left, LegacyAttackToken right) => left.Equals(right);
        public static bool operator !=(LegacyAttackToken left, LegacyAttackToken right) => !left.Equals(right);
    }
}
