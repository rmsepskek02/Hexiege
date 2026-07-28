// ============================================================================
// ResearchCellView.cs
// 연구 패널 매트릭스 레이어의 "칸(셀) 하나"를 그리고 구동하는 뷰 컴포넌트.
//
// 한 칸 = (그룹 × 스탯) 하나 또는 자연회복(Regen) 트랙 하나.
//   예) 행="근접", 열="공격력" → 셀 하나 = 인간 근접 공격력 트랙.
//
// 이 컴포넌트는 데이터 판단을 스스로 하지 않는다. 모든 상태(레벨/비용/진행/최대)는
// ResearchPanelUI(로직 코어)의 public 조회 API로 읽고, 연구 착수는 panel.TryResearch로 위임한다.
//
// 셀 표시 요소:
//   - 레벨 텍스트: "Lv X/5"
//   - 눈금(핍) 5칸: 현재 레벨만큼 채워 표시(0~5).
//   - 상태 텍스트: 일반=다음 비용(골드, 부족 시 색 강조) / 진행 중="연구중" / 최대="MAX".
//   - 셀 전체가 버튼: 일반+골드충분에서만 interactable=true. (진행/최대/부족은 비활성.)
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
    /// 연구 매트릭스의 한 칸. ResearchPanelUI 조회 API로 상태를 읽어 표시하고,
    /// 클릭을 panel.TryResearch(group, stat)로 위임한다.
    /// </summary>
    public class ResearchCellView : MonoBehaviour
    {
        [Header("Labels")]
        [Tooltip("현재 레벨 표시(예: 'Lv 3/5').")]
        [SerializeField] private TextMeshProUGUI _levelText;

        [Tooltip("상태 텍스트: 일반=다음 비용(골드) / 진행 중='연구중' / 최대='MAX'.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Pips (레벨 눈금)")]
        [Tooltip("레벨 눈금 이미지 5칸. 현재 레벨만큼 앞에서부터 채워 표시한다.")]
        [SerializeField] private Image[] _pips;

        [Tooltip("채워진 눈금 색상.")]
        [SerializeField] private Color _pipFilledColor = new Color(0.35f, 0.75f, 1f, 1f);

        [Tooltip("비어 있는 눈금 색상.")]
        [SerializeField] private Color _pipEmptyColor = new Color(1f, 1f, 1f, 0.18f);

        [Header("Button")]
        [Tooltip("셀 전체 버튼. onClick → panel.TryResearch(group, stat).")]
        [SerializeField] private Button _button;

        // 바인딩 대상(그룹×스탯). Bind로 채워진다.
        private ResearchPanelUI _panel;
        private UpgradeGroup _group;
        private UnitUpgradeStat _stat;
        private bool _bound;

        // ====================================================================
        // 바인딩
        // ====================================================================

        /// <summary>
        /// 이 셀을 특정 트랙(그룹×스탯)에 바인딩한다. 매트릭스 뷰가 셀 생성 직후 1회 호출.
        /// </summary>
        /// <param name="panel">로직 코어. 상태 조회/연구 착수 위임 대상.</param>
        /// <param name="group">강화 그룹(Regen 셀이면 정규화된 그룹 키).</param>
        /// <param name="stat">강화 스탯(Attack/Defense/MoveSpeed/Regen).</param>
        public void Bind(ResearchPanelUI panel, UpgradeGroup group, UnitUpgradeStat stat)
        {
            _panel = panel;
            _group = group;
            _stat = stat;
            _bound = true;

            // 버튼 이벤트 재바인딩(멱등 — 셀 재사용/재생성 대비 기존 리스너 제거 후 추가).
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(OnClicked);
            }

            Refresh();
        }

        // 셀 클릭 → 로직 코어에 착수 위임(싱글=UseCase 직접, 멀티=서버 요청은 panel 내부에서 분기).
        private void OnClicked()
        {
            if (!_bound || _panel == null) return;
            _panel.TryResearch(_group, _stat);
            // 성공 시 panel이 RefreshRequested를 발화 → 매트릭스 뷰가 다시 Refresh 하므로 추가 처리 불필요.
        }

        // ====================================================================
        // 표시 갱신
        // ====================================================================

        /// <summary>
        /// 현재 트랙 상태를 로직 코어에서 다시 읽어 표시를 갱신한다.
        /// 매트릭스 뷰가 RefreshRequested(팀 상태·골드·연구 착수 변화)마다 호출한다.
        /// </summary>
        public void Refresh()
        {
            if (!_bound || _panel == null) return;

            int level = _panel.GetLevel(_group, _stat);
            bool researching = _panel.IsResearching(_group, _stat);
            int cost = _panel.GetNextCost(_group, _stat); // 최대 레벨이면 -1
            bool isMax = cost < 0;

            // ── 레벨 텍스트 + 눈금 ──
            if (_levelText != null)
                _levelText.text = $"Lv {level}/{UpgradeGroupHelper.MaxLevel}";
            UpdatePips(level);

            // ── 상태 텍스트 + 버튼 활성(우선순위: 진행 중 > 최대 > 일반) ──
            if (researching)
            {
                SetStatus("연구중", _panel.GetNormalColor());
                SetInteractable(false);
            }
            else if (isMax)
            {
                SetStatus("MAX", new Color(1f, 0.85f, 0.3f, 1f));
                SetInteractable(false);
            }
            else
            {
                bool affordable = _panel.IsCostAffordable(_group, _stat);
                SetStatus(cost.ToString(), affordable ? _panel.GetNormalColor() : _panel.GetInsufficientColor());
                // 골드가 모자라면 눌러도 착수가 실패하므로 비활성으로 명확히 안내.
                SetInteractable(affordable);
            }
        }

        // 현재 레벨만큼 앞에서부터 눈금을 채운다.
        private void UpdatePips(int level)
        {
            if (_pips == null) return;
            for (int i = 0; i < _pips.Length; i++)
            {
                if (_pips[i] == null) continue;
                _pips[i].color = (i < level) ? _pipFilledColor : _pipEmptyColor;
            }
        }

        private void SetStatus(string text, Color color)
        {
            if (_statusText == null) return;
            _statusText.text = text;
            _statusText.color = color;
        }

        private void SetInteractable(bool value)
        {
            if (_button != null) _button.interactable = value;
        }
    }
}
