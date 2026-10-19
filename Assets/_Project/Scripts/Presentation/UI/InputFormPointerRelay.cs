using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hexiege.Presentation
{
    /// <summary>기존 Button의 정상 클릭 처리를 유지하면서 실제 누름·해제 회차만 입력 폼에 전달한다.</summary>
    public sealed class InputFormPointerRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private InputFormController _form;
        private Button _button;

        /// <summary>이미 연결된 폼 버튼에 누름 전달자를 한 번만 연결한다.</summary>
        public static InputFormPointerRelay Attach(InputFormController form, Button button)
        {
            var relay = button.GetComponent<InputFormPointerRelay>();
            if (relay == null) relay = button.gameObject.AddComponent<InputFormPointerRelay>();
            relay._form = form;
            relay._button = button;
            return relay;
        }

        /// <summary>재연결·파괴 시 이전 폼으로 이벤트가 전달되지 않도록 연결을 해제한다.</summary>
        public void Detach(InputFormController form)
        {
            if (_form == form) _form = null;
        }

        /// <summary>왼쪽 버튼 누름만 전달하며 실행 요청은 기존 Button.OnPointerClick에 맡긴다.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _form == null) return;
            _form.BeginButtonPointer(_button, eventData.pointerId);
        }

        /// <summary>해제 위치가 달라 클릭이 취소되어도 실제 누름 회차는 끝낸다.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _form == null) return;
            _form.EndButtonPointer(_button, eventData.pointerId);
        }
    }
}
