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
        /// </summary>
        public static int GetMaxHp(BuildingType type) => type switch
        {
            BuildingType.Castle     => 50,
            BuildingType.Barracks   => 30,
            BuildingType.MiningPost => 20,
            _                       => 10
        };
    }
}
