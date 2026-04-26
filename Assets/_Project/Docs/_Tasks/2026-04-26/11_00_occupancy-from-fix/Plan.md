# Plan: 4차 개선 — FROM 타일 점유 해제 타이밍 수정

**날짜**: 2026-04-26  
**작업 ID**: 11_00_occupancy-from-fix  
**담당**: game-programmer 에이전트  
**관련 Research**: Research.md

---

## 목표

`RegisterOccupancyMove`가 Lerp 시작 전에 FROM 점유를 즉시 해제하는 것을 차단한다.  
FROM 해제는 Lerp 완료 후 `ProcessStep`에서만 수행하도록 분리한다.

---

## 변경 파일 목록

| 파일 | 변경 유형 |
|------|----------|
| `Assets/_Project/Scripts/Application/Services/TileOccupancyManager.cs` | 메서드 추가 |
| `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs` | 메서드 2개 수정 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 변경 없음 |

---

## Step 1 — TileOccupancyManager.cs: ReserveOccupancy 추가

`Increase`는 현재 `private`이므로 TO만 증가시키는 public 래퍼를 추가한다.

**추가 위치**: `OnUnitMoved` 메서드 바로 아래 (약 98번째 줄 이후)

```csharp
/// <summary>
/// Lerp 시작 전에 도착 타일(to)의 점유를 미리 예약한다.
///
/// [4차 개선 — 2026-04-26] RegisterOccupancyMove와의 분리:
///   기존 OnUnitMoved(from, to)는 FROM-1, TO+1을 동시에 처리했다.
///   그러나 Lerp 시작 직전에는 유닛이 물리적으로 아직 FROM에 있으므로
///   FROM을 즉시 감소시키면 "FROM이 비어있다"는 거짓 판정을 유발한다.
///   이 메서드는 TO+1만 수행하고, FROM-1은 Lerp 완료 후 ProcessStep에서 처리한다.
/// </summary>
/// <param name="tile">예약할 도착 타일 좌표.</param>
/// <param name="unitSize">이동하려는 유닛의 OccupancySize.</param>
public void ReserveOccupancy(HexCoord tile, float unitSize)
{
    if (IsInvalid(tile)) return;
    Increase(tile, unitSize);
}
```

---

## Step 2 — UnitMovementUseCase.cs: RegisterOccupancyMove 수정

`OnUnitMoved(from, to)` → `ReserveOccupancy(to)` 로 변경.  
FROM은 더 이상 여기서 건드리지 않는다.

**변경 전 (현재 코드)**:
```csharp
public void RegisterOccupancyMove(HexCoord from, HexCoord to, UnitType type)
{
    if (_occupancyManager == null) return;
    float size = GetOccupancySize(type);
    _occupancyManager.OnUnitMoved(from, to, size);
}
```

**변경 후**:
```csharp
/// <summary>
/// [4차 개선 — 2026-04-26] TO 타일 점유만 예약한다.
///
/// 변경 이유:
///   기존에는 OnUnitMoved(from, to)로 FROM-1, TO+1을 동시 처리했다.
///   그러나 이 시점에 유닛은 물리적으로 아직 FROM에 있으므로
///   FROM을 즉시 감소시키면 다른 유닛이 FROM을 "빈 타일"로 잘못 판정한다.
///   FROM 해제는 Lerp 완료 후 ProcessStep에서 수행한다.
/// </summary>
public void RegisterOccupancyMove(HexCoord from, HexCoord to, UnitType type)
{
    if (_occupancyManager == null) return;
    float size = GetOccupancySize(type);
    // TO만 예약. FROM은 ProcessStep에서 Lerp 완료 후 해제한다.
    _occupancyManager.ReserveOccupancy(to, size);
}
```

**주의**: `from` 파라미터는 서명에서 유지한다(UnitView 호출부 변경 없음).

---

## Step 3 — UnitMovementUseCase.cs: ProcessStep 수정

Lerp 완료 후 FROM 점유를 해제하는 로직을 추가한다.

**변경 전 (현재 코드)**:
```csharp
public void ProcessStep(UnitData unit, HexCoord from, HexCoord to)
{
    HexDirection dir = FacingDirection.FromCoords(from, to);
    unit.Facing = dir;
    unit.Position = to;
    _grid.SetOwner(to, unit.Team);
    GameEvents.OnUnitMoved.OnNext(new UnitMovedEvent(unit.Id, from, to));
    GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(to, unit.Team));
}
```

**변경 후**:
```csharp
public void ProcessStep(UnitData unit, HexCoord from, HexCoord to)
{
    // [4차 개선 — 2026-04-26] FROM 타일 점유 해제.
    // RegisterOccupancyMove는 TO+1만 예약했으므로, 여기서 FROM-1을 처리한다.
    // 유닛이 Lerp를 완료하여 실제로 FROM을 떠난 시점이므로 타이밍이 정확하다.
    //
    // 처리 조건:
    //   - from != to: 같은 타일 이동(Phase 2 스냅 등)은 변동 없음.
    //   - _occupancyManager != null: 서버에서만 동작, null 방어.
    //   - OnUnitRemoved 내부에 IsInvalid 체크 있음 → 스폰(from=default) 안전.
    if (from != to && _occupancyManager != null)
    {
        _occupancyManager.OnUnitRemoved(from, GetOccupancySize(unit.Type));
    }

    HexDirection dir = FacingDirection.FromCoords(from, to);
    unit.Facing = dir;
    unit.Position = to;
    _grid.SetOwner(to, unit.Team);
    GameEvents.OnUnitMoved.OnNext(new UnitMovedEvent(unit.Id, from, to));
    GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(to, unit.Team));
}
```

---

## 엣지 케이스 검증

| 케이스 | 동작 |
|--------|------|
| **스폰** (from=default) | RegisterOccupancyMove → ReserveOccupancy(spawnTile)+1.<br>ProcessStep from=default → OnUnitRemoved(default) → IsInvalid=true → skip. ✓ |
| **첫 이동** (from=spawnTile) | RegisterOccupancyMove → ReserveOccupancy(nextTile)+1.<br>ProcessStep → OnUnitRemoved(spawnTile)-1. ✓ |
| **이동 중 전투 중단** | RegisterOccupancyMove → TO+1.<br>ProcessStep 미호출 → FROM 해제 안 됨 (유닛이 FROM에 계속 있음). ✓ |
| **이동 중 사망** | ReleaseOccupancyIfPending → TO-1 ✓.<br>OnEntityDied → OnUnitRemoved(FROM)-1 → FROM이 아직 감소 안 됐으므로 1회만 감소. 이중 해제 해결. ✓ |
| **from == to (Phase 2 스냅)** | RegisterOccupancyMove → ReserveOccupancy(to)+1.<br>ProcessStep `from != to` 조건 false → OnUnitRemoved 미호출.<br>ReleaseOccupancyIfPending이 _pendingOccupancyTile(=to) -1 처리함. ✓ |
| **_occupancyManager null** | RegisterOccupancyMove 시작에 null 체크 있음. ProcessStep에도 null 체크 추가. ✓ |

---

## 구현 후 검증 체크리스트 (Testcase.md 작성 기준)

1. 다수 유닛(소형 5마리 이상)이 같은 타일로 이동 시 MaxOccupancy 초과 여부
2. 이동 도중 적 발견하여 전투 진입 시 FROM 타일 점유가 올바르게 유지되는가
3. 이동 도중 유닛 사망 시 이중 해제 없이 점유가 정확히 복원되는가
4. 스폰 직후 첫 이동 시 점유 정상 등록/해제 여부
5. Phase 2 스냅 이동 후 점유 누수 없음 확인
