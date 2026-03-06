# Plan: 공격 방향 정밀도 개선 (Atan2 기반)

**날짜:** 2026-03-06
**선행:** research.md 완료, FacingDirection.cs 2D 레거시 제거 완료
**목표:** 공격 방향을 타겟 월드 좌표 → Atan2 → Y축 회전으로 직접 계산하여 정확도 향상

---

## 변경 범위 요약

| 파일 | 변경 내용 | 난이도 |
|------|---------|------|
| `UnitCombatUseCase.cs` | `TryAttack()` 반환 타입 `bool` → `HexCoord?` | 낮음 |
| `UnitView.cs` | Atan2 헬퍼 추가, `_meshYOffset` SerializeField, 공격 방향 계산 교체 | 중간 |
| `NetworkCombatController.cs` | RPC 시그니처에 `targetQ, targetR` 추가 | 중간 |

**이동 방향(`MoveAlongPath`)은 변경 없음.** 인접 타일 이동이므로 기존 HexDirection 스냅이 정확함.

---

## 상세 변경 내용

### 1. UnitCombatUseCase.cs

**`TryAttack()` 반환 타입 변경:**
```csharp
// 변경 전
public bool TryAttack(UnitData attacker)

// 변경 후
public HexCoord? TryAttack(UnitData attacker)
// 공격 성공 → target.Position 반환
// 공격 없음 → null 반환
```

`HasEnemyInRange()`는 변경 없음.

**사용처 영향:**
- `UnitView.MoveAlongPath()`: `if (TryAttack(...))` → `if (TryAttack(...) != null)` (2군데)
- `NetworkCombatController.TickCombat()`: `bool attacked = combat.TryAttack(unit)` → `HexCoord? targetPos = combat.TryAttack(unit)` + `if (targetPos.HasValue)`

---

### 2. UnitView.cs

#### 추가: `_meshYOffset` SerializeField
```csharp
[SerializeField] private float _meshYOffset = 30f;
// Unit_Pistoleer_Mesh 자식의 localEulerAngles.y = 30°와 일치.
// 다른 유닛 프리팹별 Inspector에서 조정 가능.
// 프리팹에 이미 _meshYOffset: 30 저장되어 있어 자동 복원됨.
```

#### 추가: `_rotationSpeed` SerializeField (스무스 회전용, 선택사항)
```csharp
[SerializeField] private float _rotationSpeed = 540f;
// 프리팹에 이미 _rotationSpeed: 540 저장되어 있어 자동 복원됨.
// 공격 시 즉시 회전 or 스무스 회전 여부는 구현 시 결정.
```

#### 추가: Atan2 헬퍼 메서드
```csharp
/// <summary>
/// 타겟의 도메인 헥스 좌표 → 뷰 월드 좌표로 변환 후
/// 자신의 transform.position 기준으로 Y축 회전 각도 계산.
/// _meshYOffset으로 Meshy.ai 모델 forward 방향 오차 보정.
/// </summary>
private float CalculateAttackAngle(HexCoord targetDomainPos)
{
    Vector3 targetView = ViewConverter.ToView(HexMetrics.HexToWorld(targetDomainPos));
    Vector3 dir = targetView - transform.position;
    dir.y = 0f;
    if (dir.sqrMagnitude < 0.001f)
        return transform.eulerAngles.y; // 너무 가까우면 현재 방향 유지
    return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - _meshYOffset;
}
```

#### 변경: `TriggerAttackAnimation` 시그니처
```csharp
// 변경 전
public void TriggerAttackAnimation(HexDirection direction)

// 변경 후
public void TriggerAttackAnimation(HexCoord targetPosition)
// HexDirection 파라미터 제거 — Atan2로 직접 계산하므로 불필요
```

#### 변경: `PlayAttackAnimation` 시그니처
```csharp
// 변경 전
private IEnumerator PlayAttackAnimation(HexDirection direction)
{
    ApplyDirection(direction);
    SetAnimatorTrigger(AnimAttack);
    ...
}

// 변경 후
private IEnumerator PlayAttackAnimation(float yAngle)
{
    transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
    SetAnimatorTrigger(AnimAttack);
    ...
}
```

#### 변경: 싱글플레이 이벤트 구독 (SetDependencies 내부)
```csharp
// 변경 전
GameEvents.OnEntityAttacked
    .Subscribe(e =>
    {
        if (_unitData != null && e.Attacker == (IDamageable)_unitData)
            _attackCoroutine = StartCoroutine(PlayAttackAnimation(_unitData.Facing));
    })

// 변경 후
GameEvents.OnEntityAttacked
    .Subscribe(e =>
    {
        if (_unitData != null && e.Attacker == (IDamageable)_unitData)
        {
            float angle = CalculateAttackAngle(e.Target.Position);
            _attackCoroutine = StartCoroutine(PlayAttackAnimation(angle));
        }
    })
// e.Target은 IDamageable — Position 프로퍼티 접근 가능 여부 사전 확인 필요
```

#### 변경: `MoveAlongPath` 조건문 (TryAttack null 체크)
```csharp
// 변경 전
if (_combatUseCase.TryAttack(_unitData))

// 변경 후
if (_combatUseCase.TryAttack(_unitData) != null)
// 내부 while 조건도 동일하게 변경 (2군데)
```

---

### 3. NetworkCombatController.cs

#### 변경: `TickCombat()` — TryAttack 반환값 처리
```csharp
// 변경 전
if (combat.TryAttack(unit))
{
    TriggerAttackAnimationClientRpc(unit.Id, (int)unit.Facing);
}

// 변경 후
HexCoord? targetPos = combat.TryAttack(unit);
if (targetPos.HasValue)
{
    TriggerAttackAnimationClientRpc(unit.Id, targetPos.Value.Q, targetPos.Value.R);
}
```

#### 변경: RPC 시그니처
```csharp
// 변경 전
[ClientRpc]
private void TriggerAttackAnimationClientRpc(int unitId, int facingInt)

// 변경 후
[ClientRpc]
private void TriggerAttackAnimationClientRpc(int unitId, int targetQ, int targetR)
```

#### 변경: RPC 본문
```csharp
// 변경 전
HexDirection facing = (HexDirection)facingInt;
unitView.TriggerAttackAnimation(facing);

// 변경 후
HexCoord targetPos = new HexCoord(targetQ, targetR);
unitView.TriggerAttackAnimation(targetPos);
// UnitView 내부에서 ViewConverter.ToView() + Atan2 계산
// → Blue/Red팀 각자의 IsFlipped 설정에 따라 자동으로 올바른 방향 계산
```

---

## 방향 결정 흐름 (변경 후)

```
[싱글플레이]
TryAttack(attacker) → HexCoord? targetPos
  → ExecuteAttack(attacker, target)
      → attacker.Facing = FacingDirection.FromCoords(...) [도메인 방향, 이동용]
      → GameEvents.OnEntityAttacked(attacker, target) 발행

UnitView.OnEntityAttacked
  → CalculateAttackAngle(e.Target.Position)
      → ViewConverter.ToView(HexMetrics.HexToWorld(targetPos))
      → Atan2(dir.x, dir.z) * Rad2Deg - _meshYOffset(30°)
  → PlayAttackAnimation(angle) → transform.rotation 직접 설정


[멀티플레이]
combat.TryAttack(unit) → HexCoord? targetPos
  → targetPos.HasValue → TriggerAttackAnimationClientRpc(id, targetPos.Q, targetPos.R)
        ↓ (모든 클라이언트)
UnitView.TriggerAttackAnimation(HexCoord targetPos)
  → CalculateAttackAngle(targetPos)
      → ViewConverter.ToView(HexMetrics.HexToWorld(targetPos))
         [IsFlipped=true이면 자동 반전 → Red팀 올바른 방향]
      → Atan2 → PlayAttackAnimation(angle)
```

**핵심:** 도메인 좌표(Blue 기준)를 RPC로 전달 → 각 클라이언트가 `ViewConverter.ToView()`로 자신의 관점 변환 → Atan2. Red/Blue 모두 자동 처리.

---

## 전제 조건 확인 사항

구현 전 반드시 확인:
1. `EntityAttackedEvent`에 `Target` (IDamageable) 프로퍼티가 있는가?
   - 있으면 `e.Target.Position`으로 바로 접근 가능
   - 없으면 IDamageable에 `HexCoord Position { get; }` 추가 또는 UnitData/BuildingData로 캐스팅
2. `IDamageable` 인터페이스에 `HexCoord Position { get; }` 있는가?
3. `HexMetrics.HexToWorld()` 시그니처 확인 (매개변수 타입)
4. `ViewConverter.ToView()` 시그니처 확인

---

## 검증 시나리오

- [ ] 싱글플레이: 유닛이 인접 적 유닛 공격 시 정확히 타겟 방향을 바라봄
- [ ] 싱글플레이: 유닛이 인접 적 건물 공격 시 정확히 타겟 방향을 바라봄
- [ ] 싱글플레이: 이동 방향 변경 없음 (기존 DirectionAngles 그대로)
- [ ] 멀티플레이: Blue팀(Host) 공격 방향 정확
- [ ] 멀티플레이: Red팀(Client) 공격 방향 정확 (ViewConverter.ToView 반전 확인)
- [ ] 멀티플레이: 서버 RPC 전달 후 클라이언트 애니메이션 정상 재생

---

## 리스크

1. **`_meshYOffset = 30°`**: 프리팹에 이미 저장. 다른 유닛 추가 시 각 프리팹 Inspector에서 설정 필요.
2. **RPC 시그니처 변경**: 네트워크 호환성 — 개발 단계이므로 문제없음.
3. **`EntityAttackedEvent.Target` 접근**: 이벤트 구조 확인 필요 (구현 시 확인).
