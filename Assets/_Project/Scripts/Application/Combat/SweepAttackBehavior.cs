// ============================================================================
// SweepAttackBehavior.cs
// 도끼병(BattleAxe)의 "휩쓸기형 AoE" 특수 공격 핸들러.
//
// 동작(사용자 확정 2026-07-16):
//   1) 판정 타일 = 공격자 전방 5타일 + 자기 타일 (등 뒤 1타일 제외). 좌표 계산은
//      Domain 순수 함수 SweepAttackArea.Collect 사용.
//   2) 판정 타일 위의 "적 유닛"을 먼저 리스트로 수집한다.
//      - 순회 도중 사망 유닛이 Dictionary에서 제거되면 컬렉션 변경 예외가 나므로,
//        수집(순회)과 피해(적용)를 반드시 두 단계로 분리한다.
//   3) 수집된 각 적에게 재사용 피해 헬퍼로 동일 피해(공격력 15)를 적용한다.
//
// 제외 규칙:
//   - 아군 제외: unit.Team == attacker.Team (규칙 16)
//   - 주 타깃 중복 제외: 주 타깃은 단일 경로로 이미 1회 피해를 받았으므로 제외(D-3)
//   - 죽은 유닛 제외 / 공격자 자신 제외
//   - 건물은 AoE 대상이 아님(주 타깃일 때만 단일 피해) — 애초에 Units만 순회하므로 자동 제외(D-2)
//
// Application 레이어 — Domain에만 의존.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 도끼병 휩쓸기형 AoE 구현. 전방 5타일 + 자기 타일의 모든 적 유닛에 동일 피해.
    /// </summary>
    public sealed class SweepAttackBehavior : ISpecialAttackBehavior
    {
        // 재사용 버퍼(모바일 GC 절감). 이 핸들러는 레지스트리에 1개 인스턴스만 존재하고,
        // 전투 판정은 서버/싱글 단일 스레드에서만 수행되므로 필드 재사용이 안전하다.
        private readonly List<HexCoord> _areaTiles = new List<HexCoord>(6);
        private readonly List<UnitData> _victims = new List<UnitData>(8);

        /// <summary>
        /// 휩쓸기 판정 후 대상 적 유닛 전원에게 피해를 적용한다.
        /// </summary>
        /// <param name="ctx">공격자·주 타깃·유닛 목록·피해 헬퍼 컨텍스트.</param>
        public void Apply(SpecialAttackContext ctx)
        {
            if (ctx == null) return;

            UnitData attacker = ctx.Attacker;
            if (attacker == null || !attacker.IsAlive) return;
            if (ctx.Units == null) return;

            // 1) 판정 타일 집합 = 전방 5 + 자기. Facing은 ExecuteAttack이 타겟 방향으로 갱신한 값.
            SweepAttackArea.Collect(attacker.Position, attacker.Facing, _areaTiles);

            // 주 타깃이 "유닛"일 때만 그 Id를 중복 제외 대상으로 삼는다.
            // 유닛 Id와 건물 Id는 서로 다른 카운터라 값이 겹칠 수 있으므로,
            // 주 타깃이 건물이면 유닛 제외에 사용하지 않는다(잘못된 유닛 제외 방지).
            UnitData primaryUnit = ctx.PrimaryTarget as UnitData;
            bool hasPrimaryUnit = primaryUnit != null;
            int primaryUnitId = hasPrimaryUnit ? primaryUnit.Id : 0;

            // 2) 대상 선수집(순회 중 사망 제거로 인한 컬렉션 변경 예외 회피)
            _victims.Clear();
            foreach (var unit in ctx.Units.Values)
            {
                if (unit == null) continue;
                if (!unit.IsAlive) continue;
                if (unit.Team == attacker.Team) continue;      // 아군 제외(규칙 16)
                if (unit.Id == attacker.Id) continue;          // 공격자 자신 제외(방어적)
                if (hasPrimaryUnit && unit.Id == primaryUnitId) continue; // 주 타깃 중복 제외(D-3)
                if (!AreaContains(unit.Position)) continue;    // 판정 타일 안에 있는가

                _victims.Add(unit);
            }

            // 3) 수집된 각 적에게 동일 피해 적용(재사용 헬퍼 — 단일 타깃과 같은 절차).
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
        /// 좌표가 이번 판정 타일 집합(_areaTiles)에 포함되는지 확인.
        /// 원소가 최대 6개뿐이라 선형 탐색으로 충분하며 할당이 없다.
        /// </summary>
        private bool AreaContains(HexCoord coord)
        {
            for (int i = 0; i < _areaTiles.Count; i++)
            {
                if (_areaTiles[i] == coord) return true;
            }
            return false;
        }
    }
}
