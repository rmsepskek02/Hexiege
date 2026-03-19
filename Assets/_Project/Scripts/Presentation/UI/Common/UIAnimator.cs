// ============================================================================
// UIAnimator.cs
// UI 요소에 DOTween 애니메이션을 적용하는 정적 유틸리티 클래스.
//
// 역할:
//   - 팝업 등장/퇴장, 슬라이드 인/아웃, 버튼 피드백 등
//     반복적으로 사용되는 UI 애니메이션 패턴을 한 곳에서 관리.
//   - 모든 메서드는 static이므로 인스턴스 없이 호출 가능.
//   - SetUpdate(true) 적용: Time.timeScale=0 환경(게임 종료 팝업 등)에서도 정상 동작.
//
// 사용 예시:
//   UIAnimator.PopupShow(canvasGroup, transform);
//   UIAnimator.ButtonPunch(buttonTransform);
//   UIAnimator.CountTo(goldText, 100, 200);
//
// 주의:
//   - Show 계열 메서드는 gameObject.SetActive(true)를 먼저 호출한 뒤 애니메이션 시작.
//     (비활성 오브젝트에서는 DOTween이 동작하지 않기 때문)
//   - Hide 계열 메서드는 애니메이션 완료 후 OnComplete에서 SetActive(false) 호출.
//   - CanvasGroup.blocksRaycasts: Show 시 true, Hide 완료 시 false.
//     (애니메이션 도중 의도치 않은 입력 차단/허용 제어)
//
// Presentation 레이어 — DOTween 의존.
// ============================================================================

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hexiege.Presentation
{
    /// <summary>
    /// UI 요소에 DOTween 애니메이션을 적용하는 정적 유틸리티 클래스.
    /// 팝업, 슬라이드, 버튼 피드백, 텍스트 효과 등 공통 UI 애니메이션 패턴을 제공.
    /// </summary>
    public static class UIAnimator
    {
        // ====================================================================
        // 팝업 등장/퇴장
        // ====================================================================

        /// <summary>
        /// 팝업 등장 애니메이션.
        /// CanvasGroup 알파를 0→1로 페이드인하면서 스케일을 0.85→1로 확대.
        /// Ease.OutBack 으로 약간 튀어나오는 느낌의 바운스 효과 적용.
        /// gameObject를 SetActive(true)로 활성화한 뒤 애니메이션을 시작함.
        /// </summary>
        /// <param name="cg">알파 애니메이션 대상 CanvasGroup. 투명도와 레이캐스트 제어.</param>
        /// <param name="t">스케일 애니메이션 대상 Transform.</param>
        /// <param name="duration">애니메이션 지속 시간 (초). 기본 0.2초.</param>
        /// <returns>생성된 Sequence. 호출부에서 Kill()로 중단 가능.</returns>
        public static Sequence PopupShow(CanvasGroup cg, Transform t, float duration = 0.2f)
        {
            // 비활성 오브젝트에서는 DOTween이 동작하지 않으므로 먼저 활성화
            cg.gameObject.SetActive(true);

            // 시작 상태 초기화: 완전 투명 + 약간 축소된 상태에서 시작
            cg.alpha = 0f;
            cg.blocksRaycasts = true; // 등장 시 즉시 입력 수용
            t.localScale = Vector3.one * 0.85f;

            // Sequence: 페이드인 + 스케일업을 동시에 실행
            var seq = DOTween.Sequence();
            seq.Join(cg.DOFade(1f, duration));
            seq.Join(t.DOScale(1f, duration).SetEase(Ease.OutBack));
            seq.SetUpdate(true); // timeScale=0에서도 동작
            return seq;
        }

        /// <summary>
        /// 팝업 퇴장 애니메이션.
        /// CanvasGroup 알파를 1→0으로 페이드아웃하면서 스케일을 1→0.85로 축소.
        /// 애니메이션 완료 후 SetActive(false) 호출 + onComplete 콜백 실행.
        /// </summary>
        /// <param name="cg">알파 애니메이션 대상 CanvasGroup.</param>
        /// <param name="t">스케일 애니메이션 대상 Transform.</param>
        /// <param name="onComplete">애니메이션 완료 후 실행할 콜백. null 허용.</param>
        /// <param name="duration">애니메이션 지속 시간 (초). 기본 0.15초.</param>
        /// <returns>생성된 Sequence. 호출부에서 Kill()로 중단 가능.</returns>
        public static Sequence PopupHide(CanvasGroup cg, Transform t, System.Action onComplete = null, float duration = 0.15f)
        {
            var seq = DOTween.Sequence();
            seq.Join(cg.DOFade(0f, duration));
            seq.Join(t.DOScale(0.85f, duration).SetEase(Ease.InQuad));
            seq.SetUpdate(true); // timeScale=0에서도 동작
            seq.OnComplete(() =>
            {
                // 애니메이션 완료 후 오브젝트 비활성화 + 레이캐스트 차단
                cg.blocksRaycasts = false;
                cg.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
            return seq;
        }

        // ====================================================================
        // 슬라이드 등장/퇴장
        // ====================================================================

        /// <summary>
        /// 화면 하단에서 위로 슬라이드하며 등장하는 애니메이션.
        /// anchoredPosition.y를 -offsetY에서 0으로 이동 + CanvasGroup 페이드인.
        /// 하단 패널, 토스트 메시지 등에 적합.
        /// </summary>
        /// <param name="rt">이동 대상 RectTransform. anchoredPosition.y를 조작.</param>
        /// <param name="cg">알파 애니메이션 대상 CanvasGroup.</param>
        /// <param name="offsetY">시작 위치 오프셋(양수). 기본 300px 아래에서 시작.</param>
        /// <param name="duration">애니메이션 지속 시간 (초). 기본 0.25초.</param>
        /// <returns>생성된 Sequence.</returns>
        public static Sequence SlideInFromBottom(RectTransform rt, CanvasGroup cg, float offsetY = 300f, float duration = 0.25f)
        {
            // 비활성 오브젝트에서는 DOTween이 동작하지 않으므로 먼저 활성화
            cg.gameObject.SetActive(true);

            // 시작 상태: 화면 아래 + 완전 투명
            var pos = rt.anchoredPosition;
            pos.y = -offsetY;
            rt.anchoredPosition = pos;
            cg.alpha = 0f;
            cg.blocksRaycasts = true;

            // Sequence: 위로 슬라이드 + 페이드인 동시 실행
            var seq = DOTween.Sequence();
            seq.Join(rt.DOAnchorPosY(0f, duration).SetEase(Ease.OutCubic));
            seq.Join(cg.DOFade(1f, duration));
            seq.SetUpdate(true);
            return seq;
        }

        /// <summary>
        /// 화면 하단으로 슬라이드하며 퇴장하는 애니메이션.
        /// anchoredPosition.y를 0에서 -offsetY로 이동 + CanvasGroup 페이드아웃.
        /// 완료 후 SetActive(false) + onComplete 콜백.
        /// </summary>
        /// <param name="rt">이동 대상 RectTransform.</param>
        /// <param name="cg">알파 애니메이션 대상 CanvasGroup.</param>
        /// <param name="onComplete">완료 콜백. null 허용.</param>
        /// <param name="offsetY">퇴장 위치 오프셋. 기본 300px 아래로 이동.</param>
        /// <param name="duration">애니메이션 지속 시간 (초). 기본 0.2초.</param>
        /// <returns>생성된 Sequence.</returns>
        public static Sequence SlideOutToBottom(RectTransform rt, CanvasGroup cg, System.Action onComplete = null, float offsetY = 300f, float duration = 0.2f)
        {
            var seq = DOTween.Sequence();
            seq.Join(rt.DOAnchorPosY(-offsetY, duration).SetEase(Ease.InCubic));
            seq.Join(cg.DOFade(0f, duration));
            seq.SetUpdate(true);
            seq.OnComplete(() =>
            {
                cg.blocksRaycasts = false;
                cg.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
            return seq;
        }

        // ====================================================================
        // 슬라이드 등장/퇴장 (상단)
        // ====================================================================

        /// <summary>
        /// 화면 상단에서 아래로 슬라이드하며 등장하는 애니메이션.
        /// anchoredPosition.y를 +offsetY에서 0으로 이동 + CanvasGroup 페이드인.
        /// 게임 종료 패널, 상단 알림 등 위에서 내려오는 UI에 적합.
        /// SlideInFromBottom의 상단 대칭 버전.
        /// </summary>
        /// <param name="rt">이동 대상 RectTransform. anchoredPosition.y를 조작.</param>
        /// <param name="cg">알파 애니메이션 대상 CanvasGroup.</param>
        /// <param name="offsetY">시작 위치 오프셋(양수). 기본 300px 위에서 시작.</param>
        /// <param name="duration">애니메이션 지속 시간 (초). 기본 0.25초.</param>
        /// <returns>생성된 Sequence.</returns>
        public static Sequence SlideInFromTop(RectTransform rt, CanvasGroup cg, float offsetY = 300f, float duration = 0.25f)
        {
            // 비활성 오브젝트에서는 DOTween이 동작하지 않으므로 먼저 활성화
            cg.gameObject.SetActive(true);

            // 시작 상태: 화면 위 + 완전 투명
            var pos = rt.anchoredPosition;
            pos.y = offsetY;
            rt.anchoredPosition = pos;
            cg.alpha = 0f;
            cg.blocksRaycasts = true;

            // Sequence: 아래로 슬라이드 + 페이드인 동시 실행
            var seq = DOTween.Sequence();
            seq.Join(rt.DOAnchorPosY(0f, duration).SetEase(Ease.OutCubic));
            seq.Join(cg.DOFade(1f, duration));
            seq.SetUpdate(true);
            return seq;
        }

        /// <summary>
        /// 화면 상단으로 슬라이드하며 퇴장하는 애니메이션.
        /// anchoredPosition.y를 0에서 +offsetY로 이동 + CanvasGroup 페이드아웃.
        /// 완료 후 SetActive(false) + onComplete 콜백.
        /// SlideOutToBottom의 상단 대칭 버전.
        /// </summary>
        /// <param name="rt">이동 대상 RectTransform.</param>
        /// <param name="cg">알파 애니메이션 대상 CanvasGroup.</param>
        /// <param name="onComplete">완료 콜백. null 허용.</param>
        /// <param name="offsetY">퇴장 위치 오프셋. 기본 300px 위로 이동.</param>
        /// <param name="duration">애니메이션 지속 시간 (초). 기본 0.2초.</param>
        /// <returns>생성된 Sequence.</returns>
        public static Sequence SlideOutToTop(RectTransform rt, CanvasGroup cg, System.Action onComplete = null, float offsetY = 300f, float duration = 0.2f)
        {
            var seq = DOTween.Sequence();
            seq.Join(rt.DOAnchorPosY(offsetY, duration).SetEase(Ease.InCubic));
            seq.Join(cg.DOFade(0f, duration));
            seq.SetUpdate(true);
            seq.OnComplete(() =>
            {
                cg.blocksRaycasts = false;
                cg.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
            return seq;
        }

        // ====================================================================
        // 버튼 피드백
        // ====================================================================

        /// <summary>
        /// 버튼 클릭 시 "쿵" 하는 스케일 펀치 피드백.
        /// 스케일이 잠깐 줄었다가 원래 크기로 복귀하는 탄성 효과.
        /// 모든 버튼의 onClick에 연결하여 촉감(haptic) 피드백 대용으로 사용.
        /// </summary>
        /// <param name="t">펀치 적용 대상 Transform.</param>
        /// <param name="duration">효과 지속 시간. 기본 0.15초.</param>
        /// <returns>생성된 Tween.</returns>
        public static Tween ButtonPunch(Transform t, float duration = 0.15f)
        {
            // punch 벡터: 약간 안쪽으로 축소 (음수값 = 줄어듦)
            // vibrato=6: 진동 횟수, elasticity=0.5: 탄성 정도
            return t.DOPunchScale(Vector3.one * -0.1f, duration, vibrato: 6, elasticity: 0.5f)
                .SetUpdate(true);
        }

        // ====================================================================
        // 색상/텍스트 효과
        // ====================================================================

        /// <summary>
        /// Graphic(Image, Text 등)의 색상을 부드럽게 전환.
        /// 버튼 상태 변경, 선택/비선택 시각 피드백 등에 사용.
        /// </summary>
        /// <param name="graphic">색상 변경 대상. Image, TMP_Text 등 Graphic 상속 클래스.</param>
        /// <param name="target">목표 색상.</param>
        /// <param name="duration">전환 시간. 기본 0.15초.</param>
        /// <returns>생성된 Tween.</returns>
        public static Tween ColorTransition(Graphic graphic, Color target, float duration = 0.15f)
        {
            return graphic.DOColor(target, duration)
                .SetUpdate(true);
        }

        /// <summary>
        /// 정수 값을 from에서 to로 카운트업/다운하며 텍스트에 표시.
        /// 골드, 점수, 타이머 등 숫자가 변하는 UI에 사용.
        /// 매 프레임 현재 값을 텍스트로 갱신하여 숫자가 올라가는/내려가는 효과 연출.
        /// </summary>
        /// <param name="text">숫자를 표시할 TMP_Text 컴포넌트.</param>
        /// <param name="from">시작 값.</param>
        /// <param name="to">목표 값.</param>
        /// <param name="duration">카운트 시간. 기본 0.4초.</param>
        /// <returns>생성된 Tween.</returns>
        public static Tween CountTo(TMP_Text text, int from, int to, float duration = 0.4f)
        {
            // DOTween.To로 int 값을 보간하며 매 프레임 텍스트 갱신
            int current = from;
            return DOTween.To(() => current, x =>
            {
                current = x;
                text.text = x.ToString();
            }, to, duration)
                .SetUpdate(true);
        }

        /// <summary>
        /// 텍스트 색상을 순간적으로 flashColor로 변경 후 원래 색으로 복귀.
        /// 데미지 표시, 자원 변동 강조 등 시선 유도에 사용.
        /// 전체 duration의 전반부에서 flashColor로, 후반부에서 원래색으로 전환.
        /// </summary>
        /// <param name="text">플래시 대상 TMP_Text 컴포넌트.</param>
        /// <param name="flashColor">강조 색상 (예: 빨강, 노랑 등).</param>
        /// <param name="duration">전체 플래시 시간. 기본 0.3초.</param>
        /// <returns>생성된 Sequence.</returns>
        public static Sequence FlashText(TMP_Text text, Color flashColor, float duration = 0.3f)
        {
            // 현재 색상을 미리 캐시 — 복귀 목표로 사용
            Color originalColor = text.color;
            float halfDuration = duration * 0.5f;

            var seq = DOTween.Sequence();
            // 전반부: 현재색 → flashColor
            seq.Append(text.DOColor(flashColor, halfDuration));
            // 후반부: flashColor → 원래색
            seq.Append(text.DOColor(originalColor, halfDuration));
            seq.SetUpdate(true);
            return seq;
        }

        // ====================================================================
        // 프로그레스/Fill 효과
        // ====================================================================

        /// <summary>
        /// Image의 fillAmount를 부드럽게 목표 값으로 전환.
        /// HP 바, 경험치 바, 생산 진행률 등 Filled 타입 Image에 사용.
        /// Image.type이 Filled로 설정되어 있어야 시각적으로 정상 동작.
        /// </summary>
        /// <param name="fill">fillAmount를 조절할 Image. Image.type = Filled 필수.</param>
        /// <param name="to">목표 fillAmount 값 (0~1 범위).</param>
        /// <param name="duration">전환 시간. 기본 0.25초.</param>
        /// <returns>생성된 Tween.</returns>
        public static Tween FillTo(Image fill, float to, float duration = 0.25f)
        {
            return fill.DOFillAmount(to, duration)
                .SetUpdate(true);
        }
    }
}
