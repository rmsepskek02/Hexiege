// ============================================================================
// EmailLoginView.cs
// 이메일 로그인 화면.
//
// 역할:
//   - 이메일 + 비밀번호 입력 후 로그인 실행
//   - 회원가입 버튼 → SignUpView 전환
//   - 비밀번호 찾기 → PasswordResetView 전환
//   - 뒤로가기 → LoginRootView.HandleBack()
//
// AuthSystemRules.md 규칙:
//   - 비밀번호 오류 / 가입되지 않은 이메일 모두 통합 메시지("이메일 또는 비밀번호가 올바르지 않습니다").
//     → FirebaseAuthService 의 메시지 변환 시점에 이미 처리됨.
//   - 이메일 미인증 계정은 로그인 차단 후 EmailVerifyView 로 안내.
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
    /// 이메일 로그인 View.
    /// </summary>
    public class EmailLoginView : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("입력 필드")]
        [Tooltip("이메일 입력 필드 (KeyboardType: EmailAddress 권장).")]
        [SerializeField] private TMP_InputField _emailInput;

        [Tooltip("비밀번호 입력 필드 (ContentType: Password).")]
        [SerializeField] private TMP_InputField _passwordInput;

        [Header("버튼")]
        [Tooltip("로그인 실행 버튼.")]
        [SerializeField] private Button _loginButton;

        [Tooltip("회원가입 화면으로 이동.")]
        [SerializeField] private Button _signUpButton;

        [Tooltip("비밀번호 재설정 화면으로 이동.")]
        [SerializeField] private Button _forgotPasswordButton;

        [Tooltip("뒤로가기 (로그인 선택 화면 복귀).")]
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

        // 프로필/닉네임 UseCase. 로그인 성공 후 "최초 로그인" 여부(IsFirstLogin) 판정에 사용한다.
        //   최초면 닉네임 설정 화면을 먼저 표시하고, 아니면 곧바로 로비로 이동한다.
        //   (LoginSelectView 의 Google 로그인 경로와 동일한 패턴)
        private PlayerProfileUseCase _profileUseCase;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// LoginBootstrapper 에서 호출. 의존성 주입 + 버튼 리스너 등록.
        /// </summary>
        /// <param name="profileUseCase">
        /// 최초 로그인 판정(IsFirstLogin)에 사용하는 프로필 UseCase.
        /// 이메일 로그인은 로그인 성공 시점에 이미 UGS 브릿지가 끝나 있어 토큰이 존재하므로,
        /// 이 시점에 닉네임을 저장해도 토큰 에러가 발생하지 않는다.
        /// </param>
        public void Initialize(
            LoginRootView rootView, LoginUseCase loginUseCase, LoginBootstrapper bootstrapper,
            PlayerProfileUseCase profileUseCase)
        {
            _rootView = rootView;
            _loginUseCase = loginUseCase;
            _bootstrapper = bootstrapper;
            _profileUseCase = profileUseCase;

            if (_loginButton != null) _loginButton.onClick.AddListener(OnLoginClicked);
            if (_signUpButton != null) _signUpButton.onClick.AddListener(() => _rootView.ShowSignUp());
            if (_forgotPasswordButton != null) _forgotPasswordButton.onClick.AddListener(() => _rootView.ShowPasswordReset());
            if (_backButton != null) _backButton.onClick.AddListener(() => _rootView.HandleBack());

            ClearStatus();
        }

        private void OnDestroy()
        {
            if (_loginButton != null) _loginButton.onClick.RemoveAllListeners();
            if (_signUpButton != null) _signUpButton.onClick.RemoveAllListeners();
            if (_forgotPasswordButton != null) _forgotPasswordButton.onClick.RemoveAllListeners();
            if (_backButton != null) _backButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 로그인 실행
        // ====================================================================

        /// <summary>
        /// 로그인 버튼 클릭 → LoginUseCase 호출 → 결과별 분기.
        /// </summary>
        private async void OnLoginClicked()
        {
            ClearStatus();

            string email = _emailInput != null ? _emailInput.text.Trim() : string.Empty;
            string password = _passwordInput != null ? _passwordInput.text : string.Empty;

            // 클라이언트 측 1차 검증 — 빈 값이면 Firebase 호출 없이 즉시 안내.
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                SetStatus("이메일과 비밀번호를 모두 입력하세요.");
                return;
            }

            SetInteractable(false);
            _bootstrapper.ShowLoading(true);

            try
            {
                LoginResult result = await _loginUseCase.SignInWithEmailAsync(email, password);

                switch (result)
                {
                    case LoginResult.Success:
                        // 로그인 성공 = UGS 브릿지까지 완료된 상태(토큰 존재).
                        //   최초 로그인(Cloud Save 에 닉네임 없음)이면 닉네임 설정 화면을 먼저 표시하고,
                        //   재로그인이면 곧바로 로비로 이동한다. (Google 경로와 동일 패턴)
                        //   _profileUseCase 가 주입되지 않은 개발 환경을 대비해 null 검사를 함께 둔다.
                        bool isFirst = _profileUseCase != null && await _profileUseCase.IsFirstLogin();
                        if (isFirst)
                            // 이메일 경로 최초 로그인 → 닉네임 화면.
                            //   isGooglePath 는 이제 "라우팅"이 아니라 스킵 시 자동닉네임 접두사 구분용이다.
                            //   이메일 경로이므로 false → 스킵 시 "사용자_" 접두사가 사용된다.
                            _rootView.ShowNicknameSetup(isGooglePath: false);
                        else
                            _bootstrapper.GoToNextScene();
                        return;

                    case LoginResult.NeedsEmailVerification:
                        // 이메일 미인증 — 인증 화면으로 이동하여 재발송/완료 확인 안내.
                        _rootView.ShowEmailVerify();
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
            if (_loginButton != null) _loginButton.interactable = on;
            if (_signUpButton != null) _signUpButton.interactable = on;
            if (_forgotPasswordButton != null) _forgotPasswordButton.interactable = on;
            if (_backButton != null) _backButton.interactable = on;
            if (_emailInput != null) _emailInput.interactable = on;
            if (_passwordInput != null) _passwordInput.interactable = on;
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
