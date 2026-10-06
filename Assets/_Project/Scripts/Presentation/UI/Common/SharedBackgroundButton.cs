// ============================================================================
// SharedBackgroundButton.cs
// Canvas 직속 공유 Background에 부착하여, 현재 열린 패널의 닫기 동작을 처리하는 컴포넌트.
//
// 사용 방법:
//   1. Canvas 직속 "Background" 오브젝트에 이 컴포넌트를 추가한다.
//   2. 같은 오브젝트의 Button 컴포넌트 onClick에 OnClick() 메서드를 연결한다.
//   3. 각 팝업 패널이 열릴 때 자신의 닫기 메서드를 등록하고, 닫힐 때 등록을 해제한다.
//
// ⚠️ 지금 이 방식을 쓰는 팝업은 하나도 없다(2026-10-02 실측 — 등록/해제를 부르는 코드가
//    프로젝트 전체에 0건이고, 남아 있는 것은 주석과 비활성화된 옛 코드뿐이다).
//    이 컴포넌트가 붙어 있는 씬 오브젝트도 꺼져 있다.
//    바깥을 탭해 닫는 동작은 공용 UI 관리자가 단일 소유하는 반투명 막이 대신 맡고 있다.
//    🔴 그러므로 위 「사용 방법」은 <이 컴포넌트를 쓰려면 이렇게 해야 한다>는 설명이며,
//       지금 그렇게 돌고 있다는 뜻이 아니다.
//
// 동작 원리:
//   - Register()가 호출되면 닫기 콜백(_onClose)이 저장된다.
//   - 사용자가 Background를 터치하면 Button.onClick → OnClick() →
//     저장된 _onClose가 호출되어 현재 패널이 닫힌다.
//   - Unregister()가 호출되면 콜백이 null이 되어,
//     Background를 터치해도 아무 일도 일어나지 않는다.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 공유 Background 닫기 버튼 컨트롤러.
    /// 현재 열린 팝업 패널의 Close 콜백을 등록/해제하여,
    /// Background 터치 시 해당 패널을 닫는 역할을 한다.
    /// </summary>
    public class SharedBackgroundButton : MonoBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 현재 등록된 닫기 콜백.
        /// 팝업이 열릴 때 Register()로 설정되고,
        /// 팝업이 닫힐 때 Unregister()로 null이 된다.
        /// </summary>
        private System.Action _onClose;

        // ====================================================================
        // 공개 메서드
        // ====================================================================

        /// <summary>
        /// 팝업 패널이 열릴 때 호출.
        /// 이후 Background를 터치하면 전달받은 onClose가 실행되어 패널이 닫힌다.
        /// </summary>
        /// <param name="onClose">
        /// 패널 쪽의 닫기 메서드(팝업을 닫는 메서드를 그대로 넘긴다).
        /// null을 전달하면 터치해도 아무 동작 없음.
        /// </param>
        public void Register(System.Action onClose)
        {
            _onClose = onClose;
        }

        /// <summary>
        /// 팝업 패널이 닫힐 때 호출.
        /// 등록된 콜백을 제거하여 Background 터치가 무효화된다.
        /// 패널 Close() 내에서 _popup.Hide() 이전에 호출해야
        /// Hide 애니메이션 중 추가 터치가 발생해도 안전하다.
        /// </summary>
        public void Unregister()
        {
            _onClose = null;
        }

        /// <summary>
        /// Button.onClick 이벤트에 연결할 메서드.
        /// Inspector에서 Background 오브젝트의 Button → onClick → OnClick()으로 연결한다.
        /// 등록된 콜백이 있으면 실행하고, 없으면 아무것도 하지 않는다.
        /// </summary>
        public void OnClick()
        {
            // 등록된 닫기 콜백이 있을 때만 실행
            // ?. 연산자: _onClose가 null이 아닌 경우에만 Invoke() 호출
            _onClose?.Invoke();
        }
    }
}
