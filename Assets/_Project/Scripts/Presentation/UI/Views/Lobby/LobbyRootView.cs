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
//          └─ RankingPanel (RankingView)
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UniRx;
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

        [Header("탭 패널")]
        [Tooltip("전투 탭 패널 (BattleRootView 부착 오브젝트)")]
        [SerializeField] private GameObject _battlePanel;

        [Tooltip("상점 탭 패널")]
        [SerializeField] private GameObject _shopPanel;

        [Tooltip("프로필 탭 패널")]
        [SerializeField] private GameObject _profilePanel;

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

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Start()
        {
            // NetworkGameManager 자동 탐색 (DontDestroyOnLoad 오브젝트)
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            if (_networkGameManager == null)
            {
                Debug.LogError("[Lobby] LobbyRootView: NetworkGameManager를 찾을 수 없습니다.");
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

            // 탭 전환 구독 — 탭에 따라 패널 활성화/비활성화
            _lobbyViewModel.CurrentTab
                .Subscribe(tab =>
                {
                    SetPanelActive(_battlePanel, tab == LobbyViewModel.LobbyTab.Battle);
                    SetPanelActive(_shopPanel, tab == LobbyViewModel.LobbyTab.Shop);
                    SetPanelActive(_profilePanel, tab == LobbyViewModel.LobbyTab.Profile);
                    SetPanelActive(_rankingPanel, tab == LobbyViewModel.LobbyTab.Ranking);
                })
                .AddTo(this);
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
        /// 패널 활성화/비활성화. null 안전.
        /// </summary>
        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }
    }
}
