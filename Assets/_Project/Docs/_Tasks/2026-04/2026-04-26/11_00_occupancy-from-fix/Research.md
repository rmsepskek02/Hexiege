# Research: 4차 개선 — FROM 타일 점유 해제 타이밍 수정

**날짜**: 2026-04-26  
**작업 ID**: 11_00_occupancy-from-fix  
**담당**: game-programmer 에이전트

---

## 1. 배경

3차 개선(2026-04-25)에서 `RegisterOccupancyMove`를 Lerp **시작 전**에 호출하도록 변경하여,  
같은 프레임에 다른 유닛이 경쟁적으로 같은 TO 타일을 선점하는 Race Condition을 수정했다.

QA 정적 분석은 CONDITIONAL PASS를 받았으나,  
실제 플레이 테스트에서 **다수 유닛이 동일 타일에 극심하게 겹치는 현상이 여전히 발생**하고 있다.

---

## 2. 현재 코드 분석

### 2-A. RegisterOccupancyMove (UnitMovementUseCase.cs, Lerp 시작 전 호출)

```csharp
public void RegisterOccupancyMove(HexCoord from, HexCoord to, UnitType type)
{
    if (_occupancyManager == null) return;
    float size = GetOccupancySize(type);
    _occupancyManager.OnUnitMoved(from, to, size);  // FROM-1, TO+1 동시 처리
}
```

`OnUnitMoved(from, to)`는 FROM 점유를 **즉시 감소**, TO 점유를 **즉시 증가**시킨다.

### 2-B. ProcessStep (UnitMovementUseCase.cs, Lerp 완료 후 호출)

```csharp
public void ProcessStep(UnitData unit, HexCoord from, HexCoord to)
{
    // 점유량 변경 없음 (3차 개선에서 제거됨)
    HexDirection dir = FacingDirection.FromCoords(from, to);
    unit.Facing = dir;
    unit.Position = to;
    _grid.SetOwner(to, unit.Team);
    GameEvents.OnUnitMoved.OnNext(new UnitMovedEvent(unit.Id, from, to));
    GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(to, unit.Team));
}
```

현재 ProcessStep은 점유량을 전혀 건드리지 않는다.

---

## 3. 근본 원인

### 버그 시나리오

**RegisterOccupancyMove가 Lerp 시작 시점에 FROM을 즉시 해제한다.**  
이때 유닛은 물리적으로 아직 FROM에 있다.

1. 유닛 A가 FROM→TO로 이동 시작
2. `RegisterOccupancyMove(FROM, TO)` 호출 → **FROM-1, TO+1**  
   → FROM의 점유량이 감소됨 (실제로 A는 여전히 FROM에 있음)
3. A가 Lerp 도중 적을 발견하여 이동 중단 (t≈0)  
   → A의 도메인 Position은 여전히 FROM  
   → 그러나 FROM 점유량은 이미 감소된 상태
4. 같은 프레임에 유닛 B, C, D가 `FindAvailableTile(FROM)` 체크  
   → FROM이 "비어 있다"고 판정 → B, C, D 모두 FROM으로 진입  
   → **결과: FROM에 A(실제 존재) + B + C + D가 시각적으로 겹침 (>3 유닛)**

---

## 4. 부가 버그: 사망 시 이중 해제 (Death-During-Lerp)

유닛이 Lerp 도중 사망할 때의 현재 동작:

1. `ReleaseOccupancyIfPending()` → `_pendingOccupancyTile` (=TO) -1 ✓ (올바름)
2. `OnEntityDied` 구독 → `unit.Position` (=FROM) -1 ✗  
   → 그러나 FROM은 `RegisterOccupancyMove`에서 **이미 감소됨**  
   → **이중 해제 발생** → FROM이 실제보다 비어있는 것으로 판정됨

이 버그도 4차 개선 수정으로 함께 해결된다.

---

## 5. 수정 방향

**Lerp 시작 전: TO 점유만 예약 (FROM은 건드리지 않음)**  
**Lerp 완료 후: FROM 점유 해제 (ProcessStep에서 처리)**

유닛이 Lerp 도중에는 FROM과 TO 양쪽 점유량이 동시에 기록되어 있는 상태가 된다.  
이는 유닛이 두 타일 사이를 이동 중이라는 사실을 점유 시스템이 정확하게 반영한 것으로,  
내부 구현 세부사항이며 사용자(플레이어) 시점에서는 보이지 않는다.

### 장점
- FROM 타일이 유닛이 실제로 떠날 때까지 "차있음"으로 유지됨
- 전투 중 이동 중단 시 FROM이 거짓으로 비어보이는 현상 차단
- 사망 시 이중 해제 버그도 자동으로 해결됨

### 단점
- Lerp 기간 동안 FROM+TO 양쪽에 점유량이 중복 계산됨  
  → MaxOccupancy=3 기준으로 일시적으로 4~6점유처럼 보일 수 있음  
  → **실제 영향 없음**: FindAvailableTile은 FROM과 TO를 독립적으로 체크하므로,  
    각 타일의 점유량이 3 이하면 진입 허용. 이동 중인 유닛이 차지하는 점유는  
    해당 타일의 빈자리 계산에 포함되어 다른 유닛이 진입을 적절히 회피함.

---

## 6. 수정 대상 파일

| 파일 | 변경 내용 |
|------|----------|
| `TileOccupancyManager.cs` | `ReserveOccupancy(HexCoord tile, float unitSize)` public 메서드 추가 |
| `UnitMovementUseCase.cs` | `RegisterOccupancyMove`: `OnUnitMoved` → `ReserveOccupancy` 변경<br>`ProcessStep`: 첫 줄에 `OnUnitRemoved(from, size)` 추가 |
| `UnitView.cs` | 변경 없음 |

---

## 7. 변경하지 않는 것

- `ReleaseOccupancyIfPending()` — TO 해제 로직은 그대로 (pendingOccupancyTile을 쓰고 있어 올바름)
- `OnEntityDied` 구독 — FROM에서 -1하는 로직은 그대로 (4차 개선 후에는 정상 동작)
- UnitView.cs의 Phase 0 Lerp 루프 — `RegisterOccupancyMove` 호출 타이밍 변경 없음
