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

using Unity.Netcode;
using UnityEngine;
using TMPro;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public class GameHudUI : MonoBehaviour
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

            // 네트워크 모드 확인 (Host 또는 Client)
            _isNetworkMode = NetworkManager.Singleton != null &&
                             (NetworkManager.Singleton.IsHost ||
                              NetworkManager.Singleton.IsClient);

            // 즉시 한 번 갱신
            ResetCachedValues();
            UpdateDisplay();
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
        }

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
