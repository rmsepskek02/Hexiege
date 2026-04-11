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

        [Tooltip("배럭 건설 버튼")]
        [SerializeField] private Button _barracksButton;

        [Tooltip("채굴소 건설 버튼")]
        [SerializeField] private Button _miningPostButton;

        [Tooltip("취소 버튼")]
        [SerializeField] private Button _cancelButton;

        [Header("Button Portrait Images")]
        [Tooltip("배럭 버튼 초상화 Image 컴포넌트")]
        [SerializeField] private Image _barracksButtonPortrait;

        [Tooltip("채굴소 버튼 초상화 Image 컴포넌트")]
        [SerializeField] private Image _miningPostButtonPortrait;

        [Header("Building Portraits — 종족+팀별 (팀×종족 = 6세트)")]

        [Tooltip("Blue 팀 — 인간 종족 건물 초상화")]
        [SerializeField] private BuildingRacePortraitSet _blueHumanPortraits;

        [Tooltip("Blue 팀 — 정령 종족 건물 초상화")]
        [SerializeField] private BuildingRacePortraitSet _blueSpiritPortraits;

        [Tooltip("Blue 팀 — 초월 종족 건물 초상화")]
        [SerializeField] private BuildingRacePortraitSet _blueTranscendencePortraits;

        [Tooltip("Red 팀 — 인간 종족 건물 초상화")]
        [SerializeField] private BuildingRacePortraitSet _redHumanPortraits;

        [Tooltip("Red 팀 — 정령 종족 건물 초상화")]
        [SerializeField] private BuildingRacePortraitSet _redSpiritPortraits;

        [Tooltip("Red 팀 — 초월 종족 건물 초상화")]
        [SerializeField] private BuildingRacePortraitSet _redTranscendencePortraits;

        /// <summary>
        /// 종족+팀별 건물 초상화 스프라이트 세트.
        /// 배럭과 채굴소 각각 종족마다 외형이 다르므로 종족별 스프라이트를 보유.
        /// Inspector에서 팀×종족 = 6세트를 설정.
        /// </summary>
        [System.Serializable]
        public struct BuildingRacePortraitSet
        {
            [Tooltip("배럭(병영) 초상화 스프라이트")]
            public Sprite barracks;

            [Tooltip("채굴소 초상화 스프라이트")]
            public Sprite miningPost;
        }

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

        /// <summary>
        /// GameBootstrapper에서 호출. UseCase 참조 설정 및 버튼 이벤트 연결.
        /// NetworkBuildingController는 멀티플레이 시에만 주입. 싱글플레이 시 null.
        /// </summary>
        public void Initialize(BuildingPlacementUseCase buildingPlacement,
            ResourceUseCase resource, GameConfig config,
            NetworkBuildingController networkBuildingController = null)
        {
            _buildingPlacement = buildingPlacement;
            _resource = resource;
            _config = config;
            _networkBuildingController = networkBuildingController;

            // 시작 시 팝업 비활성 (AnimatedPanel.Awake()에서 이미 비활성화되지만 명시적 보장)

            // 버튼 이벤트 연결
            // 공유 Background 닫기는 Show()/Close()에서 Register/Unregister로 처리

            if (_barracksButton != null)
                _barracksButton.onClick.AddListener(() => PlaceAndClose(BuildingType.Barracks));

            if (_miningPostButton != null)
                _miningPostButton.onClick.AddListener(() => PlaceAndClose(BuildingType.MiningPost));

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);
        }

        // ====================================================================
        // 팝업 표시/닫기
        // ====================================================================

        /// <summary>
        /// 건물 선택 팝업 표시. InputHandler에서 호출.
        /// </summary>
        /// <param name="coord">건물을 배치할 타일 좌표</param>
        /// <param name="team">배치하는 팀</param>
        public void Show(HexCoord coord, TeamId team)
        {
            _targetCoord = coord;
            _currentTeam = team;

            UpdateButtonPortraits(team);

            if (_buildingPlacement != null)
            {
                // 금광 타일: MiningPost만 활성, Barracks 비활성
                // 일반 타일: Barracks만 활성, MiningPost 비활성
                bool canMine = _buildingPlacement.CanPlaceBuildingType(
                    BuildingType.MiningPost, coord, team);
                bool canBarracks = _buildingPlacement.CanPlaceBuildingType(
                    BuildingType.Barracks, coord, team);

                if (_miningPostButton != null)
                    _miningPostButton.interactable = canMine;
                if (_barracksButton != null)
                    _barracksButton.interactable = canBarracks;
            }

            _popup?.Show();

            // 공유 Background에 이 패널의 Close 콜백 등록
            // Background 터치 시 Close()가 호출되어 팝업이 닫힌다
            _sharedBackground?.Register(Close);
        }

        /// <summary>
        /// 팀과 종족에 맞는 초상화 스프라이트를 버튼 Image에 적용.
        /// Show() 호출 시 실행되어, 현재 팀의 종족에 해당하는 건물 이미지를 버튼에 세팅.
        /// GameRaceContext에서 팀별 종족을 조회하여 6세트 중 적절한 세트를 선택.
        /// </summary>
        private void UpdateButtonPortraits(TeamId team)
        {
            // GameRaceContext에서 해당 팀의 종족을 조회
            RaceId race = team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            // 팀×종족 조합에 맞는 초상화 세트를 가져옴
            var set = GetBuildingPortraitSet(team, race);

            if (_barracksButtonPortrait   != null) _barracksButtonPortrait.sprite   = set.barracks;
            if (_miningPostButtonPortrait != null) _miningPostButtonPortrait.sprite = set.miningPost;
        }

        /// <summary>
        /// 팀과 종족 조합으로 6세트 중 해당하는 BuildingRacePortraitSet을 반환.
        /// Blue 팀 3종족(Human/Spirit/Transcendence) + Red 팀 3종족 = 총 6세트.
        /// ProductionPanelUI.GetPortraitSet()과 동일한 패턴.
        /// </summary>
        private BuildingRacePortraitSet GetBuildingPortraitSet(TeamId team, RaceId race)
        {
            if (team == TeamId.Blue)
            {
                return race switch
                {
                    RaceId.Spirit        => _blueSpiritPortraits,
                    RaceId.Transcendence => _blueTranscendencePortraits,
                    _                    => _blueHumanPortraits   // Human 또는 기본값
                };
            }
            else
            {
                return race switch
                {
                    RaceId.Spirit        => _redSpiritPortraits,
                    RaceId.Transcendence => _redTranscendencePortraits,
                    _                    => _redHumanPortraits    // Human 또는 기본값
                };
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

            _buildingPlacement?.PlaceBuilding(type, _currentTeam, _targetCoord);
            Close();
        }

        /// <summary> 건물 타입별 비용. </summary>
        private int GetBuildingCost(BuildingType type)
        {
            if (_config == null) return 0;
            switch (type)
            {
                case BuildingType.Barracks: return _config.BarracksCost;
                case BuildingType.MiningPost: return _config.MiningPostCost;
                default: return 0; // Castle은 자동 배치이므로 비용 없음
            }
        }
    }
}
