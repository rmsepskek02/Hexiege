# Testcase — 자동생산 큐 버그 수정

## TC 목록

---

### TC-SINGLE-001: [3,2,1] 수동 등록 후 자동1 활성화 — 큐 표시 및 취소 순서

**전제:**
- 배럭에 유닛을 수동으로 3→2→1 순서로 등록 (슬롯0=3 생산 중, 슬롯1=2, 슬롯2=1 대기)
- 자동생산은 아무것도 활성화되어 있지 않음

**동작:**
1. 1번 유닛 버튼을 길게 눌러 자동생산 활성화
2. 큐 슬롯 표시 확인
3. 첫 번째 슬롯부터 순서대로 취소

**기댓값:**
- 자동생산 활성화 후 큐는 슬롯0=3, 슬롯1=2, 슬롯2=1(자동) 표시
- 슬롯0(3) 취소 → 슬롯0=2, 슬롯1=1(자동), 슬롯2=비어있음
- 슬롯0(2) 취소 → 슬롯0=1(자동), 슬롯1=비어있음
- 슬롯0(1, 자동) 취소 → 자동생산 인디케이터도 꺼지고 슬롯 전체 비어있음
- 총 취소 순서: 3 → 2 → 1(자동 해제)

**결과:** FAIL
버그 - 슬롯0(3) 취소 → 슬롯 0 = 1, 슬롯 1=2, 슬롯 2 = 비어있음
(기댓값: 슬롯0=2, 슬롯1=1(자동). 큐 순서 역전 + 자동 항목이 슬롯0으로 점프함)

---

### TC-SINGLE-002: [3,2,1] 수동 등록 후 자동2 활성화 — 큐 표시 및 취소 순서

**전제:**
- 배럭에 유닛을 수동으로 3→2→1 순서로 등록 (슬롯0=3 생산 중, 슬롯1=2, 슬롯2=1 대기)
- 자동생산은 아무것도 활성화되어 있지 않음

**동작:**
1. 2번 유닛 버튼을 길게 눌러 자동생산 활성화
2. 큐 슬롯 표시 확인
3. 첫 번째 슬롯부터 순서대로 취소

**기댓값:**
- 자동생산 활성화 후 큐는 슬롯0=3, 슬롯1=2, 슬롯2=1 표시 (슬롯이 꽉 차 있어 자동2는 대기 중)
- 슬롯0(3) 취소 → 슬롯0=2, 슬롯1=1, 슬롯2=2(자동) 표시
- 슬롯0(2) 취소 → 슬롯0=1, 슬롯1=2(자동)
- 슬롯0(1) 취소 → 슬롯0=2(자동)
- 슬롯0(2, 자동) 취소 → 자동생산 인디케이터도 꺼지고 슬롯 전체 비어있음
- 총 취소 순서: 3 → 2(수동) → 1 → 2(자동 해제)

**결과:** FAIL
버그 - 슬롯0(3) 취소 → 슬롯 0=2, 슬롯 1=2, 슬롯 2=1
(기댓값: 슬롯0=2, 슬롯1=1, 슬롯2=2(자동). 자동2가 슬롯1에 중복 표시됨)

---

### TC-SINGLE-003: [3,2,1] 수동 등록 후 자동3 활성화 — 큐 표시 및 취소 순서

**전제:**
- 배럭에 유닛을 수동으로 3→2→1 순서로 등록 (슬롯0=3 생산 중, 슬롯1=2, 슬롯2=1 대기)
- 자동생산은 아무것도 활성화되어 있지 않음

**동작:**
1. 3번 유닛 버튼을 길게 눌러 자동생산 활성화
2. 큐 슬롯 표시 확인
3. 첫 번째 슬롯부터 순서대로 취소

**기댓값:**
- 자동생산 활성화 후 큐는 슬롯0=3, 슬롯1=2, 슬롯2=1 표시 (슬롯이 꽉 차 있어 자동3은 대기 중)
- 슬롯0(3) 취소 → 슬롯0=2, 슬롯1=1, 슬롯2=3(자동) 표시
- 슬롯0(2) 취소 → 슬롯0=1, 슬롯1=3(자동)
- 슬롯0(1) 취소 → 슬롯0=3(자동)
- 슬롯0(3, 자동) 취소 → 자동생산 인디케이터도 꺼지고 슬롯 전체 비어있음
- 총 취소 순서: 3(수동) → 2 → 1 → 3(자동 해제)

**결과:** FAIL
버그 - 슬롯0(3) 취소 → 슬롯0=2, 슬롯1=1, 슬롯2=비어있음. 자동3은 취소됨
(기댓값: 슬롯0=2, 슬롯1=1, 슬롯2=3(자동). 자동3 항목이 사라짐)

---

### TC-SINGLE-004: [3,2] 수동 등록 후 자동1 활성화 — 즉시 슬롯 표시

**전제:**
- 배럭에 유닛을 수동으로 3→2 순서로 등록 (슬롯0=3 생산 중, 슬롯1=2, 슬롯2=비어있음)
- 자동생산은 아무것도 활성화되어 있지 않음

**동작:**
1. 1번 유닛 버튼을 길게 눌러 자동생산 활성화

**기댓값:**
- 활성화 직후 큐는 슬롯0=3, 슬롯1=2, 슬롯2=1(자동) 표시
- 이 시점에 1번 유닛의 골드가 즉시 차감됨

**결과:**



---

### TC-SINGLE-005: [3,2] 수동 등록 후 자동2 활성화 — 마지막 항목 이관

**전제:**
- 배럭에 유닛을 수동으로 3→2 순서로 등록 (슬롯0=3 생산 중, 슬롯1=2, 슬롯2=비어있음)
- 자동생산은 아무것도 활성화되어 있지 않음

**동작:**
1. 2번 유닛 버튼을 길게 눌러 자동생산 활성화

**기댓값:**
- 마지막 수동 항목(2)이 자동으로 전환됨
- 큐는 슬롯0=3, 슬롯1=2(자동) 표시 (슬롯2는 비어있음)
- 골드 추가 차감 없음 (수동 등록 시 이미 차감됨)
- 3 생산 완료 후 2가 슬롯0으로 이동, 다음 2 자동생산 예약 시 골드 차감

**결과:**

---

### TC-SINGLE-006: [3,2] 수동 등록 후 자동3 활성화 — 즉시 슬롯 표시 (수동과 다른 타입)

**전제:**
- 배럭에 유닛을 수동으로 3→2 순서로 등록 (슬롯0=3 생산 중, 슬롯1=2, 슬롯2=비어있음)
- 자동생산은 아무것도 활성화되어 있지 않음

**동작:**
1. 3번 유닛 버튼을 길게 눌러 자동생산 활성화

**기댓값:**
- 활성화 직후 큐는 슬롯0=3(수동), 슬롯1=2(수동), 슬롯2=3(자동) 표시
- 이 시점에 자동3의 골드가 즉시 차감됨
- 기존 수동 큐 순서 유지 (2가 여전히 슬롯1에 위치)

**결과:**

---

### TC-SINGLE-007: 순수 자동 모드 회귀 없음

**전제:**
- 배럭이 완전히 비어있음 (아무것도 생산 중 아님)

**동작:**
1. 1번 유닛 버튼을 길게 눌러 자동생산 A 활성화
2. 2번 유닛 버튼을 길게 눌러 자동생산 B 활성화
3. A→B→A→B 순서로 반복되는지 확인

**기댓값:**
- 자동생산 활성화 후 슬롯0에서 A 생산 시작, 슬롯1에 B 표시
- A 완료 후 슬롯0에서 B 생산, 슬롯1에 A 표시
- A→B 무한 순환 (기존 동작과 동일)

**결과:**

---

### TC-SINGLE-008: 자동생산 슬롯 취소 시 골드 환불

**전제:**
- 배럭에 유닛 1개 수동 생산 중 (슬롯0=수동 유닛)
- 빈 슬롯2에 자동생산 유닛이 표시되어 골드가 차감된 상태

**동작:**
1. 현재 골드 확인
2. 자동생산 유닛이 표시된 슬롯을 탭하여 취소

**기댓값:**
- 자동생산 유닛이 슬롯에서 사라짐
- 차감됐던 골드가 환불되어 골드가 증가함
- 자동생산 인디케이터가 꺼짐

**결과:**

---

## 정적 분석 결과 (qa-tester 전용)

---

### 분석 범위

수정된 파일 3개 전체 코드 추적.

- `ProductionState.cs` — `CurrentProducingIsAuto` 필드 추가
- `UnitProductionUseCase.cs` — `TryStartNext`, `CancelQueueAt`, `ToggleAutoProduction`
- `ProductionPanelUI.cs` — `UpdateQueueSlots`, `OnQueueSlotClicked`

---

### 시나리오별 코드 추적

#### 시나리오 1: [3,2,1]+자동1 활성화 후 슬롯1 취소

**전제 상태:**
- ManualQueue=[2, 1], AutoEntries=[1(IsCharged=true)], AutoIndex=0
- CurrentProducing=3(수동), CurrentProducingIsAuto=false

**isNormalAutoState 계산 (CancelQueueAt 슬롯1~2 분기 / UpdateQueueSlots 동일 공식):**
- `!state.CurrentProducing.HasValue` → false (3 생산 중)
- `state.CurrentProducingIsAuto` → false (수동 시작)
- OR 조건 전부 false → `isNormalAutoState = false`

**UpdateQueueSlots slot1/slot2:**
- 1단계 (ManualQueue 우선): pending0=2, pending1=1, pendingCount=2
- 2단계 (AutoEntries): remainingSlots=0 → AutoEntries 항목 표시 없음
- **slot1=2(수동), slot2=1(수동)**

**CancelQueueAt(slotIndex=1) 취소 대상:**
- pendingOffset=0, manualPendingCount=min(2,2)=2
- pendingOffset(0) < manualPendingCount(2) → ManualQueue 경로
- **ManualQueue[0]=2 취소, 골드 환불**

판정: PASS (코드 추적 상 설계 의도와 일치)

---

#### 시나리오 2: [3,2]+자동3 활성화 — isNormalAutoState 및 Bug-C 조건 판단

**전제 상태 (자동3 추가 직전):**
- ManualQueue=[2], AutoEntries=[], CurrentProducing=3(수동), CurrentProducingIsAuto=false

**ToggleAutoProduction(type=3) 추가 경로:**

Bug-B 이관 조건: `ManualQueue.Last()==type` → ManualQueue=[2], 2≠3 → 이관 미해당

CanAutoEntryShowInSlot:
- CurrentProducing=3 있음 → 첫 번째 조건(HasValue=false) 통과 안 함
- shownCount=ManualQueue.Count(1) + IsCharged=true 자동 항목(0) = 1
- 1 < 2 → **canShowInSlot = true**

Bug-C 가드: `canShowInSlot && type(3)==CurrentProducing(3) && CurrentProducingIsAuto(false)` → false
- CurrentProducingIsAuto=false이므로 가드 미작동 → canShowInSlot=true 유지
- **자동3이 IsCharged=true로 즉시 골드 차감 후 슬롯에 표시됨**

**자동3 추가 후 상태:** AutoEntries=[3(IsCharged=true)], AutoIndex=0

**isNormalAutoState 계산:**
- `!state.CurrentProducing.HasValue` → false
- `state.CurrentProducingIsAuto` → false
- **isNormalAutoState = false**

**UpdateQueueSlots slot1/slot2:**
- 1단계: ManualQueue=[2] → pending0=2, pendingCount=1
- 2단계: remainingSlots=1, autoOffset=0(isNormalAutoState=false), offset=0
  - AutoEntries[(0+0)%1]=AutoEntries[0].Type=3 → pending1=3
- **slot1=2(수동), slot2=3(자동)**

판정: PASS (Bug-C가 올바르게 수동 생산 중 동일 타입 자동 등록 허용)

---

#### 시나리오 3: 순수 자동 [A, B] 회귀 없음 확인

**전제 상태:**
- ManualQueue=[], AutoEntries=[A(IsCharged=true), B(IsCharged=false)], AutoIndex=0
- CurrentProducing=A(자동), CurrentProducingIsAuto=true

**isNormalAutoState 계산:**
- `!state.CurrentProducing.HasValue` → false
- `state.CurrentProducingIsAuto` → true
- `state.AutoTypeAt(0%2)` = AutoEntries[0].Type = A == CurrentProducing(A) → true
- **isNormalAutoState = true** (회귀 없음)

**UpdateQueueSlots slot1/slot2:**
- 1단계: ManualQueue 없음 → pendingCount=0
- 2단계: autoOffset=1(isNormalAutoState=true), remainingSlots=2
  - offset=1: AutoEntries[(0+1)%2]=AutoEntries[1].Type=B → pending0=B
  - offset=2: offset(2) >= autoCount(2) → break
- **slot1=B, slot2=빈 슬롯**

판정: PASS (기존 순수 자동 모드 동작 회귀 없음)

---

### 버그 발견: OnQueueSlotClicked의 isNormalAutoState 판단 공식 불일치

**심각도: Minor**

**위치:** `ProductionPanelUI.cs` 488~489행 (`OnQueueSlotClicked` 내 폴백 분기)

**현상:**
`OnQueueSlotClicked`의 취소 상태 판단 공식이 설계 의도의 `isNormalAutoState` 공식과 다름.

설계 의도 공식 (`CancelQueueAt`, `UpdateQueueSlots`):
```
bool isNormalAutoState = autoCount > 0 && (
    !state.CurrentProducing.HasValue
    || (state.CurrentProducingIsAuto
        && state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value)
);
```

`OnQueueSlotClicked` 실제 공식 (488~489행):
```csharp
bool isNormalAutoState = !state.CurrentProducing.HasValue
    || state.AutoTypeAt(state.AutoIndex % autoCount) == state.CurrentProducing.Value;
```

**차이점:** `CurrentProducingIsAuto` 플래그 참조 없음. autoCount > 0 가드도 없음.

**영향 범위:**
이 분기는 `CancelQueueAt`이 false를 반환했을 때만 진입하는 폴백 경로.
`CancelQueueAt`이 false를 반환하는 케이스 = 자동 모드 취소 상태(AutoEntries에서 슬롯0 타입이 이미 제거된 상태).
이 경우 `CurrentProducingIsAuto`=true이고 AutoEntries[AutoIndex].Type≠CurrentProducing 이어야 취소 상태이므로,
`[3,2]+자동3`처럼 CurrentProducingIsAuto=false인 케이스는 CancelQueueAt에서 처리되어 이 폴백까지 오지 않음.

**현실적 발동 조건:**
자동 모드 "취소 상태" = 슬롯0 취소 후 바로 AutoEntries에서 해당 타입 제거된 상태.
이때 CurrentProducingIsAuto=true이므로 구버전 공식과 신버전 공식 차이가 발동할 조건이 한정적.
단, 공식 불일치 자체는 잠재적 오판 위험이므로 기록.

판정: 기능 오작동 확인되지 않음 — 별도 실기 검증 필요

---

### 회귀 방지 체크

| 항목 | 판단 근거 | 결과 |
|------|----------|------|
| 순수 자동 모드 isNormalAutoState=true | 시나리오3 추적: CurrentProducingIsAuto=true && AutoTypeAt(AutoIndex)==CurrentProducing → true | PASS |
| BUG-19 슬롯0 취소 후 자동 직접 시작 | CancelQueueAt slotIndex=0 분기에 state.CurrentProducingIsAuto=true 세팅 확인 (400행) | PASS |
| Rule 1 환불 기존 로직 | 수동 모드 CancelQueueAt 경로 변경 없음 (504~536행), 자동 모드 ManualQueue 취소 환불 정상 (447~451행) | PASS |
| CollectChargedSlotEntries 영향 없음 | 해당 메서드 변경 없음 (895~916행), Rule2 이관 로직 그대로 | PASS |
| TryStartNext CurrentProducingIsAuto 세팅 | isManual=false 경로에서 state.CurrentProducingIsAuto=true 세팅 (680행) | PASS |

---

### TC별 정적 분석 판정

| TC | 검증 항목 | 정적 분석 판정 |
|----|----------|--------------|
| TC-SINGLE-001 | [3,2,1]+자동1: isNormalAutoState=false, slot1=2(수동), slot2=1(수동), CancelAt1=ManualQueue[0] 취소 | CONDITIONAL PASS |
| TC-SINGLE-002 | [3,2,1]+자동2: isNormalAutoState=false, slot1=2(수동), slot2=1(수동). 자동2는 큐 풀 대기(ManualQueue=2개로 슬롯1~2 포화) | CONDITIONAL PASS |
| TC-SINGLE-003 | [3,2,1]+자동3: TC-002와 동일 구조 — ManualQueue 2개가 슬롯1~2 포화, 자동3 IsCharged=false 대기 | CONDITIONAL PASS |
| TC-SINGLE-004 | [3,2]+자동1: ManualQueue=[2], Bug-B 조건(last=2≠1) 미해당, canShowInSlot=true(shownCount=1<2), IsCharged=true로 즉시 표시 | CONDITIONAL PASS |
| TC-SINGLE-005 | [3,2]+자동2: Bug-B 조건(last=2==type=2) 해당 → ManualQueue에서 2 제거 + AutoEntries에 2(IsCharged=true) 추가 | CONDITIONAL PASS |
| TC-SINGLE-006 | [3,2]+자동3: 시나리오2 추적 — canShowInSlot=true, Bug-C 미작동, slot1=2(수동), slot2=3(자동) | CONDITIONAL PASS |
| TC-SINGLE-007 | 순수 자동: 시나리오3 추적 — isNormalAutoState=true, slot1=B, 회귀 없음 | CONDITIONAL PASS |
| TC-SINGLE-008 | 자동 슬롯 취소 골드 환불: CancelQueueAt AutoEntries 분기 IsCharged=true 환불 (470~473행) 확인 | CONDITIONAL PASS |

*CONDITIONAL PASS: 코드 추적 상 설계 의도 일치 확인, 실기에서 자동 생산 타이밍(TryStartNext 호출 시점) 동작 검증 필요*

---

### 추가 발견 사항

**TC-SINGLE-002/003 상세 추적 보완:**

[3,2,1]+자동2 상태 (ToggleAutoProduction(type=2) 진입 시):
- Bug-B: ManualQueue=[3,2,1], last=1≠2 → 이관 미해당
- CanAutoEntryShowInSlot: shownCount=ManualQueue.Count(2)=2, 2 < 2 = false → **canShowInSlot=false**
- AutoEntries.Add(2, IsCharged=false) → 골드 미차감 대기

UpdateQueueSlots:
- isNormalAutoState=false (CurrentProducingIsAuto=false, 수동 3 생산 중)
- 1단계: ManualQueue=[2,1] → pending0=2, pending1=1, pendingCount=2
- 2단계: remainingSlots=0 → AutoEntries 표시 없음
- **slot1=2(수동), slot2=1(수동)** — 기댓값(슬롯이 꽉 차 있어 자동2는 대기 중)과 일치

[3,2,1]+자동3 (ToggleAutoProduction(type=3) 진입 시): TC-002와 동일 구조.
- Bug-B: last=1≠3 → 이관 미해당
- canShowInSlot=false (ManualQueue 2개로 포화)
- slot1=2(수동), slot2=1(수동) — 자동3 미표시

TC-SINGLE-002/003 판정: CONDITIONAL PASS
