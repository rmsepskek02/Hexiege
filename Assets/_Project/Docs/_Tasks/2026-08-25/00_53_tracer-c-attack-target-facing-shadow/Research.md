# Tracer C 공격 타겟·공격 방향 Shadow Research

이 작업은 화면에서 유닛이 바라보는 대상과 서버가 실제로 피해를 주는 대상이 달라질 수 있는 구조를 먼저 정확히 드러내기 위한 조사다. 바로 회전이나 피해 코드를 교체하지 않고, 서버가 선택한 타겟·공격 회차·현재 방향·타격 순간의 조준 방향을 하나의 읽기 전용 Shadow 흐름으로 계산해 기존 결과와 비교한다. 이동 B3에서 확립한 서버 권위와 Simulation Root 단일 writer는 유지하며, 이 단계의 결과를 실제 공격 권위 전환의 선행 증거로 사용한다.

## 1. 조사 범위와 결론

- 현재 멀티플레이 공격은 서버가 타겟과 피해를 결정하므로 큰 틀의 서버 권위는 유지되고 있다.
- 그러나 타겟 선택, Root 회전, 도메인 Facing, 쿨다운 시작, 지연 타격이 서로 다른 위치와 시간에 갱신된다.
- 그 결과 한 회차의 피해 대상과 화면이 추적하는 대상이 달라지거나, 아직 공격 방향 정렬이 끝나지 않았는데 공격 애니메이션과 쿨다운이 시작될 수 있다.
- 기존 `UnitActionSequencer`는 5° 진입, 8° 유지, 회차별 TargetId 고정과 Impact별 AimDirection 판정 계약을 이미 보유하지만, 현재 런타임에서는 SpearMan 단일 타격 관측에만 제한적으로 사용한다.
- 따라서 다음 안전한 단계는 Legacy 결과를 유지한 채 25종 전체를 누락 없이 분류하고, 공격 회차 Shadow를 서버에서 계산·복제·교차 진단하는 것이다.

이 단계는 **관측과 계약 검증 단계**다. Shadow가 통과해도 실제 화면의 공격 방향이나 피해 적용 시점이 교정됐다고 선언하지 않는다. 실제 타겟·방향·Impact·피해 writer 전환은 경기 단위 `SequenceAuthoritative` 전환에서 함께 수행해야 한다.

## 2. 현재 Legacy 공격 흐름

### 2.1 타겟 선택

- `UnitCombatUseCase.TryFindTarget`은 서버에서 쿨다운이 끝났을 때 공격 사거리 내 타겟을 찾는다.
- `NetworkCombatController.TickCombat`은 `_unitCombatTargets`에 현재 타겟을 보관하고, 기존 타겟이 살아 있고 사거리 안이면 더 가까운 적이 나타나도 기존 타겟을 유지한다.
- 타겟이 바뀌면 `ChangeTargetClientRpc`로 표시용 타겟을 갱신한다.
- 공격 사거리 진입 직후의 첫 타겟은 `UnitView`의 전투 진입 코루틴과 `OnUnitEnteredCombatHandler`에서도 별도로 조회된다.

서버에서만 타겟을 고르는 원칙은 맞지만, 같은 타겟 결정을 나타내는 값이 `_unitCombatTargets`, `UnitView`의 `_combatTargetId/_combatTargetTransform`, 지연 피해 코루틴 인자에 나뉘어 있다. 이 값들을 하나의 revision과 회차로 원자적으로 묶는 상태는 아직 없다.

### 2.2 공격 방향

- 공격 사거리 진입 시 `UnitView.BeginServerActionRotation`은 이동 소유권을 Action으로 넘기고 타겟 방향으로 Simulation Root를 즉시 스냅한다.
- 이후 서버 `UnitView.Update`가 타겟 Transform의 현재 위치를 매 프레임 다시 읽어 270°/s로 Root를 회전시킨다.
- 순수 클라이언트는 Root를 쓰지 않고 NetworkTransform으로 서버 회전을 받으므로 클라이언트 이중 writer는 현재 차단돼 있다.
- 실제 피해 적용 시 `UnitCombatUseCase.ExecuteAttack`은 공격자와 타겟의 `HexCoord`로 6방향 `HexDirection`을 다시 계산해 `UnitData.Facing`을 덮어쓴다.

즉 연속 월드 방향인 Simulation Root 회전과 타일 기반 6방향 도메인 Facing이 같은 타격의 단일 AimDirection으로 확정되지 않는다. Root는 타겟을 연속 추적하지만 피해 경계의 Facing은 별도의 이산 방향으로 다시 계산된다.

### 2.3 공격 회차와 타격

- `NetworkCombatController.ExecuteAttack`은 공격 정렬 완료 여부와 무관하게 쿨다운을 즉시 소비하고 `HitFrameTimes`마다 지연 피해 코루틴을 예약한다.
- 지연 피해는 예약 당시의 TargetId를 유지한다. 공격자 또는 타겟이 사망하면 취소되지만 타겟이 사거리 밖으로 나가도 현재 Legacy 규칙상 피해를 적용한다.
- 같은 동안 표시용 `_combatTargetId`는 다음 타겟으로 변경될 수 있다. 그러면 이전 회차의 지연 피해는 옛 타겟에 적용되는데 Root와 트레이서 표현은 새 타겟을 향할 수 있다.
- SpearMan에만 연결된 기존 A0/A2 Shadow는 `AttackSequenceId`와 pose schedule/dispatch를 기록하지만, 공격 시작 전에 실제 AlignToAttack 시간을 진행하지 않고 Legacy 예약 시점에 Begin과 Commit을 연속 시도한다.

이 구조가 사용자가 제시한 “공격 타겟과 공격 방향 불일치”를 만들 수 있는 직접 경로다. 특히 **회차 타겟 고정과 표시용 추적 타겟의 생명주기가 분리되지 않은 점**이 핵심이다.

## 3. 규칙 대조

### 3.1 충족 중인 규칙

- `GameSystemRules_Units.md` 규칙 14: 공격 루프 동안 위치 이동을 멈춘다.
- `GameSystemRules_UnitCombatSynchronization.md`의 서버 권위 원칙: 순수 클라이언트는 Simulation Root와 피해를 쓰지 않는다.
- `NET-ACTION-SEQ`: `UnitActionSequencer` 자체는 커밋 시에만 AttackSequenceId를 발급할 수 있다.
- `NET-ACTION-IDEMPOTENT`: 회차·HitIndex·피해자·효과 ordinal을 묶는 결과 키 계약이 이미 존재한다.
- `NET-CANCEL-003~005`: reducer에는 공격자 사망, 커밋 후 취소, 오래된 결과를 구분할 상태가 존재한다.

### 3.2 미충족 또는 런타임 미연결 규칙

- 규칙 15와 `NET-FACING-002`: 서버 Root가 타겟을 추적하더라도 타격 순간의 SimulationFacing이 권위 AimDirection으로 결과에 고정·복제되지 않는다.
- `NET-ACTION-SEQ`: Legacy 쿨다운과 피해 예약은 5° 정렬 완료 전에 시작될 수 있고, 실제 런타임 대부분은 AttackSequenceId가 없다.
- `NET-FACING-002`: 클라이언트 표현이 과거 타격의 AimDirection을 결과에서 읽지 않고 현재 타겟 Transform을 다시 읽는다.
- `NET-CANCEL-004`: 지연 피해 TargetId는 유지되지만 표시용 타겟은 새 타겟으로 바뀔 수 있어 한 회차의 판정과 표현이 분리된다.
- `NET-DELIVERY-*`: 현재 다수 원거리 공격은 서버 발사·착탄 상태 없이 `TimerImpact`로 처리되므로 ProjectileImpact 또는 LockedPoint 의미를 완료했다고 볼 수 없다.

### 3.3 규칙 문서의 공백

기존 전체 계획은 `U-COMBAT-PHASE`, `U-TARGET-COMMIT`, `U-ATK-ALIGN`, `U-ATK-TIMELINE`, `U-IMPACT-*`를 근거로 참조한다. 그러나 현재 상시 규칙 문서에는 이 안정 ID의 정식 본문이 없고, `U-MOV-ALIGN`에서 `U-ATK-ALIGN`을 참조하는 문장만 남아 있다.

코드에 5°/8°가 들어 있다는 사실만으로 규칙이 완성됐다고 볼 수 없다. 구현 전에 `GameSystemRules_Units.md`에 공격 단계, 커밋, 정렬, 타격 판정의 의미를 기존 규칙 번호를 재배열하지 않는 별도 안정 ID로 명문화해야 한다.

## 4. 유닛별 적용 가능성

`UnitCombatAssetMatrix.md` 기준 25종은 하나의 Delivery로 취급할 수 없다.

- 바로 비교 가능한 TargetLocked 직접 공격: MeleeContact와 Hitscan 계열, 단일·다중 타격.
- 복합 결과: BattleAxe, QuakeSpirit처럼 주 타겟과 범위 결과를 함께 내는 공격.
- 독립 진행: TorrentSpirit TravelingArea, InfernoSpirit의 후속 Periodic 효과.
- 목표 프로필이 잠정인 공격: Tank, CannonCart, StreamSpirit, FoxMagician, EagleArcher 등의 ProjectileImpact 후보.
- 미완료 에셋/타임라인: QuakeSpirit, RhinoBreaker, MushroomBomber, BloomFairy 등.

Shadow 구현은 25종을 모두 manifest에 포함해야 하지만, 미완료 유닛을 임의로 Hitscan이나 Projectile로 확정하면 안 된다. 공통 TargetId·방향·회차는 관측하되 Delivery별 판정이 확정되지 않은 유닛은 `profile-unresolved` 또는 `delivery-comparison-not-auditable`로 명시해 누락과 미완료를 구분해야 한다.

## 5. 유지해야 하는 아키텍처 불변식

- 타겟 선택, 회차 판정, 방향 판정과 Shadow 복제는 서버에서만 수행한다.
- Shadow는 Simulation Root, `UnitData.Facing`, HP, 쿨다운, Animator, NetworkTransform, Legacy 타겟 사전, RPC, VFX를 쓰지 않는다.
- 클라이언트는 복제된 Shadow 상태를 읽고 진단만 하며 현재 타겟 위치에서 권위 방향을 재계산하지 않는다.
- B3의 이동→Action Root 소유권 경계와 경기 단위 movement pipeline mode는 변경하지 않는다.
- 동일 경기에서 Legacy와 신규 공격 writer를 유닛별로 섞지 않는다.
- 실제 권위 전환 전에는 Legacy 피해와 표현 결과가 경기 결과의 유일한 권위다.
- Shadow 상태는 유닛 ID 재사용을 구분하는 `AttackerInstanceId`, 단조 `AttackSequenceId`, revision을 사용한다.

## 6. 진단이 답해야 할 질문

1. Legacy가 쿨다운과 첫 공격을 시작한 시점에 신규 상태는 실제로 5° 안에 있었는가?
2. 한 Legacy 공격 예약부터 각 타격까지 TargetId가 Shadow 회차 TargetId와 같은가?
3. 각 타격 시점의 서버 SimulationFacing과 타겟 방향 오차는 8° 이내인가?
4. Legacy 피해 대상과 Shadow Impact 대상이 같은가?
5. 이전 회차의 지연 타격이 남아 있는 동안 표시용 타겟이 다음 타겟으로 바뀌었는가?
6. Host와 Client가 같은 AttackerInstanceId·SequenceId·Revision·TargetId·Phase를 받았는가?
7. 25종 중 관측되지 않은 유닛과 프로필이 미확정인 유닛이 조용히 PASS 처리되지 않았는가?

## 7. 영향 범위

| 단계 | 영향 |
|---|---|
| A1 | 기존 계약·reducer를 재사용하고 공격 정렬·Impact 회귀를 확장한다. |
| A2 | 서버 Pose seam을 25종 공격 Shadow 입력으로 일반화한다. |
| B1 | Simulation/Visual Root와 프리팹 구조를 변경하지 않는다. |
| B2/B3 | 이동 reducer와 서버 단일 writer를 유지하고 이동→공격 경계만 관측한다. |
| Tracer C Phase 4 | 이번 작업 범위. 서버 회차 Shadow 계산·복제·Legacy 비교를 구현한다. |
| Tracer C Phase 5 | 결과 키 기반 표현 전환. 이번 범위 밖이다. |
| Tracer D Phase 6 | 타겟·방향·Impact·피해의 단일 권위 writer 전환. 이번 범위 밖이다. |

## 8. 조사 결론

현재 문제는 단순한 회전 보간 수치가 아니라 **공격 회차가 없는 상태에서 서로 다른 타겟·방향 표현을 여러 경로가 갱신하는 구조**다. 즉시 `UnitView.Update`만 고치면 이전 회차 피해와 다음 타겟 표현의 불일치를 해결하지 못하고, 피해 직전 `UnitData.Facing` 덮어쓰기도 남는다.

먼저 상시 규칙의 빠진 공격 안정 ID를 명문화하고, 서버 회차 Shadow를 25종 manifest와 함께 연결해야 한다. Shadow에서 TargetId·정렬·Impact AimDirection·취소·복제 차이를 수치로 확보한 뒤에만 경기 단위 `SequenceAuthoritative` 전환으로 실제 writer를 교체한다.
