# Research: 공격 타겟 고정 (Target Lock)

**작성일:** 2026-03-27
**관련 작업:** `11_00_attack-timing-precision` (후속)

---

## 배경

HitFrameTime 딜레이 도입 후, 딜레이 중 타겟이 사거리를 벗어나면 데미지가 취소되는 현상 발견.

기획 의도: **공격 모션을 시작한 순간 타겟이 확정**. 이후 타겟이 범위를 이탈해도 데미지 적용.
(스나이퍼처럼 HitFrameTime이 긴 유닛에서 특히 중요)

---

## 현재 코드

`UnitCombatUseCase.ApplyAttackDamage()` (L122~141):

```
공격자 생존 체크
타겟 생존 체크 (사망/제거)
사거리 재확인 (IsTargetInRange)  ← 제거 대상
ExecuteAttack() + 쿨다운 리셋
```

---

## 변경 범위

| 파일 | 변경 내용 |
|------|-----------|
| `Application/UseCases/UnitCombatUseCase.cs` | `ApplyAttackDamage()`에서 `IsTargetInRange` 체크 1줄 제거 |

싱글/멀티 공통 경로 — 양쪽 모두 동일하게 적용됨.
공격자 생존, 타겟 생존 체크는 **유지** (죽은 대상에게는 데미지 없음).
