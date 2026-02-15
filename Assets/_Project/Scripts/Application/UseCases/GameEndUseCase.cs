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

                // 파괴된 Castle의 상대 팀이 승리
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
