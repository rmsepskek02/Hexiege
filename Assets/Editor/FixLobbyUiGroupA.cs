#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// [그룹 A] Lobby.unity 씬의 Toast Canvas 관련 UI 규칙 위반 4건을 한 번에 수정하는 1회성 에디터 스크립트.
    ///
    /// 처리 항목:
    ///  - A-1: Toast Canvas에 CanvasScaler 추가 (규칙 1 - 해상도 통일)
    ///  - A-2: Toast > Background 앵커 비율화 (규칙 2 - 고정 픽셀 금지)
    ///  - A-3: Toast > Background > Message 앵커 비율화 (규칙 2)
    ///  - A-4: Toast CanvasGroup 초기값 정렬 (규칙 5 - 숨김 상태 표준화)
    ///
    /// 이 스크립트는 코드 로직 변경 없이 Inspector 값만 변경한다.
    /// 사용법: Unity 상단 메뉴 [Hexiege/Fix/LobbyUI/GroupA_Toast] 실행.
    /// </summary>
    public static class FixLobbyUiGroupA
    {
        // Toast Canvas가 위치한 Lobby 씬 이름. 활성 씬 검증용으로만 사용한다.
        private const string TargetSceneName = "Lobby";

        [MenuItem("Hexiege/Fix/LobbyUI/GroupA_Toast")]
        public static void Fix()
        {
            // 현재 열려 있는(활성) 씬이 Lobby가 아니면 잘못된 씬을 수정할 위험이 있으므로 중단한다.
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != TargetSceneName)
            {
                Debug.LogWarning($"[FixLobbyUiGroupA] 활성 씬이 '{TargetSceneName}'이(가) 아닙니다. " +
                                 $"현재 활성 씬: '{activeScene.name}'. 작업을 중단합니다.");
                return;
            }

            // 씬 루트에서 'Toast'라는 이름의 GameObject를 찾는다.
            var toast = FindRootObject(TargetSceneName, "Toast");
            if (toast == null)
            {
                Debug.LogWarning("[FixLobbyUiGroupA] 'Toast' 오브젝트를 찾지 못했습니다. 작업을 중단합니다.");
                return;
            }

            // 변경 카운트(로그용)
            int changeCount = 0;

            // ---------- A-1: CanvasScaler 추가 ----------
            changeCount += FixCanvasScaler(toast);

            // ---------- A-2: Toast > Background 앵커 비율화 ----------
            var background = FindDeepChild(toast.transform, "Background");
            if (background == null)
            {
                Debug.LogWarning("[FixLobbyUiGroupA] 'Toast/Background'를 찾지 못했습니다. A-2/A-3을 건너뜁니다.");
            }
            else
            {
                changeCount += FixBackgroundAnchor(background as RectTransform);

                // ---------- A-3: Background > Message 앵커 비율화 ----------
                var message = FindDeepChild(background, "Message");
                if (message == null)
                {
                    Debug.LogWarning("[FixLobbyUiGroupA] 'Toast/Background/Message'를 찾지 못했습니다. A-3을 건너뜁니다.");
                }
                else
                {
                    changeCount += FixMessageAnchor(message as RectTransform);
                }
            }

            // ---------- A-4: Toast CanvasGroup 초기값 수정 ----------
            changeCount += FixCanvasGroup(toast);

            // 변경 사항이 있었다면 씬을 더티 표시하고 저장한다.
            if (changeCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[FixLobbyUiGroupA] 완료: {changeCount}건 수정 후 '{TargetSceneName}.unity' 저장.");
            }
            else
            {
                Debug.Log("[FixLobbyUiGroupA] 변경할 항목이 없습니다.");
            }
        }

        /// <summary>
        /// A-1: Toast Canvas에 CanvasScaler가 없으면 추가하고, 해상도 통일 규칙(1080x1920)에 맞게 설정한다.
        /// </summary>
        /// <returns>변경이 발생하면 1, 아니면 0</returns>
        private static int FixCanvasScaler(GameObject toast)
        {
            // 이미 CanvasScaler가 있으면 중복 추가하지 않는다(요구사항: 없는 경우에만 추가).
            var scaler = toast.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                Debug.Log("[FixLobbyUiGroupA] A-1: Toast에 CanvasScaler가 이미 존재합니다. 건너뜁니다.");
                return 0;
            }

            // Undo 스택에 추가 작업을 등록하여 Ctrl+Z로 되돌릴 수 있게 한다.
            scaler = Undo.AddComponent<CanvasScaler>(toast);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f; // 0 = 너비 기준, 1 = 높이 기준

            EditorUtility.SetDirty(toast);
            Debug.Log("[FixLobbyUiGroupA] A-1: Toast에 CanvasScaler 추가 완료 (1080x1920, Match=0).");
            return 1;
        }

        /// <summary>
        /// A-2: Toast > Background를 화면 하단 중앙의 비율 기반 앵커로 전환한다.
        /// </summary>
        private static int FixBackgroundAnchor(RectTransform rt)
        {
            if (rt == null) return 0;

            // 앵커/포지션/크기를 바꾸기 전에 Undo에 기록한다.
            Undo.RecordObject(rt, "Fix Toast Background Anchor");

            rt.anchorMin = new Vector2(0.176f, 0.04f);
            rt.anchorMax = new Vector2(0.824f, 0.083f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero; // 앵커가 크기를 결정하므로 고정 픽셀 크기 제거

            EditorUtility.SetDirty(rt);
            Debug.Log("[FixLobbyUiGroupA] A-2: Toast/Background 앵커 비율화 완료.");
            return 1;
        }

        /// <summary>
        /// A-3: Background > Message를 부모 영역에 비율 기반 패딩으로 채워지도록 전환한다.
        /// </summary>
        private static int FixMessageAnchor(RectTransform rt)
        {
            if (rt == null) return 0;

            Undo.RecordObject(rt, "Fix Toast Message Anchor");

            rt.anchorMin = new Vector2(0.017f, 0.15f);
            rt.anchorMax = new Vector2(0.983f, 0.85f);
            rt.sizeDelta = Vector2.zero; // 고정 픽셀 패딩(-24,-24) 제거 → 앵커 비율이 패딩 역할

            EditorUtility.SetDirty(rt);
            Debug.Log("[FixLobbyUiGroupA] A-3: Toast/Background/Message 앵커 비율화 완료.");
            return 1;
        }

        /// <summary>
        /// A-4: Toast 하위 CanvasGroup의 초기 숨김 상태를 규칙 5에 맞게 정렬한다.
        /// (alpha=0, blocksRaycasts=false, interactable=false)
        /// </summary>
        private static int FixCanvasGroup(GameObject toast)
        {
            // Toast 본인 또는 자식에 붙은 CanvasGroup을 찾는다.
            var canvasGroup = toast.GetComponentInChildren<CanvasGroup>(true);
            if (canvasGroup == null)
            {
                Debug.LogWarning("[FixLobbyUiGroupA] A-4: Toast 하위에서 CanvasGroup을 찾지 못했습니다. 건너뜁니다.");
                return 0;
            }

            Undo.RecordObject(canvasGroup, "Fix Toast CanvasGroup");

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            EditorUtility.SetDirty(canvasGroup);
            Debug.Log("[FixLobbyUiGroupA] A-4: Toast CanvasGroup 초기값(alpha=0, blocksRaycasts=false, interactable=false) 정렬 완료.");
            return 1;
        }

        // ---------------------------------------------------------------------
        // 공용 유틸리티
        // ---------------------------------------------------------------------

        /// <summary>
        /// 지정한 씬의 루트 GameObject 중에서 이름이 일치하는 것을 찾는다.
        /// (Toast/LoadingScreen 등 최상위 오브젝트 탐색에 사용)
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
        /// 부모 Transform 아래에서 이름이 일치하는 자식을 재귀적으로(깊이 우선) 탐색한다.
        /// 계층 어디에 있든 이름만으로 찾을 수 있다.
        /// </summary>
        private static Transform FindDeepChild(Transform parent, string childName)
        {
            // 직속 자식부터 검사
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;

                // 손자 이하까지 재귀 탐색
                var result = FindDeepChild(child, childName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
