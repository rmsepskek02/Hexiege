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

using UnityEngine;
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
        /// </summary>
        /// <param name="show">true면 표시, false면 숨김.</param>
        public void ShowLoading(bool show)
        {
            ApplyLoadingVisibility(show);
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
    }
}
