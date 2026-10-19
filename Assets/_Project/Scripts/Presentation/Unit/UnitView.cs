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
    //   SetDependencies는 Infrastructure 구체 타입을 받으므로 인터페이스에 포함하지 않음.
    public class UnitView : MonoBehaviour, Hexiege.Application.IUnitView, IUnitActionPoseSource
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

        /// <summary>이 View와 같은 객체의 실제 의존성을 확보한다. 시각 조회나 권위 허가는 하지 않는다.</summary>
        private void EnsureHealNetworkUnit()
        {
            // Unity의 destroyed-object null 검사도 사용한다. 다른 객체의 cache는 재사용하지 않는다.
            // component가 없거나 아직 spawn 전이어도 만들거나 권위를 우회하지 않고 기존 guard에 맡긴다.
            if (_healNetworkUnit == null || _healNetworkUnit.gameObject != gameObject)
                _healNetworkUnit = GetComponent<NetworkUnit>();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveFirstHealEntry(bool accepted, bool cacheBefore)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // 회차 생성 전 증거다. 유효 cast를 위조하지 않고 lifetime당 승인/거부 각각 한 번만 남긴다.
            byte bit = accepted ? (byte)1 : (byte)2;
            if ((_healFirstEntryEvidence & bit) != 0) return;
            _healFirstEntryEvidence |= bit;
            bool hasComponent = _healNetworkUnit != null;
            bool spawned = hasComponent && _healNetworkUnit.IsSpawned;
            GameLog.Dev.Info("Combat", nameof(UnitView), "[BLOOM-HEAL] FirstEntry",
                System.FormattableString.Invariant($"UnitId={_unitData.Id}, Lifetime={_healLifetime}, CastAllocated=false, Accepted={accepted}, CacheBefore={cacheBefore}, CacheAfter={hasComponent}, SameObject={hasComponent && _healNetworkUnit.gameObject == gameObject}, NetworkActive={NetworkContext.IsNetworkActive}, NetworkServer={NetworkContext.IsNetworkServer}, Spawned={spawned}, ComponentServer={spawned && _healNetworkUnit.IsServer}, Enabled={isActiveAndEnabled}, Alive={_unitData.IsAlive}, LocalNow={Time.timeAsDouble:G17}, Frame={Time.frameCount}"));
#endif
        }

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
            // [R-2 비활성화 — 2026-10-19 차지 유닛 발사 시점 P2-2] 옛 판별: 유닛 「타입 이름」이 FoxMagician인가.
            //   이제는 「공격 클립에 OnAttackCharge 이벤트가 있는가」로 판별한다(_hasAttackChargeMarker, P2-1).
            //   InfernoSpirit 클립에도 같은 이벤트를 넣었으므로 두 유닛이 같은 길을 쓴다.
            // if (_unitData == null || _unitData.Type != UnitType.FoxMagician) return;
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

        private void UpdateHealFacing(float deltaTime)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _healProbeBeforeYaw = transform.eulerAngles.y;
#endif
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            { ObserveHealFacingTransition(); return; }
            if (_healPresentation.Current.Active && _rootRotationOwnership.ActionOwnsRoot
                && _healPresentation.Current.Priority == 1 && _healFacingTarget != null
                && _healFacingTargetView != null && _healFacingTargetView.Data != null
                && _healFacingTargetView.Data.IsAlive)
                RotateActionTowards(_healFacingTarget.position, deltaTime);
            ObserveHealFacingTransition();
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

        // 진단은 gameplay cache를 복구하거나 projector getter를 호출하지 않는다.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveHealFacingTransition()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var state = _healPresentation.Current;
            if (!state.IsValid || !state.Active || Time.timeAsDouble < _healProbeNextSample) return;
            _healProbeNextSample = Time.timeAsDouble + 0.25d;
            string reason = HealFacingProbeReason(state);
            // 1도는 관찰용 문턱이며 회전 허가/HP 판정에는 쓰지 않는다.
            bool drifted = Mathf.Abs(Mathf.DeltaAngle(_healProbeStartYaw, transform.eulerAngles.y)) > 1f;
            bool converged = false;
            if (_healFacingTarget != null)
            {
                Vector3 delta = _healFacingTarget.position - transform.position; delta.y = 0;
                converged = delta.sqrMagnitude >= 0.001f
                    && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y,
                        Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg)) <= 1f;
            }
            if (reason == _healProbeReason && (!converged || _healProbeConverged)
                && drifted == _healProbeDrifted) return;
            _healProbeReason = reason;
            _healProbeDrifted = drifted;
            _healProbeConverged |= converged;
            if (++_healProbeTransitions <= 8) ObserveHealPose("FacingTransition", state);
            else if (_healProbeTransitions == 9)
                UnitHealCastDiagnostics.Event("FacingOverflow", state, "TransitionLimit=8, Coverage=Incomplete");
#endif
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private string HealFacingProbeReason(UnitHealCastPresentation state)
        {
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return "ClientNoWriter";
            if (!state.Active) return "Inactive";
            if (!_rootRotationOwnership.ActionOwnsRoot) return "NotActionOwner";
            if (state.Priority != 1) return "Priority";
            if (_healFacingTarget == null) return "CacheMissing";
            if (_healFacingTargetView == null || _healFacingTargetView.Data == null) return "TargetViewMissing";
            if (!_healFacingTargetView.Data.IsAlive) return "TargetDead";
            Vector3 delta = _healFacingTarget.position - transform.position; delta.y = 0;
            return delta.sqrMagnitude < 0.001f ? "NearZeroPreserveYaw" : "Eligible";
        }
#endif

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveHealPose(string stage, UnitHealCastPresentation state)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!state.IsValid || _unitData == null || !_unitData.IsHealer) return;
            if (_healProbeSequence != state.Sequence || _healProbeLifetime != state.Lifetime)
            {
                _healProbeSequence = state.Sequence; _healProbeLifetime = state.Lifetime;
                _healProbeStartYaw = transform.eulerAngles.y; _healProbeCount = 0;
                _healProbeTransitions = 0; _healProbeReason = null; _healProbeConverged = false;
                _healProbeDrifted = false; _healProbeBaselineStage = stage;
                _healProbeNextSample = 0;
            }
            if (++_healProbeCount > 24)
            {
                if (_healProbeCount == 25) UnitHealCastDiagnostics.Event("PoseOverflow", state,
                    "CheckpointLimit=24, Coverage=Incomplete");
                return;
            }
            // lookup는 관찰용 local 변수이며 실제 회전 cache에는 쓰지 않는다.
            Transform target = GetTargetTransform(state.TargetId, true);
            Vector3 pos = transform.position;
            Vector3 delta = target != null ? target.position - pos : Vector3.zero; delta.y = 0;
            bool defined = state.TargetId != state.UnitId && target != null && delta.sqrMagnitude >= 0.001f;
            float yaw = transform.eulerAngles.y;
            float expectedAngle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            string expected = defined ? expectedAngle.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "Undefined";
            string angleError = defined ? Mathf.DeltaAngle(yaw, expectedAngle).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "Undefined";
            UnitHealCastDiagnostics.Event("PoseRoot-" + stage, state, FormattableString.Invariant(
                $"Now={HealNow:G17}, Elapsed={HealNow - state.StartedAt:F6}, Self={state.UnitId == state.TargetId}, Lookup={target != null}, Cache={_healFacingTarget != null}, CacheMatch={target != null && target == _healFacingTarget}, Owner={_rootRotationOwnership.Owner}, Frozen={_isFrozenAnim}, Held={_isMovementHeldAnim}, Reason={HealFacingProbeReason(state)}, Yaw={yaw:F3}, Baseline={_healProbeStartYaw:F3}, Drift={Mathf.DeltaAngle(_healProbeStartYaw, yaw):F3}, ExpectedYaw={expected}"));
            if (stage == "FacingTransition")
                UnitHealCastDiagnostics.Event("FacingStep", state, FormattableString.Invariant(
                    $"BeforeYaw={_healProbeBeforeYaw:F3}, AfterYaw={yaw:F3}, Step={Mathf.DeltaAngle(_healProbeBeforeYaw, yaw):F3}, Converged={_healProbeConverged}, DriftThresholdDeg=1, Drifted={_healProbeDrifted}"));
            UnitHealCastDiagnostics.Event("PoseCompare-" + stage, state,
                $"AngleError={angleError}, BaselineStage={_healProbeBaselineStage}, CacheUnitId={(_healFacingTargetView != null && _healFacingTargetView.Data != null ? _healFacingTargetView.Data.Id : -1)}, CacheAlive={_healFacingTargetView != null && _healFacingTargetView.Data != null && _healFacingTargetView.Data.IsAlive}");
            UnitHealCastDiagnostics.Event("PoseVector-" + stage, state, FormattableString.Invariant(
                $"CasterXZ={pos.x:F3}/{pos.z:F3}, TargetXZ={(target != null ? target.position.x : float.NaN):F3}/{(target != null ? target.position.z : float.NaN):F3}, DeltaXZ={delta.x:F3}/{delta.z:F3}, Sqr={delta.sqrMagnitude:F6}, RootForward={transform.forward.x:F3}/{transform.forward.z:F3}, PayloadAim={state.AimX:F3}/{state.AimZ:F3}"));
            Transform visual = _visualRootProjector != null ? _visualRootProjector.ObservedVisualRoot : null;
            Transform model = _animator != null ? _animator.transform : null;
            var anim = _animator != null ? _animator.GetCurrentAnimatorStateInfo(0) : default;
            UnitHealCastDiagnostics.Event("PoseVisual-" + stage, state, FormattableString.Invariant(
                $"Visual={visual != null}, VisualYaw={(visual != null ? visual.eulerAngles.y : float.NaN):F3}, VisualLocalYaw={(visual != null ? visual.localEulerAngles.y : float.NaN):F3}, ModelProxy={model != null}, ModelForward={(model != null ? model.forward.x : float.NaN):F3}/{(model != null ? model.forward.z : float.NaN):F3}, Flip={ViewConverter.IsFlipped}, State={anim.fullPathHash}, Normalized={anim.normalizedTime:F6}, Speed={(_animator != null ? _animator.speed : float.NaN):F3}, Phase=CallbackSampleNotRenderedFrame"));
            UnitHealCastDiagnostics.Event("VisualForward-" + stage, state, FormattableString.Invariant(
                $"Forward={(visual != null ? visual.forward.x : float.NaN):F3}/{(visual != null ? visual.forward.z : float.NaN):F3}, LocalForward={(visual != null ? (visual.localRotation * Vector3.forward).x : float.NaN):F3}/{(visual != null ? (visual.localRotation * Vector3.forward).z : float.NaN):F3}"));
#endif
        }

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
        }

#endif

        private void OnDisable()
        {
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
                        EndHealPresentation("death", terminal: true);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        PostCombatTraceEvent("unit-death", retire: true);
                        RetireMovementShadowUnit();
#endif
                        // 사망한 root의 마지막 이동/공격 목표가 adapter에 남아 다음 관측으로 재사용되지 않게 한다.
                        ClearUnitActionShadowDesiredTarget();
                        CloseAttackPhasePresentation("attacker-death");

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

        private bool TryPrepareMovementScope(
            out ulong preparedCommandRevision,
            out ulong preparedSegmentRevision)
            => UnitMovementScopePolicy.TryPrepare(
                _movementCommandRevision,
                _movementSegmentRevision,
                _movementPendingCommand,
                _movementPendingSegment,
                out preparedCommandRevision,
                out preparedSegmentRevision);

        private void CommitPreparedMovementScope(
            ulong preparedCommandRevision,
            ulong preparedSegmentRevision)
        {
            _movementCommandRevision = preparedCommandRevision;
            _movementSegmentRevision = preparedSegmentRevision;
            _movementPendingCommand = false;
            _movementPendingSegment = false;
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

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void BeginSoloRecoveryEvidence()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkContext.IsNetworkActive || _unitData == null) return;
            if (_soloRecoveryPending) _soloRecoveryOpen++;
            _soloRecoveryPending = true;
            _soloRecoveryEpisode++;
            _soloRecoveryLastReason = "none";
            SoloRecoveryEvidence("start", "combat-return");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void SoloRecoveryEvidence(string stage, string reason)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkContext.IsNetworkActive || _unitData == null || _soloRecoveryEpisode == 0) return;
            // 누적 판정은 출력 예산과 독립이다. 상세가 생략돼도 미종결을 성공 0으로 숨기지 않는다.
            if (stage == "blocked")
            {
                if (_soloRecoveryLastReason == reason) return;
                _soloRecoveryBlocked++;
            }
            if (stage == "resume-success")
            {
                if (!_soloRecoveryPending) return;
                _soloRecoveryPending = false;
                _soloRecoveryResumed++;
            }
            if (stage == "completed" && _soloRecoveryPending)
            {
                // 이미 목적 중심인 정상 완료는 '걸어서 재개'와 별도로 닫는다.
                _soloRecoveryPending = false;
                _soloRecoveryCompleted++;
            }
            _soloRecoveryLastReason = reason;
            if (stage != "end" && !UnitSoloPursuitStep.TakeEvidence(
                ref _soloRecoveryLines, ref _soloRecoveryOmitted, out bool firstOmission))
            {
                if (firstOmission)
                    GameLog.Dev.Warn("Combat", nameof(UnitView), "[UAS-SOLO-RETURN] 상세 상한",
                        $"Stage=cap, Unit={_unitData.Id}, Episode={_soloRecoveryEpisode}, Limit=64, Coverage=incomplete");
                return;
            }
            Vector3 domain = ViewConverter.FromView(transform.position);
            HexCoord root = HexMetrics.WorldToHex(domain);
            HexCoord logical = _unitData.Position;
            string data = $"Stage={stage}, Unit={_unitData.Id}, Type={(int)_unitData.Type}, Mode={_lockedMovementPipelineMode}, Episode={_soloRecoveryEpisode}, RootHex={root.Q};{root.R}, LogicalHex={logical.Q};{logical.R}, Distance={HexCoord.Distance(root, logical)}, Provider={_lastPathRequestResult.Status}, Reason={reason}, Started={_soloRecoveryEpisode}, Resumed={_soloRecoveryResumed}, Blocked={_soloRecoveryBlocked}, Open={_soloRecoveryOpen + (_soloRecoveryPending ? 1 : 0)}, Omitted={_soloRecoveryOmitted}, Coverage={(_soloRecoveryOmitted > 0 || _soloRecoveryFormatRejected > 0 ? "incomplete" : "bounded")}, Frame={Time.frameCount}";
            data += $", Completed={_soloRecoveryCompleted}, FormatRejected={_soloRecoveryFormatRejected}";
            if (!UnitSoloPursuitStep.FitsEvidence(data))
            {
                _soloRecoveryFormatRejected++;
                data = $"Stage={stage}, Unit={_unitData.Id}, Episode={_soloRecoveryEpisode}, Started={_soloRecoveryEpisode}, Resumed={_soloRecoveryResumed}, Blocked={_soloRecoveryBlocked}, Completed={_soloRecoveryCompleted}, Open={_soloRecoveryOpen + (_soloRecoveryPending ? 1 : 0)}, Omitted={_soloRecoveryOmitted}, FormatRejected={_soloRecoveryFormatRejected}, Coverage=incomplete";
            }
            GameLog.Dev.Info("Combat", nameof(UnitView), "[UAS-SOLO-RETURN] 싱글 이동 복귀", data);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void EndSoloRecoveryEvidence()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_soloRecoveryEnded) return;
            SoloRecoveryEvidence("end", "view-disable-or-reuse");
            _soloRecoveryEnded = true;
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void BeginSoloPursuitEvidence(int target, bool isUnit)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkContext.IsNetworkActive || _unitData == null) return;
            _soloPursuitEvidenceActive = true;
            _soloPursuitEpisode++;
            _soloPursuitTarget = target; _soloPursuitTargetIsUnit = isUnit;
            _soloPursuitIndex = -1; _soloPursuitHasRoute = false;
            _soloPursuitWaypoint = transform.position;
            _soloPursuitStalls = 0; _soloPursuitProgressTime = Time.timeAsDouble;
            SoloPursuitEvidence("start");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void SoloPursuitEvidence(string stage, bool reached = false, bool consume = false)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkContext.IsNetworkActive || _unitData == null || !_soloPursuitEvidenceActive) return;
            if (!UnitSoloPursuitStep.TakeEvidence(ref _soloPursuitLines, ref _soloPursuitOmitted,
                    out bool firstOmission))
            {
                if (firstOmission)
                    GameLog.Dev.Warn("Combat", nameof(UnitView), "[UAS-SOLO-PURSUIT] 상세 상한 도달",
                        $"Unit={_unitData.Id}, Episode={_soloPursuitEpisode}, Stage=cap, Limit=64, Omitted={_soloPursuitOmitted}, Coverage=incomplete, Frame={Time.frameCount}");
                return;
            }
            Vector3 root = transform.position;
            Vector3 domain = ViewConverter.FromView(root);
            HexCoord rootTile = HexMetrics.WorldToHex(domain);
            string data = System.FormattableString.Invariant($"Stage={stage}, Unit={_unitData.Id}, Type={(int)_unitData.Type}, Episode={_soloPursuitEpisode}, NetworkActive=false, Mode={_lockedMovementPipelineMode}, Target={_soloPursuitTarget}, TargetIsUnit={_soloPursuitTargetIsUnit}, Goal={_soloPursuitGoal.Q};{_soloPursuitGoal.R}, HasRoute={_soloPursuitHasRoute}, Index={_soloPursuitIndex}, Waypoint={_soloPursuitWaypoint.x:G9};{_soloPursuitWaypoint.z:G9}, Reached={reached}, Consume={consume}, Root={root.x:G9};{root.y:G9};{root.z:G9}, DomainXZ={domain.x:G9};{domain.z:G9}, UnitTile={_unitData.Position.Q};{_unitData.Position.R}, RootTile={rootTile.Q};{rootTile.R}, Revision={_soloPursuitRouteRevision}, LiveRevision={_navigationEnvironmentRevision}, Invalidated={_combatPursuitPathInvalidated}, ActionOwnsRoot={_rootRotationOwnership.ActionOwnsRoot}, Frame={Time.frameCount}, Detail={_soloPursuitLines}, Omitted={_soloPursuitOmitted}, Coverage=bounded");
            if (!UnitSoloPursuitStep.FitsEvidence(data))
            {
                if (_soloPursuitFormatRejected < int.MaxValue) _soloPursuitFormatRejected++;
                data = $"Stage={stage}, Unit={_unitData.Id}, Episode={_soloPursuitEpisode}, FormatRejected={_soloPursuitFormatRejected}, Coverage=incomplete, Frame={Time.frameCount}";
            }
            GameLog.Dev.Info("Combat", nameof(UnitView), "[UAS-SOLO-PURSUIT] 싱글 추격 경계",
                data);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveSoloPursuitStall()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!_soloPursuitEvidenceActive || _soloPursuitStalls >= 2
                || Time.timeAsDouble - _soloPursuitProgressTime < 2d) return;
            _soloPursuitStalls++;
            _soloPursuitProgressTime = Time.timeAsDouble;
            SoloPursuitEvidence("stall");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void CloseSoloPursuitEvidence(string reason)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            SoloPursuitEvidence(reason);
            _soloPursuitEvidenceActive = false;
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void EndSoloPursuitEvidence()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_soloPursuitEvidenceEnded || _soloPursuitEpisode == 0 || _unitData == null) return;
            CloseSoloPursuitEvidence("view-disable");
            _soloPursuitEvidenceEnded = true;
            // 별도 terminal 한 줄은 상세 예산 밖에 예약한다. 수명당 최대 64 detail + cap + end.
            GameLog.Dev.Info("Combat", nameof(UnitView), "[UAS-SOLO-PURSUIT] 관측 수명 종료",
                $"Stage=end, Unit={_unitData.Id}, Episodes={_soloPursuitEpisode}, Detail={_soloPursuitLines}, Omitted={_soloPursuitOmitted}, FormatRejected={_soloPursuitFormatRejected}, Limit=64, Coverage={(_soloPursuitOmitted > 0 || _soloPursuitFormatRejected > 0 ? "incomplete" : "bounded")}, Frame={Time.frameCount}");
#endif
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
            Func<Vector3, bool> candidateAcquirePredicate = null,
            Func<bool> targetAcquirePresentationHandoff = null)
        {
            _lastMovementTrajectoryReachedWaypoint = false;
            _lastMovementCandidateAcquiredTarget = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired)
            {
                _postCombatTrace.HasSpatial = false;
                _postCombatTrace.Candidate = candidatePosition;
            }
#endif
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveClientWriteAttempt();
#endif
                return MovementWriteOutcome.Rejected;
            }

            // 건물 변경 이벤트가 랠리 경유 종료를 예약한 뒤에는 이전 경로로 한 프레임도
            // 더 쓰지 않는다. 기존 RepathRequired 탈출을 통해 소유 코루틴이 정리하며,
            // 여기서 콜백을 호출하거나 코루틴을 강제로 교체하지 않는다.
            if (_rallyPolicyCompletionPending
                || (_activeRallyPoint.HasValue && !IsInCombat()
                    && _pendingPath != null && _pendingPath.Count == 1))
                return MovementWriteOutcome.RepathRequired;

            // 공격/힐 행동이 회전을 소유하는 동안 이동 코루틴은 살아 있어도 pose를 쓰지 않는다.
            // Held는 진행량을 소비하지 않으므로 행동 종료 뒤 같은 navigation objective에서 재개된다.
            if (_rootRotationOwnership.ActionOwnsRoot)
                return MovementWriteOutcome.Held;

            bool reducerAuthoritative = _lockedMovementPipelineMode
                == UnitMovementPipelineMode.ReducerAuthoritative;
            if (!reducerAuthoritative)
            {
                Vector3 soloPositionBefore = transform.position;
                // SOLO도 Root 선분과 실제 공간 점유를 같은 경계에서 검증한다.
                // 중심 checkpoint와 별개이므로 추격 직전 A*의 반 타일 이동도 누락하지 않는다.
                int soloTransitionCount = 0;
                if (!NetworkContext.IsNetworkActive
                    && !TryStageSoloSpatialCommit(transform.position, candidatePosition,
                        logicalCorridorPath, logicalCorridorWaypointIndex,
                        out soloTransitionCount, out SpatialCandidateEvaluation soloSpatial))
                {
                    SoloRecoveryEvidence("blocked", "spatial-" + soloSpatial.Verdict);
                    return soloSpatial.Verdict == UnitSpatialCandidateVerdict.CandidateUnsafe
                        || soloSpatial.Verdict == UnitSpatialCandidateVerdict.RouteInvalidated
                        ? MovementWriteOutcome.RepathRequired : MovementWriteOutcome.Rejected;
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveLegacyWriterSelection();
#endif
                transform.position = candidatePosition;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeed * Time.deltaTime);
                if (!NetworkContext.IsNetworkActive && soloTransitionCount > 0)
                {
                    _movementUseCase.CommitPreflightedSpatialTransitionsPreservingFacing(
                        _unitData, _spatialTransitionBuffer, soloTransitionCount);
                    if (_isAStarMoving && logicalCorridorPath != null)
                    {
                        for (int index = 0; index < soloTransitionCount; index++)
                            GameEvents.OnUnitEnteredTile.OnNext(new UnitEnteredTileEvent(
                                _unitData.Id, _spatialTransitionBuffer[index]));
                    }
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (!NetworkContext.IsNetworkActive && _soloRecoveryPending && _isAStarMoving
                    && !_isInCombatPursuit && fallbackIntentReason != UnitMovementIntentReason.Chase
                    && CalculateMovementPositionDeltaXZ(soloPositionBefore, transform.position) > 0.000001f
                    && HexMetrics.WorldToHex(ViewConverter.FromView(transform.position)) == _unitData.Position)
                    SoloRecoveryEvidence("resume-success", "positive-navigation-progress");
#endif
                // 싱글 추격만 실제 writer 결과를 checkpoint 소비자에게 전달한다.
                // 네트워크 Legacy rollback과 Reducer의 판정/콜백은 변경하지 않는다.
                if (!NetworkContext.IsNetworkActive
                    && fallbackIntentReason == UnitMovementIntentReason.Chase
                    && !targetAcquirePriority)
                {
                    _lastMovementCandidateAcquiredTarget = candidateAcquirePredicate != null
                        && candidateAcquirePredicate(transform.position);
                    _lastMovementTrajectoryReachedWaypoint = !_lastMovementCandidateAcquiredTarget
                        && UnitSoloPursuitStep.Reached(transform.position.x, transform.position.z,
                            desiredTarget.x, desiredTarget.z, maximumTravelDistanceWorld);
                }
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
            if (!TryPrepareMovementScope(
                    out ulong evaluatedCommandRevision,
                    out ulong evaluatedSegmentRevision))
                return MovementWriteOutcome.Rejected;

            Vector3 facing = transform.forward;
            Vector3 positionBefore = transform.position;
            Quaternion rotationBefore = transform.rotation;
            double observedServerTime = Time.timeAsDouble;
            UnitMovementSnapshot reducerSnapshotBefore =
                _movementAuthorityReducer.Snapshot;
            float expectedDelta = CalculateMovementPositionDeltaXZ(
                transform.position, candidatePosition);
            float planningDistance = maximumTravelDistanceWorld >= 0f
                ? maximumTravelDistanceWorld
                : expectedDelta;
            UnitTrajectoryStep trajectoryStep = default;
            bool hasTrajectoryStep = false;
            int spatialTransitionCount = 0;
            UnitMovementEvaluation evaluation;
            UnitMovementPreparedTransition preparedTransition = default;
            SpatialCandidateEvaluation selectedProbeEvaluation = default;
            bool hasSelectedProbeEvaluation = false;

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
                // 이미 현재 Root에서 획득한 타겟은 이동하지 않고 Action으로 넘긴다.
                // pure planner에서 후보 이동으로 처음 획득하는 별도 분기는 그대로 유지한다.
                if (targetAcquirePriority) candidatePosition = positionBefore;
                // endpoint/우선 타겟 획득도 adapter가 candidate pose를 커밋할 수 있으므로
                // reducer 호출 전에 실제 후보 선분을 검증한다. 일반 NoIntent/Held는 아래에서
                // 위치를 적용하지 않으므로 미리 계산한 전이도 커밋되지 않는다.
                if (!TryStageSpatialCommit(
                        positionBefore,
                        candidatePosition,
                        logicalCorridorPath,
                        logicalCorridorWaypointIndex,
                        out spatialTransitionCount,
                        out SpatialCandidateEvaluation endpointSpatialEvaluation))
                {
                    return endpointSpatialEvaluation.Verdict
                            == UnitSpatialCandidateVerdict.RouteInvalidated
                        || endpointSpatialEvaluation.Verdict
                            == UnitSpatialCandidateVerdict.CandidateUnsafe
                        ? MovementWriteOutcome.RepathRequired
                        : MovementWriteOutcome.Rejected;
                }

                evaluation = UnitMovementEvaluationAdapter.Prepare(
                    _movementAuthorityReducer,
                    evaluatedCommandRevision,
                    evaluatedSegmentRevision,
                    _movementIntentReason,
                    transform.position.x,
                    transform.position.z,
                    facing.x,
                    facing.z,
                    desiredTarget.x,
                    desiredTarget.z,
                    targetAcquirePriority,
                    planningDistance,
                    observedServerTime,
                    out preparedTransition);
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

                if (!validPureInput)
                    return MovementWriteOutcome.Rejected;

                if (logicalCorridorPath != null)
                {
                    _lastCorridorRepathEvidence = default;
                    bool selectedStartTileEgress = false;
                    UnitTrajectoryCorridorResolution corridorResolution;
                    UnitSpatialCandidateVerdict corridorFinalVerdict;
                    SpatialCandidateEvaluation corridorFinalEvaluation = default;
                    _corridorStepValidator ??= ValidateActiveCorridorStep;
                    _activeCorridorPath = logicalCorridorPath;
                    _activeCorridorWaypointIndex = logicalCorridorWaypointIndex;
                    _activeCorridorSelectedStartTileEgress = false;
                    _activeCorridorHasEvaluation = false;
                    try
                    {
                        corridorResolution = UnitTrajectoryCorridorResolver.Resolve(
                            currentPoint,
                            currentFacing,
                            waypoint,
                            hasNext,
                            nextWaypoint,
                            planningDistance,
                            _rotationSpeed * Time.deltaTime,
                            HexMetrics.TileHeight * 0.35d,
                            _corridorStepValidator,
                            out trajectoryStep,
                            out corridorFinalVerdict);
                        selectedStartTileEgress =
                            _activeCorridorSelectedStartTileEgress;
                        corridorFinalEvaluation =
                            _activeCorridorLastEvaluation;
                        if (!_activeCorridorHasEvaluation)
                        {
                            var invalidPreflight = new UnitSpatialPreflightResult(
                                UnitSpatialPreflightStatus.InvalidChain,
                                default,
                                -1);
                            corridorFinalEvaluation = new SpatialCandidateEvaluation(
                                UnitSpatialCandidateVerdict.Fatal,
                                UnitSpatialTransitionPlanStatus.InvalidInput,
                                invalidPreflight,
                                0,
                                _unitData != null ? _unitData.Position : default,
                                _unitData != null ? _unitData.Position : default,
                                false,
                                default);
                            corridorFinalVerdict = UnitSpatialCandidateVerdict.Fatal;
                        }
                    }
                    finally
                    {
                        _activeCorridorPath = null;
                        _activeCorridorWaypointIndex = -1;
                        _activeCorridorSelectedStartTileEgress = false;
                        _activeCorridorLastEvaluation = default;
                        _activeCorridorHasEvaluation = false;
                    }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementAuthorityObserver.ObserveCorridorResolution(
                        corridorResolution,
                        selectedStartTileEgress);
#endif
                    if (corridorResolution == UnitTrajectoryCorridorResolution.Invalid
                        || corridorFinalVerdict == UnitSpatialCandidateVerdict.Fatal)
                    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        ObserveFinalSpatialCandidate(
                            corridorFinalEvaluation,
                            repathRequired: false);
#endif
                        return MovementWriteOutcome.Rejected;
                    }
                    if (corridorResolution == UnitTrajectoryCorridorResolution.RepathRequired)
                    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        ObserveFinalSpatialCandidate(
                            corridorFinalEvaluation,
                            repathRequired: true);
#endif
                        return MovementWriteOutcome.RepathRequired;
                    }
                    selectedProbeEvaluation = corridorFinalEvaluation;
                    hasSelectedProbeEvaluation = true;
                }
                else if (!UnitServerTrajectoryPlanner.TryPlan(
                             currentPoint,
                             currentFacing,
                             waypoint,
                             hasNext,
                             nextWaypoint,
                             planningDistance,
                             _rotationSpeed * Time.deltaTime,
                             HexMetrics.TileHeight * 0.35d,
                             out trajectoryStep)
                         || !trajectoryStep.IsValid)
                {
                    return MovementWriteOutcome.Rejected;
                }

                Vector3 plannedCandidate = transform.position;
                plannedCandidate.x = (float)trajectoryStep.CandidatePosition.X;
                plannedCandidate.z = (float)trajectoryStep.CandidatePosition.Z;

                // planner가 실제 후보 pose를 확정한 직후, stateful reducer보다 먼저
                // Root 경계 전이 전체를 검증한다. 실패하면 scope/reducer/pose/domain 모두 그대로다.
                if (!TryStageSpatialCommit(
                        positionBefore,
                        plannedCandidate,
                        logicalCorridorPath,
                        logicalCorridorWaypointIndex,
                        out spatialTransitionCount,
                        out SpatialCandidateEvaluation stagedSpatialEvaluation))
                {
                    return stagedSpatialEvaluation.Verdict
                            == UnitSpatialCandidateVerdict.RouteInvalidated
                        || stagedSpatialEvaluation.Verdict
                            == UnitSpatialCandidateVerdict.CandidateUnsafe
                        ? MovementWriteOutcome.RepathRequired
                        : MovementWriteOutcome.Rejected;
                }
                if (hasSelectedProbeEvaluation
                    && (selectedProbeEvaluation.Verdict != stagedSpatialEvaluation.Verdict
                        || selectedProbeEvaluation.TransitionStatus
                            != stagedSpatialEvaluation.TransitionStatus
                        || selectedProbeEvaluation.TransitionCount
                            != stagedSpatialEvaluation.TransitionCount
                        || selectedProbeEvaluation.Preflight.Status
                            != stagedSpatialEvaluation.Preflight.Status
                        || selectedProbeEvaluation.Preflight.OffendingTile
                            != stagedSpatialEvaluation.Preflight.OffendingTile
                        || selectedProbeEvaluation.Preflight.OffendingTransitionIndex
                            != stagedSpatialEvaluation.Preflight.OffendingTransitionIndex
                        || selectedProbeEvaluation.RootBeforeHex
                            != stagedSpatialEvaluation.RootBeforeHex
                        || selectedProbeEvaluation.CandidateHex
                            != stagedSpatialEvaluation.CandidateHex))
                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementAuthorityObserver.ObserveSpatialStageDivergence(
                        _unitData != null ? _unitData.Id : -1,
                        selectedProbeEvaluation.Verdict,
                        stagedSpatialEvaluation.Verdict,
                        selectedProbeEvaluation.Preflight.Status,
                        stagedSpatialEvaluation.Preflight.Status);
#endif
                    throw new InvalidOperationException(
                        "Corridor probe and final spatial stage diverged.");
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

                UnitMovementReducerStatus status = _movementAuthorityReducer.Prepare(
                    _movementAuthorityReducer.Snapshot.Revision,
                    evaluatedCommandRevision,
                    evaluatedSegmentRevision,
                    _movementIntentReason,
                    true,
                    currentFacing,
                    reducerDirection,
                    acquireFromCandidate,
                    observedServerTime,
                    out preparedTransition);
                evaluation = new UnitMovementEvaluation(
                    status,
                    preparedTransition.Decision,
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
                    evaluatedCommandRevision,
                    evaluatedSegmentRevision,
                    evaluation,
                    desiredTarget,
                    planningDistance,
                    observedServerTime,
                    reducerSnapshotBefore,
                    positionBefore,
                    rotationBefore,
                    transform.position,
                    transform.rotation,
                    authorityWriterSelected: true,
                    walkAnimationAdvancing: _animator != null
                        && _currentAnimStateHash == StateWalk
                        && _animator.speed > 0.0001f);
#endif
                return MovementWriteOutcome.Rejected;
            }

            if (!_movementAuthorityReducer.CanCommitPrepared(preparedTransition))
                return MovementWriteOutcome.Rejected;

            // 공격 범위에 처음 들어온 Chase 후보는 Root의 마지막 위치/NoIntent보다 먼저
            // provisional Start를 Host와 원격 Client 전달 큐에 확정해야 한다. callback이
            // 실패하면 아직 movement snapshot, reducer, Root 어느 것도 커밋하지 않았으므로
            // 현재 이동 pose를 보존한 채 다음 정상 frame에서 같은 후보를 다시 평가한다.
            bool requiresAttackEntryHandoff =
                UnitAttackEntryCommitOrderPolicy.RequiresPresentationHandoff(
                    fallbackIntentReason,
                    evaluation.CommitsAcquireCandidate);
            bool attackEntryHandoffCompleted = !requiresAttackEntryHandoff
                || (targetAcquirePresentationHandoff != null
                    && targetAcquirePresentationHandoff());
            if (!UnitAttackEntryCommitOrderPolicy.CanCommitRootStop(
                    requiresAttackEntryHandoff,
                    attackEntryHandoffCompleted))
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveCombatPresentationHandoffFailure(
                    _unitData != null ? _unitData.Id : -1,
                    "client-visible-entry-order");
#endif
                return MovementWriteOutcome.DeferredAttackHandoff;
            }

            // planner/spatial/reducer 준비가 모두 성공해도 아직 reducer/scope/Root/Domain은
            // 변경되지 않았다. 준비 snapshot 게시가 성공해야만 로컬 권위 상태를 커밋한다.
            if (_movementNetworkUnit == null
                || !_movementNetworkUnit.PublishMovementSnapshot(
                    preparedTransition.PreparedSnapshot))
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObservePublicationFailure(
                    _unitData != null ? _unitData.Id : -1);
#endif
                return MovementWriteOutcome.Rejected;
            }

            if (!_movementAuthorityReducer.TryCommitPrepared(preparedTransition))
            {
                throw new InvalidOperationException(
                    "Prepared movement transition became stale after publication.");
            }
            CommitPreparedMovementScope(
                evaluatedCommandRevision,
                evaluatedSegmentRevision);

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
                SynchronizeDomainFacingFromSimulationForward();
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

            if (outcome == MovementWriteOutcome.Advanced
                && spatialTransitionCount > 0)
            {
                // 같은 동기 호출 안에서 preflight한 버퍼를 커밋한다. 이 지점까지 await/yield가 없으므로
                // 정상 경로에서는 재검증이 실패하지 않는다. Facing은 새 API가 현재 값을 보존한다.
                HexCoord committedPositionBefore = _unitData != null
                    ? _unitData.Position
                    : default;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveSpatialCommitAttempt(
                    _unitData != null ? _unitData.Id : -1,
                    spatialTransitionCount,
                    committedPositionBefore);
#endif
                try
                {
                    _movementUseCase.CommitPreflightedSpatialTransitionsPreservingFacing(
                        _unitData,
                        _spatialTransitionBuffer,
                        spatialTransitionCount);
                }
                catch (Exception)
                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementAuthorityObserver.ObserveSpatialCommit(
                        _unitData != null ? _unitData.Id : -1,
                        spatialTransitionCount,
                        committedPositionBefore,
                        _unitData != null ? _unitData.Position : default,
                        succeeded: false);
#endif
                    throw;
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveSpatialCommit(
                    _unitData != null ? _unitData.Id : -1,
                    spatialTransitionCount,
                    committedPositionBefore,
                    _unitData != null ? _unitData.Position : default,
                    succeeded: true);
#endif

                if (_isAStarMoving && logicalCorridorPath != null)
                {
                    for (int index = 0; index < spatialTransitionCount; index++)
                    {
                        GameEvents.OnUnitEnteredTile.OnNext(
                            new UnitEnteredTileEvent(
                                _unitData.Id,
                                _spatialTransitionBuffer[index]));
                    }
                }
            }

            // 권위 위치와 표현 상태를 같은 commit 경계에서 바꾼다. AlignToMove/NoIntent는
            // 위치가 멈추므로 Walk 시간도 정지하고, 실제 전진 frame에서만 다시 재생한다.
            SetMovementHeldAnimation(
                UnitMovementPresentationPolicy.ShouldHoldWalk(
                    evaluation.Decision.IsValid,
                    evaluation.Decision.AllowsMovement,
                    evaluation.CommitsAcquireCandidate,
                    evaluation.Decision.IsTargetAcquirePriority));

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            UnitMovementAuthorityObserver.ObserveFrame(
                _unitData != null ? _unitData.Id : -1,
                _movementNetworkUnit != null ? _movementNetworkUnit.NetworkObjectId : 0UL,
                _unitData != null ? _unitData.Type : default,
                _unitData != null ? _unitData.Team : default,
                _movementIntentReason,
                evaluatedCommandRevision,
                evaluatedSegmentRevision,
                evaluation,
                desiredTarget,
                planningDistance,
                observedServerTime,
                reducerSnapshotBefore,
                positionBefore,
                rotationBefore,
                transform.position,
                transform.rotation,
                authorityWriterSelected: true,
                walkAnimationAdvancing: _animator != null
                    && _currentAnimStateHash == StateWalk
                    && _animator.speed > 0.0001f);
#endif
            if (evaluation.Decision.Phase == UnitMovementPhase.NoIntent)
            {
                _movementIntentReason = UnitMovementIntentReason.None;
                _movementPendingCommand = false;
                _movementPendingSegment = false;
            }
            return outcome;
        }

        /// <summary>
        /// 서버 Root 후보 선분을 bounded 간격으로 표본화하고 실제 인접 타일 전이를 사전검증한다.
        /// non-walkable 경로 마지막 타일은 접촉 목표로만 인정하며 출력 전이에는 넣지 않는다.
        /// </summary>
        // 마지막 이동 불가 목적지는 실제 점유가 아니라 기존 terminal contact다.
        // 이미 진입한 접촉 타일 내부에서 다음 frame/endpoint를 처리할 때도 이 의미를 보존한다.
        // 중간 차단·비인접 Root·경로 밖 진입은 이 예외로 수용하지 않는다.
        private bool TryStageSoloSpatialCommit(Vector3 before, Vector3 candidate,
            IReadOnlyList<HexCoord> path, int waypoint,
            out int count, out SpatialCandidateEvaluation evaluation)
        {
            count = 0; evaluation = default;
            if (_unitData != null && _unitData.IsAlive && _movementUseCase != null
                && IsFiniteXZ(before) && IsFiniteXZ(candidate)
                && path != null && path.Count >= 2 && waypoint == path.Count - 1)
            {
                HexCoord terminal = path[path.Count - 1];
                if (!_movementUseCase.IsWalkable(terminal)
                    && _unitData.Position == path[path.Count - 2]
                    && HexCoord.Distance(_unitData.Position, terminal) == 1
                    && HexMetrics.WorldToHex(ViewConverter.FromView(before)) == terminal
                    && HexMetrics.WorldToHex(ViewConverter.FromView(candidate)) == terminal
                    && IsTrajectoryPointSweepInsidePath(before, candidate, path, waypoint,
                        out _, out _))
                    return true;
            }
            return TryStageSpatialCommit(before, candidate, path, waypoint, out count, out evaluation);
        }

        private bool TryStageSpatialCommit(
            Vector3 positionBefore,
            Vector3 authoritativeCandidate,
            IReadOnlyList<HexCoord> logicalPath,
            int logicalWaypointIndex,
            out int transitionCount,
            out SpatialCandidateEvaluation evaluation)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired)
                _postCombatTrace.Candidate = authoritativeCandidate;
#endif
            evaluation = EvaluateSpatialCandidate(
                positionBefore,
                authoritativeCandidate,
                logicalPath,
                logicalWaypointIndex,
                _spatialTileSamples,
                _spatialTransitionBuffer);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            ObserveFinalSpatialCandidate(
                evaluation,
                evaluation.Verdict == UnitSpatialCandidateVerdict.RouteInvalidated
                    || evaluation.Verdict == UnitSpatialCandidateVerdict.CandidateUnsafe);
#endif
            transitionCount = evaluation.TransitionCount;
            if (evaluation.Verdict == UnitSpatialCandidateVerdict.Accepted)
                return true;

            if (evaluation.Verdict == UnitSpatialCandidateVerdict.CandidateUnsafe
                || evaluation.Verdict == UnitSpatialCandidateVerdict.RouteInvalidated)
            {
                _lastCorridorRepathEvidence = evaluation.Evidence;
            }
            transitionCount = 0;
            return false;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void ObserveFinalSpatialCandidate(
            SpatialCandidateEvaluation evaluation,
            bool repathRequired)
        {
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired)
            {
                _postCombatTrace.Spatial = evaluation;
                _postCombatTrace.SpatialFrame = Time.frameCount;
                _postCombatTrace.SpatialTime = Time.timeAsDouble;
                _postCombatTrace.HasSpatial = true;
            }
            if (repathRequired)
                _spatialRecoverablePending = true;
            else if (evaluation.Verdict == UnitSpatialCandidateVerdict.Accepted)
                _spatialRecoverablePending = false;
            UnitMovementAuthorityObserver.ObserveSpatialFinalCandidate(
                _unitData != null ? _unitData.Id : -1,
                evaluation.Verdict,
                evaluation.TransitionStatus,
                evaluation.Preflight,
                evaluation.TransitionCount,
                _unitData != null ? _unitData.Position : default,
                evaluation.RootBeforeHex,
                evaluation.CandidateHex,
                repathRequired);
        }
#endif

        /// <summary>
        /// 후보 pose 하나를 관측만 한다. reducer/scope/Root/UnitData/index/event/observer를
        /// 변경하지 않으며 caller-owned scratch buffer에만 샘플과 전이를 기록한다.
        /// </summary>
        private SpatialCandidateEvaluation EvaluateSpatialCandidate(
            Vector3 positionBefore,
            Vector3 authoritativeCandidate,
            IReadOnlyList<HexCoord> logicalPath,
            int logicalWaypointIndex,
            List<HexCoord> sampleBuffer,
            HexCoord[] transitionBuffer)
        {
            sampleBuffer.Clear();
            if (_movementUseCase == null
                || _unitData == null
                || !_unitData.IsAlive
                || !IsFiniteXZ(positionBefore)
                || !IsFiniteXZ(authoritativeCandidate))
            {
                var invalid = new UnitSpatialPreflightResult(
                    UnitSpatialPreflightStatus.InvalidUnitOrDependency,
                    default,
                    -1);
                return new SpatialCandidateEvaluation(
                    UnitSpatialCandidateVerdict.Fatal,
                    UnitSpatialTransitionPlanStatus.InvalidInput,
                    invalid,
                    0,
                    default,
                    default,
                    false,
                    default);
            }

            float distance = CalculateMovementPositionDeltaXZ(
                positionBefore, authoritativeCandidate);
            float sampleSpacing = Mathf.Max(
                HexMetrics.TileHeight * 0.1f, 0.01f);
            int segmentCount = Mathf.Max(
                1, Mathf.CeilToInt(distance / sampleSpacing));
            if (segmentCount + 1 > SpatialSampleCapacity)
            {
                var invalid = new UnitSpatialPreflightResult(
                    UnitSpatialPreflightStatus.InvalidChain,
                    default,
                    -1);
                return new SpatialCandidateEvaluation(
                    UnitSpatialCandidateVerdict.Fatal,
                    UnitSpatialTransitionPlanStatus.InsufficientCapacity,
                    invalid,
                    0,
                    HexMetrics.WorldToHex(ViewConverter.FromView(positionBefore)),
                    HexMetrics.WorldToHex(ViewConverter.FromView(authoritativeCandidate)),
                    false,
                    default);
            }

            for (int sampleIndex = 0; sampleIndex <= segmentCount; sampleIndex++)
            {
                float t = sampleIndex / (float)segmentCount;
                Vector3 viewPoint = Vector3.Lerp(
                    positionBefore, authoritativeCandidate, t);
                Vector3 domainPoint = ViewConverter.FromView(viewPoint);
                sampleBuffer.Add(HexMetrics.WorldToHex(domainPoint));
            }

            HexCoord rootBeforeHex = sampleBuffer[0];
            HexCoord candidateHex =
                sampleBuffer[sampleBuffer.Count - 1];

            bool usedStartTileEgress = false;
            CorridorRepathEvidence evidence = default;
            if (logicalPath != null
                && !IsTrajectoryPointSweepInsidePath(
                    positionBefore,
                    authoritativeCandidate,
                    logicalPath,
                    logicalWaypointIndex,
                    out usedStartTileEgress,
                    out evidence))
            {
                return new SpatialCandidateEvaluation(
                    UnitSpatialCandidateVerdict.CandidateUnsafe,
                    UnitSpatialTransitionPlanStatus.OutsideLogicalPath,
                    UnitSpatialPreflightResult.Success(),
                    0,
                    rootBeforeHex,
                    candidateHex,
                    usedStartTileEgress,
                    evidence);
            }

            bool hasTerminalContact = false;
            HexCoord terminalTile = default;
            if (logicalPath != null && logicalPath.Count > 0)
            {
                HexCoord lastSample =
                    sampleBuffer[sampleBuffer.Count - 1];
                HexCoord logicalFinal = logicalPath[logicalPath.Count - 1];
                if (lastSample == logicalFinal
                    && !_movementUseCase.IsWalkable(lastSample))
                {
                    hasTerminalContact = true;
                    terminalTile = lastSample;
                }
            }

            UnitSpatialTransitionPlanStatus status =
                UnitSpatialTransitionPolicy.TryBuild(
                    _unitData.Position,
                    sampleBuffer,
                    sampleBuffer.Count,
                    logicalPath,
                    hasTerminalContact,
                    terminalTile,
                    transitionBuffer,
                    out int transitionCount);
            UnitSpatialPreflightResult preflight =
                status == UnitSpatialTransitionPlanStatus.Ready
                    || status == UnitSpatialTransitionPlanStatus.NoTransition
                    || status == UnitSpatialTransitionPlanStatus.TerminalContact
                ? _movementUseCase.PreflightSpatialTransitions(
                    _unitData,
                    transitionBuffer,
                    transitionCount)
                : UnitSpatialPreflightResult.Success();
            UnitSpatialCandidateVerdict verdict =
                UnitSpatialCandidatePolicy.Classify(status, preflight);
            return new SpatialCandidateEvaluation(
                verdict,
                status,
                preflight,
                transitionCount,
                rootBeforeHex,
                candidateHex,
                usedStartTileEgress,
                evidence);
        }

        private static float CalculateMovementPositionDeltaXZ(
            Vector3 from,
            Vector3 to)
        {
            float deltaX = to.x - from.x;
            float deltaZ = to.z - from.z;
            return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        private UnitSpatialCandidateVerdict ValidateActiveCorridorStep(
            UnitTrajectoryStep step)
        {
            if (_activeCorridorPath == null
                || _activeCorridorWaypointIndex < 0)
            {
                return UnitSpatialCandidateVerdict.Fatal;
            }

            Vector3 candidate = transform.position;
            candidate.x = (float)step.CandidatePosition.X;
            candidate.z = (float)step.CandidatePosition.Z;
            SpatialCandidateEvaluation evaluation = EvaluateSpatialCandidate(
                transform.position,
                candidate,
                _activeCorridorPath,
                _activeCorridorWaypointIndex,
                _spatialProbeTileSamples,
                _spatialProbeTransitionBuffer);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired)
                _postCombatTrace.Candidate = candidate;
#endif
            _activeCorridorLastEvaluation = evaluation;
            _activeCorridorHasEvaluation = true;
            _activeCorridorSelectedStartTileEgress =
                evaluation.UsedStartTileEgress;
            return evaluation.Verdict;
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
            int waypointIndex,
            out bool usedStartTileEgress,
            out CorridorRepathEvidence evidence)
        {
            usedStartTileEgress = false;
            evidence = default;
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

                int matchedPathIndex = -1;
                for (int index = firstAllowed; index <= lastAllowed; index++)
                {
                    if (path[index] == tile)
                    {
                        matchedPathIndex = index;
                        break;
                    }
                }
                bool isWalkable = _movementUseCase == null
                    || _movementUseCase.IsWalkable(tile);
                UnitTrajectoryCorridorSampleResult sampleResult =
                    UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        path.Count,
                        waypointIndex,
                        matchedPathIndex,
                        isWalkable);
                if (sampleResult == UnitTrajectoryCorridorSampleResult.OutsideCorridor
                    || sampleResult == UnitTrajectoryCorridorSampleResult.Blocked)
                {
                    Vector3 rootDomain = ViewConverter.FromView(fromView);
                    UnitPathSignature.TryCreate(
                        path, out UnitPathSignature sourcePathSignature);
                    evidence = new CorridorRepathEvidence
                    {
                        IsAvailable = true,
                        Frame = Time.frameCount,
                        RootView = fromView,
                        RootDomain = rootDomain,
                        RootHex = HexMetrics.WorldToHex(rootDomain),
                        UnitDataPosition = _unitData != null
                            ? _unitData.Position
                            : default,
                        PathCount = path.Count,
                        WaypointIndex = waypointIndex,
                        PathStart = path[0],
                        CurrentWaypoint = path[waypointIndex],
                        SourcePathSignature = sourcePathSignature,
                        SampleView = viewPoint,
                        SampleHex = tile,
                        SampleIndex = sample,
                        SampleCount = sampleCount,
                        MatchedPathIndex = matchedPathIndex,
                        FirstAllowedPathIndex = firstAllowed,
                        LastAllowedPathIndex = lastAllowed,
                        IsWalkable = isWalkable,
                        SampleResult = sampleResult
                    };
                    return false;
                }
                if (sampleResult == UnitTrajectoryCorridorSampleResult.StartTileEgress)
                    usedStartTileEgress = true;
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

            ObserveMovementAuthorityAdapterFailure("completion-no-intent-rejected",
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
            _activeRallyPoint = null;
            _rallyPolicyCompletionPending = false;
            MoveToInternal(path, UnitMovementIntentReason.AStarPath, startNewCommand: true);
        }

        /// <summary>
        /// 생산 유닛의 랠리 경유를 시작한다. 재경로는 실제 경로 끝이 아니라 이 깃발을
        /// 기준으로 다시 선택한다. 일반 MoveTo와 완료 정리가 문맥을 해제한다.
        /// </summary>
        public void MoveToRally(List<HexCoord> path, HexCoord rallyPoint)
        {
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            _activeRallyPoint = rallyPoint;
            _rallyPolicyCompletionPending = false;
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

            // 중복 command 판정은 AlignPathStartToTransform보다 먼저 수행한다. 이미 활성인 같은
            // 목표는 현재 코루틴과 도착-커밋 경계를 그대로 보존해야 한다.
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
                    if (_moveCoroutine != null
                        && _navigationObjective.CanQueueSuppressedPath(
                            path[path.Count - 1],
                            _navigationEnvironmentRevision))
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
            //     AlignPathStartToTransform이 이 "역방향 첫 스텝" 케이스만 감지해 현재 논리 타일에서
            //     앞쪽 인접 타일까지의 도착 segment와 그 이후 경로를 합성한다. 정방향/정합 상태는
            //     원본 path를 그대로 반환하므로 일반 이동은 전혀 건드리지 않는다.
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
                else if (_activeRallyPoint.HasValue)
                {
                    _navigationObjective?.RetargetRallyEndpoint(destination);
                }
            }

            // 여기까지 왔다는 것은 새 objective 또는 같은 objective의 명시적 segment 교체다.
            // 중복 command는 위에서 기존 코루틴을 보존한 채 반환했으므로, 이 시점부터만
            // 예약 상태를 비우고 기존 코루틴을 교체할 수 있다.
            _pendingPath = null;
            _currentNextTileCoord = null;

            if (_moveCoroutine != null)
            {
                EndHealPresentation("movement-replaced");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                PostCombatTraceEvent("movement-replaced", close: true);
#endif
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
        ///   2) 역방향이면 실제 transform 위치 기준 "앞쪽 가장 가까운 타일"을 구한다.
        ///      그 타일에서 목적지까지의 후속 경로는 상태를 바꾸지 않고 계산하고, 현재 논리 타일에서
        ///      앞쪽 타일까지를 첫 segment로 앞에 붙인다. Root가 실제로 그 waypoint에 도착한 뒤에만
        ///      MoveAlongPathV3의 기존 checkpoint/ProcessStep 경계가 도메인 위치와 점유를 커밋한다.
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
            if (!_movementUseCase.TryFindReachableForwardRejoinTile(
                    _unitData,
                    domainPos,
                    finalTarget,
                    out HexCoord forwardTile))
            {
                return path;
            }

            // 앞쪽 타일 이후의 경로만 계산한다. RequestMoveFrom은 UnitData.Position과 점유를
            // 변경하지 않는다. forwardTile이 최종 목표면 그 타일 하나가 후속 경로 전체다.
            List<HexCoord> routeFromForward = forwardTile == finalTarget
                ? new List<HexCoord> { forwardTile }
                : _movementUseCase.RequestMoveFrom(forwardTile, finalTarget);

            // 현재 논리 타일 → 앞쪽 인접 타일 → 후속 경로를 합성한다. 비인접 후보나 잘못된
            // 후속 경로는 원본을 유지해 fail-closed하며 논리 위치를 절대 미리 넘기지 않는다.
            if (!UnitPathStartTransitionPolicy.TryBuildArrivalCommittedPath(
                    _unitData.Position,
                    forwardTile,
                    routeFromForward,
                    out List<HexCoord> corrected))
            {
                return path;
            }

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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent("environment-invalidated");
#endif
            _navigationObjective?.NotifyEnvironmentChanged(
                _navigationEnvironmentRevision);

            // 랠리 목적지가 바뀌는 질의에서는 이전 환경의 예약 경로를 이월하지 않는다.
            // 전투 중이면 아래에서 새 예약 없이 복귀 경계의 최신 질의를 기다린다.
            if (_activeRallyPoint.HasValue)
                _pendingPath = null;

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
                _combatPursuitPathInvalidated = true;
                return;
            }

            HexCoord dest = _currentDestination;
            List<HexCoord> newPath = RequestLatestAuthoritativeRepath(dest, resolveRallyGoal: true);
            if (newPath == null || newPath.Count == 0
                || (!_activeRallyPoint.HasValue && newPath.Count < 2))
            {
                // 일반 실패는 기존 대기를 유지한다. 랠리 후보 소진은 별도 예약을 통해
                // 소유 코루틴이 안전하게 종료하므로 같은 null이라도 의미를 섞지 않는다.
                return;
            }
            if (_activeRallyPoint.HasValue && newPath.Count == 1)
            {
                // 같은 타일 안의 중심 완료도 기존 코루틴에서 소비한다.
                // 여기서 즉시 재시작하면 이미 중심에 있는 경우 완료 콜백이 재진입할 수 있다.
                _pendingPath = newPath;
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
        /// 재탐색은 오래된 논리 시작점이나 FlowField cache만 보지 않고, 현재 서버
        /// Simulation Root와 이 유닛이 관측한 최신 walkability revision을 함께 검증한다.
        /// typed 결과는 실패 terminal에 보존하며 성공 path만 기존 이동 writer에 전달한다.
        /// </summary>
        private List<HexCoord> RequestLatestAuthoritativeRepath(HexCoord destination,
            bool resolveRallyGoal = false)
        {
            if (_movementUseCase == null || _unitData == null)
            {
                _lastPathRequestResult = new UnitPathRequestResult(
                    UnitPathRequestStatus.ProviderFailure,
                    null,
                    _unitData != null ? _unitData.Position : default,
                    default,
                    destination,
                    _navigationEnvironmentRevision);
                return null;
            }

            Vector3 rootDomain = ViewConverter.FromView(transform.position);
            if (resolveRallyGoal && _activeRallyPoint.HasValue)
            {
                UnitRallyPathResult rally = _movementUseCase.RequestRallyPath(_unitData,
                    _activeRallyPoint.Value, rootDomain, _navigationEnvironmentRevision);
                _lastPathRequestResult = rally.Request;
                _rallyPolicyCompletionPending = rally.Status == UnitRallyPathStatus.NoReachableCandidate;
                if (_rallyPolicyCompletionPending)
                {
                    // 일반 경로 실패나 Blocked 상태를 정상 도착으로 처리하는 분기가 아니다.
                    // 후보 소진으로 랠리를 생략하는 정책을 코루틴 종료 경계에서 한 번 소비한다.
                    // 現在 Root を同一タイルの中心に偽装した singleton は作らない。
                    _pendingPath = null;
                    return null;
                }
                if (rally.Status == UnitRallyPathStatus.CurrentTileSelected
                    && CanCompleteWithinAuthoritativeTile(rally.Request.Goal))
                    return new List<HexCoord> { rally.Request.Goal };
                if (rally.Status != UnitRallyPathStatus.PathFound)
                    return null;
                return new List<HexCoord>(rally.Request.Path);
            }
            _lastPathRequestResult = _movementUseCase.RequestAuthoritativeRepath(
                _unitData,
                rootDomain,
                destination,
                _navigationEnvironmentRevision);
            SoloRecoveryEvidence("provider", _lastPathRequestResult.Status.ToString());
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            ObservePostCombatProvider();
#endif
            if (_lastPathRequestResult.Status == UnitPathRequestStatus.EmptySameTile
                && CanCompleteWithinAuthoritativeTile(destination))
                return new List<HexCoord> { destination };
            if (!_lastPathRequestResult.IsSuccess)
                return null;
            return _lastPathRequestResult.Path as List<HexCoord>
                ?? new List<HexCoord>(_lastPathRequestResult.Path);
        }

        /// <summary>
        /// 최신 권위 Root에서 앞쪽 route 중심까지 새로 찾은 prefix와, 그 중심 뒤에
        /// 남아 있는 원래 route suffix를 하나의 staged 경로로 결합한다. 이 메서드는
        /// UnitData/Transform을 쓰지 않으며, 결합 결과도 기존 이동 writer와 corridor
        /// preflight를 통과하기 전에는 적용되지 않는다.
        /// </summary>
        private static List<HexCoord> ComposePostCombatRecoveryRoute(
            IReadOnlyList<HexCoord> recoveredPrefix,
            IReadOnlyList<HexCoord> activeRoute,
            int nextWaypointIndex,
            HexCoord rejoinCandidate)
        {
            if (recoveredPrefix == null
                || recoveredPrefix.Count < 2
                || recoveredPrefix[recoveredPrefix.Count - 1] != rejoinCandidate
                || activeRoute == null
                || activeRoute.Count < 2
                || nextWaypointIndex < 1
                || nextWaypointIndex >= activeRoute.Count)
            {
                return null;
            }

            int rejoinIndex = -1;
            for (int index = nextWaypointIndex; index < activeRoute.Count; index++)
            {
                if (activeRoute[index] == rejoinCandidate)
                {
                    rejoinIndex = index;
                    break;
                }
            }
            if (rejoinIndex < 0)
                return null;

            var composed = new List<HexCoord>(
                recoveredPrefix.Count + activeRoute.Count - rejoinIndex - 1);
            for (int index = 0; index < recoveredPrefix.Count; index++)
                composed.Add(recoveredPrefix[index]);
            for (int index = rejoinIndex + 1; index < activeRoute.Count; index++)
                composed.Add(activeRoute[index]);

            return UnitPathSignature.TryCreate(composed, out _)
                ? composed
                : null;
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
        // singleton은 typed 중심 완료 의도이며 A* signature/반복 예산을 위조하지 않는다.
        private bool CanCompleteWithinAuthoritativeTile(HexCoord goal)
        {
            return _unitData != null && _unitData.IsAlive && goal == _unitData.Position
                && _movementUseCase != null && _movementUseCase.IsWalkable(goal)
                && HexMetrics.WorldToHex(ViewConverter.FromView(transform.position)) == goal;
        }

        private bool TryAcceptRepath(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> candidatePath,
            string source,
            out UnitRepathDecision decision)
        {
            if (candidatePath != null && candidatePath.Count == 1
                && _lastPathRequestResult.Status == UnitPathRequestStatus.EmptySameTile
                && CanCompleteWithinAuthoritativeTile(candidatePath[0]))
            {
                decision = UnitRepathDecision.AcceptedNextFrame;
                _navigationObjective?.MarkWaitingRepath();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                PostCombatTraceEvent("same-tile-center-intent");
#endif
                return true;
            }
            decision = guard != null
                ? guard.Evaluate(Time.frameCount, candidatePath)
                : UnitRepathDecision.RejectedInvalidPath;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent($"general-guard:{source}:{decision}");
#endif
            return TryAcceptRepathDecision(
                guard,
                candidatePath,
                source,
                decision);
        }

        /// <summary>
        /// spatial preflight가 recoverable unsafe로 거부한 직후의 전용 경계다.
        /// 같은 환경에서 현재 active path와 같은 후보를 다시 실행하지 않고 Blocked로
        /// 보류한다. 이 보류는 adapter failure가 아니며 환경 revision 변경만 재개시킨다.
        /// </summary>
        private bool TryAcceptSpatialRecoverableRepath(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> candidatePath,
            string source,
            out UnitRepathDecision decision)
        {
            // 랠리의 현재 후보 칸 안에서 중심으로 수렴하는 입력은 이미 존재하는
            // typed singleton 계약으로만 수용한다. 일반 spatial guard를 우회하지 않는다.
            if (_activeRallyPoint.HasValue && candidatePath != null && candidatePath.Count == 1)
                return TryAcceptRepath(guard, candidatePath, source, out decision);
            decision = guard != null
                ? guard.Evaluate(
                    Time.frameCount,
                    candidatePath,
                    UnitRepathEvaluationContext.SpatialRecoverable)
                : UnitRepathDecision.RejectedInvalidPath;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent($"spatial-guard:{source}:{decision}");
#endif
            if (decision == UnitRepathDecision.RejectedRepeatedPath)
            {
                // 동일 route는 이 단계에서 곧바로 영구 Blocked가 아니다. post-combat
                // recovery planner가 다음 전방 checkpoint를 평가하고, 전체 후보의 반복
                // 소진을 확인한 뒤에만 Blocked를 승인한다.
                _navigationObjective?.MarkWaitingRepath();
                return false;
            }

            return TryAcceptRepathDecision(
                guard,
                candidatePath,
                source,
                decision);
        }

        private bool TryAcceptRepathDecision(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> candidatePath,
            string source,
            UnitRepathDecision decision)
        {
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ObserveMovementAuthorityAdapterFailure(
            string kind,
            string context)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            UnitMovementAuthorityObserver.ObserveAdapterFailure(kind, context);
#endif
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
            string candidatePathStart = candidatePath != null
                && candidatePath.Count > 0
                    ? candidatePath[0].ToString()
                    : "unavailable";
            string corridorEvidence = source == "astar-corridor"
                ? FormatCorridorRepathEvidence(_lastCorridorRepathEvidence)
                : "not-applicable";
            ObserveMovementAuthorityAdapterFailure("repath-fail-closed",
                $"[UAS-MOVE][REPATH-FAIL-CLOSED] unitId={unitId}, " +
                $"commandRevision={_movementCommandRevision}, " +
                $"segmentRevision={_movementSegmentRevision}, " +
                $"source={source}, decision={decision}, frame={Time.frameCount}, " +
                $"failedWaypoint={failedWaypoint}, " +
                $"candidatePathStart={candidatePathStart}, " +
                $"previousPath={FormatPathSignature(guard?.PreviousPath ?? default)}, " +
                $"currentPath={FormatPathSignature(guard?.CurrentPath ?? default)}, " +
                $"candidatePath={FormatPathSignature(candidateSignature)}, " +
                $"acceptedInFrame={guard?.AcceptedInFrame ?? 0}, " +
                $"acceptedWithoutProgress={guard?.AcceptedWithoutProgress ?? 0}, " +
                $"pathStatus={_lastPathRequestResult.Status}, " +
                $"authoritativeStart={_lastPathRequestResult.AuthoritativeStart}, " +
                $"simulationRootTile={_lastPathRequestResult.RootTile}, " +
                $"goal={_lastPathRequestResult.Goal}, " +
                $"environmentRevision={_lastPathRequestResult.EnvironmentRevision}, " +
                $"corridorEvidence={corridorEvidence}");
        }

        private static string FormatCorridorRepathEvidence(
            CorridorRepathEvidence evidence)
        {
            if (!evidence.IsAvailable)
                return "unavailable";

            return
                $"frame:{evidence.Frame}|" +
                $"rootView:{FormatVector3(evidence.RootView)}|" +
                $"rootDomain:{FormatVector3(evidence.RootDomain)}|" +
                $"rootHex:{evidence.RootHex}|" +
                $"unitDataPosition:{evidence.UnitDataPosition}|" +
                $"sourcePathStart:{evidence.PathStart}|" +
                $"sourceWaypoint:{evidence.CurrentWaypoint}|" +
                $"sourceWaypointIndex:{evidence.WaypointIndex}/{evidence.PathCount - 1}|" +
                $"sourcePath:{FormatPathSignature(evidence.SourcePathSignature)}|" +
                $"sampleView:{FormatVector3(evidence.SampleView)}|" +
                $"sampleHex:{evidence.SampleHex}|" +
                $"sampleIndex:{evidence.SampleIndex}/{evidence.SampleCount}|" +
                $"matchedPathIndex:{evidence.MatchedPathIndex}|" +
                $"allowedPathIndices:{evidence.FirstAllowedPathIndex}-{evidence.LastAllowedPathIndex}|" +
                $"sampleWalkable:{evidence.IsWalkable}|" +
                $"sampleResult:{evidence.SampleResult}";
        }

        private static string FormatVector3(Vector3 value)
            => $"({value.x:F6},{value.y:F6},{value.z:F6})";

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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired
                && !_postCombatTrace.ProgressObserved)
            {
                _postCombatTrace.ProgressObserved = true;
                PostCombatTraceEvent("first-positive-navigation-progress");
            }
#endif
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
            if (path == null || path.Count == 0 || _unitData == null || !_unitData.IsAlive
                || (path.Count == 1 && !CanCompleteWithinAuthoritativeTile(path[0])))
            {
                MoveCleanupAndCompleteV3();
                yield break;
            }

            _rootRotationOwnership.TryAcquireMovement();

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
            // 전투 위치에서 기존 route로 돌아갈 때 단일 A* 실패를 영구 Blocked로
            // 바꾸지 않도록, 현재 이동 코루틴 수명 동안 복귀 후보 소진 이력을 보존한다.
            // 순수 planner는 후보만 고르고 실제 Root/path commit은 아래의 기존 writer와
            // corridor preflight가 계속 담당한다.
            var postCombatRecoveryPlanner = new UnitPostCombatRecoveryPlanner();
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
                // 랠리 후보가 없거나 현재 권위 타일을 후보로 고른 정책 결과만 여기서
                // 정상 종료한다. 일반 경로 실패/Blocked는 이 조건을 세우지 않는다.
                if (_rallyPolicyCompletionPending) break;
                // 여러 frame의 무진전 상한 또는 invalid path에 도달하면 코루틴을 종료하지 않는다.
                // 종료하면 IsMoving이 false가 되어 공성 ticker가 같은 목표를 새 command로 재발행하고
                // guard가 초기화된다. 현재 pose와 코루틴을 유지한 채 walkability revision 또는
                // OnPathInvalidated가 제공한 새 PendingPath만 기다린다.
                if (navigationBlocked)
                {
                    SoloRecoveryEvidence("blocked", "navigation-blocked-loop");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    ObservePostCombatBlocked(repathGuard, -1, "navigation-blocked-loop");
#endif
                    SetMovementHeldAnimation(true);
                    ulong blockedEnvironmentRevision =
                        _navigationEnvironmentRevision;
                    while (_unitData != null
                        && _unitData.IsAlive
                        && _pendingPath == null
                        && !_rallyPolicyCompletionPending
                        && _navigationEnvironmentRevision
                            == blockedEnvironmentRevision)
                    {
                        yield return null;
                    }

                    if (_unitData == null || !_unitData.IsAlive || _rallyPolicyCompletionPending)
                        break;

                    List<HexCoord> recoveryPath = _pendingPath;
                    _pendingPath = null;
                    if (recoveryPath == null || recoveryPath.Count < 2)
                    {
                        recoveryPath = RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                        if (_rallyPolicyCompletionPending) goto cleanup;
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    PostCombatTraceEvent("blocked-environment-path-accepted");
#endif
                    _navigationObjective?.MarkWaitingRepath();

                    // 수락한 재경로는 반드시 다음 frame부터 planner에 입력한다.
                    yield return null;
                }

                if (_activeRallyPoint.HasValue && path != null && path.Count > 0)
                {
                    // 새 경로가 수용돼 outer loop에 들어온 시점에 경로/목적지/objective를
                    // 함께 맞춘다. 같은 guard 객체와 환경 revision은 그대로 유지한다.
                    finalTarget = path[path.Count - 1];
                    _currentDestination = finalTarget;
                    _navigationObjective?.RetargetRallyEndpoint(finalTarget);
                    if (path.Count == 1 && _pendingPath != null && _pendingPath.Count == 1)
                        _pendingPath = null;
                }
                _navigationObjective?.MarkNavigating();
                SetMovementHeldAnimation(false);

                if (path.Count == 1)
                {
                    Vector3 center = ViewConverter.ToView(HexMetrics.HexToWorld(finalTarget));
                    center.y += HexMetrics.UnitYOffset;
                    // 이 배열은 경로가 아닌 한 타일 내부 sweep의 두 끝점이다.
                    var centerCorridor = new List<HexCoord> { finalTarget, finalTarget };
                    BeginMovementShadowSegment(UnitMovementIntentReason.PostCombatResume);
                    while (_unitData != null && _unitData.IsAlive
                        && CalculateMovementPositionDeltaXZ(transform.position, center)
                            > (float)UnitMovementEvaluationAdapter.PositionTolerance)
                    {
                        if (ShouldEngage())
                        {
                            yield return EnterCombatPursuitV3();
                            if (!CanCompleteWithinAuthoritativeTile(finalTarget))
                            {
                                path = RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                                if (_rallyPolicyCompletionPending) goto cleanup;
                                navigationBlocked = path == null;
                                break;
                            }
                            BeginMovementShadowSegment(UnitMovementIntentReason.PostCombatResume);
                        }
                        if (!CanCompleteWithinAuthoritativeTile(finalTarget))
                        {
                            navigationBlocked = true;
                            break;
                        }
                        float multiplier = _combatUseCase != null
                            ? Mathf.Max(0f, _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData)) : 1f;
                        SetFrozenAnimation(multiplier <= MoveFreezeEpsilon);
                        Vector3 centerCandidate = Vector3.MoveTowards(transform.position, center,
                            worldSpeed * multiplier * Time.deltaTime);
                        Quaternion centerRotation = transform.rotation;
                        Vector3 centerDirection = center - transform.position;
                        centerDirection.y = 0f;
                        if (centerDirection.sqrMagnitude > 0f)
                            centerRotation = Quaternion.LookRotation(centerDirection);
                        MovementWriteOutcome outcome = ApplyMovementAuthorityFrame(center,
                            centerCandidate, centerRotation, UnitMovementIntentReason.PostCombatResume,
                            false, maximumTravelDistanceWorld: worldSpeed * multiplier * Time.deltaTime,
                            logicalCorridorPath: centerCorridor, logicalCorridorWaypointIndex: 1);
                        if (outcome == MovementWriteOutcome.Rejected || outcome == MovementWriteOutcome.RepathRequired)
                        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            CapturePostCombatTrace(path, centerCorridor, 1, finalTarget, false, outcome);
#endif
                            navigationBlocked = true;
                            break;
                        }
                        yield return null;
                    }
                    if (navigationBlocked) continue;
                    if (path != null && path.Count > 1) { yield return null; continue; }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    PostCombatTraceEvent("same-tile-center-completed");
#endif
                    SoloRecoveryEvidence("completed", "same-tile-center-completed");
                    break;
                }

                // 새 path를 받아 다시 시작할지 결정하는 플래그.
                // for 안에서 true로 바꾼 뒤 break하면 외부 while로 빠져 새 path로 continue.
                bool needRepath = false;

                // 멀티 Legacy만 중심 도착의 from을 유지하고 SOLO/Reducer는 실제 공간 커밋을 따른다.
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
                    // [A* 경로 입력] 모든 waypoint는 타일 중심의 권위 checkpoint다.
                    // 현재 점과 다음 점으로 corridor 안의 연속 trajectory를 만들되,
                    // 중간·마지막 목적지 모두 실제 중심 도달 뒤에만 소비한다.
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
                            List<HexCoord> blockedRepath =
                                RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                            if (_rallyPolicyCompletionPending) goto cleanup;
                            if (!TryAcceptSpatialRecoverableRepath(
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
                            ObserveMovementAuthorityAdapterFailure("authority-frame-rejected",
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
                                _rootRotationOwnership.TransferToAction();
                                yield return EnterHealLoopV3();
                                _rootRotationOwnership.ReleaseAction(
                                    resumeMovement: _unitData != null
                                        && _unitData.IsAlive);
                            }
                            else
                            {
                                // [전투 이동] → [공격] 흐름을 EnterCombatPursuitV3에 전부 위임.
                                yield return EnterCombatPursuitV3();

                                // 전투 종료 후 회전 정합성 유지.
                                // _combatTargetTransform이 null이 아닌 동안 Update()가 매 프레임 회전을 덮어쓰므로
                                // 전투 추격 내부의 gameplay 종료 경계가 추적과 소유권을 직접 닫는다.
                            }

                            if (_unitData == null || !_unitData.IsAlive) break;

                            // Chase의 실제 Root 전이가 Missing/NonWalkable로 무효화된 경우에는
                            // Rejected cleanup이나 복귀 정렬을 수행하지 않는다. 추격 코루틴이 이미
                            // 최소 한 frame held했으므로 여기서 기존 objective/guard를 통해 새 A*를
                            // 받고 다음 외부 while부터 적용한다.
                            if (_combatPursuitRequiresRepath)
                            {
                                _combatPursuitRequiresRepath = false;
                                List<HexCoord> pursuitRepath =
                                    RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                                if (_rallyPolicyCompletionPending) goto cleanup;
                                if (!TryAcceptSpatialRecoverableRepath(
                                        repathGuard,
                                        pursuitRepath,
                                        "chase-spatial-route-invalidated",
                                        out UnitRepathDecision pursuitRepathDecision))
                                {
                                    navigationBlocked = true;
                                    needRepath = true;
                                    _currentNextTileCoord = null;
                                    break;
                                }

                                BeginMovementShadowSegment(
                                    UnitMovementIntentReason.BlockedRepath);
                                path = pursuitRepath;
                                pathCheckpoint.Reset();
                                needRepath = true;
                                _isAStarMoving = true;
                                SetMovementHeldAnimation(true);
                                _currentNextTileCoord = null;
                                break;
                            }

                            // 전투 후 복귀 중 대상을 다시 발견해도 이 MoveAlongPathV3 수명은
                            // 끝내지 않는다. 아래 순수 flow가 재추격과 새 복귀 계산 사이에
                            // 최소 한 frame 경계를 강제하여, 즉시 반환되는 추격에서도 같은
                            // frame 무한 반복이나 objective 조기 Complete가 생기지 않게 한다.
                            var postCombatReentryFlow = new UnitPostCombatReentryFlow();
                            UnitPostCombatReentryResult recoveryStart =
                                postCombatReentryFlow.BeginRecovery(Time.frameCount);
                            if (recoveryStart.Action
                                != UnitPostCombatReentryAction.RecalculateRecovery)
                            {
                                ObserveMovementAuthorityAdapterFailure(
                                    "post-combat-reentry-start-invalid",
                                    $"[UAS-MOVE] 전투 후 복귀 수명 시작이 유효하지 않습니다. " +
                                    $"unitId={_unitData.Id}, frame={Time.frameCount}");
                                movementFailedClosed = true;
                                goto cleanup;
                            }

                            BeginSoloRecoveryEvidence();
                        recalculatePostCombatRecovery:
                            BeginMovementShadowSegment(UnitMovementIntentReason.PostCombatResume);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            BeginPostCombatTrace(path, i, finalTarget);
#endif
                            // ──────────────────────────────────────────────────────────
                            // [BUG-002 수정 — 2026-05-13] 전투 종료 → 앞쪽 타일까지 "걸어서" 정렬.
                            //
                            //   왜 필요한가:
                            //     전투는 타일 중심 밖에서 끝날 수 있다. 현재 SOLO/Reducer는 실제
                            //     경계 전이를 이미 커밋하지만, 경로의 중심 checkpoint는 별개다.
                            //     현재 pose에서 안전한 복귀 중심까지 걸어간 뒤 경로를 이어야 한다.
                            //
                            //   해결:
                            //     1) walkable·전방·도달 가능 조건을 만족하는 인접 중심을 1회 결정.
                            //     2) 그 타일 중심 뷰 좌표(alignView)까지 동일 이동 속도로 Lerp.
                            //        (규칙 5: A* 이동과 동일 속도 — moveSeconds는 1칸 이동 시간이므로
                            //         실제 거리 / TileHeight 비율을 곱해 시간 환산.)
                            //     3) Lerp 도중에도 detect 사거리 적 감지 시 즉시 중단 → 새 추격 진입.
                            //     4) planner가 남은 이동량을 중심까지 제한해 실제로 도달.
                            //     5) 같은 forwardTile을 ResumeFromForwardTileV3에 전달해 도메인 갱신 +
                            //        새 path 발급. (복귀 후보 이중 계산 방지)
                            // ──────────────────────────────────────────────────────────

                            // 1) 안전한 forwardTile 계산 — 호출은 단 1회. 후보가 없으면
                            // nearestTile을 성공으로 오인하지 않고 현재 objective의 서버 A*를
                            // 재평가한다.
                            Vector3 unitDomainPos = ViewConverter.FromView(transform.position);
                            if (!_movementUseCase.TryFindReachableForwardRejoinTile(
                                    _unitData,
                                    unitDomainPos,
                                    finalTarget,
                                    out HexCoord forwardTile,
                                    path))
                            {
                                List<HexCoord> safeResumePath =
                                    RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                                if (_rallyPolicyCompletionPending) goto cleanup;
                                // 직접 중심 후보 부재는 spatial unsafe 증거가 아니다.
                                // 유효 동일 route는 다음 frame의 최신 pose에서 안전 검사한다.
                                if (!TryAcceptRepath(
                                        repathGuard,
                                        safeResumePath,
                                        "post-combat-no-safe-forward-center",
                                        out UnitRepathDecision safeResumeDecision))
                                {
                                    navigationBlocked = safeResumeDecision != UnitRepathDecision.RejectedFrameBudget;
                                }

                                path = safeResumePath;
                                pathCheckpoint.Reset();
                                needRepath = true;
                                SetMovementHeldAnimation(true);
                                _currentNextTileCoord = null;
                                break;
                            }

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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            if (_postCombatTrace != null && !_postCombatTrace.Retired)
                            {
                                _postCombatTrace.Center = alignView;
                                _postCombatTrace.HasCenter = true;
                            }
                            CapturePostCombatTrace(path, resumeCorridor, i, finalTarget, false, MovementWriteOutcome.Held);
#endif

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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                    // 재탐색 전에 최종 corridor 판정을 저장하고 최초 실패를 내보낸다.
                                    if (alignOutcome == MovementWriteOutcome.RepathRequired
                                        || alignOutcome == MovementWriteOutcome.Rejected
                                        || shouldReengage || _lastMovementCandidateAcquiredTarget || _lastMovementTrajectoryReachedWaypoint)
                                        CapturePostCombatTrace(path, resumeCorridor, i, finalTarget,
                                            shouldReengage || _lastMovementCandidateAcquiredTarget, alignOutcome);
#endif
                                    if (alignOutcome == MovementWriteOutcome.RepathRequired)
                                    {
                                        // 건물 등으로 통로가 바뀌면 기존 route의 앞쪽 중심을
                                        // 결정적인 순서로 frame당 하나씩 질의한다. 한 번의
                                        // Unreachable은 Waiting일 뿐이며, 같은 환경/Root에서
                                        // 전체 후보를 두 차례 소진한 경우에만 Blocked를 확정한다.
                                        List<HexCoord> blockedResumePath = null;
                                        UnitPostCombatRecoveryResult recovery = default;
                                        while (_unitData != null && _unitData.IsAlive)
                                        {
                                            HexCoord recoveryRoot = HexMetrics.WorldToHex(
                                                ViewConverter.FromView(transform.position));
                                            recovery = postCombatRecoveryPlanner.Evaluate(
                                                Time.frameCount,
                                                _navigationEnvironmentRevision,
                                                recoveryRoot,
                                                path,
                                                i,
                                                finalTarget,
                                                candidate =>
                                                {
                                                    List<HexCoord> candidatePrefix =
                                                        RequestLatestAuthoritativeRepath(candidate);
                                                    if (!UnitPathSignature.TryCreate(
                                                            candidatePrefix,
                                                            out UnitPathSignature candidateSignature)
                                                        || candidateSignature
                                                            == repathGuard.CurrentPath)
                                                    {
                                                        return null;
                                                    }
                                                    return candidatePrefix;
                                                });
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                            ObservePostCombatPlanner(recovery);
#endif

                                            if (recovery.Status
                                                == UnitPostCombatRecoveryStatus.Recovered)
                                            {
                                                blockedResumePath =
                                                    ComposePostCombatRecoveryRoute(
                                                        recovery.Path,
                                                        path,
                                                        i,
                                                        recovery.Candidate);
                                                bool composedPathIsNew =
                                                    UnitPathSignature.TryCreate(
                                                        blockedResumePath,
                                                        out UnitPathSignature composedSignature)
                                                    && composedSignature
                                                        != repathGuard.CurrentPath;
                                                if (!composedPathIsNew)
                                                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                                    PostCombatTraceEvent("planner-composed-route-not-new");
#endif
                                                    postCombatRecoveryPlanner
                                                        .RejectStagedPath();
                                                    _navigationObjective?.MarkWaitingRepath();
                                                    SetMovementHeldAnimation(true);
                                                    yield return null;
                                                    continue;
                                                }

                                                if (TryAcceptSpatialRecoverableRepath(
                                                        repathGuard,
                                                        blockedResumePath,
                                                        "post-combat-corridor",
                                                        out UnitRepathDecision recoveryDecision))
                                                {
                                                    postCombatRecoveryPlanner
                                                        .AcceptStagedPath();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                                    PostCombatTraceEvent("planner-path-accepted");
#endif
                                                    break;
                                                }

                                                // A* prefix가 존재해도 전체 route가 이전 unsafe
                                                // path와 같거나 guard가 같은 frame 적용을 거부하면
                                                // 성공으로 초기화하지 않고 다음 후보를 본다.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                                PostCombatTraceEvent($"planner-guard-rejected-{recoveryDecision}");
#endif
                                                postCombatRecoveryPlanner
                                                    .RejectStagedPath();
                                                if (recoveryDecision
                                                        == UnitRepathDecision.RejectedRepeatedPath
                                                    || recoveryDecision
                                                        == UnitRepathDecision.RejectedFrameBudget)
                                                {
                                                    _navigationObjective?.MarkWaitingRepath();
                                                    SetMovementHeldAnimation(true);
                                                    yield return null;
                                                    continue;
                                                }

                                                navigationBlocked = true;
                                                break;
                                            }

                                            if (recovery.Status
                                                == UnitPostCombatRecoveryStatus.ConfirmedBlocked)
                                            {
                                                _navigationObjective?.MarkBlocked();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                                ObservePostCombatBlocked(repathGuard, recovery.CompletedSweeps, "planner-confirmed-manual-label-not-guard-limit");
#endif
                                                LogRepathFailClosed(
                                                    repathGuard,
                                                    null,
                                                    "post-combat-corridor-confirmed",
                                                    UnitRepathDecision.RejectedNoProgressBudget,
                                                    recovery.Candidate.ToString());
                                                navigationBlocked = true;
                                                break;
                                            }

                                            if (recovery.Status
                                                == UnitPostCombatRecoveryStatus.Invalid)
                                            {
                                                ObserveMovementAuthorityAdapterFailure(
                                                    "post-combat-recovery-invalid",
                                                    $"[UAS-MOVE] 전투 후 복귀 planner 입력/수명이 " +
                                                    $"유효하지 않습니다. unitId={_unitData.Id}, " +
                                                    $"frame={Time.frameCount}");
                                                movementFailedClosed = true;
                                                goto cleanup;
                                            }

                                            _navigationObjective?.MarkWaitingRepath();
                                            SetMovementHeldAnimation(true);
                                            yield return null;
                                        }

                                        if (_unitData == null || !_unitData.IsAlive)
                                            break;

                                        if (navigationBlocked)
                                        {
                                            needRepath = true;
                                            _currentNextTileCoord = null;
                                            break;
                                        }

                                        BeginMovementShadowSegment(
                                            UnitMovementIntentReason.BlockedRepath);
                                        path = blockedResumePath;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                        CapturePostCombatTrace(path, resumeCorridor, i, finalTarget, false, MovementWriteOutcome.Held);
                                        PostCombatTraceEvent("blocked-route-installed");
#endif
                                        pathCheckpoint.Reset();
                                        needRepath = true;
                                        SetMovementHeldAnimation(true);
                                        break;
                                    }
                                    if (alignOutcome == MovementWriteOutcome.Rejected)
                                    {
                                        ObserveMovementAuthorityAdapterFailure("resume-align-rejected",
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                            if (resumeWaypointReached)
                                            {
                                                UnitMovementAuthorityObserver.ObserveCenterCheckpoint(
                                                    CalculateMovementShadowPositionDeltaXZ(
                                                        transform.position,
                                                        alignView));
                                                PostCombatTraceEvent("center-checkpoint");
                                            }
#endif
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
                                        PostCombatTraceEvent("reengage-priority", close: true);
#endif
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

                            // 정렬 Lerp 도중 새 대상이 감지된 경우에는 외부 while와 cleanup으로
                            // 빠지지 않고 현재 MoveAlongPathV3 안에서 즉시 역할별 행동에 재진입한다.
                            // 행동이 끝난 뒤에는 현재 Root/Domain으로 이 블록의 모든 지역값을
                            // 다시 계산하므로, 이전 forwardTile/alignView를 재사용하지 않는다.
                            if (alignInterruptedByCombat)
                            {
                                UnitPostCombatReentryResult reentry =
                                    postCombatReentryFlow.ObserveTargetAcquired(
                                        Time.frameCount);
                                if (reentry.Action
                                    != UnitPostCombatReentryAction.EnterPursuit)
                                {
                                    ObserveMovementAuthorityAdapterFailure(
                                        "post-combat-reentry-transition-invalid",
                                        $"[UAS-MOVE] 복귀 중 행동 재진입 전이가 유효하지 않습니다. " +
                                        $"unitId={_unitData.Id}, frame={Time.frameCount}");
                                    movementFailedClosed = true;
                                    goto cleanup;
                                }

                                _isAStarMoving = false;
                                _currentNextTileCoord = null;
                                bool reentryWasHealer = _unitData.IsHealer;
                                if (reentryWasHealer)
                                {
                                    // 힐러는 적 추격으로 보내지 않고 기존 부상 아군 힐 루프를
                                    // 그대로 사용한다. Action 회전 소유권 인계/반환 순서도
                                    // 최초 A* 감지 경계와 동일하게 보존한다.
                                    _rootRotationOwnership.TransferToAction();
                                    yield return EnterHealLoopV3();
                                    _rootRotationOwnership.ReleaseAction(
                                        resumeMovement: _unitData != null
                                            && _unitData.IsAlive);
                                }
                                else
                                {
                                    yield return EnterCombatPursuitV3();
                                }

                                bool unitAliveAfterReentry = _unitData != null
                                    && _unitData.IsAlive;
                                UnitPostCombatReentryResult pursuitCompleted =
                                    postCombatReentryFlow.ObservePursuitCompleted(
                                        Time.frameCount,
                                        unitAliveAfterReentry,
                                        reentryWasHealer,
                                        _combatPursuitRequiresRepath);
                                if (!unitAliveAfterReentry)
                                    break;
                                if (pursuitCompleted.Action
                                    == UnitPostCombatReentryAction
                                        .RepathBeforeRecovery)
                                {
                                    // 최초 전투 진입 경계와 동일하게 Chase가 남긴 공간 재경로
                                    // 요구를 recovery 계산보다 먼저 소비한다. 그렇지 않으면 stale
                                    // flag와 전투 전 forwardTile/alignView가 복귀 판단에 섞인다.
                                    _combatPursuitRequiresRepath = false;
                                    List<HexCoord> reentryPursuitRepath =
                                        RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                                    if (_rallyPolicyCompletionPending) goto cleanup;
                                    if (!TryAcceptSpatialRecoverableRepath(
                                            repathGuard,
                                            reentryPursuitRepath,
                                            "post-combat-reentry-chase-spatial-route-invalidated",
                                            out UnitRepathDecision reentryPursuitRepathDecision))
                                    {
                                        navigationBlocked = true;
                                        needRepath = true;
                                        _currentNextTileCoord = null;
                                        break;
                                    }

                                    BeginMovementShadowSegment(
                                        UnitMovementIntentReason.BlockedRepath);
                                    path = reentryPursuitRepath;
                                    pathCheckpoint.Reset();
                                    needRepath = true;
                                    _isAStarMoving = true;
                                    SetMovementHeldAnimation(true);
                                    _currentNextTileCoord = null;
                                    break;
                                }
                                if (pursuitCompleted.Action
                                    != UnitPostCombatReentryAction
                                        .DeferRecoveryUntilNextFrame)
                                {
                                    ObserveMovementAuthorityAdapterFailure(
                                        "post-combat-reentry-completion-invalid",
                                        $"[UAS-MOVE] 재진입 행동 종료 전이가 유효하지 않습니다. " +
                                        $"unitId={_unitData.Id}, frame={Time.frameCount}");
                                    movementFailedClosed = true;
                                    goto cleanup;
                                }

                                UnitPostCombatReentryResult nextRecovery;
                                do
                                {
                                    nextRecovery = postCombatReentryFlow.BeginRecovery(
                                        Time.frameCount);
                                    if (nextRecovery.Action
                                        == UnitPostCombatReentryAction
                                            .DeferRecoveryUntilNextFrame)
                                    {
                                        yield return null;
                                    }
                                }
                                while (nextRecovery.Action
                                    == UnitPostCombatReentryAction
                                        .DeferRecoveryUntilNextFrame);

                                if (nextRecovery.Action
                                    != UnitPostCombatReentryAction.RecalculateRecovery)
                                {
                                    ObserveMovementAuthorityAdapterFailure(
                                        "post-combat-recovery-restart-invalid",
                                        $"[UAS-MOVE] 재진입 뒤 새 복귀 계산 전이가 유효하지 않습니다. " +
                                        $"unitId={_unitData.Id}, frame={Time.frameCount}");
                                    movementFailedClosed = true;
                                    goto cleanup;
                                }

                                // 이전 복귀 후보의 소진/성공 staged 상태 역시 현재 전투 위치와
                                // 무관해졌으므로 함께 폐기한다. 아래 label부터 Root/Domain,
                                // forwardTile, alignView, corridor를 전부 새로 계산한다.
                                postCombatRecoveryPlanner.Reset();
                                goto recalculatePostCombatRecovery;
                            }

                            // 4) 최종 부동소수점 보정도 같은 writer seam을 통과한다.
                            MovementWriteOutcome alignEndpointOutcome =
                                ApplyMovementAuthorityFrame(
                                alignView,
                                alignView,
                                transform.rotation,
                                UnitMovementIntentReason.PostCombatResume,
                                targetAcquirePriority: false);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            CapturePostCombatTrace(path, resumeCorridor, i, finalTarget, false, alignEndpointOutcome);
#endif
                            if (alignEndpointOutcome == MovementWriteOutcome.Rejected)
                            {
                                ObserveMovementAuthorityAdapterFailure("resume-endpoint-rejected",
                                    $"[UAS-MOVE] 권위 복귀 endpoint 거부. unitId={_unitData.Id}");
                                movementFailedClosed = true;
                                goto cleanup;
                            }

                            // 5) 같은 forwardTile로 도메인 갱신 + 새 path 발급.
                            List<HexCoord> resumePath = ResumeFromForwardTileV3(finalTarget, forwardTile);
                            if (_rallyPolicyCompletionPending) goto cleanup;
                            if (resumePath != null && resumePath.Count > 0)
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                CapturePostCombatTrace(path, resumeCorridor, i, finalTarget, false, MovementWriteOutcome.Held);
                                PostCombatTraceEvent("post-combat-route-installed");
#endif
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
                    // route checkpoint도 아직 소비하면 안 된다. 외부 while에서
                    // 새 path index 1부터 다시 시작한다.
                    if (needRepath) break;

                    // [전투 이동]으로 중단된 경우: toPos 강제 스냅과 ProcessStep을 건너뛰고
                    // for 루프를 빠져나가 외부 while에서 새 path로 재진입.
                    if (interruptedByCombat)
                    {
                        break;
                    }

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] 정상 waypoint 완료.
                    // ──────────────────────────────────────────────────────
                    // Legacy Lerp만 기존 타일 중심 endpoint 보정을 유지한다. Reducer trajectory는
                    // planner의 이동량 제한으로 중심에 도달하며 별도 위치 스냅을 하지 않는다.
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
                            ObserveMovementAuthorityAdapterFailure("movement-endpoint-rejected",
                                $"[UAS-MOVE] 권위 이동 endpoint 거부. unitId={_unitData.Id}");
                            movementFailedClosed = true;
                            goto cleanup;
                        }
                    }

                    // Reducer 경로의 waypoint 완료는 route checkpoint만 소비한다.
                    // 실제 Domain 위치와 혼잡도는 매 frame Root 경계 전이 시 공용 writer가 이미 커밋했다.
                    if (reducerTrajectory
                        && !pathCheckpoint.TryConsume(
                            i,
                            authoritativeWaypointReached))
                    {
                        ObserveMovementAuthorityAdapterFailure("trajectory-checkpoint-rejected",
                            $"[UAS-MOVE] trajectory waypoint 순서 거부. " +
                            $"unitId={_unitData.Id}, index={i}, " +
                            $"expected={pathCheckpoint.NextWaypointIndex}");
                        movementFailedClosed = true;
                        goto cleanup;
                    }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (reducerTrajectory)
                    {
                        UnitMovementAuthorityObserver.ObserveCenterCheckpoint(
                            CalculateMovementShadowPositionDeltaXZ(
                                transform.position,
                                toPos));
                    }
#endif
                    ObserveNavigationProgress(repathGuard, path);
                    if (!reducerTrajectory && NetworkContext.IsNetworkActive)
                    {
                        // 멀티 Legacy rollback만 기존 중심 도착 의미를 보존한다.
                        // SOLO는 공용 writer가 실제 경계에서 이미 커밋하므로 중복 발행하지 않는다.
                        if (_movementUseCase != null
                            && !(isLastStep && isLastStepToNonWalkable))
                        {
                            _movementUseCase.ProcessStep(_unitData, from, to);
                        }

                        if (_isAStarMoving)
                        {
                            GameEvents.OnUnitEnteredTile.OnNext(
                                new UnitEnteredTileEvent(_unitData.Id, to));
                        }
                    }

                    prevActualTile = reducerTrajectory || !NetworkContext.IsNetworkActive
                        ? _unitData.Position
                        : to;

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
                            List<HexCoord> refreshed =
                                RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                            if (_rallyPolicyCompletionPending) goto cleanup;
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
            // 랠리 생략 예약을 먼저 소비해야 마지막 NoIntent 게시도 이동 작성 경계를
            // 실제로 통과한다. 이전 경로를 막는 예약 flag로 종료 게시까지 막지 않는다.
            _rallyPolicyCompletionPending = false;
            if (_activeRallyPoint.HasValue && _pendingPath != null && _pendingPath.Count == 1)
                _pendingPath = null;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent(_unitData == null || !_unitData.IsAlive ? "death-cleanup" :
                movementFailedClosed ? "cancelled-cleanup" : "completion-cleanup", close: true);
#endif
            // 정상/사망/완주/RESUME 실패 등 모든 종료 경로에서 공통 cleanup.
            //   여기서 _moveCoroutine을 null로 비우고 OnMoveComplete 콜백(랠리→성 자동 이동/공성 체인)을
            //   발행한다. 콜백이 새 이동(MoveTo)을 시작하면 _moveCoroutine이 다시 non-null이 된다.
            if (_unitData != null && _unitData.IsAlive)
            {
                bool closed = TryCloseMovementAuthorityIntent(
                    UnitMovementIntentReason.AStarPath,
                    "path-cleanup");
                if (_activeRallyPoint.HasValue)
                {
                    // 랠리 경유 종료는 실제 종료 게시가 확인된 경우에만 성 진군으로 잇는다.
                    // Reducer 모드에서는 반환값뿐 아니라 최신 수용 상태도 함께 확인한다.
                    if (_lockedMovementPipelineMode == UnitMovementPipelineMode.ReducerAuthoritative)
                    {
                        closed = closed && _movementAuthorityReducer != null
                            && _movementAuthorityReducer.Snapshot.HasAcceptedObservation
                            && _movementAuthorityReducer.Snapshot.Phase == UnitMovementPhase.NoIntent
                            && _movementAuthorityReducer.Snapshot.CommandRevision == _movementCommandRevision
                            && _movementAuthorityReducer.Snapshot.SegmentRevision == _movementSegmentRevision;
                    }
                    if (!closed) movementFailedClosed = true;
                }
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
        ///      - 그 외(detect 안, 사거리 밖) → 같은 타일은 안전한 직선, 다른 타일은
        ///        타겟 접근 서버 경로로 이동.
        ///
        /// 슬롯/유닛 점유 차단 없음 — 같은 적을 향해 여러 유닛이 같은 위치로 모일 수 있습니다.
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
            _combatPursuitRequiresRepath = false;
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _postCombatLastTarget = targetId;
            _postCombatExitReason = "pursuit-active";
            PostCombatTraceEvent("pursuit-target-selected");
#endif
            bool targetIsUnit = firstTarget.Value.isUnit;
            BeginSoloPursuitEvidence(targetId, targetIsUnit);

            // 기본 이동 속도. 연구·둔화·빙결 배율은 아래 frame 루프에서 다시 읽는다.
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;
            float worldSpeed = HexMetrics.TileHeight / moveSeconds;

            // Chase도 A*와 같은 logical corridor를 사용한다. 타겟 타일 또는 환경 revision이
            // 바뀔 때만 경로를 갱신하여 움직이는 타겟 때문에 매 frame FlowField를 요청하거나,
            // 동일한 막힌 후보를 무한 반복하지 않게 한다.
            List<HexCoord> pursuitPath = null;
            int pursuitWaypointIndex = 1;
            HexCoord pursuitTargetTile = default;
            bool hasPursuitTargetTile = false;
            ulong pursuitEnvironmentRevision = _navigationEnvironmentRevision;
            var pursuitCheckpoint = new UnitPathCheckpointTracker();
            int pursuitRouteTargetId = targetId;
            bool pursuitRouteTargetIsUnit = targetIsUnit;
            _combatPursuitPathInvalidated = false;

            // 매 프레임 루프 — 적 상태 확인 + 전투 이동 또는 공격으로 분기.
            while (_unitData != null && _unitData.IsAlive)
            {
                // ── 1) 적의 최신 월드 좌표 조회 ──
                ObserveSoloPursuitStall();
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        _postCombatExitReason = "pursuit-target-lost";
                        PostCombatTraceEvent("pursuit-target-lost");
#endif
                        CloseSoloPursuitEvidence("close-target-lost");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    targetId = next.Value.id;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    _postCombatLastTarget = targetId;
                    PostCombatTraceEvent("pursuit-target-reselected");
#endif
                    targetIsUnit = next.Value.isUnit;
                    CloseSoloPursuitEvidence("close-target-replaced");
                    BeginSoloPursuitEvidence(targetId, targetIsUnit);
                    pursuitPath = null;
                    pursuitWaypointIndex = 1;
                    pursuitCheckpoint.Reset();
                    hasPursuitTargetTile = false;

                    yield return null;
                    continue;
                }

                // ── 3) 적이 detect 사거리 밖으로 이탈 → 종료 ──
                if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                {
                    TryCloseMovementAuthorityIntent(
                        UnitMovementIntentReason.Chase,
                        "pursuit-detect-range-lost");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    _postCombatExitReason = "pursuit-detect-range-lost";
                    PostCombatTraceEvent("pursuit-detect-range-lost");
#endif
                    CloseSoloPursuitEvidence("close-detect-lost");
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
                        targetAcquirePriority: true,
                        targetAcquirePresentationHandoff: () =>
                            TryBeginServerAttackEntryBeforeRootStop(
                                targetId,
                                targetIsUnit));
                    if (acquireOutcome == MovementWriteOutcome.DeferredAttackHandoff)
                    {
                        // Reducer/Root/NoIntent는 아직 전혀 커밋되지 않았다. 현재 Walk pose를
                        // 보존하고 다음 정상 frame에 동일한 direct-in-range 진입을 재시도한다.
                        yield return null;
                        continue;
                    }
                    if (acquireOutcome == MovementWriteOutcome.Rejected)
                    {
                        ObserveMovementAuthorityAdapterFailure("target-acquire-no-intent-rejected",
                            $"[UAS-MOVE] 공격 범위 진입 NoIntent 게시 거부. unitId={_unitData.Id}");
                        CloseSoloPursuitEvidence("failure-attack-entry");
                        _isInCombatPursuit = false;
                        yield break;
                    }

                    // ReducerAuthoritative 네트워크 경로는 위 callback이 이미 Action 소유권과
                    // provisional Start를 열었다. Legacy/싱글 경로는 callback을 실행하지 않으므로
                    // 기존 순서대로 여기서 Action 회전을 시작한다.
                    if (!_rootRotationOwnership.ActionOwnsRoot)
                        BeginServerActionRotation(targetId, targetIsUnit);
                    SoloPursuitEvidence("attack-entry");
                    yield return EnterCombatLoopV3();
                    EndServerActionRotation();
                    _rootRotationOwnership.TryAcquireMovement();

                    if (_unitData == null || !_unitData.IsAlive)
                    {
                        CloseSoloPursuitEvidence("close-attacker-dead");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    // 전투 종료 후 detect 내에 다른 적이 남았으면 새 타겟 선택 후 continue.
                    if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                    {
                        TryCloseMovementAuthorityIntent(
                            UnitMovementIntentReason.Chase,
                            "post-combat-detect-range-lost");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        _postCombatExitReason = "post-combat-detect-range-lost";
                        PostCombatTraceEvent("post-combat-detect-range-lost");
#endif
                        CloseSoloPursuitEvidence("close-post-combat-detect-lost");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    var nextEnemy = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                    if (!nextEnemy.HasValue)
                    {
                        TryCloseMovementAuthorityIntent(
                            UnitMovementIntentReason.Chase,
                            "post-combat-no-next-target");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        _postCombatExitReason = "post-combat-no-next-target";
                        PostCombatTraceEvent("post-combat-no-next-target");
#endif
                        CloseSoloPursuitEvidence("close-no-next-target");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    targetId = nextEnemy.Value.id;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    _postCombatLastTarget = targetId;
                    PostCombatTraceEvent("pursuit-target-reselected");
#endif
                    targetIsUnit = nextEnemy.Value.isUnit;
                    CloseSoloPursuitEvidence("close-post-combat-reselect");
                    BeginSoloPursuitEvidence(targetId, targetIsUnit);
                    pursuitPath = null;
                    pursuitWaypointIndex = 1;
                    pursuitCheckpoint.Reset();
                    hasPursuitTargetTile = false;

                    yield return null;
                    continue;
                }

                // ── 5) [전투 이동] 안전한 서버 경로로 공격 접근 ──
                // 타겟과 같은 walkable 타일 안에 있을 때만 짧은 direct 이동을 허용한다.
                // 타일이 다르면 타겟 타일까지의 FlowField 경로를 사용하므로 중간 건물이나
                // 완전 차단 지형을 향해 직선 후보를 반복하지 않는다. 다른 유닛의 점유는
                // 여전히 차단하지 않으므로 슬롯 없는 유닛 겹침 규칙은 그대로다.
                Vector3 moveDir = enemyViewPos - transform.position;
                moveDir.y = 0f;
                CaptureUnitActionShadowDesiredTarget(enemyViewPos);
                float dist = moveDir.magnitude;
                if (dist > 0.01f)
                {
                    float liveMoveMultiplier = _combatUseCase != null
                        ? Mathf.Max(0f, _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData))
                        : 1f;
                    bool movementFrozen = liveMoveMultiplier <= MoveFreezeEpsilon;
                    SetFrozenAnimation(movementFrozen);

                    Vector3 enemyDomainPos = ViewConverter.FromView(enemyViewPos);
                    HexCoord liveTargetTile = HexMetrics.WorldToHex(enemyDomainPos);
                    Vector3 rootDomainPos = ViewConverter.FromView(transform.position);
                    HexCoord liveRootTile = HexMetrics.WorldToHex(rootDomainPos);
                    bool directInsideSameTile = liveRootTile == liveTargetTile
                        && _movementUseCase.IsWalkable(liveRootTile);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementAuthorityObserver.ObserveChaseApproach(
                        directInsideSameTile);
#endif

                    Vector3 pursuitWaypointView = enemyViewPos;
                    Vector3? nextPursuitWaypointView = null;
                    IReadOnlyList<HexCoord> pursuitCorridor = null;
                    int corridorWaypointIndex = -1;

                    if (!directInsideSameTile)
                    {
                        bool routeChanged = !hasPursuitTargetTile
                            || liveTargetTile != pursuitTargetTile
                            || _combatPursuitPathInvalidated
                            || pursuitEnvironmentRevision
                                != _navigationEnvironmentRevision;
                        if (routeChanged)
                        {
                            pursuitPath = _movementUseCase.RequestCombatApproachPath(
                                _unitData,
                                liveTargetTile);
                            pursuitTargetTile = liveTargetTile;
                            hasPursuitTargetTile = true;
                            pursuitEnvironmentRevision =
                                _navigationEnvironmentRevision;
                            pursuitWaypointIndex = 1;
                            pursuitCheckpoint.Reset();
                            pursuitRouteTargetId = targetId;
                            pursuitRouteTargetIsUnit = targetIsUnit;
                            _combatPursuitPathInvalidated = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            if (!NetworkContext.IsNetworkActive)
                            {
                                _soloPursuitGoal = liveTargetTile;
                                _soloPursuitHasRoute = pursuitPath != null && pursuitPath.Count >= 2;
                                _soloPursuitRouteRevision = pursuitEnvironmentRevision;
                                _soloPursuitIndex = 1;
                                _soloPursuitWaypoint = _soloPursuitHasRoute
                                    ? TileCenterView(pursuitPath[1]) : transform.position;
                                _soloPursuitProgressTime = Time.timeAsDouble;
                                SoloPursuitEvidence("repath");
                            }
#endif
                            if (pursuitPath != null && pursuitPath.Count >= 2)
                                _navigationObjective?.MarkNavigating();
                        }

                        if (pursuitPath == null || pursuitPath.Count < 2)
                        {
                            _navigationObjective?.MarkBlocked();
                            SetMovementHeldAnimation(true);
                            yield return null;
                            continue;
                        }

                        if (pursuitWaypointIndex >= pursuitPath.Count)
                        {
                            SoloPursuitEvidence("route-exhausted");
                            pursuitPath = null;
                            hasPursuitTargetTile = false;
                            yield return null;
                            continue;
                        }

                        pursuitWaypointView = TileCenterView(
                            pursuitPath[pursuitWaypointIndex]);
                        if (pursuitWaypointIndex + 1 < pursuitPath.Count)
                        {
                            nextPursuitWaypointView = TileCenterView(
                                pursuitPath[pursuitWaypointIndex + 1]);
                        }
                        pursuitCorridor = pursuitPath;
                        corridorWaypointIndex = pursuitWaypointIndex;
                    }

                    SetMovementHeldAnimation(false);
                    float pursuitAngle = CalculateAttackAngle(pursuitWaypointView);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (!NetworkContext.IsNetworkActive)
                    {
                        _soloPursuitIndex = pursuitWaypointIndex;
                        _soloPursuitWaypoint = pursuitWaypointView;
                        _soloPursuitGoal = liveTargetTile;
                    }
#endif
                    Quaternion targetRot = Quaternion.Euler(0f, pursuitAngle, 0f);
                    Vector3 pursuitMoveDir = pursuitWaypointView - transform.position;
                    pursuitMoveDir.y = 0f;
                    Vector3 candidatePursuitPosition = pursuitMoveDir.sqrMagnitude > 0.0001f
                        ? transform.position
                            + pursuitMoveDir.normalized
                                * worldSpeed
                                * liveMoveMultiplier
                                * Time.deltaTime
                        : transform.position;
                    bool soloPursuit = !NetworkContext.IsNetworkActive
                        && _lockedMovementPipelineMode == UnitMovementPipelineMode.Legacy;
                    if (soloPursuit)
                    {
                        UnitSoloPursuitStep.Plan(transform.position.x, transform.position.z,
                            pursuitWaypointView.x, pursuitWaypointView.z,
                            worldSpeed * liveMoveMultiplier * Time.deltaTime,
                            out double nextX, out double nextZ);
                        candidatePursuitPosition = new Vector3((float)nextX,
                            transform.position.y, (float)nextZ);
                    }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementShadowObserver.LegacyFrameToken pursuitShadowToken = default;
                    if (_lockedMovementPipelineMode
                        != UnitMovementPipelineMode.ReducerAuthoritative)
                    {
                        pursuitShadowToken = BeginMovementShadowLegacyFrame(
                            pursuitWaypointView,
                            UnitMovementIntentReason.Chase,
                            CalculateMovementShadowPositionDeltaXZ(
                                transform.position,
                                candidatePursuitPosition));
                    }
#endif
                    Vector3 pursuitBefore = transform.position;
                    MovementWriteOutcome pursuitOutcome = ApplyMovementAuthorityFrame(
                        pursuitWaypointView,
                        candidatePursuitPosition,
                        targetRot,
                        UnitMovementIntentReason.Chase,
                        targetAcquirePriority: false,
                        nextTrajectoryTarget: nextPursuitWaypointView,
                        maximumTravelDistanceWorld:
                            worldSpeed * liveMoveMultiplier * Time.deltaTime,
                        logicalCorridorPath: pursuitCorridor,
                        logicalCorridorWaypointIndex: corridorWaypointIndex,
                        candidateAcquirePredicate: candidate =>
                            _combatUseCase.HasEnemyInRangeAt(_unitData, candidate),
                        targetAcquirePresentationHandoff: () =>
                            TryBeginServerAttackEntryBeforeRootStop(
                                targetId,
                                targetIsUnit));
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (soloPursuit && pursuitOutcome == MovementWriteOutcome.Advanced
                        && CalculateMovementPositionDeltaXZ(pursuitBefore, transform.position) > 0.000001f)
                        _soloPursuitProgressTime = Time.timeAsDouble;
#endif
                    if (pursuitOutcome == MovementWriteOutcome.RepathRequired)
                    {
                        SoloPursuitEvidence("route-invalidated");
                        // 현재 Chase 경로가 동적 건물로 무효화된 경우 같은 frame에는 다시
                        // 실행하지 않는다. 코루틴과 타겟은 유지하되 같은 타겟·같은 환경에서는
                        // 실패한 경로를 반복하지 않고, 타겟 이동 또는 환경 revision 변경을 기다린다.
                        _navigationObjective?.MarkWaitingRepath();
                        SetMovementHeldAnimation(true);
                        pursuitPath = null;
                        // 같은 타겟·같은 environment revision에서는 이 실패한 route를
                        // 다시 요청하지 않는다. 타겟 타일 이동 또는 OnPathInvalidated가
                        // 새 revision을 알릴 때만 routeChanged가 다시 참이 된다.
                        pursuitTargetTile = liveTargetTile;
                        hasPursuitTargetTile = true;
                        pursuitEnvironmentRevision =
                            _navigationEnvironmentRevision;
                        _combatPursuitPathInvalidated = false;
                        yield return null;
                        continue;
                    }
                    if (pursuitOutcome == MovementWriteOutcome.DeferredAttackHandoff)
                    {
                        // Root/NoIntent/reducer가 아직 커밋되지 않았다. 현재 Walk와 이동 pose를
                        // 그대로 보존하고 다음 frame에 같은 후보를 다시 평가한다.
                        yield return null;
                        continue;
                    }
                    if (pursuitOutcome == MovementWriteOutcome.Rejected)
                    {
                        ObserveMovementAuthorityAdapterFailure("pursuit-frame-rejected",
                            $"[UAS-MOVE] 권위 추격 frame 거부. unitId={_unitData.Id}, " +
                            $"commandRevision={_movementCommandRevision}, " +
                            $"segmentRevision={_movementSegmentRevision}");
                        CloseSoloPursuitEvidence("failure-writer-rejected");
                        _isInCombatPursuit = false;
                        yield break;
                    }
                    bool consumePursuitWaypoint = soloPursuit
                        ? UnitSoloPursuitStep.TryConsume(pursuitCheckpoint, pursuitWaypointIndex,
                            pursuitOutcome == MovementWriteOutcome.Advanced,
                            _lastMovementTrajectoryReachedWaypoint,
                            _lastMovementCandidateAcquiredTarget, directInsideSameTile,
                            !_combatPursuitPathInvalidated
                                && pursuitEnvironmentRevision == _navigationEnvironmentRevision
                                && hasPursuitTargetTile && pursuitTargetTile == liveTargetTile
                                && pursuitRouteTargetId == targetId
                                && pursuitRouteTargetIsUnit == targetIsUnit)
                        : !directInsideSameTile
                            && pursuitOutcome == MovementWriteOutcome.Advanced
                            && _lastMovementTrajectoryReachedWaypoint
                            && pursuitCheckpoint.TryConsume(pursuitWaypointIndex, reachedWaypoint: true);
                    if (consumePursuitWaypoint)
                    {
                        SoloPursuitEvidence("checkpoint", reached: true, consume: true);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        if (soloPursuit) _soloPursuitProgressTime = Time.timeAsDouble;
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        UnitMovementAuthorityObserver.ObserveCenterCheckpoint(
                            CalculateMovementShadowPositionDeltaXZ(
                                transform.position,
                                pursuitWaypointView));
#endif
                        pursuitWaypointIndex++;
                    }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    CompleteMovementShadowLegacyFrame(pursuitShadowToken);
#endif

                    // 후보 위치가 공격 범위에 들어온 frame에는 위 callback이 provisional
                    // Start를 먼저 확정했다. frame을 넘기지 않고 while 선두로 돌아가면
                    // 같은 Unity frame에서 EnterCombatLoopV3까지 진입한다. 선두의 중복 start는
                    // 기존 _attackPresentationRestartPending 가드가 흡수하므로 revision과
                    // Attack clip을 다시 시작하지 않는다.
                    if (_lastMovementCandidateAcquiredTarget)
                        continue;
                }

                yield return null;
            }

            // ────────────────────────────────────────────────────────────
            // [BUG-001 — 2026-05-12] while 루프가 자연 종료(_unitData null/사망)된 경우의
            // 안전망 리셋. 위쪽의 모든 명시적 yield break 경로에서도 false로 set하지만,
            // 함수 마지막에서도 반드시 false임을 보장하여 다음 추격 진입 시 잔존하지 않도록 한다.
            // ────────────────────────────────────────────────────────────
            CloseSoloPursuitEvidence("close-pursuit-ended");
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
                // 멀티플레이 서버: 현재 Action이 소유하고 있는 공개 타겟을 그대로 전달한다.
                // 여기서 새 타겟을 다시 찾으면 이동 후보 위치에서 확정한 대상과 다른 대상을
                // 공격 시작에 사용할 수 있으므로, 최초 진입과 같은 typed payload를 사용한다.
                GameEvents.OnUnitEnteredCombat.OnNext(new UnitEnteredCombatEvent(
                    _unitData.Id,
                    _combatTargetId,
                    _combatTargetIsUnit));

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
                    _combatUseCase.TryAttack(_unitData, Time.timeAsDouble);
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

        /// <summary>동일 힐 회차의 생명주기와 서버/싱글 권위가 대기 중에도 유지되는지 확인한다.</summary>
        private bool CanContinueHealWindup(UnitData caster, UnitHealCastPresentation cast, bool networkCast)
        {
            // View 재사용/회차 종료 뒤 기존 예약은 폐기하며, 종료된 Network 경기를 solo로 오인하지 않는다.
            var current = _healPresentation.Current;
            if (!isActiveAndEnabled || !ReferenceEquals(_unitData, caster) || caster == null || !caster.IsAlive
                || !current.IsValid || !current.Active || current.Lifetime != cast.Lifetime
                || current.Sequence != cast.Sequence || current.UnitId != cast.UnitId
                || current.TargetId != cast.TargetId)
                return false;
            if (!networkCast) return !NetworkContext.IsNetworkActive;
            return NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer
                && _healNetworkUnit != null && _healNetworkUnit.IsSpawned && _healNetworkUnit.IsServer;
        }

        private static bool IsFiniteHealTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // 회차의 revision(우선순위)은 달라질 수 있지만 identity/수명/sequence는 바뀌면 안 된다.
        private bool OwnsHealScope(UnitData caster, UnitHealCastPresentation cast, ulong owner)
        {
            var current = _healPresentation.Current;
            return owner == _healLoopOwner && ReferenceEquals(_unitData, caster)
                && current.Lifetime == cast.Lifetime && current.Sequence == cast.Sequence
                && current.UnitId == cast.UnitId && current.TargetId == cast.TargetId;
        }

        private bool HasHealAuthority(UnitData caster, bool networkCast)
        {
            return isActiveAndEnabled && ReferenceEquals(_unitData, caster) && caster != null && caster.IsAlive
                && (networkCast
                    ? NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer
                        && _healNetworkUnit != null && _healNetworkUnit.IsSpawned && _healNetworkUnit.IsServer
                    : !NetworkContext.IsNetworkActive);
        }

        private bool HasOwnedHealCooldown(UnitData caster, bool networkCast)
        {
            return ReferenceEquals(_healCooldownCaster, caster) && _healCooldownCast.Lifetime == _healLifetime
                && _healCooldownNetwork == networkCast
                && (!networkCast || ReferenceEquals(_healCooldownNetworkOwner, _healNetworkUnit));
        }

        private void CloseOwnedHealLoop(UnitData caster, UnitHealCastPresentation cast, ulong owner, string reason)
        {
            // 이전 coroutine가 새 모션/회전/추격 상태를 정리하지 못하게 먼저 소유권을 검사한다.
            if (!OwnsHealScope(caster, cast, owner)) return;
            EndHealPresentation(reason, terminal: caster == null || !caster.IsAlive);
            _isInCombatPursuit = false;
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
        ///   NetworkHealthSync로 수신하고, 힐 모션은 별도 회차 atomic snapshot으로 복원한다.
        ///
        /// HoT 부여 타이밍(설계 결정):
        ///   실제 회복 발동은 애니메이션 이벤트(OnAttackHit)가 아니라 이 코루틴의 HitFrameTimes 타이머로
        ///   구동한다. 이유 — 기존 데미지 시스템도 애니 이벤트가 아니라 HitFrameTimes 타이머로 데미지를
        ///   적용하며(코드베이스 관례), 애니 이벤트 의존은 클립에 이벤트가 주입되기 전까지 회복이 아예
        ///   발동하지 않는 취약점이 있기 때문이다. 타이밍(=HitFrameTimes[0])은 동일하다.
        ///   (OnAttackHit 애니 이벤트는 힐 이펙트/사운드 연출 용도로만 남는다.)
        /// </summary>
        private IEnumerator EnterHealLoopV3(bool stationaryWatch = false)
        {
            // 필수 의존성 가드 — _positionProvider는 대상 방향 회전에 사용.
            if (_combatUseCase == null || _positionProvider == null || _unitData == null)
                yield break;

            UnitData loopCaster = _unitData;
            bool loopNetwork = NetworkContext.IsNetworkActive;
            ulong loopOwner = ++_healLoopOwner;
            // 이전 순서(실기 수용 전 비교용): clock getter가 먼저 cache를 채웠다고 가정했다.
            // if (!HasHealAuthority(loopCaster, loopNetwork)) yield break;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            bool cacheBefore = _healNetworkUnit != null;
#endif
            EnsureHealNetworkUnit();
            bool entryAccepted = HasHealAuthority(loopCaster, loopNetwork);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            ObserveFirstHealEntry(entryAccepted, cacheBefore);
#endif
            if (!entryAccepted) yield break;

            // IdleWatch도 이 공통 경계를 사용한다. 이동 writer가 멈춘 뒤 서버 Action이
            // 회전을 소유하게 하며 Client Root writer를 추가하지 않는다.
            _rootRotationOwnership.TransferToAction();
            _healStationaryWatch = stationaryWatch;
            // 직전 locomotion Held만 해제한다. Frozen flag와 실제 HP 정책은 유지한다.
            SetMovementHeldAnimation(false);

            // 힐 시전 중임을 표시 — IsInCombat()가 true를 반환하도록 하여, 건물 변화로 인한
            //   RepathAllAliveUnits → OnPathInvalidated가 힐 도중 코루틴을 끊지 않게 한다(추격과 동일 취지).
            //   Unity 코루틴은 try/finally가 동작하지 않으므로 각 yield break 직전에 false로 명시 리셋한다.
            _isInCombatPursuit = true;

            int? target = _combatUseCase.FindInjuredAllyToHeal(_unitData);

            while (_unitData != null && _unitData.IsAlive)
            {
                if (loopOwner != _healLoopOwner || !HasHealAuthority(loopCaster, loopNetwork)) yield break;
                // 1) 부상 아군 재탐색.
                if (!target.HasValue)
                {
                    if (stationaryWatch) SetMovementHeldAnimation(true);
                    EndHealPresentation("no-target");
                    // 더 힐할 아군 없음 → Walk 애니메이션 재개 후 종료(호출 측이 A* 이동 재개).
                    ResumeWalkAnimation();
                    if (NetworkContext.IsNetworkActive)
                        GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);
                    _isInCombatPursuit = false;
                    yield break;
                }

                int targetId = target.Value;

                // 재진입도 actual dispatch deadline을 존중한다. 전역 Remaining은 별도 기존 조건이다.
                // clock 모드/Network 객체가 바뀌면 오래된 clock의 deadline을 다른 clock에 비교하지 않는다.
                var entryScope = _healPresentation.Current;
                while (HasOwnedHealCooldown(loopCaster, loopNetwork) || loopCaster.AttackCooldownRemaining > 0f)
                {
                    if (!OwnsHealScope(loopCaster, entryScope, loopOwner)
                        || !HasHealAuthority(loopCaster, loopNetwork)) yield break;
                    double entryNow = HealNow;
                    if (!IsFiniteHealTime(entryNow))
                    {
                        UnitHealCastDiagnostics.Event("CooldownRejected", _healCooldownCast, "Reason=invalid-clock");
                        CloseOwnedHealLoop(loopCaster, entryScope, loopOwner, "invalid-time");
                        yield break;
                    }
                    if ((!HasOwnedHealCooldown(loopCaster, loopNetwork) || entryNow >= _healCooldownDue)
                        && loopCaster.AttackCooldownRemaining <= 0f)
                    {
                        if (HasOwnedHealCooldown(loopCaster, loopNetwork))
                        {
                            UnitHealCastDiagnostics.Event("CooldownComplete", _healCooldownCast,
                                System.FormattableString.Invariant($"Now={entryNow:G17}, Elapsed={entryNow - _healCooldownDispatchAt:G17}, Remaining={loopCaster.AttackCooldownRemaining:G17}, Reentry=true"));
                            _healCooldownCaster = null;
                        }
                        break;
                    }
                    yield return null;
                }
                if (!OwnsHealScope(loopCaster, entryScope, loopOwner)
                    || !HasHealAuthority(loopCaster, loopNetwork)) yield break;

                // 2) 대상을 향해 회전 + 힐 애니메이션 재생(시전 중 이동 없음).
                PlayHealCastAnimation(targetId);

                // 이전 준비 대기: 이 프레임의 deltaTime에는 시전 이전 시간도 포함될 수 있다.
                // 사용자 실기 수용 전 비교용으로 보존하며, 아래 주석 경로는 실행하지 않는다.
                /*
                float hitDelay = (_unitData.HitFrameTimes != null && _unitData.HitFrameTimes.Length > 0)
                    ? _unitData.HitFrameTimes[0]
                    : 0.2f;
                float t = 0f;
                while (t < hitDelay && _unitData != null && _unitData.IsAlive)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                */

                // 3) 이미 게시한 힐 회차의 시계/시작점을 그대로 사용한다. 새 timer를 시작하거나
                // 애니메이션 Marker를 HP 입력으로 쓰지 않는다. 현재 시각이 절대 due에 도달한
                // 첫 유효 서버/싱글 프레임에만 아래 기존 writer로 한 번 진행한다.
                UnitData healCaster = _unitData;
                UnitHealCastPresentation healCast = _healPresentation.Current;
                bool networkHealCast = NetworkContext.IsNetworkActive;
                double healDue = healCast.StartedAt + healCast.MarkerOffset;
                double healActualNow = HealNow;
                if (!healCast.IsValid || double.IsNaN(healDue) || double.IsInfinity(healDue)
                    || double.IsNaN(healActualNow) || double.IsInfinity(healActualNow))
                {
                    UnitHealCastDiagnostics.Event("DispatchRejected", healCast, "Reason=invalid-time-or-state");
                    CloseOwnedHealLoop(healCaster, healCast, loopOwner, "invalid-time");
                    yield break;
                }

                while (CanContinueHealWindup(healCaster, healCast, networkHealCast)
                    && healActualNow < healDue)
                {
                    yield return null;
                    if (!OwnsHealScope(healCaster, healCast, loopOwner)) yield break;
                    if (!CanContinueHealWindup(healCaster, healCast, networkHealCast)) break;
                    healActualNow = HealNow;
                    // NaN 비교는 항상 false이므로 while 비교만으로는 조기 발동을 막지 못한다.
                    // invalid clock을 deltaTime 폴백으로 우회하지 않고 회차를 닫는다.
                    if (double.IsNaN(healActualNow) || double.IsInfinity(healActualNow))
                    {
                        UnitHealCastDiagnostics.Event("DispatchRejected", healCast, "Reason=invalid-clock");
                        CloseOwnedHealLoop(healCaster, healCast, loopOwner, "invalid-time");
                        yield break;
                    }
                }
                // HoT 1회 부여(서버 권위). 대상이 시전 도중 사망/제거됐으면 CastHeal 내부에서 무시된다.
                if (_unitData == null || !_unitData.IsAlive)
                { CloseOwnedHealLoop(healCaster, healCast, loopOwner, "death"); yield break; }
                // game-end/disable/despawn/회차 교체 뒤에는 이전 시전을 실행하지 않는다.
                // revision만 바뀐 동일 회차(Frozen/Held 포함)는 기존 정책대로 유지한다.
                if (!OwnsHealScope(healCaster, healCast, loopOwner)
                    || !CanContinueHealWindup(healCaster, healCast, networkHealCast))
                {
                    UnitHealCastDiagnostics.Event("DispatchRejected", healCast, "Reason=scope-or-authority-closed");
                    CloseOwnedHealLoop(healCaster, healCast, loopOwner, "scope-or-authority-closed");
                    yield break;
                }
                // 실제 호출 직전 시각이 기준이다. 준비 due나 첫 HP 업데이트 시각이 아니다.
                double dispatchAt = HealNow;
                float configuredCooldown = healCaster.AttackCooldown;
                double cooldownDue = dispatchAt + configuredCooldown;
                if (!IsFiniteHealTime(dispatchAt) || !IsFiniteHealTime(configuredCooldown)
                    || configuredCooldown <= 0f || !IsFiniteHealTime(cooldownDue) || dispatchAt < healDue)
                {
                    UnitHealCastDiagnostics.Event("DispatchRejected", healCast, "Reason=invalid-dispatch-time-or-cooldown");
                    CloseOwnedHealLoop(healCaster, healCast, loopOwner, "invalid-time");
                    yield break;
                }
                if (!OwnsHealScope(healCaster, healCast, loopOwner)
                    || !CanContinueHealWindup(healCaster, healCast, networkHealCast)) yield break;
                UnitHealCastDiagnostics.Event("CastDispatch", _healPresentation.Current,
                    System.FormattableString.Invariant($"Due={healDue:G17}, Now={dispatchAt:G17}, Elapsed={dispatchAt - healCast.StartedAt:G17}, DispatchAt={dispatchAt:G17}, ConfiguredCooldown={configuredCooldown:G17}, CooldownDue={cooldownDue:G17}"));
                _healCooldownCaster = healCaster;
                _healCooldownCast = healCast;
                _healCooldownNetwork = networkHealCast;
                _healCooldownNetworkOwner = _healNetworkUnit;
                _healCooldownDispatchAt = dispatchAt;
                _healCooldownDue = cooldownDue;
                _combatUseCase.CastHeal(_unitData, targetId, _healPresentation.Current);

                // 이전 counter-only 대기(실기 수용 전 비교용, 실행하지 않는다).
                /*
                _unitData.AttackCooldownRemaining = _unitData.AttackCooldown;
                while (_unitData != null && _unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                    yield return null;
                */
                if (!OwnsHealScope(healCaster, healCast, loopOwner)) yield break;
                // global ticker는 그대로 감소한다. deadline까지의 값을 counter에 매 프레임 덮어쓰지 않는다.
                healCaster.AttackCooldownRemaining = configuredCooldown;
                while (true)
                {
                    if (!OwnsHealScope(healCaster, healCast, loopOwner)) yield break;
                    if (!CanContinueHealWindup(healCaster, healCast, networkHealCast))
                    {
                        UnitHealCastDiagnostics.Event("CooldownCancelled", healCast, "Reason=scope-or-authority-closed");
                        CloseOwnedHealLoop(healCaster, healCast, loopOwner, healCaster.IsAlive ? "scope-or-authority-closed" : "death");
                        yield break;
                    }
                    double cooldownNow = HealNow;
                    if (!IsFiniteHealTime(cooldownNow))
                    {
                        UnitHealCastDiagnostics.Event("CooldownRejected", healCast, "Reason=invalid-clock");
                        CloseOwnedHealLoop(healCaster, healCast, loopOwner, "invalid-time");
                        yield break;
                    }
                    if (cooldownNow >= cooldownDue && healCaster.AttackCooldownRemaining <= 0f)
                    {
                        UnitHealCastDiagnostics.Event("CooldownComplete", healCast,
                            System.FormattableString.Invariant($"Now={cooldownNow:G17}, Elapsed={cooldownNow - dispatchAt:G17}, Remaining={healCaster.AttackCooldownRemaining:G17}"));
                        if (HasOwnedHealCooldown(healCaster, networkHealCast)
                            && _healCooldownCast.Sequence == healCast.Sequence) _healCooldownCaster = null;
                        break;
                    }
                    yield return null;
                }

                // 다음 대상이 있으면 같은 Action에서 새 sequence로 직접 이어간다.
                // inactive Walk/Held를 중간 게시하지 않는다. 대상 없음은 상단에서 닫는다.
                target = _combatUseCase.FindInjuredAllyToHeal(_unitData);
            }

            // while 자연 종료(_unitData null/사망) 안전망 리셋.
            if (loopOwner == _healLoopOwner && ReferenceEquals(_unitData, loopCaster))
            {
                EndHealPresentation("loop-exit", terminal: !loopCaster.IsAlive);
                _isInCombatPursuit = false;
            }
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
            SetMovementHeldAnimation(true);
            // 부상 아군 재확인 주기(초). 너무 짧으면 매 프레임 탐색 비용, 너무 길면 반응이 굼떠진다.
            const float watchInterval = 0.4f;

            while (_unitData != null && _unitData.IsAlive)
            {
                // (1) 부상 아군 감지 → 즉시 힐 루프 재진입. 힐 루프 종료 후에도 계속 지켜보기 위해 continue.
                if (ShouldEngage())
                {
                    yield return EnterHealLoopV3(stationaryWatch: true);
                    continue;
                }

                // (2) 이동 재개용 새 경로가 예약됐으면(OnPathInvalidated) 그 경로로 이동을 재개한다.
                //     MoveTo가 _moveCoroutine을 새 이동 코루틴으로 교체하므로 이 감시 코루틴은 여기서 종료한다.
                if (_pendingPath != null)
                {
                    List<HexCoord> resumePath = _pendingPath;
                    _pendingPath = null;
                    _rootRotationOwnership.ReleaseAction(resumeMovement: false);
                    SetMovementHeldAnimation(false);
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
        ///   - 멀티플레이 클라이언트: 힐 전용 atomic snapshot(회차/서버 시각/active)을 복제해
        ///     spawn와 Initialize 뒤 현재 회차를 복원한다. 일반 Attack scalar는 시작하지 않는다.
        /// </summary>
        /// <param name="targetId">회복 대상 아군 유닛의 Id(회전 방향 계산에 사용).</param>
        private void PlayHealCastAnimation(int targetId)
        {
            // BloomFairy의 힐은 C3 공격 lease와 별도 경로다. 이전 공격 scope가 섞이지 않게 닫고,
            // OnAttackHit의 명시적인 BloomFairy 예외에서 기존 힐 연출만 보존한다.
            CloseAttackPhasePresentation("separate-heal-entry");

            _healFacingTarget = GetTargetTransform(targetId, true);
            _healFacingTargetView = _healFacingTarget != null ? _healFacingTarget.GetComponent<UnitView>() : null;

            // 승인된 점진 서버 회전은 Update의 공통 RotateTowards를 사용한다.
            // 이전 snap은 실기 검증 전 비활성화 보존한다.
            /*
            Vector3 targetPos = _positionProvider != null
                ? _positionProvider.GetUnitWorldPosition(targetId)
                : Vector3.zero;
            bool canWriteSimulationRoot =
                !NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer;
            if (canWriteSimulationRoot
                && _rootRotationOwnership.ActionOwnsRoot
                && targetPos != Vector3.zero)
            {
                float angle = CalculateAttackAngle(targetPos);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            else if (canWriteSimulationRoot
                && !_rootRotationOwnership.ActionOwnsRoot
                && targetPos != Vector3.zero)
            {
                UnitMovementAuthorityObserver.ObserveAttackWriterOwnershipConflict(
                    _unitData != null ? _unitData.Id : -1);
            }
#endif

            */
            float marker = _unitData.HitFrameTimes != null && _unitData.HitFrameTimes.Length > 0
                ? _unitData.HitFrameTimes[0] : 0.2f;
            float clipLength = _unitData.AttackCooldown;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
                    if (clip.name.EndsWith("_Attack", StringComparison.Ordinal)) { clipLength = clip.length; break; }
            var state = new UnitHealCastPresentation
            {
                Lifetime = _healLifetime, Sequence = ++_healSequence,
                Revision = _healPresentation.Current.Revision + 1,
                UnitId = _unitData.Id, TargetId = targetId, StartedAt = HealNow,
                AimX = transform.forward.x, AimZ = transform.forward.z,
                MarkerOffset = marker, ClipDuration = clipLength, Active = true,
                Priority = _isFrozenAnim ? (byte)3 : _isMovementHeldAnim ? (byte)4 : (byte)1
            };
            UnitHealCastDiagnostics.Event("Start", state);
            ObserveHealPose("Start", state);
            ApplyHealPresentation(state, state.StartedAt);
            if (NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer)
                GameEvents.OnUnitHealCastStarted.OnNext(state);
        }

        /// <summary>실제 힐 Client 적용 경계. 역할 데이터·객체 수명·준비 상태를 확인한다.</summary>
        public bool ApplyHealPresentation(UnitHealCastPresentation state, double now)
        {
            if (_unitData == null || !_unitData.IsHealer || !_unitData.IsAlive
                || state.UnitId != _unitData.Id || _animator == null) return false;
            if (!_healPresentation.Accept(state)) return false;
            ObserveHealPose("Snapshot", state);
            if (!state.Active)
            {
                _healClipHeld = false;
                UnitHealCastDiagnostics.Event("End", state);
                ObserveHealPose("Inactive", state);
                if (state.Priority == 3) FreezeWalkAnimation();
                else if (state.Priority == 4) HoldMovementAnimation();
                else if (state.EndReason == 1) _animator.speed = 0f;
                else ResumeWalkAnimation();
                return true;
            }
            MaintainHealPresentation(now);
            return true;
        }

        private void MaintainHealPresentation(double now)
        {
            var state = _healPresentation.Current;
            if (!state.Active || _unitData == null || !_unitData.IsAlive || _animator == null) return;
            bool newCycle = _healPresentation.PlayedSequence != state.Sequence;
            bool priorityChanged = _appliedHealPriority != state.Priority;
            if (state.Priority == 3) { _animator.speed = 0; _appliedHealPriority = 3; return; }
            if (state.Priority == 4) { HoldMovementAnimation(); _appliedHealPriority = 4; return; }
            if (!_healPresentation.IsDue(now)) return;
            if (newCycle || priorityChanged)
            {
                float offset = _healPresentation.Offset(now);
                _animator.speed = 1;
                _animator.CrossFadeInFixedTime(StateAttack, _healCastBlend, 0, offset);
                _currentAnimStateHash = StateAttack;
                _healPresentation.MarkPlayed(now);
                _appliedHealPriority = state.Priority;
                _healClipHeld = false;
                UnitHealCastDiagnostics.Event("Play", state,
                    $"Now={now:G17}, Offset={offset:F6}, CatchUp={offset > 0}");
                ObserveHealPose("Play", state);
            }
            // 4초 clip의 후반 동작이 회차를 채운다. 일반 endpoint hold는 비활성화 보존한다.
            // 실제 Frozen/Held 정지는 위의 priority 경계에서 유지한다.
            /* if (!_healClipHeld && now - state.StartedAt >= state.ClipDuration)
            {
                // 3초 looping clip을 4초 주기로 계속 돌리면 4초 marker가 중복된다.
                // 한 clip 끝 자세만 보존한다. HP 타이머와 3초 쿨다운은 그대로 진행한다.
                _animator.Play(StateAttack, 0, 0.999999f);
                _animator.speed = 0;
                _healClipHeld = true;
                UnitHealCastDiagnostics.Event("MotionEnd", state);
            } */
            if (!_healClipHeld && now - state.StartedAt >= state.ClipDuration)
            {
                _healClipHeld = true; // 관찰 플래그만 설정한다. Animator pose/speed는 쓰지 않는다.
                UnitHealCastDiagnostics.Event("MotionEnd", state);
                ObserveHealPose("MotionEnd", state);
            }
        }

        /// <summary>모션 수명을 닫는다. 기존 HoT writer와 효과 수명에는 관여하지 않는다.</summary>
        public void EndHealPresentation(string reason, bool terminal = false)
        {
            ++_healLoopOwner;
            // 현재 lifecycle이 닫는 힐러의 pursuit도 여기서 정리한다. 중지된 coroutine에
            // 맡기면 cleanup이 실행되지 않는다. stale coroutine은 CloseOwnedHealLoop에서 차단된다.
            // 일반 공격 유닛의 pursuit 정책에는 손대지 않는다.
            if (_unitData != null && _unitData.IsHealer) _isInCombatPursuit = false;
            if (terminal && ReferenceEquals(_healCooldownCaster, _unitData)
                && _healCooldownCast.Lifetime == _healLifetime) _healCooldownCaster = null;
            ObserveHealPose("End-" + reason, _healPresentation.Current);
            _healFacingTarget = null;
            _healFacingTargetView = null;
            var state = _healPresentation.Current;
            if (state.Active)
            {
                state.Active = false;
                state.Revision++;
                state.EndedAt = HealNow;
                state.EndReason = reason == "cycle-complete" ? (byte)1 : reason == "no-target" ? (byte)2
                    : reason == "movement-replaced" ? (byte)3 : reason == "death" ? (byte)4
                    : reason == "game-end" ? (byte)5 : (byte)6;
                _healPresentation.Accept(state);
                UnitHealCastDiagnostics.Event("End", state, "Reason=" + reason);
                if (NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer)
                    GameEvents.OnUnitHealCastEnded.OnNext(state);
            }
            if (terminal) _healPresentation.Close();
            if (reason != "cycle-complete" && _unitData != null && _unitData.IsHealer)
                _rootRotationOwnership.ReleaseAction(resumeMovement: !terminal
                    && reason == "no-target" && !_healStationaryWatch && _unitData.IsAlive);
            _healClipHeld = false;
        }

        /// <summary>
        /// [V3] 근접 전투 종료 후 A* 재개 시 출발 타일을 결정한다 (규칙 11/15).
        ///
        /// 동작:
        ///   1) 호출 측이 미리 계산한 forwardTile을 받는다 — 이중 계산 방지.
        ///   2) ReducerAuthoritative는 정렬 frame에서 커밋된 실제 공간 위치를 그대로 사용한다.
        ///      Legacy rollback만 기존 ProcessStep waypoint 의미를 유지한다.
        ///   3) Walk 애니메이션을 재개한다.
        ///   4) 현재 커밋 위치에서 finalTarget으로 새 path를 받아 외부 while로 재진입한다.
        ///
        /// 뒤쪽 타일로 복귀하지 않는다(규칙 11/15).
        ///
        /// [BUG-002 — 2026-05-13]
        ///   이전에는 transform.position을 forwardTile에 즉시 스냅했으나, 이는 규칙 9(걸어서 재개)
        ///   위반이며 1타일 순간이동의 직접 원인이었다. 이제 transform.position 정렬과 공간
        ///   타일 커밋은 호출 측 공용 writer가 처리하며, 이 함수는 path 재발급만 담당한다.
        ///
        /// [중요] forwardTile 이중 계산 방지:
        ///   호출 측은 Lerp 시작 전에 안전한 복귀 후보를 1회 계산하여 forwardTile을 결정한 뒤,
        ///   그 값으로 transform을 정렬하고 동일한 forwardTile을 이 함수에 전달해야 한다.
        ///   만약 이 함수가 복귀 후보를 다시 계산하면, 정렬 후의 transform.position
        ///   기준으로 한 타일 더 앞쪽이 선택될 수 있어 도메인-뷰 좌표가 다시 어긋난다.
        /// </summary>
        private List<HexCoord> ResumeFromForwardTileV3(HexCoord finalTarget, HexCoord forwardTile)
        {
            if (_movementUseCase == null || _unitData == null || !_unitData.IsAlive)
                return null;

            // 멀티 Legacy rollback만 waypoint 의미의 직접 도메인 갱신을 유지한다.
            // ReducerAuthoritative는 앞선 정렬 frame의 실제 Root 경계 전이가 이미 커밋했으므로
            // forwardTile을 미리 점유하거나 UnitData.Position에 기록하지 않는다.
            if (_lockedMovementPipelineMode != UnitMovementPipelineMode.ReducerAuthoritative
                && NetworkContext.IsNetworkActive
                && forwardTile != _unitData.Position)
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
            //     호출 측(MoveAlongPathV3)의 공용 writer가 forwardTile 방향으로 걸으며
            //     실제 Root 경계만 Domain에 커밋한다. 여기서는 path만 다시 발급한다.
            // ────────────────────────────────────────────────────────────

            // Walk 애니메이션 재개 — 전투 중 Attack 상태였을 수 있음.
            ResumeWalkAnimation();
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

            // 새 path 계산 — finalTarget으로 RequestMove. 실패 시 null 반환(호출 측이 종료 처리).
            List<HexCoord> newPath = RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
            return newPath;
        }

        /// <summary>
        /// 이동 종료 시 정리 + 1회성 콜백 호출.
        /// 외부 while 종료(완주/사망/RESUME 실패) 직후 + path 비정상 종료 직후 등에서 호출.
        /// </summary>
        private void MoveCleanupAndCompleteV3(bool invokeCompletion = true)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (UnitSpatialDiagnosticAccountingPolicy
                .IsUnexpectedRecoverableCleanup(
                    _spatialRecoverablePending,
                    _unitData != null && _unitData.IsAlive))
            {
                UnitMovementAuthorityObserver.ObserveSpatialCleanupAfterRecoverable(
                    _unitData != null ? _unitData.Id : -1);
            }
#endif
            _spatialRecoverablePending = false;
            _rootRotationOwnership.ReleaseAll();
            ClearCombatTargetTracking();
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
            _activeRallyPoint = null;
            _rallyPolicyCompletionPending = false;
            if (invokeCompletion)
                callback?.Invoke();
        }

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
            // [P2-4] 차지 유닛의 「타격」 marker인가. 이 marker는 charge VFX를 다시 켜지 않고, 발사음은 트레이서 발사 옆에서 낸다.
            bool chargeUnitLaunchMarker = !chargeOnly && _hasAttackChargeMarker;
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
                // [R-1 비활성화 — 2026-10-19 차지 유닛 발사 시점, 사용자 승인] 옛 Fox 조기 종료.
                //   예전에는 Fox 타격 marker가 진단만 남기고 여기서 끝나 투사체를 아예 쏘지 않았다.
                //   이제 Fox 타격 marker도 아래 일반 경로(lease 소비 → 트레이서 발사 + 발사음)로 이어진다.
                //   서버 피해는 이 return과 무관하다 — 피해 시점은 UnitFactory가 OnAttackHit만 추출한 서버 타이머가 소유한다.
                //   위 진단 기록(RecordFoxMagicianNativeImpactMarker)은 그대로 살려 둔다.
                // // 피해는 PendingHit/서버 TimerImpact가 소유한다. 여기서 charge lease를
                // // 소비하거나 VFX·recipient 큐·로컬 피격·트레이서를 다시 실행하지 않는다.
                // return;
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
                    // [R-3 / P2-3 — 2026-10-19 차지 유닛 발사 시점] 차지 신호는 허가표(lease)를 「확인만」 한다.
                    //   서버 커밋 1회는 lease를 공격 1회분(HitFrameTimes 개수 = 1)만 장전한다. 예전처럼 차지가 그 1회분을
                    //   먼저 써 버리면 정작 타격(OnAttackHit) 때 lease가 없어 투사체가 나가지 않는다(멀티에서만 드러나는 문제).
                    //   그래서 아래 두 lease 모두 상태를 바꾸지 않고 읽기만 한다. 소비는 타격 marker가 한다.
                    //   옛 동작(차지도 아래 else 블록과 똑같이 소비)은 기록용으로 주석 보존:
                    //   // consumedSourceMarker = _attackPresentationSourceMarkerLease.TryConsume(out sourceOnlyMarker);
                    //   // if (!sourceOnlyMarker)
                    //   //     consumedValidScope = TryAcquirePresentationShadowMarkerScope(out markerScope);  // ← impact lease도 1회분 소비했다
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
            // [P2-5 — 2026-10-19] Fox 타격 marker도 이제 이 경로를 지나므로(R-1), Fox 진단 기록은 「차지」 marker일 때만 남긴다.
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
            // [P2-4 — 2026-10-19 차지 유닛 발사 시점] 차지 유닛의 「타격」 marker에서는 공격 VFX(= 기 모으기 charge)를
            //   다시 켜지 않는다. charge는 이미 「차지 시작」 marker에서 켜졌고, 프리팹 길이를 두 신호 사이 시간에 맞춰
            //   두었으므로(Inferno 1.3333초, Fox 1.8333초) 바로 이 발사 순간에 스스로 끝난다. 여기서 또 켜면 발사 뒤에 charge가 새로 생긴다.
            //   비차지 유닛(23종)은 아래 호출을 종전 그대로 실행한다. 생략한 경우 vfxResult는 기본값이며,
            //   아래 진단 블록은 생략 사실을 「실패」로 세지 않도록 차지 유닛 타격 marker를 건너뛴다.
            UnitAttackVfxPlaybackResult vfxResult = default;
            if (!chargeUnitLaunchMarker)
            {
                vfxResult = effectManager != null
                    ? effectManager.PlayUnitAttack(_unitData.Type, spawnPos, spawnRot, this)
                    : new UnitAttackVfxPlaybackResult(
                        UnitAttackVfxPlaybackStatus.PoolItemUnavailable,
                        0,
                        false);
            }
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
            // [P2-4] Inferno 공격 VFX 시도 기록은 charge를 실제로 켜는 marker(이제는 차지 marker)에서만 남긴다.
            //   차지 유닛의 타격 marker는 charge를 의도적으로 생략했으므로 「시도」가 아니다 — 여기서 기록하면
            //   outcome이 started/gate-suppressed가 아니어서 관측기가 playback 실패로 세게 된다. 그래서 건너뛴다.
            //   (클립에 차지 이벤트가 없으면 _hasAttackChargeMarker=false라 타격 marker에서 종전처럼 기록된다.)
            //   옛 조건: if (_unitData.Type == UnitType.InfernoSpirit)
            if (!chargeUnitLaunchMarker && _unitData.Type == UnitType.InfernoSpirit)
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
            // [R-4 — 2026-10-19 차지 유닛 발사 시점] 차지 유닛은 여기서 공격 SFX를 내지 않는다(사용자 결정 「발사음은 발사 순간」).
            //   · 차지 marker: SFX 없음 — 차지 SFX는 추후 SFX 작업에서 다룬다(규칙 15 「하나만 재생하면 나머지를 주석으로 명시」).
            //   · 타격 marker: 발사음은 아래 트레이서 발사(em.PlayTracer) 바로 아래 줄에서 낸다(규칙 15 — 트레이서 VFX와 짝).
            //   비차지 유닛은 종전 그대로 이 줄에서 공격 VFX와 짝으로 SFX를 낸다.
            // AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);                 // (옛 줄 — 모든 유닛 무조건 재생)
            if (!_hasAttackChargeMarker)
                AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);                // SFX (규칙 15 — VFX와 짝)

            // 일반 Stop이 marker보다 먼저 도착한 경우 서버가 이미 커밋한 공격자의 발사
            // VFX/SFX까지만 마친다. 타겟은 이미 사망·교체될 수 있으므로 Tracer와 로컬 피격
            // 신호를 만들지 않고 여기서 종료한다.
            // [P2-4 — 사용자 결정 D2] 차지 유닛은 이 source-only 타격에서 아무것도 내지 않는다:
            //   charge VFX는 위에서 생략했고(P2-4), SFX도 위에서 막았으며(R-4), 트레이서는 아래로 내려가지 않는다.
            //   즉 「차지 도중 공격이 끊기면 투사체·발사음 없음」이 이 return으로 지켜진다(차지 VFX 정지는 StopOwnedChargeEffects).
            // [P4] 옛 설명 「charge는 허가와 source VFX/SFX까지만 사용한다」 → 이제 charge는 허가 「확인」과 charge VFX만 쓴다(SFX 없음).
            //   recipient 관측·피격 신호·트레이서 진입보다 먼저 종료하므로 차지 표식이 피해 결과를 방출하지 않는다.
            if (chargeOnly || sourceOnlyPresentation) return;

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
                    // [R-4] 차지 유닛 발사음(규칙 15 — 트레이서 VFX와 짝). 현재 두 차지 유닛은 LegacyFallback이라
                    //   이 권위 분기에 오지 않지만, 나중에 Supported로 바뀌어도 발사 순간 소리가 빠지지 않게 같은 자리에 둔다.
                    if (_hasAttackChargeMarker)
                        AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);
                    return;
                }

                if (_unitData.Type == UnitType.MushroomBomber)
                {
                    // Unresolved Legacy marker retains only its existing HP/text rendezvous signal.
                    // The old local tracer call is disabled (not deleted):
                    // em.PlayTracer(type, spawnPos, targetPos, callback, remaining);
                    // Writer-owned immutable AttackEffectPresentation alone owns tracer + impact.
                    GameEvents.OnLocalAttackHit.OnNext(new LocalAttackHitPresentationEvent(attackerId, markerScope, markerReplicatedSnapshot));
                    return;
                }
                // 트레이서 발사. 착탄 콜백에서 OnLocalAttackHit을 발행하여 피격 연출을 착탄 시점에 방출한다.
                //   트레이서 프리셋이 없으면 PlayTracer가 콜백을 "즉시" 실행하므로 기존 즉시 방출로 폴백된다.
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
                    },
                    // [R5 임시 — C 방식(서버 투사체) 구현 시 교체] 트레이서가 도착하면 공격자 타입의
                    //   착탄 이펙트(impactPreset)를 도착 지점에서 터뜨린다. 설정이 없는 타입은 변화 없음.
                    //   피해 시점(서버)과 어긋날 수 있지만 VFX 확인용으로 사용자 승인(2026-10-19)된 임시 동작이다.
                    playImpactOnArrive: true);
                // [R-4 / P2-4 — 2026-10-19 차지 유닛 발사 시점] 차지 유닛의 공격 SFX는 투사체가 손을 떠나는 바로 이 순간에 낸다.
                //   (규칙 15 — 트레이서 VFX와 짝. 비차지 유닛은 위 공격 VFX 옆에서 이미 냈으므로 여기서는 내지 않는다.)
                if (_hasAttackChargeMarker)
                    AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);
            }
            else
            {
                // [R-4] EffectManager가 없어 트레이서를 쏠 수 없는 경우(초기화 전·테스트 등)에도 차지 유닛의 발사음은 이 타격 순간에 낸다.
                //   종전에도 공격 SFX는 VFX 재생 성공 여부와 상관없이 재생됐으므로 같은 원칙을 따른다(사거리 1.0 미만 차지 유닛도 이 분기).
                if (_hasAttackChargeMarker)
                    AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);
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
}
