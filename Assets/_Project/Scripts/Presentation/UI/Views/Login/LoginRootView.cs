// ============================================================================
// LoginRootView.cs
// Login.unity 씬의 루트 View. 패널 전환 + Back 스택 관리.
//
// 역할:
//   - 모든 하위 View(LoginSelect, EmailLogin, SignUp, EmailVerify, PasswordReset)
//     CanvasGroup 을 참조로 보유하고 한 번에 한 패널만 활성화.
//   - 패널 전환 시 이전 패널을 Back 스택에 push.
//   - Android 뒤로가기 입력 처리:
//       하위 화면 → 이전 화면으로 복귀.
//       LoginSelectView 화면(스택 비어있음) → 2회 연속 입력 시 앱 종료 확인 팝업.
//   - 익명 경고 팝업은 별도 오버레이 — 스택에 포함하지 않음.
//
// 의존성:
//   - LoginBootstrapper (씬 이동 + 로딩 표시)
//   - LoginUseCase (로그인 상태 조회)
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Hexiege.Application;
using Hexiege.Bootstrap;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 로그인 씬 루트 View. 화면 전환 조율 + Back 스택 관리.
    /// </summary>
    public class LoginRootView : MonoBehaviour
    {
        // ====================================================================
        // 화면 식별자 열거형
        // ====================================================================

        /// <summary>
        /// 로그인 씬 내에서 표시 가능한 패널 종류.
        /// 패널 전환 시 ShowPanel(LoginPanel) 형태로 사용.
        /// </summary>
        public enum LoginPanel
        {
            None,
            LoginSelect,
            EmailLogin,
            SignUp,
            EmailVerify,
            PasswordReset,
            NicknameSetup
        }

        // ====================================================================
        // Inspector 참조 — 패널 CanvasGroups
        // ====================================================================

        [Header("패널 CanvasGroups")]
        [Tooltip("로그인 방식 선택 패널. CanvasGroup으로 표시/숨김 처리.")]
        [SerializeField] private CanvasGroup _loginSelectPanel;

        [Tooltip("이메일 로그인 패널. CanvasGroup으로 표시/숨김 처리.")]
        [SerializeField] private CanvasGroup _emailLoginPanel;

        [Tooltip("이메일 회원가입 패널. CanvasGroup으로 표시/숨김 처리.")]
        [SerializeField] private CanvasGroup _signUpPanel;

        [Tooltip("이메일 인증 대기 패널. CanvasGroup으로 표시/숨김 처리.")]
        [SerializeField] private CanvasGroup _emailVerifyPanel;

        [Tooltip("비밀번호 재설정 패널. CanvasGroup으로 표시/숨김 처리.")]
        [SerializeField] private CanvasGroup _passwordResetPanel;

        [Tooltip("닉네임 설정 패널. CanvasGroup으로 표시/숨김 처리.")]
        [SerializeField] private CanvasGroup _nicknameSetupPanel;

        [Tooltip("닉네임 설정 View. 화면을 열 때 완료 경로(Google/이메일)를 전달하기 위해 참조한다.")]
        [SerializeField] private NicknameSetupView _nicknameSetupView;

        [Header("팝업 (오버레이)")]
        [Tooltip("익명 로그인 경고 팝업. 패널 전환 스택과 무관.")]
        [SerializeField] private AnonymousWarningPopup _anonymousWarningPopup;

        [Tooltip("네트워크 오류 안내 팝업.")]
        [SerializeField] private NetworkErrorPopup _networkErrorPopup;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private LoginBootstrapper _bootstrapper;
        private LoginUseCase _loginUseCase;

        /// <summary>
        /// 화면 이동 이력을 담는 스택. Back 처리 시 pop 한 화면으로 이동.
        /// </summary>
        private readonly Stack<LoginPanel> _backStack = new();

        /// <summary>
        /// 현재 표시 중인 패널.
        /// </summary>
        private LoginPanel _currentPanel = LoginPanel.None;

        /// <summary>
        /// Android 뒤로가기 2회 입력 카운트.
        /// 첫 입력 시간 기록 후 일정 시간(_backDoubleTapWindow) 이내 재입력 시 종료 팝업 표시.
        /// </summary>
        private float _lastBackPressTime = -10f;
        private const float _backDoubleTapWindow = 1.5f;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// LoginBootstrapper 에서 호출. 의존성 주입.
        /// </summary>
        public void Initialize(LoginBootstrapper bootstrapper, LoginUseCase loginUseCase)
        {
            _bootstrapper = bootstrapper;
            _loginUseCase = loginUseCase;
            HideAll();
        }

        // ====================================================================
        // 패널 전환 API
        // ====================================================================

        /// <summary>
        /// 로그인 방식 선택 화면을 표시한다.
        /// Back 스택을 초기화 — 이 화면은 로그인 흐름의 최상단.
        /// </summary>
        public void ShowLoginSelect()
        {
            _backStack.Clear();
            SetActivePanel(LoginPanel.LoginSelect);
        }

        /// <summary>이메일 로그인 화면을 표시한다.</summary>
        public void ShowEmailLogin()
        {
            PushCurrentToStack();
            SetActivePanel(LoginPanel.EmailLogin);
        }

        /// <summary>이메일 회원가입 화면을 표시한다.</summary>
        public void ShowSignUp()
        {
            PushCurrentToStack();
            SetActivePanel(LoginPanel.SignUp);
        }

        /// <summary>이메일 인증 대기 화면을 표시한다.</summary>
        public void ShowEmailVerify()
        {
            PushCurrentToStack();
            SetActivePanel(LoginPanel.EmailVerify);
        }

        /// <summary>비밀번호 재설정 화면을 표시한다.</summary>
        public void ShowPasswordReset()
        {
            PushCurrentToStack();
            SetActivePanel(LoginPanel.PasswordReset);
        }

        /// <summary>
        /// 닉네임 설정 화면을 표시한다.
        /// isGooglePath 값을 NicknameSetupView 에 먼저 전달한 뒤 패널을 전환한다.
        ///   [의미 축소] 이 값은 더 이상 완료 후 이동 경로(라우팅)를 뜻하지 않는다.
        ///   닉네임 설정은 Google/이메일 모두 "첫 로그인 성공 직후"에만 열리고 완료 시 항상 로비로 가므로,
        ///   isGooglePath 는 스킵 시 자동닉네임 접두사 구분("구글" vs "사용자")에만 사용된다.
        /// </summary>
        /// <param name="isGooglePath">
        /// true=Google 로그인 경로(스킵 접두사 "구글"), false=이메일 로그인 경로(스킵 접두사 "사용자").
        /// </param>
        public void ShowNicknameSetup(bool isGooglePath)
        {
            // View 에 경로를 먼저 알려 준다(입력/상태 초기화도 함께 수행됨).
            if (_nicknameSetupView != null)
                _nicknameSetupView.PrepareForShow(isGooglePath);

            PushCurrentToStack();
            SetActivePanel(LoginPanel.NicknameSetup);
        }

        /// <summary>
        /// 익명 경고 팝업을 표시한다.
        /// 별도 오버레이로 동작하며 Back 스택과 무관 — 팝업이 자체적으로 닫힘 처리한다.
        /// </summary>
        public void ShowAnonymousWarning()
        {
            if (_anonymousWarningPopup != null)
                _anonymousWarningPopup.Show();
        }

        /// <summary>
        /// 모든 패널을 숨긴다.
        /// 초기화 시점 또는 로그인 완료 후 씬 이동 직전에 호출.
        /// </summary>
        public void HideAll()
        {
            HideGroup(_loginSelectPanel);
            HideGroup(_emailLoginPanel);
            HideGroup(_signUpPanel);
            HideGroup(_emailVerifyPanel);
            HideGroup(_passwordResetPanel);
            HideGroup(_nicknameSetupPanel);
            _currentPanel = LoginPanel.None;
        }

        // ====================================================================
        // Back 동작
        // ====================================================================

        /// <summary>
        /// 뒤로가기 처리. 하위 화면이면 이전 화면으로, 최상단이면 앱 종료 확인 팝업.
        /// Android 뒤로가기 버튼 / UI Back 버튼 양쪽에서 호출 가능.
        /// </summary>
        public void HandleBack()
        {
            // 스택에 화면이 있으면 이전 화면으로 복귀
            if (_backStack.Count > 0)
            {
                LoginPanel prev = _backStack.Pop();
                SetActivePanel(prev);
                return;
            }

            // 스택이 비어 있음 — 로그인 선택 화면에서의 Back 처리.
            // 2회 연속 입력 시 앱 종료 확인 팝업.
            float now = Time.unscaledTime;
            if (now - _lastBackPressTime <= _backDoubleTapWindow)
            {
                ShowQuitConfirm();
                _lastBackPressTime = -10f;
            }
            else
            {
                _lastBackPressTime = now;
                // 사용자에게 "한 번 더 누르면 종료" 안내가 필요할 경우 토스트 등을 띄울 수 있음(생략).
            }
        }

        /// <summary>
        /// 앱 종료 확인 팝업을 표시한다.
        /// UIManager.ShowConfirm()으로 전역 팝업 사용 — UIManager가 null이면 즉시 종료.
        /// </summary>
        private void ShowQuitConfirm()
        {
            if (UIManager.Instance == null)
            {
                // UIManager 미초기화 상태(씬 직접 진입 등) — 안전을 위해 즉시 종료.
                Debug.LogWarning("[LoginRootView] UIManager가 없어 즉시 종료합니다.");
                UnityEngine.Application.Quit();
                return;
            }

            UIManager.Instance.ShowConfirm(
                message: "앱을 종료하시겠습니까?",
                onConfirm: UnityEngine.Application.Quit,
                onCancel: null,
                confirmLabel: "종료",
                cancelLabel: "취소");
        }

        // ====================================================================
        // Update — Android 뒤로가기 입력 감지
        // ====================================================================

        private void Update()
        {
            // 안드로이드 뒤로가기 = Escape 키 (New Input System).
            //   Editor 에서 ESC 키로 동일 흐름을 테스트할 수 있다.
            //   Keyboard.current가 null이면 키보드 장치가 없는 환경(모바일 실기 등)이므로 건너뛴다.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                HandleBack();
            }
        }

        // ====================================================================
        // 네트워크 오류 표시
        // ====================================================================

        /// <summary>
        /// 네트워크 오류 팝업을 표시한다.
        /// View 들이 Firebase 호출 결과에서 NetworkError 사유를 받았을 때 호출.
        /// </summary>
        public void ShowNetworkErrorPopup()
        {
            if (_networkErrorPopup != null)
                _networkErrorPopup.Show();
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 현재 패널을 Back 스택에 push. None 이면 무시.
        /// </summary>
        private void PushCurrentToStack()
        {
            if (_currentPanel != LoginPanel.None)
                _backStack.Push(_currentPanel);
        }

        /// <summary>
        /// 지정 패널만 활성화하고 나머지는 비활성화.
        /// </summary>
        private void SetActivePanel(LoginPanel panel)
        {
            // 먼저 전체 숨김 (_currentPanel이 LoginPanel.None으로 초기화됨)
            HideAll();
            // HideAll() 내부에서 _currentPanel = LoginPanel.None 되므로
            // 여기서 다시 panel 값으로 덮어쓴다.
            _currentPanel = panel;

            // 지정된 패널만 표시
            switch (panel)
            {
                case LoginPanel.LoginSelect:   ShowGroup(_loginSelectPanel);   break;
                case LoginPanel.EmailLogin:    ShowGroup(_emailLoginPanel);    break;
                case LoginPanel.SignUp:        ShowGroup(_signUpPanel);        break;
                case LoginPanel.EmailVerify:   ShowGroup(_emailVerifyPanel);   break;
                case LoginPanel.PasswordReset: ShowGroup(_passwordResetPanel); break;
                case LoginPanel.NicknameSetup: ShowGroup(_nicknameSetupPanel); break;
            }
        }

        /// <summary>
        /// CanvasGroup을 화면에 표시한다.
        /// alpha를 1로, blocksRaycasts와 interactable을 true로 설정하여 완전히 활성화.
        /// </summary>
        private static void ShowGroup(CanvasGroup cg)
        {
            if (cg == null) return;
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        /// <summary>
        /// CanvasGroup을 화면에서 숨긴다.
        /// alpha를 0으로, blocksRaycasts와 interactable을 false로 설정하여 완전히 비활성화.
        /// SetActive(false)를 쓰지 않아도 렌더링·입력이 차단된다.
        /// </summary>
        private static void HideGroup(CanvasGroup cg)
        {
            if (cg == null) return;
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
    }
}
