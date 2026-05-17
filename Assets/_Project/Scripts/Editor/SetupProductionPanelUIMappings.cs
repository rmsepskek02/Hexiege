// ============================================================================
// SetupProductionPanelUIMappings.cs
// ProductionPanelUI 컴포넌트의 _buildingUnitMappings 리스트에
// 모든 생산건물-유닛 매핑을 자동으로 할당하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/ProductionPanelUI 유닛 매핑 설정
//
// 실행 전 준비:
//   - Game.unity 씬을 열어둘 것 (ProductionPanelUI 컴포넌트가 있는 씬)
//
// 매핑 기준:
//   - 같은 생산 라인의 1/2/3단계 건물은 모두 동일한 유닛 리스트를 가짐
//   - requiredStage 값으로 잠금 여부를 결정 (현재 건물 단계 < requiredStage → 잠금)
//   - 예: TrainingCamp(1단계)에서 SpearMan(requiredStage=2)은 잠금 표시됨
//
// 유닛 매핑 (종족/라인별 요약):
//   Human 근거리A  : TrainingCamp → WarAcademy → HumanBarracks
//                    LittleKnight(1), SpearMan(2), BattleAxe(3)
//   Human 총기류   : Gunsmith → Armory → WeaponForge
//                    Pistoleer(1), Assault(2), Sniper(3)
//   Human 탈것류   : Garage → VehicleBay
//                    CannonCart(1), Tank(2)
//   Spirit 불      : FireSpire → BlazeConduit → InfernoCore
//                    EmberSpirit(1), FlameSpirit(2), InfernoSpirit(3)
//   Spirit 물      : AquaSpring → TidalNexus → OceanicHeart
//                    TideSpirit(1), StreamSpirit(2), TorrentSpirit(3)
//   Spirit 땅      : StoneMound → TerraForge → GaeaSanctum
//                    DustSpirit(1), BoulderSpirit(2), QuakeSpirit(3)
//   Trans. 동물A   : PrimalAltar → PrimalDen → PrimalSanctuary
//                    RabbitTrickster(1), RhinoBreaker(2), BearGuard(3)
//   Trans. 동물B   : FeralAltar → FeralDen → FeralSanctuary
//                    FoxMagician(1), EagleArcher(2), LionKnight(3)
//   Trans. 식물    : SporePatch → FloralNursery
//                    MushroomBomber(1), BloomFairy(2)
//
// 1회성 — 실행 후 삭제해도 무방.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Presentation;

public static class SetupProductionPanelUIMappings
{
    // 유닛 초상화 스프라이트가 들어있는 루트 경로
    private const string PORTRAIT_ROOT = "Assets/_Project/Sprites/Units";

    [MenuItem("Hexiege/Setup/ProductionPanelUI 유닛 매핑 설정")]
    private static void Setup()
    {
        // ── Scene에서 ProductionPanelUI 컴포넌트 탐색 ─────────────────────
        var ui = Object.FindFirstObjectByType<ProductionPanelUI>();
        if (ui == null)
        {
            Debug.LogError("[Setup] ProductionPanelUI 컴포넌트를 씬에서 찾을 수 없습니다. " +
                           "Game.unity 씬을 열고 다시 실행해주세요.");
            return;
        }

        // SerializedObject를 통해 수정하면 Undo 등록 + 씬 Dirty 마킹이 자동 처리됨
        var so = new SerializedObject(ui);
        so.Update();

        var listProp = so.FindProperty("_buildingUnitMappings");
        if (listProp == null)
        {
            Debug.LogError("[Setup] '_buildingUnitMappings' 프로퍼티를 찾을 수 없습니다. " +
                           "필드명이 변경됐는지 확인해주세요.");
            return;
        }

        // 기존 항목 전체 제거
        listProp.ClearArray();
        int index = 0;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Human 근거리A 라인: LittleKnight(1) → SpearMan(2) → BattleAxe(3)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        var humanMeleeBlue = MakeList(
            (UnitType.LittleKnight, "Human/LittleKnight/littleknight_portrait_blue.png", 1),
            (UnitType.SpearMan,     "Human/SpearMan/spearman_portrait_blue.png",         2),
            (UnitType.BattleAxe,    "Human/BattleAxe/battleaxe_portrait_blue.png",       3)
        );
        var humanMeleeRed = MakeList(
            (UnitType.LittleKnight, "Human/LittleKnight/littleknight_portrait_red.png", 1),
            (UnitType.SpearMan,     "Human/SpearMan/spearman_portrait_red.png",         2),
            (UnitType.BattleAxe,    "Human/BattleAxe/battleaxe_portrait_red.png",       3)
        );
        AddMapping(listProp, ref index, BuildingType.TrainingCamp,  humanMeleeBlue, humanMeleeRed);
        AddMapping(listProp, ref index, BuildingType.WarAcademy,    humanMeleeBlue, humanMeleeRed);
        AddMapping(listProp, ref index, BuildingType.HumanBarracks, humanMeleeBlue, humanMeleeRed);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Human 총기류 라인: Pistoleer(1) → Assault(2) → Sniper(3)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        var humanGunBlue = MakeList(
            (UnitType.Pistoleer, "Human/Pistoleer/pistoleer_portrait_blue.png", 1),
            (UnitType.Assault,   "Human/Assault/assault_portrait_blue.png",     2),
            (UnitType.Sniper,    "Human/Sniper/sniper_portrait_blue.png",       3)
        );
        var humanGunRed = MakeList(
            (UnitType.Pistoleer, "Human/Pistoleer/pistoleer_portrait_red.png", 1),
            (UnitType.Assault,   "Human/Assault/assault_portrait_red.png",     2),
            (UnitType.Sniper,    "Human/Sniper/sniper_portrait_red.png",       3)
        );
        AddMapping(listProp, ref index, BuildingType.Gunsmith,    humanGunBlue, humanGunRed);
        AddMapping(listProp, ref index, BuildingType.Armory,      humanGunBlue, humanGunRed);
        AddMapping(listProp, ref index, BuildingType.WeaponForge, humanGunBlue, humanGunRed);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Human 탈것류 라인: CannonCart(1) → Tank(2)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        var humanVehicleBlue = MakeList(
            (UnitType.CannonCart, "Human/CannonCart/cannoncart_portrait_blue.png", 1),
            (UnitType.Tank,       "Human/Tank/tank_portrait_blue.png",             2)
        );
        var humanVehicleRed = MakeList(
            (UnitType.CannonCart, "Human/CannonCart/cannoncart_portrait_red.png", 1),
            (UnitType.Tank,       "Human/Tank/tank_portrait_red.png",             2)
        );
        AddMapping(listProp, ref index, BuildingType.Garage,     humanVehicleBlue, humanVehicleRed);
        AddMapping(listProp, ref index, BuildingType.VehicleBay, humanVehicleBlue, humanVehicleRed);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Spirit 불 라인: EmberSpirit(1) → FlameSpirit(2) → InfernoSpirit(3)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        var spiritFireBlue = MakeList(
            (UnitType.EmberSpirit,   "Spirit/EmberSpirit/emberspirit_portrait_blue.png",     1),
            (UnitType.FlameSpirit,   "Spirit/FlameSpirit/flamespirit_portrait_blue.png",     2),
            (UnitType.InfernoSpirit, "Spirit/InfernoSpirit/infernospirit_portrait_blue.png", 3)
        );
        var spiritFireRed = MakeList(
            (UnitType.EmberSpirit,   "Spirit/EmberSpirit/emberspirit_portrait_red.png",     1),
            (UnitType.FlameSpirit,   "Spirit/FlameSpirit/flamespirit_portrait_red.png",     2),
            (UnitType.InfernoSpirit, "Spirit/InfernoSpirit/infernospirit_portrait_red.png", 3)
        );
        AddMapping(listProp, ref index, BuildingType.FireSpire,    spiritFireBlue, spiritFireRed);
        AddMapping(listProp, ref index, BuildingType.BlazeConduit, spiritFireBlue, spiritFireRed);
        AddMapping(listProp, ref index, BuildingType.InfernoCore,  spiritFireBlue, spiritFireRed);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Spirit 물 라인: TideSpirit(1) → StreamSpirit(2) → TorrentSpirit(3)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        var spiritWaterBlue = MakeList(
            (UnitType.TideSpirit,    "Spirit/TideSpirit/tidespirit_portrait_blue.png",       1),
            (UnitType.StreamSpirit,  "Spirit/StreamSpirit/streamspirit_portrait_blue.png",   2),
            (UnitType.TorrentSpirit, "Spirit/TorrentSpirit/torrentspirit_portrait_blue.png", 3)
        );
        var spiritWaterRed = MakeList(
            (UnitType.TideSpirit,    "Spirit/TideSpirit/tidespirit_portrait_red.png",       1),
            (UnitType.StreamSpirit,  "Spirit/StreamSpirit/streamspirit_portrait_red.png",   2),
            (UnitType.TorrentSpirit, "Spirit/TorrentSpirit/torrentspirit_portrait_red.png", 3)
        );
        AddMapping(listProp, ref index, BuildingType.AquaSpring,   spiritWaterBlue, spiritWaterRed);
        AddMapping(listProp, ref index, BuildingType.TidalNexus,   spiritWaterBlue, spiritWaterRed);
        AddMapping(listProp, ref index, BuildingType.OceanicHeart, spiritWaterBlue, spiritWaterRed);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Spirit 땅 라인: DustSpirit(1) → BoulderSpirit(2) → QuakeSpirit(3)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        var spiritEarthBlue = MakeList(
            (UnitType.DustSpirit,    "Spirit/DustSpirit/dustspirit_portrait_blue.png",       1),
            (UnitType.BoulderSpirit, "Spirit/BoulderSpirit/boulderspirit_portrait_blue.png", 2),
            (UnitType.QuakeSpirit,   "Spirit/QuakeSpirit/quakespirit_portrait_blue.png",     3)
        );
        var spiritEarthRed = MakeList(
            (UnitType.DustSpirit,    "Spirit/DustSpirit/dustspirit_portrait_red.png",       1),
            (UnitType.BoulderSpirit, "Spirit/BoulderSpirit/boulderspirit_portrait_red.png", 2),
            (UnitType.QuakeSpirit,   "Spirit/QuakeSpirit/quakespirit_portrait_red.png",     3)
        );
        AddMapping(listProp, ref index, BuildingType.StoneMound, spiritEarthBlue, spiritEarthRed);
        AddMapping(listProp, ref index, BuildingType.TerraForge, spiritEarthBlue, spiritEarthRed);
        AddMapping(listProp, ref index, BuildingType.GaeaSanctum,spiritEarthBlue, spiritEarthRed);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Transcendence 동물A 라인: RabbitTrickster(1) → RhinoBreaker(2) → BearGuard(3)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        var transAnimalABlue = MakeList(
            (UnitType.RabbitTrickster, "Transcendence/RabbitTrickster/rabbittrickster_portrait_blue.png", 1),
            (UnitType.RhinoBreaker,    "Transcendence/RhinoBreaker/rhinobreaker_portrait_blue.png",       2),
            (UnitType.BearGuard,       "Transcendence/BearGuard/bearguard_portrait_blue.png",             3)
        );
        var transAnimalARed = MakeList(
            (UnitType.RabbitTrickster, "Transcendence/RabbitTrickster/rabbittrickster_portrait_red.png", 1),
            (UnitType.RhinoBreaker,    "Transcendence/RhinoBreaker/rhinobreaker_portrait_red.png",       2),
            (UnitType.BearGuard,       "Transcendence/BearGuard/bearguard_portrait_red.png",             3)
        );
        AddMapping(listProp, ref index, BuildingType.PrimalAltar,     transAnimalABlue, transAnimalARed);
        AddMapping(listProp, ref index, BuildingType.PrimalDen,       transAnimalABlue, transAnimalARed);
        AddMapping(listProp, ref index, BuildingType.PrimalSanctuary, transAnimalABlue, transAnimalARed);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Transcendence 동물B 라인: FoxMagician(1) → EagleArcher(2) → LionKnight(3)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        var transAnimalBBlue = MakeList(
            (UnitType.FoxMagician, "Transcendence/FoxMagician/foxmagician_portrait_blue.png", 1),
            (UnitType.EagleArcher, "Transcendence/EagleArcher/eaglearcher_portrait_blue.png", 2),
            (UnitType.LionKnight,  "Transcendence/LionKnight/lionknight_portrait_blue.png",   3)
        );
        var transAnimalBRed = MakeList(
            (UnitType.FoxMagician, "Transcendence/FoxMagician/foxmagician_portrait_red.png", 1),
            (UnitType.EagleArcher, "Transcendence/EagleArcher/eaglearcher_portrait_red.png", 2),
            (UnitType.LionKnight,  "Transcendence/LionKnight/lionknight_portrait_red.png",   3)
        );
        AddMapping(listProp, ref index, BuildingType.FeralAltar,     transAnimalBBlue, transAnimalBRed);
        AddMapping(listProp, ref index, BuildingType.FeralDen,       transAnimalBBlue, transAnimalBRed);
        AddMapping(listProp, ref index, BuildingType.FeralSanctuary, transAnimalBBlue, transAnimalBRed);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Transcendence 식물 라인: MushroomBomber(1) → BloomFairy(2)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        var transPlantBlue = MakeList(
            (UnitType.MushroomBomber, "Transcendence/MushroomBomber/mushroombomber_portrait_blue.png", 1),
            (UnitType.BloomFairy,     "Transcendence/BloomFairy/bloomfairy_portrait_blue.png",         2)
        );
        var transPlantRed = MakeList(
            (UnitType.MushroomBomber, "Transcendence/MushroomBomber/mushroombomber_portrait_red.png", 1),
            (UnitType.BloomFairy,     "Transcendence/BloomFairy/bloomfairy_portrait_red.png",         2)
        );
        AddMapping(listProp, ref index, BuildingType.SporePatch,    transPlantBlue, transPlantRed);
        AddMapping(listProp, ref index, BuildingType.FloralNursery, transPlantBlue, transPlantRed);

        // ── 저장 ──────────────────────────────────────────────────────────
        so.ApplyModifiedProperties();

        // 씬 변경 사항을 Dirty 상태로 마킹 → Ctrl+S로 저장 가능
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        // Project 창에서 컴포넌트 오브젝트 하이라이트
        EditorGUIUtility.PingObject(ui.gameObject);

        Debug.Log($"[Setup] ProductionPanelUI 유닛 매핑 설정 완료. " +
                  $"총 {index}개 BuildingUnitMapping 등록. " +
                  "Inspector에서 확인 후 씬을 저장해주세요 (Ctrl+S).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 헬퍼: params 튜플을 List<(UnitType, string, int)>로 변환
    // C# params + ValueTuple 조합으로 호출부를 간결하게 유지
    // ─────────────────────────────────────────────────────────────────────────
    private static List<(UnitType type, string relativePath, int stage)> MakeList(
        params (UnitType type, string relativePath, int stage)[] entries)
    {
        return new List<(UnitType, string, int)>(entries);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 헬퍼: BuildingUnitMapping 하나를 리스트에 추가
    // ─────────────────────────────────────────────────────────────────────────
    private static void AddMapping(
        SerializedProperty listProp,
        ref int index,
        BuildingType buildingType,
        List<(UnitType type, string relativePath, int stage)> blueUnits,
        List<(UnitType type, string relativePath, int stage)> redUnits)
    {
        listProp.InsertArrayElementAtIndex(index);
        var mapping = listProp.GetArrayElementAtIndex(index);
        index++;

        // 건물 타입 설정
        mapping.FindPropertyRelative("buildingType").intValue = (int)buildingType;

        // Blue/Red 유닛 리스트 설정
        FillUnitList(mapping.FindPropertyRelative("blueUnits"), blueUnits);
        FillUnitList(mapping.FindPropertyRelative("redUnits"),  redUnits);

        Debug.Log($"[Setup] {buildingType} — blue {blueUnits.Count}개 / red {redUnits.Count}개 등록");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 헬퍼: UnitPortraitEntry 리스트 SerializedProperty를 채운다.
    // relativePath: PORTRAIT_ROOT 아래의 상대 경로
    //   예) "Human/Pistoleer/pistoleer_portrait_blue.png"
    // ─────────────────────────────────────────────────────────────────────────
    private static void FillUnitList(
        SerializedProperty listProp,
        List<(UnitType type, string relativePath, int stage)> entries)
    {
        listProp.ClearArray();

        for (int i = 0; i < entries.Count; i++)
        {
            listProp.InsertArrayElementAtIndex(i);
            var element = listProp.GetArrayElementAtIndex(i);

            // UnitType 열거형 값 설정
            element.FindPropertyRelative("type").intValue = (int)entries[i].type;

            // 해금 단계 설정 (현재 건물 단계 < requiredStage → 잠금 표시)
            element.FindPropertyRelative("requiredStage").intValue = entries[i].stage;

            // 초상화 스프라이트 로드 및 할당
            string fullPath = $"{PORTRAIT_ROOT}/{entries[i].relativePath}";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
            if (sprite == null)
                Debug.LogWarning($"[Setup] 스프라이트를 찾을 수 없습니다: {fullPath}");

            element.FindPropertyRelative("portrait").objectReferenceValue = sprite;
        }
    }
}
#endif
