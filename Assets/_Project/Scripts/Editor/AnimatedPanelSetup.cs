// ============================================================================
// AnimatedPanelSetup.cs  [Editor 전용 — 1회성 실행 스크립트]
//
// 역할:
//   GameEndPanel / ProductionPopup / BuildingPopup 오브젝트에
//   AnimatedPanel 컴포넌트를 자동으로 추가하고,
//   각 UI 스크립트의 _panel / _popup SerializedField를 재연결한다.
//
// 실행 방법:
//   Unity 메뉴 → Hexiege/Setup/Apply AnimatedPanel Setup
//
// 실행 후:
//   - Inspector에서 연결이 정상인지 확인
//   - 이 스크립트는 삭제해도 무방 (1회성)
//
// 주의:
//   - 반드시 Game 씬이 열린 상태에서 실행
//   - 씬 저장(Ctrl+S)을 꼭 해야 변경 사항이 유지됨
// ============================================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Presentation;

namespace Hexiege.Editor
{
    public static class AnimatedPanelSetup
    {
        [MenuItem("Hexiege/Setup/Apply AnimatedPanel Setup")]
        public static void Apply()
        {
            int successCount = 0;
            int failCount = 0;

            // ----------------------------------------------------------------
            // 1. GameEndPanel → AnimatedPanel (PopupFade) + GameEndUI._panel 연결
            // ----------------------------------------------------------------
            // GameObject를 이름으로 직접 탐색 (비활성 오브젝트 포함)
            var gameEndPanel = FindGameObjectByName("GameEndPanel");
            // GameEndUI 컴포넌트는 비활성 오브젝트에도 있을 수 있으므로 Include 옵션 사용
            var gameEndUI = Object.FindFirstObjectByType<GameEndUI>(FindObjectsInactive.Include);
            if (gameEndPanel == null || gameEndUI == null)
            {
                Debug.LogWarning($"[AnimatedPanelSetup] GameEndPanel 설정 실패 — GameEndPanel:{gameEndPanel != null}, GameEndUI:{gameEndUI != null}. Game 씬이 열려 있는지 확인하세요.");
                failCount++;
            }
            else
            {
                var panel = EnsureAnimatedPanel(gameEndPanel, AnimatedPanel.AnimationType.SlideFromTop);
                SetSerializedField(gameEndUI, "_panel", panel);
                Debug.Log("[AnimatedPanelSetup] ✅ GameEndPanel → AnimatedPanel(SlideFromTop) 적용 완료");
                successCount++;
            }

            // ----------------------------------------------------------------
            // 2. ProductionPopup → AnimatedPanel (SlideFromBottom) + ProductionPanelUI._popup 연결
            // ----------------------------------------------------------------
            var productionPopup = FindGameObjectByName("ProductionPopup");
            var productionPanelUI = Object.FindFirstObjectByType<ProductionPanelUI>(FindObjectsInactive.Include);
            if (productionPopup == null || productionPanelUI == null)
            {
                Debug.LogWarning($"[AnimatedPanelSetup] ProductionPopup 설정 실패 — ProductionPopup:{productionPopup != null}, ProductionPanelUI:{productionPanelUI != null}.");
                failCount++;
            }
            else
            {
                var panel = EnsureAnimatedPanel(productionPopup, AnimatedPanel.AnimationType.SlideFromBottom);
                SetSerializedField(productionPanelUI, "_popup", panel);
                Debug.Log("[AnimatedPanelSetup] ✅ ProductionPopup → AnimatedPanel(SlideFromBottom) 적용 완료");
                successCount++;
            }

            // ----------------------------------------------------------------
            // 3. BuildingPopup → AnimatedPanel (PopupFade) + BuildingPlacementUI._popup 연결
            // ----------------------------------------------------------------
            var buildingPopup = FindGameObjectByName("BuildingPopup");
            var buildingPlacementUI = Object.FindFirstObjectByType<BuildingPlacementUI>(FindObjectsInactive.Include);
            if (buildingPopup == null || buildingPlacementUI == null)
            {
                Debug.LogWarning($"[AnimatedPanelSetup] BuildingPopup 설정 실패 — BuildingPopup:{buildingPopup != null}, BuildingPlacementUI:{buildingPlacementUI != null}.");
                failCount++;
            }
            else
            {
                var panel = EnsureAnimatedPanel(buildingPopup, AnimatedPanel.AnimationType.SlideFromBottom);
                SetSerializedField(buildingPlacementUI, "_popup", panel);
                Debug.Log("[AnimatedPanelSetup] ✅ BuildingPopup → AnimatedPanel(SlideFromBottom) 적용 완료");
                successCount++;
            }

            // ----------------------------------------------------------------
            // 결과 요약
            // ----------------------------------------------------------------
            if (failCount == 0)
            {
                Debug.Log($"[AnimatedPanelSetup] 모든 설정 완료 ({successCount}/3). Ctrl+S로 씬을 저장하세요.");
                EditorUtility.DisplayDialog(
                    "AnimatedPanel Setup 완료",
                    $"총 {successCount}개 오브젝트 설정 완료.\nCtrl+S로 씬을 저장하세요.",
                    "확인"
                );
            }
            else
            {
                Debug.LogWarning($"[AnimatedPanelSetup] 완료: {successCount}개 성공, {failCount}개 실패. Console 로그를 확인하세요.");
                EditorUtility.DisplayDialog(
                    "AnimatedPanel Setup 부분 완료",
                    $"{successCount}개 성공, {failCount}개 실패.\nConsole 로그에서 실패 원인을 확인하세요.\n\n가능한 원인:\n- 잘못된 씬이 열려 있음\n- GameObject 이름이 다름",
                    "확인"
                );
            }
        }

        // ====================================================================
        // 헬퍼 메서드
        // ====================================================================

        /// <summary>
        /// 대상 GameObject에 AnimatedPanel이 없으면 추가하고 AnimationType을 설정.
        /// 이미 있으면 기존 컴포넌트 반환 (중복 추가 방지).
        /// </summary>
        private static AnimatedPanel EnsureAnimatedPanel(GameObject target, AnimatedPanel.AnimationType type)
        {
            var panel = target.GetComponent<AnimatedPanel>();
            if (panel == null)
                panel = target.AddComponent<AnimatedPanel>();

            // SerializedObject를 통해 AnimationType 설정
            var so = new SerializedObject(panel);
            var typeProp = so.FindProperty("_animationType");
            if (typeProp != null)
            {
                typeProp.enumValueIndex = (int)type;
                so.ApplyModifiedProperties();
            }

            // 변경 사항을 Undo에 등록 (Ctrl+Z로 되돌릴 수 있도록)
            Undo.RecordObject(target, "Add AnimatedPanel");
            EditorUtility.SetDirty(target);

            return panel;
        }

        /// <summary>
        /// MonoBehaviour의 SerializedField에 AnimatedPanel 참조를 연결.
        /// SerializedObject를 통해 Inspector와 동일하게 설정.
        /// </summary>
        private static void SetSerializedField(MonoBehaviour target, string fieldName, AnimatedPanel value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[AnimatedPanelSetup] {target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();

            Undo.RecordObject(target, $"Set {fieldName}");
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// 씬 전체에서 이름으로 GameObject 탐색 (비활성 오브젝트 포함).
        /// GameObject.Find()는 비활성 오브젝트를 찾지 못하므로 Resources API 우회 방식 사용.
        /// </summary>
        private static GameObject FindGameObjectByName(string name)
        {
            // 활성 오브젝트 먼저 탐색 (GameObject.Find는 활성만 탐색)
            var found = GameObject.Find(name);
            if (found != null) return found;

            // 비활성 오브젝트 탐색: 씬의 모든 루트 오브젝트를 순회
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var result = FindChildByName(root, name);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// 부모 GameObject의 하위 계층에서 이름으로 GameObject 재귀 탐색 (비활성 포함).
        /// </summary>
        private static GameObject FindChildByName(GameObject parent, string name)
        {
            if (parent.name == name) return parent;

            foreach (Transform child in parent.transform)
            {
                var result = FindChildByName(child.gameObject, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
