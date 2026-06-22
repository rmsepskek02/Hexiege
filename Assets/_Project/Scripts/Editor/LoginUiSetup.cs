// ============================================================================
// LoginUiSetup.cs
// 로그인 UI 마무리 작업용 1회성 에디터 스크립트.
//
// 목적 (재작성):
//   이전 잘못된 방향(AnonymousWarningPopup 을 UIManager Canvas 로 이동)을 되돌리고,
//   두 팝업(AnonymousWarningPopup / NetworkErrorPopup)에 각각 독립 Canvas 를 추가해
//   BlockingOverlay 위에 항상 렌더링되도록 하는 올바른 방향으로 재작성.
//
// 메뉴 항목:
//   • BUG-A: LoadingIndicator 이동 (UIManager Canvas 직속 + 전체화면 stretch)
//   • BUG-B: AnonymousWarningPopup 복원 및 Canvas 추가 (Login Canvas 로 복귀 + 독립 Canvas)
//   • BUG-B: NetworkErrorPopup Canvas 추가 (독립 Canvas + 컴포넌트 교체)
//
// 동작 원칙:
//   ⚠️ 이 스크립트는 Login.unity 를 새로 열지 않는다.
//      현재 열려 있는 씬을 그대로 수정한다 (OpenScene 호출 없음).
//      현재 씬이 Login.unity 가 아니면 경고 후 중단한다.
//
// ※ 1회성 작업 완료 후 이 파일은 삭제해도 무방하다.
// ============================================================================

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.Editor.Setup
{
    /// <summary>
    /// 로그인 UI 마무리 작업(팝업 Canvas 추가 + 슬롯 연결)을 위한 에디터 메뉴 모음.
    /// 현재 열린 Login.unity 씬을 직접 수정한다.
    /// </summary>
    public static class LoginUiSetup
    {
        // 씬 이름 (현재 열린 씬이 이 이름인지 확인용)
        private const string LoginSceneName = "Login";

        // 씬 오브젝트 이름
        private const string UIManagerCanvasName       = "UIManager Canvas";
        private const string LoginCanvasName           = "Canvas";
        private const string SafeAreaContainerName     = "SafeAreaContainer";
        private const string BlockingOverlayName       = "BlockingOverlay";
        private const string NetworkErrorPopupName     = "NetworkErrorPopup";
        private const string LoadingIndicatorName      = "LoadingIndicator";
        private const string AnonymousWarningPopupName = "AnonymousWarningPopup";

        // 컴포넌트 / 직렬화 필드 이름
        private const string UIManagerTypeName         = "UIManager";
        private const string LoginRootViewTypeName     = "LoginRootView";
        private const string AnonymousWarningPopupType = "AnonymousWarningPopup";
        private const string NetworkErrorPopupType     = "NetworkErrorPopup";
        private const string ConfirmPopupType          = "ConfirmPopup";

        private const string LoadingIndicatorField     = "_loadingIndicator";
        private const string NetworkErrorPopupField    = "_networkErrorPopup";
        private const string AnonymousWarningField     = "_anonymousWarningPopup";

        // 독립 Canvas 의 정렬 순서. BlockingOverlay(UIManager Canvas) 보다 위에 그려지도록 높게 설정.
        private const int PopupSortingOrder = 200;

        // LoadingIndicator 독립 Canvas 의 정렬 순서.
        // 팝업(200) 보다도 위에 그려져야 모든 UI 최상단에 로딩 표시가 보인다.
        private const int LoadingIndicatorSortingOrder = 300;

        // ====================================================================
        // 메뉴 1 — BUG-A: LoadingIndicator 이동
        // ====================================================================

        /// <summary>
        /// LoadingIndicator 를 UIManager Canvas 직속으로 이동시키고 전체화면 stretch 로 설정한다.
        /// LoadingIndicator 는 DontDestroyOnLoad 오브젝트(UIManager) 하위에 있어 이름 탐색이 불안정하므로,
        /// UIManager 컴포넌트의 _loadingIndicator 필드(CanvasGroup)를 통해 직접 가져온다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Login UI — BUG-A: LoadingIndicator 이동")]
        private static void MoveLoadingIndicator()
        {
            // 현재 씬이 Login.unity 인지 확인
            if (!EnsureLoginSceneOpen(out var scene)) return;

            // 1) UIManager 컴포넌트 찾기 (비활성 포함, 타입명으로 필터)
            var uiManager = FindComponentByTypeName(UIManagerTypeName);
            if (uiManager == null)
            {
                Report(false, $"'{UIManagerTypeName}' 컴포넌트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 2) UIManager 의 _loadingIndicator 필드(CanvasGroup) 읽기
            var uiManagerSO = new SerializedObject(uiManager);
            var loadingProp = uiManagerSO.FindProperty(LoadingIndicatorField);
            if (loadingProp == null || loadingProp.objectReferenceValue == null)
            {
                Report(false,
                    $"'{UIManagerTypeName}' 의 '{LoadingIndicatorField}' 필드(CanvasGroup)가 비어 있거나 없습니다.");
                return;
            }

            // 3) CanvasGroup → 해당 gameObject 가 LoadingIndicator
            var loadingCanvasGroup = loadingProp.objectReferenceValue as CanvasGroup;
            if (loadingCanvasGroup == null)
            {
                Report(false, $"'{LoadingIndicatorField}' 필드가 CanvasGroup 타입이 아닙니다.");
                return;
            }
            var loadingGO = loadingCanvasGroup.gameObject;

            // 4) UIManager Canvas GameObject 찾기
            var uiManagerCanvas = FindGameObjectByName(UIManagerCanvasName);
            if (uiManagerCanvas == null)
            {
                Report(false, $"'{UIManagerCanvasName}' 오브젝트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 5) 부모가 SafeAreaContainer 면 UIManager Canvas 직속으로 이동.
            //    이미 UIManager Canvas 직속이면 건너뜀.
            var currentParent = loadingGO.transform.parent;
            bool moved = false;
            if (currentParent != null &&
                currentParent.name == SafeAreaContainerName)
            {
                // Undo 에 부모 변경 기록 (worldPositionStays:false → UI 로컬 좌표 유지)
                Undo.SetTransformParent(
                    loadingGO.transform, uiManagerCanvas.transform,
                    "Move LoadingIndicator to UIManager Canvas");
                loadingGO.transform.SetParent(uiManagerCanvas.transform, worldPositionStays: false);
                moved = true;
            }
            else if (currentParent == uiManagerCanvas.transform)
            {
                // 이미 직속 — 부모 이동 생략, RectTransform 만 보정.
            }

            // 6) 전체화면 stretch 설정
            Undo.RecordObject(loadingGO.transform, "Stretch LoadingIndicator");
            StretchFullScreen(loadingGO);

            EditorUtility.SetDirty(loadingGO);
            SaveScene(scene);

            Report(true,
                moved
                    ? $"'{LoadingIndicatorName}' 를 '{UIManagerCanvasName}' 직속으로 이동 + 전체화면 stretch 완료."
                    : $"'{LoadingIndicatorName}' 는 이미 '{UIManagerCanvasName}' 직속입니다. 전체화면 stretch 만 적용했습니다.");
        }

        // ====================================================================
        // 메뉴 — LoadingIndicator 독립 Canvas 추가
        // ====================================================================

        /// <summary>
        /// LoadingIndicator GameObject 에 독립 Canvas(overrideSorting, sortingOrder=300) 와
        /// GraphicRaycaster 를 추가한다.
        /// BUG-B 수정으로 팝업들이 독립 Canvas(sortingOrder=200)를 갖게 되면서,
        /// UIManager Canvas(sortingOrder=100) 에 속한 LoadingIndicator 가 팝업 뒤에 가려지는 문제를 해결한다.
        /// LoadingIndicator 는 항상 모든 UI 위에 표시되어야 하므로 가장 높은 sortingOrder=300 을 부여한다.
        ///
        /// UIManager._loadingIndicator 는 CanvasGroup 참조이므로, 같은 GameObject 에 Canvas 가 추가돼도
        /// 참조가 깨지지 않는다(코드 수정 불필요).
        /// </summary>
        [MenuItem("Hexiege/Setup/Login UI — LoadingIndicator 독립 Canvas 추가")]
        private static void AddLoadingIndicatorCanvas()
        {
            // 현재 씬이 Login.unity 인지 확인
            if (!EnsureLoginSceneOpen(out var scene)) return;

            // 1) UIManager 컴포넌트 찾기 (비활성 포함, 타입명으로 필터)
            var uiManager = FindComponentByTypeName(UIManagerTypeName);
            if (uiManager == null)
            {
                Report(false, $"'{UIManagerTypeName}' 컴포넌트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 2) UIManager 의 _loadingIndicator 필드(CanvasGroup) 읽기
            var uiManagerSO = new SerializedObject(uiManager);
            var loadingProp = uiManagerSO.FindProperty(LoadingIndicatorField);
            if (loadingProp == null || loadingProp.objectReferenceValue == null)
            {
                Report(false,
                    $"'{UIManagerTypeName}' 의 '{LoadingIndicatorField}' 필드(CanvasGroup)가 비어 있거나 없습니다.");
                return;
            }

            // 3) CanvasGroup → 해당 gameObject 가 LoadingIndicator
            var loadingCanvasGroup = loadingProp.objectReferenceValue as CanvasGroup;
            if (loadingCanvasGroup == null)
            {
                Report(false, $"'{LoadingIndicatorField}' 필드가 CanvasGroup 타입이 아닙니다.");
                return;
            }
            var loadingGO = loadingCanvasGroup.gameObject;

            // 4) Canvas 컴포넌트 추가(없으면) 또는 sortingOrder 보정(있으면)
            var canvas = loadingGO.GetComponent<Canvas>();
            if (canvas == null)
            {
                // 새로 생성되는 컴포넌트는 AddComponent 가 Undo 에 등록한다.
                canvas = Undo.AddComponent<Canvas>(loadingGO);
            }
            // 기존 Canvas 가 있더라도 설정값 변경 전 Undo 에 기록한다.
            Undo.RecordObject(canvas, "Configure LoadingIndicator Canvas");
            canvas.overrideSorting = true;
            canvas.sortingOrder = LoadingIndicatorSortingOrder;

            // 5) GraphicRaycaster 추가 (없는 경우에만)
            var raycaster = loadingGO.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                Undo.AddComponent<GraphicRaycaster>(loadingGO);
            }

            // 6) 씬 dirty 마킹 + 저장
            EditorUtility.SetDirty(loadingGO);
            SaveScene(scene);

            Report(true,
                $"'{LoadingIndicatorName}' 에 독립 Canvas(overrideSorting=true, " +
                $"sortingOrder={LoadingIndicatorSortingOrder}) + GraphicRaycaster 추가 완료.");
        }

        // ====================================================================
        // 메뉴 2 — BUG-B: AnonymousWarningPopup 복원 및 Canvas 추가
        // ====================================================================

        /// <summary>
        /// 잘못 이동된 AnonymousWarningPopup 을 Login Canvas > SafeAreaContainer 로 되돌리고
        /// 독립 Canvas(overrideSorting) 를 추가해 BlockingOverlay 위에 그려지도록 한다.
        /// 불필요해진 자식 BlockingOverlay 는 제거하고, LoginRootView 슬롯을 재연결한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Login UI — BUG-B: AnonymousWarningPopup 복원 및 Canvas 추가")]
        private static void RestoreAnonymousWarningPopup()
        {
            if (!EnsureLoginSceneOpen(out var scene)) return;

            // 1) AnonymousWarningPopup 오브젝트 찾기
            var popupGO = FindGameObjectByName(AnonymousWarningPopupName);
            if (popupGO == null)
            {
                Report(false, $"'{AnonymousWarningPopupName}' 오브젝트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 2) Login Canvas > SafeAreaContainer 찾기
            var loginSafeArea = FindLoginCanvasSafeAreaContainer();
            if (loginSafeArea == null)
            {
                Report(false,
                    $"Login Canvas('{LoginCanvasName}', SortingOrder=0) 하위에서 " +
                    $"'{SafeAreaContainerName}' 를 찾을 수 없습니다.");
                return;
            }

            // 3) 부모를 Login Canvas > SafeAreaContainer 로 되돌림
            if (popupGO.transform.parent != loginSafeArea)
            {
                Undo.SetTransformParent(
                    popupGO.transform, loginSafeArea,
                    "Restore AnonymousWarningPopup parent");
                popupGO.transform.SetParent(loginSafeArea, worldPositionStays: false);
            }

            // 4) 전체화면 stretch
            Undo.RecordObject(popupGO.transform, "Stretch AnonymousWarningPopup");
            StretchFullScreen(popupGO);

            // 5) 독립 Canvas + GraphicRaycaster 추가
            AddPopupCanvas(popupGO);

            // 6) 자식 BlockingOverlay 제거
            RemoveChildByName(popupGO, BlockingOverlayName);

            // 7) LoginRootView 의 _anonymousWarningPopup 슬롯 재연결
            int reconnected = ReconnectSlot(
                AnonymousWarningField, popupGO, AnonymousWarningPopupType);

            EditorUtility.SetDirty(popupGO);
            SaveScene(scene);

            Report(true,
                $"'{AnonymousWarningPopupName}' 를 Login Canvas/{SafeAreaContainerName} 로 복원 + " +
                $"독립 Canvas(sortingOrder={PopupSortingOrder}) 추가 완료. 재연결된 슬롯: {reconnected}개.");
        }

        // ====================================================================
        // 메뉴 3 — BUG-B: NetworkErrorPopup Canvas 추가
        // ====================================================================

        /// <summary>
        /// NetworkErrorPopup 에 독립 Canvas 를 추가하고,
        /// 기존 ConfirmPopup 컴포넌트를 제거한 뒤 NetworkErrorPopup 컴포넌트를 부착한다.
        /// 이 오브젝트는 이미 Login Canvas > SafeAreaContainer 에 있으므로 이동하지 않는다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Login UI — BUG-B: NetworkErrorPopup Canvas 추가")]
        private static void SetupNetworkErrorPopup()
        {
            if (!EnsureLoginSceneOpen(out var scene)) return;

            // 1) NetworkErrorPopup 오브젝트 찾기
            var popupGO = FindGameObjectByName(NetworkErrorPopupName);
            if (popupGO == null)
            {
                Report(false, $"'{NetworkErrorPopupName}' 오브젝트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 2~3) 독립 Canvas + GraphicRaycaster 추가
            AddPopupCanvas(popupGO);

            // 4) 자식 BlockingOverlay 제거
            RemoveChildByName(popupGO, BlockingOverlayName);

            // 5) 기존 ConfirmPopup 컴포넌트 제거 (있으면)
            var confirmComp = popupGO.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().Name == ConfirmPopupType);
            if (confirmComp != null)
            {
                Undo.DestroyObjectImmediate(confirmComp);
            }

            // 6) NetworkErrorPopup 컴포넌트 추가 (없으면)
            var netComp = popupGO.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().Name == NetworkErrorPopupType);
            if (netComp == null)
            {
                // Hexiege.Presentation.NetworkErrorPopup 타입을 로드해 부착한다.
                var netType = FindTypeByName(NetworkErrorPopupType);
                if (netType == null)
                {
                    Report(false,
                        $"'{NetworkErrorPopupType}' 스크립트 타입을 찾을 수 없습니다. 컴파일이 완료됐는지 확인하세요.");
                    return;
                }
                netComp = Undo.AddComponent(popupGO, netType);
            }

            // 7) LoginRootView 의 _networkErrorPopup 슬롯 연결
            int reconnected = ReconnectSlot(
                NetworkErrorPopupField, popupGO, NetworkErrorPopupType);

            // 8) NetworkErrorPopup 컴포넌트의 _panel / _confirmButton 슬롯 자동 연결
            //    씬 구조: NetworkErrorPopup > Panel(AnimatedPanel) > ButtonContainer > ConfirmButton
            netComp = popupGO.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().Name == NetworkErrorPopupType);
            if (netComp != null)
            {
                var so = new SerializedObject(netComp);

                // _panel: 직속 자식 "Panel"의 AnimatedPanel 컴포넌트
                var panelTransform = popupGO.transform
                    .Cast<Transform>()
                    .FirstOrDefault(t => t.name == "Panel");
                if (panelTransform != null)
                {
                    var animatedPanel = panelTransform.GetComponent<MonoBehaviour>();
                    if (animatedPanel != null && animatedPanel.GetType().Name == "AnimatedPanel")
                    {
                        var panelProp = so.FindProperty("_panel");
                        if (panelProp != null)
                        {
                            panelProp.objectReferenceValue = animatedPanel;
                        }
                    }

                    // _confirmButton: Panel > ButtonContainer > ConfirmButton
                    var buttonContainer = panelTransform
                        .Cast<Transform>()
                        .FirstOrDefault(t => t.name == "ButtonContainer");
                    if (buttonContainer != null)
                    {
                        var confirmTransform = buttonContainer
                            .Cast<Transform>()
                            .FirstOrDefault(t => t.name == "ConfirmButton");
                        if (confirmTransform != null)
                        {
                            var btn = confirmTransform.GetComponent<Button>();
                            var btnProp = so.FindProperty("_confirmButton");
                            if (btnProp != null && btn != null)
                            {
                                btnProp.objectReferenceValue = btn;
                            }
                        }
                    }
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(netComp);
            }

            EditorUtility.SetDirty(popupGO);
            SaveScene(scene);

            Report(true,
                $"'{NetworkErrorPopupName}' 에 독립 Canvas(sortingOrder={PopupSortingOrder}) 추가 + " +
                $"컴포넌트 교체(ConfirmPopup 제거 → NetworkErrorPopup 추가) 완료. 재연결된 슬롯: {reconnected}개.");
        }

        // ====================================================================
        // 내부 헬퍼 — Canvas / 자식 처리
        // ====================================================================

        /// <summary>
        /// 팝업 오브젝트에 독립 Canvas(overrideSorting) 와 GraphicRaycaster 를 추가한다.
        /// 이미 존재하면 설정값만 보정한다.
        /// </summary>
        private static void AddPopupCanvas(GameObject go)
        {
            // Canvas 컴포넌트 (없으면 추가)
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = Undo.AddComponent<Canvas>(go);
            }
            Undo.RecordObject(canvas, "Configure Popup Canvas");
            canvas.overrideSorting = true;
            canvas.sortingOrder = PopupSortingOrder;

            // GraphicRaycaster (없으면 추가) — Canvas 가 입력을 받으려면 필요.
            var raycaster = go.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                Undo.AddComponent<GraphicRaycaster>(go);
            }
        }

        /// <summary>
        /// 부모 오브젝트의 직속 자식 중 이름이 일치하는 것을 찾아 제거한다.
        /// </summary>
        private static void RemoveChildByName(GameObject parent, string childName)
        {
            var child = parent.transform
                .Cast<Transform>()
                .FirstOrDefault(t => t.name == childName);

            if (child != null)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        // ====================================================================
        // 내부 헬퍼 — 탐색
        // ====================================================================

        /// <summary>
        /// Login Canvas(이름 "Canvas", SortingOrder=0) 직속 자식 중
        /// 이름이 SafeAreaContainer 인 Transform 을 반환한다.
        /// </summary>
        private static Transform FindLoginCanvasSafeAreaContainer()
        {
            // 씬의 모든 Canvas 중 이름이 "Canvas" 이고 sortingOrder 가 0 인 것을 Login Canvas 로 본다.
            var loginCanvas = Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c != null && c.name == LoginCanvasName && c.sortingOrder == 0);

            if (loginCanvas == null) return null;

            return loginCanvas.transform
                .Cast<Transform>()
                .FirstOrDefault(t => t.name == SafeAreaContainerName);
        }

        /// <summary>
        /// 지정한 직렬화 필드를 가진 모든 LoginRootView 의 슬롯을
        /// popupGO 의 expectedTypeName 컴포넌트로 연결한다. 반환값: 연결된 슬롯 수.
        /// </summary>
        private static int ReconnectSlot(string fieldName, GameObject popupGO, string expectedTypeName)
        {
            // popupGO 에서 기대 타입 컴포넌트를 찾는다.
            var component = popupGO.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().Name == expectedTypeName);
            if (component == null) return 0;

            int count = 0;
            var allMonoBehaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var mb in allMonoBehaviours)
            {
                if (mb == null || mb.GetType().Name != LoginRootViewTypeName) continue;

                var so = new SerializedObject(mb);
                var prop = so.FindProperty(fieldName);
                if (prop == null) continue;

                prop.objectReferenceValue = component;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mb);
                count++;
            }

            return count;
        }

        /// <summary>
        /// 현재 열린 씬에서 이름이 일치하는 GameObject 를 반환한다(비활성 포함).
        /// </summary>
        private static GameObject FindGameObjectByName(string goName)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == goName)?.gameObject;
        }

        /// <summary>
        /// 현재 열린 씬에서 타입 이름이 일치하는 첫 번째 MonoBehaviour 를 반환한다(비활성 포함).
        /// </summary>
        private static MonoBehaviour FindComponentByTypeName(string typeName)
        {
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(mb => mb != null && mb.GetType().Name == typeName);
        }

        /// <summary>
        /// 로드된 모든 어셈블리에서 타입 이름이 일치하는 System.Type 을 반환한다.
        /// AddComponent 에 넘길 컴포넌트 타입을 동적으로 얻을 때 사용.
        /// </summary>
        private static System.Type FindTypeByName(string typeName)
        {
            return System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    // 일부 어셈블리는 타입 로드에 실패할 수 있으므로 방어적으로 처리한다.
                    try { return a.GetTypes(); }
                    catch { return System.Array.Empty<System.Type>(); }
                })
                .FirstOrDefault(t => t.Name == typeName && typeof(Component).IsAssignableFrom(t));
        }

        // ====================================================================
        // 내부 헬퍼 — 씬 / RectTransform / 보고
        // ====================================================================

        /// <summary>
        /// 현재 열린 활성 씬이 Login.unity 인지 확인한다.
        /// Login.unity 가 아니면 경고 후 false 를 반환한다. (씬을 새로 열지 않음)
        /// </summary>
        private static bool EnsureLoginSceneOpen(out UnityEngine.SceneManagement.Scene scene)
        {
            scene = EditorSceneManager.GetActiveScene();

            if (!scene.IsValid() || scene.name != LoginSceneName)
            {
                Report(false,
                    $"현재 열린 씬이 '{LoginSceneName}' 이(가) 아닙니다. " +
                    $"(현재: '{scene.name}') Login.unity 를 먼저 연 뒤 다시 실행하세요.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 씬을 dirty 표시 후 저장한다.
        /// </summary>
        private static void SaveScene(UnityEngine.SceneManagement.Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// RectTransform 을 전체화면 stretch(anchorMin 0,0 / anchorMax 1,1 / offset 0)로 설정한다.
        /// </summary>
        private static void StretchFullScreen(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 작업 결과를 콘솔 로그 + 다이얼로그로 보고한다.
        /// </summary>
        private static void Report(bool success, string message)
        {
            string prefix = success ? "완료" : "실패";
            string full = $"[LoginUiSetup] {prefix} — {message}";

            if (success) Debug.Log(full);
            else Debug.LogError(full);

            EditorUtility.DisplayDialog("Login UI Setup", $"{prefix}\n\n{message}", "확인");
        }
    }
}
