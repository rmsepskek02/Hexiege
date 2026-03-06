# Plan: Attack Cooldown & Animation System (2026-03-06)

## 목표
- 유닛별 공격 쿨다운(AttackCooldown) 시스템 도입
- 공격 쿨다운 = Attack 애니메이션 클립 길이 (자동 설정)
- 이동 중 공격 차단: HasEnemyInRange() 기반
- IsWalking 파라미터로 이동/정지 애니메이션 전환

## 구현 항목 (순서대로)

### 1. UnitStats.cs — GetAttackCooldown() 추가
```csharp
public static float GetAttackCooldown(UnitType type) => type switch
{
    UnitType.Pistoleer => 1.0f,
    _ => 1.0f
};
```

### 2. UnitData.cs — AttackCooldown, AttackCooldownRemaining 추가
```csharp
public float AttackCooldown { get; set; }          // 공격 1회 쿨다운(초)
public float AttackCooldownRemaining { get; set; } // 남은 쿨다운. 0이면 공격 가능
```
- 생성자에서 AttackCooldown = UnitStats.GetAttackCooldown(type), AttackCooldownRemaining = 0f 초기화

### 3. UnitSpawnUseCase.cs — 변경 없음
- UnitData 생성자에서 이미 UnitStats.GetAttackCooldown을 호출하므로 추가 변경 불필요

### 4. UnitCombatUseCase.cs — 쿨다운 체크 + HasEnemyInRange() 추가
```csharp
// TryAttack: 쿨다운 체크 추가
public HexCoord? TryAttack(UnitData attacker)
{
    if (attacker == null || !attacker.IsAlive) return null;
    if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return null;
    if (attacker.AttackCooldownRemaining > 0f) return null; // 쿨다운 중
    IDamageable target = FindFirstEnemyTarget(attacker);
    if (target == null) return null;
    ExecuteAttack(attacker, target);
    attacker.AttackCooldownRemaining = attacker.AttackCooldown; // 쿨다운 리셋
    return target.Position;
}

// HasEnemyInRange: 이동 차단용 (쿨다운 무관)
public bool HasEnemyInRange(UnitData attacker)
{
    if (attacker == null || !attacker.IsAlive) return false;
    return FindFirstEnemyTarget(attacker) != null;
}
```

### 5. NetworkCombatController.cs — 쿨다운 감소 로직 추가
TickCombat()에서 각 유닛의 AttackCooldownRemaining을 _attackInterval만큼 감소:
```csharp
// _attackInterval = 0.1f 유지 (폴링 빈도)
foreach (int id in unitIds)
{
    if (!unitSpawn.Units.TryGetValue(id, out UnitData unit)) continue;
    if (!unit.IsAlive) continue;

    // 쿨다운 감소
    if (unit.AttackCooldownRemaining > 0f)
        unit.AttackCooldownRemaining -= _attackInterval;

    HexCoord? targetPos = combat.TryAttack(unit);
    if (targetPos.HasValue)
        TriggerAttackAnimationClientRpc(unit.Id, targetPos.Value.Q, targetPos.Value.R);
}
```

### 6. UnitFactory.cs — Attack 클립 길이로 AttackCooldown 설정
OnUnitSpawned 핸들러에서 Animator의 Attack 클립 길이를 읽어 unit.AttackCooldown 덮어쓰기:
```csharp
float attackClipLength = GetAttackClipLength(animator);
if (attackClipLength > 0f)
    e.Unit.AttackCooldown = attackClipLength;

private float GetAttackClipLength(Animator animator)
{
    if (animator?.runtimeAnimatorController == null) return 0f;
    foreach (var clip in animator.runtimeAnimatorController.animationClips)
        if (clip.name.Contains("Attack"))
            return clip.length;
    return 0f;
}
```

### 7. UnitView.cs — MoveAlongPath + 싱글플레이 쿨다운 감소
- MoveAlongPath: HasEnemyInRange() 기반 이동 차단, IsWalking 파라미터 적용
- Update(): 싱글플레이(NetworkContext.IsNetworkActive == false)에서 AttackCooldownRemaining 감소
- 싱글플레이 OnEntityAttacked 이벤트 구독 유지 (TriggerAttackAnimation 호출)

```csharp
// Update에 추가 (싱글플레이 전용 쿨다운 감소)
private void Update()
{
    if (!NetworkContext.IsNetworkActive && _unit != null && _unit.AttackCooldownRemaining > 0f)
        _unit.AttackCooldownRemaining -= Time.deltaTime;
}

// MoveAlongPath 이동 차단 조건 변경
if (_combatUseCase.HasEnemyInRange(_unit)) { /* 정지 */ yield break; }

// 이동 시 IsWalking=true, 멈춤/완료 시 IsWalking=false
_animator.SetBool("IsWalking", true);  // 이동 시작
_animator.SetBool("IsWalking", false); // 이동 완료/차단
```

## Inspector 작업 불필요
- 모든 변경은 코드만으로 완료 가능

## 컴파일 확인 포인트
- UnitData에 AttackCooldown/AttackCooldownRemaining 추가 시 생성자 시그니처 변경 없음 (프로퍼티이므로)
- UnitCombatUseCase.HasEnemyInRange() — UnitView에서 호출 가능한지 접근성 확인
- UnitView가 UnitCombatUseCase 참조를 갖고 있는지 확인
