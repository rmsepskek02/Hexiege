using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.Presentation
{
    /// <summary>현재 선택한 입력란을 실제 키보드 가림 영역 밖으로 이동하고 원래 배치로 복원한다.</summary>
    internal sealed class MobileKeyboardAvoidance
    {
        private readonly RectTransform _root;
        private readonly Button _submit;
        private readonly TMP_InputField[] _fields;
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly Canvas _canvas;
        private readonly RectMask2D _viewportMask;
        private readonly RectTransform _feedback;
        private readonly TMP_Text _feedbackText;
        private Vector2 _offset;
        private bool _submitPointerHeld;
        private int _submitReleasedFrame = -10;
        private Button _pressedButton;
        private Button _releasedButton;
        private int _pointerId;

        /// <summary>제출 버튼을 누른 채 키보드가 닫혀도 손을 뗄 때까지 같은 요청 제스처로 취급한다.</summary>
        public bool IsSubmitGesture => (_submitPointerHeld && _pressedButton == _submit) ||
            (_submitReleasedFrame == Time.frameCount && _releasedButton == _submit);
        /// <summary>제출·뒤로가기 등 실제 폼 버튼의 누름 회차가 아직 끝나지 않았는지 확인한다.</summary>
        public bool HasPointerGesture => _submitPointerHeld || _submitReleasedFrame == Time.frameCount;
#if UNITY_ANDROID && !UNITY_EDITOR
        private bool _nativeUnavailable;
#endif

        /// <summary>씬에 지정한 본문만 이동하며 고정 헤더·클리핑 경계를 유지한다.</summary>
        public MobileKeyboardAvoidance(RectTransform root, Button submit, TMP_InputField[] fields,
            RectTransform feedback = null)
        {
            _root = root;
            _submit = submit;
            _fields = fields;
            if (root != null) _canvas = root.GetComponentInParent<Canvas>()?.rootCanvas;
            if (root != null && root.parent != null) _viewportMask = root.parent.GetComponent<RectMask2D>();
            _feedback = feedback;
            if (feedback != null) _feedbackText = feedback.GetComponent<TMP_Text>();
        }

        /// <summary>누적 보정을 먼저 제거한 뒤 현재 화면 좌표로 필요한 이동량만 다시 계산한다.</summary>
        public void Update(bool visible, bool transferring = false)
        {
            // PointerDown으로 입력 포커스가 사라지더라도 PointerUp 전에 버튼이 내려가면
            // 클릭이 취소된다. 실제 누름 회차가 끝난 뒤에만 원래 배치를 복원한다.
            if (visible && (HasPointerGesture || transferring)) return;
            Restore();
            if (!visible || _root == null || _canvas == null) return;
            TMP_InputField selected = null;
            foreach (var field in _fields)
                if (field != null && field.isFocused) { selected = field; break; }
            if (selected == null || !TryGetKeyboardTop(out float keyboardTop)) return;

            Rect safe = Screen.safeArea;
            // 여백은 키보드 추정 높이가 아니라 실제 폼 배율에 따른 작은 읽기 여백이다.
            float margin = 12f * _canvas.scaleFactor;
            float bottom = Mathf.Max(safe.yMin, keyboardTop) + margin;
            float top = safe.yMax - margin;
            if (_viewportMask != null && _viewportMask.isActiveAndEnabled)
            {
                Rect viewport = ScreenRect(_viewportMask.rectTransform);
                bottom = Mathf.Max(bottom, viewport.yMin + margin);
                top = Mathf.Min(top, viewport.yMax - margin);
            }
            if (top <= bottom) return;
            Rect focus = ScreenRect(selected.transform as RectTransform);
            Rect desired = focus;
            if (_submit != null && _submit.IsActive())
                desired = Union(desired, ScreenRect(_submit.transform as RectTransform));
            if (_feedbackText != null && _feedbackText.isActiveAndEnabled && !string.IsNullOrEmpty(_feedbackText.text))
                desired = Union(desired, ScreenRect(_feedback));
            float delta = CalculateOffset(focus.yMin, focus.yMax, desired.yMin, desired.yMax, bottom, top);
            ApplyScreenOffset(delta);
        }

        /// <summary>이전 프레임에 직접 더한 이동량만 되돌려 해상도·SafeArea 변경 후에도 오프셋이 쌓이지 않게 한다.</summary>
        public void Restore()
        {
            if (_root != null && _offset != Vector2.zero) _root.anchoredPosition -= _offset;
            _offset = Vector2.zero;
        }

        /// <summary>실제 버튼의 PointerDown을 받은 뒤 같은 포인터의 PointerUp까지 위치를 고정한다.</summary>
        public bool BeginPointer(Button button, int pointerId)
        {
            // 이전 Pointer.current/사각형 추정은 실제 EventSystem의 포커스·누름 순서와 달랐고
            // 제출 버튼만 보호해 뒤로가기 버튼은 손을 떼기 전에 원래 위치로 내려갈 수 있었다.
            if (_submitPointerHeld || button == null || !button.IsInteractable()) return false;
            _pressedButton = button;
            _pointerId = pointerId;
            _submitPointerHeld = true;
            return true;
        }

        /// <summary>같은 버튼·포인터의 해제 프레임은 EventSystem의 클릭 판정이 끝날 때까지 보존한다.</summary>
        public void EndPointer(Button button, int pointerId)
        {
            if (!_submitPointerHeld || _pressedButton != button || _pointerId != pointerId) return;
            _submitPointerHeld = false;
            _releasedButton = button;
            _submitReleasedFrame = Time.frameCount;
        }

        /// <summary>화면 종료·재연결 시 이전 화면의 포인터 회차를 버리고 배치를 즉시 복원한다.</summary>
        public void Reset()
        {
            _submitPointerHeld = false;
            _submitReleasedFrame = -10;
            _pressedButton = _releasedButton = null;
            Restore();
        }

        // 입력란과 실행 버튼이 함께 들어가면 둘 다 노출한다. 가용 높이가 작으면 입력란을 우선한다.
        // 화면 전체를 축소하거나 기존 LayoutGroup의 크기를 변경하지 않는다.
        internal static float CalculateOffset(float focusMin, float focusMax, float desiredMin,
            float desiredMax, float visibleMin, float visibleMax)
        {
            float height = visibleMax - visibleMin;
            if (height <= 0f) return 0f;
            float low = desiredMin, high = desiredMax;
            if (high - low > height) { low = focusMin; high = focusMax; }
            if (high - low > height) return visibleMax - high;
            return Mathf.Clamp(0f, visibleMin - low, visibleMax - high);
        }

        private Rect ScreenRect(RectTransform target)
        {
            if (target == null) return new Rect();
            target.GetWorldCorners(_corners);
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
            Vector2 max = min;
            for (int i = 1; i < _corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, _corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void ApplyScreenOffset(float delta)
        {
            if (Mathf.Abs(delta) < 0.5f || !(_root.parent is RectTransform parent)) return;
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, _root.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, camera, out var from) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen + Vector2.up * delta,
                    camera, out var to)) return;
            _offset = to - from;
            _root.anchoredPosition += _offset;
        }

        private static Rect Union(Rect first, Rect second) => Rect.MinMaxRect(
            Mathf.Min(first.xMin, second.xMin), Mathf.Min(first.yMin, second.yMin),
            Mathf.Max(first.xMax, second.xMax), Mathf.Max(first.yMax, second.yMax));

        private bool TryGetKeyboardTop(out float top)
        {
            top = 0f;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android에서 area가 0인 경우가 있으므로 실제 Unity content view의 가시 프레임을 먼저 잰다.
            // adjustResize로 Unity 영역 자체가 줄어든 경우 겹침이 0이므로 추가로 이동하지 않는다.
            if (!_nativeUnavailable)
            {
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                    using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
                    using (var content = decor.Call<AndroidJavaObject>("findViewById", 16908290))
                    using (var frame = new AndroidJavaObject("android.graphics.Rect"))
                    {
                        decor.Call("getWindowVisibleDisplayFrame", frame);
                        int height = content.Call<int>("getHeight");
                        int[] location = ReadScreenLocation(content);
                        int overlap = Mathf.Max(0, location[1] + height - frame.Get<int>("bottom"));
                        bool keyboardVisible = TouchScreenKeyboard.visible;
                        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        {
                            if (version.GetStatic<int>("SDK_INT") >= 30)
                            {
                                using (var insets = decor.Call<AndroidJavaObject>("getRootWindowInsets"))
                                using (var types = new AndroidJavaClass("android.view.WindowInsets$Type"))
                                    if (insets != null)
                                        keyboardVisible = insets.Call<bool>("isVisible", types.CallStatic<int>("ime"));
                            }
                        }
                        top = height > 0 && keyboardVisible ? overlap * (float)Screen.height / height : 0f;
                        return top > 0f;
                    }
                }
                catch (AndroidJavaException)
                {
                    // 브릿지를 지원하지 않는 환경에서는 Unity가 보고한 실제 area만 사용한다.
                    // 고정 화면 비율을 키보드 높이로 추정하여 다른 버튼까지 밀어 올리지 않는다.
                    _nativeUnavailable = true;
                }
            }
#endif
            Rect area = TouchScreenKeyboard.area;
            if (!TouchScreenKeyboard.visible || area.height <= 0f || area.width <= 0f) return false;
            top = Mathf.Clamp(area.yMax, 0f, Screen.height);
            return top > 0f;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int[] ReadScreenLocation(AndroidJavaObject view)
        {
            // 관리 배열을 Call 인자로 넘기면 JNI가 만든 사본의 변경을 읽지 못한다.
            // Java가 직접 채운 배열에서 좌표를 복사한 뒤 로컬 참조를 반드시 해제한다.
            var array = AndroidJNI.NewIntArray(2);
            try
            {
                var method = AndroidJNI.GetMethodID(view.GetRawClass(), "getLocationOnScreen", "([I)V");
                AndroidJNI.CallVoidMethod(view.GetRawObject(), method, new[] { new jvalue { l = array } });
                return AndroidJNI.FromIntArray(array);
            }
            finally { AndroidJNI.DeleteLocalRef(array); }
        }
#endif
    }
}
