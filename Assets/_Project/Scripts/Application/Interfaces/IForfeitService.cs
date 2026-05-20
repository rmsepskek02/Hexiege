// ============================================================================
// IForfeitService.cs
// 게임 포기(Forfeit) 요청을 추상화한 인터페이스.
//
// 왜 필요한가:
//   InGameSettingsUI(Presentation 레이어)는 포기 버튼 확정 시 두 경로로 분기해야 한다.
//     - 싱글플레이: GameEndUseCase.Forfeit() 호출 (Red 승리로 종료)
//     - 멀티플레이: NetworkGameEndController.RequestForfeit() 호출 (서버가 자기 팀 패배 처리)
//
//   기존에는 UI가 직접 NetworkGameEndController를 FindFirstObjectByType으로 찾아 호출했다.
//   이 방식은 Presentation 레이어가 Infrastructure 구현체에 직접 결합되는 문제가 있다.
//
//   IForfeitService는 "포기 요청"이라는 의도만 노출하여, UI는 어느 모드든 같은 메서드를
//   호출하고, 실제 구현(싱글 vs 멀티)은 GameBootstrapper에서 적합한 구현체를 주입한다.
//
// 구현체:
//   - GameEndUseCase (Application 레이어, 싱글플레이용) — 기존 Forfeit() 메서드 재활용
//   - NetworkGameEndController (Infrastructure 레이어, 멀티플레이용) — RequestForfeit() 메서드 재활용
//
// 의존 주입:
//   GameBootstrapper.LoadMap()에서 NetworkContext.IsNetworkActive 분기로
//   적합한 구현체를 선택해 InGameSettingsUI.Initialize()에 주입한다.
//
// Application 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// 게임 포기 요청을 추상화한 인터페이스.
    /// 싱글/멀티 양쪽에서 동일한 호출 흐름을 보장하기 위해 UI에 주입한다.
    /// </summary>
    public interface IForfeitService
    {
        /// <summary>
        /// 사용자가 게임 포기를 확정했을 때 호출.
        /// 구현체에 따라 즉시 게임 종료를 발행하거나(싱글) 서버로 RPC를 전송한다(멀티).
        /// </summary>
        void RequestForfeit();
    }
}
