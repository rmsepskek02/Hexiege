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

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private readonly CompositeDisposable _disposables = new();

        // 본 View 자신의 GameObject에 부착된 CanvasGroup 캐시.
        // 공통 UI 규칙 Rule 5: SetActive 대신 CanvasGroup으로 표시/숨김 처리.
        private CanvasGroup _canvasGroup;

        // 취소 버튼 GameObject에 부착된 CanvasGroup 캐시.
        // 취소 버튼이 매칭 상태 텍스트와 같은 LayoutGroup 안에 있을 경우 SetActive로 숨기면
        // 다른 UI 요소가 자리 이동하여 흔들림이 생긴다. CanvasGroup.alpha=0으로 공간 유지.
        private CanvasGroup _cancelButtonGroup;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // 자신의 CanvasGroup 확보.
            if (!TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 취소 버튼의 CanvasGroup 확보 (있으면 사용, 없으면 추가).
            if (_cancelButton != null)
            {
                if (!_cancelButton.TryGetComponent(out _cancelButtonGroup))
                    _cancelButtonGroup = _cancelButton.gameObject.AddComponent<CanvasGroup>();
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

            // 화면 활성화 관리 — RandomMatch일 때만 표시
            // Rule 5: SetActive 대신 CanvasGroup의 alpha/blocksRaycasts/interactable 전환.
            vm.CurrentScreen
                .Select(s => s == BattleViewModel.BattleScreen.RandomMatch)
                .DistinctUntilChanged()
                .Subscribe(SetVisible)
                .AddTo(_disposables);

            // 대기 시간 타이머 표시
            vm.MatchWaitSeconds
                .Subscribe(sec =>
                {
                    if (_statusText != null)
                    {
                        int min = sec / 60;
                        int s = sec % 60;
                        _statusText.text = $"매칭 중... {min:00}:{s:00}";
                    }
                })
                .AddTo(_disposables);

            // 매칭 상태에 따라 취소 버튼 표시/숨김
            // SetActive 대신 CanvasGroup으로 처리해 LayoutGroup 안에서 공간을 유지한다.
            vm.IsMatchmaking
                .Subscribe(matching =>
                {
                    SetCancelButtonVisible(matching);
                    // 매칭 대기 전 초기 텍스트
                    if (!matching && _statusText != null)
                        _statusText.text = "랜덤 매칭";
                })
                .AddTo(_disposables);

            // 취소 버튼
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(
                    () => vm.CmdCancelMatchmaking.OnNext(Unit.Default));
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
        /// 취소 버튼 표시/숨김 처리 (CanvasGroup 기반).
        /// 매칭 중일 때만 보이고 평소엔 숨겨지지만, 공간은 유지되어 다른 UI 요소가 흔들리지 않는다.
        /// blocksRaycasts=false로 두어 숨김 상태에서 클릭이 무효화됨.
        /// </summary>
        /// <param name="visible">true=표시, false=숨김.</param>
        private void SetCancelButtonVisible(bool visible)
        {
            if (_cancelButtonGroup == null)
                return;

            _cancelButtonGroup.alpha = visible ? 1f : 0f;
            _cancelButtonGroup.blocksRaycasts = visible;
            _cancelButtonGroup.interactable = visible;
        }
    }
}
