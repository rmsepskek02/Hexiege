# 에디터 스크립트 정리 — 계획

계속 쓰는 검사·복구 도구는 한 곳에서 찾을 수 있게 모으고, 한 번 실행하기 위한 설정 스크립트는 수명과 부작용을 드러내도록 정리한다. 기존 게임 설정과 이미 검증된 공격 시각은 보존하며, 파일 이동과 실제 설정 실행을 구분한다.

**현재 상태: 승인된 경로 이동·정적 검증·문서 동기화 완료, 사용자 결과 확인 대기.** 15파일과 대응 기존 메타를 바이트 보존 이동했고 나머지 20파일을 유지했다. 상세 mapping과 검증 근거는 Research §8에 기록했다. 동작 변경·메뉴 실행·파일 삭제는 하지 않았으며 main 병합은 사용자의 결과 확인 뒤 진행한다.

문서 정리·검사기 정비 이력이 포함된 `codex/editor-script-organization`을 사용자가 요청한 세 작업의 통합 결과 브랜치로 유지한다. 에디터 변경까지 같은 브랜치에서 관리하며 확정 이력은 Git log를 기준으로 확인한다. main 병합의 사용자 결과 확인 경계는 그대로 유지한다.

**삭제 후보 처리 원칙:** WORKFLOW [4]에 따라 제거 사유를 먼저 제시하고 검증 전에는 원본 코드를 보존한 비활성화를 우선한다. 최종 삭제는 사용자 테스트 통과 뒤 수행한다. 단순히 오래됐거나 호출 검색이 0건이라는 이유로 삭제하지 않는다. 이번 조사에서 즉시 삭제가 확정된 파일은 없다. 실행 완료와 폐기 결정이 미입증이면 보류하며, 삭제 후 설명 주석만 남기는 방법으로 비활성화 절차를 대신하지 않는다.

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
