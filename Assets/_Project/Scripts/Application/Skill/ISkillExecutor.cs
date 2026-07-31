// ============================================================================
// ISkillExecutor.cs
// 스킬 메커니즘 타입(A/B/C) 1종을 실행하는 전략(Strategy) 계약.
//
// 왜 인터페이스로 분리하는가(특수 공격 ISpecialAttackBehavior 패턴 원용):
//   스킬 타입 3종은 효과가 근본적으로 다르다(즉발 범위 피해 / 장판 DoT / 전역 상태변경).
//   거대한 switch가 SkillActivationUseCase에 쌓이는 것을 막기 위해, 각 타입을 독립 클래스로
//   만들고 SkillMechanicType 키 레지스트리(SkillExecutorRegistry)로 매핑한다.
//   신규 타입/변형 = 실행기 클래스 추가 + 레지스트리 등록 1줄이며, 유스케이스는 다시 수정하지 않는다.
//
// Application 레이어 — Domain에만 의존.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// 스킬 메커니즘 1종의 실행 로직을 캡슐화하는 전략 인터페이스.
    /// 구현체는 <see cref="SkillActivationContext"/>가 제공하는 스킬 데이터·시전자 정보·
    /// 효과 적용 델리게이트만으로 동작해야 한다(UseCase 내부에 직접 의존하지 않음).
    /// </summary>
    public interface ISkillExecutor
    {
        /// <summary>
        /// 스킬을 실행한다. 재검증(건물 생존·쿨다운·좌표 유효)은 이미 통과한 상태로 호출된다.
        /// </summary>
        /// <param name="ctx">스킬 데이터·시전자·조준 좌표·효과 적용 델리게이트를 담은 실행 컨텍스트.</param>
        void Execute(SkillActivationContext ctx);
    }
}
