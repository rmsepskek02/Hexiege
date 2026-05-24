// ============================================================================
// SignUpView.cs
// 이메일 회원가입 화면.
//
// 역할:
//   - 이메일 + 비밀번호 + 비밀번호 확인 입력 후 회원가입 실행
//   - 클라이언트 측 1차 검증 (빈 값 / 비밀번호 불일치 / 최소 6자)
//   - 회원가입 성공 시 인증 메일 자동 발송 → EmailVerifyView 로 이동
//
// AuthSystemRules.md 규칙:
//   - 비밀번호 최소 6자(Firebase 기본).
//   - 이미 사용 중인 이메일은 "이미 사용 중인 이메일입니다" 안내.
//   - 회원가입 완료 즉시 인증 메일 발송.
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Application;
using Hexiege.Bootstrap;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 이메일 회원가입 View.
    /// </summary>
    public class SignUpView : MonoBehaviour
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>Firebase 기본 비밀번호 최소 길이.</summary>
        private const int MinPasswordLength = 6;

        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("입력 필드")]
        [Tooltip("이메일 입력 필드.")]
        [SerializeField] private TMP_InputField _emailInput;

        [Tooltip("비밀번호 입력 필드 (ContentType: Password).")]
        [SerializeField] private TMP_InputField _passwordInput;

        [Tooltip("비밀번호 확인 입력 필드 (ContentType: Password).")]
        [SerializeField] private TMP_InputField _passwordConfirmInput;

        [Header("버튼")]
        [Tooltip("회원가입 실행 버튼.")]
        [SerializeField] private Button _signUpButton;

        [Tooltip("뒤로가기 버튼.")]
        [SerializeField] private Button _backButton;

        [Header("상태 표시")]
        [Tooltip("오류 / 안내 메시지.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        // ====================================================================
        // 의존성
        // ====================================================================

        private LoginRootView _rootView;
        private LoginUseCase _loginUseCase;
        private LoginBootstrapper _bootstrapper;

        // ====================================================================
        // 초기화
        // ====================================================================

        public void Initialize(LoginRootView rootView, LoginUseCase loginUseCase, LoginBootstrapper bootstrapper)
        {
            _rootView = rootView;
            _loginUseCase = loginUseCase;
            _bootstrapper = bootstrapper;

            if (_signUpButton != null) _signUpButton.onClick.AddListener(OnSignUpClicked);
            if (_backButton != null) _backButton.onClick.AddListener(() => _rootView.HandleBack());

            ClearStatus();
        }

        private void OnDestroy()
        {
            if (_signUpButton != null) _signUpButton.onClick.RemoveAllListeners();
            if (_backButton != null) _backButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 회원가입 실행
        // ====================================================================

        /// <summary>
        /// 회원가입 버튼 클릭 → 클라 검증 → LoginUseCase.SignUpWithEmailAsync().
        /// 성공 시 인증 메일이 자동 발송되고 EmailVerifyView 로 전환된다.
        /// </summary>
        private async void OnSignUpClicked()
        {
            ClearStatus();

            string email = _emailInput != null ? _emailInput.text.Trim() : string.Empty;
            string password = _passwordInput != null ? _passwordInput.text : string.Empty;
            string confirm = _passwordConfirmInput != null ? _passwordConfirmInput.text : string.Empty;

            // 1차 클라이언트 검증 — Firebase 호출 전에 즉시 안내.
            if (string.IsNullOrWhiteSpace(email))
            {
                SetStatus("이메일을 입력하세요.");
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                SetStatus("비밀번호를 입력하세요.");
                return;
            }
            if (password.Length < MinPasswordLength)
            {
                SetStatus($"비밀번호는 {MinPasswordLength}자 이상이어야 합니다.");
                return;
            }
            if (password != confirm)
            {
                SetStatus("비밀번호가 일치하지 않습니다.");
                return;
            }

            SetInteractable(false);
            _bootstrapper.ShowLoading(true);

            try
            {
                // SignUpWithEmailAsync 는 회원가입 후 자동으로 인증 메일을 발송한다(LoginUseCase 내부).
                LoginResult result = await _loginUseCase.SignUpWithEmailAsync(email, password, displayName: null);

                switch (result)
                {
                    case LoginResult.NeedsEmailVerification:
                        // 회원가입 직후의 정상 흐름 — 인증 대기 화면으로 이동.
                        _rootView.ShowEmailVerify();
                        return;

                    case LoginResult.Success:
                        // 일반적으로 회원가입 직후에는 발생하지 않지만, 안전상 처리.
                        _bootstrapper.GoToNextScene();
                        return;

                    case LoginResult.Failed:
                        ShowError(_loginUseCase.LastError);
                        return;
                }
            }
            finally
            {
                _bootstrapper.ShowLoading(false);
                SetInteractable(true);
            }
        }

        // ====================================================================
        // UI 헬퍼
        // ====================================================================

        private void SetInteractable(bool on)
        {
            if (_signUpButton != null) _signUpButton.interactable = on;
            if (_backButton != null) _backButton.interactable = on;
            if (_emailInput != null) _emailInput.interactable = on;
            if (_passwordInput != null) _passwordInput.interactable = on;
            if (_passwordConfirmInput != null) _passwordConfirmInput.interactable = on;
        }

        private void SetStatus(string text)
        {
            if (_statusText != null) _statusText.text = text ?? string.Empty;
        }

        private void ClearStatus() => SetStatus(string.Empty);

        private void ShowError(AuthException error)
        {
            if (error == null) return;
            SetStatus(error.Message);
            if (error.Reason == AuthErrorReason.NetworkError && _rootView != null)
                _rootView.ShowNetworkErrorPopup();
        }
    }
}
