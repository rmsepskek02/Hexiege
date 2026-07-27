// ============================================================================
// ResearchTrackRowView.cs
// 연구 패널의 "트랙 한 줄(행)" 을 그리고 구동하는 뷰 컴포넌트.
//
// 한 행 = (그룹 × 스탯) 하나 또는 자연회복(Regen) 트랙 하나.
//   예) "근접 공격력", "불 방어력", "자연회복" 등.
//
// 이 컴포넌트는 데이터 판단을 절대 스스로 하지 않는다. 모든 상태(레벨/비용/시간/진행/최대)는
// ResearchPanelUI(로직 코어)의 public 조회 API(Get*/TryGetDisplayProgress/IsResearching)로 읽는다.
// 즉, "표시 전용" 이며 연구 착수 요청도 panel.TryResearch(group, stat) 로 위임한다.
//
// 행의 3가지 표시 상태(동시에 하나만 보임 — CanvasGroup alpha 로 전환):
//   ① 일반(Normal)  : 다음 레벨 비용 + 연구 시간 + [연구] 버튼. (연구 가능 상태)
//   ② 진행(Progress): 진행 바(fillAmount) + 남은 시간 카운트다운. 버튼/비용은 숨김(규칙 9 잠금).
//   ③ 최대(Max)     : "MAX" 표시. 더 연구할 수 없음(Lv5).
//
// 갱신 방식:
//   - 상태 전환/값 갱신 → 드라이버(ResearchTrackListView)가 RefreshRequested 시 Refresh() 호출.
//   - 진행 바의 매 프레임 부드러운 카운트다운 → 이 컴포넌트의 Update() 가 직접 폴링.
//
// Presentation 레이어 — MonoBehaviour. Domain(UpgradeGroup/UnitUpgradeStat) 만 참조.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 연구 패널의 트랙 한 행. ResearchPanelUI 의 조회 API 로 상태를 읽어 표시하고,
    /// [연구] 버튼 클릭을 panel.TryResearch 로 위임한다.
    /// </summary>
    public class ResearchTrackRowView : MonoBehaviour
    {
        [Header("Labels")]
        [Tooltip("트랙 이름(예: '근접 공격력', '자연회복').")]
        [SerializeField] private TextMeshProUGUI _nameText;

        [Tooltip("현재 레벨 표시(예: 'Lv 3/5').")]
        [SerializeField] private TextMeshProUGUI _levelText;

        [Header("Normal State (연구 가능)")]
        [Tooltip("비용/시간/버튼을 묶는 CanvasGroup. 연구 가능 상태에서만 alpha=1.")]
        [SerializeField] private CanvasGroup _normalGroup;

        [Tooltip("다음 레벨 비용(골드). 부족 시 색상 강조.")]
        [SerializeField] private TextMeshProUGUI _costText;

        [Tooltip("다음 레벨 연구 시간(초).")]
        [SerializeField] private TextMeshProUGUI _timeText;

        [Tooltip("연구 착수 버튼. onClick → panel.TryResearch(group, stat).")]
        [SerializeField] private Button _researchButton;

        [Header("Progress State (진행 중)")]
        [Tooltip("진행 바/타이머를 묶는 CanvasGroup. 연구 진행 중에만 alpha=1(규칙 9 잠금).")]
        [SerializeField] private CanvasGroup _progressGroup;

        [Tooltip("진행 바(Image Type=Filled). fillAmount = 진행률(0~1).")]
        [SerializeField] private Image _progressFill;

        [Tooltip("남은 시간 카운트다운 텍스트(예: '12s').")]
        [SerializeField] private TextMeshProUGUI _progressText;

        [Header("Max State (최대 레벨)")]
        [Tooltip("'MAX' 표시를 묶는 CanvasGroup. Lv5 도달 시에만 alpha=1.")]
        [SerializeField] private CanvasGroup _maxGroup;

        // 바인딩 대상(그룹×스탯). Bind 로 채워진다.
        private ResearchPanelUI _panel;
        private UpgradeGroup _group;
        private UnitUpgradeStat _stat;
        private bool _bound;

        // 진행 바를 매 프레임 갱신할지 여부(진행 상태에서만 true).
        private bool _showingProgress;

        // ====================================================================
        // 바인딩
        // ====================================================================

        /// <summary>
        /// 이 행을 특정 트랙(그룹×스탯)에 바인딩한다. 드라이버(ResearchTrackListView)가 행 생성 직후 1회 호출.
        /// </summary>
        /// <param name="panel">로직 코어. 상태 조회/연구 착수 위임 대상.</param>
        /// <param name="group">강화 그룹(Regen 트랙이면 정규화된 그룹 키).</param>
        /// <param name="stat">강화 스탯(Attack/Defense/MoveSpeed/Regen).</param>
        /// <param name="displayName">행에 표시할 트랙 이름(한국어).</param>
        public void Bind(ResearchPanelUI panel, UpgradeGroup group, UnitUpgradeStat stat, string displayName)
        {
            _panel = panel;
            _group = group;
            _stat = stat;
            _bound = true;

            if (_nameText != null) _nameText.text = displayName;

            // 버튼 이벤트 재바인딩(멱등 — 행 재사용/재생성 대비 기존 리스너 제거 후 추가).
            if (_researchButton != null)
            {
                _researchButton.onClick.RemoveAllListeners();
                _researchButton.onClick.AddListener(OnResearchClicked);
            }

            Refresh();
        }

        // [연구] 버튼 클릭 → 로직 코어에 착수 위임(싱글=UseCase 직접, 멀티=서버 요청은 panel 내부에서 분기).
        private void OnResearchClicked()
        {
            if (!_bound || _panel == null) return;
            _panel.TryResearch(_group, _stat);
            // 성공 시 panel 이 RefreshRequested 를 발화 → 드라이버가 다시 Refresh 하므로 여기서는 추가 처리 불필요.
        }

        // ====================================================================
        // 표시 갱신
        // ====================================================================

        /// <summary>
        /// 현재 트랙 상태를 로직 코어에서 다시 읽어 표시를 갱신한다.
        /// 드라이버가 RefreshRequested(팀 상태·골드·연구 착수 변화) 마다 호출한다.
        /// </summary>
        public void Refresh()
        {
            if (!_bound || _panel == null) return;

            int level = _panel.GetLevel(_group, _stat);
            bool researching = _panel.IsResearching(_group, _stat);
            int cost = _panel.GetNextCost(_group, _stat); // 최대 레벨이면 -1
            bool isMax = cost < 0;

            if (_levelText != null)
                _levelText.text = $"Lv {level}/{UpgradeGroupHelper.MaxLevel}";

            // ── 상태 우선순위: 진행 중 > 최대 > 일반 ──────────────────────────
            if (researching)
            {
                SetGroupVisible(_normalGroup, false);
                SetGroupVisible(_maxGroup, false);
                SetGroupVisible(_progressGroup, true);
                _showingProgress = true;
                UpdateProgressDisplay(); // 전환 즉시 1회 반영(다음 프레임까지 빈 바 방지).
            }
            else if (isMax)
            {
                SetGroupVisible(_normalGroup, false);
                SetGroupVisible(_progressGroup, false);
                SetGroupVisible(_maxGroup, true);
                _showingProgress = false;
            }
            else
            {
                SetGroupVisible(_progressGroup, false);
                SetGroupVisible(_maxGroup, false);
                SetGroupVisible(_normalGroup, true);
                _showingProgress = false;
                RefreshNormalContents(cost);
            }
        }

        // 일반 상태의 비용/시간/버튼 활성 갱신.
        private void RefreshNormalContents(int cost)
        {
            bool affordable = _panel.IsCostAffordable(_group, _stat);

            if (_costText != null)
            {
                _costText.text = cost.ToString();
                // 골드 부족 시 강조 색(공통 UI 규칙 7·14). 충분하면 흰색.
                _costText.color = affordable ? Color.white : _panel.GetInsufficientColor();
            }

            if (_timeText != null)
            {
                float time = _panel.GetNextTime(_group, _stat);
                _timeText.text = time > 0f ? $"{Mathf.CeilToInt(time)}s" : "-";
            }

            // 골드가 모자라면 버튼을 눌러도 착수가 실패하므로 비활성으로 명확히 안내.
            if (_researchButton != null)
                _researchButton.interactable = affordable;
        }

        private void Update()
        {
            // 진행 바는 RefreshRequested 사이에도 부드럽게 흘러야 하므로 매 프레임 폴링.
            if (_showingProgress) UpdateProgressDisplay();
        }

        // 진행 바 fillAmount + 남은 시간 텍스트를 로직 코어의 진행값으로 갱신.
        private void UpdateProgressDisplay()
        {
            if (_panel == null) return;

            if (_panel.TryGetDisplayProgress(_group, _stat, out float remaining, out float total))
            {
                if (_progressFill != null)
                    _progressFill.fillAmount = total > 0f ? Mathf.Clamp01(1f - remaining / total) : 0f;

                if (_progressText != null)
                    _progressText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, remaining))}s";
            }
            else
            {
                // 진행이 끝났는데(레벨 확정) 아직 Refresh 가 안 온 프레임 방어 — 다음 Refresh 가 상태를 정리한다.
                _showingProgress = false;
            }
        }

        // CanvasGroup 가시성(+레이캐스트) 일괄 설정. null 안전.
        private static void SetGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
