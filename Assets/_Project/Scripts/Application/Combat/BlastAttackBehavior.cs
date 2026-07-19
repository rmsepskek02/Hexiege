// ============================================================================
// BlastAttackBehavior.cs
// MushroomBomber(버섯폭격기)의 "착탄형 범위 DoT" 특수 공격 핸들러.
//
// 무엇을 하나(초급자용 설명):
//   버섯폭격기는 폭탄(포자)을 적에게 던져 착탄 지점에서 폭발시킨다.
//   - 폭탄을 맞은 딱 1마리(주 타깃)에게는 "직접 10" 피해가 들어간다.
//     → 이 직접 피해는 이 핸들러가 아니라 ExecuteAttack의 "주 타깃 단일 피해" 경로가 담당한다.
//       (그래서 이 핸들러는 ReplacesPrimaryAttack = false — 단일 피해를 대체하지 않는다.)
//   - 이 핸들러는 "착탄 지점 주변 반경 안의 적 유닛 전원"에게 DoT(지속 피해)를 부여한다.
//     주 타깃이 유닛이면 그 유닛도 반경 안(거리 0)이라 DoT를 함께 받는다(직접 10 + DoT).
//
// 도끼병(SweepAttackBehavior)과의 차이 3가지:
//   1) 판정 모양 — 도끼병은 "전방 부채꼴"(reach + arc)이지만, 버섯폭격기는
//      착탄 중심을 기준으로 한 "원형 반경"(arc 없음)이다.
//   2) 판정 중심 — 도끼병은 "공격자" 위치가 중심이지만, 버섯폭격기는 "주 타깃(착탄 지점)" 위치가 중심.
//   3) 주 타깃 포함 — 도끼병은 주 타깃을 중복 제외하지만, 버섯폭격기는 주 타깃도 DoT 대상에 포함한다.
//      (직접 10과 DoT는 서로 다른 경로라, 주 타깃이 직접 10 + DoT를 모두 받는 것이 의도된 동작이다.)
//
// 효과(피해가 아니라 DoT 부여):
//   각 대상에 ctx.ApplyDot(attacker, victim)을 호출한다. DoT의 초당 피해/지속/틱 간격은
//   UnitCombatUseCase 내부에 캡슐화돼 있고(서버 권위), 이 핸들러는 "누구에게 걸지"만 결정한다.
//   (파도(TorrentSpirit)가 SpawnWave로 모양만 넘기고 효과 적용은 UseCase가 하는 것과 같은 역할 분담.)
//
// 제외 규칙:
//   - 아군 제외: unit.Team == attacker.Team (규칙 16). 공격자 자신도 같은 팀이라 자동 제외.
//   - 죽은 유닛 제외.
//   - 건물은 DoT 대상이 아님 — 애초에 Units(유닛만)를 순회하므로 자동 제외.
//     ⚠️ 단, "주 타깃이 건물이어도" 이 핸들러는 실행되어 주변 적 유닛에 DoT를 건다
//        (핸들러는 주 타깃 종류와 무관하게 실행 — 도끼병과 동일).
//
// 선수집 후 적용(2단계):
//   순회 도중 DoT 부여로 대상이 사망해 Dictionary에서 제거될 여지가 있으므로,
//   수집(순회)과 적용을 반드시 두 단계로 분리한다(도끼병과 동일한 안전 패턴).
//   (DoT 첫 틱은 다음 서버 틱에 들어가므로 이 순간 즉사하지는 않지만, 방어적으로 분리한다.)
//
// Application 레이어 — Domain 의존 + UnityEngine.Vector3(Unity 의존 허용).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 버섯폭격기 착탄형 범위 DoT 구현. 착탄 중심(주 타깃 위치)에서 월드 원형 반경 안의
    /// 모든 적 유닛(주 타깃 포함)에게 지속 피해(DoT)를 부여한다.
    /// </summary>
    public sealed class BlastAttackBehavior : ISpecialAttackBehavior
    {
        // 재사용 버퍼(모바일 GC 절감). 레지스트리에 1개 인스턴스만 존재하고,
        // 전투 판정은 서버/싱글 단일 스레드에서만 수행되므로 필드 재사용이 안전하다.
        private readonly List<UnitData> _victims = new List<UnitData>(8);

        /// <summary>
        /// 착탄 DoT는 "기본 단일 공격(직접 10)에 얹는" 범위 효과이므로 주 타깃 단일 피해를 대체하지 않는다.
        /// (special-only가 아님 — ExecuteAttack이 주 타깃 직접 10을 먼저 적용한 뒤 이 Apply가 실행됨.)
        /// </summary>
        public bool ReplacesPrimaryAttack => false;

        /// <summary>
        /// 착탄 반경 판정 후 반경 내 적 유닛 전원(주 타깃 포함)에게 DoT를 부여한다.
        /// </summary>
        /// <param name="ctx">공격자·주 타깃·유닛 목록·DoT 부여 델리게이트·좌표 조회·착탄 반경 컨텍스트.</param>
        public void Apply(SpecialAttackContext ctx)
        {
            if (ctx == null) return;

            UnitData attacker = ctx.Attacker;
            if (attacker == null || !attacker.IsAlive) return;
            if (ctx.Units == null) return;

            // 주 타깃이 없으면 착탄 중심을 정할 수 없으므로 아무것도 하지 않는다(방어적).
            if (ctx.PrimaryTarget == null) return;

            // 착탄 중심 = 주 타깃 월드 위치(유닛/건물 무관). Y(UnitYOffset)는 무시하고 XZ만 사용.
            Vector3 center = Flatten(ctx.WorldPositionOf(ctx.PrimaryTarget));

            float radius = ctx.BlastRadius;
            float radiusSqr = radius * radius;

            // 1) 반경 내 적 유닛 선수집(순회 중 컬렉션 변경 회피).
            _victims.Clear();
            CollectEnemyUnitsInRadius(ctx, attacker, center, radiusSqr, _victims);

            // 2) 수집된 각 적 유닛에 DoT 부여(초 단위 discrete 틱). 실제 피해/사망은 UseCase 틱이 처리.
            for (int i = 0; i < _victims.Count; i++)
            {
                UnitData victim = _victims[i];
                if (victim == null || !victim.IsAlive) continue; // 방어적 재확인
                ctx.ApplyDot(attacker, victim);
            }
        }

        /// <summary>
        /// 착탄 중심에서 월드 원형 반경(XZ 평면) 안의 적 유닛을 수집한다.
        ///
        /// 향후 QuakeSpirit(또 다른 착탄형)도 "중심 원형 반경 내 적 유닛 수집"이 필요하므로
        /// 재사용 가능하도록 static 헬퍼로 분리했다. (중심 100%/인접 50% 같은 감쇠는 QuakeSpirit이
        /// 이 수집 결과 위에 거리 기반 배율을 얹으면 되므로, 순수 "수집"만 공용화한다.)
        /// </summary>
        /// <param name="ctx">좌표 조회·유닛 목록 컨텍스트.</param>
        /// <param name="attacker">공격자(팀 필터 기준 — 아군/자신 제외).</param>
        /// <param name="center">착탄 중심 월드 좌표(XZ 평면).</param>
        /// <param name="radiusSqr">반경의 제곱(거리 비교 시 제곱근을 피하려고 제곱값 사용).</param>
        /// <param name="result">수집 결과를 담을 리스트(호출자가 Clear 후 넘긴다).</param>
        private static void CollectEnemyUnitsInRadius(
            SpecialAttackContext ctx, UnitData attacker, Vector3 center,
            float radiusSqr, List<UnitData> result)
        {
            foreach (var unit in ctx.Units.Values)
            {
                if (unit == null || !unit.IsAlive) continue;
                if (unit.Team == attacker.Team) continue; // 아군 제외(규칙 16). 공격자 자신도 같은 팀이라 제외됨.
                // ⚠️ 도끼병과 달리 주 타깃을 제외하지 않는다 — 주 타깃도 DoT 대상에 포함(직접 10 + DoT).

                // 착탄 중심으로부터의 XZ 평면 거리(제곱)로 반경 안/밖 판정.
                Vector3 rel = Flatten(ctx.WorldPositionOf(unit)) - center;
                if (rel.sqrMagnitude <= radiusSqr)
                    result.Add(unit);
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
