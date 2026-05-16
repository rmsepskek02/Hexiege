# Plan — Rule 20 슬롯0 확장

## 작업 개요 (자연어 설명)

규칙 20을 슬롯0(현재 생산 중인 슬롯)까지 확장합니다.
슬롯0에서 수동으로 A를 생산하는 도중 A를 자동등록하면, 슬롯1에 A를 새로 추가하지 않고 슬롯0을 "자동 생산"으로 전환합니다.
이를 통해 슬롯0/슬롯1에 A/A가 중복으로 쌓이는 현상과 불필요한 골드 선차감을 방지합니다.

---

## 기존 로직 제거 사항

없음 — 새 조건문 추가만 이루어짐.

---

## 수정 항목

### 1. UnitProductionUseCase.cs — ToggleAutoProduction

**근거**: GameSystemRules.md 규칙 20 (확장)

Rule 2-1 PendingQueue 체크 직전, 슬롯0 체크를 추가합니다.
PendingQueue에 새 항목을 추가하지 않고 AutoTypes에만 등록하고 CurrentIsAuto=true로 전환합니다.

```csharp
// ─── Rule 20 확장: 슬롯0(CurrentProducing)이 같은 타입 수동 항목이면 수동→자동 전환 ───
// 슬롯0에서 수동으로 A를 생산 중일 때 A를 자동등록하면,
// 슬롯1에 A를 새로 추가하지 않고 슬롯0 자체를 "자동"으로 전환한다.
// → 완료 시 CompleteProduction의 wasAuto=true 조건을 만족하여 자동 순환이 시작됨.
// → 골드 이중 차감 없음 (슬롯0의 골드는 이미 수동 등록 시 차감됨).
if (state.CurrentProducing.HasValue &&
    state.CurrentProducing.Value == type &&
    !state.CurrentIsAuto)
{
    state.CurrentIsAuto = true;
    state.AutoTypes.Add(type);
    NormalizeAutoCycleIndex(state);
    GameEvents.OnProductionQueueChanged.OnNext(
        new ProductionQueueChangedEvent(barracksId));
    return true;
}
```

추가 위치: `UnitProductionUseCase.cs` — AutoTypes 상한 체크(< 3) 직후, Rule 2-1 PendingQueue 체크 직전

---

### 2. GameSystemRules.md — 규칙 20 문구 수정

**현재:**
> 자동 타입을 새로 등록할 때, 대기 큐의 마지막 항목이 수동으로 등록된 같은 타입이면
> 중복 추가 없이 기존 항목을 자동으로 전환한다. 골드는 이미 차감된 상태를 유지한다.

**변경 후:**
> 자동 타입을 새로 등록할 때, 같은 타입이 이미 슬롯0에서 수동 생산 중이거나
> 대기 큐의 마지막 항목이 수동으로 등록된 같은 타입이면,
> 중복 추가 없이 기존 항목을 자동으로 전환한다. 골드는 이미 차감된 상태를 유지한다.

---

## 수정 전후 동작 비교

| 상황 | 변경 전 | 변경 후 |
|------|---------|---------|
| 슬롯0: A(수동) + A 자동등록 | 슬롯0:A(수동), 슬롯1:A(자동, 골드차감) | 슬롯0:A(자동전환), 슬롯1:비어있음 |
| 슬롯0의 A 완료 후 | wasAuto=false → 자동순환 없음 | wasAuto=true → 자동순환 시작 |
| 골드 | 슬롯1 등록 시 추가 차감 O | 추가 차감 없음 |

---

## 위험 요소

| 위험 | 수준 | 근거 |
|------|------|------|
| AutoTypes 상한 초과 | 낮음 | 슬롯0 체크는 AutoTypes.Count < 3 통과 후에 위치 → 기존과 동일하게 차단 |
| BUG-15 충돌 | 없음 | BUG-15는 CurrentIsAuto=true 케이스, 이번 확장은 CurrentIsAuto=false 케이스 — 상호 배타 |
| CompleteProduction 오동작 | 없음 | CurrentIsAuto=true + AutoTypes.Contains(A) 조건 모두 만족 → 자동 순환 정상 작동 |

---

## 수정 파일 목록

1. `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs`
2. `Assets/_Project/Docs/GameSystemRules.md`
