using System;
using System.Collections.Generic;

namespace Hexiege.Application.Combat.Sequencing
{
    public enum PresentationShadowResultStatus
    {
        Scheduled = 0,
        CatchUp = 1,
        Matched = 2,
        Duplicate = 3,
        Conflict = 4,
        Invalid = 5,
        Expired = 6,
        Unmatched = 7,
        Ambiguous = 8,
        InstanceRetired = 9
    }

    public enum LegacyPresentationObservationKind
    {
        Enqueued = 0,
        Marker = 1,
        TracerImpact = 2,
        EmittedMarker = 3,
        EmittedImmediate = 4,
        EmittedTimeout = 5,
        EmittedTargetDeath = 6,
        EmittedAttackerStop = 7,
        StructuralInvalid = 8,
        StructuralCapacityExceeded = 9
    }

    /// <summary>
    /// Animation marker가 발생한 바로 그 순간 Client가 보유하던 공격 복제 상태다.
    /// 나중에 최신 NetworkVariable을 다시 읽으면 서로 다른 회차가 섞일 수 있으므로,
    /// Unity/NGO 타입 없이 값만 복사해 Legacy 관측과 함께 보존한다.
    /// </summary>
    public readonly struct AttackPresentationReplicatedSnapshot
    {
        public ulong AttackerInstanceId { get; }
        public ulong SequenceId { get; }
        public ulong Revision { get; }
        public bool IsValid => AttackerInstanceId != 0UL
            && SequenceId != 0UL && Revision != 0UL;

        public AttackPresentationReplicatedSnapshot(
            ulong attackerInstanceId,
            ulong sequenceId,
            ulong revision)
        {
            AttackerInstanceId = attackerInstanceId;
            SequenceId = sequenceId;
            Revision = revision;
        }
    }

    /// <summary>
    /// 한 Animation marker가 어느 서버 권위 타격에 속하는지 나타내는 최소 scope다.
    /// 한 marker가 AoE의 여러 피해자 결과를 만들 수 있으므로 피해자/효과까지 포함하는
    /// AttackResultKey와 분리한다.
    /// </summary>
    public readonly struct AttackPresentationScope : IEquatable<AttackPresentationScope>
    {
        public AttackerInstanceId AttackerInstanceId { get; }
        public AttackSequenceId SequenceId { get; }
        public int HitIndex { get; }
        public bool IsValid => AttackerInstanceId.IsValid && SequenceId.IsValid && HitIndex >= 0;

        public AttackPresentationScope(
            AttackerInstanceId attackerInstanceId,
            AttackSequenceId sequenceId,
            int hitIndex)
        {
            AttackerInstanceId = attackerInstanceId;
            SequenceId = sequenceId;
            HitIndex = hitIndex;
        }

        public static AttackPresentationScope FromResultKey(AttackResultKey key)
            => key.IsValid
                ? new AttackPresentationScope(
                    key.AttackerInstanceId, key.SequenceId, key.HitIndex)
                : default;

        public bool Equals(AttackPresentationScope other)
            => AttackerInstanceId == other.AttackerInstanceId
                && SequenceId == other.SequenceId
                && HitIndex == other.HitIndex;
        public override bool Equals(object obj)
            => obj is AttackPresentationScope other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AttackerInstanceId.GetHashCode();
                hash = (hash * 397) ^ SequenceId.GetHashCode();
                return (hash * 397) ^ HitIndex;
            }
        }
    }

    /// <summary>서버 결과를 표현 계층에 전달하는 Unity/NGO 독립 값이다.</summary>
    public readonly struct AttackResultPresentationInput : IEquatable<AttackResultPresentationInput>
    {
        public int AttackerUnitId { get; }
        public ulong ActionRevision { get; }
        public AttackResultKey Key { get; }
        public AttackDeliveryKind Delivery { get; }
        public double ImpactServerTime { get; }
        public ActionDirectionXZ AimDirection { get; }
        public bool HasImpactPosition { get; }
        public WorldPointXZ ImpactPosition { get; }
        public AttackImpactOutcome Outcome { get; }
        public int AppliedAmount { get; }
        public int ResultingHp { get; }
        public int VictimPresentationType { get; }
        public int VictimTeam { get; }
        public bool HasPresentationSnapshot => VictimPresentationType >= 0 && VictimTeam >= 0;
        public bool IsVisualResult => Outcome == AttackImpactOutcome.HitApplied
            || Outcome == AttackImpactOutcome.StatusEffectApplied;
        public bool IsValid => AttackerUnitId >= 0 && ActionRevision != 0UL && Key.IsValid
            && Delivery != AttackDeliveryKind.None && ContractNumber.IsFinite(ImpactServerTime)
            && ImpactServerTime >= 0d && AimDirection.IsValid
            && (!HasImpactPosition || ImpactPosition.IsValid)
            && Outcome != AttackImpactOutcome.None;

        public AttackResultPresentationInput(
            int attackerUnitId, ulong actionRevision, AttackResultKey key,
            AttackDeliveryKind delivery, double impactServerTime,
            ActionDirectionXZ aimDirection, bool hasImpactPosition,
            WorldPointXZ impactPosition, AttackImpactOutcome outcome,
            int appliedAmount, int resultingHp,
            int victimPresentationType = -1, int victimTeam = -1)
        {
            AttackerUnitId = attackerUnitId;
            ActionRevision = actionRevision;
            Key = key;
            Delivery = delivery;
            ImpactServerTime = impactServerTime;
            AimDirection = aimDirection;
            HasImpactPosition = hasImpactPosition;
            ImpactPosition = impactPosition;
            Outcome = outcome;
            AppliedAmount = appliedAmount;
            ResultingHp = resultingHp;
            VictimPresentationType = victimPresentationType;
            VictimTeam = victimTeam;
        }

        public bool Equals(AttackResultPresentationInput other)
            => AttackerUnitId == other.AttackerUnitId && ActionRevision == other.ActionRevision
                && Key.Equals(other.Key) && Delivery == other.Delivery
                && ImpactServerTime.Equals(other.ImpactServerTime)
                && AimDirection.Equals(other.AimDirection)
                && HasImpactPosition == other.HasImpactPosition
                && (!HasImpactPosition || ImpactPosition.Equals(other.ImpactPosition))
                && Outcome == other.Outcome && AppliedAmount == other.AppliedAmount
                && ResultingHp == other.ResultingHp
                && VictimPresentationType == other.VictimPresentationType
                && VictimTeam == other.VictimTeam;
        public override bool Equals(object obj)
            => obj is AttackResultPresentationInput other && Equals(other);
        public override int GetHashCode() => Key.GetHashCode();
    }

    /// <summary>기존 marker/FIFO 경로가 실제로 한 일을 읽기만 하는 값이다.</summary>
    public readonly struct LegacyPresentationObservation
    {
        public LegacyPresentationObservationKind Kind { get; }
        public int AttackerUnitId { get; }
        public int VictimKind { get; }
        public int VictimId { get; }
        public int ResultingHp { get; }
        public double LocalTime { get; }
        public bool HasViewForward { get; }
        public ActionDirectionXZ ViewForward { get; }
        public AttackResultKey ResultKey { get; }
        public AttackPresentationScope AttackScope { get; }
        public AttackPresentationReplicatedSnapshot ReplicatedSnapshot { get; }
        public bool HasResultKey => ResultKey.IsValid;
        public bool HasAttackScope => AttackScope.IsValid;
        public bool IsValid => AttackerUnitId >= 0 && VictimKind >= 0 && VictimId >= -1
            && ContractNumber.IsFinite(LocalTime) && LocalTime >= 0d
            && (!HasViewForward || ViewForward.IsValid);

        public LegacyPresentationObservation(
            LegacyPresentationObservationKind kind, int attackerUnitId,
            int victimKind, int victimId, int resultingHp, double localTime,
            bool hasViewForward, ActionDirectionXZ viewForward)
            : this(kind, attackerUnitId, victimKind, victimId, resultingHp,
                localTime, hasViewForward, viewForward, default, default, default)
        {
        }

        public LegacyPresentationObservation(
            LegacyPresentationObservationKind kind, int attackerUnitId,
            int victimKind, int victimId, int resultingHp, double localTime,
            bool hasViewForward, ActionDirectionXZ viewForward,
            AttackResultKey resultKey, AttackPresentationScope attackScope,
            AttackPresentationReplicatedSnapshot replicatedSnapshot = default)
        {
            Kind = kind;
            AttackerUnitId = attackerUnitId;
            VictimKind = victimKind;
            VictimId = victimId;
            ResultingHp = resultingHp;
            LocalTime = localTime;
            HasViewForward = hasViewForward;
            ViewForward = viewForward;
            ResultKey = resultKey;
            AttackScope = attackScope;
            ReplicatedSnapshot = replicatedSnapshot;
        }
    }

    public readonly struct PresentationShadowClassification
    {
        public PresentationShadowResultStatus Status { get; }
        public AttackResultKey Key { get; }
        public int AttackerUnitId { get; }
        public int CandidateCount { get; }
        public double TimingDeltaSeconds { get; }
        public double AimDeltaDegrees { get; }
        public bool HasLegacyObservation { get; }
        public LegacyPresentationObservationKind ObservationKind { get; }
        public AttackDeliveryKind Delivery { get; }
        public int VictimKind { get; }
        public int VictimId { get; }
        public AttackPresentationScope AttackScope { get; }
        public ulong ActionRevision { get; }
        public AttackPresentationReplicatedSnapshot ReplicatedSnapshot { get; }
        public PresentationShadowClassification(
            PresentationShadowResultStatus status, AttackResultKey key,
            int attackerUnitId, int candidateCount, double timingDeltaSeconds,
            double aimDeltaDegrees, bool hasLegacyObservation = false,
            LegacyPresentationObservationKind observationKind = default,
            AttackDeliveryKind delivery = AttackDeliveryKind.None,
            int victimKind = -1,
            int victimId = -1,
            AttackPresentationScope attackScope = default,
            ulong actionRevision = 0UL,
            AttackPresentationReplicatedSnapshot replicatedSnapshot = default)
        {
            Status = status; Key = key; AttackerUnitId = attackerUnitId;
            CandidateCount = candidateCount; TimingDeltaSeconds = timingDeltaSeconds;
            AimDeltaDegrees = aimDeltaDegrees;
            HasLegacyObservation = hasLegacyObservation;
            ObservationKind = observationKind;
            Delivery = delivery;
            VictimKind = victimKind;
            VictimId = victimId;
            AttackScope = attackScope;
            ActionRevision = actionRevision;
            ReplicatedSnapshot = replicatedSnapshot;
        }
    }

    /// <summary>
    /// 서버 결과와 Legacy 관측을 제한된 메모리에서 비교한다. 이 타입에는 HP/VFX/Animator/RPC
    /// 의존성이 없으므로 gameplay write와 presentation emit을 구조적으로 수행할 수 없다.
    /// </summary>
    public sealed class UnitAttackResultPresentationShadowScheduler
    {
        public const int MaximumBufferedResults = 64;
        public const double MaximumAgeSeconds = 2.0d;
        public const double CatchUpSeconds = 0.5d;
        public const double PresentationDelaySeconds = 0.10d;

        private sealed class Entry
        {
            public AttackResultPresentationInput Input;
            public double ReceivedServerTime;
            public byte ConsumedResultStages;
        }
        private readonly Dictionary<AttackResultKey, Entry> _results =
            new Dictionary<AttackResultKey, Entry>();
        private readonly Queue<AttackResultKey> _order = new Queue<AttackResultKey>();
        private readonly List<LegacyPresentationObservation> _legacy =
            new List<LegacyPresentationObservation>();
        private readonly HashSet<AttackerInstanceId> _retired =
            new HashSet<AttackerInstanceId>();
        private readonly Dictionary<AttackPresentationScope, byte> _consumedScopeStages =
            new Dictionary<AttackPresentationScope, byte>();
        private readonly Queue<PresentationShadowClassification> _ready =
            new Queue<PresentationShadowClassification>();

        /// <summary>
        /// Editor self-validation에서 bounded-buffer 계약만 읽는 검증 seam이다.
        /// 런타임 상태를 변경하지 않는다.
        /// </summary>
        public int BufferedResultCountForValidation => _results.Count;

        /// <summary>
        /// 결과 하나가 도착했을 때 먼저 보관돼 있던 Marker/Enqueue가 둘 이상이면 첫 분류는
        /// 반환값으로, 나머지는 이 큐로 전달한다. Bridge는 매 입력 뒤 큐를 끝까지 비운다.
        /// </summary>
        public bool TryDequeueClassification(out PresentationShadowClassification classification)
        {
            if (_ready.Count == 0)
            {
                classification = default;
                return false;
            }
            classification = _ready.Dequeue();
            return true;
        }

        public PresentationShadowClassification ObserveResult(
            AttackResultPresentationInput input, double synchronizedServerTime)
        {
            if (!input.IsValid || !ContractNumber.IsFinite(synchronizedServerTime)
                || synchronizedServerTime < 0d)
                return Classify(PresentationShadowResultStatus.Invalid, input, 0, double.NaN, double.NaN);
            if (_retired.Contains(input.Key.AttackerInstanceId))
                return Classify(PresentationShadowResultStatus.InstanceRetired, input, 0, double.NaN, double.NaN);
            if (_results.TryGetValue(input.Key, out Entry existing))
                return Classify(existing.Input.Equals(input)
                    ? PresentationShadowResultStatus.Duplicate
                    : PresentationShadowResultStatus.Conflict, input, 0, double.NaN, double.NaN);

            Expire(synchronizedServerTime);
            double presentationServerTime = synchronizedServerTime - PresentationDelaySeconds;
            double lateness = presentationServerTime - input.ImpactServerTime;
            // 0.10초 표현 지연으로 흡수한 뒤에도 0.50초를 넘긴 결과는 과거 marker에
            // 소급 결합하지 않는다. 2초는 이미 예약된 양방향 증거의 reorder 보관 상한이다.
            if (lateness > CatchUpSeconds)
                return Classify(PresentationShadowResultStatus.Expired, input, 0,
                    lateness, double.NaN);
            // NET reorder 상한은 전역이 아니라 공격자 회차별이다. 대규모 전투에서
            // 다른 공격자의 결과가 이 회차의 정상 후보를 밀어내면 안 된다.
            if (CountResultsInSequence(input.Key) >= MaximumBufferedResults
                && TryRemoveOldestResultInSequence(input.Key, out AttackResultKey evicted))
            {
                _ready.Enqueue(new PresentationShadowClassification(
                    PresentationShadowResultStatus.Unmatched,
                    evicted,
                    input.AttackerUnitId,
                    0,
                    double.NaN,
                    double.NaN));
            }

            _results.Add(input.Key, new Entry
            {
                Input = input,
                ReceivedServerTime = synchronizedServerTime,
                ConsumedResultStages = 0
            });
            _order.Enqueue(input.Key);
            PresentationShadowClassification matched = MatchPending(input);
            if (matched.Status == PresentationShadowResultStatus.Matched
                || matched.Status == PresentationShadowResultStatus.Duplicate)
                return matched;
            return Classify(lateness > 0d && lateness <= CatchUpSeconds
                    ? PresentationShadowResultStatus.CatchUp
                    : PresentationShadowResultStatus.Scheduled,
                input, 0, lateness, double.NaN);
        }

        public PresentationShadowClassification ObserveLegacy(LegacyPresentationObservation observation)
        {
            if (!observation.IsValid)
                return new PresentationShadowClassification(
                    PresentationShadowResultStatus.Invalid, default,
                    observation.AttackerUnitId, 0, double.NaN, double.NaN,
                    true, observation.Kind, AttackDeliveryKind.None,
                    observation.VictimKind, observation.VictimId,
                    observation.AttackScope, 0UL, observation.ReplicatedSnapshot);
            Expire(observation.LocalTime);
            ExpireLegacy(observation.LocalTime);
            bool found = TryFindCandidate(observation, out AttackResultPresentationInput match);
            int count = found ? 1 : 0;
            if (count == 0)
            {
                if (!TryGetObservationSequence(observation,
                        out AttackerInstanceId observationInstance,
                        out AttackSequenceId observationSequence))
                {
                    return new PresentationShadowClassification(
                        PresentationShadowResultStatus.Invalid, default,
                        observation.AttackerUnitId, 0, double.NaN, double.NaN,
                        true, observation.Kind, AttackDeliveryKind.None,
                        observation.VictimKind, observation.VictimId,
                        observation.AttackScope, 0UL, observation.ReplicatedSnapshot);
                }
                if (CountLegacyInSequence(observationInstance, observationSequence)
                    >= MaximumBufferedResults)
                {
                    int oldest = FindOldestLegacyInSequence(
                        observationInstance, observationSequence);
                    if (oldest >= 0)
                    {
                        LegacyPresentationObservation evicted = _legacy[oldest];
                        _ready.Enqueue(new PresentationShadowClassification(
                            PresentationShadowResultStatus.Unmatched, default,
                            evicted.AttackerUnitId, 0, double.NaN, double.NaN,
                            true, evicted.Kind, AttackDeliveryKind.None,
                            evicted.VictimKind, evicted.VictimId,
                            evicted.AttackScope, 0UL, evicted.ReplicatedSnapshot));
                        _legacy.RemoveAt(oldest);
                    }
                }
                _legacy.Add(observation);
                return new PresentationShadowClassification(
                    PresentationShadowResultStatus.Scheduled, default,
                    observation.AttackerUnitId, 0, double.NaN, double.NaN,
                    true, observation.Kind, AttackDeliveryKind.None,
                    observation.VictimKind, observation.VictimId,
                    observation.AttackScope, 0UL, observation.ReplicatedSnapshot);
            }
            if (!TryConsumeStage(match.Key, observation.Kind))
                return Classify(PresentationShadowResultStatus.Duplicate, match, 1,
                    double.NaN, double.NaN, true, observation.Kind);
            return BuildMatch(match, observation);
        }

        public void Retire(AttackerInstanceId instanceId)
        {
            if (!instanceId.IsValid) return;
            _retired.Add(instanceId);
            var remove = new List<AttackResultKey>();
            foreach (AttackResultKey key in _results.Keys)
                if (key.AttackerInstanceId == instanceId) remove.Add(key);
            for (int i = 0; i < remove.Count; i++) _results.Remove(remove[i]);
            for (int index = _legacy.Count - 1; index >= 0; index--)
            {
                if (TryGetObservationSequence(_legacy[index], out AttackerInstanceId legacyInstance, out _)
                    && legacyInstance == instanceId)
                    _legacy.RemoveAt(index);
            }
            var consumedRemove = new List<AttackPresentationScope>();
            foreach (AttackPresentationScope scope in _consumedScopeStages.Keys)
                if (scope.AttackerInstanceId == instanceId) consumedRemove.Add(scope);
            for (int i = 0; i < consumedRemove.Count; i++)
                _consumedScopeStages.Remove(consumedRemove[i]);
        }

        public void Clear()
        {
            _results.Clear(); _order.Clear(); _legacy.Clear(); _retired.Clear();
            _consumedScopeStages.Clear(); _ready.Clear();
        }

        private PresentationShadowClassification MatchPending(AttackResultPresentationInput input)
        {
            PresentationShadowClassification first = Classify(
                PresentationShadowResultStatus.Unmatched, input, 0, double.NaN, double.NaN);
            bool hasFirst = false;
            for (int i = _legacy.Count - 1; i >= 0; i--)
            {
                LegacyPresentationObservation observation = _legacy[i];
                if (!IsCandidate(input, observation)) continue;
                PresentationShadowClassification classification = TryConsumeStage(
                        input.Key, observation.Kind)
                    ? BuildMatch(input, observation)
                    : Classify(PresentationShadowResultStatus.Duplicate, input, 1,
                        double.NaN, double.NaN, true, observation.Kind);
                _legacy.RemoveAt(i);
                if (!hasFirst)
                {
                    first = classification;
                    hasFirst = true;
                }
                else
                {
                    _ready.Enqueue(classification);
                }
            }
            return first;
        }

        private int CountResultsInSequence(AttackResultKey key)
        {
            int count = 0;
            foreach (AttackResultKey candidate in _results.Keys)
            {
                if (IsSameSequence(candidate, key)) count++;
            }
            return count;
        }

        private bool TryRemoveOldestResultInSequence(
            AttackResultKey sequenceKey,
            out AttackResultKey removed)
        {
            removed = default;
            int count = _order.Count;
            bool found = false;
            for (int index = 0; index < count; index++)
            {
                AttackResultKey candidate = _order.Dequeue();
                if (!found && IsSameSequence(candidate, sequenceKey)
                    && _results.Remove(candidate))
                {
                    removed = candidate;
                    found = true;
                    continue;
                }
                _order.Enqueue(candidate);
            }
            return found;
        }

        private int CountLegacyInSequence(
            AttackerInstanceId instanceId,
            AttackSequenceId sequenceId)
        {
            int count = 0;
            for (int index = 0; index < _legacy.Count; index++)
            {
                if (TryGetObservationSequence(_legacy[index], out AttackerInstanceId candidateInstance,
                        out AttackSequenceId candidateSequence)
                    && candidateInstance == instanceId
                    && candidateSequence == sequenceId)
                    count++;
            }
            return count;
        }

        private int FindOldestLegacyInSequence(
            AttackerInstanceId instanceId,
            AttackSequenceId sequenceId)
        {
            for (int index = 0; index < _legacy.Count; index++)
            {
                if (TryGetObservationSequence(_legacy[index], out AttackerInstanceId candidateInstance,
                        out AttackSequenceId candidateSequence)
                    && candidateInstance == instanceId
                    && candidateSequence == sequenceId)
                    return index;
            }
            return -1;
        }

        private static bool TryGetObservationSequence(
            LegacyPresentationObservation observation,
            out AttackerInstanceId instanceId,
            out AttackSequenceId sequenceId)
        {
            if (observation.HasResultKey)
            {
                instanceId = observation.ResultKey.AttackerInstanceId;
                sequenceId = observation.ResultKey.SequenceId;
                return instanceId.IsValid && sequenceId.IsValid;
            }
            if (observation.HasAttackScope)
            {
                instanceId = observation.AttackScope.AttackerInstanceId;
                sequenceId = observation.AttackScope.SequenceId;
                return instanceId.IsValid && sequenceId.IsValid;
            }
            instanceId = AttackerInstanceId.None;
            sequenceId = AttackSequenceId.None;
            return false;
        }

        private static bool IsSameSequence(AttackResultKey left, AttackResultKey right)
            => left.AttackerInstanceId == right.AttackerInstanceId
                && left.SequenceId == right.SequenceId;

        private bool TryFindCandidate(
            LegacyPresentationObservation observation,
            out AttackResultPresentationInput match)
        {
            match = default;
            if (observation.HasResultKey)
            {
                if (_results.TryGetValue(observation.ResultKey, out Entry exact)
                    && IsCandidate(exact.Input, observation))
                {
                    match = exact.Input;
                    return true;
                }
                return false;
            }

            if (!observation.HasAttackScope) return false;
            bool found = false;
            foreach (Entry entry in _results.Values)
            {
                if (!IsCandidate(entry.Input, observation)) continue;
                if (!found || entry.Input.Key.CompareTo(match.Key) < 0)
                    match = entry.Input;
                found = true;
            }
            return found;
        }

        private static bool IsCandidate(
            AttackResultPresentationInput input, LegacyPresentationObservation observation)
        {
            if (observation.AttackerUnitId != input.AttackerUnitId
                || Math.Abs(observation.LocalTime - input.ImpactServerTime) > MaximumAgeSeconds)
                return false;

            if (observation.HasResultKey)
            {
                // Enqueue/Emit은 실제 피해자 한 건의 표현이다. 전체 정규 키뿐 아니라
                // 피해자와 결과 HP도 동일해야 다른 AoE 결과를 잘못 소비하지 않는다.
                return observation.ResultKey.Equals(input.Key)
                    && observation.VictimKind == input.Key.VictimKind
                    && observation.VictimId == input.Key.VictimId
                    && (observation.ResultingHp < 0
                        || observation.ResultingHp == input.ResultingHp);
            }

            // Marker/Tracer는 피해 결과가 아니라 한 공격자의 한 타격 순간이다. 타겟이 marker
            // 직전에 사망하거나 Stop/ChangeTarget이 먼저 도착하면 UnitView의 현재 target은 이미
            // -1일 수 있지만, 발사 때 캡처한 exact scope는 여전히 유효하다. 따라서 scope-only
            // 관측을 현재 피해자 Id로 다시 제한하지 않는다. AoE의 여러 피해자는 결과 단계의
            // 전체 AttackResultKey에서 각각 엄격하게 검증한다.
            return observation.HasAttackScope
                && observation.AttackScope.Equals(
                    AttackPresentationScope.FromResultKey(input.Key));
        }

        private bool TryConsumeStage(
            AttackResultKey key,
            LegacyPresentationObservationKind kind)
        {
            byte bit = GetStageBit(kind);
            if (bit == 0) return false;
            if (kind == LegacyPresentationObservationKind.Marker
                || kind == LegacyPresentationObservationKind.TracerImpact)
            {
                AttackPresentationScope scope = AttackPresentationScope.FromResultKey(key);
                _consumedScopeStages.TryGetValue(scope, out byte consumed);
                if ((consumed & bit) != 0) return false;
                _consumedScopeStages[scope] = (byte)(consumed | bit);
                return true;
            }

            if (!_results.TryGetValue(key, out Entry entry)) return false;
            if ((entry.ConsumedResultStages & bit) != 0) return false;
            entry.ConsumedResultStages |= bit;
            return true;
        }

        private static byte GetStageBit(LegacyPresentationObservationKind kind)
        {
            switch (kind)
            {
                case LegacyPresentationObservationKind.Marker: return 1;
                case LegacyPresentationObservationKind.TracerImpact: return 2;
                case LegacyPresentationObservationKind.Enqueued: return 4;
                case LegacyPresentationObservationKind.EmittedMarker:
                case LegacyPresentationObservationKind.EmittedImmediate:
                case LegacyPresentationObservationKind.EmittedTimeout:
                case LegacyPresentationObservationKind.EmittedTargetDeath:
                case LegacyPresentationObservationKind.EmittedAttackerStop:
                    return 8;
                default: return 0;
            }
        }

        private static bool IsTerminalEmit(LegacyPresentationObservationKind kind)
            => kind == LegacyPresentationObservationKind.EmittedMarker
                || kind == LegacyPresentationObservationKind.EmittedImmediate
                || kind == LegacyPresentationObservationKind.EmittedTimeout
                || kind == LegacyPresentationObservationKind.EmittedTargetDeath
                || kind == LegacyPresentationObservationKind.EmittedAttackerStop;

        private static PresentationShadowClassification BuildMatch(
            AttackResultPresentationInput input, LegacyPresentationObservation observation)
        {
            double aim = observation.HasViewForward
                ? AngleDegrees(input.AimDirection, observation.ViewForward) : double.NaN;
            return Classify(PresentationShadowResultStatus.Matched, input, 1,
                observation.LocalTime - input.ImpactServerTime, aim,
                true, observation.Kind, observation.ReplicatedSnapshot);
        }

        /// <summary>
        /// Red 진영의 Visual Root처럼 화면 좌표가 180도 반전된 경우 방향만 도메인 좌표로
        /// 되돌린다. 위치 pivot은 방향 벡터에 영향을 주지 않으므로 X/Z 부호만 반전한다.
        /// </summary>
        public static ActionDirectionXZ FlipDirection180(ActionDirectionXZ direction)
        {
            return direction.IsValid
                && ActionDirectionXZ.TryCreate(-direction.X, -direction.Z, out ActionDirectionXZ flipped)
                    ? flipped
                    : default;
        }

        private void Expire(double serverTime)
        {
            while (_order.Count > 0)
            {
                AttackResultKey key = _order.Peek();
                if (!_results.TryGetValue(key, out Entry entry)) { _order.Dequeue(); continue; }
                if (serverTime - entry.ReceivedServerTime <= MaximumAgeSeconds) break;
                _order.Dequeue(); _results.Remove(key);
            }
            CleanupOrphanedConsumedScopes();
        }

        private void CleanupOrphanedConsumedScopes()
        {
            if (_consumedScopeStages.Count == 0) return;
            var remove = new List<AttackPresentationScope>();
            foreach (AttackPresentationScope scope in _consumedScopeStages.Keys)
            {
                bool retained = false;
                foreach (AttackResultKey key in _results.Keys)
                {
                    if (AttackPresentationScope.FromResultKey(key).Equals(scope))
                    {
                        retained = true;
                        break;
                    }
                }
                if (!retained)
                {
                    for (int index = 0; index < _legacy.Count; index++)
                    {
                        if (_legacy[index].HasAttackScope
                            && _legacy[index].AttackScope.Equals(scope))
                        {
                            retained = true;
                            break;
                        }
                    }
                }
                if (!retained) remove.Add(scope);
            }
            for (int index = 0; index < remove.Count; index++)
                _consumedScopeStages.Remove(remove[index]);
        }

        private void ExpireLegacy(double serverTime)
        {
            for (int index = _legacy.Count - 1; index >= 0; index--)
            {
                if (serverTime - _legacy[index].LocalTime > MaximumAgeSeconds)
                {
                    LegacyPresentationObservation expired = _legacy[index];
                    _ready.Enqueue(new PresentationShadowClassification(
                        PresentationShadowResultStatus.Unmatched, default,
                        expired.AttackerUnitId, 0, double.NaN, double.NaN,
                        true, expired.Kind, AttackDeliveryKind.None,
                        expired.VictimKind, expired.VictimId,
                        expired.AttackScope, 0UL, expired.ReplicatedSnapshot));
                    _legacy.RemoveAt(index);
                }
            }
        }

        public void FlushPendingAsUnmatched()
        {
            for (int index = 0; index < _legacy.Count; index++)
            {
                LegacyPresentationObservation pending = _legacy[index];
                _ready.Enqueue(new PresentationShadowClassification(
                    PresentationShadowResultStatus.Unmatched, default,
                    pending.AttackerUnitId, 0, double.NaN, double.NaN,
                    true, pending.Kind, AttackDeliveryKind.None,
                    pending.VictimKind, pending.VictimId,
                    pending.AttackScope, 0UL, pending.ReplicatedSnapshot));
            }
            _legacy.Clear();
        }

        private static double AngleDegrees(ActionDirectionXZ a, ActionDirectionXZ b)
        {
            double dot = Math.Max(-1d, Math.Min(1d, a.X * b.X + a.Z * b.Z));
            return Math.Acos(dot) * (180d / Math.PI);
        }

        private static PresentationShadowClassification Classify(
            PresentationShadowResultStatus status, AttackResultPresentationInput input,
            int candidates, double timing, double aim,
            bool hasLegacyObservation = false,
            LegacyPresentationObservationKind observationKind = default,
            AttackPresentationReplicatedSnapshot replicatedSnapshot = default)
            => new PresentationShadowClassification(
                status, input.Key, input.AttackerUnitId, candidates, timing, aim,
                hasLegacyObservation, observationKind, input.Delivery,
                input.Key.VictimKind, input.Key.VictimId,
                AttackPresentationScope.FromResultKey(input.Key),
                input.ActionRevision, replicatedSnapshot);
    }

    /// <summary>
    /// 조합 루트가 활성화하는 C3 read-only 버스다. 전달과 비교만 하며 효과 API를 노출하지 않는다.
    /// </summary>
    public enum AttackPresentationDispatchFailure : byte
    {
        SnapshotInvalid = 1,
        PresenterUnavailable = 2,
        ChannelEmissionFailed = 3
    }

    public static class UnitAttackResultPresentationShadowBridge
    {
        // Comparison-only migration stage. This object has no gameplay or visual emitter callback.
        public static readonly AuthoritativeAttackPresentationCoordinator Coordinator = new();
        public static bool WasAuthoritativeEmitter { get; private set; }
        public static bool UsesAuthoritativeEmitter => IsActive && WasAuthoritativeEmitter;
        public static bool OwnsAttacker(int unitId) => UsesAuthoritativeEmitter
            && AuditEligibilityByUnitId.TryGetValue(unitId, out var eligibility)
            && (eligibility == AttackPresentationAuditEligibility.Required
                || eligibility == AttackPresentationAuditEligibility.RequiredButMissing);
        public static double CurrentServerTime => _lastCoordinatorTime;
        public static int PresentationEmits { get; private set; }
        public static int PresentationViewUnavailable { get; private set; }
        public static int PresentationSnapshotInvalid { get; private set; }
        public static int PresentationPresenterUnavailable { get; private set; }
        public static int PresentationChannelFailures { get; private set; }
        public static int PresentationOptionalViewSkipped { get; private set; }
        public static int PresentationHitVfxConfigured { get; private set; }
        public static int PresentationHitVfxEmitted { get; private set; }
        public static int PresentationHitVfxSkippedNoAsset { get; private set; }
        public static int PresentationSpatialSamples { get; private set; }
        public static int PresentationSpatialMismatches { get; private set; }
        public static double PresentationMaximumSpatialDelta { get; private set; }
        // 결과 도착 사이에 살아 있는 피해자가 이동할 수 있는 범위를 허용하되,
        // 맵 반전 누락/중복처럼 큰 좌표 오류는 숨기지 않는 표시 검증 전용 envelope다.
        public const double MaximumPresentationVictimMotionEnvelope = 1.5d;
        public static int PresentationDuplicateAttempts { get; private set; }
        public static int CompletedBundleTransportFailures { get; private set; }
        private static readonly HashSet<AttackResultKey> PresentedKeys = new();
        private static double _lastCoordinatorTime;
        private static readonly UnitAttackResultPresentationShadowScheduler Scheduler =
            new UnitAttackResultPresentationShadowScheduler();
        private static readonly Dictionary<int, AttackPresentationAuditEligibility>
            AuditEligibilityByUnitId =
                new Dictionary<int, AttackPresentationAuditEligibility>();
        public static event Action<PresentationShadowClassification> Classified;
        public static bool IsActive { get; private set; }
        private static double _clockAnchorServerTime;
        private static double _clockAnchorLocalTime;
        private static bool _hasClockAnchor;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // 관측 전용: 다른 Result가 시계 기준점을 바꿨는지 실패 시점에 확인한다.
        private static int _clockAnchorRevision;
        private static string _clockAnchorSource;
        public static void ReadClockAnchorDiagnostic(out double serverTime,
            out double localTime, out int revision, out string source)
        {
            serverTime = _hasClockAnchor ? _clockAnchorServerTime : double.NaN;
            localTime = _hasClockAnchor ? _clockAnchorLocalTime : double.NaN;
            revision = _clockAnchorRevision;
            source = _clockAnchorSource;
        }
#endif

        public static void BeginMatch(
            bool active,
            double synchronizedServerTime = double.NaN,
            double localMonotonicTime = double.NaN)
        {
            Scheduler.Clear();
            Coordinator.Clear();
            PresentationEmits = PresentationViewUnavailable = 0;
            PresentationSnapshotInvalid = PresentationPresenterUnavailable = 0;
            PresentationChannelFailures = PresentationOptionalViewSkipped = 0;
            PresentationHitVfxConfigured = PresentationHitVfxEmitted = 0;
            PresentationHitVfxSkippedNoAsset = 0;
            PresentationSpatialSamples = PresentationSpatialMismatches = 0;
            PresentationMaximumSpatialDelta = 0d;
            PresentationDuplicateAttempts = 0;
            CompletedBundleTransportFailures = 0;
            PresentedKeys.Clear();
            Coordinator.RequireCompleteBundle = UsesAuthoritativeEmitter;
            _lastCoordinatorTime = synchronizedServerTime;
            AuditEligibilityByUnitId.Clear();
            IsActive = active;
            WasAuthoritativeEmitter = active
                && NetworkContext.ActiveCombatPipelineMode == CombatPipelineMode.ResultPresentation;
            Coordinator.RequireCompleteBundle = active
                && NetworkContext.ActiveCombatPipelineMode == CombatPipelineMode.ResultPresentation;
            _hasClockAnchor = active
                && ContractNumber.IsFinite(synchronizedServerTime)
                && ContractNumber.IsFinite(localMonotonicTime)
                && synchronizedServerTime >= 0d && localMonotonicTime >= 0d;
            if (_hasClockAnchor)
            {
                _clockAnchorServerTime = synchronizedServerTime;
                _clockAnchorLocalTime = localMonotonicTime;
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _clockAnchorRevision = _hasClockAnchor ? 1 : 0;
            _clockAnchorSource = _hasClockAnchor ? "begin-match" : "none";
#endif
        }

        public static void EndMatch()
        {
            if (IsActive)
            {
                Coordinator.Complete(_lastCoordinatorTime);
                Scheduler.FlushPendingAsUnmatched();
                while (Scheduler.TryDequeueClassification(
                    out PresentationShadowClassification pending))
                    Classified?.Invoke(pending);
            }
            IsActive = false;
            _hasClockAnchor = false;
            Scheduler.Clear();
            AuditEligibilityByUnitId.Clear();
        }

        /// <summary>
        /// UnitView 초기화 시 manifest/runtime 계약의 채점 자격만 등록한다. 이 표는
        /// Legacy 피해/VFX emitter를 제어하지 않고 read-only bridge 진입만 분류한다.
        /// </summary>
        public static void RegisterAuditEligibility(
            int attackerUnitId,
            AttackPresentationAuditEligibility eligibility)
        {
            if (attackerUnitId < 0
                || eligibility == AttackPresentationAuditEligibility.None)
                return;
            AuditEligibilityByUnitId[attackerUnitId] = eligibility;
        }

        public static void ObserveResult(
            AttackResultPresentationInput input,
            double synchronizedServerTime,
            double localMonotonicTime)
        {
            if (!IsActive) return;
            if (!ShouldAudit(input.AttackerUnitId, out bool requiredButMissing))
                return;
            if (requiredButMissing)
            {
                Classified?.Invoke(new PresentationShadowClassification(
                    PresentationShadowResultStatus.Invalid,
                    input.Key,
                    input.AttackerUnitId,
                    0,
                    double.NaN,
                    double.NaN));
                return;
            }
            if (!ContractNumber.IsFinite(synchronizedServerTime)
                || !ContractNumber.IsFinite(localMonotonicTime)
                || synchronizedServerTime < 0d || localMonotonicTime < 0d)
            {
                Publish(Scheduler.ObserveResult(input, double.NaN));
                return;
            }
            _clockAnchorServerTime = synchronizedServerTime;
            _clockAnchorLocalTime = localMonotonicTime;
            _hasClockAnchor = true;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _clockAnchorRevision++;
            _clockAnchorSource = "result";
#endif
            _lastCoordinatorTime = synchronizedServerTime;
            // ResultPresentation 경기는 self-contained 완료 묶음이 Coordinator의 유일한
            // 표현 입력이다. 개별 C2 shadow 결과는 피해 수렴 관측에만 사용하며, 타입·팀
            // 스냅샷이 없는 값을 먼저 넣어 완료 묶음과 충돌시키지 않는다.
            if (!UsesAuthoritativeEmitter)
                Coordinator.ObserveResult(input, synchronizedServerTime);
            Publish(Scheduler.ObserveResult(input, synchronizedServerTime));
        }

        public static void ObserveSchedule(AttackPresentationSchedule schedule)
        {
            if (IsActive && ShouldAudit(schedule.AttackerUnitId, out _))
                Coordinator.ObserveSchedule(schedule);
        }

        public static void ObserveCompletedBundle(AttackResultPresentationInput[] results,
            double synchronizedServerTime, double localMonotonicTime)
        {
            if (!IsActive) return;
            if (ContractNumber.IsFinite(synchronizedServerTime) && synchronizedServerTime >= 0
                && ContractNumber.IsFinite(localMonotonicTime) && localMonotonicTime >= 0)
            {
                _clockAnchorServerTime = synchronizedServerTime;
                _clockAnchorLocalTime = localMonotonicTime;
                _lastCoordinatorTime = synchronizedServerTime;
                _hasClockAnchor = true;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _clockAnchorRevision++;
                _clockAnchorSource = "completed-bundle";
#endif
            }
            Coordinator.ObserveCompletedBundle(results, synchronizedServerTime);
        }

        public static void RecordPresentationDispatch(AttackResultPresentationInput result, bool displayed)
        {
            if (!UsesAuthoritativeEmitter) return;
            if (displayed) PresentationEmits++; else PresentationViewUnavailable++;
        }

        public static void RecordPresentationFailure(AttackPresentationDispatchFailure failure)
        {
            if (!UsesAuthoritativeEmitter) return;
            PresentationViewUnavailable++;
            switch (failure)
            {
                case AttackPresentationDispatchFailure.SnapshotInvalid:
                    PresentationSnapshotInvalid++;
                    break;
                case AttackPresentationDispatchFailure.PresenterUnavailable:
                    PresentationPresenterUnavailable++;
                    break;
                case AttackPresentationDispatchFailure.ChannelEmissionFailed:
                    PresentationChannelFailures++;
                    break;
            }
        }

        public static void RecordHitVfxOutcome(bool configured, bool emitted)
        {
            if (!UsesAuthoritativeEmitter) return;
            if (!configured)
            {
                PresentationHitVfxSkippedNoAsset++;
                return;
            }
            PresentationHitVfxConfigured++;
            if (emitted) PresentationHitVfxEmitted++;
        }

        public static void RecordOptionalViewSkipped()
        {
            if (UsesAuthoritativeEmitter) PresentationOptionalViewSkipped++;
        }

        public static bool RecordPresentationSpatialSample(
            double displayX,
            double displayZ,
            double victimPresentationX,
            double victimPresentationZ)
        {
            if (!UsesAuthoritativeEmitter
                || !ContractNumber.IsFinite(displayX)
                || !ContractNumber.IsFinite(displayZ)
                || !ContractNumber.IsFinite(victimPresentationX)
                || !ContractNumber.IsFinite(victimPresentationZ))
                return false;

            bool matches = IsPresentationSpatialMatch(
                displayX, displayZ, victimPresentationX, victimPresentationZ,
                out double delta);
            PresentationSpatialSamples++;
            if (delta > PresentationMaximumSpatialDelta)
                PresentationMaximumSpatialDelta = delta;
            if (!matches)
            {
                PresentationSpatialMismatches++;
                return false;
            }
            return true;
        }

        public static bool IsPresentationSpatialMatch(
            double displayX,
            double displayZ,
            double victimPresentationX,
            double victimPresentationZ,
            out double delta)
        {
            delta = double.NaN;
            if (!ContractNumber.IsFinite(displayX)
                || !ContractNumber.IsFinite(displayZ)
                || !ContractNumber.IsFinite(victimPresentationX)
                || !ContractNumber.IsFinite(victimPresentationZ))
                return false;
            double deltaX = displayX - victimPresentationX;
            double deltaZ = displayZ - victimPresentationZ;
            delta = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            return delta <= MaximumPresentationVictimMotionEnvelope;
        }
        public static bool TryConsumePresentationResult(AttackResultPresentationInput result)
        {
            return TryConsumePresentationBundle(new[] { result });
        }

        /// <summary>
        /// 완결 묶음의 모든 exact key를 먼저 검사한 뒤 한 번에 소비한다. 일부 key만
        /// 소비된 상태에서 다른 피해자의 View 준비 실패가 드러나는 partial emit을 막는다.
        /// </summary>
        public static bool TryConsumePresentationBundle(
            AttackResultPresentationInput[] results)
        {
            if (!UsesAuthoritativeEmitter || results == null || results.Length == 0
                || PresentedKeys.Count + results.Length > 32768)
            {
                PresentationDuplicateAttempts++;
                return false;
            }
            for (int i = 0; i < results.Length; i++)
            {
                if (!results[i].IsValid || PresentedKeys.Contains(results[i].Key))
                {
                    PresentationDuplicateAttempts++;
                    return false;
                }
                for (int j = 0; j < i; j++)
                    if (results[j].Key.Equals(results[i].Key))
                    {
                        PresentationDuplicateAttempts++;
                        return false;
                    }
            }
            for (int i = 0; i < results.Length; i++) PresentedKeys.Add(results[i].Key);
            return true;
        }
        public static void ReportCompletedBundleTransportFailure(string reason)
        {
            if (!IsActive) return;
            CompletedBundleTransportFailures++;
            if (CompletedBundleTransportFailures <= 16)
                GameLog.Dev.Warn("Combat", nameof(UnitAttackResultPresentationShadowBridge),
                    "[UAS-AUTH-PRESENT] completed-bundle transport failure", "reason=" + reason);
        }

        /// <summary>
        /// 경기 수명 전송 경계의 성공/실패를 반드시 소비한다. 호출자가 bool 반환을 무시해
        /// 화면 묶음 유실을 정상 경기로 끝내지 못하도록 실패만 terminal 계수에 연결한다.
        /// </summary>
        public static bool ObserveCompletedBundleTransportResult(
            bool published,
            string failureReason)
        {
            if (!published)
                ReportCompletedBundleTransportFailure(failureReason);
            return published;
        }

        public static void TickCoordinator(double localMonotonicTime)
        {
            if (!IsActive || !_hasClockAnchor) return;
            _lastCoordinatorTime = _clockAnchorServerTime + localMonotonicTime - _clockAnchorLocalTime;
            Coordinator.Tick(_lastCoordinatorTime);
        }

        public static void ObserveLegacy(LegacyPresentationObservation observation)
        {
            if (!IsActive) return;
            if (!ShouldAudit(observation.AttackerUnitId, out bool requiredButMissing))
                return;
            if (requiredButMissing)
            {
                Classified?.Invoke(new PresentationShadowClassification(
                    PresentationShadowResultStatus.Invalid,
                    observation.ResultKey,
                    observation.AttackerUnitId,
                    0,
                    double.NaN,
                    double.NaN,
                    hasLegacyObservation: true,
                    observationKind: observation.Kind,
                    victimKind: observation.VictimKind,
                    victimId: observation.VictimId,
                    attackScope: observation.AttackScope,
                    replicatedSnapshot: observation.ReplicatedSnapshot));
                return;
            }
            if (!_hasClockAnchor)
            {
                Classified?.Invoke(new PresentationShadowClassification(
                    PresentationShadowResultStatus.Invalid, default,
                    observation.AttackerUnitId, 0, double.NaN, double.NaN));
                return;
            }
            double observationServerTime = _clockAnchorServerTime
                + (observation.LocalTime - _clockAnchorLocalTime);
            var normalized = new LegacyPresentationObservation(
                observation.Kind, observation.AttackerUnitId,
                observation.VictimKind, observation.VictimId,
                observation.ResultingHp, observationServerTime,
                observation.HasViewForward, observation.ViewForward,
                observation.ResultKey, observation.AttackScope,
                observation.ReplicatedSnapshot);
            if (observation.Kind >= LegacyPresentationObservationKind.EmittedMarker
                && observation.Kind <= LegacyPresentationObservationKind.EmittedAttackerStop)
                Coordinator.ObserveLegacyEmit(observation.ResultKey, observationServerTime);
            Publish(Scheduler.ObserveLegacy(normalized));
        }

        /// <summary>
        /// 표현 저장소가 손상된 입력 또는 용량 초과를 만났지만 화면 유실을 막기 위해
        /// 즉시 fallback을 실행한 경우 호출한다. fallback 성공과 구조 건전성은 별개이므로
        /// scheduler 후보로 다시 넣지 않고 C3 실패 증거를 즉시 발행한다.
        /// </summary>
        public static void ObserveStructuralFailure(
            LegacyPresentationObservation observation)
        {
            if (!IsActive) return;
            if (!ShouldAudit(observation.AttackerUnitId, out _))
                return;
            Classified?.Invoke(new PresentationShadowClassification(
                PresentationShadowResultStatus.Invalid,
                observation.ResultKey,
                observation.AttackerUnitId,
                0,
                double.NaN,
                double.NaN,
                hasLegacyObservation: true,
                observationKind: observation.Kind,
                victimKind: observation.VictimKind,
                victimId: observation.VictimId,
                attackScope: observation.AttackScope,
                replicatedSnapshot: observation.ReplicatedSnapshot));
        }

        public static void Retire(AttackerInstanceId instanceId)
        {
            if (!IsActive) return;
            Scheduler.Retire(instanceId);
            Coordinator.Retire(instanceId);
        }

        private static bool ShouldAudit(
            int attackerUnitId,
            out bool requiredButMissing)
        {
            requiredButMissing = false;
            if (!AuditEligibilityByUnitId.TryGetValue(
                    attackerUnitId,
                    out AttackPresentationAuditEligibility eligibility))
            {
                // 구형/테스트 호출은 기존 fail-closed scheduler 경로를 유지한다.
                return true;
            }

            if (eligibility == AttackPresentationAuditEligibility.KnownUnresolved
                || eligibility == AttackPresentationAuditEligibility.NotApplicable)
                return false;
            requiredButMissing = eligibility
                == AttackPresentationAuditEligibility.RequiredButMissing;
            return true;
        }

        private static void Publish(PresentationShadowClassification primary)
        {
            Classified?.Invoke(primary);
            while (Scheduler.TryDequeueClassification(
                out PresentationShadowClassification additional))
            {
                Classified?.Invoke(additional);
            }
        }
    }
}
