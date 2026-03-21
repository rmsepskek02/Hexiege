// ============================================================================
// ProductionPanelUI.cs
// 배럭 클릭 시 표시되는 유닛 생산 패널 UI.
//
// 인터랙션 흐름:
//   1. InputHandler가 자기 팀 배럭 클릭 감지
//   2. ProductionPanelUI.Show(barracks) 호출 → 팝업 표시
//   3. 유닛 아이콘 탭 → 수동 큐 추가 (EnqueueUnit)
//   4. 유닛 아이콘 롱프레스 → 자동 생산 토글 (ToggleAutoProduction)
//   5. 랠리포인트 버튼 → 랠리포인트 설정 모드 진입
//   6. Background 터치 / CancelButton → Close()
//
// 큐 슬롯 표시:
//   슬롯 0 = 현재 생산 중인 유닛 (프로그레스 바와 연동)
//   슬롯 1~2 = 대기 큐 (ManualQueue[0], ManualQueue[1])
//   최대 3개 (1 생산 + 2 대기)
//
// UI 계층 구조 (에디터):
//   [UI] Canvas
//     └─ ProductionPopup (_popup, 토글)
//         ├─ Background (Button → Close)
//         ├─ CancelButton (Button → Close)
//         └─ ProductionPanel (Image: ui_panel_dark.png)
//             ├─ HeaderText ("배럭 Lv.1")
//             ├─ UnitButtons1 (HorizontalLayoutGroup, 미래 유닛 확장용)
//             │   └─ PistoleerButton × N
//             │       └─ Portrait (pistoleer_portrait.png)
//             │       └─ CostText ("50")
//             │       └─ AutoIndicator (자동 생산 ON 표시)
//             ├─ UnitButtons2 (HorizontalLayoutGroup, 미래 유닛 확장용)
//             ├─ QueueSlots (HorizontalLayoutGroup)
//             │   ├─ Slot1~3 (Button)
//             │   │   ├─ SlotImage (ui_slot_queue.png, 배경)
//             │   │   └─ UnitImage (유닛 초상화, _queueSlotImages에 연결)
//             ├─ ProgressBar (ui_bar_progress_frame.png + fill)
//             ├─ InfoBar
//             │   ├─ GoldIcon + GoldText (TextMeshProUGUI)
//             │   └─ PopIcon + PopText (TextMeshProUGUI)
//             └─ RallyPointButton
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, UI).
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Netcode;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;
using TMPro;

namespace Hexiege.Presentation
{
    public class ProductionPanelUI : MonoBehaviour
    {
        // ====================================================================
        // Inspector UI 참조
        // ====================================================================

        [Header("Popup")]
        [Tooltip("팝업 래퍼 (AnimatedPanel 부착, Show()/Hide()로 토글)")]
        [SerializeField] private AnimatedPanel _popup;

        [Tooltip("배경 버튼 (터치 시 팝업 닫기)")]
        [SerializeField] private Button _backgroundButton;

        [Header("Unit Buttons")]
        [Tooltip("권총병 생산 버튼")]
        [SerializeField] private Button _pistoleerButton;
        [Tooltip("돌격병 생산 버튼")]
        [SerializeField] private Button _assaultButton;
        [Tooltip("저격수 생산 버튼")]
        [SerializeField] private Button _sniperButton;

        [Header("Button Portrait Images")]
        [Tooltip("권총병 버튼 초상화 Image 컴포넌트")]
        [SerializeField] private Image _pistoleerButtonPortrait;
        [Tooltip("돌격병 버튼 초상화 Image 컴포넌트")]
        [SerializeField] private Image _assaultButtonPortrait;
        [Tooltip("저격수 버튼 초상화 Image 컴포넌트")]
        [SerializeField] private Image _sniperButtonPortrait;

        [Header("Auto Indicators")]
        [Tooltip("권총병 자동 생산 인디케이터 (해당 유닛이 AutoTypes에 등록되면 활성)")]
        [SerializeField] private GameObject _pistoleerAutoIndicator;
        [Tooltip("돌격병 자동 생산 인디케이터")]
        [SerializeField] private GameObject _assaultAutoIndicator;
        [Tooltip("저격수 자동 생산 인디케이터")]
        [SerializeField] private GameObject _sniperAutoIndicator;

        [Header("Queue Slots")]
        [Tooltip("큐 슬롯 이미지 3개 (순서대로)")]
        [SerializeField] private Image[] _queueSlotImages;

        [Header("Unit Portraits")]
        [SerializeField] private UnitPortraitSet _bluePortraits;
        [SerializeField] private UnitPortraitSet _redPortraits;

        /// <summary>
        /// 팀별 유닛 초상화 스프라이트 세트.
        /// </summary>
        [System.Serializable]
        public struct UnitPortraitSet
        {
            public Sprite pistoleer;
            public Sprite assault;
            public Sprite sniper;
        }

        [Header("Progress")]
        [Tooltip("생산 진행률 바 fill Image")]
        [SerializeField] private Image _progressFill;

        [Header("Info")]
        [Tooltip("골드 수치 텍스트")]
        [SerializeField] private TextMeshProUGUI _goldText;

        [Tooltip("인구 수치 텍스트")]
        [SerializeField] private TextMeshProUGUI _populationText;

        [Header("Buttons")]
        [Tooltip("취소 버튼 (팝업 닫기)")]
        [SerializeField] private Button _cancelButton;

        [Header("Rally Point")]
        [Tooltip("랠리포인트 설정 버튼")]
        [SerializeField] private Button _rallyPointButton;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private UnitProductionUseCase _production;
        private ResourceUseCase _resource;
        private PopulationUseCase _population;
        private ProductionTicker _ticker;

        /// <summary>
        /// 네트워크 생산 컨트롤러. null이면 싱글플레이 모드(UseCase 직접 호출).
        /// </summary>
        private NetworkProductionController _networkProductionController;

        /// <summary> 현재 표시 중인 배럭 데이터. </summary>
        private BuildingData _currentBarracks;

        /// <summary> 팝업이 열려있는지 여부. </summary>
        public bool IsOpen => _popup != null && _popup.IsVisible;

        /// <summary> 팝업이 닫힌 프레임. 같은 프레임 클릭 통과 방지용. </summary>
        public int ClosedFrame { get; private set; } = -1;

        /// <summary> 랠리포인트 설정 모드 여부. InputHandler에서 확인. </summary>
        public bool IsSettingRallyPoint { get; private set; }

        /// <summary> 랠리포인트 모드 진입 프레임. 같은 프레임 클릭 방지용. </summary>
        public int RallyPointSetFrame { get; private set; }

        /// <summary> 현재 열린 배럭 Id. 랠리포인트 설정 시 사용. </summary>
        public int CurrentBarracksId => _currentBarracks?.Id ?? -1;

        // 롱프레스 판정용
        private float _pointerDownTime;
        private bool _isPointerDown;
        private const float LongPressThreshold = 0.5f;
        private bool _longPressTriggered;
        private UnitType _activeUnitType; // 현재 눌린 버튼의 유닛 타입

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper에서 호출. UseCase 참조 설정 및 이벤트 연결.
        /// networkProductionController가 null이면 싱글플레이 모드.
        /// </summary>
        public void Initialize(UnitProductionUseCase production,
            ResourceUseCase resource, PopulationUseCase population,
            ProductionTicker ticker,
            NetworkProductionController networkProductionController = null)
        {
            _production = production;
            _resource = resource;
            _population = population;
            _ticker = ticker;
            _networkProductionController = networkProductionController;

            // 시작 시 팝업 비활성 (AnimatedPanel.Awake()에서 이미 비활성화되지만 명시적 보장)

            // 배경 버튼 → 닫기
            if (_backgroundButton != null)
                _backgroundButton.onClick.AddListener(Close);

            // 취소 버튼 → 닫기
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);

            // 랠리포인트 버튼
            if (_rallyPointButton != null)
                _rallyPointButton.onClick.AddListener(OnRallyPointClick);

            // 유닛 버튼: 롱프레스/탭 구분을 위해 EventTrigger 사용
            SetupUnitButton(_pistoleerButton, UnitType.Pistoleer);
            SetupUnitButton(_assaultButton,   UnitType.Assault);
            SetupUnitButton(_sniperButton,    UnitType.Sniper);

            // 큐 슬롯 클릭 → 생산 취소
            SetupQueueSlotButtons();

            // 생산 큐 변경 이벤트 구독 → UI 갱신
            GameEvents.OnProductionQueueChanged
                .Subscribe(_ => UpdateUI())
                .AddTo(this);

            // 자원 변경 이벤트 구독 → 골드 표시 갱신
            GameEvents.OnResourceChanged
                .Subscribe(_ => UpdateInfoBar())
                .AddTo(this);
        }

        // ====================================================================
        // 팝업 표시/닫기
        // ====================================================================

        /// <summary>
        /// 생산 패널 표시. InputHandler에서 배럭 클릭 시 호출.
        /// </summary>
        public void Show(BuildingData barracks)
        {
            _currentBarracks = barracks;
            IsSettingRallyPoint = false;

            _popup?.Show();

            // 배럭 선택 시 랠리포인트 마커 표시
            if (_ticker != null)
                _ticker.ShowRallyMarker(barracks.Id);

            UpdateButtonPortraits(barracks.Team);
            UpdateUI();
        }

        /// <summary>
        /// 팝업 닫기.
        /// </summary>
        public void Close()
        {
            ClosedFrame = Time.frameCount;
            IsSettingRallyPoint = false;

            // 팝업 닫힐 때 랠리포인트 마커 숨김
            if (_ticker != null)
                _ticker.HideAllRallyMarkers();

            _popup?.Hide();

            _currentBarracks = null;
        }

        // ====================================================================
        // 유닛 버튼 입력 (탭/롱프레스)
        // ====================================================================

        /// <summary>
        /// 유닛 버튼에 PointerDown/Up 이벤트를 연결하여 탭/롱프레스 구분.
        /// </summary>
        private void SetupUnitButton(Button button, UnitType type)
        {
            if (button == null) return;

            var trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener(_ => OnUnitPointerDown(type));
            trigger.triggers.Add(downEntry);

            var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener(_ => OnUnitPointerUp());
            trigger.triggers.Add(upEntry);

            button.onClick.RemoveAllListeners();
        }

        private void OnUnitPointerDown(UnitType type)
        {
            _activeUnitType = type;
            _pointerDownTime = Time.unscaledTime;
            _isPointerDown = true;
            _longPressTriggered = false;
        }

        private void OnUnitPointerUp()
        {
            if (!_isPointerDown) return;
            _isPointerDown = false;

            if (_longPressTriggered) return;

            OnUnitTap(_activeUnitType);
        }

        private void Update()
        {
            // 롱프레스 판정
            if (_isPointerDown && !_longPressTriggered)
            {
                if (Time.unscaledTime - _pointerDownTime >= LongPressThreshold)
                {
                    _longPressTriggered = true;
                    OnUnitLongPress(_activeUnitType);
                }
            }

            // 프로그레스 바 실시간 갱신
            if (IsOpen && _currentBarracks != null)
            {
                UpdateProgressBar();
            }
        }

        // ====================================================================
        // 생산 액션
        // ====================================================================

        /// <summary>
        /// 큐 슬롯의 부모 Button을 찾아 클릭 이벤트 연결.
        /// _queueSlotImages의 부모 또는 자기 자신에서 Button 컴포넌트를 탐색.
        /// </summary>
        private void SetupQueueSlotButtons()
        {
            if (_queueSlotImages == null) return;

            for (int i = 0; i < _queueSlotImages.Length; i++)
            {
                if (_queueSlotImages[i] == null) continue;

                // 슬롯 이미지의 부모(Slot1~3)에서 Button 컴포넌트 탐색
                // GetComponentInParent는 비활성 계층에서 실패하므로 transform.parent 직접 접근
                var button = _queueSlotImages[i].GetComponent<Button>();
                if (button == null && _queueSlotImages[i].transform.parent != null)
                    button = _queueSlotImages[i].transform.parent.GetComponent<Button>();

                if (button != null)
                {
                    // 중복 등록 방지: 기존 리스너 모두 제거 후 새로 등록
                    // (Initialize가 여러 번 호출되거나 씬 재로드 시 이중 호출 방지)
                    button.onClick.RemoveAllListeners();
                    int slotIndex = i; // 클로저 캡처용
                    button.onClick.AddListener(() => OnQueueSlotClicked(slotIndex));
                }
            }
        }

        /// <summary> 큐 슬롯 클릭 → 해당 슬롯 생산 취소. </summary>
        private void OnQueueSlotClicked(int slotIndex)
        {
            if (_currentBarracks == null || _production == null) return;
            _production.CancelQueueAt(_currentBarracks.Id, slotIndex);
        }

        /// <summary>
        /// 탭 → 수동 큐에 유닛 추가 또는 자동 생산 취소.
        /// 자동 모드 ON 상태에서 이미 등록된 타입을 탭하면 자동 생산 목록에서 제거(취소).
        /// 그 외에는 수동 큐에 추가.
        /// 멀티플레이 모드이면 NetworkProductionController를 통해 서버에 요청.
        /// 싱글플레이이면 UseCase를 직접 호출.
        /// </summary>
        private void OnUnitTap(UnitType type)
        {
            if (_currentBarracks == null || _production == null) return;

            var state = _production.GetState(_currentBarracks.Id);
            bool isAutoForType = state != null && state.IsAutoMode && state.AutoTypes.Contains(type);

            if (isAutoForType)
            {
                // 자동 모드 ON 상태에서 등록된 타입 탭 → 자동 생산 취소 (ToggleAutoProduction)
                // ToggleAutoProduction 내부에서 슬롯 0/1/2 여부에 따라 환불 분기 처리
                HandleToggleAuto(type);
            }
            else
            {
                // 자동 모드 OFF 또는 해당 타입 미등록 → 수동 큐 추가
                if (_networkProductionController != null &&
                    NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.IsListening)
                {
                    _networkProductionController.RequestEnqueueServerRpc(
                        _currentBarracks.Id,
                        (int)type,
                        (int)_currentBarracks.Team);

                    Debug.Log($"[Network] 생산 큐 요청 전송. BarracksId={_currentBarracks.Id}, UnitType={type}");
                }
                else
                {
                    _production.EnqueueUnit(_currentBarracks.Id, type);
                }
            }
        }

        /// <summary>
        /// 롱프레스 → 자동 생산 토글.
        /// 자동 모드 ON 상태에서 이미 등록된 타입 롱프레스 → 탭과 동일하게 취소 처리.
        /// 자동 모드 OFF 또는 미등록 타입 → 자동 생산 등록.
        /// 멀티플레이이면 서버에 토글 요청, 싱글플레이이면 UseCase 직접 호출.
        /// </summary>
        private void OnUnitLongPress(UnitType type)
        {
            if (_currentBarracks == null || _production == null) return;

            var state = _production.GetState(_currentBarracks.Id);
            bool isAutoForType = state != null && state.IsAutoMode && state.AutoTypes.Contains(type);

            if (isAutoForType)
            {
                // 자동 모드 ON 상태에서 이미 등록된 타입 롱프레스 → 탭과 동일한 취소 처리
                HandleToggleAuto(type);
                return;
            }

            // 자동 모드 OFF 또는 미등록 타입 → 자동 생산 등록
            HandleToggleAuto(type);
        }

        /// <summary>
        /// 자동 생산 토글 공통 로직.
        /// 네트워크/싱글플레이 분기 처리. OnUnitTap과 OnUnitLongPress에서 공통 호출.
        /// </summary>
        private void HandleToggleAuto(UnitType type)
        {
            if (_networkProductionController != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                _networkProductionController.ToggleAutoServerRpc(
                    _currentBarracks.Id,
                    (int)type,
                    (int)_currentBarracks.Team);

                Debug.Log($"[Network] 자동 생산 토글 요청. BarracksId={_currentBarracks.Id}, UnitType={type}");
            }
            else
            {
                _production.ToggleAutoProduction(_currentBarracks.Id, type);
            }
        }

        /// <summary> 랠리포인트 설정 모드 진입. </summary>
        private void OnRallyPointClick()
        {
            IsSettingRallyPoint = true;
            RallyPointSetFrame = Time.frameCount;

            // 팝업을 닫아서 타일 클릭이 가능하도록 함
            // Close()를 호출하면 IsSettingRallyPoint와 _currentBarracks가 리셋되므로
            // 직접 팝업만 비활성화
            _popup?.Hide();
        }

        /// <summary>
        /// 랠리포인트 설정 완료. InputHandler에서 타일 클릭 시 호출.
        /// </summary>
        public void CompleteRallyPointSetting(HexCoord target)
        {
            if (_currentBarracks == null || _production == null) return;

            _production.SetRallyPoint(_currentBarracks.Id, target);
            IsSettingRallyPoint = false;
            _currentBarracks = null;
        }

        // ====================================================================
        // UI 갱신
        // ====================================================================

        /// <summary>
        /// 큐 슬롯, 자동 표시, 프로그레스 바, 자원 정보를 갱신.
        /// </summary>
        private void UpdateUI()
        {
            if (_currentBarracks == null || _production == null) return;

            var state = _production.GetState(_currentBarracks.Id);
            if (state == null) return;

            // 버튼별 자동 생산 인디케이터 업데이트
            // 해당 유닛 타입이 AutoTypes에 등록되어 있으면 인디케이터 활성화
            if (_pistoleerAutoIndicator != null)
                _pistoleerAutoIndicator.SetActive(state.IsAutoMode && state.AutoTypes.Contains(UnitType.Pistoleer));
            if (_assaultAutoIndicator != null)
                _assaultAutoIndicator.SetActive(state.IsAutoMode && state.AutoTypes.Contains(UnitType.Assault));
            if (_sniperAutoIndicator != null)
                _sniperAutoIndicator.SetActive(state.IsAutoMode && state.AutoTypes.Contains(UnitType.Sniper));

            // 큐 슬롯 갱신
            UpdateQueueSlots(state);

            // 프로그레스 바
            UpdateProgressBar();

            // 자원 정보
            UpdateInfoBar();
        }

        /// <summary>
        /// 큐 슬롯 표시.
        /// 자동 모드: 슬롯0=현재 생산 중, 슬롯1=AutoTypes[(AutoIndex+1)%count], 슬롯2=AutoTypes[(AutoIndex+2)%count]
        /// 수동 모드: 슬롯0=현재 생산 중, 슬롯1~2=ManualQueue[0~1]
        /// </summary>
        private void UpdateQueueSlots(ProductionState state)
        {
            if (_queueSlotImages == null) return;

            if (state.IsAutoMode)
            {
                // ── 자동 모드: AutoTypes 기반 큐 표시 ──
                int count = state.AutoTypes.Count;

                for (int i = 0; i < _queueSlotImages.Length; i++)
                {
                    if (_queueSlotImages[i] == null) continue;

                    UnitType? slotType = null;

                    if (i == 0)
                    {
                        // 슬롯 0: 현재 생산 중인 유닛
                        slotType = state.CurrentProducing;
                    }
                    else if (count >= 2 && i == 1)
                    {
                        // 슬롯 1: AutoIndex+1 위치의 다음 생산 예정 유닛
                        // AutoIndex는 현재 생산 중인 타입을 가리키므로 +1이 다음 대기 슬롯
                        slotType = state.AutoTypes[(state.AutoIndex + 1) % count];
                    }
                    else if (count >= 3 && i == 2)
                    {
                        // 슬롯 2: AutoIndex+2 위치의 그 다음 생산 예정 유닛
                        slotType = state.AutoTypes[(state.AutoIndex + 2) % count];
                    }

                    ApplySlotImage(i, slotType);
                }
            }
            else
            {
                // ── 수동 모드: ManualQueue 기반 큐 표시 (기존 로직) ──
                for (int i = 0; i < _queueSlotImages.Length; i++)
                {
                    if (_queueSlotImages[i] == null) continue;

                    UnitType? slotType = null;

                    if (i == 0)
                    {
                        // 슬롯 0: 현재 생산 중인 유닛
                        slotType = state.CurrentProducing;
                    }
                    else
                    {
                        // 슬롯 1~2: 대기 큐 (ManualQueue[0], ManualQueue[1])
                        int queueIndex = i - 1;
                        if (queueIndex < state.ManualQueue.Count)
                            slotType = state.ManualQueue[queueIndex];
                    }

                    ApplySlotImage(i, slotType);
                }
            }
        }

        /// <summary>
        /// 큐 슬롯 이미지 적용 헬퍼.
        /// 유닛 타입이 있으면 초상화 표시, 없으면 투명 처리.
        /// </summary>
        private void ApplySlotImage(int slotIndex, UnitType? slotType)
        {
            if (slotIndex < 0 || slotIndex >= _queueSlotImages.Length) return;
            if (_queueSlotImages[slotIndex] == null) return;

            if (slotType.HasValue)
            {
                _queueSlotImages[slotIndex].sprite = GetPortrait(slotType.Value);
                _queueSlotImages[slotIndex].color = Color.white;
            }
            else
            {
                // 빈 슬롯 → UnitImage 숨김
                _queueSlotImages[slotIndex].sprite = null;
                _queueSlotImages[slotIndex].color = new Color(1f, 1f, 1f, 0f);
            }
        }

        /// <summary> 프로그레스 바 갱신. </summary>
        private void UpdateProgressBar()
        {
            if (_progressFill == null || _currentBarracks == null || _production == null) return;

            var state = _production.GetState(_currentBarracks.Id);
            _progressFill.fillAmount = (state != null) ? state.Progress : 0f;
        }

        /// <summary> 골드/인구 정보 갱신. </summary>
        private void UpdateInfoBar()
        {
            if (_currentBarracks == null) return;
            TeamId team = _currentBarracks.Team;

            if (_goldText != null && _resource != null)
                _goldText.text = _resource.GetGold(team).ToString();

            if (_populationText != null && _population != null)
            {
                int used = _population.GetUsedPopulation(team);
                int max = _population.GetMaxPopulation(team);
                _populationText.text = $"{used}/{max}";
            }
        }

        /// <summary> 팀에 맞는 초상화 스프라이트를 버튼 Image에 적용. Show() 시 호출. </summary>
        private void UpdateButtonPortraits(TeamId team)
        {
            var set = team == TeamId.Blue ? _bluePortraits : _redPortraits;
            if (_pistoleerButtonPortrait != null) _pistoleerButtonPortrait.sprite = set.pistoleer;
            if (_assaultButtonPortrait   != null) _assaultButtonPortrait.sprite   = set.assault;
            if (_sniperButtonPortrait    != null) _sniperButtonPortrait.sprite     = set.sniper;
        }

        /// <summary> 유닛 타입에 해당하는 초상화 스프라이트. 현재 배럭 팀 기준으로 세트 선택. </summary>
        private Sprite GetPortrait(UnitType type)
        {
            var set = _currentBarracks?.Team == TeamId.Blue ? _bluePortraits : _redPortraits;
            return type switch
            {
                UnitType.Pistoleer => set.pistoleer,
                UnitType.Assault   => set.assault,
                UnitType.Sniper    => set.sniper,
                _                  => set.pistoleer
            };
        }
    }
}
