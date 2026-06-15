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
//   - 포기 버튼 클릭 시 ConfirmPopup으로 사용자 의사 재확인.
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

        [Tooltip("Canvas 직속 공유 Background. 등록된 콜백을 통해 외부 클릭 시 팝업이 닫힘.")]
        [SerializeField] private SharedBackgroundButton _sharedBackground;

        [Tooltip("포기 확정용 확인 팝업. 포기 버튼 클릭 시 이 팝업을 열어 사용자 의사 재확인.")]
        [SerializeField] private ConfirmPopup _confirmPopup;

        [Header("버튼")]
        [Tooltip("팝업 우측 상단의 X 닫기 버튼.")]
        [SerializeField] private Button _closeButton;

        [Tooltip("사운드 옵션 버튼. 클릭 시 볼륨 조절 패널을 토글한다.")]
        [SerializeField] private Button _soundButton;

        [Tooltip("게임 포기 버튼. 클릭 시 ConfirmPopup으로 재확인.")]
        [SerializeField] private Button _forfeitButton;

        [Header("볼륨 패널")]
        [Tooltip("볼륨 슬라이더를 담은 패널의 CanvasGroup. Show/Hide를 CanvasGroup으로 처리 (UI 규칙 5).")]
        [SerializeField] private CanvasGroup _volumePanelGroup;

        [Tooltip("마스터 볼륨 슬라이더 (0~1).")]
        [SerializeField] private Slider _masterSlider;

        [Tooltip("BGM 볼륨 슬라이더 (0~1).")]
        [SerializeField] private Slider _bgmSlider;

        [Tooltip("SFX 볼륨 슬라이더 (0~1).")]
        [SerializeField] private Slider _sfxSlider;

        // ====================================================================
        // 내부 상태
        // ====================================================================

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
        ///
        /// [2026-05-20] forfeitService 인자 추가:
        ///   기존: OnForfeitConfirmed에서 FindFirstObjectByType<NetworkGameEndController> 호출
        ///   변경: GameBootstrapper가 싱글/멀티 모드에 따라 적합한 IForfeitService 구현체를 주입
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

            // 사운드 버튼 → 볼륨 패널 토글.
            if (_soundButton != null)
            {
                _soundButton.onClick.RemoveAllListeners();
                _soundButton.onClick.AddListener(OnSoundButtonClicked);
            }

            // 볼륨 슬라이더 초기화 — 현재 저장된 볼륨을 슬라이더에 반영하고 변경 리스너를 연결한다.
            SetupVolumeSliders();

            // 볼륨 패널은 시작 시 숨김.
            HideVolumePanel();

            // 초기 상태는 반드시 숨김으로 시작
            Hide();
        }

        /// <summary>
        /// 볼륨 슬라이더 3종의 초기값을 AudioManager에서 읽어 반영하고,
        /// 값 변경 시 AudioManager에 즉시 전달하는 리스너를 등록한다.
        /// Initialize는 재경기 시 재호출될 수 있으므로 RemoveAllListeners로 중복 등록을 방지한다.
        /// AudioManager.Instance가 null일 수 있으므로(개발 중 직접 진입) ?. 와 ?? 1f로 안전 처리한다 (규칙 5).
        /// </summary>
        private void SetupVolumeSliders()
        {
            if (_masterSlider != null)
            {
                _masterSlider.onValueChanged.RemoveAllListeners();
                _masterSlider.value = AudioManager.Instance?.GetMasterVolume() ?? 1f;
                _masterSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetMasterVolume(v));
            }
            if (_bgmSlider != null)
            {
                _bgmSlider.onValueChanged.RemoveAllListeners();
                _bgmSlider.value = AudioManager.Instance?.GetBgmVolume() ?? 1f;
                _bgmSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetBgmVolume(v));
            }
            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.RemoveAllListeners();
                _sfxSlider.value = AudioManager.Instance?.GetSfxVolume() ?? 1f;
                _sfxSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetSfxVolume(v));
            }
        }

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

            // 공유 Background에 닫기 콜백 등록 — 바깥 영역 클릭 시 Hide() 호출됨.
            // 이전에 다른 팝업이 등록해뒀더라도 Register()가 덮어쓰므로 안전.
            if (_sharedBackground != null)
                _sharedBackground.Register(Hide);

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
            // 포기 확인 팝업이 떠 있었다면 함께 닫는다 — 잔류 모달 방지.
            _confirmPopup?.Hide();

            // SharedBackground 콜백 해제 — 닫힌 후 외부 클릭이 잘못 트리거되는 것을 막음.
            if (_sharedBackground != null)
                _sharedBackground.Unregister();

            // 이 스크립트가 일시정지를 걸어둔 경우에만 복원.
            // (멀티플레이에서는 _pausedBySettings가 false이므로 건드리지 않음)
            if (_pausedBySettings)
            {
                Time.timeScale = 1f;
                _pausedBySettings = false;
            }

            // 볼륨 패널도 함께 닫는다 — 설정 메뉴가 닫히면 하위 패널도 잔류하지 않도록.
            HideVolumePanel();

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

            // 다른 씬(로비 등)에서 볼륨이 바뀌었을 수 있으므로 표시 시점에 슬라이더 값을 다시 동기화.
            RefreshVolumeSliderValues();

            _volumePanelGroup.alpha = 1f;
            _volumePanelGroup.interactable = true;
            _volumePanelGroup.blocksRaycasts = true;
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
        }

        /// <summary>
        /// 슬라이더 값만 현재 저장 볼륨으로 다시 맞춘다 (리스너 재등록 없음).
        /// onValueChanged 리스너가 이미 붙어 있으므로, 값만 갱신하면 SetXxxVolume이
        /// 다시 호출될 수 있으나 동일 값이라 부작용은 없다.
        /// </summary>
        private void RefreshVolumeSliderValues()
        {
            if (_masterSlider != null)
                _masterSlider.value = AudioManager.Instance?.GetMasterVolume() ?? 1f;
            if (_bgmSlider != null)
                _bgmSlider.value = AudioManager.Instance?.GetBgmVolume() ?? 1f;
            if (_sfxSlider != null)
                _sfxSlider.value = AudioManager.Instance?.GetSfxVolume() ?? 1f;
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
            if (_confirmPopup == null)
            {
                // 확인 팝업이 설정되어 있지 않으면 안전을 위해 동작을 막는다.
                // (Inspector 미연결 시 의도치 않은 즉시 패배 방지)
                Debug.LogWarning("[InGameSettingsUI] ConfirmPopup이 연결되지 않아 포기 동작을 수행하지 않습니다.");
                return;
            }

            _confirmPopup.Show(
                message: "정말 포기하시겠습니까?",
                confirmLabel: "포기",
                cancelLabel: "취소",
                onConfirm: OnForfeitConfirmed,
                onCancel: null);
        }

        /// <summary>
        /// 포기 확정 콜백.
        /// 싱글/멀티 모드 분기는 _forfeitService 구현체가 처리하므로 본 메서드는 단일 흐름.
        /// - 싱글: GameEndUseCase.RequestForfeit() → Red 승리로 즉시 게임 종료.
        /// - 멀티: NetworkGameEndController.RequestForfeit() → 서버가 자기 팀 패배 처리.
        ///
        /// [2026-05-20] 리팩토링:
        ///   기존: FindFirstObjectByType&lt;NetworkGameEndController&gt;()로 직접 호출
        ///   변경: GameBootstrapper에서 주입된 IForfeitService에 위임 (FindFirstObjectByType 제거)
        ///
        /// _forfeitService가 주입되지 않은 경우의 폴백:
        ///   싱글이면 _gameEndUseCase.Forfeit() 직접 호출. 멀티이면 경고 로그 후 종료.
        /// </summary>
        private void OnForfeitConfirmed()
        {
            if (_forfeitService != null)
            {
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
