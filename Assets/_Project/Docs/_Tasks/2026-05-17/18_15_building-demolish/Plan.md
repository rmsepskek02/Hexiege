# Plan — 건물 철거 시스템

## 이 작업에서 무엇을 만드는가?

플레이어가 건설한 건물을 직접 철거하는 기능을 구현한다.
건물을 클릭하면 열리는 팝업에 철거 버튼을 추가하고, 버튼 탭 즉시 건물이 제거된다.
철거 시 건설 비용의 50%가 골드로 돌아오고, 생산 큐에 이미 골드가 차감된 항목도 전액 환불된다.
채굴소처럼 현재 팝업이 없는 건물을 위해 새 팝업도 함께 제작한다.

규칙 근거: `GameSystemRules.md — 건물 철거 시스템 규칙 1~6`

---

## 구현 순서

```
[1] UnitProductionUseCase — CancelAllQueue() 신규
      ↓
[2] ProductionPanelUI — 철거 버튼 추가 (생산 건물)
      ↓
[3] MiningPostPanelUI — 채굴소용 팝업 신규 제작
      ↓
[4] InputHandler — 채굴소 클릭 분기 추가
      ↓
[5] NetworkBuildingController — RequestDemolishServerRpc 추가
      ↓
[6] GameBootstrapper — 새 의존성 주입
```

---

## 파일별 변경 내용

### [1] `UnitProductionUseCase.cs` — `CancelAllQueue()` 신규 추가

**목적:** 배럭 철거 시 생산 큐 전체를 한 번에 취소하고, 이미 차감된 골드를 전액 환불한다.

**처리 순서:**
1. `ClearRallyPoint(barracksId)` 호출 → 랠리포인트 마커 제거 이벤트 발행 (UnregisterBarracks 전에 호출해야 state에 접근 가능)
2. `state.CurrentProducing`이 있으면 해당 유닛 비용 전액 환불 (`AddGold`)
3. `state.PendingQueue` 순회 → `IsCharged=true` 항목 전액 환불, `IsCharged=false` 항목은 환불 없이 제거
4. `state.PendingQueue.Clear()`, `state.AutoTypes.Clear()`, `state.AutoCycleIndex = 0`
5. `CurrentProducing = null`, `CurrentIsAuto = false`, `ElapsedTime = 0`, `RequiredTime = 0`
6. `GameEvents.OnProductionQueueChanged` 발행
7. `UnregisterBarracks(barracksId)` 호출 → ProductionState 제거

**근거:** `GameSystemRules.md — 건물 철거 시스템 규칙 5` (이미 차감된 항목 전액 환불, 미차감 항목 환불 없이 제거)

---

### [2] `ProductionPanelUI.cs` — 철거 버튼 추가

**목적:** 생산 건물 팝업에 철거 버튼을 노출하고 클릭 시 즉시 철거를 실행한다.

**변경 내용:**
- Inspector `[SerializeField] private Button _demolishButton` 추가
- `OnDemolishButtonClicked()` 핸들러 작성:
  - 현재 열려 있는 건물 (`_currentBuilding`) 의 팀과 종족으로 건설 비용 조회
  - 환불 금액 = `BuildingStats.GetGoldCost(type, race) / 2`
  - 싱글: `_production.CancelAllQueue(buildingId)` → `_resource.AddGold(team, refund)` → `_buildingPlacement.RemoveBuilding(buildingId)` 순으로 호출
  - 멀티: `_networkBuildingController.RequestDemolishServerRpc(buildingId)` 호출
  - `Close()` 호출

**추가 의존성:** `ResourceUseCase`, `BuildingPlacementUseCase` 주입 (이미 같은 Presenter 공간에 주입되어 있을 가능성 있음 — Bootstrpper 확인 필요)

**근거:** `GameSystemRules.md — 건물 철거 시스템 규칙 2, 3, 4`

---

### [3] 신규 `MiningPostPanelUI.cs` — 채굴소용 팝업 제작

**목적:** 현재 팝업이 없는 채굴소(MiningPost) 클릭 시 표시할 단순 팝업을 제공한다.

**UI 구성:** 건물 이름 텍스트 + 철거 버튼 (최소 구성)

**변경 내용:**
- `Show(BuildingData building, RaceId race)` / `Close()` 메서드 작성
- `IsOpen`, `ClosedFrame` 프로퍼티 (InputHandler 클릭 통과 방지용, ProductionPanelUI 패턴 참조)
- 철거 버튼 클릭 핸들러:
  - 싱글: `_resource.AddGold(refund)` → `_buildingPlacement.RemoveBuilding(buildingId)` 호출
  - 멀티: `_networkBuildingController.RequestDemolishServerRpc(buildingId)` 호출
  - `Close()` 호출
- 배경 클릭 시 닫기 (`SharedBackgroundButton` 패턴 참조)

**근거:** `GameSystemRules.md — 건물 철거 시스템 규칙 2, 3, 4`

---

### [4] `InputHandler.cs` — 채굴소 클릭 분기 추가

**목적:** 채굴소를 클릭했을 때 `MiningPostPanelUI`를 열도록 분기를 추가한다.

**현재 step 2 로직:**
```
건물이 있는 타일 클릭
  → 생산 건물 + 자기 팀 → ProductionPanelUI.Show()
  → 그 외 → 타일 선택만
```

**변경 후 step 2 로직:**
```
건물이 있는 타일 클릭
  → 생산 건물 + 자기 팀 → ProductionPanelUI.Show()
  → MiningPost + 자기 팀 + 살아있음 → MiningPostPanelUI.Show()
  → 그 외 (Castle 등) → 타일 선택만
```

**추가 필드:** `private MiningPostPanelUI _miningPostUI` + `Initialize()` 파라미터 추가

**ClosedFrame 처리:** `InputHandler`의 팝업 닫힘 프레임 체크 분기에 `_miningPostUI.ClosedFrame` 추가

**근거:** `GameSystemRules.md — 건물 철거 시스템 규칙 2` (건물 클릭 시 팝업에 철거 버튼 포함)

---

### [5] `NetworkBuildingController.cs` — `RequestDemolishServerRpc` 추가

**목적:** 멀티플레이에서 클라이언트가 철거를 요청하면 서버에서 검증 후 실행한다.

**변경 내용:**
- `[ServerRpc(RequireOwnership = false)] RequestDemolishServerRpc(int buildingId, ServerRpcParams rpcParams = default)` 추가
- 서버 검증: 요청자가 해당 건물의 팀 소유주인지, Castle이 아닌지, 건물이 존재하는지
- 검증 통과 시:
  - 생산 건물이면 `_production.CancelAllQueue(buildingId)` (서버 도메인)
  - 환불: `_resource.AddGold(team, refund)`
  - `_buildingPlacement.RemoveBuilding(buildingId)`
  - `DemolishBuildingClientRpc(buildingId)` 발행 → 모든 클라이언트 도메인 상태 동기화

**근거:** 기존 `RequestBuildServerRpc`, `RequestUpgradeServerRpc` 패턴과 동일한 구조

---

### [6] `GameBootstrapper.cs` — 의존성 주입 업데이트

**목적:** 새로 추가된 `MiningPostPanelUI`와 `InputHandler` 변경에 맞게 주입 코드를 수정한다.

**변경 내용:**
- `MiningPostPanelUI` 필드 참조 추가
- `InputHandler.Initialize()` 호출 시 `_miningPostPanelUI` 추가 전달
- `MiningPostPanelUI.Initialize()` 에 필요한 UseCase 주입

---

## 위험 요소 및 주의사항

| 항목 | 내용 |
|------|------|
| **랠리포인트 마커 순서** | `CancelAllQueue` 내에서 `ClearRallyPoint` 를 반드시 `UnregisterBarracks` 이전에 호출해야 한다. 이후엔 state가 없어 이벤트 발행 불가. |
| **RaceId 조회** | `BuildingData`에 RaceId가 저장되지 않으므로 철거 시점에 `GameRaceContext.GetRace(team)` 으로 조회해야 한다. |
| **ClosedFrame 처리 누락** | `InputHandler`에 `MiningPostPanelUI.ClosedFrame` 체크를 추가하지 않으면 팝업 닫힌 프레임에 클릭이 통과되는 버그가 발생한다. |
| **멀티플레이 클라이언트 동기화** | 서버 처리 완료 후 `DemolishBuildingClientRpc`로 모든 클라이언트가 `RemoveBuilding`을 동일하게 적용해야 한다. 이미 `BuildingPlaced`, `BuildingUpgraded` 패턴이 있으므로 동일하게 따른다. |
| **Castle 클릭 시 팝업 없음** | Castle은 철거 불가이므로 클릭 시 아무 팝업도 표시하지 않는다. 현재 `InputHandler`의 타일 선택만 수행하는 기존 동작을 유지한다. |
