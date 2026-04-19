# Plan — 생산 패널 전면 재작성

## 설계 목표

이중 큐 구조(ManualQueue + AutoEntries)를 단일 PendingQueue로 통합한다.
`isNormalAutoState`와 `AutoIndex`(표시 용도)를 완전히 제거하여 슬롯 표시 로직을 자명하게 만든다.

---

## 핵심 원칙

**PendingQueue[0] = 슬롯1, PendingQueue[1] = 슬롯2. 항상.**

이 불변식이 유지되면:
- UI는 `PendingQueue[0]`과 `PendingQueue[1]`만 읽으면 된다
- `CancelQueueAt(1)` = `PendingQueue.RemoveAt(0)`
- `CancelQueueAt(2)` = `PendingQueue.RemoveAt(1)`
- `isNormalAutoState` 계산 불필요
- 표시와 취소 로직이 항상 일치

---

## 새 데이터 구조

### QueueSlot (신규)

수동/자동 구분 없이 대기 항목 하나를 표현하는 구조체.

```
QueueSlot
  Type:       UnitType    — 유닛 타입
  IsAuto:     bool        — true = 자동 등록 항목, false = 수동 등록 항목
  IsCharged:  bool        — true = 골드 이미 차감됨, false = 아직 미차감
```

### ProductionState 변경

**제거:**
- `ManualQueue: List<UnitType>` — 삭제
- `AutoEntries: List<AutoEntry>` — 삭제
- `AutoIndex: int` — 삭제 (아래 AutoCycleIndex로 대체, 역할이 다름)
- `CurrentProducingIsAuto: bool` — 삭제 (아래 CurrentIsAuto로 이름 변경)

**추가/변경:**
- `PendingQueue: List<QueueSlot>` — 신규. 슬롯1~2 + 대기 항목 통합 보관
- `AutoTypes: List<UnitType>` — 신규. 자동 등록된 타입 목록 (순환 및 인디케이터용)
- `AutoCycleIndex: int` — 신규. AutoTypes에서 다음에 PendingQueue에 추가할 위치 (표시 계산용이 아님)
- `CurrentIsAuto: bool` — 신규 (기존 CurrentProducingIsAuto를 이름 변경 + 역할 재정의)
- `IsAutoMode: bool` — `AutoTypes.Count > 0`으로 계산 (필드 → 읽기 전용 프로퍼티)

**유지:**
- `CurrentProducing`, `ElapsedTime`, `RequiredTime`, `BarracksId`, `Team`, `BarracksPosition`, `RallyPoint`
- `MaxQueueSize = 3` (슬롯0 + 슬롯1 + 슬롯2)

### 핵심 불변식

1. `PendingQueue[0..1]`의 항목은 항상 `IsCharged=true` (수동은 항상, 자동은 슬롯 진입 시)
2. `PendingQueue[2+]`의 자동 항목은 `IsCharged=false` (대기 중, 골드 미차감)
3. `AutoTypes`에 없는 타입의 IsAuto=true 항목은 PendingQueue에 존재하지 않음

---

## 메서드별 동작 설계

### EnqueueUnit (수동 추가)

```
Rule 3 적용: IsAutoMode이면 자동 모드 해제
  - IsCharged=true인 자동 항목들: IsAuto=false로 전환 (수동 이관, 큐에서 제거하지 않음)
  - IsCharged=false인 자동 항목들: PendingQueue에서 제거 (골드 미차감이므로 소멸)
  - AutoTypes.Clear(), AutoCycleIndex=0

Rule 4 체크: 슬롯 점유 수 = (CurrentProducing ? 1 : 0) + PendingQueue.Count(IsCharged=true)
  → MaxQueueSize(3) 초과 시 거부

골드/인구 검증 + 즉시 골드 차감 (수동 항목은 항상 등록 시 차감)

PendingQueue.Add(new QueueSlot(type, isAuto=false, isCharged=true))
```

### ToggleAutoProduction (자동 토글)

**이미 등록된 타입 (제거)**:
```
AutoTypes에서 type 제거

PendingQueue에서 IsAuto=true이고 Type=type인 항목 처리:
  - IsCharged=true → IsAuto=false로 전환 (Rule 2: 수동 이관, 생산 계속)
  - IsCharged=false → PendingQueue에서 제거 (골드 미차감이므로 소멸)

AutoTypes가 비면: AutoCycleIndex=0
```

**미등록 타입 (추가)**:
```
AutoEntries 최대 3개 체크

[Rule 2-1: 수동 이관 체크]
PendingQueue에서 마지막 IsAuto=false 항목이 type과 같으면:
  해당 항목의 IsAuto=true로 전환 (골드 이미 차감됨, 추가 차감 없음)
  AutoTypes.Add(type)
  이벤트 발행 후 반환

[Rule 5: 슬롯 표시 가능 여부 판단]
canShow = CurrentProducing.HasValue
          && PendingQueue.Count(IsCharged=true) < MaxQueueSize - 1 (= 2)

[BUG-15/C 방지: 자동으로 같은 타입 생산 중이면 즉시 차감 불가]
if canShow && CurrentIsAuto && CurrentProducing == type → canShow=false

if canShow:
  골드/인구 검증 + 차감
  PendingQueue.Add(new QueueSlot(type, isAuto=true, isCharged=true))
else:
  PendingQueue.Add(new QueueSlot(type, isAuto=true, isCharged=false))

AutoTypes.Add(type)
```

### CancelQueueAt (슬롯 취소)

슬롯 클릭 = 생산 취소 + (자동 항목인 경우) 해당 타입 자동생산 등록도 취소.
이전부터 슬롯 클릭은 두 동작을 함께 수행했으나 Plan.md에 누락됨 — 2026-04-19 보완.

자동 항목 취소 시 AutoTypes에서 해당 타입 제거 + 나머지 자동 항목 Rule 2 처리:
  - IsCharged=true 자동 항목 → IsAuto=false (수동 이관, 생산 계속 — Rule 2)
  - IsCharged=false 자동 항목 → PendingQueue에서 제거 (환불 없음)

```
[공통 헬퍼: CancelAutoTypeIfNeeded(state, type)]
  AutoTypes에 type이 없으면 반환 (자동 항목이 아님)
  AutoTypes에서 type 제거
  PendingQueue 역순 순회: IsAuto=true && Type==type인 항목 처리
    IsCharged=true → IsAuto=false 전환 (Rule 2: 수동 이관)
    IsCharged=false → RemoveAt (환불 없음)
  NormalizeAutoCycleIndex()

slotIndex == 0:
  CurrentProducing이 없으면 반환
  type = CurrentProducing.Value
  wasAuto = CurrentIsAuto (취소 전에 저장)
  골드 환불 (Rule 1)
  CurrentProducing = null, ElapsedTime=0, RequiredTime=0, CurrentIsAuto=false
  if wasAuto: CancelAutoTypeIfNeeded(state, type)

  [BUG-19 해결책 — 현재와 동일 원칙 유지]
  → 이제는 PendingQueue 자체가 이미 올바른 순서이므로
     TryStartNext() 위임으로 처리 (다음 Tick에서 PendingQueue[0]이 슬롯0으로 올라감)

slotIndex == 1:
  PendingQueue.Count < 1이면 반환
  cancelled = PendingQueue[0]
  IsCharged=true이면 골드 환불 (Rule 1)
  PendingQueue.RemoveAt(0)
  ChargeVisibleSlots() 호출 — 새로 visible된 PendingQueue[0]이 자동 항목이면 골드 차감
  if cancelled.IsAuto: CancelAutoTypeIfNeeded(state, cancelled.Type)

slotIndex == 2:
  PendingQueue.Count < 2이면 반환
  cancelled = PendingQueue[1]
  IsCharged=true이면 골드 환불 (Rule 1)
  PendingQueue.RemoveAt(1)
  ChargeVisibleSlots() 호출
  if cancelled.IsAuto: CancelAutoTypeIfNeeded(state, cancelled.Type)
```

### TryStartNext (내부)

```
PendingQueue.Count > 0이면:
  slot = PendingQueue[0]
  PendingQueue.RemoveAt(0)

  if slot.IsAuto && !slot.IsCharged:
    골드/인구 검증 + 차감 (미차감 항목이 슬롯0에 올라오는 시점에 차감)
    if 자원 부족: PendingQueue.Insert(0, slot); return  ← 다시 앞에 넣고 대기
    slot.IsCharged = true

  CurrentProducing = slot.Type
  CurrentIsAuto = slot.IsAuto
  ElapsedTime=0, RequiredTime=... 세팅

  ChargeVisibleSlots()  ← 새로 visible된 PendingQueue[0]을 차감

else if IsAutoMode && AutoTypes.Count > 0:
  [PendingQueue가 비어 있지만 자동 모드 — 자동 항목을 생성하여 직접 시작]
  type = AutoTypes[AutoCycleIndex % AutoTypes.Count]
  골드/인구 검증 + 차감
  if 자원 부족: return
  AutoCycleIndex++

  CurrentProducing = type
  CurrentIsAuto = true
  ElapsedTime=0, RequiredTime=...

  ChargeVisibleSlots()  ← 나머지 자동 타입들을 PendingQueue에 채움
```

### CompleteProduction (내부)

```
type = CurrentProducing.Value
wasAuto = CurrentIsAuto

스폰 처리 (기존 로직 유지)

CurrentProducing = null, CurrentIsAuto = false
ElapsedTime=0, RequiredTime=0

if wasAuto && AutoTypes.Contains(type):
  [자동 항목 순환: 완료된 타입을 PendingQueue 끝에 재추가]
  PendingQueue.Add(new QueueSlot(type, isAuto=true, isCharged=false))

ChargeVisibleSlots()  ← PendingQueue[0..1] 중 미차감 항목 차감

이벤트 발행
```

### ChargeVisibleSlots (내부 헬퍼)

```
[PendingQueue[0]과 PendingQueue[1]이 IsCharged=false인 경우 골드 차감]
for i in 0..min(1, PendingQueue.Count-1):
  if PendingQueue[i].IsCharged: continue
  골드/인구 검증 + 차감
  if 자원 부족: break  ← 부족하면 이후 항목도 차감하지 않음
  PendingQueue[i].IsCharged = true
```

---

## ProductionPanelUI 변경

### UpdateQueueSlots — 극단적으로 단순화

```
슬롯0: state.CurrentProducing
슬롯1: state.PendingQueue.Count > 0 ? state.PendingQueue[0].Type : null
슬롯2: state.PendingQueue.Count > 1 ? state.PendingQueue[1].Type : null
```

isNormalAutoState 계산 완전 제거.
자동/수동 분기 완전 제거 (동일 코드 사용).

### OnQueueSlotClicked — fallback 제거

CancelQueueAt이 항상 올바르게 동작하므로 ToggleAutoProduction fallback 로직 삭제.

```
CancelQueueAt(_currentBarracks.Id, slotIndex) 호출만 남김
```

### 인디케이터 갱신

`state.AutoTypes.Contains(type)`으로 판단 (기존 `state.AutoContains(type)` 대체).

---

## 슬롯0 취소 후 다음 항목 시작 — BUG-19 재검토

기존 BUG-19 수정은 TryStartNext()의 "ManualQueue 우선" 처리로 인해
슬롯0 취소 직후 자동 항목이 밀리는 문제를 우회하기 위한 것이었다.

새 구조에서는 PendingQueue가 이미 올바른 표시 순서를 유지하므로
TryStartNext()가 항상 PendingQueue[0]을 꺼내면 올바른 결과가 나온다.

**BUG-19 우회 코드 제거 가능 — 구현자는 반드시 시나리오 검증 후 적용:**

| 시나리오 | 기대 결과 |
|---------|---------|
| 수동 [3,2,1] + 자동1(이관됨) 상태에서 슬롯0 취소 | PendingQueue=[2(manual),1(auto)] → 다음=2 |
| 자동 [A,B,C] 슬롯0 취소 | PendingQueue=[B,C,...] → 다음=B |

---

## 시나리오 검증 테이블

| 시나리오 | 예상 PendingQueue (취소 전) | 슬롯 표시 | 검증 포인트 |
|---------|--------------------------|---------|-----------|
| 수동 [3,2,1] + 자동1 이관 | [2(m), 1(a)] | slot1=2, slot2=1 | Rule 2-1: 마지막 수동 이관 |
| 수동 [3,2,1] + 자동2 추가 | [2(m), 1(m), 2(a,false)] | slot1=2, slot2=1 | 자동2는 index2 대기 |
| 수동 [3,2] + 자동3 추가 | [2(m), 3(a,true)] | slot1=2, slot2=3 | 슬롯2 빈 자리 → 즉시 표시 |
| 순수 자동 [A,B,C] | [B(a,true), C(a,true)] | slot1=B, slot2=C | 기존 동작 유지 |
| 순수 자동 [A] | [] | slot1=없음 | count=1, 슬롯1 비어있음 |
| 큐 풀 + 자동 등록 | [X(m), Y(m), Z(a,false)] | slot1=X, slot2=Y | Z는 index2, IsCharged=false |

---

## 수정 파일 요약

| 파일 | 변경 내용 |
|------|---------|
| `Domain/Building/ProductionState.cs` | ManualQueue/AutoEntries/AutoIndex 제거. QueueSlot struct 추가. PendingQueue/AutoTypes/AutoCycleIndex/CurrentIsAuto 추가. IsAutoMode → 프로퍼티 |
| `Application/UseCases/UnitProductionUseCase.cs` | EnqueueUnit, ToggleAutoProduction, CancelQueueAt, TryStartNext, CompleteProduction, ChargeVisibleSlots 재작성. CollectChargedSlotEntries, TryPreChargeAutoEntries, CanAutoEntryShowInSlot 제거 |
| `Presentation/UI/ProductionPanelUI.cs` | UpdateQueueSlots 단순화. OnQueueSlotClicked fallback 제거. 인디케이터 판단 변경 |

---

## 위험 요소 및 주의사항

| 위험 | 대응 |
|------|------|
| CancelQueueAt slotIndex=0 후 TryStartNext 위임 안전성 | 시나리오 테스트 필수 (BUG-19 재발 여부) |
| ChargeVisibleSlots 자원 부족 시 미차감 항목이 슬롯에 표시될 수 있음 | IsCharged=false 항목은 UI에서 표시하지 않거나 별도 표시 처리 필요 |
| AutoCycleIndex가 AutoTypes 길이 변경 시 범위 초과 | AutoTypes 변경 시마다 `AutoCycleIndex %= AutoTypes.Count` 보정 |
| 기존 testcaserulefix FIX-1~10 케이스 회귀 | 재작성 후 해당 케이스 전체 재검증 |
| 멀티플레이 NetworkProductionController 동기화 | ProductionState 구조 변경 → SyncQueueStateClientRpc 동기화 데이터 포맷 변경 필요 여부 확인 |
