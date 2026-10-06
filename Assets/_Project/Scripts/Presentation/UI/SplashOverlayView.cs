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
//     로그인 X: SplashOverlay 전체 페이드아웃 (FadeOut) → 로그인 화면 노출
//     로그인 O + Lobby 로 바로 감   : FadeOut 없이 즉시 → 로딩 인디케이터 → Lobby 이동
//     로그인 O + Login 씬에 더 머묾 : 페이드아웃한다(닉네임 설정 · 이메일 인증 화면을
//                                     오버레이가 가리면 안 되므로 — 아래 skipFade 설명 참조)
//
// 역할 정리:
//   - SetStatus(text)     : StatusText 문구 변경(예: "로딩 중...")
//   - ShowTapToStart()    : StatusText 숨김 + TapToStartText 깜빡임 시작 + 탭 입력 허용
//   - FadeOut(onComplete) : 오버레이 전체 CanvasGroup 페이드아웃 후 콜백 호출
//   - 화면 탭             : _tapToStartActive 상태일 때만 동작. _skipFadeOnTap에 따라 즉시 콜백 또는 FadeOut 후 콜백
//
// 씬 배치(Login.unity 실측):
//   SplashOverlay Canvas   (이 오버레이 전용 Canvas — SortingOrder 200)
//   └─ SplashOverlay       (CanvasGroup + 이 컴포넌트)
//       ├─ Background          (Image — 전체 화면 배경, Raycast Target 켜져 있음: 탭 입력 수신)
//       └─ SafeAreaContainer
//           ├─ StatusText      (TextMeshProUGUI — 초기화 진행 문구가 들어가는 자리)
//           └─ TapToStartText  (TextMeshProUGUI — 탭 유도 문구, 초기 alpha=0)
//   ⚠️ 두 텍스트는 SplashOverlay 의 직계 자식이 아니라 SafeAreaContainer 아래에 있다.
//   ⚠️ 이 오버레이는 화면 최상위가 아니다 — 전역 로딩 인디케이터 Canvas(SortingOrder 300)가 위에 온다.
//      아래 "로그인 O" 분기가 페이드아웃 없이 넘어가도 배경이 드러나지 않는 근거가 바로 이것이다.
//
// UI 규칙 5(SetActive 금지):
//   텍스트 표시/숨김은 GameObject.SetActive가 아니라 TextMeshProUGUI.alpha(또는 CanvasGroup)로
//   제어한다. 오브젝트는 항상 active 상태를 유지한다.
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
    /// 사용자가 화면을 탭하면 다음 화면으로 넘어간다. 대부분의 분기는 페이드아웃한 뒤 콜백을
    /// 실행하지만(로그인 선택 · 닉네임 설정 · 이메일 인증), Lobby 씬으로 바로 가는 분기만
    /// 페이드아웃 없이 콜백을 즉시 실행한다(파일 머리말의 화면 흐름 참조).
    /// </summary>
    public class SplashOverlayView : MonoBehaviour, IPointerClickHandler
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("UI 참조")]
        [Tooltip("오버레이 전체의 CanvasGroup. 표시/숨김(alpha, blocksRaycasts)과 페이드아웃에 사용한다. " +
                 "SplashOverlay 루트 GameObject에 부착하는 것을 권장.")]
        [SerializeField] private CanvasGroup _overlayCanvasGroup;

        [Tooltip("초기화 진행 상태를 표시하는 텍스트(예: '로딩 중...').")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Tooltip("초기화 완료 후 깜빡이며 표시되는 'Tap to Start' 텍스트. 초기 alpha=0.")]
        [SerializeField] private TextMeshProUGUI _tapToStartText;

        // ====================================================================
        // 상수 — 애니메이션 시간
        // ====================================================================

        /// <summary>
        /// "Tap to Start" 텍스트 깜빡임의 **한 방향** 시간(초).
        /// 아래 ShowTapToStart() 가 Yoyo 반복으로 거는 값이라, 알파가 올라가는 데 이만큼,
        /// 다시 내려가는 데 또 이만큼 걸린다 — 즉 밝아졌다 어두워지는 왕복은 이 값의 두 배다.
        /// </summary>
        private const float BlinkDuration = 0.8f;

        /// <summary> 화면 탭 시 오버레이 전체가 사라지는 페이드아웃 시간(초). </summary>
        private const float FadeOutDuration = 0.5f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 현재 탭 입력을 받을 수 있는 상태인지 여부.
        /// ShowTapToStart() 호출 시 true가 되며, 이 상태에서만 화면 탭이 처리된다.
        /// (탭이 무엇을 하는지는 아래 _skipFadeOnTap 이 가른다 — 페이드아웃하거나 바로 콜백을 부른다.)
        /// (로딩 중 실수 탭으로 화면이 넘어가는 것을 방지)
        /// </summary>
        private bool _tapToStartActive;

        /// <summary>"Tap to Start" 깜빡임 트윈. 페이드아웃/파괴 시 정리(Kill)한다.</summary>
        private Tween _blinkTween;

        /// <summary>
        /// 탭으로 오버레이를 닫은 뒤 실행할 콜백(예: 로그인 선택 화면 표시).
        /// LoginBootstrapper가 ShowTapToStart 호출 전에 SetTapCallback으로 주입한다.
        /// </summary>
        private Action _tapCallback;

        /// <summary>
        /// true이면 탭 시 FadeOut을 건너뛰고 콜백을 즉시 실행한다.
        /// 로그인 O 분기에서 사용 — 로딩 인디케이터(SortingOrder 300)가 화면을 덮으므로
        /// FadeOut이 없어도 Login 씬 배경이 노출되지 않는다.
        /// </summary>
        private bool _skipFadeOnTap;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        /// <summary>
        /// 초기 상태 설정.
        /// 오버레이는 화면을 완전히 덮는 상태(alpha=1, 입력 차단)로 시작하고,
        /// "Tap to Start" 텍스트는 보이지 않도록 alpha=0으로 둔다.
        /// </summary>
        private void Awake()
        {
            if (_overlayCanvasGroup != null)
            {
                _overlayCanvasGroup.alpha = 1f;
                _overlayCanvasGroup.blocksRaycasts = true;
                _overlayCanvasGroup.interactable = true;
            }

            if (_tapToStartText != null)
                _tapToStartText.alpha = 0f;
        }

        /// <summary>
        /// 파괴 시 진행 중인 모든 트윈을 정리해 누수/콜백 오류를 방지한다.
        /// </summary>
        private void OnDestroy()
        {
            _blinkTween?.Kill();
            _blinkTween = null;

            // 오버레이 CanvasGroup에 걸린 페이드 트윈도 함께 정리.
            if (_overlayCanvasGroup != null)
                _overlayCanvasGroup.DOKill();
        }

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
            if (_statusText != null)
                _statusText.text = text;
        }

        /// <summary>
        /// 초기화 완료 후 호출. 상태 문구를 숨기고 "Tap to Start" 텍스트를
        /// alpha 0↔1로 무한 반복 깜빡이게 하며, 화면 탭 입력을 허용한다.
        /// ⚠️ 자동 로그인이 실패한 경우만이 아니다 — 부트스트래퍼는 자동 로그인이 성공했을 때도,
        ///   이메일 인증이 남았을 때도 이 메서드를 거친다(세 분기 전부가 탭 대기를 지난다).
        ///   달라지는 것은 탭 뒤에 실행될 콜백과 페이드아웃 여부뿐이다.
        /// </summary>
        public void ShowTapToStart()
        {
            // 이제부터 화면 탭으로 오버레이를 닫을 수 있다.
            _tapToStartActive = true;

            // 상태 문구는 더 이상 필요 없으므로 alpha=0으로 숨긴다(SetActive 미사용 — 규칙 5).
            if (_statusText != null)
                _statusText.alpha = 0f;

            if (_tapToStartText == null) return;

            // 깜빡임 시작: 알파를 0에서 시작해 1까지 올렸다 내렸다(Yoyo) 무한 반복.
            //   DOTween.To(getter, setter, 목표값, 시간) : 매 프레임 알파를 setter로 갱신.
            //   SetLoops(-1, LoopType.Yoyo) : -1 = 무한, Yoyo = 갔다가 되돌아오기.
            _blinkTween?.Kill();
            _tapToStartText.alpha = 0f;
            _blinkTween = DOTween.To(
                    () => _tapToStartText.alpha,
                    x => _tapToStartText.alpha = x,
                    1f,
                    BlinkDuration)
                .SetLoops(-1, LoopType.Yoyo);
        }

        /// <summary>
        /// 오버레이 전체를 페이드아웃한 뒤 콜백을 호출한다.
        /// 🔴 호출처는 이 파일의 탭 핸들러(OnPointerClick) 한 곳뿐이다 — 바깥에서 부르는 곳은 없다.
        ///   public 으로 열려 있기는 하지만, 부트스트래퍼는 탭 콜백을 주입하는 쪽만 사용한다.
        /// </summary>
        /// <param name="onComplete">페이드아웃 완료 후 실행할 콜백(null 허용).</param>
        public void FadeOut(Action onComplete)
        {
            // 깜빡임 트윈 정리 — 페이드 도중 텍스트 알파가 다시 올라가는 것을 방지.
            _blinkTween?.Kill();
            _blinkTween = null;

            // 추가 탭으로 FadeOut이 중복 실행되는 것을 막는다.
            _tapToStartActive = false;

            // CanvasGroup이 없으면 페이드 없이 즉시 완료 처리(흐름이 멈추지 않도록).
            if (_overlayCanvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            // alpha를 0까지 낮춘 뒤 입력 차단을 해제하고 완료 콜백을 호출한다.
            _overlayCanvasGroup
                .DOFade(0f, FadeOutDuration)
                .OnComplete(() =>
                {
                    _overlayCanvasGroup.blocksRaycasts = false;
                    _overlayCanvasGroup.interactable = false;
                    onComplete?.Invoke();
                });
        }

        /// <summary>
        /// 탭으로 오버레이를 닫은 후 실행할 콜백을 설정한다.
        /// LoginBootstrapper가 ShowTapToStart 호출 전에 연결한다.
        /// </summary>
        /// <param name="callback">탭 후 실행될 콜백.</param>
        /// <param name="skipFade">
        /// true이면 FadeOut 없이 콜백을 즉시 호출한다 — 탭 직후 Lobby 씬으로 넘어가는 분기 한 곳뿐이다.
        /// false(기본값)이면 FadeOut 후 콜백 호출. 자동 로그인 실패 분기뿐 아니라
        /// "로그인은 됐지만 Login 씬 안에서 다음 화면(닉네임 설정 · 이메일 인증)을 보여주는" 분기도 이쪽이다
        /// (오버레이가 남아 있으면 그 화면을 가리고 raycast 도 계속 막기 때문).
        /// </param>
        public void SetTapCallback(Action callback, bool skipFade = false)
        {
            _tapCallback = callback;
            _skipFadeOnTap = skipFade;
        }

        // ====================================================================
        // 입력 처리
        // ====================================================================

        /// <summary>
        /// 화면 탭 핸들러. _tapToStartActive 상태일 때만 동작한다.
        /// 그 안에서 _skipFadeOnTap 이 켜져 있으면 FadeOut 없이 콜백을 즉시 실행하고,
        /// 꺼져 있으면 FadeOut 뒤에 콜백을 실행한다 — 즉 탭이 항상 FadeOut 으로 가지는 않는다.
        /// (Background Image의 Raycast Target이 켜져 있고 EventSystem이 있어야 호출된다.)
        /// </summary>
        /// <param name="eventData">포인터 이벤트 데이터(미사용).</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_tapToStartActive) return;

            if (_skipFadeOnTap)
            {
                // FadeOut 없이 즉시 콜백 실행.
                // 로딩 인디케이터(SortingOrder 300)가 화면을 덮으므로 배경 노출 없음.
                _tapToStartActive = false;
                _blinkTween?.Kill();
                _blinkTween = null;
                _tapCallback?.Invoke();
            }
            else
            {
                FadeOut(_tapCallback);
            }
        }
    }
}
