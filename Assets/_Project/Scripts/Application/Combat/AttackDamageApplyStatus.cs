using Hexiege.Application.Combat.Sequencing;

namespace Hexiege.Application
{
    /// <summary>
    /// Legacy 지연 피해 writer가 실제로 선택한 종료 경로다. Tracer C가 HP 차이를 바깥에서
    /// 추정하지 않고 적용 여부를 관측할 수 있게 하는 작은 Application 값 계약이다.
    /// </summary>
    public enum AttackDamageApplyStatus : byte
    {
        Applied = 0,
        AttackerUnavailable = 1,
        TargetUnavailable = 2,
        /// <summary>
        /// 타격 순간 공격자와 타겟은 존재하지만 서버 권위 사거리 또는 공격 방향 계약을
        /// 통과하지 못했다. 타겟 고정은 대상을 유지할 뿐 명중을 보장하지 않는다.
        /// </summary>
        CombatConditionFailed = 3,
        /// <summary>
        /// 서버 권위 Impact 승인 자체가 아직 없거나 유효하지 않다. 이 상태에서는 피해를
        /// 추측 적용하지 않고 다음 진단 증거가 원인을 드러내도록 fail-closed한다.
        /// </summary>
        AuthorizationUnavailable = 4,
        /// <summary>
        /// 공격자와 커밋 타겟은 살아 있지만 서버 pose source/Transform/sample을 얻지 못해
        /// Impact 공간 판정을 수행할 수 없었다. 정상 Miss나 시간 미도달로 위조하지 않는다.
        /// </summary>
        PoseUnavailable = 5,
        /// <summary>
        /// [2026-11-23 연막탄 단계 S-2 — Plan P-4] 공격은 승인됐지만 맞을 아군 유닛이 연막 안이라 서버 회피 판정에서 「피했다」.
        /// 피해 · 화상 · 피해 이벤트 · 피해 숫자가 모두 없다(writer가 ExecuteAttack을 부르지 않는다).
        /// C2 코디네이터는 「승인 명중 + 이 값」을 AttackImpactOutcome.Evaded로 닫는다(불일치 거절이 아니다).
        /// 정수 값은 맨 뒤에 붙였다(기존 값 0~5는 그대로).
        /// </summary>
        Evaded = 6
    }

    /// <summary>
    /// 서버 Impact pose 획득 단계의 원인을 보존하는 typed 상태다. 생명주기 부재는 정규 Miss로,
    /// 살아 있는 객체의 adapter/sample 실패는 내부 실패로 분리하기 위해 사용한다.
    /// </summary>
    public enum AttackImpactPoseCaptureStatus : byte
    {
        Captured = 0,
        AttackerUnavailable = 1,
        TargetUnavailable = 2,
        AmbiguousUnavailable = 3,
        TargetStateUnavailable = 4,
        PoseSourceUnavailable = 5,
        CaptureFailed = 6,
        InvalidSample = 7,
        InfrastructureFailure = 8
    }

    /// <summary>
    /// Unity 객체를 모르는 순수 분류 seam이다. production과 Editor 회귀가 동일한 우선순위를
    /// 사용하므로 제거된 타겟과 살아 있는 타겟의 adapter 실패가 섞이지 않는다.
    /// </summary>
    public static class AttackImpactPoseCaptureClassifier
    {
        public static AttackImpactPoseCaptureStatus Classify(
            bool attackerAvailable,
            bool targetStateObserved,
            bool targetAvailable,
            bool poseSourceAvailable,
            bool captureSucceeded,
            bool sampleValid,
            bool infrastructureFailure)
        {
            if (infrastructureFailure)
                return AttackImpactPoseCaptureStatus.InfrastructureFailure;
            if (!targetStateObserved)
                return AttackImpactPoseCaptureStatus.TargetStateUnavailable;
            if (!attackerAvailable && !targetAvailable)
                return AttackImpactPoseCaptureStatus.AmbiguousUnavailable;
            if (!attackerAvailable)
                return AttackImpactPoseCaptureStatus.AttackerUnavailable;
            if (!targetAvailable)
                return AttackImpactPoseCaptureStatus.TargetUnavailable;
            if (!poseSourceAvailable)
                return AttackImpactPoseCaptureStatus.PoseSourceUnavailable;
            if (!captureSucceeded)
                return AttackImpactPoseCaptureStatus.CaptureFailed;
            if (!sampleValid)
                return AttackImpactPoseCaptureStatus.InvalidSample;
            return AttackImpactPoseCaptureStatus.Captured;
        }

        public static bool IsCanonicalUnavailableMiss(
            AttackImpactPoseCaptureStatus status)
            => status == AttackImpactPoseCaptureStatus.AttackerUnavailable
                || status == AttackImpactPoseCaptureStatus.TargetUnavailable;
    }

    /// <summary>
    /// Legacy 서버 피해 writer가 한 번의 주 타겟 공격 호출에서 확정한 읽기 전용 결과다.
    /// Tracer C가 writer 바깥에서 HP 차이를 추정하지 않도록 실제 적용 경계 안에서 만든다.
    /// </summary>
    public readonly struct AttackDamageObservation
    {
        public AttackDamageApplyStatus Status { get; }
        public int AppliedAmount { get; }
        public int ResultingHp { get; }
        public int VictimPresentationType { get; }
        public int VictimTeam { get; }
        public bool HasImpactPosition { get; }
        public WorldPointXZ ImpactPosition { get; }
        public bool HasPresentationSnapshot => VictimPresentationType >= 0 && VictimTeam >= 0;

        // [2026-11-23 연막탄 단계 S-2] 옛 식(아래)은 「회피(Evaded)」 관측을 무효로 본다 → 새 식으로 바꿨다.
        //   회피 관측 = 피해 0 · 결과 HP(지금 HP, 0 이상) · 피해자 종류 · 팀이 있음 · 좌표는 있으면 유효해야 함.
        //   (적용 · 거절 두 갈래의 판정은 옛 식과 글자 하나 다르지 않다 — 회피 갈래만 앞에 붙였다.)
        // public bool IsValid => Status == AttackDamageApplyStatus.Applied
        //     ? AppliedAmount >= 0 && ResultingHp >= 0
        //         && HasPresentationSnapshot
        //         && HasImpactPosition && ImpactPosition.IsValid
        //     : (Status == AttackDamageApplyStatus.AttackerUnavailable
        //         || Status == AttackDamageApplyStatus.TargetUnavailable
        //         || Status == AttackDamageApplyStatus.CombatConditionFailed
        //         || Status == AttackDamageApplyStatus.AuthorizationUnavailable
        //         || Status == AttackDamageApplyStatus.PoseUnavailable)
        //       && AppliedAmount == 0
        //       && ResultingHp == -1
        //       && VictimPresentationType == -1
        //       && VictimTeam == -1
        //       && !HasImpactPosition;
        public bool IsValid => Status == AttackDamageApplyStatus.Evaded
            ? AppliedAmount == 0 && ResultingHp >= 0
                && HasPresentationSnapshot
                && (!HasImpactPosition || ImpactPosition.IsValid)
            : Status == AttackDamageApplyStatus.Applied
            ? AppliedAmount >= 0 && ResultingHp >= 0
                && HasPresentationSnapshot
                && HasImpactPosition && ImpactPosition.IsValid
            : (Status == AttackDamageApplyStatus.AttackerUnavailable
                || Status == AttackDamageApplyStatus.TargetUnavailable
                || Status == AttackDamageApplyStatus.CombatConditionFailed
                || Status == AttackDamageApplyStatus.AuthorizationUnavailable
                || Status == AttackDamageApplyStatus.PoseUnavailable)
              && AppliedAmount == 0
              && ResultingHp == -1
              && VictimPresentationType == -1
              && VictimTeam == -1
              && !HasImpactPosition;

        public AttackDamageObservation(
            AttackDamageApplyStatus status,
            int appliedAmount,
            int resultingHp,
            int victimPresentationType = -1,
            int victimTeam = -1,
            bool hasImpactPosition = false,
            WorldPointXZ impactPosition = default)
        {
            Status = status;
            AppliedAmount = appliedAmount;
            ResultingHp = resultingHp;
            VictimPresentationType = victimPresentationType;
            VictimTeam = victimTeam;
            HasImpactPosition = hasImpactPosition;
            ImpactPosition = impactPosition;
        }

        public static AttackDamageObservation Unavailable(AttackDamageApplyStatus status)
            => new AttackDamageObservation(status, 0, -1);
    }

    /// <summary>
    /// 한 서버 Impact 호출 안에서 실제로 HP가 감소한 피해자 한 명의 읽기 전용 사실이다.
    /// 이 값은 피해를 다시 적용하지 않으며, 완료 묶음의 표현 입력을 만들 때만 사용한다.
    /// </summary>
    public readonly struct AppliedAttackPresentationFact
    {
        public int VictimKind { get; }
        public int VictimId { get; }
        public int EffectKind { get; }
        public int ResultOrdinal { get; }
        public int AppliedAmount { get; }
        public int ResultingHp { get; }
        public int VictimPresentationType { get; }
        public int VictimTeam { get; }
        public bool HasImpactPosition { get; }
        public WorldPointXZ ImpactPosition { get; }

        public bool IsValid => (VictimKind == 1 || VictimKind == 2)
            && VictimId >= 0 && EffectKind > 0 && ResultOrdinal >= 0
            && AppliedAmount > 0 && ResultingHp >= 0
            && VictimPresentationType >= 0 && VictimTeam >= 0
            && HasImpactPosition && ImpactPosition.IsValid;

        public AppliedAttackPresentationFact(
            int victimKind,
            int victimId,
            int effectKind,
            int resultOrdinal,
            int appliedAmount,
            int resultingHp,
            int victimPresentationType,
            int victimTeam,
            bool hasImpactPosition,
            WorldPointXZ impactPosition)
        {
            VictimKind = victimKind;
            VictimId = victimId;
            EffectKind = effectKind;
            ResultOrdinal = resultOrdinal;
            AppliedAmount = appliedAmount;
            ResultingHp = resultingHp;
            VictimPresentationType = victimPresentationType;
            VictimTeam = victimTeam;
            HasImpactPosition = hasImpactPosition;
            ImpactPosition = impactPosition;
        }
    }

    public enum AttackDamagePresentationBundleStatus : byte
    {
        Complete = 0,
        InvalidScope = 1,
        NestedCollection = 2,
        CapacityExceeded = 3,
        InvalidFact = 4,
        DuplicateKey = 5
    }

    /// <summary>
    /// 주 타겟 writer 결과와, 같은 동기 호출에서 실제 적용된 모든 피해 이벤트의 완결 묶음이다.
    /// 빈 Facts도 유효하다. 예를 들어 권위 Miss는 주 결과만 전송하고 적용 피해 사실은 없다.
    /// </summary>
    public readonly struct AttackDamagePresentationBundleObservation
    {
        public AttackDamageObservation PrimaryObservation { get; }
        public AppliedAttackPresentationFact[] Facts { get; }
        public AttackDamagePresentationBundleStatus Status { get; }
        public bool IsComplete => Status == AttackDamagePresentationBundleStatus.Complete
            && PrimaryObservation.IsValid && Facts != null;

        public AttackDamagePresentationBundleObservation(
            AttackDamageObservation primaryObservation,
            AppliedAttackPresentationFact[] facts,
            AttackDamagePresentationBundleStatus status)
        {
            PrimaryObservation = primaryObservation;
            Facts = facts;
            Status = status;
        }
    }
}
