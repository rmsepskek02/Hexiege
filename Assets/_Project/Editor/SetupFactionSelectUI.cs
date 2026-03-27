// ============================================================================
// SetupFactionSelectUI.cs
// 로비 씬의 BattleMainView 하위에 종족 선택 캐러셀 UI 계층을 자동으로 생성하는
// 1회성 에디터 스크립트.
//
// 메뉴 경로: Hexiege > Setup > Setup Faction Select UI
//
// 생성되는 UI 구조:
//   BattleMainView (기존)
//   └── FactionSelectPanel (HorizontalLayoutGroup)
//       ├── LeftArrowButton  (Button, "◀")
//       ├── FactionCenter    (VerticalLayoutGroup)
//       │   ├── FactionCharacterImage  (Image, 120×120, 회색 placeholder)
//       │   └── FactionNameText        (TextMeshProUGUI, "피스톨러", 24pt)
//       └── RightArrowButton (Button, "▶")
//
// 사용 방법:
//   1. Lobby 씬을 열어둔 상태에서 실행
//   2. 메뉴 바에서 Hexiege > Setup > Setup Faction Select UI 클릭
//   3. BattleMainView GameObject 하단에 종족 선택 UI가 자동 생성됨
//   4. 씬 저장 (Ctrl+S) 후 확인
//
// 주의:
//   - Lobby 씬이 열려있지 않으면 에러 메시지 출력 후 중단
//   - 이미 FactionSelectPanel이 존재하면 중복 생성 방지를 위해 중단
//   - 실행 후 씬이 Dirty 표시됨 → 수동 저장 필요
//
// Editor 전용 — 빌드에 포함되지 않음.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hexiege.Editor
{
    /// <summary>
    /// BattleMainView 하단에 종족 선택 캐러셀 UI 계층을 생성하는 에디터 유틸리티.
    /// </summary>
    public static class SetupFactionSelectUI
    {
        // ====================================================================
        // 상수 — UI 배치에 사용되는 수치
        // ====================================================================

        // FactionSelectPanel 전체 높이 (px)
        private const float PanelHeight = 200f;

        // FactionSelectPanel 좌우 여백 (px)
        private const float PanelHorizontalPadding = 20f;

        // 좌/우 화살표 버튼 크기 (px)
        private const float ArrowButtonWidth = 60f;
        private const float ArrowButtonHeight = 60f;

        // 캐릭터 이미지 크기 (px)
        private const float CharacterImageSize = 120f;

        // 종족 이름 텍스트 폰트 크기
        private const float FactionNameFontSize = 24f;

        // 기본 종족 이름 (placeholder)
        private const string DefaultFactionName = "피스톨러";

        // placeholder 이미지 색상 (회색)
        private static readonly Color PlaceholderColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        /// <summary>
        /// 메뉴에서 실행: Lobby 씬의 BattleMainView 하위에 종족 선택 UI 계층을 생성.
        /// </summary>
        [MenuItem("Hexiege/Setup/Setup Faction Select UI")]
        public static void Execute()
        {
            // ----------------------------------------------------------------
            // [1단계] Lobby 씬이 열려있는지 확인
            // ----------------------------------------------------------------
            // 현재 활성 씬의 이름이 "Lobby"인지 검사한다.
            // Lobby 씬이 아니면 에디터 다이얼로그로 안내 후 중단.
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "Lobby")
            {
                EditorUtility.DisplayDialog(
                    "씬 오류",
                    "Lobby 씬이 열려있지 않습니다.\n" +
                    "Lobby 씬을 열고 다시 실행해주세요.\n\n" +
                    $"현재 씬: {activeScene.name}",
                    "확인");
                Debug.LogError("[SetupFactionSelectUI] Lobby 씬이 열려있지 않습니다. 현재 씬: " + activeScene.name);
                return;
            }

            // ----------------------------------------------------------------
            // [2단계] BattleMainView GameObject 찾기
            // ----------------------------------------------------------------
            // 씬에서 "BattleMainView"라는 이름을 가진 GameObject를 검색한다.
            // 찾지 못하면 에러 메시지 출력 후 중단.
            GameObject battleMainViewObj = GameObject.Find("BattleMainView");
            if (battleMainViewObj == null)
            {
                // 비활성 오브젝트는 GameObject.Find로 찾을 수 없으므로
                // Resources.FindObjectsOfTypeAll을 사용하여 비활성 포함 전체 검색
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    // 씬에 속하고 이름이 "BattleMainView"인 오브젝트를 찾는다
                    if (go.name == "BattleMainView" && go.scene == activeScene)
                    {
                        battleMainViewObj = go;
                        break;
                    }
                }
            }

            if (battleMainViewObj == null)
            {
                EditorUtility.DisplayDialog(
                    "오브젝트 없음",
                    "BattleMainView GameObject를 찾을 수 없습니다.\n" +
                    "Lobby 씬에 BattleMainView가 있는지 확인해주세요.",
                    "확인");
                Debug.LogError("[SetupFactionSelectUI] BattleMainView GameObject를 찾을 수 없습니다.");
                return;
            }

            // ----------------------------------------------------------------
            // [3단계] 중복 생성 방지 — FactionSelectPanel이 이미 있는지 확인
            // ----------------------------------------------------------------
            // BattleMainView의 자식 중 "FactionSelectPanel"이 이미 존재하면
            // 중복 생성을 방지하기 위해 경고 출력 후 중단한다.
            Transform existingPanel = battleMainViewObj.transform.Find("FactionSelectPanel");
            if (existingPanel != null)
            {
                EditorUtility.DisplayDialog(
                    "이미 존재함",
                    "FactionSelectPanel이 이미 존재합니다.\n" +
                    "중복 생성을 방지하기 위해 작업을 중단합니다.\n\n" +
                    "기존 패널을 삭제한 후 다시 실행해주세요.",
                    "확인");
                Debug.LogWarning("[SetupFactionSelectUI] FactionSelectPanel이 이미 존재합니다. 중복 생성 방지로 중단.");
                return;
            }

            // ----------------------------------------------------------------
            // [4단계] UI 계층 생성
            // ----------------------------------------------------------------

            // 4-1) FactionSelectPanel — 종족 선택 영역의 최상위 컨테이너
            // HorizontalLayoutGroup으로 [좌 화살표 | 중앙 | 우 화살표] 구조를 만든다.
            GameObject panelObj = CreateUIGameObject("FactionSelectPanel", battleMainViewObj.transform);

            // RectTransform 설정: 부모(BattleMainView)의 하단에 가로로 꽉 차게 배치
            // anchorMin(0,0) / anchorMax(1,0) → 좌우 stretch, 세로 bottom 고정
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);       // 좌하단 기준
            panelRect.anchorMax = new Vector2(1f, 0f);       // 우하단 기준 (좌우 stretch)
            panelRect.pivot = new Vector2(0.5f, 0f);         // 피벗: 하단 중앙
            panelRect.sizeDelta = new Vector2(0f, PanelHeight); // 높이 200px, 너비는 stretch로 0
            panelRect.anchoredPosition = Vector2.zero;       // 하단에 딱 붙임

            // HorizontalLayoutGroup 추가: 자식들을 가로로 정렬
            // spacing=10, padding 좌우 20px, 자식 크기 제어는 너비만 활성화
            HorizontalLayoutGroup panelLayout = panelObj.AddComponent<HorizontalLayoutGroup>();
            panelLayout.spacing = 10f;                                       // 자식 간 가로 간격
            panelLayout.padding = new RectOffset(
                (int)PanelHorizontalPadding, (int)PanelHorizontalPadding,    // 좌우 여백 20px
                0, 0);                                                        // 상하 여백 없음
            panelLayout.childAlignment = TextAnchor.MiddleCenter;            // 자식들을 세로 중앙 정렬
            panelLayout.childControlWidth = true;                            // 자식 너비를 레이아웃이 제어
            panelLayout.childControlHeight = true;                           // 자식 높이를 레이아웃이 제어
            panelLayout.childForceExpandWidth = false;                       // 자식이 남은 공간을 강제 확장하지 않음
            panelLayout.childForceExpandHeight = false;                      // 자식이 세로로 강제 확장하지 않음

            // 4-2) LeftArrowButton — 좌측 화살표 버튼 ("◀")
            // 사용자가 이전 종족으로 넘길 때 사용하는 버튼
            GameObject leftArrowObj = CreateArrowButton("LeftArrowButton", panelObj.transform, "\u25C0");

            // 4-3) FactionCenter — 중앙 영역 (캐릭터 이미지 + 종족 이름)
            // VerticalLayoutGroup으로 세로 배치, flexible width=1로 남은 공간을 채움
            GameObject centerObj = CreateUIGameObject("FactionCenter", panelObj.transform);

            // VerticalLayoutGroup: 자식(이미지, 텍스트)을 세로로 가운데 정렬
            VerticalLayoutGroup centerLayout = centerObj.AddComponent<VerticalLayoutGroup>();
            centerLayout.spacing = 8f;                                       // 이미지와 텍스트 사이 간격
            centerLayout.childAlignment = TextAnchor.MiddleCenter;           // 가운데 정렬
            centerLayout.childControlWidth = true;
            centerLayout.childControlHeight = false;                         // 세로는 자식 각자 크기 유지
            centerLayout.childForceExpandWidth = false;
            centerLayout.childForceExpandHeight = false;

            // LayoutElement: flexible width=1 → HorizontalLayoutGroup에서 남은 공간을 이 영역이 차지
            // 좌/우 화살표 버튼(60px 고정)을 제외한 나머지 공간을 중앙이 유연하게 채운다
            LayoutElement centerLayoutElem = centerObj.AddComponent<LayoutElement>();
            centerLayoutElem.flexibleWidth = 1f;

            // 4-3a) FactionCharacterImage — 캐릭터 이미지 (placeholder)
            // 종족의 대표 캐릭터를 보여줄 이미지 슬롯 (현재는 회색 placeholder)
            GameObject characterImgObj = CreateUIGameObject("FactionCharacterImage", centerObj.transform);
            Image characterImg = characterImgObj.AddComponent<Image>();
            characterImg.color = PlaceholderColor;                           // Sprite 없을 때 회색으로 표시

            // 이미지 크기를 120×120px로 고정
            LayoutElement imgLayoutElem = characterImgObj.AddComponent<LayoutElement>();
            imgLayoutElem.preferredWidth = CharacterImageSize;
            imgLayoutElem.preferredHeight = CharacterImageSize;

            // 4-3b) FactionNameText — 종족 이름 텍스트
            // TextMeshPro를 사용하여 종족 이름을 표시 (기본값: "피스톨러")
            GameObject nameTextObj = CreateUIGameObject("FactionNameText", centerObj.transform);
            TextMeshProUGUI nameText = nameTextObj.AddComponent<TextMeshProUGUI>();
            nameText.text = DefaultFactionName;                              // 기본 텍스트: "피스톨러"
            nameText.fontSize = FactionNameFontSize;                         // 폰트 크기 24
            nameText.alignment = TextAlignmentOptions.Center;                // 가운데 정렬
            nameText.color = Color.white;                                    // 텍스트 색상: 흰색

            // 텍스트 높이를 적절히 지정 (너무 크지 않게 40px)
            LayoutElement textLayoutElem = nameTextObj.AddComponent<LayoutElement>();
            textLayoutElem.preferredHeight = 40f;

            // 4-4) RightArrowButton — 우측 화살표 버튼 ("▶")
            // 사용자가 다음 종족으로 넘길 때 사용하는 버튼
            GameObject rightArrowObj = CreateArrowButton("RightArrowButton", panelObj.transform, "\u25B6");

            // ----------------------------------------------------------------
            // [5단계] 씬을 Dirty로 표시
            // ----------------------------------------------------------------
            // 에디터에서 변경사항이 있음을 알려 저장하지 않고 씬을 닫으면 경고가 뜨도록 한다.
            // 실제 저장은 사용자가 Ctrl+S로 수동으로 수행해야 한다.
            EditorSceneManager.MarkSceneDirty(activeScene);

            // ----------------------------------------------------------------
            // [6단계] 완료 메시지 출력
            // ----------------------------------------------------------------
            Debug.Log("[SetupFactionSelectUI] 종족 선택 UI 생성 완료. BattleMainView 하위에 FactionSelectPanel이 추가되었습니다.");
            EditorUtility.DisplayDialog(
                "생성 완료",
                "종족 선택 UI가 성공적으로 생성되었습니다.\n\n" +
                "BattleMainView > FactionSelectPanel\n" +
                "  ├ LeftArrowButton\n" +
                "  ├ FactionCenter\n" +
                "  │  ├ FactionCharacterImage\n" +
                "  │  └ FactionNameText\n" +
                "  └ RightArrowButton\n\n" +
                "씬을 저장(Ctrl+S)해주세요.",
                "확인");
        }

        // ====================================================================
        // 헬퍼 메서드
        // ====================================================================

        /// <summary>
        /// UI용 GameObject를 생성하고 지정된 부모 Transform 하위에 배치한다.
        /// 모든 UI 오브젝트는 RectTransform이 필요하므로 자동으로 추가된다.
        /// </summary>
        /// <param name="name">생성할 GameObject의 이름</param>
        /// <param name="parent">부모 Transform (Canvas 계층 내에 있어야 함)</param>
        /// <returns>생성된 GameObject</returns>
        private static GameObject CreateUIGameObject(string name, Transform parent)
        {
            // new GameObject(name)은 일반 Transform을 생성하므로,
            // RectTransform을 명시적으로 추가하여 UI 시스템에서 사용할 수 있게 한다.
            GameObject obj = new GameObject(name, typeof(RectTransform));

            // 부모 설정 — false를 전달하여 월드 좌표가 아닌 로컬 좌표를 유지
            obj.transform.SetParent(parent, false);

            // Undo 등록: 에디터에서 Ctrl+Z로 되돌릴 수 있도록 생성 기록
            Undo.RegisterCreatedObjectUndo(obj, "Create " + name);

            return obj;
        }

        /// <summary>
        /// 화살표 버튼 GameObject를 생성한다.
        /// Button 컴포넌트와 TextMeshPro 자식을 포함하며, 고정 크기(60×60px)로 설정된다.
        /// </summary>
        /// <param name="name">버튼 GameObject 이름 (예: "LeftArrowButton")</param>
        /// <param name="parent">부모 Transform</param>
        /// <param name="arrowText">버튼에 표시할 텍스트 (예: "◀" 또는 "▶")</param>
        /// <returns>생성된 버튼 GameObject</returns>
        private static GameObject CreateArrowButton(string name, Transform parent, string arrowText)
        {
            // 버튼 오브젝트 생성 — Image는 Button의 targetGraphic으로 사용됨
            GameObject btnObj = CreateUIGameObject(name, parent);
            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);   // 어두운 회색 배경

            // Button 컴포넌트 추가 — 클릭 이벤트 수신용
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;                        // 클릭 영역으로 Image 사용

            // LayoutElement: 고정 크기 60×60px
            // HorizontalLayoutGroup 내에서 이 버튼이 항상 60×60px로 유지되도록 설정
            LayoutElement btnLayoutElem = btnObj.AddComponent<LayoutElement>();
            btnLayoutElem.preferredWidth = ArrowButtonWidth;
            btnLayoutElem.preferredHeight = ArrowButtonHeight;

            // 버튼 내부에 TextMeshPro 텍스트 생성 (화살표 기호 표시)
            GameObject textObj = CreateUIGameObject("Text", btnObj.transform);
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = arrowText;                            // "◀" 또는 "▶"
            tmpText.fontSize = 28f;                              // 화살표가 잘 보이는 크기
            tmpText.alignment = TextAlignmentOptions.Center;     // 버튼 중앙에 배치
            tmpText.color = Color.white;

            // 텍스트 RectTransform: 버튼 영역 전체를 채우도록 stretch 설정
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;                   // 좌하단
            textRect.anchorMax = Vector2.one;                    // 우상단 → 부모 전체 채움
            textRect.sizeDelta = Vector2.zero;                   // stretch이므로 추가 크기 없음
            textRect.anchoredPosition = Vector2.zero;

            return btnObj;
        }
    }
}
