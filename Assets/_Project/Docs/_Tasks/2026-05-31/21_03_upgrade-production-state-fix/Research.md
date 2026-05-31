# Research — 건물 업그레이드 시 생산 상태 처리 오류

건물을 업그레이드할 때 생산 큐의 골드가 환불되지 않고 사라지는 문제와, 설정해 둔 랠리포인트가 초기화되는 문제를 발견했다. 두 문제 모두 업그레이드 시 생산 상태를 교체하는 코드 한 곳에서 비롯된다.

---

## 발견 경위

런타임 진단 로그를 게임에 추가하여 실기 테스트로 확인했다.
- 업그레이드 버튼 클릭 → 동작 정상 확인
- 업그레이드 후 건물 재클릭 → 유닛 생산 패널 정상 동작 확인
- 생산 중 업그레이드 → 골드 미환불 확인
- 업그레이드 후 패널 재오픈 → 랠리포인트 초기화 확인

---

## 문제 1: 생산 중 골드 환불 누락

### 위치
[ProductionTicker.cs](Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs) — `OnBuildingUpgraded()`

### 현재 코드
```
_productionUseCase.UnregisterBarracks(e.OldBuildingId);
_productionUseCase.RegisterBarracks(e.NewBuilding);
```

### 근본 원인
`UnregisterBarracks`는 `_states.Remove(barracksId)` 한 줄만 수행한다.
즉, 현재 생산 중인 유닛(`CurrentProducing`)에 이미 차감된 골드도,
대기 큐(`PendingQueue`)에서 `IsCharged=true`인 항목의 골드도 환불 없이 그냥 삭제된다.

반면 철거(`DemolishBuilding`)는 이 상황을 올바르게 처리한다:
`BuildingPanelBase.OnDemolishButtonClick()` → `BeforeDemolish()` → `CancelAllQueue(barracksId)`.
`CancelAllQueue`는 골드 환불 후 `UnregisterBarracks`까지 포함하여 상태를 정리한다.
업그레이드는 같은 처리를 하지 않고 있었다.

---

## 문제 2: 랠리포인트 초기화

### 위치
[ProductionTicker.cs](Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs) — `OnBuildingUpgraded()`

### 근본 원인
`RegisterBarracks(newBuilding)`은 `new ProductionState(barracksId, team, position)`으로
아무 정보도 없는 빈 상태를 새로 만든다.
기존 `ProductionState`에 저장돼 있던 `RallyPoint(HexCoord?)` 값이 복사되지 않고 사라진다.

---

## 영향 범위

수정 대상 파일: 1개
- [ProductionTicker.cs](Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs)
  - `OnBuildingUpgraded()` 메서드 내부만 변경

참조하는 기존 API (변경 없음):
- `UnitProductionUseCase.CancelAllQueue(barracksId)` — 환불 + UnregisterBarracks 포함
- `UnitProductionUseCase.GetState(barracksId)` — 기존 랠리포인트 조회
- `UnitProductionUseCase.RegisterBarracks(building)` — 신규 상태 등록
- `UnitProductionUseCase.SetRallyPoint(barracksId, target)` — 랠리포인트 복원

---

## 멀티플레이 영향

`ProductionTicker.OnBuildingUpgraded()`는 서버/클라이언트 양쪽에서 동일하게 호출된다.
수정 후에도 동일 경로이므로 별도 분기 불필요.
단, 골드 환불(`CancelAllQueue` 내부의 `_resource.AddGold`)은 서버에서만 실제 값이 바뀌어야 한다.
현재 싱글/멀티 양쪽에서 로컬 골드 상태를 직접 조작하는 구조이므로 이 점은 기존 철거 흐름과 동일하다.
