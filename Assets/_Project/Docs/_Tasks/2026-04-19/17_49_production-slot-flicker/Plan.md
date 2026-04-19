# Plan — 생산 슬롯 깜빡임 버그 수정 (Slot1→Slot0 Flicker)

작성일: 2026-04-19

---

## 목표

큐가 비어있을 때 자동 생산 1종을 등록하면 슬롯1에 순간 표시됐다가 슬롯0으로 이동하는 시각적 버그를 제거.

---

## 원인 요약

`ToggleAutoProduction`에서 큐가 비어있으면 `canShow=false`로 판정 → 아이템이 `PendingQueue[0]`(슬롯1)에 미차감 상태로 추가됨 → 다음 `Tick()`에서 `TryStartNext`가 슬롯0으로 올림 → 1프레임 슬롯1 표시 발생.

---

## 수정 방법

### 수정 파일

`Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs`

### 수정 내용: `ToggleAutoProduction` 내부

`state.PendingQueue.Add(...)` 및 `state.AutoTypes.Add(...)` 이후, `OnProductionQueueChanged` 이벤트 발행 직전에 아래 조건을 추가:

```
큐에 아이템을 추가한 뒤, CurrentProducing이 비어있으면 즉시 TryStartNext(state) 호출.
이 경우 TryStartNext 내부에서 이벤트가 발행되므로, 이후의 OnProductionQueueChanged 발행은 중복 방지를 위해 Early Return.
```

#### 구체적 수정 위치

`ToggleAutoProduction` 하단 (PendingQueue.Add + AutoTypes.Add + NormalizeAutoCycleIndex 이후):

```csharp
// 변경 전
state.PendingQueue.Add(new QueueSlot(type, true, isCharged));
state.AutoTypes.Add(type);
NormalizeAutoCycleIndex(state);

GameEvents.OnProductionQueueChanged.OnNext(
    new ProductionQueueChangedEvent(barracksId));
return true;

// 변경 후
state.PendingQueue.Add(new QueueSlot(type, true, isCharged));
state.AutoTypes.Add(type);
NormalizeAutoCycleIndex(state);

// 큐가 비어있었던 경우(isCharged=false로 방금 추가된 아이템만 존재):
// TryStartNext를 즉시 호출하여 슬롯0에 바로 올림으로써 1프레임 슬롯1 표시 방지.
// TryStartNext 내부에서 OnProductionQueueChanged를 발행하므로 Early Return.
if (!state.CurrentProducing.HasValue)
{
    TryStartNext(state);
    return true;
}

GameEvents.OnProductionQueueChanged.OnNext(
    new ProductionQueueChangedEvent(barracksId));
return true;
```

---

## 영향 범위

| 경로 | 영향 |
|------|------|
| 큐 비어있을 때 자동 1종 등록 | 수정 대상 — 슬롯0에 바로 표시 |
| 큐 비어있을 때 자동 2종 동시 등록(첫 번째) | 동일하게 수정됨 |
| 큐에 이미 항목이 있을 때 자동 등록 | `CurrentProducing.HasValue=true` → 기존 경로 유지, 무변경 |
| 수동 생산 | `ToggleAutoProduction` 미사용 → 무관 |

---

## 위험 요소

- `TryStartNext`는 골드/인구 검증을 포함 → 자원 부족 시 아이템이 PendingQueue에 남아 대기 (기존 동작과 동일)
- `TryStartNext` 내부에서 `ChargeVisibleSlots`가 호출되어 슬롯 차감도 자동 처리됨
- 이벤트 중복 발행 없음 (Early Return으로 처리)

---

## 수정 파일 전체 목록

- `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` (1곳 수정)
