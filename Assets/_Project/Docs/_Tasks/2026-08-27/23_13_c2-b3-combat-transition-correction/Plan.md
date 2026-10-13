# C2·B3 전투 전환 실기 FAIL 교정 Plan

이번 계획은 최신 실기에서 확인된 “전투 종료 뒤 같은 타일 중심으로 복귀하지 못하는 정지”와 “Legacy 공격보다 Shadow Impact가 늦어지는 시간 불일치”를 한 번의 교정 경계에서 해결한다. 논리 타일과 공간 Root, 관측 시각과 Legacy overshoot를 각각 분리해 다루며 서버 권위와 기존 gameplay writer는 그대로 유지한다.

> **기존 로직 보존·제거 정책:** Legacy 피해·HP·사망 이벤트·Combat RPC·VFX writer, 서버 Simulation Root 단일 writer, 기존 `WaitingRepath`/`Blocked` 경로는 제거하지 않는다. 같은 권위 타일의 이동 가능한 중심 복귀와 overshoot가 있는 공격 회차에만 분기를 보강한다. 기존 Attack 클립을 재사용하며 신규 에셋은 없다.

## 1. 적용 규칙

- `GameSystemRules_Units.md`의 `U-MOV-REPATH`, 이동 규칙 2·4, 전투 진입 규칙 10·11
- 같은 문서의 `U-COMBAT-PHASE`, `U-ATK-ALIGN`, `U-ATK-TIMELINE`, `U-IMPACT-TARGETLOCKED`
- `GameSystemRules_UnitCombatSynchronization.md`의 `NET-AUTH-001`, `NET-ACTION-STATE`, `NET-ACTION-SEQ`, `NET-ACTION-IDEMPOTENT`
- 같은 문서의 `NET-TIME-001`, `NET-FACING-001`, `NET-FACING-002`, `NET-PRESENT-001`, `NET-PRESENT-002`, `NET-CANCEL-*`

## 2. B3 도착-목표 Root 복귀 보강

수정 후보 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitMovementController.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs`

구현:

1. post-combat 복귀 평가에서 `finalTarget == UnitData.Position`인 상태를 별도 typed 결과로 구분한다.
2. 권위 타일이 walkable이고 Root가 중심 허용 오차 밖이면, 빈 A* 경로를 실패로 보지 않고 현재 pose에서 같은 타일 중심까지의 안전한 도착-목표 trajectory를 만든다.
3. 이동은 기존 Authoritative Locomotion과 corridor/sweep preflight, 270°/s 회전, 단일 Root writer를 그대로 사용한다.
4. 중심 허용 오차에 실제 도달한 뒤에만 `Completed`를 커밋한다. 위치 스냅, 비인접 `UnitData.Position` 재쓰기, 즉시 완료는 금지한다.
5. 타일이 차단됐거나 중심까지 안전한 trajectory가 없으면 기존 재탐색과 environment revision 기반 `Blocked` 생명주기를 유지한다.
6. 이 유효한 같은-타일 복귀만 `post-combat-no-safe-forward-center`로 분류하거나 영구 `navigationBlocked`에 남기지 않는다.

## 3. C2 Legacy overshoot 단일 시간 원천

수정 후보 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs`
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionSequencer.cs`

구현:

1. Legacy scheduler가 계산한 회차 overshoot를 해당 `LegacyAttackToken`과 함께 coordinator에 전달한다.
2. Shadow `CommitServerTime`은 `observedNow`를 유지한다. 실제 경계 시각으로 backdate하지 않아 reducer의 server time/revision 단조성을 보존한다.
3. 같은 회차에 `effectiveCooldown = max(0, cooldown - overshoot)`를 사용한다.
4. 각 hit에 `effectiveImpactOffset = max(0, hitTime - overshoot)`를 사용한다.
5. Shadow Begin/Commit/Impact와 due 판정은 위 유효 값만 공유하며 원본 hit offset이나 별도 observed-time 계산으로 다시 늦추지 않는다.
6. 다중 hit는 조정된 offset 순서와 같은 reservation의 `HitIndex`를 유지한다. offset이 0으로 수렴해도 정규 hit 순서와 중복 방지 계약을 통과해야 한다.

## 4. unavailable authorization/result 정합성

수정 후보 파일:

- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionContracts.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`

구현:

1. pre-impact에서 공격자 부재와 타겟 부재를 구분해 같은 reservation·hit의 명시적 authorization miss로 보존한다.
2. Legacy writer 관측의 `AttackerUnavailable` / `TargetUnavailable`을 동일 의미의 정규 result outcome으로 변환한다.
3. authorization miss 원인과 result outcome이 다르거나 token·hit·target이 다르면 `ConfirmImpactResult`를 통과시키지 않는다.
4. unavailable을 `HitApplied`, 일반 `Miss`, 0피해 성공으로 위조하지 않는다.
5. 실패 결과도 전체 `AttackResultKey`로 한 번만 확인·복제하며 Legacy 피해를 재호출하거나 롤백하지 않는다.

## 5. 최초 Align 선행 Attack 표현

수정 후보 파일:

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs`

구현:

1. 최초 사거리 진입의 `AlignToAttack`에서도 위치는 완전히 정지하고 서버 Root만 목표 방향으로 회전한다.
2. 표현은 기존 Attack 클립을 선행 재생하되 이를 커밋 전 provisional 상태로 구분한다.
3. provisional 상태에서는 Animation Event가 Impact, VFX, SFX, 트레이서, 피해 결과를 방출하거나 소비하지 못하게 suppression한다.
4. 5도 정렬 게이트를 통과해 새 `AttackSequenceId`가 커밋되면 실제 Attack cycle을 명시적으로 restart하고, 그 시작 경계부터 Windup·Impact 표현을 승인한다.
5. 타겟 교체 중 기존 Attack 표현 연속성은 유지하되 새 회차 커밋 전 suppression 계약은 동일하게 적용한다.
6. 신규 애니메이션·VFX 에셋과 클라이언트 Root writer는 추가하지 않는다.

## 6. RED→GREEN 자동 회귀

수정 파일:

- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

먼저 현재 코드에서 다음 회귀가 RED인지 확인하고 구현 뒤 GREEN으로 만든다.

> **[2026-08-28 자동 게이트 fixture 정정]** Unity 재실행으로 확정된 주된 RED 원인은 `ExpectedImpactCount=1`인 `UnitType.SpearMan` runtime manifest에 3-hit fixture를 넣어 `TryResolveRuntime`이 `runtime-impact-count-mismatch`와 `CommittedNow=false`로 정상 거부한 것이다. aggregate assertion이 선행 실패를 숨겼다. fixture를 지원되는 `FlameSpirit` 6-hit 입력과 reservation count 6으로 맞추고 runtime resolve → commit → reservation count → hit timing을 단계별 assertion으로 검증한다. 별도로 production의 `float` 시간 도메인에 맞지 않던 `0.05d`도 `float` 호출 형태로 바꾸고 동일 0 및 경계 양쪽을 검사한다. 이는 부수적인 fixture 비대표성 교정이며 최초·주된 FAIL 원인이 아니다. production, 멀티 실기 상태, 서버 권위와 Legacy writer 판정은 그대로 유지한다.

1. 같은 walkable 권위 타일에서 Root가 중심 밖인 post-combat 복귀는 경로 `null`이어도 중심까지 진행하고 완료한다.
2. 같은 상태에서 차단 타일 또는 실제 unsafe/unreachable은 기존 `Blocked`를 유지한다.
3. commit observed time은 단조 유지하면서 cooldown/hit offset에 overshoot가 정확히 한 번 반영된다.
4. 50ms overshoot 경계에서 정상 Legacy Impact가 `NotDue`가 되지 않는다.
5. 다중 hit의 조정 offset이 0 또는 동일해져도 정규 `HitIndex` 확인이 `OutOfOrder` 없이 완료된다.
6. attacker/target pre-impact 부재와 결과 outcome의 일치·불일치를 각각 PASS/fail-closed 한다.
7. 최초 Align에서 Attack 선행 표현이 활성화되지만 Impact 계열 신호는 0이고, 5도 커밋에서 실제 cycle이 한 번 restart된다.
8. 기존 A1~B3·C1·C2 전체 자체 검증이 함께 PASS한다.

## 7. 실패 시 안전 동작

- 같은 타일 중심 복귀 candidate가 안전하지 않으면 이동을 commit하지 않고 기존 재탐색/Blocked 규칙으로 돌아간다.
- overshoot나 hit timing 값이 유한하지 않거나 음수 계약을 위반하면 Shadow를 fail-closed 하되 Legacy gameplay 결과를 변경하지 않는다.
- result outcome이 authorization miss와 일치하지 않으면 결과 확인·복제를 거부하고 Legacy 피해를 재실행하지 않는다.
- provisional Attack 표현의 상태 결속이 없으면 Impact 계열 표현을 억제한다.
- observer·로그 실패는 gameplay writer를 대체하거나 롤백하지 않는다.

## 8. 완료 게이트

### 8.1 자동 게이트

1. Unity Runtime/Editor 컴파일 오류 0건.
2. `Run Unit Action Self Validation`의 기존 계약과 신규 B3/C2 회귀 전체 PASS.
3. Shadow 경로의 HP·Root·Animator 이벤트·기존 RPC/VFX gameplay write 추가 0건.
4. 문서 정합성 검사 0건.

### 8.2 멀티플레이 실기 게이트

새 Android 빌드와 같은 코드의 Editor를 사용해 Android Host / Editor Client 재현 역할과 반대 역할을 각각 검증한다.

- post-combat 같은 권위 타일 중심 복귀가 끝까지 진행되고 영구 정지 0건
- 차단 타일·실제 unreachable이 성공으로 오판된 사례 0건
- C2 `NotDue`·다중 hit `OutOfOrder` 0건
- 모든 `AttackerUnavailable`·`TargetUnavailable`이 같은 authorization miss 원인과 정규 결과 outcome으로 확인되고 conflict/invalid 0건
- 최초 Align 중 위치 변화 0, 선행 Attack 표현 활성, 커밋 전 Impact/VFX/SFX/피해 소비 0
- 5도 커밋에서 실제 Attack cycle restart 1회와 이후 정규 결과 키 결속 PASS
- Host/Client 동일 `sharedSessionKey`, terminal drop/preflight 0, ROOT 양쪽 PASS

자동 게이트만 통과한 상태는 완료가 아니다. 위 역할교대 실기가 통과해야 B3 전투 전환과 C2 Shadow 결과 동기화를 PASS로 판정하고 다음 Presentation 단계로 진행한다.

## 9. 변경 금지 범위

- 서버 권위와 Legacy 피해·HP·사망 gameplay writer
- 이동 속도, 270°/s, 10°/15° 이동 정렬, 5°/8° 공격 정렬 수치
- 피해량·방어력·특수 공격 수치와 타겟 선택 우선순위
- 신규 애니메이션·VFX·SFX 에셋
- 실제 projectile 궤적·착탄 시스템
- 기존 권위 경로 제거 또는 경기 중 pipeline 혼합 전환

Testcase와 별도 QA 문서는 사용자가 요청하지 않았으므로 생성하지 않는다.

## 10. 2026-08-28 실기 FAIL에 따른 후속 교정 계획

최신 Editor Host / Android Client 실기는 자동 게이트가 잡지 못한 세 결함을 확인했으므로 이 작업은 완료가 아니다.

1. Legacy 서버 피해 경계가 Shadow와 동일한 권위 pose에서 타겟 생존·유효성·Impact 사거리·8° 방향을 재검증하도록 한다. Shadow가 gameplay writer가 되거나 Legacy 피해를 사후 롤백하는 방식은 사용하지 않는다.
2. Legacy scheduler와 Shadow가 같은 token/hit의 유효 Impact 시각을 공유하도록 `Applied + NotDue` 5건의 시간 원천을 최소 재현하고 교정한다. unavailable completion도 수락된 authorization과 같은 생명주기로 닫는다.
3. 정규 `Miss`를 `Cancelled`로 오판하는 observer 결과 분류를 수정하고, `AuthorizedMiss` 상세에 `MissReason`을 남겨 사거리·방향·타겟 부재를 로그만으로 구분한다.
4. 회귀는 최소한 `CombatConditionFailed → Legacy 미적용`, `TargetUnavailable → Miss`, `AttackerUnavailable → Miss`, due 경계의 Legacy/Shadow 동시 수락, 실제 타겟 ID 불변을 각각 고정한다.
5. Unity 전체 `[UAS-DIAG]` PASS 뒤 새 Android 빌드로 같은 역할과 역할 교대를 다시 검증한다. 완료 기준은 Host `targetMismatches=0`, `resultFailures=0`, `correlationFailures=0`, Client reject 0, gameplay writer 1개 유지다.

## 11. 2026-08-29 실기 FAIL 후 교정 계획

쉽게 말하면 현재 공격은 “판정할 정보를 못 얻었으니 피해는 주지 않는다”까지는 안전하게 동작하지만, 그 공격을 왜 빗나갔는지 정식 결과로 끝내지 못한다. 이동은 잘못된 경로를 적용하지 않는 데는 성공했지만, 그 경로가 정말 막힌 것인지 경로 계산이 잘못된 것인지 알 수 있는 증거가 부족하다. 다음 교정은 이 두 상태를 무조건 성공으로 바꾸는 것이 아니라, **정상적인 종료와 실제 오류를 구분할 수 있게 만든 뒤 잘못된 경로만 고치는 것**이다.

### 11.1 공격 pose 획득 결과를 typed 상태로 분리

근거 규칙: `GameSystemRules_Units.md`의 `U-IMPACT-TARGETLOCKED`, `GameSystemRules_UnitCombatSynchronization.md`의 `NET-ACTION-SEQ`, `NET-ACTION-IDEMPOTENT`, `NET-CANCEL-003`, `NET-CANCEL-004`.

1. Impact 직전 pose 획득을 단순 성공/실패로 버리지 않고 최소 `Captured`, `AttackerUnavailable`, `TargetUnavailable`, `InvalidPose`, `InfrastructureFailure`처럼 원인을 보존하는 typed 결과로 만든다.
2. 이미 사망·제거된 공격자나 커밋 타겟은 예상 가능한 전투 생명주기다. 해당 reservation·hit를 각각 `AttackerUnavailable` 또는 `TargetUnavailable`의 canonical `Miss`로 한 번만 종료한다.
3. 살아 있고 유효한 타겟인데 pose를 얻지 못한 경우는 일반 Miss로 숨기지 않는다. fail-closed 하면서 단계, 공격자·타겟 identity, token·sequence·hit, pose 제공자 상태를 bounded detail로 기록한다.
4. 유한하지 않은 pose나 identity 불일치는 피해 0을 유지하되 명시적인 진단 failure로 남긴다. 피해 적용이나 다른 타겟 전환으로 보정하지 않는다.

### 11.2 authorization과 completion을 1:1로 닫기

근거 규칙: `GameSystemRules_Units.md`의 `U-ATK-TIMELINE`, `U-IMPACT-TARGETLOCKED`, `GameSystemRules_UnitCombatSynchronization.md`의 `NET-TIME-001`, `NET-ACTION-IDEMPOTENT`.

1. 같은 token·sequence·hit의 completion은 수락된 authorization 또는 위의 명시적 terminal miss 중 정확히 하나와 결합돼야 한다.
2. `AuthorizationUnavailable + NotDue`처럼 이유가 섞인 상태를 최종 결과로 사용하지 않는다. 시간이 이르다면 due 상태로, 생명주기 종료라면 canonical miss로, 내부 자료 실패라면 fail-closed diagnostic으로 분리한다.
3. completion이 먼저 도착하거나 authorization 생성이 실패해도 같은 frame 재시도나 피해 재호출은 하지 않는다. bounded 보관 뒤 정규 종료하거나 원인이 명확한 terminal failure로 닫는다.
4. observer는 `accepted authorization + completion`과 `terminal miss + completion`의 1:1, 중복 0, 미종료 0을 terminal에서 별도 집계한다.

### 11.3 B3 invalid path에 경로 판정 증거 추가

근거 규칙: `GameSystemRules_Units.md`의 `U-MOV-REPATH`, 이동 규칙 2·4, `GameSystemRules_UnitCombatSynchronization.md`의 `NET-FACING-001`.

1. `RequestMove`가 empty/invalid를 반환하면 source, objective, start/final tile, current Root, walkability environment revision, pathfinder 결과(`Unreachable`/`EmptySameTile`/`InvalidStart`/`InvalidGoal`/`ProviderFailure`)를 typed evidence로 남긴다.
2. post-combat와 일반 A* 모두 이전·현재·후보 path의 유효성뿐 아니라 “서버 경로 그래프상 우회로 존재 여부”를 같은 판정 경계에서 기록한다. 사용자가 확인한 reachable 멈춤을 재현할 때는 **권위 start tile·Simulation Root와 최신 walkability environment revision**에서 재탐색해, 오래된 논리 시작점이나 cache를 답으로 사용하지 않는다.
3. 실제 우회로가 없으면 기존 `WaitingRepath`/`Blocked`를 유지한다. 이 상태를 성공이나 이동 완료로 바꾸지 않는다. 반대로 우회로 존재 판정인데도 empty/invalid path가 반환되면 정상 차단으로 닫지 않고 명시적인 구현 실패로 처리한다.
4. 최소 관찰 사례는 reachable인데 `RejectedInvalidPath`가 발생한 B3 구현 결함으로 확정한다. 출발점 정규화, cache/environment revision 무효화, pathfinder 반환 계약 가운데 실제 원인을 typed evidence로 특정한 뒤 그 지점만 교정한다. 단순히 fail-closed 검사를 제거하거나 unsafe candidate를 commit하지 않는다. 네 adapter failure 전부가 같은 원인인지는 추가 증거 전까지 확정하지 않는다.

### 11.4 RED→GREEN 자동 회귀와 실기 완료 조건

1. 사망·제거된 공격자/타겟은 피해 0과 canonical miss로 정확히 한 번 종료된다.
2. 살아 있는 유효 타겟의 pose 획득 실패는 단계·원인이 남고 결과가 성공으로 위조되지 않는다.
3. due 경계, authorization, completion은 token·sequence·hit별 1:1이며 `AuthorizationUnavailable + NotDue` 최종 상태가 0이다.
4. B3는 실제 unreachable과 잘못된 invalid path를 분리한다. 회귀는 권위 start tile·Simulation Root와 최신 environment revision에서 유효 우회로가 존재하는 지형을 고정하고, 이때 empty/invalid가 반환되면 반드시 FAIL하며 같은 objective를 유지한 채 올바른 우회 경로로 이동을 재개해야 PASS한다.
5. Unity 전체 self-validation PASS 뒤 새 Android 빌드로 Editor Host / Android Client와 역할 교대를 검증한다.
6. 완료 기준은 Host C2 `resultFailures=0`, `targetMismatches=0`, `correlationFailures=0`, 미종료 authorization/completion 0, Client reject 0, B3 adapter failure 0, drop/preflight 0이다.
7. 25종 커버리지는 누적 실기로 별도 충족한다. 이번 8/25 단일 경기에서 사거리 밖 피해 재발이 없었다는 사실만으로 완전 해소를 선언하지 않는다.
8. 양쪽 ROOT local PASS는 공식 교차감사 대신 사용하지 않는다. 같은 경기 파일의 `Unit Root Pose Cross Audit`가 별도 PASS해야 공식 교차 결과로 기록한다.
