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
        /// 이 특수 공격이 "주 타깃에 대한 기본 단일 피해"를 대체하는지 여부.
        ///
        ///   - false(도끼병 휩쓸기): 기존처럼 주 타깃 단일 피해가 먼저 적용되고, 그 "직후" Apply가 실행된다.
        ///     따라서 구현체는 주 타깃을 중복 타격하지 않도록 제외해야 한다.
        ///   - true(TorrentSpirit 파도): 이 유닛은 단일 대상 공격이 아예 없다(special-only).
        ///     UnitCombatUseCase.ExecuteAttack이 주 타깃 단일 피해(ApplyDamageToVictim)를 "건너뛰고"
        ///     Apply만 실행한다. 주 타깃은 파도가 지나며 다른 유닛과 동일하게 처리된다(중복 제외 불필요).
        ///
        /// 일반 유닛은 레지스트리에 없어 이 플래그를 조회하지 않으므로 동작이 완전히 동일하다(무변경).
        /// </summary>
        bool ReplacesPrimaryAttack { get; }

        /// <summary>
        /// 특수 공격을 실행한다.
        /// 호출 시점(ReplacesPrimaryAttack에 따라 다름):
        ///   - false: 주 타깃 단일 피해가 이미 1회 적용된 "직후"(구현체가 주 타깃 중복 타격 제외).
        ///   - true : 주 타깃 단일 피해를 건너뛴 뒤 곧바로(주 타깃도 이 핸들러가 처리).
        /// </summary>
        /// <param name="ctx">공격자·주 타깃·유닛 목록·피해/힐 헬퍼·파도 파라미터를 담은 실행 컨텍스트.</param>
        void Apply(SpecialAttackContext ctx);
    }
}
