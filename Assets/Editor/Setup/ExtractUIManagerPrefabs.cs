// ============================================================================
// ExtractUIManagerPrefabs.cs
// 전역 UI 시스템(UIManager) 도입을 위해, 기존 씬에 흩어져 있던 공통 UI를
// 재사용 가능한 프리팹으로 한 번에 추출하는 1회성 에디터 도구.
//
// 실행 방법:
//   Unity 상단 메뉴 → Hexiege/Setup/UIManager 프리팹 추출 클릭
//
// 추출 대상(Plan 기준 — 각 씬에서 가장 완성된 버전):
//   1) Game.unity  → ConfirmPopup        → Assets/_Project/Prefabs/UI/ConfirmPopup.prefab
//   2) Lobby.unity → ToastUI             → Assets/_Project/Prefabs/UI/ToastUI.prefab
//   3) Lobby.unity → LoadingIndicator    → Assets/_Project/Prefabs/UI/LoadingIndicator.prefab
//
// 탐색 방식:
//   - ConfirmPopup / ToastUI 는 전용 컴포넌트 타입이 존재하므로 "컴포넌트 타입"으로 찾는다.
//     (Hexiege.Presentation.ConfirmPopup, Hexiege.Presentation.ToastUI)
//   - LoadingIndicator 는 전용 컴포넌트가 없는(CanvasGroup 기반) 오브젝트이므로
//     "GameObject 이름(LoadingIndicator)"으로 찾는다.
//
// 동작 개요:
//   - 대상 씬을 Additive 로드 → 해당 오브젝트를 찾아 프리팹으로 저장 → 씬 닫기.
//   - 추출 결과(성공/실패)는 콘솔 로그로 안내한다.
//   - 이미 같은 경로에 프리팹이 있으면 SaveAsPrefabAsset이 덮어쓴다.
//
// 주의:
//   - 1회성 셋업 스크립트. 추출 완료 후 삭제해도 무방하다.
//   - Editor 전용 — 빌드에는 포함되지 않는다 (Assets/Editor 폴더).
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Presentation;

namespace HexiegeEditor
{
    /// <summary>
    /// 기존 씬의 공통 UI(ConfirmPopup / ToastUI / LoadingIndicator)를
    /// 프리팹으로 추출하는 1회성 에디터 도구.
    /// </summary>
    public static class ExtractUIManagerPrefabs
    {
        // ====================================================================
        // 상수 — 씬/프리팹 경로
        // ====================================================================

        private const string GameScenePath  = "Assets/_Project/Scenes/Game.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";

        /// <summary>프리팹을 저장할 폴더. 없으면 자동 생성한다.</summary>
        private const string PrefabDir = "Assets/_Project/Prefabs/UI";

        private const string ConfirmPopupPrefabPath     = PrefabDir + "/ConfirmPopup.prefab";
        private const string ToastUIPrefabPath          = PrefabDir + "/ToastUI.prefab";
        private const string LoadingIndicatorPrefabPath = PrefabDir + "/LoadingIndicator.prefab";

        /// <summary>
        /// LoadingScreen은 전용 컴포넌트(LoadingScreen.cs)가 있으나,
        /// Lobby.unity에서 배치된 GameObject 이름이 "LoadingScreen"이므로
        /// 컴포넌트 타입 대신 이름으로 탐색한다.
        /// </summary>
        private const string LoadingIndicatorObjectName = "LoadingScreen";

        // ====================================================================
        // 메뉴 진입점
        // ====================================================================

        /// <summary>
        /// 메뉴 클릭 시 실행. 세 개의 공통 UI 프리팹을 순서대로 추출한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/UIManager 프리팹 추출")]
        public static void Extract()
        {
            // 저장 폴더가 없으면 만든다.
            EnsurePrefabDir();

            // 1) Game.unity → ConfirmPopup (컴포넌트 타입으로 탐색)
            ExtractFromScene<ConfirmPopup>(GameScenePath, ConfirmPopupPrefabPath, "ConfirmPopup");

            // 2) Lobby.unity → ToastUI (컴포넌트 타입으로 탐색)
            ExtractFromScene<ToastUI>(LobbyScenePath, ToastUIPrefabPath, "ToastUI");

            // 3) Lobby.unity → LoadingIndicator (이름으로 탐색)
            ExtractByNameFromScene(LobbyScenePath, LoadingIndicatorObjectName, LoadingIndicatorPrefabPath);

            // 추출된 에셋을 에디터에 반영.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ExtractUIManagerPrefabs] 프리팹 추출 작업이 끝났습니다. 위 로그에서 각 항목의 성공 여부를 확인하세요.");
        }

        // ====================================================================
        // 추출 로직
        // ====================================================================

        /// <summary>
        /// 지정한 씬을 Additive 로드하여, 주어진 컴포넌트 타입 T가 부착된
        /// 첫 GameObject를 프리팹으로 저장한다. 작업 후 씬을 닫는다.
        /// </summary>
        /// <typeparam name="T">탐색할 컴포넌트 타입(예: ConfirmPopup).</typeparam>
        /// <param name="scenePath">대상 씬 경로.</param>
        /// <param name="prefabPath">저장할 프리팹 경로.</param>
        /// <param name="label">로그에 표시할 이름.</param>
        private static void ExtractFromScene<T>(string scenePath, string prefabPath, string label)
            where T : Component
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                // 비활성 오브젝트까지 포함해 씬 전체에서 컴포넌트를 찾는다.
                T component = FindComponentInScene<T>(scene);
                if (component == null)
                {
                    Debug.LogWarning($"[ExtractUIManagerPrefabs] {scenePath} 에서 {label}({typeof(T).Name}) 컴포넌트를 찾지 못했습니다. 건너뜁니다.");
                    return;
                }

                SavePrefab(component.gameObject, prefabPath, label);
            }
            finally
            {
                // 추출만 하고 씬 변경은 저장하지 않는다(true = 변경 폐기).
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 지정한 씬을 Additive 로드하여, 주어진 이름의 GameObject를 프리팹으로 저장한다.
        /// 전용 컴포넌트가 없는 오브젝트(LoadingIndicator 등)에 사용한다.
        /// </summary>
        /// <param name="scenePath">대상 씬 경로.</param>
        /// <param name="objectName">찾을 GameObject 이름.</param>
        /// <param name="prefabPath">저장할 프리팹 경로.</param>
        private static void ExtractByNameFromScene(string scenePath, string objectName, string prefabPath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                GameObject target = FindGameObjectByNameInScene(scene, objectName);
                if (target == null)
                {
                    Debug.LogWarning($"[ExtractUIManagerPrefabs] {scenePath} 에서 '{objectName}' 이름의 GameObject를 찾지 못했습니다. 건너뜁니다.");
                    return;
                }

                SavePrefab(target, prefabPath, objectName);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// 프리팹 저장 폴더가 없으면 생성한다(중간 폴더 포함).
        /// </summary>
        private static void EnsurePrefabDir()
        {
            if (!Directory.Exists(PrefabDir))
            {
                Directory.CreateDirectory(PrefabDir);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 씬 루트들을 순회하며, 비활성 오브젝트까지 포함해 컴포넌트 T를 가진 첫 오브젝트를 반환한다.
        /// </summary>
        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // 비활성 오브젝트도 검색하도록 includeInactive = true.
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 씬 루트들을 순회하며, 비활성 오브젝트까지 포함해 주어진 이름의 GameObject를 반환한다.
        /// </summary>
        private static GameObject FindGameObjectByNameInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // 루트 자신도 검사.
                if (root.name == objectName) return root;

                // 하위(비활성 포함) 전체를 순회.
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in all)
                {
                    if (t.name == objectName) return t.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// 주어진 GameObject를 지정 경로의 프리팹으로 저장한다.
        /// </summary>
        private static void SavePrefab(GameObject go, string prefabPath, string label)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out bool success);
            if (success && prefab != null)
                Debug.Log($"[ExtractUIManagerPrefabs] {label} 추출 완료 → {prefabPath}");
            else
                Debug.LogError($"[ExtractUIManagerPrefabs] {label} 프리팹 저장 실패: {prefabPath}");
        }
    }
}
