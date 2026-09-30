using System;
using System.Collections.Generic;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Core;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Legacy 공격 옆에서만 동작하는 Tracer C 서버 coordinator다.
    /// 실제 Legacy 공격 시작마다 독립 UnitActionSequencer 회차를 만들고 예약이 그 회차를
    /// Impact 종료까지 소유한다. AttackSequenceId는 production 경계의 5도 Commit에서만 발급된다.
    /// 근거: U-TARGET-COMMIT, U-ATK-ALIGN, U-ATK-TIMELINE, U-IMPACT-TARGETLOCKED.
    /// 이 클래스의 반환값은 진단/복제 전용이며 gameplay 분기나 피해 writer에 사용하면 안 된다.
    /// </summary>
    public sealed class UnitAttackShadowCoordinator
    {
        private const int MaximumPendingLegacyAttacks = 512;
        private const double StationaryPositionEpsilonSquared = 0.00000001d;
        private const int DirectDamageEffectKind = 1;
        private const int DirectResultOrdinal = 0;

        private sealed class Entry
        {
            public UnitData Unit;
            public UnitAttackShadowProfile Profile;
            public UnitActionSequencer Sequencer;
            public bool HasLastPosition;
            public WorldPointXZ LastPosition;
            public ulong LastLegacyToken;
            public ActionDirectionXZ LatestSimulationFacing;
            public int LastEvaluatedHitIndex = -1;
            public ulong LastCommittedSequenceValue;
        }

        private sealed class LegacyReservation
        {
            public LegacyAttackToken Token;
            public AttackTargetBinding Target;
            public AttackSequenceId ShadowSequenceAtSchedule;
            public double LegacyScheduledAt;
            public double ShadowCommitAt;
            public int ImpactCount;
            public ulong DispatchedMask;
            public ulong CompletedMask;
            public ulong CancelledMask;
            public ulong CancellationConsumedMask;
            public ImpactAuthorization[] Authorizations;
            public UnitActionSequencer Sequencer;
        }

        private readonly struct ReservationKey : IEquatable<ReservationKey>
        {
            public AttackerInstanceId Attacker { get; }
            public ulong Token { get; }

            public ReservationKey(LegacyAttackToken token)
            {
                Attacker = token.AttackerInstanceId;
                Token = token.Value;
            }

            public bool Equals(ReservationKey other)
                => Attacker == other.Attacker && Token == other.Token;
            public override bool Equals(object obj) => obj is ReservationKey other && Equals(other);
            public override int GetHashCode() => (Attacker.GetHashCode() * 397) ^ Token.GetHashCode();
        }

        public readonly struct IntentObservation
        {
            public UnitActionReducerStatus Status { get; }
            public UnitActionSnapshot Snapshot { get; }
            public UnitAttackShadowProfile Profile { get; }
            public bool IsStationary { get; }
            public bool CommittedNow { get; }
            public string Reason { get; }
            public UnitAttackShadowPublication Publication { get; }
            public bool ShouldPublish { get; }
            public bool ExpectedDeferred { get; }

            internal IntentObservation(
                UnitActionReducerStatus status,
                UnitActionSnapshot snapshot,
                UnitAttackShadowProfile profile,
                bool isStationary,
                bool committedNow,
                string reason,
                UnitAttackShadowPublication publication = default,
                bool shouldPublish = false,
                bool expectedDeferred = false)
            {
                Status = status;
                Snapshot = snapshot;
                Profile = profile;
                IsStationary = isStationary;
                CommittedNow = committedNow;
                Reason = reason;
                Publication = publication;
                ShouldPublish = shouldPublish;
                ExpectedDeferred = expectedDeferred;
            }
        }

        public readonly struct DispatchObservation
        {
            public UnitActionReducerStatus AdvanceStatus { get; }
            public UnitActionReducerStatus EvaluateStatus { get; }
            public ImpactAuthorization Authorization { get; }
            public UnitActionSnapshot Snapshot { get; }
            public LegacyAttackToken LegacyToken { get; }
            public AttackSequenceId ScheduledShadowSequence { get; }
            public string Reason { get; }
            public UnitAttackShadowPublication Publication { get; }
            public bool ShouldPublish { get; }

            internal DispatchObservation(
                UnitActionReducerStatus advanceStatus,
                UnitActionReducerStatus evaluateStatus,
                ImpactAuthorization authorization,
                UnitActionSnapshot snapshot,
                LegacyAttackToken legacyToken,
                AttackSequenceId scheduledShadowSequence,
                string reason,
                UnitAttackShadowPublication publication = default,
                bool shouldPublish = false)
            {
                AdvanceStatus = advanceStatus;
                EvaluateStatus = evaluateStatus;
                Authorization = authorization;
                Snapshot = snapshot;
                LegacyToken = legacyToken;
                ScheduledShadowSequence = scheduledShadowSequence;
                Reason = reason;
                Publication = publication;
                ShouldPublish = shouldPublish;
            }
        }

        private readonly Dictionary<AttackerInstanceId, Entry> _entries
            = new Dictionary<AttackerInstanceId, Entry>();
        private readonly Dictionary<ReservationKey, LegacyReservation> _reservations
            = new Dictionary<ReservationKey, LegacyReservation>();
        private readonly Queue<ReservationKey> _reservationOrder = new Queue<ReservationKey>();
        private readonly Dictionary<ReservationKey, ulong> _retiredCancellationMasks
            = new Dictionary<ReservationKey, ulong>();
        private readonly Queue<ReservationKey> _retiredCancellationOrder
            = new Queue<ReservationKey>();

        public int ActiveAttackerCount => _entries.Count;
        public int PendingLegacyAttackCount => _reservations.Count;

        public IntentObservation ObserveIntent(
            AttackerInstanceId attackerInstanceId,
            UnitData unit,
            AttackTargetBinding target,
            UnitActionPoseSample sample,
            bool targetAlive,
            bool targetValid,
            double serverTime,
            bool allowCommit = true)
        {
            if (!attackerInstanceId.IsValid || unit == null || !unit.IsAlive
                || !target.IsValid || !sample.IsValid || !IsFinite(serverTime))
            {
                return new IntentObservation(
                    UnitActionReducerStatus.InvalidInput, null, default,
                    false, false, "invalid-intent-input");
            }

            if (!UnitAttackShadowProfileResolver.TryResolveRuntime(
                    unit, out UnitAttackShadowProfile profile, out string profileReason))
            {
                return new IntentObservation(
                    UnitActionReducerStatus.UnsupportedDelivery, null, profile,
                    false, false, profileReason);
            }

            if (!_entries.TryGetValue(attackerInstanceId, out Entry entry))
            {
                entry = new Entry
                {
                    Unit = unit,
                    Profile = profile,
                    Sequencer = new UnitActionSequencer(attackerInstanceId, unit.Id)
                };
                _entries.Add(attackerInstanceId, entry);
            }
            else if (!ReferenceEquals(entry.Unit, unit) || entry.Unit.Id != unit.Id)
            {
                return new IntentObservation(
                    UnitActionReducerStatus.ScopeMismatch, entry.Sequencer.Snapshot, entry.Profile,
                    false, false, "attacker-instance-reused-with-different-unit");
            }

            UnitActionSnapshot before = entry.Sequencer.Snapshot;
            ulong revisionBeforeObservation = before.Revision;
            entry.LatestSimulationFacing = sample.SimulationFacing;
            if (before.Phase == UnitActionPhase.Windup
                || before.Phase == UnitActionPhase.Impact
                || before.Phase == UnitActionPhase.Recovery)
            {
                entry.Sequencer.Advance(before.Revision, serverTime);
                before = entry.Sequencer.Snapshot;
            }

            bool stationary = IsStationary(entry, sample.AttackerPosition);
            UnitActionReducerStatus status = UnitActionReducerStatus.NoChange;
            bool committedNow = false;

            if (before.Phase == UnitActionPhase.AlignToAttack && before.TargetBinding != target)
            {
                entry.Sequencer.CancelPreCommit(
                    before.Revision,
                    PreCommitCancelReason.InvalidTarget,
                    false,
                    serverTime);
                before = entry.Sequencer.Snapshot;
            }

            if (before.Phase == UnitActionPhase.Idle
                || before.Phase == UnitActionPhase.AcquireTarget
                || before.Phase == UnitActionPhase.Chase)
            {
                if (!TryCreatePlans(unit, out AttackTimelinePlan timeline, out AttackRangeProfile range))
                {
                    UpdateLastPosition(entry, sample.AttackerPosition);
                    return new IntentObservation(
                        UnitActionReducerStatus.InvalidInput, entry.Sequencer.Snapshot, profile,
                        stationary, false, "runtime-plan-invalid");
                }

                status = entry.Sequencer.BeginAttackAlignment(
                    before.Revision,
                    target,
                    profile.Delivery,
                    timeline,
                    range,
                    sample.SimulationFacing,
                    serverTime);
                // 새 공격 회차의 Align 진입은 이전 회차 Impact 증거의 명확한 수명 경계다.
                if (status == UnitActionReducerStatus.Accepted)
                    entry.LastEvaluatedHitIndex = -1;
                before = entry.Sequencer.Snapshot;
            }

            // 위치가 두 관측 사이에 변하지 않았고 5도 진입 경계 안에 있을 때만 Commit한다.
            // 실패 반환은 Legacy 공격을 막지 않으며 allocator도 소비하지 않는다.
            if (allowCommit
                && before.Phase == UnitActionPhase.AlignToAttack && before.TargetBinding == target
                && stationary)
            {
                status = entry.Sequencer.CommitAttack(
                    before.Revision,
                    target,
                    unit.IsAlive,
                    targetAlive,
                    targetValid,
                    sample.TargetSquaredDistance,
                    sample.FacingToAimYawDegrees,
                    serverTime,
                    serverTime);
                committedNow = status == UnitActionReducerStatus.Accepted;
            }

            UpdateLastPosition(entry, sample.AttackerPosition);
            UnitActionSnapshot snapshot = entry.Sequencer.Snapshot;
            bool shouldPublish = snapshot.Revision != revisionBeforeObservation;
            bool expectedDeferred = !committedNow
                && snapshot.Phase == UnitActionPhase.AlignToAttack
                && (!allowCommit
                    || status == UnitActionReducerStatus.NoChange
                    || status == UnitActionReducerStatus.InvalidInput);
            return new IntentObservation(
                status,
                snapshot,
                profile,
                stationary,
                committedNow,
                committedNow ? "shadow-commit" : "shadow-observed",
                CreatePublication(entry, snapshot),
                shouldPublish,
                expectedDeferred);
        }

        /// <summary>
        /// Legacy writer가 끝난 뒤 같은 예약의 authorization을 정식 결과로 닫은 관측값이다.
        /// Result는 복제·진단 전용이며 CompletionStatus가 실패해도 Legacy 피해를 되돌리지 않는다.
        /// </summary>
        public readonly struct ResultObservation
        {
            public UnitActionReducerStatus CompletionStatus { get; }
            public AttackImpactResult Result { get; }
            public AttackDeliveryKind Delivery { get; }
            public UnitActionSnapshot Snapshot { get; }
            public UnitAttackShadowPublication Publication { get; }
            public bool ShouldPublishSnapshot { get; }
            public string Reason { get; }

            public bool HasResult => Result != null;

            internal ResultObservation(
                UnitActionReducerStatus completionStatus,
                AttackImpactResult result,
                AttackDeliveryKind delivery,
                UnitActionSnapshot snapshot,
                string reason,
                UnitAttackShadowPublication publication = default,
                bool shouldPublishSnapshot = false)
            {
                CompletionStatus = completionStatus;
                Result = result;
                Delivery = delivery;
                Snapshot = snapshot;
                Reason = reason;
                Publication = publication;
                ShouldPublishSnapshot = shouldPublishSnapshot;
            }
        }

        /// <summary>
        /// Legacy가 실제 공격을 시작하는 단 하나의 production 경계에서 호출한다. B3 Action
        /// 소유권 아래의 정지·5도 통과 표본으로 독립 후보 회차를 Align→Commit하고, 성공한 뒤에만
        /// 최신 publication scope를 교체한다. 이전 예약은 자기 회차를 계속 소유한다.
        /// </summary>
        public IntentObservation PrepareLegacyAttack(
            AttackerInstanceId attackerInstanceId,
            UnitData unit,
            AttackTargetBinding target,
            UnitActionPoseSample sample,
            bool targetAlive,
            bool targetValid,
            double serverTime)
            => PrepareLegacyAttack(
                attackerInstanceId, unit, target, sample, targetAlive, targetValid,
                serverTime, 0d);

        /// <summary>
        /// Legacy scheduler가 계산한 overshoot를 회차 생성에 정확히 한 번 반영한다.
        /// CommitServerTime은 관측 시각을 유지하고, cooldown/impact offset만 앞당겨
        /// Legacy writer와 Shadow가 같은 due 경계를 공유한다.
        /// </summary>
        public IntentObservation PrepareLegacyAttack(
            AttackerInstanceId attackerInstanceId,
            UnitData unit,
            AttackTargetBinding target,
            UnitActionPoseSample sample,
            bool targetAlive,
            bool targetValid,
            double serverTime,
            double legacyOvershootSeconds)
        {
            if (!attackerInstanceId.IsValid || unit == null || !unit.IsAlive
                || !target.IsValid || !sample.IsValid || !IsFinite(serverTime)
                || !IsFinite(legacyOvershootSeconds)
                || legacyOvershootSeconds < 0d
                // 현행 Timeline은 양수 cooldown을 요구한다. 한 회차 이상을 건너뛴
                // 비정상 overshoot를 0초 회차로 위조하지 않고 Shadow만 fail-closed한다.
                || !IsFinite(unit.AttackCooldown)
                || legacyOvershootSeconds >= unit.AttackCooldown)
            {
                return new IntentObservation(
                    UnitActionReducerStatus.InvalidInput, null, default,
                    false, false, "invalid-legacy-boundary-input");
            }

            if (!UnitAttackShadowProfileResolver.TryResolveRuntime(
                    unit, out UnitAttackShadowProfile profile, out string profileReason))
            {
                return new IntentObservation(
                    UnitActionReducerStatus.UnsupportedDelivery, null, profile,
                    false, false, profileReason);
            }

            if (!_entries.TryGetValue(attackerInstanceId, out Entry entry))
            {
                entry = new Entry
                {
                    Unit = unit,
                    Profile = profile,
                    Sequencer = new UnitActionSequencer(attackerInstanceId, unit.Id)
                };
                _entries.Add(attackerInstanceId, entry);
            }
            else if (!ReferenceEquals(entry.Unit, unit) || entry.Unit.Id != unit.Id)
            {
                return new IntentObservation(
                    UnitActionReducerStatus.ScopeMismatch, entry.Sequencer.Snapshot, entry.Profile,
                    true, false, "attacker-instance-reused-with-different-unit");
            }

            if (entry.LastCommittedSequenceValue == ulong.MaxValue)
            {
                return new IntentObservation(
                    UnitActionReducerStatus.Exhausted, entry.Sequencer.Snapshot, entry.Profile,
                    true, false, "shadow-sequence-exhausted");
            }

            var nextSequence = new AttackSequenceId(entry.LastCommittedSequenceValue + 1UL);
            if (!UnitActionSequencer.TryCreateShadowCycle(
                    attackerInstanceId, unit.Id, nextSequence, out UnitActionSequencer candidate)
                || !TryCreateLegacyPlans(
                    unit, legacyOvershootSeconds,
                    out AttackTimelinePlan timeline, out AttackRangeProfile range))
            {
                return new IntentObservation(
                    UnitActionReducerStatus.InvalidInput, entry.Sequencer.Snapshot, entry.Profile,
                    true, false, "runtime-cycle-plan-invalid");
            }

            UnitActionReducerStatus begin = candidate.BeginAttackAlignment(
                candidate.Snapshot.Revision,
                target,
                profile.Delivery,
                timeline,
                range,
                sample.SimulationFacing,
                serverTime);
            if (begin != UnitActionReducerStatus.Accepted)
            {
                return new IntentObservation(
                    begin, entry.Sequencer.Snapshot, entry.Profile,
                    true, false, "legacy-boundary-align-rejected");
            }

            UnitActionReducerStatus commit = candidate.CommitAttack(
                candidate.Snapshot.Revision,
                target,
                unit.IsAlive,
                targetAlive,
                targetValid,
                sample.TargetSquaredDistance,
                sample.FacingToAimYawDegrees,
                serverTime,
                serverTime);
            if (commit != UnitActionReducerStatus.Accepted)
            {
                return new IntentObservation(
                    commit, entry.Sequencer.Snapshot, entry.Profile,
                    true, false, "legacy-boundary-commit-rejected");
            }

            // 후보 회차가 완전히 Commit된 뒤에만 publication scope를 교체한다.
            // 이전 예약은 자기 Sequencer를 계속 소유하므로 늦은 Impact도 새 타겟에 섞이지 않는다.
            entry.Sequencer = candidate;
            entry.LastCommittedSequenceValue = nextSequence.Value;
            entry.LatestSimulationFacing = sample.SimulationFacing;
            entry.LastEvaluatedHitIndex = -1;
            UpdateLastPosition(entry, sample.AttackerPosition);
            UnitActionSnapshot committed = candidate.Snapshot;
            return new IntentObservation(
                commit, committed, profile,
                true, true, "legacy-boundary-shadow-commit",
                CreatePublication(entry, committed), true);
        }

        /// <summary>
        /// Legacy 예약은 Shadow 회차 번호를 발급하지 않는다. production 경계에서 이미 Commit된
        /// 동일 Target 회차만 별도 Legacy token과 결속하며 sequence 0·target 불일치는 거부한다.
        /// </summary>
        public LegacyAttackToken ScheduleLegacyAttack(
            AttackerInstanceId attackerInstanceId,
            AttackTargetBinding target,
            int impactCount,
            double legacyScheduledAt,
            out UnitActionSnapshot snapshot)
        {
            snapshot = null;
            if (!attackerInstanceId.IsValid || !target.IsValid || impactCount <= 0 || impactCount > 64
                || !IsFinite(legacyScheduledAt)
                || !_entries.TryGetValue(attackerInstanceId, out Entry entry)
                || entry.LastLegacyToken == ulong.MaxValue)
                return LegacyAttackToken.None;

            snapshot = entry.Sequencer.Snapshot;
            if (!snapshot.SequenceId.IsValid
                || snapshot.TargetBinding != target
                || (snapshot.Phase != UnitActionPhase.Windup
                    && snapshot.Phase != UnitActionPhase.Impact
                    && snapshot.Phase != UnitActionPhase.Recovery))
                return LegacyAttackToken.None;
            ulong next = ++entry.LastLegacyToken;
            var token = new LegacyAttackToken(attackerInstanceId, next);
            var reservation = new LegacyReservation
            {
                Token = token,
                Target = target,
                ShadowSequenceAtSchedule = snapshot.SequenceId,
                LegacyScheduledAt = legacyScheduledAt,
                ShadowCommitAt = snapshot.CommitServerTime,
                ImpactCount = impactCount,
                Authorizations = new ImpactAuthorization[impactCount],
                Sequencer = entry.Sequencer
            };
            var key = new ReservationKey(token);
            _reservations[key] = reservation;
            _reservationOrder.Enqueue(key);
            TrimReservations();
            return token;
        }

        public DispatchObservation DispatchLegacyImpact(
            LegacyAttackToken token,
            int hitIndex,
            UnitActionPoseSample sample,
            bool targetAlive,
            bool targetValid,
            double serverTime)
            => DispatchLegacyImpact(
                token, hitIndex, sample, true, targetAlive, targetValid, serverTime);

        public DispatchObservation DispatchLegacyImpact(
            LegacyAttackToken token,
            int hitIndex,
            UnitActionPoseSample sample,
            bool attackerAlive,
            bool targetAlive,
            bool targetValid,
            double serverTime)
        {
            var key = new ReservationKey(token);
            if (!token.IsValid || !_reservations.TryGetValue(key, out LegacyReservation reservation)
                || !_entries.TryGetValue(token.AttackerInstanceId, out Entry entry))
            {
                return new DispatchObservation(
                    UnitActionReducerStatus.InvalidInput,
                    UnitActionReducerStatus.InvalidInput,
                    default, null, token, AttackSequenceId.None,
                    "legacy-reservation-missing");
            }

            if (hitIndex < 0 || hitIndex >= reservation.ImpactCount || !sample.IsValid || !IsFinite(serverTime))
            {
                return new DispatchObservation(
                    UnitActionReducerStatus.InvalidInput,
                    UnitActionReducerStatus.InvalidInput,
                    default, reservation.Sequencer != null
                        ? reservation.Sequencer.Snapshot
                        : entry.Sequencer.Snapshot, token,
                    reservation.ShadowSequenceAtSchedule,
                    "invalid-dispatch-input");
            }

            ulong hitBit = 1UL << hitIndex;
            if ((reservation.DispatchedMask & hitBit) != 0UL)
            {
                return new DispatchObservation(
                    UnitActionReducerStatus.Duplicate,
                    UnitActionReducerStatus.Duplicate,
                    default, reservation.Sequencer != null
                        ? reservation.Sequencer.Snapshot
                        : entry.Sequencer.Snapshot, token,
                    reservation.ShadowSequenceAtSchedule,
                    "duplicate-legacy-dispatch");
            }

            UnitActionSequencer reservationSequencer = reservation.Sequencer;
            if (reservationSequencer == null)
            {
                reservation.DispatchedMask |= hitBit;
                return new DispatchObservation(
                    UnitActionReducerStatus.InvalidInput,
                    UnitActionReducerStatus.InvalidInput,
                    default, entry.Sequencer.Snapshot, token,
                    reservation.ShadowSequenceAtSchedule,
                    "legacy-reservation-sequencer-missing");
            }

            UnitActionSnapshot current = reservationSequencer.Snapshot;
            ulong revisionBeforeDispatch = current.Revision;
            if (!reservation.ShadowSequenceAtSchedule.IsValid
                || current.SequenceId != reservation.ShadowSequenceAtSchedule
                || current.TargetBinding != reservation.Target)
            {
                reservation.DispatchedMask |= hitBit;
                return new DispatchObservation(
                    UnitActionReducerStatus.ScopeMismatch,
                    UnitActionReducerStatus.ScopeMismatch,
                    default, current, token, reservation.ShadowSequenceAtSchedule,
                    reservation.ShadowSequenceAtSchedule.IsValid
                        ? "shadow-sequence-or-target-mismatch"
                        : "legacy-started-before-shadow-aligned");
            }

            UnitActionReducerStatus advance = reservationSequencer.Advance(current.Revision, serverTime);
            current = reservationSequencer.Snapshot;
            ImpactAuthorization authorization = default;
            UnitActionReducerStatus evaluate = reservationSequencer.EvaluateImpact(
                current.Revision,
                hitIndex,
                DirectDamageEffectKind,
                DirectResultOrdinal,
                reservation.Target,
                attackerAlive,
                targetAlive,
                targetValid,
                sample.TargetSquaredDistance,
                sample.FacingToAimYawDegrees,
                sample.SimulationFacing,
                serverTime,
                out authorization);

            bool isCurrentPublicationScope = ReferenceEquals(entry.Sequencer, reservationSequencer);
            if (evaluate == UnitActionReducerStatus.Accepted && isCurrentPublicationScope)
            {
                entry.LastEvaluatedHitIndex = hitIndex;
                entry.LatestSimulationFacing = sample.SimulationFacing;
            }

            reservation.DispatchedMask |= hitBit;
            if (evaluate == UnitActionReducerStatus.Accepted)
                reservation.Authorizations[hitIndex] = authorization;
            return new DispatchObservation(
                advance,
                evaluate,
                authorization,
                reservationSequencer.Snapshot,
                token,
                reservation.ShadowSequenceAtSchedule,
                evaluate == UnitActionReducerStatus.Accepted
                    ? "shadow-impact-evaluated"
                    : "shadow-impact-rejected",
                isCurrentPublicationScope
                    ? CreatePublication(entry, reservationSequencer.Snapshot)
                    : default,
                isCurrentPublicationScope
                    && reservationSequencer.Snapshot.Revision != revisionBeforeDispatch);
        }

        /// <summary>
        /// 커밋된 Impact 시점에 공격자 또는 타겟이 이미 제거되어 pose를 만들 수 없는 경우를
        /// 같은 token/hit의 정규 Miss authorization으로 닫는다. 이 경계는 사거리·방향을
        /// 추측하지 않으며, 정확히 한쪽의 생명주기 부재만 허용한다. 양쪽 부재나 일반 내부
        /// 실패는 원인을 임의 선택하지 않고 fail-closed한다.
        /// </summary>
        public DispatchObservation DispatchLegacyUnavailableImpact(
            LegacyAttackToken token,
            int hitIndex,
            AttackDamageApplyStatus unavailableStatus,
            double serverTime)
        {
            var key = new ReservationKey(token);
            if (!token.IsValid || !_reservations.TryGetValue(key, out LegacyReservation reservation)
                || !_entries.TryGetValue(token.AttackerInstanceId, out Entry entry))
            {
                return new DispatchObservation(
                    UnitActionReducerStatus.InvalidInput,
                    UnitActionReducerStatus.InvalidInput,
                    default, null, token, AttackSequenceId.None,
                    "legacy-reservation-missing");
            }

            if (hitIndex < 0 || hitIndex >= reservation.ImpactCount || !IsFinite(serverTime)
                || (unavailableStatus != AttackDamageApplyStatus.AttackerUnavailable
                    && unavailableStatus != AttackDamageApplyStatus.TargetUnavailable))
            {
                return new DispatchObservation(
                    UnitActionReducerStatus.InvalidInput,
                    UnitActionReducerStatus.InvalidInput,
                    default, reservation.Sequencer != null
                        ? reservation.Sequencer.Snapshot
                        : entry.Sequencer.Snapshot,
                    token, reservation.ShadowSequenceAtSchedule,
                    "invalid-unavailable-dispatch-input");
            }

            ulong hitBit = 1UL << hitIndex;
            if ((reservation.DispatchedMask & hitBit) != 0UL)
            {
                return new DispatchObservation(
                    UnitActionReducerStatus.Duplicate,
                    UnitActionReducerStatus.Duplicate,
                    default, reservation.Sequencer != null
                        ? reservation.Sequencer.Snapshot
                        : entry.Sequencer.Snapshot,
                    token, reservation.ShadowSequenceAtSchedule,
                    "duplicate-legacy-dispatch");
            }

            UnitActionSequencer sequencer = reservation.Sequencer;
            if (sequencer == null)
            {
                reservation.DispatchedMask |= hitBit;
                return new DispatchObservation(
                    UnitActionReducerStatus.InvalidInput,
                    UnitActionReducerStatus.InvalidInput,
                    default, entry.Sequencer.Snapshot,
                    token, reservation.ShadowSequenceAtSchedule,
                    "legacy-reservation-sequencer-missing");
            }

            UnitActionSnapshot current = sequencer.Snapshot;
            ulong revisionBeforeDispatch = current.Revision;
            if (!reservation.ShadowSequenceAtSchedule.IsValid
                || current.SequenceId != reservation.ShadowSequenceAtSchedule
                || current.TargetBinding != reservation.Target)
            {
                reservation.DispatchedMask |= hitBit;
                return new DispatchObservation(
                    UnitActionReducerStatus.ScopeMismatch,
                    UnitActionReducerStatus.ScopeMismatch,
                    default, current, token, reservation.ShadowSequenceAtSchedule,
                    "shadow-sequence-or-target-mismatch");
            }

            UnitActionReducerStatus advance = sequencer.Advance(current.Revision, serverTime);
            current = sequencer.Snapshot;
            bool attackerAlive = unavailableStatus != AttackDamageApplyStatus.AttackerUnavailable;
            bool targetAlive = unavailableStatus != AttackDamageApplyStatus.TargetUnavailable;
            bool targetValid = targetAlive;

            // unavailable 판정은 reducer에서 공간값을 소비하지 않는다. 커밋 시 보존한 유효한
            // SimulationFacing만 authorization의 정규 방향 증거로 사용하고 임의 pose는 만들지 않는다.
            UnitActionReducerStatus evaluate = sequencer.EvaluateImpact(
                current.Revision,
                hitIndex,
                DirectDamageEffectKind,
                DirectResultOrdinal,
                reservation.Target,
                attackerAlive,
                targetAlive,
                targetValid,
                0d,
                0d,
                current.SimulationFacing,
                serverTime,
                out ImpactAuthorization authorization);

            bool isCurrentPublicationScope = ReferenceEquals(entry.Sequencer, sequencer);
            reservation.DispatchedMask |= hitBit;
            if (evaluate == UnitActionReducerStatus.Accepted)
                reservation.Authorizations[hitIndex] = authorization;
            return new DispatchObservation(
                advance,
                evaluate,
                authorization,
                sequencer.Snapshot,
                token,
                reservation.ShadowSequenceAtSchedule,
                evaluate == UnitActionReducerStatus.Accepted
                    ? "shadow-unavailable-impact-evaluated"
                    : "shadow-unavailable-impact-rejected",
                isCurrentPublicationScope
                    ? CreatePublication(entry, sequencer.Snapshot)
                    : default,
                isCurrentPublicationScope
                    && sequencer.Snapshot.Revision != revisionBeforeDispatch);
        }

        /// <summary>
        /// 서버 권위 Impact 판정을 Legacy 단일 피해 writer가 소비할 수 있는 명시 상태로 변환한다.
        /// reducer가 Accepted가 아니거나 authorization이 불완전하면 피해를 추측하지 않는다.
        /// </summary>
        public static AttackDamageApplyStatus ResolveLegacyWriterAuthorization(
            DispatchObservation observation)
        {
            if (observation.EvaluateStatus != UnitActionReducerStatus.Accepted
                || !observation.Authorization.IsValid)
                return AttackDamageApplyStatus.AuthorizationUnavailable;

            if (observation.Authorization.Outcome == ImpactAuthorizationOutcome.AuthorizedHit)
                return AttackDamageApplyStatus.Applied;

            if (observation.Authorization.Outcome != ImpactAuthorizationOutcome.AuthorizedMiss)
                return AttackDamageApplyStatus.AuthorizationUnavailable;

            switch (observation.Authorization.MissReason)
            {
                case ImpactAuthorizationMissReason.AttackerUnavailable:
                    return AttackDamageApplyStatus.AttackerUnavailable;
                case ImpactAuthorizationMissReason.TargetUnavailable:
                    return AttackDamageApplyStatus.TargetUnavailable;
                case ImpactAuthorizationMissReason.CombatConditionFailed:
                    return AttackDamageApplyStatus.CombatConditionFailed;
                default:
                    return AttackDamageApplyStatus.AuthorizationUnavailable;
            }
        }

        /// <summary>
        /// Unity 코루틴의 상대 대기가 끝났더라도 NGO 서버 시각이 아직 예약 Impact 시각보다
        /// 이르면 dispatch하지 않는다. 같은 절대 시각 경계를 production과 회귀 검증이 공유한다.
        /// </summary>
        public static bool HasReachedLegacyImpactTime(
            double observedServerTime,
            double dueServerTime)
            => IsFinite(observedServerTime)
                && IsFinite(dueServerTime)
                && observedServerTime >= dueServerTime;

        /// <summary>
        /// 공격자 사망 시 폐기된 future hit인지 확인한다. 취소 bit는 지우지 않으므로 같은
        /// 지연 작업이 중복으로 깨어나도 항상 같은 결론을 반환하며 writer/result로 진행하지 않는다.
        /// </summary>
        public bool TryConsumeLegacyImpactCancellation(LegacyAttackToken token, int hitIndex)
        {
            if (!token.IsValid
                || hitIndex < 0
                || hitIndex >= 64)
                return false;

            var key = new ReservationKey(token);
            ulong hitBit = 1UL << hitIndex;
            if (_reservations.TryGetValue(key, out LegacyReservation reservation))
            {
                if (hitIndex >= reservation.ImpactCount
                    || (reservation.CancelledMask & hitBit) == 0UL)
                    return false;

                reservation.CancellationConsumedMask |= hitBit;
                RetireReservationIfComplete(key, reservation);
                return true;
            }

            // 모든 future coroutine이 종료되어 live reservation이 퇴역한 뒤에도 중복 wake는
            // bounded tombstone에서 같은 취소 결론을 얻는다.
            return _retiredCancellationMasks.TryGetValue(key, out ulong cancelledMask)
                && (cancelledMask & hitBit) != 0UL;
        }

        /// <summary>
        /// Legacy 피해 writer가 반환한 값을 같은 token/hit authorization의 정식 Shadow 결과로 만든다.
        /// dispatch와 completion을 분리해 결과가 확정되기 전에 예약이 폐기되는 일을 막는다.
        /// </summary>
        public ResultObservation CompleteLegacyImpact(
            LegacyAttackToken token,
            int hitIndex,
            AttackDamageObservation legacy,
            double serverTime)
        {
            var key = new ReservationKey(token);
            if (!token.IsValid || !_reservations.TryGetValue(key, out LegacyReservation reservation)
                || !_entries.TryGetValue(token.AttackerInstanceId, out Entry entry))
            {
                return new ResultObservation(
                    UnitActionReducerStatus.InvalidInput, null, AttackDeliveryKind.None,
                    null, "legacy-reservation-missing-at-completion");
            }

            UnitActionSequencer sequencer = reservation.Sequencer;
            UnitActionSnapshot snapshot = sequencer != null ? sequencer.Snapshot : entry.Sequencer.Snapshot;
            if (hitIndex < 0 || hitIndex >= reservation.ImpactCount || !legacy.IsValid
                || !IsFinite(serverTime))
            {
                return new ResultObservation(
                    UnitActionReducerStatus.InvalidInput, null, entry.Profile.Delivery,
                    snapshot, "invalid-completion-input");
            }

            ulong hitBit = 1UL << hitIndex;
            if ((reservation.CompletedMask & hitBit) != 0UL)
            {
                return new ResultObservation(
                    UnitActionReducerStatus.Duplicate, null, entry.Profile.Delivery,
                    snapshot, "duplicate-legacy-completion");
            }

            reservation.CompletedMask |= hitBit;

            // 살아 있는 객체의 pose 획득 실패는 타격 시간이 이르다는 뜻이 아니다.
            // 피해 0은 유지하되 NotDue와 분리된 명시적 내부 실패로 예약을 닫는다.
            if (legacy.Status == AttackDamageApplyStatus.PoseUnavailable)
            {
                RetireReservationIfComplete(key, reservation);
                return new ResultObservation(
                    UnitActionReducerStatus.InvalidInput,
                    null,
                    entry.Profile.Delivery,
                    snapshot,
                    "impact-pose-capture-failure");
            }

            ImpactAuthorization authorization = reservation.Authorizations[hitIndex];
            if ((reservation.DispatchedMask & hitBit) == 0UL)
            {
                RetireReservationIfComplete(key, reservation);
                return new ResultObservation(
                    UnitActionReducerStatus.InvalidInput, null, entry.Profile.Delivery,
                    snapshot, "completion-without-dispatch");
            }
            if (sequencer == null)
            {
                RetireReservationIfComplete(key, reservation);
                return new ResultObservation(
                    UnitActionReducerStatus.InvalidInput, null, entry.Profile.Delivery,
                    snapshot, "completion-without-sequencer");
            }
            if (!authorization.IsValid)
            {
                RetireReservationIfComplete(key, reservation);
                return new ResultObservation(
                    UnitActionReducerStatus.InvalidInput, null, entry.Profile.Delivery,
                    snapshot, "completion-without-accepted-authorization");
            }

            AttackImpactOutcome outcome;
            if (authorization.Outcome == ImpactAuthorizationOutcome.AuthorizedHit
                && legacy.Status == AttackDamageApplyStatus.Applied)
            {
                outcome = legacy.AppliedAmount > 0
                    ? AttackImpactOutcome.HitApplied
                    : AttackImpactOutcome.StatusEffectApplied;
            }
            else if (authorization.Outcome == ImpactAuthorizationOutcome.AuthorizedMiss
                && ((authorization.MissReason
                            == ImpactAuthorizationMissReason.TargetUnavailable
                        && legacy.Status == AttackDamageApplyStatus.TargetUnavailable)
                    || (authorization.MissReason
                             == ImpactAuthorizationMissReason.AttackerUnavailable
                        && legacy.Status == AttackDamageApplyStatus.AttackerUnavailable)
                    || (authorization.MissReason
                            == ImpactAuthorizationMissReason.CombatConditionFailed
                        && legacy.Status == AttackDamageApplyStatus.CombatConditionFailed)))
            {
                outcome = AttackImpactOutcome.Miss;
            }
            else
            {
                // authorization과 실제 Legacy writer 결과가 서로 다른 사실을 주장하면
                // Miss/HitApplied로 위조하지 않는다. Legacy gameplay 결과는 이미 끝났으므로
                // 롤백하거나 재호출하지 않고 Shadow confirmation만 닫는다.
                RetireReservationIfComplete(key, reservation);
                return new ResultObservation(
                    UnitActionReducerStatus.InvalidInput, null, entry.Profile.Delivery,
                    snapshot, "authorization-result-outcome-mismatch");
            }

            bool created = AttackImpactResult.TryCreate(
                authorization.ActionRevision,
                authorization.Key,
                authorization.ImpactServerTime,
                authorization.AimDirection,
                legacy.HasImpactPosition,
                legacy.ImpactPosition,
                outcome,
                legacy.AppliedAmount,
                legacy.ResultingHp,
                out AttackImpactResult result);
            if (!created)
            {
                RetireReservationIfComplete(key, reservation);
                return new ResultObservation(
                    UnitActionReducerStatus.InvalidInput, null, entry.Profile.Delivery,
                    sequencer.Snapshot, "impact-result-contract-rejected");
            }

            ulong revisionBeforeCompletion = sequencer.Snapshot.Revision;
            UnitActionReducerStatus completion = sequencer.ConfirmImpactResult(
                revisionBeforeCompletion, result, serverTime);
            bool currentScope = ReferenceEquals(entry.Sequencer, sequencer);
            UnitActionSnapshot completedSnapshot = sequencer.Snapshot;
            RetireReservationIfComplete(key, reservation);
            return new ResultObservation(
                completion,
                result,
                entry.Profile.Delivery,
                completedSnapshot,
                completion == UnitActionReducerStatus.Accepted
                    ? "shadow-impact-result-confirmed"
                    : "shadow-impact-result-rejected",
                currentScope ? CreatePublication(entry, completedSnapshot) : default,
                currentScope && completedSnapshot.Revision != revisionBeforeCompletion);
        }

        public bool TryCreatePublication(
            AttackerInstanceId attackerInstanceId,
            UnitActionSnapshot snapshot,
            out UnitAttackShadowPublication publication)
        {
            publication = default;
            if (snapshot == null
                || !_entries.TryGetValue(attackerInstanceId, out Entry entry)
                || snapshot.AttackerInstanceId != attackerInstanceId)
                return false;
            publication = CreatePublication(entry, snapshot);
            return publication.IsValid;
        }

        private static UnitAttackShadowPublication CreatePublication(
            Entry entry,
            UnitActionSnapshot snapshot)
            => new UnitAttackShadowPublication(
                snapshot,
                entry.LatestSimulationFacing.IsValid
                    ? entry.LatestSimulationFacing
                    : snapshot.SimulationFacing,
                entry.LastEvaluatedHitIndex);

        public UnitActionSnapshot MarkDead(AttackerInstanceId attackerInstanceId)
        {
            if (!_entries.TryGetValue(attackerInstanceId, out Entry entry)) return null;

            // 예약 코루틴 자체는 NetworkCombatController가 소유하므로 여기서 중단할 수 없다.
            // 대신 아직 dispatch되지 않은 비독립 타격을 명시적으로 취소해, 코루틴이 깨어났을 때
            // pose·피해 writer·결과·observer보다 먼저 조용히 종료할 수 있게 한다.
            foreach (KeyValuePair<ReservationKey, LegacyReservation> pair in _reservations)
            {
                if (pair.Key.Attacker != attackerInstanceId) continue;

                LegacyReservation reservation = pair.Value;
                ulong all = reservation.ImpactCount == 64
                    ? ulong.MaxValue
                    : (1UL << reservation.ImpactCount) - 1UL;
                reservation.CancelledMask |= all & ~reservation.DispatchedMask;

                UnitActionSequencer sequencer = reservation.Sequencer;
                if (sequencer != null)
                {
                    UnitActionSnapshot reservedSnapshot = sequencer.Snapshot;
                    sequencer.MarkDead(reservedSnapshot.Revision);
                }
            }

            UnitActionSnapshot snapshot = entry.Sequencer.Snapshot;
            entry.Sequencer.MarkDead(snapshot.Revision);
            return entry.Sequencer.Snapshot;
        }

        public UnitActionSnapshot StopCombat(
            AttackerInstanceId attackerInstanceId,
            double serverTime)
        {
            if (!_entries.TryGetValue(attackerInstanceId, out Entry entry) || !IsFinite(serverTime))
                return null;
            UnitActionSnapshot snapshot = entry.Sequencer.Snapshot;
            if (snapshot.Phase == UnitActionPhase.AlignToAttack
                || snapshot.Phase == UnitActionPhase.Chase)
            {
                entry.Sequencer.CancelPreCommit(
                    snapshot.Revision,
                    PreCommitCancelReason.InvalidTarget,
                    false,
                    serverTime);
            }
            else if (snapshot.Phase == UnitActionPhase.Windup
                || snapshot.Phase == UnitActionPhase.Impact)
            {
                // 표시 타겟/다음 후보 생명주기가 먼저 끝나도 Schedule 당시 Legacy Impact는
                // 자기 TargetId와 Sequencer를 Impact까지 소유한다. 공격자 사망/despawn은
                // Retire가 예약까지 제거하므로 이 표시 중단 경계와 구분한다.
                if (!HasPendingReservation(entry.Sequencer))
                    entry.Sequencer.CancelCommitted(snapshot.Revision, serverTime);
            }
            return entry.Sequencer.Snapshot;
        }

        private bool HasPendingReservation(UnitActionSequencer sequencer)
        {
            foreach (LegacyReservation reservation in _reservations.Values)
            {
                if (ReferenceEquals(reservation.Sequencer, sequencer))
                    return true;
            }
            return false;
        }

        public void Retire(AttackerInstanceId attackerInstanceId)
        {
            RemoveReservations(attackerInstanceId);
            _entries.Remove(attackerInstanceId);
        }

        public void Clear()
        {
            _entries.Clear();
            _reservations.Clear();
            _reservationOrder.Clear();
            _retiredCancellationMasks.Clear();
            _retiredCancellationOrder.Clear();
        }

        private static bool TryCreatePlans(
            UnitData unit,
            out AttackTimelinePlan timeline,
            out AttackRangeProfile range)
        {
            timeline = null;
            range = default;
            if (unit.HitFrameTimes == null) return false;
            double[] offsets = new double[unit.HitFrameTimes.Length];
            for (int index = 0; index < offsets.Length; index++)
                offsets[index] = unit.HitFrameTimes[index];
            return AttackTimelinePlan.TryCreate(
                    unit.AttackCooldown,
                    unit.AttackCooldown,
                    offsets,
                    out timeline)
                && AttackRangeProfile.TryCreate(
                    unit.AttackRange,
                    HexMetrics.TileHeight,
                    out range);
        }

        private static bool TryCreateLegacyPlans(
            UnitData unit,
            double legacyOvershootSeconds,
            out AttackTimelinePlan timeline,
            out AttackRangeProfile range)
        {
            timeline = null;
            range = default;
            if (unit == null || unit.HitFrameTimes == null
                || !IsFinite(legacyOvershootSeconds)
                || legacyOvershootSeconds < 0d
                || !IsFinite(unit.AttackCooldown)
                || legacyOvershootSeconds >= unit.AttackCooldown)
                return false;

            double effectiveCooldown = Math.Max(
                0d, (double)unit.AttackCooldown - legacyOvershootSeconds);
            double[] offsets = new double[unit.HitFrameTimes.Length];
            for (int index = 0; index < offsets.Length; index++)
            {
                double sourceOffset = unit.HitFrameTimes[index];
                if (!IsFinite(sourceOffset)) return false;
                offsets[index] = Math.Max(0d, sourceOffset - legacyOvershootSeconds);
            }

            return AttackTimelinePlan.TryCreateLegacyAdjusted(
                    effectiveCooldown,
                    effectiveCooldown,
                    offsets,
                    out timeline)
                && AttackRangeProfile.TryCreate(
                    unit.AttackRange,
                    HexMetrics.TileHeight,
                    out range);
        }

        private static bool IsStationary(Entry entry, WorldPointXZ position)
        {
            if (!entry.HasLastPosition || !entry.LastPosition.IsValid || !position.IsValid)
                return false;
            double dx = position.X - entry.LastPosition.X;
            double dz = position.Z - entry.LastPosition.Z;
            return dx * dx + dz * dz <= StationaryPositionEpsilonSquared;
        }

        private static void UpdateLastPosition(Entry entry, WorldPointXZ position)
        {
            entry.LastPosition = position;
            entry.HasLastPosition = position.IsValid;
        }

        private void TrimReservations()
        {
            while (_reservations.Count > MaximumPendingLegacyAttacks && _reservationOrder.Count > 0)
            {
                ReservationKey oldest = _reservationOrder.Dequeue();
                if (_reservations.TryGetValue(oldest, out LegacyReservation reservation)
                    && reservation.CancelledMask != 0UL)
                    AddCancellationTombstone(oldest, reservation.CancelledMask);
                _reservations.Remove(oldest);
            }
        }

        private void RetireReservationIfComplete(ReservationKey key, LegacyReservation reservation)
        {
            ulong all = reservation.ImpactCount == 64
                ? ulong.MaxValue
                : (1UL << reservation.ImpactCount) - 1UL;
            if (((reservation.CompletedMask | reservation.CancellationConsumedMask) & all) == all)
            {
                _reservations.Remove(key);
                if (reservation.CancelledMask != 0UL)
                    AddCancellationTombstone(key, reservation.CancelledMask);
            }
        }

        private void AddCancellationTombstone(ReservationKey key, ulong cancelledMask)
        {
            if (cancelledMask == 0UL) return;
            if (!_retiredCancellationMasks.ContainsKey(key))
                _retiredCancellationOrder.Enqueue(key);
            _retiredCancellationMasks[key] = cancelledMask;
            while (_retiredCancellationMasks.Count > MaximumPendingLegacyAttacks
                && _retiredCancellationOrder.Count > 0)
                _retiredCancellationMasks.Remove(_retiredCancellationOrder.Dequeue());
        }

        private void RemoveReservations(AttackerInstanceId attackerInstanceId)
        {
            var remove = new List<ReservationKey>();
            foreach (KeyValuePair<ReservationKey, LegacyReservation> pair in _reservations)
            {
                if (pair.Key.Attacker == attackerInstanceId)
                    remove.Add(pair.Key);
            }
            for (int index = 0; index < remove.Count; index++)
                _reservations.Remove(remove[index]);
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
