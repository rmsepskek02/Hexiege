# Plan — 다중 히트 데미지 구현

**작업일:** 2026-04-24
**작업명:** multi-hit-damage
**참조:** Research.md

---

## 구현 접근법

`float HitFrameTime`(단일 값)을 `float[] HitFrameTimes`(배열)로 교체하여
각 히트 프레임마다 데미지가 독립적으로 적용되도록 한다.

- **싱글플레이**: `UnitCombatUseCase`에 타이머 기반 `_pendingHits` 리스트 추가
- **멀티플레이**: `NetworkCombatController.ExecuteAttack()`에서 코루틴을 히트 수만큼 실행

---

## 파일별 변경 내용

### ① `UnitStats.cs` (Domain)

**변경**: `GetHitFrameTime()` 제거 → `GetHitFrameTimes()` 추가

```
GetHitFrameTimes(UnitType) → float[]

// 단일 히트 유닛: 원소 1개짜리 배열
Pistoleer    → [ 0.833f ]
Assault      → [ 0.133f ]
Sniper       → [ 2.000f ]
EmberSpirit  → [ 1.000f ]
InfernoSpirit→ [ 1.250f ]
FoxMagician  → [ 2.417f ]
BearGuard    → [ 0.667f ]

// 다중 히트 유닛
FlameSpirit  → [ 0.667f, 1.167f, 1.433f, 1.667f, 1.933f, 2.100f ]   // 6히트
LionKnight   → [ 0.733f, 1.267f ]                                    // 2히트
```

**추가 수정**: `GetAttackCooldown()`에서 LionKnight 2.33f → 3.0f 수정 (StatsReference 기준)

---

### ② `UnitData.cs` (Domain)

**변경**: `HitFrameTime` 필드 교체

```
// Before
public float HitFrameTime { get; set; }

// After
public float[] HitFrameTimes { get; set; }
```

두 생성자 모두에서 `UnitStats.GetHitFrameTime(type)` 호출을 `UnitStats.GetHitFrameTimes(type)`으로 교체.

---

### ③ `UnitCombatUseCase.cs` (Application)

**추가**: `PendingHit` 구조체 + `_pendingHits` 리스트 + `TickPendingHits()` 메서드

```
// 대기 중인 히트 항목
private struct PendingHit
{
    public UnitData attacker;
    public int targetId;
    public bool targetIsUnit;
    public float remainingDelay;
}
private readonly List<PendingHit> _pendingHits = new();

// TryAttack()에서 ExecuteAttack() 직접 호출 제거 →
// attacker.HitFrameTimes의 각 원소마다 PendingHit enqueue

// GameBootstrapper.Update()에서 매 프레임 호출
public void TickPendingHits(float dt)
{
    // 각 히트 타이머 감소 → 0 이하면 ApplyAttackDamage() 호출
}
```

**주의**: `TryAttack()`에서 기존 `ExecuteAttack()` 직접 호출 제거.
쿨다운 리셋(`attacker.AttackCooldownRemaining = attacker.AttackCooldown`)은 `TryAttack()`에 유지.

---

### ④ `NetworkCombatController.cs` (Infrastructure)

**변경**: `ExecuteAttack()` 내 코루틴 실행 부분

```
// Before
StartCoroutine(DelayedAttackDamage(unit, targetId, targetIsUnit, unit.HitFrameTime));

// After
foreach (float hitTime in unit.HitFrameTimes)
    StartCoroutine(DelayedAttackDamage(unit, targetId, targetIsUnit, hitTime));
```

`DelayedAttackDamage` 코루틴 본체는 변경 없음 — 기존 생존 체크 로직이 각 코루틴에서 독립 동작.

---

### ⑤ `GameBootstrapper.cs` (Bootstrap)

**추가**: `Update()`에 `_combatUseCase.TickPendingHits(Time.deltaTime)` 호출

```
void Update()
{
    _combatUseCase.TickCooldowns(Time.deltaTime);
    _combatUseCase.TickPendingHits(Time.deltaTime);  // 추가
}
```

---

## 예상 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| 타겟 사망 후 잔여 히트 | 1st 히트에 타겟이 사망하면 2~6번째 코루틴/타이머가 남아 있음 | `ApplyAttackDamage()` 내 `target.IsAlive` 체크가 이미 존재 — 자동 취소됨 |
| List 순회 중 Remove | `TickPendingHits()`에서 순회 중 완료된 항목 제거 | 역방향 순회 또는 완료 목록 별도 수집 후 일괄 제거로 처리 |
| LionKnight 쿨다운 값 변경 | 2.33f → 3.0f 변경 시 Inspector 설정값이 덮어쓸 수 있음 | UnitFactory의 Animator 클립 길이 읽기 로직이 덮어씌우므로 실제 영향 없음 (코드 참조값 수정만) |
| `HitFrameTime` 참조 코드 | 다른 파일에서 `unit.HitFrameTime`을 직접 참조하는 곳이 있을 수 있음 | `GetHitFrameTime()` 및 `HitFrameTime` 검색 후 전수 교체 필요 |

---

## 구현 체크리스트

- [ ] `UnitStats.cs`: `GetHitFrameTimes()` 추가, `GetHitFrameTime()` 제거, LionKnight AttackCooldown 수정
- [ ] `UnitData.cs`: `HitFrameTime` → `HitFrameTimes` 교체 (생성자 2개)
- [ ] `UnitCombatUseCase.cs`: `PendingHit` 구조체, `_pendingHits`, `TickPendingHits()` 추가
- [ ] `NetworkCombatController.cs`: `ExecuteAttack()` 코루틴 N개 실행으로 변경
- [ ] `GameBootstrapper.cs`: `TickPendingHits()` 호출 추가
- [ ] `HitFrameTime` 단수형 참조 잔재 검색 후 제거
