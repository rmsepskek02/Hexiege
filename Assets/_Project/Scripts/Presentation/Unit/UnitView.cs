// ============================================================================
// UnitView.cs
// 유닛의 비주얼(이동, 방향, 애니메이션 상태)을 담당하는 컴포넌트.
//
// 회전 동기화 방식:
//   - 회전은 즉시 스냅(Quaternion.Euler)으로 적용한다.
//   - 멀티플레이에서는 서버가 즉시 스냅한 rotation을 NetworkTransform이 클라이언트에 보간 전달한다.
//   - Red 클라이언트 관점 반전은 VisualRootProjector가 Visual Root에만 적용한다.
//
// 프리팹 구조:
//   Unit_{Type} (GameObject)
//     ├─ MeshFilter + MeshRenderer (or SkinnedMeshRenderer)
//     ├─ Animator (AnimatorController 설정)
//     └─ UnitView (이 스크립트)
//
// Animator 제어 방식:
//   - Walk: Animator.Play(StateWalk) + speed 제어 (1=재생, 0=정지)
//   - Attack: Animator.Play(StateAttack) — 클립 length 만큼 대기
//   - IsDead (bool): 사망 여부 (Animator 트랜지션 조건)
//   - 방향: transform.rotation Y축으로 표현 (Animator 파라미터 불필요)
//
// HexDirection → Y축 회전 각도 (FlatTop 기준):
//   E=90, NE=30, NW=330, W=270, SW=210, SE=150
// PointyTop 기준:
//   E=90, NE=30, NW=330, W=270, SW=210, SE=150
//   (동일 — HexDirection 0~5가 동일한 시각적 회전을 가짐)
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Coroutine, Animator).
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    // IUnitView 인터페이스 구현.
    //   UnitFactory(Infrastructure)가 Presentation 구체 타입 대신 인터페이스로 Initialize를 호출하도록.
    //   SetDependencies는 Infrastructure 구체 타입을 받으므로 인터페이스에 포함하지 않음.
    public class UnitView : MonoBehaviour, Hexiege.Application.IUnitView, IUnitActionPoseSource
    {
        // ====================================================================
        // Animator 파라미터 해시 (문자열 비교 방지)
        // ====================================================================

        // Animator.Play()에 사용할 스테이트 이름 해시
        private static readonly int StateWalk = Animator.StringToHash("Walk");
        private static readonly int StateAttack = Animator.StringToHash("Attack");
        // StateIdle 제거 — 이 게임에서 유닛은 항상 이동 또는 공격 중이므로 Idle 상태가 존재하지 않음.

        // 마지막으로 CrossFade를 지시한 애니메이션 상태 해시(StateWalk 또는 StateAttack)를 기억한다.
        // Animator.GetCurrentAnimatorStateInfo는 CrossFade 블렌딩 도중 "출발" 상태를 반환할 수 있어
        // 상태 판별이 어긋날 수 있으므로(이 클래스 상단 원칙 — Animator 상태 의존 제거),
        // 우리가 직접 지시한 상태를 로컬로 추적해 판별의 단일 출처로 삼는다.
        // 0 = 미설정(아직 어떤 CrossFade도 지시하지 않음). StringToHash 결과는 0이 될 수 없어 안전.
        private int _currentAnimStateHash = 0;

        // 사망은 bool 파라미터로 유지 (Animator 트랜지션 조건)
        private static readonly int AnimIsDead = Animator.StringToHash("IsDead");

        // ====================================================================
        // 원거리/근접 판정 경계
        // ====================================================================

        /// <summary>
        /// 원거리 유닛 판정 경계. AttackRange가 이 값 이상이면 원거리 유닛으로 간주하여
        /// 공격 시 트레이서(발사체) 연출을 재생한다. 미만이면 근접 유닛으로 즉시 피격 연출한다.
        /// UnitCombatUseCase의 근접 판정(AttackRange &lt; 1.0f)과 동일한 경계값이다.
        /// </summary>
        private const float RangedAttackThreshold = 1.0f;

        /// <summary>
        /// 이동 배율이 이 값 이하이면 빙결로 간주한다. 서버 권위 이동 writer는 유지한 채
        /// 위치 진행량만 0으로 만들고, 클라이언트에는 Frozen 애니메이션 상태를 복제한다.
        /// </summary>
        private const float MoveFreezeEpsilon = 0.0001f;

        // ====================================================================
        // HexDirection → Y축 회전 각도 매핑
        // NE(0)=60, E(1)=120, SE(2)=180, SW(3)=240, W(4)=300, NW(5)=0
        // FlatTop 헥스에서 각 방향의 실제 Unity 월드 각도 (atan2 기반).
        // 메시 자식 Y=0, _meshYOffset 제거 이후 기준값.
        // ====================================================================

        private static readonly float[] DirectionAngles = { 60f, 120f, 180f, 240f, 300f, 0f };
        // NE(0)=60, E(1)=120, SE(2)=180, SW(3)=240, W(4)=300, NW(5)=0
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        /// <summary> Walk로 전환할 때 블렌딩 시간 (초). </summary>
        [SerializeField] private float _idleToWalkBlend = 0.10f;

        /// <summary> 어떤 상태에서 Attack으로 전환할 때 블렌딩 시간 (초). </summary>
        [SerializeField] private float _toAttackBlend = 0.08f;

        [Tooltip("Attack → Walk 전환 시 CrossFade 블렌드 시간 (초). 규칙 3: 0.10초")]
        [SerializeField] private float _attackToWalkBlend = 0.10f;

        /// <summary>
        /// 공격 VFX를 재생할 기준 위치/회전을 제공하는 빈 자식 Transform(예: 총구 끝).
        /// 프리팹에서 무기 끝 같은 위치에 빈 GameObject("VfxSpawnPoint")를 두고 이 슬롯에 연결한다.
        /// 유닛은 공격 시 적 방향으로 이미 회전하므로, 이 Transform의 월드 회전이 곧 발사 방향이 된다.
        /// 연결하지 않은 유닛(피스톨러 외)은 기존처럼 유닛 발 위치(transform.position)로 폴백한다.
        /// </summary>
        [Tooltip("공격 VFX 스폰 기준 Transform(총구 등). 비어 있으면 유닛 발 위치로 폴백.")]
        [SerializeField] private Transform _vfxSpawnPoint;

        // ====================================================================
        // 내부 참조
        // ====================================================================

        /// <summary> 이 유닛에 연결된 Domain 데이터. </summary>
        private UnitData _unitData;

        /// <summary>
        /// VFX/SFX 등 표현 소비자만 사용하는 Visual Root projector.
        /// 이동, 사거리, 타겟, A2 pose source는 계속 Simulation Root(transform)를 사용한다.
        /// </summary>
        private VisualRootProjector _visualRootProjector;

        /// <summary> NetworkUnit.OnNetworkDespawn에서 클라이언트 이펙트 재생 시 타입 참조용. </summary>
        public UnitData UnitData => _unitData;

        /// <summary> Animator — 상태 전환 및 애니메이션 재생. </summary>
        private Animator _animator;

        /// <summary>
        /// 이동 UseCase 참조. 타일 도착 시 ProcessStep 호출용.
        /// GameBootstrapper에서 주입.
        /// </summary>
        private UnitMovementUseCase _movementUseCase;

        /// <summary> 전투 UseCase 참조. 이동 완료 후 자동 공격용. </summary>
        private UnitCombatUseCase _combatUseCase;

        /// <summary> 유닛 팩토리. 타겟 유닛의 실제 transform 조회용. </summary>
        private UnitFactory _unitFactory;

        /// <summary> 건물 팩토리. 타겟 건물의 실제 transform 조회용. </summary>
        private BuildingFactory _buildingFactory;

        /// <summary>
        /// 엔티티 월드 좌표 제공자.
        /// Phase 1(월드 좌표 직선 추적)에서 매 프레임 적의 최신 월드 위치를 조회하는 데 사용.
        /// 주입되지 않은 경우 Phase 1은 스킵되고 Phase 2(A* 재개)로 바로 진입한다.
        /// </summary>
        private IEntityPositionProvider _positionProvider;

        /// <summary> 현재 이동 코루틴. null이면 정지 상태. </summary>
        private Coroutine _moveCoroutine;

        /// <summary>
        /// 현재 이동 명령의 최종 목적지(예: 적 성, 랠리포인트).
        /// MoveTo(path)가 호출될 때 path[path.Count - 1]을 저장하며,
        /// 건물 변경 시 모든 유닛 경로를 재계산하는 트리거에서
        /// "이 유닛은 어디로 가고 있었나"를 외부에서 알 수 있도록 노출용으로 보관.
        ///
        /// HexCoord는 struct여서 default = (Q=0, R=0). (0,0)이 일반 타일일 수도 있으나
        /// _hasDestination 플래그를 함께 두어 "목적지 없음" 상태를 명확히 표현한다.
        /// </summary>
        private HexCoord _currentDestination = default;
        private bool _hasDestination = false;

        /// <summary> 외부 조회용: 현재 진행 중인 이동의 최종 목적지. </summary>
        public HexCoord CurrentDestination => _currentDestination;
        /// <summary> 외부 조회용: 진행 중인 이동이 있는지 여부 (목적지 유효성과 무관). </summary>
        public bool HasDestination => _hasDestination;

        /// <summary>
        /// 공격 중 타겟 추적용 Transform 참조.
        /// 공격 시작 시 타겟 GameObject의 Transform을 저장하여,
        /// Update()에서 매 프레임 이 참조의 position을 읽어 회전 갱신.
        /// 팩토리 딕셔너리를 매 프레임 조회하는 대신 Transform 참조를 직접 사용하여 효율적.
        /// 타겟 오브젝트 파괴 시 Unity가 자동으로 null 처리.
        /// </summary>
        private Transform _combatTargetTransform = null;

        /// <summary>
        /// Legacy 이동·정렬·추격 코드가 마지막으로 실제 선택한 월드 목표다.
        /// A2는 이 값을 읽기만 하며 이동 경로, transform, UnitData.Facing을 절대 수정하지 않는다.
        /// bool을 따로 두는 이유는 월드 원점도 Vector3 값으로는 표현 가능하기 때문이다.
        /// </summary>
        private Vector3 _unitActionShadowDesiredTarget;
        private bool _hasUnitActionShadowDesiredTarget;

        // 이동 명령/구간 scope는 B2 진단과 B3 권위 writer가 함께 사용한다. 경기 mode와
        // 무관하게 같은 지점에서만 증가시켜 Legacy/신규 경로가 서로 다른 회차를 보지 않게 한다.
        private ulong _movementCommandRevision;
        private ulong _movementSegmentRevision;
        // 하나의 최종 목적지에 대한 재탐색 진행성은 이동 코루틴보다 오래 살아야 한다.
        // 공성 ticker나 다른 외부 호출이 같은 목적지의 MoveTo를 다시 발행해도 이 객체를
        // 보존하여 무진전 예산을 우회하지 못하게 한다.
        private UnitNavigationObjective _navigationObjective;
        private ulong _navigationEnvironmentRevision;
        private UnitMovementIntentReason _movementIntentReason;
        private bool _movementPendingCommand;
        private bool _movementPendingSegment;
        private UnitMovementReducer _movementAuthorityReducer;
        private NetworkUnit _movementNetworkUnit;
        private UnitMovementPipelineMode _lockedMovementPipelineMode =
            UnitMovementPipelineMode.Legacy;
        // ReducerAuthoritative 경로에서 방금 계산한 trajectory가 현재 논리 waypoint를
        // 통과했는지 호출 측에 알려 준다. 위치를 타일 중심으로 스냅하지 않고도
        // ProcessStep/OnUnitEnteredTile을 정확히 한 번 실행하기 위한 프레임 결과다.
        private bool _lastMovementTrajectoryReachedWaypoint;
        private bool _lastMovementCandidateAcquiredTarget;

        private enum MovementWriteOutcome
        {
            Rejected = 0,
            Held = 1,
            Advanced = 2,
            RepathRequired = 3
        }

        // 회전 속도 (초당 각도) — 인스펙터에서 조정 가능.
        // 전투 중 타겟 추적, A* 이동 중 방향 전환, 전투 종료 후 정렬 모두 동일한 속도로 회전한다.
        // 270도/s 기준: 반대 방향(180도)에서도 약 0.67초 내 전환 완료.
        // SerializeField로 노출되어 있어 프리팹별/유닛별 회전감 차이를 데이터로 조정할 수 있다.
        [SerializeField] private float _rotationSpeed = 270f;

        // [방어적 백업] Transform 참조가 null이 될 경우 팩토리에서 재조회하기 위한 ID 백업.
        // 멀티플레이 타이밍 문제(StopCombatAnimation / ChangeTarget 호출 순서 역전 등)로
        // _combatTargetTransform이 null로 덮어써지는 경우에도 추적을 복구할 수 있도록 한다.
        private int _combatTargetId = -1;
        private bool _combatTargetIsUnit = true;
        private bool _movementRotationWriterOwnsRoot;

        // ────────────────────────────────────────────────────────────────────
        // 전투 추격(EnterCombatPursuitV3 실행 중) 여부 플래그.
        //
        // 왜 필요한가:
        //   IsInCombat()는 _combatTargetTransform != null만 판정합니다.
        //   _combatTargetTransform은 "실제 공격 시작"(StartCombatAnimation 호출) 시점에만 set되므로,
        //   적을 감지하고 추격 중인 단계(아직 공격 사거리에 미도달)에서는 false가 됩니다.
        //   이 상태에서 건물 생성/파괴 → RepathAllAliveUnits → OnPathInvalidated가 호출되면
        //   추격 중인 유닛도 repath 대상이 되어 추격 코루틴이 끊기고 다시 이동 코루틴이 시작되어
        //   유닛이 한 프레임 이상 시각적으로 멈춥니다.
        //
        // 사용법:
        //   - EnterCombatPursuitV3 진입 직후 true로 set.
        //   - 모든 yield break 직전 + 함수 끝에서 false로 reset.
        //     (Unity 코루틴(IEnumerator)에서는 try/finally가 동작하지 않으므로 명시적 처리 필요.)
        //   - IsInCombat()이 (_combatTargetTransform != null || _isInCombatPursuit)로 평가하여
        //     추격 단계도 "전투 중"으로 판정하게 합니다.
        // ────────────────────────────────────────────────────────────────────
        private bool _isInCombatPursuit = false;

        // ────────────────────────────────────────────────────────────────────
        // A* 이동 단계 여부 플래그 (혼잡도 시스템 연동용).
        //
        // 왜 필요한가:
        //   혼잡도 누적은 "정상 A* 이동" 중에만 의미가 있다. 전투 추격(EnterCombatPursuitV3)
        //   단계에서는 유닛이 적을 향해 직선 이동하므로 타일 진입 이벤트를 발행하면
        //   엉뚱한 타일에 혼잡도가 쌓일 수 있다.
        //
        // 사용법:
        //   - MoveAlongPathV3 시작 시 true로 set.
        //   - EnterCombatPursuitV3 진입 직전(detect 사거리 적 발견)에 false로 set.
        //   - 전투 종료 후 ResumeFromForwardTileV3로 새 path를 받으면 다시 true.
        //   - MoveCleanupAndCompleteV3(이동 완료)에서 false로 reset.
        // ────────────────────────────────────────────────────────────────────
        private bool _isAStarMoving = false;

        // ────────────────────────────────────────────────────────────────────
        // 건물 생성/파괴 시 유닛 멈춤 현상 제거용 — 부드러운 경로 교체 패턴.
        //
        // 왜 필요한가:
        //   OnPathInvalidated가 호출될 때 MoveTo(newPath)로 현재 이동 코루틴을 강제로
        //   StopCoroutine + StartCoroutine하면 1~2 프레임의 공백이 생겨 유닛이 잠깐 멈춘다.
        //   이를 피하기 위해 코루틴을 멈추지 않고 경로만 교체하는 예약 방식을 사용한다.
        //
        // 동작:
        //   _pendingPath: OnPathInvalidated가 새 경로를 여기에 "예약"만 한다.
        //                 코루틴은 그대로 돌면서 매 타일 도착 시점에 _pendingPath를 검사해
        //                 비어있지 않으면 path 변수를 교체하고 외부 while로 재진입한다.
        //                 코루틴 자체는 멈추지 않으므로 시각적 끊김 없음.
        //
        //   _currentNextTileCoord: 지금 Lerp 중인 "다음 도착 타일"의 좌표를 보관.
        //                          Lerp 시작 직전 set, Lerp 완료 직후 null.
        //                          OnPathInvalidated에서 "현재 향하는 타일에 건물이 새로 생긴
        //                          (=walkable이 아닌)" 케이스를 감지해야 할 때 사용한다.
        //                          이 경우는 예약 방식으로는 늦으므로 기존처럼 즉시 MoveTo한다.
        // ────────────────────────────────────────────────────────────────────
        private List<HexCoord> _pendingPath = null;
        private HexCoord? _currentNextTileCoord = null;

        /// <summary> 현재 이동 중인지 여부. InputHandler에서 이동 명령 중복 방지에 사용. </summary>
        public bool IsMoving => _moveCoroutine != null;

        /// <summary>
        /// 이동 코루틴이 일시 대기 중이거나 경로 환경 변경을 기다리는 경우까지 포함한
        /// 서버 navigation 목표 활성 여부다. 외부 ticker는 IsMoving 하나만 보고 재명령하지 않는다.
        /// </summary>
        public bool HasActiveNavigationObjective =>
            _navigationObjective != null && _navigationObjective.IsActive;

        /// <summary> 이 유닛의 Domain 데이터. 외부에서 읽기 전용. </summary>
        public UnitData Data => _unitData;

        /// <summary>
        /// 이동 완료 시 호출되는 콜백. 1회성 — 호출 후 자동 null 리셋.
        /// ProductionTicker에서 랠리→Castle 자동 이동 체인에 사용.
        /// </summary>
        public Action OnMoveComplete { get; set; }

        // ====================================================================
        // 매 프레임 업데이트
        // ====================================================================

        /// <summary>
        /// 공격 중 타겟 방향으로 매 프레임 회전 갱신.
        /// _combatTargetTransform이 null이면(공격 중 아님 또는 타겟 파괴) 즉시 리턴.
        ///
        /// 멀티플레이:
        ///   서버에서만 transform.rotation을 갱신하고,
        ///   NetworkTransform이 이 값을 클라이언트에 보간 전달.
        ///   클라이언트에서 직접 rotation을 쓰면 NetworkTransform과 충돌하여
        ///   이전에 제거한 "클라이언트 자체 회전 로직" 문제가 재발하므로 건너뜀.
        ///
        /// 싱글플레이:
        ///   NetworkContext.IsNetworkActive == false이므로 가드 없이 매 프레임 실행.
        /// </summary>
        private void Update()
        {
            // Transform 참조가 null인 경우, 백업 ID로 재조회를 시도한다.
            // 멀티플레이 타이밍 문제 등으로 _combatTargetTransform이 null로 덮어써진 경우
            // 다음 프레임에 자동으로 복구하기 위한 방어적 처리.
            if (_combatTargetTransform == null)
            {
                // 백업 ID가 유효하지 않으면 추적 대상 없음 — 즉시 리턴
                if (_combatTargetId == -1) return;

                // 백업 ID로 Transform 재조회 시도
                _combatTargetTransform = GetTargetTransform(_combatTargetId, _combatTargetIsUnit);

                // 재조회 실패 → 진짜 없는 타겟이므로 ID도 초기화하고 추적 완전 중단
                if (_combatTargetTransform == null)
                {
                    _combatTargetId = -1;
                    return;
                }
            }

            // 멀티플레이 클라이언트에서는 rotation을 직접 쓰지 않는다.
            // NetworkTransform이 서버 rotation을 자동으로 클라이언트에 동기화하므로,
            // 클라이언트가 직접 rotation을 쓰면 NetworkTransform과 충돌한다.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;

            // 이동 writer가 root rotation을 소유하는 동안 Legacy 공격 추적 writer는
            // 같은 frame에 root를 쓸 수 없다. 충돌 시 write를 억제하고 진단 실패로 남긴다.
            if (_movementRotationWriterOwnsRoot)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveAttackWriterOwnershipConflict(
                    _unitData != null ? _unitData.Id : -1);
#endif
                return;
            }

            // 매 프레임 타겟 방향으로 유닛을 부드럽게 회전시킨다.
            // Quaternion.RotateTowards: 현재 회전에서 목표 회전으로 초당 _rotationSpeed 각도만큼 이동.
            // 타겟이 멀리 있을수록 회전이 느리게 보이지만, 일반적인 유닛 이동 속도에서는 즉각 추적처럼 보임.
            // 타겟 교체 시(ChangeTarget) 방향이 크게 바뀌어도 자연스럽게 전환.
            float angle = CalculateAttackAngle(_combatTargetTransform.position);
            Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * Time.deltaTime
            );
        }

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// UnitFactory에서 프리팹 Instantiate 직후 호출.
        /// Domain 데이터를 전달받아 초기 상태 설정.
        ///
        /// 스폰 시 즉시 rotation 스냅:
        ///   서버: 즉시 스냅 → NetworkTransform이 클라이언트에 보간 전달.
        ///   클라이언트: 여기서 Simulation Root를 쓰지 않고 NetworkTransform 결과만 사용.
        ///             Red 관점 반전은 VisualRootProjector가 Visual Root에만 적용.
        /// </summary>
        /// <param name="unitData">이 유닛의 Domain 데이터</param>
        public void Initialize(UnitData unitData)
        {
            _unitData = unitData;
            _visualRootProjector = GetComponent<VisualRootProjector>();
            _movementNetworkUnit = GetComponent<NetworkUnit>();
            _movementCommandRevision = 0UL;
            _movementSegmentRevision = 0UL;
            _navigationObjective = null;
            _navigationEnvironmentRevision = 0UL;
            _isFrozenAnim = false;
            _isMovementHeldAnim = false;
            _movementIntentReason = UnitMovementIntentReason.None;
            _movementPendingCommand = false;
            _movementPendingSegment = false;
            _movementAuthorityReducer = new UnitMovementReducer();
            _lockedMovementPipelineMode = NetworkContext.IsNetworkActive
                ? NetworkContext.ActiveUnitMovementPipelineMode
                : UnitMovementPipelineMode.Legacy;

            // Animator 캐시 — 자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용
            _animator = GetComponentInChildren<Animator>();

            // 스폰 시 Facing 방향으로 즉시 rotation 설정.
            // 서버에서 즉시 스냅하면 NetworkTransform이 이 값을 클라이언트에 자동 보간 전달.
            // 순수 클라이언트는 canonical Simulation Root를 NetworkTransform에서만 받으므로 쓰지 않는다.
            int index = (int)_unitData.Facing;
            bool canWriteSimulationRoot =
                !NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer;
            if (canWriteSimulationRoot && index >= 0 && index < DirectionAngles.Length)
            {
                float spawnAngle = DirectionAngles[index];
                transform.rotation = Quaternion.Euler(0f, spawnAngle, 0f);
            }

            // ────────────────────────────────────────────────────────────────
            // [Phase 2 후속 — 적용 순서 틈 봉합] Initialize 완료 직후 애니메이션 상태 재적용.
            //
            //   왜 필요한가 (초급자용 설명):
            //     클라이언트에서 같은 GameObject의 NetworkUnit이 스폰되는 순간(OnNetworkSpawn) 서버의
            //     현재 애니메이션 상태를 즉시 적용하는데, 그 시점이 이 Initialize(=위에서 _animator를
            //     캐시한 지점)보다 "먼저" 실행될 수 있다. 그때 메시/Animator가 아직 준비되지 않았으면
            //     StartWalkAnimation이 조용히 반환하고, 레벨 값(NetworkVariable)은 그대로라 다시 통지가
            //     오지 않아 재시도가 없다 → 간헐적으로 걷기 애니메이션이 무음 실패한다.
            //
            //   해결:
            //     _animator를 캐시한 "지금" 같은 GameObject의 NetworkUnit에서 현재 상태 값을 한 번 더
            //     읽어 재적용한다. 조기 적용이 실패했더라도 준비된 Animator로 반드시 재적용되어 틈이 닫힌다.
            //     레벨(값 기반) 동기화라 "현재 값 재적용"은 멱등하여 무해하고, 서버(호스트)/싱글플레이는
            //     ReapplyAnimStateToView 내부 가드(IsSpawned/IsServer)로 스킵된다.
            //     (UnitView→NetworkUnit은 같은 프리팹 컴포넌트 접근 — 기존 관례이며 레이어 위반이 아니다.)
            // ────────────────────────────────────────────────────────────────
            var networkUnit = GetComponent<NetworkUnit>();
            if (networkUnit != null) networkUnit.ReapplyAnimStateToView();
        }

        /// <summary>
        /// 외부 의존성 주입. GameBootstrapper에서 모든 컴포넌트 생성 후 호출.
        /// </summary>
        public void SetDependencies(
            UnitMovementUseCase movementUseCase, UnitCombatUseCase combatUseCase,
            UnitFactory unitFactory = null, BuildingFactory buildingFactory = null,
            IEntityPositionProvider positionProvider = null)
        {
            _movementUseCase = movementUseCase;
            _combatUseCase = combatUseCase;
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;
            _positionProvider = positionProvider;

            // 전투 상태 변화 이벤트 구독 — 싱글플레이 전용.
            // 멀티플레이에서는 NetworkCombatController가 GameEvents.OnNetworkCombatStarted 등을 발행한다.
            if (!NetworkContext.IsNetworkActive)
            {
                // 전투 시작 → Walk 정지 후 Attack 루프 시작 + 타겟 방향 회전
                GameEvents.OnCombatStarted
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            // Walk → Attack 직접 전환 (Idle 없음)
                            StartCombatAnimation(e.TargetId, e.TargetIsUnit);
                        }
                    })
                    .AddTo(this);

                // 타겟 변경 → 회전만 업데이트 (애니메이션 재시작 없음)
                GameEvents.OnCombatTargetChanged
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            ChangeTarget(e.NewTargetId, e.NewTargetIsUnit);
                        }
                    })
                    .AddTo(this);

                // 전투 종료 → Attack 정지 후 Idle 전환
                GameEvents.OnCombatStopped
                    .Subscribe(unitId =>
                    {
                        if (_unitData != null && unitId == _unitData.Id)
                        {
                            StopCombatAnimation();
                        }
                    })
                    .AddTo(this);
            }
            else
            {
                // 멀티플레이 전용 전투/Walk 이벤트 구독.
                // NetworkCombatController가 ClientRpc 핸들러에서 UnitView를 직접 GetComponent로 잡지 않도록,
                // GameEvents.OnNetworkCombatStarted/등을 발행하면 본인 Id에 해당하는 이벤트만 처리한다.
                //
                // AddTo(this)로 GameObject 파괴 시 자동 구독 해제 — 재경기/씬 전환 시 누수 방지.

                // 멀티플레이 전투 시작
                GameEvents.OnNetworkCombatStarted
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            StartCombatAnimation(e.TargetId, e.TargetIsUnit);
                        }
                    })
                    .AddTo(this);

                // 멀티플레이 타겟 변경
                GameEvents.OnNetworkCombatTargetChanged
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            ChangeTarget(e.TargetId, e.TargetIsUnit);
                        }
                    })
                    .AddTo(this);

                // 멀티플레이 전투 종료
                GameEvents.OnNetworkCombatStopped
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            StopCombatAnimation();
                        }
                    })
                    .AddTo(this);

                // 멀티플레이 Walk 시작은 애니메이션 상태 레벨 동기화로 처리한다.
                //   서버가 NetworkUnit._animState = Walk로 쓰면, 클라이언트 NetworkUnit이
                //   값 변경(OnValueChanged) 및 스폰 시 현재 값을 읽어 UnitView.StartWalkAnimation()을
                //   직접 호출한다(스폰 레이스로 신호가 유실될 수 없음).
            }

            // 사망 이벤트 구독 — 이 유닛이 사망하면 GameObject 파괴.
            // 유닛 전용 이벤트(OnUnitDied)를 구독한다.
            // UnitDiedEvent.Unit이 UnitData 강타입이라 별도 캐스트 없이 직접 비교만 하면 된다.
            GameEvents.OnUnitDied
                .Subscribe(e =>
                {
                    // 이 UnitView 인스턴스가 들고 있는 _unitData와 일치할 때만 자기 자신을 파괴.
                    // 다른 유닛의 사망 이벤트는 무시.
                    if (_unitData != null && e.Unit == _unitData)
                    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        RetireMovementShadowUnit();
#endif
                        // 사망한 root의 마지막 이동/공격 목표가 adapter에 남아 다음 관측으로 재사용되지 않게 한다.
                        ClearUnitActionShadowDesiredTarget();

                        // 사망 시 speed 복원 후 IsDead bool 설정 (Animator 트랜지션)
                        if (_animator != null)
                        {
                            _animator.speed = 1f;
                            _animator.SetBool(AnimIsDead, true);
                        }

                        // ────────────────────────────────────────────────────
                        // 멀티플레이 클라이언트:
                        //   사망 이펙트 재생과 GameObject 파괴를 모두
                        //   NetworkUnit.OnNetworkDespawn()에서 처리한다.
                        //   (서버가 NetworkObject.Despawn(destroy:true)를 호출하면
                        //    NGO가 이 클라이언트의 OnNetworkDespawn을 부르고 GO를 파괴한다.)
                        //   따라서 클라이언트는 여기서 아무것도 하지 않고 즉시 반환한다.
                        //
                        //   레이어 규칙 준수:
                        //     Presentation 레이어에서는 Unity.Netcode를 직접 참조하지 않고
                        //     Application 레이어의 NetworkContext 정적 홀더만 사용한다.
                        // ────────────────────────────────────────────────────
                        if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
                        {
                            // 멀티플레이 클라이언트 경로 — 여기서는 아무것도 하지 않고 반환.
                            //   (이펙트 재생/파괴는 NetworkUnit.OnNetworkDespawn이 담당)
                            return;
                        }

                        // 서버 또는 싱글플레이: 사망 이펙트를 여기서 즉시 재생.
                        // EffectManager/AudioManager가 아직 없는 경우를 대비해 ?. 연산자로 안전 처리.
                        EffectManager.Instance?.PlayUnitDeath(
                            _unitData.Type,
                            PresentationTransform.position);                                        // VFX
                        AudioManager.Instance?.PlayUnitDeathSfx(_unitData.Type);                     // SFX (규칙 15 — VFX와 짝)

                        // GameObject 파괴 경로 분기:
                        //   - 싱글플레이: NGO가 없으므로 여기서 직접 Destroy한다.
                        //   - 멀티플레이 서버: 파괴하지 않는다.
                        //       NetworkCombatController가 NetworkObject.Despawn(destroy:true)로
                        //       서버/클라이언트 GameObject를 함께 파괴하기 때문이다.
                        //       (여기서 Destroy를 호출하면 Despawn 대상이 사라져 NGO 경고가 발생한다.)
                        if (!NetworkContext.IsNetworkActive)
                        {
                            Destroy(gameObject);
                        }
                    }
                })
                .AddTo(this);
        }

        // ====================================================================
        // 방향 처리 — Y축 회전 기반
        // ====================================================================

        /// <summary>
        /// 타겟의 실제 월드 위치를 기반으로 Atan2 정밀 공격 각도를 계산.
        /// HexCoord 기반이 아닌 transform.position 기반이므로 Lerp 이동 중에도 정확한 방향.
        /// 메시 자식 Y=0으로 통일 후 보정값 불필요 — Atan2 결과를 그대로 사용.
        /// </summary>
        /// <param name="targetWorldPos">타겟의 실제 월드 좌표 (transform.position).</param>
        /// <returns>Y축 회전 각도 (도 단위).</returns>
        private float CalculateAttackAngle(Vector3 targetWorldPos)
        {
            Vector3 dir = targetWorldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
                return transform.eulerAngles.y;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// targetId와 타입으로 실제 GameObject의 world position 조회.
        /// 이미 파괴된 경우 현재 forward 방향 fallback.
        /// </summary>
        private Vector3 GetTargetWorldPos(int targetId, bool targetIsUnit)
        {
            GameObject obj = targetIsUnit
                ? _unitFactory?.GetUnitObject(targetId)
                : _buildingFactory?.GetBuildingObject(targetId);

            if (obj != null) return obj.transform.position;

            // fallback: 팩토리 미등록 또는 이미 파괴된 경우 현재 forward 방향
            return transform.position + transform.forward;
        }

        /// <summary>
        /// targetId와 타입으로 실제 GameObject의 Transform 조회.
        /// 팩토리에 등록되지 않았거나 이미 파괴된 경우 null 반환.
        /// </summary>
        private Transform GetTargetTransform(int targetId, bool targetIsUnit)
        {
            GameObject obj = targetIsUnit
                ? _unitFactory?.GetUnitObject(targetId)
                : _buildingFactory?.GetBuildingObject(targetId);
            return obj != null ? obj.transform : null;
        }

        /// <summary>
        /// 로컬 화면에 그려지는 절대 pose. 표현 소비자만 사용하며 Simulation Root를 변경하지 않는다.
        /// migration 전 프리팹은 projector가 없으므로 기존 root pose로 안전하게 폴백한다.
        /// </summary>
        private Transform PresentationTransform
        {
            get
            {
                if (_visualRootProjector == null)
                    _visualRootProjector = GetComponent<VisualRootProjector>();
                return _visualRootProjector != null
                    ? _visualRootProjector.PresentationTransform
                    : transform;
            }
        }

        private static Transform GetPresentationTransform(Transform simulationTransform)
        {
            if (simulationTransform == null) return null;
            VisualRootProjector projector =
                simulationTransform.GetComponent<VisualRootProjector>();
            return projector != null
                ? projector.PresentationTransform
                : simulationTransform;
        }

        private Vector3 GetTargetPresentationWorldPos(int targetId, bool targetIsUnit)
        {
            Transform simulationTransform = GetTargetTransform(targetId, targetIsUnit);
            Transform presentationTransform =
                GetPresentationTransform(simulationTransform);
            return presentationTransform != null
                ? presentationTransform.position
                : PresentationTransform.position + PresentationTransform.forward;
        }

        /// <summary>
        /// A2 observer가 요청한 타겟과 현재 root pose를 한 번에 읽는다.
        /// transform.position/forward는 이미 Legacy와 NetworkTransform이 사용하는 좌표이므로
        /// ViewConverter를 다시 적용하지 않는다. 제공자가 타겟을 찾지 못해 zero를 반환하거나
        /// 마지막 Legacy 목표가 정리된 상태라면 오래된 방향을 추측하지 않고 false로 끝낸다.
        /// </summary>
        public bool TryCaptureUnitActionPose(EntityRef target, out UnitActionPoseSample sample)
        {
            sample = default;
            if (_unitData == null || !_unitData.IsAlive || !target.IsValid)
                return false;
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
                return false;

            // provider의 Vector3.zero sentinel은 실제 월드 원점과 구분할 수 없다. 따라서 요청한
            // EntityRef로 factory 오브젝트가 존재하는지 직접 확인한 뒤 raw Transform 위치를 읽는다.
            Transform targetTransform = GetTargetTransform(target.Id, target.Kind == EntityKind.Unit);
            if (targetTransform == null) return false;
            Vector3 targetPosition = targetTransform.position;

            Vector3 attackerPosition = transform.position;
            Vector3 facing = transform.forward;
            if (!IsFiniteXZ(attackerPosition) || !IsFiniteXZ(facing)
                || !IsFiniteXZ(targetPosition) || !IsFiniteXZ(_unitActionShadowDesiredTarget))
                return false;

            return UnitActionPoseSample.TryCreate(
                target,
                attackerPosition.x, attackerPosition.z,
                facing.x, facing.z,
                targetPosition.x, targetPosition.z,
                _hasUnitActionShadowDesiredTarget,
                _unitActionShadowDesiredTarget.x, _unitActionShadowDesiredTarget.z,
                out sample);
        }

        /// <summary>
        /// Legacy가 이미 계산한 실제 목표만 복사한다. A2를 위해 새 경로나 좌표 변환을 만들지 않는다.
        /// </summary>
        private void CaptureUnitActionShadowDesiredTarget(Vector3 target)
        {
            if (!IsFiniteXZ(target))
            {
                ClearUnitActionShadowDesiredTarget();
                return;
            }

            _unitActionShadowDesiredTarget = target;
            _hasUnitActionShadowDesiredTarget = true;
        }

        private void ClearUnitActionShadowDesiredTarget()
        {
            _unitActionShadowDesiredTarget = default;
            _hasUnitActionShadowDesiredTarget = false;
            _movementIntentReason = UnitMovementIntentReason.None;
        }

        private static bool IsFiniteXZ(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void BeginMovementShadowCommand(UnitMovementIntentReason intentReason)
        {
            // Commit diagnostic revisions only when a Legacy writer is actually observed.
            // Empty or rejected commands therefore cannot create reducer-visible gaps.
            _movementPendingCommand = true;
            _movementPendingSegment = false;
            _movementIntentReason = intentReason;
        }

        private void BeginMovementShadowSegment(UnitMovementIntentReason intentReason)
        {
            if (_movementCommandRevision == 0UL || _movementPendingCommand)
            {
                _movementPendingCommand = true;
                _movementPendingSegment = false;
                _movementIntentReason = intentReason;
                return;
            }

            _movementPendingSegment = true;
            _movementIntentReason = intentReason;
        }

        private bool TryCommitMovementScope()
        {
            if (_movementPendingCommand)
            {
                if (_movementCommandRevision == ulong.MaxValue)
                    return false;

                _movementCommandRevision++;
                _movementSegmentRevision = 1UL;
                _movementPendingCommand = false;
                _movementPendingSegment = false;
                return true;
            }

            if (!_movementPendingSegment)
                return _movementCommandRevision != 0UL
                    && _movementSegmentRevision != 0UL;

            if (_movementSegmentRevision == ulong.MaxValue)
                return false;

            _movementSegmentRevision++;
            _movementPendingSegment = false;
            return true;
        }

        /// <summary>
        /// A*·전투 복귀·추격이 공유하는 유일한 이동 pose writer seam이다.
        /// Legacy 경기에는 기존 후보 pose를 그대로 쓰고, ReducerAuthoritative 경기에는
        /// 서버 reducer가 허용한 frame만 위치를 전진시킨다. 클라이언트는 어떤 분기에서도
        /// Simulation Root를 쓰지 않는다.
        /// </summary>
        private MovementWriteOutcome ApplyMovementAuthorityFrame(
            Vector3 desiredTarget,
            Vector3 candidatePosition,
            Quaternion targetRotation,
            UnitMovementIntentReason fallbackIntentReason,
            bool targetAcquirePriority,
            Vector3? nextTrajectoryTarget = null,
            float maximumTravelDistanceWorld = -1f,
            IReadOnlyList<HexCoord> logicalCorridorPath = null,
            int logicalCorridorWaypointIndex = -1,
            Func<Vector3, bool> candidateAcquirePredicate = null)
        {
            _lastMovementTrajectoryReachedWaypoint = false;
            _lastMovementCandidateAcquiredTarget = false;
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveClientWriteAttempt();
#endif
                return MovementWriteOutcome.Rejected;
            }

            bool reducerAuthoritative = _lockedMovementPipelineMode
                == UnitMovementPipelineMode.ReducerAuthoritative;
            if (!reducerAuthoritative)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveLegacyWriterSelection();
#endif
                transform.position = candidatePosition;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeed * Time.deltaTime);
                return MovementWriteOutcome.Advanced;
            }

            if (_movementAuthorityReducer == null)
                _movementAuthorityReducer = new UnitMovementReducer();
            if (!IsFiniteXZ(transform.position)
                || !IsFiniteXZ(candidatePosition)
                || !IsFiniteXZ(desiredTarget)
                || !IsFiniteQuaternion(transform.rotation)
                || !IsFiniteQuaternion(targetRotation))
            {
                return MovementWriteOutcome.Rejected;
            }
            if (_movementIntentReason == UnitMovementIntentReason.None)
                BeginMovementShadowSegment(fallbackIntentReason);
            if (!TryCommitMovementScope())
                return MovementWriteOutcome.Rejected;

            Vector3 facing = transform.forward;
            Vector3 positionBefore = transform.position;
            Quaternion rotationBefore = transform.rotation;
            float expectedDelta = CalculateMovementPositionDeltaXZ(
                transform.position, candidatePosition);
            float planningDistance = maximumTravelDistanceWorld >= 0f
                ? maximumTravelDistanceWorld
                : expectedDelta;
            UnitTrajectoryStep trajectoryStep = default;
            bool hasTrajectoryStep = false;
            UnitMovementEvaluation evaluation;

            // AcquireTarget와 실제 endpoint는 기존 NoIntent adapter를 그대로 사용한다.
            // 정상 이동 의도만 pure trajectory planner를 통과시켜, reducer가 raw waypoint
            // 방향 점프가 아니라 회전 속도 안에서 만들어진 실제 이동 접선을 판정하게 한다.
            float desiredDistance = CalculateMovementPositionDeltaXZ(
                transform.position, desiredTarget);
            bool endpoint = desiredDistance
                    <= (float)UnitMovementEvaluationAdapter.PositionTolerance
                && planningDistance
                    <= (float)UnitMovementEvaluationAdapter.PositionTolerance;
            if (targetAcquirePriority || endpoint)
            {
                evaluation = UnitMovementEvaluationAdapter.Evaluate(
                    _movementAuthorityReducer,
                    _movementCommandRevision,
                    _movementSegmentRevision,
                    _movementIntentReason,
                    transform.position.x,
                    transform.position.z,
                    facing.x,
                    facing.z,
                    desiredTarget.x,
                    desiredTarget.z,
                    targetAcquirePriority,
                    planningDistance,
                    Time.timeAsDouble);
            }
            else
            {
                // C# definite-assignment 규칙은 && 뒤쪽 TryCreate가 실행되지 않을 수 있다고
                // 판단한다. planner 호출 전에 세 값을 default로 명시해, 실패 입력에서는
                // validPureInput=false로 닫히면서도 컴파일러가 모든 경로의 할당을 증명하게 한다.
                WorldPointXZ currentPoint = default;
                ActionDirectionXZ currentFacing = default;
                WorldPointXZ waypoint = default;
                bool validPureInput = WorldPointXZ.TryCreate(
                        transform.position.x,
                        transform.position.z,
                        out currentPoint)
                    && ActionDirectionXZ.TryCreate(
                        facing.x,
                        facing.z,
                        out currentFacing)
                    && WorldPointXZ.TryCreate(
                        desiredTarget.x,
                        desiredTarget.z,
                        out waypoint);
                bool hasNext = nextTrajectoryTarget.HasValue;
                WorldPointXZ nextWaypoint = default;
                if (validPureInput && hasNext)
                {
                    Vector3 next = nextTrajectoryTarget.Value;
                    validPureInput = IsFiniteXZ(next)
                        && WorldPointXZ.TryCreate(next.x, next.z, out nextWaypoint);
                }

                bool planned = validPureInput
                    && UnitServerTrajectoryPlanner.TryPlan(
                        currentPoint,
                        currentFacing,
                        waypoint,
                        hasNext,
                        nextWaypoint,
                        planningDistance,
                        _rotationSpeed * Time.deltaTime,
                        HexMetrics.TileHeight * 0.35d,
                        out trajectoryStep)
                    && trajectoryStep.IsValid;
                if (!planned)
                    return MovementWriteOutcome.Rejected;

                Vector3 plannedCandidate = transform.position;
                plannedCandidate.x = (float)trajectoryStep.CandidatePosition.X;
                plannedCandidate.z = (float)trajectoryStep.CandidatePosition.Z;
                if (logicalCorridorPath != null
                    && !IsTrajectoryPointSweepInsidePath(
                        transform.position,
                        plannedCandidate,
                        logicalCorridorPath,
                        logicalCorridorWaypointIndex))
                {
                    // 다음 선분 look-ahead가 현재 corridor 형상에서는 너무 공격적으로
                    // 코너를 잘랐다면 먼저 현재 waypoint만 향하는 보수적 후보를 한 번 만든다.
                    // 이 후보도 corridor 밖이면 Transform을 쓰지 않고 재탐색을 요청한다.
                    bool fallbackPlanned = hasNext
                        && UnitServerTrajectoryPlanner.TryPlan(
                            currentPoint,
                            currentFacing,
                            waypoint,
                            false,
                            default,
                            planningDistance,
                            _rotationSpeed * Time.deltaTime,
                            0d,
                            out trajectoryStep)
                        && trajectoryStep.IsValid;
                    if (!fallbackPlanned)
                        return MovementWriteOutcome.RepathRequired;

                    plannedCandidate.x = (float)trajectoryStep.CandidatePosition.X;
                    plannedCandidate.z = (float)trajectoryStep.CandidatePosition.Z;
                    if (!IsTrajectoryPointSweepInsidePath(
                        transform.position,
                        plannedCandidate,
                        logicalCorridorPath,
                        logicalCorridorWaypointIndex))
                    {
                        return MovementWriteOutcome.RepathRequired;
                    }
                }

                bool acquireFromCandidate = candidateAcquirePredicate != null
                    && candidateAcquirePredicate(plannedCandidate);

                hasTrajectoryStep = true;
                ActionDirectionXZ reducerDirection =
                    trajectoryStep.TrajectoryDirection;
                // 150도 이상 반전은 planner가 위치 진행을 0으로 막는다. reducer에도
                // raw 목표 방향을 전달해 AlignToMove 상태가 명시적으로 게시되게 한다.
                if (trajectoryStep.RequiresStationaryAlignment
                    && !ActionDirectionXZ.TryCreate(
                        desiredTarget.x - transform.position.x,
                        desiredTarget.z - transform.position.z,
                        out reducerDirection))
                {
                    return MovementWriteOutcome.Rejected;
                }

                UnitMovementReducerStatus status = _movementAuthorityReducer.Evaluate(
                    _movementAuthorityReducer.Snapshot.Revision,
                    _movementCommandRevision,
                    _movementSegmentRevision,
                    _movementIntentReason,
                    true,
                    currentFacing,
                    reducerDirection,
                    acquireFromCandidate,
                    Time.timeAsDouble,
                    out UnitMovementDecision decision);
                evaluation = new UnitMovementEvaluation(
                    status,
                    decision,
                    _movementIntentReason,
                    true,
                    currentFacing,
                    reducerDirection,
                    false,
                    true,
                    acquireFromCandidate);
            }

            bool accepted = evaluation.ReducerInvoked
                && (evaluation.Status == UnitMovementReducerStatus.Accepted
                    || evaluation.Status == UnitMovementReducerStatus.Duplicate)
                && evaluation.Decision.IsValid;
            if (!accepted)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveFrame(
                    _unitData != null ? _unitData.Id : -1,
                    _movementNetworkUnit != null ? _movementNetworkUnit.NetworkObjectId : 0UL,
                    _unitData != null ? _unitData.Type : default,
                    _unitData != null ? _unitData.Team : default,
                    _movementIntentReason,
                    _movementCommandRevision,
                    _movementSegmentRevision,
                    evaluation,
                    positionBefore,
                    rotationBefore,
                    transform.position,
                    transform.rotation,
                    authorityWriterSelected: true);
#endif
                return MovementWriteOutcome.Rejected;
            }

            if (_movementNetworkUnit == null
                || !_movementNetworkUnit.PublishMovementSnapshot(
                    _movementAuthorityReducer.Snapshot))
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObservePublicationFailure(
                    _unitData != null ? _unitData.Id : -1);
#endif
                return MovementWriteOutcome.Rejected;
            }

            if (hasTrajectoryStep)
            {
                ActionDirectionXZ trajectoryDirection =
                    trajectoryStep.TrajectoryDirection;
                float trajectoryAngle = Mathf.Atan2(
                    (float)trajectoryDirection.X,
                    (float)trajectoryDirection.Z) * Mathf.Rad2Deg;
                // planner가 이미 270°/s 제한을 적용했으므로 여기서 다시 보간하면
                // 한 프레임의 위치 방향과 rotation이 달라진다. 같은 접선을 원자 적용한다.
                transform.rotation = Quaternion.Euler(0f, trajectoryAngle, 0f);
                SynchronizeDomainFacingFromSimulationForward();
            }
            else if (evaluation.Decision.Phase == UnitMovementPhase.AlignToMove
                || evaluation.Decision.Phase == UnitMovementPhase.Move)
            {
                ActionDirectionXZ desiredDirection =
                    evaluation.DesiredMoveDirection;
                float authorityAngle = Mathf.Atan2(
                    (float)desiredDirection.X,
                    (float)desiredDirection.Z) * Mathf.Rad2Deg;
                Quaternion authorityTargetRotation =
                    Quaternion.Euler(0f, authorityAngle, 0f);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    authorityTargetRotation,
                    _rotationSpeed * Time.deltaTime);
            }

            MovementWriteOutcome outcome = MovementWriteOutcome.Held;
            if (evaluation.Decision.AllowsMovement
                || evaluation.CommitsAcquireCandidate)
            {
                if (hasTrajectoryStep)
                {
                    Vector3 plannedPosition = transform.position;
                    plannedPosition.x = (float)trajectoryStep.CandidatePosition.X;
                    plannedPosition.z = (float)trajectoryStep.CandidatePosition.Z;
                    transform.position = plannedPosition;
                    _lastMovementTrajectoryReachedWaypoint =
                        trajectoryStep.ReachedCurrentWaypoint;
                    _lastMovementCandidateAcquiredTarget =
                        evaluation.CommitsAcquireCandidate;
                }
                else
                {
                    transform.position = candidatePosition;
                }
                outcome = MovementWriteOutcome.Advanced;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            UnitMovementAuthorityObserver.ObserveFrame(
                _unitData != null ? _unitData.Id : -1,
                _movementNetworkUnit != null ? _movementNetworkUnit.NetworkObjectId : 0UL,
                _unitData != null ? _unitData.Type : default,
                _unitData != null ? _unitData.Team : default,
                _movementIntentReason,
                _movementCommandRevision,
                _movementSegmentRevision,
                evaluation,
                positionBefore,
                rotationBefore,
                transform.position,
                transform.rotation,
                authorityWriterSelected: true);
#endif
            if (evaluation.Decision.Phase == UnitMovementPhase.NoIntent)
            {
                _movementIntentReason = UnitMovementIntentReason.None;
                _movementPendingCommand = false;
                _movementPendingSegment = false;
            }
            return outcome;
        }

        private static float CalculateMovementPositionDeltaXZ(
            Vector3 from,
            Vector3 to)
        {
            float deltaX = to.x - from.x;
            float deltaZ = to.z - from.z;
            return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        /// <summary>
        /// reducer mode의 이산 Domain Facing을 이미 commit한 SimulationFacing에 가장 가까운
        /// 헥스 방향으로 갱신한다. waypoint 시작 시 raw 선분 방향을 먼저 쓰지 않기 때문에
        /// Domain 방향이 실제 서버 rotation보다 한 단계 앞서는 상태가 생기지 않는다.
        /// </summary>
        private void SynchronizeDomainFacingFromSimulationForward()
        {
            if (_unitData == null) return;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.000001f) return;
            forward.Normalize();

            Vector3 centerDomain = HexMetrics.HexToWorld(_unitData.Position);
            Vector3 centerView = ViewConverter.ToView(centerDomain);
            float bestDot = float.NegativeInfinity;
            HexDirection bestDirection = _unitData.Facing;
            for (int index = 0; index < HexDirectionExtensions.Count; index++)
            {
                HexDirection domainDirection = (HexDirection)index;
                HexCoord neighbor = _unitData.Position + domainDirection.Offset();
                Vector3 neighborView = ViewConverter.ToView(
                    HexMetrics.HexToWorld(neighbor));
                Vector3 direction = neighborView - centerView;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.000001f) continue;

                float dot = Vector3.Dot(forward, direction.normalized);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    // 기존 A* writer와 같은 진영 변환 의미를 보존한다.
                    bestDirection = ViewConverter.FlipDirection(domainDirection);
                }
            }

            _unitData.Facing = bestDirection;
        }

        /// <summary>
        /// Collider 없이 Simulation Root point의 선분만 검사한다. 선분을 짧게 나눠
        /// 각 표본이 현재 A* path의 인접 타일 영역 중 하나에 속하는지 확인한다.
        /// 다른 유닛은 경로 차단 요소가 아니며, path 밖 타일과 새로 막힌 중간 타일만 거부한다.
        /// </summary>
        private bool IsTrajectoryPointSweepInsidePath(
            Vector3 fromView,
            Vector3 toView,
            IReadOnlyList<HexCoord> path,
            int waypointIndex)
        {
            if (path == null
                || path.Count < 2
                || waypointIndex < 1
                || waypointIndex >= path.Count
                || !IsFiniteXZ(fromView)
                || !IsFiniteXZ(toView))
            {
                return false;
            }

            float distance = CalculateMovementPositionDeltaXZ(fromView, toView);
            float sampleSpacing = Mathf.Max(HexMetrics.TileHeight * 0.1f, 0.01f);
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / sampleSpacing));
            int firstAllowed = Mathf.Max(0, waypointIndex - 1);
            int lastAllowed = Mathf.Min(path.Count - 1, waypointIndex + 1);
            for (int sample = 0; sample <= sampleCount; sample++)
            {
                float t = sample / (float)sampleCount;
                Vector3 viewPoint = Vector3.Lerp(fromView, toView, t);
                Vector3 domainPoint = ViewConverter.FromView(viewPoint);
                HexCoord tile = HexMetrics.WorldToHex(domainPoint);

                bool inCorridor = false;
                for (int index = firstAllowed; index <= lastAllowed; index++)
                {
                    if (path[index] == tile)
                    {
                        inCorridor = true;
                        break;
                    }
                }
                if (!inCorridor) return false;

                // 마지막 목적지는 성/건물일 수 있어 기존 의미상 non-walkable을 허용한다.
                // 그 외 표본 타일이 경기 중 새로 막혔다면 point sweep을 즉시 거부한다.
                bool isFinalDestination = tile == path[path.Count - 1];
                if (!isFinalDestination
                    && _movementUseCase != null
                    && !_movementUseCase.IsWalkable(tile))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 살아 있는 유닛이 이동/추격을 정상 종료할 때 마지막 권위 상태를 NoIntent로 닫는다.
        /// 종료 경계가 publication에 실패하면 이전 Move/Align 상태를 숨기지 않고 fail-closed한다.
        /// </summary>
        private bool TryCloseMovementAuthorityIntent(
            UnitMovementIntentReason fallbackIntentReason,
            string boundary)
        {
            if (_unitData == null || !_unitData.IsAlive)
                return true;

            if (_movementAuthorityReducer != null)
            {
                UnitMovementSnapshot current = _movementAuthorityReducer.Snapshot;
                if (current.HasAcceptedObservation
                    && current.Phase == UnitMovementPhase.NoIntent
                    && current.CommandRevision == _movementCommandRevision
                    && current.SegmentRevision == _movementSegmentRevision)
                {
                    _movementIntentReason = UnitMovementIntentReason.None;
                    _movementPendingCommand = false;
                    _movementPendingSegment = false;
                    return true;
                }
            }

            MovementWriteOutcome outcome = ApplyMovementAuthorityFrame(
                transform.position,
                transform.position,
                transform.rotation,
                fallbackIntentReason,
                targetAcquirePriority: false);
            if (outcome != MovementWriteOutcome.Rejected)
                return true;

            Debug.LogError(
                $"[UAS-MOVE] 이동 종료 NoIntent 게시 거부. " +
                $"unitId={_unitData.Id}, boundary={boundary}, " +
                $"commandRevision={_movementCommandRevision}, " +
                $"segmentRevision={_movementSegmentRevision}");
            return false;
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z)
                && !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void RetireMovementShadowUnit()
        {
            if (_unitData == null
                || _movementNetworkUnit == null
                || !_movementNetworkUnit.IsSpawned)
                return;

            UnitMovementShadowObserver.RetireUnit(
                _unitData.Id,
                _movementNetworkUnit.NetworkObjectId);
        }

        private static float CalculateMovementShadowPositionDeltaXZ(
            Vector3 from,
            Vector3 to)
            => CalculateMovementPositionDeltaXZ(from, to);

        private UnitMovementShadowObserver.LegacyFrameToken BeginMovementShadowLegacyFrame(
            Vector3 desiredTarget,
            UnitMovementIntentReason fallbackIntentReason,
            float expectedPositionDeltaWorld)
        {
            if (!NetworkContext.IsNetworkActive
                || !NetworkContext.IsNetworkServer
                || _unitData == null
                || !_unitData.IsAlive
                || _movementNetworkUnit == null
                || !_movementNetworkUnit.IsSpawned)
            {
                return default;
            }

            if (_movementIntentReason == UnitMovementIntentReason.None)
                BeginMovementShadowSegment(fallbackIntentReason);

            if (!TryCommitMovementScope())
                return default;
            return UnitMovementShadowObserver.BeginLegacyFrame(
                _unitData.Id,
                _movementNetworkUnit.NetworkObjectId,
                _unitData.Type,
                _unitData.Team,
                _movementCommandRevision,
                _movementSegmentRevision,
                _movementIntentReason,
                transform.position,
                transform.rotation,
                desiredTarget,
                targetAcquirePriority: false,
                expectedPositionDeltaWorld: expectedPositionDeltaWorld);
        }

        private void CompleteMovementShadowLegacyFrame(
            UnitMovementShadowObserver.LegacyFrameToken token)
        {
            UnitMovementShadowObserver.CompleteLegacyFrame(
                token,
                transform.position,
                transform.rotation);
        }
#endif

        // ====================================================================
        // 이동
        // ====================================================================

        /// <summary>
        /// 경로를 따라 유닛을 시각적으로 이동시킴.
        /// 멀티플레이에서는 서버만 실행 — 클라이언트는 NetworkTransform이
        /// 서버 위치를 자동으로 보간·동기화하므로 이동 로직을 실행하지 않음.
        /// </summary>
        /// <param name="path">A* 경로 (시작점 포함)</param>
        public void MoveTo(List<HexCoord> path)
        {
            MoveToInternal(path, UnitMovementIntentReason.AStarPath, startNewCommand: true);
        }

        /// <summary>
        /// Starts or restarts the existing Legacy coroutine while keeping B2 command/segment
        /// correlation explicit. The diagnostic scope does not affect the path or writer.
        /// </summary>
        private void MoveToInternal(
            List<HexCoord> path,
            UnitMovementIntentReason intentReason,
            bool startNewCommand)
        {
            // 멀티플레이에서는 서버만 이동 로직을 실행.
            // 클라이언트는 NetworkTransform이 서버 위치를 자동으로 보간·동기화.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;

            // 중복 command 판정은 AlignPathStartToTransform보다 먼저 수행한다. 정렬 helper는
            // 필요하면 Domain Position을 보정할 수 있으므로, 이미 활성인 같은 목표를 무시하면서
            // 그 helper를 먼저 호출하면 "억제된 명령이 상태를 변경"하는 모순이 생긴다.
            if (startNewCommand
                && path != null
                && path.Count > 0
                && _navigationObjective != null
                && _navigationObjective.IsActive
                && _navigationObjective.Matches(path[path.Count - 1]))
            {
                _navigationObjective.NotifyEnvironmentChanged(
                    _navigationEnvironmentRevision);
                if (_navigationObjective.SuppressesDuplicateCommand(
                        path[path.Count - 1], _navigationEnvironmentRevision))
                {
                    if (_moveCoroutine != null)
                    {
                        _pendingPath = path;
                        _navigationObjective.MarkWaitingRepath();
                    }
                    return;
                }
            }

            // ────────────────────────────────────────────────────────────
            // [Phase 3 이동 보정] 경로 출발점을 유닛의 실제 transform 위치와 정합.
            //
            //   왜 필요한가:
            //     RequestMove는 경로를 "도메인 타일"(_unitData.Position) 기준으로 계산한다.
            //     그런데 유닛이 두 타일 사이를 이동하는 도중(transform이 도메인 타일보다 목적지 쪽으로
            //     앞서 있음)에 새 경로가 발급되면, 경로 첫 타일(path[1])이 실제 위치보다 "뒤"에 놓여
            //     첫 걸음이 목적지 역방향으로 향한다("뒤로 밀림", 규칙 11 위반).
            //
            //   동작:
            //     AlignPathStartToTransform이 이 "역방향 첫 스텝" 케이스만 감지해 앞쪽 타일 기준으로
            //     경로를 재발급한다. 정방향/정합 상태는 원본 path를 그대로 반환하므로 일반 이동은
            //     전혀 건드리지 않는다.
            // ────────────────────────────────────────────────────────────
            if (startNewCommand)
                _navigationObjective?.Cancel();

            path = AlignPathStartToTransform(path);

            // 외부에서 이 유닛이 어디로 가는지 알 수 있도록 최종 목적지를 저장.
            // 빈 경로/null 안전 처리: path가 비정상이면 코루틴 동작이 알아서 종료된다.
            if (path != null && path.Count > 0)
            {
                HexCoord destination = path[path.Count - 1];

                _currentDestination = destination;
                _hasDestination = true;

                if (startNewCommand)
                {
                    _navigationObjective = new UnitNavigationObjective(
                        destination, path, _navigationEnvironmentRevision);
                }
            }

            // 여기까지 왔다는 것은 새 objective 또는 같은 objective의 명시적 segment 교체다.
            // 중복 command는 위에서 기존 코루틴을 보존한 채 반환했으므로, 이 시점부터만
            // 예약 상태를 비우고 기존 코루틴을 교체할 수 있다.
            _pendingPath = null;
            _currentNextTileCoord = null;

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }

            if (startNewCommand)
                BeginMovementShadowCommand(intentReason);
            else
                BeginMovementShadowSegment(intentReason);

            // 이동/전투 흐름 코루틴 시작.
            //   - GameSystemRules.md 규칙 1~18을 그대로 따른다.
            //   - 외부 while 안에 타일 순회를 직접 인라인하여 가독성을 높였다.
            _moveCoroutine = StartCoroutine(MoveAlongPathV3(path));
        }

        /// <summary>
        /// [Phase 3 이동 보정] 새 경로의 출발점을 유닛의 실제 transform 위치와 정합시킨다.
        ///
        /// 배경(왜 필요한가 — 초급자용 설명):
        ///   경로 계산(RequestMove)은 항상 "도메인 타일"(_unitData.Position)을 출발점으로 삼는다.
        ///   그런데 유닛의 도메인 위치는 "타일 중심에 도착했을 때"만 갱신되므로, 유닛이 두 타일
        ///   사이를 이동하는 도중(=transform이 도메인 타일보다 목적지 쪽으로 앞서 있음)에 새 경로가
        ///   발급되면, 경로의 첫 타일(path[1])이 실제 위치보다 "뒤"에 놓일 수 있다. 이 경우 유닛이
        ///   첫 걸음을 목적지 반대 방향(뒤)으로 내딛어 "뒤로 밀리는" 것처럼 보인다.
        ///   (규칙 11: A* 재개 시 뒤쪽 타일로 복귀 금지 — 이 위반을 강제로 바로잡는다.)
        ///
        /// 동작:
        ///   1) 첫 스텝(transform → path[1])이 최종 목적지 기준 "역방향"(XZ 내적&lt;0)인지 판정한다.
        ///      역방향이 아니면(정상) 원본 path를 그대로 반환 — 일반 이동은 전혀 건드리지 않는다.
        ///   2) 역방향이면: 실제 transform 위치 기준 "앞쪽 가장 가까운 타일"을 FindForwardClosestTile로
        ///      구하고, 도메인 위치도 그 타일로 정합(ProcessStep)시킨 뒤, 그 출발점에서 목적지까지
        ///      경로를 재발급(RequestMove)한다.
        ///      (전투 종료 정렬의 ResumeFromForwardTileV3에서 이미 검증된 패턴을 그대로 재사용한다.)
        ///
        /// 반환: 보정된 경로(역방향 케이스) 또는 원본 경로(정상/보정 불가 케이스).
        /// </summary>
        /// <param name="path">MoveTo가 받은 원본 경로(시작점 포함).</param>
        /// <returns>보정 경로 또는 원본 경로.</returns>
        private List<HexCoord> AlignPathStartToTransform(List<HexCoord> path)
        {
            // 방어: 경로 비정상 / 의존성 없음 / 유닛 사망 → 원본 그대로.
            if (path == null || path.Count < 2) return path;
            if (_movementUseCase == null || _unitData == null || !_unitData.IsAlive) return path;

            // 최종 목적지 — "앞쪽/뒤쪽" 판정의 기준.
            HexCoord finalTarget = path[path.Count - 1];

            // path[1] 중심과 최종 목적지 중심을 뷰 좌표로 변환.
            Vector3 step1View = TileCenterView(path[1]);
            Vector3 finalView = TileCenterView(finalTarget);

            // 첫 스텝 방향(transform → path[1])과 목적지 방향(transform → finalTarget)의 XZ 내적.
            //   내적 ≥ 0 : 정방향(또는 옆) — 정상 이동이므로 보정하지 않는다.
            //   내적 < 0 : 첫 스텝이 목적지 반대쪽 — "뒤로 밀림" 버그이므로 보정 대상.
            Vector3 toStep1 = step1View - transform.position; toStep1.y = 0f;
            Vector3 toFinal = finalView - transform.position; toFinal.y = 0f;
            float dot = toStep1.x * toFinal.x + toStep1.z * toFinal.z;
            if (dot >= 0f) return path;   // 정방향 — 원본 그대로.

            // ── 여기부터 역방향(보정) 케이스 ──

            // 실제 transform 위치 → 도메인 좌표 → 앞쪽 가장 가까운 타일.
            //   (ResumeFromForwardTileV3의 forwardTile 계산과 동일한 방식.)
            Vector3 domainPos = ViewConverter.FromView(transform.position);
            HexCoord forwardTile = _movementUseCase.FindForwardClosestTile(domainPos, finalTarget);

            // 도메인 위치 정합 — forwardTile이 현재 도메인과 다를 때만 ProcessStep.
            //   (같은 타일이면 생략 — ResumeFromForwardTileV3와 동일 규약.)
            //   ProcessStep은 위치 역인덱스(NotifyUnitMoved) 갱신과 OnUnitMoved 이벤트 발행만 하며,
            //   혼잡도 누적 이벤트(OnUnitEnteredTile)는 발행하지 않는다. 따라서 혼잡도/점유 부작용이 없다.
            if (forwardTile != _unitData.Position)
                _movementUseCase.ProcessStep(_unitData, _unitData.Position, forwardTile);

            // 앞쪽 타일에서 목적지까지 경로 재발급.
            List<HexCoord> corrected = _movementUseCase.RequestMove(_unitData, finalTarget);

            // 재발급 실패(도착/경로 없음) 시 원본 유지 — 최소한 기존 동작을 보존한다.
            if (corrected == null || corrected.Count < 2) return path;
            return corrected;
        }

        /// <summary>
        /// 타일 좌표를 유닛이 서는 뷰 좌표(타일 중심 + 유닛 높이 보정)로 변환하는 소형 헬퍼.
        /// (도메인 → 월드(HexToWorld) → 뷰(ToView) + UnitYOffset. 반복되던 3줄 변환을 모은 것.)
        /// </summary>
        private static Vector3 TileCenterView(HexCoord coord)
        {
            Vector3 world = HexMetrics.HexToWorld(coord);
            Vector3 view = ViewConverter.ToView(world);
            view.y += HexMetrics.UnitYOffset;
            return view;
        }

        /// <summary>
        /// 외부(예: GameBootstrapper / FlowField 갱신 트리거)에서
        /// "현재 진행 중인 경로가 무효화됐으니 동일 destination으로 다시 RequestMove해서
        ///  새 경로로 이동을 재시작하라"고 알려주는 메서드.
        ///
        /// 호출 조건:
        ///   - HasDestination == true (이미 이동 중인 유닛만)
        ///   - 서버(또는 싱글플레이)에서만. 클라이언트는 NetworkTransform 보간 동기화 결과만 본다.
        ///
        /// 동작:
        ///   1) 현재 destination을 그대로 두고 RequestMove 재호출.
        ///   2) 결과 path가 유효하면 MoveTo(path)로 새 경로 시작 — 기존 코루틴 자동 정지.
        ///   3) 경로 못 찾으면 그대로 두고 다음 트리거를 기다림(=대기).
        ///
        /// 주의:
        ///   - 새 코루틴(MoveAlongPathV3)이 도입된 후에도 동일하게 동작.
        ///   - 전투 중 유닛에 대해 호출되어도 안전: MoveTo가 코루틴을 교체하므로 전투 루프가
        ///     자연스럽게 새 path 기반 외부 while로 대체됨.
        /// </summary>
        public void OnPathInvalidated()
        {
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            if (!_hasDestination) return;
            if (_unitData == null || !_unitData.IsAlive) return;
            if (_movementUseCase == null) return;

            // 건물 생성·파괴 한 번을 이 유닛이 관측한 walkability revision으로 기록한다.
            // ulong 고갈은 현실적으로 도달하지 않지만 wrap하면 오래된 revision과 구분할 수 없으므로
            // 최대값에서 고정한다.
            if (_navigationEnvironmentRevision < ulong.MaxValue)
                _navigationEnvironmentRevision++;
            _navigationObjective?.NotifyEnvironmentChanged(
                _navigationEnvironmentRevision);

            // ────────────────────────────────────────────────────────────
            // 전투 중인 유닛에 대한 repath 차단.
            //
            // 왜 필요한가:
            //   건물 건설/파괴 시 GameBootstrapper.RepathAllAliveUnits()가
            //   살아있는 모든 유닛의 OnPathInvalidated()를 호출한다.
            //   여기서 그대로 MoveTo(newPath)를 부르면 기존 전투 코루틴이 종료되지 않은 채
            //   새 이동 코루틴이 시작되어 두 코루틴이 동시에 실행된다.
            //   결과: 전투 타일과 별도로 다음 타일에 이동 슬롯이 추가로 잡혀 유령 점유가 생긴다.
            //
            // 전투 상태 판정:
            //   - 근접: _v2AttackSlotTargetCoord.HasValue (EnterMeleePursuit 동안 set).
            //   - 원거리: _v2InStationaryCombat (EnterStationaryCombat 진입 직전에 true).
            //
            // 동작:
            //   전투 중이면 즉시 return. 전투 종료 후 ResumeFromForwardTile / 자연 진행으로 path가
            //   다시 정상 흐름을 타기 때문에, 이 시점에 굳이 새 path를 강제로 끼워 넣지 않는다.
            // ────────────────────────────────────────────────────────────
            if (IsInCombat())
            {
                return;
            }

            HexCoord dest = _currentDestination;
            List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, dest);
            if (newPath == null || newPath.Count < 2)
            {
                // newPath가 null/짧으면 현재 위치에서 그대로 대기. 다음 트리거에서 다시 시도.
                return;
            }

            // ────────────────────────────────────────────────────────────
            // 부드러운 경로 교체 — 건물 생성/파괴 시 멈춤 현상 제거.
            //
            // 기본 동작:
            //   _pendingPath에 새 경로를 "예약"만 한다.
            //   MoveAlongPathV3 코루틴은 그대로 돌면서, 매 타일 도착 직후
            //   _pendingPath를 검사해 비어있지 않으면 path 변수를 교체하고
            //   외부 while로 재진입한다. 코루틴 자체는 중단되지 않으므로
            //   StopCoroutine + StartCoroutine에 의한 1~2 프레임 공백이 사라진다.
            //
            // 예외(즉시 MoveTo 필요):
            //   지금 Lerp 중인 다음 도착 타일(_currentNextTileCoord)에
            //   바로 건물이 새로 생긴 경우. 이때는 다음 타일 도착까지 기다리면
            //   유닛이 건물을 뚫고 그 타일 중심까지 이동해 버린다.
            //   따라서 이 케이스에만 기존처럼 코루틴을 즉시 교체한다.
            // ────────────────────────────────────────────────────────────
            if (_currentNextTileCoord.HasValue
                && !_movementUseCase.IsWalkable(_currentNextTileCoord.Value))
            {
                // 다음 도착 타일이 막혔으므로 부드러운 교체 불가 — 즉시 코루틴 재시작.
                MoveToInternal(
                    newPath,
                    UnitMovementIntentReason.BlockedRepath,
                    startNewCommand: false);
                return;
            }

            // 그 외 일반 케이스: 코루틴을 그대로 두고 새 path만 예약.
            // 이후 매 타일 도착 시점에 MoveAlongPathV3가 이 값을 소비한다.
            _pendingPath = newPath;
        }

        /// <summary>
        /// 현재 유닛이 전투 중(공격 또는 전투 이동 중)인지 판정.
        /// 공격 타겟 추적 상태로 판정합니다.
        ///
        /// 판정 기준:
        ///   - `_combatTargetTransform != null`: 전투/추격 코루틴이 진행 중일 때
        ///     `StartCombatAnimation`이 set하고, `StopCombatAnimation`이 null로 리셋합니다.
        ///   - `_isInCombatPursuit == true`: 추격 단계(아직 공격 사거리 미도달)
        ///     인 동안 EnterCombatPursuitV3가 set합니다.
        ///     이 플래그가 없으면 추격 중에 건물 변화 → RepathAllAliveUnits이 트리거되어
        ///     OnPathInvalidated가 새 path로 코루틴을 교체하고, 결과적으로 추격 코루틴이
        ///     중단되어 유닛이 한 프레임 시각적으로 멈추는 버그가 발생합니다.
        ///
        /// 호출 측(OnPathInvalidated)은 이 결과로 "이동 코루틴을 새로 시작하면 안 되는 상태"를
        /// 빠르게 식별하여, 전투 중 RepathAllAliveUnits에 의한 강제 종료를 방지합니다.
        /// </summary>
        private bool IsInCombat()
        {
            // 공격 애니메이션 중(_combatTargetTransform != null) 또는
            // 추격 코루틴 실행 중(_isInCombatPursuit) — 둘 중 하나만 참이어도 전투 상태.
            return _combatTargetTransform != null || _isInCombatPursuit;
        }

        /// <summary>
        /// 이동 중 "멈춰서 무언가를 해야 하는 대상"이 사거리 안에 있는지 판정한다(상태머신 진입 조건).
        ///
        /// 일반 전투 유닛: 감지 사거리 내 "적"이 있으면 true → 전투 이동/공격 흐름 진입.
        /// 힐러(IsHealer): 힐 사거리 내 "부상 아군"이 있으면 true → 힐 루프 진입.
        ///   힐러는 적을 감지/공격하지 않으므로 적 탐색을 아예 타지 않는다(적 공격 흐름과 분리).
        ///
        /// 이 한 메서드로 두 종류의 진입 판정을 통일하여, 이동 코루틴의 감지 지점들이
        /// 유닛 역할에 맞는 올바른 대상을 검사하도록 한다.
        /// </summary>
        private bool ShouldEngage()
        {
            if (_combatUseCase == null || _unitData == null || !_unitData.IsAlive) return false;

            return _unitData.IsHealer
                ? _combatUseCase.HasInjuredAllyInHealRange(_unitData)
                : _combatUseCase.HasEnemyInDetectRange(_unitData);
        }

        /// <summary>
        /// 아직 Transform에 적용하지 않은 서버 trajectory candidate를 기준으로 감지한다.
        /// 현재 pose를 임시로 덮어쓰지 않으므로, candidate까지 이동한 바로 그 틱에
        /// 위치를 commit하고 AcquireTarget으로 전환할 수 있다.
        /// </summary>
        private bool ShouldEngageAt(Vector3 candidatePosition)
        {
            if (_combatUseCase == null || _unitData == null || !_unitData.IsAlive)
                return false;

            return _unitData.IsHealer
                ? _combatUseCase.HasInjuredAllyInHealRangeAt(
                    _unitData, candidatePosition)
                : _combatUseCase.HasEnemyInDetectRangeAt(
                    _unitData, candidatePosition);
        }

        // ====================================================================
        // MoveAlongPathV3 — 단일 상태 머신
        // ====================================================================
        // GameSystemRules.md 새 규칙 7~10에 따른 통합 상태 머신:
        //
        //   [A* 이동] : Legacy는 타일 중심 Lerp, ReducerAuthoritative는 실제 거리 기반
        //              연속 trajectory와 waypoint 통과 checkpoint를 사용한다.
        //              두 모드 모두 매 프레임 감지하며 서버만 pose를 쓴다.
        //              감지되면 → [전투 이동]으로 전환.
        //
        //   [전투 이동] : 적의 월드 위치를 향해 직선 이동 (근접/원거리 동일).
        //                매 프레임 HasEnemyInRange 체크.
        //                사거리 진입 → [공격]으로 전환.
        //                적이 detect 사거리 이탈 → [A* 이동 재개].
        //                타겟 사망 → detect 내 다른 적 재선택 또는 [A* 이동 재개].
        //
        //   [공격] : 멈춘 채로 공격/쿨다운 루프.
        //            적이 사거리 이탈 → [전투 이동] 또는 [A* 이동 재개].
        //
        // 특징:
        //   - 근접/원거리(AttackKind) 차이는 공격 사거리 수치뿐.
        //   - 타일 점유 없이 단순 타일 중심 이동 — 다른 유닛과 같은 타일에 들어가도 문제 없음.
        //   - 원거리도 detect 진입 후 전투 이동으로 접근한다.
        //
        // 헬퍼 구조:
        //   MoveAlongPathV3            — 메인 코루틴 (서버 가드 + path 순회 + cleanup)
        //   ├─ EnterCombatPursuitV3    — [전투 이동] 적 월드 위치로 직선 이동 + 사거리 도달 시 공격
        //   │     └─ EnterCombatLoopV3 — [공격] 사거리 안에 있는 동안 공격/쿨다운 루프
        //   ├─ ResumeFromForwardTileV3 — [A* 이동 재개] 현재 위치 기준 앞쪽 타일에서 새 경로 시작
        //   └─ MoveCleanupAndCompleteV3 — 코루틴 정리 + OnMoveComplete 콜백
        // ====================================================================

        /// <summary>
        /// [2026-05-11 재설계] 단일 상태 머신 메인 코루틴.
        ///
        /// 동작 흐름:
        ///   1) 멀티플레이 가드 — 서버가 아니면 즉시 yield break.
        ///   2) path 유효성 체크 — 비정상이면 cleanup 후 종료.
        ///   3) Walk 애니메이션 시작 (1회만 CrossFade).
        ///   4) 외부 while: path를 순회한다. Legacy는 중심 Lerp, 신규 권위 모드는
        ///      다음 선분 look-ahead를 포함한 거리 기반 trajectory로 진행한다.
        ///      - 매 프레임 detect 사거리 내 적 확인.
        ///      - 감지되면 EnterCombatPursuitV3로 전투 이동 → 공격 흐름 위임.
        ///      - 전투 종료 후 ResumeFromForwardTileV3로 앞쪽 타일에서 새 경로 받아 재개.
        ///   5) 정상 완주 또는 사망이면 cleanup.
        /// </summary>
        /// <param name="path">A* 경로 (시작점 포함, 길이 ≥ 2).</param>
        private bool TryAcceptRepath(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> candidatePath,
            string source,
            out UnitRepathDecision decision)
        {
            decision = guard != null
                ? guard.Evaluate(Time.frameCount, candidatePath)
                : UnitRepathDecision.RejectedInvalidPath;
            if (decision == UnitRepathDecision.AcceptedNextFrame)
            {
                _navigationObjective?.MarkWaitingRepath();
                return true;
            }

            if (decision == UnitRepathDecision.RejectedFrameBudget
                && candidatePath != null)
            {
                _pendingPath = candidatePath as List<HexCoord>
                    ?? new List<HexCoord>(candidatePath);
                _navigationObjective?.MarkWaitingRepath();
                return false;
            }

            _navigationObjective?.MarkBlocked();
            LogRepathFailClosed(guard, candidatePath, source, decision);
            return false;
        }

        private void LogRepathFailClosed(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> candidatePath,
            string source,
            UnitRepathDecision decision,
            string failedWaypointOverride = null)
        {
            string unitId = _unitData != null ? _unitData.Id.ToString() : "null";
            UnitPathSignature.TryCreate(
                candidatePath, out UnitPathSignature candidateSignature);
            string failedWaypoint = failedWaypointOverride
                ?? (candidatePath != null && candidatePath.Count > 1
                    ? candidatePath[1].ToString()
                    : "unavailable");
            Debug.LogError(
                $"[UAS-MOVE][REPATH-FAIL-CLOSED] unitId={unitId}, " +
                $"commandRevision={_movementCommandRevision}, " +
                $"segmentRevision={_movementSegmentRevision}, " +
                $"source={source}, decision={decision}, frame={Time.frameCount}, " +
                $"failedWaypoint={failedWaypoint}, " +
                $"previousPath={FormatPathSignature(guard?.PreviousPath ?? default)}, " +
                $"currentPath={FormatPathSignature(guard?.CurrentPath ?? default)}, " +
                $"candidatePath={FormatPathSignature(candidateSignature)}, " +
                $"acceptedInFrame={guard?.AcceptedInFrame ?? 0}, " +
                $"acceptedWithoutProgress={guard?.AcceptedWithoutProgress ?? 0}");
        }

        private static string FormatPathSignature(UnitPathSignature signature)
            => signature.IsValid
                ? $"{signature.WaypointCount}:{signature.OrderedHash:x16}"
                : "invalid";

        private void ObserveNavigationProgress(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> path)
        {
            if (_navigationObjective != null && _navigationObjective.IsActive)
                _navigationObjective.ObserveProgress(path);
            else
                guard?.ObserveProgress(path);
        }

        private IEnumerator MoveAlongPathV3(List<HexCoord> path)
        {
            // ── 멀티플레이 가드 ────────────────────────────────────────────
            // 서버가 아닌 클라이언트는 NetworkTransform이 위치를 보간 동기화하므로 코루틴을 돌리지 않는다.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            {
                _moveCoroutine = null;
                yield break;
            }

            // path가 비정상(null / 길이 1 이하 / 유닛 사망 등)이면 즉시 cleanup 후 종료.
            if (path == null || path.Count < 2 || _unitData == null || !_unitData.IsAlive)
            {
                MoveCleanupAndCompleteV3();
                yield break;
            }

            _movementRotationWriterOwnsRoot = true;

            // 멀티플레이 서버: 이동 시작 이벤트 발행 → NetworkCombatController가
            // _animState(=Walk) 레벨 동기화로 모든 클라이언트에 Walk 시작 전파.
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

            // Walk 애니메이션 시작 — 이동 시작 시 1회만 CrossFade.
            // 매 타일마다 CrossFade하면 블렌딩이 매번 리셋되어 Walk 모션이 끊긴다.
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
                _currentAnimStateHash = StateWalk;
            }

            // 혼잡도 기여 활성화 — 본 코루틴이 도는 동안 새 타일 진입 시
            // GameEvents.OnUnitEnteredTile을 발행한다. 전투 추격 진입 시 false로 바뀌고,
            // resumePath로 A* 재개 시 다시 true로 돌아온다.
            _isAStarMoving = true;

            // 최종 목적지(외부 while 동안 변하지 않음). 우회/재경로 모두 이 좌표를 기준.
            HexCoord finalTarget = path[path.Count - 1];
            var pathCheckpoint = new UnitPathCheckpointTracker();
            var repathGuard = _navigationObjective != null
                && _navigationObjective.IsActive
                ? _navigationObjective.RepathGuard
                : new UnitRepathProgressGuard(path);
            bool movementFailedClosed = false;
            bool navigationBlocked = false;
            SetMovementHeldAnimation(false);

            // Legacy 시간 환산과 신규 trajectory가 같은 기본 MoveSpeed를 사용한다.
            // 연구·둔화·빙결 배율은 각 frame에서 다시 읽어 즉시 반영한다.
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;
            float worldSpeed = HexMetrics.TileHeight / moveSeconds;

            // ────────────────────────────────────────────────────────────────
            // 외부 while — path가 새로 발급될 때마다 위로 돌아와 새 path로 순회.
            //   재경로 사유:
            //     (A) 전투 종료 후 ResumeFromForwardTileV3가 새 path를 반환.
            //   정상 완주 또는 사망이면 외부 while 자체가 break.
            //
            //   타일에 다른 유닛이 있어도 그대로 진입하며, 같은 타일에서 시각적으로 겹치는 것은
            //   허용된다(분산은 별도 시스템 책임이 아님).
            // ────────────────────────────────────────────────────────────────
            while (_unitData != null && _unitData.IsAlive)
            {
                // 여러 frame의 무진전 상한 또는 invalid path에 도달하면 코루틴을 종료하지 않는다.
                // 종료하면 IsMoving이 false가 되어 공성 ticker가 같은 목표를 새 command로 재발행하고
                // guard가 초기화된다. 현재 pose와 코루틴을 유지한 채 walkability revision 또는
                // OnPathInvalidated가 제공한 새 PendingPath만 기다린다.
                if (navigationBlocked)
                {
                    SetMovementHeldAnimation(true);
                    ulong blockedEnvironmentRevision =
                        _navigationEnvironmentRevision;
                    while (_unitData != null
                        && _unitData.IsAlive
                        && _pendingPath == null
                        && _navigationEnvironmentRevision
                            == blockedEnvironmentRevision)
                    {
                        yield return null;
                    }

                    if (_unitData == null || !_unitData.IsAlive)
                        break;

                    List<HexCoord> recoveryPath = _pendingPath;
                    _pendingPath = null;
                    if (recoveryPath == null || recoveryPath.Count < 2)
                    {
                        recoveryPath = _movementUseCase != null
                            ? _movementUseCase.RequestMove(
                                _unitData, finalTarget)
                            : null;
                    }

                    if (!TryAcceptRepath(
                            repathGuard,
                            recoveryPath,
                            "blocked-environment-resume",
                            out UnitRepathDecision blockedDecision))
                    {
                        // 새 환경에서도 아직 경로가 없으면 다시 Blocked로 남는다. 로그는 환경 변화
                        // 한 번당 terminal 1회만 기록되고 매 frame 재시도하지 않는다.
                        yield return null;
                        continue;
                    }

                    BeginMovementShadowSegment(
                        UnitMovementIntentReason.BlockedRepath);
                    path = recoveryPath;
                    finalTarget = path[path.Count - 1];
                    pathCheckpoint.Reset();
                    navigationBlocked = false;
                    _navigationObjective?.MarkWaitingRepath();

                    // 수락한 재경로는 반드시 다음 frame부터 planner에 입력한다.
                    yield return null;
                }

                _navigationObjective?.MarkNavigating();
                SetMovementHeldAnimation(false);

                // 새 path를 받아 다시 시작할지 결정하는 플래그.
                // for 안에서 true로 바꾼 뒤 break하면 외부 while로 빠져 새 path로 continue.
                bool needRepath = false;

                // 마지막에 실제로 도착한 타일 — ProcessStep의 from 인자에 사용.
                HexCoord prevActualTile = _unitData.Position;

                // ────────────────────────────────────────────────────────────
                // 타일 순회 for — path[0]은 시작점이므로 1부터.
                // ────────────────────────────────────────────────────────────
                for (int i = 1; i < path.Count; i++)
                {
                    // 사망/소실 체크 — 매 타일 진입 시 안전 장치.
                    if (_unitData == null || !_unitData.IsAlive) break;

                    HexCoord from = prevActualTile;
                    HexCoord to = path[i];

                    // 마지막 스텝이 non-walkable(성/건물)인지 — ProcessStep을 건너뛰는 기준.
                    bool isLastStep = (i == path.Count - 1);
                    bool isLastStepToNonWalkable = isLastStep
                        && (_movementUseCase != null && !_movementUseCase.IsWalkable(to));

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] 도메인 방향(_unitData.Facing) 갱신 — HexDirection 단위의 도메인 상태 추적용.
                    //   시각적 회전은 ApplyDirection으로 즉시 스냅하지 않고,
                    //   아래 Lerp 루프에서 매 프레임 RotateTowards로 부드럽게 회전한다.
                    //   (즉시 스냅하면 타일 전환 순간 Y축 회전이 점프하여 시각적으로 부자연스럽다.)
                    // ──────────────────────────────────────────────────────
                    HexDirection dir = FacingDirection.FromCoords(from, to);
                    dir = ViewConverter.FlipDirection(dir);
                    if (_lockedMovementPipelineMode
                        != UnitMovementPipelineMode.ReducerAuthoritative)
                    {
                        _unitData.Facing = dir;
                    }

                    // ──────────────────────────────────────────────────────
                    // [A* 경로 입력] waypoint는 타일 중심이지만 ReducerAuthoritative 위치는
                    // 중심 통과를 강제하지 않는다. 이 점과 다음 점으로 corridor 안의 연속
                    // trajectory를 만들며, 마지막 목적지만 중심에 정확히 수렴한다.
                    // ──────────────────────────────────────────────────────
                    Vector3 toDomain = HexMetrics.HexToWorld(to);

                    // Lerp 출발점은 현재 위치(직전 도착 위치를 그대로 이어받음).
                    Vector3 fromPos = transform.position;

                    // 타일 중심 = 도메인 좌표 → 뷰 좌표 변환 + 유닛 높이 보정.
                    Vector3 toPos = ViewConverter.ToView(toDomain);
                    toPos.y += HexMetrics.UnitYOffset;
                    CaptureUnitActionShadowDesiredTarget(toPos);

                    // 정상 코너는 현재 타일 중심에 도착한 뒤 다음 방향을 처음 보는 것이 아니라,
                    // 현재 waypoint에 접근하는 동안 다음 선분을 미리 보아야 연속 선회가 된다.
                    // 마지막 waypoint에는 다음 점이 없으므로 정확한 목적지 중심에 수렴한다.
                    Vector3? nextTrajectoryTarget = null;
                    if (i + 1 < path.Count)
                    {
                        Vector3 nextDomain = HexMetrics.HexToWorld(path[i + 1]);
                        Vector3 nextView = ViewConverter.ToView(nextDomain);
                        nextView.y += HexMetrics.UnitYOffset;
                        nextTrajectoryTarget = nextView;
                    }

                    // ──────────────────────────────────────────────────────
                    // [회전 시스템 변경 — 2026-05-14] 목표 각도 계산 — toPos 정의 직후 1회만.
                    //   CalculateAttackAngle은 (toPos - 현재 위치) 벡터의 XZ 평면 Atan2 결과를 도 단위로 반환한다.
                    //   이 각도는 Lerp 루프 시작 시점의 출발 위치 기준이며, 루프 도중에는 갱신하지 않는다.
                    //   (1 타일 거리이므로 출발 시점 방향과 도착 시점 방향이 거의 동일.)
                    // ──────────────────────────────────────────────────────
                    float targetAngle = CalculateAttackAngle(toPos);
                    Quaternion targetRot = Quaternion.Euler(0f, targetAngle, 0f);

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] Legacy Lerp 또는 신규 거리 기반 trajectory + 매 프레임 감지.
                    //   - detect 진입 → EnterCombatPursuitV3로 [전투 이동] 위임.
                    //   - 근접/원거리 구분 없음. 차이는 EnterCombatPursuitV3 내부의 사거리 수치뿐.
                    // ──────────────────────────────────────────────────────
                    float elapsed = 0f;
                    float targetDuration = moveSeconds;
                    bool reducerTrajectory = _lockedMovementPipelineMode
                        == UnitMovementPipelineMode.ReducerAuthoritative;
                    bool authoritativeWaypointReached = false;
                    Func<Vector3, bool> candidateEngagePredicate = reducerTrajectory
                        ? (Func<Vector3, bool>)ShouldEngageAt
                        : null;

                    // [전투 이동]으로 전환되었음을 표시. true면 Lerp while 탈출 후
                    // toPos 강제 스냅과 ProcessStep을 모두 건너뛰고 새 path로 외부 while 재진입.
                    bool interruptedByCombat = false;

                    // 지금 Lerp 중인 "다음 도착 타일" 좌표를 기록.
                    // OnPathInvalidated가 호출됐을 때 이 타일에 건물이 새로 생긴 경우만
                    // 즉시 코루틴을 재시작해야 하므로 외부에서 검사할 수 있도록 노출한다.
                    _currentNextTileCoord = to;

                    while ((reducerTrajectory
                            ? !authoritativeWaypointReached
                            : elapsed < targetDuration)
                        && _unitData != null
                        && _unitData.IsAlive)
                    {
                        float liveMoveMultiplier = _combatUseCase != null
                            ? Mathf.Max(0f, _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData))
                            : 1f;
                        bool movementFrozen = liveMoveMultiplier <= MoveFreezeEpsilon;
                        SetFrozenAnimation(movementFrozen);

                        float candidateElapsed = elapsed
                            + (movementFrozen ? 0f : Time.deltaTime * liveMoveMultiplier);
                        float t = Mathf.Clamp01(candidateElapsed / targetDuration);
                        Vector3 candidatePosition = Vector3.Lerp(fromPos, toPos, t);
                        bool shouldEngage = ShouldEngage();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        UnitMovementShadowObserver.LegacyFrameToken movementShadowToken = default;
                        if (_lockedMovementPipelineMode
                            != UnitMovementPipelineMode.ReducerAuthoritative)
                        {
                            movementShadowToken = BeginMovementShadowLegacyFrame(
                                    toPos,
                                    UnitMovementIntentReason.AStarPath,
                                    CalculateMovementShadowPositionDeltaXZ(
                                        transform.position,
                                        candidatePosition));
                        }
#endif
                        Vector3 positionBeforeAuthorityFrame = transform.position;
                        MovementWriteOutcome movementOutcome = ApplyMovementAuthorityFrame(
                            toPos,
                            candidatePosition,
                            targetRot,
                            UnitMovementIntentReason.AStarPath,
                            shouldEngage,
                            nextTrajectoryTarget,
                            reducerTrajectory
                                ? worldSpeed * liveMoveMultiplier * Time.deltaTime
                                : -1f,
                            reducerTrajectory ? path : null,
                            reducerTrajectory ? i : -1,
                            candidateEngagePredicate);
                        if (movementOutcome == MovementWriteOutcome.RepathRequired)
                        {
                            List<HexCoord> blockedRepath = _movementUseCase != null
                                ? _movementUseCase.RequestMove(_unitData, finalTarget)
                                : null;
                            if (!TryAcceptRepath(
                                    repathGuard,
                                    blockedRepath,
                                    "astar-corridor",
                                    out UnitRepathDecision repathDecision))
                            {
                                navigationBlocked = true;
                                needRepath = true;
                                _currentNextTileCoord = null;
                                break;
                            }

                            BeginMovementShadowSegment(
                                UnitMovementIntentReason.BlockedRepath);
                            path = blockedRepath;
                            pathCheckpoint.Reset();
                            needRepath = true;
                            SetMovementHeldAnimation(true);
                            _currentNextTileCoord = null;
                            break;
                        }
                        if (movementOutcome == MovementWriteOutcome.Rejected)
                        {
                            Debug.LogError(
                                $"[UAS-MOVE] 권위 이동 frame 거부. unitId={_unitData.Id}, " +
                                $"commandRevision={_movementCommandRevision}, " +
                                $"segmentRevision={_movementSegmentRevision}");
                            movementFailedClosed = true;
                            goto cleanup;
                        }
                        if (movementOutcome == MovementWriteOutcome.Advanced)
                        {
                            Vector3 committedDelta =
                                transform.position - positionBeforeAuthorityFrame;
                            if (_lastMovementTrajectoryReachedWaypoint
                                || committedDelta.x * committedDelta.x
                                    + committedDelta.z * committedDelta.z
                                    > (float)(UnitMovementEvaluationAdapter.PositionTolerance
                                        * UnitMovementEvaluationAdapter.PositionTolerance))
                            {
                                ObserveNavigationProgress(repathGuard, path);
                            }

                            if (reducerTrajectory)
                            {
                                authoritativeWaypointReached =
                                    _lastMovementTrajectoryReachedWaypoint;
                            }
                            else
                            {
                                elapsed = candidateElapsed;
                            }
                        }
                        shouldEngage = shouldEngage
                            || _lastMovementCandidateAcquiredTarget;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        CompleteMovementShadowLegacyFrame(movementShadowToken);
#endif

                        // ── [A* 이동] → [전투 이동 / 힐] 전환 ──
                        // 감지 사거리 안에 대상(일반=적 / 힐러=부상 아군)이 있으면 즉시 위임.
                        // ShouldEngage()가 유닛 역할에 맞는 대상을 검사한다(힐러는 적을 타지 않음).
                        if (shouldEngage)
                        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            UnitMovementShadowObserver.ObserveTargetAcquirePriority(
                                movementShadowToken);
#endif
                            // 혼잡도 기여 일시 중단 — 추격 단계는 타일을 거치지 않는
                            // 직선 이동이므로 OnUnitEnteredTile 발행을 막는다. resumePath에서 다시 true.
                            _isAStarMoving = false;

                            // 전투 추격 진입 — 더 이상 "다음 도착 타일"을 향해 가지 않으므로
                            // OnPathInvalidated가 잘못된 타일을 검사하지 않도록 즉시 null로 비운다.
                            // (IsInCombat()가 true가 되어 OnPathInvalidated가 사실상 early return하지만,
                            //  방어적으로 정합 상태 유지.)
                            _currentNextTileCoord = null;

                            if (_unitData.IsHealer)
                            {
                                // [힐러] 부상 아군 힐 루프에 위임 — 적 공격 흐름과 완전히 분리된 경로.
                                //   정지 → 힐 애니 → HoT 부여 → 쿨다운 → 재탐색을 EnterHealLoopV3가 담당한다.
                                _movementRotationWriterOwnsRoot = false;
                                yield return EnterHealLoopV3();
                                _movementRotationWriterOwnsRoot = true;
                            }
                            else
                            {
                                // [전투 이동] → [공격] 흐름을 EnterCombatPursuitV3에 전부 위임.
                                yield return EnterCombatPursuitV3();

                                // 전투 종료 후 회전 정합성 유지.
                                // _combatTargetTransform이 null이 아닌 동안 Update()가 매 프레임 회전을 덮어쓰므로
                                // StopCombatAnimation()을 직접 호출하여 즉시 추적을 끊습니다.
                                StopCombatAnimation();
                            }

                            if (_unitData == null || !_unitData.IsAlive) break;

                            BeginMovementShadowSegment(UnitMovementIntentReason.PostCombatResume);
                            // ──────────────────────────────────────────────────────────
                            // [BUG-002 수정 — 2026-05-13] 전투 종료 → 앞쪽 타일까지 "걸어서" 정렬.
                            //
                            //   왜 필요한가:
                            //     전투 추격 중 transform.position은 공격 슬롯(=적 근처)으로 이동했고,
                            //     _unitData.Position(도메인)은 여전히 추격 전 타일을 가리킨다.
                            //     이 상태에서 즉시 새 path Lerp를 시작하면, 출발점이 공격 슬롯이 되어
                            //     다음 타일까지 비정상적으로 멀리 점프하는 시각적 순간이동이 발생한다.
                            //
                            //   해결:
                            //     1) FindForwardClosestTile로 "앞쪽 타일" forwardTile을 1회만 결정.
                            //     2) 그 타일 중심 뷰 좌표(alignView)까지 동일 이동 속도로 Lerp.
                            //        (규칙 5: A* 이동과 동일 속도 — moveSeconds는 1칸 이동 시간이므로
                            //         실제 거리 / TileHeight 비율을 곱해 시간 환산.)
                            //     3) Lerp 도중에도 detect 사거리 적 감지 시 즉시 중단 → 새 추격 진입.
                            //     4) Lerp 완료 후 transform.position을 alignView로 최종 정렬 스냅.
                            //     5) 같은 forwardTile을 ResumeFromForwardTileV3에 전달해 도메인 갱신 +
                            //        새 path 발급. (이중 FindForwardClosestTile 호출 방지)
                            // ──────────────────────────────────────────────────────────

                            // 1) forwardTile 계산 — 호출은 단 1회.
                            Vector3 unitDomainPos = ViewConverter.FromView(transform.position);
                            HexCoord forwardTile = _movementUseCase.FindForwardClosestTile(unitDomainPos, finalTarget);

                            // 2) forwardTile 중심 뷰 좌표.
                            Vector3 forwardWorld = HexMetrics.HexToWorld(forwardTile);
                            Vector3 alignView = ViewConverter.ToView(forwardWorld);
                            alignView.y += HexMetrics.UnitYOffset;
                            CaptureUnitActionShadowDesiredTarget(alignView);

                            Vector3 alignFromPos = transform.position;
                            float alignDist = Vector3.Distance(alignFromPos, alignView);

                            // 동일 이동 속도 환산: moveSeconds는 1 타일 거리 이동에 필요한 시간이므로,
                            // 실제 정렬 거리에 비례해 시간을 줄이거나 늘린다.
                            // 거리 0(또는 매우 작음)이면 Lerp 자체를 생략하고 즉시 스냅.
                            float alignDuration = (HexMetrics.TileHeight > 0f)
                                ? (alignDist / HexMetrics.TileHeight) * moveSeconds
                                : 0f;

                            // 정렬 Lerp 중에 새 적이 감지되면 다시 추격에 진입해야 하므로 별도 플래그.
                            bool alignInterruptedByCombat = false;

                            // 정렬 방향 계산 — unitDomainPos가 속한 타일에서 forwardTile로 가는 HexDirection.
                            HexCoord nearestTile = HexMetrics.WorldToHex(unitDomainPos);
                            HexDirection alignDir = FacingDirection.FromCoords(nearestTile, forwardTile);
                            // 전투 위치에서 A*로 복귀하는 구간도 Collider가 아니라 현재/앞쪽
                            // 타일을 연결한 logical corridor로 Simulation Root point를 검사한다.
                            var resumeCorridor = new List<HexCoord>(2)
                            {
                                nearestTile,
                                forwardTile
                            };

                            // ──────────────────────────────────────────────────────
                            // [회전 시스템 변경 — 2026-05-14] 정렬 회전을 RotateTowards로 점진 회전.
                            //   기존: ApplyDirection으로 Lerp 시작 직전에 Y축 회전을 즉시 스냅.
                            //     문제 - 전투 종료 후 적 방향(스냅된 회전)에서 정렬 방향으로 점프하여 시각적으로 부자연스러움.
                            //   변경: 목표 각도(alignTargetRot)를 정해두고 Lerp 루프에서 매 프레임 누적 회전.
                            //     CalculateAttackAngle(alignView): 현재 위치에서 정렬 목적지 뷰 좌표까지의 XZ 방향 각도.
                            //   _unitData.Facing은 도메인 상태 추적 목적으로 기존처럼 HexDirection으로 갱신 유지.
                            // ──────────────────────────────────────────────────────
                            HexDirection alignViewDir = ViewConverter.FlipDirection(alignDir);
                            if (_lockedMovementPipelineMode
                                != UnitMovementPipelineMode.ReducerAuthoritative)
                            {
                                _unitData.Facing = alignViewDir;
                            }

                            float alignTargetAngle = CalculateAttackAngle(alignView);
                            Quaternion alignTargetRot = Quaternion.Euler(0f, alignTargetAngle, 0f);

                            if (alignDuration > 0.0001f)
                            {
                                float alignElapsed = 0f;
                                bool reducerResumeTrajectory = _lockedMovementPipelineMode
                                    == UnitMovementPipelineMode.ReducerAuthoritative;
                                bool resumeWaypointReached = false;
                                while ((reducerResumeTrajectory
                                        ? !resumeWaypointReached
                                        : alignElapsed < alignDuration)
                                    && _unitData != null
                                    && _unitData.IsAlive)
                                {
                                    float alignMoveMultiplier = _combatUseCase != null
                                        ? Mathf.Max(0f, _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData))
                                        : 1f;
                                    bool alignMovementFrozen =
                                        alignMoveMultiplier <= MoveFreezeEpsilon;
                                    SetFrozenAnimation(alignMovementFrozen);

                                    float candidateAlignElapsed = alignElapsed
                                        + (alignMovementFrozen
                                            ? 0f
                                            : Time.deltaTime * alignMoveMultiplier);
                                    float at = Mathf.Clamp01(candidateAlignElapsed / alignDuration);
                                    Vector3 candidateAlignPosition = Vector3.Lerp(
                                        alignFromPos,
                                        alignView,
                                        at);
                                    bool shouldReengage = ShouldEngage();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                    UnitMovementShadowObserver.LegacyFrameToken resumeShadowToken = default;
                                    if (_lockedMovementPipelineMode
                                        != UnitMovementPipelineMode.ReducerAuthoritative)
                                    {
                                        resumeShadowToken = BeginMovementShadowLegacyFrame(
                                            alignView,
                                            UnitMovementIntentReason.PostCombatResume,
                                            CalculateMovementShadowPositionDeltaXZ(
                                                transform.position,
                                                candidateAlignPosition));
                                    }
#endif
                                    Vector3 positionBeforeAlignFrame = transform.position;
                                    MovementWriteOutcome alignOutcome = ApplyMovementAuthorityFrame(
                                        alignView,
                                        candidateAlignPosition,
                                        alignTargetRot,
                                        UnitMovementIntentReason.PostCombatResume,
                                        shouldReengage,
                                        nextTrajectoryTarget: null,
                                        maximumTravelDistanceWorld:
                                            reducerResumeTrajectory
                                                ? worldSpeed * alignMoveMultiplier * Time.deltaTime
                                                : -1f,
                                        logicalCorridorPath:
                                            reducerResumeTrajectory
                                                ? resumeCorridor
                                                : null,
                                        logicalCorridorWaypointIndex:
                                            reducerResumeTrajectory ? 1 : -1,
                                        candidateAcquirePredicate:
                                            reducerResumeTrajectory
                                                ? (Func<Vector3, bool>)ShouldEngageAt
                                                : null);
                                    if (alignOutcome == MovementWriteOutcome.RepathRequired)
                                    {
                                        List<HexCoord> blockedResumePath =
                                            _movementUseCase.RequestMove(
                                                _unitData, finalTarget);
                                        if (!TryAcceptRepath(
                                                repathGuard,
                                                blockedResumePath,
                                                "post-combat-corridor",
                                                out UnitRepathDecision repathDecision))
                                        {
                                            navigationBlocked = true;
                                            needRepath = true;
                                            _currentNextTileCoord = null;
                                            break;
                                        }

                                        BeginMovementShadowSegment(
                                            UnitMovementIntentReason.BlockedRepath);
                                        path = blockedResumePath;
                                        pathCheckpoint.Reset();
                                        needRepath = true;
                                        SetMovementHeldAnimation(true);
                                        break;
                                    }
                                    if (alignOutcome == MovementWriteOutcome.Rejected)
                                    {
                                        Debug.LogError(
                                            $"[UAS-MOVE] 권위 복귀 정렬 frame 거부. " +
                                            $"unitId={_unitData.Id}, " +
                                            $"commandRevision={_movementCommandRevision}, " +
                                            $"segmentRevision={_movementSegmentRevision}");
                                        movementFailedClosed = true;
                                        goto cleanup;
                                    }
                                    if (alignOutcome == MovementWriteOutcome.Advanced)
                                    {
                                        Vector3 committedAlignDelta =
                                            transform.position - positionBeforeAlignFrame;
                                        if (_lastMovementTrajectoryReachedWaypoint
                                            || committedAlignDelta.x * committedAlignDelta.x
                                                + committedAlignDelta.z * committedAlignDelta.z
                                                > (float)(UnitMovementEvaluationAdapter.PositionTolerance
                                                    * UnitMovementEvaluationAdapter.PositionTolerance))
                                        {
                                            ObserveNavigationProgress(repathGuard, path);
                                        }

                                        if (reducerResumeTrajectory)
                                        {
                                            resumeWaypointReached =
                                                _lastMovementTrajectoryReachedWaypoint;
                                        }
                                        else
                                        {
                                            alignElapsed = candidateAlignElapsed;
                                        }
                                    }
                                    shouldReengage = shouldReengage
                                        || _lastMovementCandidateAcquiredTarget;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                    CompleteMovementShadowLegacyFrame(resumeShadowToken);
#endif

                                    // 정렬 도중에도 새 대상(일반=적 / 힐러=부상 아군) 감지 시 즉시 다음 사이클로 넘긴다.
                                    if (shouldReengage)
                                    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                        UnitMovementShadowObserver.ObserveTargetAcquirePriority(
                                            resumeShadowToken);
#endif
                                        alignInterruptedByCombat = true;
                                        break;
                                    }

                                    yield return null;
                                }
                            }

                            if (needRepath)
                                break;

                            if (_unitData == null || !_unitData.IsAlive) break;

                            // 정렬 Lerp 도중 새 적이 감지된 경우: 정렬을 마무리하지 않고 곧장 외부 while로
                            // 빠져 새 path를 받지 않은 채 다시 진입 → 다음 사이클에서 추격이 재개된다.
                            // 같은 path를 그대로 재사용하면 다시 path[i]를 향해 Lerp가 시작되고, 그 즉시
                            // detect 체크가 추격으로 전환한다.
                            if (alignInterruptedByCombat)
                            {
                                interruptedByCombat = true;
                                break;  // Lerp while 탈출 → for 루프도 interruptedByCombat 처리로 break
                            }

                            // 4) 최종 부동소수점 보정도 같은 writer seam을 통과한다.
                            MovementWriteOutcome alignEndpointOutcome =
                                ApplyMovementAuthorityFrame(
                                alignView,
                                alignView,
                                transform.rotation,
                                UnitMovementIntentReason.PostCombatResume,
                                targetAcquirePriority: false);
                            if (alignEndpointOutcome == MovementWriteOutcome.Rejected)
                            {
                                Debug.LogError(
                                    $"[UAS-MOVE] 권위 복귀 endpoint 거부. unitId={_unitData.Id}");
                                movementFailedClosed = true;
                                goto cleanup;
                            }

                            // 5) 같은 forwardTile로 도메인 갱신 + 새 path 발급.
                            List<HexCoord> resumePath = ResumeFromForwardTileV3(finalTarget, forwardTile);
                            if (resumePath != null && resumePath.Count >= 2)
                            {
                                if (!TryAcceptRepath(
                                        repathGuard,
                                        resumePath,
                                        "post-combat-resume",
                                        out UnitRepathDecision repathDecision))
                                {
                                    navigationBlocked = true;
                                    needRepath = true;
                                    _currentNextTileCoord = null;
                                    break;
                                }

                                path = resumePath;
                                pathCheckpoint.Reset();
                                needRepath = true;
                                interruptedByCombat = true;
                                SetMovementHeldAnimation(true);
                                // 혼잡도 기여 재활성화 — 전투 종료 후 정상 A* 재개.
                                _isAStarMoving = true;
                                break;  // Lerp while 탈출
                            }

                            // 새 path를 만들지 못함 → 이동 종료.
                            goto cleanup;
                        }

                        yield return null;
                    }

                    if (_unitData == null || !_unitData.IsAlive) break;

                    // corridor가 경기 중 막혀 새 path를 받은 경우 현재 waypoint의
                    // ProcessStep/EnteredTile은 아직 발생하면 안 된다. 외부 while에서
                    // 새 path index 1부터 다시 시작한다.
                    if (needRepath) break;

                    // [전투 이동]으로 중단된 경우: toPos 강제 스냅과 ProcessStep을 건너뛰고
                    // for 루프를 빠져나가 외부 while에서 새 path로 재진입.
                    if (interruptedByCombat)
                    {
                        break;
                    }

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] 정상 Lerp 완료 — 타일 중심에 스냅 + 도메인 위치 갱신.
                    // ──────────────────────────────────────────────────────
                    // Legacy Lerp만 기존 타일 중심 endpoint 보정을 유지한다. 연속 trajectory는
                    // 중간 waypoint를 통과 평면으로 소비하므로 중심 스냅을 하면 안 된다.
                    if (!reducerTrajectory)
                    {
                        MovementWriteOutcome endpointOutcome = ApplyMovementAuthorityFrame(
                            toPos,
                            toPos,
                            transform.rotation,
                            UnitMovementIntentReason.AStarPath,
                            targetAcquirePriority: false);
                        if (endpointOutcome == MovementWriteOutcome.Rejected)
                        {
                            Debug.LogError(
                                $"[UAS-MOVE] 권위 이동 endpoint 거부. unitId={_unitData.Id}");
                            movementFailedClosed = true;
                            goto cleanup;
                        }
                    }

                    // 도메인 처리: _unitData.Position을 to로 갱신.
                    // 마지막 스텝이 non-walkable(성/건물)이면 ProcessStep 생략 — 유닛이 건물 위에 존재하는 것으로 처리되지 않도록.
                    if (reducerTrajectory
                        && !pathCheckpoint.TryConsume(
                            i,
                            authoritativeWaypointReached))
                    {
                        Debug.LogError(
                            $"[UAS-MOVE] trajectory waypoint 순서 거부. " +
                            $"unitId={_unitData.Id}, index={i}, " +
                            $"expected={pathCheckpoint.NextWaypointIndex}");
                        movementFailedClosed = true;
                        goto cleanup;
                    }
                    ObserveNavigationProgress(repathGuard, path);
                    if (_movementUseCase != null && !(isLastStep && isLastStepToNonWalkable))
                    {
                        _movementUseCase.ProcessStep(_unitData, from, to);
                    }

                    // 혼잡도 시스템 — A* 이동 중 새 타일에 진입했을 때만 발행.
                    // 전투 추격 단계에서는 _isAStarMoving == false라 발행되지 않는다.
                    // GameBootstrapper가 이 이벤트를 받아 CongestionMap.Increment(tile)을 수행한다.
                    if (_isAStarMoving)
                    {
                        GameEvents.OnUnitEnteredTile.OnNext(new UnitEnteredTileEvent(_unitData.Id, to));
                    }

                    prevActualTile = to;

                    // 타일 도착 완료 — 다음 Lerp 시작 전까지는 "다음 도착 타일" 정보 없음.
                    // null로 비워 두지 않으면 OnPathInvalidated가 이미 지나간 타일을 검사할 수 있다.
                    _currentNextTileCoord = null;

                    // ──────────────────────────────────────────────────────
                    // 부드러운 경로 교체 — OnPathInvalidated가 예약한 새 path 소비.
                    //
                    //   OnPathInvalidated가 _pendingPath에 새 경로를 담아 두면,
                    //   여기서(타일 중심 도착 직후) 그것을 꺼내 path 변수를 교체한다.
                    //   현재 위치(_unitData.Position == to)가 새 path 어디에 해당하는지 찾아
                    //   거기서부터 이어가도록 외부 while로 재진입한다.
                    //
                    //   현재 위치를 새 path에서 찾지 못하는 경우(드물지만 가능):
                    //     - 새 경로 자체가 다른 시작점부터 만들어진 경우 등.
                    //     - 이때는 안전하게 MoveTo(_pendingPath)로 코루틴을 재시작한다.
                    //       (이 경로에서는 시각적 끊김이 있지만, 데이터 무결성이 우선.)
                    // ──────────────────────────────────────────────────────
                    if (_pendingPath != null)
                    {
                        List<HexCoord> pending = _pendingPath;
                        _pendingPath = null;

                        // 현재 도메인 위치가 새 path의 어느 인덱스에 있는지 검색.
                        // 찾으면 그 지점부터 이어 돌도록 path를 잘라낸다.
                        int startIdx = -1;
                        for (int k = 0; k < pending.Count; k++)
                        {
                            if (pending[k].Q == _unitData.Position.Q
                                && pending[k].R == _unitData.Position.R)
                            {
                                startIdx = k;
                                break;
                            }
                        }

                        if (startIdx >= 0 && startIdx < pending.Count - 1)
                        {
                            BeginMovementShadowSegment(UnitMovementIntentReason.PendingRepath);
                            // 정상 케이스: 현재 위치를 새 path의 startIdx로 두고,
                            // 그 다음 타일(startIdx+1)부터 진행하도록 path 교체.
                            // path[0]은 항상 시작점(현재 위치) — 슬라이스 후에도 이 규약 유지.
                            List<HexCoord> sliced = new List<HexCoord>(pending.Count - startIdx);
                            for (int k = startIdx; k < pending.Count; k++)
                            {
                                sliced.Add(pending[k]);
                            }
                            if (!TryAcceptRepath(
                                    repathGuard,
                                    sliced,
                                    "pending-path",
                                    out UnitRepathDecision repathDecision))
                            {
                                navigationBlocked = true;
                                needRepath = true;
                                _currentNextTileCoord = null;
                                break;
                            }

                            path = sliced;
                            pathCheckpoint.Reset();
                            SetMovementHeldAnimation(true);

                            // 최종 목적지가 바뀌었을 수도 있으므로 갱신.
                            finalTarget = path[path.Count - 1];

                            // 외부 while 재진입 → 새 path 기준으로 i=1부터 다시 순회.
                            needRepath = true;
                            break;  // for 탈출 → 아래 `if (needRepath) continue;`로 외부 while 재진입.
                        }
                        else
                        {
                            // 현재 위치를 포함하지 않는 pending path는 이어 달릴 수 없는 입력이다.
                            // 같은 프레임에 새 코루틴을 재시작하면 무제한 재시작 고리가 생길 수
                            // 있으므로 이 유닛만 현재 위치에서 fail-closed 한다.
                            List<HexCoord> refreshed = _movementUseCase != null
                                ? _movementUseCase.RequestMove(_unitData, finalTarget)
                                : null;
                            if (TryAcceptRepath(
                                    repathGuard,
                                    refreshed,
                                    "pending-path-current-position-refresh",
                                    out UnitRepathDecision refreshDecision))
                            {
                                BeginMovementShadowSegment(
                                    UnitMovementIntentReason.PendingRepath);
                                path = refreshed;
                                finalTarget = path[path.Count - 1];
                                pathCheckpoint.Reset();
                                needRepath = true;
                                SetMovementHeldAnimation(true);
                                break;
                            }

                            navigationBlocked = true;
                            needRepath = true;
                            _currentNextTileCoord = null;
                            break;
                        }
                    }

                    // [2026-05-11 비활성화] 우회(didDetour)/재경로 분기는 제거됐습니다.
                    // 새 규칙에서는 같은 타일에 여러 유닛이 들어가도 우회하지 않으므로 재계산 불필요.
                }  // end of for (path 순회)

                // for를 정상 종료(완주) 또는 사망/탈출 — 외부 while 분기.
                if (needRepath)
                {
                    // 수락한 재경로는 반드시 다음 Unity frame부터 처리한다. 이 경계가
                    // RequestMove → RepathRequired의 단일-frame 무한 반복을 차단한다.
                    yield return null;
                    continue;
                }
                break;                       // 정상 완주 또는 사망 — 외부 while 종료.
            }

            cleanup:
            // 정상/사망/완주/RESUME 실패 등 모든 종료 경로에서 공통 cleanup.
            //   여기서 _moveCoroutine을 null로 비우고 OnMoveComplete 콜백(랠리→성 자동 이동/공성 체인)을
            //   발행한다. 콜백이 새 이동(MoveTo)을 시작하면 _moveCoroutine이 다시 non-null이 된다.
            if (_unitData != null && _unitData.IsAlive)
            {
                TryCloseMovementAuthorityIntent(
                    UnitMovementIntentReason.AStarPath,
                    "path-cleanup");
            }
            if (_unitData == null || !_unitData.IsAlive)
                _navigationObjective?.Cancel();
            else if (movementFailedClosed)
                _navigationObjective?.Cancel();
            else
                _navigationObjective?.Complete();
            MoveCleanupAndCompleteV3(invokeCompletion: !movementFailedClosed);

            // ────────────────────────────────────────────────────────────────
            // [이슈 2 수정 — 2026-07-18] 힐러(BloomFairy) 유휴 감시 진입.
            //
            //   왜 필요한가(초급자용 설명):
            //     힐러는 적 성을 공격하지 않으므로, 부상 아군 없이 경로 끝(성 인접 타일)에 도달하면
            //     위 cleanup에서 이동 코루틴이 "완전히 끝나" 버렸다. 그 뒤에는 근처 아군이 다쳐도
            //     힐러가 다시 반응하지 못하는 "영구 유휴" 상태가 됐다(일반 유닛은 성 앞에서 무한 전투
            //     루프를 돌기 때문에 이 함정이 없다 — 힐러만 구조적으로 노출됐다).
            //
            //   해결:
            //     힐러가 "살아있는 동안 부상 아군을 계속 지켜보도록" 별도의 유휴 감시 코루틴을 시작해
            //     _moveCoroutine으로 계속 추적한다. 감시 중 부상 아군이 생기면 힐 루프로 재진입하고,
            //     이동을 재개할 새 경로(_pendingPath)가 예약되면 이동으로 돌아간다(아래 코루틴 참조).
            //
            //   비힐러 회귀 방지:
            //     - 아래 조건에 IsHealer가 있어 일반 유닛은 이 분기를 절대 타지 않는다(기존 종료 동작 그대로).
            //     - OnMoveComplete 콜백이 이미 새 이동을 시작한 경우(_moveCoroutine != null)에는 진입하지
            //       않는다. 즉 랠리 도착·공성 진행처럼 "다음 이동이 이어지는 중간 구간"에서는 감시가 끼어들지
            //       않아 기존 랠리/공성 체인이 그대로 보존된다. 순수 종착(더 이동할 곳 없음)일 때만 감시한다.
            // ────────────────────────────────────────────────────────────────
            if (!movementFailedClosed
                && _moveCoroutine == null
                && _unitData != null
                && _unitData.IsAlive
                && _unitData.IsHealer)
            {
                _moveCoroutine = StartCoroutine(HealerIdleWatchV3());
            }
        }

        /// <summary>
        /// [2026-05-11 신규] 통합 전투 추격 코루틴 (근접/원거리 공통).
        ///
        /// 새 규칙(GameSystemRules.md 규칙 7)에 따른 [전투 이동] → [공격] 단일 상태 머신.
        /// 근접/원거리 구분 없음 — 차이는 UnitData.AttackRange 수치뿐.
        ///
        /// 동작:
        ///   1) FindNearestEnemyInDetectRange로 첫 타겟 선택.
        ///   2) 매 프레임:
        ///      - 타겟이 detect 사거리 밖이면 종료(→ A* 재개).
        ///      - 타겟이 사망/파괴되면 detect 내 다른 적 재선택. 없으면 종료.
        ///      - 타겟이 공격 사거리(HasEnemyInRange) 안이면 EnterCombatLoopV3 호출.
        ///         · 전투 종료 후 detect 내 다른 적이 있으면 타겟 교체 후 continue.
        ///         · 없으면 종료.
        ///      - 그 외(detect 안, 사거리 밖) → 적 방향으로 직선 이동.
        ///
        /// 슬롯/점유 없음 — 같은 적을 향해 여러 유닛이 같은 위치로 모여도 그대로 진행합니다.
        /// </summary>
        private IEnumerator EnterCombatPursuitV3()
        {
            // ────────────────────────────────────────────────────────────
            // [BUG-001 — 2026-05-12] 추격 단계 진입 마킹.
            //   IsInCombat()가 true를 반환하도록 하여, 건물 변화로 인한
            //   RepathAllAliveUnits → OnPathInvalidated에서 추격 코루틴이 끊기지 않게 한다.
            //   Unity 코루틴(IEnumerator)은 try/finally가 동작하지 않으므로,
            //   각 yield break 직전 + 함수 끝에서 false로 명시적 리셋해야 한다.
            // ────────────────────────────────────────────────────────────
            _isInCombatPursuit = true;
            BeginMovementShadowSegment(UnitMovementIntentReason.Chase);

            // 필수 의존성 가드 — _positionProvider는 적 월드 좌표 조회에 사용.
            if (_combatUseCase == null || _positionProvider == null || _unitData == null)
            {
                _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                yield break;
            }

            // 첫 타겟 선택 — 감지 사거리 내 가장 가까운 적.
            var firstTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
            if (!firstTarget.HasValue)
            {
                TryCloseMovementAuthorityIntent(
                    UnitMovementIntentReason.Chase,
                    "pursuit-no-first-target");
                _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                yield break;
            }

            int targetId = firstTarget.Value.id;
            bool targetIsUnit = firstTarget.Value.isUnit;

            // 기본 이동 속도. 연구·둔화·빙결 배율은 아래 frame 루프에서 다시 읽는다.
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;
            float worldSpeed = HexMetrics.TileHeight / moveSeconds;

            // 매 프레임 루프 — 적 상태 확인 + 전투 이동 또는 공격으로 분기.
            while (_unitData != null && _unitData.IsAlive)
            {
                // ── 1) 적의 최신 월드 좌표 조회 ──
                // 적이 이동 중일 수 있으므로 매 프레임 갱신.
                Vector3 enemyViewPos = targetIsUnit
                    ? _positionProvider.GetUnitWorldPosition(targetId)
                    : _positionProvider.GetBuildingWorldPosition(targetId);

                // ── 2) 타겟 파괴 → 다음 타겟 재선택 or 종료 ──
                if (enemyViewPos == Vector3.zero)
                {
                    var next = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                    if (!next.HasValue)
                    {
                        // detect 내 다른 적 없음 → A* 재개로 위임.
                        TryCloseMovementAuthorityIntent(
                            UnitMovementIntentReason.Chase,
                            "pursuit-target-lost");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    targetId = next.Value.id;
                    targetIsUnit = next.Value.isUnit;

                    yield return null;
                    continue;
                }

                // ── 3) 적이 detect 사거리 밖으로 이탈 → 종료 ──
                if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                {
                    TryCloseMovementAuthorityIntent(
                        UnitMovementIntentReason.Chase,
                        "pursuit-detect-range-lost");
                    _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                    yield break;
                }

                // ── 4) 공격 사거리 도달 → [공격] 루프 ──
                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    MovementWriteOutcome acquireOutcome = ApplyMovementAuthorityFrame(
                        enemyViewPos,
                        transform.position,
                        transform.rotation,
                        UnitMovementIntentReason.Chase,
                        targetAcquirePriority: true);
                    if (acquireOutcome == MovementWriteOutcome.Rejected)
                    {
                        Debug.LogError(
                            $"[UAS-MOVE] 공격 범위 진입 NoIntent 게시 거부. unitId={_unitData.Id}");
                        _isInCombatPursuit = false;
                        yield break;
                    }

                    _movementRotationWriterOwnsRoot = false;
                    yield return EnterCombatLoopV3();
                    StopCombatAnimation();
                    _movementRotationWriterOwnsRoot = true;

                    if (_unitData == null || !_unitData.IsAlive)
                    {
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    // 전투 종료 후 detect 내에 다른 적이 남았으면 새 타겟 선택 후 continue.
                    if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                    {
                        TryCloseMovementAuthorityIntent(
                            UnitMovementIntentReason.Chase,
                            "post-combat-detect-range-lost");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    var nextEnemy = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                    if (!nextEnemy.HasValue)
                    {
                        TryCloseMovementAuthorityIntent(
                            UnitMovementIntentReason.Chase,
                            "post-combat-no-next-target");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    targetId = nextEnemy.Value.id;
                    targetIsUnit = nextEnemy.Value.isUnit;

                    yield return null;
                    continue;
                }

                // ── 5) [전투 이동] 적의 월드 위치로 직선 이동 + 매 프레임 적 방향 회전 ──
                // 슬롯 없음 — 같은 적을 노리는 유닛이 같은 위치로 모여도 진행.
                // 회전: 시각적으로 자연스럽게 적을 바라보며 추적.
                Vector3 moveDir = enemyViewPos - transform.position;
                moveDir.y = 0f;
                CaptureUnitActionShadowDesiredTarget(enemyViewPos);
                float dist = moveDir.magnitude;
                if (dist > 0.01f)
                {
                    float pursuitAngle = CalculateAttackAngle(enemyViewPos);
                    Quaternion targetRot = Quaternion.Euler(0f, pursuitAngle, 0f);
                    float liveMoveMultiplier = _combatUseCase != null
                        ? Mathf.Max(0f, _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData))
                        : 1f;
                    bool movementFrozen = liveMoveMultiplier <= MoveFreezeEpsilon;
                    SetFrozenAnimation(movementFrozen);
                    Vector3 candidatePursuitPosition = transform.position
                        + moveDir.normalized
                            * worldSpeed
                            * liveMoveMultiplier
                            * Time.deltaTime;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementShadowObserver.LegacyFrameToken pursuitShadowToken = default;
                    if (_lockedMovementPipelineMode
                        != UnitMovementPipelineMode.ReducerAuthoritative)
                    {
                        pursuitShadowToken = BeginMovementShadowLegacyFrame(
                            enemyViewPos,
                            UnitMovementIntentReason.Chase,
                            CalculateMovementShadowPositionDeltaXZ(
                                transform.position,
                                candidatePursuitPosition));
                    }
#endif
                    MovementWriteOutcome pursuitOutcome = ApplyMovementAuthorityFrame(
                        enemyViewPos,
                        candidatePursuitPosition,
                        targetRot,
                        UnitMovementIntentReason.Chase,
                        targetAcquirePriority: false);
                    if (pursuitOutcome == MovementWriteOutcome.Rejected)
                    {
                        Debug.LogError(
                            $"[UAS-MOVE] 권위 추격 frame 거부. unitId={_unitData.Id}, " +
                            $"commandRevision={_movementCommandRevision}, " +
                            $"segmentRevision={_movementSegmentRevision}");
                        _isInCombatPursuit = false;
                        yield break;
                    }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    CompleteMovementShadowLegacyFrame(pursuitShadowToken);
#endif
                }

                yield return null;
            }

            // ────────────────────────────────────────────────────────────
            // [BUG-001 — 2026-05-12] while 루프가 자연 종료(_unitData null/사망)된 경우의
            // 안전망 리셋. 위쪽의 모든 명시적 yield break 경로에서도 false로 set하지만,
            // 함수 마지막에서도 반드시 false임을 보장하여 다음 추격 진입 시 잔존하지 않도록 한다.
            // ────────────────────────────────────────────────────────────
            _isInCombatPursuit = false;
        }

        /// <summary>
        /// [V3] 공격 사거리 도달 후 공격/쿨다운 루프 (규칙 13 — 공격 중 이동 없음).
        ///
        /// 멀티플레이/싱글플레이 분기:
        ///   - 멀티플레이: OnUnitEnteredCombat 1회 발행 → NetworkCombatController가 RPC 전파.
        ///                 사거리 내인 동안 yield, 쿨다운 만료 후 사거리 재확인 시 재진입,
        ///                 사거리 이탈 시 OnUnitWalkStarted 발행 후 종료.
        ///   - 싱글플레이: 매 프레임 TryAttack(쿨다운 내부 체크), 쿨다운 대기, ClearCombatState로 종료.
        ///
        /// 재진입은 재귀 호출 대신 yield return으로 꼬리 재진입(스택 안전).
        /// </summary>
        private IEnumerator EnterCombatLoopV3()
        {
            if (_combatUseCase == null || _unitData == null) yield break;
            if (!_combatUseCase.HasEnemyInRange(_unitData)) yield break;
            // 공격 중에는 실제 이동 목표가 없다. 마지막 추격 목적지를 남기면 A2가 이를
            // 현재 공격 desired로 오해하므로 전투 정지 경계에서 optional 값을 명시적으로 비운다.
            ClearUnitActionShadowDesiredTarget();

            if (NetworkContext.IsNetworkActive)
            {
                // 멀티플레이 서버: 전투 진입 이벤트 발행 (RPC 전파는 NetworkCombatController 책임).
                GameEvents.OnUnitEnteredCombat.OnNext(_unitData.Id);

                // 사거리 내인 동안 대기 (이동 없음 — 규칙 13).
                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                    yield return null;

                if (!_unitData.IsAlive) yield break;

                // 현재 공격 사이클이 끝날 때까지 쿨다운 대기.
                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                    yield return null;

                if (!_unitData.IsAlive) yield break;

                // 사이클 완료 후 사거리에 다시 들어왔으면 같은 루프 재진입 (꼬리 재진입).
                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    yield return EnterCombatLoopV3();
                    yield break;
                }

                // 이동 재개 애니메이션 — Walk CrossFade.
                ResumeWalkAnimation();
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);
            }
            else
            {
                // 싱글플레이: 매 프레임 TryAttack(내부 쿨다운 체크).
                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                {
                    _combatUseCase.TryAttack(_unitData);
                    yield return null;
                }

                if (!_unitData.IsAlive) yield break;

                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                    yield return null;

                if (!_unitData.IsAlive) yield break;

                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    yield return EnterCombatLoopV3();
                    yield break;
                }

                _combatUseCase.ClearCombatState(_unitData.Id);

                ResumeWalkAnimation();
            }
        }

        /// <summary>
        /// [V3 신규] 힐러(BloomFairy) 전용 힐 루프 — 적 공격 흐름과 완전히 분리된 경로.
        ///
        /// 동작(설계 확정 6):
        ///   1) FindInjuredAllyToHeal로 힐 사거리 내 "가장 급한 부상 아군"을 선택.
        ///      없으면 Walk 애니메이션 재개 후 종료(→ 호출 측이 A* 이동을 재개).
        ///   2) 대상을 향해 회전 + 힐 애니메이션 재생(시전 중 이동 없음).
        ///   3) 힐 타격 타이밍(HitFrameTimes[0]) 대기 후 대상에 HoT(3초 20HP) 1회 부여(CastHeal).
        ///   4) 쿨다운(AttackCooldown, 3초) 대기.
        ///   5) 다시 재탐색(1로 반복). 대상이 풀피/사거리 이탈이면 자연히 다른 부상 아군으로 전환.
        ///
        /// 서버 권위: 이 코루틴은 서버(싱글=로컬 서버/멀티=서버)에서만 도는 MoveAlongPathV3에서만
        ///   호출되므로 CastHeal(HoT 부여)도 서버에서만 실행된다. 클라이언트는 HP/치유 텍스트를
        ///   NetworkHealthSync로 수신하고, 힐 애니메이션은 _animState(Attack) 레벨 동기화로 재생한다.
        ///
        /// HoT 부여 타이밍(설계 결정):
        ///   실제 회복 발동은 애니메이션 이벤트(OnAttackHit)가 아니라 이 코루틴의 HitFrameTimes 타이머로
        ///   구동한다. 이유 — 기존 데미지 시스템도 애니 이벤트가 아니라 HitFrameTimes 타이머로 데미지를
        ///   적용하며(코드베이스 관례), 애니 이벤트 의존은 클립에 이벤트가 주입되기 전까지 회복이 아예
        ///   발동하지 않는 취약점이 있기 때문이다. 타이밍(=HitFrameTimes[0])은 동일하다.
        ///   (OnAttackHit 애니 이벤트는 힐 이펙트/사운드 연출 용도로만 남는다.)
        /// </summary>
        private IEnumerator EnterHealLoopV3()
        {
            // 필수 의존성 가드 — _positionProvider는 대상 방향 회전에 사용.
            if (_combatUseCase == null || _positionProvider == null || _unitData == null)
                yield break;

            // 힐 시전 중임을 표시 — IsInCombat()가 true를 반환하도록 하여, 건물 변화로 인한
            //   RepathAllAliveUnits → OnPathInvalidated가 힐 도중 코루틴을 끊지 않게 한다(추격과 동일 취지).
            //   Unity 코루틴은 try/finally가 동작하지 않으므로 각 yield break 직전에 false로 명시 리셋한다.
            _isInCombatPursuit = true;

            while (_unitData != null && _unitData.IsAlive)
            {
                // 1) 부상 아군 재탐색.
                int? target = _combatUseCase.FindInjuredAllyToHeal(_unitData);
                if (!target.HasValue)
                {
                    // 더 힐할 아군 없음 → Walk 애니메이션 재개 후 종료(호출 측이 A* 이동 재개).
                    ResumeWalkAnimation();
                    if (NetworkContext.IsNetworkActive)
                        GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);
                    _isInCombatPursuit = false;
                    yield break;
                }

                int targetId = target.Value;

                // 2) 대상을 향해 회전 + 힐 애니메이션 재생(시전 중 이동 없음).
                PlayHealCastAnimation(targetId);

                // 3) 힐 타격 타이밍까지 대기(공격 유닛의 타격 프레임 재해석). 시전 중 이동 없음.
                float hitDelay = (_unitData.HitFrameTimes != null && _unitData.HitFrameTimes.Length > 0)
                    ? _unitData.HitFrameTimes[0]
                    : 0.2f;
                float t = 0f;
                while (t < hitDelay && _unitData != null && _unitData.IsAlive)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                if (_unitData == null || !_unitData.IsAlive) { _isInCombatPursuit = false; yield break; }

                // HoT 1회 부여(서버 권위). 대상이 시전 도중 사망/제거됐으면 CastHeal 내부에서 무시된다.
                _combatUseCase.CastHeal(_unitData, targetId);

                // 4) 쿨다운 시작 후 만료까지 대기.
                //    쿨다운 감소는 싱글=GameBootstrapper.TickCooldowns / 멀티=NetworkCombatController가 담당.
                _unitData.AttackCooldownRemaining = _unitData.AttackCooldown;
                while (_unitData != null && _unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                    yield return null;
                if (_unitData == null || !_unitData.IsAlive) { _isInCombatPursuit = false; yield break; }

                // 5) while 상단으로 돌아가 재탐색(부상 아군 있으면 반복, 없으면 위에서 종료).
            }

            // while 자연 종료(_unitData null/사망) 안전망 리셋.
            _isInCombatPursuit = false;
        }

        /// <summary>
        /// [이슈 2 수정 — 2026-07-18] 힐러(BloomFairy) 전용 유휴 감시 코루틴.
        ///
        /// 목적:
        ///   힐러가 이동 경로 끝(적 성 인접 등 더 이상 이동할 곳이 없는 지점)에 도달했을 때
        ///   코루틴을 영구히 끝내지 않고, "살아있는 동안 부상 아군을 계속 지켜보게" 한다.
        ///   이 코루틴은 MoveAlongPathV3의 cleanup에서 힐러일 때만 시작되며 _moveCoroutine으로 추적된다.
        ///
        /// 동작(짧은 주기 감시):
        ///   1) 힐 사거리 내 부상 아군이 있으면(ShouldEngage → HasInjuredAllyInHealRange) 즉시
        ///      EnterHealLoopV3(힐 루프)로 재진입한다. 힐 루프가 끝나면 다시 이 감시 상단으로 돌아온다.
        ///   2) 이동을 재개할 새 경로가 예약되면(_pendingPath — OnPathInvalidated가 건물 변화 등으로 예약)
        ///      그 경로로 MoveTo하여 기존 이동 로직으로 자연스럽게 복귀한다(감시 코루틴은 종료).
        ///   3) 둘 다 아니면 watchInterval(0.4초)마다 재확인하며 대기한다(매 프레임 탐색 비용 회피).
        ///
        /// 서버 권위 / 이중 틱 원칙:
        ///   이 감시 루프는 서버(싱글=로컬 서버/멀티=서버)에서만 도는 MoveAlongPathV3에서만 시작되며,
        ///   상태머신(코루틴) 레벨의 "지켜보기"일 뿐 회복을 직접 적용하지 않는다. 실제 힐 발동은
        ///   EnterHealLoopV3 → CastHeal(서버 권위) 경로가 그대로 담당한다(감시가 힐 로직을 바꾸지 않음).
        ///
        /// 기존 흐름과의 정합:
        ///   - 감시 중에는 _isInCombatPursuit이 false이므로 OnPathInvalidated가 정상적으로 _pendingPath를
        ///     예약할 수 있다(감시가 이를 소비해 이동 재개). 힐 루프 진입 중에는 EnterHealLoopV3가
        ///     _isInCombatPursuit=true로 두어 OnPathInvalidated가 힐을 끊지 않는다(추격과 동일 보호).
        ///   - 외부/공성 시스템이 새 MoveTo를 호출하면 _moveCoroutine이 교체되며 이 코루틴은 정지된다.
        /// </summary>
        private IEnumerator HealerIdleWatchV3()
        {
            // 부상 아군 재확인 주기(초). 너무 짧으면 매 프레임 탐색 비용, 너무 길면 반응이 굼떠진다.
            const float watchInterval = 0.4f;

            while (_unitData != null && _unitData.IsAlive)
            {
                // (1) 부상 아군 감지 → 즉시 힐 루프 재진입. 힐 루프 종료 후에도 계속 지켜보기 위해 continue.
                if (ShouldEngage())
                {
                    yield return EnterHealLoopV3();
                    continue;
                }

                // (2) 이동 재개용 새 경로가 예약됐으면(OnPathInvalidated) 그 경로로 이동을 재개한다.
                //     MoveTo가 _moveCoroutine을 새 이동 코루틴으로 교체하므로 이 감시 코루틴은 여기서 종료한다.
                if (_pendingPath != null)
                {
                    List<HexCoord> resumePath = _pendingPath;
                    _pendingPath = null;
                    MoveToInternal(
                        resumePath,
                        UnitMovementIntentReason.HealerResume,
                        startNewCommand: false);
                    yield break;
                }

                // (3) 대기 — watchInterval 동안 프레임을 넘기며 대기하되, 대기 중 새 경로가 예약되면
                //     즉시 상단으로 돌아가 (2)에서 이동을 재개하도록 안쪽 루프를 빠져나온다.
                float waited = 0f;
                while (waited < watchInterval && _unitData != null && _unitData.IsAlive)
                {
                    if (_pendingPath != null) break;
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            // 유닛 사망 등으로 감시가 자연 종료된 경우 — 이동 코루틴 추적을 해제한다.
            //   (이동 재개(2번 경로)로 빠져나갈 때는 위에서 yield break하므로 이 지점에 도달하지 않는다.
            //    따라서 여기서 _moveCoroutine을 비워도 새 이동 코루틴을 잘못 지우지 않는다.)
            if (_moveCoroutine != null) _moveCoroutine = null;
        }

        /// <summary>
        /// 힐러가 힐 시전을 시작할 때 호출 — 대상 방향으로 회전하고 힐 애니메이션을 재생한다.
        ///
        /// 애니메이션은 공격 유닛과 동일한 Attack 상태(BloomFairy의 Attack 클립 = 힐 모션)를 사용한다.
        ///   - 싱글플레이/호스트(서버): Animator를 직접 CrossFade(StartCombatAnimation과 동일 가드).
        ///   - 멀티플레이 클라이언트: 서버가 OnUnitHealCastStarted를 발행 → NetworkCombatController가
        ///     _animState(Attack)로 레벨 동기화하여 각 클라이언트가 힐 모션을 재생한다.
        /// </summary>
        /// <param name="targetId">회복 대상 아군 유닛의 Id(회전 방향 계산에 사용).</param>
        private void PlayHealCastAnimation(int targetId)
        {
            // 대상 방향으로 즉시 스냅 회전(서버/싱글). NetworkTransform이 클라이언트에 보간 전달.
            Vector3 targetPos = _positionProvider != null
                ? _positionProvider.GetUnitWorldPosition(targetId)
                : Vector3.zero;
            bool canWriteSimulationRoot =
                !NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer;
            if (canWriteSimulationRoot
                && !_movementRotationWriterOwnsRoot
                && targetPos != Vector3.zero)
            {
                float angle = CalculateAttackAngle(targetPos);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            else if (canWriteSimulationRoot
                && _movementRotationWriterOwnsRoot
                && targetPos != Vector3.zero)
            {
                UnitMovementAuthorityObserver.ObserveAttackWriterOwnershipConflict(
                    _unitData != null ? _unitData.Id : -1);
            }
#endif

            // Attack(=힐) 애니메이션 CrossFade — 싱글/호스트만 직접 재생(클라는 레벨 동기화가 담당).
            bool applyCrossFadeHere = !NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer;
            if (applyCrossFadeHere && _animator != null)
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0);
                _currentAnimStateHash = StateAttack;
            }

            // 멀티플레이: 서버가 힐 시전 이벤트를 발행 → 클라이언트 힐 애니메이션 동기화.
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitHealCastStarted.OnNext(_unitData.Id);
        }

        /// <summary>
        /// [V3] 근접 전투 종료 후 A* 재개 시 출발 타일을 결정한다 (규칙 11/15).
        ///
        /// 동작:
        ///   1) (호출 측이 미리 계산한) forwardTile을 파라미터로 받음 — 이중 계산 방지.
        ///   2) ProcessStep으로 도메인 위치를 그 타일로 갱신 + FROM(이전 _unitData.Position) 해제.
        ///   3) 같은 타일이면 ProcessStep 생략 — 점유 누수 방지.
        ///   4) Walk 애니메이션 재개.
        ///   5) finalTarget으로 RequestMove → 새 path 반환. 호출 측이 외부 while로 재진입.
        ///
        /// 뒤쪽 타일로 복귀하지 않는다(규칙 11/15).
        ///
        /// [BUG-002 — 2026-05-13]
        ///   이전에는 transform.position을 forwardTile에 즉시 스냅했으나, 이는 규칙 9(걸어서 재개)
        ///   위반이며 1타일 순간이동의 직접 원인이었다. 이제 transform.position 정렬은 호출 측
        ///   (MoveAlongPathV3)에서 Lerp로 처리하므로, 이 함수는 도메인 갱신과 path 재발급만 담당한다.
        ///
        /// [중요] forwardTile 이중 계산 방지:
        ///   호출 측은 Lerp 시작 전에 FindForwardClosestTile을 1회 호출하여 forwardTile을 결정한 뒤,
        ///   그 값으로 transform을 정렬하고 동일한 forwardTile을 이 함수에 전달해야 한다.
        ///   만약 이 함수가 FindForwardClosestTile을 다시 호출하면, 정렬 후의 transform.position
        ///   기준으로 한 타일 더 앞쪽이 선택될 수 있어 도메인-뷰 좌표가 다시 어긋난다.
        /// </summary>
        private List<HexCoord> ResumeFromForwardTileV3(HexCoord finalTarget, HexCoord forwardTile)
        {
            if (_movementUseCase == null || _unitData == null || !_unitData.IsAlive)
                return null;

            // 같은 타일이면 도메인 갱신 불필요. 다른 타일이면 ProcessStep만 호출.
            // [2026-05-11 비활성화] RegisterOccupancyMove는 점유 시스템 폐기로 인해 호출 제거됨.
            if (forwardTile != _unitData.Position)
            {
                _movementUseCase.ProcessStep(_unitData, _unitData.Position, forwardTile);
            }

            // ────────────────────────────────────────────────────────────
            // [BUG-002 수정 — 2026-05-13] 즉시 스냅 제거.
            //
            //   이전 구현:
            //     Vector3 forwardWorld = HexMetrics.HexToWorld(forwardTile);
            //     Vector3 forwardView = ViewConverter.ToView(forwardWorld);
            //     forwardView.y += HexMetrics.UnitYOffset;
            //     transform.position = forwardView;   ← 1타일 순간이동의 원인.
            //
            //   현재:
            //     호출 측(MoveAlongPathV3)이 이 함수 호출 전에 Lerp로 forwardTile 중심까지
            //     "걸어서" 이동시킨 뒤 정렬 스냅까지 마쳤다고 가정한다.
            //     따라서 여기서는 도메인 갱신과 path 재발급만 처리한다.
            // ────────────────────────────────────────────────────────────

            // Walk 애니메이션 재개 — 전투 중 Attack 상태였을 수 있음.
            ResumeWalkAnimation();
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

            // 새 path 계산 — finalTarget으로 RequestMove. 실패 시 null 반환(호출 측이 종료 처리).
            List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, finalTarget);
            return newPath;
        }

        /// <summary>
        /// 이동 종료 시 정리 + 1회성 콜백 호출.
        /// 외부 while 종료(완주/사망/RESUME 실패) 직후 + path 비정상 종료 직후 등에서 호출.
        /// </summary>
        private void MoveCleanupAndCompleteV3(bool invokeCompletion = true)
        {
            _movementRotationWriterOwnsRoot = false;
            _moveCoroutine = null;
            ClearUnitActionShadowDesiredTarget();
            SetMovementHeldAnimation(true);

            // 혼잡도 기여 종료 — 이동이 끝났으니 새 코루틴 전까지는 발행하지 않는다.
            _isAStarMoving = false;

            // 이동이 완전히 종료되었으므로 부드러운 교체 관련 상태도 모두 초기화.
            // 이렇게 해두지 않으면 다음에 MoveTo가 호출되기 전에 OnPathInvalidated가 끼어들었을 때
            // 이미 종료된 코루틴의 잔재 정보를 보고 잘못된 분기를 탈 수 있다.
            _pendingPath = null;
            _currentNextTileCoord = null;

            // 1회성 이동 완료 콜백 (랠리→Castle 자동 이동 체인 등에서 사용).
            var callback = OnMoveComplete;
            OnMoveComplete = null;
            if (invokeCompletion)
                callback?.Invoke();
        }

        // ====================================================================
        // 타격 반응 (Animation Event → HitReaction)
        // ====================================================================

        /// <summary>
        /// Animation Event에서 AnimationEventRelay를 통해 호출.
        /// 공격 애니메이션의 타격 프레임에 맞춰 피격 대상에게 시각적 반응(스케일 펀치)을 적용.
        /// 실제 데미지 로직과는 무관 — 순수 비주얼 피드백.
        /// </summary>
        public void OnAttackHit()
        {
            if (_unitData == null || !_unitData.IsAlive) return;

            // 공격 이펙트 재생 (VFX + SFX 동시).
            // OnAttackHit은 모든 클라이언트에서 로컬로 실행되는 Animation Event이므로
            // 멀티플레이에서도 양쪽 화면에 공격 이펙트가 정상 재생된다.
            // EffectManager가 아직 없는 경우(초기화 전/테스트 등)를 대비해 ?. 연산자로 안전 처리.

            // VfxSpawnPoint가 연결된 유닛(피스톨러 등)은 총구 위치를 스폰 기준점으로 사용한다.
            // ※ 회전은 VfxSpawnPoint.rotation을 쓰지 않는다.
            //   VfxSpawnPoint가 스켈레톤 본(손 부위) 하위에 배치되어 있어,
            //   월드 회전에 본 고유 회전(약 0, -90, -90도)이 섞여 VFX가 엉뚱한 방향으로 발사되기 때문이다.
            //   위치는 본 덕분에 정확하므로 유지하고, 회전만 유닛 루트의 정면 방향으로 교체한다.
            Transform presentationTransform = PresentationTransform;
            Vector3 spawnPos = _vfxSpawnPoint != null
                ? _vfxSpawnPoint.position
                : presentationTransform.position;
            Quaternion spawnRot = Quaternion.LookRotation(presentationTransform.forward);
            EffectManager.Instance?.PlayUnitAttack(_unitData.Type, spawnPos, spawnRot);  // VFX
            AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);                    // SFX (규칙 15 — VFX와 짝)

            // ────────────────────────────────────────────────────────────────
            // 피격 연출 방출 타이밍 결정 (전투 타격 타이밍 동기화 Phase 3 — 축 4 / 3-2)
            //
            //   근접 유닛 : 이 자리(타격 프레임)에서 OnLocalAttackHit을 즉시 발행 → 즉시 피격 연출. (기존 동작)
            //   원거리 유닛: 트레이서(발사체)를 발사하고, 그 발사체가 "착탄"하는 순간에 OnLocalAttackHit을
            //               발행한다. 즉 트레이서 비행 시간만큼 피격 연출이 지연되어, 화살/탄환이
            //               실제로 맞는 순간과 피격 연출(HP 텍스트·피격 VFX·타격 반응)이 일치한다.
            //
            //   ※ 데미지 타이밍(서버 권위)은 여기서 전혀 건드리지 않는다. 트레이서는 순수 시각 표현이다.
            // ────────────────────────────────────────────────────────────────
            int attackerId = _unitData.Id;
            EffectManager em = EffectManager.Instance;

            if (_unitData.AttackRange >= RangedAttackThreshold && em != null)
            {
                // 도착 지점 = 발사 시점의 타겟 월드 위치(값으로 복사).
                //   비행 중 타겟이 파괴되어도 트레이서는 이 좌표까지 그대로 날아가 소멸한다(댕글링 참조 없음).
                //   _combatTargetTransform이 있으면 그 위치를, 없으면 백업 ID로 재조회(둘 다 실패 시 전방 폴백).
                Transform presentationTarget =
                    GetPresentationTransform(_combatTargetTransform);
                Vector3 targetPos = presentationTarget != null
                    ? presentationTarget.position
                    : GetTargetPresentationWorldPos(
                        _combatTargetId,
                        _combatTargetIsUnit);

                // 트레이서 발사. 착탄 콜백에서 OnLocalAttackHit을 발행하여 피격 연출을 착탄 시점에 방출한다.
                //   트레이서 프리셋이 없으면 PlayTracer가 콜백을 "즉시" 실행하므로 기존 즉시 방출로 폴백된다.
                em.PlayTracer(_unitData.Type, spawnPos, targetPos,
                    () => GameEvents.OnLocalAttackHit.OnNext(attackerId));
            }
            else
            {
                // 근접 유닛(또는 EffectManager 부재 시): 발사 순간 = 타격 순간이므로 즉시 신호를 발행한다.
                // HitPresentationQueue가 이 신호를 받아 해당 공격자의 보류 큐에서 피격 연출 1건을 방출한다.
                // 모든 클라이언트에서 로컬로 실행되므로 각 화면이 자기 애니메이션 타이밍에 맞춰 연출된다.
                // (전투 타격 타이밍 동기화 Phase 2 — 축 3)
                GameEvents.OnLocalAttackHit.OnNext(attackerId);
            }
        }

            // ====================================================================
        // Walk 애니메이션 — 클라이언트 전용 (NetworkCombatController의 ClientRpc에서 호출)
        // ====================================================================

        /// <summary>
        /// Walk 애니메이션 시작(CrossFade). 클라이언트 전용 — 서버는 MoveAlongPath에서 직접 제어.
        ///
        /// [Phase 2] 호출 경로:
        ///   NetworkUnit._animState(=Walk) 값 변경/스폰 시 → NetworkUnit.ApplyAnimState → 여기.
        ///   (과거의 엣지 트리거 Walk RPC를 레벨 동기화 값으로 대체 — 스폰 레이스 유실 방지.)
        /// 이미 Walk 상태면 리셋하지 않고 speed만 복원.
        /// </summary>
        public void StartWalkAnimation()
        {
            // _animator가 null이면 lazy-init 시도 (Initialize() 전에 상태 값이 적용되는 경우 대응 — 스폰 레이스)
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
            _currentAnimStateHash = StateWalk;
        }

        /// <summary>
        /// [Phase 2 신규] 공격(Attack) 애니메이션 루프를 재생한다 — CrossFade만 수행.
        ///
        /// 클라이언트에서 NetworkUnit의 애니메이션 상태 NetworkVariable(=Attack)이 적용될 때 호출된다
        /// (값 변경 및 스폰 시 현재 값 적용). Attack 클립은 Loop Time=ON이므로 1회 CrossFade로 무한 루프.
        ///
        /// 회전/타겟 추적은 StartCombatAnimation(StartCombatClientRpc 경로)이 담당하므로, 이 메서드는
        /// 타겟 정보를 받지 않고 순수하게 "Attack 상태 전환"만 담당한다(애니메이션과 조준의 책임 분리).
        /// _unitData/Initialize보다 먼저 호출될 수 있어 animator를 지연 초기화한다(스폰 레이스 대응).
        /// </summary>
        public void PlayAttackAnimation()
        {
            // _animator가 null이면 lazy-init 시도 (Initialize() 전에 상태 값이 적용되는 경우 대응 — 스폰 레이스)
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0);
            _currentAnimStateHash = StateAttack;
        }

        /// <summary>
        /// 전투 종료 후 Walk 재개 공통 처리.
        /// Animator.GetCurrentAnimatorStateInfo는 CrossFade 블렌딩 도중 출발 상태를 반환해
        /// 상태 판별이 어긋날 수 있으므로(이 클래스 상단 원칙 — Animator 상태 의존 제거),
        /// Animator를 조회하지 않고 로컬 추적 상태(_currentAnimStateHash)로 이미 Walk인지 판별한다.
        /// </summary>
        private void ResumeWalkAnimation()
        {
            if (_animator == null) return;
            _animator.speed = 1f;
            if (_currentAnimStateHash == StateWalk) return; // 이미 Walk — 재-CrossFade로 블렌드 리셋 방지
            _animator.CrossFadeInFixedTime(StateWalk, _attackToWalkBlend, 0);
            _currentAnimStateHash = StateWalk;
        }

        /// <summary>
        /// 순수 클라이언트가 서버의 Frozen 애니메이션 상태를 적용할 때 호출한다.
        /// 현재 걷기 포즈를 유지한 채 재생 속도만 0으로 만든다.
        /// </summary>
        public void FreezeWalkAnimation()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            _animator.speed = 0f;
        }

        /// <summary>
        /// 이동 목표는 유지되지만 재경로 대기/막힘으로 변위가 없는 동안 걷기 모션을 정지한다.
        /// </summary>
        public void HoldMovementAnimation()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            // 공격 전환 중에 목표가 막혀도 공격 pose를 얼리지 않고 모든 프리팹에 존재하는
            // Walk clip의 결정적인 첫 pose를 stationary fallback으로 사용한다.
            _animator.speed = 1f;
            _animator.Play(StateWalk, 0, 0f);
            _currentAnimStateHash = StateWalk;
            _animator.speed = 0f;
        }

        private bool _isFrozenAnim;
        private bool _isMovementHeldAnim;

        /// <summary>
        /// 서버 이동 루프의 빙결 표현을 상태 변화 시에만 적용하고 클라이언트에 복제한다.
        /// 위치·회전 결정은 계속 기존 서버 권위 이동 writer를 통한다.
        /// </summary>
        private void SetFrozenAnimation(bool frozen)
        {
            if (_isFrozenAnim == frozen) return;
            _isFrozenAnim = frozen;

            if (_animator != null)
                _animator.speed = frozen || _isMovementHeldAnim ? 0f : 1f;

            if (NetworkContext.IsNetworkActive && _unitData != null)
                GameEvents.OnUnitFreezeChanged.OnNext(
                    new UnitFreezeChangedEvent(_unitData.Id, frozen));
        }

        private void SetMovementHeldAnimation(bool held)
        {
            if (_isMovementHeldAnim == held) return;
            _isMovementHeldAnim = held;

            if (held && !_isFrozenAnim)
                HoldMovementAnimation();
            else if (_animator != null)
                _animator.speed = held || _isFrozenAnim ? 0f : 1f;

            if (NetworkContext.IsNetworkActive && _unitData != null)
                GameEvents.OnUnitMovementHeldChanged.OnNext(
                    new UnitMovementHeldChangedEvent(_unitData.Id, held));
        }

        // StopWalkAnimation() 제거 — Idle 상태가 없으므로 Walk 정지 전용 메서드 불필요.
        // Walk→Attack 전환: StartCombatAnimation()에서 Attack CrossFade 직접 호출.
        // Walk→Walk: 이동 재개 시 StartWalkAnimation()에서 Walk CrossFade 호출.

        // WaitForAttackCycleEnd() 제거됨 (2026-04-03).
        // Animator normalizedTime 기반 대기 → 도메인 AttackCooldownRemaining 기반 대기로 교체.
        // AttackCooldown = 공격 클립 길이이므로 쿨다운 만료 = 한 사이클 완료.
        // Animator 상태 의존성을 제거하여 CrossFade 블렌딩 중 상태 판별 오류를 근본적으로 방지.

        // ====================================================================
        // 전투 애니메이션 — 상태 기반 (RPC/이벤트에서 호출)
        // ====================================================================

        /// <summary>
        /// 전투 시작. 타겟 방향으로 즉시 스냅 회전 후 Attack CrossFade.
        /// Attack 클립은 Loop Time=ON이므로 별도 루프 로직 불필요 — CrossFade 1회 호출로 무한 루프.
        ///
        /// 호출 경로:
        ///   멀티플레이 → NetworkCombatController.StartCombatClientRpc → UnitView.StartCombatAnimation
        ///   싱글플레이 → GameEvents.OnCombatStarted → UnitView.StartCombatAnimation
        /// </summary>
        /// <param name="targetId">타겟 엔티티의 Id.</param>
        /// <param name="targetIsUnit">true=유닛, false=건물.</param>
        public void StartCombatAnimation(int targetId, bool targetIsUnit)
        {
            if (_unitData == null || !_unitData.IsAlive) return;

            // 순수 클라이언트는 canonical Simulation Root를 NetworkTransform에서만 받으므로 쓰지 않는다.
            bool canWriteSimulationRoot =
                !NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer;
            if (canWriteSimulationRoot)
            {
                if (_movementRotationWriterOwnsRoot)
                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementAuthorityObserver.ObserveAttackWriterOwnershipConflict(
                        _unitData.Id);
#endif
                }
                else
                {
                    float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }
            }

            // ────────────────────────────────────────────────────────────────
            // [Phase 2 대체] Attack CrossFade 책임 분리.
            //   멀티플레이 "클라이언트"의 Attack 전환은 이제 애니메이션 상태 레벨 동기화가 담당한다
            //   (NetworkUnit._animState = Attack → PlayAttackAnimation). 스폰 레이스로 유실될 수 없다.
            //   여기서는 "싱글플레이" 또는 "호스트(서버)"만 직접 CrossFade한다:
            //     - 싱글플레이: NGO가 없어 레벨 동기화 경로가 없으므로 직접 재생해야 한다.
            //     - 호스트(서버): 서버가 Animator를 직접 제어하는 기존 관례를 유지한다
            //       (서버가 애니메이션을 직접 제어하는 다른 경로들과 동일한 취지).
            //   클라이언트가 여기서도 CrossFade하면 레벨 동기화와 이중 적용되므로 스킵한다.
            // ────────────────────────────────────────────────────────────────
            bool applyCrossFadeHere = !NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer;
            if (applyCrossFadeHere && _animator != null)
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0);
                _currentAnimStateHash = StateAttack;
            }

            // 공격 중 타겟 Transform 참조 저장 → Update()에서 매 프레임 추적.
            // [Phase 2] 이 타겟 참조/회전 추적은 애니메이션과 분리해 "유지"한다(클라이언트도 필요):
            //   원거리 유닛의 트레이서 조준(OnAttackHit)이 _combatTargetTransform/_combatTargetId를 사용한다.
            _combatTargetTransform = GetTargetTransform(targetId, targetIsUnit);
            ClearUnitActionShadowDesiredTarget();

            // 멀티플레이 타이밍 문제 대비: Transform 참조가 나중에 null이 되더라도
            // 백업 ID로 재조회할 수 있도록 ID를 저장한다.
            _combatTargetId = targetId;
            _combatTargetIsUnit = targetIsUnit;
        }

        /// <summary>
        /// 전투 중 타겟 변경. 회전만 업데이트, 애니메이션 상태 변경 없음.
        /// Attack 루프는 이미 재생 중이므로 끊김 없이 방향만 전환.
        ///
        /// 호출 경로:
        ///   멀티플레이 → NetworkCombatController.ChangeTargetClientRpc → UnitView.ChangeTarget
        ///   싱글플레이 → GameEvents.OnCombatTargetChanged → UnitView.ChangeTarget
        /// </summary>
        /// <param name="targetId">새 타겟 엔티티의 Id.</param>
        /// <param name="targetIsUnit">true=유닛, false=건물.</param>
        public void ChangeTarget(int targetId, bool targetIsUnit)
        {
            if (_unitData == null || !_unitData.IsAlive) return;

            // 추적 대상을 새 타겟으로 교체 → Update()가 새 타겟 방향 추적 시작
            _combatTargetTransform = GetTargetTransform(targetId, targetIsUnit);
            ClearUnitActionShadowDesiredTarget();

            // 새 타겟의 ID로 백업을 갱신한다.
            _combatTargetId = targetId;
            _combatTargetIsUnit = targetIsUnit;
        }

        /// <summary>
        /// 전투 종료 알림. 멀티플레이에서는 의도적으로 아무것도 하지 않음.
        ///
        /// 호출 경로:
        ///   멀티플레이 → NetworkCombatController.StopCombatClientRpc → UnitView.StopCombatAnimation
        ///   싱글플레이 → GameEvents.OnCombatStopped → UnitView.StopCombatAnimation
        ///
        /// 비어있는 이유 (멀티플레이):
        ///   Walk 전환은 서버가 이동을 재개할 때 _animState(=Walk) 레벨 동기화로 명시적으로 신호를 보냄.
        ///   StopCombatClientRpc 도착 시점과 서버의 실제 이동 재개 시점이 다를 수 있으므로,
        ///   여기서 Walk CrossFade를 즉시 호출하면 서버가 아직 쿨다운(최대 3초) 대기 중인 동안
        ///   클라이언트만 Walk 애니메이션이 재생되고 유닛이 실제로 이동하지 않는 불일치가 발생.
        ///   → Walk 전환 타이밍은 _animState 레벨 동기화에 완전히 위임.
        ///
        /// 싱글플레이:
        ///   MoveAlongPath 내부에서 Walk CrossFade를 직접 호출하므로 이 메서드 불필요.
        /// </summary>
        public void StopCombatAnimation()
        {
            // 회전 추적 중단 — null로 초기화하면 Update()가 자동으로 건너뜀
            _combatTargetTransform = null;

            // Transform 참조와 함께 백업 ID도 초기화하여 Update()의 재조회 시도를 차단한다.
            _combatTargetId = -1;
            ClearUnitActionShadowDesiredTarget();

            // 의도적으로 비워둠 — 위 summary 참조.
        }
    }
}
