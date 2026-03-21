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
        [Tooltip("권총병 자동 생산 인디케이터 (해당 유닛이 AutoEntries에 등록되면 활성)")]
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

        /// <summary>
        /// 큐 슬롯 클릭 → 해당 슬롯 생산 취소.
        ///
        /// 자동 모드 "취소 상태"(슬롯0의 타입이 AutoEntries에서 이미 제거된 상태) 대응:
        ///   CancelQueueAt의 방어 조건(slotIndex==1 && count<2)은 "정상 자동 상태" 기준이므로,
        ///   취소 상태에서 슬롯1~2에 표시된 AutoEntries 항목을 클릭하면 CancelQueueAt이 false를 반환함.
        ///   이 경우 해당 슬롯에 실제 표시된 AutoEntries 항목을 ToggleAutoProduction으로 제거 처리.
        /// </summary>
        private void OnQueueSlotClicked(int slotIndex)
        {
            if (_currentBarracks == null || _production == null) return;

            // CancelQueueAt 먼저 시도 — 정상 자동 모드 및 수동 모드는 여기서 처리됨
            bool cancelled = _production.CancelQueueAt(_currentBarracks.Id, slotIndex);
            if (cancelled) return;

            // CancelQueueAt 실패 시: 자동 모드 "취소 상태"에서 슬롯1~2 클릭 케이스 처리
            // 취소 상태 = CurrentProducing이 AutoEntries에서 이미 제거된 상태
            // 이때 슬롯1~2에는 AutoEntries의 "다음 생산 예정" 항목이 표시되지만,
            // CancelQueueAt은 count<2 방어 조건으로 인해 제거하지 못함
            // → 해당 슬롯에 표시된 타입을 직접 ToggleAutoProduction으로 제거
            if (slotIndex >= 1)
            {
                var state = _production.GetState(_currentBarracks.Id);
                if (state == null || !state.IsAutoMode || state.AutoCount == 0) return;

                // 취소 상태 판단: AutoEntries[AutoIndex].Type이 CurrentProducing과 다른 경우
                // (CurrentProducing이 null이면 정상 상태로 간주 — TryStartNext가 곧 시작하므로)
                int autoCount = state.AutoCount;
                bool isNormalAutoState = !state.CurrentProducing.HasValue
                    || state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value;

                if (isNormalAutoState) return; // 정상 상태면 추가 처리 불필요

                // 취소 상태에서 슬롯에 표시된 AutoEntries 항목 계산
                // UpdateQueueSlots와 동일한 로직:
                //   슬롯1 = AutoEntries[AutoIndex % count]
                //   슬롯2 = AutoEntries[(AutoIndex + 1) % count]
                int offset = slotIndex - 1; // 슬롯1→0, 슬롯2→1
                if (offset >= autoCount) return; // 표시할 항목이 없는 빈 슬롯 클릭

                int targetIdx = (state.AutoIndex + offset) % autoCount;
                UnitType targetType = state.AutoTypeAt(targetIdx);

                // ToggleAutoProduction으로 해당 타입 제거 (네트워크/싱글플레이 분기는 HandleToggleAuto 사용)
                HandleToggleAuto(targetType);
            }
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
            bool isAutoForType = state != null && state.IsAutoMode && state.AutoContains(type);

            if (isAutoForType)
            {
                // 자동 모드 ON 상태에서 등록된 타입 탭 → 자동 생산 취소 (ToggleAutoProduction)
                // ToggleAutoProduction 내부에서 IsCharged 여부에 따라 환불 분기 처리
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
            bool isAutoForType = state != null && state.IsAutoMode && state.AutoContains(type);

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
            // 해당 유닛 타입이 AutoEntries에 등록되어 있으면 인디케이터 활성화
            if (_pistoleerAutoIndicator != null)
                _pistoleerAutoIndicator.SetActive(state.IsAutoMode && state.AutoContains(UnitType.Pistoleer));
            if (_assaultAutoIndicator != null)
                _assaultAutoIndicator.SetActive(state.IsAutoMode && state.AutoContains(UnitType.Assault));
            if (_sniperAutoIndicator != null)
                _sniperAutoIndicator.SetActive(state.IsAutoMode && state.AutoContains(UnitType.Sniper));

            // 큐 슬롯 갱신
            UpdateQueueSlots(state);

            // 프로그레스 바
            UpdateProgressBar();

            // 자원 정보
            UpdateInfoBar();
        }

        /// <summary>
        /// 큐 슬롯 표시.
        /// 자동 모드 (정상): 슬롯0=현재 생산 중, 슬롯1=AutoEntries[+1], 슬롯2=AutoEntries[+2]
        /// 자동 모드 (취소): 슬롯0=현재 생산 중, 슬롯1=AutoEntries[AutoIndex](다음 타입), 슬롯2=AutoEntries[+1]
        /// 수동 모드: 슬롯0=현재 생산 중, 슬롯1~2=ManualQueue[0~1]
        /// ManualQueue가 있으면 AutoEntries보다 우선 표시.
        /// </summary>
        private void UpdateQueueSlots(ProductionState state)
        {
            if (_queueSlotImages == null) return;

            if (state.IsAutoMode)
            {
                // ── 자동 모드: ManualQueue 우선 + AutoEntries 혼용 큐 표시 ──
                // TryStartNext의 ManualQueue 우선 처리 순서와 일치시킴
                int autoCount = state.AutoCount;
                int manualCount = state.ManualQueue.Count;

                for (int i = 0; i < _queueSlotImages.Length; i++)
                {
                    if (_queueSlotImages[i] == null) continue;

                    UnitType? slotType = null;

                    if (i == 0)
                    {
                        // 슬롯 0: 현재 생산 중인 유닛 (수동/자동 무관)
                        slotType = state.CurrentProducing;
                    }
                    else if (i == 1 || i == 2)
                    {
                        // ── 슬롯 1~2: ManualQueue 우선 + AutoEntries 혼용 큐 표시 ──
                        //
                        // 상태 A (정상 자동 모드):
                        //   AutoEntries[AutoIndex % count].Type == CurrentProducing
                        //   → 슬롯0에 생산 중인 타입이 AutoEntries에 그대로 있는 상태
                        //   → 슬롯1 = AutoEntries[(AutoIndex+1) % count], 슬롯2 = AutoEntries[(AutoIndex+2) % count]
                        //
                        // 상태 B (취소 상태):
                        //   AutoEntries[AutoIndex % count].Type != CurrentProducing
                        //   → 슬롯0 타입이 AutoEntries에서 취소되어 AutoEntries[AutoIndex]는 "다음 생산 타입"
                        //   → 슬롯1 = AutoEntries[AutoIndex % count], 슬롯2 = AutoEntries[(AutoIndex+1) % count]
                        //
                        // 예) 자동 1개(Pistoleer)만 등록, 정상 상태:
                        //   isNormal=true, count=1 → 슬롯1: count>=2? NO → null (중복 방지)
                        //
                        // 예) Assault 취소 후 Sniper만 남음, CurrentProducing=Assault:
                        //   isNormal=false → 슬롯1: count>=1 → AutoEntries[0].Type=Sniper (다음 생산 예고)

                        // 현재 생산 중인 타입이 AutoEntries에 그대로 있는 "정상 상태"인지 판단
                        // CurrentProducing=null이면 AutoEntries[AutoIndex]가 곧 슬롯0에 올라올 상태
                        // → 정상 상태로 처리하여 플리커(슬롯1에 유닛이 순간 등장했다 사라지는 현상) 방지
                        bool isNormalAutoState = autoCount > 0 && (
                            !state.CurrentProducing.HasValue
                            || state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value
                        );

                        if (i == 1)
                        {
                            // 슬롯 1: ManualQueue[0] 우선, 없으면 AutoEntries 다음 항목
                            if (manualCount > 0)
                                slotType = state.ManualQueue[0];
                            else if (isNormalAutoState && autoCount >= 2)
                                slotType = state.AutoTypeAt((state.AutoIndex + 1) % autoCount);
                            else if (!isNormalAutoState && autoCount >= 1)
                                slotType = state.AutoTypeAt(state.AutoIndex % autoCount);
                        }
                        else // i == 2
                        {
                            // 슬롯 2: ManualQueue 우선, 남은 분은 AutoEntries에서 채움
                            if (manualCount > 1)
                            {
                                // ManualQueue에 2개 이상 → ManualQueue[1]
                                slotType = state.ManualQueue[1];
                            }
                            else if (manualCount == 1 && autoCount >= 1)
                            {
                                // ManualQueue 1개(슬롯1에 표시됨) + AutoEntries 다음 항목
                                // 정상 상태면 +1, 취소 상태면 +0 (AutoIndex가 이미 "다음"을 가리킴)
                                slotType = state.AutoTypeAt((state.AutoIndex + (isNormalAutoState ? 1 : 0)) % autoCount);
                            }
                            else if (isNormalAutoState && autoCount >= 3)
                            {
                                // 정상 상태: ManualQueue 없음 → AutoEntries에서 2번째 대기 항목
                                slotType = state.AutoTypeAt((state.AutoIndex + 2) % autoCount);
                            }
                            else if (!isNormalAutoState && autoCount >= 2)
                            {
                                // 취소 상태: ManualQueue 없음 → AutoEntries에서 1번째 대기 항목
                                slotType = state.AutoTypeAt((state.AutoIndex + 1) % autoCount);
                            }
                        }
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
