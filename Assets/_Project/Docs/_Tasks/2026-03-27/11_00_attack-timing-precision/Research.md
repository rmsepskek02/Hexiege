# Research: 공격 타이밍 정밀화

**작성일:** 2026-03-27

---

## 현재 공격 흐름 분석

### 싱글플레이

```
[UnitView.MoveAlongPath — 이동 루프]
  → UnitCombatUseCase.TryAttack(attacker)
      1. IsAlive 체크
      2. AttackCooldownRemaining > 0 → null 반환 (공격 불가)
      3. FindFirstEnemyTarget() — 사거리 내 타겟 탐색
      4. ExecuteAttack()
          ├─ target.TakeDamage()          ← 데미지 즉시 적용
          ├─ GameEvents.OnEntityAttacked  ← 이벤트 발행
          ├─ GameEvents.OnEntityDamaged   ← HP 동기화 이벤트
          └─ 사망 처리
      5. AttackCooldownRemaining = AttackCooldown  ← 쿨다운 리셋
  → UnitView가 OnEntityAttacked 수신
      → TriggerAttackAnimation() 호출   ← 데미지 이후 애니메이션 시작
          → PlayAttackAnimation() 코루틴
              → Animator.Play(StateAttack)
              → clipLen 대기
              → AnimationEventRelay.OnAttackHit() (타격 프레임)
                  → HitReactionCoroutine() — scale punch 비주얼만
```

**문제**: 데미지(4번)가 애니메이션(그 이후)보다 먼저 발생.

---

### 멀티플레이

```
[서버 — NetworkCombatController.Update()]
  _attackTimer += Time.deltaTime
  _attackTimer >= _attackInterval(0.05초) → TickCombat()
      → 각 유닛별 AttackCooldownRemaining -= _attackInterval
      → UnitCombatUseCase.TryAttack()
          → ExecuteAttack()
              ├─ target.TakeDamage()              ← 데미지 즉시 적용
              ├─ GameEvents.OnEntityDamaged        ← NetworkHealthSync 구독
              │       → HP NetworkVariable 업데이트 (클라이언트 자동 동기화)
              └─ 사망 시 OnEntityDied → EntityDiedClientRpc
      → TriggerAttackAnimationClientRpc(unitId, targetId, isUnit) 전송

[클라이언트 — RPC 도착 후]
  → TriggerAttackAnimation(targetId, isUnit) 호출
  → Animator.Play(StateAttack)
  → AnimationEventRelay.OnAttackHit() → scale punch
```

**문제 1**: 서버에서 데미지가 이미 적용된 후 RPC 전송 → 클라이언트에서 HP 감소(NetworkVariable)가 공격 애니메이션보다 먼저 도착할 수 있음.

**문제 2**: NGO는 NetworkVariable 업데이트와 ClientRpc 도착 순서를 보장하지 않음 → HP 감소 타이밍이 공격 모션과 어긋남.

---

## 쿨다운 관리 불일치

| 환경 | 위치 | 방식 |
|------|------|------|
| 싱글플레이 | `UnitView.Update()` L207-210 | 매 프레임 `Time.deltaTime` 감소 |
| 멀티플레이 서버 | `NetworkCombatController.TickCombat()` L196 | `_attackInterval(0.05초)` 고정 감소 |
| 멀티플레이 클라이언트 | 없음 | 서버에서만 관리 |

**문제**: 쿨다운 0.3초 유닛이 싱글에서는 정확히 0.3초, 멀티에서는 최대 0.05초 오차 발생. 또한 쿨다운 관리 로직이 두 곳(UnitView, NCC)에 분산됨.

---

## Animation Event 연결 구조

```
Attack.anim (애니메이션 클립)
  → 타격 프레임에 Animation Event 설정
  → AnimationEventRelay.OnAttackHit() 호출    (자식 Mesh GameObject에 부착)
  → UnitView.OnAttackHit() 호출               (부모 탐색)
  → HitReactionCoroutine()                    (scale punch 0.85배, 0.05초)
```

현재 `OnAttackHit`는 **비주얼 전용**. 데미지와 완전히 분리되어 있음.

---

## 개선 목표

1. **A. 타격 프레임 데미지**: 공격 모션의 타격 프레임 시점에 데미지 적용
2. **B. 클라이언트 HP 감소 타이밍**: HP 감소와 공격 모션이 시각적으로 동기화
3. **C. 쿨다운 관리 통일**: 싱글/멀티 동일 로직 사용

---

## A 구현 방안 검토

### 방안 1: 서버 딜레이 데미지 (채택)

```
서버:
  TryAttack() 성공 → 타겟 확보
  → TriggerAttackAnimationClientRpc 즉시 전송
  → WaitForSeconds(hitFrameTime) 코루틴 대기
  → 딜레이 후 ExecuteAttack() (데미지 적용)
  → HP NetworkVariable 업데이트 클라이언트 전달

클라이언트:
  RPC 수신 → 애니메이션 재생
  → 타격 프레임 도달 시점에 서버 데미지도 도착
```

**장점**: 서버 권위 유지, 애니메이션-데미지 타이밍 자연스럽게 정렬, B 문제도 자동 해소
**단점**: 딜레이 동안 타겟 소멸/이탈 예외 처리 필요

### 방안 2: 클라이언트 Animation Event 기반 데미지 서버 RPC
클라이언트의 타격 프레임 이벤트 → 서버에 데미지 요청 ServerRpc
→ 서버 권위 모델 유지하나, 추가 왕복 지연 발생. 불채택.

---

## hitFrameTime 저장 위치

Attack 애니메이션 클립의 타격 프레임 시간을 서버가 알아야 함.
`UnitStats.cs`에 `HitFrameTime` (float, 초) 필드 추가:
- Pistoleer: 실측 필요
- Assault: 실측 필요
- Sniper: 실측 필요

싱글플레이에서도 동일값 사용 → 단일 진실 소스.

---

## 영향 범위

| 파일 | 변경 내용 | 레이어 |
|------|-----------|--------|
| `Domain/Unit/UnitStats.cs` | `HitFrameTime` float 필드 추가 | Domain |
| `Domain/Unit/UnitData.cs` | `HitFrameTime` 프로퍼티 추가 | Domain |
| `Application/UseCases/UnitCombatUseCase.cs` | `TryFindTarget()` 분리 + `ApplyAttackDamage()` 신규 | Application |
| `Presentation/Unit/UnitView.cs` | 싱글 공격 흐름 변경, 쿨다운 감소 제거 | Presentation |
| `Infrastructure/Network/NetworkCombatController.cs` | TickCombat 딜레이 데미지 코루틴 추가 | Infrastructure |

싱글플레이 경로 완전히 보호 필요 — `NetworkContext.IsNetworkActive` 분기 확인.
