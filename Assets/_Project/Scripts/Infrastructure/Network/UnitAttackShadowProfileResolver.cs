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
        // [2026-10-20 서버 권위 투사체] 3 → 4. 새 전송 계약(발사/착탄 ClientRpc, HP 동기화 RPC의 투사체 번호표,
        //   MushroomBomber 행 TargetLocked)이 생겼으므로 구버전 빌드와의 경기를 handshake에서 거부한다(Plan 위험 5).
        //   옛 값(기록용): public const int PresentationContractSchemaRevision = 3;
        // [2026-10-20 단계 B] 4 → 5. 투사체 epoch 알림 RPC(BeginProjectileEpochClientRpc)에 "늦은 접속 스냅샷 발사 기록 수"
        //   인자가 추가돼 RPC 모양이 바뀌었다. 단계 A 빌드(스키마 4)와 섞인 경기는 handshake에서 거부한다.
        //   옛 값(기록용, 단계 A): public const int PresentationContractSchemaRevision = 4;
        // [2026-10-21 단계 D] 5 → 6. 발사 전 취소 통보 RPC(ProjectileLaunchCancelClientRpc)가 새로 생겼다.
        //   단계 B·C 빌드(스키마 5)와 섞인 경기는 handshake에서 거부한다(스위치 토큰도 바뀌어 해시가 다르지만, 전송 모양 변경은 스키마로도 막는다).
        //   옛 값(기록용, 단계 B·C): public const int PresentationContractSchemaRevision = 5;
        public const int PresentationContractSchemaRevision = 6;
        // Native timed target-locked attacks only. Independent waves, charge damage,
        // unresolved projectile policies and the separate heal pipeline stay Legacy.
        public static bool CanUseNativeAttackPhase(UnitType type) =>
            type != UnitType.FoxMagician && type != UnitType.TorrentSpirit
            && type != UnitType.BloomFairy
            && TryResolve(type, out var profile) && profile.CanRunSequencer
            && (profile.LegacyExecution == LegacyAttackExecutionKind.TimerImpact
                || profile.LegacyExecution == LegacyAttackExecutionKind.TimerImpactWithSecondaryEffect);
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
                // [2026-10-27 서버 권위 투사체 단계 E — 결정 E-E] 두 사유 문자열의 근거가 사라졌으므로 지금 사실에 맞게 고친다.
                //   · EagleArcher: "어느 공격 클립인지 모름"은 해소됐다(컨트롤러가 _Attack 하나만 참조 — Research 11-2).
                //     이제 Tank·CannonCart와 같은 "특수 공격 없는 단일 타격 투사체"이므로 같은 사유를 쓴다.
                //   · MushroomBomber: "지점(LockedPoint) 투사체"는 U-16에서 TargetLocked(Homing)로 바뀌었다.
                //     이제 InfernoSpirit과 같은 "투사체 + 지속 피해(독)" 모양이므로 같은 사유를 쓴다.
                //   ⚠️ 사유 문자열은 handshake 해시에 들어간다(아래 ComputeHash의 profile.Reason). 같은 커밋에서 스위치 토큰도 바뀌므로
                //      다른 빌드끼리는 어차피 경기가 거부된다. RPC 모양은 그대로라 스키마(6)는 올리지 않는다(선례: 스키마는 RPC 모양 변경 때만).
                //   C3 축 분류(Unresolved · TargetLocked · 기대 타격 수 1 · secondary 여부)는 바뀌지 않는다.
                //   옛 줄(기록용 — 주석 보존):
                //   case UnitType.EagleArcher: return UnresolvedProjectile(type, false, "projectile-animation-selection-unresolved", out profile);
                //   case UnitType.MushroomBomber: return UnresolvedProjectile(type, true, "locked-point-projectile-and-periodic-result-unresolved", out profile);
                case UnitType.EagleArcher: return UnresolvedProjectile(type, false, "projectile-system-unresolved", out profile);
                case UnitType.MushroomBomber: return UnresolvedProjectile(type, true, "projectile-and-periodic-result-unresolved", out profile);

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
            // [2026-10-20 U-16 — 스키마 4와 같은 변경] 사용자 결정(3차)으로 MushroomBomber도 다른 투사체처럼
            //   대상을 따라가 맞힌다(Homing). 그래서 이 행의 LockedPoint는 실제 게임플레이와 어긋난 값이 됐다 →
            //   모든 투사체 행을 TargetLocked로 통일한다. 25종 manifest 해시가 바뀌므로 스키마 상향과 함께 바꾼다.
            //   옛 식(기록용): type == UnitType.MushroomBomber ? AttackTargetMode.LockedPoint : AttackTargetMode.TargetLocked,
            profile = Create(
                type,
                AttackTargetMode.TargetLocked,
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
            // [2026-10-20] 서버 권위 투사체 이행 스위치(켜진 유닛 목록)도 해시에 넣는다. Host와 Client의 스위치가
            //   다르면(다른 빌드) 한쪽은 옛 트레이서·다른 쪽은 새 투사체를 그려 이중 재생이 생기므로 경기를 시작하지 않는다.
            manifest.Append("|server-projectile=")
                .Append(Hexiege.Application.ServerProjectileDeliveryPolicy.ManifestToken);
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
