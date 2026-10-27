// ============================================================================
// NetworkUnit.cs
// 유닛 프리팹 루트에 부착하는 NetworkBehaviour.
// 서버에서 unitId를 NetworkVariable<int>로 설정하면,
// 클라이언트가 OnNetworkSpawn()에서 이를 읽어 UnitView를 초기화한다.
//
// 타이밍 문제 해결:
//   NGO가 클라이언트에 프리팹을 자동 생성하는 시점과
//   SpawnUnitClientRpc로 UnitData가 도착하는 시점이 다를 수 있음.
//   → NetworkVariable<int>로 unitId를 즉시 동기화하여,
//     OnNetworkSpawn() 시점에 unitId를 읽을 수 있도록 보장.
//
// 초기화 흐름:
//   [서버]
//   1. UnitFactory.CreateUnitObject() → Instantiate → NetworkObject.Spawn()
//   2. NetworkUnit.SetUnitId(unitId) → NetworkVariable에 값 설정
//   3. SpawnUnitClientRpc로 UnitData 생성에 필요한 추가 정보 전송
//
//   [클라이언트]
//   1. NGO가 자동으로 프리팹 Instantiate + OnNetworkSpawn() 호출
//   2. OnNetworkSpawn()에서 unitId 확인:
//      - 이미 값이 있으면 즉시 등록
//      - 없으면 _unitId.OnValueChanged 콜백을 등록하여 값 도착 시 등록
//   3. SpawnUnitClientRpc 수신 시 UnitData 생성 + UnitView 초기화
//      → RegisterClientUnit()으로 UnitView를 UnitFactory에 등록
//
// 표현 좌표 계약:
//   NetworkUnit은 Simulation Root를 보정하거나 덮어쓰지 않는다.
//   NetworkTransform이 canonical 서버 position/rotation의 유일한 client writer다.
//   Red 관점 반전은 같은 root의 VisualRootProjector가 Visual Root에만 적용한다.
//
// 배치:
//   유닛 프리팹 루트 오브젝트에 부착.
//   에디터 스크립트(AddNetworkTransformToPrefabs)가 자동 추가.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Presentation;
using UniRx;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// [Phase 2] 유닛 애니메이션 상태 — NetworkVariable로 서버→클라이언트 동기화되는 "값".
    ///
    /// 왜 이 enum이 필요한가 (초급자용 설명):
    ///   기존에는 걷기/공격 전환을 "상태가 바뀌는 순간 딱 1번 쏘는 신호(엣지 트리거 RPC)"로
    ///   전달했다. 그 신호가 도착하는 순간 클라이언트 유닛이 아직 준비(구독)되지 않았으면
    ///   신호를 놓치고, 이후 다시 알려주는 신호가 없어 틀린 애니메이션에 갇혔다(스폰 레이스).
    ///   → 애니메이션 상태를 "현재 값 자체"로 공유하면, 유닛이 늦게 스폰되어도 스폰되는 순간
    ///     서버의 현재 값을 즉시 읽어 올바른 애니메이션을 재생하므로 신호를 놓칠 수 없다.
    ///
    /// byte로 인코딩하여 NetworkVariable&lt;byte&gt;에 저장한다(직렬화가 가장 안전한 형태).
    /// 사망(IsDead)은 기존 IsDead bool + Despawn 경로가 담당하므로 여기에 포함하지 않는다.
    /// </summary>
    public enum UnitAnimState : byte
    {
        /// <summary> 아직 상태가 설정되지 않음(스폰 직후 기본값). </summary>
        None = 0,
        /// <summary> 이동(걷기) 애니메이션. </summary>
        Walk = 1,
        /// <summary> 공격 애니메이션. </summary>
        Attack = 2,
        /// <summary>
        /// [스킬 - 빙결] 걷기 애니메이션 정지(제자리 프레임 고정). 빙결로 이동이 멈춘 동안 사용.
        /// 클라이언트는 이 값을 받으면 Walk 클립을 유지한 채 Animator.speed=0으로 멈춰 "제자리걸음"을 없앤다.
        /// 빙결 해제 시 서버가 다시 Walk로 되돌리면 speed=1로 재개된다.
        /// </summary>
        Frozen = 3,
        /// <summary>
        /// 이동 목표는 살아 있지만 재경로 대기/막힘으로 실제 변위가 없어 걷기 재생을 멈춘 상태.
        /// </summary>
        Held = 4
    }

    /// <summary>
    /// 유닛 프리팹의 네트워크 동기화 컴포넌트.
    /// unitId를 NetworkVariable로 서버→클라이언트 동기화.
    /// 클라이언트 OnNetworkSpawn에서 UnitView 초기화를 위한 연결 지점 역할.
    /// canonical Simulation Root는 읽기만 하며 관점 반전은 VisualRootProjector에 위임한다.
    /// </summary>
    public class NetworkUnit : NetworkBehaviour
    {
        // ====================================================================
        // NetworkVariable — unitId 동기화
        // ====================================================================

        /// <summary>
        /// 서버가 설정하고 모든 클라이언트가 읽는 유닛 Id.
        /// -1은 아직 설정되지 않은 초기 상태를 의미.
        /// ReadPermission: Everyone — 모든 클라이언트가 읽을 수 있음.
        /// WritePermission: Server — 서버만 값을 설정할 수 있음.
        /// </summary>
        private NetworkVariable<int> _unitId = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary> 현재 동기화된 유닛 Id. -1이면 아직 미설정. </summary>
        public int UnitId => _unitId.Value;

        /// <summary>
        /// [Phase 2] 서버가 쓰고 모든 클라이언트가 읽는 유닛 애니메이션 상태(byte 인코딩).
        /// 저장값은 UnitAnimState를 byte로 캐스팅한 것. 초기값 None(0).
        /// ReadPermission: Everyone / WritePermission: Server — _unitId와 완전히 동일한 관례.
        ///
        /// [엣지 트리거 RPC 대비 핵심 장점 — 스폰 레이스 구조적 해소]
        ///   과거의 엣지 트리거 RPC 방식은 "상태가 바뀌는 순간 1회"만 쏘므로
        ///   그 순간 클라이언트가 아직 이벤트 구독 전이면 신호가 유실되어 틀린 애니메이션에 갇혔다.
        ///   NetworkVariable은 "현재 값"을 항상 보관하므로 클라이언트가 스폰(OnNetworkSpawn)하는
        ///   순간 현재 값을 즉시 읽어(ApplySpawnAnimState) 적용할 수 있어 유실이 불가능하다.
        ///
        /// [중복 전송 걱정 없음 — 애니메이션 목적의 중복 방지 가드가 불필요해짐]
        ///   NGO는 값이 실제로 바뀔 때만 델타를 전송한다(같은 값을 다시 써도 전송하지 않음).
        ///   따라서 서버가 매 틱 같은 상태를 다시 써도 네트워크 비용이 없고 중복 적용도 없다.
        /// </summary>
        private NetworkVariable<byte> _animState = new NetworkVariable<byte>(
            (byte)UnitAnimState.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// B3 이동 전용 권위 상태. 공격 Action phase와 분리되며 신규 이동 mode에서만
        /// 서버가 쓴다. 클라이언트는 현재 phase/revision을 읽기만 한다.
        /// </summary>
        private NetworkVariable<byte> _movementPhase = new NetworkVariable<byte>(
            (byte)UnitMovementPhase.NoIntent,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkVariable<ulong> _movementCommandRevision = new NetworkVariable<ulong>(
            0UL,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkVariable<ulong> _movementSegmentRevision = new NetworkVariable<ulong>(
            0UL,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// phase/scope 묶음이 완전히 기록된 뒤 마지막에 증가하는 commit revision이다.
        /// 클라이언트는 이 값의 변경 콜백에서 세 값을 일관된 snapshot으로 읽는다.
        /// NGO 2.9.2의 NetworkBehaviour source-field 순서 ReadDelta/callback 계약에 따라
        /// phase/command/segment 뒤 마지막 선언과 마지막 write를 유지한다.
        /// NGO 업그레이드 시 이 commit-marker 순서 보장을 반드시 재검증해야 한다.
        /// 장기적으로 단일 복제 snapshot으로 통합하는 작업은 별도 후속 범위다.
        /// </summary>
        private NetworkVariable<ulong> _movementSemanticRevision = new NetworkVariable<ulong>(
            0UL,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Tracer C 공격 Shadow의 원자 복제 값이다. 실제 공격/회전/애니메이션에는 적용하지 않고
        /// Host와 Client가 같은 공격 회차·타겟·방향을 관측했는지 확인하는 데만 사용한다.
        /// 여러 NetworkVariable로 쪼개지 않아 revision과 target이 다른 tick 값으로 섞이지 않는다.
        /// </summary>
        private NetworkVariable<NetworkUnitActionShadowState> _attackShadowState
            = new NetworkVariable<NetworkUnitActionShadowState>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        // 서버 게시와 Client 수신이 동일한 (AttackSequenceId, Revision) 계약을 사용한다.
        // NetworkObject 수명마다 OnNetworkSpawn에서 새로 만들어 이전 spawn의 상한을 섞지 않는다.
        private UnitAttackShadowReplicationClassifier _attackShadowPublicationClassifier;
        private UnitAttackShadowImpactReplicationClassifier _attackShadowImpactClassifier;
        private AttackerInstanceId _attackPresentationInstanceId;

        public UnitMovementPhase MovementPhase =>
            (UnitMovementPhase)_movementPhase.Value;
        public ulong MovementCommandRevision => _movementCommandRevision.Value;
        public ulong MovementSegmentRevision => _movementSegmentRevision.Value;
        public ulong MovementSemanticRevision => _movementSemanticRevision.Value;
        public NetworkUnitActionShadowState AttackShadowState => _attackShadowState.Value;

        /// <summary>
        /// [Phase 2] 같은 GameObject의 UnitView 캐시 — 애니메이션 상태 적용 시 메서드 호출용.
        /// NetworkUnit → 같은 프리팹 내 UnitView 참조는 기존 관례다(OnNetworkDespawn에서도 GetComponent 사용).
        /// 클라이언트에서만 사용한다(호스트/서버는 Animator를 직접 제어하므로 적용하지 않음).
        /// </summary>
        private UnitView _unitView;

        // 객체 수명 안의 현재 공격 표현 값이다. RPC와 늦은 init 모두 같은 적용 경계를 쓴다.
        private NetworkVariable<AttackPhaseWire> _attackPhaseState = new NetworkVariable<AttackPhaseWire>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private UnitAttackPhasePresentation _pendingAttackPhase;
        private ulong _appliedAttackPhaseRevision, _closedAttackEpoch;
        private ulong _closedAttackInstance, _closedAttackSequence;
        private double _closedAttackEpochTime = double.NegativeInfinity;
        private bool _attackPhaseEnded;
        private static ulong _nextAttackPhaseLifetime;
        private ulong _attackPhaseLifetime;
#if UNITY_EDITOR
        // 기존 production gate가 native Animator를 실행할 때만 쓰는 controlled clock seam.
        // Player 빌드에는 존재하지 않으며 fresh NGO 경로를 바꾸지 않는다.
        private System.Func<double> _attackPhaseFixtureClock;
        // Existing production gate substitutes transport only, after the real local apply.
        private System.Action<UnitAttackPhasePresentation> _attackPhaseFixturePublish;
#endif

        public bool TryReadAttackPresentationClock(out double server, out ulong generation)
        {
            generation = _attackPhaseLifetime; server = double.NaN;
#if UNITY_EDITOR
            if (_attackPhaseFixtureClock != null) { server = _attackPhaseFixtureClock(); return !double.IsNaN(server) && !double.IsInfinity(server); }
#endif
            if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return false;
            server = NetworkManager.ServerTime.Time;
            return !double.IsNaN(server) && !double.IsInfinity(server);
        }

        public bool PublishAttackPhase(UnitAttackPhasePresentation value)
        {
            bool transportReady = IsSpawned && IsServer;
#if UNITY_EDITOR
            transportReady |= _attackPhaseFixturePublish != null && NetworkContext.IsNetworkServer;
#endif
            if (!transportReady || _attackPhaseEnded) return false;
            value.Lifetime = _attackPhaseLifetime;
            if (!UnitAttackPresentationPolicy.IsValidPhasePayload(value)) return false;
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            if (_unitView == null || !_unitView.CanUseNativeAttackPhase(_unitView.UnitData)) return false;
            if (!_unitView.CanApplyHostNativeAttackPhase()
                || !TryReadAttackPresentationClock(out _, out ulong generation) || generation != value.Lifetime) return false;
            // OLD (pre-RED): wire/NV write -> ReceiveAttackPhase -> unconditional true.
            // _attackPhaseState.Value = new AttackPhaseWire { Value = value };
            // ReceiveAttackPhase(value); AttackPhaseClientRpc(...); return true;
            // Publish/receipt must never outrun Host Root/priority/clock/native application.
            ReceiveAttackPhase(value);
            if (_appliedAttackPhaseRevision != value.PresentationRevision
                || !_pendingAttackPhase.Active || _pendingAttackPhase.EpochId != value.EpochId
                || !new AttackPhaseWire { Value = _pendingAttackPhase }.Equals(new AttackPhaseWire { Value = value })
                || !_unitView.IsServerCombatPresentationSatisfied(value.TargetId,
                    value.TargetIsUnit, value.PresentationRevision)) return false;
#if UNITY_EDITOR
            if (_attackPhaseFixturePublish != null) { _attackPhaseFixturePublish(value); return true; }
#endif
            var wire = new AttackPhaseWire { Value = value };
            _attackPhaseState.Value = wire;
            AttackPhaseClientRpc(wire);
            return true;
        }

        public bool TryGetLiveNativeAttackEpoch(out double epoch)
        {
            epoch = _pendingAttackPhase.EpochServerTime;
            return !_attackPhaseEnded && _pendingAttackPhase.Active && _unitView != null
                && _unitView.HasNativeAttackEpoch(_pendingAttackPhase.Lifetime, _pendingAttackPhase.EpochId);
        }

        public void CloseLogicalAttackScope() => CloseAttackPhase("logical-combat-stop", false);

        // Called on actual View Walk/priority/state exit; never reads Client progress
        // back into gameplay scheduling and never reopens a retired sequence.
        public void InvalidateNativeAttackEpoch(string reason)
        {
            if (_pendingAttackPhase.EpochId != 0)
            {
                _closedAttackEpoch = _pendingAttackPhase.EpochId;
                _closedAttackEpochTime = System.Math.Max(_closedAttackEpochTime, _pendingAttackPhase.EpochServerTime);
            }
            RetirePendingAttackSequence();
            _pendingAttackPhase = default;
        }

        private void RetirePendingAttackSequence()
        {
            if (!_pendingAttackPhase.Scope.IsValid) return;
            if (_closedAttackInstance != _pendingAttackPhase.Scope.AttackerInstanceId.Value) _closedAttackSequence = 0;
            _closedAttackInstance = _pendingAttackPhase.Scope.AttackerInstanceId.Value;
            _closedAttackSequence = System.Math.Max(_closedAttackSequence, _pendingAttackPhase.Scope.SequenceId.Value);
        }

        [ClientRpc]
        private void AttackPhaseClientRpc(AttackPhaseWire value)
        {
            if (!IsServer) ReceiveAttackPhase(value.Value);
        }

        private void OnAttackPhaseChanged(AttackPhaseWire previous, AttackPhaseWire current)
        {
            if (!IsServer) ReceiveAttackPhase(current.Value);
        }

        private void ReceiveAttackPhase(UnitAttackPhasePresentation value)
        {
            if (_attackPhaseEnded || !UnitAttackPresentationPolicy.IsValidPhasePayload(value)) return;
            if (_attackPhaseLifetime != 0 && value.Lifetime != _attackPhaseLifetime) return;
            if (value.PresentationRevision < _appliedAttackPhaseRevision
                || value.PresentationRevision < _pendingAttackPhase.PresentationRevision) return;
            if (!value.Active)
            {
                if (value.EpochId != _pendingAttackPhase.EpochId || value.Lifetime != _attackPhaseLifetime
                    || value.UnitId != _pendingAttackPhase.UnitId
                    || value.EpochServerTime != _pendingAttackPhase.EpochServerTime
                    || value.Duration != _pendingAttackPhase.Duration) return;
                _appliedAttackPhaseRevision = System.Math.Max(_appliedAttackPhaseRevision, value.PresentationRevision);
                CloseAttackPhase("replicated-stop", false);
                return;
            }
            if (value.EpochId == _closedAttackEpoch || value.EpochServerTime <= _closedAttackEpochTime
                || value.PresentationRevision == _appliedAttackPhaseRevision) return;
            if (value.Scope.IsValid && value.Scope.AttackerInstanceId.Value == _closedAttackInstance
                && value.Scope.SequenceId.Value <= _closedAttackSequence) return;
            // RPC와 현재값이 같은 revision이면 전체 원자 payload가 같아야 한다.
            // 준비 전 재전송도 배열/target/scope를 갈아 끼울 수 없다.
            if (_pendingAttackPhase.Active && value.PresentationRevision == _pendingAttackPhase.PresentationRevision
                && !new AttackPhaseWire { Value = value }.Equals(new AttackPhaseWire { Value = _pendingAttackPhase })) return;
            _attackPhaseLifetime = value.Lifetime;
            _pendingAttackPhase = value;
            if (value.Hits != null) _pendingAttackPhase.Hits = (AttackSourceOccurrence[])value.Hits.Clone();
            ApplyCurrentAttackPhase();
        }

        private void ApplyCurrentAttackPhase()
        {
            if (_attackPhaseEnded || !_pendingAttackPhase.Active
                || _pendingAttackPhase.PresentationRevision <= _appliedAttackPhaseRevision) return;
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            // 준비되지 않은 View는 성공/소비 처리하지 않는다. Initialize의 reapply에서 재시도한다.
            if (_unitView != null && _unitView.ApplyAttackPhasePresentation(_pendingAttackPhase))
                _appliedAttackPhaseRevision = _pendingAttackPhase.PresentationRevision;
            else if (_unitView != null && _unitView.AttackPhaseRejectionReason != null
                && _unitView.AttackPhaseRejectionReason != "view-not-ready"
                && _unitView.AttackPhaseRejectionReason != "priority-held"
                && _unitView.AttackPhaseRejectionReason != "clock-not-ready")
            {
                // Metadata divergence is an explicit failed contract, not an endless readiness
                // retry and not a Host-only runtime fallback. Excluded profiles must have chosen
                // the old transport before publish. No acknowledgement of View success here.
                CloseAttackPhase("native-contract-rejected", false);
            }
        }

        public void RetryAttackPhaseViewBinding() => ApplyCurrentAttackPhase();

        private void CloseAttackPhase(string reason, bool terminal)
        {
            bool keepEpoch = UnitAttackPresentationPolicy.KeepsNativeEpoch(reason, terminal);
            // OLD: every scope close poisoned the still-continuous native epoch.
            // if (_pendingAttackPhase.EpochId != 0) _closedAttackEpoch = _pendingAttackPhase.EpochId;
            if (!keepEpoch && _pendingAttackPhase.EpochId != 0)
            {
                _closedAttackEpoch = _pendingAttackPhase.EpochId;
                _closedAttackEpochTime = System.Math.Max(_closedAttackEpochTime, _pendingAttackPhase.EpochServerTime);
            }
            RetirePendingAttackSequence();
            _appliedAttackPhaseRevision = System.Math.Max(_appliedAttackPhaseRevision, _pendingAttackPhase.PresentationRevision);
            if (_pendingAttackPhase.Active && IsSpawned && IsServer)
            {
                var closed = _pendingAttackPhase; closed.Active = false;
                _attackPhaseState.Value = new AttackPhaseWire { Value = closed };
            }
            if (keepEpoch)
            {
                _pendingAttackPhase.Mode = AttackPresentationImpactMode.Suppressed;
                _pendingAttackPhase.Scope = default;
                _pendingAttackPhase.Hits = null;
            }
            else _pendingAttackPhase = default;
            _attackPhaseEnded |= terminal;
            // Walk/server-state-exit is a real playback exit, not a logical stop.
            if (_unitView != null) _unitView.CloseAttackPhasePresentation(reason, keepEpoch);
        }

        // 원자 wire: 배열 크기부터 검증하여 무한 할당을 막고, per-hit 값을 그대로 복사한다.
        public struct AttackPhaseWire : INetworkSerializable, System.IEquatable<AttackPhaseWire>
        {
            public UnitAttackPhasePresentation Value;
            public bool Equals(AttackPhaseWire other)
            {
                var a = Value; var b = other.Value;
                if (a.Lifetime != b.Lifetime || a.EpochId != b.EpochId || a.PresentationRevision != b.PresentationRevision
                    || a.ActionRevision != b.ActionRevision || a.Active != b.Active || a.UnitId != b.UnitId
                    || a.TargetId != b.TargetId || a.TargetIsUnit != b.TargetIsUnit || a.EpochServerTime != b.EpochServerTime
                    || a.Duration != b.Duration || a.CommitServerTime != b.CommitServerTime || a.Mode != b.Mode
                    || !a.Scope.Equals(b.Scope) || (a.Hits?.Length ?? 0) != (b.Hits?.Length ?? 0)) return false;
                for (int i = 0; i < (a.Hits?.Length ?? 0); i++)
                {
                    var x = a.Hits[i]; var y = b.Hits[i];
                    if (x.HitIndex != y.HitIndex || x.Loop != y.Loop || x.MarkerOffset != y.MarkerOffset
                        || x.MarkerServerTime != y.MarkerServerTime || x.ImpactServerTime != y.ImpactServerTime) return false;
                }
                return true;
            }
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref Value.Lifetime); s.SerializeValue(ref Value.EpochId);
                s.SerializeValue(ref Value.PresentationRevision); s.SerializeValue(ref Value.ActionRevision);
                s.SerializeValue(ref Value.UnitId); s.SerializeValue(ref Value.TargetId);
                s.SerializeValue(ref Value.TargetIsUnit); s.SerializeValue(ref Value.Active);
                s.SerializeValue(ref Value.EpochServerTime); s.SerializeValue(ref Value.Duration);
                s.SerializeValue(ref Value.CommitServerTime);
                byte mode = (byte)Value.Mode; s.SerializeValue(ref mode); Value.Mode = (AttackPresentationImpactMode)mode;
                ulong instance = Value.Scope.AttackerInstanceId.Value, sequence = Value.Scope.SequenceId.Value;
                int first = Value.Scope.HitIndex;
                s.SerializeValue(ref instance); s.SerializeValue(ref sequence); s.SerializeValue(ref first);
                Value.Scope = new AttackPresentationScope(new AttackerInstanceId(instance), new AttackSequenceId(sequence), first);
                int count = Value.Hits?.Length ?? 0; s.SerializeValue(ref count);
                if (count < 0 || count > 64) throw new System.InvalidOperationException("Attack occurrence wire count");
                if (s.IsReader) Value.Hits = new AttackSourceOccurrence[count];
                for (int i = 0; i < count; i++)
                {
                    s.SerializeValue(ref Value.Hits[i].HitIndex); s.SerializeValue(ref Value.Hits[i].Loop);
                    s.SerializeValue(ref Value.Hits[i].MarkerOffset); s.SerializeValue(ref Value.Hits[i].MarkerServerTime);
                    s.SerializeValue(ref Value.Hits[i].ImpactServerTime);
                }
            }
        }

        /// <summary>
        /// 클라이언트에서 UnitView 초기화가 완료되었는지 여부.
        /// SpawnUnitClientRpc에서 중복 초기화를 방지하기 위해 사용.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 게임 서비스 참조. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화.
        /// OnNetworkSpawn에서 1회만 획득하여 캐시하고 이후에는 재탐색 없이 사용한다.
        /// RegisterToFactory에서 반복 접근을 방지하기 위한 캐시.
        /// </summary>
        private IGameServices _services;

        private NetworkVariable<NetworkUnitHealCastState> _healCastState =
            new NetworkVariable<NetworkUnitHealCastState>(default,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Independent recipient snapshot. Caster animation state and HP sync cannot own this lifetime.
        private readonly NetworkVariable<NetworkBloomHealEffectState> _bloomEffectState =
            new NetworkVariable<NetworkBloomHealEffectState>(default,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private BloomHealEffectCursor _bloomCursor;
        private BloomHealEffectChannel _bloomChannel;
        private Hexiege.Domain.UnitData _bloomTarget;
        private bool _bloomClosed;
        private ulong _bloomAppliedRevision;
        private bool _bloomReadyPending;
        public BloomHealEffectPresentation BloomEffectState => _bloomEffectState.Value.Value;

        public void PublishBloomEffect(BloomHealEffectPresentation value, Hexiege.Domain.UnitData target)
        {
            if (!IsSpawned || !IsServer || _bloomClosed || value.TargetId != UnitId) return;
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            var view = _unitView;
            if (view == null || view.Data == null) { _bloomReadyPending = true; return; }
            if (!ReferenceEquals(view.Data, target))
            { _bloomChannel?.RecordRejection(value, BloomHealAdmission.BindingRejected, "PublishViewBinding"); return; }
            if (_bloomChannel != null && _bloomChannel.IsClosed
                && value.MatchGeneration != _bloomCursor.Current.MatchGeneration)
            { _bloomCursor = new BloomHealEffectCursor(); _bloomTarget = null; _bloomChannel = null; }
            if (_bloomTarget != null && !ReferenceEquals(_bloomTarget, target))
            { _bloomChannel?.RecordRejection(value, BloomHealAdmission.BindingRejected, "PublishTargetBinding"); return; }
            if (!_bloomCursor.TryAccept(value))
            { _bloomChannel?.RecordRejection(value, _bloomCursor.Classify(value), "PublishCursor"); return; }
            _bloomTarget = target;
            _bloomReadyPending = false;
            _bloomChannel ??= GameServicesLocator.Current?.GetCombatUseCase()?.BloomHealEffects;
            _bloomEffectState.Value = new NetworkBloomHealEffectState { Value = value };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _bloomChannel?.Diagnostics.Record("Publish", value);
#endif
            // Host already consumes the Application channel through the sole presenter, not this callback.
        }
        private void OnBloomEffectChanged(NetworkBloomHealEffectState before, NetworkBloomHealEffectState after)
        {
            if (!after.Value.IsValid)
                _bloomChannel?.RecordRejection(after.Value, BloomHealAdmission.Invalid, "NetworkChanged");
            if (_bloomChannel != null && _bloomChannel.IsClosed
                && after.Value.MatchGeneration != _bloomCursor.Current.MatchGeneration)
            { _bloomCursor = new BloomHealEffectCursor(); _bloomTarget = null; _bloomChannel = null; _bloomAppliedRevision = 0; }
            _bloomReadyPending = true; ApplyCurrentBloomEffect();
        }
        private void ApplyCurrentBloomEffect()
        {
            if (_bloomClosed || !IsSpawned || !_bloomReadyPending) return;
            var current = BloomEffectState;
            if (!IsServer && (!current.IsValid || current.Revision == _bloomAppliedRevision))
            {
                // 빈 current는 spawn 초기 상태다. 실제 valid 동일-revision 수신만 분류한다.
                // 캐시만 사용하므로 이 fast path에 services/GetComponent 조회를 추가하지 않는다.
                if (current.IsValid) _bloomChannel?.RecordRejection(current, _bloomCursor.Classify(current), "ApplyAlreadyRevision");
                _bloomReadyPending = false; return;
            }
            if (_bloomChannel == null || _bloomChannel.IsClosed)
                _bloomChannel = GameServicesLocator.Current?.GetCombatUseCase()?.BloomHealEffects;
            var channel = _bloomChannel;
            if (channel != null && channel.IsClosed) { _bloomReadyPending = false; return; }
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            var target = _unitView != null ? _unitView.Data : null;
            if (channel == null || target == null || target.Id != UnitId) return;
            _bloomChannel = channel;
            if (_bloomTarget != null && !ReferenceEquals(_bloomTarget, target))
            { _bloomChannel.RecordRejection(current, BloomHealAdmission.BindingRejected, "ApplyTargetBinding"); _bloomReadyPending = false; return; }
            if (IsServer)
            {
                if (channel.TryGet(target, out var baseline) && baseline.Revision != BloomEffectState.Revision)
                    PublishBloomEffect(baseline, target);
                _bloomReadyPending = false;
                return;
            }
            _bloomReadyPending = false; // Terminal rejection is not a readiness retry.
            if (!current.IsValid || current.TargetId != UnitId)
            { channel.RecordRejection(current, BloomHealAdmission.Invalid, "ApplyPayload"); return; }
            if (!channel.BindRecipient(target, current.MatchGeneration, current.TargetLifetime))
            { channel.RecordRejection(current, BloomHealAdmission.BindingRejected, "ApplyBind"); return; }
            if (!_bloomCursor.TryAccept(current))
            { channel.RecordRejection(current, _bloomCursor.Classify(current), "ApplyCursor"); return; }
            // 새 channel의 baseline이 이미 같은 current를 갖는 경우도 준비 완료다.
            // 그 외에는 실제 수락 뒤에만 applied/Receive를 기록하여 거부를 성공으로 꾸미지 않는다.
            bool alreadyCurrent = channel.TryGet(target, out var accepted) && accepted.Equals(current);
            if (!alreadyCurrent && !channel.Accept(current, target)) return;
            _bloomTarget = target;
            _bloomAppliedRevision = current.Revision;
            _bloomReadyPending = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            channel.Diagnostics.Record("Receive", current);
#endif
        }
        private readonly NetworkVariable<NetworkTimedDamageEffectState> _damageState =
            new NetworkVariable<NetworkTimedDamageEffectState>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private TimedDamageEffectChannel _damageChannel;
        private Hexiege.Domain.UnitData _damageTarget;
        private bool _damageClosed;
        private TimedDamageEffectPresentation _damageAttempt;
        private IGameServices _damageServices;
        private ulong _damageRetiredMatch;
        private ulong _damageBoundMatch;
        private void ApplyCurrentTimedDamage()
        {
            if (_damageClosed || !IsSpawned) return;
            var services = GameServicesLocator.Current;
            var channel = _damageChannel != null && !_damageChannel.Closed && ReferenceEquals(_damageServices, services)
                ? _damageChannel : services?.GetCombatUseCase()?.TimedDamageEffects;
            if (channel == null || channel.Closed) return;
            if (!ReferenceEquals(_damageChannel, channel))
            {
                if (_damageChannel != null && _damageChannel.Closed) _damageRetiredMatch = _damageBoundMatch;
                _damageChannel?.CloseAttachment(_damageTarget, "ServiceReinitialize");
                _damageAttempt = default; _damageTarget = null; _damageChannel = channel; _damageServices = services;
            }
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            var target = _unitView != null ? _unitView.Data : null;
            if (target == null || target.Id != UnitId) return;
            if (_damageTarget != null && !ReferenceEquals(_damageTarget, target))
            { _damageChannel?.CloseAttachment(_damageTarget, "BindingReplaced"); _damageClosed = true; return; }
            _damageTarget = target; _damageChannel = channel;
            if (IsServer)
            {
                if (channel.TryGet(target, out var state) && !_damageState.Value.Value.Equals(state))
                { _damageBoundMatch = state.Match; _damageState.Value = new NetworkTimedDamageEffectState { Value = state }; channel.Record("Publish", state, "CurrentValue"); }
            }
            else
            {
                var state = _damageState.Value.Value;
                if (!state.IsValid || state.Match == _damageRetiredMatch) return;
                if (channel.TryGet(target, out var current) && current.Equals(state)) return;
                // current value가 바뀌거나 실제 service owner가 교체될 때만 다시 시도한다.
                // 준비 이전에는 attempt를 저장하지 않으므로 late init은 현재값을 복원한다.
                if (_damageAttempt.Equals(state)) return;
                _damageAttempt = state;
                if (channel.Receive(state, target)) _damageBoundMatch = state.Match;
            }
        }
        private void LateUpdate()
        {
            try { ApplyCurrentTimedDamage(); }
            catch (System.Exception ex) { _damageChannel?.Record("Fault", _damageState.Value.Value, ex.GetType().Name + ": " + ex.Message); }
            // Readiness retry observes current state, never replays a missed historical begin.
            try { ApplyCurrentBloomEffect(); }
            catch (System.Exception e) { BloomHealEffectChannel.Fault(e, "RecipientReady"); }
        }
        private void CloseBloomAttachment(string reason)
        {
            if (_bloomClosed) return;
            _bloomClosed = true;
            _bloomReadyPending = false;
            _bloomChannel?.CloseAttachment(_bloomTarget, reason);
            _bloomTarget = null;
            // No server stop or mutation of the actual HoT list on disconnect/despawn.
        }

        public UnitHealCastPresentation HealCastState => _healCastState.Value.Value;
        private bool _healClosed;
        private System.IDisposable _healEndSubscription;
        public double HealServerTime => IsSpawned && NetworkManager != null
            ? NetworkManager.ServerTime.Time : Time.timeAsDouble;

        /// <summary>서버 힐 시작/종료를 atomic 값으로 게시한다. 일반 적 공격 RPC는 호출하지 않는다.</summary>
        public void PublishHealCast(UnitHealCastPresentation state)
        {
            if (!IsSpawned || !IsServer || _healClosed || !state.IsValid || state.UnitId != UnitId) return;
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            if (_unitView == null || _unitView.Data == null || !_unitView.Data.IsHealer) return;
            var previous = HealCastState;
            if (previous.IsValid && (previous.Lifetime != state.Lifetime || state.Sequence < previous.Sequence
                || (state.Sequence == previous.Sequence && !previous.Active && state.Active))) return;
            state.Revision = previous.Revision + 1;
            _healCastState.Value = new NetworkUnitHealCastState { Value = state };
            UnitHealCastDiagnostics.Event("Publish", state, $"NetworkObjectId={NetworkObjectId}, Now={HealServerTime:G17}");
            // Host 표현은 로컬 시작이 이미 수행했다. revision/priority만 같은 값으로 수렴한다.
            _unitView.ApplyHealPresentation(state, HealServerTime);
        }

        private void OnHealCastChanged(NetworkUnitHealCastState previous, NetworkUnitHealCastState current)
        {
            UnitHealCastDiagnostics.Event("Receive", current.Value, $"NetworkObjectId={NetworkObjectId}, Now={HealServerTime:G17}");
            ApplyCurrentHealCast();
        }

        private void ApplyCurrentHealCast()
        {
            if (_healClosed || !HealCastState.IsValid || HealCastState.UnitId != UnitId) return;
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            _unitView?.ApplyHealPresentation(HealCastState, HealServerTime);
        }

        private void CloseHealCast(string reason)
        {
            if (_healClosed) return;
            if (IsServer && IsSpawned && HealCastState.Active)
            {
                var state = HealCastState;
                state.Active = false;
                state.EndedAt = HealServerTime;
                state.EndReason = reason == "game-end" ? (byte)5 : (byte)6;
                PublishHealCast(state);
            }
            _healClosed = true;
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            _unitView?.EndHealPresentation(reason, terminal: true);
        }

        // ====================================================================
        // 서버 전용 API
        // ====================================================================

        /// <summary>
        /// 서버에서 유닛 스폰 후 호출하여 unitId를 NetworkVariable에 설정.
        /// 이 값은 NGO가 자동으로 모든 클라이언트에 동기화.
        /// UnitFactory.CreateUnitObject()에서 NetworkObject.Spawn() 이후 호출.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id (UnitData.Id)</param>
        public void SetUnitId(int unitId)
        {
            _unitId.Value = unitId;
        }

        /// <summary>
        /// [Phase 2] 서버에서 유닛 애니메이션 상태를 설정한다(서버 권위).
        /// NetworkCombatController의 서버 전용 핸들러(Walk/Attack 진입 지점)에서 호출한다.
        ///
        /// 같은 값을 다시 써도 NGO가 전송하지 않으므로(값 동일) 매 틱 호출해도 안전하며,
        /// 이 특성 덕분에 애니메이션 목적의 중복 방지 가드(예: _combatAnimationSent)가 불필요하다.
        /// (단, _combatAnimationSent는 데미지/타겟 RPC를 게이팅하는 별개의 기능 가드로 그대로 유지된다.)
        /// </summary>
        /// <param name="state">설정할 애니메이션 상태(Walk / Attack 등).</param>
        public void SetAnimState(UnitAnimState state)
        {
            // 방어적 가드 — WritePermission=Server이므로 클라이언트에서 호출되면 무시.
            if (!IsServer) return;

            if (state == UnitAnimState.Walk || state == UnitAnimState.None)
                CloseAttackPhase("server-state-exit", false);

            _animState.Value = (byte)state;
            if (HealCastState.IsValid)
            {
                var heal = HealCastState;
                byte priority = state == UnitAnimState.Frozen || state == UnitAnimState.Held
                    ? (byte)state : (byte)UnitAnimState.Walk;
                if (heal.Priority != priority || (!heal.Active && heal.EndReason != 0))
                {
                    heal.Priority = priority;
                    if (!heal.Active) heal.EndReason = 0;
                    PublishHealCast(heal);
                }
            }
        }

        /// <summary>ReducerAuthoritative 서버가 수락한 이동 snapshot만 게시한다.</summary>
        public bool PublishMovementSnapshot(UnitMovementSnapshot snapshot)
        {
            if (!IsServer || !snapshot.HasAcceptedObservation)
            {
                return false;
            }

            if (_movementPhase.Value == (byte)snapshot.Phase
                && _movementCommandRevision.Value == snapshot.CommandRevision
                && _movementSegmentRevision.Value == snapshot.SegmentRevision)
            {
                return true;
            }

            if (_movementSemanticRevision.Value == ulong.MaxValue)
                return false;

            _movementPhase.Value = (byte)snapshot.Phase;
            _movementCommandRevision.Value = snapshot.CommandRevision;
            _movementSegmentRevision.Value = snapshot.SegmentRevision;
            _movementSemanticRevision.Value++;
            return true;
        }

        /// <summary>
        /// 서버 coordinator가 수락한 Shadow snapshot만 원자 값으로 게시한다.
        /// 변환이나 게시 실패는 Legacy gameplay에 전달되지 않으며 기존 상태를 그대로 유지한다.
        /// </summary>
        public bool PublishAttackShadowSnapshot(UnitAttackShadowPublication publication)
        {
            if (!IsServer
                || !NetworkUnitActionShadowState.TryCreate(publication, out NetworkUnitActionShadowState state))
                return false;

            if (_attackShadowPublicationClassifier == null)
                _attackShadowPublicationClassifier = new UnitAttackShadowReplicationClassifier();
            UnitAttackShadowReplicationStatus status =
                _attackShadowPublicationClassifier.Classify(NetworkObjectId, state);
            if (status == UnitAttackShadowReplicationStatus.Duplicate) return true;
            if (status != UnitAttackShadowReplicationStatus.Accepted) return false;

            _attackShadowState.Value = state;
            return true;
        }

        /// <summary>
        /// Every committed HitIndex is sent reliably, independently of the coalescing state variable.
        /// It carries provisional direction only. Final aim and outcome still come exclusively from C2.
        /// </summary>
        public void PublishAttackPresentationSchedule(AttackPresentationSchedule schedule)
        {
            if (!IsServer || !IsSpawned || !schedule.IsValid) return;
            _attackPresentationInstanceId = schedule.Scope.AttackerInstanceId;
            UnitAttackResultPresentationShadowBridge.ObserveSchedule(schedule);
            AttackPresentationScheduleClientRpc(schedule.AttackerUnitId,
                schedule.Scope.AttackerInstanceId.Value, schedule.Scope.SequenceId.Value,
                schedule.Scope.HitIndex, schedule.CommitRevision, (byte)schedule.Delivery,
                schedule.ImpactServerTime, schedule.CommitDirection.X, schedule.CommitDirection.Z);
        }

        [ClientRpc]
        private void AttackPresentationScheduleClientRpc(int unitId, ulong instanceId,
            ulong sequenceId, int hitIndex, ulong commitRevision, byte delivery,
            double impactServerTime, double directionX, double directionZ)
        {
            if (IsServer) return;
            ActionDirectionXZ.TryCreate(directionX, directionZ, out var direction);
            var schedule = new AttackPresentationSchedule(unitId,
                new AttackPresentationScope(new AttackerInstanceId(instanceId),
                    new AttackSequenceId(sequenceId), hitIndex), commitRevision,
                (AttackDeliveryKind)delivery, impactServerTime, direction);
            if (schedule.IsValid) _attackPresentationInstanceId = schedule.Scope.AttackerInstanceId;
            UnitAttackResultPresentationShadowBridge.ObserveSchedule(schedule);
        }

        /// <summary>
        /// Legacy writer가 확정한 개별 ImpactResult를 Reliable Shadow 메시지로 보낸다.
        /// 이 RPC는 관측 전용이며 Client의 HP·Animator·VFX를 갱신하지 않는다.
        /// </summary>
        public bool PublishAttackShadowImpactResult(
            AttackImpactResult result,
            AttackDeliveryKind delivery)
        {
            if (!IsServer
                || !NetworkAttackImpactShadowResult.TryCreate(
                    result, delivery, out NetworkAttackImpactShadowResult state))
                return false;

            if (_attackShadowImpactClassifier == null)
                _attackShadowImpactClassifier = new UnitAttackShadowImpactReplicationClassifier();
            UnitAttackShadowImpactReplicationStatus status =
                _attackShadowImpactClassifier.Classify(NetworkObjectId, state);
            if (status == UnitAttackShadowImpactReplicationStatus.Duplicate) return true;
            if (status != UnitAttackShadowImpactReplicationStatus.Accepted) return false;

            if (state.TryToPresentationInput(_unitId.Value, out AttackResultPresentationInput input))
            {
                _attackPresentationInstanceId = input.Key.AttackerInstanceId;
                PistoleerTimingDiagnostics.Register(_unitView?.Data ?? GetComponent<UnitView>()?.Data, AttackPresentationScope.FromResultKey(input.Key), 0);
                PistoleerTimingDiagnostics.Event(_unitView?.Data ?? GetComponent<UnitView>()?.Data, "Receive", AttackPresentationScope.FromResultKey(input.Key),
                    NetworkManager.ServerTime.Time, $"outcome={input.Outcome} impact={PistoleerTimingDiagnostics.Number(input.ImpactServerTime)} revision={input.ActionRevision} amount={input.AppliedAmount} hp={input.ResultingHp} victim={input.Key.VictimId} kind={input.Key.VictimKind} effect={input.Key.EffectKind} ordinal={input.Key.ResultOrdinal}");
                UnitAttackResultPresentationShadowBridge.ObserveResult(
                    input, NetworkManager.ServerTime.Time,
                    UnityEngine.Time.realtimeSinceStartupAsDouble);
            }

            AttackImpactShadowResultClientRpc(state);
            return true;
        }

        [ClientRpc]
        private void AttackImpactShadowResultClientRpc(NetworkAttackImpactShadowResult state)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (IsServer || _unitId.Value < 0) return;
            UnitAttackShadowObserver.ObserveClientImpactResult(
                _unitId.Value,
                NetworkObjectId,
                state);
            UnitAttackShadowImpactReplicationStatus status =
                _attackShadowImpactClassifier.Classify(NetworkObjectId, state);
            if (status == UnitAttackShadowImpactReplicationStatus.Accepted
                && state.TryToPresentationInput(_unitId.Value, out AttackResultPresentationInput input))
            {
                _attackPresentationInstanceId = input.Key.AttackerInstanceId;
                PistoleerTimingDiagnostics.Register(_unitView?.Data ?? GetComponent<UnitView>()?.Data, AttackPresentationScope.FromResultKey(input.Key), 0);
                PistoleerTimingDiagnostics.Event(_unitView?.Data ?? GetComponent<UnitView>()?.Data, "Receive", AttackPresentationScope.FromResultKey(input.Key),
                    NetworkManager.ServerTime.Time, $"outcome={input.Outcome} impact={PistoleerTimingDiagnostics.Number(input.ImpactServerTime)} revision={input.ActionRevision} amount={input.AppliedAmount} hp={input.ResultingHp} victim={input.Key.VictimId} kind={input.Key.VictimKind} effect={input.Key.EffectKind} ordinal={input.Key.ResultOrdinal}");
                UnitAttackResultPresentationShadowBridge.ObserveResult(
                    input, NetworkManager.ServerTime.Time,
                    UnityEngine.Time.realtimeSinceStartupAsDouble);
            }
#endif
        }

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 호출.
        /// 서버: 아무 작업 없음 (UnitFactory에서 직접 초기화 완료).
        /// 클라이언트: unitId가 이미 동기화되었으면 즉시 등록,
        ///            아니면 OnValueChanged 콜백을 등록하여 값이 도착할 때 등록.
        ///
        /// OnValueChanged 콜백 방식:
        ///   NetworkVariable의 값이 변경되면 NGO가 자동으로 콜백을 호출.
        ///   폴링(매 프레임 확인)보다 효율적이고, 값 변경 즉시 반응하므로
        ///   Walk RPC가 도착하기 전에 딕셔너리 등록을 완료할 수 있음.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _damageClosed = false; _damageTarget = null; _damageChannel = null; _damageAttempt = default;
            _damageServices = null; _damageRetiredMatch = _damageBoundMatch = 0;
            if (IsServer) _damageState.Value = default;
            _bloomClosed = false; _bloomAppliedRevision = 0; _bloomTarget = null; _bloomChannel = null;
            _bloomCursor = new BloomHealEffectCursor();
            _bloomReadyPending = true;
            if (IsServer) _bloomEffectState.Value = default;
            _bloomEffectState.OnValueChanged += OnBloomEffectChanged;
            _attackPhaseEnded = false; _closedAttackEpoch = _appliedAttackPhaseRevision = 0;
            _closedAttackEpochTime = double.NegativeInfinity;
            _closedAttackInstance = _closedAttackSequence = 0;
            _pendingAttackPhase = default;
            _attackPhaseLifetime = IsServer ? ++_nextAttackPhaseLifetime : 0UL;
            _attackPhaseState.OnValueChanged += OnAttackPhaseChanged;
            _healClosed = false;
            _healEndSubscription?.Dispose();
            _healEndSubscription = GameEvents.OnGameEnd.Subscribe(e => { CloseHealCast("game-end"); CloseAttackPhase("game-end", true); });
            if (IsServer) _healCastState.Value = default;

            _attackShadowPublicationClassifier =
                new UnitAttackShadowReplicationClassifier();
            _attackShadowImpactClassifier =
                new UnitAttackShadowImpactReplicationClassifier();

            // 서버는 UnitFactory.CreateUnitObject()에서 이미 모든 초기화 완료
            if (IsServer) return;

            // 클라이언트 측에서만 RegisterToFactory가 호출되므로, 클라이언트 경로에서만 캐시한다.
            // GameServicesLocator에서 꺼내므로 FindFirstObjectByType<GameBootstrapper>() 불필요.
            _services = GameServicesLocator.Current;

            // ────────────────────────────────────────────────────────────────
            // [Phase 2] 애니메이션 상태 레벨 동기화 — 클라이언트 전용.
            //   1) 값 변경 콜백 구독: 서버가 상태를 바꿀 때마다 OnAnimStateChanged가 호출된다.
            //   2) 스폰 시 현재 값 즉시 적용: 이 유닛이 스폰되기 "전에" 서버가 이미 상태를 설정했다면
            //      OnValueChanged로는 다시 통지되지 않으므로, 지금 현재 값을 직접 읽어 적용한다.
            //      → 이것이 스폰 레이스(구독 전 신호 도착)를 구조적으로 없애는 핵심이다.
            //   (이 코드는 클라이언트 경로에서만 실행된다 — 위에서 IsServer일 때 이미 return했다.)
            // ────────────────────────────────────────────────────────────────
            _animState.OnValueChanged += OnAnimStateChanged;
            _healCastState.OnValueChanged += OnHealCastChanged;
            _movementSemanticRevision.OnValueChanged +=
                OnMovementSemanticRevisionChanged;
            _attackShadowState.OnValueChanged += OnAttackShadowStateChanged;
            ApplySpawnAnimState();
            ApplyCurrentHealCast();

            ReceiveAttackPhase(_attackPhaseState.Value.Value);
            ApplyCurrentAttackPhase();

            // 클라이언트: unitId가 이미 설정되었으면 즉시 딕셔너리에 등록
            if (_unitId.Value != -1)
            {
                RegisterToFactory(_unitId.Value);
                ObserveCurrentMovementSnapshot();
                ObserveCurrentAttackShadowState();
                return;
            }

            // unitId가 아직 도착하지 않았으면 OnValueChanged 콜백으로 대기.
            // NGO가 서버에서 NetworkVariable 값이 변경되면 이 콜백을 자동 호출.
            // 폴링 코루틴 대신 콜백을 사용하여 불필요한 매 프레임 체크를 제거.
            _unitId.OnValueChanged += OnUnitIdReceived;
        }

        /// <summary>
        /// NetworkVariable(_unitId)의 값이 변경되었을 때 NGO가 호출하는 콜백.
        /// 새 값이 -1이 아니면(유효한 unitId) UnitFactory에 등록하고 콜백을 해제.
        /// 콜백 해제를 하지 않으면 이후 값 변경 시 중복 등록이 발생할 수 있으므로 반드시 해제.
        /// </summary>
        /// <param name="previousValue">변경 전 값 (보통 -1, 초기 상태).</param>
        /// <param name="newValue">변경 후 값 (서버가 설정한 unitId).</param>
        private void OnUnitIdReceived(int previousValue, int newValue)
        {
            if (newValue == -1) return;

            // 유효한 unitId 도착 → UnitFactory 딕셔너리에 등록
            RegisterToFactory(newValue);
            ApplyCurrentHealCast();

            // 등록 완료 후 콜백 해제 — 1회만 실행하면 되므로 더 이상 구독할 필요 없음
            _unitId.OnValueChanged -= OnUnitIdReceived;
            // semantic snapshot이 unitId보다 먼저 coalesce되어 도착했더라도 등록 후 한 번 수집한다.
            ObserveCurrentMovementSnapshot();
            ObserveCurrentAttackShadowState();
        }

        // ====================================================================
        // [Phase 2] 애니메이션 상태 적용 (클라이언트 전용)
        // ====================================================================

        /// <summary>
        /// [Phase 2] 스폰 시 현재 애니메이션 상태 값을 즉시 적용한다.
        /// 값이 None(미설정)이면 아무것도 하지 않고, 이후 값이 설정되면 OnAnimStateChanged가 적용한다.
        /// "구독 전에 도착한 신호가 유실되던" 엣지 트리거 구조의 스폰 레이스를 여기서 구조적으로 없앤다.
        /// </summary>
        private void ApplySpawnAnimState()
        {
            byte current = _animState.Value;
            if (current == (byte)UnitAnimState.None) return;

            ApplyAnimState(current);
        }

        /// <summary>
        /// [Phase 2 후속 — 적용 순서 틈 봉합] 현재 애니메이션 상태 값을 UnitView에 "다시 한 번" 적용한다.
        ///
        /// 왜 필요한가 (초급자용 설명):
        ///   클라이언트에서 이 NetworkUnit은 스폰되는 순간(OnNetworkSpawn)에 서버의 현재 애니메이션
        ///   상태를 즉시 적용한다(ApplySpawnAnimState). 그런데 이 시점은 같은 GameObject의
        ///   UnitView.Initialize(=Animator를 캐시하는 지점)보다 "먼저" 실행될 수 있다. 그 순간 아직
        ///   메시/Animator가 준비되지 않았으면 StartWalkAnimation이 조용히 반환(early-return)하고,
        ///   레벨 값(NetworkVariable)은 그대로라 OnValueChanged가 다시 호출되지 않아 "재시도"가 없다.
        ///   → 간헐적으로 걷기/공격 애니메이션이 무음 실패(적용 누락)한다.
        ///
        /// 해결:
        ///   UnitView.Initialize가 Animator를 캐시한 "직후"에 이 메서드를 호출하면, 준비된 Animator로
        ///   현재 값을 반드시 재적용하여 조기 적용 실패로 벌어진 틈을 닫는다.
        ///
        /// 멱등성(안전):
        ///   레벨(값 기반) 동기화이므로 "현재 값 재적용"은 무해하다. 이미 올바르게 적용된 유닛에 다시
        ///   호출해도 같은 상태로 CrossFade가 한 번 더 걸리는 정도이며, StartWalkAnimation은 이미 Walk
        ///   재생 중이면 speed만 복원하는 식으로 흡수한다. 값이 None이면 아무것도 하지 않는다.
        ///
        /// 스킵 대상:
        ///   - 싱글플레이: NGO 미스폰이라 IsSpawned=false → 즉시 반환(_animState 동기화 경로 자체가 없음).
        ///   - 호스트/서버: 서버는 Animator를 직접 제어하는 기존 관례이므로 재적용하면 안 됨(IsServer 스킵).
        ///     (OnNetworkSpawn에서 IsServer일 때 구독/초기적용을 하지 않는 것과 동일한 취지.)
        /// </summary>
        public void ReapplyAnimStateToView()
        {
            ApplyCurrentAttackPhase();
            // IsSpawned 먼저 검사(|| 단락 평가) — 싱글플레이(미스폰)에서는 IsServer를 건드리지 않고 반환.
            if (!IsSpawned || IsServer) return;

            ApplyCurrentHealCast();

            byte current = _animState.Value;
            if (current == (byte)UnitAnimState.None) return;

            ApplyAnimState(current);
        }

        /// <summary>
        /// [Phase 2] 서버가 애니메이션 상태를 바꿀 때 NGO가 호출하는 콜백(클라이언트 전용).
        /// 새 값으로 애니메이션을 즉시 적용한다.
        /// </summary>
        /// <param name="previousValue">변경 전 상태(byte).</param>
        /// <param name="newValue">변경 후 상태(byte).</param>
        private void OnAnimStateChanged(byte previousValue, byte newValue)
        {
            ApplyAnimState(newValue);
        }

        private void OnMovementSemanticRevisionChanged(
            ulong previousValue,
            ulong newValue)
        {
            ObserveCurrentMovementSnapshot();
        }

        private void OnAttackShadowStateChanged(
            NetworkUnitActionShadowState previousValue,
            NetworkUnitActionShadowState newValue)
        {
            ObserveCurrentAttackShadowState();
        }

        private void ObserveCurrentAttackShadowState()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (IsServer || _unitId.Value < 0 || !_attackShadowState.Value.IsValid) return;
            UnitAttackShadowObserver.ObserveClientReplicatedState(
                _unitId.Value,
                NetworkObjectId,
                _attackShadowState.Value);
#endif
        }

        private void ObserveCurrentMovementSnapshot()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (IsServer
                || !UnitMovementAuthorityObserver.ShouldObserveClientReplicatedState(
                    _unitId.Value,
                    _movementSemanticRevision.Value))
                return;

            UnitMovementAuthorityObserver.ObserveClientReplicatedState(
                _unitId.Value,
                NetworkObjectId,
                (UnitMovementPhase)_movementPhase.Value,
                _movementCommandRevision.Value,
                _movementSegmentRevision.Value,
                _movementSemanticRevision.Value);
#endif
        }

        /// <summary>
        /// [Phase 2] 애니메이션 상태 값을 같은 GameObject의 UnitView에 적용한다(클라이언트 전용).
        ///
        /// 호스트/서버는 이 경로를 타지 않는다 — OnNetworkSpawn에서 IsServer일 때 이미 return하므로
        /// _animState.OnValueChanged를 애초에 구독하지 않고, 스폰 시 적용도 하지 않는다.
        /// 호스트의 애니메이션은 서버 로직(MoveAlongPathV3의 Walk CrossFade, StartCombatAnimation의
        /// Attack CrossFade)이 Animator를 직접 제어하므로 여기서 중복 적용하면 안 된다.
        ///
        /// UnitView는 같은 프리팹 루트의 컴포넌트다(OnNetworkDespawn에서도 GetComponent로 접근).
        /// _animator/Initialize보다 먼저 호출될 수 있으므로 UnitView 쪽 메서드가 animator를 지연 초기화한다.
        /// </summary>
        /// <param name="state">적용할 애니메이션 상태(byte).</param>
        private void ApplyAnimState(byte state)
        {
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            if (_unitView == null) return;

            // 활성 힐의 priority는 같은 atomic snapshot에 들어 있다. 늦게 도착한
            // 독립 Walk/Frozen/Held scalar로 더 최신 힐 snapshot을 덮지 않는다.
            if (HealCastState.IsValid && _unitView.Data != null && _unitView.Data.IsHealer)
            {
                ApplyCurrentHealCast();
                return;
            }

            switch ((UnitAnimState)state)
            {
                case UnitAnimState.Walk:
                    _unitView.StartWalkAnimation();
                    break;
                case UnitAnimState.Attack:
                    // Attack 표현은 revision+target+impactEnabled를 한 payload로 가진
                    // StartCombatClientRpc만 시작한다. 이 별도 NetworkVariable이 먼저
                    // 도착해 stale impact=true 상태로 클립을 단독 재생하면 provisional
                    // suppression보다 Animation Event가 앞설 수 있으므로 여기서는 no-op한다.
                    if (UnitAttackPresentationPolicy.ShouldStartAttackFromReplicatedLevelState())
                        _unitView.PlayAttackAnimation();
                    break;
                case UnitAnimState.Frozen:
                    // [스킬 - 빙결] 걷기 클립을 유지한 채 speed=0으로 정지 → 순수 클라의 "제자리걸음" 제거.
                    _unitView.FreezeWalkAnimation();
                    break;
                case UnitAnimState.Held:
                    _unitView.HoldMovementAnimation();
                    break;
                default:
                    // 늦은 참가/기본 None도 이전 수명의 Impact 승인을 상속하지 않는다.
                    _unitView.SuppressPendingAttackImpactPresentation();
                    break;
            }
        }

        /// <summary>
        /// 네트워크 디스폰 시 호출.
        /// OnValueChanged 콜백이 아직 등록되어 있을 수 있으므로 안전하게 해제.
        /// 예: unitId가 도착하기 전에 오브젝트가 Despawn된 경우
        ///     콜백이 남아있으면 파괴된 오브젝트에 접근하는 버그 발생 가능.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            _damageClosed = true;
            _damageChannel?.CloseAttachment(_damageTarget, "RecipientDespawn");
            _damageTarget = null; _damageChannel = null;
            CloseBloomAttachment("RecipientDespawn");
            _bloomEffectState.OnValueChanged -= OnBloomEffectChanged;
            CloseAttackPhase("despawn", true);
            _attackPhaseState.OnValueChanged -= OnAttackPhaseChanged;
            CloseHealCast("despawn");
            _healEndSubscription?.Dispose();
            _healEndSubscription = null;
            _healCastState.OnValueChanged -= OnHealCastChanged;
            // 공격한 적 없는 유닛은 표현 수명을 만들지 않았으므로 retire할 대상도 없다.
            // Coordinator도 None을 no-op으로 방어하지만 호출 경계에서 의미를 명시한다.
            if (_attackPresentationInstanceId.IsValid)
                UnitAttackResultPresentationShadowBridge.Retire(_attackPresentationInstanceId);
            _attackPresentationInstanceId = AttackerInstanceId.None;
            _attackShadowPublicationClassifier?.Retire();
            _attackShadowPublicationClassifier = null;
            _attackShadowImpactClassifier?.Retire();
            _attackShadowImpactClassifier = null;

            // 안전망: Despawn 시 혹시 남아있는 콜백 해제.
            // 이미 해제된 상태여도 -= 연산은 에러 없이 무시됨.
            _unitId.OnValueChanged -= OnUnitIdReceived;

            // [Phase 2] 애니메이션 상태 콜백도 해제(클라이언트에서만 구독했으므로 서버에선 no-op).
            // 남겨두면 파괴된 오브젝트에 접근하는 버그로 이어질 수 있다.
            _animState.OnValueChanged -= OnAnimStateChanged;
            _movementSemanticRevision.OnValueChanged -=
                OnMovementSemanticRevisionChanged;
            _attackShadowState.OnValueChanged -= OnAttackShadowStateChanged;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            UnitMovementAuthorityObserver.RetireUnitLifecycle(
                _unitId.Value,
                NetworkObjectId);
            UnitAttackShadowObserver.RetireClient(_unitId.Value);
#endif

            // 클라이언트 전용: 사망 이펙트를 여기서 재생한다.
            // OnNetworkDespawn은 GO가 파괴되기 직전에 반드시 호출되므로
            // EntityDiedClientRpc보다 NGO Despawn이 먼저 도착하는 경쟁 조건에서도 이펙트가 보장된다.
            // 서버는 UnitView의 OnUnitDied 구독자에서 이미 재생하므로 클라이언트만 처리한다.
            if (!IsServer)
            {
                var unitView = GetComponent<UnitView>();

                // [2026-10-26 수정안 (a)] 투사체 사망타로 사망 표현이 화면 착탄까지 미뤄진 유닛은
                //   사망 VFX·SFX를 여기서 내지 않는다 — UnitView가 떼어 둔 사망 대역이 착탄 순간(상한 0.3초)에 1회 낸다.
                //   이 오브젝트의 파괴 자체(NGO)는 예전과 같다. 미뤄지지 않은 유닛은 아래 기존 줄이 그대로 동작한다.
                if (unitView != null && unitView.UnitData != null && !unitView.OwnsDeferredDeathPresentation)
                {
                    // 디스폰이 사망 통보보다 먼저 온 경우: 사망 VFX를 여기서 냈으니 늦게 온 사망 통보에서는 미루지 않게 표시한다.
                    unitView.MarkDeathPresentedAtDespawn();
                    VisualRootProjector projector = GetComponent<VisualRootProjector>();
                    Vector3 deathPosition = projector != null
                        ? projector.PresentationPosition
                        : transform.position;
                    EffectManager.Instance?.PlayUnitDeath(
                        unitView.UnitData.Type,
                        deathPosition);                                     // VFX
                    AudioManager.Instance?.PlayUnitDeathSfx(unitView.UnitData.Type); // SFX (규칙 15 — VFX와 짝)
                }
            }

            base.OnNetworkDespawn();
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 클라이언트에서 NGO가 자동 생성한 이 GameObject를
        /// UnitFactory._unitObjects 딕셔너리에 등록.
        /// 이후 SpawnUnitClientRpc에서 UnitData를 생성하고 UnitView를 초기화할 때,
        /// UnitFactory.GetUnitObject(unitId)로 이 GameObject를 찾을 수 있게 됨.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id</param>
        private void RegisterToFactory(int unitId)
        {
            // IGameServices를 통해 IUnitFactory 접근.
            // OnNetworkSpawn에서 미리 캐시해 둔 참조를 사용 (매번 씬 탐색하지 않음).
            var services = _services;
            if (services == null)
            {
                // [운영] 축 A=Error / 축 B=운영.
                //   축 A 가 Error 인 이유(복구되었나? → 아니오):
                //     RegisterToFactory 는 스폰 직후 1회(218행) 또는 unitId 도착 시 1회(240행)만 호출되고
                //     재시도 경로가 없다. 여기서 return 하면 이 유닛은 이 클라이언트의 UnitFactory 에
                //     끝내 등록되지 않아 SpawnUnitClientRpc 가 오브젝트를 찾지 못한다 — 영구 누락이다.
                //   축 B: 서버 상태가 이 클라이언트 화면에만 반영되지 않는 사건이고 통지 경로가 없다 → 운영.
                //   배치 1-A 선례 그대로 "ClientRpc 적용 중 조합 루트를 얻지 못한 1회성 이벤트" → 같은 키.
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkUnit),
                                  "클라이언트: IGameServices 가 없어 UnitFactory 등록을 할 수 없다",
                                  $"UnitId={unitId}");
                return;
            }

            IUnitFactory unitFactory = services.GetUnitFactory();
            if (unitFactory == null)
            {
                // [운영] 위와 같은 사건(조합 루트에서 필요한 것을 얻지 못해 등록이 죽음)이므로 같은 키를 쓴다.
                //   키를 나누면 "클라이언트가 서버 상태를 반영하지 못한 횟수" 지표가 쪼개진다(LogRules 1.5).
                //   IGameServices 가 없었는지 UnitFactory 가 없었는지는 메시지로 구분한다.
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkUnit),
                                  "클라이언트: UnitFactory 가 null 이라 UnitFactory 등록을 할 수 없다",
                                  $"UnitId={unitId}");
                return;
            }

            // 이 GameObject를 UnitFactory 딕셔너리에 등록
            // → SpawnUnitClientRpc에서 GetUnitObject(unitId)로 조회 가능
            unitFactory.RegisterUnitObject(unitId, gameObject);

            // [개발] 성공 통보 → Info / 개발. 실패하면 위 두 줄이 원인과 함께 운영으로 남는다.
            GameLog.Dev.Info("Network", nameof(NetworkUnit), "클라이언트: UnitFactory 등록 완료",
                             $"UnitId={unitId}");
        }

        /// <summary>
        /// SpawnUnitClientRpc에서 UnitView 초기화 완료 후 호출.
        /// 중복 초기화 방지 플래그 설정.
        /// </summary>
        public void MarkInitialized()
        {
            IsInitialized = true;
        }

    }
}
