# TideSpirit 공격 타이밍 교정 — Plan

사용자가 정한 30fps `1:15` 타격에 TideSpirit의 서버 타임라인 설정을 맞추고 생산 연결을 명시적으로 고정하는 작업이다. 사용자가 Attack 클립 frame 45를 저장한 뒤 나머지 설정·씬 연결·영구 gate 구현을 마쳤고, **Unity 두 self-validation은 PASS했다.** 후속 Editor Host/Android Client 혼합 경기와 사용자 육안 수용으로 TideSpirit **focused 시각 타이밍 PASS/CLOSED**다. 아래 빌드 시작·실기 미확인 문장은 **경기 이전 당시 관찰 경계**이며 빌드 완료 과정은 지금도 직접 확인하지 않았다. 공식 CrossAudit·25종/역할교대/Legacy rollback은 OPEN이다.

**계획 변경 이유(2026-09-29):** 최초 계획은 클립과 type 16 설정이 모두 1.15초일 때 작성됐다. 사용자 저장 후 클립만 1.50초가 되었으므로, 아래 계획에서 **클립 수정 제안을 제거하고 저장값 보존·재검증**으로 바꾼다. 기존 1.15초 조사와 최초 제안은 [Research](Research.md)의 당시 이력으로 남긴다.

## 목표와 적용 경계

- 타격 목표는 `1 + 15/30 = 1.50초`, 공격 주기는 `3:00 = 3초`다. 클립의 저장된 단일 marker @ 1.50초는 보존·검증하고, 남은 type 16 설정만 `[1.15] → [1.50]`으로 교정한다. 클립 3초 길이·loop와 3초 cooldown은 유지한다.
- 실제 `Game.unity`의 `UnitFactory` Spirit `type: 16` 행에 TideSpirit 생산 Attack clip을 `attackTimelineClip`으로 명시한다. 다른 Spirit 행을 건드리지 않는다. Blue/Red prefab은 이미 같은 Controller와 `AnimationEventRelay`를 사용하고 `Base Layer/Attack`은 해당 clip이므로, 재확인 결과 그대로라면 보존한다.
- 기존 공격/서버 피해·표현 로직 제거, prefab/controller 변경, 다른 유닛 수치 수정, TideSpirit의 Supported 승격 또는 v2 전체 migration은 하지 않는다. LittleKnight 이동 수정도 하지 않는다.

## 승인된 후속 구현의 예상 변경 파일 — 당시 계획 이력

아래 표와 「위험과 검증 순서」는 구현 전 계획이다. 실제 저장·검증 결과는 문서 마지막 「구현·검증 결과」를 따른다. 표의 `제안 변경`과 아직 수행하지 않은 fail-closed 변이 시험을 완료 사실로 읽지 않는다.

`TideSpirit_Attack.anim`은 **이번 후속 구현의 수정 대상에서 제외**한다. 사용자 저장 클립을 읽기 전용으로 재검증한다: 30fps·3초·loop, 단일 `OnAttackHit @ 1.50초`, 양 팀 Controller의 실제 `Base Layer/Attack` motion과 동일 GUID. 근거는 `GameSystemRules_Units.md` 규칙 17과 `NET-PRESENT-001/002`; 위험은 사용자 저장값을 다시 덮거나 다른 Attack 클립을 검사하는 것이며, gate는 저장 파일·생산 연결이 모두 같은 클립을 가리키는지 확인하는 것이다.

| 파일 | 제안 변경 | 규칙 근거 | 예상 위험 → 완료 gate |
|---|---|---|---|
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | 실제 직렬화 `unitType: 16`의 단일 `hitFrameTimes`만 `[1.15] → [1.50]`. `attackCooldown: 3`과 다른 행 보존. enum 선언 순서가 아닌 실제 타입 값으로 식별. | `GameSystemRules_Units.md` 규칙 17(클립 이벤트와 설정 일치), 규칙 18(피해는 서버 타이머 권위), `NET-PRESENT-002`(검증 타임라인과 marker 일치). | 잘못된 타입 행 변경·쿨다운 변형 위험 → 실제 type 16의 값 하나만 1.50초, cooldown 3초 및 타 행 불변 확인; 서버 피해를 로컬 이벤트 호출로 바꾸지 않음. |
| `Assets/_Project/Scenes/Game.unity` | `UnitFactory` `_spiritPrefabs`의 `type: 16` 행에 `attackTimelineClip`을 TideSpirit Attack clip으로 명시. 양 팀 prefab 참조 및 인접 행 보존. | `GameSystemRules_Units.md` 규칙 17(실제 Attack clip에서 타이밍 추출), `NET-PRESENT-002`(검증된 타임라인 연결). | 다른 Spirit 행·유사 클립 오배선 위험 → type 16 명시 참조 GUID가 양 팀 Controller의 `Base Layer/Attack` motion과 동일하고 인접 행·prefab 참조 불변 확인. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | TideSpirit 전용 영구 production gate 추가. 실제 scene 등록 type 16, Blue/Red prefab·relay·동일 Controller, `Base Layer/Attack`의 정확한 motion, clip 30fps/3초/loop/단일 `OnAttackHit @ 1.50`, config 실제 type 16/cooldown 3/단일 `hitFrameTimes @ 1.50`을 fail-closed 대조. | `GameSystemRules_Units.md` 규칙 17·18, `NET-PRESENT-001/002`(marker의 표현 역할과 생산 타임라인 일치). | fixture만 검사해 생산 오배선을 놓치거나 잘못된 행을 통과시킬 위험 → 실제 scene·양 팀 생산 연결의 정상값 PASS, clip/marker/config/팀 연결 각각 불일치 시 실패 확인; Unity 두 메뉴의 새 결과 별도 기록. |
| `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` | 구현 후에만 TideSpirit 현재 행·검증 상태를 실제 증거에 맞춰 갱신. Unity PASS나 실기 PASS를 선기록하지 않음. | `GameSystemRules_Units.md` 규칙 17과 `NET-PRESENT-002`의 실제 marker·타임라인 정합 상태를 기록하는 문서 반영. | 계획값을 현행 구현·실기 완료로 오기할 위험 → 저장값·실제로 끝난 검증만 기재하고 미실시 gate와 `MigrationRequired` 경계 유지. |
| `Assets/_Project/Docs/StatsReference.md` | `1:15(3:00)` 자체는 이미 올바른 설계 목표다. 혼동 방지를 위한 Tide 행 또는 표 범례의 초:프레임(30fps) 설명 필요성을 구현 후 검토하되, 목표 시각은 변경하지 않음. | 사용자 확정 30fps 해석과 `GameSystemRules_Units.md` 규칙 17(설계 시각을 실제 Attack marker와 대조). | `1:15`를 십진 1.15초로 오독하거나 설계값을 불필요하게 수정할 위험 → 목표 `1:15 = 1.50초`, 주기 `3:00 = 3초` 유지; 필요 시 표기 설명만 추가하고 현행 marker/config와 대조. |

## 위험과 검증 순서

1. 구현 전 생산 경로를 다시 읽어 type 16, prefab GUID, Controller `Base Layer/Attack`과 clip GUID를 대조한다. 이름 검색이나 주변 Spirit 배열 순서만으로 행을 선택하지 않는다. `UnitType`의 명시값 공백 때문에 `enumValueIndex` 대신 실제 정수값을 사용한다.
2. 사용자 저장 clip을 보존한 채 config·scene 연결을 일치시킨 다음 영구 gate가 정상값은 통과하고 marker 개수/시간, clip identity, 팀별 연결 또는 type 16 config 불일치에는 실패하는지 확인한다. 이 gate는 규칙 17·18 및 `NET-PRESENT-001/002`의 구성 일치 검증이지 런타임의 정확한 동시성 증명은 아니다.
3. 이번에 요청된 Unity의 Unit Action self-validation과 Root Pose Cross Audit self-validation 두 메뉴를 실행해 결과를 기록한다. 에디터 refresh/domain reload 전의 stale 검증 결과를 새 코드의 판정으로 쓰지 않는다. 문서 정합성 검사 뒤, 이번에 명시 요청된 **빌드 시도**는 착수 시점까지만 확인한다. 빌드 완료·설치·기기 실행 성공을 감시하거나 확인했다고 주장하지 않는다.
4. 사용자가 별도로 실기 검증을 요청하면 동일 경기에서 TideSpirit와 LittleKnight를 혼합 생산해 양 peer의 공격 결과·필수 표현·공간 불일치 및 사용자 육안 타격을 구분해 확인한다. LittleKnight는 계속 비교 유닛으로 두고 Host `MOVE-AUTH`의 `adapterFailures`, `post-combat-no-safe-forward-center`, `Unreachable` 재발 여부를 따로 관찰한다. 이 증거 없이 TideSpirit focused PASS, LittleKnight 해결, 공식 CrossAudit, 25종 전체/역할교대/Legacy rollback 완료를 주장하지 않는다.

서버 권위 피해 타이머는 Animator 이벤트 호출에 종속시키지 않는다(규칙 18). LittleKnight 과거 FAIL의 정확한 경로 차단 원인은 이 계획의 가정이 아니다. 사용자의 구현·두 Unity 메뉴·빌드 시도 승인은 이번에 받았으며, **빌드 완료 감시와 새 실기 판정은 승인 범위에 포함하지 않는다.**

## 2026-09-29 구현·검증 결과 — 당시 실기 OPEN

- 사용자 저장 `TideSpirit_Attack.anim`의 단일 `OnAttackHit @ 1.50초`, 30fps·3초·loop는 보존했다. 후속 구현에서 `UnitStatsConfig.asset` 실제 type 16 `hitFrameTimes`를 `[1.15] → [1.50]`으로 바꾸고 cooldown 3초는 유지했다. `Game.unity` UnitFactory Spirit type 16에 해당 Attack clip을 명시 연결했으며, `RunUnitActionSelfValidation.cs`에 TideSpirit 생산 연결·marker/config gate를 추가했다. [Research](Research.md)의 저장 파일 확인을 따른다.
- Unity 재컴파일 후 19:55:36 `[UAS-DIAG]` self-validation, 19:56:59 `[UAS-ROOT-CROSS-AUDIT]` self-validation이 각각 PASS했다. 정적 연결과 두 메뉴의 통과만 확인했으며, 위 계획의 개별 오배선/marker 변이 시험이나 공격별 실제 marker→서버 피해 정밀 offset을 별도로 수행했다고 주장하지 않는다.
- `File > Build And Run` 클릭 뒤 `Detect Java Development Kit(JDK)` 진행을 확인했다. **빌드 완료·설치·Android 실기·사용자 시각 판정은 미확인**이다. TideSpirit focused 공격 시각 판정은 OPEN, LittleKnight 이동 FAIL은 별도 OPEN이다. LittleKnight는 향후 승인된 혼합 경기의 비교 유닛으로 유지하며 이번 작업에서 이동 코드를 수정하지 않았다.
- `StatsReference.md`의 `1:15(3:00)`은 확정 설계 그대로다. 매트릭스·현황 문서는 저장값/Unity PASS만 반영하고 `MigrationRequired` 및 전체 25종·역할교대·rollback 미완 경계를 유지한다.

## 2026-09-29 최신 실기 판정 — Tide focused PASS/CLOSED

- [Research](Research.md)의 최신 혼합 경기 증거: 20:14~20:16 동일 `sharedSessionKey=ae04f2a9851c45c94bb4d4c73270232400041eb6e3435b08b424d401a8e6bd03`, Editor Host/Android Client. Host에서 Red TideSpirit 32기·Blue LittleKnight 12기 생산, Host 결과 160/실패 0 ↔ Client 160 수락/거부 0, 양쪽 필수 표현 129/129·실패/중복/전송/최종 viewUnavailable 0, 공간 mismatch 0이다. Host MOVE 74,832 frame/adapterFailures 0, recoverable repath 2/repeated·fatal 0, 양쪽 ROOT local PASS/errors·drop 0. Host UAS 2/25종, correlation/productionGateInvalid/target mismatch/drop 0이다.
- 사용자 “TideSpirit 육안상 특별한 문제 없었다”는 판정으로 **1:15(1.50초) 공격 시각의 focused 시각 조건을 닫는다**. 집계는 Tide+LittleKnight 경기 전체이고 Tide 단독 정확한 marker→권위 피해 offset은 미계측이다. Android WARN 100은 초기화 대기 55+44 및 종료 disconnect 1이며 재시도 성공 44·최종 viewUnavailable 0; Android E/F·GameLog ERROR/FATAL 0. Stream/Fox timeline starts 0/INCONCLUSIVE는 미생산으로 범위 밖이다.
- 이 경기는 2/25종의 단일 역할이며 **공식 CrossAudit Analyze 미실행**이다. 전체 roster·역할교대·Legacy rollback·규칙 v2 migration 완료가 아니다. LittleKnight 이전 Host adapterFailures 4건은 이번 미재발만으로 해결되지 않아 별도 OPEN이다. 사용자 새 실기가 확보돼도 앞서 `Build And Run`에서 직접 관찰한 범위는 JDK 탐지 화면까지였으며 빌드 완료 과정을 소급해 확인했다고 쓰지 않는다.
