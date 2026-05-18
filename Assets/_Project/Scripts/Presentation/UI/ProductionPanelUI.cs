// ============================================================================
// ProductionPanelUI.cs
// 배럭(생산건물) 클릭 시 표시되는 유닛 생산 패널 UI.
//
// 변경 이력 (2026-05-17 — 건물 업그레이드 시스템 도입):
//   - 종족 단위(6개)로 묶여 있던 유닛 리스트를 BuildingType 단위 매핑으로 교체.
//   - 각 유닛에 requiredStage(해금 단계)를 추가해, 현재 건물 단계보다 높은 유닛은
//     "잠금" 상태로 표시. 잠금 유닛 탭 시 ToastKey.UpgradeRequired 토스트를 노출.
//   - 업그레이드 버튼 추가. 다음 단계 비용을 표시하고, 클릭 시 골드 검증 후
//     싱글/멀티 분기로 업그레이드 요청을 보냄.
//   - 기존 6개 종족 고정 리스트(_blueHumanUnits 등)는 호환을 위해 [Obsolete]로
//     남겨두고 주석 처리(추후 테스트 통과 시 완전 삭제 예정).
//
// 변경 이력 (2026-05-17 — ProductionPopup UI 레이아웃 재구성):
//   - 유닛 버튼 6개 → 3개로 축소. 하단 3개 슬롯은 랠리/업그레이드/철거로 분리.
//   - 철거 버튼(_demolishButton) 신설. 이번 범위는 UI와 환불 금액 표시만이며
//     실제 철거 동작은 별도 작업 예정 (OnDemolishButtonClick은 로그만 출력).
//   - 업그레이드 버튼 숨김 방식을 SetActive → CanvasGroup.alpha=0으로 교체.
//     Grid Layout Group에서 SetActive(false)는 슬롯이 사라지며 다른 버튼이
//     이동하는 부작용이 있어 레이아웃 공간을 유지하는 CanvasGroup 방식으로 전환.
//   - 업그레이드 버튼 아이콘(_upgradeIconImage)에 다음 단계 건물 Sprite를
//     런타임에 할당하기 위한 매핑 리스트(_buildingUpgradeIcons) 추가.
//   - 철거 환불 금액 = 건설 비용 50%. 초록색 텍스트로 표시(UpdateDemolishRefund).
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
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
        [Header("Popup")]
        [SerializeField] private AnimatedPanel _popup;
        [SerializeField] private SharedBackgroundButton _sharedBackground;

        [Header("Unit Buttons")]
        [SerializeField] private List<Button> _unitButtons;
        [SerializeField] private List<Image> _unitButtonPortraits;
        [SerializeField] private List<TextMeshProUGUI> _unitCostTexts;

        [Header("Auto Indicators")]
        [SerializeField] private List<GameObject> _unitAutoIndicators;

        [Header("Lock Indicators")]
        [Tooltip("각 유닛 버튼 위에 표시되는 잠금 오버레이 GameObject. 버튼 리스트와 1:1 매칭. " +
                 "현재 건물 단계보다 높은 단계의 유닛에 대해 활성화된다.")]
        [SerializeField] private List<GameObject> _unitLockIndicators;

        [Header("Unit Button Groups")]
        [Tooltip("각 유닛 버튼 GO에 부착된 CanvasGroup 목록. 유닛 버튼 리스트와 1:1 매칭. " +
                 "유닛이 없는 슬롯을 alpha=0으로 숨기되 레이아웃 공간은 유지한다.")]
        [SerializeField] private List<CanvasGroup> _unitButtonGroups;

        [Header("Queue Slots")]
        [SerializeField] private Image[] _queueSlotImages;

        /// <summary>
        /// 유닛 한 종류에 대한 초상화 + 해금 단계 정보.
        /// requiredStage: 이 유닛을 생산하려면 필요한 건물 단계(1/2/3).
        /// 현재 건물 단계 &lt; requiredStage 이면 잠금 상태로 표시된다.
        /// </summary>
        [System.Serializable]
        public struct UnitPortraitEntry
        {
            public UnitType type;
            public Sprite portrait;

            [Tooltip("이 유닛을 생산하려면 필요한 건물 단계 (1/2/3). " +
                     "예: 1단계 건물에서 생산되는 유닛은 1, 3단계에서만 생산되는 유닛은 3.")]
            public int requiredStage;
        }

        /// <summary>
        /// 한 BuildingType(라인의 한 단계)에 대한 유닛 라인업 전체.
        /// 예: TrainingCamp 항목은 근거리A 라인의 1~3단계 유닛 전부를 포함하지만,
        /// 실제 생산 가능 여부는 각 UnitPortraitEntry.requiredStage가 결정한다.
        /// </summary>
        [System.Serializable]
        public struct BuildingUnitMapping
        {
            [Tooltip("이 매핑이 대상으로 하는 건물 타입.")]
            public BuildingType buildingType;

            [Tooltip("Blue 팀이 이 건물을 사용할 때 표시되는 유닛 라인업 (해금 단계 포함 전체).")]
            public List<UnitPortraitEntry> blueUnits;

            [Tooltip("Red 팀이 이 건물을 사용할 때 표시되는 유닛 라인업 (해금 단계 포함 전체).")]
            public List<UnitPortraitEntry> redUnits;
        }

        [Header("Building → Unit Mappings (신규 — BuildingType 단위)")]
        [Tooltip("각 BuildingType에 대해 어떤 유닛을 생산할 수 있는지 정의. " +
                 "현재 건물 단계보다 높은 requiredStage 유닛은 자동으로 잠금 표시된다.")]
        [SerializeField] private List<BuildingUnitMapping> _buildingUnitMappings;

        // ────────────────────────────────────────────────────────────────────
        // [Deprecated] 종족별 고정 유닛 리스트 — 빌딩별 매핑으로 대체됨.
        // 테스트 통과 후 완전 삭제 예정 (Inspector 데이터 마이그레이션 확인 필요).
        // ────────────────────────────────────────────────────────────────────
        // [Header("Unit Entries — 종족별 유닛 설정 (DEPRECATED)")]
        // [SerializeField] private List<UnitPortraitEntry> _blueHumanUnits;
        // [SerializeField] private List<UnitPortraitEntry> _blueSpiritUnits;
        // [SerializeField] private List<UnitPortraitEntry> _blueTranscendenceUnits;
        // [SerializeField] private List<UnitPortraitEntry> _redHumanUnits;
        // [SerializeField] private List<UnitPortraitEntry> _redSpiritUnits;
        // [SerializeField] private List<UnitPortraitEntry> _redTranscendenceUnits;

        [Header("Header")]
        [Tooltip("팝업 상단에 현재 건물 이름을 표시하는 텍스트. Show() 호출 시 BuildingType.ToString()으로 갱신된다.")]
        [SerializeField] private TextMeshProUGUI _headerText;

        [Header("Progress")]
        [SerializeField] private Image _progressFill;

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _populationText;

        [Header("Buttons")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _rallyPointButton;

        [Header("Upgrade")]
        [Tooltip("건물 업그레이드 버튼. 최고 단계 건물에서는 자동으로 숨김 처리.")]
        [SerializeField] private Button _upgradeButton;

        [Tooltip("업그레이드 비용 표시 텍스트.")]
        [SerializeField] private TextMeshProUGUI _upgradeCostText;

        [Header("Action Buttons")]
        [Tooltip("철거 버튼. 클릭 로직은 별도 작업 예정.")]
        [SerializeField] private Button _demolishButton;

        [Tooltip("업그레이드 버튼에 표시될 아이콘 Image. 다음 단계 건물 Sprite를 런타임에 할당.")]
        [SerializeField] private Image _upgradeIconImage;

        [Tooltip("업그레이드 버튼에 부착된 CanvasGroup. alpha=0으로 숨겨도 레이아웃 공간 유지.")]
        [SerializeField] private CanvasGroup _upgradeButtonGroup;

        [Tooltip("철거 버튼 하단에 표시되는 환불 금액 텍스트. 초록색으로 표시.")]
        [SerializeField] private TextMeshProUGUI _demolishRefundText;

        /// <summary>
        /// BuildingType과 해당 건물의 아이콘 Sprite를 연결하는 구조체.
        /// 업그레이드 버튼에 다음 단계 건물 이미지를 설정할 때 사용한다.
        /// </summary>
        [System.Serializable]
        public struct BuildingIconEntry
        {
            [Tooltip("대상 건물 타입.")]
            public BuildingType buildingType;
            [Tooltip("Blue 팀 업그레이드 버튼에 표시될 건물 아이콘 Sprite.")]
            public Sprite blueIcon;
            [Tooltip("Red 팀 업그레이드 버튼에 표시될 건물 아이콘 Sprite.")]
            public Sprite redIcon;
        }

        [Header("Building Icons (업그레이드 아이콘용)")]
        [Tooltip("BuildingType별 건물 아이콘 목록. 업그레이드 버튼에 다음 단계 건물 이미지를 설정할 때 조회한다.")]
        [SerializeField] private List<BuildingIconEntry> _buildingUpgradeIcons;

        // ====================================================================
        // 의존성 + 내부 상태
        // ====================================================================

        private UnitProductionUseCase _production;
        private ResourceUseCase _resource;
        private PopulationUseCase _population;
        private ProductionTicker _ticker;
        private BuildingPlacementUseCase _buildingPlacement;
        private NetworkProductionController _networkProductionController;
        private NetworkBuildingController _networkBuildingController;
        private BuildingData _currentBarracks;

        public bool IsOpen => _popup != null && _popup.IsVisible;
        public int ClosedFrame { get; private set; } = -1;
        public bool IsSettingRallyPoint { get; private set; }
        public int RallyPointSetFrame { get; private set; }
        public int CurrentBarracksId => _currentBarracks?.Id ?? -1;

        /// <summary>
        /// 현재 표시 중인 버튼 슬롯에 바인딩된 UnitType 리스트.
        /// 잠금 여부와 무관하게 모든 슬롯(라인의 전체 유닛)이 포함된다.
        /// </summary>
        private List<UnitType> _activeUnitTypes = new List<UnitType>();

        /// <summary>
        /// 슬롯 인덱스 i 의 유닛이 현재 잠금 상태인지.
        /// _activeUnitTypes와 동일한 인덱스를 공유한다.
        /// </summary>
        private List<bool> _activeUnitLocks = new List<bool>();

        private float _pointerDownTime;
        private bool _isPointerDown;
        private const float LongPressThreshold = 0.5f;
        private bool _longPressTriggered;
        private UnitType _activeUnitType;

        // ────────────────────────────────────────────────────────────────────
        // 생산 실패 사유 — UI 피드백을 위해 OnUnitTap이 어떤 검증에서 실패했는지 분류.
        // ────────────────────────────────────────────────────────────────────
        private enum ProductionFailReason
        {
            None,
            GoldInsufficient,
            PopulationFull,
            QueueFull
        }

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// UseCase 의존성 주입 + 버튼 이벤트 연결.
        /// 신규 파라미터:
        ///   - buildingPlacement: 싱글플레이 업그레이드 실행에 사용.
        ///   - networkBuildingController: 멀티플레이 업그레이드 RPC 송신에 사용.
        /// 기존 호출자 호환을 위해 두 매개변수 모두 기본값 null.
        /// </summary>
        public void Initialize(UnitProductionUseCase production,
            ResourceUseCase resource, PopulationUseCase population,
            ProductionTicker ticker,
            NetworkProductionController networkProductionController = null,
            BuildingPlacementUseCase buildingPlacement = null,
            NetworkBuildingController networkBuildingController = null)
        {
            _production = production;
            _resource = resource;
            _population = population;
            _ticker = ticker;
            _networkProductionController = networkProductionController;
            _buildingPlacement = buildingPlacement;
            _networkBuildingController = networkBuildingController;

            if (_cancelButton != null) _cancelButton.onClick.AddListener(Close);
            if (_rallyPointButton != null) _rallyPointButton.onClick.AddListener(OnRallyPointClick);
            if (_upgradeButton != null) _upgradeButton.onClick.AddListener(OnUpgradeButtonClick);

            // 철거 버튼 이벤트 연결 (실제 철거 로직은 별도 작업 예정)
            if (_demolishButton != null)
                _demolishButton.onClick.AddListener(OnDemolishButtonClick);

            if (_unitButtons != null)
            {
                for (int i = 0; i < _unitButtons.Count; i++) SetupUnitButtonBySlot(_unitButtons[i], i);
            }

            SetupQueueSlotButtons();

            GameEvents.OnProductionQueueChanged.Subscribe(_ => UpdateUI()).AddTo(this);
            GameEvents.OnResourceChanged.Subscribe(_ => UpdateInfoBar()).AddTo(this);
        }

        public void Show(BuildingData barracks)
        {
            _currentBarracks = barracks;

            // 건물 이름 표시: BuildingType의 enum 이름을 그대로 사용한다.
            // 예: BuildingType.Garage → "Garage", BuildingType.TrainingCamp → "TrainingCamp"
            if (_headerText != null)
                _headerText.text = barracks.Type.ToString();

            IsSettingRallyPoint = false;
            _popup?.Show();
            _sharedBackground?.Register(Close);

            if (_ticker != null) _ticker.ShowRallyMarker(barracks.Id);

            RaceId race = (barracks.Team == TeamId.Blue) ? GameRaceContext.BlueRace : GameRaceContext.RedRace;
            Debug.Log($"[ProductionUI] Show - Team: {barracks.Team}, Race: {race}, BuildingType: {barracks.Type}, Stage: {barracks.Stage}");

            BindButtonUnitTypes(race);
            UpdateButtonPortraits(barracks.Team, race);
            UpdateLockIndicators();
            UpdateUpgradeButton(race);
            UpdateDemolishRefund(race);
            UpdateUI();
        }

        public void Close()
        {
            ClosedFrame = Time.frameCount;
            IsSettingRallyPoint = false;
            _sharedBackground?.Unregister();
            if (_ticker != null) _ticker.HideAllRallyMarkers();
            _popup?.Hide();
            _currentBarracks = null;
        }

        public void OnGameEnded() => Close();
        public void OnGameStarted() => Close();

        // ====================================================================
        // 유닛 버튼 입력 처리
        // ====================================================================

        private void SetupUnitButtonBySlot(Button button, int slotIndex)
        {
            if (button == null) return;
            var trigger = button.gameObject.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();

            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener(_ => {
                if (slotIndex < _activeUnitTypes.Count) OnUnitPointerDown(_activeUnitTypes[slotIndex], slotIndex);
            });
            trigger.triggers.Add(downEntry);

            var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener(_ => OnUnitPointerUp());
            trigger.triggers.Add(upEntry);

            button.onClick.RemoveAllListeners();
        }

        private void OnUnitPointerDown(UnitType type, int slotIndex)
        {
            // 잠금 유닛은 길게 누르기(자동 생산 토글)도 막는다. 짧은 탭에서 토스트로 안내.
            _activeUnitType = type;
            _pointerDownTime = Time.unscaledTime;
            _isPointerDown = true;
            _longPressTriggered = false;
        }

        private void OnUnitPointerUp()
        {
            if (!_isPointerDown) return;
            _isPointerDown = false;
            if (!_longPressTriggered) OnUnitTap(_activeUnitType);
        }

        private void Update()
        {
            if (_isPointerDown && !_longPressTriggered && (Time.unscaledTime - _pointerDownTime >= LongPressThreshold))
            {
                _longPressTriggered = true;
                OnUnitLongPress(_activeUnitType);
            }
            if (IsOpen && _currentBarracks != null) UpdateProgressBar();
        }

        private void SetupQueueSlotButtons()
        {
            if (_queueSlotImages == null) return;
            for (int i = 0; i < _queueSlotImages.Length; i++)
            {
                if (_queueSlotImages[i] == null) continue;
                var button = _queueSlotImages[i].GetComponent<Button>() ?? _queueSlotImages[i].transform.parent?.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    int idx = i;
                    button.onClick.AddListener(() => OnQueueSlotClicked(idx));
                }
            }
        }

        private void OnQueueSlotClicked(int slotIndex)
        {
            if (_currentBarracks == null || _production == null) return;
            if (_networkProductionController != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                _networkProductionController.CancelSlotServerRpc(_currentBarracks.Id, slotIndex, (int)_currentBarracks.Team);
                return;
            }
            _production.CancelQueueAt(_currentBarracks.Id, slotIndex);
        }

        // ====================================================================
        // 유닛 탭 → 생산 등록 / 잠금 안내
        // ====================================================================

        private void OnUnitTap(UnitType type)
        {
            if (_currentBarracks == null || _production == null) return;

            // ── 잠금 유닛 탭 시 토스트 후 종료 ─────────────────────────
            // 현재 건물 단계가 유닛 해금 단계에 못 미치면 생산이 불가하므로,
            // 사용자에게 "업그레이드 필요" 토스트를 표시하고 즉시 리턴.
            if (IsUnitLocked(type))
            {
                ToastUI.Show(ToastKey.UpgradeRequired);
                return;
            }

            var state = _production.GetState(_currentBarracks.Id);

            // 자동 생산 중인 타입을 다시 탭한 경우 → 자동 토글(해제) 분기.
            if (state != null && state.IsAutoMode && state.AutoTypes.Contains(type))
            {
                HandleToggleAuto(type);
                return;
            }

            // 사전 검증 (UI 피드백용). 우선순위: 큐 상한 > 골드 > 인구.
            ProductionFailReason reason = ValidateProduction(state, type);
            if (reason != ProductionFailReason.None)
            {
                HandleProductionFail(reason);
                return;
            }

            // 검증 통과 — 실제 등록을 위임.
            if (_networkProductionController != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                _networkProductionController.RequestEnqueueServerRpc(_currentBarracks.Id, (int)type, (int)_currentBarracks.Team);
            else
                _production.EnqueueUnit(_currentBarracks.Id, type);
        }

        /// <summary>
        /// 슬롯에 바인딩된 유닛 타입이 현재 건물 단계 기준으로 잠금 상태인지 판정.
        /// _currentBarracks.Stage &lt; requiredStage 이면 true.
        /// </summary>
        private bool IsUnitLocked(UnitType type)
        {
            if (_currentBarracks == null) return false;
            // _activeUnitTypes / _activeUnitLocks 는 BindButtonUnitTypes에서 동기 갱신.
            for (int i = 0; i < _activeUnitTypes.Count; i++)
            {
                if (_activeUnitTypes[i] == type)
                    return i < _activeUnitLocks.Count && _activeUnitLocks[i];
            }
            return false;
        }

        private ProductionFailReason ValidateProduction(ProductionState state, UnitType type)
        {
            if (state == null) return ProductionFailReason.None;

            int slotsUsed = (state.CurrentProducing.HasValue ? 1 : 0) + state.ChargedPendingCount();
            if (slotsUsed + 1 > ProductionState.MaxQueueSize)
                return ProductionFailReason.QueueFull;

            int cost = UnitProductionStats.GetGoldCost(type);
            if (_resource != null && !_resource.CanAfford(state.Team, cost))
                return ProductionFailReason.GoldInsufficient;

            int popCost = UnitProductionStats.GetPopulationCost(type);
            if (_population != null && !_population.HasPopulation(state.Team, popCost))
                return ProductionFailReason.PopulationFull;

            return ProductionFailReason.None;
        }

        private void HandleProductionFail(ProductionFailReason reason)
        {
            switch (reason)
            {
                case ProductionFailReason.GoldInsufficient:
                    ToastUI.Show(ToastKey.GoldInsufficient);
                    break;

                case ProductionFailReason.PopulationFull:
                    ToastUI.Show(ToastKey.PopulationFull);
                    break;

                case ProductionFailReason.QueueFull:
                    ToastUI.Show(ToastKey.ProductionQueueFull);
                    break;
            }
        }

        private void OnUnitLongPress(UnitType type)
        {
            // 잠금 유닛에 대해서는 자동 생산 토글도 막는다. 안내만.
            if (IsUnitLocked(type))
            {
                ToastUI.Show(ToastKey.UpgradeRequired);
                return;
            }
            HandleToggleAuto(type);
        }

        private void HandleToggleAuto(UnitType type)
        {
            if (_networkProductionController != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                _networkProductionController.ToggleAutoServerRpc(_currentBarracks.Id, (int)type, (int)_currentBarracks.Team);
            else
                _production.ToggleAutoProduction(_currentBarracks.Id, type);
        }

        private void OnRallyPointClick() { IsSettingRallyPoint = true; RallyPointSetFrame = Time.frameCount; _popup?.Hide(); }

        public void CompleteRallyPointSetting(HexCoord target)
        {
            if (_currentBarracks == null || _production == null) return;
            if (_networkProductionController != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                _networkProductionController.SetRallyPointServerRpc(_currentBarracks.Id, target.Q, target.R, (int)_currentBarracks.Team);
            _production.SetRallyPoint(_currentBarracks.Id, target);
            IsSettingRallyPoint = false;
            _currentBarracks = null;
        }

        // ====================================================================
        // 업그레이드 처리
        // ====================================================================

        /// <summary>
        /// 현재 건물의 업그레이드 가능 여부에 따라 업그레이드 버튼/비용 텍스트를 갱신.
        ///   - 다음 단계 없음 → 버튼 GameObject 비활성화 (비용 텍스트도 함께 숨김)
        ///   - 다음 단계 있음 → 버튼 활성화 + 비용 텍스트에 골드 표시
        /// 호출: Show()에서 1회, 업그레이드 완료 시 BuildingFactory가 GO를 갈아끼우면
        /// 패널 자체는 닫혀 있으므로 별도 갱신 불필요.
        /// </summary>
        private void UpdateUpgradeButton(RaceId race)
        {
            if (_currentBarracks == null) return;

            bool canUpgrade = BuildingTypeHelper.CanUpgrade(_currentBarracks.Type);

            // CanvasGroup으로 숨김 — SetActive 대신 alpha=0을 사용해 레이아웃 공간을 유지한다.
            // SetActive(false)를 쓰면 Grid Layout에서 해당 슬롯이 사라져 다른 버튼 위치가 이동한다.
            if (_upgradeButtonGroup != null)
            {
                _upgradeButtonGroup.alpha = canUpgrade ? 1f : 0f;
                _upgradeButtonGroup.blocksRaycasts = canUpgrade;
                _upgradeButtonGroup.interactable = canUpgrade;
            }

            // 업그레이드 가능한 경우: 다음 단계 건물 아이콘을 버튼 이미지에 설정한다.
            if (canUpgrade && _upgradeIconImage != null)
            {
                BuildingType? nextType = BuildingTypeHelper.GetNextStage(_currentBarracks.Type);
                if (nextType.HasValue)
                {
                    Sprite icon = GetBuildingIcon(nextType.Value, _currentBarracks.Team);
                    if (icon != null)
                        _upgradeIconImage.sprite = icon;
                }
            }

            if (_upgradeCostText != null)
            {
                if (canUpgrade)
                {
                    int cost = BuildingStats.GetUpgradeCost(_currentBarracks.Type);
                    _upgradeCostText.text = $"{cost}";
                    // 보유 골드 부족 시 비용 텍스트 빨간색으로 표시 (배치 패널 패턴과 동일)
                    int currentGold = _resource != null ? _resource.GetGold(_currentBarracks.Team) : int.MaxValue;
                    _upgradeCostText.color = (currentGold < cost) ? Color.red : Color.white;
                    _upgradeCostText.gameObject.SetActive(true);
                }
                else
                {
                    _upgradeCostText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 업그레이드 버튼 클릭 핸들러.
        /// 흐름:
        ///   1) 현재 건물이 실제로 업그레이드 가능한지 재확인 (CanUpgrade)
        ///   2) 골드 검증 → 부족 시 ToastKey.GoldInsufficient
        ///   3) 멀티플레이면 RequestUpgradeServerRpc 호출, 싱글플레이면 UseCase 직접 실행
        ///   4) 패널 닫기 (BuildingFactory가 GO를 교체할 동안 깔끔하게 정리)
        /// </summary>
        private void OnUpgradeButtonClick()
        {
            if (_currentBarracks == null) return;
            if (!BuildingTypeHelper.CanUpgrade(_currentBarracks.Type)) return;

            int cost = BuildingStats.GetUpgradeCost(_currentBarracks.Type);
            if (_resource != null && !_resource.CanAfford(_currentBarracks.Team, cost))
            {
                ToastUI.Show(ToastKey.GoldInsufficient);
                return;
            }

            bool isNetworkMode = _networkBuildingController != null
                && NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening;

            if (isNetworkMode)
            {
                // 서버에 업그레이드 요청 — 골드 차감/검증은 서버에서 다시 수행.
                _networkBuildingController.RequestUpgradeServerRpc(_currentBarracks.Id);
            }
            else
            {
                // 싱글플레이: 직접 골드 차감 + UseCase 실행
                if (_resource != null) _resource.SpendGold(_currentBarracks.Team, cost);
                RaceId singleRace = (_currentBarracks.Team == TeamId.Blue)
                    ? GameRaceContext.BlueRace
                    : GameRaceContext.RedRace;
                _buildingPlacement?.UpgradeBuilding(_currentBarracks.Id, singleRace);
            }

            // 업그레이드 후 새 건물 인스턴스는 다음 클릭에서 다시 패널을 열도록 함.
            Close();
        }

        // ====================================================================
        // 철거 / 환불 표시
        // ====================================================================

        /// <summary>
        /// 철거 버튼 하단에 환불 예정 금액을 표시한다.
        /// 환불 금액 = 건설 비용의 50%. 텍스트 색상은 초록색으로 고정한다.
        /// 실제 철거 로직은 이번 범위 외이므로 금액 표시만 처리한다.
        /// </summary>
        private void UpdateDemolishRefund(RaceId race)
        {
            if (_demolishRefundText == null || _currentBarracks == null) return;

            // 1단계 건설비 + 현재 단계까지의 업그레이드 비용 합산의 50%를 환불한다.
            // 누적 투자 비용은 GameBootstrapper 초기화 시 1회 계산되어 BuildingStats에 캐싱된다.
            // 2단계 건물 예: (1단계 건설비 + 1→2 업그레이드비) / 2
            // 3단계 건물 예: (1단계 건설비 + 1→2 업그레이드비 + 2→3 업그레이드비) / 2
            int totalInvested = BuildingStats.GetTotalInvestedCost(_currentBarracks.Type, race);
            int refund = totalInvested / 2;

            _demolishRefundText.text = $"{refund}";
            // 환불 금액임을 시각적으로 구분하기 위해 초록색으로 표시한다.
            _demolishRefundText.color = Color.green;
        }

        /// <summary>
        /// _buildingUpgradeIcons 리스트에서 해당 BuildingType에 매핑된 아이콘 Sprite를 반환한다.
        /// team 파라미터로 블루팀/레드팀에 맞는 Sprite를 선택한다.
        /// 매핑이 없으면 null을 반환한다 (호출자가 null 체크 후 처리).
        /// </summary>
        private Sprite GetBuildingIcon(BuildingType type, TeamId team)
        {
            if (_buildingUpgradeIcons == null) return null;
            foreach (var entry in _buildingUpgradeIcons)
            {
                if (entry.buildingType == type)
                    return team == TeamId.Blue ? entry.blueIcon : entry.redIcon;
            }
            return null;
        }

        /// <summary>
        /// 철거 버튼 클릭 핸들러.
        ///
        /// 처리 흐름 (멀티플레이):
        ///   1) RequestDemolishServerRpc 호출 → 서버에서 소유권 검증 후
        ///      CancelAllQueue(큐 취소 + 환불) → AddGold(건설비 50% 환불) → RemoveBuilding 실행
        ///   2) DemolishBuildingClientRpc로 모든 클라이언트에 동기화
        ///
        /// 처리 흐름 (싱글플레이):
        ///   1) CancelAllQueue → 생산 큐 전체 취소 + 이미 차감된 골드 환불
        ///   2) AddGold → 건설 비용 50% 환불
        ///   3) RemoveBuilding → 건물 도메인 상태 제거 + 이벤트 발행 (프리팹 제거)
        ///
        /// 마지막으로 Close()를 호출하여 팝업을 닫는다.
        ///
        /// 근거: GameSystemRules.md — 건물 철거 시스템 규칙 2, 3, 4, 5
        /// </summary>
        private void OnDemolishButtonClick()
        {
            // 현재 건물 데이터가 없으면 동작 중단
            if (_currentBarracks == null) return;

            // 멀티플레이 모드인지 확인 (NetworkManager가 활성화된 상태)
            bool isNetworkMode = _networkBuildingController != null
                && NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening;

            if (isNetworkMode)
            {
                // ── 멀티플레이: 서버에 철거 요청을 보낸다 ──────────────────────
                // 소유권 검증, 큐 취소/환불, 골드 환불, RemoveBuilding, 클라이언트 동기화를
                // 서버(NetworkBuildingController)가 모두 처리한다.
                _networkBuildingController.RequestDemolishServerRpc(_currentBarracks.Id);
            }
            else
            {
                // ── 싱글플레이: UseCase를 직접 호출 ────────────────────────────

                // 1) 생산 건물이면 생산 큐 전체 취소 + 이미 차감된 골드 전액 환불
                //    (CancelAllQueue 내부에서 환불 처리가 함께 이루어진다)
                if (BuildingTypeHelper.IsProductionBuilding(_currentBarracks.Type) && _production != null)
                    _production.CancelAllQueue(_currentBarracks.Id);

                // 2) 건설 비용 50% 환불
                //    누적 투자 비용(건설비 + 업그레이드 비용 합산)의 절반을 돌려준다.
                if (_resource != null)
                {
                    RaceId race = (_currentBarracks.Team == TeamId.Blue)
                        ? GameRaceContext.BlueRace
                        : GameRaceContext.RedRace;
                    int totalInvested = BuildingStats.GetTotalInvestedCost(_currentBarracks.Type, race);
                    int refund = totalInvested / 2;
                    _resource.AddGold(_currentBarracks.Team, refund);
                }

                // 3) 건물 도메인 상태 제거
                // DemolishBuilding = OnBuildingDied 발행(BuildingFactory가 GO 제거) + RemoveBuilding(도메인 딕셔너리 제거 + 타일 복구)
                _buildingPlacement?.DemolishBuilding(_currentBarracks.Id);
            }

            // 팝업 닫기 (건물이 사라지므로 패널을 유지할 이유가 없음)
            Close();
        }

        // ====================================================================
        // UI 갱신
        // ====================================================================

        private void UpdateUI()
        {
            if (_currentBarracks == null || _production == null) return;
            var state = _production.GetState(_currentBarracks.Id);
            if (state == null) return;

            if (_unitAutoIndicators != null)
            {
                for (int i = 0; i < _unitAutoIndicators.Count; i++)
                {
                    if (i < _activeUnitTypes.Count && _unitAutoIndicators[i] != null)
                        _unitAutoIndicators[i].SetActive(state.AutoTypes.Contains(_activeUnitTypes[i]));
                    else if (_unitAutoIndicators[i] != null)
                        _unitAutoIndicators[i].SetActive(false);
                }
            }
            UpdateQueueSlots(state);
            UpdateProgressBar();
            UpdateInfoBar();
        }

        /// <summary>
        /// 잠금 인디케이터 GameObject 활성/비활성 갱신.
        /// _activeUnitLocks[i] 가 true면 오버레이 On, false면 Off.
        /// 슬롯에 유닛이 바인딩되지 않은 경우는 잠금도 끈다.
        /// </summary>
        private void UpdateLockIndicators()
        {
            if (_unitLockIndicators == null) return;
            for (int i = 0; i < _unitLockIndicators.Count; i++)
            {
                if (_unitLockIndicators[i] == null) continue;
                bool show = i < _activeUnitLocks.Count
                            && i < _activeUnitTypes.Count
                            && _activeUnitLocks[i];
                _unitLockIndicators[i].SetActive(show);
            }
        }

        private void UpdateQueueSlots(ProductionState state)
        {
            if (_queueSlotImages == null) return;
            for (int i = 0; i < _queueSlotImages.Length; i++)
            {
                if (_queueSlotImages[i] == null) continue;
                UnitType? slotType = (i == 0) ? state.CurrentProducing : (i - 1 < state.PendingQueue.Count ? state.PendingQueue[i - 1].Type : (UnitType?)null);
                ApplySlotImage(i, slotType);
            }
        }

        private void ApplySlotImage(int slotIndex, UnitType? slotType)
        {
            if (slotIndex < 0 || slotIndex >= _queueSlotImages.Length || _queueSlotImages[slotIndex] == null) return;
            if (slotType.HasValue) { _queueSlotImages[slotIndex].sprite = GetPortrait(slotType.Value); _queueSlotImages[slotIndex].color = Color.white; }
            else { _queueSlotImages[slotIndex].sprite = null; _queueSlotImages[slotIndex].color = new Color(1, 1, 1, 0); }
        }

        private void UpdateProgressBar() { if (_progressFill != null && _currentBarracks != null && _production != null) _progressFill.fillAmount = _production.GetState(_currentBarracks.Id)?.Progress ?? 0f; }

        private void UpdateInfoBar()
        {
            if (_currentBarracks == null) return;

            // ── 보유 골드 텍스트 갱신 ──
            if (_goldText != null && _resource != null)
                _goldText.text = _resource.GetGold(_currentBarracks.Team).ToString();

            // ── 각 유닛 생산 비용 텍스트 색상 재평가 ──
            if (_resource != null && _unitCostTexts != null)
            {
                int currentGold = _resource.GetGold(_currentBarracks.Team);
                for (int i = 0; i < _unitCostTexts.Count; i++)
                {
                    if (_unitCostTexts[i] == null) continue;
                    if (i >= _activeUnitTypes.Count)
                    {
                        _unitCostTexts[i].color = Color.white;
                        continue;
                    }
                    int cost = UnitProductionStats.GetGoldCost(_activeUnitTypes[i]);
                    _unitCostTexts[i].color = (currentGold < cost) ? Color.red : Color.white;
                }
            }

            // ── 업그레이드 비용 텍스트도 골드 변경 시 함께 색상 재평가 ──
            if (_upgradeCostText != null && _upgradeCostText.gameObject.activeSelf
                && BuildingTypeHelper.CanUpgrade(_currentBarracks.Type))
            {
                int currentGold = _resource != null ? _resource.GetGold(_currentBarracks.Team) : int.MaxValue;
                int upCost = BuildingStats.GetUpgradeCost(_currentBarracks.Type);
                _upgradeCostText.color = (currentGold < upCost) ? Color.red : Color.white;
            }

            // ── 인구 텍스트 갱신 ──
            if (_populationText != null && _population != null)
                _populationText.text = $"{_population.GetUsedPopulation(_currentBarracks.Team)}/{_population.GetMaxPopulation(_currentBarracks.Team)}";
        }

        private void UpdateButtonPortraits(TeamId team, RaceId race)
        {
            var list = GetUnitEntriesForCurrentBuilding(team);
            if (_unitButtonPortraits == null) return;

            // 유닛이 정확히 2종류인 건물은 [유닛1][빈슬롯][유닛2] 배치를 사용한다.
            // portrait[0] = 첫 번째 유닛, portrait[1] = 스킵(빈 슬롯), portrait[2] = 두 번째 유닛.
            // 이 배치를 모르고 portrait[i] = list[i]로 넣으면 슬롯2 초상화가 갱신되지 않아
            // 이전 건물의 초상화가 그대로 남는 버그가 생긴다.
            bool twoUnitLayout = (list.Count == 2);

            for (int i = 0; i < _unitButtonPortraits.Count; i++)
            {
                if (_unitButtonPortraits[i] == null) continue;

                if (twoUnitLayout)
                {
                    // 2유닛 배치: slot0 = list[0], slot1 = 스킵, slot2 = list[1]
                    if (i == 0)
                        _unitButtonPortraits[i].sprite = list[0].portrait;
                    else if (i == 2)
                        _unitButtonPortraits[i].sprite = list[1].portrait;
                    // slot1(i==1)은 숨겨진 더미 슬롯 — 초상화 갱신 불필요
                }
                else
                {
                    // 유닛이 1개 또는 3개인 경우: 기존 동작 유지
                    if (i < list.Count)
                        _unitButtonPortraits[i].sprite = list[i].portrait;
                }
            }
        }

        private Sprite GetPortrait(UnitType type)
        {
            if (_currentBarracks == null) return null;
            var list = GetUnitEntriesForCurrentBuilding(_currentBarracks.Team);
            foreach (var entry in list) if (entry.type == type) return entry.portrait;
            return (list.Count > 0) ? list[0].portrait : null;
        }

        /// <summary>
        /// 현재 _currentBarracks에 매핑된 유닛 엔트리 리스트를 반환.
        /// 매핑이 없으면 빈 리스트를 반환 (안전 폴백 — 잘못된 BuildingType이거나
        /// Inspector에서 매핑이 누락된 경우 빈 버튼 슬롯이 표시됨).
        /// </summary>
        private List<UnitPortraitEntry> GetUnitEntriesForCurrentBuilding(TeamId team)
        {
            if (_currentBarracks == null || _buildingUnitMappings == null)
                return new List<UnitPortraitEntry>();

            foreach (var mapping in _buildingUnitMappings)
            {
                if (mapping.buildingType != _currentBarracks.Type) continue;
                return team == TeamId.Blue
                    ? (mapping.blueUnits ?? new List<UnitPortraitEntry>())
                    : (mapping.redUnits ?? new List<UnitPortraitEntry>());
            }

            // 매핑 누락 — 콘솔 경고 후 빈 리스트 반환
            Debug.LogWarning($"[ProductionPanelUI] BuildingType={_currentBarracks.Type}에 대한 유닛 매핑이 없습니다.");
            return new List<UnitPortraitEntry>();
        }

        /// <summary>
        /// 현재 건물에 대응하는 유닛 라인업 전체를 버튼 슬롯에 바인딩.
        /// 잠금 여부도 동시에 계산하여 _activeUnitLocks 에 저장.
        /// 잠금 유닛은 버튼은 활성 상태로 두고(클릭은 가능 — 토스트 안내용),
        /// 시각적 잠금 처리는 별도 _unitLockIndicators 가 담당.
        /// </summary>
        private void BindButtonUnitTypes(RaceId race)
        {
            if (_currentBarracks == null) return;
            int currentStage = _currentBarracks.Stage;

            var list = GetUnitEntriesForCurrentBuilding(_currentBarracks.Team);
            _activeUnitTypes.Clear();
            _activeUnitLocks.Clear();

            // 2유닛 특수 배치: [유닛1][빈슬롯][유닛2] — 슬롯 수(3개)에 맞게 리스트를 구성한다.
            // 빈 슬롯(인덱스1)에는 더미 UnitType을 삽입하여 slotIndex 접근 시 IndexOutOfRange를 방지한다.
            // 슬롯1은 CanvasGroup.alpha=0으로 차단되므로 더미 값이 실제로 사용되지 않는다.
            bool twoUnitLayout = (list.Count == 2);

            if (twoUnitLayout)
            {
                // 슬롯0 — 첫 번째 유닛
                _activeUnitTypes.Add(list[0].type);
                _activeUnitLocks.Add(list[0].requiredStage > 0 && currentStage < list[0].requiredStage);
                // 슬롯1 — 더미 (실제 사용 안 됨, CanvasGroup으로 클릭 차단)
                _activeUnitTypes.Add(list[0].type);
                _activeUnitLocks.Add(false);
                // 슬롯2 — 두 번째 유닛
                _activeUnitTypes.Add(list[1].type);
                _activeUnitLocks.Add(list[1].requiredStage > 0 && currentStage < list[1].requiredStage);
            }
            else
            {
                foreach (var entry in list)
                {
                    _activeUnitTypes.Add(entry.type);
                    // 단계가 모자란 유닛은 잠금. requiredStage 미설정(0) 시 항상 해금으로 간주.
                    bool locked = entry.requiredStage > 0 && currentStage < entry.requiredStage;
                    _activeUnitLocks.Add(locked);
                }
            }

            if (_unitButtons != null)
            {
                for (int i = 0; i < _unitButtons.Count; i++)
                {
                    // 2유닛 특수 배치: 슬롯0(유닛1), 슬롯1(빈), 슬롯2(유닛2)
                    // 2유닛 외: 유닛이 있는 슬롯만 표시
                    bool hasUnit = twoUnitLayout ? (i == 0 || i == 2) : (i < list.Count);

                    // CanvasGroup으로 표시/숨김 (SetActive 대신 사용, 레이아웃 공간 유지)
                    // SetActive(false)는 Grid Layout에서 슬롯 공간 자체가 사라지므로 사용하지 않는다.
                    if (_unitButtonGroups != null && i < _unitButtonGroups.Count && _unitButtonGroups[i] != null)
                    {
                        _unitButtonGroups[i].alpha = hasUnit ? 1f : 0f;
                        _unitButtonGroups[i].blocksRaycasts = hasUnit;
                        _unitButtonGroups[i].interactable = hasUnit;
                    }
                    else
                    {
                        // CanvasGroup 미연결 시 기존 SetActive 방식으로 폴백
                        _unitButtons[i].gameObject.SetActive(hasUnit);
                    }

                    // 비용 텍스트: 유닛이 있는 슬롯만 갱신
                    if (hasUnit && i < _unitCostTexts.Count && _unitCostTexts[i] != null && i < _activeUnitTypes.Count)
                        _unitCostTexts[i].text = $"{UnitProductionStats.GetGoldCost(_activeUnitTypes[i])}";
                }
            }
        }
    }
}
