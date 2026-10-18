이 작업은 게임의 주요 문서를 읽을 때 무엇이 현재 기준이고 무엇이 과거 기록인지 쉽게 알 수 있도록 정리하기 위한 것이다. 초기 조사에서 문서별 역할·중복·수치와 상태 충돌을 확인했고, 사용자 승인 후 병합·분할과 원문 보존을 실행했다. 앞부분은 조사 시점의 근거이며 뒤의 실행 절은 실제 변경·검증·남은 판정을 기록한다. 게임 동작은 바꾸지 않는다.

# 주요 문서 정리 조사

- 작성일: 2026-10-13, 작업 폴더 시간 19:03 KST(메인 세션 실측 전달값).
- 브랜치: `codex/docs-organization`(메인 세션 전달값, 이 작성에서는 Git 미실행).
- 사용자 승인: “잘 정리된거같으니 task 문서 작성하도록하자”.
- 초기 산출물: 이 Research와 [Plan.md](Plan.md). 이후 사용자 “작업 진행하자”로 주요 문서 정리 실행 승인(2026-10-13).
- 코드 비활성화·삭제 없음. 기존 Task/Log, 코드·씬·에셋·메모리는 변경하지 않는다. 승인 Plan에 따라 주요 문서·프로젝트 관리 문서는 정리한다.

## 1. 조사 범위와 방법

`rg --files Assets/_Project/Docs -g '*.md' -g '!**/_Tasks/**' -g '!**/_Logs/**'`로 조사 시점 파일 목록을 다시 확보했다. 이 범위의 Markdown은 42개다. 파일 목록과 제목 구조를 확인하고, 아래 중복·수치·경기 흐름 경계에 관련된 본문을 읽었다. 모든 문장의 구현 진위와 모든 모순을 전수 검수한 결과는 아니다.

운영 기준은 루트 AGENTS 인덱스, CLAUDE, [WORKFLOW](../../../WORKFLOW.md) [3]·[4]·[11] 및 「검수·판정 작업과 수치 보고의 절차」, 공용 메모리의 Agent Memory Management Rules A~E다. document-manager 정의·메모리 인덱스와 doc-conventions §53의 이관·참조 보존 사례를 함께 확인했다. `.claude/mistakes.md` 목차에서 소실·인계 수치·MigrationRequired 오독 유형을 확인했다.

기존 `_Tasks/`·`_Logs/`는 정리 대상에서 제외한다. 결정 근거를 확인할 때만 읽고 참조하며 소급 수정하지 않는다. `LogRules.md`는 현행 정책 문서, `LogHistory.md`는 정책의 과거 기록이므로 둘 모두 주요 문서 목록에는 포함된다.

## 2. 전체 문서 목록과 맡길 역할

아래 경로는 `Assets/_Project/Docs/` 기준이다. 현재 역할과 정리 후 목표 역할을 함께 기록한 목록이며, 역할 배정은 Plan 승인 전 제안이다.

| 문서 | 역할 및 정리 방향 |
|---|---|
| GameDesignDocument.md | 게임 목적·경험·시스템 개요. 수치 원본과 현재 구현 기록은 별도 문서 참조 |
| TechnicalDesignDocument.md | 공통 기술 구조·구현 방법. 상세 네트워크 기술 분할 후보 |
| StatsReference.md | 수치·단위·계산식·확정 여부. 실행 이력은 표 바깥 근거 링크 |
| UIGuidelines.md | 시각 스타일·레이아웃·애니메이션 기준. 필수 동작 중복은 UI 규칙 참조 |
| AuthSystemRules.md | 로그인·인증 계약. 네트워크 경기 규칙과 별개 유지 |
| GameSystemRules.md | 개별 규칙 문서별 상세 요약과 빠른 참조의 원본 |
| GameSystemRules/GameSystemRules_Map.md | 모든 도입 맵의 공정성·대칭·접근성 불변식 |
| GameSystemRules/GameSystemRules_RandomMap.md | 11×21 무작위 생성·검증·런타임 타일 규칙. 경기 운영 혼재 분리 후보 |
| GameSystemRules/GameSystemRules_Network.md | 역할 독립성·이탈 판정·승패 기록의 제품 동작 계약 |
| GameSystemRules/GameSystemRules_UI.md | 공통 UI·여러 패널·설정·경기 흐름 UI. 제목별 분할 후보 |
| GameSystemRules/GameSystemRules_Units.md | 이동·전투·특수 공격·피해 계약 |
| GameSystemRules/GameSystemRules_UnitCombatSynchronization.md | 행동 회차·권위·시간·타격 결과·표현 전달 계약 |
| GameSystemRules/GameSystemRules_Buildings.md | 랠리·철거·타워·MistShrine 동작 |
| GameSystemRules/GameSystemRules_Skills.md | 스킬 프레임워크·조준 UX·서버 권위. 구체 스킬 수치는 미확정 |
| GameSystemRules/GameSystemRules_Upgrade.md | 강화·전투 스케일·연구 운영·연구 패널 계약 |
| GameSystemRules/GameSystemRules_CanvasSortingOrder.md | Canvas 대역·씬 계층·추가 절차 |
| GameSystemRules/GameSystemRules_Sound.md | BGM·SFX·볼륨 정책과 사운드 구현 계약 |
| GameSystemRules/GameSystemRules_AI.md | 공통 AI·난이도·반응·가드·배치·구현 계약 |
| GameSystemRules/GameSystemRules_AI_Scenario_Human.md | Human 종족별 시나리오 A/B/C |
| GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md | Spirit 종족별 시나리오 A/B/C |
| GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md | Transcendence 종족별 시나리오 A/B/C |
| Assets/CommonAssetGuide.md | 에셋 제작 공통 컨셉·프롬프트 조건 |
| Assets/3DAssetCreationGuide.md | 3D 제작·팀 색상·변환·명명 기준 |
| Assets/UIAssetGuide.md | UI 제작·초상화·Sprite 임포트 기준 |
| Assets/VFXSFXGuide.md | VFX/SFX 제작·스타일·명명 기준 |
| Assets/AssetList.md | 3D/UI 에셋 보유·제작 상태 |
| Assets/VFXSFXList.md | VFX/SFX별 준비·연결 상태와 승인 사양 |
| Assets/UnitCombatAssetMatrix.md | 유닛별 전투 에셋·구현·검증 상태 감사표 |
| PROJECT_STATUS.md | 현재 구현·검증 상태 요약 |
| ROADMAP.md | 미완료 작업·우선순위·선행 조건 |
| WORK_HISTORY.md | 완료·주요 변경의 시간순 이력 및 근거 링크 |
| WORKFLOW.md | 작업 절차·역할별 문서 갱신·검사 기준 |
| LogRules.md | 현행 런타임·QA-Fix 로그·보관 정책 |
| LogHistory.md | 과거 정책·개정·검증의 원문 보존 |
| AABSizeOptimization.md | 용량 최적화 변경·검증 경계·롤백 기준 |
| UnusedAssetAudit.md | 미사용 에셋 조사 방법·판정 한계·후속 결정 |
| Skills/SKILLS_GUIDE.md | 스킬 사용 안내 |
| _Reference/MapTemplateSource/MapTemplateSource_FullyOpen.md | 완전개방형 템플릿 재생성 참조 |
| _Reference/MapTemplateSource/MapTemplateSource_ObstacleOpen.md | 장애물 개방형 템플릿 재생성 참조 |
| _Reference/MapTemplateSource/MapTemplateSource_Canyon.md | 협곡형 템플릿 재생성 참조 |
| _Reference/MapTemplateSource/MapTemplateSource_Outer.md | 외곽형 템플릿 재생성 참조 |
| _Reference/MapTemplateSource/MapTemplateSource_ThreeLane.md | 3갈래형 템플릿 재생성 참조 |

루트 `AGENTS.md`는 탐색 인덱스 보완 후보다. `CLAUDE.md`와 `.claude/MEMORY.md`는 준수 기준으로 읽되 이번 본문 정리 계획의 대량 재작성 대상에 포함하지 않는다. 에이전트 메모리 자체의 재편도 별도 범위다.

## 3. 개별 게임 규칙의 구조와 경계

15개 개별 규칙 파일의 제목 구조를 확인했다. 단순히 GameSystemRules 인덱스만 확인한 것은 아니다. 경계 판단에 필요한 본문을 추가로 읽었으나, 이 목록을 모든 규칙의 내용 검수 완료로 읽지 않는다.

| 경계 | 확인한 구조와 의미 |
|---|---|
| Map ↔ RandomMap | Map 규칙 1~5는 보편 공정성, RandomMap 1~4장은 구체 생성 사양과 검증. 제약과 검증은 서로 다른 책임이므로 같은 단어라는 이유로 병합하지 않음 |
| RandomMap ↔ Network | RandomMap 6장은 규칙 16(맵 전송·복구), 14(재경기 선택), 17~19(결과·전송·재로드 이탈). Network는 상위 역할 독립성·승패·인게임 이탈을 별도 소유 |
| UI ↔ 경기 흐름 | UI 「공통 UI 규칙」 안에 숫자 1~11, L-1~L-4, M-1~M-4, D-1~D-8이 함께 있음. 화면·버튼·타이머와 이탈 판정 책임을 나눌 수 있음 |
| UI ↔ Buildings | 생산·MistShrine·건물 배치 UI는 별도 H2. Buildings는 랠리·철거·타워·물안개 작동 계약. MistShrine 상태와 미확정 수치는 보존 필요 |
| UI ↔ Skills·Upgrade | Skills에 스킬 패널·모바일 조준 UX, Upgrade 규칙 13에 연구 패널 계약이 이미 있음. 기능과 밀착된 계약은 해당 원본에 유지하고 공통 패널 문서는 링크 |
| Units ↔ CombatSynchronization | Units는 이동·전투 판정, CombatSynchronization은 NET-* 권위·회차·시간·전달·표현·취소 계약. 분리 유지가 타당하며 중복 문장을 조항별 대조해야 함 |
| AI ↔ 종족 시나리오 | AI는 공통 실행 계약, 시나리오는 종족별 A/B/C 테이블. 각 시나리오 §2~§3의 공통 Phase/지연 설명이 반복되며 예비값 조건도 함께 존재 |
| Canvas·Sound | 특정 기술 운영 계약을 포함하지만 독립 탐색 목적이 있음. 짧아 보인다는 이유로 TDD에 통합하지 않음 |

Buildings 「MistShrine 물안개 힐 시스템」 서두는 특수 동작 건물이 2종 이상일 때 SpecialBuildings로 분리하도록 정한다. 현재 조사만으로 그 조건이 충족됐다고 확정하지 않으며, 즉시 분할 후보로 올리지 않는다.

## 4. 재확인한 중복·모순·정리 후보

행 번호는 이번 조사 시점의 위치다. 실행 때는 제목·주장으로 다시 찾아야 한다.

| ID | 위치와 관측 | 분류 / 필요한 판정 |
|---|---|---|
| D-01 | 동일한 `2026-10-10 최종 구현/native·빌드 시도 인계(Main)` 한 줄이 10파일에 있고, Select-String 수집 결과 서로 다른 본문은 1개 | 완전 중복. 근거를 한 곳에 보존하고 각 문서의 역할에 필요한 링크만 유지 |
| C-01 | GDD 152행 생산표·371행 역할 설명의 Sniper 생산 15초 ↔ StatsReference 124행 20초 | 현행 수치 충돌. Stats 원본 선언·사용자 결정 근거 확인 후 GDD 사본 정리. 코드값 변경 승인 아님 |
| C-02 | GDD 372·378·384행 등에 스탯 미확정 유닛 목록, StatsReference에는 해당 유닛 수치가 존재 | 충돌 후보. 수치 존재만으로 밸런스 확정이라 판정하지 않고 확정/임시/구현값 구분 |
| C-03 | GameSystemRules 인덱스 UI 요약은 M-3/M-4 미정이라 설명하지만 RandomMap 규칙 16 근처에 2026-09-16 미정 해소 추가 블록이 있음 | 현행 상태와 과거 문장 혼재 후보. UI 원본 최신 결정부터 판정, 아직 확정값 목록 미작성 |
| S-01 | PROJECT_STATUS 첫 H2 670행, ROADMAP 첫 H2 427행 | 현재 정보 앞에 긴 인계·검증 블록 누적. 현황 표와 날짜 기록의 분리 후보 |
| F-01 | TDD 1104·1133행 제목 모두 `### 9.` | 문서 제목 번호 중복. 게임 규칙 번호와 구분하여 수정 후보 |
| F-02 | StatsReference 「공격 쿨다운 컬럼 표기」는 초와 초:프레임 혼재를 스스로 경고. Sniper 행 직후 정정 블록이 표를 끊음 | 형식·단위 문제. 일괄 숫자 변환 금지, 타격/주기/클립 길이 분리 후보 |
| S-02 | UnitCombatAssetMatrix와 공용 메모리에 MigrationRequired가 개별 미테스트 의미가 아니라고 명시 | 상태 축 혼동 방지. 이미 수용된 유닛을 미테스트로 되돌리지 않음 |
| N-01 | _Reference/MapTemplateSource 파일 5개는 현재 AGENTS 문서 표에 별도 안내 없음 | 탐색 보완 후보. 템플릿 내용 감사 완료 아님 |
| D-02 | 종족 시나리오 §2~§3에 실행 순서·지연·Phase 타이밍 반복 | 의미 중복 후보. 채굴소 이름 등 종족별 차이와 예비값 경고는 유지 |

D-01 위치: GDD:7, TDD:3, Units:3, Buildings:181, VFXSFXGuide:3, VFXSFXList:11, UnitCombatAssetMatrix:59, PROJECT_STATUS:57, ROADMAP:57, WORK_HISTORY:93. 이는 하나의 같은 인계 문단이 있는 10자리이며 서로 다른 문제가 10종이라는 뜻은 아니다.

## 5. 근거 보존과 조사 한계

- 현행 상태 도장은 근거 확인 후 그 줄을 고친다. 과거 날짜 기록 내부의 정정은 원문과 정정 경위를 함께 보존한다(공용 메모리 B-5/B-7, WORKFLOW [11]).
- 이관 전후 제목·조항·예외·취소선·미확정·검증 한계의 대응표가 필요하다. “문서를 짧게 만든다”는 이유로 유일 정보를 없애지 않는다.
- delivery/text 성공은 timing/pixel·NGO 원인 해소나 전체 migration 완료의 근거가 아니다. 개인 수용과 구조 전환은 별도 상태다.
- 코드 주석의 기존 UI M-3/M-4 및 RandomMap 17/18 참조는 메인 세션이 GameEvents와 GameBootstrapper.Map에서 확인했다고 전달했다. 이 작성은 해당 코드 전수 재측정을 수행하지 않았으므로 전달 근거로 기록한다.
- 전체 현행 상태, 모든 밸런스 수치, 기술 설계와 실제 코드 일치 여부, 모든 기존 링크 목적지·앵커, 이력별 유일 정보 목록은 아직 확정하지 않았다. 상세 조사는 실행 첫 단계에 둔다.

## 6. 문서 검사 상태

초기 조사 시 `python3 Tools/check_docs.py`는 WindowsApps 실행 별칭 실패로 미실행이었다. 실행 단계에서 기존 bundled Python `C:/Users/rmsep/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe -X utf8 Tools/check_docs.py`를 찾았고, 메인의 초기 baseline·중간·전체 문서 고정 후 최종 검사 모두 exit0·문제 없음이었다. 임의 설치·baseline 변경은 하지 않았다. 최종 검증 범위와 결과는 §10에 기록한다.

검사기는 `_Tasks/`·`_Logs/`를 제외하므로 실행 가능해져도 그 0건이 이번 Research/Plan의 링크·사실·계획 품질 검증을 뜻하지 않는다. 새 Task의 상대 링크와 두 파일만 생성했는지는 별도로 확인한다.

## 7. 승인 후 실제 정리와 원문 보존

사용자 “작업 진행하자”(2026-10-13)로 Plan 실행이 승인됐다. Task/Log의 기존 파일은 변경하지 않고 이번 Research/Plan만 실행 상태를 갱신했다. 코드·씬·에셋·메모리·검사기·Git 변경, Unity 실행, Testcase 생성은 수행하지 않았다.

### 이력 이관 대응표

| 원본 범위 | 실제 목적지 | 보존 방법 |
|---|---|---|
| GDD H1 다음~확정 VFX 사양 직전 | [gdd-handoff](../../../History/ImplementationVerificationHistory.md#gdd-handoff) | 당시 문단의 원문순서·SHA256·문단별 연결 |
| Stats H1 다음~사람이 읽는 스탯 미러 선언 직전 | [stats-handoff](../../../History/ImplementationVerificationHistory.md#stats-handoff) | Torrent 표현 교정 상태와 최종수정일 포함 |
| TDD H1 다음~버전 직전 | [tdd-handoff](../../../History/ImplementationVerificationHistory.md#tdd-handoff) | 담당자가 제공한 exact snapshot 비교 |
| PROJECT_STATUS 첫 전체 구현 현황 이전 | [status-handoff](../../../History/ImplementationVerificationHistory.md#status-handoff) | 날짜별 인계 원문; 현행 요약을 별도 서두에 배치 |
| PROJECT_STATUS P0 단계·맵·완료 시스템 상세 | [ActionSequence](../../../History/ImplementationVerificationHistory.md#status-action-sequence), [맵](../../../History/ImplementationVerificationHistory.md#status-random-map), `status-system-1`~`status-system-57` | 단계·시스템별 전체 원문 먼저 보존; 기존 H4 경로 유지 |
| ROADMAP 우선순위 요약 이전·완료 9행 | [roadmap-handoff](../../../History/ImplementationVerificationHistory.md#roadmap-handoff), [완료 행](../../../History/ImplementationVerificationHistory.md#roadmap-completed-items) | 완료 행은 TODO에서 제외; 남은 통합 회귀가 있는 집중 PASS 행 유지 |
| WORK_HISTORY 마일스톤 이전 인계·긴 표 134행 | [work-history-handoff](../../../History/ImplementationVerificationHistory.md#work-history-handoff), `milestone-<원문SHA앞16자리>` | 기존 H3 anchor 유지; 표에는 요약·상세 링크, 원문 행은 별도 보존 |
| VFXSFXGuide 상단·타워 당시 연결 계약 | [vfx-guide-handoff](../../../History/ImplementationVerificationHistory.md#vfx-guide-handoff), [타워 계약](../../../History/ImplementationVerificationHistory.md#vfx-guide-tower-contract-history) | HumanTank 당시 사양을 보존 후 현행 Assault 참조로 정정 |
| VFXSFXList 상단·source freeze·진행 단계 | [vfx-list-handoff](../../../History/ImplementationVerificationHistory.md#vfx-list-handoff), [source freeze](../../../History/ImplementationVerificationHistory.md#vfx-list-source-freeze), [계약](../../../History/ImplementationVerificationHistory.md#vfx-list-contract-history) | 기존 H3 유지, 현재 준비·연결·시각 후속을 분리 |
| Units·CombatSynchronization 상단 반복 인계 | [units-handoff](../../../History/ImplementationVerificationHistory.md#units-handoff), [combat-handoff](../../../History/ImplementationVerificationHistory.md#combat-handoff) | 규칙·예외 본문 유지, 2026-08-27 상태는 당시 기록으로 명시 |
| GDD 당시 VFX·건물 라인 사본 | [VFX](../../../History/ImplementationVerificationHistory.md#gdd-vfx-spec-history), [건물 라인](../../../History/ImplementationVerificationHistory.md#gdd-building-lines-history) | 원문 보존 후 원본 사양 참조화; 종족 아트 의도 보존 |

같은 문단은 SHA 기반 `record-*` anchor 한 곳에 보존하고 원본별 순서로 연결했다. SHA는 UTF-8/LF 문자열 기준이며, 상대 Markdown 링크를 History 폴더에서 같은 파일을 가리키도록 재기준화한 차이만 허용했다. 원문 안의 수정·정정 경계와 실패·미확정 값은 삭제하지 않았다.

### 실행 검증과 한계

- 재현 가능한 보존 검사: `Temp/docs-organization/verify_preservation.py`; manifest는 같은 폴더의 `management-originals`, `milestone-originals`, `status-system-originals`, `gdd-vfx-original`, `tdd-original`, `roadmap-completed-original`, `vfx-list-originals`, `gdd-building-records`, `vfx-guide-contract-original` JSON이다. 임시 자료는 최종 문서 인덱스에 등록하지 않는다. 영구 원문·SHA·원본 대응은 중앙 History에 있다.
- 각 manifest의 원문 SHA 일치, 링크 재기준화 후 모든 원문 문단의 목적지 존재, 명시 anchor 중복 없음, `record-*` 연결 대상 존재를 확인했다. 메인도 독립적으로 재실행했다. 최초 GDD/Stats 상단 snapshot은 호출별 sandbox TEMP가 사라져 중앙의 영구 SHA·문단순서와 메인의 초기 파일 snapshot으로 별도 검증한다.
- 추가 GDD 건물 라인 3블록 이관에서 상대링크 한 곳의 재기준화 누락을 checker가 찾아 즉시 교정했다. 원문 SHA는 유지하고 mapped 링크만 고쳤다. checker는 이후 문제 없음이었다. 한글 metadata 오타도 원문 외 설명에서 바로 고쳤다.
- 과거 raw `.txt` 경로는 당시 위치다. `_Logs/2026-10-11/20_28_logcat/RuntimeLog_device.txt`, `_Logs/_editor/2026-10-11/RuntimeLog.txt` 및 VFX 목록의 2026-10-09 raw 링크 일부는 현재 checkout에 없다. 원문 경로는 보존하고 현행 private 보관·mapping·영수증은 LogRules §1.10 / LogHistory / 기존 보관 Task를 따른다는 안내를 History 서두에 뒀다. 현재 raw 존재를 주장하지 않는다.
- 문서 checker는 규칙/Markdown 파일 참조를 검사한다. raw 가용성·앵커 의미·수치 진위·실기 수용은 별도 검토이며 checker0을 전체 정확성으로 확대하지 않는다. 기존 메모리 baseline 증가 안내는 이 작업의 메모리 수정이 아니며 baseline은 변경하지 않았다.

## 8. 모순 정리와 남은 판정

### 근거가 있는 사본 정리

- Sniper 생산시간 GDD15/Stats20은 Stats와 actual config20을 기준으로 GDD 수치 사본을 참조화했다.
- GDD의 낡은 스탯 확정/미확정 목록은 원본참조로 바꿨으며 표에 값이 있다는 이유로 임시값을 확정하지 않았다.
- GDD 특수 유닛·AI 피해 예시의 ×10 이전 수치, 시작 골드500 고정 사본, HumanTowerTank 이전 계약, 생산 자동큐/부족 자원 처리, Toast 예정 상태를 해당 Stats·맵·UI·최신 연결 원본으로 수렴했다.
- AIConfig 단일 에셋과 난이도 GetParams 원본으로 공통 타이밍/별도 난이도 에셋 사본을 수렴했다. 종족별 시나리오 3파일·채굴소·빌드오더와 예비 수지 계산 원문은 유지했다.
- 무작위 맵은 본체·재경기·최초 실패 안내 구현 범위와 InitialGold 실제 적용·차단 대응 잔여를 구분했다. 개별 수용과 구조 이행/자동 검증/시각 후속은 감사표의 4축 안내로 구분했다.

### 첫 정리 당시 결정 없이 남긴 항목

**현행 판정 안내:** 아래는 최초 보류 시점의 기록이며 현재 미결 목록이 아니다. 이후 사용자 결정으로 스탯·공격 시간·건물 본문을 실제 구현 기준으로 정정했다. 결과는 [후속 정정](#실제-구현값-기준-후속-정정)을 따른다. 이 목록 중 AI 시나리오 경제·빌드오더만 별도 새 논의로 유지한다. EagleArcher의 클립 열거 순서 한계는 §11의 읽기 조사 범위이며 기존 개별 수용을 재오픈한 것이 아니다. 이전 수치·보류 이유는 당시 원문 기록으로 보존한다.

| 위치 | 양쪽 값·주장 | 보류 이유 |
|---|---|---|
| StatsReference 「비시간 수치의 문서·설정 충돌」 DustSpirit | HP400 / config500 | 확정 사양·구현 중 어느 값을 채택할지 근거 부족 |
| 같은 절 BoulderSpirit | HP900 / config800 | 문서·설정 모두 그대로 보존 |
| 같은 절 BearGuard | 문서HP2000·생산시간40초·비용400 / configHP1000·생산시간25초·비용300 | 수치 변경은 별도 사양 결정 필요 |
| 같은 절 RabbitTrickster | 공격60 / config100 | 문서 정리로 밸런스 선택하지 않음 |
| AI 종족 시나리오 예비 수지 | 고정500·초기 채굴소 없음 계산 / 현행 맵별200~700 | 원문 계산은 당시 가정; 재시뮬레이션 미실행 |
| 유닛 공격 시간·전투 수용 | clip 확인 근거값과 config/fallback 값, 일부 focused와 전체 범위 | config만 확인된 값은 실제 clip 확정으로 승격하지 않음; 전체 정밀·역할교대·migration·clock 원인은 기존 OPEN 유지 |
| StatsReference 「유닛 — 인간계 (Human)」 Assault 행·「현재 시간값의 근거와 사본 불일치」 | 기본 주기0.2초 / clip-derived10/30≈0.3333333초 | 같은 타격5/30초와 별개의 주기 충돌; clip-derived와 fallback의 관계를 이번 문서 정리로 확정하지 않음 |
| StatsReference 「유닛 — 초월계 (Transcendence)」 BearGuard 행·「현재 시간값의 근거와 사본 불일치」 | 설계 주기1.666666…초 / config1.2초 | 실제 production clip·설계 결정 근거를 더 확인해야 하며 기존 타격13/30초와 섞지 않음 |
| StatsReference 「유닛 — 인간계 (Human)」 Tank/CannonCart 행·「현재 시간값의 근거와 사본 불일치」 | config hit4초 / 기존 시간 표기0:17, config 주기4초 | 실제 생산 연결의 Attack clip 자동 추출값이 확인되지 않았으므로 0:17의 의미와 실제 적용 hit을 확정하지 않음 |
| GameDesignDocument 「건물 시스템 → 2. 자원 건물 (Resource Collector / MiningPost)」 | 건물 업그레이드 및 Lv1/2/3 수입10/15/20골드·초 | StatsReference 자원·MiningPost 표는 기본 수입10만 규정한다. 단계별15/20의 확정 결정·승인·구현 근거를 이번에 확인하지 않았으므로 유일 기획안을 유지한다. 기본10과 반드시 모순이라거나 코드 미구현이라고 판정하지 않음 |
| GameDesignDocument 「건물 시스템 → 본기지 (Main Base)」 | 골드 수리·약한 기본 공격·선택적 Lv2체력+1000/Lv3수리효율/Lv4방어공격/Lv5근처아군버프 | StatsReference 종족별 Castle 표는 기본HP를 다루며 단계별 값·수리비·기본공격 수치가 없다. 선택적 기획의 확정 여부와 지원 구현을 확인할 근거가 부족해 원문 유지·기획 확인 대상으로 둔다. 코드 미구현 확인 결과가 아님 |

## 9. 실제 변경 파일 목록

메인이 실제 diff와 신규 파일 목록을 대조해 전달한 실행 목록이다. 아래 `Docs/`는 `Assets/_Project/Docs/`이며, 규칙 파일명은 `GameSystemRules_` 접두사를 포함한다.

| 구분 | 실제 파일 |
|---|---|
| 루트 인덱스 수정 | `AGENTS.md` |
| 개요·수치·기술·운영 수정 | `Docs/GameDesignDocument.md`, `StatsReference.md`, `TechnicalDesignDocument.md`, `UIGuidelines.md`, `GameSystemRules.md`, `WORKFLOW.md` |
| 프로젝트 관리 수정 | `Docs/PROJECT_STATUS.md`, `ROADMAP.md`, `WORK_HISTORY.md` |
| 에셋 상태·가이드 수정 | `Docs/Assets/UnitCombatAssetMatrix.md`, `VFXSFXGuide.md`, `VFXSFXList.md` |
| 개별 게임 규칙 수정 | `Docs/GameSystemRules/GameSystemRules_AI.md`, `GameSystemRules_AI_Scenario_Human.md`, `GameSystemRules_AI_Scenario_Spirit.md`, `GameSystemRules_AI_Scenario_Transcendence.md`, `GameSystemRules_Network.md`, `GameSystemRules_RandomMap.md`, `GameSystemRules_UI.md`, `GameSystemRules_UnitCombatSynchronization.md`, `GameSystemRules_Units.md` |
| 상세 기술 신설 | [TechnicalDesignDocument_Network.md](../../../TechnicalDesignDocument_Network.md) |
| 상세 규칙 신설 | [MatchFlow](../../../GameSystemRules/GameSystemRules_MatchFlow.md), [MatchFlowUI](../../../GameSystemRules/GameSystemRules_MatchFlowUI.md), [BuildingPanelsUI](../../../GameSystemRules/GameSystemRules_BuildingPanelsUI.md), [SettingsUI](../../../GameSystemRules/GameSystemRules_SettingsUI.md) |
| 상세 이력 신설 | [ImplementationVerificationHistory.md](../../../History/ImplementationVerificationHistory.md) |
| 현재 Task 실행 기록 수정 | 이 Research.md와 Plan.md |

독립 역할이 명확한 AuthSystemRules, LogRules/LogHistory, AABSizeOptimization, UnusedAssetAudit, Skills 가이드, 맵 템플릿 참조, 다른 에셋 제작 가이드, Buildings/Skills/Upgrade/Canvas/Sound는 이번 변경 파일 목록에 포함하지 않는다. 기존 `_Logs`의 `.meta` 삭제 9개는 착수 전 작업 트리 변경이며 이 정리의 변경으로 보고하지 않는다.

최종 checker·참조 검토는 전체 담당 문서가 고정된 뒤 메인이 실행했다. 결과는 아래 §10을 따른다. 이 문서 검증은 게임 실기 수용이나 전체 사양·코드 일치의 전수 검수 완료를 뜻하지 않는다.

## 10. 메인 최종 검증 결과

아래는 첫 구조 정리에서 작성 담당과 별도로 메인이 전체 문서를 고정한 뒤 직접 수행하고 전달한 결과다. 당시 단계는 **문서 정리 및 메인 검토 완료; 별도 사양 판단 잔여**였다. 후속 사용자 결정·실제 구현값 정정과 최종 검증은 §11을 따른다.

| 확인 항목 | 최종 결과·범위 |
|---|---|
| 문서 정합성 검사 | 기존 bundled Python `-X utf8 Tools/check_docs.py`: exit0·문제 없음. `[검사 범위]`는 숫자 규칙 정의 원본14문서, 참조90 Markdown, `.claude`40문서 |
| 독립 Markdown 파일·anchor 점검 | 상시48문서+루트2문서, 합50문서: missing Markdown파일0·남은 anchor 후보0 |
| 과거 raw 경로 가용성 | `.txt` unavailable4는 당시 위치 기록으로 유지. 현재 private 보관·mapping·영수증은 LogRules 계약을 따른다. Upgrade의 `[취소](연구만취소...)`는 실제 링크가 아닌 UI 예시로 확인하여 오탐에서 제외 |
| 스탯 표 보존 | 25유닛 행의 전후 핵심 값과 `원래 시간 표기` mismatch0. 근거로 정정한 시간값과 보류 값은 §8 및 Stats 해당 절을 따름 |
| 중앙 이력 보존 gate | `verify_preservation.py`: 9 manifest·209원문 기록 PASS, explicit ID590 중복없음·recordrefs552 목적지 도달 |
| 최초 원본과 독립 대조 | 메인의 initial HEAD 원문 snapshot origins206기록 unmatched0. GDD 상단7문단·Stats 상단32문단은 초기 SHA와 목적지 내용 존재 별도 PASS |
| 보호 파일 | CLAUDE.md·`.claude/MEMORY.md`·Tools/check_docs.py SHA가 초기 baseline과 정확히 동일 |
| 변경 범위·공백 오류 | 코드·에셋·추가 기존 Task/Log 변경 없음. `git diff --check` exit0. 상시 기존수정22·신설6·현재 Task2가 이번 실행 범위이며 기존 `.meta` 삭제9는 초기 변경 |

검사기 코드·메모리 baseline은 갱신하지 않았다. 전체 게임 사양과 실제 코드의 정합을 전수 검수했다는 결론을 내리지 않는다. 설정과 문서가 충돌하는 값, clip 확인 부족·기획 확정 부족 항목은 별도 판단 대상으로 남아 있다.

<a id="실제-구현값-기준-후속-정정"></a>

## 11. 실제 구현값 기준 후속 정정

사용자가 구조 정리 결과를 확인한 뒤 유닛 스탯·공격 시간·건물 기획 문서를 실제 구현값에 맞추도록 지시했다. 유닛 스탯은 추후 밸런싱할 현재 값으로 기록하며, 이미 교정한 공격 시간은 실제 생산 연결값으로 맞춘다. AI 종족 시나리오는 새 논의 항목으로 남긴다. §8과 §10은 첫 정리 당시 보류·검증 범위이며 이 추가 결정과 아래 실제 조사 결과가 해당 보류를 갱신한다.

이번 후속은 game-programmer의 읽기 전용 config·Factory·production clip·건물 소비 경로 조사와 메인 독립 검토를 바탕으로 문서만 수정한다. 코드·설정·클립·씬·기존 Task/Log·원문 History는 바꾸지 않는다. 공격 시간 교정 완료라는 사용자 확인을 새로운 전체 migration·정밀 계측·네트워크 원인 해결로 확대하지 않는다.

### 사용자 결정과 원본 적용

- 문서와 config의 유닛 수치 차이는 현재 구현값으로 정정한다. 이는 새 밸런스 설계가 아니며 기존 표 수치는 old→new 근거로 보존한다.
- 타격·주기는 production clip 선택과 Factory 소비 경로를 확인해 기록한다. config fallback만 읽은 값을 실제 피해 타이밍으로 단정하지 않는다.
- 건물의 선택적 기획은 원문을 보존하면서 현재 지원 기능과 구분한다.
- AI 경제 가정·Phase·빌드오더는 [ROADMAP의 AI 시나리오 재논의](../../../ROADMAP.md#ai-시나리오-재논의-사용자-추가-결정-2026-10-13)로 관리한다.

### 실제 값과 소비 경로 조사

읽기 전용 game-programmer 조사에서 `UnitStatsConfig.asset`, `UnitFactory.cs`, Game 씬의 Blue/Red Factory 연결, 25개 controller `Base Layer/Attack` 및 실제 클립의 GUID·저장 이벤트·시작/종료·최대 key 시간을 대조했다. 양 팀 controller 연결이 유닛별로 일치하고, 25개 기본 Attack state의 primary GUID가 해당 유닛 클립과 일치했다. 자료는 `.utmp/docs-organization-main/implementation-values.json` 및 `controller-attack-state-values.json`이며 아래에 필요한 영구 근거를 기록한다.

- UnitStatsConfig SHA256: `08D4DD01C10ED7C4176BDFC27F9C03D5C2E22F79D6AE44D6E85DDD0B64B7B6A5`.
- 일반 공격: UnitFactory의 `ApplyAttackClipCooldown`이 클립 길이를 주기로 적용하고 `ApplyHitFrameTimesFromClip`이 `OnAttackHit` 시간을 추출한다. 이벤트가 없을 때만 config hit값을 유지한다. Fox는 피해 설정을 유지하는 예외이며 Bloom은 힐 발동 후 config 쿨다운을 유지한다.
- 조사 범위는 저장된 연결·코드·에셋이다. 이번에 Unity를 실행해 `AnimationClip.length`나 clip 열거 순서를 관측하거나 새 실기·자동 gate를 실행한 것이 아니다. 저장 stop/start와 최대 key 시간이 같은 것을 확인해 현재 클립 길이를 기록했다.

| 유닛 | 현재 타격·발동 offset(초) | 현재 생산 주기(초) | 실제 Attack 클립 GUID | 클립 SHA256 |
|---|---|---|---|---|
| Pistoleer | 0.8 | 2 | `3dc19d88c3a013c46af811597796c70a` | `509D3C3301DE15397FC4BF8EB854DC8D4FC1641F603CCC4C4876F4CFBAF68536` |
| Assault | 0.16666667 | 0.33333334 | `5db36c06bba0cd24d97d1ad80bc7190a` | `D06D4D20CFCD0B8DB44DC6D48207A215FD01A65F36386CEDC376DD4AA90EE2FE` |
| Sniper | 1.7333333 | 3 | `783d389748c4c7743a831612d7b1b5f5` | `D48C1FB5900AD4E5B386CB46493A6C5D2FF9B3245BADAA6F678AD185AEBD9155` |
| LittleKnight | 0.25, 1.15 | 3.0000002 | `d6b7b1014667fcc4e9da29c9e929b9b0` | `E1BFD770A4550EA1B763223C19DA394487A85B6DFCB339DDCD202DA9A03862C3` |
| SpearMan | 0.8333333 | 2 | `4772066a52e78bc4cb4890c02068d0ea` | `367151D68AE50C247C32E73B05C8575A63156F818C1FF19FEDBA5E5642562099` |
| BattleAxe | 1.02 | 3.1666667 | `c97327687cff891418b039f23b5b214b` | `E56D839B880ABDC5911BB2F02EA89C758CFAB6D18DAC877BE6D96E96ABE65772` |
| Tank | 0.16666667 | 4 | `45dfc56b28d646b4da9722fd7be76418` | `60832D4D690CD7B0E11F5AE03BD4737F646BBDDA5EA8BEE56B510BFC0F7A90FB` |
| CannonCart | 0.16666667 | 4 | `97e6beac61ecd944595517c567aabe50` | `B15BBD5FC12CD7EE8A1C15E85B8C7537B1E2F649F8536EC6776625B72F377D6C` |
| FlameSpirit | 0.6666667, 1.1666667, 1.4333334, 1.6666667, 1.9, 2.1 | 3 | `63b9b44d031f68f468d186399d915ee7` | `8EC3D18B445C011C97877DDACDD0C0350E1C53ED03D68EF414B8B25150949C77` |
| EmberSpirit | 1 | 2.6666667 | `335daaffd649791409558085159725aa` | `FC587004537A1A3780425AEEF27AB16B79B661F4FAA942CC64D4A4A06416D94C` |
| InfernoSpirit | 1.5 | 3 | `aa08d04a7746a8c47a25e9ad6f9b7993` | `16DC359136A246FDFA50EB7950D5A422AA3BEF895E47780F5E4E29B997F343DD` |
| DustSpirit | 1.04 | 3 | `961cafd21c1fa13429c74aa65a3a6345` | `1B309A51455704A7274559E095ED719E24B5B17F9C1E4592BAF37B59B0053136` |
| BoulderSpirit | 1.3333333 | 4 | `a08b07c6564fb5741ace267fd675b087` | `36EB7412CEF31CD79D6BFBEC93649D64CD8D17C58F0FE0322E399A4E0F913E66` |
| TideSpirit | 1.5 | 3 | `436a2847db5341848804090de50cc4ea` | `FD1B7E03F109366C56C0850EF84D111908B56BD29DC488A4C3B1D692D2E86F71` |
| StreamSpirit | 0.5 | 1.5 | `98d5134cc0c7a3249aeb355be217dcec` | `0BAB06FAA1C8FBA2D1C4735F27BB37DA5032D5E3FE2BA9CA309B2E2CEA4C1128` |
| TorrentSpirit | 2.3333333 (파도발사) | 4 | `0396c9df8348dd04a99ba18bfb31485a` | `47E5BC5184A25825F7F14A43BCAB98271ADBB4C0CF2A792946A16A814FE5E79D` |
| BearGuard | 0.43333334 | 1.6666666 | `6cd4a1668d01f24448599b56306f2b13` | `61B8E15490ADBC7DE9202F80F39E16E37178974320E2D0793E699F1596EE60B4` |
| FoxMagician | 2.25 (피해; VFX1초) | 4 | `2a61dae4330eb7c418f33f773a87978f` | `1DFC102C062ECDD9F779AD73E7807BE50859C144E8E9D89616F9433A85136C09` |
| LionKnight | 0.6, 1.4333333 | 2 | `45c00f46f91a76047a76e774cfa0f6ac` | `209D3D7C34A0617C355AB1D2AF5EBB98FC91A7A95E6A63AB131B8354B601EC50` |
| RhinoBreaker | 1.05 | 2 | `9214cd0584e17414ab4439b9602e6e44` | `F374C31775BCAEDDBFD6B0647751ACF612E5EEE69174D78EBC7B888E259263D1` |
| EagleArcher | 0.1 | Attack1=1 / Attack2=0.70000005; 실제 열거 순서 미관측 | `573c970a52265b64b9d5d5a953ffbfb8` | `6BEE5753D34410B25819F57F5B34DCF06BF3775E070DCE8238931AF064AFA2D4` |
| RabbitTrickster | 0.6666667 | 2 | `8d21000f295b9774a9cb2540a71b8ce7` | `6A10FDBA19691960CB590F655393699D01F0B62336071B027FDCD1DE588D81F8` |
| BloomFairy | 1 | 4 (명목: 발동 준비1+발동 후3) | `b934af543a2aa8f4dbc72962f233fa9f` | `BF6A6CD4B33956D1AD1D80FA2D15D04353A97A24B6B9354BB9A27DFEA1BFD4D9` |
| MushroomBomber | 1 | 3 | `8b7e1085021a1bf41a62c06f6586df0a` | `E13A090579780EA4D8BA10ED5F2FB8EC059854B6C4758104D7C569A59BBE311E` |
| QuakeSpirit | 1.667 | 5 | `75f0862e387cda542979bed8db30ba62` | `65BAF082B9A6419D4E04B13279220A32F57FFEF8CDDE7E7F3C52EC8884A16DC2` |

### 수치 old→new와 보류 해소

| 대상 | 이전 사본/확인 상태 | 현재 구현 기준 정정 |
|---|---|---|
| DustSpirit HP | 400 / config500 선택 보류 | 500 |
| BoulderSpirit HP | 900 / config800 선택 보류 | 800 |
| BearGuard HP·생산시간·비용 | 2000·40초·400 / config1000·25초·300 선택 보류 | 1000·25초·300 |
| RabbitTrickster 공격력·연구표 | 기본60·증가5, Lv0~5=60/65/70/75/80/85 | 기본100·증가8, Lv0~5=100/108/116/124/132/140 |
| Assault 주기 | 기본0.2 / clip10/30 관계 미확정 | 현재 명시 생산 클립0.33333334; config0.2는 미사용 기본값 |
| BearGuard 주기 | 설계1.666… / config1.2 선택 보류 | 실제 생산 클립1.6666666; config1.2는 fallback |
| Tank/CannonCart 타격 | config4초만 확인, 실제clip미확인 | 실제clip marker0.16666667 / cycle4 |
| BattleAxe·Ember·Stream·Lion 주기 | config3.05/2.2/1.15/3 또는 과거사본 | 실제clip3.1666667/2.6666667/1.5/2 |

Rabbit 공격 연구표는 `UnitUpgradeUseCase.GetAttackBonus/GetEffectiveAttack`이 현재 기본 공격력100에 `Round(100×0.08)=8`을 적용하는 것을 확인했다. 새 연구율을 정하지 않았다. 표의 기본25행은 config에 맞추고 이전 시간문자열25행·기존 원문비고15개는 보존했다.

### 건물 문서의 문제와 실제 정정

GDD는 현재 건물 기능 절에 **아직 지원하지 않는 선택적 기획**을 함께 적어 실제 구현 기능처럼 읽히는 문제가 있었다. 다음 경로를 직접 읽고 현행 본문과 기획 원문을 분리했다.

| 대상 | 기존 문구 | 실제 소비 근거 | 정정·보존 |
|---|---|---|---|
| MiningPost 등급·수입 | 건물 업그레이드, Lv1/2/3 수입10/15/20 | `BuildingTypeHelper` 등록 메타와 `GetNextStage`에 MiningPost 다음 단계 없음. `BuildingPlacementUseCase.UpgradeBuilding`은 next 없음에서 반환. `ResourceUseCase.TickTeamIncome`은 살아 있는 해당 팀 MiningPost 개수×기본수입×난이도배율로만 계산. GameConfig 기본수입0·채굴10 | 현행은 채굴소당 기본10과 AI배율, 등급없음. 이전 원문은 GDD 「미확정 건물 기획」에 보존 |
| Castle 공격·Lv2~5 | 약한 기본 공격, HP+1000·수리효율·방어공격·주변버프 | Castle은 생산/다음 단계 메타 없음. Castle의 3종족 attackPower/range/cooldown0. `TowerCombatUseCase`는 Type AutoTower만 처리 | 현행 Castle은 HP와 승리조건. 공격·업그레이드 기획 원문 별도보존 |
| Castle 금화 수리 | 골드 소모로 체력 회복 | 입력 `CanShowActionPanel`과 Helper Castle=false 및 실행 소비 경로에 금화 수리 조작 없음. 읽기 조사에서 해당 action을 확인하지 못함 | 금화수리 현행지원기능에서 분리. `BuildingData.Heal`, MistShrineUseCase 및 NetworkHealthSync의 건물heal은 유지하므로 본기지 회복 불가라고 쓰지 않음 |

기획 원문은 [GDD 미확정 건물 기획](../../../GameDesignDocument.md#미확정-건물-기획) 한 곳에 보존했다. 현재 문서 정정은 기능 추가·삭제가 아니다. AI 시나리오 예비 경제 가정은 새 논의 백로그로 유지하고 수지 숫자나 build order를 수정하지 않았다.

### 남은 제한과 검증 범위

EagleArcher는 기본 Attack state의 길이1초·marker0.1과 Attack2의 길이0.70000005·이벤트없음이 존재한다. Factory의 첫 `Contains("Attack")` 선택은 Unity의 `animationClips` 실제 열거 순서를 저장 파일만으로 확정하지 못하므로 유일한 주기 선택 경계를 명시했다. 어느 경로라도 타격0.1은 기본 marker 또는 config fallback이며 동일하다. 기존 사용자 개별 테스트 수용을 미테스트나 새 공격 교정 필요로 재분류하지 않는다.

이 값 확인은 현재 문서와 저장 구현의 일치이며, 과거 전체 migration·정밀 시각·역할교대·clock 원인 OPEN을 종료한 것이 아니다. 후속 최종 checker와 메인 독립 비교 결과는 아래에 추가한다.

### 문서 담당 후속 검증

- 실제25개 조사 자료와 Stats 표를 다시 비교했다: 기본 HP·공격력·공격/감지 사거리·이동속도·생산시간·골드비용·인구 mismatch0, 명시 타격값25행 일치, 숫자로 확정된 주기 일치. EagleArcher 주기는 두 선택값을 명시하고, Bloom은 준비1+발동 후3이라는 명목 주기를 구분했다.
- 이전 `원래 시간 표기`25행을 변경 전 문서와 비교해 보존25·mismatch0을 확인했다. 이전 상태 원문비고는 현재 수용 상태 도장으로 사용하지 않는다.
- bundled Python `-X utf8 Tools/check_docs.py`: exit0·문제 없음. 검사 범위는 숫자 규칙 정의14문서·참조90 Markdown·`.claude`40문서다. 이전부터 있던 메모리 baseline 증가 안내는 유지하며 baseline은 수정하지 않았다.
- 이번 후속 수정은 GDD·StatsReference·ROADMAP·PROJECT_STATUS 및 현재 Research/Plan이다. 게임 규칙 본문은 소비 계약과 충돌하지 않아 수정하지 않았다. 코드·에셋·다른 Task/Log·원문 History·메모리는 그대로다.

작성 담당과 별도의 메인 검토는 이 검증과 구분한다. 위 checker0은 실제 게임 동작이나 clip 열거 순서의 실행 관측을 보증하지 않는다.

**메인 독립 검토 인계:** 실제 Attack `.anim`25개의 SHA·OnAttackHit 배열·m_StopTime과 StatsConfig SHA를 다시 대조해 issues0, 현재 Stats25행의8개 기본 필드와 config 비교 mismatch0을 확인했다. GDD 현행 건물 범위와 미확정 기획 원문도 직접 재독했다. 이 인계는 위 문서 담당 비교와 별개의 근거이며 새 Unity 실행·실기 수용을 뜻하지 않는다.

**사용자 추가 결정:** GDD의 추가 건물 기획은 아직 구현하지 않은 낮은 우선순위의 추후 고려 대상으로 원문을 남겨두기만 한다. 지금 구현·상세 설계하거나 ROADMAP의 활성 작업으로 추가하지 않는다.

**사용자 최종 범위 결정:** “밸런싱과 AI 시나리오는 묶어서 나중에 별건으로 다시 수정작업할거야.” 경제 기준·유닛/건물 스탯·종족별 빌드오더·난이도·Phase 타이밍은 그 미래 작업에서 함께 검토한다. 이번 문서 정리 완료는 유지하며 지금 수치·시나리오를 바꾸거나 새 Task를 만들지 않는다.
