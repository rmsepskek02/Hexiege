// ============================================================================
// UnitCombatUseCase.LineStrike.cs
// 공습(직선 연속 폭격) 폭격 1회 — 직사각형 구간 안 적 유닛 · 적 건물 수집 + 피해 적용.
// (2026-11-17 지원 스킬 2-L 공습 Task — _Tasks/2026-11-17/09_12_support-skill-air-strike, Plan L-1 · P-2 · P-3 · P-9 · P-13 · U-12 · U-14)
//
// 무엇을 하나(초급자용 설명):
//   SkillActivationUseCase가 서버 권위 틱에서 "k번째 폭격 시각이 됐다"고 판단하면 이 메서드를 1번 부른다.
//   ① 직사각형(시작점 · 방향 · 길이 · 반폭) 안에 있고, ② 그 점이 k번째 구간이며, ③ 맵 안 유효 길이 안인
//   적 유닛 · 적 건물을 모은 뒤, ④ 이번 시전에서 아직 안 맞은 대상에게만 피해 1회를 준다(U-12 — 시전당 대상 1회).
//   대상 위치 기준은 원형 스킬과 같은 「대상이 선 타일 중심」이다(스킬 규칙 27 · 28 — _mapper.HexToWorld(Position)).
//
// 왜 이 파일에 따로 두나:
//   피해 1회 적용은 기존 헬퍼 ApplySkillInstantDamageToVictim(UnitCombatUseCase.cs)을 그대로 부른다 —
//   방어 감쇄 → IncomingDamage.Apply(… Skill)(받는 피해 배율 → 쉴드 → HP) → 피격 이벤트 → 사망 처리 순서가 포격과 같다(유닛 규칙 44).
//   새 피해 경로를 만들지 않으므로 UnitCombatUseCase.cs 본문의 「IncomingDamage.Apply( 4곳」(회귀 스위트 S-T9 소스 게이트)은 그대로다.
//
// 「이미 맞은 대상」 기록(유닛 Id · 건물 Id를 따로 — 두 Id 카운터가 달라 같은 숫자가 겹칠 수 있다, 규칙 29 교훈)은
//   시전마다 SkillActivationUseCase가 들고 있다가 넘겨준다. 이 메서드는 맞힌 대상을 그 기록에 더한다.
//
// Application 레이어 — Domain(LineStrikeGeometry) 의존. Netcode 참조 없음. 서버(싱글 · Host)에서만 불린다(스킬 규칙 25).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public partial class UnitCombatUseCase
    {
        // 폭격 1회 대상 수집용 재사용 버퍼(GC 절감). 원형 스킬 버퍼(_skillUnitVictims 등)와 따로 둔다
        //   — 원형 착탄과 직선 폭격이 같은 틱에 섞여도 서로의 중간 상태를 덮어쓰지 않게.
        private readonly List<UnitData> _lineUnitVictims = new List<UnitData>(16);
        private readonly List<BuildingData> _lineBuildingVictims = new List<BuildingData>(8);

        /// <summary>
        /// 공습 폭격 1회 — 직사각형의 k번째 구간(∩ 맵 안 유효 길이) 안 적 유닛 · 적 건물 중 이번 시전에서 아직 안 맞은 대상에게
        /// 방어 감쇄를 적용한 피해 1회를 준다(기존 스킬 피해 헬퍼 경유). 아군 제외. 맞힌 대상은 기록 목록에 더한다.
        /// </summary>
        /// <param name="casterTeam">시전 팀(이 팀은 제외).</param>
        /// <param name="start">직선 시작점(도메인 월드).</param>
        /// <param name="direction">정규화된 방향(도메인 월드, Y = 0).</param>
        /// <param name="length">원래 직선 길이 L(구간은 이 길이로 나눈다 — P-13).</param>
        /// <param name="halfWidth">반폭(폭 ÷ 2).</param>
        /// <param name="effectiveLength">맵 안 유효 길이(이보다 먼 대상은 맞지 않는다 — U-14).</param>
        /// <param name="strikeIndex">이번 폭격 번호(0부터 — 0이 시작점 쪽 구간).</param>
        /// <param name="strikeCount">전체 폭격 횟수(= 구간 수).</param>
        /// <param name="rawDamage">감쇄 전 원본 피해(×10 스케일).</param>
        /// <param name="sourceBuildingId">시전 건물 Id(피격 연출 attribution — 건물이 이미 무너졌어도 그대로 쓴다, P-12).</param>
        /// <param name="hitUnitIds">이번 시전에서 이미 맞은 유닛 Id 기록(이 메서드가 더한다). null이면 중복 방지를 하지 않는다.</param>
        /// <param name="hitBuildingIds">이번 시전에서 이미 맞은 건물 Id 기록(이 메서드가 더한다).</param>
        /// <returns>이번 폭격 결과(맞은 수 · 건너뛴 수 · 사망 수 — 진단 로그용).</returns>
        public LineStrikeHitResult ApplySkillLineStrike(TeamId casterTeam, Vector3 start, Vector3 direction, float length,
                                                        float halfWidth, float effectiveLength, int strikeIndex, int strikeCount,
                                                        int rawDamage, int sourceBuildingId,
                                                        HashSet<int> hitUnitIds, HashSet<int> hitBuildingIds)
        {
            if (rawDamage <= 0 || !(length > 0f) || !(halfWidth > 0f)) return default;
            Vector3 s = Flatten(start);
            Vector3 dir = Flatten(direction);

            int alreadyHit = 0, outOfMap = 0, allies = 0;

            // 1) 구간 안 대상 선수집(피해 중 사망 제거로 목록이 바뀌는 것을 피하는 2단계 — 원형 스킬과 같은 이유).
            _lineUnitVictims.Clear();
            if (_unitSpawn != null)
            {
                foreach (var unit in _unitSpawn.Units.Values)
                {
                    if (unit == null || !unit.IsAlive) continue;
                    if (!IsInLineStrikeSegment(s, dir, length, halfWidth, effectiveLength, strikeIndex, strikeCount,
                            _mapper.HexToWorld(unit.Position), ref outOfMap)) continue;
                    if (unit.Team == casterTeam) { allies++; continue; }                        // 아군 제외(규칙 16)
                    if (hitUnitIds != null && hitUnitIds.Contains(unit.Id)) { alreadyHit++; continue; } // 시전당 1회(U-12)
                    _lineUnitVictims.Add(unit);
                }
            }
            _lineBuildingVictims.Clear();
            if (_buildingPlacement != null)
            {
                foreach (var building in _buildingPlacement.Buildings.Values)
                {
                    if (building == null || !building.IsAlive) continue;
                    if (!IsInLineStrikeSegment(s, dir, length, halfWidth, effectiveLength, strikeIndex, strikeCount,
                            _mapper.HexToWorld(building.Position), ref outOfMap)) continue;
                    if (building.Team == casterTeam) { allies++; continue; }
                    if (hitBuildingIds != null && hitBuildingIds.Contains(building.Id)) { alreadyHit++; continue; }
                    _lineBuildingVictims.Add(building);
                }
            }

            // 2) 피해 적용 — 맞기 "전에" 기록에 넣는다(피해 이벤트 구독자가 다시 폭격을 부르는 일이 생겨도 두 번 맞지 않게).
            int unitKills = 0, buildingKills = 0;
            for (int i = 0; i < _lineUnitVictims.Count; i++)
            {
                UnitData u = _lineUnitVictims[i];
                hitUnitIds?.Add(u.Id);
                ApplySkillInstantDamageToVictim(u, rawDamage, sourceBuildingId);
                if (!u.IsAlive) unitKills++;
            }
            for (int i = 0; i < _lineBuildingVictims.Count; i++)
            {
                BuildingData b = _lineBuildingVictims[i];
                hitBuildingIds?.Add(b.Id);
                ApplySkillInstantDamageToVictim(b, rawDamage, sourceBuildingId);
                if (!b.IsAlive) buildingKills++;
            }

            var result = new LineStrikeHitResult(_lineUnitVictims.Count, _lineBuildingVictims.Count, alreadyHit, outOfMap, allies,
                                                 unitKills, buildingKills);
            _lineUnitVictims.Clear();
            _lineBuildingVictims.Clear();
            return result;
        }

        // 한 점(대상 타일 중심)이 k번째 폭격의 판정 범위 안인가.
        //   직사각형 안 → 구간 번호가 k → 맵 안 유효 길이 안 순서로 본다. 구간은 맞지만 유효 길이 밖이면 outOfMap을 하나 센다.
        private static bool IsInLineStrikeSegment(Vector3 start, Vector3 dir, float length, float halfWidth, float effectiveLength,
                                                  int strikeIndex, int strikeCount, Vector3 targetWorld, ref int outOfMap)
        {
            Vector3 p = Flatten(targetWorld);
            if (!LineStrikeGeometry.IsInside(start.x, start.z, dir.x, dir.z, length, halfWidth, p.x, p.z, out float along))
                return false;
            if (LineStrikeGeometry.SegmentIndexOf(along, length, strikeCount) != strikeIndex) return false;
            if (along > effectiveLength + LineStrikeGeometry.BoundaryEpsilon)
            {
                outOfMap++;
                return false;
            }
            return true;
        }
    }
}
