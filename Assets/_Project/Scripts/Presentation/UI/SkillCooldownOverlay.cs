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
//   물안개 신전 패널도 사용 버튼 하나에 같은 방식으로 이 오버레이를 쓴다(호출처는 두 곳이다).
//
// 배치(이미 끝나 있다 — 전투 씬 실측):
//   스킬 버튼의 자식으로 컨테이너 오브젝트를 두고, 그 아래에 채움 Image 와 남은 초를 보여 줄 TMP 텍스트를
//   자식으로 둔다. 이 컴포넌트와 CanvasGroup 은 컨테이너에 붙인다(전투 씬의 6자리가 모두 이 구조다).
//   전투 씬에는 이 컴포넌트가 6자리(스킬 슬롯 5 + 물안개 사용 버튼 1) 들어가 있고,
//   세 참조(채움 Image · 남은 초 텍스트 · CanvasGroup)가 6자리 전부 연결돼 있다.
//   새로 한 자리를 더 만들 때의 Image 설정은 그 6자리의 실측값을 따른다 —
//   Image Type=Filled / Fill Method=Radial 360 / Fill Origin=Top / 색은 검정 알파 0.6(플레이스홀더).
//   🔴 Clockwise 체크박스는 6자리 모두 꺼져 있다(종전 주석은 켜라고 적고 있었다).
//      꺼 둔 것은 의도다. Unity 의 Filled 이미지는 시작 지점(Fill Origin)에서 Clockwise 방향으로
//      fillAmount 만큼을 그리는데, 이 컴포넌트는 남은 비율(= 어두운 부분)을 fillAmount 로 쓴다.
//      그래서 체크박스를 꺼 두어야 어두운 부분이 12시부터 시계방향으로 걷힌다(규칙 10).
//      켜면 반대로 반시계 방향으로 걷혀 규칙과 어긋난다.
//      이 체크박스를 뒤집으면 오버레이가 걷히는 회전 방향도 그대로 뒤집히므로,
//      만지기 전에 규칙 문서의 쿨다운 시각 표시 규칙(스윕 방향 항목)을 먼저 읽을 것.
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
        [Tooltip("쿨다운 오버레이의 채움 이미지. Image Type=Filled / Fill Method=Radial 360 / Fill Origin=Top. 걷히는 회전 방향 체크박스는 이 파일 머리말의 배치 항목에 실측값이 적혀 있으니 그대로 맞출 것.")]
        [SerializeField] private Image _fillImage;

        [Header("Text")]
        [Tooltip("남은 시간(초) 숫자 텍스트.")]
        [SerializeField] private TextMeshProUGUI _remainingText;

        [Header("루트")]
        [Tooltip("오버레이 전체를 켜고 끌 CanvasGroup(없으면 이 GameObject를 SetActive로 토글).")]
        [SerializeField] private CanvasGroup _canvasGroup;

        private void Awake()
        {
            // 시작 시 완전히 숨김 + 초기값 초기화.
            //   씬에 남아 있을 수 있는 잔여 표기(예: 오서링 시 넣어 둔 "999")를 지워, 쿨다운이 아닐 때
            //   숫자/채움이 상시 떠 있는 것을 막는다(규칙 10 — 시전 중일 때만 표시).
            if (_remainingText != null) _remainingText.text = string.Empty;
            if (_fillImage != null) _fillImage.fillAmount = 0f;
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

            // radial fill = 남은 비율(1 → 0으로 감소하며, 어두운 부분이 12시부터 시계방향으로 걷힌다).
            // 그 방향은 Inspector 의 Clockwise 체크박스(꺼 둔다)가 정한다 — 머리말 배치 항목 참조.
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
        /// (슬롯 격자는 세로 레이아웃 그룹 안에 가로 레이아웃 그룹 행을 둔 구조이며, 레이아웃 그룹은 비활성 자식을
        ///  배치 계산에서 빼 버리므로 활성 토글 대신 CanvasGroup 방식을 권장한다.)
        /// </summary>
        private void ApplyVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                // 보이는 동안에는 이 그룹이 포인터 판정에서 제외되지 않도록 켜 둔다.
                // 🔴 다만 이 플래그만으로 버튼 클릭이 막히지는 않는다(2026-10-05 전투 씬 실측).
                //    전투 씬에 들어가 있는 오버레이 전부에서 채움 이미지와 남은 초 텍스트의
                //    Raycast Target 이 꺼져 있어, 이 그룹 안에는 포인터가 맞을 그래픽이 하나도 없다.
                //    그래서 탭은 그대로 아래 스킬 버튼까지 전달된다.
                //    쿨다운 중 발동을 실제로 막는 것은 오버레이가 아니라 각 패널의 입력 처리에 있는 쿨다운 가드다.
                //    스킬 슬롯 5자리는 스킬 패널의 포인터 눌림 처리에 있는 가드가 막고 안내 토스트를 띄우며,
                //    물안개 사용 버튼은 물안개 패널의 탭 처리에 있는 가드가 토스트 없이 조용히 반환한다.
                //    이 줄을 「오버레이가 차단한다」로 읽지 말 것.
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
