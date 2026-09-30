using System;
using System.Collections.Generic;

namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>
    /// 서버 공격 회차와 Animator 표현을 분리하는 순수 정책이다.
    /// 이미 Attack 표현 중인 유닛은 다음 회차 정렬 동안 모션을 유지하되,
    /// 커밋 전 Animation Event의 타격 연출은 억제한다.
    /// </summary>
    public static class UnitAttackPresentationPolicy
    {
        /// <summary>
        /// provisional Attack이 시작된 서버 시각을 기준으로 현재 보이는 애니메이션 주기 안에서
        /// gameplay 회차를 시작해도 되는지를 계산한다. 첫 marker가 아직 남아 있으면 이미
        /// 재생된 시간만 overshoot로 넘기고, marker를 지났다면 클립을 되감지 않고 다음 주기까지
        /// 기다린다.
        /// </summary>
        public static AttackPresentationCommitWindow ResolveCommitWindow(
            double presentationEpochServerTime,
            double observedServerTime,
            double cycleDurationSeconds,
            double firstImpactOffsetSeconds)
        {
            if (!IsFinite(presentationEpochServerTime)
                || !IsFinite(observedServerTime)
                || !IsFinite(cycleDurationSeconds)
                || !IsFinite(firstImpactOffsetSeconds)
                || observedServerTime < presentationEpochServerTime
                || cycleDurationSeconds <= 0d
                || firstImpactOffsetSeconds < 0d
                || firstImpactOffsetSeconds > cycleDurationSeconds)
                return default;

            double elapsed = observedServerTime - presentationEpochServerTime;
            double completedCycles = System.Math.Floor(elapsed / cycleDurationSeconds);
            double phaseElapsed = elapsed - completedCycles * cycleDurationSeconds;
            if (phaseElapsed <= firstImpactOffsetSeconds)
            {
                return new AttackPresentationCommitWindow(
                    true,
                    true,
                    phaseElapsed,
                    0d);
            }

            return new AttackPresentationCommitWindow(
                true,
                false,
                0d,
                cycleDurationSeconds - phaseElapsed);
        }

        /// <summary>
        /// 서버가 고정한 Attack 표현 epoch에서 현재 cycle phase를 한 번 계산하고,
        /// Legacy 피해 예약과 Shadow timeline이 함께 사용할 유효 타격 offset 전체를 만든다.
        /// 첫 marker가 이미 지나갔으면 과거 marker를 소급 승인하지 않고 다음 cycle까지
        /// 대기한다. Animator normalized time은 어떤 입력에도 사용하지 않는다.
        /// </summary>
        public static AttackPresentationTimelineWindow ResolveContinuousTimeline(
            double presentationEpochServerTime,
            double observedServerTime,
            double cycleDurationSeconds,
            double[] impactOffsetsSeconds)
        {
            if (!IsFinite(presentationEpochServerTime)
                || !IsFinite(observedServerTime)
                || !IsFinite(cycleDurationSeconds)
                || observedServerTime < presentationEpochServerTime
                || cycleDurationSeconds <= 0d
                || impactOffsetsSeconds == null
                || impactOffsetsSeconds.Length < 1
                || impactOffsetsSeconds.Length > 64)
                return default;

            var sourceOffsets = new double[impactOffsetsSeconds.Length];
            double previous = -1d;
            for (int index = 0; index < impactOffsetsSeconds.Length; index++)
            {
                double offset = impactOffsetsSeconds[index];
                if (!IsFinite(offset)
                    || offset < 0d
                    || offset >= cycleDurationSeconds
                    || (index > 0 && offset <= previous))
                    return default;
                sourceOffsets[index] = offset;
                previous = offset;
            }

            double elapsed = observedServerTime - presentationEpochServerTime;
            double completedCycles = System.Math.Floor(elapsed / cycleDurationSeconds);
            double phaseElapsed = elapsed - completedCycles * cycleDurationSeconds;
            if (phaseElapsed > sourceOffsets[0])
            {
                return AttackPresentationTimelineWindow.CreateWaiting(
                    cycleDurationSeconds - phaseElapsed);
            }

            var effectiveOffsets = new double[sourceOffsets.Length];
            for (int index = 0; index < sourceOffsets.Length; index++)
                effectiveOffsets[index] = sourceOffsets[index] - phaseElapsed;

            return AttackPresentationTimelineWindow.CreateCommitted(
                phaseElapsed,
                cycleDurationSeconds - phaseElapsed,
                effectiveOffsets);
        }

        /// <summary>
        /// provisional/retarget pending뿐 아니라 이미 Attack clip을 계속 재생 중인 모든
        /// 후속 회차가 같은 서버 epoch를 사용해야 하는지를 결정한다.
        /// </summary>
        public static bool ShouldAlignToPresentationEpoch(
            bool attackPresentationActive,
            bool commitPending)
            => attackPresentationActive || commitPending;

        /// <summary>
        /// 네트워크 Attack 표현이 이미 재생 중이면 새 회차 커밋은 승인 비트만 바꾸고
        /// Animator를 다시 시작하지 않는다. 싱글플레이의 기존 시작 호출은 보존한다.
        /// </summary>
        public static bool ShouldCrossFadeAttack(
            bool networkActive,
            bool attackPresentationActive,
            bool restartRequested)
            => !networkActive || (!attackPresentationActive && !restartRequested);

        /// <summary>
        /// 서버가 실제 공격 회차를 커밋했을 때 Host/Client에 보낼 표현 명령을 결정한다.
        ///
        /// Attack 애니메이션은 전투 진입 뒤 계속 루프할 수 있지만 공격 회차 ID는 공격
        /// 쿨다운마다 새로 발급된다. 따라서 이미 Attack 표현 중인지와 관계없이 모든 정상
        /// 커밋은 새 scope를 발행해야 한다. 다만 네트워크 경기에서는 이 명령으로 클립을
        /// 되감지 않고, UnitView의 marker cursor만 새 회차의 HitIndex 0으로 갱신한다.
        /// </summary>
        public static AttackPresentationCommitDispatch ResolveCommitDispatch(
            bool attackPresentationAlreadyActive)
            => new AttackPresentationCommitDispatch(
                shouldPublishScope: true,
                restartAttackCycle: false,
                beginsAttackPresentation: !attackPresentationAlreadyActive);

        public static UnitAttackPresentationDecision Evaluate(
            bool hasEnemyInRange,
            bool attackPresentationActive,
            bool attackStartGateReady)
        {
            if (!hasEnemyInRange)
                return attackPresentationActive
                    ? UnitAttackPresentationDecision.EndAttack
                    : UnitAttackPresentationDecision.NoChange;

            if (attackStartGateReady)
                return UnitAttackPresentationDecision.BeginCommittedAttack;

            return attackPresentationActive
                ? UnitAttackPresentationDecision.KeepAttackAndSuppressImpact
                : UnitAttackPresentationDecision.BeginProvisionalAttack;
        }

        /// <summary>
        /// 서버가 찾은 후보 타겟을 화면에 공개하는 경계를 결정한다.
        /// provisional Start는 target을 원자 payload로 운반하므로 별도 Change가 필요 없고,
        /// Action 소유권 전 후보는 서버 내부에만 보류한다.
        /// </summary>
        public static CombatTargetPublicationDecision ResolveTargetPublication(
            bool serverActionReady,
            bool provisionalStartCarriesTarget)
        {
            if (provisionalStartCarriesTarget)
                return CombatTargetPublicationDecision.PublishWithProvisionalStart;
            return serverActionReady
                ? CombatTargetPublicationDecision.PublishTargetChange
                : CombatTargetPublicationDecision.StageCandidateOnly;
        }

        /// <summary>
        /// 같은 유닛 수명 안에서 서버가 발급한 표현 revision을 정확히 한 번만 수락한다.
        /// 0, 중복, 오래된 값은 fail-closed한다.
        /// </summary>
        public static bool TryAcceptRevision(
            ulong incomingRevision,
            ulong lastAcceptedRevision,
            out ulong nextAcceptedRevision)
        {
            nextAcceptedRevision = lastAcceptedRevision;
            if (incomingRevision == 0UL || incomingRevision <= lastAcceptedRevision)
                return false;
            nextAcceptedRevision = incomingRevision;
            return true;
        }

        public static bool ShouldEmitLocalImpact(
            bool networkActive,
            bool committedPresentationCycle)
            => !networkActive || committedPresentationCycle;

        /// <summary>
        /// 유닛 타입의 C3 이관 상태와 실제 정규 scope 발행 성공을 하나의 표현 모드로
        /// 정규화한다. Supported 타입은 유효 scope가 없으면 반드시 억제하고,
        /// Unresolved 타입은 지원 성공을 가장하지 않은 채 기존 Legacy 표현만 보존한다.
        /// </summary>
        public static AttackPresentationImpactMode ResolveImpactMode(
            UnitAttackShadowSupport support,
            bool hasValidScope)
        {
            if (support == UnitAttackShadowSupport.Supported)
            {
                return hasValidScope
                    ? AttackPresentationImpactMode.Scoped
                    : AttackPresentationImpactMode.Suppressed;
            }

            return support == UnitAttackShadowSupport.Unresolved
                ? AttackPresentationImpactMode.LegacyFallback
                : AttackPresentationImpactMode.Suppressed;
        }

        /// <summary>
        /// Animation Event가 실제 Legacy 공격 VFX/SFX/Tracer를 방출해도 되는지 판정한다.
        /// Scoped는 정규 lease를 이번 marker가 정확히 소비했을 때만 허용하고,
        /// LegacyFallback은 C3 미지원 타입의 기존 표현을 그대로 보존한다.
        /// </summary>
        public static bool ShouldEmitNetworkAttackMarker(
            AttackPresentationImpactMode mode,
            bool consumedValidScope)
        {
            switch (mode)
            {
                case AttackPresentationImpactMode.Scoped:
                    return consumedValidScope;
                case AttackPresentationImpactMode.LegacyFallback:
                    return !consumedValidScope;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 네트워크 Animation Event가 어느 범위의 표현을 방출할 수 있는지 결정한다.
        /// source marker 예약은 서버 commit 명령을 수락한 횟수만큼만 소비할 수 있으므로
        /// LegacyFallback의 루프 Animation Event도 더 이상 무제한으로 통과하지 않는다.
        /// 일반 Stop이 먼저 도착한 예약은 SourceOnly로 남겨 공격자 위치 VFX/SFX만 끝내고,
        /// 타겟·Tracer·피격 신호는 폐기한다.
        /// </summary>
        public static AttackPresentationMarkerEmission ResolveNetworkAttackMarkerEmission(
            AttackPresentationImpactMode mode,
            bool consumedValidScope,
            bool consumedSourceMarker,
            bool sourceOnly)
        {
            if (!consumedSourceMarker)
                return AttackPresentationMarkerEmission.Suppressed;
            if (sourceOnly)
                return AttackPresentationMarkerEmission.SourceOnly;
            return ShouldEmitNetworkAttackMarker(mode, consumedValidScope)
                ? AttackPresentationMarkerEmission.Full
                : AttackPresentationMarkerEmission.Suppressed;
        }

        /// <summary>
        /// 별도 NGO animation level은 target/revision/impact 승인 정보를 갖지 않으므로
        /// Attack 클립 시작 권위가 될 수 없다. StartCombat presentation command만 시작한다.
        /// </summary>
        public static bool ShouldStartAttackFromReplicatedLevelState() => false;

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// 한 서버 공격 회차가 로컬 Animation Event에 부여한 타격 표현 허가다.
    /// Animator의 Attack 상태와 별개로 생존하며, 프로필의 유효 HitIndex만 한 번씩
    /// 소비하게 하여 계속 루프하는 Attack 클립이 추가 VFX/SFX를 만들지 못하게 한다.
    /// </summary>
    public struct AttackPresentationImpactLease
    {
        private AttackerInstanceId _attackerInstanceId;
        private AttackSequenceId _sequenceId;
        private int _nextHitIndex;
        private int _exclusiveEndHitIndex;

        public AttackPresentationImpactLeaseStatus Status { get; private set; }
        public bool IsArmed => Status == AttackPresentationImpactLeaseStatus.Armed;

        /// <summary>
        /// 타겟 변경·사망·Stop 경계에서 아직 남아 있는 큐 신호까지 같은 회차로 닫기 위한
        /// 읽기 전용 scope다. 이미 마지막 marker를 소비했어도 회차 식별자는 Close 전까지
        /// 보존하므로 늦은 결과가 구회차 신호를 다시 살리지 못한다.
        /// </summary>
        public bool TryGetSequenceScope(out AttackPresentationScope scope)
        {
            scope = default;
            if (!_attackerInstanceId.IsValid || !_sequenceId.IsValid)
                return false;
            scope = new AttackPresentationScope(
                _attackerInstanceId,
                _sequenceId,
                0);
            return true;
        }

        public bool TryArm(AttackPresentationScope firstScope, int impactCount)
        {
            Close();
            if (!firstScope.IsValid
                || impactCount <= 0
                || firstScope.HitIndex >= impactCount)
                return false;

            _attackerInstanceId = firstScope.AttackerInstanceId;
            _sequenceId = firstScope.SequenceId;
            _nextHitIndex = firstScope.HitIndex;
            _exclusiveEndHitIndex = impactCount;
            Status = AttackPresentationImpactLeaseStatus.Armed;
            return true;
        }

        public bool TryConsume(out AttackPresentationScope scope)
        {
            scope = default;
            if (!IsArmed || _nextHitIndex >= _exclusiveEndHitIndex)
                return false;

            scope = new AttackPresentationScope(
                _attackerInstanceId,
                _sequenceId,
                _nextHitIndex);
            _nextHitIndex++;
            if (_nextHitIndex >= _exclusiveEndHitIndex)
            {
                _nextHitIndex = 0;
                _exclusiveEndHitIndex = 0;
                Status = AttackPresentationImpactLeaseStatus.Consumed;
            }

            return scope.IsValid;
        }

        public void Close()
        {
            _attackerInstanceId = AttackerInstanceId.None;
            _sequenceId = AttackSequenceId.None;
            _nextHitIndex = 0;
            _exclusiveEndHitIndex = 0;
            Status = AttackPresentationImpactLeaseStatus.Closed;
        }
    }

    public enum AttackPresentationImpactLeaseStatus : byte
    {
        Closed = 0,
        Armed = 1,
        Consumed = 2
    }

    /// <summary>
    /// 네트워크 공격 marker의 표현 허가 모드. bool 두 개로 표현하면
    /// impactEnabled=true/default scope 같은 모순 조합이 생기므로 상호 배타적인 값으로 고정한다.
    /// </summary>
    public enum AttackPresentationImpactMode : byte
    {
        Suppressed = 0,
        Scoped = 1,
        LegacyFallback = 2
    }

    /// <summary>
    /// 공격 Animation Event가 사용할 수 있는 표현 범위다. SourceOnly는 공격자 위치의
    /// VFX/SFX만 허용하고 타겟 조회·Tracer·피격 신호를 금지한다.
    /// </summary>
    public enum AttackPresentationMarkerEmission : byte
    {
        Suppressed = 0,
        Full = 1,
        SourceOnly = 2
    }

    /// <summary>
    /// 서버가 수락된 presentation revision으로 커밋한 공격자 발사 marker의 bounded 예약이다.
    /// 정규 C3 scope와 별도이므로 LegacyFallback에 가짜 scope를 만들지 않는다. 일반 Stop은
    /// 남은 marker를 SourceOnly로 바꾸고, 명시적 취소·새 revision·수명 종료는 Close한다.
    /// </summary>
    public struct AttackPresentationSourceMarkerLease
    {
        private const int MaximumMarkerCount = 64;

        private ulong _presentationRevision;
        private int _remainingMarkerCount;
        private bool _sourceOnly;

        public AttackPresentationSourceMarkerLeaseStatus Status { get; private set; }
        public bool IsArmed => Status == AttackPresentationSourceMarkerLeaseStatus.Armed;
        public ulong PresentationRevision => _presentationRevision;
        public int RemainingMarkerCount => _remainingMarkerCount;
        public bool IsSourceOnly => IsArmed && _sourceOnly;

        public bool TryArm(ulong presentationRevision, int markerCount)
        {
            Close();
            if (presentationRevision == 0UL
                || markerCount <= 0
                || markerCount > MaximumMarkerCount)
                return false;

            _presentationRevision = presentationRevision;
            _remainingMarkerCount = markerCount;
            _sourceOnly = false;
            Status = AttackPresentationSourceMarkerLeaseStatus.Armed;
            return true;
        }

        public bool PreserveSourceOnly()
        {
            if (!IsArmed || _remainingMarkerCount <= 0)
                return false;
            _sourceOnly = true;
            return true;
        }

        public bool TryConsume(out bool sourceOnly)
        {
            sourceOnly = false;
            if (!IsArmed || _remainingMarkerCount <= 0)
                return false;

            sourceOnly = _sourceOnly;
            _remainingMarkerCount--;
            if (_remainingMarkerCount == 0)
            {
                _presentationRevision = 0UL;
                _sourceOnly = false;
                Status = AttackPresentationSourceMarkerLeaseStatus.Consumed;
            }
            return true;
        }

        public void Close()
        {
            _presentationRevision = 0UL;
            _remainingMarkerCount = 0;
            _sourceOnly = false;
            Status = AttackPresentationSourceMarkerLeaseStatus.Closed;
        }
    }

    public enum AttackPresentationSourceMarkerLeaseStatus : byte
    {
        Closed = 0,
        Armed = 1,
        Consumed = 2
    }

    public readonly struct AttackPresentationCommitWindow
    {
        public bool IsValid { get; }
        public bool ShouldCommit { get; }
        public double OvershootSeconds { get; }
        public double WaitSeconds { get; }

        public AttackPresentationCommitWindow(
            bool isValid,
            bool shouldCommit,
            double overshootSeconds,
            double waitSeconds)
        {
            IsValid = isValid;
            ShouldCommit = shouldCommit;
            OvershootSeconds = overshootSeconds;
            WaitSeconds = waitSeconds;
        }
    }

    /// <summary>
    /// 하나의 서버 epoch/phase 계산에서 나온 연속 Attack 회차의 원자 타이밍이다.
    /// 배열은 생성 시 복사된 private 저장소이므로 호출자가 원본을 바꿀 수 없다.
    /// </summary>
    public readonly struct AttackPresentationTimelineWindow
    {
        private readonly double[] _effectiveImpactOffsets;

        public bool IsValid { get; }
        public bool ShouldCommit { get; }
        public double OvershootSeconds { get; }
        public double WaitSeconds { get; }
        public double EffectiveCooldownSeconds { get; }
        public int ImpactCount => _effectiveImpactOffsets?.Length ?? 0;

        private AttackPresentationTimelineWindow(
            bool shouldCommit,
            double overshootSeconds,
            double waitSeconds,
            double effectiveCooldownSeconds,
            double[] effectiveImpactOffsets)
        {
            IsValid = true;
            ShouldCommit = shouldCommit;
            OvershootSeconds = overshootSeconds;
            WaitSeconds = waitSeconds;
            EffectiveCooldownSeconds = effectiveCooldownSeconds;
            _effectiveImpactOffsets = effectiveImpactOffsets;
        }

        public double GetEffectiveImpactOffset(int hitIndex)
        {
            if (_effectiveImpactOffsets == null
                || hitIndex < 0
                || hitIndex >= _effectiveImpactOffsets.Length)
                return double.NaN;
            return _effectiveImpactOffsets[hitIndex];
        }

        internal static AttackPresentationTimelineWindow CreateWaiting(
            double waitSeconds)
            => new AttackPresentationTimelineWindow(
                shouldCommit: false,
                overshootSeconds: 0d,
                waitSeconds: waitSeconds,
                effectiveCooldownSeconds: 0d,
                effectiveImpactOffsets: System.Array.Empty<double>());

        internal static AttackPresentationTimelineWindow CreateCommitted(
            double overshootSeconds,
            double effectiveCooldownSeconds,
            double[] effectiveImpactOffsets)
            => new AttackPresentationTimelineWindow(
                shouldCommit: true,
                overshootSeconds: overshootSeconds,
                waitSeconds: 0d,
                effectiveCooldownSeconds: effectiveCooldownSeconds,
                effectiveImpactOffsets: effectiveImpactOffsets);
    }

    /// <summary>
    /// 한 서버 공격 커밋이 표현 계층에 전달해야 하는 두 책임을 분리한 값이다.
    /// scope 발행은 매 커밋마다 필요하지만 Animator 시작은 최초 명령에서만 의미가 있다.
    /// </summary>
    public readonly struct AttackPresentationCommitDispatch
    {
        public bool ShouldPublishScope { get; }
        public bool RestartAttackCycle { get; }
        public bool BeginsAttackPresentation { get; }

        public AttackPresentationCommitDispatch(
            bool shouldPublishScope,
            bool restartAttackCycle,
            bool beginsAttackPresentation)
        {
            ShouldPublishScope = shouldPublishScope;
            RestartAttackCycle = restartAttackCycle;
            BeginsAttackPresentation = beginsAttackPresentation;
        }
    }

    public enum AttackPresentationRendezvousStatus : byte
    {
        Invalid = 0,
        Buffered = 1,
        Released = 2,
        Duplicate = 3,
        CapacityExceeded = 4,
        Retired = 5
    }

    /// <summary>
    /// 서버 피해 결과와 로컬 Animation Event를 정확한 공격 scope에서만 만나는 순수 저장소다.
    ///
    /// 네트워크에서는 둘 중 어느 쪽이 먼저 도착할지 알 수 없다. marker를 버리면 다음 공격
    /// 결과를 당겨 쓰고, 결과를 공격자 FIFO로만 보관하면 다음 marker가 오래된 결과를 꺼낸다.
    /// 이 타입은 양쪽 순서를 모두 보관하되 `(instance, sequence, hitIndex)`가 같은 경우에만
    /// payload를 방출한다. Unity/VFX/HP API가 없으므로 서버 권위 상태를 변경할 수 없다.
    /// </summary>
    public sealed class AttackPresentationImpactRendezvous<T>
    {
        public const int MaximumScopeCount = 256;
        public const double SignalRetentionSeconds = 2.0d;

        private sealed class ScopeState
        {
            public int AttackerId;
            public double CreatedAt;
            public bool SignalObserved;
            public double SignalExpiresAt;
            public readonly List<PendingResult> Pending = new List<PendingResult>();
            public readonly HashSet<AttackResultKey> SeenResults =
                new HashSet<AttackResultKey>();
        }

        private readonly struct PendingResult
        {
            public readonly AttackResultKey Key;
            public readonly T Value;
            public readonly double ExpiresAt;

            public PendingResult(AttackResultKey key, T value, double expiresAt)
            {
                Key = key;
                Value = value;
                ExpiresAt = expiresAt;
            }
        }

        private readonly Dictionary<AttackPresentationScope, ScopeState> _states =
            new Dictionary<AttackPresentationScope, ScopeState>();
        private readonly Dictionary<AttackerInstanceId, AttackSequenceId> _retiredThrough =
            new Dictionary<AttackerInstanceId, AttackSequenceId>();
        private readonly List<AttackPresentationScope> _remove =
            new List<AttackPresentationScope>();

        public int ScopeCountForValidation => _states.Count;

        /// <summary>
        /// 로컬 marker/tracer가 먼저 오면 신호를 보관하고, 결과가 먼저 왔다면 같은 scope의
        /// 보류 결과를 모두 반환한다. AoE는 한 HitIndex가 여러 피해자를 만들 수 있으므로
        /// 신호를 첫 결과에서 닫지 않고 짧은 reorder 창 동안 유지한다.
        /// </summary>
        public AttackPresentationRendezvousStatus ObserveSignal(
            int attackerId,
            AttackPresentationScope scope,
            double now,
            List<T> released)
        {
            if (released == null || attackerId < 0 || !scope.IsValid || !IsFinite(now))
                return AttackPresentationRendezvousStatus.Invalid;

            SweepSignalWindows(now);
            if (IsRetired(scope))
                return AttackPresentationRendezvousStatus.Retired;
            if (!TryGetOrCreate(attackerId, scope, now, out ScopeState state))
                return AttackPresentationRendezvousStatus.CapacityExceeded;
            if (state.AttackerId != attackerId)
                return AttackPresentationRendezvousStatus.Invalid;
            if (state.SignalObserved && now <= state.SignalExpiresAt)
                return AttackPresentationRendezvousStatus.Duplicate;

            state.SignalObserved = true;
            state.SignalExpiresAt = now + SignalRetentionSeconds;
            for (int index = 0; index < state.Pending.Count; index++)
                released.Add(state.Pending[index].Value);
            bool hadPending = state.Pending.Count > 0;
            state.Pending.Clear();
            return hadPending
                ? AttackPresentationRendezvousStatus.Released
                : AttackPresentationRendezvousStatus.Buffered;
        }

        /// <summary>
        /// 권위 결과가 먼저 오면 exact scope에 보관한다. marker/tracer 신호가 이미 같은
        /// scope에서 관측됐다면 즉시 반환한다. 다른 회차의 신호는 절대 후보가 아니다.
        /// </summary>
        public AttackPresentationRendezvousStatus ObserveResult(
            int attackerId,
            AttackResultKey key,
            T value,
            double now,
            double timeoutSeconds,
            List<T> released)
        {
            AttackPresentationScope scope = AttackPresentationScope.FromResultKey(key);
            if (released == null || attackerId < 0 || !key.IsValid || !scope.IsValid
                || !IsFinite(now) || !IsFinite(timeoutSeconds) || timeoutSeconds <= 0d)
                return AttackPresentationRendezvousStatus.Invalid;

            SweepSignalWindows(now);
            if (IsRetired(scope))
                return AttackPresentationRendezvousStatus.Retired;
            if (!TryGetOrCreate(attackerId, scope, now, out ScopeState state))
                return AttackPresentationRendezvousStatus.CapacityExceeded;
            if (state.AttackerId != attackerId)
                return AttackPresentationRendezvousStatus.Invalid;
            if (!state.SeenResults.Add(key))
                return AttackPresentationRendezvousStatus.Duplicate;

            if (state.SignalObserved && now <= state.SignalExpiresAt)
            {
                released.Add(value);
                return AttackPresentationRendezvousStatus.Released;
            }

            state.Pending.Add(new PendingResult(key, value, now + timeoutSeconds));
            return AttackPresentationRendezvousStatus.Buffered;
        }

        /// <summary>정상 marker를 끝내 만나지 못한 결과만 timeout 안전망으로 반환한다.</summary>
        public void CollectExpired(double now, List<T> released)
        {
            if (released == null || !IsFinite(now)) return;
            SweepSignalWindows(now);
            _remove.Clear();
            foreach (KeyValuePair<AttackPresentationScope, ScopeState> pair in _states)
            {
                ScopeState state = pair.Value;
                for (int index = state.Pending.Count - 1; index >= 0; index--)
                {
                    if (now < state.Pending[index].ExpiresAt) continue;
                    released.Add(state.Pending[index].Value);
                    state.Pending.RemoveAt(index);
                }
                if (!state.SignalObserved && state.Pending.Count == 0)
                    _remove.Add(pair.Key);
            }
            RemoveMarkedStates();
        }

        public void FlushAttacker(int attackerId, List<T> released)
        {
            if (released == null || attackerId < 0) return;
            _remove.Clear();
            foreach (KeyValuePair<AttackPresentationScope, ScopeState> pair in _states)
            {
                if (pair.Value.AttackerId != attackerId) continue;
                AppendPending(pair.Value, released);
                _remove.Add(pair.Key);
            }
            RemoveMarkedStates();
        }

        /// <summary>
        /// 타겟 변경·사망·Stop으로 닫힌 정확한 공격 회차를 폐기한다. 보류 결과를 방출하지
        /// 않으며, 같은 instance의 해당 sequence 이하를 tombstone으로 남겨 늦은 old result가
        /// 새 signal-only 상태를 만들거나 timeout으로 표현되는 것을 막는다.
        /// </summary>
        public AttackPresentationRendezvousStatus RetireSequence(
            int attackerId,
            AttackPresentationScope scope)
        {
            if (attackerId < 0 || !scope.IsValid)
                return AttackPresentationRendezvousStatus.Invalid;

            if (_retiredThrough.TryGetValue(
                    scope.AttackerInstanceId,
                    out AttackSequenceId retired)
                && retired.CompareTo(scope.SequenceId) >= 0)
                return AttackPresentationRendezvousStatus.Duplicate;
            _retiredThrough[scope.AttackerInstanceId] = scope.SequenceId;
            _remove.Clear();
            foreach (KeyValuePair<AttackPresentationScope, ScopeState> pair in _states)
            {
                AttackPresentationScope candidate = pair.Key;
                if (candidate.AttackerInstanceId == scope.AttackerInstanceId
                    && candidate.SequenceId.CompareTo(scope.SequenceId) <= 0)
                    _remove.Add(candidate);
            }
            RemoveMarkedStates();
            return AttackPresentationRendezvousStatus.Released;
        }

        public void FlushWhere(Predicate<T> predicate, List<T> released)
        {
            if (predicate == null || released == null) return;
            _remove.Clear();
            foreach (KeyValuePair<AttackPresentationScope, ScopeState> pair in _states)
            {
                ScopeState state = pair.Value;
                for (int index = state.Pending.Count - 1; index >= 0; index--)
                {
                    if (!predicate(state.Pending[index].Value)) continue;
                    released.Add(state.Pending[index].Value);
                    state.Pending.RemoveAt(index);
                }
                if (!state.SignalObserved && state.Pending.Count == 0)
                    _remove.Add(pair.Key);
            }
            RemoveMarkedStates();
        }

        public void Clear()
        {
            _states.Clear();
            _retiredThrough.Clear();
            _remove.Clear();
        }

        private bool IsRetired(AttackPresentationScope scope)
            => _retiredThrough.TryGetValue(
                    scope.AttackerInstanceId,
                    out AttackSequenceId retired)
                && retired.CompareTo(scope.SequenceId) >= 0;

        private bool TryGetOrCreate(
            int attackerId,
            AttackPresentationScope scope,
            double now,
            out ScopeState state)
        {
            if (_states.TryGetValue(scope, out state)) return true;
            if (_states.Count >= MaximumScopeCount) return false;
            state = new ScopeState
            {
                AttackerId = attackerId,
                CreatedAt = now,
                SignalObserved = false,
                SignalExpiresAt = double.NaN
            };
            _states.Add(scope, state);
            return true;
        }

        private void SweepSignalWindows(double now)
        {
            _remove.Clear();
            foreach (KeyValuePair<AttackPresentationScope, ScopeState> pair in _states)
            {
                ScopeState state = pair.Value;
                if (state.SignalObserved && now > state.SignalExpiresAt)
                {
                    state.SignalObserved = false;
                    state.SignalExpiresAt = double.NaN;
                }
                if (!state.SignalObserved && state.Pending.Count == 0
                    && now - state.CreatedAt > SignalRetentionSeconds)
                    _remove.Add(pair.Key);
            }
            RemoveMarkedStates();
        }

        private static void AppendPending(ScopeState state, List<T> released)
        {
            for (int index = 0; index < state.Pending.Count; index++)
                released.Add(state.Pending[index].Value);
            state.Pending.Clear();
        }

        private void RemoveMarkedStates()
        {
            for (int index = 0; index < _remove.Count; index++)
                _states.Remove(_remove[index]);
            _remove.Clear();
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public enum UnitAttackPresentationDecision : byte
    {
        NoChange = 0,
        BeginProvisionalAttack = 1,
        KeepAttackAndSuppressImpact = 2,
        BeginCommittedAttack = 3,
        EndAttack = 4
    }

    public enum CombatTargetPublicationDecision : byte
    {
        StageCandidateOnly = 0,
        PublishWithProvisionalStart = 1,
        PublishTargetChange = 2
    }
}
