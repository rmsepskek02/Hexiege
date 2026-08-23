// ============================================================================
// NetworkErrorPopup.cs
// 네트워크 오류 발생 시 표시되는 안내 전용 팝업.
//
// 역할:
//   - 로그인/인증 과정에서 네트워크 연결 문제(NetworkError)가 감지되면 표시.
//   - "확인" 버튼 하나만 존재하며, 클릭 시 팝업을 닫기만 한다(추가 동작 없음).
//   - 팝업 외부 클릭으로는 닫지 않음 — 사용자가 명시적으로 확인 버튼을 눌러야 한다.
//
// 구조:
//   AnimatedPanel + BlockingOverlay 패턴은 AnonymousWarningPopup / ConfirmPopup 과 동일.
//   BlockingOverlay 는 UIManager 가 단일 소유하므로,
//   이 팝업은 UIManager.Instance?.ShowBlockingOverlay() / HideBlockingOverlay() 만 호출한다.
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 네트워크 오류 안내 팝업.
    /// 확인 버튼을 누르면 팝업이 닫히기만 한다.
    /// </summary>
    public class NetworkErrorPopup : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("팝업 본체")]
        [Tooltip("팝업 등장/퇴장 애니메이션을 담당하는 AnimatedPanel.")]
        [SerializeField] private AnimatedPanel _panel;

        [Header("버튼")]
        [Tooltip("확인 버튼 → 팝업 닫기만 수행.")]
        [SerializeField] private Button _confirmButton;

        // 팝업 우측 상단의 닫기(X) 버튼.
        // 씬에는 활성 상태로 존재했으나 코드 연결이 없어 클릭해도 무반응이던 버그를 수정한다.
        [Tooltip("닫기(X) 버튼 → 팝업을 닫는다.")]
        [SerializeField] private Button _closeButton;

        // ====================================================================
        // 의존성
        // ====================================================================

        // 루트 View 참조. 현재는 직접 사용하지 않지만,
        // 다른 팝업들과 동일한 초기화 시그니처를 유지하고
        // 향후 화면 전환이 필요할 경우를 대비해 보관한다.
        private LoginRootView _rootView;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// LoginRootView 에서 호출. 의존성 주입 + 확인 버튼 콜백 등록.
        /// </summary>
        public void Initialize(LoginRootView rootView)
        {
            _rootView = rootView;

            // 확인 버튼 클릭 시 팝업을 닫는다.
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);

            // 닫기(X) 버튼 클릭 시 팝업을 닫는다.
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지를 위해 등록한 리스너를 모두 해제한다.
            if (_confirmButton != null) _confirmButton.onClick.RemoveAllListeners();
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 표시 / 숨김
        // ====================================================================

        /// <summary>팝업 표시.</summary>
        public void Show()
        {
            // UIManager 단일 소유 BlockingOverlay 를 표시(뒤쪽 입력 차단).
            UIManager.Instance?.ShowBlockingOverlay();
            if (_panel != null) _panel.Show();
        }

        /// <summary>팝업 숨김.</summary>
        public void Hide()
        {
            // BlockingOverlay 숨김(중첩 시 참조 카운터로 처리).
            UIManager.Instance?.HideBlockingOverlay();
            if (_panel != null) _panel.Hide();
        }

        // ====================================================================
        // 버튼 콜백
        // ====================================================================

        /// <summary>
        /// 확인 버튼 클릭 → 팝업 닫기만 수행.
        /// </summary>
        private void OnConfirmClicked()
        {
            Hide();
        }

        /// <summary>
        /// 닫기(X) 버튼 클릭 → 팝업 닫기만 수행.
        /// </summary>
        private void OnCloseButtonClicked()
        {
            Hide();
        }
    }
}
