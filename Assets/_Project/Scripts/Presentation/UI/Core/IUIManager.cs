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
        /// 알림 팝업을 표시한다. 제목 + 본문 + 버튼 1개 구조다(공통 UI 규칙 D-5).
        ///
        /// <para>
        /// <see cref="ShowConfirm"/> 과의 차이 — 저쪽은 사용자가 두 갈래 중 하나를
        /// 고르는 자리이고, 이것은 <b>고를 것이 없고 알리기만 하는 자리</b>다.
        /// 타입은 둘 다 <b>모달</b>이라 배경을 탭해도 닫히지 않는다(공통 UI 규칙 8 · 9).
        /// </para>
        /// </summary>
        /// <param name="title">팝업 제목(예: "알림"). 비어 있으면 제목 자리가 숨는다.</param>
        /// <param name="message">본문 메시지(예: "상대방이 떠났습니다.").</param>
        /// <param name="onClick">버튼 클릭 시 호출될 콜백. null이면 닫히기만 한다.</param>
        /// <param name="buttonLabel">유일한 버튼의 라벨(기본값 "확인").</param>
        void ShowAlert(string title, string message, System.Action onClick = null,
                       string buttonLabel = "확인");

        /// <summary>
        /// 지금 떠 있는 공통 팝업을 닫는다. <b>확인/취소 팝업(<see cref="ShowConfirm"/>)과
        /// 알림 팝업(<see cref="ShowAlert"/>) 양쪽 모두</b>가 대상이다.
        ///
        /// <para>
        /// 🔴 <b>왜 닫는 메서드는 하나뿐인가</b> — 띄우는 메서드는 둘이지만 화면에 나타나는
        /// 실체는 <b>같은 팝업 컴포넌트(ConfirmPopup) 하나</b>이고, 그 하나가 「제목·취소 버튼을
        /// 켜고 끄는 방식」으로 두 가지 모습을 겸한다. 그래서 닫는 문도 하나이며,
        /// 둘 중 무엇이 떠 있었든 이 메서드 하나로 똑같이 닫힌다.
        /// </para>
        /// <para>
        /// 🔴 <b>버튼 콜백은 호출하지 않는다.</b> 사용자가 버튼을 누른 것이 아니라
        /// 화면 쪽 사정으로 닫는 것이므로, 「확인을 눌렀을 때의 동작」이 일어나면 안 된다.
        /// 순수하게 <b>시각적으로 닫기만</b> 한다.
        /// </para>
        /// <para>
        /// 팝업이 떠 있지 않을 때 호출해도 <b>아무 일도 일어나지 않으며 그것이 정상</b>이다.
        /// 화면을 떠나는 자리에서 「혹시 떠 있으면 닫아라」라는 뜻으로 조건 없이 부르는 것을
        /// 전제로 만든 API 이기 때문이다. (그래서 구현체는 이 호출에 경고 로그를 남기지 않는다.)
        /// </para>
        /// </summary>
        void HideConfirmOrAlert();

        /// <summary>
        /// 로딩 인디케이터의 표시 여부를 토글한다.
        /// Firebase 처리, 씬 전환, 매칭 대기 등 로딩이 필요한 모든 상황에서 사용한다.
        /// 로딩의 사유와 로딩 UI는 분리되어 있으며, 호출부는 시작/종료 시점만 알린다.
        /// </summary>
        /// <param name="show">true면 표시, false면 숨김.</param>
        /// <param name="message">로딩 중 표시할 상태 메시지. 빈 문자열이면 이전 메시지 유지.</param>
        void ShowLoading(bool show, string message = "");

        /// <summary>
        /// 로딩 인디케이터를 표시하고 1초 대기 후 씬을 전환한다.
        /// </summary>
        /// <param name="sceneName">전환할 씬 이름.</param>
        /// <param name="message">로딩 중 표시할 상태 메시지.</param>
        void LoadSceneWithDelay(string sceneName, string message);

        /// <summary>
        /// 반투명 배경 오버레이(BlockingOverlay)를 표시한다.
        ///
        /// 이 오버레이는 UIManager가 단일 소유하며, 팝업이 직접 소유하지 않는다.
        /// (근거: GameSystemRules_UI.md 공통 규칙 4 — 전체화면 요소는 SafeAreaContainer 밖에 둔다.
        ///        공통 규칙 5 — 표시/숨김은 CanvasGroup으로 제어한다.)
        ///
        /// 두 가지 모드로 동작한다.
        ///   - Modal 모드 (onTap == null): 뒤쪽 입력만 차단한다. 오버레이를 터치해도 아무 일도 일어나지 않는다.
        ///     (예: ConfirmPopup, AnonymousWarningPopup, RematchRequestPopup — 명시적 버튼으로만 닫힘)
        ///   - Popup 모드 (onTap != null): 오버레이를 터치하면 등록된 콜백(보통 팝업 닫기)이 실행된다.
        ///     (예: InGameSettingsUI, BuildingPlacementUI — 바깥을 탭하면 닫힘)
        ///
        /// 중첩 호출(이미 표시된 상태에서 다시 Show)은 참조 카운터로 누적 관리한다.
        /// HideBlockingOverlay()가 마지막 1건까지 모두 호출되어 카운터가 0이 될 때에만 실제로 숨겨진다.
        /// </summary>
        /// <param name="onTap">
        /// 오버레이 터치 시 실행할 콜백. null이면 Modal 모드(입력 차단만), 값이 있으면 Popup 모드(터치 시 콜백 실행).
        /// </param>
        void ShowBlockingOverlay(System.Action onTap = null);

        /// <summary>
        /// 반투명 배경 오버레이(BlockingOverlay)를 숨긴다.
        /// 중첩 표시 중이라면 참조 카운터를 1 감소시키고, 카운터가 0이 될 때에만 실제로 숨김 처리한다.
        /// </summary>
        void HideBlockingOverlay();
    }
}

