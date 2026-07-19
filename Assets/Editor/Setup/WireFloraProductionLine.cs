// ============================================================================
// WireFloraProductionLine.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  상단 메뉴  Hexiege > Setup > Wire Flora Production Line (Game)  실행.      │
// │  (Game 씬이 열려 있지 않아도 됨 — 없으면 스크립트가 자동으로 열고 저장한다.)  │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가:
//   식물(Flora) 종족의 "생산 라인 노출"을 담당하는 데이터를 씬에 멱등 배선한다.
//   실제 생산 목록은 ProductionPanelUI 컴포넌트의 private [SerializeField]
//   _buildingUnitMappings(List<BuildingUnitMapping>)에 직렬화돼 있다. 이 스크립트는
//   그 리스트를 SerializedObject 로 열어 아래 두 건을 보장한다.
//
//     · SporePatch(BuildingType 30, 식물 1단계)  → MushroomBomber(UnitType 26, requiredStage 1)
//     · FloralNursery(BuildingType 31, 식물 2단계) → BloomFairy(UnitType 27, requiredStage 2)
//
//   각 유닛은 blueUnits / redUnits 양쪽에 팀별 포트레잇과 함께 배선된다.
//   (BloomFairy 배선으로 BloomFairy 생산 노출도 함께 완성된다.)
//
//   처리 규칙(멱등):
//     1) 대상 BuildingType 의 BuildingUnitMapping 항목이 리스트에 없으면
//        → 새 BuildingUnitMapping 항목을 추가하고 blueUnits/redUnits 를 비운 뒤 채운다.
//     2) 항목이 이미 있으면 그 항목을 재사용한다(기존 다른 유닛 엔트리는 건드리지 않음 — 비파괴).
//     3) 팀별 유닛 리스트에서 대상 UnitType 엔트리가 없으면 → 끝에 추가한다.
//        있으면 → requiredStage / portrait 만 점검해 필요 시 보정한다(정상이면 no-op).
//
// 왜 필요한가:
//   생산 패널(ProductionPanelUI)은 건물을 클릭했을 때 _buildingUnitMappings 에서
//   해당 BuildingType 의 유닛 라인업을 조회해 버튼에 노출한다. 이 매핑이 없으면
//   식물 건물을 눌러도 MushroomBomber / BloomFairy 가 생산 목록에 보이지 않는다.
//   이 데이터는 씬 직렬화(오브젝트 참조 = 포트레잇 Sprite GUID)라 반드시 Unity
//   에디터 런타임(SerializedObject) 에서 배선해야 한다 → 그래서 에디터 스크립트다.
//
// 멱등성:
//   여러 번 실행해도 안전하다. 매핑/엔트리가 이미 올바르면 no-op 이며,
//   누락됐거나 포트레잇/단계가 어긋난 경우에만 해당 부분만 보정한다.
//   기존에 배선된 다른 유닛 엔트리(예: 같은 건물의 추가 라인업)는 삭제하지 않는다.
//
// ⚠️ 컴포넌트 특정:
//   반드시 FindFirstObjectByType<ProductionPanelUI>() 로 씬의 생산 패널 컴포넌트를
//   특정한다. _buildingUnitMappings 는 ProductionPanelUI 고유 필드다.
//
// ⚠️ enum 직렬화:
//   BuildingType / UnitType 은 값이 비연속(예: …,29,30,31 / …,26,27)이므로,
//   SerializedProperty 에서 enumValueIndex(오디널)가 아닌 intValue 로 읽고/쓴다.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Domain;       // UnitType, BuildingType
using Hexiege.Presentation; // ProductionPanelUI

namespace Hexiege.EditorTools
{
    /// <summary>
    /// ProductionPanelUI._buildingUnitMappings 에 식물 라인(SporePatch→MushroomBomber,
    /// FloralNursery→BloomFairy)을 멱등하게 배선하는 1회성 에디터 스크립트.
    /// </summary>
    public static class WireFloraProductionLine
    {
        // ProductionPanelUI 가 배치된 씬. 열려 있지 않으면 이 씬을 연다.
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // ── ProductionPanelUI / 구조체 필드명(정확히 일치해야 한다) ──────────
        private const string MappingsFieldName          = "_buildingUnitMappings"; // List<BuildingUnitMapping>
        private const string MappingBuildingTypeField   = "buildingType";          // BuildingUnitMapping.buildingType
        private const string MappingBlueUnitsField       = "blueUnits";            // BuildingUnitMapping.blueUnits
        private const string MappingRedUnitsField        = "redUnits";             // BuildingUnitMapping.redUnits
        private const string EntryTypeField              = "type";                 // UnitPortraitEntry.type
        private const string EntryPortraitField          = "portrait";             // UnitPortraitEntry.portrait
        private const string EntryRequiredStageField     = "requiredStage";        // UnitPortraitEntry.requiredStage

        // ── 배선할 유닛 스펙(팀별 포트레잇 경로 포함) ────────────────────────
        private readonly struct UnitSpec
        {
            public readonly UnitType Type;
            public readonly int RequiredStage;
            public readonly string BluePortraitPath;
            public readonly string RedPortraitPath;

            public UnitSpec(UnitType type, int requiredStage, string bluePortrait, string redPortrait)
            {
                Type = type;
                RequiredStage = requiredStage;
                BluePortraitPath = bluePortrait;
                RedPortraitPath = redPortrait;
            }
        }

        // ── 배선할 건물 스펙(건물 → 노출할 유닛 목록) ────────────────────────
        private readonly struct BuildingSpec
        {
            public readonly BuildingType Building;
            public readonly UnitSpec[] Units;

            public BuildingSpec(BuildingType building, UnitSpec[] units)
            {
                Building = building;
                Units = units;
            }
        }

        // 포트레잇 경로 상수.
        private const string MushroomBlue =
            "Assets/_Project/Sprites/Units/Transcendence/MushroomBomber/mushroombomber_portrait_blue.png";
        private const string MushroomRed =
            "Assets/_Project/Sprites/Units/Transcendence/MushroomBomber/mushroombomber_portrait_red.png";
        private const string BloomBlue =
            "Assets/_Project/Sprites/Units/Transcendence/BloomFairy/bloomfairy_portrait_blue.png";
        private const string BloomRed =
            "Assets/_Project/Sprites/Units/Transcendence/BloomFairy/bloomfairy_portrait_red.png";

        // 배선 대상 정의(요청 범위 그대로 — SporePatch=MushroomBomber, FloralNursery=BloomFairy).
        // 비파괴 방식이라 같은 건물에 이미 배선된 다른 유닛(예: 식물 라인 전체)은 삭제하지 않는다.
        private static readonly BuildingSpec[] Specs =
        {
            new BuildingSpec(BuildingType.SporePatch, new[]
            {
                new UnitSpec(UnitType.MushroomBomber, 1, MushroomBlue, MushroomRed),
            }),
            new BuildingSpec(BuildingType.FloralNursery, new[]
            {
                new UnitSpec(UnitType.BloomFairy, 2, BloomBlue, BloomRed),
            }),
        };

        /// <summary>
        /// 메뉴 진입점. Game 씬의 ProductionPanelUI 에 식물 라인 생산 매핑을 멱등 배선한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Wire Flora Production Line (Game)")]
        public static void Run()
        {
            // ── 1) ProductionPanelUI 확보(열린 씬 우선, 없으면 Game 씬을 연다) ──
            bool sceneWasOpenedByThis = false;
            ProductionPanelUI panel = FindPanelInOpenScenes();
            if (panel == null)
            {
                panel = OpenGameSceneAndFindPanel(out sceneWasOpenedByThis);
            }

            if (panel == null)
            {
                Debug.LogWarning(
                    "[Setup] ProductionPanelUI 를 씬에서 찾지 못해 생산 라인을 배선하지 못했습니다.\n" +
                    "  해결: Game 씬을 열고 다시 실행하거나, Inspector 에서 수동으로 배선하세요.");
                return;
            }

            // ── 2) 매핑 리스트 확보 ───────────────────────────────────────────
            var so = new SerializedObject(panel);
            SerializedProperty mappingsProp = so.FindProperty(MappingsFieldName);
            if (mappingsProp == null || !mappingsProp.isArray)
            {
                Debug.LogError(
                    $"[Setup] ProductionPanelUI 에 '{MappingsFieldName}' 리스트 필드가 없습니다. " +
                    "필드명이 바뀌었는지 확인하세요.");
                return;
            }

            // ── 3) 스펙 순회 배선 ────────────────────────────────────────────
            var report = new System.Text.StringBuilder();
            bool anyChanged = false;
            bool anyError = false;

            foreach (var spec in Specs)
            {
                SerializedProperty mapping = FindOrCreateMapping(mappingsProp, (int)spec.Building, out bool mappingCreated);
                if (mapping == null)
                {
                    anyError = true;
                    report.AppendLine($"  · {spec.Building}({(int)spec.Building}): 매핑 필드 접근 실패(에러)");
                    continue;
                }
                if (mappingCreated)
                {
                    anyChanged = true;
                    report.AppendLine($"  · {spec.Building}({(int)spec.Building}): 신규 BuildingUnitMapping 항목 추가");
                }

                SerializedProperty blueList = mapping.FindPropertyRelative(MappingBlueUnitsField);
                SerializedProperty redList = mapping.FindPropertyRelative(MappingRedUnitsField);
                if (blueList == null || redList == null || !blueList.isArray || !redList.isArray)
                {
                    anyError = true;
                    report.AppendLine($"  · {spec.Building}({(int)spec.Building}): blueUnits/redUnits 필드 접근 실패(에러)");
                    continue;
                }

                foreach (var unit in spec.Units)
                {
                    // 팀별 포트레잇 로드(단일 스프라이트 모드 → LoadAssetAtPath<Sprite> 로 직접 로드).
                    Sprite bluePortrait = LoadPortrait(unit.BluePortraitPath);
                    Sprite redPortrait = LoadPortrait(unit.RedPortraitPath);

                    WireResult blueResult = EnsureUnitEntry(blueList, unit, bluePortrait);
                    WireResult redResult = EnsureUnitEntry(redList, unit, redPortrait);

                    if (blueResult == WireResult.Added || blueResult == WireResult.Repaired
                        || redResult == WireResult.Added || redResult == WireResult.Repaired)
                    {
                        anyChanged = true;
                    }

                    string portraitNote =
                        (bluePortrait == null ? " [blue 포트레잇 없음]" : "") +
                        (redPortrait == null ? " [red 포트레잇 없음]" : "");
                    report.AppendLine(
                        $"      → {unit.Type}({(int)unit.Type}, req{unit.RequiredStage}): " +
                        $"blue={blueResult}, red={redResult}{portraitNote}");
                }
            }

            // ── 4) 적용 / Dirty / 씬 저장 ────────────────────────────────────
            if (anyChanged)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(panel);
                EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
                if (sceneWasOpenedByThis)
                {
                    EditorSceneManager.SaveScene(panel.gameObject.scene);
                }
            }

            // ── 5) 결과 리포트 ───────────────────────────────────────────────
            string saveState = !anyChanged
                ? "변경 없음 — 이미 배선됨(저장 불필요)"
                : (sceneWasOpenedByThis
                    ? "씬 자동 저장 완료"
                    : "열린 씬 dirty 표시 — Ctrl+S 로 저장하세요");

            string header = anyError
                ? "[Setup] 식물 생산 라인 배선 — 일부 실패(위 로그 확인)."
                : "[Setup] 식물 생산 라인 배선 완료.";

            Debug.Log($"{header}\n{report}  · 씬: {saveState}");

            Selection.activeObject = panel;
            EditorGUIUtility.PingObject(panel);
        }

        // ====================================================================
        // 포트레잇 로드
        // ====================================================================

        /// <summary>
        /// 지정 경로의 Sprite 를 로드한다. 없으면 경고 후 null 반환(엔트리는 포트레잇 null 로 배선).
        /// </summary>
        private static Sprite LoadPortrait(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[Setup] 포트레잇 스프라이트를 찾지 못했습니다(포트레잇 null 로 배선): {path}");
            }
            return sprite;
        }

        // ====================================================================
        // ProductionPanelUI 탐색 / 씬 열기
        // ====================================================================

        /// <summary>
        /// 현재 열려 있는 씬(들)에서 ProductionPanelUI 를 찾는다(비활성 포함). 없으면 null.
        /// </summary>
        private static ProductionPanelUI FindPanelInOpenScenes()
        {
            return Object.FindFirstObjectByType<ProductionPanelUI>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Game 씬을 (단독 모드로) 열어 ProductionPanelUI 를 찾는다.
        /// 성공 시 <paramref name="opened"/>=true 로 알려 호출부가 자동 저장하도록 한다.
        /// </summary>
        private static ProductionPanelUI OpenGameSceneAndFindPanel(out bool opened)
        {
            opened = false;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                Debug.LogWarning($"[Setup] Game 씬을 찾지 못했습니다: {GameScenePath}");
                return null;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Setup] 씬 열기가 취소되었습니다(저장 대화 취소). 배선을 건너뜁니다.");
                return null;
            }

            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            opened = true;
            return FindPanelInOpenScenes();
        }

        // ====================================================================
        // 매핑 / 엔트리 배선
        // ====================================================================

        /// <summary>
        /// EnsureUnitEntry 의 처리 결과.
        /// </summary>
        private enum WireResult
        {
            FieldMissing, // 엔트리 내부 필드를 찾지 못함(에러)
            Added,        // 유닛 엔트리를 새로 추가함
            Repaired,     // 기존 엔트리의 portrait/requiredStage 를 보정함
            AlreadyOk     // 이미 올바름 — 변경 없음
        }

        /// <summary>
        /// _buildingUnitMappings 에서 buildingType == targetInt 인 BuildingUnitMapping 요소를 찾는다.
        /// 없으면 리스트 끝에 새 항목을 추가하고 buildingType 설정 + blueUnits/redUnits 를 비운다.
        /// (InsertArrayElementAtIndex 는 직전 요소를 복제하므로, 신규 항목의 리스트를 반드시 비워야 한다.)
        /// </summary>
        private static SerializedProperty FindOrCreateMapping(
            SerializedProperty mappingsProp, int targetInt, out bool created)
        {
            created = false;

            for (int i = 0; i < mappingsProp.arraySize; i++)
            {
                SerializedProperty element = mappingsProp.GetArrayElementAtIndex(i);
                SerializedProperty typeProp = element.FindPropertyRelative(MappingBuildingTypeField);
                if (typeProp == null)
                {
                    Debug.LogError(
                        $"[Setup] BuildingUnitMapping 에 '{MappingBuildingTypeField}' 필드가 없습니다. " +
                        "구조체 필드명이 바뀌었는지 확인하세요.");
                    return null;
                }
                if (typeProp.intValue == targetInt) return element;
            }

            // 없음 — 리스트 끝에 새 항목 추가.
            int newIndex = mappingsProp.arraySize;
            mappingsProp.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = mappingsProp.GetArrayElementAtIndex(newIndex);

            SerializedProperty newTypeProp = newElement.FindPropertyRelative(MappingBuildingTypeField);
            SerializedProperty newBlue = newElement.FindPropertyRelative(MappingBlueUnitsField);
            SerializedProperty newRed = newElement.FindPropertyRelative(MappingRedUnitsField);
            if (newTypeProp == null || newBlue == null || newRed == null)
            {
                Debug.LogError(
                    "[Setup] 새 BuildingUnitMapping 의 필드(buildingType/blueUnits/redUnits)를 찾지 못했습니다. " +
                    "구조체 필드명이 바뀌었는지 확인하세요.");
                return null;
            }

            newTypeProp.intValue = targetInt;
            // 복제된 잔여 요소 제거 — 깨끗한 빈 리스트에서 시작한다.
            newBlue.ClearArray();
            newRed.ClearArray();

            created = true;
            return newElement;
        }

        /// <summary>
        /// 팀별 유닛 리스트(blueUnits/redUnits)에서 type == unit.Type 엔트리를 찾아
        /// portrait/requiredStage 를 멱등하게 배선한다. 없으면 리스트 끝에 추가한다.
        /// </summary>
        private static WireResult EnsureUnitEntry(
            SerializedProperty unitList, UnitSpec unit, Sprite portrait)
        {
            int targetType = (int)unit.Type;

            // ── 기존 엔트리 탐색 ─────────────────────────────────────────────
            for (int i = 0; i < unitList.arraySize; i++)
            {
                SerializedProperty element = unitList.GetArrayElementAtIndex(i);
                SerializedProperty typeProp = element.FindPropertyRelative(EntryTypeField);
                SerializedProperty portraitProp = element.FindPropertyRelative(EntryPortraitField);
                SerializedProperty stageProp = element.FindPropertyRelative(EntryRequiredStageField);

                if (typeProp == null || portraitProp == null || stageProp == null)
                {
                    Debug.LogError(
                        "[Setup] UnitPortraitEntry 의 필드(type/portrait/requiredStage)를 찾지 못했습니다. " +
                        "구조체 필드명이 바뀌었는지 확인하세요.");
                    return WireResult.FieldMissing;
                }

                if (typeProp.intValue != targetType) continue;

                // 이미 존재 — portrait/requiredStage 가 올바른지 점검해 필요 시만 보정(멱등).
                bool needsFix = stageProp.intValue != unit.RequiredStage
                                || portraitProp.objectReferenceValue != portrait;
                if (!needsFix) return WireResult.AlreadyOk;

                stageProp.intValue = unit.RequiredStage;
                portraitProp.objectReferenceValue = portrait;
                return WireResult.Repaired;
            }

            // ── 엔트리 없음 — 리스트 끝에 새로 추가 ────────────────────────────
            int newIndex = unitList.arraySize;
            unitList.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = unitList.GetArrayElementAtIndex(newIndex);

            SerializedProperty newType = newElement.FindPropertyRelative(EntryTypeField);
            SerializedProperty newPortrait = newElement.FindPropertyRelative(EntryPortraitField);
            SerializedProperty newStage = newElement.FindPropertyRelative(EntryRequiredStageField);
            if (newType == null || newPortrait == null || newStage == null)
            {
                Debug.LogError(
                    "[Setup] 새 UnitPortraitEntry 의 필드(type/portrait/requiredStage)를 찾지 못했습니다. " +
                    "구조체 필드명이 바뀌었는지 확인하세요.");
                return WireResult.FieldMissing;
            }

            newType.intValue = targetType;
            newStage.intValue = unit.RequiredStage;
            newPortrait.objectReferenceValue = portrait;
            return WireResult.Added;
        }
    }
}
