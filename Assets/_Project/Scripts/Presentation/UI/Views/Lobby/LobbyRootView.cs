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

        // 각 탭 패널 GameObject에 부착된 CanvasGroup 캐시.
        // 공통 UI 규칙 Rule 5: SetActive(false) 대신 CanvasGroup으로 표시/숨김을 처리해
        // LayoutGroup 안에서 공간이 사라져 레이아웃이 깨지는 현상을 방지한다.
        private CanvasGroup _battlePanelGroup;
        private CanvasGroup _shopPanelGroup;
        private CanvasGroup _profilePanelGroup;
        private CanvasGroup _rankingPanelGroup;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // 각 탭 패널에 CanvasGroup을 확보(없으면 자동 추가).
            // Inspector에서 별도로 연결할 필요 없도록 런타임에 GetComponent → AddComponent 패턴 사용.
            _battlePanelGroup = EnsureCanvasGroup(_battlePanel);
            _shopPanelGroup = EnsureCanvasGroup(_shopPanel);
            _profilePanelGroup = EnsureCanvasGroup(_profilePanel);
            _rankingPanelGroup = EnsureCanvasGroup(_rankingPanel);
        }

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

            // 탭 전환 구독 — 탭에 따라 패널 표시/숨김 (CanvasGroup 기반)
            _lobbyViewModel.CurrentTab
                .Subscribe(tab =>
                {
                    SetPanelVisible(_battlePanelGroup, tab == LobbyViewModel.LobbyTab.Battle);
                    SetPanelVisible(_shopPanelGroup, tab == LobbyViewModel.LobbyTab.Shop);
                    SetPanelVisible(_profilePanelGroup, tab == LobbyViewModel.LobbyTab.Profile);
                    SetPanelVisible(_rankingPanelGroup, tab == LobbyViewModel.LobbyTab.Ranking);
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
        /// 대상 GameObject에 CanvasGroup이 없으면 추가하여 보장한다.
        /// CanvasGroup은 alpha 0/blocksRaycasts false 조합으로 GameObject 활성 상태를 유지하면서
        /// 사용자에게 보이지 않도록 만든다 — LayoutGroup의 공간이 사라지지 않음.
        /// null 안전.
        /// </summary>
        /// <param name="target">CanvasGroup을 확보할 대상 GameObject.</param>
        /// <returns>확보된 CanvasGroup. target이 null이면 null.</returns>
        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            if (target == null)
                return null;

            // 이미 부착되어 있으면 그대로 사용, 없으면 새로 추가.
            if (!target.TryGetComponent<CanvasGroup>(out var group))
                group = target.AddComponent<CanvasGroup>();

            return group;
        }

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
