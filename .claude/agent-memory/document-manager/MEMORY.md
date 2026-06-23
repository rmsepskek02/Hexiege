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
