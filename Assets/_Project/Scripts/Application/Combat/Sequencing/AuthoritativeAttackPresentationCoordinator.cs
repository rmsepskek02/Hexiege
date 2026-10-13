using System;
using System.Collections.Generic;

namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>
    /// A committed visual intent, not a prediction of damage or a final impact direction.
    /// Multi-victim results join by scope; the server determines their exact keys at Impact.
    /// </summary>
    public readonly struct AttackPresentationSchedule : IEquatable<AttackPresentationSchedule>
    {
        public readonly int AttackerUnitId;
        public readonly AttackPresentationScope Scope;
        public readonly ulong CommitRevision;
        public readonly AttackDeliveryKind Delivery;
        public readonly double ImpactServerTime;
        public readonly ActionDirectionXZ CommitDirection;
        public bool IsValid => AttackerUnitId >= 0 && Scope.IsValid && CommitRevision > 0
            && Delivery != AttackDeliveryKind.None && Enum.IsDefined(typeof(AttackDeliveryKind), Delivery)
            && ContractNumber.IsFinite(ImpactServerTime) && ImpactServerTime >= 0 && CommitDirection.IsValid;

        public AttackPresentationSchedule(int attackerUnitId, AttackPresentationScope scope,
            ulong commitRevision, AttackDeliveryKind delivery, double impactServerTime,
            ActionDirectionXZ commitDirection)
        {
            AttackerUnitId = attackerUnitId; Scope = scope; CommitRevision = commitRevision;
            Delivery = delivery; ImpactServerTime = impactServerTime; CommitDirection = commitDirection;
        }
        public bool Equals(AttackPresentationSchedule other) => AttackerUnitId == other.AttackerUnitId
            && Scope.Equals(other.Scope) && CommitRevision == other.CommitRevision
            && Delivery == other.Delivery && ImpactServerTime.Equals(other.ImpactServerTime)
            && CommitDirection.Equals(other.CommitDirection);
        public override bool Equals(object obj) => obj is AttackPresentationSchedule other && Equals(other);
        public override int GetHashCode() => Scope.GetHashCode();
    }

    public enum AuthoritativePresentationStatus
    {
        Accepted, Duplicate, Conflict, Invalid, CapacityExceeded, MissingSchedule, Expired, Ready, IncompleteAtEnd, MissingBundle
    }

    /// <summary>
    /// Pure comparison decision. No callback in this class can write HP or play an effect.
    /// Observed time is distinct from the intended presentation time, so network catch-up is visible.
    /// </summary>
    public readonly struct AuthoritativePresentationDecision
    {
        public readonly AttackResultPresentationInput Result;
        public readonly AuthoritativePresentationStatus Status;
        public readonly double IntendedServerTime;
        public readonly double ObservedServerTime;
        public AuthoritativePresentationDecision(AttackResultPresentationInput result,
            AuthoritativePresentationStatus status, double intended, double observed)
        { Result = result; Status = status; IntendedServerTime = intended; ObservedServerTime = observed; }
    }

    /// <summary>
    /// Exactly-keyed, bounded result lifetime shared by the comparison adapter and its regression tests.
    /// A marker, tracer arrival or attacker death never authorizes a result. Only a confirmed C2
    /// result does. Retirement closes unconfirmed visual intent while preserving even reordered results.
    /// Records remain as bounded tombstones until match end, preventing replay after object reuse.
    /// This stage intentionally produces candidate decisions only; complete AoE manifests and the
    /// multiplayer comparison gate are required before an actual presentation emitter is enabled.
    /// </summary>
    public sealed class AuthoritativeAttackPresentationCoordinator
    {
        public const double PresentationDelaySeconds = 0.10d;
        public const double CatchUpSeconds = 0.50d;
        public const double MissingScheduleDeadlineSeconds = 2d;
        private sealed class ResultState
        {
            public AttackResultPresentationInput Input;
            public double ReceivedAt;
        }
        private readonly int _capacity;
        private readonly Dictionary<AttackPresentationScope, AttackPresentationSchedule> _schedules = new();
        private readonly Dictionary<AttackResultKey, ResultState> _results = new();
        private readonly Dictionary<AttackPresentationScope, List<AttackResultKey>> _resultKeysByScope = new();
        private readonly HashSet<AttackResultKey> _pending = new();
        private readonly List<AttackResultKey> _completed = new();
        private readonly HashSet<AttackerInstanceId> _retired = new();
        private readonly Dictionary<AttackResultKey, double> _legacyEmits = new();
        private readonly Dictionary<AttackPresentationScope, AttackResultPresentationInput[]> _bundles = new();
        private readonly HashSet<AttackPresentationScope> _pendingBundles = new();
        private readonly HashSet<AttackResultKey> _readyKeys = new();
        private readonly List<AttackPresentationScope> _completedBundles = new();
        public bool RequireCompleteBundle { get; set; }
        public const int MaximumResultsPerBundle = 256;
        public int BundleCount => _bundles.Count;
        public int ReleasedBundleCount { get; private set; }
        public event Action<AttackResultPresentationInput[]> BundleReady;
        public event Action<AuthoritativePresentationDecision> Decided;
        public int ScheduleCount => _schedules.Count;
        public int ResultCount => _results.Count;
        public int PendingCount => _pending.Count;
        public int ReadyCount { get; private set; }
        public int ReadyVisualCount { get; private set; }
        public int FailureCount { get; private set; }
        public int DuplicateCount { get; private set; }
        public int RetiredCount => _retired.Count;
        public int ComparedLegacyCount { get; private set; }
        public double MaximumLegacyTimingDeltaSeconds { get; private set; }

        public AuthoritativeAttackPresentationCoordinator(int capacity = 32768)
        { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); _capacity = capacity; }

        public AuthoritativePresentationStatus ObserveSchedule(AttackPresentationSchedule schedule)
        {
            if (!schedule.IsValid) return Fail(AuthoritativePresentationStatus.Invalid);
            if (_schedules.TryGetValue(schedule.Scope, out var previous))
                return previous.Equals(schedule) ? Duplicate() : Fail(AuthoritativePresentationStatus.Conflict);
            if (_schedules.Count >= _capacity) return Fail(AuthoritativePresentationStatus.CapacityExceeded);
            _schedules.Add(schedule.Scope, schedule);
            return AuthoritativePresentationStatus.Accepted;
        }

        public AuthoritativePresentationStatus ObserveResult(AttackResultPresentationInput result, double now)
        {
            if (!result.IsValid || !ValidTime(now)
                || !Enum.IsDefined(typeof(AttackDeliveryKind), result.Delivery)
                || !Enum.IsDefined(typeof(AttackImpactOutcome), result.Outcome))
                return Fail(AuthoritativePresentationStatus.Invalid);
            if (_results.TryGetValue(result.Key, out var previous))
                return previous.Input.Equals(result) ? Duplicate() : Fail(AuthoritativePresentationStatus.Conflict);
            if (_bundles.TryGetValue(AttackPresentationScope.FromResultKey(result.Key), out var completedBundle)
                && Array.FindIndex(completedBundle, item => item.Key.Equals(result.Key)) < 0)
                return Fail(AuthoritativePresentationStatus.Conflict);
            if (_results.Count >= _capacity) return Fail(AuthoritativePresentationStatus.CapacityExceeded);
            _results.Add(result.Key, new ResultState { Input = result, ReceivedAt = now });
            var scope = AttackPresentationScope.FromResultKey(result.Key);
            if (!_resultKeysByScope.TryGetValue(scope, out var scopedKeys))
            { scopedKeys = new List<AttackResultKey>(); _resultKeysByScope.Add(scope, scopedKeys); }
            scopedKeys.Add(result.Key);
            _pending.Add(result.Key);
            if (_legacyEmits.TryGetValue(result.Key, out double emittedAt)) Compare(result, emittedAt);
            return AuthoritativePresentationStatus.Accepted;
        }

        /// <summary>
        /// 서버가 한 HitIndex의 실제 처리를 끝낸 뒤 보낸 완결 묶음만 수락한다.
        /// 첫 결과나 ordinal 최댓값으로 완료를 추측하지 않으며, 전체를 검증한 다음 복사한다.
        /// 기존 C2 결과가 먼저 도착한 경우 같은 값은 유지하고 수신 중복으로 다시 집계하지 않는다.
        /// </summary>
        public AuthoritativePresentationStatus ObserveCompletedBundle(AttackResultPresentationInput[] results, double now)
        {
            if (results == null || results.Length == 0 || results.Length > MaximumResultsPerBundle || !ValidTime(now))
                return Fail(AuthoritativePresentationStatus.Invalid);
            var first = results[0];
            var scope = AttackPresentationScope.FromResultKey(first.Key);
            if (!scope.IsValid) return Fail(AuthoritativePresentationStatus.Invalid);
            int additional = 0;
            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i];
                if (!result.IsValid || !Enum.IsDefined(typeof(AttackDeliveryKind), result.Delivery)
                    || !Enum.IsDefined(typeof(AttackImpactOutcome), result.Outcome)
                    || (result.IsVisualResult
                        && (!result.HasImpactPosition || !result.HasPresentationSnapshot))
                    || !AttackPresentationScope.FromResultKey(result.Key).Equals(scope)
                    || result.AttackerUnitId != first.AttackerUnitId || result.ActionRevision != first.ActionRevision
                    || result.Delivery != first.Delivery || result.ImpactServerTime != first.ImpactServerTime)
                    return Fail(AuthoritativePresentationStatus.Invalid);
                for (int j = 0; j < i; j++)
                    if (results[j].Key.Equals(result.Key)) return Fail(AuthoritativePresentationStatus.Conflict);
                if (_results.TryGetValue(result.Key, out var known))
                { if (!known.Input.Equals(result)) return Fail(AuthoritativePresentationStatus.Conflict); }
                else additional++;
            }
            if (_resultKeysByScope.TryGetValue(scope, out var knownKeys))
                foreach (var knownKey in knownKeys)
                    if (Array.FindIndex(results, item => item.Key.Equals(knownKey)) < 0)
                        return Fail(AuthoritativePresentationStatus.Conflict);
            if (_bundles.TryGetValue(scope, out var previous))
            {
                if (previous.Length != results.Length) return Fail(AuthoritativePresentationStatus.Conflict);
                for (int i = 0; i < previous.Length; i++)
                    if (!previous[i].Equals(results[i])) return Fail(AuthoritativePresentationStatus.Conflict);
                return Duplicate();
            }
            if (_bundles.Count >= _capacity || _results.Count + additional > _capacity)
                return Fail(AuthoritativePresentationStatus.CapacityExceeded);
            var copy = (AttackResultPresentationInput[])results.Clone();
            _bundles.Add(scope, copy);
            _pendingBundles.Add(scope);
            foreach (var result in copy)
                if (!_results.ContainsKey(result.Key)) ObserveResult(result, now);
            return AuthoritativePresentationStatus.Accepted;
        }

        public void Tick(double now)
        {
            if (!ValidTime(now)) { Fail(AuthoritativePresentationStatus.Invalid); return; }
            _completed.Clear();
            foreach (var key in _pending)
            {
                var state = _results[key];
                var result = state.Input;
                double due = result.ImpactServerTime + PresentationDelaySeconds;
                AuthoritativePresentationStatus status;
                if (!_schedules.TryGetValue(AttackPresentationScope.FromResultKey(key), out var schedule))
                {
                    if (now - state.ReceivedAt < MissingScheduleDeadlineSeconds) continue;
                    status = AuthoritativePresentationStatus.MissingSchedule;
                }
                else if (schedule.AttackerUnitId != result.AttackerUnitId
                    || schedule.Delivery != result.Delivery || result.ActionRevision < schedule.CommitRevision)
                    status = AuthoritativePresentationStatus.Conflict;
                else if (RequireCompleteBundle && !_bundles.ContainsKey(AttackPresentationScope.FromResultKey(key)))
                {
                    if (now - state.ReceivedAt < MissingScheduleDeadlineSeconds) continue;
                    status = AuthoritativePresentationStatus.MissingBundle;
                }
                else
                {
                    if (now < due) continue;
                    // A stall or genuinely late packet is explicit, never replayed against a later marker.
                    status = now > due + CatchUpSeconds
                        ? AuthoritativePresentationStatus.Expired : AuthoritativePresentationStatus.Ready;
                }
                _completed.Add(key);
                if (status == AuthoritativePresentationStatus.Ready)
                {
                    ReadyCount++; _readyKeys.Add(key);
                    if (result.Outcome == AttackImpactOutcome.HitApplied
                        || result.Outcome == AttackImpactOutcome.StatusEffectApplied) ReadyVisualCount++;
                }
                else FailureCount++;
                Decided?.Invoke(new AuthoritativePresentationDecision(result, status, due, now));
            }
            foreach (var key in _completed) _pending.Remove(key);
            _completedBundles.Clear();
            foreach (var scope in _pendingBundles)
            {
                var bundle = _bundles[scope];
                bool ready = true;
                bool terminal = true;
                foreach (var result in bundle)
                {
                    ready &= _readyKeys.Contains(result.Key);
                    terminal &= !_pending.Contains(result.Key);
                }
                if (!terminal) continue;
                _completedBundles.Add(scope);
                if (ready)
                {
                    ReleasedBundleCount++;
                    // 모든 피해자 결과가 준비됐을 때 한 번의 호출로 방출한다.
                    BundleReady?.Invoke((AttackResultPresentationInput[])bundle.Clone());
                }
            }
            foreach (var scope in _completedBundles) _pendingBundles.Remove(scope);
        }

        public bool IsVisualIntentRetired(AttackerInstanceId instance) => _retired.Contains(instance);
        public bool TryGetSchedule(AttackPresentationScope scope, out AttackPresentationSchedule schedule)
            => _schedules.TryGetValue(scope, out schedule);
        public bool TryGetConfirmedResult(AttackPresentationScope scope, out AttackResultPresentationInput result)
        {
            if (_bundles.TryGetValue(scope, out var bundle)) { result = bundle[0]; return true; }
            result = default;
            return false;
        }
        public void ObserveLegacyEmit(AttackResultKey key, double now)
        {
            if (!key.IsValid || !ValidTime(now)) return;
            if (_legacyEmits.ContainsKey(key)) return;
            if (_legacyEmits.Count >= _capacity) { Fail(AuthoritativePresentationStatus.CapacityExceeded); return; }
            _legacyEmits.Add(key, now);
            if (_results.TryGetValue(key, out var state)) Compare(state.Input, now);
        }
        private void Compare(AttackResultPresentationInput result, double emittedAt)
        {
            ComparedLegacyCount++;
            MaximumLegacyTimingDeltaSeconds = Math.Max(MaximumLegacyTimingDeltaSeconds,
                Math.Abs(emittedAt - (result.ImpactServerTime + PresentationDelaySeconds)));
        }
        public void Retire(AttackerInstanceId instance)
        {
            // 공격 회차를 한 번도 열지 않은 유닛은 종료할 표현 수명이 없다.
            // 이 정상 부재를 Invalid로 세면 경기 종료 시 "생산 유닛 - 공격 유닛" 전부가
            // 허위 C3 실패가 된다. 실제 유효 인스턴스의 충돌/용량 오류만 fail-closed한다.
            if (!instance.IsValid) return;
            if (_retired.Contains(instance)) return;
            if (_retired.Count >= _capacity) { Fail(AuthoritativePresentationStatus.CapacityExceeded); return; }
            _retired.Add(instance);
            // Intentionally retain pending and future-arriving confirmed C2 results.
        }
        public void Complete(double now)
        {
            Tick(now);
            foreach (var key in _pending)
            {
                FailureCount++;
                var result = _results[key].Input;
                Decided?.Invoke(new AuthoritativePresentationDecision(result,
                    _schedules.ContainsKey(AttackPresentationScope.FromResultKey(key))
                        ? AuthoritativePresentationStatus.IncompleteAtEnd : AuthoritativePresentationStatus.MissingSchedule,
                    result.ImpactServerTime + PresentationDelaySeconds, now));
            }
            _pending.Clear();
        }
        public void Clear()
        {
            _schedules.Clear(); _results.Clear(); _pending.Clear(); _completed.Clear(); _retired.Clear(); _legacyEmits.Clear();
            _bundles.Clear(); _pendingBundles.Clear(); _readyKeys.Clear(); _completedBundles.Clear();
            _resultKeysByScope.Clear();
            ReleasedBundleCount = 0;
            ReadyCount = 0; FailureCount = 0; DuplicateCount = 0;
            ReadyVisualCount = 0;
            ComparedLegacyCount = 0; MaximumLegacyTimingDeltaSeconds = 0;
        }
        private AuthoritativePresentationStatus Fail(AuthoritativePresentationStatus status)
        { FailureCount++; return status; }
        private AuthoritativePresentationStatus Duplicate()
        { DuplicateCount++; return AuthoritativePresentationStatus.Duplicate; }
        private static bool ValidTime(double time) => ContractNumber.IsFinite(time) && time >= 0;
    }
}
