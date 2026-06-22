// ============================================================================
// UIManager.cs
// 전역 공통 UI(확인 팝업 / 로딩 인디케이터)를 단 하나의 인스턴스로 관리하는 매니저.
//
// 왜 필요한가:
//   기존에는 ConfirmPopup과 LoadingIndicator를 Login/Lobby/Game 씬마다 따로 배치했다.
//   → 같은 UI를 여러 번 만들어야 하고, 씬마다 연결이 어긋날 위험이 있었다.
//   UIManager를 Login 씬에서 1회만 생성하고 DontDestroyOnLoad로 유지하면,
//   이후 모든 씬에서 동일한 공통 UI를 재사용할 수 있다.
//
// 동작 방식:
//   - SingletonMonoBehaviour<UIManager>를 상속한다.
//     → Awake()에서 자동으로 Instance 등록 + DontDestroyOnLoad 처리(중복 시 자기 파괴).
//   - 외부(View, Bootstrapper)는 UIManager.Instance를 통해 IUIManager 계약 메서드만 호출.
//
// 씬 배치(Login.unity):
//   [UI Systems]
//   └─ UIManager (이 컴포넌트)
//       └─ UIManager Canvas (SortingOrder 100)
//           └─ SafeAreaContainer (SafeAreaFitter)
//               ├─ ConfirmPopup       ← _confirmPopup 에 연결
//               └─ LoadingIndicator   ← _loadingIndicator(CanvasGroup) 에 연결
//
// 안전 가드:
//   - 개발 중 Game/Lobby 씬을 직접 열어 테스트하면 UIManager.Instance가 null일 수 있다.
//     → 호출부는 항상 `UIManager.Instance?.ShowConfirm(...)` 처럼 null-safe로 호출한다.
//   - 내부 참조(_confirmPopup, _loadingIndicator)도 null 가드하여 부분 연결 상태에서도 안전.
//
// Presentation 레이어 — MonoBehaviour 의존. SingletonMonoBehaviour는 Core 레이어.
// ============================================================================

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Core;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 전역 공통 UI를 관리하는 싱글톤 매니저.
    /// IUIManager 계약을 구현하며, Login 씬에서 1회 생성되어 모든 씬에서 유지된다.
    /// </summary>
    public class UIManager : SingletonMonoBehaviour<UIManager>, IUIManager
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("공통 UI 참조")]
        [Tooltip("범용 확인/취소 팝업. UIManager Canvas 하위에 배치된 ConfirmPopup을 연결한다.")]
        [SerializeField] private ConfirmPopup _confirmPopup;

        [Tooltip("로딩 인디케이터의 CanvasGroup. alpha/blocksRaycasts로 표시·숨김을 제어한다. " +
                 "SetActive 대신 CanvasGroup을 쓰는 이유: 오브젝트를 항상 active로 유지하기 위함(공통 UI 규칙).")]
        [SerializeField] private CanvasGroup _loadingIndicator;

        [Tooltip("로딩 중 상태 메시지를 표시하는 텍스트. LoadingIndicator 하위의 StatusText를 연결한다. " +
                 "null이면 메시지 표시 없이 스피너만 보인다.")]
        [SerializeField] private TextMeshProUGUI _loadingStatusText;

        [Tooltip("로딩 인디케이터가 표시된 후 최소 유지해야 하는 시간(초). " +
                 "ShowLoading(false)가 이 시간보다 빨리 호출되면 남은 시간만큼 대기 후 숨긴다. " +
                 "너무 빠르게 사라지는 '깜빡임' 현상을 방지한다. 기본값: 1초.")]
        [SerializeField] private float _loadingMinDuration = 1f;

        [Header("반투명 배경 오버레이 (BlockingOverlay)")]
        [Tooltip("모든 팝업이 공유하는 반투명 배경 오버레이의 CanvasGroup. " +
                 "UIManager Canvas 직속(SafeAreaContainer 바깥)에 배치하여 노치/홈바 영향 없이 전체화면을 덮는다. " +
                 "규칙 5에 따라 SetActive 대신 alpha/blocksRaycasts로 표시·숨김을 제어한다. " +
                 "차단이 동작하려면 하위 Image의 Raycast Target=true 이어야 한다.")]
        [SerializeField] private CanvasGroup _blockingOverlay;

        [Tooltip("BlockingOverlay에 부착된 Button. Popup 모드(터치 시 닫기)에서 onClick에 콜백을 등록한다. " +
                 "Modal 모드(터치 차단만)에서는 onClick에 아무 리스너도 등록하지 않는다.")]
        [SerializeField] private Button _blockingOverlayButton;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 반투명 배경 오버레이의 중첩 참조 카운터.
        /// ShowBlockingOverlay 호출마다 +1, HideBlockingOverlay 호출마다 -1.
        /// 0이 될 때에만 실제로 오버레이를 숨긴다.
        /// (예: ConfirmPopup이 떠 있는 상태에서 또 다른 모달이 Show되면 2가 되고,
        ///  하나가 Hide되어도 1이 남아 있으므로 오버레이는 유지된다.)
        /// </summary>
        private int _blockingOverlayRefCount;

        /// <summary>
        /// ShowLoading(true)가 호출된 시각(Time.realtimeSinceStartup 기준).
        /// ShowLoading(false) 호출 시 최소 표시 시간이 지났는지 판단하는 데 사용한다.
        /// </summary>
        private float _loadingShowTime;

        /// <summary>
        /// ShowLoading(false) 지연 처리 코루틴 참조.
        /// 최소 표시 시간이 남아 있을 때 대기 중인 숨김 코루틴을 추적한다.
        /// 중복 실행 방지 및 조기 취소(show가 다시 호출되는 경우)에 사용한다.
        /// </summary>
        private Coroutine _hideLoadingCoroutine;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        /// <summary>
        /// 싱글톤 초기화 + 로딩 인디케이터 초기 상태(숨김) 적용.
        /// 반드시 base.Awake()를 호출해야 Instance 등록 + DontDestroyOnLoad가 동작한다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // 중복 인스턴스로 파괴 예약된 경우(base.Awake에서 Destroy 호출)에는
            // Instance가 자기 자신이 아니므로 추가 초기화를 건너뛴다.
            if (Instance != this) return;

            // 시작 시 로딩 인디케이터는 항상 숨김 상태로 둔다.
            ApplyLoadingVisibility(false);

            // 시작 시 반투명 배경 오버레이도 숨김 상태로 둔다(참조 카운터=0).
            _blockingOverlayRefCount = 0;
            ApplyBlockingOverlayVisibility(false);
        }

        // ====================================================================
        // IUIManager 구현
        // ====================================================================

        /// <summary>
        /// 확인/취소 팝업을 표시한다. 내부적으로 ConfirmPopup.Show()에 위임한다.
        /// _confirmPopup이 연결되지 않았으면 경고만 출력하고 무시한다.
        /// </summary>
        /// <param name="message">팝업 본문 메시지.</param>
        /// <param name="onConfirm">확인 클릭 콜백.</param>
        /// <param name="onCancel">취소 클릭 콜백(null 허용).</param>
        /// <param name="confirmLabel">확인 버튼 라벨.</param>
        /// <param name="cancelLabel">취소 버튼 라벨.</param>
        public void ShowConfirm(string message, System.Action onConfirm,
                                System.Action onCancel = null,
                                string confirmLabel = "확인", string cancelLabel = "취소")
        {
            if (_confirmPopup == null)
            {
                Debug.LogWarning("[UIManager] ConfirmPopup 참조가 비어 있어 확인 팝업을 표시할 수 없습니다. " +
                                 "Inspector에서 연결해 주세요.");
                return;
            }

            // ConfirmPopup.Show 시그니처: (message, confirmLabel, cancelLabel, onConfirm, onCancel)
            _confirmPopup.Show(message, confirmLabel, cancelLabel, onConfirm, onCancel);
        }

        /// <summary>
        /// 로딩 인디케이터 표시 여부를 토글한다.
        /// Firebase 처리, 씬 전환, 매칭 대기 등 로딩 사유에 관계없이 이 메서드 하나로 제어한다.
        ///
        /// 최소 표시 시간 보장:
        ///   ShowLoading(true) 후 _loadingMinDuration(기본 1초)이 지나기 전에
        ///   ShowLoading(false)가 호출되면, 남은 시간 동안 코루틴으로 대기한 뒤 숨긴다.
        ///   이를 통해 로딩 인디케이터가 깜빡이고 사라지는 현상을 방지한다.
        ///   대기 중에 ShowLoading(true)가 다시 호출되면 대기 코루틴을 취소하고 계속 표시 상태를 유지한다.
        /// </summary>
        /// <param name="show">true면 표시, false면 숨김(최소 표시 시간 보장 후).</param>
        /// <param name="message">로딩 중 표시할 상태 메시지. 빈 문자열이면 이전 메시지 유지.</param>
        public void ShowLoading(bool show, string message = "")
        {
            if (show)
            {
                // 대기 중인 숨김 코루틴이 있으면 취소한다.
                // (show → hide(대기중) → show 패턴에서 대기 중 숨김이 실행되지 않도록)
                if (_hideLoadingCoroutine != null)
                {
                    StopCoroutine(_hideLoadingCoroutine);
                    _hideLoadingCoroutine = null;
                }

                // 표시 시각을 기록한다. 이후 ShowLoading(false)에서 최소 표시 시간 계산에 사용한다.
                _loadingShowTime = Time.realtimeSinceStartup;

                if (_loadingStatusText != null && !string.IsNullOrEmpty(message))
                    _loadingStatusText.text = message;

                ApplyLoadingVisibility(true);
            }
            else
            {
                // 중복 숨김 코루틴 방지
                if (_hideLoadingCoroutine != null)
                {
                    StopCoroutine(_hideLoadingCoroutine);
                    _hideLoadingCoroutine = null;
                }

                float elapsed = Time.realtimeSinceStartup - _loadingShowTime;
                if (elapsed >= _loadingMinDuration)
                {
                    // 최소 표시 시간이 이미 지났으면 즉시 숨긴다.
                    if (_loadingStatusText != null)
                        _loadingStatusText.text = string.Empty;
                    ApplyLoadingVisibility(false);
                }
                else
                {
                    // 최소 표시 시간이 남아 있으면 코루틴으로 나머지 시간 대기 후 숨긴다.
                    _hideLoadingCoroutine = StartCoroutine(HideLoadingAfterMinDurationCoroutine());
                }
            }
        }

        /// <summary>
        /// 로딩 인디케이터 최소 표시 시간이 남아 있을 때 대기 후 숨기는 코루틴.
        /// WaitForSecondsRealtime을 사용하여 Time.timeScale=0 상황(씬 전환 등)에도 올바르게 동작한다.
        /// </summary>
        private IEnumerator HideLoadingAfterMinDurationCoroutine()
        {
            float elapsed = Time.realtimeSinceStartup - _loadingShowTime;
            float remaining = _loadingMinDuration - elapsed;

            if (remaining > 0f)
                yield return new WaitForSecondsRealtime(remaining);

            if (_loadingStatusText != null)
                _loadingStatusText.text = string.Empty;

            ApplyLoadingVisibility(false);
            _hideLoadingCoroutine = null;
        }

        /// <summary>
        /// 반투명 배경 오버레이를 표시한다(IUIManager 계약 구현).
        ///
        /// 동작:
        ///   1. 참조 카운터를 1 증가시킨다(중첩 표시 지원).
        ///   2. onClick 리스너를 항상 먼저 제거한다(이전 모드의 콜백이 남지 않도록).
        ///   3. Popup 모드(onTap != null)면 onClick에 콜백을 등록한다.
        ///      Modal 모드(onTap == null)면 리스너 없이 입력만 차단한다.
        ///   4. CanvasGroup을 표시 상태(alpha=1, 입력 차단)로 만든다.
        ///
        /// 주의: 중첩 시 가장 마지막에 등록된 onTap이 현재 오버레이 콜백이 된다.
        /// 일반적으로 Popup끼리 중첩되지 않으므로(Popup 위에는 Modal만 뜨는 구조) 문제되지 않는다.
        /// </summary>
        /// <param name="onTap">터치 콜백. null이면 Modal 모드(입력 차단만).</param>
        public void ShowBlockingOverlay(Action onTap = null)
        {
            // [DEBUG-TEMP] 디버깅 완료 후 제거
            Debug.Log($"[{DateTime.Now:HH:mm:ss.fff}] [INFO] [UI/UIManager] ShowBlockingOverlay 호출 | _blockingOverlay={((_blockingOverlay != null) ? "연결됨" : "NULL")}, refCount={_blockingOverlayRefCount + 1}, onTap={(onTap != null ? "있음" : "없음")}");

            // 중첩 표시 횟수를 누적한다.
            _blockingOverlayRefCount++;

            // 버튼이 연결되어 있으면 이전 리스너를 제거하고 현재 모드에 맞게 다시 등록한다.
            if (_blockingOverlayButton != null)
            {
                _blockingOverlayButton.onClick.RemoveAllListeners();

                // Popup 모드: 터치 시 콜백 실행. Modal 모드: 리스너 없음(입력만 차단).
                if (onTap != null)
                    _blockingOverlayButton.onClick.AddListener(() => onTap.Invoke());
            }

            // 실제 표시는 카운터와 무관하게 항상 "보이는 상태"를 보장하면 된다.
            ApplyBlockingOverlayVisibility(true);
        }

        /// <summary>
        /// 반투명 배경 오버레이를 숨긴다(IUIManager 계약 구현).
        ///
        /// 동작:
        ///   1. 참조 카운터를 1 감소시킨다(0 미만으로는 내려가지 않도록 가드).
        ///   2. 카운터가 0이 된 경우에만 실제로 오버레이를 숨기고 onClick 리스너를 정리한다.
        ///   3. 아직 다른 팝업이 오버레이를 사용 중(카운터 > 0)이면 표시 상태를 유지한다.
        /// </summary>
        public void HideBlockingOverlay()
        {
            // 카운터 언더플로 방지 — 중복 Hide 호출에도 안전하게 동작.
            if (_blockingOverlayRefCount > 0)
                _blockingOverlayRefCount--;

            // 아직 오버레이를 사용 중인 팝업이 남아 있으면 숨기지 않는다.
            if (_blockingOverlayRefCount > 0) return;

            // 마지막 사용자가 닫혔으므로 실제로 숨김 처리 + 콜백 해제.
            if (_blockingOverlayButton != null)
                _blockingOverlayButton.onClick.RemoveAllListeners();

            ApplyBlockingOverlayVisibility(false);
        }

        // ====================================================================
        // 내부 로직
        // ====================================================================

        /// <summary>
        /// 로딩 인디케이터 CanvasGroup의 가시성을 적용한다.
        /// SetActive 대신 alpha/blocksRaycasts를 사용해 오브젝트를 항상 active로 유지한다.
        /// _loadingIndicator가 연결되지 않았으면 아무 동작도 하지 않는다(안전 가드).
        /// </summary>
        /// <param name="show">true면 표시(alpha=1, 입력 차단), false면 숨김(alpha=0).</param>
        private void ApplyLoadingVisibility(bool show)
        {
            if (_loadingIndicator == null) return;

            _loadingIndicator.alpha = show ? 1f : 0f;
            // 로딩 중에는 뒤쪽 UI 입력을 막아 사용자가 중복 조작하지 못하게 한다.
            _loadingIndicator.blocksRaycasts = show;
            _loadingIndicator.interactable = show;
        }

        /// <summary>
        /// 반투명 배경 오버레이 CanvasGroup의 가시성을 적용한다.
        /// SetActive 대신 alpha/blocksRaycasts를 사용해 오브젝트를 항상 active로 유지한다(규칙 5).
        /// _blockingOverlay가 연결되지 않았으면 아무 동작도 하지 않는다(안전 가드).
        /// </summary>
        /// <param name="show">true면 표시(alpha=1, 입력 차단), false면 숨김(alpha=0, 입력 통과).</param>
        private void ApplyBlockingOverlayVisibility(bool show)
        {
            // [DEBUG-TEMP] 디버깅 완료 후 제거
            Debug.Log($"[{DateTime.Now:HH:mm:ss.fff}] [INFO] [UI/UIManager] ApplyBlockingOverlayVisibility | show={show}, _blockingOverlay={((_blockingOverlay != null) ? $"alpha→{(show ? 1f : 0f)}" : "NULL(건너뜀)")}");

            if (_blockingOverlay == null) return;

            _blockingOverlay.alpha = show ? 1f : 0f;
            _blockingOverlay.blocksRaycasts = show;
            _blockingOverlay.interactable = show;
        }
    }
}
