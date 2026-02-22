// ============================================================================
// GameEndUseCase.cs
// Castle 파괴를 감지하여 게임 승패를 판정하는 UseCase.
//
// 역할:
//   1. OnEntityDied 이벤트 구독
//   2. 사망한 엔티티가 Castle인지 확인
//   3. Castle 팀의 상대 팀을 승리자로 OnGameEnd 이벤트 발행
//
// 흐름:
//   UnitCombatUseCase → OnEntityDied → GameEndUseCase → OnGameEnd → GameEndUI
//
// 멀티플레이 동작:
//   - 서버: OnEntityDied 수신 → IsGameOver 체크 → OnGameEnd 발행
//          → NetworkGameEndController가 OnGameEnd를 구독하여 ClientRpc로 전파
//   - 클라이언트: NetworkContext.IsNetworkActive가 true이면 OnGameEnd 발행을 생략.
//               GameEndUI는 AnnounceWinnerClientRpc를 통해서만 표시됨.
//               (클라이언트 GameEndUseCase는 IsGameOver 상태 추적만 담당)
//
// 싱글플레이:
//   NetworkContext.IsNetworkActive = false → 기존 로직 그대로 실행.
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
            _subscription = GameEvents.OnEntityDied
                .Subscribe(OnEntityDied);
        }

        private void OnEntityDied(EntityDiedEvent e)
        {
            if (IsGameOver) return;

            // Castle이 파괴되었는지 확인
            if (e.Entity is BuildingData building && building.Type == BuildingType.Castle)
            {
                IsGameOver = true;

                // 멀티플레이 클라이언트에서는 OnGameEnd를 직접 발행하지 않음.
                // 서버의 NetworkGameEndController → AnnounceWinnerClientRpc 경유로 표시됨.
                if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
                {
                    return;
                }

                // 싱글플레이 또는 네트워크 서버: OnGameEnd 발행
                TeamId winner = (building.Team == TeamId.Blue) ? TeamId.Red : TeamId.Blue;
                GameEvents.OnGameEnd.OnNext(new GameEndEvent(winner));
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
