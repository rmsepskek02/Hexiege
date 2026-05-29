// ============================================================================
// RebuildProductionPopup.cs — 1회성 에디터 스크립트
// ProductionPopup UI를 GameSystemRules 반응형 기준으로 재구성합니다.
// 실행: Unity 상단 메뉴 Hexiege > Setup > ProductionPopup 재구성
// 실행 후 이 파일은 삭제해도 무방합니다.
// ============================================================================
//
// [이 스크립트가 하는 일 — 유니티 초급자용 요약]
// 배럭(생산 건물)을 클릭하면 뜨는 ProductionPanel 안의 UI 요소들이 지금은
// "고정 픽셀 좌표"(예: x=400, y=-126)로 위치가 박혀 있습니다. 화면 크기가
// 바뀌면 위치가 어긋나거나 잘립니다.
//
// 이 스크립트는 그 요소들을 "비율(앵커) + Layout Group 자동 분배" 방식으로
// 바꿔서, 어떤 화면 크기에서도 알아서 자리를 잡도록 만듭니다.
//
// 핵심 원리 두 가지:
//  1) 앵커(anchorMin/anchorMax)를 0~1 비율로 잡고, anchoredPosition·sizeDelta를
//     0으로 초기화하면 → 부모 영역의 "비율 구간"에 딱 맞게 늘어납니다.
//  2) Layout Group(가로/세로 정렬 컴포넌트)의 childControl/childForceExpand를
//     켜면 → 자식들의 크기를 부모가 알아서 균등 분배합니다.
//
// 모든 변경은 Undo(Ctrl+Z)로 되돌릴 수 있도록 Undo에 등록합니다.
// 씬 저장은 하지 않습니다. 실행 후 직접 Ctrl+S로 저장하세요.
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// ProductionPanel 하위 UI 요소들을 반응형(앵커 + Layout Group) 방식으로 재구성하는
    /// 1회성 에디터 도구. 메뉴에서 실행한다.
    /// </summary>
    public static class RebuildProductionPopup
    {
        // 메뉴 경로 — Unity 상단 바에 'Hexiege > Setup > ProductionPopup 재구성'으로 노출됨.
        private const string MenuPath = "Hexiege/Setup/ProductionPopup 재구성";

        /// <summary>
        /// 메뉴 실행 진입점. 씬에서 ProductionPanel을 찾아 하위 요소들을 차례로 수정한다.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Rebuild()
        {
            // ── 1) ProductionPanelUI 컴포넌트를 통해 ProductionPanel을 안전하게 찾는다. ──
            //    GameObject.Find("ProductionPanel")처럼 이름으로 찾으면 다른 패널의
            //    동명 오브젝트(예: CancelButton, HeaderText)와 충돌할 수 있으므로,
            //    유일하게 존재하는 ProductionPanelUI 컴포넌트를 기준점으로 사용한다.
            var panelUI = Object.FindFirstObjectByType<ProductionPanelUI>(FindObjectsInactive.Include);
            if (panelUI == null)
            {
                Debug.LogWarning("[RebuildProductionPopup] 씬에서 ProductionPanelUI를 찾을 수 없습니다. " +
                                 "Game.unity 씬이 열려 있는지 확인하세요.");
                return;
            }

            // ProductionPanelUI 컴포넌트는 ProductionPopup(래퍼)에 붙어 있다.
            // 실제 수정 대상은 그 직계 자식인 ProductionPanel이므로 한 단계 내려간다.
            Transform productionPanel = panelUI.transform.Find("ProductionPanel");
            if (productionPanel == null)
            {
                Debug.LogWarning("[RebuildProductionPopup] ProductionPopup 하위에서 'ProductionPanel'을 찾을 수 없습니다. " +
                                 "씬 계층 구조가 변경됐는지 확인하세요.");
                return;
            }
            Debug.Log($"[RebuildProductionPopup] 대상 패널: '{productionPanel.name}' — 재구성을 시작합니다.");

            // 변경 작업 카운터 — 마지막에 몇 건이 처리됐는지 로그로 남긴다.
            int changeCount = 0;

            // ── 2) HeaderText: Y축 단일점 앵커 → 순수 앵커 스팬으로 전환 ──
            //    목표: anchorMin=(0.05,0.85), anchorMax=(0.85,0.97), pos/delta=0
            //    CancelButton(우측 상단)과 겹치지 않도록 X 최대값을 0.85로 제한한다.
            var headerText = FindChild(productionPanel, "HeaderText");
            if (headerText != null)
            {
                SetAnchors(headerText as RectTransform,
                    new Vector2(0.05f, 0.85f), new Vector2(0.85f, 0.97f));
                changeCount++;
            }
            else WarnMissing("HeaderText");

            // ── 3) CancelButton: 다른 패널과 닫기 버튼 위치 통일 ──
            //    목표: anchorMin=(0.883,0.852), anchorMax=(0.993,0.97), pos/delta=0
            //    (BuildingPopup / BuildingActionPanel과 동일한 위치)
            var cancelButton = FindChild(productionPanel, "CancelButton");
            if (cancelButton != null)
            {
                SetAnchors(cancelButton as RectTransform,
                    new Vector2(0.883f, 0.852f), new Vector2(0.993f, 0.97f));
                changeCount++;
            }
            else WarnMissing("CancelButton");

            // ── 4) UnitsButtons (VLG): 자식 행 크기를 부모가 완전 제어하도록 설정 ──
            //    childControl/childForceExpand를 모두 켜서 UnitButtons / Buttons 두 행이
            //    부모 영역을 가로/세로로 꽉 채우며 균등 분배되게 한다.
            var unitsButtons = FindChild(productionPanel, "UnitsButtons");
            if (unitsButtons != null)
            {
                var vlg = unitsButtons.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    Undo.RecordObject(vlg, "Set UnitsButtons VLG");
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = true;
                    EditorUtility.SetDirty(vlg);
                    changeCount++;
                }
                else Debug.LogWarning("[RebuildProductionPopup] UnitsButtons에 VerticalLayoutGroup이 없습니다.");

                // ── 4-1) UnitButtons (유닛 버튼 행, HLG): 고정 픽셀 sizeDelta 제거 + 균등 분배 ──
                //    LayoutElement.flexibleHeight=3 → 아래 액션행(=2)보다 높이를 크게 차지한다.
                var unitButtonsRow = FindChild(unitsButtons, "UnitButtons");
                if (unitButtonsRow != null)
                {
                    ResetRect(unitButtonsRow as RectTransform);
                    SetupHorizontalRow(unitButtonsRow, flexibleHeight: 3f);
                    changeCount++;
                }
                else WarnMissing("UnitsButtons/UnitButtons");

                // ── 4-2) Buttons (액션 버튼 행: 랠리/업그레이드/철거, HLG) ──
                //    LayoutElement.flexibleHeight=2 → 유닛 버튼 행보다 약간 낮게.
                var actionButtonsRow = FindChild(unitsButtons, "Buttons");
                if (actionButtonsRow != null)
                {
                    ResetRect(actionButtonsRow as RectTransform);
                    SetupHorizontalRow(actionButtonsRow, flexibleHeight: 2f);
                    changeCount++;
                }
                else WarnMissing("UnitsButtons/Buttons");
            }
            else WarnMissing("UnitsButtons");

            // ── 5) QueueSlots (HLG): 슬롯 고정 좌표 제거 + 가로 균등 분배 ──
            //    각 Slot의 anchoredPosition/sizeDelta를 0으로 초기화하고,
            //    HLG가 가로로 균등하게 나눠 배치하도록 childControlWidth/ForceExpand를 켠다.
            //    높이는 부모 앵커가 이미 제어하므로 childControlHeight는 켜지 않는다.
            var queueSlots = FindChild(productionPanel, "QueueSlots");
            if (queueSlots != null)
            {
                var hlg = queueSlots.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    Undo.RecordObject(hlg, "Set QueueSlots HLG");
                    hlg.childControlWidth = true;
                    hlg.childForceExpandWidth = true;
                    hlg.spacing = 10f; // 비율 분배 후 슬롯 사이 간격은 소폭만 유지
                    EditorUtility.SetDirty(hlg);
                    changeCount++;
                }
                else Debug.LogWarning("[RebuildProductionPopup] QueueSlots에 HorizontalLayoutGroup이 없습니다.");

                // 각 Slot(자식 전체)의 RectTransform을 0으로 초기화 → HLG가 균등 분배.
                for (int i = 0; i < queueSlots.childCount; i++)
                {
                    var slot = queueSlots.GetChild(i) as RectTransform;
                    if (slot != null)
                    {
                        ResetRect(slot);
                        changeCount++;
                    }
                }
            }
            else WarnMissing("QueueSlots");

            // ── 6) InfoBar (HLG): 아이콘/텍스트 고정 좌표 제거 + 비율 분배 ──
            //    아이콘(GoldIcon/PopIcon)은 LayoutElement.preferredWidth=60으로 너비 고정,
            //    텍스트(GoldText/PopText)는 flexibleWidth=1로 남은 공간을 균등하게 채운다.
            //    자식 순서는 [GoldIcon, GoldText, PopIcon, PopText] 이다.
            var infoBar = FindChild(productionPanel, "InfoBar");
            if (infoBar != null)
            {
                var hlg = infoBar.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    Undo.RecordObject(hlg, "Set InfoBar HLG");
                    hlg.childControlWidth = true;
                    hlg.childForceExpandWidth = true;
                    EditorUtility.SetDirty(hlg);
                    changeCount++;
                }
                else Debug.LogWarning("[RebuildProductionPopup] InfoBar에 HorizontalLayoutGroup이 없습니다.");

                // 아이콘 2개: 너비 고정(preferredWidth=60), 남는 공간 차지 안 함(flexibleWidth=0)
                changeCount += SetupInfoBarElement(infoBar, "GoldIcon", isIcon: true);
                changeCount += SetupInfoBarElement(infoBar, "PopIcon", isIcon: true);

                // 텍스트 2개: 남은 공간을 균등 분배(flexibleWidth=1)
                changeCount += SetupInfoBarElement(infoBar, "GoldText", isIcon: false);
                changeCount += SetupInfoBarElement(infoBar, "PopText", isIcon: false);
            }
            else WarnMissing("InfoBar");

            // ── 7) 씬 Dirty 마킹 — 변경사항이 있음을 에디터에 알려 저장 표시(*)를 띄운다. ──
            //    (스크립트가 직접 씬을 저장하지는 않음. 사용자가 Ctrl+S로 저장해야 함.)
            EditorUtility.SetDirty(productionPanel.gameObject);
            EditorSceneManager.MarkSceneDirty(productionPanel.gameObject.scene);

            Debug.Log($"[RebuildProductionPopup] 완료 — 총 {changeCount}건 처리. " +
                      "Ctrl+S로 씬을 저장하세요. 잘못됐다면 Ctrl+Z로 되돌릴 수 있습니다.");
        }

        // ====================================================================
        // 헬퍼 메서드
        // ====================================================================

        /// <summary>
        /// 부모 Transform의 직계 자식 중 지정한 이름의 Transform을 반환한다.
        /// 못 찾으면 null을 반환한다 (호출부에서 null 체크 + 경고 로그 처리).
        /// 이름 기반이지만 "부모의 직계 자식"만 보므로 다른 패널과 충돌하지 않는다.
        /// </summary>
        private static Transform FindChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            return parent.Find(childName);
        }

        /// <summary>
        /// RectTransform의 앵커를 지정 비율로 설정하고, anchoredPosition·sizeDelta를 0으로 초기화한다.
        /// 앵커만 바꾸고 pos/delta를 0으로 만들지 않으면 기존 픽셀 오프셋이 그대로 남아
        /// 위치가 어긋나므로, 두 값을 반드시 명시적으로 0으로 만든다.
        /// </summary>
        private static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rt == null) return;
            Undo.RecordObject(rt, "Set Anchors");
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = Vector2.zero; // 픽셀 오프셋 제거 (필수)
            rt.sizeDelta = Vector2.zero;          // 추가 크기 제거 (필수)
            EditorUtility.SetDirty(rt);
        }

        /// <summary>
        /// RectTransform의 anchoredPosition·sizeDelta만 0으로 초기화한다.
        /// (앵커 비율은 건드리지 않음 — 부모 Layout Group이 크기를 제어할 자식에 사용.)
        /// </summary>
        private static void ResetRect(RectTransform rt)
        {
            if (rt == null) return;
            Undo.RecordObject(rt, "Reset Rect");
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            EditorUtility.SetDirty(rt);
        }

        /// <summary>
        /// 가로 버튼 행(HLG)을 균등 분배 모드로 설정하고, 부모 VLG가 높이를 비율로
        /// 나눌 수 있도록 LayoutElement.flexibleHeight 값을 지정한다.
        ///  - HLG: childControlWidth/Height + childForceExpandWidth/Height = true
        ///  - LayoutElement.flexibleHeight: 행 간 높이 비율 (UnitButtons=3, Buttons=2 등)
        /// </summary>
        private static void SetupHorizontalRow(Transform row, float flexibleHeight)
        {
            // 1) 이 행 자체의 HorizontalLayoutGroup을 균등 분배 모드로.
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                Undo.RecordObject(hlg, "Set Row HLG");
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                EditorUtility.SetDirty(hlg);
            }
            else
            {
                Debug.LogWarning($"[RebuildProductionPopup] '{row.name}'에 HorizontalLayoutGroup이 없습니다.");
            }

            // 2) 부모 VLG가 이 행의 높이를 비율로 나눌 수 있도록 LayoutElement를 설정.
            //    flexibleHeight 비율이 큰 행이 더 큰 높이를 차지한다.
            var le = row.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(row.gameObject);
            Undo.RecordObject(le, "Set Row LayoutElement");
            le.flexibleHeight = flexibleHeight;
            EditorUtility.SetDirty(le);
        }

        /// <summary>
        /// InfoBar 자식(아이콘/텍스트)을 반응형으로 설정한다.
        ///  - RectTransform: anchoredPosition·sizeDelta = 0
        ///  - 아이콘이면 LayoutElement.preferredWidth=60, flexibleWidth=0 (너비 고정)
        ///  - 텍스트면 LayoutElement.flexibleWidth=1 (남은 공간 균등 분배)
        /// 처리 성공 시 1, 자식을 못 찾으면 0을 반환한다.
        /// </summary>
        private static int SetupInfoBarElement(Transform infoBar, string childName, bool isIcon)
        {
            var child = FindChild(infoBar, childName);
            if (child == null)
            {
                WarnMissing($"InfoBar/{childName}");
                return 0;
            }

            // RectTransform 오프셋 초기화 (HLG가 위치/크기를 제어).
            ResetRect(child as RectTransform);

            // LayoutElement 설정 — 없으면 추가.
            var le = child.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(child.gameObject);
            Undo.RecordObject(le, "Set InfoBar LayoutElement");

            if (isIcon)
            {
                // 아이콘: 너비를 60으로 고정하고, 남는 공간은 차지하지 않음.
                le.preferredWidth = 60f;
                le.flexibleWidth = 0f;
            }
            else
            {
                // 텍스트: 남은 가로 공간을 균등하게 채움.
                le.preferredWidth = -1f; // 강제 선호 너비 없음
                le.flexibleWidth = 1f;
            }
            EditorUtility.SetDirty(le);
            return 1;
        }

        /// <summary>
        /// 대상 오브젝트를 찾지 못했을 때 공통 경고 로그를 출력한다.
        /// 작업은 중단하지 않고 나머지 요소 처리를 계속 진행한다 (부분 적용 허용).
        /// </summary>
        private static void WarnMissing(string path)
        {
            Debug.LogWarning($"[RebuildProductionPopup] '{path}' 오브젝트를 찾을 수 없어 건너뜁니다. " +
                             "씬 계층 구조가 변경됐는지 확인하세요.");
        }
    }
}
#endif
