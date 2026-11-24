// ============================================================================
// SkillActivationContext.cs
// 스킬 실행기(ISkillExecutor)가 동작에 필요한 것을 담아 전달하는 컨텍스트.
//
// 담는 것:
//   - 시전 스킬 데이터(SkillData) — 반경/피해/지속 등 수치.
//   - 시전자 정보 — 팀(CasterTeam)·건물 Id(CasterBuildingId).
//   - 조준 좌표(AimWorld/HasAim) — 도메인 월드 연속 좌표. 지점 지정(타입 A·B)에서만 유효.
//   - 재사용 효과 델리게이트 — UnitCombatUseCase의 "건물/스킬 출처 전용 피해/DoT" 경로를 그대로 넘겨받는다.
//     (SpecialAttackContext가 ApplyDamage/ApplyDot을 델리게이트로 받는 것과 같은 역할 분담.)
//   - 2026-11-04 EMP Task 커밋 1: 「범위 안 적 방어탑 무력화」 다리 1개 추가(BuildingStatusEffectSystem 경유).
//   - 2026-11-11 지속 상태 구역 Task: 「구역 만들기」 다리 1개 추가(SkillZoneSystem 경유 — 생성자 맨 뒤 선택 인자).
//   - 2026-11-16 지원 스킬 새 상태 Task 단계 A: 「지원 스킬 효과 걸기」 다리 1개 추가(SupportSkillEffectSystem 경유 —
//     받는 피해 배율 · 생산 속도 배율처럼 서버만 계산하는 새 효과. 생성자 맨 뒤 선택 인자).
//   - 2026-11-17 공습 Task: 조준 방향(AimDirection)과 「직선 연속 폭격 예약」 다리 1개 추가(생성자 맨 뒤 선택 인자 두 개).
//
// 왜 효과 적용을 델리게이트로 주입하는가(SpecialAttackContext와 동일한 이유):
//   실행기가 UnitCombatUseCase의 private 로직(포지션 프로바이더·좌표 매퍼·이벤트 발행)에 직접
//   접근하지 않도록 하여 결합을 낮춘다. 실제 피해 적용·이벤트·사망 처리는 UseCase가 소유한다(서버 권위).
//
// Application 레이어 — Domain 의존. Netcode 직접 참조 없음(UnityEngine.Vector3만 사용 — Unity 의존 허용).
// ============================================================================

using System;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 스킬 실행기 실행에 필요한 데이터·시전자 정보·효과 적용 수단을 담는 컨텍스트.
    /// </summary>
    public sealed class SkillActivationContext
    {
        /// <summary> 시전 중인 스킬의 데이터(반경/피해/지속 등). </summary>
        public SkillData Skill { get; }

        /// <summary> 스킬을 시전한 팀(아군/적 판정 기준). </summary>
        public TeamId CasterTeam { get; }

        /// <summary> 스킬을 시전한 건물의 Id(피해 attribution·연출 attribution). </summary>
        public int CasterBuildingId { get; }

        /// <summary> 조준 좌표(도메인 월드, 연속 Vector3, XZ·Y=0). 지점 지정 스킬에서만 유효(HasAim 확인). </summary>
        public Vector3 AimWorld { get; }

        /// <summary> 조준 좌표가 유효한지 여부. 즉시 발동(타입 C)이면 false. </summary>
        public bool HasAim { get; }

        // 건물/스킬 출처 즉발 범위 피해(타입 A)를 적용하는 델리게이트.
        //   인자: (시전 팀, 중심 도메인 월드 좌표, 반경, 원본 피해, 시전 건물 Id).
        //   UnitCombatUseCase.ApplySkillInstantAreaDamage를 그대로 넘겨받는다.
        private readonly Action<TeamId, Vector3, float, int, int> _instantAreaDamage;

        // 건물/스킬 출처 범위 지속 피해 장판(타입 B)을 적용하는 델리게이트.
        //   인자: (시전 팀, 중심 도메인 월드 좌표, 반경, 초당 피해, 지속시간).
        //   UnitCombatUseCase.ApplySkillAreaDot을 그대로 넘겨받는다.
        private readonly Action<TeamId, Vector3, float, float, float> _areaDot;

        // 전역 상태변경(타입 C)을 적용하는 델리게이트.
        //   인자: (대상 팀, 상태 종류, 강도, 지속시간). 대상 팀은 실행기가 아군/적 판정을 마친 결과다.
        //   UnitCombatUseCase.ApplySkillGlobalStatus를 그대로 넘겨받는다(유닛 순회·상태 부여·회복 위임).
        private readonly Action<TeamId, StatusEffectKind, float, float> _globalStatus;

        // 지점 범위 적 방어탑 무력화(EMP, SkillMechanicType.AreaTowerDisable)를 적용하는 델리게이트.
        //   (2026-11-04 EMP Task 커밋 1 — Plan 3-1 「컨텍스트 다리 1개」.)
        //   인자: (시전 팀, 중심 도메인 월드 좌표, 반경, 무력화 지속시간).
        //   SkillActivationUseCase의 다리가 BuildingStatusEffectSystem.DisableEnemyTowersInRadius를 부른다.
        //   null이면(서비스 미주입) 아무 일도 일어나지 않는다.
        private readonly Action<TeamId, Vector3, float, float> _towerDisable;

        // 지속 상태 구역(SkillMechanicType.PersistentStatusZone — 서리 감옥 · 수액 늪)을 만드는 델리게이트.
        //   (2026-11-11 지속 상태 구역 Task 구현 단위 ①.)
        //   인자: (시전 팀, 중심 도메인 월드 좌표, 반경, 구역 지속시간, 효과 종류, 강도, 아군 대상 여부).
        //   SkillActivationUseCase의 다리가 발동 순번 · 슬롯 · 건물 Id를 붙잡아 SkillZoneSystem.CreateZone을 부른다.
        //   null이면(서비스 미주입 · 옛 호출) 아무 일도 일어나지 않는다.
        private readonly Action<TeamId, Vector3, float, float, StatusEffectKind, float, bool> _persistentZone;

        // 지원 스킬 효과(받는 피해 배율 · 생산 속도 배율 등 — 서버 전용 서비스가 보관하는 새 효과)를 거는 델리게이트.
        //   (2026-11-16 지원 스킬 새 상태 Task 단계 A — 구현 단위 A-1, Plan S-13.)
        //   인자: (대상 팀, 대상 종류(유닛 각각/건물 각각/팀), 효과 종류, 강도, 지속시간). 대상 팀은 실행기가 아군/적 판정을 마친 결과다.
        //   SkillActivationUseCase의 다리가 발동 순번 · 슬롯 · 건물 Id를 붙잡아 SupportSkillEffectSystem.Apply를 부른다.
        //   null이면(서비스 미주입 · 옛 호출) 아무 일도 일어나지 않는다.
        private readonly Action<TeamId, SkillEffectTarget, StatusEffectKind, float, float> _supportEffect;

        /// <summary>
        /// 조준 방향(도메인 월드, XZ · Y = 0, 길이 1). 공습(LineStrike)에서만 의미가 있다 — 시작점(AimWorld) → 손가락 쪽.
        /// SkillActivationUseCase.Activate가 재검증 · 정규화한 값이다(0 · NaN이면 Activate가 미리 거부한다 — Plan P-11).
        /// 다른 스킬은 언제나 0 벡터다(방향을 쓰지 않는다). (2026-11-17 공습 Task L-2)
        /// </summary>
        public Vector3 AimDirection { get; }

        // 직선 연속 폭격(공습, SkillMechanicType.LineStrike) 예약 델리게이트(2026-11-17 공습 Task L-1).
        //   인자: (시전 팀, 시작점 도메인 월드, 정규화 방향, 폭격 계획).
        //   SkillActivationUseCase의 다리가 발동 순번 · 슬롯 · 건물 Id를 붙잡아 착탄 예약(PendingImpact) N개를 만든다.
        //   null이면(옛 호출 · fixture) 아무 일도 일어나지 않는다.
        private readonly Action<TeamId, Vector3, Vector3, LineStrikePlan> _lineStrike;

        /// <summary>
        /// 컨텍스트 생성.
        /// </summary>
        /// <param name="skill">시전 스킬 데이터.</param>
        /// <param name="casterTeam">시전 팀.</param>
        /// <param name="casterBuildingId">시전 건물 Id.</param>
        /// <param name="aimWorld">조준 좌표(도메인 월드 Vector3, 지점 지정 스킬에서만 유효).</param>
        /// <param name="hasAim">조준 좌표 유효 여부.</param>
        /// <param name="instantAreaDamage">타입 A 즉발 범위 피해 적용 델리게이트.</param>
        /// <param name="areaDot">타입 B 범위 지속 피해 적용 델리게이트.</param>
        /// <param name="globalStatus">타입 C 전역 상태변경 적용 델리게이트.</param>
        /// <param name="towerDisable">지점 범위 적 방어탑 무력화(EMP) 적용 델리게이트. null 허용(무효과).</param>
        /// <param name="persistentZone">
        /// 지속 상태 구역 생성 델리게이트(2026-11-11 추가). 선택 인자라 맨 뒤에 두었다 — 기존 호출(인자 9개를 위치로 넘기는
        /// Editor 자가 검증 등)은 고치지 않아도 그대로 컴파일된다. 생략(null)하면 구역 스킬은 아무 일도 하지 않는다.
        /// </param>
        /// <param name="supportEffect">
        /// 지원 스킬 효과 걸기 델리게이트(2026-11-16 추가). 같은 이유로 맨 뒤 선택 인자다 — 생략(null)하면
        /// 받는 피해 배율 · 생산 속도 배율 같은 새 효과는 걸리지 않는다(기존 효과 경로는 그대로).
        /// </param>
        /// <param name="aimDirection">
        /// 조준 방향(2026-11-17 공습 Task 추가 — 맨 뒤 선택 인자). 생략하면 0 벡터 = 방향 없음. 기존 호출은 그대로 컴파일된다.
        /// </param>
        /// <param name="lineStrike">
        /// 직선 연속 폭격 예약 델리게이트(2026-11-17 공습 Task 추가 — 맨 뒤 선택 인자). 생략(null)하면 공습은 아무 일도 하지 않는다.
        /// </param>
        public SkillActivationContext(
            SkillData skill,
            TeamId casterTeam,
            int casterBuildingId,
            Vector3 aimWorld,
            bool hasAim,
            Action<TeamId, Vector3, float, int, int> instantAreaDamage,
            Action<TeamId, Vector3, float, float, float> areaDot,
            Action<TeamId, StatusEffectKind, float, float> globalStatus,
            Action<TeamId, Vector3, float, float> towerDisable,
            Action<TeamId, Vector3, float, float, StatusEffectKind, float, bool> persistentZone = null,
            Action<TeamId, SkillEffectTarget, StatusEffectKind, float, float> supportEffect = null,
            Vector3 aimDirection = default,
            Action<TeamId, Vector3, Vector3, LineStrikePlan> lineStrike = null)
        {
            _persistentZone = persistentZone;
            _supportEffect = supportEffect;
            AimDirection = aimDirection;
            _lineStrike = lineStrike;
            Skill = skill;
            CasterTeam = casterTeam;
            CasterBuildingId = casterBuildingId;
            AimWorld = aimWorld;
            HasAim = hasAim;
            _instantAreaDamage = instantAreaDamage;
            _areaDot = areaDot;
            _globalStatus = globalStatus;
            _towerDisable = towerDisable;
        }

        /// <summary>
        /// 타입 A — 지정한 중심의 원형 반경 안 적(유닛/건물)에게 즉발 1회 피해를 적용한다.
        /// 실제 방어 감쇄·이벤트·사망 처리는 UnitCombatUseCase가 담당한다(서버 권위).
        /// </summary>
        /// <param name="center">중심 도메인 월드 좌표(보통 <see cref="AimWorld"/>).</param>
        /// <param name="radius">원형 반경(월드 단위).</param>
        /// <param name="rawDamage">감쇄 전 원본 피해(×10 스케일 데이터).</param>
        public void ApplyInstantAreaDamage(Vector3 center, float radius, int rawDamage)
        {
            _instantAreaDamage?.Invoke(CasterTeam, center, radius, rawDamage, CasterBuildingId);
        }

        /// <summary>
        /// 타입 B — 지정한 중심의 원형 반경 안 적 유닛에게 지속시간 동안 DoT(장판)를 부여한다.
        /// DoT는 방어력 미적용(무감쇄)이며, 실제 틱은 UnitCombatUseCase.TickTimedEffects가 소비한다.
        /// </summary>
        /// <param name="center">중심 도메인 월드 좌표(보통 <see cref="AimWorld"/>).</param>
        /// <param name="radius">원형 반경(월드 단위).</param>
        /// <param name="damagePerSecond">초당 피해(×10 스케일 데이터).</param>
        /// <param name="duration">지속시간(초).</param>
        public void ApplyAreaDot(Vector3 center, float radius, float damagePerSecond, float duration)
        {
            _areaDot?.Invoke(CasterTeam, center, radius, damagePerSecond, duration);
        }

        /// <summary>
        /// 타입 C — 지정한 팀(아군/적)의 유닛 전체에 상태효과를 부여한다(전역 즉시, 조준 없음).
        /// 실제 유닛 순회·상태 부여·회복(HoT) 위임은 UnitCombatUseCase가 담당한다(서버 권위).
        /// </summary>
        /// <param name="targetTeam">상태를 부여할 대상 팀(실행기가 아군/적 판정을 마친 결과).</param>
        /// <param name="kind">상태 종류(공격 배율/이속 배율/빙결/지속 회복 등).</param>
        /// <param name="magnitude">강도(배율·회복량 등).</param>
        /// <param name="duration">지속시간(초).</param>
        public void ApplyGlobalStatus(TeamId targetTeam, StatusEffectKind kind, float magnitude, float duration)
        {
            _globalStatus?.Invoke(targetTeam, kind, magnitude, duration);
        }

        /// <summary>
        /// 지점 범위 적 방어탑 무력화(EMP) — 지정한 중심의 원형 반경 안에 "지금" 있는 적 방어탑을
        /// 지속시간 동안 공격 불가로 만든다(발동 순간 스냅샷). 유닛에는 영향이 없다.
        /// 실제 탑 선정 · 상태 부여는 BuildingStatusEffectSystem이 담당한다(서버 권위 — 스킬 규칙 25).
        /// </summary>
        /// <param name="center">중심 도메인 월드 좌표(보통 <see cref="AimWorld"/>).</param>
        /// <param name="radius">원형 반경(월드 단위).</param>
        /// <param name="duration">무력화 지속시간(초).</param>
        public void DisableEnemyTowersInArea(Vector3 center, float radius, float duration)
        {
            _towerDisable?.Invoke(CasterTeam, center, radius, duration);
        }

        /// <summary>
        /// 지속 상태 구역 — 지정한 중심에 원형 구역을 지속시간 동안 남긴다. 구역 안 대상 유닛(적 대상이면 상대 팀)에게
        /// 효과(빙결 · 이동 배율)가 걸리고, 나중에 들어온 유닛도 걸리며, 나가거나 구역이 끝나면 풀린다.
        /// 실제 구역 보관 · 구성원 판정 · 만료는 SkillZoneSystem이 담당한다(서버 권위 — 스킬 규칙 25).
        /// </summary>
        /// <param name="center">중심 도메인 월드 좌표(보통 <see cref="AimWorld"/>).</param>
        /// <param name="radius">원형 반경(월드 단위).</param>
        /// <param name="duration">구역 지속시간(초).</param>
        /// <param name="kind">효과 종류(Freeze · MoveSpeedMul).</param>
        /// <param name="magnitude">강도(MoveSpeedMul의 배율).</param>
        /// <param name="targetsAllies">true면 아군, false면 적 유닛이 대상.</param>
        public void CreatePersistentStatusZone(Vector3 center, float radius, float duration, StatusEffectKind kind,
                                               float magnitude, bool targetsAllies)
        {
            _persistentZone?.Invoke(CasterTeam, center, radius, duration, kind, magnitude, targetsAllies);
        }

        /// <summary>
        /// 지원 스킬 효과 — 지정한 팀(아군/적)에 서버 전용 새 효과(받는 피해 배율 · 생산 속도 배율 등)를 지속시간 동안 건다.
        /// 대상 종류가 「유닛」이면 그 팀의 지금 살아 있는 유닛 각각에(발동 순간 — 스킬 규칙 13), 「팀」이면 팀 자체에 건다.
        /// 실제 보관 · 만료 · 판정은 SupportSkillEffectSystem이 담당한다(서버 권위 — 스킬 규칙 25). (2026-11-16 추가)
        /// </summary>
        /// <param name="targetTeam">효과를 받을 팀(실행기가 아군/적 판정을 마친 결과).</param>
        /// <param name="target">대상 종류(유닛 각각 · 건물 각각 · 팀).</param>
        /// <param name="kind">효과 종류(IncomingDamageMul · ProductionSpeedMul).</param>
        /// <param name="magnitude">강도(배율).</param>
        /// <param name="duration">지속시간(초).</param>
        public void ApplySupportEffect(TeamId targetTeam, SkillEffectTarget target, StatusEffectKind kind, float magnitude, float duration)
        {
            _supportEffect?.Invoke(targetTeam, target, kind, magnitude, duration);
        }

        /// <summary>
        /// 공습 — 시작점에서 방향으로 뻗은 직사각형을 시작점 → 끝점 순서로 여러 번 폭격하도록 예약한다(2026-11-17 공습 Task).
        /// 실제 예약 · 서버 권위 틱 처리 · 시전당 1회 피해 · 맵 밖 잘라내기는 SkillActivationUseCase가 담당한다(스킬 규칙 25).
        /// </summary>
        /// <param name="start">시작점 도메인 월드 좌표(보통 <see cref="AimWorld"/>).</param>
        /// <param name="direction">정규화된 방향(보통 <see cref="AimDirection"/>).</param>
        /// <param name="plan">폭격 계획(길이 · 폭 · 횟수 · 지연 · 간격 · 1회 피해).</param>
        public void ScheduleLineStrike(Vector3 start, Vector3 direction, LineStrikePlan plan)
        {
            _lineStrike?.Invoke(CasterTeam, start, direction, plan);
        }
    }
}
