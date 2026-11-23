// ============================================================================
// SkillEvasionSystem.Diagnostics.cs
// 연막탄 회피 판정 진단 로그 [SKILL-EVADE] (Diag=SkillEvade, DiagRev=1 — 개발 축, 사용자 결정 U-7).
// 2026-11-23 연막탄 Task 단계 S-1 · S-2 — Plan 6절 「테스트 항목 ↔ 진단 로그」 E-L3 · E-L4 · E-L11 · E-L12 · E-L13.
//
// 무엇을 하나(초급자용 설명):
//   "총잡이의 총알이 정말 50% 확률로 빗나갔나", "빗나간 공격에 피해가 정말 없었나"는 눈으로 확인하기 어렵다
//   (총잡이는 날아가는 그림이 원래 없어서 숫자가 안 뜨는 것만 보인다). 그래서 판정할 때마다 로그 한 줄을 남긴다.
//     EvadeRoll     (E-L3 · E-L13) 판정 1건 = 1줄 — 경로 · 열쇠 · seed · 확률 · 눈금(Roll) · 결과 · HP 전후.
//                                처음 96건까지만 줄을 남기고(스팸 방지 — 투사체 진단 상한 96 선례) 그 뒤는 개수만 센다.
//     EvadeResolved (E-L4)         멀티 서버에서 회피가 C2 결과 「회피(Evaded)」로 닫혔는가 — 같은 상한(96) 안에서 1건 = 1줄.
//     EvadeSummary  (E-L11 · E-L12) 경기 끝 1줄 — 경로별 판정 수 · 회피 수 · 회피 비율 · 생략 줄 수 · seed.
//
// 지키는 것:
//   - 모든 메서드에 [Conditional] 두 개(UNITY_EDITOR · DEVELOPMENT_BUILD) — 릴리스 빌드에서는 호출 · 문자열 조립이 통째로 사라진다
//     (LogRules 1.7). 진단은 판정(roll · 회피 여부 · HP)을 절대 바꾸지 않는다(읽기만).
//   - 서버 피해 경로 안에서 돌므로 예외를 밖으로 던지지 않는다(각 메서드가 삼킨다).
//   - 매 틱 줄 금지(LogRules 1.14-8) — 판정은 "공격 한 번"마다 일어나는 사건이라 1건 1줄이고, 상한을 넘으면 세기만 한다.
//   - 확률은 밀리(500 = 50%)와 만분율(ChanceBp=5000) 둘 다, 눈금 Roll은 0~9999, 열쇠 · seed · roll은 16자리 16진수.
//
// Application 레이어 — NetworkContext(정적 홀더)로 역할만 읽는다. Unity.Netcode 직접 참조 없음.
// ============================================================================

using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public sealed partial class SkillEvasionSystem
    {
        /// <summary> 판정 · 결과 상세 줄 상한(경기당). 투사체 진단 상한(ProjectileDiagnosticLimit = 96)과 같은 값. </summary>
        public const int DiagnosticLineLimit = 96;

        // 경로별(SkillEvasionPath 정수 0~4) 판정 수 · 회피 수 — 경기 끝 요약용(개발 빌드에서만 늘어난다).
        private readonly int[] _diagRolls = new int[5];
        private readonly int[] _diagEvaded = new int[5];
        private int _diagLines;       // 지금까지 남긴 상세 줄 수(EvadeRoll + EvadeResolved)
        private int _diagSuppressed;  // 상한을 넘어 줄을 남기지 않은 건수
        private int _diagC2Evaded;    // 멀티 C2가 「회피」로 닫힌 수
        private int _diagC2Other;     // 회피했는데 C2가 다른 결과 · 거절로 닫힌 수(정상이면 0)

        private static string DiagRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        private static string Hex(ulong value) => value.ToString("x16");

        // 상세 줄을 하나 더 남겨도 되는가(상한 안이면 true + 줄 수 증가, 넘으면 생략 수만 증가).
        private bool DiagTakeLine()
        {
            if (_diagLines >= DiagnosticLineLimit) { _diagSuppressed++; return false; }
            _diagLines++;
            return true;
        }

        /// <summary>
        /// [진단 E-L3 · E-L13] 회피 판정 1건 = 1줄(상한 96 안에서). 명중이면 피해를 적용한 뒤 HpAfter를 넣어 부른다.
        /// </summary>
        /// <param name="path">공격 경로(Hitscan · Projectile · TowerShell · TowerInstant).</param>
        /// <param name="keySource">열쇠를 무엇으로 만들었나(ResultKey · AttackerOrdinal · ProjectileKey).</param>
        /// <param name="attackKey">공격 열쇠.</param>
        /// <param name="ordinal">싱글 대체 열쇠의 순번(그 밖이면 0).</param>
        /// <param name="roll">판정에 쓴 64비트 roll.</param>
        /// <param name="chanceBasisPoints">확률(만분율).</param>
        /// <param name="evaded">회피했는가.</param>
        /// <param name="attackerId">공격자 Id(유닛 · 탑).</param>
        /// <param name="attackerType">공격자 종류 이름(유닛 종류 또는 탑 표기).</param>
        /// <param name="attackerTeam">공격자 팀.</param>
        /// <param name="target">맞을 유닛.</param>
        /// <param name="hpBefore">판정 직전 HP.</param>
        /// <param name="hpAfter">판정 · 피해 뒤 HP(회피면 hpBefore와 같다).</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagOnRoll(SkillEvasionPath path, SkillEvasionKeySource keySource, ulong attackKey, ulong ordinal,
                               ulong roll, int chanceBasisPoints, bool evaded, int attackerId, string attackerType,
                               TeamId attackerTeam, UnitData target, int hpBefore, int hpAfter)
        {
            try
            {
                int p = (int)path;
                if (p >= 0 && p < _diagRolls.Length)
                {
                    _diagRolls[p]++;
                    if (evaded) _diagEvaded[p]++;
                }
                if (!DiagTakeLine()) return;
                GameLog.Dev.Info("Skill", nameof(SkillEvasionSystem), "[SKILL-EVADE] EvadeRoll",
                    System.FormattableString.Invariant(
                        $"Diag=SkillEvade, DiagRev=1, Event=EvadeRoll, Role={DiagRole()}, Path={path}, KeySrc={keySource}, Key={Hex(attackKey)}, Ordinal={ordinal}, Seed={Hex(_matchSeed)}, RollHex={Hex(roll)}, Roll={SkillEvasionRoll.RollBucket(roll)}, Chance={chanceBasisPoints / 10}, ChanceBp={chanceBasisPoints}, Evaded={(evaded ? 1 : 0)}, AttackerId={attackerId}, AttackerType={attackerType}, AttackerTeam={attackerTeam}, TargetId={(target != null ? target.Id : -1)}, TargetType={(target != null ? target.Type.ToString() : "None")}, TargetTeam={(target != null ? target.Team.ToString() : "None")}, HpBefore={hpBefore}, HpAfter={hpAfter}, Detail={_diagLines}/{DiagnosticLineLimit}"));
            }
            catch { /* 진단 전용 — 서버 피해 판정을 막지 않는다 */ }
        }

        /// <summary>
        /// [진단 E-L4] 멀티 서버 — 회피한 Hitscan 타격이 C2(서버 권위 공격 결과)에서 어떤 결과로 닫혔는가. 정상 = Outcome=Evaded · Completion=Accepted.
        /// </summary>
        /// <param name="attackerId">공격자 유닛 Id.</param>
        /// <param name="hasResult">C2 결과가 만들어졌는가(거절 · 불일치면 false).</param>
        /// <param name="outcome">C2 결과 종류(없으면 None).</param>
        /// <param name="completion">C2 순서기 처리 결과.</param>
        /// <param name="reason">C2 코디네이터의 이유 문자열.</param>
        /// <param name="key">C2 결과 키(없으면 default).</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagOnResolved(int attackerId, bool hasResult, AttackImpactOutcome outcome,
                                   UnitActionReducerStatus completion, string reason, AttackResultKey key)
        {
            try
            {
                bool ok = hasResult && outcome == AttackImpactOutcome.Evaded && completion == UnitActionReducerStatus.Accepted;
                if (ok) _diagC2Evaded++; else _diagC2Other++;
                // 정상이 아닌 결과는 상한과 무관하게 남긴다(놓치면 안 되는 이상 — 정상이면 0건이라 스팸이 아니다).
                if (ok && !DiagTakeLine()) return;
                string message = ok ? "[SKILL-EVADE] EvadeResolved" : "[SKILL-EVADE] EvadeResolved — C2가 회피로 닫히지 않음";
                string payload = System.FormattableString.Invariant(
                    $"Diag=SkillEvade, DiagRev=1, Event=EvadeResolved, Role={DiagRole()}, AttackerId={attackerId}, HasResult={hasResult}, Outcome={outcome}, Completion={completion}, Reason={(string.IsNullOrEmpty(reason) ? "None" : reason)}, Seq={key.SequenceId.Value}, Hit={key.HitIndex}, VictimId={key.VictimId}, Verdict={(ok ? "PASS" : "FAIL")}");
                if (ok) GameLog.Dev.Info("Skill", nameof(SkillEvasionSystem), message, payload);
                else GameLog.Dev.Warn("Skill", nameof(SkillEvasionSystem), message, payload);
            }
            catch { /* 진단 전용 */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagOnClose()
        {
            try
            {
                // 순수 Client는 판정을 하지 않으므로(서버 권위) 요약을 남기지 않는다 — 0만 찍힌 줄이 Client 로그를 채우지 않게.
                if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
                int totalRolls = 0, totalEvaded = 0;
                for (int i = 0; i < _diagRolls.Length; i++) { totalRolls += _diagRolls[i]; totalEvaded += _diagEvaded[i]; }
                int ratePermille = totalRolls > 0 ? (int)System.Math.Round(totalEvaded * 1000.0 / totalRolls) : -1;
                GameLog.Dev.Info("Skill", nameof(SkillEvasionSystem), "[SKILL-EVADE] EvadeSummary",
                    System.FormattableString.Invariant(
                        $"Diag=SkillEvade, DiagRev=1, Event=EvadeSummary, Role={DiagRole()}, Seed={Hex(_matchSeed)}, Rolls={totalRolls}, Evaded={totalEvaded}, RatePermille={ratePermille}, HitscanRolls={_diagRolls[(int)SkillEvasionPath.Hitscan]}, HitscanEvaded={_diagEvaded[(int)SkillEvasionPath.Hitscan]}, ProjectileRolls={_diagRolls[(int)SkillEvasionPath.Projectile]}, ProjectileEvaded={_diagEvaded[(int)SkillEvasionPath.Projectile]}, TowerShellRolls={_diagRolls[(int)SkillEvasionPath.TowerShell]}, TowerShellEvaded={_diagEvaded[(int)SkillEvasionPath.TowerShell]}, TowerInstantRolls={_diagRolls[(int)SkillEvasionPath.TowerInstant]}, TowerInstantEvaded={_diagEvaded[(int)SkillEvasionPath.TowerInstant]}, C2Evaded={_diagC2Evaded}, C2Other={_diagC2Other}, DetailLines={_diagLines}, Suppressed={_diagSuppressed}"));
            }
            catch { /* 진단 전용 */ }
        }
    }
}
