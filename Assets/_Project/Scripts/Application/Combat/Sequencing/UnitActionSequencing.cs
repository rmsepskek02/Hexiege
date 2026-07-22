using System;
using System.Collections.Generic;

namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>
    /// 서버가 판정하는 유닛 행동의 진행 단계를 나타낸다.
    /// Tracer A에서는 실제 이동이나 공격을 실행하지 않고, 이후 구현이 같은 단계 이름과
    /// 순서를 공유할 수 있도록 순수 C# 계약만 정의한다.
    /// </summary>
    public enum UnitActionPhase
    {
        Idle,
        Navigate,
        AlignToMove,
        Move,
        AcquireTarget,
        Chase,
        AlignToAttack,
        Windup,
        Impact,
        Recovery,
        Dead
    }

    /// <summary>
    /// 전장에 생성된 공격자 한 개체를 구분하는 식별자다.
    /// 같은 UnitData Id가 나중에 재사용되더라도 이전 개체의 공격 결과와 섞이지 않게 하며,
    /// 0은 아직 유효한 개체가 지정되지 않았다는 뜻이다.
    /// </summary>
    public readonly struct AttackerInstanceId : IEquatable<AttackerInstanceId>, IComparable<AttackerInstanceId>
    {
        public static readonly AttackerInstanceId None = new AttackerInstanceId(0UL);

        public ulong Value { get; }

        public AttackerInstanceId(ulong value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0UL;

        public int CompareTo(AttackerInstanceId other) => Value.CompareTo(other.Value);
        public bool Equals(AttackerInstanceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AttackerInstanceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(AttackerInstanceId left, AttackerInstanceId right) => left.Equals(right);
        public static bool operator !=(AttackerInstanceId left, AttackerInstanceId right) => !left.Equals(right);
    }

    /// <summary>
    /// 한 공격자가 시작한 공격 회차를 구분하는 번호다.
    /// 0은 아직 공격 회차가 없다는 뜻이며, 실제 회차는 1부터 시작해 공격자별로 증가한다.
    /// </summary>
    public readonly struct AttackSequenceId : IEquatable<AttackSequenceId>, IComparable<AttackSequenceId>
    {
        public static readonly AttackSequenceId None = new AttackSequenceId(0UL);

        public ulong Value { get; }

        public AttackSequenceId(ulong value)
        {
            Value = value;
        }

        public bool IsValid => Value != 0UL;

        public int CompareTo(AttackSequenceId other) => Value.CompareTo(other.Value);
        public bool Equals(AttackSequenceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AttackSequenceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(AttackSequenceId left, AttackSequenceId right) => left.Equals(right);
        public static bool operator !=(AttackSequenceId left, AttackSequenceId right) => !left.Equals(right);
    }

    /// <summary>
    /// 공격 결과 한 건을 중복 없이 식별하는 복합 키다.
    /// 공격자와 공격 회차뿐 아니라 타격 순번, 피해 대상, 효과 종류, 결과 순번까지 포함하므로
    /// 같은 메시지가 다시 도착해도 동일한 결과인지 판별할 수 있다. 종류 값은 int로 보관해
    /// 이 계약이 Unity, NGO, 화면 표현용 enum에 의존하지 않도록 한다.
    /// </summary>
    public readonly struct AttackResultKey : IEquatable<AttackResultKey>, IComparable<AttackResultKey>
    {
        public AttackerInstanceId AttackerInstanceId { get; }
        public AttackSequenceId SequenceId { get; }
        public int HitIndex { get; }
        public int VictimKind { get; }
        public int VictimId { get; }
        public int EffectKind { get; }
        public int ResultOrdinal { get; }

        public AttackResultKey(
            AttackerInstanceId attackerInstanceId,
            AttackSequenceId sequenceId,
            int hitIndex,
            int victimKind,
            int victimId,
            int effectKind,
            int resultOrdinal)
        {
            AttackerInstanceId = attackerInstanceId;
            SequenceId = sequenceId;
            HitIndex = hitIndex;
            VictimKind = victimKind;
            VictimId = victimId;
            EffectKind = effectKind;
            ResultOrdinal = resultOrdinal;
        }

        public int CompareTo(AttackResultKey other)
        {
            int comparison = AttackerInstanceId.CompareTo(other.AttackerInstanceId);
            if (comparison != 0) return comparison;
            comparison = SequenceId.CompareTo(other.SequenceId);
            if (comparison != 0) return comparison;
            comparison = HitIndex.CompareTo(other.HitIndex);
            if (comparison != 0) return comparison;
            comparison = VictimKind.CompareTo(other.VictimKind);
            if (comparison != 0) return comparison;
            comparison = VictimId.CompareTo(other.VictimId);
            if (comparison != 0) return comparison;
            comparison = EffectKind.CompareTo(other.EffectKind);
            return comparison != 0 ? comparison : ResultOrdinal.CompareTo(other.ResultOrdinal);
        }

        public bool Equals(AttackResultKey other)
        {
            return AttackerInstanceId == other.AttackerInstanceId
                && SequenceId == other.SequenceId
                && HitIndex == other.HitIndex
                && VictimKind == other.VictimKind
                && VictimId == other.VictimId
                && EffectKind == other.EffectKind
                && ResultOrdinal == other.ResultOrdinal;
        }

        public override bool Equals(object obj) => obj is AttackResultKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AttackerInstanceId.GetHashCode();
                hash = (hash * 397) ^ SequenceId.GetHashCode();
                hash = (hash * 397) ^ HitIndex;
                hash = (hash * 397) ^ VictimKind;
                hash = (hash * 397) ^ VictimId;
                hash = (hash * 397) ^ EffectKind;
                return (hash * 397) ^ ResultOrdinal;
            }
        }
    }

    /// <summary>
    /// 방향 오차가 경계 근처에서 흔들릴 때 이동이나 공격 정렬 상태가 매 프레임 바뀌지 않도록
    /// 진입 각도와 이탈 각도를 다르게 적용한다. 이동은 10도에서 진입하고 15도를 넘으면 이탈하며,
    /// 공격 정렬은 5도에서 진입하고 8도를 넘으면 이탈한다.
    /// </summary>
    public static class UnitActionAngleHysteresis
    {
        public const double MoveEnterDegrees = 10d;
        public const double MoveExitDegrees = 15d;
        public const double AttackEnterDegrees = 5d;
        public const double AttackExitDegrees = 8d;

        public static bool AllowsMovement(double yawErrorDegrees, bool isCurrentlyMoving)
        {
            double error = NormalizeError(yawErrorDegrees);
            return error <= (isCurrentlyMoving ? MoveExitDegrees : MoveEnterDegrees);
        }

        public static bool AllowsAttackAlignment(double yawErrorDegrees, bool isCurrentlyAligned)
        {
            double error = NormalizeError(yawErrorDegrees);
            return error <= (isCurrentlyAligned ? AttackExitDegrees : AttackEnterDegrees);
        }

        private static double NormalizeError(double yawErrorDegrees)
        {
            // 방향은 360도 뒤 같은 방향으로 돌아온다. 예를 들어 350도 오차는 실제 최단 회전이
            // 반대 방향 10도이므로, 먼저 0~360도로 접은 뒤 180도를 넘는 값은 360에서 뺀다.
            if (double.IsNaN(yawErrorDegrees) || double.IsInfinity(yawErrorDegrees))
                return double.PositiveInfinity;

            double wrapped = Math.Abs(yawErrorDegrees) % 360d;
            return wrapped > 180d ? 360d - wrapped : wrapped;
        }
    }

    /// <summary>
    /// 공격자마다 독립된 공격 회차 번호를 발급한다.
    /// 같은 공격자에게 발급한 번호는 항상 이전 번호보다 크며, 다른 공격자의 번호에는 영향을 주지 않는다.
    /// </summary>
    public sealed class AttackSequenceAllocator
    {
        private readonly Dictionary<AttackerInstanceId, ulong> _lastByAttacker
            = new Dictionary<AttackerInstanceId, ulong>();

        public AttackSequenceId Next(AttackerInstanceId attackerInstanceId)
        {
            if (!attackerInstanceId.IsValid)
                throw new ArgumentException("공격 회차를 발급하려면 유효한 공격자 개체 식별자가 필요하다.", nameof(attackerInstanceId));

            _lastByAttacker.TryGetValue(attackerInstanceId, out ulong last);
            if (last == ulong.MaxValue)
                throw new InvalidOperationException($"공격자 개체 {attackerInstanceId}의 공격 회차 번호를 더 발급할 수 없다.");

            ulong next = last + 1UL;
            _lastByAttacker[attackerInstanceId] = next;
            return new AttackSequenceId(next);
        }

        /// <summary>더 이상 존재하지 않는 공격자 개체의 회차 상태를 제거한다.</summary>
        public bool Forget(AttackerInstanceId attackerInstanceId)
        {
            return attackerInstanceId.IsValid && _lastByAttacker.Remove(attackerInstanceId);
        }

        /// <summary>모든 공격자 개체의 회차 상태를 제거한다.</summary>
        public void Clear()
        {
            _lastByAttacker.Clear();
        }
    }

    /// <summary>공격 결과를 버퍼에 넣지 못한 이유 또는 정상 수락 여부를 나타낸다.</summary>
    public enum AttackResultBufferAddStatus
    {
        Accepted,
        Duplicate,
        ScopeMismatch,
        CapacityExceeded,
        Expired
    }

    /// <summary>
    /// 공격자 개체 한 명의 공격 회차 하나에 속한 결과만 잠시 보관한다.
    /// 네트워크 결과가 중복되거나 순서가 뒤바뀌어 도착해도 결과 키를 한 번만 보관하고 정렬하며,
    /// 메모리가 끝없이 증가하지 않도록 최대 64건과 생성 후 2초 제한을 강제한다.
    /// 호출자는 모든 참가자가 공유하는 동기화 서버 시간을 초 단위로 전달해야 한다.
    /// 이 클래스는 Tracer A의 미사용 Shadow 계약으로 피해나 VFX를 발생시키지 않는다.
    /// </summary>
    public sealed class AttackResultOrderBuffer
    {
        public const int MaximumResultCount = 64;
        public const double MaximumAgeSeconds = 2d;

        private readonly AttackerInstanceId _attackerInstanceId;
        private readonly AttackSequenceId _sequenceId;
        private readonly double _expiresAtServerTime;
        private readonly HashSet<AttackResultKey> _seen = new HashSet<AttackResultKey>();
        private readonly List<AttackResultKey> _ordered = new List<AttackResultKey>();

        public AttackerInstanceId AttackerInstanceId => _attackerInstanceId;
        public AttackSequenceId SequenceId => _sequenceId;
        public int Count => _ordered.Count;

        public AttackResultOrderBuffer(
            AttackerInstanceId attackerInstanceId,
            AttackSequenceId sequenceId,
            double openedAtServerTime)
        {
            if (!attackerInstanceId.IsValid)
                throw new ArgumentException("결과 버퍼에는 유효한 공격자 개체 식별자가 필요하다.", nameof(attackerInstanceId));
            if (!sequenceId.IsValid)
                throw new ArgumentException("결과 버퍼에는 유효한 공격 회차가 필요하다.", nameof(sequenceId));
            if (double.IsNaN(openedAtServerTime) || double.IsInfinity(openedAtServerTime))
                throw new ArgumentOutOfRangeException(nameof(openedAtServerTime), "서버 시간은 유한한 값이어야 한다.");
            double expiresAtServerTime = openedAtServerTime + MaximumAgeSeconds;
            if (double.IsNaN(expiresAtServerTime) || double.IsInfinity(expiresAtServerTime))
                throw new ArgumentOutOfRangeException(nameof(openedAtServerTime), "만료 시각도 유한한 값이어야 한다.");

            _attackerInstanceId = attackerInstanceId;
            _sequenceId = sequenceId;
            _expiresAtServerTime = expiresAtServerTime;
        }

        public AttackResultBufferAddStatus TryAdd(AttackResultKey key, double synchronizedServerTime)
        {
            if (DiscardIfExpired(synchronizedServerTime))
                return AttackResultBufferAddStatus.Expired;
            if (key.AttackerInstanceId != _attackerInstanceId || key.SequenceId != _sequenceId)
                return AttackResultBufferAddStatus.ScopeMismatch;
            if (_seen.Contains(key))
                return AttackResultBufferAddStatus.Duplicate;
            if (_ordered.Count >= MaximumResultCount)
                return AttackResultBufferAddStatus.CapacityExceeded;

            _seen.Add(key);

            int insertAt = _ordered.BinarySearch(key);
            if (insertAt < 0) insertAt = ~insertAt;
            _ordered.Insert(insertAt, key);
            return AttackResultBufferAddStatus.Accepted;
        }

        /// <summary>
        /// 동기화 서버 시간이 2초 수명 경계에 도달했으면 보관 중인 결과를 실제로 폐기한다.
        /// 폐기가 일어났으면 true를 반환한다.
        /// </summary>
        public bool DiscardIfExpired(double synchronizedServerTime)
        {
            if (double.IsNaN(synchronizedServerTime) || double.IsInfinity(synchronizedServerTime))
                throw new ArgumentOutOfRangeException(nameof(synchronizedServerTime), "서버 시간은 유한한 값이어야 한다.");
            if (synchronizedServerTime < _expiresAtServerTime)
                return false;

            Clear();
            return true;
        }

        public AttackResultKey GetAt(int index) => _ordered[index];

        public void Clear()
        {
            _seen.Clear();
            _ordered.Clear();
        }
    }
}
