using System;
using System.Collections.Generic;
using Hexiege.Domain;
using UnityEngine;
using Hexiege.Application.Combat.Sequencing;
namespace Hexiege.Application
{
    public enum AttackEffectVisual : byte { MushroomImpact, Tower }
    public struct AttackEffectPresentation
    {
        public ulong Match, Lifetime, Sequence;
        public int AttackerId, TargetId;
        public AttackEffectVisual Visual;
        public RaceId Race;
        public Vector3 Source, Impact;
        public double ImpactTime;
        public int AppliedDamage, ResultHp;
        public AttackResultKey ResultKey;
        public bool IsValid => Match != 0 && Lifetime != 0 && Sequence != 0 && AttackerId >= 0 && TargetId >= 0
            && (Visual == AttackEffectVisual.MushroomImpact || Visual == AttackEffectVisual.Tower)
            && (Race == RaceId.Human || Race == RaceId.Spirit || Race == RaceId.Transcendence)
            && AppliedDamage >= 0 && ResultHp >= 0
            && !double.IsNaN(ImpactTime) && !double.IsInfinity(ImpactTime)
            && Finite(Source) && Finite(Impact);
        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
    // Match-local identity is presentation-only; never used to approve damage or gameplay work.
    public sealed class AttackEffectIdentity
    {
        private readonly ulong _match = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0) | 1UL;
        private readonly Dictionary<int, (IDamageable Owner, ulong Lifetime)> _owners = new();
        private ulong _lifetime, _sequence;
        private static int _logs;
        /// <summary>경기별 표현 큐 재초기화 시 상세 수집 예산을 새로 연다.</summary>
        public static void BeginLocalMatch() => _logs = 0;
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Record(string stage, AttackEffectPresentation s, string reason = "None", double now = double.NaN)
        {
            if (_logs++ >= 4096) { if (_logs == 4097) GameLog.Dev.Warn("Combat", nameof(AttackEffectIdentity), "Stage=DetailOverflow, Completeness=Unknown"); return; }
            string role = !NetworkContext.IsNetworkActive ? "Solo" : NetworkContext.IsNetworkServer ? "Server" : "Client";
            reason = (reason ?? "None").Replace('\r', ' ').Replace('\n', ' ').Replace(", ", ";");
            if (reason.Length > 160) reason = reason.Substring(0, 160) + "[truncated]";
            string line = System.FormattableString.Invariant($"Stage={stage}, Match={s.Match}, Lifetime={s.Lifetime}, Sequence={s.Sequence}, Attacker={s.AttackerId}, Target={s.TargetId}, Visual={s.Visual}, Race={s.Race}, ImpactTime={s.ImpactTime:G17}, Now={now:G17}, Applied={s.AppliedDamage}, HP={s.ResultHp}, ResultScope={s.ResultKey.AttackerInstanceId.Value}:{s.ResultKey.SequenceId.Value}:{s.ResultKey.HitIndex}:{s.ResultKey.VictimKind}:{s.ResultKey.VictimId}:{s.ResultKey.EffectKind}:{s.ResultKey.ResultOrdinal}, KeyKind={(s.ResultKey.IsValid ? "LinkedResult" : "LegacyIndependent")}, Role={role}, ClockDomain={(NetworkContext.IsNetworkActive ? "NGOServerTime" : "SoloScaled")}, Source={s.Source.x:G9}:{s.Source.y:G9}:{s.Source.z:G9}, Impact={s.Impact.x:G9}:{s.Impact.y:G9}:{s.Impact.z:G9}, Reason={reason}");
            if (stage == "Fault") GameLog.Dev.Warn("Combat", nameof(AttackEffectIdentity), line + "; GameplayContinues=True; Recovery=NextAttack; Completeness=Unknown");
            else GameLog.Dev.Info(stage == "Publish" || stage == "Receive" ? "Network" : "Combat", nameof(AttackEffectIdentity), line);
        }
        public static void End(int pending, int failures, int overflow, string reason, int keys)
        {
            string role = !NetworkContext.IsNetworkActive ? "Solo" : NetworkContext.IsNetworkServer ? "Server" : "Client";
            GameLog.Dev.Info("Combat", nameof(AttackEffectIdentity), $"Stage=END, Scope=LocalMatchQueueAggregate, Role={role}, DistinctKeys={keys}, PendingNotStarted={pending}, Failures={failures}, CapacityRejects={overflow}, DetailCount={_logs}, Completeness={(failures != 0 || overflow != 0 || _logs > 4096 || pending != 0 ? "Unknown" : "Bounded")}, Reason={reason}, VisualAcceptance=Unverified");
        }
        public AttackEffectPresentation Create(IDamageable attacker, IDamageable target, AttackEffectVisual visual,
            RaceId race, Vector3 source, Vector3 impact, double time)
        {
            if (!_owners.TryGetValue(attacker.Id, out var entry) || !ReferenceEquals(entry.Owner, attacker))
            {
                if (_owners.Count >= 1024 && !_owners.ContainsKey(attacker.Id))
                { GameLog.Dev.Warn("Combat", nameof(AttackEffectIdentity), "Stage=CapacityFailure, Limit=1024, Completeness=Unknown; GameplayContinues=True; Recovery=NextMatch"); return default; }
                entry = (attacker, ++_lifetime); _owners[attacker.Id] = entry;
            }
            return new AttackEffectPresentation { Match = _match, Lifetime = entry.Lifetime, Sequence = ++_sequence,
                AttackerId = attacker.Id, TargetId = target.Id, Visual = visual, Race = race,
                Source = source, Impact = impact, ImpactTime = time };
        }
    }
}
