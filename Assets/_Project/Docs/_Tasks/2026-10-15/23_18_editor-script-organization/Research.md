# 에디터 스크립트 정리 — 조사

개발 중 한 번만 설정하려고 만든 스크립트와 계속 쓰는 검사·보정 도구를 구분한다. 파일을 찾기 쉽게 모으되, 이미 맞춰 놓은 씬과 에셋을 다시 설정하거나 필요한 복구·검증 수단을 지우지 않도록 사용처와 실행 근거를 먼저 확인한다.

**현재 상태: 승인된 경로 이동·정적 검증·문서 동기화 완료, 사용자 결과 확인 대기.** 사용자는 계획 실행을 승인했고 결과 확인 후 main에 병합하도록 지시했다. 승인 범위는 15개 C# 파일과 대응 메타의 경로 정리 및 관련 현재 문서 동기화이며, 코드 동작 변경·삭제·비활성화와 Unity 메뉴 실행은 포함하지 않는다. main 병합은 아직 진행하지 않았다.

## 1. 기준과 조사 범위

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
