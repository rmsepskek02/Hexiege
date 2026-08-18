// ============================================================================
// NetworkCombatController.cs
// 네트워크 모드에서 유닛 전투 처리를 서버 권위로 수행하고 사망을 모든 클라이언트에 동기화.
//
// 역할:
//   - 서버 Update에서 매 프레임 UnitCombatUseCase를 통해 전투 Tick 수행
//     (싱글플레이에서는 UnitView.MoveAlongPath의 코루틴이 TryAttack을 직접 호출)
//     (멀티플레이에서는 UnitCombatUseCase.TryAttack이 클라이언트에서 return false이므로
//      이 컨트롤러가 서버 Update에서 모든 살아있는 유닛에 대해 TryAttack을 호출)
//   - OnUnitDied / OnBuildingDied 이벤트 구독 → EntityDiedClientRpc로 사망 전파
//   - 클라이언트에서 도메인 데이터 정리 + GameEvents.OnUnitDied / OnBuildingDied 재발행
//     → GameEndUseCase가 Castle 파괴 감지 → GameEndUI 반응
//
// 전투 Tick 주기:
//   UnitView.MoveAlongPath에서는 Lerp 이동 중 매 프레임 TryAttack을 호출.
//   멀티플레이에서는 클라이언트가 TryAttack을 호출해도 즉시 return false됨.
//   대신 이 컨트롤러가 서버에서 _attackInterval마다 모든 유닛을 일괄 처리.
//
// 배치:
//   씬에 빈 GameObject "NetworkCombatController" 생성.
//   NetworkObject 컴포넌트 + 이 스크립트를 부착.
//   NetworkManager의 씬 오브젝트로 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UniRx;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 전투 네트워크 컨트롤러.
    /// 서버에서 전투를 처리하고 사망 이벤트를 모든 클라이언트에 전파.
    /// </summary>
    public class NetworkCombatController : NetworkBehaviour
    {
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("전투 설정")]
        [Tooltip("전투 처리 간격 (초). 0.05 = 초당 20회 전투 판정. 첫 공격까지의 최대 지연을 줄이기 위해 낮게 설정.")]
        [SerializeField] private float _attackInterval = 0.05f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>게임 서비스 참조. UseCase 접근에 사용. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화.</summary>
        private IGameServices _services;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>
        /// Read-only multiplayer pose observer. It exists only in development/editor builds,
        /// reuses this controller's RuntimeLogger session, and is stopped before that session.
        /// </summary>
        private UnitRootPoseConsistencyObserver _rootPoseConsistencyObserver;
#endif

        /// <summary>OnUnitDied 구독 해제용 Disposable. 서버 전용(유닛 사망 → 클라이언트 RPC 전파).</summary>
        private System.IDisposable _unitDiedSubscription;

        /// <summary>OnBuildingDied 구독 해제용 Disposable. 서버 전용(건물 사망 → 클라이언트 RPC 전파).</summary>
        private System.IDisposable _buildingDiedSubscription;

        /// <summary>OnUnitWalkStarted 구독 해제용 Disposable. 서버 전용.</summary>
        private System.IDisposable _walkStartedSubscription;

        /// <summary>OnUnitEnteredCombat 구독 해제용 Disposable. 서버 전용.</summary>
        private System.IDisposable _enteredCombatSubscription;

        /// <summary>OnUnitHealCastStarted 구독 해제용 Disposable. 서버 전용(힐러 힐 애니메이션 동기화).</summary>
        private System.IDisposable _healCastSubscription;

        /// <summary>[스킬 - 빙결] OnUnitFreezeChanged 구독 해제용 Disposable. 서버 전용(빙결 걷기 정지 동기화).</summary>
        private System.IDisposable _freezeChangedSubscription;

        /// <summary>재경로 대기/막힘 중 걷기 모션 정지 상태 구독.</summary>
        private System.IDisposable _movementHeldChangedSubscription;

        private readonly System.Collections.Generic.HashSet<int> _frozenAnimationUnits =
            new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.HashSet<int> _heldMovementUnits =
            new System.Collections.Generic.HashSet<int>();

        /// <summary>
        /// 유닛별 현재 전투 타겟 추적. key=유닛Id, value=(타겟Id, 타겟이 유닛인지).
        /// 전투 중이 아닌 유닛은 Dictionary에 없음.
        /// TickCombat에서 이전 프레임 상태와 비교하여 상태 변화 시에만 RPC 전송.
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<int, (int targetId, bool isUnit)> _unitCombatTargets
            = new System.Collections.Generic.Dictionary<int, (int targetId, bool isUnit)>();

        /// <summary>
        /// StartCombatClientRpc를 전송한 유닛 Id 집합.
        /// _unitCombatTargets와 분리하여 관리하는 이유:
        ///   TickCombat(Update)이 코루틴(MoveAlongPath)보다 먼저 실행되면,
        ///   같은 프레임 내에서 TickCombat이 _unitCombatTargets에 먼저 등록하고
        ///   OnUnitEnteredCombatHandler의 ContainsKey 가드가 true를 반환하여
        ///   StartCombatClientRpc가 전송되지 않는 경쟁 조건이 발생한다.
        ///   → RPC 전송 여부는 이 Set으로만 판단한다.
        /// </summary>
        private readonly System.Collections.Generic.HashSet<int> _combatAnimationSent
            = new System.Collections.Generic.HashSet<int>();

        /// <summary>다음 전투 판정까지 남은 시간.</summary>
        private float _attackTimer = 0f;

        /// <summary>
        /// 직전 Tick에서 인터벌(_attackInterval)을 초과하여 다음 Tick으로 넘긴 이월분(초).
        /// TickCombat에 넘길 실제 경과 시간(realElapsed)을 계산할 때 이 값을 빼서
        /// 이월분이 두 번 세어지는 이중 계산을 막는다. (자세한 이유는 Update() 주석 참조.)
        /// </summary>
        private float _lastCarry = 0f;

        /// <summary>
        /// Tracer A 진단 로그에만 사용하는 공격 회차 번호 발급기다.
        /// 피해, RPC, 애니메이션, 타겟 선택에는 관여하지 않으며 현재는 SpearMan만 관측한다.
        /// </summary>
        private readonly AttackSequenceAllocator _shadowAttackSequences = new AttackSequenceAllocator();

        /// <summary>
        /// Tracer A가 서버 프로세스 안에서 관측한 UnitData 참조를 생성 개체 식별자에 연결한다.
        /// UnitData의 일반 Id가 재사용되더라도 이전 공격 결과와 섞이지 않도록 값은 1부터 단조 증가한다.
        /// 현재는 네트워크 복제 계약이 없는 진단용 seam이며, 후속 Tracer에서는 서버가 스폰 시점에 발급해
        /// snapshot으로 전달하는 정식 AttackerInstanceId로 교체해야 한다. 이 매핑은 전투 판정에 사용하지 않는다.
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<UnitData, AttackerInstanceId> _shadowAttackerInstances
            = new System.Collections.Generic.Dictionary<UnitData, AttackerInstanceId>();

        /// <summary>마지막으로 발급한 Tracer A 서버 프로세스 내 생성 개체 번호다. 0은 None으로 비워 둔다.</summary>
        private static ulong _lastShadowAttackerInstanceValue;

        // A2는 SpearMan의 단일 직접 피해 결과 하나만 관측한다. 1은 direct damage 의미이고,
        // ordinal 0은 그 hit에서 유일한 첫 결과라는 명시 값이다. 두 값은 canonical key에 반드시 들어간다.
        private const int PoseDirectDamageEffectKind = 1;
        private const int PoseSingleDirectResultOrdinal = 0;
        private const int MaximumPoseObservationCount = 256;

        /// <summary>Unity interface는 GetComponent&lt;T&gt;의 Component 제약을 만족하지 않으므로 함께 보관할 adapter cache.</summary>
        private sealed class PoseAdapterCacheEntry
        {
            public GameObject Owner;
            public MonoBehaviour Component;
            public IUnitActionPoseSource Source;
        }

        /// <summary>schedule 표본과 A1 one-cycle reducer를 dispatch까지 묶어 두는 bounded 관측 레코드.</summary>
        private sealed class PoseObservation
        {
            public AttackResultKey Key;
            public int AttackerId;
            public UnitActionSequencer Sequencer;
            public UnitActionPoseSample ScheduleSample;
            public double ScheduleServerTime;
            public double ScheduledImpactServerTime;
            public AttackTargetBinding TargetBinding;
            public float AttackRange;
            public bool UsesMeleeContactRange;
            public double MaximumDistanceWorld;
            public UnitActionReducerStatus BeginStatus;
            public UnitActionReducerStatus CommitStatus;
            public string CommitExecutionStatus;
            public bool Committed;
            public bool AttackerDead;
            public UnitActionReducerStatus MarkDeadStatus;
            public string MarkDeadExecutionStatus;
        }

        private readonly System.Collections.Generic.Dictionary<int, PoseAdapterCacheEntry> _poseAdapters
            = new System.Collections.Generic.Dictionary<int, PoseAdapterCacheEntry>();
        private readonly System.Collections.Generic.Dictionary<AttackResultKey, PoseObservation> _poseObservations
            = new System.Collections.Generic.Dictionary<AttackResultKey, PoseObservation>();
        private readonly System.Collections.Generic.Queue<AttackResultKey> _poseObservationOrder
            = new System.Collections.Generic.Queue<AttackResultKey>();

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameBootstrapper를 탐색하고
        /// 서버라면 OnUnitDied / OnBuildingDied 이벤트를 구독하여 사망 전파 준비.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // RuntimeLogger는 프로세스 전역 writer 하나만 가진다. 이 연결은 씬에
            // NetworkCombatController가 정확히 한 개 존재한다는 기존 배치 규칙을 전제로 한다.
            // Tracer A는 서버의 schedule/dispatch만 기록하므로 client 파일에는 세션 헤더만 남으며,
            // 클라이언트 presentation 계측은 후속 Tracer에서 추가한다.
            RuntimeLogger.BeginSession(
                "Assets/_Project/Docs/_Logs/2026-07-22/13_26_unit-action-sequence-implementation",
                IsServer ? "host" : "client");

            _services = GameServicesLocator.Current;
            if (_services == null)
            {
                Debug.LogWarning("[Network] NetworkCombatController: GameServicesLocator에 IGameServices가 등록되지 않았습니다.");
            }

            // NetworkContext에 네트워크 상태 주입.
            // UnitCombatUseCase가 NetworkManager에 직접 의존하지 않도록
            // Application 레이어용 정적 홀더를 업데이트.
            NetworkContext.Set(isServer: IsServer, isActive: true);
            Debug.Log($"[Network] NetworkCombatController 스폰. IsServer={IsServer}. NetworkContext 설정 완료.");

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // OnNetworkSpawn implies a live NGO multiplayer controller. Starting here (after
            // BeginSession) prevents single-player observation and avoids a second log writer.
            if (NetworkManager != null && NetworkManager.IsListening)
            {
                _rootPoseConsistencyObserver =
                    GetComponent<UnitRootPoseConsistencyObserver>();
                if (_rootPoseConsistencyObserver == null)
                {
                    _rootPoseConsistencyObserver =
                        gameObject.AddComponent<UnitRootPoseConsistencyObserver>();
                }
                _rootPoseConsistencyObserver.Initialize(NetworkManager, IsServer);
            }
#endif

            // 서버만 사망/Walk 이벤트를 구독하여 클라이언트에 동기화
            if (IsServer)
            {
                // 전투 Tick 타이머/이월분 초기화 — NGO 씬 오브젝트가 다음 게임에서 재스폰될 때
                // 이전 게임의 잔여 이월분이 남아 첫 Tick의 경과 시간이 부풀지 않도록 리셋.
                _attackTimer = 0f;
                _lastCarry = 0f;

                // 사망 이벤트는 OnUnitDied / OnBuildingDied 두 갈래로 분리되었으므로
                // 서버 측에서도 각각 구독한다.
                // 분리 이유: 구독 측에서 매번 is 캐스트로 타입을 분기하지 않도록 강타입 DTO를 사용.
                _unitDiedSubscription = GameEvents.OnUnitDied
                    .Subscribe(OnUnitDied);
                _buildingDiedSubscription = GameEvents.OnBuildingDied
                    .Subscribe(OnBuildingDied);

                // 서버 UnitView의 Walk 시작/정지 이벤트를 수신하여 클라이언트에 전파.
                // UnitView.MoveAlongPath()는 서버에서만 실행되므로,
                // Walk 애니메이션 상태를 ClientRpc로 클라이언트에 동기화.
                // 람다 대신 명명 핸들러(OnUnitWalkStartedHandler)로 분리한 이유:
                //   Walk RPC 전송과 함께 전투 애니메이션 재전송 가드(_combatAnimationSent)를
                //   해제해야 하는데, 이 두 동작을 한 곳에 명확히 모아두기 위함.
                _walkStartedSubscription = GameEvents.OnUnitWalkStarted
                    .Subscribe(OnUnitWalkStartedHandler);

                // MoveAlongPath에서 적 감지 즉시 StartCombatClientRpc 전송 (TickCombat 50ms 대기 없이)
                _enteredCombatSubscription = GameEvents.OnUnitEnteredCombat
                    .Subscribe(OnUnitEnteredCombatHandler);

                // 힐러(BloomFairy) 힐 시전 → 애니메이션 상태를 Attack(=힐 클립)으로 레벨 동기화.
                // 데미지 유발 없이 클라이언트에 힐 애니메이션만 보여주기 위한 별도 경로.
                _healCastSubscription = GameEvents.OnUnitHealCastStarted
                    .Subscribe(OnUnitHealCastStartedHandler);

                // [스킬 - 빙결] 서버 이동 코루틴이 빙결 진입/해제 시 발행하는 이벤트를 받아
                // 애니메이션 상태를 Frozen/Walk로 레벨 동기화한다(순수 클라의 "제자리걸음" 제거).
                _freezeChangedSubscription = GameEvents.OnUnitFreezeChanged
                    .Subscribe(OnUnitFreezeChangedHandler);
                _movementHeldChangedSubscription = GameEvents.OnUnitMovementHeldChanged
                    .Subscribe(OnUnitMovementHeldChangedHandler);

                Debug.Log("[Network] NetworkCombatController: 서버 측 OnUnitDied/OnBuildingDied/Walk/EnteredCombat/HealCast/FreezeChanged 이벤트 구독 완료.");
            }
        }

        /// <summary>
        /// 네트워크 디스폰 시 이벤트 구독 해제.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // 사망 이벤트 구독을 모두 해제 (Unit/Building 두 종류 모두).
            // 어느 한 쪽이라도 누수되면 다음 게임에서 동일 핸들러가 중복 실행되어
            // EntityDiedClientRpc가 두 번 전송되는 문제로 이어진다.
            _unitDiedSubscription?.Dispose();
            _unitDiedSubscription = null;

            _buildingDiedSubscription?.Dispose();
            _buildingDiedSubscription = null;

            // Walk 이벤트 구독 해제 (서버에서만 구독했으므로 null일 수 있음)
            _walkStartedSubscription?.Dispose();
            _walkStartedSubscription = null;

            // EnteredCombat 이벤트 구독 해제 (서버에서만 구독했으므로 null일 수 있음)
            _enteredCombatSubscription?.Dispose();
            _enteredCombatSubscription = null;

            // HealCast 이벤트 구독 해제 (서버에서만 구독했으므로 null일 수 있음)
            _healCastSubscription?.Dispose();
            _healCastSubscription = null;

            // [스킬 - 빙결] FreezeChanged 이벤트 구독 해제 (서버에서만 구독했으므로 null일 수 있음)
            _freezeChangedSubscription?.Dispose();
            _freezeChangedSubscription = null;
            _movementHeldChangedSubscription?.Dispose();
            _movementHeldChangedSubscription = null;
            _frozenAnimationUnits.Clear();
            _heldMovementUnits.Clear();

            // 전투 상태 초기화 — 씬 전환 시 이전 게임의 상태가 남지 않도록
            _unitCombatTargets.Clear();
            _combatAnimationSent.Clear();
            _shadowAttackerInstances.Clear();
            _shadowAttackSequences.Clear();
            _poseAdapters.Clear();
            _poseObservations.Clear();
            _poseObservationOrder.Clear();

            // 전투 Tick 타이머/이월분도 초기화 — 다음 게임 스폰 시 깨끗한 상태에서 시작.
            _attackTimer = 0f;
            _lastCarry = 0f;

            // 연결 해제 시 NetworkContext를 싱글플레이 기본값으로 초기화
            NetworkContext.Reset();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // Flush the bounded evidence summary while the existing RuntimeLogger session is
            // still open. The observer never owns or replaces that session.
            UnitMovementShadowObserver.EndSession("network-despawn");
            UnitMovementAuthorityObserver.EndSession("network-despawn");
            _rootPoseConsistencyObserver?.StopAndLogSummary();
            _rootPoseConsistencyObserver = null;
#endif

            // BeginSession과 마찬가지로 컨트롤러가 씬에 하나라는 전제에서 전역 writer를 닫는다.
            RuntimeLogger.EndSession();
        }

        // ====================================================================
        // 서버 전투 Update
        // ====================================================================

        /// <summary>
        /// 서버 전용 Update: 주기적으로 모든 살아있는 유닛의 전투를 처리.
        ///
        /// 싱글플레이: UnitView.MoveAlongPath의 코루틴이 TryAttack을 직접 호출.
        /// 멀티플레이: TryAttack이 클라이언트에서 false 반환되므로
        ///             이 서버 Update에서 일괄 처리.
        ///
        /// _attackInterval마다 Tick을 발행하여 매 프레임 처리 비용을 줄임.
        ///
        /// 타이밍 정확도 — Tick 발화 주기(cadence)와 경과 시간(elapsed)은 분리한다:
        ///   • Tick 발화 주기: _attackTimer에서 _attackInterval만큼만 빼서 이월분을 다음 Tick으로 넘긴다.
        ///     (0f로 리셋하면 이월분이 버려져 Tick 간격이 평균적으로 길어짐)
        ///   • 경과 시간: TickCombat에 넘기는 elapsed는 "직전 Tick 이후 실제로 새로 흐른 시간"만.
        ///     쿨다운 감소는 이 elapsed로 처리하므로 프레임레이트(30fps/60fps)에 무관하게
        ///     공격 주기가 애니메이션 클립 길이와 일치한다.
        ///
        ///   [과거 버그 — 이월분 이중 계산, 2026-07-11 수정]
        ///     예전에는 elapsed에 _attackTimer(이월분 포함) 전체를 넘기면서 동시에
        ///     _attackTimer에도 이월분을 남겼다. 그 이월분이 이번 elapsed에 들어갔는데
        ///     타이머에도 남아 "다음 Tick elapsed"에 또 포함됐다(같은 시간을 두 번 계산).
        ///     프레임 시간이 인터벌과 어긋날수록 elapsed 합이 실제 경과 시간보다 커져
        ///     쿨다운이 실제보다 빨리 소진됐다(관측: Pistoleer 2.0초 쿨다운이 1.71초 간격으로 발화).
        ///     → 아래처럼 직전 이월분(_lastCarry)을 빼서 순수 경과분만 넘기도록 고쳤다.
        /// </summary>
        private void Update()
        {
            // 멀티플레이 서버만 실행
            if (!IsServer) return;

            _attackTimer += Time.deltaTime;
            if (_attackTimer < _attackInterval) return;

            // ── 경과 시간 계산 (이월분 이중 계산 방지) ──
            // realElapsed = 이번 _attackTimer에서 "직전 Tick이 남긴 이월분(_lastCarry)"을 뺀 값
            //   = 직전 Tick 이후 실제로 새로 흐른 시간.
            // 예) 30fps(프레임 33.3ms), 인터벌 50ms:
            //     Tick A에서 _attackTimer=60ms → 남긴 이월분 10ms(_lastCarry).
            //     다음 프레임 두 번 뒤 _attackTimer=10+33.3+33.3=76.6ms →
            //     realElapsed = 76.6 - 10 = 66.6ms(정확히 프레임 2개분). 이월분 10ms는 중복되지 않음.
            float realElapsed = _attackTimer - _lastCarry;   // 새로 흐른 순수 시간만
            _lastCarry = _attackTimer - _attackInterval;     // 다음 Tick 계산에서 제외할 이월분
            _attackTimer = _lastCarry;                        // 타이머엔 이월분만 남김(발화 주기 유지)

            TickCombat(realElapsed);
        }

        /// <summary>
        /// 살아있는 모든 유닛에 대해 타겟 탐색 + 상태 변화 감지 + 딜레이 데미지 처리.
        ///
        /// _unitCombatTargets Dictionary로 유닛별 전투 상태(현재 타겟)를 추적하고,
        /// 상태 변화 시에만 3종 RPC(StartCombat/ChangeTarget/StopCombat)를 전송.
        /// 같은 타겟을 계속 공격 중이면 RPC 없이 데미지 코루틴만 실행.
        ///
        /// 이렇게 하면 매 쿨다운 사이클마다 Attack RPC를 반복 전송하지 않아
        /// 클라이언트의 Attack 루프(Loop Time=ON)가 끊기지 않고 자연스럽게 재생됨.
        /// </summary>
        /// <param name="elapsed">이번 Tick의 실제 경과 시간 (초). 쿨다운 감소에 사용.</param>
        private void TickCombat(float elapsed)
        {
            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null) return;

            UnitSpawnUseCase unitSpawn = _services.GetUnitSpawn();
            UnitCombatUseCase combat = _services.GetCombatUseCase();

            if (unitSpawn == null || combat == null) return;

            // ────────────────────────────────────────────────────────────────
            // 방어 타워 전투(서버 권위 — 방어 타워 시스템 규칙 9).
            // 이 Update()는 진입부에서 IsServer 가드로 클라이언트를 차단하므로,
            // 여기서 타워 Tick을 호출하면 서버에서만 데미지가 적용된다.
            // 타워가 유닛을 처치하면 TowerCombatUseCase가 OnUnitDied를 발행하고,
            // 그 이벤트를 이 컨트롤러가 이미 구독(OnUnitDied)하고 있어
            // EntityDiedClientRpc로 클라이언트에 자동 전파된다.
            // ────────────────────────────────────────────────────────────────
            TowerCombatUseCase tower = _services.GetTowerCombat();
            tower?.Tick(elapsed);

            // ────────────────────────────────────────────────────────────────
            // TorrentSpirit 파도(이동 전선) 진행 — 서버 권위(규칙 18).
            // IsServer 가드 이후이므로 서버에서만 전선이 전진하고 피해/힐이 적용된다.
            // 데미지는 OnEntityDamaged → NetworkHealthSync가, 힐은 OnEntityHealed → NetworkHealthSync가
            // 각각 클라이언트에 HP를 동기화한다.
            // ────────────────────────────────────────────────────────────────
            combat.TickWaves(elapsed);

            // ────────────────────────────────────────────────────────────────
            // BloomFairy HoT(지속 회복) 등 시간 지속 효과 진행 — 서버 권위(규칙 18/29 계승).
            // IsServer 가드 이후이므로 서버에서만 회복/피해가 적용된다. 회복은 OnEntityHealed →
            // NetworkHealthSync가 클라이언트에 HP를 동기화한다(이중 적용 없음 — 클라는 틱을 돌리지 않음).
            // ────────────────────────────────────────────────────────────────
            combat.TickTimedEffects(elapsed);

            // ────────────────────────────────────────────────────────────────
            // [Phase 3] 자연회복(초월 전용 상시 HoT) — BloomFairy 힐과 분리된 독립 채널. 서버 권위 틱.
            //   회복은 OnEntityHealed → NetworkHealthSync가 HP를 클라에 동기화(이중 적용 없음).
            // ────────────────────────────────────────────────────────────────
            combat.TickNaturalRegen(elapsed);

            // ────────────────────────────────────────────────────────────────
            // [Phase 4] 연구 진행 타이머(연구소 강화) — 서버 권위. 완료 시 UnitUpgradeUseCase가
            //   레벨을 올리고 OnResearchCompleted 훅을 호출하여 NetworkUpgradeController가 양 클라에 브로드캐스트한다.
            // ────────────────────────────────────────────────────────────────
            _services?.GetUpgradeUseCase()?.TickResearch(elapsed);

            // ────────────────────────────────────────────────────────────────
            // [스킬] 건물 글로벌 쿨다운 감소 — 서버 권위(규칙 3·25). 연구 틱 바로 옆.
            //   싱글은 GameBootstrapper.Update, 멀티 서버는 여기서만 감소한다(이중 틱 금지).
            //   멀티 순수 클라의 쿨다운 미러는 GameBootstrapper.Update(클라 분기)가 별도로 감소시킨다.
            // ────────────────────────────────────────────────────────────────
            _services?.GetSkillActivationUseCase()?.TickCooldowns(elapsed);

            // ────────────────────────────────────────────────────────────────
            // [MistShrine] 물안개 진행(1초 회복) + 자동 시전 + 시전 쿨다운 — 서버 권위(규칙 8·18·21·22).
            //   싱글은 GameBootstrapper.Update, 멀티 서버는 여기서만 돈다(이중 틱 금지 — 회복량 2배 방지).
            //   멀티 순수 클라는 회복/자동 시전을 아예 돌리지 않고, 쿨다운 미러만
            //   GameBootstrapper.Update(클라 분기)가 별도로 감소시킨다.
            //   회복 결과 HP는 OnEntityHealed → NetworkHealthSync가 클라에 동기화한다.
            // ────────────────────────────────────────────────────────────────
            MistShrineUseCase mistShrine = _services?.GetMistShrineUseCase();
            if (mistShrine != null)
            {
                mistShrine.TickCooldowns(elapsed);
                mistShrine.TickMists(elapsed);
                mistShrine.TickAutoCast(elapsed);
            }

            // ────────────────────────────────────────────────────────────────
            // [스킬 - 타입 C] 상태효과(버프/디버프/제어) 지속시간 감소 — 서버 권위(규칙 13·25).
            //   쿨다운 틱 바로 옆. 싱글은 GameBootstrapper.Update, 멀티 서버는 여기서만 감소한다(이중 틱 금지).
            //   멀티 순수 클라의 상태 미러는 GameBootstrapper.Update(클라 분기)가 별도로 감소시킨다.
            // ────────────────────────────────────────────────────────────────
            _services?.GetStatusEffectSystem()?.Tick(elapsed);

            // Dictionary를 순회하면서 타겟 탐색
            // 전투 중 RemoveUnit이 호출될 수 있으므로 키를 미리 복사
            var unitIds = new System.Collections.Generic.List<int>(unitSpawn.Units.Keys);

            // 이번 Tick에서 전투 중인 유닛 Id를 수집 (Tick 후 Dictionary 정리용)
            // HashSet을 사용하여 O(1) Contains 체크
            var activeThisTick = new System.Collections.Generic.HashSet<int>();

            foreach (int id in unitIds)
            {
                // 순회 도중 사망 처리로 제거되었을 수 있으므로 재확인
                if (!unitSpawn.Units.TryGetValue(id, out UnitData unit)) continue;
                if (!unit.IsAlive) continue;

                // 쿨다운 감소 — 고정값(_attackInterval)이 아닌 실제 경과 시간(elapsed)으로 감소.
                // 프레임레이트가 낮아도 공격 주기가 애니메이션 클립 길이와 일치.
                //
                // [축 2 — 서버 데미지 타이밍 정밀화]
                //   TickCombat은 50ms 격자(_attackInterval)에서만 쿨다운 만료를 감지한다.
                //   실제 쿨다운이 0이 되는 순간은 두 Tick 사이 어딘가일 수 있는데,
                //   그 만료 순간부터 이번 Tick까지 이미 초과 경과한 시간을 "오버슈트(overshoot)"라 한다.
                //   이 오버슈트를 뒤에서 ExecuteAttack의 데미지 딜레이(hitTime)에서 빼주면
                //   격자 오차가 데미지 시점에 누적되는 것을 막을 수 있다(최대 50ms 오차 제거).
                //
                //   계산: 차감 전 남은 쿨다운(R)이 이번 elapsed보다 작으면, R만큼 지난 시점에 만료된 것이므로
                //         만료 후 초과 경과 = elapsed - R (= elapsed 차감 시 0 아래로 내려갔을 크기).
                //         R >= elapsed(이번 Tick에도 만료 안 됨) 또는 R이 이미 0이면 오버슈트는 0.
                float overshoot = 0f;
                if (unit.AttackCooldownRemaining > 0f)
                {
                    if (unit.AttackCooldownRemaining < elapsed)
                        overshoot = elapsed - unit.AttackCooldownRemaining;

                    unit.AttackCooldownRemaining = Mathf.Max(0f, unit.AttackCooldownRemaining - elapsed);
                }

                // ────────────────────────────────────────────────────────────
                // 힐러(BloomFairy)는 적을 공격하지 않는다 → 여기서 적 전투 흐름을 건너뛴다.
                //   위의 쿨다운 감소는 반드시 통과시켜야(=continue를 여기 두어야) 힐 루프의 재시전
                //   타이밍(AttackCooldownRemaining 대기)이 정상 진행된다.
                //   힐 판정/애니메이션은 서버 UnitView.EnterHealLoopV3 + OnUnitHealCastStarted 경로가,
                //   회복 틱은 TickTimedEffects(위)가 담당한다. 여기서 healer를 combat.TryFindTarget에
                //   넣으면 attackRange(4.0)로 적을 잡아 잘못된 공격 전투에 진입하므로 반드시 제외한다.
                // ────────────────────────────────────────────────────────────
                if (unit.IsHealer) continue;

                // TryFindTarget: 쿨다운이 0이고 사거리 내 적이 있을 때만 타겟 반환.
                // HasEnemyInRange: 쿨다운과 무관하게 사거리 내 적 존재 여부만 체크.
                //
                // 두 가지를 분리하는 이유:
                //   TryFindTarget이 null을 반환하는 원인이
                //   "적이 없어서"일 수도 있고, "쿨다운 중이어서"일 수도 있음.
                //   StopCombat은 오직 "적이 없을 때"만 전송해야 하므로
                //   HasEnemyInRange로 실제 적 존재 여부를 별도 확인.
                // [스킬] 빙결/기절 상태면 공격 불가(규칙 13) — 타겟을 찾지 않아 데미지를 주지 않는다.
                //   HasEnemyInRange는 여전히 참일 수 있어 전투 애니메이션(적을 향한 자세)은 유지되지만
                //   실제 데미지(ExecuteAttack)는 발생하지 않는다. 무상태면 CanAttack=true → 기존과 동일.
                var attackResult = combat.CanAttack(unit) ? combat.TryFindTarget(unit) : null;
                bool hasEnemy = attackResult.HasValue || combat.HasEnemyInRange(unit);

                if (hasEnemy)
                {
                    // 사거리 내 적이 있음 → 전투 상태 유지
                    activeThisTick.Add(id);

                    if (attackResult.HasValue)
                    {
                        // 쿨다운 만료 + 적 있음: 타겟 변경 감지 + 데미지 실행
                        int targetId = attackResult.Value.id;
                        bool isUnit = attackResult.Value.isUnit;

                        // 실제 데미지를 적용할 타겟 — 기본값은 TryFindTarget이 반환한 가장 가까운 적.
                        // IsCurrentTargetStillValid가 true이면 기존 타겟으로 덮어씌워짐.
                        int damageTargetId = targetId;
                        bool damageTargetIsUnit = isUnit;

                        // --- 상태 변화 감지: 이전 타겟과 비교하여 RPC 전송 여부 결정 ---
                        if (_unitCombatTargets.TryGetValue(id, out var prev))
                        {
                            if (prev.targetId != targetId)
                            {
                                // 현재 타겟이 아직 살아있고 사거리 내에 있으면 교체하지 않는다.
                                // 새 유닛이 더 가까이 들어왔더라도 현재 타겟을 유지하여 공격 집중도를 보장한다.
                                // 현재 타겟이 사망했거나 사거리를 벗어난 경우에만 교체를 허용한다.
                                if (!combat.IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit))
                                {
                                    // 기존 타겟이 유효하지 않음 → 새 타겟으로 교체
                                    _unitCombatTargets[id] = (targetId, isUnit);
                                    ChangeTargetClientRpc(id, targetId, isUnit);
                                }
                                else
                                {
                                    // 기존 타겟이 아직 유효 → 애니메이션과 데미지 모두 기존 타겟 유지
                                    // (TryFindTarget이 반환한 가까운 적이 아닌 기존 타겟에게 데미지 적용)
                                    damageTargetId = prev.targetId;
                                    damageTargetIsUnit = prev.isUnit;
                                }
                            }
                            // else: 같은 타겟 → RPC 없음 (Attack 루프 유지), damageTargetId = targetId (변경 없음)
                        }
                        else
                        {
                            // 새로 전투 진입 — Dictionary 등록만 수행.
                            // StartCombatClientRpc는 OnUnitEnteredCombatHandler에서 단독 담당.
                            _unitCombatTargets[id] = (targetId, isUnit);

                            // [Phase 2] 애니메이션 상태 Attack 백업 쓰기(레벨 동기화).
                            //   OnUnitEnteredCombatHandler(즉시 경로)가 코루틴 타이밍상 아직 실행되지
                            //   않은 채 TickCombat이 먼저 전투를 등록하는 경우를 대비한 안전망이다.
                            //   NGO는 같은 값을 다시 써도 전송하지 않으므로 즉시 경로와 중복돼도 무해하다.
                            //   전투 등록 전이(transition) 시점에만 1회 실행되므로 매 틱 비용이 아니다.
                            SetUnitAnimState(id, UnitAnimState.Attack);
                        }

                        // 데미지 코루틴 실행 (쿨다운 0일 때만 여기까지 도달)
                        // damageTargetId: 기존 타겟이 유효하면 기존 타겟, 아니면 새 타겟
                        // overshoot: 이번 Tick에서 쿨다운이 만료되며 초과 경과한 시간 → 데미지 딜레이에서 차감(축 2)
                        ExecuteAttack(unit, damageTargetId, damageTargetIsUnit, overshoot);
                    }
                    else
                    {
                        // 쿨다운 중 + 적 있음 — 데미지 없음, Attack 루프 유지.
                        // 단, 쿨다운 중에도 타겟이 변경되었을 수 있으므로 (규칙 5-2)
                        // FindNearestEnemy로 현재 가장 가까운 적을 확인하여 방향 전환 RPC 전송.
                        if (_unitCombatTargets.TryGetValue(id, out var prev))
                        {
                            var nearestResult = combat.FindNearestEnemy(unit);
                            if (nearestResult.HasValue && nearestResult.Value.id != prev.targetId)
                            {
                                // 쿨다운 중에도 타겟 교체 전 동일하게 현재 타겟 유효성을 확인한다.
                                // 현재 타겟이 살아있고 사거리 내에 있으면 회전 방향도 유지한다.
                                if (!combat.IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit))
                                {
                                    _unitCombatTargets[id] = (nearestResult.Value.id, nearestResult.Value.isUnit);
                                    ChangeTargetClientRpc(id, nearestResult.Value.id, nearestResult.Value.isUnit);
                                }
                            }
                        }
                    }
                }
            }

            // 이전 Tick에서 전투 중이었지만 이번 Tick에서 타겟을 찾지 못한 유닛 → StopCombat RPC
            // Dictionary 순회 중 수정 방지를 위해 제거할 키를 미리 수집
            var toRemove = new System.Collections.Generic.List<int>();
            foreach (var kvp in _unitCombatTargets)
            {
                if (!activeThisTick.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (int id in toRemove)
            {
                _unitCombatTargets.Remove(id);
                _combatAnimationSent.Remove(id); // RPC 전송 추적도 함께 정리
                StopCombatClientRpc(id);
            }
        }

        /// <summary>
        /// hitFrameTime만큼 대기 후 실제 데미지를 적용하는 코루틴.
        ///
        /// 서버가 애니메이션 RPC를 전송한 직후 시작되며,
        /// 클라이언트에서 공격 애니메이션의 타격 프레임(OnAttackHit)에 도달할 시점에
        /// 서버에서 데미지를 적용 → HP NetworkVariable 업데이트 → 클라이언트에 HP 감소 전달.
        ///
        /// 딜레이 동안 공격자/타겟 상태가 변할 수 있으므로
        /// ApplyAttackDamage() 내부에서 모든 조건(생존, 사거리)을 재확인.
        ///
        /// delay가 0이면 최소 1프레임 대기 (Inspector 미설정 시 안전망).
        /// </summary>
        /// <param name="attacker">공격하는 유닛의 데이터</param>
        /// <param name="targetId">타겟 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛, false=건물</param>
        /// <param name="delay">데미지 적용까지의 대기 시간 (초). Attack.anim의 타격 프레임 시간.</param>
        private IEnumerator DelayedAttackDamage(
            UnitData attacker,
            int targetId,
            bool targetIsUnit,
            float delay,
            AttackerInstanceId shadowAttackerInstanceId,
            AttackSequenceId shadowSequenceId,
            int shadowHitIndex,
            double shadowScheduledImpactTime,
            AttackResultKey poseObservationKey)
        {
            // hitFrameTime > 0이면 해당 시간만큼 대기
            // hitFrameTime == 0이면 최소 1프레임 대기 (Inspector 미설정 시 즉시 적용 방지)
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            else
                yield return null;

            // 딜레이 후 UseCase를 다시 가져와서 데미지 적용
            // _services가 파괴되었을 수 있으므로 null 체크
            UnitCombatUseCase combat = _services?.GetCombatUseCase();
            if (combat == null)
            {
                TryAbandonPoseObservation(poseObservationKey, "combat-use-case-missing");
                yield break;
            }

            // observer는 Legacy 피해 호출 직전에만 실행한다. 내부 실패는 모두 잡아서 아래의
            // 기존 A0 로그와 ApplyAttackDamage 호출 순서 및 인자에 영향을 주지 않는다.
            TryDispatchPoseObservation(poseObservationKey);

            if (shadowSequenceId.IsValid)
            {
                // 이 시점은 ApplyAttackDamage 호출 직전의 dispatch 계측이다.
                // ApplyAttackDamage 내부 조건에 따라 피해가 거절될 수 있으므로 피해 성공으로 기록하지 않는다.
                RuntimeLogger.Log(
                    LogLevel.Info,
                    "Combat",
                    "NetworkCombatController",
                    "[UAS-DIAG] Legacy 피해 호출 직전 dispatch",
                    $"event=legacy-impact-dispatch, unit={attacker.Id}, " +
                    $"attackerInstance={shadowAttackerInstanceId.Value}, sequence={shadowSequenceId.Value}, " +
                    $"hit={shadowHitIndex}, targetKind={(targetIsUnit ? "unit" : "building")}, target={targetId}, " +
                    $"scheduledImpact={shadowScheduledImpactTime:F6}, dispatchTime={GetAuthoritativeServerTime():F6}, " +
                    $"aimDirection=unavailable-tracer-a, simulationFacing={attacker.Facing}");
            }

            // ApplyAttackDamage 내부에서 공격자 생존, 타겟 생존, 사거리를 모두 재확인
            combat.ApplyAttackDamage(attacker, targetId, targetIsUnit);
        }

        /// <summary>
        /// 타겟을 향한 데미지를 실행. 쿨다운 리셋 + 데미지 코루틴 시작.
        /// 애니메이션 RPC는 TickCombat의 상태 변화 감지 로직에서 별도로 전송.
        /// </summary>
        /// <param name="unit">공격하는 유닛의 데이터</param>
        /// <param name="targetId">타겟 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛, false=건물</param>
        /// <param name="overshoot">
        /// [축 2] 이번 Tick에서 쿨다운이 만료되며 초과 경과한 시간(초). 두 곳에서 함께 차감된다:
        ///   ① 쿨다운 리셋값(AttackCooldown - overshoot) → 다음 사이클 경계를 애니메이션 루프에 고정(드리프트 방지),
        ///   ② 각 히트 딜레이(hitTime - overshoot) → 이번 사이클 내 타격 시점 보정(50ms 격자 오차 제거).
        /// 첫 공격(OnUnitEnteredCombatHandler)처럼 T=0에 정확히 맞춘 경로는 0f로 호출한다.
        /// </param>
        private void ExecuteAttack(UnitData unit, int targetId, bool targetIsUnit, float overshoot)
        {
            // 1. 쿨다운 즉시 리셋 — TryFindTarget()은 쿨다운을 건드리지 않으므로 여기서 처리.
            //
            // [축 2 — 사이클 드리프트(누적 밀림) 방지]
            //   쿨다운을 전체 값(AttackCooldown)으로 그냥 리셋하면 문제가 생긴다.
            //   이번 공격은 오버슈트(50ms 격자 지연)만큼 "늦게 감지"되어 실행됐는데,
            //   여기서 전체 값 C로 리셋하면 "다음 공격 사이클의 시작점"도 그 지연만큼 뒤로 밀린다.
            //   이 밀림은 사이클마다 계속 더해지므로(o_1 + o_2 + ...) 서버 공격 사이클이
            //   클라이언트 Attack 애니메이션 루프(StartCombat 시점 T0 기준, 주기 C로 계속 회전)에서
            //   점점 벌어진다 → 장기 전투(성 공격 등)에서 수백 ms까지 어긋나는 "드리프트".
            //
            //   해결: 리셋 값에서도 오버슈트만큼 앞당긴다 → (AttackCooldown - overshoot).
            //   그러면 (감지 시점) + (C - overshoot) = (실제 만료 시점) + C 가 되어,
            //   다음 만료가 항상 애니메이션 루프 경계(T0 + k×C)에 정확히 고정된다.
            //   → 딜레이(hitTime) 차감 = "이번 사이클 내 타격 시점 보정",
            //     이 리셋 보정 = "사이클 경계를 루프에 고정" — 둘이 짝을 이뤄야 격자 오차가 누적되지 않는다.
            //   (OnUnitEnteredCombatHandler의 첫 공격은 overshoot=0f이므로 기존과 동일하게 전체 값 C로 리셋된다.)
            unit.AttackCooldownRemaining = Mathf.Max(0f, unit.AttackCooldown - overshoot);

            bool traceShadowSequence = unit.Type == UnitType.SpearMan;
            AttackerInstanceId shadowAttackerInstanceId = traceShadowSequence
                ? GetOrCreateShadowAttackerInstance(unit)
                : AttackerInstanceId.None;
            AttackSequenceId shadowSequenceId = shadowAttackerInstanceId.IsValid
                ? _shadowAttackSequences.Next(shadowAttackerInstanceId)
                : AttackSequenceId.None;
            double shadowNow = traceShadowSequence ? GetAuthoritativeServerTime() : 0d;
            double shadowCommitTime = shadowNow - overshoot;
            AttackResultKey poseObservationKey = default;
            if (traceShadowSequence)
            {
                poseObservationKey = TrySchedulePoseObservation(
                    unit, targetId, targetIsUnit,
                    shadowAttackerInstanceId, shadowSequenceId,
                    shadowCommitTime, shadowNow);
            }

            // 2. 각 히트 프레임 시간마다 독립 코루틴을 실행하여 데미지 타이밍 동기화.
            // 단일 히트 유닛: HitFrameTimes 원소가 1개 → 기존과 동일하게 코루틴 1개 실행.
            // 다중 히트 유닛(FlameSpirit 6히트, LionKnight 2히트): 원소 수만큼 코루틴 실행.
            // 각 코루틴은 독립적으로 동작하며, 타겟 사망 시 ApplyAttackDamage 내 IsAlive 체크로 자동 취소.
            for (int hitIndex = 0; hitIndex < unit.HitFrameTimes.Length; hitIndex++)
            {
                float hitTime = unit.HitFrameTimes[hitIndex];
                // [축 2] 오버슈트(격자 만료 지연)만큼 딜레이를 앞당겨 실제 타격 시점을 보정한다.
                // 오버슈트가 hitTime보다 크면 이미 지난 것이므로 하한 0(다음 프레임 즉시 적용).
                float delay = Mathf.Max(0f, hitTime - overshoot);
                double scheduledImpactTime = shadowCommitTime + hitTime;

                if (traceShadowSequence)
                {
                    RuntimeLogger.Log(
                        LogLevel.Info,
                        "Combat",
                        "NetworkCombatController",
                        "[UAS-DIAG] 공격 타격 일정 예약",
                        $"event=attack-scheduled, unit={unit.Id}, " +
                        $"attackerInstance={shadowAttackerInstanceId.Value}, sequence={shadowSequenceId.Value}, " +
                        $"phase={UnitActionPhase.Windup}, hit={hitIndex}, " +
                        $"targetKind={(targetIsUnit ? "unit" : "building")}, target={targetId}, " +
                        $"commit={shadowCommitTime:F6}, scheduledImpact={scheduledImpactTime:F6}, " +
                        $"aimDirection=unavailable-tracer-a, simulationFacing={unit.Facing}");
                }

                StartCoroutine(DelayedAttackDamage(
                    unit,
                    targetId,
                    targetIsUnit,
                    delay,
                    shadowAttackerInstanceId,
                    shadowSequenceId,
                    hitIndex,
                    scheduledImpactTime,
                    poseObservationKey));
            }
        }

        /// <summary>
        /// 적격 SpearMan 한 공격에 A1 reducer 하나를 만들고 Begin+Commit까지만 shadow 실행한다.
        /// 반환 키는 Legacy 코루틴 correlation 용도이며 유효하지 않으면 observer가 schedule되지 않았다는 뜻이다.
        /// </summary>
        private AttackResultKey TrySchedulePoseObservation(
            UnitData unit,
            int targetId,
            bool targetIsUnit,
            AttackerInstanceId attackerInstanceId,
            AttackSequenceId sequenceId,
            double commitServerTime,
            double observedServerTime)
        {
            // 서버 외 또는 SpearMan 외 경로에서는 로그조차 남기지 않아 A2 실행 횟수를 정확히 0으로 유지한다.
            if (!IsServer || unit == null || unit.Type != UnitType.SpearMan)
                return default;

            try
            {
                if (!attackerInstanceId.IsValid || !sequenceId.IsValid)
                    return LogPoseScheduleSkip(unit.Id, attackerInstanceId, sequenceId, "invalid-a0-correlation");
                if (unit.HitFrameTimes == null || unit.HitFrameTimes.Length != 1)
                    return LogPoseScheduleSkip(unit.Id, attackerInstanceId, sequenceId, "not-targetlocked-melee-single-hit");
                if (!IsFinite(commitServerTime) || !IsFinite(observedServerTime)
                    || commitServerTime > observedServerTime)
                    return LogPoseScheduleSkip(unit.Id, attackerInstanceId, sequenceId, "invalid-server-time");

                EntityRef target = new EntityRef(targetIsUnit ? EntityKind.Unit : EntityKind.Building, targetId);
                var binding = new AttackTargetBinding(AttackTargetMode.TargetLocked, target);
                if (!binding.IsValid)
                    return LogPoseScheduleSkip(unit.Id, attackerInstanceId, sequenceId, "invalid-target-binding");

                IUnitActionPoseSource source = GetPoseSource(unit.Id);
                if (source == null)
                    return LogPoseScheduleSkip(unit.Id, attackerInstanceId, sequenceId, "pose-adapter-missing");
                if (!source.TryCaptureUnitActionPose(target, out UnitActionPoseSample sample) || !sample.IsValid)
                    return LogPoseScheduleSkip(unit.Id, attackerInstanceId, sequenceId, "invalid-schedule-sample");

                // Commit 입력을 만들기 전에 도메인 컬렉션의 실제 타겟 상태를 읽는다.
                // 조회 자체가 불가능하면 true를 추측하지 않고 Commit을 실행하지 않는다.
                ReadTargetState(target, out bool targetValid, out bool targetAlive, out string targetState);
                bool targetStateObserved = targetState != "unobserved";
                double hitOffset = unit.HitFrameTimes[0];
                double cooldown = unit.AttackCooldown;
                var key = new AttackResultKey(
                    attackerInstanceId, sequenceId, 0, (int)target.Kind, target.Id,
                    PoseDirectDamageEffectKind, PoseSingleDirectResultOrdinal);
                bool timelineValid = AttackTimelinePlan.TryCreate(
                    cooldown, cooldown, new[] { hitOffset }, out AttackTimelinePlan timeline);
                bool rangeValid = AttackRangeProfile.TryCreate(
                    unit.AttackRange, HexMetrics.TileHeight, out AttackRangeProfile range);
                bool factoryValid = UnitActionSequencer.TryCreateShadowCycle(
                    attackerInstanceId, unit.Id, sequenceId, out UnitActionSequencer sequencer);
                UnitActionReducerStatus begin = UnitActionReducerStatus.InvalidInput;
                UnitActionReducerStatus commit = UnitActionReducerStatus.InvalidInput;
                string commitExecutionStatus = "not-run-prerequisite-rejected";
                if (timelineValid && rangeValid && factoryValid)
                {
                    begin = sequencer.BeginAttackAlignment(
                        sequencer.Snapshot.Revision, binding, AttackDeliveryKind.MeleeContact,
                        timeline, range, sample.SimulationFacing, commitServerTime);
                    if (begin == UnitActionReducerStatus.Accepted)
                    {
                        if (targetStateObserved)
                        {
                            commit = sequencer.CommitAttack(
                                sequencer.Snapshot.Revision, binding,
                                unit.IsAlive, targetAlive, targetValid,
                                sample.TargetSquaredDistance, sample.FacingToAimYawDegrees,
                                commitServerTime, observedServerTime);
                            commitExecutionStatus = "executed";
                        }
                        else
                        {
                            commitExecutionStatus = "not-run-target-unobserved";
                        }
                    }
                }

                bool committed = commit == UnitActionReducerStatus.Accepted
                    && sequencer != null && sequencer.Snapshot.SequenceId == sequenceId;
                var observation = new PoseObservation
                {
                    Key = key,
                    AttackerId = unit.Id,
                    Sequencer = sequencer,
                    ScheduleSample = sample,
                    ScheduleServerTime = observedServerTime,
                    ScheduledImpactServerTime = commitServerTime + hitOffset,
                    TargetBinding = binding,
                    AttackRange = unit.AttackRange,
                    UsesMeleeContactRange = rangeValid && range.UsesMeleeContact,
                    MaximumDistanceWorld = rangeValid ? range.GetMaximumDistance(target.Kind) : double.NaN,
                    BeginStatus = begin,
                    CommitStatus = commit,
                    CommitExecutionStatus = commitExecutionStatus,
                    Committed = committed,
                    MarkDeadStatus = UnitActionReducerStatus.NoChange,
                    MarkDeadExecutionStatus = "not-run-attacker-alive"
                };
                AddPoseObservation(observation);
                UnitActionPhase phase = sequencer != null
                    ? sequencer.Snapshot.Phase
                    : UnitActionPhase.Idle;
                ulong revision = sequencer != null ? sequencer.Snapshot.Revision : 0UL;
                LogPose(
                    "schedule", unit.Id, attackerInstanceId, sequenceId,
                    revision, target, 0, sample, unit.AttackRange,
                    observedServerTime, phase, commit, committed ? "accepted" : "commit-rejected",
                    $"effectKind={PoseDirectDamageEffectKind}, resultOrdinal={PoseSingleDirectResultOrdinal}, scheduledImpact={observation.ScheduledImpactServerTime:F6}, rangeMode={(observation.UsesMeleeContactRange ? "fixed-contact" : "scaled-center")}, usesMeleeContact={observation.UsesMeleeContactRange}, maximumDistanceWorld={observation.MaximumDistanceWorld:F6}, beginStatus={begin}, commitStatus={commit}, commitExecutionStatus={commitExecutionStatus}, advanceStatus=not-run-schedule, evaluateStatus=not-run-schedule, targetValid={targetValid}, targetAlive={targetAlive}, targetStateObserved={targetStateObserved}, targetState={targetState}");
                return key;
            }
            catch (System.Exception exception)
            {
                LogPoseSafe($"event=skip, reason=schedule-exception, attackerId={unit.Id}, exception={exception.GetType().Name}");
                return default;
            }
        }

        /// <summary>Legacy ApplyAttackDamage 직전 pose를 같은 key의 schedule 표본과 비교한다.</summary>
        private void TryDispatchPoseObservation(AttackResultKey key)
        {
            if (!IsServer || !key.IsValid) return;
            if (!_poseObservations.TryGetValue(key, out PoseObservation observation)) return;

            try
            {
                double now = GetAuthoritativeServerTime();
                ReadTargetState(
                    observation.TargetBinding.Target,
                    out bool targetValid,
                    out bool targetAlive,
                    out string targetState);
                bool targetStateObserved = targetState != "unobserved";
                IUnitActionPoseSource source = GetPoseSource(observation.AttackerId);
                if (source == null
                    || !source.TryCaptureUnitActionPose(observation.TargetBinding.Target, out UnitActionPoseSample dispatchSample)
                    || !dispatchSample.IsValid)
                {
                    bool attackerDead = observation.AttackerDead;
                    LogPose(
                        "dispatch", observation.AttackerId, key.AttackerInstanceId, key.SequenceId,
                        observation.Sequencer != null ? observation.Sequencer.Snapshot.Revision : 0UL,
                        observation.TargetBinding.Target, 0, observation.ScheduleSample,
                        observation.AttackRange, now,
                        observation.Sequencer != null ? observation.Sequencer.Snapshot.Phase : UnitActionPhase.Idle,
                        attackerDead ? UnitActionReducerStatus.DeadTerminal : UnitActionReducerStatus.InvalidInput,
                        attackerDead ? "attacker-dead" : "invalid-dispatch-sample",
                        $"effectKind={key.EffectKind}, resultOrdinal={key.ResultOrdinal}, facingDeltaDegrees=NaN, aimDeltaDegrees=NaN, targetMoveDistanceWorld=NaN, dispatchDeltaMs={(now - observation.ScheduledImpactServerTime) * 1000d:F3}, maximumDistanceWorld={observation.MaximumDistanceWorld:F6}, beginStatus={observation.BeginStatus}, commitStatus={observation.CommitStatus}, commitExecutionStatus={observation.CommitExecutionStatus}, markDeadStatus={observation.MarkDeadStatus}, markDeadExecutionStatus={observation.MarkDeadExecutionStatus}, advanceStatus={(attackerDead ? "not-run-attacker-dead" : "not-run-invalid-pose")}, evaluateStatus={(attackerDead ? "not-run-attacker-dead" : "not-run-invalid-pose")}, targetValid={targetValid}, targetAlive={targetAlive}, targetStateObserved={targetStateObserved}, targetState={targetState}, dispatchPose=unavailable");
                    return;
                }

                string advanceStatus = observation.AttackerDead
                    ? "not-run-attacker-dead"
                    : observation.Committed
                    ? "not-run"
                    : "not-run-commit-rejected";
                string evaluateStatus = observation.AttackerDead
                    ? "not-run-attacker-dead"
                    : observation.Committed
                    ? "not-run"
                    : "not-run-commit-rejected";
                UnitActionReducerStatus evaluate = observation.AttackerDead
                    ? UnitActionReducerStatus.DeadTerminal
                    : UnitActionReducerStatus.InvalidInput;
                if (!observation.AttackerDead && observation.Committed && observation.Sequencer != null)
                {
                    UnitActionReducerStatus advance = observation.Sequencer.Advance(
                        observation.Sequencer.Snapshot.Revision, now);
                    advanceStatus = advance.ToString();
                    if (advance == UnitActionReducerStatus.Accepted && targetState != "unobserved")
                    {
                        evaluate = observation.Sequencer.EvaluateImpact(
                            observation.Sequencer.Snapshot.Revision, 0,
                            PoseDirectDamageEffectKind, PoseSingleDirectResultOrdinal,
                            observation.TargetBinding, targetAlive, targetValid,
                            dispatchSample.TargetSquaredDistance, dispatchSample.FacingToAimYawDegrees,
                            dispatchSample.TargetAimDirection, now, out ImpactAuthorization authorization);
                        evaluateStatus = evaluate == UnitActionReducerStatus.Accepted
                            && !authorization.Key.Equals(key)
                            ? "ScopeMismatch-canonical-key"
                            : evaluate.ToString();
                    }
                    else if (targetState == "unobserved")
                    {
                        evaluateStatus = "not-run-target-unobserved";
                    }
                    else
                    {
                        evaluateStatus = "not-run-advance-rejected";
                    }
                }

                double facingDelta = UnitActionPoseSample.GetYawDegrees(
                    observation.ScheduleSample.SimulationFacing, dispatchSample.SimulationFacing);
                double aimDelta = UnitActionPoseSample.GetYawDegrees(
                    observation.ScheduleSample.TargetAimDirection, dispatchSample.TargetAimDirection);
                double targetDeltaX = dispatchSample.TargetPosition.X - observation.ScheduleSample.TargetPosition.X;
                double targetDeltaZ = dispatchSample.TargetPosition.Z - observation.ScheduleSample.TargetPosition.Z;
                double targetMoveDistance = System.Math.Sqrt(targetDeltaX * targetDeltaX + targetDeltaZ * targetDeltaZ);
                double dispatchDeltaMs = (now - observation.ScheduledImpactServerTime) * 1000d;
                UnitActionPhase phase = observation.Sequencer != null
                    ? observation.Sequencer.Snapshot.Phase
                    : UnitActionPhase.Idle;
                ulong revision = observation.Sequencer != null
                    ? observation.Sequencer.Snapshot.Revision
                    : 0UL;
                LogPose(
                    "dispatch", observation.AttackerId,
                    key.AttackerInstanceId, key.SequenceId, revision,
                    observation.TargetBinding.Target, 0, dispatchSample, observation.AttackRange,
                    now, phase, evaluate,
                    observation.AttackerDead
                        ? "attacker-dead"
                        : observation.Committed ? "observed" : "commit-rejected-observed",
                    $"effectKind={key.EffectKind}, resultOrdinal={key.ResultOrdinal}, facingDeltaDegrees={facingDelta:F3}, aimDeltaDegrees={aimDelta:F3}, targetMoveDistanceWorld={targetMoveDistance:F6}, dispatchDeltaMs={dispatchDeltaMs:F3}, rangeMode={(observation.UsesMeleeContactRange ? "fixed-contact" : "scaled-center")}, usesMeleeContact={observation.UsesMeleeContactRange}, maximumDistanceWorld={observation.MaximumDistanceWorld:F6}, beginStatus={observation.BeginStatus}, commitStatus={observation.CommitStatus}, commitExecutionStatus={observation.CommitExecutionStatus}, markDeadStatus={observation.MarkDeadStatus}, markDeadExecutionStatus={observation.MarkDeadExecutionStatus}, advanceStatus={advanceStatus}, evaluateStatus={evaluateStatus}, targetValid={targetValid}, targetAlive={targetAlive}, targetStateObserved={targetStateObserved}, targetState={targetState}");
            }
            catch (System.Exception exception)
            {
                LogPoseSafe($"event=skip, reason=dispatch-exception, attackerInstanceId={key.AttackerInstanceId.Value}, sequenceId={key.SequenceId.Value}, exception={exception.GetType().Name}");
            }
            finally
            {
                _poseObservations.Remove(key);
            }
        }

        private IUnitActionPoseSource GetPoseSource(int unitId)
        {
            if (unitId < 0 || _services == null) return null;
            GameObject owner = _services.GetUnitFactory()?.GetUnitObject(unitId);
            if (owner == null)
            {
                _poseAdapters.Remove(unitId);
                return null;
            }

            if (_poseAdapters.TryGetValue(unitId, out PoseAdapterCacheEntry cached)
                && cached.Owner == owner && cached.Component != null && cached.Source != null)
                return cached.Source;

            // Unity의 GetComponent<T>()는 T가 Component여야 하므로 interface generic 호출 대신
            // MonoBehaviour 배열을 한 번 훑고 interface 구현자를 찾는다. 이후 hot path는 위 cache를 사용한다.
            MonoBehaviour[] components = owner.GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (component is IUnitActionPoseSource source)
                {
                    _poseAdapters[unitId] = new PoseAdapterCacheEntry
                    {
                        Owner = owner,
                        Component = component,
                        Source = source
                    };
                    return source;
                }
            }

            _poseAdapters.Remove(unitId);
            return null;
        }

        /// <summary>피해 판정 함수를 호출하지 않고 도메인 컬렉션에서 타겟 존재와 생존을 분리해 읽는다.</summary>
        private void ReadTargetState(
            EntityRef target,
            out bool targetValid,
            out bool targetAlive,
            out string targetState)
        {
            targetValid = false;
            targetAlive = false;
            targetState = "unobserved";
            if (_services == null || !target.IsValid) return;

            if (target.Kind == EntityKind.Unit)
            {
                UnitSpawnUseCase spawn = _services.GetUnitSpawn();
                if (spawn == null) return;
                targetState = "observed";
                targetValid = spawn.Units.TryGetValue(target.Id, out UnitData unit) && unit != null;
                targetAlive = targetValid && unit.IsAlive;
                return;
            }

            BuildingPlacementUseCase placement = _services.GetBuildingPlacement();
            if (placement == null) return;
            targetState = "observed";
            targetValid = placement.Buildings.TryGetValue(target.Id, out BuildingData building)
                && building != null;
            targetAlive = targetValid && building.IsAlive;
        }

        private void AddPoseObservation(PoseObservation observation)
        {
            // 완료된 key도 queue에는 순서 표식으로 남을 수 있으므로 Dictionary 개수가 아니라
            // queue 자체를 제한한다. 그래야 정상 dispatch가 계속되는 장기 경기에서도 메모리가 bounded다.
            while (_poseObservationOrder.Count >= MaximumPoseObservationCount)
            {
                AttackResultKey oldest = _poseObservationOrder.Dequeue();
                // 정상 dispatch가 이미 Dictionary에서 제거한 stale queue 표식은 조용히 버린다.
                // 아직 pending인 실제 observation을 용량 때문에 제거할 때는 상관관계가 사라진
                // 이유를 canonical full key와 함께 반드시 terminal skip으로 남긴다.
                if (_poseObservations.TryGetValue(oldest, out PoseObservation evicted))
                {
                    LogPoseTerminalSkip(oldest, evicted, "capacity-evicted");
                    _poseObservations.Remove(oldest);
                }
            }

            _poseObservations[observation.Key] = observation;
            _poseObservationOrder.Enqueue(observation.Key);
        }

        private void TryAbandonPoseObservation(AttackResultKey key, string reason)
        {
            if (!key.IsValid
                || !_poseObservations.TryGetValue(key, out PoseObservation observation))
                return;

            LogPoseTerminalSkip(key, observation, reason);
            _poseObservations.Remove(key);
        }

        private static void LogPoseTerminalSkip(
            AttackResultKey key,
            PoseObservation observation,
            string reason)
        {
            LogPoseSafe(
                $"event=skip, reason={reason}, attackerId={observation.AttackerId}, " +
                $"attackerInstanceId={key.AttackerInstanceId.Value}, sequenceId={key.SequenceId.Value}, " +
                $"hitIndex={key.HitIndex}, targetKind={(EntityKind)key.VictimKind}, targetId={key.VictimId}, " +
                $"effectKind={key.EffectKind}, resultOrdinal={key.ResultOrdinal}");
        }

        private void MarkPoseObservationsDead(AttackerInstanceId attackerInstanceId)
        {
            if (!attackerInstanceId.IsValid || _poseObservations.Count == 0) return;

            // 값은 reference type이므로 Dictionary 열거 중 key를 추가·삭제하지 않고도
            // pending observation의 reducer만 Dead terminal로 전환할 수 있다.
            foreach (PoseObservation observation in _poseObservations.Values)
            {
                if (observation.Key.AttackerInstanceId != attackerInstanceId) continue;

                observation.AttackerDead = true;
                if (observation.Sequencer == null)
                {
                    observation.MarkDeadStatus = UnitActionReducerStatus.InvalidInput;
                    observation.MarkDeadExecutionStatus = "not-run-sequencer-missing";
                    continue;
                }

                try
                {
                    observation.MarkDeadStatus = observation.Sequencer.MarkDead(
                        observation.Sequencer.Snapshot.Revision);
                    observation.MarkDeadExecutionStatus = "executed";
                }
                catch (System.Exception exception)
                {
                    // 진단 observer 예외가 기존 사망·피해 경로를 막아서는 안 된다.
                    observation.MarkDeadStatus = UnitActionReducerStatus.InvalidInput;
                    observation.MarkDeadExecutionStatus = $"exception-{exception.GetType().Name}";
                }
            }
        }

        private AttackResultKey LogPoseScheduleSkip(
            int attackerId,
            AttackerInstanceId instanceId,
            AttackSequenceId sequenceId,
            string reason)
        {
            LogPoseSafe($"event=skip, reason={reason}, attackerId={attackerId}, attackerInstanceId={instanceId.Value}, sequenceId={sequenceId.Value}");
            return default;
        }

        private void LogPose(
            string eventName,
            int attackerId,
            AttackerInstanceId instanceId,
            AttackSequenceId sequenceId,
            ulong revision,
            EntityRef target,
            int hitIndex,
            UnitActionPoseSample sample,
            float attackRange,
            double serverTime,
            UnitActionPhase phase,
            UnitActionReducerStatus status,
            string reason,
            string extra)
        {
            double distance = System.Math.Sqrt(sample.TargetSquaredDistance);
            string desiredDirection = sample.HasDesiredMoveDirection
                ? $"({sample.DesiredMoveDirection.X:F4},{sample.DesiredMoveDirection.Z:F4})"
                : "none";
            LogPoseSafe(
                $"event={eventName}, serverTime={serverTime:F6}, attackerId={attackerId}, attackerInstanceId={instanceId.Value}, sequenceId={sequenceId.Value}, revision={revision}, " +
                $"targetKind={target.Kind}, targetId={target.Id}, delivery={AttackDeliveryKind.MeleeContact}, hitIndex={hitIndex}, " +
                $"attackerPositionXZ=({sample.AttackerPosition.X:F4},{sample.AttackerPosition.Z:F4}), currentFacingXZ=({sample.SimulationFacing.X:F4},{sample.SimulationFacing.Z:F4}), " +
                $"targetPositionXZ=({sample.TargetPosition.X:F4},{sample.TargetPosition.Z:F4}), targetAimDirectionXZ=({sample.TargetAimDirection.X:F4},{sample.TargetAimDirection.Z:F4}), hasDesiredMoveDirection={sample.HasDesiredMoveDirection}, desiredMoveDirectionXZ={desiredDirection}, " +
                $"yawErrorDegrees={sample.FacingToAimYawDegrees:F3}, distanceWorld={distance:F6}, attackRangeStat={attackRange:F4}, phase={phase}, reducerStatus={status}, reason={reason}, {extra}");
        }

        private static void LogPoseSafe(string details)
        {
            try
            {
                RuntimeLogger.Log(
                    LogLevel.Info, "Combat", "NetworkCombatController",
                    "[UAS-POSE] server pose observer", details);
            }
            catch (System.Exception)
            {
                // 진단 writer 실패는 Legacy 공격을 막아서는 안 된다.
            }
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>
        /// SpearMan UnitData 참조에 대응하는 Tracer A 생성 개체 식별자를 반환한다.
        /// 처음 관측한 참조에만 새 번호를 발급하며, 이 값은 서버 프로세스 내부 진단용이므로
        /// 게임플레이 상태나 네트워크 메시지에는 기록하지 않는다.
        /// </summary>
        private AttackerInstanceId GetOrCreateShadowAttackerInstance(UnitData unit)
        {
            if (_shadowAttackerInstances.TryGetValue(unit, out AttackerInstanceId existing))
                return existing;

            if (_lastShadowAttackerInstanceValue == ulong.MaxValue)
                return AttackerInstanceId.None;

            var created = new AttackerInstanceId(++_lastShadowAttackerInstanceValue);
            _shadowAttackerInstances.Add(unit, created);
            return created;
        }

        /// <summary>
        /// 서버에서 기록하는 Tracer A schedule/dispatch 시각의 기준인 NGO 서버 시간을 반환한다.
        /// 네트워크가 아직 시작되지 않은 경우에만 Unity 실행 시간을 사용하며, 게임 판정에는 사용하지 않는다.
        /// </summary>
        private double GetAuthoritativeServerTime()
        {
            return NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
        }

        // ====================================================================
        // 이벤트 핸들러 (서버 전용)
        // ====================================================================

        /// <summary>
        /// 서버 UnitView가 Walk(이동) 애니메이션을 시작했을 때 호출되는 핸들러.
        ///
        /// 하는 일 두 가지:
        ///   1) SetUnitAnimState(unitId, Walk) — 애니메이션 상태를 Walk로 레벨 동기화하여
        ///      모든 클라이언트가 Walk 애니메이션을 재생하도록 한다.
        ///   2) _combatAnimationSent.Remove(unitId) — 전투 애니메이션 재전송 가드를 해제.
        ///
        /// [핵심 — 왜 여기서 가드를 해제하는가 (초급자용 상세 설명)]
        ///   서버 UnitView의 전투 루프는 매 프레임 돌면서, 타겟이 사망해 교체되는 "찰나의 순간"에
        ///   사거리 내 적을 못 본 것으로 착각하고 Walk 경로로 잠깐 빠질 수 있다.
        ///   그 순간 OnUnitWalkStarted가 발행되고, 그 결과 애니메이션 상태가 Walk로 동기화되어
        ///   "클라이언트 애니메이터는 Attack 루프를 벗어나 Walk로 전환"된다.
        ///
        ///   문제는 서버의 전투 판정(TickCombat, 50ms 격자)은 같은 기간 사거리 내 "다른 적"을
        ///   계속 발견하기 때문에 StopCombat을 보내지 않는다는 점이다.
        ///   → StopCombat이 안 나가면 _combatAnimationSent에 이 유닛이 그대로 남는다.
        ///   → 이후 전투에 다시 진입(OnUnitEnteredCombatHandler)할 때
        ///     `_combatAnimationSent.Contains(unitId)` 가드에 걸려 StartCombatClientRpc가
        ///     재전송되지 않는다.
        ///   → 서버는 계속 공격(데미지 정상)하는데, 클라이언트만 Walk 애니메이션에 갇혀
        ///     화면에서 공격 모션 없이 굳어 보이는 버그가 발생한다(유닛 62 사례, 75초 지속).
        ///
        ///   Walk 상태로 전환되는 이 시점은 "클라이언트 애니메이터가 Attack 루프를 벗어난다"는 것을
        ///   서버가 알 수 있는 유일한 지점이다. 바로 이 지점에서 재전송 가드를 풀어두면,
        ///   다음 전투 재진입 시 StartCombatClientRpc가 반드시 다시 전송되어
        ///   클라이언트가 Attack 루프로 정확히 복귀한다.
        ///
        /// [부작용 없음]
        ///   비전투 유닛의 일반 이동에서도 이 핸들러가 호출되지만, 그 유닛은 애초에
        ///   _combatAnimationSent에 없으므로 Remove는 아무 일도 하지 않는(no-op) 안전한 호출이다.
        ///
        /// [_unitCombatTargets는 건드리지 않는다]
        ///   서버 전투 로직(타겟 추적)은 그대로 두고, 오직 애니메이션 재전송 가드만 해제한다.
        ///   서버 데미지 흐름에는 어떤 영향도 주지 않는다.
        ///
        /// [재진입 시 Attack 루프 T=0 재시작은 의도된 동작]
        ///   재진입 시 StartCombatClientRpc가 Attack CrossFade를 다시 걸어 클라이언트 Attack 루프가
        ///   T=0부터 재시작되는데, 이는 OnUnitEnteredCombatHandler가 ExecuteAttack도 함께 실행하여
        ///   서버 공격 사이클과 T=0에서 동기화되므로 어긋나지 않는다.
        /// </summary>
        /// <param name="unitId">Walk를 시작한 유닛의 Id</param>
        private void OnUnitWalkStartedHandler(int unitId)
        {
            // 1) [Phase 2] 애니메이션 상태를 Walk로 설정(레벨 동기화).
            //    클라이언트는 값 변경(OnValueChanged) 및 스폰 시 현재 값을 자동 적용하므로
            //    엣지 트리거 Walk RPC와 달리 스폰 레이스로 신호가 유실될 수 없다.
            ApplyMovementAnimationState(unitId);

            // 2) 전투 애니메이션 재전송 가드 해제 — 유지.
            //    _combatAnimationSent는 "애니메이션"이 아니라 OnUnitEnteredCombatHandler의
            //    ExecuteAttack(데미지 재발화)과 StartCombatClientRpc(타겟 전달) 재전송을 게이팅하는
            //    "기능" 가드다. 전투 이탈(Walk) 시 해제해야 다음 전투 재진입에서 첫 공격이 재실행된다.
            //    (Phase 2의 애니메이션 레벨 동기화와는 별개의 목적이므로 그대로 둔다.)
            _combatAnimationSent.Remove(unitId);
        }

        /// <summary>
        /// 힐러(BloomFairy) 힐 시전 시작 이벤트 핸들러(서버 전용).
        /// 애니메이션 상태만 Attack(=힐 클립)으로 레벨 동기화하여 클라이언트가 힐 모션을 재생하게 한다.
        /// 데미지/타겟 RPC는 전송하지 않는다(힐러는 적 공격 흐름과 분리 — 규칙 28 확장).
        /// 회복 자체는 서버 TickTimedEffects가 처리하고 HP는 NetworkHealthSync가 동기화한다.
        /// </summary>
        /// <param name="unitId">힐 시전을 시작한 힐러 유닛의 Id.</param>
        private void OnUnitHealCastStartedHandler(int unitId)
        {
            SetUnitAnimState(unitId, UnitAnimState.Attack);
        }

        /// <summary>
        /// [스킬 - 빙결] 서버 이동 코루틴이 빙결 진입/해제를 알릴 때 호출(서버 전용).
        /// 빙결이면 애니메이션 상태를 Frozen(걷기 정지)으로, 해제면 Walk(걷기 재개)로 레벨 동기화한다.
        /// 순수 클라이언트는 이 값을 받아 걷기 애니메이션을 멈추거나(제자리걸음 제거) 재개한다.
        /// 호스트(서버)는 UnitView가 Animator.speed를 직접 제어하므로 별도 처리가 필요 없다.
        /// </summary>
        /// <param name="e">(유닛 Id, 빙결 여부).</param>
        private void OnUnitFreezeChangedHandler(UnitFreezeChangedEvent e)
        {
            if (e.Frozen)
                _frozenAnimationUnits.Add(e.UnitId);
            else
                _frozenAnimationUnits.Remove(e.UnitId);

            ApplyMovementAnimationState(e.UnitId);
        }

        private void OnUnitMovementHeldChangedHandler(UnitMovementHeldChangedEvent e)
        {
            if (e.Held)
                _heldMovementUnits.Add(e.UnitId);
            else
                _heldMovementUnits.Remove(e.UnitId);

            ApplyMovementAnimationState(e.UnitId);
        }

        private void ApplyMovementAnimationState(int unitId)
        {
            UnitAnimState state = _frozenAnimationUnits.Contains(unitId)
                ? UnitAnimState.Frozen
                : _heldMovementUnits.Contains(unitId)
                    ? UnitAnimState.Held
                    : UnitAnimState.Walk;
            SetUnitAnimState(unitId, state);
        }

        /// <summary>
        /// [Phase 2] 유닛 Id로 같은 GameObject의 NetworkUnit을 찾아 애니메이션 상태를 설정한다(서버 전용).
        /// 실제 NetworkVariable 쓰기는 NetworkUnit.SetAnimState가 담당한다(NGO 허용 위치=Infrastructure).
        /// 팩토리 조회 → GetComponent&lt;NetworkUnit&gt;() 패턴은 OnUnitDied의 Despawn 경로와 동일한 관례.
        /// </summary>
        /// <param name="unitId">상태를 설정할 유닛의 Id.</param>
        /// <param name="state">설정할 애니메이션 상태(Walk / Attack).</param>
        private void SetUnitAnimState(int unitId, UnitAnimState state)
        {
            if (_services == null) return;

            IUnitFactory unitFactory = _services.GetUnitFactory();
            GameObject unitObj = unitFactory != null ? unitFactory.GetUnitObject(unitId) : null;
            if (unitObj == null) return;

            NetworkUnit networkUnit = unitObj.GetComponent<NetworkUnit>();
            if (networkUnit != null)
                networkUnit.SetAnimState(state);
        }

        /// <summary>
        /// MoveAlongPath에서 적을 처음 감지했을 때 호출.
        /// TickCombat 인터벌(최대 50ms)을 기다리지 않고 즉시 StartCombatClientRpc 전송.
        ///
        /// ※ 가드 조건: _combatAnimationSent로 판단 (_unitCombatTargets 미사용).
        ///   이유: TickCombat(Update)이 코루틴(MoveAlongPath)보다 먼저 실행될 경우,
        ///   같은 프레임에 TickCombat이 _unitCombatTargets를 먼저 등록하여
        ///   ContainsKey 가드가 true를 반환 → RPC 미전송 버그 발생.
        ///   → RPC 전송 여부를 _combatAnimationSent로 분리하여 이 경쟁 조건을 해소.
        ///
        /// ※ ExecuteAttack 동시 호출 (쿨다운=0인 경우):
        ///   StartCombatClientRpc와 함께 바로 ExecuteAttack을 실행하면
        ///   서버 공격 사이클이 T=0에서 시작 → 애니메이션 루프(T=0)와 동기화.
        ///   그렇지 않으면 다음 TickCombat(T≈50ms)에서 ExecuteAttack이 처음 실행되어
        ///   서버 사이클이 T=0.05에서 시작 → 쿨다운 만료 T=3.05 ≠ 애니메이션 루프 경계 T=3.0.
        /// </summary>
        private void OnUnitEnteredCombatHandler(int unitId)
        {
            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null) return;

            UnitSpawnUseCase unitSpawn = _services.GetUnitSpawn();
            UnitCombatUseCase combat = _services.GetCombatUseCase();
            if (unitSpawn == null || combat == null) return;

            if (!unitSpawn.Units.TryGetValue(unitId, out UnitData unit)) return;
            if (!unit.IsAlive) return;

            // StartCombatClientRpc를 이미 전송한 유닛이면 중복 전송 방지.
            // _unitCombatTargets 대신 _combatAnimationSent를 사용하는 이유:
            //   TickCombat(Update)과 코루틴(MoveAlongPath) 실행 순서 경쟁 조건 방지.
            if (_combatAnimationSent.Contains(unitId)) return;

            // 타겟 탐색 — 쿨다운과 무관하게 사거리 내 첫 타겟 탐색
            // TryFindTarget은 쿨다운 체크 포함이므로 쿨다운 중이어도 FindNearestEnemy로 확인
            var attackResult = combat.TryFindTarget(unit);
            if (!attackResult.HasValue)
            {
                // TryFindTarget이 null인 경우: 쿨다운 중이거나 적이 없음.
                // FindNearestEnemy로 쿨다운 없이 재탐색하여 실제 적 존재 여부와 타겟 ID 확인.
                var nearestResult = combat.FindNearestEnemy(unit);
                if (!nearestResult.HasValue) return; // 실제로 적이 없으면 종료

                // 쿨다운 중이지만 적은 있음 → 즉시 StartCombatClientRpc 전송.
                // 규칙 1: 적 감지 즉시 클라이언트에 알려야 하며, 쿨다운은 데미지 타이밍과 무관.
                _unitCombatTargets[unitId] = (nearestResult.Value.id, nearestResult.Value.isUnit);
                _combatAnimationSent.Add(unitId);
                // [Phase 2] 애니메이션 상태를 Attack로 설정(레벨 동기화). 클라이언트 값 변경/스폰 시 자동 적용.
                SetUnitAnimState(unitId, UnitAnimState.Attack);
                // StartCombatClientRpc는 유지 — 클라이언트 타겟 전달(회전 추적/원거리 트레이서 조준)용.
                // [Phase 2 대체] 이 RPC가 유발하던 Attack CrossFade 책임은 위 레벨 동기화로 이관됨
                //   (UnitView.StartCombatAnimation의 CrossFade는 클라이언트에서 스킵되도록 분기됨).
                StartCombatClientRpc(unitId, nearestResult.Value.id, nearestResult.Value.isUnit);
                return;
            }

            int targetId = attackResult.Value.id;
            bool targetIsUnit = attackResult.Value.isUnit;

            // 전투 상태 등록 + StartCombatClientRpc 즉시 전송
            _unitCombatTargets[unitId] = (targetId, targetIsUnit);
            _combatAnimationSent.Add(unitId);
            // [Phase 2] 애니메이션 상태를 Attack로 설정(레벨 동기화). 클라이언트 값 변경/스폰 시 자동 적용.
            SetUnitAnimState(unitId, UnitAnimState.Attack);
            // StartCombatClientRpc는 유지 — 클라이언트 타겟 전달(회전 추적/원거리 트레이서 조준)용.
            // [Phase 2 대체] 이 RPC가 유발하던 Attack CrossFade 책임은 위 레벨 동기화로 이관됨.
            StartCombatClientRpc(unitId, targetId, targetIsUnit);

            // ExecuteAttack 즉시 실행 — 서버 공격 사이클을 애니메이션 시작(T=0)과 동기화.
            // TickCombat에서 처음 실행되면 T≈50ms 오프셋이 생겨 쿨다운 만료 타이밍이
            // 애니메이션 루프 경계와 어긋남 → 타겟 소멸 후 애니메이션이 루프 직후 도중에 전환되는 현상.
            //
            // [축 2] 이 경로는 적 감지 즉시(T=0) 실행되므로 격자 오버슈트가 없다 → overshoot=0f.
            ExecuteAttack(unit, targetId, targetIsUnit, 0f);
        }

        /// <summary>
        /// 서버에서 유닛 사망 이벤트를 수신하여 모든 클라이언트에 사망 전파.
        /// 클라이언트에도 동일한 이벤트 체인을 재현하기 위해 ClientRpc로 전파.
        ///
        /// 강타입 DTO(UnitDiedEvent) 사용으로 얻는 이점:
        ///   - is 캐스트 분기 불필요. UnitDiedEvent.Unit이 이미 UnitData 강타입.
        ///   - 알 수 없는 엔티티 타입 분기 불필요 (DTO 자체가 유닛 전용).
        /// </summary>
        /// <param name="e">사망한 유닛 정보가 담긴 이벤트.</param>
        private void OnUnitDied(UnitDiedEvent e)
        {
            // 서버만 클라이언트에 전파한다. 클라이언트에서 도착하는 OnUnitDied는
            // EntityDiedClientRpc → HandleUnitDied 가 재발행한 것이므로 무시.
            if (!IsServer) return;
            if (e.Unit == null) return;

            int unitId = e.Unit.Id;

            // 사망한 유닛의 전투 상태를 Dictionary에서 제거.
            // StopCombatClientRpc는 전송하지 않음 — EntityDiedClientRpc가 사망 처리를 담당.
            // 사망한 타겟을 공격 중이던 유닛들의 전투 상태는 제거하지 않음.
            // → 다음 TickCombat에서 TryFindTarget이 다른 타겟을 찾거나 null을 반환.
            //   ChangeTargetClientRpc 또는 StopCombatClientRpc가 자연스럽게 발행됨.
            _unitCombatTargets.Remove(unitId);
            _combatAnimationSent.Remove(unitId);
            _frozenAnimationUnits.Remove(unitId);
            _heldMovementUnits.Remove(unitId);
            // 죽은 공격자의 adapter와 새 회차 발급 상태만 정리한다.
            //
            // 이미 예약된 A2 observation은 여기서 지우면 안 된다. 피해 지연 코루틴은
            // 이 NetworkCombatController가 소유하므로 공격자 GameObject가 사라져도 계속 실행된다.
            // observation을 먼저 지우면 코루틴의 TryDispatchPoseObservation은 조용히 끝나지만
            // 바로 뒤 A0 Legacy dispatch는 기록되어 같은 공격 회차의 schedule/dispatch 쌍이 깨진다.
            //
            // pending observation은 먼저 reducer를 Dead terminal로 바꾸고, 코루틴이
            // reason=attacker-dead dispatch를 기록한 뒤 finally에서 제거한다. GameObject가
            // 프레임 끝까지 남아 유효한 pose가 읽혀도 Advance/Evaluate는 실행하지 않는다.
            // 실제 Legacy 피해는 기존 ApplyAttackDamage의 attacker.IsAlive 가드가 그대로 차단한다.
            if (_shadowAttackerInstances.TryGetValue(e.Unit, out AttackerInstanceId deadAttackerInstanceId))
            {
                MarkPoseObservationsDead(deadAttackerInstanceId);
                _shadowAttackSequences.Forget(deadAttackerInstanceId);
                _shadowAttackerInstances.Remove(e.Unit);
            }
            _poseAdapters.Remove(unitId);

            Debug.Log($"[Network] 서버: 유닛 사망. Id={unitId}");

            // 모든 클라이언트에 사망 전파. RPC 시그니처는 유지(isUnit=true).
            // EntityDiedClientRpc는 클라이언트의 도메인 데이터 정리 + OnUnitDied 재발행을 담당한다.
            EntityDiedClientRpc(unitId, true);

            // ────────────────────────────────────────────────────────────────
            // [핵심 수정 — 2026-06-08] NGO를 통한 클라이언트 GameObject 파괴.
            //
            // 왜 필요한가:
            //   서버 UnitView의 OnUnitDied 구독자가 Destroy(gameObject)를 호출해도
            //   그 파괴는 서버 로컬에서만 일어나며 NGO를 통해 클라이언트로 전파되지 않는다.
            //   결과적으로 클라이언트 화면에는 죽은 유닛의 GameObject가 그대로 남는다.
            //
            //   NGO에서 클라이언트의 네트워크 오브젝트를 함께 파괴하려면
            //   반드시 서버에서 NetworkObject.Despawn(destroy: true)를 명시적으로 호출해야 한다.
            //   이렇게 하면:
            //     1) NGO가 모든 클라이언트에 Despawn 메시지를 보낸다.
            //     2) 클라이언트에서 NetworkUnit.OnNetworkDespawn()이 호출되어
            //        사망 이펙트를 재생한 뒤 GameObject가 자동으로 파괴된다.
            //     3) 서버(Host)에서도 destroy: true에 의해 GameObject가 파괴되므로
            //        UnitView가 별도로 Destroy를 호출할 필요가 없다.
            //
            // 주의: 이 처리는 Infrastructure 레이어 안에서만 NGO API를 사용하므로
            //       레이어 규칙(Presentation에서 Unity.Netcode 직접 참조 금지)을 위반하지 않는다.
            // ────────────────────────────────────────────────────────────────
            if (_services != null)
            {
                IUnitFactory unitFactory = _services.GetUnitFactory();
                GameObject unitObj = unitFactory != null
                    ? unitFactory.GetUnitObject(unitId)
                    : null;

                if (unitObj != null)
                {
                    NetworkObject netObj = unitObj.GetComponent<NetworkObject>();
                    // 아직 스폰 상태인 NetworkObject만 Despawn 가능.
                    // (이미 다른 경로로 Despawn된 경우 중복 호출 시 NGO가 경고를 출력하므로 가드)
                    if (netObj != null && netObj.IsSpawned)
                    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        // Retire B2 diagnostic state before NGO changes IsSpawned or recycles
                        // NetworkObjectId. This is read-only observer lifecycle bookkeeping.
                        UnitMovementShadowObserver.RetireUnit(
                            unitId,
                            netObj.NetworkObjectId);
#endif
                        netObj.Despawn(destroy: true);
                    }
                }
            }
        }

        /// <summary>
        /// 서버에서 건물 사망 이벤트를 수신하여 모든 클라이언트에 사망 전파.
        /// GameEndUseCase가 동일한 OnBuildingDied를 구독하므로 서버에서 이미 게임 종료 판정됨.
        /// 클라이언트에서도 EntityDiedClientRpc → HandleBuildingDied → OnBuildingDied 재발행으로
        /// 동일한 이벤트 체인을 재현한다.
        /// </summary>
        /// <param name="e">사망한 건물 정보가 담긴 이벤트.</param>
        private void OnBuildingDied(BuildingDiedEvent e)
        {
            if (!IsServer) return;
            if (e.Building == null) return;

            int buildingId = e.Building.Id;
            Debug.Log($"[Network] 서버: 건물 사망. Id={buildingId}");

            // 모든 클라이언트에 사망 전파. RPC 시그니처는 유지(isUnit=false).
            EntityDiedClientRpc(buildingId, false);
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 엔티티 사망 후 모든 클라이언트에 사망 처리 명령 전송.
        /// 클라이언트:
        ///   1. 도메인 데이터(Unit/Building) Dictionary에서 제거
        ///   2. GameEvents.OnUnitDied 또는 GameEvents.OnBuildingDied 발행
        ///      → 유닛 GameObject 파괴는 서버의 NetworkObject.Despawn(destroy:true)가
        ///        NGO를 통해 자동 처리(NetworkUnit.OnNetworkDespawn에서 이펙트 재생).
        ///      → 건물은 BuildingFactory가 OnBuildingDied 구독으로 GameObject 파괴.
        ///      → GameEndUseCase(클라이언트)가 Castle 사망 감지 → GameEndUI 반응
        /// </summary>
        /// <param name="entityId">사망한 엔티티 Id</param>
        /// <param name="isUnit">true=유닛, false=건물</param>
        [ClientRpc]
        private void EntityDiedClientRpc(int entityId, bool isUnit)
        {
            // 서버는 이미 UseCase에서 처리 완료 → 중복 방지
            if (IsServer) return;

            Debug.Log($"[Network] EntityDiedClientRpc 수신. Id={entityId}, IsUnit={isUnit}");

            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                Debug.LogError("[Network] EntityDiedClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            if (isUnit)
            {
                HandleUnitDied(entityId);
            }
            else
            {
                HandleBuildingDied(entityId);
            }
        }

        // ====================================================================
        // 전투 상태 ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 유닛이 전투 상태에 진입. 클라이언트에서 Attack 루프 시작 + 타겟 방향 회전.
        /// TickCombat에서 유닛이 처음 타겟을 발견했을 때 전송.
        /// 서버(Host)도 수신하여 동일한 애니메이션 처리.
        ///
        /// GameEvents.OnNetworkCombatStarted를 발행하면 UnitView가 자신의 Id에 해당하는
        /// 이벤트만 구독해 처리한다. 이렇게 하여 Infrastructure → Presentation 역방향 의존을 피한다.
        /// </summary>
        /// <param name="unitId">공격하는 유닛의 Id</param>
        /// <param name="targetId">타겟 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛, false=건물</param>
        [ClientRpc]
        private void StartCombatClientRpc(int unitId, int targetId, bool targetIsUnit)
        {
            // 주의: IsServer 분기를 추가하지 않는다.
            // 서버(Host)도 이 RPC를 수신하여 동일한 애니메이션 처리가 필요하기 때문.
            //
            // 이벤트 발행만 수행. UnitView가 OnNetworkCombatStarted를 구독해 자기 Id에 해당하면
            // StartCombatAnimation을 호출한다. 유닛 생성 직후 RPC가 도착해 UnitView가 아직 구독하지
            // 못한 경우라도, UnitView 측에서 OnEnable/OnNetworkSpawn 시점에 늦게 구독을 등록한 뒤
            // 다음 이벤트부터 처리한다(첫 Combat 이벤트는 다음 TickCombat에서 다시 발행됨).
            GameEvents.OnNetworkCombatStarted.OnNext(new NetworkCombatStartedEvent(unitId, targetId, targetIsUnit));
        }

        /// <summary>
        /// 전투 중 타겟 변경. 클라이언트에서 회전만 업데이트, 애니메이션 재시작 없음.
        /// TickCombat에서 유닛의 타겟이 이전과 다를 때 전송.
        /// </summary>
        /// <param name="unitId">공격하는 유닛의 Id</param>
        /// <param name="newTargetId">새 타겟 엔티티의 Id</param>
        /// <param name="newTargetIsUnit">true=유닛, false=건물</param>
        [ClientRpc]
        private void ChangeTargetClientRpc(int unitId, int newTargetId, bool newTargetIsUnit)
        {
            GameEvents.OnNetworkCombatTargetChanged.OnNext(
                new NetworkCombatTargetChangedEvent(unitId, newTargetId, newTargetIsUnit));
        }

        /// <summary>
        /// 유닛이 전투 상태 종료. 클라이언트에서 Attack → Idle 전환.
        /// TickCombat에서 유닛의 타겟이 사라졌을 때 전송.
        /// Idle로 전환하는 이유: 이동 재개 시 애니메이션 상태(_animState)가 Walk로 동기화되므로.
        /// </summary>
        /// <param name="unitId">전투를 종료하는 유닛의 Id</param>
        [ClientRpc]
        private void StopCombatClientRpc(int unitId)
        {
            GameEvents.OnNetworkCombatStopped.OnNext(new NetworkCombatStoppedEvent(unitId));
        }

        // Walk 애니메이션 전파는 엣지 트리거 RPC가 아니라 NetworkUnit._animState 레벨 동기화로 처리한다
        // (스폰 레이스 유실 방지). Walk→Attack 전환은 StartCombatClientRpc가 타겟 정보를 전달하고,
        // Attack CrossFade는 클라이언트에서 _animState=Attack 값 변경으로 재생된다.

        // ====================================================================
        // 사망 처리 헬퍼 (클라이언트 전용)
        // ====================================================================

        /// <summary>
        /// 클라이언트 측 유닛 사망 처리.
        /// UnitData를 Dictionary에서 제거하고 GameEvents.OnUnitDied 발행.
        /// UnitView가 이를 구독하여 GameObject를 파괴.
        ///
        /// 사망 이벤트 채널은 유닛 전용 OnUnitDied / 건물 전용 OnBuildingDied 둘로 나뉜다.
        /// </summary>
        private void HandleUnitDied(int unitId)
        {
            UnitSpawnUseCase unitSpawn = _services.GetUnitSpawn();
            if (unitSpawn == null)
            {
                Debug.LogWarning("[Network] HandleUnitDied: UnitSpawnUseCase가 null.");
                return;
            }

            UnitData unit = unitSpawn.GetUnit(unitId);
            if (unit == null)
            {
                // 이미 처리되었거나 없는 유닛 (조용히 무시)
                return;
            }

            // HP가 0이 아니라면 강제로 0으로 맞춤 (HP 동기화 패킷보다 사망 패킷이 먼저 도착한 경우)
            if (unit.IsAlive)
            {
                unit.TakeDamage(unit.Hp);
            }

            // 도메인 Dictionary에서 제거
            unitSpawn.RemoveUnit(unitId);

            // GameEvents.OnUnitDied 재발행 → UnitView 구독자가 GameObject를 파괴.
            GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(unit));

            Debug.Log($"[Network] 클라이언트: 유닛 사망 처리 완료. UnitId={unitId}");
        }

        /// <summary>
        /// 클라이언트 측 건물 사망 처리.
        /// BuildingData를 Dictionary에서 제거하고 GameEvents.OnBuildingDied 발행.
        /// BuildingFactory가 이를 구독하여 GameObject를 파괴.
        /// GameEndUseCase도 OnBuildingDied를 구독 → Castle 파괴 시 게임 종료 판정.
        ///
        /// 사망 이벤트 채널은 유닛 전용 OnUnitDied / 건물 전용 OnBuildingDied 둘로 나뉜다.
        /// </summary>
        private void HandleBuildingDied(int buildingId)
        {
            BuildingPlacementUseCase buildingPlacement = _services.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                Debug.LogWarning("[Network] HandleBuildingDied: BuildingPlacementUseCase가 null.");
                return;
            }

            BuildingData building = buildingPlacement.GetBuilding(buildingId);
            if (building == null)
            {
                // 이미 처리되었거나 없는 건물 (조용히 무시)
                return;
            }

            // HP 강제 소진 (클라이언트 HP 동기화보다 사망 패킷이 먼저 도착한 경우 대비)
            if (building.IsAlive)
            {
                building.TakeDamage(building.Hp);
            }

            // 도메인 Dictionary에서 제거
            buildingPlacement.RemoveBuilding(buildingId);

            // GameEvents.OnBuildingDied 재발행 → BuildingFactory, GameEndUseCase 등 구독자가 후속 처리.
            GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(building));

            Debug.Log($"[Network] 클라이언트: 건물 사망 처리 완료. BuildingId={buildingId}");
        }
    }
}
