// ============================================================================
// NetworkCombatController.Projectiles.cs
// 서버 권위 투사체(옵션 C)의 네트워크 전송 — 발사 기록 · 착탄 결과 · 늦은 접속 복원.
//
// [역할 — 유니티 초급자용]
//   서버(Host)의 UnitCombatUseCase가 "발사했다 / 착탄했다"를 확정하면 이벤트가 발행된다.
//   이 파일은 그 이벤트를 받아 Client에게 ClientRpc로 그대로 전달만 한다. 판정은 하지 않는다.
//   Client는 받은 기록을 자기 UnitCombatUseCase.ReceiveProjectile*로 넘기고, 화면 소비자가 그 기록대로 그린다.
//
// [전송 계약 — 바꾸면 PresentationContractSchemaRevision을 올린다]
//   · BeginProjectileEpochClientRpc(epoch)                      : 이 경기 UseCase 수명 번호. 다른 epoch의 기록은 버린다.
//   · ProjectileLaunchClientRpc(epoch, id, 공격자, 대상, 위치, 반경, 발사/착탄 시각) : 발사 기록 1건.
//   · ProjectileImpactClientRpc(epoch, id, 결과, 착탄 지점, 대상, HP, 피해량, 시각)   : 착탄 결과 1건.
//   · RequestProjectileSnapshotServerRpc → 서버가 그 Client에게만 epoch + "지금 비행 중"인 발사 기록을 다시 보낸다(Q14 늦은 접속).
//   중복(같은 epoch:id)은 화면 소비자(ProjectilePresentationCoordinator)가 거부한다.
//   HP 자체는 기존 NetworkHealthSync가 맞춘다(여기서 HP를 쓰지 않는다 — 단일 HP writer 유지).
//
// [파도 전송(NetworkCombatController.Waves.cs)과 같은 구조를 쓴 이유]
//   이미 실기에서 검증된 "epoch → 정상 전송 → 늦은 접속 스냅샷 → Client 대기열 배달" 틀이라,
//   같은 틀을 따르면 순서·수명 문제를 새로 만들지 않는다.
//
// Infrastructure 레이어 — NetworkBehaviour(NGO) 사용 허용.
// ============================================================================

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    public partial class NetworkCombatController
    {
        // Client 수신 대기열 상한. 넘치면 표현만 버린다(HP는 별도 RPC라 영향 없음).
        private const int ProjectileTransportCapacity = 4096;

        private UnitCombatUseCase _projectileTransportCombat;
        private ulong _remoteProjectileEpoch;
        private bool _projectileSnapshotRequested;
        private int _projectileTransportFailures;
        private readonly HashSet<ulong> _projectileSnapshotClients = new HashSet<ulong>();
        private readonly List<ProjectileLaunchPresentation> _projectileSnapshotBuffer = new List<ProjectileLaunchPresentation>();
        private readonly Queue<ProjectileLaunchPresentation> _pendingProjectileLaunches = new Queue<ProjectileLaunchPresentation>();
        private readonly Queue<ProjectileImpactPresentation> _pendingProjectileImpacts = new Queue<ProjectileImpactPresentation>();

        /// <summary>
        /// 매 프레임(Update) 호출 — 서버는 이벤트 구독·스냅샷 요청 처리, Client는 epoch 확인 후 대기열 배달을 한다.
        /// </summary>
        private void UpdateProjectileTransport()
        {
            if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return;
            var combat = (_services ?? GameServicesLocator.Current)?.GetCombatUseCase();
            if (combat == null) return;
            if (_projectileTransportCombat != combat)
            {
                // 맵 재로드 등으로 UseCase가 바뀌면 옛 구독을 끊고 새 UseCase에 다시 붙는다.
                DisposeProjectileSubscriptions();
                _projectileTransportCombat = combat;
                if (IsServer)
                {
                    BeginProjectileEpochClientRpc(combat.ProjectileEpoch);
                    combat.ProjectileLaunched += SendProjectileLaunch;
                    combat.ProjectileResolved += SendProjectileImpact;
                }
            }
            if (IsServer)
            {
                // 늦은 접속(재연결 포함) Client에게 epoch와 "지금 비행 중"인 투사체만 다시 보낸다.
                foreach (ulong client in _projectileSnapshotClients)
                {
                    var rpc = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { client } } };
                    BeginProjectileEpochClientRpc(combat.ProjectileEpoch, rpc);
                    combat.CopyActiveProjectileLaunches(_projectileSnapshotBuffer);
                    foreach (var launch in _projectileSnapshotBuffer)
                        SendProjectileLaunchRpc(launch, rpc);
                }
                _projectileSnapshotClients.Clear();
                return;
            }
            if (!_projectileSnapshotRequested)
            {
                _projectileSnapshotRequested = true;
                RequestProjectileSnapshotServerRpc();
            }
            if (_remoteProjectileEpoch == 0) return;
            combat.SetRemoteProjectileEpoch(_remoteProjectileEpoch);
            while (_pendingProjectileLaunches.Count > 0)
                combat.ReceiveProjectileLaunch(_pendingProjectileLaunches.Dequeue());
            while (_pendingProjectileImpacts.Count > 0)
                combat.ReceiveProjectileImpact(_pendingProjectileImpacts.Dequeue());
        }

        private void DisposeProjectileSubscriptions()
        {
            if (_projectileTransportCombat == null) return;
            _projectileTransportCombat.ProjectileLaunched -= SendProjectileLaunch;
            _projectileTransportCombat.ProjectileResolved -= SendProjectileImpact;
            _projectileTransportCombat = null;
        }

        /// <summary>네트워크 디스폰 때 구독·대기열·epoch를 모두 비운다(다음 경기에 남지 않게).</summary>
        private void DisposeProjectileTransport()
        {
            DisposeProjectileSubscriptions();
            _remoteProjectileEpoch = 0;
            _projectileSnapshotRequested = false;
            _projectileTransportFailures = 0;
            _projectileSnapshotClients.Clear();
            _projectileSnapshotBuffer.Clear();
            _pendingProjectileLaunches.Clear();
            _pendingProjectileImpacts.Clear();
        }

        // ── 서버 → Client 송신 ─────────────────────────────────────────────

        private void SendProjectileLaunch(ProjectileLaunchPresentation launch)
        {
            if (!IsSpawned || !IsServer || _combatStopped || !launch.IsValid) return;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // StreamSpirit 시간축 진단의 "timer-impact" 단계는 예전엔 서버 타격 피해 시각이었다.
            //   서버 투사체로 옮긴 뒤에는 그 타이머가 "발사" 시각이므로 발사 확정 순간에 같은 기록을 남긴다(진단 전용, HP 무관).
            if (launch.AttackerIsUnit && launch.AttackerKind == (int)UnitType.StreamSpirit)
                UnitAttackShadowObserver.RecordStreamSpiritTimerImpact(
                    launch.AttackerId, UnitType.StreamSpirit, launch.TargetId, launch.TargetIsUnit,
                    AttackDamageApplyStatus.Applied);
#endif
            SendProjectileLaunchRpc(launch, default);
        }

        private void SendProjectileLaunchRpc(ProjectileLaunchPresentation launch, ClientRpcParams rpc)
        {
            ProjectileLaunchClientRpc(launch.Key.Epoch, launch.Key.Id, launch.AttackerId, launch.AttackerIsUnit,
                launch.AttackerKind, launch.AttackerRace, (int)launch.Team, launch.TargetId, launch.TargetIsUnit,
                launch.Source, launch.TargetCenter, launch.TargetRadius, launch.LaunchTime, launch.ImpactTime,
                launch.Homing, rpc);
        }

        private void SendProjectileImpact(ProjectileImpactPresentation impact)
        {
            // 확정된 착탄은 종료 플래그로 버리지 않는다(파도 접촉과 같은 원칙 — HP는 이미 확정됐다).
            if (!IsSpawned || !IsServer || !impact.IsValid) return;
            ProjectileImpactClientRpc(impact.Key.Epoch, impact.Key.Id, (byte)impact.Outcome, impact.ImpactPoint,
                impact.VictimId, impact.VictimIsUnit, impact.ResultingHp, impact.AppliedAmount, impact.ImpactTime);
        }

        // ── RPC ─────────────────────────────────────────────────────────────

        [ClientRpc]
        private void BeginProjectileEpochClientRpc(ulong epoch, ClientRpcParams rpc = default)
        {
            if (IsServer || epoch == 0) return;
            if (_remoteProjectileEpoch != 0 && _remoteProjectileEpoch != epoch)
            {
                // 같은 컨트롤러 수명에 다른 epoch가 오면(맵 재로드 등) 새 epoch로 넘어가고 옛 대기열은 버린다.
                _pendingProjectileLaunches.Clear();
                _pendingProjectileImpacts.Clear();
            }
            _remoteProjectileEpoch = epoch;
            UpdateProjectileTransport();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestProjectileSnapshotServerRpc(ServerRpcParams rpc = default)
        {
            if (!IsSpawned || !IsServer || _projectileSnapshotClients.Count >= 16) return;
            _projectileSnapshotClients.Add(rpc.Receive.SenderClientId);
        }

        [ClientRpc]
        private void ProjectileLaunchClientRpc(ulong epoch, ulong id, int attacker, bool attackerIsUnit,
            int attackerKind, int attackerRace, int team, int target, bool targetIsUnit,
            Vector3 source, Vector3 targetCenter, float targetRadius, double launchTime, double impactTime,
            bool homing, ClientRpcParams rpc = default)
        {
            if (IsServer) return;
            var launch = new ProjectileLaunchPresentation(new ProjectileDeliveryKey(epoch, id), attacker,
                attackerIsUnit, attackerKind, attackerRace, (TeamId)team, target, targetIsUnit,
                source, targetCenter, targetRadius, launchTime, impactTime, homing);
            // epoch가 아직 없으면(첫 RPC가 epoch보다 먼저 온 드문 순서) 대기열에 두었다가 epoch 확인 후 배달한다.
            if (!launch.IsValid || (_remoteProjectileEpoch != 0 && epoch != _remoteProjectileEpoch)) return;
            if (_pendingProjectileLaunches.Count + _pendingProjectileImpacts.Count >= ProjectileTransportCapacity)
            { ProjectileTransportCapacityFailure(); return; }
            _pendingProjectileLaunches.Enqueue(launch);
            UpdateProjectileTransport();
        }

        [ClientRpc]
        private void ProjectileImpactClientRpc(ulong epoch, ulong id, byte outcome, Vector3 point,
            int victim, bool victimIsUnit, int hp, int applied, double time)
        {
            if (IsServer) return;
            var impact = new ProjectileImpactPresentation(new ProjectileDeliveryKey(epoch, id),
                (ProjectileImpactOutcome)outcome, point, victim, victimIsUnit, hp, applied, time);
            if (!impact.IsValid || (_remoteProjectileEpoch != 0 && epoch != _remoteProjectileEpoch)) return;
            if (_pendingProjectileLaunches.Count + _pendingProjectileImpacts.Count >= ProjectileTransportCapacity)
            { ProjectileTransportCapacityFailure(); return; }
            _pendingProjectileImpacts.Enqueue(impact);
            UpdateProjectileTransport();
        }

        private void ProjectileTransportCapacityFailure()
        {
            _projectileTransportFailures++;
            if (_projectileTransportFailures == 1 || _projectileTransportFailures % 256 == 0)
                GameLog.Dev.Warn("Combat", nameof(NetworkCombatController),
                    "Projectile transport capacity exceeded; presentation rejected (HP unchanged)",
                    $"capacity={ProjectileTransportCapacity}, failures={_projectileTransportFailures}");
        }
    }
}
