# Research — 생산 슬롯 깜빡임 버그 (Slot1→Slot0 Flicker)

작성일: 2026-04-19

---

## 버그 현상

- **재현 조건**: 생산 큐가 완전히 비어있을 때(슬롯0/1/2 모두 공백) 자동 생산 타입을 1종 등록
- **현상**: 등록 직후 매우 짧은 순간 슬롯1(두 번째 칸)에 아이콘이 표시되었다가 슬롯0(첫 번째 칸)으로 이동
- **간헐적**: 프레임 타이밍에 따라 간헐적으로 보임

---

## 원인 분석

### 흐름 추적

`ToggleAutoProduction` 내부의 `canShow` 판정 로직:

```csharp
// UnitProductionUseCase.cs:246
bool canShow = state.CurrentProducing.HasValue && state.ChargedPendingCount() < 2;
```

**큐가 비어있을 때**:
- `state.CurrentProducing.HasValue = false`
- → `canShow = false`
- → `isCharged = false` (골드 미차감)
- → `state.PendingQueue.Add(new QueueSlot(type, IsAuto=true, IsCharged=false))`
- → 아이템이 `PendingQueue[0]` 위치(슬롯1)에 추가됨
- → UI가 슬롯1에 표시

**다음 Tick**:
- `Tick()` → `TryStartNext()` 호출
- `PendingQueue.Count > 0` → `PendingQueue[0]`를 꺼내 슬롯0으로 올림
- → UI가 슬롯0으로 이동

이 두 단계 사이의 1프레임이 깜빡임으로 보임.

### 자동 2종 등록 시에는 발생하지 않는 이유

- 2번째 타입 등록 시점에는 1번째 타입이 이미 슬롯0(CurrentProducing)에 있음
- `CurrentProducing.HasValue = true` → `canShow` 판정 가능
- 2번째 아이템은 `IsCharged=true`로 슬롯1에 직접 표시됨
- 슬롯1→슬롯0 이동 없음

### 자동 1종이 아닌 경우에도 발생 가능성

동일한 조건(큐 비어있음 + 첫 번째 등록)이면 자동 2종 등록 시 첫 번째 등록에서도 동일하게 발생.
실질적으로는 **큐 비어있을 때 첫 번째 자동 타입 등록** 시에만 발생.

---

## 관련 파일

- `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs`
  - `ToggleAutoProduction()`: `canShow` 판정 (246행)
  - `TryStartNext()`: `PendingQueue[0]`를 슬롯0으로 올리는 로직 (480~519행)

---

## 수정 방향 검토

### 방법 A: 큐 비어있을 때 TryStartNext 즉시 호출

`ToggleAutoProduction` 내에서 `PendingQueue.Add()` 후 큐가 비어있었고 CurrentProducing이 없다면 즉시 `TryStartNext(state)`를 호출.

- **장점**: UI에 슬롯1→슬롯0 이동이 보이지 않음, 논리적으로 자연스러움
- **단점**: `ToggleAutoProduction`이 `TryStartNext`를 직접 호출하는 결합 발생 (현재 `TryStartNext`는 `Tick()`에서만 호출됨)

### 방법 B: canShow 조건 수정

큐가 비어있어도 `canShow = true`로 처리하여 즉시 골드 차감 + IsCharged=true로 PendingQueue에 추가. 단, CurrentProducing이 없으면 UI 슬롯1이 아닌 슬롯0에 바로 표시되어야 하므로 UI 로직도 함께 수정 필요.

- **단점**: UI와 도메인 로직 경계가 복잡해짐

### 방법 C: ToggleAutoProduction에서 PendingQueue 대신 즉시 시작

큐 비어있을 때는 PendingQueue를 거치지 않고 CurrentProducing을 직접 설정.

- **단점**: `TryStartNext`와 로직 중복, 골드 차감 경로 분기 증가

### 채택 방향

**방법 A** 채택. `ToggleAutoProduction`에서 아이템 추가 후 즉시 `TryStartNext(state)` 호출하는 것이 가장 자연스럽고 UI 로직 변경 없이 해결 가능.

- `TryStartNext`는 이미 "PendingQueue[0]이 있으면 꺼내 슬롯0으로 올린다"는 완성된 로직을 포함
- 중복 로직 없음, 단일 호출로 상태 정리
- `ChargeVisibleSlots`도 내부에서 호출되어 추가 슬롯 차감 자동 처리

---

## 수정 범위 (예상)

| 파일 | 변경 내용 |
|------|----------|
| `UnitProductionUseCase.cs` | `ToggleAutoProduction()` — PendingQueue 추가 후 즉시 시작 조건 추가 (1~3행) |
