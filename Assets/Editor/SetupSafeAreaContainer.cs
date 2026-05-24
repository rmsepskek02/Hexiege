// ============================================================================
// SetupSafeAreaContainer.cs
// ----------------------------------------------------------------------------
// 목적 (1회성 셋업 스크립트):
//   공통 UI 규칙(GameSystemRules.md 규칙 4)에 따라 SafeAreaContainer 구조를 적용한다.
//
//   Game.unity 처리:
//     [UI] Canvas
//       ├─ Background               (Safe Area 밖에 유지 = 전체 화면 배경)
//       └─ SafeAreaContainer        (신규 생성, SafeAreaFitter 부착)
//            ├─ ProductionPopup     (Canvas에서 이동)
//            ├─ BuildingPopup       (Canvas에서 이동)
//            ├─ BuildingActionPanel (Canvas에서 이동)
//            ├─ InGameSettingsPanel (Canvas에서 이동)
//            ├─ ConfirmPopup        (Canvas에서 이동)
//            ├─ GameEndPanel        (Canvas에서 이동)
//            └─ GameHUD             (Canvas에서 이동)
//
//   Lobby.unity 처리:
//     각 Canvas 아래에 SafeAreaContainer 생성, 기존 자식들 이동
//     (Lobby는 Background 같은 전용 풀스크린 배경 오브젝트가 명확하지 않으므로
//      Canvas 직속 자식들을 모두 SafeAreaContainer 안으로 이동한다.)
//
//   ToastUI 처리:
//     ToastUI는 독립 Canvas + DontDestroyOnLoad 오브젝트.
//     SafeAreaContainer 대신 ToastUI의 Canvas GameObject에 SafeAreaFitter 직접 부착.
//
// 주의사항:
//   - Hierarchy 이동 시 GameBootstrapper 등의 SerializeField 참조는 끊기지 않는다.
//     (Unity는 GameObject 인스턴스 ID로 참조하므로 부모만 바뀌어도 참조 유지)
//     단, 일부 자동 검색 로직 또는 PrefabInstance 관계는 확인 필요할 수 있음.
//   - 1회성 스크립트이므로 사용 후 사용자가 직접 삭제하면 됨.
//
// 사용 방법:
//   Unity 메뉴 [Hexiege] -> [Setup] -> [SafeArea 구조 적용]
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hexiege.Editor
{
    /// <summary>
    /// SafeAreaContainer 구조를 씬에 자동 적용하는 1회성 에디터 유틸.
    /// </summary>
    public static class SetupSafeAreaContainer
    {
        // 메뉴 경로
        private const string MenuPath = "Hexiege/Setup/SafeArea 구조 적용";

        // 신규 컨테이너 이름
        private const string SafeAreaContainerName = "SafeAreaContainer";

        // Game.unity의 [UI] Canvas 자식 중 SafeAreaContainer로 이동할 오브젝트 이름들.
        // Background는 명시적으로 이동 제외 (전체화면 배경 역할).
        private static readonly HashSet<string> GameUiObjectsToMove = new HashSet<string>
        {
            "ProductionPopup",
            "BuildingPopup",
            "BuildingActionPanel",
            "InGameSettingsPanel",
            "ConfirmPopup",
            "GameEndPanel",
            "GameHUD",
        };

        // Background는 SafeArea 밖에 유지 (이동 제외)
        private static readonly HashSet<string> ExcludedFromMove = new HashSet<string>
        {
            "Background",
        };

        // 씬 경로
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";

        /// <summary>
        /// 메뉴 클릭 시 진입점.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void RunSetup()
        {
            // 현재 열린 씬의 미저장 변경 사항이 있는지 확인하고 저장 여부를 사용자에게 묻는다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[SetupSafeAreaContainer] 사용자가 저장을 취소하여 작업을 중단합니다.");
                return;
            }

            // 작업 후 원래 씬으로 복구하기 위해 현재 씬 경로를 기억해 둔다.
            string previousScenePath = EditorSceneManager.GetActiveScene().path;

            // 결과 로그 집계용. 다이얼로그로 사용자에게 요약 보여줄 때 사용.
            var resultLog = new List<string>();

            // ============================================================
            // 1) Game.unity 처리
            // ============================================================
            ProcessGameScene(resultLog);

            // ============================================================
            // 2) Lobby.unity 처리 (ToastUI 포함)
            // ============================================================
            ProcessLobbyScene(resultLog);

            // ============================================================
            // 3) 처음 씬으로 복구
            // ============================================================
            if (!string.IsNullOrEmpty(previousScenePath) && System.IO.File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            // ============================================================
            // 4) 결과 다이얼로그 + 경고
            // ============================================================
            string summary = string.Join("\n", resultLog);
            EditorUtility.DisplayDialog(
                "SafeArea 구조 적용",
                "작업 완료.\n\n" + summary + "\n\n" +
                "⚠ 중요: Hierarchy가 변경되었으므로 아래 Inspector 연결을 반드시 확인하세요.\n" +
                "  - GameBootstrapper의 모든 SerializeField (특히 UI 패널들)\n" +
                "  - GameUIManager / NetworkGameManager의 UI 참조\n" +
                "  - 각 패널의 자식 참조는 영향 없음 (직속 부모만 변경됨)\n\n" +
                "1회성 스크립트이므로 직접 삭제하셔도 됩니다.",
                "OK");
        }

        // ====================================================================
        // Game.unity 처리
        // ====================================================================

        /// <summary>
        /// Game.unity를 열어 [UI] Canvas 아래에 SafeAreaContainer를 생성하고
        /// 지정된 오브젝트들을 그 안으로 이동한 뒤 저장한다.
        /// </summary>
        private static void ProcessGameScene(List<string> resultLog)
        {
            if (!System.IO.File.Exists(GameScenePath))
            {
                resultLog.Add($"[스킵] {GameScenePath} (파일 없음)");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // 씬에서 이름이 "[UI]"인 Canvas 오브젝트를 찾는다.
            // FindObjectsByType<Canvas>로 모든 Canvas를 가져온 뒤 이름 매칭.
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Canvas uiCanvas = canvases.FirstOrDefault(c => c.gameObject.name == "[UI]");
            if (uiCanvas == null)
            {
                resultLog.Add($"[{System.IO.Path.GetFileName(GameScenePath)}] '[UI]' Canvas를 찾지 못해 건너뜀");
                return;
            }

            int movedCount = ApplySafeAreaContainer(
                uiCanvas.transform,
                GameUiObjectsToMove,
                ExcludedFromMove,
                strictWhitelist: true);

            if (movedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                resultLog.Add($"[Game.unity] SafeAreaContainer 생성, {movedCount}개 오브젝트 이동 → 저장 완료");
            }
            else
            {
                resultLog.Add($"[Game.unity] 이동할 오브젝트 없음 (이미 적용됐거나 대상 오브젝트가 없음)");
            }
        }

        // ====================================================================
        // Lobby.unity 처리 (ToastUI 포함)
        // ====================================================================

        /// <summary>
        /// Lobby.unity를 열어 모든 Canvas 아래에 SafeAreaContainer를 생성하고
        /// Background 등 제외 오브젝트를 뺀 직속 자식들을 그 안으로 이동한다.
        /// ToastUI를 찾으면 Canvas GO에 SafeAreaFitter를 직접 부착한다.
        /// </summary>
        private static void ProcessLobbyScene(List<string> resultLog)
        {
            if (!System.IO.File.Exists(LobbyScenePath))
            {
                resultLog.Add($"[스킵] {LobbyScenePath} (파일 없음)");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

            bool sceneChanged = false;

            // ---------- ToastUI 처리 ----------
            // ToastUI는 독립 Canvas + DontDestroyOnLoad 오브젝트.
            // SafeAreaContainer 대신 Canvas GameObject 자체에 SafeAreaFitter를 직접 부착한다.
            // 단, RectTransform이 stretch(0,0)-(1,1)이 아니면 SafeAreaFitter가 의도대로 작동하지 않음.
            // → ToastUI는 화면 전체를 차지하는 Canvas이므로 stretch 가정.
            Hexiege.Presentation.ToastUI toastUI = Object.FindFirstObjectByType<Hexiege.Presentation.ToastUI>(FindObjectsInactive.Include);
            // ToastUI가 속한 Canvas의 GameObject를 캐싱. 이후 일반 Canvas 처리에서 중복 적용 방지에도 사용.
            GameObject toastCanvasGO = null;

            if (toastUI != null)
            {
                // ToastUI.gameObject의 RectTransform이 있는지 확인.
                // ToastUI는 Canvas와 같은 GameObject 또는 자식 GameObject에 부착되어 있을 수 있다.
                // Canvas가 부착된 GameObject를 찾아서 거기에 SafeAreaFitter를 부착한다.
                Canvas toastCanvas = toastUI.GetComponentInParent<Canvas>();
                if (toastCanvas != null)
                {
                    toastCanvasGO = toastCanvas.gameObject;
                    Hexiege.Presentation.SafeAreaFitter existing = toastCanvasGO.GetComponent<Hexiege.Presentation.SafeAreaFitter>();
                    if (existing == null)
                    {
                        Undo.AddComponent<Hexiege.Presentation.SafeAreaFitter>(toastCanvasGO);
                        sceneChanged = true;
                        resultLog.Add($"[Lobby.unity] ToastUI Canvas('{toastCanvasGO.name}')에 SafeAreaFitter 부착");
                    }
                    else
                    {
                        resultLog.Add($"[Lobby.unity] ToastUI Canvas('{toastCanvasGO.name}')에 이미 SafeAreaFitter 존재 (스킵)");
                    }
                }
                else
                {
                    resultLog.Add($"[Lobby.unity] ToastUI를 찾았으나 부모 Canvas 없음 → SafeAreaFitter 미부착");
                }
            }
            else
            {
                resultLog.Add($"[Lobby.unity] ToastUI를 씬에서 찾지 못함 → SafeAreaFitter 미부착");
            }

            // ---------- 일반 Canvas 처리 ----------
            // ToastUI Canvas는 위에서 별도 처리했으므로 제외한다.
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int processedCanvasCount = 0;
            foreach (Canvas canvas in canvases)
            {
                // ToastUI의 Canvas는 위에서 SafeAreaFitter 직접 부착했으므로 컨테이너 적용 제외.
                if (toastCanvasGO != null && canvas.gameObject == toastCanvasGO)
                {
                    continue;
                }

                // 자식 Canvas (nested)는 자체 좌표계가 부모 Canvas에 종속되므로 컨테이너 적용 제외.
                // 최상위 Canvas만 처리한다. → 부모 Canvas가 있으면 스킵.
                Canvas parentCanvas = canvas.transform.parent != null
                    ? canvas.transform.parent.GetComponentInParent<Canvas>()
                    : null;
                if (parentCanvas != null)
                {
                    continue;
                }

                // Lobby는 strictWhitelist=false → Background 제외 외 모든 자식 이동
                int moved = ApplySafeAreaContainer(
                    canvas.transform,
                    moveWhitelist: null,
                    excludedNames: ExcludedFromMove,
                    strictWhitelist: false);

                if (moved > 0)
                {
                    processedCanvasCount++;
                    sceneChanged = true;
                    resultLog.Add($"[Lobby.unity] Canvas '{canvas.gameObject.name}' → {moved}개 오브젝트 이동");
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                resultLog.Add($"[Lobby.unity] 저장 완료 (Canvas {processedCanvasCount}개에 SafeAreaContainer 적용)");
            }
            else
            {
                resultLog.Add($"[Lobby.unity] 변경 사항 없음");
            }
        }

        // ====================================================================
        // 공통 헬퍼: SafeAreaContainer 생성 + 자식 이동
        // ====================================================================

        /// <summary>
        /// 주어진 Canvas Transform 아래에 SafeAreaContainer GameObject를 생성하고,
        /// 지정된 직속 자식 오브젝트들을 그 안으로 이동시킨다.
        /// </summary>
        /// <param name="canvasTransform">SafeAreaContainer를 추가할 Canvas의 Transform.</param>
        /// <param name="moveWhitelist">strictWhitelist=true일 때 이 목록에 포함된 이름만 이동.</param>
        /// <param name="excludedNames">strictWhitelist 여부와 무관하게 항상 이동 제외할 이름.</param>
        /// <param name="strictWhitelist">true면 moveWhitelist 안의 이름만 이동, false면 excludedNames 외 모든 직속 자식 이동.</param>
        /// <returns>실제로 이동한 자식 개수.</returns>
        private static int ApplySafeAreaContainer(
            Transform canvasTransform,
            HashSet<string> moveWhitelist,
            HashSet<string> excludedNames,
            bool strictWhitelist)
        {
            // 이미 SafeAreaContainer가 있으면 새로 만들지 않고 재사용.
            Transform existingContainer = canvasTransform.Find(SafeAreaContainerName);
            GameObject containerGO;
            bool createdNew;

            if (existingContainer != null)
            {
                containerGO = existingContainer.gameObject;
                createdNew = false;
            }
            else
            {
                // 신규 GameObject 생성. Undo 등록으로 실수 시 복구 가능하게 만듦.
                containerGO = new GameObject(SafeAreaContainerName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(containerGO, "Create SafeAreaContainer");
                createdNew = true;

                // Canvas 직속 자식으로 배치. setParentUndo=true.
                Undo.SetTransformParent(containerGO.transform, canvasTransform, "Parent SafeAreaContainer");

                // RectTransform stretch 설정: 부모(Canvas) 전체를 채움.
                RectTransform rt = containerGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.localPosition = Vector3.zero;

                // SafeAreaFitter 컴포넌트 부착.
                Undo.AddComponent<Hexiege.Presentation.SafeAreaFitter>(containerGO);
            }

            // SafeAreaContainer를 Canvas 직속 자식 중 가장 마지막 위치로 보낸다.
            // (Background보다 뒤에 그려져야 하기 때문 - 자식 중 뒤에 있을수록 위에 그려짐)
            containerGO.transform.SetAsLastSibling();

            // 이동 대상 자식들을 수집.
            // canvasTransform의 직속 자식들을 순회 (자기 자신과 SafeAreaContainer는 제외).
            List<Transform> toMove = new List<Transform>();
            for (int i = 0; i < canvasTransform.childCount; i++)
            {
                Transform child = canvasTransform.GetChild(i);

                // SafeAreaContainer 자기 자신은 건너뜀
                if (child == containerGO.transform) continue;

                // 항상 제외할 이름인지 검사 (Background 등)
                if (excludedNames != null && excludedNames.Contains(child.name))
                {
                    continue;
                }

                if (strictWhitelist)
                {
                    // 화이트리스트 모드: 명시된 이름만 이동
                    if (moveWhitelist != null && moveWhitelist.Contains(child.name))
                    {
                        toMove.Add(child);
                    }
                }
                else
                {
                    // 블랙리스트 모드: excludedNames 외 모두 이동
                    toMove.Add(child);
                }
            }

            // 실제 이동 수행. Undo.SetTransformParent로 기록.
            // SetParent의 worldPositionStays=false → UI 좌표계에서 위치/크기 그대로 유지.
            foreach (Transform child in toMove)
            {
                Undo.SetTransformParent(child, containerGO.transform, $"Move {child.name} into SafeAreaContainer");

                // RectTransform 값 복원: SetTransformParent가 worldPositionStays 옵션을 지원하지 않으므로,
                // RectTransform의 anchorPosition/sizeDelta는 부모만 바뀌고 그대로 유지된다.
                // Canvas → SafeAreaContainer 둘 다 같은 stretch 영역을 차지하므로 시각적 차이 없음.
            }

            if (createdNew)
            {
                Debug.Log($"[SetupSafeAreaContainer] '{canvasTransform.name}' 아래에 SafeAreaContainer 신규 생성, {toMove.Count}개 자식 이동.");
            }
            else if (toMove.Count > 0)
            {
                Debug.Log($"[SetupSafeAreaContainer] '{canvasTransform.name}' 아래 기존 SafeAreaContainer에 {toMove.Count}개 자식 추가 이동.");
            }

            return toMove.Count + (createdNew ? 1 : 0); // 신규 생성도 변경 1건으로 집계 (반환은 단순 이동 카운트 + 생성 보너스)
        }
    }
}
#endif
