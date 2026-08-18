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
        public SkillActivationContext(
            SkillData skill,
            TeamId casterTeam,
            int casterBuildingId,
            Vector3 aimWorld,
            bool hasAim,
            Action<TeamId, Vector3, float, int, int> instantAreaDamage,
            Action<TeamId, Vector3, float, float, float> areaDot,
            Action<TeamId, StatusEffectKind, float, float> globalStatus)
        {
            Skill = skill;
            CasterTeam = casterTeam;
            CasterBuildingId = casterBuildingId;
            AimWorld = aimWorld;
            HasAim = hasAim;
            _instantAreaDamage = instantAreaDamage;
            _areaDot = areaDot;
            _globalStatus = globalStatus;
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
    }
}
