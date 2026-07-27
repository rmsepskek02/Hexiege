using System;
using Hexiege.Application.Combat.Sequencing;
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// Tracer A의 순수 C# 계약을 Unity Editor에서 즉시 점검하는 도구다.
    /// 씬, 프리팹, Animator, NGO 실행 환경 없이도 행동 단계 순서, 공격 회차 증가,
    /// 각도 히스테리시스, 결과 중복 제거와 역순 정렬이 계약대로 동작하는지 확인한다.
    /// 메뉴에서 Hexiege/Combat/Run Unit Action Self Validation을 선택해 실행한다.
    /// </summary>
    public static class RunUnitActionSelfValidation
    {
        [MenuItem("Hexiege/Combat/Run Unit Action Self Validation")]
        public static void Run()
        {
            ValidatePhaseVocabularyAndOrdinals();
            ValidateMonotonicSequences();
            ValidateMovementHysteresis();
            ValidateAttackHysteresis();
            ValidateDuplicateAndReorderedResults();
            ValidateA1ContractsAndReducer();

            Debug.Log("[UAS-DIAG] self-validation PASS: A1 contracts/reducer, A2 pure pose sample, phase vocabulary/ordinal, revision/time fail-closed, 5/8 attack, cancel/dead, multi-hit/result confirmation, bounded dedupe/reorder, v2 range divergence.");
        }

        /// <summary>
        /// 상태머신의 모든 전이를 검증하는 테스트가 아니다.
        /// Tracer A가 합의한 phase 이름과 직렬화 순번만 바뀌지 않았는지 확인한다.
        /// </summary>
        private static void ValidatePhaseVocabularyAndOrdinals()
        {
            UnitActionPhase[] expected =
            {
                UnitActionPhase.Idle,
                UnitActionPhase.Navigate,
                UnitActionPhase.AlignToMove,
                UnitActionPhase.Move,
                UnitActionPhase.AcquireTarget,
                UnitActionPhase.Chase,
                UnitActionPhase.AlignToAttack,
                UnitActionPhase.Windup,
                UnitActionPhase.Impact,
                UnitActionPhase.Recovery,
                UnitActionPhase.Dead
            };

            Require(Enum.GetValues(typeof(UnitActionPhase)).Length == expected.Length,
                "UnitActionPhase member count changed unexpectedly.");

            for (int index = 0; index < expected.Length; index++)
            {
                Require((int)expected[index] == index,
                    $"UnitActionPhase ordering changed at index {index}.");
            }
        }

        private static void ValidateMonotonicSequences()
        {
            var allocator = new AttackSequenceAllocator();
            var firstInstance = new AttackerInstanceId(7UL);
            var secondInstance = new AttackerInstanceId(9UL);
            AttackSequenceId attackerSevenFirst = allocator.Next(firstInstance);
            AttackSequenceId attackerSevenSecond = allocator.Next(firstInstance);
            AttackSequenceId attackerNineFirst = allocator.Next(secondInstance);

            Require(attackerSevenFirst.IsValid, "First sequence must be valid.");
            Require(attackerSevenSecond.CompareTo(attackerSevenFirst) > 0,
                "Sequence must increase for the same attacker.");
            Require(attackerNineFirst.Value == 1UL,
                "Sequence counters must be independent per attacker.");
        }

        private static void ValidateMovementHysteresis()
        {
            Require(UnitActionAngleHysteresis.AllowsMovement(10d, false),
                "Movement must enter at 10 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsMovement(10.001d, false),
                "Movement must not enter above 10 degrees.");
            Require(UnitActionAngleHysteresis.AllowsMovement(15d, true),
                "Movement must remain active at 15 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsMovement(15.001d, true),
                "Movement must stop above 15 degrees.");
            Require(UnitActionAngleHysteresis.AllowsMovement(350d, false),
                "350 degrees must normalize to the 10-degree movement entry boundary.");
            Require(!UnitActionAngleHysteresis.AllowsMovement(double.PositiveInfinity, true),
                "Infinite movement error must fail closed.");
        }

        private static void ValidateAttackHysteresis()
        {
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(-5d, false),
                "Attack alignment must enter at an absolute error of 5 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(5.001d, false),
                "Attack alignment must not enter above 5 degrees.");
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(-8d, true),
                "Attack alignment must remain active at an absolute error of 8 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(8.001d, true),
                "Attack alignment must exit above 8 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(double.NaN, true),
                "NaN must fail closed.");
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(355d, false),
                "355 degrees must normalize to the 5-degree attack entry boundary.");
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(352d, true),
                "352 degrees must normalize to the 8-degree attack exit boundary.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(double.NegativeInfinity, true),
                "Infinite attack error must fail closed.");
        }

        private static void ValidateDuplicateAndReorderedResults()
        {
            var instance = new AttackerInstanceId(7UL);
            var sequence = new AttackSequenceId(4UL);
            var buffer = new AttackResultOrderBuffer(instance, sequence, 100d);
            var first = new AttackResultKey(instance, sequence, 0, 1, 30, 2, 0);
            var second = new AttackResultKey(instance, sequence, 1, 1, 30, 2, 0);
            var laterOrdinal = new AttackResultKey(instance, sequence, 1, 1, 30, 2, 1);
            var firstCopy = new AttackResultKey(instance, sequence, 0, 1, 30, 2, 0);
            var otherInstance = new AttackResultKey(new AttackerInstanceId(8UL), sequence, 0, 1, 30, 2, 0);

            Require(first.Equals(firstCopy) && first.GetHashCode() == firstCopy.GetHashCode(),
                "The canonical identity fields must produce equal keys and hashes.");
            Require(!first.Equals(otherInstance) && first.CompareTo(otherInstance) != 0,
                "Attacker instance must participate in identity and ordering.");

            Require(buffer.TryAdd(laterOrdinal, 100.1d) == AttackResultBufferAddStatus.Accepted,
                "First arrival must be accepted.");
            Require(buffer.TryAdd(second, 100.2d) == AttackResultBufferAddStatus.Accepted,
                "Reordered hit must be accepted.");
            Require(buffer.TryAdd(first, 100.3d) == AttackResultBufferAddStatus.Accepted,
                "Earlier reordered hit must be accepted.");
            Require(buffer.TryAdd(second, 100.4d) == AttackResultBufferAddStatus.Duplicate,
                "Duplicate result key must be rejected.");
            Require(buffer.Count == 3, "Duplicate result changed buffer count.");
            Require(buffer.GetAt(0).Equals(first), "First hit was not restored to deterministic order.");
            Require(buffer.GetAt(1).Equals(second), "Second hit was not restored to deterministic order.");
            Require(buffer.GetAt(2).Equals(laterOrdinal), "Result ordinal was not restored to deterministic order.");

            var otherScope = new AttackResultKey(instance, new AttackSequenceId(5UL), 0, 1, 31, 2, 0);
            Require(buffer.TryAdd(otherScope, 100.5d) == AttackResultBufferAddStatus.ScopeMismatch,
                "A result from another attack sequence must be rejected.");
            Require(buffer.TryAdd(otherInstance, 100.5d) == AttackResultBufferAddStatus.ScopeMismatch,
                "A result from another attacker instance must be rejected.");

            buffer.Clear();
            Require(buffer.Count == 0, "Clear must remove all buffered results.");
            Require(buffer.TryAdd(first, 100.6d) == AttackResultBufferAddStatus.Accepted,
                "The fixed scope must remain usable after Clear.");

            var capacityBuffer = new AttackResultOrderBuffer(instance, sequence, 200d);
            for (int ordinal = 0; ordinal < AttackResultOrderBuffer.MaximumResultCount; ordinal++)
            {
                var key = new AttackResultKey(instance, sequence, 0, 1, ordinal, 2, ordinal);
                Require(capacityBuffer.TryAdd(key, 200.5d) == AttackResultBufferAddStatus.Accepted,
                    $"Result {ordinal} within the 64-result limit must be accepted.");
            }

            var overflow = new AttackResultKey(instance, sequence, 0, 1, 999, 2, 64);
            Require(capacityBuffer.TryAdd(overflow, 200.5d) == AttackResultBufferAddStatus.CapacityExceeded,
                "The 65th unique result must be rejected.");

            var expiryBuffer = new AttackResultOrderBuffer(instance, sequence, 300d);
            Require(expiryBuffer.TryAdd(first, 301d) == AttackResultBufferAddStatus.Accepted,
                "A result arriving before expiry must be buffered.");
            Require(expiryBuffer.TryAdd(second, 302d) == AttackResultBufferAddStatus.Expired,
                "A result arriving at the two-second lifetime boundary must be rejected.");
            Require(expiryBuffer.Count == 0,
                "Expiry must discard results that were buffered before the lifetime boundary.");
        }

        /// <summary>
        /// A1에서 추가한 순수 값 계약과 reducer의 핵심 불변을 한 장면이나 네트워크 없이 검증한다.
        /// 실제 피해 성공은 만들지 않고 외부 writer가 돌려준 결과를 확인하는 단계까지만 다룬다.
        /// </summary>
        private static void ValidateA1ContractsAndReducer()
        {
            ValidateA1ValueContractsAndRange();
            ValidateA1ReducerRevisionCommitAndMultiHit();
            ValidateA1ReducerCancellationAndDeath();
            ValidateA1OverflowAndAtomicReset();
            CheckA2PoseContracts();
        }

        /// <summary>A2 pose 값은 Unity 없이 원점·정규화·거리·yaw와 optional 이동 방향을 검증한다.</summary>
        private static void CheckA2PoseContracts()
        {
            var target = new EntityRef(EntityKind.Unit, 0);
            Require(UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    0d, 2d,
                    0d, 2d,
                    false, double.NaN, double.NaN,
                    out UnitActionPoseSample withoutDesired),
                "World origin attacker and optional missing desired direction must remain valid.");
            Require(withoutDesired.IsValid
                && !withoutDesired.HasDesiredMoveDirection
                && !withoutDesired.DesiredMoveDirection.IsValid
                && Math.Abs(withoutDesired.SimulationFacing.Z - 1d) < 0.000001d
                && Math.Abs(withoutDesired.TargetAimDirection.Z - 1d) < 0.000001d
                && Math.Abs(withoutDesired.TargetSquaredDistance - 4d) < 0.000001d
                && Math.Abs(withoutDesired.FacingToAimYawDegrees) < 0.000001d,
                "Pose must normalize facing/aim and calculate squared distance and zero yaw.");

            Require(UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    0d, 1d,
                    2d, 0d,
                    true, 3d, 4d,
                    out UnitActionPoseSample withDesired),
                "Finite pose with an explicit desired move target must be valid.");
            Require(withDesired.HasDesiredMoveDirection
                && Math.Abs(withDesired.DesiredMoveDirection.X - 0.6d) < 0.000001d
                && Math.Abs(withDesired.DesiredMoveDirection.Z - 0.8d) < 0.000001d
                && Math.Abs(withDesired.FacingToAimYawDegrees - 90d) < 0.000001d
                && Math.Abs(withDesired.TargetSquaredDistance - 4d) < 0.000001d,
                "Desired direction must normalize and facing-to-aim yaw must be 90 degrees.");

            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 0d, 1d, 0d, false, 0d, 0d, out _),
                "Zero facing must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 0d, 0d, false, 0d, 0d, out _),
                "A target at the attacker XZ must fail closed because aim is undefined.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, double.NaN, 1d, false, 0d, 0d, out _),
                "NaN target position must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1d, 0d, true, 0d, 0d, out _),
                "An explicitly present zero desired direction must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    EntityRef.None, 0d, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _)
                && !UnitActionPoseSample.TryCreate(
                    new EntityRef((EntityKind)999, 1), 0d, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _),
                "None and unknown target kinds must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, double.NaN, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _)
                && !UnitActionPoseSample.TryCreate(
                    target, double.PositiveInfinity, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _),
                "NaN and infinite attacker coordinates must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1e200d, 1e200d, false, 0d, 0d, out _),
                "Squared target distance overflow must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1d, 0d, true, double.NaN, 1d, out _)
                && !UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1d, 0d, true, double.PositiveInfinity, 1d, out _),
                "Present desired target NaN and infinity must fail closed.");
            Require(UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 0d, -2d, false, 0d, 0d,
                    out UnitActionPoseSample opposite)
                && Math.Abs(opposite.FacingToAimYawDegrees - 180d) < 0.000001d,
                "Opposite facing-to-aim yaw must be 180 degrees.");
        }

        private static void ValidateA1ValueContractsAndRange()
        {
            var zeroIdUnit = new EntityRef(EntityKind.Unit, 0);
            Require(zeroIdUnit.IsValid && !zeroIdUnit.IsNone, "Entity ID 0 must remain valid.");
            Require(EntityRef.None.IsNone && !EntityRef.None.IsValid, "None must be represented by EntityKind.None.");

            Require(!ActionDirectionXZ.TryCreate(0d, 0d, out _), "Zero direction must be rejected.");
            Require(!ActionDirectionXZ.TryCreate(double.NaN, 1d, out _), "NaN direction must be rejected.");
            Require(!ActionDirectionXZ.TryCreate(double.PositiveInfinity, 1d, out _), "Infinite direction must be rejected.");
            Require(ActionDirectionXZ.TryCreate(3d, 4d, out ActionDirectionXZ direction),
                "Finite non-zero direction must be created.");
            Require(Math.Abs(direction.X - 0.6d) < 0.000001d && Math.Abs(direction.Z - 0.8d) < 0.000001d,
                "Direction must be normalized.");
            Require(WorldPointXZ.TryCreate(0d, 0d, out WorldPointXZ origin) && origin.IsValid,
                "World origin must be a valid point.");
            Require(!WorldPointXZ.TryCreate(double.NaN, 0d, out _), "NaN point must be rejected.");

            double[] sourceOffsets = { 0.2d, 0.8d };
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, sourceOffsets, out AttackTimelinePlan timeline),
                "A valid multi-hit timeline must be created.");
            sourceOffsets[0] = 1.5d;
            Require(Math.Abs(timeline.GetImpactOffset(0) - 0.2d) < 0.000001d,
                "Timeline must defensively copy marker offsets.");
            Require(!AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.8d, 0.8d }, out _),
                "Duplicate timeline offsets must be rejected.");
            Require(!AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.8d, 0.2d }, out _),
                "Descending timeline offsets must be rejected.");
            Require(!AttackTimelinePlan.TryCreate(1d, 1d, new[] { 1d }, out _),
                "A marker at the cooldown boundary must be rejected.");
            Require(!AttackTimelinePlan.TryCreate(2d, 0.7d, new[] { 0.8d }, out _),
                "Recovery before the last marker must be rejected.");

            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile melee),
                "The v2 melee range profile must be created.");
            Require(melee.UsesMeleeContact, "AttackRange 0.5 must use v2 MeleeContact.");
            Require(Math.Abs(melee.GetMaximumDistance(EntityKind.Unit) - 0.68d) < 0.000001d,
                "The v2 unit melee boundary must be 0.63 + 0.05.");
            Require(Math.Abs(melee.GetMaximumDistance(EntityKind.Building) - 0.88d) < 0.000001d,
                "The v2 building melee boundary must also include radius 0.20.");
            Require(melee.ContainsSquaredDistance(0.50d * 0.50d, EntityKind.Unit)
                && !(0.50d * 0.50d <= 0.35d * 0.35d),
                "Distance 0.50 must expose v2 true versus Legacy unit 0.35 false.");
            Require(melee.ContainsSquaredDistance(0.70d * 0.70d, EntityKind.Building)
                && !(0.70d * 0.70d <= 0.55d * 0.55d),
                "Distance 0.70 must expose v2 true versus Legacy building 0.55 false.");
            Require(AttackRangeProfile.TryCreate(0.5001d, 1d, out AttackRangeProfile classifierConflict)
                && !classifierConflict.UsesMeleeContact && 0.5001d < 1d,
                "AttackRange 0.5001 must expose the v2 <=0.5 versus Legacy <1 classifier conflict.");
            Require(AttackRangeProfile.TryCreate(1d, 1d, out AttackRangeProfile spearRange)
                && !spearRange.UsesMeleeContact
                && Math.Abs(spearRange.GetMaximumDistance(EntityKind.Unit) - 1.05d) < 0.000001d,
                "Actual SpearMan AttackRange 1 must use the scaled-center v2 range mode.");
            Require(!AttackRangeProfile.TryCreate(double.MaxValue, 2d, out _),
                "Range multiplication overflow must fail closed.");
            Require(!AttackRangeProfile.TryCreate(1e200d, 1d, out _),
                "Range squared-distance overflow must fail closed.");

            var unsupported = new UnitActionSequencer(new AttackerInstanceId(100UL), 0);
            var target = new AttackTargetBinding(AttackTargetMode.TargetLocked, zeroIdUnit);
            UnitActionSnapshot initial = unsupported.Snapshot;
            Require(unsupported.BeginAttackAlignment(
                    initial.Revision, target, AttackDeliveryKind.ProjectileImpact,
                    timeline, melee, direction, 1d) == UnitActionReducerStatus.UnsupportedDelivery,
                "A1 reducer must reject unsupported ProjectileImpact delivery.");
            Require(unsupported.Snapshot.Revision == initial.Revision && initial.Phase == UnitActionPhase.Idle,
                "Rejected delivery must not mutate the immutable initial snapshot.");

            var lockedPoint = new AttackTargetBinding(AttackTargetMode.LockedPoint, zeroIdUnit);
            var homing = new AttackTargetBinding(AttackTargetMode.Homing, zeroIdUnit);
            Require(lockedPoint.IsValid && homing.IsValid,
                "Future target binding vocabulary must remain representable by the contract.");
            Require(unsupported.BeginAttackAlignment(
                    initial.Revision, lockedPoint, AttackDeliveryKind.MeleeContact,
                    timeline, melee, direction, 1d) == UnitActionReducerStatus.UnsupportedTargetBinding,
                "A1 reducer must explicitly reject LockedPoint binding.");
            Require(unsupported.BeginAttackAlignment(
                    initial.Revision, homing, AttackDeliveryKind.MeleeContact,
                    timeline, melee, direction, 1d) == UnitActionReducerStatus.UnsupportedTargetBinding,
                "A1 reducer must explicitly reject Homing binding.");
        }

        private static void ValidateA1ReducerRevisionCommitAndMultiHit()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "Test facing must be valid.");
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.2d, 0.8d }, out AttackTimelinePlan timeline),
                "Test timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile range),
                "Test range must be valid.");

            var target = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 0));
            var otherTarget = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 1));
            var reducer = new UnitActionSequencer(new AttackerInstanceId(200UL), 0);
            UnitActionSnapshot initial = reducer.Snapshot;

            Require(initial.Revision == 1UL && initial.LastConfirmedHitIndex == -1,
                "Initial revision must be 1 and confirmed prefix must be -1.");
            Require(reducer.BeginAttackAlignment(
                    initial.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 10d) == UnitActionReducerStatus.Accepted,
                "Alignment must be accepted.");
            UnitActionSnapshot aligned = reducer.Snapshot;
            Require(aligned.Revision == 2UL && aligned.Phase == UnitActionPhase.AlignToAttack,
                "Accepted alignment must increment revision exactly once.");
            Require(initial.Revision == 1UL && initial.Phase == UnitActionPhase.Idle,
                "Previously captured snapshot must remain immutable.");

            Require(reducer.BeginAttackAlignment(
                    1UL, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 10d) == UnitActionReducerStatus.StaleRevision,
                "Stale revision must fail closed.");
            Require(reducer.Advance(aligned.Revision, 10d) == UnitActionReducerStatus.NoChange
                && reducer.Snapshot.Revision == aligned.Revision,
                "No-op must not increment revision.");
            Require(reducer.Advance(aligned.Revision, 9.9d) == UnitActionReducerStatus.InvalidInput,
                "Backward observed time must fail closed.");
            Require(reducer.Advance(aligned.Revision, double.NaN) == UnitActionReducerStatus.InvalidInput,
                "NaN observed time must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, double.NaN, 10d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "NaN yaw must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 10d, double.PositiveInfinity)
                == UnitActionReducerStatus.InvalidInput,
                "Infinite observed time must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 9.9d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "Commit time before last accepted time must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 10.2d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "Commit time after observed now must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, otherTarget, true, true, true, 0.25d, 5d, 10d, 10.1d)
                == UnitActionReducerStatus.ScopeMismatch,
                "Commit must not replace the aligned target.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5.001d, 10d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "Commit above the 5-degree boundary must fail.");
            Require(reducer.Snapshot.Revision == aligned.Revision,
                "Rejected commit attempts must not mutate revision.");

            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 10d, 10.1d)
                == UnitActionReducerStatus.Accepted,
                "Commit at the 5-degree boundary must succeed.");
            UnitActionSnapshot committed = reducer.Snapshot;
            Require(committed.SequenceId.IsValid && committed.CooldownConsumed
                && committed.TargetBinding == target && committed.Phase == UnitActionPhase.Windup,
                "Commit must consume cooldown, allocate one sequence, and lock the target.");

            Require(reducer.Advance(committed.Revision, 10.9d) == UnitActionReducerStatus.Accepted,
                "A time leap must mark every elapsed hit due in one revision.");
            UnitActionSnapshot due = reducer.Snapshot;
            Require(due.DueHitMask == 3UL, "Both multi-hit markers must become due.");

            Require(reducer.EvaluateImpact(
                    due.Revision, 0, 7, 11, otherTarget, true, true, 0.25d, 8d, facing, 10.91d, out _)
                == UnitActionReducerStatus.ScopeMismatch
                && reducer.Snapshot.TargetBinding == target,
                "Committed TargetLocked binding must reject target transfer without mutation.");

            Require(reducer.EvaluateImpact(
                    due.Revision, 1, 8, 12, target, true, true, 0.25d, 8d, facing, 10.91d, out _)
                == UnitActionReducerStatus.OutOfOrder,
                "Due hits must be decided in HitIndex order.");
            Require(reducer.EvaluateImpact(
                    due.Revision, 0, 7, 11, target, true, true, 0.25d, 8d, facing, 10.91d,
                    out ImpactAuthorization hitAuthorization)
                == UnitActionReducerStatus.Accepted
                && hitAuthorization.Outcome == ImpactAuthorizationOutcome.AuthorizedHit
                && hitAuthorization.Key.Equals(new AttackResultKey(
                    committed.AttackerInstanceId, committed.SequenceId, 0,
                    (int)EntityKind.Unit, 0, 7, 11)),
                "Impact at the 8-degree boundary must authorize a hit.");
            UnitActionSnapshot hitZeroDecided = reducer.Snapshot;
            Require(reducer.EvaluateImpact(
                    hitZeroDecided.Revision, 0, 7, 11, target, true, true, 0.25d, 8d, facing, 10.92d, out _)
                == UnitActionReducerStatus.Duplicate,
                "The same hit must not be authorized twice.");
            Require(reducer.EvaluateImpact(
                    hitZeroDecided.Revision, 1, 8, 12, target, true, true, 0.25d, 8.001d, facing, 10.92d,
                    out ImpactAuthorization missAuthorization)
                == UnitActionReducerStatus.Accepted
                && missAuthorization.Outcome == ImpactAuthorizationOutcome.AuthorizedMiss,
                "A later hit above 8 degrees must miss independently.");
            UnitActionSnapshot bothDecided = reducer.Snapshot;
            Require(bothDecided.Phase == UnitActionPhase.Recovery && bothDecided.DecidedHitMask == 3UL,
                "All independently decided hits must enter Recovery.");

            var hitZeroKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0,
                (int)EntityKind.Unit, 0, 7, 11);
            var hitOneKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 1,
                (int)EntityKind.Unit, 0, 8, 12);
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 10, 40, out AttackImpactResult hitResult),
                "External writer must be able to create a valid applied result.");
            Require(AttackImpactResult.TryCreate(
                    missAuthorization.ActionRevision, hitOneKey, 10.8d, facing, false, default,
                    AttackImpactOutcome.Miss, 0, -1, out AttackImpactResult missResult),
                "External writer must be able to create a valid miss result.");
            Require(!AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 0, 40, out _),
                "A successful result without applied amount must be rejected.");
            var invalidKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0, 0, 0, 0, 0);
            Require(!AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, invalidKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Miss, 0, -1, out _),
                "AttackImpactResult must reject an invalid canonical key.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Evaded, 0, -1, out _),
                "Evaded outcome with zero amount must be valid.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Immune, 0, -1, out _),
                "Immune outcome with zero amount must be valid.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.StatusEffectApplied, 0, 40, out _),
                "A status effect may be applied without direct damage.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Cancelled, 0, -1, out _),
                "Cancelled outcome with zero amount must remain representable.");
            Require(!AttackImpactResult.TryCreate(
                    0UL, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Evaded, 0, -1, out _),
                "Result action revision 0 must be rejected.");

            var wrongEffectKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0,
                (int)EntityKind.Unit, 0, 70, 11);
            var wrongOrdinalKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0,
                (int)EntityKind.Unit, 0, 7, 110);
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, wrongEffectKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 10, 40, out AttackImpactResult wrongEffectResult),
                "EffectKind mutation fixture must remain a structurally valid result.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, wrongOrdinalKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 10, 40, out AttackImpactResult wrongOrdinalResult),
                "ResultOrdinal mutation fixture must remain a structurally valid result.");
            Require(reducer.ConfirmImpactResult(bothDecided.Revision, wrongEffectResult, 10.95d)
                == UnitActionReducerStatus.ScopeMismatch
                && reducer.Snapshot.Revision == bothDecided.Revision,
                "A result with the wrong EffectKind must be rejected without mutation.");
            Require(reducer.ConfirmImpactResult(bothDecided.Revision, wrongOrdinalResult, 10.96d)
                == UnitActionReducerStatus.ScopeMismatch
                && reducer.Snapshot.Revision == bothDecided.Revision,
                "A result with the wrong ResultOrdinal must be rejected without mutation.");

            Require(reducer.ConfirmImpactResult(bothDecided.Revision, missResult, 11d)
                == UnitActionReducerStatus.Accepted && reducer.Snapshot.LastConfirmedHitIndex == -1,
                "Confirming hit 1 first must keep the contiguous prefix at -1.");
            UnitActionSnapshot hitOneConfirmed = reducer.Snapshot;
            Require(reducer.ConfirmImpactResult(hitOneConfirmed.Revision, hitResult, 11.1d)
                == UnitActionReducerStatus.Accepted && reducer.Snapshot.LastConfirmedHitIndex == 1,
                "Confirming hit 0 later must advance the contiguous prefix through hit 1.");
            UnitActionSnapshot confirmed = reducer.Snapshot;
            Require(reducer.ConfirmImpactResult(confirmed.Revision, hitResult, 11.2d)
                == UnitActionReducerStatus.Duplicate,
                "A confirmed result must be idempotent.");
            Require(reducer.Advance(confirmed.Revision, 11.9d) == UnitActionReducerStatus.NoChange,
                "Recovery must wait for the later of cooldown and recovery end.");
            Require(reducer.Advance(confirmed.Revision, 12d) == UnitActionReducerStatus.Accepted
                && reducer.Snapshot.Phase == UnitActionPhase.AcquireTarget,
                "Recovery must finish exactly at max(cooldownEnd,recoveryEnd).");
            UnitActionSnapshot completed = reducer.Snapshot;
            Require(completed.TargetBinding == AttackTargetBinding.None
                && completed.Delivery == AttackDeliveryKind.None
                && !completed.SequenceId.IsValid
                && completed.Timeline == null
                && !completed.RangeProfile.IsValid
                && !completed.SimulationFacing.IsValid
                && !completed.HasSimulationAimDirection
                && !completed.SimulationAimDirection.IsValid,
                "Action end must clear target, plan, sequence, facing, and aim atomically.");
            Require(completed.StartServerTime == 0d
                && completed.CommitServerTime == 0d
                && completed.CooldownEndServerTime == 0d
                && completed.RecoveryEndServerTime == 0d
                && !completed.CooldownConsumed
                && completed.DueHitMask == 0UL
                && completed.DecidedHitMask == 0UL
                && completed.ConfirmedHitMask == 0UL
                && completed.LastConfirmedHitIndex == -1
                && completed.PhaseStartServerTime == 12d,
                "Action end must clear all timing and hit-cycle data while recording the new phase start.");
            Require(reducer.BeginAttackAlignment(
                    completed.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 12.1d) == UnitActionReducerStatus.Accepted,
                "A completed reducer must start the next alignment.");
            UnitActionSnapshot nextAlignment = reducer.Snapshot;
            Require(nextAlignment.Phase == UnitActionPhase.AlignToAttack
                && nextAlignment.StartServerTime == 12.1d
                && nextAlignment.CommitServerTime == 0d
                && nextAlignment.DueHitMask == 0UL
                && nextAlignment.DecidedHitMask == 0UL
                && nextAlignment.ConfirmedHitMask == 0UL
                && nextAlignment.LastConfirmedHitIndex == -1
                && !nextAlignment.HasSimulationAimDirection,
                "The next Align must not inherit any timing, hit, or aim state from the old cycle.");
        }

        private static void ValidateA1ReducerCancellationAndDeath()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "Test facing must be valid.");
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.2d, 0.8d }, out AttackTimelinePlan timeline),
                "Test timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile range),
                "Test range must be valid.");
            var target = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 3));

            var preCommit = new UnitActionSequencer(new AttackerInstanceId(300UL), 3);
            Require(preCommit.BeginAttackAlignment(
                    preCommit.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1d) == UnitActionReducerStatus.Accepted,
                "Pre-commit alignment must start.");
            UnitActionSnapshot beforeUnknownCancel = preCommit.Snapshot;
            Require(preCommit.CancelPreCommit(
                    beforeUnknownCancel.Revision, (PreCommitCancelReason)999, true, 1.05d)
                == UnitActionReducerStatus.InvalidInput,
                "Unknown pre-commit cancellation reason must fail closed.");
            Require(preCommit.Snapshot.Revision == beforeUnknownCancel.Revision
                && preCommit.Snapshot.Phase == beforeUnknownCancel.Phase
                && preCommit.Snapshot.TargetBinding == beforeUnknownCancel.TargetBinding,
                "Unknown cancellation reason must not mutate state.");
            Require(preCommit.CancelPreCommit(
                    preCommit.Snapshot.Revision, PreCommitCancelReason.AttackRangeExited, true, 1.1d)
                == UnitActionReducerStatus.Accepted,
                "AttackRange exit inside LoseRange must enter Chase.");
            Require(preCommit.Snapshot.Phase == UnitActionPhase.Chase
                && preCommit.Snapshot.TargetBinding == target
                && !preCommit.Snapshot.SequenceId.IsValid
                && !preCommit.Snapshot.CooldownConsumed,
                "Pre-commit Chase must retain target without sequence or cooldown.");
            Require(preCommit.BeginAttackAlignment(
                    preCommit.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1.2d) == UnitActionReducerStatus.Accepted,
                "Chase must be able to re-enter alignment.");
            Require(preCommit.CancelPreCommit(
                    preCommit.Snapshot.Revision, PreCommitCancelReason.TargetDead, false, 1.3d)
                == UnitActionReducerStatus.Accepted,
                "Dead target must cancel pre-commit action.");
            Require(preCommit.Snapshot.Phase == UnitActionPhase.AcquireTarget
                && preCommit.Snapshot.TargetBinding == AttackTargetBinding.None
                && !preCommit.Snapshot.CooldownConsumed,
                "Invalid pre-commit target must clear without refund concerns.");

            var committedCancel = new UnitActionSequencer(new AttackerInstanceId(301UL), 3);
            Require(committedCancel.BeginAttackAlignment(
                    committedCancel.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 20d) == UnitActionReducerStatus.Accepted,
                "Committed-cancel fixture must align.");
            Require(committedCancel.CommitAttack(
                    committedCancel.Snapshot.Revision, target, true, true, true,
                    0.25d, 5d, 20d, 20.1d) == UnitActionReducerStatus.Accepted,
                "Committed-cancel fixture must commit.");
            AttackSequenceId committedSequence = committedCancel.Snapshot.SequenceId;
            Require(committedCancel.CancelCommitted(committedCancel.Snapshot.Revision, 20.2d)
                == UnitActionReducerStatus.Accepted,
                "Committed action must support future-hit cancellation.");
            Require(committedCancel.Snapshot.Phase == UnitActionPhase.Recovery
                && committedCancel.Snapshot.CooldownConsumed
                && committedCancel.Snapshot.SequenceId == committedSequence
                && committedCancel.Snapshot.DecidedHitMask == timeline.AllImpactMask,
                "Committed cancellation must suppress future hits without refund or target transfer.");

            var dead = new UnitActionSequencer(new AttackerInstanceId(302UL), 3);
            Require(dead.BeginAttackAlignment(
                    dead.Snapshot.Revision, target, AttackDeliveryKind.Hitscan,
                    timeline, range, facing, 30d) == UnitActionReducerStatus.Accepted,
                "Dead fixture must align.");
            Require(dead.CommitAttack(
                    dead.Snapshot.Revision, target, true, true, true,
                    0.25d, 5d, 30d, 30.1d) == UnitActionReducerStatus.Accepted,
                "Dead fixture must commit.");
            UnitActionSnapshot deadCommitted = dead.Snapshot;
            Require(dead.Advance(deadCommitted.Revision, 30.2d) == UnitActionReducerStatus.Accepted,
                "Dead fixture hit 0 must become due before death.");
            Require(dead.EvaluateImpact(
                    dead.Snapshot.Revision, 0, 9, 13, target, true, true, 0.25d, 0d,
                    facing, 30.21d, out ImpactAuthorization preDeathAuthorization)
                == UnitActionReducerStatus.Accepted,
                "Hit authorized before death must be retained.");
            var preDeathKey = new AttackResultKey(
                deadCommitted.AttackerInstanceId, deadCommitted.SequenceId, 0,
                (int)EntityKind.Unit, 3, 9, 13);
            Require(AttackImpactResult.TryCreate(
                    preDeathAuthorization.ActionRevision, preDeathKey, 30.21d,
                    facing, false, default, AttackImpactOutcome.HitApplied, 5, 45,
                    out AttackImpactResult preDeathResult),
                "Exact pre-death authorization result must be constructible.");
            Require(dead.MarkDead(dead.Snapshot.Revision) == UnitActionReducerStatus.Accepted,
                "MarkDead must be accepted without a time input.");
            UnitActionSnapshot deadSnapshot = dead.Snapshot;
            Require(deadSnapshot.Phase == UnitActionPhase.Dead
                && deadSnapshot.EndReason == UnitActionEndReason.AttackerDead
                && deadSnapshot.TargetBinding == target,
                "Dead must be a terminal snapshot with an explicit reason.");
            Require(dead.ConfirmImpactResult(deadSnapshot.Revision, preDeathResult, 30.3d)
                == UnitActionReducerStatus.Accepted,
                "An exact result authorized before death must still be confirmed after death.");
            var futureKey = new AttackResultKey(
                deadCommitted.AttackerInstanceId, deadCommitted.SequenceId, 1,
                (int)EntityKind.Unit, 3, 9, 14);
            Require(AttackImpactResult.TryCreate(
                    preDeathAuthorization.ActionRevision, futureKey, 30.3d,
                    facing, false, default, AttackImpactOutcome.Miss, 0, -1,
                    out AttackImpactResult unauthorizedFutureResult),
                "Future result fixture must be structurally valid.");
            Require(dead.ConfirmImpactResult(dead.Snapshot.Revision, unauthorizedFutureResult, 30.4d)
                == UnitActionReducerStatus.NotDue,
                "Death must reject a future hit that never received authorization.");
            deadSnapshot = dead.Snapshot;
            Require(dead.Advance(deadSnapshot.Revision, double.NaN) == UnitActionReducerStatus.DeadTerminal,
                "Dead terminal must reject later reducer operations before reading time.");
            Require(dead.MarkDead(deadSnapshot.Revision) == UnitActionReducerStatus.NoChange
                && dead.Snapshot.Revision == deadSnapshot.Revision,
                "Repeated MarkDead must be a no-op without revision growth.");
        }

        private static void ValidateA1OverflowAndAtomicReset()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "Overflow fixture facing must be valid.");
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.2d }, out AttackTimelinePlan timeline),
                "Overflow fixture timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile range),
                "Overflow fixture range must be valid.");
            var target = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 8));

            var revisionExhausted = UnitActionSequencer.CreateForValidation(
                new AttackerInstanceId(400UL), 8, ulong.MaxValue, 0UL);
            UnitActionSnapshot revisionBefore = revisionExhausted.Snapshot;
            Require(revisionExhausted.BeginAttackAlignment(
                    revisionBefore.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1d) == UnitActionReducerStatus.Exhausted,
                "Revision maximum must fail closed instead of wrapping.");
            Require(revisionExhausted.Snapshot.Revision == revisionBefore.Revision
                && revisionExhausted.Snapshot.Phase == revisionBefore.Phase,
                "Revision exhaustion must leave state unchanged.");

            var sequenceExhausted = UnitActionSequencer.CreateForValidation(
                new AttackerInstanceId(401UL), 8, 1UL, ulong.MaxValue);
            Require(sequenceExhausted.BeginAttackAlignment(
                    sequenceExhausted.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 2d) == UnitActionReducerStatus.Accepted,
                "Sequence exhaustion fixture must align.");
            UnitActionSnapshot sequenceBeforeCommit = sequenceExhausted.Snapshot;
            Require(sequenceExhausted.CommitAttack(
                    sequenceBeforeCommit.Revision, target, true, true, true,
                    0.25d, 0d, 2d, 2.1d) == UnitActionReducerStatus.Exhausted,
                "Sequence maximum must fail closed instead of throwing or wrapping.");
            UnitActionSnapshot sequenceAfterCommit = sequenceExhausted.Snapshot;
            Require(sequenceAfterCommit.Revision == sequenceBeforeCommit.Revision
                && sequenceAfterCommit.Phase == sequenceBeforeCommit.Phase
                && !sequenceAfterCommit.SequenceId.IsValid
                && !sequenceAfterCommit.CooldownConsumed,
                "Sequence exhaustion must leave the aligned action unchanged.");

            var allocator = new AttackSequenceAllocator(ulong.MaxValue);
            Require(!allocator.TryNext(new AttackerInstanceId(402UL), out AttackSequenceId exhaustedId)
                && !exhaustedId.IsValid,
                "Allocator TryNext must expose exhaustion without an exception.");

            Require(!UnitActionSequencer.TryCreateShadowCycle(
                    AttackerInstanceId.None, 8, new AttackSequenceId(1UL), out _)
                && !UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(403UL), -1, new AttackSequenceId(1UL), out _)
                && !UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(403UL), 8, AttackSequenceId.None, out _),
                "Runtime shadow factory must reject every invalid identity input.");
            Require(UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(404UL), 8, new AttackSequenceId(1UL), out UnitActionSequencer firstCycle)
                && firstCycle.BeginAttackAlignment(
                    firstCycle.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 3d) == UnitActionReducerStatus.Accepted
                && firstCycle.CommitAttack(
                    firstCycle.Snapshot.Revision, target, true, true, true,
                    0.25d, 0d, 3d, 3d) == UnitActionReducerStatus.Accepted
                && firstCycle.Snapshot.SequenceId.Value == 1UL,
                "Runtime shadow factory must allocate exact sequence 1.");
            Require(UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(405UL), 8, new AttackSequenceId(ulong.MaxValue),
                    out UnitActionSequencer maximumCycle)
                && maximumCycle.BeginAttackAlignment(
                    maximumCycle.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 4d) == UnitActionReducerStatus.Accepted
                && maximumCycle.CommitAttack(
                    maximumCycle.Snapshot.Revision, target, true, true, true,
                    0.25d, 0d, 4d, 4d) == UnitActionReducerStatus.Accepted
                && maximumCycle.Snapshot.SequenceId.Value == ulong.MaxValue,
                "Runtime shadow factory must allocate exact maximum sequence without overflow.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException($"[UAS-DIAG] self-validation FAIL: {message}");
        }
    }
}
