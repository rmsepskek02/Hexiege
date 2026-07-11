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
        // [비활성화 — 테스트 통과 후 삭제 예정]
        //   프로필 기능은 별도 ProfilePanel(ProfileView.cs)로 완전히 분리되었으므로
        //   (GameSystemRules_UI 로비 규칙 1) SettingPanel에는 더 이상 프로필 버튼이 없다.
        // [Tooltip("프로필 서브 화면으로 진입하는 버튼.")]
        // [SerializeField] private Button _profileButton;

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

        // [비활성화 — 테스트 통과 후 삭제 예정]
        //   프로필 서브 화면은 별도 ProfilePanel로 분리되어 SettingPanel에는 존재하지 않는다.
        // [Tooltip("프로필 서브 화면 CanvasGroup (기존 ProfileView 내용).")]
        // [SerializeField] private CanvasGroup _profileSubView;

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
        // Inspector 참조 — 볼륨 컨트롤 버튼 (VolumeButtonContainer, 규칙 23~26)
        // ====================================================================

        [Header("볼륨 컨트롤 버튼")]
        [Tooltip("전체 소리켜기 버튼(음소거 해제). 음소거 상태일 때만 표시된다(규칙 24).")]
        [SerializeField] private Button _soundOnButton;

        [Tooltip("전체 음소거 버튼. 소리 켜짐 상태일 때만 표시된다(규칙 24).")]
        [SerializeField] private Button _muteButton;

        [Tooltip("초기화 버튼. 세 볼륨을 100%로 되돌리고 음소거를 해제한다(규칙 25).")]
        [SerializeField] private Button _resetButton;

        [Header("색상 설정")]
        [Tooltip("슬라이더 음소거 색상 토큰(규칙 26). Resources/Config/UIColorConfig 연결.")]
        [SerializeField] private Hexiege.Infrastructure.UIColorConfig _colorConfig;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 볼륨 슬라이더 3종 + 전체 소리켜기/음소거/초기화 버튼의 공통 로직을 담당하는 Binder.
        /// 인게임 설정 메뉴와 동일한 동작을 보장하기 위해 공통 클래스로 분리했다(규칙 22).
        /// </summary>
        private readonly VolumeControlBinder _volumeBinder = new VolumeControlBinder();

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
            // [비활성화 — 테스트 통과 후 삭제 예정] 프로필 버튼은 ProfilePanel로 분리되어 제거됨.
            // if (_profileButton != null)
            // {
            //     _profileButton.onClick.RemoveAllListeners();
            //     _profileButton.onClick.AddListener(() => ShowSubView(_profileSubView));
            // }

            // 뒤로가기 → 메인 화면 복귀.
            if (_backButton != null)
            {
                _backButton.onClick.RemoveAllListeners();
                _backButton.onClick.AddListener(OnBackClicked);
            }

            // 볼륨 슬라이더 + 전체 소리켜기/음소거/초기화 버튼을 공통 Binder에 위임(규칙 22~26).
            _volumeBinder.Bind(new VolumeControlBinder.Refs
            {
                MasterSlider = _masterSlider,
                BgmSlider = _bgmSlider,
                SfxSlider = _sfxSlider,
                MasterValueText = _masterValueText,
                BgmValueText = _bgmValueText,
                SfxValueText = _sfxValueText,
                SoundOnButton = _soundOnButton,
                MuteButton = _muteButton,
                ResetButton = _resetButton,
                ColorConfig = _colorConfig,
            });

            // 시작은 항상 메인 화면.
            ShowMain();
        }

        // ─────────────────────────────────────────────────────────────────
        // [비활성화 — 테스트 통과 후 삭제 예정]
        // 볼륨 슬라이더 초기화/리스너/퍼센트 텍스트 로직은 VolumeControlBinder로 이관되었다.
        // (인게임/로비 공통 동작 보장 — 규칙 22). 아래 구 헬퍼들은 더 이상 호출되지 않는다.
        //
        // private void SetupVolumeSliders() { ... }
        // private void SetupOneSlider(Slider slider, TextMeshProUGUI valueText,
        //     float initialValue, System.Action<float> onChanged) { ... }
        // private void UpdateValueText(TextMeshProUGUI valueText, float value) { ... }
        // ─────────────────────────────────────────────────────────────────

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
            // [비활성화 — 테스트 통과 후 삭제 예정] 프로필 서브 화면은 ProfilePanel로 분리됨.
            // SetGroupVisible(_profileSubView, false);

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
            // [비활성화 — 테스트 통과 후 삭제 예정] 프로필 서브 화면은 ProfilePanel로 분리됨.
            // SetGroupVisible(_profileSubView, subView == _profileSubView);

            // 사운드 화면이면 현재 저장된 볼륨/음소거로 슬라이더·버튼·색상을 다시 맞춘다.
            if (subView == _soundSubView)
                _volumeBinder.RefreshFromAudioManager();

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

        // ─────────────────────────────────────────────────────────────────
        // [비활성화 — 테스트 통과 후 삭제 예정]
        // 슬라이더 재동기화 로직은 VolumeControlBinder.RefreshFromAudioManager()로 이관되었다.
        //
        // private void RefreshVolumeSliderValues() { ... }
        // ─────────────────────────────────────────────────────────────────

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
