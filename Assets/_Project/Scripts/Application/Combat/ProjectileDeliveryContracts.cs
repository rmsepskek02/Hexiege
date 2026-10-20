// ============================================================================
// ProjectileDeliveryContracts.cs
// 서버 권위 투사체(옵션 C)의 "약속(계약)" 모음 — 데이터 모양과 분류표.
//
// 이 파일이 담는 것 (유니티 초급자용 설명):
//   1) ProjectileDeliveryKey      : 투사체 1발을 구별하는 번호표(경기 수명 epoch + 발사 번호).
//   2) ProjectileLaunchPresentation: 서버가 "지금 쐈다"고 확정한 기록(발사 기록). 화면은 이것만 보고 투사체를 날린다.
//   3) ProjectileImpactPresentation: 서버가 "지금 닿았다/대상이 사라졌다"고 확정한 기록(착탄 결과).
//   4) ServerProjectileDeliveryPolicy: 어떤 유닛이 "투사체 유닛"인가(Q11 분류)와,
//      그중 어느 유닛을 지금 새 경로로 돌릴지(유닛별 이행 스위치 — RM-9)를 정하는 표.
//   5) ProjectileSettings          : 서버가 비행 시간을 계산할 때 쓰는 속도·대상 반경 값(Inspector 값에서 만들어 주입).
//
// 왜 Application 레이어인가:
//   발사·착탄·피해 판정은 서버 권위 게임 규칙이라 UseCase(Application)가 소유한다.
//   화면(Presentation)과 네트워크(Infrastructure)는 이 계약을 "받아서 보여 주거나 전달"만 한다.
//   Domain은 건드리지 않는다(Domain → Core 참조 금지 규칙과 무관하게, 여기서는 Domain 타입만 읽는다).
//
// 좌표 규칙:
//   모든 위치는 "도메인 월드 좌표(Blue 기준)"다. Red 화면(반전 화면)은 받은 쪽이 ViewConverter로 바꾼다.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 투사체 1발을 경기 수명(epoch) 안에서 유일하게 구별하는 번호표다.
    /// 공격 회차 번호(AttackSequenceId)나 진단 번호와는 별개이며, ID 재사용과 무관하다.
    /// </summary>
    public readonly struct ProjectileDeliveryKey : IEquatable<ProjectileDeliveryKey>
    {
        /// <summary>이 투사체를 만든 서버 UseCase 수명 번호. 0이면 무효.</summary>
        public readonly ulong Epoch;

        /// <summary>그 수명 안에서 1부터 늘어나는 발사 번호. 0이면 무효.</summary>
        public readonly ulong Id;

        /// <summary>두 값이 모두 0이 아니어야 유효한 번호표다.</summary>
        public bool IsValid => Epoch != 0 && Id != 0;

        /// <summary>번호표를 만든다.</summary>
        /// <param name="epoch">서버 UseCase 수명 번호.</param>
        /// <param name="id">발사 번호.</param>
        public ProjectileDeliveryKey(ulong epoch, ulong id) { Epoch = epoch; Id = id; }

        /// <summary>두 번호표가 같은 투사체를 가리키는지 비교한다.</summary>
        public bool Equals(ProjectileDeliveryKey other) => Epoch == other.Epoch && Id == other.Id;

        /// <summary>object 비교 — Equals(ProjectileDeliveryKey)와 같다.</summary>
        public override bool Equals(object obj) => obj is ProjectileDeliveryKey other && Equals(other);

        /// <summary>Dictionary 키로 쓰기 위한 해시값.</summary>
        public override int GetHashCode() => Epoch.GetHashCode() * 397 ^ Id.GetHashCode();

        /// <summary>로그용 문자열(16진 epoch:번호).</summary>
        public override string ToString() => $"{Epoch:x16}:{Id}";
    }

    /// <summary>
    /// 착탄 결과의 종류. 숫자 값은 네트워크로 그대로 보내므로 바꾸지 않는다.
    /// </summary>
    public enum ProjectileImpactOutcome : byte
    {
        /// <summary>잘못된 값(0) — 받은 쪽에서 거부한다.</summary>
        None = 0,

        /// <summary>착탄 시각에 원래 대상이 살아 있어 피해를 줬다. 화면: 착탄 이펙트 + 피해 숫자·피격 반응.</summary>
        Hit = 1,

        /// <summary>비행 중 원래 대상이 사라졌다(다른 원인으로 사망 등). 화면: 마지막 지점에서 착탄 이펙트만, 피해·숫자 없음(Q4).</summary>
        TargetLost = 2
    }

    /// <summary>
    /// 서버가 확정한 "발사 기록". 화면은 이 기록의 시각·위치만으로 투사체를 날린다(속도 값은 보내지 않는다 — 위험 15).
    /// </summary>
    public readonly struct ProjectileLaunchPresentation
    {
        /// <summary>투사체 번호표.</summary>
        public readonly ProjectileDeliveryKey Key;

        /// <summary>쏜 쪽 Id(유닛이면 UnitData.Id, 건물이면 BuildingData.Id).</summary>
        public readonly int AttackerId;

        /// <summary>쏜 쪽이 유닛인가. false면 건물(예: 다음 단계의 인간 방어타워).</summary>
        public readonly bool AttackerIsUnit;

        /// <summary>쏜 쪽 종류 번호. 유닛이면 (int)UnitType, 건물이면 (int)BuildingType. 화면 프리셋 선택에만 쓴다.</summary>
        public readonly int AttackerKind;

        /// <summary>쏜 쪽 종족 번호((int)RaceId). 건물 공격자의 프리셋 선택용. 유닛 공격자는 참고값.</summary>
        public readonly int AttackerRace;

        /// <summary>쏜 쪽 팀.</summary>
        public readonly TeamId Team;

        /// <summary>원래 대상 Id.</summary>
        public readonly int TargetId;

        /// <summary>원래 대상이 유닛인가(false면 건물).</summary>
        public readonly bool TargetIsUnit;

        /// <summary>발사 순간 공격자 위치(도메인 월드 좌표, 바닥 기준).</summary>
        public readonly Vector3 Source;

        /// <summary>발사 순간 대상 중심 위치(도메인 월드 좌표, 바닥 기준).</summary>
        public readonly Vector3 TargetCenter;

        /// <summary>서버가 비행 거리에서 뺀 대상 반경(월드 미터). 화면은 같은 값으로 "표면 지점"을 계산한다(Q12).</summary>
        public readonly float TargetRadius;

        /// <summary>서버 발사 시각(서버 시계).</summary>
        public readonly double LaunchTime;

        /// <summary>서버 착탄 시각(서버 시계) = 발사 시각 + (중심 거리 − 대상 반경) ÷ 속도. 발사 때 고정(U-1).</summary>
        public readonly double ImpactTime;

        /// <summary>대상을 따라가는가(Homing). 2026-10-20 결정으로 모든 투사체가 true다(Mushroom 포함).</summary>
        public readonly bool Homing;

        /// <summary>모든 값이 정상 범위인가. 네트워크로 받은 값도 이 검사를 통과해야만 화면에 쓴다.</summary>
        public bool IsValid => Key.IsValid && AttackerId >= 0 && TargetId >= 0
            && (Team == TeamId.Blue || Team == TeamId.Red)
            && Finite(Source) && Finite(TargetCenter) && Finite(TargetRadius) && TargetRadius >= 0f
            && FiniteTime(LaunchTime) && FiniteTime(ImpactTime) && ImpactTime >= LaunchTime;

        /// <summary>발사 기록을 만든다. 값 검사는 IsValid가 한다.</summary>
        public ProjectileLaunchPresentation(ProjectileDeliveryKey key, int attackerId, bool attackerIsUnit,
            int attackerKind, int attackerRace, TeamId team, int targetId, bool targetIsUnit,
            Vector3 source, Vector3 targetCenter, float targetRadius,
            double launchTime, double impactTime, bool homing)
        {
            Key = key; AttackerId = attackerId; AttackerIsUnit = attackerIsUnit;
            AttackerKind = attackerKind; AttackerRace = attackerRace; Team = team;
            TargetId = targetId; TargetIsUnit = targetIsUnit;
            Source = source; TargetCenter = targetCenter; TargetRadius = targetRadius;
            LaunchTime = launchTime; ImpactTime = impactTime; Homing = homing;
        }

        /// <summary>float 하나가 NaN/무한대가 아닌가.</summary>
        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>벡터 세 성분이 모두 유한한가.</summary>
        public static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

        /// <summary>시각 값이 유한하고 0 이상인가.</summary>
        public static bool FiniteTime(double t) => !double.IsNaN(t) && !double.IsInfinity(t) && t >= 0d;
    }

    /// <summary>
    /// 서버가 착탄 시각에 확정한 결과. 피해는 이미 기존 단일 HP writer가 적용한 뒤이며,
    /// 이 값은 화면 표현(착탄 이펙트 위치·숫자 표시 시점)에만 쓰인다. HP를 다시 쓰지 않는다.
    /// </summary>
    public readonly struct ProjectileImpactPresentation
    {
        /// <summary>어느 투사체의 결과인가.</summary>
        public readonly ProjectileDeliveryKey Key;

        /// <summary>명중(Hit)인지 대상 소실(TargetLost)인지.</summary>
        public readonly ProjectileImpactOutcome Outcome;

        /// <summary>착탄 지점(도메인 월드 좌표) = 대상 중심에서 쏜 쪽 방향으로 대상 반경만큼 당긴 "표면 지점"(Q12).</summary>
        public readonly Vector3 ImpactPoint;

        /// <summary>원래 대상 Id.</summary>
        public readonly int VictimId;

        /// <summary>원래 대상이 유닛인가.</summary>
        public readonly bool VictimIsUnit;

        /// <summary>명중 후 대상 HP(대상 소실이면 -1).</summary>
        public readonly int ResultingHp;

        /// <summary>실제로 적용된 주 대상 피해량(대상 소실이면 0).</summary>
        public readonly int AppliedAmount;

        /// <summary>서버 착탄 시각.</summary>
        public readonly double ImpactTime;

        /// <summary>값이 정상 범위인가. 네트워크 수신 값도 이 검사를 통과해야 쓴다.</summary>
        public bool IsValid => Key.IsValid
            && (Outcome == ProjectileImpactOutcome.Hit || Outcome == ProjectileImpactOutcome.TargetLost)
            && VictimId >= 0 && ProjectileLaunchPresentation.Finite(ImpactPoint)
            && ProjectileLaunchPresentation.FiniteTime(ImpactTime)
            && (Outcome != ProjectileImpactOutcome.Hit || (ResultingHp >= 0 && AppliedAmount >= 0));

        /// <summary>착탄 결과를 만든다.</summary>
        public ProjectileImpactPresentation(ProjectileDeliveryKey key, ProjectileImpactOutcome outcome,
            Vector3 impactPoint, int victimId, bool victimIsUnit, int resultingHp, int appliedAmount, double impactTime)
        {
            Key = key; Outcome = outcome; ImpactPoint = impactPoint; VictimId = victimId;
            VictimIsUnit = victimIsUnit; ResultingHp = resultingHp; AppliedAmount = appliedAmount; ImpactTime = impactTime;
        }
    }

    /// <summary>
    /// 투사체 분류(Q11)와 유닛별 이행 스위치(RM-9)의 단일 소스.
    ///
    /// [두 축을 섞지 않는다]
    ///   · "투사체 유닛 / 즉발 유닛"은 게임플레이 전달 방식 분류다(이 클래스).
    ///   · 기존 Supported/Unresolved는 C3 시퀀서가 그 유닛을 다루는가 하는 다른 축이다(UnitAttackShadowProfileResolver).
    ///   투사체 유닛은 C3 축에서 Unresolved를 그대로 유지하고, 서버 투사체는 별도 경로로 동작한다(Torrent 선례).
    ///
    /// [이행 스위치는 왜 코드 상수인가 — Inspector 값이 아닌 이유]
    ///   Host와 Client가 같은 유닛에 대해 서로 다른 스위치를 가지면 한쪽은 옛 트레이서, 다른 쪽은 새 투사체를 그려
    ///   이중 재생·이중 숫자가 생긴다. 코드 상수는 같은 빌드끼리 반드시 같고, 아래 ManifestToken이 경기 시작
    ///   handshake 해시에 들어가므로(UnitAttackShadowProfileResolver) 다른 빌드끼리는 경기가 시작되지 않는다.
    ///
    /// ⚠️ RM-9: 모든 대상 유닛의 사용자 테스트가 끝나면 이 스위치를 제거하고 항상 새 경로로 동작하게 한다.
    /// </summary>
    public static class ServerProjectileDeliveryPolicy
    {
        // 투사체 유닛 전체(Q11 분류, 2026-10-20 결정). 근접·Hitscan·파도(Torrent)는 즉발/별도 경로라 없다.
        private static readonly UnitType[] ProjectileUnitTypes =
        {
            UnitType.Tank, UnitType.CannonCart, UnitType.InfernoSpirit, UnitType.StreamSpirit,
            UnitType.FoxMagician, UnitType.EagleArcher, UnitType.MushroomBomber
        };

        // ────────────────────────────────────────────────────────────────
        // [RM-9 — 테스트 완료 전까지만 존재하는 유닛별 이행 스위치]
        //   단계 A(2026-10-20): StreamSpirit 1종만 켠다. 나머지 투사체 유닛은 꺼진 상태로 기존 동작
        //   (타격 순간 즉시 피해 + 로컬 트레이서)을 유지한다. 다음 단계에서 한 종씩 이 목록에 추가한다.
        //   단계 B(2026-10-20 사용자 승인): Tank · CannonCart를 추가로 켠다. 두 유닛은 특수 공격 핸들러가 없는
        //   단일 타격 유닛이고, 화면 프리셋(트레이서 EffectPreset_Cannon_Tracer · 착탄 EffectPreset_Cannon_Impact)이
        //   UnitEffectConfig.asset의 type 6·7 행에 이미 연결돼 있다(2026-10-20 에셋 직접 확인). 건물 2배 피해는
        //   착탄 때 같은 단일 writer(ComputeFinalDamage)가 그대로 적용한다.
        //   ⚠️ 이 목록을 바꾸면 ManifestToken이 바뀌어 다른 빌드와는 경기가 시작되지 않는다(의도된 안전장치).
        //   옛 목록(단계 A, 기록용): { UnitType.StreamSpirit }  → ManifestToken "17"
        // ────────────────────────────────────────────────────────────────
        private static readonly UnitType[] EnabledUnitTypes =
        {
            UnitType.Tank, UnitType.CannonCart, UnitType.StreamSpirit
        };

        /// <summary>이 유닛 종류가 "투사체 유닛"(Q11 분류)인가. 스위치와 무관한 분류 자체다.</summary>
        /// <param name="type">유닛 종류.</param>
        public static bool IsProjectileUnit(UnitType type)
            => Array.IndexOf(ProjectileUnitTypes, type) >= 0;

        /// <summary>
        /// 이 유닛의 공격을 지금 "서버 권위 투사체" 경로로 처리하는가(분류 + 이행 스위치).
        /// true면 타격 순간 피해 대신 서버 발사 기록 → 비행 → 착탄 피해로 동작한다.
        /// false면 기존 경로(타격 순간 즉시 피해)를 그대로 탄다.
        /// </summary>
        /// <param name="type">유닛 종류.</param>
        public static bool IsServerProjectile(UnitType type)
            => IsProjectileUnit(type) && Array.IndexOf(EnabledUnitTypes, type) >= 0;

        /// <summary>
        /// 경기 시작 handshake 해시에 넣는 스위치 요약 문자열(켜진 유닛 번호를 오름차순으로 ","로 연결).
        /// 예: 단계 A = "17", 단계 B = "6,7,17". 두 빌드의 스위치가 다르면 해시가 달라 경기가 시작되지 않는다.
        /// </summary>
        public static string ManifestToken
        {
            get
            {
                var values = new List<int>(EnabledUnitTypes.Length);
                foreach (UnitType type in EnabledUnitTypes)
                    if (IsProjectileUnit(type)) values.Add((int)type);
                values.Sort();
                return string.Join(",", values);
            }
        }
    }

    /// <summary>
    /// 서버가 비행 시간을 계산할 때 쓰는 값 묶음(속도·대상 반경). 순수 데이터라 Unity 직렬화와 무관하다.
    /// Inspector 값(ProjectileConfig — Infrastructure)이 조합 루트(GameBootstrapper)에서 이 객체로 바뀌어 주입된다.
    /// 화면(Client)은 이 값을 쓰지 않는다 — 서버가 보낸 시각·반경만 쓴다(위험 15·17).
    /// </summary>
    public sealed class ProjectileSettings
    {
        /// <summary>속도가 설정되지 않았거나 0 이하일 때 쓰는 안전 속도(월드 미터/초).</summary>
        public const float FallbackSpeed = 4f;

        /// <summary>표에 없는 유닛 대상의 기본 반경(월드 미터).</summary>
        public const float FallbackUnitRadius = 0.15f;

        /// <summary>표에 없는 건물 대상의 기본 반경(월드 미터).</summary>
        public const float FallbackBuildingRadius = 0.35f;

        private readonly Dictionary<UnitType, float> _unitSpeeds = new Dictionary<UnitType, float>();
        private readonly Dictionary<UnitType, float> _unitRadii = new Dictionary<UnitType, float>();
        private readonly Dictionary<BuildingType, float> _buildingRadii = new Dictionary<BuildingType, float>();

        /// <summary>표에 없는 유닛 대상 반경. 생성자에서 정한다.</summary>
        public float DefaultUnitRadius { get; }

        /// <summary>표에 없는 건물 대상 반경. 생성자에서 정한다.</summary>
        public float DefaultBuildingRadius { get; }

        /// <summary>인간 방어타워 투사체 속도(다음 단계에서 사용 — 지금은 보관만 한다).</summary>
        public float HumanTowerSpeed { get; }

        /// <summary>
        /// 값 묶음을 만든다. 잘못된 값(음수·NaN)은 기본값으로 바꿔 서버 계산이 멈추지 않게 한다.
        /// </summary>
        /// <param name="defaultUnitRadius">표에 없는 유닛 반경.</param>
        /// <param name="defaultBuildingRadius">표에 없는 건물 반경.</param>
        /// <param name="humanTowerSpeed">인간 방어타워 투사체 속도.</param>
        public ProjectileSettings(float defaultUnitRadius = FallbackUnitRadius,
            float defaultBuildingRadius = FallbackBuildingRadius, float humanTowerSpeed = FallbackSpeed)
        {
            DefaultUnitRadius = ValidRadius(defaultUnitRadius) ? defaultUnitRadius : FallbackUnitRadius;
            DefaultBuildingRadius = ValidRadius(defaultBuildingRadius) ? defaultBuildingRadius : FallbackBuildingRadius;
            HumanTowerSpeed = ValidSpeed(humanTowerSpeed) ? humanTowerSpeed : FallbackSpeed;
        }

        /// <summary>코드 기본값만 가진 묶음(설정 에셋이 없을 때의 폴백).</summary>
        public static ProjectileSettings CreateDefault()
        {
            var settings = new ProjectileSettings();
            // 2026-10-20 사용자 결정 초기값. 실제 운영 값은 ProjectileConfig.asset(Inspector)이 정한다.
            settings.SetUnitSpeed(UnitType.StreamSpirit, 4f);
            settings.SetUnitSpeed(UnitType.Tank, 4f);
            settings.SetUnitSpeed(UnitType.CannonCart, 4f);
            settings.SetUnitSpeed(UnitType.InfernoSpirit, 5f);
            settings.SetUnitSpeed(UnitType.FoxMagician, 5f);
            settings.SetUnitSpeed(UnitType.EagleArcher, 6f);
            settings.SetUnitSpeed(UnitType.MushroomBomber, 5f);
            return settings;
        }

        /// <summary>유닛 종류별 투사체 속도를 정한다(잘못된 값이면 무시).</summary>
        public void SetUnitSpeed(UnitType type, float speed)
        {
            if (ValidSpeed(speed)) _unitSpeeds[type] = speed;
        }

        /// <summary>유닛 종류별 "대상 반경"을 정한다(그 유닛이 맞을 때 비행 거리에서 빼는 값).</summary>
        public void SetUnitRadius(UnitType type, float radius)
        {
            if (ValidRadius(radius)) _unitRadii[type] = radius;
        }

        /// <summary>건물 종류별 "대상 반경"을 정한다.</summary>
        public void SetBuildingRadius(BuildingType type, float radius)
        {
            if (ValidRadius(radius)) _buildingRadii[type] = radius;
        }

        /// <summary>유닛 종류의 투사체 속도. 없거나 잘못되면 FallbackSpeed.</summary>
        public float GetUnitSpeed(UnitType type)
            => _unitSpeeds.TryGetValue(type, out float speed) ? speed : FallbackSpeed;

        /// <summary>대상(유닛/건물)의 반경. 표에 없으면 기본 반경.</summary>
        /// <param name="target">투사체가 노리는 대상.</param>
        public float GetTargetRadius(IDamageable target)
        {
            if (target is UnitData unit)
                return _unitRadii.TryGetValue(unit.Type, out float r) ? r : DefaultUnitRadius;
            if (target is BuildingData building)
                return _buildingRadii.TryGetValue(building.Type, out float r) ? r : DefaultBuildingRadius;
            return 0f;
        }

        private static bool ValidSpeed(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v > 0.0001f;
        private static bool ValidRadius(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f && v < 10f;
    }
}
