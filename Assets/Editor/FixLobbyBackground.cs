// ============================================================================
// FixLobbyBackground.cs
// ----------------------------------------------------------------------------
// 목적 (1회성 셋업 스크립트):
//   로비 화면에서 배경이 Safe Area 경계에서 끊기는 문제를 수정한다.
//   GameSystemRules.md Rule 4에 따라 전체화면 배경 오브젝트를 SafeAreaContainer 밖
//   (Canvas 직속)으로 분리하고, LobbyRoot의 기존 Image 컴포넌트를 비활성화한다.
//
//   현재 구조 (문제 상태):
//     Canvas
//     └── SafeAreaContainer ← SafeAreaFitter 부착 (Safe Area 크기로 축소됨)
//         └── LobbyRoot ← Image 컴포넌트(남색 배경) + LobbyRootView
//
//   수정 후 구조 (목표 상태):
//     Canvas
//     ├── LobbyBackground ← 신규 생성. 전체화면 stretch + Image(남색 배경)
//     └── SafeAreaContainer
//         └── LobbyRoot ← Image 컴포넌트 비활성화 (배경 역할 없앰)
//
//   렌더 순서:
//     Unity UI는 Hierarchy에서 위에 있는 자식일수록 먼저(뒤에) 그려진다.
//     LobbyBackground를 SafeAreaContainer보다 앞에 배치해야 배경이 UI 아래에 그려진다.
//
// 사용 방법:
//   Unity 메뉴 [Hexiege] -> [Setup] -> [로비 배경 Safe Area 수정]
//
// 실행 후:
//   - 변경 사항은 Undo(Ctrl+Z)로 되돌릴 수 있다.
//   - 문제가 없으면 이 스크립트 파일을 삭제해도 된다.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hexiege.Editor
{
    /// <summary>
    /// Lobby.unity 로비 배경 Safe Area 문제를 수정하는 1회성 에디터 유틸.
    /// </summary>
    public static class FixLobbyBackground
    {
        // 메뉴 경로
        private const string MenuPath = "Hexiege/Setup/로비 배경 Safe Area 수정";

        // 대상 씬 경로
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";

        // 신규 생성할 배경 오브젝트 이름
        private const string BackgroundObjectName = "LobbyBackground";

        // LobbyRoot의 기존 Image 색상 그대로 사용 (r:0.059 g:0.059 b:0.102 a:1)
        private static readonly Color BackgroundColor = new Color(0.059f, 0.059f, 0.102f, 1f);

        /// <summary>
        /// 메뉴 클릭 시 진입점.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void RunFix()
        {
            // 현재 씬에 미저장 변경이 있으면 저장 여부를 사용자에게 묻는다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[FixLobbyBackground] 사용자가 저장을 취소하여 작업을 중단합니다.");
                return;
            }

            // 작업 완료 후 원래 씬으로 복구하기 위해 현재 경로를 기억해 둔다.
            string previousScenePath = EditorSceneManager.GetActiveScene().path;

            if (!System.IO.File.Exists(LobbyScenePath))
            {
                EditorUtility.DisplayDialog("오류", $"Lobby.unity를 찾을 수 없습니다.\n{LobbyScenePath}", "OK");
                return;
            }

            // Lobby.unity를 Single 모드로 연다 (다른 씬 닫기).
            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

            // ================================================================
            // 계층 탐색: LobbyRootView 컴포넌트를 기준으로 부모 계층을 역추적한다.
            //   LobbyRootView 부착 오브젝트 = LobbyRoot
            //   LobbyRoot의 부모 = SafeAreaContainer
            //   SafeAreaContainer의 부모 = Canvas
            // ================================================================
            var lobbyRootView = Object.FindFirstObjectByType<Hexiege.Presentation.LobbyRootView>(
                FindObjectsInactive.Include);

            if (lobbyRootView == null)
            {
                EditorUtility.DisplayDialog("오류",
                    "씬에서 LobbyRootView 컴포넌트를 찾지 못했습니다.\n" +
                    "LobbyRoot 오브젝트에 LobbyRootView 스크립트가 부착되어 있는지 확인하세요.",
                    "OK");
                RestoreScene(previousScenePath);
                return;
            }

            Transform lobbyRoot = lobbyRootView.transform;
            Transform safeAreaContainer = lobbyRoot.parent;
            Transform canvasTransform = safeAreaContainer?.parent;

            // 예상과 다른 계층 구조인 경우 중단.
            if (safeAreaContainer == null || canvasTransform == null)
            {
                EditorUtility.DisplayDialog("오류",
                    "예상과 다른 계층 구조입니다. 작업을 중단합니다.\n\n" +
                    $"LobbyRoot: {lobbyRoot.name}\n" +
                    $"LobbyRoot 부모: {(safeAreaContainer?.name ?? "(없음)")}\n" +
                    $"SafeAreaContainer 부모: {(canvasTransform?.name ?? "(없음)")}",
                    "OK");
                RestoreScene(previousScenePath);
                return;
            }

            var resultLog = new List<string>();
            bool sceneChanged = false;

            // ================================================================
            // 단계 1 — LobbyBackground 오브젝트 생성
            // ================================================================

            // 이미 동일 이름의 오브젝트가 Canvas 직속 자식으로 존재하면 중복 생성하지 않는다.
            Transform existingBg = canvasTransform.Find(BackgroundObjectName);
            if (existingBg != null)
            {
                resultLog.Add($"[스킵] '{BackgroundObjectName}'이 이미 존재합니다.");
            }
            else
            {
                // 새 GameObject 생성. Undo 등록으로 Ctrl+Z 복구 가능하게 만든다.
                var bgGO = new GameObject(BackgroundObjectName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(bgGO, "Create LobbyBackground");

                // Canvas의 직속 자식으로 연결한다.
                // setWorldPositionStays=false → UI 좌표계를 그대로 유지.
                Undo.SetTransformParent(bgGO.transform, canvasTransform, "Parent LobbyBackground to Canvas");

                // RectTransform: 전체 화면을 채우도록 stretch 설정
                //   anchorMin(0,0) ~ anchorMax(1,1) = 부모(Canvas) 전체 커버
                //   offsetMin/Max = 0 → 여백 없음
                var rt = bgGO.GetComponent<RectTransform>();
                rt.anchorMin  = Vector2.zero;
                rt.anchorMax  = Vector2.one;
                rt.offsetMin  = Vector2.zero;
                rt.offsetMax  = Vector2.zero;
                rt.localScale    = Vector3.one;
                rt.localPosition = Vector3.zero;

                // SafeAreaContainer의 현재 sibling index를 가져온다.
                // LobbyBackground를 그 자리에 끼워 넣으면 SafeAreaContainer가 한 칸 뒤로 밀린다.
                // → 결과: LobbyBackground(먼저/뒤에 그려짐) → SafeAreaContainer(나중/앞에 그려짐)
                int safeAreaIndex = safeAreaContainer.GetSiblingIndex();
                bgGO.transform.SetSiblingIndex(safeAreaIndex);

                // Image 컴포넌트 추가 + 배경 설정
                //   raycastTarget=false: 배경이 터치 이벤트를 가로채지 않도록 설정
                var img = Undo.AddComponent<Image>(bgGO);
                img.color = BackgroundColor;
                img.raycastTarget = false;

                resultLog.Add(
                    $"'{BackgroundObjectName}' 생성 완료\n" +
                    $"  - 위치: Canvas 직속 자식, SafeAreaContainer(index {safeAreaIndex + 1}) 앞\n" +
                    $"  - Color: {BackgroundColor}\n" +
                    $"  - raycastTarget: false");

                sceneChanged = true;
            }

            // ================================================================
            // 단계 2 — LobbyRoot의 Image 컴포넌트 비활성화
            // ================================================================

            var lobbyRootImage = lobbyRoot.GetComponent<Image>();

            if (lobbyRootImage == null)
            {
                resultLog.Add("[스킵] LobbyRoot에 Image 컴포넌트가 없습니다.");
            }
            else if (!lobbyRootImage.enabled)
            {
                resultLog.Add("[스킵] LobbyRoot의 Image 컴포넌트가 이미 비활성화되어 있습니다.");
            }
            else
            {
                // Undo.RecordObject로 변경 전 상태를 기록해 Ctrl+Z 복구를 지원한다.
                Undo.RecordObject(lobbyRootImage, "Disable LobbyRoot Image");
                lobbyRootImage.enabled = false;
                resultLog.Add("LobbyRoot의 Image 컴포넌트 비활성화 완료");
                sceneChanged = true;
            }

            // ================================================================
            // 씬 저장
            // ================================================================
            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                resultLog.Add($"\nLobby.unity 저장 {(saved ? "성공" : "실패")}");
            }
            else
            {
                resultLog.Add("\n변경 사항이 없어 저장을 생략했습니다.");
            }

            // 원래 씬으로 복구
            RestoreScene(previousScenePath);

            // 결과 다이얼로그
            EditorUtility.DisplayDialog(
                "로비 배경 Safe Area 수정",
                "작업 완료.\n\n" + string.Join("\n", resultLog) + "\n\n" +
                "실기기에서 Safe Area 경계에 관계없이 배경이 전체 화면을 채우는지 확인하세요.\n" +
                "문제가 없으면 이 스크립트 파일을 삭제하셔도 됩니다.",
                "OK");
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// 지정된 경로의 씬으로 복구한다. 경로가 없거나 파일이 없으면 아무것도 하지 않는다.
        /// </summary>
        private static void RestoreScene(string scenePath)
        {
            if (!string.IsNullOrEmpty(scenePath) && System.IO.File.Exists(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
        }
    }
}
#endif
