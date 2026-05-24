// ============================================================================
// LoginSelectView.cs
// 로그인 방식 선택 화면.
//
// 역할:
//   - Google 로그인 / 이메일 로그인 / 익명으로 시작 3가지 진입점 제공
//   - Google 로그인 즉시 실행
//   - 이메일 로그인 → EmailLoginView 로 전환
//   - 익명으로 시작 → AnonymousWarningPopup 표시 (즉시 익명 로그인하지 않음)
//   - 비동기 호출 중에는 버튼 비활성화 + 로딩 인디케이터 표시
//
// AuthSystemRules.md 규칙:
//   - 익명 버튼은 즉시 로그인하지 않고 경고 팝업을 먼저 표시한다.
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
    /// 로그인 방식 선택 화면 View.
    /// </summary>
    public class LoginSelectView : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("버튼")]
        [Tooltip("Google 로그인 버튼.")]
        [SerializeField] private Button _googleLoginButton;

        [Tooltip("이메일 로그인 / 회원가입 화면으로 이동하는 버튼.")]
        [SerializeField] private Button _emailLoginButton;

        [Tooltip("익명으로 시작하기 버튼 (경고 팝업 표시).")]
        [SerializeField] private Button _anonymousButton;

        [Header("상태 표시")]
        [Tooltip("오류 / 안내 메시지 표시 텍스트.")]
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

        /// <summary>
        /// LoginBootstrapper 에서 호출. 의존성 + 버튼 리스너 등록.
        /// </summary>
        public void Initialize(LoginRootView rootView, LoginUseCase loginUseCase, LoginBootstrapper bootstrapper)
        {
            _rootView = rootView;
            _loginUseCase = loginUseCase;
            _bootstrapper = bootstrapper;

            if (_googleLoginButton != null)
                _googleLoginButton.onClick.AddListener(OnGoogleLoginClicked);

            if (_emailLoginButton != null)
                _emailLoginButton.onClick.AddListener(OnEmailLoginClicked);

            if (_anonymousButton != null)
                _anonymousButton.onClick.AddListener(OnAnonymousClicked);

            ClearStatus();
        }

        private void OnDestroy()
        {
            // 리스너 정리 (씬 전환 시 누수 방지)
            if (_googleLoginButton != null) _googleLoginButton.onClick.RemoveAllListeners();
            if (_emailLoginButton != null) _emailLoginButton.onClick.RemoveAllListeners();
            if (_anonymousButton != null) _anonymousButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 버튼 콜백
        // ====================================================================

        /// <summary>
        /// Google 로그인 버튼 클릭. 즉시 LoginUseCase.SignInWithGoogleAsync() 실행.
        /// </summary>
        private async void OnGoogleLoginClicked()
        {
            ClearStatus();
            SetButtonsInteractable(false);
            _bootstrapper.ShowLoading(true);

            try
            {
                LoginResult result = await _loginUseCase.SignInWithGoogleAsync();

                if (result == LoginResult.Success)
                {
                    _bootstrapper.GoToNextScene();
                    return;
                }

                ShowError(_loginUseCase.LastError);
            }
            finally
            {
                _bootstrapper.ShowLoading(false);
                SetButtonsInteractable(true);
            }
        }

        /// <summary>
        /// 이메일 로그인 버튼 클릭 → EmailLoginView 로 전환.
        /// </summary>
        private void OnEmailLoginClicked()
        {
            ClearStatus();
            _rootView.ShowEmailLogin();
        }

        /// <summary>
        /// 익명으로 시작 버튼 클릭 → 경고 팝업 표시 (즉시 로그인 X).
        /// </summary>
        private void OnAnonymousClicked()
        {
            ClearStatus();
            _rootView.ShowAnonymousWarning();
        }

        // ====================================================================
        // UI 상태 헬퍼
        // ====================================================================

        /// <summary>
        /// 비동기 호출 중에는 모든 버튼을 비활성화 — 중복 호출 방지.
        /// </summary>
        private void SetButtonsInteractable(bool interactable)
        {
            if (_googleLoginButton != null) _googleLoginButton.interactable = interactable;
            if (_emailLoginButton != null) _emailLoginButton.interactable = interactable;
            if (_anonymousButton != null) _anonymousButton.interactable = interactable;
        }

        /// <summary>오류 메시지 표시. AuthException 의 Message 를 그대로 사용한다.</summary>
        private void ShowError(AuthException error)
        {
            if (_statusText == null || error == null) return;
            _statusText.text = error.Message;

            // 네트워크 오류는 별도 팝업으로도 안내 (AuthSystemRules.md 오류 처리 규칙 1).
            if (error.Reason == AuthErrorReason.NetworkError && _rootView != null)
                _rootView.ShowNetworkErrorPopup();
        }

        /// <summary>상태 텍스트 초기화.</summary>
        private void ClearStatus()
        {
            if (_statusText != null) _statusText.text = string.Empty;
        }
    }
}
