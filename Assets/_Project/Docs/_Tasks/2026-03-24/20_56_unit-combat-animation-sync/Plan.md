# Plan: 유닛 전투-애니메이션 동기화

## 구현 방향 요약

| 목표 | 구현 방식 |
|------|----------|
| 데미지 타이밍 = 타격 프레임 | 서버에서 `AttackHitDelay` 코루틴 대기 후 TakeDamage |
| 이동 중 공격 금지 | `UnitData.IsMoving` 플래그 → 서버 TickCombat에서 스킵 |
| 공격 중 이동 금지 | `IsMoving` 플래그 + `_attackCoroutine` null 체크 강화 |

---

## Step 1: UnitStats에 AttackHitDelay 추가

**파일**: `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs`

**변경 내용**: `AttackHitDelay` float 필드 추가

- Pistoleer: 0.833f
- Assault: 0.1f
- Sniper: 2.0f

ScriptableObject 또는 직렬화 가능한 구조체에 추가.

---

## Step 2: UnitData에 IsMoving 플래그 추가

**파일**: `Assets/_Project/Scripts/Domain/Unit/UnitData.cs`

**변경 내용**: `public bool IsMoving { get; set; }` 프로퍼티 추가

기본값: `false`

---

## Step 3: UnitView — IsMoving 플래그 관리 + 공격 중 이동 차단 강화

**파일**: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

### 3-1. MoveAlongPath 시작/종료 시 IsMoving 플래그 설정

```
MoveAlongPath 시작 → _unitData.IsMoving = true
MoveAlongPath 종료(break 포함) → _unitData.IsMoving = false
```

### 3-2. 이동 시작 전 공격 완료 대기

경로 이동 각 구간 시작 직전, `_attackCoroutine != null`이면 완료될 때까지 대기.
(현재는 Lerp 진행 중에만 공격 대기 — 타일 간 이동 전환 시점에 공백 존재)

```
각 타일 구간(for loop i++) 시작 시:
  while (_attackCoroutine != null) yield return null;
```

### 3-3. 멀티플레이 이동 대기 루프 정리

기존 대기 루프는 유지하되, `IsAttacking` 관련 명시적 플래그 없이 `_attackCoroutine`으로만 판단 (현행 유지).

---

## Step 4: NetworkCombatController — 히트 딜레이 + 이동 중 공격 스킵

**파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

### 4-1. TickCombat — 이동 중 유닛 공격 스킵

```csharp
if (unit.IsMoving) continue;
```

### 4-2. 공격 결정 후 히트 딜레이 코루틴

현재 흐름 (변경 전):
```
TryAttack() → ExecuteAttack() → TakeDamage() 즉시
            → TriggerAttackAnimationClientRpc()
```

변경 후:
```
TryAttack() → (데미지 분리) → TriggerAttackAnimationClientRpc() 전송
            → StartCoroutine(HitDelayDamageCoroutine(attacker, target, hitDelay))
                └── hitDelay 대기
                └── 타겟이 여전히 살아있으면 TakeDamage() + OnEntityDamaged + 사망처리
```

**구현 세부사항**:
- `UnitCombatUseCase.TryAttack()`은 현재 `ExecuteAttack()` 내부에서 TakeDamage를 즉시 실행 — 이를 분리해야 함
- 옵션: `TryAttack()` 반환값에 타겟 정보를 포함하고, 서버에서 별도 딜레이 코루틴으로 TakeDamage 처리
- `ExecuteAttack()` → 공격 방향 계산 + 이벤트 발행은 즉시, TakeDamage는 딜레이로 분리

### 4-3. AttackHitDelay 조회

```csharp
// UnitStats에서 유닛 타입별 hitDelay 조회
float hitDelay = GetHitDelay(unit.Type);
```

---

## Step 5: UnitCombatUseCase — ExecuteAttack 분리

**파일**: `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`

### 5-1. ExecuteAttack을 두 단계로 분리

**단계 A — 공격 선언** (즉시): 공격 방향 계산, `OnEntityAttacked` 발행
**단계 B — 데미지 적용** (딜레이 후 서버에서 호출): `TakeDamage()`, `OnEntityDamaged` 발행, 사망 처리

인터페이스 변경안:
```csharp
// 단계 A: 공격 방향만 설정 + OnEntityAttacked 발행 (딜레이 없음)
public void DeclareAttack(UnitData attacker, IDamageable target);

// 단계 B: 실제 데미지 적용 (서버 딜레이 코루틴에서 호출)
public void ApplyDamage(UnitData attacker, IDamageable target);
```

또는 `TryAttack()`에서 내부 `ExecuteAttack()`을 두 단계로 분리하고, NetworkCombatController에서 딜레이 후 `ApplyDamage(unitId, targetId)` ServerRpc 없이 직접 호출.

---

## 파일별 변경 요약

| 파일 | 변경 규모 | 내용 |
|------|----------|------|
| `UnitStats.cs` | 소 | `AttackHitDelay` 필드 추가 |
| `UnitData.cs` | 소 | `IsMoving` bool 프로퍼티 추가 |
| `UnitCombatUseCase.cs` | 중 | ExecuteAttack을 DeclareAttack + ApplyDamage로 분리 |
| `NetworkCombatController.cs` | 중 | IsMoving 스킵 + 히트 딜레이 코루틴 추가 |
| `UnitView.cs` | 소~중 | IsMoving 플래그 관리 + 타일 전환 시 공격 대기 추가 |

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| hitDelay 중 타겟이 사망/제거된 경우 | ApplyDamage 전 타겟 IsAlive 재확인 |
| 히트 딜레이 중 공격자가 사망한 경우 | 코루틴 시작 전 attacker null 체크, IsAlive 재확인 |
| AttackHitDelay 값이 AttackCooldown보다 길 경우 | UnitStats 값 검증 필요 (Sniper: hitDelay 2.0s, cooldown 확인 필요) |
| 싱글플레이에서 IsMoving 플래그 미반영 문제 | UnitView에서 플래그 관리하므로 싱글/멀티 동일하게 동작 |

---

## 아키텍처 제약 확인

- `UnitStats.cs`, `UnitData.cs` — Domain 레이어, Core 참조 금지 ✅
- `UnitCombatUseCase.cs` — Application 레이어, Domain만 참조 ✅
- `NetworkCombatController.cs` — Infrastructure 레이어, NetworkBehaviour 허용 ✅
- `UnitView.cs` — Presentation 레이어, Domain/Application 참조 허용 ✅

---

## 담당 에이전트

**game-programmer** 에이전트에게 위임.

전달 컨텍스트:
- 이 Plan.md + Research.md
- 관련 파일 절대 경로
- `.claude/agent-memory/game-programmer/MEMORY.md`
- `.claude/MEMORY.md` 내용
