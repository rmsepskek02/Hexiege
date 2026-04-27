// ============================================================================
// AttackPositionManager.cs
// 근접 유닛이 같은 타겟(건물/성)을 공격할 때 Phase 1 추적 단계에서
// 모든 유닛이 동일 지점으로 수렴해 과도하게 뭉치는 현상을 방지하기 위한 매니저.
//
// 사용 배경:
//   UnitView의 Phase 1(월드 좌표 직선 추적)에서는 모든 근접 유닛이
//   적의 transform.position(=enemyViewPos) 한 지점을 향해 직선 이동한다.
//   같은 건물을 공격하면 모든 유닛의 목표가 완전히 동일해져 한 점으로 수렴된다.
//
//   AttackPositionManager는 타겟의 walkable 인접 타일들을 기반으로 최대 18개의
//   "공격 슬롯 위치"를 만들고, 각 유닛에게 가장 적합한 슬롯을 1회 배정한다.
//   유닛은 자신에게 배정된 슬롯 위치까지 직진하다가, UnitView에서 슬롯에 도달하면
//   목표를 enemyViewPos로 전환해 전투 사거리(MeleeContactDist=0.3f, 건물=0.5f)까지
//   직진하여 자연스럽게 전투 루프로 진입한다.
//
// 슬롯 위치 정의 (인접 타일 1개당 최대 3개 위치):
//   타겟 타일의 walkable 인접 타일이 N개 있을 때 (N = 0~6):
//     ① 중심 위치    : HexMetrics.HexToWorld(neighbors[i])
//     ② 좌측경계 위치: (HexMetrics.HexToWorld(neighbors[i])
//                       + HexMetrics.HexToWorld(neighbors[(i+1)%N])) / 2
//     ③ 우측경계 위치: (HexMetrics.HexToWorld(neighbors[i])
//                       + HexMetrics.HexToWorld(neighbors[(i+N-1)%N])) / 2
//   ※ 좌/우 경계 위치는 인접 타일이 2개 이상일 때만 생성된다(N==1이면 중심만).
//   ※ 거리 참고:
//        중심      → 타겟으로부터 약 0.866f
//        좌/우경계 → 타겟으로부터 약 0.75f
//   ※ 이론상 최대 슬롯 수: 6 × 3 = 18개.
//
// 슬롯 점유 정책:
//   슬롯 위치 1개당 동시 배정 가능 유닛 수 = MaxUnitsPerSlot(=2).
//   18 × 2 = 최대 36개 유닛이 한 타겟을 동시에 분산 공격하는 상황을 가정.
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
        // 18위치 × 2 = 한 타겟을 최대 36개 유닛이 동시 공격하는 상황까지 분산 가능.
        // 이 값을 초과하면 가장 적게 배정된 위치를 fallback으로 공유.
        // 추후 게임 밸런스 테스트에 따라 조정 가능.
        private const int MaxUnitsPerSlot = 2;

        // Vector3 동등 비교에 쓰는 거리 임계값.
        // 부동소수점 오차로 인해 같은 위치라도 == 비교가 불안정할 수 있어,
        // Vector3.Distance < SamePositionEpsilon이면 같은 위치로 간주한다.
        // 슬롯 위치 사이 최소 거리(약 0.5f 이상)에 비해 충분히 작은 값이라 오인 위험 없음.
        private const float SamePositionEpsilon = 0.01f;

        // HexGrid 참조 — GetWalkableNeighborCoords 호출용.
        // 생성자에서 1회 보관하며 매니저 수명 동안 변경되지 않는다.
        private readonly HexGrid _grid;

        // 슬롯 점유 상태.
        // 외부 키: 타겟 타일 좌표 (공격 대상 건물/성의 타일).
        // 내부 사전:
        //   키 = 슬롯을 점유한 유닛 Id.
        //   값 = 해당 유닛에게 배정된 "도메인 좌표계 슬롯 월드 좌표" (HexMetrics.HexToWorld 기준).
        //         - 경계 위치는 정수 좌표(HexCoord)로 표현 불가능하므로 Vector3로 보관한다.
        //         - 도메인 좌표로 보관하는 이유: 점유 수 카운트는 팀별 ViewConverter 변환 전에
        //           수행되어야 모든 유닛(아군/적군 시점 차이 무관)에게 일관된 분산 결과를 준다.
        // 이 구조로 "타겟별 누가 어디를 차지했는지"를 O(1)로 추적할 수 있다.
        private readonly Dictionary<HexCoord, Dictionary<int, Vector3>> _assignments
            = new Dictionary<HexCoord, Dictionary<int, Vector3>>();

        // ClaimAttackSlot 내부에서 후보 위치 리스트를 매번 재생성하지 않도록 재사용하는 버퍼.
        // 한 호출 안에서만 채워지고 비워지므로 동시 호출은 가정하지 않는다(서버 단일 스레드).
        // 초기 용량 18은 최대 슬롯 수에 맞춤 — 거의 재할당이 발생하지 않도록 한다.
        private readonly List<Vector3> _candidateBuffer = new List<Vector3>(18);

        /// <summary>
        /// 생성자. 그리드 참조를 보관한다.
        /// </summary>
        /// <param name="grid">인접 타일 조회에 사용할 HexGrid.</param>
        public AttackPositionManager(HexGrid grid)
        {
            _grid = grid;
        }

        /// <summary>
        /// 타겟 타일 주변에서 해당 유닛에게 가장 적합한 공격 슬롯 위치를 배정하고,
        /// 그 위치의 뷰 월드 좌표(유닛 발 위치 기준 + UnitYOffset)를 반환한다.
        ///
        /// 동작 순서:
        ///   1) 같은 unitId가 이미 등록되어 있으면 기존 배정을 재사용한다(중복 안전).
        ///   2) 그리드에서 타겟의 walkable 인접 타일 목록을 조회한다(없으면 폴백).
        ///   3) 각 인접 타일마다 중심 + (인접 타일이 2개 이상일 때) 좌측경계 + 우측경계
        ///      위치를 계산해 후보 리스트에 추가한다(중복 위치는 제외).
        ///   4) 각 후보 위치의 현재 배정 유닛 수를 센다.
        ///   5) "배정 수가 가장 적은 위치" 우선, 동점이면 unitViewPos와 가장 가까운 위치 선택.
        ///   6) MaxUnitsPerSlot 초과 시에도 가장 적은 위치로 fallback (무한 대기 없음).
        ///   7) 선택된 도메인 좌표를 _assignments[targetCoord][unitId]에 저장하고,
        ///      ViewConverter로 뷰 좌표 변환 후 UnitYOffset을 더해 반환한다.
        ///
        /// 폴백:
        ///   _grid가 null이거나 walkable 인접 타일이 하나도 없으면 Vector3.zero 반환 →
        ///   호출 측(UnitView)은 이를 "슬롯 미배정"으로 해석해 enemyViewPos 직접 추적으로 폴백.
        /// </summary>
        /// <param name="targetCoord">공격 대상의 타일 좌표(건물/성 타일).</param>
        /// <param name="unitId">슬롯을 점유할 유닛 Id.</param>
        /// <param name="unitViewPos">유닛의 현재 뷰 월드 좌표 — 동점 시 가장 가까운 슬롯 선택에 사용.</param>
        /// <returns>배정된 슬롯의 뷰 월드 좌표 (유닛 발 위치 + UnitYOffset). 폴백 시 Vector3.zero.</returns>
        public Vector3 ClaimAttackSlot(HexCoord targetCoord, int unitId, Vector3 unitViewPos)
        {
            if (_grid == null) return Vector3.zero;

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

            // 타겟 주변 walkable 인접 타일 조회.
            // 인접 타일이 0개면(예: 맵 가장자리에 둘러싸인 건물 등) 폴백.
            List<HexCoord> neighbors = _grid.GetWalkableNeighborCoords(targetCoord);
            if (neighbors == null || neighbors.Count == 0)
                return Vector3.zero;

            // ----------------------------------------------------------------
            // 후보 위치 생성 — 인접 타일 N개 → 최대 N×3 위치
            // ----------------------------------------------------------------
            // 도메인 좌표계(HexMetrics.HexToWorld)에서 위치를 계산한다.
            // 뷰 좌표(ViewConverter.ToView)는 팀별로 회전될 수 있어 점유 카운트의 기준이
            // 흔들릴 수 있기 때문이다. 도메인 좌표는 모든 유닛에게 동일하게 보인다.
            _candidateBuffer.Clear();
            int neighborCount = neighbors.Count;

            for (int i = 0; i < neighborCount; i++)
            {
                Vector3 centerPos = HexMetrics.HexToWorld(neighbors[i]);

                // ① 중심 위치는 항상 추가.
                AddCandidateUnique(centerPos);

                // 인접 타일이 2개 이상일 때만 좌/우 경계 위치를 추가한다.
                // (N==1이면 (i+1)%N == (i+N-1)%N == i가 되어 같은 타일과의 평균이 의미 없음)
                if (neighborCount >= 2)
                {
                    // ② 좌측경계 위치 — 다음 인접 타일 중심과의 평균점
                    Vector3 nextPos = HexMetrics.HexToWorld(neighbors[(i + 1) % neighborCount]);
                    AddCandidateUnique((centerPos + nextPos) * 0.5f);

                    // ③ 우측경계 위치 — 이전 인접 타일 중심과의 평균점
                    //   (i + N - 1) % N : i==0일 때 N-1로 안전하게 감싼다(언더플로우 방지).
                    Vector3 prevPos = HexMetrics.HexToWorld(neighbors[(i + neighborCount - 1) % neighborCount]);
                    AddCandidateUnique((centerPos + prevPos) * 0.5f);
                }
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
        /// 후보 버퍼에 위치를 추가하되, 이미 같은 위치(SamePositionEpsilon 이내)가 있으면 무시한다.
        /// 인접 타일이 6개 미만(맵 가장자리)인 경우 같은 좌/우 경계 위치가 두 번 계산될 수 있는데,
        /// 이때 중복을 제거해 점유 카운트가 왜곡되지 않도록 한다.
        /// </summary>
        private void AddCandidateUnique(Vector3 pos)
        {
            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                if (Vector3.Distance(_candidateBuffer[i], pos) < SamePositionEpsilon)
                    return;
            }
            _candidateBuffer.Add(pos);
        }

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
    }
}
