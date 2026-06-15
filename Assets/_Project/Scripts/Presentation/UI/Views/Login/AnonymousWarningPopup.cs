// ============================================================================
// AnonymousWarningPopup.cs
// 익명으로 시작하기 버튼 클릭 시 표시되는 경고 팝업.
//
// 역할:
//   - 기기 변경 / 앱 재설치 시 데이터 손실 가능성 안내
//   - "계정 만들기": SignUpView 로 전환 (팝업 닫고 패널 전환)
//   - "계속 익명으로 진행": LoginUseCase.SignInAnonymouslyAsync() 실행 후 Lobby 이동
//   - 팝업 외부 클릭으로는 닫지 않음 — 사용자가 명시적으로 버튼 선택 필요.
//
// 구조:
//   AnimatedPanel + BlockingOverlay 패턴은 ConfirmPopup 과 동일.
//   AuthSystemRules.md 규칙: 즉시 로그인하지 않고 경고 팝업을 먼저 표시.
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
    /// 익명 로그인 경고 팝업.
    /// </summary>
    public class AnonymousWarningPopup : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("팝업 본체")]
        [Tooltip("팝업 등장/퇴장 애니메이션을 담당하는 AnimatedPanel.")]
        [SerializeField] private AnimatedPanel _panel;

        [Tooltip("뒤쪽 입력 차단 오버레이 GameObject. Raycast Target = true.")]
        [SerializeField] private GameObject _blockingOverlay;

        [Header("텍스트")]
        [Tooltip("경고 메시지 텍스트.")]
        [SerializeField] private TextMeshProUGUI _warningText;

        [Header("버튼")]
        [Tooltip("계정 만들기 버튼 → SignUpView 로 전환.")]
        [SerializeField] private Button _createAccountButton;

        [Tooltip("계속 익명으로 진행 버튼 → 익명 로그인 실행.")]
        [SerializeField] private Button _continueAnonymousButton;

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

            if (_createAccountButton != null)
                _createAccountButton.onClick.AddListener(OnCreateAccountClicked);

            if (_continueAnonymousButton != null)
                _continueAnonymousButton.onClick.AddListener(OnContinueAnonymousClicked);

            // 기본 메시지 — Inspector 에서 별도로 설정해도 무방.
            if (_warningText != null && string.IsNullOrEmpty(_warningText.text))
            {
                _warningText.text =
                    "익명으로 시작하면 기기 변경 또는 앱 재설치 시\n" +
                    "게임 데이터를 잃을 수 있습니다.\n\n" +
                    "계정을 만들어 데이터를 안전하게 보관할 수 있습니다.";
            }
        }

        private void OnDestroy()
        {
            if (_createAccountButton != null) _createAccountButton.onClick.RemoveAllListeners();
            if (_continueAnonymousButton != null) _continueAnonymousButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 표시 / 숨김
        // ====================================================================

        /// <summary>팝업 표시.</summary>
        public void Show()
        {
            if (_blockingOverlay != null) _blockingOverlay.SetActive(true);
            if (_panel != null) _panel.Show();
        }

        /// <summary>팝업 숨김.</summary>
        public void Hide()
        {
            if (_blockingOverlay != null) _blockingOverlay.SetActive(false);
            if (_panel != null) _panel.Hide();
        }

        // ====================================================================
        // 버튼 콜백
        // ====================================================================

        /// <summary>
        /// 계정 만들기 → 팝업 닫고 SignUpView 로 전환.
        /// </summary>
        private void OnCreateAccountClicked()
        {
            Hide();
            if (_rootView != null) _rootView.ShowSignUp();
        }

        /// <summary>
        /// 계속 익명으로 진행 → 익명 로그인 실행 후 Lobby 이동.
        /// </summary>
        private async void OnContinueAnonymousClicked()
        {
            // 중복 클릭 방지
            SetInteractable(false);
            _bootstrapper.ShowLoading(true);

            try
            {
                LoginResult result = await _loginUseCase.SignInAnonymouslyAsync();

                if (result == LoginResult.Success)
                {
                    Hide();
                    _bootstrapper.GoToNextScene();
                    return;
                }

                // 실패 — 팝업은 닫고 LoginSelectView 의 상태 텍스트로 안내.
                Hide();
                if (_loginUseCase.LastError != null &&
                    _loginUseCase.LastError.Reason == AuthErrorReason.NetworkError &&
                    _rootView != null)
                {
                    _rootView.ShowNetworkErrorPopup();
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
            if (_createAccountButton != null) _createAccountButton.interactable = on;
            if (_continueAnonymousButton != null) _continueAnonymousButton.interactable = on;
        }
    }
}
