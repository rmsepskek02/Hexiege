// ============================================================================
// GameHudUI.cs
// 화면 상단에 골드/인구수를 상시 표시하는 HUD.
//
// 역할:
//   1. ResourceUseCase에서 Blue 팀 골드 조회 → 텍스트 업데이트
//   2. PopulationUseCase에서 Blue 팀 인구 조회 → 텍스트 업데이트
//   3. Update() 매 프레임 폴링 (채굴소 수입이 매 프레임 변동하므로)
//
// 씬 구조 (Inspector에서 수동 배치):
//   [UI] Canvas
//     └─ GameHUD (상단 고정, 항상 활성)
//         ├─ GoldText (TMP)
//         └─ PopulationText (TMP)
//
// 플레이어 = Blue 팀 고정.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using UnityEngine;
using TMPro;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    public class GameHudUI : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("Text References")]
        [Tooltip("골드 표시 텍스트 (예: '500')")]
        [SerializeField] private TextMeshProUGUI _goldText;

        [Tooltip("인구 표시 텍스트 (예: '3 / 15')")]
        [SerializeField] private TextMeshProUGUI _populationText;

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

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper에서 호출. UseCase 참조 주입.
        /// </summary>
        public void Initialize(ResourceUseCase resource, PopulationUseCase population)
        {
            _resource = resource;
            _population = population;
            _initialized = true;

            // 즉시 한 번 갱신
            _lastGold = -1;
            _lastUsedPop = -1;
            _lastMaxPop = -1;
            UpdateDisplay();
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
            // 골드
            if (_resource != null && _goldText != null)
            {
                int gold = _resource.GetGold(TeamId.Blue);
                if (gold != _lastGold)
                {
                    _lastGold = gold;
                    _goldText.text = gold.ToString();
                }
            }

            // 인구
            if (_population != null && _populationText != null)
            {
                int used = _population.GetUsedPopulation(TeamId.Blue);
                int max = _population.GetMaxPopulation(TeamId.Blue);
                if (used != _lastUsedPop || max != _lastMaxPop)
                {
                    _lastUsedPop = used;
                    _lastMaxPop = max;
                    _populationText.text = $"{used} / {max}";
                }
            }
        }
    }
}
