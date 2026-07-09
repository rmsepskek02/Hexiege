// ============================================================================
// VolumeControlBinder.cs
// 인게임(Game.unity)과 로비(Lobby.unity) 양쪽의 볼륨 조절 UI가 공유하는
// "볼륨 슬라이더 3종 + 전체 소리켜기/전체 음소거/초기화 버튼" 로직을 한 곳에 모은
// 순수 C# 클래스(MonoBehaviour 아님).
//
// 왜 별도 클래스로 빼는가?
//   볼륨 조절 UI는 인게임 설정 메뉴와 로비 설정 패널 두 곳에 동일하게 존재한다
//   (GameSystemRules_Sound 규칙 22). 두 곳이 완전히 같은 동작을 해야 하므로,
//   슬라이더/버튼 처리 로직을 각 View에 복붙하면 한쪽만 고쳐지는 실수가 생긴다.
//   이 Binder에 로직을 모으고, 각 View는 자기 씬의 UI 오브젝트(슬라이더/버튼)만
//   찾아서 Bind()로 넘겨주면 양쪽이 항상 같은 동작을 보장한다.
//
// 씬마다 하이러키가 다르므로(프리팹 공유 안 함), Binder는 UI 오브젝트를 직접 찾지 않고
// 각 View가 참조를 주입(Bind)하는 형태로 설계했다.
//
// 담당 동작 (GameSystemRules_Sound 규칙 23~26):
//   - 슬라이더 3종(Master/BGM/SFX)을 저장값으로 초기화하고, 조작 시 AudioManager에 반영.
//   - 슬라이더를 조작하면 음소거가 자동 해제된다(결정사항 3 — AudioManager 내부에서 처리).
//   - "전체 소리켜기"/"전체 음소거" 버튼은 상호 배타적으로 하나만 표시(규칙 24, CanvasGroup).
//   - "초기화" 버튼은 세 볼륨을 1.0으로 되돌리고 음소거를 해제(규칙 25).
//   - 슬라이더 Fill 색상으로 음소거 여부를 표시(규칙 26 — 켜짐=초록/음소거=빨강).
//
// null-safe: AudioManager.Instance가 null일 수 있으므로(개발 중 씬 직접 진입) 항상 ?. 사용.
//
// Presentation 레이어 — UnityEngine.UI(Slider/Button/Image) 참조는 Presentation이므로 허용.
// AudioManager(Presentation) + UIColorConfig(Infrastructure)에만 의존한다.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Infrastructure; // UIColorConfig

namespace Hexiege.Presentation
{
    /// <summary>
    /// 인게임/로비 볼륨 조절 UI가 공유하는 슬라이더+음소거 버튼 로직.
    /// 각 View가 자기 씬의 UI 참조를 Bind()로 주입하면, 이후 동작은 이 클래스가 전담한다.
    /// </summary>
    public class VolumeControlBinder
    {
        // ====================================================================
        // 주입받는 UI 참조 (Bind에서 채워짐)
        // ====================================================================

        private Slider _masterSlider;
        private Slider _bgmSlider;
        private Slider _sfxSlider;

        private TextMeshProUGUI _masterValueText;
        private TextMeshProUGUI _bgmValueText;
        private TextMeshProUGUI _sfxValueText;

        // "전체 소리켜기" 버튼 — 음소거 상태일 때만 표시된다(규칙 24). 누르면 음소거 해제.
        private Button _soundOnButton;
        // "전체 음소거" 버튼 — 소리 켜짐 상태일 때만 표시된다(규칙 24). 누르면 음소거.
        private Button _muteButton;
        // "초기화" 버튼 — 세 볼륨을 100%로 되돌리고 음소거 해제(규칙 25).
        private Button _resetButton;

        // 위 두 버튼의 표시/숨김을 CanvasGroup으로 처리하기 위한 캐시(규칙 24, 공통 UI 규칙 5).
        // Bind()에서 각 버튼 오브젝트로부터 확보(없으면 추가)한다.
        private CanvasGroup _soundOnGroup;
        private CanvasGroup _muteGroup;

        // 슬라이더 색상 토큰(규칙 26). Inspector 미연결 시 폴백 색상을 쓴다.
        private UIColorConfig _colorConfig;

        /// <summary> Bind가 완료되었는지 여부. 중복 Bind 방지 및 안전 가드용. </summary>
        private bool _bound;

        // ====================================================================
        // 주입 참조 묶음 구조체
        //   파라미터가 많아 가독성이 떨어지므로 구조체로 묶어 전달받는다.
        //   각 View는 이 구조체를 채워 Bind()에 넘긴다.
        // ====================================================================

        /// <summary>
        /// Binder에 주입할 UI 참조 묶음. 각 필드는 씬의 해당 UI 오브젝트를 가리킨다.
        /// 일부가 null이어도 Binder는 null-safe하게 동작한다(해당 요소만 무시).
        /// </summary>
        public struct Refs
        {
            /// <summary> 마스터 볼륨 슬라이더 (0~1). </summary>
            public Slider MasterSlider;
            /// <summary> BGM 볼륨 슬라이더 (0~1). </summary>
            public Slider BgmSlider;
            /// <summary> SFX 볼륨 슬라이더 (0~1). </summary>
            public Slider SfxSlider;

            /// <summary> 마스터 볼륨 퍼센트 텍스트 (선택). </summary>
            public TextMeshProUGUI MasterValueText;
            /// <summary> BGM 볼륨 퍼센트 텍스트 (선택). </summary>
            public TextMeshProUGUI BgmValueText;
            /// <summary> SFX 볼륨 퍼센트 텍스트 (선택). </summary>
            public TextMeshProUGUI SfxValueText;

            /// <summary> "전체 소리켜기" 버튼(음소거 해제). 규칙 23·24. </summary>
            public Button SoundOnButton;
            /// <summary> "전체 음소거" 버튼(음소거 설정). 규칙 23·24. </summary>
            public Button MuteButton;
            /// <summary> "초기화" 버튼(볼륨 100% + 음소거 해제). 규칙 25. </summary>
            public Button ResetButton;

            /// <summary> 슬라이더 색상 토큰(규칙 26). null이면 폴백 색상 사용. </summary>
            public UIColorConfig ColorConfig;
        }

        // ====================================================================
        // Bind — 참조 주입 + 리스너 등록 + 초기 상태 반영
        // ====================================================================

        /// <summary>
        /// 각 View가 자기 씬의 UI 참조를 넘겨 Binder를 초기화한다.
        /// 슬라이더 초기값/리스너 등록, 버튼 리스너 등록, 초기 음소거 표시/색상 반영까지 수행한다.
        /// 재진입(탭 재활성화 등)에 안전하도록 리스너는 RemoveAllListeners 후 재등록한다.
        /// </summary>
        /// <param name="refs">주입할 UI 참조 묶음.</param>
        public void Bind(Refs refs)
        {
            _masterSlider = refs.MasterSlider;
            _bgmSlider = refs.BgmSlider;
            _sfxSlider = refs.SfxSlider;

            _masterValueText = refs.MasterValueText;
            _bgmValueText = refs.BgmValueText;
            _sfxValueText = refs.SfxValueText;

            _soundOnButton = refs.SoundOnButton;
            _muteButton = refs.MuteButton;
            _resetButton = refs.ResetButton;
            _colorConfig = refs.ColorConfig;

            // 전체 소리켜기/전체 음소거 버튼의 CanvasGroup 확보(없으면 추가).
            //   규칙 24: 표시/숨김은 SetActive가 아닌 CanvasGroup으로 처리한다(공통 UI 규칙 5).
            _soundOnGroup = EnsureCanvasGroup(_soundOnButton);
            _muteGroup = EnsureCanvasGroup(_muteButton);

            // 슬라이더 3종 초기화 — 저장된 볼륨값을 슬라이더에 반영하고 조작 리스너를 등록한다.
            SetupSlider(_masterSlider, _masterValueText, GetSavedMaster(),
                v => AudioManager.Instance?.SetMasterVolume(v));
            SetupSlider(_bgmSlider, _bgmValueText, GetSavedBgm(),
                v => AudioManager.Instance?.SetBgmVolume(v));
            SetupSlider(_sfxSlider, _sfxValueText, GetSavedSfx(),
                v => AudioManager.Instance?.SetSfxVolume(v));

            // 버튼 리스너 등록(재진입 안전 — 기존 리스너 제거 후 재등록).
            if (_soundOnButton != null)
            {
                _soundOnButton.onClick.RemoveAllListeners();
                _soundOnButton.onClick.AddListener(OnSoundOnClicked);
            }
            if (_muteButton != null)
            {
                _muteButton.onClick.RemoveAllListeners();
                _muteButton.onClick.AddListener(OnMuteClicked);
            }
            if (_resetButton != null)
            {
                _resetButton.onClick.RemoveAllListeners();
                _resetButton.onClick.AddListener(OnResetClicked);
            }

            _bound = true;

            // 초기 음소거 표시(버튼 배타)/슬라이더 색상 반영.
            RefreshMuteVisuals();
        }

        // ====================================================================
        // 외부에서 호출 — 패널 표시 시 재동기화
        // ====================================================================

        /// <summary>
        /// 볼륨 조절 패널이 (다시) 표시될 때 호출한다.
        /// 다른 씬/위치에서 볼륨이나 음소거가 바뀌었을 수 있으므로,
        /// 슬라이더 값·퍼센트 텍스트·버튼 표시·슬라이더 색상을 저장된 최신 상태로 다시 맞춘다.
        ///
        /// 슬라이더 값을 SetValueWithoutNotify로 설정하여, 프로그램이 값을 바꾸는 것만으로는
        /// 조작 리스너(SetXxxVolume)가 발화하지 않도록 한다. 이렇게 하지 않으면 음소거 상태에서
        /// 패널을 여는 것만으로 슬라이더 값 설정 → 리스너 발화 → 자동 언뮤트가 일어나 버린다.
        /// </summary>
        public void RefreshFromAudioManager()
        {
            if (!_bound) return;

            SetSliderValueSilently(_masterSlider, _masterValueText, GetSavedMaster());
            SetSliderValueSilently(_bgmSlider, _bgmValueText, GetSavedBgm());
            SetSliderValueSilently(_sfxSlider, _sfxValueText, GetSavedSfx());

            RefreshMuteVisuals();
        }

        // ====================================================================
        // 버튼 핸들러
        // ====================================================================

        /// <summary> "전체 소리켜기" 클릭 → 음소거 해제(규칙 23). </summary>
        private void OnSoundOnClicked()
        {
            AudioManager.Instance?.SetMuted(false);
            RefreshMuteVisuals();
        }

        /// <summary> "전체 음소거" 클릭 → 음소거 설정(규칙 23). </summary>
        private void OnMuteClicked()
        {
            AudioManager.Instance?.SetMuted(true);
            RefreshMuteVisuals();
        }

        /// <summary>
        /// "초기화" 클릭 → 세 볼륨을 100%(1.0)로, 음소거 해제(규칙 25).
        /// AudioManager 저장값을 갱신한 뒤 슬라이더/텍스트/버튼/색상을 모두 다시 맞춘다.
        /// </summary>
        private void OnResetClicked()
        {
            AudioManager.Instance?.ResetAllVolumes();

            // 슬라이더 값을 1.0으로(리스너 발화 없이) 되돌리고 표시 상태 갱신.
            SetSliderValueSilently(_masterSlider, _masterValueText, 1f);
            SetSliderValueSilently(_bgmSlider, _bgmValueText, 1f);
            SetSliderValueSilently(_sfxSlider, _sfxValueText, 1f);

            RefreshMuteVisuals();
        }

        // ====================================================================
        // 음소거 표시/색상 갱신 (규칙 24, 26)
        // ====================================================================

        /// <summary>
        /// 현재 음소거 여부에 따라 두 버튼(전체 소리켜기/전체 음소거)의 상호 배타 표시와
        /// 슬라이더 Fill 색상을 갱신한다.
        ///   - 음소거 ON  : "전체 소리켜기" 표시 / 슬라이더 빨강
        ///   - 음소거 OFF : "전체 음소거" 표시 / 슬라이더 초록
        /// </summary>
        private void RefreshMuteVisuals()
        {
            // AudioManager가 없으면(씬 직접 진입) 음소거 아님으로 간주.
            bool muted = AudioManager.Instance?.IsMuted() ?? false;

            // 규칙 24: 음소거면 "전체 소리켜기"만, 아니면 "전체 음소거"만 표시(CanvasGroup 배타).
            SetGroupVisible(_soundOnGroup, muted);
            SetGroupVisible(_muteGroup, !muted);

            // 규칙 26: 슬라이더 Fill 색상 — 켜짐=초록, 음소거=빨강. (색상 토큰은 안전 가드로 참조)
            Color fillColor = muted
                ? (_colorConfig != null ? _colorConfig.soundMutedColor : Color.red)
                : (_colorConfig != null ? _colorConfig.soundOnColor : Color.green);

            ApplyFillColor(_masterSlider, fillColor);
            ApplyFillColor(_bgmSlider, fillColor);
            ApplyFillColor(_sfxSlider, fillColor);
        }

        // ====================================================================
        // 슬라이더 헬퍼
        // ====================================================================

        /// <summary>
        /// 슬라이더 한 개의 초기값/퍼센트 텍스트/조작 리스너를 설정한다.
        /// 초기값은 SetValueWithoutNotify로 넣어(리스너 등록 전) 초기화만으로 볼륨이 바뀌지 않게 한다.
        /// </summary>
        /// <param name="slider">대상 슬라이더 (null 허용).</param>
        /// <param name="valueText">퍼센트 표시 텍스트 (null 허용).</param>
        /// <param name="initialValue">초기 슬라이더 값 (0~1).</param>
        /// <param name="onChanged">사용자가 값을 조작할 때 AudioManager에 전달할 콜백.</param>
        private void SetupSlider(Slider slider, TextMeshProUGUI valueText,
            float initialValue, System.Action<float> onChanged)
        {
            if (slider == null) return;

            slider.onValueChanged.RemoveAllListeners();
            // 리스너 등록 전에 값을 넣어 초기화가 볼륨/음소거 상태를 건드리지 않게 한다.
            slider.SetValueWithoutNotify(initialValue);
            UpdateValueText(valueText, initialValue);

            slider.onValueChanged.AddListener(v =>
            {
                // 사용자가 슬라이더를 조작 → AudioManager에 반영(내부에서 음소거 자동 해제 — 결정사항 3).
                onChanged?.Invoke(v);
                UpdateValueText(valueText, v);
                // 음소거가 해제됐을 수 있으므로 버튼 표시/색상을 다시 맞춘다.
                RefreshMuteVisuals();
            });
        }

        /// <summary>
        /// 슬라이더 값을 리스너 발화 없이 설정하고 퍼센트 텍스트도 함께 갱신한다.
        /// (프로그램에 의한 값 변경이 조작 리스너를 발화시켜 부작용을 일으키는 것을 방지)
        /// </summary>
        /// <param name="slider">대상 슬라이더 (null 허용).</param>
        /// <param name="valueText">퍼센트 표시 텍스트 (null 허용).</param>
        /// <param name="value">설정할 값 (0~1).</param>
        private void SetSliderValueSilently(Slider slider, TextMeshProUGUI valueText, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
            UpdateValueText(valueText, value);
        }

        /// <summary>
        /// 0~1 슬라이더 값을 "100%" 형태 정수 퍼센트 문자열로 텍스트에 반영한다.
        /// </summary>
        /// <param name="valueText">갱신할 텍스트 (null이면 무시).</param>
        /// <param name="value">0~1 볼륨 값.</param>
        private void UpdateValueText(TextMeshProUGUI valueText, float value)
        {
            if (valueText == null) return;
            valueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        /// <summary>
        /// 슬라이더의 Fill 이미지 색상을 설정한다(규칙 26).
        /// Slider.fillRect(=Fill 오브젝트)에서 Image를 찾아 색을 바꾼다.
        /// </summary>
        /// <param name="slider">대상 슬라이더 (null 허용).</param>
        /// <param name="color">적용할 색상.</param>
        private void ApplyFillColor(Slider slider, Color color)
        {
            if (slider == null || slider.fillRect == null) return;

            // fillRect 오브젝트의 Image가 실제로 채워지는 막대 이미지다.
            if (slider.fillRect.TryGetComponent(out Image fillImage))
                fillImage.color = color;
        }

        // ====================================================================
        // CanvasGroup 헬퍼 (공통 UI 규칙 5)
        // ====================================================================

        /// <summary>
        /// 버튼 오브젝트에서 CanvasGroup을 확보한다. 없으면 추가한다.
        /// 규칙 24의 상호 배타 표시를 SetActive가 아닌 CanvasGroup으로 처리하기 위해 필요하다.
        /// </summary>
        /// <param name="button">대상 버튼 (null 허용).</param>
        /// <returns>확보한 CanvasGroup. button이 null이면 null.</returns>
        private static CanvasGroup EnsureCanvasGroup(Button button)
        {
            if (button == null) return null;

            if (!button.TryGetComponent(out CanvasGroup group))
                group = button.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

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

        // ====================================================================
        // 저장 볼륨 조회 (null-safe)
        // ====================================================================

        /// <summary> 저장된 마스터 볼륨. AudioManager 없으면 1.0. </summary>
        private float GetSavedMaster() => AudioManager.Instance?.GetMasterVolume() ?? 1f;

        /// <summary> 저장된 BGM 볼륨. AudioManager 없으면 1.0. </summary>
        private float GetSavedBgm() => AudioManager.Instance?.GetBgmVolume() ?? 1f;

        /// <summary> 저장된 SFX 볼륨. AudioManager 없으면 1.0. </summary>
        private float GetSavedSfx() => AudioManager.Instance?.GetSfxVolume() ?? 1f;
    }
}
