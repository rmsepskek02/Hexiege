#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// [그룹 C] Lobby.unity 씬의 LobbyRoot 하위 TabBar / ContentArea UI 규칙 위반 2건을 수정하는 1회성 에디터 스크립트.
    ///
    /// 처리 항목 (모두 규칙 2 - 고정 픽셀 대신 앵커 비율화):
    ///  - C-1: LobbyRoot > TabBar 앵커 비율화 (화면 하단 0% ~ 7.3%)
    ///  - C-2: LobbyRoot > ContentArea 앵커 비율화 (화면 7.3% ~ 100%)
    ///
    /// TabBar는 화면 하단을, ContentArea는 그 위 나머지 영역을 채우도록 비율을 맞춘다.
    /// 이 스크립트는 코드 로직 변경 없이 Inspector 값만 변경한다.
    /// 사용법: Unity 상단 메뉴 [Hexiege/Fix/LobbyUI/GroupC_TabBar] 실행.
    /// </summary>
    public static class FixLobbyUiGroupC
    {
        private const string TargetSceneName = "Lobby";

        // TabBar 상단 경계 = ContentArea 하단 경계 (화면 높이 대비 비율). 140px / 1920px ≈ 0.073
        private const float TabBarTopRatio = 0.073f;

        [MenuItem("Hexiege/Fix/LobbyUI/GroupC_TabBar")]
        public static void Fix()
        {
            // 활성 씬이 Lobby인지 확인.
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != TargetSceneName)
            {
                Debug.LogWarning($"[FixLobbyUiGroupC] 활성 씬이 '{TargetSceneName}'이(가) 아닙니다. " +
                                 $"현재 활성 씬: '{activeScene.name}'. 작업을 중단합니다.");
                return;
            }

            // LobbyRoot를 먼저 찾는다. TabBar/ContentArea는 그 직속 자식이다.
            var lobbyRoot = FindDeepChildInScene(TargetSceneName, "LobbyRoot");
            if (lobbyRoot == null)
            {
                Debug.LogWarning("[FixLobbyUiGroupC] 'LobbyRoot'를 찾지 못했습니다. 작업을 중단합니다.");
                return;
            }

            int changeCount = 0;

            // ---------- C-1: TabBar (LobbyRoot 직속 자식) ----------
            var tabBar = FindDirectChild(lobbyRoot, "TabBar") as RectTransform;
            if (tabBar == null)
            {
                Debug.LogWarning("[FixLobbyUiGroupC] 'LobbyRoot/TabBar' 직속 자식을 찾지 못했습니다. C-1을 건너뜁니다.");
            }
            else
            {
                Undo.RecordObject(tabBar, "Fix TabBar Anchor");
                tabBar.anchorMin = new Vector2(0f, 0f);
                tabBar.anchorMax = new Vector2(1f, TabBarTopRatio);
                tabBar.anchoredPosition = Vector2.zero;
                tabBar.sizeDelta = Vector2.zero;
                EditorUtility.SetDirty(tabBar);
                Debug.Log("[FixLobbyUiGroupC] C-1: TabBar 앵커 비율화 완료 (0 ~ 7.3%).");
                changeCount++;
            }

            // ---------- C-2: ContentArea (LobbyRoot 직속 자식) ----------
            var contentArea = FindDirectChild(lobbyRoot, "ContentArea") as RectTransform;
            if (contentArea == null)
            {
                Debug.LogWarning("[FixLobbyUiGroupC] 'LobbyRoot/ContentArea' 직속 자식을 찾지 못했습니다. C-2를 건너뜁니다.");
            }
            else
            {
                Undo.RecordObject(contentArea, "Fix ContentArea Anchor");
                contentArea.anchorMin = new Vector2(0f, TabBarTopRatio);
                contentArea.anchorMax = new Vector2(1f, 1f);
                contentArea.anchoredPosition = Vector2.zero;
                contentArea.sizeDelta = Vector2.zero;
                EditorUtility.SetDirty(contentArea);
                Debug.Log("[FixLobbyUiGroupC] C-2: ContentArea 앵커 비율화 완료 (7.3% ~ 100%).");
                changeCount++;
            }

            if (changeCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[FixLobbyUiGroupC] 완료: {changeCount}건 수정 후 '{TargetSceneName}.unity' 저장.");
            }
            else
            {
                Debug.Log("[FixLobbyUiGroupC] 변경할 항목이 없습니다.");
            }
        }

        // ---------------------------------------------------------------------
        // 공용 유틸리티
        // ---------------------------------------------------------------------

        /// <summary>
        /// 부모의 '직속' 자식(손자 제외)에서 이름이 일치하는 Transform을 찾는다.
        /// TabBar/ContentArea가 LobbyRoot 직속 자식임을 명시적으로 보장하기 위해 사용한다.
        /// </summary>
        private static Transform FindDirectChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;
            }
            return null;
        }

        /// <summary>
        /// 씬 전체에서 이름이 일치하는 Transform을 재귀적으로(깊이 우선) 찾는다.
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
        /// 부모 Transform 아래에서 이름이 일치하는 자식을 재귀적으로 탐색한다.
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
