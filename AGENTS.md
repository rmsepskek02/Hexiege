# Hexiege 에이전트 & 문서 인덱스

에이전트 역할 정의 + 프로젝트 전체 문서의 단일 인덱스.

---

## 전체 문서 인덱스

### 핵심 설정 파일 (루트)

| 파일 | 내용 | 자동 로드 |
|------|------|----------|
| `CLAUDE.md` | 절대 규칙 | ✅ 항상 |
| `AGENTS.md` (이 파일) | 에이전트 역할 + 문서 인덱스 | ✅ 항상 |

### 기획 / 설계 문서

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/GameDesignDocument.md` | GDD — 게임 전체 기획 |
| `Assets/_Project/Docs/TechnicalDesignDocument.md` | TDD — 기술 아키텍처 설계 |
| `Assets/_Project/Docs/UIGuidelines.md` | UI 가이드라인 |
| `Assets/_Project/Docs/StatsReference.md` | 유닛/건물 스탯 참조표 |
| `Assets/_Project/Docs/AuthSystemRules.md` | 로그인/인증 시스템 규칙 |
| `Assets/_Project/Docs/GameSystemRules.md` | 게임 시스템 규칙 인덱스 (세부 규칙은 `GameSystemRules/` 하위 파일 참조) |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` | 공통 UI 규칙, 생산 패널, 건물 배치 패널, 인게임 설정 메뉴 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md` | 유닛 이동, 전투 진입, 전투 연계 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` | 랠리포인트, 건물 철거, 방어 타워 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI.md` | AI 난이도, 빌드오더 스크립트, 반응 시스템, 가드 메커니즘 |

### 에셋 문서

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/Assets/AssetList.md` | 전체 에셋 목록 |
| `Assets/_Project/Docs/Assets/3DAssetCreationGuide.md` | 3D 에셋 제작 가이드 |
| `Assets/_Project/Docs/Assets/CommonAssetGuide.md` | 공통 에셋 가이드 |
| `Assets/_Project/Docs/Assets/UIAssetGuide.md` | UI 에셋 가이드 |

### 프로젝트 관리

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/PROJECT_STATUS.md` | 현재 진행 상태, 완료 항목 |
| `Assets/_Project/Docs/ROADMAP.md` | 미완성/예정 작업 우선순위 |
| `Assets/_Project/Docs/WORK_HISTORY.md` | 완료된 작업 시간순 이력 |

### 작업 사이클 (Task)

| 파일/경로 | 내용 | 자동 로드 |
|----------|------|----------|
| `Assets/_Project/Docs/WORKFLOW.md` | 작업 사이클 운영 규칙 — **단일 권위 소스** | ❌ 수동 |
| `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/` | 작업별 Research / Plan / Testcase (사용자용) | — |
| `Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_[작업명]/Log.md` | QA-Fix 반복 이터레이션 로그 (에이전트용) | — |

### 에이전트 메모리

| 파일 | 내용 | 자동 로드 |
|------|------|----------|
| `.claude/MEMORY.md` | 에이전트 공용 컨텍스트 인덱스 | ❌ 수동 |
| `.claude/agent-memory/game-programmer/MEMORY.md` | game-programmer 누적 지식 | ❌ 수동 |
| `.claude/agent-memory/game-design-lead/MEMORY.md` | game-design-lead 누적 지식 | ❌ 수동 |
| `.claude/agent-memory/qa-tester/MEMORY.md` | qa-tester 누적 지식 | ❌ 수동 |
| `.claude/agent-memory/asset-prompt-crafter/MEMORY.md` | asset-prompt-crafter 누적 지식 | ❌ 수동 |
| `.claude/agent-memory/project-orchestrator/MEMORY.md` | project-orchestrator 누적 지식 | ❌ 수동 |
| `C:\Users\rmsep\.claude\projects\...\memory\` | 프로젝트 상태/학습 메모리 | ✅ 항상 |

---

## 에이전트 역할 & 위임 기준

| 에이전트 | 담당 | 언제 사용 | 전달 필수 컨텍스트 |
|---------|------|----------|-----------------|
| **game-programmer** | 코드 구현 / 버그 수정 | 코드 변경이 필요한 모든 작업 | 관련 파일 경로, 증상, 아키텍처 규칙 |
| **game-design-lead** | 게임플레이 설계 / 밸런스 결정 | 수치·규칙·흐름 결정이 필요할 때 | 현재 구현 상태, 관련 수치 |
| **qa-tester** | 구현 검증 / 버그 체크 | 구현 완료 후 반드시 | 변경된 파일 목록, 예상 동작 |
| **asset-prompt-crafter** | 3D 모델 / UI 에셋 생성 | Meshy.ai 또는 이미지 생성 필요 시 | 에셋 스펙, FBX 파이프라인 요구사항 |
| **project-orchestrator** | 작업 분해 / 에이전트 조율 | 설계+구현 동시, 3파일 이상, 복합 작업 | 전체 컨텍스트 + 각 에이전트 MEMORY 경로 |

### project-orchestrator 사용 기준

| 상황 | 사용 여부 |
|------|----------|
| 단일 파일 버그 수정 | 선택 (game-programmer 직접 가능) |
| 설계 결정 + 코드 구현이 함께 필요 | **필수** |
| 3개 이상 파일에 걸친 기능 추가 | **필수** |
| 에이전트 결과 검토 필요 | **필수** |
| 전체 현황 파악 및 다음 작업 결정 | **필수** |

---

## 완료 후 업데이트 체크리스트

작업 완료 시 해당하는 항목 모두 업데이트.

| 조건 | 업데이트 대상 |
|------|-------------|
| 항상 | `PROJECT_STATUS.md`, `ROADMAP.md`, `WORK_HISTORY.md` |
| 코드 변경 | game-programmer MEMORY.md, project-orchestrator MEMORY.md |
| 게임플레이/밸런스 변경 | game-design-lead MEMORY.md |
| 버그 수정 / 취약점 발견 | qa-tester MEMORY.md |
| 3D 에셋 작업 | asset-prompt-crafter MEMORY.md |
| 모든 작업 완료 후 | `C:/Users/rmsep/.claude/projects/.../memory/MEMORY.md` |
