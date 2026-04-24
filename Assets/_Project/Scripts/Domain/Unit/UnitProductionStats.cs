// ============================================================================
// UnitProductionStats.cs
// 유닛 타입별 생산 시간과 비용을 조회하는 정적 클래스.
//
// 과거에는 switch 표현식으로 수치가 하드코딩돼 있었으나, 현재는
// UnitStats와 마찬가지로 UnitStatsConfig(ScriptableObject)에서 값을 주입받아
// 내부 Dictionary에 저장해 두는 방식으로 변경됐다.
//
// 초기화 흐름:
//   1) GameBootstrapper가 Inspector에 연결된 UnitStatsConfig를 읽는다.
//   2) Config 항목을 Domain-친화적인 ProductionValues 형태로 변환해
//      Initialize(data)에 전달한다.
//   3) 이후 UnitProductionUseCase / UI 등 기존 API
//      (GetProductionTime, GetGoldCost, GetPopulationCost)를 호출하면
//      Dictionary에서 값이 반환된다.
//
// 메서드 시그니처는 과거와 100% 동일 — 호출부 수정 불필요.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    public static class UnitProductionStats
    {
        // ====================================================================
        // ProductionValues
        // 한 유닛의 생산 관련 수치(시간/골드/인구)를 담는 순수 C# 구조체.
        // Infrastructure의 UnitStatEntry를 Domain이 직접 참조하지 않기 위해
        // Initialize()용 별도 타입을 Domain 내부에 정의.
        // ====================================================================
        public struct ProductionValues
        {
            public float ProductionTime;
            public int GoldCost;
            public int PopulationCost;
        }

        // 내부 Dictionary. Initialize()가 호출되기 전에는 null.
        private static Dictionary<UnitType, ProductionValues> _data;

        /// <summary>
        /// UnitStatsConfig(또는 동등한 소스)에서 변환한 ProductionValues 사전을 주입한다.
        /// GameBootstrapper에서 게임 시작 시 1회 호출하는 것을 상정.
        /// 재호출하면 기존 데이터를 교체.
        /// </summary>
        public static void Initialize(IReadOnlyDictionary<UnitType, ProductionValues> data)
        {
            if (data == null)
            {
                _data = null;
                return;
            }

            // 방어적 복사 — 외부에서 원본 Dictionary가 바뀌어도 영향 없도록.
            _data = new Dictionary<UnitType, ProductionValues>(data.Count);
            foreach (var kv in data)
            {
                _data[kv.Key] = kv.Value;
            }
        }

        /// <summary>
        /// 내부용 헬퍼. Dictionary가 초기화됐고 key가 있으면 해당 ProductionValues 반환,
        /// 아니면 found=false.
        /// </summary>
        private static bool TryGet(UnitType type, out ProductionValues values)
        {
            if (_data != null && _data.TryGetValue(type, out values))
            {
                return true;
            }
            values = default;
            return false;
        }

        /// <summary> 유닛 생산에 걸리는 시간(초). </summary>
        public static float GetProductionTime(UnitType type)
            => TryGet(type, out var v) ? v.ProductionTime : 5f;

        /// <summary> 유닛 생산에 필요한 골드. </summary>
        public static int GetGoldCost(UnitType type)
            => TryGet(type, out var v) ? v.GoldCost : 50;

        /// <summary>
        /// 유닛 생산에 필요한 인구. 기본적으로 모든 유닛이 1이지만
        /// Config에서 개별 유닛의 값을 바꿀 수 있게 열어둠.
        /// Config 값이 0 이하로 설정돼 있으면 안전망으로 1을 반환.
        /// </summary>
        public static int GetPopulationCost(UnitType type)
        {
            if (TryGet(type, out var v) && v.PopulationCost > 0)
            {
                return v.PopulationCost;
            }
            return 1;
        }
    }
}
