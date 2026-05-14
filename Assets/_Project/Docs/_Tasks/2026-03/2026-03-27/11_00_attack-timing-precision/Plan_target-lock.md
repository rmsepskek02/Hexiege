# Plan: 공격 타겟 고정 (Target Lock)

**작성일:** 2026-03-27

---

## 목표

공격 모션 시작 시 타겟이 확정됨. HitFrameTime 딜레이 중 타겟이 사거리를 벗어나도 데미지 적용.
전 유닛 동일 적용. 타겟이 사망한 경우만 데미지 취소.

---

## 변경 내용

### `Application/UseCases/UnitCombatUseCase.cs`

`ApplyAttackDamage()` 에서 사거리 재확인 코드 제거:

```csharp
// 제거:
if (!IsTargetInRange(attacker, target)) return;
```

**유지되는 체크:**
- `attacker.IsAlive` — 공격자 사망 시 취소
- `target.IsAlive` — 타겟 이미 사망 시 취소

---

## 주석 수정

`ApplyAttackDamage()` 메서드 주석에서 "사거리 재확인" 항목 제거 후 타겟 고정 설계 명시.

---

## 위험 요소

없음. 제거하는 코드가 게임 로직의 다른 부분에 영향을 주지 않음.
`IsTargetInRange()` 헬퍼 메서드 자체는 삭제하지 않음 (다른 용도로 사용될 수 있음).
