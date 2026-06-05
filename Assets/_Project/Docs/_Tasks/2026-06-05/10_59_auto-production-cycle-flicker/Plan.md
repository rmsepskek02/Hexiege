# Plan — 자동생산 완료 사이클 슬롯2 깜빡임 버그 수정

작성일: 2026-06-05

---

## 작업 목적 (자연어 설명)

자동생산 중 유닛 한 마리가 완료될 때마다 슬롯2가 1프레임 깜빡이는 버그를 수정한다. 원인은 `CompleteProduction`이 자동 항목을 슬롯2 위치에 올려놓고 UI 이벤트를 발행한 뒤, 실제로 슬롯1 생산을 재시작하는 처리는 다음 프레임으로 미루기 때문이다. 이 두 단계 사이의 1프레임 간격이 깜빡임으로 보인다. 2026-04-19에 등록 경로에 적용한 "즉시 TryStartNext 호출" 방식을 완료 사이클 경로에도 동일하게 적용한다.

---

## GameSystemRules 근거

- **생산 패널 UI 규칙 6 (슬롯 구성)**: 슬롯0 생산 중, 슬롯1·2 대기. 자동생산 1종 단독 시 슬롯1만 생산 중이어야 하며 슬롯2는 비어있어야 한다.
- **생산 패널 UI 규칙 10 (자동 항목 골드 차감 시점)**: 슬롯1 또는 슬롯2에 **표시되는 시점**에 골드를 차감한다. 현재 버그는 이 규칙이 올바르게 적용되더라도, UI 표시 타이밍이 TryStartNext 호출보다 앞서 발행되어 발생한다.

---

## 수정 방법

### 수정 파일

`Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs`

### 수정 위치: `CompleteProduction` 메서드 (L668)

아래 3가지를 변경한다.

#### 변경 전 (L703-710)

```csharp
// PendingQueue[0]/[1] 중 IsCharged=false 자동 항목이 새로 슬롯에 올라왔다면 지금 차감.
ChargeVisibleSlots(state);

// 이벤트 발행 (ProductionTicker가 랠리포인트 이동 처리)
GameEvents.OnUnitProduced.OnNext(
    new UnitProducedEvent(unit, state.RallyPoint));
GameEvents.OnProductionQueueChanged.OnNext(
    new ProductionQueueChangedEvent(state.BarracksId));
```

#### 변경 후

```csharp
// [2026-06-05 깜빡임 수정] AddNewAutoSlot의 2026-04-19 수정과 동일한 패턴.
// CompleteProduction 직후 TryStartNext를 즉시 호출하여 같은 프레임 안에 슬롯 상태를 정착시킨다.
// ChargeVisibleSlots는 TryStartNext 내부에서 호출되므로 여기서 제거.
// OnProductionQueueChanged도 TryStartNext 내부에서 발행되므로 여기서 직접 발행 제거.
GameEvents.OnUnitProduced.OnNext(
    new UnitProducedEvent(unit, state.RallyPoint));

TryStartNext(state);

// TryStartNext가 아무것도 시작하지 않은 경우(큐 비어있음 + 자동 모드 아님)에는
// 내부에서 이벤트를 발행하지 않으므로 여기서 수동으로 발행해 UI를 갱신한다.
if (!state.CurrentProducing.HasValue)
{
    GameEvents.OnProductionQueueChanged.OnNext(
        new ProductionQueueChangedEvent(state.BarracksId));
}
```

---

## 수정 근거

`TryStartNext`는 내부에서 이미:
1. `ChargeVisibleSlots(state)` 호출 (L573, L637)
2. `GameEvents.OnProductionQueueChanged.OnNext(...)` 발행 (L577, L641)

을 처리하므로, `CompleteProduction`에서 이 두 가지를 제거하고 `TryStartNext`를 즉시 호출하면 UI는 최종 정착된 상태로 한 번만 갱신된다.

---

## 케이스별 동작 검증

| 케이스 | TryStartNext 결과 | fallback 이벤트 필요 여부 |
|--------|-----------------|--------------------------|
| 자동 1종 생산 완료 (AutoTypes 유지) | PendingQueue[0] → CurrentProducing 설정 + 이벤트 발행 | 불필요 |
| 자동 취소 후 마지막 생산 완료 | 큐 비어있음 + AutoMode=false → 아무것도 안 함 | **필요** (fallback 발행) |
| 수동 생산 완료 + 대기 항목 있음 | PendingQueue[0] → CurrentProducing 설정 + 이벤트 발행 | 불필요 |
| 수동 생산 완료 + 큐 비어있음 | 큐 비어있음 + AutoMode=false → 아무것도 안 함 | **필요** (fallback 발행) |
| 자동 생산 완료 + 자원 부족 | CancelAutoTypeIfNeeded + 이벤트 발행 후 return | CurrentProducing=null이지만 이미 이벤트 발행됨 (fallback 중복 발행, 기능상 무해) |

---

## 영향 범위

| 경로 | 영향 |
|------|------|
| 자동생산 완료 후 재순환 | **수정 대상** — 슬롯2 깜빡임 제거 |
| 수동생산 완료 후 다음 항목 시작 | 동일하게 개선됨 (기존 동작과 동일, 이벤트 발행 경로만 변경) |
| 수동생산 완료 후 큐 비어있음 | fallback 이벤트로 기존과 동일하게 UI 갱신 |
| 자동 모드 취소 후 마지막 생산 완료 | fallback 이벤트로 기존과 동일하게 UI 갱신 |

---

## 위험 요소

- **자원 부족으로 자동 취소 시 이벤트 이중 발행**: `TryStartNext`가 내부에서 이미 `OnProductionQueueChanged`를 발행하고 fallback도 발행되어 같은 프레임에 2회 발행될 수 있다. UI는 멱등적으로 처리되므로 기능 버그는 아니며, 시각적으로도 문제없다.
- `TryStartNext`는 기존에 `Tick()`에서만 호출됐으나, `AddNewAutoSlot`(L280)에서 이미 직접 호출하는 선례가 있어 구조적 문제 없음.

---

## 수정 파일 전체 목록

| 파일 | 수정 위치 | 내용 |
|------|----------|------|
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | `CompleteProduction` L703-710 | ChargeVisibleSlots 제거, OnProductionQueueChanged 직접 발행 제거, TryStartNext 즉시 호출 + fallback 추가 |
