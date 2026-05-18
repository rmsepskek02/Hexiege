// ============================================================================
// BuildingActionPanelUI.cs
// 비생산 건물(MiningPost / AutoTower / FlightFacility / Research / MagicBuilding /
// HealShrine 등)을 클릭했을 때 표시되는 공용 액션 팝업 UI.
//
// 왜 별도 클래스인가?
//   - 생산 건물은 유닛 버튼/큐/업그레이드 등 풀스펙 UI(ProductionPanelUI)를 사용한다.
//   - 비생산 건물은 "건물 이름 표시 + 철거" 정도의 간이 UI만 필요하다.
//   - 공통 요소(헤더/닫기/철거/환불)는 BuildingPanelBase에서 제공한다.
//
// 이 클래스에서 추가하는 동작은 없으며, 베이스의 기능만 사용한다.
// 향후 건물 타입별 특수 기능(예: AutoTower 사거리 표시 토글)이 필요해지면
// 이 클래스에서 직렬화 필드와 OnShow 오버라이드를 추가하면 된다.
//
// 표시 대상 판정은 InputHandler에서 BuildingTypeHelper.CanShowActionPanel()로 수행.
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 비생산 건물 클릭 시 표시되는 공용 액션 패널.
    /// 현재 지원 동작: 건물 이름 표시(헤더) + 철거.
    /// 모든 공통 동작은 BuildingPanelBase에 위치한다.
    /// </summary>
    public class BuildingActionPanelUI : BuildingPanelBase
    {
        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 의존성 주입. GameBootstrapper에서 호출한다.
        /// 멀티플레이 모드라면 networkBuildingController를 함께 전달하여
        /// 철거 요청이 서버 RPC를 거치도록 한다.
        /// 싱글플레이 모드라면 마지막 인자는 null로 호출한다.
        /// </summary>
        /// <param name="buildingPlacement">건물 도메인 상태 제어 UseCase.</param>
        /// <param name="resource">자원(골드) 관리 UseCase.</param>
        /// <param name="networkBuildingController">멀티플레이 컨트롤러. 싱글플레이 시 null.</param>
        public void Initialize(
            BuildingPlacementUseCase buildingPlacement,
            ResourceUseCase resource,
            NetworkBuildingController networkBuildingController = null)
        {
            // 베이스의 의존성/버튼 이벤트 연결을 수행한다.
            InitializeBase(buildingPlacement, resource, networkBuildingController);
        }
    }
}
