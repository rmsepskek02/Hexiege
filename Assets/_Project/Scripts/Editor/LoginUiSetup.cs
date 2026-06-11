// ============================================================================
// LoginUiSetup.cs
// Login.unity 씬의 로그인 UI를 일괄 셋업하는 에디터 전용 스크립트.
//
// 메뉴에서 1회 실행하면 다음 작업을 자동으로 처리한다:
//   1. 5개 패널 GameObject 에 CanvasGroup 컴포넌트를 추가(없으면 생성).
//   2. LoginSelectPanel 만 alpha=1(표시), 나머지 4개는 alpha=0(숨김)으로 초기 상태 설정.
//   3. LoginRootView 의 Inspector 패널 슬롯(CanvasGroup 타입)에 해당 CanvasGroup 자동 연결.
//   4. ConfirmPopup 오브젝트를 복제해 NetworkErrorPopup 이름으로 씬에 추가.
//   5. LoginRootView 의 _networkErrorPopup 슬롯에 새 NetworkErrorPopup 연결.
//   6. 씬 Dirty 마킹 + 저장.
//
// 이 스크립트는 Editor 폴더 안에 있으므로 빌드에 포함되지 않는다(에디터 전용).
// ============================================================================

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 로그인 씬의 CanvasGroup 셋업 + NetworkErrorPopup 생성을 자동화하는 에디터 도구.
    /// </summary>
    public static class LoginUiSetup
    {
        // 씬에서 찾을 패널 GameObject 이름 목록.
        // LoginRootView 의 SerializedProperty 이름과 1:1로 매칭된다.
        // (배열 인덱스가 같은 항목끼리 서로 대응됨)
        private static readonly string[] PanelObjectNames =
        {
            "LoginSelectPanel",
            "EmailLoginPanel",
            "SignUpPanel",
            "EmailVerifyPanel",
            "PasswordResetPanel"
        };

        // 위 PanelObjectNames 와 같은 순서로 매칭되는 LoginRootView 직렬화 필드 이름.
        private static readonly string[] PanelPropertyNames =
        {
            "_loginSelectPanel",
            "_emailLoginPanel",
            "_signUpPanel",
            "_emailVerifyPanel",
            "_passwordResetPanel"
        };

        // 초기 상태에서 표시(alpha=1)될 패널 이름.
        // 이 패널만 보이고 나머지는 숨겨진다.
        private const string InitiallyVisiblePanel = "LoginSelectPanel";

        // 기존 ConfirmPopup 오브젝트 이름(복제 원본).
        private const string ConfirmPopupName = "ConfirmPopup";

        // 새로 만들 NetworkErrorPopup 오브젝트 이름.
        private const string NetworkErrorPopupName = "NetworkErrorPopup";

        // NetworkErrorPopup 을 자식으로 붙일 부모 오브젝트 이름.
        private const string SafeAreaContainerName = "SafeAreaContainer";

        /// <summary>
        /// 메뉴 항목 진입점. Login UI를 CanvasGroup 기반으로 셋업하고 NetworkErrorPopup을 생성한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Login UI — CanvasGroup + NetworkErrorPopup 설정")]
        public static void SetupLoginUi()
        {
            // 현재 활성화된 씬을 가져온다.
            Scene scene = SceneManager.GetActiveScene();

            // Login.unity 씬이 열려 있지 않으면 경고 후 중단한다.
            // (씬 이름이 "Login" 이 아니면 잘못된 씬에서 실행된 것)
            if (scene.name != "Login")
            {
                EditorUtility.DisplayDialog(
                    "Login UI 셋업 중단",
                    "Login.unity 씬이 열려 있지 않습니다.\n" +
                    "Login 씬을 연 뒤 다시 실행하세요.\n\n" +
                    "현재 씬: " + scene.name,
                    "확인");
                return;
            }

            // ----------------------------------------------------------------
            // 1) LoginRootView 컴포넌트 탐색
            // ----------------------------------------------------------------
            // 씬 안에서 LoginRootView 를 가진 오브젝트를 찾는다.
            // FindObjectsOfType 은 deprecated 이므로 FindFirstObjectByType 사용.
            LoginRootView rootView = GameObject.FindFirstObjectByType<LoginRootView>();
            if (rootView == null)
            {
                EditorUtility.DisplayDialog(
                    "Login UI 셋업 중단",
                    "씬에서 LoginRootView 컴포넌트를 찾을 수 없습니다.\n" +
                    "LoginRootView 가 씬에 존재하는지 확인하세요.",
                    "확인");
                return;
            }

            // SerializedObject 를 통해 LoginRootView 의 직렬화 필드를 안전하게 수정한다.
            // (Reflection 대신 SerializedObject 를 쓰면 Undo/Dirty 처리가 깔끔하게 동작)
            SerializedObject rootViewSo = new SerializedObject(rootView);

            // ----------------------------------------------------------------
            // 2) 패널 5개 CanvasGroup 셋업 + LoginRootView 슬롯 연결
            // ----------------------------------------------------------------
            for (int i = 0; i < PanelObjectNames.Length; i++)
            {
                string panelName = PanelObjectNames[i];
                string propertyName = PanelPropertyNames[i];

                // 씬에서 패널 GameObject 를 이름으로 찾는다.
                GameObject panelGo = GameObject.Find(panelName);
                if (panelGo == null)
                {
                    // 못 찾으면 경고 로그만 남기고 다음 패널로 넘어간다(스킵).
                    Debug.LogWarning(
                        $"[LoginUiSetup] 패널 오브젝트 '{panelName}' 를 씬에서 찾지 못했습니다. 스킵합니다.");
                    continue;
                }

                // CanvasGroup 이 이미 있으면 가져오고, 없으면 새로 추가한다.
                CanvasGroup cg = panelGo.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    // AddComponent 도 변경 사항이므로 Undo 대상에 포함시킨다.
                    cg = Undo.AddComponent<CanvasGroup>(panelGo);
                }

                // CanvasGroup 필드 값을 바꾸기 전에 Undo 기록(Ctrl+Z 로 되돌릴 수 있게).
                Undo.RecordObject(cg, "Setup Login Panel CanvasGroup");

                // 초기 표시 패널만 alpha=1(보임), 나머지는 alpha=0(숨김)으로 설정.
                bool isVisible = (panelName == InitiallyVisiblePanel);
                cg.alpha = isVisible ? 1f : 0f;
                cg.blocksRaycasts = isVisible;
                cg.interactable = isVisible;

                // CanvasGroup 값이 바뀌었음을 에디터에 알린다.
                EditorUtility.SetDirty(cg);

                // LoginRootView 의 해당 슬롯(SerializedProperty)에 이 CanvasGroup 을 연결한다.
                SerializedProperty prop = rootViewSo.FindProperty(propertyName);
                if (prop != null)
                {
                    prop.objectReferenceValue = cg;
                }
                else
                {
                    Debug.LogWarning(
                        $"[LoginUiSetup] LoginRootView 에서 직렬화 필드 '{propertyName}' 를 찾지 못했습니다.");
                }
            }

            // ----------------------------------------------------------------
            // 3) NetworkErrorPopup 생성 (ConfirmPopup 복제)
            // ----------------------------------------------------------------
            // 복제 원본인 ConfirmPopup 오브젝트를 찾는다.
            GameObject confirmPopupGo = GameObject.Find(ConfirmPopupName);
            if (confirmPopupGo == null)
            {
                Debug.LogWarning(
                    $"[LoginUiSetup] '{ConfirmPopupName}' 오브젝트를 찾지 못해 NetworkErrorPopup 생성을 건너뜁니다.");
            }
            else
            {
                // ConfirmPopup 을 그대로 복제(Instantiate)한다.
                GameObject networkPopupGo = Object.Instantiate(confirmPopupGo);

                // 복제본 이름에 자동으로 붙는 "(Clone)" 을 제거하고 지정 이름으로 변경.
                networkPopupGo.name = NetworkErrorPopupName;

                // 생성된 오브젝트를 Undo 대상에 등록(Ctrl+Z 로 삭제 가능하게).
                Undo.RegisterCreatedObjectUndo(networkPopupGo, "Create NetworkErrorPopup");

                // SafeAreaContainer 를 찾아 그 하위로 부모를 지정한다.
                GameObject safeAreaGo = GameObject.Find(SafeAreaContainerName);
                if (safeAreaGo != null)
                {
                    // worldPositionStays: false → 로컬 좌표/스케일을 부모 기준으로 재계산.
                    // UI 오브젝트는 보통 false 로 붙여야 RectTransform 배치가 의도대로 유지된다.
                    networkPopupGo.transform.SetParent(safeAreaGo.transform, worldPositionStays: false);
                }
                else
                {
                    Debug.LogWarning(
                        $"[LoginUiSetup] '{SafeAreaContainerName}' 를 찾지 못해 NetworkErrorPopup 의 부모를 지정하지 못했습니다.");
                }

                // 복제본에서 ConfirmPopup 컴포넌트를 가져온다(LoginRootView 슬롯 타입과 일치).
                ConfirmPopup networkPopup = networkPopupGo.GetComponent<ConfirmPopup>();
                if (networkPopup != null)
                {
                    // LoginRootView 의 _networkErrorPopup 슬롯에 연결.
                    SerializedProperty networkProp = rootViewSo.FindProperty("_networkErrorPopup");
                    if (networkProp != null)
                    {
                        networkProp.objectReferenceValue = networkPopup;
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[LoginUiSetup] LoginRootView 에서 '_networkErrorPopup' 필드를 찾지 못했습니다.");
                    }
                }
                else
                {
                    Debug.LogWarning(
                        "[LoginUiSetup] 복제한 NetworkErrorPopup 에서 ConfirmPopup 컴포넌트를 찾지 못했습니다.");
                }
            }

            // ----------------------------------------------------------------
            // 4) LoginRootView 변경 사항 적용 + 씬 저장
            // ----------------------------------------------------------------
            // SerializedObject 로 변경한 슬롯 값들을 실제 컴포넌트에 반영한다(필수).
            rootViewSo.ApplyModifiedProperties();

            // LoginRootView 가 변경되었음을 에디터에 알린다.
            EditorUtility.SetDirty(rootView);

            // 씬을 Dirty 로 표시해 저장 대상에 포함시킨다.
            EditorSceneManager.MarkSceneDirty(scene);

            // 변경된 씬을 디스크에 저장한다.
            EditorSceneManager.SaveScene(scene);

            // 완료 메시지 표시.
            EditorUtility.DisplayDialog(
                "Login UI 셋업 완료",
                "5개 패널 CanvasGroup 셋업, 초기 표시 상태 설정,\n" +
                "LoginRootView 슬롯 연결, NetworkErrorPopup 생성/연결,\n" +
                "씬 저장이 완료되었습니다.",
                "확인");
        }
    }
}
