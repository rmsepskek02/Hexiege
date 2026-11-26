// ============================================================================
// NetworkLagBannerUI.cs
// 인게임 연결 지연 「알림 띠」 — 화면 상단의 얇은 띠 + 「상대 연결 지연」. 🔴 조작을 막지 않는다 (2026-11-26 2차 수정 신설).
//
// [무엇을 하나 — 초급 개발자용 설명]
//   1차는 상대 신호가 끊기면 양쪽 모두 화면 전체를 반투명 덮개(NetworkLagOverlayUI)로 막았다. 그런데 Client 가 일부러
//   네트워크를 끊으면, 게임이 그대로 흐르는 Host 쪽 조작까지 최대 15초 막혔다(악용 우려 — 사용자 지적 (다)).
//   그래서 2차부터 「문제가 생긴 쪽만 막고 피해 쪽은 막지 않는다」(사양 ②):
//     · 내가 Host 이고 내 인터넷 확인이 정상(상대 쪽 문제) · 첫 확인 결과 전 · RTT 만 느린 경우 → 이 띠만 보인다.
//     · 그 밖(내 쪽 문제 · Client 가 Host 를 잃음 · 연결 복구 중) → 기존 덮개(NetworkLagOverlayUI).
//   이 띠는 「알림」일 뿐이다 — 터치가 띠를 그대로 통과해 뒤의 게임 화면 · 버튼이 평소처럼 눌린다.
//
// [터치를 통과시키는 방법 — 세 겹]
//   ① 띠의 Image · 글자 모두 raycastTarget = false(에디터 메뉴가 그렇게 만들고, 여기 Awake 에서도 한 번 더 끈다)
//   ② CanvasGroup.blocksRaycasts = false · interactable = false (보일 때도)
//   ③ 띠 Canvas 에 GraphicRaycaster 를 붙이지 않는다 — 레이캐스터가 없는 Canvas 의 그래픽은 터치 판정 대상이 아니다.
//
// [정렬 순서] 띠 Canvas Override Sorting = 205 — 일반 패널(200) 위, 지연 덮개(210) 아래.
//   띠와 덮개는 동시에 뜨지 않는다(차단 방식이 둘 중 하나다). ⚠️ 205 는 CanvasSortingOrder 대역 표에 아직 없는 값이다 —
//   규칙 2(대역 밖 값은 문서 갱신)에 따라 테스트 후 문서에 반영한다(메인 세션 보고 사항).
//
// [씬 배치] Game.unity `[UI]/SafeAreaContainer` 아래의 `NetworkLagBanner` 오브젝트. 1회성 에디터 메뉴
//   `Hexiege/Setup/Network Lag Banner - Place In Game Scene`(Assets/Editor/Tools/NetworkLagOverlaySetup.cs)가 만든다.
//   🔴 띠가 씬에 없어도 게임은 예외 없이 돈다 — 띠는 조작을 막지 않으므로 안내만 안 보일 뿐이다(감시기가 경고 1줄).
//
// task: Assets/_Project/Docs/_Tasks/2026-11-26/10_31_ingame-leave-judgment/Plan.md 「2차 수정」 절 ② · ⑤
// Presentation 레이어 — Unity 의존(MonoBehaviour). Unity.Netcode 참조 없음.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Hexiege.Application;
using Hexiege.Infrastructure; // DisconnectDiag(진단 로그 공통 키) — NetworkLagOverlayUI · GameEndUI 와 같은 사용 방식

namespace Hexiege.Presentation
{
    /// <summary>
    /// 인게임 연결 지연 알림 띠(조작을 막지 않음). NetworkLinkState 의 차단 방식이 Banner 일 때만 보인다.
    /// </summary>
    public sealed class NetworkLagBannerUI : MonoBehaviour
    {
        // ====================================================================
        // 상수 — 정렬 값 · 문구 (코드가 단일 소스)
        // ====================================================================

        /// <summary>띠 Canvas 정렬 값 — 일반 패널(200) 위, 지연 덮개(210) 아래.</summary>
        public const int BannerSortingOrder = 205;

        /// <summary>띠 문구(사양 ②).</summary>
        private const string BannerMessage = "상대 연결 지연";

        /// <summary>로그 카테고리.</summary>
        private const string LogSystem = "UI";

        // ====================================================================
        // Inspector 참조 — 1회성 에디터 메뉴가 채운다
        // ====================================================================

        [Header("띠 구성 (에디터 메뉴가 채운다)")]
        [Tooltip("띠 자신의 Canvas(Override Sorting). 정렬 값은 코드 상수 BannerSortingOrder 가 단일 소스다.")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("띠 표시/숨김용 CanvasGroup. 🔴 터치 차단은 항상 끈다(blocksRaycasts=false).")]
        [SerializeField] private CanvasGroup _group;

        [Tooltip("띠 문구 텍스트. 표시 문구는 이 클래스 상수가 단일 소스다(여기에 문구를 적지 않는다).")]
        [SerializeField] private TextMeshProUGUI _messageText;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private bool _shown;            // 띠가 지금 떠 있는가
        private bool _matchEnded;       // 이번 경기 결과가 이미 났는가(결과 화면이 우선)
        private float _shownAt;         // 띠를 띄운 시각(realtime 초 — 숨길 때 ShownMs 로그용)
        private IDisposable _linkStateSubscription;
        private IDisposable _gameEndSubscription;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            NetworkLinkState.SetLagBannerPresent(true);
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_messageText == null) _messageText = GetComponentInChildren<TextMeshProUGUI>(true);

            // 🔴 정렬 값은 코드 상수가 이긴다(덮개와 같은 방식).
            if (_canvas != null)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = BannerSortingOrder;
            }
            MakePassThrough();
            if (_messageText != null) _messageText.text = BannerMessage;
            ApplyVisible(false);

            if (_canvas == null || _messageText == null)
            {
                GameLog.Dev.Warn(LogSystem, nameof(NetworkLagBannerUI),
                    "알림 띠 참조 일부를 찾지 못했다 — 해당 기능 없이 동작한다(조작에는 영향 없음)",
                    "HasCanvas=" + (_canvas != null) + ", HasText=" + (_messageText != null));
            }
        }

        private void Start()
        {
            // 🔴 블록 본문 람다(LogRules §1.7 관례 — 안에 Dev 로그가 이어질 수 있다).
            _linkStateSubscription = GameEvents.OnNetworkLinkStateChanged.Subscribe(e => { OnLinkStateChanged(e); });
            // 결과가 나면 곧바로 내린다(감시기의 「결과 났음」 표시보다 이 구독이 먼저 불릴 수 있다 — 덮개와 같은 이유).
            _gameEndSubscription = GameEvents.OnGameEnd.Subscribe(_ => { OnMatchEnded(); });
            Refresh();
        }

        private void OnDestroy()
        {
            _linkStateSubscription?.Dispose();
            _gameEndSubscription?.Dispose();
            NetworkLinkState.SetLagBannerPresent(false);
        }

        // ====================================================================
        // 이벤트
        // ====================================================================

        /// <summary>연결 상태가 바뀌었다 — 새 경기면 결과 표시를 지우고 다시 판단한다.</summary>
        private void OnLinkStateChanged(NetworkLinkStateChangedEvent e)
        {
            if (e.Phase == InGameLinkPhase.Waiting) _matchEnded = false; // 새 경기 시작
            if (e.Phase == InGameLinkPhase.Ended) _matchEnded = true;
            Refresh();
        }

        /// <summary>결과가 났다 — 띠를 내린다.</summary>
        private void OnMatchEnded()
        {
            _matchEnded = true;
            Refresh();
        }

        // ====================================================================
        // 표시 / 숨김
        // ====================================================================

        /// <summary>지금 상태로 띠를 켜거나 끈다.</summary>
        private void Refresh()
        {
            bool want = !_matchEnded && NetworkLinkState.ShowsLagBanner;
            if (want) Show();
            else Hide();
        }

        /// <summary>띠를 띄운다(이미 떠 있으면 아무것도 하지 않는다 — 이유가 바뀌어도 문구는 같다).</summary>
        private void Show()
        {
            if (_shown) return;
            _shown = true;
            _shownAt = Time.realtimeSinceStartup;
            ApplyVisible(true);

            // LagBannerShow — 띠가 실제로 화면에 떴다(DiagRev=3). Reason 은 띠를 고른 이유(PeerSide · PendingProbe · Rtt).
            GameLog.Dev.Info(LogSystem, nameof(NetworkLagBannerUI), DisconnectDiag.MessagePrefix + " LagBannerShow",
                DisconnectDiag.Keys("LagBannerShow",
                    "Reason=" + NetworkLinkState.BlockReason + ", Phase=" + NetworkLinkState.Phase
                    + ", BlocksInput=" + (NetworkLinkState.BlocksPlayerCommands ? "True" : "False")
                    + ", HasText=" + (_messageText != null ? "True" : "False")));
        }

        /// <summary>띠를 내린다.</summary>
        private void Hide()
        {
            if (!_shown) return;
            _shown = false;
            ApplyVisible(false);

            // LagBannerHide — 띠가 내려갔다. ShownMs 는 떠 있던 시간, NextMode 는 이어지는 차단 방식(Overlay 면 덮개로 바뀐 것).
            long shownMs = (long)Math.Round((Time.realtimeSinceStartup - _shownAt) * 1000f);
            GameLog.Dev.Info(LogSystem, nameof(NetworkLagBannerUI), DisconnectDiag.MessagePrefix + " LagBannerHide",
                DisconnectDiag.Keys("LagBannerHide",
                    "ShownMs=" + shownMs + ", NextMode=" + NetworkLinkState.BlockMode
                    + ", Phase=" + NetworkLinkState.Phase + ", MatchEnded=" + (_matchEnded ? "True" : "False")));
        }

        /// <summary>띠의 실제 보임을 켜거나 끈다. 🔴 터치 차단은 보일 때도 끈다.</summary>
        private void ApplyVisible(bool visible)
        {
            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
                _group.blocksRaycasts = false; // 🔴 조작을 막지 않는다(사양 ⑤)
                _group.interactable = false;
            }
            if (_canvas != null) _canvas.enabled = visible;
        }

        /// <summary>
        /// 띠 아래 모든 그래픽의 터치 판정을 끈다(씬 값이 잘못 저장돼 있어도 런타임에서 한 번 더 막는다).
        /// 띠 오브젝트에 GraphicRaycaster 가 붙어 있으면 꺼 둔다(에디터 메뉴는 붙이지 않는다).
        /// </summary>
        private void MakePassThrough()
        {
            foreach (Graphic g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
        }
    }
}
