# 에디터 스크립트 정리 — 조사

사용자가 실제 사용하는 로그 저장·네트워크 강제 실패와 전투 교정의 두 검사 진입점만 남겨 에디터 도구를 최소화한다. 미래에 필요할 수 있다는 이유로 도구를 계속 쌓아 두지 않으며 그때 상황에 맞춰 다시 작성한다. 필수 검사의 실제 내부 의존은 보존하되 기존 씬·에셋·맵 bytes·runtime 로직은 변경하지 않는다.

**현재 상태: 사용자 명시 승인에 따른 28파일 최종 삭제·정적 검증·main 병합 완료.** 활성 7파일·17메뉴를 보존했다. 직접 삭제 요청을 사용자 테스트 PASS로 해석하지 않는다.

**현재 확인 프로젝트:** 기존 Unity 프로젝트 `D:/Projects/Hexiege/Hexiege`의 `main`에 최종 삭제 커밋 `58614539`를 반영했다(`946ade38` → `58614539` fast-forward). Unity 컴파일·기기·메뉴 실행은 미확인이고 푸시는 하지 않았다.

별도 폴더에 미커밋 변경을 둔 채 브랜치만 안내해 사용자가 기존 프로젝트에서 결과를 확인하지 못하는 혼란이 있었다. 변경을 커밋하고 기존 Unity 프로젝트를 작업 브랜치로 전환해 해소했다. 이전 `.utmp/docs-checker-finalize`는 현재 detached 조회용이며 아래 해당 경로·미반영 기록은 전환 전 이력이다.

## 1. 기준과 조사 범위

§1~§9는 1차 경로 정리의 조사·실행·검증 이력이다. §10은 사용자 의도 정정 뒤 작성했던 직전 제안이며 구현하지 않았다. 19파일·33메뉴 목표는 최신 최소 유지 기준으로 대체됐고 현행 조사·계획은 §11 및 Plan §9를 따른다.

- 작업 브랜치: `codex/editor-script-organization`.
- 기준 커밋: `ff0ea9e92b63116e7fe46e825c69e5653022ae85`.
- 작업 경로: `D:/Projects/Hexiege/Hexiege/.utmp/docs-checker-finalize`. 공유 primary 경로의 다른 작업은 수정하지 않는다.
- 프로젝트 자체 관리 C# 대상: `Assets/Editor/` 및 `Assets/_Project/Scripts/Editor/` 아래. 외부 패키지·플러그인 Editor 코드는 제외한다.
- `rg --files` 목록과 `.utmp/editor-script-organization/inventory.json`을 대조한 결과 35파일이다. 전자는 21파일, 후자는 14파일이다. partial 파일 수와 논리적인 도구 수는 다르다.
- 조사 근거: CLAUDE.md, WORKFLOW.md [4]·[5-2]·[11], GameSystemRules.md, 에이전트 정의·공용 메모리·document-manager 메모리 인덱스, 관련 규칙과 기존 Task의 실행 기록. 기존 Task·Log는 읽기만 한다.
- 수명 분류·안전 조건은 문서 담당이 정리하고, 35파일의 메뉴·참조·쓰기 동작·GUID·컴파일 조건은 game-programmer가 감사한다. 정적 쓰기 API 검색 수를 실제 에셋 변경 수로 해석하지 않는다.

## 2. 권위 원본과 현재 불일치

| 원본 | 확인 내용 | 이번 정리의 의미 |
|---|---|---|
| WORKFLOW [5-2] 「에디터 스크립트를 두는 폴더」 | Setup은 일회성, Tools는 영구 보존. 이동 시 `.cs.meta` 동반과 namespace 영향 확인. | 반복 도구와 일회성 설정을 수명으로 구분한다. 폴더명만으로 삭제를 확정하지 않는다. |
| 같은 절 「후속 판단 대기」 | AndroidBuildAssetOptimizer는 Tools 이동 대상. Injector·Validator·Spawner는 일회성 헤더와 반복 사용 성격이 충돌해 판단 보류. | 이번 Plan은 기존 보류를 다루지만 이미 해결된 결정으로 기록하지 않는다. |
| WORKFLOW [4] 「기존 로직 제거」 | 검증 전 비활성화 우선, 사용자 테스트 통과 후 최종 삭제. | 단순 삭제 후 설명 주석을 남기는 것으로 대체할 수 없다. |
| ROADMAP의 2026-09-21 SetupConfirmPopupAlertLayout 항목 | 멱등 복구 도구로 보존할지 삭제할지 사용자 판단 미결. | Tools 보존을 권고하되 이번 일반 정리 요청으로 선택이 확정됐다고 간주하지 않는다. |
| MatchFlowUI의 알림 팝업 프리팹 전제 기록 | ConfirmPopup 레이아웃이 실제 반영됐고 해당 스크립트가 복구 수단으로 재사용 가능하다고 명시. | 실행 근거는 있으나 재사용 가치도 있어 실행 완료만으로 폐기하지 않는다. |
| UnitCombatAssetMatrix 「신규·교체 유닛 프리팹 등록 체크리스트」 | SetupUnitVisualRoots.Apply는 기존 50개 Legacy 프리팹의 일회성 migration. 새 프리팹마다 일괄 Apply를 다시 실행하지 않는다. | 한 번 실행한 migration과 계속 쓰는 검증·복구 API를 분리해 판단한다. |

WORKFLOW의 「Setup 14개」는 2026-08-26의 과거 실측값이다. 현재 35파일 총수나 현재 Setup 파일 수를 대신하는 숫자로 옮겨 적지 않는다.

## 3. 실제 재실행 위험과 직접 의존

### 옛 Injector·Validator의 클립 선택

`CombatHitEventInjector.cs`와 `CombatHitEventValidator.cs`는 controller의 animationClips에서 이름에 Attack이 들어간 첫 클립을 고른다. 현재 `UnitFactory.cs`는 `attackTimelineClip`의 명시적 연결을 우선한다. 따라서 옛 도구가 보는 클립과 실제 생산 클립이 같다고 보장할 수 없다.

Injector는 클립에 이벤트가 없으면 config의 타격 시점을 넣고 저장하는 에셋 writer다. 이미 완료된 공격 시각 교정을 이 도구 재실행으로 다시 손대지 않는다. Validator는 읽기 전용이지만 같은 옛 선택 기준이라 정상 판정도 생산 경로 전체의 증거가 되지 않는다. 이번에는 분류·격리와 향후 처리 경계를 정하고, 공격 밸런싱·타이밍 교정 작업을 재개하지 않는다.

Spawner는 Play Mode에서 실제 유닛 생성 경로를 사용한다. 저장 API가 검색되지 않아도 실행 중 게임 상태를 바꾸므로 읽기 전용 검사와 구분한다. 헤더의 「일회성」을 수명 결론으로 사용하지 않는다.

### 일회성 이름이지만 남겨야 하는 API

- `RunUnitActionSelfValidation.cs:1815`는 `RetimingBloomFairyHealPresentation.RetimeCurve`를 직접 호출한다. Retiming 메뉴가 한 번 끝났더라도 파일 전체를 지우면 회귀 검증의 의존을 끊는다. 실행 메뉴와 순수 변환 helper를 분리하려면 별도 명시적 변경과 검증이 먼저다.
- VisualRoot 및 NetworkTransform 설정 도구는 rollback/isolation 검사에서 직접 사용된다. 설정 도구와 복구 검사를 묶어서 보존·이관해야 한다.
- ResultPresentationMode에는 Apply뿐 아니라 Dry Run·Validate·Self Validate 경로가 있다. 현재 모드가 설정됐다는 사실만으로 전체 도구를 폐기하지 않는다.
- `RunUnitActionSelfValidation.cs`와 `.VfxConnections.cs`는 partial로 결합된 논리적인 도구다. 대형 파일이라는 이유로 이번 분류 작업에 내부 리팩터링을 끼워 넣지 않는다.

## 4. 실행 기록과 삭제 판단의 경계

| 후보 | 확인한 근거 | 현재 판정 |
|---|---|---|
| SetupConfirmPopupAlertLayout | 기존 post-game-leave-ui Plan 「사용자 판단 대기」와 MatchFlowUI에 실제 프리팹 반영 및 멱등 복구 가치가 함께 기록됨 | 실행 근거 있음. 삭제 승인 아님. Tools 보존 권고를 사용자에게 제시. |
| SetupUnitVisualRoots의 일괄 Apply | Matrix가 기존 프리팹 migration 목적과 신규 프리팹 반복 실행 금지를 명시 | 일회성 실행부는 정리 검토 가능하나 rollback/API 의존이 있어 파일 전체 삭제 보류. |
| RetimingBloomFairyHealPresentation | Bloom Task에 에셋 변경 계획·컴파일 경계가 기록됐으며 현재 회귀 코드가 순수 helper를 사용 | 실행·클립 수용과 API 폐기를 구별. helper 분리 없이 전체 삭제 불가. |
| CreateSpecialAttackConfigAsset | 구현검증이력에 SO 생성·씬 배선 도구와 BattleAxe 구현·사용자 테스트 기록 존재 | 과거 실행 근거는 있으나 현행 재생성 필요성·의존과 폐기 결정을 추가 확인해야 함. |
| 나머지 생성·등록·배선 Setup | 파일과 메뉴가 존재한다는 사실만으로 특정 실행·현재 에셋 반영·사용자 완료 확인을 증명하지 못함 | 실행 완료와 재사용 필요성 미입증. 삭제 후보 분류만으로 실행하거나 삭제하지 않음. |

생성된 에셋이 존재하거나 기능이 완료됐다는 기록은 해당 스크립트의 정확한 버전이 실행됐다는 증거와 다르다. 과거 Task의 「예정」·「컴파일 PASS」를 Unity 메뉴 실행 PASS로 바꾸지 않는다. 어떤 파일도 이번 조사에서 즉시 삭제 가능한 확정 대상으로 판정하지 않았다.

## 5. 안전·보존 조건

- `.claude/mistakes.md` 2026-09-21 사건은 Plan에서 비활성화를 약속하고 곧바로 삭제한 실패다. 원래 코드를 되살릴 수 있어야 비활성화이며 설명만 남기는 것은 삭제다.
- 2026-10-07 사건은 검증 메뉴 대신 MistShrine setup이 실행돼 Game이 자동 저장된 사례다. 정확한 메뉴 identity 확인, 실제 terminal 동작 확인, 씬 저장 여부와 검증 성공을 별도로 확인해야 한다.
- 정리 확인을 위해 Setup·Apply·Register·Wire·Retiming 메뉴를 실행하지 않는다. 실행 목적은 파일 이동·메뉴 연결 검증과 구별한다.
- `.cs.meta`와 GUID, Editor 컴파일 영역, namespace·partial·다른 에디터 도구의 직접 호출, 문자열 경로와 문서의 현재 참조를 함께 확인한다.
- 기존 Task·Log와 날짜별 원문은 그대로 두고 현재 안내만 적절한 상시 문서에 반영한다. 이번 단계의 상시 문서·메모리·기준값 변경은 없다.

구체적인 파일별 권고와 승인 후 순서는 [Plan.md](Plan.md)에 둔다.

## 6. 기술 감사와 조사 단계 마무리

game-programmer가 35파일의 소스·메뉴·쓰기 동작·주요 직접 참조를 감사하고 `.utmp/editor-script-organization/audit.md`·`audit.json`에 근거를 저장했다. 두 partial이 하나로 결합돼 논리 도구는 34개다. 본 조사는 대형 회귀 파일 전체의 의미 정확성을 새로 검증한 작업이 아니다. 현재 고유 asmdef/asmref는 없으며 namespace와 public batch 진입점은 유지한다.

기술 감사의 추가 위험은 다음과 같다.

- rollback/isolation 이름의 검사도 실제 프리팹을 변경한 뒤 복구하는 writer일 수 있다. 읽기 전용 검증이라고 실행하지 않는다.
- SkillSetup_CreateDataAssets는 기존 placeholder 필드와 loadout 배열을 덮어쓸 수 있다. 단순 누락 생성기나 안전한 멱등 복구라고 간주하지 않는다.
- Runtime 경로에 있지만 에디터 가드로 보호되는 Forced 계열 클래스는 런타임 코드가 에디터 빌드에서 참조하므로 이번 Editor 폴더 정리와 함께 옮기지 않는다.
- MenuItem이 없는 TorrentWaveProductionValidation과 rollback batch에도 직접 호출·외부 진입점이 있다. 메뉴 부재·정적 호출 검색 부재만으로 폐기하지 않는다.

최종 권고는 분산된 14파일을 역할별로 이관하고, ConfirmPopup 복구 도구 1파일은 Tools 보존 선택을 제안해 합계 15파일과 대응 메타를 이동하는 것이다. 나머지 20파일은 현 위치에서 유지한다. Injector도 위치만 옮기며 메뉴 비활성화·삭제는 별도 제안이다. 즉시 삭제가 확정된 파일은 없다. 이 권고안에 대한 사용자의 승인은 명시된 이동과 ConfirmPopup 보존 선택의 승인으로 취급하며 같은 결정을 반복해서 묻지 않는다.

Task 작성 후 Python을 `-X utf8 -B`로 지정해 `Tools/check_docs.py`를 실행한 결과 exit 0·문제 0건이었다. 참조 검색은 90문서로 유지됐고 새 Task는 제외됐다. 이는 조사 단계의 기계적 정합성 결과이며 새 Task의 모든 사실 주장이나 Unity 동작 검증을 대신하지 않는다. 이후 받은 Plan 구현 승인과 실제 변경은 다음 절에 구분한다.

## 7. 구현 승인과 결과 확인 경계

사용자가 Plan 구현을 승인하여 경로 정리와 관련 현재 문서의 동기화를 진행한다. ConfirmPopup을 Tools/UI에 보존하는 선택도 승인 범위에 포함되며 재질문하지 않는다. Injector는 위치만 이동하고 기존 메뉴·동작을 유지한다. 메뉴 비활성화·검사 알고리즘 교정·helper 분리·최종 삭제는 이번 구현에 포함하지 않는다.

메인 병합은 구현 결과를 사용자에게 보고하고 사용자가 확인한 뒤 진행한다. 현재 branch는 `codex/editor-script-organization`이며 primary main 경로에서는 수정하지 않는다. 확정 이동과 정적 검증은 다음 절에 기록한다. Unity·Inspector·Setup/Apply 메뉴 실행은 하지 않았다.

## 8. 승인된 경로 이동과 정적 검증 결과

코드 담당자의 `move-before.json`·`move-after.json`을 직접 읽고, mapping 15행과 전체 파일 검사 35행을 재집계했다. 각 파일의 모든 검사 불리언이 참임을 확인했다. 아래 해시·GUID 일치와 보호 파일 수는 해당 보고서의 측정 결과이며 문서 담당자가 파일 해시 전체를 새로 계산한 독립 감사는 아니다.

| 이전 경로 | 현재 경로 |
|---|---|
| `Assets/_Project/Scripts/Editor/AndroidBuildAssetOptimizer.cs` | `Assets/Editor/Tools/Build/AndroidBuildAssetOptimizer.cs` |
| `Assets/_Project/Scripts/Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs` | `Assets/Editor/Tools/Combat/RunAuthoritativeAttackPresentationValidation.cs` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs` | `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitNetworkTransformInterpolationRollbackIsolation.cs` | `Assets/Editor/Tools/Combat/Migration/RunUnitNetworkTransformInterpolationRollbackIsolation.cs` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitRootPoseCrossAudit.cs` | `Assets/Editor/Tools/Combat/RunUnitRootPoseCrossAudit.cs` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitVisualRootRollbackIsolation.cs` | `Assets/Editor/Tools/Combat/Migration/RunUnitVisualRootRollbackIsolation.cs` |
| `Assets/_Project/Scripts/Editor/Combat/SetupResultPresentationMode.cs` | `Assets/Editor/Tools/Combat/SetupResultPresentationMode.cs` |
| `Assets/_Project/Scripts/Editor/Combat/SetupUnitNetworkTransformInterpolation.cs` | `Assets/Editor/Tools/Combat/Migration/SetupUnitNetworkTransformInterpolation.cs` |
| `Assets/_Project/Scripts/Editor/Combat/SetupUnitVisualRoots.cs` | `Assets/Editor/Tools/Combat/Migration/SetupUnitVisualRoots.cs` |
| `Assets/_Project/Scripts/Editor/Combat/ValidateUnitCombatSetup.cs` | `Assets/Editor/Tools/Combat/ValidateUnitCombatSetup.cs` |
| `Assets/_Project/Scripts/Editor/CombatHitEventInjector.cs` | `Assets/Editor/Setup/Legacy/CombatHitEventInjector.cs` |
| `Assets/_Project/Scripts/Editor/CombatHitEventValidator.cs` | `Assets/Editor/Tools/Combat/CombatHitEventValidator.cs` |
| `Assets/_Project/Scripts/Editor/SpiritAttackVfxTestSpawner.cs` | `Assets/Editor/Debug/VFX/SpiritAttackVfxTestSpawner.cs` |
| `Assets/Editor/Setup/SetupConfirmPopupAlertLayout.cs` | `Assets/Editor/Tools/UI/SetupConfirmPopupAlertLayout.cs` |

| 정적 확인 항목 | 결과 |
|---|---|
| 이동한 C# / 대응 기존 메타 | 15 / 15, 원본 바이트 보존 |
| 현 위치 유지한 C# / 전체 자체 관리 C# | 20 / 35 |
| 전체 35파일의 source·meta SHA / GUID / namespace / public API / 메뉴·Editor 컴파일 경로 검사 | 보고서상 전부 일치 |
| MenuItem 선언 | 57개, 원래 선언 보존 |
| 보호 파일 비교 | 2400파일, 변경 0 |
| 기존 Editor·Combat 폴더 메타 | 변경 0, 보존 |
| 신규 목적지 폴더 메타 | 6개 생성 |
| 자체 C#의 옛 절대 스크립트 경로 | 잔존 0 |

신규 폴더 메타는 Tools의 Build·Combat·Combat/Migration·UI, Debug/VFX, Setup/Legacy에 대응한다. 기존 source와 meta를 함께 옮겼고 클래스·메뉴·기능은 바꾸지 않았다. Injector 메뉴는 여전히 실행 가능하며 Validator의 옛 선택기와 Retiming helper도 그대로다. 이번 결과를 메뉴 안전화·검사 알고리즘 개선으로 표현하지 않는다.

`WORKFLOW.md` [5-2]의 상시 도구·복구 수명과 옛 이동 보류표를 현재 위치로 맞췄다. ROADMAP의 ConfirmPopup 선택은 사용자가 승인한 Tools 보존 결정으로 제자리 갱신했고, MatchFlowUI의 과거 실행 원문은 유지하며 현재 위치·선택 해결 안내만 추가했다. UnitCombatAssetMatrix는 현행 gate 소스 경로만 교정했다. AGENTS와 PROJECT_STATUS·WORK_HISTORY·TDD·UIGuidelines·AABSizeOptimization도 관련성을 검토했으며 이번 내부 도구 배치에 새 색인이나 제품 기능 완료 기록이 필요하지 않아 수정하지 않았다.

**미실행:** Unity import·컴파일, Inspector·Setup/Apply·MenuItem 실행, native·게임 회귀 및 main 병합. 사용자의 결과 확인을 기다리며 기존 게임 동작 수용을 새로 열거나 전체 검증 완료로 확대하지 않는다.

## 9. 2026-10-16 재개 후 문서 점검

이전 중단 상태를 현재 실패로 간주하지 않고 독립 worktree와 브랜치, 실제 목적지 및 저장된 이동 보고서를 다시 확인했다. 15파일 이동·20파일 유지 등 §8의 수치는 코드 담당자 보고서를 직접 읽어 확인한 값이며 문서 담당자의 새 전체 해시 계산 결과는 아니다.

ROADMAP의 ConfirmPopup 상태를 「결정·이관 완료·사용자 결과 확인 대기」로 고쳤다. Plan의 보존 결정도 현재 승인·이동 완료 상태로 맞췄다. WORKFLOW의 현재 도구 위치·수명, Matrix의 현행 gate 경로, MatchFlowUI의 날짜별 원문과 별도 현재 위치 안내가 유지됨을 확인했다.

문서 담당자가 Python을 `-X utf8 -B`로 지정해 검사기를 새로 단독 실행한 결과 **exit 0·문제 0건**이었다. 참조 문서는 90개, 규칙 문서는 탐색/정의 해석 19/16, 규칙 참조/명명 ID 725/34, 링크/앵커 1783/1080이다. 현재 Task와 기존 Task·Log 제외는 유지된다. 이 결과는 문서의 기계적 정합성이며 Unity 컴파일·메뉴 실행 증거가 아니다.

### 메인 세션의 독립 검증

`.utmp/editor-script-organization/main-independent-review.json`을 직접 읽어 `pass: true`, `issues: []`와 다음 결과를 확인했다. 아래 비교의 실행 주체는 메인 세션이며 문서 담당자가 새 해시 감사를 실행한 것은 아니다.

- Git 기준 `ff0ea9e92b63116e7fe46e825c69e5653022ae85`와 자체 C# 35파일·대응 기존 메타 35파일을 대조했다. 이동 15쌍, 현 위치 유지 소스 20개, 전체 C# 35개와 MenuItem 선언 57개를 확인했다.
- **전체 35개 소스와 기존 메타의 이동 전 worktree 원시 바이트 SHA 70개가 모두 동일**했다. Git blob과 체크아웃의 비교에서는 70파일의 LF/CRLF 줄끝 차이만 정규화하여 내용 동일성을 확인했다. 처음의 원시 Git blob 대조가 줄끝 차이로 거짓 실패한 것이며, 이를 맞추려고 실제 파일·코드·메타를 수정한 것이 아니다.
- 보호 파일 2400개에 변경이 없고 신규 목적지 폴더 메타는 6개였다. 신규 폴더 메타 형식·프로젝트 내 GUID 중복 0 및 동일한 정적 Editor 컴파일 영역은 메인 세션의 추가 확인 전달값이다.
- primary는 main 브랜치·동일 기준 HEAD이며 tracked 변경 0임을 메인 세션이 확인했다. 독립 worktree의 이동·문서 변경을 primary 작업 변경으로 섞지 않았다.

Unity import·컴파일과 Setup/Apply 실행은 하지 않았다. 이 검증은 소스·메타 보존과 정적 이동 정합성 확인이며 새 native·게임 동작 PASS가 아니다. **사용자 결과 확인과 main 병합은 아직 대기**다. 이 결과 기록은 Task 제외 범위의 수정이므로 상시 문서 검사를 다시 반복하지 않았다.

사용자의 세 작업 통합 요청에 따라 문서 정리와 검사기 정비 이력을 이미 포함하는 `codex/editor-script-organization`을 통합 결과 브랜치로 사용한다. 기존 main 기준 `ff0ea9e9`에 두 선행 작업이 포함되어 있으므로 별도로 중복 적용하지 않는다. 현재 에디터 변경은 이 브랜치에 커밋할 준비를 마쳤으며, 이 기록 시점에는 커밋 전이다. main 병합은 사용자 결과 확인 뒤 진행한다. 기존 검증 수치와 실행 범위는 유지한다.

## 10. 사용자 의도 정정 — 불필요 코드 삭제와 Unity 메뉴 정리

**대체된 제안 — 구현하지 않음:** 이 절의 17개 퇴역·19파일·33메뉴 및 넓은 기존 도구 보존은 당시 제안이다. 최신 사용자 기준으로 대체됐으므로 현행 유지·삭제 목록이나 목표 수로 사용하지 않는다.

사용자는 필요 없는 스크립트를 삭제하고 필요한 것만 남겨 Unity 메뉴에서 역할별로 사용하려는 의도임을 명확히 했다. 경로만 이동하고 모든 기존 코드·메뉴를 보존한 1차 작업은 이 목표를 충족하지 않았다. 1차 변경은 `6d83955a`에 보존돼 있으며 새 단계에서는 당시 남긴 35파일과 MenuItem 57개 선언을 기준으로 폐기·유지·순수 helper 보존을 다시 판정한다. 새 코드 변경은 아직 없다.

### 현재 필요성과 대체 수단으로 재판정

- 폐기 판단은 현재 생성·배선된 에셋, 런타임의 직접 의존, 다른 Editor 검사기의 호출, 재사용 필요와 대체 작업 방법을 근거로 한다. 정확히 어느 버전의 옛 setup 메뉴가 실행됐는지를 모든 삭제의 필수 조건으로 두지 않는다.
- 기존 데이터나 프리팹을 다시 생성할 의도가 없고 현행 수동 authoring·생산 binding 검사로 역할이 대체되는 생성기는 폐기 후보로 판정할 수 있다. 반대로 public 순수 helper나 현재 복구 경로를 다른 도구가 사용하면 그 기능을 남기거나 먼저 분리한다.
- 「옛 메뉴 유지」는 1차의 바이트 보존 계약이었다. 추가 단계에서는 삭제 메뉴를 제거하고 남길 메뉴를 역할별로 재분류한다. 폴더 격리만으로 불필요 코드 삭제·메뉴 정리가 완료됐다고 보고하지 않는다.
- WORKFLOW [4]의 검증 전 원본 비활성화·사용자 테스트 후 최종 삭제 순서를 따른다. 사용자에게 구체 목록·근거·변경 메뉴를 먼저 공유하고 구현 승인을 받으며, 승인 목록의 테스트가 완료되면 같은 목록을 다시 승인받지 않는다.

### 관련 현재 문서의 후속 영향

WORKFLOW는 Setup/Tools 수명과 도구 목록, Matrix는 현재 gate 경로·사용 절차, MatchFlowUI·ROADMAP은 ConfirmPopup 도구의 필요성 판단에 영향을 받는다. Units에는 SpecialAttackConfig 생성·배선 메뉴 및 옛 Injector 사용 안내가 남아 있으므로 해당 도구 폐기 시 현재 대체 방법의 안내가 필요하다. 이 문서의 과거 수치·날짜별 구현 경위는 도구 정리를 이유로 재설계하지 않는다. 현재는 추가 Plan만 작성하며 상시 문서를 다시 수정하지 않는다.

game-programmer의 `audit-retirement.md`·`.json`을 읽어 35개 현재 파일의 판정과 57개 메뉴의 old→new/drop 대응을 확인했다. JSON에서 폐기 또는 helper 분리 후 폐기인 파일이 17개, 새 메뉴 경로가 남는 선언이 33개임을 직접 재집계했다. 실제 소스 변경·실행이 아닌 제안이다. 구체 파일·메뉴 대응은 Plan §8에 담았다.

최종 권고는 현재 Setup 16파일과 옛 Validator 1파일을 퇴역하고, Retiming의 순수 helper를 `Assets/Editor/Tools/Combat/BloomHealRetimingValidationUtility.cs`로 보존해 C# 실물을 최종 19파일로 줄이는 것이다. 비활성 검증 중간 단계는 기존 35파일 원문과 새 helper를 모두 보존한 실물 36파일·컴파일 활성 19파일이다. 기존 에셋·데이터·런타임 기능을 지우는 작업이 아니다.

메뉴는 퇴역 도구의 17개 선언과 중복·과거 일괄 실행 진입점 7개를 빼서 활성 선언 57→33을 목표로 한다. 추가 7개는 Authoritative 검증의 중복 메뉴, VisualRoot와 Interpolation의 Dry Run·Apply 네 메뉴, 과거 25번째 변경 조건의 Network rollback 메뉴, ResultMode의 중복 Dry Run이다. 필요한 public API와 journal 복구 기능은 유지한다. 실제 Unity 가시 메뉴 수는 아직 관찰하지 않았다.

현재 자산의 UI·스킬·건물·생산 등록 배선 및 50개 유닛 프리팹의 VisualRoot/PositionLerpSmoothing 상태는 코드 담당자가 읽기 전용으로 수집한 근거다. 문서 담당자가 새 native 동작 검증을 한 것이 아니다. Profile의 별도 선택적 미연결 필드를 UI 오류로 단정하거나 이번에 배선하지 않는다. 옛 Validator의 config 비교표 전체와 현행 production gate가 모든 기능에서 동등하다고 주장하지 않고, 그 오래된 비교표 자체를 퇴역하는 것으로 명시한다.

Spawner의 실제 생성과 raw VFX fallback·PreviewDriver는 고유한 시각 실험 용도여서 기존 기능을 유지한다. 메뉴 분류만 바꾸며 fallback을 권위 타격 검증으로 소개하지 않는다. 1차의 「모든 메뉴 보존」과 「삭제 확정 0」은 당시 범위였으며 추가 Plan의 결론을 대신하지 않는다.

현재 추가 단계에서는 Research/Plan과 아래 필수 실수 기록만 수정했다. 상시 규칙 문서와 코드·메뉴·에셋은 변경하지 않았고 Unity 실행이나 main 병합도 하지 않았다. 승인 후 비활성화·검증·사용자 테스트와 확정 목록 최종 삭제를 순서대로 진행한다.
이 목표 누락은 WORKFLOW의 실수 발견 시 기록 의무에 따라 `.claude/mistakes.md`의 목차와 말미에 2026-10-16 사건으로 추가했다. 기존 사건·메모리·기준값은 수정하지 않았다. 따라서 추가 단계의 현재 문서 변경은 Task 두 파일과 이 사건 기록 하나이며 코드 변경은 여전히 없다.

추가 Plan과 사건 기록 작성 뒤 검사기를 `-X utf8 -B`로 단독 실행해 exit 0·문제 0건을 확인했고 `git diff --check`에서도 공백 오류가 없었다. 검사의 참조 대상은 90문서로 유지되며 Task는 제외된다. 이 0건은 계획 본문의 사실 주장·폐기 적절성이나 Unity 동작을 보증하는 결과가 아니다. 추가 구현 승인과 사용자 테스트·결과 확인 전에는 코드 변경·최종 삭제·main 병합을 완료로 표기하지 않는다.

## 11. 최신 사용자 결정 — 필수 진입점과 실제 내부 의존만 유지

사용자는 AndroidBuildAssetOptimizer가 나중에 필요하면 그때 상황에 맞춰 다시 작성하면 되고 다른 스크립트도 마찬가지라고 명확히 했다. 현재 도구가 쌓여 혼란스러운 것이 정리 이유이며, 로그 저장·네트워크 강제 실패와 전투 교정에서 주로 쓰는 두 진입점 외에는 현재 정리한다. Android 보고서 기능도 남기는 쪽으로 해석하지 않는다.

유지 기준은 `LogcatCapture`, `ForceRematchMapFailureMenu`, `RunUnitActionSelfValidation`, `RunUnitRootPoseCrossAudit`다. 코드 담당자의 실제 의존 확인 결과 VfxConnections partial·Authoritative 검증·TorrentProduction 검증을 내부 보조 기능으로 남겨 기존 7파일을 유지한다. Bloom의 `RetimeCurve`·`ValidateSamples`·`Finite`·`Check`는 기존 Action 검사기 안의 private helper로 옮기며 새 파일이나 단독 메뉴는 추가하지 않는다.

MapFallbackTemplateBuilder, migration·rollback 묶음, ValidateUnitCombatSetup, SetupResultPresentationMode, ConfirmPopup 복구 도구, Spawner, TorrentWaveCalibrationTool도 필수 검사의 실제 의존이 없으면 제거한다. 기존 폴백 bytes·참조 원본과 runtime MapFallbackTemplateFactory/Domain 생성 로직, 실제 UI·유닛·VFX 자산은 보존한다. 기준은 현재 필요성과 사용자 선택이며, 옛 Tools 영구 보존 문구를 이유로 이미 결정한 정리를 다시 보류하지 않는다.

구현 시 WORKFLOW [5-2], RandomMap 규칙 12의 제작 도구 삭제 금지 안내, TDD 네트워크의 폴백 제작 도구 영구 보존 안내와 관련 현재 도구 포인터를 함께 정정할 계획이다. 지금은 이 Task 두 문서만 갱신하며 다른 규칙 문서는 수정하지 않는다. 직전 제안의 35파일 기준·57메뉴는 당시 측정값으로 보존하고 최신 결과로 재사용하지 않는다.

최소 유지 기준의 결정과 코드 구현 승인을 구분한다. 실제 의존과 목록을 반영한 Plan 공유 후 승인받아 원본 비활성화·검증·사용자 테스트·확정 목록 최종 삭제를 진행한다. 구현 결과 확인 뒤 main 병합한다. 코드·메뉴·에셋 변경이나 Unity 실행은 아직 없다.

최소 제안은 기존 35파일에서 7파일 유지·28파일 퇴역이다. 이전 감사 목록의 현재 경로 35개에서 유지 이름 7개를 제외해 제거 목록 28개를 문서 담당자가 재집계해 Plan §9에 기록했다. 메뉴 목표는 코드 담당자 전달값으로 Logcat 3·강제 실패 11·Action 검사 1·Root Pose 로그 분석/자체 검증 2의 합계 17선언이다. 이전 19파일·33메뉴와 새 helper 파일 추가 제안은 적용하지 않는다. 이 수는 아직 구현·Unity 실행 결과가 아니다.

## 12. 최신 최소 유지 승인과 구현 단계

사용자가 Plan §9를 승인했고 28파일 원문 비활성화, 필요한 7파일과 검사 내부 helper 보존, 17개 활성 메뉴의 구현·정적 검증을 마쳤다. 위 계획 작성 시점의 「코드 변경 없음」은 당시 기록과 구별한다. 기존 검사회귀 조건은 유지하며 native PASS로 해석하지 않는다.

현재 문서도 사용자 결정에 맞춰 WORKFLOW의 Tools 영구 보존·migration 묶음, RandomMap 규칙 12와 TDD 네트워크의 템플릿 제작 도구 삭제 금지 안내, Matrix·Units·TDD의 옛 생성/주입/구조 검사 도구 포인터, ROADMAP·MatchFlowUI의 ConfirmPopup 보존 선택을 교정했다. 맵 bytes·runtime 폴백·현재 에셋과 게임 규칙 수치는 유지하며 날짜별 원문은 보존한다.

### 비활성 구현·정적 검증 결과 — 사용자 테스트 대기

코드 담당자의 최종 `.utmp/editor-script-organization/minimal-after.json`을 읽어 활성 C# 7개·원문 비활성 28개·활성 MenuItem 17선언과 고유 경로 17개를 확인했다. 물리 C#은 여전히 35개이며 최종 파일·메타 삭제는 하지 않았다. 최종 증거 파일 SHA256은 코드 담당자가 전달한 `8f972e43c7a85fb53ce57d0c08dde109dc50ac96b5cfcdd496673f6a5cfbba85`다.

- 기존 Action 검사기 안의 private `BloomHealRetimingValidation`에 `RetimeCurve`·`ValidateSamples`·`Finite`·`Check`를 보존했다. 원래 writer와 `SetStopTime`은 남길 기능에 포함하지 않는다.
- 메인 세션은 이동 전 원시 `source_bytes`를 기준으로 비활성 28개가 wrapper 외에는 개행 정규화 후 동일함을 독립 실행으로 확인했다. 활성 7개·고유 메뉴 17개를 직접 추출하고 helper 네 메서드의 들여쓰기 정규화 동일성, 보호 파일 3969개 해시 일치를 확인했다. 이는 메인 세션의 실행 결과를 전달받은 기록이며 문서 담당자가 같은 전수 감사를 다시 실행한 결과가 아니다.
- 코드 담당자는 퇴역 28타입의 남은 코드·문자열 참조 0, 전처리기 균형과 공백 검사를 확인했다. 마지막 강제 실패 사용법 주석 교정은 메뉴 경로 안내만 바꾸며 기능 변경이 없다.
- 메뉴는 `Hexiege/Combat/Run Full Regression Suite`, `Hexiege/Combat/Logs/Analyze Latest Root Pose`, `Hexiege/Combat/Diagnostics/Self Validate Root Pose Audit`의 전투 3개, `Hexiege/Logs`의 로그 3개, `Hexiege/Debug/Failure`의 강제 실패 11개다. Authoritative와 Torrent 검증은 내부 호출로 보존하며 별도 메뉴를 추가하지 않았다. 이는 활성 선언의 정적 집계이며 Unity에서 관찰한 가시 메뉴 수가 아니다.

현재 안내는 WORKFLOW·RandomMap·TDD 네트워크·Matrix·Units·TDD·ROADMAP·MatchFlowUI·LogRules의 직접 관련 부분만 동기화했다. 날짜별 원문·기존 수치·게임 계약은 보존한다. Unity import·컴파일·기기·메뉴 실행은 하지 않았으며 native PASS가 아니다. **작업 프로젝트는 `D:/Projects/Hexiege/Hexiege/.utmp/docs-checker-finalize`이고 현재 main 프로젝트에는 미반영이다.** 사용자 테스트 후 승인 목록 28개의 실제 삭제를 진행하고 결과 확인 뒤 main에 병합한다.

문서 담당자가 최종 상시 문서 수정 뒤 검사기를 단독 실행해 exit 0·문제 0건을 확인했다. 규칙 탐색/해석 19/16, 참조 대상 90문서, 참조 724+명명 ID 34, 링크 1784·앵커 1080이었다. `git diff --check`도 exit 0·공백 오류 없음이며 Task 두 파일의 LF→CRLF 안내만 있었다. Task는 문서 검사에서 제외되며 이 결과는 계획의 사실·Unity 컴파일·기기 동작을 보증하지 않는다.

### main 병합 확인

사용자 병합 승인 후 메인 세션이 기존 Unity 프로젝트에서 `main`을 `ff0ea9e9` → `f9dfb292`로 fast-forward 병합했고 tracked clean을 확인했다. 위 별도 폴더·main 미반영 안내는 이 병합 전 이력이다.
메인 세션이 primary에서 문서 검사 exit 0·문제 0건(참조 724+34, 링크 1784·앵커 1080)을 확인했다는 결과를 전달받았다. 푸시는 하지 않았다.
28파일은 여전히 원문 비활성 상태이며 실제 삭제·사용자 테스트 통과·Unity 컴파일·기기·메뉴 실행을 완료로 주장하지 않는다.

## 13. 명시적 최종 삭제 승인과 실제 삭제 결과

사용자가 「비활성화한거 삭제하고 메인에 병합해줘」라고 직접 승인해 이전 삭제 대기를 해소했다. 이는 사용자 테스트 PASS 보고가 아니며 위 비활성·삭제 미진행 기록은 당시 이력으로 보존한다.

코드 담당자의 최종 전달 결과는 C# 28개·대응 메타 28개·빈 폴더 메타 8개, 합계 64파일 삭제다. 유지 C# 7개와 메타 7개의 전후 바이트 변경은 없고 활성 메뉴 17선언을 보존했다. 삭제 타입 코드 참조·직렬화 GUID 참조는 0건, 보호 파일 4176개의 변경은 0건이다. 증거는 `.utmp/editor-script-finalize/deletion-before.json`, `deletion-actions.json`, `deletion-after.json`이며 사후 증거 SHA256 전달값은 `8defb802e767172b5fa50c5f2789c3ffdc2fac1cf0708a89933a6ca0b4aa0a58`다.

메인 세션은 Plan의 28개 삭제 목록에서 만든 64경로가 실제 Git 삭제 경로와 정확히 일치함을 별도로 실행 확인했다. 나머지 변경은 Markdown 5문서뿐이며 유지 소스·메타 14개는 기준 HEAD와 개행 정규화 후 일치하고 고유 메뉴 17개를 직접 추출했다. 이는 각각 코드 담당자·메인 세션의 실행 결과를 전달받은 기록이며 문서 담당자의 새 전수 감사가 아니다. Unity import·컴파일·기기·메뉴 실행과 푸시는 하지 않았다. 이후 메인 세션이 최종 삭제 커밋 `58614539`를 main에 fast-forward 병합 완료했다(`946ade38` → `58614539`).

최종 상시 문서 수정 후 문서 담당자가 문서 검사와 공백 검사를 각 한 번 실행해 모두 exit 0을 확인했다. 문서 문제 0건, 참조 대상 90문서·참조 724+34·링크 1784·앵커 1080이며 이 정적 검사를 Unity 실행 검증으로 해석하지 않는다.
