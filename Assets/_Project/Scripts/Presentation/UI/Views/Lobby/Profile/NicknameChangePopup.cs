// ============================================================================
// NicknameChangePopup.cs
// 로비 Profile 탭의 "닉네임 변경" 모달 팝업. (Lobby.unity 내 배치)
//
// 역할(확정 결정 3 — 무료 1회 변경 A안):
//   - 무료 미사용 상태: 새 닉네임 입력 + 검증 + 저장(코드 유지) → 성공 시 프로필 갱신.
//   - 무료 소진 상태: 입력 대신 "다이아 필요 / 구매하기(준비 중)" 안내만 표시.
//   - 모달 규칙(GameSystemRules_UI 규칙 8~9): 배경 탭으로 닫히지 않으며, 확인/취소로만 닫힌다.
//
// 오버레이 소유권(아키텍처 제약):
//   자체 반투명 오버레이를 만들지 않고 UIManager 단일 소유 BlockingOverlay 를
//   Modal 모드(콜백 없음)로 재사용한다 — AnonymousWarningPopup 과 동일 패턴.
//
// 의존성:
//   ProfileView 가 이미 보유한 PlayerProfileUseCase 인스턴스를 Initialize() 로 주입받아 쓴다.
//   (신규 Infrastructure 클래스를 만들지 않으므로 Presentation→Infrastructure 추가 결합 없음.)
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 닉네임 변경 모달 팝업 View.
    /// </summary>
    public class NicknameChangePopup : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("팝업 본체")]
        [Tooltip("팝업 등장/퇴장 애니메이션을 담당하는 AnimatedPanel.")]
        [SerializeField] private AnimatedPanel _panel;

        [Tooltip("팝업 제목 텍스트('닉네임 변경').")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("무료 변경 섹션 (무료 미사용 시 표시)")]
        [Tooltip("무료 변경 입력 영역 전체를 감싸는 CanvasGroup(입력 필드 포함).")]
        [SerializeField] private CanvasGroup _freeSectionGroup;

        [Tooltip("새 닉네임 입력 필드.")]
        [SerializeField] private TMP_InputField _nicknameInput;

        [Header("유료 안내 섹션 (무료 소진 시 표시)")]
        [Tooltip("유료 안내 영역 전체를 감싸는 CanvasGroup(안내 문구 + 구매 버튼).")]
        [SerializeField] private CanvasGroup _paidSectionGroup;

        [Tooltip("유료 안내 문구('다이아 N개 필요' 등).")]
        [SerializeField] private TextMeshProUGUI _paidNoticeText;

        [Tooltip("구매하기 버튼(현재는 '준비 중' 안내만).")]
        [SerializeField] private Button _purchaseButton;

        [Header("상태 / 하단 버튼")]
        [Tooltip("검증 실패/저장 오류/준비 중 등 상태 안내 텍스트.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Tooltip("확인 버튼(무료 변경 저장). 유료 소진 시에는 숨긴다.")]
        [SerializeField] private Button _confirmButton;

        [Tooltip("취소 버튼(팝업 닫기). 모달이므로 이 버튼(또는 확인 성공)으로만 닫힌다.")]
        [SerializeField] private Button _cancelButton;

        // ====================================================================
        // 의존성 / 상태
        // ====================================================================

        // ProfileView 로부터 주입받는 프로필 UseCase(검증/변경 저장에 사용).
        private PlayerProfileUseCase _profileUseCase;

        // 변경 성공 시 호출할 콜백(ProfileView 의 프로필 화면 재갱신).
        private System.Action _onChanged;

        // 확인 버튼의 표시/숨김을 CanvasGroup 으로 처리하기 위한 캐시(공통 UI 규칙 5).
        private CanvasGroup _confirmButtonGroup;

        // 처리 중 중복 클릭 방지 플래그.
        private bool _busy;

        private bool _runtimeLayoutPolished;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// ProfileView 에서 호출. UseCase 주입 + 성공 콜백 등록 + 버튼/입력 리스너 연결.
        /// </summary>
        /// <param name="profileUseCase">닉네임 검증/변경 저장에 사용할 UseCase.</param>
        /// <param name="onChanged">변경 성공 시 호출할 콜백(프로필 갱신 등).</param>
        private void Awake()
        {
            EnsureRuntimeLayoutPolished();
        }

        public void Initialize(PlayerProfileUseCase profileUseCase, System.Action onChanged)
        {
            EnsureRuntimeLayoutPolished();

            _profileUseCase = profileUseCase;
            _onChanged = onChanged;

            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancelClicked);
            if (_purchaseButton != null) _purchaseButton.onClick.AddListener(OnPurchaseClicked);
            if (_nicknameInput != null) _nicknameInput.onValueChanged.AddListener(OnInputChanged);

            // 확인 버튼 표시/숨김용 CanvasGroup 확보(없으면 추가).
            if (_confirmButton != null &&
                !_confirmButton.TryGetComponent(out _confirmButtonGroup))
            {
                _confirmButtonGroup = _confirmButton.gameObject.AddComponent<CanvasGroup>();
            }

            if (_titleText != null && string.IsNullOrEmpty(_titleText.text))
                _titleText.text = "닉네임 변경";
        }

        private void OnDestroy()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveAllListeners();
            if (_cancelButton != null) _cancelButton.onClick.RemoveAllListeners();
            if (_purchaseButton != null) _purchaseButton.onClick.RemoveAllListeners();
            if (_nicknameInput != null) _nicknameInput.onValueChanged.RemoveListener(OnInputChanged);
        }

        // ====================================================================
        // 표시 / 숨김
        // ====================================================================

        /// <summary>
        /// 모달을 연다. 무료 사용 여부에 따라 입력(무료)/안내(유료) UI 를 전환한다.
        /// </summary>
        /// <param name="usedFree">무료 닉네임 변경을 이미 사용했는지 여부.</param>
        /// <param name="currentNickname">현재 닉네임(입력 필드 초기값으로 채워 편의 제공).</param>
        public void Show(bool usedFree, string currentNickname)
        {
            _busy = false;
            ClearStatus();

            // UIManager 단일 소유 BlockingOverlay 를 Modal 모드로 표시(콜백 없음 → 배경 탭 닫힘 불가).
            UIManager.Instance?.ShowBlockingOverlay();

            if (usedFree)
            {
                // 유료 안내 모드: 입력/확인 숨김, 안내/구매 표시.
                SetGroupVisible(_freeSectionGroup, false);
                SetGroupVisible(_paidSectionGroup, true);
                SetGroupVisible(_confirmButtonGroup, false);

                if (_paidNoticeText != null && string.IsNullOrEmpty(_paidNoticeText.text))
                    _paidNoticeText.text = "무료 변경을 이미 사용했습니다.\n추가 변경은 다이아로 가능합니다.";
            }
            else
            {
                // 무료 변경 모드: 입력/확인 표시, 안내/구매 숨김.
                SetGroupVisible(_freeSectionGroup, true);
                SetGroupVisible(_paidSectionGroup, false);
                SetGroupVisible(_confirmButtonGroup, true);

                if (_nicknameInput != null)
                    _nicknameInput.text = currentNickname ?? string.Empty;

                // 현재 입력값 기준으로 확인 버튼 활성 여부를 초기화한다.
                UpdateConfirmInteractable(_nicknameInput != null ? _nicknameInput.text : string.Empty);
            }

            if (_panel != null) _panel.Show();
        }

        /// <summary>모달을 닫는다(오버레이 해제 + 퇴장 애니메이션).</summary>
        public void Hide()
        {
            UIManager.Instance?.HideBlockingOverlay();
            if (_panel != null) _panel.Hide();
        }

        // ====================================================================
        // 버튼 / 입력 콜백
        // ====================================================================

        /// <summary>
        /// 입력 값 변경 → 검증 통과 시에만 확인 버튼 활성(빈 값/무효 값에서는 클릭 불가).
        /// </summary>
        private void OnInputChanged(string text) => UpdateConfirmInteractable(text);

        /// <summary>
        /// 확인(무료 변경 저장) 클릭 → 검증 → ChangeNicknameAsync → 성공 시 닫기 + 프로필 갱신.
        /// </summary>
        private async void OnConfirmClicked()
        {
            if (_busy || _profileUseCase == null) return;

            ClearStatus();
            string input = _nicknameInput != null ? _nicknameInput.text.Trim() : string.Empty;

            // 1차 클라이언트 검증.
            NicknameValidation preCheck = _profileUseCase.ValidateNickname(input);
            if (preCheck != NicknameValidation.Valid)
            {
                SetStatus(ValidationMessage(preCheck));
                return;
            }

            _busy = true;
            SetInteractable(false);
            UIManager.Instance?.ShowLoading(true, "닉네임 변경 중...");

            try
            {
                // 코드 유지 + hasUsedFreeNicknameChange=true 저장은 UseCase 가 담당(확정 결정 3).
                NicknameValidation result = await _profileUseCase.ChangeNicknameAsync(input);
                if (result != NicknameValidation.Valid)
                {
                    SetStatus(ValidationMessage(result));
                    return;
                }

                // 성공 → 모달 닫기 + 프로필 화면 갱신 콜백.
                Hide();
                _onChanged?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NicknameChangePopup] 닉네임 변경 실패: {e.Message}");
                SetStatus("닉네임 변경 중 오류가 발생했습니다. 다시 시도하세요.");
            }
            finally
            {
                UIManager.Instance?.ShowLoading(false);
                SetInteractable(true);
                _busy = false;
            }
        }

        /// <summary>
        /// 구매하기 클릭 → 결제 미구현이므로 "준비 중" 안내만 표시하고 팝업은 유지한다(범위 밖).
        /// </summary>
        private void OnPurchaseClicked()
        {
            SetStatus("인앱 결제는 준비 중입니다.");
        }

        /// <summary>취소 클릭 → 모달을 닫는다.</summary>
        private void OnCancelClicked()
        {
            if (_busy) return; // 저장 처리 중에는 닫기를 막는다.
            Hide();
        }

        // ====================================================================
        // UI 헬퍼
        // ====================================================================

        /// <summary>입력값이 검증을 통과할 때만 확인 버튼을 활성화한다.</summary>
        private void UpdateConfirmInteractable(string text)
        {
            if (_confirmButton == null) return;

            bool valid = _profileUseCase != null &&
                         _profileUseCase.ValidateNickname(text) == NicknameValidation.Valid;
            _confirmButton.interactable = valid;
        }

        /// <summary>확인/취소/구매/입력 필드 활성화 토글 — 비동기 저장 중 중복 입력 방지.</summary>
        private void SetInteractable(bool on)
        {
            if (_confirmButton != null) _confirmButton.interactable = on;
            if (_cancelButton != null) _cancelButton.interactable = on;
            if (_purchaseButton != null) _purchaseButton.interactable = on;
            if (_nicknameInput != null) _nicknameInput.interactable = on;
        }

        /// <summary>CanvasGroup 표시/숨김(공통 UI 규칙 5, SetActive 금지). null 안전.</summary>
        private static void SetGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

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

        private void SetStatus(string text)
        {
            if (_statusText != null) _statusText.text = text ?? string.Empty;
        }

        private void ClearStatus() => SetStatus(string.Empty);

        private void EnsureRuntimeLayoutPolished()
        {
            if (_runtimeLayoutPolished)
                return;

            PolishRuntimeLayout();
            _runtimeLayoutPolished = true;
        }

        private void PolishRuntimeLayout()
        {
            RectTransform panelRt = _panel != null ? _panel.GetComponent<RectTransform>() : null;
            if (panelRt != null)
                SetAnchors(panelRt, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.72f));

            GameObject panelGo = _panel != null ? _panel.gameObject : null;
            if (panelGo != null && panelGo.TryGetComponent(out VerticalLayoutGroup panelLayout))
            {
                panelLayout.padding = new RectOffset(30, 30, 24, 24);
                panelLayout.spacing = 12f;
                panelLayout.childControlWidth = true;
                panelLayout.childControlHeight = true;
                panelLayout.childForceExpandWidth = true;
                panelLayout.childForceExpandHeight = false;
            }

            StyleText(_titleText, 36, TextAlignmentOptions.Center, Color.white);
            StyleInput(_nicknameInput);
            StyleText(_paidNoticeText, 25, TextAlignmentOptions.Center, Color.white);
            StyleText(_statusText, 23, TextAlignmentOptions.Center, new Color(1f, 0.66f, 0.62f, 1f));
            StyleButton(_confirmButton, 28);
            StyleButton(_cancelButton, 28);
            StyleButton(_purchaseButton, 26);

            SetLayout(_titleText != null ? _titleText.gameObject : null, 58f, 0f);
            SetLayout(_freeSectionGroup != null ? _freeSectionGroup.gameObject : null, 92f, 0f);
            SetLayout(_nicknameInput != null ? _nicknameInput.gameObject : null, 82f, 0f);
            SetLayout(_paidSectionGroup != null ? _paidSectionGroup.gameObject : null, 126f, 0f);
            SetLayout(_paidNoticeText != null ? _paidNoticeText.gameObject : null, 62f, 0f);
            SetLayout(_purchaseButton != null ? _purchaseButton.gameObject : null, 64f, 0f);
            SetLayout(_statusText != null ? _statusText.gameObject : null, 34f, 0f);

            Transform buttonRow = _confirmButton != null ? _confirmButton.transform.parent : null;
            if (buttonRow != null)
            {
                SetLayout(buttonRow.gameObject, 76f, 0f);
                if (buttonRow.TryGetComponent(out HorizontalLayoutGroup rowLayout))
                {
                    rowLayout.spacing = 14f;
                    rowLayout.childControlWidth = true;
                    rowLayout.childControlHeight = true;
                    rowLayout.childForceExpandWidth = true;
                    rowLayout.childForceExpandHeight = true;
                }
            }

            SetLayout(_confirmButton != null ? _confirmButton.gameObject : null, 76f, 0f, 1f);
            SetLayout(_cancelButton != null ? _cancelButton.gameObject : null, 76f, 0f, 1f);
        }

        private static void StyleInput(TMP_InputField input)
        {
            if (input == null)
                return;

            if (input.textComponent != null)
                StyleText(input.textComponent, 28, TextAlignmentOptions.Center, new Color(0.18f, 0.13f, 0.08f, 1f));

            if (input.placeholder is TMP_Text placeholder)
                StyleText(placeholder, 26, TextAlignmentOptions.Center, new Color(0.42f, 0.34f, 0.25f, 0.82f));
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

        private static void SetLayout(GameObject go, float preferredHeight, float flexibleHeight, float flexibleWidth = -1f)
        {
            if (go == null)
                return;

            if (!go.TryGetComponent(out LayoutElement layout))
                layout = go.AddComponent<LayoutElement>();

            layout.preferredHeight = preferredHeight;
            layout.flexibleHeight = flexibleHeight;
            if (flexibleWidth >= 0f)
                layout.flexibleWidth = flexibleWidth;
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
