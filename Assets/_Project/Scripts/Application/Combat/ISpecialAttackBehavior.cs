// ============================================================================
// ISpecialAttackBehavior.cs
// 특수 공격(범위/파도/도트/힐 등) 1종을 표현하는 전략(Strategy) 계약.
//
// 왜 인터페이스로 분리하는가(방식 C — 전략 핸들러 분리):
//   특수 유닛 5종(BattleAxe/QuakeSpirit/TorrentSpirit/MushroomBomber/BloomFairy)의
//   동작이 근본적으로 달라(휩쓸기/착탄/파도/DoT/힐) 각기 고유 코드가 필요하다.
//   거대한 switch가 전투 핵심 메서드(ExecuteAttack)에 쌓이는 것을 막기 위해
//   각 특수 공격을 독립 클래스로 만들고 UnitType 키 레지스트리로 매핑한다.
//   신규 유닛 = 핸들러 클래스 추가 + 레지스트리 등록 1줄로 끝나며,
//   ExecuteAttack은 다시 수정하지 않는다.
//
// Application 레이어 — Domain에만 의존.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// 유닛 1종의 특수 공격 로직을 캡슐화하는 전략 인터페이스.
    /// 구현체는 <see cref="SpecialAttackContext"/>가 제공하는 공격자/주 타깃/유닛 목록/
    /// 재사용 피해 헬퍼만으로 동작해야 한다(UseCase 내부에 직접 의존하지 않음).
    /// </summary>
    public interface ISpecialAttackBehavior
    {
        /// <summary>
        /// 특수 공격을 실행한다.
        /// 호출 시점: 주 타깃에 대한 기존 단일 피해가 이미 1회 적용된 "직후".
        /// 따라서 구현체는 주 타깃을 중복 타격하지 않도록 주의해야 한다(중복 방지는 각 핸들러 책임).
        /// </summary>
        /// <param name="ctx">공격자·주 타깃·유닛 목록·피해 헬퍼를 담은 실행 컨텍스트.</param>
        void Apply(SpecialAttackContext ctx);
    }
}
