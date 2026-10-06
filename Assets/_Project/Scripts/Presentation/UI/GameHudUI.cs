// ============================================================================
// GameHudUI.cs
// 화면 상단에 골드/인구수/타일 카운트를 상시 표시하는 HUD.
//
// 역할:
//   1. ResourceUseCase에서 로컬 팀 골드 조회 → 텍스트 업데이트
//   2. PopulationUseCase에서 로컬 팀 인구 조회 → 텍스트 업데이트
//   3. PopulationUseCase에서 Blue/Red 팀 보유 타일 수 조회 → 텍스트 업데이트
//   4. Update() 매 프레임 폴링 — 이 HUD 는 값이 바뀌었다는 알림을 구독하지 않고 매 프레임
//      값을 읽어 이전 값과 비교한다.
//      🔴 골드에 변경 알림이 없는 것은 아니다 — 자원 UseCase 가 골드를 더하거나 쓸 때마다
//         자원 변경 이벤트(GameEvents.OnResourceChanged)를 발행하고, 생산 패널·건물 배치 패널
//         같은 다른 UI 는 그것을 구독한다. 이 HUD 만 구독하지 않고 폴링한다.
//      골드는 수입이 조금씩 누적되다가 1골드를 넘는 순간 올라가고 소비·환불로도 바뀌므로,
//      아무 프레임에나 값이 달라질 수 있다.
//      ⚠️ 수입원은 채굴소 하나가 아니다. 자원 UseCase 의 수입 틱이 팀 기본 수입과 채굴소 수입을
//         각각 따로 누적한다. 다만 기본 수입의 초당 값은 게임 설정 에셋이 정하며 0 이면 그 갈래는
//         수입을 내지 않는다 — 채굴소가 0개여도 골드가 오르는지는 그 에셋 값에 달려 있으므로
//         읽기 전에 에셋 값을 직접 확인한다.
//
// 씬 구조 (Game.unity 를 직접 열어 확인한 실제 계층 — Inspector에서 수동 배치):
//   [UI] (Canvas)
//     └─ SafeAreaContainer                 ← UI 공통 규칙 4(SafeArea 컨테이너 구조)
//         └─ GameHUD (상단 고정, 활성 상태로 저장돼 있다)
//             ├─ StatsPanel                ← 아래 네 행을 담는 컨테이너
//             │   ├─ 골드 행      (아이콘 + 텍스트 → _goldText)
//             │   ├─ 인구 행      (아이콘 + 텍스트 → _populationText)
//             │   ├─ 블루 타일 행 (아이콘 + 텍스트 → _blueTileCountText)
//             │   └─ 레드 타일 행 (아이콘 + 텍스트 → _redTileCountText)
//             └─ SettingsButton            (→ _settingsButton)
//   ⚠️ GameHUD 가 Canvas 의 직속 자식이 아니라는 점에 주의 — 사이에 SafeAreaContainer 가 있다.
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

        // 멀티플레이 모드 캐시 — Initialize() 에서 한 번 읽어 둔 값.
        // ⚠️ 2026-10-05 실측 — 이 값의 출처는 Application 레이어 정적 홀더의 단순 bool 프로퍼티라
        //    캐시하지 않고 매 프레임 직접 읽어도 비용이 사실상 없다. 즉 종전 주석이 캐시 이유로
        //    적어 둔 "네트워크 매니저를 매 프레임 건드리지 않으려고"는 지금 코드에서 성립하지 않는다
        //    (이 파일은 그 매니저 타입을 아예 참조하지 않는다 — 아래 Initialize() 의 주석 참조).
        // ✅ 그래도 캐시를 없애도 되는지는 이 자리에서 판단하지 않는다 — 연결이 끊기면 그 정적
        //    홀더가 싱글플레이 기본값으로 되돌아가므로, "경기 도중에 표시 기준이 바뀌어도 되는가"를
        //    먼저 정해야 하는 문제다.
        private bool _isNetworkMode;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper에서 호출. UseCase 참조 주입.
        /// 네트워크 모드인지 여부를 여기서 한 번 읽어 캐시한다 — 로컬 팀을 무엇으로 볼지
        /// 결정하는 데만 쓰이며, 이 메서드가 켜거나 끄는 패널은 없다.
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
