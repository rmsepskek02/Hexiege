// ============================================================================
// PersistentStatusZoneExecutor.cs
// 「지속 상태 구역」(서리 감옥 · 수액 늪) 스킬 실행기. 2026-11-11 지속 상태 구역 Task 구현 단위 ①(사용자 결정 Z-16).
//
// 무엇을 하나(유니티 초급자용 설명):
//   플레이어가 지도 한 지점을 골라 서리 감옥을 쓰면, 그 지점을 중심으로 한 원(반경 = 스킬 데이터의 Radius)이
//   정해진 시간(Duration) 동안 "구역"으로 남는다. 구역 안의 적 유닛은 그동안 얼거나(Freeze) 느려진다(MoveSpeedMul).
//   이 실행기는 스킬 데이터에서 반경 · 시간 · 효과 종류 · 강도 · 대상(아군/적)을 꺼내 컨텍스트의 「구역 만들기」 다리를 부르기만 한다.
//   실제로 구역을 들고 · 구성원을 세고 · 끝내는 일은 SkillZoneSystem이 한다(서버 권위 — 스킬 규칙 25).
//   (EMP 실행기 AreaTowerDisableExecutor가 ctx.DisableEnemyTowersInArea를 부르는 것과 같은 역할 분담이다.)
//
// 지키는 결정:
//   · 같은 실행기 하나로 서리 감옥 · 수액 늪 · (나중에) 연막탄을 다룬다 — 자산의 값(종류 · 강도 · 아군 여부)만 다르다(Z-16 · 스킬 규칙 7).
//   · 새 상태 종류를 만들지 않는다 — 빙결(Freeze)과 이동 배율(MoveSpeedMul)의 뜻을 재사용한다(Z-17).
//     그 밖의 종류(공격 배율 · 회복 · 없음)는 구역 효과로 다루지 않으므로 구역을 만들지 않는다(자산 오설정 방어).
//   · 피해 칸(TotalDamage) · 착탄 지연(ImpactDelay)은 읽지 않는다(지연 장치는 타입 A 다리에만 있다).
//   · 쿨다운은 이 실행기와 무관하게 SkillActivationUseCase가 발동 순간 시작한다(스킬 규칙 3).
//
// Application 레이어 — Domain 의존. UnityEngine은 Mathf만 사용.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 지속 상태 구역(<see cref="SkillMechanicType.PersistentStatusZone"/>) 실행기.
    /// 조준 지점에 원형 구역을 만들어 지속시간 동안 구역 안 대상 유닛에게 빙결 · 이동 배율을 건다.
    /// </summary>
    public sealed class PersistentStatusZoneExecutor : ISkillExecutor
    {
        /// <summary>
        /// 구역을 만든다. 지점 지정 스킬이므로 조준 좌표(HasAim)가 반드시 있어야 한다.
        /// </summary>
        /// <param name="ctx">스킬 데이터 · 시전 팀 · 조준 좌표 · 구역 다리를 담은 컨텍스트.</param>
        public void Execute(SkillActivationContext ctx)
        {
            if (ctx == null || ctx.Skill == null) return;

            // 지점 지정 스킬 — 조준 좌표가 없으면 구역을 둘 자리가 없으므로 무시(자산 조준 방식 오설정 방어).
            if (!ctx.HasAim) return;

            // 구역 시간 · 반경이 0 이하(또는 NaN)면 구역이 성립하지 않는다. !(x > 0)은 NaN도 거른다.
            float duration = ctx.Skill.Duration;
            if (!(duration > 0f)) return;
            float radius = Mathf.Max(0f, ctx.Skill.Radius);
            if (!(radius > 0f)) return;

            // 효과 종류 — 구역이 다루는 두 종류만 받는다(Z-17). 이동 배율은 강도가 0 이상 · 유한해야 한다.
            var kind = (StatusEffectKind)ctx.Skill.StatusKind;
            float magnitude = ctx.Skill.Magnitude;
            if (kind == StatusEffectKind.MoveSpeedMul)
            {
                if (!(magnitude >= 0f) || float.IsInfinity(magnitude)) return;
            }
            else if (kind != StatusEffectKind.Freeze)
            {
                return;
            }

            // 실제 구역 생성(대상 팀 결정 · 구성원 판정 · 효과)은 서버 권위 서비스가 담당.
            ctx.CreatePersistentStatusZone(ctx.AimWorld, radius, duration, kind, magnitude, ctx.Skill.TargetsAllies);
        }
    }
}
