# Game System Rules — 유닛

유닛의 이동, 방향 정렬, 타겟, 공격, 피해·회복 및 특수 효과에 관한 게임플레이 불변 규칙이다.

이 문서는 **게임에서 무엇이 참이어야 하는지**를 정의한다. 멀티플레이 복제와 순서 역전 처리는 `GameSystemRules_UnitCombatSynchronization.md`, 클래스·RPC·레이어 배치는 `TechnicalDesignDocument.md`, 런타임 수치는 검증된 `UnitStatsConfig`와 향후 `AttackProfile`, 사람이 읽는 수치 미러는 `StatsReference.md`, 유닛별 에셋·구현 감사 스냅샷은 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`가 담당한다.

> **상태:** 2026-07-20 규칙 v2 개정 완료. 런타임 구현은 아직 기존 구조이며 이 문서의 완료를 의미하지 않는다.

---

## 1. 공통 권위와 좌표

### U-AUTH-SERVER. 서버 권위

멀티플레이에서 서버는 이동, 경로 진행, 타겟, SimulationFacing, 공격 회차, 타격 시각·위치·결과, HP, 회복, 지속 효과 및 사망의 유일한 권위자다.

클라이언트는 서버 결과를 재생하고 보간할 뿐, Animator·Animation Event·VFX·로컬 충돌로 게임 결과를 만들지 않는다.

### U-COORD-WORLD. 판정 좌표

이동, 감지, 사거리, 방향, 범위 판정은 서버의 XZ 월드 좌표를 사용한다. Blue/Red 화면 관점 변환은 판정 좌표를 변경하지 않는다.

사거리 임계값은 공격 프로필의 `RangeMetric`으로 단일 계산한다. 기존 동작을 보존하는 초기 프로필은 다음과 같다.

- AttackRange ≤ 0.5인 근접 공격: `MeleeContactDistance = 0.63 world unit`
- 그 외 중심 거리 공격: `AttackRange × TileHeight`
- 공통 경계 오차: `RangeEpsilon = 0.05 world unit`
- 건물 대상 중심점 보정: `TargetRadius = 0.20 world unit`

향후 콜라이더 또는 유닛별 TargetRadius를 도입할 때는 AttackProfile의 명시적 값으로 이전하고 방향별 인접 타일 테스트를 통과해야 한다. 공격별 범위 모양은 타일 소속이 아니라 월드 좌표로 계산한다.

### U-ROOT-SEPARATION. 시뮬레이션과 표현 분리

- **Simulation Root:** 서버 위치·방향, NetworkTransform, 충돌·사거리·판정
- **Visual Root:** 진영별 화면 변환, 모델 오프셋, Animator, VFX·SFX

Red 화면의 180도 반전은 Visual Root에만 적용한다. 클라이언트가 NetworkTransform 대상 Root를 직접 이동·회전하지 않는다.

---

## 2. 이동

### U-MOV-PATH. 경로와 점유

서버는 A*로 현재 위치에서 목적지까지 경로를 계산하고 타일 중심 사이를 이동시킨다.

- 정적 이동 불가 지형과 건물은 경로를 차단한다.
- 다른 유닛의 현재 타일·선점 타일은 경로 차단 정보로 사용하지 않는다.
- 같은 타일에 여러 유닛이 겹칠 수 있다.
- 경로의 다음 타일이 건물 생성 등으로 이동 불가가 되면 서버가 현재 위치에서 재탐색한다.
- 모든 유닛은 `MoveSpeed`(tiles/second)를 사용하며 한 타일 이동 시간은 `1 / MoveSpeed`다.

### U-MOV-PHASE. 이동 단계

유닛 이동은 다음 의미 단계를 가진다.

```text
Idle → Navigate → AlignToMove → Move
                         ├─ 적 감지 → AcquireTarget
                         └─ 경로 차단 → Navigate
```

구현 enum은 다를 수 있지만, 이동 전 정렬과 실제 이동을 구분해야 한다.

### U-MOV-ALIGN. 이동 방향 정렬

1. 서버는 다음 이동 목표의 XZ 벡터로 `DesiredMoveDirection`을 계산한다.
2. 새 이동 방향을 받았을 때 방향 오차가 10°를 초과하면 속도를 0으로 하고 `AlignToMove`에서 제자리 회전한다.
3. 최단 Yaw 방향으로 회전해 오차가 10° 이하가 된 서버 틱부터 이동한다.
4. 이동 중 오차가 다시 15°를 초과하면 즉시 정지하고 재정렬한다.
5. 10° 진입 / 15° 이탈의 히스테리시스로 경계에서 정지와 이동이 반복되지 않게 한다.
6. 뒤를 보거나 옆을 본 채 다음 타일로 이동하지 않는다.
7. 전투 종료 후 A* 이동을 재개할 때도 같은 규칙을 적용하며 위치를 스냅하지 않는다.
8. 정렬 중 적이 AcquireRange에 들어오면 이동 정렬을 중단하고 타겟 획득을 우선한다.

각도는 초기 공통값이며 멀티플레이 실기 검증 후 `StatsReference.md`에서 조정할 수 있다.

---

## 3. 감지와 타겟

### U-TARGET-RANGE. 획득·공격·이탈 거리

거리 관계는 다음을 만족한다.

```text
AttackRange ≤ AcquireRange < LoseRange
```

- 기존 `DetectRange`는 `AcquireRange`로 해석한다.
- 현재 `DetectRange == AttackRange`인 유닛도 유효하다.
- 기본 `LoseRangeWorld`는 `AcquireRange`를 프로필의 RangeMetric으로 월드 단위 변환한 값에 `0.25 world unit`을 더하며 유닛별로 조정할 수 있다.
- LoseRange 여유값은 타겟 유지 히스테리시스이며 공격 사거리 오차와 다르다.

### U-TARGET-SELECT. 결정적 타겟 선정

서버는 AcquireRange 안의 살아 있고 공격 가능한 적을 다음 순서로 고른다.

1. XZ 제곱 거리를 `0.001 world unit²` 단위로 양자화한 값의 오름차순
2. 같은 거리에서는 대상 종류의 안정 순서: Unit → Building
3. 같은 종류에서는 안정 EntityId 오름차순

순회 순서나 클라이언트 프레임에 의존하는 “처음 발견된 적”을 사용하지 않는다. 성 공성이나 경로 차단 건물처럼 별도 강제 타겟 규칙이 필요한 경우 해당 정책을 일반 우선순위보다 먼저 명시한다.

### U-TARGET-HOLD. 타겟 유지

한 번 획득한 타겟은 살아 있고 유효하며 LoseRange 안에 있는 동안 유지한다. 더 가까운 적이 새로 들어왔다는 이유만으로 즉시 변경하지 않는다. 타겟 사망·무효화·LoseRange 이탈 시 해제한 뒤 결정적 규칙으로 다시 찾는다.

---

## 4. 전투 상태와 방향

### U-COMBAT-PHASE. 공격 단계

공격은 다음 의미 단계를 가진다.

```text
AcquireTarget → Chase → AlignToAttack → Windup → Impact(s) → Recovery
```

- Chase: AcquireRange 안의 타겟이 AttackRange 밖이면 타겟 방향으로 이동한다.
- AlignToAttack: AttackRange 안에서 이동을 멈추고 공격 방향을 맞춘다.
- Windup: 공격이 커밋된 후 첫 Impact 전 준비 구간이다.
- Impact: 서버가 타격별 결과를 확정하는 순간이다.
- Recovery: 마지막 Impact 후 다음 행동이 가능해질 때까지의 구간이다.

공격 중에는 이동하지 않는다. 예외적인 이동 공격은 별도 공격 프로필로 명시해야 한다.

### U-ATK-ALIGN. 공격 방향 정렬

1. 타겟이 유효하고 AttackRange 안이며 공격 사용 가능 상태여도 방향 오차가 5°를 초과하면 공격을 시작하지 않는다.
2. 유닛은 정지한 채 `AlignToAttack`에서 회전한다. 이때 쿨다운과 Windup을 시작하지 않는다.
3. 오차가 5° 이하가 된 서버 틱에 공격을 커밋하고 Windup을 시작한다.
4. Windup 중 잠긴 타겟을 서버가 계속 추적 회전한다.
5. MeleeContact와 Hitscan은 각 결과 Impact 순간 방향 오차가 8°를 초과하면 해당 타격이 빗나간다. ProjectileImpact와 TravelingArea는 Launch 또는 Activation 순간에 8°를 검사하고, 이후 착탄·접촉은 저장된 권위 방향과 위치를 사용한다.
6. 5° 진입 / 8° 유지의 히스테리시스를 사용한다.
7. MeleeContact·Hitscan은 Impact 순간, ProjectileImpact·TravelingArea는 Launch·Activation 순간의 SimulationFacing을 권위 AimDirection으로 기록해 판정과 표현이 같은 방향을 사용한다.

### U-TARGET-COMMIT. 타겟 잠금과 공격 커밋

- 커밋 전 Acquire·Align 단계에서는 타겟을 변경할 수 있다.
- Windup 시작과 함께 AttackSequenceId와 TargetId를 고정한다.
- 같은 공격 회차의 타격을 다른 타겟으로 이전하지 않는다.
- 커밋 전 타겟 사망·무효화·LoseRange 이탈은 타겟을 해제하고 비용 없이 취소한다. AttackRange만 벗어난 경우에는 타겟을 유지한 채 Chase로 돌아가며 쿨다운을 소비하지 않는다.
- 커밋 후 취소·빗나감·타겟 사망은 일반 쿨다운을 환불하지 않는다.
- 새 타겟 공격은 반드시 새 AttackSequenceId를 사용한다.

타겟 잠금과 방향 잠금은 다르다. 일반 공격은 TargetId를 잠근 채 Windup 중 방향을 갱신하고, LockedPoint 공격만 발사 시 AimDirection과 ImpactPoint를 고정한다.

---

## 5. 공격 전달과 결과

### U-COMBAT-DELIVERY. 전달 방식

공격 프로필은 다음 전달 방식 중 하나를 가진다.

- **MeleeContact:** 서버 접촉 Impact에 결과 확정
- **Hitscan:** 서버 발사 Impact에 즉시 결과 확정
- **ProjectileImpact:** 서버 권위 발사체의 착탄 Impact에 결과 확정
- **TravelingArea:** 서버 이동 판정 영역이 대상과 접촉할 때 결과 확정

원거리라는 이유만으로 모두 Hitscan 또는 시각 트레이서로 취급하지 않는다.

### U-COMBAT-AXES. 독립 공격 속성

전달 방식과 다음 속성을 분리한다.

- **TargetScope:** Single / Area
- **AreaShape:** Cone / Circle / Rectangle 등
- **Effect:** Damage / Heal / Status
- **ApplicationSchedule:** Instant / MultiImpact / Periodic / ImpactThenPeriodic / ContactOncePerTarget

한 공격 회차가 주 타겟 직접 결과와 범위 결과를 함께 가지면 `ImpactComponent[]`로 각 구성요소의 TargetScope·AreaShape·Effect·Schedule을 따로 선언한다.

“착탄형 AoE”는 전달 방식과 범위를 혼합한 Legacy 용어이므로 신규 프로필에서 사용하지 않는다.

### U-IMPACT-MELEE. 근접 타격

MeleeContact는 각 Impact에 타겟 생존, AttackRange, 방향 오차 8° 이하를 다시 확인한다. 조건을 만족하지 않으면 해당 HitIndex만 빗나간다. MultiImpact는 타격별로 독립 검증하되 TargetId는 유지한다.

### U-IMPACT-HITSCAN. 즉발 타격

Hitscan은 각 발사 Impact에 타겟 생존, AttackRange, 방향 오차 8° 이하를 확인하고 즉시 결과를 확정한다. 확정 뒤 타겟 이동이나 사망은 이미 적용된 결과에 영향을 주지 않는다. 트레이서는 표현일 뿐 판정을 지연하지 않는다.

### U-IMPACT-PROJECTILE. 투사체 착탄

ProjectileImpact는 발사 시 타겟·사거리·방향을 검증해 서버 권위 발사체를 생성한다.

- **LockedPoint:** 발사 시 ImpactPoint와 AimDirection을 고정한다. Single은 Impact 때 원래 타겟이 살아 있고 프로필의 `ImpactHitRadius` 안에 있을 때만 적중한다. Area는 원래 타겟의 이동·사망과 무관하게 권위 착탄점에서 범위를 판정한다.
- **Homing:** 서버가 TargetId를 추적한다. 타겟 사망·Despawn 시 빗나가며 다른 타겟으로 자동 이전하지 않는다.
- 발사 후 공격자가 사망해도 이미 생성된 발사체는 프로필에 별도 취소 규칙이 없는 한 계속 진행한다.

### U-IMPACT-TRAVELING. 이동 영역

TravelingArea는 발사 후 서버의 독립 영역으로 진행한다. 공격자나 최초 타겟 사망으로 취소하지 않으며, 각 대상의 첫 접촉 시 결과를 확정한다. 같은 영역이 같은 대상에 여러 번 적용되지 않도록 대상별 접촉 기록을 가진다.

### U-IMPACT-FRIENDLY. 아군과 사망 대상

공격 프로필이 명시적으로 아군 회복을 제공하지 않는 한 아군에게 피해·상태 이상을 적용하지 않는다. 사망 대상에는 새 결과를 적용하지 않는다.

---

## 6. 공격 시간과 쿨다운

### U-ATK-TIMELINE. 권위 AttackTimeline

서버가 읽는 검증된 AttackTimeline 또는 AttackProfile이 Windup, ActionMarkerOffset, Recovery의 정규 원본이다.

- `OnAttackHit` Animation Event는 표현·에셋 검증용이며 서버 결과를 발생시키지 않는다.
- 완성 유닛의 이벤트와 권위 `ActionMarkerOffset`은 1 animation frame 이내로 일치해야 한다.
- MeleeContact·Hitscan의 결과 Impact와 ProjectileImpact·TravelingArea의 Launch·Activation은 오름차순 `ActionMarkerOffset`과 고유 HitIndex를 가진다.
- ProjectileImpact의 착탄 `ResultImpactTime`과 TravelingArea의 접촉 시각은 서버 시뮬레이션 결과이며 ActionMarkerOffset과 분리한다.
- 필수 데이터가 없거나 불일치하면 완성 상태로 판정하지 않는다.
- 임시 폴백은 미완성 유닛에만 허용하며 출시 검증을 통과할 수 없다.

### U-ATK-COOLDOWN. 일반 공격 주기

- 일반 AttackCooldown은 Windup 커밋부터 다음 Windup 커밋 가능 시점까지의 전체 주기다.
- Align 시간은 AttackCooldown에 포함하지 않는다.
- 커밋과 동시에 쿨다운을 소비하며 이후 빗나감·취소에도 환불하지 않는다.
- 모든 MeleeContact·Hitscan Impact marker 및 ProjectileImpact·TravelingArea Launch/Activation marker는 `0 ≤ ActionMarkerOffset < AttackCooldown`을 만족해야 한다. 발사 후 비행·영역 진행으로 생기는 ResultImpactTime은 이 제한을 받지 않는다.
- MultiImpact도 공격 회차당 쿨다운을 한 번만 소비한다.
- Projectile 비행 시간은 공격 주기와 독립적이며 이전 발사체 비행 중에도 쿨다운이 끝나면 다음 회차를 시작할 수 있다.
- 다음 행동 가능 시각은 쿨다운 종료와 Recovery 종료 중 늦은 시각이다.

### U-ATK-COOLDOWN-BLOOM. BloomFairy 예외

BloomFairy는 기존 확정 의도를 유지한다.

```text
Align 완료 → Windup 1.0초 → HoT 부여 Impact → 쿨다운 3.0초
성공 공격의 총 주기 = 4.0초
```

Impact 전에 타겟이 무효화되면 발동 후 3초 쿨다운은 시작하지 않지만, 현재 시전 애니메이션의 취소 Recovery가 끝날 때까지 새 시전을 시작하지 않는다.

---

## 7. 피해·회복·지속 효과

### U-EFFECT-AUTH. 결과 적용

피해, 회복, 상태 효과와 사망은 서버 Impact에 적용한다. 클라이언트는 복제된 현재 HP로 즉시 수렴한다. 표현 시점과 상관관계는 `GameSystemRules_UnitCombatSynchronization.md`를 따른다.

### U-EFFECT-AOE. 범위 결과

범위 공격은 권위 AimDirection 또는 ImpactPoint를 중심으로 XZ 월드 좌표에서 대상을 먼저 수집한 뒤 결과를 적용한다. 순회 중 사망이나 컬렉션 변경을 피하기 위해 판정 대상 목록을 확정한 후 적용한다.

같은 Impact의 여러 피해자는 동일 AttackSequenceId와 HitIndex를 공유한다. 공격자별 FIFO 전체 방출을 사용하지 않는다.

### U-EFFECT-TIMED. HoT·DoT

- 서버가 효과 인스턴스와 틱 시각을 관리한다.
- 같은 대상의 같은 종류 효과 재적용은 기본적으로 남은 시간·총량·틱 상태를 갱신하며 중첩하지 않는다.
- Periodic은 명시된 간격마다 적용하고, 연속형은 누적 목표량과 실제 적용량의 차이를 적용해 프레임 오차를 누적하지 않는다.
- 각 틱은 효과 인스턴스 ID와 TickIndex로 한 번만 적용한다.
- 대상 사망, 효과 완료 또는 지속 시간 만료 시 제거한다.

### U-EFFECT-PRESENT. HP 텍스트

- 일반 피해·회복은 각 권위 Impact 결과에 맞춰 표시한다.
- Periodic DoT는 각 틱의 결과를 표시한다.
- 연속형 HoT는 중간 틱 텍스트를 억제하고 정상 종료 시 실제 총 회복량을 한 번 표시한다.
- 대상이 사망한 경우 HoT 종료 텍스트를 표시하지 않는다.

---

## 8. 확정된 특수 유닛 의미

이 섹션은 게임플레이 의미만 정의한다. 현재 구현 감사 상태는 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`를 따른다.

### U-SPECIAL-BATTLEAXE. BattleAxe

- Delivery: MeleeContact
- TargetScope: Area, AreaShape: Cone
- Effect: Damage, Schedule: Instant
- 공격자 권위 위치와 AimDirection을 기준으로 XZ 거리 `sweepReach` 및 반각 `sweepArcHalfAngle` 안의 적 유닛에 동일 피해를 적용한다.
- 주 타겟 직접 피해와 범위 피해가 중복 적용되지 않게 한다. 건물은 주 타겟 직접 피해만 받는다.

### U-SPECIAL-TORRENT. TorrentSpirit

- Delivery: TravelingArea
- TargetScope: Area, AreaShape: Rectangle
- Effect: 적 유닛·건물 Damage + 아군 유닛 Heal
- Schedule: ContactOncePerTarget
- 파도는 서버가 전선을 이동시키며 대상별 첫 접촉에 한 번만 결과를 적용한다.

### U-SPECIAL-MUSHROOM. MushroomBomber

- 목표 Delivery: ProjectileImpact, LockedPoint
- TargetScope: Area, AreaShape: Circle
- Effect: Damage
- Schedule: 주 타겟 직접 Instant + 범위 적 유닛 ImpactThenPeriodic DoT
- 서버 발사체의 권위 착탄점에서 원형 반경 DoT 부여를 확정한다. 주 타겟 직접 피해는 대상이 살아 있고 프로필의 `ImpactHitRadius` 안에 있을 때만 적용한다.
- 현재 Legacy 런타임은 `BlastAttackBehavior`가 ActionMarker 시점의 주 타겟 현재 위치를 중심으로 적 유닛에게 DoT를 부여한다. 서버 비행·착탄 시뮬레이션과 고정 `ImpactPoint`가 없으므로 **v2 마이그레이션 미완료**다.

### U-SPECIAL-QUAKE. QuakeSpirit

- 목표 Delivery: MeleeContact 기반 Ground Impact
- TargetScope: Area, AreaShape: Circle
- Effect: Damage, Schedule: Instant
- 주 타겟은 직접 100%, 주 타겟을 제외한 반경 내 적 유닛·적 건물은 50% 올림 피해를 받는다. 아군은 피해를 받지 않는다.
- Legacy 런타임의 `QuakeAttackBehavior`는 ActionMarker 시점의 주 타겟 현재 위치를 중심으로 이 판정을 수행하며 Host/Client 피해 결과는 로그 검증됐다.
- 이 Legacy 판정은 권위 `ImpactPoint`, `AttackSequenceId + HitIndex`, 검증된 Animation marker를 가지지 않는다. 기본 Attack 클립의 `OnAttackHit`도 미주입이고 `hitFrameTimes=1.0s`는 placeholder이므로 **Incomplete + MigrationRequired**다.

### U-SPECIAL-BLOOM. BloomFairy

- 목표 Delivery: Hitscan cast
- TargetScope: Single
- Effect: Heal, Schedule: Periodic HoT
- 같은 팀의 살아 있는 부상 유닛을 대상으로 하며 본인도 대상이 될 수 있다.
- 우선순위는 손실 체력 비율 내림차순, 동률이면 거리와 안정 EntityId 순이다.

### U-SPECIAL-INFERNO. InfernoSpirit

- 목표 Delivery: ProjectileImpact 잠정
- TargetScope: Single
- Effect: 주 타겟 직접 Damage 후 적 유닛에만 Periodic DoT
- Schedule: 직접 Instant + DoT 5/초 × 3초, 1초 discrete tick, 갱신 시 리셋
- Legacy 런타임의 `InfernoAttackBehavior`는 주 타겟 유닛에게만 DoT를 부여하고 건물에는 직접 피해만 적용한다. MushroomBomber와 별도 `ApplyInfernoDot` 진입점을 사용하며 사용자 실기 확인을 완료했다.
- 현재는 발사·착탄 분리와 회차 기반 결과가 없는 공통 타이머 경로이고 설정 1.15s와 실제 marker 0.50s도 불일치하므로 **MigrationRequired + Provisional**이다.

---

## 9. 멀티플레이 표현 계약

### U-PRESENT-SEQUENCE. 회차 기반 표현

공격 애니메이션, 발사, 비행, 착탄, 피격 VFX·SFX·HP 텍스트는 같은 `AttackSequenceId + HitIndex`를 사용한다. 공격자 FIFO, 다음 로컬 타격 프레임 또는 타임아웃에 정상 연출을 연결하지 않는다.

### U-PRESENT-LATE. 지연 처리

미래 Impact는 서버 시각에 예약하고, 이미 지난 결과는 현재 상태를 즉시 수렴한 뒤 핵심 표현을 한 번만 따라잡는다. 네트워크 지연을 숨기기 위해 다른 공격 회차에 결과를 붙이지 않는다.

상세 계약은 `GameSystemRules_UnitCombatSynchronization.md`가 권위다.

---

## 10. 완성도 판정

### U-READINESS-STATUS. 상태 구분

모든 유닛은 다음 중 하나로 표시한다.

- **Complete:** 규칙·AttackProfile·에셋·싱글·멀티 검증 완료
- **MigrationRequired:** 기존 런타임은 동작하지만 규칙 v2 구조로 이전 필요
- **Incomplete:** 필수 공격 로직 또는 에셋 미완성
- **Provisional:** 전달 방식이나 게임 감각의 실기 확인 필요

Animation Event 존재만으로 Complete가 되지 않는다. 구체적인 상태와 근거는 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`에 기록한다.

---

## 11. Legacy 규칙 매핑

2026-07-20 이전 번호는 과거 작업 문서 참조를 위해 아래 별칭으로 보존한다. 아래 번호는 현행 권위가 아니다.

| Legacy | 상태 | 현행 규칙 |
|---:|---|---|
| 1~6, 11, 14, 16 | 의미 유지·정제 | `U-AUTH-*`, `U-COORD-*`, `U-MOV-PATH`, `U-COMBAT-PHASE`, `U-EFFECT-*` |
| 7, 8, 12, 15 | 대체 | `U-MOV-ALIGN`, `U-ATK-ALIGN` |
| 9 | 대체 | `U-TARGET-RANGE` |
| 10, 13 | 대체 | `U-MOV-PHASE`, `U-COMBAT-PHASE`, `U-TARGET-SELECT`, `U-TARGET-HOLD` |
| 17 | 대체 | `U-ATK-TIMELINE` |
| 18 | 원칙 유지·구현 세부 제거 | `U-AUTH-SERVER`, `U-ATK-TIMELINE` |
| 19, 26 | 폐기 | `U-PRESENT-SEQUENCE`, `NET-ACTION-SEQ`, `NET-PRESENT-003` |
| 20 | 대체 | `U-COMBAT-DELIVERY`, `U-COMBAT-AXES` |
| 21 | 폐기 | 값 기반 행동 스냅샷으로 대체 |
| 22 | 확장 대체 | `NET-ACTION-STATE`, `NET-ACTION-SEQ` |
| 23~25 | 게임 의미만 유지 | `U-EFFECT-AOE`, `U-SPECIAL-*`; 클래스·파일은 TDD |
| 27, 31 | 에셋 상태 분리 | `UnitCombatAssetMatrix.md` |
| 28~43 | 게임 의미 정제 | `U-SPECIAL-*`, `U-EFFECT-TIMED`, `U-ATK-COOLDOWN-BLOOM` |

기존 FIFO, 모든 원거리 트레이서, 50ms 전투 Tick, 클래스·메서드·파일명은 게임플레이 규칙이 아니라 Legacy 구현 기록이다.
