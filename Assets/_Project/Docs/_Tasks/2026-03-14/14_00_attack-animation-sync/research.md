# Research: 공격 애니메이션-데미지 적용 시점 불일치

**작성일:** 2026-03-14
**증상:** 공격 애니메이션과 데미지 반영 시점이 다르게 보여 어색함

---

## 현재 공격 흐름 분석

### 싱글플레이 흐름

```
[UnitView.MoveAlongPath]
  HasEnemyInRange() = true → Walk 정지 (animator.speed = 0f)
  ↓
  TryAttack(unit) 호출
    └─ AttackCooldownRemaining <= 0이면:
         ① target.TakeDamage(attacker.AttackPower)   ← 데미지 즉시 적용
         ② GameEvents.OnEntityAttacked.OnNext()       ← 이벤트 발행
              └─ UnitView: TriggerAttackAnimation() 호출
                   └─ PlayAttackAnimation 코루틴 시작
                        └─ animator.Play(StateAttack)  ← 애니메이션 시작 (1프레임 후 실제 재생)
         ③ if (!target.IsAlive) → OnEntityDied.OnNext() → 타겟 파괴
         ④ attacker.AttackCooldownRemaining = attacker.AttackCooldown
```

**문제:** ①번 데미지 적용이 ②번 애니메이션 시작보다 먼저 발생.
실제 타격 모션은 클립 중간 지점에 있으므로, **데미지가 들어간 시점과 타격 모션이 보이는 시점 사이에 수백 ms 간격 존재.**

---

### 멀티플레이 흐름

```
[NetworkCombatController.TickCombat() — 서버]
  TryAttack(unit) 호출
    └─ 공격 성공:
         ① target.TakeDamage() 즉시 적용 (서버)
         ② NetworkHealthSync → HP SyncHealthClientRpc 전송
         ③ TriggerAttackAnimationClientRpc(unitId, targetId, targetIsUnit) 전송
              └─ 클라이언트 수신: TriggerAttackAnimation() → PlayAttackAnimation 코루틴
```

**추가 문제:** 네트워크 RTT 지연(0~수십 ms)으로 인해
서버에서 데미지가 적용된 후 클라이언트에서 애니메이션이 시작되는 시점 차이가 더 벌어짐.

---

## 핵심 타이밍 문제 정리

### 문제 1: 데미지 선적용 — 애니메이션 후재생
- `ExecuteAttack()` 내에서 `TakeDamage()` → `OnEntityAttacked` 순서로 실행
- `OnEntityAttacked` 핸들러가 `TriggerAttackAnimation()` 호출 → 코루틴은 **다음 프레임**부터 실제 재생
- 타격 이펙트 없이 HP만 먼저 줄어드는 것처럼 보임

### 문제 2: 쿨다운과 애니메이션 길이의 역할 혼재
- `AttackCooldown = 클립 길이` (UnitFactory에서 Attack 클립 길이로 설정)
- 쿨다운 0 도달 즉시 데미지 적용 → 그 후 애니메이션 재생 시작
- 쿨다운은 "다음 공격까지 대기"를 의미하는데, 현재는 "애니메이션과 동시에 대기 시작"이지 "타격 시점에 데미지"가 아님

### 문제 3: 타겟이 죽으면 애니메이션 중간에 파괴
- `TakeDamage()` 직후 `IsAlive = false`이면 `OnEntityDied` 발행 → `Destroy(gameObject)` 예약
- 타겟 GameObject가 사라진 후에도 공격 애니메이션 코루틴이 실행 중인 경우 있음

---

## 관련 파일 및 핵심 코드 위치

| 파일 | 관련 로직 | 줄 번호 |
|------|----------|---------|
| `UnitCombatUseCase.cs` | `ExecuteAttack()` — TakeDamage + 이벤트 발행 순서 | 199~228 |
| `UnitCombatUseCase.cs` | `TryAttack()` — 쿨다운 체크 + AttackCooldownRemaining 설정 | 53~75 |
| `UnitView.cs` | `MoveAlongPath` — TryAttack 호출 타이밍, attackCoroutine 대기 | 396~412 |
| `UnitView.cs` | `PlayAttackAnimation()` — 1프레임 대기 후 clipLen 읽기 | 471~491 |
| `UnitView.cs` | `SetDependencies()` — OnEntityAttacked 구독 → TriggerAttackAnimation | 156~169 |
| `NetworkCombatController.cs` | `TickCombat()` — TryAttack 후 즉시 ClientRpc 전송 | 140~173 |
| `UnitFactory.cs` | Attack 클립 길이를 AttackCooldown으로 설정 | - |

---

## 구조적 원인 요약

현재 설계는 **"쿨다운 완료 = 공격 발동"** 패턴으로,
공격 발동 시점에 데미지와 애니메이션이 동시에 트리거되지만
실제 애니메이션의 타격 포인트(클립 중간)와 데미지 적용 시점이 다름.

가장 자연스러운 방식은 **"애니메이션 타격 포인트에 데미지 적용"** 이지만,
현재 구조에서는 클립 재생 중 특정 시점을 감지하는 수단이 없음.

---

## 해결 방향 후보

### 방향 A: 데미지 딜레이 (단순 접근)
- 공격 시작 시 애니메이션만 먼저 재생
- `clipLen * 타격비율(예: 0.4f)` 후에 데미지 적용
- 구현 위치: `PlayAttackAnimation` 코루틴 내에서 딜레이 후 데미지 콜백 호출
- **장점**: 구현 간단, 기존 구조 최소 변경
- **단점**: 타격 비율 하드코딩, 유닛마다 클립이 다르면 보정 필요

### 방향 B: Animation Event 방식 (정밀)
- Attack 애니메이터 클립에 Animation Event 추가
- 타격 프레임에 이벤트 콜백 → UnitView에서 데미지 콜백 실행
- **장점**: 정확한 타격 시점, 클립마다 개별 조정 가능
- **단점**: Animator 클립 편집 필요, 멀티플레이에서 서버 권위와 충돌 가능성

### 방향 C: 공격 시작 시 예약 패턴
- `TryAttack()`이 데미지를 즉시 적용하지 않고 "공격 예약" 상태 반환
- 일정 딜레이(고정 또는 비율) 후 별도로 데미지 적용
- **단점**: 멀티플레이 서버 권위 처리와 동기화 복잡도 증가

---

## 추가 분석: 사망 타이밍 충돌 여부

### 데미지 적용과 사망 판정은 동시

```
서버 TryAttack() →
  ExecuteAttack() {
    target.TakeDamage(attackPower)       // ①
    if (!target.IsAlive) {
      OnEntityDied.OnNext()              // ② (①과 동일 호출 스택)
    }
  }
```

**서버 입장에서 "데미지 시점 = 사망 판정 시점"은 이미 일치.**
두 이벤트가 같은 틱, 같은 함수 내에서 동기적으로 처리됨.

### 방향 A의 치명적 문제

방향 A(데미지 딜레이)는 서버 측 데미지 타이밍을 직접 변경함:
- 클라이언트에서 일정 딜레이 후 데미지 → 서버 권위 원칙 위반
- 또는 서버에서 "공격 예약" 후 딜레이 → 멀티플레이 동기화 복잡도 폭증
- `EntityDied`가 데미지 딜레이 완료 전에 도착하면 HP 버퍼 대기 상태에서 사망 처리 → 꼬임

**→ 방향 A 기각**

---

## 최종 결론: 시각 효과만 분리

**핵심 원칙:** 실제 데미지/사망 처리는 현재와 완전히 동일하게 유지.
**변경 범위:** 타격 프레임에 맞는 시각적 반응(scale punch, HP바 플래시)만 추가.

```
[현재]
공격 시작 → HP 즉시 감소 → 애니메이션 시작 → 타격 모션 프레임 (어색함)

[목표]
공격 시작 → HP 즉시 감소 → 애니메이션 시작 → 타격 모션 프레임 → scale punch + HP바 플래시
                                                                    (시각 반응 일치)
```

사망 판정/처리 타이밍 무변경 → 사망 꼬임 없음.

### 구현 방식 후보

**방식 1: 코루틴 hit ratio (단순)**
- `PlayAttackAnimation()` 내에서 `clipLen * hitRatio` 초 대기 후 시각 반응 실행
- hitRatio는 UnitView Inspector에서 유닛별로 개별 설정 (예: Pistoleer 0.3, Assault 0.4, Sniper 0.5)
- .anim 파일 편집 불필요, 코드만으로 구현

**방식 2: Animation Event (정밀)**
- 각 유닛 Attack.anim 클립에 Animation Event 삽입 (타격 프레임에)
- Animator가 있는 자식 GameObject에 AnimationEventRelay 컴포넌트 필요
- AnimationEventRelay → GetComponentInParent<UnitView>().OnAttackHit() 호출
- 가장 정밀하지만 .anim 클립 편집 + 추가 컴포넌트 필요

구체적인 방식 선택 및 구현은 plan.md에서 설계 예정.
