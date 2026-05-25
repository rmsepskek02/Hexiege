// ============================================================================
// CustomHostView.cs
// 커스텀 호스트 대기 화면. 로비 코드 표시 + 연결 플레이어 수 + 취소.
//
// 역할:
//   - vm.LobbyCode 구독 → 코드 텍스트 갱신
//   - vm.ConnectedPlayers 구독 → "n/2명 연결됨" 표시
//   - vm.IsConnecting 구독 → 로딩 상태 표시
//   - vm.ErrorMessage 구독 → 에러 텍스트 표시
//   - [취소] → vm.CmdCancelHosting
//   - vm.CurrentScreen 구독 → CustomHost일 때만 활성화
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 커스텀 호스트 대기 화면 View.
    /// </summary>
    public class CustomHostView : MonoBehaviour, IView<BattleViewModel>
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("텍스트")]
        [Tooltip("로비 코드 표시 텍스트")]
        [SerializeField] private TextMeshProUGUI _codeText;

        [Tooltip("연결된 플레이어 수 텍스트")]
        [SerializeField] private TextMeshProUGUI _connectedPlayersText;

        [Tooltip("상태/로딩 텍스트")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Tooltip("에러 메시지 텍스트")]
        [SerializeField] private TextMeshProUGUI _errorText;

        [Header("버튼")]
        [Tooltip("취소 버튼")]
        [SerializeField] private Button _cancelButton;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private readonly CompositeDisposable _disposables = new();

        // 본 View 자신의 GameObject에 부착된 CanvasGroup 캐시.
        // 공통 UI 규칙 Rule 5: SetActive 대신 CanvasGroup으로 표시/숨김 처리.
        private CanvasGroup _canvasGroup;

        // 에러 텍스트 GameObject에 부착된 CanvasGroup 캐시.
        // _errorText는 다른 상태 텍스트들과 함께 같은 부모(LayoutGroup) 안에 배치되는 경우가 많아,
        // SetActive로 숨기면 다른 텍스트가 위로 밀려올라가는 레이아웃 흔들림이 생긴다.
        // CanvasGroup.alpha=0으로 숨기면 공간은 유지된다.
        private CanvasGroup _errorTextGroup;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // 자신의 CanvasGroup 확보.
            if (!TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 에러 텍스트의 CanvasGroup 확보 (있으면 사용, 없으면 추가).
            if (_errorText != null)
            {
                if (!_errorText.TryGetComponent(out _errorTextGroup))
                    _errorTextGroup = _errorText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // ====================================================================
        // IView 구현
        // ====================================================================

        /// <summary>
        /// BattleViewModel에 바인딩.
        /// </summary>
        public void Bind(BattleViewModel vm)
        {
            Unbind();

            // 화면 활성화 관리 — CustomHost일 때만 표시
            // Rule 5: SetActive 대신 CanvasGroup의 alpha/blocksRaycasts/interactable 전환.
            vm.CurrentScreen
                .Select(s => s == BattleViewModel.BattleScreen.CustomHost)
                .DistinctUntilChanged()
                .Subscribe(SetVisible)
                .AddTo(_disposables);

            // 로비 코드 표시
            vm.LobbyCode
                .Subscribe(code =>
                {
                    if (_codeText != null)
                        _codeText.text = string.IsNullOrEmpty(code) ? "---" : code;
                })
                .AddTo(_disposables);

            // 연결된 플레이어 수 표시
            vm.ConnectedPlayers
                .Subscribe(count =>
                {
                    if (_connectedPlayersText != null)
                        _connectedPlayersText.text = $"{count}/2명 연결됨";
                })
                .AddTo(_disposables);

            // 연결 상태 표시
            vm.IsConnecting
                .Subscribe(connecting =>
                {
                    if (_statusText != null)
                        _statusText.text = connecting ? "방 생성 중..." : "상대방 접속 대기 중...";
                })
                .AddTo(_disposables);

            // 에러 메시지 표시
            // 빈 메시지일 때는 CanvasGroup.alpha=0으로 숨겨 공간만 유지(레이아웃 흔들림 방지).
            vm.ErrorMessage
                .Subscribe(msg =>
                {
                    if (_errorText != null)
                        _errorText.text = msg;

                    SetErrorTextVisible(!string.IsNullOrEmpty(msg));
                })
                .AddTo(_disposables);

            // 취소 버튼
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(
                    () => vm.CmdCancelHosting.OnNext(Unit.Default));
        }

        /// <summary>
        /// 구독 해제.
        /// </summary>
        public void Unbind()
        {
            _disposables.Clear();

            if (_cancelButton != null) _cancelButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// View 표시/숨김 처리 (CanvasGroup 기반).
        /// 공통 UI 규칙 Rule 5: SetActive 대신 CanvasGroup으로 처리해
        /// LayoutGroup 안에서 공간이 사라지는 레이아웃 깨짐을 방지한다.
        /// </summary>
        /// <param name="visible">true=표시, false=숨김.</param>
        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }

        /// <summary>
        /// 에러 텍스트 표시/숨김 처리 (CanvasGroup 기반).
        /// 에러 텍스트가 부모 LayoutGroup 안에 있더라도 공간이 유지되어
        /// 다른 텍스트(코드/상태 등)가 위로 밀려올라가지 않는다.
        /// </summary>
        /// <param name="visible">true=표시, false=숨김.</param>
        private void SetErrorTextVisible(bool visible)
        {
            if (_errorTextGroup == null)
                return;

            _errorTextGroup.alpha = visible ? 1f : 0f;
            _errorTextGroup.blocksRaycasts = visible;
            _errorTextGroup.interactable = visible;
        }
    }
}
