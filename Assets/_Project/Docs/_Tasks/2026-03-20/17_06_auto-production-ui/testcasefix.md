# Testcase Fix: 자동생산 UI 버그 수정 검증

> 수정된 로직에 직접 영향받는 케이스만 추출.
> 수정 무관 또는 이번 수정과 독립적인 TC-01~03, TC-07, TC-08 등은 제외.

## 공통 조건
- **생산 취소 시 항상 전액 환불**
- 각 케이스는 새 게임에서 시작하거나 이전 상태가 완전히 초기화된 후 테스트

---

## 수정된 코드 영역

| 영역 | 수정 내용 |
|------|----------|
| `ToggleAutoProduction` | AutoTypes 비었을 때 CurrentProducing 취소 제거 → 슬롯0 항상 생산 유지 |
| `EnqueueUnit` | 수동 추가 시 CurrentProducing 취소 제거 + 선불 환불 오프셋 수정 |
| `CancelQueueAt` | 빈 슬롯 방어 추가, AutoIndex 보정 개선 |
| `SetupQueueSlotButtons` | 리스너 중복 등록 방지 (`RemoveAllListeners`) |

---

## A. 수동 슬롯 취소 정확성

### A-1. 수동 3개 예약 후 슬롯1 취소 → 슬롯1만 제거
- **전제**: 수동으로 Pistoleer(슬롯0 생산 중) + Assault(슬롯1) + Sniper(슬롯2) 예약
- **동작**: 슬롯1 클릭
- **기댓값**: Assault만 취소+환불, Pistoleer 생산 유지, Sniper → 슬롯1로 이동
- **결과**: ✅ PASS

### A-2. 슬롯1 취소 후 슬롯1 재취소
- **전제**: A-1 이후 큐 = [Pistoleer(생산중), Sniper(슬롯1)]
- **동작**: 슬롯1 재클릭
- **기댓값**: Sniper만 취소+환불, Pistoleer 생산 유지
- **결과**: ✅ PASS

---

## B. 자동 버튼 취소 시 슬롯0 생산 유지

### B-1. 자동 1개 — 버튼 탭 → 슬롯0 생산 유지
- **전제**: Pistoleer 자동 등록, 생산 중
- **동작**: Pistoleer 버튼 탭
- **기댓값**: AutoTypes 비워짐, IsAutoMode=false, Pistoleer 생산 **계속 진행**, 환불 없음
- **결과**: ✅ PASS

### B-2. 자동 2개 — 슬롯0 버튼 탭 → 해당 타입만 취소, 슬롯1 유지
- **전제**: Assault(슬롯0 생산 중) + Sniper(슬롯1 선불)
- **동작**: Assault 버튼 탭
- **기댓값**: Assault AutoTypes에서 제거, Sniper AutoTypes 유지(환불 없음), Assault 생산 유지
  > ※ Sniper를 취소하려면 Sniper 버튼을 별도로 탭해야 함
- **결과**: ✅ PASS

### B-3. 자동 2개 — 슬롯1 버튼 탭 → 슬롯1만 취소+환불, 슬롯0 유지
- **전제**: Assault(슬롯0 생산 중) + Sniper(슬롯1 선불)
- **동작**: Sniper 버튼 탭
- **기댓값**: Sniper AutoTypes 제거 + 선불 환불, Assault 생산 유지
- **결과**: ✅ PASS

---

## C. 자동 슬롯 직접 클릭 취소

### C-1. 자동 2개 — 슬롯1 클릭 → Sniper만 취소
- **전제**: Assault(슬롯0 생산 중) + Sniper(슬롯1)
- **동작**: 슬롯1 클릭
- **기댓값**: Sniper 취소+환불, Assault 생산 유지, 슬롯1 빈칸
- **결과**: ✅ PASS

### C-2. 자동 3개 — 슬롯2 클릭 → Pistoleer만 취소
- **전제**: Assault(슬롯0) + Sniper(슬롯1) + Pistoleer(슬롯2)
- **동작**: 슬롯2 클릭
- **기댓값**: Pistoleer 취소+환불, Assault·Sniper 유지
- **결과**: ✅ PASS

---

## D. 수동생산 추가 시 슬롯0 유지 (DESIGN-03)

### D-1. 자동 1개 생산 중 수동 추가 → 슬롯0 완료 후 수동 생산
- **전제**: Pistoleer 자동 생산 중
- **동작**: Assault 버튼 탭 (수동 추가)
- **기댓값**: IsAutoMode=false, Pistoleer 생산 유지 → Pistoleer 완료 후 Assault 생산
- **결과**: ✅ PASS

### D-2. 자동 2개 중 수동 추가 → 선불 환불 정확성
- **전제**: Assault(슬롯0 생산 중) + Sniper(슬롯1 선불 200골드)
- **동작**: Pistoleer 버튼 탭 (수동 추가)
- **기댓값**: Sniper 선불 200골드 환불, Assault 생산 유지, 수동 큐에 Pistoleer 추가
- **결과**: ✅ PASS

---

## E. 빈 슬롯 클릭 방어

### E-1. 자동 1개 — 슬롯1(빈칸) 클릭 → 아무 변화 없음
- **전제**: Pistoleer 자동 등록 (슬롯1·2 빈칸)
- **동작**: 슬롯1 클릭
- **기댓값**: 아무 변화 없음 (Pistoleer 생산 유지)
- **결과**: ✅ PASS

### E-2. 자동 2개 — 슬롯2(빈칸) 클릭 → 아무 변화 없음
- **전제**: Assault + Sniper 등록 (슬롯2 빈칸)
- **동작**: 슬롯2 클릭
- **기댓값**: 아무 변화 없음
- **결과**: ✅ PASS

---

## F. 연속 동작 시나리오 (핵심)

### F-1. 자동 3개 등록 후 슬롯 취소 → 재등록 → 슬롯 취소
- **동작 순서**:
  1. Assault → Sniper → Pistoleer 롱프레스 (3개 등록)
  2. 슬롯2(Pistoleer) 클릭 취소
  3. Pistoleer 롱프레스 재등록
  4. 슬롯1(Sniper) 클릭 취소
- **기댓값**: 각 단계에서 슬롯 표시 정확, 환불 정확, 생산 순서 정확
- **결과**: ✅ PASS

### F-2. 자동 취소 → 수동 추가 → 자동 재등록
- **동작 순서**:
  1. Assault 롱프레스 (자동 등록, 생산 시작)
  2. Assault 버튼 탭 (자동 취소) → Assault 생산 계속
  3. Pistoleer 탭 (수동 추가) → Assault 완료 후 Pistoleer 생산
  4. Pistoleer 생산 완료 후 Sniper 롱프레스 (자동 재등록)
- **기댓값**: 각 전환 시 상태 초기화 정상, 자동 재등록 후 인디케이터 ON, 생산 연속
- **결과**: ✅ PASS

### F-3. 여러 취소 동작 이후 상태 오염 없음
- **동작 순서**:
  1. Assault → Sniper 등록
  2. 슬롯1(Sniper) 취소
  3. Sniper 재등록
  4. Assault 버튼 탭 (슬롯0 취소)
  5. Assault 생산 완료 후 Sniper 자동생산 확인
- **기댓값**: Sniper 자동생산 정상 순환, AutoIndex 오염 없음
- **결과**: ✅ PASS

---

## QA 결과

| 케이스 | PASS/FAIL | 비고 |
|--------|-----------|------|
| A-1 | ✅ PASS | 수동 슬롯1 취소 → Assault 제거, Sniper 유지, 환불 정확 |
| A-2 | ✅ PASS | 수동 슬롯1 재취소 → Sniper 제거, Pistoleer 생산 유지 |
| B-1 | ✅ PASS | 자동 1개 버튼 탭 → IsAutoMode=false, 슬롯0 생산 유지, 환불 없음 |
| B-2 | ✅ PASS | 자동 2개 슬롯0 탭 → AutoTypes에서만 제거, Sniper 유지, 환불 없음 |
| B-3 | ✅ PASS | 자동 2개 슬롯1 탭 → Sniper 제거 + 선불 환불, Assault 생산 유지 |
| C-1 | ✅ PASS | 자동 2개 슬롯1 클릭 → Sniper 취소+환불, Assault 유지 |
| C-2 | ✅ PASS | 자동 3개 슬롯2 클릭 → Pistoleer 취소+환불, 나머지 유지 |
| D-1 | ✅ PASS | 자동→수동 전환 시 IsAutoMode=false, Pistoleer 생산 유지 |
| D-2 | ✅ PASS | 자동 2개 중 수동 추가 → Sniper 선불 정확 환불, Assault 유지 |
| E-1 | ✅ PASS | 자동 1개 빈 슬롯1 클릭 → count<2 방어로 조기 반환 |
| E-2 | ✅ PASS | 자동 2개 빈 슬롯2 클릭 → count<3 방어로 조기 반환 |
| F-1 | ✅ PASS | 3개 등록→슬롯2 취소→재등록→슬롯1 취소: AutoIndex/PreCharge 정합 |
| F-2 | ✅ PASS | 자동→취소→수동→완료→자동재등록: 각 단계 상태 정상 초기화 |
| F-3 | ✅ PASS | 취소/재등록 반복 후 AutoIndex 오염 없음, Sniper 순환 생산 정상 |
