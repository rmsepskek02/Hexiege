// ============================================================================
// ProductionPanelUI.cs
// 배럭 클릭 시 표시되는 유닛 생산 패널 UI.
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

        [Header("Queue Slots")]
        [SerializeField] private Image[] _queueSlotImages;

        [System.Serializable]
        public struct UnitPortraitEntry
        {
            public UnitType type;
            public Sprite portrait;
        }

        [Header("Unit Entries — 종족별 유닛 설정")]
        [SerializeField] private List<UnitPortraitEntry> _blueHumanUnits;
        [SerializeField] private List<UnitPortraitEntry> _blueSpiritUnits;
        [SerializeField] private List<UnitPortraitEntry> _blueTranscendenceUnits;
        [SerializeField] private List<UnitPortraitEntry> _redHumanUnits;
        [SerializeField] private List<UnitPortraitEntry> _redSpiritUnits;
        [SerializeField] private List<UnitPortraitEntry> _redTranscendenceUnits;

        [Header("Progress")]
        [SerializeField] private Image _progressFill;

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _populationText;

        [Header("Buttons")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _rallyPointButton;

        private UnitProductionUseCase _production;
        private ResourceUseCase _resource;
        private PopulationUseCase _population;
        private ProductionTicker _ticker;
        private NetworkProductionController _networkProductionController;
        private BuildingData _currentBarracks;

        public bool IsOpen => _popup != null && _popup.IsVisible;
        public int ClosedFrame { get; private set; } = -1;
        public bool IsSettingRallyPoint { get; private set; }
        public int RallyPointSetFrame { get; private set; }
        public int CurrentBarracksId => _currentBarracks?.Id ?? -1;

        private List<UnitType> _activeUnitTypes = new List<UnitType>();
        private float _pointerDownTime;
        private bool _isPointerDown;
        private const float LongPressThreshold = 0.5f;
        private bool _longPressTriggered;
        private UnitType _activeUnitType;

        // ────────────────────────────────────────────────────────────────────
        // 생산 실패 사유 — UI 피드백을 위해 OnUnitTap이 어떤 검증에서 실패했는지 분류.
        // None       : 실패 아님(정상 등록)
        // GoldInsufficient : 골드 부족 → 골드 텍스트 빨간색 + 토스트
        // PopulationFull   : 인구 한계 도달 → 토스트(HUD 색상은 GameHudUI가 자체 처리)
        // QueueFull        : 큐 3개 초과 → 토스트
        // ────────────────────────────────────────────────────────────────────
        private enum ProductionFailReason
        {
            None,
            GoldInsufficient,
            PopulationFull,
            QueueFull
        }

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

            if (_cancelButton != null) _cancelButton.onClick.AddListener(Close);
            if (_rallyPointButton != null) _rallyPointButton.onClick.AddListener(OnRallyPointClick);

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
            IsSettingRallyPoint = false;
            _popup?.Show();
            _sharedBackground?.Register(Close);

            if (_ticker != null) _ticker.ShowRallyMarker(barracks.Id);

            RaceId race = (barracks.Team == TeamId.Blue) ? GameRaceContext.BlueRace : GameRaceContext.RedRace;
            Debug.Log($"[ProductionUI] Show - Team: {barracks.Team}, Race: {race}");

            BindButtonUnitTypes(race);
            UpdateButtonPortraits(barracks.Team, race);
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

        private void SetupUnitButtonBySlot(Button button, int slotIndex)
        {
            if (button == null) return;
            var trigger = button.gameObject.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            
            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener(_ => {
                if (slotIndex < _activeUnitTypes.Count) OnUnitPointerDown(_activeUnitTypes[slotIndex]);
            });
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

        private void OnUnitTap(UnitType type)
        {
            if (_currentBarracks == null || _production == null) return;
            var state = _production.GetState(_currentBarracks.Id);

            // 자동 생산 중인 타입을 다시 탭한 경우 → 자동 토글(해제) 분기.
            // 이 분기에서는 추가 등록이 아니므로 실패 피드백을 발생시키지 않는다.
            if (state != null && state.IsAutoMode && state.AutoTypes.Contains(type))
            {
                HandleToggleAuto(type);
                return;
            }

            // ─── 사전 검증 (UI 피드백용) ───────────────────────────────
            // 실제 등록은 EnqueueUnit / ServerRpc가 하지만, 여기서 한 번 더
            // 같은 조건을 검사해 "어떤 사유로 실패할지"를 파악한다.
            // 우선순위: 큐 상한 > 골드 > 인구.
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
        /// 수동 생산 등록 가능 여부를 사전 검사.
        /// UnitProductionUseCase.EnqueueUnit의 검증 순서와 동일하게 맞춰
        /// UI 피드백과 실제 등록 결과가 어긋나지 않도록 보장한다.
        /// </summary>
        private ProductionFailReason ValidateProduction(ProductionState state, UnitType type)
        {
            if (state == null) return ProductionFailReason.None;

            // 1) 큐 상한 — CurrentProducing 1슬롯 + IsCharged=true 항목 합산.
            int slotsUsed = (state.CurrentProducing.HasValue ? 1 : 0) + state.ChargedPendingCount();
            if (slotsUsed + 1 > ProductionState.MaxQueueSize)
                return ProductionFailReason.QueueFull;

            // 2) 골드 검증.
            int cost = UnitProductionStats.GetGoldCost(type);
            if (_resource != null && !_resource.CanAfford(state.Team, cost))
                return ProductionFailReason.GoldInsufficient;

            // 3) 인구 검증.
            int popCost = UnitProductionStats.GetPopulationCost(type);
            if (_population != null && !_population.HasPopulation(state.Team, popCost))
                return ProductionFailReason.PopulationFull;

            return ProductionFailReason.None;
        }

        /// <summary>
        /// 생산 실패 사유에 따라 사용자에게 피드백을 표시.
        ///   GoldInsufficient → 생산 패널 골드 텍스트 빨강 + 토스트
        ///   PopulationFull   → 토스트만(HUD 인구 텍스트는 GameHudUI가 자체적으로 빨강 처리)
        ///   QueueFull        → 토스트만(특정 텍스트 색 변경 없음)
        /// </summary>
        private void HandleProductionFail(ProductionFailReason reason)
        {
            switch (reason)
            {
                case ProductionFailReason.GoldInsufficient:
                    if (_goldText != null) _goldText.color = Color.red;
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

        private void OnUnitLongPress(UnitType type) => HandleToggleAuto(type);

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

            // ── 골드 텍스트 갱신 + 색상 재평가 ──
            // 골드가 변할 때마다 "현재 배럭에서 만들 수 있는 가장 싼 유닛"의 비용과 비교하여
            // 부족하면 빨강, 충분하면 흰색으로 자동 복구한다.
            if (_goldText != null && _resource != null)
            {
                int currentGold = _resource.GetGold(_currentBarracks.Team);
                _goldText.text = currentGold.ToString();

                int cheapestCost = GetCheapestUnitCost();
                // cheapestCost가 0이면(목록 비었을 가능성) 색 변경하지 않음.
                if (cheapestCost > 0)
                    _goldText.color = (currentGold < cheapestCost) ? Color.red : Color.white;
                else
                    _goldText.color = Color.white;
            }

            // ── 인구 텍스트 갱신 ──
            if (_populationText != null && _population != null)
                _populationText.text = $"{_population.GetUsedPopulation(_currentBarracks.Team)}/{_population.GetMaxPopulation(_currentBarracks.Team)}";
        }

        /// <summary>
        /// 현재 배럭이 생산할 수 있는 유닛(_activeUnitTypes) 중 가장 저렴한 골드 비용 반환.
        /// 비어 있으면 0 반환(색상 재평가 시 0이면 흰색 유지).
        /// </summary>
        private int GetCheapestUnitCost()
        {
            if (_activeUnitTypes == null || _activeUnitTypes.Count == 0) return 0;

            int min = int.MaxValue;
            for (int i = 0; i < _activeUnitTypes.Count; i++)
            {
                int cost = UnitProductionStats.GetGoldCost(_activeUnitTypes[i]);
                if (cost < min) min = cost;
            }
            return (min == int.MaxValue) ? 0 : min;
        }

        private void UpdateButtonPortraits(TeamId team, RaceId race)
        {
            var list = GetUnitList(team, race);
            if (_unitButtonPortraits != null)
            {
                for (int i = 0; i < _unitButtonPortraits.Count; i++)
                    if (i < list.Count && _unitButtonPortraits[i] != null) _unitButtonPortraits[i].sprite = list[i].portrait;
            }
        }

        private Sprite GetPortrait(UnitType type)
        {
            if (_currentBarracks == null) return null;
            RaceId race = (_currentBarracks.Team == TeamId.Blue) ? GameRaceContext.BlueRace : GameRaceContext.RedRace;
            var list = GetUnitList(_currentBarracks.Team, race);
            foreach (var entry in list) if (entry.type == type) return entry.portrait;
            return (list.Count > 0) ? list[0].portrait : null;
        }

        private List<UnitPortraitEntry> GetUnitList(TeamId team, RaceId race)
        {
            if (team == TeamId.Blue) return race switch { RaceId.Spirit => _blueSpiritUnits, RaceId.Transcendence => _blueTranscendenceUnits, _ => _blueHumanUnits };
            return race switch { RaceId.Spirit => _redSpiritUnits, RaceId.Transcendence => _redTranscendenceUnits, _ => _redHumanUnits };
        }

        private void BindButtonUnitTypes(RaceId race)
        {
            if (_currentBarracks == null) return;
            var list = GetUnitList(_currentBarracks.Team, race);
            _activeUnitTypes.Clear();
            foreach (var entry in list) _activeUnitTypes.Add(entry.type);

            if (_unitButtons != null)
            {
                for (int i = 0; i < _unitButtons.Count; i++)
                {
                    bool hasUnit = i < _activeUnitTypes.Count;
                    _unitButtons[i].gameObject.SetActive(hasUnit);
                    if (hasUnit && i < _unitCostTexts.Count && _unitCostTexts[i] != null)
                        _unitCostTexts[i].text = $"{UnitProductionStats.GetGoldCost(_activeUnitTypes[i])}";
                }
            }
        }
    }
}