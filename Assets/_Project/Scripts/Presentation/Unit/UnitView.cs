// ============================================================================
// UnitView.cs
// 유닛의 비주얼(이동, 방향, 애니메이션 상태)을 담당하는 컴포넌트.
//
// 회전 동기화 방식:
//   - 서버 Simulation Root는 공통 최대 270°/s로 타겟 방향을 점진 추적한다.
//   - 멀티플레이에서는 서버 rotation을 NetworkTransform이 클라이언트에 보간 전달한다.
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
    //   SetDependencies도 Application 인터페이스로 팩토리를 받아 IUnitView 계약에 포함한다.
    // [2026-10-26 수정안 (a)] partial로 바꿨다 — 투사체 사망타 사망 표현 보류(사망 대역) 코드를 UnitView.DeathDeferral.cs에 따로 둔다.
    public partial class UnitView : MonoBehaviour, Hexiege.Application.IUnitView, IUnitActionPoseSource
    {
        private static ulong _nextHealLifetime;
        private ulong _healLifetime, _healSequence;
        private UnitHealCastPresentationCursor _healPresentation = new UnitHealCastPresentationCursor();
        private IDisposable _healGameEndSubscription;
        private bool _healClipHeld;
        private byte _appliedHealPriority;
        private NetworkUnit _healNetworkUnit;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private byte _healFirstEntryEvidence;
#endif
        private bool _healStationaryWatch;
        // 이동으로 모션이 닫혀도 실제 발동의 쿨다운은 같은 유닛 수명 동안 보존한다.
        private UnitData _healCooldownCaster;
        private UnitHealCastPresentation _healCooldownCast;
        private NetworkUnit _healCooldownNetworkOwner;
        private bool _healCooldownNetwork;
        private double _healCooldownDispatchAt, _healCooldownDue;
        private ulong _healLoopOwner;

        /// <summary>힐 전용 현재 표현 상태. 일반 Attack scope나 HP 상태가 아니다.</summary>
        public UnitHealCastPresentation HealPresentation => _healPresentation.Current;

        private double HealNow
        {
            get
            {
                if (_healNetworkUnit == null) _healNetworkUnit = GetComponent<NetworkUnit>();
                return _healNetworkUnit != null ? _healNetworkUnit.HealServerTime : Time.timeAsDouble;
            }
        }

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

        /// <summary>힐 전용 전환. Bloom 양 팀 prefab만 시험값 0.15초를 저장한다.</summary>
        [SerializeField] private float _healCastBlend = 0.08f;
        private Transform _healFacingTarget;
        private UnitView _healFacingTargetView;

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

        /// <summary> 유닛 팩토리 인터페이스. 타겟 유닛의 실제 transform 조회용. </summary>
        private IUnitFactory _unitFactory;

        /// <summary> 건물 오브젝트 조회 인터페이스. 타겟 건물의 실제 transform 조회용. </summary>
        private IBuildingObjectProvider _buildingFactory;

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

        // 깃발 좌표는 실제 경로 끝과 다르다. 도중 건설로 실제 도착 칸이 바뀌어도
        // 원래 깃발 주위에서 다시 선택하도록, 랠리 경유 중에만 이 문맥을 보관한다.
        private HexCoord? _activeRallyPoint;
        private bool _rallyPolicyCompletionPending;

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
        private UnitPathRequestResult _lastPathRequestResult;
        private UnitMovementIntentReason _movementIntentReason;
        private bool _movementPendingCommand;
        private bool _movementPendingSegment;
        private UnitMovementReducer _movementAuthorityReducer;
        private NetworkUnit _movementNetworkUnit;
        private UnitMovementPipelineMode _lockedMovementPipelineMode =
            UnitMovementPipelineMode.Legacy;
        // ReducerAuthoritative 경로에서 방금 계산한 trajectory가 현재 논리 waypoint를
        // 통과했는지 호출 측에 알려 준다. route cursor 소비에만 사용하며 Domain 위치나
        // ProcessStep/OnUnitEnteredTile을 직접 발생시키지 않는다.
        private bool _lastMovementTrajectoryReachedWaypoint;
        private bool _lastMovementCandidateAcquiredTarget;

        // Corridor resolver가 모든 후보를 거부한 마지막 표본을 보존한다.
        // 반복 frame마다 로그를 남기지 않고 terminal REPATH-FAIL-CLOSED 한 줄에만 결합하여
        // 실제 Simulation Root, Domain 좌표, logical path 시작점 중 어느 값이 어긋났는지 구분한다.
        private CorridorRepathEvidence _lastCorridorRepathEvidence;
        // Corridor resolver에 전달하는 delegate는 UnitView당 한 번만 만든다.
        // frame별 path/index는 호출 직전에 임시 필드로 설정하고 finally에서 반드시 비운다.
        private Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict> _corridorStepValidator;
        private IReadOnlyList<HexCoord> _activeCorridorPath;
        private int _activeCorridorWaypointIndex = -1;
        private bool _activeCorridorSelectedStartTileEgress;
        private SpatialCandidateEvaluation _activeCorridorLastEvaluation;
        private bool _activeCorridorHasEvaluation;
        private bool _spatialRecoverablePending;

        private struct CorridorRepathEvidence
        {
            public bool IsAvailable;
            public int Frame;
            public Vector3 RootView;
            public Vector3 RootDomain;
            public HexCoord RootHex;
            public HexCoord UnitDataPosition;
            public int PathCount;
            public int WaypointIndex;
            public HexCoord PathStart;
            public HexCoord CurrentWaypoint;
            public UnitPathSignature SourcePathSignature;
            public Vector3 SampleView;
            public HexCoord SampleHex;
            public int SampleIndex;
            public int SampleCount;
            public int MatchedPathIndex;
            public int FirstAllowedPathIndex;
            public int LastAllowedPathIndex;
            public bool IsWalkable;
            public UnitTrajectoryCorridorSampleResult SampleResult;
        }

        private readonly struct SpatialCandidateEvaluation
        {
            public UnitSpatialCandidateVerdict Verdict { get; }
            public UnitSpatialTransitionPlanStatus TransitionStatus { get; }
            public UnitSpatialPreflightResult Preflight { get; }
            public int TransitionCount { get; }
            public HexCoord RootBeforeHex { get; }
            public HexCoord CandidateHex { get; }
            public bool UsedStartTileEgress { get; }
            public CorridorRepathEvidence Evidence { get; }

            public SpatialCandidateEvaluation(
                UnitSpatialCandidateVerdict verdict,
                UnitSpatialTransitionPlanStatus transitionStatus,
                UnitSpatialPreflightResult preflight,
                int transitionCount,
                HexCoord rootBeforeHex,
                HexCoord candidateHex,
                bool usedStartTileEgress,
                CorridorRepathEvidence evidence)
            {
                Verdict = verdict;
                TransitionStatus = transitionStatus;
                Preflight = preflight;
                TransitionCount = transitionCount;
                RootBeforeHex = rootBeforeHex;
                CandidateHex = candidateHex;
                UsedStartTileEgress = usedStartTileEgress;
                Evidence = evidence;
            }
        }

        private enum MovementWriteOutcome
        {
            Rejected = 0,
            Held = 1,
            Advanced = 2,
            RepathRequired = 3,
            DeferredAttackHandoff = 4
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // 진단 저장소는 게임 상태를 소유하지 않는다. Release에서는 필드/호출/인자 계산까지 제외한다.
        private PostCombatTrace _postCombatTrace;
        private int _postCombatLastTarget = -1;
        private string _postCombatExitReason = "unavailable";

        private struct PostCombatSample
        {
            internal long Frame;
            internal double Time, SpatialTime, ProviderTime;
            internal long SpatialFrame, ProviderFrame;
            internal ulong Command, Segment, Environment;
            internal Vector3 RootView, RootDomain, Candidate, Center;
            internal HexCoord Committed, RootTile, Goal;
            internal int Waypoint, Target, PendingCount;
            internal string ExitReason;
            internal UnitPathSignature Route, Corridor, Pending;
            internal bool HasSpatial, Acquire, HasCenter;
            internal SpatialCandidateEvaluation Spatial;
            internal UnitPathRequestResult Provider;
            internal bool HasProvider;
            internal MovementWriteOutcome Outcome;
        }

        // 서버당 동시 512 View, View당 32 표본/1 episode, 경로별 256점. 오래된 저장소를
        // 퇴거하되 이후 episode 참여는 다시 허용한다. 누적 첫 N회 제한은 두지 않는다.
        private sealed class PostCombatTrace
        {
            internal const int HistoryCapacity = 32, RouteCapacity = 256;
            private const int UnitCapacity = 512;
            private static readonly PostCombatTrace[] Tracked = new PostCombatTrace[UnitCapacity];
            private static int _nextEviction;
            private static ulong _evictions;
            private PostCombatSample[] _history = new PostCombatSample[HistoryCapacity];
            private HexCoord[] _route = new HexCoord[RouteCapacity];
            private HexCoord[] _corridor = new HexCoord[RouteCapacity];
            private HexCoord[] _pending = new HexCoord[RouteCapacity];
            private int _count, _next, _routeCount, _corridorCount, _pendingCount, _slot;
            private ulong _episode, _event, _captures, _dumps, _overwrites, _pointDrops, _formatDrops, _faults;
            private readonly string _lifecycle = Guid.NewGuid().ToString("N");
            private readonly string _session;
            private readonly int _unit;
            internal bool Active, Triggered, BlockedDumped, Retired, ProgressObserved;
            internal bool HasPlanner;
            internal UnitPostCombatRecoveryStatus PlannerStatus;
            internal HexCoord PlannerCandidate;
            internal int PlannerSweeps;
            internal bool HasSpatial, HasCenter, HasProvider;
            internal SpatialCandidateEvaluation Spatial;
            internal Vector3 Candidate, Center;
            internal UnitPathRequestResult Provider;
            internal long SpatialFrame, ProviderFrame;
            internal double SpatialTime, ProviderTime;
            private ulong _episodeOverwrites, _episodePointDrops, _episodeFaults, _episodeFormatDrops;
            internal PostCombatSample Latest;

            internal PostCombatTrace(int unit)
            {
                _unit = unit;
                // 인증 세션이 아니라 기존 MOVE observer와 같은 공개 lobby ID의 해시다.
                string lobby = UnityEngine.Object.FindFirstObjectByType<NetworkGameManager>()?.CurrentLobby?.Id;
                _session = "unavailable";
                if (!string.IsNullOrEmpty(lobby))
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    _session = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(lobby)))
                        .Replace("-", "").ToLowerInvariant();
                }
                _slot = -1;
                for (int i = 0; i < Tracked.Length; i++)
                    if (Tracked[i] == null) { _slot = i; break; }
                if (_slot < 0)
                {
_slot = _nextEviction;
                    _nextEviction = (_nextEviction + 1) % UnitCapacity;
                    _evictions++;
                    Tracked[_slot].Retire("diagnostic-eviction");
                }
                Tracked[_slot] = this;
            }

            internal void Begin()
            {
                if (Active) Finish("episode-replaced-open");
                _episode++;
                _episodeOverwrites = _overwrites; _episodePointDrops = _pointDrops;
                _episodeFaults = _faults; _episodeFormatDrops = _formatDrops;
                Active = true;
                Triggered = BlockedDumped = HasSpatial = HasCenter = HasProvider = ProgressObserved = HasPlanner = false;
                _count = _next = _routeCount = _corridorCount = _pendingCount = 0;
                Latest = default;
            }

            internal void CopyPaths(IReadOnlyList<HexCoord> route, IReadOnlyList<HexCoord> corridor,
                IReadOnlyList<HexCoord> pending)
            {
                _routeCount = Copy(route, _route);
                _corridorCount = Copy(corridor, _corridor);
                _pendingCount = Copy(pending, _pending);
            }

            private int Copy(IReadOnlyList<HexCoord> path, HexCoord[] buffer)
            {
                int count = path?.Count ?? 0;
                int retained = Math.Min(count, buffer.Length);
                for (int i = 0; i < retained; i++) buffer[i] = path[i];
                _pointDrops += (ulong)(count - retained);
                return count;
            }

            internal void Capture(PostCombatSample sample)
            {
                Latest = sample;
                _captures++;
                if (_count == HistoryCapacity) _overwrites++;
                else _count++;
                _history[_next] = sample;
                _next = (_next + 1) % HistoryCapacity;
            }

            internal void Failure(string reason)
            {
                if (!Active || Triggered) return;
                Triggered = true;
                Dump(reason);
            }

            internal void Blocked(string origin, UnitRepathProgressGuard guard, int sweeps)
            {
                if (!Active || BlockedDumped) return;
                BlockedDumped = true;
                Triggered = true; // 최초 spatial 실패가 없는 Blocked도 이후 생명주기를 연결한다.
                Dump("blocked-entry");
                Emit("blocked-decision", $"origin={origin}, sweeps={sweeps}, candidateOrdinal=unavailable-private-planner-state, " +
                    $"acceptedInFrame={guard?.AcceptedInFrame ?? 0}, acceptedWithoutProgress={guard?.AcceptedWithoutProgress ?? 0}");
            }

            internal void Transition(string reason)
            {
                if (!Active) return;
                Capture(Latest);
                if (Triggered) { Emit("transition", $"reason={reason}"); EmitSample(Latest, "current"); }
            }

            internal void Finish(string reason)
            {
                if (!Active) return;
                if (Triggered) { Emit("episode-end", $"reason={reason}, closureObserved={reason != "episode-replaced-open"}"); EmitSample(Latest, "end-state"); }
                Active = false;
            }

            internal void Retire(string reason)
            {
                if (Retired) return;
                Emit("retire", $"reason={reason}, openEpisode={Active}, triggered={Triggered}, captures={_captures}, " +
                    $"dumps={_dumps}, overwrites={_overwrites}, pointDrops={_pointDrops}, formatDrops={_formatDrops}, faults={_faults}, evictions={_evictions}");
                Active = false;
                Retired = true;
                if (_slot >= 0 && Tracked[_slot] == this) Tracked[_slot] = null;
                // 퇴거된 View가 살아 있어도 진단 배열은 즉시 놓아 총 저장량을 유한하게 유지한다.
                _history = null; _route = _corridor = _pending = null;
            }

            internal void Fault() { _faults++; }

            private void Dump(string reason)
            {
                _dumps++;
                Emit("dump", $"reason={reason}, history={_count}, capacity={HistoryCapacity}, unitCapacity={UnitCapacity}, " +
                    $"routeCapacity={RouteCapacity}, activeEpisodeCapacity=1, captures={_captures}, dumps={_dumps}, " +
                    $"overwrites={_overwrites}, pointDrops={_pointDrops}, formatDrops={_formatDrops}, faults={_faults}, evictions={_evictions}, " +
                    $"episodeOverwrites={_overwrites - _episodeOverwrites}, episodePointDrops={_pointDrops - _episodePointDrops}, " +
                    $"historyComplete={(_overwrites == _episodeOverwrites && _pointDrops == _episodePointDrops && _faults == _episodeFaults && _formatDrops == _episodeFormatDrops)}");
                if (_overwrites != _episodeOverwrites || _pointDrops != _episodePointDrops
                    || _faults != _episodeFaults || _formatDrops != _episodeFormatDrops)
                    Emit("incomplete", $"episodeOverwrites={_overwrites - _episodeOverwrites}, episodePointDrops={_pointDrops - _episodePointDrops}, " +
                        $"episodeFaults={_faults - _episodeFaults}, episodeFormatDrops={_formatDrops - _episodeFormatDrops}", warning: true);
                int start = (_next - _count + HistoryCapacity) % HistoryCapacity;
                for (int i = 0; i < _count; i++) EmitSample(_history[(start + i) % HistoryCapacity], "history");
                EmitPath("original", _route, _routeCount);
                EmitPath("resume", _corridor, _corridorCount);
                EmitPath("pending", _pending, _pendingCount);
            }

            private void EmitSample(PostCombatSample s, string kind)
            {
                Emit(kind, $"frame={s.Frame}, time={N(s.Time)}, clockDomain=unity-scaled, command={s.Command}, segment={s.Segment}, env={s.Environment}, " +
                    $"rootView={V(s.RootView)}, rootDomain={V(s.RootDomain)}, rootTile={H(s.RootTile)}, committed={H(s.Committed)}, goal={H(s.Goal)}, " +
                    $"centerAvailable={s.HasCenter}, center={V(s.Center)}, centerDistanceXZ={(s.HasCenter ? N(CalculateMovementPositionDeltaXZ(s.RootView, s.Center)) : "unavailable")}, " +
                    $"candidate={V(s.Candidate)}, waypoint={s.Waypoint}, route={S(s.Route)}, corridor={S(s.Corridor)}, pending={S(s.Pending)}, pendingCount={s.PendingCount}, " +
                    $"target={s.Target}, exitReason={s.ExitReason ?? "unavailable"}, acquire={s.Acquire}, outcome={s.Outcome}");
                if (s.HasSpatial)
                {
                    SpatialCandidateEvaluation x = s.Spatial;
                    Emit("spatial", $"sampleFrame={s.Frame}, sourceFrame={s.SpatialFrame}, sourceTime={N(s.SpatialTime)}, verdict={x.Verdict}, transition={x.TransitionStatus}, preflight={x.Preflight.Status}, " +
                        $"offendingTile={H(x.Preflight.OffendingTile)}, offendingIndex={x.Preflight.OffendingTransitionIndex}, transitions={x.TransitionCount}, " +
                        $"rootBeforeHex={H(x.RootBeforeHex)}, candidateHex={H(x.CandidateHex)}, startEgress={x.UsedStartTileEgress}, evidenceAvailable={x.Evidence.IsAvailable}");
                    CorridorRepathEvidence e = x.Evidence;
                    if (e.IsAvailable)
                        Emit("failed-sample", $"sampleFrame={s.Frame}, rootView={V(e.RootView)}, rootDomain={V(e.RootDomain)}, rootHex={H(e.RootHex)}, " +
                            $"committed={H(e.UnitDataPosition)}, sourcePath={S(e.SourcePathSignature)}, pathCount={e.PathCount}, waypoint={e.WaypointIndex}, " +
                            $"start={H(e.PathStart)}, waypointTile={H(e.CurrentWaypoint)}, sampleView={V(e.SampleView)}, tile={H(e.SampleHex)}, " +
                            $"index={e.SampleIndex}, count={e.SampleCount}, matched={e.MatchedPathIndex}, first={e.FirstAllowedPathIndex}, last={e.LastAllowedPathIndex}, " +
                            $"walkable={e.IsWalkable}, result={e.SampleResult}, evidenceFrame={e.Frame}");
                    else Emit("failed-sample", $"sampleFrame={s.Frame}, reason=no-corridor-sample-evidence, walkability=unavailable-not-observed");
                }
                else Emit("spatial", $"sampleFrame={s.Frame}, reason=no-final-spatial-stage-observed");
                if (s.HasProvider)
                    Emit("provider", $"sampleFrame={s.Frame}, sourceFrame={s.ProviderFrame}, sourceTime={N(s.ProviderTime)}, status={s.Provider.Status}, start={H(s.Provider.AuthoritativeStart)}, " +
                        $"root={H(s.Provider.RootTile)}, goal={H(s.Provider.Goal)}, env={s.Provider.EnvironmentRevision}, source=observed-existing-return");
                else Emit("provider", $"sampleFrame={s.Frame}, status=unavailable-no-request-in-episode");
            }

            private void EmitPath(string kind, HexCoord[] buffer, int count)
            {
                Emit("path", $"kind={kind}, count={count}, retained={Math.Min(count, RouteCapacity)}, dropped={Math.Max(0, count - RouteCapacity)}");
                for (int i = 0; i < Math.Min(count, RouteCapacity); i += 8)
                {
                    var b = new System.Text.StringBuilder();
                    for (int j = i; j < Math.Min(i + 8, Math.Min(count, RouteCapacity)); j++)
                    { if (j != i) b.Append('|'); b.Append(H(buffer[j])); }
                    Emit("path-points", $"kind={kind}, startIndex={i}, points={b}");
                }
            }

            private void Emit(string kind, string payload, bool warning = false)
            {
                // 정적 480자(ASCII 값만 사용) chunk와 전체 UTF-8 검사로 Android 잘림을 피한다.
                // 모든 part는 같은 event를 공유한다. 값의 임의 절단은 조합 가능한 payload로 명시한다.
                ulong eventId = ++_event;
                int parts = Math.Max(1, (payload.Length + 479) / 480);
                for (int part = 0; part < parts; part++)
                {
                    string content = payload.Substring(part * 480, Math.Min(480, payload.Length - part * 480));
                    string data = $"schema=pc-recovery-v1, session={_session}, lifecycle={_lifecycle}, unit={_unit}, episode={_episode}, " +
                        $"event={eventId}, kind={kind}, part={part + 1}, parts={parts}, payload={content}";
                    const string prefix = "[23:59:59.999] [WARN] [Movement/UnitView] [UAS-PC-RECOVERY] | ";
                    if (System.Text.Encoding.UTF8.GetByteCount(prefix + data + "\n") >= 900)
                    {
                        _formatDrops++;
                        GameLog.Dev.Warn("Movement", nameof(UnitView), "[UAS-PC-RECOVERY]",
                            $"session={_session}, lifecycle={_lifecycle}, unit={_unit}, episode={_episode}, event={eventId}, part={part + 1}, parts={parts}, formatIncomplete=True, drops={_formatDrops}");
                        continue;
                    }
                    if (warning) GameLog.Dev.Warn("Movement", nameof(UnitView), "[UAS-PC-RECOVERY]", data);
                    else GameLog.Dev.Info("Movement", nameof(UnitView), "[UAS-PC-RECOVERY]", data);
                }
            }
            private static string N(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            private static string V(Vector3 v) => $"{N(v.x)};{N(v.y)};{N(v.z)}";
            private static string H(HexCoord v) => $"{v.Q};{v.R}";
            private static string S(UnitPathSignature v) => v.IsValid ? $"{v.WaypointCount}:{v.OrderedHash:x16}" : "invalid";
        }

        private PostCombatSample PostCombatState(int waypoint, HexCoord goal, IReadOnlyList<HexCoord> route,
            IReadOnlyList<HexCoord> corridor, bool acquire, MovementWriteOutcome outcome)
        {
            Vector3 root = transform.position;
            Vector3 domain = ViewConverter.FromView(root);
            UnitPathSignature.TryCreate(route, out UnitPathSignature routeSignature);
            UnitPathSignature.TryCreate(corridor, out UnitPathSignature corridorSignature);
            UnitPathSignature.TryCreate(_pendingPath, out UnitPathSignature pendingSignature);
            return new PostCombatSample
            {
                Frame = Time.frameCount, Time = Time.timeAsDouble,
                Command = _movementCommandRevision, Segment = _movementSegmentRevision, Environment = _navigationEnvironmentRevision,
                RootView = root, RootDomain = domain, RootTile = HexMetrics.WorldToHex(domain),
                Committed = _unitData.Position, Goal = goal, Waypoint = waypoint, Target = _postCombatLastTarget, ExitReason = _postCombatExitReason,
                Route = routeSignature, Corridor = corridorSignature, Pending = pendingSignature, PendingCount = _pendingPath?.Count ?? 0,
                HasSpatial = _postCombatTrace.HasSpatial, Spatial = _postCombatTrace.Spatial, Candidate = _postCombatTrace.Candidate,
                HasCenter = _postCombatTrace.HasCenter, Center = _postCombatTrace.Center,
                SpatialFrame = _postCombatTrace.SpatialFrame, SpatialTime = _postCombatTrace.SpatialTime,
                ProviderFrame = _postCombatTrace.ProviderFrame, ProviderTime = _postCombatTrace.ProviderTime,
                HasProvider = _postCombatTrace.HasProvider, Provider = _postCombatTrace.Provider, Acquire = acquire, Outcome = outcome
            };
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void BeginPostCombatTrace(IReadOnlyList<HexCoord> route, int waypoint, HexCoord goal)
        {
            if (!NetworkContext.IsNetworkActive || !NetworkContext.IsNetworkServer || _unitData == null) return;
            try
            {
                if (_postCombatTrace == null || _postCombatTrace.Retired) _postCombatTrace = new PostCombatTrace(_unitData.Id);
                _postCombatTrace.Begin();
                _postCombatTrace.CopyPaths(route, null, _pendingPath);
                _postCombatTrace.Capture(PostCombatState(waypoint, goal, route, null, false, MovementWriteOutcome.Held));
            }
            catch (Exception) { _postCombatTrace?.Fault(); }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void CapturePostCombatTrace(IReadOnlyList<HexCoord> route, IReadOnlyList<HexCoord> corridor,
            int waypoint, HexCoord goal, bool acquire, MovementWriteOutcome outcome)
        {
            if (_postCombatTrace == null || !_postCombatTrace.Active || _postCombatTrace.Retired) return;
            try
            {
                _postCombatTrace.CopyPaths(route, corridor, _pendingPath);
                _postCombatTrace.Capture(PostCombatState(waypoint, goal, route, corridor, acquire, outcome));
                if (outcome == MovementWriteOutcome.RepathRequired || outcome == MovementWriteOutcome.Rejected)
                    _postCombatTrace.Failure("first-corridor-rejection");
            }
            catch (Exception) { _postCombatTrace.Fault(); }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void PostCombatTraceEvent(string reason, bool close = false, bool retire = false)
        {
            if (_postCombatTrace == null || _postCombatTrace.Retired) return;
            try
            {
                // 실제 현재 pose만 갱신한다. 새 경로/감지/안전 판정은 호출하지 않는다.
                PostCombatSample s = _postCombatTrace.Latest;
                s.Frame = Time.frameCount; s.Time = Time.timeAsDouble; s.RootView = transform.position;
                s.RootDomain = ViewConverter.FromView(s.RootView); s.RootTile = HexMetrics.WorldToHex(s.RootDomain);
                s.Committed = _unitData != null ? _unitData.Position : s.Committed;
                s.Environment = _navigationEnvironmentRevision; s.Command = _movementCommandRevision; s.Segment = _movementSegmentRevision;
                s.Target = _postCombatLastTarget; s.ExitReason = _postCombatExitReason;
                _postCombatTrace.Latest = s;
                if (retire) _postCombatTrace.Retire(reason);
                else if (close) _postCombatTrace.Finish(reason);
                else _postCombatTrace.Transition(reason);
            }
            catch (Exception) { _postCombatTrace.Fault(); }
        }

        // 기존 제공자 반환의 값만 복사하며 가변 Path 참조는 저장하지 않는다.
        private void ObservePostCombatProvider()
        {
            if (_postCombatTrace == null || !_postCombatTrace.Active || _postCombatTrace.Retired) return;
            try
            {
                UnitPathRequestResult p = _lastPathRequestResult;
                _postCombatTrace.Provider = new UnitPathRequestResult(p.Status, null,
                    p.AuthoritativeStart, p.RootTile, p.Goal, p.EnvironmentRevision);
                _postCombatTrace.HasProvider = true;
                _postCombatTrace.ProviderFrame = Time.frameCount;
                _postCombatTrace.ProviderTime = Time.timeAsDouble;
                PostCombatSample s = _postCombatTrace.Latest;
                s.Provider = _postCombatTrace.Provider; s.HasProvider = true;
                s.ProviderFrame = _postCombatTrace.ProviderFrame; s.ProviderTime = _postCombatTrace.ProviderTime;
                _postCombatTrace.Latest = s;
                PostCombatTraceEvent("provider-return");
            }
            catch (Exception) { _postCombatTrace.Fault(); }
        }

        private void ObservePostCombatPlanner(UnitPostCombatRecoveryResult result)
        {
            if (_postCombatTrace == null || !_postCombatTrace.Active || _postCombatTrace.Retired) return;
            try
            {
                var t = _postCombatTrace;
                // 변하지 않은 planner 대기는 파일 로그/문자열 할당 없이 넘긴다.
                if (t.HasPlanner && t.PlannerStatus == result.Status && t.PlannerCandidate == result.Candidate
                    && t.PlannerSweeps == result.CompletedSweeps) return;
                t.HasPlanner = true; t.PlannerStatus = result.Status;
                t.PlannerCandidate = result.Candidate; t.PlannerSweeps = result.CompletedSweeps;
                PostCombatTraceEvent($"planner-{result.Status}-sweeps-{result.CompletedSweeps}-candidate-{result.Candidate.Q};{result.Candidate.R}");
            }
            catch (Exception) { _postCombatTrace.Fault(); }
        }

        private void ObservePostCombatBlocked(UnitRepathProgressGuard guard, int sweeps, string origin)
        {
            if (_postCombatTrace == null || !_postCombatTrace.Active || _postCombatTrace.Retired) return;
            try { _postCombatTrace.Blocked(origin, guard, sweeps); }
            catch (Exception) { _postCombatTrace.Fault(); }
        }
#endif

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
        // Attack 클립 재생 여부와 서버 공격 회차 커밋 여부는 별개다. 커밋 한 번이 허용한
        // HitIndex만 정확히 한 번씩 소비하고, 루프 클립의 이후 Marker는 닫힌 lease에서 차단한다.
        private AttackPresentationImpactLease _attackPresentationImpactLease;
        // 서버 commit 하나가 허용한 공격자 발사 VFX/SFX marker 수명이다. 정규 scope와
        // 분리되어 있어 Unresolved Legacy 타입에도 가짜 scope를 만들지 않는다.
        private AttackPresentationSourceMarkerLease _attackPresentationSourceMarkerLease;
        private AttackSourceOccurrenceLease _attackOccurrenceLease = new AttackSourceOccurrenceLease();
        private const int PendingNativeOwnerLimit = 8;
        // native 재생은 연속이어도 marker 허가와 target 문맥은 회차별 불변 값이다.
        // 후속 cursor 갱신은 미소모 이전 occurrence를 닫는 이유가 아니다.
        private sealed class NativeOccurrenceContext
        {
            public UnitAttackPhasePresentation Owner;
            public AnimationClip Clip;
            public AttackPresentationReplicatedSnapshot Snapshot;
            public Transform Target;
            public Vector3 TargetPosition;
            public Quaternion Rotation;
            public AttackSourceOccurrenceLease Lease;
        }
        private NativeOccurrenceContext _nativeOccurrenceContext;
        private readonly List<NativeOccurrenceContext> _pendingNativeOwners = new List<NativeOccurrenceContext>(PendingNativeOwnerLimit);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public AttackPresentationScope AttackPhaseLastEmissionScope { get; private set; }
        public int AttackPhaseLastEmissionTarget { get; private set; }
        public Vector3 AttackPhaseLastEmissionTargetPosition { get; private set; }
        public Quaternion AttackPhaseLastEmissionRotation { get; private set; }
#endif
        private UnitAttackPhasePresentation _attackPhase;
        private NetworkUnit _attackPhaseNetworkUnit;
        private AnimationClip _attackPhaseClip;
        private AnimationEvent _nativeAttackMarker;
        private double _attackPhaseClockHighWater = double.NaN;
        private bool _attackPhaseClockRegressed, _attackPhaseOwnsSpeed;
        private bool _attackPhaseFirstEntry;
        private ulong _nativeAppliedEpoch, _nativeAppliedLifetime;
        private string _attackPhaseHoldReason;
        private ulong _attackPhaseHoldEpisode;
        private double _attackPhaseApplyClock;
        private UnitAttackPhasePresentation _attackPhaseRetiringOwner;
        private bool _attackPhaseEntryTransaction;
        private float _attackPhaseSavedSpeed = 1f;
        public int AttackPhaseNativeEmissions { get; private set; }
        public int AttackPhaseNativeRejections { get; private set; }
        public string AttackPhaseRejectionReason { get; private set; }
        private ulong _attackPhaseRejectedRevision;
        private bool RejectAttackPhase(UnitAttackPhasePresentation value, string reason)
        {
            if (_attackPhaseRejectedRevision != value.PresentationRevision || AttackPhaseRejectionReason != reason)
                PistoleerTimingDiagnostics.Event(_unitData, "Pending", value.Scope, double.NaN,
                    $"reason={reason} presentationRev={value.PresentationRevision} applied=0");
            _attackPhaseRejectedRevision = value.PresentationRevision;
            AttackPhaseRejectionReason = reason;
            return false;
        }

        public bool CanUseNativeAttackPhase(UnitData unit)
        {
            if (unit == null || unit.IsHealer || _animator == null
                || _animator.runtimeAnimatorController == null
                || !UnitAttackShadowProfileResolver.CanUseNativeAttackPhase(unit.Type)) return false;
            AnimationClip found = null;
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                if (clip.name.EndsWith("_Attack", StringComparison.Ordinal))
                {
                    if (found != null && found != clip) return false;
                    found = clip;
                }
            if (found == null || !found.isLooping || found.length != unit.AttackCooldown
                || unit.HitFrameTimes == null) return false;
            int index = 0;
            float previousMarker = -1f;
            foreach (var marker in found.events)
                if (marker.functionName == "OnAttackHit")
                {
                    // same-zero/same-time은 기존 Legacy 계약이다. native의 고유 occurrence로
                    // 옮길 수 없는 marker는 publish 전에 제외해 기존 경로를 유지한다.
                    if (float.IsNaN(marker.time) || float.IsInfinity(marker.time)
                        || marker.time < 0 || marker.time >= found.length || marker.time <= previousMarker) return false;
                    previousMarker = marker.time;
                    if (index >= unit.HitFrameTimes.Length || marker.time != unit.HitFrameTimes[index++]) return false;
                }
            if (index == 0 || index != unit.HitFrameTimes.Length) return false;
            _attackPhaseClip = found;
            return true;
        }

        public bool ApplyAttackPhasePresentation(UnitAttackPhasePresentation value)
        {
            if (_unitData == null || _animator == null) return RejectAttackPhase(value, "view-not-ready");
            if (!_unitData.IsAlive || _unitData.Id != value.UnitId || _unitData.IsHealer
                || !UnitAttackPresentationPolicy.IsValidPhasePayload(value)) return RejectAttackPhase(value, "invalid-owner-payload");
            if (_isFrozenAnim || _isMovementHeldAnim) return RejectAttackPhase(value, "priority-held");
            if (NetworkContext.IsNetworkServer && !_rootRotationOwnership.ActionOwnsRoot)
                return RejectAttackPhase(value, "root-not-owned");
            if (_attackPhaseNetworkUnit == null) _attackPhaseNetworkUnit = GetComponent<NetworkUnit>();
            if (_attackPhaseNetworkUnit == null || !_attackPhaseNetworkUnit.TryReadAttackPresentationClock(out double raw, out ulong generation)
                || generation != value.Lifetime) return RejectAttackPhase(value, "clock-not-ready");
            if (_attackPhase.Active && (_attackPhase.Lifetime != value.Lifetime
                || value.PresentationRevision <= _attackPhase.PresentationRevision)) return RejectAttackPhase(value, "stale-owner-revision");
            if (!CanUseNativeAttackPhase(_unitData) || _attackPhaseClip.length != (float)value.Duration) return RejectAttackPhase(value, "unsupported-native-metadata");
            var markers = new List<float>();
            foreach (var marker in _attackPhaseClip.events) if (marker.functionName == "OnAttackHit") markers.Add(marker.time);
            if (_unitData.HitFrameTimes == null || markers.Count != _unitData.HitFrameTimes.Length) return RejectAttackPhase(value, "marker-count-mismatch");
            for (int i = 0; i < markers.Count; i++) if (markers[i] != _unitData.HitFrameTimes[i]) return RejectAttackPhase(value, "marker-time-mismatch");
            bool firstEpoch = _nativeAppliedEpoch != value.EpochId || _nativeAppliedLifetime != value.Lifetime;
            if (_attackPhase.Active && !firstEpoch && _attackPhase.EpochServerTime != value.EpochServerTime) return RejectAttackPhase(value, "epoch-time-mismatch");
            if (value.Mode == AttackPresentationImpactMode.Scoped)
                foreach (var hit in value.Hits)
                    if (hit.HitIndex >= markers.Count || (float)hit.MarkerOffset != markers[hit.HitIndex]) return RejectAttackPhase(value, "occurrence-marker-mismatch");
            if (firstEpoch) { _attackPhaseClockHighWater = raw; _attackPhaseClockRegressed = false; }
            _attackPhaseApplyClock = raw;
            _attackPhaseRetiringOwner = _attackPhase;
            _attackPhase = value;
            if (value.Hits != null) _attackPhase.Hits = (AttackSourceOccurrence[])value.Hits.Clone();
            if (value.PresentationRevision > _lastAttackPresentationRevision)
            {
                _attackPhaseFirstEntry = firstEpoch;
                _attackPhaseEntryTransaction = true;
                StartCombatAnimation(value.TargetId, value.TargetIsUnit, false, value.PresentationRevision, value.Mode, value.Scope);
                _attackPhaseEntryTransaction = false;
                _attackPhaseFirstEntry = false;
            }
            else if (firstEpoch && value.PresentationRevision == _lastAttackPresentationRevision)
            {
                // RED compatibility control; production NCC no longer preapplies Start.
                BindNativeAttackOccurrence(raw, generation);
                AlignFirstNativeAttackEpoch(raw);
            }
            if (_lastAttackPresentationRevision != value.PresentationRevision || _currentAnimStateHash != StateAttack)
                return RejectAttackPhase(value, "native-entry-not-applied");
            _nativeAppliedLifetime = value.Lifetime; _nativeAppliedEpoch = value.EpochId;
            AttackPhaseRejectionReason = null;
            return true;
        }

        public bool HasNativeAttackEpoch(ulong lifetime, ulong epoch)
            => _attackPhase.Active && _nativeAppliedLifetime == lifetime && _nativeAppliedEpoch == epoch;

        public bool CanApplyHostNativeAttackPhase()
            => NetworkContext.IsNetworkServer && _unitData != null && _unitData.IsAlive
                && _animator != null && _rootRotationOwnership.ActionOwnsRoot
                && !_isFrozenAnim && !_isMovementHeldAnim;

        private void BindNativeAttackOccurrence(double raw, ulong generation)
        {
            var value = _attackPhase;
            ObserveNativeLease("bind-before", value.Scope, raw);
            int skipped = 0;
            int baseline = -1;
            var replicated = _attackPhaseNetworkUnit.AttackShadowState;
            if (replicated.AttackerInstanceId == value.Scope.AttackerInstanceId.Value
                && replicated.SequenceId == value.Scope.SequenceId.Value)
                baseline = replicated.LastImpactHitIndex;
            if (value.Mode == AttackPresentationImpactMode.Scoped)
                _attackOccurrenceLease.Bind(value, raw - AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds,
                    baseline, out skipped);
            else _attackOccurrenceLease.Close();
            _nativeOccurrenceContext = new NativeOccurrenceContext {
                Owner = value, Clip = _attackPhaseClip, Lease = _attackOccurrenceLease,
                Snapshot = CaptureAttackPresentationReplicatedSnapshot(), Target = _combatTargetTransform,
                TargetPosition = GetTargetPresentationWorldPos(value.TargetId, value.TargetIsUnit),
                Rotation = Quaternion.LookRotation(PresentationTransform.forward)
            };
            ObserveNativeLease("bind-after", value.Scope, raw);
            PistoleerTimingDiagnostics.Event(_unitData, "PhaseBind", value.Scope, raw,
                $"epoch={value.EpochId} epochTime={PistoleerTimingDiagnostics.Number(value.EpochServerTime)} generation={generation} presentationRev={value.PresentationRevision} actionRev={value.ActionRevision} skipped={skipped} freshClock=1 presentationTime={PistoleerTimingDiagnostics.Number(raw - AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds)}");
            if (skipped > 0) PistoleerTimingDiagnostics.Event(_unitData, "LateSourceOccurrenceSkipped", value.Scope, raw, $"count={skipped} nextLoopReuse=0");
            PistoleerTimingDiagnostics.Event(_unitData, "Baseline", value.Scope, raw,
                $"lastConfirmed={baseline} generation={generation} skipped={skipped}");
        }

        private bool TryRetainContinuousNativeOwner()
        {
            var old = _nativeOccurrenceContext;
            if (!_attackPhaseEntryTransaction || old == null || _attackPhaseFirstEntry
                || !UnitAttackPresentationPolicy.CanRetainNativeOccurrence(old.Owner, _attackPhase, old.Clip == _attackPhaseClip)) return false;
            // ID equality alone must not transfer ownership to a recycled target object.
            // Null/null is the existing missing-view path; no Transform is dereferenced by an old emitter.
            if (!ReferenceEquals(old.Target, GetTargetTransform(_attackPhase.TargetId, _attackPhase.TargetIsUnit))) return false;
            if (old.Lease.IsBound && old.Lease.Remaining > 0)
            {
                if (_pendingNativeOwners.Count == PendingNativeOwnerLimit)
                {
                    ObserveNativeRoute(old, "overflow", -1, float.NaN, false);
                    return false; // 기존 semantic close로 전체 허가를 닫는다. 조용히 교체하지 않는다.
                }
                _pendingNativeOwners.Add(old);
                ObserveNativeRoute(old, "handoff", -1, float.NaN, true);
            }
            _attackOccurrenceLease = new AttackSourceOccurrenceLease();
            return true;
        }

        private void ClosePendingNativeOwners(string reason, bool sourceOnly)
        {
            foreach (var owner in _pendingNativeOwners)
            {
                if (sourceOnly) owner.Lease.PreserveSourceOnly();
                else owner.Lease.Close();
                ObserveNativeRoute(owner, sourceOnly ? "source-only" : "cancel", -1, float.NaN, false, reason);
                if (NetworkContext.IsNetworkActive && _unitData != null)
                    GameEvents.OnLocalAttackPresentationScopeRetired.OnNext(
                        new LocalAttackPresentationScopeRetiredEvent(_unitData.Id, owner.Owner.Scope));
            }
            if (!sourceOnly) _pendingNativeOwners.Clear();
        }

        private bool TryConsumeNativeOwner(long loop, float marker, out AttackPresentationScope scope,
            out NativeOccurrenceContext selected, out string reason)
        {
            scope = default; selected = null; reason = "wrong-absolute-occurrence";
            if (_nativeOccurrenceContext != null && _attackOccurrenceLease.MatchesOccurrence(loop, marker))
                selected = _nativeOccurrenceContext;
            foreach (var owner in _pendingNativeOwners)
            {
                if (owner.Clip != _attackPhaseClip || owner.Owner.EpochId != _nativeAppliedEpoch
                    || owner.Owner.Lifetime != _nativeAppliedLifetime || !owner.Lease.MatchesOccurrence(loop, marker)) continue;
                // Consumed and baseline-skipped current identities also collide: never guess an owner.
                if (selected != null)
                {
                    reason = "ambiguous-owner";
                    ObserveNativeRoute(owner, "rejected", loop, marker, true, reason);
                    return false;
                }
                selected = owner;
            }
            bool accepted = selected != null
                ? selected.Lease.TryConsume(loop, marker, out scope, out reason)
                : _attackOccurrenceLease.TryConsume(loop, marker, out scope, out reason);
            if (selected != null) ObserveNativeRoute(selected, accepted ? "consumed" : "rejected", loop, marker,
                selected.Lease != _attackOccurrenceLease, reason);
            // 먼저 현재 callback을 exact owner에 연결한 뒤 통과한 이전 marker만 정리한다.
            // NGO estimate가 앞섰다는 이유로 아직 native에 오지 않은 marker를 닫지 않는다.
            for (int i = _pendingNativeOwners.Count - 1; i >= 0; i--)
            {
                var owner = _pendingNativeOwners[i];
                int passed = owner.Lease.RetireBeforeOccurrence(loop, marker);
                if (passed > 0) ObserveNativeRoute(owner, "occurrence-passed", loop, marker, true);
                if (owner.Lease.Remaining == 0)
                {
                    // 통과한 미소모 marker는 source 표현 종료만 알린다. 이미 확정된 Result는 폐기하지 않는다.
                    if (passed > 0 && NetworkContext.IsNetworkActive && _unitData != null)
                        GameEvents.OnLocalAttackPresentationScopeRetired.OnNext(
                            new LocalAttackPresentationScopeRetiredEvent(_unitData.Id, owner.Owner.Scope));
                    // 소비한 owner는 emitter가 SourceOnly를 읽을 때까지 불변 문맥을 보존한다.
                    // list에서는 제거돼 다음 callback 권한은 없고, 지역 참조가 끝나면 회수된다.
                    // A successful source is NOT semantic retirement: that signal would cancel
                    // a still-pending exact Result/Tracer rendezvous. Only passed/cancelled sources send it.
                    if (!accepted || owner != selected) owner.Lease.Close();
                    _pendingNativeOwners.RemoveAt(i);
                }
            }
            return accepted;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveNativeRoute(NativeOccurrenceContext owner, string boundary, long loop,
            float marker, bool pending, string reason = "none")
        {
            PistoleerTimingDiagnostics.Event(_unitData, "NativeRoute", owner.Owner.Scope, _attackPhaseClockHighWater,
                PistoleerTimingDiagnostics.NativeRouteDetail(owner.Owner, boundary, reason, loop, marker,
                    pending, _pendingNativeOwners.Count, owner.Lease.Remaining, owner.Lease.SourceOnly,
                    _attackPhase.Scope));
        }

        // Temporary diagnostic evidence, retained until the existing timing task closes.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveNativeLease(string boundary, AttackPresentationScope context, double clock,
            long callbackLoop = -1, float callbackMarker = float.NaN)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_unitData == null || !PistoleerTimingDiagnostics.Observes(_unitData.Type)) return;
            if (!context.IsValid)
            {
                PistoleerTimingDiagnostics.NativeLeaseUnavailable(_unitData, clock, boundary);
                return;
            }
            var owner = _attackOccurrenceLease.DiagnosticOwner;
            string observation = $"{boundary}:{owner.Scope.AttackerInstanceId.Value}:{owner.Scope.SequenceId.Value}:{_attackOccurrenceLease.Remaining}";
            int count = _attackOccurrenceLease.DiagnosticHitCount;
            PistoleerTimingDiagnostics.Event(_unitData, "NativeLease", context, clock,
                PistoleerTimingDiagnostics.NativeLeaseDetail(observation, boundary,
                    boundary.StartsWith("bind-", StringComparison.Ordinal) ? "bind-raw" : "phase-high-water",
                    owner, _attackOccurrenceLease.IsBound, _attackOccurrenceLease.SourceOnly, _attackOccurrenceLease.Remaining, count));
            for (int i = 0; i < Math.Max(1, count); i++)
            {
                bool found = _attackOccurrenceLease.ReadDiagnosticHit(i, out var hit, out bool consumed);
                PistoleerTimingDiagnostics.Event(_unitData, "NativeLeaseHit", context, clock,
                    PistoleerTimingDiagnostics.NativeLeaseHitDetail(observation + ":" + i, found, hit, consumed, callbackLoop, callbackMarker));
            }
#endif
        }

        private void AlignFirstNativeAttackEpoch(double raw)
        {
            double elapsed = raw - AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds - _attackPhase.EpochServerTime;
            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0, (float)Math.Max(0d, elapsed));
            _currentAnimStateHash = StateAttack;
            if (elapsed < 0) { _attackPhaseSavedSpeed = 1; _attackPhaseOwnsSpeed = true; _animator.speed = 0; }
            PistoleerTimingDiagnostics.Event(_unitData, "NativeEpochApply", _attackPhase.Scope, raw,
                $"epoch={_attackPhase.EpochId} generation={_attackPhase.Lifetime} epochTime={PistoleerTimingDiagnostics.Number(_attackPhase.EpochServerTime)} offset={PistoleerTimingDiagnostics.Number(Math.Max(0d, elapsed))} first=1");
        }

        public void CloseAttackPhasePresentation(string reason, bool preserveSourceOnly = false)
        {
            bool keepEpoch = UnitAttackPresentationPolicy.KeepsNativeEpoch(reason, false)
                && _attackPhase.Active && _unitData != null && _unitData.IsAlive
                && !_isFrozenAnim && !_isMovementHeldAnim;
            PistoleerTimingDiagnostics.Event(_unitData, "NativeEpochClose", _attackPhase.Scope,
                UnitAttackResultPresentationShadowBridge.CurrentServerTime,
                $"reason={reason} epoch={_attackPhase.EpochId} generation={_attackPhase.Lifetime} remaining={_attackOccurrenceLease.Remaining} keep={(keepEpoch ? 1 : 0)}");
            // 정상 close만 보존할 수 있고, 실제 승인된 미소비 occurrence가 있어야 한다.
            // death/held/game-end/invalid scope에 count-only lease를 새로 만들지 않는다.
            preserveSourceOnly &= _attackPhase.Active && _attackPhase.Mode == AttackPresentationImpactMode.Scoped
                && _attackOccurrenceLease.IsBound && _attackOccurrenceLease.Remaining > 0
                && _unitData != null && _unitData.IsAlive && !_isFrozenAnim && !_isMovementHeldAnim;
            if (preserveSourceOnly) PreserveCommittedSourceMarkerAndCloseTargetImpact();
            else RetireAndCloseAttackPresentationScope(reason);
            // SourceOnly continues the already-approved native occurrence on the same epoch.
            // OLD: an empty normal scope erased a still-continuous native phase.
            // if (!preserveSourceOnly || _attackOccurrenceLease.Remaining == 0) _attackPhase = default;
            if (!keepEpoch)
            {
                _attackPhaseNetworkUnit?.InvalidateNativeAttackEpoch(reason);
                _attackPhase = default;
                _nativeAppliedEpoch = _nativeAppliedLifetime = 0;
                if (_attackPhaseOwnsSpeed && !_isFrozenAnim && !_isMovementHeldAnim && _unitData != null && _unitData.IsAlive)
                    if (_animator != null) _animator.speed = _attackPhaseSavedSpeed;
                _attackPhaseOwnsSpeed = false;
            }
            else if (!preserveSourceOnly)
            {
                _attackPhase.Mode = AttackPresentationImpactMode.Suppressed;
                _attackPhase.Scope = default; _attackPhase.Hits = null;
            }
        }

        private void FollowAttackPhase(double dt)
        {
            if (!_attackPhase.Active || _animator == null || _unitData == null || !_unitData.IsAlive
                || _isFrozenAnim || _isMovementHeldAnim) return;
            if (_currentAnimStateHash != StateAttack) { CloseAttackPhasePresentation("native-state-exit"); return; }
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (_animator.IsInTransition(0))
            {
                var next = _animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == StateAttack) state = next;
            }
            if (state.shortNameHash != StateAttack) return;
            if (!_attackPhaseOwnsSpeed) { _attackPhaseSavedSpeed = _animator.speed; _attackPhaseOwnsSpeed = true; }
            double raw = double.NaN; ulong generation = 0;
            if (_attackPhaseNetworkUnit == null || !_attackPhaseNetworkUnit.TryReadAttackPresentationClock(out raw, out generation)
                || generation != _attackPhase.Lifetime) { ObserveNativePhaseHold("clock-not-ready", raw, state, dt); _animator.speed = 0; return; }
            if (raw < _attackPhaseClockHighWater)
            {
                if (!_attackPhaseClockRegressed) PistoleerTimingDiagnostics.Event(_unitData, "PhaseClockRegression", _attackPhase.Scope, raw,
                    $"episode={_attackPhaseHoldEpisode + 1} epoch={_attackPhase.EpochId} generation={generation} previousRaw={PistoleerTimingDiagnostics.Number(_attackPhaseClockHighWater)} hold=1");
                ObserveNativePhaseHold("clock-regression", raw, state, dt);
                _attackPhaseClockRegressed = true; _animator.speed = 0; return;
            }
            _attackPhaseClockRegressed = false; _attackPhaseClockHighWater = raw;
            double actual = state.normalizedTime * _attackPhase.Duration;
            double target = raw - AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds - _attackPhase.EpochServerTime;
            double stateRate = state.speed * state.speedMultiplier;
            double advance = UnitAttackPresentationPolicy.FollowPhaseAdvance(target, actual, dt);
            _animator.speed = !double.IsNaN(stateRate) && !double.IsInfinity(stateRate)
                && stateRate > 0 && dt > 0 ? (float)(advance / dt / stateRate) : 0f;
            ObserveNativePhaseHold(_animator.speed == 0 ? "phase-ahead-or-rate" : null, raw, state, dt);
        }

        private void ObserveNativePhaseHold(string reason, double raw, AnimatorStateInfo state, double dt)
        {
            if (reason == _attackPhaseHoldReason) return;
            if (_attackPhaseHoldReason != null)
                PistoleerTimingDiagnostics.Event(_unitData, "PhaseResume", _attackPhase.Scope, raw,
                    $"episode={_attackPhaseHoldEpisode} epoch={_attackPhase.EpochId} reason={_attackPhaseHoldReason} actual={PistoleerTimingDiagnostics.Number(state.normalizedTime * _attackPhase.Duration)} target={PistoleerTimingDiagnostics.Number(raw - AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds - _attackPhase.EpochServerTime)} dt={PistoleerTimingDiagnostics.Number(dt)}");
            _attackPhaseHoldReason = reason;
            if (reason == null) return;
            ++_attackPhaseHoldEpisode;
            PistoleerTimingDiagnostics.Event(_unitData, "PhaseHold", _attackPhase.Scope, raw,
                $"episode={_attackPhaseHoldEpisode} epoch={_attackPhase.EpochId} reason={reason} actual={PistoleerTimingDiagnostics.Number(state.normalizedTime * _attackPhase.Duration)} target={PistoleerTimingDiagnostics.Number(raw - AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds - _attackPhase.EpochServerTime)} dt={PistoleerTimingDiagnostics.Number(dt)} rate={PistoleerTimingDiagnostics.Number(state.speed * state.speedMultiplier)}");
        }

        private void FixedUpdate()
        {
            if (_animator != null && _animator.updateMode == AnimatorUpdateMode.Fixed)
                FollowAttackPhase(Time.fixedDeltaTime);
        }

        public void OnAttackHit(AnimationEvent marker)
        {
            // [2026-10-26 수정안 (a)] 사망 표현이 미뤄진 유닛(사망 대역의 Animator가 아직 공격 클립을 재생 중)의 marker는 무시한다.
            //   이미 죽은 유닛이라 공격 표현(총구·발사음·차지)을 새로 내면 안 된다. 대역이 남아 있는 최대 0.3초 동안만 해당한다.
            if (_deathDeferralOwned) return;
            _nativeAttackMarker = marker;
            try { OnAttackHit(); }
            finally { _nativeAttackMarker = null; }
        }

        // [P4 — 2026-10-19 차지 유닛 발사 시점] 옛 요약(Fox 전용 기준) — 기록용으로 보존:
        //   "Fox의 1초 charge는 기존 source 허가로 VFX/SFX만 재생한다.
        //    피해 marker나 recipient 신호로 중계하지 않고 콜백 문맥은 종료 시 반드시 비운다."
        /// <summary>
        /// [P2-2] 「차지 시작」 Animation Event(OnAttackCharge) 입구 — 차지 유닛 공통.
        /// 공격 클립에 OnAttackCharge 이벤트가 찍힌 유닛(현재 FoxMagician 1.0초, InfernoSpirit 0.1667초)만
        /// 받아들이고, 기 모으기(charge) VFX 하나만 켠다. 공격 SFX는 내지 않는다(발사 순간인 OnAttackHit에서 낸다).
        /// 멀티에서는 공격 1회분 허가표(lease)를 「확인만」 하고 쓰지 않는다 — 쓰는 것은 타격(OnAttackHit)이다.
        /// 피해 marker나 recipient(피격 표현) 신호로 중계하지 않고, 콜백 문맥은 종료 시 반드시 비운다.
        /// </summary>
        public void OnAttackCharge(AnimationEvent marker)
        {
            // [2026-10-26 수정안 (a)] 사망 표현이 미뤄진 유닛은 차지를 새로 켜지 않는다(위 OnAttackHit과 같은 이유).
            if (_deathDeferralOwned) return;
            // 차지 유닛 판별은 유닛 「타입 이름」이 아니라 「공격 클립에 OnAttackCharge 이벤트가 있는가」다(_hasAttackChargeMarker, P2-1).
            //   지금 그 이벤트가 있는 클립은 FoxMagician · InfernoSpirit 두 개라 두 유닛이 같은 길을 쓴다.
            EnsureAttackChargeMarkerCache();
            if (_unitData == null || !_hasAttackChargeMarker) return;
            _nativeAttackMarker = marker;
            try { ProcessAttackMarker(chargeOnly: true); }
            finally { _nativeAttackMarker = null; }
        }

        // ────────────────────────────────────────────────────────────────
        // [P2-1 — 2026-10-19 차지 유닛 발사 시점] 「차지 유닛」 판별 캐시
        //
        //   차지 유닛 = 공격 클립(이름이 "_Attack"으로 끝나는 클립)에 OnAttackCharge 이벤트가 찍힌 유닛.
        //   유닛 타입 목록을 코드에 적지 않고 애니메이션 이벤트 자체를 기준으로 삼는다(규칙 17 — 타이밍의 단일 출처는 클립 이벤트).
        //   그래서 새 차지 유닛은 클립에 이벤트만 넣으면 된다. 반대로 이벤트를 빠뜨리면 일반 유닛처럼 동작하므로
        //   자동 검증(RunUnitActionSelfValidation T-4)이 「차지 이벤트가 있는 유닛 = FoxMagician·InfernoSpirit」을 고정한다.
        //
        //   왜 캐시하나: AnimationClip.events는 읽을 때마다 배열을 새로 만든다(메모리 할당).
        //   Animation Event는 공격마다 오므로 그때마다 읽지 않고, Initialize에서 한 번만 계산해 bool로 보관한다.
        //   Initialize 시점에 Animator가 아직 없으면 "미확정"으로 두고, 첫 Animation Event가 올 때(=Animator가 있을 때) 한 번 계산한다.
        // ────────────────────────────────────────────────────────────────
        private bool _hasAttackChargeMarker;
        private bool _attackChargeMarkerCacheResolved;
        // [P2-3] 「같은 공격 회차에서 차지는 한 번만」 표시. 차지를 실제로 허락한 회차(presentation revision)를 기억한다.
        //   공격 클립은 루프하므로, 허가표가 아직 소비되지 않은 채 다음 루프의 차지 신호가 오면 같은 회차로 한 번 더 통과할 수 있다.
        //   0 = 아직 아무 회차에서도 차지를 내지 않음(서버 revision은 1부터 시작한다 — TryAcceptRevision이 0을 거부).
        private ulong _lastChargePresentedRevision;

        /// <summary>
        /// [P2-1] 차지 유닛 판별 캐시를 아직 계산하지 않았으면 지금 계산한다.
        /// Animator나 컨트롤러가 아직 없으면 아무 값도 바꾸지 않고 미확정으로 둔다(다음 호출에서 다시 시도).
        /// 공격 클립 선택 규칙은 CanUseNativeAttackPhase(809~814행 부근)와 같다:
        /// 이름이 "_Attack"으로 끝나는 클립이 정확히 하나여야 하고, 서로 다른 클립이 둘 이상이면 판별 불가로 보고 false로 둔다.
        /// (CanUseNativeAttackPhase 자체는 FoxMagician을 제외하는 조건 안에 있어서 재사용하지 않는다.)
        /// </summary>
        private void EnsureAttackChargeMarkerCache()
        {
            if (_attackChargeMarkerCacheResolved) return;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            AnimationClip found = null;
            bool ambiguous = false;
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || !clip.name.EndsWith("_Attack", StringComparison.Ordinal)) continue;
                if (found != null && found != clip) { ambiguous = true; break; }
                found = clip;
            }
            bool hasCharge = false;
            if (!ambiguous && found != null)
            {
                // 여기서만 events 배열을 만든다(Initialize 또는 첫 marker에서 유닛 수명당 1회).
                foreach (var clipEvent in found.events)
                    if (clipEvent.functionName == nameof(OnAttackCharge)) { hasCharge = true; break; }
            }
            _hasAttackChargeMarker = hasCharge;
            _attackChargeMarkerCacheResolved = true;
        }

        /// <summary>
        /// [P2-3] 멀티 차지 신호의 「확인 전용」 허가 판정. 허가표(lease)를 하나도 소비하지 않는다.
        /// 모두 참일 때만 차지 VFX를 낸다:
        ///   ① source lease가 장전(Armed)돼 있고 남은 횟수가 있다,
        ///   ② 일반 Stop이 먼저 와서 SourceOnly로 바뀌지 않았다(취소가 이미 도착했으면 차지를 시작하지 않는다 — 사용자 결정 D2),
        ///   ③ lease가 지금 수락된 공격 회차(_lastAttackPresentationRevision)의 것이다,
        ///   ④ 이 회차에서 아직 차지를 낸 적이 없다(회차당 1회).
        /// </summary>
        private bool CanPresentAttackChargeWithoutConsuming()
        {
            ulong revision = _attackPresentationSourceMarkerLease.PresentationRevision;
            return _attackPresentationSourceMarkerLease.IsArmed
                && _attackPresentationSourceMarkerLease.RemainingMarkerCount > 0
                && !_attackPresentationSourceMarkerLease.IsSourceOnly
                && revision != 0UL
                && revision == _lastAttackPresentationRevision
                && revision != _lastChargePresentedRevision;
        }

        /// <summary>
        /// [P2-6 — 사용자 결정 D2] 공격이 「진짜로 끊긴」 경계에서 이 유닛이 켜 둔 차지 VFX를 즉시 끈다.
        /// 차지 유닛일 때만 동작한다. 정지 대상은 이 유닛이 소유한 attack 프리셋 이펙트(Stop When Caster Dies가 켜진 charge)뿐이라,
        /// 이미 손을 떠나 날아가는 투사체(트레이서)·착탄·화상 이펙트는 끊기지 않는다.
        /// 화면 표현 전용이다 — lease·피해·서버 결과는 바꾸지 않는다(NET-AUTH-001 · NET-CANCEL-005).
        /// </summary>
        /// <param name="reason">정지 사유(진단용 짧은 토큰).</param>
        private void StopOwnedChargeEffects(string reason)
        {
            if (!_hasAttackChargeMarker) return;
            EffectManager.Instance?.StopCasterOwnedAttackEffects(this, reason);
        }

        /// <summary>
        /// [2026-10-21 서버 권위 투사체 단계 D] 서버가 이 유닛의 발사 예약을 "발사 전에" 취소했다는 통보를 화면에 적용한다.
        /// 차지 유닛이면 켜져 있던 차지 VFX를 즉시 끄고(투사체·발사음은 처음부터 없다 — 서버 발사 기록이 없으므로),
        /// 멀티 Client에서는 같은 회차의 차지 신호가 늦게 오더라도 차지를 다시 켜지 않도록 그 회차를 "차지 끝남"으로 표시한다.
        ///
        /// [언제 불리는가 — 유니티 초급자용]
        ///   투사체 화면 소비자(ProjectilePresentationPresenter)가 UseCase의 ProjectileLaunchCancelled를 받아 부른다.
        ///   · 싱글·Host: 서버 UseCase가 취소한 바로 그 순간(같은 프레임).
        ///   · Client: 서버 취소 통보 RPC를 받은 순간(그 RPC는 같은 연결에서 다음 공격 시작 RPC보다 먼저 온다).
        ///
        /// [왜 기존 Stop/사망 경로만으로는 부족한가]
        ///   공격자 사망은 기존 시전자 귀속 정지(Stop When Caster Dies)가, 전투 정지는 StopCombat → SourceOnly 경로가 이미 끈다.
        ///   그러나 "차지 중 대상 사망 + 사거리 안에 다른 적이 남음"이면 서버는 쿨다운 동안 타겟도 바꾸지 않고 정지 통보도 보내지 않는다.
        ///   싱글은 lease 자체가 없다. 그래서 서버가 확정한 취소 사실을 직접 받아 끈다(판정이 아니라 서버 권위 사실의 표현).
        ///
        /// 화면 표현 전용 — lease 소비·피해·HP·서버 결과를 바꾸지 않는다(NET-AUTH-001).
        /// </summary>
        /// <param name="presentationRevision">서버 공격 표현 회차(0 = 같은 프로세스 통보 — 회차 비교 없이 적용).</param>
        /// <returns>적용 결과(진단 계수용).</returns>
        public ServerProjectileChargeCancelResult ApplyServerProjectileLaunchCancel(ulong presentationRevision)
        {
            if (_unitData == null || !_unitData.IsAlive) return ServerProjectileChargeCancelResult.Unavailable;
            EnsureAttackChargeMarkerCache();
            if (!_hasAttackChargeMarker) return ServerProjectileChargeCancelResult.NotChargeUnit;
            if (NetworkContext.IsNetworkActive && presentationRevision != 0UL)
            {
                // 더 새 회차(다음 공격)의 차지는 끄지 않는다. 같은 회차일 때만 끄고 그 회차의 남은 차지 신호도 막는다
                //   (CanPresentAttackChargeWithoutConsuming의 ④ "회차당 1회" 칸을 그대로 쓴다 — lease는 소비하지 않는다).
                if (presentationRevision != _lastAttackPresentationRevision)
                    return ServerProjectileChargeCancelResult.StaleRevision;
                _lastChargePresentedRevision = presentationRevision;
            }
            EffectManager effects = EffectManager.Instance;
            int stopped = effects != null ? effects.StopCasterOwnedAttackEffects(this, "AttackCancel-ServerLaunchCancelled") : 0;
            return stopped > 0 ? ServerProjectileChargeCancelResult.Stopped : ServerProjectileChargeCancelResult.NothingActive;
        }

        /// <summary>
        /// [2026-10-21 단계 D] 서버 권위가 있는 화면(싱글·Host)에서 "이번 공격의 발사가 이미 취소됐는가"를 묻는다.
        /// true면 차지 신호(OnAttackCharge)에서 차지 VFX를 켜지 않는다 — 취소된 공격의 차지가 뒤늦게 시작되지 않게.
        /// 순수 Client는 서버 예약을 모르므로 false(대신 취소 통보 + 회차 규칙이 막는다). 투사체가 아닌 유닛도 false(발사 예약이 없다).
        /// </summary>
        private bool IsServerProjectileChargeCancelled()
            => (!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer)
               && _combatUseCase != null && _unitData != null
               && _combatUseCase.IsChargePresentationCancelled(_unitData);
        private AttackPresentationImpactMode _attackPresentationImpactMode;
        private ulong _lastAttackPresentationRevision;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // InfernoSpirit 방향 진단은 Attack 전환/marker/종료 상태 변화에서만 읽는다.
        // gameplay 또는 Animator 상태를 쓰지 않으며 시작 pose는 Animator 평가 뒤 LateUpdate에서 소비한다.
        private bool _infernoAttackStartFacingPending;
        private int _infernoFacingTargetId = -1;
        private bool _infernoFacingTargetIsUnit;
        private ulong _infernoFacingPresentationRevision;
        private AttackPresentationScope _infernoFacingScope;

        // 순간이동 진단은 매 프레임 값을 "기억"하지만 정상 프레임 로그는 절대 출력하지 않는다.
        // 실제 화면 메시의 기준점으로 금지된 Humanoid 몸 중심 API를 사용하지 않는다. 그 API를 일반
        // LateUpdate에서 읽으면 Unity가 매 프레임 경고를 만들고, 계산된 몸 중심일 뿐 실제 renderer
        // 계약도 아니다. 그래서 초기화 때 InfernoSpirit의 유일한 SkinnedMeshRenderer와 rootBone을 한 번만
        // 찾아 캐시하고, 이후 프레임에는 캐시된 Transform.position만 읽는다.
        // 직전 표본은 이 UnitView 인스턴스 안에만 보관하고, disable/despawn 수명 경계에서 폐기한다.
        // static serial은 재사용된 unitId가 이전 생명주기의 exact key와 섞이지 않게 하는 진단 ID다.
        private static ulong _nextInfernoMotionLifecycle;
        private readonly List<SkinnedMeshRenderer> _infernoRenderedAnchorSearch =
            new List<SkinnedMeshRenderer>(2);
        private ulong _infernoMotionLifecycle;
        private SkinnedMeshRenderer _infernoRenderedAnchorRenderer;
        private Transform _infernoRenderedAnchorRootBone;
        private string _infernoRenderedAnchorCacheReason = "not-initialized";
        private bool _hasInfernoMotionPrevious;
        private int _infernoMotionPreviousFrame;
        private double _infernoMotionPreviousTime;
        private Vector3 _infernoMotionPreviousSimulation;
        private Quaternion _infernoMotionPreviousSimulationRotation;
        private bool _infernoMotionPreviousVisualAvailable;
        private Vector3 _infernoMotionPreviousVisual;
        private Quaternion _infernoMotionPreviousVisualRotation;
        private bool _infernoMotionPreviousRenderedAnchorAvailable;
        private Vector3 _infernoMotionPreviousRenderedAnchor;
        private bool _infernoMotionPreviousRendererBoundsAvailable;
        private Vector3 _infernoMotionPreviousRendererBounds;
#endif
        // 이동 후보 commit callback이 동기 전투 이벤트의 실제 완료 여부를 확인하는 ACK다.
        // revision과 별도인 이유는 전용 서버에서도 Host Animator 적용 없이 RPC enqueue 완료를
        // 확인해야 하기 때문이다. 값은 서버 로컬에서만 증가하며 네트워크 gameplay 상태가 아니다.
        private ulong _serverAttackEntryHandoffSerial;
        // Simulation Root 회전은 이동 또는 행동(공격/힐) 중 정확히 한 writer만 소유한다.
        // bool 두 개 대신 순수 Application 소유권 객체 하나를 사용해 이중 소유 상태를 막는다.
        private readonly UnitRootRotationOwnership _rootRotationOwnership =
            new UnitRootRotationOwnership();

        // ────────────────────────────────────────────────────────────────────
        // 전투 추격(EnterCombatPursuitV3 실행 중) 여부 플래그.
        //
        // 왜 필요한가:
        //   IsInCombat()는 _combatTargetTransform != null만 판정합니다.
        //   _combatTargetTransform은 서버가 전투 정렬 타겟을 확정한 시점부터 set되므로,
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
        // Chase spatial route가 회복 가능하게 무효화되면 fatal cleanup으로 닫지 않고,
        // 한 frame held 후 기존 A*/PostCombat 재탐색 경계로 반환한다.
        private bool _combatPursuitRequiresRepath;
        // 건물 생성·파괴 중에도 Chase 코루틴의 단일 writer는 유지하되, 현재 타겟 접근
        // 경로만 다음 frame에 다시 계산하도록 표시한다. OnPathInvalidated가 전투 코루틴을
        // 교체하면 두 writer가 생길 수 있으므로 bool 신호만 전달한다.
        private bool _combatPursuitPathInvalidated;

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

        // 서버 Root 선분을 타일 전이로 바꾸는 재사용 버퍼다. 프레임마다 배열/List를 만들지 않는다.
        // 한 프레임 이동이 이 상한을 넘으면 부분 적용하지 않고 권위 frame 전체를 거부한다.
        private const int SpatialSampleCapacity = 64;
        private readonly List<HexCoord> _spatialTileSamples =
            new List<HexCoord>(SpatialSampleCapacity);
        private readonly HexCoord[] _spatialTransitionBuffer =
            new HexCoord[SpatialSampleCapacity];
        private readonly List<HexCoord> _spatialProbeTileSamples =
            new List<HexCoord>(SpatialSampleCapacity);
        private readonly HexCoord[] _spatialProbeTransitionBuffer =
            new HexCoord[SpatialSampleCapacity];

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
        /// [2026-10-20 서버 권위 투사체] 이 유닛이 쏘는 투사체가 화면에서 출발하는 지점(화면 좌표, 읽기 전용).
        /// 총구 기준점(VfxSpawnPoint)이 연결돼 있으면 그 위치, 없으면 화면 Root 위치다 —
        /// 기존 로컬 트레이서(OnAttackHit 원거리 분기)가 쓰던 출발점과 같은 규칙이다.
        /// 투사체 화면 소비자(ProjectilePresentationPresenter)만 읽으며 Simulation Root를 바꾸지 않는다.
        /// </summary>
        public Vector3 ProjectileLaunchPoint => _vfxSpawnPoint != null
            ? _vfxSpawnPoint.position
            : PresentationTransform.position;

        /// <summary>
        /// [2026-10-20 단계 B — 의심 1 수정] 서버 투사체의 "발사 신호"(총구 VFX + 발사음)를 지금 1회 낸다.
        ///
        /// [언제 불리는가 — 유니티 초급자용]
        ///   투사체 화면 소비자(ProjectilePresentationPresenter)가 서버 발사 기록을 화면에 꺼내는 순간(Begin)에만 부른다.
        ///   싱글·Host는 서버 발사 순간과 같은 프레임, Client는 투사체와 같은 0.10초 화면 지연 뒤다.
        ///   서버가 발사를 취소한 공격에는 발사 기록 자체가 없으므로 이 메서드가 불리지 않는다 → 총구 VFX·소리 없음(Q6).
        ///
        /// [무엇을 내는가 — 예전 marker 자리와 같은 규칙]
        ///   · 총구 VFX: 비차지 유닛만. 위치 = 총구 기준점(VfxSpawnPoint, 없으면 화면 Root), 회전 = 화면 Root 정면.
        ///     (차지 유닛은 차지 VFX가 발사 순간 스스로 끝나도록 맞춰져 있어 발사 때 공격 VFX를 다시 켜지 않는다 — P2-4.)
        ///   · 발사음: 모든 서버 투사체 유닛(규칙 15 — 차지 유닛도 "발사음은 발사 순간").
        /// HP·피해·타겟·Simulation Root를 읽거나 쓰지 않는다(표현 전용).
        /// </summary>
        /// <returns>총구 VFX 재생 결과(차지 유닛이거나 EffectManager가 없으면 기본값).</returns>
        public UnitAttackVfxPlaybackResult PlayServerProjectileLaunchCue()
        {
            // 기본값(Status=Started)이 "재생됨"으로 읽히지 않도록, 재생하지 않은 경우의 값을 먼저 정해 둔다(marker 자리와 같은 대체값).
            var vfxResult = new UnitAttackVfxPlaybackResult(UnitAttackVfxPlaybackStatus.PoolItemUnavailable, 0, false);
            if (_unitData == null) return vfxResult;
            EffectManager effectManager = EffectManager.Instance;
            Transform presentationTransform = PresentationTransform;
            if (!_hasAttackChargeMarker && effectManager != null)
            {
                Vector3 spawnPos = _vfxSpawnPoint != null ? _vfxSpawnPoint.position : presentationTransform.position;
                Quaternion spawnRot = Quaternion.LookRotation(presentationTransform.forward);
                // 마지막 인자 this = 시전자 귀속(시전자 사망 시 멈춤 표시가 있는 프리셋만 사용 — R2-1과 같은 규칙).
                vfxResult = effectManager.PlayUnitAttack(_unitData.Type, spawnPos, spawnRot, this);
            }
            AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);                // SFX (규칙 15 — 발사 VFX와 짝)
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // StreamSpirit 시간축 진단의 VFX 단계도 실제 총구 VFX를 낸 이 자리에서 기록한다(예전 marker 자리에서 옮김).
            //   같은 공격 회차(_lastAttackPresentationRevision)에 붙는다 — 다음 공격 커밋은 쿨다운(1.5초) 뒤라 회차가 섞이지 않는다.
            if (_unitData.Type == UnitType.StreamSpirit)
            {
                string streamOutcome = effectManager == null
                    ? "manager-unavailable"
                    : vfxResult.Status == UnitAttackVfxPlaybackStatus.Started ? "started" : vfxResult.Status.ToString();
                UnitAttackShadowObserver.RecordStreamSpiritTimelineVfx(
                    _unitData.Id,
                    _unitData.Type,
                    _lastAttackPresentationRevision,
                    _attackPresentationImpactMode,
                    streamOutcome,
                    vfxResult.ParticleSystemCount,
                    vfxResult.PlaybackActive);
            }
#endif
            return vfxResult;
        }

        /// <summary>[진단·검증용] 이 유닛의 공격 클립에 차지 marker가 있는가(있으면 발사 신호에서 총구 VFX를 내지 않는다).</summary>
        public bool HasAttackChargeMarker => _hasAttackChargeMarker;

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
            if (_attackPhaseNetworkUnit == null) _attackPhaseNetworkUnit = GetComponent<NetworkUnit>();
            _attackPhaseNetworkUnit?.RetryAttackPhaseViewBinding();
            if (_animator != null && _animator.updateMode != AnimatorUpdateMode.Fixed)
                FollowAttackPhase(_animator.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
            if (_unitData != null && _unitData.IsHealer && _healPresentation.Current.Active)
                MaintainHealPresentation(HealNow);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            ObserveClientAttackEntryPresentationFrame();
#endif
            // 힐 target은 적 공격 scope와 독립이다. 서버 Action 회전 계산만 재사용한다.
            if (_unitData != null && _unitData.IsHealer)
            {
                UpdateHealFacing(Time.deltaTime);
                return;
            }
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
            if (_rootRotationOwnership.MovementOwnsRoot)
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
            RotateActionTowards(_combatTargetTransform.position, Time.deltaTime);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private ulong _healProbeSequence;
        private ulong _healProbeLifetime;
        private float _healProbeStartYaw;
        private float _healProbeBeforeYaw;
        private int _healProbeCount;
        private int _healProbeTransitions;
        private string _healProbeReason;
        private bool _healProbeConverged;
        private bool _healProbeDrifted;
        private string _healProbeBaselineStage;
        private double _healProbeNextSample;
#endif

        private void RotateActionTowards(Vector3 targetPosition, float deltaTime)
        {
            float angle = CalculateAttackAngle(targetPosition);
            Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * deltaTime
            );
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void LateUpdate()
        {
            ObservePistoleerRenderedStart();
            ConsumePendingInfernoAttackStartFacing();
            ObserveInfernoMotionJumpFrame();
            ObserveBlockedSpawnFacingFrame();
        }

#endif

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // 원인을 고치거나 FAIL을 판정하지 않는 원시 관측이다. 이 객체는 선택된 한 유닛에만 만든다.
        private sealed class BlockedSpawnFacingTrace
        {
            public int UnitId, BarracksId, Attempts, Samples, Rows;
            public TeamId Team;
            public double StartedAt, NextSampleAt;
            public Vector3 PreviousPosition;
            public Transform Mesh, Hips;
        }

        private BlockedSpawnFacingTrace _blockedSpawnFacingTrace;
        private const int SpawnFacingMaximumSamples = 16;
        private const int SpawnFacingMaximumRows = 18;
        private const double SpawnFacingDuration = 4d;
        private const double SpawnFacingSampleInterval = 0.25d;

        /// <summary>
        /// 막힘 해제 생산과 연결된 싱글 Pistoleer의 방향을 최대 4초 동안 읽기만 한다.
        /// Visual/뼈 축은 실제 몸체 정면이나 같은 프레임 최종 렌더를 보증하는 값이 아니다.
        /// </summary>
        public void BeginBlockedSpawnFacingTrace(int barracksId, int attempts)
        {
            if (NetworkContext.IsNetworkActive || _unitData == null || !_unitData.IsAlive
                || _unitData.Type != UnitType.Pistoleer || _blockedSpawnFacingTrace != null) return;
            double now = Time.unscaledTimeAsDouble;
            var trace = new BlockedSpawnFacingTrace
            {
                UnitId = _unitData.Id, Team = _unitData.Team, BarracksId = barracksId,
                Attempts = attempts, StartedAt = now, NextSampleAt = now + SpawnFacingSampleInterval,
                PreviousPosition = transform.position,
                Mesh = _animator != null ? _animator.transform : null,
                Hips = _animator != null && _animator.isHuman && _animator.avatar != null
                    && _animator.avatar.isValid ? _animator.GetBoneTransform(HumanBodyBones.Hips) : null
            };
            _blockedSpawnFacingTrace = trace;
            WriteBlockedSpawnFacingRow(trace, "begin",
                System.FormattableString.Invariant($"BlockedAttempts={attempts}, X={transform.position.x:G9}, Z={transform.position.z:G9}, RootYaw={transform.eulerAngles.y:G9}, Mode={_lockedMovementPipelineMode}, AlreadyMoving={IsMoving}, VisualStage=LateUpdateOrderUnspecified, BoneAxis=Uncalibrated"));
        }

        private void ObserveBlockedSpawnFacingFrame()
        {
            var trace = _blockedSpawnFacingTrace;
            if (trace == null) return;
            Vector3 rootPosition = transform.position;
            Vector3 step = rootPosition - trace.PreviousPosition;
            step.y = 0f;
            // 4Hz 기록 사이에도 매 프레임 갱신하여 긴 구간의 평균 이동 방향과 혼동하지 않는다.
            trace.PreviousPosition = rootPosition;
            double now = Time.unscaledTimeAsDouble;
            if (NetworkContext.IsNetworkActive || _unitData == null || !_unitData.IsAlive
                || _unitData.Id != trace.UnitId)
            { EndBlockedSpawnFacingTrace("unit-unavailable"); return; }
            if (now < trace.StartedAt || now - trace.StartedAt >= SpawnFacingDuration)
            { EndBlockedSpawnFacingTrace("time-limit"); return; }
            if (now < trace.NextSampleAt) return;
            if (trace.Samples >= SpawnFacingMaximumSamples)
            { EndBlockedSpawnFacingTrace("sample-limit"); return; }
            trace.NextSampleAt = now + SpawnFacingSampleInterval;
            trace.Samples++;

            bool moving = step.sqrMagnitude > 0.0000000001f;
            float motionYaw = moving ? Mathf.Atan2(step.x, step.z) * Mathf.Rad2Deg : 0f;
            Vector3 desired = _unitActionShadowDesiredTarget - rootPosition;
            desired.y = 0f;
            bool hasTarget = _hasUnitActionShadowDesiredTarget && desired.sqrMagnitude > 0.0000000001f;
            float targetYaw = hasTarget ? Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg : 0f;
            Transform visual = _visualRootProjector != null ? _visualRootProjector.ObservedVisualRoot : null;
            Vector3 hipsForward = trace.Hips != null ? trace.Hips.forward : Vector3.zero;
            hipsForward.y = 0f;
            bool hipsAvailable = hipsForward.sqrMagnitude > 0.0000000001f;
            float hipsYaw = hipsAvailable ? Mathf.Atan2(hipsForward.x, hipsForward.z) * Mathf.Rad2Deg : 0f;
            // unavailable 플래그가 false면 해당 0 값에는 방향 의미가 없다. projector getter로 보정하지 않는다.
            WriteBlockedSpawnFacingRow(trace, "sample",
                System.FormattableString.Invariant($"Sample={trace.Samples}, X={rootPosition.x:G9}, Z={rootPosition.z:G9}, Moving={moving}, Step={step.magnitude:G9}, MotionYaw={motionYaw:G9}, Target={hasTarget}, TargetYaw={targetYaw:G9}, RootYaw={transform.eulerAngles.y:G9}, Visual={visual != null}, VisualYaw={(visual != null ? visual.eulerAngles.y : 0f):G9}, Mesh={trace.Mesh != null}, MeshYaw={(trace.Mesh != null ? trace.Mesh.eulerAngles.y : 0f):G9}, Hips={hipsAvailable}, HipsAxisYaw={hipsYaw:G9}, Owner={_rootRotationOwnership.Owner}, Intent={_movementIntentReason}, Facing={_unitData.Facing}, Anim={_currentAnimStateHash}"));
        }

        private void EndBlockedSpawnFacingTrace(string reason)
        {
            var trace = _blockedSpawnFacingTrace;
            if (trace == null) return;
            WriteBlockedSpawnFacingRow(trace, "end", $"Reason={reason}, Samples={trace.Samples}");
            _blockedSpawnFacingTrace = null;
        }

        private static void WriteBlockedSpawnFacingRow(BlockedSpawnFacingTrace trace, string stage, string detail)
        {
            if (trace.Rows >= SpawnFacingMaximumRows) return;
            string data = System.FormattableString.Invariant($"Stage={stage}, Unit={trace.UnitId}, Team={trace.Team}, Barracks={trace.BarracksId}, Frame={Time.frameCount}, Age={Time.unscaledTimeAsDouble - trace.StartedAt:G17}, {detail}");
            // GameLog 형식의 envelope 120 byte를 포함해 900 byte 미만을 유지한다.
            if (System.Text.Encoding.UTF8.GetByteCount(data) + 120 >= 900)
                data = $"Stage=format-rejected, Unit={trace.UnitId}, Frame={Time.frameCount}, Reason=utf8-budget";
            trace.Rows++;
            GameLog.Dev.Info("Combat", nameof(UnitView), "[UAS-SPAWN-FACING] pose", data);
        }
#endif

        private void OnDisable()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            EndBlockedSpawnFacingTrace("view-disabled");
#endif
            _combatUseCase?.CloseSoloAttackPlayback(_unitData, terminal: true);
            EndSoloPursuitEvidence();
            EndSoloRecoveryEvidence();
            PistoleerTimingDiagnostics.Event(_unitData, "Unavailable", _pistoleerTimingScope,
                UnitAttackResultPresentationShadowBridge.CurrentServerTime, "reason=view-disable");
            EndHealPresentation("disable", terminal: true);
            // [2026-10-13 R2-1] 유닛 GameObject가 파괴·디스폰·비활성화되면(= 화면에서 제거) 이 유닛의
            //   기 모으기(charge) 이펙트도 즉시 끈다. 멀티 Client는 사망 시 NetworkUnit 디스폰으로 여기에 온다.
            //   씬 정리 중 EffectManager가 먼저 사라졌으면 Instance가 null이라 아무것도 하지 않는다.
            EffectManager.Instance?.StopCasterOwnedAttackEffects(this, "CasterRemoved");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent("view-disable", retire: true);
            RetireInfernoMotionJumpEvidence();
            ClearInfernoAttackFacingEvidenceState();
#endif
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            EndBlockedSpawnFacingTrace("view-reinitialized");
#endif
            // [P2-1] 풀 재사용·재초기화 때 이전 유닛의 「차지 유닛」 판별값과 회차당 1회 표시가 남지 않게 먼저 지운다.
            //   실제 계산은 아래에서 Animator를 캐시한 뒤 한다.
            _hasAttackChargeMarker = false;
            _attackChargeMarkerCacheResolved = false;
            _lastChargePresentedRevision = 0UL;
            _attackPhaseNetworkUnit = GetComponent<NetworkUnit>();
            if (!ReferenceEquals(_unitData, unitData))
            {
                _combatUseCase?.CloseSoloAttackPlayback(_unitData, terminal: true);
                _healCooldownCaster = null;
                ++_healLoopOwner;
                _healLifetime = ++_nextHealLifetime;
                _healSequence = 0;
                _healPresentation = new UnitHealCastPresentationCursor();
                _healClipHeld = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _healFirstEntryEvidence = 0;
#endif
            }
            // 첫 힐 authority 검사가 lazy HealNow보다 먼저 실행돼도 component가 준비되게 한다.
            // 같은 UnitData의 반복 Initialize는 기존 lifetime/진단 budget을 새로 열지 않는다.
            EnsureHealNetworkUnit();
            _healGameEndSubscription?.Dispose();
            _healGameEndSubscription = GameEvents.OnGameEnd.Subscribe(e =>
            {
                EndHealPresentation("game-end", terminal: true);
            }).AddTo(this);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent("view-reinitialize", retire: true);
            _postCombatTrace = null;
            _postCombatLastTarget = -1;
            _postCombatExitReason = "unavailable";
            // 풀 재사용 등으로 같은 View가 새 Domain 유닛을 받으면 이전 생명주기 표본을 먼저 폐기한다.
            RetireInfernoMotionJumpEvidence();
#endif
            if (!ReferenceEquals(_unitData, unitData))
            {
                EndSoloPursuitEvidence();
                EndSoloRecoveryEvidence();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _soloPursuitLines = _soloPursuitOmitted = _soloPursuitEpisode = _soloPursuitFormatRejected = 0;
                _soloPursuitEvidenceActive = _soloPursuitEvidenceEnded = false;
                _soloRecoveryEpisode = _soloRecoveryLines = _soloRecoveryOmitted = 0;
                _soloRecoveryResumed = _soloRecoveryBlocked = _soloRecoveryOpen = 0;
                _soloRecoveryCompleted = _soloRecoveryFormatRejected = 0;
                _soloRecoveryPending = _soloRecoveryEnded = false;
                _soloRecoveryLastReason = "none";
#endif
            }
            _unitData = unitData;
            AttackPresentationAuditEligibility auditEligibility =
                AttackPresentationAuditEligibility.RequiredButMissing;
            if (_unitData != null
                && UnitAttackShadowProfileResolver.TryResolve(
                    _unitData.Type,
                    out UnitAttackShadowProfile auditProfile))
            {
                bool runtimeContractValid =
                    UnitAttackShadowProfileResolver.TryResolveRuntime(
                        _unitData,
                        out _,
                        out _);
                auditEligibility =
                    UnitAttackShadowProfileResolver.ClassifyAuditEligibility(
                        auditProfile,
                        runtimeContractValid);
            }
            if (_unitData != null)
            {
                UnitAttackResultPresentationShadowBridge.RegisterAuditEligibility(
                    _unitData.Id,
                    auditEligibility);
            }
            _rootRotationOwnership.ReleaseAll();
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
            ClearCombatTargetTracking();
            _attackPresentationImpactLease.Close();
            _attackPresentationSourceMarkerLease.Close();
            _attackPresentationImpactMode = AttackPresentationImpactMode.Suppressed;
            _lastAttackPresentationRevision = 0UL;
            _serverAttackEntryHandoffSerial = 0UL;
            _lockedMovementPipelineMode = NetworkContext.IsNetworkActive
                ? NetworkContext.ActiveUnitMovementPipelineMode
                : UnitMovementPipelineMode.Legacy;

            // Animator 캐시 — 자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용
            _animator = GetComponentInChildren<Animator>();
            // [P2-1] 공격 클립에 OnAttackCharge가 있는지 여기서 한 번만 계산한다(Animator가 없으면 첫 marker 때 계산).
            EnsureAttackChargeMarkerCache();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            CacheInfernoRenderedAnchor();
#endif

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
            IUnitFactory unitFactory = null, IBuildingObjectProvider buildingFactory = null,
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
                            StartCombatAnimation(
                                e.TargetId,
                                e.TargetIsUnit,
                                e.RestartAttackCycle,
                                e.PresentationRevision,
                                e.ImpactMode,
                                e.PresentationScope);
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

                GameEvents.OnNetworkCombatImpactSuppressed
                    .Subscribe(unitId =>
                    {
                        if (_unitData != null && unitId == _unitData.Id)
                            SuppressPendingAttackImpactPresentation();
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
                        // [2026-10-26 수정안 (a)] 투사체 사망타가 아직 화면에 안 닿았으면 사망 "표현"을 그 착탄까지 미룬다.
                        //   여기서는 미룰지 정하고 차지 VFX 귀속만 사망 대역(Visual Root)으로 넘긴다 — 아래 정리 코드(공격 회차 닫기 등)가
                        //   차지 VFX를 지금 끄지 않게 하려고 정리보다 "앞"에 둔다. 대역 분리 자체는 정리가 끝난 뒤(아래)에 한다.
                        //   미루지 않으면(비투사체 사망·이미 착탄·대역 없음) 아래 기존 코드가 예전과 똑같이 동작한다.
                        bool deferDeathPresentation = TryBeginDeferredDeathPresentation();

                        EndHealPresentation("death", terminal: true);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        PostCombatTraceEvent("unit-death", retire: true);
                        RetireMovementShadowUnit();
#endif
                        // 사망한 root의 마지막 이동/공격 목표가 adapter에 남아 다음 관측으로 재사용되지 않게 한다.
                        ClearUnitActionShadowDesiredTarget();
                        CloseAttackPhasePresentation("attacker-death");

                        // [2026-10-26 수정안 (a)] 사망 표현을 미뤘다 — Visual Root를 떼어 사망 대역으로 남긴다.
                        //   차지 VFX 정지 · IsDead · 사망 VFX·SFX · 대역 소멸은 투사체 화면 착탄(또는 상한 0.3초) 때
                        //   ReleaseDeferredDeathPresentation이 한 번 한다. 원래 오브젝트의 파괴 경로는 예전과 같다:
                        //     · 싱글: 여기서 Destroy(아래 기존 분기와 같은 줄).
                        //     · 멀티 서버(Host): 파괴하지 않는다 — NetworkCombatController의 Despawn이 처리(기존과 같다).
                        //     · 멀티 Client: 서버 Despawn 수신 때 NGO가 파괴. NetworkUnit은 이 유닛의 사망 VFX를 대역에 맡긴다.
                        if (deferDeathPresentation)
                        {
                            DetachDeferredDeathGhost();
                            if (!NetworkContext.IsNetworkActive)
                                Destroy(gameObject);
                            return;
                        }

                        // [2026-10-13 R2-1] 기 모으기(charge) 도중 죽으면 그 이펙트를 즉시 끈다.
                        //   아래의 "멀티 클라이언트는 여기서 반환" 분기보다 "앞"에 둬야 Host·Client·싱글 모두에서 실행된다.
                        //   이 유닛에게 귀속된 공격 이펙트만 멈추며, 바로 아래에서 재생하는 사망 이펙트는 영향받지 않는다.
                        EffectManager.Instance?.StopCasterOwnedAttackEffects(this, "CasterDeath");

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
        /// 타겟 종류에 맞는 팩토리에서 ID로 오브젝트를 조회한다.
        /// 파괴 여부 확인과 위치·방향 폴백은 호출자가 처리한다.
        /// </summary>
        private GameObject GetTargetObject(int targetId, bool targetIsUnit)
        {
            return targetIsUnit
                ? _unitFactory?.GetUnitObject(targetId)
                : _buildingFactory?.GetBuildingObject(targetId);
        }

        /// <summary>
        /// targetId와 타입으로 실제 GameObject의 world position 조회.
        /// 이미 파괴된 경우 현재 forward 방향 fallback.
        /// </summary>
        private Vector3 GetTargetWorldPos(int targetId, bool targetIsUnit)
        {
            GameObject obj = GetTargetObject(targetId, targetIsUnit);

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
            GameObject obj = GetTargetObject(targetId, targetIsUnit);
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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // 개발 관측 예산은 UnitData 수명에 귀속한다. 게임 판정에는 사용하지 않는다.
        private int _soloPursuitLines, _soloPursuitOmitted, _soloPursuitEpisode, _soloPursuitFormatRejected;
        private bool _soloPursuitEvidenceActive, _soloPursuitEvidenceEnded;
        private int _soloPursuitTarget, _soloPursuitIndex = -1, _soloPursuitStalls;
        private bool _soloPursuitTargetIsUnit, _soloPursuitHasRoute;
        private HexCoord _soloPursuitGoal;
        private Vector3 _soloPursuitWaypoint;
        private ulong _soloPursuitRouteRevision;
        private double _soloPursuitProgressTime;
        private int _soloRecoveryEpisode, _soloRecoveryLines, _soloRecoveryOmitted;
        private int _soloRecoveryResumed, _soloRecoveryBlocked, _soloRecoveryOpen, _soloRecoveryCompleted;
        private int _soloRecoveryFormatRejected;
        private bool _soloRecoveryPending, _soloRecoveryEnded;
        private string _soloRecoveryLastReason = "none";
#endif

        // ====================================================================
        // 타격 반응 (Animation Event → HitReaction)
        // ====================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>
        /// InfernoSpirit 한 개체의 직전/현재 위치를 세 계층에서 읽는 진단 전용 경계다.
        ///
        /// 초급자 주의:
        /// - transform.position은 서버 이동과 NetworkTransform이 담당하는 Simulation Root다.
        /// - VisualRootProjector의 PresentationTransform은 팀 관점이 적용된 화면 Root다.
        /// - cached SkinnedMeshRenderer.rootBone은 실제 스킨 메시가 기준으로 삼는 Hips 위치다.
        /// - renderer.bounds.center는 메시 외곽을 대조하기 위한 보조값일 뿐 rootBone 대체값이 아니다.
        /// 세 값은 의미가 다르므로 하나가 없을 때 다른 값으로 대신 채우지 않는다.
        /// 이 메서드는 어느 값에도 쓰지 않고 observer에 복사본만 전달한다.
        /// </summary>
        private void ObserveInfernoMotionJumpFrame()
        {
            if (_unitData == null || _unitData.Type != UnitType.InfernoSpirit)
                return;

            EnsureInfernoMotionLifecycle();

            Vector3 simulationCurrent = transform.position;
            Quaternion simulationRotationCurrent = transform.rotation;
            if (_visualRootProjector == null)
                _visualRootProjector = GetComponent<VisualRootProjector>();
            Transform visualRoot = _visualRootProjector != null
                ? _visualRootProjector.PresentationTransform
                : null;
            bool visualCurrentAvailable = visualRoot != null;
            Vector3 visualCurrent = visualCurrentAvailable
                ? visualRoot.position
                : default;
            Quaternion visualRotationCurrent = visualCurrentAvailable
                ? visualRoot.rotation
                : default;

            bool animatorAvailable = _animator != null;
            bool rendererStillMatchesCache = _infernoRenderedAnchorRenderer != null
                && _infernoRenderedAnchorRenderer.rootBone == _infernoRenderedAnchorRootBone;
            bool rendererActive = rendererStillMatchesCache
                && _infernoRenderedAnchorRenderer.enabled
                && _infernoRenderedAnchorRenderer.gameObject.activeInHierarchy;
            bool rootBoneActive = _infernoRenderedAnchorRootBone != null
                && _infernoRenderedAnchorRootBone.gameObject.activeInHierarchy;
            bool renderedAnchorCurrentAvailable = rendererActive && rootBoneActive;
            Vector3 renderedAnchorCurrent = renderedAnchorCurrentAvailable
                ? _infernoRenderedAnchorRootBone.position
                : default;
            string renderedAnchorReason = _infernoRenderedAnchorCacheReason;
            if (!rendererStillMatchesCache && _infernoRenderedAnchorRenderer != null)
                renderedAnchorReason = "renderer-root-bone-changed";
            else if (rendererStillMatchesCache && !rendererActive)
                renderedAnchorReason = "renderer-disabled";
            else if (rendererActive && !rootBoneActive)
                renderedAnchorReason = "root-bone-inactive";
            else if (renderedAnchorCurrentAvailable
                && !IsFiniteFacingVector(renderedAnchorCurrent))
            {
                renderedAnchorCurrentAvailable = false;
                renderedAnchorReason = "root-bone-position-invalid";
            }
            else if (renderedAnchorCurrentAvailable)
                renderedAnchorReason = "available";

            // bounds는 rootBone의 이상이 메시 전체 이동으로도 보이는지 대조하는 보조 증거다.
            // rootBone이 없다고 bounds를 주 기준으로 승격하면 구조 결함을 정상처럼 숨기므로 두 가용성과
            // 사유를 반드시 분리한다.
            bool rendererBoundsCurrentAvailable = rendererActive;
            Vector3 rendererBoundsCurrent = rendererBoundsCurrentAvailable
                ? _infernoRenderedAnchorRenderer.bounds.center
                : default;
            string rendererBoundsReason = rendererBoundsCurrentAvailable
                ? "available"
                : rendererStillMatchesCache ? "renderer-disabled" : _infernoRenderedAnchorCacheReason;
            if (rendererBoundsCurrentAvailable && !IsFiniteFacingVector(rendererBoundsCurrent))
            {
                rendererBoundsCurrentAvailable = false;
                rendererBoundsReason = "renderer-bounds-center-invalid";
            }

            int frame = Time.frameCount;
            double now = Time.realtimeSinceStartupAsDouble;
            if (_hasInfernoMotionPrevious && frame > _infernoMotionPreviousFrame)
            {
                bool renderedAnchorPairAvailable =
                    _infernoMotionPreviousRenderedAnchorAvailable
                    && renderedAnchorCurrentAvailable;
                bool rendererBoundsPairAvailable =
                    _infernoMotionPreviousRendererBoundsAvailable
                    && rendererBoundsCurrentAvailable;
                bool visualPairAvailable = _infernoMotionPreviousVisualAvailable
                    && visualCurrentAvailable;

                if (_movementNetworkUnit == null)
                    _movementNetworkUnit = GetComponent<NetworkUnit>();
                bool networkIdentityAvailable = NetworkContext.IsNetworkActive
                    && _movementNetworkUnit != null
                    && _movementNetworkUnit.IsSpawned;
                bool movementEvidenceAvailable = networkIdentityAvailable;
                UnitMovementPhase movementPhase = movementEvidenceAvailable
                    ? _movementNetworkUnit.MovementPhase
                    : UnitMovementPhase.Invalid;
                ulong commandRevision = movementEvidenceAvailable
                    ? _movementNetworkUnit.MovementCommandRevision
                    : 0UL;
                ulong segmentRevision = movementEvidenceAvailable
                    ? _movementNetworkUnit.MovementSegmentRevision
                    : 0UL;
                ulong semanticRevision = movementEvidenceAvailable
                    ? _movementNetworkUnit.MovementSemanticRevision
                    : 0UL;

                // 실제 이동 writer와 같은 기본 환산(TileHeight × MoveSpeed) 및 현재 상태 배율을
                // 읽는다. 값을 얻지 못하면 0 속도로 꾸미지 않고 allowance unavailable로 보낸다.
                bool hasProductionMoveSpeedSeam = _combatUseCase != null;
                float moveMultiplier = hasProductionMoveSpeedSeam
                    ? _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData)
                    : float.NaN;
                bool speedAvailable = hasProductionMoveSpeedSeam
                    && IsFiniteFacingValue(_unitData.MoveSpeed)
                    && _unitData.MoveSpeed >= 0f
                    && IsFiniteFacingValue(moveMultiplier)
                    && moveMultiplier >= 0f;
                float productionWorldSpeed = speedAvailable
                    ? HexMetrics.TileHeight * _unitData.MoveSpeed * moveMultiplier
                    : float.NaN;

                bool animatorInTransition = animatorAvailable
                    && _animator.IsInTransition(0);
                AnimatorStateInfo currentState = animatorAvailable
                    ? _animator.GetCurrentAnimatorStateInfo(0)
                    : default;
                AnimatorStateInfo nextState = animatorAvailable && animatorInTransition
                    ? _animator.GetNextAnimatorStateInfo(0)
                    : default;

                bool walkAttackTransition = animatorAvailable
                    && animatorInTransition
                    && (currentState.shortNameHash == StateWalk
                            && nextState.shortNameHash == StateAttack
                        || currentState.shortNameHash == StateAttack
                            && nextState.shortNameHash == StateWalk);

                // rootBone이 처음 유효해진 프레임은 직전의 "값 없음"과 비교하지 않는다. 이 프레임은
                // baseline만 만들기 때문에 gameplay 사건도 아니고 유닛별 8건 예산도 소비하지 않는다.
                if (UnitAttackShadowObserver.ShouldRecordInfernoMotionPairForValidation(
                    _hasInfernoMotionPrevious,
                    _infernoMotionPreviousRenderedAnchorAvailable,
                    renderedAnchorCurrentAvailable))
                {
                    UnitAttackShadowObserver.RecordInfernoMotionJump(
                        new InfernoMotionJumpSample
                        {
                        UnitId = _unitData.Id,
                        NetworkObjectId = networkIdentityAvailable
                            ? _movementNetworkUnit.NetworkObjectId
                            : 0UL,
                        NetworkIdentityAvailable = networkIdentityAvailable,
                        Lifecycle = _infernoMotionLifecycle,
                        ViewFlipped = ViewConverter.IsFlipped,
                        PreviousFrame = _infernoMotionPreviousFrame,
                        Frame = frame,
                        PreviousTime = _infernoMotionPreviousTime,
                        Time = now,
                        DeltaTime = Time.deltaTime,
                        RawFrameSeconds = (float)(now - _infernoMotionPreviousTime),
                        SimulationPrevious = _infernoMotionPreviousSimulation,
                        SimulationCurrent = simulationCurrent,
                        SimulationRotationPrevious = _infernoMotionPreviousSimulationRotation,
                        SimulationRotationCurrent = simulationRotationCurrent,
                        VisualAvailable = visualPairAvailable,
                        VisualPrevious = _infernoMotionPreviousVisual,
                        VisualCurrent = visualCurrent,
                        VisualRotationPrevious = _infernoMotionPreviousVisualRotation,
                        VisualRotationCurrent = visualRotationCurrent,
                        RenderedAnchorAvailable = renderedAnchorPairAvailable,
                        RenderedAnchorPrevious = _infernoMotionPreviousRenderedAnchor,
                        RenderedAnchorCurrent = renderedAnchorCurrent,
                        RenderedAnchorUnavailableReason = renderedAnchorCurrentAvailable
                            ? "previous-rendered-anchor-unavailable"
                            : renderedAnchorReason,
                        RendererBoundsAvailable = rendererBoundsPairAvailable,
                        RendererBoundsPrevious = _infernoMotionPreviousRendererBounds,
                        RendererBoundsCurrent = rendererBoundsCurrent,
                        RendererBoundsUnavailableReason = rendererBoundsCurrentAvailable
                            ? "previous-renderer-bounds-unavailable"
                            : rendererBoundsReason,
                        SpeedAvailable = speedAvailable,
                        ProductionWorldSpeed = productionWorldSpeed,
                        MovementEvidenceAvailable = movementEvidenceAvailable,
                        MovementPhase = movementPhase,
                        CommandRevision = commandRevision,
                        SegmentRevision = segmentRevision,
                        SemanticRevision = semanticRevision,
                        ReplicationEvidenceAvailable = networkIdentityAvailable,
                        AnimatorCurrentStateHash = animatorAvailable
                            ? currentState.fullPathHash
                            : 0,
                        AnimatorNextStateHash = animatorAvailable && animatorInTransition
                            ? nextState.fullPathHash
                            : 0,
                        AnimatorInTransition = animatorInTransition,
                        WalkAttackTransition = walkAttackTransition
                    });
                }
            }

            // 정상/이상 여부와 무관하게 다음 프레임 비교 기준만 갱신한다. 로그 출력 여부는
            // bounded observer가 결정하며 여기서는 위치나 Animator를 절대 보정하지 않는다.
            _hasInfernoMotionPrevious = true;
            _infernoMotionPreviousFrame = frame;
            _infernoMotionPreviousTime = now;
            _infernoMotionPreviousSimulation = simulationCurrent;
            _infernoMotionPreviousSimulationRotation = simulationRotationCurrent;
            _infernoMotionPreviousVisualAvailable = visualCurrentAvailable;
            _infernoMotionPreviousVisual = visualCurrent;
            _infernoMotionPreviousVisualRotation = visualRotationCurrent;
            _infernoMotionPreviousRenderedAnchorAvailable = renderedAnchorCurrentAvailable;
            _infernoMotionPreviousRenderedAnchor = renderedAnchorCurrent;
            _infernoMotionPreviousRendererBoundsAvailable = rendererBoundsCurrentAvailable;
            _infernoMotionPreviousRendererBounds = rendererBoundsCurrent;
        }

        /// <summary>
        /// InfernoSpirit가 실제로 화면에 그리는 단 하나의 스킨 메시와 그 Hips 기준점을 캐시한다.
        /// 이 탐색은 Initialize에서 한 번만 실행된다. LateUpdate에서 계층을 다시 찾지 않으므로 유닛 수와
        /// 프레임 수에 비례하는 배열 할당이 생기지 않는다. 구조가 production 계약과 다르면 임의의 첫
        /// renderer나 bounds를 정상값으로 꾸미지 않고 unavailable 사유를 남긴다.
        /// </summary>
        private void CacheInfernoRenderedAnchor()
        {
            ClearInfernoRenderedAnchorCache();
            if (_unitData == null || _unitData.Type != UnitType.InfernoSpirit)
                return;

            _infernoRenderedAnchorSearch.Clear();
            GetComponentsInChildren(true, _infernoRenderedAnchorSearch);
            if (_infernoRenderedAnchorSearch.Count != 1)
            {
                _infernoRenderedAnchorCacheReason =
                    $"skinned-renderer-count-{_infernoRenderedAnchorSearch.Count}";
                _infernoRenderedAnchorSearch.Clear();
                return;
            }

            SkinnedMeshRenderer renderer = _infernoRenderedAnchorSearch[0];
            Transform rootBone = renderer != null ? renderer.rootBone : null;
            Transform animatorHips = _animator != null && _animator.isHuman
                ? _animator.GetBoneTransform(HumanBodyBones.Hips)
                : null;
            if (renderer == null)
                _infernoRenderedAnchorCacheReason = "skinned-renderer-unavailable";
            else if (rootBone == null)
                _infernoRenderedAnchorCacheReason = "renderer-root-bone-unavailable";
            else if (_animator == null)
                _infernoRenderedAnchorCacheReason = "animator-unavailable";
            else if (!_animator.isHuman)
                _infernoRenderedAnchorCacheReason = "animator-not-humanoid";
            else if (animatorHips == null)
                _infernoRenderedAnchorCacheReason = "animator-hips-unavailable";
            else if (rootBone != animatorHips)
                _infernoRenderedAnchorCacheReason = "root-bone-hips-mismatch";
            else
            {
                _infernoRenderedAnchorRenderer = renderer;
                _infernoRenderedAnchorRootBone = rootBone;
                _infernoRenderedAnchorCacheReason = "available";
            }
            _infernoRenderedAnchorSearch.Clear();
        }

        private void ClearInfernoRenderedAnchorCache()
        {
            _infernoRenderedAnchorRenderer = null;
            _infernoRenderedAnchorRootBone = null;
            _infernoRenderedAnchorCacheReason = "not-initialized";
            _infernoRenderedAnchorSearch.Clear();
        }

        private void EnsureInfernoMotionLifecycle()
        {
            if (_infernoMotionLifecycle != 0UL) return;
            _nextInfernoMotionLifecycle = _nextInfernoMotionLifecycle == ulong.MaxValue
                ? 1UL
                : _nextInfernoMotionLifecycle + 1UL;
            _infernoMotionLifecycle = _nextInfernoMotionLifecycle;
            _hasInfernoMotionPrevious = false;
        }

        private void RetireInfernoMotionJumpEvidence()
        {
            if (_infernoMotionLifecycle != 0UL && _unitData != null)
            {
                UnitAttackShadowObserver.RetireInfernoMotionJump(
                    _unitData.Id,
                    _infernoMotionLifecycle);
            }
            _infernoMotionLifecycle = 0UL;
            _hasInfernoMotionPrevious = false;
            _infernoMotionPreviousVisualAvailable = false;
            _infernoMotionPreviousRenderedAnchorAvailable = false;
            _infernoMotionPreviousRendererBoundsAvailable = false;
            ClearInfernoRenderedAnchorCache();
        }

        private static bool IsFiniteFacingValue(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        private void SetInfernoAttackFacingIdentity(
            int targetId,
            bool targetIsUnit,
            ulong presentationRevision,
            AttackPresentationScope scope)
        {
            if (_unitData == null || _unitData.Type != UnitType.InfernoSpirit) return;
            _infernoFacingTargetId = targetId;
            _infernoFacingTargetIsUnit = targetIsUnit;
            _infernoFacingPresentationRevision = presentationRevision;
            _infernoFacingScope = scope;
        }

        private void QueueInfernoAttackStartFacing()
        {
            if (_unitData == null || _unitData.Type != UnitType.InfernoSpirit) return;
            _infernoAttackStartFacingPending = true;
        }

        private void ConsumePendingInfernoAttackStartFacing()
        {
            if (!_infernoAttackStartFacingPending) return;
            _infernoAttackStartFacingPending = false;
            ObserveInfernoAttackFacing(
                AnimatedAttackFacingStage.AttackStartPose,
                _infernoFacingTargetId,
                _infernoFacingTargetIsUnit,
                _infernoFacingPresentationRevision,
                _infernoFacingScope);
        }

        private void ClearInfernoAttackFacingEvidenceState()
        {
            _infernoAttackStartFacingPending = false;
            _infernoFacingTargetId = -1;
            _infernoFacingTargetIsUnit = false;
            _infernoFacingPresentationRevision = 0UL;
            _infernoFacingScope = default;
        }

        private void ObserveInfernoAttackFacing(
            AnimatedAttackFacingStage stage,
            int targetId,
            bool targetIsUnit,
            ulong presentationRevision,
            AttackPresentationScope scope)
        {
            if (_unitData == null || _unitData.Type != UnitType.InfernoSpirit) return;

            Transform visual = PresentationTransform;
            Transform target = _combatTargetId == targetId
                    && _combatTargetIsUnit == targetIsUnit
                ? _combatTargetTransform
                : GetTargetTransform(targetId, targetIsUnit);
            Transform presentationTarget = GetPresentationTransform(target);
            Vector3 simulationTargetDirection = target != null
                ? target.position - transform.position
                : default;
            Vector3 presentationTargetDirection = visual != null && presentationTarget != null
                ? presentationTarget.position - visual.position
                : default;

            bool animatorAvailable = _animator != null;
            bool animatorIsHuman = animatorAvailable && _animator.isHuman;
            bool inTransition = animatorAvailable && _animator.IsInTransition(0);
            AnimatorStateInfo currentState = animatorAvailable
                ? _animator.GetCurrentAnimatorStateInfo(0)
                : default;
            AnimatorStateInfo nextState = animatorAvailable && inTransition
                ? _animator.GetNextAnimatorStateInfo(0)
                : default;
            // Humanoid의 계산된 body 방향 API는 일반 LateUpdate/Animation Event 경로에서 읽으면
            // Unity 경고를 발생시킨다. 이미 production 계약으로 검증하고 캐시한 renderer rootBone
            // (정확히 Animator Hips와 같은 Transform)의 실제 방향만 읽는다. 이 값은 과거 Humanoid
            // body 방향과 의미가 같다고 가장하지 않고 rendered Hips 방향으로 명시해 전달한다.
            Vector3 renderedHipsForward = default;
            bool renderedHipsEvidenceAvailable = _infernoRenderedAnchorRenderer != null
                && _infernoRenderedAnchorRootBone != null
                && _infernoRenderedAnchorRenderer.rootBone == _infernoRenderedAnchorRootBone
                && _infernoRenderedAnchorRenderer.enabled
                && _infernoRenderedAnchorRenderer.gameObject.activeInHierarchy
                && _infernoRenderedAnchorRootBone.gameObject.activeInHierarchy;
            string unavailableReason = "available";
            if (!animatorAvailable)
                unavailableReason = "animator-unavailable";
            else if (!animatorIsHuman)
                unavailableReason = "animator-not-humanoid";
            else if (!renderedHipsEvidenceAvailable)
                unavailableReason = NormalizeInfernoFacingReason(_infernoRenderedAnchorCacheReason);
            else
            {
                renderedHipsForward = _infernoRenderedAnchorRootBone.forward;
                if (!IsFiniteFacingVector(renderedHipsForward))
                {
                    renderedHipsEvidenceAvailable = false;
                    unavailableReason = "rendered-hips-forward-invalid";
                }
            }
            if (visual == null)
                unavailableReason = "visual-root-unavailable";
            else if (presentationTarget == null)
                unavailableReason = "target-presentation-unavailable";

            UnitAttackShadowObserver.RecordAnimatedAttackFacing(
                stage,
                _unitData.Id,
                _unitData.Type,
                targetId,
                targetIsUnit,
                presentationRevision,
                scope,
                ViewConverter.IsFlipped,
                animatorAvailable ? currentState.fullPathHash : 0,
                animatorAvailable && inTransition ? nextState.fullPathHash : 0,
                animatorAvailable ? currentState.normalizedTime : float.NaN,
                inTransition,
                animatorIsHuman,
                transform.forward,
                visual != null ? visual.forward : default,
                renderedHipsForward,
                renderedHipsEvidenceAvailable,
                simulationTargetDirection,
                presentationTargetDirection,
                unavailableReason);
        }

        private static string NormalizeInfernoFacingReason(string reason)
            => string.IsNullOrEmpty(reason) || reason == "available"
                ? "rendered-hips-unavailable"
                : reason;

        private static bool IsFiniteFacingVector(Vector3 value)
            => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
#endif

        /// <summary>
        /// Animation Event에서 AnimationEventRelay를 통해 호출.
        /// 공격 애니메이션의 타격 프레임에 맞춰 피격 대상에게 시각적 반응(스케일 펀치)을 적용.
        /// 실제 데미지 로직과는 무관 — 순수 비주얼 피드백.
        /// </summary>
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static int _torrentAllowedMarkerRows;
        private static int _torrentSuppressedMarkerRows;
        private static int _torrentPlaybackRows;

        // 로컬 marker는 서버 waveId를 알 수 없다. 시간·시전자 비교용 표지만 남긴다.
        private void LogTorrentMarker(string outcome, bool validScope,
            AttackPresentationScope scope, bool sourceLease, bool sourceOnly)
        {
            bool allowed = outcome == "allowed";
            if (allowed ? _torrentAllowedMarkerRows >= 24 : _torrentSuppressedMarkerRows >= 8)
                return;
            if (allowed) _torrentAllowedMarkerRows++;
            else _torrentSuppressedMarkerRows++;
            GameLog.Dev.Info("Combat", nameof(UnitView), "[DEBUG-TORRENT-WAVE] local-marker",
                $"peer={(NetworkContext.IsNetworkActive ? (NetworkContext.IsNetworkServer ? "Host" : "Client") : "Single")}, t={Time.realtimeSinceStartup:F3}, caster={(_unitData != null ? _unitData.Id : -1)}, outcome={outcome}, mode={_attackPresentationImpactMode}, revision={_lastAttackPresentationRevision}, validScope={validScope && scope.IsValid}, sourceLease={sourceLease}, sourceOnly={sourceOnly}, noWaveId=true");
        }
#endif

        public void OnAttackHit()
            => ProcessAttackMarker(chargeOnly: false);

        // 일반 유닛은 종전 marker 본문을 그대로 사용한다. 어느 콜백도 권위 HP 타이머를 실행하지 않는다.
        // [P4 — 2026-10-19 차지 유닛 발사 시점] 옛 문장 「Fox만 source charge와 관측용 피해 marker를 나눈다」는
        //   차지 유닛 공통 기준으로 바뀌었다. 차지 유닛(공격 클립에 OnAttackCharge가 있는 유닛 — FoxMagician·InfernoSpirit)은
        //   ① chargeOnly=true(OnAttackCharge): lease 확인만 하고 기 모으기(charge) VFX만 켠다. SFX·트레이서·피격 신호 없음.
        //   ② chargeOnly=false(OnAttackHit): lease를 소비하고, charge VFX는 다시 켜지 않으며, 트레이서(투사체) 발사 + 공격 SFX.
        //   서버 피해 시점·피해량은 이 메서드와 무관하다(UnitFactory가 OnAttackHit 시간만 추출한 서버 타이머가 소유).
        private void ProcessAttackMarker(bool chargeOnly)
        {
            // [P2-1] Initialize 때 Animator가 없어 판별을 못 했으면 여기서 한 번 계산한다(이미 계산했으면 즉시 반환).
            EnsureAttackChargeMarkerCache();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!chargeOnly && !NetworkContext.IsNetworkActive && _unitData != null && _animator != null)
            {
                bool genuine = _nativeAttackMarker != null && _nativeAttackMarker.isFiredByAnimator;
                AnimatorStateInfo observed = genuine ? _nativeAttackMarker.animatorStateInfo : _animator.GetCurrentAnimatorStateInfo(0);
                AnimationClip observedClip = genuine ? _nativeAttackMarker.animatorClipInfo.clip : null;
                _combatUseCase?.ObserveSoloAttackMarker(_unitData, Time.timeAsDouble,
                    _nativeAttackMarker != null ? _nativeAttackMarker.time : double.NaN,
                    observed.normalizedTime, _animator.speed * observed.speed * observed.speedMultiplier,
                    genuine, observed.fullPathHash, observedClip != null ? observedClip.GetInstanceID() : 0);
            }
#endif
            if (!chargeOnly && _unitData != null && _unitData.Type == UnitType.FoxMagician)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_unitData.IsAlive)
                    UnitAttackShadowObserver.RecordFoxMagicianNativeImpactMarker(
                        _unitData.Id, _unitData.Type, _lastAttackPresentationRevision,
                        _nativeAttackMarker != null ? _nativeAttackMarker.time : double.NaN,
                        _nativeAttackMarker != null && _nativeAttackMarker.isFiredByAnimator);
#endif
                // Fox 타격 marker는 위 진단만 남기고 끝나지 않는다 — 아래 일반 경로(lease 소비)로 그대로 이어진다.
            }
            PistoleerTimingDiagnostics.Event(_unitData, "RawMarker", _pistoleerTimingScope,
                UnitAttackResultPresentationShadowBridge.CurrentServerTime,
                $"normalized={PistoleerTimingDiagnostics.Number(_animator != null ? _animator.GetCurrentAnimatorStateInfo(0).normalizedTime : double.NaN)} continuity={_pistoleerContinuity} renderedAnchor={PistoleerTimingDiagnostics.Number(_pistoleerRenderedAnchor)} anchorNormalized={PistoleerTimingDiagnostics.Number(_pistoleerAnchorNormalized)}");
            // 독립 wave의 VFX/SFX는 실제 서버 Spawn payload만 소유한다. marker는 clip 검증용으로 남긴다.
            // Legacy 코드는 아래 그대로 보존하며 다른 Unresolved 유닛의 표현을 막지 않는다.
            if (_unitData != null && _unitData.Type == UnitType.TorrentSpirit
                && _combatUseCase != null && _combatUseCase.WavePresentationOwned) return;
            if (_unitData == null || !_unitData.IsAlive)
            {
                PistoleerTimingDiagnostics.Event(_unitData, "Unavailable", _pistoleerTimingScope,
                    UnitAttackResultPresentationShadowBridge.CurrentServerTime, "reason=marker-unit-dead noVfx=1");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (_unitData != null && _unitData.Type == UnitType.TorrentSpirit)
                    LogTorrentMarker("unit-unavailable", false, default, false, false);
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (_unitData != null && _unitData.Type == UnitType.InfernoSpirit)
                {
                    UnitAttackShadowObserver.RecordInfernoAttackVfxAttempt(
                        _unitData.Id,
                        _attackPresentationImpactMode,
                        false,
                        false,
                        default,
                        "unit-unavailable",
                        EffectManager.Instance != null,
                        false,
                        false,
                        false,
                        0,
                        false,
                        Vector3.zero);
                }
#endif
                return;
            }
            // [2026-10-21 서버 권위 투사체 단계 D] 차지 신호인데 서버(싱글·Host의 UseCase)가 이번 공격의 발사를 이미 취소했다면
            //   차지 VFX를 켜지 않는다(사용자 결정: 차지 중 끊김 = 투사체·소리·피해 없음, 차지 VFX 즉시 정지 후 다음 동작).
            //   이미 켜진 차지는 취소 통보(ApplyServerProjectileLaunchCancel)가 그 순간 끈다 — 이 줄은 "취소 뒤에 오는 차지 신호"를 막는다.
            //   lease는 읽지도 소비하지도 않는다(차지 = lease 확인 전용 계약 유지). 순수 Client는 이 질문이 항상 false다.
            if (chargeOnly && IsServerProjectileChargeCancelled())
            {
                ServerProjectileDiagnostics.RecordChargeSuppressedAfterServerCancel(_unitData.Type);
                return;
            }
            // 멀티플레이 일반 공격은 유효한 커밋 scope를 이 지점에서 먼저 소비해야만
            // VFX/SFX/tracer/로컬 피격 신호를 방출한다. BloomFairy 힐과 싱글플레이는
            // C3 공격 상관관계의 대상이 아니므로 기존 별도 경로를 유지한다.
            bool networkActive = NetworkContext.IsNetworkActive;
            bool separateHeal = _unitData.IsHealer;
            if (separateHeal)
            {
                bool allowed = _healPresentation.ConsumeMarker();
                UnitHealCastDiagnostics.Event(allowed ? "Marker" : "MarkerSuppressed", _healPresentation.Current);
                ObserveHealPose(allowed ? "Marker" : "MarkerSuppressed", _healPresentation.Current);
                if (!allowed) return;
            }
            AttackPresentationScope markerScope = default;
            bool consumedValidScope = false;
            bool consumedSourceMarker = false;
            bool sourceOnlyMarker = false;
            NativeOccurrenceContext nativeMarkerOwner = null;
            AttackPresentationMarkerEmission markerEmission =
                AttackPresentationMarkerEmission.Full;
            if (networkActive && !separateHeal && _attackPhase.Active)
            {
                // The native absolute occurrence gate precedes every legacy count lease.
                // Return the occurrence's exact hit, never the old cursor after a baseline skip.
                if (_nativeAttackMarker == null || !_nativeAttackMarker.isFiredByAnimator
                    || _nativeAttackMarker.animatorClipInfo.clip != _attackPhaseClip
                    || _nativeAttackMarker.animatorStateInfo.shortNameHash != StateAttack)
                {
                    PistoleerTimingDiagnostics.Event(_unitData, "NativeOccurrenceRejected", _attackPhase.Scope,
                        _attackPhaseClockHighWater, "reason=native-metadata-mismatch noVfx=1");
                    AttackPhaseNativeRejections++;
                    return;
                }
                double normalized = _nativeAttackMarker.animatorStateInfo.normalizedTime;
                if (double.IsNaN(normalized) || double.IsInfinity(normalized)) return;
                // 같은 Attack clip/hash/loop라도 self-crossfade의 outgoing state는 새 epoch가 아니다.
                // native callback의 절대 state time을 현재/다음 state와 비교한다. 동률은 식별
                // 불가능하므로 fail closed한다. 시간 허용치나 marker 허가를 늘리지 않는다.
                if (_animator.IsInTransition(0))
                {
                    var outgoing = _animator.GetCurrentAnimatorStateInfo(0);
                    var incoming = _animator.GetNextAnimatorStateInfo(0);
                    if (outgoing.shortNameHash == StateAttack && incoming.shortNameHash == StateAttack
                        && Math.Abs(normalized - outgoing.normalizedTime) <= Math.Abs(normalized - incoming.normalizedTime))
                    {
                        AttackPhaseNativeRejections++;
                        PistoleerTimingDiagnostics.Event(_unitData, "NativeOccurrenceRejected", _attackPhase.Scope,
                            _attackPhaseClockHighWater, $"reason=outgoing-or-ambiguous-native-state callback={PistoleerTimingDiagnostics.Number(normalized)} outgoing={PistoleerTimingDiagnostics.Number(outgoing.normalizedTime)} incoming={PistoleerTimingDiagnostics.Number(incoming.normalizedTime)} noVfx=1");
                        return;
                    }
                }
                long loop = (long)Math.Floor(normalized);
                ObserveNativeLease("consume-before", _attackPhase.Scope, _attackPhaseClockHighWater, loop, _nativeAttackMarker.time);
                if (!TryConsumeNativeOwner(loop, _nativeAttackMarker.time, out markerScope, out nativeMarkerOwner, out string reason))
                {
                    ObserveNativeLease("consume-rejected-" + reason, _attackPhase.Scope, _attackPhaseClockHighWater, loop, _nativeAttackMarker.time);
                    PistoleerTimingDiagnostics.Event(_unitData, "NativeOccurrenceRejected", _attackPhase.Scope,
                        _attackPhaseClockHighWater, $"reason={reason} loop={loop} marker={PistoleerTimingDiagnostics.Number(_nativeAttackMarker.time)} noVfx=1");
                    AttackPhaseNativeRejections++;
                    return;
                }
                ObserveNativeLease("consume-admitted", markerScope, _attackPhaseClockHighWater, loop, _nativeAttackMarker.time);
                sourceOnlyMarker = nativeMarkerOwner.Lease.SourceOnly;
                // pending old는 새 cursor의 count lease를 소모하지 않는다. exact occurrence 자체가 허가다.
                consumedSourceMarker = nativeMarkerOwner.Lease != _attackOccurrenceLease
                    || _attackPresentationSourceMarkerLease.TryConsume(out _);
                consumedValidScope = !sourceOnlyMarker;
                markerEmission = sourceOnlyMarker ? AttackPresentationMarkerEmission.SourceOnly : AttackPresentationMarkerEmission.Full;
                _pistoleerTimingScope = markerScope;
                PistoleerTimingDiagnostics.Event(_unitData, "NativeOccurrenceAdmitted", markerScope,
                    _attackPhaseClockHighWater, $"loop={loop} sourceOnly={(sourceOnlyMarker ? 1 : 0)} native=1");
            }
            else if (networkActive && !separateHeal)
            {
                if (chargeOnly)
                {
                    // [P2-3 — 2026-10-19 차지 유닛 발사 시점] 차지 신호는 허가표(lease)를 「확인만」 한다.
                    //   서버 커밋 1회는 lease를 공격 1회분(HitFrameTimes 개수 = 1)만 장전한다. 예전처럼 차지가 그 1회분을
                    //   먼저 써 버리면 정작 타격(OnAttackHit) 때 lease가 없어 투사체가 나가지 않는다(멀티에서만 드러나는 문제).
                    //   그래서 아래 두 lease 모두 상태를 바꾸지 않고 읽기만 한다. 소비는 타격 marker가 한다.
                    //
                    //   이름은 consumed…지만 차지일 때는 「소비했다면 성공했을 것인가」라는 뜻으로 쓴다.
                    //   ① source lease: Armed · 남은 횟수 · SourceOnly 아님 · 현재 회차 · 회차당 1회(CanPresentAttackChargeWithoutConsuming).
                    consumedSourceMarker = CanPresentAttackChargeWithoutConsuming();
                    sourceOnlyMarker = false;
                    //   ② impact lease: TryConsume는 IsArmed일 때만 성공하고, Armed 동안에는 남은 HitIndex가 항상 1개 이상이다
                    //      (마지막 HitIndex를 쓰는 순간 상태가 Consumed로 바뀐다). 그래서 IsArmed 확인이 「소비 가능」과 같은 뜻이며
                    //      새 읽기 API(P3)는 필요 없다. LegacyFallback(현재 두 차지 유닛)은 impact lease를 장전하지 않으므로 false다.
                    consumedValidScope = consumedSourceMarker
                        && _unitData != null
                        && _attackPresentationImpactLease.IsArmed;
                }
                else
                {
                    // 타격 marker(차지 유닛 포함)는 종전 그대로 source lease와 impact lease를 소비한다.
                    consumedSourceMarker =
                        _attackPresentationSourceMarkerLease.TryConsume(out sourceOnlyMarker);
                    if (!sourceOnlyMarker)
                        consumedValidScope = TryAcquirePresentationShadowMarkerScope(out markerScope);
                }
                markerEmission =
                    UnitAttackPresentationPolicy.ResolveNetworkAttackMarkerEmission(
                        _attackPresentationImpactMode,
                        consumedValidScope,
                        consumedSourceMarker,
                        sourceOnlyMarker);
                // [P2-3] 차지를 실제로 허락한 회차를 기억한다 → 같은 회차의 다음 루프 차지 신호는 위 ④에서 막힌다.
                if (chargeOnly && markerEmission != AttackPresentationMarkerEmission.Suppressed)
                    _lastChargePresentedRevision = _attackPresentationSourceMarkerLease.PresentationRevision;
                if (markerEmission == AttackPresentationMarkerEmission.Suppressed)
                {
                    PistoleerTimingDiagnostics.Event(_unitData, "MarkerSuppressed", _pistoleerTimingScope,
                        UnitAttackResultPresentationShadowBridge.CurrentServerTime, "noVfx=1 reason=lease-gate");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (_unitData.Type == UnitType.TorrentSpirit)
                        LogTorrentMarker("gate-suppressed", consumedValidScope, markerScope,
                            consumedSourceMarker, sourceOnlyMarker);
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (_unitData.Type == UnitType.InfernoSpirit)
                    {
                        UnitAttackShadowObserver.RecordInfernoAttackVfxAttempt(
                            _unitData.Id,
                            _attackPresentationImpactMode,
                            consumedValidScope,
                            false,
                            markerScope,
                            "gate-suppressed",
                            EffectManager.Instance != null,
                            false,
                            false,
                            false,
                            0,
                            false,
                            Vector3.zero);
                    }
#endif
                    return;
                }
            }
            else if (!UnitAttackPresentationPolicy.ShouldEmitLocalImpact(
                         networkActive,
                         separateHeal))
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (_unitData.Type == UnitType.TorrentSpirit)
                    LogTorrentMarker("policy-suppressed", false, default, false, false);
#endif
                return;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_unitData.Type == UnitType.TorrentSpirit)
                LogTorrentMarker("allowed", consumedValidScope, markerScope,
                    consumedSourceMarker, sourceOnlyMarker);
#endif

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (networkActive && _unitData.Type == UnitType.StreamSpirit)
            {
                // Raw Animation Event가 아니라 source-marker lease가 실제 표현을 허용한 경계만
                // 기록한다. provisional marker와 이미 소비된 loop marker는 정상 차단이므로
                // VFX 누락이나 unmatched 회차로 오판하지 않는다.
                UnitAttackShadowObserver.RecordStreamSpiritTimelineMarker(
                    _unitData.Id,
                    _unitData.Type,
                    _lastAttackPresentationRevision,
                    _attackPresentationImpactMode);
            }
            // [P2-5 — 2026-10-19] Fox 타격 marker도 이제 이 경로를 지나므로(ProcessAttackMarker 앞부분 Fox 분기), Fox 진단 기록은 「차지」 marker일 때만 남긴다.
            //   이 관측기의 marker 단계는 원래 1초 charge-marker 기록이다(타격 기록은 위 RecordFoxMagicianNativeImpactMarker).
            //   옛 조건: if (networkActive && _unitData.Type == UnitType.FoxMagician)
            if (chargeOnly && networkActive && _unitData.Type == UnitType.FoxMagician)
            {
                UnitAttackShadowObserver.RecordFoxMagicianTimelineMarker(
                    _unitData.Id,
                    _unitData.Type,
                    _lastAttackPresentationRevision,
                    _attackPresentationImpactMode);
            }
#endif

            // 타겟 참조는 Animation Event 뒤 곧바로 Stop/ChangeTarget/사망 처리로 지워질 수 있다.
            // 특히 원거리 Tracer 콜백은 수 프레임 뒤 실행되므로 필드를 다시 읽으면 새 타겟이나
            // -1을 이번 발사의 피해자로 기록한다. 발사 경계의 값과 exact scope를 함께 캡처한다.
            int presentationVictimId = chargeOnly ? -1 : nativeMarkerOwner != null ? nativeMarkerOwner.Owner.TargetId : _combatTargetId;
            bool presentationVictimIsUnit = !chargeOnly && (nativeMarkerOwner != null ? nativeMarkerOwner.Owner.TargetIsUnit : _combatTargetIsUnit);
            bool sourceOnlyPresentation =
                markerEmission == AttackPresentationMarkerEmission.SourceOnly;
            PistoleerTimingDiagnostics.Event(_unitData, "Marker", sourceOnlyPresentation ? _pistoleerTimingScope : markerScope,
                UnitAttackResultPresentationShadowBridge.CurrentServerTime, $"sourceOnly={(sourceOnlyPresentation ? 1 : 0)}");
            // 방향 진단은 marker가 발생한 순간의 복제 회차를 사용해야 한다. 트레이서가
            // 날아가는 동안 최신 NetworkVariable을 다시 읽으면 다음 공격 revision과 섞인다.
            AttackPresentationReplicatedSnapshot markerReplicatedSnapshot =
                chargeOnly || sourceOnlyPresentation
                    || UnitAttackResultPresentationShadowBridge.OwnsAttacker(_unitData.Id)
                    ? default : nativeMarkerOwner != null ? nativeMarkerOwner.Snapshot : CaptureAttackPresentationReplicatedSnapshot();
            bool authoritativePresentation = !chargeOnly && !sourceOnlyPresentation
                && UnitAttackResultPresentationShadowBridge.OwnsAttacker(_unitData.Id);
            var presentationCoordinator = UnitAttackResultPresentationShadowBridge.Coordinator;
            bool hasConfirmedImpact = authoritativePresentation
                && presentationCoordinator.TryGetConfirmedResult(markerScope, out _);
            AttackResultPresentationInput confirmedImpact = default;
            if (hasConfirmedImpact) presentationCoordinator.TryGetConfirmedResult(markerScope, out confirmedImpact);
            AttackPresentationSchedule committedSchedule = default;
            if (authoritativePresentation) presentationCoordinator.TryGetSchedule(markerScope, out committedSchedule);

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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // Animation Event까지 도달했다면 Animator가 Attack pose를 평가한 상태다.
            // 비정상적인 실행 순서로 LateUpdate보다 marker가 먼저 왔을 때도 시작 표본을 먼저 닫는다.
            if (!chargeOnly)
            {
                ConsumePendingInfernoAttackStartFacing();
                ObserveInfernoAttackFacing(
                    AnimatedAttackFacingStage.ImpactMarker,
                    presentationVictimId,
                    presentationVictimIsUnit,
                    _infernoFacingPresentationRevision,
                    markerScope.IsValid ? markerScope : _infernoFacingScope);
            }
#endif
            if (!chargeOnly && !sourceOnlyPresentation)
            {
                ObservePresentationShadowMarker(
                    LegacyPresentationObservationKind.Marker,
                    presentationTransform,
                    markerScope,
                    presentationVictimId,
                    presentationVictimIsUnit,
                    markerReplicatedSnapshot);
            }
            Vector3 spawnPos = _vfxSpawnPoint != null
                ? _vfxSpawnPoint.position
                : presentationTransform.position;
            Quaternion spawnRot = nativeMarkerOwner != null && nativeMarkerOwner.Lease != _attackOccurrenceLease
                ? nativeMarkerOwner.Rotation : Quaternion.LookRotation(presentationTransform.forward);
            if (authoritativePresentation)
            {
                // 결과가 있으면 해당 Impact의 방향을 사용한다. 아직 결과가 없는 표식은
                // commit 의도만 사용하며 최신 NetworkVariable을 과거 타격 증거로 재조회하지 않는다.
                ActionDirectionXZ aim = hasConfirmedImpact ? confirmedImpact.AimDirection : committedSchedule.CommitDirection;
                if (aim.IsValid)
                {
                    if (ViewConverter.IsFlipped) aim = UnitAttackResultPresentationShadowScheduler.FlipDirection180(aim);
                    spawnRot = Quaternion.LookRotation(new Vector3((float)aim.X, 0f, (float)aim.Z));
                }
            }
            EffectManager effectManager = EffectManager.Instance;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // [P2-5] Fox VFX 시도 기록도 charge VFX를 실제로 켜는 차지 marker일 때만. 옛 조건: if (_unitData.Type == UnitType.FoxMagician)
            if (chargeOnly && _unitData.Type == UnitType.FoxMagician)
            {
                UnitAttackShadowObserver.RecordFoxMagicianTimelineVfxAttempt(
                    _unitData.Id,
                    _unitData.Type,
                    _lastAttackPresentationRevision,
                    _attackPresentationImpactMode);
            }
#endif
            PistoleerTimingDiagnostics.Event(_unitData, "VfxAttempt", sourceOnlyPresentation ? _pistoleerTimingScope : markerScope,
                UnitAttackResultPresentationShadowBridge.CurrentServerTime, "coldPool=unknown");
            // [2026-10-13 R2-1] 마지막 인자 this = "이 이펙트의 시전자는 나다".
            //   기 모으기(charge)처럼 시전자 귀속으로 표시된 프리셋만 이 정보를 써서, 내가 죽거나 사라지면 즉시 멈춘다.
            //   귀속 표시가 없는 공격 이펙트(Pistoleer·Cannon 등)는 예전과 완전히 같게 끝까지 재생된다.
            // ────────────────────────────────────────────────────────────────
            // [서버 권위 투사체 — 투사체 유닛의 「타격」 marker는 아무것도 내지 않는다]
            //   투사체 유닛(Tank · CannonCart · InfernoSpirit · StreamSpirit · FoxMagician · EagleArcher · MushroomBomber)의
            //   투사체 · 총구 VFX · 발사음은 "서버 발사 기록을 화면이 꺼내는 순간"(ProjectilePresentationPresenter.Begin →
            //   PlayServerProjectileLaunchCue)에 나오고, 피해 숫자는 서버 착탄 결과로 착탄 순간에 1회 나온다.
            //   왜 marker에서 내지 않나: 순수 Client는 "서버가 발사를 취소했는가"를 알 수 없어(그 정보는 서버에만 있다)
            //   marker에서 총구 VFX를 내면 취소된 공격에도 VFX가 난다(2026-10-20 단계 A 실기에서 확인).
            //   서버가 발사하지 않은 공격에는 발사 기록이 없으므로 싱글 · Host · Client 모두 "투사체 · 소리 없음"이 같게 지켜진다.
            //   차지 marker(chargeOnly)는 이 분기와 무관하게 기존 차지 VFX 규칙(규칙 17)을 그대로 따른다.
            // ────────────────────────────────────────────────────────────────
            bool serverProjectileMarker = !chargeOnly
                && ServerProjectileDeliveryPolicy.IsProjectileUnit(_unitData.Type);
            if (!chargeOnly)
                ServerProjectileDiagnostics.RecordProjectileUnitMarker(_unitData.Type, _unitData.Id,
                    sourceOnlyPresentation, muzzleAtMarker: false);
            // [2026-10-21 단계 D — 차지 유닛 InfernoSpirit · FoxMagician도 여기서 끝난다]
            //   · 타격(OnAttackHit) marker: 위에서 lease를 소비했고(차지 marker는 확인만 — 규칙 17 차지 계약 그대로),
            //     차지 VFX를 다시 켜지 않으며(P2-4), 트레이서·발사음도 여기서 내지 않는다.
            //   · 발사음은 서버 발사 기록이 화면에 나오는 순간 PlayServerProjectileLaunchCue가 낸다(차지 유닛은 총구 VFX 없이 소리만).
            //   · 차지 VFX는 프리팹 길이 = 차지 구간(Inferno 1.3333초, Fox 1.8333초)이라 이 타격 순간에 스스로 끝난다.
            //   그래서 "발사 신호 = 서버 발사 기록 표시 시점"(의심 1 수정)과 "차지 = 차지 marker에서만"(차지 계약)이 서로 다른 신호를 맡아 겹치지 않는다.
            if (serverProjectileMarker) return;
            // 여기부터는 "투사체가 아닌 유닛의 타격 marker"와 "차지 유닛의 차지 marker(chargeOnly)"만 온다.
            //   차지 유닛(공격 클립에 OnAttackCharge가 있는 유닛 — 지금 FoxMagician · InfernoSpirit)은 모두 투사체 유닛이라
            //   그 「타격」 marker는 바로 위 return에서 끝난다. 그래서 아래 공격 VFX는 차지 marker에서는 기 모으기(charge) VFX,
            //   비투사체 유닛에서는 종전 공격 VFX로 재생된다.
            //   (P2-4 「차지 유닛 타격 marker에서는 charge를 다시 켜지 않는다」도 위 return이 지킨다.)
            UnitAttackVfxPlaybackResult vfxResult = effectManager != null
                ? effectManager.PlayUnitAttack(_unitData.Type, spawnPos, spawnRot, this)
                : new UnitAttackVfxPlaybackResult(
                    UnitAttackVfxPlaybackStatus.PoolItemUnavailable,
                    0,
                    false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 실제 emitter 호출 직후의 문맥이다. fixture wrapper에서 만들어낸 값이 아니다.
            AttackPhaseLastEmissionScope = markerScope;
            AttackPhaseLastEmissionTarget = presentationVictimId;
            AttackPhaseLastEmissionTargetPosition = nativeMarkerOwner != null ? nativeMarkerOwner.TargetPosition : Vector3.zero;
            AttackPhaseLastEmissionRotation = spawnRot;
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_unitData.Type == UnitType.TorrentSpirit && _torrentPlaybackRows < 24)
            {
                _torrentPlaybackRows++;
                Vector3 direction = spawnRot * Vector3.forward;
                GameLog.Dev.Info("Combat", nameof(UnitView), "[DEBUG-TORRENT-WAVE] local-playback",
                    $"peer={(NetworkContext.IsNetworkActive ? (NetworkContext.IsNetworkServer ? "Host" : "Client") : "Single")}, t={Time.realtimeSinceStartup:F3}, caster={_unitData.Id}, revision={_lastAttackPresentationRevision}, spawnX={spawnPos.x:F3}, spawnZ={spawnPos.z:F3}, dirX={direction.x:F3}, dirZ={direction.z:F3}, manager={effectManager != null}, status={vfxResult.Status}, systems={vfxResult.ParticleSystemCount}, active={vfxResult.PlaybackActive}, noWaveId=true");
            }
            if (_unitData.Type == UnitType.StreamSpirit)
            {
                string streamOutcome = effectManager == null
                    ? "manager-unavailable"
                    : vfxResult.Status == UnitAttackVfxPlaybackStatus.Started
                        ? "started"
                        : vfxResult.Status.ToString();
                UnitAttackShadowObserver.RecordStreamSpiritTimelineVfx(
                    _unitData.Id,
                    _unitData.Type,
                    _lastAttackPresentationRevision,
                    _attackPresentationImpactMode,
                    streamOutcome,
                    vfxResult.ParticleSystemCount,
                    vfxResult.PlaybackActive);
            }
            // [P2-5] Fox VFX 결과 기록도 차지 marker일 때만(타격 marker는 charge를 켜지 않는다 — P2-4).
            //   옛 조건: if (_unitData.Type == UnitType.FoxMagician)
            if (chargeOnly && _unitData.Type == UnitType.FoxMagician)
            {
                string foxOutcome = effectManager == null
                    ? "manager-unavailable"
                    : vfxResult.Status == UnitAttackVfxPlaybackStatus.Started
                        ? "started"
                        : vfxResult.Status.ToString();
                UnitAttackShadowObserver.RecordFoxMagicianTimelineVfxResult(
                    _unitData.Id,
                    _unitData.Type,
                    _lastAttackPresentationRevision,
                    _attackPresentationImpactMode,
                    foxOutcome,
                    vfxResult.ParticleSystemCount,
                    vfxResult.PlaybackActive);
            }
            // [P2-4] Inferno 공격 VFX 시도 기록은 charge를 실제로 켜는 marker(차지 marker)에서만 남긴다.
            //   Inferno의 타격 marker는 투사체 유닛이라 위 serverProjectileMarker return에서 이미 끝났으므로
            //   여기 오는 Inferno marker는 차지 marker뿐이다(타격 marker를 「실패한 시도」로 세지 않는다).
            if (_unitData.Type == UnitType.InfernoSpirit)
            {
                bool managerAvailable = effectManager != null;
                bool presetAvailable = managerAvailable
                    && vfxResult.Status != UnitAttackVfxPlaybackStatus.PresetUnavailable;
                bool prefabAvailable = presetAvailable
                    && vfxResult.Status != UnitAttackVfxPlaybackStatus.PrefabUnavailable;
                bool poolItemAvailable = prefabAvailable
                    && vfxResult.Status != UnitAttackVfxPlaybackStatus.PoolItemUnavailable;
                string outcome = !managerAvailable
                    ? "manager-unavailable"
                    : vfxResult.Status == UnitAttackVfxPlaybackStatus.Started
                        ? "started"
                        : vfxResult.Status.ToString();
                UnitAttackShadowObserver.RecordInfernoAttackVfxAttempt(
                    _unitData.Id,
                    _attackPresentationImpactMode,
                    consumedValidScope,
                    sourceOnlyPresentation,
                    markerScope,
                    outcome,
                    managerAvailable,
                    presetAvailable,
                    prefabAvailable,
                    poolItemAvailable,
                    vfxResult.ParticleSystemCount,
                    vfxResult.PlaybackActive,
                    spawnPos);
            }
#endif
            PistoleerTimingDiagnostics.Event(_unitData, "VfxResult", sourceOnlyPresentation ? _pistoleerTimingScope : markerScope,
                UnitAttackResultPresentationShadowBridge.CurrentServerTime,
                $"status={vfxResult.Status} systems={vfxResult.ParticleSystemCount} active={(vfxResult.PlaybackActive ? 1 : 0)} manager={(effectManager != null ? 1 : 0)}");
            if (_attackPhase.Active) AttackPhaseNativeEmissions++;
            // [2026-10-19 차지 유닛 발사 시점] 차지 유닛은 여기서 공격 SFX를 내지 않는다(사용자 결정 「발사음은 발사 순간」).
            //   · 차지 marker: SFX 없음 — 차지 SFX는 추후 SFX 작업에서 다룬다(규칙 15 「하나만 재생하면 나머지를 주석으로 명시」).
            //   · 타격 marker: 차지 유닛은 모두 투사체 유닛이라 위 serverProjectileMarker return에서 끝나고,
            //     발사음은 서버 발사 기록이 화면에 나오는 순간 PlayServerProjectileLaunchCue가 낸다.
            //   비차지 유닛은 종전 그대로 이 줄에서 공격 VFX와 짝으로 SFX를 낸다.
            if (!_hasAttackChargeMarker)
                AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);                // SFX (규칙 15 — VFX와 짝)

            // 일반 Stop이 marker보다 먼저 도착한 경우 서버가 이미 커밋한 공격자의 발사
            // VFX/SFX까지만 마친다. 타겟은 이미 사망·교체될 수 있으므로 Tracer와 로컬 피격
            // 신호를 만들지 않고 여기서 종료한다.
            // [P2-4 — 사용자 결정 D2] 차지 유닛의 타격 marker는 위 serverProjectileMarker return에서 이미 끝났고,
            //   차지 marker(chargeOnly)는 SFX를 내지 않은 채 이 return에서 끝나 트레이서로 내려가지 않는다.
            //   「차지 도중 공격이 끊기면 투사체·발사음 없음」은 서버가 발사를 취소하면 발사 기록 자체가 없다는 사실로 지켜진다
            //   (차지 VFX 정지는 StopOwnedChargeEffects · ApplyServerProjectileLaunchCancel).
            // [P4] 옛 설명 「charge는 허가와 source VFX/SFX까지만 사용한다」 → 이제 charge는 허가 「확인」과 charge VFX만 쓴다(SFX 없음).
            //   recipient 관측·피격 신호·트레이서 진입보다 먼저 종료하므로 차지 표식이 피해 결과를 방출하지 않는다.
            if (chargeOnly || sourceOnlyPresentation) return;

            // 아래 원거리 분기(로컬 트레이서 + 로컬 피격 신호)는 "투사체가 아닌 유닛"만 쓴다(사거리가 RangedAttackThreshold 이상이면 원거리 분기, 아니면 근접 분기).
            //   투사체 유닛은 위 serverProjectileMarker return에서 끝났다 — 투사체는 서버 발사 기록으로 그린다.

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
                    nativeMarkerOwner != null && nativeMarkerOwner.Lease != _attackOccurrenceLease
                        ? null : GetPresentationTransform(nativeMarkerOwner != null ? nativeMarkerOwner.Target : _combatTargetTransform);
                Vector3 targetPos = nativeMarkerOwner != null && nativeMarkerOwner.Lease != _attackOccurrenceLease
                    ? nativeMarkerOwner.TargetPosition : presentationTarget != null
                    ? presentationTarget.position
                    : GetTargetPresentationWorldPos(
                        presentationVictimId,
                        presentationVictimIsUnit);

                if (authoritativePresentation)
                {
                    // 착탄 콜백에는 결과 권한이 없다. 표시 시각이 이미 지났다면 구 탄환을
                    // 다시 재생해 피격 표현을 늦추지 않는다. 결과는 Coordinator가 독립 방출한다.
                    double impactTime = hasConfirmedImpact ? confirmedImpact.ImpactServerTime
                        : committedSchedule.ImpactServerTime;
                    double remaining = impactTime + AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds
                        - UnitAttackResultPresentationShadowBridge.CurrentServerTime;
                    PistoleerTimingDiagnostics.Event(_unitData, "TracerDecision", markerScope,
                        UnitAttackResultPresentationShadowBridge.CurrentServerTime,
                        $"impact={PistoleerTimingDiagnostics.Number(impactTime)} delay={PistoleerTimingDiagnostics.Number(AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds)} remaining={PistoleerTimingDiagnostics.Number(remaining)} call={(committedSchedule.IsValid && remaining > 0d ? 1 : 0)}");
                    if (committedSchedule.IsValid && remaining > 0d)
                        em.PlayTracer(_unitData.Type, spawnPos, targetPos, null, (float)remaining);
                    return;
                }

                // 트레이서 발사. 착탄 콜백에서 OnLocalAttackHit을 발행하여 피격 연출을 착탄 시점에 방출한다.
                //   트레이서 프리셋이 없으면 PlayTracer가 콜백을 "즉시" 실행하므로 기존 즉시 방출로 폴백된다.
                //   (2026-10-27 기준 UnitEffectConfig에서 트레이서 프리셋이 있는 유닛은 투사체 유닛 7종뿐이라, 여기 오는 유닛은 폴백으로 즉시 방출된다.)
                em.PlayTracer(_unitData.Type, spawnPos, targetPos,
                    () =>
                    {
                        ObservePresentationShadowMarker(
                            LegacyPresentationObservationKind.TracerImpact,
                            PresentationTransform,
                            markerScope,
                            presentationVictimId,
                            presentationVictimIsUnit,
                            markerReplicatedSnapshot);
                        GameEvents.OnLocalAttackHit.OnNext(
                            new LocalAttackHitPresentationEvent(
                                attackerId,
                                markerScope,
                                markerReplicatedSnapshot));
                    });
            }
            else
            {
                if (authoritativePresentation) return;
                // 근접 유닛(또는 EffectManager 부재 시): 발사 순간 = 타격 순간이므로 즉시 신호를 발행한다.
                // HitPresentationQueue가 이 신호를 받아 해당 공격자의 보류 큐에서 피격 연출 1건을 방출한다.
                // 모든 클라이언트에서 로컬로 실행되므로 각 화면이 자기 애니메이션 타이밍에 맞춰 연출된다.
                // (전투 타격 타이밍 동기화 Phase 2 — 축 3)
                GameEvents.OnLocalAttackHit.OnNext(
                    new LocalAttackHitPresentationEvent(
                        attackerId,
                        markerScope,
                        markerReplicatedSnapshot));
            }
        }

        /// <summary>
        /// C3는 실제 Animation Event가 사용한 화면 방향과 타겟만 읽는다. 이 메서드는 기존
        /// VFX/SFX/Animator/피해 API를 호출하지 않으며 Shadow가 꺼진 경기에서는 no-op이다.
        /// </summary>
        private void ObservePresentationShadowMarker(
            LegacyPresentationObservationKind kind,
            Transform presentationTransform,
            AttackPresentationScope attackScope,
            int victimId,
            bool victimIsUnit,
            AttackPresentationReplicatedSnapshot replicatedSnapshot)
        {
            if (!UnitAttackResultPresentationShadowBridge.IsActive
                || _unitData == null || presentationTransform == null)
                return;
            // 새 mode의 marker는 선택적인 무기 표식이다. 실제 결과 emit 방향과 구분한다.
            if (UnitAttackResultPresentationShadowBridge.OwnsAttacker(_unitData.Id)) return;
            Vector3 forward = presentationTransform.forward;
            if (!ActionDirectionXZ.TryCreate(forward.x, forward.z, out ActionDirectionXZ direction))
                return;
            // Red 화면은 Visual Root가 180도 반전되어 있다. 서버 결과의 AimDirection은
            // Blue 기준 도메인 좌표이므로 화면 forward를 같은 도메인으로 되돌린 뒤 비교한다.
            if (ViewConverter.IsFlipped)
            {
                direction = UnitAttackResultPresentationShadowScheduler.FlipDirection180(direction);
                if (!direction.IsValid) return;
            }
            UnitAttackResultPresentationShadowBridge.ObserveLegacy(
                new LegacyPresentationObservation(
                    kind,
                    _unitData.Id,
                    victimIsUnit ? 1 : 2,
                    victimId,
                    -1,
                    Time.realtimeSinceStartupAsDouble,
                    true,
                    direction,
                    default,
                    attackScope,
                    replicatedSnapshot));
        }

        /// <summary>
        /// NetworkUnit의 현재 원자 Shadow 값을 marker 시점에 값으로 복사한다. 이 snapshot은
        /// 진단 전용이며 Animator·Simulation Root·서버 Aim을 변경하지 않는다.
        /// </summary>
        private AttackPresentationReplicatedSnapshot
            CaptureAttackPresentationReplicatedSnapshot()
        {
            if (!NetworkContext.IsNetworkActive)
                return default;
            NetworkUnit networkUnit = GetComponent<NetworkUnit>();
            if (networkUnit == null)
                return default;
            NetworkUnitActionShadowState state = networkUnit.AttackShadowState;
            return state.AttackerInstanceId != 0UL
                    && state.SequenceId != 0UL
                    && state.Revision != 0UL
                ? new AttackPresentationReplicatedSnapshot(
                    state.AttackerInstanceId,
                    state.SequenceId,
                    state.Revision)
                : default;
        }

        /// <summary>
        /// 실제 Animation Event 순서를 커밋 RPC에 함께 실린 원자 공격 회차에 결속한다.
        /// 별도 NetworkVariable 도착 시점이나 현재 타겟에서 회차를 추측하지 않는다.
        /// </summary>
        private bool TryAcquirePresentationShadowMarkerScope(
            out AttackPresentationScope scope)
        {
            scope = default;
            return _unitData != null
                && _attackPresentationImpactLease.TryConsume(out scope);
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
            _combatUseCase?.CloseSoloAttackPlayback(_unitData, terminal: false);
            if (_unitData != null && _unitData.IsHealer && _healPresentation.Current.Active) return;
            // Walk/Held는 공격 회차가 아니므로 별도 NGO 상태 도착 순서와 무관하게
            // 이전 committed presentation의 로컬 Impact 승인을 먼저 닫는다.
            CloseAttackPhasePresentation("walk-entry");
            // _animator가 null이면 lazy-init 시도 (Initialize() 전에 상태 값이 적용되는 경우 대응 — 스폰 레이스)
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            if (_isMovementHeldAnim && !_isFrozenAnim)
            {
                HoldMovementAnimation();
                return;
            }

            _animator.speed = _isFrozenAnim ? 0f : 1f;
            _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
            _currentAnimStateHash = StateWalk;
        }

        /// <summary>
        /// [Phase 2 신규] 공격(Attack) 애니메이션 루프를 재생한다 — CrossFade만 수행.
        ///
        /// Attack 클립은 Loop Time=ON이므로 1회 CrossFade로 무한 루프한다. 현재 멀티플레이
        /// production은 별도 NetworkVariable에서 이 메서드를 호출하지 않고, target/revision/
        /// impact mode를 함께 가진 StartCombatAnimation 경계에서만 클립을 시작한다.
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
            if (_unitData != null && _unitData.IsHealer && _healPresentation.Current.Active) return;
            if (_animator == null) return;
            if (_isMovementHeldAnim && !_isFrozenAnim)
            {
                HoldMovementAnimation();
                return;
            }

            _animator.speed = _isFrozenAnim ? 0f : 1f;
            if (_currentAnimStateHash == StateWalk) return; // 이미 Walk — 재-CrossFade로 블렌드 리셋 방지
            _combatUseCase?.CloseSoloAttackPlayback(_unitData, terminal: false);
            _animator.CrossFadeInFixedTime(StateWalk, _attackToWalkBlend, 0);
            _currentAnimStateHash = StateWalk;
        }

        /// <summary>
        /// 순수 클라이언트가 서버의 Frozen 애니메이션 상태를 적용할 때 호출한다.
        /// 현재 걷기 포즈를 유지한 채 재생 속도만 0으로 만든다.
        /// </summary>
        public void FreezeWalkAnimation()
        {
            CloseAttackPhasePresentation("frozen-priority");
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
            _combatUseCase?.CloseSoloAttackPlayback(_unitData, terminal: false);
            CloseAttackPhasePresentation("movement-held");
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
            if (!NetworkContext.IsNetworkActive)
                _combatUseCase?.ObserveSoloAttackPause(_unitData, Time.timeAsDouble, frozen || _isMovementHeldAnim);
            if (frozen) CloseAttackPhasePresentation("frozen-priority");

            if (_animator != null)
                _animator.speed = frozen || _isMovementHeldAnim ? 0f : 1f;

            if (NetworkContext.IsNetworkActive && _unitData != null)
                GameEvents.OnUnitFreezeChanged.OnNext(
                    new UnitFreezeChangedEvent(_unitData.Id, frozen));
        }

        private void SetMovementHeldAnimation(bool held)
        {
            bool stateChanged = _isMovementHeldAnim != held;
            _isMovementHeldAnim = held;
            if (!NetworkContext.IsNetworkActive)
                _combatUseCase?.ObserveSoloAttackPause(_unitData, Time.timeAsDouble, held || _isFrozenAnim);

            if (held && !_isFrozenAnim)
            {
                // 다른 애니메이션 경로가 같은 Held 구간에서 speed를 다시 1로 만들 수 있다.
                // 상태 bool이 이미 true여도 실제 Animator를 재검증하여 정지 계약을 복구한다.
                if (_animator != null
                    && _currentAnimStateHash == StateWalk
                    && _animator.speed <= 0.0001f)
                {
                    _animator.speed = 0f;
                }
                else
                {
                    HoldMovementAnimation();
                }
            }
            else if (_animator != null)
                _animator.speed = held || _isFrozenAnim ? 0f : 1f;

            if (stateChanged
                && NetworkContext.IsNetworkActive
                && _unitData != null)
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

        private void SetCombatTargetTracking(int targetId, bool targetIsUnit)
        {
            _combatTargetTransform = GetTargetTransform(targetId, targetIsUnit);
            _combatTargetId = targetId;
            _combatTargetIsUnit = targetIsUnit;
        }

        /// <summary>
        /// 로컬 marker lease만 닫으면 HitPresentationQueue에 먼저 도착한 signal-only 상태가
        /// 남아 늦은 구회차 결과를 방출할 수 있다. 그래서 Close 직전에 정확한 instance/sequence를
        /// 표현 큐에 알린다. 이 이벤트는 HP·피해·서버 Root를 쓰지 않는 로컬 표현 수명 신호다.
        /// </summary>
        private void RetireAndCloseAttackPresentationScope(string reason = "explicit-scope-close")
        {
            if (reason == "presentation-revision-replaced" && TryRetainContinuousNativeOwner())
            {
                // 이전 exact owner는 아직 유효하다. target queue의 Retire 없이 새 cursor만 연다.
                _attackPresentationImpactLease.Close();
                _attackPresentationSourceMarkerLease.Close();
                _attackPresentationImpactMode = AttackPresentationImpactMode.Suppressed;
                return;
            }
            // [P2-6 — 2026-10-19 차지 유닛 발사 시점, 사용자 결정 D2] 차지 도중 공격이 끊기면 차지 VFX를 즉시 끈다.
            //   이 함수가 lease를 닫으면 아직 오지 않은 타격 marker는 Suppressed가 되어 투사체·발사음을 내지 않는다.
            //   그러니 켜져 있던 차지도 발사 없이 남지 않도록 같은 경계에서 끈다.
            //   호출 경계(모두 「진짜 취소/종료」): target-tracking-clear(Stop·전투 루프 종료·이동 종료·초기화),
            //   combat-target-change(멀티 타겟 교체), provisional-suppression, scope-arm-rejected,
            //   CloseAttackPhasePresentation 경유(walk-entry·frozen-priority·movement-held·separate-heal-entry·attacker-death 등).
            //   🔴 예외 "presentation-revision-replaced": 매 공격 회차의 정상 커밋(StartCombatAnimation)이 부르는 경계다.
            //   이때는 곧바로 새 회차 lease가 장전되어 이번 루프의 타격 marker가 그 lease로 정상 발사하므로 끄지 않는다.
            //   사유 토큰은 받은 reason을 그대로 넘긴다(문자열을 이어 붙이지 않아 비차지 유닛에 할당이 생기지 않는다).
            if (reason != "presentation-revision-replaced")
                StopOwnedChargeEffects(reason);
            ClosePendingNativeOwners(reason, false);
            var retiredOwner = _attackPhaseEntryTransaction ? _attackPhaseRetiringOwner : _attackPhase;
            PistoleerTimingDiagnostics.Event(_unitData, "Retire", _pistoleerTimingScope,
                UnitAttackResultPresentationShadowBridge.CurrentServerTime, $"reason={reason} epoch={retiredOwner.EpochId} generation={retiredOwner.Lifetime} sourceOnly=0 remaining={_attackOccurrenceLease.Remaining}");
            ObserveNativeLease("retire-before", _pistoleerTimingScope, _attackPhaseClockHighWater);
            _attackOccurrenceLease.Close();
            ObserveNativeLease("retire-after", _pistoleerTimingScope, _attackPhaseClockHighWater);
            if (NetworkContext.IsNetworkActive
                && _unitData != null
                && _attackPresentationImpactLease.TryGetSequenceScope(
                    out AttackPresentationScope retiredScope))
            {
                GameEvents.OnLocalAttackPresentationScopeRetired.OnNext(
                    new LocalAttackPresentationScopeRetiredEvent(
                        _unitData.Id,
                        retiredScope));
            }
            _attackPresentationImpactLease.Close();
            _attackPresentationSourceMarkerLease.Close();
            _attackPresentationImpactMode = AttackPresentationImpactMode.Suppressed;
        }

        /// <summary>
        /// 일반 Stop이 먼저 도착해도 이미 커밋된 공격자 발사 marker는 남은 횟수만큼만
        /// SourceOnly로 보존한다. exact target scope는 즉시 retire하므로 사망한 타겟이나
        /// 새 타겟으로 Tracer·피격 표현이 이전되지 않는다.
        /// </summary>
        private void PreserveCommittedSourceMarkerAndCloseTargetImpact()
        {
            // [P2-6 — 사용자 결정 D2] 일반 Stop(멀티)이 타격 marker보다 먼저 왔다 = 차지 도중 공격이 끊겼다.
            //   남은 marker는 SourceOnly가 되며, 차지 유닛은 SourceOnly 타격에서 VFX·SFX·트레이서를 하나도 내지 않는다(P2-4).
            //   그래서 켜져 있던 차지 VFX도 지금 즉시 끈다. 비차지 유닛은 StopOwnedChargeEffects 안에서 아무 일도 하지 않는다.
            StopOwnedChargeEffects("AttackCancel-SourceOnlyStop");
            ClosePendingNativeOwners("source-only-stop", true);
            ObserveNativeLease("preserve-before", _pistoleerTimingScope, _attackPhaseClockHighWater);
            _attackOccurrenceLease.PreserveSourceOnly();
            ObserveNativeLease("preserve-after", _pistoleerTimingScope, _attackPhaseClockHighWater);
            PistoleerTimingDiagnostics.Event(_unitData, "SourceStop", _pistoleerTimingScope,
                UnitAttackResultPresentationShadowBridge.CurrentServerTime, "sourceOnly=1 noTracer=1");
            if (NetworkContext.IsNetworkActive
                && _unitData != null
                && _attackPresentationImpactLease.TryGetSequenceScope(
                    out AttackPresentationScope retiredScope))
            {
                GameEvents.OnLocalAttackPresentationScopeRetired.OnNext(
                    new LocalAttackPresentationScopeRetiredEvent(
                        _unitData.Id,
                        retiredScope));
            }
            _attackPresentationImpactLease.Close();
            _attackPresentationSourceMarkerLease.PreserveSourceOnly();
            _attackPresentationImpactMode = AttackPresentationImpactMode.Suppressed;
        }

        private void ClearCombatTargetTracking(bool preserveCommittedSourceMarker = false)
        {
            // 현재 타겟을 잃은 뒤 예전 회차 marker가 새 타겟 또는 victim=-1로 방출되지 않게
            // 아직 소비되지 않은 로컬 표현 lease도 같은 경계에서 닫는다. 이미 발사된 원거리
            // Tracer는 OnAttackHit에서 값으로 캡처한 scope/피해자를 계속 사용한다.
            // [P2-6] 차지 VFX 정지는 아래 두 함수(Preserve…/Retire…) 안에서 한다 — 두 경우 모두 남은 타격 marker가 투사체를 쏘지 않기 때문이다.
            if (preserveCommittedSourceMarker)
                PreserveCommittedSourceMarkerAndCloseTargetImpact();
            else
                RetireAndCloseAttackPresentationScope("target-tracking-clear");
            _combatTargetTransform = null;
            _combatTargetId = -1;
            _combatTargetIsUnit = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            ClearInfernoAttackFacingEvidenceState();
#endif
        }

        /// <summary>
        /// 서버 gameplay 루프가 공격 사거리 진입을 확정한 경계다. ClientRpc는 지연될 수 있으므로
        /// Simulation Root 행동 소유권은 이 로컬 서버 경계에서 먼저 열고 타겟도 함께 설치한다.
        /// </summary>
        private void BeginServerActionRotation(int targetId, bool targetIsUnit)
        {
            _rootRotationOwnership.TransferToAction();
            SetCombatTargetTracking(targetId, targetIsUnit);
            // 여기서 Root를 즉시 타겟 방향으로 스냅하지 않는다. Update의 서버 전용
            // RotateTowards(270°/s)가 Action 소유권 아래에서 유일하게 점진 회전한다.
            // 공격 시작은 NetworkCombatController가 실제 pose 5° 진입을 확인한 뒤 수행한다.
        }

        /// <summary>
        /// Chase의 마지막 이동 위치가 Root에 쓰이기 전에 서버 Action 소유권을 넘기고,
        /// target·revision·impact suppression을 포함한 provisional Start를 발행한다.
        /// 이벤트 구독자가 RPC enqueue까지 완료했다는 ACK가 돌아오지 않으면 Action 소유권을
        /// 즉시 이동에 반환하므로 이번 frame은 Root/NoIntent를 커밋하지 않고 다음 frame에 재시도한다.
        /// </summary>
        private bool TryBeginServerAttackEntryBeforeRootStop(
            int targetId,
            bool targetIsUnit)
        {
            if (!NetworkContext.IsNetworkActive
                || !NetworkContext.IsNetworkServer
                || _unitData == null
                || !_unitData.IsAlive)
                return false;

            ulong serialBefore = _serverAttackEntryHandoffSerial;
            BeginServerActionRotation(targetId, targetIsUnit);
            if (!_rootRotationOwnership.ActionOwnsRoot)
                return false;

            // Subject는 동기 호출이다. NetworkCombatController가 Host 로컬 적용과 원격 RPC
            // enqueue를 마친 뒤 ConfirmServerAttackEntryHandoff를 호출해야 serial이 바뀐다.
            GameEvents.OnUnitEnteredCombat.OnNext(new UnitEnteredCombatEvent(
                _unitData.Id,
                targetId,
                targetIsUnit));
            bool completed = _serverAttackEntryHandoffSerial != serialBefore;
            if (!completed)
            {
                EndServerActionRotation();
                _rootRotationOwnership.TryAcquireMovement();
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            else
            {
                UnitMovementAuthorityObserver
                    .ObserveCombatPresentationHandoffSuccess(_unitData.Id);
            }
#endif
            return completed;
        }

        /// <summary>
        /// 서버 전투 컨트롤러가 provisional Start의 Host 적용과 원격 RPC enqueue를 모두
        /// 마친 뒤 호출하는 동기 ACK다. Client gameplay/Root writer를 만들지 않는다.
        /// </summary>
        public bool ConfirmServerAttackEntryHandoff(ulong presentationRevision)
        {
            if (!NetworkContext.IsNetworkActive
                || !NetworkContext.IsNetworkServer
                || _unitData == null
                || !_unitData.IsAlive
                || !_rootRotationOwnership.ActionOwnsRoot
                || presentationRevision == 0UL
                || _serverAttackEntryHandoffSerial == ulong.MaxValue)
                return false;

            _serverAttackEntryHandoffSerial++;
            return true;
        }

        /// <summary>
        /// 완료 receipt가 가리키는 Host 공격 표현이 같은 타겟과 revision으로 계속 유효한지
        /// 읽기만 한다. Animator, Root, target 또는 lease를 변경하지 않는다.
        /// </summary>
        public bool IsServerCombatPresentationSatisfied(
            int targetId,
            bool targetIsUnit,
            ulong presentationRevision)
        {
            return NetworkContext.IsNetworkActive
                && NetworkContext.IsNetworkServer
                && _unitData != null
                && _unitData.IsAlive
                && _rootRotationOwnership.ActionOwnsRoot
                && _currentAnimStateHash == StateAttack
                && _combatTargetId == targetId
                && _combatTargetIsUnit == targetIsUnit
                && _lastAttackPresentationRevision >= presentationRevision;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>
        /// 실제 순수 Client 화면에서 Simulation Root의 frame별 이동과 현재 Animator 표시를
        /// 함께 관측한다. 진단 전용이며 Transform이나 Animator 값을 변경하지 않는다.
        /// </summary>
        private void ObserveClientAttackEntryPresentationFrame()
        {
            if (!NetworkContext.IsNetworkActive
                || NetworkContext.IsNetworkServer
                || _unitData == null)
                return;

            if (_movementNetworkUnit == null)
                _movementNetworkUnit = GetComponent<NetworkUnit>();
            if (_movementNetworkUnit == null)
                return;

            UnitMovementAuthorityObserver.ObserveClientAttackPresentationFrame(
                _unitData.Id,
                _movementNetworkUnit.NetworkObjectId,
                transform.position,
                walkPresentationVisible: _currentAnimStateHash == StateWalk,
                attackPresentationVisible: _currentAnimStateHash == StateAttack,
                movementPhase: _movementNetworkUnit.MovementPhase,
                commandRevision: _movementNetworkUnit.MovementCommandRevision,
                segmentRevision: _movementNetworkUnit.MovementSegmentRevision,
                semanticRevision: _movementNetworkUnit.MovementSemanticRevision);
        }
#endif

        /// <summary>
        /// 서버 gameplay 전투 루프가 종료를 확정하는 유일한 소유권 반환 경계다.
        /// 네트워크 Stop 이벤트보다 먼저 로컬 상태를 닫아 지연 이벤트 순서에 의존하지 않는다.
        /// </summary>
        private void EndServerActionRotation()
        {
            ClearCombatTargetTracking(
                preserveCommittedSourceMarker: NetworkContext.IsNetworkActive);
            ClearUnitActionShadowDesiredTarget();
            bool resumeMovement = _moveCoroutine != null
                && _unitData != null
                && _unitData.IsAlive;
            _rootRotationOwnership.ReleaseAction(resumeMovement);
        }

        // ====================================================================
        // 전투 애니메이션 — 상태 기반 (RPC/이벤트에서 호출)
        // ====================================================================

        /// <summary>
        /// 전투 시작. 서버가 이미 5도 정렬을 확인한 타겟을 보존하고 Attack CrossFade한다.
        /// Attack 클립은 Loop Time=ON이므로 별도 루프 로직 불필요 — CrossFade 1회 호출로 무한 루프.
        ///
        /// 호출 경로:
        ///   멀티플레이 → NetworkCombatController.StartCombatClientRpc → UnitView.StartCombatAnimation
        ///   싱글플레이 → GameEvents.OnCombatStarted → UnitView.StartCombatAnimation
        /// </summary>
        /// <param name="targetId">타겟 엔티티의 Id.</param>
        /// <param name="targetIsUnit">true=유닛, false=건물.</param>
        // Diagnostic fields never feed Animator, damage, lease admission or network state.
        private AttackPresentationScope _pistoleerTimingScope;
        private ulong _pistoleerContinuity;
        private double _pistoleerRenderedAnchor = double.NaN;
        private double _pistoleerAnchorNormalized = double.NaN;
        private double _pistoleerAnchorLength = double.NaN;
        private double _pistoleerAnchorSpeed = double.NaN;
        private bool _pistoleerRenderedPending;

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObservePistoleerRenderedStart()
        {
            if (!_pistoleerRenderedPending || _unitData == null || !PistoleerTimingDiagnostics.Observes(_unitData.Type) || _animator == null) return;
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != StateAttack) return;
            _pistoleerRenderedPending = false;
            _pistoleerRenderedAnchor = Time.realtimeSinceStartupAsDouble;
            _pistoleerAnchorNormalized = state.normalizedTime;
            // OLD: state.length was speed-dependent (Infinity while paused), not clip length.
            // _pistoleerAnchorLength = state.length;
            _pistoleerAnchorLength = _attackPhaseClip != null ? _attackPhaseClip.length : double.NaN;
            _pistoleerAnchorSpeed = _animator.speed * state.speed * state.speedMultiplier;
            PistoleerTimingDiagnostics.Event(_unitData, "RenderedStart", _pistoleerTimingScope,
                UnitAttackResultPresentationShadowBridge.CurrentServerTime,
                $"continuity={_pistoleerContinuity} normalized={PistoleerTimingDiagnostics.Number(state.normalizedTime)} length={PistoleerTimingDiagnostics.Number(_pistoleerAnchorLength)} stateLength={PistoleerTimingDiagnostics.Number(state.length)} paused={(_animator.speed == 0 ? 1 : 0)} stateRate={PistoleerTimingDiagnostics.Number(state.speed * state.speedMultiplier)} speed={PistoleerTimingDiagnostics.Number(_animator.speed)}");
        }

        public void StartCombatAnimation(
            int targetId,
            bool targetIsUnit,
            bool restartAttackCycle = false,
            ulong presentationRevision = 0UL,
            AttackPresentationImpactMode impactMode = AttackPresentationImpactMode.Suppressed,
            AttackPresentationScope presentationScope = default)
        {
            if (_unitData == null || !_unitData.IsAlive) return;

            if (NetworkContext.IsNetworkActive)
            {
                if (!UnitAttackPresentationPolicy.TryAcceptRevision(
                        presentationRevision,
                        _lastAttackPresentationRevision,
                        out ulong acceptedRevision))
                    return;
                _lastAttackPresentationRevision = acceptedRevision;

                // Animation state/CrossFade보다 먼저 같은 RPC payload의 표현 모드를 적용한다.
                // 최초 Align provisional과 늦은 참가 기본 상태는 Suppressed이므로, 네트워크 상태
                // 콜백이 RPC보다 먼저 Attack 클립을 재생해도 marker가 gameplay/VFX를 내지 않는다.
                // Scoped만 원자 scope와 프로필 HitIndex 수로 일회성 lease를 연다.
                // LegacyFallback은 미지원 타입의 기존 표현만 보존하며 가짜 scope를 만들지 않는다.
                int impactCount = _unitData.HitFrameTimes != null
                    ? _unitData.HitFrameTimes.Length
                    : 0;
                RetireAndCloseAttackPresentationScope("presentation-revision-replaced");
                if (impactMode == AttackPresentationImpactMode.Scoped)
                {
                    bool scopeArmed =
                        _attackPresentationImpactLease.TryArm(presentationScope, impactCount);
                    bool sourceArmed =
                        _attackPresentationSourceMarkerLease.TryArm(
                            presentationRevision,
                            impactCount);
                    _attackPresentationImpactMode = scopeArmed && sourceArmed
                        ? AttackPresentationImpactMode.Scoped
                        : AttackPresentationImpactMode.Suppressed;
                    if (_attackPresentationImpactMode == AttackPresentationImpactMode.Suppressed)
                        RetireAndCloseAttackPresentationScope("scope-arm-rejected");
                }
                else if (impactMode == AttackPresentationImpactMode.LegacyFallback)
                {
                    // Unresolved 타입은 runtime marker/config가 아직 완전히 일치하지 않을 수
                    // 있으므로 최소 한 번의 기존 marker를 보존하되 commit당 bounded lease로 제한한다.
                    int legacyMarkerCount = impactCount > 0 ? impactCount : 1;
                    _attackPresentationImpactMode =
                        _attackPresentationSourceMarkerLease.TryArm(
                            presentationRevision,
                            legacyMarkerCount)
                            ? AttackPresentationImpactMode.LegacyFallback
                            : AttackPresentationImpactMode.Suppressed;
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitAttackShadowObserver.RecordStreamSpiritTimelineStart(
                    _unitData.Id,
                    _unitData.Type,
                    targetId,
                    targetIsUnit,
                    presentationRevision,
                    _attackPresentationImpactMode);
                UnitAttackShadowObserver.RecordFoxMagicianTimelineStart(
                    _unitData.Id,
                    _unitData.Type,
                    targetId,
                    targetIsUnit,
                    presentationRevision,
                    _attackPresentationImpactMode);
#endif
            }

            // 싱글플레이는 이 이벤트가 gameplay 경계이므로 여기서 Action을 연다. 멀티 서버는
            // gameplay 루프가 이미 연 소유권만 인정하고, 지연된 RPC가 이동 상태를 공격 상태로
            // 되살리는 것은 거부한다. 순수 클라이언트는 표시용 타겟을 항상 보존한다.
            if (!NetworkContext.IsNetworkActive)
                _rootRotationOwnership.TransferToAction();

            bool acceptTargetEvent =
                UnitActionRotationEventPolicy.ShouldAcceptTargetEvent(
                    NetworkContext.IsNetworkActive,
                    NetworkContext.IsNetworkServer,
                    _rootRotationOwnership.ActionOwnsRoot);
            if (acceptTargetEvent)
            {
                SetCombatTargetTracking(targetId, targetIsUnit);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                SetInfernoAttackFacingIdentity(
                    targetId,
                    targetIsUnit,
                    presentationRevision,
                    presentationScope);
#endif
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            else if (NetworkContext.IsNetworkServer)
                UnitMovementAuthorityObserver.ObserveIgnoredServerTargetEvent(
                    _unitData.Id,
                    "start");
#endif

            // Start 이벤트는 이미 정렬이 완료된 공격 시작 경계다. 이 자리에서 Root를 다시
            // 스냅하면 270°/s 서버 회전 writer와 충돌하므로 타겟 참조만 갱신한다.
            // 싱글플레이는 아직 멀티 서버의 pose gate를 사용하지 않으므로 기존 즉시 정렬을
            // 보존한다. 이번 교정 범위 밖의 모드에서 공격 전에 빗겨 보이는 회귀를 만들지 않는다.
            if (!NetworkContext.IsNetworkActive && acceptTargetEvent)
            {
                Vector3 targetPosition = GetTargetWorldPos(targetId, targetIsUnit);
                if (targetPosition != Vector3.zero)
                {
                    float angle = CalculateAttackAngle(targetPosition);
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }
            }

            // ────────────────────────────────────────────────────────────────
            // Attack CrossFade 책임 분리.
            //   멀티플레이 Attack은 상태 레벨과 별도로 target/revision/impact mode가 모두
            //   필요한 표현 명령이다. 따라서 순수 클라이언트도 이 원자 명령에서 CrossFade한다.
            //   별도 _animState=Attack은 clip을 시작하지 않아 먼저/나중 어느 쪽으로 도착해도 안전하다.
            //   싱글플레이와 호스트도 같은 메서드에서 직접 CrossFade한다:
            //     - 싱글플레이: NGO가 없어 레벨 동기화 경로가 없으므로 직접 재생해야 한다.
            //     - 호스트(서버): 서버가 Animator를 직접 제어하는 기존 관례를 유지한다
            //       (서버가 애니메이션을 직접 제어하는 다른 경로들과 동일한 취지).
            // ────────────────────────────────────────────────────────────────
            bool applyCrossFadeHere = !NetworkContext.IsNetworkActive
                || (NetworkContext.IsNetworkServer && acceptTargetEvent)
                // 순수 클라이언트 Attack은 별도 NetworkVariable callback이 아니라
                // impact mode와 같은 RPC payload가 직접 시작한다. provisional/commit
                // 모두 이 경계를 통과하므로 채널 간 도착 순서에 의존하지 않는다.
                || (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer);
            // Scope retirement/arming above precedes native binding, which precedes
            // actual CrossFade evaluation. The same transaction is used on Host/Client.
            if (_attackPhase.Active && _attackPhase.PresentationRevision == presentationRevision
                && _attackPhaseNetworkUnit != null
                && !double.IsNaN(_attackPhaseApplyClock))
            {
                if (presentationScope.IsValid) PistoleerTimingDiagnostics.Register(_unitData, presentationScope, presentationRevision);
                BindNativeAttackOccurrence(_attackPhaseApplyClock, _attackPhase.Lifetime);
            }
            bool attackPresentationAlreadyActive = _currentAnimStateHash == StateAttack && !_attackPhaseFirstEntry;
            bool attackCrossFadeApplied = false;
            if (applyCrossFadeHere
                && _animator != null
                && UnitAttackPresentationPolicy.ShouldCrossFadeAttack(
                    NetworkContext.IsNetworkActive,
                    attackPresentationAlreadyActive,
                    restartAttackCycle))
            {
                _animator.speed = 1f;
                double phaseElapsed = 0;
                double phaseNow = double.NaN; ulong phaseGeneration = 0;
                bool phaseClockReady = _attackPhase.Active && !double.IsNaN(_attackPhaseApplyClock);
                if (phaseClockReady) { phaseNow = _attackPhaseApplyClock; phaseGeneration = _attackPhase.Lifetime; }
                if (phaseClockReady) phaseElapsed = phaseNow - AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds - _attackPhase.EpochServerTime;
                float phaseOffset = (float)Math.Max(0d, phaseElapsed);
                _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0, phaseOffset);
                if (!NetworkContext.IsNetworkActive)
                    _combatUseCase?.ConfirmSoloAttackPlayback(_unitData, Time.timeAsDouble);
                if (_attackPhase.Active) PistoleerTimingDiagnostics.Event(_unitData, "NativeEpochApply", _attackPhase.Scope, phaseNow,
                    $"epoch={_attackPhase.EpochId} generation={_attackPhase.Lifetime} epochTime={PistoleerTimingDiagnostics.Number(_attackPhase.EpochServerTime)} offset={PistoleerTimingDiagnostics.Number(phaseOffset)} first=1");
                if (_attackPhase.Active && (!phaseClockReady || phaseElapsed < 0))
                {
                    _attackPhaseSavedSpeed = 1f; _attackPhaseOwnsSpeed = true;
                    _animator.speed = 0f;
                }
                _currentAnimStateHash = StateAttack;
                attackCrossFadeApplied = true;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (PistoleerTimingDiagnostics.Observes(_unitData.Type))
            {
                if (attackCrossFadeApplied)
                {
                    _pistoleerContinuity++;
                    _pistoleerRenderedAnchor = double.NaN;
                    _pistoleerAnchorNormalized = _pistoleerAnchorLength = _pistoleerAnchorSpeed = double.NaN;
                    _pistoleerRenderedPending = true;
                    _pistoleerTimingScope = default;
                }
                if (impactMode == AttackPresentationImpactMode.Scoped && presentationScope.IsValid)
                {
                    _pistoleerTimingScope = presentationScope;
                    PistoleerTimingDiagnostics.Register(_unitData, presentationScope, presentationRevision);
                    var timingState = _animator != null ? _animator.GetCurrentAnimatorStateInfo(0) : default;
                    PistoleerTimingDiagnostics.Event(_unitData, "Command", presentationScope,
                        UnitAttackResultPresentationShadowBridge.CurrentServerTime,
                        $"crossFade={(attackCrossFadeApplied ? 1 : 0)} continuity={_pistoleerContinuity} renderedAnchor={PistoleerTimingDiagnostics.Number(_pistoleerRenderedAnchor)} anchorNormalized={PistoleerTimingDiagnostics.Number(_pistoleerAnchorNormalized)} anchorLength={PistoleerTimingDiagnostics.Number(_pistoleerAnchorLength)} anchorSpeed={PistoleerTimingDiagnostics.Number(_pistoleerAnchorSpeed)} normalized={PistoleerTimingDiagnostics.Number(timingState.normalizedTime)} length={PistoleerTimingDiagnostics.Number(_attackPhaseClip != null ? _attackPhaseClip.length : double.NaN)} stateLength={PistoleerTimingDiagnostics.Number(timingState.length)} paused={(_animator != null && _animator.speed == 0 ? 1 : 0)} stateRate={PistoleerTimingDiagnostics.Number(timingState.speed * timingState.speedMultiplier)} speed={PistoleerTimingDiagnostics.Number(_animator != null ? _animator.speed : double.NaN)} delay={PistoleerTimingDiagnostics.Number(AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds)}");
                }
            }
#endif

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (attackCrossFadeApplied)
            {
                // 새 실제 CrossFade는 이전 미소비 시작 예약을 대체한다. 연속 commit은
                // CrossFade하지 않으므로 기존 pending을 지우거나 가짜 시작 표본을 만들지 않는다.
                _infernoAttackStartFacingPending = false;
                QueueInfernoAttackStartFacing();
            }
#endif

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // committed scope 갱신은 같은 Attack clip을 유지하므로 최초 provisional 전환만
            // client-visible 진입 순서의 끝점으로 기록한다.
            if (NetworkContext.IsNetworkActive
                && !NetworkContext.IsNetworkServer
                && !attackPresentationAlreadyActive
                && _currentAnimStateHash == StateAttack)
            {
                if (_movementNetworkUnit == null)
                    _movementNetworkUnit = GetComponent<NetworkUnit>();
                if (_movementNetworkUnit != null)
                {
                    UnitMovementAuthorityObserver.ObserveClientAttackPresentationStarted(
                        _unitData.Id,
                        _movementNetworkUnit.NetworkObjectId);
                }
            }
#endif

            // 공격 중 타겟 Transform 참조 저장 → Update()에서 매 프레임 추적.
            // [Phase 2] 이 타겟 참조/회전 추적은 애니메이션과 분리해 "유지"한다(클라이언트도 필요):
            //   원거리 유닛의 트레이서 조준(OnAttackHit)이 _combatTargetTransform/_combatTargetId를 사용한다.
            ClearUnitActionShadowDesiredTarget();
        }

        /// <summary>
        /// Host 서버가 gameplay의 Action 회전 소유권을 연 바로 그 호출 스택에서
        /// provisional/commit 공격 표현을 로컬에 적용한다. Host가 자신에게 되돌아오는
        /// ClientRpc 순서에 의존하지 않게 하는 원자 handoff 경계다.
        ///
        /// 이 메서드는 표현만 적용한다. 타겟 선택, 공격 scope 발급, 피해 판정은 계속
        /// NetworkCombatController의 서버 권위 흐름만 소유한다.
        /// </summary>
        public bool TryApplyHostCombatPresentation(
            int targetId,
            bool targetIsUnit,
            bool restartAttackCycle,
            ulong presentationRevision,
            AttackPresentationImpactMode impactMode,
            AttackPresentationScope presentationScope)
        {
            if (!NetworkContext.IsNetworkActive
                || !NetworkContext.IsNetworkServer
                || _unitData == null
                || !_unitData.IsAlive
                || _animator == null
                || !_rootRotationOwnership.ActionOwnsRoot)
                return false;

            StartCombatAnimation(
                targetId,
                targetIsUnit,
                restartAttackCycle,
                presentationRevision,
                impactMode,
                presentationScope);

            return _lastAttackPresentationRevision == presentationRevision
                && _currentAnimStateHash == StateAttack
                && _rootRotationOwnership.ActionOwnsRoot;
        }

        /// <summary>
        /// 서버 공격 gate가 피해 회차를 열기 직전에 확인하는 gameplay 소유권 상태다.
        /// Animator 상태가 아니라 Simulation Root의 Action 소유권만 노출한다.
        /// </summary>
        public bool IsServerCombatActionReady()
            => NetworkContext.IsNetworkActive
                && NetworkContext.IsNetworkServer
                && _unitData != null
                && _unitData.IsAlive
                && _rootRotationOwnership.ActionOwnsRoot;

        /// <summary>
        /// Host의 현재 gameplay Action 소유권 안에서 표시 타겟만 교체한다.
        /// 원격 ClientRpc가 Host로 되돌아오는 것에 의존하지 않는다.
        /// </summary>
        public bool TryApplyServerCombatTarget(int targetId, bool targetIsUnit)
        {
            if (!NetworkContext.IsNetworkActive
                || !NetworkContext.IsNetworkServer
                || _unitData == null
                || !_unitData.IsAlive
                || !_rootRotationOwnership.ActionOwnsRoot)
                return false;

            ChangeTarget(targetId, targetIsUnit);
            return _combatTargetId == targetId
                && _combatTargetIsUnit == targetIsUnit
                && _rootRotationOwnership.ActionOwnsRoot;
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

            bool acceptTargetEvent =
                UnitActionRotationEventPolicy.ShouldAcceptTargetEvent(
                    NetworkContext.IsNetworkActive,
                    NetworkContext.IsNetworkServer,
                    _rootRotationOwnership.ActionOwnsRoot);
            if (!acceptTargetEvent)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (NetworkContext.IsNetworkServer)
                    UnitMovementAuthorityObserver.ObserveIgnoredServerTargetEvent(
                        _unitData.Id,
                        "change");
#endif
                return;
            }

            // 행동 소유권이 열린 상태에서만 추적 대상을 교체한다.
            SetCombatTargetTracking(targetId, targetIsUnit);
            if (NetworkContext.IsNetworkActive)
                RetireAndCloseAttackPresentationScope("combat-target-change");   // [P2-6] 멀티: 이 안에서 차지 VFX도 끈다(lease가 닫혀 투사체가 안 나가므로)
            // [P2-6 검토 결과] 싱글 ChangeTarget에서는 차지를 끄지 않는다(위 Retire를 부르지 않으므로 끄는 경로도 없다).
            //   싱글의 타겟 변경 이벤트는 UnitCombatUseCase.TryAttack이 「새 공격을 시작하는 순간」에 발행하고(진짜 취소가 아님),
            //   싱글은 lease를 쓰지 않아 다음 타격 marker가 새 타겟으로 투사체를 그대로 쏜다. 여기서 끄면 정상 차지가 잘린다.
            ClearUnitActionShadowDesiredTarget();
        }

        /// <summary>
        /// Attack Animator 상태는 유지하면서 아직 커밋되지 않은 회차의 타격 연출만 차단한다.
        /// </summary>
        public void SuppressPendingAttackImpactPresentation()
        {
            if (!NetworkContext.IsNetworkActive) return;
            RetireAndCloseAttackPresentationScope("provisional-suppression");   // [P2-6] 이 안에서 차지 VFX도 끈다
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
            bool acceptStopEvent = UnitActionRotationEventPolicy.ShouldAcceptStopEvent(
                NetworkContext.IsNetworkActive,
                NetworkContext.IsNetworkServer);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (acceptStopEvent)
            {
                ObserveInfernoAttackFacing(
                    AnimatedAttackFacingStage.AttackEndPose,
                    _infernoFacingTargetId,
                    _infernoFacingTargetIsUnit,
                    _infernoFacingPresentationRevision,
                    _infernoFacingScope);
            }
            // 종료/무시된 Stop 모두 다음 공격에서 이전 pending이 소비되지 않게 닫는다.
            _infernoAttackStartFacingPending = false;
#endif
            if (!acceptStopEvent)
            {
                // Host stop closes the logical scope, not the still-running native owner.
                _attackPhaseNetworkUnit?.CloseLogicalAttackScope();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (_unitData != null)
                    UnitMovementAuthorityObserver.ObserveIgnoredServerTargetEvent(
                        _unitData.Id,
                        "stop");
#endif
                return;
            }

            ClearCombatTargetTracking(
                preserveCommittedSourceMarker: NetworkContext.IsNetworkActive);
            if (NetworkContext.IsNetworkActive) _attackPhaseNetworkUnit?.CloseLogicalAttackScope();
            ClearUnitActionShadowDesiredTarget();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            ClearInfernoAttackFacingEvidenceState();
#endif

            // 행동 writer를 반환한다. 이동 코루틴이 살아 있으면 Movement로 돌려주되,
            // 실제 Walk 재생은 다음 Advanced commit이 Held를 해제할 때 시작한다.
            bool resumeMovement = _moveCoroutine != null
                && _unitData != null
                && _unitData.IsAlive;
            _rootRotationOwnership.ReleaseAction(resumeMovement);

            // 의도적으로 비워둠 — 위 summary 참조.
        }
    }

    /// <summary>
    /// [2026-10-21 서버 권위 투사체 단계 D] UnitView.ApplyServerProjectileLaunchCancel 결과(진단 계수용 — 동작 판단에는 쓰지 않는다).
    /// 숫자 값은 ServerProjectileDiagnostics.RecordChargeCancelApplied의 결과 번호와 같다(바꾸면 그쪽도 함께 바꾼다).
    /// </summary>
    public enum ServerProjectileChargeCancelResult
    {
        /// <summary>켜져 있던 차지 VFX를 껐다.</summary>
        Stopped = 0,
        /// <summary>끌 차지 VFX가 없었다(아직 차지 신호 전 — 멀티 Client는 그 회차 차지를 막아 두었다).</summary>
        NothingActive = 1,
        /// <summary>통보의 회차가 화면의 현재 회차와 달라 적용하지 않았다(더 새 공격의 차지를 지키기 위해).</summary>
        StaleRevision = 2,
        /// <summary>차지 유닛이 아니다(공격 클립에 OnAttackCharge 없음) — 할 일이 없다.</summary>
        NotChargeUnit = 3,
        /// <summary>유닛 데이터가 없거나 이미 죽었다(사망 쪽 정지 경로가 처리한다).</summary>
        Unavailable = 4
    }
}
