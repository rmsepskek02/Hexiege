# 공격 진입 원자적 인계 회귀 — 조사

유닛은 공격 사거리에 들어오는 순간 위치를 멈추되, 화면에서는 걷기 자세로 굳거나 잠깐 멈추지 않고 곧바로 공격 동작으로 이어져야 한다. 이번 실기에서는 이 전환이 다시 끊겼고, 불정령 2단계는 가까이 붙은 뒤에도 걷기 동작을 계속한 채 공격하지 않는 것처럼 보였다. 이 문서는 이미 통과했다고 판단했던 영역이 왜 다시 실패했는지, 최신 같은 경기의 Android Host와 Editor Client 로그 및 현재 코드 경계를 대조한 결과를 기록한다.

## 판정

- 최신 실기 전체 판정: **FAIL**
- C3 확정 결과 표현: 이번 경기 범위에서는 정상 수렴
- 공격 진입 및 Host 표현 인계: 회귀 확인
- 기존 Unity self-validation: 실제 Host 네트워크 순서를 재현하지 않아 거짓 PASS를 허용

## 같은 경기 증거

- Android Host: `Assets/_Project/Docs/_Logs/2026-09-09/16_15_logcat/RuntimeLog_device.txt`
- Editor Client: `Assets/_Project/Docs/_Logs/_editor/2026-09-09/RuntimeLog.txt`
- 공통 세션 키: `ae3ad93fb31ecf0db4bad0e328510d3f1c103a3a8d628aebe78bcd496452dfdb`
- Android 공격 관측 스키마: `c3-authoritative-result-presentation-shadow-v8`

### 공격 진입 회귀

Android Host 이동 terminal은 전체 62,330 frame에서 위치·회전 writer 충돌과 일반 정지 Walk 위반을 0으로 집계했지만, 서버에 늦게 도착해 무시된 전투 타겟 이벤트는 **1,103건**이었다. 저장된 bounded 상세 중에도 `start` 46건과 `stop` 68건이 확인됐다. 현재 terminal은 이 값을 실패 조건에 포함하지 않아 `EVIDENCE`로 종료했다.

이는 단순 네트워크 잡음이 아니다. Host는 `UnitAnimState.Attack` 값 콜백을 의도적으로 적용하지 않고 `StartCombatClientRpc`가 되돌아온 뒤 `UnitView.StartCombatAnimation`에서 Attack CrossFade를 수행한다. 그 사이 서버 gameplay 루프가 Action 회전 소유권을 닫으면 Host는 같은 시작 이벤트를 stale로 거부한다. 위치는 이미 멈췄는데 Animator는 Walk에 남을 수 있다.

### 불정령 2단계 표본

- `EmberSpirit`은 코드와 스탯 문서에서 불정령 2단계이며 공격 사거리 0.5의 느린 근접 유닛이다.
- Unit 47은 `16:12:52.841`에 Unit 53을 대상으로 공격 시작 gate에 들어갔으나 방향 오차 `39.982°`로 `Misaligned`였다.
- `16:12:56.819`에는 두 유닛 사이 거리가 약 0.35이고 Unit 47의 Root는 `NoIntent` 상태로 3.098초 이상 안정 정지했다. Root 방향도 대상 방향으로 수렴했다.
- Unit 47은 `16:12:57.645`에 사망했다.
- 기존 관측기는 정상 gate/commit 상세를 유닛별이 아니라 타입·결과별 1회만 기록한다. 따라서 Unit 47이 그 사이 서버 피해를 실제로 발행했는지는 현재 로그만으로 확정할 수 없다. 사용자가 본 “Walk 지속·공격 부재”를 서버 판정 부재와 화면 표현 부재로 분해하지 못한 것도 진단 계약의 결함이다.

### C3와의 분리

같은 경기에서 C3 완료 묶음은 `results=3311`, `ready=3311`, `pending=0`, `failures=0`이었다. 양측 표현도 `expectedVisual=2968`, `presentationEmits=2968`, `viewUnavailable=0`, `duplicateAttempts=0`, `transportFailures=0`으로 수렴했다. 따라서 이번 현상의 직접 원인은 확정 결과를 피격 표현으로 소비하는 C3 경계가 아니라, 그보다 앞선 이동→공격 진입과 Host Animator 인계 경계다.

다만 C3 수렴만으로 이번 빌드를 PASS로 판정할 수 없다. 유닛 행동의 선행 단계가 실패했기 때문에 전체 결과는 FAIL이다.

## 현재 코드의 구조적 원인

1. `UnitView.EnterCombatPursuitV3`가 공격 사거리 진입을 확인하고 위치 `NoIntent`를 게시한 뒤 Action 회전 소유권을 연다.
2. `EnterCombatLoopV3`가 `OnUnitEnteredCombat` 이벤트를 발행한다.
3. `NetworkCombatController.OnUnitEnteredCombatHandler`는 provisional Attack 상태를 등록하고 `StartCombatClientRpc`를 보낸다.
4. Host Animator는 로컬 gameplay 경계에서 바로 전환되지 않고 그 RPC 수신 경로를 기다린다.
5. `UnitView.StartCombatAnimation`은 Host에서 Action 소유권이 남아 있을 때만 타겟과 CrossFade를 받아들인다.
6. 사거리 경계 진동, 타겟 사망·이탈 또는 호출 순서 차이로 Action 소유권이 먼저 닫히면 시작 RPC가 무시되고 Walk 표현이 남는다.

즉 서버 권위 gameplay 전환과 Host 표현 전환을 별도 비동기 경계로 나눈 것이 원인이다. 불정령 2단계의 작은 사거리와 느린 이동은 이 경계 진동을 더 자주 노출하지만 타입 전용 결함은 아니다.

## 기존 PASS가 막지 못한 이유

현재 `ValidateB3RotationWriterOwnership`은 지연된 서버 Start/Change/Stop 이벤트가 닫힌 Action 소유권을 되살리지 않아야 한다는 정책만 검사한다. 이것은 필요한 안전 조건이지만, 유효한 최초 진입이 Host에서 네트워크 왕복 없이 반드시 한 번 표현되어야 한다는 반대 조건을 검사하지 않는다.

또한 terminal은 `ignoredServerTargetEvents`를 출력만 하고 start/change/stop을 구분하지 않으며, 유효한 start 무시를 실패로 승격하지 않는다. 그래서 1,103건이 있어도 자동 검증과 실기 terminal 모두 오류를 선언하지 않았다.

## 적용해야 할 규칙

- `GameSystemRules_Units.md` 이동 규칙 8: `TargetAcquirePriority + NoIntent`는 일반 Held가 아니며 Walk에서 provisional Attack으로 직접 이어져야 한다.
- `U-COMBAT-PHASE`: `AlignToAttack`의 위치 정지와 Attack 선행 표현은 분리하되 중간 Held/Idle을 삽입하지 않는다.
- `U-ATK-ALIGN`: 서버 Root만 270°/s로 회전하고 5° 이내에서만 실제 공격 회차를 커밋한다.
- `U-ATK-TIMELINE`: provisional 표현은 피해 권한이 아니며 커밋 전 marker는 결과를 만들지 않는다.
- `NET-PRESENT-001`: 서버와 Client 모두 같은 공격 진입에서 Walk 첫 자세·Idle·Held 없이 provisional Attack으로 직접 전환한다.
- 서버 권위 유지: 이번 교정은 Host의 표현 시작 시점을 로컬 gameplay 경계와 원자화할 뿐, 공격 회차·피해·사거리·방향 판정 권한을 클라이언트에 주지 않는다.

## 추가 관측 사항

같은 Android 로그에는 대량 피해 숫자 출력 중 DOTween capacity가 `200/50 → 200/125`, 이후 `200/125 → 500/125`로 자동 확장된 Warn이 두 번 있다. 이번 공격 진입 실패의 직접 원인은 아니지만, 향후 대량 전투 성능 검증에서 별도 처리해야 한다.

## v11 실기 재검증 결과 (2026-09-09 19:17)

- Android Host: `Assets/_Project/Docs/_Logs/2026-09-09/19_17_logcat/RuntimeLog_device.txt`
- Editor Client: `Assets/_Project/Docs/_Logs/_editor/2026-09-09/RuntimeLog.txt`
- 공통 세션 키: `248f53d7ae6b899057d1104430dbade4b8624b38cef35c63d9a02382558eb23f`
- 육안 관측: 공격 직전 정지·Walk 고착·불정령 2단계 공격 부재를 재현하지 못했다.

원자적 Host 진입 교정의 주 목표는 확인됐다. Host의 `ignoredServerTargetEvents`는 1,103건에서 0건으로 내려갔고, `stationaryWalkViolations=0`, writer/corridor/spatial 실패도 모두 0이었다. C2는 서버 결과 2,035건과 실패 0, C3는 `ready=2035`, `expectedVisual=1867`, `presentationEmits=1867`, `viewUnavailable/duplicate/transport/gameplayWrites/failures=0`으로 수렴했다. Host와 Client의 Root Pose 개별 요약도 PASS였으며 크래시·ANR·미처리 예외는 없었다.

그러나 이동 observer terminal은 `combatPresentationHandoffFailures=6`으로 FAIL했다. 여섯 건은 Unit 5·21·124·142·97·82이며 전부 `boundary=target-change`, 호출 경계도 `TickCombat → PublishCombatTargetChange`로 동일했다. `provisional-not-ready`, `provisional-apply`, commit 실패는 없었다.

### 남은 구조 원인

`TickCombat`이 이동 Action보다 먼저 새 적을 발견한 것은 정상적인 서버 후보 탐색이다. 하지만 현재 코드는 그 후보를 `_unitCombatTargets`에 저장한 직후, Action 회전 소유권이 아직 열리지 않았는데도 `UnitView.TryApplyServerCombatTarget`과 `ChangeTargetClientRpc`를 실행한다. 즉 `AcquireTarget`의 변경 가능한 후보와 실제 Host/Client에 공개된 공격 타겟을 같은 상태로 취급한다. 서버 View는 올바르게 이를 거부했고, 진단기는 정상 후보 발견 자체가 아니라 조기 공개 시도를 실패로 기록했다.

이번 실패는 C2 피해 writer나 C3 결과 표현의 실패가 아니다. 조기 공개를 진단기에서 무시하면 서버 후보와 화면 타겟이 서로 다른 시점에 전환될 수 있으므로, 구현에서 후보를 보류하고 Action 소유권이 열린 원자 경계에서만 공개해야 한다.

## v14 빌드 실기 결과 (2026-09-13 01:12)

- Android Host: `Assets/_Project/Docs/_Logs/2026-09-13/01_12_logcat/RuntimeLog_device.txt`
- Editor Client: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt`
- 공통 세션 키: `01842d88a20ff3f56597188d0bf12d002488fc0031faf950b7ffc8174828828a`
- 자동 gate: `[UAS-DIAG]`, C2, C3 self-validation PASS 뒤 Android Build And Run 완료

v14의 PostCombatResume 교정은 기존 약 1초 재명령 정지 원인을 제거했다. 같은 경기의 Host와 Client 전체에서 `commandRevision > 1` 재발급은 0건이었고, Client Unit 17의 `NoIntent(commandRevision=1, segmentRevision=4)`에서 `Move(commandRevision=1, segmentRevision=5)`까지는 25ms였다. 따라서 기존 이동 명령을 외부 ticker가 새 명령으로 되살리던 결함은 재현되지 않았다.

그러나 전체 판정은 **FAIL**이다. Host 이동 terminal은 `combatPresentationHandoffFailures=70`, Client terminal은 `clientAttackEntryOrderViolations=55`를 기록했다. Host의 70건은 모두 `client-visible-entry-order` 경계에서 발생했고, 현재 `OnUnitEnteredCombatHandler`와 provisional 시작 경로가 이미 Attack 표현 중이거나 시작 pending인 유닛을 ACK 없이 반환하는 구조와 일치한다. 이미 같은 타겟의 Attack이 화면에 존재하는 멱등 성공과 실제 적용 실패를 구분하지 않아 Root 정지 commit이 불필요하게 보류될 수 있다.

Client의 55건 중 41건은 4~6 frame이지만 최대 283 frame까지 분포했다. 현재 추적기는 한 번 이동 Walk를 본 뒤 command·segment·semantic scope를 확인하지 않은 채 미래의 임의 Attack까지 정지 Walk frame을 누적한다. 따라서 55건 전체를 실제 공격 전 멈춤으로 해석할 수 없고, 정확한 공격 진입 scope로 관측기를 좁혀야 한다.

C2/C3 결과 전달 자체는 정상 수렴했다. 양측 Coordinator는 schedule 1,961, result/ready/bundle/released bundle 1,943, pending/failure 0이었고, `expectedVisual=1705`, `presentationEmits=1705`, 중복·전송 실패 0이었다. 다만 대미지 폰트의 최종 화면 좌표와 피해자 VisualRoot 좌표 차이를 기록하는 항목이 없어, 사용자가 본 위치 이상은 이 로그만으로 맞음/틀림을 판정할 수 없다. 다음 작업은 위치 권위나 emitter를 바꾸기 전에 정확히 한 번 변환된 표시 좌표를 살아 있는 동일 피해자 View와 읽기 전용으로 비교하는 증거를 추가해야 한다.
