# C1 공격 연속 표현·회차 결속 교정 Plan

이 계획은 공격 사거리 안에 적이 계속 있는 동안 유닛이 Attack 애니메이션을 유지하며 새 타겟 방향으로 회전하도록 복원한다. 타겟 변경 순간 걷기 자세를 끼워 넣지 않되, 새 타겟의 실제 타격·효과·피해는 서버가 정지와 방향 정렬을 확인한 새 공격 회차에서만 허용한다. 신규 애니메이션 에셋 없이 기존 Attack 클립, 서버 권위와 현재 Legacy 피해 아키텍처를 보존한다.

> **기존 로직 처리:** Legacy 피해·특수 공격·HP·NetworkTransform writer는 삭제하거나 신규 Sequencer 권위로 교체하지 않는다. 타겟 교체 시 `Held(Walk 첫 프레임)`를 강제하던 분기만 역할상 잘못된 전이로 제거하고, 초기 전투 진입·막힘·재경로에서 이동 정지 표현으로 쓰이는 Held는 보존한다. 사용자 멀티플레이 검증 전에는 Attack 표현 정책을 한 경계에서 되돌릴 수 있도록 순수 정책과 호출부를 분리한다.

> **2026-08-27 v4 FAIL 교정 추가:** 첫 실기에서 표현 연속성 자체와 별개로 공격 회차 복제 순서와 커밋 예약 생명주기 결함이 확인됐다. 이번 Round는 진단 기준을 완화하지 않는다. 서버 게시와 Client 분류를 `(AttackSequenceId, Revision)` 계약 하나로 통합하고, 표시 `StopCombat`이 아직 Impact가 남은 커밋 예약을 선취소하지 못하게 교정한다. Shadow는 계속 `gameplayWrites=0`이며 Legacy 피해 writer를 교체하지 않는다.

## 0. v4 실기 FAIL 교정 계획

### 0-1. 회차 범위 단조 비교기 통합

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

구현:

1. 공격자 인스턴스와 NetworkObject 수명을 먼저 고정한다.
2. 양수 sequence가 증가하면 revision이 새 회차에서 다시 시작해도 수락한다.
3. 같은 양수 sequence에서만 revision stale·duplicate·증가를 판정한다.
4. 더 작은 양수 sequence는 revision이 커도 `SequenceRegression`으로 거부한다.
5. `SequenceId=0` neutral phase는 다음 후보 생명주기로 수락하되 마지막 양수 sequence 상한을 보존한다.
6. 서버 게시 가드와 Client classifier가 동일한 순수 분류 결과를 사용하여 한쪽만 수락하는 분기를 없앤다.
7. 거부 증거에는 이전 수락 sequence/revision/phase와 incoming 값을 함께 기록한다.

근거: `U-COMBAT-PHASE`, `U-TARGET-COMMIT`, `NET-ACTION-SEQ`,
`NET-ACTION-IDEMPOTENT`, `NET-CANCEL-005`.

### 0-2. 커밋 예약과 표시 생명주기 분리

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

구현:

1. coordinator가 공격자별로 아직 미결정 Impact가 남은 현재 커밋 예약을 식별한다.
2. 표시 타겟 제거·다음 후보 탐색의 `StopCombat`은 그 예약 Sequencer를 취소하지 않는다.
3. 예약은 Schedule 당시 TargetId·SequenceId를 그대로 보유하고 Impact 순간 조건을 재검증한다.
4. 기존 타겟의 Impact가 끝난 뒤에만 예약을 retire하며 새 타겟은 독립된 더 큰 sequence를 사용한다.
5. 공격자 사망·despawn처럼 Legacy 예약 자체가 폐기되는 경계는 기존처럼 예약과 coordinator를 함께 retire한다.
6. Shadow는 Target 사망·유효성 상실·사거리 이탈·8도 초과를 같은 예약 HitIndex의 Miss로 관측한다. 이번 Round에서 이 Shadow 결과를 Legacy gameplay writer의 신규 권위 gate로 사용하지 않으며, delivery별 Legacy 적용 조건 변경은 별도 규칙·마이그레이션 범위로 남긴다.

근거: `U-TARGET-COMMIT`, `U-ATK-ALIGN`, `U-IMPACT-TARGETLOCKED`,
`NET-CANCEL-002~005`.

### 0-3. 선행 회귀 fixture와 완료 조건

1. `sequence 1 / revision 4 → neutral → sequence 2 / revision 3 → sequence 2 / revision 4`를 서버와 Client 모두 수락한다.
2. sequence 2 이후 늦은 sequence 1, 같은 회차 stale revision과 다른 payload의 같은 revision은 거부한다.
3. 표시 target이 `-1`이 되어도 커밋 대상이 Impact 조건을 통과하면 Shadow `AuthorizedHit`과 Legacy `Applied`가 일치한다.
4. 같은 형태에서 타겟 사망·사거리 이탈·8도 초과 Shadow Miss가 예약 TargetId·HitIndex에 귀속되고, Legacy 결과와 다르면 terminal이 이를 숨기지 않고 FAIL로 보고한다. 이번 Round의 완료를 위해 Legacy 적용 규칙을 임의 변경하지 않는다.
5. 다중 타격의 이전 예약과 새 타겟 회차가 겹쳐도 각 예약의 TargetId·HitIndex가 섞이지 않는다.
6. 전체 `Run Unit Action Self Validation`이 PASS일 때만 `Build And Run`을 실행한다.
7. 새 멀티 terminal은 Host `targetMismatches=0`, Client `clientRejected=0`, 양측 `verdict=PASS`여야 한다.

규칙 문서의 현재 계약이 이미 위 교정을 요구하므로 이번 Round에서 규칙 의미를 바꾸지 않는다. 실제 구현과 Task/QA 기록만 규칙에 수렴시킨다.

## 1. 규칙 문서 교정

수정 파일:

- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`

구현:

1. `U-COMBAT-PHASE`와 `U-ATK-TIMELINE`에 서버 공격 회차 단계와 Animator의 연속 Attack 표현 상태가 동일 개념이 아님을 명시한다.
2. 공격 사거리 안에 유효한 후보가 계속 있으면 타겟 교체만으로 Attack 표현을 Walk·Idle·Held로 전환하지 않고, 서버 Root가 Attack 표현 중 새 타겟으로 회전하도록 규정한다.
3. 연속 Attack 표현 중에도 새 타겟의 Windup·Impact·VFX·SFX·피해 예약은 정지·5도 정렬과 새 AttackSequence 커밋 전에는 승인하지 않는다.
4. 공격 후보가 없거나 새 후보가 공격 사거리 밖이라 Chase/A* 이동이 필요할 때만 Attack 표현을 종료한다.
5. `NET-PRESENT-*`에 표현 회차의 중복·stale·neutral 전이와 로컬 Animation Event 게이트를 추가한다.

근거: `U-COMBAT-PHASE`, `U-TARGET-COMMIT`, `U-ATK-ALIGN`, `U-ATK-TIMELINE`, `NET-ACTION-STATE`, `NET-ACTION-SEQ`, `NET-FACING-002`, `NET-PRESENT-001~003`, `NET-CANCEL-001~005`.

## 2. 순수 표현 전이 정책과 선행 회귀 검증

수정/추가 후보 파일:

- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` 신규
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

구현:

1. 최초 전투 진입, 같은 타겟 연속 회차, 타겟 교체 정렬 중, 타겟 교체 커밋, 추격 전환, 전투 종료 입력을 Unity/NGO 없는 순수 값으로 분류한다.
2. 이미 Attack 표현 중인 유닛의 타겟 교체 결과가 `KeepAttackAndSuppressImpact`이며 Held/Walk 전이를 만들지 않는 fixture를 먼저 추가한다.
3. 정렬 전 Animation Event는 새 회차 Impact를 승인하지 않고, Ready 커밋 뒤 정확히 한 번만 새 회차를 결속하는 fixture를 추가한다.
4. 중복 revision, 오래된 sequence, neutral Align, 사망·despawn·ID 재사용을 포함한 단조 전이 fixture를 추가한다.
5. Assault·StreamSpirit처럼 한 회전 시간 안에 여러 로컬 타격 프레임이 지날 수 있는 경우에도 승인되지 않은 이벤트가 결과를 소비하지 않는 fixture를 추가한다.

근거: `U-TARGET-COMMIT`, `U-ATK-ALIGN`, `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-001~004`, `NET-CANCEL-005`.

## 3. 서버 타겟 교체와 Attack 표현 연속성

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

구현:

1. 이미 Attack 표현이 활성화된 유닛이 무효 타겟에서 사거리 내 새 후보로 전환할 때 `UnitAnimState.Held`를 발행하지 않는다.
2. `_unitCombatTargets`의 후보 변경, Action 회전 타겟 변경, 정렬 표본 초기화와 새 공격 회차 준비를 명시적으로 분리한다.
3. 새 타겟 정렬 중 기존 Attack 표현은 유지하지만 Legacy 쿨다운·피해 예약은 현재 5도 시작 gate가 계속 차단한다.
4. Ready 경계에서 동일 target/revision에 대해 새 공격 시작 신호를 한 번만 발행한다. 같은 Attack enum 값 때문에 NetworkVariable change가 생기지 않는 경우에도 회차 시작 신호가 중복·유실되지 않게 한다.
5. 공격 가능한 후보가 없어지거나 추격으로 전환될 때만 기존 Stop/Walk 생명주기를 사용한다.
6. 최초 전투 진입 전의 이동 정지 Held와 타겟 교체 중 Attack 유지가 같은 `_combatAnimationSent` 부수효과에 의존하지 않도록 상태 의미를 분리한다.

근거: 전투 전환 규칙 9~14, `U-COMBAT-PHASE`, `U-TARGET-COMMIT`, `U-ATK-ALIGN`, `U-ATK-TIMELINE`, `NET-CANCEL-001~004`.

## 4. 클라이언트 표현 회차 결속

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

구현:

1. 타겟 변경 이벤트는 `UnitView.ChangeTarget`의 원래 계약대로 추적 타겟과 회전만 바꾸며 Animator 상태를 변경하지 않는다.
2. `UnitView`는 새 공격 회차가 아직 커밋되지 않은 동안 로컬 `OnAttackHit`이 새 Target의 VFX·SFX·트레이서·피격 표현 신호를 방출하지 못하게 한다.
3. 새 회차 커밋은 같은 Attack 상태 안에서 target + sequence/revision을 원자적으로 수락하고 한 번만 표현 시작 경계를 갱신한다. 단순 `UnitAnimState.Attack` 값 재할당에 의존하지 않는다.
4. Host 직접 Animator 경로와 순수 Client NetworkVariable/RPC 경로가 같은 전이 정책을 사용하도록 한다.
5. 클라이언트 classifier는 양수 회차 뒤 `SequenceId=0` neutral phase를 허용하되 마지막 발급 sequence 상한은 보존한다. 같은 revision에 다른 비-neutral sequence, 오래된 revision과 identity 충돌은 계속 fail-closed한다.
6. Shadow snapshot은 이번 단계에서 Legacy 피해 권위가 되지 않는다. 표현 게이트에 사용되는 값과 진단 전용 값의 책임을 코드와 주석에서 구분한다.

근거: `NET-ACTION-STATE`, `NET-ACTION-SEQ`, `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-001~003`, `NET-CANCEL-005`, 전환 규칙 3·6·7.

## 5. C1 observer 교정

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

구현:

1. 정상 `shadow-commit`은 매 회차 상세 출력하지 않고 타입/전이별 최초 표본과 전체 카운터만 보존한다.
2. 의도적으로 생략한 정상 반복은 `suppressed`로 집계하고 `dropped`와 구분한다. 실제 필수 증거 상한 초과만 `dropped` FAIL로 남긴다.
3. Legacy Impact의 표시 타겟은 피해 적용 전에 캡처한다. 피해 적용 후 값은 `nextCandidateAfterApply`로 별도 기록하여 사망 동기 이벤트에 의한 정상 재타겟과 Impact 당시 불일치를 구분한다.
4. Host와 Client local verdict는 역할별 필수 증거가 다름을 유지하되, Client `SequenceRegression`은 수정된 classifier의 실제 계약 위반만 집계한다.
5. Android full-line UTF-8 terminal, manifest와 예약 terminal 줄은 기존처럼 fail-closed preflight한다.
6. 위 terminal 의미와 classifier 계약 변경을 구버전 로그와 혼합하지 않도록 observer schema를 `c1-attack-target-facing-shadow-v5`로 올린다.
7. `displayTargetBeforeApply`는 다음 후보를 위한 표시 상태이므로 예약 Impact의 정합성 조건으로 사용하지 않는다. dispatch mismatch는 예약된 Legacy/Shadow TargetId, 양수 SequenceId, reducer 수락 상태와 Shadow/Legacy 피해 적용 결과가 어긋날 때만 fail-closed한다.

근거: `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-003~004`, Android-safe bounded evidence 계약과 `LogRules.md`의 고빈도 로그 제한.

## 6. Impact 사거리 충돌의 안전한 분리

이번 구현에서는 `ApplyAttackDamageCore` 전체에 사거리 재검증을 일괄 추가하지 않는다.

이유:

- `U-IMPACT-TARGETLOCKED`의 MeleeContact/Hitscan은 Impact 시 생존·사거리·8도 재검증이 필요하다.
- ProjectileImpact/TravelingArea는 발사 뒤 공격자의 현재 사거리와 독립적으로 진행할 수 있다.
- 현재 Asset Matrix에서 다수 전달 방식이 Provisional이므로 공통 Legacy 함수 하나를 바꾸면 정상 발사체·파도까지 취소할 위험이 있다.

이번 문서와 ROADMAP에는 이 충돌을 후속 전달 방식별 권위 교정의 선행 항목으로 남긴다. 구현 중 이미 확정된 TargetLocked 프로필에만 부작용 없는 seam이 확인되면 별도 Plan 변경 사유를 기록하고 사용자 승인을 다시 받은 뒤 포함한다.

근거: `U-IMPACT-TARGETLOCKED`, `U-IMPACT-LOCKEDPOINT`, `U-IMPACT-INDEPENDENT`, `NET-DELIVERY-*`.

## 7. 자동 검증 완료 조건

1. 타겟 A Attack 중 A가 무효가 되고 사거리 내 B가 선택되어도 AnimState에 Held/Walk가 발행되지 않는다.
2. A→B 정렬 중 Attack 표현과 서버 Root 회전은 계속되지만 B에 대한 Legacy 공격 예약·VFX·SFX·피격 표현은 5도 커밋 전 0건이다.
3. B 커밋 뒤 같은 회차의 표현 시작은 Host/Client 각각 정확히 1회이며 중복·stale 상태는 무시된다.
4. 최초 전투 진입, 타겟 유지, 타겟 사망, 사거리 이탈→Chase, 적 없음→A* 재개, 공격자 사망과 despawn fixture가 모두 통과한다.
5. Client neutral 전이는 수락되고 양수 sequence 상한은 보존되며 실제 sequence/revision 역행은 계속 거부된다.
6. observer self-validation에서 정상 장기 경기 형태가 `dropped=0`, 실제 mismatch 유실은 FAIL이다.
7. 기존 A1~B3 및 C1 self-validation 전체 PASS, Runtime/Editor C# 컴파일 오류 0건.
8. 신규 애니메이션·프리팹·수치 변경 0건, Legacy 피해 writer와 서버 HP 권위 단일성이 유지된다.

## 8. 사용자 멀티플레이 확인 기준

자동 검증 뒤 사용자가 새 Development Build로 Editor Host + Android Client 한 경기를 실행한다.

- 타겟 교체 시 Walk/Idle/Held 자세가 끼어들지 않는다.
- Attack 동작 중 새 타겟 방향으로 자연스럽게 회전한다.
- 회전 중 정렬되지 않은 타격 VFX·SFX·피격 표현이 발생하지 않는다.
- 타겟이 사라지고 사거리 내 대체 적이 없으면 정상적으로 추격 또는 A* 이동을 재개한다.
- C1 terminal은 `observerSchema=c1-attack-target-facing-shadow-v5`, `legacyBeforeAligned=0`, `correlationFailures=0`, `targetMismatches=0`, `clientRejected=0`, `dropped=0`이어야 한다. 정상 반복 생략은 `suppressedNormal`에만 집계한다.
- Root pose와 movement authority 기존 PASS를 유지해야 한다.

이 Plan은 별도 Testcase.md나 QA 실행을 생성하지 않는다. 사용자 실기 완료 전 C1 전체 완료로 기록하지 않는다.
