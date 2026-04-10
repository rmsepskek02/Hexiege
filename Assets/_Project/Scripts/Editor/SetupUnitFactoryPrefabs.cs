// ============================================================================
// SetupUnitFactoryPrefabs.cs
// UnitFactory의 종족별 UnitPrefabEntry 리스트에 프리팹을 자동으로 연결하는 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/UnitFactory 프리팹 연결
//
// 동작:
//   1. 씬에서 UnitFactory 컴포넌트를 찾음
//   2. 9종 유닛(3종족 × 3유닛)에 대해 Blue/Red 프리팹을 경로 규칙으로 로드
//   3. 종족별 List<UnitPrefabEntry>에 자동 연결
//   4. EditorUtility.SetDirty() + AssetDatabase.SaveAssets()로 저장
//
// 프리팹 경로 규칙:
//   Assets/_Project/Prefabs/Units/{Race}/Unit_{Name}_{Team}.prefab
//   예: Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab
//
// Editor 전용 — 빌드에 포함되지 않음.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Hexiege.Domain;
using Hexiege.Infrastructure;

namespace Hexiege.Editor
{
    public static class SetupUnitFactoryPrefabs
    {
        // ====================================================================
        // 종족별 유닛 매핑 정의
        // 각 종족에 속하는 유닛 타입과 프리팹 이름을 정의.
        // 프리팹 이름은 파일명에서 "Unit_{Name}" 부분에 해당.
        // ====================================================================

        /// <summary>
        /// 종족-유닛 매핑 구조체.
        /// raceFolderName: 프리팹 경로의 종족 폴더명 (예: "Human")
        /// units: 해당 종족의 (UnitType, 프리팹 이름) 배열
        /// </summary>
        private struct RaceMapping
        {
            public string raceFolderName;
            public (UnitType type, string prefabName)[] units;
        }

        /// <summary>
        /// 전체 종족-유닛 매핑 테이블.
        /// 새 종족이나 유닛을 추가할 때 이 테이블에만 항목을 추가하면 됨.
        /// </summary>
        private static readonly RaceMapping[] _raceMappings = new RaceMapping[]
        {
            // ── 인간계 ──
            new RaceMapping
            {
                raceFolderName = "Human",
                units = new[]
                {
                    (UnitType.Pistoleer, "Pistoleer"),
                    (UnitType.Assault,   "Assault"),
                    (UnitType.Sniper,    "Sniper")
                }
            },
            // ── 정령계 ──
            new RaceMapping
            {
                raceFolderName = "Spirit",
                units = new[]
                {
                    (UnitType.FlameSpirit,   "FlameSpirit"),
                    (UnitType.EmberSpirit,   "EmberSpirit"),
                    (UnitType.InfernoSpirit, "InfernoSpirit")
                }
            },
            // ── 초월계 ──
            new RaceMapping
            {
                raceFolderName = "Transcendence",
                units = new[]
                {
                    (UnitType.BearGuard,   "BearGuard"),
                    (UnitType.FoxMagician, "FoxMagician"),
                    (UnitType.LionKnight,  "LionKnight")
                }
            }
        };

        /// <summary>
        /// 프리팹 루트 경로. 이 경로 아래에 종족별 폴더가 위치.
        /// </summary>
        private const string PrefabBasePath = "Assets/_Project/Prefabs/Units";

        // ====================================================================
        // 메뉴 실행
        // ====================================================================

        [MenuItem("Hexiege/Setup/UnitFactory 프리팹 연결")]
        public static void Execute()
        {
            // 씬에서 UnitFactory 컴포넌트를 찾음
            var factory = Object.FindFirstObjectByType<UnitFactory>();
            if (factory == null)
            {
                Debug.LogError("[SetupUnitFactoryPrefabs] 씬에 UnitFactory 컴포넌트가 없습니다.");
                return;
            }

            // SerializedObject를 통해 Inspector 필드에 접근.
            // SerializedObject를 사용해야 Undo/Redo가 지원되고, 프리팹 오버라이드가 올바르게 처리됨.
            var so = new SerializedObject(factory);

            int totalLinked = 0;
            int totalMissing = 0;

            // 종족별로 프리팹 리스트를 채움
            // _raceMappings 배열의 인덱스가 SerializedProperty 필드명과 매핑됨
            string[] fieldNames = { "_humanPrefabs", "_spiritPrefabs", "_transcendencePrefabs" };

            for (int raceIdx = 0; raceIdx < _raceMappings.Length; raceIdx++)
            {
                var mapping = _raceMappings[raceIdx];
                var listProp = so.FindProperty(fieldNames[raceIdx]);

                if (listProp == null)
                {
                    Debug.LogError($"[SetupUnitFactoryPrefabs] 필드를 찾을 수 없습니다: {fieldNames[raceIdx]}");
                    continue;
                }

                // 리스트를 유닛 수에 맞게 크기 조정
                listProp.arraySize = mapping.units.Length;

                for (int i = 0; i < mapping.units.Length; i++)
                {
                    var (unitType, prefabName) = mapping.units[i];
                    var entryProp = listProp.GetArrayElementAtIndex(i);

                    // type 필드 설정
                    var typeProp = entryProp.FindPropertyRelative("type");
                    typeProp.enumValueIndex = GetEnumIndex(unitType);

                    // Blue 프리팹 로드 및 연결
                    string bluePath = $"{PrefabBasePath}/{mapping.raceFolderName}/Unit_{prefabName}_Blue.prefab";
                    var bluePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
                    var blueProp = entryProp.FindPropertyRelative("blue");
                    blueProp.objectReferenceValue = bluePrefab;

                    if (bluePrefab != null)
                        totalLinked++;
                    else
                    {
                        totalMissing++;
                        Debug.LogWarning($"[SetupUnitFactoryPrefabs] 프리팹 없음: {bluePath}");
                    }

                    // Red 프리팹 로드 및 연결
                    string redPath = $"{PrefabBasePath}/{mapping.raceFolderName}/Unit_{prefabName}_Red.prefab";
                    var redPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
                    var redProp = entryProp.FindPropertyRelative("red");
                    redProp.objectReferenceValue = redPrefab;

                    if (redPrefab != null)
                        totalLinked++;
                    else
                    {
                        totalMissing++;
                        Debug.LogWarning($"[SetupUnitFactoryPrefabs] 프리팹 없음: {redPath}");
                    }
                }
            }

            // 변경사항 적용 및 저장
            // SerializedObject 변경사항을 실제 오브젝트에 반영
            so.ApplyModifiedProperties();

            // 씬 오브젝트를 수정했으므로 두 단계로 저장해야 함:
            //   1. EditorUtility.SetDirty(): 이 오브젝트가 변경되었음을 Unity 에디터에 알림
            //   2. EditorSceneManager.MarkSceneDirty(): 씬 파일(.unity)을 저장 대상으로 표시
            //   3. EditorSceneManager.SaveScene(): 씬 파일을 디스크에 실제 저장
            // AssetDatabase.SaveAssets()는 .asset 파일만 저장하므로 씬 오브젝트에는 부족함.
            EditorUtility.SetDirty(factory);
            var scene = factory.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SetupUnitFactoryPrefabs] 완료. 연결 성공: {totalLinked}개, 프리팹 없음: {totalMissing}개");
        }

        /// <summary>
        /// UnitType enum 값을 SerializedProperty의 enumValueIndex로 변환.
        /// enumValueIndex는 선언 순서(0부터)와 동일하지만,
        /// enum 값이 비연속적일 경우를 대비하여 명시적으로 변환.
        /// </summary>
        private static int GetEnumIndex(UnitType type)
        {
            // UnitType의 선언 순서가 enum 정수값과 일치하므로 직접 캐스팅
            // Pistoleer=0, Assault=1, Sniper=2, FlameSpirit=3, ...
            // SerializedProperty.enumValueIndex는 선언 순서를 사용하므로 동일
            return (int)type;
        }
    }
}
#endif
