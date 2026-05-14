# Testcase — 자동생산 큐 pre-charge 버그

## TC 목록

---

### TC-SINGLE-001: 수동 큐 가득 찬 상태에서 자동생산 1개 활성화

**전제:**
- 배럭에 유닛 3개가 생산 대기 중 (슬롯0 생산중, 슬롯1·2 대기)
- 자동생산은 아무것도 활성화되어 있지 않음

**동작:**
1. 유닛 버튼을 길게 눌러 자동생산 유닛 1개를 활성화
2. 슬롯0 생산이 완료될 때까지 기다림

**기댓값:**
- 자동생산 활성화 직후에는 슬롯이 변하지 않음 (3칸 그대로)
- 슬롯0 생산 완료 순간, 슬롯1→슬롯0, 슬롯2→슬롯1로 이동하고 자동생산 유닛이 슬롯2에 즉시 표시됨
- 이 시점에 자동생산 유닛의 골드가 차감됨

**결과:** 3 2 1 로 추가해놓은 상태에서 1을 자동생산한뒤 첫번째큐부터 취소하면 3 1 2 1 순으로 취소되었음 -> 큐가 꼬임, 3 2 1 로 취소되는게 맞음 1을 취소할때 자동생산이 취소되어야함

3 2 1 로 추가해놓은 상태에서 2를 자동생산한뒤 첫번째큐부터 취소하면 3 2 2 1 순으로 취소되었음 -> 큐가 꼬임, 3 2 1 2 로 취소되는게 맞음 4번째 2가 취소될때 자동생산이 취소되어야함

3 2 1 로 추가해놓은 상태에서 3을 자동생산한뒤 첫번째큐부터 취소하면 3 2 1 순으로 취소되었음 -> 자동생산 로직이 꼬인것으로 보임, 3 2 1 3 으로 취소되어야함

> 정적 분석 근거: count=1, manualCount=2 시나리오 (CompleteProduction 직후 ManualQueue=[M2,M3])
> - autoStartOffset=0, maxAutoSlots=1, i=0, offset=0
> - `count==1 && offset>0` = false → break 없이 진행
> - AutoEntries[0].IsCharged=false → 골드 차감 + IsCharged=true 갱신 확인
>
> 단, "슬롯2에 표시"는 UpdateQueueSlots의 isNormalAutoState 판단(CurrentProducing≠AutoA → false)으로
> 인해 슬롯1에 표시될 수도 있음. 실기 확인 필요.

---

### TC-SINGLE-002: 수동 큐 가득 찬 상태에서 자동생산 2개 활성화

**전제:**
- 배럭에 유닛 3개가 생산 대기 중
- 자동생산은 아무것도 활성화되어 있지 않음

**동작:**
1. 유닛 버튼 A를 길게 눌러 자동생산 A 활성화
2. 유닛 버튼 B를 길게 눌러 자동생산 B 활성화
3. 슬롯0 생산이 완료될 때까지 기다림
4. 슬롯0 생산이 또 완료될 때까지 기다림

**기댓값:**
- 슬롯0 첫 번째 완료 시: 슬롯2에 자동생산 A가 표시됨, A 골드 차감
- 슬롯0 두 번째 완료 시: 슬롯2에 자동생산 B가 표시됨, B 골드 차감
- 수동 큐가 모두 소진된 후 자동생산 A, B가 순환 반복됨

**결과:** 

3 2 1 로 추가해놓은 상태에서 3을 자동생산하고 2를 자동생산하면 2를 자동생산할 때 큐가 3 2 2 로 변경되고 첫번째 큐부터 취소하면 3 2 2 1 순으로 취소됨 -> 큐가 꼬임, 3 2 1 3 2 순으로 취소되어야함

경우의 수가 많아서 이후 테스트는 생략함 TC-SINGLE-001 과 더불어 전반적인 로직 검증이 필요함

> 정적 분석 근거:
>
> 첫 번째 완료 시 (manualCount=2, count=2):
> - maxAutoSlots=1, autoStartOffset=0
> - i=0: idx=(0+0)%2=0 → A(false) → 골드 차감, A(true) 확인
>
> 두 번째 완료 시 (manualCount=1, count=2):
> - maxAutoSlots=2, autoStartOffset=0
> - i=0: idx=0 → A(true) → IsCharged=true → continue
> - i=1: offset=1, `count==1 && offset>0` → 2≠1, false. idx=1 → B(false) → 골드 차감 확인
>
> 단, "슬롯2에 표시" 슬롯 번호는 UpdateQueueSlots 배치 순서에 따라 달라질 수 있음. 실기 확인 필요.

---

### TC-SINGLE-003: 수동 큐 1개 남은 상태에서 자동생산 활성화

**전제:**
- 배럭에 유닛 1개만 생산 대기 중 (슬롯0 생산중, 슬롯1에만 수동 유닛)
- 슬롯2는 비어 있음

**동작:**
1. 유닛 버튼을 길게 눌러 자동생산 유닛을 활성화

**기댓값:**
- 활성화 즉시 자동생산 유닛이 슬롯2에 표시됨 (빈 슬롯이 있으므로)
- 이 시점에 골드 차감

**결과:** 3 2 가 추가된 상태에서 1을 자동생산하면 3 1 2 로 큐가 변경되었음 -> 3 2 1로 큐가 되면서 1은 자동생산이 유지 되어야함

3 2 가 추가된 상태에서 2를 자동생산하면 3 2 2로 큐가 변경되었음 -> 3 2 로 큐가 유지되며 자동생산으로 골드가 소모되지 않고 3이 생산된 후 2는 자동생산이 유지되어야함. 또한, 2가 생산된 후 2가 추가될때 골드를 소모함.

3 2가 추가된 상태에서 3을 자동생산하면 3 2 로 큐가 유지됨. -> 3 2 3 으로 큐가 추가되며 3번째 큐는 자동생산으로 인한 큐임. 

> 정적 분석 근거:
> ToggleAutoProduction 호출 시 CanAutoEntryShowInSlot:
> - CurrentProducing.HasValue=true, shownCount=ManualQueue.Count=1, 1<2 → canShowInSlot=true
> - 즉시 골드 차감, isCharged=true로 등록 확인
>
> 단, UpdateQueueSlots의 isNormalAutoState 판단:
> - CurrentProducing=Manual0, AutoTypeAt(0)=AutoA, Manual0≠AutoA → isNormalAutoState=false
> - 이 경우 AutoEntries 항목이 pending0(슬롯1), ManualQueue[0]이 pending1(슬롯2)로 배치됨
> - 즉 "자동생산 유닛이 슬롯2에 표시" 기댓값과 달리 슬롯1에 표시될 가능성 있음
> - 실기 확인 필요 (골드 차감 자체는 코드상 정상)

---

### TC-SINGLE-004: 순수 자동생산 모드 동작 유지 (회귀)

**전제:**
- 배럭이 완전히 비어 있음 (아무것도 생산 중 아님)

**동작:**
1. 유닛 버튼 A를 길게 눌러 자동생산 A 활성화
2. 유닛 버튼 B를 길게 눌러 자동생산 B 활성화
3. 자동생산이 A → B → A → B 순서로 반복되는지 관찰

**기댓값:**
- 자동생산 활성화 후 슬롯0에서 A 생산 시작, 슬롯1에 B 표시
- A 완료 후 슬롯0에서 B 생산, 슬롯1에 A 표시
- A → B 무한 순환 (기존 동작과 동일)

**결과:** PASS

> 정적 분석 근거: count=1, manualCount=0 시나리오 (TryPreChargeAutoEntries 내)
> - autoStartOffset=1, maxAutoSlots=2
> - i=0: offset=1, `count==1 && offset>0` = true → break (중복 충전 방지)
> - A의 IsCharged 중복 갱신 없음. 기존 동작과 동일하게 단일 항목은 TryStartNext에서만 차감.
>
> count=2(A+B) 시: A 완료 후 AutoIndex=1(B), TryPreChargeAutoEntries
> - autoStartOffset=1, maxAutoSlots=2
> - i=0: offset=1, `offset>=count` → 1>=2 → false, idx=(1+1)%2=0 → A(false 리셋됨) → 골드 차감
> - i=1: offset=2, `offset>=count` → 2>=2 → break
> - 슬롯1에 A 표시됨, 회귀 없음 확인

---

### TC-SINGLE-005: 자동생산 취소 시 골드 환불 정상 처리

**전제:**
- 수동 유닛 1개 생산 중, 자동생산 유닛이 슬롯2에 표시되어 골드가 차감된 상태

**동작:**
1. 슬롯2(자동생산 유닛)를 탭하여 취소

**기댓값:**
- 자동생산 유닛이 슬롯2에서 사라짐
- 차감되었던 골드가 환불됨

**결과:** CONDITIONAL PASS

> 정적 분석 근거:
> TC-SINGLE-003 이후 상태: CurrentProducing=ManualUnit, ManualQueue=[M1], AutoEntries=[AutoA(true)]
>
> UpdateQueueSlots 기준 실제 슬롯 배치:
> - isNormalAutoState=false (CurrentProducing≠AutoA)
> - 슬롯1=AutoA, 슬롯2=M1 (AutoEntries가 ManualQueue보다 우선 표시됨)
>
> TC 전제의 "슬롯2에 자동생산 유닛" 표현이 실제 배치(슬롯1=AutoA)와 다를 수 있음.
>
> CancelQueueAt(barracksId, 1) 호출 시 (슬롯1이 AutoA인 경우):
> - pendingOffset=0, autoPendingCount=Math.Min(1,2)=1
> - 0 < 1 → AutoEntry 취소 분기
> - targetIdx=(0+0+0)%1=0, AutoEntries[0]=AutoA(true), IsCharged=true → 환불 실행 확인
>
> 환불 로직 자체(CancelQueueAt 내 Rule 1)는 이번 수정 범위 밖이며 기존 코드에 정상 구현됨.
> 실기에서 슬롯 번호 확인 후 취소 버튼을 눌러야 함.

---

## 정적 분석 결과 (qa-tester)

### 검증 대상
`TryPreChargeAutoEntries()` (UnitProductionUseCase.cs:769~826)

---

### 체크리스트 항목별 판정

| # | 검증 항목 | 판정 | 근거 |
|---|-----------|------|------|
| 1 | count=1, manualCount=2 시나리오 충전 실행 | PASS | offset=0, `count==1 && offset>0` = false → 충전 진행됨 (line 802) |
| 2 | count=1, manualCount=0 시나리오 break 확인 | PASS | autoStartOffset=1, offset=1, `count==1 && offset>0` = true → break (line 802) |
| 3 | count=2, manualCount=2 첫 완료 충전 확인 | PASS | maxAutoSlots=1, i=0, offset=0, idx=0 → A(false) 충전됨 |
| 4 | count=2, manualCount=1 두 번째 완료 충전 확인 | PASS | maxAutoSlots=2, i=1, offset=1, idx=1 → B(false) 충전됨 (A는 continue) |
| 5 | `charged` 변수 완전 제거 확인 | PASS | `charged++`, `int charged` 패턴 없음 (Grep 확인) |
| 6 | ManualQueue 수정 없음 확인 | PASS | TryPreChargeAutoEntries 내 ManualQueue 쓰기 연산 없음, `.Count` 읽기만 사용 |
| 7 | break vs continue 일관성 확인 | PASS | 자원 부족 break (line 818, 821), IsCharged=true continue (line 813) |

---

### 발견된 이슈

#### NOTE-001 (Minor): TC-SINGLE-001/003/005 슬롯 번호 표현 불일치 가능성

**내용:**
TC-SINGLE-001, 003, 005의 기댓값에서 "슬롯2에 자동생산 유닛이 표시됨"이라고 서술되어 있으나,
UpdateQueueSlots의 실제 슬롯 배치 순서는 AutoEntries 항목이 ManualQueue보다 항상 우선 배치된다.

ManualQueue가 CurrentProducing 중인 경우(isNormalAutoState=false), AutoEntries[AutoIndex]가
pending0(슬롯1)에 매핑되고 ManualQueue[0]이 pending1(슬롯2)에 매핑된다.

따라서 수동 유닛 생산 중에 자동생산을 활성화하면 실제로는 **슬롯1에 자동유닛, 슬롯2에 수동유닛** 순서로 표시될 수 있다.

**영향 범위:** TryPreChargeAutoEntries 수정 범위 밖. UpdateQueueSlots 기존 로직의 동작 방식.
**판정:** TC 기댓값 서술이 실기와 다를 수 있으므로 실기 확인 시 슬롯 번호 유연하게 검증 필요.

---

### 종합 판정

| TC | 판정 |
|----|------|
| TC-SINGLE-001 | CONDITIONAL PASS |
| TC-SINGLE-002 | CONDITIONAL PASS |
| TC-SINGLE-003 | CONDITIONAL PASS |
| TC-SINGLE-004 | PASS |
| TC-SINGLE-005 | CONDITIONAL PASS |

**전체 판정: CONDITIONAL PASS**

TryPreChargeAutoEntries 수정 코드는 7개 체크리스트 항목 전부 정적 분석 통과.
TC-SINGLE-004(순수 자동 모드 회귀)는 코드 흐름상 완전히 보장됨 → PASS.
나머지 4개 TC는 골드 차감 로직 자체는 코드상 정상이나,
UpdateQueueSlots의 슬롯 배치 순서(AutoEntries 우선)로 인해 기댓값의 슬롯 번호 서술과
실제 표시 위치가 다를 수 있어 실기 확인이 필요함.
