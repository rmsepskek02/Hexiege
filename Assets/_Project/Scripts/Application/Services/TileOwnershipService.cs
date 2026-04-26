// ============================================================================
// TileOwnershipService.cs
// 매 프레임 모든 유닛의 "물리적 위치"를 확인하여 타일 소유권(점령 상태)을
// 갱신하는 Pull 방식 서비스.
//
// 도입 배경 (2026-04-26):
//   유닛 이동은 두 단계로 나뉜다.
//     - Phase 0: 타일에서 타일로 한 칸씩 Lerp 이동 → ProcessStep이 _grid.SetOwner 호출.
//     - Phase 1: 사거리 안 적을 향해 월드 좌표 직선 추적 → 타일을 가로질러도 점령 갱신 없음.
//   Phase 1 이동에서는 도메인 Position이 갱신되지 않으므로 한 유닛이 여러 타일을
//   지나가도 시각적으로만 보일 뿐 점령에 반영되지 않는 문제가 있었다.
//
//   본 서비스는 유닛의 "현재 보이는 위치(Transform.position)"를 매 프레임 읽어
//   해당 위치가 어느 헥스 타일에 속하는지 역산한 뒤, 점령 규칙을 적용한다.
//   이동 방식(Phase 0/1/2)이 무엇이든 결과적으로 화면에 보이는 위치 기준으로
//   타일이 점령되므로 "지나가기만 해도 점령" 동작이 가능해진다.
//
// 점령 규칙:
//   - 타일 위에 한 팀의 유닛만 있음     → 즉시 그 팀 점령으로 갱신.
//   - 타일 위에 양 팀이 동시에 있음     → 현재 점령 상태 유지(중립지대 방지).
//   - 타일 위에 유닛 없음               → 현재 점령 상태 유지(점령은 사라지지 않음).
//
// 멀티플레이 정책:
//   서버(또는 싱글플레이)에서만 Tick을 호출한다. 클라이언트는 NetworkTransform이
//   유닛 위치를 보간 동기화하고, 점령 결과는 별도 동기화 경로로 전달된다.
//
// 성능 메모:
//   - HashSet 인스턴스를 매 프레임 할당하지 않도록 _setPool로 재사용.
//   - 타일 Dictionary도 .Clear() 후 재사용하여 GC 압력을 최소화.
//
// Application 레이어 — Domain/Core(ViewConverter, HexMetrics) 참조 가능.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Core;

namespace Hexiege.Application.Services
{
    /// <summary>
    /// 매 프레임 유닛의 물리 위치를 확인하여 타일 소유권을 실시간 갱신하는 서비스.
    /// GameBootstrapper.Update()에서 Tick()이 호출된다.
    /// </summary>
    public class TileOwnershipService
    {
        // ====================================================================
        // 의존성
        // ====================================================================

        // 점령 갱신 대상 그리드. SetOwner / GetOwner 호출에 사용.
        private readonly HexGrid _grid;

        // 살아있는 모든 유닛 목록을 순회하기 위한 UseCase.
        // _unitSpawn.Units(IReadOnlyDictionary<int, UnitData>)을 매 프레임 읽는다.
        private readonly UnitSpawnUseCase _unitSpawn;

        // 유닛의 실제 월드(view) 좌표를 얻기 위한 인터페이스.
        // 싱글/멀티 모두 UnitWorldPositionProvider 인스턴스가 주입된다.
        private readonly IEntityPositionProvider _positionProvider;

        // ====================================================================
        // 프레임 작업용 자료구조
        // ====================================================================

        // 이번 프레임 동안 "어느 타일에 어느 팀이 존재하는가"를 누적하는 임시 딕셔너리.
        // Tick 시작마다 Clear → 이번 프레임에 다시 채움.
        private readonly Dictionary<HexCoord, HashSet<TeamId>> _tilePresence
            = new Dictionary<HexCoord, HashSet<TeamId>>();

        // HashSet 인스턴스 재사용 풀.
        // 매 프레임 new HashSet<TeamId>를 만들면 GC 부담이 커지므로 사용 후 풀에 반환했다가
        // 다음 프레임에 다시 꺼내 쓴다. 외부에 노출되지 않는 내부 최적화.
        private readonly Queue<HashSet<TeamId>> _setPool
            = new Queue<HashSet<TeamId>>();

        // ====================================================================
        // 생성자
        // ====================================================================

        /// <summary>
        /// TileOwnershipService 생성자. GameBootstrapper에서 한 번만 호출된다.
        /// </summary>
        /// <param name="grid">소유권을 갱신할 HexGrid 인스턴스.</param>
        /// <param name="unitSpawn">살아있는 유닛 목록을 제공하는 UseCase.</param>
        /// <param name="positionProvider">유닛의 실제 월드 좌표를 반환하는 인터페이스.</param>
        public TileOwnershipService(HexGrid grid, UnitSpawnUseCase unitSpawn, IEntityPositionProvider positionProvider)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _positionProvider = positionProvider;
        }

        // ====================================================================
        // 매 프레임 처리
        // ====================================================================

        /// <summary>
        /// 한 프레임의 점령 상태를 갱신한다. GameBootstrapper.Update()에서 호출.
        ///
        /// 처리 순서:
        ///   1) 이전 프레임의 _tilePresence를 비우고 HashSet은 풀로 반환.
        ///   2) 살아있는 모든 유닛을 순회하며, 뷰 좌표를 도메인 좌표로 변환 후
        ///      어느 헥스 타일에 속하는지 계산 → _tilePresence에 누적.
        ///   3) 각 타일에 대해 점령 규칙을 적용. 한 팀만 있을 때만 SetOwner + 이벤트 발행.
        /// </summary>
        public void Tick()
        {
            // 안전 가드: 의존성이 빠져 있으면 무동작 (재경기 직후 등 일시 상태).
            if (_grid == null || _unitSpawn == null || _positionProvider == null) return;

            // ─── Step 1: 이전 프레임 누적 정보 초기화 ─────────────────────────
            // 각 HashSet은 Clear 후 풀에 반납하여 다음 프레임에 재사용.
            foreach (var set in _tilePresence.Values)
            {
                set.Clear();
                _setPool.Enqueue(set);
            }
            _tilePresence.Clear();

            // ─── Step 2: 살아있는 유닛의 물리 위치를 타일 단위로 집계 ────────
            foreach (var kvp in _unitSpawn.Units)
            {
                int unitId = kvp.Key;
                UnitData unitData = kvp.Value;

                // 죽은 유닛은 점령에 영향을 주지 않는다.
                if (!unitData.IsAlive) continue;

                // 중립 유닛은 점령 주체가 아니다(현재는 미사용 케이스이지만 방어적 처리).
                if (unitData.Team == TeamId.Neutral) continue;

                // 뷰 좌표(Transform.position) → 도메인 좌표 → 헥스 좌표 역산.
                // GameObject가 사라진 직후 0~1프레임 사이에는 Vector3.zero가 반환될 수 있다.
                Vector3 viewPos = _positionProvider.GetUnitWorldPosition(unitId);
                if (viewPos == Vector3.zero) continue;

                // Red 팀이면 ViewConverter가 좌표를 반전 출력했으므로 도메인 기준으로 되돌린다.
                Vector3 domainPos = ViewConverter.FromView(viewPos);

                // 가장 가까운 헥스 타일 좌표를 계산.
                HexCoord tile = HexMetrics.WorldToHex(domainPos);

                // 그리드 밖이면 무시. (HexCoord에는 IsInvalid 프로퍼티가 없으므로
                // 그리드 자체에 등록된 타일인지를 가지고 유효성을 판단한다.)
                if (!_grid.HasTile(tile)) continue;

                // 해당 타일의 팀 집합 확보 — 풀에서 꺼내거나 새로 생성.
                if (!_tilePresence.TryGetValue(tile, out HashSet<TeamId> teams))
                {
                    teams = _setPool.Count > 0 ? _setPool.Dequeue() : new HashSet<TeamId>();
                    // 풀에서 꺼낸 HashSet은 위에서 이미 Clear되어 있지만 안전을 위해 한 번 더 비운다.
                    teams.Clear();
                    _tilePresence[tile] = teams;
                }
                teams.Add(unitData.Team);
            }

            // ─── Step 3: 점령 규칙 적용 ──────────────────────────────────────
            foreach (var kvp in _tilePresence)
            {
                HexCoord tile = kvp.Key;
                HashSet<TeamId> teams = kvp.Value;

                // 한 팀만 존재하는 경우에만 점령으로 간주.
                // (양 팀 동시에 있으면 현재 상태 유지 = 분쟁지 처리.)
                if (teams.Count != 1) continue;

                // HashSet에서 단일 팀을 꺼낸다 — foreach로 첫 원소만 가져옴.
                TeamId claimingTeam = TeamId.Neutral;
                foreach (var t in teams)
                {
                    claimingTeam = t;
                    break;
                }

                // 이미 같은 팀이 점령 중이면 갱신/이벤트 발행 모두 생략 (성능 + 노이즈 감소).
                if (_grid.GetOwner(tile) == claimingTeam) continue;

                // 실제 갱신 + 이벤트 발행.
                // HexTileView가 OnTileOwnerChanged를 구독해 색상을 즉시 변경한다.
                _grid.SetOwner(tile, claimingTeam);
                GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(tile, claimingTeam));
            }
        }
    }
}
