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
// [2026-11-23 단계 S-3 — 서버 권위 투사체 회피(Plan P-5 · 상위 R-11 「발사 시점 판정」)]
//   날아가는 투사체(Tank · CannonCart · InfernoSpirit · StreamSpirit · FoxMagician · EagleArcher + 인간 대포탑)는
//   "쏘는 순간"에 피할지 정한다. 착탄 순간에 정하면 화면이 빗나감을 그릴 시간이 없기 때문이다(Host · 싱글은 화면 지연 0이라
//   결과가 화면 착탄과 같은 프레임에 와서, 투사체가 대상에 닿는 순간 옆으로 튀는 그림이 된다 — Research 8-2).
//     · TryRollProjectileEvasion : 발사 순간 판정. 피했으면 빗나간 지점(대상 옆 반경 + 0.3, 진행 방향 + 0.5, 좌우 = 난수 비트)을 고정.
//                                  열쇠 = 발사 번호표(epoch · id) — 발사마다 유일하고, 같은 seed면 언제나 같은 결과.
//     · ResolveEvadedProjectileImpact : 착탄 시각 — writer를 부르지 않고 결과 「회피」만 발행(피해 · 화상 · 숫자 없음).
//   MushroomBomber는 착탄 범위 폭발이라 「단일」이 아니어서 판정하지 않는다(메인 기본안 M-1). 건물 대상도 판정하지 않는다(M-7).
//   이중 판정 없음: 회피하지 않은 투사체는 착탄 때 writer를 지나지만, 그때는 「투사체 착탄 중」 표시가 서 있어 위 Hitscan 관문이 건너뛴다.
//
// Application 레이어 — Domain · Application 의존. UnityEngine은 Vector3 계산에만 쓴다(S-3 — 빗나간 지점).
//   (2026-11-23 S-3: 옛 문구 「UnityEngine 참조 없음」 → 빗나간 지점 계산 때문에 Vector3를 쓰게 됐다.)
// ============================================================================

using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;
using UnityEngine;

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

        // ====================================================================
        // [2026-11-23 연막탄 단계 S-3] 서버 권위 투사체 — 발사 순간 회피 판정 · 회피 착탄
        // ====================================================================

        // 투사체 발사 순간 판정 한 번의 기록(발사 기록 · 진단에 쓴다).
        private struct ProjectileEvadeGate
        {
            public SkillEvasionPath Path;    // Projectile(유닛) 또는 TowerShell(인간 대포탑)
            public ulong Key;                // 공격 열쇠 = 발사 번호표(epoch · id) 해시
            public ulong Roll;               // 64비트 roll
            public int ChanceBasisPoints;    // 확률(만분율)
            public bool Evaded;              // 회피했는가
            public UnitData Victim;          // 맞을 유닛
            public int Hp;                   // 발사 순간 HP(투사체는 피해가 착탄 때라 판정 줄의 전후 HP가 같다)
            public Vector3 MissPoint;        // 회피면 빗나간 지점(도메인 월드)
            public float MissTravel;         // 회피면 발사 위치 → 빗나간 지점 XZ 거리
        }

        /// <summary>
        /// (서버 권위 — 발사 순간) 투사체 회피 관문. 판정 조건이 맞으면 굴려서 gate에 담고 true를 돌려준다.
        /// 조건이 맞지 않으면(평상시 — 연막 없음) 아무것도 하지 않고 false — 발사는 기존대로 대상을 향한다.
        /// </summary>
        /// <param name="record">발사할 투사체 기록(번호 · 공격자 · 팀이 이미 채워져 있다).</param>
        /// <param name="target">원래 대상(발사 순간 살아 있음이 확인됨).</param>
        /// <param name="source">발사 위치(도메인 월드 — 유닛 위치 또는 대포탑 포구).</param>
        /// <param name="center">발사 순간 대상 중심(도메인 월드).</param>
        /// <param name="radius">대상 반경(표면 지점 계산과 같은 값).</param>
        /// <param name="gate">판정 기록.</param>
        private bool TryRollProjectileEvasion(ProjectileRecord record, IDamageable target, Vector3 source, Vector3 center,
                                              float radius, out ProjectileEvadeGate gate)
        {
            gate = default;
            if (_skillEvasion == null || record == null) return false;
            // 연막은 유닛만 보호한다(M-7) — 건물 대상 투사체는 판정하지 않는다.
            //   (Id만으로 묻지 않는 이유: 건물 Id와 유닛 Id는 번호가 겹칠 수 있어, 같은 번호의 연막 속 유닛 확률을 잘못 읽을 수 있다.)
            if (!record.TargetIsUnit || !(target is UnitData victim)) return false;
            // 적의 공격만(같은 팀끼리는 원래 싸우지 않지만, 혹시라도 판정하지 않는다).
            if (victim.Team == record.Team) return false;
            // 맞을 유닛이 연막 안인가(M-2) — 연막 밖이면 확률 0 → 판정 없음(평상시 비용 = 사전 조회 한 번 없이 바로 0).
            int chance = SkillEvasionRoll.ToChanceBasisPoints(_skillEvasion.GetEvadeChance(victim.Id));
            if (chance <= 0) return false;
            // 「원거리 단일」 투사체만(M-1) — 유닛 6종(MushroomBomber 제외) · 투사체 탑(인간 대포탑).
            bool evadable = record.AttackerIsUnit
                ? SkillEvasionRoll.IsProjectileEvadableAttacker(record.AttackerType)
                : record.AttackerBuilding != null
                  && SkillEvasionRoll.IsTowerShellEvadableAttacker(record.AttackerBuilding.Type, record.AttackerRace);
            if (!evadable)
            {
                // [진단 E-L11] 연막 안 대상에게 쐈지만 회피 대상이 아닌 발사(MushroomBomber)를 센다 — 제외가 실제로 일어났다는 증거.
                if (record.AttackerIsUnit) _skillEvasion.DiagNoteProjectileNotEvadable(record.AttackerType);
                return false;
            }

            gate.Path = record.AttackerIsUnit ? SkillEvasionPath.Projectile : SkillEvasionPath.TowerShell;
            gate.ChanceBasisPoints = chance;
            gate.Victim = victim;
            gate.Hp = victim.Hp;
            // 열쇠 = 발사 번호표(epoch · id). 발사마다 유일하므로 다른 시스템이 무엇을 하든 이 발의 결과는 바뀌지 않는다.
            gate.Key = SkillEvasionRoll.KeyFromProjectile(ProjectileEpoch, record.Id);
            gate.Evaded = _skillEvasion.Roll(gate.Key, chance, out gate.Roll);
            if (gate.Evaded)
            {
                // 빗나간 지점은 서버 판정 · 양쪽 화면이 같은 이 함수 하나로 정한다(공습 「한 함수」 교훈). 좌우는 같은 roll의 맨 위 비트.
                gate.MissPoint = SkillEvasionRoll.ComputeMissPoint(source, center, radius, gate.Roll);
                gate.MissTravel = SkillEvasionRoll.MissTravelDistance(source, gate.MissPoint);
            }
            return true;
        }

        /// <summary>
        /// (서버 권위 — 착탄 시각) 발사 순간 「회피」로 정한 투사체를 닫는다. 피해 writer를 부르지 않는다
        /// → HP · 피해 이벤트 · 피해 숫자 · 화상(InfernoSpirit)이 모두 없다. 결과 「회피」를 빗나간 지점과 함께 한 번 발행한다.
        /// 호출 전에 record.Active = false로 이미 소비됐다(같은 착탄이 두 번 나지 않는다). writer를 부르지 않으므로 재진입도 없다.
        /// </summary>
        /// <param name="record">착탄한 회피 투사체 기록.</param>
        /// <param name="key">투사체 번호표.</param>
        /// <param name="diag">진단용 복사본(ProcessProjectileFlight가 만든 것).</param>
        /// <param name="now">서버 시계 현재 시각(진단 — 착탄 지연).</param>
        private void ResolveEvadedProjectileImpact(ProjectileRecord record, ProjectileDeliveryKey key,
                                                   ProjectileImpactDiagnosticSnapshot diag, double now)
        {
            Vector3 point = record.MissPoint;
            int targetId = record.TargetId;
            bool targetIsUnit = record.TargetIsUnit;
            double impactTime = record.ImpactTime;
            // 비행 중 대상 추적(ProcessProjectileFlight 맨 앞)이 이번 프레임까지 확인했다 — 사라졌으면 TargetLost가 서 있다(진단 전용).
            bool targetAlive = !record.TargetLost && record.Target != null && record.Target.IsAlive;
            int hpNow = targetAlive ? record.Target.Hp : -1;

            DiagProjectileEvadedImpact(diag, now);   // [진단] 서버 END 수 맞춤(launched = hit + targetLost + evaded + closedInFlight)
            ProjectileLog("evaded", diag, impactTime,
                EvadedImpactLogDetail(targetAlive, point));
            DiagProjectileEvadeImpactLine(record, key, targetAlive, hpNow, point, impactTime);   // [진단 E-L6 · E-L7]
            PublishProjectileResolved(new ProjectileImpactPresentation(key, ProjectileImpactOutcome.Evaded,
                point, targetId, targetIsUnit, -1, 0, impactTime));
        }

        // [진단] [PROJECTILE] stage=evaded 줄의 끝 칸(개별 로그 형식은 다른 stage와 같다 — 끝에 cause= 등만 붙는다).
        private static string EvadedImpactLogDetail(bool targetAlive, Vector3 point)
            => System.FormattableString.Invariant(
                $"cause=smoke-evade, writer=NotInvoked, targetAlive={(targetAlive ? 1 : 0)}, miss=({point.x:F3},{point.z:F3})");

        // [진단 E-L5 · E-L8] 발사 순간 판정 1줄(+ 회피면 빗나간 지점 1줄). [Conditional]이라 릴리스 빌드에서는 호출 · 문자열 조립이 사라진다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileEvadeRoll(ProjectileRecord record, ProjectileEvadeGate gate, Vector3 source, Vector3 center,
                                             float radius, double flightSeconds)
        {
            if (_skillEvasion == null || record == null) return;
            string projectile = new ProjectileDeliveryKey(ProjectileEpoch, record.Id).ToString();
            int attackerId = record.AttackerIsUnit
                ? (record.Attacker != null ? record.Attacker.Id : -1)
                : (record.AttackerBuilding != null ? record.AttackerBuilding.Id : -1);
            string attackerType = record.AttackerIsUnit ? record.AttackerType.ToString() : "tower-" + record.AttackerRace;
            _skillEvasion.DiagOnRoll(gate.Path, SkillEvasionKeySource.ProjectileKey, gate.Key, 0UL, gate.Roll,
                gate.ChanceBasisPoints, gate.Evaded, attackerId, attackerType, record.Team, gate.Victim, gate.Hp, gate.Hp,
                "Projectile=" + projectile);
            if (gate.Evaded)
                _skillEvasion.DiagOnMissPoint(gate.Path, projectile, source, center, radius, gate.MissPoint,
                    SkillEvasionRoll.MissSideIsLeft(gate.Roll), flightSeconds);
        }

        // [진단 E-L6 · E-L7] 회피 착탄 1줄(WriterInvoked=0). [Conditional] — 릴리스에서는 사라진다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagProjectileEvadeImpactLine(ProjectileRecord record, ProjectileDeliveryKey key, bool targetAlive,
                                                   int hpNow, Vector3 point, double impactTime)
        {
            if (_skillEvasion == null || record == null) return;
            SkillEvasionPath path = record.AttackerIsUnit ? SkillEvasionPath.Projectile : SkillEvasionPath.TowerShell;
            string attackerType = record.AttackerIsUnit ? record.AttackerType.ToString() : "tower-" + record.AttackerRace;
            _skillEvasion.DiagOnProjectileImpact(path, key.ToString(), attackerType, record.TargetId, targetAlive, hpNow,
                point, impactTime);
        }

        // ====================================================================
        // [2026-11-23 연막탄 단계 S-4] 즉발 탑 회피가 빌려 쓰는 읽기 전용 질의
        // ====================================================================

        /// <summary>
        /// 투사체가 쓰는 대상 반경(ProjectileSettings 표 — 유닛 종류별, 없으면 기본값)을 돌려준다. 상태를 바꾸지 않는다.
        /// [연막탄 S-4] 즉발 탑(TowerCombatUseCase)이 빗나간 지점의 옆 거리(반경 + 0.3)를 포탄 · 유닛 투사체와 같은 반경으로 정하려고 읽는다.
        /// </summary>
        /// <param name="target">대상(유닛 또는 건물).</param>
        public float GetProjectileTargetRadius(IDamageable target) => _projectileSettings.GetTargetRadius(target);
    }
}
