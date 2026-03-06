# Plan: 방향 시스템 레거시 정리 + 공격 방향 정밀화

**날짜:** 2026-03-02
**선행:** research.md 완료
**목표:**
1. 2D 레거시 잔재 완전 제거
2. 공격 방향을 "타겟 월드벡터 → Y회전" 직접 계산으로 단순화
3. UnitData.Facing 좌표계 통일 (항상 도메인 좌표)

---

## 변경 범위 요약

| 파일 | 작업 | 난이도 |
|---|---|---|
| `Domain/Unit/FacingDirection.cs` | 2D 레거시 코드 제거 | 낮음 |
| `Application/UseCases/UnitMovementUseCase.cs` | 중복 Facing 업데이트 제거 | 낮음 |
| `Application/UseCases/UnitCombatUseCase.cs` | CalcViewDirection/WorldDeltaToHexDirection 제거, StartAttack 단순화 | 중간 |
| `Presentation/Unit/UnitView.cs` | 공격 방향을 월드벡터 직접 계산으로 교체 | 중간 |
| `Infrastructure/Network/NetworkCombatController.cs` | RPC에 targetHex 추가 (네트워크 경로도 동일하게) | 중간 |

---

## 상세 변경 내용

### 1. FacingDirection.cs — 2D 레거시 제거

**제거 대상:**
- `ArtDirection` enum 전체
- `FacingInfo` struct 전체
- `PointyTopMapping` 배열
- `FlatTopMapping` 배열
- `FromHexDirection()` 메서드

**유지 대상:**
- `FromCoords(HexCoord from, HexCoord to)` — 헥스 좌표 → HexDirection 변환 (이동/네트워크 방향 계산에 사용)
- `EstimateFlatTopDirection()` / `EstimatePointyTopDirection()` — FromCoords 내부 사용

```csharp
// 변경 후 FacingDirection.cs 구조
namespace Hexiege.Domain
{
    public static class FacingDirection
    {
        // FromCoords() 만 남음
        public static HexDirection FromCoords(HexCoord from, HexCoord to) { ... }
        private static HexDirection EstimateFlatTopDirection(HexCoord delta) { ... }
        private static HexDirection EstimatePointyTopDirection(HexCoord delta) { ... }
    }
}
```

---

### 2. UnitMovementUseCase.cs — 중복 제거

`ProcessStep()`에서 `unit.Facing = dir` 및 관련 주석 3줄 제거.
UnitView.MoveAlongPath()가 동일한 작업을 이미 올바르게 수행 중.

---

### 3. UnitCombatUseCase.cs — StartAttack 단순화

**제거:**
- `CalcViewDirection()` 메서드 전체
- `WorldDeltaToHexDirection()` 메서드 전체

**StartAttack 변경 전:**
```csharp
attacker.Facing = CalcViewDirection(attacker, target); // 뷰 공간 (싱글플레이)
```

**StartAttack 변경 후:**
```csharp
attacker.Facing = FacingDirection.FromCoords(attacker.Position, target.Position); // 도메인 좌표
```

**결과:** 싱글플레이와 멀티플레이(ExecuteAttack) 모두 동일한 도메인 좌표로 통일.
`UnitData.Facing`은 항상 도메인 방향(Blue 기준).

---

### 4. UnitView.cs — 공격 방향을 월드벡터 직접 계산

**핵심 변경:**
`PlayAttackAnimation(HexDirection direction, IDamageable target)` 에서
HexDirection → DirectionAngles 테이블 참조 대신, target의 월드 위치와 자신의 위치로 직접 Y 회전 계산.

```csharp
// 변경 후 PlayAttackAnimation 도입부
private IEnumerator PlayAttackAnimation(HexDirection directionFallback, IDamageable target = null)
{
    if (target != null)
    {
        // 타겟 도메인 헥스 → 월드 좌표 (Lerp 영향 없는 안정적 위치)
        Vector3 targetWorldPos = HexMetrics.HexToWorldUnit(target.Position);
        Vector3 dir = targetWorldPos - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            // 메시 자식의 Y 오프셋 30° 보정 (Unit_Pistoleer_Mesh localEulerAngles.y)
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - _meshYOffset;
            _targetYRotation = angle;
        }
    }
    else
    {
        // 타겟 정보 없는 fallback (네트워크 경로 등)
        ApplyDirection(ViewConverter.FlipDirection(directionFallback));
    }
    // ... 이하 기존 애니메이션 로직 그대로
}
```

**추가 필드:**
```csharp
// Inspector에서 확인/조정 가능, 프리팹의 메시 자식 Y 오프셋과 일치시킴
[SerializeField] private float _meshYOffset = 30f;
```

**이동 방향(MoveAlongPath):** 변경 없음. DirectionAngles + 메시 30° 보정으로 이미 정확함.

---

### 5. NetworkCombatController.cs — RPC에 타겟 위치 추가

네트워크 경로도 클라이언트에서 직접 계산하도록 RPC 시그니처 변경.

**변경 전:**
```csharp
[ClientRpc]
private void TriggerAttackAnimationClientRpc(ulong attackerId, int direction)
{
    // 클라이언트: HexDirection → DirectionAngles
}
```

**변경 후:**
```csharp
[ClientRpc]
private void TriggerAttackAnimationClientRpc(ulong attackerId, int direction, int targetQ, int targetR)
{
    // 클라이언트: 타겟 도메인 위치 → 월드벡터 → Y회전 직접 계산
}
```

**서버 호출부 변경:**
```csharp
// ExecuteAttack 이벤트 처리 시
TriggerAttackAnimationClientRpc(
    unit.Id,
    (int)unit.Facing,       // 기존 유지 (도메인 방향, 하위 호환)
    target.Position.Q,      // 추가
    target.Position.R       // 추가
);
```

---

## 최종 방향 결정 흐름 (변경 후)

```
[싱글플레이]
StartAttack()
  → FacingDirection.FromCoords(attacker.Position, target.Position)
  → attacker.Facing = 도메인 방향
  → EntityAttackedEvent(attacker, target) 발행

UnitView.OnEntityAttacked()
  → PlayAttackAnimation(attacker.Facing, target)
      → HexMetrics.HexToWorldUnit(target.Position) - transform.position
      → Atan2(dir.x, dir.z) * Rad2Deg - 30°
      → _targetYRotation 적용  ← 정확, 안정적

[멀티플레이]
ExecuteAttack()
  → FacingDirection.FromCoords(attacker.Position, target.Position)
  → attacker.Facing = 도메인 방향
  → TriggerAttackAnimationClientRpc(id, direction, targetQ, targetR)

UnitView.TriggerAttackAnimation(direction, targetQ, targetR)
  → HexMetrics.HexToWorldUnit(new HexCoord(targetQ, targetR)) - transform.position
  → Atan2(dir.x, dir.z) * Rad2Deg - 30°
  → _targetYRotation 적용  ← 싱글플레이와 동일 로직
```

---

## 제거되는 코드량

| 제거 대상 | 라인 수 |
|---|---|
| ArtDirection enum | ~8줄 |
| FacingInfo struct | ~12줄 |
| PointyTopMapping/FlatTopMapping + FromHexDirection | ~30줄 |
| CalcViewDirection() | ~20줄 |
| WorldDeltaToHexDirection() | ~16줄 |
| UnitMovementUseCase 중복 | ~3줄 |
| **합계** | **~89줄 삭제** |

추가: ~20줄 (UnitView 직접 계산 로직, SerializeField)
**순 감소: ~69줄**

---

## 예외 사항 / 리스크

1. **_meshYOffset = 30°**: 현재 프리팹의 메시 자식 Y=30°와 일치. 다른 유닛 종류 추가 시 각 프리팹별 Inspector에서 설정 필요.

2. **RPC 시그니처 변경**: 빌드된 클라이언트와 서버 버전 불일치 주의. 개발 단계이므로 문제없음.

3. **FacingDirection.cs 레거시 제거**: `FromHexDirection()` 호출처가 없음을 Grep으로 사전 확인 완료.

4. **target.Position vs ClaimedTile**: target이 이동 중일 때 `target.Position`(완료된 타일)을 사용. 이전 버그 수정과 일관됨.

---

## 검증 시나리오

- [ ] 싱글플레이: 유닛이 인접 적 유닛 공격 시 방향 정확
- [ ] 싱글플레이: 유닛이 인접 적 건물 공격 시 방향 정확
- [ ] 싱글플레이: 이동 방향 변경 없음 (DirectionAngles 그대로)
- [ ] 멀티플레이: Blue팀 공격 방향 정확
- [ ] 멀티플레이: Red팀 공격 방향 정확 (FlipDirection 적용 확인)
