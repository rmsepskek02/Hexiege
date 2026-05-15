// ============================================================================
// CongestionMap.cs
// 타일별 "혼잡도(얼마나 붐비는가)"를 추적하는 순수 C# 클래스.
//
// 무엇을 하는가:
//   유닛이 A* 이동 중 새 타일에 들어갈 때마다 그 타일의 혼잡도를 1 올린다(Increment).
//   같은 타일에 여러 유닛이 지나갈수록 혼잡도가 누적되고,
//   경로 탐색 시 그 타일을 지나가는 비용이 커지므로 후속 유닛들이 자연스럽게 우회한다.
//
//   동시에 시간이 흐르면(설정된 감쇠 주기 DecayInterval마다) 혼잡도가 1씩 감소하며(Decay),
//   0 이하가 되면 Dictionary에서 키 자체를 제거하여 메모리를 깨끗하게 유지한다.
//
// 왜 순수 C# 클래스인가:
//   - Domain 레이어가 아닌 Application 레이어이지만, UnityEngine 의존이 없다.
//   - MonoBehaviour가 아니므로 Inspector에 직접 노출할 수 없다 → 튜닝 값은 GameConfig(CongestionDecayInterval / CongestionWeight)에서 받는다.
//   - 생성과 소유는 GameBootstrapper(composition root)가 담당한다.
//
// 멀티플레이 정책:
//   분산 의도는 서버에서만 의미가 있다(클라이언트는 NetworkTransform으로 결과만 받으므로).
//   따라서 GameBootstrapper에서 서버 가드(NetworkContext.IsNetworkServer)를 통해 Tick/Increment를 제한한다.
//
// 레이어:
//   Application — UnityEngine 의존 없음. Domain의 HexCoord에만 의존.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application.Services
{
    /// <summary>
    /// 타일별 혼잡도를 보관하고 시간에 따라 감쇠시키는 매니저.
    /// 사용 흐름:
    ///   1) 유닛이 타일에 진입 → <see cref="Increment"/>(타일) 호출 → 혼잡도 +1.
    ///   2) CongestionAwarePathfinder가 경로 비용을 계산할 때 <see cref="Get"/>(타일)로 값을 읽음.
    ///   3) 일정 주기마다 <see cref="Decay"/> 호출 → 모든 타일 혼잡도 -1.
    ///   4) 재경기/맵 전환 시 <see cref="Clear"/>로 전체 초기화.
    /// </summary>
    public class CongestionMap
    {
        // 타일별 혼잡도 저장소.
        // key   : 헥스 좌표(HexCoord)
        // value : 그 타일에 누적된 혼잡도 카운트. 1 이상일 때만 키가 존재한다.
        // 0이거나 키가 없으면 "혼잡 없음"으로 간주한다.
        private readonly Dictionary<HexCoord, int> _congestion = new Dictionary<HexCoord, int>();

        /// <summary>
        /// 해당 타일의 혼잡도를 1 증가시킨다.
        /// 유닛이 A* 이동 중 새 타일에 처음 들어가는 순간에 호출한다.
        /// </summary>
        /// <param name="tile">혼잡도를 올릴 타일의 좌표.</param>
        public void Increment(HexCoord tile)
        {
            // 이미 키가 있으면 +1, 없으면 1로 시작.
            // Dictionary 인덱서에 직접 더하는 패턴은 키가 없으면 KeyNotFoundException이 나므로
            // TryGetValue로 안전하게 처리한다.
            if (_congestion.TryGetValue(tile, out int current))
            {
                _congestion[tile] = current + 1;
            }
            else
            {
                _congestion[tile] = 1;
            }
        }

        /// <summary>
        /// 해당 타일의 현재 혼잡도를 반환한다.
        /// 키가 없으면 0(혼잡 없음)을 반환한다.
        /// </summary>
        /// <param name="tile">조회할 타일의 좌표.</param>
        /// <returns>해당 타일에 누적된 혼잡도. 등록 이력이 없으면 0.</returns>
        public int Get(HexCoord tile)
        {
            // TryGetValue로 키 부재 시에도 안전하게 0 반환.
            // 경로 탐색의 hot path에서 자주 호출되므로 예외 없이 처리하는 게 중요하다.
            return _congestion.TryGetValue(tile, out int value) ? value : 0;
        }

        /// <summary>
        /// 모든 타일의 혼잡도를 1씩 감소시킨다.
        /// 0 이하가 된 타일은 Dictionary에서 제거되어 메모리/조회 효율을 유지한다.
        /// 외부에서 일정 주기(DecayInterval)마다 호출되도록 ProductionTicker가 관리한다.
        /// </summary>
        public void Decay()
        {
            // 순회 중에 키를 제거할 수 없으므로(Dictionary 수정 시 InvalidOperationException 발생),
            // 먼저 키 목록을 별도로 복사해서 안전하게 순회한다.
            // 타일 수가 그리 많지 않아(맵 크기 = 7×17 = 119) 매번 List 할당해도 비용은 미미하다.
            var keys = new List<HexCoord>(_congestion.Keys);

            foreach (var key in keys)
            {
                int next = _congestion[key] - 1;
                if (next <= 0)
                {
                    // 0 이하가 되었으면 키 자체를 제거 — 다음 Get 호출 시 0으로 자연스럽게 처리됨.
                    _congestion.Remove(key);
                }
                else
                {
                    _congestion[key] = next;
                }
            }
        }

        /// <summary>
        /// 전체 혼잡도 데이터를 비운다.
        /// 재경기(Rematch) 또는 맵 전환 시 GameBootstrapper가 호출하여 깨끗한 상태로 시작하게 한다.
        /// </summary>
        public void Clear()
        {
            _congestion.Clear();
        }
    }
}
