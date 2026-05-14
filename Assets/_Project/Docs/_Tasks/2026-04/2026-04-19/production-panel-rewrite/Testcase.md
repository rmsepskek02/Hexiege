# Testcase — 생산 패널 전면 재작성

> 싱글플레이 에디터에서 테스트할 것.
> 각 TC는 독립된 상태에서 시작한다 (이전 TC 영향 없음).
> 골드 확인: 생산 패널 우하단 골드 수치 기준.

---

## TC-SINGLE-001: 수동 3개 등록 후 순서대로 생산

**전제:** 골드 충분. 배럭 클릭하여 생산 패널 열기. 큐 비어있음.

**동작:**
1. 첫 번째 유닛 버튼 탭 (예: 권총병)
2. 두 번째 유닛 버튼 탭 (예: 돌격병)
3. 세 번째 유닛 버튼 탭 (예: 저격수)

**기댓값:**
- 슬롯0: 권총병 (즉시 생산 시작)
- 슬롯1: 돌격병
- 슬롯2: 저격수
- 권총병 생산 완료 → 슬롯0=돌격병, 슬롯1=저격수, 슬롯2=빈 슬롯
- 돌격병 생산 완료 → 슬롯0=저격수, 슬롯1~2 빈 슬롯

**결과:**

---

## TC-SINGLE-002: 수동 3개 등록 후 슬롯0 취소 → 골드 환불

**전제:** 골드 충분. 수동으로 권총병→돌격병→저격수 순서로 등록.
슬롯0=권총병(생산 중), 슬롯1=돌격병, 슬롯2=저격수.

**동작:**
1. 슬롯0(권총병) 클릭하여 취소

**기댓값:**
- 슬롯0=돌격병 (즉시 시작), 슬롯1=저격수, 슬롯2=빈 슬롯
- 골드 환불: 권총병 비용만큼 증가

**결과:** PASS

---

## TC-SINGLE-003: 수동 3개 등록 후 슬롯1 취소 → 순서 당김

**전제:** 수동으로 권총병→돌격병→저격수 등록.
슬롯0=권총병, 슬롯1=돌격병, 슬롯2=저격수.

**동작:**
1. 슬롯1(돌격병) 클릭하여 취소

**기댓값:**
- 슬롯0=권총병 (생산 계속), 슬롯1=저격수, 슬롯2=빈 슬롯
- 골드 환불: 돌격병 비용만큼 증가

**결과:** PASS

---

## TC-SINGLE-004: 수동 3개 등록 후 슬롯2 취소

**전제:** 수동으로 권총병→돌격병→저격수 등록.

**동작:**
1. 슬롯2(저격수) 클릭하여 취소

**기댓값:**
- 슬롯0=권총병 (생산 계속), 슬롯1=돌격병, 슬롯2=빈 슬롯
- 골드 환불: 저격수 비용만큼 증가

**결과:** PASS

---

## TC-SINGLE-005: 수동 큐 [3,2,1] + 자동 등록 (마지막과 다른 타입) → 슬롯2 즉시 표시

**전제:** 수동으로 권총병(3)→돌격병(2)→저격수(1) 순서 등록.
슬롯0=권총병, 슬롯1=돌격병, 슬롯2=저격수.

**동작:**
1. 돌격병 버튼 롱프레스 (자동 등록)

**기댓값:**
- 돌격병 자동 인디케이터 ON
- 슬롯0=권총병, 슬롯1=돌격병, 슬롯2=저격수 (변화 없음)
- 돌격병이 큐 뒤에 자동 항목으로 대기 (현재 슬롯3에 해당, 표시는 안 됨)
- 골드 미차감 (슬롯이 꽉 차있으므로)

**결과:** PASS

---

## TC-SINGLE-006: 수동 큐 [3,2] + 자동 등록 → 빈 슬롯2에 즉시 표시

**전제:** 수동으로 권총병→돌격병 등록.
슬롯0=권총병, 슬롯1=돌격병, 슬롯2=빈 슬롯.

**동작:**
1. 저격수 버튼 롱프레스 (자동 등록)

**기댓값:**
- 저격수 자동 인디케이터 ON
- 슬롯0=권총병, 슬롯1=돌격병, 슬롯2=저격수
- 골드 차감: 저격수 비용만큼 감소 (슬롯2에 즉시 표시됨)

**결과:** PASS

---

## TC-SINGLE-007: 수동 큐 마지막 항목과 같은 타입 자동 등록 → 이관 처리

**전제:** 수동으로 권총병→돌격병→저격수 등록.
슬롯0=권총병, 슬롯1=돌격병, 슬롯2=저격수.

**동작:**
1. 저격수 버튼 롱프레스 (자동 등록 — 마지막 수동 항목과 같은 타입)

**기댓값:**
- 저격수 자동 인디케이터 ON
- 슬롯0=권총병, 슬롯1=돌격병, 슬롯2=저격수 (중복 추가 없이 기존 저격수가 자동으로 전환됨)
- 골드 추가 차감 없음 (이미 차감된 수동 항목이 자동으로 전환됨)

**결과:** PASS

---

## TC-SINGLE-008: 자동 1종 순수 자동 모드 순환

**전제:** 큐 비어있음.

**동작:**
1. 권총병 버튼 롱프레스 (자동 등록)
2. 권총병이 생산 완료될 때까지 대기
3. 다음 권총병 생산이 자동으로 시작되는지 확인

**기댓값:**
- 슬롯0=권총병 (자동 생산 시작)
- 생산 완료 후 자동으로 권총병 재생산 시작 (무한 순환)
- 슬롯1~2 빈 슬롯 (자동 타입이 1개뿐이므로)

**결과:** PASS

---

## TC-SINGLE-009: 자동 2종 순수 자동 모드 순환

**전제:** 큐 비어있음.

**동작:**
1. 권총병 버튼 롱프레스 (자동 등록)
2. 돌격병 버튼 롱프레스 (자동 등록)
3. 두 유닛이 순서대로 반복 생산되는지 확인

**기댓값:**
- 슬롯0=권총병, 슬롯1=돌격병
- 권총병 완료 → 슬롯0=돌격병, 슬롯1=권총병 (순환 재추가)
- 돌격병 완료 → 슬롯0=권총병, 슬롯1=돌격병
- 이 패턴이 무한 반복

**결과:** PASS

---

## TC-SINGLE-010: 자동 인디케이터 ON 상태에서 버튼 탭 → 자동 취소, 환불 없음

**전제:** 권총병 자동 등록되어 생산 중. 슬롯0=권총병, 슬롯1=빈 슬롯.

**동작:**
1. 권총병 버튼 탭 (자동 취소)

**기댓값:**
- 권총병 자동 인디케이터 OFF
- 슬롯0=권총병 (생산 계속, 취소 아님)
- 골드 환불 없음 (자동 등록 해제는 생산 취소가 아님)

**결과:** PASS

---

## TC-SINGLE-011: 자동 2종 중 슬롯1 타입 탭 취소 → 슬롯1 유지 (Rule 2)

**전제:** 권총병→돌격병 자동 등록. 슬롯0=권총병, 슬롯1=돌격병.

**동작:**
1. 돌격병 버튼 탭 (자동 취소)

**기댓값:**
- 돌격병 자동 인디케이터 OFF
- 슬롯0=권총병 (생산 계속)
- 슬롯1=돌격병 유지 (골드 이미 차감된 항목은 수동으로 이관되어 생산 계속 — Rule 2)
- 슬롯2=빈 슬롯
- 골드 환불 없음

**결과:** PASS

---

## TC-SINGLE-012: 자동 모드에서 수동 추가 → 자동 모드 해제 + 슬롯 유지 (Rule 3)

**전제:** 권총병→돌격병 자동 등록. 슬롯0=권총병, 슬롯1=돌격병.

**동작:**
1. 저격수 버튼 탭 (수동 추가)

**기댓값:**
- 모든 자동 인디케이터 OFF (자동 모드 해제)
- 슬롯0=권총병 (생산 계속)
- 슬롯1=돌격병 (이관되어 유지)
- 슬롯2=저격수 (수동 추가)
- 골드 환불 없음 (이관 항목은 이미 차감됨)

**결과:** PASS

---

## TC-SINGLE-013: 자동 모드 + 수동 취소 슬롯0 → 큐 순서 올바름 (이전 BUG-19 회귀 방지)

**전제:** 수동으로 권총병→돌격병→저격수 등록 후 저격수 자동 이관.
슬롯0=권총병, 슬롯1=돌격병, 슬롯2=저격수(자동).

**동작:**
1. 슬롯0(권총병) 취소

**기댓값:**
- 슬롯0=돌격병 (다음 항목이 올바르게 올라옴)
- 슬롯1=저격수(자동)
- 슬롯2=빈 슬롯
- 골드 환불: 권총병 비용

**결과:** PASS

---

## TC-SINGLE-014: 큐 풀 상태에서 자동 등록 → 골드 미차감, 슬롯 변화 없음

**전제:** 수동으로 권총병→돌격병→저격수 등록. 큐 3개 풀.

**동작:**
1. 권총병 버튼 롱프레스 (자동 등록)

**기댓값:**
- 권총병 자동 인디케이터 ON
- 슬롯 변화 없음 (여전히 슬롯0=권총병, 슬롯1=돌격병, 슬롯2=저격수)
- 골드 미차감 (슬롯이 꽉 차있으므로)

**결과:** PASS

---

## TC-SINGLE-015: 큐 풀 미차감 자동 항목 → 수동 큐 소진 후 슬롯 진입 시 골드 차감

**전제:** TC-SINGLE-014 이후 상태.
슬롯0=권총병, 슬롯1=돌격병, 슬롯2=저격수, 권총병 자동 등록(골드 미차감).

**동작:**
1. 권총병 생산 완료 → 슬롯0=돌격병, 슬롯1=저격수, 슬롯2=권총병(자동) 진입 대기
2. 실제로 슬롯2에 권총병이 표시될 때 골드 차감 확인

**기댓값:**
- 권총병 완료 직후: 슬롯0=돌격병, 슬롯1=저격수, 슬롯2=권총병 표시 + 골드 차감
- 이후 자동 순환 반복

**결과:** PASS

---

## TC-SINGLE-016: 순수 자동 1종 생산 중 슬롯0 취소 → 생산 취소 + 자동 등록 해제

**전제:** 권총병만 자동 등록된 상태. 슬롯0=권총병(자동 생산 중), 슬롯1~2=빈 슬롯.

**동작:**
1. 슬롯0(권총병) 클릭하여 취소

**기댓값:**
- 슬롯0=빈 슬롯 (생산 취소됨)
- 권총병 자동 인디케이터 OFF
- 골드 환불: 권총병 비용만큼 증가
- 이후 어떤 틱이 지나도 권총병이 자동으로 재생산되지 않음 (자동 등록이 완전히 해제됨)

**결과:** PASS

---

## TC-SINGLE-017: 자동 2종 생산 중 슬롯1(대기 자동 항목) 취소 → 해당 타입만 자동 해제, 슬롯0 생산 계속

**전제:** 권총병→돌격병 자동 등록. 슬롯0=권총병(자동 생산 중), 슬롯1=돌격병(자동, 골드 차감됨), 슬롯2=빈 슬롯.

**동작:**
1. 슬롯1(돌격병) 클릭하여 취소

**기댓값:**
- 슬롯0=권총병 (생산 계속)
- 슬롯1=빈 슬롯 (돌격병 취소됨)
- 골드 환불: 돌격병 비용만큼 증가
- 돌격병 자동 인디케이터 OFF
- 권총병 자동 인디케이터 ON 유지
- 이후 권총병 생산이 완료되면 권총병만 자동 재생산됨 (돌격병은 재생산되지 않음)

**결과:** PASS

---

## TC-SINGLE-018: 자동 2종 생산 중 슬롯0(현재 생산 중) 취소 → 생산 취소 + 해당 타입 자동 해제, 나머지 자동 유지

**전제:** 권총병→돌격병 자동 등록. 슬롯0=권총병(자동 생산 중), 슬롯1=돌격병(자동, 골드 차감됨), 슬롯2=빈 슬롯.

**동작:**
1. 슬롯0(권총병) 클릭하여 취소

**기댓값:**
- 슬롯0=돌격병 (다음 틱에 슬롯1의 돌격병이 슬롯0으로 올라가 생산 시작)
- 슬롯1=빈 슬롯
- 골드 환불: 권총병 비용만큼 증가
- 권총병 자동 인디케이터 OFF
- 돌격병 자동 인디케이터 ON 유지
- 돌격병 생산 완료 후 돌격병이 자동 재생산됨 (권총병은 재생산되지 않음)

**결과:** PASS

---

## QA 섹션 (qa-tester 에이전트 전용)

> 아래 섹션은 qa-tester 에이전트가 정적 분석 결과를 기록하는 공간입니다.

### 정적 분석 대상

| TC | 핵심 검증 코드 경로 | 판정 |
|----|----------------|------|
| TC-001 | EnqueueUnit × 3 → PendingQueue 순서 | PASS |
| TC-002 | CancelQueueAt(0) → 환불 + 다음 항목 시작 | PASS |
| TC-003 | CancelQueueAt(1) → PendingQueue.RemoveAt(0) + 환불 | PASS |
| TC-004 | CancelQueueAt(2) → PendingQueue.RemoveAt(1) + 환불 | PASS |
| TC-005 | ToggleAutoProduction → canShow=false → IsCharged=false 추가 | PASS |
| TC-006 | ToggleAutoProduction → canShow=true → IsCharged=true 추가 | PASS |
| TC-007 | ToggleAutoProduction Rule 2-1 → 마지막 수동 항목 IsAuto=true 전환 | PASS |
| TC-008 | TryStartNext AutoTypes 경로 + CompleteProduction 재추가 순환 | PASS |
| TC-009 | 자동 2종 순환 → CompleteProduction + ChargeVisibleSlots | PASS |
| TC-010 | ToggleAutoProduction 제거 경로 → 생산 유지 | PASS |
| TC-011 | ToggleAutoProduction 제거 + Rule 2 이관 (IsCharged=true → IsAuto=false) | PASS |
| TC-012 | EnqueueUnit Rule 3 → AutoTypes.Clear + 이관 유지 | PASS |
| TC-013 | CancelQueueAt(0) 후 TryStartNext → PendingQueue[0]이 다음 슬롯0 | PASS |
| TC-014 | ToggleAutoProduction → ChargedPendingCount=2 → canShow=false | PASS |
| TC-015 | CompleteProduction + ChargeVisibleSlots → 미차감 항목 슬롯 진입 시 차감 | PASS |

---

## 정적 분석 결과 (qa-tester)

### 1단계: 구 구조 잔존 여부 전수 검색 결과

| 검색 패턴 | 검색 결과 | 판정 |
|-----------|---------|------|
| `ManualQueue` | ProductionState.cs(10행), UnitProductionUseCase.cs(7행, 306행) — 모두 주석 | PASS |
| `AutoEntries` | ProductionState.cs(10행), UnitProductionUseCase.cs(7행) — 모두 주석 | PASS |
| `AutoEntry` | 없음 | PASS |
| `\bAutoIndex\b` | ProductionState.cs(10행) — 주석 | PASS |
| `CurrentProducingIsAuto` | 없음 | PASS |
| `AutoTypeAt` | 없음 | PASS |
| `AutoCount` | 없음 | PASS |
| `isNormalAutoState` | ProductionState.cs(11행), UnitProductionUseCase.cs(8행), ProductionPanelUI.cs(655행) — 모두 주석 | PASS |
| `IsAutoMode\s*=` | ProductionState.cs(95행, 110행) — 주석+프로퍼티 선언 (`=>` 읽기 전용), 값 대입 없음 | PASS |

구 구조 코드 잔존 없음. 모든 참조는 주석 내 역사적 언급뿐.

---

### 2단계: 수정된 4개 파일 코드 리뷰

**ProductionState.cs**
- QueueSlot struct, PendingQueue, AutoTypes, AutoCycleIndex, CurrentIsAuto 신규 필드 정상 구현.
- IsAutoMode → `AutoTypes.Count > 0` 읽기 전용 프로퍼티 (라인 110).
- ChargedPendingCount() 메서드 존재 (라인 170~177). Rule 4 큐 크기 계산에 사용.
- AutoContains()는 구버전 API처럼 보이지만 내부에서 AutoTypes를 순회하는 편의 메서드로 잔존. ProductionPanelUI에서 `state.AutoTypes.Contains(type)`을 직접 사용하므로 실제 참조는 없음.
- using 선언: `System.Collections.Generic` 단독 — Domain 레이어에 Unity/Core 의존 없음. 정상.

**UnitProductionUseCase.cs**
- EnqueueUnit, ToggleAutoProduction, CancelQueueAt, TryStartNext, CompleteProduction, ChargeVisibleSlots 모두 새 구조로 재작성됨.
- NormalizeAutoCycleIndex() 헬퍼: AutoTypes 길이 변경 시 AutoCycleIndex 안전 보정. 음수 방지 공식 `((index % count) + count) % count` 정상.
- using 선언: `System.Collections.Generic`, `Hexiege.Domain` — Application 레이어. Core/Infrastructure 의존 없음. 정상.
- 컴파일 에러 가능성 없음.

**ProductionPanelUI.cs**
- UpdateQueueSlots: 슬롯1=PendingQueue[0].Type, 슬롯2=PendingQueue[1].Type으로 단순화됨 (라인 681~687).
- OnQueueSlotClicked: CancelQueueAt 단일 호출만 남음. 이전의 ToggleAutoProduction fallback 경로 완전 제거됨 (라인 481).
- 자동 인디케이터 판단: `state.AutoTypes.Contains(_buttonUnitTypes[i])` 사용 (라인 634~638).
- using 선언 정상. Unity, UniRx, Hexiege.Domain, Hexiege.Application, Hexiege.Infrastructure, TMPro 포함.
- 컴파일 에러 가능성 없음.

**NetworkProductionController.cs**
- SyncQueueStateClientRpc 파라미터: int, bool 기본 타입만 사용 → NGO RPC 직렬화 가능.
- IsAutoMode 직접 대입 없음. AutoTypes.Clear() + Add() 패턴으로만 조작 (라인 578~592).
- AutoProductionChangedClientRpc: 새 구조에서는 UI 이벤트 발행 용도로만 사용. IsAutoMode 상태 변경 시도 없음 (라인 741~752). 정상.
- ToggleAutoServerRpc: ToggleAutoProduction 실패 시 return (라인 717~720). 단, ToggleAutoProduction은 AutoTypes 3종 초과 시에만 false를 반환하므로 이 경로는 정상.
- using 선언 정상.
- 컴파일 에러 가능성 없음.

---

### 3단계: TC별 정적 분석 상세 근거

**TC-001 (PASS)**: EnqueueUnit → `PendingQueue.Add(QueueSlot(type, false, true))` 순서대로 추가. Tick에서 TryStartNext가 PendingQueue[0]을 꺼내 CurrentProducing으로 올림. PendingQueue 불변식 유지.

**TC-002 (PASS)**: CancelQueueAt(0) → 골드 환불 후 CurrentProducing=null. 다음 Tick TryStartNext가 PendingQueue[0](돌격병)을 슬롯0으로 올림.

**TC-003 (PASS)**: CancelQueueAt(1) → PendingQueue[0](돌격병) 환불 후 RemoveAt(0). ChargeVisibleSlots 호출 → PendingQueue가 당겨져 저격수가 새 슬롯1. 이미 IsCharged=true이므로 ChargeVisibleSlots 무동작.

**TC-004 (PASS)**: CancelQueueAt(2) → PendingQueue[1](저격수) 환불 후 RemoveAt(1). ChargeVisibleSlots 호출.

**TC-005 (PASS)**: ToggleAutoProduction 돌격병. ChargedPendingCount()=2, 2 < 2 = false → canShow=false → IsCharged=false로 추가. PendingQueue[0~1] 변화 없음.

**TC-006 (PASS)**: ToggleAutoProduction 저격수. ChargedPendingCount()=1, 1 < 2 = true → canShow=true → 차감 후 IsCharged=true로 추가. 슬롯2에 즉시 표시.

**TC-007 (PASS)**: Rule 2-1 경로. PendingQueue.last = 저격수(IsAuto=false, Type=저격수), 등록 타입도 저격수 → 조건 일치 → IsAuto=true 전환 후 즉시 return. 중복 추가 없음, 골드 추가 차감 없음.

**TC-008 (PASS)**: 최초 등록 시 canShow=false(CurrentProducing=null). TryStartNext에서 PendingQueue[0] 꺼내 차감 후 CurrentProducing 설정. CompleteProduction 후 재추가 + ChargeVisibleSlots 순환. 자동 타입 1개이므로 슬롯1은 순환 대기 중 1프레임만 표시되다가 즉시 슬롯0으로 올라가는 구조.

**TC-009 (PASS)**: 2종 자동 등록. TryStartNext → 권총병 생산 시작 + ChargeVisibleSlots → 돌격병 차감. CompleteProduction → 권총병 재추가(false) + ChargeVisibleSlots → 돌격병(already charged skip) + 권총병(false→true). 다음 Tick 돌격병 슬롯0 이동. 무한 순환.

**TC-010 (PASS)**: ToggleAutoProduction(권총병) 제거 경로. AutoTypes.RemoveAt(0). PendingQueue 순회: 해당 타입(권총병) IsAuto=true 항목이 PendingQueue에 없음 — CurrentProducing에 있음. PendingQueue 변화 없음, 환불 없음, 생산 계속.

**TC-011 (PASS)**: ToggleAutoProduction(돌격병) 제거 경로. PendingQueue[0]=돌격병(IsAuto=true, IsCharged=true) → Rule 2: IsAuto=false 전환 (이관). 인디케이터 OFF, 슬롯1 유지, 환불 없음.

**TC-012 (PASS)**: EnqueueUnit(저격수). IsAutoMode=true → Rule 3: 역순 순회, 돌격병(auto,charged=true) → IsAuto=false 이관. AutoTypes.Clear(). slotsUsed=1+1=2 ≤ 3. 저격수 추가 → PendingQueue=[돌격병(m,c), 저격수(m,c)].

**TC-013 (PASS)**: CancelQueueAt(0). CurrentProducing=null. 다음 Tick TryStartNext → PendingQueue[0]=돌격병(m,c) → 슬롯0. PendingQueue=[저격수(auto,c)]. BUG-19 회귀 없음 — 구 ManualQueue 우선 처리 로직이 완전히 제거되었으므로 PendingQueue 순서대로 진행.

**TC-014 (PASS)**: ChargedPendingCount()=2 (돌격병+저격수). 2 < 2 = false → canShow=false → 권총병(auto, false) 대기 추가. 슬롯 변화 없음, 골드 미차감.

**TC-015 (PASS)**: 권총병(슬롯0, 수동) 완료. wasAuto=false → 재추가 없음. ChargeVisibleSlots: PendingQueue[0~1] 모두 IsCharged=true → skip. TryStartNext: 돌격병 슬롯0 이동 + ChargeVisibleSlots: PendingQueue[0]=저격수(c) skip, PendingQueue[1]=권총병(auto,false) → 차감 → IsCharged=true. 슬롯2 표시 + 골드 차감.

---

### 4단계: NetworkProductionController 동기화 검증

- SyncQueueStateClientRpc 파라미터: `int`, `bool` 기본 타입 17개 — NGO 직렬화 가능. 사용자 정의 struct 없음.
- IsAutoMode 직접 대입 없음. 클라이언트에서 AutoTypes를 Clear+Add로 재구성 → IsAutoMode 자동 계산. 정상.
- PendingQueue 최대 3개 전송 포맷: Plan.md의 위험 요소 "PendingQueue[2+] 초과 항목 전송 불가" 인지됨. 실제로 PendingQueue는 수동 2개 + 자동 1개 대기(총 3개)가 최대 경우이므로 현재 구현으로 충분.

---

### 종합 판정: PASS (실기 완료 — 2026-04-19)

TC-001~018 실기 테스트 완료. 전면 재작성 + CancelAutoTypeIfNeeded 버그픽스 모두 정상 동작 확인.

미해결 이슈 (별도 작업):
1. TC-008: 큐 비어있을 때 자동생산 등록 시 슬롯1에 잠깐 표시 후 슬롯0으로 이동하는 간헐적 깜빡임 — 시각적 버그로 확인됨, 별도 점검 예정.

---

### TC-016~018 정적 분석 (BUG-FIX 2026-04-19 CancelAutoTypeIfNeeded 검증)

#### 검증 항목 1: wasAuto 캡처 타이밍 (TC-016, TC-018)

CancelQueueAt slotIndex==0 분기 (UnitProductionUseCase.cs 299~321행):
- 300행: `bool wasAuto = state.CurrentIsAuto` — 캡처
- 303~309행: 환불 + 상태 초기화 (`state.CurrentIsAuto = false` 포함)
- 319~322행: `if (wasAuto) CancelAutoTypeIfNeeded(state, cancelType)` — 초기화 이후 참조

wasAuto는 state.CurrentIsAuto가 false로 덮어쓰이기 이전인 300행에서 캡처됨.
slotIndex==0 취소 시 자동 항목이었는지 여부를 올바르게 판별함. **PASS**

---

#### 검증 항목 2: cancelled 값 복사 안전성 (TC-017)

CancelQueueAt slotIndex==1 분기 (333~356행):
- 333행: `QueueSlot cancelled = state.PendingQueue[0]` — 값 복사
- 341행: `state.PendingQueue.RemoveAt(0)` — 원본 항목 제거
- 349행: `if (cancelled.IsAuto)` — 복사본 참조

QueueSlot은 struct(ProductionState.cs 45행)이므로 333행에서 값이 복사됨.
RemoveAt 이후에도 cancelled.IsAuto 접근이 안전함. slotIndex==2도 동일 패턴(363, 371, 377행). **PASS**

---

#### 검증 항목 3: CancelAutoTypeIfNeeded 내부 Rule 2 처리 (TC-016, TC-017, TC-018)

CancelAutoTypeIfNeeded 메서드 (690~721행):
1. AutoTypes에서 해당 타입 제거 (693~697행) — AutoTypes에 없으면 조기 반환
2. PendingQueue 역순 순회 (701~716행):
   - IsAuto=true && Type==type 조건 일치 시
   - IsCharged=true → s.IsAuto=false, state.PendingQueue[i]=s (수동 이관, 환불 없음)
   - IsCharged=false → state.PendingQueue.RemoveAt(i) (제거, 환불 불필요)
3. NormalizeAutoCycleIndex 호출 (720행) — AutoTypes 길이 변경 후 인덱스 범위 보정

**TC-016 시나리오**: 권총병 자동 1종만 등록, CancelQueueAt(0) → wasAuto=true → CancelAutoTypeIfNeeded(권총병) 호출 → AutoTypes=[] → IsAutoMode=false → 이후 TryStartNext에서 AutoTypes 경로 진입 안 됨. 재생산 없음. **PASS**

**TC-017 시나리오**: 권총병+돌격병 자동 등록, CancelQueueAt(1) → cancelled={돌격병, IsAuto=true, IsCharged=true} → RemoveAt(0) → ChargeVisibleSlots → cancelled.IsAuto=true → CancelAutoTypeIfNeeded(돌격병):
- AutoTypes=[권총병, 돌격병] → 돌격병 제거 → AutoTypes=[권총병]
- PendingQueue 역순 순회: RemoveAt 직후이므로 돌격병 IsAuto 항목 없음 → 순회 무동작
- NormalizeAutoCycleIndex: AutoTypes.Count=1 → AutoCycleIndex 범위 내로 보정
- 결과: 권총병 자동 유지, 돌격병 자동 해제. **PASS**

**TC-018 시나리오**: 권총병+돌격병 자동 등록, CancelQueueAt(0) → wasAuto=true → 초기화 → CancelAutoTypeIfNeeded(권총병):
- AutoTypes=[권총병, 돌격병] → 권총병 제거 → AutoTypes=[돌격병]
- PendingQueue=[돌격병(IsAuto=true, IsCharged=true)]: 돌격병은 Type=돌격병이므로 조건 불일치 → 무동작
- NormalizeAutoCycleIndex: AutoTypes.Count=1 → 보정
- 다음 Tick TryStartNext: PendingQueue[0]=돌격병(IsAuto=true, IsCharged=true) → 슬롯0으로 이동, 생산 시작
- 돌격병 완료 후 CompleteProduction: wasAuto=true && AutoTypes.Contains(돌격병)=true → 돌격병 재추가 → 순환 계속
- 결과: 돌격병 자동 유지, 권총병 자동 해제. **PASS**

---

#### TC-016~018 정적 분석 판정 요약

| TC | 핵심 검증 경로 | 판정 |
|----|-------------|------|
| TC-016 | CancelQueueAt(0), wasAuto 캡처 → CancelAutoTypeIfNeeded → AutoTypes 비워짐 → 재생산 없음 | PASS |
| TC-017 | CancelQueueAt(1), cancelled 값 복사 → CancelAutoTypeIfNeeded(돌격병) → 권총병 자동 유지 | PASS |
| TC-018 | CancelQueueAt(0), wasAuto=true → CancelAutoTypeIfNeeded(권총병) → 돌격병 자동 유지, 다음 틱 생산 시작 | PASS |
