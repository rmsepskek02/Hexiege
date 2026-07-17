// ============================================================================
// SweepAttackBehavior.cs
// 도끼병(BattleAxe)의 "휩쓸기형 AoE" 특수 공격 핸들러.
//
// 판정 방식(2026-07-16 실기 후 변경 — 월드 좌표 전방 부채꼴):
//   과거에는 "전방 5타일 + 자기 타일"처럼 타일 소속(unit.Position) 기준으로 판정했으나,
//   유닛이 타일 사이를 이어 움직이고 겹치는 실제 연출과 어긋나, 도끼 스윙이 그리는
//   연속 반경/호에 맞춰 "월드 좌표 기반 전방 부채꼴"로 교체했다.
//
//   1) forward(기준 방향) = 공격자 → 주 타깃 방향(월드 XZ). ExecuteAttack이 데미지 직전
//      Facing을 주 타깃 방향으로 갱신하므로, 주 타깃의 월드 좌표로 forward를 구하면
//      주 타깃은 항상 부채꼴 전방에 포함된다.
//   2) 각 적에 대해 공격자로부터의 XZ 평면 거리 ≤ reach 이고,
//      forward와 이루는 각도 ≤ arcHalfAngle(도) 이면 피격.
//      - Y축은 UnitYOffset(0.15) 때문에 무시하고 x,z만 사용한다.
//      - 겹쳐 붙은 적(거리 ≈ 0, 방향 벡터가 0이라 각도 판정 불가)은 무조건 포함한다
//        (자기 타일 겹침 취지 유지).
//      - forward가 유효하지 않은 경우(주 타깃이 공격자와 거의 겹침 등)에는 각도 제약 없이
//        반경 안의 적을 전부 포함한다(방어적 폴백).
//
// 제외 규칙(기존 유지):
//   - 아군 제외: unit.Team == attacker.Team (규칙 16)
//   - 주 타깃 중복 제외: 주 타깃은 단일 경로로 이미 1회 피해를 받았으므로 제외(D-3)
//   - 죽은 유닛 제외 / 공격자 자신 제외
//   - 건물은 AoE 대상이 아님(주 타깃일 때만 단일 피해) — 애초에 Units만 순회하므로 자동 제외(D-2)
//
// 선수집 후 적용(2단계):
//   순회 도중 사망 유닛이 Dictionary에서 제거되면 컬렉션 변경 예외가 나므로,
//   수집(순회)과 피해(적용)를 반드시 두 단계로 분리한다.
//
// Application 레이어 — Domain 의존 + UnityEngine.Vector3(Unity 의존 허용).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 도끼병 휩쓸기형 AoE 구현. 공격자 → 주 타깃 방향을 기준으로 한 월드 좌표 전방 부채꼴
    /// (반경 reach, 반각 arcHalfAngle) 안의 모든 적 유닛에 동일 피해를 적용한다.
    /// </summary>
    public sealed class SweepAttackBehavior : ISpecialAttackBehavior
    {
        // 방향 벡터가 "사실상 0"인지 판별하는 제곱 거리 임계값(월드 단위).
        // 이보다 가까우면 겹친 것으로 간주하여 각도 판정을 건너뛴다. (0.01 world unit)^2.
        private const float MinSeparationSqr = 0.0001f;

        // 재사용 버퍼(모바일 GC 절감). 이 핸들러는 레지스트리에 1개 인스턴스만 존재하고,
        // 전투 판정은 서버/싱글 단일 스레드에서만 수행되므로 필드 재사용이 안전하다.
        private readonly List<UnitData> _victims = new List<UnitData>(8);

        /// <summary>
        /// 휩쓸기는 "기본 단일 공격에 얹는" 즉발 범위 피해이므로 주 타깃 단일 피해를 대체하지 않는다.
        /// (special-only가 아님 — 주 타깃 단일 피해 후 Apply가 실행됨.)
        /// </summary>
        public bool ReplacesPrimaryAttack => false;

        /// <summary>
        /// 전방 부채꼴 판정 후 대상 적 유닛 전원에게 피해를 적용한다.
        /// </summary>
        /// <param name="ctx">공격자·주 타깃·유닛 목록·피해 헬퍼·좌표 조회·튜닝값 컨텍스트.</param>
        public void Apply(SpecialAttackContext ctx)
        {
            if (ctx == null) return;

            UnitData attacker = ctx.Attacker;
            if (attacker == null || !attacker.IsAlive) return;
            if (ctx.Units == null) return;

            float reach = ctx.SweepReach;
            float reachSqr = reach * reach;
            float arcHalfAngle = ctx.SweepArcHalfAngle;

            // 공격자 월드 좌표(XZ 평면). Y는 UnitYOffset 때문에 무시.
            Vector3 origin = Flatten(ctx.WorldPositionOf(attacker));

            // 기준 방향(forward) = 공격자 → 주 타깃(XZ). ExecuteAttack이 Facing을 타겟 방향으로
            // 갱신한 것과 정합하며, 이를 통해 주 타깃은 항상 부채꼴 전방에 포함된다.
            Vector3 forward = Vector3.zero;
            if (ctx.PrimaryTarget != null)
                forward = Flatten(ctx.WorldPositionOf(ctx.PrimaryTarget)) - origin;

            // forward가 사실상 0이면(주 타깃이 공격자와 겹침 등) 각도 판정이 불가능하다.
            // 이 경우 반경 내 적을 전부 포함하는 폴백을 쓴다.
            bool hasForward = forward.sqrMagnitude > MinSeparationSqr;

            // 주 타깃이 "유닛"일 때만 그 Id를 중복 제외 대상으로 삼는다.
            // 유닛 Id와 건물 Id는 서로 다른 카운터라 값이 겹칠 수 있으므로,
            // 주 타깃이 건물이면 유닛 제외에 사용하지 않는다(잘못된 유닛 제외 방지).
            UnitData primaryUnit = ctx.PrimaryTarget as UnitData;
            bool hasPrimaryUnit = primaryUnit != null;
            int primaryUnitId = hasPrimaryUnit ? primaryUnit.Id : 0;

            // 1) 대상 선수집(순회 중 사망 제거로 인한 컬렉션 변경 예외 회피)
            _victims.Clear();
            foreach (var unit in ctx.Units.Values)
            {
                if (unit == null) continue;
                if (!unit.IsAlive) continue;
                if (unit.Team == attacker.Team) continue;                 // 아군 제외(규칙 16)
                if (unit.Id == attacker.Id) continue;                     // 공격자 자신 제외(방어적)
                if (hasPrimaryUnit && unit.Id == primaryUnitId) continue; // 주 타깃 중복 제외(D-3)

                // 공격자로부터의 XZ 평면 벡터/거리.
                Vector3 toUnit = Flatten(ctx.WorldPositionOf(unit)) - origin;
                float distSqr = toUnit.sqrMagnitude;

                if (distSqr > reachSqr) continue;                         // 반경 밖 → 제외

                // 겹친 적(거리 ≈ 0): 방향 판정 불가 → 무조건 포함(자기 타일 겹침 취지).
                if (distSqr <= MinSeparationSqr)
                {
                    _victims.Add(unit);
                    continue;
                }

                // forward 무효 시: 각도 제약 없이 반경 내 전부 포함(폴백).
                if (!hasForward)
                {
                    _victims.Add(unit);
                    continue;
                }

                // 각도 판정 — Vector3.Angle은 두 벡터를 내부에서 정규화해 0~180° 무부호 각을 반환한다.
                // (Red 팀의 X축 반전은 반사 변환이라 거리·무부호 각을 보존하므로 뷰 좌표 그대로 판정 가능.)
                float angle = Vector3.Angle(forward, toUnit);
                if (angle <= arcHalfAngle)
                    _victims.Add(unit);
            }

            // 2) 수집된 각 적에게 동일 피해 적용(재사용 헬퍼 — 단일 타깃과 같은 절차).
            //    피해로 사망하면 헬퍼 내부에서 Dictionary가 정리되지만,
            //    이미 _victims에 복사해 두었으므로 순회는 안전하다.
            for (int i = 0; i < _victims.Count; i++)
            {
                UnitData victim = _victims[i];
                if (victim == null || !victim.IsAlive) continue; // 방어적 재확인
                ctx.ApplyDamage(attacker, victim);
            }
        }

        /// <summary>
        /// 벡터의 Y 성분을 0으로 눌러 XZ 평면 벡터로 만든다(높이 무시).
        /// </summary>
        private static Vector3 Flatten(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }
    }
}
