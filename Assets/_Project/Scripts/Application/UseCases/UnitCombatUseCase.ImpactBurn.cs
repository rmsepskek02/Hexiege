// ============================================================================
// UnitCombatUseCase.ImpactBurn.cs
// 타입 A(즉발 범위 피해) + 착탄 화상 — 지원 스킬 2-D 메테오(2026-11-18).
// Task: Assets/_Project/Docs/_Tasks/2026-11-18/03_34_support-skill-meteor/ (Plan P-1 · P-2 · P-3 · U-5 · U-7 · M-2 · M-3).
//
// 무엇을 하나(초급자용 설명):
//   메테오는 「떨어지는 순간 원 안의 적 유닛 · 적 건물에게 피해 1회」(기존 포격과 같은 ApplySkillInstantAreaDamage) 뒤에,
//   그 피해를 맞고도 살아 남은 「적 유닛」에게 화상을 하나 더 건다. 이 파일은 그 한 단계를 기존 피해 메서드 "뒤에" 덧붙인다.
//   기존 피해 메서드(ApplySkillInstantAreaDamage)는 한 줄도 바꾸지 않는다 → 화상 칸이 0인 포격은 지금과 같은 경로 · 같은 결과다.
//
// 대상을 정하는 기준(사용자 결정 · 상위 결정):
//   - 착탄 "순간" 원 안에 살아 있는 적 유닛만 후보다(피해 판정과 같은 기준 — 타일 중심 ↔ 조준점 거리 ≤ 반경).
//     → 1.5초 사이에 원 밖으로 나간 유닛은 피해도 화상도 없다. 착탄 뒤에 들어온 유닛도 없다(상위 O-3 결정 4).
//   - 그중 착탄 피해로 죽은 유닛은 뺀다(죽은 유닛에 화상을 거는 것은 의미가 없다 — ApplyDamageOverTime 첫 줄도 거른다).
//   - 건물은 화상 대상이 아니다(상위 결정 18 — 지속 피해 장치는 유닛 전용이기도 하다).
//
// 왜 후보를 "피해 전에" 따로 모으나:
//   기존 피해 메서드 안의 재사용 버퍼(_skillUnitVictims)를 피해 뒤에 다시 읽으면, 피해 이벤트를 받은 다른 코드가 같은 버퍼를
//   쓰는 스킬 경로를 부르는 경우 덮어써질 수 있다. 같은 기준으로 이 파일 전용 버퍼에 먼저 모아 두면 그런 걱정이 없다(같은 틱 · 같은
//   좌표라 두 번 모아도 결과가 같다 — 비용은 유닛 수만큼의 거리 계산 1번).
//
// 화상은 기존 지속 피해 장치를 그대로 쓴다(유닛 규칙 34 · 40 — 1초 틱 · 올림 · 총량 상한 · 틱마다 텍스트):
//   ApplyDamageOverTime(source: null, …, TimedDamageVisual.Burn) → 출처 −1(건물 · 스킬), 표시 = InfernoSpirit 화상 프리셋.
//   이미 지속 피해가 붙어 있으면 새 기록을 만들지 않고 그 기록을 새 값으로 갱신한다(지속 리셋 · 값 = 새 쪽 — M-2, Plan 1-3).
//   화상 틱 피해는 기존처럼 IncomingDamage.Apply(…, DoT)를 거친다 — 방어 감쇄 없음, 대지의 갑주 · 쉴드는 적용(유닛 규칙 44).
//
// Application 레이어 — Domain 의존. Unity.Netcode 직접 참조 없음. 서버 권위 쪽(싱글 · Host)에서만 불린다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 착탄 화상 1회 적용 결과(진단 로그 · 자가 검증용 값). 게임 동작에는 쓰지 않는다.
    /// </summary>
    public readonly struct SkillImpactBurnResult
    {
        /// <summary> 착탄 순간 원 안에 살아 있던 적 유닛 수(피해 전). </summary>
        public readonly int UnitsInCircle;
        /// <summary> 착탄 순간 원 안에 살아 있던 적 건물 수(피해 전 — 화상은 걸지 않는다, 비교용). </summary>
        public readonly int BuildingsInCircle;
        /// <summary> 착탄 피해 뒤에도 살아 있던 적 유닛 수(= 화상 후보). </summary>
        public readonly int Survivors;
        /// <summary> 실제로 화상이 걸린(또는 갱신된) 유닛 수 — 지속 피해 표현 상태가 Burn · 출처 −1로 바뀐 것만 센다. </summary>
        public readonly int BurnGranted;
        /// <summary> 그중 이미 화상(Burn)이 붙어 있어 「갱신」된 유닛 수(출처 불문 — InfernoSpirit · 다른 메테오). </summary>
        public readonly int RefreshedBurn;
        /// <summary> 그중 화상이 아닌 지속 피해(독 등)가 붙어 있다가 덮어쓰인 유닛 수 — 실제 경기에서는 0이어야 한다(Plan 1-3). </summary>
        public readonly int ReplacedOther;

        public SkillImpactBurnResult(int unitsInCircle, int buildingsInCircle, int survivors, int burnGranted, int refreshedBurn, int replacedOther)
        {
            UnitsInCircle = unitsInCircle;
            BuildingsInCircle = buildingsInCircle;
            Survivors = survivors;
            BurnGranted = burnGranted;
            RefreshedBurn = refreshedBurn;
            ReplacedOther = replacedOther;
        }
    }

    public partial class UnitCombatUseCase
    {
        // 착탄 화상 후보(착탄 순간 원 안 살아 있는 적 유닛) 전용 재사용 버퍼 — 피해 메서드의 버퍼(_skillUnitVictims)와 따로 둔다(머리말).
        private readonly List<UnitData> _impactBurnCandidates = new List<UnitData>(16);
        // 진단용 — 착탄 순간 원 안 적 건물 수만 세려고 쓰는 재사용 버퍼(화상 대상 아님).
        private readonly List<BuildingData> _impactBurnBuildingProbe = new List<BuildingData>(8);

        /// <summary>
        /// 타입 A 착탄 1회 + 착탄 화상(메테오). 기존 즉발 범위 피해(ApplySkillInstantAreaDamage)를 그대로 부른 뒤,
        /// 착탄 순간 원 안에 있었고 피해 뒤에도 살아 있는 적 유닛에게 화상(Burn, 출처 −1)을 건다. 건물에는 걸지 않는다.
        /// 화상 값이 0 이하이면 피해만 주고 화상 단계를 건너뛴다(= 기존 메서드와 같은 결과).
        /// </summary>
        /// <param name="casterTeam">시전 팀(아군/적 판정 기준).</param>
        /// <param name="center">착탄 중심(도메인 월드 좌표).</param>
        /// <param name="radius">원형 반경(월드 단위).</param>
        /// <param name="rawDamage">감쇄 전 착탄 피해(×10 스케일). 0 이하이면 피해 없이 화상만 건다.</param>
        /// <param name="sourceBuildingId">시전 건물 Id(피해 출처 표기 — 건물 생존은 보지 않는다, U-5).</param>
        /// <param name="burnPerSecond">화상 초당 피해(×10 스케일).</param>
        /// <param name="burnDuration">화상 지속 시간(초).</param>
        /// <returns>진단 · 검증용 결과(원 안 유닛 · 건물 수, 생존자, 화상 수, 갱신 수).</returns>
        public SkillImpactBurnResult ApplySkillInstantAreaDamageWithBurn(TeamId casterTeam, Vector3 center, float radius, int rawDamage,
                                                                         int sourceBuildingId, float burnPerSecond, float burnDuration)
        {
            // ① 착탄 "직전" 원 안 적 유닛 · 건물을 피해 판정과 같은 기준으로 모은다(반경이 0 이하 · NaN이면 아무도 없다).
            _impactBurnCandidates.Clear();
            _impactBurnBuildingProbe.Clear();
            if (radius > 0f && _unitSpawn != null && _buildingPlacement != null)
            {
                Vector3 centerWorld = Flatten(center);
                float radiusSqr = radius * radius;
                CollectEnemyUnitsInRadiusDomain(casterTeam, centerWorld, radiusSqr, _impactBurnCandidates);
                CollectEnemyBuildingsInRadiusDomain(casterTeam, centerWorld, radiusSqr, _impactBurnBuildingProbe);
            }
            int unitsInCircle = _impactBurnCandidates.Count;
            int buildingsInCircle = _impactBurnBuildingProbe.Count;
            _impactBurnBuildingProbe.Clear();

            // ② 착탄 피해 — 기존 메서드 그대로(유닛 · 건물 · 방어 감쇄 · 사망 처리 · HP 동기화 이벤트). 포격과 같은 경로다.
            ApplySkillInstantAreaDamage(casterTeam, center, radius, rawDamage, sourceBuildingId);

            // ③ 착탄 화상 — 값이 없으면 건너뛴다(화상 칸 0 = 포격과 같은 결과).
            int survivors = 0, granted = 0, refreshedBurn = 0, replacedOther = 0;
            bool hasBurn = burnPerSecond > 0f && burnDuration > 0f
                           && !float.IsInfinity(burnPerSecond) && !float.IsInfinity(burnDuration);
            for (int i = 0; i < _impactBurnCandidates.Count; i++)
            {
                UnitData victim = _impactBurnCandidates[i];
                if (victim == null || !victim.IsAlive) continue; // 착탄 피해로 죽은 유닛 — 화상 없음.
                survivors++;
                if (!hasBurn) continue;

                // 걸기 전에 지금 붙어 있는 지속 피해를 본다(진단 — 갱신인가, 다른 종류를 덮어쓰는가).
                bool hadDamage = TimedDamageEffects.TryGet(victim, out TimedDamageEffectPresentation before) && before.Active;
                // 건물 · 스킬 출처라 source = null(출처 −1). 1초 틱(InfernoSpirit · MushroomBomber와 같은 BlastDotTickInterval).
                ApplyDamageOverTime(null, victim, burnPerSecond, burnDuration, BlastDotTickInterval, TimedDamageVisual.Burn);

                // 실제로 걸렸는지 표현 상태로 확인한다(값이 안 맞아 거부되면 세지 않는다 — 조용한 무효과를 로그로 드러냄).
                if (TimedDamageEffects.TryGet(victim, out TimedDamageEffectPresentation after) && after.Active
                    && after.Visual == TimedDamageVisual.Burn && after.SourceId == -1)
                {
                    granted++;
                    if (hadDamage)
                    {
                        if (before.Visual == TimedDamageVisual.Burn) refreshedBurn++;
                        else replacedOther++;
                    }
                }
            }
            _impactBurnCandidates.Clear();
            return new SkillImpactBurnResult(unitsInCircle, buildingsInCircle, survivors, granted, refreshedBurn, replacedOther);
        }
    }
}
