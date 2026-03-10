using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ProductionPopup / BuildingPopup 반응형 UI 일괄 설정 에디터 스크립트.
/// 메뉴에서 한 번 실행 후 삭제해도 무방.
/// </summary>
public static class ResponsivePopupUISetup
{
    [MenuItem("Hexiege/UI/Apply Responsive Popup Layout")]
    public static void Apply()
    {
        // ── ProductionPanel ──
        var productionPanel = FindGameObjectByName("ProductionPanel");
        if (productionPanel == null)
        {
            Debug.LogError("[ResponsivePopupUISetup] ProductionPanel not found!");
            return;
        }

        var verticalButtons = productionPanel.transform.Find("VerticalButtons");
        if (verticalButtons != null)
        {
            SetupVerticalLayoutGroup(verticalButtons.gameObject);
            SetupButtonRows(verticalButtons);
        }
        else
        {
            Debug.LogError("[ResponsivePopupUISetup] VerticalButtons not found!");
        }

        // ── BuildingPanel ──
        var buildingPanel = FindGameObjectByName("BuildingPanel");
        if (buildingPanel == null)
        {
            Debug.LogError("[ResponsivePopupUISetup] BuildingPanel not found!");
            return;
        }

        // BuildingButtons 는 BuildingPanel 하위에서 VerticalLayoutGroup을 가진 자식
        Transform buildingButtons = null;
        foreach (Transform child in buildingPanel.transform)
        {
            if (child.GetComponent<VerticalLayoutGroup>() != null)
            {
                buildingButtons = child;
                break;
            }
        }

        if (buildingButtons != null)
        {
            SetupVerticalLayoutGroup(buildingButtons.gameObject);
            SetupButtonRows(buildingButtons);
        }
        else
        {
            Debug.LogError("[ResponsivePopupUISetup] BuildingButtons (VerticalLayoutGroup) not found!");
        }

        Debug.Log("[ResponsivePopupUISetup] Responsive layout applied successfully! Save the scene (Ctrl+S).");
    }

    /// <summary>
    /// 비활성 오브젝트도 찾을 수 있도록 Resources.FindObjectsOfTypeAll 사용
    /// </summary>
    private static GameObject FindGameObjectByName(string name)
    {
        // 먼저 활성 오브젝트에서 검색
        var go = GameObject.Find(name);
        if (go != null) return go;

        // 비활성 오브젝트 포함 검색
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == name && t.hideFlags == HideFlags.None)
                return t.gameObject;
        }
        return null;
    }

    /// <summary>
    /// VerticalLayoutGroup 패딩 및 간격 설정
    /// </summary>
    private static void SetupVerticalLayoutGroup(GameObject go)
    {
        var vlg = go.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) return;

        Undo.RecordObject(vlg, "Setup VerticalLayoutGroup");

        vlg.padding = new RectOffset(20, 20, 0, 0);
        vlg.spacing = 8;
        // ChildControlWidth 이미 1인 경우가 있으므로 유지
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        EditorUtility.SetDirty(vlg);
    }

    /// <summary>
    /// VerticalButtons/BuildingButtons 하위의 각 버튼 행(HorizontalLayoutGroup) 설정
    /// </summary>
    private static void SetupButtonRows(Transform parent)
    {
        foreach (Transform row in parent)
        {
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) continue;

            Undo.RecordObject(hlg, "Setup HorizontalLayoutGroup");

            hlg.padding = new RectOffset(8, 8, 0, 0);
            hlg.spacing = 8;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = true;
            hlg.childControlHeight = false;

            EditorUtility.SetDirty(hlg);

            // 행을 stretch 앵커로 변경 (부모 너비를 채우도록)
            var rowRect = row.GetComponent<RectTransform>();
            if (rowRect != null)
            {
                Undo.RecordObject(rowRect, "Setup Row RectTransform");
                rowRect.anchorMin = new Vector2(0, 0.5f);
                rowRect.anchorMax = new Vector2(1, 0.5f);
                rowRect.anchoredPosition = new Vector2(0, rowRect.anchoredPosition.y);
                rowRect.sizeDelta = new Vector2(0, rowRect.sizeDelta.y);
                EditorUtility.SetDirty(rowRect);
            }

            // 각 슬롯 처리
            foreach (Transform slot in row)
            {
                SetupSlot(slot);
            }
        }
    }

    /// <summary>
    /// 개별 슬롯: AspectRatioFitter 추가 + 내부 요소 앵커 기반 배치
    /// </summary>
    private static void SetupSlot(Transform slot)
    {
        // AspectRatioFitter 추가 (1.5:1 = width:height)
        var arf = slot.GetComponent<AspectRatioFitter>();
        if (arf == null)
        {
            arf = Undo.AddComponent<AspectRatioFitter>(slot.gameObject);
        }
        else
        {
            Undo.RecordObject(arf, "Setup AspectRatioFitter");
        }
        arf.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        arf.aspectRatio = 1.5f;
        EditorUtility.SetDirty(arf);

        // 슬롯 자체 RectTransform - SizeDelta height는 AspectRatioFitter가 제어
        var slotRect = slot.GetComponent<RectTransform>();
        if (slotRect != null)
        {
            Undo.RecordObject(slotRect, "Setup Slot RectTransform");
            // 앵커/사이즈는 HorizontalLayoutGroup의 ChildControlWidth가 관리하므로
            // sizeDelta.x는 레이아웃이 설정, y는 AspectRatioFitter가 설정
            EditorUtility.SetDirty(slotRect);
        }

        // 내부 자식 요소들 앵커 기반 배치로 전환
        foreach (Transform child in slot)
        {
            var childRect = child.GetComponent<RectTransform>();
            if (childRect == null) continue;

            string childName = child.name.ToLower();

            Undo.RecordObject(childRect, "Setup Child RectTransform");

            if (childName.Contains("image") || childName.Contains("unit") || childName.Contains("building"))
            {
                // 유닛/건물 이미지: 좌측 상단 영역
                SetAnchors(childRect, 0.05f, 0.15f, 0.82f, 0.90f);
            }
            else if (childName.Contains("goldicon") || childName.Contains("coinicon") || childName.Contains("icon"))
            {
                // 골드 아이콘: 중앙 하단
                SetAnchors(childRect, 0.30f, 0.05f, 0.60f, 0.35f);
            }
            else if (childName.Contains("goldtext") || childName.Contains("cost") || childName.Contains("text"))
            {
                // 골드 텍스트: 우측 하단
                SetAnchors(childRect, 0.50f, 0.05f, 1.0f, 0.38f);

                // TMP Auto Size 활성화
                var tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    Undo.RecordObject(tmp, "Setup TMP Auto Size");
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 8;
                    tmp.fontSizeMax = 24;
                    EditorUtility.SetDirty(tmp);
                }
            }
            else if (childName.Contains("auto"))
            {
                // AutoIndicator: 슬롯 전체에 오버레이
                SetAnchors(childRect, 0.0f, 0.0f, 1.0f, 1.0f);
            }
            else
            {
                // 기타: 슬롯 전체에 stretch
                SetAnchors(childRect, 0.05f, 0.05f, 0.95f, 0.95f);
            }

            EditorUtility.SetDirty(childRect);
        }
    }

    /// <summary>
    /// RectTransform을 앵커 기반 레이아웃으로 전환 (SizeDelta=0, AnchoredPosition=0)
    /// </summary>
    private static void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }
}
