# Plan — VfxPoolItem 파괴된 오브젝트 참조 버그 수정

## 이 Plan이 무엇인지

VFX Pool에서 오브젝트를 꺼낼 때, 이미 파괴된 오브젝트인지 확인하지 않아서 발생하는 에러를 수정합니다. 수정 범위는 `EffectManager.cs` 한 메서드 내부 3줄입니다.

---

## 수정 내용

### [수정] EffectManager.cs
**경로**: `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs`

**변경 전** (`GetOrCreateVfx` 내부):
```csharp
// Queue에 대기 항목이 있으면 재사용, 없으면 새 인스턴스 생성.
if (queue.Count > 0)
    return queue.Dequeue();

return CreateVfxInstance(prefab);
```

**변경 후**:
```csharp
// Queue에 대기 항목이 있으면 재사용, 없으면 새 인스턴스 생성.
// Unity에서 GameObject가 Destroy되면 C# 참조는 남아있지만
// Unity의 == 연산자가 null로 평가하므로 반드시 null 체크가 필요하다.
// (씬 정리, 딜레이 코루틴 재개 등으로 Queue 안에 파괴된 항목이 남을 수 있음)
while (queue.Count > 0)
{
    VfxPoolItem item = queue.Dequeue();
    if (item != null)   // 파괴되지 않은 항목만 반환
        return item;
    // 파괴된 항목은 버리고 다음 항목 시도
}

return CreateVfxInstance(prefab);
```

**변경 이유**: `if` → `while`로 변경하여, Queue 안에 파괴된 항목이 여러 개 있어도 유효한 항목을 찾을 때까지 계속 시도합니다. 모두 파괴되었으면 새로 생성합니다.

GameSystemRules 근거: 없음 (VFX Pool 시스템 신규 버그 수정)

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| while 루프 무한 반복 | Queue는 유한하므로 Count가 0이 되면 반드시 종료됨 |
| CreateVfxInstance 과도한 호출 | 파괴된 항목은 이미 메모리에서 사라진 것 — 새로 만드는 것이 올바름 |

---

## 구현 순서

```
[1] EffectManager.cs GetOrCreateVfx 메서드 수정 (if → while + null 체크)
```
