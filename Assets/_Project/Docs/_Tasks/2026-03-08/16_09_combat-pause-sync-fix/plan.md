# Plan: 전투 일시정지 동기화 수정

**작성일:** 2026-03-08
**작업명:** combat-pause-sync-fix

---

## 목표

서버와 클라이언트가 전투 일시정지 시 동일한 위치에서 멈추도록 수정.

---

## 수정 파일 및 내용

### 1. `UnitCombatUseCase.cs`

**[A] `HasEnemyInRangeByCoord()` 메서드 추가**

HexCoord 기반으로만 범위 판정. Vector3 미사용 → 도메인 상태 기반 → 서버/클라이언트 항상 동일한 결과.

```csharp
/// <summary>
/// HexCoord 기반 범위 내 적 존재 여부 판정.
/// Vector3.Distance 미사용 — 도메인 상태(unit.Position)만 참조.
/// ProcessStep 이후 타일 경계에서 호출 → 서버/클라이언트 결과 일치 보장.
/// </summary>
public bool HasEnemyInRangeByCoord(UnitData attacker)
{
    if (attacker == null || !attacker.IsAlive) return false;
    return FindFirstEnemyTargetByHexCoord(attacker) != null;
}
```

---

### 2. `UnitView.cs` — `MoveAlongPath`

**[A] 전투 체크를 Lerp 루프 밖으로 이동 (ProcessStep 이후)**

```csharp
// 변경 전 (현재):
while (elapsed < moveSeconds)
{
    transform.position = Vector3.Lerp(fromPos, toPos, t);

    if (NetworkContext.IsNetworkActive)
    {
        if (_combatUseCase.HasEnemyInRange(_unitData))  // ← Lerp 도중 체크
        {
            while (HasEnemyInRange) { yield return null; }
        }
    }
    yield return null;
}
ProcessStep(from, to);

// 변경 후:
while (elapsed < moveSeconds)
{
    transform.position = Vector3.Lerp(fromPos, toPos, t);
    yield return null;
}
// Lerp 완료 → 타일 중심 위치 확정
transform.position = toPos;
ProcessStep(from, to);           // unit.Position = to (서버/클라이언트 동일)
_unitData.ClaimedTile = null;

// 전투 체크: ProcessStep 이후, 타일 경계에서만 실행
// HasEnemyInRangeByCoord: HexCoord 기반 → 서버/클라이언트 동일 결과
if (NetworkContext.IsNetworkActive && _combatUseCase != null && _unitData.IsAlive)
{
    if (_combatUseCase.HasEnemyInRangeByCoord(_unitData))
    {
        if (_animator != null) _animator.speed = 0f;
        while (_unitData.IsAlive && _combatUseCase.HasEnemyInRangeByCoord(_unitData))
        {
            while (_attackCoroutine != null)
                yield return null;
            yield return null;
        }
        if (!_unitData.IsAlive) break;
        if (_animator != null) _animator.speed = 1f;
    }
}
```

**싱글플레이 전투 체크는 현재 위치 유지** (서버/클라이언트 동기화 불필요).

---

## 수정 후 동작 흐름

```
[서버]
Lerp 완료(t=1.0) → transform.position = toPos → ProcessStep → unit.Position = tile_to
→ HasEnemyInRangeByCoord(HexCoord) → true/false 결정

[클라이언트]
Lerp 완료(t=1.0) → transform.position = toPos → ProcessStep → unit.Position = tile_to
→ HasEnemyInRangeByCoord(HexCoord) → 서버와 동일 결과
```

전투 판정 시점: 양쪽 모두 `t=1.0` (타일 중심)
전투 판정 기준: 양쪽 모두 `HexCoord.Distance(unit.Position, enemy.Position)` (도메인 상태)

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 유닛이 타일에 완전히 도착한 후 전투 시작 (이전: 이동 중 멈춤) | 시각적으로 타일 중심에서 멈추는 방식으로 변경. 동기화가 우선. |
| 싱글플레이 전투 체크 영향 | 싱글플레이 분기는 그대로 유지. 네트워크 분기만 수정. |
| ProcessStep 중복 호출 가능성 | 루프 구조 변경으로 ProcessStep 위치 재배치 — 중복 없음. |

---

## 수정 범위 요약

| 파일 | 변경 유형 |
|------|-----------|
| `UnitCombatUseCase.cs` | `HasEnemyInRangeByCoord()` 메서드 추가 |
| `UnitView.cs` | `MoveAlongPath` 전투 체크 위치 변경 (Lerp 내부 → ProcessStep 이후) |
