# Research — Rule 20 슬롯0 확장

## 작업 개요 (자연어 설명)

현재 생산 규칙 20은 "대기 큐(슬롯1/2)의 마지막 항목이 수동 A일 때 자동등록하면 중복 추가 없이 자동으로 전환"합니다.
그러나 슬롯0(현재 생산 중)에 수동 A가 있는 상태에서 A를 자동등록하면, 규칙 20이 적용되지 않아 슬롯1에도 A가 추가됩니다.

이 작업은 규칙 20을 슬롯0까지 확장하여, 슬롯0에서 수동으로 A를 생산하는 도중 A를 자동등록하면 슬롯1에 중복 추가하지 않고 슬롯0을 자동으로 전환하도록 합니다.

---

## 현재 동작 분석

### ToggleAutoProduction — 미등록 타입 추가 경로 흐름

1. AutoTypes 상한 체크 (< 3)
2. **Rule 2-1**: PendingQueue 마지막 항목이 수동 A이면 → AutoTypes 추가 + IsAuto=true 전환 후 반환
3. canShow 판정: `CurrentProducing.HasValue && ChargedPendingCount() < 2`
4. BUG-15 방어: `canShow && CurrentIsAuto && CurrentProducing == type` → canShow=false
5. canShow=true이면 골드 차감 + PendingQueue 추가
6. AutoTypes 추가

### 슬롯0에 A 수동 생산 중 + A 자동등록 시 (현재 결과)

| 판정 조건 | 값 | 결과 |
|-----------|-----|------|
| Rule 2-1 체크 | PendingQueue 비어있음 | 미적용 |
| canShow 판정 | CurrentProducing.HasValue=true, ChargedPendingCount=0 | **true** |
| BUG-15 방어 | CurrentIsAuto=false (수동 생산) | 방어 미발동 → canShow 유지 |

→ **슬롯1에 A(자동, 골드 차감) 추가** → 슬롯0/슬롯1 모두 A

### 문제점

- 사용자 의도: "지금 만들고 있는 거 계속 자동으로 만들어줘"
- 실제 결과: 슬롯1에 A가 중복으로 쌓임 + 불필요한 골드 선차감 발생

---

## 영향 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | ToggleAutoProduction 로직 수정 대상 |
| `Assets/_Project/Docs/GameSystemRules.md` | 규칙 20 문구 업데이트 |

---

## 관련 규칙 및 코드 포인트

- **Rule 2-1 (현재)**: PendingQueue 마지막 항목 기준 중복 방지 → `UnitProductionUseCase.cs:222-241`
- **BUG-15 방어**: CurrentIsAuto=true이고 같은 타입이면 canShow=false → `UnitProductionUseCase.cs:250-251`
  - BUG-15는 이미 **자동**으로 생산 중인 경우 (CurrentIsAuto=true)
  - 이번 확장은 **수동**으로 생산 중인 경우 (CurrentIsAuto=false) → 충돌 없음
- **CompleteProduction**: `wasAuto && AutoTypes.Contains(type)` 조건으로 자동 순환 재추가 → `UnitProductionUseCase.cs:660-662`
  - 슬롯0을 자동 전환(CurrentIsAuto=true)하면 완료 시 자동 순환이 정상 작동함

---

## 추가 이슈 없음
