# Research — 자동생산 재등록 시 슬롯 중복/누락 버그 (CurrentIsAuto 구조 개선)

작성일: 2026-06-05

---

## 작업 목적 (자연어 설명)

자동생산을 해제했다가 다시 등록할 때 슬롯에 유닛이 중복으로 표시되거나, 반대로 들어가야 할 슬롯에 표시되지 않는 버그들이 연달아 발견됐다. 개별 메서드를 패치하는 방식으로는 같은 버그가 다른 경로에서 반복 발생할 수 있어서, 근본 원인인 `CurrentIsAuto` 필드의 구조 문제를 해결한다.

---

## 발생한 버그 케이스

### 케이스 A: 자동 해제 후 재등록 → 슬롯2 중복

- Assault 자동 → 버튼 탭(해제) → 롱프레스(재등록)
- **현상**: 슬롯2에 Assault 중복 표시
- **기대**: 슬롯1의 생산 중 항목이 자동으로 전환되고 슬롯2는 비어있어야 함

### 케이스 B: 수동 추가 후 슬롯 취소 → 재등록 → 슬롯2 중복

- Assault 자동 → Pistoleer 수동 추가 → Pistoleer 슬롯 취소 → Assault 재등록
- **현상**: 슬롯2에 Assault 중복 표시

### 케이스 C: 수동이 큐에 있는 채로 재등록 → 슬롯3 미추가

- Assault 자동 → Pistoleer 수동 추가(큐에 유지) → Assault 재등록
- **현상**: 슬롯3에 Assault가 추가되지 않음
- **기대**: `[Assault(슬롯1)] [Pistoleer(슬롯2)] [Assault(슬롯3)]` 순환 큐가 되어야 함

---

## 근본 원인 분석

### `CurrentIsAuto`가 수동 관리 필드인 것이 문제

`ProductionState.cs`를 보면 `IsAutoMode`는 이미 파생 계산 방식을 사용한다:

```csharp
// IsAutoMode: 별도 필드 없이 AutoTypes에서 파생 → 불일치 원천 차단
public bool IsAutoMode => AutoTypes.Count > 0;
```

반면 `CurrentIsAuto`는 수동 set이 필요한 필드다:

```csharp
// CurrentIsAuto: 수동으로 관리 → 다양한 경로에서 reset 누락 시 불일치 발생
public bool CurrentIsAuto { get; set; }
```

`CurrentIsAuto`가 `true`로 남아있어야 할 조건은 세 가지가 동시에 성립할 때다:
1. 이 생산이 자동 경로에서 시작됐다 (`_currentIsAutoFlag`)
2. 현재 생산 중인 항목이 있다 (`CurrentProducing.HasValue`)
3. 해당 타입이 아직 AutoTypes에 등록되어 있다 (`AutoTypes.Contains(CurrentProducing.Value)`)

자동 해제 시 `AutoTypes`에서 타입이 제거되지만 `CurrentIsAuto`는 reset되지 않아, 3번 조건이 거짓임에도 `CurrentIsAuto=true`가 남아있게 된다.

### `CurrentIsAuto` 불일치가 발생하는 경로

| 경로 | AutoTypes 변경 | CurrentIsAuto reset | 결과 |
|------|--------------|---------------------|------|
| `UnregisterAutoType` | `RemoveAt` | ❌ 없음 | 불일치 → 케이스 A |
| `DisableAutoMode` | `Clear()` | ❌ 없음 | 불일치 → 케이스 B |
| `TryStartNext` 자원부족 경로 | `Clear()` | 없음 | CurrentProducing=null이므로 무관 |
| `CancelAutoTypeIfNeeded` | `RemoveAt` | 없음 | CancelCurrentProducing이 이미 false 처리 |
| `CancelAllQueue` | `Clear()` | ✅ false 함께 처리 | 정상 |

### `CurrentIsAuto` 불일치가 만드는 버그 흐름 (케이스 A/B)

```
자동 해제 후: CurrentIsAuto=true, AutoTypes=[]  ← 불일치
         ↓
재등록 시 RegisterAutoType → TryConvertCurrentToAuto
         ↓
  if (...|| state.CurrentIsAuto)  ← true이므로 전환 거부
         ↓
  AddNewAutoSlot 호출 → 슬롯2에 같은 유닛 추가  ← 버그
```

### `PendingQueue.Count` 조건 누락이 만드는 버그 흐름 (케이스 C)

케이스 A/B를 수동 reset 패치로 고치면, 이번엔 `TryConvertCurrentToAuto`가 너무 넓게 적용된다:

```
Pistoleer 큐에 있는 상태에서 Assault 재등록:
         ↓
TryConvertCurrentToAuto: CurrentIsAuto=false → 성공
         ↓
early return → AddNewAutoSlot 미실행 → 슬롯3 비어있음  ← 케이스 C 버그
```

Rule 20의 의도는 "슬롯0 바로 옆에 같은 타입 중복 방지"다. `[Assault(슬롯1)] [Pistoleer(슬롯2)] [Assault(슬롯3)]`은 중복이 아니므로, `TryConvertCurrentToAuto`는 **PendingQueue가 비어있을 때만** 적용해야 한다.

---

## 해결 방향: `CurrentIsAuto`를 파생 계산으로 전환

`IsAutoMode`와 동일한 설계 원칙을 적용한다. backing field를 두되 getter에서 AutoTypes 상태를 함께 검사한다:

```csharp
private bool _currentIsAutoFlag;

public bool CurrentIsAuto
{
    get => _currentIsAutoFlag
           && CurrentProducing.HasValue
           && AutoTypes.Contains(CurrentProducing.Value);
    set => _currentIsAutoFlag = value;
}
```

이렇게 하면 `AutoTypes`에서 타입이 제거되는 순간 `CurrentIsAuto` getter가 자동으로 `false`를 반환한다. `UnregisterAutoType`이나 `DisableAutoMode`에서 수동 reset이 필요 없어지고, 향후 새 경로가 추가되더라도 구조상 불일치가 발생하지 않는다.

setter는 기존과 동일하게 동작하므로 `UnitProductionUseCase`의 `state.CurrentIsAuto = true/false` 코드는 그대로 유지된다.

---

## 이전 작업과의 관계

- [10_59_auto-production-cycle-flicker](../10_59_auto-production-cycle-flicker/Research.md): CompleteProduction 깜빡임 버그 (완료). 이 버그와는 독립적.
- 2026-05-17 Rule 20 슬롯0 확장: `TryConvertCurrentToAuto` 도입 당시 `CurrentIsAuto`가 수동 필드로 남겨진 것이 현재 버그의 시작점.

---

## 관련 파일

| 파일 | 변경 여부 | 내용 |
|------|---------|------|
| `Assets/_Project/Scripts/Domain/Building/ProductionState.cs` | **수정** | `CurrentIsAuto` backing field + 파생 getter |
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | **수정** | 수동 reset 제거 + `RegisterAutoType` 조건 추가 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` | **수정** | 규칙 20 보완 |
