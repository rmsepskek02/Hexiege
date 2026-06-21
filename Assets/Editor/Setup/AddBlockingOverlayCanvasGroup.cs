// ============================================================================
// AddBlockingOverlayCanvasGroup.cs
//
// [1회성 에디터 셋업 스크립트]
//
// 목적:
//   Login.unity 씬의 NetworkErrorPopup(=ConfirmPopup) 및 AnonymousWarningPopup 하위
//   BlockingOverlay GameObject에 CanvasGroup 컴포넌트가 없어서 발생하는 버그를 수정한다.
//
//   버그 증상: 실기기에서 "Tap to Start" 후 로그인 화면 전체에 검정 반투명 오버레이가 덮임.
//   근본 원인: BlockingOverlay에 CanvasGroup이 없어 각 팝업의 _blockingOverlay(CanvasGroup 타입)이
//             런타임에 null → Hide() 시 alpha=0 처리 불가 → 검정 반투명 Image가 그대로 노출.
//
//   해결: 두 팝업의 BlockingOverlay 모두에 CanvasGroup을 추가하고
//        초기값(alpha=0, interactable/blocksRaycasts=false)으로 설정한 뒤 각 필드에 연결한다.
//
// 사용법:
//   Unity 상단 메뉴 → Hexiege/Setup/BlockingOverlay CanvasGroup 추가 클릭
//   실행 후 이 파일은 삭제해도 무방하다.
// ============================================================================

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Login.unity 씬의 두 팝업(NetworkErrorPopup, AnonymousWarningPopup)
    /// BlockingOverlay에 CanvasGroup을 추가/설정하는 1회성 셋업 도구.
    /// </summary>
    public static class AddBlockingOverlayCanvasGroup
    {
        // 대상 씬 경로 (프로젝트 루트 기준)
        private const string LoginScenePath = "Assets/_Project/Scenes/Login.unity";

        // 탐색에 사용할 GameObject 이름들
        private const string NetworkErrorPopupName = "NetworkErrorPopup";
        private const string AnonymousWarningPopupName = "AnonymousWarningPopup";
        private const string BlockingOverlayName = "BlockingOverlay";

        // 직렬화 필드 이름 (두 팝업 모두 동일)
        private const string BlockingOverlayFieldName = "_blockingOverlay";

        /// <summary>
        /// 메뉴에서 실행되는 진입점. Login 씬을 열고 두 팝업의 BlockingOverlay에
        /// CanvasGroup을 셋업한 뒤 저장한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/BlockingOverlay CanvasGroup 추가")]
        public static void Run()
        {
            // ── 1) Login 씬 확보 ───────────────────────────────────────────
            // 이미 열려 있으면 그대로 사용, 아니면 OpenScene으로 연다.
            Scene scene = EnsureLoginSceneOpen();
            if (!scene.IsValid())
            {
                Debug.LogError($"[BlockingOverlay 셋업] Login 씬을 열 수 없습니다: {LoginScenePath}");
                return;
            }

            // ── 2) NetworkErrorPopup 처리 ──────────────────────────────────
            SetupPopupBlockingOverlay<ConfirmPopup>(scene, NetworkErrorPopupName);

            // ── 3) AnonymousWarningPopup 처리 ─────────────────────────────
            SetupPopupBlockingOverlay<AnonymousWarningPopup>(scene, AnonymousWarningPopupName);

            // ── 4) 씬 저장 ─────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // ── 5) 완료 로그 ───────────────────────────────────────────────
            Debug.Log(
                $"[BlockingOverlay 셋업] 완료. 두 팝업 모두 CanvasGroup(alpha=0, interactable=false, blocksRaycasts=false) " +
                $"설정 및 씬 저장됨: {LoginScenePath}");
        }

        /// <summary>
        /// 지정한 팝업 GameObject의 BlockingOverlay에 CanvasGroup을 추가하고
        /// 팝업 컴포넌트의 _blockingOverlay 필드에 연결한다.
        /// </summary>
        private static void SetupPopupBlockingOverlay<T>(Scene scene, string popupName) where T : Component
        {
            // 팝업 GameObject 탐색
            GameObject popupGo = FindGameObjectByName(scene, popupName);
            if (popupGo == null)
            {
                Debug.LogError($"[BlockingOverlay 셋업] '{popupName}' GameObject를 찾지 못했습니다.");
                return;
            }

            // 팝업 하위에서 BlockingOverlay 탐색
            Transform overlayTransform = FindChildByName(popupGo.transform, BlockingOverlayName);
            if (overlayTransform == null)
            {
                Debug.LogError(
                    $"[BlockingOverlay 셋업] '{popupName}' 하위에서 '{BlockingOverlayName}'를 찾지 못했습니다.");
                return;
            }
            GameObject overlayGo = overlayTransform.gameObject;

            // CanvasGroup 추가 또는 가져오기
            // 이미 있으면 경고 없이 값만 갱신, 없으면 새로 추가한다.
            CanvasGroup canvasGroup = overlayGo.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = Undo.AddComponent<CanvasGroup>(overlayGo);
                Debug.Log($"[BlockingOverlay 셋업] '{popupName}/{BlockingOverlayName}'에 CanvasGroup을 추가했습니다.");
            }
            else
            {
                Debug.Log($"[BlockingOverlay 셋업] '{popupName}/{BlockingOverlayName}'에 CanvasGroup이 이미 있어 값만 설정합니다.");
            }

            // CanvasGroup 초기값 설정 — 시작 시 완전히 투명 + 입력 통과 상태
            Undo.RecordObject(canvasGroup, $"Setup {popupName} BlockingOverlay CanvasGroup");
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            EditorUtility.SetDirty(canvasGroup);
            EditorUtility.SetDirty(overlayGo);

            // 팝업 컴포넌트의 _blockingOverlay 필드 연결
            T popupComponent = popupGo.GetComponent<T>();
            if (popupComponent == null)
            {
                Debug.LogWarning(
                    $"[BlockingOverlay 셋업] '{popupName}'에 {typeof(T).Name}이 없어 필드 연결을 건너뜁니다.");
                return;
            }
            LinkBlockingOverlayField(popupComponent, canvasGroup, popupName);
        }

        /// <summary>
        /// Login 씬이 이미 열려 있으면 그 씬을, 아니면 OpenScene으로 새로 열어 반환한다.
        /// </summary>
        private static Scene EnsureLoginSceneOpen()
        {
            // 현재 열린 씬 중 Login 씬이 있는지 확인한다.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == LoginScenePath && loaded.isLoaded)
                {
                    return loaded;
                }
            }

            // 열려 있지 않으면 단독 모드로 연다.
            return EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// 씬의 루트 오브젝트들을 순회하며 지정한 이름의 GameObject를 깊이 우선으로 탐색한다.
        /// 비활성 오브젝트도 포함한다.
        /// </summary>
        private static GameObject FindGameObjectByName(Scene scene, string targetName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                if (root.name == targetName)
                {
                    return root;
                }

                Transform found = FindChildByName(root.transform, targetName);
                if (found != null)
                {
                    return found.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// 주어진 부모의 모든 하위 계층(비활성 포함)에서 지정한 이름의 Transform을 재귀 탐색한다.
        /// </summary>
        private static Transform FindChildByName(Transform parent, string targetName)
        {
            // GetComponentsInChildren(true)는 비활성 자식도 포함하여 가져온다.
            Transform[] all = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                // 자기 자신은 제외하고 자식만 검사한다.
                if (t != parent && t.name == targetName)
                {
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// SerializedObject를 통해 팝업 컴포넌트의 private 필드 _blockingOverlay가 비어 있으면 연결한다.
        /// Component 기반으로 동작하므로 ConfirmPopup, AnonymousWarningPopup 등 어느 타입이든 사용 가능하다.
        /// 이미 올바르게 연결되어 있으면 변경하지 않는다.
        /// </summary>
        private static void LinkBlockingOverlayField(Component popupComponent, CanvasGroup canvasGroup, string popupName)
        {
            var so = new SerializedObject(popupComponent);
            SerializedProperty prop = so.FindProperty(BlockingOverlayFieldName);
            if (prop == null)
            {
                Debug.LogWarning(
                    $"[BlockingOverlay 셋업] {popupComponent.GetType().Name}에서 '{BlockingOverlayFieldName}' 필드를 찾지 못했습니다.");
                return;
            }

            // 이미 같은 CanvasGroup이 연결되어 있으면 변경하지 않는다.
            if (prop.objectReferenceValue == canvasGroup)
            {
                Debug.Log($"[BlockingOverlay 셋업] {popupName}.{BlockingOverlayFieldName}는 이미 올바르게 연결됨.");
                return;
            }

            prop.objectReferenceValue = canvasGroup;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(popupComponent);
            Debug.Log($"[BlockingOverlay 셋업] {popupName}.{BlockingOverlayFieldName} 필드를 재연결했습니다.");
        }
    }
}
