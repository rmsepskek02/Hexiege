# 공격 진입 타겟 전달 경계 교정 — 구현 계획

이번 작업은 근거리 유닛이 공격 직전에 멈추는 직접 원인 한 곳만 고친다. 이동 중 이미 선택한 적의 ID와 종류를 공격 시작 담당 코드까지 그대로 전달하고, 그 경계에서 같은 적을 다시 찾는 동작을 없앤다. 정상인 피해 계산과 대미지 표시는 그대로 유지하며, 자동 검증을 늘리는 방식으로 문제를 우회하지 않는다.

> **현재 상태: PASS / CLOSED · production seam 코드·self-validation·빌드·동일 범위 사용자 실기 완료 (2026-09-13)**
>
> 계획한 세 코드 파일의 구현과 이벤트 계약 전수 확인, Android Runtime·Editor Runtime·Editor Assembly 정적 컴파일, Unity self-validation과 Build And Run 실행이 완료됐다. 사용자는 이전 실패와 동일한 유닛 범위로 멀티플레이 실기를 수행했으며, 같은 범위에서 근거리 6종의 공격 commit과 `combatPresentationHandoffFailures=0`이 확인됐다. 아래 7절의 미완료 표기는 당시 진행 상태를 보존한 이력이고, 현재 최종 판정은 8절의 **PASS / CLOSED**를 따른다.

## 1. 변경 범위와 순서

### 1-1. 공격 진입 이벤트에 불변 typed payload 도입

수정 파일: `Assets/_Project/Scripts/Application/Events/GameEvents.cs`

- `UnitId`, `TargetId`, `TargetIsUnit`을 함께 보유하는 `readonly` typed payload를 정의한다.
- `OnUnitEnteredCombat`을 `Subject<int>`에서 해당 payload를 전달하는 이벤트로 변경한다.
- payload는 공격 진입 순간에 확정된 값만 담고, 수신 측이 타겟을 다시 해석하거나 재검색해야 하는 불완전 상태를 허용하지 않는다.
- 기존 이벤트의 역할인 동기 호출과 ACK 순서는 유지한다.

규칙 근거:

- `GameSystemRules_Units.md` `U-TARGET-COMMIT`: 최초 진입의 provisional Start payload가 후보 타겟을 같은 원자 경계에서 전달해야 한다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`: 최초/재진입 payload가 target과 provisional 표현 상태를 Host 적용과 원격 Client 전달에 연결해야 한다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-ACTION-IDEMPOTENT`: 중복 수신은 동일 상태를 안전하게 재확인해야 하며 다른 타겟으로 바뀌면 안 된다.

### 1-2. `UnitView`의 두 발행 지점에서 같은 타겟 전달

수정 파일: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

- `TryBeginServerAttackEntryBeforeRootStop`은 이미 인자로 받은 `targetId`와 `targetIsUnit`을 `unitId`와 함께 typed payload로 발행한다.
- `EnterCombatLoopV3`의 별도 재진입 발행점도 typed payload로 변경한다.
- 재진입 발행점은 현재 서버 Action이 소유하고 공개한 동일 타겟을 사용한다. 이 값을 만들기 위해 새 타겟 검색을 추가하지 않는다.
- callback 발행 전 Action 회전 소유권 획득, 동기 이벤트 처리 뒤 ACK 확인, ACK 실패 시 이동 소유권 복귀라는 기존 순서는 보존한다.
- ACK가 확인되기 전에는 후보 위치, 이동 reducer, Root 정지 상태를 commit하지 않는다.

규칙 근거:

- `GameSystemRules_Units.md` `U-MOV-ALIGN` 8~9: 타겟 획득 진입은 일반 Held가 아니라 이동 표현에서 provisional Attack으로 직접 넘기는 Action handoff다.
- `GameSystemRules_Units.md` `U-COMBAT-PHASE`: `AlignToAttack`은 위치 정지와 공격 방향 소유권이 분리된 서버 권위 단계다.
- `GameSystemRules_Units.md` `U-TARGET-COMMIT`: 후보 발견과 공개 타겟은 구분하되 최초 진입 경계에서는 같은 후보 타겟을 원자 전달해야 한다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-ROOT-001`: 사거리와 타겟 판정은 서버 Simulation Root 권위로 유지한다.

### 1-3. 공격 진입 핸들러는 payload 타겟을 직접 사용

수정 파일: `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

- `OnUnitEnteredCombatHandler`가 typed payload를 받도록 시그니처를 변경한다.
- payload의 `UnitId`로 공격자를 확인하고, `TargetId`와 `TargetIsUnit`을 provisional Start의 타겟으로 직접 사용한다.
- 이 핸들러 안의 `TryFindTarget`과 `FindNearestEnemy` 재검색만 제거한다.
- 전달된 타겟이 사망·소멸했거나 더 이상 유효하지 않으면 다른 타겟으로 대체하지 않는다. ACK하지 않고 반환하여 Root 위치 commit을 보류하고, 다음 정상 tick에서 기존 탐색 흐름이 새 타겟을 다시 획득하게 한다.
- Host provisional 적용과 원격 Client RPC enqueue가 모두 완료된 경우에만 기존 handoff 확인을 호출한다.

규칙 근거:

- `GameSystemRules_Units.md` `U-TARGET-COMMIT`: 무효화된 기존 타겟을 다른 대상으로 이전하지 않고 새 타겟은 새 권위 전환에서만 사용한다.
- `GameSystemRules_Units.md` `U-COMBAT-PHASE`: 공격 진입 실패를 이동 완료나 공격 commit으로 위조하지 않는다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`: Host 적용 실패 뒤 RPC만 보내거나 Start와 target-change를 중복 발행하지 않는다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-ACTION-IDEMPOTENT`: 동일 공격 진입의 중복 처리는 상태를 되돌리거나 다른 타겟으로 치환하지 않는다.

### 1-4. 기존 정상 타겟 변경 경계 보존

- `TickCombat`의 정상 새 타겟 탐색은 유지한다.
- Action 준비 뒤 발행되는 명시적 `ChangeTarget` 경계는 유지한다.
- 최초/재진입 Start와 전투 중 target-change를 합치지 않는다.
- 이번 변경으로 검색 메서드 자체를 삭제하거나 다른 호출자의 탐색 정책을 바꾸지 않는다. 제거 대상은 `OnUnitEnteredCombatHandler` 내부의 재검색 호출뿐이다.

규칙 근거:

- `GameSystemRules_Units.md` `U-TARGET-COMMIT`: Acquire 후보, 최초 Start와 전투 연계 target-change는 서로 다른 상태 전이다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`: 최초/재진입은 단일 Start payload, 활성 전투의 교체는 별도 target-change를 사용한다.

## 2. 변경 파일

1. `Assets/_Project/Scripts/Application/Events/GameEvents.cs`
2. `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
3. `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

위 세 파일 외 변경이 필요해지면 구현을 멈추고 원인과 필요성을 다시 검토한다. 관련 없는 리팩터링은 하지 않는다.

## 3. 명시적 비수정 범위

- 새 테스트, 회귀 검증기 또는 진단 추가 금지
- `UnitMovementAuthorityObserver`, `UnitAttackShadowObserver`와 관측 schema 변경 금지
- 피해 계산과 C2 gameplay/result writer 변경 금지
- C3 결과·표현 coordinator와 대미지 텍스트 변경 금지
- 피격 VFX, 공격 VFX/SFX, 공격 프로필과 유닛 공격 에셋 변경 금지
- 허용치 상향, 실패 분모 제외, handoff 실패 무시 금지

근거: 최신 같은 경기에서 서버 결과와 Client 수락 2,298건, 필수 표현 2,036/2,036건, Host/Client 대미지 위치 mismatch 0건으로 이 경로들은 정상 증거가 있다. 원인이 확인되지 않은 정상 축을 함께 수정하면 교정 범위를 다시 넓히고 회귀 위험을 만든다.

## 4. 구현 시 확인할 위험

1. `TryBeginServerAttackEntryBeforeRootStop`만 바꾸고 `EnterCombatLoopV3` 발행점을 빠뜨리면 이벤트 계약이 둘로 갈라진다. 두 지점을 함께 변경한다.
2. 재진입 발행점을 고치면서 `FindNearestEnemy`를 새로 호출하면 같은 결함을 다른 위치에 다시 만든다. 현재 Action/public target만 전달한다.
3. invalid payload 타겟을 다른 적으로 자동 대체하면 기존 공격 진입이 다른 타겟으로 이전된다. no ACK로 닫고 다음 tick의 정상 획득에 맡긴다.
4. 최초 Start와 `ChangeTarget`을 합치면 Action 준비 전 공개 또는 중복 RPC가 재발할 수 있다. 기존 경계를 보존한다.
5. 이벤트 타입 변경 뒤 구독 등록과 해제가 모두 새 handler 시그니처를 가리키는지 확인한다.

## 5. 빌드 전 안전 확인

구현이 계획한 세 파일에만 국한됐는지 검토한 뒤 다음 기존 확인만 수행한다.

1. Unity 생성 참조와 같은 세대의 Runtime/Editor C# 정적 컴파일 오류가 0인지 확인한다.
2. 기존 `Hexiege → Combat → Run Unit Action Self Validation`이 PASS인지 확인한다.
3. 두 안전 확인이 통과한 경우에만 Android `Build And Run`을 실행한다.
4. 빌드는 **실행만** 하고 완료 감시와 완료 확인은 사용자가 수행한다.

새 검증기, Observer 필드, schema 또는 테스트 코드는 추가하지 않는다. 위 안전 확인의 PASS는 빌드를 허용하는 조건이지 작업 완료 판정이 아니다.

## 6. 최종 완료 조건

최종 판정은 사용자의 새 멀티플레이 실기로만 내린다.

- 근거리 유닛이 후보 위치 진입 후 멈추지 않고 Root 정지, 방향 정렬, 공격 commit과 실제 피해까지 진행한다.
- Host `combatPresentationHandoffFailures=0`이다. 1건이라도 남으면 FAIL이다.
- 공격하지 않는 근거리 유닛이 한 종이라도 관찰되면 FAIL이다.
- 현재 정상인 피해 결과 전달과 대미지 텍스트 위치가 그대로 유지된다.
- 사용자 실기 전까지 Task 상태는 **FAIL / OPEN**이며 완료·PASS로 변경하지 않는다.

## 7. 구현 및 gate 진행 결과

계획한 production seam 교정은 세 파일에 적용됐다. 이 결과는 코드 구현 단계의 완료를 뜻하지만, Unity 검증·빌드·사용자 실기가 남아 있으므로 Task 전체는 완료되지 않았다.

### 7-1. 구현 완료 항목

- `GameEvents.cs`: `UnitId`, `TargetId`, `TargetIsUnit`을 담는 불변 `UnitEnteredCombatEvent` typed payload를 도입했다.
- `UnitView.cs`: 최초 공격 진입과 재진입 두 발행점 모두 이미 확정된 타겟을 payload로 전달하도록 변경했다.
- `NetworkCombatController.cs`: handler가 payload 타겟을 직접 사용하도록 변경하고, 해당 공격 진입 경계의 타겟 재검색을 제거했다.
- 전달된 타겟이 유효하지 않을 때 다른 타겟으로 대체하거나 ACK하지 않도록 했다.
- 기존 `TickCombat` 탐색과 `ChangeTarget` 경계는 보존했다.

### 7-2. 범위 준수 확인

실제 코드 변경은 다음 세 파일에 한정됐다.

1. `Assets/_Project/Scripts/Application/Events/GameEvents.cs`
2. `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
3. `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

검증기, Observer, schema, 피해 계산, 대미지 텍스트와 VFX는 수정하지 않았다.

### 7-3. 정적 확인 결과

- 모든 `OnUnitEnteredCombat` 발행점과 구독점을 전수 확인했다.
- 기존 `Subject<int>`, 정수형 handler와 유닛 ID 단독 발행 잔존은 0건이다.
- Unity Roslyn 응답 파일 기준 Android Runtime 정적 컴파일: exit 0 / error 0
- Unity Roslyn 응답 파일 기준 Editor Runtime 정적 컴파일: exit 0 / error 0
- Unity Roslyn 응답 파일 기준 Editor Assembly 정적 컴파일: exit 0 / error 0
- 기존 경고만 남아 있으며 이번 변경으로 확인된 정적 컴파일 오류는 없다.

### 7-4. Unity 실행 불가로 남은 gate

Windows 조작 연결 상태를 두 차례 확인했으나 모두 `apps:[]`로 반환돼 Unity 창이 노출되지 않았다. 따라서 다음 항목은 실행하지 않았다.

- `Hexiege → Combat → Run Unit Action Self Validation`: **미실행**
- Android `Build And Run`: **미시작**
- 사용자 멀티플레이 실기: **미확인**

다음 진행 순서는 기존 계획과 같다. Unity self-validation PASS를 확인한 뒤 Build And Run을 시작하고, 빌드 완료 확인과 멀티플레이 실기는 사용자가 수행한다. 사용자 실기에서 근거리 유닛의 공격·피해 진행, handoff failure 0건과 현재 정상인 대미지 텍스트 위치 유지를 모두 확인하기 전까지 상태는 **FAIL / OPEN**이다.

## 8. 최종 완료 결과 — 2026-09-13

7절은 Unity 조작 연결이 막혀 있던 당시 gate 상태로 보존한다. 이후 전체 Unit Action self-validation PASS를 확인하고 Android Build And Run을 시작했으며, 사용자가 빌드 완료 및 이전 실패와 동일한 유닛 범위의 Editor Host + Android Client 실기를 완료했다.

| 완료 조건 | 결과 |
|---|---|
| 근거리 공격 진입 | 이전 실패 근거리 6종 모두 accepted shadow-commit 확인 |
| 공격 인계 | `combatPresentationHandoffFailures=87 → 0` |
| 정지 표현 | `stationaryWalkViolations=0`, 실패 상세·overflow 0 |
| 정상 보류 | `deferredCombatTargetChanges=2`, 실제 적용 실패 0 |
| C2 결과 | Host 2,703건/실패 0, Client 2,703건 수락/거부 0 |
| C3 필수 표현 | Host/Client 2,410/2,410, 실패 0 |
| 대미지 위치 | Host 2,144건·Client 2,036건, mismatch 0 |
| 전송·순서 | Client 공격 시작 656건, gap/order violation 0 |
| 로그 안전성 | 양쪽 ROOT PASS, error/drop 0, ERROR/FATAL/Exception 0 |

공통 키는 `abe1fb66044f1aa5efbb99d4420fa901e48c927ebbf3c24ef30b72f181c1f5c1`이다. `coveredUnitTypes=9/25`는 이전과 같은 의도적 비교 범위이며 현재 Task 완료 조건을 충족한다. 전체 25종·역할교대·Legacy rollback은 이 Task의 미완료가 아니라 별도 P0 통합 회귀다.

계획한 세 production 파일만 변경했고 검증기·Observer·schema·피해 계산·대미지 텍스트·VFX는 변경하지 않았다. 따라서 이번 Task를 **PASS / CLOSED**로 닫는다.
