# C3 권위 결과 기반 Presentation Shadow Plan

이번 계획은 서버가 확정한 공격 대상·방향·타격 시각을 화면 표현의 기준으로 사용할 준비를 하는 작업이다. 먼저 새 Presenter는 아무 효과도 내지 않고, “서버 결과대로 표현했다면 언제·어디를·어느 방향으로 보여야 했는가”를 계산한다. 그 값을 현재 Attack marker와 FIFO 표현이 실제로 보여 준 결과와 비교한다. 서버 피해·HP·기존 VFX·Animator는 그대로 유지한다.

> **기존 로직 보존·제거 정책:** 이번 C3에서는 `HitPresentationQueue`, `OnAttackHit`, 기존 HP 텍스트·VFX·SFX·트레이서·타격 반응을 제거하거나 비활성화하지 않는다. 신규 Presenter는 로그만 남기며 gameplay write와 presentation emit이 0이어야 한다. 기존 emitter의 비활성화는 C3 역할교대 검증과 별도 사용자 승인 뒤 다음 단계에서만 다룬다.

## 1. 적용 규칙

- `GameSystemRules_UnitCombatSynchronization.md`의 `NET-AUTH-001`, `NET-AUTH-002`
- 같은 문서의 `NET-ACTION-SEQ`, `NET-ACTION-IDEMPOTENT`
- 같은 문서의 `NET-TIME-001`~`NET-TIME-005`
- 같은 문서의 `NET-FACING-002`
- 같은 문서의 `NET-DELIVERY-HITSCAN`, `NET-DELIVERY-PROJECTILE`, `NET-DELIVERY-TRAVELING`, `NET-DELIVERY-TIMED`
- 같은 문서의 `NET-PRESENT-001`~`NET-PRESENT-004`
- 같은 문서 10장의 전환 순서 4~6과 `single-writer / single-emitter`
- `GameSystemRules_Units.md`의 `U-ATK-TIMELINE`, `U-IMPACT-TARGETLOCKED`, 전투 연출 동기화 규칙 17~21, 애니메이션 상태 동기화 규칙 22

## 2. 구현 범위

### 2.1 전투 경기 고정 계약

수정 파일:

- `Assets/_Project/Scripts/Application/NetworkContext.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameFlow.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`

작업:

1. `CombatPipelineMode`와 mode·schema·profile hash를 원자적으로 고정하는 순수 latch를 추가한다.
2. 안전 기본값은 Legacy이며 C3 검증 경기는 PresentationShadow로 시작한다. ResultPresentation은 후속 전환용 값만 정의하고 이번 단계에서 emit하지 않는다.
3. 25종 manifest의 타입·target mode·delivery·execution kind·support·impact count·secondary flag·reason과 C3 contract revision을 일정한 순서로 직렬화해 deterministic profile hash를 만든다.
4. `RequestReadyServerRpc`에서 양쪽 combat schema, supported mode mask, profile hash를 확인한다.
5. 불일치는 게임 시작을 거부하고 이유를 남긴다. 자동으로 일부 유닛만 Legacy로 낮추거나 경기 중 mode를 바꾸지 않는다.
6. `StartGameClientRpc`에서 서버 선택 계약을 양쪽 `NetworkContext`에 한 번만 latch한다. 같은 값 재수신은 멱등, 다른 값은 fail-closed다.
7. 기존 B3 이동 handshake와 종족 전달 의미는 보존한다.

근거: 전환 규칙 5·8, `NET-AUTH-001`.

### 2.2 순수 Presentation Shadow 계약과 scheduler

신규 파일:

- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs`

작업:

1. 전체 `AttackResultKey`, 공격자 로컬 ID, action revision, delivery, Impact 시각, 권위 aim, 선택적 impact point, outcome과 결과 HP를 담는 표현 입력 값을 정의한다.
2. `PresentationServerTime = SynchronizedServerTime - CombatPresentationDelay`를 계산하는 순수 clock 정책을 추가한다. 기본 delay는 규칙값 0.10초다.
3. 결과를 exact key로 Scheduled / CatchUp / Duplicate / Conflict / Invalid / Expired / InstanceRetired로 분류한다.
4. 결과가 먼저 오거나 Legacy 관측이 먼저 오는 두 순서를 모두 bounded 보관한다.
5. 회차당 최대 64개, 최대 2초 reorder buffer와 0.50초 catch-up 경계를 적용한다.
6. 비유한 시간, server time 역행, 키/payload 충돌, retire 뒤 입력은 fail-closed 한다.
7. 결과가 만료돼도 이를 다음 Animation Event나 다음 공격 결과에 재사용하지 않는다.

근거: `NET-ACTION-IDEMPOTENT`, `NET-TIME-001`~`NET-TIME-005`, `NET-PRESENT-004`.

### 2.3 Host·Client 공통 결과 bridge와 동기화 clock

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs`

작업:

1. `NetworkAttackImpactShadowResult`를 Application 표현 입력으로 손실 없이 변환한다.
2. 순수 Client는 기존 exact-key classifier가 Accepted한 결과만 Presenter bridge로 전달한다.
3. Host는 서버 publication이 Accepted된 자리에서 동일 입력을 로컬 Presenter bridge로 한 번 전달한다. Host의 ClientRpc 수신은 계속 건너뛰어 이중 소비를 막는다.
4. 입력에는 수신 당시 synchronized server time과 로컬 monotonic time을 함께 제공해 Presentation 레이어가 Netcode를 직접 참조하지 않게 한다.
5. NetworkObject despawn·공격자 instance retire를 Presenter lifecycle에 전달한다.
6. bridge 실패는 Shadow만 실패시키고 서버 결과·HP·기존 RPC를 재실행하거나 롤백하지 않는다.

근거: `NET-AUTH-002`, `NET-TIME-001`, 전환 규칙 4.

### 2.4 Legacy marker·FIFO read-only 관측 seam

수정 파일:

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs`

작업:

1. `UnitView.OnAttackHit`의 기존 suppression 확인 뒤, 실제로 발화한 marker의 공격자 ID, 표시 타겟 종류·ID, view-space forward, marker local time을 read-only 관측 값으로 남긴다.
2. 원거리 트레이서는 발사 marker와 착탄 callback 시각을 구분해 기록한다. 기존 target position 계산과 tracer 실행은 변경하지 않는다.
3. `HitPresentationQueue`는 `OnEntityDamaged` enqueue와 모든 실제 `Emit` 경로의 공격자·피해자·결과 HP·Immediate/marker/timeout/death/stop 원인·local time을 관측한다.
4. 단일 marker AoE 전량 방출과 다중 marker 1건 FIFO의 기존 동작을 보존한다.
5. 관측 코드는 `Emit`, Animator, VFX, SFX, HP를 호출할 수 없다.

근거: `NET-PRESENT-001`~`NET-PRESENT-004`, 전환 규칙 4·6.

### 2.5 read-only Presenter 조합과 비교

신규 파일:

- `Assets/_Project/Scripts/Presentation/Effects/UnitAttackResultPresentationShadow.cs`

수정 파일:

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs`

작업:

1. 조합 루트가 Presenter Shadow를 한 번 생성·초기화한다. 씬·프리팹 Inspector 배선은 추가하지 않는다.
2. combat mode가 PresentationShadow인 경기에서만 관측 입력을 받는다. Legacy와 ResultPresentation에서는 gameplay에 개입하지 않고 상태를 비운다.
3. 서버 result의 피해자 ID를 기준으로 Legacy enqueue/emit exact candidate를 찾는다. 공격자·피해자·결과 HP·시간창에서 후보가 정확히 하나가 아니면 ambiguous로 분류한다.
4. 권위 aim을 현재 Client의 view space로 변환한 값과 marker의 실제 표시 forward를 비교한다. 서버 aim을 로컬 타겟 현재 위치로 다시 계산하지 않는다.
5. Shadow 예정 Impact와 Legacy 실제 emit의 시간 차를 기록한다. 지나간 결과는 catch-up으로 분류하고 과거 시각으로 되감지 않는다.
6. Target exact match, aim yaw delta, scheduled/actual delta, duplicate, unmatched, ambiguous, catch-up을 observer에 전달한다.
7. Presenter 클래스에는 실제 효과를 내는 의존성(`EffectManager`, `FloatingHpTextSpawner`, Animator writer)을 주입하지 않는다.

근거: `NET-FACING-002`, `NET-TIME-002`~`NET-TIME-004`, `NET-PRESENT-003`.

### 2.6 C3 bounded observer와 terminal

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`

작업:

1. schema를 C3 의미가 드러나는 새 revision으로 올리고 C1/C2 카운터를 보존한다.
2. result received, scheduled, matched, unmatched, ambiguous, duplicate, conflict, direction mismatch, target mismatch, timing mismatch, catch-up, expired, lifecycle retire를 별도 집계한다.
3. 정상 결과는 terminal 카운터와 유닛 타입별 제한 표본만 남긴다. 실패만 bounded detail을 남긴다.
4. terminal에는 `gameplayWrites=0`, `presentationEmits=0`, combat mode/schema/profile hash를 포함한다.
5. 전체 줄 UTF-8 preflight와 terminal 예약 줄을 유지한다. 절단·drop·manifest 오류는 FAIL이다.
6. 현재 C2가 결과를 만들지 않는 unresolved secondary/periodic/projectile 범위를 coverage로 가장하지 않는다.

근거: 전환 규칙 4, `NET-PRESENT-003`, `NET-PRESENT-004`.

## 3. RED→GREEN 자동 회귀

수정 파일:

- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

먼저 다음 fixture가 현재 코드에서 RED인지 확인하고, 구현 뒤 기존 A1~B3·C1·C2와 함께 GREEN으로 만든다.

1. combat latch는 동일 mode/schema/hash 재수신만 허용하고 경기 중 mode·schema·hash 변경을 거부한다.
2. profile hash는 manifest 순서·내용이 같으면 동일하고, 한 필드가 달라지면 달라진다. unresolved 상태도 hash에 포함된다.
3. 서버 결과 exact key를 처음 받으면 Scheduled, 같은 payload는 Duplicate, 같은 키의 다른 payload는 Conflict다.
4. 결과→Legacy, Legacy→결과 순서가 모두 같은 exact match로 수렴한다.
5. 후보가 둘 이상이면 nearest를 추측하지 않고 Ambiguous, 없으면 Unmatched다.
6. 64개 reorder 용량과 2.000/2.001초 만료 경계를 검증한다.
7. 0.50초 이내 늦은 결과는 한 번 CatchUp, 경계를 넘은 결과는 Expired이며 다음 marker로 넘어가지 않는다.
8. Blue/Red view 변환 뒤 aim delta 계산이 동일한 서버 방향에 수렴한다. 현재 타겟 위치를 다시 읽은 방향으로 대체하지 않는다.
9. 다중 hit와 동일 HitIndex AoE 결과는 VictimKind·VictimId·EffectKind·ResultOrdinal로 각각 한 번 매칭된다.
10. marker 전 결과, 결과 전 marker, empty marker, timeout/death/stop flush를 구분한다.
11. retire 뒤 늦은 결과를 거부하고 재사용된 unit ID의 새 attacker instance와 섞지 않는다.
12. Host 로컬 bridge와 Client bridge가 각각 결과를 한 번만 소비한다.
13. Presenter Shadow의 gameplay write와 presentation emit 계수는 항상 0이다.
14. C3 terminal 최악값 전체 줄이 UTF-8 안전 상한 안에 있고 drop·preflight 실패를 PASS로 만들지 않는다.

## 4. 실패 시 안전 동작

- handshake 불일치: 게임 시작을 거부하거나 서버가 미리 선택한 전체 경기 Legacy만 허용한다. 경기 중 자동 전환은 없다.
- 잘못된 result/payload/time: Shadow 비교에서만 거부하고 HP·피해를 변경하지 않는다.
- Legacy candidate 없음/복수: unmatched/ambiguous로 기록하고 임의 결합하지 않는다.
- 방향 변환 실패: mismatch를 성공으로 숨기지 않고 비교 불가 failure로 남긴다.
- 늦은 result: 정규 키로 한 번 catch-up 분류하며 다음 공격 FIFO에 넣지 않는다.
- observer/log 실패: gameplay와 기존 emitter는 계속 동작하고 C3 terminal은 FAIL한다.
- Presenter 초기화 실패: 기존 Legacy 표현은 유지하고 C3만 FAIL한다.

## 5. 변경 금지 범위

- 서버 타겟 선택, 피해량, 방어력, HP·사망 writer
- C1/C2 회차·authorization·result 의미
- B3 Simulation Root와 NetworkTransform writer
- 기존 `HitPresentationQueue` emit 정책과 `OnAttackHit` VFX·SFX·트레이서 실행
- 신규 애니메이션·VFX·SFX 에셋 제작
- unresolved Projectile/Traveling/Periodic 결과를 임시 Hitscan 또는 주 결과로 축약
- 경기 중 또는 유닛별 Legacy/신규 혼합
- 기존 emitter 제거·비활성화

## 6. 구현 직후 자동 완료 gate

1. Unity Runtime/Editor 컴파일 오류 0건.
2. `Run Unit Action Self Validation`의 기존 전체 계약과 신규 C3 회귀 PASS.
3. combat handshake가 mode/schema/profile hash 불일치를 fail-closed 함.
4. Host/Client Presenter 입력 중복 0건.
5. Shadow gameplay write 0, presentation emit 0.
6. Android-safe terminal preflight PASS, drop 0.
7. 문서 정합성 검사 0건.

자동 gate PASS는 C3 구현 완료이지 실제 표현 전환 완료가 아니다.

## 7. 후속 멀티플레이 gate

새 Android Development Build와 같은 코드의 Editor로 역할을 교대한다.

1. Android Host / Editor Client 집중 표본: 단일 근접, 다중 근접, Hitscan, 타겟 사망·교체, 유닛/건물 공격.
2. Editor Host / Android Client 반대 역할로 같은 항목 반복.
3. 동일 `sharedSessionKey`, combat mode/schema/profile hash, C1/C2/C3 terminal을 결합한다.
4. 정규 결과의 target mismatch, direction mismatch, duplicate/conflict, unmatched/ambiguous, Client reject가 0이어야 한다.
5. timing은 예정 Impact 대비 실제 Legacy emit delta 분포와 catch-up을 별도로 확인한다. 네트워크 지연으로 허용된 catch-up과 잘못된 다음-cycle FIFO 결합을 섞지 않는다.
6. C2 최신 집중 범위에 포함된 8종부터 비교한 뒤 남은 17종을 누적한다.
7. Projectile·Traveling·Periodic·특수 secondary는 정규 결과가 실제 생성된 항목만 PASS로 센다. 미생성 항목은 미검증으로 남긴다.
8. 다음 경기 Legacy rollback에서 combat latch가 경기 시작 전 Legacy로만 전환되고 경기 중 값이 바뀌지 않는지 확인한다.

## 8. 다음 전환 조건

C3 역할교대와 필요한 결과 coverage가 PASS한 뒤에만 별도 계획으로 ResultPresentation 경기 전환을 제안한다. 그 단계에서는 기존 FIFO와 신규 result presenter 중 **정확히 하나만** VFX·HP 텍스트·피격 반응을 emit해야 한다. 사용자 실기 승인 전 기존 경로를 삭제하지 않는다.

Testcase는 사용자가 요청하지 않았으므로 작성하지 않는다.

## 9. 구현 상태 (2026-08-30)

- [x] 전투 mode/schema/profile hash 경기 고정 계약 및 handshake
- [x] exact-key Presentation Shadow DTO·scheduler·양방향 reorder 상관
- [x] 0.10초 delay, 0.50초 catch-up, 2.000/2.001초 경계, 64개 상한
- [x] Host/Client 단일 소비 bridge와 instance retire
- [x] UnitView/HitPresentationQueue read-only 관측 및 Red 방향 환원
- [x] gameplay write 0 / presentation emit 0 구조 경계
- [x] C2 `result-END` / C3 `presentation-END` 독립 verdict 분리 및 최악값 UTF-8 preflight — v3에서 C2/C3/overall 판정을 각각 독립 계산
- [x] Unity 컴파일 오류 0건
- [x] 전체 UAS self-validation 및 C3 전용 self-validation PASS
- [x] Android Build And Run 성공, AAB 생성 및 연결 실기기 앱 실행 확인
- [ ] Android Host / Editor Client 실기 검증
- [ ] Editor Host / Android Client 역할 교대 검증
- [ ] 지원 결과 coverage 누적 및 후속 emitter 전환 승인

자동 gate는 완료했다. 남은 항목은 새 Build And Run 이후 같은 경기의 Host/Client 로그로 판정한다.

## 10. 실기 FAIL 교정 계획 (2026-08-31)

> **계획 변경 이유:** 최초 구현의 시간창 후보 추정은 `NET-ACTION-SEQ`, `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-003`의 정규 키 상관계약을 만족하지 못했다. `Ambiguous`를 정상적인 fail-closed 결과로 남기는 것만으로는 실제 공격 방향·시각을 채점할 수 없으므로, 기존 Legacy emitter는 보존한 채 C3 관측 식별자와 소비 모델을 교정한다.

1. 서버 권위 Impact authorization이 이미 보유한 전체 `AttackResultKey`를 주 결과의 `EntityDamagedEvent`와 HP 동기화 RPC에 read-only 표현 식별자로 전달한다. 피해량·HP·사망·VFX 실행 순서는 바꾸지 않는다. 근거: `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-003`.
2. `UnitView`의 실제 marker는 같은 GameObject의 원자 `NetworkUnitActionShadowState`에서 `AttackerInstanceId + AttackSequenceId`를 읽고, 같은 회차 안 marker 순서로 `HitIndex`를 만든다. 원거리 Tracer 착탄은 발사 때 캡처한 scope를 사용하며 현재 타겟/현재 회차를 다시 읽지 않는다. 근거: `NET-ACTION-SEQ`, `NET-DELIVERY-HITSCAN`, `NET-FACING-002`.
3. scheduler는 2초 시간창으로 후보를 추측하지 않는다. Marker/Tracer는 exact attack scope로, Enqueue/Emit은 전체 결과 키로만 결합한다. 한 결과의 Enqueue와 Emit, 한 scope의 Marker와 Tracer는 단계별 최대 1회만 소비하며 중복은 `Duplicate`로 분류한다. 근거: `NET-ACTION-IDEMPOTENT`, `NET-TIME-004`.
4. 결과 자체의 Scheduled/CatchUp과 Legacy 관측 대기를 별도 계수하고, 방향·시간 mismatch는 exact 결합 뒤에만 계산한다. 실기에서 발생 가능한 Shadow mismatch 상세는 bounded 진단으로 남기되 사용자 오류 UI를 여는 `Error` 심각도로 매 건 출력하지 않는다. terminal FAIL은 유지한다. 근거: `NET-PRESENT-004`, `LogRules.md` 고빈도·심각도 규칙.
5. observer 시작 로그는 combat contract latch 뒤의 mode/schema/hash를 기록하도록 경계를 교정한다. latch 전 기본값을 경기 계약처럼 기록하지 않는다. 근거: 전환 규칙 5.
6. 삭제된 `BuildingView`를 참조하는 8개 건물 프리팹에서 dangling MonoBehaviour를 제거한다. 현재 `BuildingFactory`가 사망·철거와 GameObject 생명주기를 단독 소유하므로 새 스크립트를 만들거나 책임을 이중화하지 않는다.
7. self-validation에 같은 공격자·같은 대상의 2초 내 연속 결과, 동일 단계 중복, 다중 hit, Legacy-first/result-first, exact key 불일치, scope 없는 marker fail-closed, 진단 Error 비오염 회귀를 추가한다. 전체 A1~C3 PASS 뒤에만 Build And Run을 실행한다.

## 11. 실기 FAIL 교정 구현 결과 (2026-08-31)

- [x] 서버 authorization의 전체 `AttackResultKey`를 주 피해 이벤트와 Client HP 동기화에 read-only 전달
- [x] Marker/Tracer는 `AttackerInstanceId + AttackSequenceId + HitIndex`, Enqueue/Emit은 전체 결과 키로만 상관
- [x] 공격별 단계 소비 비트로 Marker/Tracer/Enqueue/Emit 각각 최대 1회 보장
- [x] 결과 선도착·관측 선도착과 한 결과 도착 시 여러 대기 단계 drain 지원
- [x] 2초 시간창 nearest 추측 제거 및 식별자 없는 관측 fail-closed
- [x] C3 상세 불일치의 매 건 Error 출력을 bounded Warning으로 교정하고 terminal FAIL은 유지
- [x] combat latch 이후에만 C3 BEGIN mode/schema/hash 기록
- [x] 삭제된 `BuildingView` GUID를 보유한 건물 프리팹 8개의 dangling component 제거
- [x] Unity 전체 `[UAS-DIAG]`, `[C2-AUTHORITY]`, `[C3-PRESENTATION-SHADOW]` self-validation PASS
- [x] Android Build And Run 성공 — 2026-08-31 03:57 AAB 생성(141,962,893 bytes), 연결 실기기 설치 및 앱 실행, Unity Console Error 0 확인
- [ ] 새 빌드의 Android Host / Editor Client 실기 검증
- [ ] 역할 교대 실기 검증

이번 교정은 서버 타겟 선택·피해·HP·사망 writer와 기존 VFX emitter를 변경하지 않았다. 다음 실기 PASS 전까지 C3는 계속 read-only Shadow이며 실제 표현 전환은 금지한다.

## 12. v3 진단 경계 및 동일 계약 입장 gate 교정 계획 (2026-08-31)

> **계획 변경 이유:** `sharedSessionKey=beb0207a…9410f` 재검증은 Android Client가 C3 v2/PresentationShadow/schema 1, Editor Host가 C3 v1/Legacy/schema 0을 실행한 혼합 경기였다. 이 경기는 C3 실기 판정에 사용할 수 없다. 또한 현재 구현은 C2 result terminal과 C3 presentation terminal에 같은 종합 verdict를 써서, C2 결과가 정상이어도 C3 표현 실패가 C2 FAIL로 보이는 진단 결합 결함이 있다.

### 12.1 RED 회귀 fixture

`RunUnitActionSelfValidation`에 먼저 다음 독립 판정 fixture를 추가하고 현재 구현에서 RED를 확인한다.

1. C2 결과 실패·거부가 0이고 C3 unmatched 또는 direction mismatch만 존재하면 `result=EVIDENCE`, `presentation=FAIL`, `overall=FAIL`이다.
2. C2 결과 실패가 있고 C3 표현 증거가 정상이라면 `result=FAIL`, `presentation=EVIDENCE`, `overall=FAIL`이다.
3. C2와 C3가 모두 정상이고 공통 drop·manifest·terminal preflight 실패도 없을 때 세 로컬 판정은 정상 증거인 `EVIDENCE`다. 최종 PASS는 동일 경기 Host/Client 교차감사에서만 선언한다.
4. terminal preflight 실패는 세 terminal을 안전한 FAIL 축약문으로 내보내며 PASS로 숨겨지지 않는다.
5. Host/Client의 observer schema, pipeline mode, combat schema 중 하나라도 다르면 교차감사는 수치 비교 전에 `INCONCLUSIVE(schema-mismatch)`로 끝난다.

### 12.2 observer v3 구현

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- Host/Client C3 terminal을 결합하는 기존 교차감사 코드(실제 호출 지점을 구현 전 재확인)

작업:

1. `DetermineLocalVerdict`를 C2 결과, C3 표현, overall 세 개의 순수 분류 함수로 분리한다.
2. `BuildResultTerminalData`에는 C2 verdict만, `BuildPresentationTerminalData`에는 C3 verdict만 전달한다.
3. overall은 C1~C3 실패와 공통 observer 건전성 실패를 합산하되 하위 terminal의 원인을 바꾸지 않는다.
4. observer schema를 `c3-authoritative-result-presentation-shadow-v3`로 올려 v1/v2 로그와 혼합 판정되지 않게 한다.
5. 교차감사는 Host/Client BEGIN의 `observerSchema + pipelineMode + combatSchema + sharedSessionKey`가 완전히 일치한 경우에만 C2/C3 수치를 비교한다.
6. 이 변경은 read-only 진단 경계만 다루며 서버 권위 gameplay와 기존 presentation emit 경로를 수정하지 않는다.

### 12.3 구현·빌드·실기 gate

1. Unity Editor 재컴파일 후 `Library/ScriptAssemblies/Assembly-CSharp.dll` 수정 시각이 현재 소스 이후이고 v3 문자열을 포함하는지 확인한다.
2. 전체 `[UAS-DIAG]` 및 C2/C3 전용 self-validation이 PASS해야 한다.
3. 자동 gate PASS 뒤에만 Android `Build And Run`을 실행한다. schema가 바뀌므로 재빌드는 필수다.
4. 전체 경기 전 짧은 매칭 입장에서 Host/Client BEGIN이 모두 v3/PresentationShadow/combat schema 1이며 같은 `sharedSessionKey`인지 확인한다.
5. 입장 gate가 다르면 즉시 테스트를 중단하고 재컴파일·재빌드한다. 전체 경기 로그를 수집하거나 채점하지 않는다.
6. 입장 gate 통과 뒤에만 역할 교대 전체 경기를 진행해 `result-END`, `presentation-END`, overall `END`를 독립 판정한다.

### 12.4 완료 조건

- 혼합 schema 경기가 PASS 또는 gameplay FAIL로 오인되지 않는다.
- C2 정상/C3 실패가 서로 다른 terminal verdict로 정확히 표현된다.
- 동일 v3 역할교대 경기에서 C2 결과 건전성과 C3 방향·타이밍 수렴을 각각 판정할 수 있다.
- 실제 emitter 전환은 이 gate가 통과하고 사용자가 별도로 승인하기 전까지 금지한다.

## 13. v3 구현 및 자동 gate 결과 (2026-08-31)

- [x] observer schema를 `c3-authoritative-result-presentation-shadow-v3`로 변경
- [x] C2 result, C3 presentation, overall verdict를 독립 계산
- [x] C2 정상/C3 실패와 C2 실패/C3 정상 조합 회귀 추가
- [x] 무증거와 terminal preflight fail-closed 회귀 추가
- [x] Host/Client schema·pipeline mode·combat schema·session key 호환성 순수 gate 추가
- [x] C3 전용 자동 교차감사기가 현재 없음을 확인하고 이동 CrossAudit에 책임을 섞지 않음
- [x] Unity Runtime/Editor 재컴파일 및 Console Error 0
- [x] `Assembly-CSharp.dll`에서 v1/v2 없음, v3 포함 확인
- [x] 전체 `[UAS-DIAG]`, `[C2-AUTHORITY]`, `[C3-PRESENTATION-SHADOW]` self-validation PASS
- [x] Android Build And Run 성공 — `Build/Hexiege.aab`, 2026-08-31 11:19, 141,963,656 bytes
- [x] 연결 실기기 설치 및 `com.gorocompany.hexiege` 실행 로그 확인
- [ ] 동일 v3 Host/Client BEGIN 입장 gate
- [ ] Android Host / Editor Client 전체 경기
- [ ] Editor Host / Android Client 역할 교대 전체 경기

자동 gate는 완료했다. 다음 테스트는 전체 경기를 바로 시작하지 않고, 먼저 양쪽 BEGIN에서 `observerSchema=v3`, `pipelineMode=PresentationShadow`, `combatSchema=1`, 동일 `sharedSessionKey`를 확인한다. 이 입장 gate가 통과한 경기만 C2/C3 수치 판정에 사용한다.

## 14. 동일 v3 실기 FAIL 교정 계획 (2026-08-31)

### 14.1 RED 회귀 계약

1. 커밋 승인 RPC가 공격 상태 `NetworkVariable`보다 먼저 도착해도 Marker/Tracer는 RPC에 함께 실린 정확한 공격자 instance·sequence만 사용해야 한다.
2. provisional Attack은 유효 공격 scope를 갖지 않고 Marker를 소비하지 않는다. 커밋은 Attack 클립을 재시작하지 않으면서 새 scope와 hit 0을 원자적으로 연다.
3. 65개 이상의 서로 다른 공격자 회차가 동시에 존재해도 서로의 결과나 Legacy 관측을 축출하지 않는다. 한 공격자 회차 안에서 64개를 넘는 입력만 그 회차의 bounded overflow로 분류한다.
4. Unmatched·direction·timing 실패는 단계, 공격자, 타겟, scope/result key, 시간차 또는 각도차가 포함된 bounded 표본을 남긴다.
5. 방향 허용치는 규칙의 공격 커밋 허용치와 같은 이름 있는 `8°` 계약을 사용하고 `8.000°/8.001°` 경계를 self-validation으로 잠근다.

### 14.2 구현 순서

1. `ExecuteAttack`이 Shadow 예약으로 확정한 `AttackerInstanceId + AttackSequenceId + firstHitIndex`를 반환하도록 바꾼다.
2. 서버는 회차 생성·게시를 먼저 완료한 뒤, target·presentation revision·impact 승인·정확한 scope를 하나의 `StartCombatClientRpc` payload로 전송한다.
3. `NetworkCombatStartedEvent`와 `UnitView.StartCombatAnimation`이 이 scope를 함께 받아 로컬 Marker cursor를 초기화한다. Marker는 더 이상 별도 `NetworkVariable` 도착 시점에 의존하지 않는다. 복제 state는 관찰·복구용으로 보존한다.
4. Presentation Shadow scheduler의 결과와 Legacy 대기열을 공격자 회차별로 분리하고 각 회차 64개·2초 계약을 적용한다. 수명 종료와 만료 시 내부 dedupe 상태도 함께 정리한다.
5. observer schema를 v4로 올리고 단계별 unmatched와 bounded 방향·타이밍 실패 표본을 추가한다. Android 한 줄 UTF-8 상한 preflight를 다시 통과시킨다.

### 14.3 완료 gate

- 전체 `[UAS-DIAG]`와 C2/C3 전용 self-validation PASS
- Unity compile error 0, Console Error 0
- 문서 정합성 검사 0건
- 위 자동 gate가 PASS한 뒤에만 Android Build And Run 실행
- 새 빌드의 동일 v4 Host/Client 경기에서 C2 결과 PASS와 C3 presentation PASS를 별도로 확인
- C3 PASS 전까지 실제 emitter 전환 금지

### 14.4 구현·자동 검증 결과

- [x] 공격 회차를 먼저 생성하고 target·revision·impact 승인·instance·sequence·hit를 단일 RPC로 전달
- [x] UnitView Marker cursor의 별도 NetworkVariable 도착 순서 의존 제거
- [x] C3 read-only 원칙에 따라 scope 실패 시에도 기존 Legacy VFX/SFX emitter 보존
- [x] 결과·Legacy 관측 64개 상한을 공격자 회차별로 격리하고 overflow를 명시적 Unmatched로 분류
- [x] expire·attacker retire 시 result·Legacy·consumed-stage 수명 정리
- [x] observer v4 단계별 unmatched 및 bounded 방향·타이밍 상세 증거
- [x] 이름 있는 8.000°/8.001° 방향 경계 회귀
- [x] Unity compile error 0
- [x] 전체 `[UAS-DIAG]`, `[C2-AUTHORITY]`, `[C3-PRESENTATION-SHADOW]` PASS
- [x] 문서 정합성 검사 0건
- [x] Android Build And Run 성공 — 1,533초, AAB 141,976,729 bytes, 연결 기기 PID 23340 실행 확인
- [ ] 동일 v4 Host/Client BEGIN 입장 gate
- [ ] 동일 v4 멀티플레이 전체 경기 C2/C3 terminal 판정
- [ ] 역할 교대 경기

다음 실기 검증 전까지 C3는 계속 `PresentationShadow` read-only이며 실제 emitter 전환은 금지한다.

## 15. 동일 v4 연속 공격 scope FAIL 교정 계획 (2026-08-31)

동일 v4 경기에서 C2 결과·B3·ROOT는 정상이나 C3 Marker/Tracer가 Host 2,230건, Client 2,255건 Invalid였다. 서버가 매 공격마다 새 회차를 생성하는 것과 달리 정확한 scope RPC는 `_combatAnimationSent` 최초 성공 때만 전송된 것이 원인이다.

교정은 Attack 애니메이션 최초 시작 상태와 매 공격 커밋 scope 발행을 분리한다. 매 커밋마다 새 revision·instance·sequence·first hit를 같은 RPC로 보내되 `restartAttackCycle=false`를 유지하여 현재 Attack 클립을 되감지 않는다. UnitView는 최신 scope의 marker cursor만 hit 0으로 갱신한다. 연속 3회 공격에서 scope 3회, CrossFade 최초 1회라는 회귀를 추가하고 전체 self-validation PASS 뒤에만 Build And Run을 실행한다.

세부 Research/Plan은 `Assets/_Project/Docs/_Tasks/2026-08-31/17_09_c3-continuous-attack-scope-correction/`에 분리했다.

## 16. 후속 v5 C3-only 교정 문서 위치 (2026-09-01)

v4 이후 구현과 동일 v5 실기 FAIL의 최신 계획은 `Assets/_Project/Docs/_Tasks/2026-08-31/23_30_b3-c3-runtime-failure-correction/Plan.md` 11절에서 관리한다. C2 서버 권위 결과와 B3 이동은 PASS를 유지하며 이번 후속 범위에서 변경하지 않고, C3 Marker·Tracer·결과 수명, timeout 의미, Client revision/scope 수렴만 교정한다. 반복 로그의 단일 출처는 `Assets/_Project/Docs/_Logs/2026-08-31/12_54_c3-presentation-shadow-correction/Log.md` Round 7이다.
