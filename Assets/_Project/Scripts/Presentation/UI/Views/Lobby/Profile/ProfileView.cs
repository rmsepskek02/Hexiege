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
using UnityEngine.SceneManagement;
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

        // 익명 전용 영역(_anonymousSection)에 부착된 CanvasGroup 캐시.
        // 공통 UI 규칙 Rule 5: SetActive 대신 CanvasGroup으로 표시/숨김 처리.
        // _anonymousSection은 다른 UI(계정 정보 텍스트/로그아웃 버튼 등)와 같은 부모
        // LayoutGroup 안에 배치되는 경우가 많아, SetActive로 숨기면 다른 요소가 위로
        // 밀려올라가는 레이아웃 흔들림이 생긴다. CanvasGroup.alpha=0으로 공간 유지.
        private CanvasGroup _anonymousSectionGroup;

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

            // 버튼 리스너 등록
            if (_linkGoogleButton != null) _linkGoogleButton.onClick.AddListener(OnLinkGoogleClicked);
            if (_linkEmailButton != null) _linkEmailButton.onClick.AddListener(OnLinkEmailClicked);
            if (_logoutButton != null) _logoutButton.onClick.AddListener(OnLogoutClicked);

            // 최초 UI 갱신
            RefreshUI();
        }

        private void OnDestroy()
        {
            if (_linkGoogleButton != null) _linkGoogleButton.onClick.RemoveAllListeners();
            if (_linkEmailButton != null) _linkEmailButton.onClick.RemoveAllListeners();
            if (_logoutButton != null) _logoutButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// 탭이 활성화될 때마다 UI 를 갱신한다 — Profile 탭으로 돌아왔을 때 최신 상태 반영.
        /// </summary>
        private void OnEnable()
        {
            // _authService 초기화 전에 OnEnable 이 먼저 호출될 수 있으므로 null 가드.
            if (_authService != null && _authService.IsInitialized)
                RefreshUI();
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
            }
            else
            {
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

            try
            {
                await _loginUseCase.SignOutAsync();
                SceneManager.LoadScene(_loginSceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProfileView] 로그아웃 중 예외: {e.Message}");
                SetStatus("로그아웃 중 오류가 발생했습니다.");
                SetInteractable(true);
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
    }
}
