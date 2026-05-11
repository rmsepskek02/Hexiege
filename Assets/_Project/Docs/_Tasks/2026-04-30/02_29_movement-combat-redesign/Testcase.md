# 테스트케이스 — 유닛 이동/전투 시스템 재설계 (MoveAlongPathV2)

작성일: 2026-04-30
기준 문서: `GameSystemRules.md` (이동 규칙 1~18)

---

## 테스트 목록

| TC-ID | 제목 | 구분 | 판정 |
|-------|------|------|------|
| SINGLE-01 | 기본 A* 이동 — 슬롯 분산 | 싱글 | PASS |
| SINGLE-02 | 점유 가득 찬 타일 앞쪽 우회 | 싱글 | |
| SINGLE-03 | 앞쪽 빈 타일 없을 때 대기 후 재개 | 싱글 | |
| SINGLE-04 | 뒤로 우회하지 않음 확인 | 싱글 | |
| SINGLE-05 | 근접 유닛 — 감지 사거리 내 적 진입 시 직선 추적 전환 | 싱글 | |
| SINGLE-06 | 원거리 유닛 — 공격 사거리 내 적 진입 시 정지 + 공격 | 싱글 | |
| SINGLE-07 | 타겟 처치 후 가장 가까운 적으로 타겟 변경 | 싱글 | |
| SINGLE-08 | 타겟 이탈 (감지 사거리 밖) 후 A* 재개 | 싱글 | |
| SINGLE-09 | 전투 종료 후 앞쪽 타일에서 A* 재개 (뒷무빙 없음) | 싱글 | PASS |
| SINGLE-10 | 근접 유닛 여러 마리 — 같은 적 공격 시 분산 배치 | 싱글 | |
| SINGLE-11 | 유닛 사망 시 이동/공격 슬롯 즉시 해제 | 싱글 | |
| SINGLE-12 | 건물 건설 시 모든 유닛 경로 즉시 재계산 | 싱글 | |
| SINGLE-13 | 건물 파괴 시 모든 유닛 경로 즉시 재계산 | 싱글 | |
| SINGLE-14 | 같은 타일 이동 슬롯 3슬롯(앞/뒤좌/뒤우) 분산 확인 | 싱글 | PASS |
| SINGLE-15 | 점유치 2 유닛 — 슬롯 1개 + 점유 카운트 2 처리 | 싱글 | |
| MULTI-01 | 멀티플레이 양팀 이동/전투 동일 동작 | 멀티 | FAIL |
| MULTI-02 | Lerp 진행 중 전투 진입 시 이동 슬롯 해제 및 위치 스냅 | 멀티 | FAIL |
| MULTI-03 | 전투 재개 직후 적 감지 시 슬롯 미점유 전투 진입 | 멀티 | FAIL |
| MULTI-04 | 건물 변경 시 전투 중 유닛 Repath 제외 | 멀티 | FAIL |
| MULTI-05 | DETOUR 목적지 현재 타일 중복 방지 | 멀티 | FAIL |

---

### SINGLE-01: 기본 A* 이동 — 슬롯 분산

**전제:** 싱글플레이 에디터 실행. 아군 성 근처에 소형 유닛 3마리가 스폰된 상태. 적 성은 맵 반대편에 있으며 경로 상에 장애물 없음.

**동작:**
1. 유닛 3마리가 자동으로 적 성 방향으로 이동을 시작한다.

**기댓값:**
- 3마리 모두 적 성을 향해 A* 경로를 따라 타일 단위로 이동한다.
- 같은 타일에 2마리 이상이 있을 때 완전히 겹쳐 보이지 않고 앞/뒤좌/뒤우 삼각형 배치로 분산된다.
- 각 유닛이 자신의 슬롯 위치(타일 중심이 아님)를 향해 이동한다.

**결과:** PASS (2026-05-06 Round 2 확인 — BUG-001 수정 후 슬롯 분산 정상 동작)

---

### SINGLE-02: 점유 가득 찬 타일 앞쪽 우회

**전제:** 한 타일에 소형 유닛 3마리가 이미 점유 중인 상태(점유 한도=3 초과 불가). 4번째 유닛이 해당 타일로 이동하려는 상황.

**동작:**
1. 4번째 유닛이 가득 찬 타일을 다음 목표로 삼아 이동을 시도한다.

**기댓값:**
- 4번째 유닛은 가득 찬 타일로 진입하지 않는다.
- 대신 목적지(적 성) 방향으로 더 가까운 빈 타일 중 하나로 우회하여 이동한다.
- 우회 후에는 새로운 A* 경로를 계산하여 계속 전진한다.

**결과:**

---

### SINGLE-03: 앞쪽 빈 타일 없을 때 대기 후 재개

**전제:** 전방 타일들이 모두 가득 찬 극단적 상황. 유닛 1마리가 더 이상 전진할 수 없는 위치에 있음.

**동작:**
1. 해당 유닛이 전방 이동을 시도한다.
2. 전방 타일 중 일부가 비워질 때까지 기다린다.

**기댓값:**
- 유닛이 현재 위치에서 멈추고 대기 상태를 유지한다.
- 영구적으로 멈추지 않고, 전방 타일에 빈 자리가 생기면 즉시 이동을 재개한다.
- 대기 중에 뒷 타일로 이동하지 않는다.

**결과:**

---

### SINGLE-04: 뒤로 우회하지 않음 확인

**전제:** 유닛이 이동 중이며 다음 타일이 가득 찬 상태. 뒤쪽(출발 방향)에는 빈 타일이 있지만, 앞쪽(성 방향)에는 빈 타일이 없는 상황.

**동작:**
1. 유닛이 전방 이동을 시도한다.

**기댓값:**
- 유닛이 뒤쪽 타일로 이동하지 않는다.
- 앞쪽에 빈 자리가 생길 때까지 현재 위치에서 대기한다.

**결과:**

---

### SINGLE-05: 근접 유닛 — 감지 사거리 내 적 진입 시 직선 추적 전환

**전제:** 근접 유닛 1마리가 A* 경로를 따라 이동 중. 적 유닛이 감지 사거리 안으로 접근한 상황.

**동작:**
1. 근접 유닛이 이동 중에 적이 감지 사거리 내로 진입한다.

**기댓값:**
- 근접 유닛이 A* 경로 이동을 중단한다.
- 이동 슬롯을 해제하고, 적을 향해 월드 좌표 직선으로 이동한다.
- 공격 슬롯(적 주변 12방향 중 접근 방향에 가장 가까운 위치)을 배정받아 해당 위치로 이동한다.
- 공격 사거리에 도달하면 공격을 시작한다.

**결과:**

---

### SINGLE-06: 원거리 유닛 — 공격 사거리 내 적 진입 시 정지 + 공격

**전제:** 원거리 유닛 1마리가 A* 경로를 따라 이동 중. 적 유닛이 공격 사거리 안으로 접근한 상황.

**동작:**
1. 원거리 유닛이 이동 중에 적이 공격 사거리 내로 진입한다.

**기댓값:**
- 원거리 유닛이 현재 이동 슬롯 위치에서 즉시 멈춘다.
- 별도의 공격 슬롯 이동 없이 현재 위치에서 바로 공격을 시작한다.
- 공격 중 이동하지 않는다.
- 전투 종료 후 이동 슬롯 위치에서 그대로 A* 이동을 재개한다.

**결과:**

---

### SINGLE-07: 타겟 처치 후 가장 가까운 적으로 타겟 변경

**전제:** 근접 유닛이 적 1마리를 공격 중. 감지 사거리 내에 다른 적이 추가로 있는 상황.

**동작:**
1. 근접 유닛이 첫 번째 적을 처치한다.

**기댓값:**
- 첫 번째 적 처치 후 감지 사거리 내에서 가장 가까운 다른 적을 자동으로 새 타겟으로 선택한다.
- 이전 공격 슬롯을 해제하고 새 타겟의 공격 슬롯을 새로 배정받는다.
- 새 타겟을 향해 이동 및 공격을 시작한다.
- 감지 사거리 내에 더 이상 적이 없으면 A* 이동을 재개한다.

**결과:**

---

### SINGLE-08: 타겟 이탈 (감지 사거리 밖) 후 A* 재개

**전제:** 근접 유닛이 공격 슬롯으로 이동 중인 상태. 적이 감지 사거리 밖으로 이탈한 상황.

**동작:**
1. 추격 중 적이 감지 사거리 밖으로 빠져나간다.

**기댓값:**
- 근접 유닛이 추격을 중단한다.
- 공격 슬롯을 해제한다.
- 현재 월드 위치 기준으로 성 방향(앞쪽)에서 가장 가까운 타일을 찾아 A* 이동을 재개한다.
- 뒤쪽 타일로 복귀하지 않는다.

**결과:**

---

### SINGLE-09: 전투 종료 후 앞쪽 타일에서 A* 재개 (뒷무빙 없음)

**전제:** 근접 유닛이 전투를 완전히 종료한 상황. 유닛의 현재 위치가 출발 타일보다 전방에 있음.

**동작:**
1. 근접 유닛의 전투가 종료된다(적 처치 또는 이탈).
2. 유닛이 A* 이동을 재개한다.

**기댓값:**
- 유닛이 출발 타일 방향(뒤쪽)으로 돌아가지 않는다.
- 현재 월드 위치에서 성 방향으로 가장 가까운 앞쪽 타일에서 A*를 재개한다.
- 이동 재개 시 순간이동(위치 점프) 없이 자연스럽게 이어진다.

**결과:** PASS (2026-05-06 Round 2 확인 — BUG-002 수정 후 ResumeFromForwardTile transform.position 스냅 정상 동작)

---

### SINGLE-10: 근접 유닛 여러 마리 — 같은 적 공격 시 분산 배치

**전제:** 근접 유닛 4마리 이상이 같은 적 유닛 1마리를 동시에 공격하는 상황.

**동작:**
1. 4마리 이상의 근접 유닛이 같은 적을 타겟으로 잡고 접근한다.

**기댓값:**
- 유닛들이 적 주변 12방향 슬롯으로 분산 배치된다.
- 각 유닛이 접근 방향에서 가장 가까운 각도의 빈 슬롯을 배정받는다.
- 앞쪽 슬롯이 모두 차면 측면, 뒤쪽 순서로 자연스럽게 포위 형태가 만들어진다.
- 한 지점에 과도하게 몰리지 않는다.

**결과:**

---

### SINGLE-11: 유닛 사망 시 이동/공격 슬롯 즉시 해제

**전제:** 유닛이 이동 슬롯 또는 공격 슬롯을 점유 중인 상태에서 적의 공격으로 사망하는 상황.

**동작:**
1. 점유 중인 유닛이 사망한다.

**기댓값:**
- 사망 즉시 해당 유닛이 점유하던 이동 슬롯과 공격 슬롯이 해제된다.
- 해제 후 다른 유닛이 해당 슬롯을 정상적으로 배정받을 수 있다.
- 슬롯이 영구적으로 막히는 현상이 발생하지 않는다.

**결과:**

---

### SINGLE-12: 건물 건설 시 모든 유닛 경로 즉시 재계산

**전제:** 아군 유닛들이 이동 중인 상태. 이동 경로 상의 타일에 새 건물이 건설되는 상황.

**동작:**
1. 이동 경로 상에 건물을 건설한다.

**기댓값:**
- 건물이 건설되는 즉시 모든 살아있는 유닛의 경로가 재계산된다.
- 건물이 위치한 타일을 피해 새로운 경로로 이동한다.
- 경로 재계산 중 유닛이 멈추거나 이상한 방향으로 이동하지 않는다.

**결과:**

---

### SINGLE-13: 건물 파괴 시 모든 유닛 경로 즉시 재계산

**전제:** 이전에 건물이 막고 있던 경로에 더 짧은 경로가 생기는 상황. 건물이 파괴되는 순간.

**동작:**
1. 경로를 막고 있던 건물이 파괴된다.

**기댓값:**
- 건물이 파괴되는 즉시 모든 살아있는 유닛의 경로가 재계산된다.
- 이전보다 짧은 경로가 생기면 유닛이 새 경로를 사용한다.
- 경로 재계산 중 이상 동작 없음.

**결과:**

---

### SINGLE-14: 같은 타일 이동 슬롯 3슬롯(앞/뒤좌/뒤우) 분산 확인

**전제:** 같은 타일에 유닛이 최대 3마리까지 점유 가능한 상태. 동시에 3마리가 같은 타일에 진입하는 상황.

**동작:**
1. 유닛 3마리가 동일한 타일에 진입한다.

**기댓값:**
- 첫 번째 유닛이 슬롯 1번(성 방향 앞쪽 중앙)을 배정받는다.
- 두 번째 유닛이 슬롯 2번(뒤쪽 좌측)을 배정받는다.
- 세 번째 유닛이 슬롯 3번(뒤쪽 우측)을 배정받는다.
- 3마리가 삼각형 배치로 분산되어 완전 겹침이 없다.
- 슬롯 배정은 유닛의 현재 위치에서 가장 가까운 빈 슬롯 순서를 따른다.

**결과:**

---

### SINGLE-15: 점유치 2 유닛 — 슬롯 1개 + 점유 카운트 2 처리

**전제:** 점유치 2짜리 대형 유닛 1마리와 점유치 1짜리 소형 유닛 1마리가 같은 타일에 진입 가능한 상황 (합계 3 이내).

**동작:**
1. 점유치 2짜리 대형 유닛이 타일에 먼저 진입한다.
2. 점유치 1짜리 소형 유닛이 같은 타일에 진입한다.

**기댓값:**
- 대형 유닛은 물리적으로 슬롯 1개를 차지하지만, 타일 점유 카운트는 2로 기록된다.
- 소형 유닛은 남은 슬롯(빈 슬롯 중 가장 가까운 것)을 배정받아 카운트가 3이 된다.
- 두 유닛이 서로 다른 슬롯에 배치되어 시각적으로 겹치지 않는다.
- 이 상태에서 세 번째 유닛(점유치 1)이 진입을 시도하면 한도(3) 초과로 우회한다.

**결과:**

---

### MULTI-01: 멀티플레이 양팀 이동/전투 동일 동작

**전제:** 멀티플레이 모드로 두 클라이언트가 접속한 상태. 양팀이 유닛을 생산하여 이동 중인 상황.

**동작:**
1. Blue팀과 Red팀 모두 유닛을 생산하고 자동 이동을 시작한다.
2. 양팀 유닛이 서로 충돌하여 전투가 발생한다.

**기댓값:**
- 양팀 모두 A* 이동, 슬롯 분산, 전투 진입, 타겟 변경 등 싱글플레이와 동일한 동작을 수행한다.
- Blue팀 클라이언트와 Red팀 클라이언트에서 유닛 위치가 일치하여 보인다.
- 유닛 이동 결정은 서버에서만 이루어지고, 클라이언트는 NetworkTransform으로 결과를 받는다.
- 슬롯 겹침, 뒷무빙, 순간이동 등의 이상 동작이 없다.

**결과:** FAIL (2026-05-06 Round 2 — BUG-005: 유닛이 타일 사이에 정지, BUG-006: 전투 재개 직후 유령 슬롯 생성으로 유닛 뭉침/순간이동 현상 재발)

---

### MULTI-02: Lerp 진행 중 전투 진입 시 이동 슬롯 해제 및 위치 스냅

**전제:** 멀티플레이 모드. 유닛이 타일 A에서 타일 B로 이동하는 도중(이동 애니메이션 재생 중)에 적을 발견하는 상황.

**동작:**
1. 유닛이 타일 A에서 타일 B 방향으로 이동 중이다.
2. 이동이 완료되기 전에 감지 범위 내에 적이 나타난다.
3. 유닛이 전투에 진입한다.

**기댓값:**
- 유닛이 타일 B와 A 사이 허공에 멈추지 않는다.
- 유닛이 논리적 현재 위치인 타일 A 위에 정확히 스냅되어 멈춘다.
- 타일 B의 이동 슬롯이 즉시 해제되어 다른 유닛이 사용할 수 있다.
- 이후 같은 타일로 이동하려는 유닛이 정상적으로 슬롯을 배정받는다.

**결과:**

---

### MULTI-03: 전투 재개 직후 적 감지 시 슬롯 미점유 전투 진입

**전제:** 멀티플레이 모드. 근접 유닛이 전투를 막 종료하고 이동을 재개하려는 순간에 감지 범위 내에 다른 적이 이미 존재하는 상황.

**동작:**
1. 유닛의 전투 대상이 사망하여 전투가 종료된다.
2. 유닛이 이동을 재개하려 한다.
3. 이동 재개 즉시(첫 이동 슬롯 선점 전 또는 직후) 다른 적이 감지된다.

**기댓값:**
- 유닛이 이동 슬롯을 선점하지 않은 상태로 새 전투에 진입한다.
- 유닛이 타일 사이 허공에 멈추지 않는다.
- 다음 타일의 이동 슬롯이 유령 점유 상태로 남지 않는다.
- 여러 유닛이 동시에 같은 타겟 사망으로 전투 재개를 시도할 때도 동일하게 동작한다.

**결과:**

---

### MULTI-04: 건물 변경 시 전투 중 유닛 Repath 제외

**전제:** 멀티플레이 모드. 일부 유닛이 전투 중이고 다른 유닛들은 이동 중인 상태에서 건물이 건설되거나 파괴되는 상황.

**동작:**
1. 일부 유닛이 전투 중(정지 공격 또는 근접 전투)인 상태에서 건물이 건설된다.

**기댓값:**
- 전투 중인 유닛은 경로 재계산 대상에서 제외된다.
- 전투 중인 유닛이 경로 재계산으로 인해 이동을 시작하거나 이동 슬롯을 추가로 선점하지 않는다.
- 이동 중인 유닛만 새 경로를 계산하여 건물을 우회한다.
- 전투 종료 후 해당 유닛이 정상적으로 이동을 재개한다.

**결과:**

---

### MULTI-05: DETOUR 목적지 현재 타일 중복 방지

**전제:** 멀티플레이 모드. 전방 타일이 모두 가득 차 있어 우회 타일을 탐색해야 하는 상황.

**동작:**
1. 유닛이 전방으로 이동하려 하지만 전방 모든 타일이 점유 가득 찬 상태이다.
2. 시스템이 우회 가능한 타일을 탐색한다.

**기댓값:**
- 우회 목적지로 유닛이 현재 위치한 타일이 선택되지 않는다.
- 우회 이동 시 출발지와 도착지가 같아 이동 거리가 0인 상태가 발생하지 않는다.
- 빈 타일이 없으면 대기 상태로 전환하여 제자리에서 기다린다.

**결과:**

---

## 정적 분석 결과 (qa-tester)

### 분석 대상 파일 및 변경 내용

| 파일 | 변경 유형 |
|------|----------|
| `Presentation/Unit/UnitView.cs` | MoveAlongPathV2 및 헬퍼 신설 |
| `Application/Services/TileMoveSlotManager.cs` | 신규 생성 |
| `Application/Services/AttackPositionManager.cs` | ClaimByApproach 추가 |
| `Application/Services/TileOccupancyManager.cs` | FindForwardAvailable 추가 |
| `Application/UseCases/UnitMovementUseCase.cs` | FindForwardAvailable/FindForwardClosestTile 추가 |
| `Bootstrap/GameBootstrapper.cs` | TileMoveSlotManager 생성/와이어링 추가 |
| `Infrastructure/Factories/UnitFactory.cs` | _depMoveSlotManager 필드 추가 |
| `Domain/Unit/UnitStats.cs` | AttackKind 추가 |
| `Infrastructure/Config/UnitStatsConfig.cs` | attackKind 필드 추가 |

---

### BUG-001: TileMoveSlotManager 와이어링 누락 — FAIL

**심각도:** Critical (컴파일은 되지만 기능 전체 불작동)

**발견 위치:**

1) `GameBootstrapper.SetupProduction()` (1046~1048번째 줄):
```
_unitFactory.SetDependencyReferences(_config, _unitMovement, _unitCombat,
    _unitFactory, _buildingFactory, _positionProvider, _slotManager,
    _attackPositionManager);   // ← _moveSlotManager 미전달
```
`UnitFactory.SetDependencyReferences()`의 마지막 파라미터 `moveSlotManager`가 전달되지 않아 기본값인 null로 처리된다.

2) `UnitFactory.CreateUnitObject()` (287~289번째 줄):
```
unitView.SetDependencies(_depConfig, _depMovement, _depCombat,
    _depUnitFactory, _depBuildingFactory, _depPositionProvider, _depSlotManager,
    _depAttackPositionManager);   // ← _depMoveSlotManager 미전달
```
`UnitView.SetDependencies()` 호출 시 `_depMoveSlotManager`가 전달되지 않는다.

**영향:** 모든 UnitView에서 `_moveSlotManager == null` → `MoveAlongPathV2`의 슬롯 클레임 분기가 `else`(타일 중심)로 폴백 → 모든 유닛이 타일 중심으로 이동하여 겹침 현상 발생. 슬롯 분산 기능 전체 미작동.

**관련 TC:** SINGLE-01, SINGLE-14, SINGLE-15 (FAIL)

---

### BUG-002: ResumeFromForwardTile 후 transform.position 점프 — FAIL

**심각도:** Major (시각적 순간이동 발생)

**발견 위치:** `UnitView.ResumeFromForwardTile()` + `MoveAlongPathV2` + `RunTileTraversal`

**근거:**

`ResumeFromForwardTile()`에서:
- `_unitData.Position`은 forwardTile로 갱신됨 (ProcessStep 호출)
- `transform.position`은 공격 슬롯 위치(적 근처) 그대로 유지됨

`MoveAlongPathV2`에서 `V2Result.Repath`로 `RunTileTraversal`이 재진입될 때:
- `prevActualTile = _unitData.Position` = forwardTile (정상)
- `fromPos = transform.position` = 공격 슬롯 위치 (불일치)
- 첫 번째 타일 이동 시 Lerp 출발점이 공격 슬롯 위치 → 도착점이 forwardTile의 슬롯 위치

공격 슬롯은 적 주변이고 forwardTile의 슬롯 위치는 현재 전방 타일이므로, 두 위치가 크게 다를 경우 1칸 시간 안에 여러 칸을 건너뛰는 것처럼 보이는 시각적 도약이 발생한다.

**관련 TC:** SINGLE-09 (FAIL)

---

### BUG-003: FindForwardAvailable 무한 대기 — CONDITIONAL PASS

**심각도:** Major (특정 극단 상황에서 유닛 영구 정지)

**발견 위치:** `UnitView.RunTileTraversal()` 대기 루프 (1976~1990번째 줄):

```
while (actualTo == null && _unitData.IsAlive)
{
    actualTo = _movementUseCase.FindForwardAvailable(to, mySize, finalTarget);
    if (actualTo == null)
    {
        yield return null;   // 앞쪽 빈 타일 없음 — 대기
    }
}
```

코드 자체는 유닛이 살아있는 동안 계속 재시도하므로, 유닛이 사망하면 루프를 탈출한다. 그러나 전방 타일이 영구적으로 모두 가득 찬 상황(실제 게임에서는 드물지만 이론상 가능)에서는 해당 유닛이 사망할 때까지 대기한다. 대기 중 다른 유닛도 같은 상황이 되면 교착 상태(deadlock)가 발생할 수 있다.

`FindForwardAvailable`은 `TileOccupancyManager.FindForwardAvailable`을 래핑하며, 해당 메서드는 빈 타일이 없으면 null을 반환하도록 설계되어 있다. 설계 의도대로 동작하므로 정적 분석만으로 버그 판정이 어렵다.

**CONDITIONAL PASS 이유:** 설계 의도(앞쪽 빈 타일 없으면 대기)와 일치하지만, 교착 상황 발생 가능성은 실기로 최대 인구 상황에서 확인 필요.

**관련 TC:** SINGLE-03 (실기 필요)

---

### BUG-004: 공격 슬롯 fallback 무제한 — CONDITIONAL PASS

**심각도:** Minor (시각적 겹침 허용 범위 내)

**발견 위치:** `AttackPositionManager.ClaimByApproach()` (329~336번째 줄):

```
int chosenIdx = bestEmptyIdx >= 0 ? bestEmptyIdx : bestAnyIdx;
```

12방향 슬롯이 모두 차 있으면(12마리 × 1 또는 24마리 × 2) `bestAnyIdx`(가장 적게 배정된 슬롯)를 fallback으로 선택한다. `MaxUnitsPerSlot` 제한이 실제 배정 차단에 사용되지 않으므로(`_ = MaxUnitsPerSlot;` — 정보 제공용만), 이론상 한 슬롯에 무제한으로 유닛이 쌓일 수 있다.

그러나 현실적으로 한 타겟에 24마리 이상이 동시에 공격하는 상황은 게임 규모상 거의 발생하지 않으므로 즉각적인 버그 판정보다는 관찰 필요.

**CONDITIONAL PASS 이유:** 설계(D2: "슬롯이 모두 차면 가장 적게 찬 슬롯에 fallback")와 일치하며, 실기에서 실제 겹침 정도 확인 후 정책 재검토.

**관련 TC:** SINGLE-10 (실기 필요)

---

### 추가 발견 — NetworkContext 가드 확인

**위치:** `UnitView.MoveAlongPathV2()` (1860~1864번째 줄)

```
if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
{
    _moveCoroutine = null;
    yield break;
}
```

멀티플레이 클라이언트 가드가 진입부에 올바르게 존재한다. 계획(Plan.md 5-2)에서 명시한 위험 요소가 구현에서 처리됨.

판정: PASS

---

### 추가 발견 — OnPathInvalidated 연결 확인

**위치:** `GameBootstrapper.RepathAllAliveUnits()` (703~715번째 줄)

`view.OnPathInvalidated()` 호출이 존재하며, 건물 배치/사망 이벤트 구독도 `SetupEagerRepathOnBuildingChanges()`에 구현됨. 계획(Plan.md Step 5)에서 명시한 즉시 재계산 트리거가 구현됨.

단, `UnitView.OnPathInvalidated()` 메서드가 실제로 새 흐름(MoveAlongPathV2)을 재시작하는지는 코드 추가 확인 필요.

**관련 TC:** SINGLE-12, SINGLE-13

---

### TC별 정적 분석 판정 요약 (Round 1 — 2026-04-30)

| TC-ID | 정적 분석 판정 | 판정 근거 |
|-------|-------------|----------|
| SINGLE-01 | FAIL | BUG-001: _moveSlotManager null → 슬롯 분산 불작동 |
| SINGLE-02 | 실기 필요 | FindForwardAvailable 로직 정상 확인, 실제 동작은 실기로 |
| SINGLE-03 | 실기 필요 | BUG-003: 교착 가능성 있으나 실기 확인 필요 |
| SINGLE-04 | 실기 필요 | FindForwardAvailable에서 fallback BFS 미호출 로직 정상 확인 |
| SINGLE-05 | 실기 필요 | EnterMeleePursuit 진입 분기 코드 정상 확인 |
| SINGLE-06 | 실기 필요 | Ranged 분기 분리 코드 정상 확인 |
| SINGLE-07 | 실기 필요 | TARGET_SWITCH 로직 코드 정상 확인 |
| SINGLE-08 | 실기 필요 | 이탈 후 yield break + 슬롯 해제 코드 정상 확인 |
| SINGLE-09 | FAIL | BUG-002: ResumeFromForwardTile 후 transform.position 불일치 |
| SINGLE-10 | CONDITIONAL PASS | BUG-004: fallback 무제한이지만 실기 전 겹침 정도 불명 |
| SINGLE-11 | 실기 필요 | OnEntityDied 구독 + 슬롯 해제 로직 존재 확인, 실기로 누수 여부 확인 |
| SINGLE-12 | 실기 필요 | eager repath 트리거 구현 확인, OnPathInvalidated 동작은 실기로 |
| SINGLE-13 | 실기 필요 | 동상 |
| SINGLE-14 | FAIL | BUG-001 영향: 슬롯 매니저 null이면 삼각형 배치 불작동 |
| SINGLE-15 | 실기 필요 | 점유치 2 로직은 TileOccupancyManager에 존재, 슬롯 클레임은 BUG-001 해결 후 |
| MULTI-01 | 실기 필요 | 에이전트 실기 불가 — 사용자 확인 필요 |

---

## 실기 테스트 결과 (Round 2 — 2026-05-06)

### 확인된 PASS

- **SINGLE-01 / SINGLE-14 PASS** — BUG-001(TileMoveSlotManager 와이어링 누락) 수정 후 멀티플레이 실기에서 슬롯 분산 정상 동작 확인. 런타임 로그에서 `SLOT_CLAIM` 이벤트가 슬롯별로 분산 기록됨.
- **SINGLE-09 PASS** — BUG-002(ResumeFromForwardTile transform.position 불일치) 수정 후 전투 재개 시 순간이동 없음 확인. 런타임 로그에서 전투 재개 후 경로 시작점이 forwardTile로 정상 기록됨.
- **AttackKind 설정 FIXED** — EmberSpirit `kind=Melee` → MELEE_PURSUIT_START, Pistoleer `kind=Ranged` → STATIONARY_COMBAT_START 로그 근거로 BUG-005(Round 1 명명) AttackKind 오류 해소 확인.

---

### BUG-005: Lerp 진행 중 전투 진입 시 이동 슬롯 미해제 — FAIL

**심각도:** Critical (시각적 겹침 + 유령 슬롯으로 경로 장애물 생성)

**발견 위치:** `UnitView.RunTileTraversal()` — `ENEMY_DETECTED` 체크 분기 + `EnterStationaryCombat()`

**근거:**

`RunTileTraversal`에서 타일 A → B로 Lerp 진행 중 `ENEMY_DETECTED` 발동:
- 타일 B의 `SLOT_CLAIM` 이미 완료 → `EnterStationaryCombat` 진입 시 슬롯 B 미해제
- `ProcessStep`이 아직 미호출 → `_unitData.Position`이 타일 A를 가리키는 상태로 전투 진행
- 유닛은 A-B 사이 물리 위치에 Lerp 중단 상태로 고정됨

로그 근거: `[UnitID:4] SLOT_CLAIM tile=(5,8)` → `[UnitID:4] STATIONARY_COMBAT_START currentTile=(5,7)` → `[UnitID:4] UNIT_DIED position=(5,7)` → `[UnitID:4] SLOT_RELEASE tile=(5,8)`. 동일 패턴: UnitID 4, 8, 12, 16, 24 (Pistoleer 전체). 유닛이 타일 중심 아닌 허공에 멈추어 "타일과 별개 경로" 현상 + 유령 슬롯이 인접 유닛 우회 강제로 "뭉침" 현상 유발.

**관련 TC:** MULTI-01 (FAIL), MULTI-02 (FAIL)

---

### BUG-006: RESUME_FORWARD 후 즉시 ENEMY_DETECTED → 이동 슬롯 미해제 — FAIL

**심각도:** Critical (대규모 전투 타겟 사망 시 다수 유령 슬롯 한꺼번에 생성)

**발견 위치:** `UnitView.ResumeFromForwardTile()` → `RunTileTraversal` 재진입 → `ENEMY_DETECTED` 분기

**근거:**

전투 종료 후 `ResumeFromForwardTile()` → 새 경로 계산 → `RunTileTraversal` 재진입:
- 재진입 직후 다음 타일(N)의 `SLOT_CLAIM` 선점
- 이 시점에 감지 범위 내 적 존재 → Melee/Melee-InRange 분기 진입 시 슬롯 N 미해제
- 유닛이 resumeFrom 타일과 N 사이 허공에 정지, 슬롯 N 유령 점유

로그 근거: `[UnitID:15] RESUME_FORWARD` → `[TILE_MOVE from=(5,6) to=(5,5)]` → `[SLOT_CLAIM tile=(5,5)]` → `[ENEMY_DETECTED]` → `[COMBAT_START mode=Melee-InRange currentTile=(5,6)]`. 동일 패턴 UnitID 15, 14, 13, 6 — 같은 타겟 사망 시 동시 발생으로 다수 유령 슬롯 한꺼번에 생성.

**관련 TC:** MULTI-01 (FAIL), MULTI-03 (FAIL)

---

### BUG-007: Eager Repath가 전투 중 유닛에게 MoveAlongPathV2 이중 코루틴 실행 — FAIL

**심각도:** Major (이중 코루틴이 이동 슬롯 중복 선점, 유닛 경로 이탈)

**발견 위치:** `GameBootstrapper.RepathAllAliveUnits()` + `UnitView.OnPathInvalidated()` + `UnitView.MoveAlongPathV2()`

**근거:**

건물 배치/파괴 이벤트 → `RepathAllAliveUnits()` → 전투 중 유닛 포함 전체 `OnPathInvalidated()` 호출:
- `OnPathInvalidated()`는 조건 없이 `StartCoroutine(MoveAlongPathV2(...))` 실행
- 이미 `STATIONARY_COMBAT` 상태로 실행 중인 코루틴(1번)을 종료하지 않음
- 코루틴 2번이 새 경로 계산 + 다음 타일 `TILE_MOVE` + `SLOT_CLAIM` 추가 수행

로그 근거: `[UnitID:1] STATIONARY_COMBAT_START at tile=(5,8)` → 00:12:08 시각 모든 유닛 동시 `PATH_CALC` → `[UnitID:1] UNIT_DIED position=(5,8) hadMoveSlot=True SLOT_RELEASE tile=(5,9)`. 전투 타일 (5,8)에서 (5,9) 슬롯 추가 획득이 이중 코루틴의 직접 증거.

**관련 TC:** MULTI-04 (FAIL)

---

### BUG-008: DETOUR 목적지가 현재 타일로 선택되어 Lerp 거리 0 발생 — FAIL

**심각도:** Minor (시각적 순간이동 + 슬롯 중복 클레임)

**발견 위치:** `UnitView.RunTileTraversal()` DETOUR 분기 + `TileOccupancyManager.FindForwardAvailable()`

**근거:**

`FindForwardAvailable`이 전방 타일 전체 가득 찬 상황에서 우회 후보로 현재 유닛 위치 타일을 반환:
- `DETOUR originalTo=(6,8) detourTo=(7,8)` 발생 (UnitID:11이 (7,8)에 방금 도착)
- `TILE_MOVE from=(7,8) to=(7,8)` 실행 — Lerp 거리 0 → 즉시 완료
- 슬롯 클레임이 중복 발생, 시각적으로 순간이동처럼 보임

로그 근거: `[UnitID:11] TILE_ARRIVED tile=(7,8)` → `[DETOUR] originalTo=(6,8) detourTo=(7,8)` → `[TILE_MOVE] from=(7,8) to=(7,8)` → `[SLOT_CLAIM] tile=(7,8) slot=3`.

**관련 TC:** MULTI-05 (FAIL)
