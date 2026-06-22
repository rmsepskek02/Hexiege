using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hexiege.EditorTools.Fix
{
    /// <summary>
    /// ConfirmPopup 프리팹의 z-order 버그를 수정하는 1회성 에디터 스크립트.
    ///
    /// [문제]
    /// ConfirmPopup 프리팹 루트에 자체 Canvas가 없어, 부모 Canvas의 sortingOrder를
    /// 그대로 따라간다. 그 결과 다른 UI 위에 확실히 떠야 할 확인 팝업이
    /// 의도치 않게 다른 요소에 가려지는 z-order(렌더링 순서) 문제가 발생한다.
    ///
    /// [해결]
    /// 프리팹 루트에 Canvas(OverrideSorting=true, SortingOrder=250) + GraphicRaycaster를
    /// 추가하여 항상 일정한 높은 정렬 순서로 렌더링되도록 한다.
    ///
    /// 멱등(idempotent)하게 동작한다 — 여러 번 실행해도 컴포넌트를 중복 추가하지 않는다.
    /// </summary>
    public static class Fix_AddCanvasToConfirmPopup
    {
        /// <summary>수정 대상 ConfirmPopup 프리팹 경로.</summary>
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/ConfirmPopup.prefab";

        /// <summary>씬 인스턴스 오버라이드 검증 대상 씬 경로.</summary>
        private const string LoginScenePath = "Assets/_Project/Scenes/Login.unity";

        /// <summary>Canvas에 적용할 목표 SortingOrder 값.</summary>
        private const int TargetSortingOrder = 250;

        /// <summary>
        /// ConfirmPopup 프리팹 루트에 Canvas(OverrideSorting=true, SortingOrder=250)와
        /// GraphicRaycaster를 추가한다. 이미 존재하면 SortingOrder만 확인/보정한다.
        /// </summary>
        [MenuItem("Hexiege/Fix/Add Canvas To ConfirmPopup (SO=250)")]
        public static void AddCanvasToConfirmPopup()
        {
            // [1] 프리팹 에셋이 실제로 존재하는지 먼저 확인한다.
            if (!File.Exists(PrefabPath))
            {
                Debug.LogError($"[Fix_AddCanvasToConfirmPopup] 프리팹을 찾을 수 없습니다: {PrefabPath}");
                return;
            }

            // [2] PrefabUtility.LoadPrefabContents 로 프리팹을 격리된 형태로 로드한다.
            //     이렇게 로드한 인스턴스는 반드시 UnloadPrefabContents 로 해제해야 한다.
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                ApplyCanvasFix(prefabRoot);

                // [3] 변경 내용을 프리팹 에셋에 저장한다.
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                // [4] 로드한 프리팹 컨텐츠는 예외 발생 여부와 무관하게 반드시 해제한다.
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            // [5] 씬 인스턴스에 SortingOrder 오버라이드가 걸려 있는지 검증한다.
            CheckSceneInstanceOverride();
        }

        /// <summary>
        /// 프리팹 루트에 Canvas/GraphicRaycaster를 멱등하게 적용한다.
        /// </summary>
        /// <param name="prefabRoot">LoadPrefabContents로 로드한 프리팹 루트 오브젝트.</param>
        private static void ApplyCanvasFix(GameObject prefabRoot)
        {
            // [Canvas 처리] 이미 있으면 추가하지 않고 SortingOrder만 보정한다.
            Canvas canvas = prefabRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = prefabRoot.AddComponent<Canvas>();
                Debug.Log($"[Fix_AddCanvasToConfirmPopup] Canvas 추가됨.");
            }

            // SerializedObject를 통해 프로퍼티를 설정한다.
            // LoadPrefabContents 환경에서는 직접 프로퍼티 대입보다 SerializedObject/Property
            // 방식이 Unity 직렬화 시스템에 더 확실하게 반영된다.
            SerializedObject so = new SerializedObject(canvas);

            SerializedProperty overrideSortingProp = so.FindProperty("m_OverrideSorting");
            SerializedProperty sortingOrderProp    = so.FindProperty("m_SortingOrder");

            bool changed = false;

            if (overrideSortingProp != null && !overrideSortingProp.boolValue)
            {
                overrideSortingProp.boolValue = true;
                changed = true;
            }

            if (sortingOrderProp != null && sortingOrderProp.intValue != TargetSortingOrder)
            {
                Debug.Log($"[Fix_AddCanvasToConfirmPopup] SortingOrder " +
                          $"{sortingOrderProp.intValue} -> {TargetSortingOrder} 로 보정합니다.");
                sortingOrderProp.intValue = TargetSortingOrder;
                changed = true;
            }

            if (changed)
            {
                // 변경 사항을 SerializedObject에 적용한다.
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(canvas);
                Debug.Log($"[Fix_AddCanvasToConfirmPopup] Canvas 설정 완료 " +
                          $"(OverrideSorting=true, SortingOrder={TargetSortingOrder}).");
            }
            else
            {
                Debug.Log("[Fix_AddCanvasToConfirmPopup] Canvas가 이미 올바르게 설정되어 있습니다. (변경 없음)");
            }

            // [GraphicRaycaster 처리] OverrideSorting Canvas는 자체 Raycaster가 필요하다.
            //                          이미 있으면 스킵한다.
            GraphicRaycaster raycaster = prefabRoot.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                prefabRoot.AddComponent<GraphicRaycaster>();
                Debug.Log("[Fix_AddCanvasToConfirmPopup] GraphicRaycaster 추가됨.");
            }
            else
            {
                Debug.Log("[Fix_AddCanvasToConfirmPopup] GraphicRaycaster가 이미 존재하여 스킵합니다.");
            }
        }

        /// <summary>
        /// Login 씬에 배치된 ConfirmPopup 인스턴스가 SortingOrder를 오버라이드하고 있는지 검사한다.
        /// 오버라이드가 있으면 프리팹 수정이 무력화될 수 있으므로 경고를 출력한다.
        /// </summary>
        private static void CheckSceneInstanceOverride()
        {
            if (!File.Exists(LoginScenePath))
            {
                // 씬이 없으면 검증을 건너뛴다 (선택적 검증이므로 에러로 취급하지 않음).
                Debug.Log($"[Fix_AddCanvasToConfirmPopup] 검증용 씬을 찾을 수 없어 오버라이드 검사를 건너뜁니다: {LoginScenePath}");
                return;
            }

            // 현재 열려 있는 씬을 보존하기 위해, 검증 씬은 Additive로 잠시 로드한다.
            Scene loginScene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Additive);

            try
            {
                bool foundOverride = false;

                // 씬 루트 오브젝트들을 순회하며 ConfirmPopup 프리팹 인스턴스를 찾는다.
                GameObject[] rootObjects = loginScene.GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    foundOverride |= ScanForSortingOverride(root.transform);
                }

                if (!foundOverride)
                {
                    Debug.Log("[Fix_AddCanvasToConfirmPopup] Login 씬에 ConfirmPopup SortingOrder 오버라이드 없음. (정상)");
                }
            }
            finally
            {
                // 검증용으로 Additive 로드한 씬은 다시 닫는다 (저장하지 않음).
                // 단, 해당 씬이 현재 유일하게 열린 씬이면 CloseScene이 불가하므로 스킵한다.
                if (EditorSceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(loginScene, true);
                else
                    Debug.Log("[Fix_AddCanvasToConfirmPopup] Login 씬이 유일한 열린 씬이라 닫지 않음 (정상).");
            }
        }

        /// <summary>
        /// 주어진 Transform 하위 계층에서 ConfirmPopup 프리팹 인스턴스를 찾아,
        /// Canvas의 SortingOrder/OverrideSorting 프로퍼티 오버라이드 여부를 검사한다.
        /// </summary>
        /// <returns>SortingOrder 관련 오버라이드를 하나라도 발견하면 true.</returns>
        private static bool ScanForSortingOverride(Transform current)
        {
            bool found = false;

            // 이 오브젝트가 프리팹 인스턴스의 루트인지 확인한다.
            if (PrefabUtility.IsAnyPrefabInstanceRoot(current.gameObject))
            {
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject);
                if (sourcePath == PrefabPath)
                {
                    found |= InspectInstanceForSortingOverride(current.gameObject);
                }
            }

            // 자식들도 재귀적으로 검사한다 (중첩 배치 대비).
            for (int i = 0; i < current.childCount; i++)
            {
                found |= ScanForSortingOverride(current.GetChild(i));
            }

            return found;
        }

        /// <summary>
        /// ConfirmPopup 인스턴스의 프로퍼티 수정 목록에서 Canvas SortingOrder/OverrideSorting
        /// 오버라이드를 찾아 경고를 출력한다.
        /// </summary>
        /// <returns>관련 오버라이드를 발견하면 true.</returns>
        private static bool InspectInstanceForSortingOverride(GameObject instanceRoot)
        {
            // 인스턴스에 적용된 프로퍼티 수정 목록을 가져온다.
            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot);
            if (modifications == null)
            {
                return false;
            }

            bool found = false;
            foreach (PropertyModification mod in modifications)
            {
                if (mod == null || mod.target == null)
                {
                    continue;
                }

                // Canvas 컴포넌트에 대한 수정만 검사한다.
                if (mod.target is Canvas)
                {
                    // m_SortingOrder / m_OverrideSorting 프로퍼티 오버라이드를 감지한다.
                    if (mod.propertyPath == "m_SortingOrder" || mod.propertyPath == "m_OverrideSorting")
                    {
                        Debug.LogWarning($"[Fix_AddCanvasToConfirmPopup] Login 씬의 ConfirmPopup 인스턴스에 " +
                                         $"Canvas '{mod.propertyPath}' 오버라이드가 있습니다 (값='{mod.value}'). " +
                                         $"이 오버라이드가 프리팹 SortingOrder={TargetSortingOrder} 설정을 덮어쓸 수 있으니 확인하세요.");
                        found = true;
                    }
                }
            }

            return found;
        }
    }
}
