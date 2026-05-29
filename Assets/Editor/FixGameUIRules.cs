// ============================================================================
// FixGameUIRules.cs
// ----------------------------------------------------------------------------
// 목적 (1회성 셋업 스크립트):
//   Game.unity 게임 화면에서 기기 해상도/비율에 따라 UI가 잘리거나 겹치는 문제를 수정한다.
//   핵심 원칙: "포인트 앵커(anchorMin==anchorMax) + 고정 픽셀 sizeDelta" 조합을
//   모두 "비율 앵커"로 전환한다. 비율 앵커를 쓰면 화면이 커지거나 작아져도
//   UI 요소가 항상 같은 비율로 배치된다.
//
//   [작업 근거] GameSystemRules.md Rule 2 (포인트앵커 금지), Rule 5 (비율 기반 레이아웃)
//
//   [수정 항목 요약]
//   Step 1 — BuildingPanel / ProductionPanel(두 곳) 높이 비율 확인 (idempotent)
//   Step 2 — CancelButton 세 개(BuildingPlacementUI / BuildingActionPanelUI / ProductionPanelUI) 앵커 통일
//   Step 3 — BuildingButtons 컨테이너 비율화 + BuildingButtons1~3 sizeDelta 정리
//   Step 4 — ProductionPanelUI: UnitsButtons / QueueSlots / ProgressBar / InfoBar 비율화
//   Step 5 — BuildingActionPanelUI: UnitsButtons(1357007226) / CancelButton(2030508638) 처리
//   Step 6 — GameEndUI (GameEndPanel 자식들) — 이미 비율 앵커이면 스킵
//   Step 7 — InGameSettingsPanel > Panel 비율화 + 내부 SoundButton / ForfeitButton 비율화
//   Step 8 — GameHUD 비율화 + AspectRatioFitter (없을 때만 추가)
//   Step 9 — RematchRequestPopup > DeclinedPanel > ButtonArea 비율화
//
// 사용 방법:
//   Unity 메뉴 [Hexiege] -> [Fix] -> [게임 UI Rules 수정 (Game.unity)]
//
// 실행 후:
//   - 변경 사항은 Undo(Ctrl+Z)로 되돌릴 수 있다.
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
    /// Game.unity 게임 화면 UI를 GameSystemRules.md 기준에 맞게 비율 앵커로 일괄 수정하는 1회성 에디터 유틸.
    /// </summary>
    public static class FixGameUIRules
    {
        // ------------------------------------------------------------------
        // 메뉴 경로 및 씬 경로
        // ------------------------------------------------------------------
        private const string MenuPath       = "Hexiege/Fix/게임 UI Rules 수정 (Game.unity)";
        private const string GameScenePath  = "Assets/_Project/Scenes/Game.unity";

        // 금 버튼 스프라이트 경로 (Step 6 GameEndUI 버튼에 적용)
        private const string GoldBtnSpritePath =
            "Assets/_Project/Sprites/UI/Buttons/ui_btn_gold_normal.png";

        // ------------------------------------------------------------------
        // 진입점 (메뉴 클릭)
        // ------------------------------------------------------------------
        [MenuItem(MenuPath)]
        public static void RunFix()
        {
            // 미저장 변경이 있으면 저장 여부를 물어본다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[FixGameUIRules] 사용자가 저장을 취소하여 작업을 중단합니다.");
                return;
            }

            // 원래 씬 경로를 기억해 두어 작업 완료 후 복구한다.
            string previousScenePath = EditorSceneManager.GetActiveScene().path;

            if (!System.IO.File.Exists(GameScenePath))
            {
                EditorUtility.DisplayDialog("오류",
                    $"Game.unity를 찾을 수 없습니다.\n{GameScenePath}", "OK");
                return;
            }

            // Game.unity를 Single 모드로 연다.
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var log         = new List<string>();
            bool changed    = false;

            // ==================================================================
            // Step 1 — BuildingPanel / ProductionPanel 높이 확인 (idempotent)
            // ==================================================================
            // 목적: anchorMin.y=0.0, anchorMax.y=0.4 (화면 하단 40%)가 맞는지 확인한다.
            // 이미 올바른 값이면 변경하지 않고 로그만 남긴다.
            // 대상 GO 이름: "BuildingPanel" (BuildingPopup 하위)
            //              "ProductionPanel" (ProductionPopup 하위 및 BuildingActionPanel 하위)
            // ------------------------------------------------------------------
            log.Add("\n=== Step 1: 패널 높이 확인 ===");
            changed |= EnsureBottomPanel("BuildingPanel",    log);
            changed |= EnsureBottomPanel("ProductionPanel",  log);

            // ==================================================================
            // Step 2 — CancelButton 세 개 앵커 통일
            // ==================================================================
            // 현재 세 CancelButton 모두 anchorMin=(1,1), anchorMax=(1,1) + sizeDelta=(100,80).
            // 이를 우상단 비율 앵커로 변환한다:
            //   anchorMin=(0.87, 0.78), anchorMax=(1.0, 1.0), pivot=(1, 1), sizeDelta=(0,0)
            // 이렇게 하면 패널 크기가 달라져도 항상 오른쪽 상단 구석에 위치한다.
            // ------------------------------------------------------------------
            log.Add("\n=== Step 2: CancelButton 앵커 통일 ===");
            // BuildingPlacementUI > BuildingPanel > CancelButton
            // BuildingActionPanelUI > ProductionPanel > CancelButton
            // ProductionPanelUI > ProductionPanel > CancelButton
            // — 씬에 'CancelButton' 이름의 GO가 여러 개이므로
            //   각각 특정 부모 이름으로 찾는다.
            changed |= FixCancelButton("BuildingPanel",  "CancelButton", log);
            changed |= FixCancelButton("ProductionPanel","CancelButton", log);

            // ==================================================================
            // Step 3 — BuildingButtons 컨테이너 비율화 + BuildingButtons1~3 정리
            // ==================================================================
            // BuildingButtons(VerticalLayoutGroup 본체):
            //   현재: anchorMin.y=0.5, anchorMax.y=0.5 (포인트앵커) + sizeDelta.x=-60, .y=100
            //   목표: anchorMin=(0.0, 0.1), anchorMax=(1.0, 0.9) → BuildingPanel 내부 중간 영역
            //         sizeDelta=(0,0), anchoredPosition=(0,0)
            // BuildingButtons1~3(HorizontalLayoutGroup 자식들):
            //   VerticalLayoutGroup이 자동으로 배치하므로 sizeDelta=(0,0)만 지정한다.
            // ------------------------------------------------------------------
            log.Add("\n=== Step 3: BuildingButtons 비율화 ===");
            changed |= FixBuildingButtons(log);

            // ==================================================================
            // Step 4 — ProductionPanelUI 컨테이너 비율화
            // ==================================================================
            // ProductionPanelUI > ProductionPanel(=1458369113) 하위:
            //   UnitsButtons: 포인트앵커 anchorMin.y=0.5 → anchorMin.y=0.67, anchorMax.y=1.0 (상단 33%)
            //   QueueSlots  : 포인트앵커 anchorMin.y=0   → anchorMin.y=0.34, anchorMax.y=0.67 (중간 33%)
            //   ProgressBar : 포인트앵커 anchorMin.y=0   → anchorMin.y=0.17, anchorMax.y=0.34 (하단 17%)
            //   InfoBar     : 포인트앵커 anchorMin.y=0   → anchorMin.y=0.00, anchorMax.y=0.17 (최하단 17%)
            // 씬에 같은 이름 GO가 여러 개이므로 부모 이름 "ProductionPanel"을 기준으로 구분한다.
            // ------------------------------------------------------------------
            log.Add("\n=== Step 4: ProductionPanelUI 컨테이너 비율화 ===");
            changed |= FixProductionPanelContainers(log);

            // ==================================================================
            // Step 5 — BuildingActionPanelUI 내부 UnitsButtons 비율화
            // ==================================================================
            // BuildingActionPanel > ProductionPanel(=1401889088) 하위:
            //   UnitsButtons: anchorMin.y=0.5, anchorMax.y=0.5 (포인트앵커)
            //   → Step 4와 동일한 비율(0.67~1.0) 적용
            //   CancelButton: 이미 Step 2에서 처리됨
            // ------------------------------------------------------------------
            log.Add("\n=== Step 5: BuildingActionPanelUI 컨테이너 비율화 ===");
            changed |= FixBuildingActionPanelContainers(log);

            // ==================================================================
            // Step 6 — GameEndUI (GameEndPanel 자식들)
            // ==================================================================
            // GameEndPanel 자식들의 현재 앵커:
            //   ResultText   : anchorMin=(0.1, 0.62), anchorMax=(0.9, 0.75) ← 이미 비율 앵커
            //   CountdownText: anchorMin=(0.0, 0.0),  anchorMax=(1.0, 0.5)  ← 이미 비율 앵커
            //   RestartButton: anchorMin=(0.15, 0.43),anchorMax=(0.85, 0.55)← 이미 비율 앵커
            //   LobbyButton  : anchorMin=(0.15, 0.29),anchorMax=(0.85, 0.41)← 이미 비율 앵커
            // 모두 이미 올바른 비율 앵커이므로, TMP AutoSize와 버튼 스프라이트만 적용한다.
            // ------------------------------------------------------------------
            log.Add("\n=== Step 6: GameEndUI TMP AutoSize + 버튼 스프라이트 ===");
            changed |= FixGameEndUI(log);

            // ==================================================================
            // Step 7 — InGameSettingsPanel > Panel 비율화
            // ==================================================================
            // Panel(InGameSettingsPanel 하위):
            //   현재: anchorMin.y=0.5, anchorMax.y=0.5 (포인트앵커) + sizeDelta.y=650
            //   목표: anchorMin=(0.1, 0.3), anchorMax=(0.9, 0.7), sizeDelta=(0,0), anchoredPosition=(0,0)
            // 내부 요소들:
            //   CloseButton  : anchorMin=(1,1), anchorMax=(1,1) → 오른쪽 상단 (그대로 유지)
            //   SoundButton  : anchorMin=(0.5,0.5), anchorMax=(0.5,0.5) → 중앙 상단 비율화
            //   ForfeitButton: anchorMin=(0.5,0.5), anchorMax=(0.5,0.5) → 중앙 하단 비율화
            // ------------------------------------------------------------------
            log.Add("\n=== Step 7: InGameSettingsPanel 비율화 ===");
            changed |= FixInGameSettingsPanel(log);

            // ==================================================================
            // Step 8 — GameHUD 비율화
            // ==================================================================
            // GameHUD:
            //   현재: anchorMin=(0, 1), anchorMax=(1, 1), anchoredPosition.y=-50, sizeDelta.y=100
            //   → 상단 앵커에 고정 픽셀 오프셋 방식: 해상도에 따라 HUD 높이가 고정됨
            //   목표: anchorMin=(0, 0.93), anchorMax=(1, 1.0), sizeDelta=(0,0), anchoredPosition=(0,0)
            //   → 화면 상단 7%를 HUD 영역으로 지정 (비율 방식)
            // AspectRatioFitter: GetComponent로 확인 후 없을 때만 추가한다 (중복 방지).
            // ------------------------------------------------------------------
            log.Add("\n=== Step 8: GameHUD 비율화 + AspectRatioFitter ===");
            changed |= FixGameHUD(log);

            // ==================================================================
            // Step 9 — RematchRequestPopup > DeclinedPanel > ButtonArea 비율화
            // ==================================================================
            // ButtonArea:
            //   현재: anchorMin=(0.5,0), anchorMax=(0.5,0) (포인트앵커) + sizeDelta=(212,88)
            //   목표: anchorMin=(0.15, 0.05), anchorMax=(0.85, 0.30), pivot=(0.5, 0), sizeDelta=(0,0)
            //   → DeclinedPanel 너비의 70%를 차지하고 하단 5~30% 영역에 배치
            // ------------------------------------------------------------------
            log.Add("\n=== Step 9: RematchRequestPopup ButtonArea 비율화 ===");
            changed |= FixRematchButtonArea(log);

            // ================================================================
            // 씬 저장
            // ================================================================
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                log.Add($"\n▶ Game.unity 저장 {(saved ? "성공" : "실패")}");
            }
            else
            {
                log.Add("\n▶ 변경 사항 없음. 저장 생략.");
            }

            // 원래 씬으로 복구
            RestoreScene(previousScenePath);

            // 결과 출력
            string summary = string.Join("\n", log);
            Debug.Log("[FixGameUIRules] 완료:\n" + summary);
            EditorUtility.DisplayDialog("게임 UI Rules 수정 완료", summary, "OK");
        }

        // ==================================================================
        // Step 1 헬퍼 — 패널 높이 idempotent 확인
        // ==================================================================

        /// <summary>
        /// 지정된 이름의 GO가 anchorMin.y==0, anchorMax.y==0.4, sizeDelta==(0,0)인지 확인한다.
        /// 이미 올바르면 "이미 적용됨" 로그를 남기고 false 반환.
        /// 다르면 수정 후 true 반환.
        /// </summary>
        private static bool EnsureBottomPanel(string goName, List<string> log)
        {
            // 씬에 같은 이름 GO가 여러 개일 수 있으므로 FindObjectsOfType으로 모두 찾는다.
            var allTransforms = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            bool anyChanged = false;
            bool foundAny   = false;
            foreach (var rt in allTransforms)
            {
                if (rt.name != goName) continue;
                foundAny = true;

                // 목표값
                var targetMin   = new Vector2(0f, 0f);
                var targetMax   = new Vector2(1f, 0.4f);
                var targetDelta = Vector2.zero;
                var targetPos   = Vector2.zero;

                bool alreadyCorrect =
                    rt.anchorMin == targetMin &&
                    rt.anchorMax == targetMax &&
                    rt.sizeDelta == targetDelta;

                if (alreadyCorrect)
                {
                    log.Add($"  [스킵] {goName}: 이미 올바른 앵커 적용됨");
                }
                else
                {
                    Undo.RecordObject(rt, $"Fix {goName} Anchor");
                    rt.anchorMin        = targetMin;
                    rt.anchorMax        = targetMax;
                    rt.sizeDelta        = targetDelta;
                    rt.anchoredPosition = targetPos;
                    rt.pivot            = new Vector2(0.5f, 0f);
                    EditorUtility.SetDirty(rt);
                    log.Add($"  [수정] {goName}: anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}");
                    anyChanged = true;
                }
            }
            if (!foundAny)
                log.Add($"  [경고] '{goName}' GO를 씬에서 찾지 못했습니다.");

            return anyChanged;
        }

        // ==================================================================
        // Step 2 헬퍼 — CancelButton 앵커 통일
        // ==================================================================

        /// <summary>
        /// 특정 부모 이름을 가진 GO 하위에서 cancelButtonName 이름의 GO를 찾아
        /// 우상단 비율 앵커로 수정한다.
        /// </summary>
        private static bool FixCancelButton(string parentName, string cancelButtonName, List<string> log)
        {
            // 씬의 모든 RectTransform 중 이름이 cancelButtonName이고
            // 부모 이름이 parentName인 것을 찾는다.
            var allRts = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            bool anyChanged = false;
            foreach (var rt in allRts)
            {
                if (rt.name != cancelButtonName) continue;
                if (rt.parent == null || rt.parent.name != parentName) continue;

                // 목표: anchorMin=(0.87, 0.78), anchorMax=(1.0, 1.0), pivot=(1,1), sizeDelta=(0,0)
                // 의미: 패널 오른쪽 상단 13%×22% 영역에 CancelButton 배치
                var targetMin   = new Vector2(0.87f, 0.78f);
                var targetMax   = new Vector2(1.0f,  1.0f);
                var targetPivot = new Vector2(1.0f,  1.0f);

                bool alreadyCorrect =
                    rt.anchorMin == targetMin &&
                    rt.anchorMax == targetMax;

                if (alreadyCorrect)
                {
                    log.Add($"  [스킵] {parentName}/{cancelButtonName}: 이미 올바른 앵커");
                    continue;
                }

                Undo.RecordObject(rt, $"Fix {parentName}/{cancelButtonName} Anchor");
                rt.anchorMin        = targetMin;
                rt.anchorMax        = targetMax;
                rt.pivot            = targetPivot;
                rt.sizeDelta        = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                EditorUtility.SetDirty(rt);

                log.Add($"  [수정] {parentName}/{cancelButtonName}: " +
                        $"anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}");
                anyChanged = true;
            }

            if (!anyChanged)
            {
                log.Add($"  [정보] {parentName}/{cancelButtonName} 수정 없음 (이미 적용 또는 미발견)");
            }
            return anyChanged;
        }

        // ==================================================================
        // Step 3 헬퍼 — BuildingButtons 비율화
        // ==================================================================

        /// <summary>
        /// BuildingButtons(VerticalLayoutGroup 본체)를 비율 앵커로 전환하고
        /// 자식 BuildingButtons1~3의 sizeDelta를 (0,0)으로 정리한다.
        /// </summary>
        private static bool FixBuildingButtons(List<string> log)
        {
            bool anyChanged = false;
            var allRts = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var rt in allRts)
            {
                if (rt.name == "BuildingButtons" && rt.parent != null && rt.parent.name == "BuildingPanel")
                {
                    // BuildingPanel 내부 수직 버튼 목록 컨테이너
                    // 목표: BuildingPanel 전체 너비, 세로 10%~90% 영역
                    //   anchorMin=(0.0, 0.1), anchorMax=(1.0, 0.9)
                    //   sizeDelta=(0,0), anchoredPosition=(0,0), pivot=(0.5, 0.5)
                    var targetMin = new Vector2(0.0f, 0.1f);
                    var targetMax = new Vector2(1.0f, 0.9f);

                    if (rt.anchorMin == targetMin && rt.anchorMax == targetMax && rt.sizeDelta == Vector2.zero)
                    {
                        log.Add("  [스킵] BuildingButtons: 이미 올바른 앵커");
                    }
                    else
                    {
                        Undo.RecordObject(rt, "Fix BuildingButtons Anchor");
                        rt.anchorMin        = targetMin;
                        rt.anchorMax        = targetMax;
                        rt.pivot            = new Vector2(0.5f, 0.5f);
                        rt.sizeDelta        = Vector2.zero;
                        rt.anchoredPosition = Vector2.zero;
                        EditorUtility.SetDirty(rt);
                        log.Add($"  [수정] BuildingButtons: anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}");
                        anyChanged = true;
                    }
                }

                // BuildingButtons1~3: VerticalLayoutGroup 자식이므로 sizeDelta=(0,0)만 정리
                if ((rt.name == "BuildingButtons1" ||
                     rt.name == "BuildingButtons2" ||
                     rt.name == "BuildingButtons3") &&
                    rt.parent != null && rt.parent.name == "BuildingButtons")
                {
                    if (rt.sizeDelta == Vector2.zero)
                    {
                        log.Add($"  [스킵] {rt.name}: sizeDelta 이미 (0,0)");
                    }
                    else
                    {
                        Undo.RecordObject(rt, $"Fix {rt.name} SizeDelta");
                        rt.sizeDelta = Vector2.zero;
                        EditorUtility.SetDirty(rt);
                        log.Add($"  [수정] {rt.name}: sizeDelta → (0,0)");
                        anyChanged = true;
                    }
                }
            }
            return anyChanged;
        }

        // ==================================================================
        // Step 4 헬퍼 — ProductionPanelUI 컨테이너 비율화
        // ==================================================================

        /// <summary>
        /// ProductionPanelUI > ProductionPanel(=1458369113) 하위 컨테이너들을
        /// 1:1:1:1 비율(실제로는 상단 33%/중간 33%/하단17%/최하단17%)로 배치한다.
        /// 부모 이름이 "ProductionPanel"이고 부모의 부모 이름이 "ProductionPopup" 하위인 것만 처리.
        /// </summary>
        private static bool FixProductionPanelContainers(List<string> log)
        {
            bool anyChanged = false;
            var allRts = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var rt in allRts)
            {
                // ProductionPanelUI 하위 ProductionPanel 찾기
                // (BuildingActionPanel 하위 ProductionPanel과 구분)
                if (rt.parent == null || rt.parent.name != "ProductionPanel") continue;

                // 부모의 부모가 "ProductionPopup"인지 확인하여 ProductionPanelUI 하위임을 검증
                var grandParent = rt.parent.parent;
                if (grandParent == null) continue;
                // ProductionPopup GO의 RectTransform 체인: ProductionPanel.parent = ProductionPopup(RT)
                // GameObjectName은 ProductionPopup
                bool isProductionPopupChild = (grandParent.name == "ProductionPopup");
                if (!isProductionPopupChild) continue;

                string name = rt.name;

                // 각 컨테이너별 목표 앵커 지정
                // 비율 배분: 상단(UnitsButtons)=33%, 중간(QueueSlots)=33%, 하단(ProgressBar)=17%, 최하단(InfoBar)=17%
                Vector2 min, max;
                bool isTarget = true;

                switch (name)
                {
                    case "UnitsButtons":
                        // 상단 1/3 영역 (0.67 ~ 1.0)
                        min = new Vector2(0f, 0.67f);
                        max = new Vector2(1f, 1.00f);
                        break;
                    case "QueueSlots":
                        // 중간 1/3 영역 (0.34 ~ 0.67)
                        min = new Vector2(0f, 0.34f);
                        max = new Vector2(1f, 0.67f);
                        break;
                    case "ProgressBar":
                        // 하단 17% 영역 (0.17 ~ 0.34)
                        min = new Vector2(0f, 0.17f);
                        max = new Vector2(1f, 0.34f);
                        break;
                    case "InfoBar":
                        // 최하단 17% 영역 (0.00 ~ 0.17)
                        min = new Vector2(0f, 0.00f);
                        max = new Vector2(1f, 0.17f);
                        break;
                    default:
                        isTarget = false;
                        min = max = Vector2.zero;
                        break;
                }

                if (!isTarget) continue;

                if (rt.anchorMin == min && rt.anchorMax == max && rt.sizeDelta == Vector2.zero)
                {
                    log.Add($"  [스킵] ProductionPanel/{name}: 이미 올바른 앵커");
                    continue;
                }

                Undo.RecordObject(rt, $"Fix ProductionPanel/{name} Anchor");
                rt.anchorMin        = min;
                rt.anchorMax        = max;
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                EditorUtility.SetDirty(rt);

                log.Add($"  [수정] ProductionPanel/{name}: anchorMin={min}, anchorMax={max}");
                anyChanged = true;
            }
            return anyChanged;
        }

        // ==================================================================
        // Step 5 헬퍼 — BuildingActionPanelUI 내부 컨테이너 비율화
        // ==================================================================

        /// <summary>
        /// BuildingActionPanel > ProductionPanel(=1401889088) 하위 UnitsButtons를
        /// Step 4와 동일한 비율(0.67~1.0)로 비율화한다.
        /// </summary>
        private static bool FixBuildingActionPanelContainers(List<string> log)
        {
            bool anyChanged = false;
            var allRts = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var rt in allRts)
            {
                if (rt.parent == null || rt.parent.name != "ProductionPanel") continue;

                // 부모의 부모가 "BuildingActionPanel"인지 확인
                var grandParent = rt.parent.parent;
                if (grandParent == null || grandParent.name != "BuildingActionPanel") continue;

                string name = rt.name;
                Vector2 min, max;
                bool isTarget = true;

                switch (name)
                {
                    case "UnitsButtons":
                        min = new Vector2(0f, 0.67f);
                        max = new Vector2(1f, 1.00f);
                        break;
                    default:
                        isTarget = false;
                        min = max = Vector2.zero;
                        break;
                }

                if (!isTarget) continue;

                if (rt.anchorMin == min && rt.anchorMax == max && rt.sizeDelta == Vector2.zero)
                {
                    log.Add($"  [스킵] BuildingActionPanel/ProductionPanel/{name}: 이미 올바른 앵커");
                    continue;
                }

                Undo.RecordObject(rt, $"Fix BuildingActionPanel/{name} Anchor");
                rt.anchorMin        = min;
                rt.anchorMax        = max;
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                EditorUtility.SetDirty(rt);

                log.Add($"  [수정] BuildingActionPanel/ProductionPanel/{name}: " +
                        $"anchorMin={min}, anchorMax={max}");
                anyChanged = true;
            }
            return anyChanged;
        }

        // ==================================================================
        // Step 6 헬퍼 — GameEndUI
        // ==================================================================

        /// <summary>
        /// GameEndPanel 자식들에 TMP AutoSize를 적용하고 버튼에 금 스프라이트를 적용한다.
        /// (앵커는 이미 비율 앵커이므로 변경 불필요)
        /// </summary>
        private static bool FixGameEndUI(List<string> log)
        {
            bool anyChanged = false;

            // GameEndPanel GO를 찾는다.
            var allRts = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            GameObject gameEndPanel = null;
            foreach (var rt in allRts)
            {
                if (rt.name == "GameEndPanel")
                {
                    gameEndPanel = rt.gameObject;
                    break;
                }
            }

            if (gameEndPanel == null)
            {
                log.Add("  [경고] GameEndPanel GO를 찾지 못했습니다.");
                return false;
            }

            // 버튼 스프라이트 로드
            var goldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoldBtnSpritePath);
            if (goldSprite == null)
            {
                log.Add($"  [경고] 금 버튼 스프라이트를 찾지 못했습니다: {GoldBtnSpritePath}");
            }

            // GameEndPanel 자식들 순회
            foreach (Transform child in gameEndPanel.transform)
            {
                string cName = child.name;

                // ---- TMP AutoSize 적용 (ResultText) ----
                if (cName == "ResultText")
                {
                    var tmp = child.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        Undo.RecordObject(tmp, "Fix ResultText AutoSize");
                        tmp.enableAutoSizing = true;
                        tmp.fontSizeMin      = 18f;
                        tmp.fontSizeMax      = 60f;
                        EditorUtility.SetDirty(tmp);
                        log.Add("  [수정] ResultText: TMP AutoSize ON (18~60)");
                        anyChanged = true;
                    }
                }

                // ---- TMP AutoSize 적용 (CountdownText) ----
                if (cName == "CountdownText")
                {
                    var tmp = child.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        Undo.RecordObject(tmp, "Fix CountdownText AutoSize");
                        tmp.enableAutoSizing = true;
                        tmp.fontSizeMin      = 18f;
                        tmp.fontSizeMax      = 40f;
                        EditorUtility.SetDirty(tmp);
                        log.Add("  [수정] CountdownText: TMP AutoSize ON (18~40)");
                        anyChanged = true;
                    }
                }

                // ---- 버튼 스프라이트 + TMP 적용 (RestartButton, LobbyButton) ----
                if (cName == "RestartButton" || cName == "LobbyButton")
                {
                    // 버튼 Image 컴포넌트에 금 스프라이트 적용
                    if (goldSprite != null)
                    {
                        var img = child.GetComponent<Image>();
                        if (img != null && img.sprite != goldSprite)
                        {
                            Undo.RecordObject(img, $"Fix {cName} Sprite");
                            img.sprite = goldSprite;
                            EditorUtility.SetDirty(img);
                            log.Add($"  [수정] {cName}: 금 버튼 스프라이트 적용");
                            anyChanged = true;
                        }
                    }

                    // 버튼 내부 텍스트(자식 GO)에 TMP AutoSize 적용
                    foreach (Transform btnChild in child)
                    {
                        var tmp = btnChild.GetComponent<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            Undo.RecordObject(tmp, $"Fix {cName}/{btnChild.name} AutoSize");
                            tmp.enableAutoSizing = true;
                            tmp.fontSizeMin      = 18f;
                            tmp.fontSizeMax      = 40f;
                            EditorUtility.SetDirty(tmp);
                            log.Add($"  [수정] {cName}/{btnChild.name}: TMP AutoSize ON");
                            anyChanged = true;
                        }
                    }
                }
            }
            return anyChanged;
        }

        // ==================================================================
        // Step 7 헬퍼 — InGameSettingsPanel 비율화
        // ==================================================================

        /// <summary>
        /// InGameSettingsPanel 하위 Panel GO를 비율 앵커로 전환하고
        /// 내부 SoundButton / ForfeitButton을 비율 앵커로 변환한다.
        /// </summary>
        private static bool FixInGameSettingsPanel(List<string> log)
        {
            bool anyChanged = false;
            var allRts = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var rt in allRts)
            {
                // InGameSettingsPanel 하위 "Panel" GO
                if (rt.name == "Panel" &&
                    rt.parent != null && rt.parent.name == "InGameSettingsPanel")
                {
                    // 목표: anchorMin=(0.1, 0.3), anchorMax=(0.9, 0.7)
                    // → 화면 중앙 영역(좌우 80%, 상하 40%)에 설정 패널 배치
                    var targetMin = new Vector2(0.1f, 0.3f);
                    var targetMax = new Vector2(0.9f, 0.7f);

                    if (rt.anchorMin == targetMin && rt.anchorMax == targetMax && rt.sizeDelta == Vector2.zero)
                    {
                        log.Add("  [스킵] InGameSettingsPanel/Panel: 이미 올바른 앵커");
                    }
                    else
                    {
                        Undo.RecordObject(rt, "Fix InGameSettingsPanel/Panel Anchor");
                        rt.anchorMin        = targetMin;
                        rt.anchorMax        = targetMax;
                        rt.pivot            = new Vector2(0.5f, 0.5f);
                        rt.sizeDelta        = Vector2.zero;
                        rt.anchoredPosition = Vector2.zero;
                        EditorUtility.SetDirty(rt);
                        log.Add($"  [수정] InGameSettingsPanel/Panel: anchorMin={targetMin}, anchorMax={targetMax}");
                        anyChanged = true;
                    }
                }

                // Panel 하위 SoundButton: 중앙 상단 영역으로 비율화
                if (rt.name == "SoundButton" &&
                    rt.parent != null && rt.parent.name == "Panel")
                {
                    // 목표: anchorMin=(0.1, 0.55), anchorMax=(0.9, 0.85)
                    // → Panel 내 상단 30% 영역
                    var targetMin = new Vector2(0.1f, 0.55f);
                    var targetMax = new Vector2(0.9f, 0.85f);

                    if (rt.anchorMin == targetMin && rt.anchorMax == targetMax && rt.sizeDelta == Vector2.zero)
                    {
                        log.Add("  [스킵] Panel/SoundButton: 이미 올바른 앵커");
                    }
                    else
                    {
                        Undo.RecordObject(rt, "Fix SoundButton Anchor");
                        rt.anchorMin        = targetMin;
                        rt.anchorMax        = targetMax;
                        rt.pivot            = new Vector2(0.5f, 0.5f);
                        rt.sizeDelta        = Vector2.zero;
                        rt.anchoredPosition = Vector2.zero;
                        EditorUtility.SetDirty(rt);
                        log.Add($"  [수정] Panel/SoundButton: anchorMin={targetMin}, anchorMax={targetMax}");
                        anyChanged = true;
                    }
                }

                // Panel 하위 ForfeitButton: 중앙 하단 영역으로 비율화
                if (rt.name == "ForfeitButton" &&
                    rt.parent != null && rt.parent.name == "Panel")
                {
                    // 목표: anchorMin=(0.1, 0.15), anchorMax=(0.9, 0.45)
                    // → Panel 내 하단 30% 영역
                    var targetMin = new Vector2(0.1f, 0.15f);
                    var targetMax = new Vector2(0.9f, 0.45f);

                    if (rt.anchorMin == targetMin && rt.anchorMax == targetMax && rt.sizeDelta == Vector2.zero)
                    {
                        log.Add("  [스킵] Panel/ForfeitButton: 이미 올바른 앵커");
                    }
                    else
                    {
                        Undo.RecordObject(rt, "Fix ForfeitButton Anchor");
                        rt.anchorMin        = targetMin;
                        rt.anchorMax        = targetMax;
                        rt.pivot            = new Vector2(0.5f, 0.5f);
                        rt.sizeDelta        = Vector2.zero;
                        rt.anchoredPosition = Vector2.zero;
                        EditorUtility.SetDirty(rt);
                        log.Add($"  [수정] Panel/ForfeitButton: anchorMin={targetMin}, anchorMax={targetMax}");
                        anyChanged = true;
                    }
                }
            }
            return anyChanged;
        }

        // ==================================================================
        // Step 8 헬퍼 — GameHUD 비율화 + AspectRatioFitter
        // ==================================================================

        /// <summary>
        /// GameHUD의 앵커를 상단 7% 영역(비율)으로 전환하고
        /// AspectRatioFitter가 없을 때만 추가한다.
        /// </summary>
        private static bool FixGameHUD(List<string> log)
        {
            bool anyChanged = false;
            var allRts = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var rt in allRts)
            {
                if (rt.name != "GameHUD") continue;

                // GameHUD 앵커 목표:
                //   anchorMin=(0, 0.93), anchorMax=(1, 1.0)
                //   → 화면 상단 7% 영역을 HUD로 사용
                //   sizeDelta=(0,0), anchoredPosition=(0,0)
                var targetMin = new Vector2(0f, 0.93f);
                var targetMax = new Vector2(1f, 1.00f);

                if (rt.anchorMin == targetMin && rt.anchorMax == targetMax && rt.sizeDelta == Vector2.zero)
                {
                    log.Add("  [스킵] GameHUD: 이미 올바른 앵커");
                }
                else
                {
                    Undo.RecordObject(rt, "Fix GameHUD Anchor");
                    rt.anchorMin        = targetMin;
                    rt.anchorMax        = targetMax;
                    rt.pivot            = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta        = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;
                    EditorUtility.SetDirty(rt);
                    log.Add($"  [수정] GameHUD: anchorMin={targetMin}, anchorMax={targetMax}");
                    anyChanged = true;
                }

                // AspectRatioFitter: 이미 있으면 추가하지 않는다 (중복 방지).
                var arf = rt.gameObject.GetComponent<UnityEngine.UI.AspectRatioFitter>();
                if (arf == null)
                {
                    // Undo.AddComponent를 사용하면 Ctrl+Z로 컴포넌트 추가를 되돌릴 수 있다.
                    arf = Undo.AddComponent<UnityEngine.UI.AspectRatioFitter>(rt.gameObject);
                    // 가로 비율에 맞게 세로를 조정하는 모드로 설정
                    arf.aspectMode  = UnityEngine.UI.AspectRatioFitter.AspectMode.WidthControlsHeight;
                    arf.aspectRatio = 7.11f; // 화면 가로/세로 = 약 7.11:1 (HUD 바 기준)
                    EditorUtility.SetDirty(arf);
                    log.Add("  [수정] GameHUD: AspectRatioFitter 추가 (WidthControlsHeight, ratio=7.11)");
                    anyChanged = true;
                }
                else
                {
                    log.Add("  [스킵] GameHUD: AspectRatioFitter 이미 존재");
                }

                break; // GameHUD는 씬에 하나뿐
            }
            return anyChanged;
        }

        // ==================================================================
        // Step 9 헬퍼 — RematchRequestPopup ButtonArea 비율화
        // ==================================================================

        /// <summary>
        /// RematchRequestPopup > DeclinedPanel > ButtonArea를
        /// 비율 앵커로 전환한다.
        /// </summary>
        private static bool FixRematchButtonArea(List<string> log)
        {
            bool anyChanged = false;
            var allRts = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var rt in allRts)
            {
                // ButtonArea이고 부모가 DeclinedPanel인 것을 찾는다.
                if (rt.name != "ButtonArea") continue;
                if (rt.parent == null || rt.parent.name != "DeclinedPanel") continue;

                // 목표:
                //   anchorMin=(0.15, 0.05), anchorMax=(0.85, 0.30)
                //   → DeclinedPanel 너비 70%, 하단 25% 영역에 배치
                //   pivot=(0.5, 0.0) → 하단 기준 정렬
                var targetMin   = new Vector2(0.15f, 0.05f);
                var targetMax   = new Vector2(0.85f, 0.30f);
                var targetPivot = new Vector2(0.5f,  0.0f);

                if (rt.anchorMin == targetMin && rt.anchorMax == targetMax && rt.sizeDelta == Vector2.zero)
                {
                    log.Add("  [스킵] DeclinedPanel/ButtonArea: 이미 올바른 앵커");
                    continue;
                }

                Undo.RecordObject(rt, "Fix DeclinedPanel/ButtonArea Anchor");
                rt.anchorMin        = targetMin;
                rt.anchorMax        = targetMax;
                rt.pivot            = targetPivot;
                rt.sizeDelta        = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                EditorUtility.SetDirty(rt);

                log.Add($"  [수정] DeclinedPanel/ButtonArea: anchorMin={targetMin}, anchorMax={targetMax}");
                anyChanged = true;
                break; // 하나만 있으므로 break
            }
            return anyChanged;
        }

        // ==================================================================
        // 씬 복구 헬퍼
        // ==================================================================

        /// <summary>
        /// 작업 완료 후 원래 씬으로 복구한다.
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
