// ============================================================================
// FlowFieldService.cs
// 목적지(성, 랠리포인트 등)별로 HexFlowField를 캐싱·관리하는 서비스.
//
// 사용 배경:
//   기존 A* 방식은 유닛마다 매번 경로를 계산했고, 아군 유닛의 점유 타일을 모두
//   blocked로 처리해 좁은 통로에서 유닛이 자주 멈춤(deadlock).
//
//   플로우 필드는 "목적지 한 곳을 향한 전체 맵의 방향 정보"를 한 번에 계산해
//   여러 유닛이 같은 캐시를 공유한다 (유닛 수에 무관하게 O(1) 조회).
//
// 본 서비스가 하는 일:
//   1) 첫 요청 시 HexFlowField.Compute(grid, destination)로 BFS 1회 계산
//   2) 이후 같은 목적지 요청은 캐시에서 즉시 반환
//   3) 건물 배치/파괴 등으로 walkable이 변경되면 InvalidateAll()로 모든 캐시 폐기
//      (다음 요청 시 lazy 재계산)
//
// 왜 lazy 재계산인가:
//   목적지가 무엇이 될지(랠리포인트 위치, 다음 공격 대상 등) 사전에 알 수 없으므로
//   변경 직후에 모든 필드를 미리 다시 계산하면 낭비.
//   첫 요청 시점에 계산해도 187타일 BFS는 1ms 미만이라 체감 영향 없음.
//
// 멀티플레이 정책:
//   서버에서만 사용 (NetworkUnitMovementController가 경로 계산을 서버에서 실행).
//   클라이언트는 NetworkTransform이 위치를 보간 동기화하므로 플로우 필드 불필요.
//
// 이벤트 구독:
//   - GameEvents.OnBuildingPlaced  → InvalidateAll
//       건물 배치로 walkable이 false로 바뀔 수 있어 기존 경로가 부정확해짐.
//   - GameEvents.OnBuildingDied → InvalidateAll
//       건물 파괴로 walkable이 true로 바뀌면 더 짧은 경로가 생길 수 있음.
//   유닛 사망/이동은 walkable에 영향 없으므로 OnUnitDied는 구독하지 않는다.
//
// Application 레이어 — Domain에 의존, Unity에 직접 의존하지 않음.
// ============================================================================

using System;
using System.Collections.Generic;
using UniRx;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class FlowFieldService : IDisposable
    {
        // 그리드 참조 — Compute에 넘기기 위해 보관.
        private HexGrid _grid;

        // 캐시: 목적지 좌표 → 그 목적지로 향하는 플로우 필드.
        // 같은 목적지(예: 적 성)에 대한 모든 유닛 요청이 동일한 인스턴스를 공유한다.
        private readonly Dictionary<HexCoord, HexFlowField> _cache = new Dictionary<HexCoord, HexFlowField>();

        // GameEvents 구독 해제용 Disposable. 게임 종료 시 Dispose로 정리.
        private CompositeDisposable _subscriptions;

        /// <summary>
        /// 그리드 참조를 저장하고 walkable 변경 이벤트를 구독한다.
        ///
        /// GameBootstrapper.LoadMap()에서 그리드 생성 직후 1회 호출.
        /// 같은 인스턴스를 재사용한다면 기존 구독을 Dispose하고 새로 구독한다.
        /// </summary>
        /// <param name="grid">탐색 대상 HexGrid.</param>
        public void Initialize(HexGrid grid)
        {
            _grid = grid;

            // 기존 구독이 있으면 깨끗이 정리 (재경기/맵 전환 시 중복 구독 방지).
            _subscriptions?.Dispose();
            _subscriptions = new CompositeDisposable();
            _cache.Clear();

            // 건물 배치 → walkable 변경 가능 → 모든 캐시 무효화.
            GameEvents.OnBuildingPlaced
                .Subscribe(_ => InvalidateAll())
                .AddTo(_subscriptions);

            // 건물 사망만 walkable에 영향을 주므로 OnBuildingDied만 구독한다.
            // 유닛 사망(OnUnitDied)은 walkable과 무관 → 구독하지 않음.
            // 건물 전용 강타입 이벤트라 BuildingData 캐스트 분기가 필요 없다.
            GameEvents.OnBuildingDied
                .Subscribe(_ => InvalidateAll())
                .AddTo(_subscriptions);
        }

        /// <summary>
        /// 목적지에 해당하는 플로우 필드를 반환. 캐시 미존재 시 즉시 계산 후 캐싱.
        ///
        /// 호출 측은 반환된 필드의 GetPath(start)로 경로 리스트를 얻을 수 있다.
        /// 동일 목적지로의 다른 유닛 요청도 같은 인스턴스를 재사용하므로 호출이 잦아도 비용은 첫 1회뿐이다.
        /// </summary>
        /// <param name="destination">목적지 좌표 (walkable 또는 non-walkable 무관).</param>
        /// <returns>완성된 HexFlowField 인스턴스.</returns>
        public HexFlowField GetOrCompute(HexCoord destination)
        {
            // _grid가 null이면 Initialize 호출 누락 — 호출 측 버그 신호.
            // 안전을 위해 빈 필드(아무것도 못 하는 상태)를 반환하지 않고 예외를 던지는 편이
            // 디버깅에 유리하지만, 게임 실행 중 throw는 위험하므로 여기서는 그대로 진행한다.
            if (_grid == null) return null;

            if (_cache.TryGetValue(destination, out HexFlowField cached))
                return cached;

            // 캐시 미존재 → BFS 1회 계산 후 캐싱.
            HexFlowField field = HexFlowField.Compute(_grid, destination);
            _cache[destination] = field;
            return field;
        }

        /// <summary>
        /// 모든 캐시를 폐기. 건물 배치/파괴 등으로 walkable이 변경됐을 때 호출.
        ///
        /// 즉시 재계산하지 않는 이유:
        ///   현재 어떤 목적지가 활성 상태인지(요청될지) 알 수 없으므로 lazy 전략이 효율적.
        ///   다음 GetOrCompute 호출 때 필요한 목적지만 다시 계산된다.
        /// </summary>
        public void InvalidateAll()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 구독 해제 + 캐시 정리. 게임 종료/씬 전환 시 호출.
        /// Initialize를 다시 호출하면 새 구독으로 교체된다.
        /// </summary>
        public void Dispose()
        {
            _subscriptions?.Dispose();
            _subscriptions = null;
            _cache.Clear();
            _grid = null;
        }
    }
}
