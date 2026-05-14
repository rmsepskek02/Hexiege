# Testcase: 건물 배치/파괴 시 중립 광산 표시 제어

## TC 목록

---

### TC-1: SINGLE-게임 시작 시 초기 채굴소 위치 광산 오브젝트 숨김

**전제:** 싱글플레이 게임을 시작한다.

**동작:**
1. 게임을 시작하여 맵이 로드된다.
2. Blue/Red 팀의 초기 채굴소가 배치된 금광 타일을 확인한다.

**기댓값:**
- 초기 채굴소가 배치된 금광 타일에 중립 광산 오브젝트가 보이지 않는다.
- 채굴소 건물만 표시된다.

**결과:** PASS

---

### TC-2: SINGLE-중립 금광 타일에 채굴소 건설 시 광산 오브젝트 숨김

**전제:** 싱글플레이 게임이 실행 중이다. 맵 중앙의 중립 금광 타일이 표시된다.

**동작:**
1. 중립 금광 타일 위에 채굴소를 건설한다.

**기댓값:**
- 건설 직후 해당 타일의 중립 광산 오브젝트가 사라진다.
- 채굴소 건물만 표시된다.

**결과:** PASS

---

### TC-3: SINGLE-채굴소 파괴 시 광산 오브젝트 재표시

**전제:** 싱글플레이 게임에서 채굴소가 건설된 금광 타일이 있다.

**동작:**
1. 유닛이 채굴소를 공격하여 파괴한다.

**기댓값:**
- 채굴소 파괴 후 해당 타일에 중립 광산 오브젝트가 다시 나타난다.

**결과:** PASS

---

### TC-4: SINGLE-채굴소 파괴 시 타일 소유권 중립 복원

**전제:** 싱글플레이 게임에서 채굴소가 건설된 금광 타일이 있다 (Blue 또는 Red 팀 색상으로 표시).

**동작:**
1. 유닛이 채굴소를 공격하여 파괴한다.

**기댓값:**
- 채굴소 파괴 후 해당 타일의 색상이 중립(회색/기본) 색상으로 변경된다.
- Blue/Red 팀 색상이 유지되지 않는다.

**결과:** PASS

---

## QA 섹션 (정적 분석)

### 분석 대상 파일
- `HexGridRenderer.cs` — 광산 오브젝트 숨김/표시
- `BuildingPlacementUseCase.cs` — 타일 소유권 중립 복원

### 구현 검토

**초기 숨김 판별 로직:**
- `RenderGoldMines()` 내부에서 `tile.Owner != TeamId.Neutral` 조건으로 판별
- `PlaceGoldMines()` → `PlaceMiningPostDirect()` → `PlaceBuildingInternal()` 에서 타일 Owner가 팀으로 설정됨
- `RenderGoldMines()`는 그 이후에 호출되므로 조건 성립 → PASS

**이벤트 구독 방식:**
- `OnBuildingPlaced` 구독: 모든 건물 배치 시 발행되므로 MiningPost가 아닌 건물에도 `HideGoldMine()`이 호출됨
- `_goldMineObjects`에 해당 좌표 키가 없으면 아무 동작 안 함 → 안전 (PASS)

**채굴소 파괴 시 재표시:**
- `OnEntityDied`에서 `e.Entity is BuildingData building && building.Type == BuildingType.MiningPost` 조건으로 필터링
- 유닛 사망 이벤트와 혼용되지 않음 → PASS

**타일 소유권 복원:**
- `RemoveBuilding()` 내부에서 `building.Type == BuildingType.MiningPost` 조건으로 분기
- `_grid.SetOwner(building.Position, TeamId.Neutral)` + `GameEvents.OnTileOwnerChanged` 발행
- 싱글(`UnitCombatUseCase`) / 멀티(`NetworkCombatController`) 모두 `RemoveBuilding()`을 거침 → 단일 수정으로 양쪽 커버 (PASS)

**멀티플레이 검증:** 에이전트 실기 불가 — 사용자 확인 필요
