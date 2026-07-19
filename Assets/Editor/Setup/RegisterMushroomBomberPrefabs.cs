// ============================================================================
// RegisterMushroomBomberPrefabs.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  상단 메뉴  Hexiege > Setup > Register MushroomBomber Prefabs (Game) 실행. │
// │  (Game 씬이 열려 있지 않아도 됨 — 없으면 스크립트가 자동으로 열고 저장한다.)  │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가:
//   1) Game 씬에서 UnitFactory 컴포넌트를 찾는다.
//   2) UnitFactory 의 private [SerializeField] _transcendencePrefabs 리스트에
//      MushroomBomber(UnitType 26) 엔트리가 있는지 확인한다.
//        · 없으면  → 리스트 끝에 새 엔트리를 추가하고 type=MushroomBomber,
//                    blue=Unit_MushroomBomber_Blue, red=Unit_MushroomBomber_Red 를 배선한다.
//        · 있으면  → blue/red 슬롯이 올바른 프리팹을 가리키는지 점검해
//                    누락/오연결만 보정한다(정상이면 아무것도 하지 않음 — no-op).
//   3) EditorUtility.SetDirty + 씬 dirty 표시(직접 연 씬이면 자동 저장)까지 처리한다.
//   4) Console 에 추가/보정/무변경 결과·경로를 리포트한다.
//
// 왜 필요한가:
//   UnitFactory 는 종족(초월계) + UnitType + 팀 조합으로 프리팹을 찾아 유닛을
//   Instantiate 한다. _transcendencePrefabs 에 MushroomBomber 프리팹이 등록돼 있지
//   않으면 MushroomBomber 생산 시 "프리팹이 설정되지 않았습니다" 에러가 나며 유닛이
//   생성되지 않는다. 이 배선은 프리팹 GUID 를 직렬화하는 작업이라 반드시 Unity
//   에디터 런타임(SerializedObject) 에서 수행해야 한다 → 그래서 에디터 스크립트다.
//
// 멱등성:
//   여러 번 실행해도 안전하다. 엔트리가 이미 있고 blue/red 가 올바르면 no-op 이며,
//   프리팹이 누락됐거나 잘못 연결된 경우에만 해당 슬롯을 바로잡는다.
//
// ⚠️ 함정 주의(반드시 UnitFactory 로 타입 특정):
//   BuildingFactory 도 동일한 이름의 _transcendencePrefabs 필드를 갖는다. 두 컴포넌트가
//   같은 씬에 있으므로, 반드시 FindFirstObjectByType<UnitFactory>() 로 UnitFactory 를
//   특정해야 한다. (BuildingFactory 의 type 26 은 UnitType 이 아니라 BuildingType
//   PrimalSanctuary(건물)를 가리키므로, 숫자만 겹칠 뿐 전혀 다른 데이터다.)
//
// 주의:
//   · 이 스크립트는 "프리팹 등록"까지만 담당한다. 어떤 생산 건물이 MushroomBomber 를
//     생산 목록에 노출하는지(생산 라인 매핑)는 ProductionPanelUI 의 씬 직렬화
//     데이터(_buildingUnitMappings)이며, 별도의 WireFloraProductionLine.cs 가 담당한다.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Domain;         // UnitType
using Hexiege.Infrastructure; // UnitFactory

namespace Hexiege.EditorTools
{
    /// <summary>
    /// UnitFactory._transcendencePrefabs 에 MushroomBomber(26) 프리팹 엔트리를
    /// 멱등하게 등록하는 1회성 에디터 스크립트.
    /// </summary>
    public static class RegisterMushroomBomberPrefabs
    {
        // GameBootstrapper/UnitFactory 가 배치된 씬. 열려 있지 않으면 이 씬을 연다.
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // 등록할 MushroomBomber 프리팹 경로(팀별).
        private const string BluePrefabPath =
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_MushroomBomber_Blue.prefab";
        private const string RedPrefabPath =
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_MushroomBomber_Red.prefab";

        // UnitFactory 의 초월계 프리팹 리스트 필드명(정확히 일치해야 한다).
        private const string TranscendenceListFieldName = "_transcendencePrefabs";

        // UnitPrefabEntry 구조체의 상대 필드명(리스트 요소 내부).
        private const string EntryTypeFieldName = "type";
        private const string EntryBlueFieldName = "blue";
        private const string EntryRedFieldName = "red";

        // 이번에 등록할 유닛 타입(초월계 범위 폭발 딜러). 열거형 값은 26.
        private const UnitType TargetType = UnitType.MushroomBomber;

        /// <summary>
        /// 메뉴 진입점. Game 씬의 UnitFactory 에 MushroomBomber 프리팹 엔트리를 멱등 등록한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Register MushroomBomber Prefabs (Game)")]
        public static void Run()
        {
            // ── 1) 등록할 프리팹 확보(둘 다 있어야 진행) ──────────────────────
            GameObject bluePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BluePrefabPath);
            GameObject redPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RedPrefabPath);

            if (bluePrefab == null || redPrefab == null)
            {
                Debug.LogError(
                    "[Setup] MushroomBomber 프리팹을 찾지 못했습니다. 경로를 확인하세요.\n" +
                    $"  Blue: {BluePrefabPath} → {(bluePrefab == null ? "없음" : "OK")}\n" +
                    $"  Red : {RedPrefabPath} → {(redPrefab == null ? "없음" : "OK")}");
                return;
            }

            // ── 2) UnitFactory 확보(열린 씬 우선, 없으면 Game 씬을 연다) ───────
            bool sceneWasOpenedByThis = false;
            UnitFactory factory = FindFactoryInOpenScenes();
            if (factory == null)
            {
                factory = OpenGameSceneAndFindFactory(out sceneWasOpenedByThis);
            }

            if (factory == null)
            {
                Debug.LogWarning(
                    "[Setup] UnitFactory 를 씬에서 찾지 못해 프리팹을 등록하지 못했습니다.\n" +
                    "  해결: Game 씬을 열고 다시 실행하거나, Inspector 에서 수동으로 연결하세요.");
                return;
            }

            // ── 3) 리스트 엔트리 멱등 배선(SerializedObject) ─────────────────
            RegisterResult result =
                WireMushroomBomberEntry(factory, bluePrefab, redPrefab);

            if (result == RegisterResult.FieldMissing)
            {
                // 필드명이 바뀌었거나 컴포넌트가 다른 경우 — WireMushroomBomberEntry 가 에러 로그를 남긴다.
                return;
            }

            // ── 4) Dirty / 씬 저장 처리 ──────────────────────────────────────
            Scene scene = factory.gameObject.scene;
            bool changed = (result == RegisterResult.Added || result == RegisterResult.Repaired);
            if (changed)
            {
                EditorUtility.SetDirty(factory);
                EditorSceneManager.MarkSceneDirty(scene);

                // 이 스크립트가 직접 연 씬이면 자동 저장까지 마무리한다(Ctrl+S 놓침 방지).
                if (sceneWasOpenedByThis)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }

            // ── 5) 결과 리포트 ───────────────────────────────────────────────
            string entryState = result switch
            {
                RegisterResult.Added        => "신규 엔트리 추가",
                RegisterResult.Repaired     => "기존 엔트리의 blue/red 슬롯 보정",
                RegisterResult.AlreadyOk    => "이미 올바르게 등록됨(변경 없음)",
                _                            => "실패"
            };
            string saveState = !changed
                ? "변경 없음 — 저장 불필요"
                : (sceneWasOpenedByThis
                    ? "씬 자동 저장 완료"
                    : "열린 씬 dirty 표시 — Ctrl+S 로 저장하세요");

            Debug.Log(
                "[Setup] MushroomBomber 프리팹 등록 완료.\n" +
                $"  · UnitFactory.{TranscendenceListFieldName}[type={(int)TargetType} {TargetType}]: {entryState}\n" +
                $"  · Blue: {BluePrefabPath}\n" +
                $"  · Red : {RedPrefabPath}\n" +
                $"  · 씬: {saveState}");

            Selection.activeObject = factory;
            EditorGUIUtility.PingObject(factory);
        }

        // ====================================================================
        // UnitFactory 탐색 / 씬 열기
        // ====================================================================

        /// <summary>
        /// 현재 열려 있는 씬(들)에서 UnitFactory 를 찾는다(비활성 포함). 없으면 null.
        /// ⚠️ BuildingFactory 도 _transcendencePrefabs 를 갖지만, 타입을 UnitFactory 로
        /// 특정하므로 절대 혼동되지 않는다.
        /// </summary>
        private static UnitFactory FindFactoryInOpenScenes()
        {
            return Object.FindFirstObjectByType<UnitFactory>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Game 씬을 (단독 모드로) 열어 UnitFactory 를 찾는다.
        /// 성공 시 <paramref name="opened"/>=true 로 알려 호출부가 자동 저장하도록 한다.
        /// </summary>
        private static UnitFactory OpenGameSceneAndFindFactory(out bool opened)
        {
            opened = false;

            // Game 씬 에셋이 실제로 있는지 먼저 확인.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                Debug.LogWarning($"[Setup] Game 씬을 찾지 못했습니다: {GameScenePath}");
                return null;
            }

            // 저장되지 않은 현재 씬 변경이 있으면 저장 여부를 물어본다(데이터 손실 방지).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Setup] 씬 열기가 취소되었습니다(저장 대화 취소). 등록을 건너뜁니다.");
                return null;
            }

            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            opened = true;
            return FindFactoryInOpenScenes();
        }

        // ====================================================================
        // 리스트 엔트리 배선
        // ====================================================================

        /// <summary>
        /// WireMushroomBomberEntry 의 처리 결과.
        /// </summary>
        private enum RegisterResult
        {
            FieldMissing, // 리스트 필드/구조체 필드를 찾지 못함(에러)
            Added,        // 엔트리를 새로 추가함
            Repaired,     // 기존 엔트리의 blue/red 를 보정함
            AlreadyOk     // 이미 올바름 — 변경 없음
        }

        /// <summary>
        /// UnitFactory._transcendencePrefabs 에서 type==MushroomBomber 엔트리를 찾아
        /// blue/red 프리팹을 멱등하게 배선한다. 없으면 리스트 끝에 추가한다.
        /// </summary>
        private static RegisterResult WireMushroomBomberEntry(
            UnitFactory factory, GameObject bluePrefab, GameObject redPrefab)
        {
            var so = new SerializedObject(factory);
            SerializedProperty listProp = so.FindProperty(TranscendenceListFieldName);
            if (listProp == null || !listProp.isArray)
            {
                Debug.LogError(
                    $"[Setup] UnitFactory 에 '{TranscendenceListFieldName}' 리스트 필드가 없습니다. " +
                    "필드명이 바뀌었는지 확인하세요.");
                return RegisterResult.FieldMissing;
            }

            // ── 기존 엔트리 탐색: type == MushroomBomber ──────────────────────
            // 열거형 SerializedProperty 는 내부 정수값을 intValue 로 읽고/쓴다.
            // (UnitType 은 값이 비연속(…,26,27)이므로 enumValueIndex(오디널)가 아닌 intValue 사용.)
            int targetInt = (int)TargetType;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty element = listProp.GetArrayElementAtIndex(i);
                SerializedProperty typeProp = element.FindPropertyRelative(EntryTypeFieldName);
                SerializedProperty blueProp = element.FindPropertyRelative(EntryBlueFieldName);
                SerializedProperty redProp = element.FindPropertyRelative(EntryRedFieldName);

                if (typeProp == null || blueProp == null || redProp == null)
                {
                    Debug.LogError(
                        "[Setup] UnitPrefabEntry 의 필드(type/blue/red)를 찾지 못했습니다. " +
                        "구조체 필드명이 바뀌었는지 확인하세요.");
                    return RegisterResult.FieldMissing;
                }

                if (typeProp.intValue != targetInt) continue;

                // 이미 존재하는 엔트리 — blue/red 가 올바른지 점검해 필요 시만 보정(멱등).
                bool needsFix = blueProp.objectReferenceValue != bluePrefab
                                || redProp.objectReferenceValue != redPrefab;
                if (!needsFix)
                {
                    return RegisterResult.AlreadyOk;
                }

                blueProp.objectReferenceValue = bluePrefab;
                redProp.objectReferenceValue = redPrefab;
                so.ApplyModifiedProperties();
                return RegisterResult.Repaired;
            }

            // ── 엔트리 없음 — 리스트 끝에 새로 추가 ────────────────────────────
            int newIndex = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = listProp.GetArrayElementAtIndex(newIndex);

            SerializedProperty newType = newElement.FindPropertyRelative(EntryTypeFieldName);
            SerializedProperty newBlue = newElement.FindPropertyRelative(EntryBlueFieldName);
            SerializedProperty newRed = newElement.FindPropertyRelative(EntryRedFieldName);

            if (newType == null || newBlue == null || newRed == null)
            {
                Debug.LogError(
                    "[Setup] 새 UnitPrefabEntry 의 필드(type/blue/red)를 찾지 못했습니다. " +
                    "구조체 필드명이 바뀌었는지 확인하세요.");
                return RegisterResult.FieldMissing;
            }

            newType.intValue = targetInt;
            newBlue.objectReferenceValue = bluePrefab;
            newRed.objectReferenceValue = redPrefab;
            so.ApplyModifiedProperties();
            return RegisterResult.Added;
        }
    }
}
