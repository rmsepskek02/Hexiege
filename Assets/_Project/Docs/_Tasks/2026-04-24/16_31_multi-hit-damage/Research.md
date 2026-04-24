# Research — 다중 히트 데미지 구현

**작업일:** 2026-04-24
**작업명:** multi-hit-damage

---

## 1. 요구사항

공격 애니메이션 1사이클 안에 히트가 여러 번 발생하는 유닛들이 있다.
현재는 첫 번째 히트 프레임에서만 데미지가 적용되고, 나머지 히트는 무시된다.
각 히트 프레임마다 데미지가 개별적으로 적용되어야 한다.

---

## 2. 다중 히트 유닛 현황 (StatsReference.md 기준)

### FlameSpirit — 6히트 공격

| 히트 | 프레임 표기 | 초 변환 (30fps) |
|------|------------|----------------|
| 1st  | 0:20       | 0.667s          |
| 2nd  | 1:05       | 1.167s          |
| 3rd  | 1:13       | 1.433s          |
| 4th  | 1:20       | 1.667s          |
| 5th  | 1:28       | 1.933s          |
| 6th  | 2:03       | 2.100s          |

- 클립 총 길이: 3:00 = 3.0s
- 히트당 공격력: 2 → 총 데미지 12

### LionKnight — 2히트 공격

| 히트 | 프레임 표기 | 초 변환 (30fps) |
|------|------------|----------------|
| 1st  | 0:22       | 0.733s          |
| 2nd  | 1:08       | 1.267s          |

- 클립 총 길이: 3:00 = 3.0s
- 히트당 공격력: 9 → 총 데미지 18

---

## 3. 현재 코드 구조

### 히트 프레임 타이밍 저장

- **`UnitStats.GetHitFrameTime(UnitType)`** ([UnitStats.cs:157](Assets/_Project/Scripts/Domain/Unit/UnitStats.cs#L157))
  - 반환형: `float` (첫 번째 히트 프레임 1개만)
  - 현재 FlameSpirit = `0.667f`, LionKnight = `0.250f` (⚠️ 불일치 — 아래 참조)
- **`UnitData.HitFrameTime`** ([UnitData.cs:85](Assets/_Project/Scripts/Domain/Unit/UnitData.cs#L85))
  - 단일 `float` 필드

### 싱글플레이 데미지 적용 흐름

```
UnitCombatUseCase.TryAttack()
  → ExecuteAttack()   ← 즉시 데미지 적용 (HitFrameTime 딜레이 없음)
  → attacker.AttackCooldownRemaining = AttackCooldown
```

- [UnitCombatUseCase.cs:125](Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs#L125)
- 현재 싱글플레이는 애니메이션과 무관하게 즉시 1회 데미지

### 멀티플레이 데미지 적용 흐름

```
NetworkCombatController.ExecuteAttack()
  → unit.AttackCooldownRemaining = unit.AttackCooldown  ← 쿨다운 즉시 리셋
  → StartCoroutine(DelayedAttackDamage(..., unit.HitFrameTime))  ← 코루틴 1개
    → WaitForSeconds(HitFrameTime)
    → UnitCombatUseCase.ApplyAttackDamage()  ← 1회만 데미지
```

- [NetworkCombatController.cs:381](Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs#L381)
- [NetworkCombatController.cs:356](Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs#L356)

### 싱글플레이 쿨다운 구동

- `GameBootstrapper.Update()` → `UnitCombatUseCase.TickCooldowns(dt)` 호출
- 다중 히트용 타이머도 동일 패턴으로 추가 가능

---

## 4. 코드 불일치 사항 (수정 필요)

StatsReference.md와 현재 코드(`UnitStats.cs`) 사이에 LionKnight 값이 다르다.

| 항목 | StatsReference | UnitStats.cs | 비고 |
|------|---------------|--------------|------|
| LionKnight AttackCooldown | 3:00 = 3.0s | 2.33f | 코드 수정 필요 |
| LionKnight HitFrameTime (1st) | 0:22 = 0.733s | 0.250f | 코드 수정 필요 |

UnitStats.cs의 주석 `// 0:15 = 0.250초`도 내부적으로 불일치 (0:15 at 30fps = 0.5s ≠ 0.250s). StatsReference를 권위 소스로 삼아 코드를 갱신한다.

---

## 5. 영향 범위

| 파일 | 레이어 | 변경 사유 |
|------|--------|----------|
| `UnitStats.cs` | Domain | `GetHitFrameTime()` → `GetHitFrameTimes()` 반환형 변경 |
| `UnitData.cs` | Domain | `HitFrameTime: float` → `HitFrameTimes: float[]` 변경 |
| `UnitCombatUseCase.cs` | Application | 싱글플레이 다중 히트 타이머 시스템 추가 |
| `NetworkCombatController.cs` | Infrastructure | `DelayedAttackDamage` 코루틴 N개 실행 |
| `GameBootstrapper.cs` | Bootstrap | `TickPendingHits(dt)` 호출 추가 |

### 영향 없는 파일 (이유)
- `UnitView.cs`: 데미지는 서버/UseCase가 처리, View는 애니메이션만 담당
- `BuildingPlacementUseCase.cs`, `UnitSpawnUseCase.cs`: 전투 로직 변경과 무관
- 네트워크 RPC 구조: StartCombat/ChangeTarget/StopCombat RPC 흐름 변경 없음

---

## 6. 설계 결정 사항

### 싱글플레이 다중 히트 처리 방식

`UnitCombatUseCase`는 `MonoBehaviour`가 아니므로 코루틴을 직접 사용할 수 없다.
대신 프레임마다 감소하는 타이머 리스트 방식으로 구현:

```
_pendingHits: List<PendingHit>
  PendingHit = { attacker, targetId, targetIsUnit, remainingDelay }

TryAttack() 호출 시:
  각 hitFrameTime마다 PendingHit 하나씩 enqueue

TickPendingHits(float dt) — GameBootstrapper.Update()에서 매 프레임 호출:
  모든 PendingHit의 remainingDelay -= dt
  remainingDelay <= 0이면 ApplyAttackDamage() 실행 후 제거
```

이 방식은 기존 `TickCooldowns(dt)` 패턴과 동일하므로 아키텍처 일관성 유지.

### 멀티플레이 다중 히트 처리 방식

`NetworkCombatController`는 `MonoBehaviour`이므로 코루틴 사용 가능.
기존 `DelayedAttackDamage` 코루틴을 `HitFrameTimes` 배열의 각 원소마다 실행.
각 코루틴은 독립적으로 동작하며 타겟 사망 시 자동 취소 (ApplyAttackDamage 내 생존 체크).
