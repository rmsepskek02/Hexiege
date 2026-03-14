// ============================================================================
// LobbyViewModel.cs
// 로비 화면의 탭 전환 상태를 관리하는 ViewModel.
//
// 역할:
//   - CurrentTab: 현재 활성화된 탭 (Battle, Shop, Profile, Ranking)
//   - SelectTab: View에서 탭 선택 커맨드를 전달받아 CurrentTab 갱신
//
// 순수 C# 클래스 — MonoBehaviour 아님, IDisposable로 구독 정리.
// Presentation 레이어.
// ============================================================================

using System;
using UniRx;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 로비 메인 탭 전환 상태를 관리하는 ViewModel.
    /// </summary>
    public class LobbyViewModel : IDisposable
    {
        // ====================================================================
        // 탭 정의
        // ====================================================================

        public enum LobbyTab { Battle, Shop, Profile, Ranking }

        // ====================================================================
        // 상태 (View가 구독)
        // ====================================================================

        /// <summary>현재 활성 탭.</summary>
        public ReactiveProperty<LobbyTab> CurrentTab = new(LobbyTab.Battle);

        // ====================================================================
        // 커맨드 (View → ViewModel)
        // ====================================================================

        /// <summary>탭 전환 커맨드. View에서 OnNext(탭)으로 호출.</summary>
        public Subject<LobbyTab> SelectTab = new();

        // ====================================================================
        // 내부
        // ====================================================================

        private readonly CompositeDisposable _disposables = new();

        public LobbyViewModel()
        {
            // 탭 선택 커맨드 → CurrentTab 갱신
            SelectTab
                .Subscribe(tab => CurrentTab.Value = tab)
                .AddTo(_disposables);
        }

        public void Dispose() => _disposables.Dispose();
    }
}
