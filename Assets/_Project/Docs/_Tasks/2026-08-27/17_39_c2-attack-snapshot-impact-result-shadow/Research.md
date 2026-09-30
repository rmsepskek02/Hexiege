# C2 공격 Snapshot·ImpactResult Shadow Research

이번 작업은 서버가 확정한 공격 회차의 타겟과 타격 방향, Legacy 피해 writer가 실제로 만든 타격 결과를 하나의 안정된 결과 키로 연결해 Host와 Client에서 비교할 수 있게 만드는 조사다. C1에서 확보한 정지·5도 공격 시작, 연속 Attack 표현, 예약 TargetId와 AttackSequenceId 결속은 유지한다. 이번 단계도 서버 권위 Legacy 피해·HP·사망 이벤트·RPC·VFX를 대체하지 않는 Shadow 단계다.

## 1. 현재 구현 상태

C1 서버 경계는 공격 시작 시 `AttackerInstanceId + AttackSequenceId + TargetId`를 고정하고, 각 Legacy 지연 피해 코루틴에 `LegacyAttackToken`을 전달한다. 피해 호출 직전에는 같은 token으로 `UnitActionSequencer.EvaluateImpact`를 실행하여 타격 번호, 권위 AimDirection, 적중 또는 빗나감 authorization을 만든다.

현재 네트워크로 복제되는 값은 `NetworkUnitActionShadowState` 하나다. 이 값에는 회차, revision, phase, target, delivery, commit 시각, SimulationFacing과 마지막 Impact Aim이 들어가므로 현재 Snapshot은 원자적으로 관측할 수 있다. 그러나 다음 정보는 아직 서버 로컬 진단에만 남는다.

- Legacy 피해 writer가 실제로 적용했는지 또는 공격자·타겟 부재로 종료했는지
- 적용한 피해량과 결과 HP
- 결과를 식별하는 전체 `AttackResultKey`
- 권위 Impact 시각과 AimDirection을 결합한 개별 결과 메시지
- Client가 개별 결과를 중복·충돌 없이 받았는지에 대한 증거

따라서 C1의 `legacy-impact-dispatch`는 피해 호출 직전의 authorization과 호출 뒤 `AttackDamageApplyStatus`를 비교할 수 있지만, 정식 `AttackImpactResult` 생성·확인·복제까지 완료한 상태는 아니다.

## 2. 기존 계약에서 재사용할 수 있는 부분

`UnitActionContracts.cs`에는 이미 다음 순수 계약이 존재한다.

- `ImpactAuthorization`: action revision, 전체 결과 키, 고정 target, Impact 서버 시각, 권위 AimDirection, Hit/Miss 판정
- `AttackImpactResult`: 결과 키, Impact 서버 시각, 권위 AimDirection, 선택적 ImpactPoint, outcome, 적용량, 결과 HP
- `AttackResultKey`: 공격자 수명, 회차, hit index, victim, effect kind, result ordinal을 포함하는 멱등 키

`UnitActionSequencer.ConfirmImpactResult`도 역순 결과를 허용하면서 authorization과 정확히 일치하는 결과만 확인하도록 이미 구현돼 있다. 새 도메인 모델을 병렬로 만들기보다 이 계약을 production adapter와 네트워크 전송에 연결하는 것이 아키텍처 보존에 가장 안전하다.

## 3. 확인된 구조적 공백

### 3.1 Legacy writer의 결과 정보가 부족하다

`UnitCombatUseCase.ApplyAttackDamageObserved`는 현재 `Applied`, `AttackerUnavailable`, `TargetUnavailable`만 반환한다. 바깥 observer가 HP 전후를 추정하면 사망 처리나 특수 공격 경계와 경쟁할 수 있다. 피해 writer 내부에서 주 타겟의 적용 전후 HP를 같은 호출 안에서 캡처하고, 상태·적용량·결과 HP를 값으로 반환해야 한다.

특수 공격 중 주 공격을 대체하는 경로는 target HP 감소가 0이어도 Legacy 실행 자체는 성공할 수 있다. 이 경우 일반 직격 `HitApplied`로 위조하지 않고 `StatusEffectApplied`처럼 0 피해를 허용하는 Shadow outcome으로 분류한다. 이번 단계는 특수 공격의 모든 추가 피해 대상을 개별 결과로 확장하지 않으며, 고정된 주 타겟 결과와 실행 분류만 먼저 감사한다.

### 3.2 Dispatch와 Result 확인 사이의 생명주기가 없다

현재 coordinator는 마지막 hit을 dispatch하면 Legacy reservation을 바로 폐기한다. 하지만 정식 결과는 Legacy writer 호출 뒤에만 만들 수 있다. reservation은 dispatch와 completion을 구분하고, 각 hit의 결과 확인이 끝난 뒤에 폐기해야 한다. 같은 프레임 재시도, 중복 completion, 다른 token·hit·sequence의 결과는 fail-closed 해야 한다.

### 3.3 Snapshot만으로는 개별 결과를 전달할 수 없다

Snapshot은 최신 상태이므로 빠르게 연속된 다중 hit 결과를 하나의 `NetworkVariable`에 덮어쓰면 중간 결과가 사라질 수 있다. 규칙대로 현재 상태 Snapshot과 개별 ImpactResult를 분리해야 한다. ImpactResult는 기본 Reliable `ClientRpc`로 한 건씩 전달하고, Client는 이를 게임플레이나 표현에 적용하지 않고 observer에만 전달한다.

### 3.4 결과 수신 분류가 필요하다

결과는 Snapshot보다 먼저 또는 뒤에 관측될 수 있고, 여러 회차의 코루틴이 겹치면 sequence 순서와 결과 완료 순서가 다를 수 있다. 따라서 Client 분류기는 단순 sequence 단조 증가를 요구하지 않는다. NetworkObject 수명과 AttackerInstanceId를 고정하고, 전체 `AttackResultKey` 기준으로 다음을 구분해야 한다.

- 처음 받은 유효 결과: Accepted
- 같은 키와 같은 payload: Duplicate
- 같은 키지만 결과·시각·Aim·피해량이 다른 payload: Conflict
- 비정상 enum, NaN/Infinity, 잘못된 HP/피해량: Invalid
- despawn된 수명 또는 다른 attacker instance: InstanceRetired

중복 기억은 제한된 용량으로 유지하되 오래된 항목을 제거하는 것이 새 결과를 거부하는 이유가 되어서는 안 된다.

## 4. 서버 권위와 단일 writer 경계

이번 구현에서 Shadow는 다음을 절대 수행하지 않는다.

- `TakeDamage`, HP NetworkVariable, 사망·피격 이벤트 쓰기
- 공격 타겟 선택 또는 Root 회전 쓰기
- Animator, 공격 VFX·SFX, HP 텍스트 실행
- 기존 Start/Change/Stop Combat RPC를 대체하거나 억제

Legacy writer가 먼저 결과를 확정하고, 그 반환값을 이용해 Shadow `AttackImpactResult`를 만든다. Shadow 생성·확인·복제·진단에 실패해도 Legacy 결과를 롤백하거나 다시 적용하지 않는다.

## 5. 이번 구현의 완료 의미

이번 단계가 통과하면 서버가 고정한 TargetId와 AimDirection, Legacy가 만든 주 타겟 결과를 동일 결과 키로 Host와 Client에서 감사할 수 있다. 이는 아직 시각 Impact와 실제 피해 표현 시점이 교정됐다는 뜻이 아니다. 다음 Phase 5에서 이 결과 키를 Presentation의 유일한 피격 표현 승인 키로 연결한 뒤에야 시각 타격과 피해 표현 시점을 전환할 수 있다.

25종·반대 역할·Legacy rollback 실기 게이트는 새 빌드에서 수행할 통합 검증으로 유지한다.
