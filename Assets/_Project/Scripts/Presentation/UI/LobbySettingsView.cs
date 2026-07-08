// ============================================================================
// LobbySettingsView.cs
// 로비 씬 "설정" 탭의 루트 View. 내부에 간단한 네비게이션 스택 구조를 가진다.
//
// 화면 구조 (2단 네비게이션):
//   1) 메인 화면(MainView): 설정 항목 버튼들을 화면 중앙 세로로 배치.
//      - [프로필] / [사운드] 버튼. BackButton은 숨김(탭 자체가 루트이므로).
//   2) 서브 화면(SubViewContainer): 항목 버튼을 누르면 해당 서브 화면으로 전환.
//      - 사운드 서브 화면: 마스터/배경음/효과음 슬라이더 3종.
//      - 프로필 서브 화면: 기존 ProfileView 내용을 향후 통합.
//      - 서브 화면에서는 좌측 상단 BackButton이 표시되고, 누르면 메인 화면으로 복귀.
//
// 표시/숨김은 GameSystemRules_UI 규칙 5에 따라 CanvasGroup
// (alpha / blocksRaycasts / interactable)으로 처리한다. SetActive를 쓰지 않는 이유는
// LayoutGroup 안에서 공간이 사라져 레이아웃이 깨지는 것을 막기 위함이다.
//
// 볼륨 슬라이더는 AudioManager(싱글톤)와 연동한다. AudioManager.Instance가 null일 수
// 있으므로(개발 중 Login 씬을 거치지 않고 직접 진입) ?. 와 ?? 1f로 안전하게 처리한다.
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 로비 설정 탭 View. 메인 버튼 목록 ↔ 서브 화면(사운드/프로필) 간 전환을 담당한다.
    /// 사운드 서브 화면의 볼륨 슬라이더는 AudioManager와 실시간 동기화된다.
    /// </summary>
    public class LobbySettingsView : MonoBehaviour
    {
        // ====================================================================
        // 슬라이더 색상 상수 (GameSystemRules_UI 규칙 3)
        //   소리가 켜진(언뮤트) 상태는 초록, 음소거(뮤트) 상태는 빨강으로 표시한다.
        //   Fill과 Background 두 이미지에 동시에 적용한다.
        // ====================================================================

        private static readonly Color ColorSoundOn  = new Color(0.2f, 0.8f, 0.3f, 1f); // 초록
        private static readonly Color ColorSoundOff = new Color(0.9f, 0.2f, 0.2f, 1f); // 빨강

        // ====================================================================
        // Inspector 참조 — 네비게이션
        // ====================================================================

        [Header("네비게이션")]
        [Tooltip("좌측 상단 뒤로가기 버튼. 서브 화면에서만 표시되며, 누르면 메인 화면으로 복귀한다. (클릭 리스너 등록용)")]
        [SerializeField] private Button _backButton;

        [Tooltip("뒤로가기 버튼의 표시/숨김을 담당하는 CanvasGroup. " +
                 "버튼도 GameObject.SetActive 대신 CanvasGroup으로 숨겨야 규칙 5를 지킨다.")]
        [SerializeField] private CanvasGroup _backButtonGroup;

        [Tooltip("설정 메인 화면(버튼 목록)의 CanvasGroup.")]
        [SerializeField] private CanvasGroup _mainView;

        [Tooltip("서브 화면들을 담는 컨테이너의 CanvasGroup. 메인 화면 표시 중에는 숨김.")]
        [SerializeField] private CanvasGroup _subViewContainer;

        // ====================================================================
        // Inspector 참조 — 설정 항목 버튼
        // ====================================================================

        [Header("설정 버튼")]
        [Tooltip("프로필 서브 화면으로 진입하는 버튼.")]
        [SerializeField] private Button _profileButton;

        [Tooltip("사운드 서브 화면으로 진입하는 버튼.")]
        [SerializeField] private Button _soundButton;

        // ====================================================================
        // Inspector 참조 — 서브 화면 CanvasGroup
        //
        // 서브 화면도 표시/숨김을 GameObject.SetActive 대신 CanvasGroup으로 처리한다
        // (GameSystemRules_UI 규칙 5). SetActive를 쓰면 LayoutGroup 안에서 오브젝트가
        // 차지하던 공간이 사라져 형제 요소들의 배치가 흔들릴 수 있기 때문이다.
        // CanvasGroup은 오브젝트를 켜둔 채 alpha만 0으로 만들어 "보이지 않지만 자리는
        // 유지"하는 상태를 만들 수 있어 레이아웃이 안정적으로 유지된다.
        // ====================================================================

        [Header("서브 화면")]
        [Tooltip("사운드 설정 서브 화면 CanvasGroup (볼륨 슬라이더 포함).")]
        [SerializeField] private CanvasGroup _soundSubView;

        [Tooltip("프로필 서브 화면 CanvasGroup (기존 ProfileView 내용).")]
        [SerializeField] private CanvasGroup _profileSubView;

        // ====================================================================
        // Inspector 참조 — 볼륨 슬라이더
        // ====================================================================

        [Header("볼륨 슬라이더")]
        [Tooltip("마스터 볼륨 슬라이더 (0~1).")]
        [SerializeField] private Slider _masterSlider;

        [Tooltip("BGM 볼륨 슬라이더 (0~1).")]
        [SerializeField] private Slider _bgmSlider;

        [Tooltip("SFX 볼륨 슬라이더 (0~1).")]
        [SerializeField] private Slider _sfxSlider;

        [Header("볼륨 퍼센트 텍스트 (선택)")]
        [Tooltip("마스터 볼륨 퍼센트 표시 텍스트. 슬라이더 값에 따라 '100%' 형태로 갱신된다.")]
        [SerializeField] private TextMeshProUGUI _masterValueText;

        [Tooltip("BGM 볼륨 퍼센트 표시 텍스트.")]
        [SerializeField] private TextMeshProUGUI _bgmValueText;

        [Tooltip("SFX 볼륨 퍼센트 표시 텍스트.")]
        [SerializeField] private TextMeshProUGUI _sfxValueText;

        // ====================================================================
        // Inspector 참조 — VolumeButtonContainer (UI 규칙 2)
        //   로비 설정 탭은 탭 구조상 뒤로 버튼이 별도(_backButton)이므로
        //   VolumeButtonContainer에는 뒤로 버튼을 포함하지 않는다 (규칙 2).
        // ====================================================================

        [Header("VolumeButtonContainer")]
        [Tooltip("전체 소리켜기(언뮤트) 버튼. 뮤트 상태일 때만 표출된다 (규칙 4).")]
        [SerializeField] private Button _onButton;

        [Tooltip("전체 음소거(뮤트) 버튼. 언뮤트 상태일 때만 표출된다 (규칙 4).")]
        [SerializeField] private Button _offButton;

        [Tooltip("초기화 버튼. 슬라이더 3종을 모두 100%로 되돌린다 (규칙 7).")]
        [SerializeField] private Button _resetButton;

        // ====================================================================
        // Inspector 참조 — 슬라이더 색상 제어 대상 (UI 규칙 3 — Fill + Background)
        // ====================================================================

        [Header("슬라이더 색상 - Fill")]
        [Tooltip("마스터 슬라이더 Fill 이미지.")]
        [SerializeField] private Image _masterFill;

        [Tooltip("BGM 슬라이더 Fill 이미지.")]
        [SerializeField] private Image _bgmFill;

        [Tooltip("SFX 슬라이더 Fill 이미지.")]
        [SerializeField] private Image _sfxFill;

        [Header("슬라이더 색상 - Background")]
        [Tooltip("마스터 슬라이더 트랙 배경 이미지.")]
        [SerializeField] private Image _masterBackground;

        [Tooltip("BGM 슬라이더 트랙 배경 이미지.")]
        [SerializeField] private Image _bgmBackground;

        [Tooltip("SFX 슬라이더 트랙 배경 이미지.")]
        [SerializeField] private Image _sfxBackground;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        /// <summary>
        /// 컴포넌트가 활성화되는 시점에 초기화한다.
        /// 탭 전환 시 매번 재진입할 수 있으므로 Initialize는 리스너 중복 등록을 방지한다.
        /// </summary>
        private void Awake()
        {
            Initialize();
        }

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 버튼 리스너 등록 + 볼륨 슬라이더 초기값/리스너 설정 + 시작 시 메인 화면 표시.
        /// RemoveAllListeners 후 재등록하여 중복 호출을 방지한다(재진입 안전).
        /// </summary>
        public void Initialize()
        {
            // 메인 버튼 → 서브 화면 진입.
            if (_soundButton != null)
            {
                _soundButton.onClick.RemoveAllListeners();
                _soundButton.onClick.AddListener(() => ShowSubView(_soundSubView));
            }
            if (_profileButton != null)
            {
                _profileButton.onClick.RemoveAllListeners();
                _profileButton.onClick.AddListener(() => ShowSubView(_profileSubView));
            }

            // 뒤로가기 → 메인 화면 복귀.
            if (_backButton != null)
            {
                _backButton.onClick.RemoveAllListeners();
                _backButton.onClick.AddListener(OnBackClicked);
            }

            // 전체 소리켜기(언뮤트) 버튼 → AudioManager에 언뮤트 요청 후 화면 갱신 (규칙 4, 6).
            if (_onButton != null)
            {
                _onButton.onClick.RemoveAllListeners();
                _onButton.onClick.AddListener(OnUnmuteClicked);
            }

            // 전체 음소거(뮤트) 버튼 → AudioManager에 뮤트 요청 후 화면 갱신 (규칙 4, 6).
            if (_offButton != null)
            {
                _offButton.onClick.RemoveAllListeners();
                _offButton.onClick.AddListener(OnMuteClicked);
            }

            // 초기화 버튼 → 슬라이더 3종을 100%로 리셋 (규칙 7).
            if (_resetButton != null)
            {
                _resetButton.onClick.RemoveAllListeners();
                _resetButton.onClick.AddListener(OnResetClicked);
            }

            // 볼륨 슬라이더 초기값/리스너 설정.
            SetupVolumeSliders();

            // 시작은 항상 메인 화면.
            ShowMain();

            // 뮤트 상태에 따른 슬라이더 색·토글 버튼 표출을 초기 반영한다.
            RefreshMuteVisuals();
        }

        /// <summary>
        /// 볼륨 슬라이더 3종의 초기값을 AudioManager에서 읽어 반영하고,
        /// 값 변경 시 AudioManager에 즉시 전달 + 퍼센트 텍스트를 갱신하는 리스너를 등록한다.
        /// AudioManager.Instance가 null일 수 있으므로 ?. 와 ?? 1f로 안전 처리한다(규칙 5).
        /// </summary>
        private void SetupVolumeSliders()
        {
            SetupOneSlider(
                _masterSlider, _masterValueText,
                AudioManager.Instance?.GetMasterVolume() ?? 1f,
                v => AudioManager.Instance?.SetMasterVolume(v));

            SetupOneSlider(
                _bgmSlider, _bgmValueText,
                AudioManager.Instance?.GetBgmVolume() ?? 1f,
                v => AudioManager.Instance?.SetBgmVolume(v));

            SetupOneSlider(
                _sfxSlider, _sfxValueText,
                AudioManager.Instance?.GetSfxVolume() ?? 1f,
                v => AudioManager.Instance?.SetSfxVolume(v));
        }

        /// <summary>
        /// 슬라이더 한 개의 초기값/리스너/퍼센트 텍스트를 설정하는 공통 헬퍼.
        /// </summary>
        /// <param name="slider">대상 슬라이더 (null 허용).</param>
        /// <param name="valueText">퍼센트 표시 텍스트 (null 허용).</param>
        /// <param name="initialValue">초기 슬라이더 값 (0~1).</param>
        /// <param name="onChanged">값 변경 시 AudioManager에 전달할 콜백.</param>
        private void SetupOneSlider(Slider slider, TextMeshProUGUI valueText,
            float initialValue, System.Action<float> onChanged)
        {
            if (slider == null) return;

            slider.onValueChanged.RemoveAllListeners();
            slider.value = initialValue;
            UpdateValueText(valueText, initialValue);

            slider.onValueChanged.AddListener(v =>
            {
                onChanged?.Invoke(v);
                UpdateValueText(valueText, v);
                // 슬라이더 조작은 자동 언뮤트를 유발할 수 있으므로(규칙 26),
                // 값 반영 직후 슬라이더 색·토글 버튼 표출을 다시 갱신한다.
                RefreshMuteVisuals();
            });
        }

        /// <summary>
        /// 0~1 슬라이더 값을 "100%" 형태의 정수 퍼센트 문자열로 텍스트에 반영한다.
        /// </summary>
        /// <param name="valueText">갱신할 텍스트 (null이면 무시).</param>
        /// <param name="value">0~1 볼륨 값.</param>
        private void UpdateValueText(TextMeshProUGUI valueText, float value)
        {
            if (valueText == null) return;
            valueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        // ====================================================================
        // 화면 전환
        // ====================================================================

        /// <summary>
        /// 메인 화면을 표시한다. 서브 화면 컨테이너와 뒤로가기 버튼은 숨긴다.
        /// </summary>
        public void ShowMain()
        {
            SetGroupVisible(_mainView, true);
            SetGroupVisible(_subViewContainer, false);

            // 모든 서브 화면을 CanvasGroup으로 숨긴다(서로 겹치지 않도록).
            // SetActive(false) 대신 alpha=0으로 처리하여 레이아웃이 흔들리지 않게 한다(규칙 5).
            SetGroupVisible(_soundSubView, false);
            SetGroupVisible(_profileSubView, false);

            // 메인 화면에서는 뒤로가기 버튼 숨김.
            SetBackButtonVisible(false);
        }

        /// <summary>
        /// 지정한 서브 화면을 표시한다. 메인 화면은 숨기고 뒤로가기 버튼을 표시한다.
        /// 사운드 서브 화면 진입 시에는 다른 씬에서 변경됐을 수 있는 볼륨을 슬라이더에 다시 반영한다.
        /// </summary>
        /// <param name="subView">표시할 서브 화면 CanvasGroup. null이면 무시.</param>
        public void ShowSubView(CanvasGroup subView)
        {
            if (subView == null) return;

            SetGroupVisible(_mainView, false);
            SetGroupVisible(_subViewContainer, true);

            // 요청한 서브 화면만 CanvasGroup으로 표시하고 나머지는 숨긴다.
            // SetActive 대신 alpha 제어를 사용해 레이아웃을 유지한다(규칙 5).
            SetGroupVisible(_soundSubView, subView == _soundSubView);
            SetGroupVisible(_profileSubView, subView == _profileSubView);

            // 사운드 화면이면 현재 저장된 볼륨으로 슬라이더 값을 다시 맞추고,
            // 뮤트 시각 상태(슬라이더 색·토글 버튼)도 함께 갱신한다.
            if (subView == _soundSubView)
            {
                RefreshVolumeSliderValues();
                RefreshMuteVisuals();
            }

            // 서브 화면에서는 좌측 상단 뒤로가기 버튼 표시.
            SetBackButtonVisible(true);
        }

        /// <summary>
        /// 뒤로가기 버튼 클릭 핸들러. 메인 화면으로 복귀한다.
        /// </summary>
        private void OnBackClicked()
        {
            ShowMain();
        }

        /// <summary>
        /// 슬라이더 값만 현재 저장 볼륨으로 다시 맞춘다(리스너 재등록 없음).
        /// onValueChanged가 다시 호출되더라도 동일 값이라 부작용은 없으며, 퍼센트 텍스트도 함께 갱신된다.
        /// </summary>
        private void RefreshVolumeSliderValues()
        {
            if (_masterSlider != null)
            {
                _masterSlider.value = AudioManager.Instance?.GetMasterVolume() ?? 1f;
                UpdateValueText(_masterValueText, _masterSlider.value);
            }
            if (_bgmSlider != null)
            {
                _bgmSlider.value = AudioManager.Instance?.GetBgmVolume() ?? 1f;
                UpdateValueText(_bgmValueText, _bgmSlider.value);
            }
            if (_sfxSlider != null)
            {
                _sfxSlider.value = AudioManager.Instance?.GetSfxVolume() ?? 1f;
                UpdateValueText(_sfxValueText, _sfxSlider.value);
            }
        }

        // ====================================================================
        // 뮤트 / 초기화 (VolumeButtonContainer — UI 규칙 4·5·7, Sound 규칙 23·26·27)
        // ====================================================================

        /// <summary>
        /// '전체 소리켜기' 버튼 클릭 핸들러. AudioManager에 언뮤트를 요청하고 화면 상태를 갱신한다.
        /// UI는 AudioMixer/PlayerPrefs를 직접 만지지 않고 SetMuted(false) 한 줄만 호출한다 (규칙 6).
        /// </summary>
        private void OnUnmuteClicked()
        {
            AudioManager.Instance?.SetMuted(false);
            RefreshMuteVisuals();
        }

        /// <summary>
        /// '전체 음소거' 버튼 클릭 핸들러. AudioManager에 뮤트를 요청하고 화면 상태를 갱신한다.
        /// </summary>
        private void OnMuteClicked()
        {
            AudioManager.Instance?.SetMuted(true);
            RefreshMuteVisuals();
        }

        /// <summary>
        /// 초기화 버튼 클릭 핸들러 (규칙 7, Sound 규칙 27).
        /// 슬라이더 3종의 값을 1.0으로 세팅하면 기존 onValueChanged 리스너가
        /// AudioManager.SetXxxVolume(1)을 호출하고(→ 자동 언뮤트), 퍼센트 텍스트와
        /// 뮤트 시각 상태(RefreshMuteVisuals)까지 리스너 안에서 함께 갱신된다.
        /// </summary>
        private void OnResetClicked()
        {
            // 슬라이더 값 세팅 → 각 슬라이더의 onValueChanged가 볼륨 적용/텍스트/색상 갱신을 처리.
            if (_masterSlider != null) _masterSlider.value = 1f;
            if (_bgmSlider != null)    _bgmSlider.value    = 1f;
            if (_sfxSlider != null)    _sfxSlider.value    = 1f;

            // 슬라이더가 하나도 연결되지 않은 예외 상황에서도 화면 상태는 맞춰둔다.
            RefreshMuteVisuals();
        }

        /// <summary>
        /// 현재 뮤트 상태에 따라 슬라이더 색(초록/빨강)과 On/Off 토글 버튼 표출을 갱신한다
        /// (규칙 3·4·5). AudioManager.Instance가 null이면 언뮤트(소리 켜짐)로 간주한다 (규칙 5).
        /// </summary>
        private void RefreshMuteVisuals()
        {
            bool muted = AudioManager.Instance?.IsMuted() ?? false;

            // 슬라이더 Fill + Background 색을 동시에 갱신 (규칙 3).
            Color color = muted ? ColorSoundOff : ColorSoundOn;
            ApplyImageColor(_masterFill, color);
            ApplyImageColor(_bgmFill, color);
            ApplyImageColor(_sfxFill, color);
            ApplyImageColor(_masterBackground, color);
            ApplyImageColor(_bgmBackground, color);
            ApplyImageColor(_sfxBackground, color);

            // 토글 버튼 표출 (규칙 4): 뮤트면 '전체 소리켜기'만, 언뮤트면 '전체 음소거'만 표출한다.
            SetButtonVisible(_onButton, muted);
            SetButtonVisible(_offButton, !muted);
        }

        /// <summary> Image가 연결돼 있으면 지정 색을 적용한다(null이면 무시). </summary>
        /// <param name="image">대상 Image (null 허용).</param>
        /// <param name="color">적용할 색.</param>
        private static void ApplyImageColor(Image image, Color color)
        {
            if (image != null)
                image.color = color;
        }

        /// <summary>
        /// 버튼을 표시/숨김 처리한다. SetActive 대신 CanvasGroup을 사용한다 (규칙 5).
        /// CanvasGroup은 에디터 셋업 스크립트가 각 버튼에 미리 부착한다.
        /// CanvasGroup이 없는 경우에도 최소한 상호작용은 막도록 interactable을 제어한다.
        /// </summary>
        /// <param name="button">대상 버튼 (null 허용).</param>
        /// <param name="visible">true=표시, false=숨김.</param>
        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button == null) return;

            CanvasGroup cg = button.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha          = visible ? 1f : 0f;
                cg.blocksRaycasts = visible;
                cg.interactable   = visible;
            }
            else
            {
                // CanvasGroup 미부착 폴백 — 규칙 5 준수를 위해 에디터에서 CanvasGroup 부착 권장.
                button.interactable = visible;
            }
        }

        // ====================================================================
        // CanvasGroup 표시/숨김 헬퍼 (GameSystemRules_UI 규칙 5)
        // ====================================================================

        /// <summary>
        /// CanvasGroup을 표시/숨김 처리한다(alpha + blocksRaycasts + interactable 세트).
        /// </summary>
        /// <param name="group">대상 CanvasGroup (null이면 무시).</param>
        /// <param name="visible">true=표시, false=숨김.</param>
        private static void SetGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        /// <summary>
        /// 뒤로가기 버튼을 표시/숨김 처리한다.
        /// GameObject.SetActive 대신 _backButtonGroup(CanvasGroup)의 alpha/raycast/interactable을
        /// 제어하여 규칙 5(CanvasGroup 숨김/표시 패턴)를 따른다.
        /// 숨김 상태(alpha=0, blocksRaycasts=false)에서는 화면에 보이지 않고 클릭도 받지 않는다.
        /// </summary>
        /// <param name="visible">true=표시, false=숨김.</param>
        private void SetBackButtonVisible(bool visible)
        {
            SetGroupVisible(_backButtonGroup, visible);
        }
    }
}
