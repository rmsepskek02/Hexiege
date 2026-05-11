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
            if (state != null && state.IsAutoMode && state.AutoTypes.Contains(type)) HandleToggleAuto(type);
            else
            {
                if (_networkProductionController != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    _networkProductionController.RequestEnqueueServerRpc(_currentBarracks.Id, (int)type, (int)_currentBarracks.Team);
                else
                    _production.EnqueueUnit(_currentBarracks.Id, type);
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
            if (_goldText != null && _resource != null) _goldText.text = _resource.GetGold(_currentBarracks.Team).ToString();
            if (_populationText != null && _population != null) _populationText.text = $"{_population.GetUsedPopulation(_currentBarracks.Team)}/{_population.GetMaxPopulation(_currentBarracks.Team)}";
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