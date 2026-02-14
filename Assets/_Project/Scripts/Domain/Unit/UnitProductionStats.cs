// ============================================================================
// UnitProductionStats.cs
// 유닛 타입별 생산 시간과 비용을 관리하는 정적 클래스.
//
// UnitStats 패턴과 동일:
//   타입별 고정값을 switch 표현식으로 반환.
//   MVP에서 유닛 타입 추가 시 case만 추가하면 됨.
//
// GDD 기준:
//   Pistoleer: 5초, 50골드, 1인구
//   (추후) Gunner: 10초, 100골드, 1인구
//   (추후) Sniper: 15초, 150골드, 1인구
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public static class UnitProductionStats
    {
        /// <summary> 유닛 생산에 걸리는 시간(초). </summary>
        public static float GetProductionTime(UnitType type) => type switch
        {
            UnitType.Pistoleer => 5f,
            _ => 5f
        };

        /// <summary> 유닛 생산에 필요한 골드. </summary>
        public static int GetGoldCost(UnitType type) => type switch
        {
            UnitType.Pistoleer => 50,
            _ => 50
        };

        /// <summary> 유닛 생산에 필요한 인구. 모든 유닛 1 인구. </summary>
        public static int GetPopulationCost(UnitType type) => 1;
    }
}
