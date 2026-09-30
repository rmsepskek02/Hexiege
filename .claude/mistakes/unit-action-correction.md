# 유닛 공격·이동 정밀 교정 실패 기록

이 문서는 유닛의 이동, 타겟 전환, 공격 방향과 타격 시점을 정밀 교정하면서 실제로 반복된 실패를 다시 만들지 않기 위한 기록이다. **게임 규칙의 권위 원본이 아니다.** 현재 반드시 지켜야 할 게임플레이 규칙은 `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`, 멀티플레이 서버 권위·복제·취소 계약은 `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`를 따른다.

관련 작업을 시작할 때 이 문서의 목차를 먼저 보고, 현재 작업과 같은 실패 형태의 사건만 읽는다. 사건은 삭제하지 않는다. 교정이 구현·검증되면 기존 사건의 상태와 회귀 gate 결과를 갱신한다.

## 목차

- 2026-08-29  provisional Attack을 커밋 때 재시작해 원거리 적 감지 순간이 끊김
- 2026-08-29  공격자 사망 뒤 비지속 future hit 24개가 계속 실행됨
- 2026-08-30  C3가 정규 결과 키 대신 2초 시간창으로 연속 공격을 섞음
- 2026-08-31  Host v1·Client v2 혼합 경기를 동일 버전 C3 검증처럼 진행
- 2026-08-31  공격 애니메이션 시작 가드가 후속 공격 회차 스코프까지 차단
- 2026-08-31  매 커밋 scope 발행만 검증하고 소모 뒤 반복 Marker 방출을 놓침
- 2026-09-01  timeout fallback·복제 지연을 실제 marker·방향 실패와 한 합계로 판정
- 2026-09-02  C3 Shadow를 레거시 FIFO 방출 위에 덧대어 두 개의 타격 시계를 유지
- 2026-09-07  C3 단일화 계획에서 미래 일정과 확정 결과의 수명을 혼동
- 2026-09-07  공격 전환 NoIntent에 일반 Held Walk를 적용해 순간 멈춤 재발
- 2026-09-07  공격하지 않은 유닛의 Retire(None)을 C3 실패로 집계
- 2026-09-07  C3 미지원 타입의 default scope로 기존 공격 VFX까지 차단
- 2026-09-09  C3 확정 결과를 레거시 피해 이벤트와 살아 있는 View에 다시 의존시켜 피격 연출 누락
- 2026-09-09  피해 위치를 fact에만 추가하고 C2 주 결과는 빈 위치로 남겨 적중 묶음 1,059건을 거부
- 2026-09-09  Host 공격 진입을 되돌아오는 ClientRpc에 맡겨 Walk 고착을 재발시킴
- 2026-09-13  문서의 원자 target 전달을 production event seam 확인 없이 완료 처리
- 2026-09-14  production marker와 config 불일치 상태에서 Supported 승격을 문서 상태만으로 판단할 위험
- 2026-09-22  일반 Stop이 이미 커밋된 source marker까지 닫아 Client 공격 VFX를 누락

---

## 2026-08-29  provisional Attack을 커밋 때 재시작해 원거리 적 감지 순간이 끊김

**현상**

이동 중인 원거리 유닛이 적을 감지하면 공격 사거리 진입 경계에서 잠깐 멈추거나 애니메이션이 끊겨 보였다. 실기에서 Pistoleer를 포함한 원거리 유닛으로 반복 관측됐다.

**잘못된 판단**

공격 사거리 안에서 Attack 표현을 먼저 재생하면 Walk·Idle을 거치는 끊김을 없앨 수 있다고 판단했지만, 정지·방향 검증이 끝나는 커밋 순간에 같은 Attack 클립을 다시 처음부터 시작하도록 규칙과 구현을 함께 만들었다. 선행 표현의 연속성과 서버가 승인한 새 공격 회차의 시작을 같은 “클립 재시작”으로 묶은 것이 잘못이었다.

**원인**

provisional Attack이 진행되는 동안 서버는 정지와 5° 방향 정렬을 검증한다. 검증 완료 시 Animator의 현재 진행 위치를 보존하지 않고 같은 클립을 0부터 재시작하면, 이미 화면에 표시된 구간이 되감겨 짧은 정지처럼 보인다. 이 동작이 `GameSystemRules_Units.md`의 `U-COMBAT-PHASE`·`U-ATK-TIMELINE`과 동기화 문서의 `NET-PRESENT-001`에 명시돼 있었기 때문에, 구현만 고쳐도 문서 기준으로 다시 되돌아갈 수 있었다.

**로그 증거**

- 실기기 로그 `Assets/_Project/Docs/_Logs/2026-08-29/19_48_logcat/RuntimeLog_device.txt`
- Unit 4 `Pistoleer`: `19:46:59.454` `AwaitingStationarySample` → `19:46:59.555` `Ready`. 정지·정렬 승인 경계가 약 **0.101초** 지속됐다.
- 이 수치는 정지 검증 구간의 실측이며, 시각적 hitch 자체는 사용자의 실기 관찰과 규칙·구현의 restart 계약을 함께 대조해 원인을 확정했다.

**검증기가 놓친 이유**

기존 self-validation은 정지·5° 정렬·서버 승인 순서와 회차 ID 무결성을 검사했지만, provisional→commit 경계에서 Animator 정규화 시간이 뒤로 가지 않는지는 검사하지 않았다. 규칙 자체가 restart를 정답으로 요구했으므로 검증기는 잘못된 동작을 정상으로 통과시켰다.

**교정 계약**

1. provisional→commit에서 같은 Attack 클립과 정규화 진행 위치를 유지하며 restart하지 않는다.
2. 커밋 전 marker는 Impact·VFX·SFX·피격 표현·피해 결과를 만들거나 소비하지 않는다.
3. 커밋 뒤 아직 지나가지 않은 첫 marker만 새 회차의 첫 승인 후보로 사용한다.
4. 현재 cycle의 marker가 모두 지났다면 소급 타격이나 되감기 없이 다음 cycle의 첫 marker부터 승인한다.
5. 피해와 결과는 계속 서버만 승인하며, 화면의 연속 재생이 서버 권위를 대신하지 않는다.

**회귀 gate**

- provisional→commit 전후 Animator clip identity가 같고 정규화 진행 시간이 뒤로 가지 않는다.
- 같은 전환에서 Attack 시작 표현은 1회만 발생한다.
- 커밋 전 marker 결과 0건, 소급 승인 0건이다.
- 커밋 뒤 첫 승인 HitIndex는 아직 지나가지 않은 marker 또는 다음 cycle marker와 일치한다.
- 실기 멀티플레이에서 원거리 적 감지·정렬·첫 타격을 반복해 순간 멈춤과 되감김이 없어야 한다.

**상태**

`2026-08-30 이번 8/25종 집중 범위 PASS · Android Host/Editor Client sharedSessionKey=f2ad63c0…171d8 · 공격 커밋 2,144건에서 정렬 전 시작·타겟/회차/결과 상관 실패 0 · 원거리 감지 경계의 Attack 되감김/순간 멈춤은 로그가 직접 측정하지 않으므로 사용자 시각 확인을 최종 gate로 유지 · 25종 전체와 반대 역할은 통합 회귀 대기`

---

## 2026-08-29  공격자 사망 뒤 비지속 future hit 24개가 계속 실행됨

**현상**

실기기에서 공격 관련 ERROR가 반복됐다. 피해 자체는 fail-closed로 차단됐지만, 사망한 공격자가 이미 예약해 둔 미래 다중 타격을 계속 실행하려 했다.

**잘못된 판단**

Shadow가 `DeadTerminal`로 타격 승인을 거부하므로 안전하다고 보았고, Legacy 예약 작업을 사망 시점에 취소하지 않은 문제를 진단 오류나 무해한 로그로 취급했다. “잘못된 피해가 적용되지 않음”과 “존재하면 안 되는 future hit dispatch가 발생하지 않음”을 같은 조건으로 본 것이 잘못이었다.

**원인**

공격자 사망으로 Shadow 회차는 terminal이 됐지만, Legacy 다중 타격의 지연 작업은 살아 있었다. 각 예약 시간이 되자 `DeadTerminal` 회차에 판정과 완료를 시도했고, 한 번의 잘못된 타격마다 `shadow-unavailable-impact-rejected`와 `completion-without-accepted-authorization` 두 ERROR를 만들었다. 이는 `NET-CANCEL-003`의 공격자 사망 취소와 `NET-CANCEL-005`의 future HitIndex 결과 미생성 계약을 위반한다.

**로그 증거**

- 실기기 로그 `Assets/_Project/Docs/_Logs/2026-08-29/19_48_logcat/RuntimeLog_device.txt`
- `shadow-unavailable-impact-rejected` **24건**, `completion-without-accepted-authorization` **24건**으로 합계 **48 ERROR**.
- Unit 21은 `19:47:13.213` 서버 사망 뒤 같은 token 2의 예약 타격을 `19:47:13.712`, `.779`, `.879`, `.947`, `19:47:14.678`에 계속 dispatch했다. 전부 `advanceStatus=DeadTerminal`, `evaluateStatus=DeadTerminal`이었다.
- 별도 `impact-pose-capture-failure` 1건은 이 사건의 48 ERROR와 구분한다.

**검증기가 놓친 이유**

기존 검증은 terminal 회차가 피해를 fail-closed 하는지는 확인했지만, 공격자 사망 뒤 예약된 비독립 타격 작업 자체가 실행되지 않아야 한다는 lifecycle gate를 세지 않았다. observer도 취소돼야 할 dispatch를 받아 두 개의 ERROR로 사후 보고했을 뿐, 그 dispatch의 존재를 회귀 원인으로 직접 분류하지 못했다.

**교정 계약**

1. 공격자가 사망하면 Windup과 아직 실행되지 않은 `MeleeContact`·`Hitscan` future HitIndex를 취소한다.
2. 아직 독립 전달체를 만들지 않은 예약 타격은 dispatch 전에 폐기한다.
3. 폐기된 future HitIndex는 판정, `AttackImpactResult`, 완료 결과와 ERROR를 만들지 않는다.
4. 이미 서버가 발사·생성한 독립 `ProjectileImpact`·`TravelingArea`만 프로필의 `PersistsAfterSourceDeath`에 따라 지속할 수 있다.
5. 이미 적용된 결과는 이후 사망이 소급 취소하지 않는다.

**회귀 gate**

- 공격자 사망 뒤 비지속 future hit dispatch 0건이다.
- 취소된 HitIndex의 result/completion/error 0건이다.
- 사망 직전 이미 적용된 결과는 정확히 1회 보존된다.
- 독립 전달체는 `PersistsAfterSourceDeath=false`면 취소되고 `true`면 동일 회차·결과 키로 계속된다.
- 다중 타격의 초반 HitIndex 직후 공격자를 사망시키는 회귀 케이스를 self-validation과 Android 멀티플레이 모두에서 통과한다.

**상태**

`2026-08-30 이번 8/25종 집중 범위 PASS · Android Host/Editor Client sharedSessionKey=f2ad63c0…171d8 · 서버 사망 115건과 공격 결과 2,182건 동안 DeadTerminal·shadow-unavailable-impact-rejected·completion-without-accepted-authorization·impact-pose-capture-failure 모두 0 · resultFailures 0 · 25종 전체와 반대 역할은 통합 회귀 대기`

---

## 2026-08-30  C3가 정규 결과 키 대신 2초 시간창으로 연속 공격을 섞음

**현상**

C2 결과 2,220건은 Host와 Client에서 모두 정상 수락됐지만 C3는 Host `ambiguous=1,436`, Client `ambiguous=1,993`으로 FAIL했다. Android 화면에는 479건의 오류가 표시됐다.

**잘못된 판단**

Legacy 관측에 정규 결과 키가 없다는 이유로 `공격자 + 피해자 + HP + 2초` 후보가 하나일 때만 exact라고 간주했다. 후보가 여러 개면 fail-closed 하므로 안전하다고 보았지만, 연속 공격이 정상인 실제 경기에서는 marker 하나가 여러 결과의 후보가 되어 채점 자체가 성립하지 않았다.

**원인**

Animation marker는 HP가 없어 같은 공격자의 같은 대상 결과 2~12개와 동시에 맞았다. 결과별·단계별 소비 기록도 없어 같은 결과가 Marker, Tracer, Enqueue, Emit에서 반복 사용됐다. 그 결과 matched 수가 실제 결과 수를 초과했고, 정확히 결합되지 않은 표본으로 방향·시간 mismatch까지 계산했다. 예상된 Shadow 모호성을 매 건 Error로 출력해 read-only 진단 실패가 사용자용 실기 오류처럼 보였다.

**어떻게 드러났나**

- `2026-08-30/23_53_logcat`: 서버 결과 2,220, Client 수락 2,220, result reject/target/correlation failure 0.
- Host matched 5,303, Client matched 4,541로 정규 결과 2,220보다 많았다.
- Android Error 482건 중 479건이 `presentation-shadow-failure status=Ambiguous`; FATAL/ANR/비-UAS 예외는 0건이었다.

**교훈**

1. `NET-ACTION-IDEMPOTENT`가 정한 전체 결과 키 또는 `AttackerInstanceId + AttackSequenceId + HitIndex` scope가 없는 관측을 시간 근접만으로 공격 결과에 결합하지 않는다.
2. Marker/Tracer/Enqueue/Emit은 서로 다른 단계이며 각 정규 키·scope마다 단계별 최대 1회만 소비한다.
3. `matched` 총계만 보지 말고 단계별 계수와 `정규 결과 수보다 반복 사용됐는지`를 self-validation에서 검사한다.
4. Shadow 비교 실패는 terminal 판정을 FAIL로 유지하되, 예상 가능한 개별 mismatch를 사용자 오류 UI용 Error로 고빈도 출력하지 않는다.

**상태**

`2026-08-31 exact key/scope 자체 검증 및 Android v2 빌드 완료 · 첫 실기 재검증은 Editor Host v1 / Android Client v2 혼합 실행으로 무효 · 동일 버전 재검증 대기`

---

## 2026-08-31  Host v1·Client v2 혼합 경기를 동일 버전 C3 검증처럼 진행

**현상**

exact-key C3 교정 뒤 새 Android Build And Run에는 성공했지만, 다음 실기 경기에서 Android 화면 Error가 다시 발생했다.
처음에는 새 C3 구현의 방향·상관 실패처럼 보였으나 실제 경기는 Editor Host와 Android Client가 서로 다른 C3 코드를 실행했다.

**무엇을 틀렸나**

Android 설치·실행과 Unity self-validation PASS만 확인하고 멀티플레이 테스트 준비가 끝났다고 판단했다.
Editor Host가 실제로 로드한 Runtime assembly의 버전은 확인하지 않았다. 그 결과 Host는 구형
`c3-authoritative-result-presentation-shadow-v1 / Legacy / combatSchema=0`, Client는 신형
`v2 / PresentationShadow / combatSchema=1`로 같은 경기에 들어갔다.
또한 `result-END`, `presentation-END`, 전체 `END`에 하나의 종합 verdict를 재사용해,
C2 결과 자체가 정상이어도 C3 표현 실패 때문에 `result-END`까지 Error/FAIL로 출력되도록 만들었다.

**왜 그랬나**

Player 빌드가 최신 소스로 생성되면 Editor도 같은 코드를 실행한다고 가정했다.
그러나 `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`는 v2로 갱신돼 있었던 반면,
Editor가 로드한 `Library/ScriptAssemblies/Assembly-CSharp.dll`은 2026-08-30 22:11 생성본이며 v1 문자열을 보유했다.
"소스가 최신"·"Android 빌드가 최신"·"현재 Editor 프로세스가 최신 assembly를 로드함"을 서로 다른 조건으로 검사하지 않았다.

**어떻게 드러났나**

- 같은 `sharedSessionKey=beb0207a…410f`에서 Editor Host BEGIN은 `observerSchema=...-v1, pipelineMode=Legacy, combatSchema=0`, Android Client BEGIN은 `...-v2, pipelineMode=PresentationShadow, combatSchema=1`이었다.
- Android v2는 이전 경기의 `ambiguous=1,993`을 `ambiguous=0`으로 줄였지만, 구형 Host가 신형 표현 식별자를 제공하지 않아 `enqueueMatched=0`, `emitMatched=0`, `unmatched=279`가 남았다.
- Editor Host의 구형 진단기는 같은 경기에서 다시 `ambiguous=560`을 기록했다.
- Android의 실제 Error는 경기 종료 terminal 3건(`result-END`, `presentation-END`, `END`)이며 FATAL/ANR은 0건이었다.
- `result-END`는 `resultFailures=0`, `clientResultRejected=0`인데도 표현 실패의 종합 verdict를 받아 FAIL로 출력됐다.
- 별도 `UnitFactory`/`NetworkProductionController` 초기화 순서 Warning 264건은 이번 terminal Error와 섞지 않고 독립 문제로 분리했다.

**교훈**

1. 멀티플레이 검증 전 Host와 Client 양쪽 BEGIN에서 `sharedSessionKey`, `observerSchema`, pipeline mode, combat schema revision을 대조한다. 하나라도 다르면 경기를 진행해도 **판정 표본으로 사용하지 않는다**.
2. Player 빌드 성공으로 Editor assembly 최신화를 추정하지 않는다. Editor 재컴파일 뒤 로드된 `Assembly-CSharp.dll`의 수정 시각과 schema 문자열을 확인하고 Play Mode에 진입한다.
3. `result-END`는 C2 결과 실패만, `presentation-END`는 C3 표현 실패만 판정한다. 전체 `END`만 두 축을 합친 종합 verdict를 사용한다.
4. 혼합 버전 경기의 `directionMismatch`·`unmatched`는 실제 구현 결함으로 확정하거나 문서 PASS/FAIL 근거로 사용하지 않는다.
5. 실기 화면 Error는 먼저 원문을 세어 C3 terminal, 게임플레이 오류, 크래시/ANR, 일반 Warning으로 분리한 뒤 설명한다.

**재검증 gate**

- Editor와 Android BEGIN의 observer/pipeline/schema가 완전히 일치한다.
- C2 `result-END`는 결과 실패·거부 0이면 C3 상태와 독립적으로 EVIDENCE다.
- C3 `presentation-END`는 exact-key 단계별 계수로만 판정한다.
- 동일 버전 재경기에서 `ambiguous=0`을 유지하고, 남은 unmatched·direction mismatch만 새 증거로 재분석한다.
- Android 화면 Error 0, 비-UAS FATAL/ANR 0을 별도로 확인한다.

**상태**

`2026-08-31 원인 확정 · 혼합 버전 경기는 무효 표본 · verdict 분리 및 Editor 최신 assembly 확인 뒤 동일 버전 재검증 필요`

---

## 2026-08-31  공격 애니메이션 시작 가드가 후속 공격 회차 스코프까지 차단

**현상**

동일 v4 Android Host / Editor Client 경기에서 C2 서버 결과 1,278건은 실패·거부 없이 정상 수렴했고 B3 이동과 ROOT도 정상인데, C3 표현은 Host `invalid=2,230`, Client `invalid=2,255`로 FAIL했다. 실패 표본은 반복해서 `attackerInstanceId=0, sequenceId=0`인 Marker/Tracer를 기록했다.

**무엇을 틀렸나**

첫 전투 진입 때 Attack 클립을 한 번만 시작하기 위한 `_combatAnimationSent` 가드 안에, 매 서버 공격 커밋의 정확한 presentation scope를 전송하는 RPC까지 함께 넣었다. 그 결과 전투를 벗어나지 않고 반복 공격하면 첫 회차만 scope를 받고, 두 번째 이후 회차는 서버 결과가 생성되어도 화면 marker가 어느 회차인지 알 수 없었다.

**왜 그랬나**

“Attack 애니메이션을 다시 시작하지 않는다”와 “새 공격 회차 정보를 다시 보내지 않는다”를 같은 동작으로 취급했다. 클립은 한 번 시작해 계속 루프할 수 있지만, 서버 공격 회차는 쿨다운마다 새로 커밋되므로 두 수명은 같지 않다. 규칙도 restart 금지는 명시했지만 매 커밋 scope 발행 의무를 별도로 못 박지 않아 잘못된 결합을 막지 못했다.

**어떻게 드러났나**

- 공통 세션 `8baeb130…ecb15`, 양쪽 모두 `observerSchema=...-v4`, `PresentationShadow`, combat schema 1로 입장 계약은 정상이다.
- Host C2 `serverResults=1,278`, `resultFailures=0`; Client `clientResultAccepted=1,278`, `clientResultRejected=0`이다.
- 첫 유효 표본은 Client에서 instance 1 / sequence 1이었지만 이후 같은 유닛의 Marker/Tracer가 instance 0 / sequence 0으로 반복됐다.
- 코드에서 `ExecuteAttack`은 매 공격 주기 실행되지만 `StartCombatClientRpc`는 `_combatAnimationSent.Add(id)`가 처음 성공할 때만 호출됐다.

**교훈**

1. Animator의 시작·restart 여부와 공격 회차 scope 발행을 별도 명령과 별도 회귀 조건으로 검증한다.
2. 전투 상태를 유지한 채 N회 연속 공격하는 fixture에서 매 커밋마다 단조 증가 scope가 도착하고 각 회차 marker cursor가 hit 0부터 시작하는지 검사한다.
3. 후속 scope 발행은 이미 재생 중인 Attack 클립을 CrossFade하거나 normalized time을 되돌리지 않아야 한다.
4. 대량 `Invalid`가 발생하면 방향·타이밍 수치를 독립 원인으로 확정하지 않는다. 먼저 scope 수명 결함을 제거한 뒤 남은 exact 표본으로 재판정한다.

**상태**

`2026-08-31 원인 확정 · NET-PRESENT-002에 매 커밋 scope 발행 의무 추가 · 연속 공격 scope/CrossFade 분리 교정 진행 중`

---

## 2026-08-31  매 커밋 scope 발행만 검증하고 소모 뒤 반복 Marker 방출을 놓침

**현상**

매 커밋 scope 발행 교정과 self-validation PASS 뒤 새 동일 v4 경기에서도 C2는 정상 수렴했지만 C3는 Host `invalid=3,800`, Client `invalid=3,777`로 다시 FAIL했다. 한 회차의 정상 Marker 뒤 같은 Attack 루프에서 `attackerInstanceId=0, sequenceId=0` Marker가 반복됐다.

**무엇을 틀렸나**

연속 세 커밋 fixture에서 서버가 매번 새 scope를 발행하고 Animator를 restart하지 않는지만 검사했다. 실제 `UnitView.OnAttackHit`이 한 scope의 마지막 HitIndex를 소비한 뒤에도 `_attackPresentationImpactEnabled=true`를 유지해, scope 획득 실패 상태로 VFX·SFX·Tracer·피격 신호를 계속 방출하는 production 경로는 검증하지 않았다.

**왜 그랬나**

공격 회차의 시작 수명만 고쳤고 종료 수명을 모델링하지 않았다. `impactEnabled`와 instance·sequence·HitIndex cursor를 별도 필드로 둔 상태에서 “scope가 유효해야 방출 가능”이 아니라 “impactEnabled면 방출 가능”을 최종 gate로 사용했다. 얕은 순수 정책 테스트가 실제 Animation Event 호출 순서를 지나가지 않아 잘못된 자동 PASS를 만들었다.

**어떻게 드러났나**

- 공통 세션 `d8ed6d4a…fc15890`, Editor Host / Android Client, 양쪽 v4·`PresentationShadow`·combat schema 1이다.
- C2는 서버 결과 2,566 / 실패 0, Client 수락 2,566 / 거부 0이다.
- C3는 Host `invalid=3,800`, Client `invalid=3,777`; bounded 표본은 모두 scope 0인 Marker/Tracer다.
- Unit 1은 sequence 1 정상 Marker 뒤 다음 sequence 전에 scope 0 Marker가 반복됐다.
- 코드에서 `TryAcquirePresentationShadowMarkerScope`가 범위 초과로 false를 반환해도 `OnAttackHit`은 emitter를 계속 실행했다.

**교훈**

1. 회차형 허가는 `Closed/Armed/Consumed`의 시작과 종료를 함께 모델링한다.
2. Animation Event의 최종 방출 gate는 별도 bool이 아니라 유효한 정규 scope의 1회 소비 성공이어야 한다.
3. 회귀는 dispatch DTO만 검사하지 말고 production adapter와 같은 `scope 1회 → Marker N회` 호출 형태를 통과해야 한다.
4. 단일·다중 hit, 원거리 Tracer 캡처, 소모 후 반복, 중복·역순 scope, Stop/사망 clear를 한 묶음으로 검증한다.

**상태**

`2026-08-31 원인 확정 · 규칙에 1회성 표현 허가 종료 계약 추가 · production Marker 생명주기 RED/교정 진행 중`

---

## 2026-09-01  timeout fallback·복제 지연을 실제 marker·방향 실패와 한 합계로 판정

**무엇을 틀렸나**

C3가 약 0.5초 뒤 실행되는 `EmittedTimeout`을 실제 Animation marker와 같은 타격 시점 기준으로 채점했고, Client의 revision-lag와 scope-mismatch도 동일 revision의 실제 표현 방향 오류와 같은 `directionMismatch` 합계에 넣었다. 서로 다른 의미의 복구·복제 지연·실제 화면 불일치를 한 terminal 숫자로 읽으면, timeout 허용치를 넓히거나 방향 writer를 바꾸는 잘못된 교정으로 이어질 수 있다.

**왜 그랬나**

표현 관측의 종류는 Marker·Tracer·Enqueue·Emit으로 나뉘었지만, Emit이 정상 marker에 맞춰 실행된 것인지 marker 누락을 복구한 timeout인지에 따라 기대 시간이 달라지는 계약이 없었다. 방향도 exact 공격 키를 기록하면서 같은 revision인지, 이전 revision만 도착했는지, 아예 다른 scope인지를 최종 correctness 판정에서 충분히 분리하지 않았다. aggregate 수치를 먼저 0으로 만드는 데 집중하면 실제 gameplay writer와 observer 증거 문제의 경계가 무너진다.

**어떻게 드러났나**

- 동일 v5 세션 `fd521f…1ff4`에서 C2는 Host 결과 2,828/실패 0, Client 수락 2,828/거부 0이고 B3도 오류 0이었다.
- C3 Host의 Assault `EmittedTimeout`은 약 0.502~0.508초 차이로 반복 실패했다. 이는 0.5초 fallback 경계 바로 뒤이며, 별도로 Marker 계열의 약 0.7~1.99초 차이가 존재했다.
- Client `directionMismatch=49`는 same-revision 0, revision-lag 42, scope-mismatch 7이었다. 동일 서버 revision에서 실제 표현 방향이 틀린 증거는 0건이다.
- C3 unmatched는 Host 385, Client 562였고 `invalid=0`이다. 이전 scope 0 Marker 재발과 다른 실패인데 같은 생명주기 결함으로 일반화할 위험이 있었다.

**교훈**

1. 정상 marker, timeout fallback, target-death/stop 복구 표현을 별도 관측 종류·기대 시간·terminal 카운터로 검증한다.
2. timeout fallback은 정상 marker timing PASS가 아니며, 늦게 도착한 marker를 같은 결과나 다음 회차에 재결합하지 않는다.
3. 공격 방향 gameplay FAIL은 동일 `(Instance, Sequence, Revision, HitIndex)` 표본의 허용 오차 초과로만 선언한다. revision-lag와 scope-mismatch는 별도 복제 수렴 실패로 남긴다.
4. unmatched는 `scope 0` 재발로 추정하지 않고 취소·타겟 전환·결과 부재 회차를 exact key로 재구성해 원인을 증명한다.
5. 허용 오차를 넓히거나 C2/B3·서버 Aim·Client Root writer를 바꾸기 전에 failure 종류별 RED 회귀를 먼저 만든다.

**상태**

`2026-09-02 C3 v6 교정 구현 · 정상/복구 timing 분리 · marker-time 불변 revision · 구회차 retire 및 구조 실패 증거 추가 · Unity 전체/C2/C3 self-validation PASS · Android 서명 및 새 멀티 실기 재검증 대기`

---

## 2026-09-02  C3 Shadow를 레거시 FIFO 방출 위에 덧대어 두 개의 타격 시계를 유지

**무엇을 틀렸나**

C3의 정규 결과 키, 권위 Impact 시각, 방향 revision을 계속 보강하면서도 실제 화면 방출자는 레거시 `HitPresentationQueue`, 로컬 Animation Event, 트레이서 도착 콜백, 타겟 사망·공격 중단 flush로 남겨 두었다. 새 체계는 틀린 동작을 관찰만 하고 구 체계가 계속 화면을 제어했기 때문에, scope·timeout·사망·방향 문제를 하나씩 봉합해도 다른 경계에서 같은 종류의 어긋남이 반복됐다.

**왜 그랬나**

안전한 점진 전환을 이유로 Shadow를 오래 유지했지만, Shadow 검증 통과 조건과 실제 emitter 전환 시점을 구조적으로 닫지 않았다. 더 근본적으로 `GameSystemRules_Units.md`의 기존 규칙 19는 공격자 FIFO와 사망·중단 시 즉시 flush를 요구하는 반면, 최신 `NET-PRESENT-003`은 정규 결과 키 결합과 닫힌 구회차의 폐기·Unmatched를 요구한다. 서로 충돌하는 두 규칙을 정리하지 않은 채 양쪽을 동시에 만족시키려 했다.

**어떻게 드러났나**

- 동일 v6 경기 `sharedSessionKey=48fddc…8ccf`에서 C2는 서버 결과 1,714건/실패 0, Client 수락 1,714건/거부 0이고 B3·양측 ROOT도 PASS였다.
- Host Hitscan `TracerImpact`와 피격 방출이 권위 결과보다 약 0.501~0.938초 늦은 정상 timing 실패 13건을 만들었다.
- 타겟 사망 flush는 Host 최대 0.934초, Client 최대 1.499초 뒤 보류 표현을 방출했다.
- Client 방향 54건은 same-revision 실제 방향 실패 0, revision-lag 35, marker 시점 증거 부재 19였다. 서버 Aim writer가 아니라 표현 입력 수렴 경계의 문제다.
- Android의 별도 크래시·ANR·비-UAS 예외는 0이며 화면 ERROR는 C3 terminal FAIL 1건이었다.

**교훈**

1. 서버 권위 결과와 화면 표현이 각각 별도 타격 시계를 소유하게 두지 않는다. 서버가 커밋한 marker occurrence와 절대 Impact 시각을 단일 일정으로 사용한다.
2. Animation Event와 트레이서 도착 콜백은 표현 신호일 뿐 피격 표현의 권위 방출자가 될 수 없다.
3. 공격자 FIFO, timeout 정상 방출, 사망·StopCombat flush는 정규 결과 키 기반 Coordinator로 전환한 뒤 폐기 대상이다.
4. 타겟 사망·전환으로 닫힌 구회차는 뒤늦게 보여 주지 않고 명시적으로 폐기한다.
5. Client 방향은 marker 순간에 별도 NetworkVariable을 읽지 않고 같은 공격 일정에 포함된 instance·sequence·revision·권위 방향을 사용한다.
6. 허용 오차 상향이나 실패 종류별 조건 추가로 PASS를 만들지 않는다. 기존 레거시 emitter와 새 Coordinator의 differential gate를 통과한 뒤 단일 emitter로 전환한다.

**상태**

`2026-09-02 근본 원인 확정 · C3 단일 Authoritative Presentation Coordinator 전환 Research/Plan 작성 · 구현 전`

## 2026-09-07  C3 단일화 계획에서 미래 일정과 확정 결과의 수명을 혼동

**무엇을 틀렸나**

9월 2일 계획과 설명에서 타겟 사망·전환 뒤 구회차를 일괄 폐기한다고 적어 이미 서버가 적용했지만 아직 표시하지 않은 결과까지 지울 여지를 만들었다. 커밋 Schedule에 최종 방향과 ResultOrdinal 범위가 이미 존재한다고 가정했고, AoE 전체 도착을 확인할 완료 manifest와 Marker 부재 경계를 빠뜨렸다. 위 9월 2일 사건의 교훈 4·5도 이 보완 없이 구현 기준으로 사용하면 안 된다.

**왜 그랬나**

“서버 일정 하나로 통일”이라는 설명을 커밋 시 미래 결과까지 확정한다는 계약으로 확대했다. 미래 표현 허가, 이미 확정된 결과, 실제 화면 View의 수명을 구분하지 않았다.

**어떻게 드러났나**

구현 착수 전 `NET-FACING-002`는 Windup 추적과 실제 Impact 방향 기록을 요구하고, `NET-TIME-002`는 지연 흡수 시간축·결과 수신 후 표현을 이미 규정한다는 점을 대조했다. Result가 사망 알림 뒤 도착하는 정상 reorder에서도 일괄 retire가 유효 결과를 지울 수 있었다. 커밋 시 미래 AoE 대상 수는 알 수 없으므로 ordinal 전체 범위를 예약할 수 없다.

**교훈**

1. Schedule은 의도·예약 시각, Result는 실제 Impact outcome·방향으로 분리한다. commit revision을 impact revision과 동일시하지 않는다.
2. 취소/사망은 미확정 미래 허가만 닫는다. 확정 결과는 미표시 상태라도 보존하고 age·baseline·View 수명에 따라 표현한다.
3. AoE 원자 방출 전에 명시적 완료 manifest를 요구한다. 첫 결과·ordinal 최댓값·클립 이벤트 개수로 완료를 추정하지 않는다.
4. Marker가 없는 경우도 유효한 결과가 수렴해야 한다. Marker를 결과 방출 gate로 만들지 않는다.
5. 기존 표현 시간축 정책과 네트워크 지연 초과 catch-up을 적용한다. 새로운 임의 delay나 지연 0 보장으로 실패를 숨기지 않는다.
6. 비교 단계 빌드는 레거시 화면을 유지한다. 비교 구현·자동 검증·단일 writer 전환·실기 PASS를 별도 상태로 보고한다.

**상태**

규칙 19·20·21·22·26과 관련 특수 공격 참조, 동기화 계약, 현재 Research/Plan 보완. 구현·검증 결과는 담당 에이전트의 실제 증거를 받은 뒤 기록한다.

---

## 2026-09-07  공격 전환 NoIntent에 일반 Held Walk를 적용해 순간 멈춤 재발

**무엇을 틀렸나**

공격 사거리 진입에서 서버 위치를 멈추는 `TargetAcquirePriority`의 `NoIntent`를 재경로 대기·차단·일반 정렬과 같은 표현 상태로 취급했다. 그 결과 공격 선행 표현을 시작하기 직전에 `HoldMovementAnimation()`이 Walk 클립의 첫 자세를 재생하고 속도를 0으로 만들었다. 화면 흐름이 `Walk → provisional Attack`이 아니라 `Walk → 정지된 Walk 첫 자세 → provisional Attack`이 됐다.

**왜 그랬나**

이동 reducer의 “현재 틱에 위치가 전진하지 않는다”는 사실을 “Walk를 Held로 보여야 한다”는 표현 결정과 동일시했다. `UnitMovementPresentationPolicy`는 이동 가능 여부와 후보 위치 커밋만 받았고, 같은 `NoIntent`라도 행동 소유권으로 즉시 넘기는 `TargetAcquirePriority`인지 구분하지 않았다. 자동 검증도 stationary Walk 방지만 검사했을 뿐, 공격 handoff에서 Held가 한 프레임도 끼지 않아야 한다는 반대 방향의 불변식을 검사하지 않았다.

**어떻게 드러났나**

- 동일 경기 `sharedSessionKey=3a3e92c1…3665ccc`, Android Host / Editor Client에서 사용자가 Assault의 공격 사거리 진입 순간 짧은 멈춤을 육안으로 확인했다.
- Host Assault 표본은 `04:53:03.232 AwaitingStationarySample(40.780°)` → `04:53:03.329 Misaligned(15.056°)` → `04:53:03.434 Ready(0.423°)` → `04:53:03.529 commit`이었다.
- 이동 manifest의 Assault Chase는 `frames=383, move=0, noIntent=383, targetPriority=383`이었다. 물리 정지는 정상이나, 코드 대조에서 이 모든 직접 사거리 진입이 일반 Held Walk 정책을 통과함을 확인했다.

**교훈 및 교정 계약**

1. 권위 위치의 정지와 Animator의 Held 표현을 같은 상태로 간주하지 않는다.
2. 일반 `NoIntent`·`AlignToMove`는 제자리걸음을 막기 위해 Held를 사용하지만, 유효 타겟을 행동 소유권으로 즉시 넘기는 `TargetAcquirePriority`는 Held/Idle을 거치지 않는다.
3. 공격 handoff는 같은 서버 프레임에 위치 정지, Action 회전 소유권 이전, provisional Attack 시작을 수행한다.
4. provisional Attack은 정렬·커밋 대기 중 계속 재생하고, 커밋에서 restart하지 않으며, 승인 전 marker는 표현과 피해를 방출하지 않는다.
5. 회귀 검증은 `Move → TargetAcquire → AlignToAttack` 사이 Held 0회와 Attack 시작 1회를 명시적으로 검사한다.

**상태**

`2026-09-07 실기 FAIL 확정 · 규칙/Task/구현 교정 진행 중`

---

## 2026-09-07  공격하지 않은 유닛의 Retire(None)을 C3 실패로 집계

**무엇을 틀렸나**

네트워크 유닛이 Despawn할 때 유효한 공격 인스턴스를 가진 적이 없어도 C3 Coordinator의 `Retire`를 호출했다. 기존 Shadow scheduler는 `None`을 정상 no-op으로 처리하지만 신규 Coordinator는 이를 `Invalid` 실패로 증가시켜 양측 terminal에 `failures=31`을 만들었다.

**어떻게 드러났나**

- 같은 경기에서 생산 완료 유닛 142개, Coordinator의 유효 retire 111개, 실패 31개였고 `111 + 31 = 142`가 정확히 일치했다.
- Schedule 2,648개와 Result 2,591개는 모두 결합되어 `ready=2591, pending=0`이었다. `coordinator-failure` 결정 로그도 없었으므로 31건은 결과 수렴 실패가 아니었다.
- `NetworkUnit.OnNetworkDespawn()`은 `_attackPresentationInstanceId`가 `None`이어도 무조건 retire했고, Coordinator만 이 정상 부재를 실패로 계산했다.

**교훈 및 교정 계약**

1. “종료할 공격 수명이 없음”은 손상된 공격 ID와 다르며 정상 no-op이다.
2. 유효한 인스턴스만 tombstone으로 retire한다. 실제 유효 형식의 충돌·용량 초과·결과 누락은 계속 fail-closed한다.
3. 142개 중 111개만 공격한 회귀에서 retire=111, invalid retirement failure=0이어야 한다.

**상태**

`2026-09-07 실기 진단기 FAIL 확정 · 구현 교정 진행 중`

---

## 2026-09-07  C3 미지원 타입의 default scope로 기존 공격 VFX까지 차단

**무엇을 틀렸나**

C3에서 아직 `Unresolved`인 StreamSpirit은 정규 공격 token/scope를 만들 수 없는데도 공통 공격 RPC에 `impactEnabled=true`와 default scope를 전달했다. `UnitView.OnAttackHit`은 모든 네트워크 일반 공격에 유효 scope 소비를 요구하므로 lease arm/consume 실패 뒤 VFX 호출 전에 반환했다. 비교 대상에서만 격리해야 할 미완료 유닛의 기존 Legacy 공격 VFX를 새 fail-closed gate가 지워 버렸다.

**왜 그랬나**

`Supported` 타입의 provisional·중복 marker를 막는 안전 규칙을 전체 유닛에 동일 적용했다. “정규 scope가 없으면 새 표현을 승인하지 않는다”와 “아직 이관되지 않은 타입의 기존 표현을 보존한다”는 서로 다른 전환 상태를 타입 지원 여부로 나누지 않았다. 호출부 주석은 Shadow 실패가 기존 VFX를 끄면 안 된다고 했지만 실제 계약은 `impactEnabled=true + default scope`라는 모순 상태였다.

**어떻게 드러났나**

- 세션 `a13eb957…dd898`에서 사용자가 2단계 물정령 StreamSpirit의 공격 VFX 부재를 육안으로 확인했다.
- 로그는 StreamSpirit Unit 26이 공격 시작 gate `Ready`까지 도달했으며 resolver가 `Unresolved / ProjectileImpact / projectile-timeline-unresolved`로 분류했음을 함께 기록했다.
- UnitEffectConfig → EffectPreset_StreamSpirit_Attack → vfx_streamspirit_attack 프리팹 참조는 모두 정상이라 에셋 누락이 아니었다.
- 코드 추적에서 `ExecuteAttack`은 미지원 프로필에 default scope를 반환하고, `OnAttackHit`은 그 scope를 소비하지 못해 `EffectManager.PlayUnitAttack` 전에 종료됐다.

**교훈 및 교정 계약**

1. 공격 프로필 지원 상태를 marker gate의 명시적 입력으로 사용한다. `Supported`와 `Unresolved`를 같은 fail-closed 경로에 넣지 않는다.
2. `Supported`는 유효 정규 scope를 정확히 한 번 소비해야 VFX·SFX·Tracer를 방출한다.
3. `Unresolved`는 정규 scope나 지원 성공을 위조하지 않고 C3 비교 분모에서 격리하되, 이관 완료 전 기존 Legacy VFX·SFX·Tracer를 보존한다.
4. `impactEnabled=true + default/0 scope` 상태를 생성하지 않는다. 불가능한 조합은 생산 코드와 self-validation 양쪽에서 거부한다.
5. 회귀는 StreamSpirit 형태의 미지원 marker가 Legacy VFX를 1회 방출하는지와 Supported 타입의 무효/소모 scope가 아무것도 방출하지 않는지를 함께 검사한다.
6. 에셋 참조 감사만으로 VFX PASS를 선언하지 않는다. 실제 공격 marker에서 EffectManager 호출까지의 runtime 경로를 확인한다.

**상태**

`2026-09-07 실기 FAIL·원인 확정 · 규칙/Research/Plan/QA 로그 반영 · 구현 교정 대기`

**[2026-09-22 후속 교정·실기 증거 — 기존 사건 기록 유지]**

- StreamSpirit production marker와 서버 TimerImpact 설정을 `0.50초`로 정렬하고, exact controller/clip/Blue·Red prefab/VFX preset 연결 및 `Unresolved / LegacyFallback` 보존을 Unit Action self-validation에 고정했다. Unit Action과 Root Pose Cross Audit self-validation은 PASS했고 최신 코드 C# 컴파일 오류는 0건이었다.
- 공통 세션 `c50c68f41779b35a5a63f32479e8ad88b4886c71205ad1dba0aff69e5c0c9ad7`의 두 경기에서 Host VFX `603/603`, Client VFX `603/603`이 실제 시작됐으며 재생 실패·unmatched·duplicate·overflow는 0이었다. 각 결과는 `particleSystems=1`, `playbackActive=True`였다.
- terminal incomplete는 network-despawn terminal에 남은 미완료 회차이며 `vfxFailures`로 분류되지 않았다. dropped 686·817의 상세 로그 한도 때문에 각 미완료 원인이 단순 진행 중, 정상 취소, 타겟 소멸 중 무엇인지는 이번 자료만으로 확정하지 않는다. 일반 UAS END의 3/25·4/25 coverage와 dropped 한도에 따른 FAIL도 StreamSpirit 전용 실패가 아니다.
- 이 증거로 2026-09-07의 Legacy VFX 차단 회귀는 production marker/config + LegacyFallback focused 범위에서 PASS로 교정됐다. 다만 서버 권위 발사체, 발사/착탄 exact-key, full 25-type role-swap/rollback은 미완료이므로 사건을 삭제하거나 StreamSpirit 전체 이관 완료로 바꾸지 않는다. Client 최대 marker `0.732351초`는 timing variance로 남기되 VFX 누락으로 확대 해석하지 않는다.

---

## 2026-09-09  C3 확정 결과를 레거시 피해 이벤트와 살아 있는 View에 다시 의존시켜 피격 연출 누락

**무엇을 틀렸나**

C3 완료 묶음에 피해자 ID·피해량·결과 HP·Impact 위치는 넣었지만 피해자 타입과 팀 같은 필수 표현 스냅샷은 넣지 않았다. 실제 `HitPresentationQueue`는 별도 `OnEntityDamaged` 이벤트에서 보관한 `IDamageable` 또는 현재 Factory/UseCase 조회가 성공해야만 HP 텍스트와 피격 VFX를 준비할 수 있었다. 서버가 이미 확정한 과거 타격 표현을 현재 살아 있는 도메인 객체와 View의 수명에 다시 묶은 것이다.

**왜 그랬나**

레거시 FIFO에서 C3 단일 결과 방출자로 전환하면서 결과 키·시간·완결 묶음·중복 방지는 교정했지만, 마지막 Presentation adapter의 입력 완결성은 검증하지 않았다. 자동 검증도 `OnEntityDamaged`를 먼저 직접 발행해 피해자 캐시를 만들어 둔 뒤 결과 묶음을 넣었기 때문에 실제 멀티플레이의 결과/HP RPC 순서 역전과 사망·Despawn 뒤 소비를 재현하지 못했다. AoE는 한 피해자 준비 실패가 묶음 전체를 보류하고 만료 시 모든 시각 결과를 미표시로 닫아 누락을 확대할 수 있었다.

**어떻게 드러났나**

- 동일 경기 `sharedSessionKey=afc490a9d344ef1dd2ea827a5eeea872fca2b049fc621bb043b6961c6534138d`, Editor Host / Android Client, `ResultPresentation` 모드와 C3 v6 스키마가 일치했다.
- C2는 서버 결과 863건/실패 0, Client 수락 863건/거부 0이었다. 완료 묶음도 양측 863/863, pending·duplicate·transport failure 0으로 결과 생성과 전달은 정상이다.
- 화면 표시 대상 800건 중 Host는 747건 표시/53건 `viewUnavailable`, Client는 737건 표시/63건 `viewUnavailable`이었다. 각 측에서 표시+미표시가 정확히 800이므로 결과 유실이 아니라 최종 표현 소비 실패다.
- 경기 중 유닛 사망 44건과 건물 사망 2건이 있었고 Host 누락 수가 이 경계와 가깝다. 다만 기존 terminal은 개별 키와 실패 사유를 남기지 않아 사망·RPC 순서·초기화 중 각 원인의 정확한 분포는 확정할 수 없다.
- Android 경기 중 전투 예외·크래시·ANR은 0이었다. 종료 후 Lobby RemovePlayer 404 1건은 별도 정리 문제다.

**교훈 및 교정 계약**

1. 확정 결과 표현 입력은 피해자 타입·팀·Impact 위치·피해량·결과 HP를 포함해 그 값만으로 필수 HP 텍스트와 피격 VFX를 만들 수 있어야 한다.
2. C3 정상 경로는 `OnEntityDamaged`, `NetworkHealthSync` 재발행, 현재 Domain/Factory/View 조회를 필수 선행 조건으로 사용하지 않는다. 이 경로들은 권위 상태 수렴과 미지원 Legacy 표현에만 남긴다.
3. 살아 있는 View의 펀치 반응은 동일 객체 수명이 확인될 때만 추가하는 선택 표현이다. View가 사라져도 저장된 Impact 위치의 필수 표현을 누락시키지 않고, 재사용 ID의 새 객체에는 과거 반응을 적용하지 않는다.
4. AoE 완결 묶음의 원자성은 결과 집합의 완결성과 동일 시각 방출을 뜻한다. 한 피해자의 현재 View 부재가 다른 피해자의 필수 표현까지 막게 하지 않는다.
5. 회귀는 캐시를 미리 채운 성공 사례가 아니라 `결과→HP`, `HP→결과`, 치명타 제거 뒤 결과, View 부재, ID 재사용, AoE 일부 View 부재의 실제 순서를 모두 실행한다.
6. `viewUnavailable` 하나로 원인을 뭉치지 않는다. 필수 스냅샷 손상, presenter 미초기화, HP 텍스트 채널 실패, VFX 설정 부재, 선택 View identity 불일치를 별도 bounded evidence로 남긴다.
7. 재시도 시간 연장, 허용치 완화, 누락 분모 제외로 PASS를 만들지 않는다. 필수 표현의 현재 객체 의존 자체를 제거한다.

**상태**

`2026-09-09 실기 FAIL·구조 원인 확정 · 규칙/Task/구현 교정 진행 중`

---

## 2026-09-09  피해 위치를 fact에만 추가하고 C2 주 결과는 빈 위치로 남겨 적중 묶음 1,059건을 거부

**무엇을 틀렸나**

C3 self-contained 스냅샷을 만들면서 피해 적용 경계의 보조 `AppliedAttackPresentationFact`에는 대상 위치를 추가했지만, 같은 사건의 정규 C2 `AttackImpactResult`는 계속 `HasImpactPosition=false`로 생성했다. 완료 묶음의 주 결과는 C2 값을 그대로 사용해, v7이 요구하는 시각 결과 위치 계약에서 모든 적중 결과를 거부했다.

**왜 그랬나**

전송 DTO와 최종 presenter 입력을 중심으로 검증하고, 실제 production의 `피해 writer → C2 결과 생성 → C3 묶음 조립` 전체 데이터 흐름을 한 회귀로 묶지 않았다. 완성된 위치 입력을 직접 만든 테스트가 통과하자 실제 호출부도 위치를 제공한다고 잘못 일반화했다. 또한 코드에 미래 피격 VFX 경로가 있다는 사실을 현재 에셋이 실제 존재한다는 뜻으로 설명해, `presentation` 로그의 범위를 사용자에게 부정확하게 전달했다.

**어떻게 드러났나**

- 동일 v7 경기 `dae3d622…ea755`에서 C2 서버 결과 1,251건/실패 0, Client 수락 1,251건/거부 0이었다.
- Miss 계열 묶음 192건만 전달되고, 적중 1,059건은 모두 `server-completed-bundle-publish-rejected`로 거부됐다. `192 + 1,059 = 1,251`로 C2 전체 결과와 정확히 일치한다.
- 코드 대조에서 `AppliedAttackPresentationFact`는 위치를 보유하지만 `CompleteLegacyImpact`의 `AttackImpactResult.TryCreate`는 위치 존재값을 항상 false로 넘겼다.
- `UnitEffectConfig.asset`의 모든 `hitPreset`은 비어 있어 현재 실제 피격 VFX 에셋은 없고, 해당 경로는 미래 선택 채널임을 확인했다.

**교훈 및 회귀 gate**

1. 새 필드를 추가할 때 DTO 왕복만 검사하지 않고 실제 writer에서 최종 소비자까지 production 데이터 흐름을 한 회귀로 실행한다.
2. C2 권위 결과와 C3 주 피해 fact가 같은 사건을 나타내면 피해량·HP뿐 아니라 Impact 위치도 일치해야 한다.
3. 적중 결과는 서버 피해 경계에서 실제 Impact 위치를 소유하고, C3 조립기가 현재 View 조회나 임의 좌표로 보완하지 않는다.
4. 선택 에셋 미설정은 정상 생략, 설정 후 방출 실패는 오류로 별도 집계한다. 미래 코드 경로를 현재 존재하는 화면 효과라고 설명하지 않는다.
5. 경기형 회귀에서 `HitApplied 전송 성공 수 + Miss 전송 성공 수 = C2 전체 결과 수`, transport failure 0을 확인한다.

**상태**

`2026-09-09 v7 실기 FAIL·원인 확정 · 규칙/Task/QA 로그 반영 · writer-owned 위치/타입/팀 및 선택 VFX 분류 구현 · 정적 컴파일/문서 검사 PASS · Unity 메뉴 회귀/빌드 대기`

---

## 2026-09-09  Host 공격 진입을 되돌아오는 ClientRpc에 맡겨 Walk 고착을 재발시킴

**무엇을 틀렸나**

공격 사거리 진입에서 서버 Root의 위치 정지와 Action 회전 소유권은 Host 로컬 gameplay 경계에서 열었지만, Host Animator의 provisional Attack 시작은 `StartCombatClientRpc`가 되돌아와야 실행되도록 남겼다. 동시에 지연된 서버 target 이벤트가 닫힌 Action 소유권을 되살리지 못하게 하는 가드를 추가했다. 정상 시작 표현과 stale 이벤트 방어를 같은 비동기 통로에 놓은 결과, 늦은 정상 start도 stale로 버려져 위치는 멈췄는데 Walk가 계속 재생될 수 있었다.

**왜 그랬나**

서버 권위 gameplay 전환과 Host 화면 표현 전환을 분리해야 한다는 말은 맞지만, 그것을 “서로 다른 시간에 실행해도 된다”로 구현했다. Host의 `UnitAnimState` callback은 의도적으로 no-op인데도 로컬 Attack 표현의 유일한 시작점을 ClientRpc 수신에 맡겼다. 자동 검증은 닫힌 Action 소유권이 늦은 이벤트를 거부하는지만 확인하고, 유효한 최초 진입이 네트워크 왕복 없이 반드시 한 번 표시되는지는 실행하지 않았다.

**어떻게 드러났나**

- 같은 v8 경기 `sharedSessionKey=ae3ad93f…2dfdb`에서 Android Host 이동 terminal은 `ignoredServerTargetEvents=1103`을 기록했다. 저장된 bounded 상세에도 start 무시가 최소 46건 있었다.
- 사용자는 여러 유닛의 공격 직전 순간 멈춤과 불정령 2단계의 접근 후 Walk 지속·공격 부재를 육안으로 확인했다.
- EmberSpirit Unit 47은 `16:12:52.841`에 Unit 53 대상 `Misaligned(39.982°)`였고, `16:12:56.819`에는 사거리 안에서 3.098초 이상 Root가 안정 정지했지만 화면 Walk가 계속됐다. `16:12:57.645`에 사망했다.
- 같은 경기의 C3 결과는 `ready=3311`, `failures=0`, `expectedVisual=2968`, `presentationEmits=2968`, `viewUnavailable/duplicate/transport failure=0`으로 수렴했다. 따라서 결과 표현 C3가 아니라 그 앞의 이동→공격 진입 통합 경계가 실패했다.
- 진단기는 ignored event를 terminal에 출력만 하고 실패 조건에 넣지 않아 이 상태를 `EVIDENCE`로 종료했다.

**교훈**

1. 공격 사거리 진입의 서버 위치 정지, Action 회전 소유권, Host provisional Attack 표현은 같은 동기 gameplay 경계에서 원자적으로 적용한다.
2. ClientRpc는 원격 Client 전달에 사용하고, Host의 필수 표현 시작을 자기 RPC 왕복에 의존시키지 않는다.
3. stale 이벤트 거부를 검증할 때는 반대 불변식인 “유효한 로컬 start는 정확히 한 번 수락”도 같은 회귀에서 검사한다.
4. `ignoredServerTargetEvents`를 정상 노이즈로 뭉치지 않는다. start/change/stop과 expected/unexpected를 나누고 예상 밖 start 무시는 terminal FAIL로 승격한다.
5. Root 정지와 Animator 상태를 함께 관측한다. `NoIntent + Walk advancing` 또는 공격 handoff 뒤 Walk 고착은 집계 1건부터 실패다.
6. 타입별 1회 표본만으로 개별 고착을 분석하지 않는다. 이상 유닛은 target/gate/owner/anim/revision/commit/result 수명을 bounded 연속 증거로 남긴다.
7. C3 한 축의 수렴을 전체 유닛 행동 PASS로 일반화하지 않는다. 이동→정렬→공격→결과→표현의 선행 축 중 하나라도 실패하면 전체는 FAIL이다.

**상태**

`2026-09-09 v8 실기 전체 FAIL·구조 원인 및 검증 공백 확정 · Task 16_49 Research/Plan 작성 · 원자적 Host 공격 진입과 진단기 보강 구현 · v11 실기에서 ignored start/Walk 고착 0 및 C2/C3 수렴 확인 · 후속 target-change staging 교정 중`

---

## 2026-09-09  Action 전 후보 타겟을 실제 ChangeTarget으로 조기 공개

**무엇을 틀렸나**

`TickCombat`이 이동 Action보다 먼저 적 후보를 찾는 정상 순서를 허용하면서도, 그 후보를 저장한 직후 `TryApplyServerCombatTarget`과 `ChangeTargetClientRpc`를 무조건 호출했다. `AcquireTarget`의 변경 가능한 서버 후보와 Host/Client에 공개된 활성 공격 타겟을 같은 상태로 취급했다.

**어떻게 드러났나**

- v11 같은 경기 `sharedSessionKey=248f53d7…eb23f`에서 원자적 Host start 회귀는 사라졌고 `ignoredServerTargetEvents=0`, `stationaryWalkViolations=0`이었다.
- C2 2,035건/실패 0, C3 `expectedVisual=1867`/`presentationEmits=1867`, 각 실패 계수 0으로 결과·표현 경계는 정상이었다.
- 이동 terminal만 `combatPresentationHandoffFailures=6`으로 실패했고 Unit 5·21·124·142·97·82 전부 `TickCombat → PublishCombatTargetChange`, `boundary=target-change`였다.
- provisional/commit 적용 실패는 0이므로 View adapter 결함이나 C3 회귀가 아니라 Action 전 조기 공개 한 경계로 원인이 좁혀졌다.

**교훈 및 회귀 gate**

1. 서버 후보 발견은 공개 명령이 아니다. Action 소유권 전에는 후보만 보존하고 Host View와 Client RPC를 변경하지 않는다.
2. 최초/재진입은 provisional Start의 원자 payload가 target을 전달한다. 같은 target에 별도 Change RPC를 덧붙이지 않는다.
3. 전투 연계 target 변경은 기존 impact lease를 닫고, Action 준비 확인 뒤 Host 적용 성공 시에만 원격 Change RPC를 발행한다.
4. 준비 전 보류는 정상 deferred 계수, 준비 후 적용 실패는 terminal FAIL로 분리한다. 진단 failure를 무시하거나 허용치를 늘려 통과시키지 않는다.
5. pending/revision을 Action 준비 전에 선점하면 이후 정상 `OnUnitEnteredCombat`이 막힐 수 있다. 실제 원자 적용 성공 뒤에만 presentation 상태를 커밋한다.
6. C2 피해 writer와 C3 결과 표현은 증거상 정상인 경우 원인 없이 함께 수정하지 않는다.

**상태**

`2026-09-09 v11 실기 부분 PASS·target-change 조기 공개 6건 원인 확정 · Research/Plan/실수 기록 반영 · v12 후보/공개 타겟 분리 구현 및 정적 컴파일 완료 · Unity 메뉴 self-validation 대기`

---

## 2026-09-13  문서의 원자 target 전달을 production event seam 확인 없이 완료 처리

**무엇을 틀렸나**

기존 전용 기록과 Task Plan에 “최초/재진입 provisional Start의 단일 payload가 정확한 target을 원자 전달한다”고 적고 해당 교정이 구현된 기반으로 취급했다. 그러나 production의 `GameEvents.OnUnitEnteredCombat`은 계속 `Subject<int>`라 `unitId`만 전달했다. `UnitView`가 이동 후보에서 이미 확정한 `targetId`와 `targetIsUnit`은 이벤트 경계에서 버려졌고, `NetworkCombatController`는 이동 commit 전 현재 위치에서 target을 다시 검색했다.

**왜 그랬나**

`UnitView` 내부에 target을 보유하고 Host provisional Start 함수도 target 인자를 받는다는 부분 구현을 보고, 이벤트 payload까지 원자적으로 연결됐다고 일반화했다. 문서 문장과 실제 production 호출 경계를 `발행 형식 → 구독 시그니처 → 수신 값 사용` 순서로 대조하지 않았다. self-validation도 완성된 target 입력을 직접 만들어 정책·receipt를 검사했기 때문에 `후보 위치에서 범위 진입 → target 유실 → 현재 위치 재검색` 순서를 실행하지 않았다. 후속 Task는 이 미구현 seam을 정상 기반으로 가정하고 검증기·Observer·schema를 더 확장했다.

**어떻게 드러났나**

- 사용자 실기에서 근거리 유닛들이 공격하지 않았고 대미지 텍스트 위치는 정상으로 확인됐다.
- 같은 경기 `sharedSessionKey=993718220d45194886d85f22bd4e8cade81e2992baf80a5152b7127a2fd69a3e`의 Editor Host 로그 `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt` 7331행은 `combatPresentationHandoffFailures=87`, `heldFrames=423`, 저장 상세 64건, overflow 23건을 기록했고 7336행은 최종 `FAIL`이다.
- 저장 상세 64건은 모두 `client-visible-entry-order`이며 같은 경기 UnitId와 생성 타입을 대조하면 `LittleKnight` 14, `EmberSpirit` 14, `FlameSpirit` 6, `DustSpirit` 12, `TideSpirit` 14, `SpearMan` 4건이다. 근거리 계열이라는 사용자 관찰과 일치한다.
- 반면 Host의 서버 공격 결과는 2,298건/실패 0이고 필수 표현은 2,036/2,036건이다(7338~7339행). Client도 결과 수락 2,298건/거부 0, 필수 표현 2,036/2,036건이다(`Assets/_Project/Docs/_Logs/2026-09-13/05_19_logcat/RuntimeLog_device.txt` 132619·132630행). 피해·대미지 텍스트가 정상인 것은 근거리 공격 진입 실패와 모순되지 않는다.
- 코드 대조에서 이벤트 형식은 `GameEvents.cs` 1028~1033행, 후보 target 보유와 commit 전 callback은 `UnitView.cs` 1368~1388·4207~4212행, unitId-only 발행은 5123~5146행, 현재 위치 재검색은 `NetworkCombatController.cs` 2436·2472~2477행에서 확인됐다.

**교훈 및 회귀 gate**

1. 문서에 “원자 payload”라고 기록하기 전에 실제 production의 이벤트/DTO 형식, 발행 인자, 구독 시그니처, 수신 측 사용 값이 동일 target을 끝까지 전달하는지 한 줄씩 대조한다.
2. 이동 후보 위치에서 확정한 target은 `unitId + targetId + targetIsUnit` typed payload로 직접 전달한다. 최초/재진입 handoff 수신 측에서 현재 위치 기반 target 재검색을 하지 않는다.
3. 후보 위치가 공격 범위 안이고 현재 위치는 범위 밖인 근거리 경계를 production 순서 그대로 재현하지 못한 self-validation PASS를 최종 성공 증거로 사용하지 않는다.
4. 자동 검증·Observer·schema 확장으로 이 결함을 우회하지 않는다. 실제 코드 seam 하나를 최소 교정하고 사용자 멀티플레이 실기로 확인한다.
5. 피해 writer, 대미지 텍스트, 피격 VFX처럼 같은 경기에서 정상 증거가 있는 경로는 원인 없이 수정하지 않는다.
6. handoff 실패가 1건이라도 있거나 근거리 유닛이 공격하지 않으면 전체 유닛 행동은 FAIL이다. 제한된 공격 coverage나 C3 수렴을 전체 PASS로 일반화하지 않는다.
7. 후속 구현과 빌드 전에 변경 범위를 유지하고, 사용자 실기 전에는 완료로 기록하지 않는다. 빌드는 실행만 하며 완료 확인은 사용자가 담당한다.

**상태**

`2026-09-13 최신 실기 FAIL·production event seam 미구현 원인 확정·Task Research/Plan/전용 실패 기록 반영·후속 최소 구현 미착수·사용자 실기 전 완료 금지`

**후속 교정 결과 — 2026-09-13**

과거 FAIL 기록은 위와 같이 보존한다. 이후 `UnitId + TargetId + TargetIsUnit` typed payload를 production 이벤트 발행·구독 전체에 연결하고 공격 진입 handler의 현재 위치 기반 재검색을 제거했다. 전체 self-validation PASS와 Android 빌드 뒤, 사용자가 이전 실패와 동일한 유닛 범위로 Editor Host + Android Client를 재시험했다.

- 공통 키 `abe1fb66044f1aa5efbb99d4420fa901e48c927ebbf3c24ef30b72f181c1f5c1`
- 동일 근거리 6종 모두 accepted shadow-commit
- `combatPresentationHandoffFailures=87 → 0`, `stationaryWalkViolations=0`, 실패 상세·overflow 0
- Action 전 정상 보류 `deferredCombatTargetChanges=2`, 실제 적용 실패·ignored event 0
- C2 Host 2,703/실패 0 ↔ Client 2,703 수락/거부 0
- C3 필수 표현 Host/Client 2,410/2,410, 공간 mismatch 0
- ROOT PASS, error/drop 및 `ERROR`·`FATAL`·`Exception` 0

`coveredUnitTypes=9/25`는 과거와 동일한 의도적 비교 범위다. 나머지 16종을 이번 Task의 누락으로 재분류하지 않는다. production seam 교정은 **PASS / CLOSED**이며, 전체 25종·역할교대·Legacy rollback은 별도 통합 회귀 gate로 남는다.

---

## 2026-09-14  production marker와 config 불일치 상태에서 Supported 승격을 문서 상태만으로 판단할 위험

**무엇을 틀렸나**

QuakeSpirit의 영구 문서에는 `OnAttackHit` 미주입과 1.0초 placeholder가 남아 있었지만 실제 production Attack clip에는 이미 1.667초 marker가 있었다. 반대로 런타임 `UnitStatsConfig`는 여전히 1.0초였다. 문서, clip, config 중 하나만 보면 “미지원 유지” 또는 “지원 승격 가능” 중 어느 쪽도 잘못 결론낼 수 있는 상태였다.

**왜 위험했나**

Supported 승격은 단순 enum 변경이 아니라 실제 Animator Attack state가 사용하는 clip, 그 clip의 marker, 양 진영 프리팹 연결과 서버 타임라인 config가 같은 occurrence를 가리켜야 성립한다. 이 연결을 영구 회귀로 고정하지 않으면 이후 에셋 또는 config 변경으로 다시 어긋나도 manifest 숫자만 정상으로 보일 수 있다.

**어떻게 교정했나**

- 1회성 Unity 셋업이 실제 Attack clip·Controller·Blue/Red 프리팹 연결과 `OnAttackHit=1.667`을 확인한 뒤 `UnitStatsConfig`를 1.0→1.667로 저장했다.
- QuakeSpirit을 `Supported / MeleeContact / Impact 1 / secondary true`로 승격하고 manifest를 16/8/1로 갱신했다.
- 영구 Unit Action self-validation이 production asset 연결, marker 1개/1.667초와 config 1개/1.667초를 직접 읽어 불일치 시 fail-closed하도록 했다.
- Unit Action A1/B2/C2/C3와 Root Pose Cross Audit은 errors 0 PASS했다. Android 실기는 아직 대기다.

**교훈 및 회귀 gate**

1. 유닛을 `Supported`로 승격하기 전에 실제 production의 `프리팹 → Controller → 정확한 Attack state clip → marker` 연결과 런타임 config를 한 검증에서 함께 읽는다.
2. marker 개수·시각 또는 config 행·시각이 다르면 manifest count를 맞추기 위해 우회하지 말고 self-validation을 즉시 실패시킨다.
3. 에셋/설정 일치를 확인하지 않은 resolver·fixture 단독 PASS는 Supported 승격 근거가 아니다.
4. 자동 게이트 PASS는 실기 타격 시점·AoE·반복 공격 PASS가 아니다. Android 사용자 확인 전 Task는 OPEN으로 둔다.

**상태**

`2026-09-14 production marker/config 영구 fail-closed 회귀 구현 · Unity 자동 게이트 PASS · Android Build And Run 시작 · 사용자 실기 대기 / CONDITIONAL PASS / OPEN`

---

## 2026-09-20  명시값 공백이 있는 UnitType을 enumValueIndex로 비교해 다른 유닛을 교정하고 영구 gate도 오판

**무엇을 틀렸나**

InfernoSpirit의 production `hitFrameTimes`를 1.15초에서 0.50초로 교정하는 임시 Unity 스크립트가 `SerializedProperty.enumValueIndex`로 `UnitType` 행을 찾았다. `UnitType`은 명시값에 공백이 있으므로 선언 순서와 실제 직렬화 값이 일치하지 않는다. 그 결과 InfernoSpirit(`UnitType 12`)은 그대로 두고 BoulderSpirit(`UnitType 14`)을 0.50초로 잘못 변경했다.

같은 선택 오류가 영구 `RunUnitActionSelfValidation.cs`의 InfernoSpirit `UnitEffectConfig` 행 검색에도 남아 있었다. production preset 자체는 정상인데 다른 행을 검사하여 `InfernoSpirit must retain the verified production attack preset.`으로 실패했다.

**왜 그랬나**

Unity `SerializedProperty`의 enum API에서 `enumValueIndex`를 실제 enum 숫자로 오해했다. 임시 교정기의 저장 결과만 다시 확인하고, 동일한 타입 행을 선택하는 영구 gate의 조건까지 같은 기준으로 대조하지 않았다. 따라서 일회성 교정기와 영구 검증기가 서로 다른 잘못된 행을 선택할 수 있는 공통 결함이 남았다.

**어떻게 드러났나**

- 첫 자동 교정 뒤 InfernoSpirit는 여전히 `1.15`, BoulderSpirit은 잘못된 `0.50`이었다.
- 수정 스크립트가 `intValue`로 두 행을 다시 선택해 InfernoSpirit=`0.50`, BoulderSpirit=`1.15`를 원자 검증·복구했다.
- 저장값 교정 뒤 영구 Unit Action 검증은 InfernoSpirit production preset 보존 오류로 실패했고, 해당 행 선택에 남은 `enumValueIndex` 비교가 확인됐다.
- 영구 gate도 `intValue`로 바꾼 재실행에서 production timeline, 전체 Unit Action, Root Pose Cross Audit이 모두 PASS했다.

**교훈 및 회귀 gate**

1. Unity `SerializedProperty` enum 필드에서 `enumValueIndex`는 선언 순서이고 `intValue`가 실제 직렬화 값이다.
2. 명시값에 공백이 있는 `UnitType` 비교에는 반드시 `intValue`를 사용한다. 배열 인덱스나 선언 순서를 타입 ID로 취급하지 않는다.
3. 설정을 쓰는 임시 교정기와 같은 설정을 읽는 영구 gate의 행 선택 조건을 함께 대조한다. 한쪽만 올바르면 안전한 교정이 아니다.
4. 단일 대상 값만 확인하지 않는다. 잘못 선택될 수 있는 인접·대조 유닛의 기존 값도 같은 저장·재로드 단계에서 원자적으로 검증하고, 다르면 빌드로 진행하지 않는다.
5. 자동 검증 오류 문구가 production 사실과 충돌하면 에셋을 다시 고치기 전에 검증기가 실제 어떤 enum 행을 선택했는지 확인한다.

**교정 및 검증 결과**

- InfernoSpirit `hitFrameTimes=[0.50]`, BoulderSpirit `hitFrameTimes=[1.15]` 저장값 재검증 PASS.
- `[UAS-DIAG] self-validation PASS` 및 `[UAS-DIAG][PRODUCTION-TIMELINE] PASS: QuakeSpirit, BattleAxe, InfernoSpirit`.
- `[UAS-ROOT-CROSS-AUDIT] self-validation PASS`.
- Android Build And Run은 실제 URP preprocess/build pipeline에 진입했다. 완료와 사용자 실기는 확인하지 않았으므로 상태는 `빌드 시작 · 사용자 실기 대기 / OPEN`이다.
- 임시 교정 스크립트는 빌드 호출 반환 뒤 삭제되도록 설계되어 빌드 진행 중 존재할 수 있으며, 삭제 완료로 기록하지 않는다.

**상태**

`2026-09-20 설정 및 영구 gate 교정 · Unity 자동 검증 PASS · Android Build And Run 시작 · 사용자 실기 대기 / OPEN`

---

## 2026-09-20  Root·Visual 정렬 PASS를 실제 Animator 몸 방향과 연속 이동 PASS로 일반화

**무엇을 틀렸나**

InfernoSpirit의 공격 방향 문제에서 기존 Root observer가 Simulation Root와 Visual Root의 정렬·투영을 통과했다는 사실을 실제 렌더링 몸 방향까지 정상인 근거로 사용할 위험이 있었다. 이동에서도 terminal·replication·Root 구조 실패 계수가 0이라는 결과만으로 사용자가 본 순간이동을 부정할 수 있었지만, 기존 observer는 같은 유닛의 연속 프레임 위치 delta와 Animator body 위치를 기록하지 않았다.

**왜 그랬나**

화면에 보이는 모델은 Visual Root 아래에서 Animator가 클립과 Humanoid retarget 결과를 적용한 최종 몸체인데, 기존 관측 경계는 그 상위 Transform까지만 확인했다. 또한 `initial/moved/stable` 표본은 장기 수렴에는 유효하지만 한 프레임 점프, 긴 프레임 뒤 catch-up, Client 보간 간격을 분리할 수 없다. 서로 다른 관측 책임을 “Root PASS”라는 하나의 결과로 뭉치면 미관측 축이 정상처럼 보인다.

**어떻게 드러났나**

최신 Editor Host/Android Client 공통 세션 `aa5875a11546bdb8183d87b7c22aec276396670aa12029df35c075ac7b54bcde`에서 InfernoSpirit `animated-facing`을 수집했다.

- 유효 표본은 Client Start 49 / Impact 28 / End 12, Host Start 52 / Impact 47이다.
- Attack 시작 Root/Visual target yaw 절대값은 평균 Client 8.30°·Host 8.18°, 최대 Client 41.83°·Host 35.45°였고 Animator body 최대는 Client 62.46°·Host 40.52°였다. 5° 정렬 완료 전에 Attack 전환이 보이는 표본이 존재한다.
- Impact에서는 Root/Visual이 Host 0.00°, Client 평균 0.07°·최대 0.47°로 수렴했지만 body↔Visual 잔여 yaw는 Host 평균 12.47°(11.80~14.18), Client 평균 약 13.38°(대략 11.77~16.24)였다. 상위 회전·복제와 Animator 몸체 오차가 분리됐다.
- 이동 rejected/invalid/gate/writer conflict, spatial commit failure, repeated/fatal repath, Client revision/order, Root projection error는 모두 0이었지만 사용자는 InfernoSpirit 이동 순간이동을 육안으로 확인했다.
- InfernoSpirit Blue/Red prefab은 `m_ApplyRootMotion=0`이나 Walk clip의 RootT X/Z 곡선과 Animator body/mesh 내부 이동은 기존 로그가 관측하지 않는다.

**교훈 및 회귀 gate**

1. Root/Visual PASS를 Animator가 평가한 실제 몸 방향·몸 위치 PASS로 일반화하지 않는다. 화면 결함은 Simulation Root, Visual Root, Animator body를 같은 사건에서 각각 측정한다.
2. 공격 시작과 Impact를 하나의 “방향 오차”로 합치지 않는다. visible Attack 시작 전 5° ready 여부와 Impact body↔Visual 잔여 yaw를 별도 결함으로 판정한다.
3. `m_OrientationOffsetY=-45`는 사용자 비교와 production 전제에 따라 유지한다. 잔여 12~14°가 clip 내부 `RootQ`/body 곡선인지 Humanoid retarget/import 결과인지 분리하기 전에 `-57/-58` 같은 보정값을 적용하지 않는다.
4. 회전 후속은 ① visible Attack 시작을 기존 5° ready gate 뒤로 옮기는 교정과 ② Impact 잔여 body yaw의 clip/import 원인 분리 후 orientation 확정을 별도 단계로 수행한다.
5. 순간이동 관측은 InfernoSpirit 전용으로 직전/현재 Simulation Root, Visual Root, `Animator.bodyPosition`, frame time, expected allowance를 메모리에서 비교한다. 정상 프레임은 출력하지 않고 임계 사건만 단일 구조 로그로 남긴다.
6. 사건은 `authoritative-root-jump`, `visual-projection-jump`, `animator-body-jump`, `long-frame-catch-up`, 증거가 있을 때만 `client-only-replication-gap`, 그 외 `unavailable/ambiguous`로 분류한다. 제공되지 않는 network receive timestamp를 사실처럼 만들지 않는다.
7. exact-key rate limit, bounded 저장·overflow, despawn/disable/retire 정리와 read-only observer 경계를 회귀 gate로 둔다.
8. 구조 실패 계수 0은 해당 계수의 PASS일 뿐 육안 증상의 부재 증거가 아니다. 연속 delta가 없으면 “정상”이 아니라 “관측 공백”으로 기록한다.

**상태**

`2026-09-20 facing evidence 실기 FAIL·공격 시작 선행 표현과 Impact body 잔여 yaw 분리 완료 · 이동 순간이동은 관측 공백 확인 · 기존 Task에 bounded motion-jump evidence 추가 승인·구현 전 / OPEN`

**후속 통제 실험 계약 — 2026-09-20 (기존 `-45°` 기록 보존)**

- motion-jump observer v11은 이후 구현되어 Runtime/Editor 정적 컴파일 오류 0건까지 확인됐지만 실기 로그는 아직 없으므로 순간이동 판정은 미검증이다.
- `-45°` Impact body↔Visual residual은 Host 평균 `+12.47°`(`+11.80° ~ +14.18°`), Client 평균 약 `+13.38°`(약 `+11.77° ~ +16.24°`)였다. 사용자는 이 약 `+13°` 실측에 근거해 Unity Inspector에서 orientation을 `-58°`로 직접 변경·저장했고, 디스크 `InfernoSpirit_Attack.anim`도 `m_OrientationOffsetY=-58`로 확인됐다.
- 이번 비교는 orientation 한 변수만 바꾸는 Impact 고정 잔여 오차 실험이다. Host/Client `ImpactMarker.bodyFromVisualYaw`를 별도로 비교하며 0° 근처 수렴을 기대한다. 음수 overshoot가 일관되면 `-57°` 같은 후속 후보는 새 승인 뒤 다시 한 변수로 비교한다.
- `-58°` 결과가 좋아도 정렬 전 provisional AttackStart의 Root/Visual 최대 `35~42°`, body 최대 `62°` 문제를 해결한 것으로 간주하지 않는다.
- 빌드 전 영구 self-validation의 production orientation 기대값을 `-58°`로 동기화하고 두 Unity 메뉴의 새 PASS를 확인한다. 실패를 무시하거나 검증을 완화하지 않는다.
- 코드, 공격 시작 정책, Walk, NetworkTransform, 이동, 성능은 이 비교에서 변경하지 않는다. 기존 `-45°` 표본과 판정은 삭제하거나 덮어쓰지 않는다.

**실기 계측 결과 누적 — 2026-09-21**

공통 세션 `d32663e1caed7fd16b38a4166bef66647bb50e6a28344db48b3706067028d9b1`의 motion-jump v11 실측으로 관측 공백 일부를 닫았다.

- Client 사건 72건은 `animator-body-jump` 61, `unavailable-ambiguous` 11이다. 9개 유닛이 각각 8건 제한에 도달했고 overflow도 9건이다. body delta 평균 `0.1442`, 최대 `0.4923`; Simulation/Visual 최대 `0.2023`; raw frame 평균 `0.0341s`, 최대 `0.0602s`; `longFrame > 0.1s` 0건이다.
- Host 사건 72건은 모두 `animator-body-jump`다. 이 중 각 유닛 최초의 `Animator.bodyPosition=(0,0,0) → 실제 위치` 9건은 initialization artifact이며 gameplay jump가 아니다. 제외한 63건의 body delta 평균은 `0.1501`, 최대는 `4.7021`이다.
- 핵심 유효 사건은 unit 86, `23:47:29.784`, `phase=NoIntent`, Animator `81563449 → 1130333774` 전환이다. Simulation/Visual delta는 모두 0이고 body delta만 `4.7021`이다. 이어진 Move 전환에서도 body delta `0.2688`이 기록됐다.
- Host Simulation Root 최대 `0.0309`이므로 authoritative root jump 증거는 없다. Client `0.2023`은 작은 replication/buffer snap 가능성을 남기지만 peer 자동 결합이 없어 현재 분류는 `unavailable-ambiguous`다. 확정 라벨로 꾸미지 않는다.
- Host/Client Root Pose `summary-END` PASS, errors/drop 0, 공격 표현 `417/417`, failures 0은 구조 회귀가 없다는 증거일 뿐 순간이동 해소 PASS가 아니다.
- `-58°` Client Impact는 `n=49`, signed/abs 평균 `+28.343°`, 범위 `+24.778° ~ +30.883°`, 8° 이내 0건이다. 이전 `-45°`의 `n=41`, 평균 `+13.370°`, 범위 `+11.766° ~ +15.940°`보다 악화됐다. AttackStart 평균도 `+19.333° → +25.199°`로 악화됐다.

**추가 교훈 및 회귀 gate**

9. `Animator.bodyPosition`은 Humanoid 몸 중심이지 visible mesh 위치의 계약이 아니다. body delta만으로 화면 메시 점프를 확정하지 말고 hips/root bone 또는 renderer bounds 기반 stable presentation anchor를 같은 frame에서 측정한다.
10. 최초 `(0,0,0)` body/anchor 표본은 initialization으로 분리하고 gameplay 사건·유닛별 event budget을 소비하지 않게 한다.
11. 선착순 8건 cap은 후반 Walk↔Attack 전환을 누락시킬 수 있다. phase-aware budget 또는 첫 severe transition event 보존을 둔다. 다만 진단기 보강만으로 종료하지 않고 실제 defect correction의 선행 seam으로 제한한다.
12. latest Host root가 정상 범위이면 서버 locomotion·경로·속도·repath를 추정 수정하지 않는다. 실제 render anchor가 튀면 animation/presentation child만 교정하고 `ApplyRootMotion=0`을 유지한다.
13. render anchor는 정상이고 Client gap만 paired evidence로 확인될 때만 표현 전용 보간을 설계한다. 전체 NetworkTransform 보간 비활성화와 Client Simulation Root writer 추가는 금지하며 현행 `Interpolate=true`, `PositionLerpSmoothing=false`, `LegacyLerp` 계약을 유지한다.
14. 이동 교정 중 공격 orientation을 함께 바꾸지 않는다. production/validator가 현재 `-58°`라는 사실과 그 값이 실기에서 악화됐다는 판정을 함께 보존하고, 방향은 별도 Task에서 한 변수씩 교정한다.

**상태 갱신**

`2026-09-21 motion-jump 실기 FAIL·Host 권위 이동 점프 증거 없음·Walk→Attack body 4.7021 불연속 확인·visible render anchor 미관측·-58° 방향 비교 악화·새 이동 교정 Task 작성 / 교정 구현 전 / OPEN`

**추가 직접 검증 — 2026-09-21: 진단 코드가 Android 경고를 매 프레임 발생시켜 테스트를 오염**

device 로그 `Assets/_Project/Docs/_Logs/2026-09-20/23_50_logcat/RuntimeLog_device.txt`를 경고 원문과 스택으로 다시 집계했다.

- Unity 경고 `Setting and getting Body Position/Rotation ... should only be done in OnAnimatorIK or OnStateIK`: **27,727건**.
- `UnitView:ObserveInfernoMotionJumpFrame()` 스택: **27,323건**.
- `UnityEngine.Animator:get_bodyPosition()` 스택: **27,314건**.
- 첫 발생은 `23:45:39.510`이며 이후 매 프레임 수준으로 반복됐다.
- 호출 원인은 `UnitView.LateUpdate()`가 `ObserveInfernoMotionJumpFrame()`을 호출하고 그 안에서 `_animator.bodyPosition`을 읽은 경로로 확정됐다.

경고·스택 폭주는 CPU·문자열·Android logcat I/O 비용을 추가하므로 이번 테스트 렉과 Client update 몰림을 크게 악화시켰을 가능성이 높다. 그러나 다른 성능 원인을 배제하지 못했으므로 전체 렉의 단독 원인이라고 확정하지 않는다. 같은 이유로 오염된 실행의 Client Simulation/Visual 최대 `0.2023`만으로 NetworkTransform 결함을 확정하지 않는다.

**추가 교훈 및 회귀 gate**

15. Unity가 특정 callback 안에서만 허용한다고 경고하는 Animator API를 일반 `Update`/`LateUpdate` 진단에서 읽지 않는다. `_animator.bodyPosition` 접근과 그 previous/current 상태를 완전히 제거한다.
16. stable rendered anchor는 초기화 때 캐시한 대표 `SkinnedMeshRenderer.rootBone.position`을 주 값으로 사용한다. `bounds.center`는 root bone 미가용 또는 메시 외곽 대조용 보조값이며 주 값을 조용히 대체하지 않는다.
17. 첫 유효 anchor는 baseline만 만들고 사건·per-unit budget을 소비하지 않는다. 매 frame renderer 탐색·배열 생성도 금지한다.
18. Android 재수집의 필수 gate는 body position/rotation Unity 경고 0건과 `ObserveInfernoMotionJumpFrame → get_bodyPosition` 스택 0건이다.
19. 경고 제거 전 Client gap 수치는 오염된 표본이다. 경고 0건 재수집에서도 gap이 남을 때만 paired Host/Client receive/interpolation 증거와 NetworkTransform 표현 분기를 검토한다.
20. 이 제거는 검증기 정리가 아니라 진단 코드가 실제 테스트 환경에 만든 렉·로그 오염을 제거하는 기능 교정이다. 자동 검증만 하고 종료하지 않는다.

**상태 재갱신**

`2026-09-21 Android Unity 경고 27,727건·LateUpdate bodyPosition 호출 원인 확정·성능 악화 강한 가능성/단독 원인 미확정·bodyPosition 완전 제거와 cached rootBone anchor가 최우선 / 교정 구현 전 / OPEN`

**1차 교정 후 재발 확인 — 2026-09-21: 같은 금지 API 계열을 부분 제거**

`bodyPosition`만 motion observer에서 제거한 뒤 자동 검증을 통과했지만, 공격 방향 진단에는 같은 Unity 제한 대상인 `Animator.bodyRotation`이 남아 있었다. 새 Android 실기 공통 세션 `7baaffab77f02b028c5257c17c0ab66dd2b2b9b7d1957acb3f05c9aae70ed83b`에서 `get_bodyPosition`은 0건이 됐으나 `Animator:get_bodyRotation`과 동일 Body Position/Rotation 경고가 각각 296건 발생했다. 발생 구간은 `13:24:45.592~13:26:20.216`이고 스택은 `UnitView.ObserveInfernoAttackFacing → ConsumePendingInfernoAttackStartFacing/LateUpdate`다.

이는 특정 호출 하나를 제거하면서 경고 문구가 함께 지목한 같은 API 계열과 다른 진단 경로를 전체 검색하지 않은 범위 누락이다. 27,727건의 매 프레임 폭주는 없어졌지만 공격 구간 표본은 계속 오염됐다. 자동 self-validation PASS는 production hierarchy와 limiter 계약만 확인했으며 실제 Android에서 금지 API 경고 0건을 증명하지 못했다.

같은 실기에서 Host Simulation Root는 긴 프레임을 제외하면 권위 점프 증거가 없었지만 Client Root 최대 `0.4744`, Client rendered-anchor 최대 `0.4845`, 정지 Host Walk↔Attack anchor 최대 `0.1067`이 남았다. 또한 Android motion line이 `rendererBounds` 중간에서 잘려 phase·transition·allowance가 유실됐다. 따라서 Root Pose periodic PASS를 sub-frame 이동 PASS로 일반화하거나, 절단된 로그만으로 Client NetworkTransform 결함을 확정해서는 안 된다.

**추가 교훈 및 회귀 gate**

21. Unity 경고가 Body Position/Rotation API 계열을 함께 지목하면 최초 스택의 단일 호출만 고치지 않는다. `bodyPosition`, `bodyRotation` 및 그 getter를 모든 `Update`·`LateUpdate`·marker·진단 경로에서 전체 검색하고 Android 실기 0건을 확인한다.
22. Facing 증거를 바꿀 때 cached Hips/rootBone의 `Transform.forward` 또는 bone rotation이 기존 body 방향과 같은 의미인지 먼저 검증한다. 이름만 바꾼 대체값으로 자동 gate를 통과시키지 않는다.
23. Root-to-anchor 수평 오프셋과 같은 frame Root 회전 delta를 함께 기록해 관측 anchor 이동이 회전 원호 예측과 맞는지 판정한다. prefab hierarchy와 clip/retarget pose를 동시에 수정하지 않는다.
24. Android 한 줄 제한 뒤에 판정 필수 필드를 배치하지 않는다. classification·unit/lifecycle·phase·transition·allowance·핵심 delta는 앞쪽 compact line에 두고 보조 벡터는 별도 bounded detail로 분리한다.
25. 경고 0과 필수 필드 무절단 재실기 전에는 Client Root의 큰 delta를 NetworkTransform 결함으로 확정하지 않으며 현행 production 설정을 즉시 변경하지 않는다.

**상태 재갱신**

`2026-09-21 1차 Android 재검증 FAIL·bodyPosition 0건이나 bodyRotation 경고 296건 잔존·Client root clumping 강한 증거/peer join 부재·정지 Walk↔Attack anchor displacement 확인·2차 교정 대기 / OPEN`

---

## 2026-09-22  일반 Stop이 이미 커밋된 source marker까지 닫아 Client 공격 VFX를 누락

**현상**

InfernoSpirit 공격 VFX가 Client에서 간헐적으로 보이지 않았다. 실패 기준선은 같은 경기 raw Animation Event 70/70이었지만 Host started 70, Client started 61·gate-suppressed 9였다. VFX prefab, pool item, ParticleSystem은 정상 재생 가능 상태였다.

**원인**

`StopCombatAnimation`과 타겟 사망 처리가 공격 클립을 즉시 끝내지 않으면서 presentation scope를 먼저 닫았다. 네트워크 Stop이 로컬 marker보다 먼저 도착한 Client에서는 서버가 이미 커밋한 마지막 공격의 source VFX/SFX까지 소급 차단됐다. 타겟 수명과 공격자 source marker 수명을 하나의 상태로 묶은 것이 원인이다.

**교정 계약**

1. commit마다 revision과 남은 marker 수를 가진 bounded source-marker lease를 연다.
2. 정상 marker는 source lease와 필요한 exact target scope를 함께 소비해 Full 표현을 낸다.
3. 일반 Stop은 남은 확정 marker만 SourceOnly로 보존한다. SourceOnly는 공격자 VFX/SFX만 재생하고 target, tracer, local hit, damage를 만들지 않는다.
4. 명시적 suppression, 공격자 사망/despawn, 새 revision은 lease를 닫는다. 소비 완료 뒤 루프 marker는 거부한다.
5. 커밋 전 provisional marker는 source lease가 없으므로 계속 fail-closed한다.

**최종 증거**

교정 후 `dc055118…46c8f`에서 Editor Host/Android Client 실제 VFX 시작은 64/64였다. Client는 Stop 선행 11회를 SourceOnly로 보존했고 gate suppression·playback failure·overflow는 모두 0이었다. Host raw event 추가 1회는 production gate가 `Misaligned(98.363°)`인 커밋 전 marker라 정상 차단됐다. 사용자가 육안상 정상으로 확인해 focused Task를 PASS/CLOSED했다.

**회귀 gate**

- Legacy와 Scoped 각각 `commit→Stop→marker`, `commit→marker→Stop` 양쪽 순서를 검증한다.
- started parity는 raw event 총계만 보지 말고 커밋 여부를 분리한다. 미정렬 provisional marker의 정상 차단을 VFX 누락으로 세지 않는다.
- source-only 횟수는 전체 started의 부분집합이며 playback active까지 확인한다.
- focused PASS를 LegacyFallback 이관이나 전체 roster 완료로 일반화하지 않는다.

---

## 2026-09-27 FoxMagician VFX 시작 marker를 피해 시각으로 오인하고 생성 시 덮어쓰기를 놓침

**2026-09-28 후속 실기 정정(이력 보존):** 두 경기 be8b171a…bd94d4f5의 양 peer received/emitted 61쌍은 같은 frame immediate/text-played, 최대 7.097/2.445ms였다. 사용자는 약간 이른 현행 Fox 타이밍을 수용했고 재튜닝은 향후 projectile 구현으로 이관했다. 이는 focused 표시 지연 교정의 수용이지 VFX 종료/exact 공격 결합·완벽한 화면 일치·전체 migration 완료가 아니다. VFX 시작 79/78·실패 0·미완료 각 1은 원인 미확정이다. 03_41은 03_50과 중복 gameplay이므로 합산하지 않는다. Dust는 이번 49기 실생산과 250 결과/236 필수 표현으로 로그 증거를 확보했지만 명시적 육안 수용 전 CONDITIONAL PASS/OPEN이다.

**현상과 원인**

사용자 설계는 charge VFX의 후반부에 피해를 적용하는 것이다. 이전 교정은 `OnAttackHit @ 1.00초`를 VFX 시작과 피해의 공통 시점으로 해석해 `UnitStatsConfig.hitFrameTimes`를 2.25초에서 1.00초로 바꿨다. 최신 Host exact-flow 로그에서 VFX 시작과 `writerResult=Applied`가 거의 동시에 관측돼 이 해석이 반증됐다. 구현 조사에서는 `UnitFactory`의 서버 생산·Client 생성 양쪽이 설정의 `HitFrameTimes`를 Attack clip event 시각으로 다시 덮어쓰는 경로도 발견됐다. 따라서 설정만 되돌리면 실제 피해 시점은 바뀌지 않는다.

**교정 계약과 회귀 gate**

1. FoxMagician의 `OnAttackHit @ 1.00초`는 source VFX 시작으로 유지하고, 서버 피해는 별도 설정 `2.25초`를 사용한다. 최종 체감 시각은 실기 재검증 전까지 미확정이다.
2. 서버·Client `UnitFactory` 모두 Fox의 설정 피해 시각을 보존한다. 다른 유닛은 기존 animation event 추출을 유지한다.
3. 자동 검증은 에셋 값, clip marker, 두 생성 경로, `marker < impact < cooldown`, 진단 expected offset을 각각 확인한다. 자동 PASS를 실제 VFX·HP 타이밍 PASS로 확대하지 않는다.
4. 진단 terminal 필드를 늘릴 때는 Android 한 줄 UTF-8 사전 검사를 반드시 통과시킨다. 2026-09-27 첫 자체 검증에서 terminal 길이 초과로 FAIL해 필드 예산을 재조정했다.

**현재 상태**

코드 교정과 Unity 자동 검증 재실행 중이다. 새 Android 실기 로그 확인 전에는 Task를 Complete/PASS로 닫지 않는다.

**[🔴 2026-09-27 correction — original kept: 후속 17_21 경기]**

- **무엇을 틀렸나:** Plan §16.4의 exact rendezvous 보호 가정을 유효 PresentationResultKey 없는 Fox Legacy FIFO에도 적용했다.
- **왜 그랬나:** 서버 TimerImpact의 진단용 exact ticket과 실제 화면 표시의 exact-key 소유권을 혼동했다. 피해 2.25초 복원만으로 표시 대기까지 해결됐다고 취급했다.
- **어떻게 드러났나:** 사용자 VFX 종료 뒤 1~2초 지연 보고. Android Host/Editor Client `79c8f7eb…ca9dec`에서 VFX 시작 양쪽 17/17·실패 0, Host TimerImpact 17·maxMarkerTimerDelta 1.271813초이나 실제 VFX 종료/숫자 Emit은 미계측이다. 빈 unscoped marker는 보존되지 않고 뒤늦은 결과는 다음 marker/중단/timeout까지 대기할 수 있다.
- **교훈·교정 계약:** 실제 결과 key의 유효성과 큐 분기를 끝까지 확인한다. Fox unscoped 확정 결과만 기존 Emit으로 즉시 보내고 Supported/유효 key/다른 Legacy 경로는 유지한다. HP writer·피해 2.25초·VFX marker 1.00초 불변, marker 선행 후 결과 단일 방출과 다음 marker 재방출 없음이 회귀 경계다.
- **상태:** 문서 반영, 구현 담당자 결과 및 새 실기 대기/OPEN. 공통 Supported PASS를 Fox 표시 PASS로 읽지 않는다. 이번 생산 Fox+Boulder를 DustSpirit 실전 검증으로 오인하지 않는다.

**2026-09-28 후속 결과(이전 상태 보존):** writer-owned immediate 표시 교정 구현 완료. 구현 담당자 Runtime/Editor 정적 컴파일 오류 0, 메인 세션 직접 실행·Editor.log 확인 인계에서 Unit Action Self Validation 02:46:05 PASS, Root Pose Cross Audit SelfValidate 02:49:34 PASS, Console error 0 / warning 20. 빌드 미시도, 사용자 Fox+DustSpirit 실기 OPEN. 12 writer 조합은 HP 1회 적용·이벤트 immediate/key 보존만 확인한다. 진단은 인스턴스당 최대 128줄 unscoped receive/emit·TryShowDamage 성공, 공격자 부재 생략이다. VFX 종료와 공격별 exact correlation은 미계측이며 전체 marker→queue→text runtime PASS로 확대하지 않는다. 메뉴 수치는 메인 세션 관측 인계로 문서 담당자 직접 측정이 아니다.
