// ============================================================================
// RematchRequestPopup.cs
// 재경기 요청 수락/거절 팝업 + 거절 알림 팝업.
//
// ShowRequest(): "상대방이 재경기를 요청하였습니다." 팝업 (수락/거절 버튼)
// ShowDeclined(): "상대방이 재경기를 거절하였습니다." 팝업 (확인 버튼)
// Hide(): 팝업 전체 숨김
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 재경기 요청/거절 팝업 UI.
    /// 커스텀게임 종료 후 상대방의 재경기 요청에 대한 수락/거절 인터페이스.
    /// </summary>
    public class RematchRequestPopup : MonoBehaviour
    {
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("배경 오버레이")]
        [Tooltip("반투명 검정 오버레이. 팝업 표시 시 활성화, 숨김 시 비활성화.")]
        [SerializeField] private GameObject _overlay;

        [Header("요청 팝업 (수락/거절)")]
        [Tooltip("재경기 요청 수신 패널. '상대방이 재경기를 요청하였습니다.' 표시.")]
        [SerializeField] private GameObject _requestPanel;

        [Tooltip("수락 버튼.")]
        [SerializeField] private Button _acceptButton;

        [Tooltip("거절 버튼.")]
        [SerializeField] private Button _declineButton;

        [Header("거절 알림 팝업")]
        [Tooltip("거절 알림 패널. '상대방이 재경기를 거절하였습니다.' 표시.")]
        [SerializeField] private GameObject _declinedPanel;

        [Tooltip("거절 알림 확인 버튼.")]
        [SerializeField] private Button _declinedConfirmButton;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>수락 콜백.</summary>
        private System.Action _onAccept;

        /// <summary>거절 콜백.</summary>
        private System.Action _onDecline;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // 버튼 이벤트 바인딩
            if (_acceptButton != null)
                _acceptButton.onClick.AddListener(OnAcceptClicked);
            if (_declineButton != null)
                _declineButton.onClick.AddListener(OnDeclineClicked);
            if (_declinedConfirmButton != null)
                _declinedConfirmButton.onClick.AddListener(Hide);

            // 초기 상태: 숨김
            Hide();
        }

        // ====================================================================
        // 공개 API
        // ====================================================================

        /// <summary>
        /// 재경기 요청 수신 팝업 표시.
        /// 수락/거절 버튼으로 사용자 선택을 받음.
        /// </summary>
        /// <param name="onAccept">수락 시 호출할 콜백.</param>
        /// <param name="onDecline">거절 시 호출할 콜백.</param>
        public void ShowRequest(System.Action onAccept, System.Action onDecline)
        {
            _onAccept = onAccept;
            _onDecline = onDecline;

            if (_overlay != null)      _overlay.SetActive(true);
            if (_requestPanel != null) _requestPanel.SetActive(true);
            if (_declinedPanel != null) _declinedPanel.SetActive(false);
        }

        /// <summary>
        /// 재경기 거절 알림 팝업 표시.
        /// 상대가 재경기를 거절했음을 알리는 확인 전용 팝업.
        /// </summary>
        public void ShowDeclined()
        {
            if (_overlay != null)      _overlay.SetActive(true);
            if (_requestPanel != null) _requestPanel.SetActive(false);
            if (_declinedPanel != null) _declinedPanel.SetActive(true);
        }

        /// <summary>
        /// 팝업 전체 숨김. 오버레이 + 요청/거절 알림 패널 모두 비활성화.
        /// </summary>
        public void Hide()
        {
            if (_overlay != null)      _overlay.SetActive(false);
            if (_requestPanel != null) _requestPanel.SetActive(false);
            if (_declinedPanel != null) _declinedPanel.SetActive(false);
        }

        // ====================================================================
        // 버튼 핸들러
        // ====================================================================

        /// <summary>수락 버튼 클릭. 팝업 닫고 콜백 실행.</summary>
        private void OnAcceptClicked()
        {
            Hide();
            _onAccept?.Invoke();
        }

        /// <summary>거절 버튼 클릭. 팝업 닫고 콜백 실행.</summary>
        private void OnDeclineClicked()
        {
            Hide();
            _onDecline?.Invoke();
        }
    }
}
