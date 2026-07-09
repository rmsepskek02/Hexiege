// ============================================================================
// InGameSettingsUI.cs
// 인게임 중 우상단 설정 버튼을 통해 열리는 일시정지 + 옵션 + 포기 메뉴.
//
// 핵심 동작:
//   - Show() 호출 시 SharedBackgroundButton에 닫기 콜백 등록 → 바깥 클릭으로 닫힘
//   - 싱글플레이에서는 Time.timeScale=0으로 게임 일시정지.
//     (AnimatedPanel.SetUpdate(true)가 적용되어 timeScale=0에서도 페이드 애니메이션이 동작)
//   - 멀티플레이에서는 일시정지 불가 — 다른 플레이어의 진행이 멈출 수 없으므로
//     timeScale을 건드리지 않는다.
//   - 포기 버튼 클릭 시 UIManager.ShowConfirm()으로 사용자 의사 재확인.
//   - 포기 확정 시:
//       싱글: GameEndUseCase.Forfeit() — Red 승리 처리.
//       멀티: NetworkGameEndController.RequestForfeit() — 서버가 자기 팀을 패배 처리.
//
// IGameUI 콜백:
//   - OnGameStarted(): 재경기 시 이전 게임의 설정 메뉴가 잔류하지 않도록 Hide().
//   - OnGameEnded(): 게임 종료 결과창이 뜨기 전 설정 메뉴를 닫는다.
//                    Hide()가 timeScale=1을 복원하지만, 그 직후 GameEndUI가
//                    timeScale=0을 재설정하므로 충돌하지 않음.
//
// Presentation 레이어 — MonoBehaviour 의존.
// ============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 인게임 설정 팝업. 사운드/포기 버튼 + 외부 클릭으로 닫기를 지원하며,
    /// 싱글플레이에서는 표시 중 timeScale=0으로 게임을 멈춘다.
    /// </summary>
    public class InGameSettingsUI : MonoBehaviour, IGameUI
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("팝업 컴포넌트")]
        [Tooltip("팝업 박스 본체에 부착된 AnimatedPanel (PopupFade 권장).")]
        [SerializeField] private AnimatedPanel _panel;

        // UIManager가 단일 BlockingOverlay를 소유하여 SafeArea 문제 없이 전체화면 커버.
        //   테스트 통과 후 삭제 예정.
        // [Tooltip("Canvas 직속 공유 Background. 등록된 콜백을 통해 외부 클릭 시 팝업이 닫힘.")]
        // [SerializeField] private SharedBackgroundButton _sharedBackground;

        [Header("버튼")]
        [Tooltip("팝업 우측 상단의 X 닫기 버튼.")]
        [SerializeField] private Button _closeButton;

        [Tooltip("사운드 옵션 버튼. 클릭 시 볼륨 조절 패널을 토글한다.")]
        [SerializeField] private Button _soundButton;

        [Tooltip("게임 포기 버튼. 클릭 시 ConfirmPopup으로 재확인.")]
        [SerializeField] private Button _forfeitButton;

        [Header("메인 버튼 그룹")]
        [Tooltip("사운드/포기 등 메인 버튼을 묶은 컨테이너의 CanvasGroup. " +
                 "볼륨 사이드바를 열면 이 그룹을 숨기고, 뒤로가기 시 다시 표시한다 (UI 규칙 5).")]
        [SerializeField] private CanvasGroup _mainButtonContainer;

        [Header("볼륨 패널")]
        [Tooltip("볼륨 슬라이더를 담은 패널의 CanvasGroup. Show/Hide를 CanvasGroup으로 처리 (UI 규칙 5).")]
        [SerializeField] private CanvasGroup _volumePanelGroup;

        [Tooltip("볼륨 사이드바 → 메인 버튼 그룹으로 복귀하는 뒤로가기 버튼.")]
        [SerializeField] private Button _backButton;

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

        [Header("볼륨 컨트롤 버튼 (VolumeButtonContainer)")]
        [Tooltip("전체 소리켜기 버튼(음소거 해제). 음소거 상태일 때만 표시된다(규칙 24).")]
        [SerializeField] private Button _soundOnButton;

        [Tooltip("전체 음소거 버튼. 소리 켜짐 상태일 때만 표시된다(규칙 24).")]
        [SerializeField] private Button _muteButton;

        [Tooltip("초기화 버튼. 세 볼륨을 100%로 되돌리고 음소거를 해제한다(규칙 25).")]
        [SerializeField] private Button _resetButton;

        [Header("색상 설정")]
        [Tooltip("슬라이더 음소거 색상 토큰(규칙 26). Resources/Config/UIColorConfig 연결.")]
        [SerializeField] private UIColorConfig _colorConfig;

        [Header("프로필 서브 패널 (규칙 6)")]
        [Tooltip("메인 버튼 그룹의 프로필 버튼. 클릭 시 프로필 서브 패널로 전환한다.")]
        [SerializeField] private Button _profileButton;

        [Tooltip("프로필 서브 패널의 CanvasGroup. 사운드 버튼과 동일한 열기/닫기 패턴으로 토글한다. " +
                 "내부 콘텐츠는 이번 범위 밖(빈 상태 토글만).")]
        [SerializeField] private CanvasGroup _profileSubViewGroup;

        [Tooltip("프로필 서브 패널 → 메인 버튼 그룹으로 복귀하는 뒤로가기 버튼.")]
        [SerializeField] private Button _profileBackButton;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 볼륨 슬라이더 3종 + 전체 소리켜기/음소거/초기화 버튼의 공통 로직을 담당하는 Binder.
        /// 로비 설정 패널과 동일한 동작을 보장하기 위해 공통 클래스로 분리했다(규칙 22).
        /// </summary>
        private readonly VolumeControlBinder _volumeBinder = new VolumeControlBinder();

        /// <summary>
        /// 이 스크립트가 Time.timeScale을 0으로 설정한 상태인지 추적.
        /// Hide() 시 이 플래그가 true일 때만 복원하여,
        /// 다른 시스템이 만들어 둔 일시정지 상태를 임의로 풀지 않도록 한다.
        /// </summary>
        private bool _pausedBySettings;

        /// <summary>
        /// 싱글플레이 포기 시 호출할 UseCase.
        /// GameBootstrapper.LoadMap()에서 Initialize()로 주입된다.
        /// 멀티플레이에서는 사용하지 않음 — 대신 _forfeitService(NetworkGameEndController 구현)를 거친다.
        /// </summary>
        private GameEndUseCase _gameEndUseCase;

        /// <summary>
        /// 포기 요청을 위임할 서비스. 싱글/멀티 분기 없이 RequestForfeit() 한 번 호출하면 된다.
        /// GameBootstrapper.LoadMap()에서 적합한 구현체를 주입한다.
        /// </summary>
        private IForfeitService _forfeitService;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper.LoadMap()에서 호출.
        /// UseCase + 포기 서비스 주입 + 버튼 리스너 등록 + 초기 상태 보장.
        /// </summary>
        /// <param name="gameEndUseCase">싱글플레이 포기 처리에 사용할 UseCase.</param>
        /// <param name="forfeitService">포기 요청을 위임할 서비스. 싱글=GameEndUseCase, 멀티=NetworkGameEndController.</param>
        public void Initialize(GameEndUseCase gameEndUseCase, IForfeitService forfeitService = null)
        {
            _gameEndUseCase = gameEndUseCase;
            _forfeitService = forfeitService;

            // 버튼 리스너 등록 — RemoveAllListeners 후 재등록으로 중복 호출 방지.
            // (Initialize는 재경기 시 다시 호출될 수 있음)
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(Hide);
            }
            if (_forfeitButton != null)
            {
                _forfeitButton.onClick.RemoveAllListeners();
                _forfeitButton.onClick.AddListener(OnForfeitClicked);
            }

            // 사운드 버튼 → 볼륨 사이드바 표시(메인 버튼 그룹 숨김).
            if (_soundButton != null)
            {
                _soundButton.onClick.RemoveAllListeners();
                _soundButton.onClick.AddListener(OnSoundButtonClicked);
            }

            // 뒤로가기 버튼 → 볼륨 사이드바 숨김(메인 버튼 그룹 복원).
            if (_backButton != null)
            {
                _backButton.onClick.RemoveAllListeners();
                _backButton.onClick.AddListener(HideVolumePanel);
            }

            // 프로필 버튼 → 프로필 서브 패널 표시(사운드 버튼과 동일한 패턴 — 규칙 6).
            if (_profileButton != null)
            {
                _profileButton.onClick.RemoveAllListeners();
                _profileButton.onClick.AddListener(ShowProfilePanel);
            }

            // 프로필 서브 패널 뒤로가기 → 메인 버튼 그룹 복원.
            if (_profileBackButton != null)
            {
                _profileBackButton.onClick.RemoveAllListeners();
                _profileBackButton.onClick.AddListener(HideProfilePanel);
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

            // 볼륨 패널/프로필 패널은 시작 시 숨김.
            HideVolumePanel();
            HideProfilePanel();

            // 초기 상태는 반드시 숨김으로 시작
            Hide();
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
        // 표시 / 숨김
        // ====================================================================

        /// <summary>
        /// 설정 팝업을 표시.
        /// - 싱글플레이: Time.timeScale=0으로 일시정지.
        /// - 멀티플레이: 다른 플레이어가 멈춰서는 안 되므로 timeScale 건드리지 않음.
        /// - SharedBackground에 Hide 콜백을 등록하여 바깥 클릭으로 닫을 수 있게 함.
        /// </summary>
        public void Show()
        {
            // 싱글플레이만 일시정지 — 멀티플레이는 절대 timeScale을 0으로 설정하지 않음.
            // NetworkContext.IsNetworkActive == false → 싱글플레이.
            if (!NetworkContext.IsNetworkActive)
            {
                Time.timeScale = 0f;
                _pausedBySettings = true;
            }

            // UIManager의 공유 BlockingOverlay를 Popup 모드로 표시.
            //   Popup 모드: 오버레이를 터치하면 Hide()가 호출되어 팝업이 닫힘.
            //   (규칙 8: Popup 타입은 배경 탭으로 닫힘)
            UIManager.Instance?.ShowBlockingOverlay(Hide);
            // [구로직 — 테스트 통과 후 삭제]
            // if (_sharedBackground != null)
            //     _sharedBackground.Register(Hide);

            // 팝업 본체 페이드 인.
            // AnimatedPanel은 CanvasGroup으로 가시성을 제어하므로 오브젝트는 항상 active 상태.
            if (_panel != null)
                _panel.Show();
        }

        /// <summary>
        /// 설정 팝업을 닫는다. 열려 있던 확인 팝업도 함께 닫고, 일시정지를 복원한다.
        /// 중복 호출에 안전 — 이미 닫힌 상태라면 AnimatedPanel.Hide()가 조용히 무시.
        /// </summary>
        public void Hide()
        {
            // UIManager 공유 BlockingOverlay 숨김(참조 카운터 -1).
            UIManager.Instance?.HideBlockingOverlay();
            // [구로직 — 테스트 통과 후 삭제]
            // if (_sharedBackground != null)
            //     _sharedBackground.Unregister();

            // 이 스크립트가 일시정지를 걸어둔 경우에만 복원.
            // (멀티플레이에서는 _pausedBySettings가 false이므로 건드리지 않음)
            if (_pausedBySettings)
            {
                Time.timeScale = 1f;
                _pausedBySettings = false;
            }

            // 볼륨 패널/프로필 패널도 함께 닫는다 — 설정 메뉴가 닫히면 하위 패널이 잔류하지 않도록.
            HideVolumePanel();
            HideProfilePanel();

            // 팝업 본체 페이드 아웃.
            if (_panel != null)
                _panel.Hide();
        }

        // ====================================================================
        // 볼륨 패널 토글 (UI 규칙 5 — CanvasGroup으로 표시/숨김)
        // ====================================================================

        /// <summary>
        /// 사운드 버튼 클릭 핸들러. 볼륨 패널이 보이면 숨기고, 숨겨져 있으면 표시한다.
        /// CanvasGroup.alpha로 현재 표시 여부를 판단한다.
        /// </summary>
        private void OnSoundButtonClicked()
        {
            if (_volumePanelGroup == null) return;

            // alpha가 0.5 이상이면 표시 중으로 간주 → 숨김, 아니면 표시.
            if (_volumePanelGroup.alpha >= 0.5f)
                HideVolumePanel();
            else
                ShowVolumePanel();
        }

        /// <summary>
        /// 볼륨 패널을 표시한다 (CanvasGroup alpha=1 + 입력 활성화).
        /// 표시 직전 슬라이더 값을 현재 저장값과 동기화한다.
        /// </summary>
        private void ShowVolumePanel()
        {
            if (_volumePanelGroup == null) return;

            // 다른 씬(로비 등)에서 볼륨/음소거가 바뀌었을 수 있으므로 표시 시점에 슬라이더/버튼/색상을 다시 동기화.
            _volumeBinder.RefreshFromAudioManager();

            _volumePanelGroup.alpha = 1f;
            _volumePanelGroup.interactable = true;
            _volumePanelGroup.blocksRaycasts = true;

            // 메인 버튼 그룹(사운드/포기)을 숨긴다 — 볼륨 패널과 동시에 표시되지 않도록 (UI 규칙 5).
            if (_mainButtonContainer != null)
            {
                _mainButtonContainer.alpha = 0f;
                _mainButtonContainer.interactable = false;
                _mainButtonContainer.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 볼륨 패널을 숨긴다 (CanvasGroup alpha=0 + 입력 비활성화).
        /// </summary>
        private void HideVolumePanel()
        {
            if (_volumePanelGroup == null) return;

            _volumePanelGroup.alpha = 0f;
            _volumePanelGroup.interactable = false;
            _volumePanelGroup.blocksRaycasts = false;

            // 볼륨 패널이 닫히면 메인 버튼 그룹(사운드/포기)을 다시 표시한다.
            if (_mainButtonContainer != null)
            {
                _mainButtonContainer.alpha = 1f;
                _mainButtonContainer.interactable = true;
                _mainButtonContainer.blocksRaycasts = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // [비활성화 — 테스트 통과 후 삭제 예정]
        // 슬라이더 재동기화 로직은 VolumeControlBinder.RefreshFromAudioManager()로 이관되었다.
        //
        // private void RefreshVolumeSliderValues() { ... }
        // ─────────────────────────────────────────────────────────────────

        // ====================================================================
        // 프로필 서브 패널 토글 (규칙 6 — 사운드 버튼과 동일한 CanvasGroup 패턴)
        // ====================================================================

        /// <summary>
        /// 프로필 서브 패널을 표시한다 (CanvasGroup alpha=1 + 입력 활성화).
        /// 메인 버튼 그룹(사운드/프로필/포기)은 숨겨 서로 겹치지 않게 한다(규칙 5, 6).
        /// 내부 콘텐츠는 이번 범위 밖이며, 빈 패널을 열고 닫는 동작만 제공한다.
        /// </summary>
        private void ShowProfilePanel()
        {
            if (_profileSubViewGroup == null) return;

            _profileSubViewGroup.alpha = 1f;
            _profileSubViewGroup.interactable = true;
            _profileSubViewGroup.blocksRaycasts = true;

            // 메인 버튼 그룹 숨김 — 볼륨 패널과 동일한 방식(규칙 5).
            if (_mainButtonContainer != null)
            {
                _mainButtonContainer.alpha = 0f;
                _mainButtonContainer.interactable = false;
                _mainButtonContainer.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 프로필 서브 패널을 숨긴다 (CanvasGroup alpha=0 + 입력 비활성화).
        /// 숨김 시 메인 버튼 그룹을 다시 표시한다.
        /// </summary>
        private void HideProfilePanel()
        {
            if (_profileSubViewGroup == null) return;

            _profileSubViewGroup.alpha = 0f;
            _profileSubViewGroup.interactable = false;
            _profileSubViewGroup.blocksRaycasts = false;

            // 프로필 패널이 닫히면 메인 버튼 그룹을 다시 표시한다.
            // (단, 볼륨 패널이 열려 있는 상태에서 프로필을 닫는 경우는 없다 — 서로 배타적으로 열림)
            if (_mainButtonContainer != null)
            {
                _mainButtonContainer.alpha = 1f;
                _mainButtonContainer.interactable = true;
                _mainButtonContainer.blocksRaycasts = true;
            }
        }

        // ====================================================================
        // 포기 버튼 흐름
        // ====================================================================

        /// <summary>
        /// 포기 버튼 클릭 핸들러.
        /// 즉시 포기 처리하지 않고 ConfirmPopup으로 사용자 의사를 한 번 더 묻는다.
        /// </summary>
        private void OnForfeitClicked()
        {
            // UIManager가 null이면(씬 직접 진입 등) 팝업을 띄우지 않고 안전하게 무시한다.
            UIManager.Instance?.ShowConfirm(
                message: "정말 포기하시겠습니까?",
                onConfirm: OnForfeitConfirmed,
                onCancel: null,
                confirmLabel: "포기",
                cancelLabel: "취소");
        }

        /// <summary>
        /// 포기 확정 콜백.
        /// 싱글/멀티 모드 분기는 _forfeitService 구현체가 처리하므로 본 메서드는 단일 흐름.
        /// - 싱글: GameEndUseCase.RequestForfeit() → Red 승리로 즉시 게임 종료.
        /// - 멀티: NetworkGameEndController.RequestForfeit() → 서버가 자기 팀 패배 처리.
        ///
        /// _forfeitService가 주입되지 않은 경우의 폴백:
        ///   싱글이면 _gameEndUseCase.Forfeit() 직접 호출. 멀티이면 경고 로그 후 종료.
        /// </summary>
        private void OnForfeitConfirmed()
        {
            if (_forfeitService != null)
            {
                // 포기는 씬 전환 없이 같은 씬 안에서 GameEndUI를 표시하므로 로딩 인디케이터를 띄우지 않는다.
                // 로딩 인디케이터는 씬 전환(로비 복귀 등)에서만 사용한다(UI 규칙 L-2).
                _forfeitService.RequestForfeit();
            }
            else if (!NetworkContext.IsNetworkActive)
            {
                // 폴백: 싱글플레이는 _gameEndUseCase 직접 호출 (구주입 경로 호환)
                _gameEndUseCase?.Forfeit();
            }
            else
            {
                Debug.LogWarning("[InGameSettingsUI] IForfeitService가 주입되지 않아 멀티플레이 포기 요청을 보낼 수 없습니다.");
            }

            // 포기 처리는 비동기적이지만(특히 네트워크) UI는 즉시 닫는다.
            Hide();
        }

        // ====================================================================
        // IGameUI 구현
        // ====================================================================

        /// <summary>
        /// 게임 시작/재시작 시 호출. 이전 게임에서 떠 있던 설정 메뉴를 정리.
        /// </summary>
        public void OnGameStarted()
        {
            Hide();
        }

        /// <summary>
        /// 게임 종료 시 호출. 결과 UI가 뜨기 전 설정 메뉴를 닫는다.
        /// Hide() 내부에서 timeScale=1을 복원하지만, 직후 GameEndUI가
        /// 자기 표시 시점에 timeScale=0을 다시 설정하므로 충돌이 없음.
        /// </summary>
        public void OnGameEnded()
        {
            Hide();
        }
    }
}
