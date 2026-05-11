// ============================================================================
// TileMoveSlotManager.cs
// 새 이동/전투 규칙(GameSystemRules.md 규칙 10/17)에 맞춰 새로 만든 이동 슬롯 매니저.
//
// 기존 TileSlotManager와의 차이:
//   - 슬롯 개수: 6개 → 3개 (앞 / 뒤좌 / 뒤우)
//   - 슬롯의 의미: 시각 오프셋 → "타일 내 이동 목표 좌표"
//   - 배정 기준: 슬롯 0부터 선형 → 현재 위치에서 가장 가까운 빈 슬롯 → 동률 시 1→2→3
//   - 점유치: 카운트 무관 → "물리 슬롯 1개 차지 + 점유 카운트는 OccupancySize만큼 소비"
//
// 이 매니저는 "어느 슬롯에 어떤 unitId가 들어 있는가"만 책임진다.
// 타일 단위 점유 상한 판정(MaxOccupancy=3)은 별도 시스템(TileOccupancyManager)이 처리한다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 슬롯 정의 (XZ 평면, 타일 중심에서의 비율 오프셋)
//
//   슬롯 1번: 성 방향 앞쪽 중앙
//   슬롯 2번: 뒤쪽 좌측
//   슬롯 3번: 뒤쪽 우측
//
// "성 방향(앞)" 정의:
//   외부에서 ClaimSlot 호출 시 forwardDirView(뷰 좌표계 기준 단위 벡터, XZ 평면)를 함께 전달한다.
//   forwardDirView는 일반적으로 "유닛이 향하는 다음 타일 방향"이며, 도메인 좌표 → 뷰 좌표
//   변환을 거친 ViewConverter.ToView 기준 벡터다 (Red팀에서도 화면상 앞쪽이 위가 되도록).
//   호출 측에서 매 타일 진입 시 새 forward 벡터를 산출해 전달하면 된다.
//
// 비율 값(타일 중심 기준):
//   슬롯 1번: forward × SlotForwardRatio
//   슬롯 2번: -forward × SlotForwardRatio + perpLeft × SlotSideRatio
//   슬롯 3번: -forward × SlotForwardRatio - perpLeft × SlotSideRatio
//   (perpLeft = forward을 XZ 평면에서 90° 좌회전한 단위 벡터)
//
//   ※ SlotForwardRatio / SlotSideRatio는 더 이상 const가 아니다.
//     GameBootstrapper의 [SerializeField] 필드(_slotForwardRatio / _slotSideRatio)에서
//     값을 받아 생성자로 주입하므로, Unity Inspector에서 자유롭게 조정할 수 있다.
//     기본값(0.30, 0.30)은 기존 const와 동일하여 동작 변화는 없다.
//
// 단위:
//   비율 × HexMetrics.TileWidth → 최종 월드 오프셋. 타일 크기에 비례.
//
// 점유치 2 처리(D1 결정):
//   물리적으로는 슬롯 1개만 차지한다.
//   점유 카운트(TileOccupancyManager)에는 호출 측이 OccupancySize만큼을 누적시킨다.
//   따라서 "한 타일에 점유치 2짜리 유닛 1마리 + 점유치 1짜리 유닛 1마리" 같은
//   조합이 가능하지만, 둘이 서로 다른 슬롯에 위치하므로 시각적 겹침은 없다.
//
// 좌표축:
//   XZ 평면 기반. Y는 항상 0(높이 보정은 호출 측이 UnitYOffset으로 처리).
//
// 멀티플레이 정책:
//   슬롯 결정은 서버에서만 의미 있음.
//   클라이언트는 NetworkTransform이 서버 위치를 보간 동기화하므로 자체 판단 불필요.
//
// Application 레이어 — UnityEngine.Vector3 의존, MonoBehaviour 아님.
// (선례: TileSlotManager / AttackPositionManager / UnitCombatUseCase 모두 동일 패턴)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Core;

namespace Hexiege.Application
{
    public class TileMoveSlotManager
    {
        // ────────────────────────────────────────────────────────────────────
        // 상수
        // ────────────────────────────────────────────────────────────────────

        /// <summary> 한 타일이 가진 이동 슬롯 수. (앞 / 뒤좌 / 뒤우) </summary>
        public const int SlotCount = 3;

        // 슬롯 비율 — 타일 중심에서 forward 단위 벡터로 얼마나 떨어져 있는가.
        // 0.30: 인접 타일 시각 겹침을 최소화하면서 점령 판정(타일 중심 안쪽) 안전권에 머무는 값.
        // [2026-05-11] const → readonly로 변경. 생성자에서 주입받아 Inspector(GameBootstrapper)에서 조정 가능.
        private readonly float SlotForwardRatio;

        // 뒤쪽 슬롯 좌/우의 측면 거리 비율 (perpLeft 방향 단위).
        // 슬롯 2번/3번이 너무 가까우면 시각적 겹침, 너무 멀면 타일 경계로 흘러나갈 수 있음.
        // [2026-05-11] const → readonly로 변경. 생성자에서 주입받아 Inspector(GameBootstrapper)에서 조정 가능.
        private readonly float SlotSideRatio;

        // ────────────────────────────────────────────────────────────────────
        // 생성자
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 슬롯 오프셋 비율을 받아 초기화한다.
        /// <para>forwardRatio : 슬롯 1번(앞 중앙)이 타일 중심에서 성 방향으로 얼마나 떨어지는지. 0~0.5 권장.</para>
        /// <para>sideRatio    : 슬롯 2번(뒤좌)·3번(뒤우)이 타일 중심에서 좌/우로 얼마나 떨어지는지. 0~0.5 권장.</para>
        /// </summary>
        public TileMoveSlotManager(float forwardRatio = 0.30f, float sideRatio = 0.30f)
        {
            SlotForwardRatio = forwardRatio;
            SlotSideRatio    = sideRatio;
        }

        // ────────────────────────────────────────────────────────────────────
        // 내부 상태
        // ────────────────────────────────────────────────────────────────────

        // 한 타일의 슬롯 점유 정보:
        //   key = 점유 중인 unitId
        //   value = 그 유닛이 차지한 슬롯 번호 (1, 2, 3 중 하나)
        // SlotCount가 작아 Dictionary 조회 비용은 무시 가능.
        private class SlotState
        {
            public Dictionary<int, int> UnitToSlot = new Dictionary<int, int>();
        }

        // 외부 키: 타일 좌표 / 값: 그 타일의 슬롯 점유 상태.
        private readonly Dictionary<HexCoord, SlotState> _occupied
            = new Dictionary<HexCoord, SlotState>();

        // ────────────────────────────────────────────────────────────────────
        // 공개 API
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 타일에서 비어 있는 슬롯을 골라 unitId에 배정하고, 해당 슬롯의 월드 좌표(이동 목표)를 반환한다.
        ///
        /// 동작:
        ///   1) 같은 unitId가 이미 등록돼 있으면 기존 슬롯을 재사용 (중복 호출 안전).
        ///   2) 1~3번 슬롯 중 비어 있는 슬롯 후보를 만든다.
        ///   3) 후보 중 unitWorldPos에서 가장 가까운 슬롯 우선 — 동거리 시 번호(1→2→3) 우선.
        ///   4) 모두 차 있으면 1번으로 fallback (시각적 겹침 허용).
        ///
        /// 반환값 = 도메인 좌표 + 슬롯 오프셋. 호출 측은 ViewConverter.ToView로 뷰 좌표 변환 + UnitYOffset을 더해 사용.
        ///
        /// 점유치(D1):
        ///   본 메서드는 슬롯 1개만 점유한다. TileOccupancyManager의 카운트는 호출 측에서
        ///   유닛 OccupancySize만큼을 별도로 누적시킨다.
        /// </summary>
        /// <param name="tile">슬롯을 요청할 타일 좌표.</param>
        /// <param name="unitId">슬롯을 점유할 유닛 Id.</param>
        /// <param name="unitWorldPos">슬롯 선택 시 거리 기준이 될 유닛의 현재 월드 좌표 (도메인 좌표 권장).</param>
        /// <param name="forwardDirDomain">"성 방향 앞쪽" 단위 벡터(도메인 좌표 기준, XZ 평면). 영벡터면 +Z로 폴백.</param>
        /// <returns>배정된 슬롯의 도메인 월드 좌표 (Y=0). 호출 측이 ToView + UnitYOffset 적용.</returns>
        public Vector3 ClaimSlot(HexCoord tile, int unitId, Vector3 unitWorldPos, Vector3 forwardDirDomain)
        {
            if (!_occupied.TryGetValue(tile, out SlotState state))
            {
                state = new SlotState();
                _occupied[tile] = state;
            }

            // 같은 유닛이 이미 들어와 있으면 기존 슬롯 그대로.
            // (Phase 0/1/2 전환 등에서 중복 호출 가능 — Plan 5-8 상태 공유 위험 대응)
            if (state.UnitToSlot.TryGetValue(unitId, out int existingSlot))
            {
                return GetSlotWorldPosition(tile, existingSlot, forwardDirDomain);
            }

            // forward 벡터를 XZ 평면 단위 벡터로 정규화. 영벡터/Y성분은 안전하게 처리.
            Vector3 forward = NormalizeForward(forwardDirDomain);

            // 1순위 = 거리 작은 슬롯, 동률 시 슬롯 번호 작은 쪽.
            int chosen = -1;
            float bestDistSq = float.MaxValue;

            for (int slot = 1; slot <= SlotCount; slot++)
            {
                if (IsSlotTaken(state, slot)) continue;

                Vector3 slotWorld = GetSlotWorldPositionInternal(tile, slot, forward);
                float distSq = (slotWorld - unitWorldPos).sqrMagnitude;

                // 더 가까우면 교체. 동거리(매우 작은 차이)인 경우 더 작은 슬롯 번호가 이미 chosen
                // 자리에 들어와 있으므로 strict less-than으로 비교한다(D 우선순위).
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    chosen = slot;
                }
            }

            // 모든 슬롯이 차 있으면 1번으로 fallback (시각적 겹침 허용 — 매우 드문 케이스).
            // 실제 운영에선 TileOccupancyManager가 한도 초과를 미리 막아주므로 거의 발생하지 않음.
            if (chosen < 0) chosen = 1;

            state.UnitToSlot[unitId] = chosen;
            return GetSlotWorldPositionInternal(tile, chosen, forward);
        }

        /// <summary>
        /// 해당 유닛이 점유 중이던 이동 슬롯을 해제. 다른 유닛이 그 슬롯을 새로 가져갈 수 있게 된다.
        ///
        /// 호출 시점:
        ///   - 새 타일 진입 시점에 직전 타일에 대해 호출 (Lerp 시작 전, ProcessStep 전후 어디든 OK).
        ///   - 유닛이 사망할 때 현재 점유 타일에 대해 호출.
        ///   - 근접 유닛이 이동 슬롯을 풀고 공격 슬롯으로 진입할 때.
        ///
        /// 사전이 비면 항목 자체를 제거하여 메모리 누수를 방지한다.
        /// </summary>
        public void ReleaseSlot(HexCoord tile, int unitId)
        {
            if (!_occupied.TryGetValue(tile, out SlotState state)) return;

            state.UnitToSlot.Remove(unitId);
            if (state.UnitToSlot.Count == 0)
                _occupied.Remove(tile);
        }

        /// <summary>
        /// 모든 슬롯 점유 정보 초기화. 맵 전환/재경기 시 호출.
        /// </summary>
        public void Clear()
        {
            _occupied.Clear();
        }

        /// <summary>
        /// 해당 타일에 현재 이동 슬롯을 배정받은 유닛 수를 반환한다.
        /// 새 이동 코루틴(MoveAlongPathV3)이 규칙 10에 따라 이동 목표를 결정할 때 사용한다:
        ///   - 반환값이 0이면 타일 중심을 그대로 이동 목표로 삼는다.
        ///   - 반환값이 1 이상이면 ClaimSlot으로 슬롯을 배정받아 슬롯 위치를 이동 목표로 삼는다.
        /// </summary>
        /// <param name="tile">조회할 타일 좌표.</param>
        /// <returns>현재 해당 타일에 등록된 유닛 수. 등록 항목이 없으면 0.</returns>
        public int GetUnitCount(HexCoord tile)
        {
            if (!_occupied.TryGetValue(tile, out SlotState state)) return 0;
            return state.UnitToSlot.Count;
        }

        /// <summary>
        /// 디버깅/조회용: 특정 unitId가 어떤 타일의 어떤 슬롯에 있는지 알고 싶을 때 사용.
        /// 등록되어 있지 않으면 (false, 0) 반환.
        /// </summary>
        public bool TryGetSlot(HexCoord tile, int unitId, out int slot)
        {
            slot = 0;
            if (!_occupied.TryGetValue(tile, out SlotState state)) return false;
            return state.UnitToSlot.TryGetValue(unitId, out slot);
        }

        /// <summary>
        /// 외부에서 슬롯 번호와 forward 벡터만으로 위치를 다시 계산하고 싶을 때 사용.
        /// (예: UnitView 디버그 시각화)
        /// </summary>
        public Vector3 GetSlotWorldPosition(HexCoord tile, int slot, Vector3 forwardDirDomain)
        {
            Vector3 forward = NormalizeForward(forwardDirDomain);
            return GetSlotWorldPositionInternal(tile, slot, forward);
        }

        // ────────────────────────────────────────────────────────────────────
        // 내부 헬퍼
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 해당 슬롯을 누군가 차지하고 있는지 확인.
        /// SlotCount(=3)이 작아 선형 탐색이 가장 빠르다.
        /// </summary>
        private static bool IsSlotTaken(SlotState state, int slot)
        {
            foreach (var kv in state.UnitToSlot)
            {
                if (kv.Value == slot) return true;
            }
            return false;
        }

        /// <summary>
        /// 슬롯의 도메인 월드 좌표 계산.
        /// forward는 이미 정규화된 단위 벡터(Y=0)라고 가정.
        /// [2026-05-11] readonly 인스턴스 필드(SlotForwardRatio/SlotSideRatio) 참조를 위해 static 제거.
        /// </summary>
        private Vector3 GetSlotWorldPositionInternal(HexCoord tile, int slot, Vector3 forward)
        {
            Vector3 center = HexMetrics.HexToWorld(tile);
            float scale = HexMetrics.TileWidth;

            // forward를 XZ 평면에서 90° 좌회전한 단위 벡터.
            // (x, 0, z) → (-z, 0, x).
            Vector3 perpLeft = new Vector3(-forward.z, 0f, forward.x);

            switch (slot)
            {
                case 1: // 앞쪽 중앙
                    return center + forward * (SlotForwardRatio * scale);
                case 2: // 뒤쪽 좌측
                    return center
                           - forward * (SlotForwardRatio * scale)
                           + perpLeft * (SlotSideRatio * scale);
                case 3: // 뒤쪽 우측
                    return center
                           - forward * (SlotForwardRatio * scale)
                           - perpLeft * (SlotSideRatio * scale);
                default:
                    // 잘못된 슬롯 번호는 타일 중심으로 폴백 (방어적 처리).
                    return center;
            }
        }

        /// <summary>
        /// forward 입력값을 XZ 평면 단위 벡터로 정규화한다.
        /// Y 성분은 0으로 강제하여 평면 외 기울어짐을 방지.
        /// 영벡터/매우 짧은 벡터는 +Z 방향으로 폴백.
        /// </summary>
        private static Vector3 NormalizeForward(Vector3 input)
        {
            Vector3 v = new Vector3(input.x, 0f, input.z);
            float sqr = v.sqrMagnitude;
            const float epsilonSqr = 1e-6f;
            if (sqr < epsilonSqr)
            {
                // 폴백: +Z 방향. (도메인 좌표에서 "위쪽"이며 헥스 그리드에선 보통 한 행 위.)
                return new Vector3(0f, 0f, 1f);
            }
            return v / Mathf.Sqrt(sqr);
        }
    }
}
