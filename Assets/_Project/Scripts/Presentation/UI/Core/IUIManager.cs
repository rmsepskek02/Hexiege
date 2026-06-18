// ============================================================================
// IUIManager.cs
// 전역 UI 시스템의 외부 노출 계약(인터페이스).
//
// 목적:
//   ConfirmPopup, LoadingIndicator 같은 "어느 씬에서든 공통으로 쓰이는 UI"를
//   View나 Bootstrapper가 직접 들고 있지 않고, 이 인터페이스를 통해서만 사용하도록 만든다.
//   → 씬마다 공통 UI를 중복 배치하던 문제를 없애고, 호출부는 구현 세부를 모른 채
//     "확인 팝업 띄워줘", "로딩 표시 켜줘" 같은 의도만 전달한다.
//
// 구현체:
//   UIManager (SingletonMonoBehaviour<UIManager>) 가 이 인터페이스를 구현한다.
//   호출부는 UIManager.Instance 로 인스턴스를 얻어 이 계약의 메서드만 호출한다.
//
// Presentation 레이어 — 순수 C# 인터페이스(Unity 의존 없음).
// ============================================================================

namespace Hexiege.Presentation
{
    /// <summary>
    /// 전역 공통 UI(확인 팝업, 로딩 표시)에 대한 외부 노출 계약.
    /// 구현체(UIManager)는 씬 전환과 무관하게 단 하나만 존재한다.
    /// </summary>
    public interface IUIManager
    {
        /// <summary>
        /// 확인/취소 두 선택지를 가진 범용 확인 팝업을 표시한다.
        /// </summary>
        /// <param name="message">팝업 본문에 표시할 메시지.</param>
        /// <param name="onConfirm">확인 버튼 클릭 시 호출될 콜백.</param>
        /// <param name="onCancel">취소 버튼 클릭 시 호출될 콜백. null이면 닫히기만 한다.</param>
        /// <param name="confirmLabel">확인 버튼 라벨(기본값 "확인").</param>
        /// <param name="cancelLabel">취소 버튼 라벨(기본값 "취소").</param>
        void ShowConfirm(string message, System.Action onConfirm,
                         System.Action onCancel = null,
                         string confirmLabel = "확인", string cancelLabel = "취소");

        /// <summary>
        /// 로딩 인디케이터의 표시 여부를 토글한다.
        /// Firebase 처리, 씬 전환, 매칭 대기 등 로딩이 필요한 모든 상황에서 사용한다.
        /// 로딩의 사유와 로딩 UI는 분리되어 있으며, 호출부는 시작/종료 시점만 알린다.
        /// </summary>
        /// <param name="show">true면 표시, false면 숨김.</param>
        /// <param name="message">로딩 중 표시할 상태 메시지. 빈 문자열이면 이전 메시지 유지.</param>
        void ShowLoading(bool show, string message = "");
    }
}

