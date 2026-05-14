# 유닛 이동/전투 시스템 재설계 — Research

작성일: 2026-04-30
관련 규칙 문서: `Assets/_Project/Docs/GameSystemRules.md`

---

## 1. 이 작업이 무엇을 왜 하는가 (자연어 설명)

지금까지 우리 게임의 유닛은 다음 세 가지 단계를 오가며 움직이고 싸웠습니다.

1) **타일을 따라 성으로 이동하는 단계 (Phase 0)**
2) **적을 발견하면 직선으로 달려가 공격하는 단계 (Phase 1)**
3) **싸움이 끝나면 가장 가까운 타일 위치로 다시 정렬하는 단계 (Phase 2)**

이 흐름 자체는 좋았지만, 실제 플레이를 해보면 다음과 같은 이상한 현상들이 자꾸 나타났습니다.

- 적이 죽으면 유닛이 갑자기 뒤로 슝 하고 끌려갔다가 다시 앞으로 나오는 "뒷무빙"
- 한 타일에 여러 유닛이 같은 점에 뭉쳐 한 마리처럼 보이는 "뭉침"
- 좁은 길에서 유닛이 멈춰서 가지 못하는 "교착"
- 적을 보고 추적하는 도중 다른 적과 맞물리면서 진동하는 "팅김"
- 감지 사거리 밖에 있는 유닛까지 덩달아 뒤로 돌아가는 "전체 후퇴" 같은 부작용

이 현상들이 보일 때마다 한 가지씩 패치를 추가했습니다. 그러다 보니 패치가 패치를 부르고, 한 부분을 고치면 다른 부분이 망가지는 일이 반복됐습니다. 결국 "이 코드가 왜 이렇게 동작해야 하는지"보다 "이렇게 안 하면 다른 버그가 터진다"는 이유로 굳어진 조건들이 코드 전반에 누적됐습니다.

이번 작업의 목표는 그 누적된 패치를 걷어내고, **명확한 새 규칙(GameSystemRules.md)을 그대로 코드로 옮긴 깨끗한 버전**으로 다시 만드는 것입니다. 새 규칙의 핵심 아이디어는 두 가지입니다.

- **이동 슬롯**: 한 타일에는 미리 정해진 3개의 자리(앞 / 뒤좌 / 뒤우)가 있고, 유닛은 자기에게 배정된 자리(슬롯)의 월드 좌표로만 이동합니다. 같은 타일에 여러 유닛이 모여도 각자 다른 자리에 정확히 정렬됩니다.
- **공격 슬롯**: 적을 중심으로 12방향의 자리가 미리 정의돼 있고, 같은 적을 공격하는 여러 유닛은 그 12자리 중 비어있는 곳을 받아 분산됩니다. 한 점에 뭉치지 않습니다.

또한 새 규칙은 **근접/원거리 유닛의 행동을 명확히 분리**합니다.
- 근접 유닛은 적을 감지하면 이동 슬롯을 풀고 공격 슬롯으로 달려갑니다.
- 원거리 유닛은 자리를 안 옮기고 자기 이동 슬롯에 그대로 멈춰서 공격합니다.

이 작업에서는 코드를 작성하지 않고, 위 규칙을 어떻게 구현할지에 대한 조사 문서(Research.md)와 계획 문서(Plan.md)만 작성합니다.

---

## 2. 현재 구현 상태 분석 (Phase 0/1/2 동작)

### 2-1. 진입점

- `UnitFactory.CreateUnitObject()` (`Infrastructure/Factories/UnitFactory.cs`)에서 유닛 GameObject를 생성하고 `UnitView.Initialize()` + `SetDependencies()`를 호출합니다.
- 이동 명령은 `UnitMovementUseCase.RequestMove()`로 시작되고, 반환된 경로 리스트가 `UnitView.MoveTo(path)`로 전달됩니다.
- `UnitView.MoveAlongPath(path)` 코루틴이 모든 이동/전투 흐름을 단일 코루틴으로 관장합니다 (현재 `UnitView.cs` 1765라인 중 약 600라인 차지).

### 2-2. 외부 while 루프

`MoveAlongPath()` 내부의 외부 `while (_unitData.IsAlive)` 루프가 다음 세 단계를 반복 전환합니다.

1. **Phase 0 (타일 기반 A* 이동)** — `for (int i = 1; i < path.Count; i++)`로 path를 순회.
2. **Phase 1 (월드 좌표 직선 추적)** — `if (initialEnemyForward) { while (_unitData.IsAlive) { ... } }` 블록.
3. **Phase 2 (가장 가까운 타일로 스냅)** — `{ ... }` 블록 안에서 가장 가까운 타일을 찾아 Lerp 후 A* 재계산.

### 2-3. Phase 0의 현재 동작

각 타일 스텝마다 다음 처리를 수행합니다.

- `FindAvailableTile(to, mySize, finalTarget)` — 다음 타일이 점유 한도(MaxOccupancy=3)를 넘었으면 BFS로 빈 타일을 찾아 우회. forward 필터로 후방 우회를 차단.
- `RegisterOccupancyMove(from, to, type)` — Lerp 시작 직전 TO+1만 예약 (FROM-1은 ProcessStep에서 수행).
- `ApplyDirection(dir)` — Y축 회전 즉시 스냅.
- 슬롯 매니저 — `TileSlotManager.ClaimSlot(to, unitId)`로 slot offset을 받아 toPos에 더함.
- Lerp `while (elapsed < moveSeconds)` — 매 프레임 적 감지 체크 + 전투 사거리 체크.
  - 전투 사거리 진입 시: `OnUnitEnteredCombat` 발행 → `while (HasEnemyInRange)` + `while (AttackCooldownRemaining > 0)` → 재진입 또는 이동 재개.
  - 감지 사거리 진입 시(forward filter 통과): `interruptedByDetect = true; break;`
- Lerp 완료 후: `transform.position = toPos`, `ProcessStep(unit, from, to)`, `prevActualTile = to`, `_pendingOccupancyTile = default`.
- 우회 발생 시 `detouredNeedsRepath = true; break;` → 외부 while에서 `RequestMove`로 새 경로.
- 스텝 완료 후 감지 사거리 진입 시(forward filter 통과): `shouldPursue = true; break;`

### 2-4. Phase 1의 현재 동작

- `FindNearestEnemyInDetectRange()` + forward filter 통과 시 진입.
- `_attackPositionManager.ClaimAttackSlot(targetCoord, unitId, viewPos, contactRadius)` — 12방향 angular 슬롯에서 1자리 배정.
- `while (_unitData.IsAlive)` 루프:
  - 매 프레임 enemy 위치 조회 (`GetUnitWorldPosition` 등).
  - 타겟 사망(zero 반환) 시: `HasEnemyInDetectRange` + forward filter → 새 타겟 재선택 또는 break.
  - `HasEnemyInRange` 진입 시: 전투 루프 (Phase 0과 동일 구조), 종료 후 새 타겟 재선택 또는 break.
  - 감지 사거리 이탈 시: break.
  - 슬롯 도달 판단 (`Vector2.Distance(transform.position.xz, _currentAttackPos.xz) < 0.15f`) → `_currentAttackPos = Vector3.zero` → 이후 `enemyViewPos`로 직접 추적.
  - moveTarget으로 `worldSpeed * Time.deltaTime`만큼 이동 + RotateTowards로 회전.

### 2-5. Phase 2의 현재 동작

- `domainPos = ViewConverter.FromView(transform.position)` → `nearestTile = HexMetrics.WorldToHex(domainPos)`.
- `nearestTile == _unitData.Position`이면 6방향 인접 forward 타일 중 현재 위치 기준 가장 가까운 타일로 교체 (7차 개선 Step 4-A).
- `_slotManager.ClaimSlot(nearestTile, unitId)`로 다시 슬롯 점유.
- `tileCenter`까지 Lerp (worldSpeed 기준), Lerp 도중 forward 적 감지 시 break (7차 개선 Step 4-B).
- `transform.position = tileCenter` 강제 스냅.
- `nearestTile != _unitData.Position`이면 `RegisterOccupancyMove`로 점유 이동.
- `ProcessStep(unit, _unitData.Position, nearestTile)` 도메인 좌표 동기화.
- `RequestMove(unit, finalTarget)`로 A* 재계산 후 외부 while continue.

### 2-6. 누적된 보조 시스템

- **TileOccupancyManager** (`Application/Services/TileOccupancyManager.cs`) — 타일별 점유량 합계 추적, MaxOccupancy=3, BFS 우회 + forward 필터.
- **TileSlotManager** (`Application/Services/TileSlotManager.cs`) — 6슬롯 (중심 + 좌/우 + 앞좌/앞우 + 뒤). 7번째부터 슬롯 0으로 fallback.
- **AttackPositionManager** (`Application/Services/AttackPositionManager.cs`) — 12방향 angular 슬롯, MaxUnitsPerSlot=2.
- **FlowFieldService** (`Application/Services/FlowFieldService.cs`) — 목적지별 BFS 캐시. 같은 타깃을 향한 모든 유닛이 공유.
- **TileOwnershipService** (`Application/Services/TileOwnershipService.cs`) — 매 프레임 유닛 물리 위치로 타일 점령 갱신.

---

## 3. 새 규칙(GameSystemRules.md)과 현재 구현의 차이점

### 3-1. 이동 슬롯 (규칙 17, 10)

| 항목 | 현재 구현 | 새 규칙 |
|------|----------|--------|
| 슬롯 개수 | 6 (중심+좌+우+앞좌+앞우+뒤) | **3 (앞 / 뒤좌 / 뒤우)** |
| 슬롯 도착 판정 | 타일 중심 도착 = 도착, 슬롯은 시각 오프셋 | **슬롯 위치(월드) 도달 = 타일 도착** |
| 슬롯 배정 기준 | 슬롯 0부터 빈 번호 선형 탐색 | **현재 위치에서 가장 가까운 빈 슬롯, 거리 동일 시 1→2→3 우선순위** |
| 점유치 2 처리 | OccupancySize 누적, 슬롯과 무관 | **슬롯 1개 차지, 점유 카운트 2 소비** |
| 슬롯 결정 시점 | Lerp 시작 직전 ClaimSlot | **다음 타일 결정 즉시 슬롯 할당** |

### 3-2. 점유 한도 (규칙 6, 5)

| 항목 | 현재 구현 | 새 규칙 |
|------|----------|--------|
| 한도 | MaxOccupancy = 3 (소형1/중형2/대형3 누적) | **소형1/대형2, 타일 최대 3** (사실상 동일) |
| 가득 시 우회 | BFS + forward 필터 + +1 여유 fallback | **앞쪽 빈 타일로 우회, 없으면 대기 (뒤로 우회 절대 금지)** |
| FROM 해제 타이밍 | TO+1은 Lerp 시작 직전, FROM-1은 ProcessStep | (새 규칙은 명시 없음 → 분리 패턴 유지 가능) |

### 3-3. 공격 슬롯 (규칙 11, 18)

| 항목 | 현재 구현 | 새 규칙 |
|------|----------|--------|
| 슬롯 개수 | 12방향 × MaxUnitsPerSlot=2 | **12방향 (30° 간격)** |
| 슬롯 위치 | 타겟 도메인 좌표 + 방향 × contactRadius | (동일) |
| 배정 기준 | 가장 적게 배정된 위치 → 가까운 거리 | **접근 방향과 가장 가까운 각도의 빈 슬롯** |
| 해제 시점 | 사망/타겟 변경/이탈 등 모든 종료 경로 | (동일) |
| Phase와의 관계 | Phase 1 진입 시 단방향 슬롯 → enemyViewPos 전환 | **슬롯 도달 = 전투 시작, 슬롯에서 직선 이동만** |

### 3-4. 전투 진입 (규칙 13)

| 유닛 타입 | 현재 구현 | 새 규칙 |
|---------|----------|--------|
| 근접 | 감지 사거리 → Phase 1 진입 → 공격 슬롯 → 슬롯 도달 시 enemyViewPos로 전환 | **감지 사거리 → 이동 슬롯 해제 → 공격 슬롯으로 직선 이동 → 도달 시 공격** (전환 단계 없음) |
| 원거리 | DetectRange == AttackRange 가정으로 Phase 1 우회, 전투 시작 | **공격 사거리 → 이동 슬롯 유지하며 그 자리에서 즉시 공격** (명시적) |
| 공격 중 이동 | 위치 고정 (yield return null) | **모든 유닛 공격 중 이동 금지** (동일) |

### 3-5. 타겟 선택 / 종료 후 재개 (규칙 14, 15)

| 항목 | 현재 구현 | 새 규칙 |
|------|----------|--------|
| 첫 타겟 | FindNearestEnemyInDetectRange | **처음 감지된 적** |
| 다음 타겟 | FindNearestEnemyInDetectRange + forward filter | **사거리 내 가장 가까운 적** (forward filter는 새 규칙에 없음) |
| 근접 종료 후 | Phase 2: 가장 가까운 타일로 스냅 + 인접 forward 보정 | **현재 월드 위치 기준 앞쪽에서 가장 가까운 타일에서 A* 재개** |
| 원거리 종료 후 | 동일 Phase 2 (이동 슬롯 미유지) | **이동 슬롯 유지 → 그대로 A* 재개 (Phase 2 불필요)** |

### 3-6. 경로 재계산 (규칙 4)

| 항목 | 현재 구현 | 새 규칙 |
|------|----------|--------|
| 평상 시 | 타일 도착마다 path 그대로 사용 (RequestMove는 Phase 2/우회 시) | **타일 도착마다 공유 타일 상태로 다음 타일 결정** |
| 건물 변경 시 | FlowFieldService.InvalidateAll로 다음 RequestMove에서 재계산 (lazy) | **즉시 모든 유닛 경로 재계산** (eager) |
| 점유 가득 처리 | BFS 우회 | **앞쪽 빈 타일 우회 또는 대기, 뒤로 우회 금지** |

### 3-7. 사망 시 처리 (규칙 12)

| 항목 | 현재 구현 | 새 규칙 |
|------|----------|--------|
| 이동 슬롯 | OnEntityDied 구독 → ReleaseSlotIfClaimed | (동일) |
| 공격 슬롯 | 사망 핸들러에서 ReleaseAttackSlotIfClaimed | (동일) |
| 점유 카운트 | UnitMovementUseCase가 OnEntityDied 구독 → OnUnitRemoved | (동일) |
| pendingOccupancy | ReleaseOccupancyIfPending로 명시 해제 | (새 규칙에 없음, 내부 구현 디테일) |

---

## 4. 유지할 코드 vs 교체/제거할 코드

### 4-1. 유지 (그대로 또는 소폭 수정)

- **`UnitMovementUseCase.RequestMove()` 전체** — 플로우 필드 기반 경로 반환은 새 규칙의 "A*로 성을 향해 이동" 그대로 부합.
- **`FlowFieldService` 전체** — 같은 목적지 공유 캐시. 새 규칙의 "공유 타일 상태"와 호환됨. 단, walkable 변경 시 즉시 모든 유닛 재계산하려면 lazy → eager 전환을 검토해야 함.
- **`TileOccupancyManager`의 점유 합계 + CanFit + OnUnitMoved 골격** — 점유 한도는 새 규칙도 동일.
- **`TileOccupancyManager.FindAvailableTile`의 forward 필터 + +1 여유** — 새 규칙의 "뒤로 우회 금지"와 정확히 일치. 다만 fallback BFS(필터 OFF 재시도)는 "없으면 대기"로 바뀌어야 함.
- **`AttackPositionManager`의 12방향 angular 슬롯 골격 + ClaimAttackSlot/ReleaseAttackSlot API** — 새 규칙 18과 부합. 다만 배정 우선순위가 "최소 점유 → 가까운 거리"에서 "접근 방향 각도 가까운 빈 슬롯 → 순차 측면/뒤"로 바뀌어야 함. MaxUnitsPerSlot 정책도 단일 점유로 단순화 가능.
- **`UnitView.MoveTo(path)` 진입 + 사망 이벤트 구독** — 외부 인터페이스는 그대로.
- **`UnitView.StartCombatAnimation / ChangeTarget / StopCombatAnimation`** — 애니메이션 트리거 자체는 유지.
- **`UnitView.Update()`의 전투 중 회전 추적** — `_combatTargetTransform` 기반 RotateTowards.
- **`TileOwnershipService`** — 새 규칙과 무관, 점령 시스템.
- **`UnitFactory` 전체** — 의존성 주입 구조 유지.
- **`GameBootstrapper.CreateUseCases()`의 UseCase 생성/연결** — 신규 시스템도 같은 패턴으로 추가.
- **DirectionAngles, ApplyDirection, CalculateAttackAngle, ViewConverter, NetworkContext 가드** — 모두 그대로.

### 4-2. 교체 (구조 자체를 다시 설계)

- **`UnitView.MoveAlongPath()` 코루틴 전체 (약 1000라인)** — Phase 0/1/2 단일 거대 코루틴은 누적 패치의 산실. 새 규칙에 맞춰 전면 재작성 필요. 다만 **기존 코드는 제거가 아니라 비활성화(주석 처리)** 처리하여 검증 단계에서 비교 가능하도록 둠.
- **`TileSlotManager` 전체** — 6슬롯 → 3슬롯(앞/뒤좌/뒤우) + "거리 가까운 → 번호 우선" 배정 + "슬롯 위치가 곧 이동 목표" 의미로 재설계. **타일 중심에서의 시각 오프셋 매니저 → 타일 내 이동 목표 매니저**로 의미가 변함. 기존 클래스명을 유지하더라도 내부 구현은 전면 교체.
- **공격 슬롯 도달 후 enemyViewPos 전환 로직 (UnitView 1379~1397)** — 새 규칙에서는 **슬롯 도달 = 전투 시작**이므로 2단계 전환 자체가 사라짐. 슬롯 위치를 정확히 전투 사거리에 둔다는 전제가 그대로 유지된다면 이 분기 자체가 필요 없음.
- **forward filter 분기들 (UnitView 6차 개선, 5곳)** — 새 규칙은 "타겟 처리 후 뒤로 안 돌아간다"를 **Phase 2의 "현재 월드 위치 기준 앞쪽 가장 가까운 타일에서 A* 재개"**로 단일 구현. forward filter를 코드 곳곳에서 검사하던 패턴을 한 곳으로 모음.
- **Phase 2 nearestTile 인접 forward 보정 (UnitView 7차 개선 Step 4-A, 1453~1481)** — 위와 같은 이유로 단일 구현으로 흡수.
- **`UnitView.MoveAlongPath` 내 detouredNeedsRepath 흐름** — 새 규칙은 "다음 타일 결정 시 점유 가득 → 앞쪽 빈 타일 우회 또는 대기"를 매 스텝 적용. 우회 후 path 폐기 + RequestMove 재호출 패턴은 자연스러운 일부가 되어 별도 변수 없이 표현 가능.

### 4-3. 제거 또는 비활성화 (안전 검토 후)

- **Phase 1 슬롯 도달 후 enemyViewPos 직선 추적 분기** — 새 규칙은 슬롯 도달 = 전투. 이 코드 경로 자체가 의미를 잃음.
- **Phase 2의 nearestTile == _unitData.Position 분기 (인접 forward 후보 탐색)** — 새 규칙은 "현재 월드 위치 기준 앞쪽 가장 가까운 타일"을 단일 헬퍼로 표현. 같은 타일 분기 자체가 필요 없음.
- **5차 개선 점유 누수 방지 분기 (`nearestTile == _unitData.Position`이면 RegisterOccupancyMove 생략)** — 위 분기 제거 시 동시에 사라짐.
- **`UnitView._pendingOccupancyTile` 추적 + ReleaseOccupancyIfPending** — 새 구조에서는 "다음 타일 결정 = 슬롯 할당 = 점유 예약"이 한 시점에 묶이므로, "도착 못 함" 경로의 해제는 슬롯 해제 헬퍼에 통합 가능. 다만 안전을 위해 비활성화 유지를 우선.

### 4-4. 신규 도입 필요

- **이동 슬롯의 새 정의 (3슬롯)** — TileSlotManager 또는 별도의 `TileMoveSlotManager` 신설. 슬롯 위치(월드 좌표) + 슬롯 점유 상태 + 점유치 2 처리.
- **공격 슬롯 배정 규칙 변경** — `AttackPositionManager.ClaimAttackSlot`을 "접근 방향 각도 가까운 빈 슬롯"으로 갱신.
- **유닛 타입 분기 (근접 / 원거리)** — 현재 `DetectRange == AttackRange`로 묵시적 처리되던 원거리 분기를 **명시적 enum 또는 플래그**로 노출. 새 규칙 13/15가 이 구분을 전제.
- **건물 변경 시 즉시 재계산 트리거** — 현재 lazy 무효화 → 새 규칙은 "즉시 모든 유닛 경로 재계산". 살아있는 유닛 전체에 RequestMove를 다시 호출하는 헬퍼 필요.

---

## 5. 영향 범위 (관련 파일)

| 레이어 | 파일 | 영향도 |
|--------|------|--------|
| Presentation | `Presentation/Unit/UnitView.cs` | 매우 큼 — MoveAlongPath 전면 재작성 |
| Application/Services | `TileSlotManager.cs` | 매우 큼 — 6슬롯 → 3슬롯, 의미 재정의 |
| Application/Services | `AttackPositionManager.cs` | 큼 — 배정 우선순위/점유 정책 변경 |
| Application/Services | `TileOccupancyManager.cs` | 중간 — fallback BFS → "대기"로 변경 |
| Application/Services | `FlowFieldService.cs` | 작음~중간 — eager 재계산 트리거 추가 |
| Application/UseCases | `UnitMovementUseCase.cs` | 중간 — RegisterOccupancyMove/ReleaseOccupancy 시그니처 검토, 건물 변경 시 전 유닛 RequestMove 헬퍼 |
| Application/UseCases | `UnitCombatUseCase.cs` | 작음 — 인터페이스 유지, 호출 패턴은 UnitView에서 단순화 |
| Domain | `Domain/Unit/UnitStats.cs`, `UnitData.cs` | 작음 — 근접/원거리 분기 명시화 (ScriptableObject 추가 필드) |
| Bootstrap | `Bootstrap/GameBootstrapper.cs` | 작음 — 신규 매니저 와이어링 |
| Infrastructure | `Infrastructure/Factories/UnitFactory.cs` | 작음 — 신규 의존성 주입 시그니처 |
| Infrastructure | `Infrastructure/Network/Network*Controller.cs` | 검토 필요 — 새 흐름이 RPC 송수신 패턴에 영향 가능 |

---

## 6. 발견된 부가 이슈

1. **근접/원거리 구분이 명시적 데이터로 없음**
   현재 `UnitStats`는 `AttackRange`와 `DetectRange`를 따로 갖고, 코드 곳곳에서 "DetectRange가 AttackRange와 같으면 원거리(또는 즉시 공격)"라는 묵시적 가정으로 분기를 만들어왔습니다. 새 규칙 13은 둘을 명시적으로 분리해야 하므로, `UnitStats`에 `IsMelee` 같은 플래그(또는 enum) 추가가 자연스럽습니다.

2. **`HexCoord.Distance`와 월드 거리 비교의 혼재**
   forward filter는 `HexCoord.Distance` 기반(정수 거리), 이동/추적 판정은 월드 거리(float). 새 규칙 8은 "전투 사거리 판정은 월드 좌표 기준"이라고 명시했지만, "앞쪽 타일 판정"은 어떤 기준이 되어야 하는지 명시되지 않음. 새 설계에서는 `HexCoord.Distance`로 일관 적용을 권장(부동소수점 오차/팀별 ViewConverter 영향 없음).

3. **`HexCoord` default = (0,0)이 일반 타일과 충돌하는 문제**
   `TileOccupancyManager.IsInvalid`가 `(0,0)`을 무효값으로 사용하지만 (0,0)은 일반 타일일 수 있음. 새 설계에서 점유/슬롯 추적 변수에 `HexCoord?` (Nullable struct)를 일관 사용하는 편이 안전.

4. **`MoveAlongPath`가 단일 코루틴 1000라인**
   유지보수성 관점에서 Phase 0 / Phase 1 / Phase 2를 별도 메서드(코루틴)로 분리하는 것이 강력히 권장됨. 새 설계는 자연스러운 분리 기회.

5. **싱글플레이/멀티플레이 분기가 코루틴 곳곳에 흩어져 있음**
   네트워크 가드 패턴(`NetworkContext.IsNetworkActive`, `IsNetworkServer`)이 거의 모든 분기점에 반복 등장. 헬퍼 메서드로 모으면 가독성 향상 가능. 새 설계에서 검토.

6. **TileOwnershipService와 이동 슬롯의 관계**
   유닛 물리 위치(슬롯 위치) 기준으로 점령이 갱신되므로, 이동 슬롯이 타일 중심에서 어느 정도 떨어지는지(슬롯 좌표 정확도)가 점령 판정에 영향을 줄 수 있음. 슬롯 좌표는 항상 타일 중심에서 충분히 안쪽이어야 함. 새 슬롯 정의 시 거리 기준 검토.

7. **`MaxUnitsPerSlot`과 새 규칙의 단일 점유 정책**
   새 규칙 17은 "한 슬롯에 한 유닛"을 전제하는 표현인데, 현재 AttackPositionManager는 슬롯당 2명까지 허용. 24명 동시 공격을 처리하려면 슬롯이 모자라므로 정책 결정 필요(슬롯 24개로 늘릴지 / 단일점유 + fallback / MaxUnitsPerSlot 유지). 사용자 확인 필요.

8. **`AttackPositionManager.ClaimAttackSlot`의 동점 비교 시 unitViewPos 사용**
   배정 기준이 "가장 적게 점유된 위치 → 가까운 거리"인데 새 규칙은 "접근 방향 각도 가까운 빈 슬롯". 두 기준은 결과가 다름. 새 규칙이 우선이지만, 접근 방향(각도)을 어떻게 계산할지(이동 벡터? 현재 위치 → 타겟 벡터?) 명시 필요.

---

## 7. 결론

기존 Phase 0/1/2 흐름의 골격은 유지할 수 있으나, **이동 슬롯 정의(6→3) / 공격 슬롯 배정 규칙 / 슬롯 도달의 의미(시각 오프셋 → 이동 목표) / forward filter 통합 / 근접·원거리 명시 분기**가 모두 변경됩니다.

`UnitView.MoveAlongPath` 코루틴은 거의 새로 쓰는 수준이고, `TileSlotManager`와 `AttackPositionManager`는 핵심 인터페이스만 유지한 채 내부 구현이 교체됩니다. 기존 코드는 **제거 대신 주석 처리**로 비활성화하여 비교 검증 단계에서 참조 가능하도록 합니다.

다음 단계인 Plan.md에서 단계별 작업 목록과 예상 위험을 구체화합니다.
