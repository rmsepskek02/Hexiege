// ============================================================================
// CustomJoinView.cs
// 코드 입력으로 게임 참가하는 화면.
//
// 역할:
//   - TMP_InputField → 코드 입력
//   - [참가] → vm.CmdJoinGame.OnNext(코드)
//   - vm.IsConnecting 구독 → 버튼 비활성화
//   - [뒤로] → vm.CmdBack
//   - vm.CurrentScreen 구독 → CustomJoin일 때만 활성화
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
    /// 코드 참가 화면 View.
    /// </summary>
    public class CustomJoinView : MonoBehaviour, IView<BattleViewModel>
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("입력")]
        [Tooltip("로비 코드 입력 필드")]
        [SerializeField] private TMP_InputField _codeInput;

        [Header("버튼")]
        [Tooltip("참가 버튼")]
        [SerializeField] private Button _joinButton;

        [Tooltip("뒤로가기 버튼")]
        [SerializeField] private Button _backButton;

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
                .Select(s => s == BattleViewModel.BattleScreen.CustomJoin)
                .DistinctUntilChanged()
                .Subscribe(visible => gameObject.SetActive(visible))
                .AddTo(_disposables);

            // 연결 상태 → 참가 버튼 비활성화
            vm.IsConnecting
                .Subscribe(connecting =>
                {
                    if (_joinButton != null) _joinButton.interactable = !connecting;
                })
                .AddTo(_disposables);

            // 참가 버튼
            if (_joinButton != null)
                _joinButton.onClick.AddListener(() =>
                {
                    string code = _codeInput != null ? _codeInput.text.Trim() : "";
                    if (!string.IsNullOrEmpty(code))
                        vm.CmdJoinGame.OnNext(code);
                });

            // 뒤로 버튼
            if (_backButton != null)
                _backButton.onClick.AddListener(
                    () => vm.CmdBack.OnNext(Unit.Default));
        }

        /// <summary>
        /// 구독 해제.
        /// </summary>
        public void Unbind()
        {
            _disposables.Clear();

            if (_joinButton != null) _joinButton.onClick.RemoveAllListeners();
            if (_backButton != null) _backButton.onClick.RemoveAllListeners();
        }
    }
}
