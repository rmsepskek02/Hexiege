// ============================================================================
// NetworkLagOverlayUI.cs
// 인게임 연결 지연 덮개 — 반투명 배경 + 가운데 「네트워크 지연」/「연결 복구 중」 (2026-11-26 신설).
//
// [무엇을 하나 — 초급 개발자용 설명]
//   경기 중 상대 신호가 끊기면(지연 · 판정 중) 게임 화면 전체를 반투명하게 덮고 가운데에 글자를 보여 준다.
//   덮개는 화면 터치를 받아 먹어서 그 뒤의 게임 조작(건설 · 생산 · 패널 버튼 · 타일 탭)이 눌리지 않게 한다.
//   게임 자체는 멈추지 않고 뒤에서 계속 진행된다(U-2 · U-5).
//   🔴 단, **설정 여는 버튼 · 설정 창 · 포기 확인 팝업**은 덮개 위에 올라와 눌린다(설정 · 포기는 할 수 있어야 한다 — U-5).
//
// [정렬 순서(Canvas SortingOrder) — GameSystemRules_CanvasSortingOrder.md 대역 규칙 기준]
//   HUD 0 < UIManager 공용 막 100 < 일반 패널 200 < 🆕 지연 덮개 210 < 🆕 설정 여는 버튼 211 < 🆕 설정 창 212
//     < 모달 팝업(ConfirmPopup · 재경기 팝업) 250 < 로딩 300
//   - 덮개가 일반 패널(200 — 건물 · 생산 · 연구 · 스킬 패널) **위**라서, 덮개가 뜨기 전에 열려 있던 패널의 버튼도 막힌다.
//   - 설정 여는 버튼(평소에는 HUD 0)과 설정 창(평소 200)은 **덮개가 떠 있는 동안에만** 211 · 212 로 올리고,
//     덮개가 내려가면 원래대로 되돌린다(평소 화면의 그리기 순서는 한 칸도 바뀌지 않는다).
//   - 포기 확인 팝업(ConfirmPopup 프리팹, 250)과 로딩(300)은 원래 덮개보다 위라 손대지 않는다.
//   - 결과 화면(GameEndPanel 200)은 덮개보다 아래 값이지만, **결과가 나면 덮개를 내리므로** 겹치지 않는다.
//   ⚠️ 210 · 211 · 212 는 대역 표에 아직 없는 값이다 — 규칙 2(대역 밖 값은 문서 갱신)에 따라 테스트 후
//      문서에 반영한다(Plan 9-3, 메인 세션 보고 사항).
//
// [왜 공용 막(UIManager.ShowBlockingOverlay)을 쓰지 않았나 — Plan 3절 6단계 확인 결과]
//   ① 공용 막은 점유 횟수 하나와 「막을 누르면 부를 함수」 칸 하나를 여러 UI(설정 창 · 확인 팝업 · 건물 패널)가 함께 쓴다.
//      지연 중에 설정 창을 열면 그 칸이 설정 창 닫기로 덮어써지고, 닫는 순서에 따라 남는 막이 달라진다.
//   ② 공용 막은 패널(200)·팝업(250) **뒤**(100)에 그려지는 「배경」이라 열려 있던 패널을 막지 못하고,
//      가운데 글자를 둘 자리도 없다. → 그래서 전용 덮개를 씬에 둔다(U-17 · Plan 기본안).
//
// [씬 배치] Game.unity `[UI]` 바로 아래의 `NetworkLagOverlay` 오브젝트. 1회성 에디터 메뉴
//   `Hexiege/Setup/Network Lag Overlay - Place In Game Scene`(Assets/Editor/Tools/NetworkLagOverlaySetup.cs)가 만든다.
//   🔴 덮개가 씬에 없어도 게임은 예외 없이 돈다 — 감시기가 조작 차단 · 판정을 그대로 하고 경고 로그만 남긴다.
//
// [2026-11-26 2차 수정 — 사양 ②] 이 덮개는 이제 「차단 방식 Overlay」일 때만 뜬다(NetworkLinkState.ShowsLagOverlay 가 그 뜻으로 바뀌었다).
//   Host 가 상대 쪽 문제로 지연을 볼 때 · RTT 만 느릴 때는 덮개 대신 조작을 막지 않는 알림 띠(NetworkLagBannerUI)가 뜬다.
//   덮개 자체의 코드(켜기 · 끄기 · 설정 버튼 올리기)는 바뀌지 않았다. 1차의 「최소 표시 1초」는 상태기계 쪽에서 없어졌으므로
//   신호가 돌아오면 덮개도 그 순간 내려간다(사양 ③).
//
// Presentation 레이어 — Unity 의존(MonoBehaviour). Unity.Netcode 참조 없음.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Hexiege.Application;
using Hexiege.Infrastructure; // DisconnectDiag(진단 로그 공통 키) — GameEndUI 와 같은 사용 방식

namespace Hexiege.Presentation
{
    /// <summary>
    /// 인게임 연결 지연 덮개. NetworkLinkState(연결 상태)를 보고 스스로 켜고 끈다.
    /// 덮개가 떠 있는 동안 설정 여는 버튼 · 설정 창을 덮개 위로 올린다.
    /// </summary>
    public sealed class NetworkLagOverlayUI : MonoBehaviour, IPointerDownHandler
    {
        // ====================================================================
        // 상수 — 정렬 값 · 문구 (코드가 단일 소스: Awake 에서 씬 값을 이 값으로 맞춘다)
        // ====================================================================

        /// <summary>덮개 Canvas 정렬 값 — 일반 패널(200) 위, 모달(250) 아래.</summary>
        public const int OverlaySortingOrder = 210;

        /// <summary>덮개가 떠 있는 동안 설정 여는 버튼의 정렬 값 — 덮개 바로 위.</summary>
        public const int SettingsButtonSortingOrder = 211;

        /// <summary>덮개가 떠 있는 동안 설정 창의 정렬 값 — 설정 여는 버튼 위(평소 순서 「버튼 아래 · 창 위」 유지).</summary>
        public const int SettingsPanelSortingOrder = 212;

        /// <summary>지연 · 판정 중 문구(U-5).</summary>
        private const string DelayMessage = "네트워크 지연";

        /// <summary>13초 미만으로 앱이 돌아온 직후 문구(U-6).</summary>
        private const string RecoveringMessage = "연결 복구 중";

        /// <summary>로그 카테고리.</summary>
        private const string LogSystem = "UI";

        // ====================================================================
        // Inspector 참조 — 1회성 에디터 메뉴가 채운다
        // ====================================================================

        [Header("덮개 구성 (에디터 메뉴가 채운다)")]
        [Tooltip("덮개 자신의 Canvas(Override Sorting). 정렬 값은 코드 상수 OverlaySortingOrder 가 단일 소스다.")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("덮개 표시/숨김용 CanvasGroup(alpha · 터치 차단).")]
        [SerializeField] private CanvasGroup _group;

        [Tooltip("가운데 문구 텍스트. 표시 문구는 이 클래스 상수가 단일 소스다(여기에 문구를 적지 않는다).")]
        [SerializeField] private TextMeshProUGUI _messageText;

        [Header("덮개 위로 올릴 대상 (에디터 메뉴가 채운다)")]
        [Tooltip("HUD 의 설정 여는 버튼(GameHudUI._settingsButton 과 같은 오브젝트).")]
        [SerializeField] private Button _settingsButton;

        [Tooltip("인게임 설정 창 컨트롤러(열림 여부 확인용).")]
        [SerializeField] private InGameSettingsUI _settingsUI;

        [Tooltip("인게임 설정 창 본체의 Canvas(평소 Override Sorting 200).")]
        [SerializeField] private Canvas _settingsPanelCanvas;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private bool _shown;                    // 덮개가 지금 떠 있는가
        private bool _matchEnded;               // 이번 경기 결과가 이미 났는가(결과 화면이 우선 — 덮개를 다시 띄우지 않는다)
        private int _tapCount;                  // 이번 지연 동안 덮개가 막은 터치 수
        private Canvas _settingsButtonCanvas;   // 설정 여는 버튼을 올리기 위해 붙인 Canvas(처음 올릴 때 만든다)
        private bool _settingsButtonRaised;     // 설정 여는 버튼을 지금 덮개 위로 올려 두었는가
        private bool _settingsPanelRaised;      // 설정 창을 지금 덮개 위로 올려 두었는가
        private int _settingsPanelOriginalOrder;// 설정 창의 원래 정렬 값(되돌릴 때 쓴다)
        private IDisposable _linkStateSubscription;
        private IDisposable _gameEndSubscription;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            NetworkLinkState.SetLagOverlayPresent(true);
            ResolveMissingReferences();

            // 🔴 정렬 값은 코드 상수가 이긴다 — 씬에 다른 값이 저장돼 있어도 여기서 맞춘다
            //    (씬 직렬화 값이 코드 기본값을 이기는 함정을 반대로 막는 방식).
            if (_canvas != null)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = OverlaySortingOrder;
            }
            ApplyVisible(false);
        }

        private void Start()
        {
            // 상태가 바뀔 때마다 다시 판단한다. 🔴 블록 본문 람다(LogRules §1.7 관례).
            _linkStateSubscription = GameEvents.OnNetworkLinkStateChanged.Subscribe(e => { OnLinkStateChanged(e); });

            // 결과가 나면 덮개를 곧바로 내린다 — 감시기의 「결과 났음」 표시보다 이 구독이 먼저 불릴 수 있으므로
            // NetworkLinkState 를 기다리지 않고 직접 듣는다(구독 순서는 보장되지 않는다).
            _gameEndSubscription = GameEvents.OnGameEnd.Subscribe(_ => { OnMatchEnded(); });

            Refresh();
        }

        private void Update()
        {
            // 덮개가 떠 있는 동안만: 설정 창이 열려 있으면 여는 버튼은 덮개 아래로 내린다(열린 창 위에서 버튼이
            //   다시 눌려 Show() 가 두 번 불리지 않게 — 평소에도 열린 설정 창 뒤의 버튼은 공용 막에 가려 눌리지 않는다).
            if (!_shown) return;
            bool settingsOpen = _settingsUI != null && _settingsUI.IsShown;
            SetSettingsButtonRaised(!settingsOpen);
        }

        private void OnDestroy()
        {
            _linkStateSubscription?.Dispose();
            _gameEndSubscription?.Dispose();
            RestoreRaisedTargets();
            NetworkLinkState.SetLagOverlayPresent(false);
        }

        // ====================================================================
        // 이벤트
        // ====================================================================

        /// <summary>연결 상태가 바뀌었다 — 새 경기 시작이면 결과 표시를 지우고 다시 판단한다.</summary>
        private void OnLinkStateChanged(NetworkLinkStateChangedEvent e)
        {
            if (e.Phase == InGameLinkPhase.Waiting) _matchEnded = false; // 새 경기 시작
            if (e.Phase == InGameLinkPhase.Ended) _matchEnded = true;
            Refresh();
        }

        /// <summary>결과가 났다 — 덮개를 내린다(결과 화면이 우선 — Plan 2-6 「결과 화면이 뜨면 오버레이는 내려간다」).</summary>
        private void OnMatchEnded()
        {
            _matchEnded = true;
            Refresh();
        }

        /// <summary>
        /// 덮개가 터치를 막았다(덮개 배경 Image 가 받는다). 지연 1회당 첫 1줄 + 내려갈 때 합계 1줄(T-10).
        /// </summary>
        /// <param name="eventData">터치 정보(쓰지 않는다).</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_shown) return;
            _tapCount++;
            if (_tapCount == 1)
            {
                GameLog.Dev.Info(LogSystem, nameof(NetworkLagOverlayUI), DisconnectDiag.MessagePrefix + " LagOverlayTap",
                    DisconnectDiag.Keys("LagOverlayTap", "TapCount=1, Stage=First, Phase=" + NetworkLinkState.Phase));
            }
        }

        // ====================================================================
        // 표시 / 숨김
        // ====================================================================

        /// <summary>지금 상태로 덮개를 켜거나 끈다.</summary>
        private void Refresh()
        {
            bool want = !_matchEnded && NetworkLinkState.ShowsLagOverlay;
            if (want) Show(NetworkLinkState.IsRecovering);
            else Hide();
        }

        /// <summary>덮개를 띄운다(이미 떠 있으면 문구만 바꾼다).</summary>
        private void Show(bool recovering)
        {
            if (_messageText != null) _messageText.text = recovering ? RecoveringMessage : DelayMessage;
            if (_shown) return;

            _shown = true;
            _tapCount = 0;
            ApplyVisible(true);
            SetSettingsPanelRaised(true);
            SetSettingsButtonRaised(_settingsUI == null || !_settingsUI.IsShown);

            // LagOverlayShow — 덮개가 실제로 화면에 떴다(T-3 · T-15 보조). 참조 누락도 함께 보인다.
            GameLog.Dev.Info(LogSystem, nameof(NetworkLagOverlayUI), DisconnectDiag.MessagePrefix + " LagOverlayShow",
                DisconnectDiag.Keys("LagOverlayShow",
                    "Text=" + (recovering ? "Recovering" : "Delay")
                    + ", Phase=" + NetworkLinkState.Phase
                    // [2026-11-26 2차 — DiagRev=3] 덮개를 고른 이유(SelfProbeFailed · HostLost · Recovering)
                    + ", Reason=" + NetworkLinkState.BlockReason
                    + ", HasSettingsButton=" + (_settingsButton != null ? "True" : "False")
                    + ", HasSettingsPanelCanvas=" + (_settingsPanelCanvas != null ? "True" : "False")));
        }

        /// <summary>덮개를 내리고 올려 두었던 설정 버튼 · 창을 원래대로 되돌린다.</summary>
        private void Hide()
        {
            if (!_shown) return;
            _shown = false;
            ApplyVisible(false);
            RestoreRaisedTargets();

            // LagOverlayTap(합계) — 이번 지연 동안 막은 터치 수(T-10). 0 이면 남기지 않는다.
            if (_tapCount > 0)
            {
                GameLog.Dev.Info(LogSystem, nameof(NetworkLagOverlayUI), DisconnectDiag.MessagePrefix + " LagOverlayTap",
                    DisconnectDiag.Keys("LagOverlayTap", "TapCount=" + _tapCount + ", Stage=Summary"));
            }
            GameLog.Dev.Info(LogSystem, nameof(NetworkLagOverlayUI), DisconnectDiag.MessagePrefix + " LagOverlayHide",
                DisconnectDiag.Keys("LagOverlayHide", "Phase=" + NetworkLinkState.Phase + ", MatchEnded=" + (_matchEnded ? "True" : "False")));
            _tapCount = 0;
        }

        /// <summary>덮개의 실제 보임/터치 차단을 켜거나 끈다.</summary>
        private void ApplyVisible(bool visible)
        {
            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
                _group.blocksRaycasts = visible;
                _group.interactable = visible;
            }
            // Canvas 를 꺼 두면 숨긴 동안 그리기·터치 판정 비용이 없다(CanvasGroup 은 위 값으로 이중 방어).
            if (_canvas != null) _canvas.enabled = visible;
        }

        // ====================================================================
        // 설정 버튼 · 설정 창 올리기 / 되돌리기
        // ====================================================================

        /// <summary>
        /// 설정 여는 버튼을 덮개 위로 올리거나(true) 원래 자리로 내린다(false).
        /// 🔴 처음 올릴 때 버튼 오브젝트에 Canvas + GraphicRaycaster 를 붙인다(씬 파일에는 없다 — 런타임에만 생긴다).
        ///    내릴 때는 Canvas 를 지우지 않고 Override Sorting 만 끈다 — 그러면 부모 화면의 원래 순서대로 그려진다.
        /// </summary>
        private void SetSettingsButtonRaised(bool raise)
        {
            if (_settingsButton == null || raise == _settingsButtonRaised) return;
            try
            {
                if (raise && _settingsButtonCanvas == null)
                {
                    GameObject go = _settingsButton.gameObject;
                    _settingsButtonCanvas = go.GetComponent<Canvas>();
                    if (_settingsButtonCanvas == null) _settingsButtonCanvas = go.AddComponent<Canvas>();
                    // 독립 Canvas 에는 자체 GraphicRaycaster 가 있어야 버튼이 눌린다(CanvasSortingOrder 규칙 1).
                    if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
                }
                if (_settingsButtonCanvas == null) return;

                _settingsButtonCanvas.overrideSorting = raise;
                if (raise) _settingsButtonCanvas.sortingOrder = SettingsButtonSortingOrder;
                _settingsButtonRaised = raise;
            }
            catch (Exception e)
            {
                GameLog.Dev.Warn(LogSystem, nameof(NetworkLagOverlayUI),
                    "설정 여는 버튼을 덮개 위로 올리지 못했다 — 지연 중 설정을 열 수 없을 수 있다",
                    "Exception=" + e.GetType().Name);
                _settingsButton = null; // 같은 실패를 매 프레임 반복하지 않게 이후 시도를 멈춘다
            }
        }

        /// <summary>설정 창을 덮개 위로 올리거나(true) 원래 정렬 값으로 되돌린다(false).</summary>
        private void SetSettingsPanelRaised(bool raise)
        {
            if (_settingsPanelCanvas == null || raise == _settingsPanelRaised) return;
            if (raise)
            {
                _settingsPanelOriginalOrder = _settingsPanelCanvas.sortingOrder;
                _settingsPanelCanvas.sortingOrder = SettingsPanelSortingOrder;
            }
            else
            {
                _settingsPanelCanvas.sortingOrder = _settingsPanelOriginalOrder;
            }
            _settingsPanelRaised = raise;
        }

        /// <summary>올려 두었던 것을 모두 원래대로 되돌린다(숨김 · 파괴 때).</summary>
        private void RestoreRaisedTargets()
        {
            SetSettingsButtonRaised(false);
            SetSettingsPanelRaised(false);
        }

        // ====================================================================
        // 참조 보완 — 에디터 메뉴가 빠졌거나 일부만 채운 경우
        // ====================================================================

        /// <summary>
        /// 비어 있는 참조를 같은 씬에서 한 번 찾아 채운다. 못 찾으면 경고만 남기고 그 기능 없이 동작한다
        /// (덮개 자체 · 조작 차단 · 판정은 영향이 없다).
        /// </summary>
        private void ResolveMissingReferences()
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_messageText == null) _messageText = GetComponentInChildren<TextMeshProUGUI>(true);

            if (_settingsUI == null)
                _settingsUI = FindFirstObjectByType<InGameSettingsUI>(FindObjectsInactive.Include);

            if (_settingsButton == null)
            {
                // 에디터 메뉴가 GameHudUI 의 _settingsButton 을 그대로 복사해 넣는다. 비어 있으면 같은 이름의 자식을 찾는다
                // (GameHudUI 머리말의 씬 구조: GameHUD/SettingsButton).
                GameHudUI hud = FindFirstObjectByType<GameHudUI>(FindObjectsInactive.Include);
                Transform t = hud != null ? hud.transform.Find("SettingsButton") : null;
                if (t != null) _settingsButton = t.GetComponent<Button>();
            }

            if (_settingsPanelCanvas == null && _settingsUI != null)
            {
                // 설정 창 본체는 Override Sorting 을 켠 Canvas 를 가진 자식이다(씬 실측: InGameSettingsPanel/Panel, 200).
                foreach (Canvas c in _settingsUI.GetComponentsInChildren<Canvas>(true))
                {
                    if (c.overrideSorting) { _settingsPanelCanvas = c; break; }
                }
            }

            if (_settingsButton == null || _settingsPanelCanvas == null || _messageText == null || _canvas == null)
            {
                GameLog.Dev.Warn(LogSystem, nameof(NetworkLagOverlayUI),
                    "지연 덮개 참조 일부를 찾지 못했다 — 해당 기능 없이 동작한다",
                    "HasCanvas=" + (_canvas != null) + ", HasText=" + (_messageText != null)
                    + ", HasSettingsButton=" + (_settingsButton != null)
                    + ", HasSettingsPanelCanvas=" + (_settingsPanelCanvas != null));
            }
        }
    }
}
