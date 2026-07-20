// ============================================================================
// QuakeAttackBehavior.cs
// QuakeSpirit(대지의 정령)의 "착탄형 즉발 AoE" 특수 공격 핸들러.
//
// 무엇을 하나(초급자용 설명):
//   대지의 정령은 공격이 착탄하면 그 지점에서 땅을 뒤흔들어 주변을 함께 타격한다.
//   - 공격을 정통으로 맞은 딱 1마리/1채(주 타깃)에게는 "직접 100%"(공격력 그대로) 피해가 들어간다.
//     → 이 직접 100%는 이 핸들러가 아니라 ExecuteAttack의 "주 타깃 단일 피해" 경로가 담당한다.
//       (그래서 이 핸들러는 ReplacesPrimaryAttack = false — 단일 피해를 대체하지 않는다.)
//   - 이 핸들러는 "착탄 지점 주변 반경 안의 다른 적 유닛 + 적 건물"에게 즉발 스플래시(50%, 올림)를 준다.
//     ⚠️ 주 타깃은 이미 100%를 받았으므로 스플래시(50%)에서는 "제외"한다(이중 피해 금지).
//
// 버섯폭격기(BlastAttackBehavior)와의 차이 3가지:
//   1) 효과 종류 — 버섯폭격기는 반경 내 적에게 DoT(지속 피해)를 걸지만, 대지의 정령은
//      즉발 1회 피해(50%)를 준다(BattleAxe식 즉발). DoT 아님.
//   2) 대상 범위 — 버섯폭격기는 반경 내 "적 유닛"만 대상이지만, 대지의 정령은
//      반경 내 "적 유닛 + 적 건물" 모두가 대상이다(건물 공성 기여).
//   3) 주 타깃 포함 여부 — 버섯폭격기는 주 타깃도 DoT 대상에 포함하지만(직접 10 + DoT),
//      대지의 정령은 주 타깃을 스플래시에서 제외한다(주 타깃은 100%만).
//
// 판정 방식(도끼병/버섯폭격기와 동일한 월드 원형 반경 — 규칙 38):
//   착탄 중심(주 타깃 월드 위치)에서 XZ 평면 거리가 _quakeRadius 이하인 적을 수집한다.
//   - 유닛 수집은 BlastAttackBehavior.CollectEnemyUnitsInRadius를 그대로 재사용한다
//     (동일 로직 공용화 → 버섯폭격기와 유닛 수집 규칙이 완전히 일치, 회귀 없음).
//   - 건물 수집은 이 핸들러의 CollectEnemyBuildingsInRadius(신규)로 처리한다.
//
// ⚠️ 유닛/건물 hit-set(수집 리스트) 분리(규칙 29 교훈):
//   유닛 Id와 건물 Id는 서로 다른 카운터라 값이 겹칠 수 있으므로, 수집 리스트와 주 타깃 제외
//   판정을 유닛/건물 각각 분리해서 처리한다(유닛 Id로 건물을 지우는 사고 방지).
//
// 선수집 후 일괄 적용(2단계):
//   순회 도중 피해로 대상이 사망해 Dictionary에서 제거되면 컬렉션 변경 예외가 나므로,
//   수집(순회)과 적용(피해)을 반드시 두 단계로 분리한다(도끼병/버섯폭격기와 동일한 안전 패턴).
//
// Application 레이어 — Domain 의존 + UnityEngine.Vector3(Unity 의존 허용).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 대지의 정령 착탄형 즉발 AoE 구현. 착탄 중심(주 타깃 위치)에서 월드 원형 반경 안의
    /// 다른 적 유닛 + 적 건물(주 타깃 제외)에게 즉발 스플래시 피해(50%, 올림)를 준다.
    /// </summary>
    public sealed class QuakeAttackBehavior : ISpecialAttackBehavior
    {
        // 재사용 버퍼(모바일 GC 절감). 레지스트리에 1개 인스턴스만 존재하고,
        // 전투 판정은 서버/싱글 단일 스레드에서만 수행되므로 필드 재사용이 안전하다.
        // 유닛과 건물은 Id 카운터가 달라 값이 겹칠 수 있으므로 반드시 버퍼를 분리한다(규칙 29).
        private readonly List<UnitData> _unitVictims = new List<UnitData>(8);
        private readonly List<BuildingData> _buildingVictims = new List<BuildingData>(4);

        /// <summary>
        /// 착탄 스플래시는 "주 타깃 직접 100%에 얹는" 즉발 범위 피해이므로 주 타깃 단일 피해를 대체하지 않는다.
        /// (special-only가 아님 — ExecuteAttack이 주 타깃 직접 100%를 먼저 적용한 뒤 이 Apply가 실행됨.)
        /// </summary>
        public bool ReplacesPrimaryAttack => false;

        /// <summary>
        /// 착탄 반경 판정 후 반경 내 다른 적 유닛 + 적 건물(주 타깃 제외)에게 즉발 스플래시를 적용한다.
        /// </summary>
        /// <param name="ctx">공격자·주 타깃·유닛/건물 목록·스플래시 델리게이트·좌표 조회·착탄 반경 컨텍스트.</param>
        public void Apply(SpecialAttackContext ctx)
        {
            if (ctx == null) return;

            UnitData attacker = ctx.Attacker;
            if (attacker == null || !attacker.IsAlive) return;

            // 주 타깃이 없으면 착탄 중심을 정할 수 없으므로 아무것도 하지 않는다(방어적).
            if (ctx.PrimaryTarget == null) return;

            // 착탄 중심 = 주 타깃 월드 위치(유닛/건물 무관). Y(UnitYOffset)는 무시하고 XZ만 사용.
            Vector3 center = Flatten(ctx.WorldPositionOf(ctx.PrimaryTarget));

            float radius = ctx.QuakeRadius;
            float radiusSqr = radius * radius;

            // 주 타깃 제외 판정을 위해 주 타깃이 "유닛인지/건물인지"를 미리 구분해 둔다.
            // 유닛 Id와 건물 Id는 서로 다른 카운터라 값이 겹칠 수 있으므로, 각각의 타입에서만 제외한다.
            UnitData primaryUnit = ctx.PrimaryTarget as UnitData;
            bool hasPrimaryUnit = primaryUnit != null;
            int primaryUnitId = hasPrimaryUnit ? primaryUnit.Id : 0;

            BuildingData primaryBuilding = ctx.PrimaryTarget as BuildingData;
            bool hasPrimaryBuilding = primaryBuilding != null;
            int primaryBuildingId = hasPrimaryBuilding ? primaryBuilding.Id : 0;

            // 1) 반경 내 적 유닛 선수집 — 버섯폭격기와 동일한 공용 헬퍼 재사용(주 타깃 미제외 상태로 수집됨).
            _unitVictims.Clear();
            if (ctx.Units != null)
                BlastAttackBehavior.CollectEnemyUnitsInRadius(ctx, attacker, center, radiusSqr, _unitVictims);

            // 2) 반경 내 적 건물 선수집(신규 — 건물은 아군 건물 제외, 적 건물만).
            _buildingVictims.Clear();
            CollectEnemyBuildingsInRadius(ctx, attacker, center, radiusSqr, _buildingVictims);

            // 3) 수집된 적 유닛에 즉발 스플래시 적용 — 단, 주 타깃 유닛은 제외(주 타깃은 100%만 받음).
            for (int i = 0; i < _unitVictims.Count; i++)
            {
                UnitData victim = _unitVictims[i];
                if (victim == null || !victim.IsAlive) continue;          // 방어적 재확인
                if (hasPrimaryUnit && victim.Id == primaryUnitId) continue; // 주 타깃 유닛 스플래시 제외(이중 피해 금지)
                ctx.ApplyQuakeSplash(attacker, victim);
            }

            // 4) 수집된 적 건물에 즉발 스플래시 적용 — 단, 주 타깃 건물은 제외(주 타깃은 100%만 받음).
            for (int i = 0; i < _buildingVictims.Count; i++)
            {
                BuildingData victim = _buildingVictims[i];
                if (victim == null || !victim.IsAlive) continue;              // 방어적 재확인
                if (hasPrimaryBuilding && victim.Id == primaryBuildingId) continue; // 주 타깃 건물 스플래시 제외
                ctx.ApplyQuakeSplash(attacker, victim);
            }
        }

        /// <summary>
        /// 착탄 중심에서 월드 원형 반경(XZ 평면) 안의 "적 건물"을 수집한다.
        ///
        /// 버섯폭격기(유닛만)와 달리 대지의 정령은 건물도 스플래시 대상이므로, 유닛 수집
        /// (BlastAttackBehavior.CollectEnemyUnitsInRadius)과 대칭되는 건물 수집 헬퍼를 둔다.
        /// 아군 건물은 제외하고 적 건물만 담는다(규칙 16 — 아군 무피해).
        /// </summary>
        /// <param name="ctx">좌표 조회·건물 목록 컨텍스트.</param>
        /// <param name="attacker">공격자(팀 필터 기준 — 아군 건물 제외).</param>
        /// <param name="center">착탄 중심 월드 좌표(XZ 평면).</param>
        /// <param name="radiusSqr">반경의 제곱(거리 비교 시 제곱근을 피하려고 제곱값 사용).</param>
        /// <param name="result">수집 결과를 담을 리스트(호출자가 Clear 후 넘긴다).</param>
        internal static void CollectEnemyBuildingsInRadius(
            SpecialAttackContext ctx, UnitData attacker, Vector3 center,
            float radiusSqr, List<BuildingData> result)
        {
            if (ctx.Buildings == null) return;

            foreach (var building in ctx.Buildings.Values)
            {
                if (building == null || !building.IsAlive) continue;
                if (building.Team == attacker.Team) continue; // 아군 건물 제외(규칙 16). 적 건물만 대상.

                // 착탄 중심으로부터의 XZ 평면 거리(제곱)로 반경 안/밖 판정.
                // 건물 월드 위치도 서버 권위 좌표 조회(ResolveWorldPosition)를 통해 얻는다.
                Vector3 rel = Flatten(ctx.WorldPositionOf(building)) - center;
                if (rel.sqrMagnitude <= radiusSqr)
                    result.Add(building);
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
