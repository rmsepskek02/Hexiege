# RhinoBreaker 실제 Attack 타격 마커 교정 — 조사

RhinoBreaker는 공격 설정값이 없어서 멈춘 유닛이 아니라, 실제 화면에서 재생되는 공격 클립과 타격 이벤트가 들어 있는 보조 클립이 서로 갈라져 있는 유닛이다. 이번 작업은 RhinoBreaker 한 종류만 대상으로 이 연결을 바로잡고, 다른 유닛이나 피해 계산은 건드리지 않는다.

## 현재 상태

- Task 상태: **CONDITIONAL PASS — 다음 통합 빌드 최종 확인 대기**
- 대상: `UnitType.RhinoBreaker` (`unitType: 23`)
- 실제 Animator 상태: `Attack`
- 현재 공격 프로필: `Unresolved / MeleeContact / 1 impact`

## 정적으로 재현한 실패 조건

1. `UnitView`는 공격할 때 이름이 정확히 `Attack`인 Animator state를 재생한다.
2. `RhinoBreaker.controller`의 `Attack` state는 `RhinoBreaker_Attack.anim`을 사용한다.
3. 이 실제 클립에는 `OnAttackHit` Animation Event가 없다.
4. 별도 `mixamo_com` state가 사용하는 `RhinoBreaker_Attack2.anim`에만 `OnAttackHit @ 1.05s`가 있다.
5. Blue/Red 프리팹은 모두 같은 `RhinoBreaker.controller`를 사용한다.
6. `UnitStatsConfig`의 RhinoBreaker 타격 시간은 단일 값 `1.05s`다.
7. 프로필 resolver는 이 불일치를 `attack-state-and-impact-marker-unconfirmed` 사유로 명시적인 `Unresolved`로 유지한다.

## 원인

기존 범용 이벤트 추출·주입 코드는 Controller의 `animationClips` 중 이름에 `Attack`이 포함된 첫 클립을 고른다. RhinoBreaker처럼 `Attack`과 `Attack2`가 함께 있으면 실제 `Attack` state의 motion이 아닌 보조 클립을 고를 수 있다. 따라서 보조 클립의 이벤트가 정상이어도 실제 공격 모션에서는 타격 marker가 발생하지 않는다.

## 적용 규칙

- `GameSystemRules_Units.md` 규칙 17: 실제 Attack 클립의 `OnAttackHit` 시간을 타격 프레임의 단일 출처로 유지한다.
- `GameSystemRules_Units.md` `U-COMBAT-PHASE`: marker는 커밋 전 피해를 만들지 않으며, 서버 권위 공격 단계는 유지한다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`: Animation Event는 로컬 표현 marker이며 피해 권위가 아니다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-002`: 완성 유닛의 marker는 검증된 AttackTimeline과 일치해야 한다.

## 교정 범위

- 정확한 `Attack` state motion만 선택하는 RhinoBreaker 전용 일회성 Editor 메뉴
- 실제 클립에 `OnAttackHit @ 1.05s`를 기존 이벤트 보존 방식으로 멱등 주입
- RhinoBreaker 프로필을 `Supported / MeleeContact / 1 impact`로 전환
- 기존 self-validation의 지원 분할과 RhinoBreaker 기대값 수정
- Observer 구조 검증 fixture의 RhinoBreaker 상태 수정
- 메뉴 실행 후 일회성 스크립트 제거

## 제외 범위

- 다른 유닛 에셋 수정
- 피해 계산, 공격력, 사거리, 쿨다운 수정
- 공격 진입·이동·타겟 로직 수정
- `Attack2` 이벤트 수정 또는 삭제
- 범용 인젝터와 `UnitFactory`의 전면 리팩터링
- 25종 전체 또는 Legacy rollback 실기

## 완료 조건

- 실제 `RhinoBreaker_Attack.anim`에 `OnAttackHit @ 1.05s`가 정확히 1개 존재한다.
- 반복 실행에도 중복되지 않고 기존 이벤트는 보존된다.
- RhinoBreaker가 `Supported`로 해석된다.
- 프로필 분할이 `15 Supported / 9 Unresolved / 1 NotApplicableAttack`으로 수렴한다.
- 기존 Unit Action self-validation이 PASS한다.
- PASS 뒤 Android Build And Run을 시작한다.
- 최종 완료 판정은 사용자의 RhinoBreaker 멀티플레이 실기 뒤에만 내린다.

## 구현 및 gate 결과 — 2026-09-13

- RhinoBreaker 전용 Editor 메뉴로 정확한 `Base Layer/Attack → RhinoBreaker_Attack` 연결을 확인했다.
- 첫 실행에서 `OnAttackHit @ 1.050s`를 적용했고 기존 이벤트 0개를 그대로 보존했다.
- 두 번째 실행은 동일 marker가 정확히 하나인 상태를 확인해 변경 없는 `PASS (no-op)`로 끝났다.
- 일회성 Editor 메뉴와 meta 파일은 실행 후 제거했다.
- RhinoBreaker 프로필은 `Supported / MeleeContact / 1 impact / secondary false`로 전환했다.
- self-validation 기대 분할은 `15 Supported / 9 Unresolved / 1 NotApplicableAttack`으로 갱신했다.
- 기존 전체 Unit Action self-validation은 PASS했다.
- 문서 정합성 검사는 문제 0건으로 통과했다.
- Android Build And Run을 시작했다. 빌드 완료 확인과 사용자 멀티플레이 실기는 남아 있다.

## 사용자 실기 및 로그 결과 — 2026-09-13 추가

- 이전 교정 빌드로 Blue/Red RhinoBreaker의 유닛 공격과 LittleKnight 대조군을 확인했고 공격 진입·결과·표현 오류는 없었다.
- 추가 경기 `f9b842d9…35a8d0`는 Editor Host + Android Client 구성이다. RhinoBreaker 30기(Blue), LittleKnight 77기(Red)가 참여했고 유닛 99기와 건물 6기가 사망했다.
- RhinoBreaker `unitId=0`은 Red TrainingCamp `BuildingId=11`을 대상으로 `AwaitingStationary → Ready → Accepted` 커밋을 만들었고 해당 건물은 이후 사망했다.
- Host 결과 724건/실패 0과 Client 수락 724건/거부 0이 일치했다. 양쪽 `ready=724`, 필수 표현 `692/692`, 중복·전송·표현 실패 0이며 공간 mismatch와 이동 gate·writer·handoff·stationary Walk·error/drop도 0이다.
- 사용자가 실제 접촉 동작과 표현의 육안 시점, 개별 RhinoBreaker의 대상 사망 직후 전환을 명시하지 않았으므로 두 항목은 조건부 확인으로 남긴다.
- Android `UnitView` 초기화 지연 107건은 모두 completion 107건으로 수렴했고 retry failure/exhaustion은 0이다. 현재 실패는 아니지만 별도 구조적 관찰 대상으로 유지한다.
- Root pose observer는 양쪽에서 `maxUnits=64` 경고 1건씩을 남겼다. 상한 이전 표본은 PASS였고 이후 유닛은 검증 범위 밖이다.
- `U+27F3` 경고 2건은 이번 테스트에서 새 빌드를 하지 않아 문자 제거가 반영되지 않은 기존 APK의 증거다. 현재 소스와 Lobby에는 특수문자 없이 `새로고침`만 남아 있으며 다음 통합 빌드에서 확인한다.

현재 Task 상태는 **CONDITIONAL PASS / NEXT INTEGRATED BUILD FINAL CHECK / OPEN**이다.
