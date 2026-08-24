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

자동 테스트 후 같은 코드 리비전의 Unity Editor counterpart와 Android Development Build를 사용해 Host/Client 역할을 교대하는 두 경기를 실행한다. Editor 근거는 `_Logs/_editor/{yyyy-MM-dd}/RuntimeLog.txt`, 실기기 근거는 경기 직전 `Hexiege > Logcat > 1. 버퍼 비우기` 후 재현하고 종료 직후 `2. 파일로 저장`한 `_Logs/{yyyy-MM-dd}/{HH_mm}_logcat/RuntimeLog_device*.txt`다. 파일명으로 역할을 추정하지 않고 양쪽의 `Role=Host/Client`, 동일하고 available한 `sharedSessionKey`, 동일 observer schema를 결합해 한 경기 쌍을 확정한다. 하나라도 누락·불일치하거나 여러 경기 중 어느 쌍인지 모호하면 해당 증거를 fail-closed로 제외한다. 서버 tick, phase, target, desired direction, SimulationFacing, sequence, scheduled/applied impact, presentation release, root writer를 같은 identity로 추적해 Blue/Red 수정 전후를 비교한다.

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

**[당시 설계 기록/2026-08-24 일부 대체]** 당시에는 경유 타일 중심 통과를 강제하지 않고 logical path corridor 안의 최근접 통과로 checkpoint를 소비하도록 설계했다. 이후 기본 이동을 “타일 중심에서 타일 중심으로 이동”한다고 정의한 같은 규칙 문서와 충돌하고, 전투 추격의 무경로 direct 후보가 건물을 반복해서 향하는 실기 실패가 확인됐다. A1·A2·B1과 B2 관측 구조는 유지하지만, 이 문단의 **중심 생략 checkpoint**는 `2026-08-24/17_46_b3-center-path-chase-rejoin-correction` 작업의 규칙과 Plan으로 대체한다. corridor, 위치 진행과 SimulationFacing의 1° 일치, 실제 trajectory 거리/초, 270°/s, candidate 기준 target acquire, 150° 반전 정렬 계약은 계속 유지한다.

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

---

## 20. 2026-08-19 Editor Host 교정 후 실기 실패

Editor Host·모바일 Client 구성으로 실행한 최신 경기에서 Host 세션 `76360a2f...`가 정상 수집됐다. Root Pose는 64유닛·7종·3,163표본에서 구조/투영/쓰기 오류 0, 위치·회전 최대 오차 0으로 PASS했다. 프로세스 예외, ANR, OOM, 로그 유실과 `REPATH-FAIL-CLOSED`도 없었다. 따라서 이번 실패는 NetworkTransform·Visual Root·프로세스 정지가 아니라 B3 행동 경계 안에 있다.

`[UAS-MOVE-AUTH] END`는 105,066 frame 중 `rejected=30`, `invalid=30`, `attackWriterOwnershipConflicts=1`로 FAIL했다. 단일 명시 오류는 `unitId=26` Assault에서 발생한 `double-writer-attempt`다. 이동 writer가 Simulation Root 회전을 소유한 상태에서 Legacy 공격 시작/추적 경로가 타겟 방향 회전을 요청했고, 현재 코드는 공격 회전을 억제해 실제 이중 쓰기는 막지만 공격 시작 pose가 이동 방향에 남을 수 있다. 이는 `NET-FACING-002`와 single-writer 계약의 “충돌을 감지하고 한쪽을 버림” 상태이며, 이동→전투 경계에서 소유권을 명시적으로 인계한 상태가 아니다.

30건의 invalid는 `UnitMovementAuthorityObserver`가 세부 로그 예산을 phase-transition과 공유해 원인 필드를 보존하지 못했다. 최종 집계만으로는 reducer 미호출, InvalidInput, InvalidTime 또는 invalid decision을 구분할 수 없다. 진단 결과를 구현 결함으로 단정하지 않고, terminal 예약과 별도의 bounded failure evidence를 추가해 유닛·scope·입력·상태를 손실 없이 남긴 뒤 같은 시나리오로 재판정해야 한다.

현재 관측기는 서버 `Held`와 최종 Animator 상태, 실제 위치 변화 0 구간의 Walk 진행 여부도 직접 연결하지 않는다. Root Pose PASS는 Simulation Root 좌표가 맞다는 뜻일 뿐 제자리 걷기 부재를 증명하지 않는다. `WaitingRepath/Blocked/AlignToMove`의 정지 표현과 실제 displacement를 상태 전이 기준으로 함께 기록해야 한다.

원인 가설의 현재 순위는 다음과 같다.

1. 이동→전투 전환이 이동 objective/회전 소유권을 먼저 정리하지 않아 공격 회전 요청이 억제된다.
2. lifecycle 경계의 일부 frame이 유효한 reducer scope를 만들기 전에 평가되어 30건의 invalid를 만든다.
3. 서버 `Held/Walk` 적용 순서 또는 후속 이벤트 덮어쓰기가 위치 변화 0 구간의 제자리 걷기를 남긴다.
4. 진단 상세 예산 공유가 2~3번을 구분할 증거를 소실한다.

교정은 서버 권위를 유지한다. 전투 진입은 이동 목표를 임의 완료하거나 클라이언트가 방향을 계산하는 방식이 아니라, 같은 서버 frame의 행동 경계에서 이동 회전 소유권을 해제하고 공격 writer가 타겟 방향을 이어받게 한다. invalid와 표현 상태는 결과에 영향을 주지 않는 관측 seam으로 보강하고, self-validation과 다음 Editor Host 실기에서 conflict/rejected/invalid 및 위치 0 Walk를 모두 0으로 확인한다.

2026-08-19 구현에서는 이동/행동 회전을 `UnitRootRotationOwnership` 하나로 묶어 전투 시작 시 `Movement → Action`, 전투 종료 후 살아 있는 이동 objective가 있으면 `Action → Movement`로 원자 인계하도록 했다. `AlignToMove`와 일반 `NoIntent`처럼 위치를 진행하지 않는 유효 판정은 같은 commit 경계에서 Walk 시간을 멈추며, 타겟 획득으로 즉시 Attack에 들어가는 `NoIntent`는 공격 애니메이션을 덮어쓰지 않는다.

`UnitMovementAuthorityObserver`는 `b3-movement-authority-v2`로 변경했다. rejected/invalid마다 유닛·네트워크 ID, 타입/팀, command·segment revision, reducer 호출/status, 판정/의도, 위치·목표·예상 이동량, 관측 서버 시각·이전 허용 시각과 방향 유효성을 별도 bounded 예산으로 기록한다. 정지 판정에서 Walk 시간이 진행되면 `stationary-walk-animation`으로 명시 실패하며, 실패 증거 예산 초과 자체도 최종 FAIL이다. 이 변경은 observer 전용이며 서버 판정 결과나 복제 값을 바꾸지 않는다.

Unity Roslyn 기준 런타임과 Editor assembly는 오류 없이 컴파일됐고 기존 NGO obsolete 경고만 남았다. `Tools/check_docs.py`도 문제 0건이다. 아직 Editor 메뉴 self-validation과 새 실기 증거가 없으므로 기존 B3 FAIL/OPEN 판정은 유지한다.

## 21. 2026-08-19 v2 실기 실패 원인 확정 및 2차 교정

동일 경기의 Android Client와 Editor Host 로그를 `sharedSessionKey=90cb445887c5ca3e5111eba7154dffa4953297af86fea812ddcde54fef9451d7`로 결합했다. 양쪽 Root Pose는 64유닛·6종, 오류 0으로 PASS했고 Client 복제 1,540표본도 invalid와 root/reducer write가 모두 0이었다. 반면 Host movement observer는 66,838 frame에서 `attackWriterOwnershipConflicts=737`, `stationaryWalkViolations=58`, `rejected=10`, `invalid=10`, 실패 상세 overflow 4로 FAIL했다.

코드와 실패 상세를 대조해 세 원인을 확정했다.

1. 서버 `StartCombatAnimation`과 `ChangeTarget` 이벤트가 실제 gameplay 행동 종료 뒤 늦게 도착해, Movement가 소유한 Simulation Root에 공격 타겟 추적을 다시 설치했다.
2. `_isMovementHeldAnim`이 이미 true이면 재적용을 생략했기 때문에 `StartWalkAnimation` 또는 `ResumeWalkAnimation`이 Animator speed를 1로 되돌린 뒤에도 위치 변화 0 Walk가 지속됐다.
3. `UnitView`가 planner/adapter/reducer 호출 전에 command/segment revision을 확정해 planner 실패가 reducer에 보이지 않은 scope를 소비했다. 이후 reducer의 최초 관측이 `0/0 → 1/18`, `0/0 → 1/47`처럼 건너뛰어 strict fail-closed가 `InvalidInput`을 반환했다.

2차 교정은 서버 권위와 기존 계층을 유지한다. 네트워크 서버는 gameplay 사거리 진입 경계가 이미 Action 소유권을 연 경우에만 Start/Change 타겟 이벤트를 수락하고, Action 종료도 로컬 gameplay 전투 루프만 확정한다. 따라서 지연된 Start/Change/Stop 이벤트는 서버 Simulation Root 소유권을 열거나 닫지 않는다. Held는 상태 bool이 같아도 실제 Animator의 Walk 진행을 다시 멈춘다. 이동 scope는 pure 정책으로 후보만 준비하고 planner와 reducer가 수락한 뒤 확정하므로 실패 후 재시도도 같은 revision을 사용한다. `b3-movement-authority-v3`는 무시한 지연 서버 이벤트 수를 정보성으로 남겨 새 빌드와 원인 차단을 식별한다.

런타임과 Editor assembly 컴파일은 오류 없이 PASS했다. 완료 조건은 Editor self-validation PASS 후 새 Android Development Build의 같은 경기에서 `rejected=0`, `invalid=0`, `attackWriterOwnershipConflicts=0`, `stationaryWalkViolations=0`, Client write/복제 오류 0, Root Pose 오류 0을 확인하는 것이다. 실기 전이므로 B3 판정은 **FAIL / OPEN**을 유지한다.

## 22. 2026-08-19 v3 실기 — 행동 경계 PASS, astar-corridor 반복 FAIL

동일 세션 `sharedSessionKey=9cbefd11120fac3b85fc49f49a01ecb31b8ee9ed51f8fd0cc737fdb4abc5f37b`에서 Android가 Host, Editor가 Client였다. Host movement 55,120 frame은 `rejected=0`, `invalid=0`, `attackWriterOwnershipConflicts=0`, `stationaryWalkViolations=0`으로 9.11 교정이 작동했다. 지연 서버 이벤트 246건은 소유권을 변경하지 않고 정보성으로 차단됐다. Client 복제 1,435표본은 invalid·revision·scope·identity·writer 오류 0이었고, 양쪽 Root Pose는 64유닛·7종에서 PASS했다.

하지만 Host에서 `[UAS-MOVE][REPATH-FAIL-CLOSED]` 76건이 21유닛에 발생했다. 69건은 `RejectedNoProgressBudget`, 7건은 `RejectedInvalidPath`, source는 전부 `astar-corridor`였다. 이는 planner의 full-distance smooth 후보와 full-distance direct fallback이 corridor를 벗어나면 더 짧은 안전 후보나 제자리 정렬을 만들지 않고 즉시 A*를 재요청하기 때문이다. A*가 같은 logical path를 반환하면 실제 pose 진전 없이 8회 예산을 소모하고 `Blocked`가 된다. scope `0/0` 16건은 첫 reducer commit 전에 corridor 재요청부터 발생한 결과이며 reducer scope regression은 아니다.

규칙상 corridor 거부는 곧바로 목표 차단을 뜻하지 않는다. `U-MOV-ALIGN` 4에 따라 이동량을 먼저 축소하고, 안전한 비영 이동이 없으면 5·6에 따라 현재 pose에서 제한된 Yaw 정렬을 해야 한다. 현재 pose 자체가 path corridor에 없거나 walkability가 실제로 막힌 경우만 `U-MOV-REPATH`로 넘긴다. 따라서 교정은 A*나 guard 한도를 느슨하게 하지 않고 planner와 corridor adapter 사이에 순수 안전 후보 resolver를 추가하는 방식으로 진행한다.
### 22.1 교정 구현

`UnitTrajectoryCorridorResolver`를 pure policy로 추가하고 서버 `UnitView` writer에 연결했다. full smooth 후보가 corridor를 벗어나면 이동량을 유한 횟수로 줄이고, 그래도 불가능하면 current waypoint만 향하는 direct 후보도 같은 순서로 줄인다. 모든 non-zero 후보가 불가능하지만 현재 pose가 안전하면 위치를 쓰지 않고 제한된 각도만 회전한다. 현재 pose 자체도 corridor·walkability 검사에 실패할 때만 `RepathRequired`를 반환한다.

새 실기 증거는 `observerSchema=b3-movement-authority-v4`로 구분한다. summary의 `corridorFullSmooth`, `corridorReducedSmooth`, `corridorFullDirect`, `corridorReducedDirect`, `corridorStationaryAlignment`, `corridorRepathRequired`로 실제 선택 분포를 확인하며, 기존 `REPATH-FAIL-CLOSED`와 함께 반복 차단이 제거됐는지 판정한다.

## 23. 2026-08-19 v4 실기 — A* start와 corridor walkability 계약 불일치

동일 세션 `d9924ba...50d12`에서 Android Host와 Editor Client 모두 `b3-movement-authority-v4`를 기록했다. Host authority는 rejected/invalid/conflict/stationary Walk가 모두 0이고 Client replicated 1,234 sample도 오류 0이었다. 양쪽 Root Pose는 64유닛·7종에서 PASS했고 치명 예외도 없었다.

그러나 Host는 `corridorFullSmooth=43,570`, `corridorReducedDirect=20`, `corridorStationaryAlignment=5`, `corridorRepathRequired=454`를 기록했다. terminal `REPATH-FAIL-CLOSED`는 66건·14유닛이며 `RejectedNoProgressBudget=55`, `RejectedInvalidPath=11`이었다. 52건이 첫 reducer scope `0/0`이고, 여러 생산 유닛이 동일 생산 좌표·동일 path signature에서 실제 위치 진행 없이 반복됐다.

코드 대조 결과 A*는 현재 타일을 walkability 검사 없이 출발 노드로 등록하고 인접 타일부터 walkability를 적용한다. 이는 현재 타일이 환경 변화로 막혀도 빠져나올 수 있게 하는 정상 의미다. 반면 `IsTrajectoryPointSweepInsidePath`는 sample 0을 포함한 모든 표본에 walkability를 요구해 `path[0]`에서 나가는 동일 경로를 거부했다. 교정은 첫 구간의 `path[0]` egress만 허용하고 나머지 corridor·중간 차단 fail-closed는 유지해야 한다.

## 24. 2026-08-19 v6 실기 — 실제 Root 도착 전 논리 위치 선행 커밋 확정

동일 세션 `1b14b299bf0b4cf087040b7cbb4c5cb9e350cef9cf3db2c133541f7693bbdea0`에서 Android Host와 Editor Client를 결합했다. Host의 이동 권위 60,248 frame은 rejected·invalid·gate·writer·ownership·stationary Walk가 모두 0이었고, Client 복제 1,943표본도 오류 0이었다. Host fallback은 full smooth 55,095, reduced direct 101, stationary alignment 46, repath-required 643, start-tile egress 222였다. 치명 예외·ANR·OOM은 없었다. Root Pose 관측기는 매칭 대기 중 시간 제한이 먼저 끝나 양쪽 모두 유닛 0으로 INCOMPLETE였으므로 이번 경기의 Root Pose PASS 근거로 사용하지 않는다.

`[UAS-MOVE][REPATH-FAIL-CLOSED]` 80건·18유닛의 v6 terminal evidence는 모두 `rootHex != UnitData.Position`이었다. 74건은 헥스 거리 1, 6건은 거리 2였고, 78건에서 `UnitData.Position == sourcePathStart`였다. 71건은 실제 Root 표본 타일도 walkable이었으므로 NetworkTransform, Collider, 타일 walkability가 1차 원인이 아니다.

코드 대조로 직접 원인을 확정했다. `MoveToInternal`의 `AlignPathStartToTransform`은 이동 중 새 경로의 첫 걸음이 역방향이면 `FindForwardClosestTile`로 앞쪽 타일을 고른 뒤, Root가 그 타일에 도착하기 전에 `ProcessStep`을 호출했다. 이 호출은 `UnitData.Position`, 위치 역인덱스와 이동 이벤트를 모두 앞으로 넘긴다. 이후 A*는 앞당겨진 논리 위치에서 시작하지만 Root는 뒤에 남아 corridor 밖으로 판정되고, 동일 경로 재요청이 no-progress 예산을 소모해 Blocked가 됐다.

올바른 계약은 다음과 같다.

1. 새 경로가 이동 중 Root보다 뒤에서 시작하더라도 `ProcessStep`으로 앞쪽 타일을 선점하지 않는다.
2. 현재 논리 타일에서 앞쪽 타일까지를 교정 경로의 첫 segment로 명시한다.
3. 앞쪽 타일 이후의 경로는 그 타일을 명시적 출발점으로 계산하되, 계산만으로 도메인이나 점유를 변경하지 않는다.
4. 서버 trajectory가 앞쪽 타일 waypoint에 실제 도착하고 checkpoint가 이를 소비한 같은 경계에서만 기존 `ProcessStep`이 위치·점유·이벤트를 커밋한다.
5. 앞쪽 타일이 현재 논리 타일의 인접 타일이 아니거나 안전한 후속 경로를 만들 수 없으면 원본 경로를 유지해 fail-closed하며, 비인접 논리 점프를 만들지 않는다.

이 교정은 서버 단일 writer와 기존 `ProcessStep` 도착 계약을 유지한다. 클라이언트 계산, NetworkTransform 권위 변경, Collider 예외 또는 corridor 완화는 사용하지 않는다. B3 판정은 새 pure 회귀, Unity self-validation과 Android Host/Editor Client 실기 재검증 전까지 **FAIL / OPEN**이다.

위 계약은 작업 문서에만 두지 않고 `GameSystemRules_Units.md`의 공통 이동 규칙에도 반영했다. 이후 신규 이동 경로·재경로·전투 복귀 구현도 “경로 계산은 상태 변경이 아니며 실제 Root 도착이 논리 커밋의 유일한 조건”을 따라야 한다.

## 25. 2026-08-20 v7 실기 — 경로 checkpoint와 실제 공간 타일의 의미 혼합 확정

동일 세션 `c7dfe0bd78f386d2510887041e8bfb2694b7f7c6f912b256834675b99ee2ccd3`에서 Android Host와 Editor Client가 모두 `observerSchema=b3-movement-authority-v7`을 기록했다. Host 67,736 frame은 rejected·invalid·gate·writer·ownership·stationary Walk가 모두 0이었고 Client 복제 1,788표본도 오류 0이었다. 양쪽 Root Pose 관측기는 각각 64유닛·2,765표본·5종, 오류 0으로 PASS했고 최대 위치 오차는 `0.000006m`였다. 치명 예외·ANR·OOM도 없었다.

그러나 Host에는 `[UAS-MOVE][REPATH-FAIL-CLOSED]` 42건이 남았다. 39건은 parse 가능한 `astar-corridor`, 3건은 `blocked-environment-resume`였고 모두 `RejectedNoProgressBudget`이었다. `astar-corridor` 39건은 전부 `rootHex != UnitData.Position`, `UnitData.Position == sourcePathStart`, `OutsideCorridor`였으며 Root와 도메인 위치의 헥스 거리는 1~5까지 누적됐다. 36/39에서 실제 Root 타일은 walkable이었다. 따라서 v7이 제거한 선행 `AlignPathStartToTransform.ProcessStep` 외에도 정상 이동 중 도메인 위치를 Root보다 앞당기는 경로가 남아 있다.

코드 대조로 남은 원인을 확정했다. `UnitServerTrajectoryPlanner`는 부드러운 코너에서 현재 waypoint 중심을 밟지 않아도 최근접 지점을 지난 순간 `ReachedCurrentWaypoint`를 true로 만들 수 있다. 이는 경로 접선 진행을 위한 올바른 checkpoint 의미다. 그러나 `MoveAlongPathV3`는 이 route checkpoint 소비 직후 `UnitMovementUseCase.ProcessStep`을 호출해 `UnitData.Position`, 위치 역인덱스와 이동 이벤트까지 다음 타일로 커밋한다. Chase도 공용 writer로 Root를 이동시키지만 공간 타일은 갱신하지 않고, 전투 복귀 시 별도 `ProcessStep`으로 뒤늦게 점프한다. 즉 경로 진행과 실제 공간 점유라는 두 의미가 하나의 waypoint 완료 신호에 결합돼 있다.

교정 계약은 다음과 같다.

1. route checkpoint 소비는 경로 진행만 갱신하며 `UnitData.Position`이나 위치 역인덱스를 변경하지 않는다.
2. 공용 서버 이동 writer가 commit한 Root의 전후 선분에서 실제로 교차한 헥스 타일을 순서대로 산출한다.
3. 현재 커밋 타일에서 인접한 실제 경계 전이만 `UnitData.Position`과 위치 역인덱스에 반영한다. 한 frame의 다중 경계는 순서대로 처리하고 비인접 점프는 fail-closed한다.
4. A*·Chase·PendingRepath·PostCombatResume가 같은 공간 커밋기를 사용한다. reducer가 소유한 facing은 공간 위치 커밋이 덮어쓰지 않는다.
5. A* congestion 진입 이벤트는 실제 경계 전이에만 기록하고, Chase의 공간 동기화가 A* 경로 혼잡도를 오염시키지 않게 구분한다.
6. 이미 불가능한 비인접 불일치가 존재하면 정상 이동 이벤트로 위장하지 않고 별도 복구/진단 경계로 처리한다. 새 경기의 정상 완료 조건은 복구 0건이다.

이 변경은 서버 권위, NetworkTransform 역할, 기존 레이어 방향을 유지한다. corridor 허용 범위와 no-progress 예산을 늘리는 방식은 사용하지 않는다. v7 실기 결과와 기존 규칙 문장을 위 계약으로 정정했으며, deterministic 회귀와 공용 writer 교정 전 B3는 **FAIL / OPEN**이다.

### 25.1 2026-08-20 v8 구현 및 코드 게이트

v7에서 확정한 원인에 따라 경로 진행과 실제 공간 점유를 코드에서도 분리했다. pure sampled spatial transition policy가 서버 Root의 이전·현재 위치 구간에서 교차한 타일을 순서대로 표본화하고 인접 전이만 만든다. Application 공간 커밋 API는 기존 facing을 보존한 채 preflight에서 전체 전이를 검증한 뒤 commit하며, 중간 실패로 일부 타일만 반영하지 않는다.

공용 서버 writer는 A*·Chase·PendingRepath·PostCombatResume의 실제 Root 이동 뒤 같은 공간 커밋 경계를 사용한다. ReducerAuthoritative 경로는 route checkpoint 소비 시 `ProcessStep`을 호출하지 않으며 Legacy 경로는 rollback을 위해 기존 동작을 보존한다. 공간 커밋은 staged reducer의 `Prepare → snapshot publish → commit` 순서 안에서 원자적으로 처리한다. Chase처럼 trajectory를 사용하지 않는 경로도 reducer 소유 facing을 별도로 동기화하며, per-frame corridor 검사에서 캡처 람다를 생성하지 않는다. observer schema는 새 빌드 식별과 공간 전이·preflight·commit·recovery 계수를 위해 `b3-movement-authority-v8`로 올렸다.

코드 게이트는 통과했다. Runtime Roslyn 컴파일은 PASS했고 기존 NGO obsolete 경고와 `EffectManager` 미사용 경고만 남았다. Editor Roslyn 컴파일도 Runtime 뒤 순차 실행으로 PASS했다. 독립 QA는 최초 리뷰에서 P1 3건을 제기했으나 교정 후 재검토에서 모두 CLOSED됐고 차단 이슈는 0이다. 다만 Unity 메뉴 `[UAS-DIAG]` self-validation과 새 Android Development Build 실기는 아직 실행하지 않았다. 따라서 v8은 **구현·컴파일·정적 QA 코드 게이트 PASS**일 뿐 B3 전체 판정은 계속 **FAIL / OPEN**이다.

## 26. 2026-08-23 v8 Android 실기 — 공간 preflight 국소 실패

Unity 메뉴 `[UAS-DIAG]` self-validation을 PASS한 뒤 새 Android Development Build로 Android Host·Editor Client 경기를 실행했다. 동일 세션 `sharedSessionKey=f5d...`에서 양쪽 observer schema가 `b3-movement-authority-v8`이고 Host/Client 역할과 세션 식별자가 일치했다. Host authority, Client replication, 양쪽 local Root 검증은 PASS했다. 수동 동등 cross-audit도 identities 52, overlap 50, matched 50, mismatch 0, match ratio `0.962`, 최대 축 오차 `0.000917m`, 최대 회전 오차 `0°`로 PASS했다. 따라서 이번 실패는 NetworkTransform·복제·Visual Root 또는 Host/Client pose 불일치가 아니다.

Host에서는 `spatialPreflightFailures=24`가 발생했다. `OutsideLogicalPath`가 11건이고, 공간 정책이 `Ready`를 반환했지만 Application preflight가 false인 표본이 13건이었다. 후자 13건은 모두 Blue SpearMan `unitId=58`의 `(8,7) → (8,6)` 전이에서 약 1초 동안 반복됐다. 현재 observer는 이 false를 `Rejected`로 집계하고 이동 cleanup을 실행하며, 외부 생산·공성 흐름이 같은 목표를 다시 명령해 반복을 만든다.

코드와 규칙을 대조한 직접 원인은 corridor 후보 허용 범위와 strict ordered spatial transition policy의 유효성 기준이 서로 다른 seam에 있다는 점이다. trajectory/corridor 단계에서 허용한 후보가 공간 preflight에서는 logical path 밖 또는 현재 타일 상태 불일치로 거부될 수 있는데, 이 recoverable 공간 불일치를 치명적 reducer 거부와 같은 `Rejected`로 처리한다. 이는 `GameSystemRules_Units.md`의 이동 경로·corridor와 실제 공간 커밋 분리, `U-MOV-PHASE`, `U-MOV-ALIGN` 4, `U-MOV-REPATH` 2·3·6·7·8·9 및 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-FACING-001` fail-closed 계약과 맞지 않는다. fail-closed는 unsafe commit을 막아야 하지만 살아 있는 이동 목표를 cleanup으로 종료해서는 안 된다.

마지막 국소 교정의 범위는 다음으로 고정한다.

1. 공간 preflight를 bool이 아닌 `reason + offending tile`을 가진 typed result로 바꿔 recoverable 경로 불일치와 fatal dependency/invalid를 구분한다.
2. corridor 후보와 ordered spatial viability가 같은 검증 seam과 같은 logical path/tile snapshot을 사용하게 한다.
3. `OutsideLogicalPath`, tile missing, non-walkable은 위치·방향을 commit하지 않고 `RepathRequired → WaitingRepath`로 연결한다. fatal dependency 또는 입력 불변식 파괴만 `Rejected`로 남긴다.
4. 같은 유닛·같은 목표는 같은 frame에 다시 실행하지 않고 다음 frame의 최신 pose·환경 revision에서 재평가한다.
5. observer는 spatial policy result, Application preflight, staged publish/commit, repath transition과 fatal reject를 서로 다른 계수로 기록한다.

종료 조건은 pure 회귀와 Unity self-validation PASS 후 새 Android Host·Editor Client 경기에서 `spatialPreflightFailures=0`, recoverable spatial reject/cleanup 반복 0, 같은 frame 재실행 0, fatal reject 0, Host authority·Client replication 오류 0과 양쪽 Root/cross-audit PASS를 동시에 확보하는 것이다. 이 조건 전까지 B3는 **FAIL / OPEN**이며 역할교대·25종 전체 회귀·Legacy rollback을 재개하지 않는다.

## 27. 2026-08-23 v9 typed 공간 후보/preflight 구현 및 코드 게이트

v8 실기의 recoverable 공간 불일치와 fatal 입력을 한 `Rejected` 경로로 합치던 문제를 typed 후보 판정으로 분리했다. `UnitSpatialPreflightResult`는 상태, offending tile, transition index를 보존하고, 후보 판정은 `Accepted / CandidateUnsafe / RouteInvalidated / Fatal`로 구분한다. corridor fallback의 probe와 최종 stage는 같은 logical path·타일 표본·Application preflight seam을 사용하며, 둘이 달라지면 observer가 실패로 기록하고 실행을 중단한다. `OutsideLogicalPath`·비인접 표본·missing tile·non-walkable은 unsafe pose를 쓰지 않은 채 `RepathRequired → WaitingRepath`로 연결하고, 유닛/의존성·chain 불변식 파괴만 fatal `Rejected`로 남겼다.

공간 진단의 staged 의미도 고정했다. 후보 stage나 Held 판정은 `planned`를 증가시키지 않고 실제 공간 commit 호출 직전에만 전이 수를 `planned`에 더한다. commit 성공 뒤에만 `committed`를 더하며, 세션 END에서 두 값이 다르면 실패다. recoverable signature/frame 이력은 전이 수가 양수인 성공 공간 commit 뒤에만 지워져, 제자리 Accepted나 후보 stage가 반복 증거를 숨기지 않는다. observer schema는 `b3-movement-authority-v9`다.

실제 코드에는 lifecycle retire 보강도 반영됐다. 서버의 phase baseline, 클라이언트 복제 baseline과 유닛별 recoverable signature/frame 이력을 despawn lifecycle에서 제거하고, identity가 맞지 않아도 stale object-id baseline을 폐기하되 충돌 계수로 fail-closed한다. Editor reflection 회귀는 정상 retire, identity mismatch retire, recoverable 이력 제거와 object-id 재사용을 함께 검사한다.

Runtime Roslyn과 Editor Roslyn은 기존 NGO obsolete·`EffectManager` 미사용 경고만 남기고 PASS했다. 독립 정적 QA 재검토는 P0~P2 지적 0건이다. Unity 메뉴 `[UAS-DIAG]` self-validation의 **실제 실행**과 새 Android v9 Host·Editor Client 실기는 아직 수행하지 않았다. 따라서 이는 구현·컴파일·정적 QA 코드 게이트일 뿐 B3는 계속 **FAIL / OPEN**이다.

새 Android 게이트는 `spatialFinalRecoverableRepaths=0`을 강제하지 않는다. 환경 변화로 recoverable repath가 발생할 수 있으므로, 발생 건이 다음 frame 이후 해소되고 동일 signature 반복·same-frame retry·recoverable 뒤 cleanup 없이 정상 commit 또는 안전한 목표 lifecycle로 수렴했는지를 증명해야 한다. 반대로 `spatialRepeatedRecoverableRepaths`, `spatialSameFrameRetries`, `spatialCleanupAfterRecoverable`, fatal preflight, stage divergence, commit 실패와 `planned != committed`는 END 실패 조건이다. 기존 영구 규칙은 이 계약을 이미 요구하므로 추가 변경하지 않았다.

## 28. 2026-08-23 pre-merge self-validation과 main 통합 상태

v9 코드가 main과 합쳐지기 전 Unity 메뉴 `[UAS-DIAG]` self-validation을 실제 실행해 PASS했다. 이 증거는 typed 후보/preflight, staged accounting, recoverable-history retention과 lifecycle retire/reuse의 **pre-merge 기준선**이다.

현재 main 통합은 진행 중이다. main의 `GameLog` 전환, 씬 무관 Editor 파일 로그, Android Logcat 저장 도구와 `NetworkCombatController`의 게임 종료·despawn 안전 가드/전투 틱 정지 이력은 보존해야 한다. 동시에 B3 v9의 서버 writer와 observer를 의미 단위로 통합해야 한다. 통합 결과에 대한 Runtime/Editor 컴파일, Unity 메뉴 self-validation 재실행과 독립 최종 QA는 아직 완료되지 않았다. 따라서 pre-merge PASS를 post-merge PASS로 재사용하지 않으며 B3는 **FAIL / OPEN**이다.

통합 뒤 검증 로그는 현행 `LogRules.md` 절차를 따른다. Editor는 `_Logs/_editor/{date}/RuntimeLog.txt`, Android는 match 직전 buffer clear 후 `_Logs/{date}/{HH_mm}_logcat/RuntimeLog_device*.txt`로 저장한다. 양쪽 `Role=Host/Client + sharedSessionKey + schema`가 모두 맞아야 한 경기로 인정하고, 누락·불일치·여러 후보가 있는 모호한 증거는 fail-closed한다.

## 29. 2026-08-23 최종 통합 정적 코드 게이트

`origin/main` 60개 commit과 B3 v9의 양측 의미 병합을 완료했다. unmerged 항목은 0이고 staged 내용은 최신 해결본과 일치한다. main의 `GameLog`, `LogSessionOwner`, 전투 종료·shutdown 안전 처리, `IsSpawned` 가드와 research 수정 이력을 보존했으며 B3 v9의 typed 공간 후보/preflight, staged accounting, lifecycle retire와 observer도 함께 보존했다.

통합 뒤 증거 감사기는 device 최신 anchor와 Editor 일별 로그를 짝짓고, run role·shared session key·production schema exact match·전체 BEGIN/END identity·EVIDENCE/FAIL 의미·device/editor source gate를 모두 확인한다. 어느 하나라도 누락되거나 후보가 모호하면 fail-closed한다. Android-safe terminal 출력은 manifest 26줄 + summary 5줄 + compact END 1줄, 총 32줄이며 UTF-8 최대 968 byte다. manifest 27개 또는 허용 길이를 넘는 항목은 fail-closed한다. adapter는 stateful 상태를 64개로 제한하고 65번째를 거부하는 경계를 가지며 release에서는 `Conditional`로 제거된다.

post-merge Runtime과 Editor Roslyn 컴파일은 PASS했고 독립 QA는 P0~P3 지적 0건이다. 최종 문서 반영 전 정합성 검사도 0건이었다. 다만 main 통합 전 `[UAS-DIAG]` PASS는 기준선일 뿐 post-merge 증거가 아니다. post-merge Unity `[UAS-DIAG]` self-validation과 RootCrossAudit self-validation의 실제 메뉴 실행, 새 Android v9 Host·Editor Client 실기는 아직 수행하지 않았다. 따라서 현재 결과는 **통합 정적·컴파일·QA 코드 게이트 PASS**이며 B3 전체 판정은 계속 **FAIL / OPEN**이다.

## 30. 2026-08-23 RootCrossAudit false-INCONCLUSIVE 원인과 진단기 교정

동일 `sharedSessionKey=a0e690...8d8`의 Editor Host·Android Client 실제 경기에서 v9 MOVE는 양쪽 full EVIDENCE였고 ROOT도 양쪽 PASS했다. 수동 pose 감사 결과는 union 55, overlap 49, match 49, mismatch 0, coverage `0.891`, 최대 축 오차 `0.000982m`, 최대 회전 오차 `0°`였다. 그러나 기존 RootCrossAudit는 ROOT `sessionStartBucket`이 Host 14, Client 17이라는 이유로 허용 차이 2를 넘었다며 INCONCLUSIVE를 반환했다.

이 결과는 게임 pose나 서버 권위 실패가 아니라 서로 다른 프로세스의 절대 bucket을 cross-peer 동등성으로 사용한 진단기 오류다. 같은 경기의 검증된 MOVE lifecycle `startedAt`은 Host `18.284286`, Client `19.017435`로 차이가 `0.733149초`였다. run 내부에서 BEGIN/END가 같은 ROOT bucket을 유지하는 불변식은 유효하지만, 서로 다른 기기의 bucket 숫자가 같아야 한다는 조건은 공유 시간축이 없어 유효하지 않다.

교정은 Root bucket의 cross-peer equality만 제거하고 각 run 내부 bucket 불변식은 유지한다. peer 결합은 검증된 MOVE lifecycle의 `startedAt` 차이 `<= 2.000초`를 사용하며 `2.000`은 허용, `2.001`은 거부한다. stable overlap, 전체 identity, source/role/shared key/schema, EVIDENCE/FAIL과 기존 fail-closed 조건은 유지한다. Play Mode에서는 분석을 차단하고, shared snapshot 읽기 실패나 `IOException`은 PASS가 아니라 INCONCLUSIVE다.

SelfValidation fixture는 실제형 조합, `2.000/2.001` 경계, run 내부 bucket mismatch, BEGIN/END 누락, `NaN`과 `Infinity`를 포함하도록 보강했다. Editor Roslyn 컴파일은 PASS했다. 사용자 Unity 메뉴 SelfValidate와 실제 로그 Analyze 재실행은 아직 수행하지 않았으므로 공식 실제 재감사 판정은 **OPEN**이다. 이 수정은 진단기에만 한정되며 게임 로직, 서버 권위, 로그 포맷과 영구 규칙은 변경하지 않는다.

## 31. 2026-08-24 회전 복제 증거 구현과 역할교대 실기 FAIL

RootCrossAudit와 `[UAS-DIAG]` self-validation은 Unity 메뉴에서 모두 PASS했다. 다음 실기에는 안정 이동 종료 경계마다 Host의 authoritative Simulation Root를 `server-checkpoint`, Client가 NetworkTransform으로 적용한 Root를 `client-final-root`로 기록하는 `root-rotation-replication-v1` 증거를 추가했다. 증거는 `unitId/networkObjectId`, server time/tick, movement phase와 command/segment/semantic revision, quaternion과 stable window를 연결하며 유닛당 1회·64유닛 bounded, UTF-8 1000 byte 미만, 누락·drop·preflight 실패는 fail-closed다. Transform 쓰기, RPC, 게임 판정, NetworkTransform 설정·패키지는 변경하지 않았다.

실기 세션 `65ee1764...d691`은 Android Host·Editor Client 역할교대 경기였다. ROOT terminal은 Android Host 64유닛·54 endpoint, Editor Client 64유닛·53 endpoint로 양쪽 PASS했고 rotation evidence도 각각 54/54, 53/53, drop/preflight 0으로 완전했다. 그러나 Host MOVE terminal이 Unit 30 Red Pistoleer의 `repath-fail-closed` 6건과 `spatial-recoverable-repeated=1` 때문에 FAIL하여 공식 CrossAudit는 pose 판정 전에 중단됐다. 동일 command/segment `1/2`에서 `astar-corridor` 2건 뒤 `blocked-environment-resume` 4건이 반복됐고, 현재 path hash는 동일했으며 유닛은 약 18.6초 뒤 사망했다. 저장·세션·네트워크 역할 검출 실패가 아니다.

공식 인증 판정과 분리한 read-only 증거 대조에서는 stable endpoint 54/53, overlap 48, 회전 허용치 `0.1°` 초과 7건이 확인됐다. 위치 차이는 모두 허용치 이내였고 회전 차이는 약 `0.11~0.15°`였다. 7건 모두 Host/Client의 phase와 command/segment/semantic revision이 같았다. 따라서 잔여 회전 오차는 movement snapshot/revision 지연이 아니라 같은 revision을 받은 뒤 NetworkTransform 회전 적용·최종 수렴 경계에서 남는다는 가설을 지지한다. 다만 MOVE terminal FAIL 세션이므로 공식 RootCrossAudit 판정으로 승격하지 않는다.

다음 교정 순서는 Unit 30의 반복 recoverable repath를 먼저 재현·최소화해 MOVE terminal gate를 복구하고, 그 뒤 같은-revision 회전 잔차의 NetworkTransform 적용/보간 종료 경계를 교정하는 것이다. 허용 오차 상향, 클라이언트 Root writer 추가, 서버 권위 완화는 사용하지 않는다. B3는 계속 **FAIL / OPEN**이다.

## 32. 2026-08-24 동적 건물 대체 경로 재탐색 FAIL과 분석 정정

새 경기 `sharedSessionKey=3303d48c3bce76bb180fba6008142e0577d268913a437ac123374d58ce774681`은 §31의 `65ee1764...97d2d691` 경기와 별개다. Android Host run은 `e371587a1b7c484c8c6724769ab0a961`, Editor Client movement run은 `7966aa6f567d40f2bccdd15ffca2917a`다. 두 세션이나 서로 다른 Unit 30 증거를 결합하지 않는다.

Host에서 `13:27:42.849` Building 20 AquaSpring Red가 `(Q=5,R=9)`에 배치된 직후 Unit 48은 command/segment `1/4`, source `astar-corridor`에서 `candidatePath=invalid → RejectedInvalidPath`가 됐다. 이후 건물 환경 변경 때마다 Unit 48·59·69가 `blocked-environment-resume`에서 같은 invalid 결과를 반복했고 Unit 96은 `post-combat-corridor`에서 실패했다. 전체 adapter failure는 28건이다. Unit 30은 별도로 `RouteInvalidated`, `NonWalkable`, offending tile `(5,13)`, Root `(4,14)`에서 `spatial-recoverable-repeated=1`을 남겼다.

이전의 “경로가 완전히 막혀 규칙상 정상 Blocked” 분석은 철회한다. 로그의 `candidatePath=invalid`는 `UnitMovementUseCase.RequestMoveFrom`이 반환한 `null/short path`만 증명하며, 실제 그래프의 완전 단절을 증명하지 않는다. 사용자의 실기 관찰에서는 완전 차단이 아니었으므로 이번 판정 기준은 “유효한 우회 경로가 있는데 서버 재탐색이 실패했다”이다. 규칙 4와 `U-MOV-REPATH`에 따르면 unsafe candidate를 commit하지 않는 것은 맞지만, 우회 경로가 있으면 현재 권위 위치에서 다음 frame 재탐색해 같은 목표로 이동을 계속해야 한다. 현재 구현은 이 계약을 만족하지 못했다.

코드 대조로 이번 Unit 48·59·69·96 실패의 직접 원인을 확정했다. 건물 배치는 타일을 `IsWalkable=false`로 바꾼 뒤 이벤트를 발행하고, `FlowFieldService`의 캐시 무효화 구독은 eager repath 구독보다 먼저 등록돼 있어 이벤트 순서 경쟁은 아니다. 문제는 새 FlowField가 walkable 타일만 포함하므로 건물로 막힌 **현재 서버 권위 start 타일**이 field에 없다는 점이다. `HexFlowField.GetPath(start)`는 start 미포함 시 `null`을 반환하고, `UnitView`는 이미 environment revision을 소비한 뒤 그대로 반환한다. 이후 다른 건물 이벤트가 생겨도 같은 blocked start에서 같은 `null`을 반복한다. 이는 규칙 49의 제한된 `path[0]` egress가 실제 경로 질의에 구현되지 않은 계약 공백이다.

고정 소형 HexGrid에서 출발점 `S`만 non-walkable로 바꾸고 인접 우회로 `S → N → … → D`를 남겨 blocked authoritative start의 실패를 재현했다. 다만 명시 좌표만 받는 `RequestMoveFrom(S,D)`은 그 좌표가 권위 위치인지 증명할 수 없으므로 계속 fail-closed해야 한다. GREEN 계약은 `UnitData.Position`을 직접 읽는 `RequestMove(unit,D)`에만 `[S, N, …, D]` egress를 허용하는 것이며 `N`은 walkable 인접 타일이어야 한다. 도달 가능한 인접점이 여러 개면 최단 경로 뒤 Q, R 좌표 순으로 결정하고, 도달 가능한 인접점이 없으면 실패를 유지한다. 중간 blocked tile 포함·비인접 점프·`path[0]` 재진입은 계속 금지한다.

Host terminal의 서버 공간 commit 1,110/1,110, commit failure 0, Client replication/Root write 오류 0은 서버 권위와 복제 경로가 유지됐다는 근거다. Unit 30의 `spatial-recoverable-repeated=1`은 valid candidate/history 계열이므로 blocked-start null과 같은 원인으로 합치지 않고 별도 OPEN으로 유지한다. observer 최대 64유닛 때문에 뒤 유닛의 Root endpoint가 누락될 수 있고 현재 경기에는 Unit 96까지 존재했다는 진단 커버리지 한계도 별도다. 공격 방향·Impact/피해 시점은 이번 경기로 판정하지 않는다. B3는 계속 **FAIL / OPEN**이다.

구현은 `UnitMovementUseCase` 내부의 private 공용 경로 계산 core로 분리했다. `RequestMove`만 `UnitData.Position`에 대한 blocked-start egress를 허용하고, `RequestMoveFrom`은 staged 출발점이므로 같은 상황에서 `null`을 유지한다. 권위 출발점의 일반 FlowField 경로가 없을 때만 walkable 인접 타일별 기존 FlowField 경로를 조회하며, 경로 길이 최단 → Q 오름차순 → R 오름차순으로 하나를 선택해 blocked start를 한 번만 앞에 붙인다. 따라서 첫 edge는 인접하고 이후 중간 타일은 기존 walkable 경로만 사용한다. 일반 walkable start의 기존 경로는 변경하지 않았고 서버 writer, 클라이언트 Root, NetworkTransform과 environment revision 소유권도 변경하지 않았다.

Editor self-validation에는 authoritative/staged 분리, 최단 경로, Q/R 동률 결정성, 인접 첫 edge, 중간 blocked tile 배제, 도달 가능한 인접점 없음, blocked start와 목적지 동일, 일반 경로 불변 회귀를 추가했다. production과 Editor 대상 Roslyn syntax 검사는 모두 PASS했고, 최초 독립 QA의 P1은 권위/staged API 분리로 닫혔다. R 동률 fixture 보강 뒤 독립 재검토 결과는 P0~P3 0건이다. 사용자가 Unity 메뉴에서 `[UAS-DIAG]`를 실제 실행해 line 48의 `self-validation PASS`를 확인했으며, 새 문구 `blocked authoritative-start deterministic FlowField egress`를 포함한 기존 전체 계약도 함께 PASS했다. Unity compile error 보고는 없었다. 새 Android 경기와 MOVE/ROOT/CrossAudit는 아직 실행하지 않았으므로 B3는 계속 **FAIL / OPEN**이다.
