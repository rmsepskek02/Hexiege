# Research — 자동생산 완료 사이클 슬롯2 깜빡임 버그

작성일: 2026-06-05

---

## 작업 목적 (자연어 설명)

자동생산을 켜두면 슬롯1에서 유닛이 계속 생산되어야 하는데, 생산이 한 번 완료될 때마다 슬롯2가 잠깐 켜졌다가 꺼지는 깜빡임 현상이 발생한다. 플레이어 눈에 슬롯2에 무언가 나타났다가 사라지는 것처럼 보여 혼란을 준다. 이 버그의 원인을 파악하고 수정 범위를 확인한다.

---

## 버그 현상

- **재현 조건**: 자동생산을 1종 등록한 상태에서 슬롯1(CurrentProducing)의 유닛 생산이 완료될 때
- **현상**: 생산 완료 순간 슬롯2에 해당 유닛 아이콘이 순간적으로 나타났다 사라짐
- **기대 동작**: 슬롯2는 변화 없이 비어있어야 하고, 슬롯1에서 즉시 다음 생산이 시작되어야 함

---

## 원인 분석

### Tick 루프 구조

```csharp
// UnitProductionUseCase.cs:493-501
public void Tick(float deltaTime)
{
    foreach (var state in _states.Values)
    {
        if (state.CurrentProducing == null)
            TryStartNext(state);   // ← CurrentProducing 없을 때만 호출
        else
            TickProduction(state, deltaTime);  // ← CompleteProduction 포함
    }
}
```

`TickProduction`과 `TryStartNext`는 매 Tick에서 **서로 다른 분기**에서만 호출된다. `TickProduction` 내부에서 `CompleteProduction`이 호출되더라도, `TryStartNext`는 **다음 프레임 Tick**에서야 실행된다.

---

### 한 프레임 내 시퀀스 (버그 발생 경로)

| 단계 | 코드 위치 | 상태 |
|------|-----------|------|
| 1. `CompleteProduction` 진입 | L668 | 슬롯1 생산 완료 |
| 2. `CurrentProducing = null` | L690 | 슬롯1 비어짐 |
| 3. 자동 항목 PendingQueue 재추가 (isCharged=false) | L700 | PendingQueue[0] = type A |
| 4. `ChargeVisibleSlots()` 호출 | L704 | isCharged=false → 골드 차감 → isCharged=true (슬롯2 표시 조건 충족) |
| 5. `OnProductionQueueChanged` 발행 | L709 | **UI 갱신: 슬롯1=비어있음, 슬롯2=type A ← 깜빡임 발생** |
| 6. CompleteProduction 종료 | — | — |
| 7. (다음 프레임) `TryStartNext` 호출 | L498 | PendingQueue[0] → CurrentProducing |
| 8. `OnProductionQueueChanged` 발행 | L577 | **UI 갱신: 슬롯1=type A 생산 중, 슬롯2=비어있음** |

5번 이벤트 → 8번 이벤트 사이 **1프레임 동안** 슬롯2가 켜졌다 꺼지는 것이 깜빡임이다.

**근본 원인**: `CompleteProduction`이 자동 항목을 PendingQueue에 추가하고 `ChargeVisibleSlots`로 골드까지 차감하여 슬롯2를 활성화한 상태로 이벤트를 발행하지만, 실제로 슬롯1 생산을 재시작하는 `TryStartNext`는 **다음 프레임**에서야 호출된다.

---

## 2026-04-19 수정과의 관계

[Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/17_49_production-slot-flicker](../../../2026-04/2026-04-19/17_49_production-slot-flicker/Research.md)에서 **동일한 패턴의 버그**를 수정한 전례가 있다.

| 항목 | 2026-04-19 수정 (완료) | 현재 버그 (미수정) |
|------|----------------------|-------------------|
| **발생 경로** | `AddNewAutoSlot` (자동 등록) | `CompleteProduction` (완료 사이클) |
| **발생 조건** | 큐 비어있을 때 자동 등록 시 | 자동 생산 완료 후 재순환 시 |
| **해결 방법** | 즉시 `TryStartNext` 호출 | 동일 방법 적용 필요 |

당시 수정에서 `AddNewAutoSlot`(등록 경로) 만 처리하고 `CompleteProduction`(완료 사이클 경로)은 같이 처리되지 않아 동일 패턴이 잔존했다.

현재 `AddNewAutoSlot`에 적용된 수정 코드 (L276-281):
```csharp
// 슬롯 깜빡임 버그 방지: 큐가 비어있었으면 즉시 TryStartNext 호출.
// TryStartNext 내부에서 OnProductionQueueChanged를 발행하므로 중복 방지를 위해 Early Return.
if (!state.CurrentProducing.HasValue)
{
    TryStartNext(state);
    return true;
}
```

---

## 관련 파일

| 파일 | 메서드 | 역할 |
|------|--------|------|
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | `CompleteProduction` L668 | 수정 대상 — 자동 재추가 후 TryStartNext 즉시 호출 필요 |
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | `TryStartNext` L535 | `ChargeVisibleSlots` + `OnProductionQueueChanged` 내부 포함 |

---

## 수정 범위 (예상)

| 파일 | 변경 내용 |
|------|----------|
| `UnitProductionUseCase.cs` | `CompleteProduction` — `ChargeVisibleSlots` 제거, `OnProductionQueueChanged` 직접 발행 제거, `TryStartNext` 즉시 호출로 대체 (3~4행) |
