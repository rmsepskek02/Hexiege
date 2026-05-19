// ============================================================================
// GameEndUseCase.cs
// Castle 파괴를 감지하여 게임 승패를 판정하는 UseCase.
//
// 역할:
//   1. OnBuildingDied 이벤트 구독 (Castle만 관심 대상이므로 건물 전용 이벤트만 구독)
//   2. 사망한 건물이 Castle인지 확인
//   3. Castle 팀의 상대 팀을 승리자로 OnGameEnd 이벤트 발행
//
// 흐름:
//   UnitCombatUseCase → OnBuildingDied → GameEndUseCase → OnGameEnd → GameEndUI
//
// 멀티플레이 동작:
//   - 서버: OnBuildingDied 수신 → IsGameOver 체크 → OnGameEnd 발행
//          → NetworkGameEndController가 OnGameEnd를 구독하여 ClientRpc로 전파
//   - 클라이언트: NetworkContext.IsNetworkActive가 true이면 OnGameEnd 발행을 생략.
//               GameEndUI는 AnnounceWinnerClientRpc를 통해서만 표시됨.
//               (클라이언트 GameEndUseCase는 IsGameOver 상태 추적만 담당)
//
// 싱글플레이:
//   NetworkContext.IsNetworkActive = false → 기존 로직 그대로 실행.
//
// 사망 이벤트 채널:
//   유닛 전용 OnUnitDied / 건물 전용 OnBuildingDied 두 채널이 존재한다.
//   GameEndUseCase는 Castle(건물)만 관심 대상이므로 OnBuildingDied만 구독한다.
//
// Application 레이어 — 순수 C# + UniRx.
// ============================================================================

using System;
using UniRx;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class GameEndUseCase : IDisposable
    {
        /// <summary> 게임이 이미 종료되었는지 여부. 중복 발행 방지. </summary>
        public bool IsGameOver { get; private set; }

        private readonly IDisposable _subscription;

        public GameEndUseCase()
        {
            // 건물 전용 사망 이벤트만 구독 — Castle 파괴 외엔 게임 종료와 무관하므로
            // 유닛 사망(OnUnitDied)은 구독하지 않는다.
            _subscription = GameEvents.OnBuildingDied
                .Subscribe(OnBuildingDied);
        }

        /// <summary>
        /// 건물이 사망(파괴/철거)했을 때 호출.
        /// Castle이 파괴된 경우에만 게임 종료를 판정하고 OnGameEnd 이벤트를 발행한다.
        ///
        /// 건물 전용 강타입 DTO(BuildingDiedEvent)를 사용하므로 별도의 is 캐스트 분기가 불필요.
        /// </summary>
        /// <param name="e">사망한 건물 정보가 담긴 이벤트.</param>
        private void OnBuildingDied(BuildingDiedEvent e)
        {
            // 이미 게임이 끝났다면 추가 처리 없음 (중복 발행 방지)
            if (IsGameOver) return;
            if (e.Building == null) return;

            // Castle이 아닌 건물 파괴는 게임 종료와 무관 → 무시
            if (e.Building.Type != BuildingType.Castle) return;

            IsGameOver = true;

            // 멀티플레이 클라이언트에서는 OnGameEnd를 직접 발행하지 않음.
            // 서버의 NetworkGameEndController → AnnounceWinnerClientRpc 경유로 표시됨.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            {
                return;
            }

            // 싱글플레이 또는 네트워크 서버: OnGameEnd 발행.
            // 파괴된 Castle의 팀과 반대 팀이 승자.
            TeamId winner = (e.Building.Team == TeamId.Blue) ? TeamId.Red : TeamId.Blue;
            GameEvents.OnGameEnd.OnNext(new GameEndEvent(winner));
        }

        /// <summary>
        /// 싱글플레이 포기 처리.
        /// 로컬 플레이어(Blue 팀 고정)를 패배 처리하고 Red 팀 승리로 OnGameEnd 이벤트를 발행한다.
        ///
        /// 호출자:
        ///   InGameSettingsUI.OnForfeitConfirmed() — 사용자가 포기 확인 팝업에서 확정 클릭 시.
        ///
        /// 멀티플레이는 이 메서드를 사용하지 않는다.
        /// 멀티에서는 NetworkGameEndController.RequestForfeit() 경로로 서버가 처리.
        ///
        /// 이미 게임이 종료된 상태(IsGameOver==true)라면 중복 발행 방지를 위해 무시한다.
        /// </summary>
        public void Forfeit()
        {
            // 이미 끝났다면 무시 (예: Castle 파괴와 포기 클릭이 동시에 들어오는 케이스)
            if (IsGameOver) return;

            IsGameOver = true;

            // 싱글플레이에서는 로컬 플레이어 = 항상 Blue.
            // Blue가 포기했으므로 Red가 승리.
            GameEvents.OnGameEnd.OnNext(new GameEndEvent(TeamId.Red));
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
