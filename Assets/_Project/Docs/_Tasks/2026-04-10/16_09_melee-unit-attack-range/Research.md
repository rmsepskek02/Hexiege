# Research: 근접(밀착) 유닛 공격 사거리 시스템

**날짜**: 2026-04-10  
**작업**: 완전 근접 공격 유닛 추가를 위한 사거리 시스템 분석

---

## 1. 요청 배경

현재 모든 유닛의 AttackRange 최솟값은 1.0(Pistoleer)으로,  
"인접 타일에 있을 때" 공격을 시작하는 구조다.

사용자는 이보다 더 근접해서 공격하는 유닛(예: 같은 타일에서 공격)을  
추가하고 싶어한다. 이를 위해 현재 시스템이 어떻게 동작하는지 파악한다.

---

## 2. 핵심 파일 목록

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs` | 유닛 타입별 AttackRange 정의 |
| `Assets/_Project/Scripts/Domain/Unit/UnitData.cs` | 유닛 데이터 — AttackRange 필드 |
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | 사거리 판정 로직 (FindFirstEnemyTarget) |
| `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs` | 경로탐색 blocked 구성 |
| `Assets/_Project/Scripts/Domain/Hex/HexPathfinder.cs` | A* 경로탐색 — blocked 적용 범위 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 이동 코루틴 + 전투 감지 루프 |

---

## 3. 현재 사거리 값

**`UnitStats.cs:34-40`**

| 유닛 | AttackRange | 실제 의미 |
|------|-------------|----------|
| Pistoleer | 1.0f | 인접 타일(1타일 거리)에서 공격 |
| Assault | 2.0f | 2타일 거리 |
| Sniper | 5.0f | 5타일 거리 |
| 기본값(_) | 1.0f | 최소 1타일 |

---

## 4. 사거리 판정 흐름 (UnitCombatUseCase.cs)

### 4-1. 월드 좌표 기반 (주 경로)

**`UnitCombatUseCase.cs:272`**

```
maxDist = attacker.AttackRange × HexMetrics.TileHeight(0.866f) + Epsilon(0.05f)
```

- Pistoleer(1.0): `1.0 × 0.866 + 0.05 = 0.916f`  
- FlatTop 인접 타일 실제 거리 = 0.866f → Epsilon으로 부동소수점 오차 흡수

### 4-2. HexCoord 기반 폴백 (positionProvider 없을 때)

**`UnitCombatUseCase.cs:333`**

```
distance <= attacker.AttackRange  (정수 비교)
```

- AttackRange=1.0 → distance <= 1 → 인접 타일(1) 또는 같은 타일(0) 모두 포함
- 실제로 같은 타일(0)은 이동 시스템이 허용하지 않음

---

## 5. "AttackRange < 1.0" 사용 시 문제점

### 문제 1: 월드 좌표 판정 — 대상을 절대 탐지 못함

AttackRange = 0.5로 설정하면:
```
maxDist = 0.5 × 0.866 + 0.05 = 0.483f
```
인접 타일 거리(0.866f) > maxDist(0.483f) → **인접 타일 적도 감지 불가**

AttackRange = 0.0:
```
maxDist = 0.0 × 0.866 + 0.05 = 0.05f → 동일 타일(0f)만 공격 가능
```

### 문제 2: HexCoord 폴백 — 같은 타일 불가

`distance <= 0` → HexCoord 거리 0 = 같은 타일이어야 공격 가능.  
하지만 이동 시스템이 적 타일 진입 자체를 막는다.

---

## 6. 이동 차단 로직 (UnitMovementUseCase.cs:58-68)

```csharp
foreach (var other in _unitSpawn.Units.Values)
{
    if (other.Id != unit.Id && other.IsAlive)
    {
        blocked.Add(other.Position);       // 아군 + 적군 모두 차단
        if (other.Team == unit.Team && other.ClaimedTile.HasValue)
            blocked.Add(other.ClaimedTile.Value);  // 아군 ClaimedTile만 추가 차단
    }
}
```

- **적군 Position도 blocked**에 포함 → 적 타일로 경로탐색 불가
- 단, A* 경로탐색(`HexPathfinder.FindPath`)에서 **blocked는 목표 타일에 적용되지 않음**  
  (경로 중간 타일에만 적용) — `HexPathfinder.cs:47~48` 주석 참조

즉, A* 경로의 목표 타일에는 도달할 수 있지만,  
경로 중간에 적이 있으면 우회한다.

---

## 7. MoveAlongPath의 이동 중 전투 감지 (UnitView.cs:432-434)

```csharp
if (_combatUseCase.HasEnemyInRange(_unitData))
```

Lerp 이동 중 매 프레임 `HasEnemyInRange`를 체크.  
사거리 내에 적이 들어오면 이동을 멈추고 전투 루프 진입.

이 체크가 `FindFirstEnemyTarget`을 호출하므로,  
AttackRange가 너무 작으면 **Lerp 중에 적 인접 타일을 그냥 통과**해버린다.

---

## 8. 결론: 변경이 필요한 범위

"같은 타일에서 공격"(range=0)은 이동 구조상 구현이 복잡하고 게임 설계와 충돌 가능성이 있다.

실제로 원하는 것이 "완전 근접"이라면, 두 가지 해석이 가능하다:

### 해석 A — AttackRange = 1.0 (기존과 동일), 단지 새 유닛 타입 추가
- 변경 없음, UnitType과 UnitStats에 신규 타입 추가만 필요
- "근접 유닛"이란 이름이지만 Pistoleer와 동일한 사거리

### 해석 B — 같은 타일 진입 후 공격 (range=0, 진짜 밀착)
- 이동 차단 로직 수정: 특정 조건(타겟 타일이 공격 목표)에서 적 타일 진입 허용
- 사거리 판정 수정: AttackRange=0일 때 "같은 타일" 조건으로 판정
- UnitData.ClaimedTile / MoveAlongPath에서 동일 타일 점령 처리 필요
- 멀티플레이 NetworkCombatController에도 동일 변경 필요

### 해석 C — AttackRange = 1.0이지만 이동 목표를 적 인접 타일까지만 설정
- 현재 이미 이렇게 동작 중 (인접 타일에서 전투 감지 후 이동 정지)
- 추가 변경 불필요

---

## 9. 미결 질문 (Plan.md 작성 전 사용자 확인 필요)

1. "완전 근접"이 구체적으로 어떤 의미인가?
   - 인접 타일(1타일 거리) = 현재 Pistoleer와 동일?
   - 같은 타일(0타일 거리) = 이동 시스템 수정 필요?

2. 신규 유닛의 UnitType을 무엇으로 할 것인가? (새 enum 값 추가?)

3. 멀티플레이 지원 범위 포함 여부?
