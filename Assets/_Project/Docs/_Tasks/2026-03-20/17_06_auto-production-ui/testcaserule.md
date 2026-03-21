# Testcase Rule: 전역 규칙 기반 검증

> 전역 규칙 5가지를 기준으로 작성된 테스트 케이스.
> 검증 범위: ProductionState.cs + UnitProductionUseCase.cs (싱글플레이 전용).

## 전역 규칙 (기준)

1. **Rule 1** — 생산이 취소되면 항상 전액 환불
2. **Rule 2** — 자동생산이 취소되어도 생산큐에 등록(슬롯에 표시)된 것은 그대로 생산 — 자동 모드 해제 시 IsCharged=true 항목만 ManualQueue로 이관
3. **Rule 3** — 수동생산을 시행한 경우 모든 자동생산은 취소 — IsAutoMode=false, AutoEntries 클리어, 인디케이터 OFF
4. **Rule 4** — 생산큐는 최대 3개 — CurrentProducing + ManualQueue 기준 (자동 대기 AutoEntries는 별도, 큐 상한 무관)
5. **Rule 5** — 비용 차감은 슬롯에 표시되는 시점 — 슬롯 여유 있으면 자동 등록 시 즉시 차감(IsCharged=true), 큐 풀이면 미차감(IsCharged=false)이다가 슬롯 진입 시 차감

## 용어 정의

- **자동 등록**: 유닛 버튼 롱프레스 → AutoEntries에 추가, 인디케이터 ON
- **수동 추가**: 유닛 버튼 탭 → ManualQueue에 추가 (자동 모드 중 해당 타입 탭은 자동 취소)
- **슬롯0**: 현재 생산 중인 유닛 (CurrentProducing)
- **슬롯1~2**: 대기 중인 유닛 (ManualQueue 또는 AutoEntries에서 표시)
- **골드 차감됨**: AutoEntry.IsCharged=true (슬롯 진입 시 차감 완료)
- **골드 미차감**: AutoEntry.IsCharged=false (큐 풀 대기 중, 슬롯 진입 전)

## 공통 조건

- 각 케이스는 새 게임 또는 완전히 초기화된 상태에서 시작
- 자동 등록 = 롱프레스, 수동 추가 = 탭
- "자동 취소" = 자동 모드 ON 상태에서 해당 타입 버튼 탭 (ToggleAutoProduction 호출)

---

## R1. Rule 1: 생산 취소 시 전액 환불

### R1-1. 수동 생산 중인 유닛 슬롯0 취소 → 환불

- **전제**: Assault 수동 추가 → 생산 시작 (슬롯0=Assault 생산 중)
- **동작**: 슬롯0 클릭 (CancelQueueAt 슬롯0)
- **기댓값**:
  - CurrentProducing=null, ElapsedTime=0
  - Assault 골드 환불 (Rule 1: 수동 추가 시 차감됐으므로)
  - 큐 비어 있음
- **결과**:

### R1-2. 대기 중인 수동 항목 슬롯1 취소 → 환불

- **전제**: Assault(슬롯0 생산중) + Sniper(슬롯1 수동 대기)
- **동작**: 슬롯1 클릭 (CancelQueueAt 슬롯1)
- **기댓값**:
  - ManualQueue=[], Assault 생산 계속
  - Sniper 골드 환불 (Rule 1: 수동 추가 시 차감됨)
- **결과**:

### R1-3. 자동 등록 → 슬롯 진입 후 취소 → 환불

- **전제**: Pistoleer 자동 등록 → 생산 시작 (슬롯0=Pistoleer, IsCharged=true)
- **동작**: 슬롯0 클릭 (CancelQueueAt 슬롯0, 자동 모드)
- **기댓값**:
  - CurrentProducing=null, AutoEntries에서 Pistoleer 제거
  - AutoEntries 비어 있으면 IsAutoMode=false
  - Pistoleer 골드 환불 (Rule 1: 슬롯 진입 시 차감됐으므로)
- **결과**:

### R1-4. 자동 등록 → 슬롯에 표시된 대기 항목 취소 → 환불

- **전제**: Assault(슬롯0 자동 생산중, IsCharged=true) + Sniper(슬롯1 자동 대기, IsCharged=true)
- **동작**: 슬롯1 클릭 (CancelQueueAt 슬롯1, 자동 모드)
- **기댓값**:
  - AutoEntries에서 슬롯1 항목(Sniper) 제거
  - Sniper 골드 환불 (Rule 1: IsCharged=true이므로)
  - Assault 생산 계속
- **결과**:

---

## R2. Rule 2: 자동 취소 시 슬롯 표시 항목 이관

### R2-1. 자동 1개 → 인디케이터 OFF, 슬롯0 유지

- **전제**: Pistoleer 자동 등록 → 생산 시작 (슬롯0=Pistoleer, IsCharged=true)
  - AutoEntries=[Pistoleer(IsCharged=true)], AutoIndex=0, IsAutoMode=true
- **동작**: Pistoleer 버튼 탭 (자동 취소: ToggleAutoProduction(Pistoleer))
- **기댓값**:
  - AutoEntries=[], IsAutoMode=false, Pistoleer 인디케이터 OFF
  - [슬롯0] Pistoleer 생산 유지 (CurrentProducing은 건드리지 않음)
  - 빈 슬롯1~2 (ManualQueue 없음, AutoEntries 없음)
  - Pistoleer 골드 환불 (Rule 1: IsCharged=true이므로 취소 시 환불)
- **비고**: 슬롯0 Pistoleer는 AutoEntries에서 제거되므로 CompleteProduction 후 자동 순환 없음
- **결과**:

### R2-2. 자동 2개 → 슬롯0 타입 인디케이터 OFF, 슬롯1 항목은 ManualQueue 이관 없이 자동 유지

- **전제**: Assault(슬롯0 생산중, IsCharged=true) + Sniper(슬롯1 자동 대기, IsCharged=true)
  - AutoEntries=[Assault(IsCharged=true), Sniper(IsCharged=true)], AutoIndex=0
- **동작**: Assault 버튼 탭 (자동 취소: ToggleAutoProduction(Assault))
- **기댓값**:
  - AutoEntries=[Sniper(IsCharged=true)], AutoIndex=0, IsAutoMode=true (AutoEntries 0이 아님)
  - Assault 인디케이터 OFF, Sniper 인디케이터 ON
  - [슬롯0] Assault 생산 유지 (CurrentProducing 건드리지 않음)
  - [슬롯1] Sniper 표시 유지 (IsCharged=true 항목이 슬롯1에 남음)
  - Assault 골드 환불 (Rule 1: AutoEntries에서 IsCharged=true Assault 제거 시 환불)
- **비고**: 수동 추가가 아님, AutoEntries에서 타입만 제거하는 ToggleAutoProduction 경로
- **결과**:

### R2-3. 수동 추가 → 슬롯 표시 자동 항목이 ManualQueue로 이관됨 (Rule 2+3 연계)

- **전제**: Assault(슬롯0 생산중) + Sniper(슬롯1 자동 대기, IsCharged=true)
  - AutoEntries=[Assault(IsCharged=true), Sniper(IsCharged=true)], AutoIndex=0
- **동작**: Pistoleer 탭 (수동 추가: EnqueueUnit(Pistoleer))
- **기댓값**:
  - IsAutoMode=false, AutoEntries 클리어 (Rule 3)
  - ManualQueue=[Sniper, Pistoleer] — Sniper(IsCharged=true)가 앞에 이관, Pistoleer가 뒤에 추가 (Rule 2)
  - [슬롯0] Assault 생산 유지
  - [슬롯1] Sniper, [슬롯2] Pistoleer
  - 환불 없음 — Sniper는 이미 차감됐고 ManualQueue로 이관되어 생산 계속, Assault는 슬롯0 생산 중
- **결과**:

### R2-4. 수동 추가 시 큐 풀 대기 자동 항목(IsCharged=false)은 소멸, 환불 없음

- **전제**: Assault(슬롯0) + Sniper(슬롯1 자동 IsCharged=true) + Pistoleer(슬롯2 자동 IsCharged=true) + Sniper2번째(대기 IsCharged=false) — 자동 항목 3개
  - AutoEntries=[Assault(true), Sniper(true), Pistoleer(true)] → 이 경우 큐 풀, 대기 자동 항목 없음
  - 단순화: AutoEntries=[Assault(IsCharged=true), Sniper(IsCharged=false)] — Assault만 슬롯 진입, Sniper는 미차감
    - 큐=[슬롯0]Assault, 슬롯1=빈칸(ManualQueue없음), Sniper는 표시 안 됨
- **전제 재정의**: Assault 자동(슬롯0 생산중) + Pistoleer 수동(슬롯1) + Sniper 자동 등록(큐 풀, IsCharged=false)
  - AutoEntries=[Assault(IsCharged=true), Sniper(IsCharged=false)], ManualQueue=[Pistoleer]
- **동작**: Assault 탭 (수동 추가 시도: EnqueueUnit(Assault))
- **기댓값**:
  - CollectChargedSlotEntries: Pistoleer는 ManualQueue에 있고, AutoEntries에서 offset=1→Sniper IsCharged=false → 이관 대상 없음 → chargedEntries=[]
  - IsAutoMode=false, AutoEntries 클리어, Sniper 소멸, 환불 없음 (IsCharged=false이므로)
  - currentCount = 1(Assault) + 1(Pistoleer) = 2 → 2+1=3 ≤ 3 → 추가 허용
  - ManualQueue=[Pistoleer, Assault]
  - [슬롯0] Assault 생산 유지, [슬롯1] Pistoleer, [슬롯2] Assault(수동 대기)
- **결과**:

---

## R3. Rule 3: 수동 추가 시 자동 모드 취소

### R3-1. 자동 1개 중 수동 추가 → 슬롯0 유지, 슬롯1에 수동 추가

- **전제**: Pistoleer 자동 등록 → 생산 시작 (슬롯0=Pistoleer 생산중, IsCharged=true)
  - AutoEntries=[Pistoleer(IsCharged=true)], IsAutoMode=true
- **동작**: Assault 탭 (수동 추가: EnqueueUnit(Assault))
- **기댓값**:
  - IsAutoMode=false, AutoEntries 클리어 (Rule 3)
  - CollectChargedSlotEntries: offset=1, count=1 → 루프 미실행 → 이관 없음
  - ManualQueue=[Assault] (수동 항목만)
  - [슬롯0] Pistoleer 생산 유지, [슬롯1] Assault
  - 환불 없음 — Pistoleer는 생산 중(슬롯0, 차감됨), Assault는 방금 차감됨
- **결과**:

### R3-2. 자동 2개 중 수동 추가 → Sniper 이관 + Pistoleer 추가, 큐 구성 확인

- **전제**: Assault(슬롯0 생산중, IsCharged=true) + Sniper(슬롯1 자동 대기, IsCharged=true)
  - AutoEntries=[Assault(IsCharged=true), Sniper(IsCharged=true)], AutoIndex=0
- **동작**: Pistoleer 탭 (수동 추가: EnqueueUnit(Pistoleer))
- **기댓값**:
  - IsAutoMode=false, AutoEntries 클리어 (Rule 3)
  - CollectChargedSlotEntries: offset=1 → Sniper(IsCharged=true) 이관 → chargedEntries=[Sniper]
  - ManualQueue=[Sniper, Pistoleer] (Sniper 앞에 이관, Pistoleer 뒤에 추가)
  - [슬롯0] Assault 생산 유지, [슬롯1] Sniper, [슬롯2] Pistoleer
  - 환불 없음 (Sniper는 ManualQueue로 이관되어 생산 계속)
- **결과**:

### R3-3. 큐 풀 상태에서 수동 추가 시도 → 거부 (Rule 4)

- **전제**: Assault(슬롯0 생산중) + Sniper(슬롯1 수동) + Pistoleer(슬롯2 수동) — 큐 3개 풀
  - CurrentProducing=Assault, ManualQueue=[Sniper, Pistoleer]
- **동작**: Assault 탭 (수동 추가 시도: EnqueueUnit(Assault))
- **기댓값**:
  - IsAutoMode=false이므로 이관 없음 (자동 모드 블록 스킵)
  - currentCount = 1 + 2 = 3 → 3+1=4 > 3 → return false (수동 추가 거부)
  - 큐 변화 없음
- **결과**:

### R3-4. 자동 해제 후 수동으로 전환된 큐의 생산 순서

- **전제**: R3-2 이후 상태 ([슬롯0]Assault [슬롯1]Sniper [슬롯2]Pistoleer, IsAutoMode=false)
- **동작**: Assault 생산 완료 대기
- **기댓값**:
  - Assault 완료 → TryStartNext: ManualQueue=[Sniper, Pistoleer] → Sniper 생산 시작, ManualQueue=[Pistoleer]
  - [슬롯0] Sniper, [슬롯1] Pistoleer
  - Sniper 완료 → Pistoleer 생산 시작, ManualQueue=[]
  - Pistoleer 완료 → 생산 종료 (IsAutoMode=false, ManualQueue 없음)
- **결과**:

---

## R4. Rule 4: 생산큐 최대 3개 (CurrentProducing + ManualQueue 기준)

### R4-1. 큐가 3개일 때 자동 등록 → 등록 허용, 큐 변화 없음

- **전제**: [슬롯0]Assault(수동 생산중) + [슬롯1]Sniper(수동) + [슬롯2]Pistoleer(수동) — 큐 3개 풀
- **동작**: Assault 롱프레스 (자동 등록: ToggleAutoProduction(Assault))
- **기댓값**:
  - AutoEntries=[Assault(IsCharged=false)] — 큐 풀이므로 미차감 (Rule 5)
  - IsAutoMode=true, Assault 인디케이터 ON
  - ManualQueue=[Sniper, Pistoleer] 변화 없음, [슬롯0]Assault 생산 유지
  - 큐는 3개 그대로 (자동 대기는 큐 상한 무관)
- **결과**:

### R4-2. 이관 후 총 큐가 3개 초과 → 수동 추가 거부 (Rule 2+3+4 연계)

- **전제**: Assault(슬롯0 생산중) + Sniper(슬롯1 자동 IsCharged=true) + Pistoleer(슬롯2 자동 IsCharged=true)
  - AutoEntries=[Assault(IsCharged=true), Sniper(IsCharged=true), Pistoleer(IsCharged=true)], ManualQueue=[]
- **동작**: Assault 탭 (수동 추가 시도: EnqueueUnit(Assault))
- **기댓값**:
  - CollectChargedSlotEntries: offset=1→Sniper(true), offset=2→Pistoleer(true) → chargedEntries=[Sniper, Pistoleer]
  - ManualQueue=[Sniper, Pistoleer] 이관 후
  - currentCount = 1(Assault) + 2(ManualQueue) = 3 → 3+1=4 > 3 → return false (수동 추가 거부)
  - IsAutoMode은 이관 처리 전에 변경되지 않음 — **단, EnqueueUnit이 false를 반환하는 시점이 이관 후이므로 IsAutoMode=false, AutoEntries 클리어가 이미 수행된 상태**
  - 실제: AutoEntries 클리어 + ManualQueue=[Sniper, Pistoleer] 이관은 완료, Assault 미추가로 최종 ManualQueue=[Sniper, Pistoleer]
  - [슬롯0]Assault 생산 유지, [슬롯1]Sniper, [슬롯2]Pistoleer
- **결과**:

---

## R5. Rule 5: 비용 차감 시점 (슬롯 표시 시점)

### R5-1. 자동 등록 → 슬롯 여유 있으면 즉시 차감

- **전제**: Assault 자동 등록 → 생산 시작 (TryStartNext에서 생산 시작, IsCharged=true로 갱신됨)
  - 상태: AutoEntries=[Assault(IsCharged=true)], IsAutoMode=true
- **동작**: Sniper 롱프레스 (자동 등록: ToggleAutoProduction(Sniper))
- **기댓값**:
  - CanAutoEntryShowInSlot: CurrentProducing=Assault 있음, shownCount=1(Assault IsCharged=true) → 1 < 2 → true
  - Sniper 골드/인구 즉시 검증 + 차감 (슬롯1 표시 가능)
  - AutoEntries=[Assault(IsCharged=true), Sniper(IsCharged=true)]
  - [슬롯0]Assault, [슬롯1]Sniper 표시
- **결과**:

### R5-2. 자동 등록 → 큐 풀이면 미차감

- **전제**: Assault(슬롯0) + Sniper(슬롯1 자동 IsCharged=true) + Pistoleer(슬롯2 자동 IsCharged=true)
  - AutoEntries=[Assault(IsCharged=true), Sniper(IsCharged=true), Pistoleer(IsCharged=true)]
- **동작**: Assault 롱프레스 (자동 재등록 시도: ToggleAutoProduction(Assault))
  - 단, Assault는 이미 AutoEntries에 있으므로 → 제거 후 재등록 시나리오 불가
  - 수정 시나리오: Sniper 2번째 자동 등록 불가 (이미 있음) → 전제 재정의
- **전제 재정의**: Assault(슬롯0) + Sniper(슬롯1 자동 IsCharged=true) + ManualQueue=[Pistoleer] — 큐=3개 풀
  - AutoEntries=[Assault(IsCharged=true), Sniper(IsCharged=true)], ManualQueue=[Pistoleer]
- **동작**: Assault 롱프레스 (자동 등록: ToggleAutoProduction(Assault) — Assault는 이미 등록됨 → 제거됨)
  - 실제 의미 있는 테스트: **별도 전제** 사용
- **전제 최종**: Assault(슬롯0) + ManualQueue=[Sniper, Pistoleer] — 큐=3개 풀 (IsAutoMode=false)
- **동작**: Assault 롱프레스 (자동 등록: ToggleAutoProduction(Assault))
- **기댓값**:
  - CanAutoEntryShowInSlot: CurrentProducing=Assault 있음, shownCount=ManualQueue.Count(2)=2 → 2 < 2는 false → IsCharged=false
  - Assault 골드 미차감 (큐 풀 상태)
  - AutoEntries=[Assault(IsCharged=false)], IsAutoMode=true
  - 큐 변화 없음 ([슬롯0]Assault, [슬롯1]Sniper, [슬롯2]Pistoleer)
- **결과**:

### R5-3. 자동 대기 미차감 항목 → 슬롯 진입 시 골드 차감

- **전제**: R5-2 이후 상태. Assault(슬롯0) + ManualQueue=[Sniper, Pistoleer] + AutoEntries=[Assault(IsCharged=false)]
- **동작**: Assault 생산 완료 → Sniper 생산 완료 → Pistoleer 생산 완료
- **기댓값**:
  - Assault 완료 → TryStartNext: ManualQueue=[Sniper,Pistoleer] 우선 → Sniper 생산 시작 (골드 이미 차감됨)
  - ManualQueue=[Pistoleer]
  - Sniper 완료 → TryStartNext: ManualQueue=[Pistoleer] → Pistoleer 생산 시작
  - ManualQueue=[]
  - Pistoleer 완료 → TryStartNext: ManualQueue 없음, IsAutoMode=true → AutoEntries[AutoIndex=0]=Assault(IsCharged=false) → 골드/인구 검증 + **이 시점에 골드 차감**, IsCharged=true로 갱신 → Assault 생산 시작
  - 이후 Assault 자동 순환
- **결과**:

### R5-4. 자동 대기 취소 → 환불 없음 (미차감이었으므로)

- **전제**: R5-2 이후 상태. AutoEntries=[Assault(IsCharged=false)], IsAutoMode=true
- **동작**: Assault 버튼 탭 (자동 취소: ToggleAutoProduction(Assault))
- **기댓값**:
  - AutoEntries에서 Assault 제거, AutoEntries=[] → IsAutoMode=false
  - 환불 없음 (IsCharged=false → Rule 1 환불 조건 미충족)
  - ManualQueue=[Sniper, Pistoleer] 변화 없음
- **결과**:

---

## QA 결과

> 정적 분석 완료 (2026-03-22). 검증 범위: ProductionState.cs + UnitProductionUseCase.cs.

| 케이스 | PASS/FAIL | 비고 |
|--------|-----------|------|
| R1-1 | PASS | CancelQueueAt(0, 수동): CurrentProducing=null, 골드 환불 (cancelledType → AddGold) 정상 |
| R1-2 | PASS | CancelQueueAt(1, 수동): ManualQueue.RemoveAt(0), 골드 환불 정상 |
| R1-3 | PASS | CancelQueueAt(0, 자동): AutoEntries에서 제거, 빈 경우 IsAutoMode=false, 환불(Rule 1) 정상 |
| R1-4 | PASS | CancelQueueAt(1, 자동): AutoEntries 슬롯1 항목 제거, IsCharged=true이면 환불(Rule 1) 정상 |
| R2-1 | PASS | ToggleAutoProduction(Pistoleer): IsCharged=true → 환불, AutoEntries=[] → IsAutoMode=false, CurrentProducing 유지 |
| R2-2 | PASS | ToggleAutoProduction(Assault): Assault(IsCharged=true) 제거+환불, AutoEntries=[Sniper] 남음, IsAutoMode=true 유지, AutoIndex 보정 정상 |
| R2-3 | PASS | EnqueueUnit(Pistoleer): CollectChargedSlotEntries → Sniper(IsCharged=true) 이관, ManualQueue=[Sniper, Pistoleer], AutoEntries 클리어 (Rule 3) |
| R2-4 | PASS | EnqueueUnit: CollectChargedSlotEntries에서 IsCharged=false 항목 제외, IsAutoMode=false+AutoEntries 클리어, 환불 없음 |
| R3-1 | PASS | EnqueueUnit: IsAutoMode=true, CollectChargedSlotEntries count=1→offset=1 루프 미실행, chargedEntries=[], ManualQueue=[Assault], AutoEntries 클리어 |
| R3-2 | PASS | EnqueueUnit(Pistoleer): Sniper(IsCharged=true) 이관 → ManualQueue=[Sniper, Pistoleer], AutoEntries 클리어, Rule 3+2 정상 |
| R3-3 | PASS | EnqueueUnit: IsAutoMode=false이므로 이관 없음, currentCount=3 → 3+1>3 → false 반환 |
| R3-4 | PASS | TryStartNext: ManualQueue 우선 처리, FIFO 순서, 완료 후 생산 종료 정상 |
| R4-1 | PASS | ToggleAutoProduction(큐 풀): 큐 상한 체크 없음(Rule 4 AutoEntries 별도), CanAutoEntryShowInSlot → false → IsCharged=false |
| R4-2 | PASS | EnqueueUnit: 이관 처리 후 currentCount=3 → return false. ManualQueue=[Sniper,Pistoleer], IsAutoMode=false. 기능 동작 정상. ⚠️ TODO: "자동 해제됨+추가 실패" UX 피드백 — 향후 UI 작업에서 일괄 처리 |
| R5-1 | PASS | ToggleAutoProduction: CanAutoEntryShowInSlot=true → 즉시 골드 차감, IsCharged=true 정상 |
| R5-2 | PASS | ToggleAutoProduction(큐 풀): CanAutoEntryShowInSlot → shownCount=ManualQueue.Count(2)=2 → 2<2 false → IsCharged=false, 미차감 정상 |
| R5-3 | PASS | TryStartNext: ManualQueue 소진 후 AutoEntries[AutoIndex](IsCharged=false) → 이 시점 골드 차감+IsCharged=true 갱신 (L495~505), 순환 정상 |
| R5-4 | PASS | ToggleAutoProduction: IsCharged=false → 환불 없음(Rule 1 환불 조건 미충족), AutoEntries=[] → IsAutoMode=false |

### 종합 판정: PASS

**TODO:**
- R4-2: "자동 해제됨 + 추가 실패" 상황의 UX 피드백 — 향후 UI 작업에서 일괄 처리
