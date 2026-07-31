// ============================================================================
// SkillCooldownOverlay.cs
// 스킬 버튼 위에 얹는 쿨다운 시각 표시 — 시계방향(radial) fill + 남은 시간 숫자(규칙 10).
//
// 무엇을 하나(초급자용 설명):
//   건물 글로벌 쿨다운 중에는 스킬 버튼 위에 반투명 오버레이가 시계방향으로 줄어들며,
//   그 위에 남은 시간(초) 숫자가 표시된다. 아트는 플레이스홀더(단색 반투명 Image)이며,
//   이 스크립트는 fillAmount·텍스트 갱신 로직만 담당한다.
//
// 사용:
//   스킬 패널(BuildingSkillPanelUI)이 패널이 열려 있는 동안 매 프레임 SetCooldown(remaining, total)을
//   호출한다. remaining<=0이면 자동으로 숨긴다.
//
// 배치(사용자 Unity 작업):
//   스킬 버튼 위에 자식으로 Image를 두고 Image Type=Filled / Fill Method=Radial 360 /
//   Fill Origin=Top / Clockwise 체크. 그 Image를 _fillImage에, 남은 초 TMP 텍스트를 _remainingText에 연결.
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 스킬 버튼 쿨다운 오버레이(플레이스홀더). radial fill 비율과 남은 초 숫자를 갱신한다.
    /// </summary>
    public sealed class SkillCooldownOverlay : MonoBehaviour
    {
        [Header("Fill")]
        [Tooltip("시계방향 radial fill 이미지. Image Type=Filled / Radial360 / Clockwise 설정 필요.")]
        [SerializeField] private Image _fillImage;

        [Header("Text")]
        [Tooltip("남은 시간(초) 숫자 텍스트.")]
        [SerializeField] private TextMeshProUGUI _remainingText;

        [Header("루트")]
        [Tooltip("오버레이 전체를 켜고 끌 CanvasGroup(없으면 이 GameObject를 SetActive로 토글).")]
        [SerializeField] private CanvasGroup _canvasGroup;

        private void Awake()
        {
            // 시작 시 숨김.
            ApplyVisible(false);
        }

        /// <summary>
        /// 쿨다운 표시를 갱신한다. remaining이 0 이하이면 오버레이를 숨긴다.
        /// </summary>
        /// <param name="remaining">남은 쿨다운(초).</param>
        /// <param name="total">발동 시점의 총 쿨다운(초). radial fill 비율 계산에 사용.</param>
        public void SetCooldown(float remaining, float total)
        {
            if (remaining <= 0f || total <= 0f)
            {
                ApplyVisible(false);
                return;
            }

            ApplyVisible(true);

            // radial fill = 남은 비율(1 → 0으로 시계방향 감소). Clockwise는 Inspector 설정.
            if (_fillImage != null)
                _fillImage.fillAmount = Mathf.Clamp01(remaining / total);

            // 남은 초 = 올림(0.1초 남아도 "1"로 보이도록). 규칙 10 — 숫자 표기.
            if (_remainingText != null)
                _remainingText.text = Mathf.CeilToInt(remaining).ToString();
        }

        /// <summary>
        /// 오버레이를 즉시 숨긴다(쿨다운 없음/패널 닫힘).
        /// </summary>
        public void Hide()
        {
            ApplyVisible(false);
        }

        /// <summary>
        /// CanvasGroup이 있으면 alpha/raycast로, 없으면 GameObject 활성으로 표시 토글.
        /// (GridLayout 레이아웃 보존을 위해 CanvasGroup 방식을 권장한다.)
        /// </summary>
        private void ApplyVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                // 쿨다운 오버레이는 버튼 클릭을 막아야 하므로 보일 때 raycast를 켠다(쿨다운 중 발동 차단).
                _canvasGroup.blocksRaycasts = visible;
                _canvasGroup.interactable = visible;
            }
            else
            {
                if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            }
        }
    }
}
