// ============================================================================
// PasswordResetView.cs
// 비밀번호 재설정 화면.
//
// 역할:
//   - 이메일 입력 후 재설정 링크 발송
//   - 발송 완료 후 안내 메시지 + 잠시 후 EmailLoginView 로 자동 복귀
//
// AuthSystemRules.md 규칙:
//   - 가입되지 않은 이메일에도 동일 "이메일을 확인하세요" 메시지 (보안).
//     → LoginUseCase.SendPasswordResetEmailAsync 는 실패해도 호출자가 동일 메시지 표시.
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 비밀번호 재설정 View.
    /// </summary>
    public class PasswordResetView : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("입력 필드")]
        [Tooltip("이메일 입력 필드.")]
        [SerializeField] private TMP_InputField _emailInput;

        [Header("버튼")]
        [Tooltip("재설정 메일 발송 버튼.")]
        [SerializeField] private Button _sendButton;

        [Tooltip("뒤로가기 버튼.")]
        [SerializeField] private Button _backButton;

        [Header("상태 표시")]
        [Tooltip("안내 / 오류 메시지.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        // ====================================================================
        // 의존성
        // ====================================================================

        private LoginRootView _rootView;
        private LoginUseCase _loginUseCase;

        // ====================================================================
        // 초기화
        // ====================================================================

        public void Initialize(LoginRootView rootView, LoginUseCase loginUseCase)
        {
            _rootView = rootView;
            _loginUseCase = loginUseCase;

            if (_sendButton != null) _sendButton.onClick.AddListener(OnSendClicked);
            if (_backButton != null) _backButton.onClick.AddListener(() => _rootView.HandleBack());

            ClearStatus();
        }

        private void OnDestroy()
        {
            if (_sendButton != null) _sendButton.onClick.RemoveAllListeners();
            if (_backButton != null) _backButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 재설정 메일 발송
        // ====================================================================

        /// <summary>
        /// 발송 버튼 → LoginUseCase.SendPasswordResetEmailAsync().
        /// AuthSystemRules.md 규칙 2: 가입되지 않은 이메일도 동일 메시지("이메일을 확인하세요")
        ///   → 성공/실패 모두 동일 안내 표시 (보안: 계정 존재 여부 노출 방지).
        /// </summary>
        private async void OnSendClicked()
        {
            ClearStatus();

            string email = _emailInput != null ? _emailInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(email))
            {
                SetStatus("이메일을 입력하세요.");
                return;
            }

            SetInteractable(false);

            try
            {
                // 결과 무시 — 보안상 가입 여부를 노출하지 않기 위해 동일 메시지를 표시한다.
                //   네트워크 오류만 별도 분기.
                bool ok = await _loginUseCase.SendPasswordResetEmailAsync(email);

                if (!ok && _loginUseCase.LastError != null &&
                    _loginUseCase.LastError.Reason == AuthErrorReason.NetworkError)
                {
                    SetStatus(_loginUseCase.LastError.Message);
                    if (_rootView != null) _rootView.ShowNetworkErrorPopup();
                }
                else
                {
                    // 성공이든 가입되지 않은 이메일이든 동일 메시지
                    SetStatus("입력하신 이메일로 재설정 링크를 보냈습니다. 메일함을 확인하세요.");
                    // 사용자가 메시지를 확인할 시간을 주고 자동으로 이메일 로그인 화면으로 복귀.
                    Invoke(nameof(GoBackToEmailLogin), 1.5f);
                }
            }
            finally
            {
                SetInteractable(true);
            }
        }

        /// <summary>
        /// 자동 복귀용 헬퍼 — Invoke 콜백 대상.
        /// </summary>
        private void GoBackToEmailLogin()
        {
            if (_rootView != null) _rootView.ShowEmailLogin();
        }

        // ====================================================================
        // UI 헬퍼
        // ====================================================================

        private void SetInteractable(bool on)
        {
            if (_sendButton != null) _sendButton.interactable = on;
            if (_backButton != null) _backButton.interactable = on;
            if (_emailInput != null) _emailInput.interactable = on;
        }

        private void SetStatus(string text)
        {
            if (_statusText != null) _statusText.text = text ?? string.Empty;
        }

        private void ClearStatus() => SetStatus(string.Empty);
    }
}
