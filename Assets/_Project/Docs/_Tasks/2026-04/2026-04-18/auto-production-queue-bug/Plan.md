# Plan — 자동생산 큐 버그 수정 (전면 재작성)

## 자동생산 큐 시스템 규칙 (설계 의도)

### Rule 1: 취소 시 골드 환불
- 슬롯에 표시된 유닛을 취소하면 골드 전액 환불. 수동/자동 모두 동일.

### Rule 2: 자동생산 활성화 — 두 가지 경우

**경우 1 — 자동 등록하려는 유닛이 수동 큐의 마지막 유닛과 같은 경우**
- 마지막 수동 유닛을 자동으로 전환한다. 큐에 새로 추가하지 않는다.
- 이미 수동 등록 시 골드가 차감되어 있으므로 골드를 추가로 차감하지 않는다.
- 예: 수동 큐 [3,2,1] + 자동1 활성화 → 마지막 1이 자동으로 전환. 큐는 [3,2,1(자동)] 유지.

**경우 2 — 자동 등록하려는 유닛이 수동 큐의 마지막 유닛과 다른 경우**
- 등록된 수동 큐 순서를 유지하고, 그 뒤에 자동생산 유닛을 추가한다.
- 빈 슬롯이 있으면 즉시 표시되고 골드가 차감된다.
- 빈 슬롯이 없으면 슬롯에 표시될 때 골드가 차감된다.
- 예: 수동 큐 [3,2,1] + 자동2 → 큐는 [3,2,1,2(자동)].
- 예: 수동 큐 [3,2,1] + 자동3 → 큐는 [3,2,1,3(자동)].
- 예: 수동 큐 [3,2] + 자동3 → 슬롯2 비어있으므로 즉시 표시. 큐는 [3,2,3(자동)].

### Rule 3: 순수 자동 모드 동작 유지 (회귀 없음)
- 수동 큐가 비어있을 때는 기존 자동생산 동작이 그대로 유지된다.

---

## 근본 원인 분석

### 핵심 문제: `isNormalAutoState` 판단 오류

`UpdateQueueSlots`(ProductionPanelUI)와 `CancelQueueAt`(UnitProductionUseCase) 두 곳에서
슬롯1~2 표시/취소 순서를 결정하는 `isNormalAutoState` 플래그가 있다.

```csharp
// 현재 코드 (잘못됨)
bool isNormalAutoState = autoCount > 0 && (
    !state.CurrentProducing.HasValue
    || state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value
);
```

**이 플래그의 의도**: 슬롯0이 자동 유닛으로 채워져 있는지 판단.
- true → 슬롯0=자동 항목 → auto의 다음 항목은 AutoIndex+1 부터 (offset=1)
- false → 슬롯0=비어있거나 수동 → auto 항목은 AutoIndex+0 부터 (offset=0)

**문제**: `AutoEntries[AutoIndex].Type == CurrentProducing` 조건이
"현재 슬롯0이 자동으로 생산 중" 이 아니라 "자동 등록된 타입과 현재 생산 타입이 우연히 같음" 일 때도 true가 된다.

**예시 — [3,2]+자동3 케이스**:
- 수동으로 3을 생산 중, 자동3을 등록
- AutoEntries[0].Type=3 == CurrentProducing=3 → isNormalAutoState=**true** (잘못됨!)
- autoOffset=1 → 자동3(AutoIndex+1)은 AutoEntries 범위 초과 → 슬롯2에 **아무것도 안 표시됨**
- 기대: 슬롯2에 자동3이 표시되어야 함

---

## 수정 설계

### 핵심 수정: `ProductionState.CurrentProducingIsAuto` 필드 추가

슬롯0이 "수동 생산"인지 "자동 생산"인지 명시적으로 추적한다.

**파일**: `Assets/_Project/Scripts/Domain/Building/ProductionState.cs`

```csharp
/// <summary>
/// 현재 슬롯0(CurrentProducing)이 자동 생산으로 시작되었는지 여부.
/// true  = 자동 생산 중 (AutoEntries에서 시작됨)
/// false = 수동 생산 중 (ManualQueue에서 시작됨) 또는 아무것도 생산 안 함
/// </summary>
public bool CurrentProducingIsAuto { get; set; }
```

### `isNormalAutoState` 수정 — 두 파일 모두

```csharp
// 수정 후
bool isNormalAutoState = autoCount > 0 && (
    !state.CurrentProducing.HasValue
    || (state.CurrentProducingIsAuto
        && state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value)
);
```

`CurrentProducingIsAuto=false`이면 수동 생산 중 → isNormalAutoState는 항상 false.

---

## 수정 파일 및 메서드

### 1. `ProductionState.cs` — 필드 추가

- `public bool CurrentProducingIsAuto { get; set; }` 추가

---

### 2. `UnitProductionUseCase.cs` — 4곳 수정

#### 2-1. `TryStartNext()` — CurrentProducingIsAuto 세팅

수동 큐에서 시작: `state.CurrentProducingIsAuto = false`
자동에서 시작: `state.CurrentProducingIsAuto = true`

```csharp
// 수동 큐 우선
if (state.ManualQueue.Count > 0)
{
    // ... 기존 로직 ...
    state.CurrentProducingIsAuto = false;  // ← 추가
}
// 자동 모드
else if (state.IsAutoMode && state.AutoEntries.Count > 0)
{
    // ... 기존 로직 ...
    state.CurrentProducingIsAuto = true;   // ← 추가
}
```

#### 2-2. `CancelQueueAt()` 슬롯0 분기 — BUG-19 다음 자동 시작 시 세팅

슬롯0 취소 후 다음 자동을 직접 시작하는 BUG-19 수정 코드에 추가:

```csharp
if (nextAuto.IsCharged)
{
    state.CurrentProducing = nextAuto.Type;
    state.CurrentProducingIsAuto = true;   // ← 추가
    // ... 기존 로직 ...
}
```

#### 2-3. `CancelQueueAt()` 슬롯1~2 분기 — isNormalAutoState 수정

```csharp
// 수정 후
bool isNormalAutoState = autoCount > 0 && (
    !state.CurrentProducing.HasValue
    || (state.CurrentProducingIsAuto
        && state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value)
);
```

#### 2-4. `ToggleAutoProduction()` — BUG-15 조건 수정 (Bug-C)

```csharp
// 수정 후: 자동으로 생산 중일 때만 BUG-15 적용
if (canShowInSlot && type == state.CurrentProducing && state.CurrentProducingIsAuto)
    canShowInSlot = false;
```

---

### 3. `ProductionPanelUI.cs` — isNormalAutoState 수정 (Bug-A)

```csharp
// 수정 후
bool isNormalAutoState = autoCount > 0 && (
    !state.CurrentProducing.HasValue
    || (state.CurrentProducingIsAuto
        && state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value)
);
```

---

## 기존 구현 검토 (이번 수정에서 유지)

### Bug-B (ToggleAutoProduction 마지막 항목 이관) — 유지
현재 코드에 이미 구현됨. `CurrentProducingIsAuto` 도입 후에도 동작 동일.

### Bug-A (UpdateQueueSlots ManualQueue 먼저) — 유지
현재 코드에 이미 구현됨. `isNormalAutoState` 수정 후 올바르게 동작.

### Bug-A' (CancelQueueAt ManualQueue 먼저) — 유지
현재 코드에 이미 구현됨. `isNormalAutoState` 수정 후 올바르게 동작.

---

## 시나리오별 동작 검증

| 시나리오 | isNormalAutoState | slot1 | slot2 | 기대 |
|---------|------------------|-------|-------|------|
| [3,2]+자동3 (수동3 생산 중) | false (CurrentProducingIsAuto=false) | 2 | 3(자동) | ✓ |
| [3,2,1]+자동1 (이관 후) | false | 2 | 1(자동) | ✓ |
| [3,2,1]+자동2 (큐 가득) | false | 2 | 1 | ✓ (자동2는 4번째 대기) |
| 순수 자동 [A,B], A 생산 중 | true (CurrentProducingIsAuto=true) | B | - | ✓ |
| 순수 자동 [A], A 생산 중 | true | - | - | ✓ |

---

## 위험 요소 및 회귀 방지

| 위험 | 대응 |
|------|------|
| TryStartNext 이외 경로로 CurrentProducing 세팅 | CancelQueueAt BUG-19 경로에 CurrentProducingIsAuto=true 추가 |
| 순수 자동 모드 회귀 (TC-SINGLE-004) | isNormalAutoState 조건에 CurrentProducingIsAuto=true 포함 → 기존 동작 유지 |
| BUG-19 수정 (슬롯0 취소 후 자동 시작) | CancelQueueAt 슬롯0 분기 로직 유지, CurrentProducingIsAuto 세팅만 추가 |
| BUG-15 수정 (자동 재등록 이중 차감) | CurrentProducingIsAuto=true일 때만 적용 → BUG-15 케이스 유지 |

---

## 수정 파일 요약

| 파일 | 수정 내용 |
|------|---------|
| `Domain/Building/ProductionState.cs` | `CurrentProducingIsAuto` 필드 추가 |
| `Application/UseCases/UnitProductionUseCase.cs` | TryStartNext + CancelQueueAt(슬롯0/슬롯1~2) + ToggleAutoProduction |
| `Presentation/UI/ProductionPanelUI.cs` | UpdateQueueSlots의 isNormalAutoState 조건 수정 |
