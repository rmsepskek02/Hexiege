// ============================================================================
// UnitCombatUseCase.ProjectileDiagnostics.cs
// [2026-10-20 서버 권위 투사체 — 단계 B 진단 보강] 서버(싱글·Host) 투사체 수명 합계와 경기 종료 END 1줄.
//
// [왜 필요한가 — 유니티 초급자용]
//   단계 A의 개별 로그([PROJECTILE] stage=...)는 경기당 앞 96줄만 남는다(로그 폭주 방지).
//   그래서 "경기 전체에서 몇 발 쐈고, 몇 발이 어떤 이유로 취소됐는가"를 그 로그만으로는 알 수 없었다.
//   이 파일은 모든 발을 숫자로만 세어 두었다가, 투사체 서비스를 처음 닫는 순간(게임 종료·맵 정리·서비스 교체·
//   디스폰·파괴 중 먼저 오는 것) [PROJECTILE-END] side=server 한 줄로 남긴다. 남은 투사체가 0발이어도 항상 남긴다.
//
// [지켜야 할 약속]
//   · 읽기 전용 관측이다. 여기 값은 발사·취소·피해 판단에 절대 쓰이지 않는다.
//   · 기록 메서드는 모두 [Conditional]이라 릴리스 빌드에서 호출이 사라진다(계수 필드 몇 개만 남는다).
//   · 개별 로그 96줄 상한(ProjectileDiagnosticLimit)은 그대로다. END 합계는 상한과 무관하게 전 경기 기준이다.
//
// [END 줄에서 읽는 법 — 다음 실기 분석용]
//   reserved = launched + cancelled + closedReserved,  launched = hit + targetLost + closedInFlight 이면 balance=ok.
//   cancel=... 은 취소 사유별 개수다(attacker-dead = 발사 전 공격자 사망, target-dead = 발사 전 대상 사망 등).
//   flight=... 은 서버 비행 시간 분포(초), dist=... 은 발사 순간 중심 거리 분포(월드 미터)다.
//   attackerDead=... 은 "발사 뒤 공격자가 죽었는데 착탄"한 발의 수와 사망~착탄 간격(초)이다.
//   path=... 은 투사체 유닛 종류별 "새 경로(s)/기존 경로(l)" 타격 수다(스위치 off 유닛이 기존 경로를 지켰는가).
//   [2026-10-21 단계 C — 건물 공격자 구분]
//     path=... 끝에 방어 타워 공격이 종족별로 붙는다: "tower-Human:s3/l0|tower-Spirit:s0/l5".
//       타워의 s = 서버 투사체 경로(인간 CannonTower), l = 기존 즉발 경로(RuneSpire·VineTower — 즉발 유지가 설계).
//       → RuneSpire 회귀 확인: tower-Spirit의 s가 0이고 l이 공격 수만큼이면 즉발을 지켰다.
//     건물 공격자 합계는 별도 줄 [PROJECTILE-END] side=server-tower 에 있다(서버 END 1줄이 기기 logcat 약 1024자 한도에
//       가까워 칸을 더 붙이지 않았다 — 2026-10-21 view END 잘림과 같은 위험):
//       byTower=launched:N|hit:N|targetLost:N|destroyedThenHit:N|destroyedThenLost:N
//       = 건물 공격자(타워)가 쏜 투사체만 따로 센 값. destroyedThenHit = "발사 뒤 타워가 파괴됐는데 착탄해 피해를 준" 발 수
//         (공격자 사망과 같은 규칙 U-6 확인용). 이 값들은 server 줄의 launched/hit/targetLost/attackerDead 합계에도 함께 들어 있다.
//       towerPath=Human:sN/lN|Spirit:sN/lN|Transcendence:sN/lN — 종족별 타워 공격 경로(공격 0회 종족도 0으로 찍는다).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public partial class UnitCombatUseCase
    {
        // "발사 뒤 공격자 사망 → 착탄" 표본 줄 상한(경기당). 개별 96줄 상한과 별도 예산이다.
        private const int ProjectileAttackerDeathSampleLimit = 16;

        // 취소 사유 표(END 줄에 항상 이 순서로 모두 찍는다 — 0인 사유도 "없었다"는 증거로 남긴다).
        private static readonly string[] ProjectileCancelCauses =
        {
            "attacker-dead", "attacker-replaced", "target-dead", "target-missing", "target-replaced",
            "move-command", "invalid-launch-geometry", "other"
        };

        /// <summary>서버 투사체 수명 계수 묶음(진단 전용). 판정에는 쓰지 않는다.</summary>
        private sealed class ProjectileDiagnosticCounters
        {
            public int Reserved, ReserveRefused, Launched, LaunchedAtBuilding;
            public int Hit, HitBuilding, TargetLost, WriterNotApplied;
            public int ClosedReserved, ClosedInFlight;
            public int HitAfterAttackerDeath, LostAfterAttackerDeath, AttackerDeathSamples;
            public int DetailOmitted;
            // [단계 C] 건물 공격자(타워) 몫 — 위 합계의 부분 집합.
            public int TowerLaunched, TowerHit, TowerTargetLost, TowerDestroyedThenHit, TowerDestroyedThenLost;
            // [단계 C] 방어 타워 공격 경로(종족별, RaceId 0~2): s = 서버 투사체, l = 기존 즉발.
            public readonly int[] TowerServerPathByRace = new int[3];
            public readonly int[] TowerInstantPathByRace = new int[3];
            public double LaunchLagMax, ImpactLagMax;
            public readonly int[] Cancels = new int[ProjectileCancelCauses.Length];
            public readonly int[] ServerPathByType = new int[32];
            public readonly int[] LegacyPathByType = new int[32];
            public readonly ProjectileTimeDistribution Flight = new ProjectileTimeDistribution(0.1d, 0.2d, 0.4d, 0.7d);
            public readonly ProjectileTimeDistribution Distance = new ProjectileTimeDistribution(0.5d, 1d, 2d, 3d);
            public readonly ProjectileTimeDistribution AttackerDeathGap = new ProjectileTimeDistribution(0.1d, 0.3d);
            public bool EndWritten;
            // Tick 도중(착탄 피해 → 게임 종료) 닫혔을 때 END를 Tick 끝으로 미룬 사유. null이면 미룬 것이 없다.
            public string PendingEndReason;

            public int CancelledTotal
            {
                get { int sum = 0; for (int i = 0; i < Cancels.Length; i++) sum += Cancels[i]; return sum; }
            }
        }

        private readonly ProjectileDiagnosticCounters _projectileDiag = new ProjectileDiagnosticCounters();

        /// <summary>
        /// 개별 로그·착탄 진단에 필요한 투사체 값의 복사본(값 형식).
        /// 착탄 피해가 게임 종료 닫기로 재진입하면 기록(ProjectileRecord)이 초기화되므로, 피해 적용 전에 복사해 둔다.
        /// </summary>
        private struct ProjectileImpactDiagnosticSnapshot
        {
            public ulong Id; public int AttackerId; public UnitType AttackerType;
            public int TargetId; public bool TargetIsUnit; public int HitIndex;
            public double Due, LaunchTime, ImpactTime; public float TargetRadius; public int AttackPower;
            public bool AttackerDeadObserved; public double AttackerDeadAt;
            // [단계 C] 건물 공격자 구분(로그 type 칸·타워 계수용).
            public bool AttackerIsUnit; public RaceId AttackerRace;
        }

        private static ProjectileImpactDiagnosticSnapshot CaptureProjectileImpactDiagnostics(ProjectileRecord record)
        {
            return new ProjectileImpactDiagnosticSnapshot
            {
                // [단계 C] 옛 칸(기록용): AttackerId = record.Attacker != null ? record.Attacker.Id : -1,
                Id = record.Id,
                AttackerId = record.Attacker != null ? record.Attacker.Id
                    : record.AttackerBuilding != null ? record.AttackerBuilding.Id : -1,
                AttackerIsUnit = record.AttackerIsUnit, AttackerRace = record.AttackerRace,
                AttackerType = record.AttackerType, TargetId = record.TargetId, TargetIsUnit = record.TargetIsUnit,
                HitIndex = record.HitIndex, Due = record.Due, LaunchTime = record.LaunchTime,
                ImpactTime = record.ImpactTime, TargetRadius = record.TargetRadius, AttackPower = record.AttackPower,
                AttackerDeadObserved = record.AttackerDeadObserved, AttackerDeadAt = record.AttackerDeadAt
            };
        }

        /// <summary>
        /// [진단] 투사체 유닛(Q11 분류)의 공격 타격 1건이 새 경로(서버 발사 예약)와 기존 경로(타격 순간 피해) 중
        /// 어디로 갔는지 센다. 싱글 TryAttack과 멀티 서버 ExecuteAttack의 타격 분기에서 부른다. 비투사체 유닛은 무시한다.
        /// </summary>
        /// <param name="type">공격 유닛 종류.</param>
        /// <param name="serverPath">새 경로(이행 스위치 ON)면 true.</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void RecordProjectileUnitAttackPath(UnitType type, bool serverPath)
        {
            if (!ServerProjectileDeliveryPolicy.IsProjectileUnit(type)) return;
            int slot = (int)type;
            if (slot < 0 || slot >= _projectileDiag.ServerPathByType.Length) return;
            if (serverPath) _projectileDiag.ServerPathByType[slot]++;
            else _projectileDiag.LegacyPathByType[slot]++;
        }

        /// <summary>
        /// [진단 — 2026-10-21 단계 C] 방어 타워 공격 1회가 서버 투사체 경로(인간 CannonTower)와 기존 즉발 경로
        /// (RuneSpire·VineTower) 중 어디로 갔는지 종족별로 센다. TowerCombatUseCase.ExecuteTowerAttack에서 부른다.
        /// 판정에는 쓰지 않는다(읽기 전용 관측). END 줄 path= 끝의 "tower-종족:sN/lN" 조각이 이 값이다.
        /// </summary>
        /// <param name="race">타워 소유 팀의 종족.</param>
        /// <param name="serverPath">서버 투사체로 발사했으면 true, 즉발 피해면 false.</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void RecordProjectileTowerAttackPath(RaceId race, bool serverPath)
        {
            int slot = (int)race;
            if (slot < 0 || slot >= _projectileDiag.TowerServerPathByRace.Length) return;
            if (serverPath) _projectileDiag.TowerServerPathByRace[slot]++;
            else _projectileDiag.TowerInstantPathByRace[slot]++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileReserve(bool accepted)
        {
            if (accepted) _projectileDiag.Reserved++;
            else _projectileDiag.ReserveRefused++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileCancel(string cause)
        {
            int index = Array.IndexOf(ProjectileCancelCauses, cause);
            _projectileDiag.Cancels[index >= 0 ? index : ProjectileCancelCauses.Length - 1]++;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileLaunch(ProjectileRecord record, double now, float centerDistance)
        {
            _projectileDiag.Launched++;
            if (!record.TargetIsUnit) _projectileDiag.LaunchedAtBuilding++;
            if (!record.AttackerIsUnit) _projectileDiag.TowerLaunched++;   // [단계 C] 건물 공격자 몫
            _projectileDiag.Flight.Add(record.ImpactTime - record.LaunchTime);
            _projectileDiag.Distance.Add(centerDistance);
            // 발사 처리 지연 = 실제로 처리한 프레임 시각 − 예약된 발사 시각(프레임 격자 지연 관측).
            _projectileDiag.LaunchLagMax = Math.Max(_projectileDiag.LaunchLagMax, now - record.Due);
        }

        /// <summary>
        /// [진단] 비행 중 매 프레임 — 공격자가 처음 "죽은 상태"로 보인 시각을 기억한다(Q3 Persist 확인용).
        /// 판정은 바꾸지 않는다(착탄 피해는 공격자 생사와 무관하게 진행된다).
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileObserveAttacker(ProjectileRecord record, double now)
        {
            if (record.AttackerDeadObserved) return;
            // [단계 C] 건물 공격자(타워)는 "파괴됐거나 건물 목록의 그 객체가 아니면" 죽은 것으로 본다(공격자 사망과 같은 규칙).
            if (!record.AttackerIsUnit)
            {
                if (record.AttackerBuilding != null && record.AttackerBuilding.IsAlive
                    && IsCurrentProjectileTower(record.AttackerBuilding)) return;
                record.AttackerDeadObserved = true;
                record.AttackerDeadAt = now;
                return;
            }
            if (record.Attacker != null && record.Attacker.IsAlive && IsCurrentProjectileAttacker(record.Attacker)) return;
            record.AttackerDeadObserved = true;
            record.AttackerDeadAt = now;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileImpact(ProjectileImpactDiagnosticSnapshot record, double now, bool hit, bool writerNotApplied,
            AttackDamageApplyStatus writerStatus, int applied, int resultingHp)
        {
            _projectileDiag.ImpactLagMax = Math.Max(_projectileDiag.ImpactLagMax, now - record.ImpactTime);
            if (writerNotApplied) _projectileDiag.WriterNotApplied++;
            if (hit)
            {
                _projectileDiag.Hit++;
                if (!record.TargetIsUnit) _projectileDiag.HitBuilding++;
            }
            else _projectileDiag.TargetLost++;
            // [단계 C] 건물 공격자(타워) 몫 — 비행 중 타워가 파괴된 뒤의 착탄 결과도 따로 센다(U-6 확인용).
            if (!record.AttackerIsUnit)
            {
                if (hit) _projectileDiag.TowerHit++; else _projectileDiag.TowerTargetLost++;
                if (record.AttackerDeadObserved)
                {
                    if (hit) _projectileDiag.TowerDestroyedThenHit++; else _projectileDiag.TowerDestroyedThenLost++;
                }
            }
            if (!record.AttackerDeadObserved) return;

            // 발사 뒤 공격자가 죽은 발 — 착탄 결과와 사망~착탄 간격을 남긴다(표본 16줄 + END 합계).
            double gap = record.ImpactTime - record.AttackerDeadAt;
            if (hit) _projectileDiag.HitAfterAttackerDeath++;
            else _projectileDiag.LostAfterAttackerDeath++;
            _projectileDiag.AttackerDeathGap.Add(gap);
            if (_projectileDiag.AttackerDeathSamples >= ProjectileAttackerDeathSampleLimit) return;
            _projectileDiag.AttackerDeathSamples++;
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE] attacker-dead-before-impact",
                FormattableString.Invariant($"epoch={ProjectileEpoch:x16}, id={record.Id}, attacker={record.AttackerId}, type={ProjectileAttackerTypeToken(record)}, target={record.TargetId}, targetIsUnit={record.TargetIsUnit}, launch={record.LaunchTime:G17}, attackerDeadSeenAt={record.AttackerDeadAt:G17}, impact={record.ImpactTime:G17}, gap={gap:F3}, outcome={(hit ? "hit" : "target-lost")}, writer={writerStatus}, applied={applied}, hp={resultingHp}, sample={_projectileDiag.AttackerDeathSamples}/{ProjectileAttackerDeathSampleLimit}"));
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileClose(string reason)
        {
            for (int i = 0; i < _projectiles.Count; i++)
            {
                ProjectileRecord record = _projectiles[i];
                if (!record.Active) continue;
                if (record.Stage == ProjectileStage.Reserved) _projectileDiag.ClosedReserved++;
                else _projectileDiag.ClosedInFlight++;
            }
            if (_projectileDiag.EndWritten) return;
            // 착탄 피해 → 성 파괴 → 게임 종료로 Tick 도중에 닫히면, 지금 처리 중인 착탄이 아직 세어지지 않았다.
            //   그 상태로 END를 쓰면 balance가 1발 어긋나므로 Tick 끝(DiagProjectileFlushPendingEnd)으로 미룬다.
            if (_projectileTicking) { _projectileDiag.PendingEndReason = reason; return; }
            _projectileDiag.EndWritten = true;
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE-END]", BuildProjectileEndLine(reason));
            // [단계 C] 건물 공격자(타워) 합계 줄 — server 줄 바로 뒤, 같은 epoch·reason 머리로 짝을 맞춘다.
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE-END]", BuildProjectileTowerEndLine(reason));
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileFlushPendingEnd()
        {
            if (_projectileDiag.PendingEndReason == null || _projectileDiag.EndWritten) return;
            string reason = _projectileDiag.PendingEndReason;
            _projectileDiag.PendingEndReason = null;
            _projectileDiag.EndWritten = true;
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE-END]", BuildProjectileEndLine(reason));
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE-END]", BuildProjectileTowerEndLine(reason));   // [단계 C]
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileDetailOmitted() => _projectileDiag.DetailOmitted++;

        /// <summary>
        /// [진단] 서버 END 1줄을 만든다. 검증(fixture)도 같은 문자열 형식을 확인한다.
        /// </summary>
        /// <param name="reason">닫은 이유(game-end · map-clear · service-replaced · network-despawn · bootstrap-destroy 등).</param>
        public string BuildProjectileEndLine(string reason)
        {
            ProjectileDiagnosticCounters d = _projectileDiag;
            int cancelled = d.CancelledTotal;
            bool reserveBalanced = d.Reserved == d.Launched + cancelled + d.ClosedReserved;
            bool flightBalanced = d.Launched == d.Hit + d.TargetLost + d.ClosedInFlight;
            bool authority = !NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer;
            var text = new StringBuilder(900);
            text.Append("side=server v=1 role=").Append(ServerProjectileDiagnostics.RoleName)
                .Append(" authority=").Append(authority ? "local" : "remote")
                .Append(" epoch=").Append(ProjectileEpoch.ToString("x16", CultureInfo.InvariantCulture))
                .Append(" reason=").Append(string.IsNullOrEmpty(reason) ? "unknown" : reason)
                .Append(" switch=").Append(ServerProjectileDeliveryPolicy.ManifestToken)
                .Append(" reserved=").Append(d.Reserved)
                .Append(" reserveRefused=").Append(d.ReserveRefused)
                .Append(" launched=").Append(d.Launched)
                .Append(" launchedAtBuilding=").Append(d.LaunchedAtBuilding)
                .Append(" hit=").Append(d.Hit)
                .Append(" hitBuilding=").Append(d.HitBuilding)
                .Append(" targetLost=").Append(d.TargetLost)
                .Append(" writerNotApplied=").Append(d.WriterNotApplied)
                .Append(" cancelled=").Append(cancelled)
                .Append(" cancel=");
            for (int i = 0; i < ProjectileCancelCauses.Length; i++)
            {
                if (i > 0) text.Append('|');
                text.Append(ProjectileCancelCauses[i]).Append(':').Append(d.Cancels[i]);
            }
            text.Append(" closedReserved=").Append(d.ClosedReserved)
                .Append(" closedInFlight=").Append(d.ClosedInFlight)
                .Append(" flight=").Append(d.Flight.Format())
                .Append(" dist=").Append(d.Distance.Format())
                .Append(" launchLagMax=").Append(d.LaunchLagMax.ToString("F3", CultureInfo.InvariantCulture))
                .Append(" impactLagMax=").Append(d.ImpactLagMax.ToString("F3", CultureInfo.InvariantCulture))
                .Append(" attackerDead=hit:").Append(d.HitAfterAttackerDeath)
                .Append("|lost:").Append(d.LostAfterAttackerDeath)
                .Append(" attackerDeadGap=").Append(d.AttackerDeathGap.Format())
                .Append(" path=");
            bool first = true;
            for (int slot = 0; slot < d.ServerPathByType.Length; slot++)
            {
                if (d.ServerPathByType[slot] == 0 && d.LegacyPathByType[slot] == 0) continue;
                if (!first) text.Append('|');
                first = false;
                text.Append(slot).Append(":s").Append(d.ServerPathByType[slot]).Append("/l").Append(d.LegacyPathByType[slot]);
            }
            // [단계 C] 방어 타워 공격 경로(종족별) — 공격이 있었던 종족만 붙인다(유닛 칸과 같은 "0이면 생략" 규칙).
            for (int race = 0; race < d.TowerServerPathByRace.Length; race++)
            {
                if (d.TowerServerPathByRace[race] == 0 && d.TowerInstantPathByRace[race] == 0) continue;
                if (!first) text.Append('|');
                first = false;
                text.Append("tower-").Append(((RaceId)race).ToString())
                    .Append(":s").Append(d.TowerServerPathByRace[race]).Append("/l").Append(d.TowerInstantPathByRace[race]);
            }
            if (first) text.Append("none");
            text.Append(" detail=").Append(Math.Min(_projectileDiagnosticLines, ProjectileDiagnosticLimit))
                .Append('/').Append(ProjectileDiagnosticLimit)
                .Append(" detailOmitted=").Append(d.DetailOmitted)
                .Append(" balance=").Append(reserveBalanced && flightBalanced ? "ok" : "mismatch")
                .Append(" verdict=");
            // verdict는 "이 로그 범위에서 수가 맞는가"만 말한다. 화면 표현·육안 수용과는 별개다.
            if (!reserveBalanced || !flightBalanced) text.Append("FAIL");
            else if (d.WriterNotApplied > 0 || d.Cancels[Array.IndexOf(ProjectileCancelCauses, "other")] > 0
                     || d.Cancels[Array.IndexOf(ProjectileCancelCauses, "invalid-launch-geometry")] > 0)
                text.Append("ATTENTION");
            else text.Append("PASS");
            return text.ToString();
        }

        /// <summary>
        /// [진단 — 2026-10-21 단계 C] 건물 공격자(방어 타워) 합계 END 1줄을 만든다. 검증(fixture)도 같은 형식을 확인한다.
        /// 형식: "side=server-tower v=1 role=.. epoch=.. reason=.. byTower=launched:N|hit:N|targetLost:N|destroyedThenHit:N|destroyedThenLost:N
        ///        towerPath=Human:sN/lN|Spirit:sN/lN|Transcendence:sN/lN balance=ok|mismatch verdict=PASS|FAIL".
        /// balance = 타워가 쏜 발 수가 명중 + 대상 소실 + (아직 남은 비행)과 맞는가. 비행 중 닫힌 타워 발은 server 줄 closedInFlight에
        /// 함께 들어가 여기서 따로 셀 수 없으므로 "launched ≥ hit + targetLost"로만 본다(진단 전용 — 판정 무관).
        /// </summary>
        /// <param name="reason">닫은 이유(server 줄과 같은 값).</param>
        public string BuildProjectileTowerEndLine(string reason)
        {
            ProjectileDiagnosticCounters d = _projectileDiag;
            bool balanced = d.TowerLaunched >= d.TowerHit + d.TowerTargetLost
                && d.TowerHit >= d.TowerDestroyedThenHit && d.TowerTargetLost >= d.TowerDestroyedThenLost;
            var text = new StringBuilder(320);
            text.Append("side=server-tower v=1 role=").Append(ServerProjectileDiagnostics.RoleName)
                .Append(" epoch=").Append(ProjectileEpoch.ToString("x16", CultureInfo.InvariantCulture))
                .Append(" reason=").Append(string.IsNullOrEmpty(reason) ? "unknown" : reason)
                .Append(" byTower=launched:").Append(d.TowerLaunched)
                .Append("|hit:").Append(d.TowerHit)
                .Append("|targetLost:").Append(d.TowerTargetLost)
                .Append("|destroyedThenHit:").Append(d.TowerDestroyedThenHit)
                .Append("|destroyedThenLost:").Append(d.TowerDestroyedThenLost)
                .Append(" towerPath=");
            for (int race = 0; race < d.TowerServerPathByRace.Length; race++)
            {
                if (race > 0) text.Append('|');
                text.Append(((RaceId)race).ToString())
                    .Append(":s").Append(d.TowerServerPathByRace[race]).Append("/l").Append(d.TowerInstantPathByRace[race]);
            }
            text.Append(" balance=").Append(balanced ? "ok" : "mismatch")
                .Append(" verdict=").Append(balanced ? "PASS" : "FAIL");
            return text.ToString();
        }

        /// <summary>
        /// [진단 — 단계 C] 개별 로그의 type= 칸 값. 유닛 공격자는 예전과 같은 UnitType 숫자(로그 형식 무변경),
        /// 건물 공격자는 "tower-종족"(예: tower-Human)이다 — UnitType 숫자와 BuildingType 숫자가 겹쳐 오독되지 않게.
        /// </summary>
        private static string ProjectileAttackerTypeToken(ProjectileImpactDiagnosticSnapshot record)
            => record.AttackerIsUnit
                ? ((int)record.AttackerType).ToString(CultureInfo.InvariantCulture)
                : "tower-" + record.AttackerRace;
    }
}
