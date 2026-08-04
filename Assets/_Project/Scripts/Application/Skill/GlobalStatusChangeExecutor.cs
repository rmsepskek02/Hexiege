// ============================================================================
// GlobalStatusChangeExecutor.cs
// 타입 C — "전역 상태변경(버프/디버프/제어/회복)" 스킬 실행기(규칙 13).
//
// 무엇을 하나(초급자용 설명):
//   조준 없이(전역 즉시) 발동한다. 스킬 데이터의 대상 지정(TargetsAllies)에 따라 시전 팀의 아군 전체
//   또는 적 전체에게 상태효과(공격 버프/둔화/빙결/지속 회복 등)를 지속시간 동안 부여한다.
//   실제 유닛 순회·상태 부여·회복(HoT) 위임은 UnitCombatUseCase가 담당하고(서버 권위),
//   이 실행기는 스킬 데이터에서 상태 종류·강도·지속·대상을 꺼내 그 경로를 호출하기만 한다
//   (타입 A·B 실행기가 ctx.ApplyInstantAreaDamage/ApplyAreaDot를 부르는 것과 같은 역할 분담).
//
// 조준(규칙 15):
//   타입 C는 전역 즉시라 조준 좌표를 쓰지 않는다(HasAim=false 경로). 좌표를 참조하지 않는다.
//
// Application 레이어 — Domain 의존.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 타입 C(전역 상태변경) 실행기. 시전 팀 기준 아군/적 전체에게 상태효과를 부여한다(조준 없음).
    /// </summary>
    public sealed class GlobalStatusChangeExecutor : ISkillExecutor
    {
        /// <summary>
        /// 전역 상태변경을 실행한다. 조준이 없으므로 HasAim은 무시한다.
        /// </summary>
        /// <param name="ctx">스킬 데이터·시전 팀·상태 부여 델리게이트 컨텍스트.</param>
        public void Execute(SkillActivationContext ctx)
        {
            if (ctx == null || ctx.Skill == null) return;

            SkillData skill = ctx.Skill;

            // 지속시간이 없으면 상태가 성립하지 않으므로 무시(방어적).
            if (skill.Duration <= 0f) return;

            // 데이터의 정수 상태 코드(SkillDefinition._statusKind)를 도메인 열거형으로 변환.
            //   int↔enum 값은 StatusEffectKind에서 1:1로 고정돼 있다(데이터 안정성).
            var kind = (StatusEffectKind)skill.StatusKind;
            if (kind == StatusEffectKind.None) return; // 미설정이면 무효과.

            // 대상 팀 결정: 아군(긍정 효과)이면 시전 팀, 적(부정 효과)이면 반대 팀(규칙 13·16).
            TeamId targetTeam = skill.TargetsAllies
                ? ctx.CasterTeam
                : OppositeTeam(ctx.CasterTeam);

            // 실제 유닛 순회·상태 부여·회복 위임은 UnitCombatUseCase가 담당(서버 권위).
            ctx.ApplyGlobalStatus(targetTeam, kind, skill.Magnitude, skill.Duration);
        }

        /// <summary>
        /// 상대 팀을 반환한다(Blue↔Red). Neutral은 그대로(스킬 시전자는 Blue/Red만이므로 실사용 없음).
        /// </summary>
        /// <param name="team">기준 팀.</param>
        private static TeamId OppositeTeam(TeamId team)
        {
            if (team == TeamId.Blue) return TeamId.Red;
            if (team == TeamId.Red) return TeamId.Blue;
            return team;
        }
    }
}
