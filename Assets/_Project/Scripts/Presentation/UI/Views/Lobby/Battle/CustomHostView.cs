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

        // ====================================================================
        // IView 구현
        // ====================================================================

        /// <summary>
        /// BattleViewModel에 바인딩.
        /// </summary>
        public void Bind(BattleViewModel vm)
        {
            Unbind();

            // 화면 활성화 관리
            vm.CurrentScreen
                .Select(s => s == BattleViewModel.BattleScreen.CustomHost)
                .DistinctUntilChanged()
                .Subscribe(visible => gameObject.SetActive(visible))
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
            vm.ErrorMessage
                .Subscribe(msg =>
                {
                    if (_errorText != null)
                    {
                        _errorText.text = msg;
                        _errorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
                    }
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
    }
}
