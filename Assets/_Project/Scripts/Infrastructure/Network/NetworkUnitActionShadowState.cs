using System;
using System.Collections.Generic;
using Hexiege.Application.Combat.Sequencing;
using Unity.Netcode;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// reducer의 의미 상태와 그 전이를 판정할 때 사용한 최신 서버 pose를 결합한 순수 게시 계약이다.
    /// pose 변화 자체는 revision을 만들지 않으며 Commit/Evaluate/Cancel/Dead 같은 reducer 전이가
    /// 이미 만든 revision을 게시할 때만 함께 투영한다.
    /// </summary>
    public readonly struct UnitAttackShadowPublication
    {
        public UnitActionSnapshot Snapshot { get; }
        public ActionDirectionXZ SimulationFacing { get; }
        public int LastEvaluatedHitIndex { get; }

        public bool IsValid => Snapshot != null
            && SimulationFacing.IsValid
            && LastEvaluatedHitIndex >= -1;

        public UnitAttackShadowPublication(
            UnitActionSnapshot snapshot,
            ActionDirectionXZ simulationFacing,
            int lastEvaluatedHitIndex)
        {
            Snapshot = snapshot;
            SimulationFacing = simulationFacing;
            LastEvaluatedHitIndex = lastEvaluatedHitIndex;
        }
    }

    /// <summary>
    /// UnitActionSnapshot을 NGO에 직접 직렬화하지 않고, Tracer C에 필요한 원시 값만 한 번에
    /// 복제하는 원자 상태다. 하나의 NetworkVariable 값이므로 target/phase/revision이 서로 다른
    /// 프레임의 값으로 조합되지 않는다. 클라이언트는 관측만 하며 게임플레이에 적용하지 않는다.
    /// </summary>
    public struct NetworkUnitActionShadowState : INetworkSerializable, IEquatable<NetworkUnitActionShadowState>
    {
        public ulong AttackerInstanceId;
        public ulong SequenceId;
        public ulong Revision;
        public byte Phase;
        public byte TargetKind;
        public int TargetId;
        public byte Delivery;
        public double CommitServerTime;
        public float SimulationFacingX;
        public float SimulationFacingZ;
        public byte HasImpactAim;
        public float ImpactAimX;
        public float ImpactAimZ;
        public int LastImpactHitIndex;

        public bool IsEmpty => AttackerInstanceId == 0UL && Revision == 0UL;

        public bool IsValid
        {
            get
            {
                if (AttackerInstanceId == 0UL || Revision == 0UL) return false;
                if (!Enum.IsDefined(typeof(UnitActionPhase), (int)Phase)) return false;
                if (!Enum.IsDefined(typeof(EntityKind), (int)TargetKind)) return false;
                if (!Enum.IsDefined(typeof(AttackDeliveryKind), (int)Delivery)) return false;
                if (double.IsNaN(CommitServerTime) || double.IsInfinity(CommitServerTime)) return false;
                if (!IsFinite(SimulationFacingX) || !IsFinite(SimulationFacingZ)) return false;
                if (HasImpactAim > 1 || !IsFinite(ImpactAimX) || !IsFinite(ImpactAimZ)) return false;
                if (TargetKind == (byte)EntityKind.None)
                    return TargetId == 0;
                return TargetId >= 0;
            }
        }

        public static bool TryCreate(
            UnitAttackShadowPublication publication,
            out NetworkUnitActionShadowState state)
        {
            state = default;
            if (!publication.IsValid) return false;
            UnitActionSnapshot snapshot = publication.Snapshot;
            if (snapshot == null || !snapshot.AttackerInstanceId.IsValid || snapshot.Revision == 0UL)
                return false;

            AttackTargetBinding target = snapshot.TargetBinding;
            ActionDirectionXZ facing = publication.SimulationFacing;
            ActionDirectionXZ aim = snapshot.SimulationAimDirection;
            state = new NetworkUnitActionShadowState
            {
                AttackerInstanceId = snapshot.AttackerInstanceId.Value,
                SequenceId = snapshot.SequenceId.Value,
                Revision = snapshot.Revision,
                Phase = (byte)snapshot.Phase,
                TargetKind = (byte)(target.IsValid ? target.Target.Kind : EntityKind.None),
                TargetId = target.IsValid ? target.Target.Id : 0,
                Delivery = (byte)snapshot.Delivery,
                CommitServerTime = snapshot.CommitServerTime,
                SimulationFacingX = facing.IsValid ? (float)facing.X : 0f,
                SimulationFacingZ = facing.IsValid ? (float)facing.Z : 0f,
                HasImpactAim = snapshot.HasSimulationAimDirection ? (byte)1 : (byte)0,
                ImpactAimX = snapshot.HasSimulationAimDirection ? (float)aim.X : 0f,
                ImpactAimZ = snapshot.HasSimulationAimDirection ? (float)aim.Z : 0f,
                LastImpactHitIndex = publication.LastEvaluatedHitIndex
            };
            return state.IsValid;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref AttackerInstanceId);
            serializer.SerializeValue(ref SequenceId);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref TargetKind);
            serializer.SerializeValue(ref TargetId);
            serializer.SerializeValue(ref Delivery);
            serializer.SerializeValue(ref CommitServerTime);
            serializer.SerializeValue(ref SimulationFacingX);
            serializer.SerializeValue(ref SimulationFacingZ);
            serializer.SerializeValue(ref HasImpactAim);
            serializer.SerializeValue(ref ImpactAimX);
            serializer.SerializeValue(ref ImpactAimZ);
            serializer.SerializeValue(ref LastImpactHitIndex);
        }

        public bool Equals(NetworkUnitActionShadowState other)
        {
            return AttackerInstanceId == other.AttackerInstanceId
                && SequenceId == other.SequenceId
                && Revision == other.Revision
                && Phase == other.Phase
                && TargetKind == other.TargetKind
                && TargetId == other.TargetId
                && Delivery == other.Delivery
                && CommitServerTime.Equals(other.CommitServerTime)
                && SimulationFacingX.Equals(other.SimulationFacingX)
                && SimulationFacingZ.Equals(other.SimulationFacingZ)
                && HasImpactAim == other.HasImpactAim
                && ImpactAimX.Equals(other.ImpactAimX)
                && ImpactAimZ.Equals(other.ImpactAimZ)
                && LastImpactHitIndex == other.LastImpactHitIndex;
        }

        public override bool Equals(object obj)
            => obj is NetworkUnitActionShadowState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AttackerInstanceId.GetHashCode();
                hash = (hash * 397) ^ SequenceId.GetHashCode();
                hash = (hash * 397) ^ Revision.GetHashCode();
                hash = (hash * 397) ^ Phase.GetHashCode();
                hash = (hash * 397) ^ TargetId;
                return (hash * 397) ^ LastImpactHitIndex;
            }
        }

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 현재 상태 Snapshot과 분리해 Reliable 메시지로 보내는 개별 공격 결과다.
    /// 전체 AttackResultKey를 포함하므로 다중 hit·겹친 회차에서도 결과를 서로 바꾸어 쓸 수 없다.
    /// Client는 이 값을 표현이나 HP에 적용하지 않고 Shadow observer에만 전달한다.
    /// </summary>
    public struct NetworkAttackImpactShadowResult
        : INetworkSerializable, IEquatable<NetworkAttackImpactShadowResult>
    {
        public ulong AttackerInstanceId;
        public ulong SequenceId;
        public ulong ActionRevision;
        public int HitIndex;
        public int VictimKind;
        public int VictimId;
        public int EffectKind;
        public int ResultOrdinal;
        public byte Delivery;
        public double ImpactServerTime;
        public float AimX;
        public float AimZ;
        public byte HasImpactPosition;
        public float ImpactPositionX;
        public float ImpactPositionZ;
        public byte Outcome;
        public int AppliedAmount;
        public int ResultingHp;
        public int VictimPresentationType;
        public int VictimTeam;

        public AttackResultKey Key => new AttackResultKey(
            new AttackerInstanceId(AttackerInstanceId),
            new AttackSequenceId(SequenceId),
            HitIndex, VictimKind, VictimId, EffectKind, ResultOrdinal);

        public bool IsValid
        {
            get
            {
                if (!Key.IsValid || ActionRevision == 0UL) return false;
                if (!Enum.IsDefined(typeof(AttackDeliveryKind), (int)Delivery)
                    || Delivery == (byte)AttackDeliveryKind.None) return false;
                if (!Enum.IsDefined(typeof(AttackImpactOutcome), (int)Outcome)
                    || Outcome == (byte)AttackImpactOutcome.None) return false;
                if (!IsFinite(ImpactServerTime) || ImpactServerTime < 0d
                    || !IsFinite(AimX) || !IsFinite(AimZ)
                    || HasImpactPosition > 1
                    || !IsFinite(ImpactPositionX) || !IsFinite(ImpactPositionZ)) return false;
                double aimLengthSquared = (double)AimX * AimX + (double)AimZ * AimZ;
                if (aimLengthSquared < 0.999d || aimLengthSquared > 1.001d) return false;
                AttackImpactOutcome outcome = (AttackImpactOutcome)Outcome;
                if (outcome == AttackImpactOutcome.HitApplied)
                    return AppliedAmount > 0 && ResultingHp >= 0;
                return AppliedAmount == 0 && ResultingHp >= -1;
            }
        }

        public static bool TryCreate(
            AttackImpactResult result,
            AttackDeliveryKind delivery,
            out NetworkAttackImpactShadowResult state)
        {
            state = default;
            if (result == null || !result.Key.IsValid
                || delivery == AttackDeliveryKind.None) return false;
            AttackResultKey key = result.Key;
            state = new NetworkAttackImpactShadowResult
            {
                AttackerInstanceId = key.AttackerInstanceId.Value,
                SequenceId = key.SequenceId.Value,
                ActionRevision = result.ActionRevision,
                HitIndex = key.HitIndex,
                VictimKind = key.VictimKind,
                VictimId = key.VictimId,
                EffectKind = key.EffectKind,
                ResultOrdinal = key.ResultOrdinal,
                Delivery = (byte)delivery,
                ImpactServerTime = result.ImpactServerTime,
                AimX = (float)result.AuthoritativeAimDirection.X,
                AimZ = (float)result.AuthoritativeAimDirection.Z,
                HasImpactPosition = result.HasImpactPosition ? (byte)1 : (byte)0,
                ImpactPositionX = result.HasImpactPosition ? (float)result.ImpactPosition.X : 0f,
                ImpactPositionZ = result.HasImpactPosition ? (float)result.ImpactPosition.Z : 0f,
                Outcome = (byte)result.Outcome,
                AppliedAmount = result.AppliedAmount,
                ResultingHp = result.ResultingHp,
                // 개별 C2 shadow 결과는 표현 완결 묶음이 아니다. 음수 sentinel로
                // 명확히 구분하고 C3 adapter에는 완료 묶음만 전달한다.
                VictimPresentationType = -1,
                VictimTeam = -1
            };
            return state.IsValid;
        }

        public bool TryToPresentationInput(
            int attackerUnitId,
            out AttackResultPresentationInput input)
        {
            input = default;
            if (!IsValid
                || !ActionDirectionXZ.TryCreate(AimX, AimZ, out ActionDirectionXZ aim))
                return false;
            WorldPointXZ impact = default;
            if (HasImpactPosition != 0
                && !WorldPointXZ.TryCreate(ImpactPositionX, ImpactPositionZ, out impact))
                return false;
            input = new AttackResultPresentationInput(
                attackerUnitId, ActionRevision, Key, (AttackDeliveryKind)Delivery,
                ImpactServerTime, aim, HasImpactPosition != 0, impact,
                (AttackImpactOutcome)Outcome, AppliedAmount, ResultingHp,
                VictimPresentationType, VictimTeam);
            return input.IsValid;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref AttackerInstanceId);
            serializer.SerializeValue(ref SequenceId);
            serializer.SerializeValue(ref ActionRevision);
            serializer.SerializeValue(ref HitIndex);
            serializer.SerializeValue(ref VictimKind);
            serializer.SerializeValue(ref VictimId);
            serializer.SerializeValue(ref EffectKind);
            serializer.SerializeValue(ref ResultOrdinal);
            serializer.SerializeValue(ref Delivery);
            serializer.SerializeValue(ref ImpactServerTime);
            serializer.SerializeValue(ref AimX);
            serializer.SerializeValue(ref AimZ);
            serializer.SerializeValue(ref HasImpactPosition);
            serializer.SerializeValue(ref ImpactPositionX);
            serializer.SerializeValue(ref ImpactPositionZ);
            serializer.SerializeValue(ref Outcome);
            serializer.SerializeValue(ref AppliedAmount);
            serializer.SerializeValue(ref ResultingHp);
            serializer.SerializeValue(ref VictimPresentationType);
            serializer.SerializeValue(ref VictimTeam);
        }

        public bool Equals(NetworkAttackImpactShadowResult other)
        {
            return AttackerInstanceId == other.AttackerInstanceId
                && SequenceId == other.SequenceId
                && ActionRevision == other.ActionRevision
                && HitIndex == other.HitIndex
                && VictimKind == other.VictimKind
                && VictimId == other.VictimId
                && EffectKind == other.EffectKind
                && ResultOrdinal == other.ResultOrdinal
                && Delivery == other.Delivery
                && ImpactServerTime.Equals(other.ImpactServerTime)
                && AimX.Equals(other.AimX) && AimZ.Equals(other.AimZ)
                && HasImpactPosition == other.HasImpactPosition
                && ImpactPositionX.Equals(other.ImpactPositionX)
                && ImpactPositionZ.Equals(other.ImpactPositionZ)
                && Outcome == other.Outcome
                && AppliedAmount == other.AppliedAmount
                && ResultingHp == other.ResultingHp
                && VictimPresentationType == other.VictimPresentationType
                && VictimTeam == other.VictimTeam;
        }

        public override bool Equals(object obj)
            => obj is NetworkAttackImpactShadowResult other && Equals(other);
        public override int GetHashCode() => Key.GetHashCode();

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public enum UnitAttackShadowImpactReplicationStatus : byte
    {
        Accepted = 0,
        Duplicate = 1,
        Conflict = 2,
        Invalid = 3,
        InstanceRetired = 4
    }

    /// <summary>
    /// Reliable 결과의 멱등 키를 제한된 메모리로 분류한다. 결과 완료 순서는 공격 회차 순서와
    /// 다를 수 있으므로 sequence 단조 증가를 요구하지 않고 NetworkObject 수명만 엄격히 고정한다.
    /// </summary>
    public sealed class UnitAttackShadowImpactReplicationClassifier
    {
        public const int MaximumRememberedResults = 256;

        private readonly Dictionary<AttackResultKey, NetworkAttackImpactShadowResult> _accepted
            = new Dictionary<AttackResultKey, NetworkAttackImpactShadowResult>();
        private readonly Queue<AttackResultKey> _order = new Queue<AttackResultKey>();
        private ulong _networkObjectId;
        private ulong _attackerInstanceId;
        private bool _hasScope;
        private bool _retired;

        public UnitAttackShadowImpactReplicationStatus Classify(
            ulong networkObjectId,
            NetworkAttackImpactShadowResult result)
        {
            if (_retired) return UnitAttackShadowImpactReplicationStatus.InstanceRetired;
            if (!result.IsValid) return UnitAttackShadowImpactReplicationStatus.Invalid;
            if (!_hasScope)
            {
                _networkObjectId = networkObjectId;
                _attackerInstanceId = result.AttackerInstanceId;
                _hasScope = true;
            }
            else if (_networkObjectId != networkObjectId
                || _attackerInstanceId != result.AttackerInstanceId)
            {
                return UnitAttackShadowImpactReplicationStatus.InstanceRetired;
            }

            AttackResultKey key = result.Key;
            if (_accepted.TryGetValue(key, out NetworkAttackImpactShadowResult previous))
            {
                return previous.Equals(result)
                    ? UnitAttackShadowImpactReplicationStatus.Duplicate
                    : UnitAttackShadowImpactReplicationStatus.Conflict;
            }

            _accepted.Add(key, result);
            _order.Enqueue(key);
            while (_accepted.Count > MaximumRememberedResults)
                _accepted.Remove(_order.Dequeue());
            return UnitAttackShadowImpactReplicationStatus.Accepted;
        }

        public void Retire() => _retired = true;
    }

    public enum UnitAttackShadowReplicationStatus : byte
    {
        Accepted = 0,
        Duplicate = 1,
        Invalid = 2,
        StaleRevision = 3,
        SequenceRegression = 4,
        InstanceRetired = 5
    }

    /// <summary>
    /// NGO 콜백 순서와 무관하게 복제 상태를 단조 분류하는 순수 분류기다.
    /// retire된 NetworkObject 수명에서 늦게 온 패킷과 새 수명을 섞지 않는다.
    /// </summary>
    public sealed class UnitAttackShadowReplicationClassifier
    {
        private ulong _networkObjectId;
        private ulong _attackerInstanceId;
        private ulong _sequenceId;
        private ulong _sequenceRevision;
        private NetworkUnitActionShadowState _lastAcceptedState;
        private bool _hasAccepted;
        private bool _sequenceClosed;
        private bool _retired;

        public UnitAttackShadowReplicationStatus Classify(
            ulong networkObjectId,
            NetworkUnitActionShadowState state)
        {
            if (_retired) return UnitAttackShadowReplicationStatus.InstanceRetired;
            // NGO의 첫 NetworkObjectId는 0일 수 있으므로 0 자체를 invalid로 취급하지 않는다.
            if (!state.IsValid)
                return UnitAttackShadowReplicationStatus.Invalid;

            if (!_hasAccepted)
            {
                Accept(networkObjectId, state);
                return UnitAttackShadowReplicationStatus.Accepted;
            }

            if (networkObjectId != _networkObjectId
                || state.AttackerInstanceId != _attackerInstanceId)
                return UnitAttackShadowReplicationStatus.InstanceRetired;

            bool isNeutral = IsNeutralPhase(state);
            if (isNeutral)
            {
                if (state.Equals(_lastAcceptedState))
                    return UnitAttackShadowReplicationStatus.Duplicate;

                // Neutral은 다음 공격 후보의 준비 상태다. 독립 reducer의 revision을 가지므로
                // 직전 양수 회차 revision과 비교하지 않고 수락하되 발급 sequence 상한은 보존한다.
                Accept(networkObjectId, state);
                return UnitAttackShadowReplicationStatus.Accepted;
            }

            // SequenceId=0인 비-neutral payload는 어떠한 공격 회차에도 속하지 않는다.
            if (state.SequenceId == 0UL || state.SequenceId < _sequenceId)
                return UnitAttackShadowReplicationStatus.SequenceRegression;

            // 새 양수 회차가 시작되면 revision은 그 회차의 reducer에서 다시 시작할 수 있다.
            // 따라서 공격자 수명 전체 revision보다 sequence 증가를 먼저 판정한다.
            if (state.SequenceId > _sequenceId)
            {
                Accept(networkObjectId, state);
                return UnitAttackShadowReplicationStatus.Accepted;
            }

            // 여기부터는 같은 양수 회차 안에서만 revision 단조성을 판정한다.
            if (state.Revision < _sequenceRevision)
                return UnitAttackShadowReplicationStatus.StaleRevision;
            if (state.Revision == _sequenceRevision)
            {
                if (state.Equals(_lastAcceptedState))
                    return UnitAttackShadowReplicationStatus.Duplicate;
                return UnitAttackShadowReplicationStatus.SequenceRegression;
            }
            // Neutral로 닫힌 양수 회차를 늦은 패킷이 다시 열지 못하게 한다.
            if (_sequenceClosed)
                return UnitAttackShadowReplicationStatus.SequenceRegression;

            Accept(networkObjectId, state);
            return UnitAttackShadowReplicationStatus.Accepted;
        }

        public void Retire() => _retired = true;

        public bool TryGetLastAcceptedState(out NetworkUnitActionShadowState state)
        {
            state = _lastAcceptedState;
            return _hasAccepted;
        }

        private void Accept(ulong networkObjectId, NetworkUnitActionShadowState state)
        {
            _networkObjectId = networkObjectId;
            _attackerInstanceId = state.AttackerInstanceId;
            _lastAcceptedState = state;
            if (state.SequenceId > _sequenceId)
            {
                _sequenceId = state.SequenceId;
                _sequenceRevision = state.Revision;
                _sequenceClosed = false;
            }
            else if (state.SequenceId == _sequenceId && state.SequenceId > 0UL)
            {
                _sequenceRevision = state.Revision;
                _sequenceClosed = false;
            }
            else if (IsNeutralPhase(state) && _sequenceId > 0UL)
            {
                _sequenceClosed = true;
            }
            _hasAccepted = true;
        }

        private static bool IsNeutralPhase(NetworkUnitActionShadowState state)
        {
            UnitActionPhase phase = (UnitActionPhase)state.Phase;
            return state.SequenceId == 0UL
                && (phase == UnitActionPhase.Idle
                    || phase == UnitActionPhase.AcquireTarget
                    || phase == UnitActionPhase.Chase
                    || phase == UnitActionPhase.AlignToAttack);
        }
    }
}
