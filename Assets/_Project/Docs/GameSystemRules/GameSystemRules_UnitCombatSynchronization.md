# Game System Rules — 유닛 멀티플레이 전투 동기화

- **목적·답하는 질문:** 서버 권위 행동·타격 결과를 같은 identity와 시간축으로 전달·표현하는 계약을 판단한다.
- **포함:** Simulation/Visual Root 경계, 회차·revision·시간·전달·순서 역전·멱등·취소·표현 소유권
- **제외·담당 문서:** 게임플레이 의미는 [Units](GameSystemRules_Units.md), NGO 타입·RPC·클래스 배치는 [TDD](../TechnicalDesignDocument.md), 수치 미러는 [StatsReference](../StatsReference.md), 개별 이행·수용은 [감사표](../Assets/UnitCombatAssetMatrix.md)
- **권위원본·주요 참조:** 서버 권위·복제·전달 계약 원본; NET 안정 ID 유지
- **갱신 조건·방법:** 계약 승인 변경 때 해당 NET 본문을 갱신하고 범위를 명시한다. source-freeze·실행 인계·검증 회차는 [상세 이력](../History/ImplementationVerificationHistory.md#combat-handoff)에 두며 native PASS/빌드 클릭을 실기 수용/빌드 완료로 합치지 않는다.

현행 개별 구현·수용 범위는 [감사표](../Assets/UnitCombatAssetMatrix.md)를 따른다. 과거 C2/B3 실패와 Phase 인계는 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-01)에 보존하며 과거 자동 PASS를 멀티 전체 완료로 해석하지 않는다.

---

## 0. 공통 용어

- **Simulation Root**: 서버 권위 위치와 방향을 보유하고 NetworkTransform·사거리·타겟·공격 판정이 참조하는 유닛 루트다.
- **Visual Root**: Simulation Root의 자식으로서 팀별 화면 변환, 모델 방향 오프셋, Animator와 VFX를 담당하는 클라이언트 표현 루트다.
- **행동 회차(Action Sequence)**: 서버가 확정한 하나의 이동·회전·공격 행동과 그 진행 단계를 식별하는 기록이다.
- **공격 회차 ID(AttackSequenceId)**: 공격자별로 단조 증가하며 하나의 Windup·Impact·Recovery를 끝까지 묶는 서버 권위 식별자다.
- **타격 번호(HitIndex)**: 한 공격 회차 안의 여러 Impact를 0부터 순서대로 식별하는 번호다.
- **Impact**: 서버가 공격의 적중·빗나감·피해·회복·상태 효과 결과를 확정하는 권위 순간이다. Animation Event 자체가 아니다.
- **전달 방식(Delivery)**: 효과가 목표 위치에 도달하는 방식이며 MeleeContact·Hitscan·ProjectileImpact·TravelingArea로 구분한다.
- **대상 범위(TargetScope)**: 공격이 단일 대상인지 범위 대상인지 나타내는 축이며 Single 또는 Area다.
- **범위 모양(AreaShape)**: TargetScope가 Area일 때 사용하는 Cone·Circle·Rectangle 등의 월드 좌표 판정 형태다.
- **효과 종류(Effect)**: 공격이 적용하는 Damage·Heal·Status의 의미다.
- **적용 일정(ApplicationSchedule)**: 효과가 Instant·MultiImpact·Periodic·ImpactThenPeriodic·ContactOncePerTarget 중 어떤 시간 패턴으로 적용되는지를 나타낸다.
- **Windup**: 공격 회차가 커밋된 뒤 첫 Impact 전까지의 준비 구간이다.
- **Recovery**: 마지막 Impact 후 다음 행동이 가능해질 때까지의 구간이다.
- **SimulationFacing**: 서버 Simulation Root가 보유하고 이동·사거리·공격 판정에 사용하는 권위 방향이다.
- **VisualFacing**: 각 클라이언트가 SimulationFacing을 팀 관점으로 변환해 Visual Root에 표시하는 방향이다.
- **AimDirection**: 서버가 Impact 또는 발사·활성 시점에 기록하는 판정 방향이며 클라이언트가 다시 계산하지 않는다.
- **ImpactPoint**: 서버가 ProjectileImpact 또는 범위 결과의 중심으로 확정한 월드 좌표다.
- **AttackTimeline**: Windup·ActionMarkerOffset·Recovery를 정의하는 검증된 공격 시간 데이터다.
- **AttackProfile**: Delivery·TargetScope·AreaShape·Effect·ApplicationSchedule·RangeMetric·AttackTimeline을 묶은 서버 판정 설정이다.
- **AcquireRange**: 타겟이 없는 유닛이 새 타겟 후보를 획득할 수 있는 거리다.
- **LoseRange**: 이미 획득한 타겟을 유지할 수 있는 최대 거리이며 AcquireRange보다 크게 두어 타겟 떨림을 막는다.
- **Interval**: 하나의 공격 회차 커밋부터 다음 공격 회차를 커밋할 수 있을 때까지의 전체 주기다.

---

## 1. 권위 경계

### NET-AUTH-001. 서버 행동 권위

서버는 다음 항목의 유일한 권위자다.

- 시뮬레이션 위치와 방향
- 이동 시작·정지, 경로 revision·진행도, logical path corridor, 연속 선회 trajectory와 틱별 이동량
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

Simulation Root는 서버 좌표계의 위치와 방향을 보유한다. NetworkTransform, 사거리, 타겟 탐색, 공격 방향 및 착탄 판정은 이 Root만 참조한다.

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
- `NetworkTransform`은 서버 권위와 canonical world-space 동기화를 유지하고 `Interpolate=true`, `PositionLerpSmoothing=false`를 사용한다. 이는 전체 보간을 끄는 계약이 아니라 NGO LegacyLerp의 종료 잔차가 Simulation Root의 안정 pose에 남지 않게 하는 설정이다.
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
- WaitingRepath
- Blocked
- AlignToMove
- Move
- AcquireTarget
- Chase
- AlignToAttack
- Windup
- Impact
- Recovery
- Dead

구현 enum을 반드시 위 목록과 일치시킬 필요는 없지만, 네트워크 스냅샷은 클라이언트가 현재 단계를 구분할 충분한 정보를 제공해야 한다. `WaitingRepath`는 다음 서버 frame의 경로 평가를 기다리는 일시 상태이고 `Blocked`는 같은 이동 목표가 경로 환경 변경을 기다리는 상태다. 둘 다 `Completed`가 아니며 외부 시스템이 같은 목표를 새 command로 반복 발행하는 근거가 될 수 없다. 단순 `Walk / Attack` 애니메이션 값만으로 행동 권위를 표현하지 않는다.

같은 이유로 Animator가 Attack 표현을 유지한다는 사실만으로 서버가 Windup·Impact 단계라고 추측하지 않는다. 공격 사거리 안의 새 후보로 타겟을 교체하는 동안 서버 행동은 `AlignToAttack`일 수 있으며, Attack 표현 연속성과 새 `AttackSequenceId`의 승인 여부는 별도 값으로 처리한다.

---

## 4. AttackSequence 계약

### NET-ACTION-SEQ. 공격 회차 식별

서버가 공격을 커밋하고 Windup에 진입할 때 공격자별로 단조 증가하는 `AttackSequenceId`를 발급한다. 커밋 전 Align 단계는 행동 스냅샷만 있고 `AttackSequenceId=0`이다. 한 공격의 준비, 타격, 범위 결과, 회복 및 표현은 같은 ID를 사용한다.

동일 공격에 여러 타격이 있으면 0부터 시작하는 `HitIndex`로 구분한다. 표현과 결과를 공격자별 FIFO 순서로 연결하지 않는다.

Legacy scheduler가 관측 틱보다 앞선 실제 주기 경계를 overshoot로 보존하는 경우에도 Shadow `CommitServerTime`은 현재 관측 서버 시각을 유지한다. commit을 과거로 backdate하지 않으며, 같은 회차의 `effectiveCooldown = max(0, cooldown - overshoot)`와 각 hit의 `effectiveImpactOffset = max(0, hitTime - overshoot)`를 Legacy와 Shadow Begin/Commit/Impact가 공동 사용한다. 이 유효 시간 값을 적용한 뒤 원래 hit offset이나 별도 관측 시각을 다시 더해 Impact due를 늦추지 않는다.

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

pre-impact authorization 전에 공격자 또는 커밋 타겟이 사라진 경우 `AttackerUnavailable`과 `TargetUnavailable`을 구분한다. 같은 reservation·`HitIndex`의 authorization miss 원인과 정식 결과 outcome은 의미가 일치해야 하며, 전체 결과 키와 정규 결과 확인 계약을 통과해야 한다. 서로 다른 원인을 주장하는 결과는 fail-closed 하고 `HitApplied`, 일반 `Miss` 또는 0피해 성공으로 위조하지 않는다.

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
- 서버 투사체 표현(`NET-DELIVERY-PROJECTILE`)과 그에 맞춘 사망 표현·화상 VFX 시작은 순수 클라이언트에만 이 지연을 적용하고, 호스트·싱글은 0초로 재생한다. 그 밖의 경로는 호스트도 같은 표현 지연 정책을 사용해 공격 모션과 HP·피격 표현의 상대 시점을 맞춘다. *(2026-10-26 개정 — 사용자 결정 「현재 구현 동작이 올바르다 — 이 동작에 맞춰 문서를 교정」: T-3a 채택, T-3b(서버 투사체 코드에 Host 지연 0.10초 추가) 기각. 서버 투사체 화면 시각은 단계 A부터 「서버 시계 − (순수 클라이언트면 0.10초, 호스트·싱글이면 0초)」로 동작해 왔다. 「그 밖의 경로」의 호스트 처리는 전수 확인하지 않았으므로 종전 문장을 그대로 남겼다. 개정 전 원문 「호스트도 같은 표현 지연 정책을 사용해 공격 모션과 HP·피격 표현의 상대 시점을 맞춘다.」 — [서버 권위 투사체 Plan 4-3](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-3-net-time-002-pending-amendment-20261026) T-3)*
- 서버 내부 HP·판정과 사망 처리(도메인 제거·Despawn)는 지연하지 않는다. 표시용 HP 텍스트·피격 반응은 권위 Result와 같은 표현 Impact에 맞춘다. **HP바는 아직 게임에 없다 — 추후 추가 예정 기능이다.** 추가되면 HP바도 이 항목과 같은 표현 시점 정책(권위 Result와 같은 표현 Impact)을 따른다. 서버 투사체(`NET-DELIVERY-PROJECTILE`)의 사망타로 죽은 대상의 사망 표현(쓰러짐·사망 VFX·SFX·소멸)과, 그 착탄으로 시작하는 화상(InfernoSpirit) 지속 피해 VFX의 시작도 같은 화면 착탄까지 미룬다(각 보류 상한 0.3초). 그 밖의 사망 표현과 독 등 다른 지속 피해 VFX는 즉시 재생한다. *(2026-10-26 개정 — 사용자 결정 「HP바는 나중에 추가할 생각」: 현재 기능처럼 적혀 있던 「HP바」를 지우지 않고 추후 추가 예정 표기로 바꿨다. 개정 전 원문 「서버 내부 HP와 판정은 지연하지 않는다. 표시용 HP바·텍스트·피격 반응만 권위 Result와 같은 표현 Impact에 맞춘다.」 — [서버 권위 투사체 Plan 4-3](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-3-net-time-002-pending-amendment-20261026) T-2. 같은 절의 사망 표현 맞춤(T-1)과 호스트 지연 문장(T-3)은 이때 사용자 승인 대기였고, 같은 날 뒤이어 반영했다 — 아래 조각과 위 항목)* *(2026-10-26 개정 — 사용자 결정 「현재 구현 동작이 올바르다 — 이 동작에 맞춰 문서를 교정」: T-1 반영. 단계 D-2 `fb77548c`부터 투사체 사망타의 사망 표현과 화상 VFX 시작을 화면 착탄까지 미루는 동작을 맞춤 목록에 더했다. 서버 판정·도메인 제거·Despawn 시각은 바뀌지 않는다. 4-3 문안의 「지속 피해 VFX」는 코드가 화상만 미루고 독은 즉시 시작하므로 「화상(InfernoSpirit)」으로 좁혔다. 개정 전 원문(T-2 반영 뒤) 「서버 내부 HP와 판정은 지연하지 않는다. 표시용 HP 텍스트·피격 반응만 권위 Result와 같은 표현 Impact에 맞춘다.」 — [서버 권위 투사체 Plan 4-3](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-3-net-time-002-pending-amendment-20261026) T-1)*

회차 계획을 미리 받은 클라이언트는 공격자 Windup·발사 모션을 표현 시간축에 예약한다. 피격 VFX·HP 텍스트·피해자 반응은 권위 `AttackImpactResult`를 받은 경우에만 같은 표현 Impact에 재생한다. 결과를 미리 추측하지 않는다.

정상 지연이 CombatPresentationDelay 안에 들어오면 공격 모션, 표시용 HP 변화와 피격 표현이 같은 Impact에 보인다. 예산을 초과한 결과는 이미 지난 시점으로 소급 재생하지 않고 즉시 catch-up하며 진단 로그를 남긴다. 모든 네트워크 조건에서 완전 동시를 보장한다고 주장하지 않는다.

### NET-TIME-003. 늦은 도착

이미 시작된 회차를 늦게 수신하면 표현 서버 시각에 맞게 애니메이션을 fast-forward한다. 이미 지난 타격 결과는 현재 상태를 즉시 수렴시키고, 기존부터 관측 중인 회차에서 아직 표시하지 않은 피해자 반응·HP 텍스트·Impact VFX만 최대 0.50초 age 안에서 한 번 catch-up한다. 다음 공격 타격으로 넘기거나 FIFO에서 기다리지 않는다.

### NET-TIME-004. 순서 역전

타격 결과가 시작 스냅샷보다 먼저 도착해도 정규 결과 키로 제한된 버퍼에 보관해 같은 회차에 결합한다. 버퍼는 공격자 회차당 최대 64개 결과, 최대 2초 age를 허용한다. 회차 완료·취소·Despawn은 미래 표현 허가를 닫지만 이미 확정된 결과를 소급 무효화하지 않는다. 결과 수신과 종료 알림의 순서가 바뀌어도 같은 권위 상태로 수렴한다. 만료 시 진단 로그를 남기고 권위 HP는 유지하며 transient 표현은 `NET-TIME-003`의 age와 `NET-TIME-005` baseline 안에서만 최대 한 번 catch-up한다. 사라진 View나 재사용 ID에 과거 반응을 적용하지 않는다. 로컬 Animation Event 빈 신호를 다음 공격에 재사용하지 않는다.

### NET-TIME-005. 늦은 참가와 재접속

현재 행동 스냅샷에는 진행 중 회차와 `LastConfirmedHitIndex`가 포함되어야 한다. 연결·스폰 시 이 값을 해당 클라이언트의 presentation baseline으로 기록하고 그 이하 결과는 상태만 수렴하며 과거 VFX를 재생하지 않는다. baseline 이후 아직 유효한 현재 행동만 표현 서버 시간에 맞춰 재생한다. 이 정책으로 지연 패킷 catch-up과 늦은 참가 과거 미재생을 구분한다.

---

## 6. 방향과 타겟 동기화

### NET-FACING-001. 이동 방향

서버가 `DesiredMoveDirection`, `SimulationFacing`과 틱별 위치 변화를 결정한다. 클라이언트는 위치 변화량으로 별도의 권위 방향·코너 trajectory·이동 시작/정지를 추측하지 않는다.

- 서버의 Authoritative Locomotion은 위치 진행 방향과 `SimulationFacing`을 같은 trajectory 접선에서 원자적으로 결정한다. 비영 위치 변화 방향과 같은 틱의 `SimulationFacing` 오차는 1° 이하여야 한다.
- A* 선분 전환, 추격, 재경로와 전투 종료 후 이동 재개가 서로 다른 이동·회전 writer를 사용하지 않는다.
- 10° 진입 / 15° 이탈은 잘못된 방향 이동을 막는 fail-closed 기준이다. 이동 가능한 연속 trajectory가 이 기준 안에서 방향을 바꿀 수 있는 정상 코너를 매번 `AlignToMove` 정지로 바꾸지 않는다.
- 서버가 복제하는 이동 phase와 command/segment scope는 관측·늦은 참가 수렴을 위한 권위 상태다. 클라이언트는 이를 입력으로 reducer를 실행하거나 Simulation Root를 직접 쓰지 않는다.
- NetworkTransform은 서버 Simulation Root pose의 유일한 클라이언트 writer이고, Visual Root는 관점 변환과 허용된 표현 보간만 담당한다.
- 서버 복제 대상은 logical path 자체가 아니라 Authoritative Locomotion이 commit한 Simulation Root pose와 phase/scope다. 기본 A*의 경유 중심 checkpoint, logical path corridor, trajectory sweep, 실제 trajectory 거리/초 `MoveSpeed`, 공통 최대 회전 속도 270°/s와 candidate position 기준 target acquire는 서버에서만 계산한다. 경유 checkpoint는 Simulation Root가 해당 중심 허용 오차에 실제로 도달한 뒤에만 소비한다. candidate position으로 획득이 확정된 틱에는 그 위치까지만 commit하고 `NoIntent`를 게시하며 다음 틱의 추가 이동을 금지한다.
- 전투 추격은 같은 Authoritative Locomotion과 공간 preflight를 사용한다. 타겟까지의 직선 구간이 안전할 때만 direct chase를 사용하고, 중간 건물·완전 차단 지형·이동 불가 타일이 있으면 서버가 공격 접근 위치까지 경로를 계산한다. 동일한 unsafe direct candidate와 일반 A*를 번갈아 재시도하는 것은 유효한 복구가 아니다.
- 전투 종료 후에는 서버가 도달 가능한 전방 중심을 복귀 checkpoint로 선택하고, 그 중심까지의 안전한 direct 구간 또는 A* 복귀 경로를 사용한다. 클라이언트 보정이나 위치 스냅으로 중심 복귀를 대신하지 않는다.
- 전투 종료 복귀 목표가 현재 `UnitData.Position`과 같은 walkable 권위 타일이면 빈 A* 경로를 `Blocked`로 해석하지 않는다. Simulation Root가 아직 중심 밖이면 같은 Authoritative Locomotion으로 그 중심까지 걸어서 수렴한 뒤 완료한다. 차단 타일과 실제 unsafe/unreachable은 기존 fail-closed 재탐색 계약을 유지한다.
- 150° 이상의 반전은 연속 선회가 아니라 정지 정렬로 처리한다. 이 임계값은 네트워크 지연이나 클라이언트 상태로 변경하지 않는다.
- fail-closed는 unsafe candidate의 commit을 거부하는 서버 권위 원칙이다. 동일 path 1회, stale PendingPath 또는 일시적인 planner/pathfinder 불일치를 근거로 살아 있는 유닛의 이동 목표를 즉시 폐기하지 않는다.
- 같은 이동 목표의 재탐색 이력은 코루틴·segment·외부 ticker 재호출을 넘어 유지한다. 서버는 목표 변경, 실제 위치/checkpoint 진전 또는 walkability revision 변경만을 명시적인 재개·reset 근거로 사용한다.
- `Idle`, `AlignToMove`, `WaitingRepath`, `Blocked`처럼 서버 위치 변화량이 0인 비공격 상태에서는 Walk 표현 시간이 진행되지 않는다. 이 표현 상태도 서버가 값으로 복제하며 클라이언트가 NetworkTransform 변화량으로 추측하지 않는다.

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

TorrentSpirit만의 생성 전 승인 발사 예약은 `GameSystemRules_Units.md` 규칙 29를 따른다. primary 상실/사거리 이탈에도 같은 원래 대상의 현재 또는 마지막 유효 서버 aim으로 예정 발사를 정확히 한 번 수행하며 새 타깃으로 이전하지 않는다. 예약·서버 생성·contact 결과를 분리하고 기존 contact HP/힐 writer를 유지한다. 이 계약을 다른 TravelingArea 전체에 확대하거나 Client marker로 권위 생성·피해를 대신하지 않는다. 현행 Unresolved/LegacyFallback의 로컬 단조 예약 identity는 정규 C3 sequence를 위조하거나 Supported 승격을 뜻하지 않는다.

### NET-DELIVERY-TIMED

source freeze·정적 및 native/build 미관찰 인계는 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-02)에 보존한다. 현재 개별 연결·수용 범위는 [VFXSFXList](../Assets/VFXSFXList.md)와 [감사표](../Assets/UnitCombatAssetMatrix.md)를 따른다.

Periodic 효과는 최초 부여 타격의 회차 ID와 별도의 효과 인스턴스 ID를 가진다. 각 틱은 서버 시간과 틱 번호로 멱등 처리한다. 갱신·덮어쓰기·종료 정책은 `GameSystemRules_Units.md`의 효과 규칙을 따른다.

실제 writer의 `(target lifetime, TimedEffectKind.Damage)` 단일 레코드에 effect identity/revision/poison-burn provenance를 연결한다. 동일 owner refresh는 noRestart이며 poison↔burn/다른 Damage overwrite는 기존 정책대로 이전 표현을 종료·교체하고 독립 stack을 만들지 않는다. network current value와 late-init/restoration/shutdown 보호를 거쳐 single presenter가 대상 수명을 따른다. 취소·만료·사망 뒤 StopEmitting/natural drain/lease 반환은 강제 teardown과 구별한다. Mushroom 짧은 burst는3초 지면 damage zone이 아니다. Tower 확정 결과의 identity·Impact 위치/시각·Race snapshot을 표현에 전달하며 current target lookup·tracer 도착으로 피해를 새로 판정·지연하지 않는다. HP writer·주기·범위·Stats 및 Unresolved/Legacy 분류를 유지하고 자동 Supported 승격은 하지 않는다. 승인·구현 인계의 당시 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-03)다.

---

## 8. Animation Event와 표현

### SeparateHeal — 힐러 전용 값 동기화 분류 (2026-10-03)

BloomFairy의 단계별 코드·정적/native gate·빌드 시도·focused 실기 범위는 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-04)에 보존한다. 현재 개별 수용·미확정은 [감사표](../Assets/UnitCombatAssetMatrix.md)를 따르며 자동 PASS를 새 coroutine 실기나 최종 visual 수용으로 확대하지 않는다.

`GameSystemRules_Units.md` 규칙22·32의 힐러는 적 공격이 아닌 별도 회복 루프다. `NotApplicableAttack / SeparateHealPipeline`의 힐 표현 회차를 정규 AttackSequence/C3 scope로 포장하지 않는다. 다음 힐 전용 계약은 아래 일반 공격의 연속 재생·커밋 시 restart 금지와 구분하며, 다른 유닛의 공통 안전 계약을 완화하지 않는다.

- 서버만 별도 atomic snapshot NetworkVariable을 게시한다. lifetime/sequence/revision, 시전자/대상, 시작/종료 서버 시각, active, 종료 사유, Aim, marker offset/clip duration 및 Frozen/Held priority를 같은 값으로 전송한다. late spawn/init는 현재값을 복구하며 edge-only RPC에 의존하지 않는다.
- 새 힐 sequence는 시전 모션을 재생한다. 같은 sequence의 중복은 restart하지 않고 priority 해제는 서버 경과시간으로 이어간다. 낮은 sequence/revision, 다른 객체 수명, 종료된 같은 회차의 재활성화 및 terminal close 뒤 입력은 거부한다. 수신과 실제 Animator 적용 성공은 구분한다.
- Client는 모션만 재생한다. Aim은 증거용이며 새 Client Root/Visual offset 회전 입력이 아니다. 기존 서버 Root 방향 복제와 `NET-AUTH-001/002`, `NET-ROOT-001~003` 경계를 유지한다. Client가 아군을 재탐색하거나 HP/HoT/적 공격 결과를 생성하지 않는다.
- source marker는 힐 회차당 최대1회이며 이미 지난 marker는 catch-up에서 재방출하지 않는다. Frozen/Held priority와 terminal close를 유지하고 표현 종료는 이미 부여된 서버 HoT 수명을 종료하지 않는다. 폐기된 clip 끝자세 hold 정책은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-unitcombatsynchronization-01)다.
- 힐 clip·marker·cooldown·연속 모션·회전 정책은 [유닛 규칙32·36](GameSystemRules_Units.md)이 원본이다. 동기화는 같은 sequence 중복 restart 금지·future-start IsDue·late-init catch-up·lifetime/sequence/revision 멱등·실제 적용 후 priority cache를 보장한다. 서버 Action/Root 소유권과 SeparateHeal 분류를 유지한다. 이전 retime·native 적용·코드 비활성화 절차와 중복 수치 사본은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-unitcombatsynchronization-02)다.
- 힐 발동의 absolute due·회차/owner 보호와 configured cooldown 계약은 `GameSystemRules_Units.md` 규칙32를 따른다. 동일 코드·실기 인계 원문은 [보존 이력](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-07)에 통합하며 이전 timing FAIL·자동 gate·빌드 클릭은 최신 최종 실기 수용이 아니다.
- 이 예외는 일반 Attack 레벨 시작 차단·EnemyAttack/C3·SourceOnly lease 정책의 변경이나 Supported 승격이 아니다. 규칙33~37의 준비·발동 후 쿨다운·회복량/지속·diff 틱·완료 텍스트는 불변이다. 당시 구현·자동 gate·빌드 시도와 실기 미확인 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-06)다. 자동 PASS를 실제 경기 Analyze나 사용자 시각 수용으로 확대하지 않는다.
- 이전 경기의 전달·HP·Root 관측과 당시 미구현 제안은 [보존 이력](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-08)에 통합한다. 해당 관측을 방향·모션·Client HP/text 전수 검증으로 확대하지 않는다.

### NET-PRESENT-001. Animation Event 역할

`OnAttackHit` 같은 Animation Event는 로컬 VFX·SFX·카메라·무기 발사점 표식과 에셋 검증에만 사용한다. 실제 공격 결과나 회차 진행을 발생시키지 않는다.

차지 유닛(InfernoSpirit·FoxMagician)의 typed charge·launch marker와 추출·HP 경계는 `GameSystemRules_Units.md` 규칙17 「차지 유닛 typed marker 계약」을 따른다. `OnAttackCharge`(차지 시작 — 차지 VFX만, lease 확인만·소비 없음)와 `OnAttackHit`(발사 — lease 소비·tracer 발사·공격 SFX)를 구분하며 charge를 피해 offset으로 추출하거나 `OnAttackHit`에서 charge를 다시 재생하지 않는다. **[2026-10-20 개정 — 사용자 승인 목표 계약 · 🔴 이 문단의 차지 유닛(InfernoSpirit·FoxMagician)에는 아직 미반영 — 단계 D 구현 진행 중(2026-10-21 갱신: 「투사체 유닛의 피해는 서버 착탄 시각」은 StreamSpirit·Tank·CannonCart에 구현됨, `GameSystemRules_Units.md` 규칙 17 아래 2026-10-21 조각)]** 발사 전 취소 시 **서버가 발사를 취소하며** 차지 VFX는 즉시 정지하고 tracer·SFX·피해는 없다(경계는 Units 규칙 17). **투사체 유닛의 피해는 서버 착탄 시각에 확정된다.** Unresolved 분류와 기존 권위 writer는 유지하고 Fox Immediate intent는 제거한다. *(2026-10-20 개정 — 사용자 승인(서버 권위 투사체 Plan 4-2-1 F-3). 구현 전인 지금 코드는 종전 문장 「발사 전 취소 시 차지 VFX는 즉시 정지하고 tracer·SFX는 없으며, 서버 피해 시점은 바뀌지 않는다. Unresolved/LegacyFallback·Immediate intent·기존 권위 writer를 유지한다.」대로 동작한다. 개정 전 원문: [서버 권위 투사체 Plan 4-2-3](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-2-3-규칙-문서-반영과-개정-전-원문))* *(2026-10-19 개정 — 사용자 승인: 종전은 Fox 전용 문단이었고 「damage callback에서 charge를 소비·재생하지 않는다」였다. 개정 전 원문: [작업 Plan 9-4](../_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Plan.md#9-4-상시-문서-반영과-개정-전-원문))* 원래 `2:25@30fps`의 복원이며 이전2.25초 환산은 현행 값이 아니다. source8 manifest·controlled compile·일반23종 감사 및 native/멀티/시각 미실행 경계 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-05), 현재 개별 수용은 [감사표](../Assets/UnitCombatAssetMatrix.md)를 따른다. 이 한정 결과를 전체 SOLO 완료로 확대하지 않는다.

최초 공격 사거리 진입의 `AlignToAttack`은 위치를 완전히 정지한 채 기존 Attack 클립을 선행 표현으로 재생할 수 있다. 이때 이동 reducer의 `TargetAcquirePriority + NoIntent`는 일반 이동 보류가 아니라 Action handoff다. 서버와 Client 표현은 Walk 첫 자세·Idle·Held를 중간에 삽입하지 않고 현재 Walk에서 provisional Attack으로 직접 전환한다. 공격 사거리 안의 유효 후보가 계속 존재하면 타겟 교체 중에도 기존 Attack 표현 상태를 유지할 수 있다. 두 경우 모두 커밋 전 표현은 provisional이며, 지나간 Animation Event는 새 회차의 VFX·SFX·트레이서·피격 표현을 방출하거나 정규 결과를 소비하지 않는다. 서버 Root가 목표 방향 5도 이내에 들어와 새 회차를 커밋해도 화면의 같은 Attack 클립을 restart하지 않고 현재 정규화 진행 위치를 보존한다. 서버는 커밋 뒤 아직 지나가지 않은 첫 marker부터 타격 표현과 결과를 승인하며, 현재 cycle의 marker가 모두 지났다면 다음 cycle의 첫 marker까지 기다린다. 커밋 전 marker의 소급 승인과 클립 되감기는 모두 금지한다.

`AcquireTarget`의 서버 후보는 아직 네트워크 표현 타겟이 아니다. Action 소유권 전에는 후보만 보류하고 `ChangeTarget` 표현 명령을 발행하지 않는다. 최초/재진입에서는 provisional Start의 단일 payload가 target·revision·impact suppression을 Host에 먼저 적용하고 원격 Client에 전달한다. 이미 Attack 표현 중인 활성 전투의 교체만 Action 소유권과 Host 적용 성공 뒤 별도 target-change로 발행하며, 적용 실패 뒤 RPC만 전송하거나 Start와 같은 target-change를 중복 전송하지 않는다.

### NET-PRESENT-002. 검증된 AttackTimeline

서버가 읽는 검증된 AttackTimeline 또는 AttackProfile이 Windup, 타격 오프셋, Recovery의 정규 원본이다. 완성 유닛의 Animation Event는 이 데이터와 허용 오차 안에서 일치해야 한다.

같은 Attack 표현 상태 안에서 새 회차가 시작되더라도 `(AttackerInstanceId, AttackSequenceId, Revision)` 시작 경계를 한 번만 수락한다. 중복·오래된 시작 신호는 Animator를 다시 시작하지 않으며, 양수 회차 뒤 커밋 전 neutral Align 상태로 돌아가도 마지막 발급 회차 상한을 보존하여 이후 오래된 회차가 최신 표현을 되돌리지 못하게 한다.

provisional 표현에서 커밋된 회차로 전환할 때도 같은 연속 재생 anchor를 유지한다. 회차 커밋은 서버 승인 경계와 marker 소비 가능 범위를 변경할 뿐 Animator 재생 위치를 0으로 되돌리는 명령이 아니다. 커밋 시점보다 앞선 marker는 이미 소비된 것으로 간주하지도, 새 회차 결과로 소급 결합하지도 않는다.

Attack 클립의 재생 상태와 공격 회차 스코프의 발행은 서로 다른 책임이다. 서버가 전투 이탈 없이 두 번째 이후 공격 회차를 커밋하더라도 **모든 커밋마다** 새 `(AttackerInstanceId, AttackSequenceId, first HitIndex, Revision)`를 Host와 Client에 원자적으로 발행해야 한다. 이미 Attack 클립이 재생 중이면 이 발행은 Animator를 restart하거나 CrossFade하지 않고 marker가 소비할 회차 커서만 새 스코프의 `HitIndex=0`부터 갱신한다. “Attack 애니메이션을 이미 시작했다”는 상태 가드로 후속 회차 스코프 발행을 생략하는 것은 금지한다. 중복·오래된 스코프는 무시하되, 정상적인 단조 증가 회차는 전투 상태가 유지되는 동안에도 빠짐없이 수락한다.

각 스코프는 해당 AttackProfile의 유효 `HitIndex` 범위만 정확히 한 번 소비할 수 있는 1회성 표현 허가다. 마지막 유효 marker가 소비되면 같은 스코프는 즉시 `Consumed`가 되고, 다음 단조 증가 스코프가 `Armed`될 때까지 모든 반복 marker를 fail-closed로 억제한다. Marker는 유효 스코프 소비에 성공한 뒤에만 VFX·SFX·Tracer·로컬 피격 신호를 방출한다. 스코프가 없거나 0이거나 소모됐는데도 로컬 emitter가 실행되는 것은 C3 실패이며, provisional·소모 후 반복 marker가 아무 연출도 만들지 않고 억제되는 것은 정상이다. 원거리 Tracer는 발사 marker에서 소비한 불변 스코프를 착탄 콜백까지 보존하되 다음 회차 스코프로 바꾸지 않는다.

단, 위 fail-closed 표현 허가는 해당 유닛 타입의 C3 공격 프로필이 `Supported`이고 정규 스코프를 실제로 발행하는 경기 경로에만 적용한다. 마이그레이션 중 `Unresolved`로 명시된 타입은 비교 분모와 정규 스코프 발행 대상에서 제외하되, 그 사실을 이유로 기존 Legacy 공격 VFX·SFX·Tracer를 차단하지 않는다. `Unresolved` 타입에 0/default 스코프를 유효한 것처럼 전달하거나 `impactEnabled=true`와 결합해 공통 marker gate를 통과시키는 것도 금지한다. 해당 타입은 Legacy 표현을 그대로 유지하고 미지원 사실을 진단 manifest에 남기며, 정규 AttackProfile·타임라인·스코프가 완성된 뒤에만 위 1회성 허가로 전환한다.

연속 재생 중인 Attack 클립에서는 고정된 프로필 offset을 커밋 시각에 다시 더하는 것만으로 Impact 시각을 정하지 않는다. 서버는 현재 연속 재생 anchor에서 **커밋 뒤 실제로 다음에 도착할 marker occurrence**를 선택하고, 그 절대 `ImpactServerTime`과 `(AttackerInstanceId, AttackSequenceId, HitIndex, Revision)` 스코프를 같은 커밋 결과로 원자적으로 발행한다. 서버 판정 예약과 Client marker 커서는 같은 occurrence를 가리켜야 하며, 한쪽만 다음 marker로 보정하고 다른 쪽은 원래 offset을 사용하는 분리는 금지한다.

### NET-PRESENT-003. 피격 표현 상관관계

HP 텍스트, 피격 VFX, SFX와 타격 반응은 공격자 FIFO가 아니라 `NET-ACTION-IDEMPOTENT`의 정규 결과 키에 연결한다. 한 번의 AoE 타격으로 여러 대상이 맞으면 같은 회차와 HitIndex 아래 VictimKind·VictimId·EffectKind·ResultOrdinal로 각 결과를 구분해 함께 재생한다.

C3 Coordinator는 일정(Schedule), 확정 결과(Result), 선택적 로컬 표식(Marker)을 별도 입력으로 받는다. Schedule은 커밋 당시 의도·예약 시각·commit revision의 불변 기록이며 적중·피해자 수·최종 Impact 방향을 예측하지 않는다. 실제 Impact에서 서버가 확정한 result revision·AuthoritativeAimDirection·outcome이 결과 표현의 기준이다. commit revision과 impact revision이 다를 수 있으며, 이를 같은 값으로 강제하거나 commit 방향으로 Windup 추적을 막지 않는다.

공격 인스턴스를 한 번도 연 적 없는 유닛의 종료에는 retire할 표현 수명이 없다. 이 `None` 수명은 정상 no-op이며 invalid 공격 결과나 손상된 유효 ID로 집계하지 않는다. 유효 인스턴스의 중복·충돌·누락·용량 초과는 계속 fail-closed하고 원인별 증거를 남긴다.

Marker와 Tracer는 공격 HitIndex의 로컬 표현 단계다. 여러 피해자 결과를 가진 AoE에서 Marker 1개를 피해자별 결과 키와 1:1이라고 가정하지 않는다. marker가 없거나 늦어도 유효한 Result의 표현을 다음 marker까지 보류하지 않는다. 표시 시각은 `NET-TIME-002`의 공통 표현 시간축을 사용하고 지연된 결과는 `NET-TIME-003`으로 처리한다. 물리적인 네트워크 지연 0을 보장하거나 임의 delay를 추가해 진단을 통과시키지 않는다.

AoE 원자 표현에는 서버가 타격 처리를 마친 뒤 발행한 완결 묶음 또는 명시적 완료 manifest가 필요하다. manifest는 해당 HitIndex의 정규 결과 키 집합/개수를 판별할 수 있어야 한다. 커밋 시점 대상 수, 첫 결과, ordinal 최댓값 또는 Animation Event 개수로 완료를 추정하지 않는다. 이 계약이 아직 배선되지 않은 단계에서는 결과별 비교만 가능하며 AoE 원자 방출 완료로 보고하지 않는다.

타겟 사망·교체·StopCombat은 미래 marker 허가와 미확정 예상 표현을 닫는다. 이미 확정된 결과는 이후 종료 알림으로 폐기하지 않고 Result와 같은 키로 보존한다. 표시는 기존 age/baseline 및 대상 View 수명 정책으로 결정하고, 피해·HP 사실을 취소하거나 사망 시 일괄 flush하지 않는다.

**좁은 Torrent 예외:** `GameSystemRules_Units.md` 규칙 29의 서버 승인 발사 예약은 primary 상실·target switch·StopCombat만으로 취소하지 않는다. 이는 닫힌 공통 marker나 provisional 표현을 다시 허가하는 것이 아니다. 실제 서버 파도 생성과 contact 결과에 연결해 표현하고, 죽은 primary에 가짜 결과를 만들지 않는다. 발사 전 공격자 사망·게임 종료·despawn/shutdown은 예약을 취소하며 다른 타입의 위 공통 표현 종료 규칙은 유지한다.

확정된 시각 결과는 피해자 종류·팀·Impact 위치·적용량·결과 HP처럼 필수 표현에 필요한 불변 스냅샷을 포함해야 한다. C3 정상 표현기는 별도 `OnEntityDamaged` 도착, HP 동기화 재발행 또는 현재 Domain/Factory/View 조회를 필수 조건으로 삼지 않는다. 피해자 View가 이미 사라져도 age/baseline 안의 확정 결과는 저장된 위치에서 HP 텍스트와 설정된 피격 VFX를 최대 한 번 표시한다. 살아 있는 View의 펀치 같은 객체 반응은 동일 객체 수명이 확인될 때만 추가하며, 확인할 수 없거나 ID가 재사용됐으면 그 선택 반응만 생략한다.

타입별 피격 VFX 프리셋은 선택 에셋 채널이다. 프리셋이 아직 없는 현재 상태는 `SkippedNoAsset` 정상 생략이며 C3 실패가 아니다. 그러나 향후 프리셋을 연결했을 때 같은 확정 결과만으로 즉시 재생할 수 있도록 피해자 종류·팀·Impact 위치는 에셋 유무와 무관하게 항상 전송한다. 설정된 프리셋의 실제 재생 실패는 `ChannelFailure`로 분리해 fail-closed하고, 에셋 미설정·전역 presenter 미준비·스냅샷 손상과 하나의 `viewUnavailable` 합계로 섞지 않는다. 피격 VFX와 유닛 제거 시 사망 VFX는 별도 표현 채널이며 서로를 대신하거나 중복 호출하지 않는다.

AoE 완결 묶음의 원자성은 서버가 확정한 결과 집합의 완결성과 같은 표현 시각 방출을 뜻한다. 한 피해자의 현재 View 부재가 다른 피해자의 self-contained 필수 표현을 보류하거나 만료시키지 않는다. 필수 스냅샷 손상과 전역 presenter 미초기화는 fail-closed하되, 선택 View 반응 부재와 구분된 원인별 증거를 남긴다.

전환은 비교 모드에서 새 Coordinator의 예정 방출과 레거시 실방출을 먼저 기록한 뒤 경기 고정 단일 writer 모드로 진행한다. 비교 모드는 화면을 바꾸지 않으며 그 빌드를 시각 교정 완료로 안내하지 않는다. 실제 adapter·서버 일정 복제·완결 묶음·표현 시간축 연결과 검증이 끝나야 새 Coordinator가 유일한 방출자가 된다.

Marker·Tracer·Enqueue·Emit은 서로 다른 표현 단계이며 동일 exact 결과 키와 스코프에서 각 단계별 최대 한 번만 결합한다. 타겟 사망·사거리 이탈·StopCombat·새 타겟 전환으로 닫힌 구회차의 Marker와 Tracer를 이후 결과나 새 회차에 재사용하지 않는다. 공격 결과가 없는 표현 관측도 시간 근접만으로 다른 결과에 결합하지 않고 명시적으로 폐기 또는 `Unmatched`로 남긴다.

공격 방향을 Host/Client 사이에서 검증할 때는 동일 `(AttackerInstanceId, AttackSequenceId, Revision, HitIndex)`의 `AuthoritativeAimDirection`과 표현 방향만 실제 방향 일치 판정에 사용한다. 동일 revision의 허용 오차 초과만 표현 방향 실패이며, revision-lag와 scope-mismatch는 방향 writer 실패로 합치지 않고 별도의 복제 수렴 실패로 분류한다. 이 분류를 이유로 Client가 Simulation Root, NetworkTransform 또는 서버 Aim을 다시 쓰는 것은 금지한다.

### NET-PRESENT-004. 타임아웃

타임아웃은 정상 동기화 수단이 아니다. 데이터 손상이나 유실을 복구하고 로그를 남기는 최후 안전망이다. 타임아웃된 표현을 다음 공격의 Animation Event에 연결하지 않는다.

타임아웃 fallback은 정상 marker와 동일한 타격 시점 일치 증거가 아니라 **marker 누락 복구 표현**으로 별도 분류한다. fallback이 실행됐다는 이유로 정상 marker timing을 PASS 처리하거나, 이후 늦게 도착한 marker를 같은 결과 또는 다음 회차 결과에 다시 결합하지 않는다. timeout 자체의 복구 지연과 실제 marker의 Impact 시점 오차는 서로 다른 기준과 카운터로 검증하며, 하나의 허용 오차 상향으로 함께 숨기지 않는다.

---

## 9. 취소·사망·중단

### NET-CANCEL-001. 커밋 전 취소

공격이 커밋되기 전에는 타겟 변경이나 이동 재개로 자유롭게 취소할 수 있다. 공격 회차 ID는 커밋할 때만 발급한다.

### NET-CANCEL-002. 커밋 후 취소

커밋 후 취소·빗나감 여부는 `GameSystemRules_Units.md`의 전달 방식별 규칙을 따른다. 서버는 종료 사유를 회차 결과로 보낸다.

### NET-CANCEL-003. 공격자 사망

공격자가 사망하면 Windup 중인 회차와 아직 실행되지 않은 비독립 future `HitIndex`를 취소한다. `MeleeContact`·`Hitscan`과 아직 독립 전달체를 만들지 않은 예약 타격은 지연 작업이 이미 만들어졌더라도 판정 dispatch 전에 폐기하며, `AttackImpactResult`나 완료 결과를 생성하지 않는다. 이미 서버가 발사한 독립 ProjectileImpact 또는 이미 생성된 TravelingArea만 해당 공격 프로필의 `PersistsAfterSourceDeath` 값에 따라 계속 진행할 수 있다.

### NET-CANCEL-004. 타겟 사망

타겟 사망이 기존 회차를 다른 타겟으로 자동 이전시키지 않는다. 새 타겟은 새 공격 회차에서만 사용한다.

TorrentSpirit의 서버 승인 발사 예약만 `GameSystemRules_Units.md` 규칙 29의 예외를 적용한다. 원래 primary 사망·소멸·사거리 이탈은 이 예약을 취소하지 않지만 새 타깃으로 대체하지도 않는다. 공격자 사망·게임 종료·despawn/shutdown의 발사 전 취소 및 provisional 미발사는 유지한다. 다른 타입의 타깃 유효성 검사와 취소 정책은 바꾸지 않는다.

### NET-CANCEL-005. 취소와 결과 순서 역전

- 서버에서 이미 적용된 AttackImpactResult는 이후 취소가 소급 무효화하지 않는다.
- 취소가 future HitIndex를 폐기하면 서버는 해당 타격의 판정 dispatch·AttackImpactResult·완료 결과를 모두 생성하지 않는다. 취소된 타격을 `AuthorizationUnavailable`, 일반 `Miss` 또는 오류 결과로 변환하지 않는다.
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
