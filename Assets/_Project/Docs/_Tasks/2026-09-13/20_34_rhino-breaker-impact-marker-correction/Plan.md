# RhinoBreaker 실제 Attack 타격 마커 교정 — 계획

실제 런타임이 재생하는 RhinoBreaker의 `Attack` state를 직접 따라가 그 motion에만 타격 marker를 복구한다. 자동검증 PASS 후 빌드를 시작하되, 빌드 완료 확인과 최종 실기 판정은 사용자가 진행한다.

## 상태와 범위

- 상태: **CONDITIONAL PASS / 다음 통합 빌드 최종 확인 대기**
- 대상 유닛: RhinoBreaker 한 종류
- 기존 로직 제거: 없음
- 서버 공격 결과 및 피해 writer 변경: 없음

## 1. RhinoBreaker 전용 일회성 Editor 메뉴

수정 파일: `Assets/Editor/Setup/SetupRhinoBreakerAttackMarker.cs` (실행 후 제거)

- Blue/Red 프리팹이 같은 Controller를 사용하는지 확인한다.
- Controller의 Base Layer에서 이름이 정확히 `Attack`인 state를 찾는다.
- 해당 state의 motion이 정확히 `RhinoBreaker_Attack.anim`인지 확인한다.
- `UnitStatsConfig`의 RhinoBreaker 타격 시간이 단일 `1.05s`인지 확인한다.
- `1.05s`가 클립 길이 안인지 확인한다.
- 기존 `OnAttackHit`이 없을 때만 기존 이벤트를 보존해 하나를 추가한다.
- 같은 시점의 이벤트가 이미 하나면 변경 없이 PASS한다.
- 다른 시점의 `OnAttackHit` 또는 중복이 있으면 추정 수정하지 않고 FAIL한다.
- 저장 후 다시 읽어 정확히 1개인지 검증하고 명확한 PASS 로그를 남긴다.

규칙 근거: `GameSystemRules_Units.md` 규칙 17, `NET-PRESENT-001`, `NET-PRESENT-002`.

## 2. 지원 프로필 전환

수정 파일: `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`

- RhinoBreaker를 `Supported(type, MeleeContact, 1, false)`로 전환한다.
- 다른 미완료 프로필과 BloomFairy의 별도 힐 경로는 유지한다.

규칙 근거: `U-COMBAT-PHASE`, `NET-PRESENT-002`. 검증된 단일 marker가 있는 TargetLocked 근거리 1회 공격만 현재 sequencer 지원 대상으로 연다.

## 3. 기존 검증 기대값 교정

수정 파일:

- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`

- 지원 분할을 `15 / 9 / 1`로 변경한다.
- QuakeSpirit의 `Unresolved` 계약은 그대로 별도 확인한다.
- RhinoBreaker는 `Supported / MeleeContact / 1 / supported`를 요구한다.
- Observer의 UTF-8 구조 fixture도 RhinoBreaker `Supported`로 맞춘다.
- 새 검증기나 schema는 추가하지 않는다.

규칙 근거: `NET-PRESENT-001`, `NET-PRESENT-002`. 실제 계약 변경과 기존 검증 기대값만 일치시키며 판정 기준을 낮추지 않는다.

## 4. Unity 적용과 검증

1. Unity 컴파일 완료를 확인한다.
2. RhinoBreaker 전용 메뉴를 실행한다.
3. 같은 메뉴를 한 번 더 실행해 멱등성을 확인한다.
4. 실제 `.anim`에 이벤트가 정확히 1개인지 확인한다.
5. 일회성 Editor 스크립트를 제거한다.
6. Unity 재컴파일 뒤 기존 `Hexiege → Combat → Run Unit Action Self Validation`을 실행한다.
7. PASS일 때만 Android Build And Run을 실행한다.
8. 빌드 완료 감시는 하지 않고 사용자에게 실기 범위를 전달한다.

## 5. 문서 반영

- 이 Task 문서에 구현·gate 결과를 기록한다.
- `UnitCombatAssetMatrix.md`의 RhinoBreaker 행은 자동검증 결과와 사용자 실기 상태를 분리해 갱신한다.
- 사용자 실기 전에는 Task를 완료 처리하지 않는다.

## 위험과 차단 조건

- `Attack2`를 실제 클립으로 오인하면 같은 문제가 유지되므로 이름 부분 일치 선택을 금지한다.
- `1.05s`가 실제 클립 범위를 벗어나면 클램프하지 않고 중단한다.
- marker는 표현 허가일 뿐 피해를 발생시키면 안 된다.
- self-validation이 실패하면 빌드하지 않는다.
- 이번 변경 때문에 다른 유닛의 프로필이나 공격 로직을 함께 수정하지 않는다.

## 구현 및 빌드 gate 결과 — 2026-09-13

| 항목 | 결과 |
|---|---|
| 정확한 Animator state motion 확인 | PASS — `Base Layer/Attack → RhinoBreaker_Attack` |
| 실제 Attack marker 적용 | PASS — `OnAttackHit @ 1.050s` 1개 |
| 멱등성 재실행 | PASS — no-op, 중복 0 |
| 일회성 메뉴 제거 | 완료 |
| RhinoBreaker 프로필 | `Supported / MeleeContact / 1` |
| 전체 지원 분할 | `15 / 9 / 1` |
| 기존 Unit Action self-validation | PASS |
| 문서 정합성 검사 | PASS — 문제 0건 |
| Android Build And Run | 시작 완료, 완료 감시는 사용자 |
| 사용자 멀티플레이 실기 | 미실행 |

## 사용자 실기 및 로그 판정 — 2026-09-13 추가

| 항목 | 결과 |
|---|---|
| Blue/Red RhinoBreaker 유닛 공격 | PASS — 이전 두 세션에서 양 팀 공격 진입 및 결과 정상 |
| RhinoBreaker 건물 공격 | PASS — `unitId=0 → BuildingId=11`, Accepted commit 뒤 대상 건물 사망 |
| 서버/클라이언트 결과 | PASS — Host 724/실패 0 ↔ Client 724 수락/거부 0 |
| 결과 기반 표현 | PASS — 양쪽 ready 724, 필수 표현 692/692, 중복·전송·표현 실패 0 |
| 이동·공간 회귀 | PASS — gate·writer·handoff·stationary Walk·spatial mismatch·error/drop 0 |
| LittleKnight 대조군 | PASS — 77기 Red 대규모 교전에서 공격 결과 회귀 없음 |
| 실제 접촉 동작과 표현 시점 | CONDITIONAL PASS — 사용자의 명시적 육안 판정 없음 |
| 대상 사망 직후 개별 Rhino 전환 | CONDITIONAL PASS — 대규모 사망은 관측했으나 개별 전환 명시 없음 |
| UnitView 초기화 지연 | 관찰 유지 — Client 107건 모두 completion, retry failure/exhaustion 0 |
| Root pose observer 상한 | 관찰 유지 — 양쪽 maxUnits=64 경고 1건, 상한 이후 범위 밖 |

이번 추가 테스트는 새 빌드가 아니므로 `U+27F3` 경고 2건은 문자 제거 미반영 기존 APK에서 발생한 것이다. 현재 소스의 `새로고침` 문구는 다음 작업 뒤 생성하는 통합 빌드에서 확인한다. 전체 25종·역할교대·Legacy rollback 완료로 확대하지 않으며, Task는 **CONDITIONAL PASS / OPEN**으로 유지한다.
