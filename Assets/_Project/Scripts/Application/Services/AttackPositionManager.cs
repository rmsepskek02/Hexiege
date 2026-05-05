// ============================================================================
// AttackPositionManager.cs
// 근접 유닛이 같은 타겟(유닛/건물/성)을 공격할 때 Phase 1 추적 단계에서
// 모든 유닛이 동일 지점으로 수렴해 과도하게 뭉치는 현상을 방지하기 위한 매니저.
//
// 사용 배경:
//   UnitView의 Phase 1(월드 좌표 직선 추적)에서는 모든 근접 유닛이
//   적의 transform.position(=enemyViewPos) 한 지점을 향해 직선 이동한다.
//   같은 타겟을 공격하면 모든 유닛의 목표가 완전히 동일해져 한 점으로 수렴된다.
//
//   AttackPositionManager는 타겟을 중심으로 한 12방향(angular) 위치를 후보로 만들고,
//   각 유닛에게 가장 적합한 슬롯을 1회 배정한다. 유닛은 자신에게 배정된 슬롯
//   위치까지 직진하면, 슬롯이 이미 전투 사거리(유닛 0.3f / 건물 0.5f)에 있으므로
//   그 자리에서 곧바로 HasEnemyInRange가 true가 되어 자연스럽게 전투 루프로 진입한다.
//
// [7차 개선 — 2026-04-28] 슬롯 정의 변경 — 타일 기반 → 각도(angular) 기반
//   기존: 타겟의 walkable 인접 타일들의 중심/좌측경계/우측경계 위치 (최대 18개)
//   변경: 타겟을 중심으로 360°를 12등분(30°씩)한 12방향 위치 (정확히 12개)
//
//   기존 방식의 문제:
//     - 슬롯 위치가 타겟에서 0.75f~0.866f 떨어져 있어, 전투 사거리(0.3f / 0.5f)
//       보다 멀었음. 슬롯 도달 시 HasEnemyInRange가 false → 별도 2단계 전환 필요.
//     - 유닛이 출발 타일에서 전진한 상태에서 새 타겟의 슬롯을 받으면, 본인의
//       "출발 타일"이 가장 가까운 슬롯으로 배정되어 뒤로 이동하는 왕복 현상.
//
//   각도 기반 방식의 장점:
//     - 슬롯 위치 = 타겟 위치 + 방향벡터 × contactRadius
//       contactRadius로 유닛(0.3f) / 건물(0.5f) 사거리에 정확히 맞출 수 있음.
//     - 슬롯이 타겟 근처에만 있으므로 후방에 슬롯이 생기는 일 자체가 발생 가능하나,
//       12방향 모두 타겟에 인접해 있어 "출발 타일로 후퇴"하는 거리상 왕복은 사라짐.
//     - 슬롯 도달 = 전투 사거리 도달 → 2단계 전환 로직 불필요.
//
// 슬롯 점유 정책:
//   슬롯 위치 1개당 동시 배정 가능 유닛 수 = MaxUnitsPerSlot(=2).
//   12 × 2 = 최대 24개 유닛이 한 타겟을 동시에 분산 공격하는 상황을 가정.
//
// 슬롯 선택 규칙(우선순위 순):
//   1) 같은 unitId가 이미 등록되어 있으면 기존 배정 재사용 — 중복 Claim 안전.
//   2) 후보 위치들 중 "현재 배정된 유닛 수가 가장 적은 위치"를 선호.
//   3) 동점이면 unitViewPos에서 가장 가까운 위치(Vector3.Distance) 선택.
//   4) 모든 슬롯이 MaxUnitsPerSlot을 초과해도 가장 적게 배정된 위치를 fallback —
//      무한 대기 없이 항상 어딘가로는 배정된다.
//
// 멀티플레이 정책:
//   Phase 1 이동 자체가 서버에서만 실행되므로 슬롯 배정도 서버에서만 일어난다.
//   클라이언트는 NetworkTransform이 서버 위치를 보간 동기화하므로 별도 동기화 불필요.
//
// Application 레이어 — UnityEngine.Vector3에 의존하나 MonoBehaviour는 아님.
// (선례: TileSlotManager, UnitCombatUseCase, IEntityPositionProvider도 UnityEngine 사용)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Core;

namespace Hexiege.Application
{
    public class AttackPositionManager
    {
        // 슬롯 위치 1개당 동시에 배정 가능한 유닛 수.
        // 12위치 × 2 = 한 타겟을 최대 24개 유닛이 동시 공격하는 상황까지 분산 가능.
        // 이 값을 초과하면 가장 적게 배정된 위치를 fallback으로 공유.
        // 추후 게임 밸런스 테스트에 따라 조정 가능.
        private const int MaxUnitsPerSlot = 2;

        // Vector3 동등 비교에 쓰는 거리 임계값.
        // 부동소수점 오차로 인해 같은 위치라도 == 비교가 불안정할 수 있어,
        // Vector3.Distance < SamePositionEpsilon이면 같은 위치로 간주한다.
        // 12방향 사이 최소 거리(약 0.15f 이상, contactRadius=0.3f 기준)에 비해
        // 충분히 작은 값이라 다른 슬롯끼리 같은 슬롯으로 오인될 위험은 없다.
        private const float SamePositionEpsilon = 0.01f;

        // 타겟 주변 360°를 몇 등분할지 — 12방향(30°씩).
        // 6방향(60°씩)이면 헥스 인접과 비슷해 분산이 거칠고,
        // 24방향(15°씩)이면 위치 간 거리가 너무 가까워 점유 카운트 의미가 옅어진다.
        // 12방향이 적당한 분산도와 위치 변별력을 동시에 만족한다.
        private const int DirectionCount = 12;

        // 한 방향당 회전 각도(°). 360 / DirectionCount.
        // 인덱스 i가 갖는 각도는 i * DirectionStep (0°, 30°, 60°, ..., 330°).
        private const float DirectionStep = 30f;

        // 슬롯 점유 상태.
        // 외부 키: 타겟 타일 좌표 (공격 대상 유닛/건물/성의 타일).
        // 내부 사전:
        //   키 = 슬롯을 점유한 유닛 Id.
        //   값 = 해당 유닛에게 배정된 "도메인 좌표계 슬롯 월드 좌표" (HexMetrics.HexToWorld 기준).
        //         - angular 위치는 정수 좌표(HexCoord)로 표현 불가능하므로 Vector3로 보관한다.
        //         - 도메인 좌표로 보관하는 이유: 점유 수 카운트는 팀별 ViewConverter 변환 전에
        //           수행되어야 모든 유닛(아군/적군 시점 차이 무관)에게 일관된 분산 결과를 준다.
        // 이 구조로 "타겟별 누가 어디를 차지했는지"를 O(1)로 추적할 수 있다.
        private readonly Dictionary<HexCoord, Dictionary<int, Vector3>> _assignments
            = new Dictionary<HexCoord, Dictionary<int, Vector3>>();

        // ClaimAttackSlot 내부에서 후보 위치 리스트를 매번 재생성하지 않도록 재사용하는 버퍼.
        // 한 호출 안에서만 채워지고 비워지므로 동시 호출은 가정하지 않는다(서버 단일 스레드).
        // 초기 용량 12는 DirectionCount에 맞춤 — 재할당 발생하지 않음.
        // (각도 기반은 구조적으로 중복이 없으므로 별도의 중복 제거 로직 불필요)
        private readonly List<Vector3> _candidateBuffer = new List<Vector3>(DirectionCount);

        /// <summary>
        /// 생성자. 기존 grid 의존이 제거되어 매개변수가 없다.
        /// (각도 기반 슬롯 생성은 그리드 인접 정보를 사용하지 않는다)
        /// </summary>
        public AttackPositionManager()
        {
        }

        /// <summary>
        /// 타겟 타일 주변에서 해당 유닛에게 가장 적합한 공격 슬롯 위치를 배정하고,
        /// 그 위치의 뷰 월드 좌표(유닛 발 위치 기준 + UnitYOffset)를 반환한다.
        ///
        /// [7차 개선 — 2026-04-28] 슬롯 위치 = 타겟 도메인 좌표 + 방향벡터 × contactRadius.
        /// contactRadius는 호출 측이 전투 사거리에 맞춰 전달한다(유닛 타겟=0.3f, 건물 타겟=0.5f).
        ///
        /// 동작 순서:
        ///   1) 같은 unitId가 이미 등록되어 있으면 기존 배정을 재사용한다(중복 안전).
        ///   2) 타겟 도메인 좌표를 중심으로 12방향(30°씩) 위치를 후보 리스트에 추가한다.
        ///      candidatePos = HexMetrics.HexToWorld(targetCoord) + dir × contactRadius.
        ///   3) 각 후보 위치의 현재 배정 유닛 수를 센다.
        ///   4) "배정 수가 가장 적은 위치" 우선, 동점이면 unitViewPos와 가장 가까운 위치 선택.
        ///   5) MaxUnitsPerSlot 초과 시에도 가장 적은 위치로 fallback (무한 대기 없음).
        ///   6) 선택된 도메인 좌표를 _assignments[targetCoord][unitId]에 저장하고,
        ///      ViewConverter로 뷰 좌표 변환 후 UnitYOffset을 더해 반환한다.
        /// </summary>
        /// <param name="targetCoord">공격 대상의 타일 좌표(유닛/건물/성 타일).</param>
        /// <param name="unitId">슬롯을 점유할 유닛 Id.</param>
        /// <param name="unitViewPos">유닛의 현재 뷰 월드 좌표 — 동점 시 가장 가까운 슬롯 선택에 사용.</param>
        /// <param name="contactRadius">타겟 중심에서 슬롯까지의 거리(전투 사거리). 유닛 타겟=0.3f, 건물 타겟=0.5f.</param>
        /// <returns>배정된 슬롯의 뷰 월드 좌표 (유닛 발 위치 + UnitYOffset).</returns>
        public Vector3 ClaimAttackSlot(HexCoord targetCoord, int unitId, Vector3 unitViewPos, float contactRadius)
        {
            // 타겟별 점유 사전을 가져오거나 새로 만든다.
            if (!_assignments.TryGetValue(targetCoord, out var slots))
            {
                slots = new Dictionary<int, Vector3>();
                _assignments[targetCoord] = slots;
            }

            // 같은 유닛이 이미 등록돼 있으면 기존 슬롯을 재사용한다.
            // (Phase 1 진입 시 1회 + 타겟 변경 시 추가 호출 등에서 중복 호출 가능)
            if (slots.TryGetValue(unitId, out Vector3 existingDomainPos))
            {
                return ToViewWithUnitYOffset(existingDomainPos);
            }

            // ----------------------------------------------------------------
            // 후보 위치 생성 — 12방향 angular (정확히 DirectionCount개)
            // ----------------------------------------------------------------
            // 도메인 좌표계(HexMetrics.HexToWorld)에서 위치를 계산한다.
            // 뷰 좌표(ViewConverter.ToView)는 팀별로 회전될 수 있어 점유 카운트의 기준이
            // 흔들릴 수 있기 때문이다. 도메인 좌표는 모든 유닛에게 동일하게 보인다.
            //
            // 각도 i (i = 0..DirectionCount-1):
            //   angle  = i * DirectionStep           (0°, 30°, 60°, ..., 330°)
            //   dir    = Quaternion.Euler(0, angle, 0) * Vector3.forward
            //   pos    = targetDomainPos + dir * contactRadius
            //
            // 각 위치는 타겟 중심에서 정확히 contactRadius만큼 떨어져 있으므로,
            // 슬롯에 도달한 유닛은 곧바로 전투 사거리 안에 들어가 HasEnemyInRange = true가 된다.
            _candidateBuffer.Clear();
            Vector3 targetDomainPos = HexMetrics.HexToWorld(targetCoord);

            for (int i = 0; i < DirectionCount; i++)
            {
                float angle = i * DirectionStep;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 candidatePos = targetDomainPos + dir * contactRadius;
                _candidateBuffer.Add(candidatePos);
            }

            // ----------------------------------------------------------------
            // 후보 위치별 점유 수 계산 + 1순위/2순위 비교로 최적 후보 선택
            // ----------------------------------------------------------------
            // bestPos      : 현재까지 최고로 적합한 후보 위치 (도메인 좌표).
            // bestCount    : 그 후보 위치의 현재 배정 유닛 수 (작을수록 우선).
            // bestDistSq   : 그 후보 위치의 unitViewPos까지의 거리 제곱 (동점 시 작을수록 우선).
            //                sqrMagnitude는 sqrt 계산이 없어 비교에 더 빠르다.
            Vector3 bestPos = _candidateBuffer[0];
            int bestCount = int.MaxValue;
            float bestDistSq = float.MaxValue;

            for (int c = 0; c < _candidateBuffer.Count; c++)
            {
                Vector3 candidatePos = _candidateBuffer[c];

                // 이 위치에 현재 배정된 유닛 수를 센다.
                // _assignments[targetCoord]의 값들 중 candidatePos와 같은 위치를 가진 항목 개수.
                // (Vector3.Distance < SamePositionEpsilon으로 부동소수점 오차 흡수)
                int count = 0;
                foreach (var pair in slots)
                {
                    if (Vector3.Distance(pair.Value, candidatePos) < SamePositionEpsilon)
                        count++;
                }

                // 동점 비교용 거리 제곱 — unitViewPos는 뷰 좌표이므로 candidatePos도 뷰 변환 후 비교.
                Vector3 candidateViewPos = ToViewWithUnitYOffset(candidatePos);
                float distSq = (candidateViewPos - unitViewPos).sqrMagnitude;

                // 1순위: 배정 유닛 수가 더 적은 위치 우선.
                // 2순위: 같은 배정 수라면 unitViewPos에서 더 가까운 위치 우선.
                bool better = count < bestCount
                              || (count == bestCount && distSq < bestDistSq);

                if (better)
                {
                    bestPos = candidatePos;
                    bestCount = count;
                    bestDistSq = distSq;
                }
            }

            // 결과 등록. MaxUnitsPerSlot 초과해도 가장 적게 배정된 위치를 그대로 사용 (fallback).
            // 이렇게 하면 어떤 상황에서도 무한 대기 없이 "어딘가로는" 배정된다.
            // (MaxUnitsPerSlot은 현재 정보 제공용 — 향후 다른 정책 도입 시 활용 가능)
            _ = MaxUnitsPerSlot;
            slots[unitId] = bestPos;

            return ToViewWithUnitYOffset(bestPos);
        }

        // ====================================================================
        // [2026-04-30] 새 이동/전투 규칙(GameSystemRules.md 규칙 11/18) — 접근 방향 기반 배정
        // ====================================================================

        /// <summary>
        /// [2026-04-30] 새 규칙용 슬롯 배정 메서드.
        /// 기존 ClaimAttackSlot의 우선순위(점유 수 → 거리)와 달리,
        /// "접근 방향(approach)과 가장 가까운 각도의 빈 슬롯"을 1순위로 선택한다.
        ///
        /// 규칙 18 발췌:
        ///   유닛은 접근 방향과 가장 가까운 각도의 빈 슬롯에 배정된다.
        ///   앞쪽 슬롯이 모두 찬 경우 순차적으로 측면, 뒤쪽 슬롯이 배정되어 자연스러운 포위 형태가 된다.
        ///
        /// approachVecDomain은 도메인 좌표 기준 (유닛→적) 단위 벡터(또는 임의 길이 벡터).
        ///   - 방향 정보만 사용하므로 길이는 무관 (영벡터일 경우 +Z로 폴백).
        ///   - "유닛 위치 → 적 위치" 벡터를 그대로 넘기는 것을 권장(D5 결정).
        ///
        /// 배정 규칙(D2: 1 per slot + fallback):
        ///   1) 같은 unitId가 이미 등록되어 있으면 기존 배정 재사용 (중복 안전).
        ///   2) 12방향 후보 중 빈 슬롯(점유자 0)만 우선 후보로 둔다.
        ///   3) 후보 중 approach 벡터와 슬롯 방향 벡터의 각도 차이가 가장 작은 슬롯을 선택한다.
        ///      (slotDir = (slotPos - targetPos).normalized, approachDir.normalized)
        ///   4) 빈 슬롯이 없으면 fallback — 가장 적게 점유된 슬롯 중 approach와 가장 가까운 각도.
        ///
        /// 반환:
        ///   배정된 슬롯의 뷰 월드 좌표 (UnitYOffset 포함). 유닛은 이 좌표로 직진하면 도달 시 전투 사거리에 들어간다.
        /// </summary>
        /// <param name="targetCoord">공격 대상 타일 좌표.</param>
        /// <param name="unitId">슬롯을 점유할 유닛 Id.</param>
        /// <param name="unitWorldPosDomain">동률 시 거리 비교용 — 유닛의 도메인 좌표 권장. (현재는 fallback에서만 사용)</param>
        /// <param name="approachVecDomain">접근 방향 벡터(도메인 좌표 기준, XZ 평면). 영벡터면 +Z로 폴백.</param>
        /// <param name="contactRadius">타겟 중심에서 슬롯까지의 거리(전투 사거리). 유닛 타겟=0.3f, 건물 타겟=0.5f.</param>
        /// <returns>배정된 슬롯의 뷰 월드 좌표.</returns>
        public Vector3 ClaimByApproach(HexCoord targetCoord, int unitId,
            Vector3 unitWorldPosDomain, Vector3 approachVecDomain, float contactRadius)
        {
            // 타겟별 점유 사전 가져오기/생성.
            if (!_assignments.TryGetValue(targetCoord, out var slots))
            {
                slots = new Dictionary<int, Vector3>();
                _assignments[targetCoord] = slots;
            }

            // 같은 유닛이 이미 등록돼 있으면 기존 슬롯 재사용 — 중복 호출 안전.
            if (slots.TryGetValue(unitId, out Vector3 existingDomainPos))
            {
                return ToViewWithUnitYOffset(existingDomainPos);
            }

            // approach 벡터를 XZ 평면 단위 벡터로 정규화.
            // 영벡터면 +Z로 폴백 — 결과적으로 "기본 위쪽 방향"에서 슬롯을 찾게 됨.
            Vector3 approach = NormalizeXZ(approachVecDomain);

            // 12방향 후보를 도메인 좌표로 만들고 angular 비교 준비.
            // 후보별 angle = approach 벡터와 슬롯 방향(타겟→슬롯) 사이의 각도(0~180°).
            // 0°에 가까울수록 "정면에서 접근"이라는 의미이므로 우선 배정.
            _candidateBuffer.Clear();
            Vector3 targetDomainPos = HexMetrics.HexToWorld(targetCoord);

            // 1차 패스: 빈 슬롯(점유 수 0) 후보들 중 approach와 가장 가까운 각도.
            // 동시에 fallback 후보도 같이 추적한다(빈 슬롯이 없을 때 사용).
            int bestEmptyIdx = -1;
            float bestEmptyAngleSq = float.MaxValue;

            int bestAnyIdx = -1;
            int bestAnyCount = int.MaxValue;
            float bestAnyAngleSq = float.MaxValue;

            for (int i = 0; i < DirectionCount; i++)
            {
                float angleDeg = i * DirectionStep;
                Vector3 dir = Quaternion.Euler(0f, angleDeg, 0f) * Vector3.forward;
                Vector3 candidatePos = targetDomainPos + dir * contactRadius;
                _candidateBuffer.Add(candidatePos);

                // 슬롯 방향과 approach 사이의 각도 비교용 코사인 → 각도(°) 환산하지 않고
                // (1 - dot)을 "각도 거리 대용"으로 사용하면 정렬 결과가 동일하면서 비용이 더 싸다.
                // dot이 클수록(=1에 가까울수록) 각도가 작다 → "각도 거리 대용" = (1 - dot).
                // approach와 dir 모두 단위 벡터(approach는 NormalizeXZ로 길이 1, dir도 회전 결과 길이 1)이므로 안전.
                float dot = Vector3.Dot(approach, dir);
                float angleProxy = 1f - dot; // 0(완전 일치) ~ 2(정반대).

                int count = CountAt(slots, candidatePos);

                if (count == 0)
                {
                    // 빈 슬롯 후보군. 더 작은 angleProxy를 우선.
                    if (angleProxy < bestEmptyAngleSq)
                    {
                        bestEmptyAngleSq = angleProxy;
                        bestEmptyIdx = i;
                    }
                }

                // fallback 후보군: 점유 수가 적은 것 우선, 동률이면 angleProxy가 작은 것.
                bool better = count < bestAnyCount
                              || (count == bestAnyCount && angleProxy < bestAnyAngleSq);
                if (better)
                {
                    bestAnyCount = count;
                    bestAnyAngleSq = angleProxy;
                    bestAnyIdx = i;
                }
            }

            // 빈 슬롯이 있으면 그쪽, 없으면 fallback.
            int chosenIdx = bestEmptyIdx >= 0 ? bestEmptyIdx : bestAnyIdx;

            // 안전장치: 어떤 이유로든 chosenIdx가 음수이면 0번으로 폴백.
            if (chosenIdx < 0) chosenIdx = 0;

            Vector3 chosenDomainPos = _candidateBuffer[chosenIdx];
            slots[unitId] = chosenDomainPos;
            return ToViewWithUnitYOffset(chosenDomainPos);
        }

        /// <summary>
        /// 해당 유닛이 점유 중이던 공격 슬롯을 해제한다.
        /// 유닛 사망 / Phase 1 종료(break) / 타겟 변경 / StopMovement 등 모든 종료 경로에서 호출.
        /// 내부 사전이 비면 외부 사전 항목도 제거하여 메모리 누수를 방지한다.
        /// </summary>
        /// <param name="targetCoord">슬롯이 등록되어 있던 타겟 타일 좌표.</param>
        /// <param name="unitId">슬롯을 해제할 유닛 Id.</param>
        public void ReleaseAttackSlot(HexCoord targetCoord, int unitId)
        {
            if (!_assignments.TryGetValue(targetCoord, out var slots)) return;

            slots.Remove(unitId);

            // 내부 사전이 비면 외부도 같이 제거 — 누적되는 빈 항목 방지.
            if (slots.Count == 0)
                _assignments.Remove(targetCoord);
        }

        /// <summary>
        /// 모든 공격 슬롯 점유 정보를 초기화한다. 맵 전환/재경기 시 호출.
        /// </summary>
        public void Clear()
        {
            _assignments.Clear();
        }

        // -------------------------------------------------------------------
        // 내부 헬퍼
        // -------------------------------------------------------------------

        /// <summary>
        /// 도메인 좌표(HexMetrics 기준 월드 좌표) → 뷰 월드 좌표(유닛 발 높이 보정).
        /// ViewConverter.ToView로 팀별 시점 변환을 적용하고, Y에 UnitYOffset을 더한다.
        /// </summary>
        private static Vector3 ToViewWithUnitYOffset(Vector3 domainPos)
        {
            Vector3 viewPos = ViewConverter.ToView(domainPos);
            viewPos.y += HexMetrics.UnitYOffset;
            return viewPos;
        }

        /// <summary>
        /// [2026-04-30] 슬롯 위치(candidatePos)에 현재 몇 명이 배정돼 있는지 센다.
        /// SamePositionEpsilon 기준으로 동일 위치를 같은 슬롯으로 간주한다.
        /// 빈 슬롯/포화 fallback 판정에 사용.
        /// </summary>
        private static int CountAt(Dictionary<int, Vector3> slots, Vector3 candidatePos)
        {
            int count = 0;
            foreach (var pair in slots)
            {
                if (Vector3.Distance(pair.Value, candidatePos) < SamePositionEpsilon)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// [2026-04-30] 입력 벡터를 XZ 평면 단위 벡터로 정규화.
        /// Y 성분을 0으로 강제하고, 영벡터/매우 짧은 벡터는 +Z로 폴백한다.
        /// </summary>
        private static Vector3 NormalizeXZ(Vector3 input)
        {
            Vector3 v = new Vector3(input.x, 0f, input.z);
            float sqr = v.sqrMagnitude;
            const float epsilonSqr = 1e-6f;
            if (sqr < epsilonSqr)
            {
                return new Vector3(0f, 0f, 1f);
            }
            return v / Mathf.Sqrt(sqr);
        }
    }
}
