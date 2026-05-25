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

        // 본 View 자신의 GameObject에 부착된 CanvasGroup 캐시.
        // 공통 UI 규칙 Rule 5: SetActive 대신 CanvasGroup으로 표시/숨김 처리.
        private CanvasGroup _canvasGroup;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // CanvasGroup이 없으면 추가하여 표시/숨김에 사용할 준비를 한다.
            if (!TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
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

            // 화면 활성화 관리 — CustomGame일 때만 표시
            // Rule 5: SetActive 대신 CanvasGroup의 alpha/blocksRaycasts/interactable 전환.
            vm.CurrentScreen
                .Select(s => s == BattleViewModel.BattleScreen.CustomGame)
                .DistinctUntilChanged()
                .Subscribe(SetVisible)
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
    }
}
