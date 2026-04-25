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
        }

        // ====================================================================
        // 런타임 Dictionary
        // ====================================================================

        /// <summary>
        /// (건물 타입, 종족) 조합 → 스탯 값 매핑.
        /// Initialize 전에는 null이며, 이 경우 폴백 값을 반환한다.
        /// </summary>
        private static Dictionary<(BuildingType, RaceId), StatValues> _data;

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
        /// 기존 switch 표현식과 동일한 값.
        /// </summary>
        private static int GetDefaultMaxHp(BuildingType type, RaceId race)
        {
            // Transcendence 종족은 Castle/Barracks/MiningPost 모두 더 높은 HP
            switch (type)
            {
                case BuildingType.Castle:
                    return race == RaceId.Transcendence ? 200 : 100;
                case BuildingType.Barracks:
                    return race == RaceId.Transcendence ? 50 : 30;
                case BuildingType.MiningPost:
                    return race == RaceId.Transcendence ? 40 : 20;
                default:
                    return 10;
            }
        }

        /// <summary>
        /// Initialize 미호출 / Config 미연결 시 사용할 폴백 골드 비용.
        /// 기존 GameConfig의 기본값과 동일 (Barracks=100, MiningPost=50, Castle=0).
        /// </summary>
        private static int GetDefaultGoldCost(BuildingType type, RaceId race)
        {
            switch (type)
            {
                case BuildingType.Barracks: return 100;
                case BuildingType.MiningPost: return 50;
                default: return 0; // Castle 및 알 수 없는 타입
            }
        }
    }
}
