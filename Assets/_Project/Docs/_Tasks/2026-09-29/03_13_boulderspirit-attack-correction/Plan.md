# BoulderSpirit 공격 생산 경로 교정 — Plan

이번에는 BoulderSpirit의 타격 순간을 현재 1:15보다 5프레임 앞선 **1:10**으로 시험하려 한다. 아래 1.50초 구현·실기 기록은 당시 결과로 남기고, 새 변경은 문서로 먼저 공유해 승인받은 다음에만 적용한다. LittleKnight 이동 실패는 별도 문제여서 이 타격 시점 변경에 섞지 않는다.

> **최신 상태(2026-09-29, 승인된 1:10 사용자 육안 수용 후):** 30fps `1:10 = 40/30초 ≈ 1.3333333초`가 생산 클립·type 14 설정·영구 gate에 저장됐고 `StatsReference.md`도 `1:10`이다. Unity 두 self-validation PASS 뒤 Android Host/Editor Client 새 경기에서 혼합 2종 공격 결과·표현이 수렴했고, 사용자가 새 타격을 **“육안상 문제없었어”**라고 수용했다. **BoulderSpirit focused PASS/CLOSED**; 단독 정밀 시차·전체 migration은 미검증이다. 아래 1.15→1.50 계획·1.50 실기는 이력이며 맨 아래 최신 판정을 따른다. 기존 로직 제거 없음.

BoulderSpirit의 공격이 문서에 적힌 타격 순간에 맞도록 고친다. 사용자가 `1:15`를 30fps의 초:프레임 표기로 확정했으므로 타격 시각은 **1.50초**다. 현재 애니메이션과 설정에 저장된 1.15초를 함께 바로잡고, 실제 경기에서 쓰는 클립 연결과 자동 검사도 명확히 한다.

> **2026-09-29 실기 후 최신 상태:** 아래 승인 계획의 1.15초는 교정 전 값이다. 현재 저장값은 1.50초이며, 사용자는 BoulderSpirit 화면 타격이 약간 늦지만 수용 가능하다고 했다. 경기 전체 공격 결과/표현은 수렴했다. 다만 Host LittleKnight 이동 gate는 FAIL이므로 전체 경기 PASS는 아니며, 정확한 BoulderSpirit 단독 1.50초 동기화도 미계측이다. 맨 아래 실기 결과 참조.

## 전제·범위

- Research 기준 현재 저장값은 30fps·4초 루프 Attack 클립의 `OnAttackHit` **1 @ 1.15초**, type 14 config `hitFrameTimes=[1.15]`·cooldown 4초다. 사용자 결정으로 `StatsReference.md`의 `1:15(4:00)`은 **1.50초 타격·4초 주기**이며 현재 두 1.15초 값은 의도보다 0.35초 이르다. 이번 승인 범위는 BoulderSpirit의 단일 marker와 type 14 설정만 1.50초로 함께 교정하고 주기 4초는 유지하는 것이다.
- **기존 로직 제거 없음.** `UnitFactory` 공용 fallback, 서버 타이머 피해, Shadow/Coordinator, Legacy rollback을 유지한다. 신규 공격 VFX/SFX 제작, 스탯 HP 정정, 규칙 문서 수정, Testcase/QA 작성은 이 단계에서 하지 않는다.
- 사용자의 이번 **“그에 따라 plan 수정하고 구현 및 빌드시도 진행해”**는 1.50초 교정 Plan의 구현·조건부 빌드 시도를 승인한 지시다. document-manager의 이번 편집은 Research.md/Plan.md 두 파일에 한정하며, 코드·씬·에셋 수정과 Unity 검증·빌드 실행은 후속 구현 담당 단계다. 실행 전 PASS나 빌드 완료를 주장하지 않는다.

## 승인된 수정 항목

| 항목 / 예상 수정 파일 | 방법 | 규칙 근거 | 위험과 완료 gate |
|---|---|---|---|
| 타격 marker — `Assets/_Project/Animations/Units/BoulderSpirit/BoulderSpirit_Attack.anim` | 기존 **단일** `OnAttackHit`의 time `1.15 → 1.50초`만 교정한다. 30fps·4초 길이·loop와 다른 이벤트/곡선은 보존한다. | `GameSystemRules_Units.md` 규칙 17(Attack 이벤트 단일 출처), 규칙 18(서버 타이머 피해); `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001/002` | 이벤트 추가·삭제, 잘못된 clip 수정 위험. 실제 `Base Layer/Attack` motion clip에 `OnAttackHit` 하나가 정확히 1.50초인지 재로드 검증한다. |
| 설정 폴백 — `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | 실제 직렬화 **`unitType: 14`** 단일 행의 `hitFrameTimes [1.15] → [1.50]`만 변경한다. `attackCooldown: 4`와 다른 스탯·유닛 행은 유지한다. | `GameSystemRules_Units.md` 규칙 17·18; `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-002` | 과거 `enumValueIndex` 오류로 다른 행이 변경된 전례가 있다. 실제 `intValue`/직렬화 14를 사용해 행 유일성, 변경 전후 인접·대조 유닛 불변, 저장·재로드 값을 확인한다. |
| 생산 clip 명시 — `Assets/_Project/Scenes/Game.unity` | `UnitFactory._spiritPrefabs`의 **type 14** Blue/Red 참조는 유지하고 `attackTimelineClip`에 실제 `BoulderSpirit_Attack.anim`을 연결한다. `_humanPrefabs`의 같은 숫자 행과 혼동하지 않는다. | `GameSystemRules_Units.md` 규칙 17(타격 이벤트 단일 출처); `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-002`(검증된 AttackTimeline) | 씬 저장 누락·다른 종족 행 오수정·GUID 오연결. Editor에서 실제 Game 씬 type 14 단일 Spirit 행과 두 prefab/clip identity를 재로드 대조한다. |
| 읽기 전용 production gate — `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | BoulderSpirit 전용 gate를 기존 self-validation에 등록한다. 실제 `Base Layer/Attack` state motion·speed, 클립 30fps/4초/loop/단일 `OnAttackHit @ 1.50`, Blue·Red Animator controller와 relay, Game 씬 type 14 양 팀 prefab·명시 clip, config type 14 단일 행의 cooldown 4초/[1.50]을 함께 읽어 검사한다. 명시 참조가 controller clip 열거 순서에 흔들리지 않는지도 확인한다. 검사만 하고 에셋은 자동 교정하지 않는다. | `GameSystemRules_Units.md` 규칙 17·18; `GameSystemRules_UnitCombatSynchronization.md` `NET-ACTION-SEQ`, `NET-PRESENT-001/002`, `NET-ACTION-IDEMPOTENT` | 잘못된 enum 행 선택은 BoulderSpirit을 다시 손상시킬 수 있다. 직렬화 **`intValue`/실제 type 14**로 찾고 중복·누락·불일치를 fail-closed로 처리한다. 이 gate의 PASS는 실기 접촉 시점 PASS가 아니다. |

위 네 파일 외에 `BoulderSpirit.controller`, Blue/Red prefab, `UnitFactory.cs`, 전투/표현 엔진의 동작은 수정하지 않는다. 다른 유닛의 `분:프레임` 표기와 저장값은 별도 감사 대상이며 **일괄 수정하지 않는다**. 자동 gate에서 승인 범위 밖 차이가 드러나면 원인과 저장값을 먼저 기록하고 추가 범위를 확인한다. 실제 공격 VFX/SFX는 설정되지 않았고 목록상의 이름만으로 제작·연결을 승인받은 것으로 보지 않는다.

## 검증 순서와 미완 경계

1. 승인된 구현 후 실제 생산 scene→양 팀 prefab→controller→정확한 Attack state clip→marker→config를 읽는 gate와 기존 Unit Action self-validation을 통과시킨다. `Hexiege/Combat/Run Unit Action Self Validation`에서 PASS·Console Error 0을 확인한다.
2. `Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit`도 PASS·Console Error 0을 확인한다. **두 Unity self-validation PASS 후에만** 이번 사용자 지시에 따라 Android 빌드를 **시도**한다. 빌드 완료·기기 실기 확인은 사용자 몫이며, 빌드 시작만으로 완료나 실기 PASS를 주장하지 않는다.
3. 후속 실제 경기에서는 BoulderSpirit 생산, 공격 시작 Ready/commit, 단일 `HitIndex=0` 결과와 Host/Client 필수 표현을 같은 회차로 대조한다. 반복 공격, 타겟 교체·공격자/타겟 사망·Stop, marker 누락·중복, 화면 접촉 시각은 각각 확인한다(`NET-ACTION-SEQ`, `NET-PRESENT-002/003`, `NET-CANCEL-003~005`, 유닛 규칙 18·19). 경기 전체 결과 건수를 BoulderSpirit 단독 건수로 쓰지 않는다.

정적 값 일치·자동 검증만으로 사용자 육안 타이밍 수용, v2 Complete, 25종 전체, Host/Client 역할교대 또는 Legacy rollback을 완료로 표시하지 않는다. `StatsReference.md`의 `1:15`는 사용자가 **1.50초로 확정**했으며 이 Plan은 그 결정만 BoulderSpirit에 적용한다.

## 2026-09-29 실기 결과와 잔여 gate

사용자는 교정된 BoulderSpirit 공격이 화면에서 약간 늦어 보이지만 수용 가능하다고 평가했다. 현재 clip의 단일 marker와 type 14 설정은 모두 1.50초, 주기는 4초다. 동일 경기 `sharedSessionKey=06d034abf18c778a02cf56c57c1b8cdf81d9b8f46a74aba5e1a4356161e1a6c4`에서 Host BoulderSpirit 생산 및 공격 시작 Ready/Accepted를 확인했다. Host 공격 결과 453/실패 0 ↔ Android Client 453 수락/거부 0, 양쪽 필수 표현 414/414·실패 0, ROOT local PASS/errors 0이다. **453·414는 BoulderSpirit+LittleKnight 경기 전체 수치**이며 정확한 BoulderSpirit 단독 marker→피해 1.50초·subframe 동기화의 계측값이 아니다. 근거: `Assets/_Project/Docs/_Logs/_editor/2026-09-29/RuntimeLog.txt` 12:43~12:45 및 `Assets/_Project/Docs/_Logs/2026-09-29/12_47_logcat/RuntimeLog_device.txt`의 동일 세션 END.

**분리된 FAIL:** Host MOVE-AUTH `adapterFailures=4`, UnitId 16/19/21/26은 모두 LittleKnight다. `post-combat-no-safe-forward-center`의 `RejectedInvalidPath/Unreachable` `(5,15)→(6,15)`이며 이번 통합 경기의 이동 gate는 FAIL이다. BoulderSpirit 타격 지연의 원인으로 추정하지 않는다(FPS/frame-time 측정 없음). LittleKnight 이동 결함은 별도 진단·교정과 재경기 gate가 필요하다. 이 Task에서 코드·에셋·Testcase/QA를 추가 변경하지 않는다. 공식 cross-audit, 정확한 공격별 시차, 25종·역할교대·Legacy rollback은 여전히 미완; **BoulderSpirit focused 사용자 수용 ≠ 전체 세션 PASS ≠ v2 Complete**다.

## 2026-09-29 새 1:10 교정 제안 — 사용자 공유·구현 승인 대기

**변경 이유와 경계:** 사용자가 이전 1:15(1.50초) 실기를 수용한 뒤, 시각적 타격점을 다시 시험하려고 **1:10(40/30초 ≈ 1.3333333초)**을 새 목표로 정했다. 이전 승인·구현·실기 수치는 당시 이력이며 새 목표의 검증 결과가 아니다. 4초 공격 주기/클립 길이/loop, 기존 scene의 명시 clip binding, controller, Blue·Red prefab과 다른 유닛은 그대로 둔다. **기존 로직 제거·비활성화 없음.** LittleKnight MOVE-AUTH FAIL은 이 범위 밖의 독립 진단 대상이다.

| 승인 후 예상 수정 파일 | 정확한 제안 | 규칙 근거 | 위험·완료 gate |
|---|---|---|---|
| `Assets/_Project/Animations/Units/BoulderSpirit/BoulderSpirit_Attack.anim` | 실제 `Base Layer/Attack` 클립의 **단일** `OnAttackHit` time만 `1.5 → 1.3333333초`(30fps frame 40)로 이동한다. 4초 길이·30fps·loop·곡선은 보존한다. | `GameSystemRules_Units.md` 규칙 17(Attack event 단일 출처), 규칙 18(서버 타이머 피해); `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001/002` | 잘못된 클립이나 추가 이벤트 변경 위험. 저장·재로드 뒤 이벤트 개수 1, 40/30초, production Attack state motion identity를 검사한다. |
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | 실제 직렬화 **`unitType: 14`**의 `hitFrameTimes: [1.5] → [1.3333333]`만 변경한다. `attackCooldown: 4` 및 다른 모든 필드는 보존한다. | 유닛 규칙 17·18, 동기화 `NET-PRESENT-002` | enum 선언 순서(`enumValueIndex`) 오선택 전례. `intValue`/직렬화 type 14 단일 행을 찾고 인접 행 불변과 재로드 결과를 확인한다. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | **기존** BoulderSpirit production gate의 단일 marker/config 기대값 `1.5 → 40/30초`로 갱신한다. scene→양 팀 prefab→controller→Attack clip·30fps·4초→type 14 config 동일성·fail-closed 성질은 유지한다. | 유닛 규칙 17·18, 동기화 `NET-PRESENT-002`, `NET-ACTION-SEQ` | gate만 바꾸거나 에셋만 바꾸면 거짓 PASS/FAIL. 생산 자산 전체를 다시 읽는 Unit Action self-validation PASS를 요구한다. |
| `Assets/_Project/Docs/StatsReference.md` BoulderSpirit 행 | 사용자 새 의도에 따라 `1:15(4:00) → 1:10(4:00)`로 **구현과 함께** 동기화한다. 이번 Plan 편집에서는 아직 변경하지 않는다. | 유닛 규칙 17·18 및 사용자 확정 30fps 표기 | 현재 구현값과 미래 계획값을 혼동하지 않도록 에셋 저장·자동 검증과 맞춰 반영한다. |

**변경하지 않을 파일:** `Assets/_Project/Scenes/Game.unity`의 기존 명시 binding, `BoulderSpirit.controller`, Blue/Red prefab, `UnitFactory.cs`, 이동/전투/표현 공통 코드, 다른 유닛의 marker/config. 구현 후 현황 문서·매트릭스는 실제 저장·검증 상태를 확인한 **별도 문서 반영 단계**에서 갱신한다. 타격 시점 5프레임 이동만으로 이동 FAIL이나 체감 지연이 해결된다고 주장하지 않는다.

**승인 후 검증 순서:** 실제 저장값 대조 → `Hexiege/Combat/Run Unit Action Self Validation` 및 `Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit` **두 메뉴 PASS·Console Error 0**을 확인한다. **빌드 시도는 이번 1:10 계획의 범위 밖이며 사용자가 별도로 요청할 때만 진행한다.** 후속 실기가 별도로 진행되면 새 Editor Host/Android Client 같은 세션에서 BoulderSpirit 실제 생산·공격 시작·회차별 결과/표현과 사용자 화면 수용을 확인한다. 경기 전체 집계를 BoulderSpirit 단독 시차 측정으로 대체하지 않는다. LittleKnight 이동 gate는 별도 원인 조사·재현·교정·재경기 전까지 **FAIL/OPEN**이며 후속 공격 실기의 전체 경기 PASS 근거로 쓰지 않는다.

**별도 FAIL의 현재 설명:** Host 로그의 LittleKnight 16/19/21/26에서 전투 후 안전한 전방 복귀 타일 탐색이 실패하고, 권위 경로 재탐색 `(5,15)→(6,15)`도 `Unreachable`이라 후보가 null이 됐다. `UnitRepathProgressGuard.Evaluate`의 경로 유효성 검사에서 `RejectedInvalidPath`가 나와 `UnitView.TryAcceptRepathDecision`이 `MarkBlocked()`와 adapter-failure를 기록했고 Host MOVE-AUTH END가 FAIL이다. 확인된 것은 **이 직접 실패 경로**까지다. 실제 차단 타일·환경 변경·지형/점유의 어느 조건이 원인인지는 저장 trace만으로 특정하지 못했고, 반복 재현 루프도 이번 문서 작업에는 없다. 근거: `Assets/_Project/Docs/_Logs/_editor/2026-09-29/RuntimeLog.txt` 동일 key의 생산/12:45:21~23 failure/END, `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 post-combat 분기·repath 처리, `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`의 forward 후보 조건, `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs`의 경로 서명 검사.

## 2026-09-29 승인된 1:10 구현 상태 — 자동 검증 대기

승인된 네 항목 중 저장값은 직접 확인했다. `BoulderSpirit_Attack.anim`의 **단일 marker 1.3333333초**, `UnitStatsConfig.asset`의 **type 14 `[1.3333333]`·cooldown 4**, `RunUnitActionSelfValidation.cs` 기존 Boulder gate의 **marker/config 1.3333333f(40/30초) 기대값**, `StatsReference.md` Boulder 행의 **`1:10(4:00)`**이 현재 상태다. 생산 clip의 30fps·4초는 유지하고 이전 명시 scene binding·양 팀 prefab/controller는 이 변경에서 건드리지 않았다. 위 「승인 대기」 제목·제안 문구는 이 상태 이전의 계획 이력으로 읽는다.

**다음 gate:** 메인 담당의 `Hexiege/Combat/Run Unit Action Self Validation`과 `Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit` 실행·PASS 여부 확인이 필요하다. **아직 PASS를 기록할 근거가 없고, 빌드 시도는 별도 사용자 요청 전 범위 밖**이다. 새 1:10의 Host/Client 결과나 육안 타이밍도 미검증이다. 1.50초 세션의 사용자 수용·453/414 경기 전체 수치와 LittleKnight MOVE-AUTH FAIL은 과거 버전/별도 결함 근거로 유지한다. 전체 25종·역할교대·rollback 또는 v2 Complete는 여전히 미완이다.

## 2026-09-29 1:10 최신 gate 상태 — 빌드 완료·실기 대기

위 ‘다음 gate’는 메뉴 실행 전 기록이다. 메인 세션 관찰상 처음 두 Unit Action 시도는 Assets Refresh/domain reload 전 낡은 **1.5초 검증 메시지**로 실패했다. Refresh·재컴파일 뒤 새 도메인에서 약 13:21 Unit Action self-validation **PASS**, 약 13:22 Root Pose Cross Audit self-validation **PASS**가 확인됐다. 따라서 두 Unity 메뉴 gate는 최신 도메인 기준 통과로 갱신하되, 초기 실패 이력을 삭제하지 않는다. Docs 에셋 관련 일시적 `[Worker1] Import Error Code(4)` 경고가 있었고 reload 뒤 새 Console 오류는 관찰되지 않았으나, 과거 오류 한 건이 Console 이력에 남아 있어 전역 `Console Error 0`으로 표기하지 않는다.

이후 사용자 요청 범위에서 `File > Build And Run`을 눌렀고 `Checking prerequisites / Starting Android build` 화면이 확인됐다. **빌드 시도 시작만 확인**했으며 완료·설치·기기 경기 PASS는 미확인이고 이 문서 작업에서 후속 모니터링하지 않는다. 새 1:10의 사용자 화면 수용과 Host/Client 공격별 결과·표현은 후속 실기 gate다. 1.50초 버전의 focused 수용과 경기 전체 453/414는 이 gate를 대체하지 않는다. LittleKnight Host MOVE-AUTH 4건 FAIL은 별도 OPEN; 전체 25종·역할교대·Legacy rollback/v2 Complete 역시 미완이다.

## 2026-09-29 1:10 후속 경기 판정 — 육안 확인 대기

위 ‘기기 경기 미확인’은 빌드 시작 당시 기록이다. 이후 새 `sharedSessionKey=0f0bbb3247a104cc839894f0c181ca05f7ba6960a415ba3be12f0662d923eb60`에서 **Android PID 8899 Host / Editor Client**가 같은 경기를 기록했다. Host 생산 BoulderSpirit **7기**, LittleKnight 혼합 **2/25종**이다. Host 공격 `results=283/실패 0`, `ready=283`, `commits=168` ↔ Client `accepted=283/rejected=0`, `ready=283`; 양쪽 필수 표현 **265/265·실패 0**, spatial mismatch 0이다. Host MOVE core·adapterFailures 0, 양쪽 ROOT **local PASS/errors 0**이다. 해당 PID의 Unity E/F 0, Editor 18시대 ERROR/FATAL/Exception 0이다. 출처: `Assets/_Project/Docs/_Logs/2026-09-29/18_13_logcat/RuntimeLog_device.txt`의 PID 8899 생산·END와 `Assets/_Project/Docs/_Logs/_editor/2026-09-29/RuntimeLog.txt`의 동일 key END.

**남은 gate:** 위 283/265/168은 **BoulderSpirit+LittleKnight 경기 전체** 수치이므로 BoulderSpirit 단독 marker→Impact `40/30초`의 정확한 차이 또는 사용자 화면 타이밍 수용을 입증하지 않는다. 새 1:10의 육안 평가는 사용자 대기다. StreamSpirit/Fox의 미생산 starts 0 `INCONCLUSIVE`는 이 focused 판정과 무관하다. 이전 LittleKnight Host MOVE adapterFailures 4건은 이번 경기에서 재발하지 않았지만 원인·수정 증명 없이 **별도 OPEN**이다. 기존 1.50초 화면 수용과 453/414도 과거 버전에만 해당한다. 이 문서에서 빌드 완료나 공식 cross-audit PASS, 25종·역할교대·Legacy rollback/v2 Complete를 주장하지 않는다.

## 2026-09-29 1:10 사용자 수용 후 최종 범위 판정

위 ‘육안 확인 대기’는 사용자 응답 전 상태다. 사용자는 **“BoulderSpirit 1:10 타격 육안상 문제없었어”**라고 확인했다. 따라서 이 Plan의 BoulderSpirit 공격 타이밍 **focused gate는 PASS/CLOSED**다. 이전 1.50초 테스트의 수용과 새 1:10 수용은 서로 다른 버전의 이력으로 분리한다. 혼합 경기 283 결과·265/265 표현·Host MOVE adapterFailures 0·양쪽 local ROOT PASS는 새 버전의 focused 로그 근거이나 BoulderSpirit 단독 marker→피해 정밀 offset, 공식 cross-audit 또는 전체 25종·역할교대·Legacy rollback/v2 Complete를 증명하지 않는다. 과거 LittleKnight MOVE 4건은 재발하지 않았을 뿐 원인 미확정으로 별도 OPEN이다. 여기서 이동 수정 계획이나 다음 유닛 Task를 확정하지 않는다.
