// ============================================================================
// RebuildBuildingPlacementUI.cs
// ----------------------------------------------------------------------------
// 목적 (1회성 셋업 스크립트):
//   Game.unity의 건물 배치 팝업(BuildingPopup) 하위 계층을 GameSystemRules의
//   반응형 규칙(Rule 2 — Layout Group 중첩 패턴)에 맞게 처음부터 재구성한다.
//
//   기존 문제:
//     - 패널 높이 부족 / 버튼이 패널 테두리 침범 / 골드 아이콘 Y축 정렬 불일치
//     - 수치 조정만으로는 해결이 어려워 계층 구조 자체를 다시 만든다.
//
//   목표 계층:
//     BuildingPopup  (전체화면 래퍼, anchor 0~1, 기존 유지)
//       └── BuildingPanel  (SafeArea 하단 40%, anchor 0,0 ~ 1,0.4)
//             ├── CancelButton  (우상단 고정)
//             └── GridContainer (VerticalLayoutGroup)
//                   ├── Row0 (HorizontalLayoutGroup) → Button[0,1,2]
//                   ├── Row1 (HorizontalLayoutGroup) → Button[3,4,5]
//                   └── Row2 (HorizontalLayoutGroup) → Button[6,7,8]
//
//   각 Button 내부:
//     Button (HorizontalLayoutGroup)
//       ├── IconImage   (건물 아이콘, flexibleWidth 6)
//       └── CostContainer (flexibleWidth 4, VerticalLayoutGroup)
//             ├── GoldIcon  (Image, preferred 24x24)
//             └── CostText  (TextMeshProUGUI)
//
//   반응형 원리 (GameSystemRules Rule 2):
//     GridLayoutGroup의 CellSize(고정 픽셀)를 쓰지 않고
//     VerticalLayoutGroup + HorizontalLayoutGroup 중첩 + Control Child Size +
//     Child Force Expand 조합으로 가용 공간을 자동 균등 분배한다.
//
// 사용 방법:
//   Unity 메뉴 [Hexiege] -> [Setup] -> [BuildingPlacementUI 재구성]
//
// 실행 후:
//   - 변경 사항은 Undo(Ctrl+Z)로 되돌릴 수 있다.
//   - BuildingPlacementUI 컴포넌트의 필드(_buildingButtons / _buildingButtonIcons /
//     _buildingCostTexts / _buildingGoldIcons)가 자동으로 재연결된다.
//   - 문제가 없으면 이 스크립트 파일을 삭제해도 된다.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hexiege.Editor
{
    /// <summary>
    /// BuildingPopup 하위 계층을 반응형 중첩 Layout Group 구조로 재구성하는 1회성 에디터 유틸.
    /// </summary>
    public static class RebuildBuildingPlacementUI
    {
        // 메뉴 경로
        private const string MenuPath = "Hexiege/Setup/BuildingPlacementUI 재구성";

        // 대상 씬 경로
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // 기본 폰트 경로 (GameSystemRules 규칙 6)
        private const string FontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";

        // 스프라이트 경로
        private const string GoldIconSpritePath = "Assets/_Project/Sprites/UI/Icons/ui_icon_gold.png";
        private const string DefaultBuildingSpritePath = "Assets/_Project/Sprites/Buildings/Human/bld_castle_red.png";

        // 버튼 총 개수 (3행 x 3열)
        private const int ButtonCount = 9;
        private const int ColumnsPerRow = 3;

        // 레이아웃 수치
        private const float GridSpacing = 8f;
        private const float ButtonInnerSpacing = 6f;
        private const float CostInnerSpacing = 4f;
        private const float GoldIconSize = 44f;    // 골드 아이콘 크기
        private const float CostTextHeight = 22f;
        private const float IconFlexWidth = 6f;    // 좌측 60%
        private const float CostFlexWidth = 4f;    // 우측 40%

        /// <summary>
        /// 메뉴 클릭 시 진입점.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void RunRebuild()
        {
            // 현재 씬에 미저장 변경이 있으면 저장 여부를 사용자에게 묻는다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[RebuildBuildingPlacementUI] 사용자가 저장을 취소하여 작업을 중단합니다.");
                return;
            }

            // 작업 완료 후 원래 씬으로 복구하기 위해 현재 경로를 기억해 둔다.
            string previousScenePath = EditorSceneManager.GetActiveScene().path;

            if (!System.IO.File.Exists(GameScenePath))
            {
                EditorUtility.DisplayDialog("오류", $"Game.unity를 찾을 수 없습니다.\n{GameScenePath}", "OK");
                return;
            }

            // Game.unity를 Single 모드로 연다.
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // ================================================================
            // 1) BuildingPlacementUI 컴포넌트를 기준으로 계층 탐색
            // ================================================================
            var placementUI = Object.FindFirstObjectByType<Hexiege.Presentation.BuildingPlacementUI>(
                FindObjectsInactive.Include);

            if (placementUI == null)
            {
                EditorUtility.DisplayDialog("오류",
                    "씬에서 BuildingPlacementUI 컴포넌트를 찾지 못했습니다.\n" +
                    "BuildingPopup 오브젝트에 스크립트가 부착되어 있는지 확인하세요.",
                    "OK");
                RestoreScene(previousScenePath);
                return;
            }

            // SerializedObject로 기존 필드를 읽어 어떤 오브젝트가 버튼/패널인지 신뢰성 있게 파악한다.
            var so = new SerializedObject(placementUI);
            SerializedProperty buttonsProp = so.FindProperty("_buildingButtons");
            SerializedProperty cancelProp = so.FindProperty("_cancelButton");

            // 기존 버튼 9개 수집 (재사용 대상). 순서는 기존 인덱스 0~8 유지.
            var existingButtons = new List<Button>();
            if (buttonsProp != null && buttonsProp.isArray)
            {
                for (int i = 0; i < buttonsProp.arraySize; i++)
                {
                    var obj = buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue as Button;
                    existingButtons.Add(obj); // null도 그대로 추가 (인덱스 정합성)
                }
            }

            // CancelButton 참조 (기존 X 이미지 버튼). 위치 재조정에 사용.
            var cancelButton = cancelProp != null ? cancelProp.objectReferenceValue as Button : null;

            // ================================================================
            // 2) 계층 구조 파악
            //    BuildingPlacementUI 부착 오브젝트 = BuildingPopup (전체화면 래퍼)
            //    BuildingPanel = BuildingPopup 하위에서 "BuildingPanel" 이름으로 탐색
            // ================================================================
            Transform buildingPopup = placementUI.transform;

            Transform buildingPanel = FindDeepChild(buildingPopup, "BuildingPanel");
            if (buildingPanel == null)
            {
                EditorUtility.DisplayDialog("오류",
                    "BuildingPopup 하위에서 'BuildingPanel' 오브젝트를 찾지 못했습니다.\n" +
                    "이름이 변경되었는지 확인하세요.",
                    "OK");
                RestoreScene(previousScenePath);
                return;
            }

            // Undo 전체 계층 기록 — 이후 모든 변경을 Ctrl+Z 한 번에 되돌릴 수 있게 한다.
            Undo.RegisterFullObjectHierarchyUndo(buildingPopup.gameObject, "Rebuild BuildingPlacementUI");

            // 폰트 에셋 로드 (없으면 기존 CostText 폰트 유지)
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // 골드 아이콘 스프라이트 탐색 (Resources 또는 프로젝트 전체)
            Sprite goldSprite = FindGoldSprite();

            var resultLog = new List<string>();

            // ================================================================
            // 3) BuildingPanel 앵커 — SafeArea 하단 40%
            // ================================================================
            var panelRt = buildingPanel.GetComponent<RectTransform>();
            if (panelRt == null) panelRt = buildingPanel.gameObject.AddComponent<RectTransform>();
            SetStretch(panelRt, new Vector2(0f, 0f), new Vector2(1f, 0.4f), new Vector2(0.5f, 0f));
            resultLog.Add("BuildingPanel 앵커: (0,0)~(1,0.4), pivot(0.5,0) — SafeArea 하단 40%");

            // ================================================================
            // 4) CancelButton 위치 — 우상단 고정
            // ================================================================
            if (cancelButton != null)
            {
                // CancelButton을 BuildingPanel 직속 자식으로 (이미 자식이면 유지)
                if (cancelButton.transform.parent != buildingPanel)
                    cancelButton.transform.SetParent(buildingPanel, false);

                var cancelRt = cancelButton.GetComponent<RectTransform>();
                // 사용자 조정값(anchoredPosition=(25,0), sizeDelta=(-65,-40))을 앵커에 흡수하여 순수 앵커 기반으로 변환
                // offsetMin=(90,40), offsetMax=(25,0) 기준으로 1080×768 부모에 대해 계산
                // left: (0.80×1080+90)/1080=0.883, right: (0.97×1080+25)/1080=0.993
                // bottom: (0.80×768+40)/768=0.852, top: (0.97×768+0)/768=0.970
                SetStretch(cancelRt,
                    new Vector2(0.883f, 0.852f), new Vector2(0.993f, 0.970f),
                    new Vector2(1f, 1f));
                resultLog.Add("CancelButton 앵커: (0.82,0.78)~(1,1), pivot(1,1) — 우상단 고정");
            }
            else
            {
                resultLog.Add("[경고] CancelButton 참조가 없어 위치 재조정을 건너뜁니다.");
            }

            // ================================================================
            // 5) GridContainer 생성 (또는 재사용)
            // ================================================================
            Transform existingGrid = buildingPanel.Find("GridContainer");
            GameObject gridGO;
            if (existingGrid != null)
            {
                gridGO = existingGrid.gameObject;
                resultLog.Add("[재사용] 기존 GridContainer를 사용합니다.");
            }
            else
            {
                gridGO = new GameObject("GridContainer", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(gridGO, "Create GridContainer");
                Undo.SetTransformParent(gridGO.transform, buildingPanel, "Parent GridContainer");
                resultLog.Add("GridContainer 신규 생성");
            }

            var gridRt = gridGO.GetComponent<RectTransform>();
            // 사용자 조정값(anchoredPosition.y=64)을 앵커에 흡수하여 순수 앵커 기반으로 변환
            // bottom: (0.04×768 + 64) / 768 = 0.123, top: (0.78×768 + 64) / 768 = 0.864
            SetStretch(gridRt,
                new Vector2(0.08f, 0.123f), new Vector2(0.92f, 0.864f),
                new Vector2(0.5f, 0.5f));

            var gridVLG = GetOrAdd<VerticalLayoutGroup>(gridGO);
            gridVLG.childAlignment = TextAnchor.MiddleCenter;
            gridVLG.childControlHeight = true;
            gridVLG.childForceExpandHeight = true;
            gridVLG.childControlWidth = true;
            gridVLG.childForceExpandWidth = true;
            gridVLG.spacing = GridSpacing;
            gridVLG.padding = new RectOffset(20, 20, 20, 20);

            // ================================================================
            // 6) Row0/Row1/Row2 생성 + 각 버튼 재구성
            // ================================================================
            var rebuiltButtons = new List<Button>(ButtonCount);
            var rebuiltIcons = new List<Image>(ButtonCount);
            var rebuiltGoldIcons = new List<Image>(ButtonCount);
            var rebuiltCostTexts = new List<TextMeshProUGUI>(ButtonCount);

            for (int row = 0; row < ButtonCount / ColumnsPerRow; row++)
            {
                // 행 컨테이너
                string rowName = $"Row{row}";
                Transform existingRow = gridGO.transform.Find(rowName);
                GameObject rowGO;
                if (existingRow != null)
                {
                    rowGO = existingRow.gameObject;
                }
                else
                {
                    rowGO = new GameObject(rowName, typeof(RectTransform));
                    Undo.RegisterCreatedObjectUndo(rowGO, "Create " + rowName);
                    Undo.SetTransformParent(rowGO.transform, gridGO.transform, "Parent " + rowName);
                }
                rowGO.transform.SetSiblingIndex(row);

                var rowHLG = GetOrAdd<HorizontalLayoutGroup>(rowGO);
                rowHLG.childAlignment = TextAnchor.MiddleCenter;
                rowHLG.childControlWidth = true;
                rowHLG.childForceExpandWidth = true;
                rowHLG.childControlHeight = true;
                rowHLG.childForceExpandHeight = true;
                rowHLG.spacing = GridSpacing;
                rowHLG.padding = new RectOffset(0, 0, 0, 0);

                // 해당 행의 버튼 3개 재구성
                for (int col = 0; col < ColumnsPerRow; col++)
                {
                    int index = row * ColumnsPerRow + col;
                    Button button = (index < existingButtons.Count) ? existingButtons[index] : null;

                    // 기존 버튼이 없으면 신규 생성
                    if (button == null)
                    {
                        var btnGO = new GameObject($"Button{index}",
                            typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
                        Undo.RegisterCreatedObjectUndo(btnGO, "Create Button " + index);
                        button = btnGO.GetComponent<Button>();
                    }

                    // 버튼을 행의 자식으로 이동
                    button.transform.SetParent(rowGO.transform, false);
                    button.transform.SetSiblingIndex(col);

                    // 버튼 RectTransform 초기화 (Layout이 크기 제어하므로 anchor는 중앙)
                    var btnRt = button.GetComponent<RectTransform>();
                    btnRt.localScale = Vector3.one;

                    // 버튼이 자체 Image를 배경으로 갖도록 보장 (없으면 추가)
                    GetOrAdd<Image>(button.gameObject);

                    // CanvasGroup 보장 (빈 슬롯 숨김 로직이 사용)
                    GetOrAdd<CanvasGroup>(button.gameObject);

                    // 기존 자식들을 모두 제거한다.
                    // 재사용된 버튼(구 Slot, BuildingButtons 등)에 남은 기존 자식(GoldIcon 등)이
                    // 새로 생성하는 IconImage/CostContainer와 충돌하여 레이아웃을 망가뜨린다.
                    for (int c = button.transform.childCount - 1; c >= 0; c--)
                    {
                        Undo.DestroyObjectImmediate(button.transform.GetChild(c).gameObject);
                    }

                    // 버튼 내부를 HorizontalLayoutGroup으로 재구성.
                    // childControlWidth = true 필수 — false이면 flexibleWidth 비율이 동작하지 않는다.
                    var btnHLG = GetOrAdd<HorizontalLayoutGroup>(button.gameObject);
                    btnHLG.childAlignment = TextAnchor.MiddleCenter;
                    btnHLG.childControlWidth = true;    // flexibleWidth 6:4 비율이 동작하려면 true 필수
                    btnHLG.childForceExpandWidth = true;
                    btnHLG.childControlHeight = true;
                    btnHLG.childForceExpandHeight = true;
                    btnHLG.spacing = ButtonInnerSpacing;
                    btnHLG.padding = new RectOffset(8, 8, 8, 8);

                    // ---- IconImage (건물 아이콘) ----
                    Image iconImage = SetupIconImage(button.transform, index);
                    rebuiltIcons.Add(iconImage);

                    // ---- CostContainer (골드 아이콘 + 비용 텍스트) ----
                    SetupCostContainer(button.transform, index, goldSprite, font,
                        out Image goldIcon, out TextMeshProUGUI costText);
                    rebuiltGoldIcons.Add(goldIcon);
                    rebuiltCostTexts.Add(costText);

                    rebuiltButtons.Add(button);
                }
            }

            // ================================================================
            // 7) Inspector 필드 재연결 (SerializedObject)
            // ================================================================
            AssignList(so, "_buildingButtons", rebuiltButtons.ConvertAll(b => (Object)b));
            AssignList(so, "_buildingButtonIcons", rebuiltIcons.ConvertAll(b => (Object)b));
            AssignList(so, "_buildingGoldIcons", rebuiltGoldIcons.ConvertAll(b => (Object)b));
            AssignList(so, "_buildingCostTexts", rebuiltCostTexts.ConvertAll(b => (Object)b));
            so.ApplyModifiedPropertiesWithoutUndo();

            resultLog.Add($"버튼 {rebuiltButtons.Count}개 재구성 + Inspector 필드 4종 재연결 완료");
            resultLog.Add(goldSprite != null
                ? "GoldIcon 스프라이트 연결됨"
                : "[주의] 골드 아이콘 스프라이트를 찾지 못해 null로 두었습니다. Inspector에서 수동 연결 필요.");

            // ================================================================
            // 8) 씬 저장
            // ================================================================
            EditorUtility.SetDirty(placementUI);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            resultLog.Add($"\nGame.unity 저장 {(saved ? "성공" : "실패")}");

            RestoreScene(previousScenePath);

            EditorUtility.DisplayDialog(
                "BuildingPlacementUI 재구성",
                "작업 완료.\n\n" + string.Join("\n", resultLog) + "\n\n" +
                "Play 모드에서 자기 팀 빈 타일을 탭해 팝업 레이아웃을 확인하세요.\n" +
                "문제가 없으면 이 스크립트 파일을 삭제하셔도 됩니다.",
                "OK");
        }

        // ====================================================================
        // 내부 구성 헬퍼
        // ====================================================================

        /// <summary>
        /// 버튼 내부 좌측 IconImage(건물 아이콘)를 생성/재사용하고 LayoutElement 비율을 설정한다.
        /// </summary>
        private static Image SetupIconImage(Transform parent, int index)
        {
            Transform existing = parent.Find("IconImage");
            GameObject iconGO;
            if (existing != null)
            {
                iconGO = existing.gameObject;
            }
            else
            {
                iconGO = new GameObject("IconImage", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(iconGO, "Create IconImage " + index);
                Undo.SetTransformParent(iconGO.transform, parent, "Parent IconImage " + index);
            }
            iconGO.transform.SetSiblingIndex(0);

            var img = GetOrAdd<Image>(iconGO);
            img.preserveAspect = true;
            img.raycastTarget = false;

            // 임시 기본 스프라이트 설정 — 에디터에서 레이아웃 확인용 (런타임에 Show()에서 교체됨)
            if (img.sprite == null)
            {
                var defaultSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultBuildingSpritePath);
                if (defaultSprite != null) img.sprite = defaultSprite;
            }

            var le = GetOrAdd<LayoutElement>(iconGO);
            le.flexibleWidth = IconFlexWidth;   // 좌측 60% 비율
            le.flexibleHeight = 1f;
            le.ignoreLayout = false;

            return img;
        }

        /// <summary>
        /// 버튼 내부 우측 CostContainer(골드 아이콘 + 비용 텍스트)를 생성/재사용한다.
        /// 기존 CostText가 버튼 어딘가에 있으면 재사용하여 폰트/세팅을 보존한다.
        /// </summary>
        private static void SetupCostContainer(Transform parent, int index, Sprite goldSprite,
            TMP_FontAsset font, out Image goldIcon, out TextMeshProUGUI costText)
        {
            Transform existing = parent.Find("CostContainer");
            GameObject containerGO;
            if (existing != null)
            {
                containerGO = existing.gameObject;
            }
            else
            {
                containerGO = new GameObject("CostContainer", typeof(RectTransform), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(containerGO, "Create CostContainer " + index);
                Undo.SetTransformParent(containerGO.transform, parent, "Parent CostContainer " + index);
            }
            containerGO.transform.SetSiblingIndex(1);

            var le = GetOrAdd<LayoutElement>(containerGO);
            le.flexibleWidth = CostFlexWidth;   // 우측 40% 비율
            le.flexibleHeight = 1f;
            le.ignoreLayout = false;

            var vlg = GetOrAdd<VerticalLayoutGroup>(containerGO);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;   // GoldIcon 높이를 LayoutElement preferred로 제어하려면 true 필수
            vlg.childForceExpandHeight = false;
            vlg.spacing = CostInnerSpacing;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            // ---- GoldIcon ----
            Transform goldT = containerGO.transform.Find("GoldIcon");
            GameObject goldGO;
            if (goldT != null)
            {
                goldGO = goldT.gameObject;
            }
            else
            {
                goldGO = new GameObject("GoldIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(goldGO, "Create GoldIcon " + index);
                Undo.SetTransformParent(goldGO.transform, containerGO.transform, "Parent GoldIcon " + index);
            }
            goldGO.transform.SetSiblingIndex(0);

            // 기존에 AspectRatioFitter가 붙어있으면 제거한다.
            // AspectRatioFitter는 LayoutElement preferredHeight를 무시하고 크기를 재조정하여
            // GoldIcon이 비정상적으로 커지는 원인이 된다.
            if (goldGO.TryGetComponent(out AspectRatioFitter arf))
                Undo.DestroyObjectImmediate(arf);

            goldIcon = GetOrAdd<Image>(goldGO);
            goldIcon.type = Image.Type.Simple;
            goldIcon.preserveAspect = true;
            goldIcon.raycastTarget = false;
            if (goldSprite != null) goldIcon.sprite = goldSprite;

            var goldLe = GetOrAdd<LayoutElement>(goldGO);
            goldLe.minWidth = GoldIconSize;
            goldLe.minHeight = GoldIconSize;
            goldLe.preferredHeight = GoldIconSize;
            goldLe.preferredWidth = GoldIconSize;
            goldLe.flexibleWidth = 0f;
            goldLe.flexibleHeight = 0f;  // 높이 팽창 방지

            // ---- CostText (기존 재사용 우선) ----
            costText = FindExistingCostText(parent);
            GameObject costGO;
            if (costText != null)
            {
                costGO = costText.gameObject;
                // 컨테이너 자식으로 이동
                if (costGO.transform.parent != containerGO.transform)
                    costGO.transform.SetParent(containerGO.transform, false);
            }
            else
            {
                costGO = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(costGO, "Create CostText " + index);
                Undo.SetTransformParent(costGO.transform, containerGO.transform, "Parent CostText " + index);
                costText = costGO.GetComponent<TextMeshProUGUI>();
            }
            costGO.transform.SetSiblingIndex(1);

            // 폰트가 비어있을 때만 기본 폰트 설정 (기존 Maplestory 폰트 보존)
            if (font != null && costText.font == null)
                costText.font = font;

            costText.alignment = TextAlignmentOptions.Center;
            costText.fontSize = 16f;
            costText.raycastTarget = false;
            // 임시 기본 텍스트 — 에디터에서 레이아웃 확인용 (런타임에 Show()에서 교체됨)
            if (string.IsNullOrEmpty(costText.text) || costText.text == "New Text")
                costText.text = "999";

            var costLe = GetOrAdd<LayoutElement>(costGO);
            costLe.preferredWidth = 300f;
            costLe.preferredHeight = CostTextHeight;
        }

        /// <summary>
        /// 버튼 하위(임의 깊이)에서 기존 CostText로 쓸 수 있는 TextMeshProUGUI를 찾는다.
        /// CostContainer 안의 GoldIcon 등 비텍스트는 자동 제외된다.
        /// </summary>
        private static TextMeshProUGUI FindExistingCostText(Transform buttonRoot)
        {
            // 이름이 "CostText"인 자식을 우선 탐색
            var byName = FindDeepChild(buttonRoot, "CostText");
            if (byName != null && byName.TryGetComponent(out TextMeshProUGUI byNameTmp))
                return byNameTmp;

            // 없으면 버튼 하위 첫 번째 TMP 텍스트를 사용
            return buttonRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // ====================================================================
        // 범용 헬퍼
        // ====================================================================

        /// <summary>RectTransform 앵커/피벗/anchoredPosition/sizeDelta를 명시적으로 설정한다.</summary>
        private static void SetStretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition = default, Vector2 sizeDelta = default)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.localScale = Vector3.one;
        }

        /// <summary>컴포넌트가 있으면 반환, 없으면 Undo 등록과 함께 추가한다.</summary>
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent(out T comp)) return comp;
            return Undo.AddComponent<T>(go);
        }

        /// <summary>SerializedObject의 리스트 프로퍼티를 주어진 오브젝트들로 교체한다.</summary>
        private static void AssignList(SerializedObject so, string propName, List<Object> values)
        {
            var prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogWarning($"[RebuildBuildingPlacementUI] 프로퍼티 '{propName}'를 찾지 못했습니다.");
                return;
            }
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>이름이 일치하는 자손(깊이 무관)을 BFS로 탐색한다.</summary>
        private static Transform FindDeepChild(Transform root, string name)
        {
            var queue = new Queue<Transform>();
            for (int i = 0; i < root.childCount; i++) queue.Enqueue(root.GetChild(i));
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                if (t.name == name) return t;
                for (int i = 0; i < t.childCount; i++) queue.Enqueue(t.GetChild(i));
            }
            return null;
        }

        /// <summary>
        /// 골드 아이콘 스프라이트를 탐색한다.
        /// Resources/프로젝트 전체에서 "gold" 또는 "Gold"가 이름에 포함된 Sprite를 찾는다.
        /// </summary>
        private static Sprite FindGoldSprite()
        {
            // ui_icon_gold 경로로 직접 로드
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoldIconSpritePath);
            if (sprite != null)
            {
                Debug.Log($"[RebuildBuildingPlacementUI] 골드 스프라이트 사용: {GoldIconSpritePath}");
                return sprite;
            }

            Debug.LogWarning("[RebuildBuildingPlacementUI] ui_icon_gold.png를 찾지 못했습니다. GoldIcon은 null로 둡니다.");
            return null;
        }

        // ====================================================================
        // 헬퍼 — 씬 복구
        // ====================================================================

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
