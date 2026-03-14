// ============================================================================
// CustomGameView.cs
// 커스텀 게임 모드 선택 화면. "방 만들기" / "코드로 참가" 선택.
//
// 역할:
//   - [방 만들기] → vm.CmdStartHosting
//   - [코드로 참가] → vm.CurrentScreen = CustomJoin
//   - [뒤로] → vm.CmdBack
//   - vm.CurrentScreen 구독 → CustomGame일 때만 활성화
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UniRx;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 커스텀 게임 선택 화면 View.
    /// </summary>
    public class CustomGameView : MonoBehaviour, IView<BattleViewModel>
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("버튼")]
        [Tooltip("방 만들기 버튼")]
        [SerializeField] private Button _createRoomButton;

        [Tooltip("코드로 참가 버튼")]
        [SerializeField] private Button _joinByCodeButton;

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
                .Select(s => s == BattleViewModel.BattleScreen.CustomGame)
                .DistinctUntilChanged()
                .Subscribe(visible => gameObject.SetActive(visible))
                .AddTo(_disposables);

            // 버튼 → 커맨드
            if (_createRoomButton != null)
                _createRoomButton.onClick.AddListener(
                    () => vm.CmdStartHosting.OnNext(Unit.Default));

            if (_joinByCodeButton != null)
                _joinByCodeButton.onClick.AddListener(
                    () => vm.CurrentScreen.Value = BattleViewModel.BattleScreen.CustomJoin);

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

            if (_createRoomButton != null) _createRoomButton.onClick.RemoveAllListeners();
            if (_joinByCodeButton != null) _joinByCodeButton.onClick.RemoveAllListeners();
            if (_backButton != null) _backButton.onClick.RemoveAllListeners();
        }
    }
}
