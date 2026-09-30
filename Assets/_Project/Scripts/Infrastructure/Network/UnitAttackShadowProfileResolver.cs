using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Domain의 25개 UnitType을 Tracer C 순수 프로필로 변환한다.
    /// switch의 모든 행을 명시해 신규 유닛이 추가되거나 기존 행이 빠지면 검증이 즉시 실패한다.
    /// Projectile/Traveling 공격은 현재 Sequencer가 지원하지 않으므로 임의로 Melee/Hitscan으로
    /// 낮추지 않고 Unresolved 상태를 유지한다.
    /// </summary>
    public static class UnitAttackShadowProfileResolver
    {
        public const int PresentationContractSchemaRevision = 1;
        private static readonly UnitType[] ManifestTypes =
        {
            UnitType.Pistoleer, UnitType.Assault, UnitType.Sniper,
            UnitType.LittleKnight, UnitType.SpearMan, UnitType.BattleAxe,
            UnitType.Tank, UnitType.CannonCart,
            UnitType.FlameSpirit, UnitType.EmberSpirit, UnitType.InfernoSpirit,
            UnitType.DustSpirit, UnitType.BoulderSpirit, UnitType.QuakeSpirit,
            UnitType.TideSpirit, UnitType.StreamSpirit, UnitType.TorrentSpirit,
            UnitType.BearGuard, UnitType.FoxMagician, UnitType.LionKnight,
            UnitType.RhinoBreaker, UnitType.EagleArcher, UnitType.RabbitTrickster,
            UnitType.MushroomBomber, UnitType.BloomFairy
        };

        public static IReadOnlyList<UnitType> AllManifestTypes => ManifestTypes;

        public static bool TryResolve(UnitType type, out UnitAttackShadowProfile profile)
        {
            switch (type)
            {
                case UnitType.Pistoleer: return Supported(type, AttackDeliveryKind.Hitscan, 1, false, out profile);
                case UnitType.Assault: return Supported(type, AttackDeliveryKind.Hitscan, 1, false, out profile);
                case UnitType.Sniper: return Supported(type, AttackDeliveryKind.Hitscan, 1, false, out profile);
                case UnitType.LittleKnight: return Supported(type, AttackDeliveryKind.MeleeContact, 2, false, out profile);
                case UnitType.SpearMan: return Supported(type, AttackDeliveryKind.MeleeContact, 1, false, out profile);
                case UnitType.BattleAxe: return Supported(type, AttackDeliveryKind.MeleeContact, 1, true, out profile);
                case UnitType.FlameSpirit: return Supported(type, AttackDeliveryKind.MeleeContact, 6, false, out profile);
                case UnitType.EmberSpirit: return Supported(type, AttackDeliveryKind.MeleeContact, 1, false, out profile);
                case UnitType.DustSpirit: return Supported(type, AttackDeliveryKind.MeleeContact, 1, false, out profile);
                case UnitType.BoulderSpirit: return Supported(type, AttackDeliveryKind.MeleeContact, 1, false, out profile);
                case UnitType.TideSpirit: return Supported(type, AttackDeliveryKind.MeleeContact, 1, false, out profile);
                case UnitType.BearGuard: return Supported(type, AttackDeliveryKind.MeleeContact, 1, false, out profile);
                case UnitType.LionKnight: return Supported(type, AttackDeliveryKind.MeleeContact, 2, false, out profile);
                case UnitType.RabbitTrickster: return Supported(type, AttackDeliveryKind.MeleeContact, 1, false, out profile);

                case UnitType.Tank: return UnresolvedProjectile(type, false, "projectile-system-unresolved", out profile);
                case UnitType.CannonCart: return UnresolvedProjectile(type, false, "projectile-system-unresolved", out profile);
                case UnitType.InfernoSpirit: return UnresolvedProjectile(type, true, "projectile-and-periodic-result-unresolved", out profile);
                case UnitType.StreamSpirit: return UnresolvedProjectile(type, false, "projectile-timeline-unresolved", out profile);
                case UnitType.FoxMagician: return UnresolvedProjectile(type, false, "projectile-system-unresolved", out profile);
                case UnitType.EagleArcher: return UnresolvedProjectile(type, false, "projectile-animation-selection-unresolved", out profile);
                case UnitType.MushroomBomber: return UnresolvedProjectile(type, true, "locked-point-projectile-and-periodic-result-unresolved", out profile);

                case UnitType.QuakeSpirit: return Supported(type, AttackDeliveryKind.MeleeContact, 1, true, out profile);

                case UnitType.RhinoBreaker:
                    return Supported(type, AttackDeliveryKind.MeleeContact, 1, false, out profile);

                case UnitType.TorrentSpirit:
                    profile = Create(
                        type, AttackTargetMode.TargetLocked, AttackDeliveryKind.TravelingArea,
                        LegacyAttackExecutionKind.ActiveTravelingArea,
                        UnitAttackShadowSupport.Unresolved, 1, true,
                        "traveling-area-sequence-unresolved");
                    return true;

                case UnitType.BloomFairy:
                    profile = Create(
                        type, AttackTargetMode.TargetLocked, AttackDeliveryKind.Hitscan,
                        LegacyAttackExecutionKind.SeparateHealPipeline,
                        UnitAttackShadowSupport.NotApplicableAttack, 0, true,
                        "separate-heal-pipeline");
                    return true;

                default:
                    profile = default;
                    return false;
            }
        }

        /// <summary>
        /// manifest 자체뿐 아니라 현재 런타임 HitFrameTimes가 지원 프로필의 타격 수와 맞는지도 검증한다.
        /// ScriptableObject/Animator가 미완료인 유닛은 성공으로 가장하지 않고 구체적인 이유를 반환한다.
        /// </summary>
        public static bool TryResolveRuntime(
            UnitData unit,
            out UnitAttackShadowProfile profile,
            out string reason)
        {
            profile = default;
            reason = "accepted";
            if (unit == null || !TryResolve(unit.Type, out profile) || !profile.IsValid)
            {
                reason = "manifest-missing-or-invalid";
                return false;
            }

            if (!profile.CanRunSequencer)
            {
                reason = profile.Reason;
                return false;
            }

            if (unit.HitFrameTimes == null || unit.HitFrameTimes.Length != profile.ExpectedImpactCount)
            {
                reason = "runtime-impact-count-mismatch";
                return false;
            }

            for (int index = 0; index < unit.HitFrameTimes.Length; index++)
            {
                float value = unit.HitFrameTimes[index];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f
                    || (index > 0 && value < unit.HitFrameTimes[index - 1]))
                {
                    reason = "runtime-impact-timeline-invalid";
                    return false;
                }
            }

            return true;
        }

        private static bool Supported(
            UnitType type,
            AttackDeliveryKind delivery,
            int impactCount,
            bool secondary,
            out UnitAttackShadowProfile profile)
        {
            profile = Create(
                type, AttackTargetMode.TargetLocked, delivery,
                secondary
                    ? LegacyAttackExecutionKind.TimerImpactWithSecondaryEffect
                    : LegacyAttackExecutionKind.TimerImpact,
                UnitAttackShadowSupport.Supported,
                impactCount,
                secondary,
                secondary ? "primary-direct-supported-secondary-observed-only" : "supported");
            return true;
        }

        private static bool UnresolvedProjectile(
            UnitType type,
            bool secondary,
            string reason,
            out UnitAttackShadowProfile profile)
        {
            profile = Create(
                type,
                type == UnitType.MushroomBomber
                    ? AttackTargetMode.LockedPoint
                    : AttackTargetMode.TargetLocked,
                AttackDeliveryKind.ProjectileImpact,
                secondary
                    ? LegacyAttackExecutionKind.TimerImpactWithSecondaryEffect
                    : LegacyAttackExecutionKind.TimerImpact,
                UnitAttackShadowSupport.Unresolved,
                1,
                secondary,
                reason);
            return true;
        }

        private static UnitAttackShadowProfile Create(
            UnitType type,
            AttackTargetMode targetMode,
            AttackDeliveryKind delivery,
            LegacyAttackExecutionKind legacyExecution,
            UnitAttackShadowSupport support,
            int impactCount,
            bool secondary,
            string reason)
        {
            return new UnitAttackShadowProfile(
                (int)type, type.ToString(), targetMode, delivery, legacyExecution,
                support, impactCount, secondary, reason);
        }

        public static bool ValidateManifest(out string reason)
        {
            reason = null;
            UnitType[] enumTypes = (UnitType[])Enum.GetValues(typeof(UnitType));
            if (enumTypes.Length != 25 || ManifestTypes.Length != enumTypes.Length)
            {
                reason = "unit-type-count-mismatch";
                return false;
            }

            var seen = new HashSet<UnitType>();
            for (int index = 0; index < ManifestTypes.Length; index++)
            {
                UnitType type = ManifestTypes[index];
                if (!seen.Add(type))
                {
                    reason = "duplicate-unit-type";
                    return false;
                }
                if (!TryResolve(type, out UnitAttackShadowProfile profile) || !profile.IsValid)
                {
                    reason = "missing-or-invalid-profile";
                    return false;
                }
            }

            for (int index = 0; index < enumTypes.Length; index++)
            {
                if (!seen.Contains(enumTypes[index]))
                {
                    reason = "enum-unit-type-not-manifested";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// manifest의 의도된 미지원과, 지원을 약속했지만 현재 UnitData가 그 계약을
        /// 만족하지 못하는 실패를 분리한다. observer 분류일 뿐 Legacy 실행 방식은 바꾸지 않는다.
        /// </summary>
        public static AttackPresentationAuditEligibility ClassifyAuditEligibility(
            UnitAttackShadowProfile profile,
            bool runtimeContractValid)
        {
            if (!profile.IsValid)
                return AttackPresentationAuditEligibility.RequiredButMissing;

            switch (profile.Support)
            {
                case UnitAttackShadowSupport.Supported:
                    return runtimeContractValid
                        ? AttackPresentationAuditEligibility.Required
                        : AttackPresentationAuditEligibility.RequiredButMissing;
                case UnitAttackShadowSupport.Unresolved:
                    return AttackPresentationAuditEligibility.KnownUnresolved;
                case UnitAttackShadowSupport.NotApplicableAttack:
                    return AttackPresentationAuditEligibility.NotApplicable;
                default:
                    return AttackPresentationAuditEligibility.RequiredButMissing;
            }
        }

        /// <summary>
        /// 지원되지 않은 행도 포함해 25종 manifest 전체를 고정 순서로 해시한다. 따라서 한쪽이
        /// 임시로 Projectile을 Hitscan으로 낮추거나 reason을 바꾸면 경기 시작 handshake가 실패한다.
        /// </summary>
        public static bool TryComputePresentationProfileHash(out string hash, out string reason)
        {
            hash = null;
            if (!ValidateManifest(out reason)) return false;

            var manifest = new StringBuilder(2048);
            manifest.Append("c3-schema=")
                .Append(PresentationContractSchemaRevision.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < ManifestTypes.Length; index++)
            {
                UnitType type = ManifestTypes[index];
                if (!TryResolve(type, out UnitAttackShadowProfile profile) || !profile.IsValid)
                {
                    reason = "profile-resolution-failed";
                    return false;
                }
                manifest.Append('|').Append(index.ToString(CultureInfo.InvariantCulture))
                    .Append(':').Append(profile.UnitTypeValue.ToString(CultureInfo.InvariantCulture))
                    .Append(':').Append(profile.UnitTypeName)
                    .Append(':').Append((int)profile.TargetMode)
                    .Append(':').Append((int)profile.Delivery)
                    .Append(':').Append((int)profile.LegacyExecution)
                    .Append(':').Append((int)profile.Support)
                    .Append(':').Append(profile.ExpectedImpactCount.ToString(CultureInfo.InvariantCulture))
                    .Append(':').Append(profile.HasSecondaryResults ? '1' : '0')
                    .Append(':').Append(profile.Reason);
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(manifest.ToString()));
                var text = new StringBuilder(bytes.Length * 2);
                for (int index = 0; index < bytes.Length; index++)
                    text.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                hash = text.ToString();
            }
            reason = "accepted";
            return true;
        }
    }
}
