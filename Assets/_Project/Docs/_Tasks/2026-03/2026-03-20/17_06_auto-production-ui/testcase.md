# Testcase: 자동생산 UI 개선

## QA 결과 요약
| 항목 | 결과 |
|------|------|
| 총 케이스 | 37 |
| PASS | 32 (1·2·3·4차 — TC-11-2 설계 확정 PASS 포함) |
| CONDITIONAL PASS | 0 |
| FAIL | 0 |
| 미테스트 | 5 (TC-09-1~3 멀티플레이 실기) |

---

## TC-01. 버튼별 자동 인디케이터 표시

### TC-01-1. 자동 등록 시 해당 버튼 인디케이터 활성화
- **전제**: 배럭 클릭 → 생산 패널 오픈
- **동작**: Pistoleer 버튼 롱프레스
- **기댓값**: Pistoleer 버튼 위 인디케이터만 ON, Assault/Sniper 인디케이터는 OFF
- **결과**: ⬜

### TC-01-2. 복수 등록 시 각각 인디케이터 활성화
- **동작**: Pistoleer 롱프레스 → Assault 롱프레스
- **기댓값**: Pistoleer + Assault 인디케이터 모두 ON, Sniper만 OFF
- **결과**: ⬜

### TC-01-3. 자동 취소 시 인디케이터 비활성화
- **전제**: Pistoleer 자동 등록 상태
- **동작**: Pistoleer 버튼 탭(취소)
- **기댓값**: Pistoleer 인디케이터 OFF
- **결과**: ⬜

### TC-01-4. 자동 모드 완전 해제 시 전체 인디케이터 OFF
- **전제**: Pistoleer + Assault 자동 등록 상태
- **동작**: 두 버튼 모두 탭(취소)
- **기댓값**: 모든 인디케이터 OFF
- **결과**: ⬜

---

## TC-02. 자동모드 큐 슬롯 표시

### TC-02-1. 자동 1개 등록 — 슬롯 0만 표시
- **동작**: Pistoleer 롱프레스 → 생산 시작 대기
- **기댓값**: 슬롯 0 = Pistoleer(생산 중), 슬롯 1~2 = 빈칸
- **결과**: ⬜

### TC-02-2. 자동 2개 등록 — 슬롯 0~1 표시
- **동작**: Assault → Sniper 순서로 롱프레스
- **기댓값**: 슬롯 0 = Assault(생산 중), 슬롯 1 = Sniper(예약), 슬롯 2 = 빈칸
- **결과**: ⬜

### TC-02-3. 자동 3개 등록 — 슬롯 0~2 모두 표시
- **동작**: Assault → Sniper → Pistoleer 순서로 롱프레스
- **기댓값**: 슬롯 0 = Assault(생산 중), 슬롯 1 = Sniper, 슬롯 2 = Pistoleer
- **결과**: ⬜

### TC-02-4. 생산 완료 후 슬롯 순환
- **전제**: Assault → Sniper → Pistoleer 등록, Assault 생산 중
- **동작**: Assault 생산 완료 대기
- **기댓값**: 슬롯 0 = Sniper(생산 시작), 슬롯 1 = Pistoleer, 슬롯 2 = Assault(다음 순환)
- **결과**: ⬜

---

## TC-03. 최대 3개 제한

### TC-03-1. 4번째 등록 시 거부
- **전제**: Pistoleer + Assault + Sniper 모두 자동 등록
- **동작**: Pistoleer 롱프레스(4번째 시도 — 이미 있는 타입 재등록은 제거이므로, 실제로는 다른 시나리오 필요)
  > ※ 3종류 유닛만 존재하므로 각기 다른 타입 4개 등록 시나리오는 현재 불가.
  > 현재 코드는 `AutoTypes.Count >= 3`에서 추가 거부 — 3가지 등록 후 새 타입 추가 시도 시 false 반환 확인 불가 (유닛 3종 = 최대 3개이므로 정상).
- **기댓값**: 3개 등록 후 인디케이터 3개 모두 ON, 추가 등록 불가
- **결과**: ⬜

---

## TC-04. 버튼 탭 동작 분기

### TC-04-1. 자동모드 OFF 상태에서 탭 → 수동 큐 추가
- **전제**: 자동 등록 없는 상태
- **동작**: Pistoleer 버튼 탭
- **기댓값**: ManualQueue에 Pistoleer 추가, 슬롯 0에 생산 시작
- **결과**: ⬜

### TC-04-2. 자동모드 ON 상태에서 등록된 타입 탭 → 자동 취소
- **전제**: Pistoleer 자동 등록, 생산 중(슬롯 0)
- **동작**: Pistoleer 버튼 탭
- **기댓값**: Pistoleer AutoTypes에서 제거, 현재 생산은 계속 진행, 인디케이터 OFF
- **결과**: ⬜

### TC-04-3. 자동모드 ON 상태에서 미등록 타입 탭 → 수동 큐 추가
- **전제**: Assault 자동 등록 상태
- **동작**: Pistoleer 버튼 탭
- **기댓값**: ManualQueue에 Pistoleer 추가 실패 (자동 모드 ON이고 ManualQueue 추가 시 자동모드 해제), 자동 모드 OFF → Assault 자동 취소 + 골드 환불
  > ※ EnqueueUnit 내부에서 IsAutoMode 해제 로직 동작 확인
- **결과**: ⬜

---

## TC-05. 롱프레스 동작 분기

### TC-05-1. 자동모드 OFF 상태에서 롱프레스 → 자동 등록
- **동작**: Pistoleer 0.5초 이상 롱프레스
- **기댓값**: Pistoleer AutoTypes에 추가, 인디케이터 ON
- **결과**: ⬜

### TC-05-2. 자동모드 ON 상태에서 등록된 타입 롱프레스 → 탭과 동일 취소
- **전제**: Pistoleer 자동 등록(슬롯 1 또는 2)
- **동작**: Pistoleer 0.5초 롱프레스
- **기댓값**: Pistoleer AutoTypes에서 제거 + 선불 골드 환불, 인디케이터 OFF
- **결과**: ⬜

---

## TC-06. 슬롯 직접 클릭 취소

### TC-06-1. 자동모드 — 슬롯 0 클릭 → 현재 생산 즉시 취소
- **전제**: Assault 자동 등록, 생산 중(슬롯 0)
- **동작**: 슬롯 0 클릭
- **기댓값**: 현재 생산 취소 + 골드 환불 + AutoTypes에서 Assault 제거, AutoTypes 비면 자동모드 OFF
- **결과**: ⬜

### TC-06-2. 자동모드 — 슬롯 1 클릭 → 해당 타입 제거 + 환불
- **전제**: Assault(슬롯 0) + Sniper(슬롯 1) 등록
- **동작**: 슬롯 1 클릭
- **기댓값**: Sniper AutoTypes에서 제거 + 선불 골드 환불, 슬롯 1 빈칸
- **결과**: ⬜

### TC-06-3. 자동모드 — 슬롯 2 클릭 → 해당 타입 제거 + 환불
- **전제**: Assault(슬롯 0) + Sniper(슬롯 1) + Pistoleer(슬롯 2) 등록
- **동작**: 슬롯 2 클릭
- **기댓값**: Pistoleer AutoTypes에서 제거 + 선불 골드 환불, 슬롯 2 빈칸
- **결과**: ⬜

### TC-06-4. 수동모드 — 슬롯 0 클릭 → 기존 수동 취소 로직 유지
- **전제**: 수동으로 Pistoleer 생산 중
- **동작**: 슬롯 0 클릭
- **기댓값**: 현재 생산 취소 + 골드 환불
- **결과**: ⬜

---

## TC-07. 골드 선불 / 환불

### TC-07-1. 자동 2번째 등록 시 골드 선불 차감
- **전제**: Assault 자동 등록 후 생산 시작 상태 (골드 확인 필요)
- **동작**: Sniper 롱프레스 (2번째 등록)
- **기댓값**: Sniper 비용만큼 골드 즉시 차감 (선불)
- **결과**: ⬜

### TC-07-2. 골드 부족 시 자동 등록 실패
- **전제**: 골드가 Sniper 비용보다 적은 상태, Assault 자동 생산 중
- **동작**: Sniper 롱프레스
- **기댓값**: 등록 거부 (인디케이터 변화 없음), 골드 차감 없음
- **결과**: ⬜

### TC-07-3. 슬롯 1~2 취소 시 선불 골드 환불
- **전제**: Assault(슬롯 0) + Sniper(슬롯 1) 등록, Sniper 선불 차감 상태
- **동작**: Sniper 탭(취소)
- **기댓값**: Sniper 비용만큼 골드 환불, AutoPreChargedCount 감소
- **결과**: ⬜

### TC-07-4. 슬롯 0 취소 시 환불 없음 (생산 계속)
- **전제**: Assault 자동 등록, 슬롯 0 생산 중
- **동작**: Assault 탭(슬롯 0 취소)
- **기댓값**: Assault AutoTypes에서 제거, 현재 생산 계속, 골드 환불 없음
- **결과**: ⬜

---

## TC-08. AutoIndex 관리

### TC-08-1. 슬롯 0 버튼 취소 후 생산 완료 시 AutoIndex 올바른 순서
- **전제**: Assault(idx0), Sniper(idx1), Pistoleer(idx2) 등록. Assault 생산 중 (AutoIndex=0)
- **동작**: Assault 탭(AutoTypes에서 제거) → Assault 생산 완료 대기
- **기댓값**: AutoTypes = [Sniper, Pistoleer], Assault 완료 후 Sniper(idx0) 생산 시작 (AutoIndex 증가 안 함, Sniper부터 시작)
- **결과**: ⬜

### TC-08-2. 슬롯 중간 제거 후 AutoIndex 보정
- **전제**: Assault(0) + Sniper(1) + Pistoleer(2), AutoIndex=0, Assault 생산 중
- **동작**: 슬롯 1(Sniper) 클릭 취소
- **기댓값**: AutoTypes = [Assault, Pistoleer], AutoIndex 범위 초과 없음
- **결과**: ⬜

---

## TC-09. 멀티플레이 자동 생산 동기화

### TC-09-1. 클라이언트에서 롱프레스 → 서버 자동 등록 동기화
- **전제**: 멀티플레이 세션, 클라이언트(Red) 배럭 선택
- **동작**: Sniper 롱프레스
- **기댓값**: 서버에서 ToggleAutoProduction(Sniper) 실행, 클라이언트 인디케이터 ON, 큐 슬롯 갱신
- **결과**: ⬜

### TC-09-2. 멀티플레이에서 유닛 타입이 올바르게 전달 (기존 버그 수정 확인)
- **전제**: 멀티플레이 세션
- **동작**: Sniper 롱프레스(자동 등록) → 생산 완료 대기
- **기댓값**: Sniper가 생산됨 (기존 버그: 항상 Pistoleer 생산되던 문제 없음)
- **결과**: ⬜

### TC-09-3. 멀티플레이에서 클라이언트 큐 슬롯 동기화
- **전제**: 멀티플레이 세션, 호스트(Blue) 자동 생산 등록
- **동작**: Assault → Sniper 롱프레스
- **기댓값**: 호스트와 클라이언트 모두 동일한 큐 슬롯 표시 (슬롯0=Assault, 슬롯1=Sniper)
- **결과**: ⬜

---

## 버그 수정 이력

### 2026-03-21 수정 (game-programmer)
**수정 원인**: AutoIndex가 "현재 생산 중인 타입"을 가리키는데, 슬롯 1~2 계산에서 "다음 예정"으로 잘못 해석

| 파일 | 수정 위치 | 변경 내용 |
|------|----------|----------|
| `ProductionPanelUI.cs` | `UpdateQueueSlots()` 슬롯 1~2 | 슬롯 1: `AutoTypes[AutoIndex%count]` → `AutoTypes[(AutoIndex+1)%count]` |
| `ProductionPanelUI.cs` | `UpdateQueueSlots()` 슬롯 2 | 슬롯 2: `AutoTypes[(AutoIndex+1)%count]` → `AutoTypes[(AutoIndex+2)%count]` |
| `UnitProductionUseCase.cs` | `CancelQueueAt()` 슬롯 1~2 | `autoSlotOffset = slotIndex - 1` → `autoSlotOffset = slotIndex` |

---

## QA 에이전트 테스트 결과

### 1차 (2026-03-20) — 정적 분석

| 케이스 | PASS/FAIL | 비고 |
|--------|-----------|------|
| TC-01-1 | PASS | UpdateUI(): `state.AutoTypes.Contains(UnitType.Pistoleer)` 기반 SetActive 정확히 동작 |
| TC-01-2 | PASS | 각 타입별 독립 인디케이터 SetActive 조건이 분리되어 있어 복수 등록 시 각각 ON |
| TC-01-3 | PASS | ToggleAutoProduction → AutoTypes 제거 → OnProductionQueueChanged → UpdateUI → SetActive(false) |
| TC-01-4 | PASS | AutoTypes 비면 IsAutoMode=false → 전체 인디케이터 조건(`IsAutoMode &&`) false → 모두 OFF |
| TC-02-1 | PASS | 슬롯 0=CurrentProducing, count<2이므로 슬롯 1·2=null → 빈칸 정확함 |
| TC-02-2 | FAIL | [버그] 슬롯 1 표시 오류. AutoIndex=0 생산 중, `AutoTypes[AutoIndex%count]`=AutoTypes[0]=Assault(생산 중과 동일). 기댓값: 슬롯 1=Sniper. 실제: 슬롯 1=Assault 중복 표시 |
| TC-02-3 | FAIL | [버그] TC-02-2와 동일 원인. AutoIndex=0일 때 슬롯 1=AutoTypes[0]=Assault, 슬롯 2=AutoTypes[1]=Sniper. 기댓값과 1칸씩 어긋남 |
| TC-02-4 | FAIL | [버그] CompleteProduction 후 AutoIndex=(0+1)%3=1. 슬롯 1=AutoTypes[1%3]=Sniper(OK), 슬롯 2=AutoTypes[(1+1)%3]=Pistoleer(OK). 단, 슬롯 2에 Assault(순환)가 보여야 한다는 기댓값은 코드상 불가. 슬롯 2=AutoTypes[2]=Pistoleer이므로 순환 후 Assault는 미표시. 기댓값의 "슬롯 2=Assault(다음 순환)" 미충족 |
| TC-03-1 | PASS | `AutoTypes.Count >= 3` 시 return false. 유닛 3종 = 최대 3개이므로 정상 제한 |
| TC-04-1 | PASS | IsAutoMode=false, AutoTypes 미포함 → EnqueueUnit 경로 타고 ManualQueue 추가 |
| TC-04-2 | PASS | isAutoForType=true → HandleToggleAuto → ToggleAutoProduction(isSlot0=true) → AutoTypes 제거, 생산 계속, 환불 없음. 인디케이터 OFF |
| TC-04-3 | PASS | isAutoForType=false(Pistoleer 미등록) → EnqueueUnit → IsAutoMode=true이므로 EnqueueUnit 내부에서 선불 환불 + AutoTypes.Clear() + 자동 모드 해제 + 현재 자동 생산 취소(환불) |
| TC-05-1 | PASS | isAutoForType=false → HandleToggleAuto → ToggleAutoProduction(미등록) → AutoTypes에 추가, IsAutoMode=true, 인디케이터 ON |
| TC-05-2 | PASS | isAutoForType=true → HandleToggleAuto → ToggleAutoProduction(isSlot0 여부 판단) → 슬롯 1~2면 환불. 인디케이터 OFF |
| TC-06-1 | PASS | CancelQueueAt slotIndex=0, IsAutoMode=true → 현재 생산 취소 + 골드 환불 + AutoTypes에서 제거. 비면 IsAutoMode=false |
| TC-06-2 | FAIL | [버그] CancelQueueAt 슬롯 1: `targetIdx=(AutoIndex+0)%count=0` → AutoTypes[0]=Assault 삭제. 기댓값: Sniper 삭제. UI도 슬롯 1에 Assault 표시하므로(TC-02-2 버그 연동) 취소 대상 타입이 표시와는 일치하나 설계 의도(Sniper 취소)와 다름 |
| TC-06-3 | FAIL | [버그] 슬롯 2: `targetIdx=(AutoIndex+1)%3=1` → AutoTypes[1]=Sniper 삭제. 기댓값: Pistoleer 삭제. TC-02-3 버그와 연동하여 UI 표시와 취소 대상 모두 기댓값과 1칸 어긋남 |
| TC-06-4 | PASS | IsAutoMode=false → 수동 모드 분기, CancelQueueAt slotIndex=0 → CurrentProducing 취소 + 환불 |
| TC-07-1 | PASS | ToggleAutoProduction: needPreCharge=CurrentProducing.HasValue=true → 골드 즉시 차감 + AutoPreChargedCount+=1 |
| TC-07-2 | PASS | ToggleAutoProduction needPreCharge 분기: `!_resource.CanAfford(...)` → return false. 골드 차감 없음, AutoTypes 변경 없음 |
| TC-07-3 | FAIL | [버그] TC-06-2와 동일 원인. 탭(취소) 시 ToggleAutoProduction → isSlot0=false(Sniper는 AutoTypes[1]) → 환불 처리는 올바름. 단 취소 대상이 idx 기반으로 정확히 Sniper를 찾는지 확인: `idx = AutoTypes.IndexOf(Sniper) = 1`, `isSlot0 = (CurrentProducing==Sniper && idx==AutoIndex(0))` = false → 환불 OK. 이 TC는 PASS. 재판정: PASS |
| TC-07-4 | PASS | ToggleAutoProduction: isSlot0=true → AutoTypes 제거만, 생산 계속, 환불 없음 |
| TC-08-1 | FAIL | [버그] CompleteProduction: `AutoTypes.Contains(type)` 체크 시 Assault가 AutoTypes에서 제거된 상태 → AutoIndex 증가 안 함 = AutoIndex=0. AutoTypes=[Sniper, Pistoleer], 다음 TryStartNext: AutoTypes[0]=Sniper 생산 시작. 기댓값 일치. 단 슬롯 표시(UpdateQueueSlots)는 여전히 TC-02-2 버그 영향으로 슬롯 1·2 표시가 어긋남 |
| TC-08-2 | FAIL | [버그] 슬롯 1(Sniper) CancelQueueAt: `targetIdx=(0+0)%3=0` → AutoTypes[0]=Assault 삭제(잘못된 타입). AutoIndex 보정도 이 버그에서 파생되어 불정확. 기댓값: Sniper 삭제, AutoTypes=[Assault, Pistoleer] |
| TC-09-1 | 미테스트 | 멀티플레이 실기 필요. 코드 흐름: HandleToggleAuto → ToggleAutoServerRpc(barracksId, (int)type, teamIndex) 파라미터 올바름. 서버에서 ToggleAutoProduction(unitType) 실행 후 AutoProductionChangedClientRpc 전파 — 흐름 정상 |
| TC-09-2 | 미테스트 | 멀티플레이 실기 필요. 코드 분석: ToggleAutoServerRpc에 unitTypeInt 파라미터 명시적 전달, `UnitType unitType = (UnitType)unitTypeInt`로 변환 후 ToggleAutoProduction 실행. 기존 하드코딩 버그 수정 확인됨 |
| TC-09-3 | 미테스트 | 멀티플레이 실기 필요. SyncQueueStateClientRpc에서 AutoTypes, AutoIndex 전체 동기화 확인됨. TC-02-2 버그로 인해 양측 슬롯 표시가 동일하게 잘못될 가능성 있음 |

### 2차 (2026-03-21) — 버그 수정 후 재검증 (정적 분석)

| 케이스 | PASS/FAIL | 비고 |
|--------|-----------|------|
| TC-02-2 | PASS | AutoIndex=0, count=2. 슬롯 1=AutoTypes[(0+1)%2]=AutoTypes[1]=Sniper. 기댓값 일치 |
| TC-02-3 | PASS | AutoIndex=0, count=3. 슬롯 1=AutoTypes[1]=Sniper, 슬롯 2=AutoTypes[2]=Pistoleer. 기댓값 일치 |
| TC-02-4 | PASS | CompleteProduction 후 AutoIndex=1. 슬롯 0=Sniper, 슬롯 1=AutoTypes[(1+1)%3]=Pistoleer, 슬롯 2=AutoTypes[(1+2)%3]=Assault. testcase.md 기댓값(슬롯 2=Assault 다음 순환) 일치 |
| TC-06-2 | PASS | slotIndex=1. autoSlotOffset=1. targetIdx=(0+1)%2=1 → AutoTypes[1]=Sniper 삭제 + 환불. 기댓값 일치 |
| TC-06-3 | PASS | slotIndex=2. autoSlotOffset=2. targetIdx=(0+2)%3=2 → AutoTypes[2]=Pistoleer 삭제 + 환불. 기댓값 일치 |
| TC-08-1 | PASS | Assault 탭 취소 후 AutoTypes=[Sniper, Pistoleer], AutoIndex=0. 생산 완료 시 Contains(Assault)=false → AutoIndex 증가 안 함 → TryStartNext: AutoTypes[0]=Sniper 생산 시작. 기댓값 일치 |
| TC-08-2 | PASS | slotIndex=1. targetIdx=(0+1)%3=1 → AutoTypes[1]=Sniper 삭제. 결과: AutoTypes=[Assault, Pistoleer], AutoIndex=0 (범위 정상). 기댓값 일치 |

**경계값 검증**
- count=1: `count >= 2` 조건으로 슬롯 1 분기 미진입. 안전.
- count=2: `count >= 3` 조건으로 슬롯 2 분기 미진입. 안전.

**종합 판정: PASS** — 1차 FAIL 7건 전체 수정 확인. 수정 전 오류(AutoIndex 기준 오프셋 불일치)가 두 파일에서 일관되게 수정됨.

---

## TC-10. 수동 슬롯 취소 정확성 (3차 신규)

### TC-10-1. 수동 3개 예약 후 슬롯1만 취소
- **전제**: 수동으로 Pistoleer(슬롯0) + Assault(슬롯1) + Sniper(슬롯2) 예약
- **동작**: 슬롯1 클릭
- **기댓값**: Assault만 취소 + 환불, Pistoleer(슬롯0) 생산 유지, Sniper(슬롯2→슬롯1)로 이동
- **결과**: ⬜

### TC-10-2. 수동 3개 예약 후 슬롯1 취소 후 슬롯1 재취소
- **전제**: 수동 3개 예약, 슬롯1 취소 완료 후 큐=[Pistoleer(생산중), Sniper(슬롯1)]
- **동작**: 슬롯1 재클릭
- **기댓값**: Sniper 취소 + 환불, Pistoleer 생산 유지
- **결과**: ⬜

---

## TC-11. 자동 버튼 취소 시 슬롯0 생산 유지 (3차 신규)

### TC-11-1. 자동 1개 — 버튼 탭 취소 → 슬롯0 생산 유지
- **전제**: Pistoleer 자동 등록, 생산 중
- **동작**: Pistoleer 버튼 탭
- **기댓값**: AutoTypes 비워짐, IsAutoMode=false, Pistoleer 생산 **계속 진행**, 환불 없음
- **결과**: ⬜

### TC-11-2. 자동 2개 — 슬롯0 버튼 탭 취소 → 슬롯0 생산 유지, 슬롯1 유지
- **전제**: Assault(슬롯0) + Sniper(슬롯1) 등록
- **동작**: Assault 버튼 탭 (슬롯0 취소)
- **기댓값**: Assault AutoTypes에서만 제거, Sniper는 AutoTypes에 유지(환불 없음), Assault 생산 유지
  > ※ 버튼 탭은 해당 타입만 취소. 나머지 슬롯은 독립적으로 유지됨.
- **결과**: ⬜

### TC-11-3. 자동 2개 — 슬롯1 버튼 탭 취소 → 슬롯0 생산 유지, 슬롯1 환불
- **전제**: Assault(슬롯0) + Sniper(슬롯1) 등록
- **동작**: Sniper 버튼 탭 (슬롯1 취소)
- **기댓값**: Sniper AutoTypes 제거 + 선불 환불, Assault 생산 유지
- **결과**: ⬜

---

## TC-12. 수동생산 추가 시 자동생산 슬롯0 유지 (3차 신규 — DESIGN-03)

### TC-12-1. 자동 1개 생산 중 수동 추가 → 슬롯0 완료 후 수동 생산
- **전제**: Pistoleer 자동 생산 중
- **동작**: Assault 버튼 탭 (수동 큐 추가)
- **기댓값**: IsAutoMode=false, AutoTypes 클리어, Pistoleer 생산 유지, Assault ManualQueue 추가 → Pistoleer 완료 후 Assault 생산
- **결과**: ⬜

### TC-12-2. 자동 2개 중 수동 추가 → 선불 환불 + 슬롯0 유지
- **전제**: Assault(슬롯0) + Sniper(슬롯1) 등록, Sniper 선불 차감 상태
- **동작**: Pistoleer 버튼 탭 (수동 큐 추가)
- **기댓값**: IsAutoMode=false, Sniper 선불 환불, Assault 생산 유지, Pistoleer ManualQueue 추가
- **결과**: ⬜

---

## TC-13. 빈 슬롯 클릭 방어 (3차 신규)

### TC-13-1. 자동 1개 등록 시 슬롯1 클릭 → 아무 효과 없음
- **전제**: Pistoleer 자동 등록(슬롯0만 표시)
- **동작**: 슬롯1(빈칸) 클릭
- **기댓값**: 아무 변화 없음 (방어 로직으로 early return)
- **결과**: ⬜

### TC-13-2. 자동 2개 등록 시 슬롯2 클릭 → 아무 효과 없음
- **전제**: Assault + Sniper 등록 (슬롯2 빈칸)
- **동작**: 슬롯2(빈칸) 클릭
- **기댓값**: 아무 변화 없음
- **결과**: ⬜

---

## TC-14. 연속 동작 시나리오 (3차 신규)

### TC-14-1. 자동 등록 → 슬롯 취소 → 재등록 연속 동작
- **전제**: Assault → Sniper → Pistoleer 등록
- **동작**: 슬롯2(Pistoleer) 취소 → Pistoleer 재등록 → 슬롯1(Sniper) 취소
- **기댓값**: 각 단계에서 AutoTypes/AutoIndex/AutoPreChargedCount 정합 유지, 슬롯 표시 정확
- **결과**: ⬜

### TC-14-2. 자동 취소 후 수동 추가 → 자동 재등록
- **전제**: Assault 자동 생산 중
- **동작**: Assault 버튼 탭(자동 취소) → Pistoleer 탭(수동 추가) → Pistoleer 생산 완료 후 Sniper 롱프레스(자동 재등록)
- **기댓값**: 각 전환 시 상태 초기화 정상, 자동 재등록 후 인디케이터 ON
- **결과**: ⬜

---

### 3차 (2026-03-21) — 2차 버그 수정 후 QA (정적 분석)

| 케이스 | PASS/FAIL | 비고 |
|--------|-----------|------|
| TC-10-1 | PASS | 수동 분기 queueIndex=0 → ManualQueue[0]=Assault 제거 + 환불. Pistoleer 생산 유지, Sniper→슬롯1 이동 정확 |
| TC-10-2 | PASS | 슬롯1 재클릭: ManualQueue=[Sniper] → queueIndex=0 → Sniper 취소 + 환불. Pistoleer 생산 유지 |
| TC-11-1 | PASS | ToggleAutoProduction: isSlot0=true → AutoTypes.RemoveAt → AutoTypes=[] → IsAutoMode=false, AutoIndex=0. CurrentProducing=Pistoleer 유지, 환불 없음 |
| TC-11-2 | PASS | 설계 확정(2026-03-21): 버튼 탭은 해당 타입만 취소. Sniper를 취소하려면 Sniper 버튼을 별도로 탭해야 함. AutoTypes=[Sniper] 유지, Assault 생산 계속 — 의도된 동작. |
| TC-11-3 | PASS | isSlot0=false(Sniper는 idx=1, CurrentProducing=Assault) → AutoTypes.RemoveAt(1) → AutoPreChargedCount=0 + 환불 Sniper 200. Assault 생산 유지 |
| TC-12-1 | PASS | EnqueueUnit: IsAutoMode=true, AutoPreChargedCount=0 → 환불 루프 0회. AutoTypes.Clear, IsAutoMode=false. Pistoleer 생산 유지, ManualQueue=[Assault] |
| TC-12-2 | PASS | [4차 수정 후] slotIdx=(AutoIndex+1+i)%count=(0+1+0)%2=1 → AutoTypes[1]=Sniper 비용(200) 정확히 환불. 골드 오차 해소. 슬롯0 Assault 생산 유지, ManualQueue=[Pistoleer] 정상 |
| TC-13-1 | PASS | CancelQueueAt: slotIndex=1, count=1 → `slotIndex==1 && count<2` 방어 조건 true → early return. 아무 변화 없음 |
| TC-13-2 | PASS | CancelQueueAt: slotIndex=2, count=2 → `slotIndex==2 && count<3` 방어 조건 true → early return. 아무 변화 없음 |
| TC-14-1 | PASS | 3단계 연속 동작 추적: AutoTypes/AutoIndex/AutoPreChargedCount 정합 유지 확인. 최종 상태 AutoTypes=[Assault, Pistoleer], AutoPreChargedCount=1(Pistoleer 선불) 일치 |
| TC-14-2 | PASS | 자동→수동→자동 전환 추적: 각 단계 IsAutoMode/AutoTypes/ManualQueue 상태 정상. 자동 재등록 후 IsAutoMode=true, 인디케이터 ON 정확 |

**회귀 검증 (기존 PASS 케이스)**

| 케이스 | 회귀 여부 | 비고 |
|--------|-----------|------|
| TC-04-2 | 회귀 없음 | ToggleAutoProduction isSlot0=true 분기 변경 없음 |
| TC-05-2 | 회귀 없음 | ToggleAutoProduction isSlot0=false 환불 분기 변경 없음 |
| TC-06-1 | 회귀 없음 | CancelQueueAt slotIndex=0 분기 수정 범위 외 |
| TC-06-2 | 회귀 없음 | 2차 수정 PASS 유지. 빈 슬롯 방어 추가로 영향 없음 |
| TC-06-3 | 회귀 없음 | 2차 수정 PASS 유지. 빈 슬롯 방어 추가로 영향 없음 |
| TC-07-3 | 회귀 없음 | ToggleAutoProduction isSlot0=false 환불 분기 변경 없음 |

**종합 판정: PASS**
신규 TC 11개 전체 PASS. TC-11-2는 설계 확정(버튼 탭은 해당 타입만 취소)으로 PASS 처리. TC-12-2는 4차 수정으로 환불 오차 해소 → PASS 전환.

---

### 4차 (2026-03-21) — TC-11-2, TC-12-2 재검증 (정적 분석)

**수정 내용**: `UnitProductionUseCase.cs` L115 오프셋 보정
`(state.AutoIndex + i)` → `(state.AutoIndex + 1 + i)`

| 케이스 | PASS/FAIL | 비고 |
|--------|-----------|------|
| TC-12-2 | PASS | slotIdx=(0+1+0)%2=1 → AutoTypes[1]=Sniper(200) 환불. 기댓값 완전 일치. 3차 골드 오차(-100) 해소 |
| TC-11-2 | PASS | L115 수정은 ToggleAutoProduction 경로와 무관. 설계 확정으로 PASS 처리 |

**회귀 검증**

| 케이스 | 결과 | 비고 |
|--------|------|------|
| AutoPreChargedCount=0 | 영향 없음 | 루프 0회 진입 확인 |
| AutoPreChargedCount=2 | 올바름 | i=0→slotIdx=1, i=1→slotIdx=2. AutoTypes[1], AutoTypes[2] 순서대로 환불 — 선불 차감 순서와 일치 |

**종합 판정: PASS**
TC-12-2 PASS 전환 완료. TC-11-2 설계 확정(2026-03-21 사용자 확인) → PASS 처리. 회귀 없음 확인.
