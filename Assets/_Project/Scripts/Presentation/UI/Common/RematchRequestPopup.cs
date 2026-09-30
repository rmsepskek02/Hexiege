// ============================================================================
// RematchRequestPopup.cs
// 재경기 요청 수락/거절 팝업 + 거절 알림 팝업.
//
// ShowRequest(): "상대방이 재경기를 요청하였습니다." 팝업 (수락/거절 버튼)
// ShowDeclined(): "상대방이 재경기를 거절하였습니다." 팝업 (확인 버튼)
// Hide(): 팝업 전체 숨김
// TryCloseUnansweredRequest(): 응답 전 요청 팝업이 떠 있으면 닫는다 (공통 UI 규칙 D-6 1항)
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

        /// <summary>
        /// 지금 화면에 <b>요청 팝업(수락/거절)</b>이 떠 있고 사용자가 아직 응답하지 않았는지 여부.
        ///
        /// [초급자용 설명] 이 값이 왜 필요한가
        ///   공통 UI 규칙 D-6 은 「재경기 요청 팝업이 떠 있는 상태에서 응답하기 전에 요청자가 이탈한 경우」
        ///   에만 적용된다. 즉 <b>이 팝업이 떠 있었는지</b>가 바깥(GameEndUI)에서 알아야 하는 정보인데,
        ///   패널의 알파값은 페이드 애니메이션 도중에는 0 도 1 도 아니어서 그것으로 판단할 수 없다.
        ///   그래서 「띄웠다 / 닫았다」를 명시적인 플래그 하나로 들고 있는다.
        ///
        ///   🔴 이 값을 직접 대입하는 자리는 <b>아래 「상태 전이 한 자리」뿐</b>이다.
        ///   값을 바꾸는 다섯 경로(요청 표시 2개 · 닫기 · 거절 알림 패널로 교체 · 파괴)가
        ///   모두 그 한 자리를 지나므로, 그 자리에서만 기록을 남기면 어느 경로도 빠뜨리지 않는다.
        /// </summary>
        private bool _requestShowing;

        // ====================================================================
        // 상태 전이의 계기 (기록용)
        // ====================================================================

        /// <summary>
        /// 이 팝업의 상태가 바뀐 <b>계기</b> 7종.
        ///
        /// <para>
        /// [초급자용 설명] 왜 문자열이 아니라 열거형인가
        ///   계기를 문자열로 넘기면 부르는 자리마다 표기가 조금씩 달라지고(대소문자 하나만 달라도
        ///   다른 값이다), 나중에 기록을 모아 셀 때 <b>같은 사건이 두 갈래로 조용히 쪼개진다.</b>
        ///   열거형이면 오타가 곧바로 컴파일 오류이고, 이름을 바꾸면 편집기가 전부 따라 바꿔 준다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>멤버 이름이 그대로 기록에 나간다.</b> 그러므로 한 번 정한 이름은 바꾸지 않는다 —
        /// 이름을 바꾸면 이미 쌓인 기록과 연결이 끊긴다.
        /// </para>
        /// </summary>
        private enum PopupTransitionCause
        {
            /// <summary>요청 팝업(수락/거절)을 띄웠다.</summary>
            RequestShown,

            /// <summary>요청 팝업이 거절 알림 패널로 교체됐다.</summary>
            DeclinedSwitch,

            /// <summary>사용자가 수락을 눌렀다.</summary>
            Accept,

            /// <summary>사용자가 거절을 눌렀다.</summary>
            Decline,

            /// <summary>응답하기 전에 요청자가 이탈해 강제로 닫혔다(공통 UI 규칙 D-6 1항).</summary>
            OpponentLeft,

            /// <summary>거절 알림 패널의 확인 버튼으로 닫혔다.</summary>
            DeclinedConfirmed,

            /// <summary>응답이 없는 채로 객체가 파괴됐다(씬 언로드 · 앱 종료).</summary>
            Destroy
        }

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
            // 🔴 파괴되기 전에 「화면 전체를 덮는 반투명 막」의 점유를 먼저 놓는다.
            //
            // [초급자용 설명] 왜 이 자리에서 놓아야 하는가
            //   그 막은 이 팝업이 소유한 물건이 아니다. 공용 UI 관리자가 하나만 들고 있고,
            //   그 관리자는 씬이 바뀌어도 파괴되지 않도록 만들어져 있다(로그인 씬에서 한 번 생긴다).
            //   관리자는 「막을 켜 달라고 한 곳이 지금 몇 곳인가」를 숫자로 세어 두고,
            //   그 숫자가 0이 될 때에만 막을 실제로 끈다.
            //   그래서 이 팝업이 점유를 놓지 않은 채 사라지면 숫자가 1로 남고, 막은 켜진 채
            //   다음 씬(로비)까지 따라간다. 막은 화면 전체를 받는 버튼이라 남으면 로비의 어떤
            //   버튼도 눌리지 않는다 — 앱을 완전히 종료해야 풀리는 상태가 된다.
            //
            //   실제로 그렇게 되는 경로는 「이 팝업이 떠 있는 동안 자동 로비 복귀 시간이 만료되어
            //   씬이 언로드되는 것」이다. 사용자가 버튼을 눌러 닫는 경로들은 닫기 메서드가
            //   이미 점유를 놓아 주므로 이 경로만 구멍이었다.
            //
            // 🔴 순서 — 점유 해제를 페이드 Tween 정리보다 **먼저** 둔다.
            //   이 파일의 닫기 메서드도 「점유를 먼저 놓고 그다음 연출을 정리」하는 순서이므로
            //   새로운 순서를 만들지 않고 기존 순서를 그대로 따른다.
            //
            // ⚠️ 두 번 불려도 안전하다 — 아래 헬퍼가 자기 점유 여부를 먼저 보고 한 번만 동작하고,
            //   공용 관리자 쪽에도 세어 둔 숫자가 음수로 내려가지 않게 막는 검사가 있다.
            //   그래서 닫기 메서드로 이미 놓은 뒤에 파괴되어도 숫자가 두 번 줄지 않는다.
            //
            // ⚠️ 아래 두 줄의 순서 — 「닫혔다」를 먼저, 「점유를 놓았다」를 그다음에 남긴다.
            //   기록을 읽는 사람은 위에서 아래로 읽으므로, 팝업이 끝난 사실 → 그 결과로 막을 놓은 사실
            //   순서여야 인과가 그대로 읽힌다. 이 파일의 닫기 메서드도 같은 순서다.
            SetRequestShowing(false, PopupTransitionCause.Destroy);
            HideOverlayOnce(PopupTransitionCause.Destroy);

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
            ShowOverlayOnce(PopupTransitionCause.RequestShown);
            // 「응답 대기 중」 상태로 표시한다 (공통 UI 규칙 D-6 판정용).
            //
            // ⚠️ 이 한 줄이 막을 잡는 위의 한 줄보다 뒤에 오는 이유 — 두 줄 사이에 이 상태값을
            //   읽는 코드가 없으므로 동작은 순서와 무관하고, 이 순서여야 기록에 「막을 잡았다 →
            //   팝업이 떴다」로 남아 그 줄에 실린 점유 여부가 실제와 맞는다.
            SetRequestShowing(true, PopupTransitionCause.RequestShown);
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
            ShowOverlayOnce(PopupTransitionCause.RequestShown);
            // 「응답 대기 중」 상태로 표시한다 (공통 UI 규칙 D-6 판정용).
            // 순서를 막 점유 뒤에 둔 이유는 위 오버로드의 같은 자리 주석과 같다.
            SetRequestShowing(true, PopupTransitionCause.RequestShown);
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
            // (요청 팝업에서 넘어온 경우에는 이미 점유 중이라 이 호출이 조용히 돌아간다)
            ShowOverlayOnce(PopupTransitionCause.DeclinedSwitch);
            // 요청 팝업이 거절 알림 팝업으로 교체되므로 「응답 대기 중」이 아니다.
            SetRequestShowing(false, PopupTransitionCause.DeclinedSwitch);
            // 거절 알림 패널만 페이드인.
            FadeIn(_declinedPanel, _declinedPanelCg, ref _declinedFade);
        }

        /// <summary>
        /// 팝업 전체 숨김. 오버레이 + 요청/거절 알림 패널 모두 페이드아웃 후 비활성화.
        ///
        /// <para>
        /// ⚠️ <b>인자가 없는 이 형태는 버튼 배선용이다.</b> 거절 알림 패널의 확인 버튼이
        /// <c>onClick</c> 에 이 메서드를 그대로 등록하는데, 그 자리에 등록할 수 있는 것은
        /// <b>인자를 받지 않는 메서드</b>뿐이라 이 형태가 남아 있어야 한다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>새로 닫는 자리를 만들 때는 이것이 아니라 계기를 함께 넘기는 아래 비공개 형태를 쓴다.</b>
        /// 계기를 넘기지 않으면 기록에 「확인 버튼으로 닫혔다」로 남아 사실과 달라진다.
        /// </para>
        /// </summary>
        public void Hide()
        {
            HideInternal(PopupTransitionCause.DeclinedConfirmed);
        }

        /// <summary>
        /// 팝업 전체 숨김의 본체. <b>무엇 때문에 닫혔는지</b>를 함께 받아 기록에 남긴다.
        ///
        /// <para>
        /// 🔴 <b>계기를 인자로 받는 이유</b> — 닫기는 네 경로(수락 · 거절 · 공통 UI 규칙 D-6 의
        /// 강제 닫기 · 거절 알림의 확인 버튼)가 <b>모두 이 한 자리로 모이는데</b>, 이 메서드 자신은
        /// 누가 자기를 불렀는지 알 수 없다. 그래서 부르는 쪽이 계기를 알려 준다.
        /// </para>
        ///
        /// <para>
        /// ⚠️ 부르는 쪽마다 기록을 흩어 넣지 않는 이유는 <b>빠뜨리기 때문</b>이다. 이 프로젝트는
        /// 같은 판단을 이미 한 번 했다 — 회차 표식을 넣을 때 부르는 쪽이 아니라 <b>데이터를
        /// 조립하는 본문</b>에 넣어야 한 자리도 빠지지 않는다는 것이 확인돼 있다.
        /// </para>
        /// </summary>
        /// <param name="cause">이 닫기를 일으킨 계기.</param>
        private void HideInternal(PopupTransitionCause cause)
        {
            // 어떤 경로로 닫혀도 「응답 대기 중」은 끝난다.
            // ⚠️ 실제로 떠 있었을 때만 기록이 남는다 — 아래 전이 한 자리가 그것을 판별한다.
            //   인자 없는 형태는 거절 알림의 확인 버튼에도 배선돼 있어, 요청 팝업이 떠 있지 않은
            //   상태에서도 불린다. 그 자리에서 「요청 팝업이 닫혔다」를 남기면 거짓이 된다.
            SetRequestShowing(false, cause);

            // 오버레이는 UIManager가 단일 소유 — 점유 중일 때만 1회 해제.
            HideOverlayOnce(cause);
            FadeOut(_requestPanel, _requestPanelCg, ref _requestFade);
            FadeOut(_declinedPanel, _declinedPanelCg, ref _declinedFade);
        }

        /// <summary>
        /// 아직 응답하지 않은 재경기 요청 팝업이 떠 있으면 닫는다 (공통 UI 규칙 D-6 1항).
        ///
        /// <para>
        /// [초급자용 설명] 왜 이 팝업을 <b>먼저</b> 닫는가
        ///   요청을 보낸 상대가 이미 게임을 떠났으므로, 수락 버튼을 눌러도 그 응답이 닿을 곳이 없다.
        ///   그런데 팝업이 그대로 떠 있으면 사용자는 아직 선택할 수 있다고 믿고 수락을 누르게 되고,
        ///   아무 일도 일어나지 않는 화면 앞에서 기다린다. 그래서 <b>닿을 곳이 없어진 선택지를
        ///   화면에서 먼저 치우고</b>, 그 다음에 무슨 일이 일어났는지 알리는 순서(규칙 D-6 의 1항 → 2항)다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>「떠 있지 않았다」와 「떠 있어서 닫았다」를 구분해 돌려준다.</b> 규칙 D-6 은
        ///    요청 팝업이 떠 있던 경우에만 적용되는 규정이고, 떠 있지 않았다면 이탈 사실은
        ///    규칙 D-1 의 타이머 텍스트가 이미 알리고 있다. 그 자리에 알림 팝업까지 겹쳐 띄우면
        ///    규칙 D-1 이 「별도 팝업을 새로 띄우지 않는다」고 정한 것과 어긋난다.
        /// </para>
        /// </summary>
        /// <returns>
        /// 요청 팝업이 응답 대기 상태로 떠 있어서 닫았으면 <c>true</c>,
        /// 애초에 떠 있지 않았으면 <c>false</c>(이 경우 아무것도 건드리지 않는다).
        /// </returns>
        public bool TryCloseUnansweredRequest()
        {
            if (!_requestShowing)
            {
                // 🔴 여기가 「규칙 D-6 의 절차가 건너뛰어진 자리」다. 화면에는 아무 변화도 없어서
                //   기록이 없으면 이 일이 있었다는 사실 자체를 확인할 방법이 없다.
                //
                // ⚠️ 이 프로젝트에는 「가드에는 기록을 남기지 않는다」는 관례가 있는데, 그것은
                //   매 프레임·매 틱 도달하는 가드를 두고 한 말이다. 이 가드는 한 경기에 많아도
                //   한 번 도달하고(부르는 쪽이 중복 통보를 막는다), 바로 이 침묵이 확인을 막았다.
                //
                // ⚠️ 부르는 쪽에 넣지 않은 이유 — 「떠 있지 않았다」를 아는 것은 이 팝업의
                //   상태값뿐이고, 부르는 쪽은 참/거짓만 받는다. 사실이 발생한 계층에서 한 번만
                //   남기는 것이 로그 규칙(1.3 원칙 1 · 1.14 금지 9)이 요구하는 형태다.
                LogForcedCloseSkipped();
                return false;
            }

            // 닫기 본체가 요청/거절 패널과 오버레이 점유, 그리고 응답 대기 표시를 함께 정리한다.
            // (응답 대기 표시를 내리는 일도 그 안에서 처리되므로 여기서 따로 손대지 않는다)
            HideInternal(PopupTransitionCause.OpponentLeft);
            return true;
        }

        // ====================================================================
        // 오버레이 참조 점유 헬퍼 (UIManager 단일 소유 구조)
        // ====================================================================

        /// <summary>
        /// UIManager BlockingOverlay를 Modal 모드로 표시하되, 이 팝업이 이미 점유 중이면 중복 +1 하지 않는다.
        /// 요청 팝업 → 거절 팝업 전환 시(ShowRequest 후 ShowDeclined) 오버레이 참조가 2가 되어
        /// Hide() 1회로 0이 되지 않는 잔류 문제를 막는다.
        ///
        /// <para>
        /// 🔴 <b>계기를 받는 이유</b> — 표시 세 경로가 모두 이 한 함수를 지나므로 여기서 기록을
        /// 남기면 한 자리도 빠지지 않는다. 그런데 이 함수는 누가 불렀는지 알 수 없어
        /// 부르는 쪽이 알려 준다. 🔴 <b>비공개 메서드라 외부 계약은 바뀌지 않는다.</b>
        /// </para>
        /// </summary>
        /// <param name="cause">이 표시를 일으킨 계기.</param>
        private void ShowOverlayOnce(PopupTransitionCause cause)
        {
            if (_overlayShown) return;     // 이미 점유 중이면 그대로 둔다.
            _overlayShown = true;
            // ⚠️ 위 조기 반환 뒤에 두었으므로 「실제로 새로 잡은 경우」에만 남는다.
            //   아무 일도 하지 않고 돌아간 호출까지 남기면 요청 → 거절 전환마다 뜻 없는 줄이 늘어난다.
            LogOverlayAcquired(cause);
            UIManager.Instance?.ShowBlockingOverlay(); // Modal 모드(콜백 없음)
        }

        /// <summary>
        /// 이 팝업이 점유 중인 오버레이 참조를 1회 해제한다. 점유 중이 아니면 아무 동작도 하지 않는다.
        /// </summary>
        /// <param name="cause">이 해제를 일으킨 계기.</param>
        private void HideOverlayOnce(PopupTransitionCause cause)
        {
            if (!_overlayShown) return;
            _overlayShown = false;
            // ⚠️ 위 조기 반환 뒤 — 「실제로 놓은 경우」에만 남는다(표시 쪽과 같은 이유).
            // ⚠️ 공용 관리자에게 알리기 **전에** 남긴다. 그래야 기록이 「이 팝업이 놓았다 →
            //   그래서 막이 꺼졌다(또는 다른 점유자가 남아 꺼지지 않았다)」 순서로 읽힌다.
            LogOverlayReleased(cause);
            UIManager.Instance?.HideBlockingOverlay();
        }

        // ====================================================================
        // 상태 전이 한 자리 + 기록 (2026-09-30 추가 · 화면 동작은 바뀌지 않는다)
        //
        // [초급자용 설명] 이 절은 무엇이고 왜 생겼는가
        //   공통 UI 규칙 D-5 · D-6 이 정한 동작(요청 팝업을 닫고 알림 팝업을 띄운다)이
        //   제대로 됐는지는 지금까지 **사람이 화면을 보는 것** 말고는 확인할 방법이 없었다.
        //   이 파일에는 기록을 남기는 자리가 한 곳도 없었기 때문이다.
        //   특히 「화면 전체를 덮는 반투명 막을 잡았는지 · 놓았는지」와 「강제 닫기가
        //   건너뛰어졌는지」는 **화면을 봐도 알 수 없다** — 막은 거의 투명하고, 건너뛰는 것은
        //   아무 화면 변화도 만들지 않는다. 그래서 이 둘은 기록만이 답을 준다.
        //
        // 🔴 남기는 자리는 **상태가 실제로 바뀌는 순간과 조기 반환뿐**이다.
        //    매 프레임 도는 자리에는 넣지 않는다(로그 규칙 1.14 금지 8).
        //
        // ⚠️ 존속 축은 **개발**이다(로그 규칙 1.2). 에디터·개발 빌드에서만 의미가 있는 화면 상태
        //    기록이라 운영 축의 이벤트 키를 **새로 만들지 않았다.** 아래 두 컴파일 조건 덕분에
        //    릴리스 빌드에서는 호출도 **문자열 조립도** 통째로 사라진다(로그 규칙 1.7).
        //
        // 🔴 판별(전이인가 · 어느 심각도인가)을 **부르는 쪽이 아니라 이 메서드들 안에서** 하는 이유 —
        //    그 컴파일 조건은 **호출문 전체(인자 계산 포함)** 를 지운다. 판별을 안에 넣으면
        //    릴리스 빌드에는 **조건식조차 남지 않는다.** 부르는 쪽에 조건문을 쓰면 그 조건식은 남는다.
        // ====================================================================

        /// <summary>
        /// 「요청 팝업이 떠 있다」는 상태값을 바꾸는 <b>유일한 자리</b>. 값이 실제로 달라질 때만
        /// 기록을 남긴다.
        ///
        /// <para>
        /// 🔴 <b>값이 같으면 아무것도 하지 않고 돌아간다.</b> 전이가 아닌 자리에 기록을 남기면
        /// 거짓이 된다 — 인자 없는 닫기 형태는 거절 알림의 확인 버튼에도 배선돼 있어,
        /// 요청 팝업이 떠 있지 않은 상태에서도 불린다.
        /// </para>
        /// </summary>
        /// <param name="showing">바꿀 값. true면 응답 대기 시작, false면 종료.</param>
        /// <param name="cause">이 변화를 일으킨 계기.</param>
        private void SetRequestShowing(bool showing, PopupTransitionCause cause)
        {
            if (_requestShowing == showing) return;   // 전이가 아니다 — 남길 것이 없다.

            _requestShowing = showing;
            LogRequestShowingChanged(showing, cause);
        }

        /// <summary>
        /// 요청 팝업이 뜨거나 닫힌 사실을 남긴다(심각도는 둘 다 <b>정보</b> — 의도된 흐름이다).
        ///
        /// <para>
        /// 함께 싣는 참/거짓 하나는 <b>그 순간 이 팝업이 막 점유를 들고 있었는가</b>다.
        /// 닫힘 줄에서 그것이 참이면 바로 다음에 「놓았다」 줄이 따라와야 하고,
        /// 거짓이면 그 줄은 애초에 없는 것이 정상이다. 🔴 <b>그래서 줄 수를 늘리지 않고도
        /// 두 사실을 함께 확인할 수 있다.</b>
        /// </para>
        /// </summary>
        /// <param name="showing">true면 떴다, false면 닫혔다.</param>
        /// <param name="cause">계기. 닫힘 경로를 가르는 값이 된다.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogRequestShowingChanged(bool showing, PopupTransitionCause cause)
        {
            if (showing)
            {
                GameLog.Dev.Info("UI", nameof(RematchRequestPopup),
                                 "재경기 요청 팝업이 떴다 — 응답 대기 시작",
                                 $"Path={cause}, IsOverlayHeld={_overlayShown}");
                return;
            }

            GameLog.Dev.Info("UI", nameof(RematchRequestPopup),
                             "재경기 요청 팝업이 닫혔다 — 응답 대기 종료",
                             $"Path={cause}, IsOverlayHeld={_overlayShown}");
        }

        /// <summary>
        /// 이 팝업이 공용 반투명 막의 점유를 <b>새로 잡은</b> 사실을 남긴다(심각도 <b>정보</b> —
        /// 규칙 D-5 가 정한 정상 동작이다).
        /// </summary>
        /// <param name="cause">계기.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogOverlayAcquired(PopupTransitionCause cause)
        {
            GameLog.Dev.Info("UI", nameof(RematchRequestPopup),
                             "공용 반투명 막의 점유를 잡았다",
                             $"Cause={cause}");
        }

        /// <summary>
        /// 이 팝업이 공용 반투명 막의 점유를 <b>놓은</b> 사실을 남긴다.
        ///
        /// <para>
        /// 🔴 <b>계기가 파괴일 때만 심각도를 한 칸 올린다.</b> 그 줄이 남았다는 것은
        /// <b>정상 닫기 경로를 지나지 않은 채 객체가 사라졌고, 그물이 대신 놓아 주었다</b>는 뜻이다.
        /// 「대체 경로로 계속 진행됐다」는 로그 규칙 1.2 의 예가 그대로 이 모양이다.
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>왜 정보로 두지 않는가</b> — 규칙 D-6 은 「팝업을 방치해도 갇히지 않는다」고
        /// 방치 경로를 허용하지만, 허용된 것은 <b>사용자가 방치해도 된다</b>는 것이고
        /// <b>닫기 경로가 점유를 놓지 않아도 된다</b>는 것이 아니다. 같은 부류의 구멍이 다시
        /// 생기면 이 한 줄로 잡힌다 — 정보로 두면 정상 줄 사이에 묻힌다.
        /// </para>
        ///
        /// <para>
        /// ⚠️ 이 파일에서 <b>심각도를 가르는 자리는 여기 하나뿐</b>이다. 같은 화면(결과 화면)의
        /// 기존 진단 기록이 <b>정상 전이는 정보, 막힌 자리·그물 발동은 경고</b>로 갈라 두었고,
        /// 한 화면에서 기준을 다르게 쓰면 「왜 화면이 안 바뀌었나」를 찾는 사람의 훑기가 무용해진다.
        /// </para>
        /// </summary>
        /// <param name="cause">계기.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogOverlayReleased(PopupTransitionCause cause)
        {
            if (cause == PopupTransitionCause.Destroy)
            {
                GameLog.Dev.Warn("UI", nameof(RematchRequestPopup),
                                 "공용 반투명 막의 점유를 놓았다 — 정상 닫기 경로를 지나지 않은 채 파괴됐다",
                                 $"Cause={cause}");
                return;
            }

            GameLog.Dev.Info("UI", nameof(RematchRequestPopup),
                             "공용 반투명 막의 점유를 놓았다",
                             $"Cause={cause}");
        }

        /// <summary>
        /// 공통 UI 규칙 D-6 의 강제 닫기가 <b>요청 팝업이 떠 있지 않아 건너뛰어진</b> 사실을 남긴다.
        ///
        /// <para>
        /// 🔴 심각도를 한 칸 올린다 — <b>가드에 막혀 규칙이 정한 절차가 수행되지 않고 조기
        /// 반환한 자리</b>이기 때문이다. 화면에 아무 변화도 없어 이 줄이 유일한 단서다.
        /// </para>
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogForcedCloseSkipped()
        {
            GameLog.Dev.Warn("UI", nameof(RematchRequestPopup),
                             "요청 팝업이 떠 있지 않아 공통 UI 규칙 D-6 의 강제 닫기를 건너뛴다");
        }

        // ====================================================================
        // 버튼 핸들러
        // ====================================================================

        /// <summary>수락 버튼 클릭. 팝업 닫고 콜백 또는 GameEvents.OnLocalRematchAccepted 발행.</summary>
        private void OnAcceptClicked()
        {
            HideInternal(PopupTransitionCause.Accept);
            // 콜백이 설정되어 있으면(레거시 경로) 콜백 호출, 그렇지 않으면 GameEvents 발행.
            if (_onAccept != null) _onAccept.Invoke();
            else GameEvents.OnLocalRematchAccepted.OnNext(Unit.Default);
        }

        /// <summary>거절 버튼 클릭. 팝업 닫고 콜백 또는 GameEvents.OnLocalRematchDeclined 발행.</summary>
        private void OnDeclineClicked()
        {
            HideInternal(PopupTransitionCause.Decline);
            if (_onDecline != null) _onDecline.Invoke();
            else GameEvents.OnLocalRematchDeclined.OnNext(Unit.Default);
        }
    }
}
