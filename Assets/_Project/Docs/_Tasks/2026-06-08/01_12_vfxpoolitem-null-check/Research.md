# Research — VfxPoolItem 파괴된 오브젝트 참조 버그

## 이 작업이 무엇인지

테스트 중 유닛이 사망할 때 아래 에러가 발생합니다.

> MissingReferenceException: The object of type 'VfxPoolItem' has been destroyed but you are still trying to access it.

VFX 오브젝트를 재사용하기 위해 Pool에 보관해두었는데, 이미 파괴된 오브젝트를 꺼내 사용하려 해서 발생하는 버그입니다. 이 Research는 정확한 원인 위치와 재현 조건을 분석합니다.

---

## 에러 스택 트레이스 전문

```
MissingReferenceException: The object of type 'VfxPoolItem' has been destroyed
VfxPoolItem.Play (Vector3 pos, Quaternion rot)          ← VfxPoolItem.cs:89
EffectManager.Play (EffectPreset preset, Vector3 pos)   ← EffectManager.cs:221
EffectManager.PlayUnitDeath (UnitType type, Vector3 pos)← EffectManager.cs:170
UnitView.<SetDependencies>b__39_7 (UnitDiedEvent e)     ← UnitView.cs:451
```

---

## 버그 발생 흐름

```
[1] 유닛 공격 VFX 재생 (OnAttackHit)
      ↓
[2] 파티클 재생 완료 → VfxPoolItem.Return() 호출
      ↓
[3] gameObject.SetActive(false) → Pool Queue에 반환
      ↓
[4] (씬 정리 또는 외부 원인으로 VfxPoolItem GameObject가 Destroy됨)
      ↓
[5] 유닛 사망 → EffectManager.PlayUnitDeath() 호출
      ↓
[6] GetOrCreateVfx() → Queue.Dequeue() → 파괴된 VfxPoolItem 반환 ← 여기서 에러
      ↓
[7] item.Play() → transform 접근 → MissingReferenceException
```

---

## 버그 위치

**파일**: `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs`

```csharp
// EffectManager.cs:258-262 (GetOrCreateVfx 내부)
if (queue.Count > 0)
    return queue.Dequeue();   // ← null(파괴) 체크 없이 반환
```

Unity에서 GameObject가 `Destroy`되면 C# 참조는 살아있지만, Unity의 `==` 연산자는 `null`로 평가합니다. Queue에는 이 "가짜 null" 참조가 남아 있어, 꺼낼 때 이미 파괴된 오브젝트를 반환합니다.

---

## 파괴 원인 추정

스택 트레이스를 보면 `NetworkCombatController.DelayedAttackDamage` 코루틴에서 이벤트가 발행됩니다. 코루틴이 딜레이 중에 씬 정리(또는 게임 종료) 가 일어나면, EffectManager와 그 자식 VfxPoolItem들이 먼저 파괴됩니다. 이후 코루틴이 재개되어 UnitDied 이벤트가 발행되면 이미 파괴된 VfxPoolItem을 Queue에서 꺼내려 하면서 에러가 발생합니다.

---

## 영향 범위

| 파일 | 변경 유형 | 설명 |
|------|----------|------|
| `EffectManager.cs` | 수정 | `GetOrCreateVfx` 내 null 체크 추가 |
