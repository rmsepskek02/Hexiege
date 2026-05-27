// ============================================================================
// GameHudUI.cs
// 화면 상단에 골드/인구수/타일 카운트를 상시 표시하는 HUD.
//
// 역할:
//   1. ResourceUseCase에서 로컬 팀 골드 조회 → 텍스트 업데이트
//   2. PopulationUseCase에서 로컬 팀 인구 조회 → 텍스트 업데이트
//   3. PopulationUseCase에서 Blue/Red 팀 보유 타일 수 조회 → 텍스트 업데이트
//   4. Update() 매 프레임 폴링 (채굴소 수입이 매 프레임 변동하므로)
//
// 씬 구조 (Inspector에서 수동 배치):
//   [UI] Canvas
//     └─ GameHUD (상단 고정, 항상 활성)
//         ├─ GoldText (TMP)
//         ├─ PopulationText (TMP)
//         ├─ BlueTileCountText (TMP)
//         └─ RedTileCountText (TMP)
//
// 멀티플레이 vs 싱글플레이:
//   - 싱글플레이: 로컬 팀 = Blue 고정
//   - 멀티플레이: LocalPlayerTeam.Current 기준으로 자신 팀 표시
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public class GameHudUI : MonoBehaviour, IGameUI
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("자신 팀 정보")]
        [Tooltip("골드 표시 텍스트 (예: '500')")]
        [SerializeField] private TextMeshProUGUI _goldText;

        [Tooltip("인구 표시 텍스트 (예: '3 / 15')")]
        [SerializeField] private TextMeshProUGUI _populationText;

        [Header("타일 카운트")]
        [Tooltip("블루 팀 보유 타일 수 텍스트")]
        [SerializeField] private TextMeshProUGUI _blueTileCountText;

        [Tooltip("레드 팀 보유 타일 수 텍스트")]
        [SerializeField] private TextMeshProUGUI _redTileCountText;

        [Header("설정 버튼")]
        [Tooltip("설정 메뉴를 여는 버튼. 화면 우측 상단에 배치.")]
        [SerializeField] private Button _settingsButton;

        [Tooltip("설정 버튼 클릭 시 열릴 인게임 설정 메뉴 UI.")]
        [SerializeField] private InGameSettingsUI _settingsUI;

        [Header("색상 설정")]
        [Tooltip("프로젝트 공용 UI 색상 설정 에셋. Resources/Config/UIColorConfig.asset 을 연결. " +
                 "인구 텍스트가 가득 찼을 때/평상시 색상이 이 에셋에서 결정된다.")]
        [SerializeField] private UIColorConfig _colorConfig;

        // ====================================================================
        // 의존성 (Initialize로 주입)
        // ====================================================================

        private ResourceUseCase _resource;
        private PopulationUseCase _population;
        private bool _initialized;

        // 불필요한 문자열 할당 줄이기 위한 캐시
        private int _lastGold = -1;
        private int _lastUsedPop = -1;
        private int _lastMaxPop = -1;
        private int _lastBlueTiles = -1;
        private int _lastRedTiles = -1;

        // 인구 텍스트 색상 변경 상태 캐시.
        // 매 프레임 Color 객체 비교/할당이 일어나지 않도록, "이번에 가득 찼었나?"만 기억해
        // 상태 전이가 있을 때만 색을 바꾼다. 초기값 null로 두어 최초 1회는 반드시 갱신되도록 함.
        private bool? _lastPopFull;

        // 멀티플레이 모드 캐시 (매 프레임 NetworkManager 접근 방지)
        private bool _isNetworkMode;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper에서 호출. UseCase 참조 주입.
        /// 네트워크 모드 여부를 확인하여 적팀 패널 활성화/비활성화.
        /// </summary>
        public void Initialize(ResourceUseCase resource, PopulationUseCase population)
        {
            _resource = resource;
            _population = population;
            _initialized = true;

            // 네트워크 모드 확인 — Application 레이어의 NetworkContext 정적 홀더 사용.
            // (Presentation → Unity.Netcode 직접 의존 제거)
            _isNetworkMode = NetworkContext.IsNetworkActive;

            // 설정 버튼 리스너 등록.
            // RemoveListener 후 AddListener: Initialize가 재경기로 인해 여러 번 호출돼도
            // 클릭 시 OnSettingsClicked가 한 번만 호출되도록 보장.
            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveListener(OnSettingsClicked);
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            // 즉시 한 번 갱신
            ResetCachedValues();
            UpdateDisplay();
        }

        /// <summary>
        /// 설정 버튼 클릭 시 호출. 인게임 설정 메뉴 팝업을 연다.
        /// _settingsUI가 Inspector에 연결되지 않은 경우 안전하게 무시.
        /// </summary>
        private void OnSettingsClicked()
        {
            _settingsUI?.Show();
        }

        /// <summary>
        /// 캐시된 값 초기화. 재초기화 시 강제 갱신 보장.
        /// </summary>
        private void ResetCachedValues()
        {
            _lastGold = -1;
            _lastUsedPop = -1;
            _lastMaxPop = -1;
            _lastBlueTiles = -1;
            _lastRedTiles = -1;
            // null로 초기화 → 다음 UpdateDisplay()에서 isFull과 비교 시
            // 반드시 색상 갱신이 1회 강제 발생하도록 한다.
            _lastPopFull = null;
        }

        // ====================================================================
        // IGameUI 구현
        // ====================================================================

        /// <summary>
        /// 게임 시작/재시작 시 호출.
        /// 캐시된 표시값을 초기화하여 다음 프레임에서 강제 갱신되도록 함.
        /// 재경기(Rematch) 시 이전 게임의 골드/인구 값이 잔류하는 것을 방지.
        /// </summary>
        public void OnGameStarted()
        {
            ResetCachedValues();
        }

        // OnGameEnded(): HUD는 게임 종료 시에도 계속 표시되므로 처리 없음 (default 빈 구현 사용).

        // ====================================================================
        // 매 프레임 갱신
        // ====================================================================

        private void Update()
        {
            if (!_initialized) return;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            // 로컬 팀 결정 (싱글플레이는 Blue 고정, 멀티플레이는 LocalPlayerTeam 사용)
            TeamId localTeam = _isNetworkMode ? LocalPlayerTeam.Current : TeamId.Blue;

            // ── 자신 팀 골드 ──
            if (_resource != null && _goldText != null)
            {
                int gold = _resource.GetGold(localTeam);
                if (gold != _lastGold)
                {
                    _lastGold = gold;
                    _goldText.text = gold.ToString();
                }
            }

            // ── 자신 팀 인구 ──
            if (_population != null && _populationText != null)
            {
                int used = _population.GetUsedPopulation(localTeam);
                int max = _population.GetMaxPopulation(localTeam);
                if (used != _lastUsedPop || max != _lastMaxPop)
                {
                    _lastUsedPop = used;
                    _lastMaxPop = max;
                    _populationText.text = $"{used} / {max}";
                }

                // 인구 가득 참 상태 시각화 — used >= max일 때 강조 색상(보통 빨강).
                // 상태가 바뀐 프레임에만 Color 할당이 일어나 GC 부담을 최소화.
                bool isFull = used >= max;
                if (_lastPopFull != isFull)
                {
                    _lastPopFull = isFull;
                    // 색상 설정 에셋이 연결되어 있으면 그 값을, 아니면 합리적인 폴백 색을 사용한다.
                    // (Inspector 미연결 시에도 시각적 경고가 정상 동작하도록 안전 가드.)
                    if (_colorConfig != null)
                        _populationText.color = isFull ? _colorConfig.populationFullColor : _colorConfig.normalTextColor;
                    else
                        _populationText.color = isFull ? Color.red : Color.white;
                }
            }

            // ── 블루 팀 보유 타일 수 ──
            if (_population != null && _blueTileCountText != null)
            {
                int blueTiles = _population.GetMaxPopulation(TeamId.Blue);
                if (blueTiles != _lastBlueTiles)
                {
                    _lastBlueTiles = blueTiles;
                    _blueTileCountText.text = blueTiles.ToString();
                }
            }

            // ── 레드 팀 보유 타일 수 ──
            if (_population != null && _redTileCountText != null)
            {
                int redTiles = _population.GetMaxPopulation(TeamId.Red);
                if (redTiles != _lastRedTiles)
                {
                    _lastRedTiles = redTiles;
                    _redTileCountText.text = redTiles.ToString();
                }
            }

        }
    }
}
