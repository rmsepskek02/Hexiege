# Research — 자동생산 큐 버그 수정

## 개요

자동생산 등록(롱프레스) 및 슬롯 취소 시 큐 순서가 꼬이는 버그들을 분석한다.

---

## 시스템 구조 개요

```
슬롯 0 = CurrentProducing (현재 생산 중)
슬롯 1 = 다음 대기 항목
슬롯 2 = 그 다음 대기 항목

ManualQueue  = 수동으로 등록된 항목 (최대 3 - CurrentProducing = 최대 2개 대기)
AutoEntries  = 자동생산 등록 항목 리스트 (순환 반복)
AutoIndex    = AutoEntries에서 현재 순환 위치
```

---

## 실기 테스트에서 확인된 버그

### 테스트 전제
- 수동으로 유닛 3, 2, 1 순서로 등록 → CurrentProducing=3, ManualQueue=[2,1]
- 이후 자동생산 활성화

| 시나리오 | 실제 동작 | 기대 동작 |
|---------|---------|---------|
| [3,2,1] + 자동1 활성화 후 취소 | 3→1→2→1 순서로 취소 | 3→2→1 (1 취소 시 자동 해제) |
| [3,2,1] + 자동2 활성화 후 취소 | 3→2→2→1 순서로 취소 | 3→2→1→2 (4번째가 자동2) |
| [3,2,1] + 자동3 활성화 후 취소 | 3→2→1 (자동3 사라짐) | 3→2→1→3 (4번째가 자동3) |
| [3,2] + 자동1 활성화 | 큐가 3→1→2로 변경 | 3→2→1 (슬롯2에 자동1) |
| [3,2] + 자동2 활성화 | 큐가 3→2→2로 변경 | 3→2 유지, 2는 자동전환 |
| [3,2] + 자동3 활성화 | 큐가 3→2로 유지(자동3 미표시) | 3→2→3 (슬롯2에 자동3 즉시) |

---

## 근본 원인 분석

### 핵심 원인: `isNormalAutoState` 판단 오류

슬롯1~2 표시 순서와 취소 대상 결정에 쓰이는 `isNormalAutoState` 플래그가 잘못된다.

**현재 코드 (ProductionPanelUI.cs, UnitProductionUseCase.cs 양쪽)**
```csharp
bool isNormalAutoState = autoCount > 0 && (
    !state.CurrentProducing.HasValue
    || state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value
);
```

**의도**: 슬롯0이 자동 항목으로 채워진 "정상 자동 상태"인지 판단.
- `true` → AutoIndex 위치가 슬롯0 → 대기 항목은 AutoIndex+1 부터
- `false` → 슬롯0이 비었거나 수동 → 대기 항목은 AutoIndex+0 부터

**문제**: `AutoEntries[AutoIndex].Type == CurrentProducing` 조건은
"현재 자동으로 생산 중"과 "수동 생산 중인데 타입이 우연히 같음"을 구분하지 못한다.

**예시 — [3,2]+자동3 케이스**:
- 수동으로 3 생산 중 + 자동3 등록
- `AutoEntries[0].Type=3 == CurrentProducing=3` → `isNormalAutoState=true` (잘못됨)
- `autoOffset=1` → 자동3은 AutoEntries 범위 초과 → 슬롯2에 아무것도 표시 안 됨
- 기대: 슬롯2에 자동3이 표시되어야 함

---

## Bug별 상세

### Bug-A: `UpdateQueueSlots` — AutoEntries가 ManualQueue보다 항상 먼저 표시됨

**위치**: `ProductionPanelUI.cs` → `UpdateQueueSlots()` 슬롯1~2 pending 목록 구성

**문제 흐름**:
```
1단계: AutoEntries 대기 항목을 먼저 추가
2단계: ManualQueue 항목을 그 뒤에 추가
→ slot1 = 자동 항목, slot2 = 수동 항목 (순서 꼬임)
```

**[3,2,1]+자동1 활성화 시**:
- `isNormalAutoState=false` (자동1 ≠ 수동3)
- `pending0 = AutoEntries[0] = 1(자동)`, `pending1 = ManualQueue[0] = 2(수동)`
- 표시: [3, **1**, 2] → 1이 2 앞으로 점프하는 버그

**올바른 순서**: ManualQueue를 먼저, AutoEntries는 남은 슬롯에

---

### Bug-A': `CancelQueueAt` — Bug-A와 동일한 순서 오류

**위치**: `UnitProductionUseCase.cs` → `CancelQueueAt()` 자동 모드 슬롯1~2 취소

**문제**: `UpdateQueueSlots`와 취소 대상 결정 순서가 불일치하면
슬롯에 보이는 것과 실제로 취소되는 항목이 달라진다.

현재 코드는 AutoEntries를 ManualQueue보다 먼저 취소 대상으로 삼고 있어
Bug-A와 동일한 이유로 취소 순서가 꼬인다.

---

### Bug-B: `ToggleAutoProduction` — ManualQueue 마지막 항목 이관 미구현

**위치**: `UnitProductionUseCase.cs` → `ToggleAutoProduction()` 미등록 타입 추가 경로

**문제**: 자동 활성화 시 해당 유닛이 ManualQueue 마지막 항목과 같은 경우에도
무조건 AutoEntries에 별도 추가 → 큐에 같은 타입이 중복됨

**[3,2,1]+자동1 활성화 시**:
- ManualQueue=[2,1]에서 마지막이 1
- 현재: AutoEntries에 1 추가 → 큐가 [3,2,1(수동),1(자동)]로 중복
- 기대: ManualQueue에서 마지막 1 제거 + AutoEntry로 이관 (골드 승계, IsCharged=true)

---

### Bug-C: `ToggleAutoProduction` — BUG-15 수정이 과도하게 적용됨

**위치**: `UnitProductionUseCase.cs` → `ToggleAutoProduction()` line ~282

**현재 코드**:
```csharp
if (canShowInSlot && type == state.CurrentProducing)
    canShowInSlot = false;
```

**원래 의도 (BUG-15)**: 자동 생산 중인 동일 타입 재등록 시 골드 이중 차감 방지.

**문제**: 조건이 너무 광범위해서 **수동으로** 같은 타입 생산 중인 경우도 막힘.

**[3,2]+자동3 활성화 시**:
- CurrentProducing=3(수동), 슬롯2 비어있음
- `type(3) == CurrentProducing(3)` → `canShowInSlot=false`
- 결과: 자동3이 IsCharged=false로 등록 → 슬롯2에 표시 안 됨
- 기대: 슬롯2 빈 자리이므로 자동3 즉시 표시되어야 함

수동으로 생산 중인 경우는 이중 차감 위험 없음 → 조건을 좁혀야 함.

---

## 파악된 수정 대상 파일

| 파일 | 수정 대상 메서드 | 관련 버그 |
|------|--------------|---------|
| `Domain/Building/ProductionState.cs` | 필드 추가 | 핵심 원인 해결 |
| `Application/UseCases/UnitProductionUseCase.cs` | `ToggleAutoProduction`, `CancelQueueAt`, `TryStartNext` | Bug-B, Bug-C, Bug-A', isNormalAutoState |
| `Presentation/UI/ProductionPanelUI.cs` | `UpdateQueueSlots` | Bug-A, isNormalAutoState |

---

## 유지해야 하는 기존 동작

- **TC-SINGLE-004 (순수 자동 모드)**: ManualQueue=[] 상태에서의 자동 순환 동작 회귀 없음
- **Rule 1 (취소 시 환불)**: 슬롯 취소 시 IsCharged=true 항목 전액 환불 유지
- **BUG-19 수정 (슬롯0 취소 후 자동 직접 시작)**: CancelQueueAt 슬롯0 분기 유지
- **Rule 2 (수동 시작 시 자동 슬롯 항목 이관)**: EnqueueUnit 시 IsCharged=true 항목 ManualQueue 이관 유지
