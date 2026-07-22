# Research — 서버 권위 Unit ActionSequence 구현

## 이 작업이 무엇인지

멀티플레이에서 유닛이 가는 방향과 보는 방향이 다르고, 공격 대상과 공격 방향이 어긋나며, 화면의 타격 순간과 실제 피해 시점이 맞지 않는 세 문제를 하나의 서버 권위 행동 흐름으로 교정한다. 기존 전투 수치와 특수 유닛의 능력 의미는 보존하고, 위치·방향·타겟·타격 결과를 같은 행동 회차로 묶어 서로 다른 시점의 정보가 섞이지 않게 만드는 것이 목적이다.

이 문서의 본문은 구현 전 코드 조사 결과이며, 2026-07-22 Tracer A0/A1 구현·검증 결과는 아래 진행 기록에 추가한다.

## 2026-07-22 Tracer A0/A1 진행 결과

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

자동 테스트 후 Editor Host + 빌드 Client에서 동일 UnitId를 추적하는 구조화 로그를 사용한다. 서버 tick, phase, target, desired direction, SimulationFacing, sequence, scheduled/applied impact, presentation release, root writer를 한 행으로 기록한다. Blue/Red 양쪽 영상 또는 로그 시간으로 세 증상을 재현하고 수정 전후를 비교한다.

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
