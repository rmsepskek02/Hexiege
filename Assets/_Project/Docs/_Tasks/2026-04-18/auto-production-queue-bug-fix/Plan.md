# Plan — 자동생산 큐 버그 수정

## 자동생산 큐 시스템 규칙 (설계 의도)

### Rule 1: 취소 시 골드 환불
- 슬롯에 표시된 유닛을 취소하면 골드 전액 환불. 수동/자동 모두 동일.

### Rule 2: 자동생산 활성화 — 두 가지 경우

**경우 1 — 자동 등록하려는 유닛이 수동 큐의 마지막 유닛과 같은 경우**
- 마지막 수동 유닛을 자동으로 전환한다. 큐에 새로 추가하지 않는다.
- 이미 수동 등록 시 골드가 차감되어 있으므로 골드를 추가로 차감하지 않는다.
- 예: 수동 큐 [3,2,1] + 자동1 활성화 → ManualQueue에서 마지막 1 제거 + AutoEntry(IsCharged=true). 큐는 [3,2,1(자동)] 유지.

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

## 수정 설계

### 핵심 수정: `ProductionState.CurrentProducingIsAuto` 필드 추가

슬롯0(CurrentProducing)이 "수동 생산"인지 "자동 생산"인지 명시적으로 추적한다.

타입 비교(`AutoTypeAt == CurrentProducing`)만으로는 수동/자동 구분이 불가능하다.
예) 수동으로 3을 생산 중이고 자동3이 등록되면 타입이 같아 `isNormalAutoState=true`로 오판.

`CurrentProducingIsAuto` 플래그로 슬롯0의 "출처"를 명시하면 오판을 원천 차단한다.

**파일**: `Assets/_Project/Scripts/Domain/Building/ProductionState.cs`

```csharp
/// <summary>
/// 현재 슬롯0(CurrentProducing)이 자동 생산으로 시작되었는지 여부.
/// true  = 자동 생산 중 (AutoEntries에서 시작됨)
/// false = 수동 생산 중 (ManualQueue에서 시작됨) 또는 아무것도 생산 안 함
/// </summary>
public bool CurrentProducingIsAuto { get; set; }
```

### `isNormalAutoState` 수정 공식 (3개 파일 동일하게 적용)

```csharp
// 수정 후 — CurrentProducingIsAuto=false이면 수동 생산 중 → isNormalAutoState는 항상 false
bool isNormalAutoState = autoCount > 0 && (
    !state.CurrentProducing.HasValue
    || (state.CurrentProducingIsAuto
        && state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value)
);
```

---

## 수정 파일 및 메서드 상세

### 1. `ProductionState.cs` — 필드 추가

- `public bool CurrentProducingIsAuto { get; set; }` 추가
- 초기값 false (생산 없음 상태와 동일)

---

### 2. `UnitProductionUseCase.cs` — 4곳 수정

#### 2-1. `TryStartNext()` — CurrentProducingIsAuto 세팅

수동 큐에서 시작할 때는 `false`, 자동에서 시작할 때는 `true`로 세팅.

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

---

#### 2-2. `CancelQueueAt()` 슬롯0 분기 — BUG-19 자동 직접 시작 경로

슬롯0 취소 후 다음 자동 항목을 직접 시작하는 경로에 플래그 추가:

```csharp
if (nextAuto.IsCharged)
{
    state.CurrentProducing = nextAuto.Type;
    state.CurrentProducingIsAuto = true;   // ← 추가
    // ... 기존 로직 ...
}
```

---

#### 2-3. `CancelQueueAt()` 슬롯1~2 분기 — isNormalAutoState 수정 + 표시 순서 변경

**isNormalAutoState 수정**:
```csharp
// 수정 후
bool isNormalAutoState = autoCount > 0 && (
    !state.CurrentProducing.HasValue
    || (state.CurrentProducingIsAuto
        && state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value)
);
```

**Bug-A' 수정: ManualQueue 먼저, AutoEntries 후순위**

취소 대상 결정 순서를 UpdateQueueSlots와 일치시킨다:
```
1단계: ManualQueue 항목 먼저 취소 (pendingOffset < manualPendingCount)
2단계: 남은 슬롯의 AutoEntries 항목 취소
```

---

#### 2-4. `ToggleAutoProduction()` — Bug-B + Bug-C 수정

**Bug-B 수정: ManualQueue 마지막 항목이 같은 타입이면 이관 처리**

```csharp
// ManualQueue 마지막 항목과 같은 타입이면 "자동으로 전환"
if (state.ManualQueue.Count > 0
    && state.ManualQueue[state.ManualQueue.Count - 1] == type)
{
    // 마지막 수동 항목 제거 (골드는 이미 차감됨, 환불 없이 자동으로 전환)
    state.ManualQueue.RemoveAt(state.ManualQueue.Count - 1);

    // 자동 항목으로 추가 — IsCharged=true (이미 차감된 골드 승계)
    state.AutoEntries.Add(new AutoEntry(type, true));
    state.IsAutoMode = true;

    GameEvents.OnProductionQueueChanged.OnNext(...);
    return true;
}
```

**Bug-C 수정: BUG-15 조건을 자동 생산 중일 때만 적용**

```csharp
// 수정 전: type == state.CurrentProducing (수동/자동 구분 없음 → 과도한 차단)
// 수정 후: CurrentProducingIsAuto=true일 때만 적용 (자동으로 생산 중일 때만)
if (canShowInSlot && type == state.CurrentProducing && state.CurrentProducingIsAuto)
    canShowInSlot = false;
```

---

### 3. `ProductionPanelUI.cs` — isNormalAutoState 수정 + 표시 순서 변경

#### UpdateQueueSlots — Bug-A 수정: ManualQueue 먼저 표시

슬롯1~2 pending 목록 구성 순서 변경:
```
1단계: ManualQueue 항목을 먼저 추가 (최대 2개)
2단계: 남은 슬롯에 AutoEntries 대기 항목 추가
→ slot1 = 목록[0], slot2 = 목록[1]
```

**isNormalAutoState 수정**:
```csharp
// 수정 후
bool isNormalAutoState = autoCount > 0 && (
    !state.CurrentProducing.HasValue
    || (state.CurrentProducingIsAuto
        && state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value)
);
```

---

## 시나리오별 동작 검증

| 시나리오 | CurrentProducingIsAuto | isNormalAutoState | slot1 | slot2 | 기대 |
|---------|----------------------|------------------|-------|-------|------|
| [3,2]+자동3 (수동3 생산 중) | false | false | 2 | 3(자동) | ✓ |
| [3,2,1]+자동1 (이관 후) | false | false | 2 | 1(자동) | ✓ |
| [3,2,1]+자동2 (큐 가득) | false | false | 2 | 1 | ✓ (자동2는 4번째 대기) |
| 순수 자동 [A,B], A 생산 중 | true | true | B | — | ✓ |
| 순수 자동 [A], A 생산 중 | true | true | — | — | ✓ |

---

## 위험 요소 및 회귀 방지

| 위험 | 대응 |
|------|------|
| TryStartNext 이외 경로로 CurrentProducing 세팅 | CancelQueueAt BUG-19 경로에 `CurrentProducingIsAuto=true` 추가 |
| 순수 자동 모드 회귀 (TC-SINGLE-004) | `isNormalAutoState` 조건에 `CurrentProducingIsAuto=true` 포함 → 기존 동작 유지 |
| BUG-15 이중 차감 방지 로직 깨짐 | Bug-C 수정으로 자동 생산 중일 때만 적용 → BUG-15 케이스 유지 |
| CancelQueueAt 슬롯0 취소 후 순서 역전 (BUG-19) | 슬롯0 분기 로직 유지, `CurrentProducingIsAuto` 세팅만 추가 |

---

## 수정 파일 요약

| 파일 | 수정 내용 |
|------|---------|
| `Domain/Building/ProductionState.cs` | `CurrentProducingIsAuto` 필드 추가 |
| `Application/UseCases/UnitProductionUseCase.cs` | `TryStartNext` + `CancelQueueAt`(슬롯0/슬롯1~2) + `ToggleAutoProduction` |
| `Presentation/UI/ProductionPanelUI.cs` | `UpdateQueueSlots`의 pending 순서 + `isNormalAutoState` 조건 수정 |
