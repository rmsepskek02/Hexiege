# Plan — 건물 철거 시스템

## 이 작업에서 무엇을 만드는가?

플레이어가 건설한 건물을 직접 철거하는 기능을 구현한다.
건물을 클릭하면 열리는 팝업에 철거 버튼을 추가하고, 버튼 탭 즉시 건물이 제거된다.
철거 시 건설 비용의 50%가 골드로 돌아오고, 생산 큐에 이미 골드가 차감된 항목도 전액 환불된다.

> **⚠️ 작업 범위 조정:** 이번에는 이미 팝업 UI가 있는 **생산 건물** 철거 로직만 구현한다.
> 채굴소(MiningPost) UI 제작([3] MiningPostPanelUI, [4] InputHandler 분기)은 **별도 작업으로 연기**한다.

규칙 근거: `GameSystemRules.md — 건물 철거 시스템 규칙 1~6`

---

## 구현 순서

```
[1] UnitProductionUseCase — CancelAllQueue() 신규           ✅ 완료
      ↓
[2] ProductionPanelUI — OnDemolishButtonClick() 로직 구현  ✅ 완료
      ↓
[3] MiningPostPanelUI — 채굴소용 팝업 신규 제작             ← 연기 (UI 제작 별도)
      ↓
[4] InputHandler — 채굴소 클릭 분기 추가                   ← 연기 ([3] 완료 후)
      ↓
[5] NetworkBuildingController — RequestDemolishServerRpc 추가  ✅ 완료
      ↓
[6] GameBootstrapper — 의존성 주입 업데이트                ✅ 완료 (변경 불필요 확인)
      ↓
[7] BuildingFactory — OnEntityDied 구독 추가 (건물 GO 파괴) ✅ 완료
      ↓
[8] BuildingView.cs 삭제                                   ✅ 완료
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

### [2] `ProductionPanelUI.cs` — `OnDemolishButtonClick()` 로직 구현

> **⚠️ 이전 작업 반영:** UI 껍데기는 이미 완료된 상태다.
> `_demolishButton`, `_demolishRefundText` 필드, 버튼 이벤트 연결, `UpdateDemolishRefund()` 구현이
> 이미 코드에 존재한다. 추가로 필드를 선언하거나 `Show()`를 수정할 필요 없다.

**현재 상태:**
- `_demolishButton` 필드: 존재 (라인 143)
- `_demolishRefundText` 필드: 존재 (라인 155)
- 버튼 이벤트 연결: `Initialize()`에서 완료
- `UpdateDemolishRefund(race)`: `Show()`에서 호출 + 완전 구현됨
- `OnDemolishButtonClick()`: **스텁만 존재** — `Debug.Log` 한 줄뿐 (라인 657~661)

**이번에 구현할 내용:**
- `OnDemolishButtonClick()` 메서드 본문 작성:
  - `_currentBarracks`가 null이면 즉시 리턴
  - 종족 조회: `RaceId race = (_currentBarracks.Team == TeamId.Blue) ? GameRaceContext.BlueRace : GameRaceContext.RedRace`
  - 싱글: `_production.CancelAllQueue(barracksId)` → `_resource.AddGold(team, refund)` → `_buildingPlacement.RemoveBuilding(barracksId)` 순으로 호출
  - 멀티: `_networkBuildingController.RequestDemolishServerRpc(barracksId)` 호출
  - `Close()` 호출

**추가 의존성 확인 필요:** `ResourceUseCase`, `BuildingPlacementUseCase`, `NetworkBuildingController` 주입 여부 — Bootstrapper 확인 후 누락된 것만 추가

**근거:** `GameSystemRules.md — 건물 철거 시스템 규칙 2, 3, 4`

---

### [3] 신규 `MiningPostPanelUI.cs` — 채굴소용 팝업 제작 ⏸ 연기

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

### [4] `InputHandler.cs` — 채굴소 클릭 분기 추가 ⏸ 연기

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

### [5] `NetworkBuildingController.cs` — `RequestDemolishServerRpc` 추가 ✅ 완료

**목적:** 멀티플레이에서 클라이언트가 철거를 요청하면 서버에서 검증 후 실행한다.

**변경 내용:**
- `[ServerRpc(RequireOwnership = false)] RequestDemolishServerRpc(int buildingId, ServerRpcParams rpcParams = default)` 추가
- 서버 검증: 요청자가 해당 건물의 팀 소유주인지, Castle이 아닌지, 건물이 존재하는지
- 검증 통과 시:
  - 생산 건물이면 `_production.CancelAllQueue(buildingId)` (서버 도메인)
  - 환불: `_resource.AddGold(team, refund)`
  - `_buildingPlacement.DemolishBuilding(buildingId)` (OnEntityDied 발행 + RemoveBuilding)
  - `DemolishBuildingClientRpc(buildingId)` 발행 → 모든 클라이언트 도메인 상태 동기화

**근거:** 기존 `RequestBuildServerRpc`, `RequestUpgradeServerRpc` 패턴과 동일한 구조

---

### [6] `GameBootstrapper.cs` — 의존성 주입 업데이트 ✅ 완료 (변경 불필요)

모든 의존성이 이미 주입되어 있음을 확인. 추가 작업 없음.

---

### [7] `BuildingFactory.cs` — `OnEntityDied` 구독 추가

**목적:** 건물 프리팹에 `BuildingView` 컴포넌트가 없어 `OnEntityDied` 발생 시 GO가 파괴되지 않는 버그를 수정한다.

**버그 원인:**
- 건물 프리팹 구조: 루트 GO(Transform만) + 자식 GO(MeshFilter/MeshRenderer)
- `BuildingView` 컴포넌트가 어떤 프리팹에도 부착되어 있지 않음
- 따라서 `BuildingView.Initialize()` 미호출 → `OnEntityDied` 구독 없음 → GO 파괴 불발

**선택한 해결 방법 — B 방식:**
- `BuildingFactory`가 `_buildingObjects` 딕셔너리(Id→GO)를 이미 관리하고 있으므로,
  `OnEntityDied` 이벤트 구독 1개를 추가해 해당 딕셔너리로 GO를 직접 파괴한다.
- 기존 `BuildingView` 방식(건물 수만큼 N개 구독) 대비 **구독 1개 + O(1) 딕셔너리 조회**로 성능 우위.
- 철거뿐 아니라 전투 HP 소진 파괴 경로도 동일하게 혜택을 받는다.

**변경 내용 (`BuildingFactory.Awake()`):**
```csharp
GameEvents.OnEntityDied
    .Subscribe(e =>
    {
        // 유닛 사망은 무시하고 건물만 처리
        if (e.Entity is not BuildingData building) return;
        if (_buildingObjects.TryGetValue(building.Id, out var go) && go != null)
        {
            _buildingObjects.Remove(building.Id);
            Destroy(go);
        }
    })
    .AddTo(this);
```

---

### [8] `BuildingView.cs` 삭제

**목적:** [7]로 GO 파괴 책임이 `BuildingFactory`로 이전됐으므로 `BuildingView`는 불필요한 코드가 된다.

**삭제 조건 확인:**
- `MiningEffectView`가 `GetComponent<BuildingView>()` 사용 → `MiningEffectView` 현재 **미사용**이므로 문제 없음
- `BuildingFactory`에서 `GetComponent<Presentation.BuildingView>()` 호출 코드 → 함께 제거 필요

**변경 내용:**
- `BuildingView.cs` 파일 삭제
- `BuildingView.cs.meta` 파일 삭제
- `BuildingFactory.cs`의 `view.Initialize(data)` 블록 제거 (2곳: CreateBuildingObject, UpgradeBuildingObject)

---

## 위험 요소 및 주의사항

| 항목 | 내용 |
|------|------|
| **랠리포인트 마커 순서** | `CancelAllQueue` 내에서 `ClearRallyPoint` 를 반드시 `UnregisterBarracks` 이전에 호출해야 한다. 이후엔 state가 없어 이벤트 발행 불가. |
| **RaceId 조회** | `BuildingData`에 RaceId가 저장되지 않으므로 철거 시점에 `GameRaceContext.GetRace(team)` 으로 조회해야 한다. |
| **ClosedFrame 처리 누락** | [3][4] 연기됨. `MiningPostPanelUI` 제작 시 반드시 `InputHandler`에 ClosedFrame 체크를 추가해야 한다. |
| **멀티플레이 클라이언트 동기화** | 서버 처리 완료 후 `DemolishBuildingClientRpc`로 모든 클라이언트가 `RemoveBuilding`을 동일하게 적용해야 한다. 이미 `BuildingPlaced`, `BuildingUpgraded` 패턴이 있으므로 동일하게 따른다. |
| **Castle 클릭 시 팝업 없음** | Castle은 철거 불가이므로 클릭 시 아무 팝업도 표시하지 않는다. 현재 `InputHandler`의 타일 선택만 수행하는 기존 동작을 유지한다. |
| **BuildingFactory OnEntityDied — 유닛 사망 이벤트 필터링** | `OnEntityDied`는 유닛/건물 공용 이벤트다. `BuildingFactory` 구독 시 `e.Entity is not BuildingData` 조건으로 유닛 사망을 반드시 걸러야 한다. |
| **MiningEffectView 재사용 시** | 향후 `MiningEffectView`를 활성화하면 `BuildingView` 없이 동작하지 않는다. 그 시점에 `MiningEffectView`의 `BuildingData` 조회 방식을 재설계해야 한다. |
