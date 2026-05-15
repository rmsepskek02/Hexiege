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
//   AttackPositionManager는 각 유닛이 타겟에 실제로 접근하는 각도를 그대로
//   슬롯으로 등록하고, 다른 유닛들과 충분히 떨어진 각도일 때만 배정한다.
//   배정에 성공한 유닛은 타겟에서 contactRadius만큼 떨어진 슬롯 좌표로 직진하면,
//   도달과 동시에 전투 사거리(유닛 0.3f / 건물 0.5f)에 들어가 곧바로 전투에 진입한다.
//
// [8차 개선 — 2026-05-11] 슬롯 정의 변경 — 6방향 고정 슬롯 → 동적 각도 기반 (최대 MaxAttackersPerTarget마리, MinAngleSeparation° 최소 간격)
//   기존: 0°/60°/120°/180°/240°/300°의 6개 고정 슬롯 + 빈 슬롯이 없으면 fallback 공유.
//   변경: 유닛이 실제로 접근하는 각도(Atan2) 그대로 등록 → 각도 충돌 여부만 검사.
//
//   기존 방식의 문제:
//     - 뒤쪽 슬롯(접근 방향 기준 ±90°를 넘는 슬롯)은 직선 이동 유닛이
//       앞에 서 있는 유닛들에 막혀 물리적으로 도달할 수 없었다.
//     - 4번째 이후 유닛은 fallback 공유로 동일한 Vector3 위치에 겹쳐서 배정됐다.
//
//   동적 방식의 장점:
//     - 어떤 각도에서 와도 그 각도로 그대로 슬롯이 잡혀 항상 도달 가능하다.
//     - 각도 간격(60°)이 보장되어 시각적으로도 분명히 분산된다.
//     - 정원(3마리) 초과 또는 각도 충돌이 발생하면 Vector3.zero를 반환해 호출자가
//       해당 타겟을 포기하고 A* 이동으로 우회하도록 한다.
//
// 슬롯 점유 정책:
//   타겟 1개당 동시 등록 가능 유닛 수 = MaxAttackersPerTarget(=3).
//   서로 다른 유닛은 최소 MinAngleSeparation(=60°) 이상의 각도 간격을 유지해야 한다.
//   이 두 제한을 만족하지 못하면 배정 거부 → Vector3.zero 반환.
//
// 슬롯 선택 규칙:
//   1) 같은 unitId가 이미 등록되어 있으면 기존 angleDeg로 슬롯 위치 재계산 → 중복 Claim 안전.
//   2) 현재 공격자 수가 MaxAttackersPerTarget 이상이면 거부(Vector3.zero).
//   3) 신규 접근 각도와 기존 모든 공격자의 angleDeg 차이를 확인하여,
//      어느 하나라도 MinAngleSeparation 미만이면 거부(Vector3.zero).
//   4) 위 검사를 모두 통과하면 (unitId, angleDeg) 등록 후
//      슬롯 위치(타겟 + 접근 방향 × contactRadius)를 뷰 좌표로 변환해 반환.
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
        // 한 타겟에 동시에 근접 공격할 수 있는 유닛의 최대 수.
        // 이 수 이상이면 신규 유닛의 슬롯 배정을 거부하여 우회를 유도한다.
        private const int MaxAttackersPerTarget = 3;

        // 새로 접근하는 유닛의 각도가 기존 공격자와 이 값보다 가깝다면 배정을 거부한다.
        // 60°로 설정 시 최대 3마리(0°, 120°, 240° 또는 유사 간격)가 서로 겹치지 않고 분산될 수 있다.
        private const float MinAngleSeparation = 60f;

        // 공격자 1명의 등록 정보.
        // angleDeg: 이 유닛이 타겟에 접근할 때의 방향 각도(°).
        //   계산 방법: Mathf.Atan2(approachVec.x, approachVec.z) * Mathf.Rad2Deg
        //   이 각도를 기준으로 신규 유닛과의 최소 간격(MinAngleSeparation)을 검사한다.
        private struct AttackerInfo
        {
            public int unitId;
            public float angleDeg;
        }

        // 타겟 타일 좌표별 공격자 목록.
        // 최대 MaxAttackersPerTarget명까지 등록할 수 있다.
        // Release 시 unitId로 찾아 제거한다.
        private readonly Dictionary<HexCoord, List<AttackerInfo>> _attackers
            = new Dictionary<HexCoord, List<AttackerInfo>>();

        /// <summary>
        /// 생성자. 그리드 의존이 없으므로 매개변수 없음.
        /// (각도 기반 슬롯 계산은 그리드 인접 정보를 사용하지 않는다)
        /// </summary>
        public AttackPositionManager()
        {
        }

        // ====================================================================
        // [2026-05-11] 동적 각도 기반 슬롯 배정 — GameSystemRules.md 규칙 18
        // ====================================================================

        /// <summary>
        /// [2026-05-11] 동적 슬롯 배정 메서드.
        /// 유닛이 실제로 접근하는 각도(approach 벡터의 Atan2)를 그대로 등록하고,
        /// 다른 등록된 공격자들과 최소 MinAngleSeparation 이상 떨어져 있을 때만 배정한다.
        ///
        /// 규칙 18 발췌:
        ///   한 타겟에 동시 공격 가능한 근접 유닛은 최대 3마리.
        ///   새로 접근하는 유닛의 접근 각도가 기존 공격자와 60° 미만으로 겹치면 배정 거부.
        ///   배정이 거부된 유닛은 해당 타겟을 무시하고 A* 이동을 재개하여 우회한다.
        ///
        /// approachVecDomain은 도메인 좌표 기준 (유닛→적) 단위 벡터(또는 임의 길이 벡터).
        ///   - 방향 정보만 사용하므로 길이는 무관 (영벡터일 경우 +Z로 폴백).
        ///   - "유닛 위치 → 적 위치" 벡터를 그대로 넘기는 것을 권장.
        ///
        /// 배정 규칙:
        ///   1) 같은 unitId가 이미 등록되어 있으면 기존 angleDeg로 슬롯 위치 재계산 후 반환 (중복 안전).
        ///   2) 현재 등록 수가 MaxAttackersPerTarget 이상 → Vector3.zero (정원 초과 거부).
        ///   3) 신규 접근 각도와 기존 모든 공격자의 angleDeg 차이 중 어느 하나라도
        ///      MinAngleSeparation 미만 → Vector3.zero (각도 충돌 거부).
        ///   4) 통과 시 (unitId, angleDeg) 등록 후
        ///      슬롯 도메인 위치 = HexMetrics.HexToWorld(targetCoord) + approach × contactRadius
        ///      를 뷰 좌표로 변환하여 반환.
        ///
        /// 반환:
        ///   - 배정 성공: 슬롯의 뷰 월드 좌표 (UnitYOffset 포함). 도달 시 곧바로 전투 사거리에 진입.
        ///   - 배정 실패: Vector3.zero. 호출자는 이 타겟 공격을 포기하고 A* 이동을 재개해야 한다.
        /// </summary>
        /// <param name="targetCoord">공격 대상 타일 좌표.</param>
        /// <param name="unitId">슬롯을 점유할 유닛 Id.</param>
        /// <param name="unitWorldPosDomain">현재 시그니처 호환용. 동적 방식에서는 사용하지 않는다.</param>
        /// <param name="approachVecDomain">접근 방향 벡터(도메인 좌표 기준, XZ 평면). 영벡터면 +Z로 폴백.</param>
        /// <param name="contactRadius">타겟 중심에서 슬롯까지의 거리(전투 사거리). 유닛 타겟=0.3f, 건물 타겟=0.5f.</param>
        /// <returns>배정 성공 시 슬롯 뷰 월드 좌표, 실패 시 Vector3.zero.</returns>
        public Vector3 ClaimByApproach(HexCoord targetCoord, int unitId,
            Vector3 unitWorldPosDomain, Vector3 approachVecDomain, float contactRadius)
        {
            // 타겟별 공격자 목록 가져오기/생성.
            if (!_attackers.TryGetValue(targetCoord, out var list))
            {
                list = new List<AttackerInfo>();
                _attackers[targetCoord] = list;
            }

            // 타겟 도메인 좌표 — 슬롯 위치 계산의 기준점.
            Vector3 targetDomainPos = HexMetrics.HexToWorld(targetCoord);

            // 1) 같은 유닛이 이미 등록돼 있으면 기존 angleDeg로 슬롯 위치 재계산 → 중복 호출 안전.
            //    (TARGET_SWITCH 등으로 같은 유닛이 같은 타겟에 재클레임을 호출해도 일관된 슬롯 반환)
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].unitId == unitId)
                {
                    float existingRad = list[i].angleDeg * Mathf.Deg2Rad;
                    // angleDeg를 다시 단위 벡터로 복원: x = sin(rad), z = cos(rad) (Atan2(x, z)의 역)
                    Vector3 existingDir = new Vector3(Mathf.Sin(existingRad), 0f, Mathf.Cos(existingRad));
                    Vector3 existingSlotDomain = targetDomainPos + existingDir * contactRadius;
                    return ToViewWithUnitYOffset(existingSlotDomain);
                }
            }

            // 2) 정원 초과 검사 — 이미 MaxAttackersPerTarget명이 등록된 상태면 새 유닛은 거부.
            if (list.Count >= MaxAttackersPerTarget)
            {
                return Vector3.zero;
            }

            // 3) 접근 벡터를 XZ 평면 단위 벡터로 정규화 후 접근 각도 산출.
            //    영벡터(같은 위치) 입력은 NormalizeXZ가 +Z(0°)로 폴백 처리한다.
            Vector3 approach = NormalizeXZ(approachVecDomain);
            float angleDeg = Mathf.Atan2(approach.x, approach.z) * Mathf.Rad2Deg;

            // 4) 기존 모든 등록 공격자와 각도 차이 검사.
            //    Mathf.DeltaAngle은 -180~180 반환 → Abs 적용하면 0~180.
            //    이렇게 하면 360° 순환 경계(예: 350° vs 10°)도 자연스럽게 20°로 계산된다.
            for (int i = 0; i < list.Count; i++)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(angleDeg, list[i].angleDeg));
                if (diff < MinAngleSeparation)
                {
                    return Vector3.zero;
                }
            }

            // 5) 배정 성공 — 등록 후 슬롯 도메인 좌표 계산.
            list.Add(new AttackerInfo { unitId = unitId, angleDeg = angleDeg });
            Vector3 slotDomainPos = targetDomainPos + approach * contactRadius;
            Vector3 slotViewPos = ToViewWithUnitYOffset(slotDomainPos);

            return slotViewPos;
        }

        /// <summary>
        /// 해당 유닛이 점유 중이던 공격 슬롯을 해제한다.
        /// 유닛 사망 / Phase 1 종료(break) / 타겟 변경 / StopMovement 등 모든 종료 경로에서 호출.
        /// 내부 목록이 비면 외부 사전 항목도 제거하여 메모리 누수를 방지한다.
        /// </summary>
        /// <param name="targetCoord">슬롯이 등록되어 있던 타겟 타일 좌표.</param>
        /// <param name="unitId">슬롯을 해제할 유닛 Id.</param>
        public void ReleaseAttackSlot(HexCoord targetCoord, int unitId)
        {
            if (!_attackers.TryGetValue(targetCoord, out var list)) return;

            // unitId가 일치하는 항목을 찾아 제거. List<struct>이므로 인덱스 기반으로 RemoveAt.
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].unitId == unitId)
                {
                    list.RemoveAt(i);
                    break;
                }
            }

            // 목록이 비면 외부 사전에서도 키 제거 — 누적되는 빈 항목 방지.
            if (list.Count == 0)
                _attackers.Remove(targetCoord);
        }

        /// <summary>
        /// 모든 공격 슬롯 점유 정보를 초기화한다. 맵 전환/재경기 시 호출.
        /// </summary>
        public void Clear()
        {
            _attackers.Clear();
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
        /// 입력 벡터를 XZ 평면 단위 벡터로 정규화.
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
