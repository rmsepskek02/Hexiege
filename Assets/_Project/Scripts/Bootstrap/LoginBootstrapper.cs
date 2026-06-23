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

using System;                       // [DEBUG-TEMP] 디버깅 완료 후 제거 (DateTime)
using System.Threading.Tasks;
using GooglePlayGames;
using UnityEngine;
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

        [Tooltip("네트워크 오류 안내 팝업.")]
        [SerializeField] private NetworkErrorPopup _networkErrorPopup;

        [Header("스플래시 오버레이")]
        [Tooltip("Login 씬 진입 시 표시되는 스플래시 오버레이. " +
                 "초기화 중 '로딩 중...' → 완료 후 'Tap to Start' → 탭 시 페이드아웃을 담당한다.")]
        [SerializeField] private SplashOverlayView _splashOverlay;

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
            // 1) 스플래시 오버레이에 "로딩 중..." 상태를 가장 먼저 표시한다.
            //    초기화가 끝날 때까지 배경 이미지 + 상태 문구로 진행 상황을 안내한다.
            //    개발 중 오버레이 미연결 상황을 대비해 ?. 로 안전 처리한다.
            _splashOverlay?.SetStatus("로딩 중...");

            // 2) 사운드 시스템 초기화 — Firebase 초기화보다 먼저 수행하여
            //    Login 씬 진입 직후 곧바로 로그인 BGM이 재생되도록 한다.
            //    AudioManager는 [Audio] 오브젝트에 부착되어 있으며 DontDestroyOnLoad로 이후 씬까지 유지된다.
            //    Instance가 null일 수 있는 개발 중 직접 진입 상황을 대비해 ?. 연산자로 안전 처리한다 (규칙 5).
            AudioManager.Instance?.Initialize(_soundConfig);

            // 3) 전역 UIManager 존재 확인(로그용). UIManager는 이 씬의 [UI Systems] 하위에 배치되어
            //    Awake에서 Instance 등록 + DontDestroyOnLoad 처리된다.
            //    개발 중 직접 진입 등으로 null일 수 있으므로 경고만 출력하고 진행한다(규칙 5).
            if (UIManager.Instance == null)
                Debug.LogWarning("[LoginBootstrapper] UIManager.Instance가 null입니다. " +
                                 "[UI Systems]에 UIManager가 배치되어 있는지 확인하세요. 공통 UI 호출은 무시됩니다.");

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
            RuntimeLog("INFO", "초기화 시작"); // [DEBUG-TEMP] 디버깅 완료 후 제거

            // 1) Firebase SDK 초기화
            _authService = new FirebaseAuthService();
            bool fbReady = await _authService.InitializeAsync();
            if (!fbReady)
            {
                Debug.LogError("[LoginBootstrapper] Firebase 초기화 실패. 로그인 선택 화면을 표시합니다.");
                // 초기화 실패해도 로그인 화면 자체는 표시 — 사용자가 재시도할 수 있도록.
                RuntimeLog("ERROR", "Firebase 초기화 실패"); // [DEBUG-TEMP] 디버깅 완료 후 제거
            }
            else
            {
                RuntimeLog("INFO", "Firebase 초기화 성공"); // [DEBUG-TEMP] 디버깅 완료 후 제거
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
                    Debug.Log("[LoginBootstrapper] 자동 로그인 성공. 'Tap to Start' 후 Lobby 씬으로 이동합니다.");
                    // 자동 로그인 성공이라도 곧바로 씬을 이동하지 않고 "Tap to Start"를 거친다.
                    //   탭 콜백으로 GoToNextScene을 주입한 뒤, 로딩 인디케이터를 끄고 "Tap to Start"를 표시한다.
                    //   → 사용자가 탭하면 오버레이가 페이드아웃되고 그 완료 콜백(GoToNextScene)으로 Lobby 씬으로 이동한다.
                    //   오버레이가 없으면(개발 중 직접 진입 등) 곧바로 씬을 이동한다.
                    if (_splashOverlay != null)
                    {
                        // skipFade: true — 탭 시 FadeOut 없이 즉시 GoToNextScene 호출.
                        // SceneLoader가 로딩 인디케이터를 표시하므로 Login 씬 배경이 노출되지 않는다.
                        _splashOverlay.SetTapCallback(GoToNextScene, skipFade: true);

                        // [로딩 인디케이터 끄기] 스플래시가 "Tap to Start" 상태로 화면에 준비되는 시점이다(UI 규칙 L-3).
                        UIManager.Instance?.ShowLoading(false);

                        _splashOverlay.ShowTapToStart();
                    }
                    else
                    {
                        UIManager.Instance?.ShowLoading(false);
                        GoToNextScene();
                    }
                    return;
                }
                Debug.LogWarning("[LoginBootstrapper] 자동 로그인 실패. 'Tap to Start' 후 로그인 선택 화면을 표시합니다.");
            }

            // 5) 자동 로그인 실패 또는 세션 없음
            //    "Tap to Start"를 표시하고, 사용자가 탭하면 오버레이가 페이드아웃된 뒤
            //    그 완료 콜백(ShowLoginSelect)으로 로그인 선택 화면을 노출한다.
            //    → 탭 콜백을 먼저 주입(SetTapCallback)한 다음 ShowTapToStart()를 호출한다.
            //    오버레이가 없으면(개발 중 직접 진입 등) 곧바로 로그인 선택 화면을 표시한다.
            if (_splashOverlay != null)
            {
                _splashOverlay.SetTapCallback(ShowLoginSelect);

                // [로딩 인디케이터 끄기] 스플래시가 "Tap to Start" 상태로 화면에 준비되는 시점이다.
                //   이 시점에는 Login 씬이 이미 사용자에게 보여줄 준비가 끝났으므로,
                //   로그아웃 등으로 이전 씬에서 켜둔 전역 로딩 인디케이터를 여기서 끈다(UI 규칙 L-3).
                //   (ShowLoginSelect는 사용자가 탭한 뒤에야 실행되므로, 거기서 끄면
                //    탭 전까지 로딩 인디케이터가 계속 떠 있는 버그가 발생한다.)
                UIManager.Instance?.ShowLoading(false);

                _splashOverlay.ShowTapToStart();
            }
            else
            {
                // 스플래시가 없으면(개발 중 직접 진입 등) 곧바로 로그인 선택 화면을 표시하기 직전에 끈다.
                UIManager.Instance?.ShowLoading(false);
                ShowLoginSelect();
            }
        }

        /// <summary>
        /// 로그인 선택 화면을 노출한다.
        /// 스플래시 페이드아웃 완료 콜백(탭 이후) 또는 오버레이 미연결 시 직접 호출된다.
        /// </summary>
        private void ShowLoginSelect()
        {
            // [주의] 로딩 인디케이터 끄기는 더 이상 여기서 하지 않는다.
            //   ShowLoginSelect는 스플래시 "Tap to Start"를 사용자가 탭한 뒤에야 실행되므로,
            //   여기서 끄면 탭 전까지 로딩 인디케이터가 계속 떠 있는 버그가 발생한다(TC-04).
            //   → 로딩 끄기는 스플래시가 화면에 준비된 시점(InitializeAndDispatchAsync)으로 이동했다.

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

            if (_networkErrorPopup != null)
                _networkErrorPopup.Initialize(_rootView);
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
            // SceneLoader.Load 가 내부에서 로딩 인디케이터를 자동 표시한다.
            SceneLoader.Load(_nextSceneName);
        }

        /// <summary>
        /// 로딩 인디케이터 표시 토글.
        /// 로딩 인디케이터는 전역 UIManager로 이동했으므로, 이 메서드는 UIManager에 위임한다.
        /// (EmailLoginView 등 기존 호출부가 이 시그니처를 그대로 사용하므로 메서드는 유지한다.)
        /// UIManager.Instance가 null일 수 있는 개발 중 직접 진입 상황을 대비해 ?. 로 안전 처리한다.
        /// </summary>
        public void ShowLoading(bool show, string message = "")
        {
            UIManager.Instance?.ShowLoading(show, message);
        }

        // ====================================================================
        // [DEBUG-TEMP] 디버깅 완료 후 제거 — RuntimeLog 출력
        // 실기기(Android)에서 로그인 흐름을 Logcat(Debug.Log)으로 추적하기 위한 임시 로그.
        // 사용자가 Logcat 출력을 복사해 공유한다.
        // ====================================================================

        /// <summary>
        /// [DEBUG-TEMP] 한 줄 로그를 Debug.Log(Logcat)로 출력한다.
        /// 형식: [HH:MM:SS.ms] [LEVEL] [Bootstrap/LoginBootstrapper] 메시지
        /// </summary>
        private void RuntimeLog(string level, string message)
        {
            // 에디터 파일 형식과 동일하게 시간/레벨/시스템 태그를 붙여 출력한다.
            Debug.Log($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [Bootstrap/LoginBootstrapper] {message}");
        }
    }
}
