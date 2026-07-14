// ============================================================================
// NicknameSetupView.cs
// 닉네임 설정 화면. (Login.unity 내 패널)
//
// 역할:
//   - "UGS 세션이 있는 첫 로그인 성공 직후" 닉네임을 1회 설정한다.
//     (Google 최초 로그인 후 = LoginSelectView, 이메일 최초 로그인 후 = EmailLoginView)
//   - 확인 버튼: 입력 검증 → 저장 → 로비 이동.
//   - 스킵 버튼: 자동 생성 닉네임으로 저장 → 로비 이동.
//   - 완료 후 흐름은 경로와 무관하게 항상 로비(GoToNextScene)로 통일되었다.
//     (과거에는 이메일 경로만 이메일 인증 화면으로 분기했으나, 닉네임 설정 시점을
//      "첫 로그인 성공 직후"로 통일하면서 이 분기가 사라졌다.)
//
// isGooglePath 파라미터의 의미(축소됨):
//   더 이상 "완료 후 이동 경로(라우팅)"를 뜻하지 않는다.
//   이제는 스킵 시 자동 생성 닉네임의 접두사 구분("구글" vs "사용자")에만 사용한다.
//   화면을 여는 쪽(LoginSelectView / EmailLoginView)이
//   LoginRootView.ShowNicknameSetup(isGooglePath) 로 지정하고,
//   LoginRootView 가 본 View 의 PrepareForShow(isGooglePath) 를 호출해 전달한다.
//
// AuthSystemRules.md 닉네임 규칙 1~6 / GameSystemRules_UI.md 닉네임 설정 화면 규칙 1~4.
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Application;
using Hexiege.Bootstrap;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 닉네임 설정 화면 View.
    /// </summary>
    public class NicknameSetupView : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("입력")]
        [Tooltip("닉네임 입력 필드. 한글 2~12자 또는 영문/숫자 2~24자.")]
        [SerializeField] private TMP_InputField _nicknameInput;

        [Header("버튼")]
        [Tooltip("입력한 닉네임을 저장하고 다음 화면으로 진행하는 버튼.")]
        [SerializeField] private Button _confirmButton;

        [Tooltip("자동 생성 닉네임으로 진행하는 스킵 버튼.")]
        [SerializeField] private Button _skipButton;

        [Header("상태 표시")]
        [Tooltip("검증/저장 결과 안내 메시지.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        // ====================================================================
        // 의존성 / 상태
        // ====================================================================

        private LoginRootView _rootView;
        private PlayerProfileUseCase _profileUseCase;
        private LoginBootstrapper _bootstrapper;

        // 자동 생성 닉네임 접두사 구분 플래그.
        //   true = Google 경로(스킵 시 "구글_"), false = 이메일 경로(스킵 시 "사용자_").
        //   [의미 축소] 과거에는 완료 후 이동 경로(라우팅)로도 쓰였으나,
        //   이제 완료 후 흐름은 경로 무관하게 항상 로비이므로 접두사 구분 용도만 남았다.
        //   PrepareForShow() 로 화면을 열 때마다 갱신된다.
        private bool _isGooglePath;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// LoginBootstrapper 에서 호출. 의존성 주입 + 버튼 리스너 등록.
        /// </summary>
        public void Initialize(
            LoginRootView rootView, PlayerProfileUseCase profileUseCase, LoginBootstrapper bootstrapper)
        {
            _rootView = rootView;
            _profileUseCase = profileUseCase;
            _bootstrapper = bootstrapper;

            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);

            ClearStatus();
        }

        private void OnDestroy()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveAllListeners();
            if (_skipButton != null) _skipButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 화면 열기 준비
        // ====================================================================

        /// <summary>
        /// 화면을 표시하기 직전에 호출된다(LoginRootView.ShowNicknameSetup 이 호출).
        /// 자동닉네임 접두사 구분값을 설정하고 입력/상태를 초기화한다.
        /// </summary>
        /// <param name="isGooglePath">
        /// 스킵 시 자동 생성 닉네임 접두사 구분. true=Google("구글_"), false=이메일("사용자_").
        /// (완료 후 이동 경로는 경로 무관하게 항상 로비이므로 이 값은 접두사 구분에만 쓰인다.)
        /// </param>
        public void PrepareForShow(bool isGooglePath)
        {
            _isGooglePath = isGooglePath;
            if (_nicknameInput != null) _nicknameInput.text = string.Empty;
            ClearStatus();
        }

        // ====================================================================
        // 버튼 콜백
        // ====================================================================

        /// <summary>
        /// 확인 버튼 클릭 → 입력 검증 → 저장 → 다음 화면.
        /// </summary>
        private async void OnConfirmClicked()
        {
            ClearStatus();

            string input = _nicknameInput != null ? _nicknameInput.text.Trim() : string.Empty;

            // 1차 클라이언트 검증 — 저장 호출 전에 즉시 안내(서버 최종 저장 전 UX).
            NicknameValidation preCheck = _profileUseCase.ValidateNickname(input);
            if (preCheck != NicknameValidation.Valid)
            {
                SetStatus(ValidationMessage(preCheck));
                return;
            }

            SetInteractable(false);
            _bootstrapper.ShowLoading(true, "닉네임 저장 중...");

            try
            {
                NicknameValidation result = await _profileUseCase.SaveNicknameAsync(input);
                if (result != NicknameValidation.Valid)
                {
                    // 저장 직전 재검증에서 걸린 경우(이론상 preCheck 와 동일하나 방어적으로 처리).
                    SetStatus(ValidationMessage(result));
                    return;
                }

                GoToNextStep();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NicknameSetupView] 닉네임 저장 실패: {e.Message}");
                SetStatus("닉네임 저장 중 오류가 발생했습니다. 다시 시도하세요.");
            }
            finally
            {
                _bootstrapper.ShowLoading(false);
                SetInteractable(true);
            }
        }

        /// <summary>
        /// 스킵 버튼 클릭 → 자동 생성 닉네임으로 저장 → 다음 화면.
        /// 자동 생성 접두사: Google 경로="구글", 이메일 경로="사용자" (AuthSystemRules.md 규칙 4).
        /// </summary>
        private async void OnSkipClicked()
        {
            ClearStatus();
            SetInteractable(false);
            _bootstrapper.ShowLoading(true, "닉네임 생성 중...");

            try
            {
                string prefix = _isGooglePath ? "구글" : "사용자";
                await _profileUseCase.SaveAutoNicknameAsync(prefix);
                GoToNextStep();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NicknameSetupView] 자동 닉네임 저장 실패: {e.Message}");
                SetStatus("닉네임 생성 중 오류가 발생했습니다. 다시 시도하세요.");
            }
            finally
            {
                _bootstrapper.ShowLoading(false);
                SetInteractable(true);
            }
        }

        // ====================================================================
        // 흐름 분기
        // ====================================================================

        /// <summary>
        /// 닉네임 저장 완료 후 다음 화면으로 이동한다.
        ///   [흐름 통일] 이제 닉네임 설정은 Google/이메일 모두 "세션 있는 첫 로그인 성공 직후"에만
        ///   일어나므로, 완료 후에는 경로와 무관하게 항상 로비(Lobby 씬)로 이동한다.
        /// </summary>
        private void GoToNextStep()
        {
            // 항상 로비로 이동 (경로 분기 없음).
            _bootstrapper.GoToNextScene();

            // === [구 로직 — 비활성화] 경로별 분기 (이메일 경로 → 이메일 인증 화면) ===
            //   과거에는 이메일 가입 직후 닉네임을 설정했기에 완료 후 인증 화면으로 보냈다.
            //   이제 닉네임은 "인증 후 첫 로그인 성공 직후"에 설정되므로 인증 분기가 불필요하다.
            //   실기 통과 후 아래 블록은 최종 삭제 예정(WORKFLOW 기존 로직 제거 규칙).
            // if (_isGooglePath)
            // {
            //     _bootstrapper.GoToNextScene();
            // }
            // else
            // {
            //     if (_rootView != null) _rootView.ShowEmailVerify();
            // }
        }

        // ====================================================================
        // UI 헬퍼
        // ====================================================================

        /// <summary>검증 결과 코드를 사용자 안내 문구로 변환한다.</summary>
        private static string ValidationMessage(NicknameValidation v)
        {
            switch (v)
            {
                case NicknameValidation.Empty:
                    return "닉네임을 입력하세요.";
                case NicknameValidation.LengthOutOfRange:
                    return "닉네임은 한글 2~12자 또는 영문·숫자 2~24자여야 합니다.";
                case NicknameValidation.InvalidCharacter:
                    return "닉네임에 특수문자나 공백은 사용할 수 없습니다.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>버튼/입력 필드 활성화 토글 — 비동기 처리 중 중복 입력 방지.</summary>
        private void SetInteractable(bool on)
        {
            if (_confirmButton != null) _confirmButton.interactable = on;
            if (_skipButton != null) _skipButton.interactable = on;
            if (_nicknameInput != null) _nicknameInput.interactable = on;
        }

        private void SetStatus(string text)
        {
            if (_statusText != null) _statusText.text = text ?? string.Empty;
        }

        private void ClearStatus() => SetStatus(string.Empty);
    }
}
