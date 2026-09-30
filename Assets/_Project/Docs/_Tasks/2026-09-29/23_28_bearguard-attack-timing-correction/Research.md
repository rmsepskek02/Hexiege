# BearGuard 공격 타이밍 교정 — Research

**최신 판정(2026-09-30): BearGuard 0:13 focused 실기 PASS/CLOSED.** 사용자 육안 수용과 재테스트 `b986fbbcd52462ab5d1c65e4d6699d1fb9f32339bdc574d130c8ee0761d7d94b`의 동일 경기 Host/Client 결과·표현 및 각 local ROOT PASS가 근거다. 이는 2/25 혼합 경기의 focused 판정이며 BearGuard 단독 marker→피해 offset, 공식 CrossAudit Analyze, 25종·역할교대·rollback 또는 규칙 v2 Complete가 아니다. 아래 2026-09-29 “실기 미확인”은 당시 이력이다.

BearGuard의 타격이 사용자가 정한 애니메이션 `0:13` 지점에서 일어나도록 공격 클립·게임 설정·생산 연결을 조사한 문서다. **최신 검증 상태(2026-09-29):** Unity 재컴파일 후 Console에서 23:43:36 `[UAS-DIAG] self-validation PASS`, 23:45:06 `[UAS-ROOT-CROSS-AUDIT] self-validation PASS`를 확인했다. `File > Build And Run` 클릭 뒤 `Checking prerequisites / Starting Android build` 화면까지만 확인했다. **빌드 완료·설치·기기 경기·사용자 화면 판정은 확인하지 않았다.** 기존의 `0.2초` 조사값은 구현 전 이력, 아래 “Unity 검증 전” 서술은 검증 직전 이력이다.

**이전 상태(2026-09-29, 구현 저장 완료 / Unity 검증 전):** 실제 디스크의 단일 `OnAttackHit`와 type 20 `hitFrameTimes`가 모두 `0.43333334초`로 바뀌었고, Game 씬 UnitFactory의 BearGuard 등록에 생산 Attack clip GUID가 명시 연결됐다. 영구 BearGuard production gate도 코드에 추가됐다. 당시 Unity 자동 재컴파일 중이어서 두 self-validation, Android Build And Run, 기기 경기와 사용자 화면 판정은 아직 확인되지 않았다. 아래 `0.2초` 조사값은 **구현 전 이력**으로 보존한다.

## 목표와 구현 전 확인값(당시 이력)

| 항목 | 조사 결과 | 근거 |
|---|---|---|
| 사용자 목표 | 30fps `0:13` = `13/30초` ≈ `0.433333초`. 별도 Inspector 재질문·확인 절차 없이 이 목표를 구현에 반영하도록 승인받았다. | 사용자 지시 |
| 생산 Attack 클립 | `BearGuard_Attack.anim`의 `m_Events`에는 `OnAttackHit` 1개가 **`time: 0.2`**로 남아 있다. `m_SampleRate: 30`, stop time `1.6666666`, loop `1`이다. 따라서 현재 저장 파일은 아직 목표 `0:13`과 다르다. | `Assets/_Project/Animations/Units/BearGuard/BearGuard_Attack.anim` |
| 클립 정체성 | Attack clip GUID `6cd4a1668d01f24448599b56306f2b13`가 `BearGuard.controller`의 `Base Layer/Attack` motion이다. Blue/Red BearGuard prefab은 동일 controller GUID `df3dfff57716a3e4caea2d278b2eff9e`와 `AnimationEventRelay`를 사용한다. | Attack/controller `.meta`, `BearGuard.controller`, `Unit_BearGuard_{Blue,Red}.prefab` |
| 런타임 설정 | `UnitStatsConfig.asset`의 실제 직렬화 `unitType: 20`은 `hitFrameTimes: [0.2]`, `attackCooldown: 1.2`다. | `Assets/_Project/Resources/Config/UnitStatsConfig.asset` type 20 블록 |
| 생산 씬 연결 | `Game.unity`의 **UnitFactory** `_transcendencePrefabs` type 20에는 BearGuard Blue/Red prefab이 있으나 `attackTimelineClip` 명시 참조가 없다. 씬의 다른 컴포넌트에도 `type: 20`이 있으므로 그 행이나 주변 타입을 생산 등록으로 오인하지 않는다. | `Assets/_Project/Scenes/Game.unity`의 UnitFactory 직렬화 등록 |
| 공격 프로필·검증 | `UnitAttackShadowProfileResolver`는 BearGuard를 `Supported / MeleeContact / Impact 1 / secondary false`로 취급한다. `RunUnitActionSelfValidation.cs`의 호출 목록에 BearGuard 전용 **production marker/config/scene gate는 없다**. | `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`, `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` |
| 설계·감사 문서 | `StatsReference.md` BearGuard 행은 타격·주기를 `0:20(1:20)`으로 적고, `UnitCombatAssetMatrix.md`는 현재 marker/config `1 @ 0.20s / 동일`, `MigrationRequired`로 기록한다. | 두 문서의 BearGuard 행 |

`0:20`은 30fps에서 `20/30초`이고 사용자의 새 타격 목표 `0:13`과 다르다. 별도로 설계 주기 `1:20`은 `50/30 = 1.666666…초`인데, 현재 직렬화 cooldown은 `1.2초`다. 클립 길이 `1.6666666초`와 설계 주기가 비슷하다는 사실만으로 런타임 반복 주기가 어느 값인지 단정하지 않는다. **이번 승인은 타격 이벤트 교정이지 공격 주기 밸런스 변경 승인이 아니다.** cooldown `1.2`는 임의로 수정하지 않고 불일치를 별도 OPEN으로 남긴다.

## 규칙과 검증 경계

`GameSystemRules_Units.md` 규칙 17은 일반 공격의 HitFrameTimes를 실제 Attack 클립의 `OnAttackHit`에서 읽고, 수동 설정은 이벤트가 없을 때만 폴백으로 쓴다. 규칙 18은 실제 피해가 서버 타이머에서만 적용된다고 정한다. 동기화 계약 `NET-PRESENT-001`은 Animation Event를 로컬 표현 표식으로 제한하고, `NET-PRESENT-002`는 완성 유닛의 이벤트와 검증된 AttackTimeline 일치를 요구한다. 따라서 이벤트·type 20 폴백·실제 Game 씬 바인딩·양 팀 Animator를 함께 고정해야 하지만, 정적 값의 일치만으로 화면 타격과 권위 피해의 정확한 동시성을 증명할 수는 없다.

BearGuard는 `Supported`이나 감사표의 `MigrationRequired`가 뜻하는 규칙 v2 전체 멀티플레이 검증은 끝나지 않았다. 이번 작업의 성공 여부는 구현 후 새 Unity 검증과 사용자 소유 실기로 별도 판정해야 한다. LittleKnight는 혼합 경기 비교 대상으로 계속 사용하되, 이전 Host MOVE-AUTH adapter failure 4건(`post-combat-no-safe-forward-center`, `Unreachable`)은 별도 OPEN이며 BearGuard 타격의 원인으로 섞지 않는다.

## 2026-09-29 구현 저장 후 확인·잔여

- `BearGuard_Attack.anim`의 `m_Events`는 `OnAttackHit` 단일 `time: 0.43333334`로 저장됐다. 30fps `0:13 = 13/30초`의 float 직렬화값이다. 클립은 30fps·약 1.6666666초·loop 유지다. 착수 당시 `0.2초`는 위 표의 과거 조사값이다.
- `UnitStatsConfig.asset`의 실제 `unitType: 20`은 `hitFrameTimes: [0.43333334]`, `attackCooldown: 1.2`다. `Game.unity`의 UnitFactory `_transcendencePrefabs` type 20은 Blue/Red prefab을 보존하면서 `attackTimelineClip`에 GUID `6cd4a1668d01f24448599b56306f2b13`을 명시한다.
- `RunUnitActionSelfValidation.cs`에 BearGuard 생산 clip·controller `Base Layer/Attack`·양 팀 prefab/relay·씬 type 20·설정값을 대조하는 전용 gate가 추가됐다. **코드 존재 확인은 Unity 메뉴 PASS가 아니다.** 재컴파일 후 Unit Action/Root Pose Cross Audit self-validation 결과를 각각 확인해야 한다.
- `StatsReference.md` 타격 표기는 `0:13(1:20)`으로 동기화한다. 설계 주기 `1:20 = 1.666666…초`와 실제 cooldown `1.2초`의 차이는 별도 OPEN이며 이번 구현에서 주기를 수정하지 않았다. 빌드 시도·완료, 설치·실기·화면 수용은 아직 미확인이다. LittleKnight 이동 4건 및 전체 migration 검증도 OPEN이다.

## 2026-09-29 Unity 검증 및 빌드 시작 관찰

- Unity 재컴파일 후 `Hexiege > Combat > Run Unit Action Self Validation`을 실행해 23:43:36 Console `[UAS-DIAG] self-validation PASS`를 확인했다. 이어 `Hexiege > Combat > Diagnostics > Self Validate Unit Root Pose Cross Audit`을 실행해 23:45:06 `[UAS-ROOT-CROSS-AUDIT] self-validation PASS`를 확인했다. 이는 자동 검사 통과이지 BearGuard 기기 화면의 타격 시각을 측정한 결과가 아니다.
- `File > Build And Run` 클릭 후 `Checking prerequisites / Starting Android build` 화면을 확인했다. 빌드 완료·설치·기기 실기·사용자 육안 수용은 감시하거나 확인하지 않았다. LittleKnight 이전 Host MOVE-AUTH 4건, 설계 주기와 실제 cooldown 차이, 공식 CrossAudit·25종/역할교대/rollback은 OPEN이다.

## 2026-09-30 focused 실기 판정 — 첫 경기와 재테스트 분리

- 첫 경기 `cc278c50c0641d2ee5f2e5b6acce2129d12e0de030cafea384f80db7fb461819`의 Host/Client ROOT는 각각 00:18:45/00:18:48 `INCOMPLETE`였다. 관찰 종료가 첫 LittleKnight(00:20:04)·BearGuard(00:20:12) 생산보다 앞서므로 이 ROOT를 생산 유닛 검증 PASS로 사용하지 않는다. 이 경기 수치를 재테스트와 합산하지 않는다.
- 재테스트 `b986fbbcd52462ab5d1c65e4d6699d1fb9f32339bdc574d130c8ee0761d7d94b`는 Editor Host `Assets/_Project/Docs/_Logs/_editor/2026-09-30/RuntimeLog.txt`와 Android Client `Assets/_Project/Docs/_Logs/2026-09-30/00_47_logcat/RuntimeLog_device.txt`의 동일 경기다. BearGuard 11기·LittleKnight 49기, 총 60기·2/25종이 관찰됐다. 아래 합계는 **경기 전체**이지 BearGuard 단독 타이밍 측정이 아니다.
- Host `result-END` 641/실패 0 ↔ Client 수락 641/거부 0. 양 peer `presentation-END` 필수 608/emit 608/실패 0, 공간 mismatch 0(Host 574·Client 500 samples). Host MOVE 100,250 frames에서 adapter/gate/stationaryWalk 0, recoverable repaths 5·repeated/fatal 0. Client attackEntry transport gap/order violation 0. Host/Client ROOT `summary-END`는 각각 local `PASS`, coveragePassed=True, errors/logDrop=0, trackedUnits 60, stableAfterMoveEndpointUnits 58 및 rotationEvidence 58이다.
- 사용자가 BearGuard 타격을 육안상 괜찮다고 수용해 **0:13 focused 타격 교정은 PASS/CLOSED**다. 공식 `Analyze Latest Unit Root Pose Logs`는 실행하지 않았으므로 두 local ROOT PASS나 과거 self-validation PASS를 공식 CrossAudit PASS로 부르지 않는다. BearGuard 단독 marker→서버 피해 간격은 계측되지 않았다. LittleKnight 과거 Host adapterFailures 4는 이번 미재발에도 별도 OPEN이고 설계 주기 `1:20` 대 실제 cooldown `1.2초`도 별도 OPEN이다. 전체 25종·역할교대·Legacy rollback/v2 Complete는 미검증이다.
