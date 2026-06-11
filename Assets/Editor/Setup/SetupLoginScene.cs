// ============================================================================
// SetupLoginScene.cs
// Login.unity 씬을 코드로 생성하고, 로그인 시스템에 필요한 모든 UI(7개 패널/팝업)를
// 9:16 세로 모드 Canvas에 배치한 뒤, LoginBootstrapper 및 각 View의 SerializeField를
// 자동으로 연결하는 1회성 에디터 셋업 스크립트.
//
// 실행 방법:
//   Unity 상단 메뉴 → Hexiege/Setup/Login 씬 생성 클릭
//
// 동작 개요:
//   1) 빈 씬을 새로 만들어 Login.unity 로 저장 (이미 있으면 덮어쓰기 확인)
//   2) Canvas(Screen Space - Overlay) + CanvasScaler(1080x1920, 세로 기준) + EventSystem 생성
//   3) [Bootstrap] 오브젝트에 LoginBootstrapper 부착
//   4) 7개 화면 구성:
//        - LoginSelectView   : Google/이메일/익명 버튼 + 상태 텍스트
//        - EmailLoginView     : 이메일/비번 입력 + 로그인/회원가입/비번찾기/뒤로 버튼
//        - SignUpView         : 이메일/비번/비번확인 입력 + 회원가입/뒤로 버튼
//        - EmailVerifyView    : 이메일 표시 + 인증완료/재발송 버튼
//        - PasswordResetView  : 이메일 입력 + 발송/뒤로 버튼
//        - AnonymousWarningPopup : 경고 텍스트 + 계정만들기/계속익명 버튼 (오버레이)
//        - (ConfirmPopup는 종료/네트워크오류 공용 — 1개 배치 후 양쪽에 연결)
//   5) 각 View 컴포넌트의 private [SerializeField] 필드를 SerializedObject 로 자동 주입
//   6) LoginBootstrapper 의 View 참조 필드 자동 주입
//   7) Build Settings 의 Scene 목록에 Login.unity 등록 (Login=0, Lobby=1, Game=2)
//
// 주의:
//   - 이 스크립트는 "골격(레이아웃 + 참조 연결)"만 만든다. 실제 비주얼(배경 이미지,
//     폰트, 색상, 아이콘)은 디자인 단계에서 별도로 다듬는다.
//   - private 필드 주입은 리플렉션이 아닌 SerializedObject/FindProperty 방식을 사용한다
//     (프로젝트의 기존 Setup 스크립트 관례와 동일, 컴파일 의존성 없이 안전).
//
// Editor 전용 — 빌드에는 포함되지 않는다 (Assets/Editor 폴더).
// ============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hexiege.Bootstrap;
using Hexiege.Presentation;

namespace HexiegeEditor
{
    /// <summary>
    /// Login.unity 씬을 생성하고 UI 배치 + Inspector 참조 연결을 자동 수행하는 에디터 도구.
    /// </summary>
    public static class SetupLoginScene
    {
        // ====================================================================
        // 상수 — 레이아웃/경로
        // ====================================================================

        /// <summary>생성할 씬 파일 경로.</summary>
        private const string SceneAssetPath = "Assets/_Project/Scenes/Login.unity";

        /// <summary>9:16 세로 모드 기준 해상도 (CanvasScaler Reference Resolution).</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        // 오버레이/텍스트 색상 — 스프라이트로 대체되지 않는 UI 요소에 사용한다.
        private static readonly Color OverlayBg = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color TextColor = Color.black;

        // ====================================================================
        // 메뉴 진입점
        // ====================================================================

        /// <summary>
        /// 메뉴에서 호출되는 진입점. 씬을 새로 만들고 전체 UI를 구성한 뒤 저장한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Login 씬 생성")]
        public static void CreateLoginScene()
        {
            // 이미 씬이 있으면 덮어쓸지 사용자에게 확인받는다 (실수로 작업물 삭제 방지).
            if (System.IO.File.Exists(SceneAssetPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Login 씬 이미 존재",
                    $"{SceneAssetPath} 이 이미 존재합니다.\n새로 만들어 덮어쓸까요?",
                    "덮어쓰기", "취소");
                if (!overwrite) return;
            }

            // 1) 빈 씬 생성 (기본 카메라/라이트 포함 → 카메라만 남기고 라이트 제거).
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2) Canvas + EventSystem 생성
            Canvas canvas = CreateCanvas();

            // GameSystemRules_UI.md 규칙 4: 전체화면 배경(Background)은 SafeAreaContainer 밖, Canvas 직속.
            // 노치/홈바 기기에서 배경이 잘리지 않도록 SafeArea 크기와 무관하게 전체를 덮는다.
            GameObject backgroundGo = CreateUIObject("Background", canvas.transform);
            StretchFull(backgroundGo.GetComponent<RectTransform>());
            Image bgImage = backgroundGo.AddComponent<Image>();
            // bg_login.png 배경 스프라이트 적용 (color는 ApplySprite 내부에서 Color.white로 설정).
            bgImage.color = Color.white;
            ApplySprite(bgImage, "bg_login");
            bgImage.raycastTarget = false; // 배경이 터치 이벤트를 막지 않도록

            // GameSystemRules_UI.md 규칙 4: 실제 UI는 SafeAreaContainer 안에 배치.
            // SafeAreaFitter가 노치/홈바 영역을 제외한 안전 영역으로 RectTransform을 자동 조정한다.
            GameObject safeAreaGo = CreateUIObject("SafeAreaContainer", canvas.transform);
            StretchFull(safeAreaGo.GetComponent<RectTransform>());
            safeAreaGo.AddComponent<SafeAreaFitter>();
            Transform safeAreaTransform = safeAreaGo.transform;

            // 3) [Bootstrap] 오브젝트 생성 + LoginBootstrapper 부착
            var bootstrapperGo = new GameObject("[Bootstrap]");
            LoginBootstrapper bootstrapper = bootstrapperGo.AddComponent<LoginBootstrapper>();

            // 4) 루트 View 오브젝트 (LoginRootView) 생성 — 패널들의 부모.
            // safeAreaTransform을 부모로 사용 (canvas.transform이 아님)
            GameObject rootGo = CreateUIObject("LoginRoot", safeAreaTransform);
            StretchFull(rootGo.GetComponent<RectTransform>());
            LoginRootView rootView = rootGo.AddComponent<LoginRootView>();

            // 5) 공용 ConfirmPopup 1개 생성 (종료 확인 + 네트워크 오류에 공유).
            ConfirmPopup confirmPopup = CreateConfirmPopup(safeAreaTransform);

            // 6) 각 화면 패널 생성 + 컴포넌트 참조 연결
            GameObject loadingIndicator = CreateLoadingIndicator(safeAreaTransform);

            LoginSelectView selectView = CreateLoginSelectPanel(rootGo.transform, out GameObject selectPanel);
            EmailLoginView emailLoginView = CreateEmailLoginPanel(rootGo.transform, out GameObject emailLoginPanel);
            SignUpView signUpView = CreateSignUpPanel(rootGo.transform, out GameObject signUpPanel);
            EmailVerifyView verifyView = CreateEmailVerifyPanel(rootGo.transform, out GameObject verifyPanel);
            PasswordResetView resetView = CreatePasswordResetPanel(rootGo.transform, out GameObject resetPanel);
            AnonymousWarningPopup anonPopup = CreateAnonymousWarningPopup(safeAreaTransform);

            // 7) LoginRootView 의 패널/팝업 참조 주입
            WireLoginRootView(rootView, selectPanel, emailLoginPanel, signUpPanel,
                verifyPanel, resetPanel, anonPopup, confirmPopup);

            // 8) LoginBootstrapper 의 View 참조 주입
            WireBootstrapper(bootstrapper, rootView, selectView, emailLoginView,
                signUpView, verifyView, resetView, anonPopup, loadingIndicator);

            // 9) 초기 표시 상태: 모든 패널 비활성, 로딩 인디케이터만 켜둠
            //    (런타임에 LoginBootstrapper 가 자동 로그인 분기 후 적절한 패널을 켠다)
            selectPanel.SetActive(false);
            emailLoginPanel.SetActive(false);
            signUpPanel.SetActive(false);
            verifyPanel.SetActive(false);
            resetPanel.SetActive(false);

            // 10) 씬 저장
            EnsureFolder("Assets/_Project/Scenes");
            bool saved = EditorSceneManager.SaveScene(scene, SceneAssetPath);
            if (!saved)
            {
                Debug.LogError("[SetupLoginScene] 씬 저장 실패.");
                return;
            }

            // 11) Build Settings 에 등록 (Login=0, Lobby=1, Game=2 순서 유지 시도)
            RegisterSceneInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SetupLoginScene] Login 씬 생성 완료: {SceneAssetPath}\n" +
                      "→ 모든 View/팝업 참조가 자동 연결되었습니다. " +
                      "디자인(배경/폰트/색상)은 별도 단계에서 다듬으세요.");
        }

        // ====================================================================
        // Canvas / EventSystem
        // ====================================================================

        /// <summary>
        /// Screen Space - Overlay Canvas 와 9:16 세로 기준 CanvasScaler, EventSystem 을 생성한다.
        /// </summary>
        private static Canvas CreateCanvas()
        {
            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // GameSystemRules_UI.md 규칙 1: Match Width Or Height = 0 (가로 너비 기준).
            // 세로가 긴 기기(21:9 등)에서 캔버스 논리 높이가 자연스럽게 늘어나는 방식으로 대응한다.
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            // EventSystem 이 없으면 UI 입력이 동작하지 않으므로 함께 생성.
            //   프로젝트는 New Input System 단독 모드(activeInputHandler=1)이므로
            //   레거시 StandaloneInputModule 대신 InputSystemUIInputModule 을 사용해야 한다.
            //   (Lobby 씬의 EventSystem 과 동일한 구성)
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            return canvas;
        }

        // ====================================================================
        // 화면별 패널 생성
        // ====================================================================

        /// <summary>로그인 방식 선택 패널을 만들고 LoginSelectView 참조를 연결한다.</summary>
        private static LoginSelectView CreateLoginSelectPanel(Transform parent, out GameObject panel)
        {
            panel = CreatePanel("LoginSelectPanel", parent);
            LoginSelectView view = panel.AddComponent<LoginSelectView>();

            // LoginSelectPanel은 BackButton이 없으므로 ContentArea Padding Top을 좁게 설정한다.
            // ContentArea: VLG를 부착한 컨테이너. Title이 첫 번째 자식.
            Transform contentArea = CreateContentAreaNoBack(panel.transform);

            CreateTitle(contentArea, "HEXIEGE");

            Button google = CreateVlgButton(contentArea, "GoogleLoginButton", "Google로 로그인", "ui_btn_lavender");
            Button email = CreateVlgButton(contentArea, "EmailLoginButton", "이메일로 로그인", "ui_btn_lavender");
            Button anon = CreateVlgButton(contentArea, "AnonymousButton", "익명으로 시작하기", "ui_btn_lavender");
            TextMeshProUGUI status = CreateStatusText(panel.transform);

            var so = new SerializedObject(view);
            so.FindProperty("_googleLoginButton").objectReferenceValue = google;
            so.FindProperty("_emailLoginButton").objectReferenceValue = email;
            so.FindProperty("_anonymousButton").objectReferenceValue = anon;
            so.FindProperty("_statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        /// <summary>이메일 로그인 패널을 만들고 EmailLoginView 참조를 연결한다.</summary>
        private static EmailLoginView CreateEmailLoginPanel(Transform parent, out GameObject panel)
        {
            panel = CreatePanel("EmailLoginPanel", parent);
            EmailLoginView view = panel.AddComponent<EmailLoginView>();

            // BackButton은 패널 직속 — 앵커 고정으로 ContentArea 레이아웃에 영향받지 않는다.
            Button back = CreateBackButton(panel.transform);

            // ContentArea: VLG를 부착한 스크롤 불필요 컨테이너.
            // Title이 첫 번째 자식으로 들어가고 나머지 콘텐츠가 순서대로 쌓인다.
            Transform contentArea = CreateContentArea(panel.transform);

            CreateTitle(contentArea, "이메일 로그인");

            TMP_InputField emailInput = CreateVlgInput(contentArea, "EmailInput", "이메일 주소",
                TMP_InputField.ContentType.EmailAddress);
            TMP_InputField pwInput = CreateVlgInput(contentArea, "PasswordInput", "비밀번호",
                TMP_InputField.ContentType.Password);

            Button forgot = CreateVlgButton(contentArea, "ForgotPasswordButton", "비밀번호 찾기", "ui_btn_lavender");
            Button login = CreateVlgButton(contentArea, "LoginButton", "로그인", "ui_btn_lavender");
            Button signUp = CreateVlgButton(contentArea, "SignUpButton", "회원가입", "ui_btn_lavender");
            TextMeshProUGUI status = CreateStatusText(panel.transform);

            var so = new SerializedObject(view);
            so.FindProperty("_emailInput").objectReferenceValue = emailInput;
            so.FindProperty("_passwordInput").objectReferenceValue = pwInput;
            so.FindProperty("_loginButton").objectReferenceValue = login;
            so.FindProperty("_signUpButton").objectReferenceValue = signUp;
            so.FindProperty("_forgotPasswordButton").objectReferenceValue = forgot;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.FindProperty("_statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        /// <summary>회원가입 패널을 만들고 SignUpView 참조를 연결한다.</summary>
        private static SignUpView CreateSignUpPanel(Transform parent, out GameObject panel)
        {
            panel = CreatePanel("SignUpPanel", parent);
            SignUpView view = panel.AddComponent<SignUpView>();

            // BackButton은 패널 직속 — 앵커 고정으로 ContentArea 레이아웃에 영향받지 않는다.
            Button back = CreateBackButton(panel.transform);

            // ContentArea: VLG를 부착한 컨테이너. Title이 첫 번째 자식.
            Transform contentArea = CreateContentArea(panel.transform);

            CreateTitle(contentArea, "회원가입");

            TMP_InputField emailInput = CreateVlgInput(contentArea, "EmailInput", "이메일 주소",
                TMP_InputField.ContentType.EmailAddress);
            TMP_InputField pwInput = CreateVlgInput(contentArea, "PasswordInput", "비밀번호 (최소 6자)",
                TMP_InputField.ContentType.Password);
            TMP_InputField pwConfirm = CreateVlgInput(contentArea, "PasswordConfirmInput", "비밀번호 확인",
                TMP_InputField.ContentType.Password);

            Button signUp = CreateVlgButton(contentArea, "SignUpButton", "회원가입", "ui_btn_lavender");
            TextMeshProUGUI status = CreateStatusText(panel.transform);

            var so = new SerializedObject(view);
            so.FindProperty("_emailInput").objectReferenceValue = emailInput;
            so.FindProperty("_passwordInput").objectReferenceValue = pwInput;
            so.FindProperty("_passwordConfirmInput").objectReferenceValue = pwConfirm;
            so.FindProperty("_signUpButton").objectReferenceValue = signUp;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.FindProperty("_statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        /// <summary>이메일 인증 대기 패널을 만들고 EmailVerifyView 참조를 연결한다.</summary>
        private static EmailVerifyView CreateEmailVerifyPanel(Transform parent, out GameObject panel)
        {
            panel = CreatePanel("EmailVerifyPanel", parent);
            EmailVerifyView view = panel.AddComponent<EmailVerifyView>();

            // BackButton 없는 패널 — ContentAreaNoBack(Padding Top 80)을 사용한다.
            Transform contentArea = CreateContentAreaNoBack(panel.transform);

            CreateTitle(contentArea, "이메일 인증");
            TextMeshProUGUI emailText = CreateVlgLabel(contentArea, "EmailText", "[이메일 주소]", 28f);
            CreateVlgLabel(contentArea, "GuideText",
                "인증 메일이 발송되었습니다.\n메일함을 확인하고 인증 링크를 클릭해 주세요.", 26f);

            Button check = CreateVlgButton(contentArea, "CheckVerifyButton", "인증 완료 확인", "ui_btn_lavender");
            Button resend = CreateVlgButton(contentArea, "ResendButton", "인증 메일 재발송", "ui_btn_lavender");
            TextMeshProUGUI status = CreateStatusText(panel.transform);

            var so = new SerializedObject(view);
            so.FindProperty("_emailText").objectReferenceValue = emailText;
            so.FindProperty("_statusText").objectReferenceValue = status;
            so.FindProperty("_checkVerifyButton").objectReferenceValue = check;
            so.FindProperty("_resendButton").objectReferenceValue = resend;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        /// <summary>비밀번호 재설정 패널을 만들고 PasswordResetView 참조를 연결한다.</summary>
        private static PasswordResetView CreatePasswordResetPanel(Transform parent, out GameObject panel)
        {
            panel = CreatePanel("PasswordResetPanel", parent);
            PasswordResetView view = panel.AddComponent<PasswordResetView>();

            // BackButton은 패널 직속 — 앵커 고정으로 ContentArea 레이아웃에 영향받지 않는다.
            Button back = CreateBackButton(panel.transform);

            // ContentArea: VLG를 부착한 컨테이너. Title이 첫 번째 자식.
            Transform contentArea = CreateContentArea(panel.transform);

            CreateTitle(contentArea, "비밀번호 재설정");
            CreateVlgLabel(contentArea, "GuideText",
                "가입 시 사용한 이메일 주소를 입력하시면\n재설정 링크를 보내드립니다.", 26f);

            TMP_InputField emailInput = CreateVlgInput(contentArea, "EmailInput", "이메일 주소",
                TMP_InputField.ContentType.EmailAddress);
            Button send = CreateVlgButton(contentArea, "SendButton", "재설정 메일 보내기", "ui_btn_lavender");
            TextMeshProUGUI status = CreateStatusText(panel.transform);

            var so = new SerializedObject(view);
            so.FindProperty("_emailInput").objectReferenceValue = emailInput;
            so.FindProperty("_sendButton").objectReferenceValue = send;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.FindProperty("_statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        // ====================================================================
        // 팝업 생성
        // ====================================================================

        /// <summary>
        /// 익명 경고 팝업을 생성한다.
        /// 구조: BlockingOverlay(전체 차단) + PopupBox(AnimatedPanel + VLG) + 텍스트 + 버튼 2개.
        /// </summary>
        private static AnonymousWarningPopup CreateAnonymousWarningPopup(Transform parent)
        {
            GameObject root = CreateUIObject("AnonymousWarningPopup", parent);
            StretchFull(root.GetComponent<RectTransform>());
            AnonymousWarningPopup popup = root.AddComponent<AnonymousWarningPopup>();

            GameObject overlay = CreateOverlay(root.transform, "BlockingOverlay");
            GameObject box = CreatePopupBox(root.transform, "PopupBox", 720f, 760f,
                out AnimatedPanel animPanel);

            // PopupBox 우측 상단에 닫기 버튼 배치 (VLG 영향을 받지 않도록 ignoreLayout=true)
            GameObject closeGo = CreateUIObject("CloseButton", box.transform);
            RectTransform closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.833f, 0.895f);  // 720×760 기준 우측 상단
            closeRt.anchorMax = new Vector2(1.0f, 1.0f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = Vector2.zero;
            closeRt.sizeDelta = Vector2.zero;
            LayoutElement closeLe = closeGo.AddComponent<LayoutElement>();
            closeLe.ignoreLayout = true;
            Image closeImg = closeGo.AddComponent<Image>();
            ApplySprite(closeImg, "ui_btn_cancel");
            closeGo.AddComponent<Button>().targetGraphic = closeImg;

            // AnonymousWarningPopup 내부 VLG — 박스 안 요소를 위에서 아래로 순서대로 쌓는다.
            VerticalLayoutGroup boxVlg = box.AddComponent<VerticalLayoutGroup>();
            boxVlg.padding = new RectOffset(40, 40, 40, 40);
            boxVlg.spacing = 24f;
            boxVlg.childAlignment = TextAnchor.UpperCenter;
            boxVlg.childControlWidth = true;
            boxVlg.childForceExpandWidth = true;
            boxVlg.childControlHeight = true;
            boxVlg.childForceExpandHeight = false;

            // 라벨과 버튼을 CreateVlgLabel, CreateVlgButton으로 배치한다.
            TextMeshProUGUI warning = CreateVlgLabel(box.transform, "WarningText", string.Empty, 28f);
            Button create = CreateVlgButton(box.transform, "CreateAccountButton", "계정 만들기", "ui_btn_lavender");
            Button cont = CreateVlgButton(box.transform, "ContinueAnonymousButton", "계속 익명으로 진행", "ui_btn_lavender");

            var so = new SerializedObject(popup);
            so.FindProperty("_panel").objectReferenceValue = animPanel;
            so.FindProperty("_blockingOverlay").objectReferenceValue = overlay;
            so.FindProperty("_warningText").objectReferenceValue = warning;
            so.FindProperty("_createAccountButton").objectReferenceValue = create;
            so.FindProperty("_continueAnonymousButton").objectReferenceValue = cont;
            so.ApplyModifiedPropertiesWithoutUndo();

            // AnimatedPanel 이 Awake 에서 SetActive(false) 처리하므로 초기엔 보이지 않음.
            return popup;
        }

        /// <summary>
        /// 공용 ConfirmPopup 을 생성한다 (앱 종료 확인 + 네트워크 오류 안내에 공유).
        /// </summary>
        private static ConfirmPopup CreateConfirmPopup(Transform parent)
        {
            GameObject root = CreateUIObject("ConfirmPopup", parent);
            StretchFull(root.GetComponent<RectTransform>());
            ConfirmPopup popup = root.AddComponent<ConfirmPopup>();

            GameObject overlay = CreateOverlay(root.transform, "BlockingOverlay");
            GameObject box = CreatePopupBox(root.transform, "PopupBox", 720f, 460f,
                out AnimatedPanel animPanel);

            // PopupBox 우측 상단에 닫기 버튼 배치 (VLG 영향을 받지 않도록 ignoreLayout=true)
            GameObject closeGo = CreateUIObject("CloseButton", box.transform);
            RectTransform closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.833f, 0.867f);  // 720×460 기준 우측 상단
            closeRt.anchorMax = new Vector2(1.0f, 1.0f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = Vector2.zero;
            closeRt.sizeDelta = Vector2.zero;
            LayoutElement closeLe = closeGo.AddComponent<LayoutElement>();
            closeLe.ignoreLayout = true;
            Image closeImg = closeGo.AddComponent<Image>();
            ApplySprite(closeImg, "ui_btn_cancel");
            closeGo.AddComponent<Button>().targetGraphic = closeImg;

            // ConfirmPopup 내부 VLG — 박스 안 요소를 위에서 아래로 순서대로 쌓는다.
            VerticalLayoutGroup boxVlg = box.AddComponent<VerticalLayoutGroup>();
            boxVlg.padding = new RectOffset(40, 40, 40, 40);
            boxVlg.spacing = 24f;
            boxVlg.childAlignment = TextAnchor.UpperCenter;
            boxVlg.childControlWidth = true;
            boxVlg.childForceExpandWidth = true;
            boxVlg.childControlHeight = true;
            boxVlg.childForceExpandHeight = false;

            // 메시지 텍스트
            TextMeshProUGUI message = CreateVlgLabel(box.transform, "MessageText", "메시지", 30f);

            // 확인/취소 버튼 컨테이너 (HLG로 좌우 배치)
            GameObject btnContainer = CreateUIObject("ButtonContainer", box.transform);
            RectTransform btnContainerRt = btnContainer.GetComponent<RectTransform>();
            btnContainerRt.sizeDelta = Vector2.zero;
            LayoutElement btnContainerLe = btnContainer.AddComponent<LayoutElement>();
            btnContainerLe.preferredHeight = 110f;
            btnContainerLe.flexibleHeight = 0f;
            HorizontalLayoutGroup btnHlg = btnContainer.AddComponent<HorizontalLayoutGroup>();
            btnHlg.spacing = 20f;
            btnHlg.childControlWidth = true;
            btnHlg.childForceExpandWidth = true;
            btnHlg.childControlHeight = true;
            btnHlg.childForceExpandHeight = true;

            // 확인/취소 버튼 (HLG 자식 — 각각 50% 너비 차지)
            Button confirm = CreateVlgButton(btnContainer.transform, "ConfirmButton", "확인", "ui_btn_lavender");
            Button cancel = CreateVlgButton(btnContainer.transform, "CancelButton", "취소", "ui_btn_lavender");

            TextMeshProUGUI confirmLabel = confirm.GetComponentInChildren<TextMeshProUGUI>();
            TextMeshProUGUI cancelLabel = cancel.GetComponentInChildren<TextMeshProUGUI>();

            var so = new SerializedObject(popup);
            so.FindProperty("_blockingOverlay").objectReferenceValue = overlay;
            so.FindProperty("_panel").objectReferenceValue = animPanel;
            so.FindProperty("_messageText").objectReferenceValue = message;
            so.FindProperty("_confirmButton").objectReferenceValue = confirm;
            so.FindProperty("_cancelButton").objectReferenceValue = cancel;
            so.FindProperty("_confirmButtonText").objectReferenceValue = confirmLabel;
            so.FindProperty("_cancelButtonText").objectReferenceValue = cancelLabel;

            // UIColorConfig 에셋을 GUID 기반으로 로드해 버튼 색상 설정을 주입한다.
            // ConfirmPopup.Awake()에서 이 설정을 읽어 확인/취소 버튼 배경색을 적용한다.
            // (ScriptableObject로 로드하는 이유: 이 에디터 스크립트에 Hexiege.Infrastructure
            //  using 의존성을 추가하지 않기 위함. 실제 에셋 타입은 UIColorConfig이며,
            //  _colorConfig 프로퍼티는 해당 타입만 받으므로 잘못된 에셋이 들어갈 위험은 없다.)
            string[] colorConfigGuids = AssetDatabase.FindAssets("t:UIColorConfig");
            if (colorConfigGuids.Length > 0)
            {
                var colorConfig = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(colorConfigGuids[0]));
                so.FindProperty("_colorConfig").objectReferenceValue = colorConfig;
            }
            else
            {
                Debug.LogWarning("[SetupLoginScene] UIColorConfig 에셋을 찾을 수 없습니다. " +
                                 "ConfirmPopup 버튼 색상이 기본값으로 표시됩니다.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            return popup;
        }

        /// <summary>자동 로그인 / 비동기 요청 중 표시하는 단순 로딩 인디케이터.</summary>
        private static GameObject CreateLoadingIndicator(Transform parent)
        {
            GameObject root = CreateUIObject("LoadingIndicator", parent);
            StretchFull(root.GetComponent<RectTransform>());

            // 반투명 배경 + 중앙 스피너 이미지
            Image bg = root.AddComponent<Image>();
            bg.color = OverlayBg;

            // 스피너 이미지 — 화면 중앙에 80x80px 비율 배치 (1080×1920 기준)
            // 실제 회전 애니메이션은 런타임 컴포넌트에서 처리; 여기선 이미지만 배치
            GameObject spinnerGo = CreateUIObject("Spinner", root.transform);
            RectTransform spinnerRt = spinnerGo.GetComponent<RectTransform>();
            // 너비 80px(1080기준) = 0.074, 높이 80px(1920기준) 중앙 배치
            spinnerRt.anchorMin = new Vector2(0.426f, 0.479f);  // (460/1080, (960-40)/1920)
            spinnerRt.anchorMax = new Vector2(0.574f, 0.521f);  // (620/1080, (960+40)/1920)
            spinnerRt.pivot = new Vector2(0.5f, 0.5f);
            spinnerRt.anchoredPosition = Vector2.zero;
            spinnerRt.sizeDelta = Vector2.zero;
            Image spinnerImg = spinnerGo.AddComponent<Image>();
            ApplySprite(spinnerImg, "ui_spinner_hexorb", preserveAspect: true);

            root.SetActive(false); // 기본 숨김
            return root;
        }

        // ====================================================================
        // 참조 연결
        // ====================================================================

        /// <summary>LoginRootView 의 패널/팝업 SerializeField 를 주입한다.</summary>
        private static void WireLoginRootView(LoginRootView rootView,
            GameObject selectPanel, GameObject emailLoginPanel, GameObject signUpPanel,
            GameObject verifyPanel, GameObject resetPanel,
            AnonymousWarningPopup anonPopup, ConfirmPopup confirmPopup)
        {
            var so = new SerializedObject(rootView);
            so.FindProperty("_loginSelectPanel").objectReferenceValue = selectPanel;
            so.FindProperty("_emailLoginPanel").objectReferenceValue = emailLoginPanel;
            so.FindProperty("_signUpPanel").objectReferenceValue = signUpPanel;
            so.FindProperty("_emailVerifyPanel").objectReferenceValue = verifyPanel;
            so.FindProperty("_passwordResetPanel").objectReferenceValue = resetPanel;
            so.FindProperty("_anonymousWarningPopup").objectReferenceValue = anonPopup;
            // 종료 확인과 네트워크 오류는 동일 ConfirmPopup 을 공유한다.
            so.FindProperty("_confirmPopup").objectReferenceValue = confirmPopup;
            so.FindProperty("_networkErrorPopup").objectReferenceValue = confirmPopup;
            // _headerText 는 선택값(null 허용) — 비워둔다.
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>LoginBootstrapper 의 View 참조 SerializeField 를 주입한다.</summary>
        private static void WireBootstrapper(LoginBootstrapper bootstrapper,
            LoginRootView rootView, LoginSelectView selectView, EmailLoginView emailLoginView,
            SignUpView signUpView, EmailVerifyView verifyView, PasswordResetView resetView,
            AnonymousWarningPopup anonPopup, GameObject loadingIndicator)
        {
            var so = new SerializedObject(bootstrapper);
            so.FindProperty("_rootView").objectReferenceValue = rootView;
            so.FindProperty("_loginSelectView").objectReferenceValue = selectView;
            so.FindProperty("_emailLoginView").objectReferenceValue = emailLoginView;
            so.FindProperty("_signUpView").objectReferenceValue = signUpView;
            so.FindProperty("_emailVerifyView").objectReferenceValue = verifyView;
            so.FindProperty("_passwordResetView").objectReferenceValue = resetView;
            so.FindProperty("_anonymousWarningPopup").objectReferenceValue = anonPopup;
            so.FindProperty("_loadingIndicator").objectReferenceValue = loadingIndicator;
            // _nextSceneName 은 기본값("Lobby") 유지.
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ====================================================================
        // UI 생성 헬퍼
        // ====================================================================

        /// <summary>RectTransform 을 가진 빈 UI 오브젝트를 만든다.</summary>
        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>
        /// 파일명 키워드로 스프라이트를 GUID 기반 검색하여 로드한다.
        /// 파일명 공백 등의 이슈를 피하기 위해 키워드 검색 방식을 사용한다.
        /// </summary>
        private static Sprite LoadSprite(string nameKeyword)
        {
            string[] guids = AssetDatabase.FindAssets($"{nameKeyword} t:Sprite");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[SetupLoginScene] 스프라이트 '{nameKeyword}'을 찾을 수 없습니다.");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// Image 컴포넌트에 스프라이트를 적용한다.
        /// 스프라이트를 사용할 때는 color를 Color.white로 설정해 원색이 표시되도록 한다.
        /// preserveAspect: 아이콘처럼 비율 유지가 필요한 경우 true로 설정한다.
        /// </summary>
        private static void ApplySprite(Image img, string nameKeyword, bool preserveAspect = false)
        {
            Sprite sprite = LoadSprite(nameKeyword);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = preserveAspect;
                img.color = Color.white; // 스프라이트 원색 표시
            }
        }

        /// <summary>전체 화면을 덮는 투명 컨테이너 패널을 만든다.</summary>
        private static GameObject CreatePanel(string name, Transform parent)
        {
            // 로그인 씬 전용 씬이므로 씬 배경(bg_login)이 이미 존재한다.
            // 각 뷰는 패널 배경 없이 투명 컨테이너로 구성한다.
            // 오버레이 팝업(ConfirmPopup, AnonymousWarningPopup)은 CreatePopupBox를 사용하므로 이 메서드와 무관하다.
            GameObject panel = CreateUIObject(name, parent);
            StretchFull(panel.GetComponent<RectTransform>());
            return panel;
        }

        /// <summary>
        /// ContentArea VLG 안의 첫 번째 자식으로 들어가는 제목 텍스트를 만든다.
        ///
        /// VLG ChildControlHeight=true 환경이므로 anchoredPosition 대신
        /// LayoutElement.preferredHeight로 높이를 지정한다.
        /// </summary>
        private static TextMeshProUGUI CreateTitle(Transform parent, string text)
        {
            GameObject go = CreateUIObject("Title", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            // VLG가 크기를 결정하므로 sizeDelta는 0으로 초기화.
            rt.sizeDelta = Vector2.zero;

            // VLG에서 선호 높이를 인식할 수 있도록 LayoutElement를 추가한다.
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 200f;   // 타이틀 영역 높이 (기존 Label sizeDelta.y=160 기준)
            le.flexibleHeight = 0f;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 68f;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(tmp, bold: true); // 타이틀은 Bold
            return tmp;
        }

        /// <summary>
        /// ContentArea 컨테이너를 만들고 VerticalLayoutGroup을 부착한다 (BackButton 있는 패널용).
        ///
        /// 구조: 패널 전체를 차지하는 스트레치 RectTransform + VLG.
        /// BackButton이 패널 직속에 앵커 고정(좌상단)으로 배치되고,
        /// ContentArea 안에서는 VLG가 자식들을 위에서 아래로 순서대로 쌓는다.
        ///
        /// VLG 설정:
        ///   - Padding Top=200 — BackButton(높이 120, 상단 앵커 40px 오프셋) 아래에서 시작
        ///   - Padding Bottom=160, Left=60, Right=60 — 상하좌우 여백
        ///   - Spacing=24 — 자식 요소 간 간격
        ///   - ChildControlHeight=true, ChildForceExpandHeight=false
        ///     → 각 자식의 LayoutElement.preferredHeight를 그대로 반영하고 추가 늘리지 않음
        ///   - ChildControlWidth=true, ChildForceExpandWidth=true
        ///     → 자식이 ContentArea 가로폭을 꽉 채움
        /// </summary>
        private static Transform CreateContentArea(Transform parent)
        {
            GameObject go = CreateUIObject("ContentArea", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            // 패널 전체에 스트레치 — VLG가 패널 크기 전체를 사용한다.
            StretchFull(rt);

            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            // Padding Top=200: BackButton(높이 120, 상단 40px 오프셋 → 하단 끝=160px)보다
            // 충분한 여백을 주어 Title이 BackButton에 겹치지 않도록 한다.
            vlg.padding = new RectOffset(60, 60, 200, 160);
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            return go.transform;
        }

        /// <summary>
        /// ContentArea 컨테이너를 만들고 VerticalLayoutGroup을 부착한다 (BackButton 없는 패널용).
        ///
        /// BackButton이 없으므로 Padding Top을 80으로 줄여 콘텐츠가 화면 상단에 더 가깝게 배치된다.
        /// </summary>
        private static Transform CreateContentAreaNoBack(Transform parent)
        {
            GameObject go = CreateUIObject("ContentArea", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            StretchFull(rt);

            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            // BackButton이 없으므로 Top 여백을 80으로 줄인다.
            vlg.padding = new RectOffset(60, 60, 80, 160);
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            return go.transform;
        }

        /// <summary>화면 하단의 상태/오류 메시지 텍스트를 만든다.</summary>
        private static TextMeshProUGUI CreateStatusText(Transform parent)
        {
            GameObject go = CreateUIObject("StatusText", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            // GameSystemRules_UI.md 규칙 2: sizeDelta 금지 → 앵커 비율 기반으로 배치.
            // 화면 하단 고정: 1080px 기준 좌우 60px 여백, 하단 80~200px 위치.
            rt.anchorMin = new Vector2(0.056f, 0.042f);
            rt.anchorMax = new Vector2(0.944f, 0.104f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = 32f;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            // 줄바꿈은 TMP 기본값이 활성(Normal)이므로 별도 설정하지 않는다.
            ApplyFont(tmp, bold: false);
            return tmp;
        }

        /// <summary>
        /// VLG 자식용 버튼을 만든다.
        ///
        /// ContentArea VLG 안에서 사용되므로 anchoredPosition 대신
        /// LayoutElement.preferredHeight로 높이를 지정한다.
        /// 가로는 VLG의 ChildForceExpandWidth=true에 의해 ContentArea 너비를 꽉 채운다.
        /// </summary>
        private static Button CreateVlgButton(Transform parent, string name, string label, string spriteKeyword)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            // VLG가 크기를 결정하므로 sizeDelta는 0으로 초기화.
            rt.sizeDelta = Vector2.zero;

            Image img = go.AddComponent<Image>();
            // 버튼별 스프라이트를 키워드로 검색해 적용한다 (색상은 ApplySprite 내부에서 Color.white).
            ApplySprite(img, spriteKeyword);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // VLG에서 선호 높이를 인식할 수 있도록 LayoutElement를 추가한다.
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 160f;
            le.flexibleHeight = 0f;

            // 버튼 텍스트 라벨
            GameObject labelGo = CreateUIObject("Label", go.transform);
            StretchFull(labelGo.GetComponent<RectTransform>());
            TextMeshProUGUI tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 42f;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(tmp, bold: true); // 버튼 라벨은 Bold

            return btn;
        }

        /// <summary>
        /// VLG 자식용 TMP_InputField를 만든다.
        ///
        /// ContentArea VLG 안에서 사용되므로 LayoutElement.preferredHeight로 높이를 지정한다.
        /// </summary>
        private static TMP_InputField CreateVlgInput(Transform parent, string name, string placeholder,
            TMP_InputField.ContentType contentType)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = Vector2.zero;

            Image img = go.AddComponent<Image>();
            // 입력 필드 배경 — ui_panel_dark.png 적용 + 약간 반투명.
            ApplySprite(img, "ui_panel_dark");
            img.color = new Color(1f, 1f, 1f, 0.85f);

            TMP_InputField input = go.AddComponent<TMP_InputField>();
            input.contentType = contentType;

            // VLG에서 선호 높이를 인식할 수 있도록 LayoutElement를 추가한다.
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 130f;
            le.flexibleHeight = 0f;

            // TextArea 뷰포트 (TMP InputField 표준 구조)
            GameObject textArea = CreateUIObject("Text Area", go.transform);
            RectTransform taRt = textArea.GetComponent<RectTransform>();
            // StretchFull 대신 직접 앵커 비율로 TextArea 내부 여백을 표현한다.
            // GameSystemRules_UI.md 규칙 2: offsetMin/offsetMax 고정 픽셀값 사용 금지.
            // 부모(InputField)의 런타임 크기는 너비 960px(VLG ContentArea: 1080 - 좌우 padding 60씩),
            // 높이 100px(LayoutElement.preferredHeight)이다.
            // 원하는 여백(수평 20px, 수직 8px)을 부모 크기로 나눠 앵커 비율로 환산한다.
            //   anchorMin.x = 20 / 960  ≈ 0.021,  anchorMin.y = 8 / 100 = 0.080
            //   anchorMax.x = 1 - 20/960 ≈ 0.979, anchorMax.y = 1 - 8/100 = 0.920
            taRt.anchorMin = new Vector2(0.021f, 0.080f);
            taRt.anchorMax = new Vector2(0.979f, 0.920f);
            taRt.pivot = new Vector2(0.5f, 0.5f);
            taRt.anchoredPosition = Vector2.zero;
            taRt.sizeDelta = Vector2.zero;
            textArea.AddComponent<RectMask2D>();

            // 플레이스홀더
            GameObject ph = CreateUIObject("Placeholder", textArea.transform);
            StretchFull(ph.GetComponent<RectTransform>());
            TextMeshProUGUI phTmp = ph.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = 38f;
            phTmp.color = Color.black;
            phTmp.alignment = TextAlignmentOptions.Left;
            ApplyFont(phTmp, bold: false);

            // 입력 텍스트
            GameObject txt = CreateUIObject("Text", textArea.transform);
            StretchFull(txt.GetComponent<RectTransform>());
            TextMeshProUGUI txtTmp = txt.AddComponent<TextMeshProUGUI>();
            txtTmp.text = string.Empty;
            txtTmp.fontSize = 38f;
            txtTmp.color = TextColor;
            txtTmp.alignment = TextAlignmentOptions.Left;
            ApplyFont(txtTmp, bold: false);

            input.textViewport = taRt;
            input.textComponent = txtTmp;
            input.placeholder = phTmp;

            return input;
        }

        /// <summary>
        /// VLG 자식용 텍스트 라벨을 만든다.
        ///
        /// ContentArea VLG 안에서 사용되므로 anchoredPosition 대신
        /// LayoutElement.preferredHeight로 높이를 지정한다.
        /// </summary>
        private static TextMeshProUGUI CreateVlgLabel(Transform parent, string name, string text, float fontSize)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = Vector2.zero;

            // VLG에서 선호 높이를 인식할 수 있도록 LayoutElement를 추가한다.
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 150f;
            le.flexibleHeight = 0f;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(tmp, bold: false); // 안내 텍스트는 Light
            return tmp;
        }

        /// <summary>좌상단 모서리에 뒤로가기(←) 버튼을 만든다.</summary>
        private static Button CreateBackButton(Transform parent)
        {
            GameObject go = CreateUIObject("BackButton", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            // GameSystemRules_UI.md 규칙 2: sizeDelta 금지 → 앵커 비율 기반으로 배치.
            // 좌상단 고정: 1080×1920 기준 좌측 40px, 상단 40px에서 120×120px 크기.
            rt.anchorMin = new Vector2(0.037f, 0.917f);
            rt.anchorMax = new Vector2(0.148f, 0.979f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // ui_icon_back.png는 투명 배경의 완성형 버튼 그래픽이므로 직접 버튼 Image로 사용한다.
            // (별도 배경을 쓰면 배경색이 흰색으로 노출되는 문제 발생 — 로비 PrevButton 동일 패턴 적용)
            Image img = go.AddComponent<Image>();
            ApplySprite(img, "ui_icon_back", preserveAspect: true);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            return btn;
        }

        /// <summary>화면 전체를 덮는 반투명 입력 차단 오버레이를 만든다.</summary>
        private static GameObject CreateOverlay(Transform parent, string name)
        {
            GameObject go = CreateUIObject(name, parent);
            StretchFull(go.GetComponent<RectTransform>());
            Image img = go.AddComponent<Image>();
            img.color = OverlayBg;
            img.raycastTarget = true; // 뒤쪽 입력 차단
            return go;
        }

        /// <summary>
        /// 중앙에 뜨는 팝업 박스를 만들고 AnimatedPanel(+CanvasGroup)을 부착한다.
        /// </summary>
        private static GameObject CreatePopupBox(Transform parent, string name,
            float width, float height, out AnimatedPanel animPanel)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            // GameSystemRules_UI.md 규칙 2: sizeDelta 금지 → 앵커 비율 기반으로 배치.
            // 캔버스 기준(1080×1920)으로 팝업 박스 크기를 앵커 비율로 변환한다.
            float halfW = width / 2f / 1080f;
            float halfH = height / 2f / 1920f;
            rt.anchorMin = new Vector2(0.5f - halfW, 0.5f - halfH);
            rt.anchorMax = new Vector2(0.5f + halfW, 0.5f + halfH);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            Image img = go.AddComponent<Image>();
            // 팝업 박스 배경 — ui_panel_light.png 적용.
            ApplySprite(img, "ui_panel_light");
            img.color = Color.white;

            // AnimatedPanel 이 Awake 에서 CanvasGroup 을 자동 추가하므로 여기선 부착만 한다.
            animPanel = go.AddComponent<AnimatedPanel>();
            return go;
        }

        // ====================================================================
        // RectTransform / 레이아웃 유틸
        // ====================================================================

        /// <summary>
        /// Maplestory 폰트 에셋을 GUID 기반으로 로드한다.
        /// bold=false → Maplestory Light SDF, bold=true → Maplestory Bold SDF.
        /// 폰트 파일이 없으면 null 반환 (기본 폰트 유지).
        /// </summary>
        private static TMP_FontAsset LoadMapleFont(bool bold = false)
        {
            string searchName = bold ? "Maplestory Bold SDF" : "Maplestory Light SDF";
            // t:TMP_FontAsset 필터로 TMP 폰트 에셋만 검색한다.
            string[] guids = AssetDatabase.FindAssets($"{searchName} t:TMP_FontAsset");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[SetupLoginScene] 폰트 '{searchName}'을 찾을 수 없습니다. " +
                                 "기본 폰트를 사용합니다. (Assets/_Project/Fonts/ 확인 필요)");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// TMP 컴포넌트에 Maplestory 폰트를 적용한다.
        /// null 반환 시 기본 폰트 유지.
        /// </summary>
        private static void ApplyFont(TextMeshProUGUI tmp, bool bold = false)
        {
            TMP_FontAsset font = LoadMapleFont(bold);
            if (font != null) tmp.font = font;
        }

        /// <summary>RectTransform 을 부모 전체로 스트레치시킨다.</summary>
        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ====================================================================
        // 폴더 / Build Settings
        // ====================================================================

        /// <summary>폴더가 없으면 생성한다 (중첩 경로 대응).</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// Login.unity 를 Build Settings 의 Scene 목록 맨 앞(index 0)에 등록한다.
        /// 이미 등록되어 있으면 중복 추가하지 않는다.
        /// </summary>
        private static void RegisterSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            // 이미 등록되어 있는지 확인
            bool exists = scenes.Exists(s => s.path == SceneAssetPath);
            if (!exists)
            {
                // 맨 앞에 삽입하여 Login = build index 0 이 되도록 한다.
                scenes.Insert(0, new EditorBuildSettingsScene(SceneAssetPath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[SetupLoginScene] Build Settings 에 Login.unity 를 index 0 으로 등록했습니다.");
            }
            else
            {
                Debug.Log("[SetupLoginScene] Login.unity 가 이미 Build Settings 에 등록되어 있습니다.");
            }
        }
    }
}
