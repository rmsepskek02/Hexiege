# Plan: 공격 타이밍 정밀화

**작성일:** 2026-03-27

---

## 구현 전략

### A. 타격 프레임 데미지 (서버 딜레이 방식)

서버가 애니메이션 RPC를 즉시 전송한 뒤, `HitFrameTime`만큼 대기 후 데미지를 적용.
클라이언트는 RPC 수신 후 애니메이션을 재생하고, 타격 프레임 도달 시점에 서버 HP 감소가 도착.

### B. 클라이언트 HP 감소 타이밍

A 구현으로 서버 데미지 타이밍이 타격 프레임 기준으로 맞춰지므로 자동 개선.
추가 작업 없음.

### C. 쿨다운 관리 통일

`UnitView.Update()` 쿨다운 감소 제거.
싱글플레이 전용 쿨다운 감소 로직을 `UnitCombatUseCase.TickCooldown(float dt)`으로 이동하고 GameBootstrapper에서 호출.

---

## 파일별 변경 내용

---

### 1. `Domain/Unit/UnitStats.cs` — HitFrameTime 필드 추가

각 유닛 타입별 공격 애니메이션 타격 프레임 시간(초).
Animation Event가 발생하는 시점까지의 경과 시간.

```csharp
/// <summary>
/// 공격 애니메이션에서 타격 프레임까지의 시간 (초).
/// 서버가 애니메이션 RPC 전송 후 실제 데미지를 딜레이하는 기준값.
/// Attack.anim 타임라인에서 OnAttackHit Animation Event 위치로 설정.
/// </summary>
public float HitFrameTime;
```

**Inspector에서 각 유닛 ScriptableObject에 값 설정 필요** (Editor 스크립트 불필요, 직접 입력).

---

### 2. `Domain/Unit/UnitData.cs` — HitFrameTime 프로퍼티 추가

```csharp
/// <summary> 공격 타격 프레임까지의 시간 (초). UnitStats에서 복사. </summary>
public float HitFrameTime { get; private set; }
```

UnitData 생성자 또는 초기화 위치에서 `HitFrameTime = stats.HitFrameTime` 설정.

---

### 3. `Application/UseCases/UnitCombatUseCase.cs` — 메서드 분리 + 쿨다운 Tick 추가

**기존 `TryAttack()` 분리**:

```csharp
/// <summary>
/// 타겟 탐색만 수행. 데미지 없음, 쿨다운 리셋 없음.
/// 서버가 애니메이션 RPC 전송 전 타겟 유효성 확인용.
/// </summary>
public (int id, bool isUnit)? TryFindTarget(UnitData attacker)
{
    if (attacker == null || !attacker.IsAlive) return null;
    if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return null;
    if (attacker.AttackCooldownRemaining > 0f) return null;

    IDamageable target = FindFirstEnemyTarget(attacker);
    if (target == null) return null;

    return (target.Id, target is UnitData);
}

/// <summary>
/// 실제 데미지 적용. TryFindTarget() 후 hitFrameTime 딜레이 뒤 호출.
/// targetId로 타겟을 재탐색하여 소멸/이탈 여부를 재확인.
/// </summary>
public void ApplyAttackDamage(UnitData attacker, int targetId, bool targetIsUnit)
{
    // 딜레이 동안 공격자 사망 체크
    if (attacker == null || !attacker.IsAlive) return;

    // 타겟 재탐색 — 딜레이 동안 소멸/이탈 가능
    IDamageable target = FindTargetById(targetId, targetIsUnit);
    if (target == null || !target.IsAlive) return;

    // 사거리 재확인 — 딜레이 동안 이탈 가능
    if (!IsInRange(attacker, target)) return;

    ExecuteAttack(attacker, target);
    attacker.AttackCooldownRemaining = attacker.AttackCooldown;
}
```

**기존 `TryAttack()`**: 싱글플레이 하위 호환을 위해 유지하거나 `TryFindTarget + ApplyAttackDamage` 조합으로 내부 구현 변경.

**쿨다운 Tick 추가**:

```csharp
/// <summary>
/// 싱글플레이 전용 쿨다운 감소. GameBootstrapper.Update()에서 호출.
/// 멀티플레이에서는 NetworkCombatController가 처리하므로 호출 금지.
/// </summary>
public void TickCooldowns(float dt)
{
    foreach (var unit in _unitSpawn.Units.Values)
    {
        if (unit.AttackCooldownRemaining > 0f)
            unit.AttackCooldownRemaining = Mathf.Max(0f, unit.AttackCooldownRemaining - dt);
    }
}
```

---

### 4. `Presentation/Unit/UnitView.cs` — 쿨다운 감소 제거

**제거**: `Update()` 내 쿨다운 감소 코드 (L207-210)

```csharp
// 제거 대상:
private void Update()
{
    if (!NetworkContext.IsNetworkActive && _unitData != null && _unitData.AttackCooldownRemaining > 0f)
        _unitData.AttackCooldownRemaining = Mathf.Max(0f, _unitData.AttackCooldownRemaining - Time.deltaTime);
}
```

Update() 전체가 비면 메서드 삭제.

---

### 5. `Bootstrap/GameBootstrapper.cs` — 싱글플레이 쿨다운 Tick 호출

싱글플레이에서만 `Update()`에 추가:

```csharp
private void Update()
{
    if (!IsNetworkMode() && _combatUseCase != null)
        _combatUseCase.TickCooldowns(Time.deltaTime);
}
```

---

### 6. `Infrastructure/Network/NetworkCombatController.cs` — 딜레이 데미지 코루틴

**TickCombat() 변경**:

```csharp
private void TickCombat()
{
    // ...
    foreach (int id in unitIds)
    {
        // ...쿨다운 감소 유지 (서버 Tick 기반)...

        var attackResult = combat.TryFindTarget(unit);  // ← TryAttack → TryFindTarget으로 변경
        if (attackResult.HasValue)
        {
            // 애니메이션 RPC 즉시 전송
            TriggerAttackAnimationClientRpc(unit.Id, attackResult.Value.id, attackResult.Value.isUnit);
            // hitFrameTime 후 데미지 적용
            StartCoroutine(DelayedDamage(unit, attackResult.Value.id, attackResult.Value.isUnit, unit.HitFrameTime));
            // 쿨다운 리셋 (TryFindTarget이 하지 않으므로 여기서 처리)
            unit.AttackCooldownRemaining = unit.AttackCooldown;
        }
    }
}

private IEnumerator DelayedDamage(UnitData attacker, int targetId, bool targetIsUnit, float delay)
{
    if (delay > 0f)
        yield return new WaitForSeconds(delay);

    UnitCombatUseCase combat = _bootstrapper?.GetCombatUseCase();
    combat?.ApplyAttackDamage(attacker, targetId, targetIsUnit);
}
```

---

## Inspector 작업 (구현 완료 후)

각 유닛 ScriptableObject의 `HitFrameTime` 값 입력:
- `Pistoleer_Stats.asset` — Attack.anim에서 OnAttackHit 이벤트 위치 확인 후 입력
- `Assault_Stats.asset` — 동일
- `Sniper_Stats.asset` — 동일

Animation Event 위치 확인 방법: Unity Editor → Animation 창 → Attack 클립 → OnAttackHit 이벤트 타임라인 위치(초) 읽기

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| 딜레이 중 타겟 소멸 | hitFrameTime 대기 중 타겟이 다른 유닛에게 사망 | `ApplyAttackDamage()`에서 `target.IsAlive` 재확인 |
| 딜레이 중 공격자 소멸 | 공격자 사망 후 코루틴 계속 실행 | `attacker.IsAlive` 재확인 |
| 딜레이 중 사거리 이탈 | 이동으로 공격자/타겟 거리 벌어짐 | `IsInRange()` 재확인 (선택적 — 엄격도 결정 필요) |
| HitFrameTime=0 설정 | Inspector 미설정 시 딜레이 없이 즉시 데미지 | WaitForSeconds(0) → yield return null로 최소 1프레임 대기 |
| 싱글플레이 쿨다운 통일 후 체감 변화 | TickCooldowns 호출 주체 변경으로 미묘한 타이밍 차이 | 기존 `Time.deltaTime` 방식과 동일하므로 무관 |

---

## 구현 순서

```
[1] UnitStats.cs — HitFrameTime 필드 추가
    UnitData.cs — HitFrameTime 프로퍼티 추가
      ↓
[2] UnitCombatUseCase.cs
    - TryFindTarget() 신규 (타겟 탐색만)
    - ApplyAttackDamage() 신규 (데미지 + 사거리 재확인)
    - TickCooldowns() 신규
    - 기존 TryAttack()은 싱글 호환용으로 유지 (내부에서 TryFindTarget + ApplyAttackDamage 조합)
      ↓
[3] UnitView.cs — Update() 쿨다운 감소 제거
    GameBootstrapper.cs — Update() TickCooldowns 호출 추가
      ↓
[4] NetworkCombatController.cs
    - TickCombat()에서 TryFindTarget 사용 + DelayedDamage 코루틴 추가
    - 쿨다운 리셋을 TryFindTarget 성공 직후로 이동
      ↓
[5] 컴파일 확인
      ↓
[6] Inspector: 각 UnitStats ScriptableObject에 HitFrameTime 값 입력
      ↓
[7] 테스트: 싱글/멀티 양쪽에서 타격 프레임 데미지 타이밍 확인
```

---

## 아키텍처 제약 확인

- `UnitStats.cs` / `UnitData.cs` — Domain 레이어. `using Hexiege.Core` 금지. ✅ (float 필드만 추가)
- `UnitCombatUseCase.cs` — Application 레이어. NetworkBehaviour 금지. NetworkContext 정적 홀더 사용. ✅
- `GameBootstrapper.cs` — Bootstrap 레이어. 단일 composition root. Update() 추가 가능. ✅
- `NetworkCombatController.cs` — Infrastructure 레이어. NetworkBehaviour 허용. StartCoroutine 사용 가능. ✅
- `UnitView.cs` — Presentation 레이어. NetworkBehaviour 금지. NetworkContext 정적 홀더 사용. ✅
