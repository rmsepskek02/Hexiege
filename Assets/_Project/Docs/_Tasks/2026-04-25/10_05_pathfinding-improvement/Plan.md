# Plan — 패스파인딩 개선 (플로우 필드 + 슬롯 기반 오프셋)

**날짜:** 2026-04-25
**작업명:** pathfinding-improvement
**참조:** Research.md

---

## 목표

| 문제 | 해결 방법 |
|------|-----------|
| 유닛 많으면 멈춤 | 플로우 필드 도입 + 아군 겹침 허용 (ClaimedTile 차단 제거) |
| 돌아가는 현상 | 플로우 필드: 타일 이동마다 최적 방향 재조회 |
| 시각적 겹침 | 슬롯 기반 오프셋: 같은 타일 유닛을 시각적으로 분산 |

---

## 변경 범위

### 신규 파일

| 파일 | 레이어 | 역할 |
|------|--------|------|
| `Domain/Hex/HexFlowField.cs` | Domain | BFS 기반 플로우 필드 계산 |
| `Application/Services/FlowFieldService.cs` | Application | 플로우 필드 생명주기 관리 |
| `Application/Services/TileSlotManager.cs` | Application | 타일별 슬롯 점유 관리 |

### 수정 파일

| 파일 | 변경 내용 |
|------|-----------|
| `Application/UseCases/UnitMovementUseCase.cs` | RequestMove → 플로우 필드 사용, blocked 구성 변경 |
| `Presentation/Unit/UnitView.cs` | 슬롯 오프셋 적용, per-step 차단 체크 제거 |
| `Bootstrap/GameBootstrapper.cs` | FlowFieldService, TileSlotManager 생성 및 주입 |

---

## 구현 상세

### 1. HexFlowField (Domain/Hex/HexFlowField.cs)

BFS(너비 우선 탐색)로 목적지 타일에서 역방향으로 전체 맵을 탐색하여  
각 타일에 "다음에 이동할 타일"을 기록하는 클래스.

**A*와 차이점:**
- A*: 출발→목적지 방향으로 탐색, 특정 유닛 1개의 경로를 반환
- 플로우 필드: 목적지→전체 맵 방향으로 탐색, 맵 전체 방향 정보를 저장

```
주요 멤버:
  - Dictionary<HexCoord, HexCoord> _flow
      키: 타일 좌표  /  값: 다음에 이동할 타일 좌표
  - HexCoord _destination
      이 플로우 필드의 목적지 좌표

주요 메서드:
  - static HexFlowField Compute(HexGrid grid, HexCoord destination)
      BFS로 플로우 필드 계산 후 반환 (목적지가 non-walkable이면 인접 walkable에서 시작)
  - HexCoord? GetNextTile(HexCoord current)
      현재 타일에서 다음 타일 조회. 경로 없으면 null.
  - List<HexCoord> GetPath(HexCoord start)
      start에서 _destination까지의 경로 리스트 생성 (UnitView 호환용)
      최대 300타일 제한(무한루프 방지)
```

**BFS 순서:**  
1. 목적지 타일을 큐에 넣고 시작  
2. 큐에서 타일을 꺼낼 때 해당 타일의 walkable 이웃 6방향을 탐색  
3. 아직 방문하지 않은 이웃: `_flow[이웃] = 현재타일` 기록 후 큐에 추가  
4. 큐가 빌 때까지 반복  

결과: 맵의 모든 walkable 타일이 목적지 방향 화살표를 가짐

**건물(non-walkable) 목적지 처리:**  
`Compute` 호출 시 목적지가 non-walkable이면  
인접 walkable 타일들을 BFS 시작점으로 사용 (기존 `FindPathToNeighbor`와 동일한 의도).  
GetPath 결과 마지막에 건물 타일을 추가하는 것은 UnitMovementUseCase에서 처리.

---

### 2. FlowFieldService (Application/Services/FlowFieldService.cs)

목적지 타일별로 플로우 필드를 캐싱하여 관리하는 서비스.  
성, 랠리포인트 등 모든 목적지에 플로우 필드를 사용 — A* 분기 없음.

```
주요 멤버:
  - Dictionary<HexCoord, HexFlowField> _cache
      목적지 좌표 → 플로우 필드 캐시 (성, 랠리포인트 모두 포함)
  - HexGrid _grid (참조)

주요 메서드:
  - void Initialize(HexGrid grid)
      그리드 참조만 저장. 플로우 필드는 첫 요청 시 계산.
  - HexFlowField GetOrCompute(HexCoord destination)
      캐시에 있으면 반환, 없으면 HexFlowField.Compute() 후 캐시 저장 및 반환
  - void InvalidateAll()
      모든 캐시 제거. 건물 배치/파괴 시 호출.
      이후 요청이 들어올 때 필요한 필드만 다시 계산됨 (lazy recompute)
```

**이벤트 구독:**  
- `GameEvents.OnBuildingPlaced` → `InvalidateAll()` 호출  
- `GameEvents.OnEntityDied` (건물 사망) → `InvalidateAll()` 호출

**랠리포인트 처리:**  
랠리포인트도 동일하게 `GetOrCompute(rallyPoint)`로 처리.  
랠리포인트 변경 시 이전 캐시는 `InvalidateAll()`로 무효화.  
187타일 BFS는 1ms 미만이므로 재계산 비용 없음.  
모든 목적지에 동일한 시스템 사용 — `RequestMove` 내 분기 없음.

---

### 3. TileSlotManager (Application/Services/TileSlotManager.cs)

같은 타일에 여러 유닛이 있을 때 시각적 위치를 분산시키기 위한 슬롯 관리자.  
논리 위치(HexCoord)는 변경하지 않고 시각 위치(transform.position)에만 오프셋 적용.

```
슬롯 정의 (타일 중심 기준 View 좌표 오프셋):
  슬롯 0: (0,    0)     ← 중심
  슬롯 1: (-0.3, 0)     ← 왼쪽
  슬롯 2: (+0.3, 0)     ← 오른쪽
  슬롯 3: (-0.15, +0.3) ← 앞-왼쪽
  슬롯 4: (+0.15, +0.3) ← 앞-오른쪽
  슬롯 5: (0,    -0.3)  ← 뒤쪽
  (6번 이상: 슬롯 0으로 fallback, 시각적 겹침 허용)

단위: HexMetrics.TileWidth 기준 비율값으로 최종 결정
      (실제 수치는 구현 후 시각 테스트로 조정)

주요 멤버:
  - Dictionary<HexCoord, Dictionary<int, int>> _occupied
      키: 타일 좌표 / 값: (unitId → 슬롯 번호)

주요 메서드:
  - Vector3 ClaimSlot(HexCoord tile, int unitId)
      타일에서 빈 슬롯을 찾아 unitId에 할당 → 슬롯 오프셋 Vector3 반환
  - void ReleaseSlot(HexCoord tile, int unitId)
      해당 유닛의 슬롯 해제
  - Vector3 GetSlotOffset(int slotIndex)
      슬롯 번호 → View 좌표 오프셋 반환 (XZ 평면, Y=0)
```

**슬롯 할당 타이밍:**  
- Phase 0 에서 다음 타일로 Lerp 시작 직전: `ClaimSlot(to, unitId)` → Lerp 목적지에 오프셋 추가  
- 타일 도착 후: `ReleaseSlot(from, unitId)` (이전 타일 슬롯 해제)  

---

### 4. UnitMovementUseCase 수정

#### RequestMove 변경

```
변경 전:
  blocked = 아군 Position + ClaimedTile 수집
  HexPathfinder.FindPath(blocked)

변경 후:
  field = _flowFieldService.GetOrCompute(target)
  path = field.GetPath(unit.Position)
  목적지가 non-walkable(성/건물)이면 path 끝에 target 추가
```

성, 랠리포인트 구분 없이 동일한 경로로 처리. 분기 없음.

blocked HashSet을 완전히 제거하여 아군 겹침을 허용.  
`IsTileBlockedBySameTeam`은 Phase 0에서 더 이상 사용하지 않으므로 메서드를 제거하거나 빈 상태로 유지.

#### ClaimedTile 처리

`ProcessStep`은 변경 없음.  
UnitData.ClaimedTile 자체는 유지하나 경로 계산 시 차단 목록에서 제외.  
(향후 참조하는 다른 코드 없는지 확인 후 완전 제거 검토)

---

### 5. UnitView 수정

#### Phase 0 per-step 차단 체크 제거

```
변경 전 (UnitView.cs:520-533):
  if (IsTileBlockedBySameTeam(to))
      newPath = RequestMove(...)
      if null → break
      else → i=0, continue

변경 후:
  해당 블록 전체 삭제
  (플로우 필드가 타일 이동마다 최적 방향을 제공하므로 재계산 불필요)
```

#### 슬롯 오프셋 적용

```
Lerp 이동 직전:
  Vector3 slotOffset = _slotManager.ClaimSlot(to, _unitData.Id)
  toPos = 타일 중심 뷰 좌표 + slotOffset

타일 도착 후 (ClaimedTile = null 이전):
  _slotManager.ReleaseSlot(from, _unitData.Id)
```

Phase 1(직선 추적) 중에는 슬롯 적용 없음 — 월드 좌표로 자유 이동 중이라 불필요.  
Phase 2 스냅 후 타일 도착 시: `ClaimSlot` 호출.

#### Phase 2 재계산

변경 없음. `_movementUseCase.RequestMove(_unitData, finalTarget)` 호출이  
내부적으로 플로우 필드를 사용하므로 투명하게 동작.

---

### 6. GameBootstrapper 수정

```
추가:
  private FlowFieldService _flowFieldService;
  private TileSlotManager _slotManager;

LoadMap() 내 초기화 순서:
  1. HexGrid 생성 (기존)
  2. FlowFieldService 생성 → Initialize(grid)
     (성 좌표 사전 주입 불필요 — 첫 RequestMove 시 lazy 계산)
  3. TileSlotManager 생성
  4. UnitMovementUseCase 생성 시 FlowFieldService 주입
  5. UnitView.SetDependencies 호출 시 TileSlotManager 주입
```

---

## 구현 순서 (권장)

```
[1] HexFlowField.cs 작성 및 단독 테스트
      → BFS 로직 완성 여부를 먼저 확인

[2] FlowFieldService.cs 작성
      → HexFlowField를 래핑하는 수준

[3] UnitMovementUseCase.RequestMove 수정
      → FlowFieldService 연결, blocked 제거
      → 이 시점에서 이동이 플로우 필드로 전환됨 (슬롯 없는 상태)

[4] GameBootstrapper 수정
      → 의존성 주입 연결

[5] UnitView.cs Phase 0 차단 체크 제거
      → [3][4] 완료 후 진행

[6] TileSlotManager.cs 작성

[7] UnitView.cs 슬롯 오프셋 적용

[8] 슬롯 오프셋 수치 시각 테스트 후 조정
```

---

## 위험 요소 및 주의사항

### 위험 1: 첫 요청 시 플로우 필드 계산 타이밍
Lazy 계산 방식이므로 첫 유닛이 이동 명령을 받는 시점에 BFS가 실행됨.  
187타일 BFS는 1ms 미만이라 게임플레이 체감 영향 없음.  
성 좌표는 RequestMove 호출 시 target으로 전달되므로 사전 주입 불필요.

### 위험 2: 건물 파괴 시 재계산 타이밍
성이 파괴되면 게임 종료이므로 걱정 불필요.  
일반 건물(막사, 골드마인) 파괴 시 walkable이 변경되어 플로우 필드 재계산 필요.  
OnEntityDied 이벤트에서 건물 여부를 체크하여 Recompute 호출.

### 위험 3: 멀티플레이 서버 권위
현재 경로 계산은 서버만 실행 (NetworkUnitMovementController).  
FlowFieldService도 서버에서만 초기화·사용. 클라이언트는 NetworkTransform으로 동기화.  
슬롯 오프셋은 Presentation 레이어이므로 클라이언트에서도 독립적으로 적용 가능.  
단, 서버와 클라이언트의 슬롯 할당이 동일하지 않아도 시각적으로만 다를 뿐 전투 판정에 영향 없음.

### 위험 4: Phase 1 종료 후 Phase 2 스냅 위치
Phase 2에서 `nearestTile`을 찾고 ProcessStep을 호출하는 로직은 변경 없음.  
이후 RequestMove 호출이 플로우 필드를 사용하므로 자연스럽게 연결됨.

### 위험 5: ClaimedTile 참조 여부
UnitData.ClaimedTile을 현재 참조하는 곳:
  - UnitMovementUseCase.RequestMove → blocked 구성 (제거 대상)
  - UnitMovementUseCase.IsTileBlockedBySameTeam → (제거 대상)
  - UnitView.MoveAlongPath → ClaimedTile 설정/해제 (슬롯 관리로 역할 이전)
제거 범위를 명확히 확인 후 처리.

---

## 기대 효과

- **멈춤 현상 제거**: blocked 제거로 경로 계산 실패 자체가 없어짐
- **돌아가는 현상 제거**: 타일 이동마다 플로우 필드에서 최적 방향 조회
- **자연스러운 대형**: 같은 방향으로 이동하는 유닛이 슬롯 분산으로 넓게 퍼짐
- **기존 전투/점령 시스템 무변경**: HexCoord 기반 판정은 그대로 유지

---

## 2차 개선 — 실기 테스트 후 발견 문제 (2026-04-25)

### 발견된 버그 요약

| # | 증상 | 원인 | 해결 방법 |
|---|------|------|-----------|
| Bug 1 | 근접 유닛이 감지 사거리 내 적을 발견해도 현재 Lerp 완료(타일 중심 도착)까지 이동 후 추적 시작 | 감지 사거리 체크가 Lerp 루프 밖에 위치 | Lerp 루프 내부에 체크 추가, 감지 즉시 Lerp 중단 |
| Bug 2 | 유닛이 완전히 겹치고 한 경로로 뭉치는 현상 | 슬롯이 6개뿐이라 7번째 유닛부터 모두 중심(slot 0)으로 fallback; 플로우 필드가 동일 방향을 제시해 집결 | 타일 점유 시스템 도입: 타일당 최대 점유치 3 초과 시 BFS로 대체 타일 탐색 |

---

### Fix 1: 근접 유닛 감지 사거리 Lerp 중단

**수정 파일:** `Presentation/Unit/UnitView.cs`

**문제 위치:**  
`MoveAlongPath()` Phase 0의 Lerp 루프(`while (elapsed < moveSeconds)`)가 끝난 뒤에야  
`HasEnemyInDetectRange && !HasEnemyInRange` 체크가 실행됨.  
→ Lerp 도중 적을 감지해도 현재 타일 중심까지 이동 완료 후 추적 시작.

**수정 내용:**

```
변경 전 구조 (Phase 0 내부):
  for (i = 1; i < path.Count; i++):
    while (elapsed < moveSeconds):      ← Lerp 루프
      elapsed += Time.deltaTime
      transform.position = Lerp(...)
      yield return null
    [타일 도착 처리]
  [루프 종료 후]
  if (HasEnemyInDetectRange && !HasEnemyInRange):  ← 감지 체크 (너무 늦음)
    shouldPursue = true

변경 후 구조:
  bool interruptedByDetect = false
  for (i = 1; i < path.Count; i++):
    while (elapsed < moveSeconds):      ← Lerp 루프
      elapsed += Time.deltaTime
      transform.position = Lerp(...)
      if (HasEnemyInDetectRange && !HasEnemyInRange):   ← 감지 체크 (즉시)
        interruptedByDetect = true
        break
      yield return null
    if (interruptedByDetect):
      ReleaseSlotIfClaimed()            ← 슬롯 해제
      shouldPursue = true
      break                             ← for 루프 탈출
    [타일 도착 처리 — interruptedByDetect가 false일 때만 실행]
```

**주의사항:**
- Lerp 중단 시 타일 도착 후 처리(ClaimedTile 업데이트, ReleaseSlot 이전 타일 등)를 건너뜀
- `interruptedByDetect` 플래그로 post-step 처리 블록 전체를 건너뜀
- `ReleaseSlotIfClaimed()`는 중단 직후 호출하여 슬롯 누수 방지
- Phase 1 진입 로직(`shouldPursue = true`)은 기존 흐름 그대로 재사용

---

### Fix 2: 타일 점유 시스템 (TileOccupancyManager)

#### 설계 원칙

- **예약 없음**: 실제 유닛의 도메인 Position에 기반한 점유만 추적 (ClaimedTile 문제 재발 방지)
- **타일당 최대 점유치 3**: 소형(1), 중형(2), 대형(3) 유닛 기준
- **이동 전 체크**: Lerp 시작 직전에 다음 타일 점유 가능 여부 확인 (시각적·최적화 모두 유리)
- **BFS 우회**: 점유치 초과 시 인접 타일 → 그 인접 타일 순으로 BFS 탐색하여 여유 타일 발견
- **대기**: 모든 인접 타일이 꽉 찬 경우(이론상 거의 불가) `yield return null` 반복 대기

#### 유닛 점유 크기 (OccupancySize)

| 유닛 타입 | 점유 크기 |
|----------|---------|
| 소형 유닛 | 1 |
| 중형 유닛 | 2 |
| 대형 유닛 | 3 |

UnitStatsConfig ScriptableObject에 `OccupancySize` 필드로 저장.  
게임 인구 한계(보유 타일 수 = 최대 인구)에 의해 "모든 타일 가득 참" 상태는 현실적으로 불가능.

#### 신규 파일

**`Application/Services/TileOccupancyManager.cs`**

```
주요 멤버:
  - Dictionary<HexCoord, float> _occupancy
      키: 타일 좌표 / 값: 현재 점유치 합계

주요 메서드:
  - bool CanFit(HexCoord tile, float unitSize)
      _occupancy[tile] + unitSize <= 3 이면 true
  - void OnUnitMoved(HexCoord from, HexCoord to, float unitSize)
      from 점유치 감소, to 점유치 증가 (from == HexCoord.Invalid이면 증가만)
  - void OnUnitRemoved(HexCoord tile, float unitSize)
      tile 점유치 감소
  - HexCoord? FindAvailableTile(HexCoord preferred, float unitSize, HexGrid grid)
      preferred부터 BFS 탐색 → CanFit이 true인 첫 번째 walkable 타일 반환
      모두 꽉 차면 null 반환
  - void Clear()
      전체 점유 정보 초기화 (맵 재로드 시)
```

#### 수정 파일 목록

| 파일 | 변경 내용 |
|------|-----------|
| `Infrastructure/Config/UnitStatsConfig.cs` | `UnitStatEntry`에 `float OccupancySize` 필드 추가 |
| `Domain/Unit/StatValues.cs` (또는 UnitStats) | `float OccupancySize` 필드 추가 |
| `Bootstrap/GameBootstrapper.cs` | TileOccupancyManager 생성, 의존성 주입, OccupancySize 기본값 설정 |
| `Application/UseCases/UnitMovementUseCase.cs` | TileOccupancyManager 주입; ProcessStep에서 점유 업데이트 호출; FindAvailableTile 위임 |
| `Presentation/Unit/UnitView.cs` | Phase 0 각 스텝 직전 점유 가능 여부 확인; 초과 시 대체 타일로 이동 또는 대기 |
| `Editor/SetupUnitStatsConfig.cs` | OccupancySize 기본값 설정 (유닛 타입별) |

#### UnitMovementUseCase 수정 상세

```
ProcessStep(unit, from, to):
  기존 로직 (ClaimedTile 업데이트 등) 그대로 유지
  _tileOccupancyManager.OnUnitMoved(from, to, GetOccupancySize(unit.Type)) 추가

GetOccupancySize(UnitType type):
  _unitStats.GetOccupancySize(type) 조회 반환

FindAvailableTile 위임:
  _tileOccupancyManager.FindAvailableTile(preferred, size, _grid) 호출
```

#### UnitView 수정 상세 (Phase 0)

```
for (i = 1; i < path.Count; i++):
  HexCoord nextTile = path[i]
  float mySize = _movementUseCase.GetOccupancySize(_unitData.Type)

  // 점유 가능 여부 확인 (이동 전 체크)
  HexCoord? actualNext = null
  while (actualNext == null):
    actualNext = _movementUseCase.FindAvailableTile(nextTile, mySize)
    if (actualNext == null):
      yield return null    // 공간 생길 때까지 대기 (거의 발생 안 함)

  // actualNext로 Lerp 수행 (슬롯 클레임 포함)
  // ... 기존 Lerp 코드, nextTile을 actualNext로 대체 ...

  // Lerp 완료 후: Fix 1의 interruptedByDetect 체크
  // ... 기존 post-step 처리 ...
```

**Phase 1 중 점유 처리:**  
Phase 1은 월드 좌표 자유 이동이므로 점유 시스템 미적용.  
Phase 2 스냅 후 도메인 Position이 갱신될 때 `OnUnitMoved` 호출.

---

### 구현 순서

```
[1] UnitStatsConfig + StatValues: OccupancySize 필드 추가
[2] TileOccupancyManager 작성
[3] UnitMovementUseCase: TileOccupancyManager 주입 + ProcessStep 점유 업데이트
[4] GameBootstrapper: TileOccupancyManager 생성 및 주입, 기본값 설정
[5] UnitView Fix 1: Lerp 루프 내 감지 체크 추가 (interruptedByDetect)
[6] UnitView Fix 2: Phase 0 각 스텝 전 점유 가능 확인 + 대기
[7] Editor/SetupUnitStatsConfig: OccupancySize 기본값 추가
```

---

## 3차 개선 — 2차 구현 실기 테스트 후 발견 문제 (2026-04-25)

### 발견된 문제 요약

| # | 증상 | 원인 |
|---|------|------|
| 문제 1 | 점유치 한도(3)를 초과해 유닛이 한 타일에 몰리는 현상 지속 | Race Condition + `from` 추적 오류로 점유 정보가 점점 틀어짐 |
| 문제 2 | 뭉쳐있는 곳 주변에서 유닛이 팅기듯 밀려나는 현상 | BFS 우회 후 원래 경로를 그대로 이어가면서 방향 불일치 발생 |

---

### 근본 원인 분석

#### 원인 A — `from` 추적 오류 (점유 정보 부정확의 핵심)

Phase 0 루프에서 `from = path[i-1]`로 하드코딩되어 있는데, 우회(actualTo ≠ path[i])가 발생하면 다음 스텝의 `from`이 실제 이전 도착 타일이 아닌 원래 경로 타일이 됩니다.

```
예시:
  스텝 i: from=path[i-1], to=path[i] → actualTo=sideA (우회)
    ProcessStep(path[i-1], sideA) → 점유: sideA +1 ✓
  
  스텝 i+1: from=path[i] (틀림! 실제 이전 위치는 sideA)
    ProcessStep(path[i], path[i+1]) → 점유: path[i] -1 ← 이 유닛이 간 적 없는 타일
```

결과: 막혔던 타일(path[i])의 점유치가 잘못 감소 → "비어있다"고 판단하고 후속 유닛이 계속 진입 → 한도 초과 뭉침.  
sideA 점유치는 영구적으로 +1 누적 → 이 타일이 실제보다 혼잡하게 보임.

#### 원인 B — Race Condition (동시 진입 판정)

같은 프레임에 여러 유닛 코루틴이 동시에 `CanFit`을 확인합니다.  
이 시점에는 아직 아무도 도착하지 않았으므로 모두 통과 → 동시에 같은 타일로 출발 → 한도 초과.

#### 원인 C — 우회 후 경로 불일치 (팅김의 원인)

우회로 `actualTo`에 도착했지만 이후 스텝은 원래 path[i+1]로 이어집니다.  
`actualTo` → `path[i+1]` 방향이 측면·후방이면 유닛이 지그재그로 움직이고, 시각적으로 "팅기는" 현상이 나타납니다.

---

### 수정 계획

#### 수정 A — 점유 업데이트 타이밍 변경 (Race Condition + `from` 오류 동시 해결)

**핵심 아이디어:** 점유 업데이트를 Lerp 완료 후(ProcessStep 내부)가 아닌 **Lerp 시작 직전**에 수행.

```
변경 전:
  actualTo 결정 → Lerp 시작 → Lerp 완료 → ProcessStep → OnUnitMoved (점유 갱신)

변경 후:
  actualTo 결정 → OnUnitMoved(prevActualTile, actualTo) 즉시 호출 (점유 갱신)
              → Lerp 시작 → Lerp 완료 → ProcessStep (도메인 로직만, 점유 갱신 제외)
```

효과:
- 같은 프레임에 다른 유닛이 CanFit 확인 시 이미 점유된 것으로 인식 → Race Condition 해결
- `prevActualTile` 변수를 도입해 실제 이전 도착 타일을 추적 → `from` 오류 해결

**`prevActualTile` 추적 방식:**

```
prevActualTile = _unitData.Position  (루프 시작 전 초기화)

for (i = 1; i < path.Count; i++):
  from = prevActualTile              (path[i-1] 대신 실제 이전 타일)
  to   = path[i]
  actualTo = FindAvailableTile(to, mySize)
  
  OnUnitMoved(from, actualTo, size)  ← Lerp 전 즉시 점유 갱신
  
  [Lerp 수행]
  
  ProcessStep(unit, from, actualTo)  ← 도메인 로직만 (점유 갱신 없음)
  prevActualTile = actualTo          ← 다음 스텝의 from을 위해 기록
```

**ProcessStep 변경:**  
점유 업데이트(`OnUnitMoved`) 호출을 ProcessStep 내부에서 제거.  
ProcessStep은 도메인 Position 갱신, 타일 점령, 이벤트 발행만 담당.

**사망/중단 시 점유 정리:**  
Lerp 시작 전에 점유를 등록했으므로, 이동 중단 시(사망, 전투 진입) 등록된 점유를 해제해야 합니다.  
`ReleaseOccupancyIfRegistered()` 메서드 추가하여 중단 경로에서 호출.  
(기존 `ReleaseSlotIfClaimed()`와 동일한 패턴)

---

#### 수정 B — 우회 후 경로 재계산 (팅김 해결)

우회가 발생했다는 것은 원래 경로가 현재 상황에 맞지 않는다는 신호입니다.  
우회 타일 도착 즉시 `for` 루프를 break하고, `while` 루프 상단에서 현재 위치 기준으로 flow field 경로를 새로 계산합니다.

```
for (i = 1; i < path.Count; i++):
  actualTo = FindAvailableTile(to, mySize)
  
  [점유 등록, Lerp, ProcessStep]
  
  if (actualTo != to):          ← 우회 발생
    break                       ← for 루프 탈출
                                ← while 루프가 RequestMove(unit, finalTarget) 재호출
                                ← unit.Position = actualTo 기준으로 새 경로 계산
```

효과: 우회 후 항상 현재 위치에서 최적 경로로 재출발 → 지그재그 없음.

---

#### 수정 C — 우회 방향 개선 (팅김 추가 개선)

BFS가 목적지와의 거리(HexCoord 기준)를 고려하여 더 가까운 타일을 우선 탐색하도록 변경.  
현재 BFS는 탐색 순서가 무작위(이웃 배열 순서)여서 뒤쪽·측면 타일이 먼저 선택될 수 있습니다.

```
FindAvailableTile 내 BFS 수정:
  후보 타일 발견 시 즉시 반환하지 않고,
  목적지까지의 거리(HexCoord.Distance)가 preferred보다 멀지 않은 타일을 우선 선택.
  (완전 정렬이 아닌 "forward 필터" 수준 — 성능 영향 최소화)
```

효과: 우회 타일이 후방이 아닌 측면·전방으로 한정 → 수정 B와 함께 적용 시 팅김 완전 해소.

---

### 수정 파일 목록

| 파일 | 변경 내용 |
|------|-----------|
| `Application/Services/TileOccupancyManager.cs` | `FindAvailableTile` BFS에 목적지 방향 필터 추가; `OnUnitMoved` 시그니처 유지 |
| `Application/UseCases/UnitMovementUseCase.cs` | ProcessStep에서 `OnUnitMoved` 제거; `RegisterOccupancyMove(from, to, size)` 신규 메서드 추가; `FindAvailableTile`에 destination 파라미터 추가 |
| `Presentation/Unit/UnitView.cs` | `prevActualTile` 추적 변수 도입; 점유 등록을 Lerp 전으로 이동; 우회 발생 시 for 루프 break; `ReleaseOccupancyIfRegistered()` 추가 |

---

### 구현 순서

```
[1] TileOccupancyManager: FindAvailableTile에 destination 파라미터 + forward 필터 추가
[2] UnitMovementUseCase: ProcessStep에서 OnUnitMoved 제거 + RegisterOccupancyMove 추가
                        + FindAvailableTile에 destination 전달
[3] UnitView: prevActualTile 추적, 점유 등록 타이밍 변경, 우회 시 break, ReleaseOccupancyIfRegistered
```
