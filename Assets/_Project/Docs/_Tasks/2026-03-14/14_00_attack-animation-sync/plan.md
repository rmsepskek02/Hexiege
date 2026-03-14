# Plan: 공격 애니메이션-타격 반응 시점 동기화

**작성일:** 2026-03-14
**목표:** 실제 데미지/사망 타이밍은 그대로 유지하면서,
타격 모션 프레임 시점에 맞춰 시각 반응(scale punch + HP바 플래시)만 추가.

---

## 채택 방식: Animation Event (방식 2)

유닛마다 애니메이션이 다르므로 타격 프레임을 정확하게 지정하는 Animation Event 방식 채택.

### 방식 비교 요약

| 항목 | 코루틴 hit ratio | Animation Event |
|------|-----------------|-----------------|
| 정밀도 | 비율 추정 (±0.05s 오차 가능) | 프레임 단위 정확 |
| 유닛별 개별 조정 | Inspector float 수정 | .anim 이벤트 이동 |
| 구현 복잡도 | 낮음 | 중간 (bridge 컴포넌트 필요) |
| 향후 유닛 추가 | hit ratio 설정 필요 | .anim 이벤트 추가 필요 |

Animation Event는 Unity 에디터에서 시각적으로 타격 프레임 위치를 확인하며 설정 가능 → 직관적이고 정밀.

---

## 구현 계획

### Step 1: AnimationEventRelay.cs 생성

`Assets/_Project/Scripts/Presentation/Unit/AnimationEventRelay.cs`

```
역할: Animator가 있는 자식 GameObject에 부착하여
      Animation Event 호출을 부모 UnitView로 전달.
```

```csharp
// AnimationEventRelay.cs
// Animator 자식 GameObject에 부착.
// Animation Event "OnAttackHit" 수신 → UnitView로 전달.
public class AnimationEventRelay : MonoBehaviour
{
    private UnitView _unitView;

    private void Awake()
    {
        _unitView = GetComponentInParent<UnitView>();
    }

    // Animation Event에서 호출 (클립 이름: OnAttackHit)
    public void OnAttackHit()
    {
        _unitView?.OnAttackHit();
    }
}
```

### Step 2: UnitView.OnAttackHit() 추가

`UnitView.cs`에 추가:

```csharp
// 현재 공격 대상 Id (TriggerAttackAnimation 호출 시 저장)
private int _currentTargetId;
private bool _currentTargetIsUnit;

// TriggerAttackAnimation 수정: 타겟 정보 저장 추가
public void TriggerAttackAnimation(int targetId, bool targetIsUnit)
{
    if (_unitData == null || !_unitData.IsAlive) return;
    _currentTargetId = targetId;
    _currentTargetIsUnit = targetIsUnit;
    // ... 기존 코드 유지
}

// AnimationEventRelay → 타격 프레임에서 호출됨
public void OnAttackHit()
{
    if (_unitData == null || !_unitData.IsAlive) return;
    StartCoroutine(HitReactionCoroutine());
}

// 시각 반응: scale punch
private IEnumerator HitReactionCoroutine()
{
    Vector3 originalScale = transform.localScale;
    transform.localScale = originalScale * 0.85f;
    yield return new WaitForSeconds(0.05f);
    transform.localScale = originalScale;
}
```

> **HP바 플래시**: 현재 UnitView가 HP바 UI에 직접 접근하지 않음.
> 추후 HP바 컴포넌트 구조 확인 후 추가 (이번 구현에서는 scale punch만 적용).

### Step 3: 각 유닛 프리팹에 AnimationEventRelay 부착

유닛 프리팹 구조 (예시):
```
Unit_Pistoleer_Blue (root — UnitView 있음)
  └─ [FBX 루트 오브젝트] (Animator 있음 — 여기에 AnimationEventRelay 부착)
       └─ Armature
            └─ ...
```

대상 프리팹 (6개):
- Unit_Pistoleer_Blue/Red.prefab
- Unit_Assault_Blue/Red.prefab
- Unit_Sniper_Blue/Red.prefab

Blue/Red는 같은 Animator Controller/FBX를 공유하므로,
**한 팀 프리팹에서 Animator 자식에 AnimationEventRelay를 부착하면 나머지 팀은 동일하게 처리.**

### Step 4: 각 Attack.anim 클립에 Animation Event 추가

대상 클립 (3개):
- `Pistoleer_Attack.anim`
- `Assault_Attack.anim`
- `Sniper_Attack.anim`

Unity Animation 창에서:
1. 클립 선택
2. 타격 프레임 위치에 커서 이동 (육안으로 팔이 앞으로 나오는 프레임)
3. Add Event → Function 이름: `OnAttackHit`

---

## 변경 없는 부분 (중요)

- `UnitCombatUseCase.TryAttack()` — 데미지 적용 타이밍 무변경
- `NetworkCombatController.TickCombat()` — 서버 전투 로직 무변경
- `EntityDiedClientRpc` — 사망 처리 타이밍 무변경
- `TriggerAttackAnimationClientRpc` — 호출 시점 무변경

---

## 예상 결과

```
[Before]
HP -30 (숫자 감소 표시)
0.3~0.5초 후 타격 모션 → (어색함)

[After]
HP -30 (숫자 감소 표시)
타격 모션 프레임 도달 → scale punch (0.85배 → 원복)
                       → HP바 색상 플래시 (추후)
→ 공격 모션과 반응이 시각적으로 일치
```

---

## 작업 파일 목록

| 작업 | 파일 |
|------|------|
| 신규 생성 | `Assets/_Project/Scripts/Presentation/Unit/AnimationEventRelay.cs` |
| 수정 | `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` |
| 프리팹 수정 | `Unit_Pistoleer_Blue/Red`, `Unit_Assault_Blue/Red`, `Unit_Sniper_Blue/Red` (.prefab) |
| 클립 수정 | `Pistoleer_Attack.anim`, `Assault_Attack.anim`, `Sniper_Attack.anim` |

---

## 구현 순서

1. `AnimationEventRelay.cs` 생성
2. `UnitView.cs` — `OnAttackHit()` + `HitReactionCoroutine()` 추가, `TriggerAttackAnimation` 타겟 저장
3. 프리팹 6개 — Animator 자식에 AnimationEventRelay 부착
4. .anim 클립 3개 — 타격 프레임에 Animation Event 추가
5. Unity Play → 시각 반응 확인 및 프레임 미세 조정
