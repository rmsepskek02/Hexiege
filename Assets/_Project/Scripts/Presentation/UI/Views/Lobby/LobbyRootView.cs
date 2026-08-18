// ============================================================================
// LobbyRootView.cs
// 로비 씬의 루트 View. ViewModel 생성 및 하위 View에 주입.
//
// 역할:
//   - Start()에서 LobbyViewModel + BattleViewModel 생성
//   - 하위 View(TabBarView, BattleRootView)에 ViewModel 바인딩
//   - OnDestroy()에서 ViewModel Dispose 호출 (메모리 누수 방지)
//
// 씬 계층:
//   LobbyRoot (이 스크립트 부착)
//     ├─ TabBar (TabBarView)
//     └─ ContentArea
//          ├─ BattlePanel (BattleRootView)
//          ├─ ShopPanel (ShopView)
//          ├─ ProfilePanel (ProfileView)
//          ├─ SettingPanel (LobbySettingsView)
//          └─ RankingPanel (RankingView)
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UniRx;
using Hexiege.Application;      // GameLog — 런타임 로그 파사드 (LogRules.md 1.4)
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 로비 씬 루트. ViewModel 생성 및 하위 View에 주입하는 진입점.
    /// </summary>
    public class LobbyRootView : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("하위 View")]
        [Tooltip("탭 바 View")]
        [SerializeField] private TabBarView _tabBarView;

        [Tooltip("전투 탭 루트 View")]
        [SerializeField] private BattleRootView _battleRootView;

        [SerializeField] private ProfileView _profileView;

        [SerializeField] private RankingView _rankingView;

        [Header("탭 패널")]
        [Tooltip("전투 탭 패널 (BattleRootView 부착 오브젝트)")]
        [SerializeField] private GameObject _battlePanel;

        [Tooltip("상점 탭 패널")]
        [SerializeField] private GameObject _shopPanel;

        [Tooltip("프로필 탭 패널")]
        [SerializeField] private GameObject _profilePanel;

        [Tooltip("설정 탭 패널 (LobbySettingsView 부착 오브젝트)")]
        [SerializeField] private GameObject _settingPanel;

        [Tooltip("랭킹 탭 패널")]
        [SerializeField] private GameObject _rankingPanel;

        [Header("의존성")]
        [Tooltip("NetworkGameManager (DontDestroyOnLoad). 비워두면 자동 탐색.")]
        [SerializeField] private NetworkGameManager _networkGameManager;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private LobbyViewModel _lobbyViewModel;
        private BattleViewModel _battleViewModel;

        // 각 탭 패널 GameObject에 부착된 CanvasGroup 캐시.
        // 공통 UI 규칙 Rule 5: SetActive(false) 대신 CanvasGroup으로 표시/숨김을 처리해
        // LayoutGroup 안에서 공간이 사라져 레이아웃이 깨지는 현상을 방지한다.
        private CanvasGroup _battlePanelGroup;
        private CanvasGroup _shopPanelGroup;
        private CanvasGroup _profilePanelGroup;
        private CanvasGroup _settingPanelGroup;
        private CanvasGroup _rankingPanelGroup;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // 각 패널에 에디터에서 미리 부착된 CanvasGroup을 가져온다.
            // CanvasGroup이 없으면 null — SetupLobbyPanelCanvasGroups 에디터 스크립트 실행 필수.
            _battlePanelGroup  = _battlePanel?.GetComponent<CanvasGroup>();
            _shopPanelGroup    = _shopPanel?.GetComponent<CanvasGroup>();
            _profilePanelGroup = _profilePanel?.GetComponent<CanvasGroup>();
            _settingPanelGroup = _settingPanel?.GetComponent<CanvasGroup>();
            _rankingPanelGroup = _rankingPanel?.GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            // NetworkGameManager 자동 탐색 (DontDestroyOnLoad 오브젝트)
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            if (_networkGameManager == null)
            {
                // [개발] Warn + 개발 — LobbyUI 와 같은 판정이다.
                //   FindFirstObjectByType 은 네트워크 스폰 여부와 무관하게 씬의 활성 오브젝트를 찾으므로
                //   null = "씬(또는 DontDestroyOnLoad)에 없다" = 설정 오류다(1.3 원칙 3 단서).
                //   같은 사건에는 같은 축 값을 준다 — 계층마다 판정이 흔들리면 지표가 갈라진다.
                GameLog.Dev.Warn("UI", nameof(LobbyRootView),
                                 "NetworkGameManager 를 찾을 수 없다 — 로비 기능을 초기화하지 못한다");
                return;
            }

            // UGS 초기화 (Lobby 씬 진입 시 자동)
            _ = _networkGameManager.InitializeAsync();

            // ViewModel 생성
            _lobbyViewModel = new LobbyViewModel();
            _battleViewModel = new BattleViewModel(_networkGameManager);

            // 하위 View 바인딩
            if (_tabBarView != null)
                _tabBarView.Bind(_lobbyViewModel);

            if (_battleRootView != null)
                _battleRootView.Bind(_battleViewModel);

            // 탭 전환 구독 — 탭에 따라 패널 표시/숨김 (CanvasGroup 기반)
            if (_profileView == null && _profilePanel != null)
                _profileView = _profilePanel.GetComponent<ProfileView>();
            if (_rankingView == null && _rankingPanel != null)
                _rankingView = _rankingPanel.GetComponent<RankingView>();

            _lobbyViewModel.CurrentTab
                .Subscribe(tab =>
                {
                    SetPanelVisible(_battlePanelGroup, tab == LobbyViewModel.LobbyTab.Battle);
                    SetPanelVisible(_shopPanelGroup, tab == LobbyViewModel.LobbyTab.Shop);
                    SetPanelVisible(_profilePanelGroup, tab == LobbyViewModel.LobbyTab.Profile);
                    SetPanelVisible(_settingPanelGroup, tab == LobbyViewModel.LobbyTab.Setting);
                    SetPanelVisible(_rankingPanelGroup, tab == LobbyViewModel.LobbyTab.Ranking);

                    if (tab == LobbyViewModel.LobbyTab.Profile)
                    {
                        _profileView?.OnProfileTabShown();
                    }
                    else if (tab == LobbyViewModel.LobbyTab.Ranking)
                    {
                        _ = _rankingView?.RefreshAsync();
                    }
                })
                .AddTo(this);

            // [로딩 인디케이터 끄기] Lobby 씬이 준비된 시점이다.
            // 게임 포기(멀티)/로비 복귀/연결 끊김 복귀 등으로 다른 씬에서 켜둔
            // 전역 로딩 인디케이터를 여기서 끈다(UI 규칙 L-3).
            // 어디서 켰든 목적지 씬(Lobby)이 준비되면 자동으로 꺼지도록 책임을 일원화한다.
            UIManager.Instance?.ShowLoading(false);
        }

        private void OnDestroy()
        {
            // 하위 View 구독 해제
            if (_tabBarView != null)
                _tabBarView.Unbind();

            if (_battleRootView != null)
                _battleRootView.Unbind();

            // ViewModel 정리 (이벤트 구독 해제 + CompositeDisposable)
            _battleViewModel?.Dispose();
            _lobbyViewModel?.Dispose();
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// 패널 표시/숨김 처리 (CanvasGroup 기반).
        /// 공통 UI 규칙 Rule 5: SetActive 대신 CanvasGroup의 alpha/blocksRaycasts/interactable을 전환한다.
        ///
        ///   - 표시: alpha=1, blocksRaycasts=true, interactable=true
        ///   - 숨김: alpha=0, blocksRaycasts=false, interactable=false
        ///
        /// null 안전.
        /// </summary>
        /// <param name="group">대상 패널의 CanvasGroup.</param>
        /// <param name="visible">true=표시, false=숨김.</param>
        private static void SetPanelVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }
    }
}
