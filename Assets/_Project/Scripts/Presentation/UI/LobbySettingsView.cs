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
        // Inspector 참조 — 네비게이션
        // ====================================================================

        [Header("네비게이션")]
        [Tooltip("좌측 상단 뒤로가기 버튼. 서브 화면에서만 표시되며, 누르면 메인 화면으로 복귀한다.")]
        [SerializeField] private Button _backButton;

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
        // Inspector 참조 — 서브 화면 GameObject
        // ====================================================================

        [Header("서브 화면")]
        [Tooltip("사운드 설정 서브 화면 GameObject (볼륨 슬라이더 포함).")]
        [SerializeField] private GameObject _soundSubView;

        [Tooltip("프로필 서브 화면 GameObject (기존 ProfileView 내용).")]
        [SerializeField] private GameObject _profileSubView;

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

            // 볼륨 슬라이더 초기값/리스너 설정.
            SetupVolumeSliders();

            // 시작은 항상 메인 화면.
            ShowMain();
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

            // 모든 서브 화면 비활성(서로 겹치지 않도록).
            if (_soundSubView != null) _soundSubView.SetActive(false);
            if (_profileSubView != null) _profileSubView.SetActive(false);

            // 메인 화면에서는 뒤로가기 버튼 숨김.
            SetBackButtonVisible(false);
        }

        /// <summary>
        /// 지정한 서브 화면을 표시한다. 메인 화면은 숨기고 뒤로가기 버튼을 표시한다.
        /// 사운드 서브 화면 진입 시에는 다른 씬에서 변경됐을 수 있는 볼륨을 슬라이더에 다시 반영한다.
        /// </summary>
        /// <param name="subView">표시할 서브 화면 GameObject. null이면 무시.</param>
        public void ShowSubView(GameObject subView)
        {
            if (subView == null) return;

            SetGroupVisible(_mainView, false);
            SetGroupVisible(_subViewContainer, true);

            // 요청한 서브 화면만 켜고 나머지는 끈다.
            if (_soundSubView != null) _soundSubView.SetActive(subView == _soundSubView);
            if (_profileSubView != null) _profileSubView.SetActive(subView == _profileSubView);

            // 사운드 화면이면 현재 저장된 볼륨으로 슬라이더 값을 다시 맞춘다.
            if (subView == _soundSubView)
                RefreshVolumeSliderValues();

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
        /// 버튼은 단일 오브젝트이므로 GameObject.SetActive로 처리해도 레이아웃 영향이 없다
        /// (LayoutGroup 자식이 아닌 앵커 고정 배치이기 때문).
        /// </summary>
        /// <param name="visible">true=표시, false=숨김.</param>
        private void SetBackButtonVisible(bool visible)
        {
            if (_backButton == null) return;
            _backButton.gameObject.SetActive(visible);
        }
    }
}
