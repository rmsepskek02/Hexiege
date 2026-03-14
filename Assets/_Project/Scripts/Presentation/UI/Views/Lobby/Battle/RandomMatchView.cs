// ============================================================================
// RandomMatchView.cs
// 랜덤 매칭 대기 화면. 매칭 스피너 표시 + 취소.
//
// 역할:
//   - vm.IsMatchmaking 구독 → 스피너/대기 텍스트 표시
//   - [취소] → vm.CmdCancelMatchmaking
//   - vm.CurrentScreen 구독 → RandomMatch일 때만 활성화
//   - (현재 플레이스홀더, 실제 Matchmaker 연동은 추후)
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
    /// 랜덤 매칭 대기 화면 View. (현재 플레이스홀더)
    /// </summary>
    public class RandomMatchView : MonoBehaviour, IView<BattleViewModel>
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("텍스트")]
        [Tooltip("매칭 상태 텍스트")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("버튼")]
        [Tooltip("매칭 취소 버튼")]
        [SerializeField] private Button _cancelButton;

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
                .Select(s => s == BattleViewModel.BattleScreen.RandomMatch)
                .DistinctUntilChanged()
                .Subscribe(visible => gameObject.SetActive(visible))
                .AddTo(_disposables);

            // 매칭 상태 표시
            vm.IsMatchmaking
                .Subscribe(matching =>
                {
                    if (_statusText != null)
                        _statusText.text = matching ? "매칭 중..." : "랜덤 매칭 (추후 구현 예정)";
                })
                .AddTo(_disposables);

            // 취소 버튼
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(
                    () => vm.CmdCancelMatchmaking.OnNext(Unit.Default));

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

            if (_cancelButton != null) _cancelButton.onClick.RemoveAllListeners();
            if (_backButton != null) _backButton.onClick.RemoveAllListeners();
        }
    }
}
