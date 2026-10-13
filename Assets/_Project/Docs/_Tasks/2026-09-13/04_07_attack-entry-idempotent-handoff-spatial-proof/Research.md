# 공격 진입 멱등 인계와 대미지 위치 증거 — 조사

이번 교정은 공격 전에 간헐적으로 멈추는 현상과 Client에서 대미지 숫자가 엉뚱한 위치에 보이는 현상을 다시 분리해 다룬다. 최신 빌드는 이전의 약 1초 이동 명령 재발급 문제는 제거했지만, 이미 공격 화면이 정상적으로 열려 있는 상태를 실패로 처리하는 경계와 실제 공격 진입이 아닌 구간까지 멈춤으로 세는 진단 결함이 남았다. 대미지 숫자는 결과 전송 수가 일치해도 최종 화면 좌표가 맞는지 증명하지 못하고 있다.

## 최신 같은 경기 근거

- Android Host: `Assets/_Project/Docs/_Logs/2026-09-13/01_12_logcat/RuntimeLog_device.txt`
- Editor Client: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt`
- 공통 세션 키: `01842d88a20ff3f56597188d0bf12d002488fc0031faf950b7ffc8174828828a`
- 이동 관측 스키마: `b3-movement-authority-v14`
- 결과 표현 관측 스키마: `c3-authoritative-result-presentation-shadow-v8`

Host 이동 terminal은 `combatPresentationHandoffFailures=70`으로 FAIL했다. 저장된 상세는 모두 `boundary=client-visible-entry-order`였다. 반면 공격 결과 Coordinator는 양측 schedule 1,961, result/ready/bundle/released bundle 1,943, pending/failure 0이었고, 필수 시각 결과도 양측 `expectedVisual=1705`, `presentationEmits=1705`로 수렴했다. 따라서 피해 결과의 생성·전달 유실보다 공격 진입 ACK와 마지막 화면 좌표 검증이 우선 원인이다.

## 원인 1 — 이미 충족된 인계를 실패로 처리

서버 Root 정지 직전 `UnitView`는 동기 공격 진입 이벤트를 발행하고, 네트워크 전투 컨트롤러가 Host 적용과 원격 RPC enqueue를 끝낸 뒤 ACK를 돌려줘야 한다. 그러나 현재 진입 핸들러와 provisional 시작 메서드는 해당 유닛이 이미 Attack 표현 중이거나 시작 pending이면 곧바로 반환한다. ACK가 없으므로 `UnitView`는 같은 타겟의 Attack이 이미 화면에 존재해도 인계 실패로 판단하고 Action 소유권을 이동에 되돌린다.

이 상태는 두 종류를 구분해야 한다.

- 같은 타겟과 같은 표현 수명을 이미 Host에 적용하고 원격 전달까지 끝낸 상태: 클립을 재시작하거나 RPC를 중복 전송하지 않고 멱등 성공 ACK를 해야 한다.
- 다른 타겟이거나 Host 적용/원격 전달이 끝나지 않은 pending 상태: 성공을 위조하지 않고 실제 적용을 완료하거나 다음 frame으로 보류해야 한다.

단순히 `_combatAnimationSent` 또는 pending 집합 포함 여부만으로는 이 둘을 구분할 수 없다. 정확한 target kind/id와 presentation revision, Host 적용·원격 enqueue 완료 여부를 가진 불변 receipt가 필요하다.

## 원인 2 — Client 멈춤 관측 scope가 너무 넓음

Client terminal은 공격 시작 426건 중 transport gap 248건과 4 frame 이상 위반 55건을 기록했다. 55건 중 41건은 4~6 frame이고 최대값은 283 frame이었다. 현재 `ClientAttackEntryPresentationOrderTracker`는 위치·Walk·Attack만 입력받는다. 한 번 이동 중 Walk를 본 뒤 Attack이 보일 때까지 정지 Walk frame을 계속 누적하므로, 별도 idle·추적·재진입과 다음 공격을 한 사건으로 잘못 연결할 수 있다.

실제 공격 진입 위반은 같은 복제 command/segment/semantic 수명 안에서 이동 Walk가 끝나고 공격 진입 상태가 시작된 경우에만 판정해야 한다. scope 변경, 일반 NoIntent, 전투 종료, 다시 이동한 새 segment는 이전 후보를 폐기해야 한다. 1~3 frame의 복제/보간 순서 차이는 계속 별도 증거로 보존하되, 4 frame 이상을 실패로 올리는 기준은 정확한 공격 진입과 상관된 경우에만 적용한다.

## 원인 3 — 대미지 숫자의 최종 화면 좌표를 검증하지 않음

`HitPresentationQueue`는 서버 결과의 canonical Impact 위치를 만들고, pure Client가 뒤집힌 시점에는 `ViewConverter.ToView`를 한 번 적용한 뒤 HP 텍스트와 선택 피격 VFX에 전달한다. 살아 있는 동일 피해자 View는 punch 용도로만 확인한다. 이 구조는 self-contained 결과 표현 원칙에는 맞지만, 최종 표시 좌표가 피해자 VisualRoot와 실제로 같은지 로그에 남기지 않는다.

따라서 최신 로그의 `presentationEmits=1705`는 “숫자를 방출했다”는 증거일 뿐 “올바른 피해자 위치에 방출했다”는 증거가 아니다. 같은 피해자 객체와 presentation transform을 확인할 수 있는 순간에만 두 XZ 좌표의 차이를 읽기 전용으로 비교하고, flip 적용 횟수·역할·피해자 키와 함께 bounded evidence를 남겨야 한다. View가 이미 사라진 확정 결과는 기존 규칙대로 저장 위치에서 표시하며 공간 비교 불가를 표현 실패로 만들지 않는다.

## 보존해야 할 계약

- `GameSystemRules_Units.md` 이동 규칙 8과 `U-COMBAT-PHASE`: 공격 진입은 Walk에서 provisional Attack으로 직접 이어지고 중간 Held/Idle을 삽입하지 않는다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`: Action 전 후보를 공개하지 않고 provisional Start가 target과 suppression을 원자 전달한다.
- `NET-PRESENT-002`: 이미 재생 중인 Attack 클립은 회차/인계 확인 때문에 restart하지 않는다.
- `NET-PRESENT-003`: 확정 결과의 위치 스냅샷은 필수 표현의 권위이며 현재 View는 선택 반응과 읽기 전용 검증에만 사용한다.
- `NET-ROOT-*`: Client는 Simulation Root, NetworkTransform, 서버 Aim을 쓰지 않는다.
- 기존 C2 서버 피해 writer와 C3 단일 결과 emitter, Supported/Unresolved marker gate를 변경하지 않는다.

## 판정

v14는 이전 명령 재발급 정지는 제거했지만 공격 진입 전체와 대미지 위치를 완료하지 못했다. 다음 구현은 멱등 ACK receipt, exact-scope Client 관측, 최종 표시 위치의 읽기 전용 증거를 함께 추가한다. 자동 PASS 뒤에도 최종 해결 판정은 새 Android/Editor 같은 경기의 양측 terminal과 사용자 육안 확인이 필요하다.

## 2026-09-13 최신 사용자 실기 결과 — FAIL / OPEN

이 Task의 구현과 Unity self-validation PASS 뒤 진행한 최신 멀티플레이 실기에서 사용자는 **근거리 유닛들이 공격하지 않는 현상**을 확인했다. 대미지 텍스트 위치는 정상으로 확인됐다. 따라서 앞선 자동 PASS는 실제 공격 진입 전체를 증명하지 못하며, 이 Task와 유닛 행동 교정 전체는 **FAIL / OPEN**이다.

### 같은 경기 식별자와 로그

- 공통 `sharedSessionKey`: `993718220d45194886d85f22bd4e8cade81e2992baf80a5152b7127a2fd69a3e`
- Editor Host: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt`
- Android Client: `Assets/_Project/Docs/_Logs/2026-09-13/05_19_logcat/RuntimeLog_device.txt`
- Host 이동 terminal은 `combatPresentationHandoffFailures=87`, `heldFrames=423`, `failureDetails=64`, `failureEvidenceOverflow=23`을 기록했다(Host 7331행). 최종 이동 판정은 `verdict=FAIL`이다(Host 7336행).
- 저장된 handoff 상세 64건은 모두 `boundary=client-visible-entry-order`이며 첫 발생은 Host 6722행, 마지막 저장 상세는 7234행 구간에 있다. 64개 상세의 UnitId를 같은 경기 생성 로그와 대조하면 `LittleKnight` 14건, `EmberSpirit` 14건, `FlameSpirit` 6건, `DustSpirit` 12건, `TideSpirit` 14건, `SpearMan` 4건이다. 대표 생성/실패 쌍은 Host 6688/6722행, 6695/6723행, 6815/6835행, 6740/6798행, 6713/6728행, 6964/7043행이다.
- Host C3는 서버 공격 결과 2,298건과 결과 실패 0건을 기록했다(7338행). 필수 표현은 `expectedVisual=2036`, `presentationEmits=2036`, 실패·중복·전송 실패 0으로 수렴했다(7339행). Client도 결과 수락 2,298건/거부 0건(132619행), 필수 표현 2,036/2,036건(132630행)이다. 이는 **실제로 발생한 공격의 피해·대미지 텍스트 파이프라인이 작동했다는 증거**이지, 공격에 진입하지 못한 근거리 유닛까지 정상이라는 증거는 아니다.
- 표시 위치 읽기 전용 표본은 Host 1,830건에서 mismatch 0, 최대 XZ 차이 0.220(7341행), Client 1,752건에서 mismatch 0, 최대 0.245(132648행)였다. 사용자도 대미지 텍스트 위치가 정상이라고 확인했다.
- Host 공격 관측은 `coveredUnitTypes=4/25`에 불과했다(7342행). 대표 정규 commit은 `Pistoleer`, `Assault`, `SpearMan`에서 확인된다(6806, 6849, 7003행). `StreamSpirit`은 정규 프로필이 `Unresolved`지만 production start gate가 `Ready`까지 진행한 증거가 있다(6898~6903행). 이 제한된 공격 증거를 25종 전체 정상으로 확대할 수 없다.

### 확정된 production 경계 불일치

기존 전용 실수 기록과 이전 Plan은 “provisional Start의 단일 payload가 target을 원자 전달한다”고 정했다. 그러나 실제 production event seam은 이 계약을 구현하지 않았다.

1. `GameEvents.OnUnitEnteredCombat`은 `Subject<int>`이며 `unitId`만 전달한다(`Assets/_Project/Scripts/Application/Events/GameEvents.cs` 1028~1033행).
2. `UnitView`는 Chase 후보 위치에서 공격 범위 진입을 판정하고 확정한 `targetId`와 `targetIsUnit`을 callback에 보유한다(4207~4212행). Root/Reducer commit 전에 handoff callback을 먼저 호출하는 순서도 명시돼 있다(1368~1388행).
3. 그러나 실제 이벤트 발행은 두 target 값을 버리고 `_unitData.Id`만 보낸다(5123~5146행).
4. `NetworkCombatController.OnUnitEnteredCombatHandler(int unitId)`는 target을 받지 못하므로 `combat.TryFindTarget(unit)` 또는 `combat.FindNearestEnemy(unit)`로 다시 찾는다(2436행, 2472~2477행).
5. 이 재검색 시점에는 후보 위치가 아직 Root와 `UnitData.Position`에 commit되기 전이다. 따라서 작은 공격 사거리의 근거리 유닛은 후보 위치에서는 범위 안이지만 현재 권위 위치에서는 범위 밖으로 판정될 수 있고, candidate가 없으면 handler가 ACK 없이 반환한다. `UnitView`는 handoff 실패로 Root 정지를 commit하지 않고 다음 frame에 같은 경계를 반복한다.

이 사건은 **완전 교정된 버그의 재발이 아니다.** 문서에는 원자 target 전달을 완료한 것으로 기록했지만 실제 production 이벤트 형식과 호출 순서를 끝까지 대조하지 않아 불완전 구현을 완료로 오판했고, 후속 Task가 그 오판을 정상 기반으로 가정한 사건이다. self-validation은 target이 완성된 입력을 직접 구성했을 뿐 위 production 순서인 `후보 위치 판정 → unitId-only 이벤트 → commit 전 현재 위치 재검색`을 재현하지 못했으므로 최종 성공 증거가 아니다.

## 다음 교정의 제한 범위

- 공격 진입 이벤트를 `unitId + targetId + targetIsUnit`의 **typed payload**로 바꾸고, 이동 후보에서 확정한 target을 `NetworkCombatController`까지 직접 전달한다.
- 해당 최초/재진입 경계에서 현재 위치 기반 `TryFindTarget`/`FindNearestEnemy` 재검색을 제거한다. 전투 중 명시적 target 변경 경계는 별도 계약으로 유지한다.
- 피해 계산, C2 결과 writer, 대미지 텍스트, 피격 VFX와 현재 정상인 결과 표현 경로는 수정하지 않는다.
- 검증기, Observer, schema를 더 확장하지 않는다. 기존 자동 PASS를 완료 판정으로 사용하지 않는다.
- **후속 구현은 아직 시작하지 않았다.** 수정 후에도 사용자의 새 멀티플레이 실기 확인 전에는 완료 또는 PASS로 기록하지 않는다.
