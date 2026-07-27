using System;

namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>
    /// 서버가 관측한 순수 값만 받아 한 유닛의 공격 행동 상태를 전진시키는 reducer다.
    /// Unity, NGO, Animator, 피해 writer를 호출하지 않으며 적중 가능 여부를 Authorization으로만 내보낸다.
    /// 실제 피해와 AttackImpactResult 생성은 외부 서버 권위 writer의 책임이다.
    /// </summary>
    public sealed class UnitActionSequencer
    {
        private readonly AttackerInstanceId _attackerInstanceId;
        private readonly int _attackerId;
        private readonly AttackSequenceAllocator _sequenceAllocator;

        private ulong _revision;
        private UnitActionPhase _phase = UnitActionPhase.Idle;
        private AttackTargetBinding _target = AttackTargetBinding.None;
        private AttackDeliveryKind _delivery;
        private AttackSequenceId _sequenceId = AttackSequenceId.None;
        private AttackTimelinePlan _timeline;
        private AttackRangeProfile _rangeProfile;
        private ActionDirectionXZ _simulationFacing;
        private double _phaseStartServerTime;
        private bool _hasSimulationAimDirection;
        private ActionDirectionXZ _simulationAimDirection;
        private double _startServerTime;
        private double _commitServerTime;
        private double _cooldownEndServerTime;
        private double _recoveryEndServerTime;
        private bool _cooldownConsumed;
        private ulong _dueHitMask;
        private ulong _decidedHitMask;
        private ulong _confirmedHitMask;
        private ulong _authorizedHitMask;
        private ulong _authorizedMissMask;
        private readonly ulong[] _authorizedRevisions = new ulong[64];
        private readonly AttackResultKey[] _authorizedKeys = new AttackResultKey[64];
        private int _lastConfirmedHitIndex = -1;
        private UnitActionEndReason _endReason;
        private bool _hasAcceptedTime;
        private double _lastAcceptedServerTime;

        public UnitActionSnapshot Snapshot => CreateSnapshot();

        public UnitActionSequencer(AttackerInstanceId attackerInstanceId, int attackerId)
            : this(attackerInstanceId, attackerId, 1UL, 0UL)
        {
        }

        private UnitActionSequencer(
            AttackerInstanceId attackerInstanceId,
            int attackerId,
            ulong initialRevision,
            ulong initialSequenceValue)
        {
            if (!attackerInstanceId.IsValid)
                throw new ArgumentException("Sequencer에는 유효한 공격자 개체 식별자가 필요하다.", nameof(attackerInstanceId));
            if (attackerId < 0)
                throw new ArgumentOutOfRangeException(nameof(attackerId), "공격자 ID는 0 이상이어야 한다.");
            if (initialRevision == 0UL)
                throw new ArgumentOutOfRangeException(nameof(initialRevision), "행동 revision은 0이 될 수 없다.");

            _attackerInstanceId = attackerInstanceId;
            _attackerId = attackerId;
            _revision = initialRevision;
            _sequenceAllocator = new AttackSequenceAllocator(initialSequenceValue);
        }

        /// <summary>
        /// 숫자 경계의 fail-closed 동작을 순수 C# 검증에서 재현하기 위한 생성 진입점이다.
        /// 런타임 상태 복원 API가 아니며, 전달된 값을 그대로 다음 revision/sequence 경계로 사용한다.
        /// </summary>
        public static UnitActionSequencer CreateForValidation(
            AttackerInstanceId attackerInstanceId,
            int attackerId,
            ulong initialRevision,
            ulong initialSequenceValue)
        {
            return new UnitActionSequencer(
                attackerInstanceId, attackerId, initialRevision, initialSequenceValue);
        }

        /// <summary>
        /// 이미 외부에서 정한 다음 공격 회차와 정확히 같은 번호를 발급하는 runtime shadow cycle을 만든다.
        /// revision은 항상 1에서 시작하며 잘못된 식별자나 공격자 ID는 예외 없이 false로 거부한다.
        /// </summary>
        public static bool TryCreateShadowCycle(
            AttackerInstanceId attackerInstanceId,
            int attackerId,
            AttackSequenceId nextSequenceId,
            out UnitActionSequencer sequencer)
        {
            sequencer = null;
            if (!attackerInstanceId.IsValid || attackerId < 0 || !nextSequenceId.IsValid)
                return false;

            // nextSequenceId는 1 이상이므로 -1은 underflow하지 않는다. MaxValue도 MaxValue-1 seed에서
            // 정확히 한 번 발급할 수 있고, 그 다음 발급만 allocator가 Exhausted로 막는다.
            sequencer = new UnitActionSequencer(
                attackerInstanceId, attackerId, 1UL, nextSequenceId.Value - 1UL);
            return true;
        }

        /// <summary>유효한 타겟과 공격 계획을 고정하기 전 AlignToAttack 단계에 진입한다.</summary>
        public UnitActionReducerStatus BeginAttackAlignment(
            ulong expectedRevision,
            AttackTargetBinding target,
            AttackDeliveryKind delivery,
            AttackTimelinePlan timeline,
            AttackRangeProfile rangeProfile,
            ActionDirectionXZ simulationFacing,
            double observedServerTime)
        {
            UnitActionReducerStatus guard = Guard(expectedRevision, observedServerTime);
            if (guard != UnitActionReducerStatus.Accepted) return guard;
            if (!target.IsValid || timeline == null || !rangeProfile.IsValid || !simulationFacing.IsValid)
                return UnitActionReducerStatus.InvalidInput;
            if (target.Mode != AttackTargetMode.TargetLocked)
                return UnitActionReducerStatus.UnsupportedTargetBinding;
            if (!IsSupportedDelivery(delivery)) return UnitActionReducerStatus.UnsupportedDelivery;
            if (_phase != UnitActionPhase.Idle && _phase != UnitActionPhase.AcquireTarget
                && _phase != UnitActionPhase.Chase)
                return UnitActionReducerStatus.InvalidPhase;

            // 새 정렬은 이전 공격 회차의 시간·마스크·조준을 먼저 원자적으로 제거한 뒤 시작한다.
            ResetCycleToNeutral(true, true);
            _phase = UnitActionPhase.AlignToAttack;
            _phaseStartServerTime = observedServerTime;
            _target = target;
            _delivery = delivery;
            _timeline = timeline;
            _rangeProfile = rangeProfile;
            _simulationFacing = simulationFacing;
            _startServerTime = observedServerTime;
            _endReason = UnitActionEndReason.None;
            AcceptTime(observedServerTime);
            return UnitActionReducerStatus.Accepted;
        }

        /// <summary>
        /// 5도 정렬, 동일 타겟, 생존·유효·v2 사거리 조건을 모두 만족할 때만 공격 회차를 커밋한다.
        /// 실패 입력은 상태나 회차 번호를 전혀 바꾸지 않는다.
        /// </summary>
        public UnitActionReducerStatus CommitAttack(
            ulong expectedRevision,
            AttackTargetBinding target,
            bool attackerAlive,
            bool targetAlive,
            bool targetValid,
            double targetSquaredDistance,
            double yawErrorDegrees,
            double commitServerTime,
            double observedServerTime)
        {
            UnitActionReducerStatus guard = Guard(expectedRevision, observedServerTime);
            if (guard != UnitActionReducerStatus.Accepted) return guard;
            if (_phase != UnitActionPhase.AlignToAttack) return UnitActionReducerStatus.InvalidPhase;
            if (target != _target) return UnitActionReducerStatus.ScopeMismatch;
            if (!ContractNumber.IsFinite(commitServerTime) || commitServerTime > observedServerTime
                || (_hasAcceptedTime && commitServerTime < _lastAcceptedServerTime))
                return UnitActionReducerStatus.InvalidInput;
            if (!attackerAlive || !targetAlive || !targetValid
                || !ContractNumber.IsFinite(yawErrorDegrees)
                || !UnitActionAngleHysteresis.AllowsAttackAlignment(yawErrorDegrees, false)
                || !_rangeProfile.ContainsSquaredDistance(targetSquaredDistance, _target.Target.Kind))
                return UnitActionReducerStatus.InvalidInput;

            double cooldownEndServerTime = commitServerTime + _timeline.CooldownSeconds;
            double recoveryEndServerTime = commitServerTime + _timeline.RecoveryEndOffset;
            if (!ContractNumber.IsFinite(cooldownEndServerTime)
                || !ContractNumber.IsFinite(recoveryEndServerTime))
                return UnitActionReducerStatus.InvalidInput;

            if (!_sequenceAllocator.TryNext(_attackerInstanceId, out AttackSequenceId nextSequenceId))
                return UnitActionReducerStatus.Exhausted;

            _sequenceId = nextSequenceId;
            _phase = UnitActionPhase.Windup;
            _phaseStartServerTime = commitServerTime;
            _commitServerTime = commitServerTime;
            _cooldownEndServerTime = cooldownEndServerTime;
            _recoveryEndServerTime = recoveryEndServerTime;
            _cooldownConsumed = true;
            _dueHitMask = 0UL;
            _decidedHitMask = 0UL;
            _confirmedHitMask = 0UL;
            _authorizedHitMask = 0UL;
            _authorizedMissMask = 0UL;
            Array.Clear(_authorizedRevisions, 0, _authorizedRevisions.Length);
            Array.Clear(_authorizedKeys, 0, _authorizedKeys.Length);
            _lastConfirmedHitIndex = -1;
            _endReason = UnitActionEndReason.None;
            AcceptTime(observedServerTime);
            return UnitActionReducerStatus.Accepted;
        }

        /// <summary>
        /// 커밋 전 취소를 적용한다. AttackRange만 벗어났고 LoseRange 안이면 타겟을 유지한 채 Chase로,
        /// 나머지 무효·사망·LoseRange 이탈은 타겟을 지우고 AcquireTarget으로 돌아간다.
        /// </summary>
        public UnitActionReducerStatus CancelPreCommit(
            ulong expectedRevision,
            PreCommitCancelReason reason,
            bool targetWithinLoseRange,
            double observedServerTime)
        {
            UnitActionReducerStatus guard = Guard(expectedRevision, observedServerTime);
            if (guard != UnitActionReducerStatus.Accepted) return guard;
            if (!Enum.IsDefined(typeof(PreCommitCancelReason), reason))
                return UnitActionReducerStatus.InvalidInput;
            if (_phase != UnitActionPhase.AlignToAttack && _phase != UnitActionPhase.Chase)
                return UnitActionReducerStatus.InvalidPhase;

            bool chase = reason == PreCommitCancelReason.AttackRangeExited && targetWithinLoseRange;
            UnitActionPhase nextPhase = chase ? UnitActionPhase.Chase : UnitActionPhase.AcquireTarget;
            AttackTargetBinding nextTarget = chase ? _target : AttackTargetBinding.None;
            if (_phase == nextPhase && _target == nextTarget) return UnitActionReducerStatus.NoChange;

            if (chase)
                ResetExecutionState(false);
            else
                ResetCycleToNeutral(true, true);

            _phase = nextPhase;
            _phaseStartServerTime = observedServerTime;
            _target = nextTarget;
            _endReason = UnitActionEndReason.PreCommitCancelled;
            AcceptTime(observedServerTime);
            return UnitActionReducerStatus.Accepted;
        }

        /// <summary>현재 서버 시각까지 도달한 모든 HitIndex를 한 번에 due로 표시한다.</summary>
        public UnitActionReducerStatus Advance(ulong expectedRevision, double observedServerTime)
        {
            UnitActionReducerStatus guard = Guard(expectedRevision, observedServerTime);
            if (guard != UnitActionReducerStatus.Accepted) return guard;

            if (_phase == UnitActionPhase.Windup || _phase == UnitActionPhase.Impact)
            {
                ulong due = _dueHitMask;
                for (int hitIndex = 0; hitIndex < _timeline.ImpactCount; hitIndex++)
                {
                    if (observedServerTime >= _commitServerTime + _timeline.GetImpactOffset(hitIndex))
                        due |= 1UL << hitIndex;
                }

                if (due != _dueHitMask)
                {
                    _dueHitMask = due;
                    if (_phase != UnitActionPhase.Impact)
                        _phaseStartServerTime = observedServerTime;
                    _phase = UnitActionPhase.Impact;
                    AcceptTime(observedServerTime);
                    return UnitActionReducerStatus.Accepted;
                }
            }
            else if (_phase == UnitActionPhase.Recovery)
            {
                double actionEnd = Math.Max(_cooldownEndServerTime, _recoveryEndServerTime);
                if (observedServerTime >= actionEnd)
                {
                    UnitActionEndReason completedReason = _endReason == UnitActionEndReason.None
                        ? UnitActionEndReason.Completed
                        : _endReason;
                    ResetCycleToNeutral(true, true);
                    _phase = UnitActionPhase.AcquireTarget;
                    _phaseStartServerTime = observedServerTime;
                    _endReason = completedReason;
                    AcceptTime(observedServerTime);
                    return UnitActionReducerStatus.Accepted;
                }
            }

            return UnitActionReducerStatus.NoChange;
        }

        /// <summary>
        /// due가 된 가장 이른 미결정 타격을 8도 유지·타겟 생존·v2 사거리로 독립 판정한다.
        /// 적중이어도 피해 결과를 만들지 않고 외부 writer가 처리할 Authorization만 반환한다.
        /// </summary>
        public UnitActionReducerStatus EvaluateImpact(
            ulong expectedRevision,
            int hitIndex,
            int expectedEffectKind,
            int expectedResultOrdinal,
            AttackTargetBinding target,
            bool targetAlive,
            bool targetValid,
            double targetSquaredDistance,
            double yawErrorDegrees,
            ActionDirectionXZ authoritativeAimDirection,
            double observedServerTime,
            out ImpactAuthorization authorization)
        {
            authorization = default;
            UnitActionReducerStatus guard = Guard(expectedRevision, observedServerTime);
            if (guard != UnitActionReducerStatus.Accepted) return guard;
            if (_phase != UnitActionPhase.Impact && _phase != UnitActionPhase.Windup)
                return UnitActionReducerStatus.InvalidPhase;
            if (target != _target) return UnitActionReducerStatus.ScopeMismatch;
            if (hitIndex < 0 || expectedEffectKind < 0 || expectedResultOrdinal < 0
                || _timeline == null || hitIndex >= _timeline.ImpactCount
                || !authoritativeAimDirection.IsValid || !ContractNumber.IsFinite(yawErrorDegrees))
                return UnitActionReducerStatus.InvalidInput;

            ulong bit = 1UL << hitIndex;
            if ((_dueHitMask & bit) == 0UL) return UnitActionReducerStatus.NotDue;
            if ((_decidedHitMask & bit) != 0UL) return UnitActionReducerStatus.Duplicate;

            int firstPending = FindFirstSetIndex(_dueHitMask & ~_decidedHitMask);
            if (firstPending != hitIndex) return UnitActionReducerStatus.OutOfOrder;

            bool hit = targetAlive && targetValid
                && _rangeProfile.ContainsSquaredDistance(targetSquaredDistance, target.Target.Kind)
                && UnitActionAngleHysteresis.AllowsAttackAlignment(yawErrorDegrees, true);
            ImpactAuthorizationOutcome outcome = hit
                ? ImpactAuthorizationOutcome.AuthorizedHit
                : ImpactAuthorizationOutcome.AuthorizedMiss;

            // Authorization은 이 전이가 수락된 직후의 revision과 결합한다. 결과 writer는 이 값을
            // 그대로 돌려줘야 하므로 같은 회차·타격 번호의 오래된 결과도 재사용될 수 없다.
            ulong authorizationRevision = _revision + 1UL;
            var authorizationKey = new AttackResultKey(
                _attackerInstanceId, _sequenceId, hitIndex,
                (int)_target.Target.Kind, _target.Target.Id,
                expectedEffectKind, expectedResultOrdinal);
            authorization = new ImpactAuthorization(
                authorizationRevision, authorizationKey, _target.Target,
                observedServerTime, authoritativeAimDirection, outcome);
            _decidedHitMask |= bit;
            _authorizedRevisions[hitIndex] = authorizationRevision;
            _authorizedKeys[hitIndex] = authorizationKey;
            if (hit) _authorizedHitMask |= bit;
            else _authorizedMissMask |= bit;
            _hasSimulationAimDirection = true;
            _simulationAimDirection = authoritativeAimDirection;
            if ((_decidedHitMask & _timeline.AllImpactMask) == _timeline.AllImpactMask)
            {
                _phase = UnitActionPhase.Recovery;
                _phaseStartServerTime = observedServerTime;
            }
            else
                _phase = UnitActionPhase.Impact;

            AcceptTime(observedServerTime);
            return UnitActionReducerStatus.Accepted;
        }

        /// <summary>
        /// 외부 writer가 만든 결과가 현재 회차·타겟·타격 authorization과 정확히 일치할 때만 확인한다.
        /// 결과 도착은 역순이어도 허용하지만 LastConfirmedHitIndex는 0부터 연속된 prefix만 전진한다.
        /// </summary>
        public UnitActionReducerStatus ConfirmImpactResult(
            ulong expectedRevision,
            AttackImpactResult result,
            double observedServerTime)
        {
            if (expectedRevision != _revision) return UnitActionReducerStatus.StaleRevision;
            if (_revision == ulong.MaxValue) return UnitActionReducerStatus.Exhausted;
            if (result == null || !result.Key.IsValid) return UnitActionReducerStatus.InvalidInput;
            UnitActionReducerStatus timeGuard = GuardTime(observedServerTime);
            if (timeGuard != UnitActionReducerStatus.Accepted) return timeGuard;
            if (result.ImpactServerTime < _commitServerTime || result.ImpactServerTime > observedServerTime)
                return UnitActionReducerStatus.InvalidInput;
            if (result.Key.AttackerInstanceId != _attackerInstanceId || result.Key.SequenceId != _sequenceId)
                return UnitActionReducerStatus.ScopeMismatch;
            if (result.Key.HitIndex < 0 || _timeline == null || result.Key.HitIndex >= _timeline.ImpactCount)
                return UnitActionReducerStatus.ScopeMismatch;
            if (result.Key.VictimKind != (int)_target.Target.Kind || result.Key.VictimId != _target.Target.Id)
                return UnitActionReducerStatus.ScopeMismatch;

            ulong bit = 1UL << result.Key.HitIndex;
            if ((_confirmedHitMask & bit) != 0UL) return UnitActionReducerStatus.Duplicate;
            ulong authorizedRevision = _authorizedRevisions[result.Key.HitIndex];
            if (authorizedRevision == 0UL) return UnitActionReducerStatus.NotDue;
            if (result.ActionRevision != authorizedRevision) return UnitActionReducerStatus.ScopeMismatch;
            if (!result.Key.Equals(_authorizedKeys[result.Key.HitIndex]))
                return UnitActionReducerStatus.ScopeMismatch;
            bool hitOutcome = result.Outcome == AttackImpactOutcome.HitApplied
                || result.Outcome == AttackImpactOutcome.Evaded
                || result.Outcome == AttackImpactOutcome.Immune
                || result.Outcome == AttackImpactOutcome.StatusEffectApplied;
            bool outcomeMatches = ((_authorizedHitMask & bit) != 0UL && hitOutcome)
                || ((_authorizedMissMask & bit) != 0UL && result.Outcome == AttackImpactOutcome.Miss);
            if (!outcomeMatches) return UnitActionReducerStatus.ScopeMismatch;

            _confirmedHitMask |= bit;
            while (_lastConfirmedHitIndex + 1 < _timeline.ImpactCount)
            {
                ulong nextBit = 1UL << (_lastConfirmedHitIndex + 1);
                if ((_confirmedHitMask & nextBit) == 0UL) break;
                _lastConfirmedHitIndex++;
            }

            AcceptTime(observedServerTime);
            return UnitActionReducerStatus.Accepted;
        }

        /// <summary>커밋 뒤 남은 타격을 억제하고 쿨다운을 환불하지 않은 채 Recovery로 전환한다.</summary>
        public UnitActionReducerStatus CancelCommitted(
            ulong expectedRevision,
            double observedServerTime)
        {
            UnitActionReducerStatus guard = Guard(expectedRevision, observedServerTime);
            if (guard != UnitActionReducerStatus.Accepted) return guard;
            if (_phase != UnitActionPhase.Windup && _phase != UnitActionPhase.Impact)
                return UnitActionReducerStatus.InvalidPhase;

            _decidedHitMask |= _timeline.AllImpactMask;
            _phase = UnitActionPhase.Recovery;
            _phaseStartServerTime = observedServerTime;
            _endReason = UnitActionEndReason.CommittedCancelled;
            AcceptTime(observedServerTime);
            return UnitActionReducerStatus.Accepted;
        }

        /// <summary>시간 입력 없이 공격자를 Dead terminal 상태로 만든다.</summary>
        public UnitActionReducerStatus MarkDead(ulong expectedRevision)
        {
            if (expectedRevision != _revision) return UnitActionReducerStatus.StaleRevision;
            if (_phase == UnitActionPhase.Dead) return UnitActionReducerStatus.NoChange;
            if (_revision == ulong.MaxValue) return UnitActionReducerStatus.Exhausted;

            // 이미 권한을 발급한 타격의 대상·회차·revision은 보존한다. 아직 미결정인 타격만
            // decided 처리하여 사망 뒤 새 피해 권한이 생기는 것을 차단한다.
            if (_timeline != null) _decidedHitMask |= _timeline.AllImpactMask;
            _phase = UnitActionPhase.Dead;
            _phaseStartServerTime = _hasAcceptedTime ? _lastAcceptedServerTime : 0d;
            _endReason = UnitActionEndReason.AttackerDead;
            _revision++;
            return UnitActionReducerStatus.Accepted;
        }

        private UnitActionReducerStatus Guard(ulong expectedRevision, double observedServerTime)
        {
            if (expectedRevision != _revision) return UnitActionReducerStatus.StaleRevision;
            if (_phase == UnitActionPhase.Dead) return UnitActionReducerStatus.DeadTerminal;
            if (_revision == ulong.MaxValue) return UnitActionReducerStatus.Exhausted;
            return GuardTime(observedServerTime);
        }

        private UnitActionReducerStatus GuardTime(double observedServerTime)
        {
            if (!ContractNumber.IsFinite(observedServerTime)) return UnitActionReducerStatus.InvalidInput;
            if (_hasAcceptedTime && observedServerTime < _lastAcceptedServerTime)
                return UnitActionReducerStatus.InvalidInput;
            return UnitActionReducerStatus.Accepted;
        }

        private void AcceptTime(double observedServerTime)
        {
            _hasAcceptedTime = true;
            _lastAcceptedServerTime = observedServerTime;
            _revision++;
        }

        private static bool IsSupportedDelivery(AttackDeliveryKind delivery)
            => delivery == AttackDeliveryKind.MeleeContact || delivery == AttackDeliveryKind.Hitscan;

        private static int FindFirstSetIndex(ulong mask)
        {
            for (int index = 0; index < 64; index++)
            {
                if ((mask & (1UL << index)) != 0UL) return index;
            }
            return -1;
        }

        /// <summary>타겟과 공격 계획은 유지하면서 커밋 이후에만 생기는 실행 데이터를 초기화한다.</summary>
        private void ResetExecutionState(bool clearFacing)
        {
            _sequenceId = AttackSequenceId.None;
            _commitServerTime = 0d;
            _cooldownEndServerTime = 0d;
            _recoveryEndServerTime = 0d;
            _cooldownConsumed = false;
            _dueHitMask = 0UL;
            _decidedHitMask = 0UL;
            _confirmedHitMask = 0UL;
            _authorizedHitMask = 0UL;
            _authorizedMissMask = 0UL;
            Array.Clear(_authorizedRevisions, 0, _authorizedRevisions.Length);
            Array.Clear(_authorizedKeys, 0, _authorizedKeys.Length);
            _lastConfirmedHitIndex = -1;
            _hasSimulationAimDirection = false;
            _simulationAimDirection = default;
            if (clearFacing) _simulationFacing = default;
        }

        /// <summary>
        /// 한 공격 회차가 끝날 때 다음 Align이 과거 데이터에 의존하지 않도록 관련 상태를 한 번에 중립화한다.
        /// </summary>
        private void ResetCycleToNeutral(bool clearTarget, bool clearFacing)
        {
            ResetExecutionState(clearFacing);
            if (clearTarget) _target = AttackTargetBinding.None;
            _delivery = AttackDeliveryKind.None;
            _timeline = null;
            _rangeProfile = default;
            _startServerTime = 0d;
            _phaseStartServerTime = 0d;
        }

        private UnitActionSnapshot CreateSnapshot()
        {
            return new UnitActionSnapshot(
                _revision, _attackerInstanceId, _attackerId, _phase, _target, _delivery,
                _sequenceId, _timeline, _rangeProfile, _simulationFacing,
                _phaseStartServerTime, _hasSimulationAimDirection, _simulationAimDirection,
                _startServerTime, _commitServerTime, _cooldownEndServerTime,
                _recoveryEndServerTime, _cooldownConsumed, _dueHitMask,
                _decidedHitMask, _confirmedHitMask, _lastConfirmedHitIndex, _endReason);
        }
    }
}
