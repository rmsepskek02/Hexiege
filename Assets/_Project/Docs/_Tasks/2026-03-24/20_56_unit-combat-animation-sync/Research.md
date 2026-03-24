# Research: 유닛 전투-애니메이션 동기화

## 작업 목표

멀티플레이 상황에서 유닛의 시각적 표현(애니메이션)과 실제 게임 로직(이동/공격/데미지)을 일치시킨다.

- 공격 애니메이션 재생 중에는 이동하지 않는다
- 이동 애니메이션 재생 중에는 공격하지 않는다
- 데미지는 공격 애니메이션의 타격 프레임 타이밍에 발생한다

---

## 관련 파일

| 파일 | 레이어 | 역할 |
|------|--------|------|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | Presentation | 이동/공격 애니메이션 코루틴, _attackCoroutine/ClaimedTile 관리 |
| `Assets/_Project/Scripts/Presentation/Unit/AnimationEventRelay.cs` | Presentation | Animator Animation Event → UnitView.OnAttackHit() 중계 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` | Infrastructure | 서버 전투 Tick, TriggerAttackAnimationClientRpc, EntityDiedClientRpc |
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | Application | TryAttack, HasEnemyInRange, ExecuteAttack |
| `Assets/_Project/Scripts/Domain/Unit/UnitData.cs` | Domain | 유닛 상태 데이터 (IsAlive, Hp, AttackCooldownRemaining, ClaimedTile 등) |
| `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs` | Domain | 유닛 타입별 고정 스탯 |
| `Assets/_Project/Scripts/Application/Events/GameEvents.cs` | Application | OnEntityAttacked, OnEntityDamaged, OnEntityDied 이벤트 허브 |

---

## 현재 구조 분석

### 멀티플레이 공격 흐름 (현재)

```
[서버] NetworkCombatController.TickCombat()  (0.1초 간격)
  └── UnitCombatUseCase.TryAttack()
        └── ExecuteAttack()
              ├── target.TakeDamage()         ← 즉시 데미지 발생
              ├── OnEntityDamaged 발행        ← NetworkHealthSync가 HP 동기화
              └── 사망 시 OnEntityDied 발행  ← EntityDiedClientRpc 전파
  └── TriggerAttackAnimationClientRpc()      ← 데미지 발생 "후" 애니메이션 트리거
        └── [모든 클라이언트] TriggerAttackAnimation()
              └── PlayAttackAnimation() 코루틴
                    ├── DOTween Y축 회전
                    └── Animator.Play("Attack") → clipLen 대기 → _attackCoroutine = null
```

### 이동 중 전투 대기 루프 (현재, 멀티플레이 분기)

```csharp
// UnitView.MoveAlongPath 내부
if (_combatUseCase.HasEnemyInRange(_unitData))
{
    // 이동 Lerp 일시정지 → 공격 애니메이션 끝날 때까지 대기
    while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
    {
        while (_attackCoroutine != null)
            yield return null;
        yield return null;  // 한 프레임 더 대기
    }
    // Walk 복귀
}
```

---

## 발견된 문제점

### 문제 1: 데미지가 타격 프레임보다 먼저 발생

- **현상**: 서버 TickCombat에서 공격 결정 즉시 `TakeDamage()` 실행 → 그 이후 `TriggerAttackAnimationClientRpc()` 전송
- **결과**: 실제 데미지(HP 감소, 사망)가 시각적 타격 프레임보다 앞서 발생
- **유닛별 타격 프레임 시각 (Animation Event 위치)**:
  - Pistoleer: 0.833초
  - Assault: 0.1초
  - Sniper: 2.0초

### 문제 2: 이동 중에도 서버에서 공격 판정 발생

- **현상**: 서버 TickCombat은 매 0.1초 모든 유닛을 일괄 처리 → 유닛이 이동 Lerp 중이어도 공격 가능
- **결과**: Walk 애니메이션 재생 중 갑자기 Attack 애니메이션으로 전환 → 시각적 어색함
- **원인**: 서버에서 유닛의 이동 상태를 알 수 없음 (현재 UnitData에 IsMoving 플래그 없음)
- **참고**: `UnitData.ClaimedTile != null`이 Lerp 중 설정되나, 타일 도착 직후 null 리셋 → 다음 타일 Lerp 시작 전 공백 구간 존재

### 문제 3: 공격 애니메이션 완료 후 이동 재개가 부정확

- **현상**: 이동 대기 루프에서 `_attackCoroutine == null` 조건 만족 시 이동 즉시 재개
- **문제**: `_attackCoroutine`이 null이 된 시점과 Walk 복귀 처리 사이에 1프레임 공백 가능
- **추가**: 이동 Lerp 구간 외부(타일 도착 후 다음 타일 Lerp 전)에서는 공격 애니메이션 중 이동 차단 로직이 없음

---

## 설계 옵션 검토

### 옵션 A: 서버 히트 딜레이 코루틴 (채택)

공격 결정 후 유닛 타입별 히트 딜레이만큼 서버에서 대기 후 TakeDamage 실행.

**흐름:**
```
서버: 공격 결정 → TriggerAttackAnimationClientRpc() 전송
                → StartCoroutine(HitDelayDamage(attacker, target, delay))
                    └── delay 대기 (Pistoleer 0.833s, Assault 0.1s, Sniper 2.0s)
                    └── TakeDamage() + OnEntityDamaged + 사망 처리
```

**장점**: 단순, 서버 권위 유지, 레이어 위반 없음
**단점**: 딜레이 값이 Animation Clip 설정과 이중 관리됨 → UnitStats에 `AttackHitDelay` 필드 추가로 단일화

### 옵션 B: Animation Event → ServerRpc 방식 (미채택)

클라이언트 AnimationEvent → DealDamageServerRpc 호출 → 서버 TakeDamage.

**문제**:
- 클라이언트 이벤트가 서버 데미지를 트리거하는 역방향 의존 → 서버 권위 원칙 위반
- 네트워크 지연으로 Host/Client 간 타이밍 불일치
- 클라이언트가 임의로 데미지 ServerRpc를 호출할 수 있는 보안 취약점

---

### UnitData.IsMoving 플래그 추가

서버 TickCombat에서 이동 중인 유닛 공격 스킵에 사용.

- UnitView.MoveAlongPath 시작 시: `_unitData.IsMoving = true`
- MoveAlongPath 완료 시: `_unitData.IsMoving = false`
- 서버 TickCombat: `if (unit.IsMoving) continue;`

---

## 영향 범위

| 파일 | 변경 내용 |
|------|----------|
| `UnitStats.cs` | `AttackHitDelay` float 필드 추가 (유닛 타입별 타격 딜레이) |
| `UnitData.cs` | `IsMoving` bool 프로퍼티 추가 |
| `NetworkCombatController.cs` | 히트 딜레이 코루틴 + 이동 중 공격 스킵 로직 |
| `UnitView.cs` | IsMoving 플래그 관리, 공격 중 이동 차단 강화 |

**변경 불필요:**
- `UnitCombatUseCase.cs` — ExecuteAttack은 서버에서 딜레이 후 호출됨 (구조 유지)
- `AnimationEventRelay.cs` — 순수 비주얼(스케일 펀치) 역할 유지
- `GameEvents.cs` — 이벤트 구조 변경 없음

---

## 싱글플레이 호환성

싱글플레이에서는 `UnitView.MoveAlongPath` 내부 코루틴이 직접 `TryAttack()`을 호출.
`IsMoving` 플래그 체크는 싱글플레이에서도 동일하게 동작 — Walk 애니메이션 중 TryAttack이 호출되는 타이밍은 `HasEnemyInRange()`로 감지된 후이므로 자연스럽게 이동 중지 후 공격 전환.

히트 딜레이: 싱글플레이에서는 `UnitCombatUseCase.TryAttack()`이 데미지를 즉시 처리하므로 딜레이 없음. 단, `AnimationEventRelay.OnAttackHit()`으로 시각적 피격 효과만 유지.

> 싱글플레이에서 히트 딜레이를 동일하게 적용하려면 추가 작업 필요 — 우선 멀티플레이 대상으로 작업.
