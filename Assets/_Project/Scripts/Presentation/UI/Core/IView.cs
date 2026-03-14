// ============================================================================
// IView.cs
// View 공통 인터페이스. MVVM 패턴에서 View가 ViewModel에 바인딩하는 계약.
//
// Presentation 레이어 — Unity 의존 없음.
// ============================================================================

namespace Hexiege.Presentation
{
    /// <summary>
    /// View-ViewModel 바인딩 공통 인터페이스.
    /// 모든 View는 이 인터페이스를 구현하여 ViewModel과 일관된 방식으로 연결.
    /// </summary>
    public interface IView<TViewModel>
    {
        /// <summary>
        /// ViewModel을 바인딩하여 UI 상태 구독 설정.
        /// </summary>
        void Bind(TViewModel viewModel);

        /// <summary>
        /// ViewModel 구독 해제. 씬 전환이나 재바인딩 전에 호출.
        /// </summary>
        void Unbind();
    }
}
