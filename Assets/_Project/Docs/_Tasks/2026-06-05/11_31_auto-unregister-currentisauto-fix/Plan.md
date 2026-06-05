# Plan — 자동생산 재등록 슬롯 버그 수정 (CurrentIsAuto 구조 개선)

작성일: 2026-06-05

---

## 작업 목적 (자연어 설명)

자동생산 해제/재등록 시 슬롯 중복 표시 또는 누락 버그를 구조적으로 해결한다. 개별 메서드를 패치하는 대신, `CurrentIsAuto` 필드를 `IsAutoMode`와 동일한 파생 계산 방식으로 전환하여 `AutoTypes` 상태와의 불일치 자체를 구조상 불가능하게 만든다. 추가로 `TryConvertCurrentToAuto`의 적용 범위를 제한하여 케이스 C(슬롯3 미추가) 버그도 함께 수정한다.

---

## GameSystemRules 근거

- **생산 패널 UI 규칙 20 (자동 생산 중복 방지)**: "같은 타입이 이미 슬롯0에서 수동 생산 중이면 중복 추가 없이 기존 항목을 자동으로 전환한다."
  - 보완: PendingQueue에 다른 항목이 있을 때는 슬롯0 전환 대신 슬롯3에 새로 추가하는 것이 올바른 동작이다 (중간에 다른 유닛이 있으므로 중복이 아님). → `GameSystemRules_UI.md` 업데이트 필요

---

## 수정 1: `ProductionState.cs` — `CurrentIsAuto` 파생 계산 전환

### 수정 파일

`Assets/_Project/Scripts/Domain/Building/ProductionState.cs`

### 변경 전

```csharp
/// <summary>
/// 현재 슬롯0(CurrentProducing)이 자동 생산으로 시작되었는지 여부.
/// ...
/// </summary>
public bool CurrentIsAuto { get; set; }
```

### 변경 후

```csharp
/// <summary>
/// 현재 슬롯0(CurrentProducing)이 자동 생산으로 시작되었는지 여부.
///
/// [2026-06-05 구조 개선] IsAutoMode와 동일한 파생 계산 방식으로 전환.
/// 별도 bool 필드를 두면 자동 해제(UnregisterAutoType, DisableAutoMode 등) 시
/// reset 누락으로 AutoTypes 상태와 불일치가 발생하는 구조적 문제가 있었다.
///
/// getter: backing flag가 true이고, 현재 생산 중인 타입이 AutoTypes에 아직 있을 때만 true.
///         → AutoTypes에서 타입이 제거되는 순간 자동으로 false 반환 (수동 reset 불필요).
/// setter: _currentIsAutoFlag만 갱신. 기존 코드(state.CurrentIsAuto = true/false)와 호환.
/// </summary>
public bool CurrentIsAuto
{
    get => _currentIsAutoFlag
           && CurrentProducing.HasValue
           && AutoTypes.Contains(CurrentProducing.Value);
    set => _currentIsAutoFlag = value;
}

/// <summary> CurrentIsAuto getter의 backing field. "이 생산이 자동으로 시작됐는가"만 저장. </summary>
private bool _currentIsAutoFlag;
```

---

## 수정 2: `UnitProductionUseCase.cs` — 수동 reset 제거

### 수정 파일

`Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs`

### 2-1. `UnregisterAutoType` — 수동 reset 제거

**제거할 코드 (2026-06-05에 추가됐던 패치):**

```csharp
// [2026-06-05 버그 수정] 해제하는 타입이 현재 자동으로 생산 중인 타입이면
// CurrentIsAuto를 함께 false로 초기화한다.
// ...
if (state.CurrentIsAuto && state.CurrentProducing == type)
    state.CurrentIsAuto = false;
```

**이유**: ProductionState의 getter가 `AutoTypes.Contains`를 검사하므로, `AutoTypes.RemoveAt` 직후 `CurrentIsAuto`는 자동으로 false를 반환한다. 수동 reset이 불필요하다.

---

### 2-2. `DisableAutoMode` — 수동 reset 제거

**제거할 코드 (2026-06-05에 추가됐던 패치):**

```csharp
// [2026-06-05 버그 수정] 자동 모드 전체 해제 시 CurrentIsAuto도 함께 초기화한다.
// ...
if (state.CurrentIsAuto)
    state.CurrentIsAuto = false;
```

**이유**: `AutoTypes.Clear()` 직후 getter가 자동으로 false를 반환한다. 수동 reset이 불필요하다.

---

### 2-3. `RegisterAutoType` — `PendingQueue.Count == 0` 조건 추가

**변경 전:**

```csharp
// Rule 20 확장: 슬롯0(CurrentProducing)이 같은 타입 수동 항목이면 수동→자동 전환.
if (TryConvertCurrentToAuto(state, barracksId, type))
    return true;
```

**변경 후:**

```csharp
// Rule 20 확장: 슬롯0(CurrentProducing)이 같은 타입 수동 항목이면 수동→자동 전환.
// PendingQueue가 비어있을 때만 적용한다.
// PendingQueue에 다른 항목이 있으면 슬롯0 전환 대신 AddNewAutoSlot으로 슬롯3에 추가한다.
// 예: [Assault(슬롯1)] [Pistoleer(슬롯2)] 상태에서 Assault 재등록 →
//     슬롯0만 전환하면 슬롯3이 비어있어 사용자가 순환 큐를 확인할 수 없음.
//     Pistoleer가 중간에 있으므로 슬롯3 추가가 올바른 동작이다.
if (state.PendingQueue.Count == 0 && TryConvertCurrentToAuto(state, barracksId, type))
    return true;
```

---

## 수정 3: `GameSystemRules_UI.md` — 규칙 20 보완

### 수정 파일

`Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md`

### 추가 내용 (규칙 20 하단에 추가)

```
단, 대기 큐(PendingQueue)에 다른 항목이 있을 경우에는 슬롯0 전환 대신
대기 큐 끝에 새로 추가한다. 중간에 다른 유닛이 있으므로 이 경우는 중복이 아니다.
예: [Assault(슬롯0)] [Pistoleer(슬롯2)] 상태에서 Assault 자동 등록 →
    슬롯3에 Assault 추가 → [Assault(슬롯1)] [Pistoleer(슬롯2)] [Assault(슬롯3)]
```

---

## 케이스별 동작 검증

| 케이스 | 수정 전 | 수정 후 |
|--------|---------|---------|
| A: 자동 해제 → 재등록 (PendingQueue 비어있음) | `CurrentIsAuto=true` 스테일 → TryConvert 거부 → 슬롯2 중복 ❌ | AutoTypes=[] → getter=false → PendingQueue.Count==0 → TryConvert 성공 → 슬롯0 전환 ✅ |
| B: 수동 추가 후 취소 → 재등록 (PendingQueue 비어있음) | 동일 ❌ | 동일 ✅ |
| C: Pistoleer 큐에 있는 채로 재등록 | TryConvert 성공 → early return → 슬롯3 비어있음 ❌ | PendingQueue.Count>0 → 건너뜀 → AddNewAutoSlot → 슬롯3 Assault 추가 ✅ |
| 정상: 자동 1종 생산 중 (해제 없음) | CurrentIsAuto=true → TryConvert 거부 (이미 자동이므로 맞음) ✅ | AutoTypes=[Assault] → getter=true → TryConvert 거부 (이미 자동이므로 맞음) ✅ |
| 정상: 수동 Assault 생산 중 → 자동 등록 | CurrentIsAuto=false → TryConvert 성공 ✅ | AutoTypes에 없음 → getter=false → PendingQueue.Count==0 → TryConvert 성공 ✅ |

---

## 위험 요소

- **setter 호환성**: `state.CurrentIsAuto = true/false` 코드가 9곳에 있으나, 모두 `_currentIsAutoFlag`만 설정하므로 기존 동작과 동일하다. getter의 추가 조건이 값을 보정하므로 각 set 시점의 정확성에 덜 의존한다.
- **CompleteProduction의 `wasAuto` 캡처**: `bool wasAuto = state.CurrentIsAuto`는 `state.CurrentProducing = null` 이전에 실행된다. 캡처 시점에 `CurrentProducing.HasValue=true` + `AutoTypes.Contains(type)` 조건이 정확하게 평가된다. 순서 변경 없음.
- **성능**: `AutoTypes.Contains`는 O(n), AutoTypes.Count ≤ 3이므로 사실상 O(1). 성능 영향 없음.

---

## 수정 파일 전체 목록

| 파일 | 수정 위치 | 내용 |
|------|----------|------|
| `Assets/_Project/Scripts/Domain/Building/ProductionState.cs` | `CurrentIsAuto` 프로퍼티 | backing field + 파생 getter 추가 |
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | `UnregisterAutoType` | 수동 reset 블록 제거 |
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | `DisableAutoMode` | 수동 reset 블록 제거 |
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | `RegisterAutoType` L197 | `PendingQueue.Count == 0` 조건 추가 (1행) |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` | 규칙 20 하단 | 대기 큐에 항목 있을 때 슬롯3 추가 동작 명시 |
