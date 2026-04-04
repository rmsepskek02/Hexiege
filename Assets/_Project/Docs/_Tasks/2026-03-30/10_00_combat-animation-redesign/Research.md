# Research: 멀티플레이 전투/애니메이션 구조 재설계

## 1. 현재 구조 분석

### 1-1. 멀티플레이 전투 흐름 (서버 기준)

**서버 TickCombat (NetworkCombatController.cs L156~212)**

1. `Update()`에서 `_attackInterval`(0.05초=20Hz) 주기로 `TickCombat()` 호출
2. `TickCombat()`이 살아있는 모든 유닛을 순회:
   - 쿨다운 감소 (`unit.AttackCooldownRemaining -= _attackInterval`)
   - `UnitCombatUseCase.TryFindTarget(unit)` 호출 — 타겟 탐색만, 데미지 없음
   - 타겟 발견 시 `ExecuteAttack()` 호출
3. `ExecuteAttack()` (L255~265):
   - **`TriggerAttackAnimationClientRpc(unitId, targetId, targetIsUnit)` 즉시 전송**
   - 쿨다운 리셋 (`unit.AttackCooldownRemaining = unit.AttackCooldown`)
   - `DelayedAttackDamage` 코루틴 시작 (HitFrameTime만큼 대기 후 데미지 적용)

**서버 MoveAlongPath 전투 루프 (UnitView.cs L415~457)**

1. Lerp 이동 중 매 프레임 `_combatUseCase.HasEnemyInRange(_unitData)` 체크
2. 적 감지 시:
   - `GameEvents.OnUnitEnteredCombat.OnNext(_unitData.Id)` 발행
   - Walk 유지한 채 전투 대기 루프 진입
   - `_attackCoroutine != null`인 동안 대기 (매 프레임 yield)
   - `HasEnemyInRange == false`이면 루프 탈출 → Walk 재개

**OnUnitEnteredCombat 핸들러 (NetworkCombatController.cs L273~297)**

- MoveAlongPath가 적 감지 시 TickCombat 인터벌(50ms) 지연 없이 즉시 `ExecuteAttack()` 호출
- TryFindTarget 쿨다운 체크 포함, 0.1초 이하 잔여 쿨다운은 0으로 보정

### 1-2. 애니메이션 신호 흐름 (서버 -> 클라이언트)

**현재 RPC 목록:**

| RPC | 발생 시점 | 클라이언트 동작 |
|-----|----------|---------------|
| `TriggerAttackAnimationClientRpc(unitId, targetId, targetIsUnit)` | 매 공격 사이클마다 (쿨다운 리셋 시) | `UnitView.StopWalkAnimation()` + `UnitView.TriggerAttackAnimation()` |
| `StartWalkAnimationClientRpc(unitId)` | 이동 시작, 전투 루프 탈출 후 이동 재개 | `UnitView.StartWalkAnimation()` |
| `StopWalkAnimationClientRpc(unitId)` | 이동 완료, StopMovement() 호출 시 | `UnitView.StopWalkAnimation()` |
| `EntityDiedClientRpc(entityId, isUnit)` | 엔티티 사망 시 | 도메인 데이터 정리 + GameEvents.OnEntityDied 재발행 |

**핵심 문제: TriggerAttackAnimationClientRpc의 반복 호출**

- 서버 `TickCombat()`이 0.05초마다 실행
- 같은 유닛이 같은 타겟을 계속 공격할 때, **매 쿨다운 사이클마다** `TriggerAttackAnimationClientRpc` 전송
- 클라이언트 `TriggerAttackAnimation()` (UnitView.cs L631~651)에서 `_attackCoroutine != null`이면 무시하는 가드가 있음
- **그러나**: Attack 코루틴은 `clipLen` 대기 후 `_attackCoroutine = null`로 해제됨 (L687)
- 서버 쿨다운과 클라이언트 클립 길이가 정확히 일치하지 않으면, 코루틴 해제 직후 다음 RPC가 도착하여 **Attack 애니메이션을 처음부터 재시작**
- 결과: Attack 루프가 끊기고 다시 시작되는 **끊김 현상**

### 1-3. 이동/전투 전환 흐름

**서버 (MoveAlongPath):**
```
Walk 중 → HasEnemyInRange 감지 → OnUnitEnteredCombat 발행 → 전투 대기 루프
→ HasEnemyInRange false → Walk 재개 (OnUnitWalkStarted 발행)
→ 이동 완료 → OnUnitWalkStopped 발행
```

**클라이언트:**
```
StartWalkAnimationClientRpc → Walk CrossFade
TriggerAttackAnimationClientRpc → StopWalk + Attack CrossFade (매 사이클 반복)
StopWalkAnimationClientRpc → Idle CrossFade
```

**싱글플레이 (UnitView.cs L460~484):**
- `HasEnemyInRange` + `TryAttack` 직접 호출
- `GameEvents.OnEntityAttacked` 이벤트로 `TriggerAttackAnimation` 호출 (L192~203)
- 동일한 끊김 문제 가능성 있음 (매 TryAttack 성공 시 이벤트 발행)

---

## 2. 변경 대상 파일 및 범위

### 2-1. NetworkCombatController.cs (Infrastructure)

**삭제 대상:**
- `TriggerAttackAnimationClientRpc(int unitId, int targetId, bool targetIsUnit)` — 매 사이클마다 호출하는 현재 패턴 전체 삭제

**신규 작성:**
- `StartCombatClientRpc(int unitId, int targetId, bool targetIsUnit)` — "전투 시작" 신호. 클라이언트에서 Attack 루프 시작 + 타겟 방향 회전
- `ChangeTargetClientRpc(int unitId, int targetId, bool targetIsUnit)` — "타겟 변경" 신호. 회전만 업데이트, 애니메이션 재시작 없음
- `StopCombatClientRpc(int unitId)` — "전투 종료" 신호. Attack → Walk/Idle 전환

**수정 대상:**
- `TickCombat()` — 현재: 매 사이클마다 `ExecuteAttack()` → 변경: 유닛별 전투 상태(현재 타겟 ID) 추적, 상태 변화 시에만 RPC 전송
- `ExecuteAttack()` — 데미지 코루틴만 유지, 애니메이션 RPC는 상태 변화 로직으로 이동
- `OnUnitEnteredCombat()` — StartCombat 신호를 전송하도록 변경

**유지:**
- `EntityDiedClientRpc` — 변경 없음
- `StartWalkAnimationClientRpc` / `StopWalkAnimationClientRpc` — 유지 (이동/정지 동기화)
- `DelayedAttackDamage` — 코루틴 로직 유지 (데미지 타이밍은 변경 불필요)
- `_diedSubscription`, `_walkStartedSubscription`, `_walkStoppedSubscription` — 유지

**신규 필요 상태:**
- `Dictionary<int, int> _unitCombatTargets` — 유닛별 현재 전투 타겟 ID 추적 (key=unitId, value=targetId)
- 또는 `Dictionary<int, (int targetId, bool isUnit)>` 형태

### 2-2. UnitView.cs (Presentation)

**삭제 대상:**
- `PlayAttackAnimation(float yAngle)` 코루틴 — 단발성 클립 대기 패턴 삭제
- `_attackCoroutine` — 단발 코루틴 참조 삭제
- `_isWalkPending` — 공격 중 Walk 대기 플래그 삭제 (새 설계에서는 StopCombat이 명시적으로 오므로 불필요)

**신규 작성:**
- `StartCombatAnimation(int targetId, bool targetIsUnit)` — Attack 루프 시작. 타겟 방향 회전 + Attack CrossFade. Attack 클립은 Loop Time=ON이므로 별도 루프 로직 불필요
- `ChangeTarget(int targetId, bool targetIsUnit)` — 타겟 방향 회전만. 애니메이션 상태 변경 없음
- `StopCombatAnimation()` — Attack → Idle/Walk 전환

**수정 대상:**
- `TriggerAttackAnimation(int targetId, bool targetIsUnit)` — StartCombatAnimation으로 교체
- MoveAlongPath 전투 루프 (서버 측):
  - 현재: `_attackCoroutine` 완료 대기 → 변경: 전투 상태 플래그 기반으로 단순화
  - `_combatUseCase.HasEnemyInRange`로 루프, 별도 코루틴 없이 yield return null
- `StartWalkAnimation()` — `_attackCoroutine` 가드 삭제, 단순화
- `StopWalkAnimation()` — `_isWalkPending` 관련 코드 삭제

**유지:**
- `Initialize()`, `SetDependencies()` — 구조 유지
- `ApplyDirection()`, `CalculateAttackAngle()`, `GetTargetWorldPos()` — 유지
- `OnAttackHit()`, `HitReactionCoroutine()` — 비주얼 피드백 유지
- `MoveTo()`, `StopMovement()` — 이동 기본 로직 유지
- `StateWalk`, `StateAttack`, `StateIdle`, `AnimIsDead` 해시 — 유지
- 싱글플레이 `OnEntityAttacked` 구독 (L192~203) — 수정 필요 (StartCombatAnimation 호출로 변경)

### 2-3. GameEvents.cs (Application)

**삭제 대상:**
- `OnUnitEnteredCombat` — 현재: MoveAlongPath에서 첫 적 감지 시 발행 → 변경: NetworkCombatController 내부 로직으로 흡수

**유지:**
- `OnEntityAttacked` — 싱글플레이에서 UnitView가 구독. 단, 싱글플레이 전투 애니메이션도 새 패턴으로 맞춰야 할 수 있음
- `OnUnitWalkStarted` / `OnUnitWalkStopped` — 유지
- 나머지 모든 이벤트 — 변경 없음

**검토 필요:**
- `OnEntityAttacked` 발행 시점 — 현재 `UnitCombatUseCase.ExecuteAttack()`에서 매 데미지 적용 시 발행. 싱글플레이에서 이 이벤트가 매 사이클 호출 → 동일한 끊김 문제. 싱글플레이도 "전투 시작/종료" 패턴으로 변경 필요

### 2-4. UnitCombatUseCase.cs (Application)

**변경 없음 예상. 확인 사항:**
- `TryFindTarget()` — 서버에서 타겟 탐색 용도로 계속 사용. 변경 불필요
- `HasEnemyInRange()` — 서버 MoveAlongPath에서 전투 진입/탈출 판정으로 계속 사용
- `ApplyAttackDamage()` — 데미지 적용 로직 변경 없음
- `ExecuteAttack()` — 내부에서 `GameEvents.OnEntityAttacked.OnNext()` 발행. 싱글플레이에서 이것이 매 사이클 호출되는 문제 → 이 이벤트를 싱글플레이에서도 "전투 시작" 시에만 발행하도록 변경하거나, UnitView 구독 패턴을 변경

### 2-5. NetworkUnit.cs (Infrastructure)

**변경 없음.** LateUpdate의 Red 클라이언트 좌표/회전 보정은 독립적.

---

## 3. 새 설계에서 필요한 신규 구성요소

### 3-1. 신규 RPC 목록

| RPC | 파라미터 | 의미 |
|-----|---------|------|
| `StartCombatClientRpc` | `int unitId, int targetId, bool targetIsUnit` | 유닛이 전투 상태 진입. 클라이언트: Attack 루프 시작 + 타겟 방향 회전 |
| `ChangeTargetClientRpc` | `int unitId, int newTargetId, bool newTargetIsUnit` | 전투 중 타겟 변경. 클라이언트: 회전만 업데이트 |
| `StopCombatClientRpc` | `int unitId` | 유닛이 전투 상태 종료. 클라이언트: Idle/Walk 전환 |

### 3-2. 신규 서버 상태 (NetworkCombatController)

```
Dictionary<int, (int targetId, bool isUnit)> _unitCombatTargets
```

- key: unitId
- value: 현재 전투 중인 타겟 정보
- 유닛이 전투 중이 아니면 Dictionary에 없음
- TickCombat에서 매 사이클:
  1. TryFindTarget으로 타겟 탐색
  2. 이전 타겟과 비교:
     - 없었는데 생김 → `StartCombatClientRpc` + Dictionary 추가
     - 있었는데 같음 → RPC 없음, 데미지만 처리
     - 있었는데 다른 타겟 → `ChangeTargetClientRpc` + Dictionary 갱신
     - 있었는데 사라짐 → `StopCombatClientRpc` + Dictionary 제거

### 3-3. 삭제되는 기존 코드

| 위치 | 코드 | 이유 |
|------|------|------|
| NetworkCombatController | `TriggerAttackAnimationClientRpc` | Start/Change/Stop 3종 RPC로 대체 |
| NetworkCombatController | `_enteredCombatSubscription` (OnUnitEnteredCombat 구독) | MoveAlongPath에서 이벤트 발행 자체가 제거됨 |
| UnitView | `PlayAttackAnimation(float yAngle)` 코루틴 | 단발 클립 대기 패턴 불필요 (루프 기반으로 변경) |
| UnitView | `_attackCoroutine` 필드 | 코루틴 기반 공격 관리 폐기 |
| UnitView | `_isWalkPending` 필드 | 코루틴/Walk 충돌 관리 불필요 |
| UnitView | `TriggerAttackAnimation()` 메서드 | `StartCombatAnimation()`으로 대체 |
| GameEvents | `OnUnitEnteredCombat` Subject | NetworkCombatController 내부 상태 관리로 흡수 |
| UnitView (MoveAlongPath) | `GameEvents.OnUnitEnteredCombat.OnNext()` 호출 | 이벤트 자체 삭제 |

---

## 4. 아키텍처 제약 및 위험 요소

### 4-1. Clean Architecture 레이어 규칙

- **NetworkCombatController (Infrastructure)**: `_unitCombatTargets` Dictionary 추가는 OK. Infrastructure에서 UnitData 참조는 기존 패턴과 동일
- **UnitView (Presentation)**: 새 메서드(StartCombatAnimation, ChangeTarget, StopCombatAnimation)는 Presentation 레이어 내 순수 비주얼 메서드 — 문제 없음
- **GameEvents (Application)**: OnUnitEnteredCombat 제거는 Application 레이어 내 변경 — Infrastructure(NetworkCombatController)와 Presentation(UnitView)의 구독 코드도 함께 삭제 필요
- **UnitCombatUseCase (Application)**: OnEntityAttacked 발행 로직을 건드릴 경우, 싱글플레이 전투 흐름에 영향. 이벤트 자체는 유지하되 UnitView의 구독 패턴을 변경하는 것이 안전

### 4-2. NGO 제약

- **RPC 메서드명 규칙**: `StartCombatClientRpc`, `ChangeTargetClientRpc`, `StopCombatClientRpc` — "ClientRpc" 접미사 충족
- **RPC 파라미터**: `int`, `bool` — 기본 직렬화 가능 타입. 문제 없음
- **서버(Host)도 ClientRpc 수신**: `TriggerAttackAnimationClientRpc`에서 Host도 수신하여 서버 로컬 애니메이션을 처리하는 기존 패턴 유지. 새 RPC도 동일하게 Host 포함 수신

### 4-3. 위험 요소

**1. 서버 TickCombat과 MoveAlongPath의 동시 실행**
- TickCombat은 `_attackInterval` 주기로 실행, MoveAlongPath는 코루틴으로 매 프레임 실행
- 둘 다 같은 유닛에 대해 전투 상태를 판단하므로, `_unitCombatTargets` 갱신 시 경합 조건 주의
- 해결: MoveAlongPath는 `HasEnemyInRange` 판정만 하고, 실제 상태 관리/RPC 발행은 TickCombat에서만 수행

**2. 유닛 사망 시 Dictionary 정리**
- 유닛이 사망하면 `_unitCombatTargets`에서도 제거해야 함
- 타겟이 사망하면 해당 타겟을 공격 중이던 유닛의 엔트리도 정리 필요
- OnEntityDied 구독에서 처리하거나, TickCombat 시작 시 alive 체크로 자연 정리

**3. MoveAlongPath 전투 루프 재설계**
- 현재 `_attackCoroutine != null` 대기 패턴을 제거해야 함
- 서버에서는 `HasEnemyInRange` 체크로 전투 대기, Attack 애니메이션 타이밍은 신경 안 씀
- **중요**: `_attackCoroutine` 기반 대기를 제거하면, 서버 MoveAlongPath가 적이 사거리 내에 있는 동안 단순히 yield return null로 대기
- `_unitCombatTargets`에서 전투 상태를 읽어 전투 중인지 판단하는 것은 레이어 위반 (Presentation → Infrastructure)
- 대안: MoveAlongPath에서는 기존처럼 `HasEnemyInRange`만 사용. 전투 상태 Dictionary는 NetworkCombatController 내부에서만 관리

**4. 싱글플레이 일관성**
- 멀티플레이는 3종 RPC(Start/Change/Stop)로 전환되지만, 싱글플레이는 RPC를 쓰지 않음
- 싱글플레이에서도 동일한 "전투 시작/타겟 변경/전투 종료" 패턴 적용 필요
- 방안 A: 싱글플레이용 로컬 상태 머신을 UnitView에 추가
- 방안 B: UnitCombatUseCase에서 상태 변화 이벤트 발행, UnitView가 구독
- 방안 B가 Clean Architecture에 더 적합 (Application → Presentation 방향)

**5. Attack 클립 Loop Time=ON 전제**
- 현재 Attack 클립이 Loop Time=ON으로 설정되어 있어야 새 설계가 동작
- 확인 필요: Animator Controller에서 Attack 상태가 루프 가능한지
- 이미 L683 주석에 "Attack 클립은 Loop Time=ON"이라고 명시되어 있으므로 현재 설정 기준으로는 OK

**6. 공격 중 이동 재개 타이밍**
- StopCombat RPC 수신 후 Walk로 전환하는 타이밍이 기존보다 지연될 수 있음
- 기존: 서버 MoveAlongPath에서 직접 Walk CrossFade + StartWalkAnimationClientRpc
- 변경 후: TickCombat에서 StopCombatClientRpc → 클라이언트 Walk 전환
- TickCombat 인터벌(50ms)만큼 지연 가능 → 체감 미미하지만 확인 필요
- 대안: MoveAlongPath에서 전투 루프 탈출 시 OnUnitWalkStarted 이벤트 유지, NetworkCombatController에서 StartWalkRpc + StopCombatRpc 동시 전송

---

## 부록: 현재 파일 위치 및 라인 참조

| 파일 | 경로 | 주요 라인 |
|------|------|----------|
| NetworkCombatController.cs | `Assets/_Project/Scripts/Infrastructure/Network/` | TickCombat: L180~212, ExecuteAttack: L255~265, TriggerAttackAnimationClientRpc: L390~413 |
| UnitView.cs | `Assets/_Project/Scripts/Presentation/Unit/` | TriggerAttackAnimation: L631~651, PlayAttackAnimation: L659~713, MoveAlongPath 전투루프: L415~457 |
| GameEvents.cs | `Assets/_Project/Scripts/Application/Events/` | OnUnitEnteredCombat: L423, OnEntityAttacked: L332 |
| UnitCombatUseCase.cs | `Assets/_Project/Scripts/Application/UseCases/` | TryFindTarget: L90~106, HasEnemyInRange: L165~169 |
| NetworkUnit.cs | `Assets/_Project/Scripts/Infrastructure/Network/` | 변경 없음 |
