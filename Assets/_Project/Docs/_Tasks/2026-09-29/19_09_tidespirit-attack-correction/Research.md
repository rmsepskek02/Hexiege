# TideSpirit 공격 타이밍 교정 — Research

**최신 상태(2026-09-29, 혼합 실기 후):** 사용자 육안 판정은 TideSpirit 타격에 “특별한 문제 없었다”이다. 동일 `sharedSessionKey=ae04f2a9851c45c94bb4d4c73270232400041eb6e3435b08b424d401a8e6bd03`의 20:14~20:16 Editor Host/Android Client 경기에서 Red TideSpirit 32기와 Blue LittleKnight 12기를 생산했다. 경기 전체 Host 결과 160/실패 0 ↔ Client 160 수락/거부 0, 양쪽 필수 표현 129/129·실패 0, 공간 mismatch 0, Host MOVE adapterFailures 0, 양쪽 ROOT **local PASS**다. 이 근거로 TideSpirit **focused 시각 타이밍 PASS/CLOSED**; 단독 marker→피해 정확한 시간차·공식 CrossAudit·25종/역할교대/Legacy rollback은 미확인이다. 이전 LittleKnight 이동 FAIL 4건은 별도 OPEN이다. 세부 경계는 아래 「최신 혼합 경기」를 따른다.

**이전 상태(2026-09-29, 구현·Unity 메뉴 확인 직후):** 사용자가 Unity Animation 창에서 `TideSpirit_Attack` frame 45 = `1:15`를 저장한 클립은 단일 `OnAttackHit @ 1.50초`, 30fps·3초·loop다. 후속 구현으로 type 16 설정 `[1.50]`, Game 씬 type 16의 명시 Attack clip 연결, TideSpirit 영구 production gate가 저장됐다. 재컴파일 후 Unity Unit Action·Root Pose Cross Audit self-validation이 각각 **19:55:36 / 19:56:59 PASS**했다. `File > Build And Run` 클릭 뒤 `Detect Java Development Kit(JDK)` 진행 화면까지만 관찰했으며 당시 빌드 완료·설치·기기 실기·사용자 육안 판정은 **미확인**이었다. LittleKnight 이동 FAIL은 별도 OPEN이다.

아래 표는 **사용자 클립 저장 전의 정적 조사 이력**이다. 특히 클립 `1.15초`와 설정·클립이 같았다는 문장은 당시 파일 상태이며 현재 상태가 아니다. LittleKnight 비교 관찰의 경계는 유지한다.

## 설계값과 현재 생산값

| 항목 | 확인 결과 | 근거 |
|---|---|---|
| 설계 타격·주기 | `StatsReference.md` TideSpirit 행 `1:15(3:00)`. 사용자가 확정한 30fps 초:프레임 해석에서 `1:15 = 45/30 = 1.50초`, `3:00 = 3초`다. 과거의 혼재된 일반 주석을 십진초 해석의 근거로 쓰지 않는다. | `Assets/_Project/Docs/StatsReference.md` TideSpirit 행; 사용자 확정 지시 |
| 실제 Attack 클립 | `TideSpirit_Attack.anim`의 `m_SampleRate: 30`, stop time `3`, loop 활성, `OnAttackHit` 정확히 1개 @ `1.15초`. 설계보다 `0.35초` 이르며, `1.15초`는 30fps의 `1:15`가 아니다. | `Assets/_Project/Animations/Units/TideSpirit/TideSpirit_Attack.anim`의 clip settings 및 `m_Events` |
| 런타임 설정 | `UnitStatsConfig.asset` 실제 직렬화 `unitType: 16`: `attackCooldown: 3`, `hitFrameTimes: [1.15]`. 클립과는 같지만 설계 1.50초와 다르다. | `Assets/_Project/Resources/Config/UnitStatsConfig.asset` type 16 블록 |
| 생산 경로 | `Game.unity`의 `UnitFactory` Spirit `type: 16`에는 TideSpirit Blue/Red prefab 참조가 있으나 `attackTimelineClip` 명시 참조가 없다. 인접 Spirit 등록의 clip 참조를 type 16으로 오인하지 않는다. | `Assets/_Project/Scenes/Game.unity`의 `_spiritPrefabs` type 16 등록 |
| 양 팀 Animator | Blue/Red prefab 모두 같은 `TideSpirit.controller`와 `AnimationEventRelay`를 사용한다. Controller의 `Base Layer/Attack` motion은 위 Attack 클립이다. | `Assets/_Project/Prefabs/Units/Spirit/Unit_TideSpirit_{Blue,Red}.prefab`; `Assets/_Project/Animations/Units/TideSpirit/TideSpirit.controller` 및 `.meta` GUID |
| 영구 검증 | `RunUnitActionSelfValidation.cs`에 TideSpirit 전용 production 연결·marker/config gate가 없다. | `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` |
| 자산 감사표 | TideSpirit 현재 행은 `1 @ 1.15s / 동일`, `MigrationRequired`다. 이 값은 현재 저장 상태로 읽고 설계 목표 또는 완료 판정으로 읽지 않는다. | `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` TideSpirit 행 |

**클립 저장 직후 정정 이력(후속 구현 전):** 사용자 Unity 화면 확인(`frame 45 = 1:15`)과 Ctrl+S는 사용자 수행·보고 사항이다. 별도로 저장된 `Assets/_Project/Animations/Units/TideSpirit/TideSpirit_Attack.anim`의 `m_Events`를 직접 확인한 결과 `OnAttackHit`은 정확히 1개이고 `time: 1.5`다. 같은 파일의 `m_SampleRate: 30`, `m_StopTime: 3`, `m_LoopTime: 1`도 유지된다. 따라서 위 표의 클립 `1.15초`와 “클립·설정 일치”는 **저장 전 기록**이며 이 시점에는 클립 1.50초와 설정 1.15초가 불일치했다. 당시 `UnitStatsConfig.asset`의 실제 `unitType: 16`은 `attackCooldown: 3`, `hitFrameTimes: [1.15]`로 남아 있었고, `Game.unity` Spirit type 16에는 `attackTimelineClip`이 없었으며, Tide 전용 영구 검증도 없었다. 당시 감사표의 `1 @ 1.15s / 동일` 역시 클립 변경을 반영하지 못했다. `StatsReference.md`의 `1:15(3:00)` 설계값은 수정할 필요가 없다.

## 2026-09-29 후속 구현과 검증 경계

- 저장 파일 대조: `TideSpirit_Attack.anim`의 단일 `OnAttackHit time: 1.5`·30fps·stop time 3·loop 1, `UnitStatsConfig.asset` 직렬화 `unitType: 16`의 `attackCooldown: 3`, `hitFrameTimes: [1.5]`, `Game.unity` `_spiritPrefabs` type 16의 Blue/Red 기존 prefab과 `attackTimelineClip` GUID `436a2847db5341848804090de50cc4ea`가 일치한다. 이 clip은 사용자 저장값을 보존한 것이며 후속 코드 구현에서 다시 수정하지 않았다.
- `RunUnitActionSelfValidation.cs`는 TideSpirit 생산 clip·controller·양 팀 prefab/relay, 실제 `Base Layer/Attack`, 단일 marker 1.50초·3초 loop, type 16 설정과 Game 씬 명시 연결을 직접 검사하는 gate를 호출한다. 이 정적 코드·에셋 확인과 별도로, 재컴파일한 Unity Console에서 19:55:36 `[UAS-DIAG]` 및 19:56:59 `[UAS-ROOT-CROSS-AUDIT]` self-validation PASS를 확인했다. 두 메뉴 PASS는 새 멀티 경기·타격 시각의 육안 수용이 아니다.
- `File > Build And Run` 클릭 후 `Detect Java Development Kit(JDK)` 진행만 관찰했다. 그 뒤 빌드의 완료·실패, 설치 또는 Android 실행 결과를 감시하거나 확인하지 않았다. 새 TideSpirit/LittleKnight 혼합 경기와 사용자 화면 평가는 아직 없다. 따라서 focused 타이밍 판정은 **OPEN**이고 규칙 v2 전체·공식 cross-audit·25종/역할교대/rollback 완료도 아니다.

규칙 17의 일반 공격 HitFrameTimes 출처는 실제 Attack clip 이벤트이고, 규칙 18의 피해 적용은 서버 타이머 권위다. `NET-PRESENT-001`은 Animation Event를 로컬 표현 표식으로 제한하고, `NET-PRESENT-002`는 검증된 타임라인과 완성 유닛의 marker 일치를 요구한다. 따라서 클립·설정·실제 생산 연결을 한꺼번에 확인해야 하며, 값 일치만으로 피해와 화면의 동시성을 증명할 수 없다.

## LittleKnight 비교 대상과 별도 이동 문제

사용자는 LittleKnight를 별도 단독 테스트로 떼지 않고 이후 유닛 교정의 혼합 경기에서 계속 비교 대상으로 사용하기로 했다. 이전 Editor Host 경기 `06d034ab…e1a6c4`의 12:45:21~23에 LittleKnight Unit 21/19/16/26 네 건은 `source=post-combat-no-safe-forward-center`, start/root `(5,15)` → goal `(6,15)`, `pathStatus=Unreachable`, `envRevision=1`이었다. 전방 재합류 후보 실패 후 권위 재탐색이 null을 반환하고 `UnitRepathProgressGuard.Evaluate(null)`이 `RejectedInvalidPath`, `UnitView.TryAcceptRepathDecision`이 `MarkBlocked`와 adapter failure로 이어진 직접 사슬까지 확인됐다. **왜 그 경로가 Unreachable이었는지에 대한 타일·건물 점유 증거는 없다.** 후속 경기 `0f0bbb…923eb60`의 Host `adapterFailures=0`은 미재발 관찰이지 원인 규명이나 수정 증명이 아니다. 자세한 이력은 [BoulderSpirit Research](../03_13_boulderspirit-attack-correction/Research.md)에 보존한다.

이번 TideSpirit 문서의 LittleKnight 역할은 같은 경기의 비교·회귀 관찰뿐이다. 이동 코드 수정, LittleKnight 단독 실기, 경로 차단 원인 단정은 범위 밖이다. 이전 FAIL을 TideSpirit 타격이나 30fps 표기 문제의 결과로 연결하지 않는다.

## 2026-09-29 최신 혼합 경기 — focused 시각 PASS/CLOSED

- 근거는 Editor Host `Assets/_Project/Docs/_Logs/_editor/2026-09-29/RuntimeLog.txt`와 Android Client PID 28589 `Assets/_Project/Docs/_Logs/2026-09-29/20_17_logcat/RuntimeLog_device.txt`의 **20:14~20:16, 동일 sharedSessionKey `ae04f2a9851c45c94bb4d4c73270232400041eb6e3435b08b424d401a8e6bd03`**만이다. Host 생산 완료 로그에는 Red TideSpirit 32기, Blue LittleKnight 12기만 있다. 12시 LittleKnight FAIL 및 18시 Boulder 경기는 이 집계에 포함하지 않았다.
- 공격 집계는 Host `result-END serverResults=160, resultFailures=0`, Client `clientResultAccepted=160, clientResultRejected=0`이다. 양쪽 `presentation-END ready=160, expectedVisual=129, presentationEmits=129, failures=0, viewUnavailable=0, duplicateAttempts=0, transportFailures=0`; 공간 표본은 Host 116/Client 114, mismatch 각 0이다. 이들은 **두 유닛 혼합 경기 전체 집계**이지 TideSpirit 단독 타격 횟수가 아니다.
- Host `UAS-MOVE-AUTH` 74,832 frame에서 adapter/gate/writer/handoff/stationary-Walk/drop failure 0, final recoverable repath 2/repeated 0/fatal 0이다. Client replication invalid/duplicate/revision conflict와 attack-entry gap/order는 0이다. Host UAS END의 `coveredUnitTypes=2/25`, correlationFailures/productionGateInvalid/targetMismatches/dropped 0. 양쪽 ROOT `summary-END`는 **각 local PASS**, errors/drop 0, stable-after-move endpoint Host 42/Client 40이다. 공식 CrossAudit Analyze는 실행하지 않았다.
- Host WARN 1은 종료 시 연결 해제 대기, ERROR/FATAL 0이다. Android WARN 100은 Factory의 아직 미생성 GameObject 55, ProductionController 재시도 대기 44, 종료 disconnect 1이다. `RetryInitializeUnitView` 성공 44, 실패 0, 최종 `viewUnavailable=0`이며 Android Unity E/F와 GameLog ERROR/FATAL은 0이다. 일시적 초기화 경고를 Tide 타격 실패로 취급하지 않는다. Stream/Fox timeline의 starts 0/INCONCLUSIVE도 미생산 범위로 이번 Tide 판정과 무관하다.
- 사용자는 TideSpirit 공격을 육안상 특별한 문제 없었다고 수용했다. 따라서 **1:15(1.50초) 설정에 대한 focused 시각 조건은 PASS/CLOSED**다. 로그에 Tide 전용 timeline terminal이 없어 공격별 marker→권위 피해의 정확한 offset 또는 동시성은 계측하지 못했다. 2/25종·고정 역할의 단일 경기와 local ROOT PASS를 25종 전체·역할교대·Legacy rollback·공식 CrossAudit PASS나 v2 migration 완료로 확대하지 않는다. 실기가 기록됐다고 과거 Build And Run 완료 과정을 관찰한 것으로 소급 주장하지 않는다. LittleKnight의 이전 `adapterFailures=4`는 이번 0건만으로 원인·수정이 증명되지 않아 별도 OPEN이다.
