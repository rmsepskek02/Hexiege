// ============================================================================
// LobbySceneBuilder.cs
// Lobby.unity 씬의 UI 계층 구조를 에디터 메뉴에서 자동 생성하는 빌더 스크립트.
//
// 역할:
//   - Canvas + EventSystem 생성
//   - LobbyRoot → TabBar → ContentArea → 각 패널 계층 구조 자동 구축
//   - Maplestory Bold SDF 폰트 적용
//   - 앵커 기반 반응형 UI (9:16 기준)
//   - 각 View 컴포넌트의 SerializeField 자동 연결
//
// 사용법: Unity 메뉴 Hexiege/UI/Build Lobby Scene
//
// Editor 전용 스크립트.
// ============================================================================

#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace Hexiege.Editor
{
    /// <summary>
    /// Lobby 씬 UI 계층 구조 자동 생성 에디터 도구.
    /// </summary>
    public static class LobbySceneBuilder
    {
        // ====================================================================
        // 색상 상수
        // ====================================================================

        private const string ColorBackground = "#0F0F1A";
        private const string ColorTabBar = "#1A1A2E";
        private const string ColorBlue = "#4A90D9";
        private const string ColorPurple = "#6C5CE7";
        private const string ColorGreen = "#00B894";
        private const string ColorGray = "#636E72";
        private const string ColorGold = "#FFD700";
        private const string ColorLightGray = "#B2BEC3";
        private const string ColorError = "#FF6B6B";
        private const string ColorPlaceholder = "#636E72";

        // ====================================================================
        // 폰트 캐시
        // ====================================================================

        private static TMP_FontAsset _font;

        // ====================================================================
        // 메뉴 진입점
        // ====================================================================

        [MenuItem("Hexiege/UI/Build Lobby Scene")]
        public static void BuildLobbyScene()
        {
            // 폰트 로드
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Project/Fonts/Maplestory Bold SDF.asset");

            if (_font == null)
                Debug.LogWarning("[LobbySceneBuilder] Maplestory Bold SDF 폰트를 찾을 수 없습니다. 기본 폰트를 사용합니다.");

            // Canvas + EventSystem 생성
            var canvas = CreateCanvas();
            var canvasGo = canvas.gameObject;

            // EventSystem
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // LobbyRoot — 전체 배경
            var lobbyRoot = CreateChild(canvasGo, "LobbyRoot");
            SetFullStretch(lobbyRoot);
            var bgImage = lobbyRoot.AddComponent<Image>();
            bgImage.color = HexColor(ColorBackground);

            // LobbyRootView 컴포넌트
            var lobbyRootView = lobbyRoot.AddComponent<Hexiege.Presentation.LobbyRootView>();

            // ── TabBar (하단) ────────────────────────────────────
            var tabBar = CreateChild(lobbyRoot, "TabBar");
            var tabBarRect = GetRect(tabBar);
            tabBarRect.anchorMin = new Vector2(0, 0);
            tabBarRect.anchorMax = new Vector2(1, 0);
            tabBarRect.pivot = new Vector2(0.5f, 0);
            tabBarRect.offsetMin = Vector2.zero;
            tabBarRect.offsetMax = Vector2.zero;
            tabBarRect.sizeDelta = new Vector2(0, 140);

            // TabBar 배경
            var tabBarBg = tabBar.AddComponent<Image>();
            tabBarBg.color = HexColor(ColorTabBar);

            // HorizontalLayoutGroup
            var hlg = tabBar.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 0;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            // TabBarView 컴포넌트
            var tabBarView = tabBar.AddComponent<Hexiege.Presentation.TabBarView>();

            // 탭 버튼 4개
            var battleTabBtn = CreateTabButton(tabBar, "BattleTabBtn", "\uc804\ud22c");
            var shopTabBtn = CreateTabButton(tabBar, "ShopTabBtn", "\uc0c1\uc810");
            var profileTabBtn = CreateTabButton(tabBar, "ProfileTabBtn", "\ud504\ub85c\ud544");
            var rankingTabBtn = CreateTabButton(tabBar, "RankingTabBtn", "\ub7ad\ud0b9");

            // TabBarView SerializeField 연결
            SetPrivateField(tabBarView, "_battleTabButton", battleTabBtn.GetComponent<Button>());
            SetPrivateField(tabBarView, "_shopTabButton", shopTabBtn.GetComponent<Button>());
            SetPrivateField(tabBarView, "_profileTabButton", profileTabBtn.GetComponent<Button>());
            SetPrivateField(tabBarView, "_rankingTabButton", rankingTabBtn.GetComponent<Button>());

            // ── ContentArea (탭 바 위 전체) ─────────────────────
            var contentArea = CreateChild(lobbyRoot, "ContentArea");
            var contentRect = GetRect(contentArea);
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(0, 140); // 탭 바 높이만큼
            contentRect.offsetMax = Vector2.zero;

            // ── BattlePanel ──────────────────────────────────────
            var battlePanel = CreateChild(contentArea, "BattlePanel");
            SetFullStretch(battlePanel);
            var battleRootView = battlePanel.AddComponent<Hexiege.Presentation.BattleRootView>();

            // BattleMainPanel
            var battleMainPanel = CreateBattleMainPanel(battlePanel);
            var battleMainView = battleMainPanel.GetComponent<Hexiege.Presentation.BattleMainView>();

            // CustomGamePanel
            var customGamePanel = CreateCustomGamePanel(battlePanel);
            customGamePanel.SetActive(false);
            var customGameView = customGamePanel.GetComponent<Hexiege.Presentation.CustomGameView>();

            // CustomHostPanel
            var customHostPanel = CreateCustomHostPanel(battlePanel);
            customHostPanel.SetActive(false);
            var customHostView = customHostPanel.GetComponent<Hexiege.Presentation.CustomHostView>();

            // CustomJoinPanel
            var customJoinPanel = CreateCustomJoinPanel(battlePanel);
            customJoinPanel.SetActive(false);
            var customJoinView = customJoinPanel.GetComponent<Hexiege.Presentation.CustomJoinView>();

            // RandomMatchPanel
            var randomMatchPanel = CreateRandomMatchPanel(battlePanel);
            randomMatchPanel.SetActive(false);
            var randomMatchView = randomMatchPanel.GetComponent<Hexiege.Presentation.RandomMatchView>();

            // BattleRootView SerializeField 연결
            SetPrivateField(battleRootView, "_battleMainView", battleMainView);
            SetPrivateField(battleRootView, "_customGameView", customGameView);
            SetPrivateField(battleRootView, "_customHostView", customHostView);
            SetPrivateField(battleRootView, "_customJoinView", customJoinView);
            SetPrivateField(battleRootView, "_randomMatchView", randomMatchView);

            // ── ShopPanel (플레이스홀더) ─────────────────────────
            var shopPanel = CreatePlaceholderPanel(contentArea, "ShopPanel", "\uc0c1\uc810 (\uc900\ube44 \uc911)");
            shopPanel.SetActive(false);

            // ── ProfilePanel (플레이스홀더) ──────────────────────
            var profilePanel = CreatePlaceholderPanel(contentArea, "ProfilePanel", "\ud504\ub85c\ud544 (\uc900\ube44 \uc911)");
            profilePanel.SetActive(false);

            // ── RankingPanel (플레이스홀더) ──────────────────────
            var rankingPanel = CreatePlaceholderPanel(contentArea, "RankingPanel", "\ub7ad\ud0b9 (\uc900\ube44 \uc911)");
            rankingPanel.SetActive(false);

            // ── LobbyRootView SerializeField 연결 ────────────────
            SetPrivateField(lobbyRootView, "_tabBarView", tabBarView);
            SetPrivateField(lobbyRootView, "_battleRootView", battleRootView);
            SetPrivateField(lobbyRootView, "_battlePanel", battlePanel);
            SetPrivateField(lobbyRootView, "_shopPanel", shopPanel);
            SetPrivateField(lobbyRootView, "_profilePanel", profilePanel);
            SetPrivateField(lobbyRootView, "_rankingPanel", rankingPanel);

            // 에디터 더티 마킹
            EditorUtility.SetDirty(canvasGo);
            Debug.Log("[LobbySceneBuilder] Lobby 씬 UI 계층 구조 생성 완료.");
        }

        // ====================================================================
        // Canvas 생성
        // ====================================================================

        /// <summary>
        /// Canvas + CanvasScaler + GraphicRaycaster 생성.
        /// </summary>
        private static Canvas CreateCanvas()
        {
            var canvasGo = new GameObject("[UI] Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        // ====================================================================
        // 탭 버튼 생성
        // ====================================================================

        /// <summary>
        /// 탭 바 하위 버튼 생성. LayoutElement(flexibleWidth=1) 포함.
        /// </summary>
        private static GameObject CreateTabButton(GameObject parent, string name, string label)
        {
            var btnGo = CreateChild(parent, name);
            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = HexColor(ColorTabBar);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImage;

            var le = btnGo.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;

            // 텍스트
            var textGo = CreateChild(btnGo, "Text");
            SetFullStretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            ApplyFont(tmp);

            return btnGo;
        }

        // ====================================================================
        // BattleMainPanel
        // ====================================================================

        /// <summary>
        /// BattleMainPanel 생성. 싱글플레이/커스텀/랜덤 버튼 3개.
        /// </summary>
        private static GameObject CreateBattleMainPanel(GameObject parent)
        {
            var panel = CreateChild(parent, "BattleMainPanel");
            SetFullStretch(panel);
            var view = panel.AddComponent<Hexiege.Presentation.BattleMainView>();

            // VerticalLayoutGroup
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20;
            vlg.padding = new RectOffset(0, 0, 60, 60);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            // 버튼들
            var singleplayBtn = CreatePanelButton(panel, "SingleplayBtn", "\uc2f1\uae00\ud50c\ub808\uc774", ColorBlue, 120, 32);
            var customBtn = CreatePanelButton(panel, "CustomBtn", "\ucee4\uc2a4\ud140 \uac8c\uc784", ColorPurple, 120, 32);
            var randomBtn = CreatePanelButton(panel, "RandomBtn", "\ub79c\ub364 \ub9e4\uce6d", ColorGreen, 120, 32);

            // SerializeField 연결
            SetPrivateField(view, "_singleplayButton", singleplayBtn.GetComponent<Button>());
            SetPrivateField(view, "_customGameButton", customBtn.GetComponent<Button>());
            SetPrivateField(view, "_randomMatchButton", randomBtn.GetComponent<Button>());

            return panel;
        }

        // ====================================================================
        // CustomGamePanel
        // ====================================================================

        /// <summary>
        /// CustomGamePanel 생성. 방 만들기/코드로 참가/뒤로 버튼 3개.
        /// </summary>
        private static GameObject CreateCustomGamePanel(GameObject parent)
        {
            var panel = CreateChild(parent, "CustomGamePanel");
            SetFullStretch(panel);
            var view = panel.AddComponent<Hexiege.Presentation.CustomGameView>();

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20;
            vlg.padding = new RectOffset(0, 0, 60, 60);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            var createRoomBtn = CreatePanelButton(panel, "CreateRoomBtn", "\ubc29 \ub9cc\ub4e4\uae30", ColorPurple, 120, 32);
            var joinByCodeBtn = CreatePanelButton(panel, "JoinByCodeBtn", "\ucf54\ub4dc\ub85c \ucc38\uac00", ColorBlue, 120, 32);
            var backBtn = CreatePanelButton(panel, "BackBtn", "\ub4a4\ub85c", ColorGray, 120, 32);

            SetPrivateField(view, "_createRoomButton", createRoomBtn.GetComponent<Button>());
            SetPrivateField(view, "_joinByCodeButton", joinByCodeBtn.GetComponent<Button>());
            SetPrivateField(view, "_backButton", backBtn.GetComponent<Button>());

            return panel;
        }

        // ====================================================================
        // CustomHostPanel
        // ====================================================================

        /// <summary>
        /// CustomHostPanel 생성. 코드 표시 + 플레이어 수 + 상태 + 에러 + 취소.
        /// </summary>
        private static GameObject CreateCustomHostPanel(GameObject parent)
        {
            var panel = CreateChild(parent, "CustomHostPanel");
            SetFullStretch(panel);
            var view = panel.AddComponent<Hexiege.Presentation.CustomHostView>();

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 24;
            vlg.padding = new RectOffset(0, 0, 80, 60);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            // CodeText
            var codeTextGo = CreateChild(panel, "CodeText");
            var codeLE = codeTextGo.AddComponent<LayoutElement>();
            codeLE.preferredHeight = 100;
            codeLE.flexibleWidth = 1;
            var codeText = codeTextGo.AddComponent<TextMeshProUGUI>();
            codeText.text = "---";
            codeText.fontSize = 72;
            codeText.fontStyle = FontStyles.Bold;
            codeText.color = HexColor(ColorGold);
            codeText.alignment = TextAlignmentOptions.Center;
            ApplyFont(codeText);

            // PlayersText
            var playersTextGo = CreateChild(panel, "PlayersText");
            var playersLE = playersTextGo.AddComponent<LayoutElement>();
            playersLE.preferredHeight = 40;
            playersLE.flexibleWidth = 1;
            var playersText = playersTextGo.AddComponent<TextMeshProUGUI>();
            playersText.text = "0/2\uba85 \uc5f0\uacb0\ub428";
            playersText.fontSize = 28;
            playersText.color = Color.white;
            playersText.alignment = TextAlignmentOptions.Center;
            ApplyFont(playersText);

            // StatusText
            var statusTextGo = CreateChild(panel, "StatusText");
            var statusLE = statusTextGo.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 36;
            statusLE.flexibleWidth = 1;
            var statusText = statusTextGo.AddComponent<TextMeshProUGUI>();
            statusText.text = "\ubc29 \uc0dd\uc131 \uc911...";
            statusText.fontSize = 24;
            statusText.color = HexColor(ColorLightGray);
            statusText.alignment = TextAlignmentOptions.Center;
            ApplyFont(statusText);

            // ErrorText (기본 비활성화)
            var errorTextGo = CreateChild(panel, "ErrorText");
            var errorLE = errorTextGo.AddComponent<LayoutElement>();
            errorLE.preferredHeight = 32;
            errorLE.flexibleWidth = 1;
            var errorText = errorTextGo.AddComponent<TextMeshProUGUI>();
            errorText.text = "";
            errorText.fontSize = 22;
            errorText.color = HexColor(ColorError);
            errorText.alignment = TextAlignmentOptions.Center;
            ApplyFont(errorText);
            errorTextGo.SetActive(false);

            // CancelBtn
            var cancelBtn = CreatePanelButton(panel, "CancelBtn", "\ucde8\uc18c", ColorGray, 100, 28);

            // SerializeField 연결
            SetPrivateField(view, "_codeText", codeText);
            SetPrivateField(view, "_connectedPlayersText", playersText);
            SetPrivateField(view, "_statusText", statusText);
            SetPrivateField(view, "_errorText", errorText);
            SetPrivateField(view, "_cancelButton", cancelBtn.GetComponent<Button>());

            return panel;
        }

        // ====================================================================
        // CustomJoinPanel
        // ====================================================================

        /// <summary>
        /// CustomJoinPanel 생성. 코드 입력 + 에러 + 상태 + 참가/뒤로 버튼.
        /// </summary>
        private static GameObject CreateCustomJoinPanel(GameObject parent)
        {
            var panel = CreateChild(parent, "CustomJoinPanel");
            SetFullStretch(panel);
            var view = panel.AddComponent<Hexiege.Presentation.CustomJoinView>();

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 24;
            vlg.padding = new RectOffset(60, 60, 60, 60);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            // CodeInput (TMP_InputField)
            var codeInput = CreateInputField(panel, "CodeInput", "\ucd08\ub300 \ucf54\ub4dc \uc785\ub825", 100);

            // ErrorText (기본 비활성화)
            var errorTextGo = CreateChild(panel, "ErrorText");
            var errorLE = errorTextGo.AddComponent<LayoutElement>();
            errorLE.preferredHeight = 32;
            errorLE.flexibleWidth = 1;
            var errorText = errorTextGo.AddComponent<TextMeshProUGUI>();
            errorText.text = "";
            errorText.fontSize = 22;
            errorText.color = HexColor(ColorError);
            errorText.alignment = TextAlignmentOptions.Center;
            ApplyFont(errorText);
            errorTextGo.SetActive(false);

            // StatusText
            var statusTextGo = CreateChild(panel, "StatusText");
            var statusLE = statusTextGo.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 36;
            statusLE.flexibleWidth = 1;
            var statusText = statusTextGo.AddComponent<TextMeshProUGUI>();
            statusText.text = "";
            statusText.fontSize = 24;
            statusText.color = HexColor(ColorLightGray);
            statusText.alignment = TextAlignmentOptions.Center;
            ApplyFont(statusText);

            // JoinBtn
            var joinBtn = CreatePanelButton(panel, "JoinBtn", "\ucc38\uac00", ColorBlue, 100, 28);

            // BackBtn
            var backBtn = CreatePanelButton(panel, "BackBtn", "\ub4a4\ub85c", ColorGray, 100, 28);

            // SerializeField 연결
            SetPrivateField(view, "_codeInput", codeInput.GetComponent<TMP_InputField>());
            SetPrivateField(view, "_errorText", errorText);
            SetPrivateField(view, "_statusText", statusText);
            SetPrivateField(view, "_joinButton", joinBtn.GetComponent<Button>());
            SetPrivateField(view, "_backButton", backBtn.GetComponent<Button>());

            return panel;
        }

        // ====================================================================
        // RandomMatchPanel
        // ====================================================================

        /// <summary>
        /// RandomMatchPanel 생성. 상태 텍스트 + 취소/뒤로 버튼.
        /// </summary>
        private static GameObject CreateRandomMatchPanel(GameObject parent)
        {
            var panel = CreateChild(parent, "RandomMatchPanel");
            SetFullStretch(panel);
            var view = panel.AddComponent<Hexiege.Presentation.RandomMatchView>();

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 24;
            vlg.padding = new RectOffset(60, 60, 60, 60);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            // StatusText
            var statusTextGo = CreateChild(panel, "StatusText");
            var statusLE = statusTextGo.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 50;
            statusLE.flexibleWidth = 1;
            var statusText = statusTextGo.AddComponent<TextMeshProUGUI>();
            statusText.text = "\ub79c\ub364 \ub9e4\uce6d (\ucd94\ud6c4 \uad6c\ud604 \uc608\uc815)";
            statusText.fontSize = 30;
            statusText.color = HexColor(ColorLightGray);
            statusText.alignment = TextAlignmentOptions.Center;
            ApplyFont(statusText);

            // CancelBtn
            var cancelBtn = CreatePanelButton(panel, "CancelBtn", "\ucde8\uc18c", ColorGray, 100, 28);

            // BackBtn
            var backBtn = CreatePanelButton(panel, "BackBtn", "\ub4a4\ub85c", ColorGray, 100, 28);

            // SerializeField 연결
            SetPrivateField(view, "_statusText", statusText);
            SetPrivateField(view, "_cancelButton", cancelBtn.GetComponent<Button>());
            SetPrivateField(view, "_backButton", backBtn.GetComponent<Button>());

            return panel;
        }

        // ====================================================================
        // 플레이스홀더 패널
        // ====================================================================

        /// <summary>
        /// 플레이스홀더 패널 생성 (Shop/Profile/Ranking).
        /// </summary>
        private static GameObject CreatePlaceholderPanel(GameObject parent, string name, string text)
        {
            var panel = CreateChild(parent, name);
            SetFullStretch(panel);

            var tmp = panel.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 40;
            tmp.color = HexColor(ColorPlaceholder);
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(tmp);

            return panel;
        }

        // ====================================================================
        // 공통 버튼 생성
        // ====================================================================

        /// <summary>
        /// LayoutElement 기반 버튼 생성. 10% 좌우 여백은 부모 VerticalLayoutGroup padding으로 처리.
        /// </summary>
        private static GameObject CreatePanelButton(
            GameObject parent, string name, string label,
            string colorHex, float height, float fontSize)
        {
            var btnGo = CreateChild(parent, name);

            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1;

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = HexColor(colorHex);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImage;

            // 버튼 텍스트
            var textGo = CreateChild(btnGo, "Text");
            SetFullStretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(tmp);

            return btnGo;
        }

        // ====================================================================
        // TMP_InputField 생성
        // ====================================================================

        /// <summary>
        /// TMP_InputField 생성 (배경 + placeholder + 입력 텍스트).
        /// </summary>
        private static GameObject CreateInputField(
            GameObject parent, string name, string placeholderText, float height)
        {
            var inputGo = CreateChild(parent, name);

            var le = inputGo.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1;

            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.15f, 0.15f, 0.25f, 1f);

            // TextArea (마스크 영역)
            var textArea = CreateChild(inputGo, "Text Area");
            SetFullStretch(textArea);
            var textAreaRect = GetRect(textArea);
            textAreaRect.offsetMin = new Vector2(20, 5);
            textAreaRect.offsetMax = new Vector2(-20, -5);
            textArea.AddComponent<RectMask2D>();

            // Placeholder
            var placeholderGo = CreateChild(textArea, "Placeholder");
            SetFullStretch(placeholderGo);
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.text = placeholderText;
            placeholder.fontSize = 28;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = HexColor(ColorPlaceholder);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyFont(placeholder);

            // 입력 텍스트
            var textGo = CreateChild(textArea, "Text");
            SetFullStretch(textGo);
            var inputText = textGo.AddComponent<TextMeshProUGUI>();
            inputText.text = "";
            inputText.fontSize = 28;
            inputText.color = Color.white;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyFont(inputText);

            // TMP_InputField 컴포넌트
            var inputField = inputGo.AddComponent<TMP_InputField>();
            inputField.textViewport = GetRect(textArea);
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.fontAsset = _font;

            return inputGo;
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 부모 하위에 RectTransform 포함 자식 오브젝트 생성.
        /// </summary>
        private static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        /// <summary>
        /// RectTransform을 부모 전체 영역에 채우기 (Stretch-Stretch).
        /// </summary>
        private static void SetFullStretch(GameObject go)
        {
            var rect = GetRect(go);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// RectTransform 가져오기.
        /// </summary>
        private static RectTransform GetRect(GameObject go)
        {
            return go.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 폰트 적용. null이면 기본 폰트 유지.
        /// </summary>
        private static void ApplyFont(TextMeshProUGUI tmp)
        {
            if (_font != null)
                tmp.font = _font;
        }

        /// <summary>
        /// HEX 색상 문자열을 Color로 변환.
        /// </summary>
        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        /// <summary>
        /// private [SerializeField] 필드에 값 설정 (리플렉션).
        /// </summary>
        private static void SetPrivateField<T>(Component component, string fieldName, T value)
        {
            var type = component.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(component, value);
                EditorUtility.SetDirty(component);
            }
            else
            {
                Debug.LogWarning($"[LobbySceneBuilder] {type.Name}.{fieldName} 필드를 찾을 수 없습니다.");
            }
        }
    }
}

// ============================================================================
// Build Settings 등록 유틸리티
// ============================================================================
namespace Hexiege.Editor
{
    public static class LobbyBuildSettingsSetup
    {
        [MenuItem("Hexiege/Setup/Register Scenes to Build Settings")]
        public static void RegisterScenes()
        {
            var scenes = new[]
            {
                new UnityEditor.EditorBuildSettingsScene("Assets/_Project/Scenes/Lobby.unity", true),
                new UnityEditor.EditorBuildSettingsScene("Assets/_Project/Scenes/Game.unity", true),
            };
            UnityEditor.EditorBuildSettings.scenes = scenes;
            Debug.Log("[BuildSettings] Lobby(0) + Game(1) 등록 완료");
        }
    }
}

#endif
