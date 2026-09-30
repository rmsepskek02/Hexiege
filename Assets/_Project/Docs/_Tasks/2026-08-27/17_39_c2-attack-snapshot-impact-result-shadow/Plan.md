# C2 공격 Snapshot·ImpactResult Shadow Plan

이번 계획은 C1의 서버 정지·5도 공격 시작과 독립 회차 결속을 유지한 채, Legacy 피해 writer가 확정한 결과를 정식 `AttackImpactResult`로 만들고 Reliable Shadow 메시지로 복제·감사하는 구현 계획이다. 기존 피해·HP·사망 이벤트·RPC·VFX는 계속 유일한 gameplay writer이며 신규 결과는 진단과 후속 Presentation 전환 준비에만 사용한다.

## 1. 적용 규칙

- `GameSystemRules_UnitCombatSynchronization.md`의 행동 Snapshot 최소 데이터 계약
- 같은 문서의 Impact 결과 레코드 계약과 `NET-ACTION-IDEMPOTENT`
- 같은 문서의 서버 시각·공격 방향·전달 방식별 판정 규칙
- 같은 문서의 전환 규칙: 신규 Snapshot/ImpactResult는 Shadow 전송하고 신규 Presenter는 아직 로그만 남긴다
- `GameSystemRules_Units.md`의 공격 정렬, 회차 타겟 고정, 서버 권위 피해 규칙

## 2. 구현 단계

### 2.1 Legacy 결과 값 계약 보강

수정 파일:

- `Assets/_Project/Scripts/Application/Combat/AttackDamageApplyStatus.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`

작업:

1. status, 적용량, 결과 HP를 담는 불변 관측 값을 추가한다.
2. `ApplyAttackDamageObserved`는 target을 찾은 동일 writer 호출 안에서 HP 전후를 캡처한다.
3. 기존 `ApplyAttackDamage` void API와 실제 피해·이벤트 순서는 유지한다.
4. 공격자 또는 타겟 부재는 적용량 0, 결과 HP -1로 명시한다.
5. 특수 공격이 주 공격을 대체해 주 타겟 HP가 줄지 않은 성공 경로는 0 피해 성공으로 보존한다.

### 2.2 Coordinator dispatch/completion 분리

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs`

작업:

1. Legacy reservation에 dispatched mask와 completed mask를 각각 유지한다.
2. `DispatchLegacyImpact`는 authorization을 만들되 reservation을 아직 폐기하지 않는다.
3. Legacy writer 호출 뒤 `CompleteLegacyImpact`가 같은 token·hit·authorization인지 확인한다.
4. writer 결과를 `AttackImpactResult`로 변환하고 `ConfirmImpactResult`에 전달한다.
5. 모든 hit이 completion까지 끝난 뒤 reservation을 폐기한다.
6. dispatch 실패도 completion에서 명시적으로 닫아 reservation 누수를 막되 결과를 위조하지 않는다.

### 2.3 개별 ImpactResult 네트워크 계약

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`

작업:

1. `AttackImpactResult`와 delivery를 NGO 원시 값으로 투영하는 `NetworkAttackImpactShadowResult`를 추가한다.
2. 전체 결과 키, action revision, Impact 서버 시각, AimDirection, 선택적 ImpactPoint, outcome, 적용량, 결과 HP를 직렬화한다.
3. 서버와 Client가 공유하는 bounded 멱등 분류기를 추가한다.
4. 서버는 유효한 새 결과만 기본 Reliable `ClientRpc`로 전송한다.
5. Host는 gameplay 경로를 다시 실행하지 않고 서버 observer만 기록한다.
6. Client는 결과를 observer에만 전달하며 Animator·HP·VFX에는 적용하지 않는다.

### 2.4 Production adapter와 observer 연결

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`

작업:

1. 피해 호출 직전 authorization을 만든 기존 순서를 유지한다.
2. Legacy writer 반환 직후 coordinator completion을 호출한다.
3. 유효한 결과 publication은 해당 공격자 `NetworkUnit`을 통해 전송한다.
4. 서버 observer는 authorization, Legacy 상태, 결과 outcome·적용량·HP와 completion 상태를 비교한다.
5. Client observer는 accepted, duplicate, conflict, invalid, retired를 bounded 카운터와 실패 증거로 기록한다.
6. 정상 결과를 매 hit마다 일반 로그로 남기지 않고 terminal 카운터와 제한된 대표 표본만 사용한다.

### 2.5 RED→GREEN 자체 검증

수정 파일:

- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

먼저 다음 회귀를 추가해 기존 코드에서 RED를 확인한다.

1. Legacy 적용량·결과 HP 관측 값의 성공/부재/0피해 성공 계약
2. dispatch 후 completion 전 reservation 유지와 completion 후 폐기
3. authorization과 정확히 일치하는 HitApplied/Miss 또는 0피해 결과 생성
4. 중복 completion, 잘못된 token·hit, invalid 값 fail-closed
5. 네트워크 결과 직렬화 값의 유효성
6. 같은 키·같은 payload Duplicate, 같은 키·다른 payload Conflict
7. 결과 완료 순서가 sequence 순서와 달라도 Accepted
8. retire 이후 패킷 거부와 새 NetworkObject 수명 격리
9. Snapshot보다 결과가 먼저 또는 뒤에 도착해도 결과 자체를 잃지 않는 분류

GREEN 뒤 A1~B3와 C1 기존 검증도 모두 통과해야 한다.

## 3. 변경 금지 범위

- Legacy 피해량·방어력·특수 공격 수치
- 타겟 선택 우선순위와 C1 연속 Attack 표현 정책
- 서버 Simulation Root 단일 writer와 B3 이동 경로
- 피격 VFX·HP 텍스트·피해자 애니메이션 emitter
- 실제 projectile 궤적·착탄 시스템
- 기존 Combat RPC 제거 또는 권위 전환
- 신규 애니메이션·VFX 에셋 제작

## 4. 실패 시 안전 동작

- 결과 값 생성이나 직렬화 실패: Shadow 결과만 생략하고 Legacy 피해는 유지한다.
- coordinator scope 불일치: 오류 증거를 남기고 Legacy 결과를 재적용하지 않는다.
- Client 중복: 무시한다.
- Client 충돌·invalid·retired: fail-closed 진단만 남기고 gameplay에 적용하지 않는다.
- observer 또는 로그 예외: bounded 경고 후 공격 루프를 계속한다.

## 5. 자동 완료 게이트

1. 문서 정합성 검사 0건.
2. Unity C# 컴파일 오류 0건.
3. `Run Unit Action Self Validation`의 기존 A1~B3·C1 및 신규 C2 항목 PASS.
4. Shadow 경로의 HP·Root·Animator·RPC/VFX gameplay write 0건.
5. 다중 hit와 겹친 회차에서 결과 키 중복·누락·잘못된 폐기 없음.
6. 서버가 보낸 모든 유효 결과를 Client가 같은 payload로 수락하거나 정확한 중복으로 분류 가능.

## 6. 구현 후 실기 게이트

새 Android 빌드에서 Editor와 기기 역할을 교대해 검증한다. 단일 근접, 다중 근접, Hitscan, 특수 공격 표본을 먼저 보고, 빠른 타겟 사망·타겟 교체·건물 공격을 포함한다. 첫 집중 게이트가 통과하면 남은 25종 coverage와 Legacy rollback을 통합 검증한다.

이번 C2의 PASS는 결과 복제와 비교 게이트의 PASS다. 시각 Impact와 피해 표현 시점의 실제 교정은 다음 Phase 5 결과 키 Presentation 구현과 실기 검증 후 판정한다.

## 7. 구현 결과 (2026-08-27)

C2 코드 구현과 Editor 자동 게이트를 완료했다.

- Legacy writer 내부에서 status·적용량·결과 HP를 확정하는 `AttackDamageObservation`을 추가했다.
- coordinator reservation을 dispatch와 completion으로 분리하고, 같은 token·hit authorization만 `AttackImpactResult`로 확인한 뒤 폐기하도록 변경했다.
- 전체 `AttackResultKey`, delivery, action revision, Impact 시각, 권위 AimDirection, outcome, 적용량과 결과 HP를 담는 NGO 결과 값을 추가했다.
- 현재 Snapshot NetworkVariable과 개별 결과 Reliable ClientRpc를 분리했다. Client 결과 경로는 observer만 호출하며 gameplay write는 없다.
- 결과 완료 순서와 sequence 순서가 달라도 전체 키로 수락하고, exact duplicate·payload conflict·invalid·retired 수명을 구분하는 bounded 분류기를 추가했다.
- observer schema를 `c2-attack-impact-result-shadow-v6`으로 올리고 C1 END와 C2 `result-END`를 분리했다. 두 terminal과 manifest 모두 전체 줄 UTF-8 최악값 preflight를 통과한다.
- Unity 첫 자체 검증은 길어진 단일 terminal 때문에 RED였고, terminal 분리 뒤 전체 A1~B3·C1·C2 검증이 GREEN PASS했다. Unity Console의 실제 C# 컴파일 오류는 0건이다.

Android/Editor 역할교대 실기는 아직 수행하지 않았다. 따라서 C2 상태는 **구현·Editor 게이트 PASS / 멀티플레이 통합 게이트 OPEN**이다. 다음 실행은 새 Development Build의 집중 표본 경기이며, 그 결과가 PASS일 때 Phase 5 결과 키 Presentation 구현으로 진행한다.

## 8. 멀티플레이 실기 결과와 후속 교정 (2026-08-27 추가)

이 계획의 구현은 Editor self-validation을 통과했지만, 후속 Android Host / Editor Client 동일 경기(`sharedSessionKey=d8cc…`)에서 C2 실기 게이트가 **FAIL**했다. 따라서 §7의 “멀티플레이 통합 게이트 OPEN”은 실기 전 상태 기록이며, 현 판정은 **자동 게이트 PASS / 멀티 실기 FAIL**이다.

- Legacy scheduler가 `observedNow - overshoot`의 실제 주기 경계를 사용하지만 Shadow는 관측 시각과 원래 hit offset으로 due를 계산해 Impact가 최대 약 50ms 늦어졌다.
- 정상 Legacy 결과가 Shadow에서 `NotDue`로 거부되고 다중 hit가 `OutOfOrder`로 분류됐다.
- 같은 경기에서 B3 Unit 64 `LittleKnight`가 같은 walkable 권위 타일의 중심 복귀를 경로 없음으로 오판해 `post-combat-no-safe-forward-center` 영구 정지를 만들었다.
- 최초 `AlignToAttack`은 기존 Attack 클립을 선행 표현으로 재생하되 커밋 전 Impact 계열 출력을 억제하고, 5도 커밋에서 실제 cycle을 restart해야 한다.

후속 교정의 Research와 구현 계획은 `Assets/_Project/Docs/_Tasks/2026-08-27/23_13_c2-b3-combat-transition-correction/Research.md` 및 `Assets/_Project/Docs/_Tasks/2026-08-27/23_13_c2-b3-combat-transition-correction/Plan.md`가 담당한다. 이 교정과 역할교대 실기가 통과하기 전에는 C2 완료 또는 Phase 5 진입으로 판정하지 않는다.
