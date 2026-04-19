# Research — 자동생산 큐 버그 (전면 재분석)

## 실기 테스트 결과 요약

| 시나리오 | 실제 동작 | 기대 동작 |
|---------|---------|---------|
| [3,2,1] + 자동1 활성화 → 슬롯 취소 | 3→**1**→2→1 (꼬임) | 3→2→**1** (1 취소 시 자동도 취소) |
| [3,2,1] + 자동2 활성화 → 슬롯 취소 | 3→2→**2**→1 (꼬임) | 3→2→1→**2** (4번째 2가 자동) |
| [3,2,1] + 자동3 활성화 → 슬롯 취소 | 3→2→1 (자동 사라짐) | 3→2→1→**3** (4번째 3이 자동) |
| [3,2] + 자동1 활성화 | 큐가 3→**1**→2로 변경 | 3→2→**1** (슬롯2에 자동1) |
| [3,2] + 자동2 활성화 | 큐가 3→2→**2**로 변경 | 3→2 유지, 2는 자동전환 (골드 다음 사이클 차감) |
| [3,2] + 자동3 활성화 | 큐가 3→2로 유지 (자동3 미표시) | 큐가 3→2→**3**으로 변경 (슬롯2에 자동3 즉시) |

---

## 관련 파일

| 파일 | 관련 버그 |
|------|---------|
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | Bug-B, Bug-C |
| `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | Bug-A, Bug-A' |

---

## Bug-A: `UpdateQueueSlots` — AutoEntries가 ManualQueue보다 항상 먼저 표시됨

### 위치
`ProductionPanelUI.cs` → `UpdateQueueSlots()` — 슬롯1~2 pending 목록 구성 로직

### 현재 코드 흐름

```
1단계: AutoEntries 대기 항목을 pending 목록에 먼저 추가
  - isNormalAutoState=true  → AutoIndex+1, AutoIndex+2
  - isNormalAutoState=false → AutoIndex+0, AutoIndex+1   ← 문제 발생 지점

2단계: ManualQueue 항목을 그 뒤에 추가

결과: slot1=AutoEntries 항목, slot2=ManualQueue 항목
```

### 버그 발생 시나리오

**[3,2,1] + 자동1 활성화:**
- CurrentProducing=3, ManualQueue=[2,1], AutoEntries=[1(false)]
- isNormalAutoState: AutoEntries[0]=1 ≠ CurrentProducing=3 → **false**
- pending0 = AutoEntries[0] = **1** (자동)
- pending1 = ManualQueue[0] = **2** (수동)
- 표시: [3, **1**, 2] ← 큐 순서 꼬임 (1이 2 앞으로 점프)

**[3,2] + 자동1 활성화:**
- CurrentProducing=3, ManualQueue=[2], AutoEntries=[1(true)]
- isNormalAutoState=false → pending0=1(자동), pending1=2(수동)
- 표시: [3, **1**, 2] ← 3→1→2로 꼬임

### 올바른 동작

수동 큐에 항목이 있는 경우 ManualQueue 항목이 항상 먼저 표시되어야 함:
- 1단계: ManualQueue 항목 먼저 추가
- 2단계: 남은 슬롯에 AutoEntries 대기 항목 추가

---

## Bug-A': `CancelQueueAt` — UpdateQueueSlots와 동일한 pending 순서 사용

### 위치
`UnitProductionUseCase.cs` → `CancelQueueAt()` — 자동 모드 슬롯1~2 취소 로직

### 문제
`CancelQueueAt`의 취소 대상 결정 로직도 UpdateQueueSlots와 동일하게
"AutoEntries 먼저, ManualQueue 나중" 순서를 사용하므로, Bug-A와 동일한 이유로
취소 순서가 꼬임.

**예시:** [3,2,1]+자동1 → 슬롯 취소 순서가 3→1→2→1로 꼬이는 원인

### 필요한 수정
UpdateQueueSlots 수정과 동일한 순서(ManualQueue 먼저)로 일치시켜야 함.

---

## Bug-B: `ToggleAutoProduction` — 큐 마지막 항목 자동전환 미구현

### 위치
`UnitProductionUseCase.cs` → `ToggleAutoProduction()` — 미등록 타입 추가 경로

### 현재 동작
자동생산 활성화 시 무조건 AutoEntries에 새 항목 추가.
ManualQueue에 이미 같은 유닛이 있어도 중복 추가됨.

### 버그 발생 시나리오

**[3,2,1] + 자동1 활성화:**
- ManualQueue=[2,1]에서 1은 마지막 항목
- 현재: AutoEntries에 1을 별도 추가 → 큐에 1이 두 번 존재
- 기대: ManualQueue 마지막 항목(1)을 AutoEntries로 이관, ManualQueue=[2]

**[3,2] + 자동2 활성화:**
- ManualQueue=[2]에서 2는 마지막 항목
- 현재: AutoEntries에 2를 별도 추가 → 큐가 [3,2,2]로 보임
- 기대: ManualQueue 마지막 항목(2)을 AutoEntries로 이관 (골드 이미 차감됨 → IsCharged=true)
  - 표시: [3, 2(자동)] 유지
  - 2가 생산 완료 후 다음 사이클에서 새 2 생산 시 골드 차감

### 규칙 정의
> 자동생산을 활성화할 때 해당 유닛 타입이 ManualQueue의 **마지막** 항목과 일치하면
> → 새 AutoEntry를 추가하지 않고, 마지막 ManualQueue 항목을 AutoEntries로 이관(IsCharged=true)

---

## Bug-C: `ToggleAutoProduction` — BUG-15 수정이 과도하게 적용됨

### 위치
`UnitProductionUseCase.cs` → `ToggleAutoProduction()` line 245~246

### 현재 코드
```csharp
if (canShowInSlot && type == state.CurrentProducing)
    canShowInSlot = false;
```

### 원래 의도 (BUG-15)
자동 유닛 X가 슬롯0에서 생산 중일 때 X를 재등록하면 골드 중복 차감 방지.
→ 이미 자동으로 X가 생산 중이므로 IsCharged=false로 등록, TryStartNext에서 처리.

### 버그 발생 시나리오

**[3,2] + 자동3 활성화:**
- CurrentProducing=3 (**수동**), ManualQueue=[2], 슬롯2 비어있음
- `type(3) == state.CurrentProducing(3)` → true → `canShowInSlot=false`
- 결과: auto 3이 IsCharged=false로 등록됨 → 슬롯2에 표시 안 됨 → **Bug**
- 기대: 슬롯2가 비어있으므로 auto 3이 슬롯2에 즉시 표시되어야 함 (IsCharged=true, 골드 차감)

### 원인 분석
BUG-15 수정의 조건이 너무 광범위함:
- `type == CurrentProducing`이지만 **CurrentProducing이 수동 생산인 경우**는 이중 차감 위험 없음
- 수동으로 생산 중인 3의 골드 ≠ 자동 생산될 다음 사이클 3의 골드

### 필요한 수정
BUG-15 조건을 **CurrentProducing이 자동 생산 중일 때만** 적용하도록 좁힘:

```csharp
// 현재 생산 중인 유닛이 자동 생산(AutoEntries[AutoIndex])인지 판별
bool currentIsAutoProducing = state.IsAutoMode
    && state.AutoEntries.Count > 0
    && state.AutoEntries[state.AutoIndex].Type == state.CurrentProducing;

if (canShowInSlot && type == state.CurrentProducing && currentIsAutoProducing)
    canShowInSlot = false;
```

---

## 기존 수정 (TryPreChargeAutoEntries) 상태

이전 세션에서 수정한 `TryPreChargeAutoEntries()`는 골드 차감 타이밍 로직으로,
Bug-A/B/C와는 독립적입니다. 해당 수정은 유지합니다.

---

## 영향 범위 요약

| 버그 | 파일 | 메서드 | 수정 규모 |
|-----|------|--------|---------|
| Bug-A | ProductionPanelUI.cs | UpdateQueueSlots() | pending 목록 구성 순서 변경 |
| Bug-A' | UnitProductionUseCase.cs | CancelQueueAt() | pending 순서 Bug-A와 동일하게 변경 |
| Bug-B | UnitProductionUseCase.cs | ToggleAutoProduction() | 마지막 ManualQueue 항목 이관 로직 추가 |
| Bug-C | UnitProductionUseCase.cs | ToggleAutoProduction() | BUG-15 조건 좁히기 |

---

## 주의 사항

- TC-SINGLE-004 (순수 자동 모드) 회귀 없어야 함
  - ManualQueue=[] 상태에서는 기존 동작 유지
- Rule 2 (수동 생산 시작 시 자동 슬롯 항목 ManualQueue 이관) 동작 유지
- Rule 1 (취소 시 환불) 동작 유지
- CancelQueueAt의 자동 모드 슬롯0 취소 (BUG-19 수정) 동작 유지
