// ============================================================================
// SetupUnitFactoryPrefabs.cs
// UnitFactory / BuildingFactory Inspector 필드에 종족별·팀별 프리팹을 자동으로 연결하는 에디터 유틸리티.
//
// 사용법:
//   Unity 메뉴바 → Hexiege → Setup → UnitFactory 프리팹 연결
//
// 배경:
//   UnitFactory의 Inspector 필드가 _bluePrefabs/_redPrefabs에서
//   종족별 6세트로 변경되면서 기존 프리팹 연결이 초기화됨.
//   18개 프리팹(3종족 × 2팀 × 3유닛)을 수동으로 연결하는 대신
//   이 스크립트로 한 번에 자동 연결.
//
// 프리팹 경로 규칙:
//   Assets/_Project/Prefabs/Units/{종족명}/{유닛이름}_{팀색}.prefab
//
// 종족별 유닛 매핑:
//   Pistoleer(경량 원거리): Human=Pistoleer, Spirit=EmberSpirit, Transcendence=FoxMagician
//   Assault(근접 탱커):    Human=Assault, Spirit=FlameSpirit, Transcendence=BearGuard
//   Sniper(장거리 딜러):   Human=Sniper, Spirit=InfernoSpirit, Transcendence=LionKnight
//
// 1회성 유틸리티 — 프리팹 연결 완료 후에는 삭제해도 무방.
// ============================================================================

using UnityEditor;
using UnityEngine;
using Hexiege.Infrastructure;

public class SetupUnitFactoryPrefabs
{
    /// <summary>
    /// 메뉴 아이템: Hexiege → Setup → UnitFactory 프리팹 연결.
    /// 씬에서 UnitFactory를 찾아 종족별·팀별 프리팹 필드를 자동 설정.
    /// </summary>
    [MenuItem("Hexiege/Setup/UnitFactory 프리팹 연결")]
    static void Setup()
    {
        // 씬에 배치된 UnitFactory 컴포넌트 찾기
        var factory = Object.FindObjectOfType<UnitFactory>();
        if (factory == null)
        {
            Debug.LogError("[Setup] UnitFactory를 씬에서 찾을 수 없습니다. Game 씬이 열려 있는지 확인하세요.");
            return;
        }

        // SerializedObject를 통해 private [SerializeField] 필드에 접근
        var so = new SerializedObject(factory);

        // 프리팹 폴더 경로 (종족별 하위 폴더)
        string basePath = "Assets/_Project/Prefabs/Units";

        // ====================================================================
        // Human (인간) — Pistoleer, Assault, Sniper
        // ====================================================================
        SetPrefabSet(so, "_humanBluePrefabs", basePath + "/Human",
            "Unit_Pistoleer_Blue", "Unit_Assault_Blue", "Unit_Sniper_Blue");
        SetPrefabSet(so, "_humanRedPrefabs", basePath + "/Human",
            "Unit_Pistoleer_Red", "Unit_Assault_Red", "Unit_Sniper_Red");

        // ====================================================================
        // Spirit (정령) — EmberSpirit, FlameSpirit, InfernoSpirit
        // ====================================================================
        SetPrefabSet(so, "_spiritBluePrefabs", basePath + "/Spirit",
            "Unit_EmberSpirit_Blue", "Unit_FlameSpirit_Blue", "Unit_InfernoSpirit_Blue");
        SetPrefabSet(so, "_spiritRedPrefabs", basePath + "/Spirit",
            "Unit_EmberSpirit_Red", "Unit_FlameSpirit_Red", "Unit_InfernoSpirit_Red");

        // ====================================================================
        // Transcendence (초월) — FoxMagician, BearGuard, LionKnight
        // ====================================================================
        SetPrefabSet(so, "_transcendenceBluePrefabs", basePath + "/Transcendence",
            "Unit_FoxMagician_Blue", "Unit_BearGuard_Blue", "Unit_LionKnight_Blue");
        SetPrefabSet(so, "_transcendenceRedPrefabs", basePath + "/Transcendence",
            "Unit_FoxMagician_Red", "Unit_BearGuard_Red", "Unit_LionKnight_Red");

        // 변경 사항을 Inspector에 반영
        so.ApplyModifiedProperties();

        // 씬 더티 마킹 — 저장하지 않으면 프리팹 연결이 유실됨
        EditorUtility.SetDirty(factory);
        Debug.Log("[Setup] UnitFactory 프리팹 연결 완료! 씬을 저장(Ctrl+S)해주세요.");
    }

    /// <summary>
    /// UnitTeamPrefabSet 하나의 3개 프리팹(pistoleer, assault, sniper)을 설정.
    /// </summary>
    /// <param name="so">UnitFactory의 SerializedObject</param>
    /// <param name="fieldName">Inspector 필드명 (예: "_humanBluePrefabs")</param>
    /// <param name="folder">프리팹 폴더 경로 (예: "Assets/_Project/Prefabs/Units/Human")</param>
    /// <param name="pistoleerName">Pistoleer 역할 프리팹 이름 (확장자 제외)</param>
    /// <param name="assaultName">Assault 역할 프리팹 이름 (확장자 제외)</param>
    /// <param name="sniperName">Sniper 역할 프리팹 이름 (확장자 제외)</param>
    static void SetPrefabSet(SerializedObject so, string fieldName, string folder,
        string pistoleerName, string assaultName, string sniperName)
    {
        // SerializedObject에서 해당 필드(UnitTeamPrefabSet 구조체) 찾기
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[Setup] 필드 '{fieldName}'를 찾을 수 없습니다. UnitFactory 코드를 확인하세요.");
            return;
        }

        // 구조체 내부 필드에 프리팹 할당
        // UnitTeamPrefabSet의 필드명: pistoleer, assault, sniper (소문자)
        SetPrefab(prop.FindPropertyRelative("pistoleer"), $"{folder}/{pistoleerName}.prefab");
        SetPrefab(prop.FindPropertyRelative("assault"),   $"{folder}/{assaultName}.prefab");
        SetPrefab(prop.FindPropertyRelative("sniper"),    $"{folder}/{sniperName}.prefab");
    }

    /// <summary>
    /// 개별 프리팹 참조를 설정. 프리팹을 찾지 못하면 경고 로그 출력.
    /// </summary>
    /// <param name="prop">할당할 SerializedProperty (GameObject 타입)</param>
    /// <param name="path">프리팹 에셋 경로 (예: "Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab")</param>
    static void SetPrefab(SerializedProperty prop, string path)
    {
        if (prop == null)
        {
            Debug.LogWarning($"[Setup] SerializedProperty가 null입니다. 경로: {path}");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            Debug.LogWarning($"[Setup] 프리팹을 찾을 수 없습니다: {path}");
        else
            prop.objectReferenceValue = prefab;
    }

    // ====================================================================
    // BuildingFactory 프리팹 자동 연결
    // ====================================================================

    /// <summary>
    /// 메뉴 아이템: Hexiege -> Setup -> BuildingFactory 프리팹 연결.
    /// 씬에서 BuildingFactory를 찾아 종족별·팀별 건물 프리팹 필드를 자동 설정.
    ///
    /// 건물 종족 매핑:
    ///   Castle(본진):   Human=Castle, Spirit=SpiritNexus, Transcendence=ElderTree
    ///   Barracks(병영): Human=Barracks, Spirit=SummoningAltar, Transcendence=HunterPlant
    ///   MiningPost:     Human=MiningPost(팀 공용), Spirit=ManaRift, Transcendence=FungalNode
    /// </summary>
    [MenuItem("Hexiege/Setup/BuildingFactory 프리팹 연결")]
    static void SetupBuildingFactory()
    {
        // 씬에 배치된 BuildingFactory 컴포넌트 찾기
        var factory = Object.FindObjectOfType<BuildingFactory>();
        if (factory == null)
        {
            Debug.LogError("[Setup] BuildingFactory를 씬에서 찾을 수 없습니다. Game 씬이 열려 있는지 확인하세요.");
            return;
        }

        // SerializedObject를 통해 private [SerializeField] 필드에 접근
        var so = new SerializedObject(factory);

        // 프리팹 폴더 경로 (종족별 하위 폴더)
        string basePath = "Assets/_Project/Prefabs/Buildings";

        // ====================================================================
        // Human (인간) — Castle, Barracks, MiningPost(팀 공용 동일 프리팹)
        // ====================================================================
        SetBuildingPrefabSet(so, "_humanBluePrefabs", basePath + "/Human",
            "Building_Castle_Blue", "Building_Barracks_Blue", "Building_MiningPost");
        SetBuildingPrefabSet(so, "_humanRedPrefabs", basePath + "/Human",
            "Building_Castle_Red", "Building_Barracks_Red", "Building_MiningPost");

        // ====================================================================
        // Spirit (정령) — SpiritNexus(=Castle), SummoningAltar(=Barracks), ManaRift(=MiningPost)
        // ====================================================================
        SetBuildingPrefabSet(so, "_spiritBluePrefabs", basePath + "/Spirit",
            "Building_SpiritNexus_Blue", "Building_SummoningAltar_Blue", "Building_ManaRift_Blue");
        SetBuildingPrefabSet(so, "_spiritRedPrefabs", basePath + "/Spirit",
            "Building_SpiritNexus_Red", "Building_SummoningAltar_Red", "Building_ManaRift_Red");

        // ====================================================================
        // Transcendence (초월) — ElderTree(=Castle), HunterPlant(=Barracks), FungalNode(=MiningPost)
        // ====================================================================
        SetBuildingPrefabSet(so, "_transcendenceBluePrefabs", basePath + "/Transcendence",
            "Building_ElderTree_Blue", "Building_HunterPlant_Blue", "Building_FungalNode_Blue");
        SetBuildingPrefabSet(so, "_transcendenceRedPrefabs", basePath + "/Transcendence",
            "Building_ElderTree_Red", "Building_HunterPlant_Red", "Building_FungalNode_Red");

        // 변경 사항을 Inspector에 반영
        so.ApplyModifiedProperties();

        // 씬 더티 마킹 — 저장하지 않으면 프리팹 연결이 유실됨
        EditorUtility.SetDirty(factory);
        Debug.Log("[Setup] BuildingFactory 프리팹 연결 완료! 씬을 저장(Ctrl+S)해주세요.");
    }

    /// <summary>
    /// BuildingTeamPrefabSet 하나의 3개 프리팹(castle, barracks, miningPost)을 설정.
    /// </summary>
    /// <param name="so">BuildingFactory의 SerializedObject</param>
    /// <param name="fieldName">Inspector 필드명 (예: "_humanBluePrefabs")</param>
    /// <param name="folder">프리팹 폴더 경로 (예: "Assets/_Project/Prefabs/Buildings/Human")</param>
    /// <param name="castleName">Castle 역할 프리팹 이름 (확장자 제외)</param>
    /// <param name="barracksName">Barracks 역할 프리팹 이름 (확장자 제외)</param>
    /// <param name="miningPostName">MiningPost 역할 프리팹 이름 (확장자 제외)</param>
    static void SetBuildingPrefabSet(SerializedObject so, string fieldName, string folder,
        string castleName, string barracksName, string miningPostName)
    {
        // SerializedObject에서 해당 필드(BuildingTeamPrefabSet 구조체) 찾기
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[Setup] 필드 '{fieldName}'를 찾을 수 없습니다. BuildingFactory 코드를 확인하세요.");
            return;
        }

        // 구조체 내부 필드에 프리팹 할당
        // BuildingTeamPrefabSet의 필드명: castle, barracks, miningPost (소문자)
        SetPrefab(prop.FindPropertyRelative("castle"),     $"{folder}/{castleName}.prefab");
        SetPrefab(prop.FindPropertyRelative("barracks"),   $"{folder}/{barracksName}.prefab");
        SetPrefab(prop.FindPropertyRelative("miningPost"), $"{folder}/{miningPostName}.prefab");
    }
}
