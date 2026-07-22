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

            Debug.Log("[UAS-DIAG] self-validation PASS: phase vocabulary/ordinal, sequence, 10/15 move, 5/8 attack, bounded dedupe/reorder.");
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

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException($"[UAS-DIAG] self-validation FAIL: {message}");
        }
    }
}
