# 에디터 스크립트 정리 — 계획

사용자가 실제 쓰는 로그 저장·네트워크 강제 실패와 전투 교정의 두 검사 진입점만 남기고 에디터 도구를 최소화한다. 나중에 필요할 가능성만 있는 도구는 미리 쌓아 두지 않고 그때 상황에 맞춰 다시 작성한다. 필수 검사의 실제 내부 의존은 보존하되 별도 메뉴로 늘리지 않으며, 기존 게임 설정과 런타임 로직은 유지한다.

**현재 상태: 사용자의 「비활성화한거 삭제하고 메인에 병합해줘」 명시 승인에 따른 최종 삭제·정적 검증·main 병합 완료.** 활성 7파일·17메뉴를 보존하고 비활성 원문 28파일·대응 메타와 빈 폴더 메타를 제거했다. 직접 삭제 승인을 사용자 테스트 PASS로 기록하지 않는다.

**현재 확인 프로젝트:** 기존 Unity 프로젝트 `D:/Projects/Hexiege/Hexiege`의 `main`에 최종 삭제 커밋 `58614539`를 반영했다(`946ade38` → `58614539` fast-forward). 앞선 비활성 구현의 main 병합 이력은 보존한다. Unity 컴파일·기기·메뉴 실행은 미확인이고 푸시는 하지 않았다.

기존 별도 작업 폴더 `.utmp/docs-checker-finalize`는 detached 조회용으로 전환했다. 사용자는 원래 Unity 프로젝트에서 현재 변경을 확인하며, 아래 별도 폴더 기준 기록은 전환 전 실행 이력으로 보존한다.

문서 정리·검사기 정비·에디터 정리를 포함한 `codex/editor-script-organization`의 통합 결과를 사용자 병합 승인에 따라 main에 반영했다. 확정 이력은 Git log를 기준으로 확인한다. 이 병합 승인을 사용자 테스트 통과나 28파일 최종 삭제 승인 조건 충족으로 해석하지 않는다.

**추가 계획의 삭제 절차:** WORKFLOW [4]에 따라 아래에 대상과 삭제해도 되는 근거를 명시하고, 검증 전에는 원본 코드를 보존한 비활성화를 우선한다. 최종 삭제는 사용자 테스트 통과 뒤 수행한다. 사용자가 승인한 명시 목록을 테스트 완료 후 같은 이유로 다시 승인받지 않는다. 단순히 오래됐거나 호출 검색이 0건이라는 이유로 지우지 않되, 특정 과거 스크립트 실행 영수증이 없다는 이유만으로 모든 후보를 보류하지 않는다. 현재 효용·런타임 영향·직접 의존·대체 검사와 복구 방법으로 판정한다. 설명 주석만 남기고 원본을 지우는 것은 비활성화가 아니다.

**이력 구분:** §1~§7은 1차 경로 정리의 당시 계획·승인·실행 기록이다. 당시 「삭제 확정 0」·기존 메뉴 유지·기능 변경 제외는 1차 범위의 결정이며 영구 제약이 아니다. §8은 작성만 했던 직전 제안으로, 19파일·33메뉴 목표와 넓은 도구 보존 목록 모두 현재는 적용하지 않는다. 최신 최소 유지 계획은 §9를 따른다.

## 1. 역할과 목적지 제안

- `Assets/Editor/Tools/`: 반복 검사·복구·재생성 도구. 필요하면 Build·Combat·VFX 하위로 구분한다.
- `Assets/Editor/Setup/`: 특정 에셋·씬에 한 번 적용하는 설정. 실행·완료 근거와 보류 사유를 구분한다. migration의 일괄 Apply가 일회성이어도 같은 파일에 상시 복구·반복 검증 API가 있으면 Tools 수명으로 보존한다.
- `Assets/Editor/Debug/`: 일시적인 강제 실패와 진단 제어. 현재 기능을 유지한다.
- `Assets/_Project/Scripts/Editor/`의 자체 도구는 위 역할에 맞춰 이관을 제안한다. namespace·class·메뉴 이름은 파일 정리만을 이유로 바꾸지 않는다.

목적지의 세부 폴더명과 실제 코드 변경은 game-programmer가 승인 범위에서 확정한다. 한 번의 정리로 중복 검사 알고리즘 통합·대형 partial 분할·공격값 수정까지 확대하지 않는다.

## 2. 변경 항목의 근거

| 제안 | 근거 원본·규칙 | 보존할 계약 |
|---|---|---|
| 상시 검사·복구 도구를 Tools에 모음 | WORKFLOW [5-2], GameSystemRules 인덱스와 해당 시스템 규칙 | 도구 수명과 파일 배치만 정리. 게임 동작은 유지. |
| 맵 템플릿 도구 보존 | GameSystemRules_RandomMap 규칙 12, WORKFLOW [5-2] | 저장 형식 변경 시 재생성과 검증 경로 유지. |
| 전투·VisualRoot 검사와 migration/rollback 쌍 보존 | GameSystemRules_UnitCombatSynchronization의 NET-ROOT-003·NET-ROOT-004, Matrix 신규·교체 프리팹 체크리스트 | Root/Visual 구조·권위·rollback 검증을 느슨하게 하지 않음. |
| 옛 Injector 격리와 Validator 처리 보류 | GameSystemRules_Units의 U-ATK-TIMELINE 및 전투 연출 규칙, Matrix의 생산 clip·event 감사 | 이미 수용한 타격값·클립을 재설정하지 않음. 실제 생산 binding 우선 계약 유지. |
| ConfirmPopup 도구 Tools 보존 권고 | GameSystemRules_MatchFlowUI의 알림 팝업 프리팹 전제, ROADMAP의 기존 미결 항목 | 삭제/재사용 결정은 사용자에게 제시. 팝업 레이아웃을 재적용하지 않음. |
| Skill·MistShrine·등록 Setup의 실행 근거 확인 | GameSystemRules_Skills·Buildings 및 CanvasSortingOrder의 해당 UI/건물 계약 | 설정 결과와 현재 기획 수치를 정리 도중 다시 쓰지 않음. |

## 3. 파일별 권고

아래는 현재 35파일의 작업 전 제안이다. 「보류」는 아직 실행·폐기 근거가 충분하지 않다는 뜻이며 삭제 예약이 아니다. 모든 이동에는 원래 `.cs.meta`가 동반된다.

| 현재 파일 | 제안 | 목적지/상태 | 이유·제약 |
|---|---|---|---|
| `Assets/_Project/Scripts/Editor/AndroidBuildAssetOptimizer.cs` | 이동 | `Assets/Editor/Tools/Build/` | WORKFLOW 기존 이동 대상. 보고서 생성도 파일 쓰기이며 Apply는 importer 변경. |
| `Assets/_Project/Scripts/Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs` | 이동·보존 | `Assets/Editor/Tools/Combat/` | 상시 검증 또는 rollback 의존. 직접 호출·메뉴 유지. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | partial 묶음 이동 | `Assets/Editor/Tools/Combat/` | VfxConnections partial과 Retiming helper 직접 호출 보존. 내부 분할 제외. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs` | partial 묶음 이동 | `Assets/Editor/Tools/Combat/` | 본체와 같은 namespace·partial 결합 유지. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitNetworkTransformInterpolationRollbackIsolation.cs` | migration과 묶음 이동 | `Assets/Editor/Tools/Combat/Migration/` | 실제 프리팹 실패 주입·복구 검사. 읽기 전용으로 오인해 실행하지 않음. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitRootPoseCrossAudit.cs` | 이동·보존 | `Assets/Editor/Tools/Combat/` | 상시 검증 또는 rollback 의존. 직접 호출·메뉴 유지. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitVisualRootRollbackIsolation.cs` | migration과 묶음 이동 | `Assets/Editor/Tools/Combat/Migration/` | 실제 프리팹·메타 실패 주입/복구. public batch 진입점과 보호 조건 유지. |
| `Assets/_Project/Scripts/Editor/Combat/SetupResultPresentationMode.cs` | 이동·보존 | `Assets/Editor/Tools/Combat/` | Dry Run/Validate/Self Validate 존재. 현재 모드 적용만으로 삭제 불가. |
| `Assets/_Project/Scripts/Editor/Combat/SetupUnitNetworkTransformInterpolation.cs` | 의존 쌍 이동 | `Assets/Editor/Tools/Combat/Migration/` | 일괄 Apply와 달리 반복 복구 API는 상시 수명. rollback 검사와 보존. |
| `Assets/_Project/Scripts/Editor/Combat/SetupUnitVisualRoots.cs` | 의존 쌍 이동 | `Assets/Editor/Tools/Combat/Migration/` | 일괄 Apply 반복 금지. 상시 Dry Run·복구 API와 rollback 검사 보존. |
| `Assets/_Project/Scripts/Editor/Combat/ValidateUnitCombatSetup.cs` | 이동·보존 | `Assets/Editor/Tools/Combat/` | 상시 검증 또는 rollback 의존. 직접 호출·메뉴 유지. |
| `Assets/_Project/Scripts/Editor/CombatHitEventInjector.cs` | 위치만 이동·퇴역 후보 | `Assets/Editor/Setup/Legacy/` | 생산 clip 선택 불일치. 메뉴 비활성화·삭제는 후속 승인 대상이며 폴더 이동만으로 실행이 차단되지 않음. |
| `Assets/_Project/Scripts/Editor/CombatHitEventValidator.cs` | 이동·기존 기능 보존 | `Assets/Editor/Tools/Combat/` | 파일 이동만 승인 범위. 옛 clip 선택 교정/현행 검사 통합은 후속 보류. |
| `Assets/_Project/Scripts/Editor/SpiritAttackVfxTestSpawner.cs` | 이동·보존 | `Assets/Editor/Debug/VFX/` | Play Mode 실제 유닛 생성·임시 시각 fallback 도구. 1회 실행 방지 guard가 비활성인 상태도 변경하지 않음. |
| `Assets/Editor/Debug/ForceRematchMapFailureMenu.cs` | 유지 | 현재 `Assets/Editor/Debug/` | 현재 강제 실패 진입점 보존. 정리 검증 중 실행하지 않음. |
| `Assets/Editor/Setup/CreateNicknameChangePopup.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/CreateNicknameSetupPanel.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/CreateProfileStatsFields.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/CreateRankingTable.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/CreateSpecialAttackConfigAsset.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/MistShrineSetup_Config.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/MistShrineSetup_Scene.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/RegisterBloomFairyPrefabs.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/RegisterMushroomBomberPrefabs.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/RegisterQuakeSpiritPrefabs.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/RetimingBloomFairyHealPresentation.cs` | 유지·폐기 보류 | 현재 `Assets/Editor/Setup/` | RetimeCurve를 회귀 코드가 호출. helper 분리 없이 전체 삭제 불가. |
| `Assets/Editor/Setup/SetupConfirmPopupAlertLayout.cs` | Tools 보존 권고·판단 보류 | `Assets/Editor/Tools/UI/` 제안 | 실행 근거와 복구 가치 있음. 기존 사용자 미결 결정을 먼저 확인. |
| `Assets/Editor/Setup/SkillSetup_CreateDataAssets.cs` | 유지·고위험 퇴역 검토 보류 | 현재 `Assets/Editor/Setup/` | 기존 placeholder 스탯·loadout 배열을 덮어쓸 수 있음. 실행 완료 증거·재사용 필요 확인 전 재실행/삭제하지 않음. |
| `Assets/Editor/Setup/SkillSetup_Scene.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/WireEarthSpiritProduction.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Setup/WireFloraProductionLine.cs` | 유지·폐기 판단 보류 | 현재 `Assets/Editor/Setup/` | 해당 버전 실행 완료·재사용 필요·의존 검토 전 삭제하거나 재실행하지 않음. |
| `Assets/Editor/Tools/LogcatCapture.cs` | 유지 | 현재 `Assets/Editor/Tools/` | 상시 로그 도구. 로그 정책과 원문 보존 유지. |
| `Assets/Editor/Tools/MapFallbackTemplateBuilder.cs` | 유지 | 현재 `Assets/Editor/Tools/` | 반복 재생성 도구. WORKFLOW 영구 보존. |
| `Assets/Editor/Tools/TorrentWaveCalibrationTool.cs` | 유지 | 현재 `Assets/Editor/Tools/` | 반복 측정 도구. 이름과 달리 임시 객체·측정 실행 부작용 별도 검토. |
| `Assets/Editor/Tools/TorrentWaveProductionValidation.cs` | 의존 보존 | 현재 `Assets/Editor/Tools/` | 측정/회귀 도구의 공용 API 사용처 보존. |

### 사용자가 승인한 실행 범위

| 구분 | 파일 수 | 실행할 내용 |
|---|---:|---|
| 기존 Scripts/Editor 분산 파일 이관 | 14 | Tools/Build 1·Tools/Combat 7·Tools/Combat/Migration 4·Setup/Legacy 1·Debug/VFX 1파일을 위 경로로 `.meta`와 함께 이동. 기존 메뉴·namespace·기능 유지. |
| ConfirmPopup 복구 도구 보존 | 1 | SetupConfirmPopupAlertLayout을 Tools/UI로 이동. 이 권고안 승인에는 기존 미결 선택을 Tools 보존으로 확정하는 것이 포함됨. |
| 그 밖의 기존 Setup·Tools·Debug | 20 | 현 위치 유지. 현재 기능·직접 의존과 역사 근거 보존. |

따라서 권고안 전체 승인 시 이동 대상은 C# 15파일과 대응 `.meta`이며 나머지 20파일은 유지한다. 파일별 표의 목적지에 대한 승인은 해당 이동의 승인으로 취급하고 같은 선택을 다시 묻지 않는다. 재실행·메뉴 비활성화·삭제·순수 helper 분리·검사 알고리즘 교정처럼 이 표에 포함되지 않은 변경만 별도로 제안한다. Injector도 위치만 옮기며 메뉴가 살아 있어 안전해진 것으로 판정하지 않는다.

## 4. 승인 후 실행 순서

1. **목록 고정:** 전체 파일·GUID·메뉴·직접 참조·partial 관계와 보호할 씬/프리팹/클립/config 해시를 기록한다. 각 Setup은 실행 근거·현재 재사용 필요·위험을 별도 표시한다.
2. **승인 범위 적용:** 사용자가 이 권고안을 승인하면 위 이동·유지 표를 그대로 실행하며 ConfirmPopup의 Tools 보존 선택을 다시 묻지 않는다. Injector의 비활성화·폐기, helper 분리 등 미포함 변경만 별도 제안한다. 이동 승인을 에셋 재설정이나 폐기 승인으로 확대하지 않는다.
3. **단순 이동 우선:** 상시 도구와 직접 의존 쌍을 `.meta`와 함께 이동한다. namespace·클래스·기존 MenuItem 경로를 유지한다. 기존 경로를 문자열로 참조하는 현재 코드·설정만 필요한 범위에서 동기화한다.
4. **후속 검토 분리:** Injector는 원본·동작을 유지한 채 Legacy 경로로만 옮긴다. 원본을 보존할 비활성화·최종 폐기, Retiming helper 분리, Validator의 생산 binding 정합화는 코드 담당자의 구체 제안·승인·검증을 거쳐 별도 단계로 다룬다. 처음 이동 작업에 기능 변경을 섞지 않는다.
5. **필요 검증:** 컴파일 대상·namespace·partial·직접 참조·메뉴 중복과 GUID 보존, 정리 범위 밖 파일 해시를 확인한다. 메뉴 확인을 위해 Setup/Apply를 실행하지 않는다. Unity 메뉴나 컴파일이 필요하면 사용자 지시 범위와 정확한 진입점을 확인한다.
6. **실제 완료 후 정리:** 사용자 테스트와 실행 완료 증거를 얻은 폐기 대상만 최종 삭제한다. 현재 문서의 경로·수명 안내와 보류 상태를 동기화하고 Task에 근거를 남긴다. 기존 Task·Log는 소급 수정하지 않는다.

별도 Testcase 작성·QA 단계는 사용자 요청이 있을 때만 진행한다. 이번 계획에서는 Unity 실행이나 게임 테스트를 수행하지 않는다.

## 5. 구현 담당과 수정 범위

| 역할 | 승인 후 담당 범위 |
|---|---|
| game-programmer | 파일 이동과 `.meta`, 필요한 현재 참조, 명시된 비활성화 및 컴파일·메뉴 정합 검증. 기능 변경이 필요하면 먼저 원인과 범위를 제안. |
| document-manager | 현재 경로·수명 안내와 승인된 보류 결정의 상시 문서 동기화, 현재 Task 결과 기록. 과거 이력 보존. |
| 메인 세션 | 사용자 결정 확인, 이동/폐기/기능수정 승인 경계 관리, 결과 독립 검토, 별도 브랜치의 변경 범위 확인. |

게임 runtime 코드·수치·씬·프리팹·애니메이션·실제 메모리 기준값은 이번 구조 정리의 기본 수정 대상이 아니다. 에디터 import가 발생하더라도 에셋 내용 변경을 정리의 당연한 결과로 수용하지 않는다.

## 6. 검증 완료 기준과 남은 선택

- 이동 전후 C# 파일 및 `.meta` 대응이 일치하고 GUID·namespace·직접 API·partial 결합이 유지된다.
- 기존 메뉴 경로가 누락·중복되지 않는다. 메뉴를 확인했다는 것과 실제 Apply를 실행했다는 것을 구별한다.
- 씬·프리팹·클립·config·importer 설정의 원치 않은 변경이 없다. 예상 밖 변경이 있으면 중단하고 원인을 확인한다.
- 현재 문서 경로는 맞추되 과거 Task의 원문은 그대로 남긴다. 문서 검사기의 실제 범위에서 0건을 확인한다.
- ConfirmPopup은 사용자의 이번 Plan 승인으로 Tools 보존 선택이 확정됐고 파일 이동을 완료했다. 과거 구현 완료를 삭제 승인으로 간주하지 않는다. main 반영은 사용자 결과 확인 뒤 진행한다.
- Injector 폐기, Validator 교정·통합, Retiming 순수 helper 분리는 이번 파일 이동과 별개 선택이다. 필요성과 변경 범위가 승인되기 전에는 진행하지 않는다.

## 7. 진행 상태

- [x] 원본 운영 규칙·실수 기록·현재 경로와 주요 의존 조사.
- [x] 기술 감사 최종 결과 반영 및 문서 정합성 검사 exit 0·문제 0건 확인.
- [x] 사용자 구현 승인 — 결과 확인 후 main 병합 지시 포함.
- [x] 승인된 15파일·대응 메타 이동, 정적 검증과 관련 현재 문서 동기화.
- [ ] 사용자 구현 결과 확인.
- [ ] main 병합.

## 8. 추가 목표 — 불필요 코드와 Unity 메뉴 정리

**대체된 제안 — 구현하지 않음:** 이 절 전체의 17파일 퇴역·19파일 유지 구조·33메뉴·추가 보존 도구와 진행 체크는 당시 계획 이력이다. 사용자의 최신 최소 유지 기준으로 대체됐으므로 현재 구현 목록이나 목표 수로 사용하지 않는다. 현행은 §9다.

1차 이동만으로 사용자가 원한 「필요 없는 스크립트 삭제·필요한 도구만 메뉴에서 분류」가 완료되지 않았다. 기준은 1차 커밋 `6d83955a`의 C# 35파일·MenuItem 선언 57개다. 추가 계획의 승인 대상은 폐기할 파일과 메뉴, 분리해 남길 helper, 유지할 기능 및 새 Unity 메뉴 경로다. 기존 경로 이동 승인을 이번 코드·메뉴 변경 승인으로 대신하지 않는다.

### 판정 원칙과 완료 조건

- 현재 효용·직접 의존·런타임 영향·대체 수단으로 폐기를 판정한다. 오래됨·미참조만으로 삭제하지 않으며, 반대로 옛 스크립트의 정확한 실행 영수증 부재만으로 모든 파일을 보류하지 않는다.
- 생성/등록/배선기, 낡은 중복 검사, 일괄 migration 실행 메뉴와 계속 필요한 검사·복구 API를 구분한다. public helper가 살아 있으면 순수 기능을 먼저 보존한 뒤 불필요 writer를 처리한다.
- 승인 후 먼저 폐기 목록의 원본을 되살릴 수 있게 비활성화하고, 필요한 도구만 사용하는 Unity 메뉴를 제공한다. 사용자가 해당 결과를 테스트 완료하면 같은 승인 목록의 파일·메타와 비활성 원문을 최종 삭제한다. 삭제 대상을 다시 승인받는 절차를 추가하지 않는다.
- 기존 에셋·공격 시각·런타임 권위와 수치를 변경하지 않는다. 메뉴 정리를 확인하려고 옛 Setup/Apply를 실행하지 않는다.
- 완료 판정은 불필요 코드의 실제 삭제, 필요한 기능·직접 의존 보존, 남길 메뉴의 일관된 분류 및 사용자 결과 확인이다. Legacy 폴더 이동이나 메뉴를 가리는 것만으로 최종 완료로 보지 않는다.

### 폐기할 기존 파일 17개 — 승인·테스트 뒤 최종 삭제

아래는 코드 담당자의 현재 효용 재감사에 따른 추가 계획이다. 지금 파일을 삭제한 것이 아니며 구현 승인 후 먼저 비활성화한다. 기존 에셋을 지우거나 재생성하지 않고, 데이터는 현재 Inspector/프리팹 authoring과 production 검증으로 관리한다.

| 현재 파일 | 폐기 근거와 남길 대체 수단 |
|---|---|
| `Assets/Editor/Setup/CreateNicknameChangePopup.cs` | 현행 Lobby의 닉네임 모달과 참조가 존재한다. 새로 만드는 옛 UI builder 대신 현재 프리팹·씬을 편집한다. |
| `Assets/Editor/Setup/CreateNicknameSetupPanel.cs` | 현행 Login에 닉네임 패널·View·Bootstrap 참조가 있다. runtime 코드와 현재 UI는 유지한다. |
| `Assets/Editor/Setup/CreateProfileStatsFields.cs` | Profile UI 실체가 있고 옛 자동 필드 생성기는 현재 UI 유지에 필요하지 않다. 기존 선택적 미연결 필드를 이번에 배선하지 않는다. |
| `Assets/Editor/Setup/CreateRankingTable.cs` | 현행 Ranking UI와 RankRow를 보존한다. 옛 생성 코드에 남은 주석상 출처는 현재 UI 정의로 정리한다. |
| `Assets/Editor/Setup/CreateSpecialAttackConfigAsset.cs` | 현재 SO·GameBootstrapper 배선을 보존한다. 값을 다시 기본값으로 생성·덮어쓰는 메뉴를 없애고 Inspector authoring을 사용한다. |
| `Assets/Editor/Setup/MistShrineSetup_Config.cs` | 현재 설정을 유지한다. 옛 임시 수치를 다시 적용하는 writer가 필요하지 않다. |
| `Assets/Editor/Setup/MistShrineSetup_Scene.cs` | 현재 패널·범위·네트워크 배선 실체를 보존한다. 옛 scene 자동 설정·저장 경로를 퇴역한다. |
| `Assets/Editor/Setup/RegisterBloomFairyPrefabs.cs` | 현재 생산 등록과 기존 프리팹은 남긴다. 유닛별 고정 등록기 대신 실제 UnitFactory 연결과 공용 검증을 사용한다. |
| `Assets/Editor/Setup/RegisterMushroomBomberPrefabs.cs` | 현재 생산 등록과 기존 프리팹을 유지하며 동일한 공용 authoring·검증으로 대체한다. |
| `Assets/Editor/Setup/RegisterQuakeSpiritPrefabs.cs` | 현재 생산 등록과 기존 프리팹을 유지하며 동일한 공용 authoring·검증으로 대체한다. |
| `Assets/Editor/Setup/RetimingBloomFairyHealPresentation.cs` | 회귀 코드가 쓰는 순수 곡선 helper를 먼저 상시 도구로 분리한 뒤, 과거 clip/prefab retime writer와 메뉴를 퇴역한다. |
| `Assets/Editor/Setup/SkillSetup_CreateDataAssets.cs` | 현재 skill 데이터·loadout을 보존하고 옛 placeholder 필드·배열을 덮어쓰는 생성기를 퇴역한다. 최종 스킬 기획이나 밸런싱을 하지 않는다. |
| `Assets/Editor/Setup/SkillSetup_Scene.cs` | 현재 skill 패널·조준·네트워크 배선과 overlay 에셋을 유지한다. 옛 자동 씬 설정·저장 경로를 제거한다. |
| `Assets/Editor/Setup/WireEarthSpiritProduction.cs` | 현재 ProductionPanel 매핑과 portrait·stage를 보존한다. 유닛별 일괄 재배선 대신 현재 authoring과 공용 검증을 사용한다. |
| `Assets/Editor/Setup/WireFloraProductionLine.cs` | 현재 Flora 생산 UI 매핑·portrait·stage를 보존하며 옛 재배선 메뉴를 퇴역한다. |
| `Assets/Editor/Setup/Legacy/CombatHitEventInjector.cs` | 옛 config→첫 Attack 클립 주입 방식은 현행 명시 production clip 우선 계약과 다르다. 현재 clip 이벤트와 승인된 타격값을 보존하고 공용 production gate를 사용한다. |
| `Assets/Editor/Tools/Combat/CombatHitEventValidator.cs` | 같은 옛 첫 Attack 선택에 기대는 중복 검사다. 현행 production timeline·profile·연결 검증이 있는 공용 검사를 남긴다. |

### 남길 기존 기능과 순수 helper

기존 35파일에서 위 17파일을 퇴역하고, 상시 순수 helper 1파일을 추가하면 최종 C# 목표는 19파일이다. 유지하는 기존 18파일에는 SelfValidation partial 2개, AuthoritativeAttack 검증, RootPose CrossAudit, ValidateUnitCombatSetup, ResultPresentationMode, migration/rollback 4개, AndroidBuildAssetOptimizer, LogcatCapture, MapFallbackTemplateBuilder, Torrent 측정·검증 2개, ConfirmPopup 복구 도구, SpiritAttackVfxTestSpawner, ForceRematchMapFailureMenu가 포함된다.

Retiming의 `RetimeCurve`와 그 내부 검증·유한값 검사 기능은 상시 순수 helper로 보존하고 회귀 호출을 새 helper로 연결한다. helper는 MenuItem·AssetDatabase 저장·씬·프리팹 변경 없이 계산만 담당한다. 소스 파일 삭제를 위해 회귀 조건이나 곡선 변환 결과를 느슨하게 하지 않는다.

기존 VisualRoot/NetworkTransform의 Apply·Recover와 rollback public batch API는 복구 목적에 필요하므로 유지한다. 일상 Unity 메뉴에서 불필요한 Apply·중복 진입점을 빼는 것과 필요한 API 자체를 삭제하는 것은 구별한다. Spawner는 실제 Play Mode 미리보기 기능이 있어 Debug 도구로 남기고, native 전체 검증 도구로 소개하지 않는다.

### Unity 메뉴 구조와 선언 수 목표

| 기준과 처리 | MenuItem 선언 수 |
|---|---:|
| 현재 기준 `6d83955a` | 57 |
| 17개 기존 파일의 퇴역으로 제거 | 17 |
| 필요한 API는 남기되 중복·과거 일괄 실행 MenuItem 제거 | 7 |
| 추가 단계 최종 목표 | 33 |

C# 파일 목표는 기존 35 - 퇴역 17 + 순수 helper 1 = 19다. 비활성화 중간 단계는 기존 원문 35 + helper 1 = 실물 36파일이며, 17개를 컴파일에서 비활성화해 필요한 19파일만 활성화한다. 최종 삭제 뒤 실제 파일이 19개가 된다. 비활성 원문 안의 MenuItem 문자열은 실제 컴파일 메뉴로 세지 않는다. MenuItem 33개는 활성 선언 목표이며 실제 Unity 가시 항목 수는 컴파일 뒤 확인해야 한다.

살릴 최상위 메뉴는 Combat 5·Logs 3·Map 2·Build 2·UI 1·Debug 20개 선언으로 구분한다. 읽기·회귀 검사와 에셋/씬을 쓰는 복구·보정 메뉴를 구별하고 후자에는 Writes/Deletes 표기를 둔다. 새 메뉴는 다음과 같다.

```text
Hexiege
  Debug
    Failure
      다음 재경기
        ① 연결 끊김 (Disconnected)
        ② 해시 불일치 (HashMismatch)
        ③ 응답 없음 (ResponseTimeout · 약 20초 소요)
        ④ 수락 전송 예외 (AcceptSendException)
      다음 최초 경기
        ① 연결 끊김 (Disconnected)
        ② 해시 불일치 (HashMismatch)
        ③ 응답 없음 (ResponseTimeout · 약 20초 소요)
      싱글 맵 준비 (다음 1회)
      싱글 맵 투영 (다음 1회)
      현재 설정 확인
      해제
    VFX
      Spawn Spirit Combat Preview
      Torrent Wave
        Diagnose Bake Coordinates
        Measure Seeded Production Particles
        Measure Production Runtime Helper
        Measure Production Directions
    Recovery
      Restore Pending Interpolation Migration (Writes Assets)
      Restore Pending Visual Root Migration (Writes Assets)
    Scene
      Apply Result Presentation Mode (Writes Scene)
      Self Validate Mode Tool
  Build
    Report Asset Sizes
    Apply Android Texture Defaults (Writes Assets)
  Combat
    Run Full Regression Suite
    Logs
      Analyze Latest Root Pose
    Diagnostics
      Self Validate Root Pose Audit
    Validate Scene Presentation Mode
    Validate Unit Setup
  Logs
    Clear Device Buffer (Deletes Logs)
    Save Device Logs
    Clean Old Editor Logs (Deletes Assets)
  Map
    Rebuild Fallback Templates (Writes Assets)
    Validate Fallback Templates
  UI
    Repair ConfirmPopup Layout (Writes Prefab)
```

전체 57개 선언의 old→new/drop 대응은 다음과 같다. 삭제되는 예전 경로의 호환 alias를 남겨 옛 writer를 계속 노출하지 않는다. 필요한 migration/rollback의 public batch API와 일상 메뉴의 폐기는 별개다.

| 기존 Unity 메뉴 | 새 경로 또는 폐기 | 처리 근거·부작용 |
|---|---|---|
| `Hexiege/Debug/강제 실패 — 다음 재경기/① 연결 끊김 (Disconnected)` | `Hexiege/Debug/Failure/다음 재경기/① 연결 끊김 (Disconnected)` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 다음 재경기/② 해시 불일치 (HashMismatch)` | `Hexiege/Debug/Failure/다음 재경기/② 해시 불일치 (HashMismatch)` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 다음 재경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)` | `Hexiege/Debug/Failure/다음 재경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 다음 최초 경기/① 연결 끊김 (Disconnected)` | `Hexiege/Debug/Failure/다음 최초 경기/① 연결 끊김 (Disconnected)` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 다음 최초 경기/② 해시 불일치 (HashMismatch)` | `Hexiege/Debug/Failure/다음 최초 경기/② 해시 불일치 (HashMismatch)` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 다음 최초 경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)` | `Hexiege/Debug/Failure/다음 최초 경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 다음 재경기/④ 수락 전송 예외 (AcceptSendException)` | `Hexiege/Debug/Failure/다음 재경기/④ 수락 전송 예외 (AcceptSendException)` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 싱글 맵 준비 (다음 1회)` | `Hexiege/Debug/Failure/싱글 맵 준비 (다음 1회)` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 싱글 맵 투영 (다음 1회)` | `Hexiege/Debug/Failure/싱글 맵 투영 (다음 1회)` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 현재 설정 확인` | `Hexiege/Debug/Failure/현재 설정 확인` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/Debug/강제 실패 — 해제` | `Hexiege/Debug/Failure/해제` | EditorSessionState 다음1회실패예약/조회/해제 |
| `Hexiege/VFX/Spawn Spirit Attack Test` | `Hexiege/Debug/VFX/Spawn Spirit Combat Preview` | PlayMode runtime writer |
| `Hexiege/Setup/Create Nickname Change Popup (Lobby)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Create Nickname Setup Panel (Login)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Create Profile Stats Fields (Lobby)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Create Ranking Table (Lobby)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Create SpecialAttackConfig Asset (Game)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Combat/Inject OnAttackHit Events (From Config)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/MistShrine/1. Apply Config Values (SpecialAttackConfig)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/MistShrine/2. Setup Scene (Panel, Range, Network)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Register BloomFairy Prefabs (Game)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Register MushroomBomber Prefabs (Game)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Register QuakeSpirit Prefabs (Game)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Retiming BloomFairy Heal Presentation (4s)` | 도구와 메뉴 퇴역 | 함수분리후폐기 |
| `Hexiege/Skill/1. Create Skill Data Assets` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Skill/2. Setup Scene (Panel, Aim, Network)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Wire Earth Spirit Production (Game)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Setup/Wire Flora Production Line (Game)` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Build Size/Generate Asset Size Report` | `Hexiege/Build/Report Asset Sizes` | 보고서파일쓰기 |
| `Hexiege/Build Size/Apply Android Texture Defaults` | `Hexiege/Build/Apply Android Texture Defaults (Writes Assets)` | importer/meta writer |
| `Hexiege/Combat/Validate Attack Hit Events` | 도구와 메뉴 퇴역 | 폐기 |
| `Hexiege/Combat/Network Transform/Test Rollback Isolation at Changed Prefab 25` | MenuItem 제거·API 보존 | 현재pending0이라25개변경전제충족불가.일상메뉴제거,격리batch Run/API보존 |
| `Hexiege/Combat/Network Transform/Dry Run Disable Position Lerp Smoothing` | MenuItem 제거·API 보존 | 현재50/50false,구조검사에서false검증함.중복일상DryRun메뉴제거;API보존 |
| `Hexiege/Combat/Network Transform/Apply Disable Position Lerp Smoothing` | MenuItem 제거·API 보존 | 현재50/50false.과거일괄writer의일상Apply메뉴제거;rollback용publicAPI보존 |
| `Hexiege/Combat/Network Transform/Recover Pending Position Lerp Smoothing Apply` | `Hexiege/Debug/Recovery/Restore Pending Interpolation Migration (Writes Assets)` | pendingjournal asset복구writer |
| `Hexiege/Combat/Visual Root/Dry Run Unit Visual Root Migration` | MenuItem 제거·API 보존 | 현재50/50완료.일상구조검사ValidateUnitCombatSetup에통합안내;public DryRun/Apply/Recover는batch복구의존때문보존 |
| `Hexiege/Combat/Visual Root/Apply Unit Visual Root Migration` | MenuItem 제거·API 보존 | 과거B0→B1일괄migration.Apply일상메뉴제거;복구/isolatedbatch용publicAPI보존 |
| `Hexiege/Combat/Visual Root/Recover Pending Unit Visual Root Migration` | `Hexiege/Debug/Recovery/Restore Pending Visual Root Migration (Writes Assets)` | pendingjournal asset복구writer |
| `Hexiege/Diagnostics/Self Validate Authoritative Attack Presentation` | MenuItem 제거·API 보존 | 전체전투회귀Run에서동일Run을호출하므로별도중복MenuItem제거;public Run/API는보존 |
| `Hexiege/Combat/Run Unit Action Self Validation` | `Hexiege/Combat/Run Full Regression Suite` | fixture/Temp보고서작성,production자산보존 |
| `Hexiege/Combat/Diagnostics/Analyze Latest Unit Root Pose Logs` | `Hexiege/Combat/Logs/Analyze Latest Root Pose` | 기존로그읽기후처리 |
| `Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit` | `Hexiege/Combat/Diagnostics/Self Validate Root Pose Audit` | fixture검사 |
| `Hexiege/Combat/Result Presentation Mode/Dry Run` | MenuItem 제거·API 보존 | 같은필드Inspect를공유하는Validate일상메뉴하나로통합(publicDryRun유지) |
| `Hexiege/Combat/Result Presentation Mode/Validate` | `Hexiege/Combat/Validate Scene Presentation Mode` | 씬설정읽기 |
| `Hexiege/Combat/Result Presentation Mode/Apply` | `Hexiege/Debug/Scene/Apply Result Presentation Mode (Writes Scene)` | Game씬단일필드writer+rollback |
| `Hexiege/Combat/Result Presentation Mode/Self Validate` | `Hexiege/Debug/Scene/Self Validate Mode Tool` | inmemory fixture검사 |
| `Hexiege/Combat/Visual Root/Validate Unit Combat Setup` | `Hexiege/Combat/Validate Unit Setup` | 50prefab/NetworkTransform읽기 |
| `Hexiege/Logcat/1. 버퍼 비우기` | `Hexiege/Logs/Clear Device Buffer (Deletes Logs)` | adb로그버퍼삭제 |
| `Hexiege/Logcat/2. 파일로 저장` | `Hexiege/Logs/Save Device Logs` | adb읽기/로그파일쓰기 |
| `Hexiege/Logcat/3. 오래된 에디터 로그 정리` | `Hexiege/Logs/Clean Old Editor Logs (Deletes Assets)` | 보존정책하로그/meta삭제 |
| `Hexiege/무작위 맵/1. 폴백 템플릿 5개 다시 만들기` | `Hexiege/Map/Rebuild Fallback Templates (Writes Assets)` | bytes/참조문서재생성 |
| `Hexiege/무작위 맵/2. 저장된 폴백 템플릿 검사` | `Hexiege/Map/Validate Fallback Templates` | 저장template읽기검사 |
| `Hexiege/Combat/Torrent Wave/Diagnose Bake Coordinates` | `Hexiege/Debug/VFX/Torrent Wave/Diagnose Bake Coordinates` | 임시particle/CSV보고서,sourceasset hash보호 |
| `Hexiege/Combat/Torrent Wave/Measure Seeded Production Particles` | `Hexiege/Debug/VFX/Torrent Wave/Measure Seeded Production Particles` | 임시particle/CSV보고서,sourceasset hash보호 |
| `Hexiege/Combat/Torrent Wave/Measure Production Runtime Helper` | `Hexiege/Debug/VFX/Torrent Wave/Measure Production Runtime Helper` | 임시particle/CSV보고서,sourceasset hash보호 |
| `Hexiege/Combat/Torrent Wave/Measure Production Directions` | `Hexiege/Debug/VFX/Torrent Wave/Measure Production Directions` | 임시particle/CSV보고서,sourceasset hash보호 |
| `Hexiege/Setup/Setup ConfirmPopup Alert Layout` | `Hexiege/UI/Repair ConfirmPopup Layout (Writes Prefab)` | 복구prefab writer |

### 남길 helper와 Spawner의 기존 기능

`Assets/Editor/Tools/Combat/BloomHealRetimingValidationUtility.cs`를 추가해 `RetimeCurve`·`ValidateSamples`·`Finite`·`Check`를 원래 동작 그대로 보존한다. `RunUnitActionSelfValidation`의 호출을 새 helper로 옮긴 뒤 기존 first/second·tail/sample curve 회귀와 finite/weighted/step/Auto 거부 조건을 확인한다. 옛 writer의 `Run`과 `SetStopTime`은 helper에 이식하지 않는다.

Spawner는 실제 UnitSpawn 기반 Play Mode 미리보기와 raw VFX fallback·PreviewDriver를 포함한 기존 기능을 그대로 유지한다. fallback은 시각 실험을 위한 별도 경로이므로 실제 생산 타이밍·권위 Impact 검증으로 소개하지 않는다. 이번에는 메뉴 분류와 사용 범위 안내만 바꾸며 fallback 알고리즘이나 타격값을 변경하지 않는다.

### 구현·검증·실제 삭제 순서

1. 17개 퇴역 파일·메타의 원본 범위를 고정하고 직렬화 GUID·직접 호출을 확인한다. 현재 기획·씬·프리팹·클립·config의 보호 기준을 기록한다.
2. 추가 Plan 승인 뒤 순수 helper를 추출하고 호출을 전환한다. 퇴역 원본은 복원 가능하게 비활성화하며, 필요 메뉴를 새 분류로 노출한다. 원본 비활성 코드 자체를 이 단계에서 지우지 않는다.
3. 현재 컴파일 참조에 맞춰 Editor 코드와 helper 회귀, 메뉴 대응 57행·중복·누락을 검증한다. 검사기를 느슨하게 하거나 단순 config 대조표가 모든 생산 검증으로 완전히 대체됐다고 주장하지 않는다. 바뀌지 않아야 할 에셋·runtime 내용을 대조한다.
4. 사용자에게 필요한 새 메뉴와 결과를 공유한다. 사용자 확인 범위에서 Unity import/컴파일·메뉴 표시·실제 사용 결과를 확인하며 destructive Setup을 재실행하지 않는다. 전25종 밸런싱·공격 재검증을 새로 요구하지 않는다.
5. 사용자가 테스트 완료하면 이미 승인한 17파일과 대응 메타의 비활성 원문을 최종 삭제한다. 현재 트리에서 직렬화 GUID 참조와 남길 코드의 직접 참조가 해소됐는지 확인한다. 필요한 public 복구·batch API·새 helper는 남긴다. 아래에 명시한 빈 폴더 정리는 이 단계에서만 한다.
6. 현재 문서의 메뉴·대체 authoring·도구 수명 안내를 맞추고 Task에 실제 삭제 목록과 검증 경계를 기록한다. 사용자가 결과를 확인한 뒤 통합 브랜치를 main에 병합한다.

최종 삭제는 실행에 의해 생성된 기존 에셋·로그·runtime 스크립트까지 지우는 것이 아니다. 기록되지 않은 외부 executeMethod 호출은 완전 열거할 수 없으므로 퇴역할 public 일회성 진입점의 호환 중단을 이 Plan에서 명시한다.

최종 파일 삭제 뒤 `Assets/Editor/Setup/Legacy/`, `Assets/Editor/Setup/`, `Assets/_Project/Scripts/Editor/Combat/`, `Assets/_Project/Scripts/Editor/`가 실제로 비었는지 확인하고, 빈 디렉토리와 각 대응 폴더 메타만 정리한다. 비어 있지 않으면 안의 다른 파일을 재귀 삭제하지 않고 남긴 항목을 보고한다. 상위 `Assets/Editor/`나 Tools/Debug·그 메타는 이 삭제 범위가 아니다.

### 추가 단계의 변경 파일과 문서 역할

| 대상 | 변경 내용 |
|---|---|
| 위 퇴역 17파일과 대응 메타 | 비활성화 후 사용자 테스트 완료 시 최종 삭제 |
| 새 BloomHealRetimingValidationUtility 및 메타 | 순수 helper 보존 |
| 유지하는 Editor 도구 | 표의 MenuItem 경로 정리·중복 항목 제거와 SelfValidation의 helper 호출 전환. Spawner의 기존 기능은 유지 |
| 최종 삭제 뒤 실제 빈 폴더와 대응 메타 | 위 4개 경로가 비었을 때만 정리. 비활성화 단계에서는 유지 |
| WORKFLOW [5-2] | 현행 필요 도구·폐기 결과·사용 메뉴 안내 |
| UnitCombatAssetMatrix | 현행 검증 진입점과 신규 프리팹 authoring 안내. 기존 수용·수치는 유지 |
| GameSystemRules_Units의 도구 안내 | SpecialAttackConfig 기본 CreateAssetMenu/Inspector 배선 및 현재 production gate 안내. 옛 Injector 실행 의무가 남지 않도록 교정 |
| ROADMAP·MatchFlowUI | ConfirmPopup 복구 도구의 새 메뉴를 필요한 현재 포인터에만 반영. 날짜별 원문은 보존 |
| 현재 Research/Plan | 상세 35파일·57선언 대응·승인·비활성화·삭제·검증의 실제 결과 |
| `.claude/mistakes.md` | WORKFLOW 필수 의무로 이번 목표 누락 1사건을 목차·말미에 추가. 기존 원문·메모리·기준값은 보존 |

현재 이 표는 추가 구현 계획이며 코드·메뉴·상시 규칙 문서는 아직 변경하지 않았다. 계획 작성 외 실제 추가 기록은 이번 목표 누락 사건 하나다.

### 역할과 문서 반영 범위

game-programmer는 35파일의 폐기·유지·helper 분리와 메뉴 변경을 구체화한다. document-manager는 현재 Task에 상세 근거를 모으고, 구현 뒤 WORKFLOW·Matrix·Units 및 필요성이 달라진 ROADMAP/MatchFlowUI 안내만 역할에 맞게 교정한다. 기존 Task·Log·역사 원문과 에이전트 메모리·기준값은 보존한다. 메인 세션이 변경 계획과 실제 결과를 검토하며 main 병합은 사용자 결과 확인 뒤 진행한다. 별도 Testcase·QA 단계는 새로 만들지 않는다.

### 현재 진행

- [x] 사용자 의도를 불필요 코드 삭제·사용 메뉴 정리로 반영.
- [x] 파일별 재감사와 구체 폐기·유지·helper 분리 목록, 메뉴 트리 제안 확정.
- [ ] 변경 Plan 공유 및 구현 승인.
- [ ] 비활성화·필요 기능 보존·메뉴 재분류.
- [ ] 사용자 테스트 완료 뒤 승인 목록 최종 삭제.
- [ ] 사용자 결과 확인 및 main 병합.

## 9. 현행 계획 — 실제 쓰는 필수 도구만 유지

### 사용자 결정과 필수 진입점

사용자는 AndroidBuildAssetOptimizer를 포함한 나머지 도구도 나중에 필요하면 그 상황에 맞춰 다시 작성하기로 했다. 현재 쌓인 도구가 혼란을 만드는 것이 문제이므로, 「재사용 가능」·「복구 가치 있음」만으로 계속 남기지 않는다. Android 크기 보고서 기능도 이번 유지 목록에서 제외한다.

| 유지할 진입점 | 현재 파일 | 유지 목적 |
|---|---|---|
| 로그 저장 | `Assets/Editor/Tools/LogcatCapture.cs` | 실제 사용하는 로그 저장 기능 |
| 네트워크 강제 실패 | `Assets/Editor/Debug/ForceRematchMapFailureMenu.cs` | 연결 끊김·해시 불일치·응답 없음 등 필요한 실패 재현 |
| 전투 회귀 검사 | `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` | 전투 교정에서 주로 사용한 전체 검사 진입점 |
| Root Pose 로그·검사 | `Assets/Editor/Tools/Combat/RunUnitRootPoseCrossAudit.cs` | 전투 교정에서 주로 사용한 로그 대조·검사 진입점 |

코드 담당자가 확인한 실제 의존은 위 네 파일과 `RunUnitActionSelfValidation.VfxConnections.cs` partial, `RunAuthoritativeAttackPresentationValidation.cs`, `TorrentWaveProductionValidation.cs`다. 따라서 기존 7파일을 유지한다. 뒤 세 파일은 검사기 내부 보조 기능이며 Authoritative의 단독 메뉴는 제거한다. Bloom의 `RetimeCurve`·`ValidateSamples`·`Finite`·`Check`는 새 파일을 추가하지 않고 기존 Action 검사기 안의 private helper로 옮겨 회귀 조건을 유지한다.

### 현재 제거 대상으로 보는 범위

| 범위 | 현행 결정 |
|---|---|
| AndroidBuildAssetOptimizer | Apply와 보고서 기능을 모두 제거한다. 향후 필요 시 재작성하며 보고서 전용 도구를 따로 남기지 않는다. |
| 기존 생성·등록·배선·Retiming writer와 옛 Injector/Validator | 필수 검사가 실제 사용하는 순수 기능만 먼저 보존하고 나머지 일회성·낡은 중복 도구를 정리한다. |
| MapFallbackTemplateBuilder | 필수 검사 직접 의존이 없으면 제거한다. 기존 맵 bytes·참조 원본과 런타임 `MapFallbackTemplateFactory`/Domain 생성·검증 로직은 보존한다. |
| migration·rollback 4개, ValidateUnitCombatSetup, SetupResultPresentationMode | 필수 검사의 실제 의존만 확인해 필요한 내부 코드 외에는 제거한다. 별도 migration/복구 메뉴를 유지하기 위한 상시 묶음은 남기지 않는다. |
| ConfirmPopup 복구 도구 | 직전 Tools 보존 선택보다 최신 최소 유지 지시를 우선한다. 필수 검사 의존이 없으면 제거하며 기존 팝업 프리팹과 배치는 유지한다. |
| SpiritAttackVfxTestSpawner, TorrentWaveCalibrationTool | 필수 검사의 직접 의존이 없으면 미리보기·측정 메뉴와 함께 제거한다. 실제 VFX 에셋·runtime 동작은 유지한다. |

최종 제안은 기존 C# 35파일 중 7파일 유지·28파일 퇴역이며 새 C# 파일은 추가하지 않는다. 비활성화 검증 중에는 기존 실물 35파일과 원문을 남기고 활성 코드는 유지 7파일만 남긴다. 사용자 테스트 완료 뒤 퇴역 28파일과 대응 메타를 실제 삭제한다. 필수 검사의 조건을 약화하거나 현재 기능과 무관한 회귀 범위를 추가하지 않는다.

| 제거할 기존 파일 |
|---|
| `Assets/Editor/Debug/VFX/SpiritAttackVfxTestSpawner.cs` |
| `Assets/Editor/Setup/CreateNicknameChangePopup.cs` |
| `Assets/Editor/Setup/CreateNicknameSetupPanel.cs` |
| `Assets/Editor/Setup/CreateProfileStatsFields.cs` |
| `Assets/Editor/Setup/CreateRankingTable.cs` |
| `Assets/Editor/Setup/CreateSpecialAttackConfigAsset.cs` |
| `Assets/Editor/Setup/Legacy/CombatHitEventInjector.cs` |
| `Assets/Editor/Setup/MistShrineSetup_Config.cs` |
| `Assets/Editor/Setup/MistShrineSetup_Scene.cs` |
| `Assets/Editor/Setup/RegisterBloomFairyPrefabs.cs` |
| `Assets/Editor/Setup/RegisterMushroomBomberPrefabs.cs` |
| `Assets/Editor/Setup/RegisterQuakeSpiritPrefabs.cs` |
| `Assets/Editor/Setup/RetimingBloomFairyHealPresentation.cs` |
| `Assets/Editor/Setup/SkillSetup_CreateDataAssets.cs` |
| `Assets/Editor/Setup/SkillSetup_Scene.cs` |
| `Assets/Editor/Setup/WireEarthSpiritProduction.cs` |
| `Assets/Editor/Setup/WireFloraProductionLine.cs` |
| `Assets/Editor/Tools/Build/AndroidBuildAssetOptimizer.cs` |
| `Assets/Editor/Tools/Combat/CombatHitEventValidator.cs` |
| `Assets/Editor/Tools/Combat/Migration/RunUnitNetworkTransformInterpolationRollbackIsolation.cs` |
| `Assets/Editor/Tools/Combat/Migration/RunUnitVisualRootRollbackIsolation.cs` |
| `Assets/Editor/Tools/Combat/Migration/SetupUnitNetworkTransformInterpolation.cs` |
| `Assets/Editor/Tools/Combat/Migration/SetupUnitVisualRoots.cs` |
| `Assets/Editor/Tools/Combat/SetupResultPresentationMode.cs` |
| `Assets/Editor/Tools/Combat/ValidateUnitCombatSetup.cs` |
| `Assets/Editor/Tools/MapFallbackTemplateBuilder.cs` |
| `Assets/Editor/Tools/TorrentWaveCalibrationTool.cs` |
| `Assets/Editor/Tools/UI/SetupConfirmPopupAlertLayout.cs` |

### 최소 메뉴

| 남길 기능 | 활성 MenuItem 선언 목표 |
|---|---:|
| Logcat 로그 저장·운영 | 3 |
| 네트워크 강제 실패 | 11 |
| Action 전투 전체 검사 | 1 |
| Root Pose 실제 로그 분석·자체 검증 | 2 |
| 합계 | 17 |

사용 메뉴는 로그·강제 실패·전투 검사로만 구분한다. 나머지 Build·Map·UI 복구·VFX preview/측정·migration 사용 메뉴는 제거한다. 위 17은 코드 담당자가 제시한 활성 선언 목표이며 Unity 가시 메뉴 수를 실행으로 확인한 값이 아니다. Root Pose의 실제 로그 분석과 자체 검증은 모두 남기고 내부 보조 검사에는 단독 메뉴를 추가하지 않는다.

### 구현 시 함께 바로잡을 현재 문서

사용자 최신 지시는 기존 도구 영구 보존 문구보다 우선한다. 아래 충돌은 삭제를 보류하거나 같은 선택의 재허락을 요구할 근거가 아니며, 승인된 구현에 맞춰 현재 문서의 해당 안내를 교정 중이다. 날짜별 원문과 게임 동작·수치는 보존한다.

| 문서 위치 | 예정 교정 |
|---|---|
| WORKFLOW [5-2]의 Tools 영구 보존·migration 복구·현행 도구 목록 | 필수 진입점과 실제 내부 의존만 남기는 이번 사용자 결정, 불필요 도구는 필요 시 재작성하는 원칙 반영 |
| GameSystemRules_RandomMap 규칙 12의 템플릿 제작 도구 삭제 금지 문구 | 제작 메뉴 제거와 runtime 폴백·템플릿 자산 보존을 구분하도록 정정 |
| TechnicalDesignDocument_Network의 폴백 템플릿 제작·검증 계약 절, 특히 제작 도구 영구 보존 안내 | 동일한 사용자 결정을 반영. 저장 형식·기존 bytes·runtime 생성/검증 계약은 유지 |
| Matrix·Units의 현재 검사·생성 도구 안내 | 남길 실제 전투 진입점과 내부 보조 기능, 현재 authoring 방법으로 안내. 기존 수치·유닛 수용은 유지 |
| ROADMAP·MatchFlowUI의 ConfirmPopup 도구 포인터 | 과거 구현·이동 기록을 보존하면서 최신 제거 결정과 프리팹 보존을 명확히 구분 |

### 승인·비활성화·검증·삭제 순서

1. 위 7파일·28파일 목록을 현재 트리와 대조하고, 네 사용자 진입점의 내부 의존과 private helper 이전 범위를 고정한다.
2. 갱신한 Plan을 공유하고 구현 승인을 받는다. 현재 사용자 메시지는 최소 유지 기준의 결정이며 코드 삭제를 이미 실행한 것으로 기록하지 않는다.
3. 승인 뒤 필수 의존을 보존·정리하고 나머지 원본을 복원 가능하게 비활성화한다. 필요한 사용자 메뉴만 남긴다. 실제 설정·씬·프리팹·클립·맵 bytes와 runtime 로직은 변경하지 않는다.
4. 필수 검사와 코드 참조·컴파일 정합성, 메뉴 및 보호 자산을 검증하고 사용자에게 결과를 공유한다. 검사 기준을 낮추지 않으며 native 미실행을 PASS로 기록하지 않는다.
5. 사용자가 테스트 완료하면 같은 승인 목록의 비활성 원문·파일·대응 메타를 최종 삭제한다. 동일 결정을 반복 승인받지 않는다. 삭제 뒤 실제 빈 폴더만 정리한다.
6. 현재 문서를 사용자 결정에 맞게 동기화하고 결과 확인 뒤 main에 병합한다. 새 Testcase·QA 문서·메모리 갱신은 추가하지 않는다.

### 현재 진행

- [x] 사용자 최신 최소 유지 기준 반영 및 직전 제안 대체 표시.
- [x] 필수 검사 실제 의존 감사와 기존 7파일 유지·28파일 퇴역·메뉴 17선언 계획 반영.
- [x] 변경 Plan §9 공유 및 구현 승인 — 사용자 「알겠어 이렇게 정리하자」.
- [x] 원문 28개 비활성화·활성 7파일/17메뉴 구현 및 정적 검증. Research §12의 실행 주체·증거·미실행 경계 참조.
- [x] 사용자 명시적 최종 삭제 지시에 따라 승인 목록 28개·대응 메타 28개·빈 폴더 메타 8개를 삭제하고 정적 검증했다. 현재 C# 7개·활성 메뉴 17선언이며 Unity import·컴파일·메뉴 실행은 미실행.
- [x] 앞선 비활성 구현의 사용자 main 병합 승인 및 `ff0ea9e9` → `f9dfb292` fast-forward 반영. 당시 실제 삭제는 미완료였으며 후속 최종 삭제 결과는 Research §13을 따른다.
- [x] 최종 삭제 커밋 `58614539`를 main에 fast-forward 병합. 사용자 테스트 PASS·Unity 실행 결과는 별도로 미확인.
