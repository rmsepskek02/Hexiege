// ============================================================================
// TabBarView.cs
// 로비 하단 탭 바. 탭 버튼 5개의 이벤트를 ViewModel 커맨드로 연결.
//
// 역할:
//   - 버튼 클릭 → vm.SelectTab.OnNext(탭)
//   - vm.CurrentTab 구독 → 선택된 탭 버튼 시각 강조
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UniRx;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 로비 탭 바 View. 탭 버튼 클릭을 LobbyViewModel에 전달.
    /// </summary>
    public class TabBarView : MonoBehaviour, IView<LobbyViewModel>
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("탭 버튼")]
        [SerializeField] private Button _battleTabButton;
        [SerializeField] private Button _shopTabButton;
        [SerializeField] private Button _profileTabButton;
        // 설정 탭 버튼. 씬 계층에서는 SettingTabBtn 오브젝트의 Button 컴포넌트를 연결한다.
        // (탭바 배치 순서상 프로필 버튼과 랭킹 버튼 사이에 위치)
        [SerializeField] private Button _settingTabButton;
        [SerializeField] private Button _rankingTabButton;

        [Header("선택 표시")]
        [Tooltip("탭 버튼 기본 색상")]
        [SerializeField] private Color _normalColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [Tooltip("탭 버튼 선택 색상")]
        [SerializeField] private Color _selectedColor = Color.white;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private readonly CompositeDisposable _disposables = new();

        // ====================================================================
        // IView 구현
        // ====================================================================

        /// <summary>
        /// LobbyViewModel에 바인딩. 탭 버튼 이벤트 + 선택 상태 구독.
        /// </summary>
        public void Bind(LobbyViewModel vm)
        {
            Unbind();

            // 버튼 클릭 → 탭 전환 커맨드
            BindButton(_battleTabButton, vm, LobbyViewModel.LobbyTab.Battle);
            BindButton(_shopTabButton, vm, LobbyViewModel.LobbyTab.Shop);
            BindButton(_profileTabButton, vm, LobbyViewModel.LobbyTab.Profile);
            BindButton(_settingTabButton, vm, LobbyViewModel.LobbyTab.Setting);
            BindButton(_rankingTabButton, vm, LobbyViewModel.LobbyTab.Ranking);

            // 현재 탭 → 버튼 시각 강조
            vm.CurrentTab
                .Subscribe(tab =>
                {
                    UpdateButtonVisual(_battleTabButton, tab == LobbyViewModel.LobbyTab.Battle);
                    UpdateButtonVisual(_shopTabButton, tab == LobbyViewModel.LobbyTab.Shop);
                    UpdateButtonVisual(_profileTabButton, tab == LobbyViewModel.LobbyTab.Profile);
                    UpdateButtonVisual(_settingTabButton, tab == LobbyViewModel.LobbyTab.Setting);
                    UpdateButtonVisual(_rankingTabButton, tab == LobbyViewModel.LobbyTab.Ranking);
                })
                .AddTo(_disposables);
        }

        /// <summary>
        /// 구독 해제. 버튼 리스너는 OnDestroy에서 자동 정리되므로 Subject 구독만 해제.
        /// </summary>
        public void Unbind()
        {
            _disposables.Clear();
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// 버튼 클릭 이벤트를 탭 선택 커맨드에 연결.
        /// </summary>
        private void BindButton(Button button, LobbyViewModel vm, LobbyViewModel.LobbyTab tab)
        {
            if (button == null) return;
            button.onClick.AddListener(() =>
            {
                vm.SelectTab.OnNext(tab);
            });
        }

        /// <summary>
        /// 탭 버튼의 시각 상태를 갱신 (선택/비선택).
        /// </summary>
        private void UpdateButtonVisual(Button button, bool isSelected)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = isSelected ? _selectedColor : _normalColor;
            button.colors = colors;
        }
    }
}
