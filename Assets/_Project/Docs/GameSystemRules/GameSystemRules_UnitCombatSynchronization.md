# Game System Rules — 유닛 멀티플레이 전투 동기화

멀티플레이에서 유닛의 이동 방향, 타겟, 공격 방향, 타격 결과와 화면 표현이 같은 서버 행동을 재생하도록 만드는 동기화 계약이다.

이 문서는 **무엇을 동기화해야 하는지**를 정의한다. 구체적인 NGO 타입, RPC 이름, 클래스 배치는 `TechnicalDesignDocument.md`가 담당한다. 게임플레이 의미는 `GameSystemRules_Units.md`, 런타임 수치는 검증된 `UnitStatsConfig`와 향후 `AttackProfile`, 사람이 읽는 수치 미러는 `StatsReference.md`, 에셋 감사 스냅샷은 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`가 담당한다.

---

## 1. 권위 경계

### NET-AUTH-001. 서버 행동 권위

서버는 다음 항목의 유일한 권위자다.

- 시뮬레이션 위치와 방향
- 이동 시작·정지와 경로 진행
- 타겟 획득·잠금·상실
- 공격 회차의 생성·취소·종료
- 공격 전달 방식과 타격 시각
- 타격 순간의 권위 방향·착탄 위치
- 적중·빗나감·피해·회복·상태 효과 결과
- HP와 사망 상태

클라이언트는 서버 행동을 재생하고 보간하지만 결과를 새로 판정하지 않는다. 클라이언트 Animator, 로컬 Animation Event, VFX 위치 또는 프레임 진행률은 서버 결과의 입력이 될 수 없다.

### NET-AUTH-002. 즉시 수렴과 표현 예약의 분리

권위 HP와 상태는 서버 결과를 수신하는 즉시 수렴시킨다. 화면 표현은 동일한 공격 회차와 타격 번호의 서버 Impact 시각에 맞춰 예약할 수 있지만, 표현을 기다리기 위해 권위 상태 적용을 지연하지 않는다.

---

## 2. Simulation Root와 Visual Root

### NET-ROOT-001. Simulation Root

Simulation Root는 서버 좌표계의 위치와 방향을 보유한다. NetworkTransform, 사거리, 충돌, 타겟 탐색, 공격 방향 및 착탄 판정은 이 Root만 참조한다.

클라이언트는 Simulation Root에 팀별 화면 반전이나 Animator 보정을 직접 쓰지 않는다.

### NET-ROOT-002. Visual Root

Visual Root는 Simulation Root의 자식 표현 계층이다. 다음 작업만 담당한다.

- Blue/Red 관점 변환
- 모델 고유 방향 오프셋
- Animator와 로컬 보간
- 무기 발사점, VFX, SFX 및 피격 반응

Red 관점의 180도 변환은 Visual Root에만 적용한다. 서버 Simulation Root와 네트워크 동기화 값은 Blue 기준 단일 좌표계를 유지한다.

### NET-ROOT-003. 방향 용어

- **SimulationFacing**: 서버 판정에 사용되는 권위 방향
- **VisualFacing**: 해당 클라이언트 화면에 렌더링되는 방향

VisualFacing은 SimulationFacing을 재생한 결과이며 판정 원본이 아니다.

### NET-ROOT-004. 신규·교체 유닛 프리팹 승인 게이트

이 계약은 현재 등록된 프리팹뿐 아니라 앞으로 추가하거나 교체하는 모든 유닛 프리팹에 적용한다. 신규 유닛 타입은 Blue/Red 프리팹 쌍이 모두 아래 조건을 통과하기 전에는 생산 목록이나 빌드에 등록하지 않는다.

- 프리팹 최상위 오브젝트를 Simulation Root로 사용하고 `NetworkObject`, `NetworkTransform`, `NetworkUnit`, `UnitView`, `VisualRootProjector` 등 네트워크·권위 컴포넌트를 이 Root에 둔다.
- Simulation Root의 직접 자식 표현 계층은 로컬 위치 `(0,0,0)`, 로컬 회전 identity, 로컬 스케일 `(1,1,1)`인 `VisualRoot` 하나로 구성한다.
- 모델, Renderer, Animator, 무기 발사점, `VfxSpawnPoint`와 그 밖의 표현 전용 오브젝트는 모두 `VisualRoot` 아래에 둔다. `NetworkObject`와 `NetworkTransform`은 `VisualRoot`로 이동하지 않는다.
- Collider는 Simulation Root에만 두고 `VisualRoot` 하위에는 두지 않는다. 이는 Simulation Root의 충돌·선택 경계를 화면 관점 반전과 분리하는 authoring 계약이며, 서버 피해 판정이 Collider에 의존한다는 뜻은 아니다.
- Simulation Root에는 Animator와 Renderer를 두지 않는다. 각 Animator와 같은 GameObject에 `AnimationEventRelay` 하나를 두고, 프리팹 전체 relay 수가 Animator 수와 일치해야 한다.
- 모든 Animator의 Root Motion을 비활성화한다. Animator가 Simulation Root 또는 Visual Root를 별도의 이동 writer로 만들 수 없다.
- `VisualRootProjector._visualRoot`는 해당 프리팹의 직접 자식 `VisualRoot`를 참조해야 하며 누락·중복 projector를 허용하지 않는다.
- 사거리·타겟·방향·착탄·피해 판정은 Simulation Root pose만 읽는다. VFX·SFX·플로팅 텍스트·피격 반응 등 표현 소비자는 presentation pose를 읽으며 Simulation Root에 쓰지 않는다.
- 신규 타입은 `UnitType`, `Unit_<UnitType>_<Blue|Red>` 형식의 Blue/Red 파일명과 등록, 스탯·공격 프로필, Animator·VFX 연결, 에셋 감사표와 구조 검증기의 예상 roster·기준선을 함께 갱신한다. 검증 상수를 완화하거나 검증기를 우회하여 프리팹을 승인하지 않는다.
- 전체 구조 검증과 Host/Client·Blue/Red smoke를 통과해야 한다. 어느 한쪽 프리팹만 통과한 부분 상태는 실패로 처리한다.

기존 50개 프리팹을 전환한 B1 일괄 migration은 과거 자산을 위한 일회성 도구다. 신규 프리팹은 검증된 migrated 템플릿에서 처음부터 위 구조로 만들고 전체 검증을 실행한다. Legacy 구조의 외부 프리팹을 가져오면 별도 단일 프리팹 설정 절차 또는 수동 규격화 후 검증하며, 이미 전환된 전체 프리팹에 B1 일괄 migration을 다시 적용하지 않는다.

---

## 3. 서버 행동 상태

### NET-ACTION-STATE. 값 기반 행동 상태

서버는 현재 행동 상태를 값으로 보유하며 늦게 스폰되거나 재접속한 클라이언트도 현재 상태를 복원할 수 있어야 한다.

최소 상태는 다음과 같다.

- Idle
- AlignToMove
- Move
- AcquireTarget
- Chase
- AlignToAttack
- Windup
- Impact
- Recovery
- Dead

구현 enum을 반드시 위 목록과 일치시킬 필요는 없지만, 네트워크 스냅샷은 클라이언트가 현재 단계를 구분할 충분한 정보를 제공해야 한다. 단순 `Walk / Attack` 애니메이션 값만으로 행동 권위를 표현하지 않는다.

---

## 4. AttackSequence 계약

### NET-ACTION-SEQ. 공격 회차 식별

서버가 공격을 커밋하고 Windup에 진입할 때 공격자별로 단조 증가하는 `AttackSequenceId`를 발급한다. 커밋 전 Align 단계는 행동 스냅샷만 있고 `AttackSequenceId=0`이다. 한 공격의 준비, 타격, 범위 결과, 회복 및 표현은 같은 ID를 사용한다.

동일 공격에 여러 타격이 있으면 0부터 시작하는 `HitIndex`로 구분한다. 표현과 결과를 공격자별 FIFO 순서로 연결하지 않는다.

### 최소 데이터

공격 회차 시작 스냅샷은 최소한 다음 정보를 가진다.

- `AttackSequenceId`
- `AttackerId`와 재스폰·ID 재사용을 구분하는 `AttackerInstanceId`
- `TargetId` 또는 권위 목표 위치
- 공격 프로필 ID와 전달 방식
- 타겟 처리 방식: TargetLocked / LockedPoint / Homing 등
- `StartServerTime`
- `CommitServerTime`
- 계획된 Windup 및 타격별 시간 오프셋
- 현재 행동 단계

각 권위 타격 레코드는 다음 정보를 가진다.

- `AttackSequenceId + HitIndex`
- `ImpactServerTime`
- `AuthoritativeAimDirection`
- 필요한 경우 `ImpactPosition`
- 적중·빗나감·취소 상태
- 대상별 피해·회복·상태 효과 결과

회차 종료 정보는 `RecoveryEndServerTime`과 종료 사유를 가진다.

### NET-ACTION-IDEMPOTENT. 멱등 처리

클라이언트는 아래 정규 키로 결과와 표현을 최대 한 번 적용한다.

```text
직접·범위 결과:
AttackerInstanceId + AttackSequenceId + HitIndex
+ VictimKind + VictimId + EffectKind + ResultOrdinal

Periodic 틱:
EffectInstanceId + TickIndex + VictimKind + VictimId + EffectKind

상태 스냅샷:
AttackerInstanceId + AttackSequenceId + Revision
```

중복 메시지는 무시하고, 이미 종료된 회차보다 오래된 상태 스냅샷은 현재 상태를 되돌리지 않는다.

---

## 5. 서버 시간 재생

### NET-TIME-001. 단일 서버 시간축

공격 시작, 타격, 착탄, 지속 효과 틱과 회복 종료는 동기화된 서버 시간축으로 표현한다. 로컬 프레임 수 또는 패킷 도착 시각을 권위 시각으로 사용하지 않는다.

### NET-TIME-002. 지연 흡수 표현 시간축

각 클라이언트는 서버 시뮬레이션보다 뒤에서 재생하는 `CombatPresentationDelay`를 사용한다.

```text
PresentationServerTime = SynchronizedServerTime - CombatPresentationDelay
```

- 초기값은 0.10초다.
- 실제 적용값은 NetworkTransform 보간 지연과 추정 단방향 지연 + jitter margin 중 큰 값으로 정하고 0.075~0.25초 범위에서 조정한다.
- 호스트도 같은 표현 지연 정책을 사용해 공격 모션과 HP·피격 표현의 상대 시점을 맞춘다.
- 서버 내부 HP와 판정은 지연하지 않는다. 표시용 HP바·텍스트·피격 반응만 권위 Result와 같은 표현 Impact에 맞춘다.

회차 계획을 미리 받은 클라이언트는 공격자 Windup·발사 모션을 표현 시간축에 예약한다. 피격 VFX·HP 텍스트·피해자 반응은 권위 `AttackImpactResult`를 받은 경우에만 같은 표현 Impact에 재생한다. 결과를 미리 추측하지 않는다.

정상 지연이 CombatPresentationDelay 안에 들어오면 공격 모션, 표시용 HP 변화와 피격 표현이 같은 Impact에 보인다. 예산을 초과한 결과는 이미 지난 시점으로 소급 재생하지 않고 즉시 catch-up하며 진단 로그를 남긴다. 모든 네트워크 조건에서 완전 동시를 보장한다고 주장하지 않는다.

### NET-TIME-003. 늦은 도착

이미 시작된 회차를 늦게 수신하면 표현 서버 시각에 맞게 애니메이션을 fast-forward한다. 이미 지난 타격 결과는 현재 상태를 즉시 수렴시키고, 기존부터 관측 중인 회차에서 아직 표시하지 않은 피해자 반응·HP 텍스트·Impact VFX만 최대 0.50초 age 안에서 한 번 catch-up한다. 다음 공격 타격으로 넘기거나 FIFO에서 기다리지 않는다.

### NET-TIME-004. 순서 역전

타격 결과가 시작 스냅샷보다 먼저 도착해도 정규 결과 키로 제한된 버퍼에 보관해 같은 회차에 결합한다. 버퍼는 공격자 회차당 최대 64개 결과, 최대 2초 age를 허용한다. 회차 완료·취소·Despawn 또는 만료 시 폐기하고 진단 로그를 남긴다. 만료돼도 권위 HP는 유지하며 결과별 최소 catch-up을 최대 한 번만 수행한다. 로컬 Animation Event 빈 신호를 다음 공격에 재사용하지 않는다.

### NET-TIME-005. 늦은 참가와 재접속

현재 행동 스냅샷에는 진행 중 회차와 `LastConfirmedHitIndex`가 포함되어야 한다. 연결·스폰 시 이 값을 해당 클라이언트의 presentation baseline으로 기록하고 그 이하 결과는 상태만 수렴하며 과거 VFX를 재생하지 않는다. baseline 이후 아직 유효한 현재 행동만 표현 서버 시간에 맞춰 재생한다. 이 정책으로 지연 패킷 catch-up과 늦은 참가 과거 미재생을 구분한다.

---

## 6. 방향과 타겟 동기화

### NET-FACING-001. 이동 방향

서버가 DesiredMoveDirection과 SimulationFacing을 결정한다. 클라이언트는 위치 변화량으로 별도의 권위 방향을 추측하지 않는다.

### NET-FACING-002. 공격 방향

타겟 잠금과 방향 잠금을 분리한다.

- 일반 타겟 잠금 공격은 회차 동안 TargetId를 유지하되 Windup 중 서버가 타겟을 추적 회전한다.
- 각 타격 순간의 SimulationFacing을 `AuthoritativeAimDirection`으로 기록해 판정과 표현이 같은 방향을 사용한다.
- LockedPoint 투사체는 발사 시 권위 목표 위치와 방향을 고정한다. Single은 ImpactHitRadius 결과를, Area는 권위 착탄점 범위 결과를 서버가 확정한다.
- Homing 투사체는 서버가 추적 대상과 갱신 방식을 관리한다.

클라이언트가 타겟 위치를 다시 읽어 과거 타격 방향을 재계산하지 않는다.

---

## 7. 전달 방식별 동기화

### NET-DELIVERY-HITSCAN

Hitscan은 서버 Impact 시각에 결과가 확정된다. 총구 섬광·트레이서·피격 표현은 같은 `AttackSequenceId + HitIndex`를 재생하며 트레이서 도착을 기다려 서버 결과를 변경하지 않는다.

### NET-DELIVERY-PROJECTILE

ProjectileImpact는 서버가 발사 시각, 착탄 시각, 목표 처리 방식과 착탄 위치를 관리한다. 피해와 범위 효과는 권위 착탄 레코드에서만 확정된다. 로컬 투사체는 서버 궤적의 표현이며 로컬 충돌로 피해를 만들지 않는다.

### NET-DELIVERY-TRAVELING

TravelingArea는 서버가 판정 영역을 진행시키고 대상별 첫 접촉을 기록한다. 대상별 결과는 같은 공격 회차 안에서 독립 HitIndex 또는 명시적 접촉 인덱스로 식별한다.

### NET-DELIVERY-TIMED

Periodic 효과는 최초 부여 타격의 회차 ID와 별도의 효과 인스턴스 ID를 가진다. 각 틱은 서버 시간과 틱 번호로 멱등 처리한다. 갱신·덮어쓰기·종료 정책은 `GameSystemRules_Units.md`의 효과 규칙을 따른다.

---

## 8. Animation Event와 표현

### NET-PRESENT-001. Animation Event 역할

`OnAttackHit` 같은 Animation Event는 로컬 VFX·SFX·카메라·무기 발사점 표식과 에셋 검증에만 사용한다. 실제 공격 결과나 회차 진행을 발생시키지 않는다.

### NET-PRESENT-002. 검증된 AttackTimeline

서버가 읽는 검증된 AttackTimeline 또는 AttackProfile이 Windup, 타격 오프셋, Recovery의 정규 원본이다. 완성 유닛의 Animation Event는 이 데이터와 허용 오차 안에서 일치해야 한다.

### NET-PRESENT-003. 피격 표현 상관관계

HP 텍스트, 피격 VFX, SFX와 타격 반응은 공격자 FIFO가 아니라 `NET-ACTION-IDEMPOTENT`의 정규 결과 키에 연결한다. 한 번의 AoE 타격으로 여러 대상이 맞으면 같은 회차와 HitIndex 아래 VictimKind·VictimId·EffectKind·ResultOrdinal로 각 결과를 구분해 함께 재생한다.

### NET-PRESENT-004. 타임아웃

타임아웃은 정상 동기화 수단이 아니다. 데이터 손상이나 유실을 복구하고 로그를 남기는 최후 안전망이다. 타임아웃된 표현을 다음 공격의 Animation Event에 연결하지 않는다.

---

## 9. 취소·사망·중단

### NET-CANCEL-001. 커밋 전 취소

공격이 커밋되기 전에는 타겟 변경이나 이동 재개로 자유롭게 취소할 수 있다. 공격 회차 ID는 커밋할 때만 발급한다.

### NET-CANCEL-002. 커밋 후 취소

커밋 후 취소·빗나감 여부는 `GameSystemRules_Units.md`의 전달 방식별 규칙을 따른다. 서버는 종료 사유를 회차 결과로 보낸다.

### NET-CANCEL-003. 공격자 사망

공격자가 사망하면 Windup 중인 회차는 취소한다. 이미 서버가 발사한 독립 ProjectileImpact 또는 이미 생성된 TravelingArea는 해당 공격 프로필의 `PersistsAfterSourceDeath` 값에 따라 계속 진행할 수 있다.

### NET-CANCEL-004. 타겟 사망

타겟 사망이 기존 회차를 다른 타겟으로 자동 이전시키지 않는다. 새 타겟은 새 공격 회차에서만 사용한다.

### NET-CANCEL-005. 취소와 결과 순서 역전

- 서버에서 이미 적용된 AttackImpactResult는 이후 취소가 소급 무효화하지 않는다.
- 취소가 future HitIndex를 폐기하면 서버는 해당 타격 결과를 생성하지 않는다.
- 클라이언트는 `(AttackerInstanceId, AttackSequenceId, Revision)`보다 오래된 상태를 무시하되, 서버가 실제 생성한 결과는 정규 결과 키로 한 번 표시한다.
- 공격자 사망, 타겟 사망, StopCombat과 Despawn도 같은 우선순위를 따른다.

---

## 10. 현재 구조에서의 전환 규칙

기존 `HitPresentationQueue`의 공격자별 FIFO, 로컬 `OnAttackHit` 빈 신호 폐기, 타임아웃 기반 다음 주기 방출은 이 문서로 대체 대상이다. 기존 경로는 신규 회차 경로를 shadow mode로 검증하기 전까지 제거하지 않는다.

InfernoSpirit의 `InfernoAttackBehavior`와 QuakeSpirit의 `QuakeAttackBehavior`는 2026-07-20 main에 반영된 **Legacy authority adapter**다. 두 핸들러의 서버 피해 결과와 특수 효과 의미는 보존하되, 현재 `ExecuteAttack`/공격자 FIFO 결과를 v2 완료로 간주하지 않는다. 이전 시 각각 `AttackProfile`의 Effect/Area 구성요소로 옮기고 동일한 `AttackSequenceId + HitIndex + ResultOrdinal` 결과를 방출해야 한다.

전환 순서는 다음과 같다.

1. 서버 회차와 타격 ID를 기존 결과 옆에서 **기록만** 한다. Shadow 경로는 피해, RPC, VFX를 발생시키지 않는다.
2. 기존 FIFO 결과와 신규 상관관계 결과를 로그로 비교한다.
3. Simulation Root와 Visual Root를 분리하되 기존 피해 권위는 유지한다.
4. 신규 Snapshot/ImpactResult를 shadow 전송하고 신규 Presenter는 로그만 남긴다.
5. 경기 시작 전에 서버가 `CombatSchemaRevision + AttackProfileHash + CombatPipelineMode`를 확정해 모든 참가자와 일치시킨다. 불일치하면 연결을 거절하거나 해당 경기 전체를 Legacy 모드로 시작하며 경기 중 변경하지 않는다.
6. Presentation 전환 경기에서는 Legacy와 신규 중 **정확히 하나만** VFX·HP 텍스트·피격 반응을 emit한다.
7. Authority 전환 경기에서는 신규 Sequencer만 타겟·방향·Impact·피해를 쓰고 Legacy scheduler/PendingHit 피해 writer를 끈다. 같은 경기에서 유닛별로 두 권위 경로를 혼합하지 않는다.
8. rollback은 진행 중 경기가 아니라 다음 경기 시작 시 CombatPipelineMode를 바꾸는 방식으로만 수행한다.
9. 유닛별 멀티플레이 검증 후 기존 FIFO와 클라이언트 루트 보정을 비활성화한다.
10. 사용자 실기 검증과 문서 갱신 승인 후 기존 경로를 제거한다.

항상 `single-writer / single-emitter`를 지킨다. 이 문서는 목표 계약이며 현재 구현 완료를 의미하지 않는다.
