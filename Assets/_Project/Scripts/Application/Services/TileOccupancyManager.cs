// ============================================================================
// TileOccupancyManager.cs
// 타일별 "유닛이 실제로 차지하는 양"을 추적하여, 좁은 타일에 너무 많은 유닛이
// 몰려 시각적으로 완전히 겹치는 현상을 방지하는 매니저.
//
// 사용 배경 (2026-04-25 2차 개선):
//   플로우 필드 도입 후 같은 방향으로 향하는 유닛이 모두 동일한 경로를 따라가
//   한 타일에 많은 유닛이 겹쳐 모이는 현상이 생겼다.
//   기존 TileSlotManager는 6개 슬롯밖에 없어 7번째 유닛부터 모두 슬롯 0(중심)으로
//   몰려 시각적으로 한 점에 뭉친다.
//
//   TileOccupancyManager는 "이 타일은 이미 합계 3을 차지 중이라 더 못 들어옴"이라는
//   판정을 도입하여, 초과되면 BFS로 이웃 타일을 찾아 우회시킨다.
//
// 핵심 설계 원칙:
//   1) 예약 없음 — 실제 유닛의 도메인 Position에 기반한 점유만 추적.
//      (예약 기반은 "이동 시작은 했는데 끝나지 않은" 상태가 누적되면 데드락 위험.)
//   2) 타일당 최대 점유 합계 = 3 (= 대형 유닛 1마리 = 소형 유닛 3마리).
//   3) 이동 시작 직전에만 체크 — Lerp 도중에는 체크하지 않음.
//   4) 초과 시 BFS로 walkable 인접 타일 → 그 인접 타일 순으로 탐색하여 빈자리 찾음.
//   5) 모든 타일이 가득 찼으면 호출 측에서 yield return null로 대기.
//      (게임 인구 한계가 보유 타일 수와 같으므로 이론상 거의 발생하지 않음.)
//
// TileSlotManager와의 차이:
//   TileSlotManager  — 시각적 위치 분산 전용 (오프셋 Vector3 반환).
//   TileOccupancyManager — 도메인 점유량 추적 (이동 가능 여부 판정).
//   서로 독립적으로 동작하며, 같은 타일에 들어온 유닛은 두 시스템 모두에 등록된다.
//
// 멀티플레이 정책:
//   서버에서만 사용. 클라이언트는 NetworkTransform이 위치를 보간 동기화하므로
//   별도의 점유 추적이 필요하지 않다.
//
// Application 레이어 — Domain에 의존, Unity에 직접 의존하지 않음.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class TileOccupancyManager
    {
        // 한 타일이 받을 수 있는 점유 합계의 상한.
        // 소형=1, 중형=2, 대형=3 기준이며,
        // 대형 1마리 또는 중형 1+소형 1 또는 소형 3마리 등이 가능하다.
        public const float MaxOccupancy = 3f;

        // 타일별 누적 점유량.
        // 키: 타일 좌표 / 값: 현재까지 등록된 유닛들의 OccupancySize 합계.
        private readonly Dictionary<HexCoord, float> _occupancy
            = new Dictionary<HexCoord, float>();

        /// <summary>
        /// 해당 타일에 unitSize만큼의 점유를 추가할 수 있는지 확인.
        /// 부동소수점 누적 오차로 0.999... 같은 값이 합쳐졌을 때 false 처리되지 않도록
        /// 비교에 작은 여유(epsilon)를 둔다.
        /// </summary>
        /// <param name="tile">확인할 타일 좌표.</param>
        /// <param name="unitSize">추가하려는 유닛의 OccupancySize.</param>
        /// <returns>점유 합계가 MaxOccupancy 이내면 true.</returns>
        public bool CanFit(HexCoord tile, float unitSize)
        {
            // GetValueOrDefault: 타일이 _occupancy에 없으면 0 반환 — 빈 타일 처리.
            float current = _occupancy.TryGetValue(tile, out float value) ? value : 0f;
            const float epsilon = 0.0001f;
            return current + unitSize <= MaxOccupancy + epsilon;
        }

        /// <summary>
        /// 모든 점유 정보를 초기화. 맵 전환/재경기 시 호출.
        /// </summary>
        public void Clear()
        {
            _occupancy.Clear();
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// HexCoord 무효값(스폰 직전 등) 판정.
        /// default(HexCoord)는 (Q=0, R=0)이지만, 호출 약속에 의해 "스폰 직전"으로 해석.
        /// 일반 (0,0) 타일과 구분이 필요한 경우 호출 측에서 default를 의도적으로 넘기지 않으면 된다.
        /// </summary>
        private static bool IsInvalid(HexCoord coord)
        {
            return coord.Q == 0 && coord.R == 0;
        }

        /// <summary>
        /// 점유량 누적 — 같은 타일에 여러 유닛이 등록되면 합계가 늘어난다.
        /// </summary>
        private void Increase(HexCoord tile, float amount)
        {
            if (_occupancy.TryGetValue(tile, out float current))
                _occupancy[tile] = current + amount;
            else
                _occupancy[tile] = amount;
        }

        /// <summary>
        /// 점유량 차감 — 0 이하가 되면 사전에서 항목 자체를 제거(메모리 정리).
        /// 미등록 타일이거나 차감 후 음수가 되면 0으로 클램프.
        /// </summary>
        private void Decrease(HexCoord tile, float amount)
        {
            if (!_occupancy.TryGetValue(tile, out float current)) return;

            float next = current - amount;
            const float epsilon = 0.0001f;
            if (next <= epsilon)
                _occupancy.Remove(tile);
            else
                _occupancy[tile] = next;
        }
    }
}
