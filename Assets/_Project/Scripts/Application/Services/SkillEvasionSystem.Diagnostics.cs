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
//                                (S-3부터 DiagRev=2 — 끝에 투사체 착탄 회피 수 · 비행 중 대상 사망 수 · Mushroom 제외 수 칸이 붙었다.)
//   [2026-11-23 단계 S-3 — 서버 권위 투사체(유닛 6종 · 인간 대포탑)]
//     EvadeRoll      Path=Projectile | TowerShell — 발사 순간 판정 1줄(KeySrc=ProjectileKey, 끝에 Projectile=epoch:id).
//                    투사체는 피해가 착탄 때라 이 줄의 HpBefore = HpAfter = 발사 순간 HP다(명중이어도 같다 — 피해는 아직 없다).
//     EvadeMissPoint (E-L5)  회피한 발사 1건 = 1줄 — 빗나간 지점(옆 거리 · 앞 거리 · 좌우) · 비행 시간.
//     EvadeImpact    (E-L6 · E-L7) 회피 투사체 착탄 1건 = 1줄 — Outcome=Evaded · WriterInvoked=0(피해 · 화상 없음).
//   [2026-11-23 단계 S-4 — 즉발 탑(정령 RuneSpire 번개 · 초월 VineTower)]
//     EvadeRoll      Path=TowerInstant — 공격 순간 판정 1줄(KeySrc=TowerOrdinal, Ordinal = 그 탑의 판정 순번, 끝에 Tower= · Race= · EffectSeq= ·
//                    WriterInvoked=). 명중이면 피해를 적용한 뒤 남기므로 HpAfter가 줄어 있고, 회피면 HpBefore = HpAfter다.
//     EvadeTowerInstant (E-L9 · E-L10) 회피한 즉발 탑 공격 1건 = 1줄 — 빗나간 지점(옆 · 앞 거리) · 피해 writer · 이벤트 미발행(Events=0).
//                    번개(RuneSpire)의 화면 상태는 화면 쪽 AttackEffectIdentity Play 줄의 「Endpoint=Untracked(Evaded)」로 본다.
//     요약(EvadeSummary)은 이미 있던 TowerInstantRolls · TowerInstantEvaded 칸이 센다(줄 모양 무변경 — DiagRev=2 그대로).
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

        // [2026-11-23 연막탄 S-3] S-2 서명 맨 뒤에 선택 인자 extra 하나만 붙였다. S-2 Hitscan 호출은 그대로이고 줄 모양도 같다.
        //   (2026-11-24 연막탄 테스트 PASS 뒤 주석으로 남겨 둔 옛 서명을 지웠다 — 실기 멀티에서 Hitscan · 투사체 · 대포탑 · 즉발 탑 판정 줄이 모두 이 서명으로 찍혔다.)
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
        /// <param name="extra">[연막탄 S-3] 줄 끝에 덧붙일 칸(예: 투사체 경로의 "Projectile=epoch:id"). null이면 S-2와 같은 줄.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagOnRoll(SkillEvasionPath path, SkillEvasionKeySource keySource, ulong attackKey, ulong ordinal,
                               ulong roll, int chanceBasisPoints, bool evaded, int attackerId, string attackerType,
                               TeamId attackerTeam, UnitData target, int hpBefore, int hpAfter, string extra = null)
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
                // [S-3] S-2 줄과 같은 칸 · 같은 순서이고, extra가 있을 때만 끝에 ", " + extra가 붙는다.
                //   (2026-11-24 연막탄 테스트 PASS 뒤 주석으로 남겨 둔 S-2 옛 줄을 지웠다 — 아래 줄이 실기 멀티 로그에 경로별로 찍혔다.)
                string tail = string.IsNullOrEmpty(extra) ? string.Empty : ", " + extra;
                GameLog.Dev.Info("Skill", nameof(SkillEvasionSystem), "[SKILL-EVADE] EvadeRoll",
                    System.FormattableString.Invariant(
                        $"Diag=SkillEvade, DiagRev=1, Event=EvadeRoll, Role={DiagRole()}, Path={path}, KeySrc={keySource}, Key={Hex(attackKey)}, Ordinal={ordinal}, Seed={Hex(_matchSeed)}, RollHex={Hex(roll)}, Roll={SkillEvasionRoll.RollBucket(roll)}, Chance={chanceBasisPoints / 10}, ChanceBp={chanceBasisPoints}, Evaded={(evaded ? 1 : 0)}, AttackerId={attackerId}, AttackerType={attackerType}, AttackerTeam={attackerTeam}, TargetId={(target != null ? target.Id : -1)}, TargetType={(target != null ? target.Type.ToString() : "None")}, TargetTeam={(target != null ? target.Team.ToString() : "None")}, HpBefore={hpBefore}, HpAfter={hpAfter}, Detail={_diagLines}/{DiagnosticLineLimit}") + tail);
            }
            catch { /* 진단 전용 — 서버 피해 판정을 막지 않는다 */ }
        }

        // ── [2026-11-23 연막탄 단계 S-3] 서버 권위 투사체 회피 진단(E-L5 · E-L6 · E-L7 · E-L8 · E-L11) ─────────────────

        // 착탄 때 「회피」로 닫힌 투사체 수(경로별 — Projectile · TowerShell), 그중 비행 중 대상이 죽은 수(O-6 관찰용).
        private readonly int[] _diagImpactEvaded = new int[5];
        private int _diagImpactTargetGone;
        // 연막 안 대상에게 쐈지만 회피 대상 공격자가 아니라 판정하지 않은 MushroomBomber 발사 수(E-L11 — 제외가 실제로 일어났다는 증거).
        private int _diagMushroomInSmokeSkipped;

        /// <summary>
        /// [진단 E-L5 — 연막탄 S-3] 회피한 투사체의 빗나간 지점 1줄(발사 순간, 상한 96 안에서). 판정 줄(EvadeRoll) 바로 뒤에 남는다.
        /// 정상 = SideDist ≈ 반경 + 0.3, ForwardDist ≈ 0.5, Flight = 거리 ÷ 속도.
        /// </summary>
        /// <param name="path">Projectile(유닛) 또는 TowerShell(인간 대포탑).</param>
        /// <param name="projectile">투사체 번호표 문자열(epoch:id — [PROJECTILE] 줄과 짝을 맞춘다).</param>
        /// <param name="source">발사 위치(도메인).</param>
        /// <param name="center">발사 순간 대상 중심(도메인).</param>
        /// <param name="radius">대상 반경.</param>
        /// <param name="missPoint">빗나간 지점(도메인).</param>
        /// <param name="left">대상의 왼쪽으로 빗나갔는가(쏜 쪽에서 대상을 볼 때).</param>
        /// <param name="flightSeconds">발사 → 착탄 시간(초).</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagOnMissPoint(SkillEvasionPath path, string projectile, UnityEngine.Vector3 source, UnityEngine.Vector3 center,
                                    float radius, UnityEngine.Vector3 missPoint, bool left, double flightSeconds)
        {
            try
            {
                if (!DiagTakeLine()) return;
                // 진행 방향 d(쏜 쪽 → 대상, XZ 단위벡터)로 빗나간 지점을 "옆 거리 · 앞 거리"로 다시 나눠 적는다(식이 맞는지 로그로 확인).
                float dx = center.x - source.x, dz = center.z - source.z;
                float len = UnityEngine.Mathf.Sqrt(dx * dx + dz * dz);
                if (!(len > 0.0001f)) { dx = 0f; dz = 1f; len = 1f; }
                dx /= len; dz /= len;
                float mx = missPoint.x - center.x, mz = missPoint.z - center.z;
                float forward = mx * dx + mz * dz;
                float side = UnityEngine.Mathf.Abs(-mx * dz + mz * dx);
                GameLog.Dev.Info("Skill", nameof(SkillEvasionSystem), "[SKILL-EVADE] EvadeMissPoint",
                    System.FormattableString.Invariant(
                        $"Diag=SkillEvade, DiagRev=1, Event=EvadeMissPoint, Role={DiagRole()}, Path={path}, Projectile={projectile}, Side={(left ? "Left" : "Right")}, SideDist={side:F3}, ForwardDist={forward:F3}, Radius={radius:F3}, ExpectSide={radius + SkillEvasionRoll.MissSideExtra:F3}, ExpectForward={SkillEvasionRoll.MissForwardExtra:F3}, Center=({center.x:F3},{center.z:F3}), Miss=({missPoint.x:F3},{missPoint.z:F3}), Source=({source.x:F3},{source.z:F3}), Flight={flightSeconds:F3}, Detail={_diagLines}/{DiagnosticLineLimit}"));
            }
            catch { /* 진단 전용 */ }
        }

        /// <summary>
        /// [진단 E-L6 · E-L7 — 연막탄 S-3] 회피한 투사체가 착탄 시각에 「회피」로 닫혔다 — 피해 writer를 부르지 않았다(WriterInvoked=0).
        /// 1건 = 1줄(상한 96 안에서), 수는 경기 끝 요약에 모두 센다. InfernoSpirit도 writer를 안 부르므로 화상이 걸리지 않는다.
        /// </summary>
        /// <param name="path">Projectile(유닛) 또는 TowerShell(인간 대포탑).</param>
        /// <param name="projectile">투사체 번호표 문자열(epoch:id).</param>
        /// <param name="attackerType">공격자 종류 표기.</param>
        /// <param name="targetId">원래 대상 유닛 Id.</param>
        /// <param name="targetAlive">착탄 순간 원래 대상이 아직 살아 있었는가(false = 비행 중 사망 — 결과는 그래도 Evaded, O-6).</param>
        /// <param name="hpNow">착탄 순간 대상 HP(살아 있지 않으면 -1).</param>
        /// <param name="missPoint">착탄 지점 = 빗나간 지점(도메인).</param>
        /// <param name="impactTime">서버 착탄 시각.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagOnProjectileImpact(SkillEvasionPath path, string projectile, string attackerType, int targetId,
                                           bool targetAlive, int hpNow, UnityEngine.Vector3 missPoint, double impactTime)
        {
            try
            {
                int p = (int)path;
                if (p >= 0 && p < _diagImpactEvaded.Length) _diagImpactEvaded[p]++;
                if (!targetAlive) _diagImpactTargetGone++;
                if (!DiagTakeLine()) return;
                GameLog.Dev.Info("Skill", nameof(SkillEvasionSystem), "[SKILL-EVADE] EvadeImpact",
                    System.FormattableString.Invariant(
                        $"Diag=SkillEvade, DiagRev=1, Event=EvadeImpact, Role={DiagRole()}, Path={path}, Projectile={projectile}, AttackerType={attackerType}, TargetId={targetId}, TargetAlive={(targetAlive ? 1 : 0)}, HpNow={hpNow}, Outcome=Evaded, WriterInvoked=0, Applied=0, Miss=({missPoint.x:F3},{missPoint.z:F3}), ImpactTime={impactTime:G17}, Detail={_diagLines}/{DiagnosticLineLimit}"));
            }
            catch { /* 진단 전용 */ }
        }

        /// <summary>
        /// [진단 E-L11 — 연막탄 S-3] 연막 안 대상에게 쐈지만 회피 대상이 아닌 투사체(MushroomBomber — 범위 폭발이라 제외)를 센다. 줄은 남기지 않는다.
        /// </summary>
        /// <param name="attackerType">쏜 유닛 종류.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagNoteProjectileNotEvadable(UnitType attackerType)
        {
            if (attackerType == UnitType.MushroomBomber) _diagMushroomInSmokeSkipped++;
        }

        // ── [2026-11-23 연막탄 단계 S-4] 즉발 탑 회피 진단(E-L9 · E-L10) ─────────────────────────────────────

        /// <summary>
        /// [진단 E-L9 · E-L10 — 연막탄 S-4] 즉발 탑(RuneSpire · VineTower)의 공격이 연막 회피로 빗나갔다 — 1건 = 1줄(상한 96 안에서).
        /// 판정 줄(EvadeRoll Path=TowerInstant) 바로 뒤에 남는다. 정상 = SideDist ≈ 반경 + 0.3 · ForwardDist ≈ 0.5 · HpBefore = HpNow ·
        /// Writer=NotInvoked · Events=0(공격받음 · 피해 이벤트를 내지 않았다 → 피해 숫자 원천이 없다).
        /// </summary>
        /// <param name="towerId">쏜 탑 Id.</param>
        /// <param name="race">탑 종족(Spirit = 번개 · Transcendence = 덩굴).</param>
        /// <param name="ordinal">그 탑의 회피 판정 순번(열쇠 재료).</param>
        /// <param name="effectSequence">표현 스냅샷 순번(화면 쪽 AttackEffectIdentity 줄의 Sequence=와 짝). 스냅샷을 못 만들었으면 0.</param>
        /// <param name="target">원래 대상 유닛.</param>
        /// <param name="source">탑 위치(도메인).</param>
        /// <param name="center">공격 순간 대상 위치(도메인 — 발밑).</param>
        /// <param name="radius">대상 반경(빗나간 지점 계산에 쓴 값).</param>
        /// <param name="missPoint">빗나간 지점(도메인).</param>
        /// <param name="left">대상의 왼쪽으로 빗나갔는가(탑에서 대상을 볼 때).</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagOnTowerInstantEvade(int towerId, RaceId race, ulong ordinal, ulong effectSequence, UnitData target,
                                            UnityEngine.Vector3 source, UnityEngine.Vector3 center, float radius,
                                            UnityEngine.Vector3 missPoint, bool left)
        {
            try
            {
                if (!DiagTakeLine()) return;
                // 진행 방향 d(탑 → 대상, XZ 단위벡터)로 빗나간 지점을 "옆 거리 · 앞 거리"로 다시 나눠 적는다(DiagOnMissPoint와 같은 식).
                float dx = center.x - source.x, dz = center.z - source.z;
                float len = UnityEngine.Mathf.Sqrt(dx * dx + dz * dz);
                if (!(len > 0.0001f)) { dx = 0f; dz = 1f; len = 1f; }
                dx /= len; dz /= len;
                float mx = missPoint.x - center.x, mz = missPoint.z - center.z;
                float forward = mx * dx + mz * dz;
                float side = UnityEngine.Mathf.Abs(-mx * dz + mz * dx);
                GameLog.Dev.Info("Skill", nameof(SkillEvasionSystem), "[SKILL-EVADE] EvadeTowerInstant",
                    System.FormattableString.Invariant(
                        $"Diag=SkillEvade, DiagRev=1, Event=EvadeTowerInstant, Role={DiagRole()}, Path=TowerInstant, Tower={towerId}, Race={race}, Ordinal={ordinal}, EffectSeq={effectSequence}, TargetId={(target != null ? target.Id : -1)}, TargetType={(target != null ? target.Type.ToString() : "None")}, HpNow={(target != null ? target.Hp : -1)}, Writer=NotInvoked, Events=0, Side={(left ? "Left" : "Right")}, SideDist={side:F3}, ForwardDist={forward:F3}, Radius={radius:F3}, ExpectSide={radius + SkillEvasionRoll.MissSideExtra:F3}, ExpectForward={SkillEvasionRoll.MissForwardExtra:F3}, Center=({center.x:F3},{center.z:F3}), Miss=({missPoint.x:F3},{missPoint.z:F3}), Source=({source.x:F3},{source.z:F3}), Detail={_diagLines}/{DiagnosticLineLimit}"));
            }
            catch { /* 진단 전용 — 서버 탑 공격 처리를 막지 않는다 */ }
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
                // [2026-11-23 연막탄 S-3] S-2 줄(DiagRev=1)의 칸은 이름 · 순서 그대로 두고, 끝에 투사체 착탄 칸 넷을 붙였다
                //   → 줄 모양이 바뀌었으므로 DiagRev=2.
                //   ProjectileImpactEvaded / TowerShellImpactEvaded = 착탄 때 「회피」로 닫힌 수(정상 = 같은 경로의 …Evaded 수 — 경기 끝에
                //   아직 날고 있던 투사체는 착탄 전에 닫혀 1~2 적을 수 있다), ImpactTargetGone = 그중 비행 중 대상이 죽은 수(O-6, 결과는 그래도 Evaded),
                //   MushroomInSmokeSkipped = 연막 안 대상에게 쐈지만 회피 대상이 아니라 판정하지 않은 MushroomBomber 발사 수(E-L11).
                //   (2026-11-24 연막탄 테스트 PASS 뒤 주석으로 남겨 둔 DiagRev=1 옛 줄을 지웠다 — 아래 DiagRev=2 줄이 실기 멀티 경기 끝마다 찍혔다
                //    (MushroomInSmokeSkipped · ImpactTargetGone 값 포함).)
                GameLog.Dev.Info("Skill", nameof(SkillEvasionSystem), "[SKILL-EVADE] EvadeSummary",
                    System.FormattableString.Invariant(
                        $"Diag=SkillEvade, DiagRev=2, Event=EvadeSummary, Role={DiagRole()}, Seed={Hex(_matchSeed)}, Rolls={totalRolls}, Evaded={totalEvaded}, RatePermille={ratePermille}, HitscanRolls={_diagRolls[(int)SkillEvasionPath.Hitscan]}, HitscanEvaded={_diagEvaded[(int)SkillEvasionPath.Hitscan]}, ProjectileRolls={_diagRolls[(int)SkillEvasionPath.Projectile]}, ProjectileEvaded={_diagEvaded[(int)SkillEvasionPath.Projectile]}, TowerShellRolls={_diagRolls[(int)SkillEvasionPath.TowerShell]}, TowerShellEvaded={_diagEvaded[(int)SkillEvasionPath.TowerShell]}, TowerInstantRolls={_diagRolls[(int)SkillEvasionPath.TowerInstant]}, TowerInstantEvaded={_diagEvaded[(int)SkillEvasionPath.TowerInstant]}, C2Evaded={_diagC2Evaded}, C2Other={_diagC2Other}, DetailLines={_diagLines}, Suppressed={_diagSuppressed}, ProjectileImpactEvaded={_diagImpactEvaded[(int)SkillEvasionPath.Projectile]}, TowerShellImpactEvaded={_diagImpactEvaded[(int)SkillEvasionPath.TowerShell]}, ImpactTargetGone={_diagImpactTargetGone}, MushroomInSmokeSkipped={_diagMushroomInSmokeSkipped}"));
            }
            catch { /* 진단 전용 */ }
        }
    }
}
