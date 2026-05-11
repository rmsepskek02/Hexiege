# Plan — 이동 슬롯 유령 점유 & 이중 코루틴 버그 수정

작성일: 2026-05-06  
Research: `_Tasks/2026-05-06/01_19_movement-slot-ghostfix/Research.md`  
Log: `_Logs/2026-04-30/02_29_movement-combat-redesign/Log.md` (Round 2)

---

## 이 작업이 무엇인지

멀티플레이 테스트에서 유닛이 타일 사이 허공에 고정되고, 해당 자리를 다른 유닛이 피하느라 경로가 꼬이는 4가지 버그를 수정한다.

모든 버그의 공통 원인은 "이동 슬롯(타일 위 자리)이 점유된 채로 전투에 진입하거나, 전투 중인 유닛에게 이동 명령이 잘못 내려지는 것"이다. 코드 수정 범위는 3개 파일이며, 모두 기존 로직의 누락된 한두 줄을 채우는 방식으로 수정한다.

---

## 수정 대상 파일 목록

| 파일 | 수정 이유 |
|------|---------|
| `Presentation/Unit/UnitView.cs` | BUG-005, BUG-006 — 전투 진입 시 슬롯 해제 누락 |
| `Bootstrap/GameBootstrapper.cs` | BUG-007 — 전투 중 유닛에게 경로 재계산 전달 차단 |
| `Application/Services/TileOccupancyManager.cs` | BUG-008 — 우회 탐색 시 현재 유닛 위치 타일 제외 |
| `Application/UseCases/UnitMovementUseCase.cs` | BUG-008 — FindForwardAvailable 인터페이스 변경 전달 |

---

## Step 1 — BUG-005 / BUG-006: 전투 진입 직전 이동 슬롯 해제

### 문제 위치
`UnitView.RunTileTraversal()` 내 원거리(Ranged) 전투 진입 분기 (라인 2107~2149)

### 수정 내용
원거리 전투 진입 분기에서 `EnterStationaryCombat()` 호출 **직전**에 `ReleaseV2MoveSlotIfClaimed()` 를 호출한다.

근접 유닛 분기(라인 2169~2173)에는 이미 해당 호출이 존재하므로, 원거리 분기에 동일한 패턴을 추가하면 된다.

```
// 수정 전 (원거리 분기)
yield return EnterStationaryCombat();

// 수정 후 (원거리 분기)
ReleaseV2MoveSlotIfClaimed();   ← 추가
yield return EnterStationaryCombat();
```

### BUG-006 처리 여부
BUG-006은 `ResumeFromForwardTile` 이후 `RunTileTraversal` 재진입 시 같은 원거리 분기를 타는 경우다. Step 1의 수정이 재진입 경로에도 동일하게 적용되므로 별도 수정 없이 함께 해결된다.

### 위험 요소
- `ReleaseV2MoveSlotIfClaimed()`가 이미 슬롯이 없는 상태에서 호출되어도 내부적으로 null 체크 후 무시하는지 확인 필요. (기존 근접 분기에서 이미 같은 방식으로 사용 중이므로 안전할 가능성이 높음)

---

## Step 2 — BUG-007: 전투 중 유닛 Repath 차단

### 문제 위치
두 곳 중 하나를 선택:
- `GameBootstrapper.RepathAllAliveUnits()` (라인 705) — 호출 측 필터
- `UnitView.OnPathInvalidated()` (라인 728) — 수신 측 필터

### 수정 방향 결정
**`OnPathInvalidated()` 수신 측 필터 방식 채택**

이유: `RepathAllAliveUnits()`는 향후 다른 컨텍스트에서도 호출될 수 있으므로, 방어 로직은 UnitView 자신이 갖는 것이 더 견고하다.

### 수정 내용
`OnPathInvalidated()` 상단 가드 조건에 전투 상태 확인 추가.

```
// 수정 전
public void OnPathInvalidated()
{
    if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
    if (!_hasDestination) return;
    if (_unitData == null || !_unitData.IsAlive) return;
    if (_movementUseCase == null) return;
    // 바로 새 경로 계산...

// 수정 후
public void OnPathInvalidated()
{
    if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
    if (!_hasDestination) return;
    if (_unitData == null || !_unitData.IsAlive) return;
    if (_movementUseCase == null) return;
    if (IsInCombat()) return;   ← 추가 (전투 중이면 repath 무시)
    // 이후 새 경로 계산...
```

`IsInCombat()` 판단 기준: 공격 슬롯을 보유 중(`_attackSlotTile.HasValue` 또는 `_hasAttackSlot` 등 기존 필드)이거나 전투 모드 플래그가 활성인 경우. 실제 필드명은 game-programmer가 코드 확인 후 결정.

### 위험 요소
- 전투 종료 후 정상적으로 Repath가 필요한 시점에 이 가드가 방해하지 않는지 확인 필요. 전투 종료 시 플래그가 해제된 후 `ResumeFromForwardTile`이 호출되므로 타이밍상 문제없을 것으로 예상되나, 코드 확인 필요.

---

## Step 3 — BUG-008: DETOUR 탐색 시 현재 유닛 위치 타일 제외

### 문제 위치
`TileOccupancyManager.FindForwardAvailable()` (라인 287~311) + `BfsFindAvailable()` (라인 211~261)

### 수정 내용

**3-1. `TileOccupancyManager.FindForwardAvailable()` 시그니처 변경**

현재 유닛 위치 타일(`currentTile`)을 파라미터로 추가하고, BFS 탐색 시 해당 타일을 `visited`에 미리 등록하여 우회 후보에서 제외한다.

```
// 수정 전
public HexCoord? FindForwardAvailable(HexCoord preferred, float unitSize, HexGrid grid, HexCoord destination)

// 수정 후
public HexCoord? FindForwardAvailable(HexCoord preferred, float unitSize, HexGrid grid, HexCoord destination, HexCoord currentTile)
```

`BfsFindAvailable()` 내부:
```
// 수정 전
var visited = new HashSet<HexCoord> { preferred };

// 수정 후
var visited = new HashSet<HexCoord> { preferred, currentTile };   ← currentTile 추가
```

**3-2. `UnitMovementUseCase.FindForwardAvailable()` 인터페이스 동일하게 변경**

UnitView에서 호출하는 `_movementUseCase.FindForwardAvailable(to, mySize, finalTarget)` 를 `prevActualTile` 을 추가 인자로 전달하도록 변경.

**3-3. 호출부(`UnitView.RunTileTraversal()`) 수정**

대기 루프 내 `FindForwardAvailable` 호출 시 `prevActualTile` 추가 전달:
```
// 수정 전
actualTo = _movementUseCase.FindForwardAvailable(to, mySize, finalTarget);

// 수정 후
actualTo = _movementUseCase.FindForwardAvailable(to, mySize, finalTarget, prevActualTile);
```

### 위험 요소
- `FindForwardAvailable` 시그니처 변경으로 호출부가 여러 곳일 경우 모두 수정 필요. 호출처 전수 검색 후 누락 없이 수정할 것.
- `currentTile`이 `default(HexCoord)`인 경우(초기화 전 등) 예외 처리 여부 확인.

---

## 기존 로직 제거 여부

이번 수정에서 기존 로직을 제거하는 항목은 없다. 모두 기존 코드에 조건문 또는 메서드 호출을 추가하거나 파라미터를 변경하는 방식이다.

---

## 수정 순서

1. **Step 1** (BUG-005/006) — UnitView.cs 원거리 전투 진입 분기에 슬롯 해제 추가
2. **Step 2** (BUG-007) — UnitView.cs OnPathInvalidated 전투 상태 가드 추가
3. **Step 3** (BUG-008) — TileOccupancyManager → UnitMovementUseCase → UnitView 순서로 시그니처 변경 및 호출부 수정

Step 1, 2는 독립적으로 수정 가능. Step 3은 시그니처 변경을 포함하므로 관련 파일을 연속으로 수정.

---

## 예상 위험 요소 종합

| 위험 | 대응 |
|------|------|
| Step 1: `ReleaseV2MoveSlotIfClaimed` null 처리 여부 | 기존 근접 분기 동일 사용 확인으로 검증 |
| Step 2: 전투 상태 플래그 필드명 불일치 | game-programmer가 코드에서 정확한 필드명 확인 |
| Step 2: 전투 종료-Repath 타이밍 문제 | ResumeFromForwardTile 호출 시점 확인 |
| Step 3: 호출부 누락 | `FindForwardAvailable` 전수 grep 후 모두 수정 |
| Step 3: `currentTile` default 값 처리 | 조건 추가 또는 기본값 처리 |
