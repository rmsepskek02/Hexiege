// ============================================================================
// UnitCombatUseCase.Evasion.cs
// 연막탄 「회피」 관문 — 즉발 총탄(Hitscan: Pistoleer · Assault · Sniper)이 연막 안의 아군 유닛을 맞히기 직전에 서버가 회피를 판정한다.
// 2026-11-23 지원 스킬 연막탄 Task(_Tasks/2026-11-23/15_28_support-skill-smoke-bomb) 단계 S-2(Plan P-3 · P-4 · 메인 기본안 M-1 · M-2).
//
// 무엇을 하나(유니티 초급자용 설명):
//   총잡이의 총알은 날아가는 그림 없이 "타격 순간"에 바로 맞는다. 그 타격은 피해 writer(ApplyAttackDamageCore) 한 곳을 지난다.
//   이 파일의 관문은 writer 안, "대상이 아직 살아 있는지 확인한 뒤 · 실제 피해(ExecuteAttack)를 주기 전"에 한 번 묻는다.
//     ① 공격자가 총잡이 3종인가? ② 맞을 것이 유닛인가(건물은 연막 혜택 없음)? ③ 지금 투사체 착탄 처리 중이 아닌가?
//     ④ 그 유닛이 연막(아군 구역) 안이라 회피 확률이 0보다 큰가?
//   넷 다 맞으면 주사위(결정적 해시)를 굴리고, 피했으면 ExecuteAttack을 부르지 않는다.
//     → HP · 피해 이벤트(OnEntityDamaged) · 피해 숫자 · 특수 효과가 전부 없다(숫자는 피해 이벤트가 없으니 저절로 생략된다).
//     → writer는 「회피(AttackDamageApplyStatus.Evaded)」를 돌려주고, 멀티 C2 코디네이터가 그것을 결과 「회피」로 닫는다.
//
// 왜 피해 공통 헬퍼(IncomingDamage.Apply) 안이 아니라 여기인가(Plan P-3 · 위험 R-4):
//   헬퍼 시점에는 특수 핸들러(화상 · 독 · 폭발)가 이미 돌 준비가 끝났고, 같은 헬퍼를 스플래시 · 지속 피해 · 스킬도 지난다.
//   거기서 끊으면 화상 · 피해 이벤트가 새고, 멀티 C2는 「승인 명중인데 writer가 적용 안 함」을 불일치로 거절한다.
//
// 이중 판정 방지(Plan R-6 · E-T6): 서버 권위 투사체의 착탄도 같은 writer를 지난다. 투사체는 발사 순간에 따로 판정하므로(단계 S-3)
//   착탄 처리 중(ProjectileImpactInProgress)이면 이 관문은 아예 굴리지 않는다. 투사체 유닛은 총잡이 3종이 아니라 ①에서도 걸러지지만,
//   두 조건을 함께 두어 한쪽이 바뀌어도 이중 판정이 생기지 않게 했다.
//
// 공격 열쇠(O-10 해결안):
//   멀티 = C2 정규 결과 키(presentationResultKey — 유효할 때). 싱글 = 결과 키가 없다(default) → (공격자 Id, 그 공격자의 회피 판정 순번).
//
// 회귀 안전(핵심): 회피 서비스가 없거나(미주입) · 연막 구성원이 없으면(평상시) 확률 0 → 판정 자체를 하지 않는다
//   → 기존 writer와 한 줄도 다르지 않은 경로로 ExecuteAttack까지 간다.
//
// Application 레이어 — Domain · Application 의존(UnityEngine 참조 없음).
// ============================================================================

using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public partial class UnitCombatUseCase
    {
        // 연막탄 회피 판정 서비스(경기 seed + 연막 확률 조회). GameBootstrapper가 SetSkillEvasionSystem으로 주입한다.
        //   미주입(null)이면 판정이 없다 → 기존 전투와 완전히 같다(하위호환).
        private SkillEvasionSystem _skillEvasion;

        /// <summary>
        /// 연막탄 회피 판정 서비스를 주입한다. GameBootstrapper(조합 루트)에서 맵마다 1회 호출. 생성자는 바꾸지 않는다(Editor fixture 보호).
        /// 미주입(null)이면 회피 판정이 없다 → 기존 전투와 완전히 동일.
        /// </summary>
        /// <param name="evasion">같은 경기의 회피 판정 서비스.</param>
        public void SetSkillEvasionSystem(SkillEvasionSystem evasion)
        {
            _skillEvasion = evasion;
        }

        /// <summary> 지금 주입된 회피 판정 서비스(검증 · 진단용). </summary>
        public SkillEvasionSystem SkillEvasion => _skillEvasion;

        // Hitscan 관문 한 번의 판정 기록(진단 줄을 피해 적용 뒤에 남기려고 들고 다닌다).
        private struct HitscanEvadeGate
        {
            public SkillEvasionKeySource KeySource; // 열쇠를 무엇으로 만들었나
            public ulong Key;                       // 공격 열쇠
            public ulong Ordinal;                   // 싱글 대체 열쇠의 순번(멀티면 0)
            public ulong Roll;                      // 64비트 roll
            public int ChanceBasisPoints;           // 확률(만분율)
            public bool Evaded;                     // 회피했는가
            public UnitData Victim;                 // 맞을 유닛
            public int HpBefore;                    // 판정 직전 HP
        }

        /// <summary>
        /// (서버 권위 writer 안) Hitscan 회피 관문. 판정 조건이 맞으면 굴려서 결과를 gate에 담고 true를 돌려준다.
        /// 조건이 맞지 않으면(평상시) 아무것도 하지 않고 false — writer는 기존대로 진행한다.
        /// </summary>
        /// <param name="attacker">공격자(살아 있음이 이미 확인됨).</param>
        /// <param name="target">대상(살아 있음이 이미 확인됨).</param>
        /// <param name="presentationResultKey">멀티 C2 정규 결과 키(싱글은 default).</param>
        /// <param name="gate">판정 기록.</param>
        private bool TryRollHitscanEvasion(UnitData attacker, IDamageable target, AttackResultKey presentationResultKey,
                                           out HitscanEvadeGate gate)
        {
            gate = default;
            if (_skillEvasion == null || attacker == null) return false;
            // 투사체 착탄 처리 중 — 투사체는 발사 순간에 판정한다(S-3). 여기서 다시 굴리지 않는다(이중 판정 방지 — R-6).
            if (ProjectileImpactInProgress) return false;
            // 「원거리 단일」 즉발 공격 = 총잡이 3종만(M-1). 근접 · 파도 · 힐 · 투사체 유닛은 대상이 아니다.
            if (!SkillEvasionRoll.IsHitscanEvadableAttacker(attacker.Type)) return false;
            // 연막은 유닛만 보호한다(M-7) — 건물 대상 공격은 판정하지 않는다.
            if (!(target is UnitData victim)) return false;
            // 적의 공격만(같은 팀끼리는 원래 싸우지 않지만, 혹시라도 판정하지 않는다).
            if (victim.Team == attacker.Team) return false;
            // 맞을 유닛이 연막 안인가(M-2) — 연막 밖이면 확률 0 → 판정 없음.
            int chance = SkillEvasionRoll.ToChanceBasisPoints(_skillEvasion.GetEvadeChance(victim.Id));
            if (chance <= 0) return false;

            gate.ChanceBasisPoints = chance;
            gate.Victim = victim;
            gate.HpBefore = victim.Hp;
            if (presentationResultKey.IsValid)
            {
                // 멀티 — 서버 권위 Impact 승인이 만든 정규 결과 키(같은 타격 한 번 = 같은 열쇠).
                gate.KeySource = SkillEvasionKeySource.ResultKey;
                gate.Key = SkillEvasionRoll.KeyFromResultKey(presentationResultKey);
            }
            else
            {
                // 싱글(또는 결과 키가 없는 경로) — O-10 대체 열쇠(공격자 Id + 그 공격자의 회피 판정 순번).
                gate.KeySource = SkillEvasionKeySource.AttackerOrdinal;
                gate.Key = _skillEvasion.NextAttackerOrdinalKey(attacker.Id, out gate.Ordinal);
            }
            gate.Evaded = _skillEvasion.Roll(gate.Key, chance, out gate.Roll);
            return true;
        }

        // [진단 E-L3] 판정 1줄 — 피했으면 HP는 그대로, 맞았으면 피해 적용 뒤 HP. 진단 서비스 쪽 메서드가 [Conditional]이라 릴리스에서는 사라진다.
        private void DiagHitscanEvadeRoll(UnitData attacker, in HitscanEvadeGate gate, int hpAfter)
        {
            _skillEvasion?.DiagOnRoll(SkillEvasionPath.Hitscan, gate.KeySource, gate.Key, gate.Ordinal, gate.Roll,
                gate.ChanceBasisPoints, gate.Evaded, attacker.Id, attacker.Type.ToString(), attacker.Team,
                gate.Victim, gate.HpBefore, hpAfter);
        }

        /// <summary>
        /// [진단 E-L4 — 멀티 서버] 회피한 Hitscan 타격이 C2에서 어떤 결과로 닫혔는지 회피 서비스 진단에 넘긴다.
        /// NetworkCombatController가 writer 결과가 <see cref="AttackDamageApplyStatus.Evaded"/>일 때만 부른다. 판정에는 관여하지 않는다.
        /// </summary>
        /// <param name="attackerId">공격자 유닛 Id.</param>
        /// <param name="hasResult">C2 결과가 만들어졌는가.</param>
        /// <param name="outcome">C2 결과 종류(없으면 None).</param>
        /// <param name="completion">C2 순서기 처리 결과.</param>
        /// <param name="reason">C2 코디네이터 이유 문자열.</param>
        /// <param name="key">C2 결과 키(없으면 default).</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagNoteEvadeResolved(int attackerId, bool hasResult, AttackImpactOutcome outcome,
                                          UnitActionReducerStatus completion, string reason, AttackResultKey key)
        {
            _skillEvasion?.DiagOnResolved(attackerId, hasResult, outcome, completion, reason, key);
        }
    }
}
