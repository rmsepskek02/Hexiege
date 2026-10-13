# 공격 진입 원자적 인계 회귀 — 교정 계획

이번 교정의 목표는 “서버가 공격 사거리 진입을 확정한 순간”을 하나의 경계로 만드는 것이다. 그 경계에서 서버 Root의 위치 정지, 공격 방향 회전 소유권, Host의 provisional Attack 표현을 함께 시작하고, 원격 Client에는 같은 revision의 표현 명령을 전달한다. 실제 공격과 피해는 지금처럼 서버가 정지·사거리·5° 방향 조건을 통과한 뒤에만 승인한다.

> 이번 작업은 C3 확정 결과 표현을 다시 설계하지 않는다. 공격 결과가 생기기 전 단계인 B3 이동→C1 공격 진입의 통합 경계를 교정한다. 서버 권위, C2 피해 writer, C3 단일 결과 표현은 유지한다.

## 1. 원자적 서버 진입 경계

근거: `GameSystemRules_Units.md` 이동 규칙 8, `U-COMBAT-PHASE`, `NET-PRESENT-001`.

- `UnitView`가 공격 사거리 진입을 확정해 `TargetAcquirePriority + NoIntent`를 commit하는 시점에 Action 회전 소유권과 정확한 후보 타겟을 설치한다.
- 같은 동기 경계에서 Host Animator를 provisional Attack으로 정확히 한 번 전환한다.
- Host의 최초 Attack 표현은 `ClientRpc` 왕복이나 `UnitAnimState` callback 도착 순서에 의존하지 않는다.
- 이 경계에서는 공격 scope가 없는 `Suppressed` 표현만 열며, 쿨다운·회차 ID·피해 예약·VFX/SFX/타격 결과는 시작하지 않는다.

## 2. Host와 원격 Client 표현 전달 분리

근거: `NET-PRESENT-001`, `NET-PRESENT-002`, 규칙 22 값 기반 동기화.

- 서버 gameplay 경계는 Host의 로컬 표현을 직접 적용한다.
- `StartCombatClientRpc`는 원격 Client에 동일 target/revision/impact mode/scope를 전달하는 역할로 제한한다.
- Host가 RPC를 다시 수신해 중복 CrossFade하거나, 이미 닫힌 Action 소유권 때문에 정상 시작을 버리는 구조를 제거한다.
- 원격 Client는 계속 원자 payload에서 target과 impact suppression/scope를 먼저 적용한 뒤 Animator를 전환한다.
- 늦은 참가를 위한 `UnitAnimState` 값은 현재 표현 수준을 보존하되, 공격 scope나 타겟을 추측하는 권한으로 사용하지 않는다.

## 3. revision 기반 수명과 종료 순서

근거: `NET-ACTION-*`, `NET-PRESENT-002`, `NET-CANCEL-*`.

- provisional start, committed scope, target change, stop이 같은 유닛에서 단조 증가 revision으로 분류되도록 유지한다.
- 늦은 start/change가 종료 뒤 행동을 되살리는 것은 계속 거부한다.
- 다만 유효한 로컬 최초 start는 네트워크 지연 대상이 아니므로 “stale event 거부” 통계에 들어가지 않게 한다.
- 사거리 이탈·타겟 상실 시 Host 로컬 종료를 먼저 확정하고 원격 Client에 종료를 전달한다.
- 재진입은 이전 provisional epoch와 impact lease를 재사용하지 않고 새 수명으로 시작한다.

## 4. 진단기 교정

근거: 실제 실기 `ignoredServerTargetEvents=1103`을 실패로 보지 못한 회귀.

- `ignoredServerTargetEvents`를 start/change/stop과 기대된 stale/예상 밖 무시로 분리한다.
- Host의 유효 start가 무시되거나 `NoIntent` 뒤 정해진 다음 표현 상태가 Attack이 아니면 causal FAIL을 남긴다.
- 정상적인 로컬 종료 뒤 되돌아온 오래된 stop/start는 별도 `expectedStale`로 집계해 실제 결함과 섞지 않는다.
- 공격 진입 이상이 발생한 유닛은 unit ID, 타입, target ID, action owner, movement phase, animation state, presentation revision, gate 결과, commit/result 유무를 하나의 bounded lifecycle 증거로 남긴다.
- terminal은 예상 밖 start 무시, 공격 handoff 불완결, 장기 `NoIntent + Walk`, revision 수명 충돌 중 하나라도 있으면 FAIL한다.

## 5. 자동 회귀 검증 보강

근거: 기존 `ValidateB3RotationWriterOwnership`이 정책 단위만 검사한 공백.

다음 production과 같은 순서를 순수 fixture가 아니라 공통 adapter 경계에서 실행한다.

1. `Move → TargetAcquirePriority + NoIntent → Action 소유권 → provisional Attack`이 한 동기 호출 안에서 완료되고 Held/Idle 0회, Attack 시작 1회인지 확인한다.
2. Host 로컬 적용 뒤 같은 start RPC가 돌아와도 Attack clip restart와 revision 중복 수락이 0인지 확인한다.
3. 진입 직후 사거리 이탈 → 늦은 start → 재진입 순서에서 오래된 start는 거부되고 새 start는 정확히 한 번 수락되는지 확인한다.
4. 불정령 2단계 형태의 작은 사거리에서 Misaligned → 회전 수렴 → Ready/commit 또는 명시적 target-loss 종료 중 하나로 유한 시간 안에 끝나는지 확인한다.
5. `NoIntent + Walk advancing` 또는 Root 정지 중 Walk 지속 상태가 한 frame이라도 발생하면 실패한다.
6. commit 전 marker 결과 0, commit 후 정확한 scope만 소비하는 기존 C1/C2/C3 회귀를 함께 유지한다.

## 6. 변경 예정 파일

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
  - 서버 공격 진입/종료의 로컬 표현 adapter와 행동 소유권 경계를 원자화한다.
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
  - Host 직접 적용과 원격 Client 전송을 분리하고 provisional/commit/stop revision 수명을 정리한다.
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`
  - 필요할 경우 Host 중복 적용을 막는 값 상태 역할을 명확히 한다. 실제 변경 필요성은 구현 중 호출 경계 대조 후 결정한다.
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs`
  - 로컬 gameplay handoff와 원격 stale event 분류를 순수 정책으로 분리한다.
- `Assets/_Project/Scripts/Infrastructure/Network/UnitMovementAuthorityObserver.cs`
  - start/change/stop 및 expected/unexpected 분리와 terminal FAIL 조건을 추가한다.
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
  - Host 실제 순서, 이탈·지연·재진입, EmberSpirit 형태의 유한 수렴 회귀를 추가한다.

기존 C2/C3 파일은 회귀 검증을 위해 읽지만, 원인 증거 없이 수정하지 않는다.

## 7. 검증 순서와 완료 기준

1. Unity 컴파일 오류 0.
2. `Hexiege/Combat/Run Unit Action Self Validation` PASS.
3. PASS일 때만 `Build And Run`을 시작하며, 빌드 완료 확인은 사용자가 진행한다.
4. 새 Android Host / Editor Client 같은 경기에서 Assault와 EmberSpirit을 반드시 포함한다.
5. 반대 역할 Editor Host / Android Client에서도 같은 전환을 확인한다.
6. 양측에서 공격 진입 Held/Idle 0, 장기 `NoIntent + Walk` 0, 예상 밖 start 무시 0, 회차/타겟/결과 불일치 0이어야 한다.
7. C3는 기존 기준 `expectedVisual == presentationEmits`, pending·duplicate·transport failure·gameplayWrites 0을 계속 만족해야 한다.

자동 검증 PASS는 구현 완료가 아니라 빌드 허용 조건이다. 최종 완료는 두 역할의 멀티플레이 실기에서 공격 직전 끊김과 EmberSpirit Walk 고착이 재현되지 않고 terminal 계약도 모두 통과한 뒤 판정한다.

## 8. 구현 결과 (2026-09-09)

- `UnitView`에 서버 Action 소유권 준비 여부, Host 로컬 Attack 표현 적용, 서버 로컬 타겟 교체의 명시적 adapter를 추가했다.
- `NetworkCombatController`는 provisional/commit Attack을 `서버 Action 준비 확인 → Host 로컬 적용 → 상태 기록 → 원격 ClientRpc` 순서로 처리한다.
- Host는 Start/Change/Suppress/Stop ClientRpc를 다시 소비하지 않는다. 타겟 교체와 suppression은 서버 로컬에 먼저 반영하고, RPC는 순수 원격 Client만 처리한다.
- 위치·방향만 우연히 공격 조건을 만족해도 서버 Action 소유권이 없으면 Legacy 공격 시작 gate를 통과하지 못한다.
- 일반 Tick이 이동 코루틴보다 먼저 후보를 본 경우에는 상태를 선점하지 않고 defer한다. 실제 `OnUnitEnteredCombat` 원자 경계의 적용 실패는 `combat-presentation-handoff-failed` causal evidence와 이동 observer terminal FAIL로 남긴다.
- 이동 observer schema를 `b3-movement-authority-v11`로 올리고 `combatPresentationHandoffFailures`를 terminal에 추가했다.
- Unity 컴파일 오류 0건과 `Run Unit Action Self Validation` PASS를 확인했다. 이 결과는 빌드 허용 gate이며 멀티플레이 실기 완료 판정은 아직 아니다.

## 9. v11 실기 후 보완 — 후보 타겟과 공개 타겟 분리

v11 실기에서 원자적 Host 진입과 C2/C3는 정상 수렴했지만, Action 소유권 전에 `ChangeTarget`을 공개하려 한 6건이 확인됐다. 다음 보완은 C2/C3를 변경하지 않고 공격 진입의 타겟 공개 경계만 교정한다.

1. `_unitCombatTargets`는 서버가 탐색한 변경 가능한 후보/잠금 상태로 사용한다. 이 값의 변경만으로 Host View나 원격 Client의 표시 타겟을 바꾸지 않는다.
2. Action 회전 소유권이 아직 준비되지 않은 후보는 `staged`로 보류한다. 이 상태에서는 `TryApplyServerCombatTarget`과 `ChangeTargetClientRpc`를 호출하지 않는다.
3. 최초 진입 또는 비활성 표현에서 새 후보를 선택한 경우, `BeginProvisionalAttackPresentation`의 원자 payload가 정확한 target과 suppression을 함께 Host에 적용하고 원격 Client에 전달한다. 같은 target을 별도 Change RPC로 중복 발행하지 않는다.
4. 이미 Attack 표현을 유지하는 전투 연계에서 새 타겟으로 바뀌면 기존 impact lease를 먼저 닫는다. Action 소유권이 준비된 경우에만 새 target을 Host에 적용한 뒤 Change RPC를 보낸다. 준비되지 않았으면 pending/revision을 선점하지 않고 다음 정상 진입 경계까지 보류한다.
5. Action 준비 전 보류는 `deferredCombatTargetChanges` 정상 진단으로 집계한다. Action 준비 후 실제 target 적용 실패만 `combatPresentationHandoffFailures`로 남겨 terminal FAIL을 유지한다.
6. 이동 observer schema를 v12로 올리고, 자동 검증은 `not-ready → stage only/no RPC`, `ready → publish once`, `provisional Start가 target을 원자 전달`, 기존 C2/C3 불변식을 함께 확인한다.

### 완료 gate

- Unity 컴파일 오류 0.
- `Run Unit Action Self Validation` PASS.
- PASS일 때만 Build And Run 시작.
- 다음 같은 경기에서 `combatPresentationHandoffFailures=0`, `stationaryWalkViolations=0`, C2/C3 기존 수렴 조건 유지.
- 9종 표본의 현재 성공은 보존하되, 전체 25종 완료 판정은 별도 실기 coverage를 채운 뒤에만 한다.

## 10. v12 구현 상태 (2026-09-10)

- 규칙 원본에 서버 후보와 공개 타겟의 상태 경계를 명시했다.
- 순수 `CombatTargetPublicationDecision` 정책으로 `StageCandidateOnly`, `PublishWithProvisionalStart`, `PublishTargetChange`를 분리했다.
- `TickCombat`의 target-change는 Action 준비 전에는 후보만 보존하고 이전 impact lease만 닫는다. presentation pending/revision과 Change RPC를 선점하지 않는다.
- 최초/재진입의 `OnUnitEnteredCombat`은 별도 Change를 제거하고 provisional Start payload 하나로 Host target·suppression·Animator와 원격 target을 전달한다.
- 실제 Action-owned target 적용에 성공한 경우에만 Change RPC를 발행하며, 실패하면 RPC를 보내지 않고 기존 terminal FAIL을 유지한다.
- observer schema는 `b3-movement-authority-v12`이며 정상 보류를 `deferredCombatTargetChanges`로 별도 집계한다.
- 문서 정합성 검사와 Unity 생성 rsp 기반 Runtime/Editor C# 정적 컴파일은 코드 오류 0건이다. 소스 생성기 버전 경고 3건은 Unity 외부 직접 컴파일 환경의 기존 경고이며 이번 코드 오류가 아니다.
- Unity 메뉴 self-validation과 Build And Run은 아직 실행 전이다. self-validation PASS 전에는 빌드를 시작하지 않는다.

## 11. v14 구현·실기 판정 (2026-09-13)

- v14는 PostCombatResume에서 기존 command를 유지한 채 즉시 pursuit로 재진입하고, 회복성 재탐색 전에 현재 타겟 경로를 다시 확인하도록 교정했다.
- 이동 observer를 `b3-movement-authority-v14`로 올리고 순수 Client의 Root 정지→Attack 표시 frame 순서 및 서버 handoff 실패 episode를 추가했다.
- Unity Runtime/Editor 정적 컴파일 오류 0건, `[UAS-DIAG]`/C2/C3 self-validation PASS 뒤 Android Build And Run과 같은 경기 실기를 완료했다.
- 실기에서 기존 약 1초 command 재발급은 0건으로 사라졌지만, Host `client-visible-entry-order` handoff 실패 70건과 Client 순서 위반 55건이 남았다.
- Client 55건은 command·segment scope 없이 미래 Attack까지 누적하는 관측기 오탐을 포함하므로 실제 멈춤 수로 확정하지 않는다.
- C3 결과는 양측 `expectedVisual=1705`, `presentationEmits=1705`, pending·중복·전송 실패 0으로 수렴했지만, 최종 대미지 폰트 좌표와 피해자 표현 좌표의 차이는 관측하지 못했다.
- 따라서 이 작업의 기존 command 재발급 교정은 확인됐으나 전체 공격 진입/표현 판정은 **부분 성공·OPEN**이다. 다음 task에서 멱등 handoff ACK, 정확한 Client 진입 scope, 대미지 폰트 공간 증거를 교정한다.
