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

            PolishRuntimeLayout();

            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);

            // 입력 값이 바뀔 때마다 확인 버튼 활성/비활성을 실시간 갱신한다
            // (닉네임 설정 화면 규칙 2 "빈 값이면 확인 버튼 클릭 불가" 방식 채택).
            if (_nicknameInput != null) _nicknameInput.onValueChanged.AddListener(OnInputChanged);

            // 초기 상태: 입력이 비어 있으므로 확인 버튼을 비활성으로 시작한다.
            if (_confirmButton != null) _confirmButton.interactable = false;

            ClearStatus();
        }

        private void OnDestroy()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveAllListeners();
            if (_skipButton != null) _skipButton.onClick.RemoveAllListeners();
            if (_nicknameInput != null) _nicknameInput.onValueChanged.RemoveListener(OnInputChanged);
        }

        /// <summary>
        /// 입력 필드 값 변경 콜백. 현재 입력이 검증을 통과하면 확인 버튼을 활성화한다.
        /// (버튼을 상시 활성화해 두고 클릭 후 검증하는 기존 방식 대신, 빈 값/무효 값에서는
        ///  아예 클릭이 불가능하도록 실시간으로 잠근다.)
        /// </summary>
        private void OnInputChanged(string text)
        {
            if (_confirmButton == null) return;

            bool valid = _profileUseCase != null &&
                         _profileUseCase.ValidateNickname(text) == NicknameValidation.Valid;
            _confirmButton.interactable = valid;
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

            // 입력을 비웠으므로 확인 버튼도 비활성으로 초기화한다(빈 값 = 클릭 불가).
            if (_confirmButton != null) _confirmButton.interactable = false;

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

        // ====================================================================
        // 런타임 UI 레이아웃 보정
        // ====================================================================

        /// <summary>
        /// 에디터 생성 직후의 닉네임 화면이 상단에 몰려 보이지 않도록,
        /// 런타임에 Content 영역과 자식 높이를 보정한다.
        /// </summary>
        private void PolishRuntimeLayout()
        {
            Image panelImage = GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0f, 0f, 0f, 0f);
                panelImage.raycastTarget = true;
            }

            Transform content = FindDeep(transform, "Content") ?? transform;
            RectTransform contentRt = content.GetComponent<RectTransform>();
            if (contentRt != null)
                SetAnchors(contentRt, new Vector2(0.04f, 0.34f), new Vector2(0.96f, 0.73f));

            VerticalLayoutGroup layout = EnsureVerticalLayout(content.gameObject);
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            StyleNamedText(content, "Title", 40, TextAlignmentOptions.Center, Color.white);
            StyleInput(_nicknameInput);
            StyleButton(_confirmButton, 34);
            StyleButton(_skipButton, 32);
            StyleText(_statusText, 26, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.62f, 1f));

            SetNamedLayout(content, "Title", 78f, 0f);
            SetNamedLayout(content, "NicknameInput", 94f, 0f);
            SetNamedLayout(content, "ConfirmButton", 96f, 0f);
            SetNamedLayout(content, "SkipButton", 90f, 0f);
            SetNamedLayout(content, "StatusText", 0f, 1f);
        }

        private static void StyleInput(TMP_InputField input)
        {
            if (input == null)
                return;

            Image image = input.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.white;
                image.raycastTarget = true;
            }

            if (input.textComponent != null)
                StyleText(input.textComponent, 32, TextAlignmentOptions.Center, new Color(0.18f, 0.13f, 0.08f, 1f));

            if (input.placeholder is TMP_Text placeholder)
                StyleText(placeholder, 29, TextAlignmentOptions.Center, new Color(0.45f, 0.35f, 0.24f, 0.85f));
        }

        private static void StyleButton(Button button, int fontSize)
        {
            if (button == null)
                return;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                StyleText(label, fontSize, TextAlignmentOptions.Center, Color.white);
        }

        private static void StyleNamedText(Transform root, string objectName, int fontSize, TextAlignmentOptions alignment, Color color)
        {
            Transform t = FindDeep(root, objectName);
            if (t != null && t.TryGetComponent(out TextMeshProUGUI text))
                StyleText(text, fontSize, alignment, color);
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

        private static void SetNamedLayout(Transform root, string objectName, float preferredHeight, float flexibleHeight)
        {
            Transform t = FindDeep(root, objectName);
            if (t == null)
                return;

            if (!t.TryGetComponent(out LayoutElement layout))
                layout = t.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.flexibleHeight = flexibleHeight;
        }

        private static VerticalLayoutGroup EnsureVerticalLayout(GameObject go)
        {
            if (!go.TryGetComponent(out VerticalLayoutGroup layout))
                layout = go.AddComponent<VerticalLayoutGroup>();
            return layout;
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

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null)
                return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
