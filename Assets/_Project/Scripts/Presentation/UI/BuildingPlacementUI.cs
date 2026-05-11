// ============================================================================
// BuildingPlacementUI.cs
// 자기 팀 빈 타일을 탭했을 때 건물 선택 팝업을 표시하는 UI 컴포넌트.
//
// 인터랙션 흐름:
//   1. InputHandler가 자기 팀 빈 타일 탭 감지
//   2. BuildingPlacementUI.Show(coord, team) 호출 → 팝업 표시
//   3. 플레이어가 배럭/채굴소 버튼 탭 → PlaceAndClose() → 건물 배치 + 팝업 닫기
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
//     │       └─ VerticalButtons (Vertical Layout Group)
//     │           ├─ Buttons1 (Horizontal Layout) — Castle, Barracks, MiningPost
//     │           └─ Buttons2 (Horizontal Layout) — Castle, Barracks, MiningPost
//     └─ EventSystem
//
// BuildingPopup 래퍼 하나를 SetActive 토글하면 하위 전체(Background + Panel)가 함께 제어됨.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, UI).
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
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

        [Header("Building Entries — 종족+팀별 (팀×종족 = 6세트)")]
        [SerializeField] private List<BuildingPortraitEntry> _blueHumanBuildings;
        [SerializeField] private List<BuildingPortraitEntry> _blueSpiritBuildings;
        [SerializeField] private List<BuildingPortraitEntry> _blueTranscendenceBuildings;
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

        // ====================================================================
        // 초기화
        // ====================================================================

        public void Initialize(BuildingPlacementUseCase buildingPlacement,
            ResourceUseCase resource, GameConfig config,
            NetworkBuildingController networkBuildingController = null)
        {
            _buildingPlacement = buildingPlacement;
            _resource = resource;
            _config = config;
            _networkBuildingController = networkBuildingController;

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);
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
            if (_buildingButtons != null)
            {
                for (int i = 0; i < _buildingButtons.Count; i++)
                {
                    if (i < list.Count)
                    {
                        var entry = list[i];
                        _buildingButtons[i].gameObject.SetActive(true);
                        _buildingButtons[i].onClick.RemoveAllListeners();
                        _buildingButtons[i].onClick.AddListener(() => PlaceAndClose(entry.type));

                        // 건설 비용 텍스트 업데이트
                        if (i < _buildingCostTexts.Count && _buildingCostTexts[i] != null)
                        {
                            int cost = BuildingStats.GetGoldCost(entry.type, race);
                            _buildingCostTexts[i].SetText($"{cost}G");
                        }

                        // 배치 가능 여부 체크
                        if (_buildingPlacement != null)
                        {
                            _buildingButtons[i].interactable = _buildingPlacement.CanPlaceBuildingType(entry.type, coord, team);
                        }
                    }
                    else
                    {
                        _buildingButtons[i].gameObject.SetActive(false);
                    }
                }
            }

            _popup?.Show();
            _sharedBackground?.Register(Close);
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
                        _buildingCostTexts[i].SetText($"{cost}G");
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
                    return; // 골드 부족 → 배치하지 않음
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
