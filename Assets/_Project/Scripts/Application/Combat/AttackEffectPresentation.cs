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
        /// <summary>
        /// [2026-11-23 연막탄 단계 S-4 — Plan P-8] 서버가 「연막 회피」로 정한 즉발 탑 공격인가(정령 RuneSpire 번개 · 초월 VineTower).
        /// true면 피해가 없었다(AppliedDamage = 0 · 피해 이벤트 없음 → 피해 숫자 없음)이고, <see cref="Impact"/>는 대상 발밑이 아니라
        /// 서버가 공격 순간 고정한 <b>빗나간 지점</b>(대상 옆 반경 + 0.3 · 탑 반대쪽으로 0.5)이다 — 빗나간 지점 칸을 따로 두지 않고
        /// Impact 한 칸에 싣는다(같은 뜻의 칸이 둘이면 어긋날 수 있다).
        /// 화면: 번개는 대상을 따라가지 않고 이 지점을 끝점으로 고정해 그린다(대상 옆으로 빗나간 번개). VineTower는 공격 이펙트가 원래 없다.
        /// 기본값 false — 옛 스냅샷(MushroomImpact · 명중 탑 공격)은 모두 false라 동작이 같다.
        /// </summary>
        public bool Evaded;
        // [2026-11-23 연막탄 S-4] 그전 검사식 끝에 「회피면 탑 스냅샷이고 적용 피해가 0이어야 한다」 한 조건만 덧붙였다
        //   (회피가 아니면 그전 식과 결과가 같다). 네트워크로 받은 스냅샷도 이 검사를 지나야 화면에 쓴다.
        //   (2026-11-24 연막탄 테스트 PASS 뒤 주석으로 남겨 둔 옛 검사식을 지웠다 — 실기 멀티에서 RuneSpire 회피 스냅샷이 이 검사를 지나
        //    Host에서 보내지고 Client에서 받아졌다(AttackEffectIdentity Publish · Receive 줄 Reason=SmokeEvaded).)
        public bool IsValid => Match != 0 && Lifetime != 0 && Sequence != 0 && AttackerId >= 0 && TargetId >= 0
            && (Visual == AttackEffectVisual.MushroomImpact || Visual == AttackEffectVisual.Tower)
            && (Race == RaceId.Human || Race == RaceId.Spirit || Race == RaceId.Transcendence)
            && AppliedDamage >= 0 && ResultHp >= 0
            && !double.IsNaN(ImpactTime) && !double.IsInfinity(ImpactTime)
            && Finite(Source) && Finite(Impact)
            && (!Evaded || (Visual == AttackEffectVisual.Tower && AppliedDamage == 0));
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
