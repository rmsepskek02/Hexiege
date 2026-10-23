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
//   팩토리도 Application 인터페이스로 받아 Infrastructure 구체 타입을 노출하지 않는다.
//   유닛은 기존 IUnitFactory, 건물은 조회 전용 IBuildingObjectProvider를 사용하며,
//   호출자는 기존 팩토리 객체를 그대로 전달한다.
//
// Application 레이어 — Domain/Application 타입에 의존.
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
        /// <param name="unitFactory">타겟 유닛의 씬 오브젝트를 조회할 팩토리 인터페이스.</param>
        /// <param name="buildingFactory">타겟 건물의 씬 오브젝트를 조회할 인터페이스.</param>
        /// <param name="positionProvider">유닛 위치 조회 서비스.</param>
        void SetDependencies(
            UnitMovementUseCase movementUseCase,
            UnitCombatUseCase combatUseCase,
            IUnitFactory unitFactory = null,
            IBuildingObjectProvider buildingFactory = null,
            IEntityPositionProvider positionProvider = null);
    }
}
