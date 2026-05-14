# Research: 자동생산 UI 개선

## 관련 파일

| 파일 | 레이어 | 역할 |
|------|--------|------|
| `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | Presentation | 생산 패널 UI, 버튼 입력 처리 |
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | Application | 생산 큐/타이머/자동모드 로직 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs` | Infrastructure | 멀티플레이 생산 동기화 |
| `Assets/_Project/Scripts/Domain/Building/ProductionState.cs` | Domain | 배럭 생산 상태 데이터 |

---

## 현재 상태 분석

### 1. 버그: 멀티플레이에서 항상 Pistoleer 자동생산

**원인 1 — NetworkProductionController.cs:518**
```csharp
// ❌ UnitType 파라미터 없이 항상 Pistoleer 하드코딩
bool success = production.ToggleAutoProduction(barracksId, UnitType.Pistoleer);
```

**원인 2 — ProductionPanelUI.cs:394**
```csharp
// ❌ type 파라미터 자체를 서버에 전달하지 않음
_networkProductionController.ToggleAutoServerRpc(
    _currentBarracks.Id,
    (int)_currentBarracks.Team);
```

싱글플레이는 `_production.ToggleAutoProduction(_currentBarracks.Id, type)`로 올바르게 전달 → 정상 동작.

---

### 2. 인디케이터 구조 문제

**현재**
```csharp
[SerializeField] private GameObject _autoIndicator; // 단일 오브젝트
```
`UpdateUI()`에서 `_autoIndicator.SetActive(state.IsAutoMode)` → 자동모드 ON/OFF만 표시.
어떤 유닛 버튼이 등록됐는지 구분 불가.

**필요한 것**: 버튼별 개별 인디케이터 3개 (Pistoleer / Assault / Sniper)

---

### 3. 자동모드 큐 슬롯 표시 미구현

**현재 UpdateQueueSlots 로직**
```csharp
// 슬롯 1~2는 ManualQueue만 표시
int queueIndex = i - 1;
if (queueIndex < state.ManualQueue.Count)
    slotType = state.ManualQueue[queueIndex];
```
자동모드에서 ManualQueue가 비어있으므로 슬롯 1~2가 항상 비어 보임.

**필요한 것**: 자동모드에서 슬롯 1~2에 다음 생산 예정 유닛 표시
- 슬롯 0: `state.CurrentProducing`
- 슬롯 1: `AutoTypes[(AutoIndex + 0) % count]` (다음)
- 슬롯 2: `AutoTypes[(AutoIndex + 1) % count]` (그 다음, count >= 3일 때만)

> 등록 순서: Assault → Sniper → Pistoleer 순으로 등록 시
> 슬롯0=Assault(생산중), 슬롯1=Sniper, 슬롯2=Pistoleer
> Assault 완료 → 슬롯0=Sniper, 슬롯1=Pistoleer, 슬롯2=Assault (순환)

---

### 4. 버튼 탭 동작 — 자동모드 중 미처리

**현재**: 탭은 항상 수동 큐 추가 (`OnUnitTap`)
**필요한 것**: 자동모드 ON 상태의 버튼 탭 → 해당 유닛 자동생산 취소

---

### 5. 취소 로직 현황

#### 버튼 취소 (현재)
롱프레스 → `ToggleAutoProduction` → AutoTypes에서 제거
- 자동생산 취소 시 골드 환불 없음
- 슬롯 0 취소 시 `CurrentProducing = null` → 즉시 중단

#### 슬롯 취소 (현재)
`CancelQueueAt` → ManualQueue에서만 제거 + 골드 환불
- 자동모드 인식 없음

#### 필요한 취소 동작

| 취소 방법 | 슬롯 0 (생산 중) | 슬롯 1~2 (예약) |
|----------|----------------|----------------|
| 버튼 탭/롱프레스 | AutoTypes에서 제거, 현재 생산 완료 허용, 환불 없음 | 즉시 제거 + 환불 |
| 슬롯 직접 클릭 | 즉시 취소 + 환불 + AutoTypes에서 제거 | 즉시 제거 + 환불 + AutoTypes에서 제거 |

---

### 6. 자동모드 최대 3개 제한 미구현

현재 `AutoTypes`에 개수 제한 없음. 최대 3개 제한 추가 필요.

---

### 7. 골드 선불(Pre-charge) 문제

**현재**: TryStartNext에서 자동모드 유닛의 골드를 생산 시작 시 차감
→ 슬롯 1~2의 유닛 골드는 아직 차감되지 않은 상태

**취소 시 환불을 하려면**: 슬롯 1~2가 화면에 표시될 때 골드를 선불 차감해야 함

**선불 차감 시점**:
- 새 타입이 AutoTypes에 추가되어 슬롯 1 또는 2에 들어올 때
- 슬롯 0 완료 후 순환하여 새 타입이 슬롯 2를 채울 때

**TryStartNext 영향**: 자동모드에서 슬롯 0 생산 시작 시 이미 골드가 선불된 경우 중복 차감 방지 필요
→ `ProductionState`에 선불 상태 추적 필드 추가 고려

---

### 8. 자동모드 슬롯 0 버튼 취소 시 AutoIndex 문제

Assault(idx0), Sniper(idx1), Pistoleer(idx2)에서 Assault를 버튼 취소(AutoTypes에서 제거):
- AutoTypes = [Sniper(0), Pistoleer(1)], AutoIndex = 0
- Assault는 CurrentProducing 유지 (생산 완료 허용)
- Assault 완료 시 CompleteProduction에서 AutoIndex 증가 → Pistoleer가 먼저 생산됨 (버그)

**필요한 수정**: CompleteProduction에서 완료된 type이 AutoTypes에 없으면 AutoIndex 증가 생략
```csharp
if (state.AutoTypes.Contains(type))
    state.AutoIndex = (state.AutoIndex + 1) % state.AutoTypes.Count;
// AutoTypes에 없으면(버튼 취소된 경우) AutoIndex는 이미 다음 타입을 가리킴
```

---

## 영향 범위 요약

| 파일 | 변경 규모 |
|------|----------|
| `ProductionPanelUI.cs` | 대 — 버튼별 인디케이터, 탭 분기, 큐 슬롯 표시 로직 |
| `UnitProductionUseCase.cs` | 중 — ToggleAutoProduction(최대 3개), CancelQueueAt(자동모드), CompleteProduction(AutoIndex), 골드 선불 로직 |
| `ProductionState.cs` | 소 — 선불 상태 추적 필드 추가 가능성 |
| `NetworkProductionController.cs` | 소 — ToggleAutoServerRpc에 unitTypeInt 파라미터 추가 |
