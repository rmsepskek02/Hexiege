// ============================================================================
// BuildingStats.cs
// 건물 타입별 기본 스탯 정의.
//
// BuildingType에 따른 MaxHp 등 기본값을 한 곳에서 관리.
// BuildingPlacementUseCase에서 건물 생성 시 참조.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public static class BuildingStats
    {
        /// <summary>
        /// 건물 타입별 기본 최대 체력 반환.
        /// 하위 호환용 — 내부적으로 Human 종족 기준값을 반환.
        /// 종족별 HP가 필요하면 GetMaxHp(BuildingType, RaceId) 오버로드 사용.
        /// </summary>
        public static int GetMaxHp(BuildingType type)
            => GetMaxHp(type, RaceId.Human);

        /// <summary>
        /// 건물 타입 + 종족별 최대 체력 반환.
        /// 초월계(Transcendence)는 다른 종족보다 건물 HP가 높음 (StatsReference.md 기준).
        ///
        /// Castle:     Human/Spirit = 100, Transcendence = 200
        /// Barracks:   Human/Spirit = 30,  Transcendence = 50
        /// MiningPost: Human/Spirit = 20,  Transcendence = 40
        /// </summary>
        public static int GetMaxHp(BuildingType type, RaceId race) => (type, race) switch
        {
            // ── Castle ──
            (BuildingType.Castle,     RaceId.Transcendence) => 200,
            (BuildingType.Castle,     _)                    => 100,
            // ── Barracks ──
            (BuildingType.Barracks,   RaceId.Transcendence) => 50,
            (BuildingType.Barracks,   _)                    => 30,
            // ── MiningPost ──
            (BuildingType.MiningPost, RaceId.Transcendence) => 40,
            (BuildingType.MiningPost, _)                    => 20,
            // ── 기본값 (알 수 없는 건물 타입) ──
            _                                               => 10
        };
    }
}
