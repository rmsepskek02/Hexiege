# SpearMan 0:25 타격 교정 — Plan

SpearMan의 한 번의 창 찌르기에서 타격이 나타나는 시점을 30fps `0:25`, 곧 `25/30 = 0.8333333초`로 맞출 계획이다. Editor에서 조정·저장했다는 사용자 보고를 목표로 따르며, 디스크에 보이는 `0.24초`는 구현 전 상태로만 취급한다. 실제 저장 클립, 게임 설정, 생산 연결과 안내 표기가 일치하도록 하되 **공격 주기 `2초`는 유지**한다. 이 문서는 검토용 계획이며 구현이나 검증 PASS 기록이 아니다.

**기존 로직 제거 없음.** 단일 근접 타격, 서버 권위 피해 경로, Blue/Red prefab과 controller, 다른 유닛 및 다른 스탯은 유지한다. 투사체·신규 VFX 제작, Testcase/QA 문서와 빌드는 이번 계획 단계의 작업이 아니다.

## 구현 항목과 규칙 근거

| 대상 | 승인 후 할 일 | 근거·완료 조건 |
|---|---|---|
| `Assets/_Project/Animations/Units/SpearMan/SpearMan_Attack.anim` | 기존 `OnAttackHit` **한 개**의 시간만 `0.24 → 0.8333333초`(25/30)로 저장한다. 샘플레이트 30, 2초 길이, loop, 곡선은 보존한다. | `GameSystemRules_Units.md` 규칙 17·18, `NET-PRESENT-001/002`. 최종 디스크 `m_Events`에서 이벤트 개수·함수명·25프레임을 확인한다. Editor 화면의 미반영 상태만으로 완료 처리하지 않는다. |
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | `unitType: 4`의 `hitFrameTimes` 한 값만 `0.8333333`으로 맞춘다. `attackCooldown: 2` 및 나머지 스탯·type은 보존한다. | 규칙 17·18, `U-ATK-TIMELINE`. 프레임 `0:25`를 `0.25초`로 오인하지 않고 실제 type 4 블록에서 검증한다. |
| `Assets/_Project/Scenes/Game.unity` | UnitFactory `_humanPrefabs`의 type 4에 SpearMan Attack clip GUID `4772066a52e78bc4cb4890c02068d0ea`를 `attackTimelineClip`으로 명시한다. Blue/Red prefab 참조는 그대로 둔다. | 규칙 17, `NET-PRESENT-002`. 다른 type 4 필드나 인접 등록을 잘못 고치지 않고 실제 `Base Layer/Attack` motion과 일치시킨다. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | 기존 BearGuard/LionKnight 생산 검증 패턴을 따라 SpearMan 영구 gate를 추가한다. 실제 scene type 4 등록·명시 clip, 두 prefab의 controller/relay, controller `Base Layer/Attack` motion, clip의 정확히 한 `OnAttackHit @ 25/30`, config 한 값과 cooldown 2를 함께 fail-closed 검사한다. | 규칙 17·18, `NET-PRESENT-001/002`. resolver/fixture만 정상인 경우를 생산 PASS로 오판하지 않고 각 연결·시간·이벤트 수 불일치에서 실패해야 한다. |
| `Assets/_Project/Docs/StatsReference.md` | SpearMan 타격 표기만 `0:24(2:00) → 0:25(2:00)`으로 맞춘다. | 규칙 17(실제 타격 프레임), 규칙 18(타격 시각과 공격 주기 분리). 기존 `2:00`과 나머지 행·수치는 유지한다. |
| `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` 및 이 task 문서 | 구현 후 실제 저장·검증 단계가 확인되면 해당 시점의 clip/config/scene/gate 상태만 동기화한다. | 규칙 17·18, `NET-PRESENT-002`. `MigrationRequired`와 VFX 없음은 별도 근거 전 유지하고 계획 단계에서 PASS를 선기록하지 않는다. |

## 순서와 검증 경계

1. 저장 클립의 기존 이벤트 한 개를 25프레임으로 교정하고 type 4 config 및 StatsReference를 함께 정렬한다. 디스크값을 다시 읽어 `0.8333333초`, 단일 marker, cooldown 2와 `0:25(2:00)`을 확인한다.
2. Game 씬 실제 UnitFactory type 4에 clip을 명시하고 영구 생산 gate로 씬→두 prefab→controller Attack state→클립→이벤트→config를 검증한다. 서버 피해 경로와 단일 근접 타격은 바꾸지 않는다.
3. 정적 대조와 Unity 재컴파일 뒤 `Hexiege > Combat > Run Unit Action Self Validation` 및 `Hexiege > Combat > Diagnostics > Self Validate Unit Root Pose Cross Audit` 결과를 **각각 실제 실행 후** 기록한다. 문서 수정 시 `Tools/check_docs.py` 검사 범위를 확인하고 오류 0건을 확인한다. 정적 검사나 self-validation을 실제 화면 타격의 PASS로 확대하지 않는다.
4. 사용자 실기 테스트는 구현·자동 검사 이후 별도 단계다. SpearMan이 실제 생산된 경기의 Host/Client 결과와 화면 타격을 사용자가 확인하기 전 focused 완료로 판정하지 않는다. 공식 CrossAudit, 전체 25종, 역할교대, rollback 및 다른 유닛 결함은 이 교정만으로 완료되지 않는다.

이 단계에서는 위 파일의 **수정 계획만** 작성한다. 사용자 검토 후 메인 세션이 구현을 진행하며, 지금은 Research.md와 Plan.md 외 어떤 파일도 수정하지 않는다.

## 2026-09-30 구현 저장 결과와 남은 검증

위 계획 단계의 미구현 문장은 당시 이력이다. 현재 디스크에는 SpearMan Attack의 단일 `OnAttackHit @ 0.8333333초`(30fps `0:25`), type 4 `hitFrameTimes: [0.8333333]`/`attackCooldown: 2`, Game 씬 `_humanPrefabs` type 4의 명시 Attack clip GUID `4772066a52e78bc4cb4890c02068d0ea`, SpearMan 전용 영구 production gate 코드가 저장돼 있다. StatsReference도 `0:25(2:00)`으로 정렬됐다. 기존 `0.24초`는 구현 전 저장 상태로만 남긴다. 투사체·hit VFX는 추가되지 않았고 `MigrationRequired` 유지다.

이것은 **저장 파일과 검증 코드의 정적 확인**이다. 다음은 Unity 재컴파일 후 Unit Action Self Validation과 Root Pose Cross Audit Self Validation을 각각 실제 실행해 결과를 기록하는 단계이며, 아직 어느 메뉴도 PASS로 판정하지 않는다. 빌드·설치·Host/Client 경기·사용자 육안 확인도 미실행·미확인이다. 그러므로 SpearMan focused 교정은 **OPEN**이고, 공식 CrossAudit·정확한 marker→권위 피해 offset·25종/역할교대/Legacy rollback/v2 전체 완료는 별도 미검증 범위다. 새 Testcase/QA 문서는 만들지 않는다.

## 2026-09-30 자동 검사 결과 — PASS 범위와 후속

위 「남은 검증」의 Unity 미실행 문장은 실행 전 이력이다. 메인 세션이 Windows Unity 6000.3.5f2 재컴파일/domain reload 후 Console 오류 0을 확인하고, 18:19:55 `Run Unit Action Self Validation`의 `[UAS-DIAG] self-validation PASS`와 하위 production timeline PASS, 18:20:48 `Self Validate Unit Root Pose Cross Audit`의 `[UAS-ROOT-CROSS-AUDIT] self-validation PASS`를 확인했다. Console 노란 경고 1건의 원인은 미확인이다. **PASS는 이 두 자동 검사에만 적용**한다.

다음 판단 경계는 실제 SpearMan 생산·공격이 포함된 사용자 경기와 육안 타격 확인이다. 빌드·설치·게임플레이·공식 Root Pose CrossAudit Analyze는 아직 수행되지 않았으며, 정확한 marker→권위 피해 시간차도 미계측이다. focused 타격 교정은 **OPEN**, `MigrationRequired` 유지다. 사용자 요청 없는 Testcase/QA 작성이나 빌드는 진행하지 않는다.

## 2026-09-30 사용자 실기 결과 — focused PASS/CLOSED, migration OPEN

위 실기 대기 문장은 당시 이력이다. 사용자는 SpearMan `0:25` 공격을 육안상 문제없다고 수용했다. 두 역할 교대 경기 모두 SpearMan+LittleKnight **2/25종**만 생산·공격했다. `39af4c6d…52373a` Editor Host/Android Client에서는 SpearMan 47(Blue 23/Red 24)·LittleKnight 48(Blue 24/Red 24)를 생산했고, **경기 전체** Host 597 결과/실패 0 ↔ Client 597 수락/거부 0, 양쪽 C3 ready 597/597·필수 표현 474/474·실패 0, local ROOT 양쪽 PASS였다. `9b0404e3…3a4f` Android Host/Editor Client에서는 SpearMan 28(Blue 14/Red 14)·LittleKnight 31(Blue 15/Red 16)를 생산했고, **경기 전체** Host 298 결과/실패 0 ↔ Client 298 수락/거부 0, 양쪽 C3 ready 298/298·필수 표현 271/271·실패 0, local ROOT 양쪽 PASS였다. 각 Host의 MOVE adapter/gate/writer/handoff failures 0, 경기 시각대 GameLog ERROR/FATAL 0이다. Host SpearMan unitId 2/98은 각각 start gate `Ready`와 commit `Accepted`를 남겼다. 근거: `_Logs/2026-09-30/18_50_logcat/RuntimeLog_device.txt`, `_Logs/_editor/2026-09-30/RuntimeLog.txt`.

이 근거로 **SpearMan 0:25 focused 교정만 PASS/CLOSED**한다. 결과 597/298과 표현 474/271은 혼합 경기 전체이지 SpearMan 단독 집계가 아니다. 정확한 marker→권위 피해 offset, 공식 CrossAudit Analyze, 25종 전체·전면 역할교대·Legacy rollback/v2 migration은 미검증이므로 **`MigrationRequired`는 OPEN**이다. 과거 LionKnight C3 만료 7건·LittleKnight MOVE 4건은 별도 OPEN이며 이 두 경기의 비재발로 해결 판정하지 않는다. Stream/Fox INCONCLUSIVE는 미생산 유형이다. 별도 빌드 완료 관찰이나 신규 Testcase/QA 결과를 주장하지 않는다.
