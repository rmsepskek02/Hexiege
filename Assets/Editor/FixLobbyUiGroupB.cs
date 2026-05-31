#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// [그룹 B] Lobby.unity 씬의 LoadingScreen 및 CodeInput 관련 UI 규칙 위반 3건을 수정하는 1회성 에디터 스크립트.
    ///
    /// 처리 항목 (모두 규칙 2 - 고정 픽셀 대신 앵커 비율화):
    ///  - B-1: LoadingScreen > Spinner 앵커 비율화
    ///  - B-2: LoadingScreen > StatusText 앵커 비율화
    ///  - B-3: CodeInput > Text Area 앵커 비율화
    ///
    /// 이 스크립트는 코드 로직 변경 없이 Inspector 값만 변경한다.
    /// 사용법: Unity 상단 메뉴 [Hexiege/Fix/LobbyUI/GroupB_LoadingScreen] 실행.
    /// </summary>
    public static class FixLobbyUiGroupB
    {
        private const string TargetSceneName = "Lobby";

        [MenuItem("Hexiege/Fix/LobbyUI/GroupB_LoadingScreen")]
        public static void Fix()
        {
            // 활성 씬이 Lobby인지 확인. 아니면 잘못된 씬을 건드릴 수 있으므로 중단.
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != TargetSceneName)
            {
                Debug.LogWarning($"[FixLobbyUiGroupB] 활성 씬이 '{TargetSceneName}'이(가) 아닙니다. " +
                                 $"현재 활성 씬: '{activeScene.name}'. 작업을 중단합니다.");
                return;
            }

            int changeCount = 0;

            // ---------- B-1, B-2: LoadingScreen 하위 ----------
            var loadingScreen = FindRootObject(TargetSceneName, "LoadingScreen");
            if (loadingScreen == null)
            {
                Debug.LogWarning("[FixLobbyUiGroupB] 'LoadingScreen' 루트 오브젝트를 찾지 못했습니다. B-1/B-2를 건너뜁니다.");
            }
            else
            {
                // B-1: Spinner
                var spinner = FindDeepChild(loadingScreen.transform, "Spinner") as RectTransform;
                if (spinner == null)
                {
                    Debug.LogWarning("[FixLobbyUiGroupB] 'LoadingScreen/.../Spinner'를 찾지 못했습니다. B-1을 건너뜁니다.");
                }
                else
                {
                    Undo.RecordObject(spinner, "Fix Spinner Anchor");
                    spinner.anchorMin = new Vector2(0.444f, 0.511f);
                    spinner.anchorMax = new Vector2(0.556f, 0.573f);
                    spinner.anchoredPosition = Vector2.zero;
                    spinner.sizeDelta = Vector2.zero;
                    EditorUtility.SetDirty(spinner);
                    Debug.Log("[FixLobbyUiGroupB] B-1: Spinner 앵커 비율화 완료.");
                    changeCount++;
                }

                // B-2: StatusText
                var statusText = FindDeepChild(loadingScreen.transform, "StatusText") as RectTransform;
                if (statusText == null)
                {
                    Debug.LogWarning("[FixLobbyUiGroupB] 'LoadingScreen/.../StatusText'를 찾지 못했습니다. B-2를 건너뜁니다.");
                }
                else
                {
                    Undo.RecordObject(statusText, "Fix StatusText Anchor");
                    statusText.anchorMin = new Vector2(0.176f, 0.448f);
                    statusText.anchorMax = new Vector2(0.824f, 0.490f);
                    statusText.anchoredPosition = Vector2.zero;
                    statusText.sizeDelta = Vector2.zero;
                    EditorUtility.SetDirty(statusText);
                    Debug.Log("[FixLobbyUiGroupB] B-2: StatusText 앵커 비율화 완료.");
                    changeCount++;
                }
            }

            // ---------- B-3: CodeInput > Text Area ----------
            // CodeInput은 LoadingScreen 외부(CustomJoinPanel 등)에 있을 수 있으므로 씬 전체에서 탐색한다.
            var codeInput = FindDeepChildInScene(TargetSceneName, "CodeInput");
            if (codeInput == null)
            {
                Debug.LogWarning("[FixLobbyUiGroupB] 씬에서 'CodeInput'을 찾지 못했습니다. B-3을 건너뜁니다.");
            }
            else
            {
                // TMP_InputField의 텍스트 영역은 보통 'Text Area'라는 이름의 자식이다.
                var textArea = FindDeepChild(codeInput, "Text Area") as RectTransform;
                if (textArea == null)
                {
                    Debug.LogWarning("[FixLobbyUiGroupB] 'CodeInput/.../Text Area'를 찾지 못했습니다. B-3을 건너뜁니다.");
                }
                else
                {
                    Undo.RecordObject(textArea, "Fix CodeInput Text Area Anchor");
                    textArea.anchorMin = new Vector2(0.02f, 0.05f);
                    textArea.anchorMax = new Vector2(0.98f, 0.95f);
                    textArea.sizeDelta = Vector2.zero; // 고정 픽셀 패딩 제거
                    EditorUtility.SetDirty(textArea);
                    Debug.Log("[FixLobbyUiGroupB] B-3: CodeInput/Text Area 앵커 비율화 완료.");
                    changeCount++;
                }
            }

            if (changeCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[FixLobbyUiGroupB] 완료: {changeCount}건 수정 후 '{TargetSceneName}.unity' 저장.");
            }
            else
            {
                Debug.Log("[FixLobbyUiGroupB] 변경할 항목이 없습니다.");
            }
        }

        // ---------------------------------------------------------------------
        // 공용 유틸리티
        // ---------------------------------------------------------------------

        /// <summary>
        /// 지정한 씬의 루트 GameObject 중에서 이름이 일치하는 것을 찾는다.
        /// </summary>
        private static GameObject FindRootObject(string sceneName, string objectName)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != sceneName) return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == objectName) return root;
            }
            return null;
        }

        /// <summary>
        /// 씬 전체(모든 루트 하위 포함)에서 이름이 일치하는 Transform을 재귀적으로 찾는다.
        /// 루트 오브젝트 이름을 모르거나 여러 루트에 흩어진 오브젝트를 찾을 때 사용한다.
        /// </summary>
        private static Transform FindDeepChildInScene(string sceneName, string objectName)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != sceneName) return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == objectName) return root.transform;

                var found = FindDeepChild(root.transform, objectName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 부모 Transform 아래에서 이름이 일치하는 자식을 재귀적으로(깊이 우선) 탐색한다.
        /// </summary>
        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;

                var result = FindDeepChild(child, childName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
