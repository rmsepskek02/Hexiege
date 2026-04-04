// ============================================================================
// BattleMainView.cs
// 전투 탭 메인 화면. 싱글플레이 / 커스텀 게임 / 랜덤 매칭 선택.
//
// 역할:
//   - [싱글플레이] → vm.CmdStartSingleplay
//   - [커스텀 게임] → vm.CurrentScreen = CustomGame
//   - [랜덤 매칭] → vm.CurrentScreen = RandomMatch
//   - vm.CurrentScreen 구독 → Main일 때만 활성화
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UniRx;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 전투 메인 화면 View. 게임 모드 선택 버튼.
    /// </summary>
    public class BattleMainView : MonoBehaviour, IView<BattleViewModel>
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("버튼")]
        [Tooltip("싱글플레이 시작 버튼")]
        [SerializeField] private Button _singleplayButton;

        [Tooltip("커스텀 게임 버튼")]
        [SerializeField] private Button _customGameButton;

        [Tooltip("랜덤 매칭 버튼")]
        [SerializeField] private Button _randomMatchButton;

        [Header("종족 선택")]
        [Tooltip("종족 선택 UI View (BattleMainView 하단에 배치)")]
        [SerializeField] private RaceSelectionView _raceSelectionView;

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

            // 화면 활성화 관리 — Main일 때만 표시
            vm.CurrentScreen
                .Select(s => s == BattleViewModel.BattleScreen.Main)
                .DistinctUntilChanged()
                .Subscribe(visible => gameObject.SetActive(visible))
                .AddTo(_disposables);

            // 버튼 → 커맨드
            if (_singleplayButton != null)
                _singleplayButton.onClick.AddListener(
                    () => vm.CmdStartSingleplay.OnNext(Unit.Default));

            if (_customGameButton != null)
                _customGameButton.onClick.AddListener(
                    () => vm.CurrentScreen.Value = BattleViewModel.BattleScreen.CustomGame);

            if (_randomMatchButton != null)
                _randomMatchButton.onClick.AddListener(
                    () => vm.CmdStartMatchmaking.OnNext(Unit.Default));
        }

        /// <summary>
        /// 종족 선택 ViewModel을 별도로 바인딩.
        /// IView&lt;BattleViewModel&gt; 인터페이스의 Bind 시그니처를 유지하면서
        /// 종족 선택 기능을 추가하기 위해 별도 메서드로 분리.
        /// BattleRootView.Bind()에서 Bind(vm) 호출 직후에 호출됨.
        /// </summary>
        /// <param name="raceVm">종족 선택 ViewModel.</param>
        public void BindRace(RaceSelectionViewModel raceVm)
        {
            if (_raceSelectionView != null)
                _raceSelectionView.Bind(raceVm);
        }

        /// <summary>
        /// 구독 해제.
        /// </summary>
        public void Unbind()
        {
            _disposables.Clear();

            if (_singleplayButton != null) _singleplayButton.onClick.RemoveAllListeners();
            if (_customGameButton != null) _customGameButton.onClick.RemoveAllListeners();
            if (_randomMatchButton != null) _randomMatchButton.onClick.RemoveAllListeners();

            // 종족 선택 View도 함께 구독 해제
            if (_raceSelectionView != null) _raceSelectionView.Unbind();
        }
    }
}
