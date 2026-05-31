# Plan — 건물 업그레이드 시 생산 상태 처리 오류 수정

업그레이드 시 생산 큐 골드 환불이 누락되는 문제와 랠리포인트가 초기화되는 문제를 수정한다.
수정 대상은 `ProductionTicker.OnBuildingUpgraded()` 한 곳이며, 기존 API를 조합하는 방식으로 해결한다.

---

## GameSystemRules 근거

- **건물 철거 시스템 규칙 5**: "생산 건물 철거 시, 큐에 있는 항목 중 골드가 이미 차감된 항목은 전액 환불하고 취소한다. 골드가 아직 차감되지 않은 항목은 환불 없이 제거된다."

업그레이드는 기존 건물이 사라지고 새 건물로 교체되는 이벤트이므로, 생산 큐 처리는 철거와 동일한 규칙을 따른다.

---

## 수정 내용

### 수정 파일: [ProductionTicker.cs](Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs)
**메서드**: `OnBuildingUpgraded(BuildingUpgradedEvent e)`

#### 현재 코드

```csharp
private void OnBuildingUpgraded(BuildingUpgradedEvent e)
{
    if (_productionUseCase == null) return;
    if (!BuildingTypeHelper.IsProductionBuilding(e.NewBuilding.Type)) return;

    _productionUseCase.UnregisterBarracks(e.OldBuildingId);
    _productionUseCase.RegisterBarracks(e.NewBuilding);
}
```

#### 수정 후 코드

```csharp
private void OnBuildingUpgraded(BuildingUpgradedEvent e)
{
    if (_productionUseCase == null) return;
    if (!BuildingTypeHelper.IsProductionBuilding(e.NewBuilding.Type)) return;

    // 업그레이드 전 랠리포인트 좌표를 먼저 저장해 둔다.
    // CancelAllQueue가 랠리포인트를 초기화하므로 반드시 그 전에 읽어야 한다.
    HexCoord? savedRallyPoint = _productionUseCase.GetState(e.OldBuildingId)?.RallyPoint;

    // 생산 중이거나 골드가 차감된 대기 항목을 환불하고 기존 상태를 제거한다.
    // (UnregisterBarracks 대신 CancelAllQueue를 사용 — 내부에서 UnregisterBarracks까지 수행)
    // 근거: GameSystemRules.md — 건물 철거 시스템 규칙 5
    _productionUseCase.CancelAllQueue(e.OldBuildingId);

    // 새 건물로 빈 생산 상태를 등록한다.
    _productionUseCase.RegisterBarracks(e.NewBuilding);

    // 저장해 둔 랠리포인트가 있으면 새 건물 상태에 복원한다.
    if (savedRallyPoint.HasValue)
        _productionUseCase.SetRallyPoint(e.NewBuilding.Id, savedRallyPoint.Value);
}
```

---

## 변경 요약

| 항목 | 이전 | 이후 |
|------|------|------|
| 생산 큐 종료 방식 | `UnregisterBarracks` (환불 없음) | `CancelAllQueue` (환불 포함, UnregisterBarracks 내장) |
| 랠리포인트 처리 | 없음 (초기화됨) | 업그레이드 전 저장 → 복원 |
| 수정 파일 수 | — | 1개 |
| 신규 API 추가 | — | 없음 (기존 API 조합) |

---

## 위험 요소

- **`CancelAllQueue`의 `ClearRallyPoint` 호출**: `CancelAllQueue` 내부에서 `ClearRallyPoint(oldId)` 이벤트가 발행된다. 이 이벤트로 기존 마커가 숨겨지지만, 곧이어 `SetRallyPoint`가 새 ID로 복원하므로 문제없다.

- **`OnProductionQueueChanged` 이벤트 타이밍**: `CancelAllQueue` 내부에서 `OnProductionQueueChanged`가 발행된다. 이 시점에 패널은 아직 열려 있을 수 있으나, `UpdateUI()`는 `GetState(oldId)`가 null을 반환하면 즉시 리턴하도록 되어 있어 안전하다.

- **멀티플레이 골드 환불**: 현재 골드 연산이 로컬에서 직접 수행되는 구조이므로 기존 철거 흐름과 동일하게 동작한다. 별도 분기 불필요.
