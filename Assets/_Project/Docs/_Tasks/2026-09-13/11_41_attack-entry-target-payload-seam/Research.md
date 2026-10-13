# 공격 진입 타겟 전달 경계 교정 — 조사

근거리 유닛은 적에게 충분히 가까워진 뒤 공격을 시작해야 하지만, 최신 멀티플레이 경기에서는 일부 유닛이 공격 직전에 멈춘 채 다음 단계로 넘어가지 못했다. 이번 조사는 새로운 기능이나 검증 장치를 추가하는 작업이 아니다. 이동 중 이미 선택한 적의 정보가 공격 시작 담당 코드까지 온전히 전달되지 않는 한 지점을 확인하고, 그 지점만 고치기 위한 근거를 정리한다.

## 1. 현재 판정

- 상태: **PASS / CLOSED** — 2026-09-13 동일 범위 Editor Host + Android Client 실기 완료
- production seam 코드 교정: **적용 완료**
- 이벤트 발행·구독 전수 확인: **PASS** — 기존 `Subject<int>`, 정수형 handler, 유닛 ID 단독 발행 잔존 0건
- Unity Roslyn 정적 컴파일: **PASS** — Android Runtime, Editor Runtime, Editor Assembly 모두 exit 0 / error 0
- Unity self-validation: **PASS** — 전체 `[UAS-DIAG]`, C2, C3 메뉴 gate 통과
- Android Build And Run: **실행 완료** — 완료 확인과 실기는 사용자 담당
- 사용자 멀티플레이 실기: **PASS** — 이전 실패와 동일한 유닛 범위
- 최종 PASS 조건: 수정 빌드에 대한 사용자 멀티플레이 실기 확인
- 기존 self-validation과 정적 컴파일: 빌드 전 안전 확인일 뿐 최종 PASS 근거가 아님

이번 현상은 완전히 고쳤던 버그가 다시 발생한 것이 아니다. 이전 문서에서 “최초 공격 진입 시 타겟을 원자적으로 전달한다”고 기록했지만, 실제 production 이벤트는 여전히 유닛 ID만 전달했다. 즉, production seam이 완성되지 않았는데 문서와 자동 검증 결과를 근거로 완료했다고 잘못 판단한 상태였다.

## 2. 사용자 실기와 로그 근거

- 공통 경기 키: `sharedSessionKey=993718220d45194886d85f22bd4e8cade81e2992baf80a5152b7127a2fd69a3e`
- Editor Host 로그: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt`
- Android Client 로그: `Assets/_Project/Docs/_Logs/2026-09-13/05_19_logcat/RuntimeLog_device.txt`
- 사용자 육안 결과: 근거리 유닛들이 모두 정상적으로 공격하지 않았고, 대미지 텍스트 위치는 정상으로 보였다.

Host 로그 7331행은 `combatPresentationHandoffFailures=87`, `heldFrames=423`, 저장 상세 64건과 overflow 23건을 기록한다. 7336행의 이동 최종 판정은 `FAIL`이다. 따라서 공격 진입 인계가 실제 경기에서 실패했다는 사실은 확정이다.

반면 같은 Host 로그 7338~7342행은 다음을 기록한다.

- 서버 공격 결과 2,298건, 결과 실패 0건
- 필수 시각 결과 2,036건과 실제 표시 2,036건
- Host 공간 표본 1,830건, 위치 불일치 0건
- 공격 증거 범위 4/25종, 결과·표현 관측 verdict는 `EVIDENCE`

Client도 132630행에서 필수 시각 결과 2,036건과 실제 표시 2,036건이 일치하고, 132648행에서 공간 표본 1,752건과 위치 불일치 0건을 기록한다. 이는 **정상적으로 공격까지 진입한 회차의 피해 결과와 대미지 위치는 수렴했다**는 뜻이지, 공격 진입 실패가 없었다는 뜻이 아니다. 제한된 공격 coverage와 C2/C3 수렴을 전체 유닛 행동 PASS로 일반화할 수 없다.

## 3. 실제 production 호출 흐름

1. `UnitView.cs:4176~4212`에서 다음 이동 후보인 `candidatePursuitPosition`이 공격 범위에 들어오는지 판정한다. 이 시점의 callback은 이미 `targetId`와 `targetIsUnit`을 보유한다.
2. `UnitView.cs:5129~5146`의 `TryBeginServerAttackEntryBeforeRootStop`은 해당 타겟으로 Action 회전 소유권을 열지만, `OnUnitEnteredCombat`에는 `_unitData.Id`만 발행한다.
3. 이벤트 정의인 `GameEvents.cs:1028~1033`은 `Subject<int>`이므로 타겟 ID와 타겟 종류를 전달할 수 없다.
4. `NetworkCombatController.cs:2436~2477`의 `OnUnitEnteredCombatHandler(int unitId)`는 잃어버린 타겟을 `TryFindTarget` 또는 `FindNearestEnemy`로 다시 찾는다.
5. 이 재검색은 아직 후보 위치가 commit되기 전의 현재 Simulation Root 위치를 기준으로 한다. 근거리 유닛은 후보 위치에서는 사거리 안이지만 현재 위치에서는 사거리 밖일 수 있어 결과가 `null`이 된다.
6. 후보가 없으면 핸들러는 ACK 없이 반환한다. `UnitView.cs:1368~1390`의 이동 commit 경계는 공격 표현 인계가 완료되지 않았다고 판단해 Root 위치 commit을 보류한다.
7. 다음 frame에도 같은 순서가 반복되면서 유닛이 공격 직전에 멈춘다.

별도의 재진입 발행점도 남아 있다. `UnitView.cs:4298~4326`의 `EnterCombatLoopV3`는 `OnUnitEnteredCombat.OnNext(_unitData.Id)`를 다시 호출한다. 이벤트 타입을 바꿀 때 이 두 번째 발행점도 같은 typed payload 계약으로 바뀌어야 한다.

## 4. 가설 순위와 판정

### 1순위 — 후보 타겟 유실 뒤 commit 전 위치에서 재검색

**판정: 확정**

- candidate 판정 callback에는 `targetId`와 `targetIsUnit`이 존재한다.
- 실제 이벤트는 `Subject<int>`이며 두 값을 버린다.
- 수신 핸들러는 현재 위치에서 타겟을 다시 검색하고, 결과가 없으면 ACK 없이 반환한다.
- 저장된 handoff 상세 64건이 모두 `client-visible-entry-order`인 것은 내부 provisional 적용 전 이 경계에서 중단되는 구조와 일치한다.
- 근거리 타입에서 집중적으로 발생했다는 사용자 관찰과 candidate-inside/current-outside 조건이 일치한다.

### 2순위 — Host provisional 표현 적용 실패

**판정: 기각**

Host 적용 단계 자체가 실패했다면 `provisional-not-ready` 또는 `provisional-apply` 계열 경계가 먼저 남아야 한다. 저장된 상세는 모두 그보다 바깥쪽인 `client-visible-entry-order`이며, 현재 코드는 타겟 재검색 결과가 없으면 provisional 적용 호출 전 반환한다.

### 3순위 — 멱등 receipt 충돌 또는 pending 오판

**판정: 기각**

receipt 충돌이 원인이면 `provisional-conflict` 또는 `provisional-idempotent-ack` 관련 근거가 먼저 나타나야 한다. 최신 저장 상세에는 해당 경계가 없고, 실패는 타겟 재검색 결과가 없는 시점에 발생한다.

### 4순위 — 공격 정렬 또는 cooldown gate 실패

**판정: 주원인으로 기각**

Host 공격 terminal은 `legacyBeforeAligned=0`, `productionGateInvalid=0`, `targetMismatches=0`이다. 실패 유닛은 정렬·공격 commit gate에 들어가기 전 handoff에서 막히므로 이 축은 최신 멈춤의 주원인이 아니다.

### 5순위 — 피해·대미지 표현 파이프라인 실패

**판정: 기각**

Host 서버 결과 2,298건과 Client 수락 2,298건이 수렴하고, 필수 표현은 양측 2,036/2,036건이다. Host/Client 대미지 위치 mismatch도 0건이다. 피해·표현 경로를 수정하면 정상 축을 불필요하게 흔들게 된다.

## 5. 규칙과의 불일치

- `GameSystemRules_Units.md` `U-MOV-ALIGN` 8~9는 공격 진입을 일반 Held가 아닌 Action handoff로 다루고, 같은 틱에 이동 표현에서 provisional Attack으로 직접 전환하도록 요구한다.
- `GameSystemRules_Units.md` `U-COMBAT-PHASE`는 `AlignToAttack`에서 위치를 완전히 정지하고 공격 회차 전환을 별도 권위 단계로 처리한다.
- `GameSystemRules_Units.md` `U-TARGET-COMMIT`은 최초 진입의 provisional Start payload가 후보 타겟과 타격 억제를 같은 원자 경계에서 전달하도록 요구한다. 현재 `Subject<int>`는 이 계약을 충족하지 않는다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`은 최초/재진입에서 단일 payload가 target·revision·impact suppression을 Host 적용과 원격 Client 전달에 연결하도록 요구한다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-ACTION-IDEMPOTENT`는 중복 수신이 현재 상태를 되돌리지 않도록 요구한다. 이는 동일 payload의 안전한 재처리 근거이며, 수신 시점 재검색으로 다른 타겟을 만드는 근거가 아니다.

## 6. 자동 검증 공백

현재 self-validation은 target 값이 이미 완성된 정책 객체나 DTO를 직접 구성해 검사한다. 다음 실제 production 순서를 실행하지 않는다.

`후보 위치는 사거리 안 → 현재 위치는 사거리 밖 → 이벤트로 target 전달 → provisional ACK → 후보 위치 commit`

따라서 기존 self-validation PASS는 이번 production seam의 성공 증거가 될 수 없다. 다만 이번 작업에서는 새 테스트나 진단을 추가하지 않는다. 검증기·Observer·schema를 확대하는 대신 실제 이벤트 seam을 최소 수정하고, 기존 self-validation과 정적 컴파일은 빌드 전 회귀 안전 확인으로만 사용한다. 최종 판정은 새 빌드에 대한 사용자 멀티플레이 실기와 `combatPresentationHandoffFailures=0` 여부로 한다.

## 7. 영향 범위

최소 변경 후보는 다음 세 파일이다.

- `Assets/_Project/Scripts/Application/Events/GameEvents.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

피해 계산, C2 결과 writer, C3 결과·표현, 대미지 텍스트, 피격 VFX, 공격 에셋, 검증기, Observer와 schema는 최신 증거상 원인이 아니므로 변경 대상이 아니다.

## 8. 구현 결과

확정된 production seam만 계획대로 교정했다. 코드가 적용되고 정적 컴파일까지 통과했지만, Unity self-validation과 빌드 및 사용자 실기는 아직 수행되지 않았다. 따라서 이 절은 **코드 적용 결과**를 기록할 뿐 작업 완료나 PASS를 의미하지 않는다.

### 8-1. 실제 변경 파일

- `Assets/_Project/Scripts/Application/Events/GameEvents.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

계획한 세 파일에만 production seam 교정이 적용됐다. 피해 계산, 대미지 텍스트, VFX, Observer, schema와 검증기는 변경하지 않았다.

### 8-2. 적용된 계약

- `UnitEnteredCombatEvent` typed payload가 `UnitId`, `TargetId`, `TargetIsUnit`을 함께 전달한다.
- `UnitView`의 최초 공격 진입과 재진입 발행점 모두 이미 확정된 타겟을 payload에 담아 발행한다.
- `NetworkCombatController`의 공격 진입 handler는 payload 타겟을 직접 사용하며, 이 경계에서 현재 위치를 기준으로 타겟을 다시 검색하지 않는다.
- 전달된 타겟이 사망·소멸해 유효하지 않으면 다른 타겟으로 대체하지 않고 ACK하지 않는다. 다음 정상 tick의 기존 탐색 흐름이 새 타겟을 획득한다.
- 기존 `TickCombat` 탐색과 명시적 `ChangeTarget` 경계는 유지됐다.

### 8-3. 독립 정적 확인

- 모든 `OnUnitEnteredCombat` 발행점과 구독점을 전수 확인한 결과, 기존 `Subject<int>`, 정수형 handler와 유닛 ID 단독 발행은 0건이다.
- Unity가 생성한 Roslyn 응답 파일을 사용한 Android Runtime, Editor Runtime, Editor Assembly 정적 컴파일은 모두 exit 0 / error 0이다.
- 출력에는 기존 경고만 남았으며 이번 변경으로 확인된 정적 컴파일 오류는 없다.

### 8-4. 미완료 gate

Windows 조작 연결 상태를 두 차례 확인했지만 모두 `apps:[]`로 반환돼 Unity 창을 조작할 수 없었다. 이 때문에 다음 단계는 수행하지 않았다.

1. `Hexiege → Combat → Run Unit Action Self Validation`
2. Android `Build And Run`
3. 수정 빌드의 사용자 멀티플레이 실기
4. 근거리 공격 정상 진행과 `combatPresentationHandoffFailures=0` 확인

정적 컴파일 PASS를 self-validation PASS나 실기 PASS로 대체하지 않는다. 사용자 실기 전 최종 상태는 계속 **FAIL / OPEN**이다.

## 9. 2026-09-13 후속 동일 조건 실기 결과 — PASS

위 8절은 Unity 조작 경로를 찾기 전의 코드 gate 기록이며 삭제하지 않는다. 이후 네이티브 Windows 조작 경로로 self-validation PASS를 확인하고 Build And Run을 시작했으며, 사용자가 빌드 완료 뒤 이전 실패와 동일한 유닛으로 Editor Host + Android Client 실기를 수행했다.

- 공통 경기 키: `abe1fb66044f1aa5efbb99d4420fa901e48c927ebbf3c24ef30b72f181c1f5c1`
- 이전 실패 경기 `993718…`: 동일 근거리 6종 `LittleKnight`, `EmberSpirit`, `TideSpirit`, `DustSpirit`, `FlameSpirit`, `SpearMan`에서 `combatPresentationHandoffFailures=87`
- 최신 Host: `combatPresentationHandoffFailures=0`, `heldFrames=431`, `stationaryWalkViolations=0`, 실패 상세·overflow 0, `deferredCombatTargetChanges=2`, `ignoredServerTargetEvents=0`
- `deferredCombatTargetChanges=2`는 Action 준비 전 공개를 막은 정상 보류이며 적용 실패가 아니다.
- 최신 Host의 accepted shadow-commit에 위 근거리 6종이 모두 포함됐다.
- C2: Host `serverResults=2703`, `resultFailures=0`; Client `accepted=2703`, `rejected=0`
- C3 필수 표현: Host/Client 모두 `expectedVisual=2410`, `presentationEmits=2410`, 실패 0
- 위치: Host 표본 2,144건·Client 표본 2,036건, mismatch 0
- Client 공격 시작 656건, transport gap·order violation 0
- Host/Client ROOT summary PASS, error/drop 0, 최신 세션 `ERROR`·`FATAL`·`Exception` 0

이번 `coveredUnitTypes=9/25`는 이전과 동일하게 사용자가 의도한 비교 범위다. 나머지 유닛은 이전에도 이번에도 시험하지 않았으므로 현재 Task의 실패나 미완료 사유가 아니다. 전체 25종·역할교대·Legacy rollback은 별도 통합 회귀로 유지한다. 동일 조건에서 과거 근거리 6종 handoff 실패 87건이 0건으로 사라지고 피해·표현·위치 정상 축이 유지됐으므로, production typed target payload seam 교정은 **PASS / CLOSED**다.
