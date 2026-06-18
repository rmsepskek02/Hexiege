// ============================================================================
// SplashOverlayView.cs
// Login 씬 진입 직후 표시되는 스플래시(초기화/인트로) 오버레이 UI.
//
// 화면 흐름:
//   앱 실행 → Login 씬 로드
//     ┌───────────────────────────┐
//     │      [배경 이미지]         │
//     │       "로딩 중..."         │  ← 초기화 진행 중 (SetStatus)
//     └───────────────────────────┘
//         ↓ 초기화(AudioManager + Firebase + UIManager) 완료
//     ┌───────────────────────────┐
//     │      [배경 이미지]         │
//     │     "Tap to Start"         │  ← alpha 0↔1 깜빡임 (ShowTapToStart)
//     └───────────────────────────┘
//         ↓ 화면 탭
//     SplashOverlay 전체 페이드아웃 (FadeOut) → 로그인 화면 노출 또는 Lobby 이동
//
// 역할 정리:
//   - SetStatus(text)     : StatusText 문구 변경(예: "로딩 중...")
//   - ShowTapToStart()    : StatusText 숨김 + TapToStartText 깜빡임 시작 + 탭 입력 허용
//   - FadeOut(onComplete) : 오버레이 전체 CanvasGroup 페이드아웃 후 콜백 호출
//   - 화면 탭             : ShowTapToStart 상태일 때만 1회 FadeOut을 트리거
//
// 씬 배치(Login.unity):
//   Canvas
//   └─ SplashOverlay (CanvasGroup + 이 컴포넌트, 최상위 표시)
//       ├─ Background     (Image — 전체 화면 배경, Raycast Target=true 권장: 탭 입력 수신)
//       ├─ StatusText     (TextMeshProUGUI — "로딩 중...")
//       └─ TapToStartText (TextMeshProUGUI — "Tap to Start", 초기 alpha=0)
//
// 탭 입력 주의:
//   IPointerClickHandler가 동작하려면 클릭을 받을 UI(보통 Background Image)의
//   Raycast Target이 켜져 있어야 하고, 씬에 EventSystem이 존재해야 한다.
//
// Presentation 레이어 — MonoBehaviour + DOTween + TMP 의존.
// ============================================================================

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hexiege.Presentation
{
    /// <summary>
    /// Login 씬 진입 시 표시되는 스플래시 오버레이.
    /// 초기화 중에는 상태 문구를, 완료 후에는 "Tap to Start" 깜빡임을 보여주고,
    /// 사용자가 화면을 탭하면 페이드아웃하며 로그인 화면으로 넘어간다.
    /// </summary>
    public class SplashOverlayView : MonoBehaviour, IPointerClickHandler
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("UI 참조")]
        [Tooltip("초기화 진행 상태를 표시하는 텍스트(예: '로딩 중...').")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Tooltip("초기화 완료 후 깜빡이며 표시되는 'Tap to Start' 텍스트. 초기 alpha=0 권장.")]
        [SerializeField] private TextMeshProUGUI _tapToStartText;

        [Tooltip("오버레이 전체의 CanvasGroup. 페이드아웃에 사용한다. " +
                 "SplashOverlay 루트 GameObject에 부착하는 것을 권장.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("애니메이션 설정")]
        [Tooltip("'Tap to Start' 텍스트의 깜빡임 1회(밝아졌다 어두워지는) 시간(초).")]
        [SerializeField, Min(0f)] private float _blinkDuration = 0.8f;

        [Tooltip("화면 탭 시 오버레이 전체가 사라지는 페이드아웃 시간(초).")]
        [SerializeField, Min(0f)] private float _fadeOutDuration = 0.5f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 현재 "Tap to Start" 상태(탭 입력을 받을 수 있는 상태)인지 여부.
        /// ShowTapToStart() 호출 시 true가 되며, 탭으로 FadeOut을 1회 트리거한 뒤 다시 false.
        /// </summary>
        private bool _canTap;

        /// <summary>이미 FadeOut이 시작되었는지 여부. 탭 중복 입력으로 페이드가 겹치는 것을 방지.</summary>
        private bool _fadingOut;

        /// <summary>"Tap to Start" 깜빡임 트윈. 페이드아웃 시 정리(Kill)한다.</summary>
        private Tween _blinkTween;

        // ====================================================================
        // 공개 메서드
        // ====================================================================

        /// <summary>
        /// 상태 문구를 변경한다(예: "로딩 중...").
        /// 초기화 진행 단계에서 호출한다.
        /// </summary>
        /// <param name="text">표시할 상태 문구.</param>
        public void SetStatus(string text)
        {
            if (_statusText == null) return;

            // 상태 텍스트를 다시 보이게 하고 문구를 갱신한다.
            _statusText.gameObject.SetActive(true);
            _statusText.text = text;
        }

        /// <summary>
        /// 초기화 완료 후 호출. 상태 문구를 숨기고 "Tap to Start" 텍스트를
        /// alpha 0↔1로 무한 반복 깜빡이게 하며, 화면 탭 입력을 허용한다.
        /// </summary>
        public void ShowTapToStart()
        {
            // 상태 문구는 더 이상 필요 없으므로 숨긴다.
            if (_statusText != null)
                _statusText.gameObject.SetActive(false);

            // 탭 입력 허용 — 이 시점부터 화면을 탭하면 FadeOut이 트리거된다.
            _canTap = true;

            if (_tapToStartText == null) return;

            // 깜빡임 시작: 알파를 0에서 시작해 1까지 올렸다 내렸다(Yoyo) 무한 반복.
            //   SetLoops(-1, LoopType.Yoyo) : -1 = 무한, Yoyo = 갔다가 되돌아오기.
            //   SetEase(Ease.InOutSine)     : 부드럽게 밝아지고 어두워지는 곡선.
            _tapToStartText.alpha = 0f;
            _blinkTween?.Kill();
            _blinkTween = _tapToStartText
                .DOFade(1f, _blinkDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true); // 타임스케일 영향 없이 항상 동작.
        }

        /// <summary>
        /// 오버레이 전체를 페이드아웃한 뒤 콜백을 호출한다.
        /// 외부(LoginBootstrapper)에서 자동 로그인 성공 등으로 직접 호출하거나,
        /// 사용자 탭(OnPointerClick)을 통해 호출된다. 중복 호출은 무시한다.
        /// </summary>
        /// <param name="onComplete">페이드아웃 완료 후 실행할 콜백(null 허용).</param>
        public void FadeOut(Action onComplete = null)
        {
            // 이미 페이드아웃이 진행 중이거나 끝났으면 중복 실행하지 않는다.
            if (_fadingOut) return;
            _fadingOut = true;
            _canTap = false;

            // 깜빡임 트윈 정리 — 페이드 도중 텍스트 알파가 다시 올라가는 것을 방지.
            _blinkTween?.Kill();
            _blinkTween = null;

            // CanvasGroup이 없으면 페이드 없이 즉시 완료 처리(흐름이 멈추지 않도록).
            if (_canvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            // 페이드 도중에는 입력을 막는다(중간 탭으로 인한 부작용 방지).
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            _canvasGroup
                .DOFade(0f, _fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke());
        }

        // ====================================================================
        // 입력 처리
        // ====================================================================

        /// <summary>
        /// 화면 탭 핸들러. "Tap to Start" 상태일 때만 FadeOut을 트리거한다.
        /// (Background Image의 Raycast Target이 켜져 있고 EventSystem이 있어야 호출된다.)
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_canTap) return;
            FadeOut();
        }

        // ====================================================================
        // 라이프사이클
        // ====================================================================

        /// <summary>
        /// 파괴 시 진행 중인 트윈을 정리해 누수/콜백 오류를 방지한다.
        /// </summary>
        private void OnDestroy()
        {
            _blinkTween?.Kill();
            _blinkTween = null;
        }
    }
}
