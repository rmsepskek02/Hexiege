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
    /// 유닛이 A* 이동 중 새 타일에 진입할 때 발행되는 이벤트 데이터.
    /// 혼잡도(CongestionMap) 누적 등에 사용.
    /// 발행은 서버 전용 — UnitView.MoveAlongPath의 _isAStarMoving 분기에서.
    /// </summary>
    public readonly struct UnitEnteredTileEvent
    {
        /// <summary> 진입한 유닛 Id. </summary>
        public readonly int UnitId;
        /// <summary> 진입한 타일 좌표. </summary>
        public readonly HexCoord Coord;

        public UnitEnteredTileEvent(int unitId, HexCoord coord)
        {
            UnitId = unitId;
            Coord = coord;
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
    /// 공격 이벤트 데이터. 유닛/건물 모두 포함.
    /// </summary>
    public struct EntityAttackedEvent
    {
        public readonly IDamageable Attacker;
        public readonly IDamageable Target;

        public EntityAttackedEvent(IDamageable attacker, IDamageable target)
        {
            Attacker = attacker;
            Target = target;
        }
    }

    /// <summary>
    /// 피격 이벤트 데이터. 데미지 적용 후 현재 HP 포함.
    /// NetworkHealthSync에서 HP를 모든 클라이언트에 동기화할 때 사용.
    ///
    /// 공격자 정보(AttackerId / AttackerIsUnit)는 피격 표현 큐(HitPresentationQueue)가
    /// "누가 때렸는지"를 알아야 공격자의 로컬 타격 프레임(OnAttackHit) 신호에 맞춰
    /// 피격 연출(HP 텍스트·피격 VFX·타격 반응)을 방출하기 위해 함께 실어 나른다.
    /// (전투 타격 타이밍 동기화 Phase 2 — 축 3)
    /// </summary>
    public readonly struct EntityDamagedEvent
    {
        /// <summary> 피격당한 엔티티. </summary>
        public readonly IDamageable Entity;

        /// <summary> 데미지 적용 후 현재 HP. </summary>
        public readonly int CurrentHp;

        /// <summary> 피격 엔티티가 유닛인지 여부. false면 건물. </summary>
        public readonly bool IsUnit;

        /// <summary>
        /// 공격자의 Id. 유닛이면 UnitData.Id, 건물(타워)이면 BuildingData.Id.
        /// 피격 표현 큐가 이 값을 키로 공격자별 FIFO 큐를 관리한다.
        /// </summary>
        public readonly int AttackerId;

        /// <summary>
        /// 공격자가 유닛인지 여부. false면 타워(건물).
        /// 타워 공격은 타격 애니메이션 프레임이 없으므로, 피격 표현 큐가
        /// 보류 없이 즉시 방출하는 분기(안전망 ⓒ)를 타게 하는 판별에 쓰인다.
        /// </summary>
        public readonly bool AttackerIsUnit;

        public EntityDamagedEvent(IDamageable entity, int currentHp, bool isUnit,
            int attackerId, bool attackerIsUnit)
        {
            Entity = entity;
            CurrentHp = currentHp;
            IsUnit = isUnit;
            AttackerId = attackerId;
            AttackerIsUnit = attackerIsUnit;
        }
    }

    /// <summary>
    /// 유닛 사망 이벤트 데이터.
    /// 발행: UnitCombatUseCase(싱글), NetworkCombatController(클라이언트 동기화)
    /// 구독: UnitView(GameObject 파괴), ProductionTicker(siege 목록 정리),
    ///       NetworkCombatController(서버 → EntityDiedClientRpc 전파)
    ///
    /// 유닛 전용 강타입 사망 DTO.
    /// 구독 측에서 매번 is 캐스트로 타입을 확인하지 않아도 되도록,
    /// 유닛 전용 핸들러와 건물 전용 핸들러를 명확히 구분하기 위한 목적이다.
    /// </summary>
    public readonly struct UnitDiedEvent
    {
        /// <summary> 사망한 유닛 데이터. </summary>
        public readonly UnitData Unit;

        public UnitDiedEvent(UnitData unit)
        {
            Unit = unit;
        }
    }

    /// <summary>
    /// 건물 사망 이벤트 데이터.
    /// 발행: UnitCombatUseCase(전투 파괴), BuildingPlacementUseCase.DemolishBuilding(철거),
    ///       NetworkCombatController(클라이언트 동기화)
    /// 구독: BuildingFactory(GameObject 파괴), GameEndUseCase(Castle 파괴 → 게임 종료),
    ///       FlowFieldService(walkable 변경 → 경로 캐시 무효화),
    ///       GameBootstrapper(살아있는 유닛 즉시 재경로),
    ///       ProductionTicker(생산건물 해제 + walkable 변경 후처리),
    ///       HexGridRenderer(MiningPost 파괴 시 금광 재표시)
    ///
    /// 건물 전용 강타입 사망 DTO.
    /// UnitDiedEvent와 한 쌍으로 사용되며 구독자는 타입 캐스트 없이 BuildingData에 바로 접근할 수 있다.
    /// </summary>
    public readonly struct BuildingDiedEvent
    {
        /// <summary> 사망한 건물 데이터. </summary>
        public readonly BuildingData Building;

        public BuildingDiedEvent(BuildingData building)
        {
            Building = building;
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
    /// 건물 업그레이드 이벤트 데이터.
    /// BuildingPlacementUseCase.UpgradeBuilding 성공 시 발행 →
    /// BuildingFactory가 새 프리팹을 먼저 만들고 기존 GO를 제거 (빈 타일 방지).
    /// </summary>
    public struct BuildingUpgradedEvent
    {
        /// <summary>
        /// 업그레이드 이전 건물의 Id.
        /// BuildingFactory는 이 Id로 기존 GO를 찾아 제거한다.
        /// </summary>
        public int OldBuildingId;

        /// <summary>
        /// 업그레이드로 새로 생성된 BuildingData.
        /// Id는 보통 기존 Id가 그대로 유지되지만, 네트워크 동기화 시 새 Id가 할당될 수 있다.
        /// BuildingFactory는 NewBuilding.Id로 새 GO를 등록한다.
        /// </summary>
        public BuildingData NewBuilding;

        public BuildingUpgradedEvent(int oldBuildingId, BuildingData newBuilding)
        {
            OldBuildingId = oldBuildingId;
            NewBuilding = newBuilding;
        }
    }

    // ====================================================================
    // 생산 시스템 이벤트 데이터
    // ====================================================================

    /// <summary>
    /// 자원(골드) 변경 이벤트.
    /// ResourceUseCase에서 골드 변동 시 발행 → UI 갱신.
    /// </summary>
    public struct ResourceChangedEvent
    {
        public readonly TeamId Team;
        public readonly int Gold;

        public ResourceChangedEvent(TeamId team, int gold)
        {
            Team = team;
            Gold = gold;
        }
    }

    /// <summary>
    /// 생산 시작 이벤트.
    /// UnitProductionUseCase에서 새 유닛 생산 시작 시 발행.
    /// </summary>
    public struct ProductionStartedEvent
    {
        public readonly int BarracksId;
        public readonly UnitType Type;

        public ProductionStartedEvent(int barracksId, UnitType type)
        {
            BarracksId = barracksId;
            Type = type;
        }
    }

    /// <summary>
    /// 유닛 생산 완료 이벤트.
    /// 생산된 유닛 + 랠리포인트 정보. ProductionTicker가 자동 이동 처리.
    /// </summary>
    public struct UnitProducedEvent
    {
        public readonly UnitData Unit;
        public readonly HexCoord? RallyPoint;

        /// <summary>
        /// 이 유닛을 생산한 배럭(생산 건물)의 Id.
        /// AI 시스템(AIOpponentController)이 이 값으로 _lineProduction을 조회하여
        /// 같은 배럭에 다음 유닛을 EnqueueUnit 재호출하는 "콜백 기반 연속 생산"에 사용한다.
        /// (GameSystemRules_AI.md 규칙 12)
        /// 기존 구독자(ProductionTicker)는 이 필드를 사용하지 않으므로 하위 호환성에 영향 없음.
        /// </summary>
        public readonly int BarracksId;

        public UnitProducedEvent(UnitData unit, HexCoord? rallyPoint, int barracksId)
        {
            Unit = unit;
            RallyPoint = rallyPoint;
            BarracksId = barracksId;
        }
    }

    /// <summary>
    /// 생산 큐 변경 이벤트.
    /// 큐 추가/제거, 생산 완료 등에서 발행 → ProductionPanelUI 갱신.
    /// </summary>
    public struct ProductionQueueChangedEvent
    {
        public readonly int BarracksId;

        public ProductionQueueChangedEvent(int barracksId)
        {
            BarracksId = barracksId;
        }
    }

    // ====================================================================
    // 게임 종료 이벤트 데이터
    // ====================================================================

    /// <summary>
    /// 게임 종료 이벤트. Castle 파괴 시 발행.
    /// </summary>
    public readonly struct GameEndEvent
    {
        /// <summary> 승리한 팀. </summary>
        public readonly TeamId Winner;

        public GameEndEvent(TeamId winner)
        {
            Winner = winner;
        }
    }

    /// <summary>
    /// 랠리포인트 변경 이벤트.
    /// UnitProductionUseCase에서 랠리포인트 설정/해제 시 발행.
    /// </summary>
    public readonly struct RallyPointChangedEvent
    {
        public readonly int BarracksId;
        /// <summary> 설정된 좌표. null이면 해제. </summary>
        public readonly HexCoord? Coord;
        /// <summary> 랠리포인트를 설정한 배럭의 소속 팀. 화면 표시 필터링에 사용. </summary>
        public readonly TeamId Team;

        public RallyPointChangedEvent(int barracksId, HexCoord? coord, TeamId team)
        {
            BarracksId = barracksId;
            Coord = coord;
            Team = team;
        }
    }

    /// <summary>
    /// 전투 시작 이벤트 데이터 (싱글플레이 전용).
    /// UnitCombatUseCase.TryAttack()에서 유닛이 전투 상태에 진입할 때 발행.
    /// UnitView가 구독하여 StartCombatAnimation() 호출.
    /// </summary>
    public struct CombatStartedEvent
    {
        /// <summary> 공격하는 유닛의 Id. </summary>
        public readonly int UnitId;
        /// <summary> 타겟 엔티티의 Id. </summary>
        public readonly int TargetId;
        /// <summary> true=유닛, false=건물. </summary>
        public readonly bool TargetIsUnit;

        public CombatStartedEvent(int unitId, int targetId, bool targetIsUnit)
        {
            UnitId = unitId;
            TargetId = targetId;
            TargetIsUnit = targetIsUnit;
        }
    }

    /// <summary>
    /// 전투 타겟 변경 이벤트 데이터 (싱글플레이 전용).
    /// UnitCombatUseCase.TryAttack()에서 전투 중 타겟이 바뀔 때 발행.
    /// UnitView가 구독하여 ChangeTarget() 호출 — 회전만 업데이트.
    /// </summary>
    public struct CombatTargetChangedEvent
    {
        /// <summary> 공격하는 유닛의 Id. </summary>
        public readonly int UnitId;
        /// <summary> 새 타겟 엔티티의 Id. </summary>
        public readonly int NewTargetId;
        /// <summary> true=유닛, false=건물. </summary>
        public readonly bool NewTargetIsUnit;

        public CombatTargetChangedEvent(int unitId, int newTargetId, bool newTargetIsUnit)
        {
            UnitId = unitId;
            NewTargetId = newTargetId;
            NewTargetIsUnit = newTargetIsUnit;
        }
    }

    // ====================================================================
    // 멀티플레이 동기화 전용 전투/이동 이벤트 (NetworkCombatController → UnitView)
    // ====================================================================
    //
    // 왜 별도 이벤트인가?
    //   싱글플레이용 OnCombatStarted/등은 UnitView가 `if (!NetworkContext.IsNetworkActive)` 가드로 구독.
    //   멀티플레이에서 같은 이벤트를 재사용하면 가드 분리/이중 구독 관리가 복잡해지므로,
    //   별도 채널(OnNetworkCombatStarted 등)로 발행하고 UnitView가 별도 구독한다.
    //   NetworkCombatController는 UnitView를 직접 GetComponent로 꺼내지 않게 된다.

    /// <summary>
    /// 멀티플레이 전용 전투 시작 이벤트 데이터.
    /// 서버 NetworkCombatController가 StartCombatClientRpc 흐름에서 발행.
    /// UnitView는 OnNetworkCombatStarted 구독으로 StartCombatAnimation() 호출.
    /// </summary>
    public readonly struct NetworkCombatStartedEvent
    {
        /// <summary> 공격하는 유닛의 Id. </summary>
        public readonly int UnitId;
        /// <summary> 타겟 엔티티의 Id. </summary>
        public readonly int TargetId;
        /// <summary> true=유닛, false=건물. </summary>
        public readonly bool TargetIsUnit;

        public NetworkCombatStartedEvent(int unitId, int targetId, bool targetIsUnit)
        {
            UnitId = unitId;
            TargetId = targetId;
            TargetIsUnit = targetIsUnit;
        }
    }

    /// <summary>
    /// 멀티플레이 전용 타겟 변경 이벤트 데이터.
    /// 서버 NetworkCombatController가 ChangeTargetClientRpc 흐름에서 발행.
    /// UnitView는 OnNetworkCombatTargetChanged 구독으로 ChangeTarget() 호출 — 회전만 업데이트.
    /// </summary>
    public readonly struct NetworkCombatTargetChangedEvent
    {
        /// <summary> 공격하는 유닛의 Id. </summary>
        public readonly int UnitId;
        /// <summary> 새 타겟 엔티티의 Id. </summary>
        public readonly int TargetId;
        /// <summary> true=유닛, false=건물. </summary>
        public readonly bool TargetIsUnit;

        public NetworkCombatTargetChangedEvent(int unitId, int targetId, bool targetIsUnit)
        {
            UnitId = unitId;
            TargetId = targetId;
            TargetIsUnit = targetIsUnit;
        }
    }

    /// <summary>
    /// 멀티플레이 전용 전투 종료 이벤트 데이터.
    /// 서버 NetworkCombatController가 StopCombatClientRpc 흐름에서 발행.
    /// UnitView는 OnNetworkCombatStopped 구독으로 StopCombatAnimation() 호출.
    /// </summary>
    public readonly struct NetworkCombatStoppedEvent
    {
        /// <summary> 전투를 종료하는 유닛의 Id. </summary>
        public readonly int UnitId;

        public NetworkCombatStoppedEvent(int unitId)
        {
            UnitId = unitId;
        }
    }

    // ====================================================================
    // 멀티플레이 재경기 관련 이벤트
    // ====================================================================

    /// <summary>
    /// 멀티플레이 서버에서 게임 종료 시 재경기 버튼 설정을 알리는 이벤트.
    /// NetworkGameEndController.AnnounceWinnerClientRpc 흐름에서 발행.
    /// GameEndUI는 OnNetworkRematchAvailable 구독으로 SetupRematchButton 호출.
    /// </summary>
    public readonly struct NetworkRematchAvailableEvent
    {
        /// <summary> 랜덤 매칭 여부. true이면 GameEndUI에서 재경기 버튼을 숨길 수 있음. </summary>
        public readonly bool IsRandomMatch;

        public NetworkRematchAvailableEvent(bool isRandomMatch)
        {
            IsRandomMatch = isRandomMatch;
        }
    }

    /// <summary>
    /// 멀티플레이 상대 클라이언트에서 재경기 요청 팝업을 표시하라는 신호.
    /// NetworkGameEndController.NotifyRematchRequestedClientRpc 흐름에서 발행.
    /// RematchRequestPopup이 OnNetworkRematchRequested 구독으로 ShowRequest 호출.
    ///
    /// 응답(수락/거절)은 OnLocalRematchAccepted / OnLocalRematchDeclined로 발행.
    /// </summary>
    public readonly struct NetworkRematchRequestedEvent
    {
        // 현재 추가 데이터 필요 없음 — 단순 신호 역할.
        // 추후 요청자 표시 등이 필요하면 ClientId/PlayerName 등을 추가.
    }

    // OnLocalRematchRequested / OnLocalRematchAccepted / OnLocalRematchDeclined:
    //   UI(GameEndUI 버튼 / RematchRequestPopup 버튼) 측에서 발행하여
    //   NetworkGameEndController가 구독해 ServerRpc를 보내는 흐름이다.
    //   별도 payload가 없으므로 Subject<Unit>으로 발행한다.

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

        /// <summary>
        /// 유닛이 A* 이동 중 새 타일에 진입할 때 발행 (서버 전용).
        /// 발행: UnitView (MoveAlongPathV3에서 _isAStarMoving == true인 동안 타일 전환 직후)
        /// 구독: GameBootstrapper → CongestionMap.Increment (혼잡도 누적)
        /// </summary>
        public static readonly Subject<UnitEnteredTileEvent> OnUnitEnteredTile
            = new Subject<UnitEnteredTileEvent>();

        // ====================================================================
        // 전투 관련 이벤트
        // ====================================================================

        /// <summary>
        /// 엔티티(유닛/건물)가 공격했을 때 발행.
        /// 발행: UnitCombatUseCase
        /// 구독: UnitView (공격 애니메이션 재생)
        /// </summary>
        public static readonly Subject<EntityAttackedEvent> OnEntityAttacked = new Subject<EntityAttackedEvent>();

        /// <summary>
        /// 엔티티(유닛/건물)가 피격당하여 HP가 변경되었을 때 발행.
        /// 발행: UnitCombatUseCase (데미지 적용 직후)
        /// 구독: NetworkHealthSync (HP를 모든 클라이언트에 동기화)
        /// </summary>
        public static readonly Subject<EntityDamagedEvent> OnEntityDamaged = new();

        /// <summary>
        /// 공격자(유닛)의 로컬 타격 프레임(Animation Event OnAttackHit)이 발생했을 때 발행. 유닛 Id를 전달.
        /// 발행: UnitView.OnAttackHit (모든 클라이언트에서 로컬로 실행되는 Animation Event)
        /// 구독: HitPresentationQueue (해당 공격자의 보류 큐에서 피격 연출 1건을 방출)
        ///
        /// 왜 필요한가:
        ///   데미지·HP는 서버 권위로 즉시 갱신되지만, "맞는 화면"의 연출(HP 텍스트·피격 VFX·타격 반응)은
        ///   공격자가 실제로 칼을 휘두르는 순간(로컬 애니메이션의 타격 프레임)에 맞춰 터뜨려야 자연스럽다.
        ///   이 이벤트가 그 "타격 순간" 신호 역할을 한다. (전투 타격 타이밍 동기화 Phase 2 — 축 3)
        /// </summary>
        public static readonly Subject<int> OnLocalAttackHit = new Subject<int>();

        /// <summary>
        /// 유닛이 사망했을 때 발행.
        /// 발행: UnitCombatUseCase(싱글플레이 전투), NetworkCombatController(클라이언트 동기화).
        /// 구독: UnitView(GameObject 파괴), ProductionTicker(siege 목록 정리),
        ///       NetworkCombatController(서버 → EntityDiedClientRpc 전파).
        ///
        /// 사망 채널은 유닛(OnUnitDied) / 건물(OnBuildingDied) 두 갈래로 분리되어 있으며,
        /// 구독 측이 is 캐스트로 타입을 구분할 필요가 없도록 DTO도 강타입으로 노출한다.
        /// </summary>
        public static readonly Subject<UnitDiedEvent> OnUnitDied = new Subject<UnitDiedEvent>();

        /// <summary>
        /// 건물이 사망(파괴/철거)했을 때 발행.
        /// 발행: UnitCombatUseCase(전투 파괴), BuildingPlacementUseCase.DemolishBuilding(철거),
        ///       NetworkCombatController(클라이언트 동기화).
        /// 구독: BuildingFactory(GameObject 파괴), GameEndUseCase(Castle → 게임 종료),
        ///       FlowFieldService(경로 캐시 무효화), GameBootstrapper(살아있는 유닛 재경로),
        ///       ProductionTicker(생산건물 해제 + walkable 변경 후처리),
        ///       HexGridRenderer(MiningPost 파괴 시 금광 재표시).
        ///
        /// OnUnitDied와 짝을 이루는 건물 전용 사망 채널. 구독자는 BuildingData에 바로 접근 가능.
        /// </summary>
        public static readonly Subject<BuildingDiedEvent> OnBuildingDied = new Subject<BuildingDiedEvent>();

        // ====================================================================
        // 건물 관련 이벤트
        // ====================================================================

        /// <summary>
        /// 건물이 배치되었을 때 발행.
        /// 발행: BuildingPlacementUseCase
        /// 구독: BuildingFactory (프리팹 인스턴스 생성)
        /// </summary>
        public static readonly Subject<BuildingPlacedEvent> OnBuildingPlaced = new Subject<BuildingPlacedEvent>();

        /// <summary>
        /// 건물이 업그레이드되어 다음 단계로 교체되었을 때 발행.
        /// 발행: BuildingPlacementUseCase.UpgradeBuilding
        /// 구독: BuildingFactory (새 프리팹 생성 후 기존 GO 제거)
        /// </summary>
        public static readonly Subject<BuildingUpgradedEvent> OnBuildingUpgraded = new Subject<BuildingUpgradedEvent>();

        // ====================================================================
        // 생산 시스템 이벤트
        // ====================================================================

        /// <summary>
        /// 자원(골드) 변경 시 발행.
        /// 발행: ResourceUseCase
        /// 구독: ProductionPanelUI (골드 표시 갱신)
        /// </summary>
        public static readonly Subject<ResourceChangedEvent> OnResourceChanged = new Subject<ResourceChangedEvent>();

        /// <summary>
        /// 생산 시작 시 발행.
        /// 발행: UnitProductionUseCase
        /// 구독: ProductionPanelUI (프로그레스 바 시작)
        /// </summary>
        public static readonly Subject<ProductionStartedEvent> OnProductionStarted = new Subject<ProductionStartedEvent>();

        /// <summary>
        /// 유닛 생산 완료 시 발행.
        /// 발행: UnitProductionUseCase
        /// 구독: ProductionTicker (랠리포인트 자동 이동)
        /// </summary>
        public static readonly Subject<UnitProducedEvent> OnUnitProduced = new Subject<UnitProducedEvent>();

        /// <summary>
        /// 생산 큐 변경 시 발행.
        /// 발행: UnitProductionUseCase
        /// 구독: ProductionPanelUI (큐 슬롯 갱신)
        /// </summary>
        public static readonly Subject<ProductionQueueChangedEvent> OnProductionQueueChanged = new Subject<ProductionQueueChangedEvent>();

        /// <summary>
        /// 랠리포인트 변경 시 발행.
        /// 발행: UnitProductionUseCase
        /// 구독: ProductionTicker (마커 생성/이동/제거)
        /// </summary>
        public static readonly Subject<RallyPointChangedEvent> OnRallyPointChanged = new Subject<RallyPointChangedEvent>();

        // ====================================================================
        // 유닛 이동(Walk) 네트워크 동기화 이벤트
        // ====================================================================

        /// <summary>
        /// 유닛이 이동(Walk)을 시작했을 때 발행. 유닛 Id를 전달.
        /// 멀티플레이 서버 전용 — 싱글플레이에서는 발행하지 않음.
        /// 발행: UnitView.MoveAlongPath (서버에서 이동 코루틴 시작/재개 시)
        /// 구독: NetworkCombatController (SetUnitAnimState로 _animState=Walk 레벨 동기화 전파)
        /// </summary>
        public static readonly Subject<int> OnUnitWalkStarted = new Subject<int>();

        // OnUnitWalkStopped 제거 — Idle 상태가 없으므로 Walk 정지 이벤트 불필요.
        // Walk→Attack: StartCombatClientRpc에서 직접 처리.

        // ====================================================================
        // 전투 상태 변화 이벤트 (싱글플레이 전용)
        // 멀티플레이에서는 NetworkCombatController가 ClientRpc로 직접 UnitView 메서드 호출.
        // ====================================================================

        /// <summary>
        /// 멀티플레이 서버에서 유닛이 이동 중 처음으로 사거리 내 적을 감지했을 때 발행. 유닛 Id를 전달.
        /// MoveAlongPath에서 HasEnemyInRange가 true가 되는 첫 순간에만 발행.
        /// NetworkCombatController가 구독하여 즉시 StartCombatClientRpc를 전송.
        /// TickCombat을 기다리지 않고 공격 애니메이션을 즉시 시작하기 위함.
        /// </summary>
        public static readonly Subject<int> OnUnitEnteredCombat = new Subject<int>();

        /// <summary>
        /// 유닛이 전투 상태에 진입했을 때 발행.
        /// 발행: UnitCombatUseCase (싱글플레이에서 TryAttack 성공 시, 이전에 전투 중이 아니었던 경우)
        /// 구독: UnitView (StartCombatAnimation 호출)
        /// </summary>
        public static readonly Subject<CombatStartedEvent> OnCombatStarted = new Subject<CombatStartedEvent>();

        /// <summary>
        /// 전투 중 타겟이 변경되었을 때 발행.
        /// 발행: UnitCombatUseCase (싱글플레이에서 TryAttack 성공 시, 이전 타겟과 다른 경우)
        /// 구독: UnitView (ChangeTarget 호출 — 회전만 업데이트)
        /// </summary>
        public static readonly Subject<CombatTargetChangedEvent> OnCombatTargetChanged = new Subject<CombatTargetChangedEvent>();

        /// <summary>
        /// 유닛이 전투 상태에서 벗어났을 때 발행.
        /// 발행: UnitCombatUseCase (싱글플레이에서 사거리 내 적이 없어진 경우)
        /// 구독: UnitView (StopCombatAnimation 호출)
        /// </summary>
        public static readonly Subject<int> OnCombatStopped = new Subject<int>();

        // ====================================================================
        // 멀티플레이 전용 전투/이동 동기화 이벤트
        // ====================================================================
        // NetworkCombatController가 UnitView를 직접 GetComponent로 잡지 않도록
        // ClientRpc 핸들러 내부에서 본 이벤트를 발행하고, UnitView가 자신의 Id에 해당하는
        // 이벤트만 필터링하여 처리한다. 싱글플레이용 OnCombatStarted/등과 채널을 분리해
        // 가드/구독 관리가 단순해진다.

        /// <summary>
        /// 멀티플레이 서버에서 클라이언트에 전투 시작 명령 전파 시 발행.
        /// 발행: NetworkCombatController.StartCombatClientRpc → ApplyStartCombatWithRetry
        /// 구독: UnitView (자기 Id에 해당하면 StartCombatAnimation 호출)
        /// </summary>
        public static readonly Subject<NetworkCombatStartedEvent> OnNetworkCombatStarted
            = new Subject<NetworkCombatStartedEvent>();

        /// <summary>
        /// 멀티플레이 서버에서 클라이언트에 타겟 변경 명령 전파 시 발행.
        /// 발행: NetworkCombatController.ChangeTargetClientRpc
        /// 구독: UnitView (자기 Id에 해당하면 ChangeTarget 호출 — 회전만 업데이트)
        /// </summary>
        public static readonly Subject<NetworkCombatTargetChangedEvent> OnNetworkCombatTargetChanged
            = new Subject<NetworkCombatTargetChangedEvent>();

        /// <summary>
        /// 멀티플레이 서버에서 클라이언트에 전투 종료 명령 전파 시 발행.
        /// 발행: NetworkCombatController.StopCombatClientRpc
        /// 구독: UnitView (자기 Id에 해당하면 StopCombatAnimation 호출)
        /// </summary>
        public static readonly Subject<NetworkCombatStoppedEvent> OnNetworkCombatStopped
            = new Subject<NetworkCombatStoppedEvent>();

        // Walk 시작의 멀티플레이 전파는 별도 엣지 트리거 이벤트가 아니라
        // NetworkUnit._animState(=Walk) 레벨 동기화로 처리한다(스폰 레이스 유실 방지).

        // ====================================================================
        // 멀티플레이 재경기(Rematch) 이벤트
        // ====================================================================
        // NetworkGameEndController가 UI를 직접 호출하지 않도록 이벤트 채널로 분리.
        //   서버 → 클라이언트 방향: OnNetworkRematchAvailable / OnNetworkRematchRequested / OnNetworkRematchDeclined
        //   UI → NetworkGameEndController 방향: OnLocalRematchRequested / OnLocalRematchAccepted / OnLocalRematchDeclined
        //
        // 모두 단순 신호이므로 payload가 거의 없다. Subject<Unit>은 UniRx의 빈 신호 패턴.

        /// <summary>
        /// 게임 종료 시 재경기 버튼 활성화를 알리는 이벤트.
        /// 발행: NetworkGameEndController.AnnounceWinnerClientRpc
        /// 구독: GameEndUI (SetupRematchButton 호출)
        /// </summary>
        public static readonly Subject<NetworkRematchAvailableEvent> OnNetworkRematchAvailable
            = new Subject<NetworkRematchAvailableEvent>();

        /// <summary>
        /// 상대 플레이어로부터 재경기 요청을 받았음을 알리는 이벤트.
        /// 발행: NetworkGameEndController.NotifyRematchRequestedClientRpc
        /// 구독: RematchRequestPopup (ShowRequest 호출)
        /// </summary>
        public static readonly Subject<NetworkRematchRequestedEvent> OnNetworkRematchRequested
            = new Subject<NetworkRematchRequestedEvent>();

        /// <summary>
        /// 상대 플레이어가 재경기를 거절했음을 알리는 이벤트.
        /// 발행: NetworkGameEndController.NotifyRematchDeclinedClientRpc
        /// 구독: RematchRequestPopup (ShowDeclined 호출), GameEndUI (RestoreRematchButton 호출)
        /// </summary>
        public static readonly Subject<Unit> OnNetworkRematchDeclined = new Subject<Unit>();

        /// <summary>
        /// 서버가 재경기를 시작(Game 씬 재로드 직전)했음을 모든 클라이언트에 알리는 이벤트.
        /// 발행: NetworkGameEndController.NotifyRematchStartingClientRpc (서버 → 전체 클라이언트)
        /// 구독: GameEndUI (전역 로딩 인디케이터 표시 — "재경기 준비 중...")
        ///
        /// 왜 이벤트로 보내는가:
        ///   재경기 씬 전환은 서버(Infrastructure)에서 일어나지만, 로딩 인디케이터(UIManager)는
        ///   Presentation 레이어 소속이다. Infrastructure가 Presentation을 직접 참조하면
        ///   레이어 방향이 역행하므로, GameEvents(Application)를 경유하여 Presentation의
        ///   GameEndUI가 구독해 로딩을 띄우도록 한다(UI 규칙 L-3, L-4).
        /// </summary>
        public static readonly Subject<Unit> OnNetworkRematchStarting = new Subject<Unit>();

        /// <summary>
        /// 네트워크 게임 종료 후 로비로 복귀할 때 발행하는 이벤트.
        /// 발행: NetworkGameManager.BackToLobby (NGO Shutdown 완료 직후)
        /// 구독: GameEndUI (SceneLoader.Load 호출)
        ///
        /// 왜 이벤트로 보내는가:
        ///   BackToLobby는 Infrastructure 레이어지만, 씬 전환(SceneLoader)은 Presentation 소속이다.
        ///   레이어 방향을 보호하기 위해 GameEvents(Application)를 경유한다(UI 규칙 L-4 주석).
        /// </summary>
        public static readonly Subject<string> OnNetworkBackToLobby = new Subject<string>();

        /// <summary>
        /// 로컬에서 재경기 요청 버튼을 눌렀음을 알리는 이벤트.
        /// 발행: GameEndUI (SetupRematchButton 콜백)
        /// 구독: NetworkGameEndController (RequestRematchServerRpc 호출)
        /// </summary>
        public static readonly Subject<Unit> OnLocalRematchRequested = new Subject<Unit>();

        /// <summary>
        /// 로컬에서 재경기 요청 팝업의 "수락" 버튼을 눌렀음을 알리는 이벤트.
        /// 발행: RematchRequestPopup (OnAcceptClicked)
        /// 구독: NetworkGameEndController (AcceptRematchServerRpc 호출)
        /// </summary>
        public static readonly Subject<Unit> OnLocalRematchAccepted = new Subject<Unit>();

        /// <summary>
        /// 로컬에서 재경기 요청 팝업의 "거절" 버튼을 눌렀음을 알리는 이벤트.
        /// 발행: RematchRequestPopup (OnDeclineClicked)
        /// 구독: NetworkGameEndController (DeclineRematchServerRpc 호출)
        /// </summary>
        public static readonly Subject<Unit> OnLocalRematchDeclined = new Subject<Unit>();

        // ====================================================================
        // 게임 종료 이벤트
        // ====================================================================

        /// <summary>
        /// 게임 종료 시 발행. Castle 파괴로 승패 결정.
        /// 발행: GameEndUseCase
        /// 구독: GameEndUI (승리/패배 팝업 표시), GameUIManager (열린 UI 일괄 닫기)
        /// </summary>
        public static readonly Subject<GameEndEvent> OnGameEnd = new Subject<GameEndEvent>();

        // ====================================================================
        // 게임 생명주기 이벤트
        // ====================================================================

        // ====================================================================
        // 토스트 메시지 요청 이벤트
        // ====================================================================
        // ToastUI(Presentation)가 본 이벤트를 구독하여 ToastKey에 해당하는 메시지를 큐에 추가한다.
        // Infrastructure 레이어(NetworkBuildingController 등)가 ToastUI 정적 호출 대신 본 이벤트를 발행하면
        // Infrastructure → Presentation 직접 의존이 사라진다.

        /// <summary>
        /// 토스트 메시지 표시 요청.
        /// 발행: NetworkBuildingController(실패 알림), NetworkProductionController(실패 알림) 등
        /// 구독: ToastUI (큐에 키를 추가하고 표시)
        /// </summary>
        public static readonly Subject<ToastKey> OnToastRequested = new Subject<ToastKey>();

        /// <summary>
        /// 게임 시작(또는 재시작) 시 발행. LoadMap() 완료 후 발행.
        /// 모든 시스템 초기화가 끝난 상태이므로, UI가 안전하게 초기 상태를 설정할 수 있음.
        /// 발행: GameBootstrapper.LoadMap() 맨 마지막
        /// 구독: GameUIManager (등록된 모든 UI에 OnGameStarted() 호출)
        /// </summary>
        public static readonly Subject<Unit> OnGameStarted = new Subject<Unit>();
    }
}
