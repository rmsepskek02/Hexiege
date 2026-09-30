# Tracer C 공격 타겟·공격 방향 Shadow Plan

이 계획은 서버가 선택한 공격 대상과 유닛이 실제로 바라보는 방향을 하나의 공격 회차로 묶기 위한 첫 구현 단계다. 먼저 Legacy 공격을 그대로 살려 둔 상태에서 신규 회차를 서버가 옆에서 계산하고 Host와 Client에 읽기 전용으로 복제한다. 이 자료로 타겟 변경, 정렬, 다중 타격, 사망·취소 상황의 차이를 모두 확인한 뒤에만 실제 공격 writer를 전환한다. 서버 권위와 기존 아키텍처를 유지하며, 관측 통과를 실제 화면 교정 완료로 오인하지 않는다.

> **기존 로직 처리:** 이번 Phase 4에서는 Legacy 타겟 선택, Root 회전, 쿨다운, 지연 피해, HP, RPC, VFX를 제거하거나 비활성화하지 않는다. 신규 경로는 Shadow 상태와 진단만 만들며 기존 결과의 분기 조건으로 사용하지 않는다. 실제 Legacy writer 제거는 이후 `SequenceAuthoritative` 경기의 사용자 멀티플레이 검증과 문서/메모리 업데이트 승인 전에는 수행하지 않는다.

## 1. 적용 규칙과 선행 문서 교정

### 1.1 현재 유효한 근거

- `GameSystemRules_Units.md` 규칙 13: 처음 선택한 타겟을 유지하고 처치 후 다음 타겟을 선택한다.
- 규칙 14: 공격 중 이동하지 않는다.
- 규칙 15: 공격 중 서버 유닛은 타겟 방향을 점진적으로 유지한다.
- 규칙 17~18: 검증된 HitFrameTimes와 서버 시간축이 타격 시점의 원본이다.
- `NET-ACTION-STATE`: AlignToAttack, Windup, Impact, Recovery를 값으로 구분한다.
- `NET-ACTION-SEQ`: 정렬 완료 후 커밋 시 AttackSequenceId를 발급하고 한 회차의 TargetId와 HitIndex를 묶는다.
- `NET-ACTION-IDEMPOTENT`: 결과와 스냅샷의 중복·오래된 revision을 차단한다.
- `NET-FACING-002`: 회차 타겟 잠금과 타격 방향 잠금을 분리하고 각 Impact의 SimulationFacing을 AimDirection으로 기록한다.
- `NET-DELIVERY-*`: 전달 방식별 서버 판정과 표현 경계를 구분한다.
- `NET-CANCEL-001~005`: 커밋 전 취소, 커밋 후 취소, 공격자·타겟 사망과 순서 역전을 회차 단위로 처리한다.
- 동기화 문서 10장 전환 규칙 1~4: Legacy를 유지한 Shadow 기록·비교·복제를 먼저 수행한다.

### 1.2 구현 전 보완할 상시 규칙

수정 파일:

- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`

기존 규칙 1~44의 번호는 변경하지 않고 다음 안정 ID 본문을 전투 진입·연계 규칙 앞에 추가한다.

1. `U-COMBAT-PHASE`: AcquireTarget → Chase → AlignToAttack → Windup → Impact → Recovery의 의미와 이동 정지 경계.
2. `U-TARGET-COMMIT`: 커밋 전 타겟 변경은 가능하지만 커밋된 회차의 TargetId는 다른 타겟으로 이전하지 않으며, 새 타겟은 새 회차를 사용한다.
3. `U-ATK-ALIGN`: 서버가 위치 이동 0을 보장하고 270°/s 이내로 회전한다. 오차 5° 이하에서만 Windup에 진입하고, 커밋 후 각 Impact는 8° 유지 기준으로 판정한다.
4. `U-ATK-TIMELINE`: 쿨다운과 Windup 시작은 정렬 완료·회차 커밋과 같은 서버 경계이며, 각 HitIndex의 Impact 시각은 검증된 타임라인을 사용한다.
5. `U-IMPACT-TARGETLOCKED`: MeleeContact/Hitscan은 각 Impact에 회차 TargetId의 생존·유효성·사거리·AimDirection을 서버 pose로 재검증한다.
6. `U-IMPACT-LOCKEDPOINT`: LockedPoint는 Launch의 목표점과 방향을 고정하고 이후 현재 타겟 위치로 바꾸지 않는다.
7. `U-IMPACT-INDEPENDENT`: Homing과 TravelingArea는 서버가 별도 생명주기로 진행하며 source 사망 지속 여부를 프로필로 결정한다.

이 문서 교정을 마친 뒤 같은 ID를 코드 주석과 계획에 사용한다. 정의가 없는 ID를 구현 근거로 먼저 사용하지 않는다.

## 2. 구현 범위

### 2.1 순수 계약 회귀를 먼저 추가

수정 파일:

- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- 필요 시 `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionContracts.cs`
- 필요 시 `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionSequencer.cs`

작업:

1. 5° 정렬 전에는 sequence·쿨다운 커밋이 생기지 않는 fixture를 고정한다.
2. 5.000° 진입과 5.001° 거부, 커밋 후 8.000° 적중과 8.001° miss 경계를 검증한다.
3. 커밋 뒤 TargetId 변경 요청이 `ScopeMismatch`이고 새 타겟은 새 sequence에서만 허용되는지 검증한다.
4. 이전 회차의 늦은 Impact와 다음 회차의 타겟·revision이 섞이지 않는지 검증한다.
5. 다중 HitIndex, 공격자 사망, 타겟 사망, range 이탈, duplicate/stale/out-of-order 입력을 fail-closed로 검증한다.
6. 현재 지원하지 않는 Delivery/TargetMode가 성공으로 처리되지 않는지 검증한다.

기존 reducer 계약이 이미 충족하는 항목은 재작성하지 않고 production 경로와 같은 adapter 입력 fixture를 추가한다.

### 2.2 25종 공격 프로필 manifest

수정 후보 파일:

- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- 신규 `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackShadowProfile.cs`
- 신규 `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`

작업:

1. 25개 UnitType을 모두 명시적으로 열거하고 TargetMode, 목표 Delivery, Legacy 실행 방식, HitIndex 수, 특수 결과, 프로필 확정 상태를 값으로 만든다.
2. MeleeContact/Hitscan, 다중 타격, 주 타겟+범위, TravelingArea, Periodic, healer를 서로 다른 분류로 유지한다.
3. 잠정 ProjectileImpact와 미완료 타임라인은 임의로 확정하지 않고 `profile-unresolved`로 기록한다.
4. manifest에서 빠진 UnitType, 중복 매핑, HitFrameTimes와 impact 수 불일치는 self-validation 실패로 처리한다.
5. 미완료 유닛은 전체 진단에서 조용히 제외하지 않는다. 공통 TargetId·Facing 비교 가능 여부와 Delivery별 Impact 비교 불가 이유를 각각 기록한다.

### 2.3 서버 공격 회차 Shadow coordinator

신규/수정 후보 파일:

- 신규 `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionSequencer.cs`

작업:

1. 서버에서 공격자 인스턴스별로 하나의 Shadow sequencer와 단조 sequence allocator를 소유한다.
2. Legacy 타겟 획득 시 `AlignToAttack`을 시작하되 sequence는 0으로 유지한다.
3. 매 서버 frame/tick의 권위 pose에서 현재 Root 방향과 회차 타겟 방향을 계산하고, 위치 이동이 0이며 5° 이하가 된 시점에만 Shadow Commit을 수행한다.
4. Legacy가 그보다 먼저 쿨다운·애니메이션·피해 예약을 시작하면 `legacy-started-before-shadow-aligned` 차이로 기록한다. Legacy 동작은 변경하지 않는다.
5. 커밋 후 Shadow TargetId는 고정한다. `_unitCombatTargets` 또는 표시용 타겟이 바뀌어도 기존 회차를 바꾸지 않고 다음 회차 후보로만 보관한다.
6. 각 Legacy 지연 피해 dispatch 시각에 최신 서버 pose를 캡처해 Shadow `Advance`와 `EvaluateImpact`를 호출한다.
7. Legacy 피해 성공/취소를 Shadow authorization과 비교하되 `ConfirmImpactResult`에 넣는 비교 결과는 진단용이며 실제 HP·사망 이벤트를 만들지 않는다.
8. 공격자 사망, 타겟 사망, StopCombat, Despawn에서 해당 회차를 규칙 순서대로 종료하고 future hit를 폐기한다.
9. 기존 SpearMan 전용 `PoseObservation`은 새 coordinator로 흡수하되, 동일 증거가 새 경로에서 확보될 때까지 기존 코드를 제거하지 않는다. 중복 로그와 이중 sequence 발급은 self-validation에서 막는다.

### 2.4 서버 권위 pose와 방향 비교

수정 후보 파일:

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionPoseContracts.cs`
- 신규 `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`

작업:

1. 기존 `IUnitActionPoseSource`를 읽기 전용으로 사용해 Simulation Root 위치·연속 Facing·타겟 위치를 캡처한다.
2. 타격 AimDirection은 타격 시점의 서버 SimulationFacing에서 만들고, 타겟 벡터와의 yaw 오차를 별도 값으로 기록한다.
3. `UnitData.Facing`의 6방향 값과 Root 연속 방향의 차이를 기록하되, Shadow에서 어느 쪽도 덮어쓰지 않는다.
4. 이전 회차 피해 대상, 현재 표시 타겟, Shadow 회차 타겟이 다른 경우 세 ID를 한 증거에 남긴다.
5. `UnitView.Update`, `BeginServerActionRotation`, `ChangeTarget`의 기존 Root writer는 이번 단계에서 유지한다. Shadow observer가 이 writer의 결과만 읽는다.

### 2.5 Shadow Snapshot 원자 복제

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`
- 신규 또는 공용 `NetworkUnitActionShadowState` 직렬화 값 타입

작업:

1. AttackerInstanceId, AttackSequenceId, Revision, Phase, TargetKind/Id, Delivery, CommitServerTime, SimulationFacing, 마지막 Impact AimDirection과 HitIndex를 하나의 원자적 NetworkVariable 값으로 복제한다.
2. WritePermission은 Server, ReadPermission은 Everyone으로 고정한다.
3. 클라이언트는 이 값을 현재 행동이나 Root에 적용하지 않고 observer에만 전달한다.
4. 오래된 revision, sequence 회귀, 인스턴스 retire 후 ID 재사용을 클라이언트에서 분류해 기록한다.
5. Shadow 복제는 신규 gameplay RPC를 만들지 않으며 기존 Start/Change/Stop RPC를 대체하지 않는다.

### 2.6 진단기와 Android-safe terminal

수정 후보 파일:

- 신규 `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- 필요 시 신규 `Assets/_Project/Scripts/Editor/Combat/RunUnitAttackShadowCrossAudit.cs`

작업:

1. 세션 시작/종료 anchor, role, schema, pipeline mode와 25종 coverage manifest를 기록한다.
2. per-sequence bounded evidence로 Legacy/Shadow target, 정렬 시작·커밋 시각, Impact 시각, Root Facing, AimDirection, 결과 차이를 기록한다.
3. terminal은 전체 줄 UTF-8 preflight를 통과한 compact 필수 필드만 출력하고, 상세 내용은 bounded periodic/evidence 줄로 분리한다.
4. Host/Client 같은 세션·반대 역할 파일을 콘텐츠 anchor로 결합하고 파일명만으로 역할을 추정하지 않는다.
5. 다음을 서로 다른 판정으로 구분한다.
   - 구현 불일치: target mismatch, commit-before-align, impact facing/range mismatch, stale/reused identity, client write.
   - 프로필 미완료: unresolved delivery/timeline/asset.
   - 증거 부족: 미관측 UnitType, terminal 절단, session pair 불완전.
6. 진단 실패가 Legacy 공격 결과를 중단시키지 않으며, 예외는 bounded 로깅 후 Shadow만 fail-closed 한다.

## 3. 파일별 예상 변경

| 파일 | 변경 목적 |
|---|---|
| `GameSystemRules_Units.md` | 누락된 공격 단계·정렬·커밋·Impact 안정 ID를 정식 규칙으로 추가 |
| `UnitActionContracts.cs` | 필요한 경우 Shadow snapshot/profile의 순수 값 계약 보강 |
| `UnitActionSequencer.cs` | production adapter가 요구하는 정렬·Impact·취소 회귀 보강 |
| `UnitAttackShadowProfile.cs` | 25종 공격 의미와 확정 상태의 Application 계약 |
| `UnitAttackShadowProfileResolver.cs` | 현재 설정·유닛 타입을 계약으로 변환하는 Infrastructure adapter |
| `UnitAttackShadowCoordinator.cs` | 서버 per-attacker 회차 Shadow 생명주기 |
| `NetworkCombatController.cs` | Legacy 획득·예약·dispatch·사망 이벤트를 coordinator에 전달 |
| `UnitView.cs` / `UnitActionPoseContracts.cs` | 기존 서버 pose의 읽기 전용 캡처 seam 사용·보강 |
| `NetworkUnit.cs` | 원자적 Shadow snapshot 서버→클라이언트 복제 |
| `UnitAttackShadowObserver.cs` | target/facing/result 차이와 bounded terminal 진단 |
| `RunUnitActionSelfValidation.cs` | pure 경계, manifest, 직렬화, lifecycle, formatter 회귀 |
| `UnitCombatAssetMatrix.md` | 25종 profile/Shadow 관측 가능 범위와 미완료 사유 동기화 |

파일명과 분리는 구현 전 실제 assembly 의존성을 다시 확인해 조정할 수 있다. Application은 Unity/NGO를 참조하지 않고, Infrastructure가 NetworkVariable과 런타임 adapter를 소유한다.

## 4. 위험 요소와 대응

| 위험 | 대응 |
|---|---|
| Shadow가 실제 공격 결과에 영향을 줌 | coordinator 반환값을 Legacy 분기 조건으로 사용하지 않고 writer 호출 수 0을 self-validation과 runtime sentinel로 검증한다. |
| Legacy 표시 타겟과 과거 회차 타겟 혼동 | AttackerInstanceId+SequenceId에 TargetId를 고정하고 current display target을 별도 비교 필드로 기록한다. |
| 25종 중 쉬운 유닛만 PASS | 전체 UnitType manifest를 terminal 예약 공간까지 포함해 선검증하고 미관측·미확정·불일치를 별도 카운터로 남긴다. |
| 잠정 원거리 공격을 잘못 확정 | 현재 Legacy 실행 방식과 목표 Delivery를 분리하고 미확정 상태에서는 공통 target/facing만 감사한다. |
| NetworkVariable 여러 필드가 서로 다른 frame 상태를 보임 | 하나의 직렬화 값과 Revision으로 원자 복제한다. |
| 로그 폭증과 Android 절단 | 공격별 전체 snapshot을 반복 출력하지 않고 bounded divergence/evidence와 compact terminal을 사용한다. |
| 기존 B3 Root writer와 충돌 | Shadow는 Root를 쓰지 않는다. 실제 공격 writer 전환은 별도 match-fixed Authority 단계에서만 수행한다. |
| 기존 SpearMan Shadow와 이중 sequence | 새 coordinator가 동등 증거를 낼 때까지 legacy observer를 격리하고, 동시에 둘이 활성화되면 FAIL로 처리한다. |
| 미완료 유닛 때문에 전체 작업이 무한 대기 | 구현 오류와 profile/asset 미완료를 분리한다. Phase 4 공통 계약 PASS와 유닛별 미완료 목록을 동시에 보고하며 Complete 표시는 하지 않는다. |

## 5. 자동 검증 완료 조건

1. Unity C# 컴파일 오류 0건.
2. `RunUnitActionSelfValidation`에서 A1~B3 기존 항목과 신규 Tracer C 항목 모두 PASS.
3. 5°/8° 경계, target switch, 다중 hit, 사망·취소, duplicate/stale/out-of-order 회귀 PASS.
4. 25개 UnitType manifest 누락·중복 0건이며 미확정 profile은 명시적으로 보고됨.
5. Shadow의 Root/UnitData/HP/cooldown/Animator/NetworkTransform/RPC/VFX write 0건.
6. 서버 snapshot revision과 클라이언트 수신 revision이 단조 증가하고 ID retire/reuse 회귀 PASS.
7. Android terminal 최악값 전체 줄 UTF-8 preflight PASS.

## 6. 후속 멀티플레이 검증 게이트

이 항목은 구현 승인 후 별도 사용자 테스트 단계에서 수행한다.

1. Android와 Editor의 Host/Client 역할을 교대해 같은 빌드 리비전으로 각 1경기 이상 진행한다.
2. 우선 표본은 단일 근접, 다중 근접, Hitscan, 주 타겟+AoE의 4종으로 한다.
3. 첫 게이트가 통과하면 25종 coverage manifest를 채운다. 한 경기에서 양 팀에 나온 동일 UnitType은 1종으로 계산한다.
4. 빠른 타겟 사망과 다음 타겟 전환, 이동 중 전투 진입, 다중 타격 도중 타겟 사망, 건물 공격을 포함한다.
5. Host/Client의 sequence·target·phase·revision이 일치하고 client writer가 0인지 확인한다.
6. Legacy와 Shadow의 차이는 다음 네 범주로 집계한다: 정렬 전 시작, target mismatch, Impact 방향/사거리 mismatch, 취소 lifecycle mismatch.
7. 프로필이 확정되지 않은 유닛은 공통 target/facing 증거와 unresolved 이유를 함께 남기고 규칙 v2 Complete로 올리지 않는다.

## 7. 완료 판정과 다음 단계

Phase 4 완료는 다음만 의미한다.

- 서버 공격 회차 Shadow가 25종을 누락 없이 분류한다.
- Legacy와 신규 target/facing/Impact 차이를 회차 단위로 재현·설명할 수 있다.
- Host와 Client가 동일한 Shadow snapshot을 받는다.
- 신규 경로가 실제 gameplay writer를 전혀 추가하지 않는다.

Phase 4 완료는 “화면의 공격 방향이 고쳐졌다” 또는 “피해 시점이 고쳐졌다”는 뜻이 아니다. 다음 순서는 결과 키 기반 Presentation Shadow/전환(Phase 5), 그 후 경기 시작 시 고정한 `SequenceAuthoritative`에서 타겟·방향·Impact·피해 writer를 함께 전환하는 Phase 6이다. 실제 교정 완료 선언은 이 권위 전환과 25종 역할교대 멀티플레이 검증 이후에만 한다.

## 8. 이번 구현에서 제외

- Legacy 피해량·방어력·특수 효과 수치 변경
- 공격 애니메이션·VFX·HP 텍스트 emitter 전환
- 실제 Projectile 궤적과 착탄 시스템 구현
- 미완료 Attack 클립·Animation Event·VFX 에셋 제작
- 유닛 이동 B3 재설계 또는 NetworkTransform 설정 변경
- 경기 중 pipeline mode 변경
