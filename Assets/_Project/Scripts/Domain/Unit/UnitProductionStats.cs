// ============================================================================
// UnitProductionStats.cs
// 유닛 타입별 생산 시간과 비용을 관리하는 정적 클래스.
//
// UnitStats 패턴과 동일:
//   타입별 고정값을 switch 표현식으로 반환.
//   MVP에서 유닛 타입 추가 시 case만 추가하면 됨.
//
// StatsReference.md 기준:
//   인간계 — Pistoleer: 5초/50G, Assault: 10초/100G, Sniper: 15초/200G
//   정령계 — EmberSpirit: 5초/50G, FlameSpirit: 15초/200G, InfernoSpirit: 30초/500G
//   초월계 — FoxMagician: 5초/50G, BearGuard: 25초/400G, LionKnight: 15초/200G
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
            // ── 인간계 ──
            UnitType.Pistoleer    => 5f,
            UnitType.Assault      => 10f,
            UnitType.Sniper       => 15f,
            // ── 정령계 ──
            UnitType.EmberSpirit  => 5f,
            UnitType.FlameSpirit  => 15f,
            UnitType.InfernoSpirit => 30f,
            // ── 초월계 ──
            UnitType.FoxMagician  => 5f,
            UnitType.BearGuard    => 25f,
            UnitType.LionKnight   => 15f,
            _ => 5f
        };

        /// <summary> 유닛 생산에 필요한 골드. </summary>
        public static int GetGoldCost(UnitType type) => type switch
        {
            // ── 인간계 ──
            UnitType.Pistoleer    => 50,
            UnitType.Assault      => 100,
            UnitType.Sniper       => 200,
            // ── 정령계 ──
            UnitType.EmberSpirit  => 50,
            UnitType.FlameSpirit  => 200,
            UnitType.InfernoSpirit => 500,
            // ── 초월계 ──
            UnitType.FoxMagician  => 50,
            UnitType.BearGuard    => 400,
            UnitType.LionKnight   => 200,
            _ => 50
        };

        /// <summary> 유닛 생산에 필요한 인구. 모든 유닛 1 인구. </summary>
        public static int GetPopulationCost(UnitType type) => 1;
    }
}
