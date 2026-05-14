# Research: 건물 배치 시 중립 광산 숨기기

## 작업 배경

건물이 배치된 타일에서 중립 광산(GoldMineTile) 3D 오브젝트가 그대로 남아있어 시각적으로 겹쳐 보이는 문제.  
수정 필요한 시나리오:
1. 게임 시작 시 — 초기 제공 채굴소 위치에 중립 광산이 겹쳐있음
2. 플레이어가 중립 광산 타일에 건물을 건설했을 때 광산 오브젝트가 남아있음

---

## 관련 파일 목록

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 게임 시작 시 광산 배치 + 초기 채굴소 배치 (PlaceGoldMines, L693~744) |
| `Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs` | 광산 3D 오브젝트 생성 및 관리 (RenderGoldMines, L147~171) |
| `Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs` | 건물 배치 로직 (PlaceBuildingInternal L145~173, PlaceMiningPostDirect L88~95) |
| `Assets/_Project/Scripts/Application/Events/GameEvents.cs` | 이벤트 테이블 (OnBuildingPlaced L401) |
| `Assets/_Project/Prefabs/Buildings/GoldMineTile.prefab` | 중립 광산 3D 프리팹 |

---

## 현재 동작 분석

### 광산 배치 흐름 (GameBootstrapper.LoadMap)

```
1. PlaceCastles()         — Blue/Red Castle 배치
2. PlaceGoldMines()       — 광산 타일 속성 설정 (HasGoldMine=true, IsWalkable=false)
                             + PlaceMiningPostDirect()로 초기 채굴소 자동 배치
3. RenderGoldMines()      — GoldMineTile.prefab 인스턴스화 → 3D 오브젝트 생성
4. OnGameStarted 발행     — 모든 시스템 초기화 완료
```

**문제점:** 3단계(RenderGoldMines)에서 광산 오브젝트가 생성된 후,
2단계에서 이미 채굴소가 배치된 자리임에도 광산 오브젝트를 숨기지 않음.

### 건물 배치 흐름 (BuildingPlacementUseCase.PlaceBuildingInternal)

```
1. tile.IsWalkable = false 설정
2. 소유권 설정 (SetOwner)
3. 인접 타일 소유권 확장
4. OnBuildingPlaced 이벤트 발행 → BuildingFactory가 프리팹 생성
```

**문제점:** 건물 배치 시 광산 오브젝트 숨김 처리 없음.  
HasGoldMine이 true인 타일에 건물이 배치돼도 광산 오브젝트는 계속 표시됨.

### HexGridRenderer의 광산 오브젝트 관리

```csharp
// RenderGoldMines() (L147~171)
// _goldMineObjects 리스트에 GameObject를 저장하고 있으나,
// 이후 개별 오브젝트 숨김/표시 처리 메서드가 없음
```

`_goldMineObjects`는 List로 관리되고 있으나 HexCoord → GameObject 매핑이 없어서  
특정 좌표의 광산 오브젝트를 찾아서 숨기는 기능이 현재 구현되어 있지 않음.

---

## 영향 범위

### 수정이 필요한 곳
1. **HexGridRenderer** — `_goldMineObjects`를 `Dictionary<HexCoord, GameObject>`로 변경하여 좌표 기반 접근 지원 + 숨김 메서드 추가
2. **BuildingPlacementUseCase** — 건물 배치 시 광산 오브젝트 숨김 처리 호출
3. **GameBootstrapper** — 초기 채굴소 배치 후(RenderGoldMines 이후) 해당 위치 광산 숨김 처리

### 수정 불필요한 곳
- HexTile.HasGoldMine — 변경 불필요 (타일 속성은 유지)
- BuildingData, GameEvents — 변경 불필요
- 네트워크 레이어 (NGO RPC) — 광산 오브젝트는 Presentation 레이어 표현이므로 네트워크 동기화 불필요

---

## 아키텍처 제약 확인

- `HexGridRenderer`는 Presentation 레이어 → Application 레이어(BuildingPlacementUseCase)와 직접 참조 불가
- 따라서 `GameEvents.OnBuildingPlaced` 이벤트를 구독하여 간접 처리하는 방식이 적합
- `HexGridRenderer`가 `GameEvents.OnBuildingPlaced` 구독 → 해당 위치에 광산 오브젝트가 있으면 숨김

---

## 발견된 부가 이슈

- 채굴소가 파괴(RemoveBuilding)됐을 때 광산 오브젝트를 다시 표시해야 하는지 여부는 현재 기획서에 명시 없음  
  → Plan.md에서 scope 결정 필요
