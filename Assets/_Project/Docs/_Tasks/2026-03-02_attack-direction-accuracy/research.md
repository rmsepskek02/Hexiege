# Research: 유닛 공격 방향 정확도 이슈

**날짜:** 2026-03-02
**증상:** 공격 방향이 "가끔" 어긋남 (일관된 오류가 아닌 간헐적 발생)
**범위:** UnitCombatUseCase.cs (CalcViewDirection), UnitView.cs

---

## 전제 확인: DirectionAngles는 정상

`Unit_Pistoleer_Mesh` Inspector: **Rotation Y = 30°** (모델 기본 오프셋)

- 모델이 로컬 +Z에서 30° 틀어진 상태로 임포트됨
- DirectionAngles `{ 30°, 90°, 150°, 210°, 270°, 330° }` = 이 30° 오프셋 보정값
- 예: `DirectionAngles[NE=0] = 30°` → 부모 Y=30° + 메시 Y=30° = 총 60° → FlatTop NE 방향 ✓
- **수정 불필요**


> MEMORY.md TileHeight=0.36 오류 발견: GameConfig.cs 실제값 = **0.866f**
-> 메모리 업데이트 진행해

---

## 현재 공격 방향 결정 흐름 (싱글플레이)

```
MoveAlongPath → Lerp 완료 → 타일 도착
  → StartAttack(attacker)
      → FindFirstEnemyTarget()     ← 월드좌표 기반 사거리 체크 (이전 버그 수정됨)
      → CalcViewDirection()        ← ★ 여기가 문제
          → provider.TryGetWorldPosition(attacker)  → transform.position (Lerp 완료 = 정확)
          → provider.TryGetWorldPosition(target)    → transform.position (★ Lerp 중일 수 있음)
          → WorldDeltaToHexDirection(dx, dz)
      → attacker.Facing = 계산된 HexDirection
  → OnEntityAttacked 이벤트 발행
  → UnitView.PlayAttackAnimation(attacker.Facing)
```

---

## 핵심 원인 분석

### WorldDeltaToHexDirection 자체는 정확함

FlatTop 실제 이웃 방향의 월드 각도 (TileWidth=1.0, TileHeight=0.866 기준):

| HexDirection | 실제 atan2(dz,dx) 각도 | Sector boundary | 결과 |
|---|---|---|---|
| E | 330° | [330°, 30°) | ✓ 정확 |
| NE | 30° | [30°, 90°) | ✓ 정확 |
| NW | 90° | [90°, 150°) | ✓ 정확 |
| W | 150° | [150°, 210°) | ✓ 정확 |
| SW | 210° | [210°, 270°) | ✓ 정확 |
| SE | 270° | [270°, 330°) | ✓ 정확 |

Mathf.RoundToInt round-half-away-from-zero 기준으로 **모든 이웃이 경계값에 정확히 위치하여 올바르게 판별됨.**

**WorldDeltaToHexDirection 수정 불필요.**

### 실제 원인: 타겟의 Lerp 중 위치 사용

`CalcViewDirection`에서 **타겟이 유닛일 때 transform.position (Lerp 중 위치)** 을 사용:

```csharp
// CalcViewDirection 내부
Vector3 targetWP = ViewConverter.ToView(HexMetrics.HexToWorld(target.Position)); // 초기값 = 도메인 정중앙
if (target is UnitData tu)
{
    Vector3 tuWP = Vector3.zero;
    if (_positionProvider.TryGetWorldPosition(tu.Id, out tuWP))
        targetWP = tuWP;  // ★ Lerp 중인 시각적 위치로 덮어쓰기
}
return WorldDeltaToHexDirection(targetWP.x - attackerWP.x, targetWP.z - attackerWP.z);
```

**실제로 일어나는 일:**

```
공격자: 타일(2,1) 도착, transform.position = 정확한 헥스 중앙
타겟: 타일(3,1)→(3,2) Lerp 이동 중, transform.position = 이동 중간 위치

이상적인 각도 (헥스 중앙 기준): atan2(dz, dx) = 330° → E → 올바른 방향
Lerp 중 타겟 위치: 약간 이동 → 각도 335°, 325°... 등 변동

sector boundary: [330°, 30°) = E 섹터
→ 각도가 320°로 떨어지면: sector 5 → SE (틀림!)
→ 각도가 335°면: sector 0 → E (정상)
```

**섹터 경계(30°, 90°, 150°, 210°, 270°, 330°)가 정확히 이웃 헥스 방향과 일치**하므로,
타겟이 이웃 헥스 방향으로 이동 중이면 각도가 경계를 넘나들며 방향이 불안정해짐.

### 왜 "가끔" 발생하는가

| 상황 | 결과 |
|---|---|
| 타겟이 이동 중 + 이동 방향이 E/NE 경계와 교차 | ✗ 틀린 방향 |
| 타겟이 정지 중 (헥스 중앙) | ✓ 정확한 방향 |
| 타겟이 공격자로 이동 중 | ✓ 각도가 직선적, 경계 안쪽 |
| 타겟이 공격자 기준 대각선 방향으로 이동 중 | ✗ 경계 근처 위험 |

---

## 왜 공격자 위치는 괜찮은가

`StartAttack`은 **MoveAlongPath의 Lerp 완료 이후** 호출됨:
- 공격자 transform.position = 헥스 정중앙 = attacker.Position의 월드 좌표와 일치
- 공격자 측 오차 없음

→ 공격자 월드 좌표는 provider 없이 `HexMetrics.HexToWorld(attacker.Position)`으로 계산해도 동일.

---

## CalcViewDirection의 근본적 불필요성

`StartAttack`은 Lerp 완료 후만 호출 → **공격자는 항상 헥스 중앙에 있음**.

도메인 헥스 좌표 기반 `FacingDirection.FromCoords(attacker.Position, target.Position)`:
- 인접 타일(AttackRange=1): **정확한 HexDirection** (exact match)
- 비인접 타일: `EstimateFlatTopDirection` (합리적 추정)
- 타겟의 Lerp 상태에 영향받지 않음 → **안정적**

따라서 CalcViewDirection + WorldDeltaToHexDirection 전체를 단순화할 수 있음.

---

## 멀티플레이에서의 동일 분석

`ExecuteAttack`은 **이미 FacingDirection.FromCoords(attacker.Position, target.Position)** 사용:
- 타겟 Lerp 영향 없음
- 안정적이고 올바름

→ **멀티플레이 방향 계산은 현재 상태가 올바름. 수정 불필요.**

---

## 해결 방향

### Option A: CalcViewDirection에서 타겟 위치 고정 (최소 수정)
타겟이 유닛이어도 Lerp 위치 대신 도메인 헥스 중앙 사용:

```csharp
private HexDirection CalcViewDirection(UnitData attacker, IDamageable target)
{
    // 타겟은 항상 도메인 헥스 중앙 기준
    Vector3 attackerWP = Vector3.zero;
    if (_positionProvider != null && _positionProvider.TryGetWorldPosition(attacker.Id, out attackerWP))
    {
        Vector3 targetWP = ViewConverter.ToView(HexMetrics.HexToWorld(target.Position));
        return WorldDeltaToHexDirection(targetWP.x - attackerWP.x, targetWP.z - attackerWP.z);
    }
    HexDirection domainDir = FacingDirection.FromCoords(attacker.Position, target.Position);
    return ViewConverter.FlipDirection(domainDir);
}
```

장점: 최소한의 수정, 기존 구조 유지
단점: WorldDeltaToHexDirection 유지 (사실상 동일 결과를 다르게 계산)

### Option B: CalcViewDirection 단순화 → FacingDirection.FromCoords 직접 사용 (권장)
```csharp
// StartAttack 내부에서:
attacker.Facing = FacingDirection.FromCoords(attacker.Position, target.Position);
```

장점:
- 코드 대폭 단순화 (CalcViewDirection, WorldDeltaToHexDirection 전체 제거 가능)
- ExecuteAttack과 동일한 로직 → 싱글/멀티 일관성
- 타겟 Lerp 영향 없음, 안정적

단점:
- 멀티플레이에서 StartAttack은 호출되지 않으므로 ViewConverter.FlipDirection 불필요, 그냥 domainDir 반환

---

## 영향 범위

| 컴포넌트 | 수정 필요 | 내용 |
|---|---|---|
| `UnitCombatUseCase.cs` | **필요** | CalcViewDirection 단순화 / 타겟 위치 고정 |
| `UnitView.cs` | 불필요 | DirectionAngles 정상 (Y=30° 보정값) |
| `GameBootstrapper.cs` | 불필요 | HexMetrics.TileHeight 이미 수정됨 |
| MEMORY.md | **필요** | FlatTop TileHeight=0.36 오류 수정 |

---

## 참고: CalcViewDirection / WorldDeltaToHexDirection 제거 후 삭제 가능 코드
- `UnitCombatUseCase.cs`: `CalcViewDirection()` 전체, `WorldDeltaToHexDirection()` 전체
- `UnitCombatUseCase.cs`: `using Hexiege.Core` (ViewConverter, HexMetrics 사용 안 하면 불필요해질 수 있음)
  - 단, `FindFirstEnemyTarget` 건물 월드좌표 계산에 여전히 사용 중 → 유지 필요
