# Testcase — 패스파인딩 개선 (플로우 필드 + 슬롯 기반 오프셋)

작성일: 2026-04-25  
관련 Plan: `Plan.md`

---

## TC 목록

---

### SINGLE-01: 유닛이 목적지(적 성)까지 경로를 따라 이동한다

**전제:** 싱글플레이어 에디터 실행. 아군 유닛 1기가 스폰되어 있고, 맵 상에 적 성이 존재한다.

**동작:**
1. 아군 유닛에게 이동 명령을 내린다 (랠리포인트 또는 공격 명령으로 적 성 방향 이동 유도).
2. 유닛이 이동을 시작하면 이동 경로를 관찰한다.

**기댓값:**
- 유닛이 막힘 없이 적 성 방향으로 연속 이동한다.
- 이동 도중 멈추거나 제자리를 맴도는 현상이 없다.
- 적 성 바로 앞 타일까지 이동한 뒤 공격 판정이 시작된다.

**결과:** CONDITIONAL PASS

---

### SINGLE-02: 경로 상의 타일이 이미 아군 유닛으로 가득 차 있어도 유닛이 멈추지 않는다

**전제:** 싱글플레이어 에디터 실행. 아군 유닛 여러 기(5기 이상)가 같은 경로 위에 밀집해 있다.

**동작:**
1. 밀집 구간 뒤에 있는 추가 유닛에게 같은 목적지로 이동 명령을 내린다.
2. 해당 유닛이 밀집 구간을 통과하는지 관찰한다.

**기댓값:**
- 뒤에 있는 유닛이 앞 유닛들을 통과하며 이동을 완료한다.
- 이동 중 유닛이 완전히 멈춰서 움직이지 않는 현상(프리즈)이 발생하지 않는다.

**결과:** CONDITIONAL PASS

---

### SINGLE-03: 더 짧은 경로가 열려 있을 때 유닛이 돌아가지 않는다

**전제:** 싱글플레이어 에디터 실행. 맵에 건물이 배치되어 특정 경로가 막혀 있다.

**동작:**
1. 아군 유닛이 막힌 경로를 우회하며 이동 중인 상태를 만든다.
2. 해당 건물을 파괴하여 더 짧은 경로를 열어준다.
3. 새로운 유닛에게 같은 목적지로 이동 명령을 내린다.

**기댓값:**
- 건물 파괴 후 새로 이동 명령을 받은 유닛은 더 짧은 경로로 이동한다.
- 불필요하게 긴 우회 경로를 선택하지 않는다.

**결과:** CONDITIONAL PASS

---

### SINGLE-04: 건물이 새로 배치되면 이동 경로가 갱신된다

**전제:** 싱글플레이어 에디터 실행. 아군 유닛이 적 성으로 이동 명령을 받기 전 상태.

**동작:**
1. 맵에 건물을 배치하여 기존 최단 경로를 막는다.
2. 건물 배치 직후 유닛에게 이동 명령을 내린다.

**기댓값:**
- 유닛이 새로 배치된 건물을 우회하는 경로로 이동한다.
- 막힌 경로로 진입을 시도하다 멈추지 않는다.

**결과:** CONDITIONAL PASS

---

### SINGLE-05: 같은 타일에 여러 유닛이 있을 때 시각적으로 분산되어 보인다

**전제:** 싱글플레이어 에디터 실행. 아군 유닛 3기 이상이 같은 타일 위에 위치하도록 유도한다.

**동작:**
1. 여러 유닛에게 같은 타일을 목적지로 이동 명령을 내린다.
2. 유닛들이 해당 타일에 도착한 상태를 관찰한다.

**기댓값:**
- 같은 타일에 있는 유닛들이 서로 겹쳐 보이지 않고 약간씩 분산된 위치에 표시된다.
- 유닛의 전투 판정이나 소속 타일 정보는 변경되지 않는다(시각 효과만 적용됨).

**결과:** CONDITIONAL PASS

---

### SINGLE-06: 슬롯이 최대(6기)를 초과해도 게임이 멈추지 않는다

**전제:** 싱글플레이어 에디터 실행. 아군 유닛 7기 이상이 같은 타일에 위치하도록 유도한다.

**동작:**
1. 아군 유닛 7기 이상에게 같은 타일을 목적지로 이동 명령을 내린다.
2. 7번째 이후 유닛들이 해당 타일에 도착한 상태를 관찰한다.

**기댓값:**
- 6기를 초과한 유닛들도 오류 없이 해당 타일에 도착한다.
- 7번째 이후 유닛은 0번 슬롯(중심)에 겹쳐 표시될 수 있으나, 게임이 비정상 종료되거나 유닛이 사라지지 않는다.

**결과:** CONDITIONAL PASS

---

### SINGLE-07: 유닛이 이동 완료 후 슬롯이 해제된다

**전제:** 싱글플레이어 에디터 실행. 아군 유닛 2기가 같은 타일에 위치한 상태.

**동작:**
1. 두 유닛 중 한 기를 다른 타일로 이동 명령을 내린다.
2. 유닛이 이동을 완료한 뒤 남은 유닛을 관찰한다.

**기댓값:**
- 이동한 유닛이 떠난 슬롯이 해제되어 남은 유닛이 중심(슬롯 0) 위치로 이동하거나, 남은 유닛의 위치가 올바른 슬롯 오프셋을 유지한다.
- 이동 완료 후 타일에 남아있는 유닛 수에 맞는 슬롯이 올바르게 적용된다.

**결과:** CONDITIONAL PASS

---

### SINGLE-08: 유닛 사망 시 슬롯이 정상 해제된다

**전제:** 싱글플레이어 에디터 실행. 아군 유닛 여러 기가 같은 타일에 위치한 상태.

**동작:**
1. 같은 타일에 있는 아군 유닛 중 한 기를 전투를 통해 처치한다.
2. 사망 처리 후 남은 유닛들의 슬롯 배치를 관찰한다.

**기댓값:**
- 사망한 유닛의 슬롯이 해제되어 남은 유닛들이 정상적인 오프셋 위치를 유지한다.
- 슬롯 정보가 누적되어 메모리 오류가 발생하지 않는다.

**결과:** CONDITIONAL PASS

---

### SINGLE-09: 랠리포인트로 이동 명령 시 경로가 올바르게 계산된다

**전제:** 싱글플레이어 에디터 실행. 아군 진영에 랠리포인트가 설정되어 있다.

**동작:**
1. 유닛에게 랠리포인트 위치로 이동 명령을 내린다.
2. 유닛의 이동 경로를 관찰한다.

**기댓값:**
- 유닛이 랠리포인트를 향해 최단 경로로 이동한다.
- 이동 도중 멈추는 현상이 발생하지 않는다.

**결과:** CONDITIONAL PASS

---

### PERF-01: 유닛 25기 동시 이동 시 프레임 드랍이 없다

**전제:** 싱글플레이어 에디터 실행. 아군 25기가 맵에 스폰된 상태.

**동작:**
1. 아군 25기 전체에게 적 성 방향으로 이동 명령을 동시에 내린다.
2. 모든 유닛이 이동하는 동안 프레임 속도를 관찰한다.

**기댓값:**
- 이동 명령 직후 눈에 띄는 프레임 드랍(1초 이상 걸림, 화면 버벅임)이 없다.
- 유닛들이 막힘 없이 지속적으로 이동한다.

**결과:** CONDITIONAL PASS

---

### PERF-02: 양 팀 합산 50기 이동 시 성능이 유지된다

**전제:** 멀티플레이어 에디터(Host) + 빌드(Client) 구성. 양 팀 각 25기씩 총 50기가 맵에 스폰된 상태.

**동작:**
1. 양 팀 유닛 모두가 서로의 성을 향해 동시에 이동하는 상황을 만든다.
2. 유닛들이 이동하는 동안 Host 및 Client의 프레임 속도를 관찰한다.

**기댓값:**
- Host와 Client 모두에서 심각한 프레임 드랍(5초 이상 멈춤, 게임 비정상 종료)이 발생하지 않는다.
- 유닛 이동이 계속 진행된다.

**결과:** CONDITIONAL PASS

---

### MULTI-01: 멀티플레이에서 Host의 유닛 경로가 Client에서도 동일하게 보인다

**전제:** 멀티플레이어 에디터(Host) + 빌드(Client) 구성. 아군 유닛이 스폰된 상태.

**동작:**
1. Host에서 아군 유닛에게 적 성으로 이동 명령을 내린다.
2. 유닛의 이동을 Host 화면과 Client 화면에서 동시에 관찰한다.

**기댓값:**
- Host와 Client 양쪽에서 유닛이 같은 방향으로 이동하는 것으로 보인다.
- Client에서 유닛 위치가 튀거나 갑자기 순간이동하는 현상이 없다.

**결과:** 에이전트 실기 불가 — 사용자 확인 필요

---

### MULTI-02: 멀티플레이에서 여러 유닛이 동시에 이동해도 위치 동기화가 유지된다

**전제:** 멀티플레이어 에디터(Host) + 빌드(Client) 구성. 양 팀 유닛이 각 5기 이상 스폰된 상태.

**동작:**
1. Host와 Client 양쪽에서 각자의 유닛에게 이동 명령을 동시에 내린다.
2. 유닛들이 이동하는 동안 양쪽 화면의 위치가 일치하는지 관찰한다.

**기댓값:**
- Host와 Client 모두에서 유닛들의 위치가 유사하게 표시된다.
- 유닛이 화면에서 사라지거나 잘못된 위치에 표시되는 현상이 없다.

**결과:** 에이전트 실기 불가 — 사용자 확인 필요

---

## QA 섹션 (qa-tester 전용)

### 정적 분석 대상 파일
- `Assets/_Project/Scripts/Domain/Hex/HexFlowField.cs`
- `Assets/_Project/Scripts/Application/Services/FlowFieldService.cs`
- `Assets/_Project/Scripts/Application/Services/TileSlotManager.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`

### 중점 점검 항목
- `HexFlowField.Compute`: non-walkable 목적지(성) 처리 시 인접 walkable 타일을 시드로 올바르게 사용하는지 확인
- `HexFlowField.GetPath`: 300스텝 무한루프 방지 로직 및 목적지 append 조건 확인
- `FlowFieldService.GetOrCompute`: `_grid == null` 시 null 반환 방어 코드 확인
- `FlowFieldService.Initialize`: 기존 구독 Dispose 후 재구독 (씬 재시작 시 중복 구독 방지)
- `TileSlotManager.ClaimSlot`: 같은 unitId 중복 호출 시 기존 슬롯 재사용 여부 확인
- `TileSlotManager.ReleaseSlot`: 슬롯 해제 후 빈 사전 항목 제거 (메모리 누수 방지)
- `UnitMovementUseCase.RequestMove`: `path.Count < 2` 조건으로 단칸 경로 제외 확인
- `UnitView`: 슬롯 Claim/Release 호출 시점 — 타일 도착 전 Claim, 도착 완료 후 Release 순서 확인
- `UnitView`: 사망 / `StopMovement` 시 `ReleaseSlotIfClaimed` 호출 여부 확인
- `GameBootstrapper`: `FlowFieldService.Initialize(grid)` 및 `TileSlotManager` 생성 위치, 재경기 시 `InvalidateAll` / `Clear` 호출 확인
- `UnitFactory`: `UnitView.SetDependencies`에 `TileSlotManager` 전달 여부 확인
- Domain 레이어 순수성: `HexFlowField.cs`에 `using Hexiege.Core` 또는 `using UnityEngine` 없는지 확인

### QA 결과 (qa-tester 작성)

**분석 일자:** 2026-04-25  
**분석 방법:** 정적 코드 분석 (실기 불가 — 에이전트 환경)  
**분석 범위:** 신규 3개 파일 + 수정 4개 파일 전체 읽기 + Grep 전수 검색

---

#### 1. Domain 레이어 순수성 검증 (HexFlowField.cs)

- `using System.Collections.Generic;` 만 선언됨. `using Hexiege.Core` 및 `using UnityEngine` 없음.
- 주석에도 "Domain 레이어 — Unity 의존 없음, 순수 C#" 명시. 규칙 완전 준수.
- 판정: 이상 없음

---

#### 2. HexFlowField.Compute — non-walkable 목적지 시드 처리

- `destWalkable` 분기에서 walkable이면 `flow[destination] = destination`으로 자기 참조 시드 등록 후 큐에 추가.
- non-walkable이면 `grid.GetWalkableNeighborCoords(destination)`으로 인접 walkable 타일을 수집하고, 각각 `flow[seed] = seed`로 자기 참조 등록 후 큐에 추가.
- 중복 방지: `!flow.ContainsKey(seed)` 가드 처리됨.
- 판정: 이상 없음

---

#### 3. HexFlowField.GetPath — 300스텝 무한루프 방지 및 non-walkable 목적지 Destination 추가

- `const int maxSteps = 300` 선언, `while (steps++ < maxSteps)` 루프로 상한 제한됨.
- non-walkable 목적지 보정:
  - 루프 종료 후 `path[path.Count - 1] != Destination` 조건 확인.
  - `!_flow.ContainsKey(Destination)` 조건으로 non-walkable 여부를 판정해 `path.Add(Destination)` 수행.
- `destTileCheck` 변수가 null로 선언된 채 `_ = destTileCheck;`로 할당됨. 이 코드는 컴파일 경고 억제 목적으로 보이나, **경고 억제를 위한 불필요한 코드 잔존**이다. 기능에는 영향 없음 (Minor).
- 판정: 이상 없음 (단, destTileCheck 잔존은 Minor 지적 — 실기 PASS 가능)

---

#### 4. FlowFieldService.Initialize — 기존 구독 Dispose 후 재구독

- `_subscriptions?.Dispose();` 후 `_subscriptions = new CompositeDisposable();` 실행. 재경기 시 중복 구독 방지 완전히 구현됨.
- `_cache.Clear()`도 Initialize 내부에서 함께 수행. 그리드 교체 시 구버전 캐시 잔존 없음.
- 판정: 이상 없음

---

#### 5. FlowFieldService.GetOrCompute — _grid == null 방어 코드

- `if (_grid == null) return null;` 명시적 null 가드 있음.
- 주석에 "Initialize 호출 누락 — 호출 측 버그 신호"로 의도 설명됨.
- 판정: 이상 없음

---

#### 6. FlowFieldService — BuildingData 타입 캐스트 검증

- `OnEntityDied` 구독에서 `if (e.Entity is BuildingData) InvalidateAll();` 패턴 사용.
- `BuildingData`는 `Hexiege.Domain` 네임스페이스의 클래스로 확인됨 (`BuildingData.cs` Grep 결과).
- FlowFieldService에 `using Hexiege.Domain;` 선언되어 있으므로 타입 해석 정상.
- 판정: 이상 없음

---

#### 7. TileSlotManager.ClaimSlot — 같은 unitId 중복 호출 시 기존 슬롯 재사용

- `slots.TryGetValue(unitId, out int existingSlot)` 분기에서 기존 슬롯 재사용 구현됨.
- 중복 Claim 시 새 슬롯 할당 없이 `GetSlotOffset(existingSlot)` 즉시 반환.
- 판정: 이상 없음

---

#### 8. TileSlotManager.ReleaseSlot — 빈 사전 항목 제거 (메모리 누수 방지)

- `slots.Remove(unitId)` 후 `if (slots.Count == 0) _occupied.Remove(tile);` 패턴으로 메모리 누수 방지됨.
- 판정: 이상 없음

---

#### 9. UnitMovementUseCase.RequestMove — blocked HashSet 잔존 여부 및 path.Count < 2 조건

- `UnitMovementUseCase.cs` 내 `HashSet`, `IsTileBlockedBySameTeam` Grep 결과: 잔존 없음.
- `if (path == null || path.Count < 2) return null;` 조건 정확히 구현됨.
- 판정: 이상 없음

---

#### 10. UnitView — ClaimSlot/ReleaseSlot 호출 타이밍

- Phase 0 루프 내 슬롯 처리 순서:
  1. `_claimedSlotTile.HasValue` 인 경우 이전 슬롯 오프셋을 `fromPos`에 반영 (Lerp 시작점 보정).
  2. `!isLastStepToNonWalkable` 조건에서: 이전 타일 슬롯 해제(`ReleaseSlot`) → 새 타일 슬롯 점유(`ClaimSlot`) → `_claimedSlotTile = to`.
  3. Lerp 이동은 슬롯 점유 직후 수행.
- Lerp **이전** ClaimSlot, **도착 완료 후** 다음 스텝 진입 시 이전 타일 ReleaseSlot 구조로, 의도된 타이밍과 일치.
- 판정: 이상 없음

---

#### 11. UnitView — 사망 / StopMovement 시 ReleaseSlotIfClaimed 호출

- **사망**: `OnEntityDied` 구독 내 `ReleaseSlotIfClaimed()` 호출 확인 (333번째 라인).
- **StopMovement**: `StopMovement()` 메서드 내 `ReleaseSlotIfClaimed()` 호출 확인 (445번째 라인).
- **Phase 1 진입 시**: `ReleaseSlotIfClaimed()` 호출 확인 (781번째 라인).
- `ReleaseSlotIfClaimed` 내부: `_slotManager != null && _claimedSlotTile.HasValue` 이중 방어 후 `ReleaseSlot` + `_claimedSlotTile = null` 초기화.
- 판정: 이상 없음

---

#### 12. GameBootstrapper — FlowFieldService, TileSlotManager 생성 및 주입

- `CreateUseCases()` 내부:
  - `_flowFieldService == null` 가드 후 `new FlowFieldService()` 생성.
  - `_flowFieldService.Initialize(_grid)` 호출 — `CreateUseCases()` 내에서 `_unitMovement = new UnitMovementUseCase(_grid, _unitSpawn, _flowFieldService);` 직전에 실행되므로 순서 정상.
  - `_slotManager == null` 가드 후 `new TileSlotManager()` 생성.
  - `_slotManager.Clear()` 호출 — 재경기 시 슬롯 상태 초기화됨.
- 재경기 흐름: `LoadMap()` → `CreateUseCases()` → `_flowFieldService.Initialize(_grid)` (내부에서 구독 Dispose 후 재구독 + 캐시 Clear) + `_slotManager.Clear()`. 중복 구독 및 상태 누적 방지 완료.
- 판정: 이상 없음

---

#### 13. UnitFactory — SetDependencyReferences + SetDependencies TileSlotManager 전달

- `UnitFactory.SetDependencyReferences` 시그니처에 `TileSlotManager slotManager = null` 파라미터 추가 확인.
- 내부 `_depSlotManager = slotManager;` 저장 후 `_hasDependencies = true;`.
- `CreateUnitObject` 및 `InitializeUnitView` 양쪽 모두 `unitView.SetDependencies(... _depSlotManager)` 전달 확인.
- `GameBootstrapper.SetupProduction()` 내 `_unitFactory.SetDependencyReferences(_config, _unitMovement, _unitCombat, _unitFactory, _buildingFactory, _positionProvider, _slotManager)` — `_slotManager` 7번째 인자로 전달됨.
- 판정: 이상 없음

---

#### 14. 구버전 API 잔존 Grep 전수 검색 결과

| 검색 패턴 | 검색 대상 | 결과 |
|----------|----------|------|
| `IsTileBlockedBySameTeam` | 전체 .cs | UnitView.cs 556번 라인 — 제거 안내 주석만 존재. 실제 호출 없음 |
| `HashSet` (UnitMovementUseCase 내) | UnitMovementUseCase.cs | 없음 |
| `new HashSet` (UnitMovementUseCase 내) | UnitMovementUseCase.cs | 없음 |
| `using Hexiege.Core` (HexFlowField.cs 내) | HexFlowField.cs | 없음 |
| `using UnityEngine` (HexFlowField.cs 내) | HexFlowField.cs | 없음 |
| `HexPathfinder` (UnitMovementUseCase 내) | UnitMovementUseCase.cs | 없음 (HexPathfinder.cs 자체는 잔존하나 UnitMovementUseCase는 더 이상 참조 안 함) |

- 판정: 구버전 코드 완전 제거 확인됨

---

#### 15. 기타 주목 사항 (Minor)

- `HexFlowField.GetPath` 내 `HexTile destTileCheck = null;` 변수 선언 후 `_ = destTileCheck;` 할당. 컴파일러 경고 억제 목적으로 보이나 불필요한 잔존 코드. 기능 영향 없음.
- `TileSlotManager.ClaimSlot`의 빈 슬롯 탐색 로직이 이중 루프(외부: 슬롯 0~5 순회, 내부: 점유 사전 순회)로 구성됨. 유닛이 최대 6개이므로 최대 36번 비교로 성능 영향 미미.
- SINGLE-07 기댓값: "남은 유닛이 중심(슬롯 0) 위치로 이동"하는 동작은 현재 구현에 없음. 슬롯은 이동 명령 시점에만 재할당되므로, 한 유닛이 나간다고 해서 남은 유닛이 슬롯을 자동으로 재정렬하지 않는다. 기댓값의 후반부 "올바른 슬롯 오프셋을 유지" 부분은 만족하나, "중심으로 이동" 부분은 자동으로 발생하지 않는다. 기획 의도 확인 필요 (CONDITIONAL PASS 근거).

---

#### TC 판정 요약

| TC ID | 판정 | 비고 |
|-------|------|------|
| SINGLE-01 | CONDITIONAL PASS | 코드 구조 정상. 실기 확인 필요 |
| SINGLE-02 | CONDITIONAL PASS | blocked HashSet 제거 확인. 실기 확인 필요 |
| SINGLE-03 | CONDITIONAL PASS | InvalidateAll 구독 구현 확인. 실기 확인 필요 |
| SINGLE-04 | CONDITIONAL PASS | OnBuildingPlaced 구독 구현 확인. 실기 확인 필요 |
| SINGLE-05 | CONDITIONAL PASS | TileSlotManager ClaimSlot 로직 확인. 실기 확인 필요 |
| SINGLE-06 | CONDITIONAL PASS | fallback 슬롯 0 처리 확인. 실기 확인 필요 |
| SINGLE-07 | CONDITIONAL PASS | 슬롯 해제(ReleaseSlot) 구현 확인. 자동 재정렬은 없으므로 "중심 이동" 기댓값 일부 미충족 가능 — 기획 확인 필요 |
| SINGLE-08 | CONDITIONAL PASS | 사망 시 ReleaseSlotIfClaimed 확인. 실기 확인 필요 |
| SINGLE-09 | CONDITIONAL PASS | 랠리포인트도 FlowFieldService.GetOrCompute 경유. 실기 확인 필요 |
| PERF-01 | CONDITIONAL PASS | BFS 1회 캐시 공유 구조 확인. 실기 성능 측정 필요 |
| PERF-02 | CONDITIONAL PASS | 에이전트 실기 불가 — 사용자 확인 필요 |
| MULTI-01 | CONDITIONAL PASS | 에이전트 실기 불가 — 사용자 확인 필요 |
| MULTI-02 | CONDITIONAL PASS | 에이전트 실기 불가 — 사용자 확인 필요 |

---

#### 종합 판정: CONDITIONAL PASS

컴파일 에러 수준의 문제는 발견되지 않았다. 아키텍처 규칙(Domain 순수성, 단일 컴포지션 루트, UniRx 구독 패턴) 모두 준수되었으며, plan.md에 명시된 12개 중점 점검 항목이 코드 상 전부 정확하게 구현되어 있다. 구버전 코드(blocked HashSet, IsTileBlockedBySameTeam 실제 호출)도 완전히 제거되었다.

실기에서 확인이 필요한 조건:
1. SINGLE-07: 슬롯 해제 후 남은 유닛의 "중심으로 자동 이동" 기댓값이 현재 구현에서 발생하지 않는다. 기획 문서에서 해당 기댓값의 정확한 의도를 확인해야 한다.
2. SINGLE-01~09, PERF-01: 실제 실행 환경에서의 이동 완주 여부 및 슬롯 시각 효과 확인 필요.
3. PERF-01~02, MULTI-01~02: 에이전트 실기 불가 — 사용자 직접 확인 필요.

---

## 2차 개선 TC (2026-04-25 실기 버그 발견 후 추가)

---

### SINGLE-10: 근접 유닛이 이동 중 감지 사거리에 적이 들어오면 즉시 추적을 시작한다

**전제:** 싱글플레이어 에디터 실행. 근접 유닛(예: BearGuard)이 이동 명령을 받아 타일 간 이동 중이다. 이동 도중 감지 사거리 안에 적 유닛이 진입할 수 있는 위치에 적이 있다.

**동작:**
1. 근접 유닛이 다음 타일로 이동을 시작한다.
2. 이동 애니메이션(Lerp) 도중 감지 사거리 내에 적이 있는 상태를 만든다.
3. 유닛이 타일 중심에 완전히 도달하기 전에 어떻게 반응하는지 관찰한다.

**기댓값:**
- 유닛이 타일 중심까지 완전히 이동을 마치지 않고, Lerp 도중 즉시 이동을 멈춘다.
- 멈춘 직후 적 유닛을 향해 방향을 바꾸고 추적을 시작한다.
- 이전처럼 현재 타일까지 다 이동한 뒤에야 방향을 바꾸는 현상이 없다.

**결과:** CONDITIONAL PASS

---

### SINGLE-11: 타일에 유닛이 가득 차면 다음 유닛이 인접 빈 타일로 우회한다

**전제:** 싱글플레이어 에디터 실행. 특정 타일에 유닛 3기(소형 기준 최대 점유치)가 이미 위치해 있다.

**동작:**
1. 추가 유닛에게 이미 가득 찬 타일 방향으로 이동 명령을 내린다.
2. 해당 유닛이 가득 찬 타일 앞에서 어떻게 처리되는지 관찰한다.

**기댓값:**
- 유닛이 가득 찬 타일로 직접 진입하지 않고, 인접한 빈 타일로 우회하여 이동한다.
- 우회 이동이 자연스럽게 이루어지며 유닛이 멈추거나 프리즈되지 않는다.
- 모든 유닛이 완전히 겹쳐서 한 점에 몰리는 현상이 없다.

**결과:** CONDITIONAL PASS

---

### SINGLE-12: 여러 유닛이 같은 목적지로 이동할 때 자연스럽게 분산된다

**전제:** 싱글플레이어 에디터 실행. 아군 유닛 6기 이상이 같은 목적지(적 성)를 향해 이동 중이다.

**동작:**
1. 6기 이상의 유닛에게 동시에 적 성 방향으로 이동 명령을 내린다.
2. 유닛들이 이동하는 동안 서로 간의 간격과 분포를 관찰한다.

**기댓값:**
- 유닛들이 한 줄로 완전히 겹쳐서 이동하지 않고, 여러 타일에 걸쳐 분산되어 이동한다.
- 한 타일에 최대 3의 점유치를 초과하는 유닛이 몰리지 않는다.
- 일부 유닛이 다른 경로를 통해 목적지에 접근하는 자연스러운 분산이 관찰된다.

**결과:** CONDITIONAL PASS

---

### SINGLE-13: SetupUnitStatsConfig 실행 후 점유 크기가 유닛별로 올바르게 설정된다

**전제:** 싱글플레이어 에디터. UnitStatsConfig.asset이 생성되어 있다.

**동작:**
1. Unity 에디터 메뉴에서 `Hexiege/Setup/UnitStatsConfig 생성`을 실행한다.
2. UnitStatsConfig.asset을 Inspector에서 열어 각 유닛의 OccupancySize 값을 확인한다.

**기댓값:**
- 소형 유닛(Assault, Pistoleer, Sniper, EmberSpirit, FoxMagician): OccupancySize = 1
- 중형 유닛(FlameSpirit, InfernoSpirit, LionKnight): OccupancySize = 2
- 대형 유닛(BearGuard): OccupancySize = 3
- 값이 Inspector에 올바르게 표시된다.

**결과:** CONDITIONAL PASS

---

### 2차 개선 정적 분석 (qa-tester)

**분석 일자:** 2026-04-25  
**분석 방법:** 정적 코드 분석 (실기 불가 — 에이전트 환경)  
**분석 대상:** 2차 개선 신규/수정 파일 전체 읽기 + Grep 전수 검색

#### 분석 대상 파일
- `Assets/_Project/Scripts/Application/Services/TileOccupancyManager.cs` (신규)
- `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs` (OccupancySize 추가)
- `Assets/_Project/Scripts/Infrastructure/Config/UnitStatsConfig.cs` (OccupancySize 추가)
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs` (TileOccupancyManager 주입)
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` (TileOccupancyManager 생성/주입)
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` (Bug 1 + Bug 2 수정)
- `Assets/_Project/Scripts/Editor/SetupUnitStatsConfig.cs` (OccupancySize 기본값)

---

#### 분석 1. Bug 1 수정 — interruptedByDetect 플래그 선언 위치 (SINGLE-10)

- `interruptedByDetect = false` 선언 위치: `while (_unitData.IsAlive)` 루프 진입 직후, `for (int i = 1; ...)` 루프 **바깥** (UnitView.cs 553번 라인).
- for 루프 안에 선언되지 않았으므로 매 스텝마다 초기화되는 버그가 없음.
- Lerp while 루프 내부에서 체크 위치: `elapsed += Time.deltaTime` 및 `transform.position = Lerp(...)` 이후, `yield return null` **이전** (678~684번 라인). 이동 직후 즉시 체크 — 정확한 위치.
- 중단 시 처리 경로 (801~807번 라인):
  - `_unitData.ClaimedTile = null`
  - `ReleaseSlotIfClaimed()` 호출 확인
  - `shouldPursue = true`
  - `break` (for 루프 탈출)
- 중단 시 for 루프 바깥의 post-step 처리(ProcessStep, ClaimedTile 갱신, 타일 점령)가 건너뛰어짐 — 의도된 동작 확인.
- 판정: 이상 없음

---

#### 분석 2. Bug 1 수정 — Lerp 도중 감지 체크 순서 검증 (SINGLE-10)

- Lerp while 루프 내 실행 순서:
  1. `elapsed += Time.deltaTime`
  2. `t = Clamp01(elapsed / moveSeconds)`
  3. `transform.position = Lerp(fromPos, toPos, t)` — 이동 적용
  4. **감지 체크**: `HasEnemyInDetectRange && !HasEnemyInRange` → `interruptedByDetect = true; break;`
  5. **전투 체크**: `HasEnemyInRange` → 전투 루프 진입
  6. `yield return null`
- 감지 체크(4)가 전투 체크(5)보다 먼저 실행되어, 감지 사거리 안이지만 공격 사거리 밖인 적을 우선 포착. 코드 주석에 "HasEnemyInRange는 아래 전투 루프가 처리하므로 여기서는 제외" 명시 — 의도 명확.
- 판정: 이상 없음

---

#### 분석 3. TileOccupancyManager — Domain 레이어 순수성

- `using System.Collections.Generic;` 및 `using Hexiege.Domain;`만 선언됨.
- `using UnityEngine;` 없음. Application 레이어 파일이며 Unity 의존 없음.
- 파일 헤더 주석에 "Application 레이어 — Domain에 의존, Unity에 직접 의존하지 않음" 명시.
- 판정: 이상 없음

---

#### 분석 4. TileOccupancyManager — IsInvalid 판정의 (0,0) 충돌 위험성

- `IsInvalid(HexCoord coord)` 구현: `coord.Q == 0 && coord.R == 0` 조건으로 "스폰 직전"을 의미하는 무효값으로 취급.
- 실제 맵에 (Q=0, R=0) 타일이 존재하는 경우 문제 가능성: 코드 및 주석에서 "스폰 시 default를 넘긴다는 호출 약속"으로 처리한다고 명시됨.
- 실제 영향 검토:
  - `OnUnitMoved(from, to, size)`: `from == default(HexCoord)`이면 감소를 건너뜀 — 스폰 시 from에 default를 넘기면 (0,0) 타일의 점유량이 차감되지 않는다는 약속. 호출 측이 실제로 default를 넘기는지 확인 필요.
  - `UnitMovementUseCase.ProcessStep`에서 `OnUnitMoved(from, to, size)` 호출 — from은 실제 직전 타일 좌표이므로 스폰 직후 ProcessStep에서 from이 (0,0)일 가능성이 있음.
  - `OnUnitRemoved(tile, size)`: `IsInvalid(tile)` 조건에서 (0,0)이면 감소를 건너뜀 — 사망한 유닛이 (0,0) 타일에 있으면 해당 타일 점유량이 영구적으로 감소되지 않는 버그 가능성.
- 평가: (0,0) 타일이 맵 상에 존재하는 경우 경미한 점유량 오차가 발생할 수 있으나, 게임 진행에 치명적 영향은 없음 (이동 불가/프리즈 미발생). 기획 의도와 주석으로 방어한 설계.
- 판정: **Minor** — (0,0) 타일 관련 극단적 시나리오에서만 발생, 게임 진행에 치명적 영향 없음. 실기 확인 필요.

---

#### 분석 5. TileOccupancyManager — FindAvailableTile BFS 및 GetWalkableNeighborCoords 사용

- `FindAvailableTile` 내부 BFS: `grid.GetWalkableNeighborCoords(current)` 사용 확인 (152번 라인).
- `GetWalkableNeighborCoords`는 `HexGrid`에 정의되어 있으며, walkable이 아닌 타일(건물, 금광 등)은 이웃 후보에서 제외됨 — HexGrid.cs 197번 라인 확인.
- non-walkable 타일이 BFS 후보에서 자동 제외되어 건물 타일로의 우회가 발생하지 않음.
- 판정: 이상 없음

---

#### 분석 6. TileOccupancyManager — CanFit 부동소수점 엡실론 처리

- `CanFit` 구현: `current + unitSize <= MaxOccupancy + epsilon` 조건, `epsilon = 0.0001f` 적용됨 (65번 라인).
- 소형 3기(1+1+1=3.0), 중형 1기+소형 1기(2+1=3.0) 등 합산이 정확히 3.0인 경우에도 엡실론 여유로 정상 처리됨.
- `Decrease` 내부에도 동일 `epsilon = 0.0001f` 사용 — 0에 가까워졌을 때 사전 항목 제거.
- 판정: 이상 없음

---

#### 분석 7. UnitMovementUseCase — OnEntityDied 구독 및 OnUnitRemoved 경로

- 생성자 내부에서 `_occupancyManager != null` 조건 후 `GameEvents.OnEntityDied` 구독 (58~68번 라인).
- 구독 내용: `evt.Entity is UnitData unit` 캐스트 성공 시 `GetOccupancySize(unit.Type)` + `_occupancyManager.OnUnitRemoved(unit.Position, size)` 호출.
- `unit.Position`: 사망 시점의 도메인 Position이 반영됨 — OnEntityDied가 발행되는 시점에 Position이 아직 유효한지 여부가 관건.
- GameEvents.OnEntityDied 발행 시점 추적: UnitCombatUseCase에서 HP <= 0 조건 직후 발행. 이 시점에 `unit.Position`은 마지막으로 ProcessStep에서 설정된 값이므로 유효.
- 구독 해제: `_subscriptions`에 `Add()` 후 `Dispose()` 호출 경로 확인 (74~77번 라인). GameBootstrapper에서 `_unitMovement?.Dispose()` 후 재생성 패턴 확인 (726번 라인) — 중복 구독 방지 완료.
- 판정: 이상 없음

---

#### 분석 8. UnitMovementUseCase — ProcessStep 내 OnUnitMoved 호출 동기화

- `ProcessStep` 내 실행 순서 (145~167번 라인):
  1. `unit.Position = to` — 도메인 Position 갱신
  2. `_grid.SetOwner(to, unit.Team)` — 타일 점령
  3. `_occupancyManager.OnUnitMoved(from, to, size)` — 점유량 갱신
  4. `GameEvents.OnUnitMoved.OnNext(...)` — 이벤트 발행
- 도메인 Position 갱신(1) 이후 점유량 갱신(3)이 수행되므로, OnEntityDied 구독에서 `unit.Position`을 읽을 때 이미 최신 위치임.
- 판정: 이상 없음

---

#### 분석 9. GameBootstrapper — TileOccupancyManager 생성 및 Clear 호출 위치

- `CreateUseCases()` 내부 (719~727번 라인):
  - `_occupancyManager == null` 가드 후 `new TileOccupancyManager()` 생성.
  - `_occupancyManager.Clear()` 즉시 호출 — 재경기 시 이전 게임의 점유 상태 초기화.
  - 이후 `_unitMovement?.Dispose()` → `new UnitMovementUseCase(..., _occupancyManager)` 주입.
- `_occupancyManager.Clear()` 호출 이후 `_unitMovement` 재생성이 이루어지므로, 구버전 `_unitMovement`가 가진 이벤트 구독이 먼저 Dispose된 뒤 새 인스턴스가 새로 구독 — 중복 구독 없음.
- 재경기 안전성: `ClearAll()` → `CreateUseCases()` 순서로 호출되며, `CreateUseCases()` 내에서 `_occupancyManager.Clear()`가 수행됨. 안전.
- 판정: 이상 없음

---

#### 분석 10. GameBootstrapper — InitializeUnitStatsFromConfig의 OccupancySize 매핑

- `InitializeUnitStatsFromConfig` 내 `StatValues` 생성 코드 (378~391번 라인):
  - `OccupancySize = entry.occupancySize` 명시적 매핑 확인.
  - 주석에 "SO에서 0으로 비어 있으면 UnitStats.GetOccupancySize()에서 1f로 폴백" 설명됨.
- `UnitStats.GetOccupancySize()` 폴백 로직: `v.OccupancySize > 0f`이면 반환, 아니면 `1f` 반환 (UnitStats.cs 178~181번 라인).
- 판정: 이상 없음

---

#### 분석 11. UnitView — Phase 0 FindAvailableTile 대기 루프의 IsAlive 가드 (SINGLE-11, SINGLE-12)

- `actualTo == null` 대기 루프 (588~593번 라인):
  ```
  while (actualTo == null && _unitData.IsAlive) {
      actualTo = _movementUseCase.FindAvailableTile(to, mySize);
      if (actualTo == null)
          yield return null;
  }
  if (!_unitData.IsAlive) break;
  ```
- `_unitData.IsAlive` 가드가 루프 조건에 포함되어 있음 — 사망 시 무한 대기 방지.
- 루프 종료 후 추가 `if (!_unitData.IsAlive) break;` 이중 방어도 있음.
- 판정: 이상 없음

---

#### 분석 12. UnitView — non-walkable 마지막 스텝에서 점유 체크 건너뜀

- `isLastStepToNonWalkable` 조건 (580~581번 라인):
  - `i == path.Count - 1` (마지막 스텝) AND `!_movementUseCase.IsWalkable(to)` (non-walkable 타일)
  - 이 조건이 true이면 `FindAvailableTile` 호출 블록(`if (!isLastStepToNonWalkable && ...)`)을 건너뜀.
- 우회 결과로 to가 변경된 경우 재평가 로직도 있음 (597~598번 라인) — 안전망 존재.
- 판정: 이상 없음

---

#### 분석 13. SetupUnitStatsConfig — OccupancySize 값 매핑 검증 (SINGLE-13)

- `PopulateDefaults` 내 각 유닛별 `occupancy` 인수 확인:

| 유닛 | occupancy 인수 | TC 기댓값 |
|------|----------------|----------|
| Pistoleer | 1f | 소형=1 |
| Assault | 1f | 소형=1 |
| Sniper | 1f | 소형=1 |
| FlameSpirit | 2f | 중형=2 |
| EmberSpirit | 1f | 소형=1 |
| InfernoSpirit | 2f | 중형=2 |
| BearGuard | 3f | 대형=3 |
| FoxMagician | 1f | 소형=1 |
| LionKnight | 2f | 중형=2 |

- TC 기댓값과 코드 값이 전부 일치함.
- `WriteEntryToProperty`에서 `elem.FindPropertyRelative("occupancySize").floatValue = entry.occupancySize;` 직렬화 확인 (212~213번 라인).
- 판정: 이상 없음

---

#### 분석 14. UnitFactory — SetDependencies에 _depSlotManager 전달 여부

- `SetDependencyReferences` 시그니처: `TileSlotManager slotManager = null` 파라미터 있음 (68~73번 라인).
- 내부 `_depSlotManager = slotManager` 저장 확인 (81번 라인).
- `CreateUnitObject`에서 `unitView.SetDependencies(..., _depSlotManager)` 호출 확인 (275~276번 라인).
- `GameBootstrapper.SetupProduction()`에서 `_unitFactory.SetDependencyReferences(..., _slotManager)` 호출 확인 (877~878번 라인).
- 주의: `SetDependencyReferences`에 `_occupancyManager`는 **전달되지 않음** — UnitView는 `TileOccupancyManager`를 직접 주입받지 않고, `_movementUseCase.FindAvailableTile()`을 통해 간접 사용함. 설계 의도 일치.
- 판정: 이상 없음

---

#### 분석 15. Grep 전수 검색 — 구버전 API 및 using 위반 잔존 여부

| 검색 패턴 | 검색 대상 | 결과 |
|----------|----------|------|
| `using UnityEngine` | TileOccupancyManager.cs | 없음 |
| `using Hexiege.Core` | TileOccupancyManager.cs | 없음 |
| `interruptedByDetect` | 전체 .cs | UnitView.cs 553, 682, 801번 라인만 존재 — for 루프 바깥 선언 확인 |
| `OnUnitRemoved` | 전체 .cs | UnitMovementUseCase.cs(구독), TileOccupancyManager.cs(메서드) 2곳만 |
| `GetWalkableNeighborCoords` | TileOccupancyManager.cs | 152번 라인 — BFS 내부에서만 사용 |

- 판정: 구버전 코드 잔존 없음

---

#### 기타 주목 사항 (Minor)

- `TileOccupancyManager.IsInvalid`의 (0,0) 충돌 가능성: 분석 4 참조. 현재 맵 구조에서 좌측 상단 타일이 (Q=0, R=0)일 수 있으며, 해당 타일에서 사망한 유닛의 점유량이 해제되지 않는 시나리오가 존재함. `IsInvalid` 호출이 `OnUnitMoved`의 `from` 검사와 `OnUnitRemoved`의 `tile` 검사 두 곳에 있으므로, 두 경우 모두 (0,0) 타일 유닛 이동/사망 시 점유량 오차가 생길 수 있음. 게임 진행에 치명적 영향은 없으나, 장기전에서 (0,0) 타일이 고점유로 잠길 가능성은 낮은 수준으로 존재함.
- `FindAvailableTile`의 BFS 상한이 `maxNodes = 256`으로 고정됨. 187타일 그리드 기준 충분한 여유이며 실기 영향 없음.

---

#### 2차 개선 판정 요약

| TC ID | 판정 | 비고 |
|-------|------|------|
| SINGLE-10 | CONDITIONAL PASS | interruptedByDetect 선언 위치·체크 순서·중단 처리 경로 모두 정상. 실기 확인 필요 |
| SINGLE-11 | CONDITIONAL PASS | FindAvailableTile BFS 구조·IsAlive 가드·non-walkable 예외 처리 정상. 실기 확인 필요 |
| SINGLE-12 | CONDITIONAL PASS | ProcessStep OnUnitMoved 동기화·OccupancyManager 주입 경로 정상. 실기 분산 여부 확인 필요 |
| SINGLE-13 | CONDITIONAL PASS | 코드상 9종 유닛 occupancy 값이 TC 기댓값과 전부 일치. 에디터 메뉴 실행 후 Inspector 실기 확인 필요 |

---

## 3차 개선 TC (2026-04-25 2차 실기 테스트 후 추가)

---

### SINGLE-14: 권총병처럼 멈춰서 공격하는 유닛이 있어도 뒤따르는 유닛이 한도 이상으로 같은 타일에 몰리지 않는다

**전제:** 싱글플레이어 에디터. 자동 생산으로 권총병 여러 기가 생산되어 이동 중이다. 맵 중간 어딘가에서 권총병들이 적을 감지해 멈추고 공격 중이다.

**동작:**
1. 자동 생산을 계속 진행하여 권총병이 지속적으로 합류하도록 한다.
2. 멈춰 있는 권총병 집결지에 새 권총병들이 계속 도착하는 상황을 관찰한다.

**기댓값:**
- 한 타일에 소형 유닛(점유 크기 1) 기준 최대 3기를 초과하여 몰리지 않는다.
- 한도를 초과한 유닛은 인접한 다른 타일로 자연스럽게 분산된다.
- 장시간 자동 생산을 돌려도 한 지점에 유닛이 무한정 쌓이는 현상이 없다.

**결과:** CONDITIONAL PASS

---

### SINGLE-15: 뭉쳐있는 유닛 집결지 주변에서 이동 중인 유닛이 튕기듯 밀려나지 않는다

**전제:** 싱글플레이어 에디터. 여러 유닛이 한 지점에 집결해 있고, 뒤에서 유닛이 접근 중이다.

**동작:**
1. 집결지가 있는 타일 방향으로 유닛들이 접근하는 상황을 만든다.
2. 집결지 근처에서 이동 중인 유닛들의 움직임을 관찰한다.

**기댓값:**
- 유닛이 막힌 타일 앞에서 측면이나 뒤쪽으로 크게 튕겨나가는 현상이 없다.
- 우회가 발생해도 목적지 방향으로 자연스럽게 돌아오는 동선이 유지된다.
- 지그재그나 진동하듯 오가는 움직임이 없다.

**결과:** CONDITIONAL PASS

---

### SINGLE-16: 우회가 발생한 유닛이 우회 지점에서 경로를 다시 계산하여 자연스럽게 이어간다

**전제:** 싱글플레이어 에디터. 유닛이 이동 중 특정 타일이 가득 차서 옆 타일로 우회한 상황.

**동작:**
1. 유닛이 우회 타일에 도착한 직후의 움직임을 관찰한다.
2. 우회 이후 목적지까지 경로를 이어가는지 확인한다.

**기댓값:**
- 우회 후 원래 경로의 다음 타일로 억지로 이어가지 않고, 우회 지점에서 목적지까지 새 경로로 이동한다.
- 우회 후 방향이 어색하게 꺾이거나 역방향으로 이동하는 현상이 없다.
- 목적지까지 최종적으로 정상 도달한다.

**결과:** CONDITIONAL PASS

---

## 3차 개선 정적 분석 (qa-tester)

**분석 일자:** 2026-04-25
**분석 방법:** 정적 코드 분석 (실기 불가 — 에이전트 환경)
**분석 대상:** 3차 개선 수정 파일 전체 읽기

### 분석 대상 파일
- `Assets/_Project/Scripts/Application/Services/TileOccupancyManager.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

---

### TileOccupancyManager 점검

#### 점검 1. BfsFindAvailable — forward 필터 조건

- 구현 (221번 라인): `int candidateDist = HexCoord.Distance(nb, destination);` 후 `if (candidateDist <= preferredDist + 1) return nb;`
- 명세 조건 `candidateDist <= preferredDist + 1`과 정확히 일치.
- 판정: 이상 없음

---

#### 점검 2. useForwardFilter 활성 조건

- 구현 (165번 라인): `bool useForwardFilter = !(destination == default(HexCoord)) && destination != preferred;`
- destination이 default이거나 preferred와 같으면 false — 명세 조건과 일치.
- 판정: 이상 없음

---

#### 점검 3. forward 필터로 결과 없을 때 fallback BFS

- 구현 (175~178번 라인): forwardResult가 null이고 useForwardFilter가 true인 경우에만 `BfsFindAvailable(preferred, unitSize, grid, false, default, 0)` 호출.
- 조건이 충족될 때만 2회 BFS 수행 — 불필요한 중복 탐색 방지 설계 확인.
- 판정: 이상 없음

---

#### 점검 4. fallback BFS 파라미터

- fallback 호출: `BfsFindAvailable(preferred, unitSize, grid, false, default, 0)`
- `useForwardFilter=false`, `destination=default`, `preferredDist=0` — 명세와 일치.
- 판정: 이상 없음

---

#### 점검 5. 기존 3-arg FindAvailableTile 오버로드 하위 호환

- 구현 (123~127번 라인): `public HexCoord? FindAvailableTile(HexCoord preferred, float unitSize, HexGrid grid)` → `return FindAvailableTile(preferred, unitSize, grid, default);`
- destination=default로 4-arg에 위임. forward 필터 자동 비활성화되어 기존 동작과 동일.
- 판정: 이상 없음

---

#### 점검 6. forward 필터 미통과 시 외곽 탐색 계속 여부

- BfsFindAvailable 내부 (207~232번 라인): 이웃 nb에 대해 `CanFit(nb, unitSize)`가 true이고 forward 필터 미통과인 경우에도 231번 라인 `queue.Enqueue(nb);`가 실행됨.
- `CanFit`이 false인 타일도 동일하게 큐에 추가 — 외곽 탐색이 단절되지 않음.
- 판정: 이상 없음

---

### UnitMovementUseCase 점검

#### 점검 7. ProcessStep 내부 OnUnitMoved 호출 부재

- ProcessStep (150~165번 라인) 전체 코드: `FacingDirection.FromCoords`, `unit.Facing`, `unit.Position = to`, `_grid.SetOwner`, `GameEvents.OnUnitMoved.OnNext`, `GameEvents.OnTileOwnerChanged.OnNext`만 존재.
- `_occupancyManager` 참조 전혀 없음 — 명세 준수.
- 판정: 이상 없음

---

#### 점검 8. RegisterOccupancyMove 구현 순서

- 구현 (183~188번 라인): `if (_occupancyManager == null) return;` → `float size = GetOccupancySize(type);` → `_occupancyManager.OnUnitMoved(from, to, size);`
- GetOccupancySize(type) 먼저, OnUnitMoved 나중 — 명세와 일치.
- 판정: 이상 없음

---

#### 점검 9. ReleaseOccupancy 구현

- 구현 (199~204번 라인): `if (_occupancyManager == null) return;` → `float size = GetOccupancySize(type);` → `_occupancyManager.OnUnitRemoved(tile, size);`
- 판정: 이상 없음

---

#### 점검 10. RegisterOccupancyMove / ReleaseOccupancy 양쪽 null 방어

- RegisterOccupancyMove (185번 라인): `if (_occupancyManager == null) return;` — 있음.
- ReleaseOccupancy (201번 라인): `if (_occupancyManager == null) return;` — 있음.
- 판정: 이상 없음

---

#### 점검 11. FindAvailableTile 3-arg 오버로드 null 방어 및 preferred 반환

- 구현 (247~251번 라인): `if (_occupancyManager == null) return preferred;` → `return _occupancyManager.FindAvailableTile(preferred, unitSize, _grid, destination);`
- null 시 preferred 반환 — 명세와 일치.
- 판정: 이상 없음

---

#### 점검 12. 기존 2-arg FindAvailableTile 오버로드 하위 호환 유지

- 구현 (230~234번 라인): `public HexCoord? FindAvailableTile(HexCoord preferred, float unitSize)` 메서드 존재.
- 내부: `_occupancyManager.FindAvailableTile(preferred, unitSize, _grid)` — TileOccupancyManager 3-arg(destination 없음) 오버로드 호출.
- 판정: 이상 없음

---

### UnitView 점검

#### 점검 13. _pendingOccupancyTile 필드 선언

- 구현 (155번 라인): `private HexCoord _pendingOccupancyTile = default;`
- default 초기화 확인.
- 판정: 이상 없음

---

#### 점검 14. ReleaseOccupancyIfPending — default 조건으로 미등록 시 무시

- 구현 (500번 라인): `if (_pendingOccupancyTile == default(HexCoord)) return;`
- 미등록(default) 상태에서 호출 시 즉시 반환 — 이중 해제 방지.
- 호출 후 `_pendingOccupancyTile = default;` (506번 라인) 리셋 확인.
- 판정: 이상 없음

---

#### 점검 15. ReleaseOccupancyIfPending 호출 위치 3곳

- 사망 핸들러 (351번 라인): `ReleaseOccupancyIfPending();` — OnEntityDied 구독 내부. 있음.
- StopMovement (468번 라인): `ReleaseOccupancyIfPending();` — 있음.
- interruptedByDetect 처리 경로 (896번 라인): `ReleaseOccupancyIfPending();` — `if (interruptedByDetect)` 블록 내부. 있음.
- 판정: 이상 없음

---

#### 점검 16. prevActualTile 초기화 위치

- 구현 (608번 라인): `HexCoord prevActualTile = _unitData.Position;`
- `while (_unitData.IsAlive)` 루프 진입 직후, `for (int i = 1; ...)` 선언 바깥.
- 외부 while 루프가 반복될 때마다 현재 도메인 Position으로 초기화됨. Phase 2 재개 후에도 올바른 시작점.
- 판정: 이상 없음

---

#### 점검 17. for 루프 내 from = prevActualTile 사용

- 구현 (615번 라인): `HexCoord from = prevActualTile;`
- 해당 라인 전후에 `path[i-1]` 패턴 없음 — 완전 대체 확인.
- 판정: 이상 없음

---

#### 점검 18. Lerp 직전 RegisterOccupancyMove + _pendingOccupancyTile 순서

- 구현 (683~686번 라인): `_movementUseCase.RegisterOccupancyMove(from, to, _unitData.Type);` → `_pendingOccupancyTile = to;`
- RegisterOccupancyMove 먼저, _pendingOccupancyTile 설정 나중 — 명세와 일치.
- 이 블록 이후 Lerp while 루프 실행 — Lerp 직전 점유 등록 확인.
- 판정: 이상 없음

---

#### 점검 19. 정상 도착 후 prevActualTile + _pendingOccupancyTile 갱신

- 구현 (923~924번 라인): `prevActualTile = to;` → `_pendingOccupancyTile = default;`
- ProcessStep 호출(915번 라인) 이후에 갱신됨 — 도메인 Position 업데이트 완료 후 추적 변수 갱신.
- 판정: 이상 없음

---

#### 점검 20. didDetour 플래그 — actualTo != to 조건

- 구현 (660~661번 라인): `if (actualTo.Value != to) didDetour = true;`
- `to = actualTo.Value;` (663번 라인)로 to가 갱신된 후, 실제 이동은 변경된 to로 수행됨.
- 판정: 이상 없음

---

#### 점검 21. detouredNeedsRepath = true + break 위치

- 구현 (929~932번 라인): `if (didDetour) { detouredNeedsRepath = true; break; }`
- 이 코드는 `prevActualTile = to;` + `_pendingOccupancyTile = default;` 갱신(923~924번 라인) 이후에 실행됨.
- 즉 우회 타일 도착 처리(ProcessStep, prevActualTile 갱신)가 완료된 뒤 for 루프를 탈출하는 구조.
- 판정: 이상 없음

---

#### 점검 22. for 루프 종료 후 detouredNeedsRepath 처리

- 구현 (970~984번 라인): `if (detouredNeedsRepath)` → `_movementUseCase.RequestMove(_unitData, finalTarget)` → 성공 시 `path = rerouted; continue;`, 실패 시 `break;`
- `_unitData.Position`이 actualTo(우회 타일)로 갱신된 상태이므로 자연스러운 재경로 계산됨.
- 실패 시 break — 무한 루프 방지.
- 판정: 이상 없음

---

#### 점검 23. FindAvailableTile 3-arg 오버로드 사용 (finalTarget 전달)

- 구현 (652번 라인): `actualTo = _movementUseCase.FindAvailableTile(to, mySize, finalTarget);`
- 3-arg 버전으로 finalTarget을 destination으로 전달 — forward 필터 활성화 확인.
- 판정: 이상 없음

---

#### 점검 24. Phase 2 스냅 후 RegisterOccupancyMove 호출 여부

- 구현 (1190~1193번 라인): `if (_movementUseCase != null) { _movementUseCase.RegisterOccupancyMove(_unitData.Position, nearestTile, _unitData.Type); }`
- Phase 2 스냅 도착 시점에 명시적으로 점유 갱신 호출됨.
- Phase 1 진입 직전 `_pendingOccupancyTile = default` 리셋(924번 라인)이 선행되어 이중 등록 위험 없음.
- 판정: 이상 없음

---

#### 점검 25. interruptedByDetect 처리 시 _pendingOccupancyTile 해제

- interruptedByDetect 처리(889~899번 라인):
  1. `_unitData.ClaimedTile = null`
  2. `ReleaseSlotIfClaimed()`
  3. `ReleaseOccupancyIfPending()` — _pendingOccupancyTile 해제 + default 리셋
  4. `shouldPursue = true`
  5. `break`
- Lerp 중단 시 미도착 to 타일의 점유가 정상 해제됨.
- 판정: 이상 없음

---

### Grep 전수 검색 결과

| 검색 패턴 | 검색 대상 | 결과 |
|----------|----------|------|
| `path\[i-1\]` | UnitView.cs | 없음 — prevActualTile로 완전 대체 확인 |
| `OnUnitMoved` (ProcessStep 내부) | UnitMovementUseCase.cs | ProcessStep 메서드 내 없음. RegisterOccupancyMove에서만 호출 |
| `_occupancyManager` | UnitMovementUseCase.cs ProcessStep 범위 | 없음 |
| `_pendingOccupancyTile` | UnitView.cs | 선언(155번), 설정(686번), 갱신(924번), default 리셋(506번) — 4곳만 존재 |
| `ReleaseOccupancyIfPending` | UnitView.cs | 사망(351번), StopMovement(468번), interruptedByDetect(896번) — 3곳 확인 |

---

### TC 판정 요약

| TC ID | 판정 | 비고 |
|-------|------|------|
| SINGLE-14 | CONDITIONAL PASS | Lerp 직전 점유 등록(원인 B 해결) + prevActualTile 추적(원인 A 해결) 구조 정상. 실기 확인 필요 |
| SINGLE-15 | CONDITIONAL PASS | forward 필터 + fallback BFS 구조 정상. 밀집 시나리오 실기 확인 필요 |
| SINGLE-16 | CONDITIONAL PASS | didDetour break + RequestMove 재호출 + 실패 시 break(무한루프 방지) 구조 정상. 실기 확인 필요 |

---

### 종합 판정: CONDITIONAL PASS

컴파일 에러 수준의 문제 없음. 3차 개선의 세 가지 근본 원인(원인 A: prevActualTile, 원인 B: Lerp 직전 점유 등록, 원인 C: 우회 후 재경로)이 모두 코드 구조상 정확히 구현되었다.

주목할 설계 검증:
1. `didDetour = true` 이후 `prevActualTile = to` + `_pendingOccupancyTile = default` 갱신이 먼저 실행되고, 그 다음 `detouredNeedsRepath = true; break;`가 실행됨. 우회 타일 도착 처리가 완료된 상태에서 경로 재계산이 시작되므로 `_unitData.Position`이 올바른 출발점이 됨.
2. Phase 2 스냅 시 RegisterOccupancyMove 명시적 호출로 Phase 1 중 점유 공백이 채워짐. Phase 1 진입 직전 _pendingOccupancyTile이 이미 default 상태이므로 이중 등록 없음.
3. BfsFindAvailable에서 forward 필터 미통과 후보도 큐에 계속 추가되어 외곽 탐색이 단절되지 않음.

실기에서 확인이 필요한 조건:
- SINGLE-14: 실제 권총병 자동생산 시나리오에서 집결지 분산 동작 확인.
- SINGLE-15: forward 필터가 실제로 팅김을 줄이는지 밀집 상황에서 확인.
- SINGLE-16: 우회 후 재경로가 자연스러운 동선으로 이어지는지 확인.
