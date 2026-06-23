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

using System.Collections.Generic;

namespace Hexiege.Domain
{
    public static class BuildingTypeHelper
    {
        // ====================================================================
        // lookup table (생산건물 메타데이터)
        //
        // 왜 switch가 아니라 Dictionary인가?
        //   - 기존에는 IsProductionBuilding / GetStage / GetNextStage 세 메서드가
        //     각각 동일한 건물 목록을 switch로 중복 나열했다.
        //   - 신규 건물 추가 시 세 곳을 모두 수정해야 해서 누락 위험이 컸다.
        //   - 한 건물의 모든 속성(생산 여부 / 단계 / 다음 단계)을 한 행에 모아두면
        //     추가·수정이 한 곳에서 끝나고, 세 메서드는 단순 조회만 한다.
        // ====================================================================

        /// <summary>
        /// 생산건물 1개의 메타데이터(생산 여부 / 단계 / 다음 단계)를 담는 불변 구조체.
        /// 값 타입(struct)이라 Dictionary 값으로 저장돼도 추가 힙 할당이 없다.
        /// </summary>
        private readonly struct BuildingMeta
        {
            public readonly bool IsProduction;        // 생산건물 여부
            public readonly int Stage;                // 단계(1·2·3)
            public readonly BuildingType? NextStage;  // 다음 단계(없으면 null)

            public BuildingMeta(bool isProduction, int stage, BuildingType? nextStage = null)
            {
                IsProduction = isProduction;
                Stage = stage;
                NextStage = nextStage;
            }
        }

        /// <summary>
        /// 모든 생산건물의 메타데이터 테이블.
        /// 여기에 없는 건물(Castle, MiningPost, AutoTower 등)은 비생산건물로 간주된다.
        /// 신규 생산건물을 추가할 때는 이 테이블에 한 행만 추가하면 된다.
        /// </summary>
        private static readonly Dictionary<BuildingType, BuildingMeta> _buildingTable =
            new Dictionary<BuildingType, BuildingMeta>
            {
                // Human — 근거리A
                { BuildingType.TrainingCamp,      new BuildingMeta(true, 1, BuildingType.WarAcademy) },
                { BuildingType.WarAcademy,        new BuildingMeta(true, 2, BuildingType.HumanBarracks) },
                { BuildingType.HumanBarracks,     new BuildingMeta(true, 3) },
                // Human — 총기류
                { BuildingType.Gunsmith,          new BuildingMeta(true, 1, BuildingType.Armory) },
                { BuildingType.Armory,            new BuildingMeta(true, 2, BuildingType.WeaponForge) },
                { BuildingType.WeaponForge,       new BuildingMeta(true, 3) },
                // Human — 탈것류 (2단계까지)
                { BuildingType.Garage,            new BuildingMeta(true, 1, BuildingType.VehicleBay) },
                { BuildingType.VehicleBay,        new BuildingMeta(true, 2) },
                // Spirit — 불
                { BuildingType.FireSpire,         new BuildingMeta(true, 1, BuildingType.BlazeConduit) },
                { BuildingType.BlazeConduit,      new BuildingMeta(true, 2, BuildingType.InfernoCore) },
                { BuildingType.InfernoCore,       new BuildingMeta(true, 3) },
                // Spirit — 물
                { BuildingType.AquaSpring,        new BuildingMeta(true, 1, BuildingType.TidalNexus) },
                { BuildingType.TidalNexus,        new BuildingMeta(true, 2, BuildingType.OceanicHeart) },
                { BuildingType.OceanicHeart,      new BuildingMeta(true, 3) },
                // Spirit — 땅
                { BuildingType.StoneMound,        new BuildingMeta(true, 1, BuildingType.TerraForge) },
                { BuildingType.TerraForge,        new BuildingMeta(true, 2, BuildingType.GaeaSanctum) },
                { BuildingType.GaeaSanctum,       new BuildingMeta(true, 3) },
                // Transcendence — 동물A
                { BuildingType.PrimalAltar,       new BuildingMeta(true, 1, BuildingType.PrimalDen) },
                { BuildingType.PrimalDen,         new BuildingMeta(true, 2, BuildingType.PrimalSanctuary) },
                { BuildingType.PrimalSanctuary,   new BuildingMeta(true, 3) },  // 생산 건물로 포함 (사용자 확인)
                // Transcendence — 동물B
                { BuildingType.FeralAltar,        new BuildingMeta(true, 1, BuildingType.FeralDen) },
                { BuildingType.FeralDen,          new BuildingMeta(true, 2, BuildingType.FeralSanctuary) },
                { BuildingType.FeralSanctuary,    new BuildingMeta(true, 3) },
                // Transcendence — 식물 (2단계까지)
                { BuildingType.SporePatch,        new BuildingMeta(true, 1, BuildingType.FloralNursery) },
                { BuildingType.FloralNursery,     new BuildingMeta(true, 2) },
            };

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
            => _buildingTable.TryGetValue(type, out var meta) && meta.IsProduction;

        // [Phase 2 리팩토링] 기존 switch 본문 — 테스트 통과 후 삭제 예정.
        /*
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
        */

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
            => _buildingTable.TryGetValue(type, out var meta) ? meta.Stage : 0;

        // [Phase 2 리팩토링] 기존 switch 본문 — 테스트 통과 후 삭제 예정.
        /*
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
        */

        // ====================================================================
        // 업그레이드 경로
        // ====================================================================

        /// <summary>
        /// 다음 단계 건물 타입을 반환한다.
        /// 다음 단계가 없으면(최고 단계 / 비생산건물) null을 반환한다.
        /// 예: TrainingCamp → WarAcademy, Garage → VehicleBay, VehicleBay → null
        /// </summary>
        public static BuildingType? GetNextStage(BuildingType type)
            => _buildingTable.TryGetValue(type, out var meta) ? meta.NextStage : null;

        // [Phase 2 리팩토링] 기존 switch 본문 — 테스트 통과 후 삭제 예정.
        /*
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
        */

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
