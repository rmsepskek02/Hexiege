// ============================================================================
// SpecialAttackRegistry.cs
// UnitType → ISpecialAttackBehavior 매핑 테이블.
//
// 역할:
//   특정 유닛 타입이 특수 공격을 가지는지, 가진다면 어떤 핸들러인지 조회한다.
//   등록되지 않은 유닛은 특수 공격이 없으므로 TryGet이 null을 반환하고,
//   ExecuteAttack은 기존 단일 타깃 동작만 수행한다(일반 유닛 무변경).
//
// 이번 범위:
//   BattleAxe → SweepAttackBehavior 만 등록.
//   나머지 특수 유닛 4종(QuakeSpirit/TorrentSpirit/MushroomBomber/BloomFairy)은
//   각자 ISpecialAttackBehavior 구현체를 만든 뒤 여기 한 줄씩 등록하면 된다(뼈대만).
//
// 왜 인스펙터 배선이 아닌 코드 등록인가:
//   UnitType 키 매핑이라 ScriptableObject/인스펙터 배선이 필요 없다.
//   신규 유닛 = 핸들러 추가 + 여기 등록 1줄. 데이터 배선 비용을 회피한다.
//
// Application 레이어 — Domain에만 의존. 외부 의존이 없는 순수 전략 테이블이므로
// UnitCombatUseCase가 내부에서 직접 생성한다(정적 스탯 테이블과 동일한 성격).
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 유닛 타입별 특수 공격 핸들러 조회 테이블.
    /// </summary>
    public sealed class SpecialAttackRegistry
    {
        // UnitType → 핸들러. 핸들러는 상태를 재사용 버퍼 정도만 가지므로 타입당 1개 인스턴스 공유.
        private readonly Dictionary<UnitType, ISpecialAttackBehavior> _behaviors
            = new Dictionary<UnitType, ISpecialAttackBehavior>();

        /// <summary>
        /// 기본 특수 공격 핸들러들을 등록한다.
        /// </summary>
        public SpecialAttackRegistry()
        {
            // 도끼병: 휩쓸기형 AoE.
            _behaviors[UnitType.BattleAxe] = new SweepAttackBehavior();

            // TODO(특수 유닛 확장): 아래처럼 핸들러를 추가 등록한다.
            //   _behaviors[UnitType.QuakeSpirit]    = new QuakeAttackBehavior();
            //   _behaviors[UnitType.TorrentSpirit]  = new TorrentAttackBehavior();
            //   _behaviors[UnitType.MushroomBomber] = new BlastAttackBehavior();
            //   _behaviors[UnitType.BloomFairy]     = new HealBehavior();
        }

        /// <summary>
        /// 해당 유닛 타입의 특수 공격 핸들러를 반환. 없으면 null.
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>등록된 핸들러 또는 null(특수 공격 없음).</returns>
        public ISpecialAttackBehavior TryGet(UnitType type)
        {
            _behaviors.TryGetValue(type, out var behavior);
            return behavior;
        }
    }
}
