# Research: 전투 일시정지 시 서버/클라이언트 동기화 어긋남

**작성일:** 2026-03-08
**작업명:** combat-pause-sync-fix

---

## 증상

HOST와 클라이언트 화면에서 유닛 위치가 다르게 보임.
특히 유닛이 많아질수록 격차가 벌어짐.

---

## 원인 분석

### 핵심 원인: HasEnemyInRange가 Lerp 도중 실행됨

`UnitView.MoveAlongPath` 내부 구조:

```csharp
while (elapsed < moveSeconds)  // Lerp 루프
{
    transform.position = Vector3.Lerp(fromPos, toPos, t);

    if (_combatUseCase.HasEnemyInRange(_unitData))  // ← 여기서 체크
    {
        while (HasEnemyInRange) { yield return null; }  // ← 여기서 멈춤
    }
}
// ProcessStep은 Lerp 완료 후 호출됨
_movementUseCase.ProcessStep(_unitData, from, to);
```

### HasEnemyInRange 구현 (UnitCombatUseCase.cs)

- 서버/클라이언트 가드 없음 → 양쪽 모두 실행됨
- `FindFirstEnemyTarget()` 호출
- `_positionProvider` 있으면 `Vector3.Distance(transform.position, ...)` 기반 판정
- `_positionProvider` 없거나 zero면 `HexCoord.Distance` 폴백

```csharp
// HasEnemyInRange: 서버 가드 없음 (양쪽 실행)
public bool HasEnemyInRange(UnitData attacker)
{
    if (attacker == null || !attacker.IsAlive) return false;
    return FindFirstEnemyTarget(attacker) != null;  // Vector3.Distance 사용
}

// TryAttack에만 서버 가드 있음
public (int id, bool isUnit)? TryAttack(UnitData attacker)
{
    if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return null;
    ...
}
```

### 왜 divergence가 발생하는가

서버와 클라이언트는 동일한 경로(path)를 받아 각자 Lerp를 실행.
프레임 타이밍 차이로 인해 `HasEnemyInRange`가 다른 `t` 시점에 true를 반환:

```
서버:  elapsed=0.09s, t=0.3 → HasEnemyInRange=true → transform.position = tile1과 tile2 사이 30% 지점에서 멈춤
클라이언트: elapsed=0.21s, t=0.7 → HasEnemyInRange=true → transform.position = 70% 지점에서 멈춤
```

두 화면에서 **같은 유닛이 다른 위치에 멈춰있음**.

전투가 끝나면 둘 다 Lerp를 재개하고 ProcessStep을 호출하여 unit.Position은 결국 동일해지지만,
그 사이 동안 **시각적 불일치** 누적.

### 유닛 겹침 현상과의 연관

A가 tile2 근처 중간 위치에서 전투 중 → 클라이언트에서 A는 tile2에 가까이 보임
B가 tile2로 이동 완료 → tile2 중심에 위치
→ A와 B가 겹쳐 보임 (A는 논리적으로 tile1에 있지만 시각적으로 tile2 근처)

---

## 영향 범위

| 파일 | 관련 내용 |
|------|-----------|
| `UnitView.cs` | MoveAlongPath: Lerp 루프 안에서 HasEnemyInRange 호출 |
| `UnitCombatUseCase.cs` | HasEnemyInRange → FindFirstEnemyTarget → Vector3.Distance |

---

## 전체 흐름 확인

| 컴포넌트 | 역할 | 서버/클라이언트 |
|----------|------|----------------|
| NetworkCombatController | TryAttack 매 0.1초 실행, TriggerAttackAnimationClientRpc 전송 | 서버 전용 |
| UnitCombatUseCase.TryAttack | 실제 데미지 실행 | 서버 전용 (NetworkContext 가드) |
| UnitCombatUseCase.HasEnemyInRange | 범위 내 적 존재 여부 | 서버 + 클라이언트 모두 |
| UnitView.MoveAlongPath | Lerp 이동 + 전투 대기 | 서버 + 클라이언트 모두 |
| NetworkHealthSync | HP 변화 동기화 | 서버 → 모든 클라이언트 |
| NetworkCombatController.EntityDiedClientRpc | 사망 전파 | 서버 → 모든 클라이언트 |
| ProductionTicker | 유닛 AI 이동 (랠리/Castle/Siege) | 서버 전용 (IsNetworkClient return) |
| BroadcastMoveClientRpc | AI 이동 경로 클라이언트 전파 | 서버 → 클라이언트 |
