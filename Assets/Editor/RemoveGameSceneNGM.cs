using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Infrastructure;

namespace Hexiege.Editor
{
    /// <summary>
    /// Game.unity 씬에서 사용하지 않게 된 NetworkGameManager GameObject를 제거하는 1회성 Editor 도구.
    ///
    /// 배경:
    /// - GameBootstrapper에서 _networkGameManager 필드가 제거되어 Game.unity의 NetworkGameManager 오브젝트는
    ///   더 이상 어떤 코드에서도 참조되지 않는 고아(orphan) 오브젝트가 되었다.
    /// - Lobby.unity에도 NetworkGameManager가 존재하므로, 반드시 Game 씬에 속한 오브젝트만 골라 제거해야 한다.
    ///
    /// 동작 요약:
    /// 1) Game.unity가 이미 열려 있으면 그 씬을 사용하고, 아니면 Additive(추가) 방식으로 임시로 연다.
    /// 2) Game 씬에 속한 NetworkGameManager를 찾는다(go.scene.name == "Game"으로 씬 소속을 구분).
    /// 3) 찾으면 Undo.DestroyObjectImmediate로 삭제한다(에디터에서 Ctrl+Z 되돌리기 지원).
    /// 4) 변경된 Game 씬을 저장한다.
    /// 5) 우리가 Additive로 직접 연 경우에는 작업이 끝난 뒤 다시 닫는다.
    /// </summary>
    public static class RemoveGameSceneNGM
    {
        // Game 씬의 에셋 경로. 씬을 추가로 열어야 할 때 사용한다.
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // 씬 소속을 구분할 때 비교할 씬 이름(확장자 제외).
        private const string GameSceneName = "Game";

        /// <summary>
        /// 메뉴에서 호출되는 진입점. Game 씬의 NetworkGameManager를 찾아 제거하고 씬을 저장한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Game씬 NGM 제거")]
        public static void RemoveNetworkGameManagerFromGameScene()
        {
            // Game.unity가 현재 에디터에 이미 열려 있는지 먼저 확인한다.
            // SceneManager.GetSceneByName은 "이름"이 일치하는 (로드된) 씬을 반환한다.
            // 열려 있지 않거나 로드되지 않았다면 IsValid()/isLoaded가 false가 된다.
            Scene gameScene = SceneManager.GetSceneByName(GameSceneName);

            // 우리가 이 함수 안에서 직접 Additive로 열었는지 여부.
            // true이면 작업이 끝난 뒤 우리가 연 씬을 다시 닫아 원래 상태로 되돌린다.
            bool openedByThisTool = false;

            // Game 씬이 아직 열려 있지 않다면 Additive(추가) 방식으로 임시로 연다.
            // Additive로 열면 현재 보고 있는 씬을 닫지 않고 Game 씬을 함께 로드한다.
            if (!gameScene.IsValid() || !gameScene.isLoaded)
            {
                // 경로에 실제 씬 파일이 존재하는지 확인한다(경로 오타/이동 대비).
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
                {
                    EditorUtility.DisplayDialog(
                        "Game씬 NGM 제거 실패",
                        $"Game 씬 파일을 찾을 수 없습니다:\n{GameScenePath}",
                        "확인");
                    return;
                }

                gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
                openedByThisTool = true;
            }

            try
            {
                // 씬에 배치된 모든 NetworkGameManager 컴포넌트를 찾는다.
                // FindObjectsByType은 (비활성 포함, 정렬 없음 옵션으로) 로드된 모든 씬에서 검색하므로
                // 아래에서 go.scene.name으로 "Game 씬에 속한 것"만 골라낸다.
                NetworkGameManager[] allManagers =
                    Object.FindObjectsByType<NetworkGameManager>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

                // 실제로 제거한 GameObject 개수.
                int removedCount = 0;

                foreach (NetworkGameManager manager in allManagers)
                {
                    // 파괴된 참조 방어(이전 반복에서 함께 제거됐을 수 있음).
                    if (manager == null)
                    {
                        continue;
                    }

                    GameObject go = manager.gameObject;

                    // 핵심: 이 오브젝트가 "Game 씬"에 속해 있을 때만 제거한다.
                    // Lobby.unity에도 NetworkGameManager가 있으므로 씬 소속으로 반드시 구분해야 한다.
                    if (go.scene.name != GameSceneName)
                    {
                        continue;
                    }

                    // Undo.DestroyObjectImmediate: 즉시 파괴하되 에디터의 Undo(Ctrl+Z) 기록에 남긴다.
                    // 에디터 씬 편집에서는 Destroy가 아니라 DestroyImmediate 계열을 사용해야 한다.
                    Undo.DestroyObjectImmediate(go);
                    removedCount++;
                }

                // 하나도 찾지 못했다면 이미 제거됐거나 애초에 없는 상태이므로 안내 후 종료한다.
                if (removedCount == 0)
                {
                    EditorUtility.DisplayDialog(
                        "Game씬 NGM 제거",
                        "Game 씬에서 NetworkGameManager를 찾지 못했습니다.\n" +
                        "이미 제거됐거나 존재하지 않습니다.",
                        "확인");
                    return;
                }

                // 변경된 Game 씬을 디스크에 저장한다.
                EditorSceneManager.SaveScene(gameScene);

                EditorUtility.DisplayDialog(
                    "Game씬 NGM 제거 완료",
                    $"Game 씬에서 NetworkGameManager {removedCount}개를 제거하고 씬을 저장했습니다.",
                    "확인");
            }
            finally
            {
                // 우리가 이 도구 안에서 Additive로 직접 연 경우에만 다시 닫아 원래 상태로 되돌린다.
                // (사용자가 원래부터 Game 씬을 열어 두고 있었다면 닫지 않는다.)
                // 두 번째 인자 removeScene=true: 메모리에서 씬을 완전히 언로드한다.
                if (openedByThisTool && gameScene.IsValid() && gameScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(gameScene, true);
                }
            }
        }
    }
}
