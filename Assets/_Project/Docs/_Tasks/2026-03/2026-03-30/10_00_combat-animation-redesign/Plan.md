# Plan: 멀티플레이 전투/애니메이션 구조 재설계

## 1. 구현 목표

- **매 공격 사이클마다 RPC 반복 전송** 패턴을 **전투 상태 변화 시에만 RPC 전송** 패턴으로 교체
- 서버가 유닛별 전투 상태(현재 타겟)를 Dictionary로 추적, 상태 변화 시에만 3종 RPC 전송
- 클라이언트 UnitView에서 `_attackCoroutine` / `_isWalkPending` 기반 단발 공격 관리를 제거하고, Attack 루프(Loop Time=ON) 기반으로 단순화
- 싱글플레이에서도 동일한 "전투 시작/타겟 변경/전투 종료" 이벤트 패턴 적용 (Application 레이어에서 이벤트 발행)

---

## 2. 설계 결정사항

### 2-1. 서버 전투 상태 추적

서버 `NetworkCombatController`에 `Dictionary<int, (int targetId, bool isUnit)> _unitCombatTargets`를 추가하여 유닛별 현재 전투 타겟을 추적한다.

TickCombat에서 매 사이클:
1. `TryFindTarget()`으로 타겟 탐색
2. Dictionary 이전 상태와 비교하여 상태 변화 시에만 RPC 전송:
   - 없었는데 생김 -> `StartCombatClientRpc` + Dictionary 추가
   - 있었는데 같은 타겟 -> RPC 없음 (데미지 코루틴만 실행)
   - 있었는데 다른 타겟 -> `ChangeTargetClientRpc` + Dictionary 갱신
   - 있었는데 사라짐 -> `StopCombatClientRpc` + Dictionary 제거

> **버그 수정 (2026-04-03)**: 초기 구현에서 "없었는데 생김" 케이스에 `StartCombatClientRpc`를 제거하고 `OnUnitEnteredCombatHandler`에만 의존했으나, Unity 실행 순서(TickCombat=Update > 코루틴) 때문에 TickCombat이 먼저 dict에 등록 → `OnUnitEnteredCombatHandler`가 `ContainsKey=true`로 return → RPC 미전송 버그 발생. TickCombat else 브랜치에 `StartCombatClientRpc` 복원으로 해결. 두 핸들러 중 먼저 실행된 쪽이 dict에 등록하므로 중복 전송 없음.

### 2-2. 클라이언트 애니메이션 관리

- `StartCombatAnimation()`: Attack CrossFade 1회 호출. Attack 클립은 Loop Time=ON이므로 별도 루프 로직 불필요.
- `ChangeTarget()`: 타겟 방향 회전만 업데이트. 애니메이션 상태 변경 없음.
- `StopCombatAnimation()`: Walk 또는 Idle로 CrossFade. 이동 중이었으면 Walk, 아니면 Idle.

### 2-3. 싱글플레이 일관성

- `UnitCombatUseCase`에 전투 상태 추적 Dictionary 추가 (`Dictionary<int, (int targetId, bool isUnit)> _combatTargets`)
- `TryAttack()` 호출 시 상태 변화를 감지하여 GameEvents로 이벤트 발행
- UnitView가 싱글플레이에서 이 이벤트를 구독하여 동일한 메서드 호출
- Clean Architecture 준수: Application(UnitCombatUseCase) -> Presentation(UnitView) 방향

### 2-4. MoveAlongPath 전투 루프 단순화

- `_attackCoroutine != null` 대기 패턴 완전 제거
- `HasEnemyInRange` 체크만으로 전투 대기 (단순 yield return null 루프)
- 전투 애니메이션 타이밍은 UnitView가 RPC/이벤트를 통해 독립적으로 관리
- **적이 사거리 밖으로 나가면 현재 공격 애니메이션 사이클 완료 후 이동 재개** (2026-03-30 추가):
  - `HasEnemyInRange`가 false가 된 직후 `WaitForAttackCycleEnd()` 코루틴으로 현재 Attack 사이클 종료 대기
  - `AnimatorStateInfo.normalizedTime % 1.0f`의 소수부를 매 프레임 비교하여 새 루프 진입 시점(소수부가 이전보다 작아지는 순간)을 감지
  - 대기 중 적이 다시 사거리에 들어오면 전투 루프에 재진입 (continue)
  - 멀티플레이/싱글플레이 양쪽 동일하게 적용
  - **버그 수정**: 기존에는 적이 사거리 밖으로 나가면 공격 모션이 중간에 끊기고 즉시 Walk로 전환되어 부자연스러운 애니메이션 발생

### 2-5. StopCombat 발행 주체

- **멀티플레이**: MoveAlongPath에서 전투 루프 탈출 시 `OnUnitWalkStarted` 이벤트 발행 (기존 유지). NetworkCombatController의 TickCombat에서 `_unitCombatTargets` Dictionary에서 해당 유닛의 타겟이 사라진 것을 감지하여 `StopCombatClientRpc` 전송.
- **싱글플레이**: `TryAttack()` 내부에서 타겟이 사라진 것을 감지하여 `OnCombatStopped` 이벤트 발행.
- **중요**: MoveAlongPath에서 전투 루프 탈출 시에는 Walk 이벤트만 발행. StopCombat은 전투 상태 관리자(서버 TickCombat / 싱글 TryAttack)가 담당.

---

## 3. 파일별 변경 명세

### 3-1. GameEvents.cs (Application)

**삭제:**

```csharp
// L418~423 삭제 — OnUnitEnteredCombat Subject 전체
/// <summary>
/// 멀티플레이 서버에서 유닛이 이동 중 처음으로 사거리 내 적을 감지했을 때 발행. 유닛 Id를 전달.
/// MoveAlongPath에서 HasEnemyInRange가 true가 되는 첫 순간에만 발행.
/// NetworkCombatController가 구독하여 TickCombat 인터벌을 기다리지 않고 즉시 Attack RPC 전송.
/// </summary>
public static readonly Subject<int> OnUnitEnteredCombat = new Subject<int>();
```

**신규 추가 (전투 상태 변화 이벤트 3종 — 싱글플레이 전용):**

```csharp
// ====================================================================
// 전투 상태 변화 이벤트 (싱글플레이 전용)
// 멀티플레이에서는 NetworkCombatController가 ClientRpc로 직접 UnitView 메서드 호출.
// ====================================================================

/// <summary>
/// 유닛이 전투 상태에 진입했을 때 발행.
/// 발행: UnitCombatUseCase (싱글플레이에서 TryAttack 성공 시, 이전에 전투 중이 아니었던 경우)
/// 구독: UnitView (StartCombatAnimation 호출)
/// </summary>
public static readonly Subject<CombatStartedEvent> OnCombatStarted = new Subject<CombatStartedEvent>();

/// <summary>
/// 전투 중 타겟이 변경되었을 때 발행.
/// 발행: UnitCombatUseCase (싱글플레이에서 TryAttack 성공 시, 이전 타겟과 다른 경우)
/// 구독: UnitView (ChangeTarget 호출 — 회전만 업데이트)
/// </summary>
public static readonly Subject<CombatTargetChangedEvent> OnCombatTargetChanged = new Subject<CombatTargetChangedEvent>();

/// <summary>
/// 유닛이 전투 상태에서 벗어났을 때 발행.
/// 발행: UnitCombatUseCase (싱글플레이에서 사거리 내 적이 없어진 경우)
/// 구독: UnitView (StopCombatAnimation 호출)
/// </summary>
public static readonly Subject<int> OnCombatStopped = new Subject<int>();
```

**신규 이벤트 구조체 추가:**

```csharp
/// <summary>
/// 전투 시작 이벤트 데이터 (싱글플레이 전용).
/// </summary>
public struct CombatStartedEvent
{
    /// <summary> 공격하는 유닛의 Id. </summary>
    public readonly int UnitId;
    /// <summary> 타겟 엔티티의 Id. </summary>
    public readonly int TargetId;
    /// <summary> true=유닛, false=건물. </summary>
    public readonly bool TargetIsUnit;

    public CombatStartedEvent(int unitId, int targetId, bool targetIsUnit)
    {
        UnitId = unitId;
        TargetId = targetId;
        TargetIsUnit = targetIsUnit;
    }
}

/// <summary>
/// 전투 타겟 변경 이벤트 데이터 (싱글플레이 전용).
/// </summary>
public struct CombatTargetChangedEvent
{
    /// <summary> 공격하는 유닛의 Id. </summary>
    public readonly int UnitId;
    /// <summary> 새 타겟 엔티티의 Id. </summary>
    public readonly int NewTargetId;
    /// <summary> true=유닛, false=건물. </summary>
    public readonly bool NewTargetIsUnit;

    public CombatTargetChangedEvent(int unitId, int newTargetId, bool newTargetIsUnit)
    {
        UnitId = unitId;
        NewTargetId = newTargetId;
        NewTargetIsUnit = newTargetIsUnit;
    }
}
```

---

### 3-2. UnitCombatUseCase.cs (Application)

**신규 필드 추가:**

```csharp
/// <summary>
/// 싱글플레이 전용 전투 상태 추적.
/// key=유닛Id, value=(타겟Id, 타겟이 유닛인지).
/// 전투 중이 아닌 유닛은 Dictionary에 없음.
/// 멀티플레이에서는 NetworkCombatController._unitCombatTargets가 이 역할을 수행.
/// </summary>
private readonly Dictionary<int, (int targetId, bool isUnit)> _combatTargets
    = new Dictionary<int, (int targetId, bool isUnit)>();
```

**TryAttack() 수정 (L53~75):**

기존 TryAttack 내부에서 상태 변화 감지 + 이벤트 발행 로직을 추가한다.

```csharp
public (int id, bool isUnit)? TryAttack(UnitData attacker)
{
    if (attacker == null || !attacker.IsAlive) return null;

    // 멀티플레이 모드에서는 서버만 전투 처리.
    if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return null;

    // 쿨다운 중이면 공격 불가
    if (attacker.AttackCooldownRemaining > 0f) return null;

    // 타겟 탐색
    IDamageable target = FindFirstEnemyTarget(attacker);
    if (target == null) return null;

    int targetId = target.Id;
    bool targetIsUnit = target is UnitData;

    // --- 싱글플레이 전투 상태 변화 감지 및 이벤트 발행 ---
    if (!NetworkContext.IsNetworkActive)
    {
        if (_combatTargets.TryGetValue(attacker.Id, out var prev))
        {
            // 이전에 전투 중이었고 타겟이 다르면 → ChangeTarget 이벤트
            if (prev.targetId != targetId)
            {
                _combatTargets[attacker.Id] = (targetId, targetIsUnit);
                GameEvents.OnCombatTargetChanged.OnNext(
                    new CombatTargetChangedEvent(attacker.Id, targetId, targetIsUnit));
            }
            // 같은 타겟이면 이벤트 없음
        }
        else
        {
            // 이전에 전투 중이 아니었으면 → StartCombat 이벤트
            _combatTargets[attacker.Id] = (targetId, targetIsUnit);
            GameEvents.OnCombatStarted.OnNext(
                new CombatStartedEvent(attacker.Id, targetId, targetIsUnit));
        }
    }

    ExecuteAttack(attacker, target);

    // 공격 성공 -> 쿨다운 시작
    attacker.AttackCooldownRemaining = attacker.AttackCooldown;

    return (targetId, targetIsUnit);
}
```

**신규 메서드 — ClearCombatState(int unitId):**

싱글플레이에서 전투 종료 시 Dictionary 정리 + 이벤트 발행용.
MoveAlongPath 전투 루프 탈출 시 UnitView에서 호출.

```csharp
/// <summary>
/// 싱글플레이 전용: 유닛의 전투 상태를 해제하고 OnCombatStopped 이벤트를 발행.
/// MoveAlongPath에서 HasEnemyInRange가 false가 되어 전투 루프를 탈출할 때 호출.
/// 멀티플레이에서는 NetworkCombatController가 TickCombat에서 StopCombatClientRpc를 전송하므로 불필요.
/// </summary>
/// <param name="unitId">전투를 종료하는 유닛의 Id</param>
public void ClearCombatState(int unitId)
{
    if (_combatTargets.Remove(unitId))
    {
        GameEvents.OnCombatStopped.OnNext(unitId);
    }
}
```

**유닛 사망 시 Dictionary 정리 — ExecuteAttack() 수정 (L351~380):**

기존 `ExecuteAttack()` 내부의 사망 처리 블록에 `_combatTargets` 정리 추가.

```csharp
// 타겟 사망 처리
if (!target.IsAlive)
{
    // 사망 이벤트 발행 -> UnitView/BuildingView가 GameObject 파괴
    GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(target));

    // 싱글플레이: 사망한 유닛의 전투 상태 정리
    if (!NetworkContext.IsNetworkActive && target is UnitData deadUnit)
    {
        _combatTargets.Remove(deadUnit.Id);
    }

    // 싱글플레이: 사망한 타겟을 공격 중이던 유닛들의 전투 상태도 정리하지 않음.
    // → 다음 TryAttack() 호출 시 새 타겟을 탐색하거나,
    //   HasEnemyInRange가 false가 되어 MoveAlongPath에서 ClearCombatState() 호출.
    // 이렇게 하면 사망 직후 즉시 StopCombat 이벤트가 발행되지 않고,
    // 자연스러운 타이밍(다음 프레임)에 전투 종료/새 타겟 전환이 발생.

    // 데이터 정리 -- Dictionary에서 제거
    if (target is UnitData u)
        _unitSpawn.RemoveUnit(u.Id);
    else if (target is BuildingData b)
        _buildingPlacement.RemoveBuilding(b.Id);
}
```

---

### 3-3. NetworkCombatController.cs (Infrastructure)

**신규 필드 추가:**

```csharp
/// <summary>
/// 유닛별 현재 전투 타겟 추적. key=유닛Id, value=(타겟Id, 타겟이 유닛인지).
/// 전투 중이 아닌 유닛은 Dictionary에 없음.
/// TickCombat에서 이전 프레임 상태와 비교하여 상태 변화 시에만 RPC 전송.
/// </summary>
private readonly System.Collections.Generic.Dictionary<int, (int targetId, bool isUnit)> _unitCombatTargets
    = new System.Collections.Generic.Dictionary<int, (int targetId, bool isUnit)>();
```

**삭제:**

1. `_enteredCombatSubscription` 필드 (L69) 및 관련 구독/해제 코드
   - `OnNetworkSpawn()` L115~116 삭제:
     ```csharp
     _enteredCombatSubscription = GameEvents.OnUnitEnteredCombat
         .Subscribe(OnUnitEnteredCombat);
     ```
   - `OnNetworkDespawn()` L136~137 삭제:
     ```csharp
     _enteredCombatSubscription?.Dispose();
     _enteredCombatSubscription = null;
     ```

2. `OnUnitEnteredCombat(int unitId)` 메서드 전체 삭제 (L273~297)

3. `TriggerAttackAnimationClientRpc(int unitId, int targetId, bool targetIsUnit)` 메서드 전체 삭제 (L389~413)

4. `ExecuteAttack()` 메서드에서 `TriggerAttackAnimationClientRpc` 호출 제거 (L258). ExecuteAttack은 데미지 코루틴 전용으로 변경.

**ExecuteAttack() 수정 (L255~265):**

애니메이션 RPC 전송을 제거하고 데미지 코루틴 + 쿨다운 리셋만 수행.

```csharp
/// <summary>
/// 타겟을 향한 데미지를 실행. 쿨다운 리셋 + 데미지 코루틴 시작.
/// 애니메이션 RPC는 TickCombat의 상태 변화 감지 로직에서 별도로 전송.
/// </summary>
/// <param name="unit">공격하는 유닛의 데이터</param>
/// <param name="targetId">타겟 엔티티의 Id</param>
/// <param name="targetIsUnit">true=유닛, false=건물</param>
private void ExecuteAttack(UnitData unit, int targetId, bool targetIsUnit)
{
    // 1. 쿨다운 즉시 리셋 -- TryFindTarget()은 쿨다운을 건드리지 않으므로 여기서 처리
    unit.AttackCooldownRemaining = unit.AttackCooldown;

    // 2. hitFrameTime 후 데미지 적용 -- 타격 프레임과 데미지 타이밍 동기화
    StartCoroutine(DelayedAttackDamage(unit, targetId, targetIsUnit, unit.HitFrameTime));
}
```

**TickCombat() 전면 재작성 (L180~212):**

타겟 변화 감지 로직을 추가하고, 상태 변화 시에만 RPC를 전송한다.

```csharp
private void TickCombat()
{
    if (_bootstrapper == null)
        _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

    if (_bootstrapper == null) return;

    UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
    UnitCombatUseCase combat = _bootstrapper.GetCombatUseCase();

    if (unitSpawn == null || combat == null) return;

    // Dictionary를 순회하면서 타겟 탐색
    // 전투 중 RemoveUnit이 호출될 수 있으므로 키를 미리 복사
    var unitIds = new System.Collections.Generic.List<int>(unitSpawn.Units.Keys);

    // 이번 Tick에서 전투 중인 유닛 Id를 수집 (Tick 후 Dictionary 정리용)
    // HashSet을 사용하여 O(1) Contains 체크
    var activeThisTick = new System.Collections.Generic.HashSet<int>();

    foreach (int id in unitIds)
    {
        // 순회 도중 사망 처리로 제거되었을 수 있으므로 재확인
        if (!unitSpawn.Units.TryGetValue(id, out UnitData unit)) continue;
        if (!unit.IsAlive) continue;

        // 쿨다운 감소 (서버 Tick 간격만큼)
        if (unit.AttackCooldownRemaining > 0f)
            unit.AttackCooldownRemaining -= _attackInterval;

        // 타겟 탐색만 수행 (데미지 없음, 쿨다운 리셋 없음)
        var attackResult = combat.TryFindTarget(unit);

        if (attackResult.HasValue)
        {
            int targetId = attackResult.Value.id;
            bool isUnit = attackResult.Value.isUnit;
            activeThisTick.Add(id);

            // --- 상태 변화 감지 ---
            if (_unitCombatTargets.TryGetValue(id, out var prev))
            {
                // 이전에 전투 중이었음
                if (prev.targetId != targetId)
                {
                    // 타겟 변경 -> ChangeTarget RPC
                    _unitCombatTargets[id] = (targetId, isUnit);
                    ChangeTargetClientRpc(id, targetId, isUnit);
                }
                // else: 같은 타겟 -> RPC 없음
            }
            else
            {
                // 새로 전투 진입 -> StartCombat RPC
                _unitCombatTargets[id] = (targetId, isUnit);
                StartCombatClientRpc(id, targetId, isUnit);
            }

            // 데미지 코루틴 실행 (상태 변화 여부와 무관하게 매 사이클 실행)
            ExecuteAttack(unit, targetId, isUnit);
        }
    }

    // 이전 Tick에서 전투 중이었지만 이번 Tick에서 타겟을 찾지 못한 유닛 -> StopCombat RPC
    // Dictionary 순회 중 수정 방지를 위해 제거할 키를 미리 수집
    var toRemove = new System.Collections.Generic.List<int>();
    foreach (var kvp in _unitCombatTargets)
    {
        if (!activeThisTick.Contains(kvp.Key))
        {
            toRemove.Add(kvp.Key);
        }
    }
    foreach (int id in toRemove)
    {
        _unitCombatTargets.Remove(id);
        StopCombatClientRpc(id);
    }
}
```

**유닛 사망 시 Dictionary 정리:**

기존 `OnEntityDied` 핸들러(`OnEntityDied()` L308~337) 내부에서 `_unitCombatTargets` 정리 추가.

```csharp
private void OnEntityDied(EntityDiedEvent e)
{
    if (!IsServer) return;
    if (e.Entity == null) return;

    int entityId;
    bool isUnit;

    if (e.Entity is UnitData unit)
    {
        entityId = unit.Id;
        isUnit = true;

        // 사망한 유닛의 전투 상태를 Dictionary에서 제거.
        // StopCombatClientRpc는 전송하지 않음 — EntityDiedClientRpc가 사망 처리를 담당.
        _unitCombatTargets.Remove(entityId);
    }
    else if (e.Entity is BuildingData building)
    {
        entityId = building.Id;
        isUnit = false;
    }
    else
    {
        Debug.LogWarning("[Network] NetworkCombatController.OnEntityDied: 알 수 없는 엔티티 타입.");
        return;
    }

    // 사망한 타겟을 공격 중이던 유닛들의 전투 상태는 제거하지 않음.
    // -> 다음 TickCombat에서 TryFindTarget이 다른 타겟을 찾거나 null을 반환.
    //    ChangeTargetClientRpc 또는 StopCombatClientRpc가 자연스럽게 발행됨.

    Debug.Log($"[Network] 서버: 엔티티 사망. Id={entityId}, IsUnit={isUnit}");
    EntityDiedClientRpc(entityId, isUnit);
}
```

**신규 RPC 3종 추가:**

```csharp
// ====================================================================
// 전투 상태 ClientRpc -- 서버 -> 모든 클라이언트
// ====================================================================

/// <summary>
/// 유닛이 전투 상태에 진입. 클라이언트에서 Attack 루프 시작 + 타겟 방향 회전.
/// TickCombat에서 유닛이 처음 타겟을 발견했을 때 전송.
/// 서버(Host)도 수신하여 동일한 애니메이션 처리.
/// </summary>
/// <param name="unitId">공격하는 유닛의 Id</param>
/// <param name="targetId">타겟 엔티티의 Id</param>
/// <param name="targetIsUnit">true=유닛, false=건물</param>
[ClientRpc]
private void StartCombatClientRpc(int unitId, int targetId, bool targetIsUnit)
{
    var unitFactory = _bootstrapper?.GetUnitFactory();
    if (unitFactory == null) return;

    GameObject unitObj = unitFactory.GetUnitObject(unitId);
    if (unitObj == null) return;

    UnitView unitView = unitObj.GetComponent<UnitView>();
    if (unitView == null) return;

    // Walk 정지 후 Attack 루프 시작
    unitView.StopWalkAnimation();
    unitView.StartCombatAnimation(targetId, targetIsUnit);
}

/// <summary>
/// 전투 중 타겟 변경. 클라이언트에서 회전만 업데이트, 애니메이션 재시작 없음.
/// TickCombat에서 유닛의 타겟이 이전과 다를 때 전송.
/// </summary>
/// <param name="unitId">공격하는 유닛의 Id</param>
/// <param name="newTargetId">새 타겟 엔티티의 Id</param>
/// <param name="newTargetIsUnit">true=유닛, false=건물</param>
[ClientRpc]
private void ChangeTargetClientRpc(int unitId, int newTargetId, bool newTargetIsUnit)
{
    var unitFactory = _bootstrapper?.GetUnitFactory();
    if (unitFactory == null) return;

    GameObject unitObj = unitFactory.GetUnitObject(unitId);
    if (unitObj == null) return;

    UnitView unitView = unitObj.GetComponent<UnitView>();
    if (unitView == null) return;

    // 회전만 업데이트 -- 애니메이션 상태 변경 없음
    unitView.ChangeTarget(newTargetId, newTargetIsUnit);
}

/// <summary>
/// 유닛이 전투 상태 종료. 클라이언트에서 애니메이션 전환 없음.
/// 이동 재개 시 StartWalkAnimationClientRpc가 Walk CrossFade를 처리.
/// Idle CrossFade를 여기서 호출하지 않는 이유:
///   Walk RPC보다 늦게 도착하면 Walk를 Idle로 덮어씌우는 버그 발생.
///   이 게임에서 유닛은 항상 이동 또는 공격 중이므로 Idle 상태 불필요.
/// </summary>
/// <param name="unitId">전투를 종료하는 유닛의 Id</param>
[ClientRpc]
private void StopCombatClientRpc(int unitId)
{
    var unitFactory = _bootstrapper?.GetUnitFactory();
    if (unitFactory == null) return;

    GameObject unitObj = unitFactory.GetUnitObject(unitId);
    if (unitObj == null) return;

    UnitView unitView = unitObj.GetComponent<UnitView>();
    if (unitView == null) return;

    unitView.StopCombatAnimation();
}
```

**OnNetworkDespawn에 Dictionary 정리 추가:**

```csharp
public override void OnNetworkDespawn()
{
    base.OnNetworkDespawn();
    _diedSubscription?.Dispose();
    _diedSubscription = null;

    _walkStartedSubscription?.Dispose();
    _walkStartedSubscription = null;
    _walkStoppedSubscription?.Dispose();
    _walkStoppedSubscription = null;

    // _enteredCombatSubscription 삭제됨 (OnUnitEnteredCombat 이벤트 제거)

    // 전투 상태 Dictionary 초기화
    _unitCombatTargets.Clear();

    NetworkContext.Reset();
}
```

---

### 3-4. UnitView.cs (Presentation)

**삭제:**

1. `_attackCoroutine` 필드 (L127):
   ```csharp
   /// <summary> 현재 공격 코루틴. </summary>
   private Coroutine _attackCoroutine;
   ```

2. `_isWalkPending` 필드 (L129~130):
   ```csharp
   /// <summary> 공격 코루틴 실행 중 Walk 명령이 도착하면 true. 코루틴 종료 시 Walk 자동 전환. </summary>
   private bool _isWalkPending;
   ```

3. `TriggerAttackAnimation()` 메서드 전체 (L631~651)

4. `PlayAttackAnimation()` 코루틴 전체 (L659~713)

5. `SetDependencies()` 내부 `OnEntityAttacked` 구독 삭제 (L191~204):
   ```csharp
   // 공격 이벤트 구독 -- 싱글플레이 전용.
   // 멀티플레이에서는 NetworkCombatController가 ClientRpc로 직접 TriggerAttackAnimation() 호출.
   if (!NetworkContext.IsNetworkActive)
   {
       GameEvents.OnEntityAttacked
           .Subscribe(e =>
           {
               if (_unitData != null && e.Attacker == (IDamageable)_unitData)
               {
                   TriggerAttackAnimation(e.Target.Id, e.Target is UnitData);
               }
           })
           .AddTo(this);
   }
   ```

6. `StartWalkAnimation()` 내부 `_attackCoroutine` 가드 제거 (L576~580):
   ```csharp
   if (_attackCoroutine != null)
   {
       _isWalkPending = true;
       return;
   }
   ```

7. `StartWalkAnimation()` 내부 `_isWalkPending = false;` 제거 (L583)

8. `StopWalkAnimation()` 내부 `_isWalkPending = false;` 제거 (L607)

**신규 구독 추가 — SetDependencies() 내부 (기존 OnEntityAttacked 구독 위치 대체):**

```csharp
// 전투 상태 변화 이벤트 구독 -- 싱글플레이 전용.
// 멀티플레이에서는 NetworkCombatController가 ClientRpc로 직접 메서드 호출.
if (!NetworkContext.IsNetworkActive)
{
    // 전투 시작 -> Attack 루프 시작 + 타겟 방향 회전
    GameEvents.OnCombatStarted
        .Subscribe(e =>
        {
            if (_unitData != null && e.UnitId == _unitData.Id)
            {
                StopWalkAnimation();
                StartCombatAnimation(e.TargetId, e.TargetIsUnit);
            }
        })
        .AddTo(this);

    // 타겟 변경 -> 회전만 업데이트
    GameEvents.OnCombatTargetChanged
        .Subscribe(e =>
        {
            if (_unitData != null && e.UnitId == _unitData.Id)
            {
                ChangeTarget(e.NewTargetId, e.NewTargetIsUnit);
            }
        })
        .AddTo(this);

    // 전투 종료 -> Idle 전환
    GameEvents.OnCombatStopped
        .Subscribe(unitId =>
        {
            if (_unitData != null && unitId == _unitData.Id)
            {
                StopCombatAnimation();
            }
        })
        .AddTo(this);
}
```

**신규 메서드 3종 추가 (공격 애니메이션 섹션 교체):**

```csharp
// ====================================================================
// 전투 애니메이션 -- 상태 기반 (RPC/이벤트에서 호출)
// ====================================================================

/// <summary>
/// 전투 시작. 타겟 방향으로 즉시 스냅 회전 후 Attack CrossFade.
/// Attack 클립은 Loop Time=ON이므로 별도 루프 로직 불필요 — CrossFade 1회 호출로 무한 루프.
///
/// 호출 경로:
///   멀티플레이 -> NetworkCombatController.StartCombatClientRpc -> UnitView.StartCombatAnimation
///   싱글플레이 -> GameEvents.OnCombatStarted -> UnitView.StartCombatAnimation
/// </summary>
/// <param name="targetId">타겟 엔티티의 Id.</param>
/// <param name="targetIsUnit">true=유닛, false=건물.</param>
public void StartCombatAnimation(int targetId, bool targetIsUnit)
{
    if (_unitData == null || !_unitData.IsAlive) return;

    // 타겟 방향으로 즉시 스냅 회전
    // 서버에서 즉시 스냅 -> NetworkTransform이 클라이언트에 보간 전달
    float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
    transform.rotation = Quaternion.Euler(0f, angle, 0f);

    // Attack CrossFade 시작 -- Loop Time=ON이므로 자동 루프
    if (_animator != null)
    {
        _animator.speed = 1f;
        _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0);
    }
}

/// <summary>
/// 전투 중 타겟 변경. 회전만 업데이트, 애니메이션 상태 변경 없음.
/// Attack 루프는 이미 재생 중이므로 끊김 없이 방향만 전환.
///
/// 호출 경로:
///   멀티플레이 -> NetworkCombatController.ChangeTargetClientRpc -> UnitView.ChangeTarget
///   싱글플레이 -> GameEvents.OnCombatTargetChanged -> UnitView.ChangeTarget
/// </summary>
/// <param name="targetId">새 타겟 엔티티의 Id.</param>
/// <param name="targetIsUnit">true=유닛, false=건물.</param>
public void ChangeTarget(int targetId, bool targetIsUnit)
{
    if (_unitData == null || !_unitData.IsAlive) return;

    // 새 타겟 방향으로 즉시 스냅 회전
    float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
    transform.rotation = Quaternion.Euler(0f, angle, 0f);
}

/// <summary>
/// 전투 종료. Attack -> Idle CrossFade.
/// 이동 재개 시 StartWalkAnimation이 별도로 호출되므로 여기서는 Idle로만 전환.
///
/// 호출 경로:
///   멀티플레이 -> NetworkCombatController.StopCombatClientRpc -> UnitView.StopCombatAnimation
///   싱글플레이 -> GameEvents.OnCombatStopped -> UnitView.StopCombatAnimation
/// </summary>
public void StopCombatAnimation()
{
    if (_animator == null)
        _animator = GetComponentInChildren<Animator>();
    if (_animator == null) return;

    _animator.speed = 1f;
    _animator.CrossFadeInFixedTime(StateIdle, _toIdleBlend, 0);
}
```

**MoveAlongPath 전투 루프 수정 (L422~514):**

`_attackCoroutine` 대기 패턴을 제거하고 단순 `yield return null` 루프로 교체.
**적이 사거리 밖으로 나가면 `WaitForAttackCycleEnd()`로 현재 Attack 사이클 완료 대기 후 이동 재개.**

```csharp
// --------------------------------------------------------
// Lerp 이동 + 이동 중 전투 체크
// --------------------------------------------------------
float elapsed = 0f;
while (elapsed < moveSeconds)
{
    elapsed += Time.deltaTime;
    float t = Mathf.Clamp01(elapsed / moveSeconds);
    transform.position = Vector3.Lerp(fromPos, toPos, t);

    // 이동 중 전투 체크 (매 프레임)
    if (_combatUseCase != null && _unitData.IsAlive)
    {
        if (_combatUseCase.HasEnemyInRange(_unitData))
        {
            if (NetworkContext.IsNetworkActive)
            {
                // 멀티플레이 서버: 사거리 내 적 감지 -> 즉시 이벤트 발행
                GameEvents.OnUnitEnteredCombat.OnNext(_unitData.Id);

                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                {
                    yield return null;
                }

                if (!_unitData.IsAlive) break;

                // 적이 사거리 밖으로 나감 -> 현재 공격 애니메이션 사이클 완료까지 대기
                yield return WaitForAttackCycleEnd();

                if (!_unitData.IsAlive) break;

                // 대기 중 적이 다시 사거리에 들어왔으면 전투 루프 재진입
                if (_combatUseCase.HasEnemyInRange(_unitData))
                    continue;

                // 이동 재개: Walk 복귀 + 클라이언트에 Walk 시작 이벤트 발행
                if (_animator != null)
                {
                    _animator.speed = 1f;
                    _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
                }
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);
            }
            else
            {
                // 싱글플레이: HasEnemyInRange로 이동 차단, TryAttack은 쿨다운 기반으로 자동 실행

                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                {
                    _combatUseCase.TryAttack(_unitData);
                    yield return null;
                }

                if (!_unitData.IsAlive) break;

                // 적이 사거리 밖으로 나감 -> 현재 공격 애니메이션 사이클 완료까지 대기
                yield return WaitForAttackCycleEnd();

                if (!_unitData.IsAlive) break;

                // 대기 중 적이 다시 사거리에 들어왔으면 전투 루프 재진입
                if (_combatUseCase.HasEnemyInRange(_unitData))
                    continue;

                // 전투 종료 -> ClearCombatState()가 StopCombat 이벤트를 발행
                _combatUseCase.ClearCombatState(_unitData.Id);

                // 적 제거 후 Walk 복귀
                if (_animator != null)
                {
                    _animator.speed = 1f;
                    _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
                }
            }
        }
    }

    yield return null;
}
```

**신규 메서드 — WaitForAttackCycleEnd():**

적이 사거리 밖으로 나갈 때 공격 모션을 중간에 끊지 않고 현재 사이클을 완료한 뒤 이동을 재개하기 위한 코루틴.
`AnimatorStateInfo.normalizedTime % 1.0f`의 소수부를 매 프레임 추적하여 새 루프 사이클 진입 시점을 감지.

```csharp
private IEnumerator WaitForAttackCycleEnd()
{
    if (_animator == null) yield break;

    AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
    if (!stateInfo.IsName("Attack"))
    {
        yield return null;
        if (_animator == null) yield break;
        stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("Attack")) yield break;
    }

    float prevFrac = stateInfo.normalizedTime % 1.0f;

    while (true)
    {
        yield return null;
        if (_animator == null) yield break;
        if (_unitData == null || !_unitData.IsAlive) yield break;

        stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("Attack")) yield break;

        float currentFrac = stateInfo.normalizedTime % 1.0f;
        if (currentFrac < prevFrac)
            yield break;

        prevFrac = currentFrac;
    }
}
```

**MoveAlongPath의 멀티플레이 OnUnitEnteredCombat 발행 제거:**

기존 L425의 `GameEvents.OnUnitEnteredCombat.OnNext(_unitData.Id);`를 삭제한다.
이 이벤트 자체가 GameEvents에서 제거되므로, 발행 코드도 함께 삭제.

---

## 4. 구현 순서

### Phase 1: GameEvents 이벤트 구조 변경
1. GameEvents.cs에 `CombatStartedEvent`, `CombatTargetChangedEvent` 구조체 추가
2. GameEvents.cs에 `OnCombatStarted`, `OnCombatTargetChanged`, `OnCombatStopped` Subject 추가
3. GameEvents.cs에서 `OnUnitEnteredCombat` Subject 삭제

### Phase 2: UnitCombatUseCase 싱글플레이 전투 상태 추적
1. `_combatTargets` Dictionary 필드 추가
2. `TryAttack()` 내부에 상태 변화 감지 + 이벤트 발행 로직 추가
3. `ClearCombatState(int unitId)` 메서드 추가
4. `ExecuteAttack()` 내부 사망 처리에 `_combatTargets.Remove()` 추가

### Phase 3: NetworkCombatController 서버 전투 상태 추적
1. `_unitCombatTargets` Dictionary 필드 추가
2. `TickCombat()` 전면 재작성 (상태 변화 감지 + RPC 분기)
3. `ExecuteAttack()` 수정 (TriggerAttackAnimationClientRpc 호출 제거)
4. `TriggerAttackAnimationClientRpc` 삭제
5. `OnUnitEnteredCombat` 핸들러 + 구독 삭제
6. `StartCombatClientRpc` / `ChangeTargetClientRpc` / `StopCombatClientRpc` 추가
7. `OnEntityDied` 핸들러에 `_unitCombatTargets.Remove()` 추가
8. `OnNetworkDespawn`에 `_unitCombatTargets.Clear()` 추가

### Phase 4: UnitView 애니메이션 구조 교체
1. `_attackCoroutine`, `_isWalkPending` 필드 삭제
2. `TriggerAttackAnimation()`, `PlayAttackAnimation()` 메서드 삭제
3. `StartCombatAnimation()`, `ChangeTarget()`, `StopCombatAnimation()` 메서드 추가
4. `WaitForAttackCycleEnd()` 코루틴 추가 — 적이 사거리 밖으로 나갈 때 현재 Attack 사이클 완료 대기
5. `SetDependencies()` 내부 구독 교체 (OnEntityAttacked -> OnCombatStarted/Changed/Stopped)
6. `StartWalkAnimation()` / `StopWalkAnimation()` 단순화 (가드 코드 제거)
7. MoveAlongPath 전투 루프 수정 (`_attackCoroutine` 대기 -> 단순 yield, OnUnitEnteredCombat 발행 제거, 싱글플레이 ClearCombatState 호출 추가, **사거리 이탈 시 WaitForAttackCycleEnd() 대기 + 재진입 체크 추가**)

### Phase 5: 검증
1. 컴파일 확인 (OnUnitEnteredCombat 참조 전부 제거 확인)
2. 싱글플레이 테스트: 전투 진입/타겟 변경/전투 종료 애니메이션 확인
3. 멀티플레이 테스트: Host/Client 양쪽 애니메이션 동기화 확인
4. 엣지 케이스: 전투 중 유닛 사망, 타겟 사망, 이동 중 전투 진입/탈출
5. **사거리 이탈 테스트**: 적이 사거리 밖으로 나갈 때 공격 모션이 끊기지 않고 사이클 완료 후 Walk 전환 확인
6. **사거리 재진입 테스트**: WaitForAttackCycleEnd 대기 중 적이 다시 사거리에 들어올 때 전투 루프 재진입 확인

---

## 5. 위험 요소 및 대응

### 5-1. TickCombat 인터벌로 인한 첫 공격 지연 → 수정됨 (2026-03-30)

**문제**: 기존 설계에서는 StartCombatClientRpc를 TickCombat에서 전송하여 적 감지 후 최대 50ms 공백 발생 → 공격 애니메이션이 즉시 재생되지 않고 잠시 멈추는 버그.

**수정**: MoveAlongPath에서 적 감지 즉시 `StartCombatClientRpc`를 직접 호출. TickCombat은 데미지와 타겟 변경/종료 감지만 담당.

**UnitView.cs MoveAlongPath 변경:**
```csharp
if (_combatUseCase.HasEnemyInRange(_unitData))
{
    if (NetworkContext.IsNetworkActive)
    {
        // 적 감지 즉시 StartCombatClientRpc 전송 — TickCombat 대기 없음
        var firstTarget = _combatUseCase.TryFindTarget(_unitData);  // 없으면 TryFindAnyEnemy 필요 시 HasEnemyInRange 내부 로직 활용
        // NetworkCombatController에 즉시 전투 시작 알림
        GameEvents.OnUnitEnteredCombat.OnNext(_unitData.Id);  // → NetworkCombatController가 즉시 StartCombatClientRpc 전송
        ...
    }
}
```

**NetworkCombatController 변경:**
- `OnUnitEnteredCombat` 구독 복원 — 단, 이번에는 `StartCombatClientRpc`만 전송 (데미지 없음, TickCombat이 데미지 처리)
- TickCombat: `StartCombat`은 보내지 않고 `ChangeTarget` / `StopCombat`만 감지

### 5-2. 서버 MoveAlongPath와 TickCombat 동시 접근

**문제**: MoveAlongPath(코루틴)와 TickCombat(Update)이 같은 유닛의 상태를 동시에 수정할 수 있음.

**대응**: 기존과 동일한 패턴 유지. MoveAlongPath는 `HasEnemyInRange` 판정과 yield 대기만 수행. 실제 상태 관리(Dictionary 갱신, RPC 전송)는 TickCombat에서만 수행. 둘 다 메인 스레드에서 실행되므로 진정한 동시 접근은 발생하지 않음.

### 5-3. 유닛 사망 시 Dictionary 정리 누락

**문제**: 사망한 유닛이 `_unitCombatTargets`에 남아있으면 StopCombatClientRpc가 사망 유닛에 대해 전송됨.

**대응**: `OnEntityDied` 핸들러에서 사망한 유닛의 엔트리를 즉시 제거. 사망한 타겟을 공격 중이던 유닛들은 다음 TickCombat에서 TryFindTarget이 새 타겟 또는 null을 반환하여 자연스럽게 처리됨.

### 5-4. 싱글플레이 전투 종료 이벤트 발행 타이밍

**문제**: `ClearCombatState()`가 MoveAlongPath에서만 호출되면, 이동하지 않고 정지 상태에서 전투가 끝나는 경우(타겟이 사망해서 HasEnemyInRange가 false가 되는 순간) StopCombat 이벤트가 발행되지 않을 수 있음.

**대응**: MoveAlongPath 전투 루프 내에서 `HasEnemyInRange`가 false가 되면 루프를 탈출하면서 `ClearCombatState()` 호출. 정지 상태에서의 전투는 현재 설계에서 MoveAlongPath 코루틴이 없으므로 발생하지 않음 (유닛은 항상 이동 중에만 전투 진입). 단, 이동 완료 후 목적지에서 적과 교전하는 경우를 위해 `TickCooldowns()` 호출 경로에서도 `HasEnemyInRange` 변화를 감지해야 할 수 있음. 이 부분은 현재 코드에서 이동 완료 후 전투가 어떻게 처리되는지 추가 확인 필요.

### 5-5. Attack CrossFade 타이밍과 ChangeTarget 회전

**문제**: StartCombatAnimation에서 Attack CrossFade를 시작한 직후 다음 TickCombat에서 ChangeTarget이 도착하면, CrossFade 진행 중에 회전이 변경됨.

**대응**: 문제 없음. CrossFade는 Animator 내부 블렌딩이고, 회전은 transform.rotation 즉시 스냅으로 독립적. 두 작업이 서로 간섭하지 않음.

### 5-6. StopCombat과 StartWalk RPC 도착 순서

**문제**: TickCombat에서 StopCombatClientRpc를 전송하고, MoveAlongPath에서 OnUnitWalkStarted(-> StartWalkAnimationClientRpc)를 전송할 때 도착 순서가 보장되는지.

**대응**: 둘 다 같은 서버 프레임 내에서 전송되므로 순서가 보장됨 (NGO는 같은 프레임 RPC를 순서대로 전달). StopCombat(Idle CrossFade) 후 StartWalk(Walk CrossFade)가 같은 프레임에 도착하면 Walk가 이기므로 정상. 다른 프레임에 전송되더라도 Idle -> Walk 순서이므로 시각적 문제 없음.

---

## 부록: 변경 파일 요약

| 파일 | 삭제 | 추가 | 수정 |
|------|------|------|------|
| GameEvents.cs | `OnUnitEnteredCombat` | `CombatStartedEvent`, `CombatTargetChangedEvent`, `OnCombatStarted`, `OnCombatTargetChanged`, `OnCombatStopped` | - |
| UnitCombatUseCase.cs | - | `_combatTargets`, `ClearCombatState()` | `TryAttack()`, `ExecuteAttack()` |
| NetworkCombatController.cs | `TriggerAttackAnimationClientRpc`, `OnUnitEnteredCombat()`, `_enteredCombatSubscription` | `_unitCombatTargets`, `StartCombatClientRpc`, `ChangeTargetClientRpc`, `StopCombatClientRpc` | `TickCombat()`, `ExecuteAttack()`, `OnEntityDied()`, `OnNetworkDespawn()` |
| UnitView.cs | `_attackCoroutine`, `_isWalkPending`, `TriggerAttackAnimation()`, `PlayAttackAnimation()` | `StartCombatAnimation()`, `ChangeTarget()`, `StopCombatAnimation()`, `WaitForAttackCycleEnd()` | `SetDependencies()`, `StartWalkAnimation()`, `StopWalkAnimation()`, MoveAlongPath 전투 루프 (사거리 이탈 시 사이클 완료 대기 + 재진입 체크) |
