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
//   슬롯 1~2 = 대기 큐 (PendingQueue[0], PendingQueue[1])
//   최대 3개 (1 생산 + 2 대기)
//
// [재작성 — 2026-04-19]
// ProductionState가 단일 PendingQueue 구조로 바뀌면서 UI 로직도 단순화됨.
//   - UpdateQueueSlots: 슬롯1=PendingQueue[0], 슬롯2=PendingQueue[1]만 읽으면 끝.
//   - OnQueueSlotClicked: CancelQueueAt 호출만 남고, 이전의 "취소 상태" fallback 경로 제거됨.
//   - 자동 인디케이터: state.AutoTypes.Contains(type)로 판단.
//
// 종족별 유닛 버튼 동적 바인딩:
//   Show() 호출 시 배럭의 팀(Blue/Red)으로 GameRaceContext에서 종족 조회 →
//   종족에 맞는 UnitType 3개를 버튼에 재바인딩.
//   Human: Pistoleer / Assault / Sniper
//   Spirit: EmberSpirit / FlameSpirit / InfernoSpirit
//   Transcendence: BearGuard / FoxMagician / LionKnight
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
    public class ProductionPanelUI : MonoBehaviour, IGameUI
    {
        // ====================================================================
        // Inspector UI 참조
        // ====================================================================

        [Header("Popup")]
        [Tooltip("팝업 래퍼 (AnimatedPanel 부착, Show()/Hide()로 토글)")]
        [SerializeField] private AnimatedPanel _popup;

        [Tooltip("Canvas 직속 공유 Background (터치 시 팝업 닫기)")]
        [SerializeField] private SharedBackgroundButton _sharedBackground;

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

        [Header("Unit Portraits — 종족별 초상화 (팀×종족 = 6세트)")]
        [Tooltip("Blue팀 Human 종족 초상화 (slot1=Pistoleer, slot2=Assault, slot3=Sniper)")]
        [SerializeField] private UnitPortraitSet _blueHumanPortraits;
        [Tooltip("Blue팀 Spirit 종족 초상화 (slot1=EmberSpirit, slot2=FlameSpirit, slot3=InfernoSpirit)")]
        [SerializeField] private UnitPortraitSet _blueSpiritPortraits;
        [Tooltip("Blue팀 Transcendence 종족 초상화 (slot1=BearGuard, slot2=FoxMagician, slot3=LionKnight)")]
        [SerializeField] private UnitPortraitSet _blueTranscendencePortraits;
        [Tooltip("Red팀 Human 종족 초상화")]
        [SerializeField] private UnitPortraitSet _redHumanPortraits;
        [Tooltip("Red팀 Spirit 종족 초상화")]
        [SerializeField] private UnitPortraitSet _redSpiritPortraits;
        [Tooltip("Red팀 Transcendence 종족 초상화")]
        [SerializeField] private UnitPortraitSet _redTranscendencePortraits;

        /// <summary>
        /// 종족별 유닛 초상화 스프라이트 세트.
        /// 3개 슬롯이 종족에 따라 다른 유닛을 나타냄:
        ///   Human: slot1=Pistoleer, slot2=Assault, slot3=Sniper
        ///   Spirit: slot1=EmberSpirit, slot2=FlameSpirit, slot3=InfernoSpirit
        ///   Transcendence: slot1=BearGuard, slot2=FoxMagician, slot3=LionKnight
        /// </summary>
        [System.Serializable]
        public struct UnitPortraitSet
        {
            public Sprite slot1;
            public Sprite slot2;
            public Sprite slot3;
        }

        [Header("Unit Cost Texts")]
        [Tooltip("슬롯1 유닛 골드 비용 텍스트 (예: '50G')")]
        [SerializeField] private TextMeshProUGUI _slot1CostText;

        [Tooltip("슬롯2 유닛 골드 비용 텍스트 (예: '100G')")]
        [SerializeField] private TextMeshProUGUI _slot2CostText;

        [Tooltip("슬롯3 유닛 골드 비용 텍스트 (예: '200G')")]
        [SerializeField] private TextMeshProUGUI _slot3CostText;

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

        /// <summary>
        /// 현재 버튼 3개에 바인딩된 유닛 타입.
        /// Show() 시 종족에 따라 갱신됨.
        /// _buttonUnitTypes[0] = 첫 번째 버튼(slot1), [1] = 두 번째(slot2), [2] = 세 번째(slot3).
        /// </summary>
        private UnitType[] _buttonUnitTypes = new UnitType[3];

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

            // 공유 Background 닫기는 Show()/Close()에서 Register/Unregister로 처리

            // 취소 버튼 → 닫기
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);

            // 랠리포인트 버튼
            if (_rallyPointButton != null)
                _rallyPointButton.onClick.AddListener(OnRallyPointClick);

            // 유닛 버튼: 롱프레스/탭 구분을 위해 EventTrigger 사용.
            // 버튼 슬롯 인덱스(0/1/2)로 바인딩 — 실제 UnitType은 Show() 시 _buttonUnitTypes[]에서 동적으로 결정됨.
            // Initialize()는 한 번만 호출되므로 EventTrigger 중복 추가 위험 없음.
            SetupUnitButtonBySlot(_pistoleerButton, 0);
            SetupUnitButtonBySlot(_assaultButton,   1);
            SetupUnitButtonBySlot(_sniperButton,    2);

            // 기본값으로 Human 종족 UnitType을 초기 설정 (Show() 호출 전 안전망)
            _buttonUnitTypes[0] = UnitType.Pistoleer;
            _buttonUnitTypes[1] = UnitType.Assault;
            _buttonUnitTypes[2] = UnitType.Sniper;

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

            // 공유 Background에 이 패널의 Close 콜백 등록
            // Background 터치 시 Close()가 호출되어 팝업이 닫힌다
            _sharedBackground?.Register(Close);

            // 배럭 선택 시 랠리포인트 마커 표시
            if (_ticker != null)
                _ticker.ShowRallyMarker(barracks.Id);

            // 배럭 팀에 따라 종족을 조회하고, 버튼에 해당 종족의 UnitType을 바인딩
            // 예: Spirit 종족이면 slot1=EmberSpirit, slot2=FlameSpirit, slot3=InfernoSpirit
            RaceId race = barracks.Team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            BindButtonUnitTypes(race);

            UpdateButtonPortraits(barracks.Team, race);
            UpdateUI();
        }

        /// <summary>
        /// 팝업 닫기.
        /// </summary>
        public void Close()
        {
            ClosedFrame = Time.frameCount;
            IsSettingRallyPoint = false;

            // 공유 Background 콜백 해제 (Hide 애니메이션 중 추가 터치 방지)
            _sharedBackground?.Unregister();

            // 팝업 닫힐 때 랠리포인트 마커 숨김
            if (_ticker != null)
                _ticker.HideAllRallyMarkers();

            _popup?.Hide();

            _currentBarracks = null;
        }

        // ====================================================================
        // IGameUI 구현
        // ====================================================================

        /// <summary>
        /// 게임 종료 시 호출.
        /// 생산 패널이 열려있다면 닫아서 게임 종료 화면이 깨끗하게 표시되도록 함.
        /// Close() 내부에서 SharedBackgroundButton.Unregister()도 호출하므로 안전.
        /// </summary>
        public void OnGameEnded()
        {
            Close();
        }

        /// <summary>
        /// 게임 시작/재시작 시 호출.
        /// 혹시 열려있을 수 있는 패널을 닫아서 초기 상태 보장.
        /// 재경기(Rematch) 시 이전 게임에서 열린 패널이 남아있는 것을 방지.
        /// </summary>
        public void OnGameStarted()
        {
            Close();
        }

        // ====================================================================
        // 유닛 버튼 입력 (탭/롱프레스)
        // ====================================================================

        /// <summary>
        /// 유닛 버튼에 PointerDown/Up 이벤트를 연결하여 탭/롱프레스 구분.
        /// 슬롯 인덱스(0/1/2)로 바인딩하여, 실제 UnitType은 _buttonUnitTypes[slotIndex]에서
        /// 런타임에 조회됨. 이 방식으로 Show()마다 EventTrigger를 재등록할 필요 없이
        /// _buttonUnitTypes 배열만 갱신하면 종족 변경이 반영됨.
        /// </summary>
        private void SetupUnitButtonBySlot(Button button, int slotIndex)
        {
            if (button == null) return;

            var trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            // PointerDown: 슬롯 인덱스로 _buttonUnitTypes에서 현재 바인딩된 UnitType 조회
            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener(_ => OnUnitPointerDown(_buttonUnitTypes[slotIndex]));
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
        /// [재작성 — 2026-04-19]
        /// 새 PendingQueue 구조에서는 CancelQueueAt이 항상 올바르게 동작하므로
        /// 이전의 "취소 상태" fallback(ToggleAutoProduction 우회) 경로가 완전히 제거됨.
        ///   슬롯0 → CancelQueueAt(0): CurrentProducing 취소 + 환불
        ///   슬롯1 → CancelQueueAt(1): PendingQueue[0] 제거 + 환불(IsCharged=true인 경우)
        ///   슬롯2 → CancelQueueAt(2): PendingQueue[1] 제거 + 환불(IsCharged=true인 경우)
        /// </summary>
        private void OnQueueSlotClicked(int slotIndex)
        {
            if (_currentBarracks == null || _production == null) return;

            // ── 네트워크 모드: 서버에 취소 요청 전송 후 즉시 리턴 ──
            // 서버에서 CancelQueueAt 실행 → 골드 환불 + SyncQueueStateClientRpc로 UI 자동 갱신
            // 클라이언트가 로컬 UseCase를 직접 호출하면 서버와 상태 불일치 발생 (BUG-14)
            if (_networkProductionController != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                _networkProductionController.CancelSlotServerRpc(
                    _currentBarracks.Id,
                    slotIndex,
                    (int)_currentBarracks.Team);

                Debug.Log($"[Network] 큐 슬롯 취소 요청 전송. BarracksId={_currentBarracks.Id}, SlotIndex={slotIndex}");
                return;
            }

            // ── 싱글플레이: 로컬 UseCase 직접 호출 ──
            // 새 구조에서는 CancelQueueAt이 모든 케이스를 처리하므로 추가 fallback 불필요.
            // 실패 반환은 "그 슬롯에 취소할 항목이 없음" 상태이므로 조용히 무시.
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
            // 자동 모드 ON이면서 해당 타입이 AutoTypes에 포함되어 있으면 "이미 등록된 타입" 탭으로 간주.
            // 이 경우 자동 생산에서 해당 타입을 제거(Rule 2: IsCharged=true 항목은 수동 이관).
            bool isAutoForType = state != null && state.IsAutoMode && state.AutoTypes.Contains(type);

            if (isAutoForType)
            {
                // 자동 모드 ON 상태에서 등록된 타입 탭 → 자동 생산 취소 (ToggleAutoProduction)
                // ToggleAutoProduction 내부에서 IsCharged 여부에 따라 환불/이관 분기 처리
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

            // BUG-16 수정: 네트워크 모드에서는 로컬 상태로 isAutoForType를 판단하지 않는다.
            // 클라이언트의 로컬 ProductionState는 서버와 SyncClientRpc 동기화 타이밍 차이로
            // 실제 서버 상태와 다를 수 있다.
            // 예: 이미 자동 등록된 타입인데 클라이언트에서는 아직 동기화 안 되어
            //     isAutoForType=false → 취소 대신 추가 경로로 진입하는 버그 발생.
            // 서버의 ToggleAutoProduction이 등록 여부를 정확히 판단하므로
            // 네트워크 모드에서는 사전 판단 없이 바로 서버에 토글 요청한다.
            bool isNetworkMode = _networkProductionController != null
                && NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening;

            if (!isNetworkMode)
            {
                // 싱글플레이: 로컬 상태로 판단 (동기화 이슈 없음)
                var state = _production.GetState(_currentBarracks.Id);
                // AutoTypes에 이미 해당 타입이 포함되어 있으면 "이미 등록된 자동 타입"
                bool isAutoForType = state != null && state.IsAutoMode && state.AutoTypes.Contains(type);

                if (isAutoForType)
                {
                    // 자동 모드 ON 상태에서 이미 등록된 타입 롱프레스 → 탭과 동일한 취소 처리
                    HandleToggleAuto(type);
                    return;
                }
            }

            // 네트워크 모드: 서버가 등록/취소 판단 → 바로 토글 요청
            // 싱글플레이: 자동 모드 OFF 또는 미등록 타입 → 자동 생산 등록
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
            // _buttonUnitTypes[]에 바인딩된 현재 종족의 UnitType으로 판단.
            // state.AutoTypes가 해당 유닛 타입을 포함하면 인디케이터 활성화.
            // 예: Spirit 종족이면 EmberSpirit/FlameSpirit/InfernoSpirit을 AutoTypes에서 확인.
            if (_pistoleerAutoIndicator != null)
                _pistoleerAutoIndicator.SetActive(state.AutoTypes.Contains(_buttonUnitTypes[0]));
            if (_assaultAutoIndicator != null)
                _assaultAutoIndicator.SetActive(state.AutoTypes.Contains(_buttonUnitTypes[1]));
            if (_sniperAutoIndicator != null)
                _sniperAutoIndicator.SetActive(state.AutoTypes.Contains(_buttonUnitTypes[2]));

            // 큐 슬롯 갱신
            UpdateQueueSlots(state);

            // 프로그레스 바
            UpdateProgressBar();

            // 자원 정보
            UpdateInfoBar();
        }

        /// <summary>
        /// 큐 슬롯 표시.
        ///
        /// [재작성 — 2026-04-19]
        /// 새 PendingQueue 구조 불변식: PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2.
        /// 자동/수동 구분 없이 동일하게 읽으면 되므로 isNormalAutoState 계산이 필요 없음.
        ///
        ///   슬롯0 = state.CurrentProducing (현재 생산 중인 유닛. 없으면 빈 슬롯)
        ///   슬롯1 = state.PendingQueue[0].Type (대기 1순위)
        ///   슬롯2 = state.PendingQueue[1].Type (대기 2순위)
        ///
        /// PendingQueue[2] 이상은 "아직 슬롯에 올라올 차례가 아닌 자동 대기 항목"으로 UI에 표시하지 않음.
        /// 자동 순환으로 슬롯이 비면 TryStartNext / ChargeVisibleSlots가 자동으로 채워줌.
        /// </summary>
        private void UpdateQueueSlots(ProductionState state)
        {
            if (_queueSlotImages == null) return;

            for (int i = 0; i < _queueSlotImages.Length; i++)
            {
                if (_queueSlotImages[i] == null) continue;

                UnitType? slotType = null;

                if (i == 0)
                {
                    // 슬롯 0: 현재 생산 중인 유닛 (CurrentProducing)
                    slotType = state.CurrentProducing;
                }
                else
                {
                    // 슬롯 1 → PendingQueue[0], 슬롯 2 → PendingQueue[1]
                    // PendingQueue가 해당 인덱스까지 채워져 있으면 그 타입을 표시하고,
                    // 아니면 빈 슬롯(null)으로 처리하여 UnitImage를 투명 처리한다.
                    int queueIndex = i - 1;
                    if (queueIndex < state.PendingQueue.Count)
                        slotType = state.PendingQueue[queueIndex].Type;
                }

                ApplySlotImage(i, slotType);
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

        /// <summary>
        /// 팀+종족에 맞는 초상화 스프라이트를 버튼 Image에 적용. Show() 시 호출.
        /// 종족별 UnitPortraitSet에서 slot1/slot2/slot3 스프라이트를 각 버튼에 설정.
        /// </summary>
        private void UpdateButtonPortraits(TeamId team, RaceId race)
        {
            var set = GetPortraitSet(team, race);
            if (_pistoleerButtonPortrait != null) _pistoleerButtonPortrait.sprite = set.slot1;
            if (_assaultButtonPortrait   != null) _assaultButtonPortrait.sprite   = set.slot2;
            if (_sniperButtonPortrait    != null) _sniperButtonPortrait.sprite    = set.slot3;
        }

        /// <summary>
        /// 유닛 타입에 해당하는 초상화 스프라이트. 현재 배럭 팀+종족 기준으로 세트 선택.
        /// _buttonUnitTypes[0/1/2]와 비교하여 해당 슬롯의 스프라이트를 반환.
        /// 큐 슬롯(ApplySlotImage)에서 UnitType으로 초상화를 찾을 때 사용.
        /// </summary>
        private Sprite GetPortrait(UnitType type)
        {
            if (_currentBarracks == null) return null;

            TeamId team = _currentBarracks.Team;
            RaceId race = team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            var set = GetPortraitSet(team, race);

            // _buttonUnitTypes 배열에서 슬롯 위치를 찾아 해당 슬롯의 스프라이트 반환
            // 예: type=FlameSpirit이고 _buttonUnitTypes[0]=FlameSpirit이면 → set.slot1
            if (type == _buttonUnitTypes[0]) return set.slot1;
            if (type == _buttonUnitTypes[1]) return set.slot2;
            if (type == _buttonUnitTypes[2]) return set.slot3;

            // 매칭되지 않는 타입 (다른 종족 유닛이 큐에 남아있는 경우 등) → slot1 폴백
            return set.slot1;
        }

        /// <summary>
        /// 팀+종족 조합으로 6세트 중 하나의 UnitPortraitSet을 선택하여 반환.
        /// </summary>
        private UnitPortraitSet GetPortraitSet(TeamId team, RaceId race)
        {
            if (team == TeamId.Blue)
            {
                return race switch
                {
                    RaceId.Spirit         => _blueSpiritPortraits,
                    RaceId.Transcendence  => _blueTranscendencePortraits,
                    _                     => _blueHumanPortraits
                };
            }
            else
            {
                return race switch
                {
                    RaceId.Spirit         => _redSpiritPortraits,
                    RaceId.Transcendence  => _redTranscendencePortraits,
                    _                     => _redHumanPortraits
                };
            }
        }

        /// <summary>
        /// 종족에 따라 _buttonUnitTypes 배열을 갱신.
        /// Show() 시 호출되어 버튼 3개에 바인딩될 UnitType을 결정.
        /// EventTrigger의 PointerDown 콜백이 _buttonUnitTypes[slotIndex]를 참조하므로
        /// 이 배열만 갱신하면 탭/롱프레스 시 올바른 종족의 유닛이 생산됨.
        /// </summary>
        private void BindButtonUnitTypes(RaceId race)
        {
            switch (race)
            {
                case RaceId.Spirit:
                    _buttonUnitTypes[0] = UnitType.EmberSpirit;
                    _buttonUnitTypes[1] = UnitType.FlameSpirit;
                    _buttonUnitTypes[2] = UnitType.InfernoSpirit;
                    break;
                case RaceId.Transcendence:
                    _buttonUnitTypes[0] = UnitType.FoxMagician;
                    _buttonUnitTypes[1] = UnitType.BearGuard;
                    _buttonUnitTypes[2] = UnitType.LionKnight;
                    break;
                default: // Human
                    _buttonUnitTypes[0] = UnitType.Pistoleer;
                    _buttonUnitTypes[1] = UnitType.Assault;
                    _buttonUnitTypes[2] = UnitType.Sniper;
                    break;
            }

            // 각 슬롯 버튼에 해당 유닛의 골드 비용을 텍스트로 표시
            // UnitProductionStats에서 유닛 타입별 골드 비용을 조회
            if (_slot1CostText != null)
                _slot1CostText.SetText($"{UnitProductionStats.GetGoldCost(_buttonUnitTypes[0])}");
            if (_slot2CostText != null)
                _slot2CostText.SetText($"{UnitProductionStats.GetGoldCost(_buttonUnitTypes[1])}");
            if (_slot3CostText != null)
                _slot3CostText.SetText($"{UnitProductionStats.GetGoldCost(_buttonUnitTypes[2])}");
        }
    }
}
