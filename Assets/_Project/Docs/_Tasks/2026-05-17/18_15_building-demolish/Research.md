# Research — 건물 철거 시스템

## 이 작업은 무엇인가?

플레이어가 자신이 건설한 건물을 직접 철거할 수 있는 기능을 추가한다.
Castle(본기지)을 제외한 모든 건물이 대상이며, 철거 시 건설 비용의 50%가 즉시 환불된다.
생산 건물을 철거할 때는 생산 큐에 있는 항목 중 이미 골드가 차감된 것들도 전액 환불된다.
건물 클릭 시 열리는 팝업에 철거 버튼을 추가하여, 버튼 탭 즉시(확인 팝업 없이) 철거가 실행된다.

규칙 근거: `GameSystemRules.md — 건물 철거 시스템 규칙 1~6`

---

## 현재 코드 구조 파악

### Application Layer

#### `BuildingPlacementUseCase.cs`

- **`RemoveBuilding(int buildingId)`** — 건물을 `_buildings` 딕셔너리에서 제거하고 타일 상태를 복구하는 메서드가 이미 존재한다. 현재는 적 유닛에게 HP가 0이 됐을 때 내부적으로 호출되는 경로이다.
  - 금광 타일(`HasGoldMine`)은 `IsWalkable`을 복구하지 않는다 (금광 오브젝트가 남아있으므로).
  - MiningPost 제거 시 타일 소유권을 `Neutral`로 복귀 + `GameEvents.OnTileOwnerChanged` 발행.
  - 이 메서드를 철거에서도 재사용할 수 있다.

- **`BuildingStats.GetGoldCost(BuildingType, RaceId)`** — 건물 건설 비용 조회 가능. 50% 환불 계산에 활용.

- **`BuildingTypeHelper.IsProductionBuilding(BuildingType)`** — 생산 건물 여부 판단.

- **Castle 여부 판단** — `building.Type == BuildingType.Castle` 로 단순 비교 가능.

#### `UnitProductionUseCase.cs`

- **`CancelQueueAt(int barracksId, int slotIndex)`** — 슬롯 단위 취소 메서드 존재. 단, 슬롯 인덱스 0/1/2를 각각 호출해야 한다.
  - 슬롯0 취소 시 항상 전액 환불 (CurrentProducing은 항상 IsCharged=true).
  - 슬롯1/2 취소 시 IsCharged=true 항목만 환불, false는 환불 없음.
  - 철거 시 "모든 항목 일괄 취소 + 환불"을 처리하는 전용 메서드가 현재 없으므로 신규 추가 필요.

- **`UnregisterBarracks(int barracksId)`** — ProductionState만 딕셔너리에서 제거. 환불 처리가 없으므로 철거 시 직접 호출하면 안 된다. 큐 취소/환불 후 마지막에 호출해야 한다.

- **`PendingQueue` 구조** — 길이가 가변(0~N)이므로 슬롯2 → 슬롯1 → 슬롯0 순으로 취소해야 인덱스 밀림 없이 안전하게 처리된다.

### Presentation Layer

#### `InputHandler.cs`

- `HandleClick()` 판정 순서: 생산 건물 클릭 → `ProductionPanelUI.Show()`, **그 외 건물(Castle, MiningPost 등) 클릭 → 타일 선택만 수행하고 팝업 없음**.
- 따라서 채굴소 등 비생산 건물을 클릭해도 현재는 아무 팝업도 뜨지 않는다.
- 채굴소 클릭 시 새 팝업을 열려면 `InputHandler`도 수정이 필요하다.

#### `ProductionPanelUI.cs`

- 배럭 클릭 시 열리는 생산 패널 UI.
- 유닛 버튼, 큐 슬롯, 업그레이드 버튼 등이 있다.
- 여기에 철거 버튼을 추가하면 생산 건물 철거가 가능해진다.

#### 채굴소(MiningPost) 팝업 — 현재 없음

- 채굴소 클릭 시 표시할 팝업이 존재하지 않는다.
- 철거 버튼을 포함한 단순 정보 팝업을 신규 제작해야 한다.
- `InputHandler`에 채굴소 건물 클릭 분기를 추가해야 한다.

---

## 영향 범위

| 레이어 | 파일 | 작업 유형 |
|--------|------|----------|
| Application | `BuildingPlacementUseCase.cs` | `DemolishBuilding()` 신규 추가 |
| Application | `UnitProductionUseCase.cs` | `CancelAllQueue()` 신규 추가 |
| Presentation | `ProductionPanelUI.cs` | 철거 버튼 추가 |
| Presentation | `InputHandler.cs` | 채굴소 클릭 분기 추가 |
| Presentation | 신규 `MiningPostPanelUI.cs` | 채굴소용 팝업 신규 제작 |
| Infrastructure | `NetworkBuildingController.cs` | `RequestDemolishServerRpc()` 추가 |

---

## 주요 발견 사항 및 주의점

1. **`RemoveBuilding()` 재사용 가능** — 기존 적 유닛 파괴 경로가 이미 타일 복구 + 이벤트 발행을 처리하므로 철거에서도 그대로 재사용할 수 있다.

2. **채굴소 팝업 신규 제작 필요** — 이번 작업의 UI 부분 중 가장 작업량이 많은 부분이다. 단순 팝업이지만 프리팹 제작과 InputHandler 분기 추가가 함께 필요하다.

3. **생산 큐 취소 순서** — `PendingQueue` 길이가 가변이므로 슬롯2 → 슬롯1 → 슬롯0 순서로 `CancelQueueAt`을 호출해야 한다. 또는 PendingQueue를 직접 순회하는 전용 메서드를 만드는 것이 더 안전하다.

4. **랠리포인트 마커 처리** — `UnregisterBarracks()` 가 `ProductionState`를 제거하면 `GameEvents.OnRallyPointChanged(null)` 이 발행되어야 마커가 제거된다. `DemolishBuilding` 흐름 내에서 `ClearRallyPoint` 또는 동등한 이벤트 발행이 필요한지 확인 필요.

5. **멀티플레이 분기** — `NetworkBuildingController`에 `RequestDemolishServerRpc`를 추가해야 한다. 서버에서 건물 소유권(자신의 팀 건물인지) 검증 후 처리해야 한다.

6. **채굴소 수입 중단** — `ResourceUseCase.TickIncome()`이 살아있는 MiningPost 수를 실시간 카운트하므로 `RemoveBuilding()` 호출로 건물이 제거되면 자동으로 수입이 중단된다. 별도 처리 불필요.
