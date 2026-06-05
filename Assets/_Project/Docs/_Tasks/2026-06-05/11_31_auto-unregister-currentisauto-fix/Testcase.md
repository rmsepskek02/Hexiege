# Testcase — 자동생산 재등록 슬롯 버그 수정

작성일: 2026-06-05

---

## 공통 조건

- 싱글플레이 에디터에서 테스트
- 각 TC는 새 게임에서 시작하거나 배럭을 새로 배치한 뒤 진행

---

### SINGLE-TC-01: 자동 해제 후 재등록 — 슬롯2 중복 없음 (케이스 A)

**전제:** 배럭에서 Assault 자동생산이 활성화되어 슬롯1에서 생산 중이다.

**동작:**
1. Assault 유닛 버튼을 탭하여 자동생산을 해제한다.
2. Assault 유닛 버튼을 롱프레스하여 자동생산을 재등록한다.

**기댓값:**
- 슬롯1은 Assault가 생산 중인 상태를 유지한다.
- 슬롯2에 Assault가 중복으로 나타나지 않는다.
- 자동 인디케이터가 다시 켜진다.

**결과:** PASS (정적 분석)
- 해제 시 AutoTypes=[] → getter의 세 번째 조건(AutoTypes.Contains) = false → CurrentIsAuto=false 자동 반영
- 재등록 시 PendingQueue.Count==0 조건 충족 → TryConvertCurrentToAuto 경로 진입
- TryConvertCurrentToAuto: CurrentIsAuto=false 확인 후 슬롯0을 자동 전환, 새 슬롯 추가 없음
- 슬롯2 중복 없음 확인

PASS

---

### SINGLE-TC-02: 수동 추가 후 슬롯 취소 → 재등록 — 슬롯2 중복 없음 (케이스 B)

**전제:** 배럭에서 Assault 자동생산이 활성화되어 슬롯1에서 생산 중이다.

**동작:**
1. Pistoleer 유닛 버튼을 탭하여 수동 생산으로 추가한다 (슬롯2에 Pistoleer 표시).
2. 슬롯2(Pistoleer)를 직접 클릭하여 취소한다.
3. Assault 유닛 버튼을 롱프레스하여 자동생산을 재등록한다.

**기댓값:**
- 슬롯1은 Assault가 생산 중인 상태를 유지한다.
- 슬롯2에 Assault가 중복으로 나타나지 않는다.
- 자동 인디케이터가 다시 켜진다.

**결과:** PASS (정적 분석)
- 수동 추가(EnqueueUnit) 시 DisableAutoMode → AutoTypes=[] → _currentIsAutoFlag는 true로 남아있어도 getter가 false 반환
- Pistoleer 취소 후 PendingQueue=[]
- 재등록 시 PendingQueue.Count==0 조건 충족 → TryConvertCurrentToAuto 경로 진입
- CurrentIsAuto getter = false(AutoTypes 비어있음) → 전환 성공, 새 슬롯 추가 없음

PASS

---

### SINGLE-TC-03: 수동이 큐에 있는 채로 재등록 — 슬롯3에 추가 (케이스 C)

**전제:** 배럭에서 Assault 자동생산이 활성화되어 슬롯1에서 생산 중이다.

**동작:**
1. Pistoleer 유닛 버튼을 탭하여 수동 생산으로 추가한다 (슬롯2에 Pistoleer 표시).
2. Assault 유닛 버튼을 롱프레스하여 자동생산을 재등록한다.

**기댓값:**
- 슬롯1: Assault (생산 중)
- 슬롯2: Pistoleer (대기)
- 슬롯3: Assault (대기)
- 자동 인디케이터가 켜진다.

**결과:** PASS (정적 분석)
- 수동 추가 시 DisableAutoMode → AutoTypes=[], PendingQueue=[Pistoleer(수동,차감)]
- 재등록 시 PendingQueue.Count==1 → TryConvertCurrentToAuto 조건(Count==0) 미충족 → 건너뜀
- TryConvertLastPendingToAuto: 마지막=Pistoleer, Type 불일치 → 건너뜀
- AddNewAutoSlot: canShow=true(ChargedPendingCount=1 < 2), BUG-15 조건 미충족(CurrentIsAuto=false)
- PendingQueue=[Pistoleer, Assault(자동,차감)] → 슬롯2=Pistoleer, 슬롯3=Assault 정상 배치

PASS

---

### SINGLE-TC-04: 자동 해제 후 재등록 없음 — 생산 계속

**전제:** 배럭에서 Assault 자동생산이 활성화되어 슬롯1에서 생산 중이다.

**동작:**
1. Assault 유닛 버튼을 탭하여 자동생산을 해제한다.
2. 아무것도 하지 않고 생산이 완료될 때까지 기다린다.

**기댓값:**
- Assault 생산이 완료된다.
- 생산 완료 후 큐가 비어있고 다음 자동 생산이 시작되지 않는다.

**결과:** PASS (정적 분석)
- 해제 시 AutoTypes=[] → getter의 AutoTypes.Contains=false
- CompleteProduction L676: wasAuto = state.CurrentIsAuto → getter 계산 → false (AutoTypes 비어있음)
- if (wasAuto && AutoTypes.Contains(type)) → false → 재추가 없음
- TryStartNext: PendingQueue 비어있음, IsAutoMode=false → 생산 미시작
- 큐 비어있음 확인, 자동 재생산 없음 확인

PASS

---

### SINGLE-TC-05: 자동생산 정상 순환 — 수정 후 기존 동작 유지

**전제:** 배럭이 비어있다.

**동작:**
1. Assault 유닛 버튼을 롱프레스하여 자동생산을 등록한다.
2. 생산이 완료될 때까지 기다린다.
3. 생산 완료 후 다시 Assault가 자동으로 시작되는지 확인한다.

**기댓값:**
- 슬롯1에서 Assault가 반복 생산된다.
- 슬롯2에 Assault가 순간적으로 깜빡이는 현상이 없다.

**결과:** PASS (정적 분석)
- 등록 시 AddNewAutoSlot → canShow=false(CurrentProducing 없음) → TryStartNext 즉시 호출 → 슬롯0 직행
- wasAuto getter: _currentIsAutoFlag=true, CurrentProducing=Assault, AutoTypes.Contains=true → true
- CompleteProduction: wasAuto=true, AutoTypes.Contains=true → PendingQueue.Add(Assault, auto, uncharged)
- TryStartNext: PendingQueue[0]=Assault(auto, uncharged) → 골드 차감 → 생산 시작 → 순환 정상
- PendingQueue가 항상 0개 이하 유지되므로 슬롯2 깜빡임 없음
- 수정 전후 동작 동일 — regression 없음

PASS

---

### SINGLE-TC-06: Rule 20 정상 동작 — 수동 Assault 생산 중 자동 등록

**전제:** Assault를 수동으로 생산 중이다 (자동 없음).

**동작:**
1. Assault 유닛 버튼을 탭하여 수동 생산을 추가한다.
2. 생산이 시작되면 Assault 버튼을 롱프레스하여 자동을 등록한다.

**기댓값:**
- 슬롯2에 Assault가 중복으로 추가되지 않는다.
- 슬롯1의 Assault가 자동으로 전환된다 (자동 인디케이터 켜짐).
- Assault 생산 완료 후 자동으로 재시작된다.

**결과:** PASS (정적 분석)
- 수동 생산 중 상태: CurrentProducing=Assault(수동), _currentIsAutoFlag=false, PendingQueue=[Assault(수동,차감)] 또는 PendingQueue=[]

  (TC 동작 1: 수동 탭 → PendingQueue에 Assault 추가. 단, 전제 "Assault를 수동으로 생산 중이다"가 이미 슬롯0에서 진행 중인 상태이므로, 동작 1의 탭으로 PendingQueue=[Assault(수동,차감)]이 됨)

- 롱프레스 시 RegisterAutoType → PendingQueue.Count==1 → TryConvertCurrentToAuto 조건 미충족
- TryConvertLastPendingToAuto: last=Assault(IsAuto=false, Type=Assault) → 조건 충족 → IsAuto=true로 전환
- AutoTypes.Add(Assault), 새 슬롯 추가 없음 → 슬롯2 중복 없음
- AutoTypes=[Assault] → 자동 인디케이터 켜짐
- 슬롯0 완료 후 wasAuto=false → 재추가 없음 / 슬롯1(Assault, IsAuto=true) TryStartNext → CurrentIsAuto=true 설정 → 완료 후 wasAuto=true → 재추가 → 자동 순환 시작

PASS

---

## QA 섹션

### 정적 분석 결과 (qa-tester) — 2026-06-05

#### 분석 범위
- `ProductionState.cs` — CurrentIsAuto getter/setter 구조 변경
- `UnitProductionUseCase.cs` — RegisterAutoType, UnregisterAutoType, DisableAutoMode, CompleteProduction

---

#### 요청 사항별 검증

**1. 6개 TC 코드 흐름 추적 — 전체 PASS**

| TC | 핵심 경로 | 판정 |
|----|----------|------|
| TC-01 | TryConvertCurrentToAuto (PendingQueue.Count==0 조건 충족) | PASS |
| TC-02 | DisableAutoMode → Pistoleer 취소 → TryConvertCurrentToAuto | PASS |
| TC-03 | PendingQueue.Count==1 → TryConvertCurrentToAuto 건너뜀 → AddNewAutoSlot | PASS |
| TC-04 | CompleteProduction wasAuto getter = false → 재추가 없음 | PASS |
| TC-05 | 정상 순환 경로 — regression 없음 | PASS |
| TC-06 | TryConvertLastPendingToAuto 경로 → 슬롯2 중복 없음 | PASS |

---

**2. CurrentIsAuto getter 세 조건 시나리오별 검증**

| 시나리오 | _currentIsAutoFlag | CurrentProducing.HasValue | AutoTypes.Contains | 결과 |
|---------|-------------------|--------------------------|-------------------|------|
| 자동 생산 중 (정상) | true | true | true | true (정상) |
| 자동 해제 직후 (핵심 수정) | true | true | false | **false** (버그 수정 효과) |
| 자동 완료 후 초기화 | false | false | - | false (정상) |
| 수동 생산 중 | false | true | false | false (정상) |
| 슬롯0 취소 후 | false | false | - | false (정상) |

- 세 조건의 단락 평가(short-circuit)가 올바르게 작동함. `_currentIsAutoFlag=false`이면 나머지 조건 평가 불필요.
- 특히 "자동 해제 직후" 시나리오에서 `_currentIsAutoFlag`가 true로 남아있어도 getter가 false를 반환하는 것이 이번 수정의 핵심 효과이며 정상 확인.

---

**3. wasAuto 캡처 시점 검증 (CompleteProduction L676)**

```
bool wasAuto = state.CurrentIsAuto;   // ← getter 호출
state.CurrentProducing = null;         // ← 이 이후에 초기화
state.CurrentIsAuto = false;           // ← _currentIsAutoFlag 초기화
```

getter 평가 시점: `CurrentProducing.HasValue=true`, `AutoTypes` 상태는 해제 여부에 따라 다름.
- 자동 해제 후: `AutoTypes=[]` → getter=false → wasAuto=false → 재추가 없음 (TC-04 확인)
- 자동 유지 중: `AutoTypes=[type]` → getter=true → wasAuto=true → 재추가 정상 (TC-05 확인)

`wasAuto` 캡처 순서는 `state.CurrentProducing = null` 이전이므로 올바름. 문제 없음.

---

**4. TryConvertCurrentToAuto 진입 시 AutoTypes.Contains 결과 검증**

`TryConvertCurrentToAuto`는 `RegisterAutoType` 내에서 아래 조건이 모두 충족될 때 호출됨:
- `state.AutoTypes.IndexOf(type) < 0` (ToggleAutoProduction에서 미등록 타입만 RegisterAutoType 호출)
- `state.PendingQueue.Count == 0` (2026-06-05 추가 조건)

`AutoTypes.IndexOf(type) < 0`이 전제이므로 `AutoTypes.Contains(type) = false`가 보장됨.

따라서 `TryConvertCurrentToAuto` 내부에서 `!state.CurrentIsAuto` 체크 시:
- getter의 세 번째 조건 `AutoTypes.Contains(type) = false` → getter=false → `!state.CurrentIsAuto = true` → 전환 진입 가능

단, 이 시점에서 다른 타입이 AutoTypes에 있고 `_currentIsAutoFlag=true`인 경우도 있을 수 있음.
예: AutoTypes=[Pistoleer], 현재 Assault 수동 생산 중 → Assault 자동 등록 시도.
- `AutoTypes.Contains(Assault) = false` → getter=false → TryConvertCurrentToAuto 진입 시도
- `CurrentProducing.Value == Assault (true)`, `CurrentIsAuto=false` → 전환 성공
- 결과: Assault가 자동으로 전환되고 AutoTypes=[Pistoleer, Assault]가 됨 — 의도한 동작.

**전제 조건 만족: AutoTypes.Contains 결과가 항상 false임이 보장됨.**

---

**5. Regression 검증**

- Rule 20 (수동→자동 전환): TC-06에서 TryConvertLastPendingToAuto 경로 확인 — 정상
- 자동 순환: TC-05에서 CompleteProduction → TryStartNext 반복 경로 확인 — 정상
- 취소/환불: CancelCurrentProducing, CancelPendingSlot의 wasAuto 캡처 로직은 수정 대상 아님 — 영향 없음
- DisableAutoMode (Rule 3): PendingQueue 자동 항목 처리 로직 수정 없음, AutoTypes.Clear만 수행 — regression 없음

---

**6. 발견된 버그**

없음.

---

#### 종합 판정: PASS (정적 분석)

**전체 6개 TC 코드 흐름 추적 결과 전원 PASS.**

- `CurrentIsAuto` getter의 파생 계산 방식이 세 TC 케이스(A, B, C) 모두에서 올바르게 작동함.
- `wasAuto` 캡처 타이밍 정상.
- `TryConvertCurrentToAuto` 진입 조건(`PendingQueue.Count==0`)이 케이스 C에서 AddNewAutoSlot으로 올바르게 우회됨.
- 기존 정상 동작(Rule 20, 자동 순환, 취소/환불)에 regression 없음.
