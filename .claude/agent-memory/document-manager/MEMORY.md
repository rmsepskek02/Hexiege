# document-manager 에이전트 메모리

> 이 파일은 document-manager 에이전트가 세션 간에 누적하는 지식을 저장합니다.
> 200줄 이내로 유지할 것.

## 담당 문서 구조 요약

- 절대 규칙: `CLAUDE.md`, `AGENTS.md`
- 에이전트 정의: `.claude/agents/*.md`
- 공용 메모리: `.claude/MEMORY.md`
- 에이전트 개별 메모리: `.claude/agent-memory/[agent-name]/MEMORY.md`
- 작업 사이클: `Assets/_Project/Docs/WORKFLOW.md`
- 프로젝트 관리: `PROJECT_STATUS.md`, `ROADMAP.md`, `WORK_HISTORY.md`
- 설계 문서: `GameDesignDocument.md`, `TechnicalDesignDocument.md`, `GameSystemRules/`
- Task 문서: `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/`
- 로그: `Assets/_Project/Docs/_Logs/`

## 주요 원칙
- 추정 금지 — 파일을 직접 읽어서 확인 후 작성
- 범위 외 수정 전 사용자 승인 필요
- 문서 첫 부분은 자연어로 목적 설명 (CLAUDE.md 규칙 13)

## 업데이트 패턴 (발견 사항)
- game-programmer 메모리는 `MEMORY.md`(200줄 요약, "최근 작업" 섹션에 최신 항목 prepend) + `work-history.md`(상세 전체) + 주제별 토픽 파일(network.md/architecture.md/ui-system.md 등)로 분리. 코드 변경 시: ① MEMORY.md 최근 작업에 요약 추가 ② 해당 토픽 파일에 상세/교훈 추가.
- 버그 수정 시 토픽 파일이 이미 존재할 수 있음(예: random-matching-bugfix.md는 별개 2026-03 버그). 새 버그는 관련 토픽 파일(network.md 등) 본문에 추가하고, 기존 동명 파일과 혼동 주의.
- PROJECT_STATUS.md / ROADMAP.md / WORK_HISTORY.md는 모두 헤더에 "최종 수정일" + "현재 단계" 보유 → 작업 완료 시 3개 모두 갱신.
- 표준 완료 작업 항목 형식: 시스템 분류 표(`| 항목 | 상태 | 비고 |`)에 행 추가. WORK_HISTORY는 마일스톤 표 상단에 날짜 역순 prepend.

## 누적 교훈
- 환경 주의: `.claude/MEMORY.md`의 절대 경로는 Windows(`d:/Dmain/...`)로 적혀 있으나 실제 작업 환경은 Linux(`/home/user/Hexiege/`). 파일 접근은 항상 `/home/user/Hexiege/` 기준.
- PROJECT_STATUS.md는 길어서(약 680줄) Read가 truncate됨 → 부분 read한 줄은 Edit 전 해당 위치를 다시 Read해야 편집 가능. 헤더(날짜/단계)는 첫 5줄만 다시 읽으면 됨.
- 아키텍처 리팩토링 문서화 위치 (2026-06-26 IUnitFactory 작업 기준):
  - TechnicalDesignDocument.md: "Clean Architecture 구조" 섹션 아래 "의존성 방향 추상화(Application 인터페이스 패턴)" 서브섹션 + 맨 끝 "변경 이력" 표.
  - PROJECT_STATUS.md: 코드 정리/리팩토링 항목들이 모인 표(클린업 Phase 1/2 근처)에 행 추가.
  - ROADMAP.md: "우선순위 요약" 표 + 헤더 날짜/단계.
  - WORK_HISTORY.md: "마일스톤 이력" 표 최상단(날짜 역순)에 행 추가.
- 의존성 역전 패턴(인터페이스=Application, 구현=Infrastructure) 사례 모음: IGameServices, IUnitFactory, IEntityPositionProvider, IForfeitService.
- 사운드 시스템 규칙 문서는 `GameSystemRules/GameSystemRules_Sound.md`에 있음(규칙 번호제). 버그 수정이 기존 규칙의 누락을 드러내면(예: 2026-07-08 BUG-1 — 규칙 8이 StopCoroutine만 기술) WORKFLOW [12] 근거로 해당 규칙에 요건을 보완할 것.
- Task 문서(Research/Plan)는 히스토리 보존이 원칙 → 본문을 재작성하지 말고 하단에 "완료 결과"/"실제 구현 결과" 섹션을 append하여 계획 대비 달라진 점(예: 폰트 Light→Bold, BUG-3 원인 추정 오류)을 기록.
- 실기 버그 수정 작업 완료 시 갱신 대상: PROJECT_STATUS(헤더+시스템 표 행 추가), ROADMAP(헤더+우선순위 표), WORK_HISTORY(마일스톤 prepend), game-programmer MEMORY(최근 작업 prepend), qa-tester MEMORY(해당 시스템 QA 섹션에 실기 결과 반영), 공용 .claude/MEMORY.md(교훈), 관련 GameSystemRules. 사용자 MEMORY(Windows 경로)는 Linux 환경에서 접근 불가 → 갱신 불가함을 사용자에게 알릴 것.
