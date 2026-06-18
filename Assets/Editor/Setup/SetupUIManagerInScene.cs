// ============================================================================
// SetupUIManagerInScene.cs
// 전역 UI 시스템(UIManager) 및 SplashOverlay를 Login.unity 씬에 배치하는 1회성 에디터 도구.
//
// 실행 방법:
//   Unity 상단 메뉴 → Hexiege/Setup/Login씬 UIManager 배치 클릭
//
// 생성하는 계층 구조(Login.unity):
//
//   [UI Systems]                       ← 빈 GameObject(그룹)
//   └─ UIManager                       ← UIManager 컴포넌트(SingletonMonoBehaviour → DontDestroyOnLoad)
//       └─ UIManager Canvas            ← Canvas (SortingOrder 100) + CanvasScaler + GraphicRaycaster
//           └─ SafeAreaContainer       ← SafeAreaFitter (노치/홈바 회피)
//               ├─ ConfirmPopup        ← 프리팹 인스턴스(있을 때만)
//               └─ LoadingIndicator    ← 프리팹 인스턴스(있을 때만)
//
//   SplashOverlay Canvas (씬 루트)     ← Canvas (SortingOrder 200 — 항상 최상위)
//   └─ SplashOverlay                   ← CanvasGroup + SplashOverlayView (IPointerClickHandler)
//       ├─ Background                  ← Image (전체화면, RaycastTarget=true — 탭 입력 수신)
//       ├─ StatusText                  ← TextMeshProUGUI ("로딩 중...")
//       └─ TapToStartText              ← TextMeshProUGUI ("Tap to Start", alpha=0)
//
//   Toast (씬 루트)                    ← ToastUI 프리팹 — 씬 루트에 직접 배치
//                                        ToastUI.Awake()가 SetParent(null) + DontDestroyOnLoad 자동 처리.
//                                        자체 Canvas(SortingOrder:100) + SafeAreaFitter 내장.
//
// 전제:
//   - ConfirmPopup / LoadingIndicator / ToastUI 프리팹은 STEP 4(ExtractUIManagerPrefabs)에서
//     추출한다. 이 스크립트 실행 시점에 프리팹이 없을 수 있으므로,
//     프리팹이 존재할 때만 인스턴스화하고 없으면 경고 로그만 출력한다(안전 처리).
//   - UIManager의 _confirmPopup / _loadingIndicator 필드는 프리팹이 배치된 경우 자동 연결한다.
//   - SplashOverlayView의 Inspector 참조(_overlayCanvasGroup, _statusText, _tapToStartText)와
//     LoginBootstrapper._splashOverlay는 이 스크립트가 자동으로 연결한다.
//
// Toast를 별도 Canvas 안에 넣지 않는 이유:
//   ToastUI.Awake()에서 transform.SetParent(null)로 스스로 부모를 끊고
//   DontDestroyOnLoad를 적용하므로, 래퍼 Canvas 안에 넣어도 Awake 직후 빠져나간다.
//   Toast는 자체 Canvas를 보유하므로 씬 루트에 직접 배치하는 것이 올바른 방식이다.
//
// 참고:
//   - 9:16 세로 기준 해상도(1080x1920)는 프로젝트의 다른 Setup 스크립트와 동일하게 맞춘다.
//   - private [SerializeField] 주입은 SerializedObject/FindProperty 방식을 사용한다(기존 관례).
//   - 1회성 셋업 스크립트. 배치 완료 후 삭제해도 무방하다.
//
// Editor 전용 — 빌드에는 포함되지 않는다 (Assets/Editor 폴더).
// ============================================================================

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hexiege.Bootstrap;
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

        /// <summary>규칙 6: 기본 폰트 경로 (Maplestory Light SDF).</summary>
        private const string FontLightPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";
        /// <summary>규칙 6: 강조 폰트 경로 (Maplestory Bold SDF).</summary>
        private const string FontBoldPath  = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        /// <summary>9:16 세로 기준 해상도 (CanvasScaler Reference Resolution).</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        /// <summary>UIManager Canvas 정렬 순서(공통 팝업/로딩 — 일반 UI 위).</summary>
        private const int UIManagerCanvasSortingOrder = 100;

        /// <summary>
        /// SplashOverlay Canvas 정렬 순서.
        /// UIManager(100)보다 높게 설정해 초기화 중 공통 UI가 노출되는 것을 막는다.
        /// </summary>
        private const int SplashOverlayCanvasSortingOrder = 200;

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

            // 2) ConfirmPopup / LoadingIndicator 프리팹 인스턴스화(존재할 때만) + UIManager 참조 연결
            ConfirmPopup confirmPopup = InstantiatePrefabInto<ConfirmPopup>(
                ConfirmPopupPrefabPath, safeAreaGo.transform, "ConfirmPopup");

            CanvasGroup loadingIndicator = InstantiateLoadingIndicatorInto(
                LoadingIndicatorPrefabPath, safeAreaGo.transform);

            // 3) SplashOverlay — 씬 루트에 전용 Canvas(SortingOrder 200)를 생성하고
            //    그 아래에 SplashOverlayView 계층을 자동 구성한다.
            //    SortingOrder를 UIManager Canvas(100)보다 높게 설정해 초기화 중
            //    공통 UI가 SplashOverlay 위로 노출되지 않도록 한다.
            SplashOverlayView splashOverlay = CreateSplashOverlay(scene);

            // 4) ToastUI — 씬 루트에 직접 배치.
            //    Toast는 자체 Canvas + SafeAreaFitter를 내장한 독립 오브젝트다.
            //    ToastUI.Awake()가 SetParent(null) + DontDestroyOnLoad를 자동 처리하므로
            //    래퍼 Canvas 없이 씬 루트에 바로 인스턴스화한다.
            InstantiateToastAtSceneRoot(scene);

            // 5) UIManager의 private [SerializeField] 필드 자동 주입
            WireUIManagerReferences(uiManager, confirmPopup, loadingIndicator);

            // 6) LoginBootstrapper._splashOverlay 자동 주입
            WireLoginBootstrapperSplashOverlay(scene, splashOverlay);

            // 7) 씬 저장
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
            // 규칙 1: Match Width Or Height = 0 (가로 너비 기준 스케일링).
            // 세로가 긴 기기(21:9 등)에서 캔버스 논리 높이가 자연스럽게 늘어나는 방식으로 대응한다.
            scaler.matchWidthOrHeight = 0f;

            return canvasGo;
        }

        /// <summary>
        /// SplashOverlay 전용 Canvas를 씬 루트에 생성하고, 그 아래에 SplashOverlayView 계층을
        /// (Background / StatusText / TapToStartText) 구성한다.
        /// SplashOverlayView의 SerializeField 참조도 자동으로 연결한다.
        /// </summary>
        /// <returns>생성된 SplashOverlayView. 항상 non-null을 반환한다.</returns>
        private static SplashOverlayView CreateSplashOverlay(Scene scene)
        {
            // --- SplashOverlay Canvas (씬 루트, SortingOrder 200) ---
            // UIManager Canvas(100)보다 높은 순서로 배치해 초기화 중에도 항상 최상위에 표시된다.
            GameObject splashCanvasGo = CreateCanvas("SplashOverlay Canvas", SplashOverlayCanvasSortingOrder);
            SceneManager.MoveGameObjectToScene(splashCanvasGo, scene);

            // --- SplashOverlay (CanvasGroup + SplashOverlayView) ---
            // CanvasGroup: alpha / blocksRaycasts 로 페이드아웃 및 입력 제어.
            // SplashOverlayView: 상태 문구 변경, "Tap to Start" 깜빡임, FadeOut 로직을 담당.
            GameObject overlayGo = new GameObject("SplashOverlay", typeof(RectTransform));
            overlayGo.transform.SetParent(splashCanvasGo.transform, false);
            StretchFull(overlayGo.GetComponent<RectTransform>());
            CanvasGroup overlayCanvasGroup = overlayGo.AddComponent<CanvasGroup>();
            SplashOverlayView splashView = overlayGo.AddComponent<SplashOverlayView>();

            // --- Background (Image, 전체화면, RaycastTarget = true) ---
            // 규칙 4: 전체화면 배경은 SafeAreaContainer 밖(SplashOverlay 직속)에 배치한다.
            //   SafeArea 제약을 받으면 노치/홈바 영역이 배경색으로 비어 보이기 때문이다.
            // IPointerClickHandler가 탭을 수신하려면 이 Image의 RaycastTarget이 켜져 있어야 한다.
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(overlayGo.transform, false);
            StretchFull(bgGo.GetComponent<RectTransform>());
            Image bgImage = bgGo.GetComponent<Image>();
            bgImage.color = new Color(0.07f, 0.07f, 0.07f, 1f); // 기본 어두운 배경색.
            bgImage.raycastTarget = true;

            // --- SafeAreaContainer (규칙 4) ---
            // 실제 UI 요소(텍스트)는 노치/홈바 영역을 피해야 하므로 SafeAreaContainer 안에 배치한다.
            // SafeAreaFitter 컴포넌트가 런타임에 SafeArea 범위로 RectTransform을 자동 조정한다.
            GameObject safeAreaGo = new GameObject("SafeAreaContainer", typeof(RectTransform));
            safeAreaGo.transform.SetParent(overlayGo.transform, false);
            StretchFull(safeAreaGo.GetComponent<RectTransform>());
            safeAreaGo.AddComponent<SafeAreaFitter>();

            // 규칙 6: Maplestory Light SDF 폰트를 로드한다. 없으면 경고 후 기본 폰트로 진행한다.
            TMP_FontAsset fontLight = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontLightPath);
            if (fontLight == null)
                Debug.LogWarning($"[SetupUIManagerInScene] Maplestory Light SDF 폰트를 찾지 못했습니다({FontLightPath}). " +
                                 "기본 폰트로 생성됩니다. Inspector에서 수동으로 교체해 주세요.");

            // --- StatusText (TextMeshProUGUI, "로딩 중...") ---
            // 초기화 진행 중 상태 문구. SafeAreaContainer 안에 배치해 노치/홈바에 가리지 않도록 한다.
            GameObject statusGo = new GameObject("StatusText", typeof(RectTransform));
            statusGo.transform.SetParent(safeAreaGo.transform, false);
            TextMeshProUGUI statusText = statusGo.AddComponent<TextMeshProUGUI>();
            statusText.text = "로딩 중...";
            statusText.fontSize = 36;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.white;
            if (fontLight != null) statusText.font = fontLight; // 규칙 6.
            RectTransform statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.1f, 0.45f);
            statusRt.anchorMax = new Vector2(0.9f, 0.55f);
            statusRt.offsetMin = Vector2.zero;
            statusRt.offsetMax = Vector2.zero;

            // --- TapToStartText (TextMeshProUGUI, "Tap to Start", alpha=0) ---
            // ShowTapToStart() 호출 시 alpha 0→1 깜빡임으로 표시된다.
            // SafeAreaContainer 안에 배치해 노치/홈바에 가리지 않도록 한다.
            // Awake에서 alpha=0으로 초기화되므로, 에디터에서 값을 미리 0으로 설정해도 무방하다.
            GameObject tapGo = new GameObject("TapToStartText", typeof(RectTransform));
            tapGo.transform.SetParent(safeAreaGo.transform, false);
            TextMeshProUGUI tapText = tapGo.AddComponent<TextMeshProUGUI>();
            tapText.text = "Tap to Start";
            tapText.fontSize = 42;
            tapText.alignment = TextAlignmentOptions.Center;
            tapText.color = Color.white;
            tapText.alpha = 0f;
            if (fontLight != null) tapText.font = fontLight; // 규칙 6.
            RectTransform tapRt = tapGo.GetComponent<RectTransform>();
            tapRt.anchorMin = new Vector2(0.1f, 0.45f);
            tapRt.anchorMax = new Vector2(0.9f, 0.55f);
            tapRt.offsetMin = Vector2.zero;
            tapRt.offsetMax = Vector2.zero;

            // --- SplashOverlayView Inspector 참조 자동 주입 ---
            SerializedObject soView = new SerializedObject(splashView);
            soView.FindProperty("_overlayCanvasGroup").objectReferenceValue = overlayCanvasGroup;
            soView.FindProperty("_statusText").objectReferenceValue = statusText;
            soView.FindProperty("_tapToStartText").objectReferenceValue = tapText;
            soView.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[SetupUIManagerInScene] SplashOverlay 계층 생성 및 참조 연결 완료.");
            return splashView;
        }

        /// <summary>
        /// 씬의 LoginBootstrapper를 찾아 _splashOverlay 필드에 생성된 SplashOverlayView를 주입한다.
        /// LoginBootstrapper가 없으면 경고 로그만 출력하고 넘어간다(개발 중 직접 진입 대비).
        /// </summary>
        private static void WireLoginBootstrapperSplashOverlay(Scene scene, SplashOverlayView splashView)
        {
            if (splashView == null) return;

            // 씬 전체 루트를 순회하며 LoginBootstrapper 컴포넌트를 탐색한다.
            LoginBootstrapper bootstrapper = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bootstrapper = root.GetComponentInChildren<LoginBootstrapper>(true);
                if (bootstrapper != null) break;
            }

            if (bootstrapper == null)
            {
                Debug.LogWarning("[SetupUIManagerInScene] LoginBootstrapper를 씬에서 찾지 못했습니다. " +
                                 "_splashOverlay 필드를 Inspector에서 수동으로 연결해 주세요.");
                return;
            }

            SerializedObject so = new SerializedObject(bootstrapper);
            SerializedProperty prop = so.FindProperty("_splashOverlay");
            if (prop != null)
            {
                prop.objectReferenceValue = splashView;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[SetupUIManagerInScene] LoginBootstrapper._splashOverlay 자동 연결 완료.");
            }
            else
            {
                Debug.LogWarning("[SetupUIManagerInScene] LoginBootstrapper에서 _splashOverlay 프로퍼티를 " +
                                 "찾지 못했습니다. Inspector에서 수동으로 연결해 주세요.");
            }
        }

        /// <summary>
        /// ToastUI 프리팹을 씬 루트에 직접 인스턴스화한다.
        /// Toast는 자체 Canvas를 보유하고 Awake()에서 SetParent(null) + DontDestroyOnLoad를
        /// 스스로 처리하므로, 별도 래퍼 Canvas 없이 씬 루트에 배치하는 것이 올바른 방식이다.
        /// </summary>
        private static void InstantiateToastAtSceneRoot(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToastUIPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[SetupUIManagerInScene] ToastUI 프리팹이 아직 없습니다({ToastUIPrefabPath}). " +
                                 "STEP 4(UIManager 프리팹 추출)를 먼저 실행한 뒤 다시 배치하세요. 이 항목은 건너뜁니다.");
                return;
            }

            // 프리팹 연결을 유지한 채 씬 루트(부모 없음)에 인스턴스화한다.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            Debug.Log("[SetupUIManagerInScene] ToastUI 프리팹을 씬 루트에 인스턴스화 완료.");
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
