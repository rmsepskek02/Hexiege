# Research — 이동 슬롯 유령 점유 & 이중 코루틴 버그 수정

작성일: 2026-05-06  
관련 로그: `_Logs/2026-04-30/02_29_movement-combat-redesign/Log.md` (Round 2)  
관련 TC: `_Tasks/2026-04-30/02_29_movement-combat-redesign/Testcase.md` (MULTI-01~05)

---

## 이 작업이 무엇인지

멀티플레이 실기 테스트(2026-05-06)에서 유닛이 타일 사이 허공에 멈추고, 다른 유닛들이 그 허공에 고정된 유닛을 피하느라 경로가 꼬이는 현상이 관찰되었다. 또한 건물이 건설될 때 전투 중인 유닛이 갑자기 이동하려 하거나, 우회 이동 시 제자리 순간이동이 발생했다.

원인은 모두 **이동 슬롯(타일 위 대기 자리)** 이 잘못된 시점에 점유되거나 해제되지 않아 발생하는 문제다. 크게 4가지 버그로 분류된다.

---

## BUG-005 — 이동 중 전투 진입 시 이동 슬롯 미해제

**현상**: 유닛이 타일 A에서 타일 B로 이동하는 애니메이션 중에 적을 발견하고 전투에 진입할 때, 타일 B의 이동 슬롯이 해제되지 않은 채 유닛이 A-B 사이 허공에 고정된다.

**파일**: `Presentation/Unit/UnitView.cs`  
**메서드**: `RunTileTraversal()` (라인 1936~)  
**문제 구간**: 라인 2107~2149

```
// 타일 B 슬롯 클레임 후(라인 2066~2071) Lerp 시작
// ...Lerp 진행 중...
// 라인 2112: 원거리 적 감지 → EnterStationaryCombat 호출(라인 2132)
// 이 시점에 ClaimedMoveSlot(타일 B 슬롯)을 해제하는 코드 없음
```

- 근접 유닛은 라인 2169~2173에서 `ReleaseV2MoveSlotIfClaimed()` 호출이 존재하지만, 원거리 유닛(Ranged) 분기에는 없음
- 로그 근거: `SLOT_CLAIM tile=(5,8)` → `STATIONARY_COMBAT_START currentTile=(5,7)` → 사망 후에야 `SLOT_RELEASE tile=(5,8)`
- Pistoleer(원거리) 유닛 전체(UnitID 4, 8, 12, 16, 24)에서 동일 패턴 반복

---

## BUG-006 — 전투 재개 직후 적 감지 시 이동 슬롯 미해제

**현상**: 전투 종료 후 이동을 재개하는 유닛이 첫 타일로의 슬롯을 선점하자마자 바로 다른 적을 감지하여 전투에 진입할 때, 방금 선점한 슬롯이 해제되지 않는다. 같은 타겟이 죽을 때 여러 유닛이 동시에 이 상황에 빠져 유령 슬롯이 한꺼번에 대량 생성된다.

**파일**: `Presentation/Unit/UnitView.cs`  
**메서드**: `ResumeFromForwardTile()` (라인 2607~) + `RunTileTraversal()` 재진입

**문제 흐름**:
1. 라인 2184: `ResumeFromForwardTile()` 호출 → `V2Result.Repath` 반환
2. 라인 1909: 외부 while 루프에서 `RunTileTraversal()` 재진입
3. 라인 2066~2071: 재진입 첫 사이클에서 다음 타일(N)의 슬롯 클레임(`_v2MoveSlotTile = to`)
4. 라인 2107~2149: 즉시 원거리 적 감지 → `EnterStationaryCombat` 진입 — 슬롯 N 미해제

로그 근거: `RESUME_FORWARD` → `TILE_MOVE from=(5,6) to=(5,5)` → `SLOT_CLAIM tile=(5,5)` → `ENEMY_DETECTED` → `COMBAT_START mode=Melee-InRange currentTile=(5,6)`. UnitID 15, 14, 13, 6이 동시 발생.

---

## BUG-007 — Eager Repath가 전투 중 유닛에게 이동 코루틴 재시작

**현상**: 건물이 건설·파괴될 때 전투 중인 유닛도 경로 재계산 대상이 되어 이동 코루틴이 새로 시작된다. 기존 전투 코루틴이 종료되지 않아 두 코루틴이 동시에 실행되고, 이동 슬롯을 중복 점유한다.

**파일 1**: `Bootstrap/GameBootstrapper.cs`  
**메서드**: `RepathAllAliveUnits()` (라인 694~716)

```csharp
// 라인 705: 사망 여부만 확인, 전투 상태 확인 없음
if (unit == null || !unit.IsAlive) continue;
// ...
view.OnPathInvalidated();  // 라인 714: 전투 중 유닛도 호출
```

**파일 2**: `Presentation/Unit/UnitView.cs`  
**메서드**: `OnPathInvalidated()` (라인 728~742)

```csharp
// 전투 상태 확인 없이 새 경로 계산 + 이동 코루틴 교체
MoveTo(newPath);  // 라인 739
```

로그 근거: `STATIONARY_COMBAT_START at tile=(5,8)` → 00:12:08 시각 전체 유닛 동시 `PATH_CALC` → `UNIT_DIED position=(5,8) hadMoveSlot=True SLOT_RELEASE tile=(5,9)`. 전투 타일(5,8)에서 (5,9) 슬롯을 별도 획득한 것이 이중 코루틴의 직접 증거.

---

## BUG-008 — DETOUR 목적지로 현재 유닛 위치 타일 선택

**현상**: 전방 타일이 모두 가득 찬 상황에서 우회 타일을 탐색할 때, 유닛이 현재 위치한 타일 자체가 우회 목적지로 선택되어 이동 거리가 0인 상태가 발생한다.

**파일**: `Application/Services/TileOccupancyManager.cs`  
**메서드**: `FindForwardAvailable()` (라인 287~311) + `BfsFindAvailable()` (라인 211~261)

**문제 구조**:
- `FindForwardAvailable(preferred, ...)` 호출 시 `preferred`는 **다음 목표 타일**
- BFS 탐색 시 `visited = { preferred }` 로 시작 — preferred 타일만 제외
- **유닛이 현재 위치한 타일**(`prevActualTile`)은 전달되지 않아 BFS 후보에서 제외되지 않음
- 결과: 우회 후보로 유닛이 이미 있는 타일이 반환될 수 있음

로그 근거: `TILE_ARRIVED tile=(7,8)` → `DETOUR originalTo=(6,8) detourTo=(7,8)` → `TILE_MOVE from=(7,8) to=(7,8)` → `SLOT_CLAIM tile=(7,8) slot=3`. UnitID:11이 (7,8)에 도착한 직후 같은 타일로 DETOUR 발생.

**호출 위치**: `UnitView.RunTileTraversal()` 내 대기 루프에서 `_movementUseCase.FindForwardAvailable(to, mySize, finalTarget)` 호출. `to`(목표 타일)만 전달하고 유닛의 현재 논리 위치(`prevActualTile`)는 전달하지 않음.

---

## 영향 범위

| 버그 | 영향 파일 | 영향 범위 |
|------|---------|---------|
| BUG-005 | UnitView.cs | Ranged 유닛 전투 진입 분기 |
| BUG-006 | UnitView.cs | ResumeFromForwardTile → RunTileTraversal 재진입 분기 |
| BUG-007 | GameBootstrapper.cs + UnitView.cs | 건물 이벤트 핸들러 + OnPathInvalidated |
| BUG-008 | TileOccupancyManager.cs + UnitMovementUseCase.cs | FindForwardAvailable + 호출부 |

BUG-005와 BUG-006은 동일한 근본 원인(ENEMY_DETECTED 시 슬롯 미해제)에서 발생하므로 같은 수정 지점에서 함께 처리 가능하다.

---

## 수정 방향 힌트 (Plan.md에서 상세화)

- **BUG-005 / BUG-006**: `EnterStationaryCombat` 또는 `EnterMeleePursuit` 진입 직전 공통 경로에서 `ReleaseV2MoveSlotIfClaimed()` 호출 추가. 근접 유닛 분기(라인 2169~2173)와 동일한 패턴으로 원거리 분기에도 적용.
- **BUG-007**: `RepathAllAliveUnits()`에서 공격 슬롯을 보유 중이거나 전투 플래그 활성 유닛을 skip. 또는 `OnPathInvalidated()`에서 전투 상태를 확인하고 조기 반환.
- **BUG-008**: `FindForwardAvailable` 호출 시 유닛 현재 위치(`prevActualTile`)를 추가 인자로 전달하여 BFS 탐색 시 해당 타일을 visited에 미리 등록.
