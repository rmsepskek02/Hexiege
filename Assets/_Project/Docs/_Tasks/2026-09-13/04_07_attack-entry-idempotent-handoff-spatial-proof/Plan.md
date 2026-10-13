# 공격 진입 멱등 인계와 대미지 위치 증거 — 교정 계획

> **⚠️ 현행 상태: FAIL / OPEN — 2026-09-13 최신 멀티플레이 사용자 실기 FAIL로 아래 §1~§7은 superseded(폐기)되었다.**
>
> §1~§7은 이전 구현 당시의 과거 계획과 판단 근거를 보존하기 위한 이력일 뿐, 다음 구현의 승인 범위가 아니다. 특히 §6의 7개 파일 목록과 §3~§5의 검증기·Observer 확장 계획을 현행 작업 지시로 사용하면 안 된다. **다음 구현에서 유일하게 승인된 현행 대상은 §8-2~§8-4이며**, typed payload로 target을 직접 전달하고 해당 공격 진입 경계의 재검색만 제거한다. 검증기·Observer·schema 확장과 피해·대미지 텍스트·VFX 수정은 금지하며, 사용자 실기 전에는 완료 또는 PASS로 판정하지 않는다.

이번 작업은 이미 정상인 공격 화면을 다시 시작하지 않으면서 서버가 “공격 화면 인계가 완료됐다”는 사실을 정확히 확인하도록 고친다. Client의 멈춤 진단은 같은 공격 진입 사건만 세도록 좁히고, 대미지 숫자는 실제 피해자 화면 위치와 얼마나 떨어져 표시됐는지 기록한다. 공격 판정·피해·위치 권위는 서버에 그대로 두며, 진단을 통과시키기 위한 허용치 상향이나 실패 무시는 하지 않는다.

## 1. 정확한 공격 진입 receipt

근거: `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`, `NET-PRESENT-002`, `GameSystemRules_Units.md` `U-COMBAT-PHASE`.

- 유닛별로 마지막 완료된 provisional 표현의 target kind/id, presentation revision, Host 적용 완료, 원격 RPC enqueue 완료를 값으로 보존한다.
- 같은 target의 완료 receipt가 있고 Host Attack이 계속 유효하면 새 revision·RPC·CrossFade 없이 ACK만 반환한다.
- target이 다르거나 receipt가 미완료면 멱등 성공으로 처리하지 않는다. 실제 Start/Change 경계 또는 정상 defer로 보낸다.
- `_combatAnimationSent`와 pending 집합은 표현 상태 캐시로만 사용하고, 원자 인계 완료의 단독 근거로 사용하지 않는다.
- StopCombat, 사망, despawn, 새 수명 시작은 receipt를 정확히 retire한다.

## 2. 동기 ACK 결과를 명시적 값으로 반환

근거: `NET-PRESENT-001`, `NET-ACTION-IDEMPOTENT`.

- provisional 시작 함수는 void 조기 반환 대신 `Started`, `AlreadySatisfied`, `Deferred`, `Failed`와 presentation revision을 가진 순수 결과를 반환한다.
- `Started`와 `AlreadySatisfied`만 서버 Root 정지 인계를 ACK한다.
- `Deferred`는 causal error가 아니며 이번 frame의 Root 정지를 보류한다.
- 실제 Host 적용 또는 ACK 불일치만 `combatPresentationHandoffFailures`로 남긴다.
- 이미 만족된 경우 Attack clip restart, target-change RPC, scope 재발급, marker lease 변경은 모두 0이어야 한다.

## 3. Client 공격 진입 관측 scope 교정

근거: `GameSystemRules_Units.md` 이동 규칙 8, `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`, `NET-ROOT-004`.

- 추적기에 command revision, segment revision, semantic revision과 복제 movement phase/target-acquire 여부를 입력한다.
- 이동 Walk 관측과 공격 진입 NoIntent가 같은 정규 scope로 이어진 경우에만 stationary gap 후보를 연다.
- 일반 idle, 다른 segment, 전투 종료, lifecycle retire, 공격과 무관한 재이동은 후보를 즉시 닫는다.
- 1~3 frame은 transport/interpolation 증거로 보존하고, 4 frame 이상은 정확한 같은-scope 공격 진입에서만 causal 위반으로 집계한다.
- 최대 283 frame 같은 기존 stale 누적을 재현하는 RED 회귀를 먼저 만들고, 교정 후 `None`으로 닫히는지 확인한다.

## 4. 대미지 표시 공간 증거

근거: `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-003`, `NET-FACING-002`.

- 최종 `displayPosition`을 만든 직후, 동일 객체 수명이 확인된 피해자 presentation transform이 있으면 XZ 차이를 계산한다.
- Host/Blue는 canonical 좌표 그대로, flipped pure Client는 결과 위치에 정확히 한 번 view 변환을 적용한 뒤 비교한다.
- 허용치는 기존 presentation pose/NetworkTransform 공간 기준에서 이름 있는 상수로 공유하고 임의 상향하지 않는다.
- 표본 수, 일치, mismatch, 최대 거리, 역할·flip·피해자 키의 bounded 상세를 C3 presentation 공간 전용 terminal에 기록한다.
- 동일 피해자 View가 없으면 `notObserved`로 분리한다. 저장 위치에서의 필수 HP 텍스트 방출을 막거나 실패로 만들지 않는다.
- mismatch가 있으면 C3 presentation verdict를 FAIL로 만들되 gameplay writer는 0을 유지한다.

## 5. TDD 자동 회귀

1. 이미 같은 target의 Attack이 활성인 상태에서 재진입 이벤트를 보내 기존 구현의 ACK 누락을 RED로 재현한다.
2. 같은 target 완료 receipt는 ACK 1회, 새 revision/RPC/CrossFade/scope mutation 0인지 검사한다.
3. conflicting target·미완료 pending은 성공으로 위조되지 않는지 검사한다.
4. 정확한 command/segment 공격 진입에서 1~3 frame과 4 frame 이상 분류가 유지되는지 검사한다.
5. scope 변경 뒤 훗날 Attack이 도착하는 283 frame 형태는 위반이 아니라 폐기되는지 검사한다.
6. unflipped Host/Client와 flipped Client에서 좌표 변환이 0회/정확히 1회인지, mismatch와 no-view가 분리되는지 검사한다.
7. 기존 C1/C2/C3, PostCombatResume, target staging, marker lease, single writer/emitter 회귀를 모두 유지한다.

## 6. 변경 예정 파일

- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitMovementAuthorityObserver.cs`
- `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

실제 호출 경계 대조 결과 필요 없는 파일은 수정하지 않는다.

## 7. 완료 gate

1. Unity 생성 참조와 같은 세대의 Runtime/Editor C# 정적 컴파일 오류 0.
2. `Hexiege → Combat → Run Unit Action Self Validation` 전체 PASS.
3. PASS를 확인한 경우에만 Android `Build And Run` 실행.
4. 새 같은 경기에서 Host `combatPresentationHandoffFailures=0`.
5. Client 공격 진입 위반은 exact-scope 표본에서만 집계되며 실제 4 frame 이상 위반 0.
6. C3 공간 표본 mismatch 0, 최대 XZ 거리 허용치 이내, flip 적용 계약 일치.
7. C2/C3 결과 수렴, Root pose, single writer/emitter 기존 terminal 조건 유지.

자동 gate와 빌드 성공은 실기 허용 조건이다. 최종 완료는 사용자가 공격 전 멈춤과 대미지 숫자 위치를 확인하고, 같은 경기 로그에서 위 조건이 모두 수렴한 뒤 판정한다.

## 8. 2026-09-13 사용자 실기 FAIL과 계획 정정

앞선 계획에 따라 추가한 멱등 receipt·exact-scope 관측·표시 위치 증거는 Unity self-validation을 통과했지만, 최신 멀티플레이 실기에서 근거리 유닛들이 공격하지 않았다. Host 이동 terminal도 `combatPresentationHandoffFailures=87`과 최종 `FAIL`을 기록했다. 따라서 §1~§7의 자동 gate 통과는 실제 production 공격 진입 성공을 증명하지 못했고, 이 Task는 **FAIL / OPEN**이다.

### 8-1. 잘못된 기존 전제

- 기존 문서와 구현 설명은 provisional Start가 target을 원자 전달한다고 전제했다.
- 실제 `OnUnitEnteredCombat` 이벤트는 `unitId`만 전달한다. `UnitView`가 후보 위치에서 확정한 target은 이벤트 경계에서 유실되고, 수신 측이 이동 commit 전 현재 위치에서 target을 다시 검색한다.
- 후속 Task는 이 불완전한 production seam을 정상 기반으로 가정한 채 멱등 ACK와 관측 범위를 확장했다.
- 따라서 이번 사건은 “완료된 원자 전달이 다시 깨진 회귀”가 아니라 **실제 seam 미구현을 문서상 완료로 오판한 사건**으로 기록한다.

### 8-2. 후속 구현 범위 — 미착수

**현재 상태: 후속 구현 미착수.** 다음 구현은 아래 한 경계로 제한한다.

1. `OnUnitEnteredCombat`을 `unitId`, `targetId`, `targetIsUnit`을 함께 가진 typed payload 이벤트로 변경한다.
2. `UnitView`가 이동 후보에서 이미 확정한 target을 그 payload로 직접 전달한다.
3. `NetworkCombatController`는 최초/재진입 handoff에서 전달받은 target을 그대로 사용하고, 해당 경계의 `TryFindTarget`/`FindNearestEnemy` 재검색을 제거한다.
4. target 전달, Host provisional 적용, 원격 Client 전달 enqueue와 ACK가 같은 handoff 경계에서 완료돼야 Root 정지를 commit한다.
5. 전투 중 새 타겟 선택과 `ChangeTarget`은 기존의 명시적 Action 준비 경계를 유지하며 최초 Start와 섞지 않는다.

근거 규칙은 `GameSystemRules_Units.md`의 `U-MOV-ALIGN` 9, `U-COMBAT-PHASE`, `U-TARGET-COMMIT`과 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-PRESENT-001`, `NET-ACTION-IDEMPOTENT`다. 후보 위치의 범위 진입과 원자 target 전달을 같은 사건으로 유지해야 하며, 수신 측 재검색으로 다른 시간·위치의 후보를 만들면 안 된다.

### 8-3. 명시적 비수정·금지 범위

- **검증기, Observer, schema 확장 금지.** 이번 결함을 이유로 새 진단 계층이나 terminal 필드를 추가하지 않는다.
- C2 피해 writer, 공격 결과 생성·전송, 대미지 텍스트, `HitPresentationQueue`, 피격 VFX 및 정상 결과 표현 경로를 수정하지 않는다.
- 허용치 상향, 실패 분모 제외, handoff 실패 무시로 PASS를 만들지 않는다.
- 관련 없는 리팩터링과 공격 프로필/에셋 변경을 하지 않는다.
- 빌드는 실행만 하며 완료 감시와 확인은 사용자가 수행한다.

### 8-4. 완료 판정

- 정적 컴파일과 기존 self-validation은 빌드 전 안전 확인일 뿐 최종 성공 증거가 아니다.
- 새 빌드에서 근거리 유닛이 이동 후보 진입 후 실제로 Root 정지, 방향 정렬, 공격 commit과 피해 결과까지 진행해야 한다.
- 최신 경기와 같은 `combatPresentationHandoffFailures`가 1건이라도 남거나, 근거리 유닛이 공격하지 않으면 FAIL이다.
- 현재 정상인 대미지 텍스트 위치와 결과 표현이 유지돼야 한다.
- **사용자의 새 멀티플레이 실기 확인 전에는 구현 완료·작업 완료·PASS로 기록하지 않는다.**
