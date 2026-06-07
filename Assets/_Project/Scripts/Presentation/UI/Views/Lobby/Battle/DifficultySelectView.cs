// ============================================================================
// DifficultySelectView.cs
// 싱글플레이 난이도 선택 화면 View.
//
// 역할:
//   - BattleScreen.SingleplayDifficulty일 때 CanvasGroup으로 표시
//   - 쉬움 / 보통 / 어려움 버튼 클릭 → vm.CmdSelectDifficulty 발행
//   - 뒤로가기 버튼 클릭 → vm.CmdBack 발행 → NavigateBack() → BattleScreen.Main 복귀
//
// Inspector 연결 필요 (씬 조립 시 Lobby.unity에서):
//   _easyButton     → 쉬움 버튼
//   _normalButton   → 보통 버튼
//   _hardButton     → 어려움 버튼
//   _backButton     → 뒤로가기 버튼
//
// UI 표시/숨김은 GameSystemRules_UI.md Rule 5에 따라
// CanvasGroup alpha/blocksRaycasts/interactable 전환으로 처리.
// (GameSystemRules_AI.md 규칙 35)
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UniRx;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 싱글플레이 난이도 선택 화면 View.
    /// BattleScreen.SingleplayDifficulty 상태일 때 표시된다.
    /// </summary>
    public class DifficultySelectView : MonoBehaviour, IView<BattleViewModel>
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("난이도 선택 버튼")]
        [Tooltip("쉬움 난이도 버튼")]
        [SerializeField] private Button _easyButton;

        [Tooltip("보통 난이도 버튼")]
        [SerializeField] private Button _normalButton;

        [Tooltip("어려움 난이도 버튼")]
        [SerializeField] private Button _hardButton;

        [Header("네비게이션")]
        [Tooltip("이전 화면(전투 메인)으로 돌아가는 버튼")]
        [SerializeField] private Button _backButton;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// BattleViewModel 구독을 관리하는 Disposable 컨테이너.
        /// Unbind() 호출 시 한 번에 모든 구독 해제.
        /// </summary>
        private readonly CompositeDisposable _disposables = new();

        /// <summary>
        /// 이 View의 표시/숨김을 담당하는 CanvasGroup.
        /// Rule 5: SetActive 대신 CanvasGroup으로 처리하여 LayoutGroup 깨짐을 방지.
        /// </summary>
        private CanvasGroup _canvasGroup;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // CanvasGroup이 없으면 Awake에서 자동으로 추가한다.
            // 반드시 Inspector에서도 CanvasGroup을 부착해 둘 것을 권장.
            if (!TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // ====================================================================
        // IView 구현
        // ====================================================================

        /// <summary>
        /// BattleViewModel에 바인딩.
        /// CurrentScreen을 구독하여 SingleplayDifficulty 상태일 때만 이 View를 표시.
        /// </summary>
        public void Bind(BattleViewModel vm)
        {
            // 혹시 이전 바인딩이 남아있으면 먼저 정리
            Unbind();

            // CurrentScreen이 SingleplayDifficulty일 때만 표시
            // Rule 5: CanvasGroup alpha/blocksRaycasts/interactable 전환
            vm.CurrentScreen
                .Select(s => s == BattleViewModel.BattleScreen.SingleplayDifficulty)
                .DistinctUntilChanged()
                .Subscribe(SetVisible)
                .AddTo(_disposables);

            // 쉬움 버튼 → DifficultyLevel.Easy
            if (_easyButton != null)
                _easyButton.onClick.AddListener(
                    () => vm.CmdSelectDifficulty.OnNext(DifficultyLevel.Easy));

            // 보통 버튼 → DifficultyLevel.Normal
            if (_normalButton != null)
                _normalButton.onClick.AddListener(
                    () => vm.CmdSelectDifficulty.OnNext(DifficultyLevel.Normal));

            // 어려움 버튼 → DifficultyLevel.Hard
            if (_hardButton != null)
                _hardButton.onClick.AddListener(
                    () => vm.CmdSelectDifficulty.OnNext(DifficultyLevel.Hard));

            // 뒤로가기 버튼 → CmdBack → NavigateBack() → BattleScreen.Main 복귀
            if (_backButton != null)
                _backButton.onClick.AddListener(
                    () => vm.CmdBack.OnNext(Unit.Default));
        }

        /// <summary>
        /// 구독 및 버튼 리스너를 모두 해제.
        /// </summary>
        public void Unbind()
        {
            _disposables.Clear();

            if (_easyButton != null)   _easyButton.onClick.RemoveAllListeners();
            if (_normalButton != null) _normalButton.onClick.RemoveAllListeners();
            if (_hardButton != null)   _hardButton.onClick.RemoveAllListeners();
            if (_backButton != null)   _backButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// View 표시/숨김 처리 (CanvasGroup 기반).
        /// Rule 5: SetActive 대신 alpha/blocksRaycasts/interactable을 전환하여
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
    }
}
