# Research: 건물 인근 타일 이동/공격 불가 버그

**날짜:** 2026-03-17
**증상:**
1. 유닛이 건물 인근 타일에 접근하지 못함
2. 유닛이 건물 인근 타일에 있음에도 건물을 공격하지 못함

---

## 1. 관련 코드 파악

### BuildingPlacementUseCase.cs — PlaceBuildingInternal (라인 145-173)

```csharp
private BuildingData PlaceBuildingInternal(BuildingType type, TeamId team,
    HexCoord position, HexTile tile)
{
    // ...
    tile.IsWalkable = false;       // ← 건물 타일 이동 불가 설정
    _grid.SetOwner(position, team);

    // 인접 타일 소유권만 변경 (IsWalkable 미변경)
    var neighbors = _grid.GetNeighbors(position);
    foreach (var neighbor in neighbors)
    {
        if (neighbor.Owner != team)
        {
            _grid.SetOwner(neighbor.Coord, team);
            GameEvents.OnTileOwnerChanged.OnNext(...);
        }
    }
    // ...
}
```

**핵심**: 건물 타일 `IsWalkable = false`, 인접 타일은 Owner만 변경(IsWalkable 유지)

---

### HexPathfinder.FindPath (라인 44-48)

```csharp
// 도착 타일이 없거나 이동 불가면 즉시 null 반환
if (grid.GetTile(goal) == null || !grid.GetTile(goal).IsWalkable) return null;

// 도착지가 blocked에 포함되면 경로 없음
if (blockedCoords != null && blockedCoords.Contains(goal)) return null;
```

**핵심**: 목표 타일이 `IsWalkable=false`이면 A* 탐색 자체를 시작하지 않음

---

### HexGrid.GetWalkableNeighborCoords (라인 197-210)

```csharp
public List<HexCoord> GetWalkableNeighborCoords(HexCoord coord)
{
    var result = new List<HexCoord>(HexDirectionExtensions.Count);
    for (int i = 0; i < HexDirectionExtensions.Count; i++)
    {
        HexDirection dir = (HexDirection)i;
        HexCoord neighborCoord = dir.Neighbor(coord);
        if (_tiles.TryGetValue(neighborCoord, out HexTile tile) && tile.IsWalkable)
            result.Add(neighborCoord);
    }
    return result;
}
```

**핵심**: `IsWalkable=false` 타일은 이웃 목록에서 제외 → A* 루프에서 건물 타일 우회 불가

---

### UnitMovementUseCase.RequestMove (라인 52-79)

```csharp
public List<HexCoord> RequestMove(UnitData unit, HexCoord target, ...)
{
    var blocked = new HashSet<HexCoord>();
    foreach (var other in _units.GetAll())
    {
        if (other.Id == unit.Id) continue;
        blocked.Add(other.Position);                        // 모든 유닛 현재 위치
        if (other.Team == unit.Team)
            blocked.Add(other.ClaimedTile);                 // 아군 ClaimedTile
    }
    var path = HexPathfinder.FindPath(_grid, unit.Position, target, blocked);
    return path;
}
```

**핵심**: `target`을 그대로 PathFinder에 전달. 건물 위치를 target으로 전달하면 PathFinder에서 즉시 null 반환

---

### UnitCombatUseCase — 공격 범위 판정 (라인 95-156)

```csharp
private IDamageable FindFirstEnemyTarget(UnitData attacker)
{
    float maxDist = attacker.AttackRange * HexMetrics.TileHeight;  // 월드 거리 기준

    // 적 유닛 탐색
    foreach (var unit in _units.GetAll())
    {
        float dist = Vector3.Distance(attackerWorldPos, targetWorldPos);
        if (dist <= maxDist && dist < minWorldDist) closestTarget = unit;
    }

    // 적 건물 탐색
    foreach (var building in _buildings.GetAll())
    {
        float dist = Vector3.Distance(attackerWorldPos, buildingWorldPos);
        if (dist <= maxDist && dist < minWorldDist) closestTarget = building;
    }
}
```

**핵심**: 월드 좌표 기반 거리 판정. `attacker.AttackRange * HexMetrics.TileHeight`가 maxDist

---

## 2. 버그 원인 분석

### 원인 1: 이동 명령 목표가 건물 위치일 때 (이동 불가)

유닛 AI(Siege 이동 또는 공격 이동)에서 건물 위치를 이동 목표로 전달하면:

```
UnitMovementUseCase.RequestMove(unit, buildingPosition)
  → HexPathfinder.FindPath(grid, unitPos, buildingPosition, blocked)
    → GetTile(buildingPosition).IsWalkable == false
    → return null  (경로 탐색 포기)
  → path = null → 이동 불가
```

**결과**: 유닛이 건물 타겟을 향해 이동 명령을 받지 못함

---

### 원인 2: 경로를 찾더라도 공격 범위 미달 (공격 불가)

만약 유닛이 건물 인근 타일까지 도달했을 때:
- 월드 좌표 기반 판정은 `AttackRange * TileHeight` 내에 건물이 있으면 공격 가능
- **Pistoleer**: AttackRange=1.0, TileHeight=0.866 → maxDist=0.866
- **Assault**: AttackRange=2.0, TileHeight=0.866 → maxDist=1.732
- **Sniper**: AttackRange=5.0, TileHeight=0.866 → maxDist=4.33

인접 타일에서 건물까지의 실제 월드 거리: FlatTop 기준 약 0.866 world units
→ Pistoleer maxDist(0.866) ≈ 인접 거리, 부동소수점 오차로 공격 실패 가능성 있음

---

### 원인 3: 적 Castle 공격 이동 로직 확인 필요

UnitCombatUseCase나 SiegeUseCase에서 적 Castle(건물)에 대해 이동 명령을 어떻게 내리는지 확인이 필요.
- 건물 Position을 그대로 target으로 전달하는가?
- 건물 인접 타일을 자동으로 찾아서 전달하는가?

---

## 3. 영향 범위

| 파일 | 영향 | 수정 필요 여부 |
|------|------|--------------|
| `UnitMovementUseCase.cs` | RequestMove target 검증 없음 | **수정 필요** |
| `HexPathfinder.cs` | 목표 IsWalkable 체크 → null | 수정 가능 (또는 UseCase에서 처리) |
| `UnitCombatUseCase.cs` | maxDist 계산 방식 | 검토 필요 |
| `BuildingPlacementUseCase.cs` | IsWalkable 정책 | 변경 불필요 |

---

## 4. 추가 확인 필요

- `SiegeUseCase.cs` 또는 유닛 AI에서 적 Castle을 타겟으로 이동 명령 내리는 코드
- `UnitView.cs` 또는 `NetworkUnitMovementController.cs`에서 공격 목표로 이동 요청하는 코드

**예상 위치**: 유닛이 이동 후 적 건물을 공격하는 흐름을 담당하는 Use Case 또는 Controller
