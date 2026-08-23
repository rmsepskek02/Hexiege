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
using Hexiege.Application;      // GameLog — 런타임 로그 파사드 (LogRules.md 1.4)
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
                // [개발] Warn + 개발.
                //   UIManager 는 Login 씬에서 1회 생성되어 DontDestroyOnLoad 되므로,
                //   여기서 null 이면 Login 을 거치지 않고 씬에 직접 진입한 개발 편의 경로다.
                //   축 A: 팝업을 띄우지 않고 그냥 반환해 게임은 계속된다 → Warn.
                //   축 B: 플레이어 빌드는 항상 Login 부터 시작한다 → 축 B ① 이 "아니오" → 개발.
                GameLog.Dev.Warn("UI", nameof(UIManagerTestButtonHandler),
                                 "UIManager.Instance 가 null — 테스트 팝업을 표시할 수 없다");
                return;
            }

            UIManager.Instance.ShowConfirm(
                message:      "UIManager 테스트\n확인 → NetworkErrorPopup 표시\n취소 → 닫기",
                onConfirm:    ShowNetworkError,
                // [개발] Info + 개발. 테스트 시나리오의 정상 흐름 통보다.
                //   ⚠️ 블록 본문 `{ ... ; }` 으로 감싼 이유: 개발 축 로그 메서드에는 [Conditional] 이 붙어 있고,
                //      C# 컴파일러는 **문(statement) 자리의 호출만** 제거한다. 식 본문 람다
                //      (화살표 뒤에 호출식만 오는 형태)는 문이 아니라 식이라 제거 규칙이 적용되지 않는다.
                //      블록으로 감싸면 릴리스 빌드에서 호출과 문자열 조립이 정상적으로 사라진다(LogRules 1.7).
                onCancel:     () => { GameLog.Dev.Info("UI", nameof(UIManagerTestButtonHandler),
                                                       "취소 클릭 — ConfirmPopup 닫힘"); },
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
                // [개발] Warn + 개발.
                //   FindAnyObjectByType 이 null = "이 씬에 LoginRootView 가 없다" 는 뜻이고,
                //   그건 테스트를 Login 씬 밖에서 눌렀다는 사용 오류다(1.3 원칙 3 단서와 같은 부류).
                GameLog.Dev.Warn("UI", nameof(UIManagerTestButtonHandler),
                                 "LoginRootView 를 찾을 수 없다 — Login 씬인지 확인해야 한다");
                return;
            }

            // [개발] Info + 개발. 테스트 시나리오의 정상 흐름 통보다.
            //   ⚠️ 이 파일은 Assets/Editor/ 가 아니라 Assets/_Project/Scripts/Debug/ 에 있어
            //      **릴리스 빌드에 포함**된다. GameLog.Dev 로 옮기면 [Conditional] 두 개가 붙어
            //      릴리스에서 호출과 문자열 조립까지 통째로 사라진다(LogRules 1.7).
            GameLog.Dev.Info("UI", nameof(UIManagerTestButtonHandler), "NetworkErrorPopup 표시");
            rootView.ShowNetworkErrorPopup();
        }
    }
}
