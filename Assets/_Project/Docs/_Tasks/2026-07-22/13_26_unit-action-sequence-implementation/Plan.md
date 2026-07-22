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
3. **Tracer A2:** Server-authoritative pose seam shadow — 다음 단계
4. **Tracer B:** Phase 2~3 — Root 분리와 이동/방향
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

## 8. 구현·검증 진행 기록

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
