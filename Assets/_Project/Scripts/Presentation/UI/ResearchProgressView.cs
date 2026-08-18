// ============================================================================
// ResearchProgressView.cs
// 연구 패널 "진행 레이어"를 그리고 구동하는 뷰 컴포넌트.
//
// 해당 연구소가 연구 중일 때만(패널이 진행 레이어를 표시할 때) 의미가 있다.
//   표시: [트랙 이름] + [Lv X → X+1] + [게이지 바] + [남은 시간] + [취소 버튼].
//   생산 큐 같은 1/2/3 칸은 없다(연구소 단위 단일 진행).
//   취소 = 연구만 취소(투입 골드 환불) → 패널이 매트릭스 레이어로 복귀.
//
// 이 컴포넌트는 데이터 판단을 스스로 하지 않는다. 진행 대상/값은 모두 ResearchPanelUI의
// 조회 API(TryGetCurrentLabResearch/GetLevel/TryGetDisplayProgress)로 읽고,
// 취소는 panel.TryCancelCurrentLabResearch()로 위임한다.
//
// 갱신 방식:
//   - 트랙/레벨 등 정적 값 → 드라이버(패널)가 RefreshRequested 시 Refresh() 호출.
//   - 게이지/남은 시간의 매 프레임 부드러운 카운트다운 → 이 컴포넌트의 Update()가 직접 폴링.
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
    /// 연구 진행 레이어. 현재 연구소가 연구 중인 트랙의 진행 상태를 표시하고 [취소]를 위임한다.
    /// </summary>
    public class ResearchProgressView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("연구 로직 코어. 진행 대상/값 조회 + 취소 위임 + 갱신 알림(RefreshRequested)의 원천.")]
        [SerializeField] private ResearchPanelUI _panel;

        [Header("Labels")]
        [Tooltip("진행 중 트랙 이름(예: '근접 공격력', '자연회복').")]
        [SerializeField] private TextMeshProUGUI _nameText;

        [Tooltip("레벨 전이 표시(예: 'Lv 2 → 3').")]
        [SerializeField] private TextMeshProUGUI _levelText;

        [Header("Progress")]
        [Tooltip("게이지 바(Image Type=Filled). fillAmount = 진행률(0~1).")]
        [SerializeField] private Image _progressFill;

        [Tooltip("남은 시간 카운트다운 텍스트(예: '12s').")]
        [SerializeField] private TextMeshProUGUI _timeText;

        [Header("Cancel")]
        [Tooltip("연구 취소 버튼. onClick → panel.TryCancelCurrentLabResearch(). 건물은 유지, 골드 환불.")]
        [SerializeField] private Button _cancelButton;

        // 현재 표시 중인 진행 트랙(패널에서 조회한 값 캐시).
        private bool _hasTrack;
        private UpgradeGroup _group;
        private UnitUpgradeStat _stat;

        // ====================================================================
        // 생명주기
        // ====================================================================

        private bool _subscribed;

        private void Awake()
        {
            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveAllListeners();
                _cancelButton.onClick.AddListener(OnCancelClicked);
            }
        }

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void OnDisable()
        {
            if (_subscribed && _panel != null)
            {
                _panel.RefreshRequested -= Refresh;
                _subscribed = false;
            }
        }

        private void TrySubscribe()
        {
            if (_subscribed || _panel == null) return;
            _panel.RefreshRequested += Refresh;
            _subscribed = true;
        }

        // ====================================================================
        // 갱신
        // ====================================================================

        /// <summary>
        /// 현재 연구소의 진행 트랙을 다시 조회해 이름/레벨을 갱신한다.
        /// 진행 중이 아니면(=매트릭스 레이어) 표시할 것이 없으므로 캐시만 비운다.
        /// </summary>
        public void Refresh()
        {
            if (_panel == null) return;

            _hasTrack = _panel.TryGetCurrentLabResearch(out _group, out _stat);
            if (!_hasTrack) return;

            if (_nameText != null)
                _nameText.text = ResearchPanelUI.TrackDisplayName(_group, _stat);

            if (_levelText != null)
            {
                int level = _panel.GetLevel(_group, _stat);
                _levelText.text = $"Lv {level} → {level + 1}";
            }

            UpdateProgress(); // 전환 즉시 1회 반영(다음 프레임까지 빈 바 방지).
        }

        private void Update()
        {
            // 게이지/남은 시간은 RefreshRequested 사이에도 부드럽게 흘러야 하므로 매 프레임 폴링.
            if (_hasTrack && _panel != null && _panel.IsPanelOpen) UpdateProgress();
        }

        // 게이지 fillAmount + 남은 시간 텍스트를 로직 코어의 진행값으로 갱신.
        private void UpdateProgress()
        {
            if (_panel == null || !_hasTrack) return;

            if (_panel.TryGetDisplayProgress(_group, _stat, out float remaining, out float total))
            {
                if (_progressFill != null)
                    _progressFill.fillAmount = total > 0f ? Mathf.Clamp01(1f - remaining / total) : 0f;

                if (_timeText != null)
                    _timeText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, remaining))}s";
            }
            else
            {
                // 진행이 끝났는데(레벨 확정) 아직 Refresh가 안 온 프레임 방어 — 다음 Refresh가 상태를 정리한다.
                _hasTrack = false;
            }
        }

        // 취소 버튼 → 로직 코어에 취소 위임(싱글=UseCase 직접, 멀티=서버 요청은 panel 내부에서 분기).
        private void OnCancelClicked()
        {
            if (_panel == null) return;
            _panel.TryCancelCurrentLabResearch();
            // 성공/확정 시 panel이 RefreshRequested를 발화 → 매트릭스로 복귀.
        }
    }
}
