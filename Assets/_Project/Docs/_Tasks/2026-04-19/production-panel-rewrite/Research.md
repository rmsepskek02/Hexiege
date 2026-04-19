# Research — 생산 패널 전면 재작성

## 재작성 결정 배경

생산 패널 관련 버그가 여러 차례 수정되었음에도 동일한 유형의 버그가 반복 재발했다.
2026-04-18 수정 작업(auto-production-queue-bug-fix)에서 코드 정적 분석은 PASS였으나
실기 TC-001~003 전부 FAIL — 이는 코드의 지역적 수정으로는 해결이 불가함을 의미한다.
근본 원인은 **이중 큐 구조** 자체에 있으며, 완전한 재작성이 필요하다.

---

## 현재 구조의 근본 문제

### 이중 큐 구조

현재 시스템은 수동/자동 항목을 별도 컨테이너에 보관한다:
- `ManualQueue: List<UnitType>` — 수동 등록 항목
- `AutoEntries: List<AutoEntry>` — 자동 등록 항목 (IsCharged 포함)
- `AutoIndex: int` — AutoEntries 순환 위치

이 두 큐를 합쳐 슬롯1~2에 표시하려면 "둘을 어떻게 섞느냐"를 계산해야 한다.
이 계산이 `isNormalAutoState` 플래그이며, 이것이 모든 버그의 원천이다.

### isNormalAutoState 오판 문제

```
isNormalAutoState = autoCount > 0 &&
    (!CurrentProducing || (CurrentProducingIsAuto && AutoTypeAt(AutoIndex) == CurrentProducing))
```

이 플래그 하나가 세 곳(ProductionPanelUI, UnitProductionUseCase × 2)에서 각각 계산되며,
표시 순서(UpdateQueueSlots)와 취소 대상 결정(CancelQueueAt)이 이 값에 의존한다.
세 곳 중 하나라도 계산 방식이 다르면 "화면에 보이는 것"과 "취소되는 것"이 달라진다.

### AutoIndex 관리 복잡성

AutoEntries에서 항목이 추가/제거될 때마다 AutoIndex를 보정해야 한다.
보정 로직이 `ToggleAutoProduction`, `CancelQueueAt`, `CompleteProduction` 등 여러 곳에 분산되어
한 곳만 빠져도 인덱스가 어긋난다.

---

## 반복 버그 패턴

| 버그 유형 | 원인 | 발생 맥락 |
|----------|------|---------|
| 슬롯 순서 역전 | isNormalAutoState 오판 | 수동+자동 혼합 상태에서 자동 등록/취소 |
| 취소 대상 불일치 | UpdateQueueSlots와 CancelQueueAt 계산 순서 불일치 | 슬롯 X 버튼 클릭 |
| 자동 항목 표시 누락 | isNormalAutoState=true로 오판 → autoOffset=1 → 범위 초과 | 수동 생산 중 자동 등록 |
| AutoIndex 범위 초과 | 항목 제거 시 보정 누락 | 취소 연속 수행 |

---

## 수정이 아닌 재작성이 필요한 이유

- 정적 분석은 "코드가 의도대로 동작한다"는 것만 검증 → 설계 자체가 잘못된 경우 통과
- isNormalAutoState 계산이 N개 위치에 분산 → 한 곳 수정 시 다른 곳 회귀 발생
- 지금까지 최소 5회 이상 수정 시도에도 동일 버그 재발
- 이중 큐 구조를 단일 큐로 교체하면 isNormalAutoState 자체가 필요 없어짐

---

## 재사용할 설계 규칙

아래 규칙은 오랜 대화와 실기 테스트를 통해 확정된 것으로, 재작성 후에도 반드시 준수한다.

### 전역 규칙 5가지 (GameDesignDocument.md 최종 확정: 2026-03-23)

| Rule | 내용 | 비고 |
|------|------|------|
| Rule 1 | 슬롯 X 버튼으로 직접 취소 시 항상 전액 골드 환불 | 버튼 탭(자동 해제)은 환불 없음 |
| Rule 2 | 슬롯에 표시된(골드 차감된) 자동 항목은 자동 취소 후에도 생산 계속 | IsCharged=true 항목은 수동으로 이관하여 유지 |
| Rule 3 | 수동 생산 추가 시 자동 모드 전체 해제 | Rule 2 이관 먼저, 그 후 AutoTypes 클리어 |
| Rule 4 | 생산 큐 최대 3개 | CurrentProducing + 골드 차감된 대기 항목 합산. 미차감 자동 항목 미포함 |
| Rule 5 | 골드 차감 시점 = 슬롯에 표시되는 시점 | 슬롯 여유 있으면 즉시, 꽉 차면 슬롯 진입 시 |

### 버튼 동작 규칙

| 동작 | 조건 | 결과 |
|------|------|------|
| 유닛 버튼 탭 | 해당 타입이 자동 목록에 있음 | 자동 취소 (환불 없음) |
| 유닛 버튼 탭 | 해당 타입이 자동 목록에 없음 | 수동 추가 |
| 유닛 버튼 롱프레스 | 항상 | 자동 등록/취소 토글 |
| 슬롯 X 버튼 | 항상 | 해당 슬롯 취소 + 전액 환불 (Rule 1) |

### 검증 완료 동작 (testcaserulefix.md PASS 케이스)

| 상황 | 동작 | 결과 |
|------|------|------|
| 버튼 탭으로 자동 취소 | 골드 환불 없음 | FIX-1, FIX-2 PASS |
| IsCharged=true 자동 항목 탭 취소 | 슬롯 유지 (수동 이관) | FIX-10 PASS (Rule 2) |
| 수동 추가 → 자동 모드 해제 | IsCharged=true 항목 먼저 수동 이관 후 자동 클리어 | FIX-3 PASS |
| 큐 풀 상태 자동 등록 | IsCharged=false로 등록, 골드 미차감 | FIX-5 PASS |
| 미차감 자동 항목 슬롯 진입 | 진입 시점에 골드 검증+차감 | FIX-6 PASS |
| 미차감 자동 항목 탭 취소 | 골드 환불 없음 | FIX-7 PASS |

### 자동 등록 시 "수동 이관" 규칙 (Plan.md Rule 2-1)

자동으로 등록하려는 타입이 PendingQueue의 마지막 수동 항목과 같은 경우:
- 마지막 수동 항목을 자동으로 전환 (IsAuto=true, IsCharged 그대로 유지)
- 별도 항목 추가 없음 (중복 방지)
- 골드 추가 차감 없음 (이미 차감됨)
- 예: 수동 큐 [3,2,1] + 자동1 → [3,2,1(auto)], ManualQueue 마지막 1이 자동으로 전환

---

## 재작성 범위 (3개 파일)

| 파일 | 역할 |
|------|------|
| `Domain/Building/ProductionState.cs` | 데이터 구조 교체 (QueueSlot 도입, 이중 큐 제거) |
| `Application/UseCases/UnitProductionUseCase.cs` | 생산 로직 전면 재작성 |
| `Presentation/UI/ProductionPanelUI.cs` | UpdateQueueSlots 단순화, OnQueueSlotClicked fallback 제거 |

---

## 기존 검증 완료 케이스 (회귀 방지 대상)

재작성 후 아래 케이스들이 여전히 올바르게 동작해야 한다.

| 케이스 | 기대 동작 |
|--------|---------|
| 순수 자동 모드 [A,B,C] 순환 | A→B→C→A→B→C 무한 순환 |
| 수동 [3,2,1] 후 자동1 등록 | 마지막 1이 자동으로 전환, 큐 [3,2,1(auto)] |
| 수동 [3,2,1] 후 자동2 등록 | 큐 [3,2,1,2(auto)] |
| 수동 [3,2] 후 자동3 등록 | 슬롯2 비어 있으므로 즉시 표시, 큐 [3,2,3(auto)] |
| 자동 2개 중 슬롯1 타입 탭 취소 | 슬롯1 유지 (수동 이관), 슬롯2 비어있음 (FIX-10) |
| 큐 풀 상태 자동 등록 | 골드 미차감, 큐 풀이 해소될 때 차감 (FIX-5,6) |
| 수동 추가 시 자동 이관 | IsCharged=true 항목 수동 큐에 유지 (FIX-3) |
