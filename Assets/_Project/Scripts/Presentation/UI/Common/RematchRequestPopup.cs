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

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using Hexiege.Application;

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

        /// <summary>요청 패널 CanvasGroup. DOFade 페이드 애니메이션에 사용.</summary>
        private CanvasGroup _requestPanelCg;

        /// <summary>거절 알림 패널 CanvasGroup. DOFade 페이드 애니메이션에 사용.</summary>
        private CanvasGroup _declinedPanelCg;

        /// <summary>요청 패널 페이드 Tween. 오버레이와 독립적으로 페이드 애니메이션 실행.</summary>
        private Tween _requestFade;

        /// <summary>거절 알림 패널 페이드 Tween. 오버레이와 독립적으로 페이드 애니메이션 실행.</summary>
        private Tween _declinedFade;

        /// <summary>OnNetworkRematchRequested / OnNetworkRematchDeclined 이벤트 구독 모음.</summary>
        private CompositeDisposable _eventSubscriptions;

        /// <summary>
        /// 이 팝업이 현재 UIManager BlockingOverlay 참조를 점유 중인지 추적.
        /// 요청 팝업 → 거절 팝업으로 전환(ShowRequest 후 ShowDeclined)될 때 오버레이를
        /// 중복으로 +1 하지 않도록 보장한다. 이 팝업은 항상 오버레이 참조를 0 또는 1개만 보유한다.
        /// (그렇지 않으면 Hide() 1회로는 카운터가 0이 되지 않아 오버레이가 잔류한다.)
        /// </summary>
        private bool _overlayShown;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // CanvasGroup 캐시: 없으면 자동 추가 — DOFade 애니메이션에 필수
            // 오버레이는 UIManager가 단일 소유하므로 여기서 캐시하지 않는다.
            _requestPanelCg = EnsureCanvasGroup(_requestPanel);
            _declinedPanelCg = EnsureCanvasGroup(_declinedPanel);

            // 버튼 이벤트 바인딩
            if (_acceptButton != null)
                _acceptButton.onClick.AddListener(OnAcceptClicked);
            if (_declineButton != null)
                _declineButton.onClick.AddListener(OnDeclineClicked);
            if (_declinedConfirmButton != null)
                _declinedConfirmButton.onClick.AddListener(Hide);

            // 초기 상태: 즉시 숨김 (애니메이션 없이 — Awake에서 페이드하면 시각적 깜빡임 발생)
            // 오버레이 초기화는 UIManager가 담당한다.
            if (_requestPanelCg != null) { _requestPanelCg.alpha = 0f; _requestPanelCg.blocksRaycasts = false; _requestPanelCg.interactable = false; }
            if (_declinedPanelCg != null){ _declinedPanelCg.alpha = 0f; _declinedPanelCg.blocksRaycasts = false; _declinedPanelCg.interactable = false; }

            // GameEvents 구독으로 재경기 요청/거절 이벤트를 수신한다.
            //   OnNetworkRematchRequested → ShowRequest() (콜백 없이)
            //   OnNetworkRematchDeclined → ShowDeclined()
            _eventSubscriptions = new CompositeDisposable();

            GameEvents.OnNetworkRematchRequested
                .Subscribe(_ => ShowRequest())
                .AddTo(_eventSubscriptions);

            GameEvents.OnNetworkRematchDeclined
                .Subscribe(_ => ShowDeclined())
                .AddTo(_eventSubscriptions);
        }

        private void OnDestroy()
        {
            // 패널별 페이드 Tween을 모두 정리 — 씬 전환 시 남은 Tween 에러 방지
            _requestFade?.Kill();
            _declinedFade?.Kill();

            // GameEvents 구독 해제
            _eventSubscriptions?.Dispose();
            _eventSubscriptions = null;
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 지정된 GameObject에서 CanvasGroup을 가져오거나 없으면 추가.
        /// DOFade 애니메이션에 CanvasGroup이 필수이므로 안전하게 보장.
        /// </summary>
        /// <param name="go">CanvasGroup을 확보할 대상 GameObject.</param>
        /// <returns>확보된 CanvasGroup. go가 null이면 null 반환.</returns>
        private CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (go == null) return null;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        /// <summary>
        /// 대상 패널을 DOFade 0→1 페이드인 실행.
        /// 이전 페이드가 진행 중이면 Kill()로 정리 후 새 페이드 시작.
        /// ref로 Tween을 전달받아 패널별 독립적으로 관리 — 다른 패널의 Tween을 덮어쓰지 않음.
        /// </summary>
        /// <param name="go">대상 GameObject (null 가드용으로만 사용).</param>
        /// <param name="cg">페이드 대상 CanvasGroup.</param>
        /// <param name="fadeTween">해당 패널의 Tween 참조. Kill/재할당용.</param>
        private void FadeIn(GameObject go, CanvasGroup cg, ref Tween fadeTween)
        {
            if (go == null || cg == null) return;
            cg.alpha = 0f;
            cg.blocksRaycasts = true; // 페이드인 시 레이캐스트 활성화
            cg.interactable = true;   // 상호작용 활성화
            fadeTween?.Kill();
            fadeTween = cg.DOFade(1f, 0.2f).SetUpdate(true);
        }

        /// <summary>
        /// 대상 패널을 DOFade 1→0 페이드아웃 후 CanvasGroup으로 숨김 처리.
        /// ref로 Tween을 전달받아 패널별 독립적으로 관리.
        /// </summary>
        /// <param name="go">대상 GameObject (null 가드용으로만 사용).</param>
        /// <param name="cg">페이드 대상 CanvasGroup.</param>
        /// <param name="fadeTween">해당 패널의 Tween 참조. Kill/재할당용.</param>
        private void FadeOut(GameObject go, CanvasGroup cg, ref Tween fadeTween)
        {
            if (go == null || cg == null) return;
            fadeTween?.Kill();
            fadeTween = cg.DOFade(0f, 0.15f).SetUpdate(true)
                .OnComplete(() =>
                {
                    // 페이드아웃 완료 후 레이캐스트/상호작용 차단으로 완전 숨김 처리.
                    // blocksRaycasts=false가 누락되면 보이지 않는 CanvasGroup이 터치를 차단하여
                    // 게임 UI 전체가 클릭 불가능해지는 버그 발생.
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                });
        }

        // ====================================================================
        // 공개 API
        // ====================================================================

        /// <summary>
        /// 재경기 요청 수신 팝업 표시 — 콜백 없는 단순 표시.
        /// 수락/거절 시에는 GameEvents.OnLocalRematchAccepted / OnLocalRematchDeclined를 발행한다.
        ///
        /// Awake에서 GameEvents.OnNetworkRematchRequested 구독으로 자동 호출되며,
        /// 직접 호출도 가능하다.
        /// </summary>
        public void ShowRequest()
        {
            // 콜백 미사용 — GameEvents 발행으로 대체.
            _onAccept = null;
            _onDecline = null;

            // 거절 패널은 즉시 숨김 (페이드 불필요 — 보이지 않는 상태)
            if (_declinedPanelCg != null) { _declinedPanelCg.alpha = 0f; _declinedPanelCg.blocksRaycasts = false; _declinedPanelCg.interactable = false; }

            // 오버레이는 UIManager가 단일 소유 — Modal 모드로 표시(터치해도 닫히지 않음).
            ShowOverlayOnce();
            // 요청 패널만 페이드인.
            FadeIn(_requestPanel, _requestPanelCg, ref _requestFade);
        }

        /// <summary>
        /// 재경기 요청 수신 팝업 표시 — 콜백 버전(레거시 호환).
        /// 새 코드에서는 콜백 없는 ShowRequest()를 사용하고 GameEvents.OnLocalRematch* 이벤트로 응답할 것.
        /// </summary>
        /// <param name="onAccept">수락 시 호출할 콜백.</param>
        /// <param name="onDecline">거절 시 호출할 콜백.</param>
        public void ShowRequest(System.Action onAccept, System.Action onDecline)
        {
            _onAccept = onAccept;
            _onDecline = onDecline;

            if (_declinedPanelCg != null) { _declinedPanelCg.alpha = 0f; _declinedPanelCg.blocksRaycasts = false; _declinedPanelCg.interactable = false; }
            // 오버레이는 UIManager가 단일 소유 — Modal 모드로 표시.
            ShowOverlayOnce();
            FadeIn(_requestPanel, _requestPanelCg, ref _requestFade);
        }

        /// <summary>
        /// 재경기 거절 알림 팝업 표시.
        /// 상대가 재경기를 거절했음을 알리는 확인 전용 팝업.
        /// </summary>
        public void ShowDeclined()
        {
            // 요청 패널은 즉시 숨김 (페이드 불필요 — 보이지 않는 상태)
            if (_requestPanelCg != null) { _requestPanelCg.alpha = 0f; _requestPanelCg.blocksRaycasts = false; _requestPanelCg.interactable = false; }

            // 오버레이는 UIManager가 단일 소유 — Modal 모드로 표시.
            ShowOverlayOnce();
            // 거절 알림 패널만 페이드인.
            FadeIn(_declinedPanel, _declinedPanelCg, ref _declinedFade);
        }

        /// <summary>
        /// 팝업 전체 숨김. 오버레이 + 요청/거절 알림 패널 모두 페이드아웃 후 비활성화.
        /// </summary>
        public void Hide()
        {
            // 오버레이는 UIManager가 단일 소유 — 점유 중일 때만 1회 해제.
            HideOverlayOnce();
            FadeOut(_requestPanel, _requestPanelCg, ref _requestFade);
            FadeOut(_declinedPanel, _declinedPanelCg, ref _declinedFade);
        }

        // ====================================================================
        // 오버레이 참조 점유 헬퍼 (UIManager 단일 소유 구조)
        // ====================================================================

        /// <summary>
        /// UIManager BlockingOverlay를 Modal 모드로 표시하되, 이 팝업이 이미 점유 중이면 중복 +1 하지 않는다.
        /// 요청 팝업 → 거절 팝업 전환 시(ShowRequest 후 ShowDeclined) 오버레이 참조가 2가 되어
        /// Hide() 1회로 0이 되지 않는 잔류 문제를 막는다.
        /// </summary>
        private void ShowOverlayOnce()
        {
            if (_overlayShown) return;     // 이미 점유 중이면 그대로 둔다.
            _overlayShown = true;
            UIManager.Instance?.ShowBlockingOverlay(); // Modal 모드(콜백 없음)
        }

        /// <summary>
        /// 이 팝업이 점유 중인 오버레이 참조를 1회 해제한다. 점유 중이 아니면 아무 동작도 하지 않는다.
        /// </summary>
        private void HideOverlayOnce()
        {
            if (!_overlayShown) return;
            _overlayShown = false;
            UIManager.Instance?.HideBlockingOverlay();
        }

        // ====================================================================
        // 버튼 핸들러
        // ====================================================================

        /// <summary>수락 버튼 클릭. 팝업 닫고 콜백 또는 GameEvents.OnLocalRematchAccepted 발행.</summary>
        private void OnAcceptClicked()
        {
            Hide();
            // 콜백이 설정되어 있으면(레거시 경로) 콜백 호출, 그렇지 않으면 GameEvents 발행.
            if (_onAccept != null) _onAccept.Invoke();
            else GameEvents.OnLocalRematchAccepted.OnNext(Unit.Default);
        }

        /// <summary>거절 버튼 클릭. 팝업 닫고 콜백 또는 GameEvents.OnLocalRematchDeclined 발행.</summary>
        private void OnDeclineClicked()
        {
            Hide();
            if (_onDecline != null) _onDecline.Invoke();
            else GameEvents.OnLocalRematchDeclined.OnNext(Unit.Default);
        }
    }
}
