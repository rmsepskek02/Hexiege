// ============================================================================
// LobbySetupUtils.cs
// 로비 씬 분리 후 설정을 자동화하는 에디터 유틸리티.
//
// 기능:
//   1. Build Settings에 Lobby(index 0), Game(index 1) 등록
//   2. Game.unity의 NetworkManager 오브젝트를 Lobby.unity로 이전
//
// 사용법:
//   Hexiege/Setup/1. Fix Build Settings
//   Hexiege/Setup/2. Migrate NetworkManager to Lobby
//
// Editor 전용 스크립트.
// ============================================================================

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Hexiege.Editor
{
    public static class LobbySetupUtils
    {
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";
        private const string GameScenePath  = "Assets/_Project/Scenes/Game.unity";

        // ====================================================================
        // STEP 2 — Build Settings 등록
        // ====================================================================

        [MenuItem("Hexiege/Setup/1. Fix Build Settings")]
        public static void FixBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(GameScenePath,  true),
            };

            EditorBuildSettings.scenes = scenes;
            Debug.Log("[LobbySetupUtils] Build Settings 등록 완료: Lobby(0), Game(1)");
        }

        // ====================================================================
        // STEP 3 — Game.unity → Lobby.unity NetworkManager 이전
        // ====================================================================

        [MenuItem("Hexiege/Setup/2. Migrate NetworkManager to Lobby")]
        public static void MigrateNetworkManager()
        {

            // Lobby.unity를 메인 씬으로 열기
            var lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

            // Game.unity를 Additive로 열기
            var gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

            // Game 씬에서 NetworkManager 찾기
            GameObject networkManagerGo = null;
            foreach (var root in gameScene.GetRootGameObjects())
            {
                if (root.name == "NetworkManager")
                {
                    networkManagerGo = root;
                    break;
                }
            }

            if (networkManagerGo == null)
            {
                // "NetworkManager"라는 이름이 아닐 수 있으니 Unity.Netcode.NetworkManager 컴포넌트로 재탐색
                foreach (var root in gameScene.GetRootGameObjects())
                {
                    if (root.GetComponent<Unity.Netcode.NetworkManager>() != null)
                    {
                        networkManagerGo = root;
                        break;
                    }
                }
            }

            if (networkManagerGo == null)
            {
                Debug.LogError("[LobbySetupUtils] Game.unity에서 NetworkManager를 찾을 수 없습니다. 이미 이전됐거나 오브젝트 이름이 다를 수 있습니다.");
                EditorSceneManager.CloseScene(gameScene, true);
                return;
            }

            // Lobby 씬으로 이동
            SceneManager.MoveGameObjectToScene(networkManagerGo, lobbyScene);

            // 두 씬 저장
            EditorSceneManager.SaveScene(lobbyScene);
            EditorSceneManager.SaveScene(gameScene);

            // Game 씬 언로드
            EditorSceneManager.CloseScene(gameScene, false);

            Debug.Log($"[LobbySetupUtils] '{networkManagerGo.name}' → Lobby.unity 이전 완료. 두 씬 모두 저장됨.");
        }
    }
}

#endif
