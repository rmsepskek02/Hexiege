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
// 구현 방식:
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
//   OnEnable 은 씬 진입 시 1회만 발화한다. 그래서 OnEnable/Start 는 데이터를 불러오지 않고
//   서비스·행 풀·버튼 리스너를 구성하는 초기화만 한다.
//   랭킹 데이터를 실제로 불러오는 시점은 두 곳이며 둘 다 이미 배선돼 있다.
//     - 랭킹 탭을 고른 순간 — LobbyRootView 의 탭 전환 구독이 이 View 의 RefreshAsync 를 부른다.
//     - 새로고침 버튼을 누른 순간 — 아래 OnRefreshClicked.
//   즉 숨겨진 랭킹 패널이 로비 진입만으로 네트워크를 때리는 일은 없다.
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

        [Header("새로고침 / 빈 상태")]
        [Tooltip("랭킹 목록을 수동으로 다시 불러오는 새로고침 버튼(확정 결정 4).")]
        [SerializeField] private Button _refreshButton;

        [Tooltip("등재 인원이 0명일 때 표시하는 빈 상태 안내 텍스트(확정 결정 4). " +
                 "표시/숨김은 CanvasGroup 으로 처리한다(공통 UI 규칙 5).")]
        [SerializeField] private TextMeshProUGUI _emptyStateText;

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

        // 헤더 열의 원본 라벨(정렬 화살표를 붙이기 전 텍스트). 좌→우 순서로,
        // CreateRankingTable.ColumnLabels 와 동일하게 유지해야 한다.
        private static readonly string[] _columnLabels = { "순위", "닉네임", "승률", "게임수", "승", "패" };

        // 빈 상태 안내 / 목록 / 페이지네이션의 표시·숨김을 CanvasGroup 으로 처리하기 위한 캐시.
        // (공통 UI 규칙 5: SetActive 대신 CanvasGroup alpha/blocksRaycasts/interactable 사용)
        private CanvasGroup _emptyStateGroup;   // 빈 상태 안내 텍스트
        private CanvasGroup _listGroup;         // ScrollView(행 목록)
        private CanvasGroup _paginationGroup;   // PaginationRow(이전/페이지/다음)

        private SortColumn _sortColumn = SortColumn.WinRate; // 기본: 승률
        private bool _sortAscending = false;                 // 기본: 내림차순
        private int _currentPage = 0;

        /// <summary>표시용 목록만 선택한다. 개발 미리보기는 실제 조회 목록에 섞이지 않는다.</summary>
        private IReadOnlyList<LeaderboardEntry> ActiveEntries
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_editorLayoutPreview) return _previewEntries;
#endif
                return _entries;
            }
        }

        private bool _initialized = false;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Start()
        {
            InitializeIfNeeded();
        }

        /// <summary>
        /// 오브젝트가 활성화될 때 초기화만 수행한다(데이터는 불러오지 않는다).
        /// Start 가 이미 초기화했으면 아무 일도 하지 않는다.
        /// 랭킹 데이터 로드 시점은 이 파일 머리말 「주의(데이터 로드 시점)」 참조.
        /// </summary>
        private void OnEnable()
        {
            InitializeIfNeeded();
        }

        private void OnDestroy()
        {
            if (_prevPageButton != null) _prevPageButton.onClick.RemoveAllListeners();
            if (_nextPageButton != null) _nextPageButton.onClick.RemoveAllListeners();
            if (_refreshButton != null) _refreshButton.onClick.RemoveAllListeners();

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
            AcquireVisibilityGroups();

            if (_prevPageButton != null) _prevPageButton.onClick.AddListener(OnPrevPage);
            if (_nextPageButton != null) _nextPageButton.onClick.AddListener(OnNextPage);
            if (_refreshButton != null) _refreshButton.onClick.AddListener(OnRefreshClicked);

            // 빈 상태 안내 문구를 1회 세팅해 둔다(표시 여부는 RefreshAsync 에서 토글).
            if (_emptyStateText != null)
                _emptyStateText.text = $"아직 랭킹이 없습니다 ({RankingUseCase.MinGamesForRank}판 이상 필요)";

            PolishRuntimeLayout();

            _initialized = true;
        }

        /// <summary>
        /// 빈 상태 안내/목록/페이지네이션의 표시·숨김에 쓸 CanvasGroup 을 확보한다(없으면 추가).
        ///   - 목록(ScrollView)과 페이지네이션(PaginationRow)은 기존 참조에서 GameObject 를
        ///     역으로 찾아 CanvasGroup 을 얻는다(별도 슬롯을 추가하지 않기 위함).
        ///   - PaginationRow 는 이전 버튼의 부모(에디터 셋업 기준 직속 부모)로 판단한다.
        /// </summary>
        private void AcquireVisibilityGroups()
        {
            _emptyStateGroup = EnsureGroup(_emptyStateText);
            _listGroup = EnsureGroup(_scrollRect);

            if (_prevPageButton != null && _prevPageButton.transform.parent != null)
            {
                GameObject pagRow = _prevPageButton.transform.parent.gameObject;
                if (!pagRow.TryGetComponent(out _paginationGroup))
                    _paginationGroup = pagRow.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>지정 컴포넌트의 GameObject 에서 CanvasGroup 을 확보한다(없으면 추가). null 안전.</summary>
        private static CanvasGroup EnsureGroup(Component target)
        {
            if (target == null) return null;
            if (!target.TryGetComponent(out CanvasGroup group))
                group = target.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        private void PolishRuntimeLayout()
        {
            // 패널 자체의 제목은 위쪽에 두고 표와 같은 중앙 영역에 겹쳐 그리지 않는다.
            if (TryGetComponent(out TextMeshProUGUI title))
            {
                title.verticalAlignment = VerticalAlignmentOptions.Top;
                title.fontSize = 68f;
                title.color = Color.black;
                Vector4 margin = title.margin;
                margin.y = 64f;
                title.margin = margin;
            }

            Transform table = _headerRow != null ? _headerRow.parent : null;
            if (table != null)
            {
                RectTransform tableRt = table.GetComponent<RectTransform>();
                // 기존 표가 제목 높이까지 차지하던 배치는 아래처럼 제목 공간을 분리한다.
                // SetAnchors(tableRt, new Vector2(0.035f, 0.075f), new Vector2(0.965f, 0.985f));
                // SetAnchors(tableRt, new Vector2(0.035f, 0.075f), new Vector2(0.965f, 0.88f));
                // 열 개 행의 선호 높이에 맞춰 표를 접되, 제목 아래의 기존 위쪽 경계는 유지한다.
                if (tableRt != null)
                    tableRt.pivot = new Vector2(tableRt.pivot.x, 1f);
                SetAnchors(tableRt, new Vector2(0.035f, 0.88f), new Vector2(0.965f, 0.88f));
                if (!table.TryGetComponent(out ContentSizeFitter tableFitter))
                    tableFitter = table.gameObject.AddComponent<ContentSizeFitter>();
                tableFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                tableFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                if (table.TryGetComponent(out VerticalLayoutGroup tableLayout))
                {
                    tableLayout.spacing = 6f;
                    // ui_panel_dark 스프라이트의 좌우85/상하70 경계에 기존 간격6만큼 여유를 둔다.
                    tableLayout.padding = new RectOffset(91, 91, 76, 76);
                    tableLayout.childControlWidth = true;
                    tableLayout.childControlHeight = true;
                    tableLayout.childForceExpandWidth = true;
                    tableLayout.childForceExpandHeight = false;
                }
            }

            SetLayout(_headerRow != null ? _headerRow.gameObject : null, 58f, 0f);
            // SetLayout(_scrollRect != null ? _scrollRect.gameObject : null, 0f, 1f);
            // 빈 목록과 마지막 페이지도 열 개 슬롯 높이를 예약한다. 더 긴 행은 기존 스크롤로 탐색한다.
            float contentSpacing = _content != null && _content.TryGetComponent(out VerticalLayoutGroup contentLayout)
                ? contentLayout.spacing : 0f;
            float pageHeight = RowsPerPage * 96f + Mathf.Max(0, RowsPerPage - 1) * contentSpacing;
            SetLayout(_scrollRect != null ? _scrollRect.gameObject : null, pageHeight, 0f);
            SetLayout(_prevPageButton != null && _prevPageButton.transform.parent != null
                ? _prevPageButton.transform.parent.gameObject
                : null, 64f, 0f);

            if (_headerButtons != null)
            {
                foreach (Button header in _headerButtons)
                    StyleButton(header, 24);
            }
            NormalizeColumnWidths(_headerRow);
            foreach (RankRowView row in _rows)
            {
                if (row == null) continue;
                NormalizeColumnWidths(row.transform);
                if (row.TryGetComponent(out LayoutElement rowLayout))
                {
                    // 프리팹의 고정 60 높이 대신 세 줄을 담고, 더 긴 내용은 기존 LayoutGroup이 높이를 계산한다.
                    rowLayout.minHeight = 96f;
                    rowLayout.preferredHeight = -1f;
                    rowLayout.flexibleHeight = 0f;
                }
            }

            StyleButton(_prevPageButton, 24);
            StyleButton(_nextPageButton, 24);
            // 기존 은색 페이지 버튼 위의 문구 대비를 맞춘다.
            if (_prevPageButton != null)
                StyleText(_prevPageButton.GetComponentInChildren<TextMeshProUGUI>(true), 24, TextAlignmentOptions.Center, Color.black);
            if (_nextPageButton != null)
                StyleText(_nextPageButton.GetComponentInChildren<TextMeshProUGUI>(true), 24, TextAlignmentOptions.Center, Color.black);
            StyleButton(_refreshButton, 23);
            StyleText(_pageText, 25, TextAlignmentOptions.Center, Color.white);
            // 페이지 이동은 기존 화살표 Graphic으로 표시한다. 자식 문구만 숨겨 버튼 입력을 유지한다.
            ApplyGroupVisibility(EnsureGroup(_prevPageButton != null
                ? _prevPageButton.GetComponentInChildren<TextMeshProUGUI>(true) : null), false);
            ApplyGroupVisibility(EnsureGroup(_nextPageButton != null
                ? _nextPageButton.GetComponentInChildren<TextMeshProUGUI>(true) : null), false);
            if (_prevPageButton != null && _prevPageButton.transform.parent != null &&
                _prevPageButton.transform.parent.TryGetComponent(out HorizontalLayoutGroup pagination))
            {
                pagination.spacing = 12f;
                pagination.childControlWidth = true;
                pagination.childForceExpandWidth = false;
                // 글자의 선호폭 대신 부모의 남은 폭을 이전/페이지/다음/새로고침 비율로 나눈다.
                Component[] items = { _prevPageButton, _pageText, _nextPageButton, _refreshButton };
                float[] weights = { 1f, 3f, 1f, 2f };
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] == null) continue;
                    if (!items[i].TryGetComponent(out LayoutElement column))
                        column = items[i].gameObject.AddComponent<LayoutElement>();
                    column.minWidth = column.preferredWidth = 0f;
                    column.flexibleWidth = weights[i];
                }
            }
            StyleText(_emptyStateText, 26, TextAlignmentOptions.Center, Color.white);
            // 빈 안내는 LayoutGroup 밖의 오버레이이므로 헤더/페이지 버튼을 제외한 본문에만 둔다.
            if (_emptyStateText != null)
                SetAnchors(_emptyStateText.rectTransform, new Vector2(0.035f, 0.15f), new Vector2(0.965f, 0.84f));
        }

        private static void NormalizeColumnWidths(Transform row)
        {
            if (row == null) return;
            // 씬/행 프리팹의 열 비중은 유지하고, 긴 실제 값의 선호폭이 열 경계를 밀지 않게 한다.
            for (int i = 0; i < row.childCount; i++)
            {
                if (!row.GetChild(i).TryGetComponent(out LayoutElement column)) continue;
                column.minWidth = 0f;
                column.preferredWidth = 0f;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("개발 빌드 표시 전용")]
        [Tooltip("임시 랭킹 25개를 3페이지로 표시한다. 실제 랭킹 조회/캐시에 넣지 않으며 Release에서는 실행되지 않는다.")]
        [SerializeField] private bool _developmentLayoutPreview = true;
        // 실제 랭킹 캐시와 분리된 표시 전용 미리보기. 저장/서비스/정렬 입력에 사용하지 않는다.
        private readonly Dictionary<Button, bool> _previewButtonStates = new Dictionary<Button, bool>();
        private bool _editorLayoutPreview;
        private readonly List<LeaderboardEntry> _previewEntries = new List<LeaderboardEntry>();
        private int _previewOriginalPage;
        private string _previewOriginalTitle;

#if UNITY_EDITOR
        [ContextMenu("UI 레이아웃/임시 랭킹 25개 페이지 미리보기")]
        private void PreviewEditorRankingLayout()
        {
            // 조회 완료 후 Play를 일시정지한 상태에서만 명시적으로 사용한다.
            if (!UnityEngine.Application.isPlaying || !UnityEditor.EditorApplication.isPaused || !_initialized)
                return;
            ApplyLayoutPreview();
        }
#endif

        private void ApplyLayoutPreview()
        {
            if (!_editorLayoutPreview)
            {
                if (TryGetComponent(out TextMeshProUGUI originalTitle))
                    _previewOriginalTitle = originalTitle.text;
                _previewButtonStates.Clear();
                _previewOriginalPage = _currentPage;
                RememberPreviewButton(_refreshButton);
                if (_headerButtons != null)
                    foreach (Button button in _headerButtons) RememberPreviewButton(button);
            }
            _editorLayoutPreview = true;
            if (TryGetComponent(out TextMeshProUGUI title)) title.text = "랭킹 (임시 미리보기)";
            _previewEntries.Clear();
            for (int i = 0; i < 25; i++)
            {
                _previewEntries.Add(new LeaderboardEntry
                {
                    Rank = i + 1,
                    Nickname = i % 2 == 0 ? "가나다라마바사아자차카타" : "ABCDEFGHIJKLMNOPQRSTUVWX",
                    NicknameCode = "9999",
                    WinRate = i == 0 ? 100f : (i == 1 ? 0f : 99.9f),
                    TotalGames = 123456, Wins = 123455, Losses = 1
                });
            }
            SetEmptyStateVisible(false);
            foreach (var state in _previewButtonStates)
                if (state.Key != null) state.Key.interactable = false;
            _currentPage = 0;
            RenderCurrentPage();
            Canvas.ForceUpdateCanvases();
        }

        private void RememberPreviewButton(Button button)
        {
            if (button != null) _previewButtonStates[button] = button.interactable;
        }

#if UNITY_EDITOR
        [ContextMenu("UI 레이아웃/실제 랭킹 표시 복원")]
        private void RestoreEditorRankingLayout()
        {
            if (!UnityEngine.Application.isPlaying || !_editorLayoutPreview) return;
            _editorLayoutPreview = false;
            _developmentLayoutPreview = false;
            _previewEntries.Clear();
            if (TryGetComponent(out TextMeshProUGUI title)) title.text = _previewOriginalTitle;
            SetEmptyStateVisible(_entries.Count == 0);
            foreach (var state in _previewButtonStates)
                if (state.Key != null) state.Key.interactable = state.Value;
            _previewButtonStates.Clear();
            _currentPage = Mathf.Clamp(_previewOriginalPage, 0, PageCount - 1);
            RenderCurrentPage();
            Canvas.ForceUpdateCanvases();
        }
#endif
#endif

        private static void SetLayout(GameObject go, float preferredHeight, float flexibleHeight)
        {
            if (go == null)
                return;

            if (!go.TryGetComponent(out LayoutElement layout))
                layout = go.AddComponent<LayoutElement>();

            layout.preferredHeight = preferredHeight;
            layout.flexibleHeight = flexibleHeight;
            layout.flexibleWidth = 1f;
        }

        private static void StyleButton(Button button, int fontSize)
        {
            if (button == null)
                return;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                StyleText(label, fontSize, TextAlignmentOptions.Center, Color.white);
        }

        private static void StyleText(TMP_Text text, int fontSize, TextAlignmentOptions alignment, Color color)
        {
            if (text == null)
                return;

            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null)
                return;

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 행 프리팹을 Content 아래에 RowsPerPage 개 생성해 풀로 보관한다.
        /// </summary>
        private void BuildRowPool()
        {
            if (_rowPrefab == null || _content == null)
            {
                // [개발] Warn + 개발 — Inspector 배선 누락(1.3 원칙 3 단서).
                //   ⚠️ 이 파일에서 Cloud(랭킹 조회) 계층과 중복되는 로그는 없다. 조회 실패는
                //      LeaderboardService 가 LeaderboardQueryFailed / LeaderboardMetadataParseFailed 로
                //      이미 운영 기록하며, 이 자리는 그 흐름과 무관한 UI 배선 문제다.
                GameLog.Dev.Warn("UI", nameof(RankingView),
                                 "행 프리팹/컨테이너 미배선 — 랭킹 행 풀을 만들 수 없다",
                                 "Field=_rowPrefab|_content");
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
        /// 로딩 중에는 전역 로딩 인디케이터를 표시한다.
        /// </summary>
        public async System.Threading.Tasks.Task RefreshAsync()
        {
            InitializeIfNeeded();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 임시 목록은 같은 페이지 표시 경로만 사용하고 실제 entries/서비스 캐시는 건드리지 않는다.
            if (_developmentLayoutPreview || _editorLayoutPreview)
            {
                ApplyLayoutPreview();
                return;
            }
#endif
            if (_rankingUseCase == null)
                return;

            UIManager.Instance?.ShowLoading(true, "랭킹 불러오는 중...");

            try
            {
                List<LeaderboardEntry> loaded = await _rankingUseCase.GetRankingsAsync();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // 조회 중 개발 표시로 전환한 경우 늦은 실제 응답이 임시 화면을 덮지 않는다.
                if (_developmentLayoutPreview || _editorLayoutPreview) return;
#endif

                _entries.Clear();
                if (loaded != null)
                    _entries.AddRange(loaded);

                // 기본 정렬(승률 내림차순) 적용 후 첫 페이지 표시.
                _sortColumn = SortColumn.WinRate;
                _sortAscending = false;
                ApplySort();
                UpdateHeaderLabels();

                _currentPage = 0;
                RenderCurrentPage();

                // 등재 인원이 0명이면 빈 상태 안내를 표시하고 목록/페이지네이션을 숨긴다.
                SetEmptyStateVisible(_entries.Count == 0);
            }
            finally
            {
                UIManager.Instance?.ShowLoading(false);
            }
        }

        /// <summary>
        /// 새로고침 버튼 클릭 → 랭킹을 다시 로드한다(RefreshAsync 재사용, 확정 결정 4).
        /// RefreshAsync 내부에서 로딩 인디케이터를 표시하므로 별도 처리는 필요 없다.
        /// </summary>
        private void OnRefreshClicked()
        {
            _ = RefreshAsync();
        }

        /// <summary>
        /// 빈 상태 안내/목록/페이지네이션의 표시를 토글한다(공통 UI 규칙 5, CanvasGroup 기반).
        /// </summary>
        /// <param name="empty">true면 빈 상태 안내 표시(+목록/페이지 숨김), false면 반대.</param>
        private void SetEmptyStateVisible(bool empty)
        {
            ApplyGroupVisibility(_emptyStateGroup, empty);
            ApplyGroupVisibility(_listGroup, !empty);
            ApplyGroupVisibility(_paginationGroup, !empty);
        }

        /// <summary>CanvasGroup 표시/숨김을 alpha/blocksRaycasts/interactable 로 적용한다(null 안전).</summary>
        private static void ApplyGroupVisibility(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 미리보기에서 실제 목록 정렬이 변경되지 않도록 버튼 외 직접 호출도 차단한다.
            if (_editorLayoutPreview) return;
#endif
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
            UpdateHeaderLabels();
            _currentPage = 0;
            RenderCurrentPage();
        }

        /// <summary>
        /// 현재 정렬 기준/방향을 헤더 라벨에 반영한다.
        ///   - 활성 정렬 열에는 "라벨 ▲/▼"(오름차순 ▲, 내림차순 ▼)로 표시.
        ///   - 나머지 열은 원본 라벨로 되돌린다.
        /// 열 인덱스(좌→우): 0 순위(고정) / 1 닉네임 / 2 승률 / 3 게임수 / 4 승 / 5 패.
        /// </summary>
        private void UpdateHeaderLabels()
        {
            if (_headerButtons == null) return;

            int activeIndex = SortColumnToHeaderIndex(_sortColumn);
            string arrow = _sortAscending ? "▲" : "▼";

            for (int i = 0; i < _headerButtons.Length; i++)
            {
                if (_headerButtons[i] == null) continue;

                // 버튼 하위 라벨 텍스트(비활성 포함)를 찾아 갱신한다.
                TextMeshProUGUI label = _headerButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (label == null) continue;

                string baseLabel = i < _columnLabels.Length ? _columnLabels[i] : label.text;
                label.text = (i == activeIndex) ? $"{baseLabel} {arrow}" : baseLabel;
            }
        }

        /// <summary>정렬 기준 열 → 헤더 버튼 인덱스 매핑(순위 열은 정렬 대상 아님 → -1).</summary>
        private static int SortColumnToHeaderIndex(SortColumn column)
        {
            return column switch
            {
                SortColumn.Nickname => 1,
                SortColumn.WinRate => 2,
                SortColumn.Games => 3,
                SortColumn.Wins => 4,
                SortColumn.Losses => 5,
                _ => -1
            };
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
                int pages = (ActiveEntries.Count + RowsPerPage - 1) / RowsPerPage;
                return Mathf.Max(1, pages);
            }
        }

        /// <summary>
        /// 현재 페이지 데이터를 행 풀에 바인딩하고, 페이지 텍스트/버튼 상태를 갱신한다.
        /// </summary>
        private void RenderCurrentPage()
        {
            IReadOnlyList<LeaderboardEntry> entries = ActiveEntries;
            _currentPage = Mathf.Clamp(_currentPage, 0, PageCount - 1);
            int startIndex = _currentPage * RowsPerPage;

            for (int i = 0; i < _rows.Count; i++)
            {
                int dataIndex = startIndex + i;
                if (dataIndex < entries.Count)
                    _rows[i].Bind(entries[dataIndex], i); // i = 페이지 내 행 슬롯 인덱스(짝수행 얼룩용)
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
