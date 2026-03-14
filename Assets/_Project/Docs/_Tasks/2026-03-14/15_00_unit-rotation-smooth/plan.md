# Plan: 유닛 회전 부드럽게 (DOTween)

**날짜:** 2026-03-14
**작업자:** Claude

---

## 목표

유닛이 이동 방향을 바꿀 때, 그리고 공격 대상을 향해 회전할 때 모두 DOTween 보간을 적용한다.
이동 회전과 공격 회전 모두 `_rotationDuration`을 공유하며 부드럽게 처리한다.

---

## 변경 파일

`Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` 단 1개

---

## 구체적 변경 내용

### 1. using 추가
```csharp
using DG.Tweening;
```

### 2. 필드 추가
```csharp
[SerializeField] private float _rotationDuration = 0.15f;
```
Inspector에서 조정 가능하도록 노출.

### 3. `ApplyDirection()` 수정

**변경 전:**
```csharp
private void ApplyDirection(int index)
{
    if (index < 0 || index >= DirectionAngles.Length) return;
    transform.rotation = Quaternion.Euler(0f, DirectionAngles[index], 0f);
}
```

**변경 후:**
```csharp
private void ApplyDirection(int index)
{
    if (index < 0 || index >= DirectionAngles.Length) return;
    transform.DOKill();
    transform.DORotate(new Vector3(0f, DirectionAngles[index], 0f), _rotationDuration)
             .SetEase(Ease.OutQuad);
}
```
- `DOKill()`: 진행 중인 회전 tween 취소 (빠른 방향 전환 시 누적 방지)
- `SetEase(Ease.OutQuad)`: 감속 곡선으로 자연스러운 느낌

### 4. `PlayAttackAnimation()` 수정

**변경 전:**
```csharp
transform.DOKill();
transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
```

**변경 후:**
```csharp
transform.DOKill();
transform.DORotate(new Vector3(0f, yAngle, 0f), _rotationDuration)
         .SetEase(Ease.OutQuad);
```
- 즉시 스냅 → DOTween 보간으로 교체
- `DOKill()` 유지: 이동 tween을 취소 후 공격 회전 tween 시작
- 이동과 동일한 `_rotationDuration` / `Ease.OutQuad` 사용

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 빠른 이동 스텝 시 회전이 뒤처짐 | `_rotationDuration`을 짧게 설정 (0.1~0.2s), 테스트 후 조정 |
| 공격 도중 이동 회전이 각도 덮어씀 | `PlayAttackAnimation()`에 `DOKill()` 추가로 방지 |
| OnDestroy에서 tween 누수 | DOTween은 대상 오브젝트 파괴 시 자동 정리됨 (별도 처리 불필요) |

---

## 테스트 체크리스트

- [ ] 유닛 이동 시 회전이 부드럽게 보간됨
- [ ] 빠른 방향 전환 시 회전이 자연스럽게 갱신됨 (이전 tween 취소 후 새 방향으로)
- [ ] 공격 시 타겟 방향으로 부드럽게 보간 회전됨
- [ ] 공격 중 이동 회전 tween이 공격 각도를 덮어쓰지 않음
- [ ] Blue/Red 팀 모두, Pistoleer/Assault/Sniper 모두 동일하게 동작
- [ ] `_rotationDuration` Inspector 값 조정이 런타임에 반영됨
