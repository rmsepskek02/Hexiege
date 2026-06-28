// ============================================================================
// UIManagerTestButtonHandler.cs
// ConfirmPopup / NetworkErrorPopup 동작을 검증하는 임시 테스트 핸들러.
//
// 동작:
//   버튼 클릭
//     → UIManager.ShowConfirm 팝업 표시
//     → 확인 클릭 → LoginRootView.ShowNetworkErrorPopup() 호출
//     → 취소 클릭 → 아무것도 안 함
//
// 주의:
//   이 컴포넌트는 임시 테스트용이다.
//   테스트 완료 후 씬에서 [UIManager Test] 오브젝트를 삭제하고
//   이 파일(UIManagerTestButtonHandler.cs)도 제거해도 무방하다.
//
// Presentation 레이어.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using Hexiege.Presentation;

namespace Hexiege.DebugTools
{
    /// <summary>
    /// ConfirmPopup / NetworkErrorPopup 임시 테스트 핸들러.
    /// [UIManager Test] 버튼 GameObject에 부착된다.
    /// </summary>
    public class UIManagerTestButtonHandler : MonoBehaviour
    {
        private void Start()
        {
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnTestButtonClicked);
        }

        /// <summary>
        /// 테스트 버튼 클릭 시 호출. ConfirmPopup → 확인 시 NetworkErrorPopup 흐름을 검증한다.
        /// </summary>
        private void OnTestButtonClicked()
        {
            if (UIManager.Instance == null)
            {
                Debug.LogWarning("[UIManagerTest] UIManager.Instance가 null입니다.");
                return;
            }

            UIManager.Instance.ShowConfirm(
                message:      "UIManager 테스트\n확인 → NetworkErrorPopup 표시\n취소 → 닫기",
                onConfirm:    ShowNetworkError,
                onCancel:     () => Debug.Log("[UIManagerTest] 취소 클릭 — ConfirmPopup 닫힘 ✅"),
                confirmLabel: "네트워크 오류 테스트",
                cancelLabel:  "닫기"
            );
        }

        /// <summary>
        /// LoginRootView를 씬에서 찾아 ShowNetworkErrorPopup()을 호출한다.
        /// </summary>
        private void ShowNetworkError()
        {
            var rootView = FindAnyObjectByType<LoginRootView>();
            if (rootView == null)
            {
                Debug.LogWarning("[UIManagerTest] LoginRootView를 찾을 수 없습니다. Login 씬인지 확인하세요.");
                return;
            }

            Debug.Log("[UIManagerTest] NetworkErrorPopup 표시 ✅");
            rootView.ShowNetworkErrorPopup();
        }
    }
}
