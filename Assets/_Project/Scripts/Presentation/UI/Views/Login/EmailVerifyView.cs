// ============================================================================
// EmailVerifyView.cs
// 이메일 인증 대기 화면.
//
// 역할:
//   - 인증 메일이 발송된 이메일 주소 표시
//   - "인증 완료 확인" 버튼: Firebase 서버에 재쿼리하여 인증 완료 시 Lobby 이동
//   - "재발송" 버튼: 인증 메일 재발송
//
// AuthSystemRules.md 규칙:
//   - 인증 미완료 시 Lobby 이동 차단.
//   - 재발송 가능.
//   - 인증 완료 확인 = ReloadAsync() 후 IsEmailVerified 체크 (FirebaseAuthService 내부).
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
    /// 이메일 인증 대기 View.
    /// </summary>
    public class EmailVerifyView : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("표시")]
        [Tooltip("발송된 이메일 주소를 표시할 텍스트.")]
        [SerializeField] private TextMeshProUGUI _emailText;

        [Tooltip("안내 / 오류 메시지.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("버튼")]
        [Tooltip("인증 완료 확인 버튼 — Firebase 서버에 재쿼리.")]
        [SerializeField] private Button _checkVerifyButton;

        [Tooltip("인증 메일 재발송 버튼.")]
        [SerializeField] private Button _resendButton;

        // ====================================================================
        // 의존성
        // ====================================================================

        private LoginRootView _rootView;
        private LoginUseCase _loginUseCase;
        private LoginBootstrapper _bootstrapper;
        private PlayerProfileUseCase _profileUseCase;

        // ====================================================================
        // 초기화
        // ====================================================================

        public void Initialize(
            LoginRootView rootView,
            LoginUseCase loginUseCase,
            LoginBootstrapper bootstrapper,
            PlayerProfileUseCase profileUseCase)
        {
            _rootView = rootView;
            _loginUseCase = loginUseCase;
            _bootstrapper = bootstrapper;
            _profileUseCase = profileUseCase;

            if (_checkVerifyButton != null) _checkVerifyButton.onClick.AddListener(OnCheckVerifyClicked);
            if (_resendButton != null) _resendButton.onClick.AddListener(OnResendClicked);

            ClearStatus();
        }

        private void OnDestroy()
        {
            if (_checkVerifyButton != null) _checkVerifyButton.onClick.RemoveAllListeners();
            if (_resendButton != null) _resendButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // Unity 생명주기 — 화면 활성화 시 이메일 주소 갱신
        // ====================================================================

        /// <summary>
        /// 화면이 활성화될 때마다 현재 사용자의 이메일을 표시.
        /// LoginRootView 가 패널 전환 시 GameObject.SetActive(true) 를 호출하면 자동 실행됨.
        /// </summary>
        private void OnEnable()
        {
            if (_emailText != null && _loginUseCase != null)
                _emailText.text = _loginUseCase.Email;
        }

        // ====================================================================
        // 인증 완료 확인
        // ====================================================================

        /// <summary>
        /// 인증 완료 확인 버튼 → LoginUseCase.CheckEmailVerifiedAsync().
        /// true 면 UGS 브릿지 후 Lobby 이동, false 면 안내 메시지.
        /// </summary>
        private async void OnCheckVerifyClicked()
        {
            ClearStatus();
            SetInteractable(false);
            _bootstrapper.ShowLoading(true);

            try
            {
                bool verified = await _loginUseCase.CheckEmailVerifiedAsync();

                if (verified)
                {
                    bool isFirst = _profileUseCase != null && await _profileUseCase.IsFirstLogin();
                    if (isFirst)
                    {
                        _rootView.ShowNicknameSetup(isGooglePath: false);
                    }
                    else
                    {
                        _bootstrapper.GoToNextScene();
                    }
                    return;
                }

                // 인증 미완료 — 오류 또는 미인증 상태.
                if (_loginUseCase.LastError != null)
                    ShowError(_loginUseCase.LastError);
                else
                    SetStatus("아직 이메일 인증이 완료되지 않았습니다. 메일함을 다시 확인하세요.");
            }
            finally
            {
                _bootstrapper.ShowLoading(false);
                SetInteractable(true);
            }
        }

        // ====================================================================
        // 인증 메일 재발송
        // ====================================================================

        /// <summary>
        /// 재발송 버튼 → LoginUseCase.SendEmailVerificationAsync().
        /// </summary>
        private async void OnResendClicked()
        {
            ClearStatus();
            SetInteractable(false);
            _bootstrapper.ShowLoading(true);

            try
            {
                bool ok = await _loginUseCase.SendEmailVerificationAsync();

                if (ok)
                    SetStatus("인증 메일을 다시 발송했습니다. 메일함을 확인하세요.");
                else
                    ShowError(_loginUseCase.LastError);
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
            if (_checkVerifyButton != null) _checkVerifyButton.interactable = on;
            if (_resendButton != null) _resendButton.interactable = on;
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
