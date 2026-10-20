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
        }

        private static ProjectileImpactDiagnosticSnapshot CaptureProjectileImpactDiagnostics(ProjectileRecord record)
        {
            return new ProjectileImpactDiagnosticSnapshot
            {
                Id = record.Id, AttackerId = record.Attacker != null ? record.Attacker.Id : -1,
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
            if (!record.AttackerDeadObserved) return;

            // 발사 뒤 공격자가 죽은 발 — 착탄 결과와 사망~착탄 간격을 남긴다(표본 16줄 + END 합계).
            double gap = record.ImpactTime - record.AttackerDeadAt;
            if (hit) _projectileDiag.HitAfterAttackerDeath++;
            else _projectileDiag.LostAfterAttackerDeath++;
            _projectileDiag.AttackerDeathGap.Add(gap);
            if (_projectileDiag.AttackerDeathSamples >= ProjectileAttackerDeathSampleLimit) return;
            _projectileDiag.AttackerDeathSamples++;
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE] attacker-dead-before-impact",
                FormattableString.Invariant($"epoch={ProjectileEpoch:x16}, id={record.Id}, attacker={record.AttackerId}, type={(int)record.AttackerType}, target={record.TargetId}, targetIsUnit={record.TargetIsUnit}, launch={record.LaunchTime:G17}, attackerDeadSeenAt={record.AttackerDeadAt:G17}, impact={record.ImpactTime:G17}, gap={gap:F3}, outcome={(hit ? "hit" : "target-lost")}, writer={writerStatus}, applied={applied}, hp={resultingHp}, sample={_projectileDiag.AttackerDeathSamples}/{ProjectileAttackerDeathSampleLimit}"));
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
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileFlushPendingEnd()
        {
            if (_projectileDiag.PendingEndReason == null || _projectileDiag.EndWritten) return;
            string reason = _projectileDiag.PendingEndReason;
            _projectileDiag.PendingEndReason = null;
            _projectileDiag.EndWritten = true;
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[PROJECTILE-END]", BuildProjectileEndLine(reason));
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
    }
}
