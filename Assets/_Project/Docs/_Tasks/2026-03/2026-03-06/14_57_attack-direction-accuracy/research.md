# Research: 공격 방향 정밀도 개선

**날짜:** 2026-03-06
**배경:** git restore로 코드 전체를 HEAD로 복원 후 재작업. 2D→3D 레거시 정리와 공격 방향 정밀화를 함께 진행.

---

## 현재 코드 상태 (HEAD baseline 기준)

### 완료된 작업 (2026-03-06 오늘)
- `FacingDirection.cs` 2D 레거시 완전 제거 ✅
  - 제거됨: `ArtDirection` enum, `FacingInfo` struct, `PointyTopMapping[]`, `FlatTopMapping[]`, `FromHexDirection()`
  - 유지됨: `FromCoords()`, `EstimatePointyTopDirection()`, `EstimateFlatTopDirection()`

---

## 프리팹 구조 분석 (Unit_Pistoleer.prefab)

```
Unit_Pistoleer (루트)                ← UnitView 스크립트 부착
  m_LocalRotation: (0, 0, 0, 1)      ← 기본 회전 없음
  _rotationSpeed: 540
  _meshYOffset: 30                   ← 이미 저장된 메시 오프셋 값
  │
  └─ Unit_Pistoleer_Mesh (자식)      ← Animator + SkinnedMeshRenderer
       m_LocalEulerAnglesHint: (0, 30, 0)  ← 메시가 Y축 30° 틀어져 있음
       │
       ├─ Armature (X=-90° 보정, FBX Y-up → Unity Y-up)
       │    └─ Hips → 스켈레톤 계층
       └─ char1 (SkinnedMeshRenderer)
```

**핵심:** Meshy.ai 모델의 forward 방향이 Unity Z+ 기준에서 30° 틀어져 있음.
→ Atan2 계산 결과에서 30° 빼줘야 유닛이 정확하게 타겟을 향함.
→ `_meshYOffset = 30`이 프리팹에 이미 저장되어 있음 (UnitView 필드 복원 시 자동 로드됨).

---

## 현재 공격 방향 흐름 (HEAD 기준)

### 싱글플레이
```
UnitView.MoveAlongPath() [코루틴, 매 프레임]
  → _combatUseCase.TryAttack(_unitData)  [bool 반환]
        ↓ true 시
  UnitCombatUseCase.ExecuteAttack(attacker, target)
    → FacingDirection.FromCoords(attacker.Position, target.Position)  [HexDirection, 6방향 스냅]
    → attacker.Facing = attackDir  [도메인 기준]
    → GameEvents.OnEntityAttacked.OnNext(...)
          ↓
  UnitView (GameEvents.OnEntityAttacked 구독)
    → PlayAttackAnimation(_unitData.Facing)  ← ViewConverter.FlipDirection 미적용 (코드 불일치)
    → ApplyDirection(direction) → DirectionAngles[index] → transform.rotation Y축 회전
```

### 멀티플레이
```
NetworkCombatController.TickCombat() [서버, _attackInterval마다]
  → combat.TryAttack(unit)  [bool 반환]
        ↓ true 시
  → unit.Facing이 도메인 방향으로 갱신되어 있음
  → TriggerAttackAnimationClientRpc(unit.Id, (int)unit.Facing)  [int 2개]
        ↓ (모든 클라이언트)
  UnitView.TriggerAttackAnimation(HexDirection direction)
    → ViewConverter.FlipDirection(direction)  ← 여기서 반전 적용
    → PlayAttackAnimation(viewDir) → DirectionAngles → Y축 회전
```

---

## 현재 문제점

### 문제 1: 6방향 스냅 (근본 원인)
`FacingDirection.FromCoords()` → 큐브 좌표 delta → 6방향 스냅.
인접 타일(AttackRange=1)은 항상 정확. AttackRange≥2 또는 비인접 대상 추정 시 최대 30° 오차.
→ 현재 프로토타입은 AttackRange=1이므로 당장 큰 문제는 아니나, 3D에서는 시각적으로 어색함.

### 문제 2: 싱글플레이 ViewConverter.FlipDirection 미적용
멀티: `TriggerAttackAnimation`에서 `FlipDirection` 호출
싱글: `PlayAttackAnimation(_unitData.Facing)` 직접 호출 → 반전 없음
→ 싱글플레이는 항상 Blue팀 관점(IsFlipped=false)이라 현재 무해하지만 코드 불일치.

### 문제 3: NetworkCombatController RPC 정보 부족
`TriggerAttackAnimationClientRpc(unitId, facingInt)` → 스냅된 HexDirection int만 전달.
클라이언트가 Atan2 계산을 하려면 타겟 위치가 필요하지만 전달되지 않음.

---

## 현재 파일별 상태

| 파일 | 현재 상태 | 변경 필요 |
|------|---------|---------|
| `FacingDirection.cs` | 2D 레거시 제거 완료 ✅ | 없음 |
| `UnitCombatUseCase.cs` | `TryAttack() → bool`, `FromCoords()` 사용 | `TryAttack() → HexCoord?` 변경 필요 |
| `UnitView.cs` | DirectionAngles 테이블, `_meshYOffset` 필드 없음 | Atan2 로직 + `_meshYOffset` SerializeField 추가 |
| `NetworkCombatController.cs` | RPC에 `facingInt`만 전달 | `targetQ, targetR` 추가 |
| `UnitMovementUseCase.cs` | 확인 필요 (중복 Facing 업데이트 여부) | 미확인 |

---

## 관련 파일 경로
- `Assets/_Project/Scripts/Domain/Unit/FacingDirection.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Core/HexMetrics.cs` (`HexToWorld()` 사용)
- `Assets/_Project/Scripts/Core/ViewConverter.cs` (`ToView()`, `FlipDirection()`)
- `Assets/_Project/Prefabs/Units/Unit_Pistoleer.prefab`
