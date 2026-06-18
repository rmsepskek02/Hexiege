// ============================================================================
// ExtractUIManagerPrefabs.cs
// 전역 UI 시스템(UIManager) 도입을 위해, 기존 씬에 흩어져 있던 공통 UI를
// 재사용 가능한 프리팹으로 한 번에 추출하는 1회성 에디터 도구.
//
// 실행 방법:
//   Unity 상단 메뉴 → Hexiege/Setup/UIManager 프리팹 추출 클릭
//
// 추출 대상(Plan 기준 — 각 씬에서 가장 완성된 버전):
//   1) Game.unity  → ConfirmPopup     → Assets/_Project/Prefabs/UI/ConfirmPopup.prefab
//   2) Lobby.unity → ToastUI          → Assets/_Project/Prefabs/UI/ToastUI.prefab
//   3) Lobby.unity → LoadingIndicator → Assets/_Project/Prefabs/UI/LoadingIndicator.prefab
//
// 탐색 방식:
//   - ConfirmPopup / ToastUI 는 전용 컴포넌트 타입이 존재하므로 "컴포넌트 타입"으로 찾는다.
//     (Hexiege.Presentation.ConfirmPopup, Hexiege.Presentation.ToastUI)
//   - LoadingIndicator 는 Lobby.unity의 "LoadingScreen" 오브젝트를 이름으로 찾아 추출한다.
//     단, LoadingScreen은 자체 Canvas + LoadingScreen.cs 싱글턴을 가진 독립 오브젝트이므로
//     UIManager 하위에 임베드하기 위해 아래 컴포넌트를 추출 전 제거한다(B안):
//       제거: Canvas, CanvasScaler, GraphicRaycaster, LoadingScreen.cs
//       유지: RectTransform, CanvasGroup, RootPanel(Image), Spinner(Image), StatusText(TMP)
//     씬은 CloseScene(removeScene: true)으로 닫아 변경을 폐기하므로 원본 씬은 보존된다.
//
// 동작 개요:
//   - 대상 씬을 Additive 로드 → 해당 오브젝트를 찾아 프리팹으로 저장 → 씬 닫기.
//   - 추출 결과(성공/실패)는 콘솔 로그로 안내한다.
//   - 이미 같은 경로에 프리팹이 있으면 SaveAsPrefabAsset이 덮어쓴다.
//
// 주의:
//   - 1회성 셋업 스크립트. 추출 완료 후 삭제해도 무방하다.
//   - Editor 전용 — 빌드에는 포함되지 않는다 (Assets/Editor 폴더).
//   - LoadingIndicator 추출 후 Spinner 자식 오브젝트에 SpinnerRotator 컴포넌트를
//     수동으로 부착해야 회전 애니메이션이 동작한다.
//     (기존 LoadingScreen.cs의 Update()가 담당하던 회전 로직을 대체)
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hexiege.Presentation;
using Hexiege.Presentation.UI;

// SpinnerRotator 참조 안내:
//   이 스크립트는 LoadingIndicator 추출 후 Spinner 자식에 SpinnerRotator를 자동 부착한다.
//   SpinnerRotator.cs(Assets/_Project/Scripts/Presentation/UI/Common/)가 먼저
//   컴파일되어 있어야 한다. 스크립트 오류 없이 컴파일된 상태에서 메뉴를 실행할 것.

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
        /// Lobby.unity에서 LoadingIndicator로 추출할 오브젝트 이름.
        /// 씬에서는 "LoadingScreen"으로 배치되어 있으나, 추출 시 Canvas/LoadingScreen.cs를
        /// 제거(B안)하여 UIManager에 임베드 가능한 순수 UI 프리팹으로 저장한다.
        /// </summary>
        private const string LoadingScreenObjectName = "LoadingScreen";

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

            // 1) Game.unity → ConfirmPopup (컴포넌트 타입으로 탐색, 구조 변경 없음)
            ExtractFromScene<ConfirmPopup>(GameScenePath, ConfirmPopupPrefabPath, "ConfirmPopup");

            // 2) Lobby.unity → ToastUI (컴포넌트 타입으로 탐색, 구조 변경 없음)
            //    ToastUI는 자체 Canvas + DontDestroyOnLoad를 가진 독립 오브젝트이므로
            //    그대로 추출한다. SetupUIManagerInScene에서 씬 루트에 직접 배치한다.
            ExtractFromScene<ToastUI>(LobbyScenePath, ToastUIPrefabPath, "ToastUI");

            // 3) Lobby.unity → LoadingIndicator (B안: LoadingScreen에서 비UI 컴포넌트 제거 후 추출)
            //    원본 씬은 CloseScene(true)으로 변경이 폐기되어 보존된다.
            ExtractLoadingIndicatorFromScene(LobbyScenePath, LoadingIndicatorPrefabPath);

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
        /// B안 — Lobby.unity의 LoadingScreen에서 비UI 컴포넌트를 제거한 뒤
        /// LoadingIndicator.prefab으로 저장한다.
        ///
        /// LoadingScreen은 자체 Canvas + LoadingScreen.cs 싱글턴을 가진 독립 오브젝트이므로
        /// 그대로 UIManager 하위에 넣으면 Canvas 중첩 및 DontDestroyOnLoad 충돌이 발생한다.
        /// 따라서 추출 전 UIManager와 충돌하는 컴포넌트만 씬 내 임시로 제거하고 저장한다.
        /// 씬은 CloseScene(true)으로 닫아 원본이 변경되지 않도록 보장한다.
        ///
        /// 제거 대상 컴포넌트:
        ///   - Canvas          : UIManager Canvas 안에 중첩 Canvas가 생기는 것을 방지
        ///   - CanvasScaler    : Canvas가 없으면 의미 없으므로 함께 제거
        ///   - GraphicRaycaster: Canvas가 없으면 동작하지 않으므로 함께 제거
        ///   - LoadingScreen   : DontDestroyOnLoad / OnSceneLoaded 자동 Hide / 싱글턴 제거
        ///
        /// 유지되는 구조:
        ///   LoadingIndicator (RectTransform + CanvasGroup)
        ///   └─ RootPanel (Image — 반투명 배경)
        ///       ├─ Spinner (Image — 회전 아이콘, SpinnerRotator 수동 부착 필요)
        ///       └─ StatusText (TextMeshProUGUI)
        /// </summary>
        private static void ExtractLoadingIndicatorFromScene(string scenePath, string prefabPath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                GameObject target = FindGameObjectByNameInScene(scene, LoadingScreenObjectName);
                if (target == null)
                {
                    Debug.LogWarning($"[ExtractUIManagerPrefabs] {scenePath} 에서 '{LoadingScreenObjectName}' 이름의 GameObject를 찾지 못했습니다. 건너뜁니다.");
                    return;
                }

                // Canvas 관련 컴포넌트 제거 — UIManager Canvas 안에 중첩되면 안 됨.
                // 제거 순서 중요: Canvas에 의존하는 컴포넌트(GraphicRaycaster → CanvasScaler)를
                // 먼저 제거한 뒤 Canvas를 제거해야 한다. 순서가 틀리면 Unity가 의존성 오류를 낸다.
                // 씬은 CloseScene(true)으로 폐기되므로 원본에는 영향 없음.
                var raycaster = target.GetComponent<GraphicRaycaster>();
                if (raycaster != null) Object.DestroyImmediate(raycaster);

                var canvasScaler = target.GetComponent<CanvasScaler>();
                if (canvasScaler != null) Object.DestroyImmediate(canvasScaler);

                var canvas = target.GetComponent<Canvas>();
                if (canvas != null) Object.DestroyImmediate(canvas);

                // LoadingScreen.cs 제거 — DontDestroyOnLoad + 싱글턴 + OnSceneLoaded 자동 Hide 제거.
                // UIManager가 CanvasGroup으로 직접 Show/Hide를 제어하므로 이 로직은 필요 없다.
                var loadingScreen = target.GetComponent<LoadingScreen>();
                if (loadingScreen != null) Object.DestroyImmediate(loadingScreen);

                // SpinnerRotator 자동 부착 — "Spinner" 이름의 자식 오브젝트에 추가.
                // LoadingScreen.cs가 담당하던 Z축 회전 로직(Update())을 대체한다.
                // 비활성 자식까지 포함해 탐색한다.
                AddSpinnerRotatorToSpinner(target);

                SavePrefab(target, prefabPath, "LoadingIndicator");
            }
            finally
            {
                // CloseScene(true) = 변경 폐기. 씬 원본(LoadingScreen 그대로)은 보존된다.
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// target 하위에서 이름이 "Spinner"인 첫 오브젝트를 찾아 SpinnerRotator를 추가한다.
        /// 이미 SpinnerRotator가 부착되어 있으면 중복 추가하지 않는다.
        /// Spinner 오브젝트가 없으면 경고만 출력하고 계속 진행한다.
        /// </summary>
        private static void AddSpinnerRotatorToSpinner(GameObject root)
        {
            // GetComponentsInChildren으로 비활성 자식까지 포함해 탐색.
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Spinner") continue;

                // 이미 부착된 경우 건너뜀.
                if (t.GetComponent<SpinnerRotator>() != null)
                {
                    Debug.Log("[ExtractUIManagerPrefabs] Spinner에 SpinnerRotator가 이미 있습니다. 건너뜁니다.");
                    return;
                }

                t.gameObject.AddComponent<SpinnerRotator>();
                Debug.Log("[ExtractUIManagerPrefabs] Spinner에 SpinnerRotator를 자동 부착했습니다.");
                return;
            }

            Debug.LogWarning("[ExtractUIManagerPrefabs] LoadingIndicator 하위에서 'Spinner' 오브젝트를 찾지 못했습니다. " +
                             "SpinnerRotator를 수동으로 부착해야 합니다.");
        }

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
