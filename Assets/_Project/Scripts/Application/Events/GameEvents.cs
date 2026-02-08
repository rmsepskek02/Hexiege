// ============================================================================
// GameEvents.cs
// 게임 전역 이벤트 허브. UniRx의 Subject를 사용하여 레이어 간 통신을 담당.
//
// 이벤트 허브란?
//   레이어 간에 직접 참조 없이 "이런 일이 일어났다"를 알려주는 중앙 메시지 시스템.
//   발행자(Publisher)는 이벤트를 OnNext()로 보내고,
//   구독자(Subscriber)는 Subscribe()로 받아서 반응.
//
// 흐름 예시 (타일 선택):
//   InputHandler(Presentation) → GridInteractionUseCase(Application)
//     → OnTileSelected.OnNext(coord)  [이벤트 발행]
//     → HexTileView(Presentation)가 Subscribe해서 색상 변경  [이벤트 수신]
//
// Subject<T> vs Event:
//   C# event도 가능하지만 Subject는 구독 해제(Dispose), 필터링, 결합 등
//   리액티브 연산자를 체이닝할 수 있어 유연함.
//   .AddTo(this)로 MonoBehaviour 파괴 시 자동 구독 해제.
//
// 사용 예시:
//   // 발행 (UseCase에서)
//   GameEvents.OnTileSelected.OnNext(coord);
//
//   // 구독 (View에서)
//   GameEvents.OnTileSelected
//       .Subscribe(coord => HighlightTile(coord))
//       .AddTo(this);
//
// Application 레이어 — UniRx 의존.
// ============================================================================

using System;
using UniRx;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 타일 선택 이벤트 데이터.
    /// </summary>
    public struct TileSelectedEvent
    {
        /// <summary> 선택된 타일의 좌표. </summary>
        public HexCoord Coord;

        /// <summary> 이전에 선택되어 있던 타일 좌표. 없으면 null. </summary>
        public HexCoord? PreviousCoord;

        public TileSelectedEvent(HexCoord coord, HexCoord? previousCoord)
        {
            Coord = coord;
            PreviousCoord = previousCoord;
        }
    }

    /// <summary>
    /// 타일 소유자 변경 이벤트 데이터.
    /// 유닛 이동 시 타일 점령 → 이 이벤트 발행 → HexTileView가 색상 변경.
    /// </summary>
    public struct TileOwnerChangedEvent
    {
        /// <summary> 소유자가 변경된 타일의 좌표. </summary>
        public HexCoord Coord;

        /// <summary> 새로운 소유자 팀. </summary>
        public TeamId NewOwner;

        public TileOwnerChangedEvent(HexCoord coord, TeamId newOwner)
        {
            Coord = coord;
            NewOwner = newOwner;
        }
    }

    /// <summary>
    /// 유닛 이동 이벤트 데이터.
    /// 유닛이 타일 하나를 이동할 때마다 발행.
    /// </summary>
    public struct UnitMovedEvent
    {
        /// <summary> 이동한 유닛의 Id. </summary>
        public int UnitId;

        /// <summary> 이동 전 좌표. </summary>
        public HexCoord From;

        /// <summary> 이동 후 좌표. </summary>
        public HexCoord To;

        public UnitMovedEvent(int unitId, HexCoord from, HexCoord to)
        {
            UnitId = unitId;
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// 유닛 생성 이벤트 데이터.
    /// UnitSpawnUseCase에서 유닛 생성 시 발행 → UnitFactory가 프리팹 생성.
    /// </summary>
    public struct UnitSpawnedEvent
    {
        /// <summary> 생성된 유닛의 데이터. </summary>
        public UnitData Unit;

        public UnitSpawnedEvent(UnitData unit)
        {
            Unit = unit;
        }
    }

    /// <summary>
    /// 유닛 공격 이벤트 데이터.
    /// UnitCombatUseCase에서 공격 시 발행 → UnitView가 공격 애니메이션 재생.
    /// </summary>
    public struct UnitAttackEvent
    {
        /// <summary> 공격하는 유닛 Id. </summary>
        public int AttackerId;

        /// <summary> 피격당하는 유닛 Id. </summary>
        public int TargetId;

        /// <summary> 가한 데미지. </summary>
        public int Damage;

        /// <summary> 공격 방향 (공격자 → 타겟). </summary>
        public HexDirection Direction;

        public UnitAttackEvent(int attackerId, int targetId, int damage, HexDirection direction)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            Damage = damage;
            Direction = direction;
        }
    }

    /// <summary>
    /// 유닛 사망 이벤트 데이터.
    /// UnitCombatUseCase에서 HP가 0 이하가 되면 발행.
    /// </summary>
    public struct UnitDiedEvent
    {
        /// <summary> 사망한 유닛 Id. </summary>
        public int UnitId;

        public UnitDiedEvent(int unitId)
        {
            UnitId = unitId;
        }
    }

    /// <summary>
    /// 건물 배치 이벤트 데이터.
    /// BuildingPlacementUseCase에서 건물 배치 시 발행 → BuildingFactory가 프리팹 생성.
    /// </summary>
    public struct BuildingPlacedEvent
    {
        /// <summary> 배치된 건물의 데이터. </summary>
        public BuildingData Building;

        public BuildingPlacedEvent(BuildingData building)
        {
            Building = building;
        }
    }

    /// <summary>
    /// 게임 전역 이벤트 허브.
    /// 모든 이벤트는 static Subject로, 어디서든 발행/구독 가능.
    /// </summary>
    public static class GameEvents
    {
        // ====================================================================
        // 타일 관련 이벤트
        // ====================================================================

        /// <summary>
        /// 타일이 선택되었을 때 발행.
        /// 발행: GridInteractionUseCase
        /// 구독: HexTileView (선택 하이라이트), InputHandler (유닛 이동 명령)
        /// </summary>
        public static readonly Subject<TileSelectedEvent> OnTileSelected = new Subject<TileSelectedEvent>();

        /// <summary>
        /// 타일 소유자가 변경되었을 때 발행.
        /// 발행: UnitMovementUseCase (유닛 이동 시 점령)
        /// 구독: HexTileView (팀 색상 변경)
        /// </summary>
        public static readonly Subject<TileOwnerChangedEvent> OnTileOwnerChanged = new Subject<TileOwnerChangedEvent>();

        // ====================================================================
        // 유닛 관련 이벤트
        // ====================================================================

        /// <summary>
        /// 유닛이 타일 하나를 이동했을 때 발행.
        /// 발행: UnitMovementUseCase
        /// 구독: UnitView (스프라이트 이동 애니메이션)
        /// </summary>
        public static readonly Subject<UnitMovedEvent> OnUnitMoved = new Subject<UnitMovedEvent>();

        /// <summary>
        /// 유닛이 생성되었을 때 발행.
        /// 발행: UnitSpawnUseCase
        /// 구독: UnitFactory (프리팹 인스턴스 생성)
        /// </summary>
        public static readonly Subject<UnitSpawnedEvent> OnUnitSpawned = new Subject<UnitSpawnedEvent>();

        // ====================================================================
        // 전투 관련 이벤트
        // ====================================================================

        /// <summary>
        /// 유닛이 공격했을 때 발행.
        /// 발행: UnitCombatUseCase
        /// 구독: UnitView (공격 애니메이션 재생)
        /// </summary>
        public static readonly Subject<UnitAttackEvent> OnUnitAttack = new Subject<UnitAttackEvent>();

        /// <summary>
        /// 유닛이 사망했을 때 발행.
        /// 발행: UnitCombatUseCase
        /// 구독: UnitView (사망 처리, GameObject 파괴)
        /// </summary>
        public static readonly Subject<UnitDiedEvent> OnUnitDied = new Subject<UnitDiedEvent>();

        // ====================================================================
        // 건물 관련 이벤트
        // ====================================================================

        /// <summary>
        /// 건물이 배치되었을 때 발행.
        /// 발행: BuildingPlacementUseCase
        /// 구독: BuildingFactory (프리팹 인스턴스 생성)
        /// </summary>
        public static readonly Subject<BuildingPlacedEvent> OnBuildingPlaced = new Subject<BuildingPlacedEvent>();
    }
}
