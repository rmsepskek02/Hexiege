// ============================================================================
// BuildingStats.cs
// 건물 타입 + 종족별 스탯(HP / 골드 비용 / 공격력)을 관리하는 정적 클래스.
//
// 동작 방식:
//   - 내부적으로 Dictionary<(BuildingType, RaceId), StatValues>를 보유.
//   - Bootstrap 레이어(GameBootstrapper)에서 Initialize()를 호출하여
//     ScriptableObject(BuildingStatsConfig) 값으로 Dictionary를 채워준다.
//   - 런타임에는 GetMaxHp / GetGoldCost / GetAttackPower 등으로 조회만 수행.
//
// 왜 이렇게 분리했나?
//   Domain 레이어는 Unity 의존(ScriptableObject)이 금지됨.
//   따라서 Domain은 "순수 C# Dictionary"만 다루고,
//   ScriptableObject → Dictionary 변환은 Bootstrap에서 처리.
//   UnitStats와 동일한 패턴.
//
// Initialize 호출 전(또는 Config 미연결 시)에는 GetDefault*() 메서드의
// 폴백 값을 반환한다 — 기존 switch 표현식과 동일한 값이므로 안전망 역할.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    public static class BuildingStats
    {
        // ====================================================================
        // StatValues — Domain 내부 순수 C# 구조체
        // ====================================================================

        /// <summary>
        /// 한 (BuildingType, RaceId) 조합에 대한 수치 묶음.
        /// Unity에 의존하지 않는 순수 C# 구조체.
        /// </summary>
        public struct StatValues
        {
            /// <summary> 최대 체력. </summary>
            public int MaxHp;

            /// <summary> 건설 비용(골드). Castle은 자동 배치이므로 0. </summary>
            public int GoldCost;

            /// <summary> 공격력. 현재 미사용, 향후 타워 기능용. </summary>
            public int AttackPower;

            /// <summary> 공격 쿨다운(초). 현재 미사용, 향후 타워 기능용. 비타워 건물은 0. </summary>
            public float AttackCooldown;

            /// <summary>
            /// 이 건물을 다음 단계로 업그레이드할 때 드는 골드 비용.
            /// 최고 단계 건물이나 비생산건물은 0.
            /// 종족과 무관하게 BuildingType당 단일 값으로 관리한다.
            /// </summary>
            public int UpgradeCost;
        }

        // ====================================================================
        // 런타임 Dictionary
        // ====================================================================

        /// <summary>
        /// (건물 타입, 종족) 조합 → 스탯 값 매핑.
        /// Initialize 전에는 null이며, 이 경우 폴백 값을 반환한다.
        /// </summary>
        private static Dictionary<(BuildingType, RaceId), StatValues> _data;

        // (건물 타입, 종족) → 해당 건물까지 투자된 총 골드 (1단계 건설비 + 모든 업그레이드비 합산).
        // GameBootstrapper.InitializeBuildingStatsFromConfig() 완료 후 캐싱된다.
        private static Dictionary<(BuildingType, RaceId), int> _totalInvestedCostCache
            = new Dictionary<(BuildingType, RaceId), int>();

        /// <summary>
        /// GameBootstrapper 초기화 시 호출.
        /// 해당 (건물 타입, 종족) 조합의 누적 투자 비용을 캐시에 저장한다.
        /// 누적 투자 비용 = 1단계 건설비 + 1→2 업그레이드비 + 2→3 업그레이드비 (해당하는 경우).
        /// </summary>
        public static void SetTotalInvestedCost(BuildingType type, RaceId race, int total)
            => _totalInvestedCostCache[(type, race)] = total;

        /// <summary>
        /// 해당 건물까지 투자된 총 골드를 반환한다.
        /// GameBootstrapper 초기화 후에만 정확한 값을 반환하며,
        /// 캐시에 없으면 현재 건물의 건설비(GetGoldCost)를 폴백으로 반환한다.
        /// 철거 환불 계산에 사용: 반환값 / 2 = 환불 금액.
        /// </summary>
        public static int GetTotalInvestedCost(BuildingType type, RaceId race)
            => _totalInvestedCostCache.TryGetValue((type, race), out int v) ? v : GetGoldCost(type, race);

        /// <summary>
        /// Bootstrap 레이어에서 호출.
        /// ScriptableObject(BuildingStatsConfig) 값을 Domain용 Dictionary에 주입한다.
        /// 동일 키가 중복되면 나중 값이 덮어쓴다 (Dictionary의 기본 동작).
        /// </summary>
        /// <param name="data">외부에서 만들어 전달한 (타입, 종족) → 스탯 딕셔너리.</param>
        public static void Initialize(IReadOnlyDictionary<(BuildingType, RaceId), StatValues> data)
        {
            if (data == null)
            {
                _data = null;
                return;
            }

            _data = new Dictionary<(BuildingType, RaceId), StatValues>(data.Count);
            foreach (var kv in data)
            {
                _data[kv.Key] = kv.Value;
            }
        }

        // ====================================================================
        // 조회 API — 기존 시그니처 유지 + 신규 추가
        // ====================================================================

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
        public static int GetMaxHp(BuildingType type, RaceId race)
        {
            if (TryGet(type, race, out var v))
                return v.MaxHp;
            return GetDefaultMaxHp(type, race);
        }

        /// <summary>
        /// 건물 타입 + 종족별 건설 비용(골드) 반환.
        /// Castle은 자동 배치이므로 0을 반환한다.
        /// </summary>
        public static int GetGoldCost(BuildingType type, RaceId race)
        {
            if (TryGet(type, race, out var v))
                return v.GoldCost;
            return GetDefaultGoldCost(type, race);
        }

        /// <summary>
        /// 건물 타입 + 종족별 공격력 반환.
        /// 현재는 미사용이며, 향후 공격 기능을 가진 건물(타워 등) 도입 시 활용.
        /// </summary>
        public static int GetAttackPower(BuildingType type, RaceId race)
        {
            if (TryGet(type, race, out var v))
                return v.AttackPower;
            return 0;
        }

        /// <summary>
        /// 건물 타입 + 종족별 공격 쿨다운(초) 반환.
        /// 현재는 미사용이며, 향후 타워 기능 도입 시 활용.
        /// 비타워 건물이거나 Config에 값이 없으면 0을 반환.
        /// </summary>
        public static float GetAttackCooldown(BuildingType type, RaceId race)
        {
            if (TryGet(type, race, out var v))
                return v.AttackCooldown;
            return 0f;
        }

        /// <summary>
        /// 건물을 다음 단계로 업그레이드할 때 드는 골드 비용 반환.
        /// 업그레이드 비용은 종족과 무관하게 BuildingType당 단일 값으로 관리한다.
        /// 따라서 내부 Dictionary 조회 시에는 임의 종족(Human)을 키로 사용해도 무방.
        /// Initialize 시점에 모든 종족 항목에 동일 값을 넣어두기 때문이다.
        ///
        /// 폴백: Dictionary에 없으면 0을 반환 (최고 단계 / 비생산건물 = 업그레이드 불가).
        /// </summary>
        public static int GetUpgradeCost(BuildingType type)
        {
            // 어떤 종족 키로 조회해도 같은 값이 들어있도록 Initialize에서 보장.
            if (TryGet(type, RaceId.Human, out var vh)) return vh.UpgradeCost;
            if (TryGet(type, RaceId.Spirit, out var vs)) return vs.UpgradeCost;
            if (TryGet(type, RaceId.Transcendence, out var vt)) return vt.UpgradeCost;
            return 0;
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// Dictionary에서 (타입, 종족) 조합의 값을 안전하게 조회.
        /// Initialize 전이거나 키가 없으면 false를 반환하여 호출자가 폴백을 쓰게 한다.
        /// </summary>
        private static bool TryGet(BuildingType type, RaceId race, out StatValues value)
        {
            if (_data != null && _data.TryGetValue((type, race), out value))
                return true;

            value = default;
            return false;
        }

        /// <summary>
        /// Initialize 미호출 / Config 미연결 시 사용할 폴백 HP 값.
        /// 모든 생산건물(IsProductionBuilding=true)은 단계별로 동일한 기본 HP를 갖는다.
        /// 실제 운영에서는 Inspector의 BuildingStatsConfig에서 단계별/종족별로 조정한다.
        /// </summary>
        private static int GetDefaultMaxHp(BuildingType type, RaceId race)
        {
            // 비생산건물 폴백
            switch (type)
            {
                case BuildingType.Castle:
                    return race == RaceId.Transcendence ? 200 : 100;
                case BuildingType.MiningPost:
                    return race == RaceId.Transcendence ? 40 : 20;
            }

            // 생산건물 폴백 — 단계별 기본 HP (1단계 30, 2단계 45, 3단계 60)
            // Transcendence는 다른 종족 대비 약 1.6배.
            if (BuildingTypeHelper.IsProductionBuilding(type))
            {
                int stage = BuildingTypeHelper.GetStage(type);
                int baseHp = stage switch
                {
                    1 => 30,
                    2 => 45,
                    3 => 60,
                    _ => 30
                };
                return race == RaceId.Transcendence ? (int)(baseHp * 1.6f) : baseHp;
            }

            // 알 수 없는 타입
            return 10;
        }

        /// <summary>
        /// Initialize 미호출 / Config 미연결 시 사용할 폴백 골드 비용.
        /// MiningPost=50, Castle=0. 생산건물은 단계별로 기본값을 다르게 매긴다.
        /// 실제 운영에서는 Inspector에서 정확한 값을 설정한다.
        /// </summary>
        private static int GetDefaultGoldCost(BuildingType type, RaceId race)
        {
            switch (type)
            {
                case BuildingType.MiningPost: return 50;
                case BuildingType.Castle:     return 0;
            }

            // 생산건물 폴백 — 단계별 기본 골드 비용 (1단계 100, 2단계 150, 3단계 200)
            if (BuildingTypeHelper.IsProductionBuilding(type))
            {
                int stage = BuildingTypeHelper.GetStage(type);
                return stage switch
                {
                    1 => 100,
                    2 => 150,
                    3 => 200,
                    _ => 100
                };
            }

            return 0;
        }
    }
}
