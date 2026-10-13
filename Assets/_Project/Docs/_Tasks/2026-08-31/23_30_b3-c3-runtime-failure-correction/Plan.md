# B3·C3 실기 실패 교정 Plan

이번 교정은 멈춘 유닛을 억지로 순간이동시키거나 공격 이펙트를 눈대중으로 늦추는 작업이 아니다. 서버가 이미 결정하는 경로·타격 시각·공격 방향을 Host와 Client가 같은 회차로 정확히 소비하도록 경계를 바로잡는다. 길을 한 번 못 찾았다는 이유로 살아 있는 유닛의 목표를 버리지 않고, 연속 재생 중인 공격 애니메이션에서는 실제 다음 타격 표식과 서버 Impact 예약을 같은 기준으로 계산한다.

## 1. 보존할 아키텍처와 금지 사항

- 서버만 Simulation Root, 타겟, 공격 회차, Impact, 피해, HP와 사망을 쓴다.
- C1/C2 Legacy gameplay writer와 현재 single-writer/single-emitter를 유지한다.
- C3 `PresentationShadow`는 read-only 비교자로 유지한다.
- Attack 클립을 회차마다 restart하거나 provisional marker를 소급 승인하지 않는다.
- Client Simulation Root/NetworkTransform writer를 추가하지 않는다.
- B3 복구를 위해 위치 스냅, 비인접 타일 commit, unsafe candidate commit을 허용하지 않는다.
- 단순히 timing/direction tolerance를 넓혀 PASS로 만들지 않는다.
- StreamSpirit 미지원 사실을 숨기거나 기존 gameplay/VFX를 억제하지 않는다.

근거: `NET-AUTH-001`, `NET-ROOT-001`~`004`, `NET-TIME-001`, `NET-PRESENT-001`~`004`, 동기화 문서 10절.

## 2. B3 — bounded deterministic 복귀 복구

### 2.1 RED 회귀

production 실패와 같은 fixture를 먼저 추가한다.

1. 전투 후 현재 경로의 복귀 질의 한 번이 `Unreachable`이어도 즉시 `Blocked`가 되지 않는다.
2. 같은 route의 현재 지점보다 앞쪽에 도달 가능한 중심이 있으면 결정적 순서로 첫 합류점을 선택한다.
3. 전방 합류점이 전부 불가하면 최종 목적지까지 최신 권위 위치에서 새 경로를 평가한다.
4. 한 Unity frame에 재경로 요청은 최대 한 번이고 새 결과는 다음 frame부터 소비한다.
5. candidate corridor가 unsafe이면 Root·checkpoint·`UnitData.Position`을 전혀 commit하지 않는다.
6. 실제 진전 또는 walkability revision 변경만 복구 이력을 초기화한다.
7. 모든 전방 합류점과 최종 목적지가 같은 environment revision에서 반복 확인돼 실제 불가일 때만 `Blocked`가 된다.

### 2.2 구현

- `UnitView`의 post-combat 복귀를 단일 `RequestLatestAuthoritativeRepath(finalTarget)` 성공 여부가 아니라 objective-scoped 복구 상태로 관리한다.
- 기존 route에서 현재 권위 위치보다 **앞쪽**인 중심 후보만 route 순서대로 검사한다. 뒤쪽·현재 차단 타일·비인접 논리 점프는 후보가 아니다.
- 각 후보는 동일한 pathfinder와 corridor preflight를 통과해야 한다. 첫 안전 후보 하나만 staged하고 다음 frame에 commit한다.
- 전방 후보가 없으면 최신 `Simulation Root / UnitData.Position / environmentRevision`에서 최종 목적지까지 한 번 질의한다.
- 단일 `Unreachable`은 `WaitingRepath`와 bounded history로 보존한다. 같은 objective·revision·무진전 상태에서 모든 후보와 최종 목적지 실패가 반복 확인된 경우에만 `Blocked`를 승인한다.
- 대기·차단 중 Walk 표현은 진행하지 않는다. 목표 변경·진전·환경 revision 증가만 재개 근거로 사용한다.

근거: `GameSystemRules_Units.md` `U-MOV-REPATH` 1~9, `U-MOV-ALIGN` 8·11, 규칙 4·11, `NET-FACING-001`.

## 3. C3 타이밍 — 연속 Attack anchor와 서버 Impact 원자화

### 3.1 RED 회귀

LittleKnight의 marker `0.25s`, `1.15s`와 연속 Attack cycle을 사용해 다음을 먼저 실패로 고정한다.

1. 첫 marker가 지난 cycle 중간에 새 회차를 커밋하면 `HitIndex=0`은 실제 다음 marker인 `1.15s` occurrence와 결합한다.
2. 같은 회차 서버 Impact도 그 occurrence의 절대 서버 시각에 예약된다. 고정 `commit+0.25s`와 갈라지면 RED다.
3. 다음 `HitIndex=1`은 다음 cycle의 `0.25s` occurrence와 정확히 결합한다.
4. `(AttackerInstanceId, AttackSequenceId, Revision, HitIndex)`별 Impact와 marker 소비는 각각 최대 한 번이다.
5. commit 직전·marker 정확 경계·cycle wrap·overshoot 0과 양수·동일 offset 다중 hit를 모두 포함한다.
6. StopCombat, 타겟 사망, 공격자 사망과 새 sequence가 이전 anchor/scope를 재사용하지 않는다.

### 3.2 구현

- 서버가 검증된 AttackTimeline과 서버 시간으로 연속 Attack cycle anchor를 유지한다. Client Animator normalized time을 권위 입력으로 사용하지 않는다.
- 새 회차 커밋 시 현재 anchor에서 커밋 뒤의 **실제 다음 marker occurrence**들을 계산한다. 첫 occurrence부터 `HitIndex=0..N-1`에 배정한다.
- 계산된 절대 Impact 시각/유효 offset과 새 presentation scope를 같은 commit 결과에서 원자적으로 발행한다. scope만 다음 marker를 고르고 서버 예약은 원래 offset을 쓰는 분리를 제거한다.
- Legacy overshoot는 기존 계약대로 관측 `CommitServerTime`을 보존하고 유효 시간에 정확히 한 번만 반영한다. anchor 보정 뒤 원래 offset을 다시 더하지 않는다.
- Host와 Client Marker/Tracer/Enqueue/Emit는 exact instance+sequence+revision+hitIndex만 소비한다. FIFO·2초 근접 결합·다음 회차 재사용은 금지한다.
- Animation은 같은 Attack 상태와 진행 위치를 유지하며 CrossFade/restart하지 않는다.

근거: `U-ATK-TIMELINE`, `NET-ACTION-SEQ`, `NET-ACTION-IDEMPOTENT`, `NET-TIME-001`~`004`, `NET-PRESENT-001`~`003`.

## 4. C3 방향 — 증거 보강 후 Visual-only 수렴

### 4.1 RED 분류 회귀

동일 exact scope에 다음 값을 함께 싣고 분류하는 순수 fixture를 추가한다.

- 서버 Impact의 `AuthoritativeAimDirection`
- 서버 scope/revision과 Impact server time
- 클라이언트가 수신·적용한 scope/revision
- NetworkTransform이 복제한 최종 Simulation Root facing
- Marker/Tracer 시점의 Visual-facing

같은 revision Visual-only mismatch, revision-lag, 다른 scope 결합을 서로 다른 사유로 분류해야 한다. 증거가 누락되거나 non-finite이면 방향 원인을 추정하지 않고 fail-closed 한다.

### 4.2 구현 단계

1. `NetworkUnitActionShadowState`/observer seam에 위 bounded evidence를 추가한다.
2. 동일 경기에서 Client-only 59건이 어느 분류인지 먼저 확인한다.
3. 같은 revision의 Visual-facing만 미수렴한 경우에 한해 Visual Root가 권위 aim을 향해 허용된 표현 시간 안에 수렴하도록 교정한다.
4. revision-lag이면 오래된 표현 scope를 폐기하고 최신 단조 증가 scope만 적용한다.
5. observer 결합 오류이면 runtime 방향 writer를 바꾸지 않고 exact-key 비교기만 고친다.

어느 경우에도 Client Simulation Root, NetworkTransform, 서버 `AuthoritativeAimDirection`을 다시 쓰지 않는다.

근거: `NET-AUTH-001`, `NET-ROOT-001`~`003`, `NET-FACING-002`, `NET-ACTION-IDEMPOTENT`.

## 5. StreamSpirit `Unresolved` 표본 격리

- resolver가 `projectile-timeline-unresolved`를 반환한 공격은 정규 scope를 만들 수 없으므로 C3 지원 표본의 Marker/Tracer/Enqueue/Emit exact 채점에 넣지 않는다.
- zero instance/sequence를 유효 scope처럼 큐에 등록하거나 다른 회차에 결합하지 않는다.
- manifest에는 UnitType, delivery, `support=Unresolved`, reason을 bounded coverage로 남긴다.
- terminal은 “지원된 프로필 invalid”와 “미지원 프로필 coverage”를 별도 집계한다.
- Legacy gameplay 공격, 피해, 기존 VFX/SFX/Tracer를 이 분류 때문에 억제하지 않는다.
- 프로필을 임의의 Hitscan/MeleeContact나 가짜 timeline으로 대체하지 않는다.

근거: `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-003`, 동기화 문서 10절의 Shadow read-only 전환 규칙.

## 6. 오류 로그 집약

- 서로 다른 원인의 최초 production failure만 ERROR 1건으로 기록한다.
- bounded 세부 표본과 coverage manifest는 EVIDENCE/WARN으로 남긴다.
- 경기 종료 시 축별 terminal verdict를 ERROR 최대 1건으로 기록한다.
- C2 PASS를 C3 FAIL 때문에 ERROR로 바꾸지 않고 result/presentation/movement 축을 분리한다.
- ERROR 집약은 실패를 숨기거나 terminal FAIL을 PASS로 낮추지 않는다.
- 같은 원인에서 Unity stack이 반복 확장되지 않도록 계약 실패는 필요한 payload를 한 줄에 담는다.

근거: `Assets/_Project/Docs/LogRules.md`의 심각도·존속 분리 원칙, `NET-PRESENT-004`의 bounded 복구 진단 계약.

## 7. 예상 변경 파일

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionContracts.cs`
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

구현 조사에서 실제 책임이 다른 기존 파일에 있으면 이 목록과 Plan을 먼저 갱신한다. 새 gameplay writer, Client Root writer 또는 임시 공격 에셋은 추가하지 않는다.

## 8. 자동 검증 순서

1. 각 실패를 production-shaped 순수 seam에서 RED로 확인한다.
2. B3 복귀 recovery를 GREEN으로 만든다.
3. 연속 Attack anchor·Impact scope 원자화를 GREEN으로 만든다.
4. StreamSpirit 지원 표본 격리와 방향 증거 분류를 GREEN으로 만든다.
5. 전체 `RunUnitActionSelfValidation`을 실행한다.
6. 컴파일 오류 0과 문서 정합성 검사 7종 0건을 확인한다.
7. 위 자동 gate가 PASS일 때만 Android Build And Run을 진행한다.

## 9. 실기 PASS gate

새 빌드의 동일 경기 Android/Editor 표본에서 아래를 모두 만족해야 한다.

### 입장·권위

- 양쪽 `sharedSessionKey`, observer schema, pipeline mode, combat schema가 완전히 같다.
- C2 `serverResults == clientResultAccepted`, result failure/reject/correlation failure 0.
- gameplay writer/emitter conflict 0, Client Root/reducer write attempt 0.

### B3

- `movement-adapter-failure=0`
- 단일 stale/`Unreachable` 뒤 정상 전방 합류 또는 최종 목적지 복구가 1회 이상 관측된다.
- 유효한 우회로가 있는 유닛의 영구 `Blocked=0`
- no-progress 무한 재시도, same-frame retry, unsafe spatial commit 0.
- 정지 상태 Walk 진행 0.

### C3 타이밍·상관

- 지원된 유닛의 invalid scope/key 0.
- LittleKnight 연속 회차의 `HitIndex=0/1` 모두 exact result와 한 번씩 결합한다.
- 허용 범위를 넘는 timing mismatch 0; 임계치 상향으로 통과시키지 않는다.
- Marker/Tracer/Enqueue/Emit 단계별 중복 소비와 다음 회차 재사용 0.
- StreamSpirit `Unresolved`는 지원 표본 invalid 0이며 manifest coverage로만 남는다.

### C3 방향·화면

- 같은 revision exact 표본의 direction mismatch 0.
- revision-lag와 observer 결합 오류가 0이거나 명시된 bounded 복구로 종료된다.
- Client가 Simulation Root를 쓰지 않는다.
- 사용자 육안으로 유닛 영구 정지, 공격 애니메이션과 실제 타격 차이, 공격 방향 불일치가 재현되지 않는다.

### 오류 로그

- 비-UAS 미처리 예외, FATAL, ANR, native crash 0.
- 축별 실패가 없으면 UAS ERROR 0.
- 실패를 의도적으로 주입한 회귀에서는 원인 ERROR 1건 + terminal ERROR 1건을 넘지 않는다.

## 10. 완료 판정

Unity self-validation PASS는 구현 계약의 자동 gate일 뿐 멀티플레이 완료 판정이 아니다. 실제 Android Host / Editor Client 동일 경기에서 B3·C2·C3와 사용자 육안 확인이 모두 통과해야 이번 교정을 PASS로 닫는다. 한 축이라도 실패하면 해당 축만 증거에 따라 다시 교정하고, 이미 PASS한 서버 권위 결과나 이동 Root 계약을 함께 재설계하지 않는다.

## 11. v5 잔여 C3-only 교정 계획 (2026-09-01)

동일 v5 경기에서 C2와 B3는 통과했지만 C3는 Host/Client 모두 실패했다. 따라서 이번 Round는 C3 표현 상관·시간·복제 증거만 교정한다. 서버 피해·HP·타겟 선택, C2 결과 계약, B3 이동 reducer/Root writer, Legacy 단일 emitter는 변경하지 않으며 `PresentationShadow`를 유지한다.

### 11.1 RED 회귀와 회차 재구성

1. `(AttackerInstanceId, AttackSequenceId, Revision, HitIndex)`별로 서버 결과, Marker, Tracer, Enqueue, Emit을 재구성한다.
2. 타겟 사망·사거리 이탈·StopCombat·새 타겟 전환으로 닫힌 구회차의 Marker/Tracer가 이후 결과나 새 회차에 결합되지 않는 fixture를 먼저 RED로 만든다.
3. 정상 marker, marker 누락 복구 timeout, target-death/stop 복구 표현을 서로 다른 관측 종류와 기대 시간으로 분리한다.
4. 약 0.5초 `EmittedTimeout`과 약 0.7~1.99초 Marker 차이를 서로 다른 회귀로 고정한다. 허용 오차를 넓혀 둘을 함께 PASS시키지 않는다.
5. 같은 revision 방향 오류, revision-lag, scope-mismatch를 독립 fixture로 검증한다.

### 11.2 구현 경계

- 정상 Marker/Tracer는 같은 exact scope와 HitIndex의 서버 결과에 단계별 최대 한 번만 결합한다.
- 취소·종료된 scope 또는 authoritative result가 없는 표현은 다음 회차에 재사용하지 않고 명시적으로 폐기한다.
- timeout fallback은 marker 누락을 복구하는 제한된 표현이며 정상 marker 타이밍 일치로 기록하지 않는다. 늦게 도착한 marker도 다음 결과에 재결합하지 않는다.
- Client 방향은 동일 sequence/revision 표본만 실제 gameplay 방향 mismatch로 판정한다. revision-lag와 scope-mismatch는 별도 복제 수렴 실패로 남기고 서버 `AuthoritativeAimDirection`, Simulation Root 또는 NetworkTransform을 Client가 다시 쓰지 않는다.
- C3는 read-only observer/presenter로 남고 피해·HP·타겟·이동 결과를 발생시키지 않는다.

### 11.3 완료 gate

1. 신규 production-shaped 회귀를 포함한 전체 `[UAS-DIAG]`, C2, C3 self-validation이 PASS한다.
2. C2와 B3 기존 회귀가 그대로 PASS하고 관련 production 코드는 변경되지 않는다.
3. 문서 정합성 검사 7종이 0건이다.
4. 위 자동 gate가 PASS일 때만 Android Build And Run을 실행한다.
5. 새 동일 버전 경기에서 supported profile의 unmatched·timing mismatch·same-revision direction mismatch가 0이어야 한다.
6. revision-lag와 scope-mismatch는 정상으로 숨기지 않고 0으로 수렴하거나 bounded 복구/폐기 증거로 닫혀야 한다.
7. C3 실기 PASS 전까지 실제 emitter 전환과 Legacy 제거는 금지한다.

## 12. C3 v6 구현 결과와 실기 대기 상태 (2026-09-02)

### 12.1 구현 완료 범위

- 로컬 타격 신호를 attacker-only 값이 아니라 `(AttackerInstanceId, AttackSequenceId, Revision, HitIndex)`의 정확한 표현 scope로 전달한다.
- Marker/Tracer와 authoritative result는 도착 순서와 관계없이 exact scope rendezvous에서 최대 한 번만 결합한다.
- 타겟 변경·사망·StopCombat·Held·새 scope 전환 시 이전 sequence를 retire/tombstone 처리해 늦은 결과나 timeout이 구회차 표현을 다시 방출하지 못하게 한다.
- 정상 marker 타이밍과 timeout/target-death/attacker-stop 복구 타이밍을 별도 terminal로 분리한다. timeout 발생 자체는 C3 실패이며 허용치를 넓히지 않는다.
- 방향 판정은 marker 시점의 불변 복제 instance/sequence/revision을 사용한다. same-revision 실제 방향 오류와 revision-lag/scope-mismatch를 분리한다.
- rendezvous의 invalid/capacity 초과는 시각 fallback과 별개로 구조 실패 증거를 남긴다.
- C2 서버 권위 writer, B3 이동 writer, Legacy 단일 emitter와 `PresentationShadow` read-only 경계는 변경하지 않았다.

### 12.2 자동 gate 결과

- Unity compile: PASS(컴파일 오류 0)
- `[UAS-DIAG]`: PASS
- `[UAS-DIAG][C2-AUTHORITY]`: PASS
- `[UAS-DIAG][C3-PRESENTATION-SHADOW]`: PASS — observer schema v6, marker-time immutable replication evidence, 정상/복구 timing 분리, timeout-zero gate, 구회차 retire, 구조 실패 증거 포함
- Android Build And Run: **미완료** — 코드 실패가 아니라 Unity Android keystore 서명 비밀번호가 입력되지 않아 signing 단계에서 중단됐다. 비밀번호는 자동 입력·기록하지 않는다.

### 12.3 남은 완료 gate

사용자가 Unity에서 서명 비밀번호를 직접 입력한 뒤 Build And Run을 다시 실행한다. 새 Android Host / Editor Client 동일 v6 경기에서 C2/B3 보존, supported C3 exact-scope unmatched 0, 정상 timing mismatch 0, timeout recovery 0, same-revision direction mismatch 0, revision-lag/scope-mismatch의 bounded 종료를 확인하기 전에는 C3를 최종 PASS로 닫지 않는다.
