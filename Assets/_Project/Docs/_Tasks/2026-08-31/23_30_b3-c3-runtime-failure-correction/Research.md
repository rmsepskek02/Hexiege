# B3·C3 실기 실패 교정 Research

이번 작업은 한 경기에서 실제로 확인된 두 가지 문제를 함께 바로잡기 위한 조사 기록이다. 첫째, 전투가 끝난 유닛이 한 번의 경로 탐색 실패 뒤 영구 정지하는 문제다. 둘째, 공격 애니메이션의 타격 표식과 서버가 적용한 실제 타격 시각·방향이 어긋나는 문제다. 피해와 HP를 결정하는 서버 권위, 기존 단일 gameplay writer/emitter, C3 `PresentationShadow`의 read-only 경계는 유지한다. 화면을 맞추기 위해 클라이언트가 Simulation Root나 피해 결과를 다시 쓰는 방식은 사용하지 않는다.

## 1. 최신 실기 표본과 판정 경계

- 공통 세션 키: `5a30519176af944a401d7cd17fa029e6e2c27092e155b4192ea2f0677582d41c`
- 역할: Android Host / Editor Client
- 양쪽 공통 계약: `observerSchema=c3-authoritative-result-presentation-shadow-v4`, `pipelineMode=PresentationShadow`, `combatSchema=1`
- C2 서버 결과: `serverResults=2358`, `resultFailures=0`
- C2 클라이언트 결과: `clientResultAccepted=2358`, `clientResultRejected=0`
- ROOT: 양쪽 로컬 PASS, 복제 invalid·writer conflict·revision regression 0
- 최신 Editor 근거: `Assets/_Project/Docs/_Logs/_editor/2026-08-31/RuntimeLog.txt`
- Android 근거: 테스트 직후 동일 기기의 live Logcat buffer. 새 저장 파일은 작업 폴더에서 확인되지 않았으므로, 아래 Android 수치는 버퍼가 초기화되기 전 확보한 증거로 구분한다.

이 경기에서 **C2 피해·결과 수렴은 PASS**다. 그러나 B3 이동 terminal과 C3 표현 terminal은 FAIL이므로, 전체 유닛 공격·이동 교정을 PASS로 판정할 수 없다.

## 2. B3 — Unit 84 전투 후 영구 정지

Android Host에서 Unit 84 `Pistoleer`가 `post-combat-corridor` 복귀 단계에서 다음 실패를 남겼다.

- 발생 시각: `23:07:57.137`
- `decision=RejectedInvalidPath`
- `pathStatus=Unreachable`
- `authoritativeStart=(5,3)`
- `simulationRootTile=(5,3)`
- `goal=(6,-2)`
- `environmentRevision=1`
- `currentPath=20:88cba8f3c4448bf5`
- candidate/previous path: unavailable

이 실패는 한 번의 최신 경로 질의가 `Unreachable`을 반환한 뒤 같은 목표의 전방 합류점과 최종 목적지 복구를 모두 소진했는지 증명하지 못한 채 `Blocked`로 종료되는 경계다. 사용자가 본 “유닛 한 기가 멈춰 아무 행동도 하지 않는 현상”과 동일한 production 증거다.

### 관련 규칙

- `GameSystemRules_Units.md` `U-MOV-REPATH` 2~6: stale·동일 path·일시적 `Unreachable`은 즉시 목표 폐기 근거가 아니며, 같은 objective의 이력과 환경 revision을 유지해야 한다.
- `GameSystemRules_Units.md` 규칙 4: 유효한 우회 경로가 있으면 같은 command를 유지한 채 계속 이동해야 한다.
- `GameSystemRules_Units.md` 규칙 11: 전투 종료 후 도달 가능한 전방 중심 또는 최종 목적지까지 안전한 복귀 경로를 사용해야 한다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-FACING-001`: fail-closed는 unsafe candidate를 commit하지 않는 원칙이지 단일 경로 실패로 살아 있는 유닛의 목표를 폐기하는 원칙이 아니다.

## 3. C3 — 전체 수치와 1회성 lease 교정 효과

### Android Host

- `invalid=160`
- `unmatched=285` — Marker 145 / Tracer 138 / Enqueue 0 / Emit 2
- `directionMismatch=0`
- `timingMismatch=47`
- `maxTimingDelta=1.037898s`
- `coveredUnitTypes=9/25`
- `verdict=FAIL`

### Editor Client

- `invalid=160`
- `unmatched=379` — Marker 188 / Tracer 176 / Enqueue 0 / Emit 15
- `directionMismatch=59`
- `maxAimDelta=24.138°`
- `timingMismatch=79`
- `maxTimingDelta=1.938711s`
- `verdict=FAIL`

직전 실기의 scope 0 Marker/Tracer invalid 약 3,800건이 양쪽 160건으로 줄었다. 따라서 `AttackPresentationImpactLease`가 소모 뒤 반복 Marker 방출을 차단한 교정은 유효했다. 남은 160건은 같은 결함의 재발로 일반화하지 않고, 아래 두 독립 원인으로 분리한다.

## 4. StreamSpirit — 미해결 projectile profile의 zero scope 관측

남은 invalid 표본은 Unit 15·31 `StreamSpirit`에 집중됐고, `instanceId=0`, `sequenceId=0`인 `Enqueued`, `EmittedTimeout`, `EmittedTargetDeath` 단계였다. resolver는 StreamSpirit을 `ProjectileImpact`이지만 `projectile-timeline-unresolved`인 프로필로 명시한다.

미완성 프로필이라는 사실은 오류가 아니지만, 정규 scope를 만들 수 없는 유닛의 zero-key 관측을 C3 지원 표본에 넣어 지원된 공격의 계약 위반처럼 채점하는 것은 잘못이다. 반대로 이를 이유로 기존 gameplay 공격이나 VFX를 막는 것도 범위 초과다. C3 비교 대상에서 fail-closed로 격리하되, manifest와 coverage에는 `Unresolved` 지원 상태를 그대로 남겨 미완성 사실을 숨기지 않아야 한다.

### 관련 규칙

- `NET-ACTION-IDEMPOTENT`: 결과·표현은 정규 키로만 최대 한 번 적용한다.
- `NET-PRESENT-003`: 표현은 공격자 FIFO가 아니라 정확한 회차·HitIndex에 연결한다.
- 동기화 문서 10절: Shadow 경로는 read-only이며 Legacy gameplay 결과와 표현을 임의로 끄거나 대체하지 않는다.

## 5. LittleKnight — 연속 Attack anchor와 서버 Impact 예약의 불일치

`LittleKnight`의 검증된 marker/config 타격 시점은 한 cycle 안의 `0.25s`, `1.15s`다. 그러나 Attack 표현을 계속 재생하는 중 새 회차가 cycle 중간에 커밋되면, 화면은 규칙대로 클립을 restart하지 않고 **커밋 뒤 실제로 다음에 도착하는 marker**를 새 회차 `HitIndex=0`으로 소비한다.

현재 서버 Shadow 타임라인은 이 연속 재생 anchor를 반영하지 않고 새 커밋마다 첫 Impact를 고정적으로 `CommitServerTime + 0.25s`에 예약한다. 커밋이 첫 marker를 이미 지난 cycle 중간에 발생한 경우 화면의 다음 marker는 `1.15s`이므로, 서버 결과와 화면 marker가 약 한 marker 간격만큼 갈라진다. 최신 Client 표본에서 LittleKnight 첫 타격의 반복 delta가 약 `0.78~0.84s`였고, 이어지는 `hitIndex=1`은 exact 결과를 찾지 못했다.

즉 문제는 허용 오차가 작아서가 아니라, **서버 권위 타임라인이 선택한 marker occurrence와 presentation scope가 선택한 marker occurrence가 다르기 때문**이다. 타이밍 임계치를 넓히면 실제 불일치를 숨길 뿐이다.

### 관련 규칙

- `GameSystemRules_Units.md` `U-ATK-TIMELINE`: provisional→commit에서 클립을 되감지 않고 커밋 뒤 아직 지나가지 않은 marker를 승인한다.
- `NET-ACTION-SEQ`: 하나의 회차에 `AttackSequenceId + HitIndex`와 서버 Impact 시각을 원자적으로 묶는다.
- `NET-TIME-001`: 실제 Impact는 단일 서버 시간축이 권위다.
- `NET-PRESENT-001`·`NET-PRESENT-002`: 연속 Attack anchor를 유지하면서 커밋 뒤의 유효 marker만 정확한 새 scope로 소비한다.

## 6. Client-only 공격 방향 mismatch

같은 경기에서 Host의 `directionMismatch=0`인데 Editor Client만 59건, 최대 24.138°가 기록됐다. C2 타겟·결과 수렴과 ROOT 복제 계약은 정상이다. 이 조합은 서버의 타겟 선택 자체보다 클라이언트 표현 방향 또는 revision 지연을 우선 의심하게 하지만, 현 로그에는 같은 exact scope의 서버 `AuthoritativeAimDirection`, 클라이언트가 수신한 revision, 최종 presentation facing을 한 줄로 결합한 bounded 증거가 부족하다.

따라서 이 단계에서는 “클라이언트 Visual Root 보간 문제”로 확정하지 않는다. 먼저 동일 `(AttackerInstanceId, AttackSequenceId, Revision, HitIndex)`의 권위 aim과 클라이언트 최종 방향을 함께 기록해 다음 세 경우를 구분해야 한다.

1. 같은 revision을 받았지만 Visual-facing만 미수렴
2. 클라이언트가 이전 revision을 표현 중
3. observer가 서로 다른 시점의 방향을 결합한 채점 오류

교정이 필요해도 범위는 Visual-only 수렴이다. Client가 Simulation Root·NetworkTransform·서버 Aim을 다시 쓰는 방법은 금지한다.

### 관련 규칙

- `NET-ROOT-001`~`NET-ROOT-003`: Simulation Root는 서버 권위이고 Visual Root는 표현만 담당한다.
- `NET-FACING-002`: 각 Impact의 `AuthoritativeAimDirection`을 판정과 표현이 함께 사용하며 클라이언트가 재계산하지 않는다.
- `NET-AUTH-001`: 클라이언트 Animator·VFX·로컬 방향은 서버 결과의 입력이 될 수 없다.

## 7. 실기기 Error가 많아 보인 원인

live Logcat에서 Runtime `[ERROR]` 레코드는 16건이었고, raw `E Unity` 줄은 스택 확장 때문에 약 95줄로 보였다. 비-UAS 미처리 예외, `NullReferenceException`, FATAL, ANR, native crash는 발견되지 않았다. 16건은 B3 실제 adapter failure 1건, C3 terminal FAIL과 그 manifest/terminal 요약이 반복된 것이다.

따라서 오류를 정상으로 낮추지는 않되, 하나의 원인이 여러 ERROR와 스택으로 증폭되지 않도록 `최초 원인 ERROR 1건 + 최종 terminal ERROR 1건`으로 집약할 필요가 있다. 세부 샘플과 manifest는 bounded EVIDENCE/WARN으로 남겨 분석 가능성을 보존한다.

## 8. 직접 영향 범위

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` — post-combat 복귀와 실제 marker 소비 경계
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionContracts.cs` — 공격 회차·Impact·Aim 계약 DTO
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` — 연속 anchor와 정확한 scope/HitIndex 표현 정책
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs` — 서버 Commit/Impact 시간축
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs` — 권위 aim/revision 복제 payload
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs` — 지원/미지원 표본과 bounded C3 증거
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` — `Unresolved` 지원 상태 분류
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` — production-shaped RED/회귀 gate

## 9. 조사 결론

이번 실패는 하나의 임계치로 해결할 문제가 아니다. B3는 단일 `Unreachable`과 실제 목표 불가를 구분하지 못했고, C3는 연속 Attack cycle의 실제 다음 marker와 서버의 고정 Impact offset을 서로 다른 기준으로 계산했다. StreamSpirit은 미지원 표본 분류 문제이며, Client 방향은 아직 exact 증거가 부족하다. 네 문제를 분리해 각각 회귀를 먼저 RED로 고정한 뒤 교정해야 하며, C2 서버 권위 writer와 C3 read-only 경계는 그대로 보존한다.

## 10. 동일 v5 C3-only 재검증 결과 (2026-09-01)

이번 경기는 Editor Host와 Android Client가 같은 `sharedSessionKey=fd521f9476114d50473bdbbaf06dcef6d38f50efdcca1ae82a3c25533d611ff4`, `observerSchema=c3-authoritative-result-presentation-shadow-v5`, `pipelineMode=PresentationShadow`, `combatSchema=1`로 실행됐다. 혼합 버전이나 다른 경기 로그를 결합한 표본이 아니므로 C3 실기 판정에 사용할 수 있다.

- C2는 Host 서버 결과 2,828건, 결과 실패 0건이고 Client 수락 2,828건, 거부 0건이다. 서버 권위 피해와 결과 복제는 PASS다.
- B3 Host는 rejected·invalid·gate·writer·Client write·stationary Walk·adapter·drop이 모두 0이다. 최종 recoverable repath 29건은 반복 recoverable 0건으로 종료됐다. Client 복제 2,226건도 invalid·duplicate·revision regression·scope conflict가 모두 0이다. 이번 C3 교정에서 B3를 재개봉하지 않는다.
- ROOT는 Host/Client 로컬 terminal이 각각 PASS이고 evidence drop은 0이다. peer 교차감사는 별도 gate이며 이 로컬 결과만으로 교차 PASS를 선언하지 않는다.
- C3 Host는 `unmatched=385`(Marker 195 / Tracer 182 / Enqueue 0 / Emit 8), `timingMismatch=112`, `directionMismatch=0`, 최대 시간 차이 1.992135초로 FAIL이다.
- C3 Client는 `unmatched=562`(Marker 294 / Tracer 264 / Enqueue 0 / Emit 4), `timingMismatch=38`, 최대 시간 차이 1.977195초로 FAIL이다.
- Client의 방향 49건은 same-revision 0, revision-lag 42, scope-mismatch 7이다. 따라서 동일 서버 revision에서 실제 표현 방향이 잘못된 증거는 0건이며, 이 수치만으로 방향 writer를 바꾸면 안 된다.
- Android의 실제 ERROR envelope는 C3 `presentation-END` 1건이다. 비-UAS 미처리 예외, FATAL, ANR, native crash는 0이다.

Host의 `EmittedTimeout` 표본에는 0.502~0.508초 차이가 반복됐고, 별도로 Marker 계열에서 약 0.7~1.99초 차이가 관측됐다. 전자는 0.5초 복구 timeout을 정상 marker 타격과 같은 기준으로 채점한 경계 문제이고, 후자는 연속 Attack cycle의 다른 marker occurrence 또는 구회차 표현이 결과에 결합됐을 가능성을 별도로 추적해야 한다. 두 부류를 하나의 tolerance 상향으로 숨기지 않는다.

이번 유효 표본의 결론은 **C2와 B3는 보존하고 C3만 FAIL/OPEN**이다. 다음 구현은 Marker·Tracer·결과의 정확한 회차 수명, timeout 복구의 별도 의미, Client revision/scope 수렴 분류만 교정하며 `PresentationShadow`와 Legacy 단일 writer/emitter를 유지한다.
