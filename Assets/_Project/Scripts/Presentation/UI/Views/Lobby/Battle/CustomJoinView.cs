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

            // 화면 활성화 관리 — CustomJoin일 때만 표시
            // Rule 5: SetActive 대신 CanvasGroup의 alpha/blocksRaycasts/interactable 전환.
            vm.CurrentScreen
                .Select(s => s == BattleViewModel.BattleScreen.CustomJoin)
                .DistinctUntilChanged()
                .Subscribe(SetVisible)
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
