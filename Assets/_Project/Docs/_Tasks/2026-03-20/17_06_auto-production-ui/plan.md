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
