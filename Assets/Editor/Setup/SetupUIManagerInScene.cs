// ============================================================================
// SetupUIManagerInScene.cs
// 전역 UI 시스템(UIManager)을 Login.unity 씬에 배치하는 1회성 에디터 도구.
//
// 실행 방법:
//   Unity 상단 메뉴 → Hexiege/Setup/Login씬 UIManager 배치 클릭
//
// 생성하는 계층 구조(Login.unity):
//   [UI Systems]                       ← 빈 GameObject(그룹)
//   ├─ UIManager                       ← UIManager 컴포넌트(SingletonMonoBehaviour → DontDestroyOnLoad)
//   │   └─ UIManager Canvas            ← Canvas (SortingOrder 100) + CanvasScaler + GraphicRaycaster
//   │       └─ SafeAreaContainer       ← SafeAreaFitter (노치/홈바 회피)
//   │           ├─ ConfirmPopup        ← 프리팹 인스턴스(있을 때만)
//   │           └─ LoadingIndicator    ← 프리팹 인스턴스(있을 때만)
//   └─ Toast Canvas                    ← Canvas (SortingOrder 200) + CanvasScaler + GraphicRaycaster
//       └─ ToastUI                     ← 프리팹 인스턴스(있을 때만)
//
// 전제:
//   - ConfirmPopup / LoadingIndicator / ToastUI 프리팹은 STEP 4(ExtractUIManagerPrefabs)에서
//     사용자가 수동으로 추출한다. 이 스크립트 실행 시점에 프리팹이 없을 수 있으므로,
//     프리팹이 존재할 때만 인스턴스화하고 없으면 경고 로그만 출력한다(안전 처리).
//   - UIManager의 _confirmPopup / _loadingIndicator 필드는 프리팹이 배치된 경우 자동 연결한다.
//
// 참고:
//   - 9:16 세로 기준 해상도(1080x1920)는 프로젝트의 다른 Setup 스크립트와 동일하게 맞춘다.
//   - private [SerializeField] 주입은 SerializedObject/FindProperty 방식을 사용한다(기존 관례).
//   - 1회성 셋업 스크립트. 배치 완료 후 삭제해도 무방하다.
//
// Editor 전용 — 빌드에는 포함되지 않는다 (Assets/Editor 폴더).
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hexiege.Presentation;

namespace HexiegeEditor
{
    /// <summary>
    /// Login.unity 씬에 전역 UIManager 계층을 배치하고, 프리팹을 인스턴스화하여
    /// UIManager 참조를 자동 연결하는 에디터 도구.
    /// </summary>
    public static class SetupUIManagerInScene
    {
        // ====================================================================
        // 상수 — 경로/해상도/정렬 순서
        // ====================================================================

        private const string LoginScenePath = "Assets/_Project/Scenes/Login.unity";

        private const string ConfirmPopupPrefabPath     = "Assets/_Project/Prefabs/UI/ConfirmPopup.prefab";
        private const string LoadingIndicatorPrefabPath = "Assets/_Project/Prefabs/UI/LoadingIndicator.prefab";
        private const string ToastUIPrefabPath          = "Assets/_Project/Prefabs/UI/ToastUI.prefab";

        /// <summary>9:16 세로 기준 해상도 (CanvasScaler Reference Resolution).</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        /// <summary>UIManager Canvas 정렬 순서(공통 팝업/로딩 — 일반 UI 위).</summary>
        private const int UIManagerCanvasSortingOrder = 100;

        /// <summary>Toast Canvas 정렬 순서(가장 위 — 토스트는 항상 최상위).</summary>
        private const int ToastCanvasSortingOrder = 200;

        // ====================================================================
        // 메뉴 진입점
        // ====================================================================

        /// <summary>
        /// 메뉴 클릭 시 실행. Login.unity를 열어 UIManager 계층을 구성하고 저장한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Login씬 UIManager 배치")]
        public static void Setup()
        {
            // Login.unity를 단일(Single) 모드로 연다.
            Scene scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);

            // 이미 [UI Systems]가 있으면 중복 배치를 막는다.
            if (FindRootByName(scene, "[UI Systems]") != null)
            {
                Debug.LogWarning("[SetupUIManagerInScene] 씬에 이미 [UI Systems]가 존재합니다. 중복 배치를 막기 위해 중단합니다. " +
                                 "다시 배치하려면 기존 [UI Systems]를 먼저 삭제하세요.");
                return;
            }

            // 1) [UI Systems] 그룹 + UIManager + UIManager Canvas + SafeAreaContainer 생성
            GameObject uiSystems = new GameObject("[UI Systems]");
            SceneManager.MoveGameObjectToScene(uiSystems, scene);

            GameObject uiManagerGo = new GameObject("UIManager");
            uiManagerGo.transform.SetParent(uiSystems.transform, false);
            UIManager uiManager = uiManagerGo.AddComponent<UIManager>();

            // UIManager Canvas (SortingOrder 100)
            GameObject uiManagerCanvasGo = CreateCanvas("UIManager Canvas", UIManagerCanvasSortingOrder);
            uiManagerCanvasGo.transform.SetParent(uiManagerGo.transform, false);

            // SafeAreaContainer (SafeAreaFitter) — 화면 전체 stretch
            GameObject safeAreaGo = new GameObject("SafeAreaContainer", typeof(RectTransform));
            safeAreaGo.transform.SetParent(uiManagerCanvasGo.transform, false);
            StretchFull(safeAreaGo.GetComponent<RectTransform>());
            safeAreaGo.AddComponent<SafeAreaFitter>();

            // 2) Toast Canvas (SortingOrder 200)
            GameObject toastCanvasGo = CreateCanvas("Toast Canvas", ToastCanvasSortingOrder);
            toastCanvasGo.transform.SetParent(uiSystems.transform, false);

            // 3) 프리팹 인스턴스화(존재할 때만) + UIManager 참조 연결
            ConfirmPopup confirmPopup = InstantiatePrefabInto<ConfirmPopup>(
                ConfirmPopupPrefabPath, safeAreaGo.transform, "ConfirmPopup");

            CanvasGroup loadingIndicator = InstantiateLoadingIndicatorInto(
                LoadingIndicatorPrefabPath, safeAreaGo.transform);

            InstantiatePrefabInto<ToastUI>(
                ToastUIPrefabPath, toastCanvasGo.transform, "ToastUI");

            // 4) UIManager의 private [SerializeField] 필드 자동 주입
            WireUIManagerReferences(uiManager, confirmPopup, loadingIndicator);

            // 5) 씬 저장
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, LoginScenePath);
            if (saved)
                Debug.Log("[SetupUIManagerInScene] Login.unity에 [UI Systems] 배치 및 저장 완료.");
            else
                Debug.LogError("[SetupUIManagerInScene] Login.unity 저장에 실패했습니다.");
        }

        // ====================================================================
        // 생성 헬퍼
        // ====================================================================

        /// <summary>
        /// Screen Space - Overlay Canvas + CanvasScaler(9:16) + GraphicRaycaster를 가진 GameObject 생성.
        /// </summary>
        /// <param name="name">생성할 Canvas GameObject 이름.</param>
        /// <param name="sortingOrder">Canvas 정렬 순서.</param>
        private static GameObject CreateCanvas(string name, int sortingOrder)
        {
            GameObject canvasGo = new GameObject(name,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // 세로(높이) 기준으로 맞춤.

            return canvasGo;
        }

        /// <summary>
        /// 프리팹이 존재하면 부모 아래에 인스턴스화하고, 부착된 컴포넌트 T를 반환한다.
        /// 프리팹이 없으면 경고 로그만 출력하고 null을 반환한다(안전 처리).
        /// </summary>
        /// <typeparam name="T">프리팹 루트에 부착된 컴포넌트 타입.</typeparam>
        /// <param name="prefabPath">프리팹 에셋 경로.</param>
        /// <param name="parent">인스턴스를 둘 부모 Transform.</param>
        /// <param name="label">로그용 이름.</param>
        private static T InstantiatePrefabInto<T>(string prefabPath, Transform parent, string label)
            where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[SetupUIManagerInScene] {label} 프리팹이 아직 없습니다({prefabPath}). " +
                                 "STEP 4(UIManager 프리팹 추출)를 먼저 실행한 뒤 다시 배치하세요. 이 항목은 건너뜁니다.");
                return null;
            }

            // 프리팹 연결을 유지한 채 씬에 인스턴스화한다(에디터 전용 API).
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetParent(parent, false);

            T component = instance.GetComponent<T>();
            if (component == null)
                Debug.LogWarning($"[SetupUIManagerInScene] {label} 프리팹에 {typeof(T).Name} 컴포넌트가 없습니다.");

            Debug.Log($"[SetupUIManagerInScene] {label} 프리팹 인스턴스화 완료.");
            return component;
        }

        /// <summary>
        /// LoadingIndicator 프리팹을 인스턴스화하고, 루트의 CanvasGroup을 반환한다.
        /// LoadingIndicator는 전용 컴포넌트가 없으므로 CanvasGroup으로 다룬다(없으면 추가).
        /// 프리팹이 없으면 경고 로그만 출력하고 null을 반환한다.
        /// </summary>
        private static CanvasGroup InstantiateLoadingIndicatorInto(string prefabPath, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[SetupUIManagerInScene] LoadingIndicator 프리팹이 아직 없습니다({prefabPath}). " +
                                 "STEP 4를 먼저 실행한 뒤 다시 배치하세요. 이 항목은 건너뜁니다.");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetParent(parent, false);

            // UIManager는 LoadingIndicator를 CanvasGroup으로 제어한다. 없으면 추가한다.
            CanvasGroup cg = instance.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = instance.AddComponent<CanvasGroup>();

            Debug.Log("[SetupUIManagerInScene] LoadingIndicator 프리팹 인스턴스화 완료.");
            return cg;
        }

        // ====================================================================
        // 참조 주입
        // ====================================================================

        /// <summary>
        /// UIManager의 private [SerializeField] 필드(_confirmPopup, _loadingIndicator)를
        /// SerializedObject 방식으로 주입한다. 값이 null이면 해당 항목만 건너뛴다.
        /// </summary>
        private static void WireUIManagerReferences(UIManager uiManager,
            ConfirmPopup confirmPopup, CanvasGroup loadingIndicator)
        {
            SerializedObject so = new SerializedObject(uiManager);

            if (confirmPopup != null)
            {
                SerializedProperty prop = so.FindProperty("_confirmPopup");
                if (prop != null) prop.objectReferenceValue = confirmPopup;
            }

            if (loadingIndicator != null)
            {
                SerializedProperty prop = so.FindProperty("_loadingIndicator");
                if (prop != null) prop.objectReferenceValue = loadingIndicator;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ====================================================================
        // 공용 유틸
        // ====================================================================

        /// <summary>
        /// RectTransform을 부모 영역 전체로 stretch(anchorMin=0,0 / anchorMax=1,1 / offset=0)한다.
        /// </summary>
        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 씬 루트 오브젝트 중 주어진 이름의 GameObject를 반환한다(없으면 null).
        /// </summary>
        private static GameObject FindRootByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }
    }
}
