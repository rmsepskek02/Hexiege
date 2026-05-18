// ============================================================================
// BuildingTypeHelper.cs
// BuildingType 열거형에 대한 카테고리/단계/업그레이드 경로를 판별하는 정적 헬퍼.
//
// 왜 별도 클래스인가?
//   - 열거형(BuildingType)에는 데이터를 직접 매달 수 없으므로 헬퍼로 분리.
//   - 생산 라인/단계 매핑이 한 곳에 모여 있어 신규 건물 추가 시 이 파일만 손대면 됨.
//
// 사용 예시:
//   if (BuildingTypeHelper.IsProductionBuilding(building.Type)) { ... }
//   BuildingType? next = BuildingTypeHelper.GetNextStage(building.Type);
//   int stage = BuildingTypeHelper.GetStage(building.Type); // 1, 2, 3 또는 0(비생산)
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public static class BuildingTypeHelper
    {
        // ====================================================================
        // 카테고리 판별
        // ====================================================================

        /// <summary>
        /// 해당 건물 타입이 "유닛을 생산하는 건물"인지 여부를 반환한다.
        /// 기존 코드에서 `type == BuildingType.Barracks` 형태로 사용되던 분기를
        /// 이 메서드 호출로 대체하여, 모든 종족·라인·단계의 생산건물을 인식한다.
        /// </summary>
        /// <param name="type">검사할 건물 타입.</param>
        /// <returns>생산건물이면 true, 비생산건물(Castle, MiningPost 등)이면 false.</returns>
        public static bool IsProductionBuilding(BuildingType type)
        {
            switch (type)
            {
                // Human 생산건물
                case BuildingType.TrainingCamp:
                case BuildingType.WarAcademy:
                case BuildingType.HumanBarracks:
                case BuildingType.Gunsmith:
                case BuildingType.Armory:
                case BuildingType.WeaponForge:
                case BuildingType.Garage:
                case BuildingType.VehicleBay:
                // Spirit 생산건물
                case BuildingType.FireSpire:
                case BuildingType.BlazeConduit:
                case BuildingType.InfernoCore:
                case BuildingType.AquaSpring:
                case BuildingType.TidalNexus:
                case BuildingType.OceanicHeart:
                case BuildingType.StoneMound:
                case BuildingType.TerraForge:
                case BuildingType.GaeaSanctum:
                // Transcendence 생산건물
                case BuildingType.PrimalAltar:
                case BuildingType.PrimalDen:
                case BuildingType.PrimalSanctuary:
                case BuildingType.FeralAltar:
                case BuildingType.FeralDen:
                case BuildingType.FeralSanctuary:
                case BuildingType.SporePatch:
                case BuildingType.FloralNursery:
                    return true;

                default:
                    // Castle, MiningPost, AutoTower 등 비생산건물
                    return false;
            }
        }

        // ====================================================================
        // 단계(Stage) 조회
        // ====================================================================

        /// <summary>
        /// 건물의 단계 번호를 반환한다.
        /// 1·2·3은 생산건물의 각 단계, 0은 비생산건물(또는 단계 개념이 없는 건물).
        /// </summary>
        /// <param name="type">검사할 건물 타입.</param>
        /// <returns>1/2/3 (생산건물 단계) 또는 0 (비생산건물).</returns>
        public static int GetStage(BuildingType type)
        {
            switch (type)
            {
                // ---------------- 1단계 ----------------
                case BuildingType.TrainingCamp:
                case BuildingType.Gunsmith:
                case BuildingType.Garage:
                case BuildingType.FireSpire:
                case BuildingType.AquaSpring:
                case BuildingType.StoneMound:
                case BuildingType.PrimalAltar:
                case BuildingType.FeralAltar:
                case BuildingType.SporePatch:
                    return 1;

                // ---------------- 2단계 ----------------
                case BuildingType.WarAcademy:
                case BuildingType.Armory:
                case BuildingType.VehicleBay:
                case BuildingType.BlazeConduit:
                case BuildingType.TidalNexus:
                case BuildingType.TerraForge:
                case BuildingType.PrimalDen:
                case BuildingType.FeralDen:
                case BuildingType.FloralNursery:
                    return 2;

                // ---------------- 3단계 ----------------
                case BuildingType.HumanBarracks:
                case BuildingType.WeaponForge:
                case BuildingType.InfernoCore:
                case BuildingType.OceanicHeart:
                case BuildingType.GaeaSanctum:
                case BuildingType.PrimalSanctuary:
                case BuildingType.FeralSanctuary:
                    return 3;

                default:
                    return 0;
            }
        }

        // ====================================================================
        // 업그레이드 경로
        // ====================================================================

        /// <summary>
        /// 다음 단계 건물 타입을 반환한다.
        /// 다음 단계가 없으면(최고 단계 / 비생산건물) null을 반환한다.
        /// 예: TrainingCamp → WarAcademy, Garage → VehicleBay, VehicleBay → null
        /// </summary>
        public static BuildingType? GetNextStage(BuildingType type)
        {
            switch (type)
            {
                // Human — 근거리A
                case BuildingType.TrainingCamp:  return BuildingType.WarAcademy;
                case BuildingType.WarAcademy:    return BuildingType.HumanBarracks;
                // Human — 총기류
                case BuildingType.Gunsmith:      return BuildingType.Armory;
                case BuildingType.Armory:        return BuildingType.WeaponForge;
                // Human — 탈것류 (2단계까지만)
                case BuildingType.Garage:        return BuildingType.VehicleBay;

                // Spirit — 불
                case BuildingType.FireSpire:     return BuildingType.BlazeConduit;
                case BuildingType.BlazeConduit:  return BuildingType.InfernoCore;
                // Spirit — 물
                case BuildingType.AquaSpring:    return BuildingType.TidalNexus;
                case BuildingType.TidalNexus:    return BuildingType.OceanicHeart;
                // Spirit — 땅
                case BuildingType.StoneMound:    return BuildingType.TerraForge;
                case BuildingType.TerraForge:    return BuildingType.GaeaSanctum;

                // Transcendence — 동물A
                case BuildingType.PrimalAltar:   return BuildingType.PrimalDen;
                case BuildingType.PrimalDen:     return BuildingType.PrimalSanctuary;
                // Transcendence — 동물B
                case BuildingType.FeralAltar:    return BuildingType.FeralDen;
                case BuildingType.FeralDen:      return BuildingType.FeralSanctuary;
                // Transcendence — 식물 (2단계까지만)
                case BuildingType.SporePatch:    return BuildingType.FloralNursery;

                // 최고 단계 또는 비생산건물 → 업그레이드 경로 없음
                default:
                    return null;
            }
        }

        /// <summary>
        /// 해당 건물이 업그레이드 가능한지 여부를 반환한다.
        /// 다음 단계 타입이 존재하면 true, 최고 단계 / 비생산건물이면 false.
        /// </summary>
        public static bool CanUpgrade(BuildingType type)
        {
            return GetNextStage(type).HasValue;
        }

        // ====================================================================
        // 액션 패널 표시 가능 여부
        // ====================================================================

        /// <summary>
        /// 비생산 건물 중 액션 패널(BuildingActionPanelUI)을 표시할 대상인지 판정.
        /// 생산 건물은 ProductionPanelUI가 담당하므로 제외하며,
        /// Castle은 철거 불가 + 생산 불가이므로 클릭해도 액션 패널을 표시하지 않는다.
        /// </summary>
        /// <param name="type">검사할 건물 타입.</param>
        /// <returns>액션 패널을 표시할 비생산 건물이면 true.</returns>
        public static bool CanShowActionPanel(BuildingType type)
        {
            // 생산 건물은 생산 패널이 담당 → 제외
            if (IsProductionBuilding(type)) return false;
            // Castle은 철거 불가 + 다른 액션도 없음 → 제외
            if (type == BuildingType.Castle) return false;
            // 그 외 비생산 건물(MiningPost, AutoTower 등)은 액션 패널 표시 대상
            return true;
        }
    }
}
