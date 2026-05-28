// ============================================================================
// FixUIColorAndLayout.cs
// ----------------------------------------------------------------------------
// 목적 (1회성 셋업 스크립트):
//   2026-05-27 게임 화면 UI TC FAIL 9건(색상 + 레이아웃)을 일괄 자동 수정한다.
//
// 수행 작업:
//   [Round 1]
//   1) Resources/Config/UIColorConfig.asset 생성 (없을 때만)
//   2) Game.unity 의 다음 RectTransform 앵커 일괄 변경 (외부 컨테이너)
//        - GameEndPanel 자식 3개 (ResultText, RestartButton, LobbyButton)
//        - SettingsButton (GameHUD 우측 상단)
//        - BuildingPanel / ProductionPanel / ActionPanel (3개 팝업 안쪽 컨테이너)
//        - RequestPanel / DeclinedPanel (RematchRequestPopup 안쪽)
//   3) 다음 UI 컴포넌트의 _colorConfig SerializeField 에 위 에셋을 자동 연결
//        - GameEndUI / GameHudUI / BuildingPlacementUI
//        - ProductionPanelUI / BuildingActionPanelUI (BuildingPanelBase 상속)
//        - ConfirmPopup
//   4) ConfirmPopup 은 _confirmButton.targetGraphic / _cancelButton.targetGraphic 으로
//      버튼 배경색을 적용하므로 별도 Image 슬롯 연결이 필요 없다.
//
//   [Round 2 — 2026-05-27 추가]
//   외부 컨테이너만 비율 기반으로 바뀌고 내부 자식 요소가 여전히 고정 픽셀이라
//   실기기 BP-001/BP-002/PRD-001/BAP-001/SET-007/END-001/MULTI-END-002 FAIL.
//   다음을 추가로 처리:
//        Step 12) BuildingPlacementUI / ProductionPanelUI / BuildingActionPanelUI
//                 의 내부 자식 RectTransform 을 부모 기준 앵커 비율로 전환하고
//                 텍스트는 TMP Auto Size 활성화 (GridLayoutGroup 의 경우 cellSize 도 조정)
//        Step 13) GameEndUI 의 _resultText / RestartButton 레이블 / LobbyButton 레이블 /
//                 _countdownText 에 TMP Auto Size 활성화
//        Step 14) RematchRequestPopup 의 _requestPanel / _declinedPanel 내부 텍스트는
//                 Auto Size 활성화, 버튼 RectTransform 은 앵커 비율 전환
//        Step 15) BuildingPlacementUI 의 비용 텍스트(_buildingCostTexts) 좌측 치우침 수정
//                 (TextAlignmentOptions.Center + anchoredPosition.x = 0)
//
// 사용 방법:
//   Unity 메뉴 [Hexiege] -> [Setup] -> [UI Color & Layout Fix]
//
// 안전성:
//   - Undo.RecordObject / Undo.RegisterCreatedObjectUndo 적용 → Ctrl+Z로 되돌릴 수 있음.
//   - 변경 후 씬을 자동 저장하므로 Ctrl+Z는 작업 직후에만 유효함 (다음 작업 후에는 못 되돌림).
//   - 비활성 오브젝트(GameEndPanel 등)도 FindObjectsInactive.Include로 정상 탐색.
//
// 슬라이드 애니메이션 영향 검토:
//   BuildingPanel / ProductionPanel / ActionPanel 은 SlideFromBottom 애니메이션을 사용한다.
//   UIAnimator.SlideInFromBottom 은 RectTransform.anchoredPosition.y 를 -offsetY → 0 으로
//   보간한다. 앵커를 (0,0)~(1,0.4), pivot=(0.5, 0) 로 바꾸면 컨테이너의 기준점이 부모 하단으로
//   고정되고, anchoredPosition.y=0 일 때 패널이 정확히 부모의 아래쪽에 붙는다.
//   따라서 -offsetY → 0 슬라이드는 그대로 자연스럽게 동작한다.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hexiege.Infrastructure;
using Hexiege.Presentation;

namespace Hexiege.Editor
{
    /// <summary>
    /// 색상/레이아웃 FAIL 9건을 일괄 수정하는 1회성 에디터 유틸.
    /// </summary>
    public static class FixUIColorAndLayout
    {
        // ====================================================================
        // 상수
        // ====================================================================

        // 메뉴 경로
        private const string MenuPath = "Hexiege/Setup/UI Color & Layout Fix";

        // 대상 씬
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // UIColorConfig 에셋 저장 위치 (Resources 폴더 내부여야 런타임 로드도 가능)
        private const string ResourcesDir = "Assets/_Project/Resources";
        private const string ConfigDir    = "Assets/_Project/Resources/Config";
        private const string ConfigAssetPath = "Assets/_Project/Resources/Config/UIColorConfig.asset";

        // ====================================================================
        // 진입점
        // ====================================================================

        /// <summary>
        /// 메뉴 클릭 시 실행. 1) UIColorConfig 에셋 생성 → 2) Game.unity 열기 →
        /// 3) RectTransform 앵커 일괄 변경 → 4) UI 컴포넌트에 _colorConfig 연결 → 5) 저장.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void RunFix()
        {
            // 1) UIColorConfig 에셋 준비 (없으면 생성)
            UIColorConfig colorConfig = EnsureColorConfigAsset();
            if (colorConfig == null)
            {
                EditorUtility.DisplayDialog("오류",
                    "UIColorConfig.asset 생성/로드에 실패했습니다.\n" +
                    "Assets/_Project/Resources/Config 경로 권한 또는 디스크 상태를 확인하세요.",
                    "OK");
                return;
            }

            // 2) Game.unity 열기 (현재 씬 변경이 있으면 사용자에게 저장 여부 확인)
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[FixUIColorAndLayout] 사용자가 저장을 취소하여 작업을 중단합니다.");
                return;
            }

            string previousScenePath = EditorSceneManager.GetActiveScene().path;

            if (!System.IO.File.Exists(GameScenePath))
            {
                EditorUtility.DisplayDialog("오류",
                    $"Game.unity 를 찾을 수 없습니다.\n{GameScenePath}", "OK");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var resultLog = new List<string>();
            resultLog.Add($"UIColorConfig 에셋: {ConfigAssetPath}");

            // 3) RectTransform 앵커 일괄 변경
            ApplyRectTransformLayouts(resultLog);

            // 4) UI 컴포넌트에 _colorConfig SerializeField 자동 연결
            ApplyColorConfigToComponents(colorConfig, resultLog);

            // 5) 씬 저장
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            resultLog.Add($"\nGame.unity 저장 {(saved ? "성공" : "실패")}");

            // 원래 씬 복구
            RestoreScene(previousScenePath);

            // 결과 다이얼로그
            EditorUtility.DisplayDialog(
                "UI Color & Layout Fix",
                "작업 완료.\n\n" + string.Join("\n", resultLog) + "\n\n" +
                "[수동 처리 불필요]\n" +
                "ConfirmPopup 은 _confirmButton.targetGraphic 으로 버튼 배경색을 적용합니다.\n" +
                "별도 Image 슬롯 연결 없이 동작합니다.\n\n" +
                "[주의]\n" +
                "변경 후 Ctrl+Z 사용 시 한 번에 모든 변경이 되돌려질 수 있습니다.\n" +
                "필요시 작업을 다시 실행하세요.",
                "OK");
        }

        // ====================================================================
        // 진입점 — 파손된 UI 레이아웃 복구 (1회성)
        // ====================================================================

        /// <summary>
        /// Round 2 에디터 스크립트의 StretchSingleComponent(_cancelButton) 호출이
        /// 잘못 적용한 stretch 앵커를 복구한다.
        ///
        /// 파손 내역:
        ///   - 건물 패널 3개(BuildingPlacementUI / BuildingActionPanelUI / ProductionPanelUI)의
        ///     CancelButton 이 anchorMin=(0,0), anchorMax=(1,1) 전체 stretch 상태가 됨.
        ///   - RematchRequestPopup 의 DeclinedConfirmButton 도 동일하게 전체 stretch 됨.
        ///
        /// 코드는 이미 수정됐으나 Game.unity 씬 파일에는 파손된 값이 저장된 상태이므로
        /// 씬을 열어 해당 RectTransform 들을 의도된 코너/하단-중앙 배치로 되돌린다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Fix Broken UI Layout")]
        public static void RunFixBrokenUI()
        {
            // 현재 씬에 저장하지 않은 변경이 있으면 사용자에게 저장 여부를 먼저 확인한다.
            // 사용자가 취소하면 작업을 중단해 실수로 덮어쓰는 것을 막는다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[FixUIColorAndLayout] 사용자가 저장을 취소하여 작업을 중단합니다.");
                return;
            }

            // 작업 후 원래 보고 있던 씬으로 되돌리기 위해 현재 씬 경로를 기억한다.
            string previousScenePath = EditorSceneManager.GetActiveScene().path;

            // 대상 씬 파일이 실제로 존재하는지 검사 (경로 오타/파일 이동 대비).
            if (!System.IO.File.Exists(GameScenePath))
            {
                EditorUtility.DisplayDialog("오류",
                    $"Game.unity 를 찾을 수 없습니다.\n{GameScenePath}", "OK");
                return;
            }

            // Game.unity 를 단독으로 연다 (다른 씬은 모두 닫힘).
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // 작업 결과를 한 줄씩 모아두었다가 마지막 다이얼로그에 표시한다.
            var resultLog = new List<string>();

            // 1) 건물 패널 3개의 CancelButton 복구.
            FixCancelButtons(resultLog);

            // 2) RematchRequestPopup 의 DeclinedConfirmButton 복구.
            FixDeclinedConfirmButton(resultLog);

            // 3) 변경 사항을 디스크에 저장한다 (Dirty 마킹 후 SaveScene).
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            resultLog.Add($"\nGame.unity 저장 {(saved ? "성공" : "실패")}");

            // 4) 작업 전 보고 있던 씬으로 복구.
            RestoreScene(previousScenePath);

            // 5) 결과 다이얼로그 표시.
            EditorUtility.DisplayDialog(
                "Fix Broken UI Layout",
                "파손된 UI 레이아웃 복구 완료.\n\n" + string.Join("\n", resultLog) + "\n\n" +
                "[주의]\n" +
                "변경 후 Ctrl+Z 사용 시 한 번에 모든 변경이 되돌려질 수 있습니다.\n" +
                "필요시 작업을 다시 실행하세요.",
                "OK");
        }

        // --------------------------------------------------------------------
        // CancelButton 3종 복구
        // --------------------------------------------------------------------

        /// <summary>
        /// BuildingPlacementUI / ProductionPanelUI / BuildingActionPanelUI 의
        /// _cancelButton RectTransform 을 우상단 코너 고정 배치로 복구한다.
        ///
        /// 목표값(닫기 X 아이콘 버튼, 부모 패널 우상단 코너에 고정 픽셀 크기):
        ///   anchorMin = (1,1), anchorMax = (1,1), pivot = (1,1),
        ///   anchoredPosition = (-10,-10), sizeDelta = (100,80)
        /// 피벗 (1,1) 이므로 버튼은 코너 기준 좌하방으로 성장 → 코너에서 10px 안쪽에 자연스럽게 위치.
        ///
        /// 주의: BuildingPlacementUI 는 자체 private _cancelButton 을 가지고,
        ///       ProductionPanelUI / BuildingActionPanelUI 는 BuildingPanelBase 의
        ///       protected _cancelButton 을 상속한다. 상속과 무관하게 각 타입별로
        ///       독립적으로 인스턴스를 찾아 처리한다(SerializedObject 는 동일하게 동작).
        /// </summary>
        private static void FixCancelButtons(List<string> log)
        {
            log.Add("[CancelButton 복구]");

            // 각 타입을 독립적으로 탐색하여 _cancelButton 을 복구한다.
            FixSingleCancelButton<BuildingPlacementUI>(log);
            FixSingleCancelButton<ProductionPanelUI>(log);
            FixSingleCancelButton<BuildingActionPanelUI>(log);
        }

        /// <summary>
        /// 타입 T 인스턴스 하나를 찾아 그 _cancelButton 의 RectTransform 을 우상단 코너 배치로 복구한다.
        /// 비활성 오브젝트도 포함하여 탐색한다(FindObjectsInactive.Include).
        /// </summary>
        private static void FixSingleCancelButton<T>(List<string> log)
            where T : MonoBehaviour
        {
            // 비활성 패널(팝업은 평소 닫혀 있음)도 찾을 수 있도록 Include 옵션 사용.
            var component = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (component == null)
            {
                log.Add($"  [경고] {typeof(T).Name} 인스턴스를 찾지 못했습니다.");
                return;
            }

            // SerializedObject 를 통해 _cancelButton 필드 참조를 안전하게 읽는다.
            var so = new SerializedObject(component);
            so.Update();
            var prop = so.FindProperty("_cancelButton");
            if (prop == null || prop.objectReferenceValue == null)
            {
                log.Add($"  [경고] {typeof(T).Name}: _cancelButton 필드 또는 참조가 null.");
                return;
            }

            // 참조가 Button 직접 타입이 아닐 수도 있으므로(Component 폴백) 둘 다 처리.
            Button btn = prop.objectReferenceValue as Button;
            if (btn == null && prop.objectReferenceValue is Component c)
                btn = c.GetComponent<Button>();

            RectTransform rt = btn != null ? btn.GetComponent<RectTransform>() : null;
            if (rt == null)
            {
                log.Add($"  [경고] {typeof(T).Name}: _cancelButton 에서 RectTransform 추출 실패.");
                return;
            }

            // Undo 기록 후 우상단 코너 고정 배치 값을 적용.
            Undo.RecordObject(rt, $"Fix CancelButton of {typeof(T).Name}");
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -10f);
            rt.sizeDelta        = new Vector2(100f, 80f);
            EditorUtility.SetDirty(rt);

            log.Add($"  {typeof(T).Name}._cancelButton: anchor=(1,1), pivot=(1,1), pos=(-10,-10), size=(100,80)");
        }

        // --------------------------------------------------------------------
        // DeclinedConfirmButton 복구
        // --------------------------------------------------------------------

        /// <summary>
        /// RematchRequestPopup 의 _declinedConfirmButton RectTransform 을
        /// DeclinedPanel 하단 중앙 배치로 복구한다.
        ///
        /// 목표값(거절 알림 팝업의 "확인" 버튼, GO명 "ButtonArea"):
        ///   anchorMin = (0.5,0), anchorMax = (0.5,0), pivot = (0.5,0),
        ///   anchoredPosition = (0,15), sizeDelta = (212,88)
        /// 다른 버튼(_acceptButton / _declineButton)과 동일한 크기(212×88)로 맞춘다.
        /// </summary>
        private static void FixDeclinedConfirmButton(List<string> log)
        {
            log.Add("\n[DeclinedConfirmButton 복구]");

            // 거절 알림 팝업은 평소 비활성 상태이므로 Include 로 탐색.
            var popup = Object.FindFirstObjectByType<RematchRequestPopup>(FindObjectsInactive.Include);
            if (popup == null)
            {
                log.Add("  [경고] RematchRequestPopup 인스턴스를 찾지 못했습니다.");
                return;
            }

            // SerializedObject 를 통해 _declinedConfirmButton 필드 참조를 안전하게 읽는다.
            var so = new SerializedObject(popup);
            so.Update();
            var prop = so.FindProperty("_declinedConfirmButton");
            if (prop == null || prop.objectReferenceValue == null)
            {
                log.Add("  [경고] RematchRequestPopup: _declinedConfirmButton 필드 또는 참조가 null.");
                return;
            }

            // Button 직접 타입이 아닐 수도 있으므로 Component 폴백 처리.
            Button btn = prop.objectReferenceValue as Button;
            if (btn == null && prop.objectReferenceValue is Component c)
                btn = c.GetComponent<Button>();

            RectTransform rt = btn != null ? btn.GetComponent<RectTransform>() : null;
            if (rt == null)
            {
                log.Add("  [경고] RematchRequestPopup: _declinedConfirmButton 에서 RectTransform 추출 실패.");
                return;
            }

            // Undo 기록 후 하단 중앙 배치 값을 적용.
            Undo.RecordObject(rt, "Fix DeclinedConfirmButton of RematchRequestPopup");
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 15f);
            rt.sizeDelta        = new Vector2(212f, 88f);
            EditorUtility.SetDirty(rt);

            log.Add("  RematchRequestPopup._declinedConfirmButton: anchor=(0.5,0), pivot=(0.5,0), pos=(0,15), size=(212,88)");
        }

        // ====================================================================
        // 1) UIColorConfig 에셋 생성/로드
        // ====================================================================

        /// <summary>
        /// Resources/Config/UIColorConfig.asset 을 보장한다.
        /// 이미 존재하면 그대로 로드, 없으면 새로 생성한다.
        /// 필요한 폴더(Resources, Resources/Config)도 자동 생성한다.
        /// </summary>
        private static UIColorConfig EnsureColorConfigAsset()
        {
            // 기존 에셋이 있으면 그대로 반환 — 색상값을 임의로 덮어쓰지 않는다.
            var existing = AssetDatabase.LoadAssetAtPath<UIColorConfig>(ConfigAssetPath);
            if (existing != null)
            {
                Debug.Log($"[FixUIColorAndLayout] 기존 UIColorConfig 사용: {ConfigAssetPath}");
                return existing;
            }

            // 폴더 트리 생성
            if (!AssetDatabase.IsValidFolder(ResourcesDir))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ConfigDir))
                AssetDatabase.CreateFolder(ResourcesDir, "Config");

            // 새 ScriptableObject 인스턴스 생성 + 에셋으로 저장
            var newConfig = ScriptableObject.CreateInstance<UIColorConfig>();
            AssetDatabase.CreateAsset(newConfig, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FixUIColorAndLayout] UIColorConfig 신규 생성: {ConfigAssetPath}");
            return newConfig;
        }

        // ====================================================================
        // 2) RectTransform 앵커 일괄 변경
        // ====================================================================

        /// <summary>
        /// 9개 TC에 해당하는 RectTransform 앵커/피벗/사이즈를 일괄 변경한다.
        /// 각 변경은 Undo.RecordObject로 기록되어 Ctrl+Z 복구가 가능하다.
        /// </summary>
        private static void ApplyRectTransformLayouts(List<string> log)
        {
            log.Add("\n[RectTransform 앵커 변경]");

            // ----- GameEndUI 자식 3개 (ResultText / RestartButton / LobbyButton) -----
            var gameEndUI = Object.FindFirstObjectByType<GameEndUI>(FindObjectsInactive.Include);
            if (gameEndUI != null)
            {
                // SerializedObject로 _resultText / _restartButton / _backToLobbyButton 필드를 안전하게 조회
                var so = new SerializedObject(gameEndUI);
                so.Update();

                TrySetAnchorsFromObjectField(so, "_resultText",
                    anchorMin: new Vector2(0.1f, 0.62f),
                    anchorMax: new Vector2(0.9f, 0.75f),
                    pivot:     new Vector2(0.5f, 0.5f),
                    log: log, label: "ResultText");

                TrySetAnchorsFromObjectField(so, "_restartButton",
                    anchorMin: new Vector2(0.15f, 0.43f),
                    anchorMax: new Vector2(0.85f, 0.55f),
                    pivot:     new Vector2(0.5f, 0.5f),
                    log: log, label: "RestartButton");

                TrySetAnchorsFromObjectField(so, "_backToLobbyButton",
                    anchorMin: new Vector2(0.15f, 0.29f),
                    anchorMax: new Vector2(0.85f, 0.41f),
                    pivot:     new Vector2(0.5f, 0.5f),
                    log: log, label: "LobbyButton");
            }
            else
            {
                log.Add("[경고] GameEndUI 컴포넌트를 찾지 못했습니다.");
            }

            // ----- SettingsButton (GameHudUI._settingsButton) -----
            var gameHudUI = Object.FindFirstObjectByType<GameHudUI>(FindObjectsInactive.Include);
            if (gameHudUI != null)
            {
                var so = new SerializedObject(gameHudUI);
                so.Update();

                TrySetAnchorsFromObjectField(so, "_settingsButton",
                    anchorMin: new Vector2(0.87f, 0.05f),
                    anchorMax: new Vector2(0.99f, 0.95f),
                    pivot:     new Vector2(0.5f, 0.5f),
                    log: log, label: "SettingsButton");
            }
            else
            {
                log.Add("[경고] GameHudUI 컴포넌트를 찾지 못했습니다.");
            }

            // ----- BuildingPanel (BuildingPlacementUI._popup 의 첫 번째 자식) -----
            var buildingPlacementUI = Object.FindFirstObjectByType<BuildingPlacementUI>(FindObjectsInactive.Include);
            if (buildingPlacementUI != null)
            {
                // _popup 슬롯은 AnimatedPanel — 그 자신의 RectTransform이 패널 컨테이너인 경우가 일반적.
                // 단, "BuildingPopup outer(전체화면)" 안에 다시 "BuildingPanel(하단 40%)" 이 들어있는 구조이므로
                // _popup(AnimatedPanel)이 가리키는 transform이 곧 outer일 수 있고, 그 안쪽 컨테이너는 자식이다.
                // 따라서 _popup의 자식 중 'BuildingPanel' 같은 이름의 컨테이너를 찾는다.
                Transform popupTransform = GetAnimatedPanelTransform(buildingPlacementUI, "_popup");
                Transform inner = FindInnerPanelChild(popupTransform, "BuildingPanel");
                if (inner != null)
                {
                    ApplyBottomFortyPercent(inner.GetComponent<RectTransform>(), "BuildingPanel", log);
                }
                else
                {
                    log.Add("[경고] BuildingPanel 내부 컨테이너를 찾지 못했습니다.");
                }
            }
            else
            {
                log.Add("[경고] BuildingPlacementUI 컴포넌트를 찾지 못했습니다.");
            }

            // ----- ProductionPanel (ProductionPanelUI._popup 의 자식) -----
            var productionPanelUI = Object.FindFirstObjectByType<ProductionPanelUI>(FindObjectsInactive.Include);
            if (productionPanelUI != null)
            {
                Transform popupTransform = GetAnimatedPanelTransform(productionPanelUI, "_popup");
                Transform inner = FindInnerPanelChild(popupTransform, "ProductionPanel");
                if (inner != null)
                {
                    ApplyBottomFortyPercent(inner.GetComponent<RectTransform>(), "ProductionPanel", log);
                }
                else
                {
                    log.Add("[경고] ProductionPanel 내부 컨테이너를 찾지 못했습니다.");
                }
            }
            else
            {
                log.Add("[경고] ProductionPanelUI 컴포넌트를 찾지 못했습니다.");
            }

            // ----- ActionPanel (BuildingActionPanelUI._popup 의 자식) -----
            var actionPanelUI = Object.FindFirstObjectByType<BuildingActionPanelUI>(FindObjectsInactive.Include);
            if (actionPanelUI != null)
            {
                Transform popupTransform = GetAnimatedPanelTransform(actionPanelUI, "_popup");
                Transform inner = FindInnerPanelChild(popupTransform, "ActionPanel");
                if (inner != null)
                {
                    ApplyBottomFortyPercent(inner.GetComponent<RectTransform>(), "ActionPanel", log);
                }
                else
                {
                    log.Add("[경고] ActionPanel 내부 컨테이너를 찾지 못했습니다.");
                }
            }
            else
            {
                log.Add("[경고] BuildingActionPanelUI 컴포넌트를 찾지 못했습니다.");
            }

            // ----- RematchRequestPopup 내부 _requestPanel / _declinedPanel -----
            var rematchPopup = Object.FindFirstObjectByType<RematchRequestPopup>(FindObjectsInactive.Include);
            if (rematchPopup != null)
            {
                var so = new SerializedObject(rematchPopup);
                so.Update();

                TrySetAnchorsFromObjectField(so, "_requestPanel",
                    anchorMin: new Vector2(0.1f, 0.3f),
                    anchorMax: new Vector2(0.9f, 0.7f),
                    pivot:     new Vector2(0.5f, 0.5f),
                    log: log, label: "RequestPanel");

                TrySetAnchorsFromObjectField(so, "_declinedPanel",
                    anchorMin: new Vector2(0.1f, 0.3f),
                    anchorMax: new Vector2(0.9f, 0.7f),
                    pivot:     new Vector2(0.5f, 0.5f),
                    log: log, label: "DeclinedPanel");
            }
            else
            {
                log.Add("[경고] RematchRequestPopup 컴포넌트를 찾지 못했습니다.");
            }

            // ================================================================
            // Round 2 — 내부 자식 요소 비율화 + TMP Auto Size 적용
            // ================================================================
            //
            // 왜 Round 2가 필요한가?
            //   Round 1에서 외부 컨테이너(BuildingPanel / ProductionPanel / ActionPanel /
            //   GameEndPanel 자식 / RematchRequestPopup 의 두 패널)의 anchor 만 비율로 바꿨다.
            //   그러나 그 내부의 버튼/텍스트 자식들은 여전히 절대 픽셀(sizeDelta 고정)이라
            //   부모가 커져도 따라 늘어나지 않아 실기기 TC가 다음과 같이 FAIL 했다:
            //     - BP-001/BP-002 : 건물 버튼 그리드가 좁은 폭에 고정
            //     - PRD-001       : 유닛 카드 영역이 작아 보임
            //     - BAP-001       : 액션 패널 내부 요소가 한쪽으로 몰림
            //     - SET-007/END-001 : 결과 텍스트/버튼 레이블의 글자 크기가 그대로
            //     - MULTI-END-002 : 재경기 팝업 내부 텍스트/버튼 영역 미적용
            //
            // 아래 4 단계는 각 패널의 내부 자식 트리를 순회하면서:
            //   1) RectTransform 의 anchorMin/Max 를 0~1 비율(stretch)로 전환
            //   2) Image/Button 등은 sizeDelta 를 (0,0) 으로 리셋해 부모 크기를 따라가게 함
            //   3) TextMeshProUGUI 는 enableAutoSizing = true 로 글자 크기 자동 스케일
            // ================================================================

            // ----- Step 12) 3개 건물 팝업 내부 자식 비율화 -----
            ApplyBuildingPopupInnerLayout(buildingPlacementUI, productionPanelUI, actionPanelUI, log);

            // ----- Step 13) GameEndPanel 내부 텍스트 Auto Size -----
            ApplyGameEndInnerAutoSize(gameEndUI, log);

            // ----- Step 14) RematchRequestPopup 내부 자식 비율화 + Auto Size -----
            ApplyRematchPopupInnerLayout(rematchPopup, log);

            // ----- Step 15) BuildingPlacementUI 비용 텍스트 좌측 치우침 보정 -----
            ApplyBuildingCostTextAlignment(buildingPlacementUI, log);
        }

        // ====================================================================
        // 3) UI 컴포넌트 _colorConfig 자동 연결
        // ====================================================================

        /// <summary>
        /// 각 UI 컴포넌트의 _colorConfig SerializeField에 인자로 받은 ScriptableObject 에셋을 할당한다.
        /// SerializedObject.FindProperty + ApplyModifiedProperties 패턴으로 Undo/Dirty 처리를 안전하게 한다.
        /// </summary>
        private static void ApplyColorConfigToComponents(UIColorConfig config, List<string> log)
        {
            log.Add("\n[UIColorConfig 슬롯 연결]");

            AssignColorConfigField<GameEndUI>(config, log);
            AssignColorConfigField<GameHudUI>(config, log);
            AssignColorConfigField<BuildingPlacementUI>(config, log);
            // BuildingPanelBase 상속 클래스: ProductionPanelUI / BuildingActionPanelUI
            // protected _colorConfig 도 SerializedObject에서 동일하게 FindProperty로 접근 가능하다.
            AssignColorConfigField<ProductionPanelUI>(config, log);
            AssignColorConfigField<BuildingActionPanelUI>(config, log);
            AssignColorConfigField<ConfirmPopup>(config, log);
        }

        /// <summary>
        /// 타입 T의 모든 인스턴스를 찾아 _colorConfig 필드에 config 에셋을 연결한다.
        /// 비활성 오브젝트도 포함. _colorConfig 필드가 없는 컴포넌트는 자동 스킵.
        /// </summary>
        private static void AssignColorConfigField<T>(UIColorConfig config, List<string> log)
            where T : MonoBehaviour
        {
            var instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (instances == null || instances.Length == 0)
            {
                log.Add($"[스킵] {typeof(T).Name} 인스턴스 없음.");
                return;
            }

            foreach (var inst in instances)
            {
                var so = new SerializedObject(inst);
                so.Update();
                var prop = so.FindProperty("_colorConfig");
                if (prop == null)
                {
                    log.Add($"[스킵] {typeof(T).Name}: _colorConfig 필드 없음.");
                    continue;
                }

                Undo.RecordObject(inst, $"Assign UIColorConfig to {typeof(T).Name}");
                prop.objectReferenceValue = config;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(inst);
                log.Add($"  {typeof(T).Name}._colorConfig = UIColorConfig.asset (연결 완료)");
            }
        }

        // ====================================================================
        // RectTransform 헬퍼
        // ====================================================================

        /// <summary>
        /// SerializedObject에서 지정 이름의 ObjectReference 필드를 찾아 그 대상의 RectTransform 앵커/피벗을 설정.
        /// 필드가 GameObject/Component 어떤 타입이든 RectTransform을 추출하여 적용한다.
        /// sizeDelta = (0, 0), anchoredPosition = (0, 0) 으로 통일.
        /// </summary>
        private static void TrySetAnchorsFromObjectField(SerializedObject so, string fieldName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            List<string> log, string label)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null || prop.objectReferenceValue == null)
            {
                log.Add($"  [경고] {label}: SerializedObject에서 {fieldName} 필드를 찾지 못했습니다.");
                return;
            }

            // 필드 대상이 Component 인지 GameObject 인지 따라 RectTransform 추출
            RectTransform rt = null;
            if (prop.objectReferenceValue is RectTransform rect)
            {
                rt = rect;
            }
            else if (prop.objectReferenceValue is Component comp)
            {
                rt = comp.GetComponent<RectTransform>();
            }
            else if (prop.objectReferenceValue is GameObject go)
            {
                rt = go.GetComponent<RectTransform>();
            }

            if (rt == null)
            {
                log.Add($"  [경고] {label}: {fieldName} 의 대상에서 RectTransform을 추출하지 못했습니다.");
                return;
            }

            ApplyAnchors(rt, anchorMin, anchorMax, pivot, Vector2.zero, Vector2.zero, label, log);
        }

        /// <summary>
        /// 하단 40% 영역 패널의 표준 앵커를 적용한다.
        /// anchorMin=(0,0), anchorMax=(1, 0.4), pivot=(0.5, 0), sizeDelta=(0,0), anchoredPos=(0,0).
        /// 슬라이드 애니메이션과 호환됨.
        /// </summary>
        private static void ApplyBottomFortyPercent(RectTransform rt, string label, List<string> log)
        {
            if (rt == null)
            {
                log.Add($"  [경고] {label}: RectTransform이 null이라 적용 불가.");
                return;
            }
            ApplyAnchors(rt,
                anchorMin: new Vector2(0f, 0f),
                anchorMax: new Vector2(1f, 0.4f),
                pivot:     new Vector2(0.5f, 0f),
                sizeDelta: Vector2.zero,
                anchoredPos: Vector2.zero,
                label: label,
                log: log);
        }

        /// <summary>
        /// RectTransform의 anchorMin / anchorMax / pivot / sizeDelta / anchoredPosition 을 일괄 변경하고
        /// Undo 기록과 Dirty 표시를 수행한다.
        /// </summary>
        private static void ApplyAnchors(RectTransform rt,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 sizeDelta, Vector2 anchoredPos,
            string label, List<string> log)
        {
            Undo.RecordObject(rt, $"Set Anchors {label}");
            rt.anchorMin       = anchorMin;
            rt.anchorMax       = anchorMax;
            rt.pivot           = pivot;
            rt.sizeDelta       = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            EditorUtility.SetDirty(rt);
            log.Add($"  {label}: anchorMin={anchorMin}, anchorMax={anchorMax}, pivot={pivot}, sizeDelta={sizeDelta}");
        }

        /// <summary>
        /// 컴포넌트 안의 _popup SerializedField(AnimatedPanel 또는 GameObject)에서 Transform을 추출한다.
        /// </summary>
        private static Transform GetAnimatedPanelTransform(Component owner, string fieldName)
        {
            var so = new SerializedObject(owner);
            so.Update();
            var prop = so.FindProperty(fieldName);
            if (prop == null || prop.objectReferenceValue == null) return null;

            if (prop.objectReferenceValue is Component comp) return comp.transform;
            if (prop.objectReferenceValue is GameObject go)  return go.transform;
            return null;
        }

        /// <summary>
        /// 부모 transform의 자식들 중 이름이 candidateName 과 일치하거나(대소문자 무시) 부분 일치하는 첫 번째
        /// 자식을 찾아 반환. 없으면 자식이 1개일 때 그것을 반환(폴백).
        /// 이는 BuildingPopup outer 아래에 "BuildingPanel" 같은 단일 컨테이너가 있는 일반적인 구조를 가정.
        /// </summary>
        private static Transform FindInnerPanelChild(Transform parent, string candidateName)
        {
            if (parent == null) return null;

            // 1) 완전 일치
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (string.Equals(c.name, candidateName, System.StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            // 2) 부분 일치 (Background 제외)
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name.IndexOf(candidateName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }
            // 3) Background 만 있고 본체 자식이 1개라면 그것을 사용 (전체화면이 아닌 자식 우선)
            Transform fallback = null;
            int candidateCount = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                // "Background" 이름은 전체화면 오버레이로 간주하여 제외
                if (c.name.IndexOf("Background", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                fallback = c;
                candidateCount++;
            }
            if (candidateCount == 1) return fallback;

            return null;
        }

        // ====================================================================
        // Round 2 — Step 12 ~ 15 구현
        // ====================================================================

        // --------------------------------------------------------------------
        // Step 12) BuildingPopup / ProductionPopup / BuildingActionPanel
        //          내부 자식 RectTransform 비율화 + GridLayoutGroup cellSize 비율 보정
        // --------------------------------------------------------------------

        /// <summary>
        /// 3개의 건물 관련 팝업 내부 텍스트에 TMP Auto Size 를 적용한다.
        ///
        /// Round 1에서 외부 컨테이너(BuildingPanel/ProductionPanel/ActionPanel)의 anchor 를
        /// 비율 기반으로 변경했다. 버튼/이미지의 RectTransform 을 stretch 로 바꾸면
        /// 부모 전체를 덮어버려 레이아웃이 망가지므로 텍스트 Auto Size 만 적용한다.
        /// 버튼 배치/크기는 기존 Inspector 설정 그대로 유지한다.
        /// </summary>
        private static void ApplyBuildingPopupInnerLayout(
            BuildingPlacementUI buildingPlacementUI,
            ProductionPanelUI productionPanelUI,
            BuildingActionPanelUI actionPanelUI,
            List<string> log)
        {
            log.Add("\n[Step 12] 건물 팝업 내부 텍스트 Auto Size 적용");

            // === BuildingPlacementUI ===
            // 비용 텍스트(List<TMP>)에만 Auto Size 적용.
            // 버튼/아이콘 RectTransform 은 건드리지 않는다 — stretch 하면 부모 전체를 덮어 레이아웃 파괴.
            if (buildingPlacementUI != null)
            {
                EnableAutoSizeOnTextList(buildingPlacementUI, "_buildingCostTexts", "BuildingCostText", log);
            }
            else
            {
                log.Add("  [경고] BuildingPlacementUI 없음 — Step 12 (Building) 스킵");
            }

            // === ProductionPanelUI ===
            // 텍스트 요소에만 Auto Size 적용. 버튼/이미지/LayoutGroup 은 그대로 둔다.
            if (productionPanelUI != null)
            {
                EnableAutoSizeOnSingleText(productionPanelUI, "_headerText",        "Production.HeaderText",        log);
                EnableAutoSizeOnSingleText(productionPanelUI, "_demolishRefundText","Production.DemolishRefundText",log);
                EnableAutoSizeOnTextList  (productionPanelUI, "_unitCostTexts",     "UnitCostText",                 log);
                EnableAutoSizeOnSingleText(productionPanelUI, "_goldText",          "Production.GoldText",          log);
                EnableAutoSizeOnSingleText(productionPanelUI, "_populationText",    "Production.PopulationText",    log);
                EnableAutoSizeOnSingleText(productionPanelUI, "_upgradeCostText",   "Production.UpgradeCostText",   log);
            }
            else
            {
                log.Add("  [경고] ProductionPanelUI 없음 — Step 12 (Production) 스킵");
            }

            // === BuildingActionPanelUI ===
            // 텍스트 요소에만 Auto Size 적용.
            if (actionPanelUI != null)
            {
                EnableAutoSizeOnSingleText(actionPanelUI, "_headerText",        "Action.HeaderText",        log);
                EnableAutoSizeOnSingleText(actionPanelUI, "_demolishRefundText","Action.DemolishRefundText",log);
            }
            else
            {
                log.Add("  [경고] BuildingActionPanelUI 없음 — Step 12 (Action) 스킵");
            }
        }

        // --------------------------------------------------------------------
        // Step 13) GameEndPanel 내부 텍스트 Auto Size
        // --------------------------------------------------------------------

        /// <summary>
        /// GameEndUI 의 SerializeField 를 기반으로 결과 텍스트와 각 버튼 내부 레이블에
        /// TMP Auto Size 를 활성화한다.
        ///
        /// 처리 대상:
        ///   - _resultText : "승리!" / "패배!" 표시
        ///   - _restartButton 의 자식 TMP : 다시하기 레이블 (이름이 무엇이든 첫 번째 TMP 사용)
        ///   - _backToLobbyButton 의 자식 TMP : 로비로 돌아가기 레이블
        ///   - _countdownText : "30초 후 로비로 돌아갑니다."
        ///   - _restartButtonText : 코드에서 "요청 중..." 으로 바뀌는 텍스트
        /// </summary>
        private static void ApplyGameEndInnerAutoSize(GameEndUI gameEndUI, List<string> log)
        {
            log.Add("\n[Step 13] GameEndPanel 내부 텍스트 Auto Size");

            if (gameEndUI == null)
            {
                log.Add("  [경고] GameEndUI 없음 — Step 13 스킵");
                return;
            }

            // _resultText : 직접 참조하는 결과 텍스트
            EnableAutoSizeOnSingleText(gameEndUI, "_resultText", "GameEnd.ResultText", log);

            // _restartButtonText : 코드에서 "요청 중..." 으로 바뀌는 텍스트
            EnableAutoSizeOnSingleText(gameEndUI, "_restartButtonText", "GameEnd.RestartButtonText", log);

            // _countdownText : 30초 카운트다운 표시 텍스트
            EnableAutoSizeOnSingleText(gameEndUI, "_countdownText", "GameEnd.CountdownText", log);

            // 각 버튼 내부의 TMP 자식도 Auto Size 활성화 — 일부 프로젝트에서는 _restartButtonText 미연결 가능성 있어 안전 처리.
            EnableAutoSizeOnChildTextOfButtonField(gameEndUI, "_restartButton", "GameEnd.RestartButton.ChildText", log);
            EnableAutoSizeOnChildTextOfButtonField(gameEndUI, "_backToLobbyButton", "GameEnd.LobbyButton.ChildText", log);
        }

        // --------------------------------------------------------------------
        // Step 14) RematchRequestPopup 내부 자식 비율화 + Auto Size
        // --------------------------------------------------------------------

        /// <summary>
        /// 재경기 요청 팝업/거절 알림 팝업 내부 텍스트에 TMP Auto Size 를 적용한다.
        ///
        /// 버튼 RectTransform 을 stretch 하면 부모 패널 전체를 덮어 레이아웃이 파괴되므로
        /// 텍스트 Auto Size 만 적용하고 버튼 배치는 기존 Inspector 설정 그대로 유지한다.
        ///
        /// RematchRequestPopup 의 SerializeField:
        ///   _requestPanel  (GameObject) : 수락/거절 패널
        ///   _declinedPanel (GameObject) : 거절 알림 패널
        /// </summary>
        private static void ApplyRematchPopupInnerLayout(RematchRequestPopup rematchPopup, List<string> log)
        {
            log.Add("\n[Step 14] RematchRequestPopup 내부 텍스트 Auto Size 적용");

            if (rematchPopup == null)
            {
                log.Add("  [경고] RematchRequestPopup 없음 — Step 14 스킵");
                return;
            }

            var so = new SerializedObject(rematchPopup);
            so.Update();

            // 두 패널 내부의 모든 TMP 텍스트에 Auto Size 적용.
            // 버튼 RectTransform 은 stretch 하지 않는다 — 레이아웃 파괴 원인.
            EnableAutoSizeOnAllTextsUnder(so, "_requestPanel",  "Rematch.RequestPanel.AllTexts",  log);
            EnableAutoSizeOnAllTextsUnder(so, "_declinedPanel", "Rematch.DeclinedPanel.AllTexts", log);
        }

        // --------------------------------------------------------------------
        // Step 15) BuildingPlacementUI 비용 텍스트 정렬 보정
        // --------------------------------------------------------------------

        /// <summary>
        /// BuildingPlacementUI._buildingCostTexts 리스트의 각 TextMeshProUGUI 에 대해
        /// 가로 정렬을 Center 로 통일하고, anchoredPosition.x = 0 으로 보정해
        /// 비용 숫자가 좌측으로 치우치는 BP-002 FAIL 을 해결한다.
        /// </summary>
        private static void ApplyBuildingCostTextAlignment(BuildingPlacementUI buildingPlacementUI, List<string> log)
        {
            log.Add("\n[Step 15] BuildingPlacementUI 비용 텍스트 정렬 보정");

            if (buildingPlacementUI == null)
            {
                log.Add("  [경고] BuildingPlacementUI 없음 — Step 15 스킵");
                return;
            }

            var so = new SerializedObject(buildingPlacementUI);
            so.Update();

            var prop = so.FindProperty("_buildingCostTexts");
            if (prop == null || !prop.isArray)
            {
                log.Add("  [경고] _buildingCostTexts 필드를 찾지 못했습니다.");
                return;
            }

            for (int i = 0; i < prop.arraySize; i++)
            {
                var elementProp = prop.GetArrayElementAtIndex(i);
                if (elementProp == null || elementProp.objectReferenceValue == null) continue;

                // TextMeshProUGUI 추출 — 직접 참조 또는 Component 폴백.
                TextMeshProUGUI tmp = elementProp.objectReferenceValue as TextMeshProUGUI;
                if (tmp == null && elementProp.objectReferenceValue is Component compRef)
                    tmp = compRef.GetComponent<TextMeshProUGUI>();

                if (tmp == null) continue;

                Undo.RecordObject(tmp, "Set BuildingCostText alignment to Center");

                // 가로 가운데 + 세로 가운데 정렬로 통일 (중앙).
                tmp.alignment = TextAlignmentOptions.Center;

                EditorUtility.SetDirty(tmp);

                // anchoredPosition.x 보정 — 부모 기준 0 으로 맞춤.
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Undo.RecordObject(rt, "Set BuildingCostText anchoredPosition.x = 0");
                    Vector2 pos = rt.anchoredPosition;
                    pos.x = 0f;
                    rt.anchoredPosition = pos;
                    EditorUtility.SetDirty(rt);
                }

                log.Add($"  {tmp.gameObject.name} 정렬 = Center, anchoredPosition.x = 0");
            }
        }

        // ====================================================================
        // Round 2 공용 헬퍼 — RectTransform 비율화 / TMP Auto Size 활성화
        // ====================================================================

        /// <summary>
        /// SerializedObject 의 List 필드(예: _buildingButtons)에 들어 있는 모든 항목의
        /// RectTransform 을 부모 기준 비율(stretch)로 전환한다.
        /// 각 항목은 0~1 의 앵커를 가지며 sizeDelta = (0,0) 으로 부모 크기를 그대로 따른다.
        /// 단, 부모가 GridLayoutGroup 이면 GridLayoutGroup 이 직접 자식 RectTransform 을
        /// 매 프레임 덮어쓰므로 stretch 가 무시되어 효과가 없다 → 이 경우는 자식의 sizeDelta 만
        /// 손대지 않고 그대로 두고, 부모 그리드 자체는 AdjustParentGridOfList 에서 별도 처리한다.
        /// </summary>
        private static void StretchListOfComponents<T>(Object owner, string fieldName, string labelPrefix, List<string> log)
            where T : Component
        {
            var so = new SerializedObject(owner);
            so.Update();
            var prop = so.FindProperty(fieldName);
            if (prop == null || !prop.isArray)
            {
                log.Add($"  [경고] {fieldName} 필드를 찾지 못했습니다.");
                return;
            }

            for (int i = 0; i < prop.arraySize; i++)
            {
                var elementProp = prop.GetArrayElementAtIndex(i);
                if (elementProp == null || elementProp.objectReferenceValue == null) continue;

                RectTransform rt = ExtractRectTransform(elementProp.objectReferenceValue);
                if (rt == null) continue;

                // 부모가 LayoutGroup(예: GridLayoutGroup)이면 자식 RectTransform 의 anchor/size 를
                // 매 프레임 LayoutGroup 이 덮어쓰므로 굳이 바꾸지 않는다. LayoutGroup 의 셀 자체는
                // AdjustParentGridOfList 에서 별도 비율 처리한다.
                if (HasLayoutGroupParent(rt))
                {
                    log.Add($"  {labelPrefix}[{i}]: 부모 LayoutGroup → stretch 스킵 (cellSize는 부모에서 보정)");
                    continue;
                }

                ApplyStretchAnchors(rt, $"{labelPrefix}[{i}]", log);
            }
        }

        /// <summary>
        /// 배열 필드(Image[] _queueSlotImages 등) 버전. StretchListOfComponents 와 동일 처리.
        /// </summary>
        private static void StretchArrayOfComponents<T>(Object owner, string fieldName, string labelPrefix, List<string> log)
            where T : Component
        {
            // 내부적으로 List<>이든 Array든 SerializedProperty.isArray 는 동일하게 동작.
            StretchListOfComponents<T>(owner, fieldName, labelPrefix, log);
        }

        /// <summary>
        /// 단일 컴포넌트 필드(_cancelButton, _demolishButton 등)의 RectTransform 을 stretch 로 전환.
        /// </summary>
        private static void StretchSingleComponent<T>(Object owner, string fieldName, string label, List<string> log)
            where T : Component
        {
            var so = new SerializedObject(owner);
            so.Update();
            var prop = so.FindProperty(fieldName);
            if (prop == null || prop.objectReferenceValue == null)
            {
                log.Add($"  [경고] {label}: {fieldName} 필드 또는 참조가 null.");
                return;
            }

            RectTransform rt = ExtractRectTransform(prop.objectReferenceValue);
            if (rt == null)
            {
                log.Add($"  [경고] {label}: RectTransform 추출 실패.");
                return;
            }

            if (HasLayoutGroupParent(rt))
            {
                log.Add($"  {label}: 부모 LayoutGroup → stretch 스킵.");
                return;
            }

            ApplyStretchAnchors(rt, label, log);
        }

        /// <summary>
        /// 리스트의 첫 항목의 부모(=Grid 컨테이너)를 찾아, 부모 자체의 RectTransform 을 stretch 로 전환하고
        /// 만약 GridLayoutGroup 이 부착되어 있다면 cellSize 를 부모 크기에 비례하도록 조정한다.
        /// (정확한 비례 계산은 런타임에 부모 크기에 따라 달라지므로 여기서는 cellSize 의 x/y 를
        /// 부모 RectTransform.rect 의 일정 비율 — 가로 1/3, 세로 1/2 — 로 설정해 합리적 기본값을 잡는다.)
        /// </summary>
        private static void AdjustParentGridOfList<T>(Object owner, string fieldName, string label, List<string> log)
            where T : Component
        {
            var so = new SerializedObject(owner);
            so.Update();
            var prop = so.FindProperty(fieldName);
            if (prop == null || !prop.isArray || prop.arraySize == 0)
            {
                log.Add($"  [경고] {label}: {fieldName} 비어있어 부모 조회 불가.");
                return;
            }

            // 첫 항목의 부모를 가져온다.
            var firstElem = prop.GetArrayElementAtIndex(0);
            if (firstElem == null || firstElem.objectReferenceValue == null) return;

            RectTransform childRt = ExtractRectTransform(firstElem.objectReferenceValue);
            if (childRt == null || childRt.parent == null) return;

            RectTransform parentRt = childRt.parent as RectTransform;
            if (parentRt == null)
            {
                log.Add($"  [경고] {label}: 부모가 RectTransform 이 아님.");
                return;
            }

            // 부모 자체를 stretch 로 (외부 컨테이너 안에서 자유롭게 늘어나도록)
            ApplyStretchAnchors(parentRt, $"{label}(parent)", log);

            // 부모에 GridLayoutGroup 이 있으면 cellSize 를 부모 크기에 맞춰 재계산.
            // 정확한 픽셀 값은 런타임 부모 크기에 따라 다르지만, 여기서는 합리적 기본 비율을 잡는다.
            var grid = parentRt.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                Undo.RecordObject(grid, "Adjust GridLayoutGroup cellSize");
                // 부모의 현재 rect 크기를 기준으로 셀 크기를 재계산.
                // rect 가 0 인 경우(아직 레이아웃 안 잡힌 비활성 패널)에는 기본 비율 추정값 사용.
                float w = parentRt.rect.width > 1f ? parentRt.rect.width : 600f;
                float h = parentRt.rect.height > 1f ? parentRt.rect.height : 300f;
                // 한 행에 3개 정도 들어가는 그리드를 기본 가정.
                float cellW = (w - grid.padding.left - grid.padding.right - grid.spacing.x * 2f) / 3f;
                // 두 줄 가정.
                float cellH = (h - grid.padding.top - grid.padding.bottom - grid.spacing.y) / 2f;
                grid.cellSize = new Vector2(Mathf.Max(cellW, 50f), Mathf.Max(cellH, 50f));
                // 일정 행에 고정하지 않고 부모 크기에 자유롭게 흘러가도록 Flexible(=AxisConstraint.Flexible).
                grid.constraint = GridLayoutGroup.Constraint.Flexible;
                EditorUtility.SetDirty(grid);
                log.Add($"  {label}: GridLayoutGroup cellSize=({cellW:F0},{cellH:F0}), constraint=Flexible");
            }
        }

        /// <summary>
        /// 단일 TextMeshProUGUI 필드에 Auto Size 를 활성화하고 min/max 폰트 크기를 설정한다.
        /// </summary>
        private static void EnableAutoSizeOnSingleText(Object owner, string fieldName, string label, List<string> log)
        {
            var so = new SerializedObject(owner);
            so.Update();
            var prop = so.FindProperty(fieldName);
            if (prop == null || prop.objectReferenceValue == null)
            {
                log.Add($"  [경고] {label}: {fieldName} 필드 또는 참조가 null.");
                return;
            }

            TextMeshProUGUI tmp = prop.objectReferenceValue as TextMeshProUGUI;
            if (tmp == null && prop.objectReferenceValue is Component compRef)
                tmp = compRef.GetComponent<TextMeshProUGUI>();

            if (tmp == null)
            {
                log.Add($"  [경고] {label}: TextMeshProUGUI 추출 실패.");
                return;
            }

            EnableAutoSize(tmp, label, log);
        }

        /// <summary>
        /// List&lt;TextMeshProUGUI&gt; 필드의 모든 요소에 Auto Size 활성화.
        /// </summary>
        private static void EnableAutoSizeOnTextList(Object owner, string fieldName, string labelPrefix, List<string> log)
        {
            var so = new SerializedObject(owner);
            so.Update();
            var prop = so.FindProperty(fieldName);
            if (prop == null || !prop.isArray)
            {
                log.Add($"  [경고] {fieldName} 필드를 찾지 못했습니다.");
                return;
            }

            for (int i = 0; i < prop.arraySize; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                if (elem == null || elem.objectReferenceValue == null) continue;

                TextMeshProUGUI tmp = elem.objectReferenceValue as TextMeshProUGUI;
                if (tmp == null && elem.objectReferenceValue is Component compRef)
                    tmp = compRef.GetComponent<TextMeshProUGUI>();

                if (tmp == null) continue;
                EnableAutoSize(tmp, $"{labelPrefix}[{i}]", log);
            }
        }

        /// <summary>
        /// 어떤 GameObject/Component 가 가리키는 트리(GetComponentsInChildren) 아래에 있는
        /// 모든 TextMeshProUGUI 컴포넌트에 Auto Size 를 활성화한다.
        /// RematchRequestPopup 의 _requestPanel / _declinedPanel 처럼 GameObject 가 직접 직렬화되어
        /// 자식 텍스트가 여러 개 있을 때 사용.
        /// </summary>
        private static void EnableAutoSizeOnAllTextsUnder(SerializedObject so, string fieldName, string label, List<string> log)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null || prop.objectReferenceValue == null)
            {
                log.Add($"  [경고] {label}: {fieldName} 필드 또는 참조가 null.");
                return;
            }

            Transform root = null;
            if (prop.objectReferenceValue is GameObject go) root = go.transform;
            else if (prop.objectReferenceValue is Component comp) root = comp.transform;

            if (root == null)
            {
                log.Add($"  [경고] {label}: Transform 추출 실패.");
                return;
            }

            // 비활성 자식까지 모두 포함하여 탐색.
            var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            int count = 0;
            foreach (var tmp in texts)
            {
                if (tmp == null) continue;
                EnableAutoSize(tmp, $"{label}/{tmp.gameObject.name}", log);
                count++;
            }

            if (count == 0)
                log.Add($"  {label}: 자식 TMP 텍스트가 발견되지 않음.");
        }

        /// <summary>
        /// 버튼 필드의 대상 Button 의 자식 GameObject 트리에서 첫 번째 TextMeshProUGUI 를 찾아
        /// Auto Size 를 활성화한다. (RestartButton 내부의 "다시하기" 레이블 등)
        /// </summary>
        private static void EnableAutoSizeOnChildTextOfButtonField(Object owner, string fieldName, string label, List<string> log)
        {
            var so = new SerializedObject(owner);
            so.Update();
            var prop = so.FindProperty(fieldName);
            if (prop == null || prop.objectReferenceValue == null)
            {
                log.Add($"  [경고] {label}: {fieldName} 필드 또는 참조가 null.");
                return;
            }

            Button btn = prop.objectReferenceValue as Button;
            if (btn == null && prop.objectReferenceValue is Component compRef)
                btn = compRef.GetComponent<Button>();

            if (btn == null)
            {
                log.Add($"  [경고] {label}: Button 추출 실패.");
                return;
            }

            // 버튼 자식 트리에서 모든 TMP 텍스트를 찾아 Auto Size 활성화.
            var texts = btn.GetComponentsInChildren<TextMeshProUGUI>(true);
            int count = 0;
            foreach (var tmp in texts)
            {
                if (tmp == null) continue;
                EnableAutoSize(tmp, $"{label}/{tmp.gameObject.name}", log);
                count++;
            }

            if (count == 0)
                log.Add($"  {label}: 버튼 내부 TMP 텍스트가 발견되지 않음.");
        }

        /// <summary>
        /// TextMeshProUGUI 의 Auto Size 를 켜고 min=14, max=72 의 안전한 폰트 범위를 지정.
        /// Auto Size 가 켜져 있으면 부모 크기에 따라 텍스트가 자동 스케일링되어 모바일 다양 해상도에 대응.
        /// </summary>
        private static void EnableAutoSize(TextMeshProUGUI tmp, string label, List<string> log)
        {
            if (tmp == null) return;

            Undo.RecordObject(tmp, $"Enable AutoSize {label}");
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14f;
            tmp.fontSizeMax = 72f;
            EditorUtility.SetDirty(tmp);
            log.Add($"  {label}: enableAutoSizing=true (min=14, max=72)");
        }

        /// <summary>
        /// RectTransform 을 anchorMin=(0,0)/anchorMax=(1,1) 의 풀스트레치로 설정하고
        /// offsetMin = offsetMax = (0,0), pivot = (0.5,0.5) 로 통일한다.
        /// 부모의 크기에 정확히 맞춰 늘어나는 표준 자식 RectTransform 형태.
        /// </summary>
        private static void ApplyStretchAnchors(RectTransform rt, string label, List<string> log)
        {
            if (rt == null) return;

            Undo.RecordObject(rt, $"Stretch Anchors {label}");
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            // offsetMin / offsetMax 를 모두 0 으로 설정해 부모와 정확히 일치하도록 만든다.
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(rt);
            log.Add($"  {label}: anchorMin=(0,0), anchorMax=(1,1), offset=0 (stretch)");
        }

        /// <summary>
        /// 임의의 Object 참조에서 RectTransform 추출 (GameObject/Component/RectTransform 모두 지원).
        /// </summary>
        private static RectTransform ExtractRectTransform(Object obj)
        {
            if (obj == null) return null;
            if (obj is RectTransform rt) return rt;
            if (obj is Component comp) return comp.GetComponent<RectTransform>();
            if (obj is GameObject go) return go.GetComponent<RectTransform>();
            return null;
        }

        /// <summary>
        /// 자기 부모(또는 그 이상)에 LayoutGroup(HorizontalLayoutGroup / VerticalLayoutGroup /
        /// GridLayoutGroup)이 부착되어 있는지 검사. LayoutGroup 자식의 RectTransform 은
        /// 매 프레임 부모가 덮어쓰므로 anchor 변경이 의미 없음을 판정하는 데 사용.
        /// </summary>
        private static bool HasLayoutGroupParent(RectTransform rt)
        {
            if (rt == null) return false;
            var parent = rt.parent;
            if (parent == null) return false;
            return parent.GetComponent<LayoutGroup>() != null;
        }

        // ====================================================================
        // 공통 헬퍼
        // ====================================================================

        /// <summary>
        /// 지정 경로의 씬으로 복구. 경로가 비어있거나 파일이 없으면 아무 동작도 하지 않음.
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
