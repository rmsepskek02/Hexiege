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

using System;                        // [DEBUG-TEMP] 디버깅 완료 후 제거 (DateTime)
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

        // [2026-06-21] BlockingOverlay를 UIManager 단일 소유 구조로 통합.
        //   자체 _blockingOverlay 대신 UIManager.Instance?.ShowBlockingOverlay() (Modal 모드)를 사용한다.
        //   아래 필드는 테스트 통과 후 삭제 예정 — 현재는 비활성화(주석 처리)로 보존.
        // [Rule 5] SetActive 대신 CanvasGroup으로 제어한다. (오브젝트는 켜둔 채 투명도/입력만 토글)
        // [Tooltip("뒤쪽 입력 차단 오버레이 CanvasGroup. alpha/blocksRaycasts로 제어.")]
        // [SerializeField] private CanvasGroup _blockingOverlay;

        [Header("텍스트")]
        [Tooltip("경고 메시지 텍스트.")]
        [SerializeField] private TextMeshProUGUI _warningText;

        [Header("버튼")]
        [Tooltip("계정 만들기 버튼 → SignUpView 로 전환.")]
        [SerializeField] private Button _createAccountButton;

        [Tooltip("계속 익명으로 진행 버튼 → 익명 로그인 실행.")]
        [SerializeField] private Button _continueAnonymousButton;

        // 팝업 우측 상단의 닫기(X) 버튼.
        // 씬에는 활성 상태로 존재했으나 코드 연결이 없어 클릭해도 무반응이던 버그를 수정한다.
        [Tooltip("닫기(X) 버튼 → 팝업을 닫고 이전 화면으로 돌아간다.")]
        [SerializeField] private Button _closeButton;

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
            RuntimeLog("INFO", "Initialize 호출됨"); // [DEBUG-TEMP] 디버깅 완료 후 제거
            _rootView = rootView;
            _loginUseCase = loginUseCase;
            _bootstrapper = bootstrapper;

            if (_createAccountButton != null)
                _createAccountButton.onClick.AddListener(OnCreateAccountClicked);

            if (_continueAnonymousButton != null)
                _continueAnonymousButton.onClick.AddListener(OnContinueAnonymousClicked);

            // 닫기(X) 버튼 클릭 시 팝업을 닫는다.
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseButtonClicked);

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
            // 메모리 누수 방지를 위해 닫기 버튼 리스너도 해제한다.
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 표시 / 숨김
        // ====================================================================

        /// <summary>팝업 표시.</summary>
        public void Show()
        {
            RuntimeLog("INFO", "팝업 표시됨"); // [DEBUG-TEMP] 디버깅 완료 후 제거
            // [2026-06-21] UIManager 단일 소유 BlockingOverlay를 Modal 모드로 표시(터치해도 닫히지 않음).
            UIManager.Instance?.ShowBlockingOverlay();
            // [구로직 — 테스트 통과 후 삭제]
            // [Rule 5] SetActive 대신 CanvasGroup으로 표시. (불투명 + 뒤 입력 차단)
            // if (_blockingOverlay != null)
            // {
            //     _blockingOverlay.alpha = 1f;
            //     _blockingOverlay.blocksRaycasts = true;
            //     _blockingOverlay.interactable = true;
            // }
            if (_panel != null) _panel.Show();
        }

        /// <summary>팝업 숨김.</summary>
        public void Hide()
        {
            // [2026-06-21] UIManager 단일 소유 BlockingOverlay를 숨김(중첩 시 참조 카운터로 처리).
            UIManager.Instance?.HideBlockingOverlay();
            // [구로직 — 테스트 통과 후 삭제]
            // [Rule 5] SetActive 대신 CanvasGroup으로 숨김. (투명 + 입력 통과)
            // if (_blockingOverlay != null)
            // {
            //     _blockingOverlay.alpha = 0f;
            //     _blockingOverlay.blocksRaycasts = false;
            //     _blockingOverlay.interactable = false;
            // }
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
        /// 닫기(X) 버튼 클릭 → 팝업만 닫는다(추가 동작 없음).
        /// </summary>
        private void OnCloseButtonClicked()
        {
            Hide();
        }

        /// <summary>
        /// 계속 익명으로 진행 → 익명 로그인 실행 후 Lobby 이동.
        /// </summary>
        private async void OnContinueAnonymousClicked()
        {
            RuntimeLog("INFO", "익명 계속 버튼 클릭됨"); // [DEBUG-TEMP] 디버깅 완료 후 제거

            // 중복 클릭 방지
            SetInteractable(false);
            _bootstrapper.ShowLoading(true, "로그인 중...");

            try
            {
                LoginResult result = await _loginUseCase.SignInAnonymouslyAsync();

                // [DEBUG-TEMP] 디버깅 완료 후 제거 — LoginResult 값 기록
                if (result == LoginResult.Success)
                    RuntimeLog("INFO", $"익명 로그인 결과 | result={result}");
                else
                    RuntimeLog("ERROR", $"익명 로그인 결과 | result={result}");

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
            // 로그인 진행 중에는 닫기 버튼도 비활성화하여 도중 취소를 막는다.
            if (_closeButton != null) _closeButton.interactable = on;
        }

        // ====================================================================
        // [DEBUG-TEMP] 디버깅 완료 후 제거 — RuntimeLog 출력
        // 실기기(Android)에서 로그인 흐름을 Logcat(Debug.Log)으로 추적하기 위한 임시 로그.
        // 사용자가 Logcat 출력을 복사해 공유한다.
        // ====================================================================

        /// <summary>
        /// [DEBUG-TEMP] 한 줄 로그를 Debug.Log(Logcat)로 출력한다.
        /// 형식: [HH:MM:SS.ms] [LEVEL] [UI/AnonymousWarningPopup] 메시지
        /// </summary>
        private void RuntimeLog(string level, string message)
        {
            // 에디터 파일 형식과 동일하게 시간/레벨/시스템 태그를 붙여 출력한다.
            Debug.Log($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [UI/AnonymousWarningPopup] {message}");
        }
    }
}
