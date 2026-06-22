// ============================================================================
// LoginUiSetup.cs
// 로그인 UI 마무리 작업용 1회성 에디터 스크립트.
//
// 목적 (Plan.md Step 3 ~ Step 5):
//   • Step 3 — LoginRootView 의 NetworkErrorPopup 슬롯을 씬의 NetworkErrorPopup 에 연결.
//   • Step 4 — BUG-A: LoadingIndicator 를 SafeAreaContainer 밖(UIManager Canvas 직속)으로
//              이동시켜 전체화면 커버를 복구.
//   • Step 5 — BUG-B: AnonymousWarningPopup 을 UIManager Canvas 안으로 이동시켜
//              BlockingOverlay 에 가려지지 않도록 수정 + Inspector 슬롯 재연결.
//
// 사용 방법:
//   메뉴: Hexiege > Setup > (각 항목 선택)
//
// ⚠️  실행 전 Login.unity 를 저장해두는 것을 권장한다 (Ctrl+S).
//      이 스크립트는 Login.unity 를 열고 수정한 뒤 저장한다.
//
// ※ 1회성 작업 완료 후 이 파일은 삭제해도 무방하다.
// ============================================================================

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hexiege.Editor.Setup
{
    /// <summary>
    /// 로그인 UI 마무리 작업(슬롯 연결 + 버그 수정)을 위한 에디터 메뉴 모음.
    /// </summary>
    public static class LoginUiSetup
    {
        // 씬 경로
        private const string LoginScenePath = "Assets/_Project/Scenes/Login.unity";

        // 씬 오브젝트 이름
        private const string UIManagerCanvasName       = "UIManager Canvas";
        private const string SafeAreaContainerName     = "SafeAreaContainer";
        private const string NetworkErrorPopupName     = "NetworkErrorPopup";
        private const string LoadingIndicatorName      = "LoadingIndicator";
        private const string AnonymousWarningPopupName = "AnonymousWarningPopup";

        // LoginRootView 컴포넌트 타입 이름 / 직렬화 필드 이름
        private const string LoginRootViewTypeName     = "LoginRootView";
        private const string NetworkErrorPopupField    = "_networkErrorPopup";
        private const string AnonymousWarningField     = "_anonymousWarningPopup";

        // ====================================================================
        // Step 3 — NetworkErrorPopup 슬롯 연결
        // ====================================================================

        /// <summary>
        /// LoginRootView 의 _networkErrorPopup 슬롯에 씬의 NetworkErrorPopup 오브젝트를 연결한다.
        /// 씬에는 오브젝트가 이미 존재하지만 Inspector 슬롯이 비어 있는 상태를 해소한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Login UI — NetworkErrorPopup 슬롯 연결")]
        private static void ConnectNetworkErrorPopupSlot()
        {
            if (!OpenLoginScene(out var scene)) return;

            // 1) NetworkErrorPopup 오브젝트 탐색
            var popupGO = FindGameObjectByName(NetworkErrorPopupName);
            if (popupGO == null)
            {
                Report(false, $"'{NetworkErrorPopupName}' 오브젝트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 2) LoginRootView 컴포넌트 탐색
            var loginRootView = FindComponentByTypeName(LoginRootViewTypeName);
            if (loginRootView == null)
            {
                Report(false, $"'{LoginRootViewTypeName}' 컴포넌트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 3) 직렬화 프로퍼티에 참조 연결
            //    _networkErrorPopup 필드가 ConfirmPopup 류 컴포넌트일 수 있으므로,
            //    프로퍼티 타입에 맞는 컴포넌트를 popupGO 에서 탐색하여 연결한다.
            var so = new SerializedObject(loginRootView);
            var prop = so.FindProperty(NetworkErrorPopupField);
            if (prop == null)
            {
                Report(false,
                    $"'{LoginRootViewTypeName}' 에 '{NetworkErrorPopupField}' 필드가 없습니다. " +
                    "코드에 해당 필드가 정의되어 있는지 확인하세요.");
                return;
            }

            Object reference = ResolveReferenceForProperty(prop, popupGO);
            if (reference == null)
            {
                Report(false,
                    $"'{NetworkErrorPopupName}' 에서 '{NetworkErrorPopupField}' 슬롯에 맞는 " +
                    "컴포넌트를 찾을 수 없습니다.");
                return;
            }

            prop.objectReferenceValue = reference;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(loginRootView);

            SaveScene(scene);
            Report(true, $"LoginRootView.{NetworkErrorPopupField} → '{NetworkErrorPopupName}' 연결 완료.");
        }

        // ====================================================================
        // Step 4 — BUG-A: LoadingIndicator 이동
        // ====================================================================

        /// <summary>
        /// LoadingIndicator 를 SafeAreaContainer 밖(UIManager Canvas 직속)으로 이동시키고
        /// RectTransform 을 전체화면 stretch 로 설정한다.
        /// 내부의 SafeAreaContainer/Spinner/StatusText 구조는 그대로 유지된다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Login UI — BUG-A LoadingIndicator 이동")]
        private static void MoveLoadingIndicator()
        {
            if (!OpenLoginScene(out var scene)) return;

            // 1) UIManager Canvas 탐색
            var uiManagerCanvas = FindGameObjectByName(UIManagerCanvasName);
            if (uiManagerCanvas == null)
            {
                Report(false, $"'{UIManagerCanvasName}' 오브젝트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 2) LoadingIndicator 탐색 (UIManager Canvas 하위 어디에 있든 탐색)
            var loadingGO = FindGameObjectByName(LoadingIndicatorName);
            if (loadingGO == null)
            {
                Report(false, $"'{LoadingIndicatorName}' 오브젝트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 이미 UIManager Canvas 직속이면 부모 재설정은 생략하고 RectTransform 만 보정
            if (loadingGO.transform.parent != uiManagerCanvas.transform)
            {
                // worldPositionStays=false: UI 요소이므로 로컬 좌표 기준으로 부모만 교체
                loadingGO.transform.SetParent(uiManagerCanvas.transform, worldPositionStays: false);
            }

            // 3) 전체화면 stretch 설정
            StretchFullScreen(loadingGO);

            EditorUtility.SetDirty(loadingGO);
            SaveScene(scene);
            Report(true,
                $"'{LoadingIndicatorName}' 를 '{UIManagerCanvasName}' 직속으로 이동 + 전체화면 stretch 설정 완료.");
        }

        // ====================================================================
        // Step 5 — BUG-B: AnonymousWarningPopup 이동 + 슬롯 재연결
        // ====================================================================

        /// <summary>
        /// AnonymousWarningPopup 을 UIManager Canvas 의 SafeAreaContainer 하위로 이동시키고
        /// RectTransform 을 전체화면 stretch 로 설정한 뒤, 이를 참조하는 모든 LoginRootView 의
        /// _anonymousWarningPopup 슬롯을 재연결한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Login UI — BUG-B AnonymousWarningPopup 이동")]
        private static void MoveAnonymousWarningPopup()
        {
            if (!OpenLoginScene(out var scene)) return;

            // 1) AnonymousWarningPopup 탐색
            var popupGO = FindGameObjectByName(AnonymousWarningPopupName);
            if (popupGO == null)
            {
                Report(false, $"'{AnonymousWarningPopupName}' 오브젝트를 씬에서 찾을 수 없습니다.");
                return;
            }

            // 2) UIManager Canvas 의 SafeAreaContainer 탐색
            var targetParent = FindUIManagerSafeAreaContainer();
            if (targetParent == null)
            {
                Report(false,
                    $"'{UIManagerCanvasName}' 하위에서 '{SafeAreaContainerName}' 를 찾을 수 없습니다.");
                return;
            }

            // 3) 부모 이동
            if (popupGO.transform.parent != targetParent)
            {
                popupGO.transform.SetParent(targetParent, worldPositionStays: false);
            }

            // 4) 전체화면 stretch 설정
            StretchFullScreen(popupGO);
            EditorUtility.SetDirty(popupGO);

            // 5) 이동 후 popupGO 를 참조해야 하는 LoginRootView 들의 슬롯 재연결.
            //    AnonymousWarningPopup 컴포넌트 타입에 맞춰 참조를 해결한다.
            int reconnected = ReconnectAnonymousWarningSlots(popupGO);

            SaveScene(scene);
            Report(true,
                $"'{AnonymousWarningPopupName}' 를 '{UIManagerCanvasName}/{SafeAreaContainerName}' 하위로 이동 + " +
                $"전체화면 stretch 설정 완료. 재연결된 슬롯: {reconnected}개.");
        }

        // ====================================================================
        // 내부 헬퍼 — Step 5 보조
        // ====================================================================

        /// <summary>
        /// UIManager Canvas 직속 자식 중 이름이 SafeAreaContainer 인 Transform 을 반환한다.
        /// </summary>
        private static Transform FindUIManagerSafeAreaContainer()
        {
            var uiManagerCanvas = FindGameObjectByName(UIManagerCanvasName);
            if (uiManagerCanvas == null) return null;

            return uiManagerCanvas.transform
                .Cast<Transform>()
                .FirstOrDefault(t => t.name == SafeAreaContainerName);
        }

        /// <summary>
        /// 씬의 모든 LoginRootView 의 _anonymousWarningPopup 슬롯을 popupGO 기준으로 재연결한다.
        /// 반환값: 실제로 재연결된 슬롯 수.
        /// </summary>
        private static int ReconnectAnonymousWarningSlots(GameObject popupGO)
        {
            int count = 0;

            // 씬 내 모든 LoginRootView 컴포넌트를 순회 (참조가 여러 곳일 수 있음)
            var allMonoBehaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var mb in allMonoBehaviours)
            {
                if (mb == null || mb.GetType().Name != LoginRootViewTypeName) continue;

                var so = new SerializedObject(mb);
                var prop = so.FindProperty(AnonymousWarningField);
                if (prop == null) continue;

                Object reference = ResolveReferenceForProperty(prop, popupGO);
                if (reference == null) continue;

                prop.objectReferenceValue = reference;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mb);
                count++;
            }

            return count;
        }

        // ====================================================================
        // 내부 헬퍼 — 공통
        // ====================================================================

        /// <summary>
        /// Login.unity 를 단일 모드로 연다. 실패 시 false.
        /// 현재 열린 씬에 저장되지 않은 변경이 있으면 사용자에게 저장 여부를 먼저 묻는다.
        /// </summary>
        private static bool OpenLoginScene(out UnityEngine.SceneManagement.Scene scene)
        {
            scene = default;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Report(false, $"Login.unity 를 열 수 없습니다: {LoginScenePath}");
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
        /// 직렬화 프로퍼티의 기대 타입에 맞춰 sourceGO 에서 연결할 Object 를 해결한다.
        /// 프로퍼티 타입을 알 수 없으면 GameObject 자체를 우선 시도하고,
        /// 그래도 안 되면 부착된 MonoBehaviour 중 적합한 것을 탐색한다.
        /// </summary>
        private static Object ResolveReferenceForProperty(SerializedProperty prop, GameObject sourceGO)
        {
            // 프로퍼티의 기대 타입 문자열에서 컴포넌트 타입명을 추출.
            // 예: "PPtr<$AnonymousWarningPopup>" → "AnonymousWarningPopup"
            string typeName = ExtractTypeNameFromProperty(prop);

            // 1) GameObject 슬롯이면 GameObject 자체를 연결
            if (typeName == "GameObject")
                return sourceGO;

            // 2) 특정 컴포넌트 타입 슬롯이면 해당 타입 컴포넌트를 탐색
            if (!string.IsNullOrEmpty(typeName))
            {
                var comp = sourceGO.GetComponents<Component>()
                    .FirstOrDefault(c => c != null && c.GetType().Name == typeName);
                if (comp != null) return comp;
            }

            // 3) 타입을 특정하지 못한 경우: GameObject 에 부착된 첫 번째 MonoBehaviour 를 시도,
            //    그것도 없으면 GameObject 자체를 반환
            var mb = sourceGO.GetComponent<MonoBehaviour>();
            if (mb != null) return mb;

            return sourceGO;
        }

        /// <summary>
        /// SerializedProperty.type ("PPtr<$Type>" 형태)에서 순수 타입 이름만 추출한다.
        /// </summary>
        private static string ExtractTypeNameFromProperty(SerializedProperty prop)
        {
            string raw = prop.type; // 예: "PPtr<$AnonymousWarningPopup>" 또는 "PPtr<GameObject>"
            int dollar = raw.IndexOf('$');
            int gt = raw.IndexOf('>');

            if (dollar >= 0 && gt > dollar)
                return raw.Substring(dollar + 1, gt - dollar - 1);

            // "$" 없이 "PPtr<GameObject>" 형태인 경우
            int lt = raw.IndexOf('<');
            if (lt >= 0 && gt > lt)
                return raw.Substring(lt + 1, gt - lt - 1);

            return string.Empty;
        }

        /// <summary>
        /// 현재 열린 씬에서 이름이 일치하는 GameObject 를 반환한다(비활성 포함).
        /// </summary>
        private static GameObject FindGameObjectByName(string goName)
        {
            // 비활성 오브젝트까지 포함하기 위해 Transform 전체를 순회한다.
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
        /// 작업 결과를 콘솔 로그 + 다이얼로그로 보고한다.
        /// </summary>
        private static void Report(bool success, string message)
        {
            string prefix = success ? "✓ 완료" : "✗ 실패";
            string full = $"[LoginUiSetup] {prefix} — {message}";

            if (success) Debug.Log(full);
            else Debug.LogError(full);

            EditorUtility.DisplayDialog("Login UI Setup", $"{prefix}\n\n{message}", "확인");
        }
    }
}
