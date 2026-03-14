// ============================================================================
// BattleRootView.cs
// 전투 탭 루트 View. BattleViewModel을 생성하고 서브뷰에 바인딩.
//
// 역할:
//   - Bind()에서 모든 서브뷰(BattleMainView, CustomGameView 등)에 ViewModel 전달
//   - Unbind()에서 서브뷰 구독 해제
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 전투 탭 루트 View. 서브뷰에 BattleViewModel을 바인딩.
    /// </summary>
    public class BattleRootView : MonoBehaviour, IView<BattleViewModel>
    {
        // ====================================================================
        // Inspector 참조 — 서브뷰
        // ====================================================================

        [Header("서브뷰")]
        [SerializeField] private BattleMainView _battleMainView;
        [SerializeField] private CustomGameView _customGameView;
        [SerializeField] private CustomHostView _customHostView;
        [SerializeField] private CustomJoinView _customJoinView;
        [SerializeField] private RandomMatchView _randomMatchView;

        // ====================================================================
        // IView 구현
        // ====================================================================

        /// <summary>
        /// 모든 서브뷰에 BattleViewModel을 바인딩.
        /// </summary>
        public void Bind(BattleViewModel vm)
        {
            Unbind();

            if (_battleMainView != null) _battleMainView.Bind(vm);
            if (_customGameView != null) _customGameView.Bind(vm);
            if (_customHostView != null) _customHostView.Bind(vm);
            if (_customJoinView != null) _customJoinView.Bind(vm);
            if (_randomMatchView != null) _randomMatchView.Bind(vm);
        }

        /// <summary>
        /// 모든 서브뷰의 구독 해제.
        /// </summary>
        public void Unbind()
        {
            if (_battleMainView != null) _battleMainView.Unbind();
            if (_customGameView != null) _customGameView.Unbind();
            if (_customHostView != null) _customHostView.Unbind();
            if (_customJoinView != null) _customJoinView.Unbind();
            if (_randomMatchView != null) _randomMatchView.Unbind();
        }
    }
}
