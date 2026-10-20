// ============================================================================
// UnitCombatUseCase.Projectiles.cs
// 서버 권위 투사체(옵션 C) — 발사 예약 · 발사 · 비행 · 착탄 피해 · 닫기.
//
// [한 줄 요약 — 유니티 초급자용]
//   예전: 공격 애니메이션의 타격 순간(OnAttackHit 시각)에 서버가 "바로" 피해를 줬다. 화면의 투사체는 장식이었다.
//   지금: 그 타격 순간은 "발사" 시각이다. 서버가 발사 기록을 만들고, 거리 ÷ 속도만큼 지난 "착탄" 시각에
//         기존 피해 writer(ApplyAttackDamageCore → ExecuteAttack)를 한 번 불러 피해를 준다.
//         그래서 피해량·방어 감쇄·건물 2배·DoT·범위 효과는 예전과 똑같이 계산된다(단일 HP writer 유지).
//
// [흐름]
//   1) 공격 커밋(싱글 TryAttack / 멀티 NetworkCombatController.ExecuteAttack)
//        → TryReserveProjectileLaunch: "발사 예약"(due = 커밋 시각 + OnAttackHit offset)을 만든다.
//   2) 매 프레임 TickProjectiles(now)
//        · 예약 중(발사 전): 공격자 사망·교체 / 대상 사망·교체 → 발사 취소(투사체·소리·피해 없음, Q6·Q15).
//          대상이 사거리 밖으로 나간 것은 취소 사유가 아니다(Q5 — 원래 대상에게 그대로 발사).
//        · due가 되면 발사: 착탄 시각 = 발사 + (중심 거리 − 대상 반경) ÷ 속도 를 이때 한 번 계산해 고정(U-1).
//          공격력도 이때 저장한다(발사 뒤 공격자가 죽어도 맞으면 피해 — Q3 Persist).
//        · 비행 중: 원래 대상 "객체"를 따라간다(Homing). 대상이 사라지면 마지막 위치를 고정한다.
//        · 착탄 시각: 대상이 살아 있으면 피해 1회 + 결과(Hit), 사라졌으면 결과(TargetLost)만 — 피해 없음(Q4).
//   3) 게임 종료·맵 정리·디스폰·서비스 교체 → CloseProjectiles: 남은 예약/비행을 모두 피해 없이 닫는다(위험 7).
//
// [왜 50ms 전투 틱이 아니라 매 프레임인가]
//   멀티 전투 틱(_attackInterval 0.05초)에 묶으면 착탄이 최대 50ms 늦어진다(위험 4).
//   그래서 TorrentSpirit 발사 예약(TickTorrentLaunches)과 같은 매 프레임 진행에 붙인다.
//
// [ID 재사용 안전 — 위험 6]
//   대상·공격자를 ID가 아니라 "객체 참조"로 보관하고, 매번 같은 객체인지(ReferenceEquals) 확인한다.
//   같은 ID로 새 유닛이 태어나도 그 유닛은 맞지 않는다.
//
// Application 레이어 — Domain 의존, UnityEngine 수학 타입(Vector3/Mathf)만 사용. NGO 직접 참조 없음.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public partial class UnitCombatUseCase
    {
        // 동시에 존재할 수 있는 예약+비행 투사체 상한. Torrent 예약(1024)과 같은 값 — 넘치면 공격 커밋을 거절한다.
        private const int ProjectileCapacity = 1024;

        // 개발 로그 상한(한 UseCase 수명당). 매 공격마다 찍으면 로그가 넘치므로 앞부분만 남긴다(LogRules 1.14 — 매 틱 로깅 금지).
        private const int ProjectileDiagnosticLimit = 96;

        /// <summary>투사체 1발의 진행 단계.</summary>
        private enum ProjectileStage : byte
        {
            /// <summary>발사 예약(커밋 뒤, 발사 시각 전). 이 단계에서만 "발사 취소"가 가능하다.</summary>
            Reserved = 0,
            /// <summary>발사됨 — 비행 중. 취소되지 않는다(게임 종료 닫기 제외).</summary>
            InFlight = 1
        }

        /// <summary>
        /// 서버가 들고 있는 투사체 1발의 상태. 재사용 풀에서 꺼내 쓰므로(모바일 GC 절감) class다.
        /// </summary>
        private sealed class ProjectileRecord
        {
            public bool Active;
            public ProjectileStage Stage;
            public ulong Id;
            public UnitData Attacker;
            public UnitType AttackerType;
            public TeamId Team;
            public IDamageable Target;
            public int TargetId;
            public bool TargetIsUnit;
            public double Due;
            public int HitIndex;
            // ── 발사 때 채운다 ──
            public double LaunchTime;
            public double ImpactTime;
            public Vector3 Source;
            public Vector3 LastTargetCenter;
            public float TargetRadius;
            public int AttackPower;
            public bool TargetLost;
            public ProjectileLaunchPresentation Launch;

            public void Reset()
            {
                Active = false; Stage = ProjectileStage.Reserved; Id = 0; Attacker = null;
                AttackerType = default; Team = default; Target = null; TargetId = -1; TargetIsUnit = false;
                Due = 0d; HitIndex = 0; LaunchTime = 0d; ImpactTime = 0d; Source = Vector3.zero;
                LastTargetCenter = Vector3.zero; TargetRadius = 0f; AttackPower = 0; TargetLost = false;
                Launch = default;
            }
        }

        private readonly List<ProjectileRecord> _projectiles = new List<ProjectileRecord>(16);
        private readonly Stack<ProjectileRecord> _projectilePool = new Stack<ProjectileRecord>(16);
        private ProjectileRecord[] _projectileScratch = new ProjectileRecord[16];
        private ulong _nextProjectileId;
        private bool _projectilesClosed;
        // TickProjectiles 실행 중인가. 실행 중에는 목록을 압축(재사용 풀 반환)하지 않는다 —
        //   처리 중인 기록이 다른 코드(피해 이벤트 구독자)의 재진입 호출로 초기화되는 것을 막는다.
        private bool _projectileTicking;
        private int _projectileDiagnosticLines;
        private ProjectileSettings _projectileSettings = ProjectileSettings.CreateDefault();

        // 공격자별 "가장 최근 발사 예약의 결말" — 발사가 취소됐는지(true) 아닌지(false).
        //   화면의 OnAttackHit marker가 "취소된 공격의 총구 VFX·발사음"을 내지 않게 할 때만 읽는다(Q6).
        //   싱글·Host처럼 이 UseCase가 서버 권위인 곳에서만 값이 생긴다(순수 Client는 서버 lease가 대신 막는다).
        private readonly Dictionary<int, ProjectileCueState> _projectileCueStates = new Dictionary<int, ProjectileCueState>();
        private struct ProjectileCueState { public UnitData Attacker; public bool Cancelled; }

        // 착탄 피해를 적용하는 "그 순간"에만 채워지는 문맥(try/finally로 반드시 비운다).
        //   · 공격력은 발사 때 저장한 값을 쓴다(EffectiveAttack가 이 값을 우선 반환 — Q3, 위험 3).
        //   · 이 피해로 생기는 피격 이벤트에 투사체 번호표를 실어, 화면이 "착탄 시각"에 숫자를 1회 보이게 한다(Q7).
        private UnitData _projectileImpactAttacker;
        private int _projectileImpactAttackPower;
        private ProjectileDeliveryKey _projectileImpactKey;

        /// <summary>지금 투사체 착탄 피해를 적용하는 중인가(특수 공격 핸들러의 Persist 판정용).</summary>
        private bool ProjectileImpactInProgress => _projectileImpactAttacker != null;

        /// <summary>이 UseCase 수명의 투사체 번호 epoch. Client는 서버 값을 받아 바꾼다.</summary>
        public ulong ProjectileEpoch { get; private set; }
            = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0) | 1UL;

        /// <summary>
        /// 투사체 발사·착탄 시각이 쓰는 시계. 파도와 같은 조합 루트 시계(싱글 = Time.timeAsDouble, 멀티 = NGO ServerTime)를 쓴다.
        /// </summary>
        public double ProjectileServerTime => WaveServerTime;

        /// <summary>서버가 투사체를 "발사"했을 때(Client는 서버 기록을 받았을 때) 발행된다. 화면·전송 계층이 구독한다.</summary>
        public event Action<ProjectileLaunchPresentation> ProjectileLaunched;

        /// <summary>서버가 착탄 결과를 확정했을 때(Client는 결과를 받았을 때) 발행된다.</summary>
        public event Action<ProjectileImpactPresentation> ProjectileResolved;

        /// <summary>
        /// 조합 루트(GameBootstrapper)가 Inspector 설정(ProjectileConfig)에서 만든 속도·반경 값을 주입한다.
        /// null이면 코드 기본값을 유지한다.
        /// </summary>
        /// <param name="settings">서버 비행 시간 계산용 값 묶음.</param>
        public void SetProjectileSettings(ProjectileSettings settings)
        {
            if (settings != null) _projectileSettings = settings;
        }

        /// <summary>
        /// 공격 커밋 전에 "이번 공격의 발사 예약 hitCount개를 받을 자리가 있는가"를 확인한다.
        /// 자리가 없으면 호출자는 쿨다운·공격 커밋을 하지 않는다(Torrent 예약과 같은 원칙).
        /// </summary>
        /// <param name="hitCount">이번 공격의 타격(=발사) 수.</param>
        public bool CanReserveProjectileLaunches(int hitCount)
        {
            if (_projectilesClosed) return false;
            int needed = Math.Max(1, hitCount);
            return CountActiveProjectiles() + needed <= ProjectileCapacity && _nextProjectileId < ulong.MaxValue - (ulong)needed;
        }

        /// <summary>
        /// 발사 예약 1개를 만든다(공격 커밋 경계에서 호출). due는 서버 시계 기준 발사 시각이다.
        /// 공격자·대상이 이미 무효면 만들지 않는다.
        /// </summary>
        /// <param name="attacker">쏘는 유닛(객체 참조로 보관).</param>
        /// <param name="targetId">원래 대상 Id.</param>
        /// <param name="targetIsUnit">대상이 유닛인가.</param>
        /// <param name="due">발사 시각(서버 시계).</param>
        /// <param name="hitIndex">이번 공격 안의 타격 번호(진단용).</param>
        /// <returns>예약을 만들었으면 true.</returns>
        public bool TryReserveProjectileLaunch(UnitData attacker, int targetId, bool targetIsUnit,
            double due, int hitIndex)
        {
            if (_projectilesClosed || attacker == null || !attacker.IsAlive
                || !ServerProjectileDeliveryPolicy.IsServerProjectile(attacker.Type)
                || !IsCurrentProjectileAttacker(attacker)
                || double.IsNaN(due) || double.IsInfinity(due) || due < 0d)
                return false;
            IDamageable target = FindTargetById(targetId, targetIsUnit);
            if (target == null || !target.IsAlive || !CanReserveProjectileLaunches(1)) return false;

            ProjectileRecord record = _projectilePool.Count > 0 ? _projectilePool.Pop() : new ProjectileRecord();
            record.Reset();
            record.Active = true;
            record.Stage = ProjectileStage.Reserved;
            record.Id = ++_nextProjectileId;
            record.Attacker = attacker;
            record.AttackerType = attacker.Type;
            record.Team = attacker.Team;
            record.Target = target;
            record.TargetId = targetId;
            record.TargetIsUnit = targetIsUnit;
            record.Due = due;
            record.HitIndex = hitIndex;
            _projectiles.Add(record);
            _projectileCueStates[attacker.Id] = new ProjectileCueState { Attacker = attacker, Cancelled = false };
            ProjectileLog("reserved", record, due);
            return true;
        }

        /// <summary>
        /// 이 공격자의 "아직 발사하지 않은" 예약을 모두 취소한다(이동·정지 명령 등 명시적 끊김 — Q6).
        /// 이미 발사된 투사체는 건드리지 않는다(발사 후 공격자 사정은 피해를 막지 않는다 — Q3).
        /// </summary>
        /// <param name="attacker">끊긴 공격자.</param>
        /// <param name="reason">로그용 사유 토큰(예: "move-command").</param>
        public void CancelProjectileLaunches(UnitData attacker, string reason)
        {
            if (attacker == null || _projectiles.Count == 0) return;
            for (int i = 0; i < _projectiles.Count; i++)
            {
                ProjectileRecord record = _projectiles[i];
                if (record.Active && record.Stage == ProjectileStage.Reserved
                    && ReferenceEquals(record.Attacker, attacker))
                    CancelReservation(record, reason);
            }
            CompactProjectiles();
        }

        /// <summary>
        /// 이 공격자에게 아직 발사되지 않은(대상이 살아 있는) 예약이 있는가.
        /// 멀티 서버가 "커밋된 투사체 회차 중에는 사거리 이탈로 표현을 닫지 않는다"(위험 16)를 판단할 때 쓴다.
        /// </summary>
        /// <param name="attacker">확인할 공격자.</param>
        public bool HasPendingProjectileLaunch(UnitData attacker)
        {
            if (attacker == null) return false;
            for (int i = 0; i < _projectiles.Count; i++)
            {
                ProjectileRecord record = _projectiles[i];
                if (record.Active && record.Stage == ProjectileStage.Reserved
                    && ReferenceEquals(record.Attacker, attacker)
                    && record.Target != null && record.Target.IsAlive)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 이 공격자의 가장 최근 발사 예약이 "발사 전에 취소"됐는가.
        /// 화면 marker가 취소된 공격의 총구 VFX·발사음을 내지 않게 할 때 쓴다(Q6 — 투사체·소리·피해 없음).
        /// 새 예약이 아직 대기 중이면 false(그 공격은 아직 유효하다).
        /// </summary>
        /// <param name="attacker">확인할 공격자.</param>
        public bool WasLatestProjectileLaunchCancelled(UnitData attacker)
        {
            if (attacker == null || !_projectileCueStates.TryGetValue(attacker.Id, out ProjectileCueState state)
                || !ReferenceEquals(state.Attacker, attacker))
                return false;
            return state.Cancelled && !HasPendingProjectileLaunch(attacker);
        }

        /// <summary>
        /// 매 프레임 호출 — 예약 검사·발사·비행 추적·착탄을 진행한다(서버/싱글 전용).
        /// 싱글: GameBootstrapper.Update, 멀티 서버: NetworkCombatController.Update(매 서버 frame).
        /// </summary>
        /// <param name="now">서버 시계 현재 시각.</param>
        public void TickProjectiles(double now)
        {
            if (_projectilesClosed || _projectiles.Count == 0
                || double.IsNaN(now) || double.IsInfinity(now)) return;

            // 처리 중 이벤트(피해→사망→다른 코드)가 목록을 바꿀 수 있으므로 이번 프레임 대상을 먼저 복사한다.
            int count = _projectiles.Count;
            if (_projectileScratch.Length < count)
                _projectileScratch = new ProjectileRecord[Math.Max(count, _projectileScratch.Length * 2)];
            _projectiles.CopyTo(_projectileScratch);

            _projectileTicking = true;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    ProjectileRecord record = _projectileScratch[i];
                    _projectileScratch[i] = null;
                    if (record == null || !record.Active) continue;
                    if (record.Stage == ProjectileStage.Reserved) ProcessProjectileReservation(record, now);
                    // 발사 직후 거리가 0에 가까우면(착탄 시각 ≤ 지금) 같은 프레임에 바로 착탄까지 처리한다.
                    if (record.Active && record.Stage == ProjectileStage.InFlight) ProcessProjectileFlight(record, now);
                    // 피해 → 성 파괴 → 게임 종료가 같은 호출 안에서 일어나면 닫기가 이미 목록을 비웠다.
                    if (_projectilesClosed)
                    {
                        Array.Clear(_projectileScratch, 0, _projectileScratch.Length);
                        return;
                    }
                }
            }
            finally { _projectileTicking = false; }
            CompactProjectiles();
        }

        /// <summary>
        /// 게임 종료·맵 정리·디스폰·서비스 교체 때 남은 예약/비행을 모두 "피해 없이" 닫는다(위험 7).
        /// 한 번 닫으면 이 UseCase 수명에서는 다시 열리지 않는다(Torrent 닫기와 같은 수명 규칙).
        /// </summary>
        /// <param name="reason">로그용 사유 토큰.</param>
        public void CloseProjectiles(string reason = "lifecycle-close")
        {
            bool alreadyClosed = _projectilesClosed;
            _projectilesClosed = true;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            int open = CountActiveProjectiles();
            if (!alreadyClosed && open > 0)
                GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE] close",
                    $"epoch={ProjectileEpoch:x16}, reason={reason}, cancelled={open}, capacity={ProjectileCapacity}");
#endif
            for (int i = 0; i < _projectiles.Count; i++)
            {
                _projectiles[i].Reset();
                _projectilePool.Push(_projectiles[i]);
            }
            _projectiles.Clear();
            _projectileCueStates.Clear();
            if (_projectileScratch != null) Array.Clear(_projectileScratch, 0, _projectileScratch.Length);
        }

        /// <summary>
        /// 늦게 접속한 Client 복원용 — 지금 "비행 중"인 투사체의 발사 기록을 복사한다(Q14).
        /// 아직 발사하지 않은 예약과 이미 착탄한 것은 넣지 않는다(착탄 결과는 다시 보내지 않는다).
        /// </summary>
        /// <param name="output">결과를 담을 목록(먼저 비운다).</param>
        public void CopyActiveProjectileLaunches(List<ProjectileLaunchPresentation> output)
        {
            if (output == null) return;
            output.Clear();
            if (_projectilesClosed) return;
            for (int i = 0; i < _projectiles.Count; i++)
            {
                ProjectileRecord record = _projectiles[i];
                if (record.Active && record.Stage == ProjectileStage.InFlight && record.Launch.IsValid)
                    output.Add(record.Launch);
            }
        }

        /// <summary>
        /// Client 전용 — 서버 UseCase 수명 번호(epoch)를 받아 둔다. 서버 자신은 바꾸지 않는다.
        /// </summary>
        /// <param name="epoch">서버가 보낸 epoch(0이면 무시).</param>
        public void SetRemoteProjectileEpoch(ulong epoch)
        {
            if (epoch != 0 && !NetworkContext.IsNetworkServer) ProjectileEpoch = epoch;
        }

        /// <summary>
        /// Client 전용 — 서버 발사 기록을 받아 화면 소비자에게 넘긴다. 판정 목록에는 넣지 않는다(서버 권위 유지).
        /// </summary>
        /// <param name="launch">서버 발사 기록.</param>
        public void ReceiveProjectileLaunch(ProjectileLaunchPresentation launch)
        {
            if (NetworkContext.IsNetworkServer || !launch.IsValid || launch.Key.Epoch != ProjectileEpoch) return;
            PublishProjectileLaunched(launch);
        }

        /// <summary>
        /// Client 전용 — 서버 착탄 결과를 받아 화면 소비자에게 넘긴다. HP는 별도 기존 RPC가 맞춘다.
        /// </summary>
        /// <param name="impact">서버 착탄 결과.</param>
        public void ReceiveProjectileImpact(ProjectileImpactPresentation impact)
        {
            if (NetworkContext.IsNetworkServer || !impact.IsValid || impact.Key.Epoch != ProjectileEpoch) return;
            PublishProjectileResolved(impact);
        }

        // ====================================================================
        // 내부 — 예약 / 발사 / 비행 / 착탄
        // ====================================================================

        private bool IsCurrentProjectileAttacker(UnitData attacker)
            => _unitSpawn != null && _unitSpawn.Units.TryGetValue(attacker.Id, out UnitData current)
               && ReferenceEquals(current, attacker);

        private int CountActiveProjectiles()
        {
            int active = 0;
            for (int i = 0; i < _projectiles.Count; i++) if (_projectiles[i].Active) active++;
            return active;
        }

        private void ProcessProjectileReservation(ProjectileRecord record, double now)
        {
            // ① 발사 전 공격자 사망·객체 교체 → 발사 안 함(Q6 · NET-CANCEL-003).
            if (!record.Attacker.IsAlive || !IsCurrentProjectileAttacker(record.Attacker))
            {
                CancelReservation(record, "attacker-unavailable");
                return;
            }
            // ② 발사 전(또는 발사 순간) 대상 사망·객체 교체 → 발사 안 함(Q6 · Q15).
            //    대상이 사거리 밖으로 나간 것은 여기서 보지 않는다 — 원래 대상에게 그대로 발사한다(Q5).
            IDamageable current = FindTargetById(record.TargetId, record.TargetIsUnit);
            if (current == null || !ReferenceEquals(current, record.Target) || !current.IsAlive)
            {
                CancelReservation(record, "target-unavailable");
                return;
            }
            if (now < record.Due) return;
            LaunchProjectile(record, current);
        }

        private void LaunchProjectile(ProjectileRecord record, IDamageable target)
        {
            Vector3 source = ResolveWorldPosition(record.Attacker);
            Vector3 center = ResolveWorldPosition(target);
            float radius = _projectileSettings.GetTargetRadius(target);
            float speed = _projectileSettings.GetUnitSpeed(record.AttackerType);
            // 비행 거리 = 중심 거리 − 대상 반경(Q12, 2026-10-20 2차 결정). 높이 차이는 보지 않는다(XZ 평면).
            float centerDistance = new Vector2(center.x - source.x, center.z - source.z).magnitude;
            float travel = Mathf.Max(0f, centerDistance - radius);
            // 발사 시각은 예약 시각(due) 그대로 둔다 — 프레임이 조금 늦게 와도 서버 일정(OnAttackHit offset)이 기준이다.
            double launchTime = record.Due;
            double impactTime = launchTime + travel / Mathf.Max(0.0001f, speed);

            record.Stage = ProjectileStage.InFlight;
            record.LaunchTime = launchTime;
            record.ImpactTime = impactTime;
            record.Source = source;
            record.LastTargetCenter = center;
            record.TargetRadius = radius;
            // 공격력은 지금(발사 순간) 값을 저장한다. 비행 중 공격자가 죽거나 버프가 끝나도 이 값으로 맞는다(Q3).
            record.AttackPower = EffectiveAttack(record.Attacker);

            // 싱글플레이는 예전에 "피해 순간"에 공격자 Domain Facing을 타일 방향으로 갱신했다.
            //   그 순간이 이제 발사 순간이므로 같은 갱신을 여기서 한 번 한다(싱글 동작 보존).
            //   멀티는 서버 Simulation Root가 방향 writer라 갱신하지 않는다(기존 ApplyAttackDamageObserved와 같다).
            if (!NetworkContext.IsNetworkActive)
                record.Attacker.Facing = FacingDirection.FromCoords(record.Attacker.Position, target.Position);

            int race = 0;
            record.Launch = new ProjectileLaunchPresentation(
                new ProjectileDeliveryKey(ProjectileEpoch, record.Id), record.Attacker.Id, attackerIsUnit: true,
                attackerKind: (int)record.AttackerType, attackerRace: race, team: record.Team,
                targetId: record.TargetId, targetIsUnit: record.TargetIsUnit,
                source: source, targetCenter: center, targetRadius: radius,
                launchTime: launchTime, impactTime: impactTime, homing: true);
            if (!record.Launch.IsValid)
            {
                // 좌표가 비정상(NaN 등)이면 착탄 지점을 정할 수 없다 — 피해를 주지 않고 취소로 닫는다(fail-closed).
                CancelReservation(record, "invalid-launch-geometry");
                return;
            }
            _projectileCueStates[record.Attacker.Id] = new ProjectileCueState { Attacker = record.Attacker, Cancelled = false };
            ProjectileLog("launched", record, impactTime);
            PublishProjectileLaunched(record.Launch);
        }

        private void ProcessProjectileFlight(ProjectileRecord record, double now)
        {
            // Homing: 원래 대상 "객체"가 살아 있는 동안 위치를 따라간다. 한 번 사라지면 마지막 위치에 고정한다.
            if (!record.TargetLost)
            {
                IDamageable current = FindTargetById(record.TargetId, record.TargetIsUnit);
                if (current != null && ReferenceEquals(current, record.Target) && current.IsAlive)
                    record.LastTargetCenter = ResolveWorldPosition(current);
                else
                    record.TargetLost = true;
            }
            if (now < record.ImpactTime) return;

            // 외부 이벤트(피해→사망→게임 종료) 재진입 전에 이 투사체를 먼저 소비한다 — 같은 착탄이 두 번 나지 않는다.
            //   결과에 필요한 값은 지역 변수로 먼저 복사한다(재진입한 게임 종료 닫기가 기록을 초기화해도 안전).
            record.Active = false;
            Vector3 point = ProjectileSurfacePoint(record.Source, record.LastTargetCenter, record.TargetRadius);
            var key = new ProjectileDeliveryKey(ProjectileEpoch, record.Id);
            int targetId = record.TargetId;
            bool targetIsUnit = record.TargetIsUnit;
            double impactTime = record.ImpactTime;
            if (!record.TargetLost)
            {
                AttackDamageObservation observation = ApplyProjectileImpactDamage(record, key);
                // 이 피해로 성이 무너져 게임이 끝나 닫기가 재진입해도, 결과는 위 지역 변수로 한 번 보낸다
                //   (피해는 이미 확정됐다. 화면 소비자는 게임 종료 이벤트에서 스스로 남은 표현을 닫는다).
                if (observation.Status == AttackDamageApplyStatus.Applied)
                {
                    ProjectileLog("hit", record, impactTime);
                    PublishProjectileResolved(new ProjectileImpactPresentation(key, ProjectileImpactOutcome.Hit,
                        point, targetId, targetIsUnit, observation.ResultingHp,
                        observation.AppliedAmount, impactTime));
                    return;
                }
            }
            ProjectileLog("target-lost", record, impactTime);
            PublishProjectileResolved(new ProjectileImpactPresentation(key, ProjectileImpactOutcome.TargetLost,
                point, targetId, targetIsUnit, -1, 0, impactTime));
        }

        /// <summary>
        /// 착탄 피해 — 기존 단일 HP writer(ApplyAttackDamageCore → ExecuteAttack)를 그대로 한 번 부른다.
        /// 다른 점은 두 가지뿐이다: ① 공격자가 이미 죽었어도 진행(Persist), ② 공격력은 발사 때 저장한 값.
        /// </summary>
        private AttackDamageObservation ApplyProjectileImpactDamage(ProjectileRecord record, ProjectileDeliveryKey key)
        {
            _projectileImpactAttacker = record.Attacker;
            _projectileImpactAttackPower = record.AttackPower;
            _projectileImpactKey = key;
            try
            {
                return ApplyAttackDamageCore(record.Attacker, record.TargetId, record.TargetIsUnit,
                    updateLegacyDomainFacing: false, presentationResultKey: default,
                    allowInactiveAttacker: true);
            }
            finally
            {
                _projectileImpactAttacker = null;
                _projectileImpactAttackPower = 0;
                _projectileImpactKey = default;
            }
        }

        /// <summary>
        /// 대상 "표면 지점" = 대상 중심에서 쏜 쪽(발사 위치) 방향으로 반경만큼 당긴 점(Q12).
        /// 발사 위치가 반경 안이면(아주 가까우면) 중심을 그대로 쓴다. 높이는 대상 중심 값을 쓴다.
        /// </summary>
        /// <param name="source">발사 위치.</param>
        /// <param name="center">대상 중심.</param>
        /// <param name="radius">대상 반경.</param>
        public static Vector3 ProjectileSurfacePoint(Vector3 source, Vector3 center, float radius)
        {
            Vector2 toSource = new Vector2(source.x - center.x, source.z - center.z);
            float distance = toSource.magnitude;
            if (radius <= 0f || distance <= radius || distance < 0.0001f) return center;
            Vector2 offset = toSource / distance * radius;
            return new Vector3(center.x + offset.x, center.y, center.z + offset.y);
        }

        private void CancelReservation(ProjectileRecord record, string reason)
        {
            record.Active = false;
            if (record.Attacker != null)
                _projectileCueStates[record.Attacker.Id] = new ProjectileCueState { Attacker = record.Attacker, Cancelled = true };
            ProjectileLog("cancelled-" + reason, record, record.Due);
        }

        private void CompactProjectiles()
        {
            // Tick 도중(재진입 호출)에는 압축하지 않는다. Tick이 끝나며 한 번에 정리한다.
            if (_projectileTicking) return;
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                ProjectileRecord record = _projectiles[i];
                if (record.Active) continue;
                _projectiles.RemoveAt(i);
                record.Reset();
                _projectilePool.Push(record);
            }
        }

        // 구독자(화면·전송) 예외가 서버 판정(피해·다음 투사체)을 멈추지 않게 하나씩 격리해 부른다(파도 선례).
        private void PublishProjectileLaunched(ProjectileLaunchPresentation launch)
        {
            Delegate[] handlers = ProjectileLaunched?.GetInvocationList();
            if (handlers == null) return;
            for (int i = 0; i < handlers.Length; i++)
            {
                try { ((Action<ProjectileLaunchPresentation>)handlers[i])(launch); }
                catch (Exception exception) { ProjectileSubscriberFault("launch", launch.Key, i, exception); }
            }
        }

        private void PublishProjectileResolved(ProjectileImpactPresentation impact)
        {
            Delegate[] handlers = ProjectileResolved?.GetInvocationList();
            if (handlers == null) return;
            for (int i = 0; i < handlers.Length; i++)
            {
                try { ((Action<ProjectileImpactPresentation>)handlers[i])(impact); }
                catch (Exception exception) { ProjectileSubscriberFault("impact", impact.Key, i, exception); }
            }
        }

        private void ProjectileSubscriberFault(string stage, ProjectileDeliveryKey key, int index, Exception exception)
        {
            try
            {
                GameLog.Ops.Warn(LogEvent.UnhandledException, "Combat", nameof(UnitCombatUseCase),
                    "Projectile presentation degraded; authoritative processing continues", exception,
                    $"Stage={stage}, Projectile={key}, SubscriberIndex={index}, GameplayContinues=True, PresentationDegraded=True");
            }
            catch (Exception)
            { /* logger 자체 실패 — 서버 판정으로 다시 던지지 않는다. */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ProjectileLog(string stage, ProjectileRecord record, double time)
        {
            if (_projectileDiagnosticLines >= ProjectileDiagnosticLimit) return;
            _projectileDiagnosticLines++;
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE]",
                FormattableString.Invariant($"epoch={ProjectileEpoch:x16}, stage={stage}, id={record.Id}, attacker={(record.Attacker != null ? record.Attacker.Id : -1)}, type={(int)record.AttackerType}, target={record.TargetId}, targetIsUnit={record.TargetIsUnit}, hit={record.HitIndex}, due={record.Due:G17}, launch={record.LaunchTime:G17}, impact={record.ImpactTime:G17}, time={time:G17}, radius={record.TargetRadius:F3}, attack={record.AttackPower}, detail={_projectileDiagnosticLines}/{ProjectileDiagnosticLimit}"));
        }
    }
}
