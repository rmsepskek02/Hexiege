// ============================================================================
// FixRule5Violations.cs
// 공통 UI 규칙 5(가시성은 SetActive 대신 CanvasGroup으로 제어) 위반을 일괄 수정하는
// 1회성 에디터 스크립트.
//
// 배경:
//   기존에는 일부 팝업/오버레이가 SetActive(false/true)로 가시성을 제어했다.
//   하지만 규칙 5에 따라 오브젝트는 항상 active 상태를 유지하고,
//   CanvasGroup(alpha/blocksRaycasts/interactable)으로만 보임/숨김을 처리해야 한다.
//   (오브젝트가 active=false이면 LayoutGroup에서 제외되거나 초기화 코드가 실행되지
//    않는 등의 부작용이 생길 수 있음)
//
// 역할 (Game.unity 씬 대상):
//   ① AnimatedPanel이 부착된 모든 오브젝트를 active로 전환한다.
//   ② 특정 오버레이/패널 오브젝트에 CanvasGroup을 추가하고 active로 전환한 뒤,
//      초기값(alpha=0, blocksRaycasts=false, interactable=false)으로 숨김 상태를 만든다.
//   ③ 씬을 저장한다.
//
// 사용법:
//   Unity 에디터 메뉴 → Hexiege/Setup/규칙5 위반 수정 클릭.
//
// 이 스크립트는 실행 후 삭제해도 무방한 1회성 유틸리티.
// (이전의 ActivateAnimatedPanels.cs를 통합·대체함)
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 공통 UI 규칙 5 위반(SetActive 기반 가시성 제어)을 CanvasGroup 방식으로 일괄 전환하는
    /// 1회성 에디터 유틸리티.
    /// </summary>
    public static class FixRule5Violations
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        /// <summary>
        /// Game.unity 씬을 열어 규칙 5 위반 오브젝트들을 수정한 뒤 저장한다.
        /// AnimatedPanel 오브젝트 활성화 + 지정 오버레이/패널에 CanvasGroup 추가 및 초기화를 수행한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/규칙5 위반 수정")]
        public static void Fix()
        {
            // 작업 중이던 씬이 있으면 저장 여부를 먼저 묻는다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Game.unity 씬을 Single 모드로 연다.
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            int changed = 0;

            // ── ① AnimatedPanel 부착 오브젝트를 모두 active로 전환 ───────────────
            // 비활성 오브젝트도 포함해서 탐색한다.
            AnimatedPanel[] panels = Object.FindObjectsByType<AnimatedPanel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (AnimatedPanel panel in panels)
            {
                GameObject go = panel.gameObject;
                if (go.activeSelf) continue; // 이미 active면 건너뜀

                Undo.RecordObject(go, "Activate AnimatedPanel");
                go.SetActive(true);
                EditorUtility.SetDirty(go);
                changed++;
                Debug.Log($"[FixRule5Violations] AnimatedPanel '{go.name}' 을(를) active로 전환했습니다.");
            }

            // ── ② 지정 오버레이/패널에 CanvasGroup 추가 + active + 초기값 설정 ─────
            // 부모 이름까지 검증하여 정확히 매칭되는 오브젝트만 처리한다.

            // RematchRequestPopup 의 자식: Overlay, RequestPanel, DeclinedPanel
            changed += SetupChildByName("RematchRequestPopup", "Overlay");
            changed += SetupChildByName("RematchRequestPopup", "RequestPanel");
            changed += SetupChildByName("RematchRequestPopup", "DeclinedPanel");

            // ConfirmPopup 의 자식: BlockingOverlay
            changed += SetupChildByName("ConfirmPopup", "BlockingOverlay");

            // ProductionPopup 의 자식 중 이름이 BorderOverlay / LockIndicator 인 것 전부
            changed += SetupAllDescendantsByName("ProductionPopup", "BorderOverlay");
            changed += SetupAllDescendantsByName("ProductionPopup", "LockIndicator");

            // ── ②-추가: ProductionPanelUI._unitLockIndicators Inspector 재연결 ─────
            // _unitLockIndicators 필드 타입이 List<GameObject> → List<CanvasGroup>으로 변경되어
            // 기존 Inspector 참조가 끊긴다. SerializedObject로 CanvasGroup 참조를 재할당한다.
            changed += ReassignLockIndicators();

            // ── ③ 씬 저장 ────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FixRule5Violations] 완료: {changed}개 오브젝트 수정");
        }

        /// <summary>
        /// 지정한 부모 이름을 가진 오브젝트의 자식 중, 정확히 일치하는 이름의 자식 하나를 처리한다.
        /// (자식의 직접 부모 이름이 parentName과 같은 경우만 매칭하여 오탐을 방지)
        /// </summary>
        /// <param name="parentName">부모 오브젝트 이름.</param>
        /// <param name="childName">처리할 자식 오브젝트 이름.</param>
        /// <returns>처리한 오브젝트 수.</returns>
        private static int SetupChildByName(string parentName, string childName)
        {
            int count = 0;
            // 씬 내 모든 Transform(비활성 포함)을 탐색한다.
            Transform[] all = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (Transform t in all)
            {
                if (t.name != childName) continue;
                if (t.parent == null || t.parent.name != parentName) continue;

                ApplyCanvasGroup(t.gameObject);
                count++;
            }
            return count;
        }

        /// <summary>
        /// 지정한 조상(ancestor) 이름을 가진 오브젝트 아래에 있는, 해당 이름의 모든 자손 오브젝트를 처리한다.
        /// (직접 자식이 아니어도 조상 계층에 ancestorName이 있으면 매칭)
        /// </summary>
        /// <param name="ancestorName">조상 오브젝트 이름.</param>
        /// <param name="targetName">처리할 자손 오브젝트 이름.</param>
        /// <returns>처리한 오브젝트 수.</returns>
        private static int SetupAllDescendantsByName(string ancestorName, string targetName)
        {
            int count = 0;
            Transform[] all = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (Transform t in all)
            {
                if (t.name != targetName) continue;
                if (!HasAncestor(t, ancestorName)) continue;

                ApplyCanvasGroup(t.gameObject);
                count++;
            }
            return count;
        }

        /// <summary>
        /// 대상 Transform의 상위 계층에 지정한 이름의 조상이 있는지 검사한다.
        /// </summary>
        /// <param name="t">대상 Transform.</param>
        /// <param name="ancestorName">찾을 조상 이름.</param>
        /// <returns>조상이 존재하면 true.</returns>
        private static bool HasAncestor(Transform t, string ancestorName)
        {
            Transform p = t.parent;
            while (p != null)
            {
                if (p.name == ancestorName) return true;
                p = p.parent;
            }
            return false;
        }

        /// <summary>
        /// ProductionPanelUI 컴포넌트의 _unitLockIndicators 필드(List&lt;CanvasGroup&gt;)에
        /// LockIndicator 오브젝트들의 CanvasGroup 참조를 자동으로 재할당한다.
        ///
        /// 배경:
        ///   _unitLockIndicators 필드 타입이 List&lt;GameObject&gt; → List&lt;CanvasGroup&gt;으로 변경되어
        ///   Unity 직렬화 참조가 끊긴다. SerializedObject/SerializedProperty를 사용하면
        ///   에디터 스크립트에서 Inspector 필드를 직접 재할당할 수 있다.
        ///
        /// 탐색 순서:
        ///   ProductionPopup 아래의 모든 LockIndicator 오브젝트를 계층 순서대로 찾아
        ///   각각의 CanvasGroup을 _unitLockIndicators 배열에 순서대로 채운다.
        /// </summary>
        /// <returns>재할당한 항목 수. 실패 시 0.</returns>
        private static int ReassignLockIndicators()
        {
            // ProductionPanelUI 컴포넌트를 씬에서 찾는다 (비활성 오브젝트 포함).
            ProductionPanelUI productionPanelUI = Object.FindFirstObjectByType<ProductionPanelUI>(
                FindObjectsInactive.Include);

            if (productionPanelUI == null)
            {
                Debug.LogWarning("[FixRule5Violations] ProductionPanelUI 컴포넌트를 찾지 못했습니다. " +
                                 "_unitLockIndicators 재할당을 건너뜁니다.");
                return 0;
            }

            // ProductionPopup 루트를 찾아 그 아래의 LockIndicator 오브젝트들을 계층 순서로 수집한다.
            Transform productionPopupRoot = FindRootByName("ProductionPopup");
            if (productionPopupRoot == null)
            {
                Debug.LogWarning("[FixRule5Violations] 'ProductionPopup' 루트 오브젝트를 찾지 못했습니다.");
                return 0;
            }

            // GetComponentsInChildren은 계층 순서(DFS)를 보장하므로 슬롯 순서와 일치한다.
            // 비활성 오브젝트도 포함한다.
            Transform[] allChildren = productionPopupRoot.GetComponentsInChildren<Transform>(
                includeInactive: true);

            var lockIndicatorCgs = new List<CanvasGroup>();
            foreach (Transform t in allChildren)
            {
                if (t.name != "LockIndicator") continue;
                // CanvasGroup은 SetupAllDescendantsByName 단계에서 이미 추가되어 있어야 한다.
                var cg = t.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    // 혹시 아직 없으면 여기서도 추가한다 (방어 코드).
                    cg = t.gameObject.AddComponent<CanvasGroup>();
                    EditorUtility.SetDirty(t.gameObject);
                }
                lockIndicatorCgs.Add(cg);
            }

            if (lockIndicatorCgs.Count == 0)
            {
                Debug.LogWarning("[FixRule5Violations] LockIndicator 오브젝트를 찾지 못했습니다.");
                return 0;
            }

            // SerializedObject를 통해 _unitLockIndicators 배열 필드를 직접 재할당한다.
            // SerializedProperty는 필드 이름(언더스코어 포함)으로 접근한다.
            SerializedObject so = new SerializedObject(productionPanelUI);
            SerializedProperty prop = so.FindProperty("_unitLockIndicators");

            if (prop == null)
            {
                Debug.LogWarning("[FixRule5Violations] '_unitLockIndicators' 프로퍼티를 찾지 못했습니다. " +
                                 "필드 이름이 변경되었는지 확인하세요.");
                return 0;
            }

            // 배열 크기를 맞추고 각 원소에 CanvasGroup 참조를 할당한다.
            prop.arraySize = lockIndicatorCgs.Count;
            for (int i = 0; i < lockIndicatorCgs.Count; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = lockIndicatorCgs[i];
            }

            // 변경 사항을 실제로 반영한다.
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(productionPanelUI);

            Debug.Log($"[FixRule5Violations] _unitLockIndicators 재할당 완료: {lockIndicatorCgs.Count}개 CanvasGroup 연결.");
            return lockIndicatorCgs.Count;
        }

        /// <summary>
        /// 씬 루트 오브젝트 중 지정한 이름을 가진 Transform을 반환한다.
        /// 없으면 null 반환.
        /// </summary>
        /// <param name="name">찾을 루트 오브젝트 이름.</param>
        private static Transform FindRootByName(string name)
        {
            // FindObjectsByType은 비활성 루트도 포함한다.
            Transform[] all = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
            {
                if (t.name == name && t.parent == null) return t;
                // 루트가 아닌 경우에도 이름이 일치하면 반환 (씬 계층에 따라 조정 가능)
                if (t.name == name) return t;
            }
            return null;
        }

        /// <summary>
        /// 오브젝트를 active로 전환하고 CanvasGroup을 추가/확보한 뒤,
        /// 초기 숨김 상태(alpha=0, blocksRaycasts=false, interactable=false)로 설정한다.
        /// </summary>
        /// <param name="go">처리할 GameObject.</param>
        private static void ApplyCanvasGroup(GameObject go)
        {
            Undo.RecordObject(go, "Fix Rule5 Violation");

            // 규칙 5: 오브젝트는 항상 active 상태를 유지한다.
            if (!go.activeSelf) go.SetActive(true);

            CanvasGroup cg = EnsureCanvasGroup(go);
            cg.alpha = 0f;              // 완전 투명 = 숨김
            cg.blocksRaycasts = false;  // 터치/클릭 차단 해제
            cg.interactable = false;    // 상호작용 비활성

            EditorUtility.SetDirty(go);
            Debug.Log($"[FixRule5Violations] '{go.name}' 에 CanvasGroup 적용 및 초기화 완료.");
        }

        /// <summary>
        /// 지정한 GameObject에서 CanvasGroup을 가져오거나 없으면 추가한다.
        /// </summary>
        /// <param name="go">대상 GameObject.</param>
        /// <returns>확보된 CanvasGroup.</returns>
        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }
    }
}
