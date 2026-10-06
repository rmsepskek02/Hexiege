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
    /// 이 인터페이스를 구현한 View 는 ViewModel 과 일관된 방식으로 연결된다.
    ///
    /// ⚠️ 이름이 View 로 끝나는 클래스가 모두 이것을 구현하는 것은 아니다(2026-10-05 실측 —
    ///    이름이 View 로 끝나는 클래스 28개 중 구현체는 9개뿐이다).
    ///    구현하는 쪽은 로비의 전투 탭 화면들과 종족·난이도 선택 화면, 그리고 로비 탭 바다.
    ///    로그인·회원가입·랭킹·상점·프로필·설정 화면과 인게임의 타일·유닛 표시 클래스는
    ///    ViewModel 없이 스스로 상태를 들고 있어 이 계약을 쓰지 않는다.
    ///    그래서 "View 니까 Bind 가 있을 것"이라고 가정하고 호출해서는 안 된다.
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
