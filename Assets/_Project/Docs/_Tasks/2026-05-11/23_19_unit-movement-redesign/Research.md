# Research — 유닛 이동/전투 시스템 재설계

## 작업 목적

이동 슬롯과 공격 슬롯 기반의 유닛 분산 시스템을 전면 폐기하고, 단순한 상태 머신 구조로 교체하는 작업이다.

기존 방식은 수십 개의 유닛이 동시에 움직일 때 슬롯 배정 로직이 지나치게 복잡해져 버그가 잦아지는 문제가 있었다.
새 방식은 겹침을 허용하고, 모든 유닛(근접/원거리 구분 없이)이 동일한 3단계 상태 머신을 따르도록 한다:
**A* 이동 → 전투 이동(월드 직선) → 공격**, 이 세 가지만으로 동작한다.

---

## 현재 구현 상태

### 핵심 파일 목록

| 파일 | 클래스 | 역할 |
|------|--------|------|
| `Scripts/Presentation/Unit/UnitView.cs` | UnitView | 유닛 비주얼 이동/상태 처리 (Phase 0/1/2) |
| `Scripts/Application/UseCases/UnitMovementUseCase.cs` | UnitMovementUseCase | 이동 명령 처리, 점유 관리 |
| `Scripts/Application/UseCases/UnitCombatUseCase.cs` | UnitCombatUseCase | 공격/피격/사망, 감지/공격 사거리 판정 |
| `Scripts/Application/Services/TileMoveSlotManager.cs` | TileMoveSlotManager | 이동 슬롯 배정 (1~3번, 삼각형 배치) |
| `Scripts/Application/Services/TileOccupancyManager.cs` | TileOccupancyManager | 타일 점유량 추적 (최대 3.0) |
| `Scripts/Application/Services/AttackPositionManager.cs` | AttackPositionManager | 공격 슬롯 배정 (동적 각도 기반) |
| `Scripts/Domain/Unit/UnitData.cs` | UnitData | 유닛 상태 데이터 |
| `Scripts/Domain/Unit/UnitStats.cs` | UnitStats | 유닛 타입별 스탯 정의 |
| `Scripts/Domain/Hex/HexPathfinder.cs` | HexPathfinder | A* 경로 탐색 |
| `Scripts/Domain/Hex/HexFlowField.cs` | HexFlowField | BFS 플로우 필드 |

### 현재 이동 Phase 구조 (UnitView)

- **Phase 0**: A* 경로를 따라 타일 중심→타일 중심 Lerp 이동
  - 점유 예약 (`RegisterOccupancyMove`) 및 슬롯 배정 (`TileMoveSlotManager.ClaimSlot`) 포함
- **Phase 1**: 근접 유닛 전용 — 감지 사거리 내 적 발견 시 공격 슬롯 위치로 월드 직선 이동
  - `AttackPositionManager`로부터 슬롯 위치를 받아 이동
- **Phase 2**: 전투 종료 후 앞쪽 가장 가까운 타일로 이동 후 A* 재개
  - `FindForwardClosestTile()`로 재개 타일 결정
- **원거리 유닛**: Phase 1 없음. Phase 0 중 공격 사거리 내 적 감지 시 즉시 정지 후 공격

### 현재 타겟 선택 로직 (UnitCombatUseCase)

- `FindFirstEnemyTarget()` — 월드 좌표 거리 기반으로 처음 감지되는 적 반환
- `FindNearestEnemyInDetectRange()` — 감지 사거리 내 가장 가까운 적 반환
- 사거리 판정: `HasEnemyInRange()`, `HasEnemyInDetectRange()`

---

## 제거 대상

| 파일 | 이유 |
|------|------|
| `TileMoveSlotManager.cs` | 이동 슬롯 시스템 전면 폐기 |
| `TileOccupancyManager.cs` | 타일 점유 한도 제거, 겹침 허용 |
| `AttackPositionManager.cs` | 공격 슬롯 시스템 전면 폐기 |

---

## 수정 대상

### UnitView.cs — 이동 Phase 재구성 (가장 큰 변경)

현재 Phase 0/1/2 구조를 새 상태 머신으로 교체한다.

**새 상태:**
1. **A* 이동**: 타일 중심→타일 중심 이동 (Phase 0 유지, 슬롯/점유 코드만 제거)
2. **전투 이동**: 타겟을 향해 월드 직선 이동 (근접/원거리 동일)
3. **공격**: 멈추고 공격

**제거할 코드:**
- 슬롯 관련: `ReleaseV2MoveSlotIfClaimed()`, `ReleaseV2AttackSlotIfClaimed()`, `_v2MoveSlotTile`, `_v2AttackSlotTargetCoord`
- 점유 관련: `RegisterOccupancyMove()`, `ReleaseOccupancy()`, `_pendingOccupancyTile`
- 원거리 전용 Phase 0 정지 로직

**유지/재활용할 코드:**
- Phase 0 Lerp 이동 코드 (슬롯/점유 부분만 제거)
- Phase 2의 `FindForwardClosestTile()` 호출 로직 → 전투 종료 후 A* 재개에 그대로 사용

### UnitMovementUseCase.cs — 점유 관련 코드 제거

**제거:**
- `RegisterOccupancyMove()`, `ReleaseOccupancy()`, `FindForwardAvailable()`
- `TileOccupancyManager` 의존성

**유지:**
- `RequestMove()` — A* 경로 요청
- `ProcessStep()` — 타일 도착 처리 (Domain Position 갱신)
- `FindForwardClosestTile()` — 전투 종료 후 앞쪽 타일 탐색

### UnitCombatUseCase.cs — 원거리 유닛 로직 통합

현재 원거리 유닛은 Phase 0 중 정지 후 공격하는 별도 분기가 있다.
이 분기를 제거하고 근접과 동일한 흐름(전투 이동 → 공격)으로 통합한다.

**유지:**
- `HasEnemyInDetectRange()`, `HasEnemyInRange()` — 사거리 판정
- `FindNearestEnemyInDetectRange()` — 전투 종료 후 다음 타겟 탐색
- `TryAttack()`, `ApplyAttackDamage()` — 공격/데미지 처리

### UnitData.cs — 슬롯 관련 필드 제거

- `ClaimedTile` 제거 (이동 슬롯 추적 용도)
- 점유 관련 필드가 있다면 함께 제거

### UnitStats.cs — OccupancySize 제거

- `StatValues` 구조체에서 `OccupancySize` 필드 제거
- `GetOccupancySize()` 메서드 제거

### GameBootstrapper — 제거된 서비스 의존성 정리

- `TileMoveSlotManager`, `TileOccupancyManager`, `AttackPositionManager` 생성/주입 코드 제거

---

## 영향 범위

- 이동/전투에 직접 영향: `UnitView`, `UnitMovementUseCase`, `UnitCombatUseCase`
- 데이터 정리: `UnitData`, `UnitStats`
- 부트스트랩 정리: `GameBootstrapper`
- 멀티플레이 컨트롤러 확인 필요: `NetworkCombatController` 등 서버 측 유닛 처리 코드

---

## 주요 주의사항

1. **`FindForwardClosestTile()` 재활용**: 이미 구현된 로직으로 전투 종료 후 A* 재개에 그대로 사용 가능하다. 재구현하지 않는다.

2. **원거리 유닛 감지/공격 사거리 수치 재검토**: 현재 원거리 유닛은 공격/감지 사거리가 동일하게 설정되어 있다 (`AttackRange × HexMetrics.TileHeight`). 새 규칙에서는 감지 사거리 > 공격 사거리여야 하므로, Inspector 또는 UnitStats에서 수치 분리가 필요하다.

3. **Phase 명칭 정리**: 코드 내 Phase 0/1/2 명칭은 새 상태 이름(A*이동/전투이동/공격)으로 변경한다.
