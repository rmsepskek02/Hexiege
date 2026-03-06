# Research: Attack Cooldown & Animation System (2026-03-06)

## 현재 상태 파악

### 문제점
1. `_attackInterval` (NetworkCombatController)이 공격 폴링 빈도와 실제 공격 속도를 동시에 제어
   - 서버가 0.2초마다 TryAttack을 호출하면, 유닛이 적을 발견해도 최대 0.2초 지연 발생
   - Attack 애니메이션 클립 길이와 무관하게 공격이 반복됨 → 애니메이션-타격 타이밍 불일치

2. Walk→Attack 트랜지션: HasExitTime=true, ExitTime=94% → Attack 발동 최대 0.2초+94% 딜레이
   - 유저가 HasExitTime=false로 수정 완료

3. 이동 중 공격 차단 로직 없음 → 이동하면서 공격 판정 발생 가능

### 파일 현황

#### UnitData.cs (Domain)
- AttackCooldown, AttackCooldownRemaining 프로퍼티 없음
- TakeDamage() 구현됨

#### UnitStats.cs (Domain)
- GetAttackCooldown() 없음

#### UnitSpawnUseCase.cs (Application)
- SpawnUnit(): AttackCooldown 전달 없음

#### UnitCombatUseCase.cs (Application)
- TryAttack(): HexCoord? 반환 (공격 성공 시 target.Position)
- 쿨다운 체크 없음
- HasEnemyInRange() 없음

#### NetworkCombatController.cs (Infrastructure)
- _attackInterval = 0.2f (폴링 빈도)
- TickCombat(): TryAttack 호출 후 TriggerAttackAnimationClientRpc 전송
- 쿨다운 감소 로직 없음
- TriggerAttackAnimationClientRpc(unitId, targetQ, targetR) — 구현 완료

#### UnitView.cs (Presentation)
- MoveAlongPath(): TryAttack() != null 체크 (이동 차단)
- TriggerAttackAnimation(HexCoord targetPos) — Atan2 기반 구현 완료
- 싱글플레이 OnEntityAttacked 구독 → TriggerAttackAnimation 호출
- 싱글플레이 쿨다운 감소 없음

#### UnitFactory.cs (Infrastructure)
- OnUnitSpawned 구독 → 프리팹 생성
- Attack 클립 길이 읽어 unit.AttackCooldown 설정 로직 없음

#### Pistoleer.controller (Animation)
- 파라미터: IsDead(bool), Attack(trigger), IsWalking(bool) — 유저가 추가 완료
- 상태: Idle(기본), Walk, Attack, Dead
- Walk→Attack: HasExitTime=false, TransitionDuration=0.25 — 유저가 수정 완료

## 설계 결정

### AttackCooldown 값 설정 방법
- UnitFactory에서 프리팹 Animator의 Attack 클립 길이를 읽어 unit.AttackCooldown 설정
- UnitStats.GetAttackCooldown()은 fallback 기본값만 제공 (UnitFactory 읽기 실패 시)

### 쿨다운 감소 위치
- **싱글플레이**: UnitView.Update() (클라이언트 프레임마다)
- **멀티플레이**: NetworkCombatController.TickCombat() (서버에서 _attackInterval마다)
  - 정확도 vs 간단함 트레이드오프: TickCombat 주기(_attackInterval=0.1f)로 감소
  - 서버가 권위를 가지므로 클라이언트 쿨다운 감소는 불필요

### HasEnemyInRange() 추가
- UnitCombatUseCase에 추가
- 쿨다운 무관하게 범위 내 적 존재 여부만 판정
- MoveAlongPath 이동 차단 조건으로 사용

### IsWalking 파라미터 적용
- UnitView.MoveAlongPath: 이동 시작 시 IsWalking=true, 멈춤/도착 시 IsWalking=false
