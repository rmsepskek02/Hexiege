// ============================================================================
// ProjectileDeliveryContracts.cs
// 서버 권위 투사체(옵션 C)의 "약속(계약)" 모음 — 데이터 모양과 분류표.
//
// 이 파일이 담는 것 (유니티 초급자용 설명):
//   1) ProjectileDeliveryKey      : 투사체 1발을 구별하는 번호표(경기 수명 epoch + 발사 번호).
//   2) ProjectileLaunchPresentation: 서버가 "지금 쐈다"고 확정한 기록(발사 기록). 화면은 이것만 보고 투사체를 날린다.
//   3) ProjectileImpactPresentation: 서버가 "지금 닿았다/대상이 사라졌다"고 확정한 기록(착탄 결과).
//   4) ServerProjectileDeliveryPolicy: 어떤 유닛이 "투사체 유닛"인가(Q11 분류)를 정하는 표.
//      분류에 든 유닛은 항상 서버 투사체 경로로 공격한다(이행 스위치는 2026-10-27 P5 정리에서 없앴다).
//      [2026-10-21 단계 C] 같은 클래스에 "투사체 타워"(인간 CannonTower) 분류도 있다.
//   5) ProjectileSettings          : 서버가 비행 시간을 계산할 때 쓰는 속도·대상 반경 값(Inspector 값에서 만들어 주입).
//      [단계 C] 인간 타워 속도·포구 위치(발사 원점)도 여기 있다.
//   6) IBuildingProjectileImpactWriter: [단계 C] 타워 투사체의 착탄 피해를 타워 writer(TowerCombatUseCase)에 맡기는 창구.
//   7) ProjectileLaunchCancelPresentation: [2026-10-21 단계 D] 서버가 "발사 전에 예약을 취소했다"는 통보
//      (차지 유닛 화면이 차지 VFX를 즉시 끄는 서버 권위 근거 — 사유 ProjectileLaunchCancelCause).
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

        /// <summary>쏜 쪽이 유닛인가. false면 건물(2026-10-21 단계 C부터 인간 방어타워 CannonTower).</summary>
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

        /// <summary>
        /// 발사 순간 공격자 위치(도메인 월드 좌표).
        /// · 유닛 공격자: 유닛 위치(바닥 기준). 화면은 총구(UnitView.ProjectileLaunchPoint)를 따로 찾는다.
        /// · 건물 공격자(단계 C 인간 타워): 서버가 계산한 "포구 위치" 그 자체(높이 포함). 화면은 이 점에서 투사체·발사 이펙트를 낸다.
        /// 비행 거리는 XZ 평면으로만 재므로 높이는 착탄 시각에 영향이 없다.
        /// </summary>
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
    /// [2026-10-21 단계 D] 서버가 "발사 전에" 투사체 예약을 취소한 사유. 숫자 값은 네트워크로 그대로 보내므로 바꾸지 않는다.
    /// (진단 END 줄의 cancel= 사유 토큰과 같은 분류다 — UnitCombatUseCase.ProjectileCancelCauses.)
    /// </summary>
    public enum ProjectileLaunchCancelCause : byte
    {
        /// <summary>잘못된 값(0) — 받은 쪽에서 거부한다.</summary>
        None = 0,
        /// <summary>발사 전 공격자 사망(attacker-dead).</summary>
        AttackerDead = 1,
        /// <summary>공격자 Id에 다른 객체가 들어옴(attacker-replaced).</summary>
        AttackerReplaced = 2,
        /// <summary>발사 전(또는 발사 순간) 대상 사망(target-dead).</summary>
        TargetDead = 3,
        /// <summary>살아 있는 대상이 목록에서 사라짐(target-missing).</summary>
        TargetMissing = 4,
        /// <summary>대상 Id에 다른 객체가 들어옴(target-replaced).</summary>
        TargetReplaced = 5,
        /// <summary>이동·정지 같은 명시적 명령(move-command).</summary>
        MoveCommand = 6,
        /// <summary>발사 좌표가 비정상(invalid-launch-geometry).</summary>
        InvalidGeometry = 7,
        /// <summary>그 밖의 사유(other).</summary>
        Other = 8
    }

    /// <summary>
    /// [2026-10-21 단계 D] 서버가 확정한 "발사 취소 통보". 화면은 이 통보로 차지(기 모으기) VFX를 즉시 끈다.
    ///
    /// [왜 필요한가 — 유니티 초급자용]
    ///   차지 유닛(InfernoSpirit · FoxMagician)은 공격 시작 직후 차지 VFX를 켜고, 발사 순간(OnAttackHit)까지 모은다.
    ///   그 사이 대상이 죽으면 서버는 발사를 취소한다(투사체·소리·피해 없음). 그런데 다른 적이 사거리 안에 남아 있으면
    ///   서버는 "전투 정지" 통보를 보내지 않으므로(쿨다운 중에는 타겟도 바꾸지 않는다), 화면은 취소를 알 길이 없어
    ///   차지 VFX가 끝까지 모인 뒤 아무것도 쏘지 않는 어색한 장면이 된다.
    ///   그래서 "서버가 취소했다"는 사실 자체를 화면에 알린다 — 판정이 아니라 서버 권위 사실의 전달이다.
    ///
    /// PresentationRevision: 멀티 서버가 취소 순간에 그 공격자에게 마지막으로 보낸 공격 표현 회차 번호.
    ///   Client 화면은 자기 회차와 같을 때만 끈다(더 새 회차의 차지를 실수로 끄지 않게). 0 = 같은 프로세스(싱글·Host) 통보 —
    ///   그 UseCase가 지금 들고 있는 회차가 곧 취소된 회차이므로 비교 없이 적용한다.
    /// </summary>
    public readonly struct ProjectileLaunchCancelPresentation
    {
        /// <summary>취소된 예약의 번호표(epoch + 예약 번호 — 발사 기록의 번호표와 같은 번호 공간).</summary>
        public readonly ProjectileDeliveryKey Key;

        /// <summary>쏘려던 유닛 Id.</summary>
        public readonly int AttackerId;

        /// <summary>쏘려던 유닛 종류((int)UnitType). 진단·필터용.</summary>
        public readonly int AttackerKind;

        /// <summary>취소 사유.</summary>
        public readonly ProjectileLaunchCancelCause Cause;

        /// <summary>서버 공격 표현 회차(0 = 같은 프로세스 통보 — 비교 없이 적용).</summary>
        public readonly ulong PresentationRevision;

        /// <summary>서버가 취소를 확정한 시각(서버 시계).</summary>
        public readonly double CancelTime;

        /// <summary>값이 정상 범위인가. 네트워크 수신 값도 이 검사를 통과해야 쓴다.</summary>
        public bool IsValid => Key.IsValid && AttackerId >= 0 && Cause != ProjectileLaunchCancelCause.None
            && (byte)Cause <= (byte)ProjectileLaunchCancelCause.Other
            && ProjectileLaunchPresentation.FiniteTime(CancelTime);

        /// <summary>취소 통보를 만든다. 값 검사는 IsValid가 한다.</summary>
        public ProjectileLaunchCancelPresentation(ProjectileDeliveryKey key, int attackerId, int attackerKind,
            ProjectileLaunchCancelCause cause, ulong presentationRevision, double cancelTime)
        {
            Key = key; AttackerId = attackerId; AttackerKind = attackerKind; Cause = cause;
            PresentationRevision = presentationRevision; CancelTime = cancelTime;
        }

        /// <summary>같은 통보에 회차 번호만 붙인 사본(멀티 서버 전송 계층이 채운다).</summary>
        /// <param name="revision">서버 공격 표현 회차.</param>
        public ProjectileLaunchCancelPresentation WithRevision(ulong revision)
            => new ProjectileLaunchCancelPresentation(Key, AttackerId, AttackerKind, Cause, revision, CancelTime);
    }

    /// <summary>
    /// "투사체 유닛 / 투사체 타워" 분류(Q10 · Q11)의 단일 소스.
    /// 분류에 든 유닛·타워의 공격은 항상 "서버 권위 투사체" 경로(서버 발사 기록 → 비행 → 착탄 때 피해)로 처리된다.
    ///
    /// [두 축을 섞지 않는다]
    ///   · "투사체 유닛 / 즉발 유닛"은 게임플레이 전달 방식 분류다(이 클래스).
    ///   · 기존 Supported/Unresolved는 C3 시퀀서가 그 유닛을 다루는가 하는 다른 축이다(UnitAttackShadowProfileResolver).
    ///   투사체 유닛은 C3 축에서 Unresolved를 그대로 유지하고, 서버 투사체는 별도 경로로 동작한다(Torrent 선례).
    ///
    /// [분류는 왜 코드 상수인가 — Inspector 값이 아닌 이유]
    ///   Host와 Client가 같은 유닛을 서로 다르게 분류하면 한쪽은 로컬 트레이서, 다른 쪽은 서버 투사체를 그려
    ///   이중 재생·이중 숫자가 생긴다. 코드 상수는 같은 빌드끼리 반드시 같고, 아래 ManifestToken이 경기 시작
    ///   handshake 해시에 들어가므로(UnitAttackShadowProfileResolver) 분류가 다른 빌드끼리는 경기가 시작되지 않는다.
    ///
    /// [2026-10-27 P5 정리 — 이행 스위치(RM-9) 제거]
    ///   투사체 대상을 한 종씩 옮기는 동안(단계 A~E) "분류"와 별도로 "이행 스위치" 목록을 두고 그 목록에 든 유닛만 새 경로로
    ///   보냈다. 단계 E에서 분류 7종 + 인간 타워가 모두 스위치에 들어가 두 목록이 같아졌고 사용자 테스트도 끝나서
    ///   스위치 목록을 지우고 "분류에 들었는가" 하나만 묻게 했다(사용자 결정 P5-B (가)).
    /// </summary>
    public static class ServerProjectileDeliveryPolicy
    {
        // 투사체 유닛 전체(Q11 분류, 2026-10-20 결정). 근접·Hitscan·파도(Torrent)는 즉발/별도 경로라 없다.
        //   ⚠️ 이 목록을 바꾸면 ManifestToken이 바뀌어 다른 빌드와는 경기가 시작되지 않는다(의도된 안전장치).
        private static readonly UnitType[] ProjectileUnitTypes =
        {
            UnitType.Tank, UnitType.CannonCart, UnitType.InfernoSpirit, UnitType.StreamSpirit,
            UnitType.FoxMagician, UnitType.EagleArcher, UnitType.MushroomBomber
        };

        // ────────────────────────────────────────────────────────────────
        // [2026-10-21 단계 C — 인간 방어타워(CannonTower) 전환]
        //   "투사체 타워" 분류(Q10 결정): 방어 타워(AutoTower) 중 Human 종족(CannonTower)만 투사체 타워다.
        //   Spirit RuneSpire(번개)는 투사체 개념이 아니라 즉발 피해를 유지하고(Q10 ②),
        //   Transcendence VineTower는 결정이 없어 바꾸지 않는다(Buildings 규칙 12 「인간 타워만」).
        //   ⚠️ 이 목록도 ManifestToken에 들어가 경기 시작 handshake 해시에 반영된다(분류가 다른 빌드끼리는 경기가 시작되지 않는다).
        // ────────────────────────────────────────────────────────────────
        private static readonly RaceId[] ProjectileTowerRaces = { RaceId.Human };

        /// <summary>
        /// 이 건물(종류 + 종족)이 "투사체 타워"(Q10 분류)인가.
        /// true면 즉발 피해 대신 서버 발사 기록 → 비행 → 착탄 피해로 동작한다.
        /// false면 기존 경로(공격 순간 즉발 피해 + 발사 스냅샷)를 그대로 탄다 — RuneSpire·VineTower는 항상 false.
        /// 지금은 방어 타워(AutoTower) + Human(CannonTower)만 true다.
        /// </summary>
        /// <param name="type">건물 종류.</param>
        /// <param name="race">건물 소유 팀의 종족.</param>
        public static bool IsProjectileTower(BuildingType type, RaceId race)
            => type == BuildingType.AutoTower && Array.IndexOf(ProjectileTowerRaces, race) >= 0;

        /// <summary>
        /// 이 유닛 종류가 "투사체 유닛"(Q11 분류)인가.
        /// true면 타격 순간 피해 대신 서버 발사 기록 → 비행 → 착탄 피해로 동작한다.
        /// false(근접·총잡이 등 비투사체 유닛)면 기존 경로(타격 순간 즉시 피해)를 그대로 탄다.
        /// </summary>
        /// <param name="type">유닛 종류.</param>
        public static bool IsProjectileUnit(UnitType type)
            => Array.IndexOf(ProjectileUnitTypes, type) >= 0;

        /// <summary>
        /// 경기 시작 handshake 해시에 넣는 "투사체 분류" 요약 문자열.
        /// 앞부분 = 투사체 유닛 번호를 오름차순으로 ","로 연결, 뒤에 투사체 타워 종족(소문자)을 ";tower:" 뒤에 붙인다.
        /// 지금 값 = "6,7,12,17,21,24,26;tower:human".
        /// [2026-10-27 P5 정리 — 사용자 결정 P5-A (가)] 예전에는 이행 스위치 목록에서 만들었다. 스위치를 지우면서
        ///   분류 목록에서 만들게 바꿨지만, 단계 E 이후 두 목록의 원소가 같았으므로 문자열은 한 글자도 바뀌지 않는다
        ///   (P5 이전 빌드와 handshake 해시가 같다 — 동작이 같은 빌드끼리 경기를 막지 않기 위해서다. 스키마 6 유지).
        /// 두 빌드의 분류가 다르면 해시가 달라 경기가 시작되지 않는다.
        /// 공백이 없어야 한다 — handshake 원문은 "|" · "=" 로 칸을 나눈다.
        /// </summary>
        public static string ManifestToken
        {
            get
            {
                var values = new List<int>(ProjectileUnitTypes.Length);
                foreach (UnitType type in ProjectileUnitTypes) values.Add((int)type);
                values.Sort();
                string units = string.Join(",", values);
                var towers = new List<string>(ProjectileTowerRaces.Length);
                foreach (RaceId race in ProjectileTowerRaces) towers.Add(race.ToString().ToLowerInvariant());
                towers.Sort(StringComparer.Ordinal);
                return towers.Count == 0 ? units : units + ";tower:" + string.Join(",", towers);
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

        /// <summary>
        /// 인간 방어타워(CannonTower) 대포 입구(포구)의 기본 위치 — 타워 루트 기준 "타워가 바라보는 방향" 좌표계(월드 미터).
        ///   x = 타워 오른쪽, y = 위, z = 대포가 향한 앞쪽.
        /// [근거 — 2026-10-21 측정] 프리팹 Building_CannonTower_Blue/Red.prefab에는 포구 Transform이 없다(메시 1개뿐).
        ///   그래서 메시(CannonTower.fbx, 정점 1,483개)를 직접 읽어 프리팹 배치(자식 위치 y 0.28 · X축 −90° · 크기 35,
        ///   FBX 단위 cm → 0.01)를 적용해 계산했다. 본체와 떨어진 작은 덩어리(정점 45개)가 포신이며,
        ///   그 축(주성분)은 거의 +Z(위로 약 4.8°), 포신 끝 중심 = (−0.001, 0.405, 0.170), 포신 반지름 ≈ 0.026이다.
        ///   반올림해 (0, 0.405, 0.17)을 쓴다. 실제 운영 값은 ProjectileConfig.asset(Inspector)이 정한다.
        /// </summary>
        public static readonly Vector3 DefaultHumanTowerMuzzleOffset = new Vector3(0f, 0.405f, 0.17f);

        /// <summary>인간 방어타워(CannonTower) 투사체 속도(월드 미터/초). 2026-10-21 단계 C부터 실제로 쓴다.</summary>
        public float HumanTowerSpeed { get; }

        /// <summary>인간 방어타워 포구 위치(타워 루트 기준 타워 정면 좌표계). 서버 발사 위치와 화면 발사 이펙트 위치가 같은 이 값을 쓴다.</summary>
        public Vector3 HumanTowerMuzzleOffset { get; }

        /// <summary>
        /// 값 묶음을 만든다. 잘못된 값(음수·NaN)은 기본값으로 바꿔 서버 계산이 멈추지 않게 한다.
        /// </summary>
        /// <param name="defaultUnitRadius">표에 없는 유닛 반경.</param>
        /// <param name="defaultBuildingRadius">표에 없는 건물 반경.</param>
        /// <param name="humanTowerSpeed">인간 방어타워 투사체 속도.</param>
        /// <param name="humanTowerMuzzleOffset">인간 방어타워 포구 위치. null이면 측정 기본값(DefaultHumanTowerMuzzleOffset).</param>
        public ProjectileSettings(float defaultUnitRadius = FallbackUnitRadius,
            float defaultBuildingRadius = FallbackBuildingRadius, float humanTowerSpeed = FallbackSpeed,
            Vector3? humanTowerMuzzleOffset = null)
        {
            DefaultUnitRadius = ValidRadius(defaultUnitRadius) ? defaultUnitRadius : FallbackUnitRadius;
            DefaultBuildingRadius = ValidRadius(defaultBuildingRadius) ? defaultBuildingRadius : FallbackBuildingRadius;
            HumanTowerSpeed = ValidSpeed(humanTowerSpeed) ? humanTowerSpeed : FallbackSpeed;
            // 포구 위치가 비정상(NaN·무한대·타워에서 2m 넘게 떨어짐)이면 측정 기본값을 쓴다 — 발사 위치가 맵 밖으로 튀지 않게.
            Vector3 muzzle = humanTowerMuzzleOffset ?? DefaultHumanTowerMuzzleOffset;
            HumanTowerMuzzleOffset = ProjectileLaunchPresentation.Finite(muzzle) && muzzle.magnitude <= 2f
                ? muzzle : DefaultHumanTowerMuzzleOffset;
        }

        /// <summary>
        /// 투사체 타워의 서버 속도. 지금은 인간 타워만 투사체 타워라 HumanTowerSpeed를 돌려준다.
        /// 다른 타워가 들어오면 이 메서드에 분기를 더한다(분류는 ServerProjectileDeliveryPolicy.IsProjectileTower).
        /// </summary>
        /// <param name="type">건물 종류.</param>
        /// <param name="race">건물 종족.</param>
        public float GetTowerSpeed(BuildingType type, RaceId race) => HumanTowerSpeed;

        /// <summary>
        /// 투사체 타워의 "포구 위치"를 도메인 월드 좌표(Blue 기준)로 계산한다. 서버 발사 위치 = 이 값.
        ///
        /// [팀별 방향 규칙 — 유니티 초급자용]
        ///   BuildingFactory.GetInitialRotation은 인간 타워를 "내 타워는 회전 없음, 상대 타워는 Y축 180°"로 놓는다.
        ///   그래서 어느 화면에서 보든 도메인 좌표로는 Blue 타워 포신이 +Z, Red 타워 포신이 −Z를 향한다
        ///   (Red 화면은 맵을 반전해 보여 주므로 Red 타워가 화면 위쪽(+Z)을 향해 보인다).
        ///   따라서 Red 타워는 정면 좌표계 오프셋의 x·z 부호를 뒤집으면(= 180° 회전) 도메인 포구 위치가 된다.
        /// </summary>
        /// <param name="towerPosition">타워 루트 위치(도메인 월드 좌표 — 건물 Y 오프셋 포함).</param>
        /// <param name="team">타워 소유 팀.</param>
        public Vector3 ResolveTowerMuzzle(Vector3 towerPosition, TeamId team)
        {
            Vector3 offset = HumanTowerMuzzleOffset;
            float sign = team == TeamId.Red ? -1f : 1f;
            return new Vector3(towerPosition.x + offset.x * sign, towerPosition.y + offset.y, towerPosition.z + offset.z * sign);
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

    /// <summary>
    /// [2026-10-21 단계 C] 건물 공격자(투사체 타워)의 "착탄 피해 writer" 창구.
    ///
    /// [왜 필요한가 — 유니티 초급자용]
    ///   투사체 1발의 수명(예약·비행·착탄·닫기)은 UnitCombatUseCase가 한곳에서 관리한다(네트워크 전송·화면 소비자가
    ///   모두 그 이벤트를 구독하므로). 하지만 타워 피해의 "단일 HP writer"는 TowerCombatUseCase다
    ///   (방어 감쇄 · 피격/사망 이벤트 · 유닛 제거). 그래서 착탄 순간에는 투사체 수명 관리자가 이 창구로
    ///   타워 writer를 한 번 불러 피해를 준다. 타워 피해 계산을 두 곳에 복사하지 않기 위한 다리다.
    /// </summary>
    public interface IBuildingProjectileImpactWriter
    {
        /// <summary>
        /// 투사체 타워의 착탄 피해 1회를 적용한다(서버/싱글 전용). 타워가 이미 파괴됐어도 진행한다(Q3 · 1-1-2 U-6).
        /// </summary>
        /// <param name="tower">쏜 타워(발사 때 붙잡은 객체 — 이미 파괴됐을 수 있다).</param>
        /// <param name="target">원래 대상 유닛(착탄 순간 살아 있음을 호출자가 확인했다).</param>
        /// <param name="attackPowerAtLaunch">발사 때 저장한 원래 공격력(방어 감쇄 전).</param>
        /// <param name="key">이 투사체 번호표 — 피격 이벤트에 실어 화면이 착탄 순간에 숫자를 1회 내게 한다(Q7).</param>
        /// <returns>적용 결과(Applied면 적용량·남은 HP).</returns>
        AttackDamageObservation ApplyTowerProjectileImpact(BuildingData tower, UnitData target,
            int attackPowerAtLaunch, ProjectileDeliveryKey key);
    }
}
