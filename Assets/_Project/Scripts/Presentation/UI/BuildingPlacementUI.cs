// ============================================================================
// BuildingPlacementUI.cs
// 자기 팀 빈 타일을 탭했을 때 건물 선택 팝업을 표시하는 UI 컴포넌트.
//
// 인터랙션 흐름:
//   1. InputHandler가 자기 팀 빈 타일 탭 감지
//   2. BuildingPlacementUI.Show(coord, team) 호출 → 팝업 표시
//   3. 플레이어가 건물 버튼 탭 → PlaceAndClose() → 건물 배치 + 팝업 닫기
//   4. Background 터치, CancelButton → Close()
//
// Castle은 게임 시작 시 자동 배치되므로 이 UI에 포함하지 않음.
//
// UI 계층 구조 (에디터에서 생성):
//   [UI] (Canvas - Screen Space Overlay)
//     ├─ BuildingPopup (_popup → 이 하나만 토글)
//     │   ├─ Background (전체화면 검은 오버레이 + Button → Close)
//     │   ├─ CancelButton
//     │   └─ BuildingPanel
//     │       └─ BuildingButtonGrid (Layout Group)
//     │           └─ Button × N (종족별 건물 수에 맞게 구성)
//     │               Human/Spirit: 생산건물 3종 + 비생산건물 3종 + 채굴소 = 7개
//     │               Transcendence: 생산건물 3종 + 비생산건물 4종 + 채굴소 = 8개
//     └─ EventSystem
//
// BuildingPopup 래퍼 하나를 SetActive 토글하면 하위 전체(Background + Panel)가 함께 제어됨.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, UI).
// ============================================================================

using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public class BuildingPlacementUI : MonoBehaviour, IGameUI
    {
        // ====================================================================
        // Inspector에서 설정할 UI 참조
        // ====================================================================

        [Header("UI References")]
        [Tooltip("팝업 래퍼 (AnimatedPanel 부착, Show()/Hide()로 토글)")]
        [SerializeField] private AnimatedPanel _popup;

        [Header("Shared Background")]
        [Tooltip("Canvas 직속 공유 Background (터치 시 팝업 닫기)")]
        [SerializeField] private SharedBackgroundButton _sharedBackground;

        [Header("Dynamic Building Buttons")]
[Tooltip("건물 배치 버튼 리스트")]
        [SerializeField] private List<Button> _buildingButtons;

        [Tooltip("버튼 리스트와 1:1 매칭되는 아이콘 Image 컴포넌트")]
        [SerializeField] private List<Image> _buildingButtonIcons;

        [Tooltip("버튼 리스트와 1:1 매칭되는 비용 텍스트")]
        [SerializeField] private List<TextMeshProUGUI> _buildingCostTexts;

        [Tooltip("취소 버튼")]
        [SerializeField] private Button _cancelButton;

        [System.Serializable]
        public struct BuildingPortraitEntry
        {
            public BuildingType type;
            public Sprite icon;
        }

        [Header("Blue Team — Building Entries")]
        [SerializeField] private List<BuildingPortraitEntry> _blueHumanBuildings;
        [SerializeField] private List<BuildingPortraitEntry> _blueSpiritBuildings;
        [SerializeField] private List<BuildingPortraitEntry> _blueTranscendenceBuildings;

        [Header("Red Team — Building Entries")]
        [SerializeField] private List<BuildingPortraitEntry> _redHumanBuildings;
        [SerializeField] private List<BuildingPortraitEntry> _redSpiritBuildings;
        [SerializeField] private List<BuildingPortraitEntry> _redTranscendenceBuildings;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 건물 배치 UseCase 참조. </summary>
        private BuildingPlacementUseCase _buildingPlacement;

        /// <summary> 자원 UseCase 참조 (골드 차감). </summary>
        private ResourceUseCase _resource;

        /// <summary> 건물 비용 참조. </summary>
        private GameConfig _config;

        /// <summary>
        /// 네트워크 건물 배치 컨트롤러 참조.
        /// 멀티플레이 시 UseCase 직접 호출 대신 ServerRpc를 통해 배치 요청을 서버에 전달.
        /// 싱글플레이 시 null이어도 무방.
        /// </summary>
        private NetworkBuildingController _networkBuildingController;

        /// <summary> 현재 선택된 타일 좌표 (팝업 표시 중). </summary>
        private HexCoord _targetCoord;

        /// <summary> 현재 배치 중인 팀. </summary>
        private TeamId _currentTeam;

        /// <summary> 팝업이 열려있는지 여부. InputHandler에서 확인. </summary>
        public bool IsOpen => _popup != null && _popup.IsVisible;

        /// <summary> 팝업이 닫힌 프레임. 같은 프레임 클릭 통과 방지용. </summary>
        public int ClosedFrame { get; private set; } = -1;

        /// <summary>
        /// 골드 변경 이벤트(OnResourceChanged) 구독 토큰.
        /// 팝업이 열린 동안만 구독을 유지해 비용 텍스트 색상을 실시간으로 재평가하고,
        /// Close() 시 즉시 Dispose하여 불필요한 콜백 호출을 막는다.
        /// </summary>
        private IDisposable _resourceSubscription;

        /// <summary>
        /// 각 건물 버튼에 대응되는 CanvasGroup 캐시 리스트.
        /// _buildingButtons와 1:1로 매칭되며, Awake()에서 자동으로 채워진다.
        ///
        /// 빈 슬롯을 숨길 때 SetActive(false) 대신 CanvasGroup.alpha=0 을 사용하는 이유:
        ///   - HorizontalLayoutGroup은 SetActive(false) 자식을 레이아웃 계산에서 완전히 제외함.
        ///   - 그 결과 9개 슬롯 중 7번만 남으면 Child Force Expand Width=true 설정에 의해
        ///     7번 버튼 하나가 전체 가로폭을 채우는 레이아웃 깨짐이 발생.
        ///   - CanvasGroup은 GameObject가 활성 상태를 유지하므로 RectTransform 크기는 보존되고,
        ///     alpha/interactable/blocksRaycasts만 0으로 만들어 시각적·상호작용적으로 숨김 처리만 함.
        ///
        /// Inspector에는 노출하지 않으며, 버튼에 CanvasGroup이 없으면 Awake에서 자동 부착한다.
        /// </summary>
        private List<CanvasGroup> _buttonCanvasGroups;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper가 호출하는 초기화 메서드.
        /// UseCase 등 의존성을 주입받고, 버튼별 CanvasGroup 캐시를 함께 구성한다.
        ///
        /// [중요 — 왜 Awake()를 쓰지 않고 Initialize()에서 처리하는가?]
        ///   BuildingPopup GameObject는 씬 시작 시 비활성(SetActive=false) 상태로 배치된다.
        ///   Unity의 Awake()는 GameObject가 비활성이면 호출되지 않는다.
        ///   (정확히는 처음 활성화되는 시점에서야 호출됨)
        ///
        ///   따라서 만약 CanvasGroup 캐시 구성을 Awake()에 두면:
        ///     1. 씬 로드 시 BuildingPopup은 비활성 → Awake() 미호출 → _buttonCanvasGroups = null
        ///     2. 첫 Show() 호출 시 CanvasGroup 캐시가 null이라 alpha=1 설정이 건너뛰어짐
        ///     3. _popup.Show() 내부에서 SetActive(true) 발생 → 이 순간 Awake() 실행
        ///     4. Awake()가 모든 CanvasGroup의 alpha를 0으로 초기화
        ///     5. 결과: 팝업은 열렸지만 버튼이 전혀 안 보임 (두 번째 클릭부터 정상)
        ///
        ///   Initialize()는 GameBootstrapper에서 직접 호출하는 일반 C# 메서드이므로
        ///   GameObject가 비활성 상태여도 정상 실행된다 → 캐시 구성이 보장됨.
        ///
        /// [CanvasGroup 캐시 구성 로직]
        ///   각 건물 버튼 GameObject에 CanvasGroup이 부착되어 있으면 그대로 캐시하고,
        ///   없다면 AddComponent로 자동 부착한다. 그 결과 Inspector에서 별도의 CanvasGroup
        ///   배선 작업을 하지 않아도 빈 슬롯 숨김(alpha=0) 동작이 정상 작동한다.
        ///
        ///   초기 상태는 모두 alpha=0 (숨김)으로 설정해, Show() 호출 전 잔존 표시를 방지한다.
        /// </summary>
        public void Initialize(BuildingPlacementUseCase buildingPlacement,
            ResourceUseCase resource, GameConfig config,
            NetworkBuildingController networkBuildingController = null)
        {
            // ---- 1) 의존성 주입 ----
            _buildingPlacement = buildingPlacement;
            _resource = resource;
            _config = config;
            _networkBuildingController = networkBuildingController;

            // 취소 버튼 클릭 시 팝업을 닫도록 리스너 등록.
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);

            // ---- 2) 건물 버튼별 CanvasGroup 캐시 구성 ----
            // 버튼 리스트가 비어있으면(에디터에서 미배선) 빈 리스트로 초기화하고 종료.
            // null 체크 후 List 할당으로 NRE 방지.
            if (_buildingButtons == null)
            {
                _buttonCanvasGroups = new List<CanvasGroup>();
                return;
            }

            _buttonCanvasGroups = new List<CanvasGroup>(_buildingButtons.Count);

            for (int i = 0; i < _buildingButtons.Count; i++)
            {
                var button = _buildingButtons[i];
                if (button == null)
                {
                    // 빈 슬롯 자리에는 null을 추가해 인덱스 정합성을 유지.
                    _buttonCanvasGroups.Add(null);
                    continue;
                }

                // 버튼 GameObject에서 CanvasGroup 조회 → 없으면 자동 부착.
                // TryGetComponent는 가비지 할당이 없는 권장 패턴.
                if (!button.TryGetComponent(out CanvasGroup cg))
                {
                    cg = button.gameObject.AddComponent<CanvasGroup>();
                }

                // 초기 상태: 완전히 숨김. Show()에서 필요한 슬롯만 alpha=1로 켠다.
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;

                _buttonCanvasGroups.Add(cg);
            }
        }

        public void Show(HexCoord coord, TeamId team)
        {
            _targetCoord = coord;
            _currentTeam = team;

            // 1. 해당 팀의 현재 종족 조회
            RaceId race = team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            Debug.Log($"[BuildingPlacementUI] Show - Team: {team}, Race: {race}");

            // 2. 종족별 건물 리스트 가져오기
            var list = GetBuildingList(team, race);

            // 3. 버튼 이미지(아이콘) 및 비용 업데이트
            UpdateButtonPortraits(team, race);

            // 4. 버튼 활성화 및 건설 로직 연결
            //
            // 빈 슬롯은 SetActive(false)가 아닌 CanvasGroup으로 숨긴다.
            // (이유: HorizontalLayoutGroup이 SetActive(false) 자식을 레이아웃에서 제외하여
            //  남은 버튼 1개가 전체 가로폭을 채우는 레이아웃 깨짐 발생.
            //  CanvasGroup은 GameObject 활성 상태를 유지하므로 RectTransform 공간은 보존됨.)
            if (_buildingButtons != null)
            {
                for (int i = 0; i < _buildingButtons.Count; i++)
                {
                    // 해당 인덱스의 CanvasGroup 캐시 (Awake에서 1:1 매칭으로 구성됨).
                    // 안전을 위해 범위/null 체크 후 사용.
                    CanvasGroup cg = (_buttonCanvasGroups != null && i < _buttonCanvasGroups.Count)
                        ? _buttonCanvasGroups[i]
                        : null;

                    if (i < list.Count)
                    {
                        var entry = list[i];

                        // 슬롯 활성: 시각적으로 보이고 클릭 가능 상태로 전환.
                        if (cg != null)
                        {
                            cg.alpha = 1f;
                            cg.interactable = true;
                            cg.blocksRaycasts = true;
                        }

                        _buildingButtons[i].onClick.RemoveAllListeners();
                        _buildingButtons[i].onClick.AddListener(() => PlaceAndClose(entry.type));

                        // 건설 비용 텍스트 업데이트
                        if (i < _buildingCostTexts.Count && _buildingCostTexts[i] != null)
                        {
                            int cost = BuildingStats.GetGoldCost(entry.type, race);
                            _buildingCostTexts[i].SetText($"{cost}");
                        }

                        // 배치 가능 여부 체크
                        if (_buildingPlacement != null)
                        {
                            _buildingButtons[i].interactable = _buildingPlacement.CanPlaceBuildingType(entry.type, coord, team);
                        }
                    }
                    else
                    {
                        // 빈 슬롯: 시각적으로 숨기되 레이아웃 공간은 유지.
                        // GameObject는 SetActive(true) 상태이므로 HorizontalLayoutGroup이
                        // 이 슬롯의 RectTransform 크기를 그대로 계산에 포함시킨다.
                        if (cg != null)
                        {
                            cg.alpha = 0f;
                            cg.interactable = false;
                            cg.blocksRaycasts = false;
                        }
                    }
                }
            }

            _popup?.Show();
            _sharedBackground?.Register(Close);

            // 팝업이 열리는 순간 현재 보유 골드를 기준으로 비용 텍스트 색상을 즉시 평가.
            // (살 수 없는 건물의 비용은 빨간색, 살 수 있는 건물은 흰색으로 표시)
            UpdateCostTextColors();

            // 팝업이 열려 있는 동안 골드 변동이 발생하면 색상을 실시간으로 재평가하도록 구독.
            // 자기 팀(_currentTeam)에 해당하는 이벤트만 필터링하여 불필요한 갱신을 방지.
            // 이전에 남아있을 수 있는 구독은 안전하게 해제 후 새로 시작.
            _resourceSubscription?.Dispose();
            _resourceSubscription = GameEvents.OnResourceChanged
                .Where(evt => evt.Team == _currentTeam)
                .Subscribe(_ => UpdateCostTextColors());
        }

        /// <summary>
        /// 현재 팀의 보유 골드와 각 건물의 건설 비용을 비교하여 비용 텍스트 색상을 갱신.
        ///
        /// 규칙:
        ///   - 보유 골드 < 비용 → 빨간색 (살 수 없음을 시각적으로 알림)
        ///   - 보유 골드 >= 비용 → 흰색 (정상)
        ///   - 버튼이 비활성(SetActive=false)이거나 텍스트가 null 이면 건너뜀.
        ///
        /// 호출 시점:
        ///   1. Show() — 팝업이 열리는 순간 1회
        ///   2. OnResourceChanged 이벤트 — 골드가 변동될 때마다
        /// </summary>
        private void UpdateCostTextColors()
        {
            if (_resource == null || _buildingCostTexts == null) return;

            // 현재 팀의 종족 조회 (Show()에서 _currentTeam이 세팅된 상태)
            RaceId race = _currentTeam == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            // 종족별 건물 리스트 조회
            var list = GetBuildingList(_currentTeam, race);

            // 현재 보유 골드 (한 번만 조회 후 재사용)
            int currentGold = _resource.GetGold(_currentTeam);

            for (int i = 0; i < _buildingCostTexts.Count; i++)
            {
                // 텍스트 또는 매칭되는 버튼이 없으면 건너뜀
                if (_buildingCostTexts[i] == null) continue;
                if (_buildingButtons == null || i >= _buildingButtons.Count) continue;
                if (_buildingButtons[i] == null) continue;

                // 빈 슬롯 판별 — 더 이상 GameObject.activeSelf로 판단하지 않는다.
                // (변경 이유: 빈 슬롯도 SetActive(true) 상태이므로 activeSelf는 항상 true.
                //  대신 CanvasGroup.alpha < 0.5 인 슬롯을 "숨겨진 빈 슬롯"으로 간주하여 스킵.)
                if (_buttonCanvasGroups == null
                    || i >= _buttonCanvasGroups.Count
                    || _buttonCanvasGroups[i] == null
                    || _buttonCanvasGroups[i].alpha < 0.5f) continue;

                // 리스트 범위 밖이면 흰색으로 유지
                if (i >= list.Count)
                {
                    _buildingCostTexts[i].color = Color.white;
                    continue;
                }

                int cost = BuildingStats.GetGoldCost(list[i].type, race);

                // 보유 골드가 비용보다 적으면 빨간색으로 표시
                _buildingCostTexts[i].color = (currentGold < cost) ? Color.red : Color.white;
            }
        }

        private void UpdateButtonPortraits(TeamId team, RaceId race)
        {
            var list = GetBuildingList(team, race);
            if (_buildingButtonIcons != null)
            {
                for (int i = 0; i < _buildingButtonIcons.Count; i++)
                {
                    if (i < list.Count && _buildingButtonIcons[i] != null)
                    {
                        _buildingButtonIcons[i].sprite = list[i].icon;
                    }
                }
            }
        }

        private List<BuildingPortraitEntry> GetBuildingList(TeamId team, RaceId race)
        {
            if (team == TeamId.Blue)
            {
                return race switch
                {
                    RaceId.Spirit        => _blueSpiritBuildings,
                    RaceId.Transcendence => _blueTranscendenceBuildings,
                    _                    => _blueHumanBuildings
                };
            }
            else
            {
                return race switch
                {
                    RaceId.Spirit        => _redSpiritBuildings,
                    RaceId.Transcendence => _redTranscendenceBuildings,
                    _                    => _redHumanBuildings
                };
            }
        }

        private void UpdateBuildingStatsText(TeamId team, RaceId race)
        {
            var list = GetBuildingList(team, race);
            if (_buildingCostTexts != null)
            {
                for (int i = 0; i < _buildingCostTexts.Count; i++)
                {
                    if (i < list.Count && _buildingCostTexts[i] != null)
                    {
                        int cost = BuildingStats.GetGoldCost(list[i].type, race);
                        _buildingCostTexts[i].SetText($"{cost}");
                    }
                }
            }
        }

        /// <summary>
        /// 팝업 닫기. Background 터치, 취소 버튼, 또는 건물 배치 후 호출.
        /// </summary>
        public void Close()
        {
            ClosedFrame = Time.frameCount;

            // 골드 변경 이벤트 구독 해제 — 팝업이 닫혀 있는 동안 불필요한 콜백을 막는다.
            _resourceSubscription?.Dispose();
            _resourceSubscription = null;

            // 비용 텍스트 색상을 모두 흰색으로 초기화.
            // 다음 Show() 호출 시 빨간색이 잔존하지 않은 깨끗한 상태에서 시작하기 위함.
            if (_buildingCostTexts != null)
            {
                for (int i = 0; i < _buildingCostTexts.Count; i++)
                {
                    if (_buildingCostTexts[i] != null)
                        _buildingCostTexts[i].color = Color.white;
                }
            }

            // 공유 Background 콜백 해제 (Hide 애니메이션 중 추가 터치 방지)
            _sharedBackground?.Unregister();

            _popup?.Hide();
        }

        // ====================================================================
        // IGameUI 구현
        // ====================================================================

        /// <summary>
        /// 게임 종료 시 호출.
        /// 건물 배치 팝업이 열려있다면 닫아서 게임 종료 화면이 깨끗하게 표시되도록 함.
        /// Close() 내부에서 SharedBackgroundButton.Unregister()도 호출하므로 안전.
        /// </summary>
        public void OnGameEnded()
        {
            Close();
        }

        /// <summary>
        /// 게임 시작/재시작 시 호출.
        /// 혹시 열려있을 수 있는 팝업을 닫아서 초기 상태 보장.
        /// 재경기(Rematch) 시 이전 게임에서 열린 팝업이 남아있는 것을 방지.
        /// </summary>
        public void OnGameStarted()
        {
            Close();
        }

        // ====================================================================
        // 건물 배치
        // ====================================================================

        /// <summary>
        /// 선택된 건물 타입을 배치하고 팝업 닫기.
        /// 싱글플레이: UseCase 직접 호출 (기존 흐름 유지).
        /// 멀티플레이: NetworkBuildingController.RequestBuildServerRpc를 통해
        ///             서버에 배치 요청을 위임. 골드 차감과 실제 배치는 서버에서 처리.
        /// </summary>
        private void PlaceAndClose(BuildingType type)
        {
            bool isNetworkMode = NetworkManager.Singleton != null &&
                                 (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient);

            if (isNetworkMode && _networkBuildingController != null)
            {
                // 멀티플레이: 로컬에서는 골드 확인만 하고, 실제 배치/차감은 서버에 위임
                if (_resource != null && _config != null)
                {
                    int cost = GetBuildingCost(type);
                    if (!_resource.CanAfford(_currentTeam, cost))
                    {
                        // 골드 부족 → 요청 자체를 보내지 않음 (서버에서도 검증하지만 불필요한 RPC 방지)
                        Debug.Log($"[Network] 건물 배치 클라이언트 사전 검증 실패: 골드 부족. 타입={type}, 팀={_currentTeam}");
                        Close();
                        return;
                    }
                }

                // 서버에 배치 요청 전송
                _networkBuildingController.RequestBuildServerRpc(
                    (int)type,
                    (int)_currentTeam,
                    _targetCoord.Q,
                    _targetCoord.R);

                Close();
                return;
            }

            // 싱글플레이: 기존 흐름 그대로 유지 (UseCase 직접 호출)
            if (_resource != null && _config != null)
            {
                int cost = GetBuildingCost(type);
                if (!_resource.CanAfford(_currentTeam, cost))
                {
                    // 골드 부족 시 사용자에게 토스트 메시지로 피드백.
                    // 팝업은 그대로 유지하여, 빨간색으로 표시된 비용 텍스트와 함께
                    // 사용자가 어떤 건물이 부족한지 즉시 인지할 수 있게 한다.
                    ToastUI.Show(ToastKey.GoldInsufficient);
                    return; // 골드 부족 → 배치하지 않음 (팝업 유지)
                }
                _resource.SpendGold(_currentTeam, cost);
            }

            // 싱글플레이: 현재 팀의 종족을 GameRaceContext에서 조회하여 건물 HP 결정에 사용
            RaceId singleRace = _currentTeam == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            _buildingPlacement?.PlaceBuilding(type, _currentTeam, _targetCoord, singleRace);
            Close();
        }

        /// <summary>
        /// 건물 타입 + 현재 팀 종족 기준 건설 비용을 반환.
        ///
        /// 조회 흐름:
        ///   1. _currentTeam(TeamId.Blue/Red) → GameRaceContext에서 종족 조회
        ///   2. BuildingStats.GetGoldCost(type, race) 호출 → Dictionary에서 값 반환
        ///
        /// Castle은 자동 배치이므로 BuildingStatsConfig에 0으로 설정되어 있어야 함.
        /// </summary>
        private int GetBuildingCost(BuildingType type)
        {
            // 팝업이 열려있는 팀의 종족을 조회 (Show()에서 _currentTeam이 세팅됨)
            RaceId race = _currentTeam == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            return BuildingStats.GetGoldCost(type, race);
        }
    }
}
