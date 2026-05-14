# Plan: 공격 방향 실제 Transform 기반으로 복구 (2026-03-07)

## 목표
`CalculateAttackAngle`이 타겟의 실제 `transform.position`을 사용하도록 복구.

## 변경 범위

### 1. UnitCombatUseCase.cs — 반환 타입 변경
`TryAttack()` 반환 타입: `HexCoord?` → `int?` (targetId)
- 공격 성공 시 `target.Id` 반환 (UnitData.Id 또는 BuildingData.Id)
- 실패/쿨다운 시 null

```csharp
public int? TryAttack(UnitData attacker)
{
    ...
    ExecuteAttack(attacker, target);
    attacker.AttackCooldownRemaining = attacker.AttackCooldown;
    return target.Id;
}
```

### 2. NetworkCombatController.cs — RPC 시그니처 변경
`TriggerAttackAnimationClientRpc(unitId, targetQ, targetR)` → `TriggerAttackAnimationClientRpc(unitId, targetId, targetIsUnit)`

- 클라이언트가 targetId + targetIsUnit으로 적절한 Factory에서 GameObject 조회
- TickCombat: `int? targetId = combat.TryAttack(unit)` → HasValue 체크

```csharp
[ClientRpc]
private void TriggerAttackAnimationClientRpc(int unitId, int targetId, bool targetIsUnit)
{
    ...
    unitView.TriggerAttackAnimation(targetId, targetIsUnit);
}
```

TickCombat에서도 target이 Unit인지 Building인지 알아야 함.
→ `TryAttack()` 반환을 `(int id, bool isUnit)?` 튜플로 변경하는 것이 더 명확.

**최종 결정: `(int id, bool isUnit)?` 튜플 반환**
```csharp
public (int id, bool isUnit)? TryAttack(UnitData attacker)
{
    ...
    bool isUnit = target is UnitData;
    return (target.Id, isUnit);
}
```

### 3. UnitView.cs — Transform 기반으로 변경
**SetDependencies에 UnitFactory 추가:**
```csharp
public void SetDependencies(GameConfig config,
    UnitMovementUseCase movementUseCase, UnitCombatUseCase combatUseCase,
    UnitFactory unitFactory, BuildingFactory buildingFactory)
```

**CalculateAttackAngle 파라미터 변경:**
```csharp
private float CalculateAttackAngle(Vector3 targetWorldPos)
{
    Vector3 dir = targetWorldPos - transform.position;
    dir.y = 0f;
    if (dir.sqrMagnitude < 0.001f)
        return transform.eulerAngles.y;
    return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - _meshYOffset;
}
```

**TriggerAttackAnimation 변경:**
```csharp
public void TriggerAttackAnimation(int targetId, bool targetIsUnit)
{
    if (_unitData == null || !_unitData.IsAlive) return;

    Vector3 targetWorldPos = GetTargetWorldPos(targetId, targetIsUnit);

    if (_attackCoroutine != null)
        StopCoroutine(_attackCoroutine);

    float angle = CalculateAttackAngle(targetWorldPos);
    _attackCoroutine = StartCoroutine(PlayAttackAnimation(angle));
}

private Vector3 GetTargetWorldPos(int targetId, bool targetIsUnit)
{
    GameObject obj = targetIsUnit
        ? _unitFactory?.GetUnitObject(targetId)
        : _buildingFactory?.GetBuildingObject(targetId);

    if (obj != null) return obj.transform.position;

    // fallback: 이미 파괴된 경우 현재 바라보는 방향 유지
    return transform.position + transform.forward;
}
```

**싱글플레이 OnEntityAttacked 구독 변경:**
```csharp
GameEvents.OnEntityAttacked
    .Subscribe(e =>
    {
        if (_unitData != null && e.Attacker == (IDamageable)_unitData)
        {
            bool isUnit = e.Target is UnitData;
            TriggerAttackAnimation(e.Target.Id, isUnit);
        }
    })
    .AddTo(this);
```

**싱글플레이 MoveAlongPath 변경:**
```csharp
// HasEnemyInRange로 이동 차단 후 TryAttack 결과 처리
var result = _combatUseCase.TryAttack(_unitData);
// (TriggerAttackAnimation은 OnEntityAttacked 이벤트로 자동 호출)
```

### 4. GameBootstrapper.cs — SetDependencies 호출 수정
UnitFactory, BuildingFactory를 UnitView에 추가 주입.

### 5. BuildingFactory.cs — GetBuildingObject 확인
이미 존재하는지 확인. 없으면 UnitFactory의 GetUnitObject 패턴과 동일하게 추가.

## 영향 범위 요약
| 파일 | 변경 내용 |
|---|---|
| UnitCombatUseCase.cs | TryAttack 반환 `(int id, bool isUnit)?` 튜플 |
| NetworkCombatController.cs | RPC 시그니처 + TickCombat 튜플 처리 |
| UnitView.cs | SetDependencies + TriggerAttackAnimation + CalculateAttackAngle |
| GameBootstrapper.cs | SetDependencies 호출부 수정 |
| BuildingFactory.cs | GetBuildingObject 존재 확인 (없으면 추가) |
