// ============================================================================
// UIManagerTestButtonHandler.cs
// UIManager.ShowConfirm / ShowLoading 동작을 검증하는 임시 테스트 핸들러.
//
// 동작:
//   버튼 클릭
//     → UIManager.ShowConfirm 팝업 표시
//     → 확인 클릭 → UIManager.ShowLoading(true, "테스트 로딩 중...") → 2초 후 Hide
//     → 취소 클릭 → 아무것도 안 함
//
// 주의:
//   이 컴포넌트는 임시 테스트용이다.
//   테스트 완료 후 씬에서 [UIManager Test] 오브젝트를 삭제하고
//   이 파일(UIManagerTestButtonHandler.cs)도 제거해도 무방하다.
//
// Presentation 레이어.
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Presentation;

namespace Hexiege.DebugTools
{
    /// <summary>
    /// UIManager ShowConfirm / ShowLoading 임시 테스트 핸들러.
    /// [UIManager Test] 버튼 GameObject에 부착된다.
    /// </summary>
    public class UIManagerTestButtonHandler : MonoBehaviour
    {
        private void Start()
        {
            // Button.onClick에 테스트 동작을 연결한다.
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnTestButtonClicked);
        }

        /// <summary>
        /// 테스트 버튼 클릭 시 호출. ShowConfirm → 확인 시 ShowLoading 흐름을 검증한다.
        /// </summary>
        private void OnTestButtonClicked()
        {
            if (UIManager.Instance == null)
            {
                UnityEngine.Debug.LogWarning("[UIManagerTest] UIManager.Instance가 null입니다. " +
                                             "Login 씬에 [UI Systems] > UIManager가 배치되어 있는지 확인하세요.");
                return;
            }

            UIManager.Instance.ShowConfirm(
                message:      "UIManager 테스트\n확인 → ShowLoading(2초)\n취소 → 닫기",
                onConfirm:    () => StartCoroutine(TestLoadingCoroutine()),
                onCancel:     () => UnityEngine.Debug.Log("[UIManagerTest] 취소 클릭 — ConfirmPopup 닫힘 ✅"),
                confirmLabel: "로딩 테스트",
                cancelLabel:  "닫기"
            );
        }

        /// <summary>
        /// ShowLoading(true) → 2초 대기 → ShowLoading(false) 흐름을 검증한다.
        /// </summary>
        private IEnumerator TestLoadingCoroutine()
        {
            UnityEngine.Debug.Log("[UIManagerTest] ShowLoading(true) 호출 ✅");
            UIManager.Instance?.ShowLoading(true, "테스트 로딩 중...");

            yield return new WaitForSeconds(2f);

            UIManager.Instance?.ShowLoading(false);
            UnityEngine.Debug.Log("[UIManagerTest] ShowLoading(false) 호출 — 로딩 종료 ✅");
        }
    }
}
