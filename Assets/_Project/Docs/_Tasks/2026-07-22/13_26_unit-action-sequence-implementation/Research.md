# Research — 서버 권위 Unit ActionSequence 구현

## 이 작업이 무엇인지

멀티플레이에서 유닛이 가는 방향과 보는 방향이 다르고, 공격 대상과 공격 방향이 어긋나며, 화면의 타격 순간과 실제 피해 시점이 맞지 않는 세 문제를 하나의 서버 권위 행동 흐름으로 교정한다. 기존 전투 수치와 특수 유닛의 능력 의미는 보존하고, 위치·방향·타겟·타격 결과를 같은 행동 회차로 묶어 서로 다른 시점의 정보가 섞이지 않게 만드는 것이 목적이다.

이 문서의 본문은 구현 전 코드 조사 결과이며, 2026-07-22 Tracer A0/A1 및 2026-07-27 Tracer A2 구현·검증 결과는 아래 진행 기록에 추가한다.

## 2026-07-22~27 Tracer A0/A1/A2 진행 결과

### A0 — SpearMan schedule/dispatch Shadow

- enum·값 식별자와 계약 유틸리티(각도 히스테리시스·회차 발급·bounded 결과 버퍼)를 `UnitActionSequencing.cs`로 추가하고, asmdef 변경 없이 실행하는 `RunUnitActionSelfValidation.cs`의 초기 검증 기반을 추가했다.
- `NetworkCombatController.cs`에는 SpearMan만 관측하는 Shadow 진단을 연결했다. 신규 경로는 공격 회차와 예약·방출 비교 로그만 만들며 기존 피해, HP, RPC, VFX를 쓰거나 변경하지 않는다.
- 사용자 멀티플레이 Host 로그에서 204회 예약과 204회 방출이 관측됐다. 고유 회차도 204개였고 누락 0, 중복 0, 타겟 불일치 0, 예약과 방출 사이 facing 변경 0이었다.
- Windup은 전 회차에서 정확히 240ms였다. `dispatch - scheduled` 지연은 최소 0.013ms, 평균 9.105ms, 중앙값 8.226ms, p95 19.862ms, 최대 29.386ms였다. 16.667ms 초과는 27건이었고 33.333ms 및 50ms 초과는 0건이었다.
- Client 로그는 현재 계측 범위상 header만 기록됐다. 따라서 이번 통과는 서버 Shadow 예약·방출의 일관성에 대한 계측 기반 통과이며 Host/Client 양방향 ActionSequence 복제 검증이 아니다.

### A1 — Pure Application 계약과 stateful reducer

- `UnitActionContracts.cs`와 `UnitActionSequencer.cs`를 추가해 NGO·Animator·씬·피해 writer에 의존하지 않는 값 계약, 스냅샷, 타임라인·사거리 계약과 stateful reducer를 구현했다.
- reducer는 revision·서버 시간 fail-closed, AlignToAttack→Windup→Impact→Recovery, commit/cancel/dead, 다중 타격 결정·확인, 회차·revision 고갈 시 원자적 거부를 순수 상태 전이로 처리한다.
- Unity Editor 메뉴 self-validation PASS를 사용자가 확인했다. C# 9/Application 컴파일 PASS, Editor 컴파일 PASS, reflection 기반 `Validate*` 10개 PASS, 최종 Standards/Spec 리뷰 P0~P3 지적 0건이다.
- A1은 순수 Application 경계의 완료다. 런타임 pose/result seam과 피해·RPC·VFX는 연결하지 않았으며, 이동 방향/바라보기, 타겟/공격 방향, 시각 Impact/실제 피해 시점의 세 문제는 아직 해결 완료가 아니다.
- 다음 단계는 **A2 server-authoritative pose seam shadow**다.

### A2 — Server-authoritative pose seam shadow

- 순수 Application 계약 `IUnitActionPoseSource`와 `UnitActionPoseSample`을 추가하고, `UnitView`의 read-only Legacy adapter가 현재 root pose, 타겟 pose, optional 이동 의도와 공격 목표 방향을 제공하도록 연결했다.
- `NetworkCombatController`는 서버의 SpearMan `TargetLocked + MeleeContact + 1-hit` 공격만 A1 reducer의 one-cycle Shadow로 관측한다. 위치·회전·타겟·피해·HP·RPC·VFX·path writer는 추가하지 않았으며 기존 Legacy 분기가 경기 결과의 유일한 권위다.
- 첫 런타임 감사에서 공격자 사망 경계의 pending observer 조기 삭제 가능성을 발견해, `OnUnitDied`에서 관측을 삭제하지 않고 reducer를 `DeadTerminal`로 전환하도록 교정했다. bounded 관측 용량 초과도 무음 삭제 대신 full canonical key를 가진 `capacity-evicted` terminal skip으로 남긴다.
- 사용자가 Unity Editor self-validation PASS를 확인했다. Host 최신 검증 세션(2026-07-27 18:04:04)은 schedule 429 / dispatch 428이며, 차이 1건은 로그 종료 당시 Impact 예정 시각 전의 in-flight 회차다. 완료 회차의 상관관계 누락·중복은 없고, 공격자 사망 2건은 `MarkDead=Accepted`와 `DeadTerminal`로 종료됐으며 `capacity-evicted`와 예외는 0건이다.
- 원격 Client 최신 세션(2026-07-27 18:09:48)은 `[UAS-POSE]` 0건으로 서버 전용 실행 경계를 지켰다. 따라서 A2 런타임 게이트는 PASS다.
- A2 PASS는 서버 권위 pose 관측 seam의 검증 완료만 뜻한다. 이동/바라보기·타겟/공격 방향·시각 Impact/실제 피해 시점 교정과 권위 전환은 아직 완료되지 않았으며, 다음 단계는 **Tracer B Simulation Root / Visual Root 분리**다.

---

## 1. 기준과 범위

- 멀티플레이 Host/Client와 Blue/Red가 기준이다.
- 서버는 이동, SimulationFacing, 타겟, 공격 커밋, Impact, 피해·회복·사망의 유일한 권위자다.
- `Domain → Application → Core → Infrastructure → Presentation → Bootstrap` 방향을 유지한다.
- 기존 A*, UnitStatsConfig, 특수 공격의 게임플레이 의미, 서버 HP 동기화는 폐기하지 않는다.
- 규칙 권위는 `GameSystemRules_Units.md`와 `GameSystemRules_UnitCombatSynchronization.md`다.
- 기존 InfernoSpirit 직접 25+DoT 5×3, QuakeSpirit 직접 20+주변 10 등의 Legacy 의미를 보존한다.
- 사용자 변경인 `Maplestory Bold SDF.asset`과 미추적 Quake 로그 `.meta`는 범위 밖이다.

---

## 2. 현재 실행 경로

### 이동과 회전

`UnitView.MoveAlongPathV3`가 서버에서 A* 경로를 따라 `transform.position`을 Lerp하고 같은 루트의 `transform.rotation`을 `RotateTowards`로 갱신한다. 이동은 회전 완료를 기다리지 않으므로 큰 방향 전환에서도 위치 이동과 회전이 동시에 시작된다.

전투 추격·A* 복귀·힐러 시전도 `UnitView` 내부 코루틴이 직접 위치·회전·Animator를 함께 다룬다. 결과적으로 Simulation과 Presentation의 책임이 한 클래스와 한 Transform에 결합돼 있다.

### Red 클라이언트 관점 변환

`NetworkUnit.LateUpdate`는 Red 클라이언트에서 NetworkTransform 대상 루트의 위치를 `ViewConverter.ToView`로 다시 쓰고 Y 회전에 매 프레임 180°를 더한다. 이는 `Simulation Root`를 클라이언트가 직접 수정하는 구조다.

`ToView`와 180° 회전은 각각 두 번 적용하면 원래 값으로 돌아오는 자기 역함수다. NetworkTransform이 매 프레임 새 서버 값을 쓰지 않는 조건에서 `LateUpdate`가 연속 실행되면 루트 값이 Red/Blue 관점 사이를 왕복할 수 있다. 설령 NetworkTransform이 매 프레임 덮어써 화면이 안정적으로 보이더라도, 같은 Transform을 두 writer가 수정하는 사실은 유지된다.

### 타겟과 공격 방향

`NetworkCombatController`는 약 50ms Tick에서 타겟과 쿨다운을 검사한다. 전투 시작/타겟 변경/종료는 각각 ClientRpc로 보내며, Animator 상태는 별도 NetworkVariable로 전달한다.

`UnitView.StartCombatAnimation`은 타겟 위치로 루트 회전을 즉시 스냅하고 타겟 Transform을 저장한다. 그러나 서버 공격 예약은 방향 오차가 5° 안에 들어왔는지 확인하지 않고 바로 시작한다. `UnitCombatUseCase.ExecuteAttack`은 피해 적용 순간 HexCoord로 `UnitData.Facing`을 다시 계산하므로, 서버 Transform 회전·도메인 Facing·클라이언트가 현재 타겟 Transform으로 계산한 방향이 서로 다른 시간의 값을 사용할 수 있다.

추가로 규칙의 `MeleeContactDistance=0.63`과 런타임 상수 `0.3`이 충돌한다. `ApplyAttackDamage`는 주석과 달리 Impact 순간 사거리·방향을 재검증하지 않고 공격자·타겟 생존만 확인한다. 따라서 정렬을 고쳐도 근접 적중 의미가 규칙과 달라질 수 있다.

### 피해와 표현

`NetworkCombatController.ExecuteAttack`은 각 `HitFrameTimes`만큼 기다리는 코루틴을 생성하고, 만료되면 `UnitCombatUseCase.ApplyAttackDamage`가 피해를 적용한다. 공격 회차 ID나 HitIndex가 데이터에 남지 않는다.

`NetworkHealthSync`는 절대 HP와 공격자 ID를 ClientRpc로 보내고, 클라이언트는 `EntityDamagedEvent`를 재발행한다. `HitPresentationQueue`는 이를 공격자 ID별 FIFO로 보류하다가 로컬 Animation Event `OnAttackHit` 신호에서 방출한다.

로컬 타격 신호가 피해 이벤트보다 먼저 도착하면 빈 신호를 버린다. 이후 도착한 피해 결과는 다음 공격의 타격 신호 또는 쿨다운×1.5 타임아웃을 기다린다. QuakeSpirit처럼 marker가 없는 경우 최대 7.5초까지 지연될 수 있다.

---

## 3. 최소 재현 가능한 피드백 루프

현재 환경에는 Unity 6000.3.5f2 프로젝트 정보와 Test Framework 1.6.0은 있지만 실행 가능한 Unity Editor CLI가 발견되지 않았다. 따라서 구현 첫 단계에서 다음 두 겹의 피드백 루프를 만든다.

### 자동화 가능한 순수 테스트

NGO·Animator·씬 없이 Application 수준에서 다음을 결정적으로 재현한다.

1. 120° 방향 변경에서 AlignToMove 없이 위치가 먼저 변하는 Legacy 모델과, 10° 이내 정렬 후에만 이동하는 신규 모델 비교
2. 공격 방향 오차 30°에서 Legacy가 즉시 피해를 예약하는 것과 신규 모델이 AlignToAttack에 머무는 것 비교
3. `LocalMarker → DamageResult` 순서에서 FIFO가 marker를 버리고 다음 공격까지 결과를 지연하는 시퀀스 재현
4. 동일 결과 중복·순서 역전·취소 후 결과·늦은 참가 baseline을 정규 결과 키로 처리하는지 검증
5. Red 관점 변환을 같은 Simulation Root에 연속 두 번 적용했을 때 값이 왕복하는지, Visual Root에만 적용하면 Simulation 값이 불변인지 검증

### 사용자 실기 피드백 루프

자동 테스트 후 같은 코드 리비전의 Unity Editor counterpart와 Android Development Build를 사용해 Host/Client 역할을 교대하는 두 경기를 실행한다. Editor의 `RuntimeLog_host.txt`/`RuntimeLog_client.txt`와 Android Logcat의 구조화 로그로 동일 UnitId를 추적한다. 서버 tick, phase, target, desired direction, SimulationFacing, sequence, scheduled/applied impact, presentation release, root writer를 한 행으로 기록한다. Blue/Red 양쪽 로그 시간으로 세 증상을 재현하고 수정 전후를 비교한다.

일반 로그를 무차별 추가하지 않고 고유 접두사 `[UAS-DIAG]`를 사용하며 작업 종료 전에 제거하거나 정식 계측 채널로 승격한다.

---

## 4. 순위화한 원인 가설

### H1 — Simulation Root에 서버와 Red 클라이언트 writer가 공존한다

가장 가능성이 높다. `NetworkUnit.LateUpdate`가 NetworkTransform 대상 루트를 직접 변환한다.

- 예측: 클라이언트 root write를 0으로 만들고 Red 변환을 Visual Root로 옮기면 서버 좌표·회전은 Blue/Red에서 동일하고 시각 방향만 180° 반전된다.
- 반증: root writer를 분리한 뒤에도 동일 UnitId의 SimulationFacing과 화면 정면이 지속적으로 불일치한다.

### H2 — 이동과 공격 전에 정렬 완료를 요구하는 서버 상태가 없다

`UnitView`는 이동과 회전을 동시에 수행하고 `NetworkCombatController`는 각도 확인 없이 공격을 예약한다.

- 예측: 10°/15° 이동 히스테리시스와 5°/8° 공격 히스테리시스를 서버 상태 머신에 넣으면 뒤·옆을 본 이동과 타겟 반대 방향 공격이 사라진다.
- 반증: 상태·각도 로그가 항상 기준 안인데도 화면 방향이 틀리면 H1 또는 Visual offset 문제다.

### H3 — 타겟·Animator·피해가 공통 회차 ID 없이 별도 채널로 전달된다

타겟 RPC, 애니메이션 NetworkVariable, HP RPC가 서로 결합되지 않는다.

- 예측: `AttackSequenceId + HitIndex`로 Snapshot과 ImpactResult를 결합하면 순서 역전에서도 다른 공격의 타겟·피해·표현이 섞이지 않는다.
- 반증: 정규 키가 동일한 결과만 재생하는데도 타이밍이 틀리면 서버 시간 변환 또는 AttackTimeline 데이터 문제다.

### H4 — 공격자별 FIFO가 로컬 marker 선행을 복구하지 못한다

코드가 빈 marker를 명시적으로 버린다.

- 예측: 결과 키 기반 양방향 버퍼로 바꾸면 marker/result 도착 순서와 무관하게 같은 Impact에서 한 번만 방출되고 정상 경로 timeout은 0이 된다.
- 반증: 순서 조합 테스트가 모두 통과하지만 실기에서 지연되면 CombatPresentationDelay 또는 클립 marker가 잘못된 것이다.

### H5 — AttackTimeline과 실제 Animator clip marker가 불일치한다

25종 중 marker 누락 4종, 주요 불일치와 복수 Attack clip 선택 문제가 있다.

- 예측: 명시적 AttackProfile과 실제 state clip을 연결하고 1 frame 허용 오차 검증을 통과시키면 서버 Impact와 동작 프레임의 고정 오프셋이 사라진다.
- 반증: 프로필·marker가 일치해도 지연량이 네트워크 순서에 따라 변하면 H3/H4가 원인이다.

---

## 5. 아키텍처 결론

현재 서버 권위 원칙은 맞지만 전투 오케스트레이션 경계가 없다. `UnitView`, `NetworkCombatController`, `HitPresentationQueue`를 각각 조금씩 고치는 방식은 상태·타겟·결과를 계속 별도 채널로 남기므로 재발 위험이 크다.

권장 방식은 기존 피해 의미와 A*를 보존하면서 Application에 `UnitActionSequencer`라는 깊은 모듈을 병행 구축하는 것이다. 이 모듈이 타겟, SimulationFacing, phase, 커밋, Impact, 취소를 캡슐화하고 Infrastructure는 Snapshot/Result 복제만, Presentation은 서버 시간 기준 재생만 담당한다.

전환 중에는 경기 시작 전에 `Legacy / Shadow / SequenceAuthoritative` 중 하나를 고정한다. 한 경기 안에서 유닛별 또는 타격별로 Legacy와 신규 writer를 섞지 않는다.

---

## 6. 확인된 테스트 seam과 제약

- Unity Test Framework 패키지는 존재하지만 현재 프로젝트에는 테스트 assembly와 자동화된 전투 테스트가 없다.
- 프로젝트가 predefined Assembly 중심이라 테스트 assembly 구성 방식을 먼저 검증해야 한다. 잘못된 asmdef 도입으로 전체 Assembly-CSharp 참조가 끊기지 않게 한다.
- 실행 가능한 Unity CLI가 현재 셸에서 발견되지 않아 자동 테스트 실행은 Editor 경로 확인 또는 사용자 Editor 실행이 필요하다.
- 최초 수직 슬라이스는 씬·NGO와 무관한 enum·값 식별자 및 계약 유틸리티로 잡아 빠른 테스트 seam을 만든다.
- 프리팹 Root 분리는 코드 변경보다 직렬화 위험이 크므로 멱등 Editor migration과 검증 리포트를 함께 제공해야 한다.
- 현행 “Assembly Definitions 없음” 제약 때문에 첫 슬라이스에서 테스트 asmdef를 추가하지 않았다. Editor self-validation runner는 enum·값 식별자, 각도 히스테리시스, 공격 회차 발급기, bounded 결과 버퍼와 A1 stateful reducer의 revision/time·commit/cancel/dead·multi-hit·result confirmation 계약을 검증한다. 독립 테스트 assembly 도입은 런타임 경계 분리 효과를 확인한 뒤 별도 판단한다.

---

## 7. 첫 안전 수직 슬라이스

첫 구현은 A0 **Shadow Melee Sequence**와 A1 **Pure Application stateful reducer**로 제한했다.

- marker와 설정이 모두 0.24초로 일치하는 SpearMan을 관측 대상으로 삼는다.
- 서버 Shadow 진단은 `AttackSequenceId`, TargetId, CommitServerTime, HitIndex 0, 예약 Impact 시각과 예약 시점 facing을 기록한다. 현재 `aimDirection`은 unavailable이다.
- 신규 경로는 피해·HP·RPC·VFX를 만들지 않고 예약(`schedule`)과 Legacy 피해 호출 직전 방출(`dispatch`)만 비교한다. `dispatch`는 `ApplyAttackDamage` 직전 관측점이며 실제 피해 성공이나 HP 결과를 뜻하지 않는다. Snapshot/ImpactResult와 실제 피해 결과 비교는 후속 단계다.
- SpearMan 필터는 shadow 관측에만 허용한다. 실제 권위 전환은 모든 adapter가 준비된 뒤 경기 전체 mode로만 수행한다.
- 불일치가 발생해도 Legacy가 유일 writer/emitter이므로 게임 결과는 변하지 않는다.
- A1 reducer는 순수 Application 값만 받아 상태를 전진시키며 런타임 pose, 결과 writer, RPC, VFX를 호출하지 않는다.
- A2에서 서버 권위 pose seam을 Shadow로 연결해 reducer 입력과 기존 Transform/도메인 방향을 비교한다.

---

## 8. 결론

세 현상은 독립 버그가 아니라 같은 원인군에서 발생한다. Simulation과 Visual이 같은 루트를 쓰고, 이동·타겟·공격·피해 표현이 공통 행동 회차 없이 각각 진행되는 것이 핵심이다.

전면 재작성이나 Legacy 즉시 삭제는 필요하지 않다. A0 계측과 A1 순수 계약·reducer 완료 → A2 서버 권위 pose seam shadow → Root 분리 → 공격 결과 shadow → 표현 전환 → 권위 전환 → 25종 이전 순으로 진행하면 서버 권위와 아키텍처를 유지하면서 안전하게 교정할 수 있다.

---

## 9. 2026-07-27 Tracer B0 구조 감사 결과

- Blue/Red 25종, 총 50개 프리팹을 Human 16 / Spirit 18 / Transcendence 16으로 식별했다.
- 현재 VisualRoot는 0개이며 migration 예상치는 생성 50, 재사용 0, root 직속 자식 이동 58이다.
- VFX/socket은 직렬화 할당 16 / null 34, root 직속 8 / 중첩 8이다.
- read-only 검증기와 dry-run은 prefab/scene/runtime을 수정하지 않았고 Git 에셋 diff 0, mutation API 호출 0, `assetsModified=0`을 유지했다.
- Unity dry-run을 연속 두 번 실행한 결과 모두 `prefabsLogged=50/50`, `errorsLogged=0`, `assetsModified=0`으로 PASS했다. 종합 manifest 해시는 두 실행 모두 `1d1043ff2ea440a5f25d24a9e006bca8739ecb514b4e362f8dd6c01504ae1dcd`였다.
- 초기 전체 `SerializedProperty` 해시는 `LoadPrefabContents`가 만드는 native bookkeeping까지 포함해 실행 간 불안정했다. NGO 의미 설정 allowlist만 해시하도록 교정했고, 16KB Console 절단에 대비해 runId·프리팹별 로그와 종합 digest를 분리했다.

이 결과는 B1 migration의 입력과 rollback 기준이 결정적이라는 뜻이다. 실제 VisualRoot 생성, 참조 재배선, 이동·회전 writer 변경은 아직 수행하지 않았으므로 세 사용자 증상의 해결 근거로 사용하지 않는다.

---

## 10. 2026-07-27 Tracer B1 적용 결과

- `VisualRootProjector`와 `PresentationPoseProvider`를 추가해 서버 판정용 Simulation pose와 VFX·피격 반응·플로팅 텍스트용 presentation pose를 분리했다.
- `NetworkUnit`의 Red 클라이언트 Simulation Root 위치·회전 보정을 제거하고, 멀티플레이 `UnitFactory`는 서버 canonical world position으로 유닛을 생성하도록 교정했다. A2 pose observer는 계속 Simulation Root를 읽으며 기존 피해·HP·RPC·VFX 권위는 변경하지 않았다.
- B0 manifest를 입력으로 50개 프리팹에 직접 자식 `VisualRoot`와 root `VisualRootProjector`를 적용했다. 네트워크 설정 기준선과 projector 참조, Animator Root Motion 비활성화, 저장 후 재로드 구조를 검사하는 Editor migration을 사용했다.
- migration 저널의 완료가 확인됐고, 사용자가 Apply를 다시 실행했을 때 `[UAS-ROOT][apply][NO-OP] 50개 프리팹이 이미 migrated state`가 기록됐다. 두 번째 실행은 `SaveAsPrefabAsset`과 `SaveAssets`를 호출하지 않아 멱등 적용을 확인했다.
- 신규·교체 프리팹은 이 50개 일괄 migration을 반복하지 않는다. 검증된 migrated 템플릿에서 처음부터 Root 계약을 만족시키고, roster·감사표·검증기의 고정 예상 수와 VFX 기준선을 함께 갱신한 뒤 전체 검증을 통과시킨다.

현재 근거로 확정할 수 있는 범위는 **50개 asset migration + journal completed + 재실행 NO-OP + rollback PASS**다. Android 1대와 Unity Editor counterpart의 역할교대 Host/Client·Blue/Red runtime smoke는 아직 미완료이므로 B1 전체 완료나 이동·바라보기 문제 해결 완료로 판정하지 않는다.

---

## 11. 2026-07-29 잘못된 Collider 진단 가정 제거

- 2026-07-27 B1 문서와 검증에 근거 없이 Collider 배치 가정이 도입됐다. 프로젝트 감사 결과 Collider는 B1 Simulation Root/Visual Root 분리 범위가 아니며 서버 single-writer, 클라이언트 Simulation Root write 금지, 표현 계층 분리의 완료 조건도 아니다.
- 따라서 Collider 존재 여부나 배치를 검사하는 공통 계약·런타임 observer 조건·Editor validator 조건·self-validation 회귀를 코드와 B1 검증 범위에서 완전히 제거했다.
- B1 구조 권위는 NetworkObject/NetworkTransform 등 네트워크 컴포넌트의 Simulation Root 잔류, identity VisualRoot, root Animator/Renderer 0, projector 참조, Root Motion 비활성화, Animator별 relay, 서버 single-writer와 client Simulation Root write 금지다.
- 정적 runtime compile과 diff-check는 PASS다. B1 실기는 Android 1대와 Unity Editor counterpart가 Host/Client 역할을 교대하며 Editor 파일 로그와 Android Logcat을 짝지어 재검증한다.

이 교정은 잘못 추가된 진단 가정을 제거한 것이며 Collider에 관한 신규 authoring 계약을 만들지 않는다. 이 시점에는 B1 overall이 OPEN이었으며, 아래 양방향 실기 결과가 최신 판정을 대체한다.

---

## 12. 2026-07-29 B1 양방향 이동 pose 실기 완료

- Match A는 Unity Editor Host·Blue와 Android Client·Red 구성이다. stable endpoint 34/39를 exact match해 coverage `0.872`, pose mismatch 0, 최대 축 오차 `0.000767m`, 최대 회전 오차 `0.000362°`를 기록했다.
- Match B는 Android Host·Blue와 Unity Editor Client·Red 구성이다. 양쪽 완전 END가 PASS했고 endpoint 36/33, union 38, exact match 29(`0.763158`), temporal-only 2, host-only 5, client-only 2였다. exact match pose mismatch 0, 최대 축/거리 오차 `0.000491m`, 최대 회전 오차 `0.000159196°`, drop/error 0이다.
- non-overlap은 서로 다른 stable 관측 시점과 endpoint 집합을 별도로 분류한 결과이며 exact match pose mismatch로 계산하지 않는다. self-validation은 overlap/non-overlap, union coverage와 실제 match shape를 포함해 PASS했다.
- NGO 2.9.2 Vector3 LegacyLerp의 millimeter-scale 종료 잔차를 피하도록 `Interpolate=true`, `PositionLerpSmoothing=false`, server authority와 canonical world-space 위치 동기화 계약을 검증한다.

위 근거로 **B1의 이동 Simulation Root/Visual Root 분리와 NetworkTransform 보간 양방향 멀티 검증은 완료**다. 공격 방향, 공격 Impact·피해 적용 시점, result seam과 B2 이후 ActionSequence 전환은 이번 증거로 검증하지 않았으며 계속 미완료다.

---

## 13. 2026-08-03 Tracer B2 관측 결과

B2는 기존 이동 writer를 교체하지 않고 서버가 같은 입력을 pure `UnitMovementReducer`로 판정해 Legacy 이동 결과와 비교하는 Shadow 단계로 구현했다. reducer는 command/segment scope와 revision을 사용하며, 이동 방향 오차가 10° 이하일 때 이동에 진입하고 이동 중 15°를 초과하면 재정렬한다. 타겟 획득은 같은 scope 안에서 기존 이동 의도보다 우선한다.

실기 중 목표점에 정확히 도착해 desired 방향과 expected writer delta가 모두 0인 정상 프레임이 invalid로 분류되는 진단기 문제가 발견됐다. 이는 실제 이동 구현 오류가 아니라 관측 입력의 “이동 의도 없음” 표현 누락이었다. 두 값이 모두 epsilon 이하일 때만 `NoIntent`로 정규화하고, 먼 목표인데 expected delta만 0인 상태와 endpoint인데 material delta가 있는 상태는 계속 fail-closed로 유지했다.

Android Logcat은 긴 단일 manifest가 잘릴 수 있으므로 entry 경계를 보존한 채 UTF-8 byte 제한 아래로 청크화했다. 청크 순번, 문자 수, byte 수, 전체 SHA-256과 terminal 로그 예약을 self-validation 및 실기 로그로 검증했다. 이 보정은 진단 증거의 손실을 막기 위한 것이며 게임플레이 writer를 변경하지 않는다.

Android 1대와 Unity Editor counterpart의 Host/Client 역할을 경기 집합에서 교대해 6개 성공 그룹에서 25/25종의 Blue/Red를 모두 관측했다. 첫 4종은 observer v4, 나머지 21종은 endpoint adapter가 보강된 v5 증거이며 reducer schema는 모두 `b2-movement-reducer-v1`이다. 각 유닛을 Host와 Client 역할 양쪽에서 각각 시험한 것은 아니다. 인정 세션 합계는 accepted decision 545,190회, manifest entry 420개다. Android Host인 그룹 3·4는 대화 중 직접 수집·검산한 Logcat이 근거이며 저장된 Editor RuntimeLog만으로 25종 전체를 재구성할 수 없다.

최종 성공 세션은 invalid·duplicate·stale·illegal·scope·unknown mismatch, client reducer invocation, Simulation Root write attempt, exception, dropped log, manifest preflight failure와 terminal overflow가 모두 0이었다. 모든 manifest는 청크 ordinal·entry count·SHA-256이 일치했다.

B2에서 관측된 `legacyMovedWhileShadowAlign`은 신규 reducer 오류가 아니라, 방향 정렬 전에 이동할 수 있는 기존 writer와 규칙 v2 10° 정렬 계약 사이의 분류된 차이다. B3에서 신규 writer를 경기 단위로 단독 활성화해 제거해야 할 대상이며, B2 단계에서 Legacy 동작을 수정하거나 결과 권위를 혼합하지 않는다. 따라서 B2 Shadow/증거 게이트는 완료하지만 실제 이동 방향과 바라보기 방향의 교정 완료는 B3 writer 전환과 멀티 검증 전까지 선언하지 않는다. 공격 타겟·공격 방향과 시각 Impact·실제 피해 시점도 이번 범위 밖이다.

---

## 14. 2026-08-04 B3 정확성 증거와 연속 이동 품질 결함

B3는 공격 권위 모드와 분리된 경기 단위 `UnitMovementPipelineMode`를 도입하고, `ReducerAuthoritative` 경기에서 서버 `UnitMovementReducer`만 Simulation Root position·rotation과 이동 phase를 결정하도록 전환했다. Client는 reducer와 Simulation Root writer를 실행하지 않고 NetworkTransform과 서버가 복제한 phase/scope를 읽는다. A*, Chase, PendingRepath와 PostCombatResume의 위치·회전은 같은 writer seam을 통과하고, target-acquire 우선 프레임과 endpoint는 `NoIntent`로 닫힌다.

현재 B3 로그는 정확성 계약의 유효한 증거다. 여러 Host 세션의 `[UAS-MOVE-AUTH] END`에서 reducer rejection, invalid, gate failure, writer selection failure, Client root/reducer write, 공격 writer ownership conflict, dropped log, manifest preflight failure와 terminal overflow가 0이었다. Client 세션도 복제 표본 1,822건에서 invalid 0을 기록했다. 예를 들어 Host 세션 `4316ab13...`은 96,909 frame과 coverage 27항목, Host 세션 `9798f1fa...`는 21,375 frame과 AStarPath·Chase·PendingRepath·PostCombatResume coverage 23항목을 오류 0으로 종료했다. 이 증거는 B3 writer가 서버 권위·single-writer·10°/15°·scope·복제 계약을 지켰음을 보여 주며 폐기하지 않는다.

다만 실기 품질 검토에서 정상 A* 경로가 타일 경계마다 정지한 뒤 회전하고 다시 출발하는 stop-turn-go 현상이 확인됐다. 로그에도 A* 선분 전환에서 약 60°의 `AlignToMove`와 position delta 0이 반복된다. 원인은 NetworkTransform 보간이나 Visual Root 투영이 아니라, `MoveAlongPathV3`가 현재 타일 선분의 목표만 넘겨 다음 선분에서 DesiredMoveDirection을 불연속적으로 바꾸고, reducer가 규칙대로 15° 초과를 안전 정지시키는 서버 Simulation Root 입력 구조다.

따라서 B3 판정은 다음처럼 분리한다.

- **정확성 증거: 유지.** 서버 권위, 경기 단위 mode, exact-one writer, Client write/reducer 0, Align 중 위치 진행 0, target priority, endpoint, command/segment와 phase 복제는 현재 증거로 유효하다.
- **완료 판정: 재개방.** 이동 방향과 바라보기 방향의 논리적 일치는 확보했지만, 모든 정상 코너를 stop-turn-go로 표현하는 현재 trajectory는 프로젝트 완성도 기준을 충족하지 못한다.
- **후속 설계:** deep Authoritative Locomotion Module이 phase·SimulationFacing·position/rotation·진행량·복제 commit을 소유하고, Server Trajectory Planner가 현재/다음 선분과 이동 가능 통로를 이용해 연속 위치 접선과 DesiredMoveDirection을 만든다.
- **금지:** 10°/15° 안전 계약 완화, Client 예측 writer 추가, 방향만 미리 돌리고 위치는 이전 직선으로 이동, Visual Root smoothing으로 canonical 정지를 숨기는 방식, 경기 중 또는 유닛별 Legacy fallback.

**[당시 설계 확정/현재 구현 완료·멀티 검증 대기]** 경유 타일 중심 통과는 강제하지 않는다. 서버는 최초 Simulation Root point와 A* logical path를 기반으로 각 타일 영역을 연결한 logical path corridor를 만들고, trajectory를 해당 corridor 안에서 sweep한다. 위치 진행과 SimulationFacing은 같은 trajectory 접선을 사용하며 오차는 1° 이내여야 한다. `MoveSpeed`는 타일 중심 간 진행률이 아니라 실제 trajectory distance/second로 적용한다. 공통 최대 회전 속도는 270°/s를 사용하고, target acquire 판정은 이미 계산된 해당 틱의 candidate position을 기준으로 적용해 한 프레임 이전 pose와 섞이지 않게 한다. 획득이 확정된 틱에는 candidate pose까지만 commit하고 다음 틱의 추가 이동을 금지한다. 150°는 회전 속도가 아니라 반전 판정 임계값이며, 그 이상의 반전은 연속 선회가 아니라 정지 정렬 fallback으로 처리한다.

연속 이동 교정은 60° 코너, 180° 재경로, Chase, PendingRepath, PostCombatResume, held tick 진행량 0과 재개 jump 0, velocity↔SimulationFacing 1° 이내, 차단 통로 보존을 순수 검증한 뒤 Android/Editor 역할교대 멀티플레이와 25종 회귀를 다시 통과해야 한다. 이 재검증 전에는 B3와 이동 방향 증상 해결을 완료로 선언하지 않는다.

## 15. 2026-08-04 B3 연속 이동 구현 및 Editor 검증 결과

설계한 연속 이동을 기존 서버 권위와 레이어 경계를 유지한 채 구현했다. pure Application의 `UnitServerTrajectoryPlanner`가 서버 pose, 현재·다음 waypoint, 이동 거리, 회전 제한과 반전 임계값을 받아 `UnitTrajectoryStep`을 만든다. `UnitPathCheckpointTracker`는 곡선이 경유 타일 중심을 정확히 밟지 않아도 실제 trajectory가 통과한 checkpoint를 순서대로 한 번만 소비한다. 이를 통해 중간 중심 스냅 없이 최종 목적지에는 정확히 수렴한다.

`UnitView`의 공통 서버 writer는 A*, Chase, PendingRepath와 PostCombatResume를 같은 planner/commit 경계로 수렴시켰다. candidate segment는 logical path corridor의 Simulation Root point sweep을 통과해야 하며, 실패하면 안전한 candidate로 줄이거나 재경로한다. 실제 이동 거리만 진행량으로 소비하고 hold 중에는 진행량을 소비하지 않는다. commit된 `SimulationFacing`을 `UnitData.Facing`에 동기화하므로 권위 displacement와 Facing이 서로 다른 선분을 가리키지 않는다. 공통 회전 속도 270°/s와 150° 이상 반전 정지 정렬은 유지한다.

이동 중 적 또는 힐 타겟 획득은 이전 pose가 아니라 이번 틱의 candidate position으로 조회한다. 획득이 확정되면 해당 candidate pose까지만 commit하고 `NoIntent` lifecycle로 닫는다. `UnitCombatUseCase`에는 candidate world position을 받는 조회 경계를 추가했고, `UnitMovementAuthorityObserver`는 이 정상 candidate commit을 writer 위반으로 오인하지 않도록 gate를 교정했다. 공격 결과·피해·HP·RPC·VFX 권위는 변경하지 않았다.

구현 검증 중 두 종류의 결함을 self-validation이 실제로 검출했다. 첫째, C#의 단축 평가 경로 때문에 `UnitView`와 Editor validator의 지역 변수가 미할당일 수 있다는 CS0165가 발생해 명시적 기본값 초기화로 교정했다. 둘째, 최초 곡선 planner가 60° 코너 waypoint 중심을 약간 비켜 지나가면서 회전하는 통과 평면과 만나지 못해 waypoint를 영원히 소비하지 못했다. checkpoint 판정을 실제 trajectory의 순차 통과 기준으로 바꾼 뒤 정상 소비와 최종 수렴을 확인했다.

최종 Unity 메뉴 로그는 다음 계약을 포함해 PASS했다.

```text
[UAS-DIAG] self-validation PASS: A1 contracts/reducer, A2 pure pose sample, B2 movement reducer command/segment scope and 10/15 hysteresis, endpoint NoIntent normalization/lifecycle, target-acquire priority, duplicate/stale/invalid fail-closed, Android-safe lossless coverage manifest chunks and reserved-terminal preflight, B3 match-fixed movement pipeline mode/rollback, continuous 60-degree trajectory/150-degree reverse/held-progress invariants, shared production adapter, reducer-direction rotation source, attack-range NoIntent lifecycle, client replication identity/scope monotonic classification and retire/reuse, phase vocabulary/ordinal, 5/8 attack, cancel/dead, multi-hit/result confirmation, bounded dedupe/reorder, v2 range divergence.
```

사용자 Editor smoke에서는 정상 코너의 stop-turn-go가 교정된 것으로 잠정 확인됐다. 이는 Editor 단계의 조건부 PASS이며 B3 전체 완료는 아니다. 공통 이동 코드를 변경했으므로 기존 21/25종 B3 증거는 서버 권위·single-writer 정확성 기준선으로만 보존한다. 새 Android Development Build에서 Editor/Android Host·Client 역할교대, 25종 전체 회귀, 다음 경기 Legacy rollback을 모두 통과해야 최종 완료를 판정한다. 공격 방향과 시각 Impact·실제 피해 적용 시점은 별도 후속 게이트로 남는다.

---

## 16. 2026-08-18 Android Host 게임 정지 진단

새 B3 빌드의 Android Host 경기에서 별도의 Unity 예외나 Android 크래시 표시 없이 경기 전체가 멈추는 실파를 확인했다. 로그캣은 해당 기기가 `role=host`인 것을 보였고, Unity 게임 스레드 로그는 `08:48:49.737`의 `UnitRootPoseConsistencyObserver` 이동 표본을 끝으로 중단됐다. 직전에 Garage 배치와 Pistoleer·LittleKnight 이동이 있었지만, 건물 배치 자체를 단독 원인으로 확정할 증거는 아직 없다.

정지 직후 프로세스는 종료되지 않았고 CPU를 약 `127%`를 사용했다. 로그에는 `FATAL EXCEPTION`, `ANR`, `OutOfMemory`, 네트워크 종료, GPU device lost가 없었다. 따라서 대기·네트워크 중단이나 크래시보다 Unity 게임 스레드가 동기식 반복에 머물러 있었을 가능성이 높다. 기기에서 native stack을 읽는 `debuggerd` 호출은 root 권한이 필요해 실행하지 못했으므로, 아래 코드 경로 진단은 높은 확률의 원인이지만 native stack으로 확정된 결론은 아니다.

코드에서는 `ApplyMovementAuthorityFrame` 의 candidate와 보수적 fallback이 모두 logical corridor point sweep을 통과하지 못하면 `RepathRequired`를 반환한다. A* 및 PostCombatResume 호출자는 즉시 `RequestMove`를 실행해 새 path를 대입하고 `needRepath=true`로 외부 `while`에 돌아간다. 해당 `continue`에는 `yield`가 없고, 새 path가 이전과 동일한지, 이동 진전이 있었는지, 같은 frame에 재탐색했는지를 검사하지 않는다. A*가 같은 입력에 같은 path를 반환하고 corridor가 계속 거부하면 한 frame에서 끝나지 않는 `RepathRequired → RequestMove → continue` 순환이 가능하다.

이 결함은 `U-MOV-PATH`의 “안전하지 않은 구간을 적용하지 않고 재탐색”은 지켰지만, 재탐색이 수렴하지 않을 때 서버 틱을 반환해야 한다는 안전 경계가 누락된 것이다. 이동량을 줄이거나 Legacy writer로 자동 전환하는 방식으로는 해결하지 않는다. 해당 유닛만 현재 pose에 안전하게 멈추고, 다른 유닛과 서버 경기 루프는 반드시 진행해야 한다.

이 Android 실패로 B3 현재 판정을 **FAIL / OPEN**으로 낮춘다. Editor self-validation과 코너 이동 품질 PASS 증거는 보존하지만, 재탐색 유한 종료·서버 루프 진행성을 교정하고 Android Host에서 같은 종류의 시나리오를 재검증하기 전에는 빌드 안정성과 B3 완료를 주장하지 않는다.

---

## 17. 2026-08-18 최신 main 통합 사전 감사

원격 `main` 정보를 갱신해 B3 브랜치의 공통 기준 `95b6b17a...`에서 비교했다. 현재 B3 HEAD는 `b24fd6cf`(공통 기준 이후 13개 커밋), 최신 `origin/main`은 `26576d46`(공통 기준 이후 93개 커밋)이다. main은 248개 파일을 변경했고, 커밋된 B0~B2 이력과 main 사이에만 25개 파일의 병합 충돌이 예상된다. 복구된 B3 worktree와 main이 직접 겹치는 핵심 파일은 10개다.

main에는 연구소 공·방·속 강화, 상태효과, 스킬 A/B/C, MistShrine, 건물 UI 복구, 최신 문서 구조와 `Tools/check_docs.py`가 추가됐다. 특히 `UnitView` 이동은 연구 이동 배율과 둔화를 매 frame 재조회하고, 빙결 배율 0에서 위치·걷기 애니메이션을 정지한다. 반면 복구된 B3는 경로 시작 시 `worldSpeed`를 한 번 계산한다. B3를 예전 기준에서 더 구현하면 둔화·빙결·이동 연구가 Authoritative Locomotion에서 무시되는 회귀가 생긴다.

직접 통합 정책은 다음과 같다.

- `UnitView`: B3의 단일 서버 writer·trajectory·corridor를 유지하되 main의 매 frame 유효 이동 배율, 빙결 hold, Frozen 애니메이션 전이를 같은 commit seam에 통합한다. 빙결은 corridor 실패나 재탐색 사유가 아니며 진행량 0으로 hold한다.
- `UnitCombatUseCase`: B3의 candidate-position 적·힐 타겟 조회와 main의 `EffectiveAttack`, 방어력, 이동 배율, `CanAttack`, 스킬·자연회복을 모두 유지한다.
- `NetworkUnit`: B3의 movement phase/command/segment/semantic revision 복제와 main의 `Frozen` 애니메이션 상태를 함께 유지한다.
- `NetworkCombatController`: main의 상태효과·MistShrine·로그 세션 배선을 유지하고 B3 observer 세션 종료만 합친다.
- `Game.unity`: main 신규 UI·스킬·MistShrine 배선을 권위로 삼고 B3의 `_serverMovementPipelineMode: 1` 필드만 재적용한다. B3 예전 씬 전체를 선택하지 않는다.
- 문서: main의 최신 구조·버전·정합성 검사 규칙을 기준으로 B0~B3 이력과 규칙 v2.1을 소실 없이 합친다.

안전한 순서는 B3 worktree를 새 이름의 stash로 다시 보존 → `origin/main` merge 및 커밋된 B0~B2 충돌 해소 → B3 stash 재적용 및 10개 중첩 파일 의미 단위 통합 → 컴파일·자가검증 기준선 확보 → 9.8 재탐색 정지 교정이다. 13개 B0~B2 커밋을 93개 main 커밋 위로 순차 재작성하는 rebase보다 한 번의 merge가 이력 보존과 충돌 해소에 더 안전하다.

---

## 18. 2026-08-18 최신 main 통합 및 유한 재탐색 교정 결과

`origin/main`을 B3 브랜치에 merge하고 B3 worktree를 다시 적용했다. main의 연구 강화·상태효과·스킬·MistShrine·빙결/둔화와 최신 씬 배선을 보존하면서 B3의 경기 단위 `ReducerAuthoritative` single-writer, movement phase 복제와 연속 trajectory를 의미 단위로 합쳤다. 안전 stash `277a3ea54e91cd778829943491e7f4827f9649db`는 검증이 끝날 때까지 삭제하지 않는다.

Android Host 정지의 동기식 반복 경로에는 pure Application `UnitRepathProgressGuard`를 연결했다. path signature는 waypoint 개수와 순서화된 모든 `HexCoord(Q,R)`을 결정적으로 해시하며 런타임 객체 identity나 플랫폼별 `GetHashCode`를 쓰지 않는다. 같은 path, A→B→A 순환, invalid path와 같은 frame의 두 번째 수락은 해당 유닛에서 fail-closed한다. 서로 다른 path만 반환해도 실제 진전 없는 수락은 최대 8회로 제한한다. 수락한 path도 `yield return null` 뒤 다음 Unity frame부터 처리한다. authoritative position이 tolerance보다 이동하거나 logical checkpoint를 소비하면 no-progress 이력을 reset한다.

`UnitView`의 A* corridor, PostCombatResume corridor, 전투 복귀 path와 PendingRepath를 공통 guard로 수렴했다. fail-closed 종료에서는 현재 Simulation Root pose를 유지하고 이동 완료 callback과 힐러 idle watcher의 즉시 재진입을 억제한다. Client writer나 유닛별/경기 중 Legacy fallback은 추가하지 않았고 공격·피해·HP·RPC·VFX 권위도 변경하지 않았다. 현재 위치를 포함하지 않는 pending path 역시 같은-frame 코루틴 재시작 대신 해당 유닛만 안전 종료한다.

Unity 6000.3.5f2 배치 검증에서 처음에는 최신 main 빙결 통합의 전투 복귀 지역 변수 이름 충돌 2건(CS0136)을 검출해 의미 변화 없이 이름을 분리했다. 재실행 결과 Tundra script build가 성공했고 다음 self-validation이 PASS했다.

```text
[UAS-DIAG] self-validation PASS: ... B3 match-fixed movement pipeline mode/rollback, continuous 60-degree trajectory/150-degree reverse/held-progress invariants, finite repath frame/repeat-cycle/distinct-no-progress budgets and progress reset, ...
```

문서 정합성 검사 `Tools/check_docs.py`도 문제 0건으로 PASS했다. 이 결과는 유한 종료 구현과 Editor 게이트의 PASS이며 Android Host 실기 완료가 아니다. B3는 계속 **FAIL / OPEN**이고, 다음 게이트는 새 Android Development Build에서 이전 정지 종류를 포함한 경로 무효화 시험으로 경기 루프 정지·ANR·로그 폭주 0 및 다른 유닛/경기 시간 진행을 확인하는 것이다. 그 뒤에만 역할교대·25종 전체·다음 경기 Legacy rollback을 재개한다.

---

## 19. 2026-08-18 유한 재탐색 교정 실기 실패와 제자리 걷기 원인

새 Android Development Build의 Android Host 경기에서 프로세스 crash·ANR·OOM·예외는 없었고 서버 경기 루프도 이전처럼 한 frame에 영구 정지하지 않았다. 그러나 `REPATH-FAIL-CLOSED`가 10회 발생해 회복 가능한 유닛 이동이 대량 종료됐다. 네 유닛은 약 1.2초 안에 `source=pending-path`, `decision=RejectedMissingCurrentPosition`으로 정지했다. 이는 PendingPath가 계산된 뒤 실제 소비 지점까지 유닛이 전진할 수 있다는 예약 경로의 정상 시간차를 치명적 입력으로 오판한 것이다.

나머지 주요 실패는 corridor가 재탐색을 요청한 뒤 A*가 동일 logical path를 반환한 경우였다. 현 `UnitRepathProgressGuard` self-validation은 동일 path 1회를 즉시 `RejectedRepeatedPath`로 처리하는 것을 정답으로 고정했지만, 실기에서는 pathfinder가 주변 조건상 같은 최선 경로를 반환하는 정상적인 planner/pathfinder 불일치가 존재했다. “같은 frame의 동기 반복을 금지한다”와 “동일 결과 한 번이면 이동 목표를 폐기한다”가 잘못 결합돼 있었다.

유닛 15는 `ProductionTicker.TickSiege`가 `IsMoving == false`를 확인한 뒤 약 1초마다 같은 성 목표의 `MoveTo`를 새 command로 발행해 command revision 2→3→4로 진행했다. guard가 코루틴마다 생성되므로 새 command가 실패 이력을 초기화했고, 유닛 단위 fail-closed가 외부 호출자를 통한 반복까지 막지 못했다. 따라서 재탐색 생명주기는 코루틴/segment가 아니라 유닛의 이동 목표에 귀속해야 한다.

시각적 제자리 걷기도 같은 원인이다. `MoveAlongPathV3` 시작은 서버 애니메이션 값을 Walk로 설정하지만 실패 cleanup은 명시적인 Walk 종료/정지 표현을 게시하지 않는다. 코드에는 “유닛은 항상 이동 또는 공격하므로 Idle이 없다”는 전제로 `StateIdle`과 `OnUnitWalkStopped`가 제거돼 있지만, 동기화 규칙의 최소 행동 상태에는 이미 Idle이 존재한다. 실패 종료 후 서버 위치는 정지하고 클라이언트는 정상적으로 마지막 Walk 값을 유지하므로 사용자가 본 제자리 걷기가 발생한다. 이는 클라이언트 보간 문제가 아니라 서버 lifecycle과 표현 상태 계약의 누락이다.

같은 세션에서 Client movement replication 221건의 invalid·duplicate·revision/identity/scope 오류는 모두 0이었고 Client Simulation Root write도 0이었다. Host/Client Root Pose는 errors 0으로 PASS했다. Host movement observer만 `rejected=3`, `invalid=3`, 최종 `FAIL`이었다. 따라서 NetworkTransform, Visual Root, 세션 역할 또는 클라이언트 복제는 이번 원인에서 제외한다.

교정 결론은 다음과 같다.

- fail-closed는 unsafe candidate commit을 거부하는 틱 단위 원칙으로 유지하되 recoverable 경로 입력만으로 이동 목표를 종료하지 않는다.
- stale PendingPath는 폐기 후 다음 frame에 현재 권위 위치에서 다시 요청한다.
- 동일/순환 path는 목표 단위 무진전 관측으로 누적하고 실제 여러 frame 무진전 뒤에만 `Blocked`로 전환한다.
- 이동 목표 상태는 외부 ticker의 새 command 호출을 넘어 유지하며 환경 revision이나 목표 변경으로만 재개한다.
- `WaitingRepath/Blocked/AlignToMove`는 Walk 시간이 진행되지 않는 서버 권위 정지 표현을 가진다. 빙결 스킬 `Frozen`과는 별도다.
- 이동 종료/보류는 일반 movement frame 평가를 재사용하지 않고 명시적인 lifecycle commit으로 닫는다.

이 실기 결과로 9.8의 Editor self-validation PASS는 철회하지 않지만, 그 테스트의 동일 path 기대가 잘못됐음이 확인됐다. B3는 계속 **FAIL / OPEN**이며 9.10 교정의 RED→GREEN 회귀와 Android Host 재검증 전에는 25종 검증을 진행하지 않는다.
