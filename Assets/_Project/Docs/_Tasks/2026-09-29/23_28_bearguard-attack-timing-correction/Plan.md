# BearGuard 공격 타이밍 교정 — Plan

**최신 판정(2026-09-30): 0:13 focused 실기 PASS/CLOSED.** 사용자가 BearGuard 공격을 육안상 문제없다고 수용했다. 재테스트 `b986fbbcd52462ab5d1c65e4d6699d1fb9f32339bdc574d130c8ee0761d7d94b`의 2/25 혼합 경기는 경기 전체 Host/Client 결과 641/641·필수 표현 각 608/608·실패 0, 공간 mismatch 0, Host MOVE adapterFailures 0, 양 peer local ROOT PASS였다. 첫 경기의 ROOT INCOMPLETE와 혼합하지 않는다. 아래 2026-09-29 “실기 전”은 당시 이력이다. BearGuard 단독 정밀 타이밍, 공식 CrossAudit Analyze, LittleKnight 이전 MOVE 4건, 설계 주기/실제 cooldown 차이, 전체 migration은 여전히 OPEN이다.

BearGuard의 타격 표식을 사용자가 지정한 30fps `0:13`으로 옮기고 실제 게임의 클립·설정을 맞추는 계획과 진행 기록이다. **최신 상태(2026-09-29):** 저장된 `0.43333334초` 타격값·Game 씬 명시 연결·영구 생산 gate에 대해 Unity Unit Action self-validation(23:43:36)과 Root Pose Cross Audit self-validation(23:45:06)이 각각 PASS했다. `File > Build And Run` 후 `Checking prerequisites / Starting Android build` 화면까지만 확인했다. **빌드 완료·설치·실기·화면 수용은 미확인**이며 이 Plan의 과거 “미실행” 서술은 검증 전 이력이다.

**이전 상태(2026-09-29, 구현 직후):** 클립·type 20 설정의 `0.43333334초` 저장, Game 씬 명시 Attack clip 연결, BearGuard 영구 생산 gate 추가까지 구현됐다. 당시 Unity 자동 재컴파일 중이며 두 self-validation과 Build And Run은 아직 실행·확인되지 않았다. 아래 `0.2초`와 미구현 표현은 **계획 작성 당시의 이력**이다.

**기존 로직 제거 없음.** 서버 권위 피해 타이머, 공통 C2/C3 결과·표현 소유권, BearGuard `Supported / MeleeContact / Impact 1` 프로필과 Blue/Red prefab/controller는 유지한다. LittleKnight 이동 코드는 이번 범위에서 수정하지 않는다.

## 목표와 범위

- 타격 이벤트는 `0:13 = 13/30 ≈ 0.433333333초` 1개다. 클립 30fps, 약 1.6666666초 길이와 loop 설정은 유지한다. 후속 구현은 사용자 목표를 현재 **디스크의 단일 `OnAttackHit @ 0.2초`에 저장 반영**하는 것이며 별도 Inspector 질문이나 클립 재확인 승인을 요구하지 않는다.
- `UnitStatsConfig.asset` 실제 `unitType: 20`의 단일 `hitFrameTimes`를 `[0.2] → [13/30]`으로 맞춘다. `attackCooldown: 1.2`는 보존한다. `StatsReference.md` BearGuard의 **타격 표기만** 구 `0:20`에서 새 목표 `0:13`으로 정정할 계획이다. 괄호의 설계 주기 `1:20 = 1.666666…초`와 실제 cooldown `1.2초`의 불일치는 명시적으로 OPEN/범위 밖이며 어느 쪽도 임의로 조정하지 않는다.
- `Game.unity` **UnitFactory** `_transcendencePrefabs`의 실제 `type: 20` 등록에 BearGuard Attack clip을 `attackTimelineClip`으로 명시한다. 주변 타입, 같은 씬의 다른 type 20 배열, 양 팀 prefab 참조는 바꾸지 않는다. 영구 Unit Action self-validation에 production clip/controller/양 팀 prefab/scene/config gate를 추가한다.
- 사용자가 task 작성 뒤 구현과 **빌드 시도**를 승인했다. 구현 완료·Unity 메뉴 PASS·빌드 시작·빌드 완료·설치·기기 실기는 각각 실제 확인한 단계만 후속 기록한다. 빌드 완료 감시와 판정은 사용자 몫이며, 이 Plan에서 PASS를 선기록하지 않는다. 새 Testcase/QA 문서는 생성하지 않는다.

## 승인 후 예상 수정 항목과 완료 gate

| 파일 | 제안 변경 | GameSystemRules 근거 | 주요 위험 → 완료 gate |
|---|---|---|---|
| `Assets/_Project/Animations/Units/BearGuard/BearGuard_Attack.anim` | 디스크의 단일 `OnAttackHit` `0.2 → 13/30`초로 저장. 다른 curve, 30fps, 약 1.6666666초 stop time, loop 유지. | `GameSystemRules_Units.md` 규칙 17(실제 Attack 이벤트가 타격 시각 출처), 규칙 18(이벤트가 직접 피해를 적용하지 않음), `NET-PRESENT-001/002`(표식 역할·타임라인 일치). | Unity 보고만 믿고 디스크 옛 값을 남기거나 이벤트를 중복 생성할 위험 → 저장된 실제 clip에 `OnAttackHit` 정확히 1개 @ `13/30`, 다른 clip 속성 불변 확인. |
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | 실제 직렬화 type 20의 `hitFrameTimes`만 `[0.2] → [13/30]`; cooldown `1.2`와 타 타입 행 보존. | 규칙 17(클립 없을 때 폴백과 생산 타격점 정합), 규칙 18(서버 타이머 권위), `NET-PRESENT-002`(검증 타임라인). | enum 배열 순서 오인·주기 자동 변경 위험 → type 값 20을 직접 대조하고 타격값만 일치, cooldown 1.2와 타 행 불변 확인. |
| `Assets/_Project/Scenes/Game.unity` | UnitFactory `_transcendencePrefabs` type 20에 Attack clip GUID `6cd4a1668d01f24448599b56306f2b13`의 `attackTimelineClip`을 명시. | 규칙 17(실제 생산 Attack 클립에서 이벤트 추출), `NET-PRESENT-002`(검증 타임라인·완성 유닛 marker 일치). | 같은 씬의 다른 type 20 등록이나 인접 초월계 행 오배선 위험 → UnitFactory 한 등록만 수정하고 Blue/Red prefab·Controller `Base Layer/Attack` motion GUID와 명시 clip의 일치 확인. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | BearGuard 전용 영구 production gate 호출·구현: 실제 scene type 20, Blue/Red prefab·relay·동일 controller, `Base Layer/Attack` motion, 단일 marker `13/30`, 30fps/길이/loop, config type 20의 marker 폴백을 fail-closed 대조. cooldown은 변경 대상이 아니므로 현재 1.2 보존 여부만 확인하고 설계 1:20과의 일치를 강제하지 않음. | 규칙 17·18, `NET-PRESENT-001/002`(서버 권위 유지·생산 marker와 검증 타임라인 결합). | fixture만 통과하고 생산 씬 오배선을 놓칠 위험 → 정상 production 연결 PASS, marker 개수/시각·clip identity·team/controller/relay·config 불일치 시 각각 실패하는 gate; 새 Unity 메뉴 결과를 별도로 확인. |
| `Assets/_Project/Docs/StatsReference.md` | BearGuard 타격 표기 `0:20 → 0:13` 정정. 괄호의 `1:20`은 현재 설계 주기 이력으로 보존하고 실제 cooldown 1.2와 미해결 차이를 명시. | 규칙 17(설계 타격점과 실제 Attack 이벤트의 대조), 규칙 18(주기 수치와 피해 권위를 혼동하지 않음). | `1:20`까지 승인 없이 바꿔 밸런스 결정을 대신할 위험 → 타격 표기만 새 목표로 정정, 주기 미해결을 숨기지 않음. |
| `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` | 실제 저장·검증 후 BearGuard 현재 marker/config·명시 연결·검증 단계만 갱신. `MigrationRequired`와 별도 주기 차이는 유지. | 규칙 17·18 및 `NET-PRESENT-002`의 실제 공격 타임라인 상태를 감사표에 반영. | 계획값을 검증 완료로 선기록할 위험 → 저장 파일·Unity 메뉴·실기 각각 관찰한 것만 기재하고 미완 gate 분리. |

## 구현·검증 순서와 판정 경계

1. 현재 디스크의 `0.2초` 이벤트와 사용자 목표 `13/30초`의 차이를 기준으로 Attack clip의 **한 이벤트만** 저장 교정한다. type 20 config 폴백을 맞추고 실제 UnitFactory 등록에 동일 clip GUID를 연결한다. prefab/controller·공통 공격 로직·cooldown은 보존한다.
2. 영구 production gate는 clip·controller `Base Layer/Attack`·양 팀 prefab/relay·type 20 scene/config를 함께 읽고 불일치 시 실패하게 한다. 일반 resolver/fixture 테스트만으로 이 경계를 통과했다고 보지 않는다. Unity 재컴파일 후 Unit Action 및 Root Pose Cross Audit self-validation 결과를 각각 확인하되, 현재는 어느 메뉴도 새 값으로 PASS했다고 기록하지 않는다.
3. 문서 정합성을 검사한 뒤 승인된 Android Build And Run **시도/시작**을 진행한다. 성공·실패·설치·실제 경기의 완료를 감시해 선판정하지 않는다. 사용자가 후속 실기를 제공하면 BearGuard와 LittleKnight 혼합 경기에서 실제 생산, Host/Client 결과·필수 표현, 공간 불일치, Host MOVE adapterFailures/`Unreachable`, 사용자 타격 화면을 구분해 본다. LittleKnight 이전 MOVE FAIL 4건은 미재발만으로 닫지 않는다.
4. 새 marker와 폴백 설정의 일치는 **구성 검증**이다. 정확한 marker→권위 피해 offset이나 화면 동시성, 공식 CrossAudit, 25종 전체·역할교대·Legacy rollback/규칙 v2 완료를 자동으로 의미하지 않는다. 주기 `1:20` 설계 대 실제 cooldown `1.2` 불일치는 별도 후속 결정 전 OPEN이다.

## 2026-09-29 구현 결과와 다음 gate

| 계획 항목 | 현재 저장 상태 | 남은 완료 gate |
|---|---|---|
| Attack clip·type 20 설정 | 생산 클립의 단일 `OnAttackHit`와 `UnitStatsConfig.asset`의 단일 `hitFrameTimes`가 각각 `0.43333334초`로 저장됨. 30fps·약 1.6666666초 clip/loop, cooldown `1.2` 유지. | 재컴파일 후 Unity Unit Action production gate 확인. 정확한 화면 타격/권위 피해 동시성은 별도 실기 근거 필요. |
| Game 씬·영구 gate | UnitFactory `_transcendencePrefabs` type 20에 Attack GUID `6cd4a1668d01f24448599b56306f2b13` 명시 연결. BearGuard production clip/controller/Blue·Red prefab·relay/scene/config 검사 코드 추가. | Unit Action 및 Root Pose Cross Audit self-validation 각각 실행·결과 확인. 코드 추가만으로 PASS 아님. |
| 설계·감사 문서 | StatsReference 타격을 `0:13(1:20)`으로, 감사표를 실제 저장값과 검증 대기로 갱신. | `1:20 = 1.666666…초` 설계 주기와 실제 cooldown `1.2초` 차이는 별도 결정 전 OPEN. |
| 빌드·실기 | 이 기록 시점 Build And Run 미실행, 새 경기·사용자 화면 판정 없음. | 승인된 빌드 **시도** 이후 완료·설치 확인은 사용자 소유. LittleKnight 혼합 비교 및 이전 MOVE 실패는 별도로 평가. 전체 25종/역할교대/rollback·공식 CrossAudit 미완. |

## 2026-09-29 Unity PASS·빌드 시작 화면 후속

- 재컴파일 후 Unit Action 메뉴 실행 결과 23:43:36 `[UAS-DIAG] self-validation PASS`, Root Pose Cross Audit 메뉴 실행 결과 23:45:06 `[UAS-ROOT-CROSS-AUDIT] self-validation PASS`를 Console에서 확인했다. 위 표의 Unity 대기 gate는 이 두 메뉴에 한해 충족됐다. 생산 타격의 기기 시각·정확한 marker→권위 피해 offset을 입증한 것은 아니다.
- `File > Build And Run` 클릭 후 `Checking prerequisites / Starting Android build` UI만 확인했다. 빌드 완료·설치·경기·사용자 육안 판정은 확인하지 않으며 완료 감시는 사용자 몫이다. LittleKnight 이동 FAIL 4건과 설계 주기 `1:20` 대 실제 cooldown `1.2초` 차이 및 공식 CrossAudit·25종/역할교대/rollback은 OPEN이다.

## 2026-09-30 focused 완료 판정과 잔여 경계

- 첫 `cc278c50…b461819` 경기의 Host/Client ROOT `INCOMPLETE`는 관찰 종료 00:18:45/48이 첫 LittleKnight 00:20:04·BearGuard 00:20:12보다 앞선 결과다. 이 경기에서 ROOT PASS를 주장하지 않으며 수치를 재테스트에 더하지 않는다.
- 재테스트 `b986fbbc…61d7d94b`는 BearGuard 11·LittleKnight 49, 2/25종의 단일 Editor Host/Android Client 경기다. Host 641 결과/실패 0 ↔ Client 수락 641/거부 0; 필수 표현 양쪽 608/608·실패 0; 공간 mismatch 0; Host MOVE adapter/gate/stationaryWalk 실패 0, recoverable repaths 5·반복/치명 0; Client attackEntry gap/order 0. 양쪽 local ROOT PASS·coveragePassed=True·errors/logDrop 0, 각 60기 추적/안정 endpoint 58·회전 근거 58이었다. 원본·상세 수치는 Research 최신 절에 둔다.
- 사용자 육안 수용을 더해 BearGuard **0:13 공격 시각 focused gate만 PASS/CLOSED**로 기록한다. 경기 전체 집계에서 BearGuard 단독 marker→권위 피해 간격이나 exact frame 동시성을 역산하지 않는다. 공식 `Analyze Latest Unit Root Pose Logs`는 미실행이며 기존 Unity self-validation과 두 local ROOT PASS로 대신하지 않는다.
- LittleKnight의 이전 Host MOVE adapterFailures 4는 이 경기에서 재발하지 않았어도 별도 OPEN. BearGuard 설계 주기 `1:20 = 1.666666…초` 대 실제 cooldown `1.2초` 차이는 의사결정 전 OPEN. 25종·역할교대·Legacy rollback/규칙 v2 Complete도 OPEN이다. 이 focused 판정 때문에 추가 유닛이나 cooldown 변경을 선정하지 않는다.
