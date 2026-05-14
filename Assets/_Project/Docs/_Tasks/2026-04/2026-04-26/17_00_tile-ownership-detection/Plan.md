# Plan: 타일 소유권 실시간 감지 시스템

**날짜**: 2026-04-26  
**작업 ID**: 17_00_tile-ownership-detection  
**담당**: game-programmer 에이전트  
**관련 Research**: Research.md

---

## 목표

Phase 0(타일 이동)에서만 갱신되던 타일 소유권을,  
**독립 서비스(TileOwnershipService)가 매 프레임 물리 위치 기반으로 갱신**하여  
Phase 1(직선 추적) 중에도 유닛이 지나가는 타일이 즉시 점령되도록 한다.

---

## 점령 규칙

| 케이스 | 동작 |
|---|---|
| 타일 위에 한 팀 유닛만 있음 | 즉시 그 팀으로 점령 |
| 타일 위에 양 팀 모두 있음 | 현재 점령 상태 유지 |
| 타일 위에 유닛 없음 | 현재 점령 상태 유지 |

---

## 변경 파일 목록

| 파일 | 변경 유형 |
|---|---|
| `Assets/_Project/Scripts/Application/Services/TileOwnershipService.cs` | 신규 생성 |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | TileOwnershipService 주입 + Update() 호출 추가 |

---

## Step 1 — TileOwnershipService 신규 생성

**파일**: `Assets/_Project/Scripts/Application/Services/TileOwnershipService.cs`

```csharp
// ============================================================================
// TileOwnershipService.cs
// 매 프레임 모든 유닛의 물리 위치를 확인하여 타일 소유권을 실시간으로 갱신한다.
//
// [기존 문제]
//   Phase 0(타일 이동)에서는 ProcessStep이 호출되어 타일 소유권이 갱신되지만,
//   Phase 1(월드 좌표 직선 추적)에서는 ProcessStep이 호출되지 않아
//   유닛이 여러 타일을 지나가도 소유권이 전혀 갱신되지 않았다.
//
// [해결 방식]
//   이 서비스는 유닛의 이동 방식(Phase 0/1/2)과 무관하게,
//   매 프레임 유닛의 실제 물리 위치(transform.position)를 HexCoord로 변환하여
//   타일 소유권을 직접 결정한다.
//
// [점령 규칙]
//   1. 타일 위에 한 팀만 있으면 → 즉시 그 팀으로 점령
//   2. 양 팀 모두 있으면 → 현재 상태 유지 (전투 중)
//   3. 유닛이 없으면 → 현재 상태 유지 (마지막 점령 팀 유지)
//
// [네트워크]
//   서버(또는 싱글플레이)에서만 실행한다.
//   클라이언트는 NetworkTileSync를 통해 OnTileOwnerChanged 이벤트를 수신한다.
//
// Application 레이어 — Unity 최소 의존 (Vector3, HexMetrics, ViewConverter 사용)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Core;

namespace Hexiege.Application.Services
{
    public class TileOwnershipService
    {
        // 타일 소유권을 결정하는 데 필요한 의존성
        private readonly HexGrid _grid;
        private readonly UnitSpawnUseCase _unitSpawn;
        private readonly IEntityPositionProvider _positionProvider;

        // 이번 프레임에 타일별로 어떤 팀의 유닛이 있는지 기록.
        // 매 Tick마다 초기화 후 재구성된다.
        // Key: HexCoord (타일 좌표), Value: 그 타일 위에 있는 팀 집합
        private readonly Dictionary<HexCoord, HashSet<TeamId>> _tilePresence
            = new Dictionary<HexCoord, HashSet<TeamId>>();

        // HashSet 재사용 풀 — 매 프레임 new HashSet 생성으로 인한 GC 부담 감소.
        // _tilePresence를 Clear()할 때 반환하고, 새 타일 등록 시 꺼내 재사용한다.
        private readonly Queue<HashSet<TeamId>> _setPool
            = new Queue<HashSet<TeamId>>();

        public TileOwnershipService(
            HexGrid grid,
            UnitSpawnUseCase unitSpawn,
            IEntityPositionProvider positionProvider)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _positionProvider = positionProvider;
        }

        /// <summary>
        /// 매 프레임 GameBootstrapper.Update()에서 호출.
        /// 서버(또는 싱글플레이)에서만 실행해야 한다.
        /// </summary>
        public void Tick()
        {
            // ─────────────────────────────────────────────────
            // Step 1: 이전 프레임 데이터 초기화
            //         사용한 HashSet은 풀에 반환하여 재사용 준비
            // ─────────────────────────────────────────────────
            foreach (var set in _tilePresence.Values)
            {
                set.Clear();
                _setPool.Enqueue(set);
            }
            _tilePresence.Clear();

            // ─────────────────────────────────────────────────
            // Step 2: 모든 살아있는 유닛의 물리 위치 → HexCoord 변환
            //         타일별 팀 존재 여부 집계
            // ─────────────────────────────────────────────────
            foreach (var (id, unitData) in _unitSpawn.Units)
            {
                // 사망한 유닛은 건너뜀
                if (!unitData.IsAlive) continue;

                // 중립 유닛(유닛이 없어야 할 타일 등 예외 케이스)은 건너뜀
                if (unitData.Team == TeamId.Neutral) continue;

                // 유닛의 현재 뷰 좌표 (transform.position, Phase 1 중에도 정확한 물리 위치)
                Vector3 viewPos = _positionProvider.GetUnitWorldPosition(id);

                // 유닛 GameObject가 파괴된 경우 (Vector3.zero 반환) 건너뜀
                if (viewPos == Vector3.zero) continue;

                // 뷰 좌표 → 도메인 좌표 → 타일 좌표 변환
                // ViewConverter.FromView: Red팀 좌표 반전 처리 포함
                Vector3 domainPos = ViewConverter.FromView(viewPos);
                HexCoord tile = HexMetrics.WorldToHex(domainPos);

                // 유효하지 않은 타일(그리드 범위 밖)은 건너뜀
                if (tile.IsInvalid) continue;

                // 타일 존재 여부 등록
                if (!_tilePresence.TryGetValue(tile, out HashSet<TeamId> teams))
                {
                    // 풀에서 HashSet 꺼내거나 없으면 새로 생성
                    teams = _setPool.Count > 0 ? _setPool.Dequeue() : new HashSet<TeamId>();
                    _tilePresence[tile] = teams;
                }
                teams.Add(unitData.Team);
            }

            // ─────────────────────────────────────────────────
            // Step 3: 점령 규칙 적용
            //         한 팀만 있는 타일만 소유권 갱신.
            //         양 팀 / 유닛 없음은 현재 상태 유지.
            // ─────────────────────────────────────────────────
            foreach (var (tile, teams) in _tilePresence)
            {
                // 규칙 1: 한 팀만 있음 → 즉시 그 팀으로 점령
                if (teams.Count == 1)
                {
                    // HashSet에서 유일한 팀 꺼내기
                    TeamId claimingTeam = TeamId.Neutral;
                    foreach (var t in teams) claimingTeam = t;

                    // 현재 소유권과 다를 때만 갱신하여 이벤트 발행 빈도 최소화
                    if (_grid.GetOwner(tile) != claimingTeam)
                    {
                        _grid.SetOwner(tile, claimingTeam);
                        GameEvents.OnTileOwnerChanged.OnNext(
                            new TileOwnerChangedEvent(tile, claimingTeam));
                    }
                }
                // 규칙 2: 양 팀 모두 있음 (teams.Count >= 2) → 아무것도 하지 않음
                // 규칙 3: 유닛 없음 → _tilePresence에 없으므로 이 루프에 진입하지 않음
            }
        }
    }
}
```

---

## Step 2 — HexGrid에 GetOwner 추가 (없는 경우)

**파일**: `Assets/_Project/Scripts/Domain/Hex/HexGrid.cs`

현재 `SetOwner(tile, team)` 메서드만 있다면, 소유권 비교를 위해 `GetOwner(tile)` 조회 메서드도 필요하다.  
이미 존재하는지 먼저 확인 후, 없으면 추가한다.

```csharp
/// <summary>
/// 타일의 현재 소유 팀 반환. 유효하지 않은 타일이면 TeamId.Neutral 반환.
/// </summary>
public TeamId GetOwner(HexCoord tile)
{
    if (!_tiles.TryGetValue(tile, out HexTile hexTile)) return TeamId.Neutral;
    return hexTile.Owner;
}
```

---

## Step 3 — GameBootstrapper에 TileOwnershipService 주입 및 Tick 호출

**파일**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

### 3-1. 필드 추가

```csharp
// TileOwnershipService — 매 프레임 유닛 물리 위치 기반 타일 소유권 갱신
private TileOwnershipService _tileOwnership;
```

### 3-2. LoadMap() 내 초기화 (기존 _tileOccupancy, _unitCombat 생성 이후에 추가)

```csharp
// TileOwnershipService: 유닛 위치 기반 실시간 타일 점령 서비스
// _positionProvider, _unitSpawn, _grid는 이 시점에 이미 초기화되어 있어야 한다.
_tileOwnership = new TileOwnershipService(_grid, _unitSpawn, _positionProvider);
```

### 3-3. Update()에 Tick 호출 추가

기존 패턴(`TickCooldowns` 등)과 동일하게,  
서버 또는 싱글플레이에서만 실행되도록 조건 가드를 확인하고 추가한다.

```csharp
// TileOwnershipService: 유닛 물리 위치 기반 타일 소유권 실시간 갱신
// 서버(또는 싱글플레이)에서만 실행한다.
_tileOwnership?.Tick();
```

> ⚠️ 기존 `TickCooldowns` 등의 서버 가드 조건을 그대로 따른다.  
> 네트워크 활성 시 클라이언트에서는 호출하지 않도록 주의.

---

## 구현 시 주의사항

1. **`HexGrid.GetOwner` 존재 확인**: Step 2 적용 전 HexGrid.cs에서 이미 존재하는지 확인.  
   `HexTile.Owner` 필드 접근 방식도 함께 확인.

2. **`_positionProvider` 초기화 순서**: `GameBootstrapper.LoadMap()`에서 `_positionProvider`가  
   `TileOwnershipService` 생성 이전에 이미 초기화되어 있는지 확인.

3. **`ViewConverter.FromView`의 서버 동작**: 서버는 Blue팀 관점으로 ViewConverter가 설정된다.  
   Red팀 유닛의 transform.position을 FromView로 변환할 때 올바른 HexCoord가 나오는지  
   Phase 2 스냅 코드(`UnitView.cs`)의 동일 변환 패턴과 비교하여 검증.

4. **`ProcessStep`과의 중복 호출**: Phase 0에서는 ProcessStep이 `SetOwner`를 호출하고,  
   같은 프레임에 TileOwnershipService도 `SetOwner`를 호출할 수 있다.  
   결과는 동일하므로 문제없으나, `GetOwner != claimingTeam` 조건으로 이벤트 중복 발행은 방지된다.

5. **주석은 한국어로 상세하게**: 유니티 초급 개발자도 이해할 수 있는 수준.

---

## 엣지 케이스 검증

| 케이스 | 동작 |
|---|---|
| Phase 1에서 빠르게 통과한 타일 | 그 순간 한 팀만 있으면 즉시 점령, 이후 유닛 없음 → 유지 |
| 양 팀 교전 중 타일 | 현재 소유권 유지, 전투 종료 후 살아남은 팀이 자동 점령 |
| 유닛 사망 직후 프레임 | IsAlive=false → 건너뜀. 다음 Tick에서 나머지 팀으로 자동 갱신 |
| 유닛 없는 타일 | _tilePresence에 미등록 → 소유권 변경 없음 (마지막 점령 유지) |
| 성/건물 타일 | 유닛이 진입하지 않으므로 소유권 변경 없음 (건물 배치 시 별도 처리 유지) |
| 중립 유닛 (TeamId.Neutral) | 건너뜀 — 중립 유닛이 존재하더라도 타일 점령 없음 |
| 그리드 범위 밖 좌표 | IsInvalid 체크로 건너뜀 |
| Vector3.zero 반환 (유닛 소멸) | viewPos == Vector3.zero 체크로 건너뜀 |
