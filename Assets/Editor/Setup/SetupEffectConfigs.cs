// ============================================================================
// SetupEffectConfigs.cs (Editor 전용, 1회성 도구)
// EffectManager 시스템이 사용하는 3종 Config 에셋을 자동 생성하는 메뉴.
//
// 생성되는 에셋:
//   1) Assets/_Project/Resources/Config/UnitEffectConfig.asset
//      → 25종 UnitType 전체에 빈 Entry 추가 (프리셋은 Inspector에서 수동 연결).
//   2) Assets/_Project/Resources/Config/BuildingEffectConfig.asset
//      → 주요 건물(Castle, MiningPost, AutoTower)에만 빈 Entry 추가.
//   3) Assets/_Project/Resources/Config/UiEffectConfig.asset
//      → UiEffectKey 7종 전체에 빈 Entry 추가.
//
// 사용법:
//   Unity 상단 메뉴에서 아래 항목을 각각 실행.
//     Hexiege/Setup/UnitEffectConfig 생성
//     Hexiege/Setup/BuildingEffectConfig 생성
//     Hexiege/Setup/UiEffectConfig 생성
//
// 구현 메모:
//   Config의 _entries 필드는 private이므로 SerializedObject로 안전하게 채운다.
//   (리플렉션 직접 접근보다 Inspector 직렬화와 일관성이 높다.)
//   이미 같은 경로에 에셋이 있으면 덮어쓰지 않고 경고만 출력한다.
//
// 이 스크립트는 1회성 도구이므로 에셋 생성 후 삭제해도 무방하다.
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 이펙트 Config 3종을 자동 생성하는 Editor 메뉴 모음.
    /// </summary>
    public static class SetupEffectConfigs
    {
        // Config 에셋이 저장될 폴더 (Resources 하위여야 런타임 Resources.Load 가능).
        private const string ConfigFolder = "Assets/_Project/Resources/Config";

        // ====================================================================
        // 1) UnitEffectConfig 생성
        // ====================================================================

        [MenuItem("Hexiege/Setup/UnitEffectConfig 생성")]
        public static void CreateUnitEffectConfig()
        {
            string path = $"{ConfigFolder}/UnitEffectConfig.asset";
            if (!EnsureCanCreate(path)) return;

            UnitEffectConfig config = ScriptableObject.CreateInstance<UnitEffectConfig>();

            // SerializedObject로 private _entries 리스트에 25종 UnitType 빈 Entry 채우기.
            SerializedObject so = new SerializedObject(config);
            SerializedProperty entries = so.FindProperty("_entries");
            entries.ClearArray();

            // 25종 UnitType — Research 문서 기준 enum 값.
            UnitType[] allTypes =
            {
                UnitType.Pistoleer, UnitType.Assault, UnitType.Sniper, UnitType.LittleKnight,
                UnitType.SpearMan, UnitType.BattleAxe, UnitType.Tank, UnitType.CannonCart,
                UnitType.FlameSpirit, UnitType.EmberSpirit, UnitType.InfernoSpirit, UnitType.DustSpirit,
                UnitType.BoulderSpirit, UnitType.QuakeSpirit, UnitType.TideSpirit, UnitType.StreamSpirit,
                UnitType.TorrentSpirit, UnitType.BearGuard, UnitType.FoxMagician, UnitType.LionKnight,
                UnitType.RhinoBreaker, UnitType.EagleArcher, UnitType.RabbitTrickster,
                UnitType.MushroomBomber, UnitType.BloomFairy,
            };

            for (int i = 0; i < allTypes.Length; i++)
            {
                entries.InsertArrayElementAtIndex(i);
                SerializedProperty elem = entries.GetArrayElementAtIndex(i);
                // 구조체 필드 이름은 UnitEffectEntry의 public 필드명과 동일.
                elem.FindPropertyRelative("unitType").enumValueIndex = EnumIndexOf(elem.FindPropertyRelative("unitType"), (int)allTypes[i]);
                elem.FindPropertyRelative("attackPreset").objectReferenceValue = null;
                elem.FindPropertyRelative("deathPreset").objectReferenceValue = null;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAsset(config, path);
            Debug.Log($"[SetupEffectConfigs] UnitEffectConfig 생성 완료 ({allTypes.Length}종 유닛): {path}");
        }

        // ====================================================================
        // 2) BuildingEffectConfig 생성
        // ====================================================================

        [MenuItem("Hexiege/Setup/BuildingEffectConfig 생성")]
        public static void CreateBuildingEffectConfig()
        {
            string path = $"{ConfigFolder}/BuildingEffectConfig.asset";
            if (!EnsureCanCreate(path)) return;

            BuildingEffectConfig config = ScriptableObject.CreateInstance<BuildingEffectConfig>();

            SerializedObject so = new SerializedObject(config);
            SerializedProperty entries = so.FindProperty("_entries");
            entries.ClearArray();

            // 주요 비생산 건물만 기본 추가. 나머지는 Inspector에서 수동 추가 가능.
            BuildingType[] mainTypes =
            {
                BuildingType.Castle, BuildingType.MiningPost, BuildingType.AutoTower,
            };

            for (int i = 0; i < mainTypes.Length; i++)
            {
                entries.InsertArrayElementAtIndex(i);
                SerializedProperty elem = entries.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("buildingType").enumValueIndex = EnumIndexOf(elem.FindPropertyRelative("buildingType"), (int)mainTypes[i]);
                elem.FindPropertyRelative("destroyPreset").objectReferenceValue = null;
                elem.FindPropertyRelative("upgradePreset").objectReferenceValue = null;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAsset(config, path);
            Debug.Log($"[SetupEffectConfigs] BuildingEffectConfig 생성 완료 ({mainTypes.Length}종 건물): {path}");
        }

        // ====================================================================
        // 3) UiEffectConfig 생성
        // ====================================================================

        [MenuItem("Hexiege/Setup/UiEffectConfig 생성")]
        public static void CreateUiEffectConfig()
        {
            string path = $"{ConfigFolder}/UiEffectConfig.asset";
            if (!EnsureCanCreate(path)) return;

            UiEffectConfig config = ScriptableObject.CreateInstance<UiEffectConfig>();

            SerializedObject so = new SerializedObject(config);
            SerializedProperty entries = so.FindProperty("_entries");
            entries.ClearArray();

            // UiEffectKey 7종 전체.
            UiEffectKey[] allKeys =
            {
                UiEffectKey.ButtonClick, UiEffectKey.ButtonConfirm, UiEffectKey.ButtonCancel,
                UiEffectKey.PanelOpen, UiEffectKey.PanelClose,
                UiEffectKey.GoldGain, UiEffectKey.TimerComplete,
            };

            for (int i = 0; i < allKeys.Length; i++)
            {
                entries.InsertArrayElementAtIndex(i);
                SerializedProperty elem = entries.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("key").enumValueIndex = EnumIndexOf(elem.FindPropertyRelative("key"), (int)allKeys[i]);
                elem.FindPropertyRelative("preset").objectReferenceValue = null;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAsset(config, path);
            Debug.Log($"[SetupEffectConfigs] UiEffectConfig 생성 완료 ({allKeys.Length}종 UI 키): {path}");
        }

        // ====================================================================
        // 공통 헬퍼
        // ====================================================================

        /// <summary>
        /// enum SerializedProperty에서 "정수 값(enumValue)"에 해당하는 "선택 인덱스(enumValueIndex)"를 찾는다.
        /// UnitType/BuildingType은 enum 값이 연속적이지 않으므로(예: 0,1,...,7,10,...),
        /// 정수 값을 그대로 enumValueIndex에 넣으면 안 되고, enumNames 목록에서의 위치를 찾아야 한다.
        /// </summary>
        /// <param name="prop">대상 enum SerializedProperty.</param>
        /// <param name="enumIntValue">설정하려는 enum의 정수 값.</param>
        /// <returns>enumValueIndex에 넣을 선택 인덱스. 못 찾으면 0.</returns>
        private static int EnumIndexOf(SerializedProperty prop, int enumIntValue)
        {
            // prop.enumValueIndex에 후보 값을 하나씩 대입해 보며 일치하는 인덱스를 찾는다.
            // enumNames.Length만큼 순회하면 모든 후보를 검사할 수 있다.
            int count = prop.enumNames.Length;
            for (int idx = 0; idx < count; idx++)
            {
                prop.enumValueIndex = idx;
                if (prop.intValue == enumIntValue)
                    return idx;
            }
            return 0;
        }

        /// <summary>
        /// 대상 경로에 에셋을 만들 수 있는지 검사. 폴더가 없으면 생성하고,
        /// 이미 같은 경로에 에셋이 있으면 덮어쓰기를 막기 위해 false 반환.
        /// </summary>
        private static bool EnsureCanCreate(string assetPath)
        {
            // Resources/Config 폴더가 없으면 만든다.
            string absFolder = Path.Combine(UnityEngine.Application.dataPath, "..", ConfigFolder);
            if (!Directory.Exists(absFolder))
                Directory.CreateDirectory(absFolder);

            if (File.Exists(Path.Combine(UnityEngine.Application.dataPath, "..", assetPath)))
            {
                Debug.LogWarning($"[SetupEffectConfigs] 이미 존재하여 생성을 건너뜁니다: {assetPath}\n" +
                                 "덮어쓰려면 기존 에셋을 먼저 삭제하세요.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// ScriptableObject를 지정 경로에 저장하고 Project 창에서 선택 상태로 만든다.
        /// </summary>
        private static void SaveAsset(ScriptableObject asset, string path)
        {
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
