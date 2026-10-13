using System;
using System.Collections.Generic;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;

namespace Hexiege.Infrastructure
{
    public enum NetworkAttackPresentationBundleStatus : byte
    {
        Accepted = 0,
        Duplicate = 1,
        Conflict = 2,
        Invalid = 3,
        CapacityExceeded = 4
    }

    /// <summary>
    /// 실제 서버 피해 호출이 끝난 뒤에만 완결 묶음을 만든다. 첫 결과나 가장 큰 ordinal로
    /// 완료를 추측하지 않으며, C2 주 결과는 이미 전송된 값과 byte-level 의미가 같아야 한다.
    /// </summary>
    public static class AttackPresentationBundleAssembler
    {
        public const int SecondaryDamageEffectKind = 2;

        public static bool TryCreate(
            int attackerUnitId,
            AttackImpactResult primaryResult,
            AttackDeliveryKind delivery,
            AttackDamagePresentationBundleObservation observation,
            out AttackResultPresentationInput[] results)
        {
            results = null;
            if (attackerUnitId < 0 || primaryResult == null || !primaryResult.Key.IsValid
                || delivery == AttackDeliveryKind.None || !observation.IsComplete)
                return false;

            AppliedAttackPresentationFact[] facts = observation.Facts;
            AttackDamageObservation primaryObservation = observation.PrimaryObservation;
            if (facts.Length + 1 > AuthoritativeAttackPresentationCoordinator.MaximumResultsPerBundle)
                return false;
            bool primaryIsVisual = primaryResult.Outcome == AttackImpactOutcome.HitApplied
                || primaryResult.Outcome == AttackImpactOutcome.StatusEffectApplied;
            if (primaryIsVisual
                && (!primaryObservation.HasPresentationSnapshot
                    || !primaryObservation.HasImpactPosition
                    || !primaryResult.HasImpactPosition
                    || !primaryObservation.ImpactPosition.Equals(primaryResult.ImpactPosition)
                    || primaryObservation.AppliedAmount != primaryResult.AppliedAmount
                    || primaryObservation.ResultingHp != primaryResult.ResultingHp))
                return false;

            var assembled = new List<AttackResultPresentationInput>(facts.Length + 1);
            bool foundPrimaryAppliedFact = false;
            AppliedAttackPresentationFact primaryAppliedFact = default;
            for (int i = 0; i < facts.Length; i++)
            {
                AppliedAttackPresentationFact fact = facts[i];
                if (!fact.IsValid) return false;
                bool isPrimary = fact.VictimKind == primaryResult.Key.VictimKind
                    && fact.VictimId == primaryResult.Key.VictimId
                    && fact.EffectKind == primaryResult.Key.EffectKind
                    && fact.ResultOrdinal == primaryResult.Key.ResultOrdinal;
                if (isPrimary)
                {
                    if (foundPrimaryAppliedFact
                        || primaryResult.Outcome != AttackImpactOutcome.HitApplied
                        || fact.AppliedAmount != primaryResult.AppliedAmount
                        || fact.ResultingHp != primaryResult.ResultingHp
                        || fact.VictimPresentationType != primaryObservation.VictimPresentationType
                        || fact.VictimTeam != primaryObservation.VictimTeam
                        || !fact.HasImpactPosition
                        || !fact.ImpactPosition.Equals(primaryObservation.ImpactPosition))
                        return false;
                    foundPrimaryAppliedFact = true;
                    primaryAppliedFact = fact;
                    continue;
                }

                var key = new AttackResultKey(
                    primaryResult.Key.AttackerInstanceId,
                    primaryResult.Key.SequenceId,
                    primaryResult.Key.HitIndex,
                    fact.VictimKind,
                    fact.VictimId,
                    fact.EffectKind,
                    fact.ResultOrdinal);
                var input = new AttackResultPresentationInput(
                    attackerUnitId,
                    primaryResult.ActionRevision,
                    key,
                    delivery,
                    primaryResult.ImpactServerTime,
                    primaryResult.AuthoritativeAimDirection,
                    fact.HasImpactPosition,
                    fact.ImpactPosition,
                    AttackImpactOutcome.HitApplied,
                    fact.AppliedAmount,
                    fact.ResultingHp,
                    fact.VictimPresentationType,
                    fact.VictimTeam);
                if (!input.IsValid) return false;
                for (int j = 0; j < assembled.Count; j++)
                    if (assembled[j].Key.Equals(key)) return false;
                assembled.Add(input);
            }

            if (primaryResult.Outcome == AttackImpactOutcome.HitApplied && !foundPrimaryAppliedFact)
                return false;
            if (primaryResult.Outcome != AttackImpactOutcome.HitApplied && facts.Length != 0)
                return false;

            AttackResultPresentationInput primaryInput = CreatePrimaryInput(
                attackerUnitId, primaryResult, delivery, primaryObservation);
            if (!primaryInput.IsValid) return false;
            assembled.Add(primaryInput);

            assembled.Sort((left, right) => left.Key.CompareTo(right.Key));
            results = assembled.ToArray();
            return true;
        }

        public static bool TryToNetwork(
            AttackResultPresentationInput[] inputs,
            out NetworkAttackImpactShadowResult[] payload)
        {
            payload = null;
            if (inputs == null || inputs.Length == 0
                || inputs.Length > AuthoritativeAttackPresentationCoordinator.MaximumResultsPerBundle)
                return false;
            payload = new NetworkAttackImpactShadowResult[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                AttackResultPresentationInput input = inputs[i];
                if (!input.IsValid
                    || (input.IsVisualResult
                        && (!input.HasImpactPosition || !input.HasPresentationSnapshot)))
                { payload = null; return false; }
                payload[i] = new NetworkAttackImpactShadowResult
                {
                    AttackerInstanceId = input.Key.AttackerInstanceId.Value,
                    SequenceId = input.Key.SequenceId.Value,
                    ActionRevision = input.ActionRevision,
                    HitIndex = input.Key.HitIndex,
                    VictimKind = input.Key.VictimKind,
                    VictimId = input.Key.VictimId,
                    EffectKind = input.Key.EffectKind,
                    ResultOrdinal = input.Key.ResultOrdinal,
                    Delivery = (byte)input.Delivery,
                    ImpactServerTime = input.ImpactServerTime,
                    AimX = (float)input.AimDirection.X,
                    AimZ = (float)input.AimDirection.Z,
                    HasImpactPosition = input.HasImpactPosition ? (byte)1 : (byte)0,
                    ImpactPositionX = input.HasImpactPosition ? (float)input.ImpactPosition.X : 0f,
                    ImpactPositionZ = input.HasImpactPosition ? (float)input.ImpactPosition.Z : 0f,
                    Outcome = (byte)input.Outcome,
                    AppliedAmount = input.AppliedAmount,
                    ResultingHp = input.ResultingHp,
                    VictimPresentationType = input.VictimPresentationType,
                    VictimTeam = input.VictimTeam
                };
                if (!payload[i].IsValid) { payload = null; return false; }
            }
            return true;
        }

        public static bool TryFromNetwork(
            int attackerUnitId,
            NetworkAttackImpactShadowResult[] payload,
            out AttackResultPresentationInput[] inputs)
        {
            inputs = null;
            if (attackerUnitId < 0 || payload == null || payload.Length == 0
                || payload.Length > AuthoritativeAttackPresentationCoordinator.MaximumResultsPerBundle)
                return false;
            inputs = new AttackResultPresentationInput[payload.Length];
            for (int i = 0; i < payload.Length; i++)
            {
                if (!payload[i].TryToPresentationInput(attackerUnitId, out inputs[i]))
                { inputs = null; return false; }
                if (inputs[i].IsVisualResult
                    && (!inputs[i].HasImpactPosition || !inputs[i].HasPresentationSnapshot))
                { inputs = null; return false; }
            }
            return true;
        }

        private static AttackResultPresentationInput CreatePrimaryInput(
            int attackerUnitId,
            AttackImpactResult result,
            AttackDeliveryKind delivery,
            AttackDamageObservation observation)
            => new AttackResultPresentationInput(
                attackerUnitId, result.ActionRevision, result.Key, delivery,
                result.ImpactServerTime, result.AuthoritativeAimDirection,
                result.HasImpactPosition, result.ImpactPosition, result.Outcome,
                result.AppliedAmount, result.ResultingHp,
                observation.VictimPresentationType,
                observation.VictimTeam);
    }

    /// <summary>
    /// Reliable RPC 재전송은 멱등 수락하고 같은 scope의 다른 manifest는 충돌로 거부한다.
    /// 공격자 Despawn과 무관한 match owner에서 유지하므로 늦게 도착한 확정 결과도 보존된다.
    /// </summary>
    public sealed class NetworkAttackPresentationBundleClassifier
    {
        // 한 번의 멀티플레이 경기에서 관측되는 공격 결과는 수천 건이 될 수 있다.
        // 256개에서 오래된 키를 버리면 Reliable 재전송이나 아주 늦게 도착한 동일 묶음이
        // 새 공격처럼 다시 표시될 수 있고, 버리지 않으면 257번째부터 정상 묶음이 전부
        // CapacityExceeded가 된다. 그래서 경기 수명 coordinator와 같은 명시적 상한을
        // 사용해 한 경기 안에서는 tombstone을 보존하고, 경기 종료 때 Clear로 해제한다.
        public const int MaximumRememberedBundles = 32768;
        private readonly Dictionary<AttackPresentationScope, NetworkAttackImpactShadowResult[]>
            _accepted = new Dictionary<AttackPresentationScope, NetworkAttackImpactShadowResult[]>();
        private readonly Queue<AttackPresentationScope> _order = new Queue<AttackPresentationScope>();

        public NetworkAttackPresentationBundleStatus Classify(
            int attackerUnitId,
            NetworkAttackImpactShadowResult[] payload)
        {
            if (!AttackPresentationBundleAssembler.TryFromNetwork(
                    attackerUnitId, payload, out AttackResultPresentationInput[] inputs))
                return NetworkAttackPresentationBundleStatus.Invalid;
            AttackPresentationScope scope = AttackPresentationScope.FromResultKey(inputs[0].Key);
            for (int i = 0; i < inputs.Length; i++)
            {
                if (!AttackPresentationScope.FromResultKey(inputs[i].Key).Equals(scope)
                    || inputs[i].AttackerUnitId != attackerUnitId)
                    return NetworkAttackPresentationBundleStatus.Invalid;
                for (int j = 0; j < i; j++)
                    if (inputs[j].Key.Equals(inputs[i].Key))
                        return NetworkAttackPresentationBundleStatus.Conflict;
            }

            if (_accepted.TryGetValue(scope, out NetworkAttackImpactShadowResult[] previous))
            {
                if (previous.Length != payload.Length)
                    return NetworkAttackPresentationBundleStatus.Conflict;
                for (int i = 0; i < previous.Length; i++)
                    if (!previous[i].Equals(payload[i]))
                        return NetworkAttackPresentationBundleStatus.Conflict;
                return NetworkAttackPresentationBundleStatus.Duplicate;
            }
            if (_accepted.Count >= MaximumRememberedBundles)
                return NetworkAttackPresentationBundleStatus.CapacityExceeded;
            _accepted.Add(scope, (NetworkAttackImpactShadowResult[])payload.Clone());
            _order.Enqueue(scope);
            return NetworkAttackPresentationBundleStatus.Accepted;
        }

        public void Clear()
        {
            _accepted.Clear();
            _order.Clear();
        }
    }
}
