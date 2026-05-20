// ============================================================================
// IUnitView.cs
// 유닛 시각화 컴포넌트의 외부 인터페이스.
//
// 왜 필요한가:
//   UnitFactory(Infrastructure)가 UnitView(Presentation) 구체 클래스를 직접 참조하면
//   Infrastructure → Presentation 역방향 의존이 발생한다.
//   본 인터페이스는 UnitFactory가 호출하는 두 진입점만 노출해 의존 방향을 바로잡는다.
//
// 구현체:
//   Presentation.UnitView (MonoBehaviour)
//
// 위치 결정 — Application에 두는 이유:
//   IUnitView는 Application/Domain 컨텍스트(UnitData/UseCase)에 묶여 있어 Application이 자연스러운 위치.
//   SetDependencies가 Infrastructure 타입(UnitFactory/BuildingFactory)을 받지만,
//   Application/Infrastructure는 어셈블리가 분리되어 있지 않아(Assembly-CSharp 단일) 컴파일 의존만 형성된다.
//   엄격한 클린 아키텍처를 적용하려면 Factory 측에도 인터페이스(IUnitFactory/IBuildingFactory)를 도입해야 하지만,
//   본 리팩토링 범위에서는 UnitFactory가 UnitView 구체 타입을 직접 알지 않게 하는 1차 목표만 달성한다.
//
// Application 레이어 — Domain/Infrastructure 컨텍스트 일부 사용 허용(주석 참조).
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 유닛 시각화 컴포넌트의 외부 인터페이스.
    /// UnitFactory가 UnitView를 다룰 때 Presentation 구체 타입 대신 본 인터페이스를 사용한다.
    /// </summary>
    public interface IUnitView
    {
        /// <summary>
        /// UnitData를 시각화 컴포넌트에 주입한다.
        /// 생성 직후/네트워크 동기화 직후 호출된다.
        /// </summary>
        /// <param name="unitData">시각화할 유닛의 도메인 데이터.</param>
        void Initialize(UnitData unitData);

        /// <summary>
        /// 시각화 컴포넌트가 동작에 필요한 의존성을 주입한다.
        /// UnitFactory가 매 유닛 생성 시 호출하여 즉시 동작 가능하게 한다.
        /// </summary>
        /// <param name="movementUseCase">유닛 이동 UseCase.</param>
        /// <param name="combatUseCase">유닛 전투 UseCase.</param>
        /// <param name="unitFactory">유닛 팩토리(Infrastructure 구체 타입).</param>
        /// <param name="buildingFactory">건물 팩토리(Infrastructure 구체 타입).</param>
        /// <param name="positionProvider">유닛 위치 조회 서비스.</param>
        void SetDependencies(
            UnitMovementUseCase movementUseCase,
            UnitCombatUseCase combatUseCase,
            Hexiege.Infrastructure.UnitFactory unitFactory = null,
            Hexiege.Infrastructure.BuildingFactory buildingFactory = null,
            IEntityPositionProvider positionProvider = null);
    }
}
