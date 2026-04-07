# Plan: 건물 배치/파괴 시 중립 광산 표시 제어

## 작업 범위 결정

### 채굴소 파괴 시 광산 오브젝트 재표시 여부
- **Scope IN** (이번 작업에 포함)
- 채굴소가 파괴되면 해당 위치의 광산 오브젝트를 다시 표시

### 재표시 조건
- `OnEntityDied` 이벤트에서 `IDamageable`을 `BuildingData`로 캐스팅
- `BuildingType.MiningPost`이면서 해당 타일 `HasGoldMine == true`인 경우에만 재표시
  - Castle, Barracks가 금광 타일에 배치되는 경우는 없으므로 사실상 항상 재표시지만, 안전을 위해 조건 확인

---

## 구현 접근법

### 핵심 아이디어
`HexGridRenderer`가 `GameEvents.OnBuildingPlaced` 이벤트를 구독하여,  
건물이 배치된 좌표에 광산 오브젝트가 있으면 숨긴다.

이 방식을 선택한 이유:
- `HexGridRenderer`는 Presentation 레이어 → Application 레이어(UseCase) 직접 참조 불가
- `GameEvents`는 레이어 간 통신 전용 채널 → 아키텍처 제약 준수
- 기존 구독 패턴(`Subscribe(...).AddTo(this)`)과 동일한 방식 → 코드 일관성 유지

### 초기 상태 처리 (게임 시작 시 겹침)
`GameBootstrapper.LoadMap()` 흐름:
```
PlaceGoldMines()       ← 초기 채굴소 배치 (OnBuildingPlaced 발행)
RenderGoldMines()      ← 광산 오브젝트 생성
```

문제: 채굴소 배치(이벤트 발행) 시점에 광산 오브젝트가 아직 생성되지 않아 구독 핸들러가 숨길 오브젝트가 없음.

**해결:** `RenderGoldMines()` 내부에서 오브젝트 생성 직후,  
해당 좌표에 이미 건물이 있는지 `HexGrid`를 통해 확인하여 바로 숨김.

---

## 파일별 변경 내용

### 1. `HexGridRenderer.cs`

#### 변경 1: `_goldMineObjects` 타입 변경
```
Before: private readonly List<GameObject> _goldMineObjects
After:  private readonly Dictionary<HexCoord, GameObject> _goldMineObjects
```
좌표로 특정 광산 오브젝트를 찾기 위해 List → Dictionary 변경.

#### 변경 2: `RenderGoldMines()` 수정
- 오브젝트 생성 후 `_goldMineObjects[kvp.Key] = mineObj` 로 저장 (좌표 키)
- 오브젝트 생성 직후, 해당 타일에 건물이 있으면 즉시 숨김  
  → `grid.GetTile(coord)` 로 타일 조회 → `tile.Building != null` 확인  
  (또는 HexGrid에 건물 정보가 없다면 다른 방법 확인 필요 → 아래 참고)

#### 변경 3: `HideGoldMine(HexCoord coord)` 메서드 추가
```csharp
// 특정 좌표의 광산 오브젝트를 숨기는 메서드
public void HideGoldMine(HexCoord coord)
```

#### 변경 4: `ShowGoldMine(HexCoord coord)` 메서드 추가
```csharp
// 특정 좌표의 광산 오브젝트를 다시 표시하는 메서드
public void ShowGoldMine(HexCoord coord)
```

#### 변경 5: `SubscribeEvents()` 메서드 추가 및 구독 등록
```csharp
// OnBuildingPlaced 이벤트 구독
// 건물이 배치된 좌표에 광산 오브젝트가 있으면 숨김
GameEvents.OnBuildingPlaced
    .Subscribe(e => HideGoldMine(e.Building.Position))
    .AddTo(this);

// OnEntityDied 이벤트 구독
// 채굴소가 파괴된 경우, 해당 좌표의 광산 오브젝트를 다시 표시
GameEvents.OnEntityDied
    .Subscribe(e =>
    {
        if (e.Entity is BuildingData building && building.Type == BuildingType.MiningPost)
            ShowGoldMine(building.Position);
    })
    .AddTo(this);
```
`SubscribeEvents()`는 `Awake()` 또는 기존 초기화 지점에서 호출.

#### 변경 6: `ClearGrid()` 수정
```
Before: _goldMineObjects.Clear()
After:  _goldMineObjects.Clear()  (Dictionary도 동일하게 Clear 가능, 변경 없음)
```

---

### 2. `BuildingPlacementUseCase.cs` ← 추가 수정

#### 변경: `RemoveBuilding()` 수정 — MiningPost 파괴 시 타일 Owner Neutral 복원
현재 `RemoveBuilding()`은 `IsWalkable`만 복구하고 Owner는 그대로 둠.  
MiningPost 파괴 시 타일이 시각적으로 팀 색상을 유지하는 문제가 있음.

```
추가 로직 (building.Type == BuildingType.MiningPost인 경우):
  _grid.SetOwner(building.Position, TeamId.Neutral)
  GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(building.Position, TeamId.Neutral))
```

**호출 경로 확인** — 싱글/멀티 모두 이 메서드를 거침:
- 싱글플레이: `UnitCombatUseCase` → `RemoveBuilding()`
- 멀티플레이: `NetworkCombatController.EntityDiedClientRpc()` → `RemoveBuilding()` → `OnEntityDied` 재발행

네트워크 추가 처리 불필요 — `OnTileOwnerChanged`는 Presentation 레이어 이벤트이므로 각 클라이언트에서 독립 처리됨.

### 3. `GameBootstrapper.cs`

#### 변경 없음
초기 채굴소 배치(PlaceMiningPostDirect)는 이미 `PlaceGoldMines()`에서 수행되며,  
이후 `RenderGoldMines()` 내부에서 건물 존재 여부 확인으로 처리하므로 추가 수정 불필요.

---

## 확인 필요 사항 (Research 단계에서 미확인)

### HexGrid에서 특정 좌표의 건물 존재 여부 확인 방법
`BuildingPlacedEvent`에 `BuildingData.Position(HexCoord)`가 포함되어 있어  
이벤트 구독 시는 문제 없음.

`RenderGoldMines()` 내 초기 상태 확인을 위해:
- `HexGrid.GetTile(coord).HasGoldMine == true` + 건물이 있는지 확인 필요
- game-programmer 에이전트가 HexGrid API 확인 후 적절한 방법 선택

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| `_goldMineObjects` 변경 시 `ClearGrid()`에서 오류 | Dictionary.Clear()도 List와 동일하게 동작 — 문제 없음 |
| 멀티플레이에서 Red팀 좌표 반전 | ViewConverter.ToView()는 이미 RenderGoldMines()에서 처리 중 — 영향 없음 |
| `SubscribeEvents()` 중복 호출 | Awake()에서 1회만 호출 — 문제 없음 |
| 이벤트 구독 시점 (광산 오브젝트 생성 전 PlaceMiningPostDirect 호출) | RenderGoldMines() 내부에서 초기 건물 상태 확인으로 별도 처리 |
| `OnEntityDied`에서 유닛 사망 이벤트도 함께 발행됨 | `e.Entity is BuildingData` 캐스팅으로 건물만 필터링 — 문제 없음 |
| 채굴소 파괴 후 광산 오브젝트가 없는 경우 (이미 ClearGrid로 제거됨 등) | `ShowGoldMine()` 내부에서 null 체크 후 처리 — 문제 없음 |

---

## 구현 순서

1. `HexGridRenderer._goldMineObjects` List → Dictionary 변경
2. `RenderGoldMines()` 수정 — Dictionary 저장 + 초기 건물 유무 확인 후 숨김
3. `HideGoldMine(HexCoord coord)` 메서드 추가
4. `ShowGoldMine(HexCoord coord)` 메서드 추가
5. `SubscribeEvents()` 메서드 추가 (`OnBuildingPlaced` + `OnEntityDied` 구독) 및 `Awake()`에서 호출
6. 컴파일 확인

---

## 담당 에이전트

**game-programmer** — 코드 구현
