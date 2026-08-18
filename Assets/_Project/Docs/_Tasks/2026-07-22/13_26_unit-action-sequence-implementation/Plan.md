# Plan — 서버 권위 Unit ActionSequence 단계적 구현

## 이 작업이 무엇인지

유닛의 이동·바라보기·타겟·공격·피해 표현을 하나의 서버 권위 행동 흐름으로 묶는다. 플레이어가 보는 방향과 실제 판정 방향이 같고, 화면의 Impact와 서버 피해 결과가 같은 공격 회차에 연결되도록 만드는 대규모 정밀 교정 작업이다.

기존 서버 피해와 특수 유닛 능력을 한 번에 갈아엎지 않는다. 신규 `UnitActionSequence`를 기존 경로 옆에서 먼저 계산하고 비교한 뒤, 경기 시작 시 선택한 모드로 전체 전환한다.

> **기존 로직 제거 안전 규칙:** `HitPresentationQueue`, Legacy PendingHit/공격 코루틴, Start/Change/Stop 전투 RPC, Red root 보정은 신규 경로의 shadow 비교와 사용자 멀티플레이 실기 승인을 통과하기 전까지 삭제하지 않는다. 전환 단계에서는 경기 단위 설정으로 한쪽만 활성화하고, 검증 전 코드는 rollback용으로 유지한다. 최종 삭제는 사용자 테스트 통과 및 문서/메모리 업데이트 승인 후 별도 수행한다.

**작성일:** 2026-07-22
**Research:** `Research.md`
**구현 기준:** 멀티플레이 Host/Client, Blue/Red
**Testcase:** 사용자 명시 요청 전에는 작성하지 않음

---

## 1. 절대 불변 조건

1. 서버만 이동, SimulationFacing, 타겟, 공격 커밋, Impact, 피해·회복·사망을 쓴다. (`U-AUTH-SERVER`, `NET-AUTH-001`)
2. 클라이언트는 Simulation Root를 직접 수정하지 않는다. Red 관점 변환은 Visual Root만 담당한다. (`U-ROOT-SEPARATION`, `NET-ROOT-001`)
3. 공격 결과는 `AttackerInstanceId + AttackSequenceId + HitIndex + VictimKind + VictimId + EffectKind + ResultOrdinal`로 식별한다. (`NET-ACTION-IDEMPOTENT`, `NET-PRESENT-003`)
4. 한 경기에는 위치·타겟·피해 writer가 하나, 피격 표현 emitter가 하나만 존재한다.
5. Animation Event는 표현 marker이며 피해를 만들지 않는다. (`U-ATK-TIMELINE`, `NET-PRESENT-001`)
6. 전환 모드는 경기 시작 전에 고정하고 경기 중 또는 유닛별로 혼합하지 않는다.
7. Application은 Unity.Netcode와 Infrastructure 구체 타입을 참조하지 않는다.

---

## 2. 전체 구현 순서

### Phase 0 — 진단 피드백 루프와 기준선

동작을 바꾸기 전에 세 증상을 숫자로 재현한다.

- `[UAS-DIAG]` 구조화 이벤트: UnitId, server tick/time, phase, target, desired direction, SimulationFacing, root writer, sequence, scheduled/dispatch(현재 Shadow), applied Impact·presentation release(후속)
- 로그는 상태 전이와 Impact 경계에서만 기록하고 매 프레임 무차별 출력하지 않는다.
- 순수 테스트/검증기에서 다음 Legacy 실패 시퀀스를 고정한다.
  - 큰 방향 전환 중 위치 선행
  - 공격 정렬 전 피해 예약
  - marker 선행 후 피해 결과 지연
  - Red root에 관점 변환 반복 적용
- Host/Client 로그의 동일 UnitId를 비교할 수 있는 correlation 형식을 사용한다.

근거 규칙: `NET-OBS-001`, `NET-OBS-002`, `NET-OBS-003`.

완료 게이트:

- 자동화 가능한 최소 재현이 실패 신호를 낸다.
- Runtime 실기가 필요한 항목은 실행 절차와 기대 로그가 문서화된다.
- 계측 자체가 피해·RPC·VFX를 발생시키지 않는다.

### Phase 1 — 순수 UnitActionSequence 계약과 테스트 seam (A1 완료)

NGO·Animator·씬과 분리된 Application 모듈을 만든다.

예상 신규 타입:

- `UnitActionPhase`: Idle, Navigate, AlignToMove, Move, AcquireTarget, Chase, AlignToAttack, Windup, Impact, Recovery, Dead
- `EntityRef`: EntityKind + 안정 ID
- `AttackSequenceId`: 공격자별 단조 증가 회차
- `UnitActionSnapshot`: 현재 phase, revision, target, 방향, 서버 시작 시각, 마지막 확인 타격
- `AttackImpactResult`: 정규 결과 키, 권위 시각·방향·ImpactPoint, 결과와 HP
- `UnitActionSequencer`: 상태 전이·커밋·취소·Impact 예약을 캡슐화
- `AttackResultKey`와 제한 버퍼: 중복·순서 역전·늦은 참가 처리

Application port:

- `IServerClock`
- `IEntityPoseProvider`
- `IUnitMotionPort`
- `IUnitActionReplicationSink`
- `ICombatResultSink`

`UnitActionContracts.cs`, `UnitActionSequencer.cs`, `UnitActionSequencing.cs`에 순수 값 계약과 stateful reducer를 구현했다. 기존 A*와 피해 writer는 바꾸지 않았으며, 런타임 pose/result seam과 피해·RPC·VFX 연결은 A1 범위가 아니다.

근거 규칙: `U-MOV-PHASE`, `U-COMBAT-PHASE`, `U-TARGET-COMMIT`, `U-ATK-TIMELINE`, `NET-ACTION-*`, `NET-TIME-*`.

완료 게이트:

- 10°/15° 이동 히스테리시스, 5°/8° 공격 히스테리시스 테스트
- 결정적 타겟 tie-break와 LoseRange 유지 테스트
- 커밋 전 무료 취소/커밋 후 무환불 테스트
- 다중 HitIndex, 중복, 역순, 취소, 늦은 참가 baseline 테스트
- Application 신규 코드에 NGO 참조 0건
- 규칙 0.63/런타임 0.3 근접 거리 충돌과 Impact 순간 방향·사거리 미검증을 self-validation 기준선에서 재현

### Phase 2 — Simulation Root / Visual Root 분리

기존 NetworkObject와 NetworkTransform이 있는 루트는 Simulation Root로 유지한다. 모델·Animator·VFX socket·팀 관점 변환은 Visual Root 자식으로 이동한다.

- `NetworkUnit.LateUpdate`의 Red root position/rotation 쓰기를 VisualRoot projector로 대체
- `UnitView.StartCombatAnimation` 등 클라이언트 root 회전 쓰기 제거
- 서버는 Simulation Root만, 클라이언트는 Visual Root만 수정하는 개발 assertion 추가
- Blue는 Visual Root 관점 오프셋 0°, Red는 180°
- 멱등 Editor migration으로 Blue/Red 50개 프리팹을 변환하고 참조 누락 리포트 생성

근거 규칙: `U-ROOT-SEPARATION`, `NET-ROOT-001`, `NET-FACING-001`, `NET-FACING-002`.

완료 게이트:

- Host/Client·Blue/Red의 동일 UnitId Simulation position/rotation 값 일치
- 클라이언트 Simulation Root write 0건
- Visual 방향만 관점에 맞게 반전
- Animator/VFX spawn point/NetworkUnit 직렬화 참조 누락 0건

### Phase 3 — 서버 권위 이동·방향 상태 전환

기존 A* 경로 계산은 재사용하고 진행 제어를 Sequencer로 옮긴다.

- 다음 목표로 DesiredMoveDirection 계산
- 오차 >10°이면 AlignToMove에서 속도 0
- 오차 ≤10°에서 Move 진입
- 이동 중 오차 >15°이면 즉시 정지·재정렬
- 정렬 중 적이 AcquireRange에 들어오면 AcquireTarget 우선
- 전투 종료 후 이동 재개도 같은 상태를 사용
- Legacy UnitView 이동 코루틴과 shadow 비교 후 경기 단위 전환

근거 규칙: `U-MOV-PATH`, `U-MOV-PHASE`, `U-MOV-ALIGN`, `U-TARGET-RANGE`.

완료 게이트:

- 위치 변화가 시작될 때 방향 오차 ≤10°
- 이동 중 >15° 프레임에서 위치 증가 0
- 재경로·추격·전투 종료 복귀에서 위치 스냅 없음
- Blue/Red가 같은 서버 phase와 SimulationFacing 수신

### Phase 4 — 공격 회차 shadow 계산과 복제

Legacy 피해는 그대로 유지하면서 신규 회차를 기록·복제만 한다.

- AlignToAttack 완료 전 Windup/쿨다운 시작 금지
- 공격 진입 5°, 유지 8°
- 커밋 시 AttackSequenceId와 TargetId 고정
- Melee/Hitscan Impact별 방향·사거리 재검증
- LockedPoint는 Launch의 AimDirection/ImpactPoint 고정
- Homing과 TravelingArea는 서버 추적/독립 진행
- Snapshot은 현재 상태 복제, ImpactResult는 신뢰성 있는 결과 메시지로 분리
- Legacy target/time/damage와 신규 shadow 결과 비교 로그

근거 규칙: `U-ATK-ALIGN`, `U-TARGET-COMMIT`, `U-IMPACT-*`, `NET-ACTION-*`, `NET-DELIVERY-*`.

완료 게이트:

- shadow가 피해·HP·RPC·VFX를 만들지 않음
- Legacy와 신규의 target/result 차이를 UnitId/sequence로 추적 가능
- 사망·Despawn·타겟 변경 중 future Impact 취소 규칙 일치

### Phase 5 — 결과 키 기반 Presentation 전환

`UnitActionPresenter`와 `ImpactPresentationBuffer`를 도입한다.

- 서버 시간에서 `CombatPresentationDelay` 뒤를 재생
- Snapshot으로 공격 모션을 시작/fast-forward
- 확인된 ImpactResult만 HP 텍스트·피격 VFX·타격 반응을 방출
- 결과가 Snapshot보다 먼저 와도 회차당 64개/2초 제한 버퍼에 저장
- 늦은 참가 baseline 이전 결과는 상태만 수렴하고 과거 VFX 미재생
- 이미 지난 결과는 최대 0.50초 age만 한 번 catch-up
- 이 모드에서는 `HitPresentationQueue` emitter를 비활성화하되 rollback 코드는 유지

근거 규칙: `NET-PRESENT-*`, `NET-TIME-001~005`, `NET-ACTION-IDEMPOTENT`.

완료 게이트:

- marker/result 도착 순서와 무관하게 같은 결과가 정확히 한 번 방출
- 다른 공격 회차로 결과 이월 0건
- 정상 경로 FIFO timeout 0건
- AoE 피해자별 중복 HP 텍스트/VFX 0건

### Phase 6 — SequenceAuthoritative 피해 전환

경기 시작 계약을 추가한다.

```text
CombatPipelineMode = Legacy | Shadow | SequenceAuthoritative
CombatSchemaRevision
AttackProfileHash
```

- 서버가 경기 시작 전 값을 확정하고 참가자 불일치 시 연결 거절 또는 경기 전체 Legacy fallback
- 신규 Sequencer만 공격 예약·타겟·방향·피해 결과를 쓰게 함
- Legacy PendingHit/DelayedAttackDamage writer는 해당 경기에서 비활성
- `ApplyDamageToVictim`과 `ApplyFixedDamageToVictim`을 amount 기반 단일 authority result writer로 통합
- 이벤트·사망·HP 동기화는 결과 적용기 한 곳에서 한 번만 발생
- rollback은 다음 경기 시작 시 모드 변경으로만 수행

근거 규칙: 동기화 문서 10장 전환 규칙, `NET-AUTH-001`, `NET-ACTION-IDEMPOTENT`.

완료 게이트:

- damage writer 1개, presentation emitter 1개
- 동일 결과 키당 HP 변경·사망 이벤트·VFX 각 최대 1회
- 경기 중 mode 변경 불가
- Legacy와 SequenceAuthoritative를 한 경기에서 혼합하지 않음

### Phase 7 — 전달 방식과 특수 유닛 이전

일반 single → multi-hit → Hitscan → Projectile LockedPoint/Homing → Area → TravelingArea → Periodic/Heal 순으로 이전한다.

- BattleAxe: 주 타겟 + Cone 결과를 같은 HitIndex 아래 ResultOrdinal로 구분
- TorrentSpirit: 서버 wave contact에 회차/접촉 인덱스 부여
- MushroomBomber: Launch에서 LockedPoint 고정, 착탄 Impact에 직접+DoT 부여
- InfernoSpirit: 직접 결과와 EffectInstanceId 기반 DoT 보존
- QuakeSpirit: 권위 ImpactPoint에서 주 타겟 20 + 주변 유닛·건물 10 보존
- BloomFairy: 성공 Impact 후 3초 쿨다운 예외 보존
- FlameSpirit/LionKnight: MultiImpact HitIndex 보존

근거 규칙: `U-COMBAT-DELIVERY`, `U-COMBAT-AXES`, `U-SPECIAL-*`, `NET-DELIVERY-*`.

완료 게이트:

- 특수 유닛 수치·대상·건물 포함 여부 회귀 없음
- source 사망 이후 Projectile/TravelingArea 지속 정책 준수
- DoT/HoT 틱 중복 0건

### Phase 8 — AttackProfile과 25종 완성도 이전

- UnitFactory의 이름에 Attack이 포함된 첫 clip 선택 제거
- 실제 Animator state clip을 AttackProfile이 명시적으로 참조
- marker 누락 4종 교정: QuakeSpirit, RhinoBreaker, MushroomBomber, BloomFairy
- BattleAxe·Inferno·Stream 등 marker/설정 불일치 해결
- 복수 Attack clip 4종의 선택 모호성 제거
- hit/tracer preset 25종 연결 상태를 유닛별로 검증
- AssetMatrix의 Complete 체크리스트를 유닛별로 통과

근거 규칙: `U-ATK-TIMELINE`, `NET-PRESENT-002`, AssetMatrix 완료 판정.

---

## 3. 예상 파일 구조

실제 이름은 구현 중 기존 namespace와 충돌 여부를 확인해 확정한다.

### 신규 구현 및 예상

| 계층 | 예상 경로 | 책임 |
|---|---|---|
| Application | `Scripts/Application/Combat/Sequencing/UnitActionSequencing.cs` | A0 값 ID·각도 히스테리시스·회차 발급·bounded 결과 버퍼 — 구현 완료 |
| Application | `Scripts/Application/Combat/Sequencing/UnitActionContracts.cs` | A1 현재 행동 스냅샷·타임라인·사거리·타겟·결과 권한 계약 — 구현 완료 |
| Application | `Scripts/Application/Combat/Sequencing/UnitActionSequencer.cs` | A1 phase·타겟·방향·commit·Impact stateful reducer — 구현 완료 |
| Application | `Scripts/Application/Interfaces/IUnitAction*.cs` | 시간·pose·motion·replication·result port |
| Infrastructure | `Scripts/Infrastructure/Network/NetworkUnitActionReplicator.cs` | NGO Snapshot/Result adapter |
| Infrastructure | `Scripts/Infrastructure/Config/CombatPipelineConfig.cs` | 경기 단위 mode/schema/profile 계약 |
| Presentation | `Scripts/Presentation/Unit/VisualRootProjector.cs` | Blue/Red Visual Root 관점 변환 |
| Presentation | `Scripts/Presentation/Unit/UnitActionPresenter.cs` | 서버 시간 행동 재생 |
| Presentation | `Scripts/Presentation/Effects/ImpactPresentationBuffer.cs` | 결과 역순·중복·catch-up 처리 |
| Editor | `Scripts/Editor/Combat/SetupUnitVisualRoots.cs` | 멱등 프리팹 migration |
| Editor | `Scripts/Editor/Combat/ValidateUnitCombatSetup.cs` | root/profile/marker 검증 리포트 |
| Editor | `Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | A0 계약 유틸리티와 A1 stateful reducer 검증 — Unity Editor 메뉴 PASS |

### 주요 수정 예상

| 파일 | 변경 이유 |
|---|---|
| `UnitView.cs` | 시뮬레이션 이동·root 회전·표현 책임 분리 |
| `NetworkUnit.cs` | Red client root 보정 제거, Snapshot 연결 |
| `NetworkCombatController.cs` | Legacy/Shadow/Sequence mode 조율 및 신규 adapter 호출 |
| `UnitCombatUseCase.cs` | 단일 authority result writer, 특수 의미 adapter |
| `GameEvents.cs` | Legacy 이벤트와 정규 결과 이벤트 경계 분리 |
| `NetworkHealthSync.cs` | 정규 결과 키·절대 HP 전달, 중복 방지 |
| `HitPresentationQueue.cs` | 신규 mode에서 emitter 비활성, 최종 승인 후 제거 |
| `UnitFactory.cs` | Visual Root·명시적 AttackProfile 적용 |
| `GameBootstrapper*.cs` | 유일한 조합 루트에서 신규 port/adapter 조립 |
| Unit Blue/Red prefabs | Simulation/Visual Root 계층과 직렬화 참조 전환 |

---

## 4. 구현 단위와 승인 게이트

작업량보다 완성도를 우선하되 한 번의 거대한 diff로 만들지 않는다.

1. **Tracer A0:** SpearMan schedule/dispatch Shadow — 완료
2. **Tracer A1:** Pure Application 계약과 stateful reducer — 완료
3. **Tracer A2:** Server-authoritative pose seam shadow — 완료
4. **Tracer B:** Phase 2~3 — Root 분리와 이동/방향 — 다음 단계
5. **Tracer C:** Phase 4~5 — 공격 shadow와 Presentation
6. **Tracer D:** Phase 6 — 서버 권위 writer 전환
7. **Tracer E:** Phase 7~8 — 공격 유형·25종 이전

각 tracer는 이전 게이트를 통과해야 다음으로 간다. 실패하면 새 코드를 지우지 않고 다음 경기 Legacy mode로 rollback하며 차이를 계측한다.

---

## 5. 위험과 대응

| 위험 | 대응 |
|---|---|
| `UnitView` 책임 분리 중 이동·힐·재경로 회귀 | 기존 코루틴 보존, motion port shadow 비교, tracer별 전환 |
| NetworkTransform 부모와 Visual Root 변환 충돌 | client root write assertion, Blue/Red 같은 Simulation 값 검사 |
| Snapshot과 ImpactResult 순서 역전 | 정규 키 제한 버퍼와 late baseline 테스트 |
| feature flag가 이중 writer를 허용 | 경기 단위 enum, 시작 후 immutable, assertion으로 즉시 실패 |
| 특수 공격의 직접 피해 경로 중복 | amount 기반 단일 result writer로 수렴 |
| Projectile 비행 시간 도입으로 밸런스 변화 | 현재 의미를 profile에 명시하고 기획 승인 없는 수치 변경 금지 |
| 프리팹 대량 직렬화 손상 | 멱등 Editor script, 검증 report, 수동 50개 수정 금지 |
| 테스트 assembly 부재 | 첫 tracer는 현행 asmdef 없음 제약을 유지하고 Editor self-validation runner 사용. 독립 테스트 assembly는 경계 분리 후 별도 판단 |
| Unity CLI 부재 | Editor 실행 명령/사용자 실기 절차를 산출물로 제공 |

---

## 6. 구현 후 검증 범위

Testcase 작성과 qa-tester 실행은 사용자가 명시적으로 요청할 때 별도 진행한다. 구현 자체의 필수 기술 검증은 다음과 같다.

- 정적: Infrastructure 밖 NGO 참조 0, client Simulation Root write 0, writer/emitter 각 1개
- 순수 상태: 정렬 히스테리시스·타겟 결정성·commit/cancel·multi-hit·dedupe·reorder·late join
- 멀티 실기: Host/Client, Blue/Red, 0/50/100/200ms 지연·지터·역순·중복·손실
- 회귀: 일반 근접/원거리, 특수 6종, multi-hit, 타겟 사망·변경, 공격자 사망, 재접속
- 부하: 다수 유닛 allocation·bandwidth·버퍼 상한·모바일 프레임

---

## 7. Tracer A0/A1 실제 구현 범위

사용자가 승인했고 실제로 구현한 범위는 **A0 SpearMan schedule/dispatch Shadow**와 **A1 Pure Application 계약·stateful reducer**다.

- enum·값 식별자와 계약 유틸리티: 각도 히스테리시스, 공격 회차 발급기, bounded 결과 버퍼
- 현행 asmdef 구조를 건드리지 않는 Editor self-validation runner
- marker/config가 0.24초로 일치하는 SpearMan만 Shadow 관측
- `AttackSequenceId`, TargetId, CommitServerTime, HitIndex 0, 예약 Impact 시각과 예약 시점 facing을 `schedule`에 기록
- Legacy `ApplyAttackDamage` 호출 직전에 `dispatch`를 기록해 schedule/dispatch의 회차·타겟·facing·시간을 비교
- 현재 `aimDirection`은 unavailable이며, `dispatch`는 실제 피해 성공이나 HP 결과를 뜻하지 않음
- `UnitActionContracts`와 `UnitActionSequencer` stateful reducer: revision/time fail-closed, 정렬·commit·Impact·Recovery, cancel/dead, multi-hit 결정·확인
- Snapshot/ImpactResult 런타임 복제, AimDirection·pose 입력 seam, 실제 피해 결과 비교는 후속 범위
- Legacy 동작 변경 없음. 피해·HP·RPC·VFX 신규 발행 없음

A1 결과를 기준선으로 삼아 A2 server-authoritative pose seam을 Shadow로 연결한다. 이후 Tracer B의 프리팹 Root migration을 진행한다. 이 순서가 실제 게임을 깨뜨리지 않고 대규모 교정을 이어가는 가장 안전한 진입점이다.

---

## 8. Tracer A2 — Server-authoritative pose seam shadow

### 8.1 목적과 고정 범위

A2는 **SpearMan 멀티플레이 서버 전용 관측 seam**이다. 서버가 이미 보유한 현재 root pose, 타겟 pose, optional 이동 의도와 공격 목표 방향을 순수 A1 reducer 입력으로 전달해 한 공격 주기의 schedule/dispatch Shadow를 비교한다.

- 대상은 `TargetLocked + MeleeContact + 1-hit` SpearMan만 허용한다.
- 서버에서만 observer를 실행한다. Host의 클라이언트 표현 경로와 원격 Client에서는 A2 reducer를 실행하지 않는다.
- 한 Legacy 공격 주기마다 독립된 A1 reducer 회차 하나만 만들고 schedule/dispatch를 관측한다.
- A2는 pose를 읽고 로그를 남기는 observer다. 위치·회전·타겟·피해·표현을 쓰지 않는다.
- A2는 세 사용자 증상을 해결하는 단계가 아니며 Simulation Root/Visual Root 분리 완료도 아니다.

### 8.2 좁은 Pure Application pose 계약

신규 pure 계약은 `IUnitActionPoseSource`와 `UnitActionPoseSample`이다.

- `IUnitActionPoseSource`: Unity `Transform`, `GameObject`, NGO 타입을 노출하지 않고 유효한 공격자/타겟 pose sample을 읽는 read-only port
- `UnitActionPoseSample`: 공격자 현재 XZ 위치·SimulationFacing, 타겟 XZ 위치, optional `DesiredMoveDirection`, 공격 목표를 향하는 `TargetAimDirection`, 타겟 참조와 sample 유효성을 담는 순수 값
- `DesiredMoveDirection`: 이동 명령이나 추격이 제공하는 optional 이동 의도다. 값이 없으면 공격 조준 방향으로 대체하지 않는다.
- `TargetAimDirection`: 공격자 현재 위치에서 현재 타겟 pose를 향하는 공격 목표 방향이다. 이동 의도와 별도로 계산·기록한다.
- `targetValid`와 `targetAlive`: Domain의 read-only 상태로 관측한다. 해당 상태를 관측하지 못한 sample에서는 값을 추정하지 않고 reducer의 Evaluate를 호출하지 않는다.
- 서버 시간과 A1 회차/revision은 observer가 별도 공급하며 sample이 권위 결과나 HP를 포함하지 않는다.

기존 광범위 예정안 `IEntityPoseProvider` 대신 이 좁은 인터페이스를 사용한다. A2가 필요한 것은 “현재 공격 한 회차의 공격자와 타겟 pose를 읽는 기능”뿐이다. 전체 엔티티 조회·검색·쓰기 기능을 노출하면 Application 계약이 불필요하게 커지고 기존 위치 제공자와 책임이 겹치며, 향후 adapter 교체와 rollback 범위도 넓어진다. 좁은 port는 서버 전용 read-only 관측임을 타입 경계로 제한하고 A2 제거 시 영향 범위를 최소화한다.

### 8.3 Legacy adapter와 observer 연결

- `UnitView`는 Legacy adapter로서 현재 root 위치·facing, 현재 타겟 pose, optional `DesiredMoveDirection`과 `TargetAimDirection`을 분리해 제공한다.
- adapter는 기존 이동·회전·Animator·타겟 선택 흐름을 변경하지 않고 순수 `UnitActionPoseSample`만 반환한다.
- `NetworkCombatController`는 `IUnitFactory`가 반환한 유닛 `GameObject`에서 `IUnitActionPoseSource` interface adapter를 찾는다. 구체 `UnitView` 타입으로 새 의존성을 만들지 않는다.
- 적격 Legacy 공격이 시작되면 A1 reducer로 one-cycle Shadow를 schedule하고, 기존 `ApplyAttackDamage` 직전에 같은 회차를 dispatch 관측한다.
- dispatch는 여전히 “Legacy 피해 호출 직전”을 의미하며 실제 적중, 피해 성공, ResultingHp 또는 `AttackImpactResult` 생성 완료를 의미하지 않는다.
- reducer commit이 yaw 5° 초과, 사거리 이탈 또는 다른 규칙으로 거부돼도 적격 Legacy 공격의 schedule/dispatch pose 상관관계는 반드시 유지한다. 이때 reducer 단계만 `not-run` 또는 거부 상태로 기록하며 Legacy 공격 흐름에는 영향을 주지 않는다.

### 8.4 `[UAS-POSE]` 핵심 로그 스키마

상태 전이 경계에서만 다음 상관 필드를 기록한다.

- `event`: `schedule`, `dispatch`, `skip`
- `serverTime`, `attackerId`, `attackerInstanceId`, `sequenceId`, `revision`
- `targetKind`, `targetId`, `delivery`, `hitIndex`
- `attackerPositionXZ`, `currentFacingXZ`, `targetPositionXZ`, `desiredMoveDirectionXZ`(optional), `targetAimDirectionXZ`
- `targetStateObserved`, `targetValid`, `targetAlive`, `phase`, `reducerStatus`
- `facingDeltaDegrees`, `aimDeltaDegrees`, `targetMoveDistanceWorld`
- `distanceWorld`, `maximumDistanceWorld`, `dispatchDeltaMs`
- `reason`: adapter 없음, invalid sample, 범위 밖 공격 유형, 타겟 변경, 시간·revision 거부 등 fail-closed 사유

로그에는 실제 피해량·HP 성공 판정을 기록하지 않는다. A2의 목적은 pose 입력과 reducer 전이 상관관계를 검증하는 것이다.

### 8.5 변경 금지 경계와 fail-closed

A2에서는 기존 **damage, RPC, VFX, HP, path, NetworkTransform, NetworkUnit, prefab, GameBootstrapper, UnitCombatUseCase**를 수정하지 않는다. 기존 writer/emitter와 Legacy 분기가 계속 경기 결과의 유일한 권위다.

다음 조건에서는 observer만 `skip`하고 즉시 기존 Legacy 분기로 돌아간다.

- 서버가 아님
- SpearMan이 아니거나 `TargetLocked + MeleeContact + 1-hit`이 아님
- `IUnitActionPoseSource` adapter를 찾지 못함
- 공격자/타겟 pose, desired direction 또는 서버 시간이 유효하지 않음
- schedule 이후 타겟·회차 scope가 달라짐
- observer 내부 예외 또는 로그 실패

어떤 skip·거부·예외도 Legacy 공격 예약, `ApplyAttackDamage`, RPC, HP, VFX, 이동·회전 또는 타겟 유지에 영향을 주지 않는다. observer의 반환값을 Legacy 분기 조건으로 사용하지 않는다.

적격 Legacy 공격에서 pose sample과 상관키가 유효하다면 reducer의 commit/Evaluate 결과는 `skip` 사유가 아니다. reducer가 거부하거나 target Domain 상태가 관측되지 않아 Evaluate를 실행할 수 없어도 observer는 같은 회차의 `schedule`과 `dispatch`를 남기고, `reducerStatus`와 `reason`에 `rejected` 또는 `not-run`을 기록한다.

### 8.6 롤백

- `NetworkCombatController`의 A2 observer 호출을 비활성화하거나 제거하면 즉시 A1 이전 Legacy 실행과 동일해진다.
- `UnitView`의 read-only adapter와 pure pose 계약은 writer를 갖지 않으므로 제거해도 직렬화·씬·프리팹 migration이 필요 없다.
- A2 실패 시 신규 seam을 다음 단계로 확장하지 않고 A0/A1 코드와 Legacy 권위를 유지한다.

### 8.7 완료 게이트

**정적 게이트**

- `IUnitActionPoseSource`와 `UnitActionPoseSample`은 Application의 pure C# 계약이며 UnityEngine/NGO 참조가 0이다.
- `NetworkCombatController`는 `IUnitFactory`의 `GameObject`에서 interface로 adapter를 찾고 구체 `UnitView` 의존성을 추가하지 않는다.
- 런타임 sequencer factory는 `CreateForValidation` 등 validation 전용 API를 호출하지 않고 정상 생성자·런타임 계약만 사용한다.
- A2 observer에서 damage/RPC/VFX/HP/path/Transform writer 호출이 0이다.
- 변경 금지 파일·프리팹·씬의 변경이 0이다.
- TargetLocked Melee 1-hit 이외 입력은 모두 fail-closed `skip`이다.
- `targetValid`/`targetAlive`는 Domain read-only state에서만 채우며 `targetStateObserved=false`이면 reducer Evaluate 호출이 0이다.

**Unity 게이트**

- C# 9/Application 컴파일 PASS, Editor 컴파일 PASS
- 기존 A1 self-validation 전체 PASS 및 A2 pose 계약 검증 PASS
- Game 씬 로드 시 missing script/직렬화 오류 0
- A2 비활성 상태에서 기존 SpearMan 동작과 로그 기준선이 유지됨

**멀티플레이 게이트**

- Host 서버에서만 `[UAS-POSE]`가 기록되고 원격 Client의 observer 실행은 0
- 적격 SpearMan 공격마다 reducer commit 수락·거부와 무관하게 one-cycle schedule/dispatch pose 상관관계가 정확히 하나이며 누락·중복·타겟 scope 불일치 0
- reducer가 yaw 5° 초과·사거리 이탈 등으로 거부하면 schedule/dispatch는 유지되고 reducer 단계만 `rejected` 또는 `not-run`으로 기록됨
- current facing, optional desired move direction, target aim direction, target pose와 reducer phase/revision을 같은 회차로 추적 가능
- `targetStateObserved=false`인 회차는 Evaluate 없이 `not-run`으로 기록되고 Legacy dispatch 상관관계는 유지됨
- adapter 없음·invalid sample·비적격 공격에서 명시적 `skip`이 기록되고 Legacy 피해·HP·RPC·VFX 결과는 기준선과 동일
- 사용자 멀티플레이 검증 전에는 A2 완료 또는 다음 Root 분리 단계로 승격하지 않음

**A2 판정 경계:** 위 게이트를 통과해도 “서버 권위 pose 관측 seam 검증 완료”일 뿐이다. 이동 방향/바라보기, 타겟/공격 방향, 시각 Impact/실제 피해 시점 문제 해결이나 Simulation Root/Visual Root 분리 완료로 판정하지 않는다. 게임 규칙 변경은 필요 없다.

---

## 9. Tracer B — Simulation Root / Visual Root와 이동·방향 전환

### 9.1 공통 경계와 규칙 근거

Tracer B는 A2에서 검증한 read-only pose seam을 기준으로 Root 소유권과 서버 이동·방향 writer를 교정한다. 공격 Impact·피해 writer 전환은 Tracer D의 범위로 남긴다.

- `U-ROOT-SEPARATION`, `NET-ROOT-001~003`: NetworkObject·NetworkTransform·사거리·판정은 Simulation Root에 남기고, 모델·Animator·VFX/SFX·진영별 화면 변환은 Visual Root로 분리한다.
- `U-MOV-PATH`, `U-MOV-PHASE`, `U-MOV-ALIGN`: 기존 A* 경로 의미를 보존하고 서버 `DesiredMoveDirection`과 `SimulationFacing`을 기준으로 10° 진입 / 15° 이탈 히스테리시스를 적용한다.
- `NET-FACING-001`, `NET-FACING-002`: 클라이언트는 위치 변화량이나 현재 타겟 위치로 권위 방향을 재추정하지 않는다. 이동·공격 표현은 서버 SimulationFacing과 이후 권위 Aim 결과를 재생한다.
- `GameSystemRules_UnitCombatSynchronization.md` 10장의 전환 규칙에 따라 경기마다 `CombatPipelineMode`를 고정하고 `single-writer / single-emitter`를 지킨다. 같은 경기에서 SpearMan만 신규 권위 writer로 전환하고 나머지 유닛을 Legacy 권위로 두는 혼합은 금지한다.
- B0→B1→B2→B3 순서를 고정한다. **B1의 50개 프리팹 원자적 migration과 참조 검증 전에는 B2 Shadow와 B3 writer 전환을 시작하지 않는다.**

### 9.2 B0 — 구조 검증기와 migration dry-run

런타임 동작과 프리팹을 변경하지 않는 read-only 사전 감사 단계다.

- Blue/Red 25종, 총 50개 프리팹에서 NetworkObject, NetworkTransform, `UnitView`, `NetworkUnit`, Animator, 모델 root, VFX/socket과 직렬화 참조를 수집한다.
- 목표 Simulation Root / Visual Root 계층, 이동 대상 컴포넌트, 유지할 네트워크 컴포넌트와 누락·중복·비정상 참조를 프리팹별 manifest로 산출한다.
- migration을 실행했을 때의 변경 예상과 rollback 대상을 보고하되 AssetDatabase 저장, 프리팹 수정, 씬 수정, 런타임 writer 연결을 하지 않는다.
- dry-run 결과가 50/50이고 자동 이동할 수 없는 참조가 모두 명시되기 전에는 B1을 승인하지 않는다.

**B0 완료 게이트**

- 대상 프리팹 50/50 식별, 중복 UnitType/진영 매핑 0
- NetworkObject·NetworkTransform을 Simulation Root에서 이동시키는 계획 0
- Animator·모델·VFX/socket의 Visual Root 이동 계획과 직렬화 재배선 목록 누락 0
- 실행 전후 예상 계층과 rollback manifest 생성
- prefab/scene/runtime mutation 0

### 9.3 B1 — 50개 프리팹 원자적 Root 분리와 Presentation seam

B0 manifest를 입력으로 사용하는 멱등 Editor migration 한 번으로 Blue/Red 50개 프리팹을 같은 규칙으로 전환한다. 일부 프리팹만 저장되면 전체 배치를 실패 처리하고 원자적으로 원상 복구한다.

- 기존 NetworkObject와 NetworkTransform이 있는 루트를 Simulation Root로 유지한다.
- 모델·Animator·VFX/socket·모델 고유 방향 오프셋을 Visual Root 자식으로 옮기고 기존 직렬화 참조를 재배선한다.
- `NetworkUnit`의 Red 관점 position/rotation 보정과 `UnitView`의 클라이언트 root 회전 쓰기를 Visual Root projector/presentation seam으로 대체할 수 있는 명시적 참조를 제공한다.
- 서버 Simulation Root writer와 클라이언트 Visual Root projector의 소유권 assertion을 추가하되, B1에서는 이동·공격 권위 writer 자체를 전환하지 않는다.
- migration 재실행은 변경 0이어야 하며, 실패 시 50개 전체가 migration 전 상태로 복구돼야 한다.

**B1 완료 게이트**

- 50/50 프리팹에 동일한 Simulation Root / Visual Root 계약 적용
- missing script, 깨진 직렬화 참조, Animator·VFX/socket 누락 0
- NetworkObject·NetworkTransform 위치와 네트워크 식별자 변경 0
- migration 2회차 diff 0, 부분 저장 0, rollback 검증 PASS
- Host/Client·Blue/Red smoke에서 Simulation Root 값은 동일하고 관점 반전은 Visual Root에만 존재

### 9.4 B2 — 서버 이동·방향 Shadow

B1을 통과한 50개 프리팹에서 기존 이동 writer는 그대로 유지하고, 신규 서버 이동 reducer가 같은 입력으로 계산한 phase·방향·이동 허용 결과만 비교 기록한다.

- 다음 A* 목표로 서버 `DesiredMoveDirection`을 계산한다.
- 방향 오차가 10°를 초과하면 Shadow는 `AlignToMove`, 10° 이하이면 `Move` 진입을 판정한다.
- 이동 중 오차가 15°를 초과하면 Shadow는 즉시 정지·재정렬을 판정한다.
- 재경로, 추격, 타겟 획득 우선, 전투 종료 후 이동 재개의 기존 입력과 신규 phase를 동일 UnitInstance/명령 회차로 상관시킨다.
- Shadow 결과는 위치·회전·Animator·NetworkTransform·path를 쓰지 않으며 Legacy writer의 분기 조건으로 사용하지 않는다.

**B2 완료 게이트**

- Host 서버에서 10° 진입 / 15° 이탈 판정과 Legacy 위치 변화의 상관 로그 수집
- 신규 판정이 `AlignToMove`인 표본에서 이동 허용 오판 0
- 재경로·추격·전투 종료 복귀의 회차 누락·중복·scope 불일치 0
- Client에서 서버 이동 reducer 실행 및 Simulation Root write 0
- 25종·Blue/Red 표본을 확보하고 유닛별 불일치 원인을 분류

### 9.5 B3 — 경기 단위 25종 이동·방향 writer 전환과 연속 이동 품질 교정

B2의 25종 Shadow 게이트를 통과한 뒤, 경기 시작 시 고정한 공격 파이프라인과 독립적인 `UnitMovementPipelineMode`에 따라 **25종 전체의 이동·SimulationFacing writer를 한 번에 전환**한다.

- 신규 모드에서는 서버 reducer만 이동 phase, SimulationFacing과 Simulation Root 위치를 쓴다. Legacy `UnitView` 이동/회전 writer는 전 유닛에서 비활성화한다.
- Legacy 모드에서는 기존 writer만 사용하며 신규 reducer는 진단 외 write를 하지 않는다.
- SpearMan 단독 authoritative canary 또는 유닛별 Legacy/v2 writer 혼합을 금지한다. 한 유닛에서 두 writer가 동시에 활성화되는 상태도 금지한다.
- 클라이언트는 Visual Root projector와 presentation seam만 실행하며 Simulation Root, 권위 phase 또는 SimulationFacing을 쓰지 않는다.
- rollback은 진행 중 경기의 유닛별 전환이 아니라 다음 경기 시작 시 전체 `UnitMovementPipelineMode`를 Legacy로 선택하는 방식으로만 수행한다.
- 공격 타겟·Impact·피해 writer와 Presentation emitter는 이 단계에서 기존 권위를 유지하며 Tracer C/D에서 별도 전환한다.

**B3 완료 게이트**

- 경기별 활성 이동·방향 writer가 Legacy 또는 신규 중 정확히 하나
- 신규 모드에서 25종 전체가 동일 권위 경로를 사용하고 SpearMan 포함 유닛별 혼합 0
- 위치 변화 시작 시 방향 오차 ≤10°, 이동 중 >15° 표본의 위치 증가 0
- Host/Client·Blue/Red의 동일 UnitId Simulation position/rotation/phase 수렴
- 재경로·추격·전투 종료 복귀에서 위치 스냅, 중복 이동, 정지/이동 진동 0
- 기존 피해·HP·RPC·VFX single-writer/single-emitter와 게임플레이 수치 변경 0
- 정상 A* 코너에서 불필요한 stop-turn-go 0. `AlignToMove`는 새 명령·급격한 재경로 등 연속 trajectory로 안전하게 처리할 수 없는 경우에만 사용
- 위치 진행 방향과 SimulationFacing은 같은 trajectory 접선을 사용하며 오차 ≤1°. 방향만 선행하거나 위치만 이전 선분으로 진행하는 불일치 0
- Align/hold tick의 경로 시간·거리 소비 0, 이동 재개 position jump 0
- trajectory의 차단 타일·건물 통과 0, logical path corridor 이탈 0, 기존 타일 도착 순서·점령·목적지 의미 변경 0

**2026-08-04 B3 판정 재개방:** 현재 ReducerAuthoritative writer는 서버 권위·single-writer·10°/15°·target priority·endpoint·phase/scope 복제 정확성 증거를 확보했다. 그러나 타일 선분 전환마다 반복되는 stop-turn-go 품질 결함으로 위 완료 게이트 전체를 통과하지 못했다. 기존 정확성 증거는 유지하되, 아래 9.6을 구현하고 재검증하기 전까지 B3와 이동 방향 증상 해결을 완료로 판정하지 않는다. 타겟/공격 방향과 시각 Impact/실제 피해 시점은 Tracer C/D 이후 게이트이며 B 단계 PASS로 완료 처리하지 않는다.

### 9.6 B3 연속 이동 심화 — Authoritative Locomotion + Server Trajectory Planner

1. pure Application 검증이 가능한 Authoritative Locomotion Module을 정의한다. Module은 trajectory 표본과 서버 pose, `deltaTime`, command/segment scope를 받아 phase, SimulationFacing, 다음 position/rotation, 진행량과 복제 snapshot commit을 함께 결정한다.
2. A*, Chase, PendingRepath와 PostCombatResume를 동일 Seam의 Adapter로 수렴시킨다. 각 Adapter는 경로·타겟 의도만 제공하고 Transform 회전·진행량·phase를 별도로 쓰지 않는다.
3. Server Trajectory Planner가 최초 Simulation Root point와 A* logical path로 타일 영역을 연결한 corridor를 만들고, 현재/다음 선분을 이용해 그 안에서 연속 trajectory를 sweep한다. 경유 타일 중심 통과는 강제하지 않는다.
4. 위치 진행 방향과 SimulationFacing은 같은 trajectory 접선을 사용하고 오차를 1° 이내로 유지한다. `MoveSpeed`는 실제 trajectory distance/second로 적용한다.
5. **[구현 완료/멀티 검증 대기]** 공통 최대 회전 속도는 270°/s다. target acquire는 해당 틱에 계산된 candidate position으로 판정하며, 획득이 확정된 틱에는 그 candidate position까지만 commit하고 `NoIntent`를 게시한 뒤 다음 틱의 추가 이동을 금지한다.
6. **[구현 완료/멀티 검증 대기]** 150°는 회전 속도가 아니라 반전 판정 임계값이다. 150° 이상의 반전은 연속 선회가 아니라 정지 정렬 fallback으로 처리한다.
7. 경기 단위 Legacy와 ReducerAuthoritative Adapter를 같은 Seam에 유지해 rollback을 보존한다. 경기 중·유닛별 mode 변경은 금지한다.
8. `NetworkTransform`, Visual Root 구조, 공격·피해·RPC·VFX Legacy 권위는 변경하지 않는다. Visual Root 표현 개선은 canonical 연속 이동 PASS 뒤의 별도 단계다.

**9.6 검증 순서**

- pure: 60° 코너, 180° 재경로, 이동 타겟 Chase, PendingRepath, PostCombatResume, endpoint와 target priority
- 불변: held tick progress 0, resume jump 0, velocity↔SimulationFacing ≤1°, corridor/차단 통로 이탈 0, 같은 입력/같은 결과
- 정적: A*/Chase/repath/resume의 직접 Simulation Root 이동·회전 writer 0, 공통 Seam만 사용
- 멀티: Editor Host·Android Client와 Android Host·Editor Client 역할교대, phase/pose 수렴, Client reducer/write 0
- 회귀: 25종 공통 경로, 공격 방향·피해·HP·RPC·VFX 값과 emitter 변경 0, 다음 경기 Legacy rollback

**9.7 구현 Seam과 파일 책임**

1. `Application/Combat/Sequencing/UnitMovementContracts.cs`의 현재 reducer 계약은 command/segment, revision/time, target priority와 fail-closed를 유지한다. 여기에 Unity 타입을 참조하지 않는 Authoritative Locomotion Module의 입력·출력 값과 순수 trajectory 진행 계약을 둔다.
2. 새 Server Trajectory Planner Module은 최초 Simulation Root point와 logical path를 받아 corridor, 거리 기반 진행량, 현재 접선과 candidate pose를 계산한다. 삭제할 경우 A*·Chase·PendingRepath·PostCombatResume에 corridor/look-ahead/곡률 계산이 다시 퍼질 정도의 깊이를 가져야 한다.
3. `Presentation/Unit/UnitView.cs`의 `ApplyMovementAuthorityFrame`은 유일한 런타임 Adapter/commit Seam으로 유지하되, 호출자가 `Lerp` 진행률·회전·candidate position을 각각 계산하는 얕은 Interface는 제거한다. A*·Chase·재경로·전투 복귀는 의도와 logical path만 전달한다.
4. `Infrastructure/Network/NetworkUnit.cs`는 서버가 commit한 pose와 phase/scope 복제만 담당한다. Client trajectory 계산이나 Simulation Root write를 추가하지 않는다.
5. `Bootstrap/GameBootstrapper.Setup.cs`와 `Infrastructure/Factories/UnitFactory.cs`는 새 구현을 조립·주입하는 위치다. Application은 Infrastructure나 NGO를 참조하지 않는다.
6. `Editor/Combat/RunUnitActionSelfValidation.cs`에 60° 연속 코너, 150° 임계 전후, 180° 반전, held progress 0, resume jump 0, candidate acquire 후 추가 이동 0, corridor 이탈 0, 동일 입력 결정성 회귀를 먼저 추가한다.
7. 변경 순서는 순수 계약·검증 실패 재현 → Planner/Locomotion 구현 → UnitView Adapter 연결 → direct Simulation Root writer 정적 감사 → Editor self-validation → Android/Editor 역할교대 → 25종 → 다음 경기 Legacy rollback이다. 경기 중 또는 유닛별 writer 혼합 canary는 금지한다.

**주요 위험:** corridor corner-cutting과 차단 타일 횡단, 타일 중심 강제 제거 뒤 `ProcessStep`·점유·진입 이벤트 의미 불일치, `_unitData.Facing`과 실제 SimulationFacing의 선행 갱신, 프레임률별 적분 차이와 모바일 GC, target acquire의 stale pose 또는 초과 이동, 공격 writer ownership 침범을 P0 회귀로 다룬다.

### 9.8 B3 Android Host 정지 교정 — 유한 재탐색과 유닛 단위 fail-closed

**근거 규칙:** `GameSystemRules_Units.md` `U-AUTH-SERVER`, `U-MOV-PATH`, `U-MOV-ALIGN` 7·8·10, `GameSystemRules_UnitCombatSynchronization.md` `NET-FACING-001` 및 전환 규칙 5·8·10을 유지한다. 즉 corridor가 거부한 candidate는 commit하지 않고 서버만 재탐색하되, 재탐색 실패를 Client writer·Visual Root 보정·경기 중 Legacy 전환으로 숨기지 않는다.

1. `UnitMovementContracts.cs`에 Unity·NGO에 의존하지 않는 유한 재탐색 guard 계약을 둔다. guard는 이동 command scope, 실제 위치 진전, 결정적 path signature 이력과 frame token을 입력으로 받아 `AcceptNextFrame`, `StopRepeatedPath`, `StopCycle`, `RejectInvalid`와 같은 결과를 반환한다.
2. path signature는 프로세스별 `GetHashCode`나 순회 순서에 의존하지 않고, waypoint 개수와 순서화된 `HexCoord(Q,R)` 열로 결정적으로 비교한다. 진전 없이 같은 signature가 다시 나오거나 이전 signature 순환으로 돌아오면 재탐색을 종료한다. 이력은 명시적인 상한을 두어 정상 진전 없이 메모리가 증가하지 않게 한다.
3. 한 Unity frame에서 재탐색은 유닛별로 최대 한 번만 수락한다. 새 path를 수락해도 같은 frame에 바로 planner를 재실행하지 않고 `yield return null`로 서버 루프에 제어를 반환한 뒤 다음 frame에서 검증한다.
4. 실제 authoritative trajectory가 position tolerance보다 큰 거리를 commit하거나 logical checkpoint를 소비하면 no-progress 이력을 초기화한다. 새 movement command도 guard를 초기화하지만, segment revision만 바뀌어 같은 command의 반복을 숨기지 않는다.
5. 반복·순환·invalid path로 guard가 종료를 결정하면 해당 유닛의 Simulation Root를 현재 pose에 유지하고 이동 intent를 정상 종료한다. 다른 유닛, NetworkTickSystem, 공격·피해·HP·RPC·VFX writer는 계속 진행해야 한다. Legacy mode로의 rollback은 기존처럼 다음 경기에서만 가능하다.
6. `UnitView.cs`의 A*·PendingRepath·PostCombatResume `RepathRequired` 분기를 공통 guard adapter로 수렴한다. 각 호출자가 독립적으로 재시도 카운터나 fallback writer를 가지지 않게 한다.
7. 진단은 `LogRules.md` 형식으로 terminal 결과만 중복 없이 남긴다. 최소 필드는 unit ID, command/segment revision, 실패 waypoint, 이전/신규 path signature, no-progress 횟수, 종료 사유다. 개발 중 임시 프레임 로그는 고유 prefix를 사용하고 원인 확정 후 제거한다.

**지정 피드백 루프와 회귀 게이트**

- pure self-validation에서 동일 path 반복, A→B→A 순환, invalid/empty path, 새 path 다음 frame 수락, 실제 진전 후 이력 초기화를 검증한다.
- 시뮬레이션 한 frame당 재탐색 수락이 1회 이하이고, 모든 입력이 유한 번의 결정 후 반환되는지 검증한다.
- Unity self-validation은 기존 60° 연속 코너, 150° reverse, held progress, candidate acquire, client writer 금지 회귀와 함께 PASS해야 한다.
- Editor에서 이동 중 건물 배치·경로 무효화를 재현해 한 유닛의 안전 정지와 다른 유닛·경기 시간 진행을 확인한다.
- 새 Android Development Build의 Android Host 경기에서 같은 종류의 경로 무효화를 포함해 게임 루프 정지·ANR·진단 폭주 0을 확인한다.
- 정지 재현 회귀를 통과한 후에만 Editor Host/Android Client ↔ Android Host/Editor Client 역할교대, 25종 전체, 다음 경기 Legacy rollback을 재개한다.

**변경 파일 예정:**

- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs` — pure 재탐색 guard 계약·순환 판정
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` — 한 frame 1회 재탐색, 다음 frame 재개, 유닛 단위 fail-closed adapter
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` — 동일 path·순환·진전 reset·유한 종료 회귀

**제외 범위:** NetworkTransform/Visual Root 구조, 공격 타겟·공격 방향, Impact·피해 시점, 공격·HP·RPC·VFX writer, 유닛별 Legacy fallback은 변경하지 않는다.

### 9.9 최신 main 통합 게이트 — B3 정지 교정 전 필수

9.8 코드를 수정하기 전에 `origin/main` `26576d46` 기준을 B3 브랜치에 병합한다. main의 연구·상태효과·스킬·MistShrine·UI·문서 기능은 기존 완료 사항이며, B3 편의를 위해 삭제·되돌림·고정값 치환하지 않는다.

1. 현재 B3 worktree의 tracked·untracked·staged 변경을 새 설명적 stash로 보존하고 stash commit ID와 변경 목록을 확인한다. 기존 `2742d880...` stash는 삭제하지 않는다.
2. worktree가 clean인 상태에서 `origin/main`을 merge한다. 13개 B0~B2 커밋을 순차 재작성하는 rebase는 사용하지 않는다.
3. 커밋 이력 병합 충돌은 다음 권위 우선순위로 해소한다.
   - 최신 `AGENTS.md`, `CLAUDE.md`, `WORKFLOW.md`, 스킬·MistShrine·강화·UI 구조와 main `Game.unity`: main 우선
   - B0~B2 Simulation/Visual Root, ActionSequence reducer, movement shadow·observer, 서버 single-writer 규칙과 증거: B3 브랜치 이력 보존
   - 동일 파일에 두 의미가 공존하면 한쪽 파일 전체를 선택하지 않고 method·serialized field·문서 section 단위로 통합
4. merge 충돌을 모두 해소한 뒤 새 B3 stash를 재적용한다. stash를 `pop`하지 않고 `apply`해 완전 검증 전까지 복구 지점을 유지한다.
5. 직접 중첩 10개 파일은 다음처럼 통합한다.
   - `UnitView.cs`: B3 trajectory/single-writer + main 매 frame 이동 배율·빙결 hold/Frozen animation
   - `UnitCombatUseCase.cs`: B3 candidate-position query + main 강화·방어·상태·스킬·회복
   - `NetworkUnit.cs`: B3 movement semantic snapshot + main Frozen animation state
   - `NetworkCombatController.cs`: B3 observer lifecycle + main 상태효과·MistShrine·로그 세션
   - `Game.unity`: main 씬 전체 보존 + `_serverMovementPipelineMode: 1` 재적용
   - 규칙·현황·로드맵·TDD·이력: main 구조에 B0~B3 상태와 FAIL/OPEN 판정 병합
6. 통합 후 9.8 교정 전의 기준선으로 컴파일, 기존 `[UAS-DIAG]` self-validation, 신규 상태효과 정적 경로, 씬 serialized reference, 서버/client writer 소유권을 확인한다.
7. 기준선 PASS 후에만 9.8 재탐색 유한 종료를 구현한다. 통합 결함과 정지 교정 결함을 한 번의 실기 테스트로 혼합 판정하지 않는다.

**main 통합 중 금지:** B3 예전 `Game.unity` 전체 선택, main `UnitView` 전체 선택으로 B3 writer 삭제, B3 `UnitView` 전체 선택으로 둔화·빙결 회귀, 문서 충돌을 이력 삭제로 해결, 경기 중 Legacy fallback, stash `pop`으로 복구 지점 삭제.

---

## 10. 구현·검증 진행 기록

### 2026-07-22 — Tracer A0 Shadow Melee Sequence 통과

- [x] `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionSequencing.cs` 신규 — enum·값 식별자와 계약 유틸리티(각도 히스테리시스·회차 발급·bounded 결과 버퍼)
- [x] `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` 신규 — A0 초기 self-validation PASS
- [x] `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` 수정 — SpearMan Shadow 예약·방출 진단 연결
- [x] 기존 서버 권위 피해, HP 동기화, RPC, VFX를 유일 writer/emitter로 유지
- [x] 사용자 멀티플레이 Host 계측 — scheduled 204 / dispatch 204 / unique 204, missing 0, duplicate 0, target mismatch 0, schedule↔dispatch facing change 0
- [x] Windup 240ms 전건 일치
- [x] `dispatch - scheduled` — min 0.013ms / avg 9.105ms / p50 8.226ms / p95 19.862ms / max 29.386ms, 16.667ms 초과 27건, 33.333ms·50ms 초과 0건
- [ ] Client 상관 로그 검증 — 현 로그는 계측 범위상 header only
- [ ] 실제 `UnitActionSequencer` 권위 흐름 및 Snapshot/ImpactResult 복제
- [ ] Simulation Root / Visual Root 분리와 이동·공격 방향 교정
- [ ] 서버 Impact와 클라이언트 시각 표현의 결과 키 기반 결합
- [ ] 25종 및 지연·지터·손실·다수 유닛 검증

**A0 판정:** schedule/dispatch Shadow 진단 게이트는 계측 기반으로 통과했다. 이는 실제 피해 성공, 세 사용자 증상의 해결 완료 또는 v2 권위 전환 완료를 뜻하지 않는다.

### 2026-07-22 — Tracer A1 Pure Application 계약·stateful reducer 통과

- [x] `UnitActionContracts.cs` — Entity/Target/Delivery/Timeline/Range/Direction/Snapshot/Impact authorization 값 계약
- [x] `UnitActionSequencer.cs` — revision과 서버 시간을 사용하는 stateful reducer, commit/cancel/dead, multi-hit 결정·확인, 고갈 시 원자적 거부
- [x] C# 9/Application 컴파일 PASS
- [x] Editor 컴파일 PASS
- [x] Unity Editor 메뉴 `Hexiege/Combat/Run Unit Action Self Validation` PASS
- [x] reflection 기반 `Validate*` 10개 PASS
- [x] 최종 Standards/Spec 리뷰 P0~P3 지적 0건
- [ ] 런타임 pose/result seam 연결
- [ ] 피해·RPC·VFX 연결 — A1에서는 의도적으로 미연결

**A1 판정:** 순수 Application UnitAction 계약과 stateful reducer는 완료됐다. 런타임 행동 교정 완료가 아니며, 다음 단계는 **A2 server-authoritative pose seam shadow**다.

### 2026-07-27 — Tracer A2 Server-authoritative pose seam shadow 통과

- [x] `UnitActionPoseContracts.cs` — pure Application `IUnitActionPoseSource`와 `UnitActionPoseSample`
- [x] `UnitView.cs` — 기존 Transform·타겟·이동 의도를 읽기만 하는 Legacy pose adapter
- [x] `NetworkCombatController.cs` — 서버/SpearMan/TargetLocked Melee 1-hit 한정 one-cycle reducer Shadow와 `[UAS-POSE]` schedule/dispatch/skip
- [x] 런타임 sequencer 생성 경로를 validation 전용 factory와 분리
- [x] 공격자 사망 시 pending observer 조기 삭제 제거, reducer `MarkDead`를 통한 `DeadTerminal` 종료
- [x] bounded 관측 용량 초과 시 full canonical key의 `capacity-evicted` terminal skip 기록
- [x] 사용자 Unity Editor self-validation PASS
- [x] Host 최신 검증(2026-07-27 18:04:04): schedule 429 / dispatch 428. 마지막 1건은 로그 종료 당시 Impact 예정 전 in-flight이며 완료 회차 누락·중복 0
- [x] Host 공격자 사망 2건: `MarkDead=Accepted`, `DeadTerminal`; `capacity-evicted` 0, 예외 0
- [x] Client 최신 검증(2026-07-27 18:09:48): `[UAS-POSE]` 0 — 서버 전용 observer 경계 PASS
- [x] 기존 피해·HP·RPC·VFX·이동·회전·타겟 writer와 서버 권위 Legacy 분기 변경 없음
- [ ] Simulation Root / Visual Root 분리와 이동·바라보기 방향 교정
- [ ] 서버 Impact와 클라이언트 시각 표현의 결과 키 기반 결합
- [ ] 25종 및 지연·지터·손실·다수 유닛 검증

**A2 판정:** 서버 권위 pose 관측 seam의 정적·Editor·멀티플레이 런타임 게이트는 PASS다. 이는 세 사용자 증상의 해결 완료나 v2 권위 전환 완료를 뜻하지 않는다. 다음 단계는 **Tracer B Simulation Root / Visual Root 분리**다.

### 2026-07-27 — Tracer B0 Visual Root migration readiness 통과

- [x] Blue/Red 25종, 총 50개 유닛 프리팹 식별: Human 16 / Spirit 18 / Transcendence 16
- [x] 현재 구조 감사: VisualRoot 0, 신규 생성 계획 50, 재사용 계획 0, 직속 자식 이동 계획 58
- [x] VFX/socket 기준선: 직렬화 할당 16 / null 34, root 직속 8 / 중첩 8
- [x] NetworkObject·NetworkTransform·`UnitView`·`NetworkUnit`의 Simulation Root 잔류와 rollback manifest를 read-only로 검증
- [x] prefab/scene Git diff 0, mutation API 호출 0, `assetsModified=0`
- [x] Unity dry-run 연속 2회 `[END][PASS]`: 각각 `prefabsLogged=50/50`, `errorsLogged=0`
- [x] 두 실행의 `aggregateManifestSha256`가 `1d1043ff2ea440a5f25d24a9e006bca8739ecb514b4e362f8dd6c01504ae1dcd`로 동일
- [x] 전체 `SerializedProperty` 해시에 임시 `LoadPrefabContents` native bookkeeping이 섞여 실행 간 해시가 달라지는 진단기 문제를 발견하고 NGO 의미 설정 allowlist 해시로 교정
- [x] Unity Console 16KB 절단에 대비해 runId·프리팹별 manifest와 종합 digest를 분리하고 `[END]` 요약에서 50/50 기록 여부를 검증

**B0 판정:** 실제 에셋을 바꾸지 않는 migration readiness 게이트만 PASS다. 아직 50개 프리팹에 Visual Root를 생성하지 않았고 이동·바라보기, 타겟·공격 방향, 시각 Impact·실제 피해 시점의 세 증상도 해결 완료가 아니다. 다음 단계는 **B1 50개 프리팹 원자적 Root migration과 Presentation seam 적용**이다.

### 2026-07-27 — Tracer B1 asset migration과 Presentation seam 적용

- [x] `VisualRootProjector`와 `PresentationPoseProvider`를 추가해 Simulation pose와 presentation pose의 소비 경계를 분리
- [x] `NetworkUnit`의 Red 클라이언트 Simulation Root 보정 제거, 멀티플레이 `UnitFactory`의 canonical world position 생성
- [x] `HitPresentationQueue`, `FloatingHpTextSpawner`, 사망 VFX·피격 반응을 presentation pose로 전환
- [x] Blue/Red 25종, 총 50개 프리팹에 직접 자식 `VisualRoot`와 root `VisualRootProjector` 적용
- [x] migration journal completed
- [x] Apply 재실행 `[NO-OP]`: 50개 모두 migrated state, `SaveAsPrefabAsset`/`SaveAssets` 호출 0
- [x] 신규·교체 프리팹의 영구 승인 계약과 Asset Matrix 등록 체크리스트 문서화
- [x] graceful failure injection index 0/24/49 모두 PASS: 각 실행 `JournalRecovered=true`, `VerifiedFileCount=100`, `InitialAnalyzerPassed=true`
- [x] crash index 24 강제 종료 후 별도 프로세스 `RecoverCrash` PASS: 복구 전 prefab mismatch 25 / meta mismatch 0 / VisualRoot 25 / projector 25, 복구 후 prefab과 `.meta` 100/100 원상복구
- [x] primary Unity compile Tundra success, `[UAS-DIAG]` self-validation PASS
- [x] Android 1대와 Unity Editor counterpart의 역할교대 Host/Client·Blue/Red runtime smoke에서 Simulation Root 동일성과 Visual Root 전용 관점 변환 검증

**2026-07-29 rollback 판정:** graceful index 0/24/49와 crash index 24 별도 복구가 모두 PASS했고, 각 시험 뒤 primary 50개 prefab + 50개 `.meta`가 검증됐다. primary Unity compile과 `[UAS-DIAG]` 자체 검증도 PASS다.

**2026-07-29 잘못된 Collider 진단 가정 제거:**

- [x] 2026-07-27 B1에서 근거 없이 도입된 Collider 존재·배치 가정을 B1 계약 밖으로 재분류
- [x] 공통 Collider 계약, runtime observer 조건, Editor validator 조건과 self-validation 회귀를 코드·검증에서 완전 제거
- [x] B1 구조 권위를 network component placement, identity VisualRoot, root Animator/Renderer 0, projector/ref, Root Motion off, Animator별 relay, 서버 single-writer와 client Simulation Root write 금지로 복원
- [x] 정적 runtime compile 및 diff-check PASS

**B1 Android + Unity Editor counterpart 역할교대 실기 절차:**

1. 같은 코드 리비전이 열린 Unity Editor와 Android 1대에 설치한 Android Development Build를 준비한다. Unity Editor는 PC target build가 아니라 기존 테스트 harness/counterpart로 사용하며 Windows/Standalone build는 사용하지 않는다.
2. 같은 실기 세션에서 두 경기를 역할교대한다.
   - Match A: Editor Host·Blue + Android Client·Red. `RuntimeLog_host.txt`와 Android client Logcat을 짝지어 분석한다.
   - Match B: Android Host·Blue + Editor Client·Red. Android host Logcat과 `RuntimeLog_client.txt`를 짝지어 분석한다.
3. 각 경기의 `[UAS-ROOT-POSE]` 로그 쌍은 `sharedSessionKey`가 서로 같고 `unavailable`이 아니어야 한다. `role`과 `isFlipped`는 Match A/B의 Host/Client·Blue/Red 배치와 일치해야 한다.
4. 각 경기에서 180초 안에 Blue/Red 양 진영, 서로 다른 유닛 타입 2종 이상, 실제 이동한 유닛 2기 이상, 이동 후 3초 stable 표본을 확보한다.
5. 각 경기 양쪽 `[END]`가 coverage PASS와 errors 0인지 확인하고, 파일 로그와 Logcat 쌍을 외부 stable cross-audit한다.

이 절차는 `WORKFLOW.md`의 `MULTI-` Host+Client 표준과 `LogRules.md`의 Editor 파일 로그/실기기 Logcat 규칙을 함께 적용한다. 물리 Android 2대 검증은 release E2E·성능·호환성 QA에 권장할 수 있지만 B1 필수 게이트는 아니다.

**2026-07-29 B1 양방향 실기 판정:**

- Match A(Editor Host·Android Client)는 stable endpoint 34/39를 exact match해 coverage `0.872`, pose mismatch 0, 최대 축 오차 `0.000767m`, 최대 회전 오차 `0.000362°`로 PASS했다.
- Match B(Android Host·Editor Client)는 양쪽 완전 END PASS, endpoint 36/33, union 38, exact match 29(`0.763158`), temporal-only 2, host-only 5, client-only 2였다. exact match의 pose mismatch는 0, 최대 축/거리 오차 `0.000491m`, 최대 회전 오차 `0.000159196°`, drop/error는 0이다.
- `[UAS-DIAG]` self-validation은 overlap/non-overlap, union coverage와 실제 match shape를 포함해 PASS했다.

**B1 최종 판정:** 50개 asset migration·journal·멱등 재실행·rollback과 Android/Editor 역할교대 양방향 Simulation Root/Visual Root·NetworkTransform 보간 교차 검증이 모두 PASS했다. B1은 완료다. 이 완료는 이동 pose와 관점 표현 경계에만 해당한다. 공격 방향·공격 Impact·피해 적용 시점, result seam, B2 서버 이동·방향 Shadow와 이후 단계는 미완료이며 별도 게이트를 통과해야 한다.

### 2026-08-03 — Tracer B2 서버 이동·SimulationFacing Shadow 통과

- [x] `UnitMovementContracts.cs` — NGO·Animator·Transform·피해 writer에 의존하지 않는 이동 입력/상태/결정 계약과 `UnitMovementReducer`
- [x] command/segment scope, revision 단조 증가, duplicate/stale/invalid fail-closed
- [x] 이동 정렬 10° 진입 / 15° 이탈 히스테리시스와 target-acquire priority
- [x] 목표점 도착에서 desired XZ 거리와 expected writer delta가 모두 epsilon 이하인 경우에만 `NoIntent`로 정규화
- [x] `UnitMovementShadowObserver` — 서버 read-only reducer/Legacy writer 비교와 클라이언트 sentinel. 신규 경로의 Simulation Root·SimulationFacing write 0
- [x] Android-safe lossless coverage manifest — entry 경계 청크, 청크별 문자/UTF-8 byte 메타데이터, SHA-256, 최대 29청크와 terminal 3줄 예약, preflight fail-closed
- [x] Unity Editor self-validation PASS — scope lifecycle, 10°/15° 경계, endpoint `NoIntent`, target priority, duplicate/stale/invalid, 29청크/30청크 경계 포함
- [x] Android 1대 + Unity Editor counterpart 역할교대 멀티플레이에서 25/25종·Blue/Red 표본 확보
- [x] 모든 인정 세션에서 invalid·duplicate·stale·illegal·scope·unknown mismatch 0
- [x] Client reducer invocation, Simulation Root write attempt, exception, dropped log, manifest preflight failure, terminal overflow 0
- [x] 각 인정 세션의 manifest ordinal·entry count·SHA-256 일치

**25종 커버리지 그룹:**

1. Editor Host / Android Client — Assault, LittleKnight, Pistoleer, SpearMan
2. Editor Host / Android Client — BattleAxe, CannonCart, Sniper, Tank
3. Android Host / Editor Client — BearGuard, MushroomBomber, RabbitTrickster, RhinoBreaker
4. Android Host / Editor Client — BloomFairy, EagleArcher, FoxMagician, LionKnight
5. Editor Host / Android Client — DustSpirit, EmberSpirit, FlameSpirit, InfernoSpirit, TideSpirit
6. Editor Host / Android Client — BoulderSpirit, QuakeSpirit, StreamSpirit, TorrentSpirit

6개 인정 경기의 accepted reducer decision은 합계 545,190회이고 manifest entry는 합계 420개다. 그룹 5는 host reducer 200,291회, manifest 20/20청크·87항목·SHA-256 `16f57a6d8ac2383f4d41365511f835ecd68d8779e9e2c329ac42ded7fbb31a16`으로 종료했다. 그룹 6은 host reducer 113,236회, manifest 13/13청크·58항목·SHA-256 `771a8b22a4ef023d64815f1754bc006e205894bb45a6e9baa25758ccbde16853`으로 종료했다.

**진단 교정 이력:** 초기 Android 로그 손실은 v4의 byte-bounded 청크와 reserved terminal로 제거했다. 목표점에서 위치·목표·expected delta가 모두 0인 정상 상태를 invalid로 보던 진단기 오류는 v5 endpoint `NoIntent` 정규화로 교정하고 재시험했다. 잘못된 브랜치에서 실행돼 B2 로그가 없었던 5종 시험과 교정 전 실패 세션은 완료 증거에서 제외했다. 첫 4종은 observer v4, 나머지 21종은 v5 증거이며 reducer schema는 모두 `b2-movement-reducer-v1`이다.

**B2 최종 판정:** 서버 이동·SimulationFacing Shadow와 25/25종·Blue/Red 누적 증거 게이트는 PASS다. `legacyMovedWhileShadowAlign`은 신규 계약과 기존 Legacy writer의 분류된 차이이며 실제 writer 교정이 B3에 남았다는 근거다. 각 유닛을 Host와 Client 역할 양쪽에서 각각 시험한 것은 아니며, 역할교대는 전체 경기 집합에서 검증했다. 실제 writer는 계속 Legacy이므로 이동 방향과 바라보기 방향 교정 완료로 판정하지 않는다. 다음 단계는 **B3 경기 단위 25종 이동·SimulationFacing writer 전환**이다. 공격 방향과 시각 Impact/실제 피해 시점은 Tracer C/D까지 계속 미완료다.

사용자가 Testcase 작성을 요청하지 않았으므로 별도 `Testcase.md`는 생성하지 않았으며, 이번 실행 결과는 이 진행 기록에 보존한다.

### 2026-08-04 — Tracer B3 연속 이동 구현 및 Editor 조건부 PASS

- [x] `UnitMovementContracts.cs`에 pure Application `UnitServerTrajectoryPlanner`, `UnitTrajectoryStep`, `UnitPathCheckpointTracker` 구현
- [x] 공통 서버 writer에 A*·Chase·PendingRepath·PostCombatResume 연결
- [x] 경유 타일 중심 스냅 제거, 실제 trajectory distance 기반 waypoint 순차 소비와 최종 목적지 수렴
- [x] logical path corridor point sweep, 안전 후보 축소와 실패 시 fallback repath
- [x] candidate position 기준 적·힐 타겟 조회 후 획득 확정 pose까지만 commit
- [x] commit된 `SimulationFacing`을 `UnitData.Facing`에 동기화하고 이동 방향과 같은 접선을 사용
- [x] 공통 270°/s 회전 제한, 150° 이상 반전 hold, hold tick 진행량 0
- [x] `UnitMovementAuthorityObserver`가 candidate acquire commit을 정상 권위 결과로 분류하도록 gate 교정
- [x] Unity 컴파일 과정에서 발견된 CS0165 미할당 지역 변수들을 명시적 기본값으로 초기화
- [x] 최초 self-validation이 발견한 60° 곡선의 waypoint orbit을 통과 기반 checkpoint 소비로 교정
- [x] 최종 Unity self-validation PASS: `continuous 60-degree trajectory/150-degree reverse/held-progress invariants`
- [x] 사용자 Unity Editor smoke: 정상 코너 이동 체감이 교정된 것으로 잠정 확인
- [ ] 새 Android Development Build 생성
- [ ] Editor Host·Android Client / Android Host·Editor Client 역할교대
- [ ] 공통 이동 변경 뒤 25종 전체 회귀. 기존 B3 21/25종 로그는 정확성 기준선일 뿐 완료 증거로 재사용하지 않음
- [ ] 다음 경기 `Legacy` mode rollback과 기존 공격·피해·HP·RPC·VFX 불변 확인

**2026-08-18 Android Host 실패:** 예외·ANR·OOM 없이 Unity 로그가 이동 표본에서 끝나고 프로세스 CPU가 상승한 경기 정지를 확인했다. `RepathRequired → RequestMove → needRepath → yield 없는 outer continue`가 동일·순환 path에서 한 frame 무한 반복이 될 수 있는 구조를 확인했다. native stack 확정은 기기 root 제한으로 못했으나 로그·CPU·코드 경로가 동일한 실패 모델을 가리킨다.

- [x] pure 유한 재탐색 guard와 동일 path·A→B→A 순환 종료 구현
- [x] 한 frame당 유닛별 재탐색 1회, 새 path는 다음 frame에 적용
- [x] 진전 시 guard reset, 무진전 반복 시 해당 유닛만 현재 pose에 fail-closed
- [x] self-validation에 동일/순환/invalid/frame budget/서로 다른 path 무진전 8회 상한/다음-frame/진전 reset 유한 종료 회귀 추가
- [ ] Editor·Android에서 fail-closed 유닛 외 다른 유닛과 서버 경기 루프 진행 확인
- [ ] Editor 경로 무효화 재현와 Android Host 정지 재현 회귀 PASS
- [ ] 정지 회귀 PASS 후 Android/Editor 역할교대·25종 전체·Legacy rollback 재개

**B3 현재 판정: FAIL / OPEN.** 9.8 유한 재탐색 구현과 Unity compile·self-validation은 PASS했다. 그러나 Android Host 경기 루프 정지 재현 회귀는 아직 실행하지 않았으므로 빌드 안정성 완료를 주장하지 않는다. 다음은 새 Android Development Build에서 fail-closed 유닛 외 경기 진행과 정지·ANR·로그 폭주 0을 확인하는 것이다. 공격 타겟·공격 방향, 시각 Impact·실제 피해 적용 시점은 이번 교정 범위가 아니며 계속 미완료다.
