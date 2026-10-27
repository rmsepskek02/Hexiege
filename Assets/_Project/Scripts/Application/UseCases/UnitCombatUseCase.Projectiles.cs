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
//   [2026-10-21 단계 D — 차지 유닛 InfernoSpirit · FoxMagician]
//     2)의 "발사 취소"가 일어나면 ProjectileLaunchCancelled(사유 포함)를 발행한다. 화면 소비자가 받아 차지 VFX를 즉시 끄고,
//     전송 계층이 Client에게 같은 통보를 보낸다(회차 번호를 붙여). 싱글·Host 화면은 IsChargePresentationCancelled로
//     "이미 취소된 공격의 차지 신호"를 막는다. 사거리 이탈은 여전히 취소가 아니다(Q5).
//
// [2026-10-21 단계 C — 건물 공격자(인간 방어타워 CannonTower)]
//   타워는 공격 애니메이션 marker가 없어 "공격 순간 = 발사 순간"이다. 그래서 예약 단계 없이
//   TryLaunchTowerProjectile가 곧바로 비행 중(InFlight) 기록을 만든다(발사 전 끊김 구간이 없다 — 대상은 그 순간 살아 있다).
//   발사 위치는 타워 포구(ProjectileSettings.ResolveTowerMuzzle), 속도는 HumanTowerSpeed, 공격력은 발사 때 저장한다.
//   비행·Homing·대상 소실·닫기는 유닛 투사체와 똑같은 코드(ProcessProjectileFlight)를 탄다.
//   다른 점은 착탄 피해 writer 하나뿐이다 — 유닛은 ApplyAttackDamageCore, 타워는 IBuildingProjectileImpactWriter
//   (= TowerCombatUseCase의 타워 피해 writer). 비행 중 타워가 파괴돼도 맞으면 피해를 준다(공격자 사망과 같음 — U-6).
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
            // 유닛 공격자면 Attacker(UnitData), 건물 공격자(단계 C 타워)면 AttackerBuilding이 채워진다(둘 중 하나만).
            public UnitData Attacker;
            public UnitType AttackerType;
            // ── [단계 C] 건물 공격자 칸 ──
            public bool AttackerIsUnit = true;
            public BuildingData AttackerBuilding;
            public RaceId AttackerRace;
            public IBuildingProjectileImpactWriter BuildingWriter;
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
            // ── [2026-10-20 단계 B 진단 전용] 발사 뒤 공격자가 처음 죽은 것으로 보인 시각(판정에는 쓰지 않는다) ──
            public bool AttackerDeadObserved;
            public double AttackerDeadAt;

            public void Reset()
            {
                Active = false; Stage = ProjectileStage.Reserved; Id = 0; Attacker = null;
                AttackerType = default; Team = default; Target = null; TargetId = -1; TargetIsUnit = false;
                AttackerIsUnit = true; AttackerBuilding = null; AttackerRace = default; BuildingWriter = null;
                Due = 0d; HitIndex = 0; LaunchTime = 0d; ImpactTime = 0d; Source = Vector3.zero;
                LastTargetCenter = Vector3.zero; TargetRadius = 0f; AttackPower = 0; TargetLost = false;
                Launch = default; AttackerDeadObserved = false; AttackerDeadAt = 0d;
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
        /// [2026-10-21 단계 D] 서버가 유닛 공격자의 발사 예약을 "발사 전에" 취소했을 때(Client는 취소 통보를 받았을 때) 발행된다.
        /// 화면 소비자(ProjectilePresentationPresenter)가 구독해 그 공격자의 차지 VFX를 즉시 끈다. 전송 계층은 Client로 전달한다.
        /// 피해·HP와는 무관하다(취소된 예약은 처음부터 피해를 만들지 않는다).
        /// </summary>
        public event Action<ProjectileLaunchCancelPresentation> ProjectileLaunchCancelled;

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
                || !ServerProjectileDeliveryPolicy.IsProjectileUnit(attacker.Type)   // 비투사체 유닛이 잘못 들어오면 거절
                || !IsCurrentProjectileAttacker(attacker)
                || double.IsNaN(due) || double.IsInfinity(due) || due < 0d)
            {
                DiagProjectileReserve(false);   // [진단] 예약 거절 수(판정 무관)
                return false;
            }
            IDamageable target = FindTargetById(targetId, targetIsUnit);
            if (target == null || !target.IsAlive || !CanReserveProjectileLaunches(1))
            {
                DiagProjectileReserve(false);
                return false;
            }

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
            DiagProjectileReserve(true);
            DiagChargeUnitStage(attacker.Type, launched: false);   // [단계 D 진단] 차지 유닛 예약 수
            ProjectileLog("reserved", record, due);
            return true;
        }

        /// <summary>
        /// [2026-10-21 단계 C] 투사체 타워(인간 방어타워 CannonTower)의 공격 1회 = "지금 발사"한다(서버/싱글 전용).
        ///
        /// [유닛 투사체와 다른 점 — 유니티 초급자용]
        ///   · 예약 단계가 없다. 타워에는 공격 애니메이션 marker가 없어 공격 순간이 곧 발사 순간이다.
        ///     그래서 "발사 전 대상 사망 → 취소"는 "공격 순간 대상이 살아 있어야 발사"로 대신한다(Q15 — 죽은 대상은 쏘지 않는다).
        ///   · 발사 위치 = 타워 포구(ProjectileSettings.ResolveTowerMuzzle — 사용자 결정 「대포 입구에서 발사」).
        ///   · 속도 = ProjectileSettings.GetTowerSpeed(인간 타워 4 — ProjectileConfig.asset의 _humanTowerSpeed).
        ///   · 착탄 피해 = writer(TowerCombatUseCase)가 발사 때 저장한 공격력으로 1회 준다. 비행 중 타워가 파괴돼도 준다(U-6).
        /// 비행 시간 고정(U-1)·Homing·비행 중 대상 소실(Q4)·닫기(위험 7)는 유닛 투사체와 같은 코드가 처리한다.
        /// </summary>
        /// <param name="tower">쏘는 타워(객체 참조로 보관 — ID 재사용 안전).</param>
        /// <param name="race">타워 소유 팀의 종족(프리셋 선택·분류 확인용).</param>
        /// <param name="target">원래 대상 유닛(타워는 유닛만 공격한다 — 방어 타워 규칙 1).</param>
        /// <param name="attackPower">발사 순간의 타워 공격력(방어 감쇄 전 원래 값). 착탄 때 이 값으로 맞는다.</param>
        /// <param name="writer">착탄 피해를 적용할 타워 writer.</param>
        /// <returns>발사했으면 true. false면 호출자는 공격·쿨다운을 커밋하지 않는다(용량 초과·닫힘·무효 입력).</returns>
        public bool TryLaunchTowerProjectile(BuildingData tower, RaceId race, UnitData target, int attackPower,
            IBuildingProjectileImpactWriter writer)
        {
            double now = ProjectileServerTime;
            if (_projectilesClosed || tower == null || !tower.IsAlive || target == null || !target.IsAlive
                || writer == null || attackPower < 0
                || !ServerProjectileDeliveryPolicy.IsProjectileTower(tower.Type, race)   // 즉발 타워가 잘못 들어오면 거절
                || !IsCurrentProjectileTower(tower) || !IsCurrentProjectileTargetUnit(target)
                || double.IsNaN(now) || double.IsInfinity(now) || now < 0d || !CanReserveProjectileLaunches(1))
            {
                DiagProjectileReserve(false);   // [진단] 발사 거절 수(판정 무관) — 유닛 예약 거절과 같은 칸
                return false;
            }

            ProjectileRecord record = _projectilePool.Count > 0 ? _projectilePool.Pop() : new ProjectileRecord();
            record.Reset();
            record.Active = true;
            record.Id = ++_nextProjectileId;
            record.AttackerIsUnit = false;
            record.AttackerBuilding = tower;
            record.AttackerRace = race;
            record.BuildingWriter = writer;
            record.Team = tower.Team;
            record.Target = target;
            record.TargetId = target.Id;
            record.TargetIsUnit = true;
            record.Due = now;
            record.HitIndex = 0;
            _projectiles.Add(record);
            // [진단] 수지(reserved = launched + cancelled + closedReserved)가 유닛과 같은 식으로 맞도록 "예약 1 + 발사 1"로 센다.
            DiagProjectileReserve(true);

            // 발사 위치 = 타워 루트(서버 화면 = 도메인 좌표, 건물 Y 오프셋 포함) + 팀 방향으로 돌린 포구 오프셋.
            Vector3 towerPosition = ResolveWorldPosition(tower);
            Vector3 source = _projectileSettings.ResolveTowerMuzzle(towerPosition, tower.Team);
            Vector3 center = ResolveWorldPosition(target);
            float radius = _projectileSettings.GetTargetRadius(target);
            float speed = _projectileSettings.GetTowerSpeed(tower.Type, race);
            // 비행 거리 = 포구에서 대상 중심까지의 XZ 거리 − 대상 반경(Q12). 유닛 투사체(LaunchProjectile)와 같은 식이다.
            float centerDistance = new Vector2(center.x - source.x, center.z - source.z).magnitude;
            float travel = Mathf.Max(0f, centerDistance - radius);
            double impactTime = now + travel / Mathf.Max(0.0001f, speed);

            record.Stage = ProjectileStage.InFlight;
            record.LaunchTime = now;
            record.ImpactTime = impactTime;
            record.Source = source;
            record.LastTargetCenter = center;
            record.TargetRadius = radius;
            record.AttackPower = attackPower;
            record.Launch = new ProjectileLaunchPresentation(
                new ProjectileDeliveryKey(ProjectileEpoch, record.Id), tower.Id, attackerIsUnit: false,
                attackerKind: (int)tower.Type, attackerRace: (int)race, team: tower.Team,
                targetId: target.Id, targetIsUnit: true,
                source: source, targetCenter: center, targetRadius: radius,
                launchTime: now, impactTime: impactTime, homing: true);
            if (!record.Launch.IsValid)
            {
                // 좌표가 비정상이면 착탄 지점을 정할 수 없다 — 피해 없이 취소로 닫는다(fail-closed, 유닛과 같다).
                CancelReservation(record, "invalid-launch-geometry", "invalid-launch-geometry");
                CompactProjectiles();
                return false;
            }
            DiagProjectileLaunch(record, now, centerDistance);
            ProjectileLog("launched", record, impactTime,
                FormattableString.Invariant($"distance={centerDistance:F3}, flight={impactTime - now:F3}"));
            PublishProjectileLaunched(record.Launch);
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
                    CancelReservation(record, reason, reason);
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
        /// [2026-10-21 단계 D] 차지 유닛 화면이 "차지 시작" 신호(OnAttackCharge)에서 차지 VFX를 켜도 되는지 묻는 서버 권위 질문.
        /// true = 이 공격자의 가장 최근 서버 투사체 예약이 발사 전에 취소됐고, 그 뒤 새 예약이 없다 → 차지를 켜지 않는다.
        ///
        /// [왜 필요한가 — 유니티 초급자용]
        ///   싱글플레이에는 멀티의 "공격 1회분 허가표(lease)"가 없어서, 화면은 서버가 이번 공격을 취소했는지 알 수 없다.
        ///   예: 공격을 시작하자마자(Inferno 차지 신호 0.1667초 전) 대상이 죽으면, 서버는 발사를 이미 취소했는데
        ///   화면은 곧이어 차지 VFX를 켜고 끝까지 모은 뒤 아무것도 쏘지 않는다. 이 질문이 그 장면을 막는다.
        ///   새 공격이 커밋되면(새 예약) false로 돌아가 다음 차지는 정상적으로 켜진다.
        /// 서버 권위가 있는 실행 형태(싱글·Host)에서만 의미가 있다 — 순수 Client는 예약 정보가 없어 항상 false다
        ///   (Client는 대신 서버 취소 통보 + lease 회차 규칙으로 막는다 — UnitView.ApplyServerProjectileLaunchCancel).
        /// 투사체가 아닌 유닛은 발사 예약이 없으므로 항상 false다.
        /// </summary>
        /// <param name="attacker">차지를 켜려는 유닛.</param>
        public bool IsChargePresentationCancelled(UnitData attacker)
            => attacker != null && ServerProjectileDeliveryPolicy.IsProjectileUnit(attacker.Type)
               && WasLatestProjectileLaunchCancelled(attacker);

        /// <summary>
        /// [2026-10-21 단계 D] Client 전용 — 서버의 발사 취소 통보를 받아 화면 소비자에게 넘긴다. 판정 목록은 건드리지 않는다.
        /// </summary>
        /// <param name="cancel">서버 취소 통보(회차 번호 포함).</param>
        public void ReceiveProjectileLaunchCancel(ProjectileLaunchCancelPresentation cancel)
        {
            if (NetworkContext.IsNetworkServer || !cancel.IsValid || cancel.Key.Epoch != ProjectileEpoch) return;
            PublishProjectileLaunchCancelled(cancel);
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
            finally
            {
                _projectileTicking = false;
                // [단계 B 진단] 착탄 피해로 게임이 끝나 Tick 도중에 닫혔다면, 그 착탄까지 센 뒤 END를 여기서 남긴다.
                DiagProjectileFlushPendingEnd();
            }
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
            // [2026-10-20 단계 B] 처음 닫힐 때 경기 전체 합계 END 1줄을 남긴다. 남은 투사체가 0발이어도 항상 남긴다(진단 전용).
            //   남은 예약/비행 수(closedReserved/closedInFlight)를 세야 하므로 아래 초기화보다 먼저 부른다.
            if (!alreadyClosed) DiagProjectileClose(reason);
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

        // [단계 C] 이 타워 객체가 아직 건물 목록의 "그 객체"인가(파괴·철거·ID 재사용이면 false).
        private bool IsCurrentProjectileTower(BuildingData tower)
            => _buildingPlacement != null && _buildingPlacement.Buildings.TryGetValue(tower.Id, out BuildingData current)
               && ReferenceEquals(current, tower);

        // [단계 C] 이 대상 유닛 객체가 아직 유닛 목록의 "그 객체"인가.
        private bool IsCurrentProjectileTargetUnit(UnitData target)
            => _unitSpawn != null && _unitSpawn.Units.TryGetValue(target.Id, out UnitData current)
               && ReferenceEquals(current, target);

        private int CountActiveProjectiles()
        {
            int active = 0;
            for (int i = 0; i < _projectiles.Count; i++) if (_projectiles[i].Active) active++;
            return active;
        }

        private void ProcessProjectileReservation(ProjectileRecord record, double now)
        {
            // [단계 C] 건물 공격자는 예약 없이 바로 발사하므로 여기에 올 수 없다. 와도(코드 오류) 피해 없이 닫는다(fail-closed).
            if (!record.AttackerIsUnit || record.Attacker == null)
            {
                CancelReservation(record, "attacker-unavailable", "other");
                return;
            }
            // ① 발사 전 공격자 사망·객체 교체 → 발사 안 함(Q6 · NET-CANCEL-003).
            if (!record.Attacker.IsAlive || !IsCurrentProjectileAttacker(record.Attacker))
            {
                // [단계 B 진단] 사유 토큰(reason)은 단계 A와 같게 두고, 세부 원인(cause)만 갈라 기록한다.
                CancelReservation(record, "attacker-unavailable",
                    !record.Attacker.IsAlive ? "attacker-dead" : "attacker-replaced");
                return;
            }
            // ② 발사 전(또는 발사 순간) 대상 사망·객체 교체 → 발사 안 함(Q6 · Q15).
            //    대상이 사거리 밖으로 나간 것은 여기서 보지 않는다 — 원래 대상에게 그대로 발사한다(Q5).
            IDamageable current = FindTargetById(record.TargetId, record.TargetIsUnit);
            if (current == null || !ReferenceEquals(current, record.Target) || !current.IsAlive)
            {
                // 세부 원인: 붙잡아 둔 대상 객체가 죽었으면 target-dead(사망 뒤 목록에서 빠진 경우 포함),
                //   살아 있는데 목록에 없으면 target-missing, 같은 Id에 다른 객체가 있으면 target-replaced.
                string cause = record.Target == null || !record.Target.IsAlive ? "target-dead"
                    : current == null ? "target-missing" : "target-replaced";
                CancelReservation(record, "target-unavailable", cause);
                return;
            }
            if (now < record.Due) return;
            LaunchProjectile(record, current, now);
        }

        private void LaunchProjectile(ProjectileRecord record, IDamageable target, double now)
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
                CancelReservation(record, "invalid-launch-geometry", "invalid-launch-geometry");
                return;
            }
            _projectileCueStates[record.Attacker.Id] = new ProjectileCueState { Attacker = record.Attacker, Cancelled = false };
            DiagProjectileLaunch(record, now, centerDistance);
            DiagChargeUnitStage(record.AttackerType, launched: true);   // [단계 D 진단] 차지 유닛 발사 수
            ProjectileLog("launched", record, impactTime,
                FormattableString.Invariant($"distance={centerDistance:F3}, flight={impactTime - launchTime:F3}"));
            PublishProjectileLaunched(record.Launch);
        }

        private void ProcessProjectileFlight(ProjectileRecord record, double now)
        {
            // [단계 B 진단] 발사 뒤 공격자가 처음 죽은 것으로 보인 시각을 기억한다(판정 무관 — Q3 확인용).
            DiagProjectileObserveAttacker(record, now);
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
            // [단계 B 진단] 피해 적용이 게임 종료 닫기로 재진입하면 record가 초기화되므로, 진단에 필요한 값을 먼저 복사해 둔다
            //   (공격자 생사는 이 메서드 맨 앞에서 이번 프레임 기준으로 이미 확인했다 — 같은 프레임 사망 포함).
            ProjectileImpactDiagnosticSnapshot diag = CaptureProjectileImpactDiagnostics(record);
            if (!record.TargetLost)
            {
                AttackDamageObservation observation = ApplyProjectileImpactDamage(record, key);
                // 이 피해로 성이 무너져 게임이 끝나 닫기가 재진입해도, 결과는 위 지역 변수로 한 번 보낸다
                //   (피해는 이미 확정됐다. 화면 소비자는 게임 종료 이벤트에서 스스로 남은 표현을 닫는다).
                if (observation.Status == AttackDamageApplyStatus.Applied)
                {
                    DiagProjectileImpact(diag, now, true, false, observation.Status,
                        observation.AppliedAmount, observation.ResultingHp);
                    ProjectileLog("hit", diag, impactTime,
                        FormattableString.Invariant($"attackerAlive={(diag.AttackerDeadObserved ? 0 : 1)}, applied={observation.AppliedAmount}, hp={observation.ResultingHp}"));
                    PublishProjectileResolved(new ProjectileImpactPresentation(key, ProjectileImpactOutcome.Hit,
                        point, targetId, targetIsUnit, observation.ResultingHp,
                        observation.AppliedAmount, impactTime));
                    return;
                }
                // 대상은 살아 있었지만 writer가 피해를 적용하지 않았다(예: writer 내부 조건) — 결과는 기존대로 TargetLost.
                DiagProjectileImpact(diag, now, false, true, observation.Status, 0, -1);
                ProjectileLog("target-lost", diag, impactTime,
                    FormattableString.Invariant($"cause=writer-{observation.Status}, attackerAlive={(diag.AttackerDeadObserved ? 0 : 1)}"));
            }
            else
            {
                DiagProjectileImpact(diag, now, false, false, AttackDamageApplyStatus.TargetUnavailable, 0, -1);
                ProjectileLog("target-lost", diag, impactTime,
                    FormattableString.Invariant($"cause=target-gone-in-flight, attackerAlive={(diag.AttackerDeadObserved ? 0 : 1)}"));
            }
            PublishProjectileResolved(new ProjectileImpactPresentation(key, ProjectileImpactOutcome.TargetLost,
                point, targetId, targetIsUnit, -1, 0, impactTime));
        }

        /// <summary>
        /// 착탄 피해 — 기존 단일 HP writer(ApplyAttackDamageCore → ExecuteAttack)를 그대로 한 번 부른다.
        /// 다른 점은 두 가지뿐이다: ① 공격자가 이미 죽었어도 진행(Persist), ② 공격력은 발사 때 저장한 값.
        /// </summary>
        private AttackDamageObservation ApplyProjectileImpactDamage(ProjectileRecord record, ProjectileDeliveryKey key)
        {
            // [단계 C] 건물 공격자(인간 타워) — 타워 피해의 단일 writer(TowerCombatUseCase)에 맡긴다.
            //   타워는 유닛만 공격하므로 대상은 UnitData다. 타워가 이미 파괴됐어도 진행한다(공격자 사망과 같음 — U-6).
            //   공격력은 발사 때 저장한 원래 값(record.AttackPower), 방어 감쇄는 writer가 착탄 순간의 연구 레벨로 한다.
            if (!record.AttackerIsUnit)
            {
                if (record.BuildingWriter == null || record.AttackerBuilding == null || !(record.Target is UnitData unitTarget))
                    return AttackDamageObservation.Unavailable(AttackDamageApplyStatus.AttackerUnavailable);
                return record.BuildingWriter.ApplyTowerProjectileImpact(record.AttackerBuilding, unitTarget,
                    record.AttackPower, key);
            }
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

        /// <param name="record">취소할 예약.</param>
        /// <param name="reason">단계 A와 같은 사유 토큰(개별 로그 stage 이름 "cancelled-" + reason).</param>
        /// <param name="cause">[단계 B 진단] 세부 원인(attacker-dead / target-dead / move-command 등) — END 합계와 개별 로그 cause= 칸.</param>
        private void CancelReservation(ProjectileRecord record, string reason, string cause)
        {
            record.Active = false;
            if (record.Attacker != null)
                _projectileCueStates[record.Attacker.Id] = new ProjectileCueState { Attacker = record.Attacker, Cancelled = true };
            DiagProjectileCancel(cause);
            DiagChargeUnitCancel(record.AttackerType, record.AttackerIsUnit, cause);   // [단계 D 진단] 차지 유닛 사유별 취소 수
            ProjectileLog("cancelled-" + reason, record, record.Due, "cause=" + cause);
            // [2026-10-21 단계 D] 유닛 공격자의 취소는 화면(차지 VFX 즉시 정지)과 전송 계층(Client 전달)에 알린다.
            //   건물 공격자(타워)는 예약 단계가 없고 차지도 없어 알리지 않는다.
            //   알림은 판정이 끝난 뒤의 "사실 전달"이다 — 구독자 예외는 격리되어 서버 판정(다음 투사체)을 멈추지 않는다.
            if (record.AttackerIsUnit && record.Attacker != null)
                PublishProjectileLaunchCancelled(new ProjectileLaunchCancelPresentation(
                    new ProjectileDeliveryKey(ProjectileEpoch, record.Id), record.Attacker.Id, (int)record.AttackerType,
                    ToCancelCause(cause), presentationRevision: 0UL, cancelTime: SafeProjectileTime()));
        }

        // [단계 D] 진단 사유 토큰(문자열) → 네트워크로 보내는 취소 사유 값. 표에 없는 토큰은 Other.
        private static ProjectileLaunchCancelCause ToCancelCause(string cause)
        {
            switch (cause)
            {
                case "attacker-dead": return ProjectileLaunchCancelCause.AttackerDead;
                case "attacker-replaced": return ProjectileLaunchCancelCause.AttackerReplaced;
                case "target-dead": return ProjectileLaunchCancelCause.TargetDead;
                case "target-missing": return ProjectileLaunchCancelCause.TargetMissing;
                case "target-replaced": return ProjectileLaunchCancelCause.TargetReplaced;
                case "move-command": return ProjectileLaunchCancelCause.MoveCommand;
                case "invalid-launch-geometry": return ProjectileLaunchCancelCause.InvalidGeometry;
                default: return ProjectileLaunchCancelCause.Other;
            }
        }

        // [단계 D] 취소 통보의 시각. 시계가 비정상(NaN·음수)이면 0 — 통보 검사(FiniteTime)를 통과시켜 화면 정지가 빠지지 않게 한다.
        private double SafeProjectileTime()
        {
            double now = ProjectileServerTime;
            return double.IsNaN(now) || double.IsInfinity(now) || now < 0d ? 0d : now;
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

        // [단계 D] 발사 취소 통보 — 다른 두 발행과 같은 격리 규칙.
        private void PublishProjectileLaunchCancelled(ProjectileLaunchCancelPresentation cancel)
        {
            Delegate[] handlers = ProjectileLaunchCancelled?.GetInvocationList();
            if (handlers == null) return;
            for (int i = 0; i < handlers.Length; i++)
            {
                try { ((Action<ProjectileLaunchCancelPresentation>)handlers[i])(cancel); }
                catch (Exception exception) { ProjectileSubscriberFault("launch-cancel", cancel.Key, i, exception); }
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
        private void ProjectileLog(string stage, ProjectileRecord record, double time, string detail = null)
            => ProjectileLog(stage, CaptureProjectileImpactDiagnostics(record), time, detail);

        // [단계 B] 개별 로그 형식은 단계 A와 같고, 끝에 선택 칸(detail — cause= / distance= 등)만 덧붙인다.
        //   상한(96줄)을 넘긴 줄은 버리되 몇 줄 버렸는지 END의 detailOmitted로 센다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ProjectileLog(string stage, ProjectileImpactDiagnosticSnapshot record, double time, string detail = null)
        {
            if (_projectileDiagnosticLines >= ProjectileDiagnosticLimit) { DiagProjectileDetailOmitted(); return; }
            _projectileDiagnosticLines++;
            string extra = string.IsNullOrEmpty(detail) ? string.Empty : ", " + detail;
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE]",
                FormattableString.Invariant($"epoch={ProjectileEpoch:x16}, stage={stage}, id={record.Id}, attacker={record.AttackerId}, type={ProjectileAttackerTypeToken(record)}, target={record.TargetId}, targetIsUnit={record.TargetIsUnit}, hit={record.HitIndex}, due={record.Due:G17}, launch={record.LaunchTime:G17}, impact={record.ImpactTime:G17}, time={time:G17}, radius={record.TargetRadius:F3}, attack={record.AttackPower}, detail={_projectileDiagnosticLines}/{ProjectileDiagnosticLimit}") + extra);
        }
    }
}
