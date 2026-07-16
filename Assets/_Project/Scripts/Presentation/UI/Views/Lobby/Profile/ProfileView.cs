// ============================================================================
// ProfileView.cs
// 로비 씬 Profile 탭. 계정 연동 + 로그아웃 UI.
//
// 역할:
//   - 현재 계정 상태(익명 / Google / 이메일) 에 따라 다른 UI 표시
//   - 익명 상태: 계정 연동(Google / 이메일) 버튼 + 로그아웃 버튼
//   - 실계정 상태: 표시 이름 / 이메일 + 로그아웃 버튼 (연동 버튼 숨김)
//   - 연동 성공 시 UI 자동 갱신
//   - 로그아웃 시 Firebase + UGS 세션 종료 후 Login 씬으로 이동
//
// AuthSystemRules.md 규칙:
//   - 익명 계정에만 연동 버튼 표시.
//   - 연동 충돌(이미 다른 계정에 연결) 시 안내 팝업 표시 후 익명 상태 유지.
//   - 로그아웃 후 Login 씬 이동.
//
// 의존성 처리:
//   본 Lobby 씬은 GameBootstrapper 가 Composition Root 이지만, ProfileView 는
//   Firebase 의존이므로 FirebaseAuthService 인스턴스가 별도로 필요하다.
//   본 View 가 자체적으로 FirebaseAuthService 를 생성하여 사용한다 — Login 씬에서 이미
//   초기화가 끝난 상태이므로 InitializeAsync() 는 Firebase 의존성 체크만 수행하고 즉시 반환.
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 프로필 탭 View. 계정 연동 + 로그아웃.
    /// </summary>
    public class ProfileView : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("계정 정보 표시")]
        [Tooltip("계정 정보(이름/이메일 또는 '익명 사용자') 텍스트.")]
        [SerializeField] private TextMeshProUGUI _accountInfoText;

        [Header("닉네임/코드")]
        [Tooltip("닉네임#코드 표시 텍스트(예: 전사#4729).")]
        [SerializeField] private TextMeshProUGUI _nicknameText;

        [Tooltip("닉네임 변경 버튼. 실계정에서만 표시(익명 계정은 숨김).")]
        [SerializeField] private Button _changeNicknameButton;

        [Header("전적")]
        [Tooltip("총 게임 수 텍스트.")]
        [SerializeField] private TextMeshProUGUI _totalGamesText;

        [Tooltip("승리 횟수 텍스트.")]
        [SerializeField] private TextMeshProUGUI _winsText;

        [Tooltip("패배 횟수 텍스트.")]
        [SerializeField] private TextMeshProUGUI _lossesText;

        [Tooltip("승률 텍스트(총게임수 0이면 '-').")]
        [SerializeField] private TextMeshProUGUI _winRateText;

        [Tooltip("마지막 접속 종료 날짜/시각 텍스트(예: 2026-06-29 15:30).")]
        [SerializeField] private TextMeshProUGUI _lastSessionText;

        [Tooltip("전적/내 랭킹을 수동으로 다시 불러오는 새로고침 버튼(확정 결정 2).")]
        [SerializeField] private Button _refreshButton;

        [Header("닉네임 변경 모달")]
        [Tooltip("닉네임 변경 모달 팝업(확정 결정 3). 같은 Lobby 씬에 배치되며, 본 View 가 참조로 보유한다.")]
        [SerializeField] private NicknameChangePopup _nicknameChangePopup;

        [Header("내 랭킹")]
        [Tooltip("내 순위 텍스트('N위' 또는 '순위 없음').")]
        [SerializeField] private TextMeshProUGUI _myRankText;

        [Header("연동 버튼 (익명 계정 전용)")]
        [Tooltip("익명 계정 전용 영역 — Google/이메일 연동 버튼을 묶은 부모 GameObject.")]
        [SerializeField] private GameObject _anonymousSection;

        [Tooltip("Google 로 연동 버튼.")]
        [SerializeField] private Button _linkGoogleButton;

        [Tooltip("이메일로 연동 버튼.")]
        [SerializeField] private Button _linkEmailButton;

        [Header("이메일 연동 입력 (선택)")]
        [Tooltip("이메일 연동 시 새 이메일 입력 필드. 비워두면 _linkEmailButton 은 단순히 안내만 표시.")]
        [SerializeField] private TMP_InputField _linkEmailInput;

        [Tooltip("이메일 연동 시 비밀번호 입력 필드.")]
        [SerializeField] private TMP_InputField _linkPasswordInput;

        [Header("공통 버튼")]
        [Tooltip("로그아웃 버튼.")]
        [SerializeField] private Button _logoutButton;

        [Header("상태 표시")]
        [Tooltip("연동/로그아웃 결과 메시지.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("씬 전환")]
        [Tooltip("로그아웃 후 이동할 씬 이름.")]
        [SerializeField] private string _loginSceneName = "Login";

        // ====================================================================
        // 런타임 의존성
        // ====================================================================

        private FirebaseAuthService _authService;
        private LoginUseCase _loginUseCase;
        private AccountLinkUseCase _accountLinkUseCase;

        // 프로필(닉네임/전적) 조회 UseCase. 구현체는 Infrastructure(UGS Cloud Save) —
        //   Presentation 은 Infrastructure 보다 바깥 레이어이므로 참조/생성 허용.
        private PlayerProfileUseCase _profileUseCase;

        // 내 순위 조회 UseCase. 구현체는 Infrastructure(UGS Leaderboards).
        private RankingUseCase _rankingUseCase;

        // 닉네임 변경 버튼의 표시/숨김을 CanvasGroup 으로 처리하기 위한 캐시(공통 UI 규칙 5).
        private CanvasGroup _changeNicknameButtonGroup;

        // 익명 전용 영역(_anonymousSection)에 부착된 CanvasGroup 캐시.
        // 공통 UI 규칙 Rule 5: SetActive 대신 CanvasGroup으로 표시/숨김 처리.
        // _anonymousSection은 다른 UI(계정 정보 텍스트/로그아웃 버튼 등)와 같은 부모
        // LayoutGroup 안에 배치되는 경우가 많아, SetActive로 숨기면 다른 요소가 위로
        // 밀려올라가는 레이아웃 흔들림이 생긴다. CanvasGroup.alpha=0으로 공간 유지.
        private CanvasGroup _anonymousSectionGroup;
        private GameObject _profileContentPanel;
        private bool _runtimeLayoutPolished;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        /// <summary>
        /// 본 View 는 Lobby 씬에 직접 배치되며, Login 씬에서 이미 Firebase 가 초기화된 상태다.
        /// 따라서 FirebaseAuthService 를 새로 생성하고 InitializeAsync() 만 다시 한 번 호출하여
        /// 의존성 체크를 통과시킨다(멱등성 — 이미 초기화돼 있으면 즉시 반환).
        /// </summary>
        private async void Start()
        {
            EnsureRuntimeLayoutPolished();

            // 익명 영역의 CanvasGroup을 확보(없으면 추가).
            // Inspector에서 연결한 _anonymousSection이 null이면 그룹도 null로 유지.
            if (_anonymousSection != null)
            {
                if (!_anonymousSection.TryGetComponent(out _anonymousSectionGroup))
                    _anonymousSectionGroup = _anonymousSection.AddComponent<CanvasGroup>();
            }

            _authService = new FirebaseAuthService();
            bool ready = await _authService.InitializeAsync();
            if (!ready)
            {
                SetStatus("인증 서비스 초기화에 실패했습니다.");
                return;
            }

            _loginUseCase = new LoginUseCase(_authService);
            _accountLinkUseCase = new AccountLinkUseCase(_authService);

            // 프로필/랭킹 UseCase 생성(구현체는 Infrastructure — Presentation 에서 생성 허용).
            IPlayerProfileService profileService = new PlayerProfileService();
            _profileUseCase = new PlayerProfileUseCase(profileService);

            ILeaderboardService leaderboardService = new LeaderboardService();
            _rankingUseCase = new RankingUseCase(leaderboardService);

            // 닉네임 변경 버튼의 CanvasGroup 확보(없으면 추가) — 표시/숨김에 사용.
            if (_changeNicknameButton != null &&
                !_changeNicknameButton.TryGetComponent(out _changeNicknameButtonGroup))
            {
                _changeNicknameButtonGroup = _changeNicknameButton.gameObject.AddComponent<CanvasGroup>();
            }

            // 버튼 리스너 등록
            if (_linkGoogleButton != null) _linkGoogleButton.onClick.AddListener(OnLinkGoogleClicked);
            if (_linkEmailButton != null) _linkEmailButton.onClick.AddListener(OnLinkEmailClicked);
            if (_logoutButton != null) _logoutButton.onClick.AddListener(OnLogoutClicked);
            if (_changeNicknameButton != null) _changeNicknameButton.onClick.AddListener(OnChangeNicknameClicked);
            if (_refreshButton != null) _refreshButton.onClick.AddListener(OnRefreshClicked);

            // 닉네임 변경 모달 초기화 — 이미 생성해 둔 _profileUseCase 를 그대로 주입한다.
            //   (Presentation 은 Infrastructure 바깥 레이어이므로 UseCase 공유에 문제없다.)
            //   변경 성공 시 콜백으로 프로필 화면을 다시 갱신한다(확정 결정 3).
            if (_nicknameChangePopup != null)
                _nicknameChangePopup.Initialize(_profileUseCase, () => { _ = RefreshProfileDataAsync(); });

            // 최초 UI 갱신(계정 상태 + Cloud Save 프로필/랭킹)
            RefreshUI();
            _ = RefreshProfileDataAsync();
        }

        private void OnDestroy()
        {
            if (_linkGoogleButton != null) _linkGoogleButton.onClick.RemoveAllListeners();
            if (_linkEmailButton != null) _linkEmailButton.onClick.RemoveAllListeners();
            if (_logoutButton != null) _logoutButton.onClick.RemoveAllListeners();
            if (_changeNicknameButton != null) _changeNicknameButton.onClick.RemoveAllListeners();
            if (_refreshButton != null) _refreshButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// 탭이 활성화될 때마다 UI 를 갱신한다 — Profile 탭으로 돌아왔을 때 최신 상태 반영.
        /// </summary>
        private void OnEnable()
        {
            EnsureRuntimeLayoutPolished();

            // _authService 초기화 전에 OnEnable 이 먼저 호출될 수 있으므로 null 가드.
            if (_authService != null && _authService.IsInitialized)
            {
                RefreshUI();
                _ = RefreshProfileDataAsync();
            }
        }

        /// <summary>
        /// LobbyRootView가 Profile 탭을 표시할 때 호출한다.
        /// 탭 전환 시점에도 런타임 레이아웃 보정이 반드시 적용되도록 한다.
        /// </summary>
        public void OnProfileTabShown()
        {
            EnsureRuntimeLayoutPolished();
            if (_authService != null && _authService.IsInitialized)
            {
                RefreshUI();
                _ = RefreshProfileDataAsync();
            }
        }

        // ====================================================================
        // UI 갱신
        // ====================================================================

        /// <summary>
        /// 현재 계정 상태에 따라 정보 텍스트와 연동 버튼 표시 여부를 갱신한다.
        /// </summary>
        private void RefreshUI()
        {
            ClearStatus();

            if (_authService == null || !_authService.IsLoggedIn)
            {
                if (_accountInfoText != null)
                    _accountInfoText.text = "로그인 정보가 없습니다.";
                SetAnonymousSectionVisible(false);
                // 로그인 정보가 없으면 닉네임 변경 버튼도 숨긴다.
                SetChangeNicknameVisible(false);
                return;
            }

            if (_authService.IsAnonymous)
            {
                if (_accountInfoText != null)
                {
                    _accountInfoText.text =
                        "현재 익명으로 로그인 중입니다.\n" +
                        "계정을 연동하면 기기 변경 시에도 데이터를 유지할 수 있습니다.";
                }
                SetAnonymousSectionVisible(true);
                // 익명 계정에는 닉네임 변경 버튼을 표시하지 않는다(UI 규칙 3).
                SetChangeNicknameVisible(false);
            }
            else
            {
                // 실계정 → 닉네임 변경 버튼 표시.
                SetChangeNicknameVisible(true);
                // 실계정(Google 또는 이메일).
                //   Google 로그인은 DisplayName 이 채워지고, 이메일 로그인은 Email 만 채워지는 경향.
                string label = !string.IsNullOrWhiteSpace(_authService.DisplayName)
                    ? _authService.DisplayName
                    : _authService.Email;

                if (_accountInfoText != null)
                    _accountInfoText.text = string.IsNullOrWhiteSpace(label) ? "로그인됨" : label;

                SetAnonymousSectionVisible(false);
            }
        }

        // ====================================================================
        // 닉네임 / 전적 / 내 랭킹 (Cloud Save + Leaderboard)
        // ====================================================================

        /// <summary>
        /// Cloud Save 프로필(닉네임/전적)과 Leaderboard 내 순위를 로드해 각 텍스트에 바인딩한다.
        /// 탭이 활성화될 때마다 호출되어 최신 데이터를 반영한다(UI 규칙 6).
        /// </summary>
        private async System.Threading.Tasks.Task RefreshProfileDataAsync()
        {
            if (_profileUseCase == null)
                return;

            PlayerProfileData profile = await _profileUseCase.LoadProfileAsync();
            if (profile == null)
                return;

            // 닉네임#코드 (없으면 '게스트' 표기 — 익명/미설정 계정 대비).
            if (_nicknameText != null)
                _nicknameText.text = profile.HasNickname ? profile.DisplayName : "게스트";

            // 전적.
            if (_totalGamesText != null) _totalGamesText.text = profile.TotalGames.ToString();
            if (_winsText != null) _winsText.text = profile.Wins.ToString();
            if (_lossesText != null) _lossesText.text = profile.Losses.ToString();

            // 승률: 총게임수 0이면 '-' (UI 규칙 4).
            if (_winRateText != null)
                _winRateText.text = profile.TotalGames > 0 ? $"{profile.WinRate:F1}%" : "-";

            // 마지막 접속 종료 날짜/시각.
            if (_lastSessionText != null)
                _lastSessionText.text = FormatLastSession(profile.LastSessionEndAt);

            // 내 랭킹.
            await RefreshMyRankAsync(profile.TotalGames);
        }

        /// <summary>
        /// 내 순위를 조회해 텍스트에 바인딩한다.
        ///   총게임수 20 미만: "랭킹: 순위 없음 (20판 이상 필요)"
        ///   20 이상 & 순위 있음: "랭킹: N위"
        ///   20 이상 & 순위 없음: "랭킹: 순위 없음"
        /// (UI 규칙 5)
        /// </summary>
        private async System.Threading.Tasks.Task RefreshMyRankAsync(int totalGames)
        {
            if (_myRankText == null)
                return;

            if (totalGames < RankingUseCase.MinGamesForRank)
            {
                _myRankText.text = $"랭킹: 순위 없음 ({RankingUseCase.MinGamesForRank}판 이상 필요)";
                return;
            }

            int rank = _rankingUseCase != null ? await _rankingUseCase.GetMyRankAsync() : -1;
            _myRankText.text = rank > 0 ? $"랭킹: {rank}위" : "랭킹: 순위 없음";
        }

        /// <summary>
        /// ISO 문자열을 "yyyy-MM-dd HH:mm" 로 변환한다. 파싱 실패/빈 값이면 "-".
        /// </summary>
        private static string FormatLastSession(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso))
                return "-";

            if (System.DateTime.TryParse(iso, out System.DateTime dt))
                return dt.ToString("yyyy-MM-dd HH:mm");

            return "-";
        }

        /// <summary>
        /// 닉네임 변경 버튼 클릭 → 닉네임 변경 모달을 연다(확정 결정 3).
        /// 무료 사용 여부(hasUsedFreeNicknameChange)와 현재 닉네임을 먼저 조회해 모달에 전달하면,
        /// 모달이 무료(입력) UI 와 유료(안내) UI 중 무엇을 보여줄지 결정한다.
        /// </summary>
        private async void OnChangeNicknameClicked()
        {
            if (_profileUseCase == null)
                return;

            PlayerProfileData profile = await _profileUseCase.LoadProfileAsync();
            bool usedFree = profile != null && profile.HasUsedFreeNicknameChange;
            string currentNickname = profile != null && profile.HasNickname ? profile.Nickname : string.Empty;

            if (_nicknameChangePopup != null)
            {
                _nicknameChangePopup.Show(usedFree, currentNickname);
            }
            else
            {
                // 모달이 연결되지 않은 예외적 상황(씬 미배선 등) — 안내만 표시.
                SetStatus("닉네임 변경 창을 열 수 없습니다. (설정 필요)");
            }

            // === [구 로직 — 비활성화] 모달 없이 상태 텍스트로만 안내하던 방식 ===
            //   확정 결정 3 에 따라 실제 입력/검증/저장이 가능한 모달로 대체되었다.
            //   실기 통과 후 아래 주석 블록은 최종 삭제 예정(WORKFLOW 기존 로직 제거 규칙).
            // SetStatus(usedFree
            //     ? "닉네임 변경은 인앱 결제로 가능합니다. (준비 중)"
            //     : "최초 1회 닉네임 변경은 무료입니다. (준비 중)");
        }

        /// <summary>
        /// 새로고침 버튼 클릭 → 전적/내 랭킹을 다시 불러온다(확정 결정 2).
        /// 기존 RefreshProfileDataAsync() 를 재사용하며, 갱신 동안 전역 로딩 인디케이터를 표시한다.
        /// (UIManager 가 null 일 수 있는 상황 대비 ?. null-safe 패턴 — 규칙 L-4.)
        /// </summary>
        private async void OnRefreshClicked()
        {
            UIManager.Instance?.ShowLoading(true, "갱신 중...");
            try
            {
                await RefreshProfileDataAsync();
            }
            finally
            {
                UIManager.Instance?.ShowLoading(false);
            }
        }

        /// <summary>
        /// 닉네임 변경 버튼 표시/숨김(CanvasGroup 기반, 공통 UI 규칙 5).
        /// </summary>
        private void SetChangeNicknameVisible(bool visible)
        {
            if (_changeNicknameButtonGroup == null)
                return;

            _changeNicknameButtonGroup.alpha = visible ? 1f : 0f;
            _changeNicknameButtonGroup.blocksRaycasts = visible;
            _changeNicknameButtonGroup.interactable = visible;
        }

        // ====================================================================
        // Google 연동
        // ====================================================================

        /// <summary>
        /// Google 연동 버튼 클릭 → AccountLinkUseCase.LinkWithGoogleAsync().
        /// </summary>
        private async void OnLinkGoogleClicked()
        {
            ClearStatus();
            SetInteractable(false);

            try
            {
                LinkResult result = await _accountLinkUseCase.LinkWithGoogleAsync();

                switch (result)
                {
                    case LinkResult.Success:
                        SetStatus("Google 계정 연동에 성공했습니다.");
                        RefreshUI();
                        return;

                    case LinkResult.AlreadyLinkedElsewhere:
                        ShowAlert("이미 다른 계정에 연동된 정보입니다.");
                        return;

                    case LinkResult.Failed:
                        var err = _accountLinkUseCase.LastError;
                        SetStatus(err != null ? err.Message : "연동에 실패했습니다.");
                        return;
                }
            }
            finally
            {
                SetInteractable(true);
            }
        }

        // ====================================================================
        // 이메일 연동
        // ====================================================================

        /// <summary>
        /// 이메일 연동 버튼 클릭 → 이메일/비밀번호 입력 검증 후 AccountLinkUseCase.LinkWithEmailAsync().
        /// </summary>
        private async void OnLinkEmailClicked()
        {
            ClearStatus();

            string email = _linkEmailInput != null ? _linkEmailInput.text.Trim() : string.Empty;
            string password = _linkPasswordInput != null ? _linkPasswordInput.text : string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                SetStatus("이메일과 비밀번호를 모두 입력하세요.");
                return;
            }
            if (password.Length < 6)
            {
                SetStatus("비밀번호는 6자 이상이어야 합니다.");
                return;
            }

            SetInteractable(false);

            try
            {
                LinkResult result = await _accountLinkUseCase.LinkWithEmailAsync(email, password);

                switch (result)
                {
                    case LinkResult.Success:
                        SetStatus("이메일 계정 연동에 성공했습니다. 인증 메일을 확인하세요.");
                        RefreshUI();
                        return;

                    case LinkResult.AlreadyLinkedElsewhere:
                        ShowAlert("이미 사용 중인 이메일입니다.");
                        return;

                    case LinkResult.Failed:
                        var err = _accountLinkUseCase.LastError;
                        SetStatus(err != null ? err.Message : "연동에 실패했습니다.");
                        return;
                }
            }
            finally
            {
                SetInteractable(true);
            }
        }

        // ====================================================================
        // 로그아웃
        // ====================================================================

        /// <summary>
        /// 로그아웃 버튼 클릭 → Firebase + UGS 세션 종료 후 Login 씬 이동.
        /// 익명 계정 로그아웃은 복구 불가하므로 일반적으로 연동 권유가 필요하지만,
        /// AuthSystemRules.md 규칙 3 에 따라 강제하지는 않는다 — 본 View 는 즉시 진행.
        /// </summary>
        private async void OnLogoutClicked()
        {
            SetInteractable(false);

            // 로그아웃은 Firebase/UGS 세션 종료(비동기)가 진행되는 동안 사용자가 멈춘 화면을
            // 보지 않도록, 세션 종료 시작 시점부터 전역 로딩 인디케이터를 띄운다.
            // (비동기 대기가 있어 SceneLoader.Load 보다 먼저 로딩을 표시해야 한다.)
            // 로딩을 끄는 책임은 목적지 씬(Login)의 LoginBootstrapper가 담당한다(UI 규칙 L-3).
            // UIManager가 null일 수 있는 상황(씬 직접 진입 등)을 대비해 ?. 로 안전 처리한다(규칙 L-4).
            UIManager.Instance?.ShowLoading(true, "로그아웃 중...");

            try
            {
                await _loginUseCase.SignOutAsync();
                // SceneLoader.Load 가 내부에서 ShowLoading(true)를 다시 호출하므로 메시지가 갱신된다.
                SceneLoader.Load(_loginSceneName, "로그아웃 중...");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProfileView] 로그아웃 중 예외: {e.Message}");
                SetStatus("로그아웃 중 오류가 발생했습니다.");
                SetInteractable(true);
                // 예외로 씬 전환이 일어나지 않으므로 여기서 로딩을 직접 끈다(UI 규칙 L-3 예외 경로).
                UIManager.Instance?.ShowLoading(false);
            }
        }

        // ====================================================================
        // UI 헬퍼
        // ====================================================================

        private void SetInteractable(bool on)
        {
            if (_linkGoogleButton != null) _linkGoogleButton.interactable = on;
            if (_linkEmailButton != null) _linkEmailButton.interactable = on;
            if (_logoutButton != null) _logoutButton.interactable = on;
        }

        private void SetStatus(string text)
        {
            if (_statusText != null) _statusText.text = text ?? string.Empty;
        }

        private void ClearStatus() => SetStatus(string.Empty);

        /// <summary>
        /// 알림 팝업 표시. UIManager가 없으면 statusText 로 대체.
        /// </summary>
        private void ShowAlert(string message)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowConfirm(
                    message: message,
                    onConfirm: null,
                    onCancel: null,
                    confirmLabel: "확인",
                    cancelLabel: string.Empty);
            }
            else
            {
                // UIManager 미초기화 상태(씬 직접 진입 등) — statusText로 대체.
                SetStatus(message);
            }
        }

        /// <summary>
        /// 익명 전용 영역 표시/숨김 처리 (CanvasGroup 기반).
        /// 공통 UI 규칙 Rule 5: SetActive 대신 CanvasGroup으로 처리해
        /// LayoutGroup 안에서 공간이 사라지는 레이아웃 깨짐을 방지한다.
        /// 익명 영역이 보이지 않을 때도 공간이 유지되어, 주변 UI(로그아웃 버튼 등)가
        /// 위로 밀려올라오는 흔들림 없이 항상 같은 위치에 표시된다.
        /// </summary>
        /// <param name="visible">true=표시, false=숨김.</param>
        private void SetAnonymousSectionVisible(bool visible)
        {
            if (_anonymousSectionGroup == null)
                return;

            _anonymousSectionGroup.alpha = visible ? 1f : 0f;
            _anonymousSectionGroup.blocksRaycasts = visible;
            _anonymousSectionGroup.interactable = visible;
        }

        // ====================================================================
        // 런타임 UI 레이어 보정
        // ====================================================================

        /// <summary>
        /// 에디터 1회성 생성 UI가 기존 프로필 UI와 겹쳐 보이지 않도록,
        /// 런타임에 참조된 요소들을 하나의 패널 아래로 모은다.
        /// 씬 YAML 직접 수정 없이 직렬화 슬롯을 유지하는 안전한 보정 경로다.
        /// </summary>
        private void EnsureRuntimeLayoutPolished()
        {
            if (_runtimeLayoutPolished)
                return;

            PolishRuntimeLayout();
            _runtimeLayoutPolished = true;
        }

        private void PolishRuntimeLayout()
        {
            _profileContentPanel = FindOrCreateChild(transform, "ProfileContentPanel");
            _profileContentPanel.transform.SetAsLastSibling();

            RectTransform panelRt = _profileContentPanel.GetComponent<RectTransform>();
            SetAnchors(panelRt, new Vector2(0.055f, 0.245f), new Vector2(0.945f, 0.875f));

            Image panelImage = EnsureImage(_profileContentPanel);
            panelImage.color = new Color(0.08f, 0.07f, 0.06f, 0.62f);
            panelImage.raycastTarget = true;

            CanvasGroup panelGroup = EnsureCanvasGroup(_profileContentPanel);
            SetGroupVisible(panelGroup, true);

            VerticalLayoutGroup layout = EnsureVerticalLayout(_profileContentPanel);
            layout.padding = new RectOffset(34, 34, 36, 30);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Transform statsContainer = FindStatsContainerFrom(_nicknameText != null ? _nicknameText.transform : null);
            if (statsContainer == null)
                statsContainer = FindDeep(transform, "ProfileStatsContainer");

            if (_accountInfoText == null)
                _accountInfoText = CreatePanelText("AccountInfoText", "로그인 정보 확인 중...");
            if (_statusText == null)
                _statusText = CreatePanelText("StatusText", string.Empty);

            MoveToPanel(_accountInfoText != null ? _accountInfoText.transform : null, 70f, 0f);

            if (statsContainer != null)
            {
                statsContainer.SetParent(_profileContentPanel.transform, false);
                SetLayout(statsContainer.gameObject, 0f, 1f);
                CanvasGroup statsGroup = EnsureCanvasGroup(statsContainer.gameObject);
                SetGroupVisible(statsGroup, true);

                VerticalLayoutGroup statsLayout = EnsureVerticalLayout(statsContainer.gameObject);
                statsLayout.spacing = 9f;
                statsLayout.padding = new RectOffset(0, 0, 0, 0);
                statsLayout.childControlWidth = true;
                statsLayout.childControlHeight = true;
                statsLayout.childForceExpandWidth = true;
                statsLayout.childForceExpandHeight = false;
            }

            MoveToPanel(_anonymousSection != null ? _anonymousSection.transform : null, 92f, 0f);
            MoveToPanel(_statusText != null ? _statusText.transform : null, 36f, 0f);
            MoveToPanel(_logoutButton != null ? _logoutButton.transform : null, 76f, 0f);

            StyleText(_accountInfoText, 27, TextAlignmentOptions.Center, Color.white);
            StyleText(_nicknameText, 31, TextAlignmentOptions.Center, Color.white);
            StyleText(_totalGamesText, 26, TextAlignmentOptions.Right, Color.white);
            StyleText(_winsText, 26, TextAlignmentOptions.Right, Color.white);
            StyleText(_lossesText, 26, TextAlignmentOptions.Right, Color.white);
            StyleText(_winRateText, 26, TextAlignmentOptions.Right, Color.white);
            StyleText(_lastSessionText, 25, TextAlignmentOptions.Right, Color.white);
            StyleText(_myRankText, 26, TextAlignmentOptions.Right, Color.white);
            StyleText(_statusText, 24, TextAlignmentOptions.Center, new Color(1f, 0.90f, 0.62f, 1f));

            StyleButton(_changeNicknameButton, 25);
            StyleButton(_refreshButton, 22);
            StyleButton(_logoutButton, 34);

            HideDuplicateStatsContainers(transform, statsContainer);
            HideNamed(transform, "BackButton");
            HideSiblingsExceptProfilePanel();

        }

        private TextMeshProUGUI CreatePanelText(string objectName, string defaultText)
        {
            if (_profileContentPanel == null)
                return null;

            GameObject go = FindOrCreateChild(_profileContentPanel.transform, objectName);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            if (text == null)
                text = go.AddComponent<TextMeshProUGUI>();

            text.text = defaultText;
            text.raycastTarget = false;
            return text;
        }

        private void MoveToPanel(Transform target, float preferredHeight, float flexibleHeight)
        {
            if (target == null || _profileContentPanel == null || target == _profileContentPanel.transform)
                return;

            target.SetParent(_profileContentPanel.transform, false);
            SetLayout(target.gameObject, preferredHeight, flexibleHeight);
            SetGroupVisible(EnsureCanvasGroup(target.gameObject), true);
        }

        private static Transform FindStatsContainerFrom(Transform source)
        {
            Transform t = source;
            while (t != null)
            {
                if (t.name == "ProfileStatsContainer")
                    return t;
                t = t.parent;
            }
            return null;
        }

        private static void HideDuplicateStatsContainers(Transform root, Transform keep)
        {
            if (root == null)
                return;

            foreach (Transform child in root)
            {
                if (child.name == "ProfileStatsContainer" && child != keep)
                {
                    child.name = "ProfileStatsContainer_HiddenDuplicate";
                    SetGroupVisible(EnsureCanvasGroup(child.gameObject), false);
                }

                HideDuplicateStatsContainers(child, keep);
            }
        }

        private static void HideNamed(Transform root, string objectName)
        {
            Transform t = FindDeep(root, objectName);
            if (t != null)
                SetGroupVisible(EnsureCanvasGroup(t.gameObject), false);
        }

        private void HideSiblingsExceptProfilePanel()
        {
            if (_profileContentPanel == null)
                return;

            foreach (Transform child in transform)
            {
                if (child == _profileContentPanel.transform)
                    continue;
                if (_nicknameChangePopup != null && child == _nicknameChangePopup.transform)
                    continue;

                SetGroupVisible(EnsureCanvasGroup(child.gameObject), false);
            }
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
                return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, objectName);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static GameObject FindOrCreateChild(Transform parent, string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
                return existing.gameObject;

            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image EnsureImage(GameObject go)
        {
            if (!go.TryGetComponent(out Image image))
                image = go.AddComponent<Image>();
            return image;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (!go.TryGetComponent(out CanvasGroup group))
                group = go.AddComponent<CanvasGroup>();
            return group;
        }

        private static VerticalLayoutGroup EnsureVerticalLayout(GameObject go)
        {
            if (!go.TryGetComponent(out VerticalLayoutGroup layout))
                layout = go.AddComponent<VerticalLayoutGroup>();
            return layout;
        }

        private static void SetGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private static void SetLayout(GameObject go, float preferredHeight, float flexibleHeight)
        {
            if (go == null)
                return;
            if (!go.TryGetComponent(out LayoutElement layout))
                layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.flexibleHeight = flexibleHeight;
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

        private static void StyleText(TextMeshProUGUI text, int fontSize, TextAlignmentOptions alignment, Color color)
        {
            if (text == null)
                return;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
        }

        private static void StyleButton(Button button, int fontSize)
        {
            if (button == null)
                return;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                StyleText(label, fontSize, TextAlignmentOptions.Center, Color.black);
        }
    }
}
