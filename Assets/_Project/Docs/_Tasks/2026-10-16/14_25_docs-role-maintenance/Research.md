# 주요 문서 역할 재정리 — 조사

문서 정리 이후 여러 작업의 결과가 다시 주요 문서에 쌓였다. 필요한 결과를 더 붙이는 방식 대신, 현재 규칙·기술·수치·상태·할 일·과거 근거를 각각 제자리에서 읽도록 정리하고 다른 브랜치의 후속 작업에서도 같은 구조를 유지할 방법을 조사한다.

**현재 상태: 2차 본문 재정리·검증 뒤 사용자 결과 검토와 병합 승인을 받아 내용 커밋 `0a676aca`의 main 반영 완료. 푸시는 하지 않았다.** 1차 완료 판정의 부족과 당시 증거는 이력으로 구분한다. 주요42문서별 결과와 최종 검증은 §10, 실제 병합 기록은 §11에 있다. 기존 Task/Log·메모리·코드와 범위 밖 메타데이터는 보호했다.

## 1. 기준과 범위

- 프로젝트: `D:/Projects/Hexiege/Hexiege`, 작업 브랜치 `codex/docs-role-maintenance`. 기준 main `b4e7620e`는 메인 세션 전달값이다.
- 직접 파일 목록을 열거해 `Assets/_Project/Docs` 아래 Markdown 중 `_Tasks/`, `_Logs/`, `History/`, `_Reference/`를 제외한 **42파일**을 확인했다. `LogHistory.md`는 이 경로 필터에 포함되지만 역할은 이력이다. 42개를 모두 수정한다는 뜻은 아니다.
- 루트 `AGENTS.md`는 인덱스이며 별도 검토 대상이다. `CLAUDE.md`는 권한·위임 근거이고 이번 변경 대상이 아니다.
- 이전 문서 정리 Task와 기존 이관 파일은 읽기 근거로만 사용한다. 기존 Task/Log 원문을 다시 쓰거나 재배치하지 않는다.
- 게임 값·구현 정답의 새 감사가 아니다. 현재 승인된 결정을 유지하며 이번에 증거가 부족한 항목은 결론을 만들지 않는다.

## 2. 확인한 운영 근거

`WORKFLOW.md` 작업 시작 전 확인, [3]~[4], [10]~[12]는 조사·계획 승인·정합성 검토·변경 목록을 요구한다. 현행 [11]에는 이미 역할표·인계 복제 금지·본문 상태 제자리 갱신 기준이 있다. 기준이 없었던 문제가 아니라 이 간략한 기준이 후속 작업의 작성·완료 갱신·다른 브랜치 병합 검토로 충분히 연결되지 않은 문제다. 기존 [11]을 확장·정돈하고 검사기 범위와 한계는 보존한다.

`.claude/MEMORY.md` B3/B5/B7의 Read→표적 수정, 현행 상태의 제자리 정정, 날짜별 원문 보존을 확인했다. 문서 정리에도 같은 구분을 적용하되 메모리를 수정하거나 원문 규칙을 다른 곳에 복제하지 않는다. `.claude/mistakes.md` 목차와 2026-08-17 원문 소실 사건, 2026-10-06 수치 재측정 누락 사건을 읽었다. 같은 내용이 다른 위치에서 발견됐다는 이유만으로 이관 완료라고 단정하지 않고 원문·목적지 대응을 확인해야 한다.

`LogRules.md` §1.10(특히 554~571행)과 §2(725행 이후)를 확인했다. 현재 로그 규범은 LogRules, 날짜별 정책·실행 원문은 LogHistory, 작업별 과정은 Task/Log가 담당한다. 이 구분을 다시 무너뜨리지 않는 것이 이번 정리의 보호 조건이다.

## 3. 파일별 역할 목록

경로는 `Assets/_Project/Docs/` 기준이다. 아래 표는 이번 조사 대상 목록이며 상시 문서에 중앙 상세 역할표로 복제하지 않는다. 사용자 추가 결정에 따라 각 문서의 상세 역할은 그 문서 머리말, 공통 작성·갱신·병합 절차는 WORKFLOW, 경로와 한 줄 찾기 안내는 AGENTS가 담당한다.

| 파일 | 담을 내용 / 담지 않을 내용 | 권위·갱신 계기 |
|---|---|---|
| GameDesignDocument.md | 게임 경험·시스템 목적·기획 확정 여부 / 구현 receipt·컴파일 결과 제외 | 기획 원본. 사용자 기획 결정 때 갱신 |
| TechnicalDesignDocument.md | 공통 아키텍처·구현 패턴·개발 도구 사용 / 세션별 인계·경기 수치 제외 | 공통 기술 원본. 구조·운영 도구 변경 때 |
| TechnicalDesignDocument_Network.md | 네트워크 구조·데이터 계약·검증 방식 / 개별 실행 성공·빌드 시도 제외 | 네트워크 기술 원본. 계약 변경 때 |
| UIGuidelines.md | 공통 시각·조작 지침 / 화면별 구현 이력 제외 | UI 지침. 디자인 원칙 변경 때 |
| StatsReference.md | 현재 구현 수치·단위·선택 경계 / source 해시·compile 로그 제외 | 사람이 읽는 수치 미러. 실제 자산·소비 경로 변경 때 |
| AuthSystemRules.md | 인증 제품 동작·불변 조건 / 날짜별 수정 과정 제외 | 인증 규칙. 승인된 동작 변경 때 |
| LogRules.md | 현행 로그 규범·보관 절차 / 회차별 보관 영수증 제외 | 로그 정책 단일 원본. 승인된 정책 변경 때 |
| LogHistory.md | 과거 로그 정책·검증 원문 / 현행 의무 판정 제외 | 로그 전용 이력. 원문 이관·정정 주석 때 |
| GameSystemRules.md | 도메인 규칙 찾기·원본 연결 / 상세 규칙·수치·receipt 사본 제외 | 규칙 인덱스. 파일 역할·연결 변경 때 |
| GameSystemRules/GameSystemRules_Map.md | 맵 공정성·대칭·접근 조건 / 경기 분석 제외 | 해당 도메인 동작 원본 |
| GameSystemRules/GameSystemRules_RandomMap.md | 생성·검증·실패 처리 규칙 / 도구 삭제 영수증 제외 | 해당 도메인 동작 원본 |
| GameSystemRules/GameSystemRules_Network.md | Host/Client에 무관한 제품 동작 / NGO 구현 상세 제외 | 해당 도메인 동작 원본 |
| GameSystemRules/GameSystemRules_UI.md | 공통 UI·팝업·로딩 및 기존 위치 안내 / 인계 수치 제외 | UI 공통 원본·호환 참조 |
| GameSystemRules/GameSystemRules_MatchFlowUI.md | 실패·결과·재경기 화면 M/D 동작 / 도구 이동·삭제 과정 제외 | 경기 흐름 UI 원본 |
| GameSystemRules/GameSystemRules_BuildingPanelsUI.md | 생산·건물 패널 동작 / 개별 적용 과정 제외 | 건물 패널 원본 |
| GameSystemRules/GameSystemRules_SettingsUI.md | 설정·프로필 동작 / 테스트 회차 기록 제외 | 설정 UI 원본 |
| GameSystemRules/GameSystemRules_MatchFlow.md | 맵 전송·복구·재경기 운영 / 전송 구현 상세·경기 receipt 제외 | 경기 운영 원본 |
| GameSystemRules/GameSystemRules_Units.md | 이동·타겟·공격·피해·표현 불변 계약 / 과거 실패 분석·컴파일 나열 제외 | 유닛 규칙 원본 |
| GameSystemRules/GameSystemRules_UnitCombatSynchronization.md | 권위·시간·회차·전달·표현 동기화 계약 / 구현 허가 인계·회차별 실행 제외 | 전투 동기화 계약 원본 |
| GameSystemRules/GameSystemRules_Buildings.md | 건물 현재 동작과 기획 보류 경계 / 실행 과정 제외 | 건물 규칙 원본 |
| GameSystemRules/GameSystemRules_Skills.md | 스킬 공통 동작·미확정 기획 / 임의 목록·수치 확정 제외 | 스킬 규칙 원본 |
| GameSystemRules/GameSystemRules_Upgrade.md | 강화·연구·스케일 계약 / 수치 사본·적용 receipt 제외 | 강화 규칙 원본 |
| GameSystemRules/GameSystemRules_CanvasSortingOrder.md | Canvas 계층·정렬 계약 / 적용 일지 제외 | 정렬 규칙 원본 |
| GameSystemRules/GameSystemRules_Sound.md | BGM/SFX 동작·구조 / 제작 회차 제외 | 사운드 규칙 원본 |
| GameSystemRules/GameSystemRules_AI.md | AI 공통 구조·동작 / 시나리오·밸런스 재설계 제외 | AI 공통 원본. 이번에는 역할·참조만 |
| GameSystemRules/GameSystemRules_AI_Scenario_Human.md | Human 시나리오 원안 / 이번 내용 재설계 제외 | 향후 별건 재논의. 내용 보존 |
| GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md | Spirit 시나리오 원안 / 이번 내용 재설계 제외 | 향후 별건 재논의. 내용 보존 |
| GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md | Transcendence 시나리오 원안 / 이번 내용 재설계 제외 | 향후 별건 재논의. 내용 보존 |
| Assets/UnitCombatAssetMatrix.md | 유닛별 현재 연결·수용·이행 상태와 근거 포인터 / 긴 감사 과정·세션 로그 제외 | 에셋/수용 상태 원본. 실제 연결·수용 변경 때 |
| Assets/AssetList.md | 에셋 보유·선정·잔여 목록 / 제작 과정 제외 | 에셋 목록. 보유·선정 변경 때 |
| Assets/3DAssetCreationGuide.md | 3D 제작·명명·변환 절차 / 작업 receipt 제외 | 제작 지침. 절차 변경 때 |
| Assets/CommonAssetGuide.md | 공통 제작 조건 / 개별 생성 이력 제외 | 공통 제작 지침 |
| Assets/UIAssetGuide.md | UI 제작·import 절차 / 화면 구현 상태 제외 | UI 제작 지침 |
| Assets/VFXSFXGuide.md | VFX/SFX 제작·명명 지침 / 실기 분석 제외 | 표현 에셋 제작 지침 |
| Assets/VFXSFXList.md | 현재 preset·연결·보유·제작/수용 잔여 / source freeze·전체 해시·긴 경기 통계 제외 | VFX/SFX 목록·연결 상태 원본 |
| PROJECT_STATUS.md | 시스템별 현재 구현·검증 요약 / 날짜별 경기 receipt·세부 잔여 중복 제외 | 현재 상태. 사실 전환 때 제자리 갱신 |
| ROADMAP.md | 실제 미완·보류·우선순위·결정 필요 / 완료 항목 상세·수정 과정 제외 | 백로그. 완료·보류·우선순위 결정 때 |
| WORK_HISTORY.md | 주요 완료 마일스톤 날짜·결과·근거 연결 / 전체 검증 원문·현재 판정 사본 제외 | 완료 이력 인덱스. 마일스톤 완료 때 |
| WORKFLOW.md | 작성·갱신·검토·병합 기준 / 특정 작업의 삭제 영수증 제외 | 문서 운영 단일 원본. 승인된 절차 변경 때 |
| AABSizeOptimization.md | 주제별 최적화 조사·적용·롤백 기록 / 현재 크기 자동 추정 제외 | 전문 기록. 동일 주제 근거 추가 때 |
| UnusedAssetAudit.md | 감사 방법·결과·보류 결정 / 미참조=삭제 허가 단정 제외 | 전문 감사 기록. 같은 감사 후속 때 |
| Skills/SKILLS_GUIDE.md | 스킬 선택·사용 안내 / 프로젝트 기능 완료 이력 제외 | 도구 안내. 실제 사용법 변경 때 |

AGENTS는 이 전체 표를 재복제하지 않고 찾을 문서와 짧은 역할만 안내한다. GameSystemRules도 규칙 도메인 찾기이며 전체 프로젝트 문서 운영 권위표가 아니다. History/ImplementationVerificationHistory는 규범이 아닌 기존 이관 원문 저장소로 재사용한다.

## 4. 실제 혼재와 분리 판단

아래 위치는 조사 시점 행번호다. 날짜 존재 자체를 이관 사유로 삼지 않았으며, 같은 문단의 현행 계약과 과거 실행 사실을 분리해 읽었다.

변경 계보는 본문 존재와 구별한다. 메인 세션의 `a36ff2f7..HEAD` diff 확인 전달값에 따르면 Matrix 상단 10/14 Fox·10/15 SOLO 두 블록, Stats112 Fox controlled receipt, WORKFLOW의 Setup 삭제 결정 블록은 문서 정리 이후 실제 추가됐다. Matrix의 10/11 관측 등은 a36에도 남아 있던 기존 잔존이다. 아래 나머지는 이번에 후속 추가/기존 잔존 여부를 별도 판정하지 않았으며, 모두 병합으로 되돌아왔다고 단정하지 않는다. 실행 단계에서는 두 유형에 같은 역할 기준을 적용한다.

| 위치·현재 주장 | 판정 | 제안 목적지와 남길 내용 |
|---|---|---|
| PROJECT_STATUS 7~17: 새 싱글2경기·초월8종 관측·복원 감사 종결을 날짜별 긴 문단으로 설명 | 현재 상태는 필요하나 경기/감사별 receipt가 요약 영역에 재유입 | 현재는 SOLO focused 수용/전체 미수용 경계만 유지. 경기·감사 설명은 기존 Task로 연결, 이곳 유일 원문만 History로 |
| Matrix 3~9,17~36: source8 manifest·compile·controlled 실행·과거 감사 대기와 해제 | 에셋 연결/개별 수용과 상세 감사 경위가 혼합 | 현재 연결/수용 표는 유지. 경위·전달 주체·원시 숫자·이전 대기 문단은 History 대응 앵커/기존 Task로 연결 |
| StatsReference 108~112: Fox 8파일 receipt·Main compile/Probe·manifest 수령·native 미실행 나열 | 타격/주기 수치의 선택 근거를 넘어 검증 일지가 포함 | 현행 offset·단위·생산 선택 경계만 남기고 receipt는 History/기존 Task. 수치와 Eagle 선택 미확정은 보존 |
| Units 330~346: Assault 설정 정합화와 Fox typed marker 계약 사이에 변경 경위·이전 수용 해석 추가 | typed 사건·불변 조건은 규범, 적용 날짜/기존 수용 비교는 이력 | U-ATK-TIMELINE 및 현행 typed 계약은 본문, 변화/검증 경위는 History. 규칙 식별자 유지 |
| Sync 278~284: source freeze44·SHA 재대조·정적4·native 실패/대기와 구현 승인 인계가 Periodic 규칙 사이에 존재 | 현행 effect identity/owner 정책과 당시 진행 상태가 혼재 | 기존 NET-DELIVERY-TIMED 계약은 유지·현행화, 인계 문단은 History. native 미실행/OPEN은 해당 상태 요약에 남김 |
| TDD 11행 이후 MistShrine 날짜별 구현·검증 요약 | 구성 요소·설계 이유는 기술 가치가 있으나 구현 완료/실기 경위는 상태·이력 | 구조 설명은 해당 설계 절, 현재 구현 요약은 Status, 당시 원문은 History. 멀티 미검증을 없애지 않음 |
| ROADMAP 29~33 및51: 완료 이관 안내가 있으나 분류1의 옛10→8 경위·ConfirmPopup 완료 상세가 백로그 표에 남음 | 미완8의 목록 포인터는 필요, 닫힌 경위·완료 도구 설명은 백로그 아님 | 미완/미확인 경계를 간결히 유지하고 완료는 WORK_HISTORY 짧은 행+History/기존 Task 연결. 숫자 재판정 없이 근거 수준 표시 |
| WORKFLOW 237~245: 사용자 결정·과거 Setup14 실측·이번 삭제 승인 완료 | 최소 유지 정책은 운영 기준, 특정 회차 완료/실측은 이력 | 일반 기준·현재 사용 도구 안내 유지. 특정 삭제 결과는 기존 Task/WORK_HISTORY 포인터, 과거 실측 유일 원문은 History |
| MatchFlowUI 537~541: 프리팹 전제 옆에 Setup 실행·Tools 이동·삭제 선택의 연속 기록 | 레이아웃 전제는 규칙, 도구 수명 변경은 개발 이력 | 프리팹 전제를 현행 규칙으로 보존. 실행/이동/삭제 원문은 History로 옮기고 현재 도구 절차만 TDD/WORKFLOW 연결 |
| WORK_HISTORY31~32: 같은 SOLO 복귀의 구현 인계와 사용자 수용이 별도 행이며 한 행 안에 최초 대기·보완·추가 수용이 연속 정정 | 단계별 완료 사실은 이력 가치가 있으나 긴 재정정은 이력 인덱스 역할을 넘음 | 두 단계의 고유 사실을 지우지 않고 짧은 마일스톤/상세 근거 연결로 정리. 원문 정정 과정은 History에 보존. ROADMAP7~15의 focused 완료/감사 종결 설명도 현재 미완과 분리 |

### 이미 역할이 정리돼 있어 보존할 사례

- GDD 7~23행은 기획·수치·상태 역할을 분리한다. HumanTower Assault 재사용은 현행 기획이므로 날짜가 있다는 이유로 통째 이관하지 않는다. 이전 계약 변경 근거만 이력으로 구분한다.
- WORKFLOW [11]과 AGENTS 검사기 안내는 현행 지원 형식·검사 범위·한계가 반영돼 있다. 오래된 검사기 경고를 다시 가져오거나 코드 정비를 재개할 이유가 없다.
- LogRules §1.10/§2와 LogHistory의 규범/원문 구분은 유지한다. 과거 정책 앵커는 호환 안내이므로 삭제 대상이 아니다.
- 밸런싱·AI 시나리오 재논의 및 낮은 우선의 추가 기획은 완료로 바꾸지 않는다. 미검증과 사용자 보류는 문서 소음이 아니라 유효한 현재 경계다.

## 5. 원인과 실행 위험

원래 역할표·인계 복제 금지·현재 상태 제자리 갱신 기준은 이미 있다. 이를 작성·완료 갱신·다른 브랜치의 새 완료 블록 검토에 적용하는 구체 절차가 약해, 이후 실제 완료 receipt가 다시 추가되고 이전 잔존도 남았다. 「원문 보존」을 주요 문서에 이전 블록을 계속 쌓는 방식으로 적용하지 않도록 기준을 연결해야 한다. 기계 검사 0건으로는 이 혼재를 발견할 수 없다.

핵심 위험은 짧게 만드는 과정에서 유효한 계약·예외·미관측 범위를 함께 지우는 것이다. 각 블록을 의무/설계 이유/현행 상태/원문으로 나누고, 유일 원문은 대응표와 함께 옮긴 후 실제 보존을 대조해야 한다. 줄 수 감소만으로 성공 판정하지 않는다.

## 6. 로그와 원문 보호 경계

정책 원본은 LogRules §1.10/§2이며 다른 주요 문서에는 그 정책의 사본을 만들지 않는다. 이번 Task에서 확인한 보호 계약은 Log.md·기존 Task·raw txt.meta·CrossAuditResult 등 소스 근거 보존, raw RuntimeLog 본문만 지정 private `rmsepskek02/Hexiege-Logs` 월별 Release ZIP으로 별도 운영한다는 구분이다.

실제 운영 시 업로드 직전마다 private 대상 확인, ZIP+manifest 전체 다운로드·모든 entry 경로/크기/hash 검증, 원래 상대경로·내용/hash 보존이 필요하다. 추적 해제 승인과 로컬 원본 삭제 승인은 별개이며 자동 삭제·전체 _Logs ignore·history rewrite를 허용하지 않는다. **이번 문서 정리에서는 업로드·추적 해제·ignore 변경·raw 삭제·커밋을 수행하지 않는다.** 이 요약으로 정책을 새로 정의하지 않으며 실제 판단은 LogRules를 다시 확인한다.

## 7. 제안 결론

주요 문서를 더 나누기 전에 현재 본문에서 역할을 복원하고 기존 History를 재사용한다. 현행 결정은 제자리에서 읽히도록 갱신하고, 근거는 기존 Task/Log/History로 연결하며, 완료 작업은 짧은 이력 인덱스만 남긴다. 이후 WORKFLOW의 한 운영 기준으로 작성·완료 갱신·다른 브랜치 병합 검토를 연결하는 Plan을 제안한다. 게임 규칙·수치·검사회귀 조건과 현재 Editor 7파일/17메뉴는 변경하지 않는다.

작성 후 검사기를 한 번 실행해 exit0·문제0건을 확인했다(참조 대상90문서, 원본358·안내89). Task는 검사 제외이므로 이 결과가 새 계획의 의미·주장·이관 완전성을 검증하는 것은 아니다. 파일 목록과 두 Task의 목록을 별도로 대조해 주요42파일의 누락이 없음을 확인했다.

## 8. 사용자 추가 결정 — 상세 역할은 각 문서 머리말에

AGENTS와 WORKFLOW가 길어지지 않도록 AGENTS는 경로+한 줄 역할만 남기고, 각 문서 머리말에 목적/답하는 질문·포함·제외와 담당 문서·권위원본/참조·갱신 조건과 방법을 구체화한다. WORKFLOW는 이 머리말을 확인하는 공통 작성·완료 갱신·병합 절차만 관리한다. 42파일 상세 역할을 중앙 문서에 다시 복제하지 않으며 임의 줄 수 제한도 만들지 않는다. 이는 문서 책임 안내이며 CLAUDE의 권한 규칙 변경이 아니다. 기존 Task/Log와 날짜별 원문에 머리말을 일괄 추가하지 않는다. 이 계획 작성 후 사용자가 실행을 승인했으며 결과는 다음 절과 같다.

## 9. 실행 결과와 검증 경계

이 절은 1차 정리 시점의 결과와 증거다. 뒤이어 본문 역할 혼재가 남은 것을 확인하여 완료 판정을 철회했다. 현재는 round2 기준본으로 관리 문서 전문 재구성과 규칙·설계 본문 재정리를 진행하며, 최종 결과는 후속 절에서 구분한다.

### 반영한 내용

- 주요42문서와 AGENTS·기존 구현 이력의 머리말에서 목적/포함/제외·담당/원본/갱신 경계를 읽을 수 있게 했다. AGENTS는 경로·한 줄 역할, WORKFLOW는 요청→승인→위임→확인→갱신·병합 검토의 핵심 절차로 유지한다. 상세42역할표를 중앙 본문에 복제하지 않았다.
- 규칙·기술·수치/에셋·현재 상태·백로그 사이의 receipt/수정 경위를 기존 History로 옮겼다. 유효한 typed marker·SOLO clock/절대 due/pursuit·UI 프리팹·맵 형식/복구 계약은 현행 본문에서 읽히도록 남겼다. 미확정·보류·관측 한계를 완료로 바꾸지 않았다.
- Status에는 기존 기록의 시스템별 기능·확인 범위·잔여 요약을 남겨 상세 이력만 열어야 현재 답을 알 수 있는 공백을 보완했다. Roadmap은 완료 receipt/대체된 AttackTimeline 목록을 정리하고 실제 통합 회귀·결정 필요만 남겼다. WorkHistory는 짧은 마일스톤과 상세 포인터로 수렴했다.
- 초기 MapVersion1은 당시 사실로 보존하고 현행2와 구분했다. 최초 멀티 실패의 방/연결 정리와 재경기의 결과화면 복구를 구분했으며 새 사양·수치 변경이 아니다.
- AGENTS는 UTF-8/LF 기준11804→8454바이트, WORKFLOW는34140→31103바이트다. 디스크 CRLF 파일 크기와 혼동하지 않으며 임의 길이 제한을 만든 결과가 아니다.

### 검사와 독립 확인

문서 담당자가 전체 writer 안정화 후 검사기를 실행하고, 마지막 MapVersion 안내 보정 뒤 다시 실행해 최종 exit0·문제0건을 확인했다. 최종 stdout은 `.utmp/docs-role-maintenance/check-docs-final.txt`다. 실제 범위는 규칙 탐색19/해석16·원본358/안내89·정의 없는 시나리오3, 참조90문서/.claude40, 참조578+명명ID33·링크2154·앵커1189, 그룹/namespace 제외16·원문보존 보정1이다. Task/Logs 및 외부 skills/plugins/worktrees는 제외된다. 이 결과는 의미·구현 일치·Unity 실행 보증이 아니다.

메인 세션의 최종 독립 실행 결과는 `.utmp/docs-role-maintenance/root-final-verification.json`에 있다. **신규118대응+기존 보존 원문 재사용5=123대응**의 baseline source·SHA·목적지 실물·앵커 유일성을 확인했다. 신규118대응의 SHA는116종이며 동일한 Assault readiness 원문3곳은 한 실물과 세 앵커로 연결했다. 재사용5는 동일 사건이라는 추측이 아니라 기존 보존 블록 안의 해당 원문·SHA를 대조한 것이다.

메인 세션은 보호2006파일 불변, 기존 History 본문 전체 보존, 허용된 tracked 문서44개 외 변경0, 기존 H2~H6/HTML 앵커/규칙 정의 제목 순서 유지도 확인했다. Stats 수치12열·Matrix 프로필 앞5열·AI 시나리오3본문·제작 가이드4개의 fence 블록은 기준과 같았다. 이 수치·검증의 실행 주체는 메인 세션이며 문서 담당자의 별도 전수 코드/에셋 감사가 아니다.

공백 검사는 History의 보존 원문 한 행 끝 두 칸만 보고한다. `role-maintenance-20261016-assets-matrix-01`의 원래 `**브랜치:** codex/unit-movement-attack-sync-audit` 행이며 baseline과 같은 원문이다. 메인 세션이 원문 변형을 피하는 보존 예외로 확인했다. 새 EOF 빈행은 정리했고 그 외 공백 오류는 없다. 이를 무조건 exit0이라고 보고하지 않는다.

코드·게임값·자산·메타·메모리/baseline·기존 Task/Log·raw·ignore·Git 이력은 변경하지 않았다. 활성 Editor7소스/17메뉴·사용자 개별 수용과 밸런싱/AI 재논의·추가 기획 보류는 유지한다. 로그 업로드·추적 해제·로컬 삭제·Unity import/컴파일/메뉴·기기·새 QA는 실행하지 않았다. 커밋·main 병합·푸시는 별도 사용자 지시 전까지 수행하지 않는다.

### 변경 파일

직접 읽기 목록으로 tracked44개(주요42+AGENTS+기존 History)를 확인했고 현재 새 Task2개를 별도로 구분한다.

- `AGENTS.md`
- `Assets/_Project/Docs/AABSizeOptimization.md`
- `Assets/_Project/Docs/Assets/3DAssetCreationGuide.md`
- `Assets/_Project/Docs/Assets/AssetList.md`
- `Assets/_Project/Docs/Assets/CommonAssetGuide.md`
- `Assets/_Project/Docs/Assets/UIAssetGuide.md`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- `Assets/_Project/Docs/Assets/VFXSFXGuide.md`
- `Assets/_Project/Docs/Assets/VFXSFXList.md`
- `Assets/_Project/Docs/AuthSystemRules.md`
- `Assets/_Project/Docs/GameDesignDocument.md`
- `Assets/_Project/Docs/GameSystemRules.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Human.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_BuildingPanelsUI.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_CanvasSortingOrder.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Map.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_MatchFlow.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_MatchFlowUI.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Network.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_RandomMap.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_SettingsUI.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Sound.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Upgrade.md`
- `Assets/_Project/Docs/History/ImplementationVerificationHistory.md`
- `Assets/_Project/Docs/LogHistory.md`
- `Assets/_Project/Docs/LogRules.md`
- `Assets/_Project/Docs/PROJECT_STATUS.md`
- `Assets/_Project/Docs/ROADMAP.md`
- `Assets/_Project/Docs/Skills/SKILLS_GUIDE.md`
- `Assets/_Project/Docs/StatsReference.md`
- `Assets/_Project/Docs/TechnicalDesignDocument.md`
- `Assets/_Project/Docs/TechnicalDesignDocument_Network.md`
- `Assets/_Project/Docs/UIGuidelines.md`
- `Assets/_Project/Docs/UnusedAssetAudit.md`
- `Assets/_Project/Docs/WORKFLOW.md`
- `Assets/_Project/Docs/WORK_HISTORY.md`

현재 새 Task:

- `Assets/_Project/Docs/_Tasks/2026-10-16/14_25_docs-role-maintenance/Research.md`
- `Assets/_Project/Docs/_Tasks/2026-10-16/14_25_docs-role-maintenance/Plan.md`

### 원문 이관 대응표

기준은 `b4e7620e`, 원문 SHA는 UTF-8/LF 기준이다. 원문 전체는 각 링크의 fence 안에 보존했고 source와 기준을 fence 밖에 명시했다. 내부 상대 링크는 당시 source 문맥 그대로이며 현행 링크는 fence 밖의 출처를 따른다. 구현 scratch의 manifest/receipt는 상세 대조 수단이고 아래 표가 지속되는 source→목적지 대응 기록이다.

| source (Docs 기준) | 보존 앵커 | 원문 SHA256 |
|---|---|---|
| `StatsReference.md` | [core-stats-fox-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-stats-fox-01) | `fb34d4213da393baa9420d3f6673aa733533edadb6b0f25a0f425d14c732c82f` |
| `StatsReference.md` | [core-stats-notes-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-stats-notes-01) | `33741832b3085908a07a4352349839c97f8a9cd7f1db75bc9906f69474aeb1b7` |
| `TechnicalDesignDocument.md` | [core-tdd-mist-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-tdd-mist-01) | `5125510ef61360219dc45e8c76c90f9886ad41a4d19b9838fd3e32e27199ff7d` |
| `TechnicalDesignDocument.md` | [core-tdd-versions-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-tdd-versions-01) | `f7eebc4587c9da76b79ebdc207935e86270418e8cfa9420cc47fdbb3e9aa3a2c` |
| `PROJECT_STATUS.md` | [core-status-solo-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-status-solo-01) | `5fa831be666ce06f61f1e4b0eed301aaabefbea248913cea143f5f7b4c8f22bf` |
| `PROJECT_STATUS.md` | [core-status-assault-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-status-assault-01) | `400745e121730c1bbe2ce7323e52cea4c165d8b5dd79b8ec51820575bbf96ae1` |
| `ROADMAP.md` | [core-roadmap-assault-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-assault-01) | `400745e121730c1bbe2ce7323e52cea4c165d8b5dd79b8ec51820575bbf96ae1` |
| `WORK_HISTORY.md` | [core-work-assault-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-assault-01) | `400745e121730c1bbe2ce7323e52cea4c165d8b5dd79b8ec51820575bbf96ae1` |
| `ROADMAP.md` | [core-roadmap-solo-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-solo-01) | `de9effbd419740d92cc2dc6a33cda107e0fef169bbb0beba4f8357aaf394c210` |
| `ROADMAP.md` | [core-roadmap-editor-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-editor-01) | `30d40ac543104d944153c82a114eeaa8241bc4fca82e72c5699fa773d786df5b` |
| `WORK_HISTORY.md` | [core-work-solo-1](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-solo-1) | `0d90e37df0b66b634aad2636e465ba7a619b7a21efe6af07972f69a6c9b18862` |
| `WORK_HISTORY.md` | [core-work-solo-2](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-solo-2) | `db66f09ec62f7323fa6053146eeafe2eb2b85bf0da34bab276c6838e72f999da` |
| `WORK_HISTORY.md` | [core-work-solo-3](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-solo-3) | `7f37ca5fdd78837bde6b1324963cd0ca1c4d76bb9893016ce3a6e067614e43b7` |
| `WORK_HISTORY.md` | [core-work-audit-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-audit-01) | `baa071cf70e18ae72dee81ac97381c8f9f47c928a37a213d3005ad3620116b6b` |
| `WORK_HISTORY.md` | [core-work-ui-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-ui-01) | `6692b5e2c274a0781ae5dc4991dac7a7d148a198929bd257453a5d58a632cdfb` |
| `WORKFLOW.md` | [core-workflow-tools-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-workflow-tools-01) | `551187bbbdf55ae5148ac231d7e4e776e843cec0ce23b018f249f36bfdc894e7` |
| `WORKFLOW.md` | [core-workflow-roles-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-workflow-roles-01) | `6e6c6eb5aa9ad80a91e6e40839a94bc660906c271e6541ea164f805773e33467` |
| `WORKFLOW.md` | [core-workflow-audit-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-workflow-audit-01) | `57f63e85d01521c7a0ffba7eb1d13728d7681b8ef6216d4cb73ac9582846f72c` |
| `WORKFLOW.md` | [core-workflow-delete-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-workflow-delete-01) | `3f6637f50bdd15b733edce8d482bf4508ad67b7728486b6d522f9e2ac0341b5f` |
| `TechnicalDesignDocument.md` | [core-tdd-action-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-tdd-action-01) | `0c367f5bbb312bbe3821c25ab03ad252874f544bc44b87c27850d1c7dfffbc2d` |
| `TechnicalDesignDocument.md` | [core-tdd-failure-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-tdd-failure-01) | `403976f0af251ade97da55adec504cd2f7c026dea0bdd22805164513f9699852` |
| `TechnicalDesignDocument_Network.md` | [core-network-transfer-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-transfer-01) | `f1690e1211b753dfd9e369e43a1ab435811500525b8c2f22b3d7fa23e73ab0d7` |
| `TechnicalDesignDocument_Network.md` | [core-network-map-stages-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-map-stages-01) | `44c5308f7143939f9ece7d68d7b0d37f60fab6906cc114715dfa8ecd11a5f2e0` |
| `TechnicalDesignDocument_Network.md` | [core-network-recovery-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-recovery-01) | `c6e9b1e509d8dda93b4af94dedfcab9ea1a4e12676381838604e5180ad3daaf8` |
| `TechnicalDesignDocument_Network.md` | [core-network-result-leave-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-result-leave-01) | `5bd368164336e59bb3973a35f1062b6692814fe9e9953f1a08dc4192d6c0188e` |
| `ROADMAP.md` | [core-roadmap-reconnect-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-reconnect-01) | `e712e7cc738f62d737367dc9d240a00a641d47eca1767228ab5c34b8c1120b8a` |
| `ROADMAP.md` | [core-roadmap-row-74](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-row-74) | `b03b1f9f1cd029f4302bf633ce5009ae5adfc590e59b4d68dbba90b4b92e43f1` |
| `ROADMAP.md` | [core-roadmap-row-77](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-row-77) | `a3517cc49f13b6329cf724b862483f2ccfc9754f4ff7ac411f365e0cb0dc6e55` |
| `ROADMAP.md` | [core-roadmap-row-78](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-row-78) | `9cfd6dd62a94269a6d802a84a39b466cc1cb605abf3362e155c6a609f00cbc6f` |
| `ROADMAP.md` | [core-roadmap-row-90](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-row-90) | `fca49fbe64f594e0ff311d0639f07ba0a6e6981b84961e584532326f0dc9eead` |
| `ROADMAP.md` | [core-roadmap-row-91](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-row-91) | `7e5621a1daf976e6b77266bfc1882475ea70c1e46594112787832b449f965cf6` |
| `ROADMAP.md` | [core-roadmap-row-92](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-row-92) | `96e70be4f915ab5b006edc53dd47421e32aad54b9b0dd9a4c01e6ecbf285f98e` |
| `ROADMAP.md` | [core-roadmap-row-96](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-row-96) | `02337a796b20b8b8fe7e3f3f768fe3e6966302b8a7626b13eac163757f3509b7` |
| `ROADMAP.md` | [core-roadmap-row-97](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-row-97) | `bd6862dc7b4648024c24fe6511c836528aa9998b2a3daa3ea4251893601c8bc3` |
| `TechnicalDesignDocument_Network.md` | [core-network-failure-scope-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-failure-scope-01) | `8e7511c4c3018dde377e98aff8e2911ac53ffc94c83068594e2c50408b8c59d1` |
| `TechnicalDesignDocument_Network.md` | [core-network-version-field-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-version-field-01) | `198b6ce9029a331da484cdddd17142c1ad33505f0a52918118b53051ec7a6eb1` |
| `TechnicalDesignDocument_Network.md` | [core-network-version-transition-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-version-transition-01) | `90b59af0f35b0f7130908d820b91d72b81bf1200a5388a32c810934e88bb07d7` |
| `TechnicalDesignDocument_Network.md` | [core-network-version-package-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-version-package-01) | `bc1cb04a077b41a04662609ed89c25a6f57e2fed1f1e2aa3554abd024a24a57f` |
| `ROADMAP.md` | [core-roadmap-phase-56](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-56) | `1f6dbff96b39850729bc3b1d380f34faf6373f5437763193819cc220321c8a89` |
| `ROADMAP.md` | [core-roadmap-phase-57](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-57) | `bc4d85ace9c08f5669a2f22385b9119f4623f0c52459e1c4bfbf195d9a4088a6` |
| `ROADMAP.md` | [core-roadmap-phase-58](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-58) | `49eb0f6099a9dc590e68ef03b3cb09bf1fd07b5762d09fbf3daf070267482581` |
| `ROADMAP.md` | [core-roadmap-phase-59](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-59) | `b4b886ac8069066b6e6ebe20ee82de4200560409efe2e5169801b7729e04979c` |
| `ROADMAP.md` | [core-roadmap-phase-60](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-60) | `6fc20937e5896772d7f25441a566ff16795faccac275f20a3fca9e861ce98a73` |
| `ROADMAP.md` | [core-roadmap-phase-61](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-61) | `5cf0253c0034aa208f0097441a88d441b5efa235b8246b000f374dca5033e7b3` |
| `ROADMAP.md` | [core-roadmap-phase-62](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-62) | `7f459873183a0306b28c82ce239aca4109c7866192e60054dd82665aab61bc8e` |
| `ROADMAP.md` | [core-roadmap-phase-63](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-63) | `51b1a2081c413cb7c074ce1d69176761b7237fa3fa0dbadcf647e503e85592eb` |
| `ROADMAP.md` | [core-roadmap-phase-64](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-64) | `7aedc7a989a1cf260099b6feea9512877281f93f68d4b901cc9ccf246ef50857` |
| `ROADMAP.md` | [core-roadmap-phase-65](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-65) | `80a851849b77b94853e2160c1a0bd8e83e974f467dff5f00cf6795297cbeaa78` |
| `ROADMAP.md` | [core-roadmap-phase-66](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-66) | `cddc82689027ab7dc671f3ffd4ea6fa6fc1c966dcb0882d6e5b5d68c9828fe2a` |
| `ROADMAP.md` | [core-roadmap-phase-67](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-phase-67) | `00f50832911c17aed572782eb585a44d9baf6eb9659c8a71dffb2320d415721f` |
| `ROADMAP.md` | [core-roadmap-log-meta-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-log-meta-01) | `90f911b3e2a11bbc777e67cd080b45c9684c6840547727528504a476fac36303` |
| `ROADMAP.md` | [core-roadmap-status-ui-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-status-ui-01) | `bb318c84ba49888261d38d45a4b13856046c18c4ad78eb666fc9b2e6f78f1e32` |
| `GameDesignDocument.md` | [core-gdd-versions-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-gdd-versions-01) | `e483f335b150c23d6b1d4372011a4a62e7eaccb573d68e3f17d90815abe34c0a` |
| `TechnicalDesignDocument_Network.md` | [core-network-solo-reload-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-solo-reload-01) | `aefc02548145264da435506c3d8da505235832fa5b39faa740220a4e0cbe4b1e` |
| `ROADMAP.md` | [core-roadmap-reconnect-summary-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-reconnect-summary-01) | `6364f30c488e2cd152d6f94abf886b532ee45ccc102cab259ebf6aecc985bc67` |
| `ROADMAP.md` | [core-roadmap-mapversion-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-roadmap-mapversion-01) | `4e916d787aad5582cc707490d605d071bd45e9f17ee12f47eff237f1e314108f` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-01) | `701e33e53a51b9606fb1a9a81ab320145b297c5ae57b7dee4624ff30d93a3c40` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-02) | `ae9ddda507b9138580d4219da4ca944437c6ff60f0276af079f9696dc119ed4a` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-03](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-03) | `c543ff01eb27bba775cc2572344c8fa5e3a2d972cd875be7206b7262f898db18` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-04](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-04) | `85c56a848f34c9c8e363ace6d55eb3104acaa8e5001b35a000a930ab0e1b6559` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-05](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-05) | `fb3b718b17412ee88c25fc42c21fb59653f25f45a7cf1fe700dabe52dd452525` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-06](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-06) | `dc5fca94e12d7e208018bbc7ff7f3ac394463c7d03327443b95d246b779ffb43` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-07](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-07) | `5b7bacc831d2fa9e00216d3ae0315aaaf0472a40005aa34c88fafc66e097b3f8` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-08](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-08) | `ed2314560da5a40aa679733eb64be57a44f90a6674bbf03e3bf7b641e75afbb2` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-unitcombatsynchronization-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-01) | `76ce64f992110da6becda6d87f6250ab94fa0529d1add2b244c1b3af7f2fd65e` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-unitcombatsynchronization-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-02) | `b930b02ca9cd89bc6c01bca4e7d696b51070a6a111b16184ac37b863df10ffea` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-unitcombatsynchronization-03](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-03) | `330a161bfb77affd55d8aee1a691c66306b4cb6bff95f35dd78613b3a0cd22bb` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-unitcombatsynchronization-04](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-04) | `91303d2964808bade399cf6f5226706534243a996ea66e641c401f99b9df2e8f` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-01) | `453efb0bdfa51deb3834278e1f38a1d33fab3ad7e140baebd9034092a41649a8` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-09](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-09) | `f0fc2814e17b151fa740c74a47f69d4680e79c0467449ac496a7ccdf0f22994d` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-10](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-10) | `71f68a37efd12b5596952148797915337707c9a5b907a58cd43ae0aae688d01e` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-11](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-11) | `aac8b72ffb969cbbf7b8164b3571005ad2283d9c4098bf3a746451cf34e1d0a1` |
| `GameSystemRules/GameSystemRules_Units.md` | [rules-units-12](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-12) | `78942ebe3a89f2e47ec55482a0b6da4f4ed8a40dc2c9bc09df6bb4610e550f2c` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-unitcombatsynchronization-05](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-05) | `faa4d8c65efb6def4b889ba751a95afdbce2c842e13c117df06437c6caa6336f` |
| `GameSystemRules/GameSystemRules_RandomMap.md` | [rules-randommap-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-randommap-01) | `62d227480c3c39d10d7f6ee0ce2edd6d243c98a002d3bd78894d334c206473eb` |
| `GameSystemRules/GameSystemRules_Buildings.md` | [rules-buildings-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-buildings-01) | `d1d95fc2ccc36903caae930cab19be52a7da7b8112bf74225fdaf869d9d17a25` |
| `GameSystemRules/GameSystemRules_AI.md` | [rules-ai-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-ai-01) | `adb7595224286e0a40d1d54caff3d6d32c9c1835f9610fe4101ff391133e1d85` |
| `GameSystemRules/GameSystemRules_AI.md` | [rules-ai-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-ai-02) | `07438d92ee0c2a671b9a82f1f73e158411d4e152091317d220dce4e7c36cab36` |
| `GameSystemRules/GameSystemRules_Upgrade.md` | [rules-upgrade-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-upgrade-01) | `16ad7bb15ed90e412051c9bd2fde16463b885d839c8bbb9be273511f02caf693` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-02) | `40b7c01f492fcf797dfb1fbb0c53f873fbe090b61bc35b4b4fb5957cda0d850d` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-03](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-03) | `cb5059e696018ded2485124c4dd7c3fd0870e09bcd97b7dca8493f0d2363efae` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-04](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-04) | `8795c2691832f2187db55d8d93322908dfc01f26c0f818e6113ebb434250e38c` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-05](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-05) | `be96db8a49a6902b62cb5666080039e5b97e2c826caf0c2da132399c20aee4e9` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-06](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-06) | `adbeb5fbbf51a6cfbd18cb61cd43977e6098ed368247e98efdadfd7554f73b83` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-07](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-07) | `2f28791b98ae5e9593468d3803b888e2c10115c0290547173dd13036bf4f1eb0` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-08](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-08) | `0096d6dda7ac1a8d04ffbd51f5fcc31a9758fa96acedef1d61b870c7aba6854f` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-09](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-09) | `a1b3a8ac81915f01d7422039a564a9c232abe2fbbcb5b3e31e12192d1c04e6c3` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-10](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-10) | `117234a936a0543e89d6d274e683c1d88e5881448652fa0c687c9a771d62f4f6` |
| `GameSystemRules/GameSystemRules_Network.md` | [rules-network-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-network-01) | `ae6193227b596d443092c515aa7db0d52caa9d5ad8d4af3dd1d9392bc0e2c487` |
| `GameSystemRules/GameSystemRules_Network.md` | [rules-network-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-network-02) | `bbd78aee7fa5b79a922c9299bdff6a2a5dee64a91cb55f56cf0c94fca5963dc0` |
| `GameSystemRules/GameSystemRules_Network.md` | [rules-network-03](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-network-03) | `c66ad4f01b38fc877dc9a42528c1c9382908338790cece8c05be8961cc17047a` |
| `GameSystemRules/GameSystemRules_Network.md` | [rules-network-04](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-network-04) | `80543a020a8ff0de418e37bf906219f6b052f242daa25c43fd0707b0cb384bbc` |
| `GameSystemRules/GameSystemRules_MatchFlow.md` | [rules-matchflow-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-01) | `75791a47ab27cc07a8ca4efd879d5acafd61aff2b3de5c18a2e42b0496d4674b` |
| `GameSystemRules/GameSystemRules_MatchFlow.md` | [rules-matchflow-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-02) | `3cedfdef59ae33fe9ff4f50dc349604abbad920c5a6897d77b66d55f45cc4580` |
| `GameSystemRules/GameSystemRules_MatchFlow.md` | [rules-matchflow-03](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-03) | `ddf996a605b2bf54160aaa7e53f0872cc133bd464121916ffd46df936e18daa2` |
| `GameSystemRules/GameSystemRules_MatchFlow.md` | [rules-matchflow-04](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-04) | `ea7a6f40c3c21b16b938b730e2e34554986f2eb92efda564996c76095643f5ac` |
| `GameSystemRules/GameSystemRules_MatchFlow.md` | [rules-matchflow-05](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-05) | `9a86b1cacc68a7c5ecbd4be92804cdf73f4f8059d2bde6008bdb30348c9f042a` |
| `GameSystemRules/GameSystemRules_MatchFlow.md` | [rules-matchflow-06](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-06) | `714c021d7110e42be02309d73d99ae044aeb3e52100da47a3c0ba9101a612276` |
| `GameSystemRules/GameSystemRules_RandomMap.md` | [rules-randommap-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-randommap-02) | `afce885728cef5dbb5d999787b4d85acc2964d4f346d02ffdf529d632eea31b9` |
| `GameSystemRules/GameSystemRules_RandomMap.md` | [rules-randommap-03](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-randommap-03) | `2d1ca7710df1becc479cf1555a329a322a3cdd3c36b9a103802b2d7ae71b93e8` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-unitcombatsynchronization-06](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-06) | `2e4d92e085953a6f1626bb61f6497c0cfead740c2483eac470aaf4fc63260cde` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-11](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-11) | `0026099d26da96ce191fdb563dc4766444d70bba288dda7b9b1a0697c755dd43` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-12](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-12) | `a7e2d4a1455c09234e9e460d6de6d1afc983a391da74f40679805d5e3422643d` |
| `GameSystemRules/GameSystemRules_MatchFlowUI.md` | [rules-matchflowui-13](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflowui-13) | `6db287436fbe48873f77a6785166ee3fd618289e3320bbd7e51e19a780d71c19` |
| `Assets/UnitCombatAssetMatrix.md` | [assets-matrix-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-matrix-01) | `147c61e51eb425e0b0a18f963506e50ef20fc82dde131d3626eb5daaae242925` |
| `Assets/UnitCombatAssetMatrix.md` | [assets-matrix-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-matrix-02) | `f6a483231555fd2468815819830b8db21d98ea7a047c14fe9e8d8f55796dc565` |
| `Assets/UnitCombatAssetMatrix.md` | [assets-matrix-03](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-matrix-03) | `4f6297fdd00d2bb2c49e440df10932d684e9e0b29790149de72f03cbc9c12bf8` |
| `Assets/VFXSFXList.md` | [assets-vfx-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-01) | `1ea4d007f89c7c053a907bbf79e91d85daec6b70df9b39ef07a40c66709fc4aa` |
| `Assets/VFXSFXList.md` | [assets-vfx-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-02) | `20458076c64fe49cdd199ece3e33d62c6df56085667f266ef3602d1b4c6fab08` |
| `Assets/VFXSFXList.md` | [assets-vfx-03](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-03) | `047de363fd5d3646ebc6b861bceb4f406cc59438555e4e9835b35535679109c7` |
| `Assets/VFXSFXList.md` | [assets-vfx-04](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-04) | `3053a7baf6871d8cdafea78a32ccbaeb02b0e0d9a79e1e2672e7a83e6935a3d3` |
| `Assets/VFXSFXList.md` | [assets-vfx-05](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-05) | `53fa3a0d61d33d802636d07d5ef7c0fb5c6ec44a1e5fdc8fb8987b151cbef653` |
| `Assets/VFXSFXList.md` | [assets-vfx-06](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-06) | `088c3a8c1c9861ad92db2f3974e21828c0cc3e3efd5746656f53290ffe6c994f` |
| `Assets/VFXSFXGuide.md` | [assets-guide-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-guide-01) | `378191830df047ccb23f0b9c22d633343057ff041316b97bf22b6a237fdd4baa` |
| `Assets/UnitCombatAssetMatrix.md` | [assets-matrix-04](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-matrix-04) | `9df3dd247ee9860bf0a32b31e6f82b926decd708dc13927b31b41ac5ca264ea8` |
| `GameSystemRules/GameSystemRules_Skills.md` | [skills-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-skills-01) | `1dac005e414ebf9771a27c4537517a994a370f5727b8dfb4c6d8f40651e9f771` |
| `GameSystemRules/GameSystemRules_Skills.md` | [skills-02](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-skills-02) | `24c55c0f433e6e606f2d67ac8dbdb22358d87760486221138db2a245bfc1428b` |
| `GameSystemRules/GameSystemRules_UI.md` | [ui-01](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-ui-01) | `67bb9bfbed04608fb960b63add7e40d3f09214dbda005e9e8c20566017583373` |

기존 보존 원문을 재사용한 대응:

| source (Docs 기준) | 기존 목적지 | 원문 SHA256 |
|---|---|---|
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-units-07](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-07) | `bf105bba60c4159cea941a4937ae1c4d058c222077e38093e5b7905751555c17` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-units-07](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-07) | `cec1caf79cbeeab48b940bba23adbca709ad53bf1a1ab7c8b69c93d9fc2676f3` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-units-07](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-07) | `1fb0dc37a1f2446a5badeb86fe49571ece9cf1706c770dafbd427488e05a3b5f` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-units-07](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-07) | `482e41f6bc2bceaa3f0cf05a1d1c9424c68ee931419132aa1bb06453241de964` |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | [rules-units-08](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-08) | `459bec51c374437f0add207600146723385ca5dbddb1909dd33cfb1e4cb4c5b4` |

## 10. 본문 재정리 — 2차 실행

1차는 머리말·일부 이관과 기계 검사로 완료를 판단했지만 본문에 오래된 완료/미완 표·상태 사본·제목과 이력 링크만 남은 절이 잔존했다. 사용자 재정리와 이어 진행 지시에 따라 담당 문서 전문을 다시 검토했다. 일반 제목을 모두 남기는 것을 완료 조건으로 삼지 않고, 실제 참조는 호환 앵커로 유지하며 본문 역할을 우선했다.

- Status는 시스템별 현재 기능·확인 범위·한계로 재구성했다. 로그인 구현과 후속 연결, Firebase 인증과 UGS 프로필/랭킹, 싱글 실패 피드백과 멀티 RPC UI 미완, 볼륨 UI 확인과 BGM/SFX 배선·실기 잔여를 구분했다. 옛 GameConfig 완료 목록·로그 이관표·기술 트리·일부 모델 사본은 현황에서 제거하고 해당 원본으로 연결했다.
- ROADMAP은 실제 미완·선행 조건·보류만 분류별로 통합했다. 완료된 신규 유닛 등록·기존 Quake 수용을 다시 미완으로 만들지 않았고, 밸런스 수치 사본·상세 완료 경위는 남기지 않았다. 밸런싱+AI는 추후 별건, BM 상충을 포함한 추가 기획은 낮은 우선 미결을 유지했다.
- WORK_HISTORY는 기존 마일스톤250행을 날짜순으로 정돈하고 긴 실행 receipt를 보존 원문으로 연결했다. 과거 분석/구현 완료와 당시 실기 OPEN은 해당 시점의 기록으로 남기며 현재 판정은 Status·Matrix로 연결한다.
- 규칙·기술·수치/에셋 문서는 각 담당자가 본문을 검토해 유효 조건·이유·미확정 범위를 남기고 과거 개정/상태 사본을 이관했다. 코드·게임 값·개별 수용을 바꾸는 작업은 아니다.

### 원문 보존 대응

2차 기준은 `.utmp/docs-role-maintenance/round2/baseline.json`이다. 각 manifest의 `sourcepath/sourceheading/text/sha256/anchor/replacement`가 원문과 현재 본문의 대응을 기록하며 목적지는 모두 기존 History다. 원문은 UTF-8/LF SHA로 확인하고 내부 코드 fence보다 긴 바깥 fence 안에 그대로 보존한다. source 본문은 중앙 보존과 메인 세션의 독립 대조 뒤에만 치환했다.

| manifest — round2 폴더 | 대응 수 | 원문·현재 본문 역할 |
|---|---:|---|
| `core-management-archive.json` | 4 | Status·Roadmap·WorkHistory 본문과 WORKFLOW 갱신 항목 |
| `assets-core-archive.json` | 40 | 기술/기획·수치·목록의 과거 동작/사본·상세 요약 |
| `assets-core-addendum-archive.json` | 6 | 기술 구현 단계·인증 예외·경제 상세 사본 |
| `assets-core-final-archive.json` | 15 | 전문 재검토 잔여와 과거 편집 지시 제목 |
| `rules-archive.json` | 57 | 유닛·UI·연구·맵 등 과거 개정/판정·다른 원본 사본 |
| 합계 | 122 | 원문 소스29파일, 목적 anchor별 보존 |

직접 확인 기록은 `round2/archive-receipts.json`, 관리 문서 전문 검토 기록은 `round2/review-ledger.json`이다. Task에 원문을 재복제하지 않는다. 모든 작성 담당 안정화 뒤 문서 검사와 메인 최종 독립 대조를 완료했다. Unity/native 메뉴·기기·멀티 실행은 수행하지 않았다.

### 주요42문서별 본문 검토 결과

아래 판정은 2차 본문 검토의 처리 방식이다. 1차 머리말 정리 여부와 구별하며, 역할상 유지한 문서도 전문 검토 대상에 포함했다.

| 문서 — Docs 기준 경로 | 처리 | 핵심 이유·보존 범위 |
|---|---|---|
| PROJECT_STATUS.md | 재구성 | 시스템 현재 기능·확인 범위·한계. 과거 완료표·기술/에셋 사본 제거 |
| ROADMAP.md | 재구성 | 실제 미완·선행·보류 통합. 완료 항목·수치 사본·옛 Phase 중복 분리 |
| WORK_HISTORY.md | 재구성 | 완료 마일스톤 날짜순 정돈·긴 receipt 이력 연결 |
| WORKFLOW.md | 부분 정돈 | 일반 제목 통합과 실제 참조/규칙ID 보존을 구별 |
| GameDesignDocument.md | 부분 정돈 | 게임 의도·컨셉 유지, 상세 규칙/값 사본 분리·BM 미결 보존 |
| TechnicalDesignDocument.md | 부분 정돈 | 현행 계층·구조·사용법 유지, 옛 프로토타입/완료 상태 분리 |
| TechnicalDesignDocument_Network.md | 부분 정돈 | 현행 전송/책임/계약 유지, 과거 형식·부분 구현 사본 분리 |
| StatsReference.md | 부분 정돈 | 현행 값·단위·예외 유지, 이전 값 열·빈 절·검증 receipt 분리 |
| AuthSystemRules.md | 부분 정돈 | 이메일·닉네임 예외를 본문 조건에 통합, 날짜별 부록 분리 |
| UIGuidelines.md | 부분 정돈 | 제작·연결 지침 유지, 삭제된 도구 메뉴 안내 정정 |
| GameSystemRules.md | 부분 정돈 | 찾기 인덱스 유지, 상세 규칙 사본을 원본 참조로 수렴 |
| LogRules.md | 역할상 유지 | 현행 보관·수집·마스킹·작성 정책 및 해석 예시 유지 |
| LogHistory.md | 역할상 유지 | 로그 규칙의 날짜별 원문 자체가 역할이므로 본문 보존 |
| AABSizeOptimization.md | 역할상 유지 | 당시 최적화·기기 확인·롤백 기록 자체가 목적 |
| UnusedAssetAudit.md | 역할상 유지·참조 정돈 | 당시 감사 방법·오탐·결정 보존, 현재 전 에셋 판정으로 승격하지 않음 |
| Skills/SKILLS_GUIDE.md | 역할상 유지 | 사용 트리거·설치·호출 예시가 운영 안내 |
| Assets/AssetList.md | 부분 정돈 | 현재 인벤토리·보류 유지, 상태 사본/연결 표현 정돈 |
| Assets/UnitCombatAssetMatrix.md | 부분 정돈 | 25종 프로필·준비/이행·개별 수용 유지, 옛 집계 분리 |
| Assets/VFXSFXList.md | 부분 정돈 | 현재 연결·수용/보류 유지, 과거 source/native 경위 분리 |
| Assets/3DAssetCreationGuide.md | 부분 정돈 | 제작 규약·프롬프트 유지, diff 흔적·과거 제작 상태 분리 |
| Assets/CommonAssetGuide.md | 부분 정돈 | 공통 제작 조건 유지, 상세 전투/수치 사본 분리 |
| Assets/UIAssetGuide.md | 역할상 유지 | 제작 흐름·Sprite 설정·재사용 프롬프트 보존 |
| Assets/VFXSFXGuide.md | 역할상 유지 | 표현 방향·요청 브리프·명명·연동 지침 보존 |
| GameSystemRules/GameSystemRules_AI.md | 부분 정돈 | 현행 난이도/반응 계약 유지, 과거 단계/사본 분리 |
| GameSystemRules/GameSystemRules_AI_Scenario_Human.md | 역할상 유지 | 시나리오 원문 유지, 밸런싱과 별건 재논의 |
| GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md | 역할상 유지 | 시나리오 원문 유지, 밸런싱과 별건 재논의 |
| GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md | 역할상 유지 | 시나리오 원문 유지, 밸런싱과 별건 재논의 |
| GameSystemRules/GameSystemRules_Units.md | 부분 정돈 | 현행 이동/공격/배율·시간 조건 유지, 옛 간략문·교정 receipt 분리 |
| GameSystemRules/GameSystemRules_UnitCombatSynchronization.md | 부분 정돈 | 권위/전달 계약 유지, 과거 pose-hold/retime·native 절차 사본 분리 |
| GameSystemRules/GameSystemRules_Upgrade.md | 부분 정돈 | 현행 연구/배율/감쇄 계약 유지, 과거 구현·범위 표기 정돈 |
| GameSystemRules/GameSystemRules_Buildings.md | 부분 정돈 | 현행 건물 기능·미결 유지, 과거 구현/색상 사본 분리 |
| GameSystemRules/GameSystemRules_BuildingPanelsUI.md | 부분 정돈 | 패널 조건 유지, MistShrine 상태 중복 분리 |
| GameSystemRules/GameSystemRules_Skills.md | 부분 정돈 | 스킬 계약·미확정 목록 유지, 좌표화 전후/구현 단계 이력 분리 |
| GameSystemRules/GameSystemRules_UI.md | 부분 정돈 | 공통 UI 현재 정의 유지, 옛 예시/해석 개정 분리 |
| GameSystemRules/GameSystemRules_MatchFlowUI.md | 역할상 유지 | 경기 실패/결과/재경기 화면의 현행 규칙과 조건 유지 |
| GameSystemRules/GameSystemRules_SettingsUI.md | 부분 정돈 | 현재 설정/프로필 조건 유지, 고정 팀색상 사본 정돈 |
| GameSystemRules/GameSystemRules_CanvasSortingOrder.md | 부분 정돈 | 현재 SortingOrder·추가 규칙 유지, 과거 구현 receipt 분리 |
| GameSystemRules/GameSystemRules_Sound.md | 부분 정돈 | 현행 사운드·추후 분리 선행조건 유지, 옛 고정 팀 예시 분리 |
| GameSystemRules/GameSystemRules_Map.md | 부분 정돈 | 대칭·공정성 계약 유지, 당시 시뮬레이션 수치 이력 분리 |
| GameSystemRules/GameSystemRules_RandomMap.md | 부분 정돈 | 현행 생성/검증/폴백 조건 유지, 과거 실측/버전/폐기 용도 분리 |
| GameSystemRules/GameSystemRules_MatchFlow.md | 부분 정돈 | 맵 전송·실패·이탈 계약 유지, 과거 호출/구현 경위 분리 |
| GameSystemRules/GameSystemRules_Network.md | 역할상 유지 | Host/Client 무관한 제품 조건과 이탈 판정 책임 유지 |

별도 원본인 AGENTS는 메인 세션이 전문 검토해 인덱스 역할상 유지했다. 중앙 History는 새 이관을 추가하고 기존 전문은 보존했다. 문서별 절·행 검토의 상세 기록은 담당 ledger에 두되 위 결과와 핵심 판단은 이 Task에서 읽을 수 있게 남긴다.

### 2차 검증 결과

- 직접 실행한 최종 문서 검사: exit0, 문제 없음. 규칙 탐색19/해석16, 원본358/안내89, 참조 대상90문서와 `.claude`40문서, 규칙 참조419+명명ID33, 링크2363/앵커1210. 실제 stdout은 `round2/check-docs-final.txt`에 저장했다. 첫 실행의 TDD→Units 앵커1건은 실제 현행 절을 확인해 교정 후 재실행했다.
- 직접 원문 확인: 새122대응의 baseline 원문/SHA·목적 anchor 유일성·anchor 뒤 원문 존재를 확인했다. 신규21블록이 기존 History 중간에 삽입된 것을 발견해 해당 블록만 말미로 이동했다. 기존 원문의 순서·내용 손실은 없었고 이동 뒤 round2 기준 History 전문의 연속 보존을 재확인했다.
- 직접 `git diff --check`: History4849행의 원문 끝2공백1건만 남았다. `**브랜치:**` 당시 행의 Markdown 공백을 지우면 보존 원문이 달라지므로 유지한다. 관리 문서의 불필요한 EOF 빈행은 정돈했다.
- 담당자가 전달한 전문 검토 범위: 규칙19문서325절, 설계/에셋 등19문서513절. 관리3문서는 전문 재구성, WORKFLOW는 메인 전문 검토 뒤 갱신1항목 보완, AGENTS는 메인 전문 검토 후 유지했다.
- 메인 세션 전달 최종 독립 검증 PASS: `round2/root-final-verification.json`. 신규122대응 원문/SHA/목적지·앵커, 기존 History 전문 연속 보존, 보호2006파일 불변, 주요42문서 검토와 직전 대비30문서 변경, 규칙ID/HTML앵커 유지, AI3시나리오 원문·로그 규범/이력 불변, Stats25종×10현재값 열·Matrix25행 불변, 코드/기타 tracked 변경0을 확인했다. 이는 문서·보존 검증이며 미실행 Unity/기기/멀티 PASS를 뜻하지 않는다.

## 11. 사용자 승인과 main 병합

사용자가 최종 결과 검토 뒤 「메인에 병합하도록하자」로 승인했다. 메인 세션 전달값에 따르면 내용 커밋 `0a676acad5006d3a4bcb6d95d2deee1bf22a9079`를 만들고 main을 `b4e7620e`에서 `0a676aca`로 fast-forward 병합했다. 병합 뒤 main·작업 브랜치·HEAD의 동일 리비전도 메인 세션이 확인했다.

병합 전 `check_docs`는 exit0·문제0이었다. 내용 커밋 범위는 50파일(주요 문서44 + 현재 Task2 + 관련 Unity 메타4)이며, 기존 raw 로그·다른 Task/메타는 포함하지 않았다. 푸시는 하지 않았다. 이 병합 영수증은 `codex/docs-role-maintenance`에서 기록하며 기록 자체의 커밋 해시는 미리 기재하지 않는다.
