// ============================================================================
// SetupDifficultySelectView.cs (Editor 전용, 1회성 씬 조립 도구)
// Lobby.unity 씬에 싱글플레이 난이도 선택 화면(DifficultySelectView)을
// 자동으로 생성하고, 버튼 4개를 배선한 뒤 BattleRootView에 연결한다.
//
// 사용법:
//   1) Lobby.unity 씬을 연다.
//   2) Unity 상단 메뉴 → Hexiege/Setup/난이도 선택 UI 생성
//
// 동작:
//   - 씬에서 BattleRootView를 찾는다(없으면 중단).
//   - 이미 DifficultySelectView가 있으면 그 오브젝트를 선택만 하고 중단(중복 방지).
//   - BattleMainView의 부모를 기준으로 DifficultySelectView GO를 생성한다.
//     (BattleMainView가 없으면 BattleRootView를 부모로 사용)
//   - 화면 전체를 덮는 RectTransform + CanvasGroup(초기 숨김) + DifficultySelectView 부착.
//   - 쉬움/보통/어려움/뒤로 버튼 4개를 세로로 배치하고 TMP 텍스트를 붙인다.
//   - SerializedObject로 DifficultySelectView의 버튼 필드와
//     BattleRootView의 _difficultySelectView 필드를 자동 배선한다.
//   - 씬을 더티 표시 후 저장한다.
//
// 왜 Editor 스크립트로?
//   씬에 GameObject를 만들고 private SerializeField를 배선하는 작업은
//   런타임 코드로 불가능하며, 수동 작업은 실수가 잦다.
//   1회성으로 자동화하여 일관된 결과를 보장한다.
//
// 주의:
//   - 모든 GO 생성/컴포넌트 추가/부모 변경에 Undo를 등록하여 Ctrl+Z로 되돌릴 수 있다.
//   - 프로젝트 전체가 TextMeshPro 기반이므로 텍스트는 TextMeshProUGUI만 사용한다.
//   - 실행 후 이 파일은 삭제해도 무방하다(1회성).
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Lobby.unity에 DifficultySelectView를 자동 생성·배선하는 1회성 에디터 도구.
    /// </summary>
    public static class SetupDifficultySelectView
    {
        // ====================================================================
        // 메뉴 진입점
        // ====================================================================

        /// <summary>
        /// 난이도 선택 화면을 생성하고 BattleRootView에 연결한 뒤 씬을 저장한다.
        /// Lobby.unity 씬이 열린 상태에서 실행해야 한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/난이도 선택 UI 생성")]
        public static void Create()
        {
            // ── 1) BattleRootView 찾기 ─────────────────────────────────────
            // 비활성 오브젝트까지 포함해서 검색(탭이 꺼져 있어도 찾기 위함).
            var rootView = Object.FindFirstObjectByType<BattleRootView>(FindObjectsInactive.Include);
            if (rootView == null)
            {
                Debug.LogError("[SetupDifficultySelectView] 씬에서 BattleRootView를 찾을 수 없습니다. " +
                               "Lobby.unity 씬이 열려 있는지 확인하세요.");
                return;
            }

            // ── 2) 이미 존재하면 중복 생성 방지 ────────────────────────────
            var existing = Object.FindFirstObjectByType<DifficultySelectView>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.LogWarning("[SetupDifficultySelectView] DifficultySelectView가 이미 존재합니다. " +
                                 "기존 오브젝트를 선택합니다(중복 생성하지 않음).");
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            // ── 3) 부모 트랜스폼 결정 ──────────────────────────────────────
            // BattleMainView의 부모(= 전투 탭 콘텐츠 영역) 아래에 두면
            // 다른 서브뷰들과 같은 계층에 배치되어 자연스럽다.
            // BattleMainView가 없으면 BattleRootView 자신을 부모로 사용한다.
            var battleMain = Object.FindFirstObjectByType<BattleMainView>(FindObjectsInactive.Include);
            Transform parentTransform = battleMain != null && battleMain.transform.parent != null
                ? battleMain.transform.parent
                : rootView.transform;

            // ── 4) DifficultySelectView 루트 GO 생성 ──────────────────────
            var viewGo = new GameObject("DifficultySelectView", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(viewGo, "Create DifficultySelectView");
            Undo.SetTransformParent(viewGo.transform, parentTransform, "Parent DifficultySelectView");

            // 기존 BattlePanel 서브뷰(BattleMainPanel, CustomGamePanel 등)와 동일하게
            // 상단 절반(anchorMin.y=0.5 ~ anchorMax.y=1)만 차지한다.
            // 하단 절반은 RaceSelectionView(항상 표시)가 사용하는 구조이므로 침범하지 않는다.
            var viewRect = viewGo.GetComponent<RectTransform>();
            viewRect.anchorMin = new Vector2(0f, 0.5f);
            viewRect.anchorMax = new Vector2(1f, 1f);
            viewRect.pivot = new Vector2(0.5f, 0.5f);
            viewRect.anchoredPosition = Vector2.zero;
            viewRect.sizeDelta = Vector2.zero;

            // CanvasGroup 초기값: 숨김 상태(alpha 0, 입력 차단).
            // DifficultySelectView가 CurrentScreen 구독으로 표시 여부를 제어한다.
            var canvasGroup = Undo.AddComponent<CanvasGroup>(viewGo);
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // DifficultySelectView 컴포넌트 부착.
            var view = Undo.AddComponent<DifficultySelectView>(viewGo);

            // ── 5) 버튼 영역 컨테이너 생성 (Rule 2: 앵커 비율 기반) ────────
            // 화면 가로 70%, 세로 30% 크기의 중앙 영역. 고정 픽셀값 없음.
            var areaGo = new GameObject("ButtonArea", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(areaGo, "Create ButtonArea");
            Undo.SetTransformParent(areaGo.transform, viewRect, "Parent ButtonArea");

            var areaRect = areaGo.GetComponent<RectTransform>();
            areaRect.anchorMin = new Vector2(0.15f, 0.35f);
            areaRect.anchorMax = new Vector2(0.85f, 0.65f);
            areaRect.pivot = new Vector2(0.5f, 0.5f);
            areaRect.anchoredPosition = Vector2.zero;
            areaRect.sizeDelta = Vector2.zero;

            // VerticalLayoutGroup: 버튼 높이는 각 버튼의 LayoutElement.preferredHeight(=100)로
            // 고정하고, 가로폭만 컨테이너에 맞춰 늘린다.
            // - childControlHeight=true + childForceExpandHeight=false →
            //   각 버튼이 LayoutElement.preferredHeight 값을 그대로 사용(균등 분배 X).
            // - childForceExpandWidth=true → 버튼 가로폭은 컨테이너 전체로 확장.
            var vlg = Undo.AddComponent<VerticalLayoutGroup>(areaGo);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 12f;

            // ── 6) 버튼 4개 생성 (ButtonArea 자식) ────────────────────────
            Button easyButton   = CreateButton(areaRect, "EasyButton",   "쉬움");
            Button normalButton = CreateButton(areaRect, "NormalButton", "보통");
            Button hardButton   = CreateButton(areaRect, "HardButton",   "어려움");
            Button backButton   = CreateButton(areaRect, "BackButton",   "뒤로");

            // ── 7) DifficultySelectView 필드 배선 ─────────────────────────
            // private SerializeField는 SerializedObject로만 코드 배선이 가능하다.
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_easyButton").objectReferenceValue   = easyButton;
            viewSo.FindProperty("_normalButton").objectReferenceValue = normalButton;
            viewSo.FindProperty("_hardButton").objectReferenceValue   = hardButton;
            viewSo.FindProperty("_backButton").objectReferenceValue   = backButton;
            viewSo.ApplyModifiedProperties();

            // ── 8) BattleRootView 필드 배선 ───────────────────────────────
            var rootSo = new SerializedObject(rootView);
            rootSo.FindProperty("_difficultySelectView").objectReferenceValue = view;
            rootSo.ApplyModifiedProperties();

            // ── 9) 씬 더티 표시 + 저장 ────────────────────────────────────
            Scene scene = rootView.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // ── 10) 완료 안내 ─────────────────────────────────────────────
            Selection.activeObject = viewGo;
            EditorGUIUtility.PingObject(viewGo);
            Debug.Log($"[SetupDifficultySelectView] 생성 완료: '{viewGo.name}' " +
                      $"(부모: {parentTransform.name}) → BattleRootView에 연결 + 씬 저장 완료.");
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// 버튼 GO를 생성한다(RectTransform + Image + Button + 자식 TMP 텍스트).
        /// </summary>
        /// <param name="parent">버튼을 배치할 부모 RectTransform.</param>
        /// <param name="goName">버튼 GameObject 이름.</param>
        /// <param name="label">버튼에 표시할 한글 텍스트.</param>
        /// <returns>생성된 Button 컴포넌트.</returns>
        private static Button CreateButton(RectTransform parent, string goName, string label)
        {
            // 버튼 본체 GO 생성.
            var buttonGo = new GameObject(goName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(buttonGo, $"Create {goName}");
            Undo.SetTransformParent(buttonGo.transform, parent, $"Parent {goName}");

            // VerticalLayoutGroup이 크기와 위치를 제어하므로
            // 버튼 자체는 stretch 앵커만 설정한다 (Rule 2: 고정 픽셀 금지).
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            // 버튼 높이 고정: LayoutElement.preferredHeight = 100.
            // VerticalLayoutGroup이 이 값을 읽어 버튼 높이를 100px로 잡는다
            // (childForceExpandHeight=false이므로 균등 분배되지 않음).
            var layoutElement = Undo.AddComponent<LayoutElement>(buttonGo);
            layoutElement.preferredHeight = 100f;

            // 버튼 배경 이미지(클릭 영역 + 시각적 배경).
            var image = Undo.AddComponent<Image>(buttonGo);
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Button 컴포넌트(타깃 그래픽 = 위 이미지).
            var button = Undo.AddComponent<Button>(buttonGo);
            button.targetGraphic = image;

            // ── 자식 텍스트 GO 생성(TextMeshProUGUI) ──────────────────────
            var textGo = new GameObject("Text (TMP)", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textGo, $"Create {goName} Text");
            Undo.SetTransformParent(textGo.transform, rect, $"Parent {goName} Text");

            // 텍스트가 버튼 전체를 덮도록 stretch.
            var textRect = textGo.GetComponent<RectTransform>();
            StretchFull(textRect);

            // TMP 텍스트 설정(가운데 정렬).
            var tmp = Undo.AddComponent<TextMeshProUGUI>(textGo);
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 28f;
            tmp.color = Color.white;

            // ── 폰트 에셋 명시 설정 (GameSystemRules_UI.md Rule 6) ─────────
            // 에디터 스크립트로 UI를 생성할 때도 폰트를 명시적으로 지정해야 한다.
            // 버튼 텍스트는 강조 요소이므로 Maplestory Bold SDF를 사용한다.
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Project/Fonts/Maplestory Bold SDF.asset");
            if (boldFont == null)
                Debug.LogWarning("[SetupDifficultySelectView] Maplestory Bold SDF.asset을 찾을 수 없습니다. 폰트 경로를 확인하세요.");
            else
                tmp.font = boldFont;

            return button;
        }

        /// <summary>
        /// RectTransform을 부모 전체를 채우도록(stretch) 설정한다.
        /// </summary>
        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
