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
//   · BeginProjectileEpochClientRpc(epoch, snapshotLaunches)    : 이 경기 UseCase 수명 번호. 다른 epoch의 기록은 버린다.
//       snapshotLaunches = -1이면 일반 알림(전체 방송), 0 이상이면 "늦은 접속 스냅샷 시작 — 바로 뒤에 발사 기록 N건"이다
//       (2026-10-20 단계 B: 진단이 스냅샷 수신 수를 셀 수 있도록 추가 — 스키마 4 → 5).
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
using UniRx;
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

        // ── [2026-10-20 단계 B 진단 보강] 전송 계수(읽기 전용 관측 — 전송·배달 판단에는 쓰지 않는다) ──
        //   경기 종료(또는 디스폰·UseCase 교체) 때 [PROJECTILE-END] side=transport 1줄로 남긴다(Host·Client 각각).
        private sealed class ProjectileTransportCounters
        {
            // 서버(Host)
            public int EpochBroadcasts, LaunchesSent, LaunchesSkippedStopped, ImpactsSent;
            public int SnapshotRequestsReceived, SnapshotRequestsRefused, SnapshotSends, SnapshotLaunchesSent;
            public int StopDeferredTicks, StopDeferredEpisodes;
            // Client
            public int EpochMessages, EpochSwitches, LaunchRpcs, ImpactRpcs, DroppedInvalid, DroppedEpoch;
            public int SnapshotRequested, SnapshotEpochsReceived, SnapshotLaunchesExpected, SnapshotLaunchesReceived;
            public int LaunchesDelivered, ImpactsDelivered;
            public bool EndWritten;
        }

        private ProjectileTransportCounters _projectileTransportDiag = new ProjectileTransportCounters();
        // Client — 직전에 받은 스냅샷 알림 뒤에 아직 도착하지 않은 스냅샷 발사 기록 수(진단 전용).
        private int _projectileSnapshotLaunchesPending;
        // 서버 — 정지 통보를 미루는 중인 유닛(사건 수를 "틱 수"가 아니라 "유닛별 1회"로 세기 위한 집합, 진단 전용).
        private readonly HashSet<int> _projectileStopDeferredUnits = new HashSet<int>();
        private System.IDisposable _projectileTransportGameEnd;

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
                //   [단계 B 진단] 앞 UseCase 몫의 전송 END를 아직 안 남겼으면 남기고, 계수를 새로 시작한다.
                if (_projectileTransportCombat != null) WriteProjectileTransportEnd("service-replaced");
                DisposeProjectileSubscriptions();
                _projectileTransportCombat = combat;
                ResetProjectileTransportDiagnostics();
                if (IsServer)
                {
                    BeginProjectileEpochClientRpc(combat.ProjectileEpoch, -1);
                    DiagProjectileTransport(d => d.EpochBroadcasts++);
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
                    combat.CopyActiveProjectileLaunches(_projectileSnapshotBuffer);
                    // [단계 B] 스냅샷 알림에 "바로 뒤에 올 발사 기록 수"를 싣는다(Client 진단이 스냅샷 수신 수를 센다).
                    //   옛 줄(기록용): BeginProjectileEpochClientRpc(combat.ProjectileEpoch, rpc);  — 목록 복사보다 먼저 보냈다.
                    BeginProjectileEpochClientRpc(combat.ProjectileEpoch, _projectileSnapshotBuffer.Count, rpc);
                    foreach (var launch in _projectileSnapshotBuffer)
                        SendProjectileLaunchRpc(launch, rpc);
                    WriteProjectileSnapshotSent(client, _projectileSnapshotBuffer.Count);
                }
                _projectileSnapshotClients.Clear();
                return;
            }
            if (!_projectileSnapshotRequested)
            {
                _projectileSnapshotRequested = true;
                RequestProjectileSnapshotServerRpc();
                DiagProjectileTransport(d => d.SnapshotRequested++);
            }
            if (_remoteProjectileEpoch == 0) return;
            combat.SetRemoteProjectileEpoch(_remoteProjectileEpoch);
            while (_pendingProjectileLaunches.Count > 0)
            {
                combat.ReceiveProjectileLaunch(_pendingProjectileLaunches.Dequeue());
                DiagProjectileTransport(d => d.LaunchesDelivered++);
            }
            while (_pendingProjectileImpacts.Count > 0)
            {
                combat.ReceiveProjectileImpact(_pendingProjectileImpacts.Dequeue());
                DiagProjectileTransport(d => d.ImpactsDelivered++);
            }
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
            // [단계 B 진단] 경기 종료 END를 아직 안 남겼으면(종료 없이 연결이 끊긴 경우 등) 디스폰 사유로 남긴다.
            if (_projectileTransportCombat != null) WriteProjectileTransportEnd("network-despawn");
            _projectileTransportGameEnd?.Dispose();
            _projectileTransportGameEnd = null;
            _projectileStopDeferredUnits.Clear();
            _projectileSnapshotLaunchesPending = 0;
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
            if (!IsSpawned || !IsServer || _combatStopped || !launch.IsValid)
            {
                if (launch.IsValid && _combatStopped) DiagProjectileTransport(d => d.LaunchesSkippedStopped++);
                return;
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // StreamSpirit 시간축 진단의 "timer-impact" 단계는 예전엔 서버 타격 피해 시각이었다.
            //   서버 투사체로 옮긴 뒤에는 그 타이머가 "발사" 시각이므로 발사 확정 순간에 같은 기록을 남긴다(진단 전용, HP 무관).
            // [2026-10-20 단계 B — 진단 오표기 수정 C] 이 순간에는 피해 writer가 실행되지 않는다(피해는 착탄 때).
            //   예전에는 writerResult=Applied로 찍어 "발사 때 피해가 적용됐다"로 읽힐 수 있었다. 이제는
            //   writerResult=NotInvoked(server-projectile-launch; damage-at-impact)로 실제 의미를 적는다.
            //   타이머 매칭·계수(timer-impact 단계 수)는 예전과 같다 — 시간축 진단의 완료 판정은 바꾸지 않는다.
            //   옛 호출(기록용): RecordStreamSpiritTimerImpact(launch.AttackerId, UnitType.StreamSpirit,
            //                   launch.TargetId, launch.TargetIsUnit, AttackDamageApplyStatus.Applied);
            if (launch.AttackerIsUnit && launch.AttackerKind == (int)UnitType.StreamSpirit)
                UnitAttackShadowObserver.RecordStreamSpiritTimerImpact(
                    launch.AttackerId, UnitType.StreamSpirit, launch.TargetId, launch.TargetIsUnit,
                    AttackDamageApplyStatus.Applied,
                    writerResultOverride: "NotInvoked(server-projectile-launch; damage-at-impact)");
#endif
            SendProjectileLaunchRpc(launch, default);
            DiagProjectileTransport(d => d.LaunchesSent++);
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
            DiagProjectileTransport(d => d.ImpactsSent++);
        }

        // ── RPC ─────────────────────────────────────────────────────────────

        // [2026-10-20 단계 B] snapshotLaunches 인자 추가(-1 = 일반 알림, 0 이상 = 늦은 접속 스냅샷 발사 기록 수).
        //   옛 시그니처(기록용): private void BeginProjectileEpochClientRpc(ulong epoch, ClientRpcParams rpc = default)
        [ClientRpc]
        private void BeginProjectileEpochClientRpc(ulong epoch, int snapshotLaunches, ClientRpcParams rpc = default)
        {
            if (IsServer || epoch == 0) return;
            DiagProjectileTransport(d => d.EpochMessages++);
            if (_remoteProjectileEpoch != 0 && _remoteProjectileEpoch != epoch)
            {
                // 같은 컨트롤러 수명에 다른 epoch가 오면(맵 재로드 등) 새 epoch로 넘어가고 옛 대기열은 버린다.
                _pendingProjectileLaunches.Clear();
                _pendingProjectileImpacts.Clear();
                DiagProjectileTransport(d => d.EpochSwitches++);
            }
            _remoteProjectileEpoch = epoch;
            if (snapshotLaunches >= 0)
            {
                // [진단] 스냅샷 알림 — 바로 뒤에 오는 발사 기록 N건을 "스냅샷 수신"으로 센다(같은 연결의 RPC는 보낸 순서대로 온다).
                _projectileSnapshotLaunchesPending = snapshotLaunches;
                DiagProjectileTransport(d => { d.SnapshotEpochsReceived++; d.SnapshotLaunchesExpected += snapshotLaunches; });
            }
            UpdateProjectileTransport();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestProjectileSnapshotServerRpc(ServerRpcParams rpc = default)
        {
            if (!IsSpawned || !IsServer) return;
            DiagProjectileTransport(d => d.SnapshotRequestsReceived++);
            if (_projectileSnapshotClients.Count >= 16) { DiagProjectileTransport(d => d.SnapshotRequestsRefused++); return; }
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
            DiagProjectileTransport(d => d.LaunchRpcs++);
            if (_projectileSnapshotLaunchesPending > 0)
            {
                // [진단] 스냅샷 알림 바로 뒤의 발사 기록 = 늦은 접속 스냅샷으로 받은 기록.
                _projectileSnapshotLaunchesPending--;
                DiagProjectileTransport(d => d.SnapshotLaunchesReceived++);
            }
            // epoch가 아직 없으면(첫 RPC가 epoch보다 먼저 온 드문 순서) 대기열에 두었다가 epoch 확인 후 배달한다.
            if (!launch.IsValid) { DiagProjectileTransport(d => d.DroppedInvalid++); return; }
            if (_remoteProjectileEpoch != 0 && epoch != _remoteProjectileEpoch) { DiagProjectileTransport(d => d.DroppedEpoch++); return; }
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
            DiagProjectileTransport(d => d.ImpactRpcs++);
            if (!impact.IsValid) { DiagProjectileTransport(d => d.DroppedInvalid++); return; }
            if (_remoteProjectileEpoch != 0 && epoch != _remoteProjectileEpoch) { DiagProjectileTransport(d => d.DroppedEpoch++); return; }
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

        // ====================================================================
        // [2026-10-20 단계 B 진단 보강] 전송 계수 · END 1줄 (읽기 전용 관측 — 릴리스 빌드에서는 호출이 사라진다)
        //
        // [END 줄에서 읽는 법 — 다음 실기 분석용, 태그 [PROJECTILE-END] side=transport]
        //   Host  : sent=launch:N|impact:N  snapshot=requests:N|refused:N|sends:N|launches:N  stopDeferred=episodes:N|ticks:N
        //   Client: recv=epoch:N|launch:N|impact:N  delivered=launch:N|impact:N  dropped=invalid:N|epoch:N|capacity:N
        //           snapshot=requested:N|epochs:N|expected:N|received:N
        //   늦은 접속 확인: Host snapshot.launches == Client snapshot.received 이면 스냅샷이 다 도착했다.
        //   (복원 = 화면이 비행 중간부터 그린 수는 side=view 줄의 begins=midFlight:N 이다.)
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileTransport(System.Action<ProjectileTransportCounters> update)
        {
            if (_projectileTransportDiag != null) update(_projectileTransportDiag);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ResetProjectileTransportDiagnostics()
        {
            _projectileTransportDiag = new ProjectileTransportCounters();
            _projectileStopDeferredUnits.Clear();
            _projectileTransportGameEnd?.Dispose();
            // 경기 종료 순간에 END를 남긴다(Host·Client 모두 OnGameEnd가 발행된다 — Client는 NetworkGameEndController가 발행).
            _projectileTransportGameEnd = GameEvents.OnGameEnd.Subscribe(_ => { WriteProjectileTransportEnd("game-end"); });
        }

        /// <summary>[진단] 서버가 정지 통보를 미룬 틱(발사 전 예약이 남은 동안). 유닛별 첫 틱만 "사건"으로 센다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileStopDeferred(int unitId)
        {
            if (_projectileTransportDiag == null) return;
            _projectileTransportDiag.StopDeferredTicks++;
            if (_projectileStopDeferredUnits.Count < 4096 && _projectileStopDeferredUnits.Add(unitId))
                _projectileTransportDiag.StopDeferredEpisodes++;
        }

        /// <summary>[진단] 정지 통보가 실제로 나가면 그 유닛의 "미룸 사건"을 닫는다(다음 미룸은 새 사건).</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileStopReleased(int unitId) => _projectileStopDeferredUnits.Remove(unitId);

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WriteProjectileSnapshotSent(ulong client, int launches)
        {
            ProjectileTransportCounters d = _projectileTransportDiag;
            if (d == null) return;
            d.SnapshotSends++;
            d.SnapshotLaunchesSent += launches;
            // 늦은 접속은 경기당 몇 번 안 되므로 사건마다 1줄(상한 16줄).
            if (d.SnapshotSends <= 16)
                GameLog.Dev.Info("Combat", nameof(NetworkCombatController), "[PROJECTILE-SNAPSHOT] sent",
                    $"epoch={(_projectileTransportCombat != null ? _projectileTransportCombat.ProjectileEpoch : 0UL):x16}, client={client}, inFlightLaunches={launches}, send={d.SnapshotSends}/16");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WriteProjectileTransportEnd(string reason)
        {
            ProjectileTransportCounters d = _projectileTransportDiag;
            if (d == null || d.EndWritten) return;
            d.EndWritten = true;
            bool server = IsServer;
            string body;
            if (server)
            {
                body = $"side=transport v=1 role=Host epoch={(_projectileTransportCombat != null ? _projectileTransportCombat.ProjectileEpoch : 0UL):x16} reason={reason} "
                    + $"epochBroadcasts={d.EpochBroadcasts} sent=launch:{d.LaunchesSent}|impact:{d.ImpactsSent}|launchSkippedStopped:{d.LaunchesSkippedStopped} "
                    + $"snapshot=requests:{d.SnapshotRequestsReceived}|refused:{d.SnapshotRequestsRefused}|sends:{d.SnapshotSends}|launches:{d.SnapshotLaunchesSent} "
                    + $"stopDeferred=episodes:{d.StopDeferredEpisodes}|ticks:{d.StopDeferredTicks} "
                    + $"verdict={(d.SnapshotRequestsRefused == 0 ? "PASS" : "ATTENTION")}";
            }
            else
            {
                bool snapshotComplete = d.SnapshotLaunchesReceived == d.SnapshotLaunchesExpected;
                bool clean = d.DroppedInvalid == 0 && d.DroppedEpoch == 0 && _projectileTransportFailures == 0;
                body = $"side=transport v=1 role=Client epoch={_remoteProjectileEpoch:x16} reason={reason} "
                    + $"recv=epoch:{d.EpochMessages}|epochSwitch:{d.EpochSwitches}|launch:{d.LaunchRpcs}|impact:{d.ImpactRpcs} "
                    + $"delivered=launch:{d.LaunchesDelivered}|impact:{d.ImpactsDelivered}|queuedNow:{_pendingProjectileLaunches.Count + _pendingProjectileImpacts.Count} "
                    + $"dropped=invalid:{d.DroppedInvalid}|epoch:{d.DroppedEpoch}|capacity:{_projectileTransportFailures} "
                    + $"snapshot=requested:{d.SnapshotRequested}|epochs:{d.SnapshotEpochsReceived}|expected:{d.SnapshotLaunchesExpected}|received:{d.SnapshotLaunchesReceived} "
                    + $"verdict={(snapshotComplete && clean ? "PASS" : "ATTENTION")}";
            }
            GameLog.Dev.Info("Combat", nameof(NetworkCombatController), "[PROJECTILE-END]", body);
        }
    }
}
