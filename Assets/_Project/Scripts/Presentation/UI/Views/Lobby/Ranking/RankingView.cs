// ============================================================================
// RankingView.cs
// 랭킹 탭 View. UGS Leaderboard 데이터를 표 형태로 표시한다.
//
// 역할:
//   - 탭 활성화 시 상위 랭킹(최대 100명)을 1회 로드한다.
//   - 10행씩 페이지네이션(최대 10페이지)으로 표시한다.
//   - 헤더 열을 탭하면 해당 열 기준으로 오름/내림차순 정렬을 전환한다.
//   - 이전/다음 페이지 버튼으로 페이지를 이동한다(추가 네트워크 로드 없이 캐시로 처리).
//
// 구현 방식(GameSystemRules_UI.md Ranking 탭 규칙 4):
//   외부 라이브러리 없이 ScrollRect + VerticalLayoutGroup + 헤더 Button 조합.
//   행 프리팹(RankRowView)을 Content 아래에 10개 인스턴스로 두고 재사용한다.
//
// 의존성:
//   본 View 는 ProfileView 와 동일한 방식으로, 필요한 서비스/UseCase 를 자체 생성한다.
//   Presentation 은 레이어 순서상 Infrastructure 보다 바깥이므로 Infrastructure 참조가 허용된다
//   (Application → Infrastructure 역참조만 금지).
//
// 주의(데이터 로드 시점):
//   로비 탭은 CanvasGroup(alpha) 로 전환되어 GameObject 가 계속 활성 상태이므로,
//   OnEnable 은 씬 진입 시 1회만 발화한다. 즉 랭킹 로드는 "탭 클릭" 이 아니라 "로비 진입" 시
//   이뤄진다. 탭 클릭 시점 로드가 필요하면 LobbyRootView 의 탭 전환 구독에서 RefreshAsync()
//   를 호출하도록 별도 배선이 필요하다(본 작업 범위 밖 — Inspector/씬 작업 단계에서 검토).
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 랭킹 탭 View.
    /// </summary>
    public class RankingView : MonoBehaviour
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>한 페이지에 표시하는 행 수.</summary>
        private const int RowsPerPage = 10;

        // ====================================================================
        // 정렬 상태
        // ====================================================================

        /// <summary>정렬 기준 열(순위 열은 고정이므로 정렬 대상이 아니다).</summary>
        private enum SortColumn
        {
            WinRate,   // 승률 (기본 정렬 기준)
            Nickname,  // 닉네임(텍스트)
            Games,     // 총 게임 수
            Wins,      // 승리
            Losses     // 패배
        }

        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("헤더")]
        [Tooltip("헤더 행 부모. 자식으로 열 헤더 Button 들을 둔다(좌→우: 순위/닉네임/승률/게임수/승/패).")]
        [SerializeField] private Transform _headerRow;

        [Header("스크롤 리스트")]
        [Tooltip("랭킹 목록 스크롤 영역.")]
        [SerializeField] private ScrollRect _scrollRect;

        [Tooltip("VerticalLayoutGroup 이 붙은 Content. 여기에 행 프리팹 인스턴스를 생성한다.")]
        [SerializeField] private Transform _content;

        [Tooltip("행 프리팹(RankRowView). Content 아래에 10개 인스턴스로 재사용한다.")]
        [SerializeField] private RankRowView _rowPrefab;

        [Header("페이지네이션")]
        [Tooltip("이전 페이지 버튼.")]
        [SerializeField] private Button _prevPageButton;

        [Tooltip("다음 페이지 버튼.")]
        [SerializeField] private Button _nextPageButton;

        [Tooltip("현재 페이지 표시 텍스트(예: 1 / 10).")]
        [SerializeField] private TextMeshProUGUI _pageText;

        // ====================================================================
        // 런타임 상태
        // ====================================================================

        private RankingUseCase _rankingUseCase;

        // 로드된 전체 랭킹(최대 100명). 페이지 이동/정렬은 이 캐시로 처리한다.
        private readonly List<LeaderboardEntry> _entries = new List<LeaderboardEntry>();

        // Content 아래에 생성한 행 인스턴스 풀(10개).
        private readonly List<RankRowView> _rows = new List<RankRowView>();

        // 헤더 버튼(좌→우 순서). _headerRow 자식에서 자동 수집.
        private Button[] _headerButtons;

        private SortColumn _sortColumn = SortColumn.WinRate; // 기본: 승률
        private bool _sortAscending = false;                 // 기본: 내림차순
        private int _currentPage = 0;

        private bool _initialized = false;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Start()
        {
            InitializeIfNeeded();
            _ = RefreshAsync();
        }

        /// <summary>
        /// 탭(또는 오브젝트)이 활성화될 때 랭킹을 갱신한다.
        /// 최초 활성화 시에는 Start 에서 로드하므로 중복 로드를 피하기 위해 초기화 이후에만 갱신한다.
        /// </summary>
        private void OnEnable()
        {
            if (_initialized)
                _ = RefreshAsync();
        }

        private void OnDestroy()
        {
            if (_prevPageButton != null) _prevPageButton.onClick.RemoveAllListeners();
            if (_nextPageButton != null) _nextPageButton.onClick.RemoveAllListeners();

            if (_headerButtons != null)
            {
                foreach (var btn in _headerButtons)
                    if (btn != null) btn.onClick.RemoveAllListeners();
            }
        }

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 서비스/행 풀/버튼 리스너를 1회 구성한다.
        /// </summary>
        private void InitializeIfNeeded()
        {
            if (_initialized)
                return;

            // UseCase 생성(구현체는 Infrastructure — Presentation 에서 생성 허용).
            ILeaderboardService leaderboardService = new LeaderboardService();
            _rankingUseCase = new RankingUseCase(leaderboardService);

            BuildRowPool();
            BindHeaderButtons();

            if (_prevPageButton != null) _prevPageButton.onClick.AddListener(OnPrevPage);
            if (_nextPageButton != null) _nextPageButton.onClick.AddListener(OnNextPage);

            _initialized = true;
        }

        /// <summary>
        /// 행 프리팹을 Content 아래에 RowsPerPage 개 생성해 풀로 보관한다.
        /// </summary>
        private void BuildRowPool()
        {
            if (_rowPrefab == null || _content == null)
            {
                Debug.LogWarning("[RankingView] _rowPrefab 또는 _content 가 연결되지 않아 행 풀을 만들 수 없습니다.");
                return;
            }

            for (int i = 0; i < RowsPerPage; i++)
            {
                RankRowView row = Instantiate(_rowPrefab, _content);
                row.Clear();
                _rows.Add(row);
            }
        }

        /// <summary>
        /// 헤더 행 자식에서 Button 들을 수집하고, 열 인덱스에 맞춰 정렬 콜백을 등록한다.
        /// 열 순서(좌→우): 0 순위(고정) / 1 닉네임 / 2 승률 / 3 게임수 / 4 승 / 5 패.
        /// </summary>
        private void BindHeaderButtons()
        {
            if (_headerRow == null)
                return;

            _headerButtons = _headerRow.GetComponentsInChildren<Button>(includeInactive: true);

            for (int i = 0; i < _headerButtons.Length; i++)
            {
                int columnIndex = i; // 클로저 캡처 방지용 지역 복사
                Button btn = _headerButtons[i];
                if (btn == null) continue;

                btn.onClick.AddListener(() => OnHeaderClicked(columnIndex));
            }
        }

        // ====================================================================
        // 데이터 로드
        // ====================================================================

        /// <summary>
        /// 랭킹 데이터를 로드하고 첫 페이지를 표시한다.
        /// 로딩 중에는 전역 로딩 인디케이터를 표시한다(GameSystemRules_UI.md Ranking 규칙 6).
        /// </summary>
        public async System.Threading.Tasks.Task RefreshAsync()
        {
            if (_rankingUseCase == null)
                return;

            UIManager.Instance?.ShowLoading(true, "랭킹 불러오는 중...");

            try
            {
                List<LeaderboardEntry> loaded = await _rankingUseCase.GetRankingsAsync();

                _entries.Clear();
                if (loaded != null)
                    _entries.AddRange(loaded);

                // 기본 정렬(승률 내림차순) 적용 후 첫 페이지 표시.
                _sortColumn = SortColumn.WinRate;
                _sortAscending = false;
                ApplySort();

                _currentPage = 0;
                RenderCurrentPage();
            }
            finally
            {
                UIManager.Instance?.ShowLoading(false);
            }
        }

        // ====================================================================
        // 정렬
        // ====================================================================

        /// <summary>
        /// 헤더 열 클릭 처리. 순위(0)는 고정 열이므로 무시한다.
        /// 같은 열을 다시 누르면 오름/내림차순을 전환한다.
        /// </summary>
        /// <param name="columnIndex">클릭된 열 인덱스(0=순위 고정).</param>
        private void OnHeaderClicked(int columnIndex)
        {
            // 열 인덱스 → 정렬 기준 매핑. 0(순위)은 고정.
            SortColumn? target = columnIndex switch
            {
                1 => SortColumn.Nickname,
                2 => SortColumn.WinRate,
                3 => SortColumn.Games,
                4 => SortColumn.Wins,
                5 => SortColumn.Losses,
                _ => (SortColumn?)null // 0(순위) 또는 범위 밖 → 정렬 안 함
            };

            if (target == null)
                return;

            if (_sortColumn == target.Value)
            {
                // 같은 열 재클릭 → 방향 토글.
                _sortAscending = !_sortAscending;
            }
            else
            {
                // 새 열 → 해당 열 기준, 숫자/승률은 내림차순 우선, 닉네임은 오름차순 우선.
                _sortColumn = target.Value;
                _sortAscending = target.Value == SortColumn.Nickname;
            }

            ApplySort();
            _currentPage = 0;
            RenderCurrentPage();
        }

        /// <summary>
        /// 현재 정렬 기준/방향으로 _entries 를 정렬한다.
        /// 순위 열 값(entry.Rank)은 리더보드 원본 순위(승률 기준)이며 정렬로 바뀌지 않는다.
        /// </summary>
        private void ApplySort()
        {
            _entries.Sort((a, b) =>
            {
                int cmp = _sortColumn switch
                {
                    SortColumn.Nickname => string.Compare(
                        a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase),
                    SortColumn.Games => a.TotalGames.CompareTo(b.TotalGames),
                    SortColumn.Wins => a.Wins.CompareTo(b.Wins),
                    SortColumn.Losses => a.Losses.CompareTo(b.Losses),
                    _ => a.WinRate.CompareTo(b.WinRate) // WinRate
                };

                return _sortAscending ? cmp : -cmp;
            });
        }

        // ====================================================================
        // 페이지네이션
        // ====================================================================

        /// <summary>이전 페이지로 이동.</summary>
        private void OnPrevPage()
        {
            if (_currentPage <= 0)
                return;

            _currentPage--;
            RenderCurrentPage();
        }

        /// <summary>다음 페이지로 이동.</summary>
        private void OnNextPage()
        {
            if (_currentPage >= PageCount - 1)
                return;

            _currentPage++;
            RenderCurrentPage();
        }

        /// <summary>전체 페이지 수(최소 1).</summary>
        private int PageCount
        {
            get
            {
                int pages = (_entries.Count + RowsPerPage - 1) / RowsPerPage;
                return Mathf.Max(1, pages);
            }
        }

        /// <summary>
        /// 현재 페이지 데이터를 행 풀에 바인딩하고, 페이지 텍스트/버튼 상태를 갱신한다.
        /// </summary>
        private void RenderCurrentPage()
        {
            int startIndex = _currentPage * RowsPerPage;

            for (int i = 0; i < _rows.Count; i++)
            {
                int dataIndex = startIndex + i;
                if (dataIndex < _entries.Count)
                    _rows[i].Bind(_entries[dataIndex]);
                else
                    _rows[i].Clear();
            }

            // 페이지 표시(1-base).
            if (_pageText != null)
                _pageText.text = $"{_currentPage + 1} / {PageCount}";

            // 페이지 버튼 활성화 상태 갱신(양 끝에서 비활성).
            if (_prevPageButton != null) _prevPageButton.interactable = _currentPage > 0;
            if (_nextPageButton != null) _nextPageButton.interactable = _currentPage < PageCount - 1;

            // 스크롤을 맨 위로.
            if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
