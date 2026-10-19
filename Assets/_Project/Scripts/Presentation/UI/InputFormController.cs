using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    /// <summary>기존 화면의 입력 참조를 사용해 편집, 이동, 오류 및 제출 상태를 관리한다.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class InputFormController : MonoBehaviour
    {
        [Header("키보드 회피 영역")]
        [Tooltip("지정하면 고정 헤더를 제외한 이 본문만 이동한다. 비워 둔 기존 폼은 폼 전체를 사용한다.")]
        [SerializeField] private RectTransform _keyboardContent;
        [Tooltip("본문과 함께 이동하는 오류 안내 영역. 표시 중일 때 가능한 범위에서 함께 노출한다.")]
        [SerializeField] private RectTransform _keyboardFeedback;
        [Tooltip("로그인·회원가입 전용: 화면의 입력 표시/검증은 유지하고 native 연결만 공통 보안 모드로 재사용한다.")]
        [SerializeField] private bool _useSharedSecureKeyboard;
        private bool _configured;
        private TMP_InputField[] _fields = Array.Empty<TMP_InputField>();
        private UnityAction<string>[] _changes;
        private UnityAction<string>[] _submits;
        private ColorBlock[] _colors;
        private bool[] _errors;
        private Button _submitButton;
        private Action _changed;
        private Func<bool> _valid;
        private Keyboard _keyboard;
        private bool _composing;
        private int _compositionFrame = -10;
        private int _pending = -1;
        private int _queuedFrame;
        private bool _open = true;
        private bool _busy;
        private Action _validationFeedback;
        private bool _validationPending;
        private CanvasGroup[] _groups = Array.Empty<CanvasGroup>();
        private bool _lastReady;
        private bool[] _appliedErrors;
        private bool _colorsInitialized;
        private int _session;
        private bool _wasVisible;
        private MobileKeyboardAvoidance _keyboardAvoidance;
        private InputFormPointerRelay[] _pointerRelays = Array.Empty<InputFormPointerRelay>();
        private bool _focusCleanupPending;
#if UNITY_ANDROID && !UNITY_EDITOR
        private bool _applicationPaused;
        private static int s_nativeTouchPolicyLeases;
        private static bool s_originalConsumesOutsideTouches;
        private bool _nativeTouchPolicyHeld;
        private TMP_InputField _nativeEditingField;
        private TouchScreenKeyboard _nativeKeyboard;
        private string _nativeGestureText;
        private int _nativeEditingFrame = -10;
        private readonly List<TouchScreenKeyboard> _retiredKeyboards = new List<TouchScreenKeyboard>();
        private ContinuousInputField _transferTarget;
        private int _transferFrame = -10;
        private TMP_InputField _requestedTransferSource;
        private TMP_InputField _requestedTransferTarget;
        private bool _nativeGapObserved;
        private TouchScreenKeyboard _sharedSecureKeyboard;
#endif

        /// <summary>처리 중 화면 뒤로가기도 중복 실행과 같은 경계로 차단한다.</summary>
        public bool IsBusy => _busy;
        /// <summary>화면을 닫고 다시 연 이전 응답을 구별할 식별자다.</summary>
        public int Session => _session;
        /// <summary>현재 표시 회차에서 시작한 요청의 응답인지 확인한다.</summary>
        public bool IsCurrent(int session) => session == _session && IsVisible;

        /// <summary>현재 화면에서 요청을 새로 시작할 수 있는지 반환한다.</summary>
        // 이전 조건: IsVisible && !_busy && !IsComposing && HasRequiredValues();
        // 조합 중 여부는 Enter 오제출만 막는다. 버튼은 키보드를 내리지 않아도 사용할 수 있다.
        public bool CanSubmit => IsVisible && !_busy && HasRequiredValues();
        /// <summary>현재 입력기의 조합 중 여부와 확정 프레임을 함께 확인한다.</summary>
        public bool IsComposing => _composing || Time.frameCount <= _compositionFrame + 1;
        /// <summary>명시적 열림 상태와 부모 CanvasGroup의 실제 표시 상태를 확인한다.</summary>
        public bool IsVisible
        {
            get
            {
                // 씬에 미리 연결된 컴포넌트는 View 초기화 전에도 OnEnable/Update를 받을 수 있다.
                // 입력 참조가 연결되기 전에는 빈 부모 목록을 실제 표시 상태로 오인하지 않는다.
                if (!_configured || !_open || !isActiveAndEnabled) return false;
                foreach (var group in _groups)
                    if (group != null && group.alpha <= 0f) return false;
                return true;
            }
        }

        /// <summary>씬에 이미 연결된 필드 순서와 실행 버튼을 공통 입력 처리에 연결한다.</summary>
        public static InputFormController Attach(MonoBehaviour owner, Button submit, Action changed,
            Func<bool> valid, params TMP_InputField[] fields)
        {
            var form = owner.GetComponent<InputFormController>();
            if (form == null) form = owner.gameObject.AddComponent<InputFormController>();
            form.Configure(submit, changed, valid, fields);
            return form;
        }

        private void Configure(Button submit, Action changed, Func<bool> valid, TMP_InputField[] fields)
        {
            Detach();
            _pending = -1;
            _busy = false;
            _validationPending = false;
            _validationFeedback = null;
            _colorsInitialized = false;
            _groups = GetComponentsInParent<CanvasGroup>(true);
            _submitButton = submit;
            _changed = changed;
            _valid = valid;
            _fields = fields;
            _keyboardAvoidance = new MobileKeyboardAvoidance(
                _keyboardContent != null ? _keyboardContent : transform as RectTransform,
                submit, fields, _keyboardFeedback);
            var buttons = GetComponentsInChildren<Button>(true);
            _pointerRelays = new InputFormPointerRelay[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
                _pointerRelays[i] = InputFormPointerRelay.Attach(this, buttons[i]);
            _changes = new UnityAction<string>[fields.Length];
            _submits = new UnityAction<string>[fields.Length];
            _colors = new ColorBlock[fields.Length];
            _errors = new bool[fields.Length];
            _appliedErrors = new bool[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                int index = i;
                var field = fields[i];
                if (field == null) continue;
                if (field is ContinuousInputField continuous) continuous.BindForm(this);
                // 직렬화된 값도 실제 인스턴스에서 교정한다. TMP의 직접 선택·드래그는 유지한다.
                field.onFocusSelectAll = false;
                field.lineType = TMP_InputField.LineType.SingleLine;
                // Android의 별도 입력 창 대신 게임 화면 안에서 편집한다.
                // TMP는 이 설정이 켜져 있어야 모바일에서도 커서와 터치 드래그를 처리한다.
                // 소프트 키보드 자체는 숨기지 않으므로 기존 문자 입력은 그대로 유지된다.
                field.shouldHideMobileInput = true;
                field.caretWidth = 3;
                field.customCaretColor = true;
                field.caretColor = new Color(0.1f, 0.45f, 1f, 1f);
                _colors[i] = field.colors;
                _changes[i] = value => OnChanged(index);
                _submits[i] = value => QueueSubmit(index);
                field.onValueChanged.AddListener(_changes[i]);
                field.onSubmit.AddListener(_submits[i]);
            }
            _configured = true;
            Refresh();
            RefreshNativeTouchPolicy();
        }

        private void OnEnable()
        {
            _open = true;
            TrackKeyboard();
        }

        private void TrackKeyboard()
        {
            if (_keyboard == Keyboard.current) return;
            if (_keyboard != null) _keyboard.onIMECompositionChange -= OnComposition;
            _keyboard = Keyboard.current;
            _composing = false;
            if (_keyboard != null) _keyboard.onIMECompositionChange += OnComposition;
        }

        private void OnComposition(IMECompositionString composition)
        {
            // 키보드 이벤트는 전역이므로 다른 화면에서 시작한 조합을 가져오지 않는다.
            if (!HasEditingField()) return;
            // 빈 문자열로 바뀌는 확정 이벤트도 보호하여 같은 Enter가 제출되지 않게 한다.
            _composing = composition.Count > 0;
            _compositionFrame = Time.frameCount;
        }

        private void Update()
        {
            if (!_configured) return;
            TrackKeyboard();
            RefreshNativeTouchPolicy();
            RememberNativeEditingField();
            // 일부 모바일 IME는 키보드를 닫을 때 빈 조합 종료 이벤트를 보내지 않는다.
            // 실제 편집이 끝났으면 조합 잠금도 해제해야 입력한 폼이 영구 잠기지 않는다.
            if (_composing && (!HasEditingField() ||
                (TouchScreenKeyboard.isSupported && !TouchScreenKeyboard.visible)))
                _composing = false;
            bool visible = IsVisible;
            if (_wasVisible && !visible) ReleaseFocus();
            _wasVisible = visible;
            if (CanSubmit != _lastReady) Refresh();
            if (!IsVisible || _busy || IsComposing || _keyboard == null) return;
            if (!_keyboard.tabKey.wasPressedThisFrame) return;
            int current = SelectedIndex();
            if (current >= 0) FocusNext(current, _keyboard.shiftKey.isPressed ? -1 : 1);
        }

        private int SelectedIndex()
        {
            if (EventSystem.current == null) return -1;
            for (int i = 0; i < _fields.Length; i++)
                if (_fields[i] != null && EventSystem.current.currentSelectedGameObject == _fields[i].gameObject)
                    return i;
            return -1;
        }

        private bool HasEditingField()
        {
            foreach (var field in _fields)
                if (field != null && field.isFocused) return true;
            return false;
        }

        private void QueueSubmit(int index)
        {
            if (!IsVisible || _busy || IsComposing || !CanFocus(_fields[index])) return;
            _pending = index;
            _queuedFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            // Android는 키보드 표시·숨김에도 focus 변경을 보낼 수 있다. EventSystem이 같은
            // 프레임에 보낸 실제 PointerDown을 확인한 뒤에만 이전 native 캐시를 버린다.
            if (_focusCleanupPending)
            {
                _focusCleanupPending = false;
                if (_keyboardAvoidance == null || !_keyboardAvoidance.HasPointerGesture)
                    ForgetNativeEditingField();
            }
            bool transferring = IsKeyboardTransferPending();
            EndNativeEditingIfIdle(transferring);
            _keyboardAvoidance?.Update(IsVisible && !_busy, transferring);
            if (_validationPending && !IsComposing && IsVisible && !_busy)
            {
                _validationPending = false;
                _validationFeedback?.Invoke();
            }
            // TMP가 현재 필드의 제출 후 비활성화를 끝낸 다음 프레임에 다음 필드를 선택한다.
            if (_pending < 0 || Time.frameCount <= _queuedFrame) return;
            int index = _pending;
            _pending = -1;
            if (!IsVisible || _busy || IsComposing) return;
            if (index < _fields.Length - 1) FocusNext(index, 1);
            else if (CanSubmit && _submitButton != null && _submitButton.IsInteractable())
                _submitButton.onClick.Invoke();
        }

        private void FocusNext(int current, int direction)
        {
            for (int step = 1; step <= _fields.Length; step++)
            {
                int index = (current + direction * step + _fields.Length) % _fields.Length;
                var field = _fields[index];
                if (!CanFocus(field)) continue;
#if UNITY_ANDROID && !UNITY_EDITOR
                _requestedTransferSource = _fields[current];
                _requestedTransferTarget = field;
                try
                {
#endif
                field.Select();
                field.ActivateInputField();
#if UNITY_ANDROID && !UNITY_EDITOR
                }
                finally { _requestedTransferSource = _requestedTransferTarget = null; }
#endif
                return;
            }
        }

        private static bool CanFocus(TMP_InputField field) =>
            field != null && field.isActiveAndEnabled && field.IsInteractable();

        private bool HasRequiredValues()
        {
            if (_submitButton == null || !_submitButton.IsActive()) return false;
            foreach (var field in _fields)
            {
                if (!CanFocus(field)) return false;
                bool password = field.inputType == TMP_InputField.InputType.Password;
                if (password ? string.IsNullOrEmpty(field.text) : string.IsNullOrWhiteSpace(field.text)) return false;
            }
            return _fields.Length > 0;
        }

        private void OnChanged(int index)
        {
            _errors[index] = false;
            _validationPending = true;
            _changed?.Invoke();
            Refresh();
        }

        /// <summary>버튼 요청 직전에 Android 입력기의 마지막 글자를 반영하고 제출 상태를 재검사한다.</summary>
        public bool PrepareSubmit()
        {
            if (!IsVisible || _busy) return false;
            TracePointer("submit", _submitButton, -1);
#if UNITY_ANDROID && !UNITY_EDITOR
            // TMP는 native keyboard.text를 LateUpdate에서 읽지만 버튼 이벤트가 먼저 올 수 있다.
            // 포커스 해제 시 TMP가 참조를 버리므로 직전 편집 또는 같은 버튼 누름 회차만 보관한다.
            // 문자 검증이 있는 필드는 TMP 검증을 우회하지 않는다. 비밀번호/닉네임의 기존
            // 문자 무제한 입력에서만 마지막 조합 글자를 동기화하고 기존 길이 제한은 유지한다.
            if (_nativeEditingField != null && _nativeKeyboard != null &&
                (Time.frameCount <= _nativeEditingFrame + 1 || _keyboardAvoidance.IsSubmitGesture) &&
                CanFocus(_nativeEditingField) &&
                _nativeKeyboard.status != TouchScreenKeyboard.Status.Canceled &&
                (_nativeKeyboard.status != TouchScreenKeyboard.Status.LostFocus ||
                 (_keyboardAvoidance.IsSubmitGesture && _nativeGestureText != null)) &&
                _nativeEditingField.characterValidation == TMP_InputField.CharacterValidation.None &&
                _nativeEditingField.onValidateInput == null)
            {
                // 이 버튼을 누르기 직전에 읽은 문자열은 포커스 해제로 LostFocus가 되어도 유효하다.
                // 다른 화면/단순 포커스 이탈의 LostFocus 문자열을 다시 가져오지는 않는다.
                string latest = _nativeKeyboard.status == TouchScreenKeyboard.Status.LostFocus
                    ? _nativeGestureText : _nativeKeyboard.text;
                if (latest != null)
                {
                    int limit = _nativeEditingField.characterLimit;
                    if (limit > 0 && latest.Length > limit) latest = latest.Substring(0, limit);
                    _nativeEditingField.text = latest;
                }
            }
#endif
            ForgetNativeEditingField();
            Refresh();
            return CanSubmit && (_valid == null || _valid());
        }

        private void RememberNativeEditingField()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var field in _fields)
            {
                if (field == null || !field.isFocused || field.shouldHideSoftKeyboard) continue;
                if (field.touchScreenKeyboard == null) continue;
                _nativeEditingField = field;
                _nativeKeyboard = field.touchScreenKeyboard;
                _nativeEditingFrame = Time.frameCount;
                // PointerDown보다 먼저 TMP.OnDeselect가 키보드 참조를 버릴 수 있으므로,
                // 편집 프레임의 문자열을 잠깐 보관한다. 실제 같은 제출 제스처에서만 소비한다.
                _nativeGestureText = _nativeKeyboard.status == TouchScreenKeyboard.Status.Visible ||
                    _nativeKeyboard.status == TouchScreenKeyboard.Status.Done ? _nativeKeyboard.text : null;
                return;
            }
            if (Time.frameCount > _nativeEditingFrame + 1 && !_keyboardAvoidance.IsSubmitGesture)
                ForgetNativeEditingField();
#endif
        }

        private void ForgetNativeEditingField()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _nativeEditingField = null;
            _nativeKeyboard = null;
            _nativeGestureText = null;
            _nativeEditingFrame = -10;
#endif
        }

        /// <summary>조합이 끝난 입력에 대한 기존 검증 안내를 등록한다.</summary>
        public void SetValidationFeedback(Action feedback) => _validationFeedback = feedback;

        /// <summary>지정 필드의 검증 오류를 기존 화면 색상과 구분하여 표시한다.</summary>
        public void SetError(TMP_InputField field, bool error = true)
        {
            for (int i = 0; i < _fields.Length; i++)
                if (_fields[i] == field) _errors[i] = error;
            Refresh();
        }

        /// <summary>요청 중에는 완료 입력과 버튼의 실행을 함께 막는다.</summary>
        public void SetBusy(bool busy)
        {
            _busy = busy;
            if (busy) _pending = -1;
            Refresh();
        }

        /// <summary>필수값·기존 검증·표시·처리 상태를 기준으로 실행 버튼을 갱신한다.</summary>
        public void Refresh()
        {
            _lastReady = CanSubmit;
            if (_submitButton != null)
                _submitButton.interactable = _lastReady && (_valid == null || _valid());
            for (int i = 0; i < _fields.Length; i++)
            {
                if (_fields[i] == null) continue;
                if (_colorsInitialized && _appliedErrors[i] == _errors[i]) continue;
                var colors = _colors[i];
                colors.selectedColor = Color.Lerp(colors.normalColor, new Color(0.65f, 0.8f, 1f), 0.4f);
                if (_errors[i])
                    colors.normalColor = colors.highlightedColor = colors.selectedColor = new Color(1f, 0.65f, 0.65f);
                _fields[i].colors = colors;
                _appliedErrors[i] = _errors[i];
            }
            _colorsInitialized = true;
        }

        /// <summary>화면 표시 시 입력 상태만 재평가하며 기존 입력값은 보존한다.</summary>
        public void Open()
        {
            _open = true;
            if (_errors != null) Array.Clear(_errors, 0, _errors.Length);
            _changed?.Invoke();
            Refresh();
            RefreshNativeTouchPolicy();
        }

        /// <summary>화면을 완전히 닫을 때 포커스·키보드와 비밀번호만 정리한다.</summary>
        public void Close()
        {
            _open = false;
            _session++;
            _pending = -1;
            _validationPending = false;
            ReleaseFocus();
            foreach (var field in _fields)
            {
                if (field == null) continue;
                if (field.inputType == TMP_InputField.InputType.Password) field.text = string.Empty;
            }
            Refresh();
        }

        private void ReleaseFocus()
        {
            ReleaseNativeTouchPolicy();
            _focusCleanupPending = false;
            _keyboardAvoidance?.Reset();
            ForgetNativeEditingField();
            _pending = -1;
            _composing = false;
            foreach (var field in _fields)
            {
                if (field == null) continue;
                field.DeactivateInputField();
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == field.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            DrainRetiredKeyboards();
        }

        private void OnDisable()
        {
            Close();
            if (_keyboard != null) _keyboard.onIMECompositionChange -= OnComposition;
            _keyboard = null;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            // 이전 즉시 Reset은 키보드 포커스 통지가 PointerDown 전후에 올 때 버튼을 옮겼다.
            // 입력값과 위치를 여기서 바꾸지 않고 같은 프레임의 실제 누름 확인까지 정리를 미룬다.
            _pending = -1;
            _focusCleanupPending = true;
            TracePointer("focus-lost", null, -1);
        }

        private void OnApplicationPause(bool paused)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _applicationPaused = paused;
#endif
            RefreshNativeTouchPolicy();
            if (!paused) return;
            _pending = -1;
            _focusCleanupPending = false;
            ReleaseFocus();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        internal bool UsesSharedSecureKeyboard(ContinuousInputField field) =>
            _useSharedSecureKeyboard && IsVisible && !_busy && !_applicationPaused && field != null &&
            Array.IndexOf(_fields, field) >= 0 && CanFocus(field) && !field.readOnly &&
            field.textComponent != null && !field.shouldHideSoftKeyboard &&
            !TouchScreenKeyboard.isInPlaceEditingAllowed;

        internal TouchScreenKeyboard AcquireSharedSecureKeyboard(ContinuousInputField field)
        {
            if (!UsesSharedSecureKeyboard(field)) return null;
            if (_sharedSecureKeyboard != null && (_transferTarget != field ||
                _sharedSecureKeyboard.status == TouchScreenKeyboard.Status.Done ||
                _sharedSecureKeyboard.status == TouchScreenKeyboard.Status.Canceled))
            {
                // 같은 폼의 명시적인 전환 외에는 이전 편집 회차의 연결을 재사용하지 않는다.
                // 닫힘/취소 직후 같은 프레임에 다시 선택해도 새 세션으로 시작한다.
                if (_sharedSecureKeyboard.active) _sharedSecureKeyboard.active = false;
                _sharedSecureKeyboard = null;
            }
            if (_sharedSecureKeyboard == null)
            {
                // 이메일도 native 연결은 처음부터 보안 모드다. 화면의 평문 표시와 기존
                // 이메일 검증은 TMP 필드가 담당하며 native 자동교정은 사용하지 않는다.
                int session = _session;
                var keyboard = TouchScreenKeyboard.Open(field.text,
                    TouchScreenKeyboardType.Default, false, false, true, false, "", field.characterLimit);
                // Open은 native 이벤트를 처리하는 동안 화면 닫기/일시정지에 재진입할 수 있다.
                // 반환된 연결을 현재 표시 회차가 계속 소유할 때만 보관한다.
                if (!IsCurrent(session) || !UsesSharedSecureKeyboard(field) || !field.isFocused ||
                    EventSystem.current == null || EventSystem.current.currentSelectedGameObject != field.gameObject)
                {
                    if (keyboard != null && keyboard.active) keyboard.active = false;
                    return null;
                }
                _sharedSecureKeyboard = keyboard;
            }
            else
            {
                // 다른 필드의 원문을 연결하기 전에 먼저 현재 필드 값으로 교체한다.
                // 타입/보안을 바꾸거나 Open을 다시 호출하지 않는다.
                _sharedSecureKeyboard.characterLimit = field.characterLimit;
                _sharedSecureKeyboard.text = field.text;
            }
            // 텍스트 교체도 native 입력 재시작을 거칠 수 있으므로 반환 직전 소유권을 재검사한다.
            return UsesSharedSecureKeyboard(field) && field.isFocused && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == field.gameObject ? _sharedSecureKeyboard : null;
        }

        internal ContinuousInputField ResolveTransferTarget(ContinuousInputField source, BaseEventData eventData)
        {
            ContinuousInputField target = null;
            if (_requestedTransferSource == source)
                target = _requestedTransferTarget as ContinuousInputField;
            else if (eventData is PointerEventData pointer && pointer.pointerCurrentRaycast.gameObject != null)
                target = pointer.pointerCurrentRaycast.gameObject.GetComponentInParent<ContinuousInputField>();
            return CanTransfer(source, target) ? target : null;
        }

        private bool CanTransfer(ContinuousInputField source, ContinuousInputField target) =>
            IsVisible && !_busy && target != null && source != target && CanFocus(target) &&
            Array.IndexOf(_fields, source) >= 0 && Array.IndexOf(_fields, target) >= 0 &&
            (!_useSharedSecureKeyboard || (UsesSharedSecureKeyboard(source) && UsesSharedSecureKeyboard(target))) &&
            !source.shouldHideSoftKeyboard && !target.shouldHideSoftKeyboard &&
            !TouchScreenKeyboard.isInPlaceEditingAllowed;

        internal bool RetainKeyboardForTransfer(ContinuousInputField source, ContinuousInputField target,
            TouchScreenKeyboard keyboard)
        {
            if (!CanTransfer(source, target) || !keyboard.active ||
                keyboard.status != TouchScreenKeyboard.Status.Visible) return false;
            if (_useSharedSecureKeyboard && keyboard != _sharedSecureKeyboard) return false;
            if (!_useSharedSecureKeyboard) _retiredKeyboards.Add(keyboard);
            _transferTarget = target;
            _transferFrame = Time.frameCount;
            _nativeGapObserved = false;
            TraceKeyboardTransfer("retained", source, target, keyboard);
            ForgetNativeEditingField();
            return true;
        }

        internal void CompleteKeyboardTransfer(ContinuousInputField target)
        {
            if (_transferTarget != target) return;
            TraceKeyboardTransfer("target-activated", null, target, target.touchScreenKeyboard);
            _transferTarget = null;
        }
#endif

        private bool IsKeyboardTransferPending()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return _transferTarget != null && IsVisible && !_busy &&
                EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _transferTarget.gameObject &&
                Time.frameCount <= _transferFrame + 1 && !_transferTarget.isFocused;
#else
            return false;
#endif
        }

        private void EndNativeEditingIfIdle(bool transferring)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (transferring || (_retiredKeyboards.Count == 0 && _sharedSecureKeyboard == null)) return;
            foreach (var field in _fields)
            {
                if (field == null || !field.isFocused || field.touchScreenKeyboard == null) continue;
                var status = field.touchScreenKeyboard.status;
                // 이메일↔보안 입력 재설정 중 active/visible이 잠깐 내려갈 수 있다. 이때 이전
                // wrapper를 끄면 방금 시작한 새 native 연결까지 Hide할 수 있으므로, 실제
                // 편집 연결이 남아 있는 비종료 상태는 세션 종료로 판단하지 않는다.
                // Done/Canceled는 명시적인 종료이며 null 실패·미활성 timeout도 아래에서 정리한다.
                if (status != TouchScreenKeyboard.Status.Done && status != TouchScreenKeyboard.Status.Canceled)
                {
                    if (!field.touchScreenKeyboard.active && !_nativeGapObserved)
                    {
                        _nativeGapObserved = true;
                        TraceKeyboardTransfer("nonterminal-gap-preserved", null, field, field.touchScreenKeyboard);
                    }
                    return;
                }
                // TMP는 Canceled 수신만으로 isFocused를 끄지 않는다. 공통 연결의 소유권을
                // 해제할 때 필드 참조도 함께 정리해야 다음 탭이 버려진 연결을 다시 켜지 않는다.
                // 기존 TMP의 취소 시 원문 복구 정책은 DeactivateInputField가 그대로 처리한다.
                if (_useSharedSecureKeyboard && field.touchScreenKeyboard == _sharedSecureKeyboard)
                    field.DeactivateInputField();
            }
            DrainRetiredKeyboards();
#endif
        }

        private void DrainRetiredKeyboards()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _transferTarget = null;
            _requestedTransferSource = _requestedTransferTarget = null;
            // 현재 편집이 먼저 닫힌 뒤 이전 wrapper도 표준 TMP처럼 inactive 상태로 만든다.
            // 활성 전환 중에는 이전 wrapper를 해제하지 않아 새 키보드 설정을 건드리지 않는다.
            if (_retiredKeyboards.Count > 0) TraceKeyboardTransfer("retired-drain", null, null, null);
            foreach (var keyboard in _retiredKeyboards) keyboard.active = false;
            _retiredKeyboards.Clear();
            // 일반 TMP가 현재 연결을 먼저 닫았다면 중복 Hide를 보내지 않는다.
            if (_sharedSecureKeyboard != null && _sharedSecureKeyboard.active)
                _sharedSecureKeyboard.active = false;
            _sharedSecureKeyboard = null;
#endif
        }

        private void RefreshNativeTouchPolicy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsVisible || _applicationPaused) { ReleaseNativeTouchPolicy(); return; }
            if (_nativeTouchPolicyHeld) return;
            if (s_nativeTouchPolicyLeases == 0)
            {
                // Unity의 Android 키보드가 외부 첫 탭을 소비하는 동작만 끈다.
                // 입력란 전환의 TMP native Open/Close를 공유하는 기능은 아니다.
                s_originalConsumesOutsideTouches = TouchScreenKeyboard.Android.consumesOutsideTouches;
                TouchScreenKeyboard.Android.consumesOutsideTouches = false;
            }
            s_nativeTouchPolicyLeases++;
            _nativeTouchPolicyHeld = true;
#endif
        }

        private void ReleaseNativeTouchPolicy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_nativeTouchPolicyHeld) return;
            _nativeTouchPolicyHeld = false;
            if (--s_nativeTouchPolicyLeases > 0) return;
            // 기본값을 추정하지 않고 첫 폼이 들어오기 직전에 읽은 실제 설정으로 복원한다.
            TouchScreenKeyboard.Android.consumesOutsideTouches = s_originalConsumesOutsideTouches;
#endif
        }

        internal void BeginButtonPointer(Button button, int pointerId)
        {
            if (!IsVisible || _busy || button == null || !button.IsActive()) return;
            if (_keyboardAvoidance == null || !_keyboardAvoidance.BeginPointer(button, pointerId)) return;
            TracePointer("down", button, pointerId);
        }

        internal void EndButtonPointer(Button button, int pointerId)
        {
            _keyboardAvoidance?.EndPointer(button, pointerId);
            TracePointer("up", button, pointerId);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void TracePointer(string phase, Button button, int pointerId)
        {
            // 입력 문자열·계정·방 코드는 기록하지 않는다. 실제 Unity 이벤트 도달 범위만 구별한다.
            GameLog.Dev.Info("UI", nameof(InputFormController), "입력 폼 포인터 진단",
                $"schema=input-form-pointer-v2 phase={phase} form={name} button={(button != null ? button.name : "none")} pointer={pointerId} frame={Time.frameCount} keyboardVisible={TouchScreenKeyboard.visible}");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void TraceKeyboardTransfer(string phase, TMP_InputField source, TMP_InputField target,
            TouchScreenKeyboard keyboard)
        {
            // 모드 전환과 세션 종료를 구별하는 개발 진단이다. 입력 본문·길이·계정은 기록하지 않는다.
            string from = source != null ? $"{source.keyboardType}/{source.inputType}" : "none";
            string to = target != null ? $"{target.keyboardType}/{target.inputType}" : "none";
            string native = keyboard != null ? $"{keyboard.status}/{keyboard.active}" : "none";
            GameLog.Dev.Info("UI", nameof(InputFormController), "입력 모드 전환 진단",
                $"schema=input-form-transfer-v3 phase={phase} from={from} to={to} native={native} visible={TouchScreenKeyboard.visible} frame={Time.frameCount} retained={_retiredKeyboards.Count}");
        }
#endif

        private void OnDestroy() => Detach();

        private void Detach()
        {
            _configured = false;
            // 재연결에서도 현재 native 편집을 먼저 닫은 뒤 보관 연결을 표준 해제 상태로 만든다.
            ReleaseFocus();
            foreach (var relay in _pointerRelays)
                if (relay != null) relay.Detach(this);
            _pointerRelays = Array.Empty<InputFormPointerRelay>();
            for (int i = 0; i < _fields.Length; i++)
            {
                if (_fields[i] == null) continue;
                if (_fields[i] is ContinuousInputField continuous) continuous.BindForm(null);
                _fields[i].onValueChanged.RemoveListener(_changes[i]);
                _fields[i].onSubmit.RemoveListener(_submits[i]);
                _fields[i].colors = _colors[i];
            }
        }
    }
}
