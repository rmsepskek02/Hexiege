using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hexiege.Presentation
{
    /// <summary>같은 폼의 입력란 전환에서는 native 키보드의 명시적 Hide를 생략하고 기존 TMP 활성화를 이어간다.</summary>
    public sealed class ContinuousInputField : TMP_InputField
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private InputFormController _form;
        private bool _flushingTransfer;
#endif

        /// <summary>직렬화된 TMP 참조·이벤트를 유지하면서 해당 입력 폼의 전환 수명만 연결한다.</summary>
        internal void BindForm(InputFormController form)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _form = form;
#endif
        }

        /// <summary>같은 폼의 다음 입력란일 때만 이전 native 연결을 임시 보관한다.</summary>
        public override void OnDeselect(BaseEventData eventData)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var target = _form != null ? _form.ResolveTransferTarget(this, eventData) : null;
            if (!_flushingTransfer && target != null && isFocused && m_SoftKeyboard != null &&
                m_SoftKeyboard.active && m_SoftKeyboard.status == TouchScreenKeyboard.Status.Visible)
            {
                var keyboard = m_SoftKeyboard;
                // native 마지막 글자·선택 범위는 TMP 자신의 검증/동기화 경로로 반영한다.
                // 이 과정에서 Done/취소/화면 닫기가 재진입하면 아래 동일 연결 검사를 통과하지 못한다.
                _flushingTransfer = true;
                try { base.LateUpdate(); }
                finally { _flushingTransfer = false; }
                if (isFocused && m_SoftKeyboard == keyboard &&
                    _form != null && _form.RetainKeyboardForTransfer(this, target, keyboard))
                    m_SoftKeyboard = null;
            }
#endif
            // 원래 TMP는 여기서 keyboard.active=false 후 참조를 버린다. 폼 내 전환만 위에서
            // 참조를 분리하므로 기본 private 편집 상태·포커스 이벤트의 정리는 그대로 실행된다.
            base.OnDeselect(eventData);
        }

        /// <summary>대상 입력란의 기존 활성화/Open이 끝난 시점을 폼에 알린다.</summary>
        protected override void LateUpdate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!isFocused && _form != null &&
                _form.UsesSharedSecureKeyboard(this) && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject)
            {
                // TMP의 원문/취소/포커스/커서 초기화는 그대로 실행하되 기본 Open만 막는다.
                // setter는 기존 연결이 있으면 닫으므로 반드시 연결이 없는 활성화 경계만 사용한다.
                if (m_SoftKeyboard == null)
                {
                    var form = _form;
                    int session = form.Session;
                    bool originalHide = shouldHideSoftKeyboard;
                    shouldHideSoftKeyboard = true;
                    try { base.LateUpdate(); }
                    finally { shouldHideSoftKeyboard = originalHide; }
                    if (isFocused && _form == form && form.IsCurrent(session) &&
                        EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject &&
                        form.UsesSharedSecureKeyboard(this))
                    {
                        m_SoftKeyboard = form.AcquireSharedSecureKeyboard(this);
                        if (m_SoftKeyboard != null)
                        {
                            // native RangeInt는 방향을 표현하지 않는다. 게임 내부 양끝의 방향은
                            // 그대로 두고 native 범위만 현재 문자열 길이에 맞춰 정규화한다.
                            int anchor = Mathf.Clamp(stringPositionInternal, 0, text.Length);
                            int focus = Mathf.Clamp(stringSelectPositionInternal, 0, text.Length);
                            int start = Mathf.Min(anchor, focus);
                            int length = Mathf.Abs(anchor - focus);
                            m_SoftKeyboard.selection = new RangeInt(start, length);
                            form.CompleteKeyboardTransfer(this);
                        }
                        else DeactivateInputField();
                    }
                    else if (isFocused) DeactivateInputField();
                    return;
                }
            }
#endif
            base.LateUpdate();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (isFocused && _form != null) _form.CompleteKeyboardTransfer(this);
#endif
        }
    }
}
