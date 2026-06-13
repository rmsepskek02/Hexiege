// ============================================================================
// ActivateAnimatedPanels.cs
// AnimatedPanel CanvasGroup 리팩토링 후속 1회성 에디터 스크립트.
//
// 배경:
//   AnimatedPanel/UIAnimator가 SetActive 대신 CanvasGroup(alpha/blocksRaycasts/
//   interactable)으로만 가시성을 제어하도록 리팩토링됨(공통 UI 규칙 5).
//   이전에는 씬에서 패널 오브젝트를 m_IsActive=false(비활성)로 두어 초기 숨김을
//   처리했지만, 이제는 오브젝트가 항상 active 상태여야 한다.
//   (active=false면 EnsureInitialized()가 호출되지 않아 초기 CanvasGroup 값이
//    적용되지 않고, LayoutGroup에서도 제외되는 버그가 발생할 수 있음)
//
// 역할:
//   Game.unity 씬에서 AnimatedPanel 컴포넌트가 부착된 모든 오브젝트를 찾아
//   비활성 상태인 것을 active 상태로 전환한다.
//   초기 숨김은 런타임 EnsureInitialized()의 CanvasGroup 설정이 대신 처리한다.
//
// 사용법:
//   Unity 에디터 메뉴 → Hexiege/Setup/AnimatedPanel 활성화 클릭.
//   (현재 열려 있는 씬 + Game.unity를 대상으로 동작)
//
// 실행 후 이 스크립트는 삭제해도 무방한 1회성 유틸리티.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// AnimatedPanel이 부착된 비활성 오브젝트를 active로 전환하는 1회성 에디터 유틸리티.
    /// CanvasGroup 리팩토링 후 초기 숨김을 SetActive=false에 의존하지 않도록 정리한다.
    /// </summary>
    public static class ActivateAnimatedPanels
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        /// <summary>
        /// Game.unity 씬을 열어 AnimatedPanel 부착 오브젝트를 모두 active로 전환 후 저장한다.
        /// 비활성 상태였던 오브젝트만 변경하며, 변경 내역을 콘솔에 로그로 남긴다.
        /// </summary>
        [MenuItem("Hexiege/Setup/AnimatedPanel 활성화")]
        public static void ActivateInGameScene()
        {
            // Game.unity 씬을 Single 모드로 연다.
            // (작업 중이던 씬이 있으면 저장 여부를 먼저 묻는다)
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // 비활성 오브젝트도 포함해서 모든 AnimatedPanel을 탐색한다.
            AnimatedPanel[] panels = Object.FindObjectsByType<AnimatedPanel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int changed = 0;
            foreach (AnimatedPanel panel in panels)
            {
                GameObject go = panel.gameObject;
                if (go.activeSelf) continue; // 이미 active면 건너뜀

                // Undo 등록 후 active로 전환
                Undo.RecordObject(go, "Activate AnimatedPanel");
                go.SetActive(true);
                EditorUtility.SetDirty(go);
                changed++;
                Debug.Log($"[ActivateAnimatedPanels] '{go.name}' 을(를) active로 전환했습니다.");
            }

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[ActivateAnimatedPanels] 완료: {changed}개 오브젝트를 active로 전환하고 씬을 저장했습니다.");
            }
            else
            {
                Debug.Log("[ActivateAnimatedPanels] 변경할 비활성 AnimatedPanel이 없습니다.");
            }
        }
    }
}
