// ============================================================================
// SetupBuildingActionPanelUI.cs
// BuildingActionPanel GameObject를 씬에 자동 생성하고, 필드 배선 및
// GameBootstrapper 연결까지 한 번에 처리하는 에디터 스크립트.
//
// 메뉴: Hexiege/UI/Setup Building Action Panel UI
//
// 동작 방식:
//   ProductionPopup GO를 복제한 뒤 생산 전용 자식을 제거하고
//   BuildingActionPanelUI 컴포넌트로 교체한다.
//   이렇게 하면 패널 배경/버튼 스타일 등 시각적 요소가 ProductionPanelUI와 동일하게 유지된다.
//
// 이 스크립트가 자동으로 처리하는 일:
//   [A] 씬에서 BuildingActionPanelUI가 이미 존재하는지 확인
//       → 존재하면: 필드 재배선 + GameBootstrapper 연결만 수행
//   [B] 씬에서 ProductionPanelUI 탐색 (복제 원본)
//   [C] ProductionPopup GO 복제 → BuildingActionPanel로 변환
//       1) ProductionPanelUI 컴포넌트 제거 → BuildingActionPanelUI 추가
//       2) 생산 전용 GO 제거: QueueSlots, ProgressBar, InfoBar
//       3) 미구현 버튼 invisible 처리:
//          Button1/2/3 (UnitButtons 행), RallyPointButton, UpgradeButton
//          → CanvasGroup.alpha=0, blocksRaycasts=false
//       4) 제거된 GO 높이만큼 패널 세로 크기 축소
//   [D] BuildingActionPanelUI의 SerializeField 6개를 SerializedObject로 배선
//       - _popup          : AnimatedPanel (ProductionPanel GO)
//       - _sharedBackground : Canvas 공용 Background GO의 SharedBackgroundButton
//       - _headerText     : HeaderText GO의 TextMeshProUGUI
//       - _cancelButton   : CancelButton GO의 Button
//       - _demolishButton : DestroyButton GO의 Button
//       - _demolishRefundText : DestroyButton 내부 TextMeshProUGUI (GoldText)
//   [E] GameBootstrapper의 _buildingActionPanelUI 필드에 자동 연결
//   [F] 씬 Dirty 마킹 + 콘솔 로그
//
// 사용 절차:
//   1. Game.unity 씬을 열어둔다.
//   2. 메뉴 Hexiege/UI/Setup Building Action Panel UI 클릭.
//   3. 콘솔 로그로 결과 확인 후 Ctrl+S로 씬 저장.
//
// 결과 계층 구조:
//   BuildingActionPanel (ProductionPopup에서 복제, 비주얼 동일)
//   └── ProductionPanel (AnimatedPanel → _popup)
//       ├── HeaderText (_headerText)
//       ├── CancelButton (_cancelButton)
//       └── UnitsButtons
//           ├── UnitButtons
//           │   ├── Button1  (CanvasGroup.alpha=0 — 1행 1열 자리 채우기)
//           │   ├── Button2  (CanvasGroup.alpha=0 — 1행 2열 자리 채우기)
//           │   └── Button3  (CanvasGroup.alpha=0 — 1행 3열 자리 채우기)
//           └── Buttons
//               ├── RallyPointButton  (CanvasGroup.alpha=0 — 2행 1열 자리 채우기)
//               ├── UpgradeButton     (CanvasGroup.alpha=0 — 2행 2열 자리 채우기)
//               └── DestroyButton     (_demolishButton, alpha=1 — 2행 3열 = 철거)
//                   └── GoldText      (_demolishRefundText)
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hexiege.Presentation;
using Hexiege.Bootstrap;

public static class SetupBuildingActionPanelUI
{
    // 제거할 생산 전용 GO 이름 목록
    private static readonly string[] _goToRemove = { "QueueSlots", "ProgressBar", "InfoBar" };

    // invisible 처리할 미구현 버튼 GO 이름 목록
    // (레이아웃 자리만 차지하고 시각적으로 보이지 않음)
    private static readonly string[] _invisibleSlots =
    {
        "Button1", "Button2", "Button3",    // UnitButtons 행 (1행 1~3열)
        "RallyPointButton", "UpgradeButton" // Buttons 행 (2행 1~2열)
    };

    [MenuItem("Hexiege/UI/Setup Building Action Panel UI")]
    private static void Setup()
    {
        // ── [A] 기존 BuildingActionPanelUI 존재 여부 확인 ───────────────────
        BuildingActionPanelUI actionPanel = Object.FindFirstObjectByType<BuildingActionPanelUI>();
        bool alreadyExisted = (actionPanel != null);

        if (alreadyExisted)
        {
            Debug.Log("[Setup] BuildingActionPanelUI가 이미 존재합니다. " +
                      "필드 재배선 + GameBootstrapper 연결만 수행합니다.");
        }
        else
        {
            // ── [B] ProductionPanelUI 탐색 ──────────────────────────────────
            ProductionPanelUI productionUI = Object.FindFirstObjectByType<ProductionPanelUI>();
            if (productionUI == null)
            {
                Debug.LogError("[Setup] ProductionPanelUI를 씬에서 찾을 수 없습니다. " +
                               "Game.unity 씬을 열고 다시 실행해주세요.");
                return;
            }

            // ── [C] 복제 + 변환 ────────────────────────────────────────────
            actionPanel = CloneAndConvert(productionUI);
            if (actionPanel == null)
            {
                Debug.LogError("[Setup] BuildingActionPanel 생성에 실패했습니다.");
                return;
            }
        }

        // ── [D] 6개 필드 배선 ──────────────────────────────────────────────
        int wired = WireFields(actionPanel);

        // ── [E] GameBootstrapper 연결 ──────────────────────────────────────
        bool bootstrapperWired = WireGameBootstrapperReference(actionPanel);

        // ── [F] 마무리 ─────────────────────────────────────────────────────
        EditorUtility.SetDirty(actionPanel);
        EditorSceneManager.MarkSceneDirty(actionPanel.gameObject.scene);
        EditorGUIUtility.PingObject(actionPanel.gameObject);

        string mode = alreadyExisted ? "재배선" : "신규 생성";
        string bsMsg = bootstrapperWired ? "연결 완료" : "수동 연결 필요";
        Debug.Log($"[Setup] BuildingActionPanelUI {mode} 완료. " +
                  $"연결된 필드: {wired}/6, GameBootstrapper: {bsMsg}. " +
                  $"씬을 저장하세요 (Ctrl+S).");
    }

    // ========================================================================
    // [C] ProductionPopup GO를 복제해 BuildingActionPanel로 변환
    // ========================================================================

    /// <summary>
    /// ProductionPanelUI GO를 복제한 뒤 생산 전용 자식을 제거하고
    /// BuildingActionPanelUI 컴포넌트로 교체한다.
    /// 패널 배경/버튼 스타일 등 시각 요소는 원본과 동일하게 유지된다.
    /// </summary>
    private static BuildingActionPanelUI CloneAndConvert(ProductionPanelUI productionUI)
    {
        // ── [C-1] GO 복제 ───────────────────────────────────────────────────
        // Object.Instantiate로 씬 오브젝트를 그대로 복제한다.
        // 복제된 GO는 자식 계층 구조를 포함한다.
        GameObject cloneGO = Object.Instantiate(productionUI.gameObject);
        if (cloneGO == null)
        {
            Debug.LogError("[Setup] Instantiate 실패.");
            return null;
        }

        // ── [C-2] ProductionPanelUI 제거 → BuildingActionPanelUI 추가 ───────
        // 컴포넌트만 교체하고 GO 계층 구조(배경 이미지, 버튼 스타일 등)는 그대로 유지한다.
        ProductionPanelUI clonedProduction = cloneGO.GetComponent<ProductionPanelUI>();
        if (clonedProduction != null)
            Object.DestroyImmediate(clonedProduction);

        BuildingActionPanelUI actionPanel = cloneGO.AddComponent<BuildingActionPanelUI>();

        // ── [C-3] 이름 변경 ─────────────────────────────────────────────────
        cloneGO.name = "BuildingActionPanel";

        // ── [C-4] 부모/위치 설정 ────────────────────────────────────────────
        // ProductionPanelUI와 같은 부모 하위에 배치해 Canvas 계층이 동일하게 유지된다.
        Transform parent = productionUI.transform.parent;
        cloneGO.transform.SetParent(parent, false);
        cloneGO.transform.SetSiblingIndex(productionUI.transform.GetSiblingIndex() + 1);

        // RectTransform 앵커/피벗/스케일 복사 (sizeDelta는 높이 축소 후 별도 적용)
        RectTransform srcRect = productionUI.GetComponent<RectTransform>();
        RectTransform dstRect = cloneGO.GetComponent<RectTransform>();
        if (srcRect != null && dstRect != null)
        {
            dstRect.anchorMin        = srcRect.anchorMin;
            dstRect.anchorMax        = srcRect.anchorMax;
            dstRect.pivot            = srcRect.pivot;
            dstRect.anchoredPosition = srcRect.anchoredPosition;
            dstRect.sizeDelta        = srcRect.sizeDelta;
            dstRect.localScale       = srcRect.localScale;
        }

        // ── [C-5] 생산 전용 GO 제거 (QueueSlots / ProgressBar / InfoBar) ────
        // 제거되는 GO의 높이를 합산해 나중에 패널 높이를 줄이는 데 사용한다.
        float removedHeight = 0f;
        foreach (string goName in _goToRemove)
        {
            Transform found = FindChildByName(cloneGO.transform, goName);
            if (found == null)
            {
                Debug.LogWarning($"[Setup] '{goName}' GO를 찾지 못했습니다 — 건너뜁니다.");
                continue;
            }

            // sizeDelta.y로 해당 GO의 세로 크기를 읽는다.
            // (RectTransform이 고정 높이 방식일 때 정확하다)
            RectTransform rt = found.GetComponent<RectTransform>();
            if (rt != null)
                removedHeight += Mathf.Abs(rt.sizeDelta.y);

            Object.DestroyImmediate(found.gameObject);
            Debug.Log($"[Setup] '{goName}' GO 제거 완료.");
        }

        // 패널(루트 RectTransform) 세로 크기를 제거한 GO 높이만큼 줄인다.
        if (dstRect != null && removedHeight > 0f)
        {
            dstRect.sizeDelta = new Vector2(dstRect.sizeDelta.x,
                                            dstRect.sizeDelta.y - removedHeight);
            Debug.Log($"[Setup] 패널 높이 {removedHeight}px 축소 완료.");
        }

        // ── [C-6] 미구현 버튼 invisible 처리 ───────────────────────────────
        // 버튼은 GO를 제거하지 않고 보이지 않게만 만든다.
        // GridLayout이 자리를 차지해 철거 버튼이 2행 3열 위치에 유지된다.
        foreach (string slotName in _invisibleSlots)
        {
            Transform found = FindChildByName(cloneGO.transform, slotName);
            if (found == null)
            {
                Debug.LogWarning($"[Setup] '{slotName}' GO를 찾지 못했습니다 — 건너뜁니다.");
                continue;
            }
            MakeInvisible(found.gameObject);
            Debug.Log($"[Setup] '{slotName}' invisible 처리 완료.");
        }

        return actionPanel;
    }

    // ========================================================================
    // [D] BuildingActionPanelUI의 SerializeField 6개를 배선
    // ========================================================================

    /// <summary>
    /// SerializedObject API로 BuildingPanelBase의 공유 필드 6개를 채운다.
    /// 재실행(이미 존재하는 경우)에서도 동일한 로직으로 필드를 재연결한다.
    /// </summary>
    private static int WireFields(BuildingActionPanelUI actionPanel)
    {
        SerializedObject so = new SerializedObject(actionPanel);
        so.Update();

        int wired = 0;
        GameObject root = actionPanel.gameObject;

        // _popup — ProductionPanel GO에 부착된 AnimatedPanel 컴포넌트
        AnimatedPanel popup = root.GetComponentInChildren<AnimatedPanel>(true);
        wired += SetRef(so, "_popup", popup);

        // _sharedBackground — Canvas 직속 Background GO의 SharedBackgroundButton.
        //   ProductionPanelUI를 포함한 모든 팝업이 이 하나의 GO를 공유한다.
        //   팝업이 열릴 때 Register(Close), 닫힐 때 Unregister()를 호출해 바깥 탭 닫기 처리.
        // FindObjectsInactive.Include 옵션을 주어야 비활성 GO도 탐색한다.
        // Background GO는 평소에 비활성 상태이므로 이 옵션 없이는 찾지 못한다.
        SharedBackgroundButton sharedBg =
            Object.FindFirstObjectByType<SharedBackgroundButton>(FindObjectsInactive.Include);
        wired += SetRef(so, "_sharedBackground", sharedBg);

        // _headerText — HeaderText GO의 TextMeshProUGUI (건물 이름 표시)
        Transform headerTextTf = FindChildByName(root.transform, "HeaderText");
        wired += SetRef(so, "_headerText",
            headerTextTf != null ? headerTextTf.GetComponent<TextMeshProUGUI>() : null);

        // _cancelButton — CancelButton GO의 Button (X 닫기 버튼)
        Transform cancelTf = FindChildByName(root.transform, "CancelButton");
        wired += SetRef(so, "_cancelButton",
            cancelTf != null ? cancelTf.GetComponent<Button>() : null);

        // _demolishButton — DestroyButton GO의 Button (철거 버튼, 2행 3열)
        Transform destroyTf = FindChildByName(root.transform, "DestroyButton");
        wired += SetRef(so, "_demolishButton",
            destroyTf != null ? destroyTf.GetComponent<Button>() : null);

        // _demolishRefundText — DestroyButton 내부의 TextMeshProUGUI (환불 금액 표시)
        //   DestroyButton 자식 중 첫 번째 TextMeshProUGUI를 사용한다 (= GoldText GO).
        TextMeshProUGUI refundText =
            destroyTf != null
            ? destroyTf.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        wired += SetRef(so, "_demolishRefundText", refundText);

        so.ApplyModifiedProperties();
        return wired;
    }

    // ========================================================================
    // [E] GameBootstrapper의 _buildingActionPanelUI 필드 자동 연결
    // ========================================================================

    /// <summary>
    /// 씬의 GameBootstrapper에서 _buildingActionPanelUI 필드를 찾아 actionPanel을 연결한다.
    /// GameBootstrapper가 없거나 필드가 없으면 false 반환.
    /// </summary>
    private static bool WireGameBootstrapperReference(BuildingActionPanelUI actionPanel)
    {
        GameBootstrapper bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>();
        if (bootstrapper == null)
        {
            Debug.LogWarning("[Setup] 씬에 GameBootstrapper가 없습니다. 수동 연결이 필요합니다.");
            return false;
        }

        SerializedObject bso = new SerializedObject(bootstrapper);
        bso.Update();

        SerializedProperty prop = bso.FindProperty("_buildingActionPanelUI");
        if (prop == null)
        {
            Debug.LogWarning("[Setup] GameBootstrapper에 '_buildingActionPanelUI' 필드가 없습니다. " +
                             "필드명을 확인하세요.");
            return false;
        }

        prop.objectReferenceValue = actionPanel;
        bso.ApplyModifiedProperties();
        EditorUtility.SetDirty(bootstrapper);
        Debug.Log("[Setup] GameBootstrapper._buildingActionPanelUI 연결 완료.");
        return true;
    }

    // ========================================================================
    // 유틸리티
    // ========================================================================

    /// <summary>
    /// GO에 CanvasGroup 컴포넌트를 추가(또는 기존 것을 사용)해
    /// alpha=0, blocksRaycasts=false, interactable=false로 설정한다.
    /// GO 자체는 씬에 남아있어 레이아웃 공간을 유지한다.
    /// </summary>
    private static void MakeInvisible(GameObject go)
    {
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.blocksRaycasts = false;
        cg.interactable   = false;
    }

    /// <summary>
    /// SerializedObject의 특정 필드에 컴포넌트 참조를 연결하는 헬퍼.
    /// 성공 시 1, 실패 시 0을 반환한다.
    /// </summary>
    private static int SetRef(SerializedObject so, string fieldName, Component comp)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[Setup] '{fieldName}' 프로퍼티를 BuildingActionPanelUI에서 찾을 수 없습니다. " +
                           "BuildingPanelBase의 필드명이 바뀌었는지 확인하세요.");
            return 0;
        }
        if (comp == null)
        {
            Debug.LogWarning($"[Setup] '{fieldName}'에 연결할 컴포넌트가 null입니다. " +
                             "Inspector에서 수동 연결이 필요합니다.");
            return 0;
        }
        prop.objectReferenceValue = comp;
        Debug.Log($"[Setup] '{fieldName}' 연결 완료: '{comp.gameObject.name}' ({comp.GetType().Name})");
        return 1;
    }

    /// <summary>
    /// 이름으로 자식 Transform을 재귀적으로 탐색한다 (DFS).
    /// 첫 번째 매칭 결과를 반환하며, 없으면 null.
    /// </summary>
    private static Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildByName(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
