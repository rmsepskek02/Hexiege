# Plan: 근접 공격 거리 다듬기

**날짜**: 2026-04-11

---

## 1. 작업 목적

근접 유닛(AttackRange = 0.5)의 공격 판정 거리를 타겟 타입별로 분리.
- 유닛 타겟: 0.3f — 두 유닛 메시가 시각적으로 닿아 보이는 거리
- 건물 타겟: 0.5f — 건물 메시가 커서 0.2f 일찍 감지해도 자연스럽게 닿아 보임

원거리 유닛(range ≥ 1.0)은 기존 로직 그대로 유지.

---

## 2. 변경 파일

`Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` 단일 파일만 수정.

---

## 3. 구체적인 수정 내용

### 상수 추가 (메서드 바깥 또는 메서드 상단)

```csharp
// 근접 유닛(AttackRange < 1.0) 전용 판정 거리 상수
// 유닛 타겟: 두 유닛 메시가 시각적으로 닿아 보이는 최소 거리
private const float MeleeContactDist = 0.3f;
// 건물 타겟: 건물 메시가 크므로 유닛 타겟보다 0.2f 먼저 감지
private const float BuildingDetectionRadius = 0.2f;
```

### `FindFirstEnemyTarget` 수정

현재 (line 272):
```csharp
const float Epsilon = 0.05f;
float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;
// 유닛/건물 루프 모두 동일한 maxDist 사용
```

수정 후:
```csharp
const float Epsilon = 0.05f;
bool isMelee = attacker.AttackRange < 1.0f;

// 근접 유닛은 타겟 타입별로 판정 거리를 분리.
// 원거리 유닛은 기존 AttackRange 기반 계산 유지.
float unitMaxDist     = isMelee
    ? MeleeContactDist + Epsilon
    : attacker.AttackRange * HexMetrics.TileHeight + Epsilon;
float buildingMaxDist = isMelee
    ? MeleeContactDist + BuildingDetectionRadius + Epsilon
    : attacker.AttackRange * HexMetrics.TileHeight + Epsilon;
```

유닛 루프에서 `maxDist` → `unitMaxDist` 사용.
건물 루프에서 `maxDist` → `buildingMaxDist` 사용.

### `IsTargetInRange` 수정

현재 (line 405):
```csharp
const float Epsilon = 0.05f;
float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;
```

수정 후:
```csharp
const float Epsilon = 0.05f;
bool isMelee = attacker.AttackRange < 1.0f;
bool targetIsBuilding = target is BuildingData;

float maxDist = isMelee
    ? (targetIsBuilding
        ? MeleeContactDist + BuildingDetectionRadius + Epsilon
        : MeleeContactDist + Epsilon)
    : attacker.AttackRange * HexMetrics.TileHeight + Epsilon;
```

---

## 4. 변경하지 않는 부분

| 항목 | 이유 |
|------|------|
| `FindFirstEnemyTargetByHexCoord` | HexCoord 폴백은 정수 거리 기반 — float 조정과 무관 |
| `NetworkCombatController` | TryFindTarget/HasEnemyInRange → UnitCombatUseCase 경유이므로 자동 반영 |
| `UnitData.AttackRange` | 수치 변경 없음 (0.5 유지) — maxDist 계산 방식만 바꿈 |
| 원거리 유닛 로직 | `isMelee = range < 1.0f` 분기로 완전히 보호됨 |

---

## 5. 수치 근거

| 항목 | 값 | 근거 |
|------|-----|------|
| `MeleeContactDist` | 0.3f | 유닛 메시 크기 기준 시각적 접촉 거리 (사용자 확정) |
| `BuildingDetectionRadius` | 0.2f | 건물 메시 반경 보정 — 건물이 유닛보다 크므로 일찍 감지 (사용자 확정) |
| `Epsilon` | 0.05f | 기존 유지 — 부동소수점 경계 오차 방지 |

---

## 6. 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| MeleeContactDist 너무 작음 | 유닛이 서로 겹쳐 보일 수 있음 | 실기에서 확인 후 수치 조정 |
| BuildingDetectionRadius 너무 큼 | 건물에서 멀리 떨어진 상태로 공격 시작 | 실기에서 확인 후 수치 조정 |
| Castle Lerp 이동 영향 | Castle 방향으로 Lerp 이동 중 buildingMaxDist(0.55f)가 기존(0.483f)보다 커졌으므로 더 일찍 감지 | 시각적으로 더 자연스러울 것으로 예상, 실기 확인 |
