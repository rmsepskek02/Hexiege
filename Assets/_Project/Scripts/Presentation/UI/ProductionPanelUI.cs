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
        [SerializeField] private List<Image> _unitBorderOverlays;

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
            // 플레이 모드 중 인스펙터에서 위 값(속도/두께/둥글기/여백)을 고치면 즉시 셰이더에 반영되도록 한다.
            // (이 필드들에는 [Range] 특성이 없으므로 인스펙터에는 슬라이더가 아니라 숫자 입력칸으로 보인다)
            if (UnityEngine.Application.isPlaying && _instancedAutoMaterial != null)
            {
                UpdateMaterialProperties();
            }
        }
        #endif

        [Header("Lock Indicators")]
        [Tooltip("유닛 버튼 위에 표시되는 잠금 오버레이의 CanvasGroup 목록. 버튼 리스트와 1:1이 아니다. " +
                 "인디케이터 i번은 버튼 슬롯 i+1번에 대응한다 (0번 → 슬롯1, 1번 → 슬롯2). " +
                 "슬롯0은 1단계 유닛이라 항상 해금 상태여서 잠금 인디케이터를 두지 않기 때문이다. " +
                 "따라서 버튼이 3개여도 이 리스트는 2개가 정상이며, 배선 순서는 슬롯1 → 슬롯2 순이다. " +
                 "현재 건물 단계보다 높은 단계의 유닛 슬롯에 대해 alpha=1로 표시된다.")]
        [SerializeField] private List<CanvasGroup> _unitLockIndicators;

        [Header("Unit Button Groups")]
        [Tooltip("각 유닛 버튼 GO에 부착된 CanvasGroup 목록. 유닛 버튼 리스트와 1:1 매칭. " +
                 "유닛이 없는 슬롯을 alpha=0으로 숨기되 레이아웃 공간은 유지한다.")]
        [SerializeField] private List<CanvasGroup> _unitButtonGroups;

        /// <summary>
        /// _unitButtonGroups 미배선 경고를 이미 출력했는지 여부. (도배 방지용 1회성 플래그)
        /// 씬/프리팹 배선은 게임 실행 중에 바뀌지 않는 반면 BindButtonUnitTypes()는 패널을 열 때마다
        /// 호출되므로, 억제가 없으면 같은 경고가 콘솔을 가득 채워 다른 로그를 덮어버린다.
        /// </summary>
        private bool _unitButtonGroupWarningLogged;

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
        /// 현재 표시 중인 버튼 슬롯에 바인딩된 UnitType 리스트. 리스트 인덱스 = 버튼 슬롯 인덱스.
        /// 잠금 여부와 무관하게 라인의 유닛이 전부 포함된다.
        /// 단, 유닛이 정확히 2종류인 건물은 [유닛1][빈슬롯][유닛2] 배치를 쓰기 때문에
        /// 인덱스1에는 인덱스0과 같은 값이 더미로 들어간다(BindButtonUnitTypes 참조).
        /// 즉 이 리스트의 개수가 곧 실제 유닛 종류 수인 것은 아니다.
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
            // 이 메서드는 잠금 여부를 판정하지 않는다. 어떤 유닛을 언제 눌렀는지만 기록해 둔다.
            // 잠금 판정은 "짧은 탭"인지 "길게 누르기"인지가 확정된 뒤,
            // OnUnitTap() / OnUnitLongPress()가 각각 IsUnitLocked()로 수행한다
            // (두 경로 모두 잠금이면 ToastKey.UpgradeRequired만 띄우고 중단한다).
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

        private void OnRallyPointClick()
        {
            IsSettingRallyPoint = true;
            RallyPointSetFrame = Time.frameCount;

            // 조준 중에는 맵이 보여야 하므로 팝업을 숨긴다.
            _popup?.Hide();

            // 공유 BlockingOverlay(패널이 열릴 때 BuildingPanelBase.Show()에서 표시됨)는
            //   "탭하면 Close()" 콜백을 가진 Popup 모드다.
            //   이 오버레이를 그대로 두면 조준하려고 맵을 탭했을 때 오버레이가 터치를 먼저 먹어
            //   Close()가 실행되고, OnBeforeClose()에서 IsSettingRallyPoint가 false로 초기화되며
            //   랠리 마커까지 숨겨져 "지정이 취소된 것처럼" 보인다.
            //   따라서 조준 모드 진입 시 오버레이를 내려 터치가 맵으로 전달되게 한다.
            //   (BuildingSkillPanelUI의 지점 조준 진입부와 동일한 패턴)
            UIManager.Instance?.HideBlockingOverlay();
        }

        public void CompleteRallyPointSetting(HexCoord target)
        {
            if (_currentBuilding == null || _production == null) return;
            // 멀티플레이 → 서버에도 위임. NetworkContext + 래퍼 메서드로 NGO 직접 의존 제거.
            if (_networkProductionController != null && NetworkContext.IsNetworkActive)
                _networkProductionController.RequestSetRallyPoint(_currentBuilding.Id, target.Q, target.R, _currentBuilding.Team);
            _production.SetRallyPoint(_currentBuilding.Id, target);
            IsSettingRallyPoint = false;

            // 이 경로는 Close()를 거치지 않으므로, Close()가 대신 해 주던
            //   BlockingOverlay 참조 카운터 반납을 여기서 직접 수행한다.
            //   (패널 표시 시 BuildingPanelBase.Show()가 ShowBlockingOverlay로 +1 해 둔 몫)
            //   조준 진입 시 이미 1회 내렸으므로 보통은 카운터가 0인 상태에서 호출되지만,
            //   UIManager.HideBlockingOverlay()는 0 미만으로 내려가지 않는 가드를 갖고 있어 안전하다.
            UIManager.Instance?.HideBlockingOverlay();

            _currentBuilding = null;
        }

        // ====================================================================
        // 업그레이드 처리
        // ====================================================================

        /// <summary>
        /// 현재 건물의 업그레이드 가능 여부에 따라 업그레이드 버튼/비용 텍스트를 갱신.
        ///   - 다음 단계 없음 → 버튼에 붙은 CanvasGroup을 alpha=0 / blocksRaycasts=false /
        ///     interactable=false 로 숨긴다. 버튼 GameObject 자체는 계속 켜 둔다.
        ///     (비용 텍스트만은 GameObject를 SetActive(false)로 끈다)
        ///   - 다음 단계 있음 → 같은 CanvasGroup을 alpha=1 / blocksRaycasts=true / interactable=true 로
        ///     표시하고, 다음 단계 건물 아이콘과 비용(골드) 텍스트를 함께 채운다.
        /// 버튼을 SetActive로 끄지 않는 이유는 아래 본문 주석과
        /// GameSystemRules_UI.md — 공통 UI 규칙 5(CanvasGroup 숨김/표시 패턴) 참조.
        /// 호출: 패널을 열 때 OnShow()에서 1회. 업그레이드가 완료되면 BuildingFactory가 건물 GO를
        /// 갈아끼우지만 그 시점에는 이 패널이 이미 닫혀 있으므로 별도 갱신이 필요 없다.
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
        ///   3) 멀티플레이면 NetworkBuildingController.RequestUpgrade() 래퍼를 호출한다.
        ///      (Presentation이 NGO에 직접 의존하지 않도록 ServerRpc를 직접 부르지 않는다.
        ///       실제 서버 전송은 그 래퍼 내부의 RequestUpgradeServerRpc가 담당한다.)
        ///      싱글플레이면 골드를 직접 차감한 뒤 UseCase를 직접 실행한다.
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

                    // ── 자동 생산 시각 효과 갱신 (테두리 표시의 유일한 제어 경로) ──
                    // 자동 생산 중이면 alpha=1로 테두리 효과를 보이고, 아니면 alpha=0으로 숨긴다.
                    // blocksRaycasts는 테두리가 버튼 입력을 가로채지 않도록 가시성과 동일하게 맞춘다.
                    //
                    // [왜 CanvasGroup 하나로만 제어하는가 — 유니티 입문자용 설명]
                    // 예전에는 아래에 같은 오브젝트를 SetActive(isAuto)로 껐다 켜는 블록이 하나 더 있었다.
                    // 두 블록이 가리키던 대상은 씬에서 같은 BorderOverlay 오브젝트였고
                    // (근거: GameSystemRules_UI.md — MistShrine 패널 UI 규칙 14),
                    // 결국 "테두리를 보이게 할지"를 정하는 스위치가 두 개 달려 있는 상태였다.
                    // 스위치가 둘이면 한쪽 조건만 바뀌어도 서로 어긋나고, 증상은 "테두리가 안 보인다" 하나인데
                    // 원인 후보가 둘이라 디버깅이 어려워진다. 그래서 SetActive 쪽을 걷어내고 하나로 합쳤다.
                    //
                    // 남길 쪽으로 CanvasGroup을 택한 이유 (근거: GameSystemRules_UI.md — 공통 UI 규칙 5):
                    //  1) SetActive(false)로 끈 오브젝트는 Layout Group 안에서 차지하던 자리까지 사라져 형제 요소가 밀린다.
                    //  2) 꺼진 오브젝트는 Awake 등 내부 로직이 돌지 않는다. 비활성 상태로 씬이 저장되면
                    //     이후 다시 켜도 초기화가 끝나 있지 않아 영영 표시되지 않는 함정에 빠진다.
                    // alpha=0은 오브젝트를 켜 둔 채 "보이지 않게"만 만들기 때문에 위 두 문제가 모두 없다.
                    // 화면에 보이는 결과는 기존과 동일하다 — 예전에도 숨김은 alpha=0이 이미 완결하고 있었고
                    // SetActive는 그 위에 덧붙어 있던 중복 조치였을 뿐이다.
                    if (_unitBorderOverlayCgs != null && i < _unitBorderOverlayCgs.Count && _unitBorderOverlayCgs[i] != null)
                    {
                        _unitBorderOverlayCgs[i].alpha = isAuto ? 1f : 0f;
                        _unitBorderOverlayCgs[i].blocksRaycasts = isAuto;
                    }
                }
            }
            UpdateQueueSlots(state);
            UpdateProgressBar();
            UpdateInfoBar();
        }

        /// <summary>
        /// 자물쇠 오버레이의 CanvasGroup 갱신 + 잠긴 유닛 초상화 디밍.
        /// GameObject를 SetActive로 껐다 켜는 방식이 아니라, CanvasGroup의 alpha와 blocksRaycasts만 바꾼다
        /// (근거: GameSystemRules_UI.md — 공통 UI 규칙 5 "CanvasGroup 숨김/표시 패턴").
        ///   - 슬롯이 잠금  → 오버레이 alpha=1, blocksRaycasts=true + 초상화를 어둡게(RGB 0.35)
        ///   - 슬롯이 해금  → 오버레이 alpha=0, blocksRaycasts=false + 초상화를 원래 색(흰색)으로 복원
        /// 주의: 인디케이터 인덱스 i는 버튼 슬롯 인덱스 i+1에 대응한다(1:1이 아니다 — 본문 주석 참조).
        /// 슬롯에 유닛이 바인딩되지 않은 경우도 해금과 동일하게 처리한다.
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
            if (_unitButtonPortraits == null) return;

            // 유닛이 정확히 2종류인 건물은 [유닛1][빈슬롯][유닛2] 배치를 사용한다.
            // portrait[0] = 첫 번째 유닛, portrait[1] = 스킵(빈 슬롯), portrait[2] = 두 번째 유닛.
            // 이 배치를 모르고 portrait[i] = list[i]로 넣으면 슬롯2 초상화가 갱신되지 않아
            // 이전 건물의 초상화가 그대로 남는 버그가 생긴다.
            bool twoUnitLayout = (list.Count == 2);

            for (int i = 0; i < _unitButtonPortraits.Count; i++)
            {
                // 초상화 Image가 배선되지 않은 슬롯은 건너뛴다. (continue 유지 — 아래 갱신을 실행하면 NRE)
                if (_unitButtonPortraits[i] == null) continue;

                if (twoUnitLayout)
                {
                    // 2유닛 배치: slot0 = list[0], slot1 = 스킵, slot2 = list[1]
                    if (i == 0)
                    {
                        _unitButtonPortraits[i].sprite = list[0].portrait;
                    }
                    else if (i == 2)
                    {
                        _unitButtonPortraits[i].sprite = list[1].portrait;
                    }
                    // slot1(i==1)은 숨겨진 더미 슬롯 — 초상화 갱신 불필요
                }
                else
                {
                    // 유닛이 1개 또는 3개인 경우: 기존 동작 유지
                    if (i < list.Count)
                    {
                        _unitButtonPortraits[i].sprite = list[i].portrait;
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

            // 매핑 누락 — 경고 후 빈 리스트 반환
            // [개발] Warn + 개발.
            //   _buildingUnitMappings 는 Inspector 에서 채우는 목록이므로 매핑 누락은 설정 오류다
            //   (LogRules 1.3 원칙 3 단서). 모든 기기에 같은 설정이 나가므로 축 B ① 이 "아니오" → 개발.
            GameLog.Dev.Warn("UI", nameof(ProductionPanelUI),
                             "건물에 대응하는 유닛 매핑이 Inspector 에 없다",
                             $"BuildingType={_currentBuilding.Type}");
            return new List<UnitPortraitEntry>();
        }

        /// <summary>
        /// 현재 건물에 대응하는 유닛 라인업 전체를 버튼 슬롯에 바인딩.
        /// 잠금 여부도 동시에 계산하여 _activeUnitLocks 에 저장.
        /// 잠금 유닛이라도 이 메서드는 버튼을 눌리는 상태 그대로 둔다
        /// (탭했을 때 "업그레이드 필요" 토스트를 띄워야 하므로 입력 자체는 살려 둔다).
        /// 잠금의 시각 표현(자물쇠 오버레이 + 초상화 디밍)은 UpdateLockIndicators()가 담당한다.
        /// 이 메서드가 CanvasGroup으로 제어하는 것은 "유닛이 없는 빈 슬롯"의 숨김뿐이다.
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
            // 슬롯1은 아래에서 CanvasGroup으로 가려지므로(alpha=0으로 안 보이게 만들고,
            // blocksRaycasts=false / interactable=false로 입력을 막는다) 더미 값이 실제로 쓰이지 않는다.
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
                // CanvasGroup이 배선되지 않은 슬롯 번호를 모아 두는 임시 리스트.
                // 정상 배선 상태에서는 끝까지 null로 남아 메모리 할당이 전혀 발생하지 않는다.
                List<int> unwiredSlots = null;

                for (int i = 0; i < _unitButtons.Count; i++)
                {
                    // 2유닛 특수 배치: 슬롯0(유닛1), 슬롯1(빈), 슬롯2(유닛2)
                    // 2유닛 외: 유닛이 있는 슬롯만 표시
                    bool hasUnit = twoUnitLayout ? (i == 0 || i == 2) : (i < list.Count);

                    // ── 빈 슬롯 숨김은 CanvasGroup으로만 처리한다 ──────────────────────
                    // 근거: GameSystemRules_UI.md — 공통 UI 규칙 5 "CanvasGroup 숨김/표시 패턴".
                    // SetActive(false)를 쓰면 Grid Layout 안에서 그 슬롯이 차지하던 공간까지 사라져
                    // 뒤쪽 버튼들이 앞으로 당겨진다. 그래서 "CanvasGroup이 없으면 SetActive로라도 끈다"는
                    // 폴백을 두지 않는다 — 규칙이 금지한 방식을 예외 경로로 되살리는 셈이기 때문이다.
                    // 대신 배선이 빠진 사실을 아래에서 경고로 알려, 씬에서 고치도록 유도한다.
                    if (_unitButtonGroups != null && i < _unitButtonGroups.Count && _unitButtonGroups[i] != null)
                    {
                        _unitButtonGroups[i].alpha = hasUnit ? 1f : 0f;
                        _unitButtonGroups[i].blocksRaycasts = hasUnit;
                        _unitButtonGroups[i].interactable = hasUnit;
                    }
                    else
                    {
                        // 미배선 슬롯 — 숨김/표시를 적용할 수단이 없다.
                        // 슬롯마다 즉시 경고하면 한 번 여는데 여러 줄이 찍히므로, 번호만 모아 두었다가
                        // 루프가 끝난 뒤 한 줄로 합쳐서 출력한다.
                        if (unwiredSlots == null) unwiredSlots = new List<int>();
                        unwiredSlots.Add(i);
                    }

                    // 비용 텍스트: 유닛이 있는 슬롯만 갱신
                    // _unitCostTexts 자체가 미배선(null)일 수 있으므로 UpdateInfoBar()와 동일하게 null을 먼저 확인한다.
                    if (hasUnit && _unitCostTexts != null && i < _unitCostTexts.Count && _unitCostTexts[i] != null && i < _activeUnitTypes.Count)
                        _unitCostTexts[i].text = $"{UnitProductionStats.GetGoldCost(_activeUnitTypes[i])}";
                }

                // ── 미배선 경고 (호출당 1줄 + 인스턴스당 1회) ─────────────────────────
                // 이 메서드는 패널을 열 때마다 호출되지만 배선 상태는 실행 중에 바뀌지 않으므로,
                // 플래그로 최초 1회만 출력해 콘솔 도배를 막는다.
                if (unwiredSlots != null && !_unitButtonGroupWarningLogged)
                {
                    _unitButtonGroupWarningLogged = true;
                    // [개발] Warn + 개발 — 전형적인 Inspector 배선 누락(1.3 원칙 3 단서).
                    //   조치 안내처럼 긴 자유 문장은 message 쪽에 두고,
                    //   집계 가능한 값(슬롯 번호)만 key=value 로 뺀다(LogRules 1.4).
                    GameLog.Dev.Warn("UI", nameof(ProductionPanelUI),
                        $"유닛 버튼 슬롯의 CanvasGroup 이 Inspector 의 {nameof(_unitButtonGroups)} 에 배선되어 있지 않다. " +
                        "해당 슬롯은 유닛이 없어도 숨겨지지 않아 빈 버튼이 그대로 보인다. " +
                        "조치: 씬에서 이 컴포넌트를 선택한 뒤, 각 유닛 버튼 GameObject 의 CanvasGroup 을 " +
                        $"{nameof(_unitButtons)} 와 같은 순서로 {nameof(_unitButtonGroups)} 리스트에 넣는다 " +
                        "(CanvasGroup 컴포넌트가 없으면 해당 버튼에 먼저 추가해야 한다)",
                        $"UnwiredSlots={string.Join("|", unwiredSlots)}");
                }
            }
        }
    }
}
