# RabbitTrickster 공격 클립 명시 연결 조사

토끼 유닛이 화면에서 사용하는 공격 동작과 공격 시간을 읽는 동작이 항상 같도록 연결하려는 작업입니다. 이미 DustSpirit에 적용된 명시 연결 방식을 재사용하며, 공격 수치나 애니메이션 자체는 바꾸지 않습니다.

**최신 판정(2026-09-28):** 사용자 지정 0:20(0.6666667초) 빌드의 실기와 육안 수용으로 Rabbit focused PASS/CLOSED다. 아래 첫 상태 문단과 0.18초 경기는 당시 이력이며, 새 경기 근거와 한계는 문서 끝의 「0:20 새 빌드 동일 경기 후속 증거」를 따른다.

## 범위와 현재 상태

- **당시 상태(marker 교정 직후, 실기 전): 명시 연결 유지 / 0.6666667초 생산 계약 정합화 완료 / 새 Unity 메뉴·빌드·실기 대기 / 타격 시각 우려 OPEN.** 실제 Attack 저장본, type 25 설정, `ValidateRabbitTricksterProductionTimeline`의 호출·기대값·설정 대조를 확인했다. 아래 Unity PASS와 사용자 확인 빌드·실기는 **이전 0.18초 버전의 당시 결과**이며 새 값의 접촉 프레임이나 동기 PASS를 확인하지 않는다.
- 아래 최초 작성 범위·조사 결과는 **구현 전 이력**이다. 현재 문서 동기화 범위는 Research/Plan과 AssetMatrix·Units 규칙 17·PROJECT_STATUS·ROADMAP이며, 검사는 메인이 담당한다.

### 최초 작성 당시 상태(이력 보존)

- 작성 시각: 2026-09-28 10:03 KST, 시스템 현재 시간 직접 확인.
- 사용자 승인: “RabbitTrickster task 문서 작성하고 바로 구현 및 빌드까지, 빌드 확인 사용자”. 이번 document-manager 단계는 Research/Plan 작성만 수행한다. 메인 검토 후 game-programmer가 구현한다.
- 구현 전 / 실기 **OPEN**. 컴파일·self-validation·빌드는 이 문서 작업에서 실행하지 않았다. TC/QA/git 작업 및 상시 문서·메모리 변경은 제외한다.
- 사용자 전달 환경: Unity 연결 정상, Login 씬 비실행 상태. 이 문서 작성자가 UI를 직접 확인한 결과는 아니다.

## 직접 확인한 근거(구현 전 조사 이력)

아래 경로는 저장소 루트 기준이다. 씬·메타·클립 YAML과 코드 본문을 직접 읽었다. 행 번호보다 컴포넌트·필드·GUID를 식별 기준으로 사용한다.

| 대상 | 확인 결과 |
|---|---|
| `Assets/_Project/Scenes/Game.unity` 실제 UnitFactory | script GUID `ffbee3fe087401948ae349db70b1d746`, 클래스 `Hexiege.Infrastructure.UnitFactory`, GameObject fileID `1708566850`. `_transcendencePrefabs`의 `type: 25`(조사 시 41748행 근처)에 `attackTimelineClip`이 없다. |
| 해당 행의 양 팀 프리팹 | Blue GUID `f8057fd8cc6c0ec4fbab3017b937d894`, Red GUID `651c84fc382894a48b555c1c17bfc9d6`. `Assets/_Project/Prefabs/Units/Transcendence/Unit_RabbitTrickster_Blue.prefab` 및 `_Red.prefab`의 메타와 일치한다. 두 프리팹의 Controller GUID는 `5b68d127286ca944f9aa795362e846d2`, root motion은 꺼져 있다. |
| `Assets/_Project/Animations/Units/RabbitTrickster/RabbitTrickster.controller` | `Base Layer/Attack`의 motion은 GUID `8d21000f295b9774a9cb2540a71b8ce7`, fileID `7400000`. speed 1, speedParameterActive 0. |
| 같은 폴더의 `RabbitTrickster_Attack.anim` 및 `.meta` | 위 GUID와 일치. 시작 0, 종료 2초, `OnAttackHit` 이벤트 한 개가 0.18초에 있다. |
| 같은 폴더의 `RabbitTrickster_Attack3.anim` | 시작 0, 종료 1.1초, `m_Events: []`. 이름에 Attack을 포함하는 별도 클립이다. |
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | `unitType: 25`의 attackCooldown 2, hitFrameTimes `[0.18]`. 이번 작업에서 변경하지 않는다. |
| `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs` | `GetAttackTimelineClip`은 종족·타입 등록에서 명시 클립을 읽는다. `SelectAttackTimelineClip`은 명시 참조 우선, 없으면 이름에 Attack이 들어가는 첫 클립을 선택한다. 서버/싱글과 Client 생성 양쪽이 선택한 동일 클립으로 길이와 이벤트를 읽는다. |
| DustSpirit 선례 | 같은 실제 UnitFactory의 `_spiritPrefabs/type: 13`에는 `attackTimelineClip` GUID `961cafd21c1fa13429c74aa65a3a6345`가 이미 연결되어 있다. `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`의 `ValidateDustSpiritProductionTimeline`은 실제 씬·양 팀 프리팹·Attack state·marker·설정과 alternate-first 선택을 함께 검사한다. |
| 현재 지원 상태 | `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`에서 RabbitTrickster는 이미 `Supported / MeleeContact / Impact 1 / secondary false`. 상태 변경은 필요하지 않다. |

## 결론과 증거 경계(구현 전 조사 이력)

명시 연결 누락으로 이름 기반 열거 순서에 의존할 여지가 남아 있다. Attack3가 먼저 선택되면 코드상 주기는 1.1초로 읽히고 이벤트는 비어 설정의 0.18초로 폴백한다. **실제 실행에서 Attack3가 먼저 선택됐다고 확인한 것은 아니다.** 이번 작업은 그 순서 의존성을 제거하는 제한된 연결 작업이다.

씬에는 서로 다른 컴포넌트의 `type: 25`가 여러 곳에 있다. 타입 숫자만 검색해 일괄 변경하지 않는다. 위 UnitFactory 식별자와 `_transcendencePrefabs`, 양 팀 GUID를 모두 확인해야 한다.

`Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`에도 Rabbit 복수 클립 선택 위험이 기록되어 있으나, 그 이력 표현을 현재 resolver 상태 대신 사용하거나 이번에 수정하지 않는다. 에셋 연결 일치는 실기 타격·반복 공격·멀티플레이 동기화 완료의 증거가 아니다.

## 관련 규칙

- `GameSystemRules.md` 인덱스에서 유닛·전투 동기화 규칙을 확인했다.
- `GameSystemRules/GameSystemRules_Units.md`: 규칙 17(타격 프레임 타이밍의 단일 출처), 규칙 18(서버 데미지 타이밍 정밀화), `U-ATK-TIMELINE`.
- `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`: `NET-PRESENT-001`, `NET-PRESENT-002`, 제10절 전환 규칙. Animation Event가 직접 피해를 쓰지 않는 기존 권위·표현 경계를 유지한다.
- 운영 근거: 루트 `CLAUDE.md`, `Assets/_Project/Docs/WORKFLOW.md`, `.claude/agents/document-manager.md`. 과거 enum 행 오선택 기록에 따라 후속 직렬화 조회는 `enumValueIndex`가 아닌 `intValue`를 사용한다.

## 구현 반영 결과(2026-09-28)

실제 `Game.unity` UnitFactory `_transcendencePrefabs/type: 25`의 기존 양 팀 GUID를 유지한 채 `attackTimelineClip` GUID `8d21000f295b9774a9cb2540a71b8ce7` 한 행이 추가됐다. 기존 `RunUnitActionSelfValidation.cs`에는 Rabbit production clip/controller·양 팀 프리팹·씬 등록·2초/0.18초 config·Attack3-first 선택 및 공통 생성 경로 검증이 추가됐다. 이는 검증 **코드 존재** 확인이며 Unity 실행 PASS가 아니다.

메인 patch 검토 결과 런타임 공통 코드·수치·클립·다른 유닛·Supported 상태 변경은 없다. 최초 조사에서 확인한 열거 순서 의존 위험을 명시 연결로 제거했지만, 실기 오선택 관측이나 실기 정상 판정을 뜻하지 않는다. Unity 검증과 사용자 실기는 대기하며 빌드는 미시도다.

### Unity 메뉴 후속 결과(2026-09-28, 메인 직접 확인·전달)

- 15:34:26: `[UAS-DIAG]` PASS 및 `[UAS-DIAG][PRODUCTION-TIMELINE]` PASS. 후자에 RabbitTrickster explicit Attack/Attack3-first가 명시됐다.
- 15:35:16: `[UAS-ROOT-CROSS-AUDIT] self-validation PASS`.
- Unity Console error 0 / warning 1. 메인 설명에 따르면 경고는 앞선 코드 작성과 동시 새로고침의 source version warning이며, 최종 재컴파일 이후 현재 error 0이다. 경고 0으로 기록하지 않는다.
- 위 구현 직후의 ‘Unity 검증 대기’와 이 시점의 ‘빌드 시작 대기’는 당시 기록으로 보존한다. 최신 실기 상태는 아래 후속 분석을 따른다.

## 최신 Host/Client 실기와 타격 시각 분석(2026-09-28)

> **이전 0.18초 버전의 실기 기록:** 아래 `7d4f85a6…e2654a5` 경기는 사용자 marker 이동과 설정·검증 교정 전의 증거다. 새 0.6666667초 타격 동기 PASS 근거로 재사용하지 않는다.

- 사용자가 빌드 후 Editor Host/Android Client 경기를 실기로 확인했다. 같은 경기의 `sharedSessionKey=7d4f85a6b980b02035e532b36068e5ca349ae35610294a423d3a4ebe2e2654a5`가 Editor 일일 로그(`Assets/_Project/Docs/_Logs/_editor/2026-09-28/RuntimeLog.txt`)와 기기 로그(`Assets/_Project/Docs/_Logs/2026-09-28/16_09_logcat/RuntimeLog_device.txt`)에 있다.
- Host `16:06:52.031`의 RabbitTrickster Unit 1은 `shadow-commit`과 `legacy-schedule`이 같은 회차로 기록됐다. Host 종료 집계는 일정 223건, 결과 212건, 결과 실패 0건이다. Client는 결과 212건 수락/0건 거부, 필수 표현 199/199건이다. 이는 **해당 경기 전체의 전투 결과·표현 집계**이지 RabbitTrickster만의 타격 프레임 계측이나 223건 모두의 결과 완료를 뜻하지 않는다.
- 생산 Attack 클립 길이는 2초이고 유일한 `OnAttackHit`은 0.18초다. `UnitFactory`는 이 marker를 `HitFrameTimes`로 읽으며, `NetworkCombatController`는 해당 hit offset에 서버 권위 피해 타이머를 예약한다. 서버 피해가 로컬 Animation Event 자체로 실행되는 것은 아니다(`GameSystemRules_Units.md` 규칙 17·18).
- 사용자의 관찰은 **공격 애니메이션에 비해 피해 적용이 약간 빠르게 보인다**는 것이다. 실제 접촉 프레임은 아직 측정하지 않았으며 후속 확인 대상이다. 따라서 선행 시간차의 정확한 값이나 원인을 0.18초 marker, 네트워크, 표현 큐 중 하나로 단정할 수 없다. 이번 실기는 빌드·전투 경로가 사용되었다는 근거지만 타격 시각 PASS는 아니다. 시각 우려는 OPEN으로 유지한다.

## 사용자 지정 0:20 marker와 후속 정합화(2026-09-28 현재)

- 사용자가 Unity Animation에서 `RabbitTrickster_Attack`의 유일한 `OnAttackHit`을 약 0:05에서 **0:20(30fps)**으로 옮겨 저장했다. 디스크의 Attack 클립은 2초, `m_SampleRate: 30`, 단일 event `time: 0.6666667`이다. 실제 `Game.unity` UnitFactory type 25의 명시 `attackTimelineClip` GUID `8d21000f295b9774a9cb2540a71b8ce7`는 그대로다. 사용자 저장 `.anim`은 이 문서 작업에서 변경하지 않았다.
- 별도 game-programmer 교정 뒤 실제 `UnitStatsConfig.asset`의 type 25는 `attackCooldown: 2`, `hitFrameTimes: [0.6666667]`이다. `RunUnitActionSelfValidation.ValidateRabbitTricksterProductionTimeline`의 호출과 생산 클립 2초·marker 1개/0.6666667초(30fps 20프레임) 기대값, 설정과 marker 일치 검사도 파일에서 확인했다. **설정·검증 구현은 완료**됐으나 새 값에 대한 Unity 메뉴 실행 PASS를 뜻하지 않는다.
- 규칙 17 및 `U-ATK-TIMELINE`의 단일 생산 타임라인을 이 새 값으로 맞춘 것이며, 규칙 18의 서버 권위 피해 타이머·표현 소유권과 Supported 상태는 변경하지 않았다. 이전 로그는 0.18초 계약에서 나온 결과 전달 증거로만 보존한다. 실제 접촉 시각·새 빌드에서의 화면상 피해 시각은 여전히 미계측이다.
- **현재 판정: 새 사용자 지정값의 production 계약 정합화 완료 / Unity 메뉴·새 빌드·새 Host/Client 실기 미실행 / 타격 시각 focused OPEN.** 다음 확인은 새 Unity production self-validation과 빌드 후 동일 조건 실기에서 타격 시각을 직접 비교하는 것이다. 전체 25종 migration·역할교대·rollback 완료는 주장하지 않는다.

## 0:20 새 빌드 동일 경기 후속 증거와 focused 판정(2026-09-28)

위 미실행·OPEN 문장은 당시 상태로 보존한다. 사용자는 새 0:20 marker 빌드의 RabbitTrickster 공격이 육안상 **“알맞게 나오는 것 같다”**고 보고했다. 이는 focused 시각 수용이며 정확한 프레임 간 오차를 계측했다는 뜻은 아니다. 현재 생산 Attack의 단일 `OnAttackHit`, type 25 설정, 영구 self-validation 기대값은 모두 0.6666667초(30fps 0:20)다. 이전 `7d4f85a6…e2654a5` 경기는 0.18초 버전이므로 이 새 판정에 합산하지 않는다.

- 새 Editor Host/Android Client 동일 경기 `sharedSessionKey=3fc75ef4357e734ee1db9e95b7a20192e7920086df0e531e1425ea234b7beded`. 근거: Host `Assets/_Project/Docs/_Logs/_editor/2026-09-28/RuntimeLog.txt`의 20:07–20:09 GameLog, Client `Assets/_Project/Docs/_Logs/2026-09-28/20_10_logcat/RuntimeLog_device.txt`의 같은 key. 양쪽 생산은 RabbitTrickster 각 15기, LittleKnight 각 13기다.
- **아래 수치는 Rabbit+LittleKnight가 함께 든 경기 전체 집계이며 Rabbit 단독 타격 건수가 아니다.** Host coordinator schedules 138/results 129/failures 0, Host result-END serverResults 129/resultFailures 0 ↔ Client accepted 129/rejected 0. 양쪽 presentation-END는 expectedVisual 122/presentationEmits 122, failures·duplicateAttempts·transportFailures 0이다. Host 공격 observer는 commits 113, correlationFailures·targetMismatches·dropped 0이다. schedules와 results의 차이 9건을 실패로 분류하지 않는다.
- Host/Client `UAS-ROOT-POSE summary-END`는 각각 **local PASS**, errors 0/logDropCount 0이며 양쪽 `UAS-MOVE-AUTH END`는 EVIDENCE다. 공식 Root Pose CrossAudit은 이번 경기에서 실행하지 않았다. 관련 시간대 GameLog의 ERROR/FATAL은 양쪽 0건이다.
- 런타임 종료 집계는 Animation marker부터 권위 피해 적용까지의 정확한 offset이나 화면 접촉 프레임을 직접 측정하지 않는다. 따라서 사용자 수용을 이번 **Rabbit 명시 클립·0:20 타이밍 focused PASS/CLOSED**의 주 근거로 삼되 subframe 상관성, 25종 전체·역할교대·Legacy rollback·C3 전체 migration 완료로 확대하지 않는다. 성능 렉은 별도 결함이며 이 판정에 포함하지 않는다.
