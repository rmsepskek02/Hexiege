# Testcase: 자동생산 골드 소모 버그

## TC 목록

---

### SINGLE-1: 자동생산 단일 항목 — 반복 생산 시 골드 소모

**전제:** 싱글플레이, Blue팀 배럭 1개, 골드 충분 (500 이상), 자동생산 미등록 상태

**동작:**
1. 유닛 버튼을 길게 눌러 Pistoleer(50골드) 자동생산 등록
2. 첫 번째 Pistoleer 생산 완료까지 대기
3. 생산 완료 직후 골드 잔액 확인
4. 두 번째 Pistoleer 생산 완료까지 대기
5. 생산 완료 직후 골드 잔액 재확인

**기댓값:**
- 첫 번째 생산 완료 후: 등록 시점 골드에서 50골드 차감된 상태
- 두 번째 생산 완료 후: 첫 번째 완료 시 골드에서 추가로 50골드 차감된 상태
- 매 생산마다 50골드씩 소모됨

**결과:** PASS

---

### SINGLE-2: 자동생산 복수 항목 — 각 유닛 생산 시 해당 골드 소모

**전제:** 싱글플레이, Blue팀 배럭 1개, 골드 충분 (500 이상), 자동생산 미등록 상태

**동작:**
1. Pistoleer(50골드)를 길게 눌러 자동생산 등록
2. Assault(100골드)를 길게 눌러 자동생산 추가 등록
3. Pistoleer 생산 완료까지 대기 → 골드 잔액 확인
4. Assault 생산 완료까지 대기 → 골드 잔액 확인
5. 다시 Pistoleer 생산 완료까지 대기 → 골드 잔액 확인

**기댓값:**
- Pistoleer 생산 완료마다 50골드 소모
- Assault 생산 완료마다 100골드 소모
- 순환이 반복되어도 매번 해당 유닛의 골드가 정상 소모됨

**결과:** PASS

---

### SINGLE-3: 자동생산 취소 시 환불 정상 동작

**전제:** 싱글플레이, 자동생산으로 Pistoleer 등록 후 생산 중 상태, 골드 기록

**동작:**
1. Pistoleer 자동생산 등록 후 생산이 시작된 것을 확인
2. 대기열에 표시된 다음 Pistoleer(슬롯 1) 버튼을 길게 눌러 자동생산 취소
3. 골드 잔액 확인

**기댓값:**
- 취소 전 슬롯 1에 표시되어 이미 골드가 차감된 항목 → 취소 시 50골드 환불됨
- 현재 생산 중인 유닛(슬롯 0)은 영향 없이 계속 생산

**결과:** PASS

---

### MULTI-1: 멀티플레이 — 자동생산 반복 시 골드 소모

**전제:** Host(Blue) + Client(Red) 구성, Host 기준 배럭 1개, 골드 충분

**동작:**
1. Host가 Pistoleer 자동생산 등록
2. 첫 번째 생산 완료까지 대기 → Host 골드 UI 확인
3. 두 번째 생산 완료까지 대기 → Host 골드 UI 확인

**기댓값:**
- 매 생산마다 Host UI에서 50골드 차감이 반영됨
- Client UI에서도 Host 골드가 동기화되어 동일하게 감소함

**결과:** 에이전트 실기 불가 — 사용자 확인 필요

---

## QA 섹션

<!-- qa-tester 에이전트 전용 공간 -->

### 정적 분석

**수정 파일**: `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs`
**수정 위치**: `CompleteProduction` 메서드 내 AutoIndex 순환 블록 (라인 685~691 근처)

**수정 내용**:
```csharp
if (state.AutoContains(type))
{
    // 생산 완료된 항목의 IsCharged 리셋 — 다음 순환 시 골드를 다시 차감하기 위해
    var completedEntry = state.AutoEntries[state.AutoIndex];
    state.AutoEntries[state.AutoIndex] = new AutoEntry(completedEntry.Type, false);

    state.AutoIndex = (state.AutoIndex + 1) % state.AutoEntries.Count;
}
```

**검증 포인트**:
1. `IsCharged` 리셋 시점이 유닛 스폰 완료 이후인지 (환불 로직과 충돌 없음)
2. `TryPreChargeAutoEntries`가 리셋된 항목을 올바르게 재차감하는지
3. `TryStartNext`에서 리셋된 항목이 IsCharged=false 경로로 처리되는지
4. struct 재할당 방식이 기존 코드 패턴과 일치하는지

---

## 정적 분석 결과 (qa-tester)

**분석 일시:** 2026-04-04
**분석 대상:** `UnitProductionUseCase.cs` — `CompleteProduction` 수정 (라인 685~694)

---

### 검증 포인트 분석

#### 1. IsCharged 리셋 시점 — 환불 로직과의 충돌 여부

`CompleteProduction` 실행 흐름 추적:

1. `_unitSpawn.SpawnUnit(...)` 호출 — 유닛 스폰 성공 여부 확인 (라인 668)
2. 스폰 실패 시 즉시 return — 리셋 코드 미실행 (라인 669~672)
3. 스폰 성공 시 `state.CurrentProducing = null` (라인 676) 설정
4. 이후 리셋 코드 실행 (라인 691)

환불 경로(`CancelQueueAt`)는 `state.CurrentProducing.HasValue` 기준으로 동작한다. 리셋 코드 실행 시점에는 이미 `CurrentProducing = null`로 설정되어 있으므로, 취소 경로가 개입할 수 없는 구조이다. 리셋 직전에 유닛이 이미 스폰 완료된 상태이므로 환불 로직과의 충돌 없음.

**판정: 이상 없음**

---

#### 2. TryPreChargeAutoEntries의 리셋 항목 재차감 여부

`TryPreChargeAutoEntries` (라인 760) 루프 구조:

- `offset = 1`부터 시작하여 `AutoIndex` 위치(offset=0)를 명시적으로 건너뜀 (라인 771)
- 순환 전 리셋된 항목은 `AutoIndex` 위치에 있음 (`IsCharged=false`)
- 순환 후(`AutoIndex = (AutoIndex+1)%Count`) 해당 항목은 마지막 위치로 이동
- 항목이 1개인 경우: `offset < count` 조건(`1 < 1` = false)으로 루프 미진입 → 슬롯1~2 사전 차감 대상 없음. 다음 `TryStartNext`에서 `IsCharged=false` 경로로 차감됨
- 항목이 2개 이상인 경우: 리셋된 항목이 `offset >= 1` 범위에 들어오면 `IsCharged=false` 조건 충족 → 골드 검증 후 재차감

Plan.md의 동작 검증 논리(단일/복수 항목 추적)와 코드 구현이 일치함.

**판정: 이상 없음**

---

#### 3. TryStartNext의 IsCharged=false 경로 처리

`TryStartNext` (라인 575) 코드 경로:

- `entry.IsCharged`가 false이면 골드/인구 검증 후 `SpendGold` 호출 (라인 601~610)
- 차감 후 `state.AutoEntries[state.AutoIndex] = new AutoEntry(type, true)`로 `IsCharged=true` 갱신 (라인 613)
- 단일 항목 시나리오: `TryPreChargeAutoEntries`에서 미차감 → `TryStartNext`에서 `IsCharged=false` 경로 진입 → 정상 차감

리셋 후 `AutoIndex`가 순환하여 해당 항목이 다시 슬롯0에 오를 때 반드시 `IsCharged=false`이므로, `TryStartNext`의 차감 분기가 정상 실행됨.

**판정: 이상 없음**

---

#### 4. struct 재할당 패턴 일치 여부

`AutoEntry`는 struct로 선언되어 있어 리스트 인덱서로 직접 접근 후 재할당이 필요하다. 기존 코드에서 동일 패턴을 이미 3곳에서 사용 중임을 확인:

- 라인 350: `state.AutoEntries[state.AutoIndex] = new AutoEntry(nextAuto.Type, true);`
- 라인 613: `state.AutoEntries[state.AutoIndex] = new AutoEntry(type, true);`
- 라인 791: `state.AutoEntries[idx] = new AutoEntry(entry.Type, true);`

이번 수정(라인 691)도 동일 패턴을 따르고 있어 코드 일관성 확인.

**판정: 이상 없음**

---

### TC별 정적 분석 판정

| TC | 판정 | 근거 |
|----|------|------|
| SINGLE-1 | PASS | 실기 확인 완료 (2026-04-04) |
| SINGLE-2 | PASS | 실기 확인 완료 (2026-04-04) |
| SINGLE-3 | PASS | 실기 확인 완료 (2026-04-04) |
| MULTI-1 | 에이전트 실기 불가 — 사용자 확인 필요 | 서버 측 UnitProductionUseCase에서 동일 로직 실행. 멀티플레이 동기화는 정적 분석 범위 외 |

---

### 종합 판정: PASS (2026-04-04 실기 완료)

수정된 코드(`CompleteProduction` 내 `IsCharged` 리셋 + `AutoIndex` 순환)는 Plan.md의 수정 내용과 정확히 일치하며, 검증 포인트 4항목 모두 이상 없음을 확인. 환불 로직과의 충돌 없음, struct 재할당 패턴 일치, TryStartNext/TryPreChargeAutoEntries 재차감 흐름 정상 추적 완료.

실기 테스트(SINGLE-1~3, MULTI-1)로 최종 확인 필요.
