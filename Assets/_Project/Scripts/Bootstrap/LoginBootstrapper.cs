// ============================================================================
// LoginBootstrapper.cs
// Login.unity 씬 전용 Composition Root.
//
// 역할:
//   1. Google Play Games 플러그인 활성화 (게임 전체에서 1회만 호출)
//   2. FirebaseAuthService 인스턴스 생성 및 초기화
//   3. LoginUseCase / AccountLinkUseCase 인스턴스 생성
//   4. 모든 View(LoginSelectView, EmailLoginView 등)에 의존성 주입
//   5. 자동 로그인 시도 → 성공 시 Lobby 씬 이동, 실패 시 LoginRootView 활성화
//
// GameBootstrapper 와의 관계:
//   - LoginBootstrapper 는 Login 씬에만 존재. GameBootstrapper 는 Lobby/Game 씬에 존재.
//   - 두 Bootstrapper 는 완전히 독립적이며, 서로 참조하지 않는다.
//   - 씬 전환 시점(SceneManager.LoadScene) 에 한쪽이 파괴되고 다른 쪽이 새로 생성된다.
//
// 부착 위치: Login.unity 씬의 [Bootstrap] 빈 GameObject.
//
// Bootstrap 레이어 — 모든 레이어에 접근 가능한 유일한 곳.
// ============================================================================

using System.Threading.Tasks;
using GooglePlayGames;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Application;
using Hexiege.Infrastructure;
using Hexiege.Presentation;

namespace Hexiege.Bootstrap
{
    /// <summary>
    /// Login 씬 진입점. 의존성 그래프 구성 + 자동 로그인 분기.
    /// </summary>
    public class LoginBootstrapper : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조 — View 들
        // ====================================================================

        [Header("View 참조")]
        [Tooltip("로그인 씬의 루트 View. 패널 전환 및 Back 스택 관리.")]
        [SerializeField] private LoginRootView _rootView;

        [Tooltip("로그인 방식 선택 View.")]
        [SerializeField] private LoginSelectView _loginSelectView;

        [Tooltip("이메일 로그인 View.")]
        [SerializeField] private EmailLoginView _emailLoginView;

        [Tooltip("이메일 회원가입 View.")]
        [SerializeField] private SignUpView _signUpView;

        [Tooltip("이메일 인증 대기 View.")]
        [SerializeField] private EmailVerifyView _emailVerifyView;

        [Tooltip("비밀번호 재설정 View.")]
        [SerializeField] private PasswordResetView _passwordResetView;

        [Tooltip("익명 로그인 경고 팝업.")]
        [SerializeField] private AnonymousWarningPopup _anonymousWarningPopup;

        [Header("로딩 표시")]
        [Tooltip("자동 로그인 시도 중 표시할 로딩 인디케이터 GameObject.")]
        [SerializeField] private GameObject _loadingIndicator;

        [Header("씬 전환 설정")]
        [Tooltip("로그인 성공 후 이동할 씬 이름. 기본값: Lobby")]
        [SerializeField] private string _nextSceneName = "Lobby";

        [Header("사운드")]
        [Tooltip("AudioManager에 주입할 BGM/SFX 데이터 묶음(SoundConfig 에셋).")]
        [SerializeField] private SoundConfig _soundConfig;

        // ====================================================================
        // 런타임 인스턴스
        // ====================================================================

        private FirebaseAuthService _authService;
        private LoginUseCase _loginUseCase;
        private AccountLinkUseCase _accountLinkUseCase;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        /// <summary>
        /// Awake 에서 Google Play Games 활성화.
        /// PlayGamesPlatform.Activate() 는 게임 시작 시 단 한 번만 호출해야 한다(공식 문서 권장).
        /// </summary>
        private void Awake()
        {
            // GPGS 활성화 — 이후 PlayGamesPlatform.Instance 가 정상 동작한다.
            //   GooglePlayGames 플러그인이 설치되지 않은 상태에서 컴파일하면 에러가 나므로,
            //   SDK 설치 후 빌드해야 한다.
            PlayGamesPlatform.Activate();
            Debug.Log("[LoginBootstrapper] GooglePlayGames 플랫폼 활성화 완료.");
        }

        /// <summary>
        /// Start 에서 Firebase 초기화 → 의존성 주입 → 자동 로그인 시도.
        /// Awake 가 아닌 Start 인 이유: 다른 컴포넌트의 Awake 가 먼저 끝나도록 보장.
        /// </summary>
        private async void Start()
        {
            // 사운드 시스템 초기화 — Firebase 초기화보다 먼저 수행하여
            // Login 씬 진입 직후 곧바로 로그인 BGM이 재생되도록 한다.
            // AudioManager는 [Audio] 오브젝트에 부착되어 있으며 DontDestroyOnLoad로 이후 씬까지 유지된다.
            // Instance가 null일 수 있는 개발 중 직접 진입 상황을 대비해 ?. 연산자로 안전 처리한다 (규칙 5).
            AudioManager.Instance?.Initialize(_soundConfig);

            await InitializeAndDispatchAsync();
        }

        // ====================================================================
        // 초기화 흐름
        // ====================================================================

        /// <summary>
        /// 전체 초기화 + 자동 로그인 분기를 수행한다.
        /// 1) Firebase 초기화
        /// 2) UseCase 인스턴스 생성
        /// 3) View 들에 의존성 주입
        /// 4) 자동 로그인 시도 → 성공 시 Lobby 이동, 실패 시 로그인 선택 화면 표시
        /// </summary>
        private async Task InitializeAndDispatchAsync()
        {
            ShowLoading(true);

            // 1) Firebase SDK 초기화
            _authService = new FirebaseAuthService();
            bool fbReady = await _authService.InitializeAsync();
            if (!fbReady)
            {
                Debug.LogError("[LoginBootstrapper] Firebase 초기화 실패. 로그인 선택 화면을 표시합니다.");
                // 초기화 실패해도 로그인 화면 자체는 표시 — 사용자가 재시도할 수 있도록.
            }

            // 2) UseCase 생성
            _loginUseCase = new LoginUseCase(_authService);
            _accountLinkUseCase = new AccountLinkUseCase(_authService);

            // 3) View 의존성 주입
            InjectDependencies();

            // 4) 자동 로그인 시도
            //    AuthSystemRules.md 규칙:
            //      - Firebase 세션이 유효하면 로그인 화면 표시 없이 Lobby 로 즉시 이동.
            //      - 익명 로그인 상태도 자동 로그인 대상에 포함.
            if (fbReady && _authService.IsLoggedIn)
            {
                bool autoOk = await _loginUseCase.TryAutoLoginAsync();
                if (autoOk)
                {
                    Debug.Log("[LoginBootstrapper] 자동 로그인 성공. Lobby 씬으로 이동합니다.");
                    GoToNextScene();
                    return;
                }
                Debug.LogWarning("[LoginBootstrapper] 자동 로그인 실패. 로그인 선택 화면을 표시합니다.");
            }

            // 자동 로그인 실패 또는 세션 없음 → 로그인 선택 화면 표시
            ShowLoading(false);
            if (_rootView != null)
                _rootView.ShowLoginSelect();
        }

        /// <summary>
        /// 모든 View 에 UseCase / 자기 자신(LoginBootstrapper) 참조를 주입한다.
        /// View 는 Inspector 에서 다른 View 를 직접 참조하지 않고, LoginRootView 를 통해서만
        /// 화면 전환을 요청한다 — 의존성 방향 일관성 유지.
        /// </summary>
        private void InjectDependencies()
        {
            if (_rootView != null)
                _rootView.Initialize(this, _loginUseCase);

            if (_loginSelectView != null)
                _loginSelectView.Initialize(_rootView, _loginUseCase, this);

            if (_emailLoginView != null)
                _emailLoginView.Initialize(_rootView, _loginUseCase, this);

            if (_signUpView != null)
                _signUpView.Initialize(_rootView, _loginUseCase, this);

            if (_emailVerifyView != null)
                _emailVerifyView.Initialize(_rootView, _loginUseCase, this);

            if (_passwordResetView != null)
                _passwordResetView.Initialize(_rootView, _loginUseCase);

            if (_anonymousWarningPopup != null)
                _anonymousWarningPopup.Initialize(_rootView, _loginUseCase, this);
        }

        // ====================================================================
        // 외부 호출 API
        // ====================================================================

        /// <summary>
        /// 로그인 성공 후 다음 씬(Lobby)으로 이동한다.
        /// View 들이 로그인 완료 시 호출.
        /// </summary>
        public void GoToNextScene()
        {
            if (string.IsNullOrWhiteSpace(_nextSceneName))
            {
                Debug.LogError("[LoginBootstrapper] _nextSceneName 이 비어 있어 씬 이동을 진행할 수 없습니다.");
                return;
            }
            SceneManager.LoadScene(_nextSceneName);
        }

        /// <summary>
        /// 로딩 인디케이터 표시 토글.
        /// 자동 로그인 / 비동기 요청 중 사용자에게 진행 상황을 안내.
        /// </summary>
        public void ShowLoading(bool show)
        {
            if (_loadingIndicator != null)
                _loadingIndicator.SetActive(show);
        }
    }
}
