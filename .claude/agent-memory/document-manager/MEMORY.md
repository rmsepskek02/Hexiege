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

## 누적 교훈
- 환경 주의: `.claude/MEMORY.md`의 절대 경로는 Windows(`d:/Dmain/...`)로 적혀 있으나 실제 작업 환경은 Linux(`/home/user/Hexiege/`). 파일 접근은 항상 `/home/user/Hexiege/` 기준.
- PROJECT_STATUS.md는 길어서(약 680줄) Read가 truncate됨 → 부분 read한 줄은 Edit 전 해당 위치를 다시 Read해야 편집 가능. 헤더(날짜/단계)는 첫 5줄만 다시 읽으면 됨.
- 아키텍처 리팩토링 문서화 위치 (2026-06-26 IUnitFactory 작업 기준):
  - TechnicalDesignDocument.md: "Clean Architecture 구조" 섹션 아래 "의존성 방향 추상화(Application 인터페이스 패턴)" 서브섹션 + 맨 끝 "변경 이력" 표.
  - PROJECT_STATUS.md: 코드 정리/리팩토링 항목들이 모인 표(클린업 Phase 1/2 근처)에 행 추가.
  - ROADMAP.md: "우선순위 요약" 표 + 헤더 날짜/단계.
  - WORK_HISTORY.md: "마일스톤 이력" 표 최상단(날짜 역순)에 행 추가.
- 의존성 역전 패턴(인터페이스=Application, 구현=Infrastructure) 사례 모음: IGameServices, IUnitFactory, IEntityPositionProvider, IForfeitService.
