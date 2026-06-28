// ============================================================================
// ProductionPanelUI.cs
// 배럭(생산건물) 클릭 시 표시되는 유닛 생산 패널 UI.
//
// 변경 이력 (2026-05-18 — BuildingPanelBase 도입):
//   - 공통 요소(팝업/외부탭/헤더/닫기/철거/환불)를 BuildingPanelBase로 이동.
//   - _currentBarracks 필드를 베이스의 _currentBuilding으로 통합.
//   - Show/Close 메서드는 베이스에 위임하고 OnShow/OnBeforeClose 훅만 사용.
//   - 철거 흐름은 베이스의 OnDemolishButtonClick + BeforeDemolish 훅 패턴으로 통합.
//
// 변경 이력 (2026-05-17 — 건물 업그레이드 시스템 도입):
//   - 종족 단위(6개)로 묶여 있던 유닛 리스트를 BuildingType 단위 매핑으로 교체.
//   - 각 유닛에 requiredStage(해금 단계)를 추가해, 현재 건물 단계보다 높은 유닛은
//     "잠금" 상태로 표시. 잠금 유닛 탭 시 ToastKey.UpgradeRequired 토스트를 노출.
//   - 업그레이드 버튼 추가. 다음 단계 비용을 표시하고, 클릭 시 골드 검증 후
//     싱글/멀티 분기로 업그레이드 요청을 보냄.
//
// 변경 이력 (2026-05-17 — ProductionPopup UI 레이아웃 재구성):
//   - 유닛 버튼 6개 → 3개로 축소. 하단 3개 슬롯은 랠리/업그레이드/철거로 분리.
//   - 업그레이드 버튼 숨김 방식을 SetActive → CanvasGroup.alpha=0으로 교체.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;
using TMPro;

namespace Hexiege.Presentation
{
    public class ProductionPanelUI : BuildingPanelBase
    {
        [Header("Unit Buttons")]
        [SerializeField] private List<Button> _unitButtons;
        [SerializeField] private List<Image> _unitButtonPortraits;
        [SerializeField] private List<TextMeshProUGUI> _unitCostTexts;

        [Header("Auto Indicators")]
        [SerializeField] private List<GameObject> _unitAutoIndicators;

        [Header("Auto Production Effect")]
        [Tooltip("자동 생산 중일 때 유닛 버튼에 적용할 테두리 회전 효과 머티리얼.")]
        [SerializeField] private Material _autoProductionMaterial;
        [Tooltip("회전 속도")]
        [SerializeField] private float _borderSpeed = 5.0f;
        [Tooltip("테두리 두께")]
        [SerializeField] private float _borderThickness = 0.05f;
        [Tooltip("테두리 모서리 둥글기 (0~0.5)")]
        [SerializeField] private float _borderRadius = 0.1f;
        [Tooltip("테두리 안쪽 여백 (0~0.5)")]
        [SerializeField] private float _borderInset = 0.02f;

        [Tooltip("각 유닛 버튼의 테두리 효과를 담당하는 오버레이 이미지 리스트. 버튼 리스트와 1:1 매칭.")]
[SerializeField] private List<UnityEngine.UI.Image> _unitBorderOverlays;

        private Material _instancedAutoMaterial;

        /// <summary>_unitBorderOverlays 각각의 CanvasGroup 캐시. 가시성 제어용.</summary>
        private List<CanvasGroup> _unitBorderOverlayCgs;

        // ====================================================================
        // 초기화
        // ====================================================================

        private void Awake()
        {
            // ── 자동 생산 효과 머티리얼 인스턴스화 (최적화) ──────────────────────
            if (_autoProductionMaterial != null)
            {
                _instancedAutoMaterial = new Material(_autoProductionMaterial);
                UpdateMaterialProperties();

                // 인스턴스화된 머티리얼을 모든 오버레이에 할당
                if (_unitBorderOverlays != null)
                {
                    foreach (var overlay in _unitBorderOverlays)
                    {
                        if (overlay != null) overlay.material = _instancedAutoMaterial;
                    }
                }
            }

            // ── CanvasGroup 캐시 초기화 ──────────────────────────────────────────
            // _unitBorderOverlays는 material 할당 때문에 List<Image> 타입을 유지해야 하므로,
            // 가시성 제어용으로 각 오버레이의 CanvasGroup을 별도 리스트에 1:1로 캐싱한다.
            // CanvasGroup이 없으면 런타임에 추가하여 안전하게 보장한다.
            _unitBorderOverlayCgs = new List<CanvasGroup>();
            if (_unitBorderOverlays != null)
            {
                foreach (var overlay in _unitBorderOverlays)
                {
                    if (overlay != null)
                    {
                        var cg = overlay.gameObject.GetComponent<CanvasGroup>();
                        if (cg == null) cg = overlay.gameObject.AddComponent<CanvasGroup>();
                        _unitBorderOverlayCgs.Add(cg);
                    }
                    else
                    {
                        // 인덱스 정합성을 위해 null 슬롯도 그대로 채운다.
                        _unitBorderOverlayCgs.Add(null);
                    }
                }
            }
        }

        /// <summary>
        /// 인스펙터의 설정값들을 실제 셰이더 머티리얼에 반영합니다.
        /// </summary>
        private void UpdateMaterialProperties()
        {
            if (_instancedAutoMaterial == null) return;
            
            _instancedAutoMaterial.SetFloat("_Speed", _borderSpeed);
            _instancedAutoMaterial.SetFloat("_Thickness", _borderThickness);
            _instancedAutoMaterial.SetFloat("_Radius", _borderRadius);
            _instancedAutoMaterial.SetFloat("_Inset", _borderInset);
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // 플레이 모드에서 슬라이더를 움직이면 즉시 셰이더에 반영되도록 함
            if (UnityEngine.Application.isPlaying && _instancedAutoMaterial != null)
            {
                UpdateMaterialProperties();
            }
        }
        #endif

        [Header("Lock Indicators")]
        [Tooltip("각 유닛 버튼 위에 표시되는 잠금 오버레이의 CanvasGroup. 버튼 리스트와 1:1 매칭. " +
                 "현재 건물 단계보다 높은 단계의 유닛에 대해 alpha=1로 표시된다.")]
        [SerializeField] private List<CanvasGroup> _unitLockIndicators;

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

        [Header("Progress")]
        [SerializeField] private Image _progressFill;

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _populationText;

        [Header("Buttons")]
        [Tooltip("랠리포인트 설정 버튼. 클릭 후 타일을 선택하면 생산 유닛 집결지로 지정된다.")]
        [SerializeField] private Button _rallyPointButton;

        [Header("Upgrade")]
        [Tooltip("건물 업그레이드 버튼. 최고 단계 건물에서는 자동으로 숨김 처리.")]
        [SerializeField] private Button _upgradeButton;

        [Tooltip("업그레이드 비용 표시 텍스트.")]
        [SerializeField] private TextMeshProUGUI _upgradeCostText;

        [Tooltip("업그레이드 버튼에 표시될 아이콘 Image. 다음 단계 건물 Sprite를 런타임에 할당.")]
        [SerializeField] private Image _upgradeIconImage;

        [Tooltip("업그레이드 버튼에 부착된 CanvasGroup. alpha=0으로 숨겨도 레이아웃 공간 유지.")]
        [SerializeField] private CanvasGroup _upgradeButtonGroup;

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
        private PopulationUseCase _population;
        private ProductionTicker _ticker;
        private NetworkProductionController _networkProductionController;

        public bool IsSettingRallyPoint { get; private set; }
        public int RallyPointSetFrame { get; private set; }

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
        /// 베이스의 닫기/철거 버튼 등록은 InitializeBase에서 처리하고,
        /// 이 메서드는 생산 패널 고유 요소(랠리/업그레이드/유닛 버튼)만 연결한다.
        /// </summary>
        public void Initialize(UnitProductionUseCase production,
            ResourceUseCase resource, PopulationUseCase population,
            ProductionTicker ticker,
            NetworkProductionController networkProductionController = null,
            BuildingPlacementUseCase buildingPlacement = null,
            NetworkBuildingController networkBuildingController = null)
        {
            // 베이스 의존성/공통 버튼 등록 (닫기/철거)
            InitializeBase(buildingPlacement, resource, networkBuildingController);

            _production = production;
            _population = population;
            _ticker = ticker;
            _networkProductionController = networkProductionController;

            // 생산 패널 고유 버튼 이벤트
            if (_rallyPointButton != null) _rallyPointButton.onClick.AddListener(OnRallyPointClick);
            if (_upgradeButton != null) _upgradeButton.onClick.AddListener(OnUpgradeButtonClick);

            if (_unitButtons != null)
            {
                for (int i = 0; i < _unitButtons.Count; i++) SetupUnitButtonBySlot(_unitButtons[i], i);
            }

            SetupQueueSlotButtons();

            GameEvents.OnProductionQueueChanged.Subscribe(_ => UpdateUI()).AddTo(this);
            GameEvents.OnResourceChanged.Subscribe(_ => UpdateInfoBar()).AddTo(this);
        }

        // ====================================================================
        // 베이스 훅 — Show / Close
        // ====================================================================

        /// <summary>
        /// 생산 패널 고유의 Show 처리.
        /// 베이스가 _currentBuilding 저장 + 헤더 갱신 + 환불 표시까지 끝낸 후 호출된다.
        /// </summary>
        protected override void OnShow(BuildingData building)
        {
            IsSettingRallyPoint = false;

            if (_ticker != null) _ticker.ShowRallyMarker(building.Id);

            RaceId race = (building.Team == TeamId.Blue) ? GameRaceContext.BlueRace : GameRaceContext.RedRace;
            Debug.Log($"[ProductionUI] Show - Team: {building.Team}, Race: {race}, BuildingType: {building.Type}, Stage: {building.Stage}");

            BindButtonUnitTypes(race);
            UpdateButtonPortraits(building.Team, race);
            UpdateLockIndicators();
            UpdateUpgradeButton(race);
            UpdateUI();
        }

        /// <summary>
        /// 생산 패널 고유의 Close 사전 처리.
        /// 베이스가 ClosedFrame 기록/팝업 닫기를 수행하기 전에 호출된다.
        /// </summary>
        protected override void OnBeforeClose()
        {
            IsSettingRallyPoint = false;
            if (_ticker != null) _ticker.HideAllRallyMarkers();
        }

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
            if (IsOpen && _currentBuilding != null) UpdateProgressBar();
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
            if (_currentBuilding == null || _production == null) return;
            // 멀티플레이 → 서버에 위임. NetworkContext + 래퍼 메서드로 NGO 직접 의존 제거.
            if (_networkProductionController != null && NetworkContext.IsNetworkActive)
            {
                _networkProductionController.RequestCancelSlot(_currentBuilding.Id, slotIndex, _currentBuilding.Team);
                return;
            }
            _production.CancelQueueAt(_currentBuilding.Id, slotIndex);
        }

        // ====================================================================
        // 유닛 탭 → 생산 등록 / 잠금 안내
        // ====================================================================

        private void OnUnitTap(UnitType type)
        {
            if (_currentBuilding == null || _production == null) return;

            // ── 잠금 유닛 탭 시 토스트 후 종료 ─────────────────────────
            // 현재 건물 단계가 유닛 해금 단계에 못 미치면 생산이 불가하므로,
            // 사용자에게 "업그레이드 필요" 토스트를 표시하고 즉시 리턴.
            if (IsUnitLocked(type))
            {
                ToastUI.Show(ToastKey.UpgradeRequired);
                return;
            }

            var state = _production.GetState(_currentBuilding.Id);

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

            // 검증 통과 — 실제 등록을 위임. 멀티플레이는 NetworkContext + 래퍼 메서드로 분기.
            if (_networkProductionController != null && NetworkContext.IsNetworkActive)
                _networkProductionController.RequestEnqueue(_currentBuilding.Id, type, _currentBuilding.Team);
            else
                _production.EnqueueUnit(_currentBuilding.Id, type);
        }

        /// <summary>
        /// 슬롯에 바인딩된 유닛 타입이 현재 건물 단계 기준으로 잠금 상태인지 판정.
        /// _currentBuilding.Stage &lt; requiredStage 이면 true.
        /// </summary>
        private bool IsUnitLocked(UnitType type)
        {
            if (_currentBuilding == null) return false;
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
            // 멀티플레이 → 서버에 위임. NetworkContext + 래퍼 메서드로 NGO 직접 의존 제거.
            if (_networkProductionController != null && NetworkContext.IsNetworkActive)
                _networkProductionController.RequestToggleAuto(_currentBuilding.Id, type, _currentBuilding.Team);
            else
                _production.ToggleAutoProduction(_currentBuilding.Id, type);
        }

        private void OnRallyPointClick() { IsSettingRallyPoint = true; RallyPointSetFrame = Time.frameCount; _popup?.Hide(); }

        public void CompleteRallyPointSetting(HexCoord target)
        {
            if (_currentBuilding == null || _production == null) return;
            // 멀티플레이 → 서버에도 위임. NetworkContext + 래퍼 메서드로 NGO 직접 의존 제거.
            if (_networkProductionController != null && NetworkContext.IsNetworkActive)
                _networkProductionController.RequestSetRallyPoint(_currentBuilding.Id, target.Q, target.R, _currentBuilding.Team);
            _production.SetRallyPoint(_currentBuilding.Id, target);
            IsSettingRallyPoint = false;
            _currentBuilding = null;
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
            if (_currentBuilding == null) return;

            bool canUpgrade = BuildingTypeHelper.CanUpgrade(_currentBuilding.Type);

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
                BuildingType? nextType = BuildingTypeHelper.GetNextStage(_currentBuilding.Type);
                if (nextType.HasValue)
                {
                    Sprite icon = GetBuildingIcon(nextType.Value, _currentBuilding.Team);
                    if (icon != null)
                        _upgradeIconImage.sprite = icon;
                }
            }

            if (_upgradeCostText != null)
            {
                if (canUpgrade)
                {
                    int cost = BuildingStats.GetUpgradeCost(_currentBuilding.Type);
                    _upgradeCostText.text = $"{cost}";
                    // 보유 골드 부족 시 비용 텍스트를 강조 색상으로 표시 (배치 패널 패턴과 동일)
                    // _colorConfig는 BuildingPanelBase에서 protected로 상속받은 필드를 사용한다.
                    int currentGold = _resource != null ? _resource.GetGold(_currentBuilding.Team) : int.MaxValue;
                    bool insufficient = currentGold < cost;
                    _upgradeCostText.color = insufficient
                        ? (_colorConfig?.goldInsufficientColor ?? Color.red)
                        : (_colorConfig?.normalTextColor       ?? Color.white);
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
            if (_currentBuilding == null) return;
            if (!BuildingTypeHelper.CanUpgrade(_currentBuilding.Type)) return;

            int cost = BuildingStats.GetUpgradeCost(_currentBuilding.Type);
            if (_resource != null && !_resource.CanAfford(_currentBuilding.Team, cost))
            {
                ToastUI.Show(ToastKey.GoldInsufficient);
                return;
            }

            // 멀티플레이 여부 — Application 레이어 NetworkContext + 컨트롤러 주입으로 판단.
            // (이전: NetworkManager.Singleton.IsListening 직접 호출 — Presentation의 NGO 직접 의존)
            bool isNetworkMode = _networkBuildingController != null
                && NetworkContext.IsNetworkActive;

            if (isNetworkMode)
            {
                // 서버에 업그레이드 요청 — 골드 차감/검증은 서버에서 다시 수행.
                // ServerRpc 직접 호출 대신 일반 래퍼(RequestUpgrade) 사용으로 결합도 감소.
                _networkBuildingController.RequestUpgrade(_currentBuilding.Id);
            }
            else
            {
                // 싱글플레이: 직접 골드 차감 + UseCase 실행
                if (_resource != null) _resource.SpendGold(_currentBuilding.Team, cost);
                RaceId singleRace = (_currentBuilding.Team == TeamId.Blue)
                    ? GameRaceContext.BlueRace
                    : GameRaceContext.RedRace;
                _buildingPlacement?.UpgradeBuilding(_currentBuilding.Id, singleRace);
            }

            // 업그레이드 후 새 건물 인스턴스는 다음 클릭에서 다시 패널을 열도록 함.
            Close();
        }

        // ====================================================================
        // 철거 사전 처리 — 베이스 OnDemolishButtonClick에서 호출됨 (싱글플레이만)
        // ====================================================================

        /// <summary>
        /// 베이스의 OnDemolishButtonClick(싱글플레이 분기)에서 호출되는 사전 훅.
        /// 생산 건물의 생산 큐 전체를 취소(이미 차감된 골드 환불 포함)한다.
        /// 멀티플레이에서는 서버 RequestDemolishServerRpc 내부에서 동일 작업이 수행되므로
        /// 이 훅은 호출되지 않는다.
        /// </summary>
        protected override void BeforeDemolish()
        {
            if (_currentBuilding == null) return;

            // IsProductionBuilding=true 인 건물만 큐 취소가 의미가 있다.
            // (이 패널 자체가 생산 건물 전용이므로 보통 true이지만, 베이스 일관성을 위해 가드 유지.)
            if (BuildingTypeHelper.IsProductionBuilding(_currentBuilding.Type) && _production != null)
                _production.CancelAllQueue(_currentBuilding.Id);
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

        // ====================================================================
        // UI 갱신
        // ====================================================================

        private void UpdateUI()
        {
            if (_currentBuilding == null || _production == null) return;
            var state = _production.GetState(_currentBuilding.Id);
            if (state == null) return;

            if (_unitButtons != null)
            {
                for (int i = 0; i < _unitButtons.Count; i++)
                {
                    if (_unitButtons[i] == null) continue;

                    bool isAuto = i < _activeUnitTypes.Count && state.AutoTypes.Contains(_activeUnitTypes[i]);

                    // ── 자동 생산 시각 효과 갱신 ──
                    // 자동 생산 중이면 alpha=1로 테두리 효과를 보이고, 아니면 alpha=0으로 숨긴다.
                    // blocksRaycasts는 테두리가 버튼 입력을 가로채지 않도록 가시성과 동일하게 맞춘다.
                    if (_unitBorderOverlayCgs != null && i < _unitBorderOverlayCgs.Count && _unitBorderOverlayCgs[i] != null)
                    {
                        _unitBorderOverlayCgs[i].alpha = isAuto ? 1f : 0f;
                        _unitBorderOverlayCgs[i].blocksRaycasts = isAuto;
                    }

                    // ── 기존 도트 인디케이터 갱신 ──
if (_unitAutoIndicators != null && i < _unitAutoIndicators.Count && _unitAutoIndicators[i] != null)
                    {
                        _unitAutoIndicators[i].SetActive(isAuto);
                    }
                }
            }
            UpdateQueueSlots(state);
            UpdateProgressBar();
            UpdateInfoBar();
        }

        /// <summary>
        /// 잠금 인디케이터 GameObject 활성/비활성 갱신 + 잠긴 유닛 초상화 디밍.
        /// _activeUnitLocks[i] 가 true면 자물쇠 오버레이 On + 초상화를 어둡게,
        /// false면 오버레이 Off + 초상화 원래 색(흰색)으로 복원한다.
        /// 슬롯에 유닛이 바인딩되지 않은 경우는 잠금도 끈다.
        /// </summary>
        private void UpdateLockIndicators()
        {
            if (_unitLockIndicators == null) return;
            for (int i = 0; i < _unitLockIndicators.Count; i++)
            {
                if (_unitLockIndicators[i] == null) continue;

                // ── 인디케이터 인덱스 → 슬롯 인덱스 매핑 ──────────────────────────
                // 슬롯0(index 0)은 1단계 유닛이라 항상 해금 상태이므로 잠금 인디케이터가 없다.
                // 따라서 _unitLockIndicators[0]은 슬롯1(2단계 유닛), [1]은 슬롯2(3단계 유닛)에
                // 대응한다. 인디케이터 인덱스 i에 +1을 더해 실제 슬롯 인덱스를 구한다.
                int slotIndex = i + 1;

                // 잠금 여부 판정: 해당 슬롯의 _activeUnitLocks[slotIndex]가 true이면
                // 현재 건물 단계가 유닛 해금 단계에 못 미치는 잠금 유닛이다.
                bool locked = slotIndex < _activeUnitLocks.Count
                              && slotIndex < _activeUnitTypes.Count
                              && _activeUnitLocks[slotIndex];

                // 자물쇠 아이콘 오버레이 표시/숨김 처리.
                // 잠금 시 alpha=1(표시), 해금 시 alpha=0(숨김).
                // blocksRaycasts는 가시성과 동일하게 맞춰, 보이지 않는 자물쇠가 버튼 입력을 가로채지 않게 한다.
                _unitLockIndicators[i].alpha = locked ? 1f : 0f;
                _unitLockIndicators[i].blocksRaycasts = locked;

                // 초상화 디밍: 잠금 상태면 어둡게, 해금 상태면 원래 색(흰색)으로 복원한다.
                // 디밍 대상도 슬롯 인덱스(slotIndex)를 기준으로 해야 인디케이터와 같은 슬롯을 가리킨다.
                // _unitButtonPortraits가 비어 있거나 해당 슬롯이 없는 경우를 안전하게 건너뛴다.
                // (참고) UpdateButtonPortraits()는 .sprite만 변경하므로 여기서 .color를 바꿔도 충돌하지 않는다.
                if (_unitButtonPortraits != null && slotIndex < _unitButtonPortraits.Count && _unitButtonPortraits[slotIndex] != null)
                {
                    // RGB 0.35는 약 35% 밝기 — 유닛 실루엣은 알아볼 수 있지만 비활성처럼 어둡게 보인다.
                    // 알파(투명도)는 1로 유지해 디밍만 적용하고 사라지지는 않게 한다.
                    _unitButtonPortraits[slotIndex].color = locked
                        ? new Color(0.35f, 0.35f, 0.35f, 1f)
                        : Color.white;
                }
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

        private void UpdateProgressBar() { if (_progressFill != null && _currentBuilding != null && _production != null) _progressFill.fillAmount = _production.GetState(_currentBuilding.Id)?.Progress ?? 0f; }

        private void UpdateInfoBar()
        {
            if (_currentBuilding == null) return;

            // ── 보유 골드 텍스트 갱신 ──
            if (_goldText != null && _resource != null)
                _goldText.text = _resource.GetGold(_currentBuilding.Team).ToString();

            // ── 각 유닛 생산 비용 텍스트 색상 재평가 ──
            // _colorConfig는 BuildingPanelBase에서 protected로 상속받은 필드를 사용한다.
            // 미연결 시에도 Color.red/Color.white 폴백으로 안전하게 동작.
            if (_resource != null && _unitCostTexts != null)
            {
                int currentGold = _resource.GetGold(_currentBuilding.Team);
                Color insufficientColor = _colorConfig?.goldInsufficientColor ?? Color.red;
                Color normalColor       = _colorConfig?.normalTextColor       ?? Color.white;

                for (int i = 0; i < _unitCostTexts.Count; i++)
                {
                    if (_unitCostTexts[i] == null) continue;
                    if (i >= _activeUnitTypes.Count)
                    {
                        // 빈 슬롯은 기본(흰색)으로 유지
                        _unitCostTexts[i].color = normalColor;
                        continue;
                    }
                    int cost = UnitProductionStats.GetGoldCost(_activeUnitTypes[i]);
                    _unitCostTexts[i].color = (currentGold < cost) ? insufficientColor : normalColor;
                }
            }

            // ── 업그레이드 비용 텍스트도 골드 변경 시 함께 색상 재평가 ──
            if (_upgradeCostText != null && _upgradeCostText.gameObject.activeSelf
                && BuildingTypeHelper.CanUpgrade(_currentBuilding.Type))
            {
                int currentGold = _resource != null ? _resource.GetGold(_currentBuilding.Team) : int.MaxValue;
                int upCost = BuildingStats.GetUpgradeCost(_currentBuilding.Type);
                bool insufficient = currentGold < upCost;
                _upgradeCostText.color = insufficient
                    ? (_colorConfig?.goldInsufficientColor ?? Color.red)
                    : (_colorConfig?.normalTextColor       ?? Color.white);
            }

            // ── 인구 텍스트 갱신 ──
            if (_populationText != null && _population != null)
                _populationText.text = $"{_population.GetUsedPopulation(_currentBuilding.Team)}/{_population.GetMaxPopulation(_currentBuilding.Team)}";
        }

        private void UpdateButtonPortraits(TeamId team, RaceId race)
        {
            var list = GetUnitEntriesForCurrentBuilding(team);
            Debug.Log($"[Portrait] team={team} listCount={list.Count} portraitsCount={_unitButtonPortraits?.Count}");
            if (_unitButtonPortraits == null) return;

            // 유닛이 정확히 2종류인 건물은 [유닛1][빈슬롯][유닛2] 배치를 사용한다.
            // portrait[0] = 첫 번째 유닛, portrait[1] = 스킵(빈 슬롯), portrait[2] = 두 번째 유닛.
            // 이 배치를 모르고 portrait[i] = list[i]로 넣으면 슬롯2 초상화가 갱신되지 않아
            // 이전 건물의 초상화가 그대로 남는 버그가 생긴다.
            bool twoUnitLayout = (list.Count == 2);

            for (int i = 0; i < _unitButtonPortraits.Count; i++)
            {
                if (_unitButtonPortraits[i] == null) { Debug.Log($"[Portrait] [{i}] Image null"); continue; }

                if (twoUnitLayout)
                {
                    // 2유닛 배치: slot0 = list[0], slot1 = 스킵, slot2 = list[1]
                    if (i == 0)
                    {
                        _unitButtonPortraits[i].sprite = list[0].portrait;
                        Debug.Log("[Portrait] [0] sprite=" + (list[0].portrait != null ? list[0].portrait.name : "NULL"));
                    }
                    else if (i == 2)
                    {
                        _unitButtonPortraits[i].sprite = list[1].portrait;
                        Debug.Log("[Portrait] [2] sprite=" + (list[1].portrait != null ? list[1].portrait.name : "NULL"));
                    }
                    // slot1(i==1)은 숨겨진 더미 슬롯 — 초상화 갱신 불필요
                }
                else
                {
                    // 유닛이 1개 또는 3개인 경우: 기존 동작 유지
                    if (i < list.Count)
                    {
                        _unitButtonPortraits[i].sprite = list[i].portrait;
                        Debug.Log("[Portrait] [" + i + "] sprite=" + (list[i].portrait != null ? list[i].portrait.name : "NULL"));
                    }
                }
            }
        }

        private Sprite GetPortrait(UnitType type)
        {
            if (_currentBuilding == null) return null;
            var list = GetUnitEntriesForCurrentBuilding(_currentBuilding.Team);
            foreach (var entry in list) if (entry.type == type) return entry.portrait;
            return (list.Count > 0) ? list[0].portrait : null;
        }

        /// <summary>
        /// 현재 _currentBuilding에 매핑된 유닛 엔트리 리스트를 반환.
        /// 매핑이 없으면 빈 리스트를 반환 (안전 폴백 — 잘못된 BuildingType이거나
        /// Inspector에서 매핑이 누락된 경우 빈 버튼 슬롯이 표시됨).
        /// </summary>
        private List<UnitPortraitEntry> GetUnitEntriesForCurrentBuilding(TeamId team)
        {
            if (_currentBuilding == null || _buildingUnitMappings == null)
                return new List<UnitPortraitEntry>();

            foreach (var mapping in _buildingUnitMappings)
            {
                if (mapping.buildingType != _currentBuilding.Type) continue;
                return team == TeamId.Blue
                    ? (mapping.blueUnits ?? new List<UnitPortraitEntry>())
                    : (mapping.redUnits ?? new List<UnitPortraitEntry>());
            }

            // 매핑 누락 — 콘솔 경고 후 빈 리스트 반환
            Debug.LogWarning($"[ProductionPanelUI] BuildingType={_currentBuilding.Type}에 대한 유닛 매핑이 없습니다.");
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
            if (_currentBuilding == null) return;
            int currentStage = _currentBuilding.Stage;

            var list = GetUnitEntriesForCurrentBuilding(_currentBuilding.Team);
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
