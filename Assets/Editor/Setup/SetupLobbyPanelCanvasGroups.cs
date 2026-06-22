// ============================================================================
// SetupLobbyPanelCanvasGroups.cs
// Lobby.unity 씬의 탭 패널들에 CanvasGroup을 에디터에서 미리 부착하는 셋업 스크립트.
//
// 배경:
//   공통 UI 규칙 Rule 5 — 패널 표시/숨김은 SetActive 대신 CanvasGroup으로 처리한다.
//   컴포넌트는 런타임 코드(AddComponent)가 아니라 에디터에서 미리 부착하는 것이 원칙이므로,
//   이 스크립트로 4개 탭 패널에 CanvasGroup을 일괄 부착하고 초기값을 세팅한다.
//
// 사용법:
//   Unity 상단 메뉴 → Hexiege/Setup/Lobby 패널 CanvasGroup 설정
//
// Editor 전용 — 빌드에 포함되지 않는다.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Lobby 씬의 탭 패널(Battle/Shop/Profile/Ranking)에 CanvasGroup을 부착하고
    /// 초기 표시 상태(Battle만 보임)를 설정하는 에디터 셋업 도구.
    /// </summary>
    public static class SetupLobbyPanelCanvasGroups
    {
        // Lobby 씬 경로.
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";

        // 셋업 대상 패널 이름들.
        private const string BattlePanelName = "BattlePanel";
        private const string ShopPanelName = "ShopPanel";
        private const string ProfilePanelName = "ProfilePanel";
        private const string RankingPanelName = "RankingPanel";

        /// <summary>
        /// Lobby 씬을 로드하여 4개 탭 패널에 CanvasGroup을 부착하고 초기값을 설정한 뒤 저장한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Lobby 패널 CanvasGroup 설정")]
        public static void Setup()
        {
            // 1) Lobby 씬 로드 (이미 열려있으면 재사용).
            Scene scene = EnsureLobbySceneLoaded();
            if (!scene.IsValid())
            {
                Debug.LogError($"[Setup] Lobby 씬을 로드할 수 없습니다: {LobbyScenePath}");
                return;
            }

            // 2) 4개 패널 탐색 (비활성 포함).
            GameObject battlePanel = FindPanel(scene, BattlePanelName);
            GameObject shopPanel = FindPanel(scene, ShopPanelName);
            GameObject profilePanel = FindPanel(scene, ProfilePanelName);
            GameObject rankingPanel = FindPanel(scene, RankingPanelName);

            // 하나라도 못 찾으면 중단 (잘못 저장 방지).
            if (battlePanel == null || shopPanel == null || profilePanel == null || rankingPanel == null)
            {
                Debug.LogError("[Setup] 일부 패널을 찾지 못해 작업을 중단합니다. " +
                               $"Battle={battlePanel != null}, Shop={shopPanel != null}, " +
                               $"Profile={profilePanel != null}, Ranking={rankingPanel != null}");
                return;
            }

            // 3) 4개 패널 모두 활성화 (CanvasGroup 방식은 GameObject가 항상 활성 상태여야 함).
            battlePanel.SetActive(true);
            shopPanel.SetActive(true);
            profilePanel.SetActive(true);
            rankingPanel.SetActive(true);

            // 4~5) CanvasGroup 부착 + 초기값 설정.
            //   - Battle: 표시 (alpha=1)
            //   - Shop/Profile/Ranking: 숨김 (alpha=0)
            ConfigureCanvasGroup(battlePanel, visible: true);
            ConfigureCanvasGroup(shopPanel, visible: false);
            ConfigureCanvasGroup(profilePanel, visible: false);
            ConfigureCanvasGroup(rankingPanel, visible: false);

            // 6) 씬을 dirty 표시 후 저장.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 7) 완료 로그.
            Debug.Log("[Setup] Lobby 패널 CanvasGroup 설정 완료. " +
                      "Battle=표시, Shop/Profile/Ranking=숨김 으로 저장했습니다.");
        }

        /// <summary>
        /// Lobby 씬이 현재 열려 있으면 그대로 반환하고, 아니면 단일 모드로 로드하여 반환한다.
        /// </summary>
        /// <returns>로드된 Lobby 씬. 실패 시 유효하지 않은 Scene.</returns>
        private static Scene EnsureLobbySceneLoaded()
        {
            // 이미 열려있는지 확인.
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == LobbyScenePath)
                return active;

            // 그 외 로드된 씬들 중에 있는지 확인.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.path == LobbyScenePath)
                    return s;
            }

            // 열려있지 않으면 새로 로드.
            return EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// 씬의 루트 오브젝트들을 순회하며 지정한 이름의 패널을 찾는다 (비활성 포함).
        /// </summary>
        /// <param name="scene">탐색 대상 씬.</param>
        /// <param name="panelName">찾을 패널 이름.</param>
        /// <returns>찾은 GameObject. 없으면 null.</returns>
        private static GameObject FindPanel(Scene scene, string panelName)
        {
            // GetRootGameObjects는 비활성 루트도 포함한다.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // 하위 자식까지 모두 탐색 (includeInactive: true).
                foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t.name == panelName)
                        return t.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// 대상 패널에 CanvasGroup을 확보(없으면 추가)하고 표시/숨김 초기값을 설정한다.
        /// </summary>
        /// <param name="panel">대상 패널 GameObject.</param>
        /// <param name="visible">true=표시(alpha 1), false=숨김(alpha 0).</param>
        private static void ConfigureCanvasGroup(GameObject panel, bool visible)
        {
            // 이미 있으면 재사용, 없으면 추가.
            if (!panel.TryGetComponent<CanvasGroup>(out var group))
                group = panel.AddComponent<CanvasGroup>();

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;

            // Inspector 변경 사항이 씬 저장에 반영되도록 dirty 처리.
            EditorUtility.SetDirty(panel);
        }
    }
}
