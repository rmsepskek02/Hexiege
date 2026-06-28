// ============================================================================
// MigrateBlockingOverlayToUIManager.cs
// UIManager 단일 소유 BlockingOverlay 씬 마이그레이션 에디터 스크립트.
//
// 목적:
//   Login.unity에서 UIManager Canvas 직속에 BlockingOverlay GameObject를 생성하고
//   UIManager 컴포넌트의 _blockingOverlay / _blockingOverlayButton 필드를 자동 연결한다.
//   Game.unity에서 RematchRequestPopup > Overlay Image의 RaycastTarget을 비활성화한다.
//
// 사용 방법:
//   메뉴: Hexiege > Setup > BlockingOverlay UIManager 씬 마이그레이션 실행
//
// ⚠️  실행 전 씬을 저장해두는 것을 권장한다 (Ctrl+S).
//      이 스크립트는 Login.unity와 Game.unity를 열고 수정한 뒤 저장한다.
//      현재 열린 씬의 저장되지 않은 변경 사항이 있으면 실행 전 확인 다이얼로그가 뜬다.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace Hexiege.Editor.Setup
{
    public static class MigrateBlockingOverlayToUIManager
    {
        // 씬 경로
        private const string LoginScenePath = "Assets/_Project/Scenes/Login.unity";
        private const string GameScenePath  = "Assets/_Project/Scenes/Game.unity";

        // Login 씬에서 찾을 오브젝트 이름
        private const string UIManagerCanvasName  = "UIManager Canvas";
        private const string BlockingOverlayName  = "BlockingOverlay";
        private const string UIManagerObjectName  = "UIManager";

        // Game 씬에서 찾을 오브젝트 이름
        private const string RematchPopupName     = "RematchRequestPopup";
        private const string OverlayName          = "Overlay";

        // ====================================================================
        // 메뉴 항목
        // ====================================================================

        [MenuItem("Hexiege/Setup/BlockingOverlay UIManager 씬 마이그레이션 실행")]
        private static void RunMigration()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "BlockingOverlay UIManager 마이그레이션",
                "Login.unity와 Game.unity를 열고 아래 작업을 자동으로 수행합니다.\n\n" +
                "[Login.unity]\n" +
                "• UIManager Canvas 직속에 BlockingOverlay 생성\n" +
                "• UIManager의 _blockingOverlay / _blockingOverlayButton 필드 연결\n\n" +
                "[Game.unity]\n" +
                "• RematchRequestPopup > Overlay Image의 RaycastTarget 비활성화\n\n" +
                "계속하시겠습니까?",
                "실행", "취소"
            );

            if (!confirmed) return;

            // 현재 열린 씬 저장 여부 확인
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            bool loginOk = MigrateLoginScene();
            bool gameOk  = MigrateGameScene();

            string result =
                $"[Login.unity] {(loginOk ? "✓ 완료" : "✗ 실패 — 콘솔 로그 확인")}\n" +
                $"[Game.unity]  {(gameOk  ? "✓ 완료" : "✗ 실패 — 콘솔 로그 확인")}";

            EditorUtility.DisplayDialog("마이그레이션 결과", result, "확인");
            Debug.Log($"[MigrateBlockingOverlayToUIManager] 완료\n{result}");
        }

        // ====================================================================
        // Login.unity 마이그레이션
        // ====================================================================

        private static bool MigrateLoginScene()
        {
            // 씬 열기
            var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Migration] Login.unity를 열 수 없습니다: {LoginScenePath}");
                return false;
            }

            // UIManager Canvas 탐색
            var uiManagerCanvas = FindInScene<Canvas>(UIManagerCanvasName);
            if (uiManagerCanvas == null)
            {
                Debug.LogError($"[Migration] '{UIManagerCanvasName}' Canvas를 Login 씬에서 찾을 수 없습니다.");
                return false;
            }

            // BlockingOverlay가 이미 UIManager Canvas 직속 자식으로 존재하는지 확인
            var existingOverlay = uiManagerCanvas.transform
                .Cast<Transform>()
                .FirstOrDefault(t => t.name == BlockingOverlayName);

            GameObject overlayGO;
            if (existingOverlay != null)
            {
                Debug.Log($"[Migration] BlockingOverlay가 이미 존재합니다. 필드 연결만 수행합니다.");
                overlayGO = existingOverlay.gameObject;
            }
            else
            {
                // BlockingOverlay GameObject 생성
                overlayGO = new GameObject(BlockingOverlayName);
                overlayGO.transform.SetParent(uiManagerCanvas.transform, worldPositionStays: false);

                // RectTransform — 전체화면 커버
                var rect = overlayGO.GetComponent<RectTransform>();
                if (rect == null) rect = overlayGO.AddComponent<RectTransform>();
                rect.anchorMin        = Vector2.zero;
                rect.anchorMax        = Vector2.one;
                rect.offsetMin        = Vector2.zero;
                rect.offsetMax        = Vector2.zero;

                // Image — 반투명 검정, 터치 차단
                var img = overlayGO.AddComponent<Image>();
                img.color         = new Color(0f, 0f, 0f, 0.6f);
                img.raycastTarget = true;

                // Button — Popup 모드에서 터치 닫기용
                overlayGO.AddComponent<Button>();

                // CanvasGroup — 초기 숨김 상태 (alpha=0)
                var cg = overlayGO.AddComponent<CanvasGroup>();
                cg.alpha           = 0f;
                cg.blocksRaycasts  = false;
                cg.interactable    = false;

                // SafeAreaContainer 보다 앞(Hierarchy 위쪽=먼저 렌더)에 위치하도록 sibling 0으로 설정
                overlayGO.transform.SetSiblingIndex(0);

                Debug.Log("[Migration] BlockingOverlay GameObject 생성 완료.");
            }

            // UIManager 컴포넌트 탐색 및 필드 연결
            bool fieldOk = ConnectUIManagerFields(overlayGO);

            // 씬 저장
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Migration] Login.unity 저장 완료.");

            return fieldOk;
        }

        /// <summary>
        /// UIManager 컴포넌트를 씬에서 찾아 _blockingOverlay / _blockingOverlayButton 필드를 연결한다.
        /// </summary>
        private static bool ConnectUIManagerFields(GameObject overlayGO)
        {
            // UIManager 컴포넌트는 이름이 "UIManager"인 GameObject에 부착되어 있음
            var uiManagerGO = GameObject.Find(UIManagerObjectName);
            if (uiManagerGO == null)
            {
                Debug.LogError($"[Migration] '{UIManagerObjectName}' GameObject를 찾을 수 없습니다.");
                return false;
            }

            // 컴포넌트 타입은 MonoBehaviour이므로 이름으로 찾는다
            var uiManager = uiManagerGO.GetComponent<MonoBehaviour>();
            MonoBehaviour targetComponent = null;

            // UIManager 네임스페이스의 컴포넌트를 타입 이름으로 탐색
            foreach (var mb in uiManagerGO.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "UIManager")
                {
                    targetComponent = mb;
                    break;
                }
            }

            if (targetComponent == null)
            {
                Debug.LogError("[Migration] UIManager 컴포넌트를 찾을 수 없습니다.");
                return false;
            }

            var so = new SerializedObject(targetComponent);

            // _blockingOverlay (CanvasGroup)
            var cg = overlayGO.GetComponent<CanvasGroup>();
            var overlayProp = so.FindProperty("_blockingOverlay");
            if (overlayProp != null && cg != null)
            {
                overlayProp.objectReferenceValue = cg;
                Debug.Log("[Migration] UIManager._blockingOverlay 연결 완료.");
            }
            else
            {
                Debug.LogWarning("[Migration] _blockingOverlay 필드 또는 CanvasGroup을 찾을 수 없습니다.");
            }

            // _blockingOverlayButton (Button)
            var btn = overlayGO.GetComponent<Button>();
            var buttonProp = so.FindProperty("_blockingOverlayButton");
            if (buttonProp != null && btn != null)
            {
                buttonProp.objectReferenceValue = btn;
                Debug.Log("[Migration] UIManager._blockingOverlayButton 연결 완료.");
            }
            else
            {
                Debug.LogWarning("[Migration] _blockingOverlayButton 필드 또는 Button을 찾을 수 없습니다.");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetComponent);
            return true;
        }

        // ====================================================================
        // Game.unity 마이그레이션
        // ====================================================================

        private static bool MigrateGameScene()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Migration] Game.unity를 열 수 없습니다: {GameScenePath}");
                return false;
            }

            bool result = DisableRematchOverlayRaycast();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Migration] Game.unity 저장 완료.");

            return result;
        }

        /// <summary>
        /// RematchRequestPopup > Overlay Image의 RaycastTarget을 false로 설정한다.
        /// 코드에서 이미 참조가 제거되었으므로 입력 차단 역할이 없어야 한다.
        /// </summary>
        private static bool DisableRematchOverlayRaycast()
        {
            // RematchRequestPopup GameObject 탐색
            var rematchPopupGO = GameObject.Find(RematchPopupName);
            if (rematchPopupGO == null)
            {
                Debug.LogError($"[Migration] '{RematchPopupName}' GameObject를 Game 씬에서 찾을 수 없습니다.");
                return false;
            }

            // Overlay 자식 탐색
            var overlayTransform = rematchPopupGO.transform.Find(OverlayName);
            if (overlayTransform == null)
            {
                Debug.LogError($"[Migration] '{RematchPopupName}' 하위에 '{OverlayName}' GameObject가 없습니다.");
                return false;
            }

            var img = overlayTransform.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogWarning($"[Migration] Overlay에 Image 컴포넌트가 없습니다. 건너뜁니다.");
                return true;
            }

            if (!img.raycastTarget)
            {
                Debug.Log("[Migration] Overlay Image RaycastTarget이 이미 false입니다.");
                return true;
            }

            img.raycastTarget = false;
            EditorUtility.SetDirty(img);
            Debug.Log("[Migration] RematchRequestPopup > Overlay Image RaycastTarget 비활성화 완료.");
            return true;
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 현재 열린 씬에서 이름이 일치하는 컴포넌트 T를 가진 첫 번째 GameObject를 반환한다.
        /// </summary>
        private static T FindInScene<T>(string goName) where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.name == goName);
        }
    }
}
