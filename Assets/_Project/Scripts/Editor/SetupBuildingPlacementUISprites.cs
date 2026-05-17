// ============================================================================
// SetupBuildingPlacementUISprites.cs
// BuildingPlacementUI 컴포넌트의 6개 종족/팀별 건물 리스트에
// BuildingType과 아이콘 스프라이트를 자동 할당하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/BuildingPlacementUI 스프라이트 설정
//
// 실행 전 준비:
//   - Game.unity 씬을 열어둘 것 (BuildingPlacementUI 컴포넌트가 있는 씬)
//
// 할당 기준:
//   - 배치 패널에는 각 라인의 1단계 건물 + MiningPost만 포함
//     (2·3단계 업그레이드는 건물 클릭 후 업그레이드 버튼으로 진행)
//   - MiningPost는 세 종족 모두 BuildingType.MiningPost 사용
//     (에셋명은 종족별로 다르지만 코드 타입은 동일)
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

public static class SetupBuildingPlacementUISprites
{
    [MenuItem("Hexiege/Setup/BuildingPlacementUI 스프라이트 설정")]
    private static void Setup()
    {
        // ── Scene에서 BuildingPlacementUI 컴포넌트 탐색 ─────────────────────
        var ui = Object.FindFirstObjectByType<BuildingPlacementUI>();
        if (ui == null)
        {
            Debug.LogError("[Setup] BuildingPlacementUI 컴포넌트를 씬에서 찾을 수 없습니다. " +
                           "Game.unity 씬을 열고 다시 실행해주세요.");
            return;
        }

        // SerializedObject를 통해 수정하면 Undo 등록 + 씬 Dirty 마킹이 자동 처리됨
        var so = new SerializedObject(ui);
        so.Update();

        // ── Human ────────────────────────────────────────────────────────────
        AssignList(so, "_blueHumanBuildings", new List<(BuildingType, string)>
        {
            // 생산 건물 (1단계만)
            (BuildingType.TrainingCamp,   "Assets/_Project/Sprites/Buildings/Human/bld_trainingcamp_blue.png"),
            (BuildingType.Gunsmith,       "Assets/_Project/Sprites/Buildings/Human/bld_gunsmith_blue.png"),
            (BuildingType.Garage,         "Assets/_Project/Sprites/Buildings/Human/bld_garage_blue.png"),
            // 비생산 건물
            (BuildingType.AutoTower,      "Assets/_Project/Sprites/Buildings/Human/bld_cannontower_blue.png"),
            (BuildingType.FlightFacility, "Assets/_Project/Sprites/Buildings/Human/bld_flightfacility_blue.png"),
            (BuildingType.Research,       "Assets/_Project/Sprites/Buildings/Human/bld_technicallaboratory_blue.png"),
            // 채굴소
            (BuildingType.MiningPost,     "Assets/_Project/Sprites/Buildings/Human/bld_miningpost_blue.png"),
        });

        AssignList(so, "_redHumanBuildings", new List<(BuildingType, string)>
        {
            (BuildingType.TrainingCamp,   "Assets/_Project/Sprites/Buildings/Human/bld_trainingcamp_red.png"),
            (BuildingType.Gunsmith,       "Assets/_Project/Sprites/Buildings/Human/bld_gunsmith_red.png"),
            (BuildingType.Garage,         "Assets/_Project/Sprites/Buildings/Human/bld_garage_red.png"),
            (BuildingType.AutoTower,      "Assets/_Project/Sprites/Buildings/Human/bld_cannontower_red.png"),
            (BuildingType.FlightFacility, "Assets/_Project/Sprites/Buildings/Human/bld_flightfacility_red.png"),
            (BuildingType.Research,       "Assets/_Project/Sprites/Buildings/Human/bld_technicallaboratory_red.png"),
            (BuildingType.MiningPost,     "Assets/_Project/Sprites/Buildings/Human/bld_miningpost_red.png"),
        });

        // ── Spirit ───────────────────────────────────────────────────────────
        AssignList(so, "_blueSpiritBuildings", new List<(BuildingType, string)>
        {
            // 생산 건물 (1단계만)
            (BuildingType.FireSpire,     "Assets/_Project/Sprites/Buildings/Spirit/bld_firespire_blue.png"),
            (BuildingType.AquaSpring,    "Assets/_Project/Sprites/Buildings/Spirit/bld_aquaspring_blue.png"),
            (BuildingType.StoneMound,    "Assets/_Project/Sprites/Buildings/Spirit/bld_stonemound_blue.png"),
            // 비생산 건물
            (BuildingType.AutoTower,     "Assets/_Project/Sprites/Buildings/Spirit/bld_runespire_blue.png"),
            (BuildingType.MagicBuilding, "Assets/_Project/Sprites/Buildings/Spirit/bld_magicspirit_blue.png"),
            (BuildingType.Research,      "Assets/_Project/Sprites/Buildings/Spirit/bld_astronomicalspirit_blue.png"),
            // 채굴소 (ManaRift) — BuildingType은 MiningPost
            (BuildingType.MiningPost,    "Assets/_Project/Sprites/Buildings/Spirit/bld_manarift_blue.png"),
        });

        AssignList(so, "_redSpiritBuildings", new List<(BuildingType, string)>
        {
            (BuildingType.FireSpire,     "Assets/_Project/Sprites/Buildings/Spirit/bld_firespire_red.png"),
            (BuildingType.AquaSpring,    "Assets/_Project/Sprites/Buildings/Spirit/bld_aquaspring_red.png"),
            (BuildingType.StoneMound,    "Assets/_Project/Sprites/Buildings/Spirit/bld_stonemound_red.png"),
            (BuildingType.AutoTower,     "Assets/_Project/Sprites/Buildings/Spirit/bld_runespire_red.png"),
            (BuildingType.MagicBuilding, "Assets/_Project/Sprites/Buildings/Spirit/bld_magicspirit_red.png"),
            (BuildingType.Research,      "Assets/_Project/Sprites/Buildings/Spirit/bld_astronomicalspirit_red.png"),
            (BuildingType.MiningPost,    "Assets/_Project/Sprites/Buildings/Spirit/bld_manarift_red.png"),
        });

        // ── Transcendence ────────────────────────────────────────────────────
        AssignList(so, "_blueTranscendenceBuildings", new List<(BuildingType, string)>
        {
            // 생산 건물 (1단계만)
            (BuildingType.PrimalAltar,   "Assets/_Project/Sprites/Buildings/Transcendence/bld_primalaltar_blue.png"),
            (BuildingType.FeralAltar,    "Assets/_Project/Sprites/Buildings/Transcendence/bld_feralaltar_blue.png"),
            (BuildingType.SporePatch,    "Assets/_Project/Sprites/Buildings/Transcendence/bld_sporepatch_blue.png"),
            // 비생산 건물
            (BuildingType.AutoTower,     "Assets/_Project/Sprites/Buildings/Transcendence/bld_vinetower_blue.png"),
            (BuildingType.MagicBuilding, "Assets/_Project/Sprites/Buildings/Transcendence/bld_willowshrine_blue.png"),
            (BuildingType.Research,      "Assets/_Project/Sprites/Buildings/Transcendence/bld_ancientgrove_blue.png"),
            (BuildingType.HealShrine,    "Assets/_Project/Sprites/Buildings/Transcendence/bld_mistshrine_blue.png"),
            // 채굴소 (FungalNode) — BuildingType은 MiningPost
            (BuildingType.MiningPost,    "Assets/_Project/Sprites/Buildings/Transcendence/bld_fungalnode_blue.png"),
        });

        AssignList(so, "_redTranscendenceBuildings", new List<(BuildingType, string)>
        {
            (BuildingType.PrimalAltar,   "Assets/_Project/Sprites/Buildings/Transcendence/bld_primalaltar_red.png"),
            (BuildingType.FeralAltar,    "Assets/_Project/Sprites/Buildings/Transcendence/bld_feralaltar_red.png"),
            (BuildingType.SporePatch,    "Assets/_Project/Sprites/Buildings/Transcendence/bld_sporepatch_red.png"),
            (BuildingType.AutoTower,     "Assets/_Project/Sprites/Buildings/Transcendence/bld_vinetower_red.png"),
            (BuildingType.MagicBuilding, "Assets/_Project/Sprites/Buildings/Transcendence/bld_willowshrine_red.png"),
            (BuildingType.Research,      "Assets/_Project/Sprites/Buildings/Transcendence/bld_ancientgrove_red.png"),
            (BuildingType.HealShrine,    "Assets/_Project/Sprites/Buildings/Transcendence/bld_mistshrine_red.png"),
            (BuildingType.MiningPost,    "Assets/_Project/Sprites/Buildings/Transcendence/bld_fungalnode_red.png"),
        });

        so.ApplyModifiedProperties();

        // 씬 변경 사항을 Dirty 상태로 마킹 → Ctrl+S로 저장 가능
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        // Project 창에서 컴포넌트 오브젝트 하이라이트
        EditorGUIUtility.PingObject(ui.gameObject);

        Debug.Log("[Setup] BuildingPlacementUI 스프라이트 설정 완료. " +
                  "Inspector에서 확인 후 씬을 저장해주세요 (Ctrl+S).");
    }

    /// <summary>
    /// SerializedObject에서 지정된 리스트 프로퍼티를 초기화하고 entries로 채운다.
    /// </summary>
    private static void AssignList(SerializedObject so, string propName,
        List<(BuildingType type, string spritePath)> entries)
    {
        var listProp = so.FindProperty(propName);
        if (listProp == null)
        {
            Debug.LogWarning($"[Setup] 프로퍼티 '{propName}'를 찾을 수 없습니다. " +
                             "필드명이 변경됐는지 확인해주세요.");
            return;
        }

        // 기존 항목 전체 제거 후 재삽입
        listProp.ClearArray();

        for (int i = 0; i < entries.Count; i++)
        {
            listProp.InsertArrayElementAtIndex(i);
            var element = listProp.GetArrayElementAtIndex(i);

            // BuildingType 열거형 값 설정 (intValue = 선언 순서 기준 정수값)
            element.FindPropertyRelative("type").intValue = (int)entries[i].type;

            // 스프라이트 로드 및 할당
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entries[i].spritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Setup] 스프라이트를 찾을 수 없습니다: {entries[i].spritePath}");
            }
            element.FindPropertyRelative("icon").objectReferenceValue = sprite;
        }

        Debug.Log($"[Setup] {propName} — {entries.Count}개 항목 설정 완료");
    }
}
#endif
