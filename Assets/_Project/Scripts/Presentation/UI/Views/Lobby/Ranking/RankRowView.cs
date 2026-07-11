// ============================================================================
// RankRowView.cs
// 랭킹 테이블의 한 행(플레이어 1명)을 표시하는 MonoBehaviour.
//
// 역할:
//   - LeaderboardEntry 데이터를 받아 순위/닉네임/승률/게임수/승/패 텍스트에 바인딩한다.
//   - 페이지의 남는 행은 Clear() 로 빈 상태로 초기화한다.
//
// 사용처:
//   RankingView 가 이 프리팹을 ScrollRect Content 아래에 10개 인스턴스로 두고,
//   페이지 데이터에 맞춰 Bind()/Clear() 를 반복 호출한다.
//
// 열 구성(GameSystemRules_UI.md Ranking 탭 규칙 1):
//   순위 | 닉네임#코드 | 승률 | 게임수 | 승 | 패
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using TMPro;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 랭킹 테이블 한 행 View.
    /// </summary>
    public class RankRowView : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조 — 열별 텍스트
        // ====================================================================

        [Header("행 텍스트 (좌→우 열 순서)")]
        [Tooltip("순위 텍스트.")]
        [SerializeField] private TextMeshProUGUI _rankText;

        [Tooltip("닉네임#코드 텍스트.")]
        [SerializeField] private TextMeshProUGUI _nicknameText;

        [Tooltip("승률 텍스트(xx.x%).")]
        [SerializeField] private TextMeshProUGUI _winRateText;

        [Tooltip("총 게임 수 텍스트.")]
        [SerializeField] private TextMeshProUGUI _gamesText;

        [Tooltip("승리 횟수 텍스트.")]
        [SerializeField] private TextMeshProUGUI _winsText;

        [Tooltip("패배 횟수 텍스트.")]
        [SerializeField] private TextMeshProUGUI _lossesText;

        // ====================================================================
        // 바인딩 API
        // ====================================================================

        /// <summary>
        /// 랭킹 엔트리 데이터를 각 텍스트에 바인딩하고 행을 표시한다.
        /// </summary>
        /// <param name="entry">표시할 랭킹 1건.</param>
        public void Bind(Hexiege.Application.LeaderboardEntry entry)
        {
            if (entry == null)
            {
                Clear();
                return;
            }

            SetVisible(true);

            SetText(_rankText, entry.Rank.ToString());
            SetText(_nicknameText, entry.DisplayName);
            SetText(_winRateText, $"{entry.WinRate:F1}%");
            SetText(_gamesText, entry.TotalGames.ToString());
            SetText(_winsText, entry.Wins.ToString());
            SetText(_lossesText, entry.Losses.ToString());
        }

        /// <summary>
        /// 행을 빈 상태로 초기화한다(페이지에서 데이터가 없는 남는 행).
        /// 공통 UI 규칙 5: SetActive 대신 CanvasGroup 으로 숨긴다.
        /// </summary>
        public void Clear()
        {
            SetText(_rankText, string.Empty);
            SetText(_nicknameText, string.Empty);
            SetText(_winRateText, string.Empty);
            SetText(_gamesText, string.Empty);
            SetText(_winsText, string.Empty);
            SetText(_lossesText, string.Empty);

            SetVisible(false);
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 행 전체의 표시/숨김을 CanvasGroup 으로 처리한다(공통 UI 규칙 5).
        /// CanvasGroup 이 없으면(에디터에서 미부착) 아무 것도 하지 않는다 — 텍스트만 비워도
        /// 사실상 빈 행으로 보이므로 안전하다.
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (TryGetComponent(out CanvasGroup group))
            {
                group.alpha = visible ? 1f : 0f;
                group.blocksRaycasts = visible;
                group.interactable = visible;
            }
        }

        /// <summary>TMP 텍스트 null-safe 설정.</summary>
        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
