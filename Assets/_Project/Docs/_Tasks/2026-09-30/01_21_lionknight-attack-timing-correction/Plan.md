# LionKnight 두 번의 공격 타이밍 교정 — Plan

LionKnight가 칼을 휘두를 때 두 번의 타격이 사용자가 정한 `0:18`, `1:13` 프레임에 맞도록 실제 저장 클립과 게임 설정을 함께 고칠 계획이다. 30fps로 각각 `0.6초`, `43/30초 ≈ 1.43333333초`다. 화면에서 수정했다고 보고된 값과 달리 현재 디스크에는 예전 이벤트 `0.22`, `1.08`초가 남아 있으므로 저장 파일을 반드시 확인한다. **이 Plan은 구현 전 계획이다. Unity 검사·빌드·사용자 실기 결과는 아직 없다.**

**기존 로직 제거 없음.** 두 타격의 순서와 HitIndex `0/1`, `Supported / MeleeContact / Impact 2` 프로필, 서버 권위 피해 writer, `attackCooldown: 3초`, Blue/Red prefab과 controller, 나머지 스탯을 유지한다. 이번 변경은 사용자가 지정한 두 타격 시각과 그 생산 연결·검증·문서 일치에 한정한다.

## 구현 대상과 근거

| 대상 파일 | 계획된 변경 및 확인 | 규칙 근거 | 주요 위험과 완료 조건 |
|---|---|---|---|
| `Assets/_Project/Animations/Units/LionKnight/LionKnight_Attack.anim` | 디스크에 남은 두 `OnAttackHit`를 순서대로 `0.22 → 18/30 = 0.6초`, `1.08 → 43/30 ≈ 1.43333333초`로 저장한다. 30fps·2초 길이·loop와 다른 곡선은 보존한다. | `GameSystemRules_Units.md` 규칙 17(실제 Attack 이벤트가 다중 히트 시각 출처), 규칙 18(이벤트가 직접 피해를 쓰지 않음), `NET-PRESENT-001/002`. | Unity 창의 미저장 변경을 구현 완료로 오인하거나 이벤트를 중복 추가할 위험 → 저장 디스크의 `m_Events`에 정확히 두 `OnAttackHit`가 frame 18·43에 오름차순 존재함을 재확인. |
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | 실제 `unitType: 22`의 `hitFrameTimes` 두 값만 `[0.6, 43/30]`으로 맞춘다. `attackCooldown: 3`, HP·공격력 등 다른 스탯과 다른 type 행은 보존한다. | 규칙 17(클립 이벤트와 config 폴백 정합), 규칙 18(서버 피해 일정), `NET-PRESENT-002`(검증된 타임라인). | type 순서나 초/프레임 단위 혼동 → `unitType: 22` 블록을 직접 대조하고 두 히트 순서·3초 cooldown 불변 확인. |
| `Assets/_Project/Scenes/Game.unity` | 실제 UnitFactory `_transcendencePrefabs` type 22 등록에 `LionKnight_Attack.anim` GUID `45c00f46f91a76047a76e774cfa0f6ac`를 `attackTimelineClip`으로 명시한다. Blue/Red prefab 참조는 보존한다. | 규칙 17(생산 유닛의 실제 Attack clip에서 이벤트 추출), `NET-PRESENT-002`(검증 타임라인·완성 유닛 이벤트 일치). | 같은 씬의 다른 type 22 배열 또는 인접 type 20을 오수정할 위험 → UnitFactory 오브젝트의 해당 한 등록만 수정하고 `Base Layer/Attack` motion 및 두 prefab controller와 GUID 대조. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | LionKnight 전용 영구 production gate를 기존 Unit Action 메뉴에 추가한다. 실제 scene type 22·양 팀 prefab/relay·controller `Base Layer/Attack` motion·명시 clip GUID·정확히 두 marker `18/30`, `43/30`·config 두 값 및 cooldown 3을 함께 fail-closed 검증한다. 정상·불일치 경계는 기존 self-validation 패턴을 따른다. | 규칙 17·18, `NET-PRESENT-001/002`, `AttackSequenceId + HitIndex` 다중 타격 계약. | fixture/프로필만 통과하고 생산 배선 오류를 놓칠 위험 → clip identity·marker 수/순서·scene/prefab/controller/config 불일치에서 각각 실패; Unity 메뉴 PASS를 별도 확인. |
| `Assets/_Project/Docs/StatsReference.md` | **클립 이벤트를 바꿀 때 반드시 같은 구현 단계에서** LionKnight 타격 표기를 `0:22,1:08(3:00) → 0:18,1:13(3:00)`으로 정정한다. 괄호의 `3:00`, 2히트·HP·공격력 등은 유지한다. | 규칙 17(설계 타격 프레임과 실제 Attack marker 대조), 규칙 18(타격점과 공격 주기 구분). | 사용자에게 가장 눈에 띄는 참조표를 예전 값으로 남길 위험 → clip·config 저장값과 30fps 표기 일치 확인. **이번 문서 작성 단계에서는 StatsReference 자체를 수정하지 않는다.** |
| `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` 및 현재 task Research/Plan | 후속 구현·검증의 **실제 관찰 단계**에 맞춰 clip/config·명시 연결·gate와 결과를 기록한다. `MigrationRequired`는 별도 전체 검증 전 유지한다. | 규칙 17·18 및 `NET-PRESENT-002`의 생산 타임라인 감사 경계. | 계획만으로 PASS 선기록 → 저장·Unity·빌드 시작·사용자 실기를 각기 별도 기록. 이 단계에서는 해당 상시 문서를 수정하지 않는다. |

## 순서와 판정

1. 사용자 목표 `0:18`, `1:13`과 디스크의 이전 초 값 `0.22`, `1.08` 차이를 기준으로 저장 Attack 클립의 **기존 이벤트 두 개만** 교정한다. 즉시 type 22 config 두 값과 StatsReference `0:18,1:13(3:00)`을 동기화한다. cooldown과 다른 스탯은 건드리지 않는다.
2. Game 씬 UnitFactory type 22에 검증한 Attack clip을 명시하고 LionKnight production gate로 clip→`Base Layer/Attack` controller→Blue/Red prefab/relay→scene/config 전체를 대조한다. 다중 히트의 정렬된 `HitIndex=0/1`과 기존 결과·피해 writer는 유지한다.
3. Unity 재컴파일 후 `Hexiege > Combat > Run Unit Action Self Validation`과 `Hexiege > Combat > Diagnostics > Self Validate Unit Root Pose Cross Audit`을 각각 실행해 실제 Console 결과를 기록한다. 두 self-validation은 생산 배선·진단 자체의 게이트이지 사용자 화면 타격이나 **공식** Root Pose CrossAudit Analyze PASS가 아니다.
4. 승인된 `File > Build And Run`은 **시도/시작**까지만 수행·기록하고 빌드 완료 감시, 설치 및 기기 경기 판정은 사용자 몫으로 남긴다. 사용자 실기가 나오면 같은 세션 Host/Client의 LionKnight 실제 생산·두 HitIndex 결과/표현과 육안 타격을 분리해 평가한다. 아직 이 단계들을 PASS로 쓰지 않는다.
5. 이 focused 교정만으로 25종 전체, 역할교대, Legacy rollback, 규칙 v2 Complete 또는 이전 LittleKnight MOVE 실패 해결을 선언하지 않는다. 새로운 Testcase/QA 문서는 만들지 않는다.

## 2026-09-30 구현·실기 결과 — FAIL/OPEN

LionKnight의 두 타격 목표는 저장 Attack clip과 type 22 설정에서 30fps `0:18 = 0.6초`, `1:13 = 1.4333333초`로 맞춰졌다. Game 씬의 명시 clip 연결과 영구 production gate 코드도 확인했다. 위 구현 전 계획과 당시 미실행 표기는 이력으로 남긴다. 이 저장 상태만으로 Unity 두 self-validation의 실행 결과나 화면의 정확한 접촉 시점을 PASS로 판정하지 않는다.

사용자 승인 실기 `a2d9c80e143ab3ee03081f3925c936873e23978929e15528ec0b9b2a4c622225`는 **Android Host / Editor Client**, LionKnight+LittleKnight **2/25종** 범위다. Host C2 결과 224/실패 0, C3 ready 224/실패 0·필수 표현 199/199였다. Client C2 수락 224/거부 0이지만 C3 ready 217/실패 7·224묶음 중 217 방출, 필수 표현은 **ready 범위에서만** 192/192였다. Client 실패 7건은 전부 `Expired`(의도 시각 대비 약 0.527~1.192초 지연, 한도 0.5초 초과)다. 첫 실패 `instance=1`은 LionKnight `UnitId=2`에 대응하지만, 나머지 여섯 건의 유닛 유형은 입증되지 않았다. 양쪽 local ROOT는 PASS이며 공식 CrossAudit Analyze PASS와 다르다. 근거: `_Logs/2026-09-30/11_37_logcat/RuntimeLog_device.txt`, `_Logs/_editor/2026-09-30/RuntimeLog.txt`.

사용자는 큰 시각적 문제는 보지 못했지만 심한 렉을 보고했다. FPS/frame-time 기록이 없으므로 렉 원인이나 만료와의 인과를 단정하지 않는다. **최종 판정은 Client 표현 만료 때문에 FAIL/OPEN**이다. 후속은 만료 경계의 원인 분리와 재검증이며, 이 문서 작업에서 성능·코드·에셋을 수정하지 않는다. 정확한 marker→피해 offset·25종·역할교대·Legacy rollback·v2 Complete도 미완이다. 기존 LittleKnight MOVE 4건은 별도 OPEN이다.

## 2026-09-30 후속 계측 경기 — FAIL/OPEN 유지

위 판정은 이전 `a2d9c80e…c622225` 경기의 역사다. 새 진단 경기 `a81345042552685a831405817424d1a4b7e2260e56708f4e6f8febb7c3aacb5b`는 **Editor Host / Android Client**에서 LionKnight 8기·LittleKnight 22기(2/25종)로 진행됐다. Host/Client C3 일정 226, 결과·ready 216, 실패·pending 0, 묶음 방출 216/216, 필수 표현 188/188이고 Client C2 수락 216/거부 0이다. Client expiry diagnostic arrival 216·overflow/pending 0, `Expired` 0. 양쪽 local ROOT PASS·오류 0과 사용자 육안의 큰 문제 없음도 기록한다. 로그: Host `Assets/_Project/Docs/_Logs/_editor/2026-09-30/RuntimeLog.txt`, Client `Assets/_Project/Docs/_Logs/2026-09-30/13_46_logcat/RuntimeLog_device.txt`.

이번에는 기존 만료가 재현되지 않아 실패 전후 시각 상세가 없고, 과거 심한 렉도 이 런에서 평가되지 않았다. 진단 추가만으로 이전 결함의 원인 또는 수정 성공을 주장하지 않는다. 정확한 LionKnight marker→권위 피해·화면 접촉, 공식 CrossAudit, 전체 25종·역할교대·Legacy rollback, 별도 LittleKnight MOVE 과제는 여전히 미완이다. **이 task는 FAIL/OPEN**이며 다음 재현 증거가 나오면 `12_37_attack-presentation-expiry-diagnosis`의 시간 구간별 절차로 원인을 분리한다.

## 2026-09-30 최종 focused 판정 — PASS/CLOSED, C3 결함 별도 OPEN

앞 절의 FAIL/OPEN은 사용자 판정 분리 전 이력이다. 사용자는 `a8134504…c3aacb5b` Editor Host / Android Client의 LionKnight 8기·LittleKnight 22기(2/25종) 혼합 실기와 육안상 큰 문제 없음을 수용해 **LionKnight 두 타격 시각 focused 교정을 PASS/CLOSED**로 판정했다. 경기 전체 C2 216/216, C3 결과·ready 216/216·실패 0, 묶음 216/216 방출, 필수 표현 양측 188/188, Client `Expired` 0, 양쪽 local ROOT PASS가 확인 범위다.

첫 `a2d9…c622225` 경기의 Client `Expired` 7건은 **별도 미해결 C3 결함**으로 유지하며 진단 task `12_37_attack-presentation-expiry-diagnosis`는 OPEN이다. 진단 계측만 추가된 후 무만료 한 경기가 나왔을 뿐 원인·근본 수정은 확인되지 않았다. 정확한 접촉/피해 offset, 공식 CrossAudit, 25종·역할교대·rollback·v2 완료, 과거 심한 렉과 LittleKnight MOVE 결함은 이 focused closure에 포함하지 않는다.
