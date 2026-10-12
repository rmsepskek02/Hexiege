// ============================================================================
// TorrentAttackBehavior.cs
// TorrentSpirit(물의 상급 정령)의 "파도형 이동 AoE" 특수 공격 핸들러.
//
// 도끼병(SweepAttackBehavior)과의 근본적 차이 3가지:
//   1) special-only — 단일 대상 공격이 아예 없다(ReplacesPrimaryAttack = true).
//      ExecuteAttack이 주 타깃 단일 피해를 건너뛰고 이 핸들러만 실행한다.
//   2) 이동 파도 — 즉발이 아니라 전방으로 이동하는 전선이 닿는 순간 효과가 발동한다.
//      (이 핸들러는 파도의 "모양/방향"만 계산해 서버 시뮬레이터에 생성 요청하고,
//       전선 전진·닿은 유닛 판정·1회 적용은 UnitCombatUseCase가 매 서버 틱마다 처리한다.
//       규칙 18 — 데미지 타이밍은 서버 권위. 클라 파티클 위치에 종속 금지.)
//   3) 적에겐 피해, 아군에겐 회복 — 팀 구분 효과. 시전자 자신/죽은 유닛 제외는 시뮬레이터가 처리.
//
// 판정 영역(규칙 24 — 월드 좌표 직사각형):
//   forward = 공격자 → 주 타깃(월드 XZ). ExecuteAttack이 Facing을 타겟 방향으로 갱신한 것과 정합.
//   폭 WaveWidth × 전방 길이 WaveLength의 직사각형을, 전선이 WaveTravelTime 동안 훑는다.
//
// Application 레이어 — Domain 의존 + UnityEngine.Vector3(Unity 의존 허용).
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// TorrentSpirit 파도형 AoE. 공격자 → 주 타깃 방향으로 이동하는 직사각형 전선을 생성하며,
    /// 실제 효과 적용은 서버 시뮬레이터(UnitCombatUseCase)가 담당한다.
    /// </summary>
    public sealed class TorrentAttackBehavior : ISpecialAttackBehavior
    {
        // forward가 "사실상 0"인지 판별하는 제곱 거리 임계값(월드 단위).
        // 주 타깃이 공격자와 거의 겹쳐 방향을 구할 수 없을 때 파도 생성을 건너뛰기 위한 값. (0.01 world unit)^2.
        private const float MinForwardSqr = 0.0001f;

        /// <summary>
        /// TorrentSpirit은 단일 대상 공격이 없는 special-only 유닛이다.
        /// true이므로 ExecuteAttack이 주 타깃 단일 피해(ApplyDamageToVictim)를 건너뛴다.
        /// </summary>
        public bool ReplacesPrimaryAttack => true;

        /// <summary>
        /// 공격자 → 주 타깃 방향으로 파도(이동 전선)를 생성 요청한다.
        /// </summary>
        /// <param name="ctx">공격자·주 타깃·좌표 조회·파도 파라미터·파도 생성 델리게이트 컨텍스트.</param>
        public void Apply(SpecialAttackContext ctx)
        {
            if (ctx == null)
            {
                LogSkipped("ctx", -1, -1);
                return;
            }

            UnitData attacker = ctx.Attacker;
            if (attacker == null || !attacker.IsAlive)
            {
                LogSkipped("attacker", attacker != null ? attacker.Id : -1,
                    ctx.PrimaryTarget != null ? ctx.PrimaryTarget.Id : -1);
                return;
            }
            if (ctx.PrimaryTarget == null)
            {
                LogSkipped("target", attacker.Id, -1);
                return;
            }

            // 시작 지점 = 공격자 월드 좌표(XZ 평면). Y(UnitYOffset)는 무시.
            Vector3 origin = Flatten(ctx.WorldPositionOf(attacker));

            // 진행 방향 = 공격자 → 주 타깃(XZ). ExecuteAttack의 Facing 갱신과 정합.
            Vector3 forward = Flatten(ctx.WorldPositionOf(ctx.PrimaryTarget)) - origin;

            // 방향을 구할 수 없으면(주 타깃이 공격자와 거의 겹침) 파도를 생성하지 않는다.
            // 원거리 유닛(사거리 3)이라 실전에서는 항상 유효 방향이 나오지만 방어적으로 가드한다.
            if (forward.sqrMagnitude <= MinForwardSqr)
            {
                LogSkipped("zero-forward", attacker.Id, ctx.PrimaryTarget.Id);
                return;
            }
            forward.Normalize();

            // 파도 생성 요청 — 피해량은 시전자 AttackPower(UnitStatsConfig, 전 유닛 일관), 힐량은 config.
            ctx.SpawnWave(CreateWaveRequest(attacker, origin, forward,
                ctx.WaveWidth, ctx.WaveLength, ctx.WaveTravelTime, ctx.WaveHeal,
                ctx.PrimaryTarget.Id, ctx.PrimaryTarget is UnitData));
        }

        /// <summary>기존 파도 형태를 보존하며 승인 예약의 원점과 방향으로 생성 요청을 만든다.</summary>
        public static WaveSpawnRequest CreateWaveRequest(UnitData attacker, Vector3 origin,
            Vector3 forward, float width, float length, float travelTime, float heal,
            int primaryId, bool primaryIsUnit, ulong reservation = 0)
        {
            return new WaveSpawnRequest(
                caster: attacker,
                origin: Flatten(origin),
                forward: forward,
                width: width,
                length: length,
                travelTime: travelTime,
                heal: Mathf.RoundToInt(heal),
                diagnosticPrimaryTargetId: primaryId,
                diagnosticPrimaryTargetIsUnit: primaryIsUnit,
                diagnosticLaunchReservation: reservation);
        }

        // 조기 반환은 SpawnWave에 도달하지 않으므로 이 경계에서만 기록한다.
        // 성공 여부는 이 메서드가 아니라 실제 등록 지점의 spawn 행으로 확인한다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSkipped(string reason, int casterId, int targetId)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_diagnosticSkipRows >= 16) return;
            _diagnosticSkipRows++;
            GameLog.Dev.Info("Combat", nameof(TorrentAttackBehavior),
                "[DEBUG-TORRENT-WAVE] skip",
                $"reason={reason}, caster={casterId}, primary={targetId}, serverTime={Time.realtimeSinceStartup:F3}");
#endif
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static int _diagnosticSkipRows;
#endif

        /// <summary>
        /// 벡터의 Y 성분을 0으로 눌러 XZ 평면 벡터로 만든다(높이 무시).
        /// </summary>
        private static Vector3 Flatten(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }
    }
}
