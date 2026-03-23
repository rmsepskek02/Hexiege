// ============================================================================
// SharedBackgroundButton.cs
// Canvas 직속 공유 Background에 부착하여, 현재 열린 패널의 닫기 동작을 처리하는 컴포넌트.
//
// 사용 방법:
//   1. Canvas 직속 "Background" 오브젝트에 이 컴포넌트를 추가한다.
//   2. 같은 오브젝트의 Button 컴포넌트 onClick에 OnClick() 메서드를 연결한다.
//   3. 각 팝업 패널은 Show() 시 Register(Close)를 호출하고,
//      Close() 시 Unregister()를 호출한다.
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
        /// 패널의 Close 메서드 (예: BuildingPlacementUI.Close, ProductionPanelUI.Close).
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
