// ============================================================================
// UnitStats.cs
// 유닛 타입별 기본 스탯 정의.
//
// UnitType에 따른 MaxHp, AttackPower, AttackRange 기본값을 한 곳에서 관리.
// UnitSpawnUseCase에서 유닛 생성 시 참조.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public static class UnitStats
    {
        /// <summary> 유닛 타입별 기본 최대 체력. </summary>
        public static int GetMaxHp(UnitType type) => type switch
        {
            UnitType.Pistoleer => 50,
            _                  => 10
        };

        /// <summary> 유닛 타입별 기본 공격력. </summary>
        public static int GetAttackPower(UnitType type) => type switch
        {
            UnitType.Pistoleer => 3,
            _                  => 1
        };

        /// <summary> 유닛 타입별 기본 공격 사거리 (타일 단위). </summary>
        public static int GetAttackRange(UnitType type) => type switch
        {
            UnitType.Pistoleer => 1,
            _                  => 1
        };

        /// <summary> 유닛 타입별 타일 1칸 이동 소요 시간(초). 작을수록 빠름. </summary>
        public static float GetMoveSeconds(UnitType type) => type switch
        {
            UnitType.Pistoleer => 0.8f,
            _                  => 0.3f
        };
    }
}
