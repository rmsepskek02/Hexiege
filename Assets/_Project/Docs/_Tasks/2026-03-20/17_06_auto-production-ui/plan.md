# Plan: 자동생산 UI 개선

## 작업 목표

1. 자동생산 등록/취소 로직 완성 (멀티플레이 버그 수정 포함)
2. 버튼별 개별 인디케이터 표시
3. 자동모드 큐 슬롯 1~2 표시 구현
4. 버튼 탭/롱프레스 동작 분기 (자동모드 ON/OFF 상태 구분)
5. 취소 시 골드 환불 처리

---

## 변경 파일 목록

### 1. `ProductionState.cs` (Domain)

**추가 사항**:
- `AutoPreChargedCount` — 현재 선불 차감된 슬롯 수 추적
  - 슬롯 1에 선불 시 1, 슬롯 2에도 선불 시 2
  - 슬롯 0 생산 시작 시 1 감소 (소비됨)

```csharp
// 선불 차감된 슬롯 수 (슬롯1, 슬롯2)
public int AutoPreChargedCount { get; set; } = 0;
```

---

### 2. `UnitProductionUseCase.cs` (Application)

#### 2-1. `ToggleAutoProduction` 수정
```
변경 내용:
- 최대 3개 제한 추가 (AutoTypes.Count >= 3 → 추가 거부)
- 신규 타입 추가 시:
  - 슬롯 1 또는 2에 들어오는 경우(= AutoTypes.Count가 2 또는 3이 되는 경우) 골드 선불 차감
  - AutoPreChargedCount += 1
  - 골드/인구 부족 시 추가 거부
- 기존 타입 제거(취소) 시:
  - 슬롯 0 (AutoIndex 위치): AutoTypes에서 제거, CurrentProducing 유지, 환불 없음
  - 슬롯 1~2 (AutoIndex+1, +2): 선불된 골드 환불, AutoPreChargedCount -= 1
```

#### 2-2. `TryStartNext` 수정
```
변경 내용:
- 자동모드에서 슬롯 0 생산 시작 시:
  - AutoPreChargedCount > 0이면 골드 이미 선불됨 → 중복 차감 생략
  - AutoPreChargedCount -= 1
  - AutoPreChargedCount == 0이면 기존처럼 골드/인구 검증 + 차감
```

#### 2-3. `CompleteProduction` 수정
```
변경 내용:
- AutoIndex 증가 로직 수정:
  if (state.AutoTypes.Contains(type))  // 완료된 type이 AutoTypes에 있을 때만 증가
      state.AutoIndex = (state.AutoIndex + 1) % state.AutoTypes.Count;
  // 버튼 취소된 타입(AutoTypes에 없음)은 AutoIndex 이미 올바른 위치 → 증가 생략

- 순환 후 새로 슬롯 2가 채워지는 경우 선불 차감:
  - AutoTypes.Count == 3이고 순환이 발생한 경우
  - 새 슬롯 2에 해당하는 타입의 골드 선불 + AutoPreChargedCount += 1
  - 골드 부족 시: 자동모드 중단 없이 슬롯 2가 비어 보임 (다음 완료 시 재시도)
```

#### 2-4. `CancelQueueAt` 수정
```
변경 내용 (자동모드 분기 추가):
- state.IsAutoMode == true인 경우:
  - slotIndex 0: CurrentProducing 즉시 취소 + 골드 환불 + AutoTypes에서 해당 타입 제거
  - slotIndex 1~2: 해당 슬롯의 AutoTypes 타입 제거 + 선불된 골드 환불
    - 슬롯 1 → AutoTypes[(AutoIndex) % count] 제거
    - 슬롯 2 → AutoTypes[(AutoIndex + 1) % count] 제거
    - AutoPreChargedCount -= 1
  - AutoTypes가 비면 IsAutoMode = false
- state.IsAutoMode == false인 경우: 기존 ManualQueue 로직 유지
```

---

### 3. `ProductionPanelUI.cs` (Presentation)

#### 3-1. 버튼별 인디케이터 필드 교체
```
제거: [SerializeField] private GameObject _autoIndicator;
추가:
  [SerializeField] private GameObject _pistoleerAutoIndicator;
  [SerializeField] private GameObject _assaultAutoIndicator;
  [SerializeField] private GameObject _sniperAutoIndicator;
```

#### 3-2. 버튼 탭 동작 분기 (`OnUnitTap`)
```
변경 내용:
if (자동모드 ON && 해당 type이 AutoTypes에 등록되어 있음)
    → 자동생산 취소 (CancelAutoUnit 호출 또는 ToggleAutoProduction)
else
    → 기존 수동 큐 추가 (EnqueueUnit)
```

#### 3-3. 롱프레스 동작 분기 (`OnUnitLongPress`)
```
변경 내용:
if (자동모드 ON && 해당 type이 AutoTypes에 등록되어 있음)
    → 탭과 동일하게 취소
else
    → 기존 자동생산 등록 (ToggleAutoProduction)
```

#### 3-4. 큐 슬롯 표시 `UpdateQueueSlots` 수정
```
자동모드 분기 추가:
if (state.IsAutoMode)
{
    // 슬롯 0: CurrentProducing
    // 슬롯 1: AutoTypes[(AutoIndex) % count] if count >= 2
    // 슬롯 2: AutoTypes[(AutoIndex + 1) % count] if count >= 3
}
else
{
    // 기존 ManualQueue 로직
}
```

#### 3-5. 인디케이터 업데이트 `UpdateUI` 수정
```
변경 내용:
- _autoIndicator.SetActive() 제거
- 버튼별 인디케이터: AutoTypes.Contains(type)로 ON/OFF
  _pistoleerAutoIndicator.SetActive(state.AutoTypes.Contains(UnitType.Pistoleer));
  _assaultAutoIndicator.SetActive(state.AutoTypes.Contains(UnitType.Assault));
  _sniperAutoIndicator.SetActive(state.AutoTypes.Contains(UnitType.Sniper));
```

---

### 4. `NetworkProductionController.cs` (Infrastructure)

#### 4-1. `ToggleAutoServerRpc` 파라미터 추가
```
변경 내용:
- int unitTypeInt 파라미터 추가
- UnitType.Pistoleer 하드코딩 → (UnitType)unitTypeInt 로 변경
```

#### 4-2. `AutoProductionChangedClientRpc` 수정
```
변경 내용:
- int unitTypeInt 파라미터 추가
- UnitType.Pistoleer 하드코딩 → (UnitType)unitTypeInt 로 변경
```

#### 4-3. `ProductionPanelUI.ToggleAutoServerRpc` 호출부 수정
```
변경 내용:
_networkProductionController.ToggleAutoServerRpc(
    _currentBarracks.Id,
    (int)type,            // ← 추가
    (int)_currentBarracks.Team);
```

---

## Inspector 작업

| 항목 | 내용 |
|------|------|
| `_autoIndicator` 필드 제거 | Inspector에서 기존 연결 해제 |
| 버튼별 인디케이터 오브젝트 | 각 유닛 버튼 하위에 인디케이터 GameObject 3개 생성 |
| `_pistoleerAutoIndicator` | PistoleerButton 하위 인디케이터 연결 |
| `_assaultAutoIndicator` | AssaultButton 하위 인디케이터 연결 |
| `_sniperAutoIndicator` | SniperButton 하위 인디케이터 연결 |

> ⚠️ Inspector 연결 작업은 Editor 1회성 스크립트 또는 수동 작업 필요 — 구현 완료 후 사용자에게 안내

---

## 취소 동작 전체 정리

### 버튼 탭 또는 롱프레스 (자동모드 ON 상태)
| 해당 유닛 위치 | 동작 |
|--------------|------|
| 슬롯 0 (생산 중) | AutoTypes에서 제거, 현재 생산 완료 허용, 환불 없음 |
| 슬롯 1 또는 2 (예약) | AutoTypes에서 제거 + 선불된 골드 환불 |

### 생산큐 슬롯 직접 클릭
| 슬롯 | 동작 |
|------|------|
| 슬롯 0 | 현재 생산 즉시 취소 + 골드 환불 + AutoTypes에서 제거 |
| 슬롯 1 또는 2 | 해당 타입 AutoTypes에서 제거 + 골드 환불 |

---

## 위험 요소

| 항목 | 위험도 | 대응 |
|------|--------|------|
| AutoPreChargedCount 관리 | 중 | 취소/완료/등록 모든 경로에서 정합성 유지 필요 |
| AutoIndex 관리 | 중 | 제거/완료 모든 경로에서 경계값 체크 |
| 멀티플레이 선불 동기화 | 중 | SyncQueueStateClientRpc에 AutoPreChargedCount 추가 고려 |
| 슬롯 슬라이딩 (슬롯 1 취소 시 2→1 이동) | 저 | UpdateQueueSlots에서 AutoIndex 기준 재계산으로 자동 처리 |

---

## 구현 순서

```
[1] ProductionState.cs — AutoPreChargedCount 필드 추가
[2] UnitProductionUseCase.cs — ToggleAutoProduction, TryStartNext, CompleteProduction, CancelQueueAt 수정
[3] NetworkProductionController.cs — ToggleAutoServerRpc, AutoProductionChangedClientRpc 수정
[4] ProductionPanelUI.cs — 인디케이터 필드, 탭/롱프레스 분기, UpdateQueueSlots, UpdateUI 수정
[5] Inspector 작업 — 버튼별 인디케이터 오브젝트 생성 및 연결
```

---

## 담당 에이전트

- **game-programmer**: [1]~[4] 코드 구현
- **사용자**: [5] Inspector 작업

---

---

# 2차 버그 수정 (2026-03-21) — 사용자 실기 테스트 결과 반영

## 공통 원칙 (신규 확정)

> **생산이 취소되면 항상 전액 환불** — 예외 없음

---

## 발견된 버그 및 기획 변경

### BUG-01. 수동 모드 슬롯1 취소 시 슬롯1,2 모두 취소

**현상**: 수동생산 큐 3개 예약 후 슬롯1 클릭 → 슬롯1·2 동시 취소
**의심 원인**: `SetupQueueSlotButtons`에서 버튼 클릭 리스너 중복 등록 가능성,
또는 `CancelQueueAt` 수동 분기 로직 오류
**수정 방향**:
- `SetupQueueSlotButtons` 버튼 리스너 등록 전 기존 리스너 제거(`RemoveAllListeners`) 확인
- `CancelQueueAt` 수동 분기 동작 검증 (slotIndex=1 → ManualQueue[0]만 제거되는지 확인)

---

### BUG-02. 자동생산 버튼 취소 시 슬롯0 생산도 중단 + 환불 없음

**현상**: 자동생산 ON 상태에서 유닛 버튼 탭/롱프레스로 취소 시 슬롯0 생산까지 중단됨, 환불 없음
**원인 분석**: `ToggleAutoProduction`에서 마지막 AutoType 제거 후 `AutoTypes.Count == 0` 분기 진입 → `CurrentProducing = null` 처리 (환불 없이)
```csharp
// 현재 (버그)
if (state.AutoTypes.Count == 0)
{
    state.IsAutoMode = false;
    state.AutoIndex = 0;
    if (state.CurrentProducing.HasValue)
    {
        state.CurrentProducing = null; // ← 환불 없이 생산 취소
        ...
    }
}
```
**수정 방향**:
- `ToggleAutoProduction`의 `AutoTypes.Count == 0` 분기에서 `CurrentProducing` 취소 로직 제거
- 슬롯0는 항상 생산 완료 허용 (버튼 취소든 자동 해제든 동일)

---

### DESIGN-03. 수동생산 추가 시 자동생산 공존 (TC-04-3 기획 변경)

**변경 전**: 수동생산 추가 시 자동생산 전체 즉시 취소 (슬롯0 포함)
**변경 후**: 자동 모드는 해제되지만 슬롯0 생산은 완료까지 유지

**수정 위치**: `UnitProductionUseCase.EnqueueUnit` — 자동 모드 해제 분기
```csharp
// 현재 (버그)
if (state.CurrentProducing.HasValue)
{
    int refund = UnitProductionStats.GetGoldCost(state.CurrentProducing.Value);
    _resource.AddGold(state.Team, refund);   // 환불
    state.CurrentProducing = null;            // ← 이 블록 전체 제거
    state.ElapsedTime = 0f;
    state.RequiredTime = 0f;
}

// 수정 후: AutoTypes 클리어 + 선불 환불만, CurrentProducing은 유지
```

---

### BUG-04. TC-05-2: 버튼 취소 시 슬롯0 생산 중단

**현상**: 자동생산 중 유닛 버튼으로 취소 시 슬롯1,2만 취소되어야 하나 슬롯0도 중단됨
**원인**: BUG-02와 동일 — `ToggleAutoProduction` 내 `AutoTypes.Count==0` 분기 문제
**수정**: BUG-02 수정과 동일

---

### BUG-05. TC-06-2: 슬롯1 클릭 시 슬롯0도 취소

**현상**: 자동모드에서 슬롯1 클릭 → 슬롯0(생산중) 유닛도 취소됨
**의심 원인**:
- `CancelQueueAt` auto 분기에서 `autoSlotOffset = slotIndex`로 수정 후 count=1인 경우 `(AutoIndex+1)%1=0` → 현재 생산 타입 제거 → AutoTypes 비면 CurrentProducing 취소 경로 진입 가능성
- 또는 UI 슬롯 클릭 이벤트가 slotIndex=0으로 잘못 전달될 가능성
**수정 방향**:
- `CancelQueueAt` 자동 모드 슬롯1~2 취소 시 빈 슬롯(표시 없는 슬롯) 클릭 방어 로직 추가
- `AutoTypes.Count == 0` 분기에서 `CurrentProducing` 취소 제거 (BUG-02와 동일)
- 슬롯1 취소 전 `count >= 2` 검증 추가

---

### BUG-06. TC-06-3: 슬롯2 취소 후 연쇄 버그 (AutoIndex/AutoTypes 상태 오염)

**현상**: 3개 자동생산 중 슬롯2 취소 후 슬롯0,2 자동생산 취소되고 큐 슬롯1,2 생산 취소됨. 이후 빈 슬롯1 클릭 시 추가 취소 발생
**원인**: 여러 동작을 이어서 할 때 AutoIndex와 AutoTypes가 불일치 상태로 오염, 이후 모든 계산 오류
**수정 방향**:
- 모든 취소 경로(버튼/슬롯)에서 AutoIndex 보정 로직 재검토
- `AutoPreChargedCount` 정합성 검증 강화
- 빈 슬롯 클릭 방어: `slotIndex >= 1`이고 해당 위치에 표시할 타입이 없으면 조기 return

---

## 수정 대상 파일 및 범위

| 파일 | 수정 내용 |
|------|----------|
| `UnitProductionUseCase.cs` | `ToggleAutoProduction`: AutoTypes.Count==0 시 CurrentProducing 유지 |
| `UnitProductionUseCase.cs` | `EnqueueUnit`: 자동 해제 시 CurrentProducing 유지 (DESIGN-03) |
| `UnitProductionUseCase.cs` | `CancelQueueAt`: 빈 슬롯 방어, 전액 환불 경로 보강 |
| `ProductionPanelUI.cs` | `SetupQueueSlotButtons`: 리스너 중복 등록 방지 확인 |

> ⚠️ **game-programmer에게**: 코드 수정 전 반드시 각 파일을 Read 도구로 직접 읽고 현재 상태를 확인할 것. 연속 동작 시나리오를 머릿속으로 추적하며 AutoIndex/AutoTypes/AutoPreChargedCount 세 상태값의 정합성을 모든 경로에서 검증할 것.

---

## 구현 순서 (2차)

```
[1] UnitProductionUseCase.cs — ToggleAutoProduction, EnqueueUnit, CancelQueueAt 수정
[2] ProductionPanelUI.cs — SetupQueueSlotButtons 리스너 방어 확인
```

---

## 3차 버그 수정 및 설계 변경 (2026-03-22 실기 테스트 후)

### 전역 규칙 확정 (5가지)

> 이하 모든 수정은 이 규칙을 기준으로 판단한다.

1. **생산이 취소되면 항상 전액 환불** — 예외 없음
2. **자동생산이 취소되어도 생산큐에 등록된 것은 그대로 생산** — 자동 모드 해제 시 큐 항목 유지, 환불 없음
3. **수동생산을 시행한 경우 모든 자동생산은 취소** — 인디케이터 OFF, AutoTypes 클리어. 단 큐 항목은 Rule 2에 따라 유지
4. **생산큐는 최대 3개까지 등록가능** — CurrentProducing + ManualQueue 합산 기준 (자동 대기는 별도)
5. **비용 차감은 생산큐에 추가될 때 차감** — 자동 등록 시점 X, 생산 시작(TryStartNext) 시점 O

---

### DESIGN-04. 수동 추가 시 자동 큐 유지 (Rule 2+3 통합)

**변경 전 (DESIGN-03)**: 수동 추가 시 선불 환불 + AutoTypes 클리어 → 슬롯1+ 항목 제거
**변경 후**: 선불 환불 없이 선불된 AutoTypes 항목을 ManualQueue로 이전 후 AutoTypes 클리어

**수정 위치**: `UnitProductionUseCase.EnqueueUnit` — 자동 모드 해제 분기

```
// 수정 전: 선불 환불 후 AutoTypes 클리어
for (int i = 0; i < AutoPreChargedCount; i++) { AddGold(refund); }
AutoTypes.Clear(); IsAutoMode=false;

// 수정 후: 선불 항목을 ManualQueue 앞에 삽입 (환불 없음), AutoTypes 클리어
// 선불 순서대로 ManualQueue에 Insert(0, ...) → 기존 순서 유지 후 새 수동 항목 뒤에 추가
for (int i = 0; i < AutoPreChargedCount; i++) {
    int slotIdx = (AutoIndex + 1 + i) % AutoTypes.Count;
    ManualQueue.Insert(i, AutoTypes[slotIdx]);   // 큐 앞쪽에 순서대로 삽입
}
AutoPreChargedCount = 0; IsAutoMode = false; AutoTypes.Clear();
// 이후 새 수동 항목 ManualQueue.Add(type) 기존대로
```

**영향**: 슬롯1 항목이 ManualQueue로 이전되므로, 이후 표시는 수동 모드 큐 표시 로직으로 처리

---

### BUG-07. 슬롯1 표시 오류 — count < 2 시 미표시 (B-2, F-3, 신규)

**현상**: 자동 등록된 타입이 1개만 남을 때 슬롯1이 빈칸으로 표시됨
**원인**: `UpdateQueueSlots` — `count >= 2`일 때만 슬롯1 표시
**영향 케이스**:
- B-2: Assault 버튼 탭 후 Sniper(슬롯1) 사라짐
- F-3 4단계: Assault 버튼 탭 후 Sniper(슬롯1) 사라짐
- 수동+자동 혼용: ManualQueue 있을 때 auto 슬롯 미표시

**수정 위치**: `ProductionPanelUI.UpdateQueueSlots`

```csharp
// 수정 전
else if (count >= 2 && i == 1) { slotType = AutoTypes[(AutoIndex+1) % count]; }
else if (count >= 3 && i == 2) { slotType = AutoTypes[(AutoIndex+2) % count]; }

// 수정 후: 자동 모드 + ManualQueue 혼용 표시
// slot 1: ManualQueue 우선, 없으면 AutoTypes 다음 항목
// slot 2: ManualQueue[1] 우선, 없으면 AutoTypes 그 다음 항목
if (i == 1) {
    if (ManualQueue.Count > 0) slotType = ManualQueue[0];
    else if (count >= 1) slotType = AutoTypes[(AutoIndex + 1) % count];
}
else if (i == 2) {
    if (ManualQueue.Count > 1) slotType = ManualQueue[1];
    else if (ManualQueue.Count == 1 && count >= 1) slotType = AutoTypes[(AutoIndex + 1) % count];
    else if (count >= 2) slotType = AutoTypes[(AutoIndex + 2) % count];
}
```

> ⚠️ `TryStartNext`의 ManualQueue 우선 순서와 일치시킬 것

---

### BUG-08. 자동 재등록 차단 — ManualQueue 존재 시 (F-2)

**현상**: 수동 추가 후 자동 생산을 등록하려 하면 등록 불가. 슬롯0 생산 완료 후에야 등록 가능
**원인**: `ToggleAutoProduction` L154 — `if (state.ManualQueue.Count > 0) return false;`
**수정 방향**: 해당 조건 제거. ManualQueue가 있어도 자동 등록 허용
- `needPreCharge` 조건은 `CurrentProducing.HasValue`로 유지 (기존대로)
- 슬롯 표시는 BUG-07 수정(UpdateQueueSlots 혼용 로직)으로 처리

**주의**: ManualQueue + AutoTypes 동시 존재 시 TryStartNext는 ManualQueue 우선으로 동작 (기존 코드 유지)

---

### 수정 대상 파일 및 범위 (3차)

| 파일 | 수정 내용 |
|------|----------|
| `UnitProductionUseCase.cs` | `EnqueueUnit`: 선불 환불 제거 + ManualQueue 이전 (DESIGN-04) |
| `UnitProductionUseCase.cs` | `ToggleAutoProduction`: ManualQueue.Count > 0 차단 조건 제거 (BUG-08) |
| `ProductionPanelUI.cs` | `UpdateQueueSlots`: 슬롯1 count>=1 표시 + 혼용 모드 처리 (BUG-07) |

> ⚠️ **game-programmer에게**: 3가지 수정은 상호 연관됨. 반드시 순서대로 수정하고 각 수정 후 AutoIndex/AutoTypes/ManualQueue/AutoPreChargedCount 4개 상태값 정합성을 전 경로에서 검증할 것.

---

## 구현 순서 (3차)

```
[1] UnitProductionUseCase.cs — EnqueueUnit 선불 환불 → ManualQueue 이전으로 변경
[2] UnitProductionUseCase.cs — ToggleAutoProduction ManualQueue 차단 조건 제거
[3] ProductionPanelUI.cs — UpdateQueueSlots 혼용 모드 표시 수정
```

---

## 4차 설계 변경 (2026-03-22 규칙 추가)

### 전역 규칙 변경 사항

**Rule 4 범위 변경**: 생산큐 최대 3개 = CurrentProducing + ManualQueue 기준
- 자동 등록(AutoTypes)은 큐 상한과 무관하게 항상 허용
- 자동 대기 중인 항목은 빈 슬롯이 생길 때 비로소 큐에 진입

**Rule 5 신규**: 비용 차감은 생산큐에 추가될 때
- `ToggleAutoProduction` 등록 시 골드 차감 없음
- `EnqueueUnit` 호출 시 골드 즉시 차감 (수동, 기존 유지)
- `TryStartNext`에서 자동 유닛 생산 시작 시 골드 차감 (기존 비선불 경로)

---

### DESIGN-05. AutoPreChargedCount 시스템 전면 제거 (Rule 5)

**배경**: Rule 5로 인해 자동 등록 시 선불 개념이 사라짐 → `AutoPreChargedCount` 불필요

**제거 대상**:
- `ProductionState.AutoPreChargedCount` 필드
- `ToggleAutoProduction` — needPreCharge 분기 전체 제거
- `EnqueueUnit` — AutoPreChargedCount 환불 루프 및 ManualQueue 이전 로직 전체 제거 (AutoTypes.Clear + IsAutoMode=false만 유지)
- `CancelQueueAt` 자동 슬롯1~2 — AutoPreChargedCount 환불 분기 제거 (Rule 5: 미차감 → 환불 없음)
- `CompleteProduction` — 선불 보충 루프 전체 제거

**변경 후 자동 슬롯1~2 취소 시**: AutoTypes에서 제거만, 환불 없음

---

### DESIGN-06. 자동 등록 큐 상한 제거 (Rule 4 변경)

**배경**: Rule 4가 자동 대기를 포함하지 않으므로 ToggleAutoProduction의 큐 풀 차단 제거

**제거 대상**:
- `ToggleAutoProduction` — `totalCount + 1 > MaxQueueSize` 체크 제거
- `AutoTypes.Count >= 3` 상한은 유지 (최대 3종류 자동 등록 제한)

**자동 대기 → 큐 진입 로직**:
- `TryStartNext`에서 ManualQueue 소진 후 IsAutoMode=true이면 AutoTypes[AutoIndex] 생산 시작
- Rule 4(최대 3개)는 ManualQueue.Count + (CurrentProducing ? 1 : 0) 기준으로만 체크

---

### 수정 대상 파일 및 범위 (4차)

| 파일 | 수정 내용 |
|------|----------|
| `ProductionState.cs` | `AutoPreChargedCount` 필드 제거 |
| `UnitProductionUseCase.cs` | `ToggleAutoProduction`: needPreCharge 분기 제거, totalCount 큐 상한 제거 |
| `UnitProductionUseCase.cs` | `EnqueueUnit`: AutoPreChargedCount 환불 루프 제거, ManualQueue 이전 로직 제거 |
| `UnitProductionUseCase.cs` | `CancelQueueAt` 자동 슬롯1~2: AutoPreChargedCount 환불 분기 제거 |
| `UnitProductionUseCase.cs` | `CompleteProduction`: 선불 보충 루프 제거 |

> ⚠️ **game-programmer에게**: AutoPreChargedCount 참조 전체 제거 후 컴파일 오류 없는지 확인할 것.
> `CancelQueueAt` 자동 슬롯1~2는 환불 없이 AutoTypes에서 제거만 수행.
> `TryStartNext` 자동 경로는 기존 비선불 경로(골드 검증 후 차감)가 그대로 유지됨.

---

## 구현 순서 (4차)

```
[1] ProductionState.cs — AutoPreChargedCount 제거
[2] UnitProductionUseCase.cs — ToggleAutoProduction, EnqueueUnit, CancelQueueAt, CompleteProduction 수정
[3] 컴파일 확인 (AutoPreChargedCount 참조 잔재 없는지)
```

---

## 5차 규칙 재해석 및 코드 수정 (2026-03-22 실기 테스트 후)

### 전역 규칙 재해석 (최종 확정)

4차에서 Rule 5를 잘못 해석하여 코드가 규칙에 맞지 않게 구현됨.

| 규칙 | 4차 해석 (오류) | 5차 해석 (확정) |
|------|----------------|----------------|
| Rule 5 | 자동 등록 시 골드 미차감, TryStartNext 시 차감 | "생산큐에 추가" = 슬롯에 표시되는 시점 → 그 시점에 차감 |
| Rule 2+3 | 수동 추가 시 AutoTypes.Clear → 슬롯1 유닛 소멸 | 자동 모드 취소(Rule 3) + 슬롯에 표시된 유닛은 수동 큐 이관(Rule 2) |

---

### DESIGN-07. Rule 5 최종 해석 — 슬롯 표시 시점에 차감

**케이스별 골드 차감 시점:**
- 자동 등록 시 해당 유닛이 슬롯1 또는 슬롯2에 즉시 표시 가능한 경우 → `ToggleAutoProduction` 시 즉시 차감
- 큐 풀 상태에서 자동 등록 시 슬롯에 표시 안 됨 → 빈 슬롯 생겨 큐에 진입할 때(`TryStartNext`) 차감

**슬롯에 즉시 표시 가능한 조건:**
- `CurrentProducing.HasValue` && 등록 후 AutoTypes가 슬롯1 또는 슬롯2에 해당하는 경우
- 큐 풀 상태(CurrentProducing + ManualQueue.Count >= MaxQueueSize)이면 슬롯 진입 불가 → 미차감

**취소 시 환불 여부:**
- 골드가 차감된 유닛 취소 → 환불 (Rule 1)
- 골드 미차감 유닛(큐 풀 대기) 취소 → 환불 없음

---

### DESIGN-08. Rule 2+3 최종 해석 — 수동 추가 시 자동 유닛 수동 큐 이관

**수정 전 (4차)**: 수동 추가 시 AutoTypes.Clear → 슬롯에 표시된 자동 유닛 소멸

**수정 후:**
- Rule 3: 자동 모드 취소 (인디케이터 OFF, IsAutoMode=false)
- Rule 2: 슬롯에 이미 표시된 자동 유닛들(골드 차감 완료) → ManualQueue 앞에 순서대로 이관
- 큐 풀 대기 유닛(골드 미차감)은 이관 없이 소멸 (슬롯에 없었으므로 Rule 2 해당 없음)
- 새 수동 유닛은 ManualQueue 맨 뒤에 추가

**수정 위치**: `UnitProductionUseCase.EnqueueUnit` — 자동 모드 해제 분기

---

### 수정 대상 파일 및 범위 (5차)

| 파일 | 수정 내용 |
|------|----------|
| `UnitProductionUseCase.cs` | `ToggleAutoProduction`: 슬롯 표시 가능한 경우 즉시 골드 차감 |
| `UnitProductionUseCase.cs` | `CancelQueueAt` 자동 슬롯1~2: 골드 차감된 경우 환불 복원 |
| `UnitProductionUseCase.cs` | `EnqueueUnit`: 슬롯에 표시된 자동 유닛 ManualQueue 이관 후 AutoTypes.Clear |
| `UnitProductionUseCase.cs` | `TryStartNext`: 큐 풀 대기 유닛만 이 시점에 골드 차감 (이미 차감된 유닛 중복 차감 방지) |

> ⚠️ **game-programmer에게**: 4차에서 제거한 선불 개념이 부분적으로 복원됨.
> 단, AutoPreChargedCount 카운터 방식이 아닌, "슬롯 표시 가능 여부" 기반으로 설계할 것.
> 각 자동 유닛의 골드 차감 여부를 추적하는 방법을 직접 설계하되,
> AutoTypes와 연동하여 취소/이관 시 정합성을 유지할 것.

---

## 구현 순서 (5차)

```
[1] UnitProductionUseCase.cs — ToggleAutoProduction, EnqueueUnit, CancelQueueAt, TryStartNext 수정
[2] 컴파일 확인
```
