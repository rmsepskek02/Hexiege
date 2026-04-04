# Research: 자동생산 골드 소모 버그

## 작업 배경
자동생산 시 유닛이 대기열에 추가될 때마다 골드가 소모되어야 하나,
첫 등록 시에만 골드가 소모되고 이후 순환(반복 생산)에서는 소모되지 않는 버그.

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Domain/Building/ProductionState.cs` | 생산 상태, AutoEntry 구조체 (IsCharged 플래그) |
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | 골드 차감 핵심 로직 |
| `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs` | 매 프레임 Tick 실행 |

---

## 핵심 구조 파악

### AutoEntry 구조체 (ProductionState.cs)
```
AutoEntry {
    UnitType Type      // 유닛 타입
    bool IsCharged     // 골드 차감 완료 여부
}
```
- `IsCharged = true` → 이미 골드 차감 완료, 중복 차감 방지
- `IsCharged = false` → 아직 차감 안 됨, 슬롯 표시 시점에 차감 예정

---

## 골드 소모 발생 지점 (UnitProductionUseCase.cs)

### 1. 자동생산 첫 등록 시 (ToggleAutoProduction)
- 슬롯에 즉시 표시 가능한 경우 → `SpendGold()` 호출 + `IsCharged = true`
- 슬롯이 꽉 찬 경우 → `IsCharged = false`로 추가 (나중에 차감 예정)

### 2. 생산 시작 시 (TryStartNext)
- `IsCharged = false`인 자동 항목이 슬롯 0(CurrentProducing)으로 올라올 때
- 골드/인구 검증 후 `SpendGold()` 호출 + `IsCharged = true`

### 3. 생산 완료 후 사전 차감 (TryPreChargeAutoEntries)
- `CompleteProduction` 완료 직후 호출
- 슬롯 1~2에 표시될 다음 자동 항목들을 미리 차감
- **조건**: `IsCharged = false`인 항목만 처리

---

## 버그 원인 분석

### 자동생산 순환 흐름
```
1. 첫 등록 (ToggleAutoProduction)
   → SpendGold() 호출
   → IsCharged = true 설정
   → 생산 시작

2. 생산 완료 (CompleteProduction)
   → AutoIndex = (AutoIndex + 1) % AutoEntries.Count
   → TryPreChargeAutoEntries() 호출

3. TryPreChargeAutoEntries 내부
   → IsCharged = false인 항목만 SpendGold() 시도
   → [문제] 이미 IsCharged = true이므로 → 차감 건너뜀!

4. 다음 TryStartNext
   → IsCharged = true이므로 → 차감 건너뜀!

→ 결국 첫 등록 시 차감한 골드 1회만 소모
```

### 핵심 원인
`CompleteProduction` 또는 `AutoIndex` 순환 시점에 **소비된 AutoEntry의 `IsCharged`를 `false`로 리셋하지 않음**.

AutoEntry는 영구 등록 항목이고, 생산이 완료되어 다음 순환으로 넘어가도 `IsCharged` 플래그가 그대로 `true`로 유지되기 때문에, 이후 순환에서 골드 차감 로직이 모두 건너뛰어진다.

---

## 영향 범위

- **버그 영향**: 자동생산 2번째 순환부터 골드 소모 없음 → 골드 제약 없이 무한 생산 가능
- **수동 생산**: 영향 없음 (EnqueueUnit은 별도 로직)
- **멀티플레이**: 서버 측 UnitProductionUseCase에서 동일 로직 실행 → 동일하게 영향받음
- **골드 환불(Rule 1)**: IsCharged 기반으로 환불 여부 결정 → 리셋 위치 주의 필요

---

## 수정 방향 (상세는 Plan.md 참조)

생산 완료된 AutoEntry의 `IsCharged`를 `false`로 리셋하는 위치를 찾아 적용.
리셋 시점은 해당 항목의 생산이 **완료**된 직후여야 하며, 환불 로직과 충돌하지 않아야 함.
