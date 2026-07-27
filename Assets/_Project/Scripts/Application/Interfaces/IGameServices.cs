// ============================================================================
// IGameServices.cs
// Infrastructure 레이어의 NetworkXxx 컴포넌트들이 사용하는 게임 서비스 인터페이스.
//
// 왜 이 인터페이스가 필요한가?
//   원래 NetworkXxx 파일들은 씬에서 GameBootstrapper(Bootstrap 계층)를 직접
//   FindFirstObjectByType<>()으로 탐색해서 UseCase를 얻어 왔다.
//   그런데 Infrastructure가 Bootstrap을 직접 참조하는 것은
//   "내부 레이어가 외부 레이어를 모른다"는 Clean Architecture 규칙 위반이다.
//
//   이 인터페이스를 Application 계층에 두면:
//     - Bootstrap이 이 인터페이스를 구현하고, Application 계층의 로케이터에 등록한다.
//     - Infrastructure는 로케이터에서 인터페이스를 꺼내 쓴다.
//     - Infrastructure ↛ Bootstrap (역방향 의존 제거).
//
// 포함 멤버:
//   Infrastructure/Network 파일들이 실제로 호출하는 UseCase Getter 10종 +
//   네트워크 게임 생명주기 2종(StartNetworkGame, IsNetworkGameStarted).
//   GetConfig / GetGameEndUI / GetFlowFieldService는 Infrastructure 미사용 → 제외.
//
// Application 레이어 — Domain/Application 타입 참조 허용. Unity.Netcode 참조 금지.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// GameBootstrapper가 제공하는 UseCase 및 네트워크 생명주기 서비스의 추상화.
    /// Infrastructure/Network 계층이 Bootstrap에 직접 의존하지 않도록 한다.
    /// </summary>
    public interface IGameServices
    {
        // ── UseCase 접근 ────────────────────────────────────────────────────

        /// <summary>
        /// 현재 로드된 HexGrid. 타일 소유권 동기화(NetworkTileSync)에서 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        HexGrid GetGrid();

        /// <summary>
        /// 자원(골드) UseCase. 골드 차감/환불/UI 갱신에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        ResourceUseCase GetResource();

        /// <summary>
        /// 건물 배치 UseCase. 건물 생성·업그레이드·철거 서버 처리에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        BuildingPlacementUseCase GetBuildingPlacement();

        /// <summary>
        /// 유닛 생산 UseCase. 생산 큐 관리 및 생산 완료 처리에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        UnitProductionUseCase GetUnitProduction();

        /// <summary>
        /// 유닛 스폰 UseCase. 유닛 등록·제거·조회에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        UnitSpawnUseCase GetUnitSpawn();

        /// <summary>
        /// 인구 UseCase. 인구 상한 체크 및 유닛 생산 가능 여부 판단에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        PopulationUseCase GetPopulation();

        /// <summary>
        /// 유닛 이동 UseCase. 이동 경로 계산 및 위치 갱신에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        UnitMovementUseCase GetMovement();

        /// <summary>
        /// 유닛 전투 UseCase. 공격 판정·데미지 처리에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        UnitCombatUseCase GetCombatUseCase();

        /// <summary>
        /// 타워 전투 UseCase. 방어 타워의 자동 공격 처리에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        TowerCombatUseCase GetTowerCombat();

        /// <summary>
        /// 연구소 유닛 강화(업그레이드) UseCase. 팀별 트랙 레벨·진행 상태 조회/구동에 사용.
        /// 서버(Infrastructure)가 연구 요청 처리·타이머 구동·완료 브로드캐스트에 참조한다.
        /// 맵 로드 전이면 null.
        /// </summary>
        UnitUpgradeUseCase GetUpgradeUseCase();

        /// <summary>
        /// 유닛 팩토리. 유닛 GameObject 생성·조회·뷰 초기화에 사용.
        /// IUnitFactory로 추상화하여 Infrastructure.UnitFactory에 직접 의존하지 않는다.
        /// </summary>
        IUnitFactory GetUnitFactory();

        // ── 네트워크 게임 생명주기 ────────────────────────────────────────

        /// <summary>
        /// 네트워크 게임 시작. 맵 로드 + UseCase 초기화 + 팀별 뷰 설정.
        /// NetworkGameFlow.StartGameClientRpc()에서 Host/Client 양쪽 모두 호출한다.
        /// </summary>
        void StartNetworkGame(TeamId localTeam);

        /// <summary>
        /// 네트워크 게임이 이미 시작되었는지 여부.
        /// true이면 중복 시작 시도를 무시하는 데 사용(NetworkGameFlow).
        /// </summary>
        bool IsNetworkGameStarted { get; }
    }
}
