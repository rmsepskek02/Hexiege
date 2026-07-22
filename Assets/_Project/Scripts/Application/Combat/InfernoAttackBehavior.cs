// ============================================================================
// InfernoAttackBehavior.cs
// InfernoSpirit(지옥불 정령)의 "때린 대상 1명에게만 거는 지속 피해(DoT)" 특수 공격 핸들러.
//
// 무엇을 하나(초급자용 설명):
//   지옥불 정령은 원거리로 적을 포격한다.
//   - 포격을 맞은 딱 1마리(주 타깃)에게는 "직접 25" 피해가 들어간다.
//     → 이 직접 피해는 이 핸들러가 아니라 ExecuteAttack의 "주 타깃 단일 피해" 경로가 담당한다.
//       (그래서 ReplacesPrimaryAttack = false — 단일 피해를 대체하지 않는다.)
//   - 이 핸들러는 그 "주 타깃 1명"에게 추가로 DoT(3초간 초당 5, 총 15)를 부여한다.
//     따라서 주 타깃이 적 유닛이면 직접 25 + DoT를 함께 받는다.
//
// MushroomBomber(BlastAttackBehavior)와의 차이(더 단순함):
//   - MushroomBomber는 착탄 지점 "원형 반경" 안의 적 유닛 전원에게 DoT를 건다(AoE).
//   - InfernoSpirit은 "때린 그 1명"에게만 건다(반경 수집 없음 — AoE 아님).
//     그래서 반경 순회 코드가 전혀 없고, 주 타깃 1명만 판정한다.
//   - DoT 값도 분리되어 있다: MushroomBomber = 초당 2/3초, InfernoSpirit = 초당 5/3초.
//     그래서 MushroomBomber용 ctx.ApplyDot이 아니라, InfernoSpirit 전용 ctx.ApplyInfernoDot을 호출한다.
//
// 대상 규칙(설계 확정):
//   - 주 타깃이 "적 유닛"이면 그 1명에게 DoT 부여.
//   - 주 타깃이 "건물"이면 아무것도 안 함(건물은 직접 25만, DoT 없음).
//   - 주 타깃이 직접 25에 이미 죽어 제거됐으면 DoT 스킵
//     (IsAlive 확인으로 자연 배제 — BlastAttackBehavior와 동일한 데이터 흐름 방어).
//   - 아군은 애초에 주 타깃이 될 수 없지만, 방어적으로 팀 필터도 둔다(규칙 16 아군 무피해).
//
// Application 레이어 — Domain에만 의존.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 지옥불 정령의 단일 대상 DoT 구현. 주 타깃이 적 유닛이면 그 1명에게만
    /// 지속 피해(DoT)를 부여한다(AoE 아님, 반경 없음). 건물이면 아무것도 하지 않는다.
    /// </summary>
    public sealed class InfernoAttackBehavior : ISpecialAttackBehavior
    {
        /// <summary>
        /// DoT는 "기본 단일 공격(직접 25)에 얹는" 효과이므로 주 타깃 단일 피해를 대체하지 않는다.
        /// (special-only가 아님 — ExecuteAttack이 주 타깃 직접 25를 먼저 적용한 뒤 이 Apply가 실행됨.)
        /// </summary>
        public bool ReplacesPrimaryAttack => false;

        /// <summary>
        /// 주 타깃이 살아있는 적 유닛이면 그 1명에게만 DoT를 부여한다.
        /// 건물이거나(캐스팅 실패), 직접 25로 이미 죽었거나(IsAlive=false), 아군이면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="ctx">공격자·주 타깃·InfernoSpirit DoT 부여 델리게이트를 담은 실행 컨텍스트.</param>
        public void Apply(SpecialAttackContext ctx)
        {
            if (ctx == null) return;

            UnitData attacker = ctx.Attacker;
            if (attacker == null || !attacker.IsAlive) return;

            // 주 타깃을 유닛으로 캐스팅 시도. 건물(BuildingData)이면 UnitData가 아니므로 null → 아무것도 안 함.
            // (IDamageable을 UnitData로 캐스팅되면 유닛, 안 되면 건물/기타.)
            UnitData victim = ctx.PrimaryTarget as UnitData;
            if (victim == null) return;              // 주 타깃이 유닛이 아님(건물 등) → DoT 없음.
            if (!victim.IsAlive) return;             // 직접 25에 이미 사망(제거됨) → DoT 스킵(자연 배제).
            if (victim.Team == attacker.Team) return; // 방어적: 아군이면 제외(규칙 16). 실제로는 적만 주 타깃이 됨.

            // 주 타깃 1명에게만 InfernoSpirit 전용 DoT(초당 5/3초)를 부여.
            // 실제 초당 피해/지속/틱 간격(1초 discrete·올림·총 15 클램프·매초 남은 체력 텍스트·갱신 리셋)은
            // UnitCombatUseCase에 캡슐화돼 있고(서버 권위), 이 핸들러는 "누구에게 걸지"만 결정한다.
            ctx.ApplyInfernoDot(attacker, victim);
        }
    }
}
