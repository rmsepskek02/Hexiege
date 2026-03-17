# Plan: 건물 인근 타일 이동/공격 불가 버그 수정

**날짜:** 2026-03-17
**관련 Research:** [research.md](research.md)

---

## 실제 버그 원인 확정

### 이동 흐름 재확인

`ProductionTicker.cs`의 `MoveTowardEnemyCastle()` → `FindPathToNearestEmptyTile()` (BFS, maxRange=3) 구조로 Castle 인접 타일을 자동으로 목표로 선택함. 즉, **건물 위치를 target으로 직접 전달하는 문제는 없음**.

### 버그 원인 1: ClaimedTile 선점으로 모든 인접 타일 차단 (이동 불가)

```
RequestMove() 내 blocked HashSet:
  ├── 모든 유닛의 Position
  └── 아군 유닛의 ClaimedTile ← 핵심 문제

FindPathToNearestEmptyTile() BFS:
  1. Castle 인접 타일 탐색 (6방향)
  2. IsWalkable 체크 통과
  3. HexPathfinder.FindPath() 호출
     → blocked에 아군 ClaimedTile 포함
     → 인접 타일들이 모두 아군 ClaimedTile로 선점됨
     → 경로 찾기 실패 → null 반환 → 이동 불가
```

아군 유닛들이 Castle 인근 타일에 ClaimedTile을 선점하고 있으면, 새 유닛은 그 타일로 경로를 찾지 못함. BFS가 다음 타일을 찾더라도 blocked로 막힘.

### 버그 원인 2: 공격 범위 경계값 부동소수점 오차 (공격 불가)

```
Pistoleer: AttackRange=1.0, HexMetrics.TileHeight=0.866
  → maxDist = 1.0 × 0.866 = 0.866

FlatTop 인접 타일 실제 월드 거리 = 0.866

부동소수점: 0.866000... <= 0.866000... → 연산 결과에 따라 false 가능
```

인접 타일에 정확히 도착했더라도 `dist <= maxDist` 조건을 만족하지 못해 타겟을 찾지 못함.

---

## 수정 방향

### 수정 1: `HexPathfinder.FindPath()` — 적 타일 blocked 예외 처리

**파일**: `Assets/_Project/Scripts/Domain/Hex/HexPathfinder.cs`

현재:
```csharp
if (blockedCoords != null && blockedCoords.Contains(goal)) return null;
```

목표 타일 자체가 blocked이면 즉시 null 반환. 실제 이동 중에는 목표 타일(인접 타일)에 다른 유닛이 ClaimedTile을 설정해 놓은 경우 차단됨.

**수정 내용**: `FindPath`의 goal 검증에서 blocked 체크를 제거.
→ 목표 타일은 blocked여도 경로를 찾을 수 있게 함. 실제 도착 시 충돌은 `ProcessStep()`에서 처리.

```csharp
// 제거: if (blockedCoords != null && blockedCoords.Contains(goal)) return null;
```

**근거**: `blocked`는 경로 중간 타일에만 적용해야 함. 목표 타일까지 blocked 적용 시 인접 타일 점령 중인 유닛 때문에 아무도 접근 못하는 교착 상태 발생.

---

### 수정 2: `UnitCombatUseCase.FindFirstEnemyTarget()` — 부동소수점 여유값 추가

**파일**: `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`

현재:
```csharp
float maxDist = attacker.AttackRange * HexMetrics.TileHeight;
if (dist <= maxDist && dist < minWorldDist)
```

수정:
```csharp
const float Epsilon = 0.05f;
float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;
if (dist <= maxDist && dist < minWorldDist)
```

**근거**: 인접 타일 거리(0.866) = Pistoleer maxDist(0.866)가 정확히 일치하는 경계 케이스. 부동소수점 연산 오차로 `0.866 <= 0.866`이 false가 될 수 있음. Epsilon=0.05는 타일 크기 대비 충분히 작아 원거리 타겟 오판 없음.

---

## 수정 파일 목록

| 파일 | 수정 내용 | 규모 |
|------|----------|------|
| `Domain/Hex/HexPathfinder.cs` | `FindPath` goal blocked 체크 제거 (1줄) | 소 |
| `Application/UseCases/UnitCombatUseCase.cs` | maxDist에 Epsilon 추가 (1줄) | 소 |

---

## 수정하지 않는 항목

- `BuildingPlacementUseCase.cs`: `IsWalkable=false` 정책 유지 (의도된 설계)
- `UnitMovementUseCase.cs`: RequestMove blocked HashSet 구성 방식 유지
- `ProductionTicker.FindPathToNearestEmptyTile()`: BFS 자체 로직 유지

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| goal blocked 제거 시 충돌 | 목표 타일에 도착했는데 다른 유닛이 있는 경우 | ProcessStep의 충돌 처리 로직 유지됨 — 문제 없음 |
| Epsilon 범위 과다 | 너무 크면 인접하지 않은 건물도 타겟 | 0.05f ≈ 타일 크기의 5.7% — 충분히 안전한 범위 |

---

## 테스트 체크리스트

- [x] 싱글플레이: 유닛 다수 생산 후 적 Castle로 이동 → 인접 타일 집결 가능 ✅
- [x] 싱글플레이: 인접 타일에서 Castle 공격 시작 → Pistoleer/Assault/Sniper 모두 공격 ✅
- [x] 멀티플레이: 적 Castle에 유닛 이동 → 공격 정상 동작 ✅
- [x] 인접 타일 5개 이상 유닛이 점유 중 → 새 유닛도 나머지 타일로 이동 가능 ✅
- [x] 원거리 유닛(Sniper, Range=5.0)이 먼 거리 빈 타일에서 Castle 공격 ✅
- [x] Castle 파괴 → 게임 종료 정상 동작 ✅
