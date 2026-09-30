# C3 권위 결과 기반 Presentation Shadow Research

이번 작업은 서버가 이미 확정한 공격 결과를 클라이언트가 정확한 대상·방향·시점으로 표현할 수 있는지, 실제 화면 효과를 바꾸기 전에 안전하게 검증하는 단계다. 서버 피해와 HP는 지금처럼 즉시 적용하고, 새 경로는 화면 효과를 내지 않은 채 기존 `OnAttackHit`·공격자별 FIFO 표현과 결과만 비교한다. 이 비교가 통과해야 이후 경기에서 기존 표현과 신규 표현 중 하나만 선택하는 전환을 시작할 수 있다.

## 1. 착수 기준과 현재 상태

- C2 최신 집중 경기 `sharedSessionKey=f2ad63c0…171d8`은 Android Host / Editor Client, 8/25종 범위에서 PASS했다.
- 공격 커밋 2,144건, 서버 결과 2,182건에서 타겟·회차·결과 상관 실패와 Client 결과 거부가 0이었다.
- B3 이동도 이 경기 범위에서 adapter·진전·writer 충돌 없이 PASS했다.
- 이 결과는 **8/25종 집중 범위**의 근거다. 나머지 17종, 반대 역할, 다음 경기 Legacy rollback은 통합 회귀 대기 상태다.
- C2는 `AttackImpactResult`를 Reliable Shadow 메시지로 Host/Client에 전달하고 전체 결과 키로 중복·충돌을 분류하지만, Client 표현에는 아직 연결하지 않는다.

## 2. 규칙상 C3가 해결해야 하는 것

`GameSystemRules_UnitCombatSynchronization.md`의 `NET-AUTH-001`·`NET-AUTH-002`는 서버만 판정하고 권위 상태와 표현 예약을 분리하도록 한다. `NET-ACTION-IDEMPOTENT`는 FIFO가 아니라 전체 결과 키로 표현을 최대 한 번 적용하도록 하고, `NET-TIME-001`~`NET-TIME-005`는 동기화 서버 시각과 지연 흡수·catch-up·순서 역전·늦은 참가 처리를 요구한다. `NET-FACING-002`는 과거 타겟 위치를 Client가 다시 읽지 말고 서버 `AuthoritativeAimDirection`을 사용하도록 한다. `NET-PRESENT-001`~`NET-PRESENT-004`는 Animation Event를 표현 표식으로만 사용하고 권위 결과와 정규 키로 결합하도록 한다.

전환 순서 4단계는 “신규 Snapshot/ImpactResult를 Shadow 전송하고 신규 Presenter는 로그만 남긴다”이다. 5단계는 경기 시작 전에 `CombatSchemaRevision + AttackProfileHash + CombatPipelineMode`를 고정하고, 6단계부터 Legacy와 신규 중 정확히 하나만 emit하도록 한다. 따라서 C3는 **4단계의 Presenter Shadow와 5단계의 경기 고정 계약 준비**까지만 담당한다. 실제 emitter 전환은 이번 범위가 아니다.

## 3. 현재 서버 결과 경로

### 3.1 순수 결과 계약

`Application/Combat/Sequencing/UnitActionContracts.cs`의 `AttackImpactResult`에는 다음 값이 이미 있다.

- `AttackerInstanceId + AttackSequenceId + HitIndex + VictimKind + VictimId + EffectKind + ResultOrdinal`
- `ActionRevision`
- `ImpactServerTime`
- `AuthoritativeAimDirection`
- 선택적 `ImpactPosition`
- outcome, 적용량, 결과 HP

새 Presenter가 별도의 공격 결과 모델을 만들 필요는 없다. 이 값을 표현 전용 불변 입력으로 투영하면 된다.

### 3.2 네트워크 전달

`Infrastructure/Network/NetworkUnitActionShadowState.cs`의 `NetworkAttackImpactShadowResult`가 전체 키와 위 결과를 NGO 원시 값으로 직렬화한다. `NetworkUnit.PublishAttackShadowImpactResult`는 서버에서 결과를 분류한 뒤 Reliable `ClientRpc`로 보내고, Client는 `UnitAttackShadowObserver.ObserveClientImpactResult`에만 전달한다.

현재 Host는 서버 결과 observer만 기록하고 `ClientRpc` 수신 경로는 건너뛴다. C3는 Host와 순수 Client가 같은 표현 정책을 비교해야 하므로 서버 로컬 결과도 동일한 read-only Presenter 입력으로 전달하는 별도 bridge가 필요하다. 이 bridge는 결과를 재적용하거나 RPC를 되돌려 보내면 안 된다.

## 4. 현재 Legacy 표현 경로

### 4.1 피격 표현 FIFO

`Presentation/Effects/HitPresentationQueue.cs`는 `OnEntityDamaged`를 공격자 ID별 FIFO에 넣고 `OnLocalAttackHit`에서 방출한다. 단일 marker AoE는 큐 전량, 다중 marker는 한 건을 방출하며, 순서 역전된 빈 marker는 버린다. 타임아웃·사망·전투 중단·즉시 표현은 별도 flush 경로다.

이 큐의 `EntityDamagedEvent`에는 공격자 ID, 피해자 종류·ID, 결과 HP는 있지만 `AttackerInstanceId`, `AttackSequenceId`, `HitIndex`, `ResultOrdinal`이 없다. 따라서 기존 FIFO 자체를 정규 결과 키로 오인하면 안 된다. C3 비교기는 서버 결과 키를 기준으로 유지하면서, Legacy enqueue/emit을 공격자 ID·피해자·결과 HP·시간·marker 순서로 제한된 시간창 안에서 양방향 상관해야 한다. 정확히 하나를 고를 수 없으면 추측하지 않고 ambiguous 또는 unmatched로 실패 분류한다.

### 4.2 로컬 공격 marker와 방향

`Presentation/Unit/UnitView.cs::OnAttackHit`는 커밋된 marker에서 공격 VFX·SFX를 내고, 원거리는 로컬 타겟 위치로 트레이서를 발사한 뒤 착탄 callback에서 `OnLocalAttackHit`을 발행한다. 현재 방향과 타겟은 로컬 `PresentationTransform`과 `_combatTarget*`을 다시 읽는다.

이 동작은 C3가 찾으려는 차이의 비교 대상이다. C3에서 기존 경로를 서버 방향으로 바꾸면 Shadow가 아니므로 금지한다. 대신 marker 시점의 실제 로컬 표현 방향·선택한 표시 타겟·발화 시각을 값으로 관측하고, 서버 결과의 권위 방향을 해당 Client의 view space로 변환해 비교한다. Red 관점 변환과 모델 고유 오프셋을 섞어 서버 방향을 다시 정의하지 않는다.

## 5. 현재 시간 계약의 공백

서버 결과는 `ImpactServerTime`을 갖지만 Presentation 레이어에는 동기화 서버 시각을 읽는 명시적 clock이 없다. `NetworkCombatController`와 observer는 `NetworkManager.ServerTime.Time`을 직접 읽지만 Presentation이 Netcode에 직접 의존해서는 안 된다.

C3는 Application에 순수 시각 계산기와 clock 입력 계약을 두고 Infrastructure가 현재 동기화 서버 시각을 제공하도록 해야 한다. `PresentationServerTime = SynchronizedServerTime - CombatPresentationDelay`를 사용하며 초기 delay는 규칙값 0.10초다. 실제 결과가 이미 표현 시각을 지났다면 소급 재생으로 시간을 되돌리지 않고 catch-up으로 분류한다. 0.50초를 넘긴 오래된 결과, 비유한 시각, 역행 시각은 fail-closed 한다.

Shadow 단계는 실제 VFX를 예약하지 않지만 “예약했다면 언제였는가”를 계산해 Legacy 실제 marker/FIFO emit 시각과 비교해야 한다.

## 6. 경기 단위 고정 계약의 현재 공백

`Application/NetworkContext.cs`와 `Infrastructure/Network/NetworkGameFlow.cs`에는 B3 이동용 `UnitMovementPipelineMode + schema` handshake와 경기 latch가 이미 있다. 그러나 전투용 `CombatPipelineMode`, `CombatSchemaRevision`, `AttackProfileHash`는 규칙에만 있고 코드에는 없다.

C3 구현에서는 이 기존 패턴을 확장하되 이동 mode와 혼합하지 않는다.

- 서버가 다음 경기 전투 mode를 선택한다.
- 양쪽이 같은 combat schema와 deterministic profile hash를 지원하는지 준비 RPC에서 확인한다.
- 불일치하면 게임 시작을 거부하거나 서버가 명시적으로 선택한 전체 경기 Legacy로만 시작한다.
- 시작 RPC에서 받은 계약을 `NetworkContext`의 전투 latch에 한 번만 고정한다.
- 진행 중 mode/hash 변경과 유닛별 혼합은 거부한다.

현재 `UnitAttackShadowProfileResolver`는 25종 manifest를 명시하지만 Projectile·Traveling·일부 특수 결과는 `Unresolved`다. hash는 이 상태까지 포함해 결정적으로 계산해야 하며, `Unresolved`를 지원 완료로 바꾸지 않는다.

## 7. 제안하는 C3 구조

### 7.1 Application 순수 계약

- `CombatPipelineMode`: Legacy / PresentationShadow / ResultPresentation의 경기 단위 값
- combat match latch: mode·schema·profile hash를 원자적으로 한 번 고정
- 표현 입력 DTO: 네트워크 타입을 노출하지 않고 전체 결과 키·공격자 로컬 ID·Impact 시각·권위 방향·결과를 보존
- 순수 Presentation Shadow scheduler: 유효성, exact-key dedupe/conflict, 동기화 서버 시각, delay, late/catch-up, retire를 분류
- bounded 상관기: 서버 결과, Legacy damage enqueue/emit, local marker를 양방향으로 보관하고 exact/ambiguous/unmatched를 분류

### 7.2 Infrastructure bridge와 observer

- 서버 로컬과 Client 수신 결과를 같은 Application DTO로 변환해 Presenter에 한 번 전달
- 동기화 서버 clock 제공
- combat mode/schema/profile hash handshake 및 latch
- 기존 `UnitAttackShadowObserver`에 C3 전용 bounded 카운터와 terminal 증거 추가
- NetworkObject/attacker instance retire 시 scheduler와 비교 이력 폐기

### 7.3 Presentation observer-only adapter

- 새 `UnitAttackResultPresentationShadow`는 실제 emit API를 보유하지 않는다.
- `UnitView`는 marker 시점의 실제 표시 타겟·view 방향·시각을 관측 값으로만 발행한다.
- `HitPresentationQueue`는 enqueue와 실제 emit 시점을 관측 값으로만 발행한다.
- 신규 scheduler의 예정 시각·타겟·방향과 Legacy 관측을 비교해 observer에 전달한다.
- HP 텍스트, VFX, SFX, 트레이서, 타격 반응, Animator를 호출하지 않는다.

## 8. 알려진 범위와 위험

1. **주 결과와 특수 부가 결과의 차이:** 현재 C2는 Legacy 주 결과를 중심으로 `AttackImpactResult`를 만든다. AoE의 모든 피해자, Periodic tick, 실제 Projectile/Traveling 독립 전달체 결과가 전부 정규화된 상태는 아니다. C3가 받은 결과는 정확히 비교하되, 받지 못한 결과를 성공으로 세지 않는다.
2. **Legacy FIFO의 키 부족:** 시간상 가까운 항목을 무조건 같은 결과로 추측하면 잘못된 PASS가 된다. exact candidate가 하나가 아니면 ambiguous로 남긴다.
3. **Host 이중 처리:** Host는 서버이자 Client다. 서버 로컬 bridge와 ClientRpc를 둘 다 소비하지 않도록 한 경로만 사용한다.
4. **방향 좌표계:** 서버 aim을 Client view space로 변환한 뒤 표시 방향과 비교한다. 모델 고유 mesh offset을 서버 판정 방향으로 되돌려 쓰지 않는다.
5. **늦은 결과:** catch-up은 허용 정책이지 FIFO 다음 공격에 붙이는 권한이 아니다. 정규 키로 현재 결과를 한 번만 분류한다.
6. **로그 상한:** 매 hit 전체 로그는 Android terminal을 다시 절단시킬 수 있다. 정상은 카운터와 제한된 대표 표본, 실패만 bounded detail로 남긴다.
7. **Shadow 오염:** 신규 코드가 기존 `Emit`, `Play*`, Animator, HP 이벤트를 한 번이라도 호출하면 C3 실패다.

## 9. 조사 결론

C3는 기존 C2 결과 전송을 다시 설계하는 작업이 아니라, 그 결과를 안전한 표현 입력으로 승격해 Legacy 표현과 비교하는 작업이다. 가장 중요한 구조는 **서버 결과 exact key가 기준이고 Legacy FIFO는 비교 대상일 뿐**이라는 점이다. 이번 구현은 read-only여야 하며, 실제 표현 교체는 C3 로그가 역할교대·지원 범위에서 수렴한 뒤 별도 승인으로 진행한다.

Testcase는 사용자가 요청하지 않았으므로 작성하지 않는다.

## 15. 동일 v4 연속 공격 scope 실패 분석 (2026-08-31)

동일한 v4/PresentationShadow/combat schema 1 경기에서 C2는 Host 결과 1,278건과 Client 수락 1,278건이 모두 정상이고 B3·ROOT도 정상이다. C3만 Host `invalid=2,230`, Client `invalid=2,255`로 실패했으며 대표 표본은 instance 0 / sequence 0의 Marker·Tracer였다.

코드와 로그를 대조한 결과 서버 `ExecuteAttack`은 매 쿨다운 주기 새 회차를 만들지만, 그 scope를 전달하는 `StartCombatClientRpc`가 `_combatAnimationSent.Add(id)` 최초 성공 분기 안에 있었다. Attack 루프를 한 번만 시작하려는 표현 가드가 후속 공격 회차 데이터까지 막은 것이다. v4 교정은 이전 unmatched·direction을 크게 감소시켰지만 이 별도 수명 문제는 남겼다.

이번 후속 작업은 서버 gameplay writer를 바꾸지 않는다. Attack 클립은 계속 재생하고 모든 커밋 scope만 원자적으로 갱신한다. 상세 조사는 `Assets/_Project/Docs/_Tasks/2026-08-31/17_09_c3-continuous-attack-scope-correction/Research.md`에 기록했다.

## 10. 구현 후 확인 결과 (2026-08-30)

- 경기 시작 시 `CombatPipelineMode + CombatSchemaRevision + AttackProfileHash`를 한 번 latch하고, 다른 값의 재수신은 거부하도록 구현했다.
- Host publication과 Client 수신 결과를 같은 Application DTO 및 scheduler로 전달하되 Host가 RPC 결과를 다시 소비하지 않도록 분리했다.
- scheduler는 `0.10초 PresentationDelay`, `0.50초 CatchUp`, `2.000초 reorder`, 최대 64개 보관을 각각 독립 계약으로 적용한다. `2.001초` 또는 delay 흡수 후 `0.501초` 늦은 결과는 다음 marker에 붙이지 않고 만료한다.
- Red 진영의 180도 반전된 Visual Root forward는 관측 시점에 도메인 방향으로 환원하며 서버 aim 자체는 변경하지 않는다.
- `UnitView`와 `HitPresentationQueue`에는 read-only 관측 seam만 추가했다. 신규 Shadow에는 gameplay writer와 VFX·SFX·Animator emitter 의존성이 없다.
- C2 결과 terminal과 C3 표현 terminal을 각각 `result-END`, `presentation-END`로 분리해 최악값 전체 필드가 Android UTF-8 한 줄 상한 검증을 받도록 했다.
- Unity Runtime/Editor 컴파일은 성공했다. 기존 NGO obsolete 및 기존 미사용 필드 경고 외 C3 오류는 0건이다.
- `Run Unit Action Self Validation`에서 A1~C2 전체와 `[C3-PRESENTATION-SHADOW]`가 PASS했다.
- Android `Build And Run`이 성공해 `Build/Hexiege.aab`를 생성했고, 연결 실기기에서 `com.gorocompany.hexiege` 실행 프로세스를 확인했다. 첫 IL2CPP 풀 빌드 산출물은 후속 증분 빌드 캐시로 유지된다.

이 결과는 자동 회귀 PASS이며 멀티플레이 실기 결과가 아니다. 실제 emitter 전환과 기존 FIFO 제거는 계속 금지된다.

## 11. 첫 멀티플레이 실기 실패 분석 (2026-08-31)

이번 실기에서 C2 서버 권위 결과는 정상 전달됐다. 서버가 만든 결과 2,220건을 Android Client도 2,220건 모두 수락했고, 결과 거절·타겟 불일치·상관 실패는 0건이었다. 실패한 곳은 그 결과를 기존 화면 표현과 비교하는 C3 채점기다.

현재 C3는 Legacy 표현에 정규 결과 키가 없어서 `공격자 + 피해자 + 결과 HP + 2초 시간창`으로 후보를 찾는다. Animation marker는 결과 HP도 없기 때문에 같은 공격자가 같은 대상을 연속 공격하면 후보가 2~12개까지 늘어난다. 또한 같은 결과를 Marker, Tracer, Enqueue, Emit 단계가 각각 다시 사용할 수 있어 Host의 `matched=5,303`, Client의 `matched=4,541`이 실제 결과 2,220건보다 커졌다. 이 상태의 방향·시간 mismatch는 어떤 공격과 비교했는지 확정할 수 없으므로 gameplay 실패 근거로 사용할 수 없다.

Android의 Error 482건 중 479건은 위 `Ambiguous`를 C3가 매 건 Error로 기록한 진단 로그다. FATAL, ANR, 예외 또는 비-UAS Error는 없었다. 즉 게임이 충돌한 것이 아니라 read-only 채점기의 예상 불일치가 사용자용 오류 채널을 오염시켰다.

별개로 8개 건물 프리팹에는 이미 삭제된 `Hexiege.Presentation.BuildingView` 직렬화 컴포넌트가 남아 있어 Missing Script 경고 14건을 만들었다. `BuildingFactory`가 건물 생명주기를 단독 관리하는 현재 구조와 맞지 않는 잔재이므로 스크립트를 복원하지 않고 프리팹의 유령 컴포넌트를 제거한다.

## 12. 교정 구현 결론 (2026-08-31)

C3 상관 기준을 시간과 HP의 유사성에서 **서버가 발급한 정규 결과 영수증**으로 교체했다. Enqueue/Emit은 피해 이벤트와 Client HP 동기화를 통해 전달된 전체 `AttackResultKey`를 사용한다. Marker/Tracer는 원자 행동 상태에서 캡처한 `AttackerInstanceId + AttackSequenceId + HitIndex`만 사용하며, Tracer는 발사 때의 scope를 착탄까지 보존한다. 식별자가 없으면 다른 공격을 추측하지 않는다.

같은 공격 결과를 화면 파이프라인의 여러 단계가 관측하는 것은 정상이다. 따라서 결과 하나를 한 번만 쓰는 모델 대신, 공격별로 Marker·Tracer·Enqueue·Emit 단계 도장을 각각 한 번만 허용한다. 동일 단계 재수신만 Duplicate이고 서로 다른 단계는 독립 비교다. 이 구조로 실제 결과보다 matched가 비정상적으로 부풀던 원인을 제거했다.

Android 화면에 반복 표시된 Error는 gameplay 예외가 아니라 C3의 `Ambiguous` 상세 로그였다. 상세 표본은 제한된 Warning으로 낮추고 최종 terminal 판정만 FAIL을 유지해, 문제를 숨기지 않으면서 사용자 오류 UI 오염을 막았다. `BuildingView` Missing Script는 현재 팩토리 소유권과 충돌하는 직렬화 잔재 8개를 제거해 해결했다.

자동 검증은 전체 UAS와 C2·C3 전용 계약이 모두 PASS했다. 다만 이는 상관 알고리즘 회귀 검증이며, 새 빌드의 멀티플레이 방향·타이밍 수렴은 Host/Client 실기 로그로 별도 확인해야 한다.

## 13. 첫 v2 재검증 무효 판정과 terminal 판정 결합 결함 (2026-08-31)

`sharedSessionKey=beb0207a…9410f` 경기는 같은 경기였지만 같은 C3 계약을 실행한 경기가 아니었다. Android Client는 `observerSchema=c3-authoritative-result-presentation-shadow-v2`, `pipelineMode=PresentationShadow`, `combatSchema=1`이었고, Editor Host는 이전 어셈블리의 `observerSchema=...-v1`, `pipelineMode=Legacy`, `combatSchema=0`이었다. 따라서 Android의 `ambiguous=0`은 v2 exact-correlation 개선 증거로 참고할 수 있지만, Host/Client 일치나 C3 실기 PASS/FAIL의 근거로 사용할 수 없다.

혼합 버전이 발생한 직접 원인은 소스와 Editor가 실제 실행한 어셈블리의 불일치다. 현재 소스의 observer schema는 v2지만 당시 `Library/ScriptAssemblies/Assembly-CSharp.dll`은 v1 문자열을 포함한 이전 빌드였다. 이후 검증은 전체 경기를 시작하기 전에 양쪽 BEGIN의 `observerSchema + pipelineMode + combatSchema + sharedSessionKey`가 모두 일치하는지 확인하는 짧은 입장 gate를 먼저 통과해야 한다. 하나라도 다르면 해당 경기는 `INCONCLUSIVE(schema-mismatch)`로 종료하고 수치 비교를 금지한다.

또한 현재 `UnitAttackShadowObserver.EndSession`은 `DetermineLocalVerdict()` 하나를 계산해 overall `END`, C2 `result-END`, C3 `presentation-END`에 모두 전달한다. 이 때문에 Android에서 `resultFailures=0`, `clientResultRejected=0`인데도 C3의 `unmatched=279`, `directionMismatch=226` 때문에 `result-END`가 FAIL로 출력됐다. 이는 서버 권위 결과 전달의 건전성과 표현 비교 실패를 분리한다는 C2/C3 진단 경계를 위반한다.

다음 교정은 observer schema를 v3로 올리고 다음 세 판정을 독립 계산해야 한다.

- `result-END`: C2 결과 생성 실패, Client 결과 거부, 결과 계약·전송 증거 오류만 판정한다.
- `presentation-END`: C3 exact-stage 상관 실패, unmatched, direction/timing mismatch, duplicate/conflict, lifecycle 오류만 판정한다.
- overall `END`: C1~C3 및 공통 로그 건전성을 합산하는 최종 판정으로 유지한다.

서버 타겟 선택·피해·HP·사망 writer와 기존 VFX emitter는 이 교정 범위가 아니다. 로컬 terminal은 최종 교차감사 전의 정상 증거를 `EVIDENCE`로 표현하므로, v3 self-validation은 “C2 정상 + C3 실패” fixture에서 `result-END=EVIDENCE`, `presentation-END=FAIL`, `END=FAIL`을 요구하고, 반대 조합과 terminal preflight 실패도 각각 독립적으로 검증해야 한다.

## 14. v3 진단 경계 구현 결과 (2026-08-31)

`UnitAttackShadowObserver`를 `c3-authoritative-result-presentation-shadow-v3`로 올리고 C2 결과, C3 표현, 전체 observer 판정을 각각 독립 계산하도록 교정했다. C2 결과 evidence는 server result 또는 Client accepted result이며, C2 failure는 result completion failure, Client result rejection, terminal preflight failure로 한정된다. C3 표현 evidence와 failure는 exact-stage scheduler 계수에서 별도로 계산하며 unmatched, ambiguous, duplicate, conflict, expired, invalid, retired, direction/timing mismatch를 실패로 분류한다. overall은 기존 C1/C2/C3 및 공통 로그 건전성 판정을 계속 합산한다.

`RunUnitActionSelfValidation`에는 C2 정상/C3 실패, C2 실패/C3 정상, 양쪽 무증거, terminal preflight 실패의 독립 조합과 Host/Client schema·combat contract·session 불일치 fixture를 추가했다. C3 전용 자동 교차감사기는 현재 프로젝트에 존재하지 않음을 확인했다. 따라서 호환성 분류는 순수 회귀 seam으로 잠그고, 실기에서는 전체 경기 전에 양쪽 BEGIN 네 필드를 확인하는 입장 gate를 유지한다. 이동 전용 `RunUnitRootPoseCrossAudit`에 공격 책임을 섞지 않는다.

Unity Editor는 새 소스를 재컴파일해 `Assembly-CSharp.dll`에서 v1/v2가 제거되고 v3만 포함된 것을 확인했다. 전체 Unit Action self-validation과 C2/C3 전용 검증이 PASS했고 Console Error는 0이었다. 이어서 Android Build And Run이 성공해 `Build/Hexiege.aab`를 2026-08-31 11:19에 생성했으며 크기는 141,963,656 bytes다. 연결 기기에 설치한 뒤 `com.gorocompany.hexiege`의 새 프로세스 로그 유입도 확인했다.

이 결과는 자동 회귀와 빌드·설치 gate의 완료다. 아직 동일 v3 Host/Client BEGIN 입장 gate와 역할 교대 전체 경기 결과는 없으므로 C3 실기 PASS 또는 실제 emitter 전환 완료를 뜻하지 않는다.

## 15. 동일 v3 실기 경기 결과와 구조 결함 확정 (2026-08-31 12:54)

`sharedSessionKey=49c170e7db50af7b728dcddd5cbfa568e807437ece91882486437b306e0cf82d` 경기에서 Editor Host와 Android Client가 모두 `observerSchema=c3-authoritative-result-presentation-shadow-v3`, `pipelineMode=PresentationShadow`, `combatSchema=1`로 입장했다. 이번 결과는 이전처럼 schema가 섞였거나 Relay가 중단된 표본이 아니라 동일 계약의 유효한 멀티플레이 표본이다.

- B3 이동은 양쪽 `EVIDENCE`, ROOT는 Host 41개·Client 42개 안정 endpoint와 오류 0으로 PASS했다.
- C2는 Host가 만든 1,470개 서버 결과를 Client가 1,470개 모두 수락했다. `resultFailures=0`, `clientResultRejected=0`, `targetMismatches=0`, `correlationFailures=0`이므로 서버 권위 피해·결과 복제는 정상이다.
- C3는 Host와 Client 모두 FAIL이다. Host는 `unmatched=915`, `directionMismatch=59`, `timingMismatch=29`, Client는 `unmatched=866`, `directionMismatch=320`, `timingMismatch=23`이다. duplicate/conflict/ambiguous/expired/invalid/retired는 모두 0이다.
- 게임 종료 시 정상 `network-despawn` 외 Relay·ANR·런타임 예외는 없다. 따라서 이번 C3 실패를 네트워크 중단으로 무효화하지 않는다.

코드 추적에서 첫 구조 결함을 확정했다. `NetworkCombatController.TickCombat`은 커밋 경계에서 `StartCombatClientRpc(... impactEnabled:true)`를 먼저 전송하고, 그 뒤 `ExecuteAttack`에서 Shadow 공격 회차를 생성·게시한다. `UnitView`는 승인 비트는 RPC에서 받지만 Marker/Tracer의 `AttackerInstanceId + AttackSequenceId`는 별도 `NetworkVariable`에서 읽는다. 두 전송 채널의 도착 순서는 원자적이지 않으므로 Client가 새 회차 승인을 받고도 이전 회차 또는 아직 없는 회차를 Marker에 붙일 수 있다. Client 방향 불일치가 Host보다 크게 나온 결과도 이 경쟁 조건과 일치한다.

두 번째 구조 결함은 scheduler의 상한 단위다. 규칙은 결과 reorder 상한 64개를 **공격자 회차별**로 요구하지만 현재 구현은 모든 공격자·회차가 공유하는 전역 64개 결과 큐와 전역 64개 Legacy 관측 큐를 사용한다. 다수 유닛 전투에서는 서로 무관한 공격이 정상 후보를 밀어내고, 나중 Marker/Tracer가 도착했을 때 `Unmatched`가 될 수 있다. 단순히 숫자를 키우지 않고 회차별 상한과 전체 수명 정리를 함께 구현해야 한다.

세 번째 진단 결함도 확인했다. 현재 C3 terminal은 aggregate 수치만 남기며 Unmatched·방향·타이밍 실패의 공격자/회차/단계 표본을 남기지 않는다. 또한 방향 mismatch 기준 `0.1°`가 코드에 하드코딩되어 있고 규칙의 실제 타격 정렬 허용치 `8°`와 이름 있는 계약으로 연결되지 않았다. 다음 교정은 C3를 v4로 올려 bounded 상세 증거와 단계별 계수를 제공하고, 방향 허용치를 규칙에서 파생된 단일 상수로 잠근다.

이번 판정은 “구현 PASS, 진단기만 문제”가 아니다. C2 gameplay writer는 PASS지만 C3 표현 상관관계는 실제로 FAIL이며, 실 emitter 전환은 계속 금지한다.

## 16. v4 교정 구현 및 자동 gate 결과 (2026-08-31)

서버의 공격 회차 생성과 표현 승인 순서를 교정했다. `ExecuteAttack`이 Legacy 예약에 결속된 `AttackerInstanceId + AttackSequenceId + firstHitIndex`를 반환하고, 서버는 이 회차가 확정된 뒤에만 target·presentation revision·impact 승인과 함께 하나의 `StartCombatClientRpc` payload로 보낸다. `UnitView`는 Marker cursor를 이 원자 payload로 초기화하며 더 이상 별도 `NetworkUnitActionShadowState`의 도착 순서에서 회차를 추측하지 않는다. Shadow scope가 비정상인 경우에도 C3 read-only 원칙에 따라 기존 VFX/SFX emitter는 끄지 않고 observer만 fail-closed 처리한다.

scheduler의 결과와 Legacy 관측 상한은 전역 64개에서 공격자 회차별 64개로 교정했다. 서로 다른 65개 공격자 회차는 서로를 축출하지 않으며, 같은 회차의 65번째 입력만 해당 회차의 bounded overflow `Unmatched` 증거를 만든다. 만료·attacker retire 때 result·Legacy·consumed-stage 상태를 함께 정리한다.

observer schema는 `c3-authoritative-result-presentation-shadow-v4`로 올렸다. Unmatched를 Marker·Tracer·Enqueue·Emit 단계별로 집계하고, bounded 상세 줄에 attacker unit/instance, sequence, hit, victim, delivery, timing delta, aim delta를 기록한다. 방향 mismatch는 규칙의 실제 공격 정렬 계약과 같은 이름 있는 `8.0°` 상수를 사용하며 `8.000°/8.001°` 경계를 회귀로 고정했다. Android terminal 최악값 UTF-8 preflight에도 새 필드를 포함했다.

Unity 재컴파일 결과 compile error는 0건이었다. 전체 `[UAS-DIAG]`, `[C2-AUTHORITY]`, `[C3-PRESENTATION-SHADOW]` self-validation이 PASS했고 문서 정합성 검사도 0건이었다. 이후 File > Build And Run을 실행해 1,533초 만에 성공했다. `Build/Hexiege.aab`는 2026-08-31 14:08:20에 141,976,729 bytes로 생성됐고, 연결 기기에서 새 앱 프로세스 PID 23340의 로그 유입을 확인했다.

이는 v4 자동 회귀·빌드·설치 gate의 PASS다. 실제 emitter 전환 승인이나 실기 C3 PASS는 아니며, 다음 단계는 이 빌드로 동일 v4 Host/Client 입장 gate를 확인한 뒤 멀티플레이 전체 경기를 다시 수집하는 것이다.

## 17. 후속 v5 C3-only 교정 문서 위치 (2026-09-01)

v4 이후의 B3·C3 통합 교정과 동일 v5 실기 결과는 `Assets/_Project/Docs/_Tasks/2026-08-31/23_30_b3-c3-runtime-failure-correction/`에 이어서 기록한다. 최신 유효 경기에서 C2와 B3는 PASS를 유지했고 C3만 FAIL/OPEN이므로, 구체 수치·원인 분리·다음 계획은 해당 Research 10절과 Plan 11절 및 `Assets/_Project/Docs/_Logs/2026-08-31/12_54_c3-presentation-shadow-correction/Log.md` Round 7을 단일 참조로 사용한다. 이 원본 문서에 최신 계획 전문을 중복 기재하지 않는다.
