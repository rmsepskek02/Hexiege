# Plan: 자동생산 골드 소모 버그 수정

## 수정 대상 파일
- `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs`

---

## 버그 원인 (확인 완료)

`CompleteProduction` 메서드(라인 655)에서 자동 항목의 생산이 완료된 후:
1. `AutoIndex`를 다음 항목으로 순환시킴 (라인 686)
2. `TryPreChargeAutoEntries`로 슬롯 1~2 항목 사전 차감 시도 (라인 689)

그러나 **방금 생산 완료된 항목의 `IsCharged`가 `true`로 남아있는 채로 순환**되기 때문에:
- 다음 순환에서 해당 항목이 다시 `AutoIndex`에 도달할 때 `TryStartNext`에서 `IsCharged = true` 확인 → 차감 건너뜀
- `TryPreChargeAutoEntries`에서도 `IsCharged = true`이면 건너뜀

→ 결과: 자동생산 첫 등록 시 1회만 골드 소모, 이후 무한 무료 생산

---

## 수정 방법

### 위치
`CompleteProduction` 내부 → `AutoIndex` 순환 직전 (라인 685~686)

### 변경 내용
`AutoIndex` 순환 전, **방금 완료된 항목의 `IsCharged`를 `false`로 리셋**

```csharp
// 수정 전 (라인 685-686):
if (state.AutoContains(type))
    state.AutoIndex = (state.AutoIndex + 1) % state.AutoEntries.Count;

// 수정 후:
if (state.AutoContains(type))
{
    // 생산 완료된 항목의 IsCharged 리셋 → 다음 순환 시 골드 재차감
    var completedEntry = state.AutoEntries[state.AutoIndex];
    state.AutoEntries[state.AutoIndex] = new AutoEntry(completedEntry.Type, false);

    state.AutoIndex = (state.AutoIndex + 1) % state.AutoEntries.Count;
}
```

---

## 수정 후 동작 검증 (논리 추적)

### 단일 자동 항목 (Pistoleer 1개)
```
1. 첫 등록 → IsCharged=true, AutoIndex=0
2. TryStartNext → IsCharged=true → 차감 없이 생산 시작 ✓ (이미 차감됨)
3. 생산 완료:
   → AutoEntries[0] = {Pistoleer, false}  ← 리셋
   → AutoIndex = (0+1)%1 = 0
   → TryPreChargeAutoEntries: count=1, offset < 1 조건 → 루프 실행 안 됨
4. TryStartNext → IsCharged=false → SpendGold 차감 → 생산 시작 ✓
```

### 복수 자동 항목 (Pistoleer, Assault)
```
초기: AutoEntries=[{P,true},{A,true}], AutoIndex=0
1. TryStartNext → Pistoleer (IsCharged=true) → 차감 없이 시작 ✓
2. Pistoleer 생산 완료:
   → AutoEntries[0] = {P, false}  ← 리셋
   → AutoIndex = 1
   → TryPreChargeAutoEntries: offset 1 → idx=(1+1)%2=0, Pistoleer IsCharged=false
     → SpendGold(Pistoleer) → AutoEntries[0]={P,true}  ← 다음 슬롯 사전 차감
3. TryStartNext → Assault (IsCharged=true) → 차감 없이 시작 ✓
4. Assault 생산 완료:
   → AutoEntries[1] = {A, false}  ← 리셋
   → AutoIndex = 0
   → TryPreChargeAutoEntries: offset 1 → idx=(0+1)%2=1, Assault IsCharged=false
     → SpendGold(Assault) → AutoEntries[1]={A,true}  ← 사전 차감
5. TryStartNext → Pistoleer (IsCharged=true) → 차감 없이 시작 ✓
→ 매 생산마다 정확히 1회 골드 소모
```

---

## 환불 로직과의 충돌 여부

Rule 1: 생산 취소 시 `IsCharged=true`인 항목만 환불

리셋 타이밍은 **생산 완료 직후** (유닛 이미 스폰됨) → 이 시점에는 취소가 발생하지 않으므로 환불 로직과 충돌 없음.

---

## 위험 요소

없음. 단일 라인 추가 수준의 변경이며, `AutoEntry`는 구조체(struct)이므로 인덱서 직접 재할당이 안전.

---

## 체크리스트

- [ ] `CompleteProduction` 수정
- [ ] 단일 항목 자동생산 골드 소모 확인
- [ ] 복수 항목 자동생산 골드 소모 확인
- [ ] 취소 환불 동작 이상 없음 확인
