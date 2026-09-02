# Hexiege 문서 인덱스

프로젝트 전체 문서의 단일 인덱스. **이 파일은 인덱스만 담는다** — 규칙·역할 정의는 각 원본 문서에 있다.

| 찾는 것 | 어디에 |
|---------|--------|
| 절대 규칙 · 에이전트 위임 기준 | `CLAUDE.md` |
| 작업 사이클 · 완료 후 업데이트 체크리스트 | `Assets/_Project/Docs/WORKFLOW.md` |
| 에이전트 메모리 관리 규칙 | `.claude/MEMORY.md` |

---

### 핵심 설정 파일 (루트)

| 파일 | 내용 | 자동 로드 |
|------|------|----------|
| `CLAUDE.md` | 절대 규칙 | ✅ 항상 |
| `AGENTS.md` (이 파일) | 전체 문서 인덱스 | ❌ 수동 |

### 기획 / 설계 문서

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/GameDesignDocument.md` | GDD — 게임 전체 기획 |
| `Assets/_Project/Docs/TechnicalDesignDocument.md` | TDD — 기술 아키텍처 설계 |
| `Assets/_Project/Docs/UIGuidelines.md` | UI 가이드라인 |
| `Assets/_Project/Docs/StatsReference.md` | 유닛/건물 스탯 참조표 — 자원 시스템, 범위 공격 규칙, 유닛 강화(연구소) 시스템 스탯 포함 |
| `Assets/_Project/Docs/AuthSystemRules.md` | 로그인/인증 시스템 규칙 |
| `Assets/_Project/Docs/LogRules.md` | 런타임 로그 + QA-Fix 로그(`Log.md`) 작성 규칙 (절 목록은 그 문서가 단일 소스) |
| `Assets/_Project/Docs/GameSystemRules.md` | 게임 시스템 규칙 인덱스 — **규칙 문서별 상세 요약의 단일 소스** (세부 규칙은 `GameSystemRules/` 하위 파일) |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Map.md` | 대전 맵 전체 180도 대칭, 광산 공정성 및 정적 최단 접근거리 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_RandomMap.md` | FlatTop 11×21 무작위 대전 맵 유형별 생성·검증 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` | 공통 UI 규칙, 생산 패널, MistShrine 패널, 건물 배치 패널, 인게임 설정 메뉴, 로비 설정/프로필 UI |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md` | 유닛 이동(건물로 경로가 막혔을 때의 동작 포함), 전투 진입, 전투 연계, 전투 연출 동기화, 애니메이션 상태 동기화, 특수 공격 시스템, 방어력 데미지 감쇄 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` | 랠리포인트, 건물 철거, 방어 타워, MistShrine 물안개 힐 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md` | 스킬 건물, 쿨다운/스킬 수 규칙, 3×3 스킬 UI, 스킬 메커니즘 타입, 발동 경로, 모바일 지점 조준 UX, 서버 권위 — 구체 스킬 목록·수치는 아직 미확정 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Upgrade.md` | 연구소 유닛 강화(공/방/속 + 자연회복) + 전투 스탯 ×10 스케일 + 연구 패널 UI |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_CanvasSortingOrder.md` | Canvas SortingOrder 구조, 씬별 Canvas 계층, 전역 프리팹 Canvas, 새 Canvas 추가 시 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Sound.md` | BGM 전환, SFX 정책, 볼륨 제어, AudioManager 아키텍처 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI.md` | AI 난이도, 빌드오더 스크립트, 반응 시스템, 건물 배치 로직, 가드 메커니즘, 아키텍처 및 구현 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Human.md` | Human 종족 AI 빌드오더 시나리오 A/B/C |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md` | Spirit 종족 AI 빌드오더 시나리오 A/B/C |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md` | Transcendence 종족 AI 빌드오더 시나리오 A/B/C |
| `Assets/_Project/Docs/Skills/SKILLS_GUIDE.md` | Claude Code 스킬 사용 가이드 |

### 에셋 문서

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/Assets/AssetList.md` | 3D 모델 · UI/스프라이트 에셋 목록 (VFX/SFX는 `VFXSFXList.md`) |
| `Assets/_Project/Docs/Assets/3DAssetCreationGuide.md` | 3D 에셋 제작 가이드 — 팀 색상 구분 규칙, 종족 컨셉, 생성 프롬프트, Meshy AI 변환 설정, 명명 규칙 |
| `Assets/_Project/Docs/Assets/CommonAssetGuide.md` | 에셋 제작 공통 가이드 — 프로젝트 컨셉, 프롬프트 작성 원칙, 이미지 공통 조건 |
| `Assets/_Project/Docs/Assets/UIAssetGuide.md` | UI 에셋 제작 가이드 — AI 도구 공통 절대 규칙, 유닛 초상화 제작 흐름, Unity Sprite Import 설정, 명명 규칙 |
| `Assets/_Project/Docs/Assets/VFXSFXGuide.md` | VFX/SFX 제작 가이드 — 종족별 스타일, 폴더 구조/명명 규칙, 프롬프트 작성 원칙, 카테고리별 가이드 |
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | VFX/SFX 에셋 목록 (보류 항목 포함) |

### 프로젝트 관리

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/PROJECT_STATUS.md` | 현재 진행 상태, 완료 항목 — 기술 스택 · 아키텍처 · 에셋 현황 포함 |
| `Assets/_Project/Docs/ROADMAP.md` | 미완성/예정 작업 우선순위 — Phase별(네트워크 · 게임플레이 · 콘텐츠 · 플랫폼) 백로그 |
| `Assets/_Project/Docs/WORK_HISTORY.md` | 완료된 작업 시간순 이력 (마일스톤 표) |
| `Assets/_Project/Docs/AABSizeOptimization.md` | Android AAB 용량 최적화 기록 — 적용 변경, 변경하지 않은 영역, 기기 테스트 체크리스트, 롤백 기준 |
| `Assets/_Project/Docs/UnusedAssetAudit.md` | 미사용 에셋 감사 및 정리 기록 — 스캔 방법, 결과 해석 주의점, 후속 결정 |

### 작업 사이클 (Task)

| 파일/경로 | 내용 | 자동 로드 |
|----------|------|----------|
| `Assets/_Project/Docs/WORKFLOW.md` | 작업 사이클 운영 규칙 — **단일 권위 소스** | ❌ 수동 |
| `.claude/mistakes.md` | **AI 실수 기록** — AI가 이 프로젝트에서 저지른 실수의 누적 기록. **전문을 읽지 않는다** — 상단 목차를 훑고 이번 작업과 성격이 비슷한 항목만 펼쳐 읽는다. **읽는 법 · 적는 법 · 항목 형식은 그 문서 상단이 단일 소스**이며 이 자리에 옮겨 적지 않는다. 확인 시점은 [WORKFLOW.md](Assets/_Project/Docs/WORKFLOW.md) 「작업 시작 전 확인」 | ❌ 수동 |
| `Tools/check_docs.py` | 문서 정합성 검사기(읽기 전용) — 규칙 번호·문서 링크·규칙 참조의 정합성과 **에이전트 메모리 보호**(고아 토픽 파일, 총합 행수 감소)를 찾아 목록만 출력한다. **검사 항목의 정확한 목록은 검사기 자신과 WORKFLOW.md [11]이 단일 소스**이며 이 자리에 옮겨 적지 않는다. 행수 감소 기준값은 `.claude/agent-memory/_baseline.json` — 직접 편집 금지, 갱신은 `--update-baseline`, 감소가 포함되면 `--reason` 필수. 문서 수정 후 `python3 Tools/check_docs.py`를 리포지토리 루트에서 실행해 **0건**을 확인한다. 🔴 **주의: 규칙을 `**규칙 N. 제목**` 굵은 글씨로 쓰지 않으면 그 문서의 규칙이 통째로 검사에서 빠지고, 검사기는 아무 경고 없이 0건을 낸다.** 실제로 몇 개 문서가 규칙 원본으로 읽히는지는 **검사기가 매 실행 시 출력하는 `[검사 범위]` 블록이 권위 소스**이며 이 자리에 숫자를 옮겨 적지 않는다. **검사 범위와 형식 규정은 [WORKFLOW.md](Assets/_Project/Docs/WORKFLOW.md) [11]이 단일 소스** — 실행 결과를 해석하기 전에 그쪽을 볼 것 | ❌ 수동 |
| `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/` | 작업별 Research / Plan / Testcase (사용자용) | — |
| `Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_[작업명]/Log.md` | QA-Fix 반복 이터레이션 로그 (에이전트용) | — |

### 에이전트 메모리

> 메모리 갱신 방법은 `.claude/MEMORY.md` 「🔴 Agent Memory Management Rules」 참조.
>
> 에이전트 메모리는 대부분 `MEMORY.md`(**인덱스**) + 같은 폴더의 토픽 파일(`.claude/agent-memory/<에이전트>/*.md`) 구조다. 어떤 토픽 파일이 있는지는 **그 인덱스가 단일 소스**이며 이 자리에 옮겨 적지 않는다 — 인덱스에서 링크되지 않은 토픽 파일은 `Tools/check_docs.py` 가 잡는다.

| 파일 | 내용 | 자동 로드 |
|------|------|----------|
| `.claude/MEMORY.md` | 에이전트 공용 컨텍스트 — 아키텍처 핵심 제약, 🔴 메모리 관리 규칙, 문서 언어 규칙, 좌표계, 공통 교훈 | ❌ 수동 |
| `.claude/agent-memory/game-programmer/MEMORY.md` | game-programmer 누적 지식 인덱스 | ❌ 수동 |
| `.claude/agent-memory/game-design-lead/MEMORY.md` | game-design-lead 누적 지식 인덱스 | ❌ 수동 |
| `.claude/agent-memory/qa-tester/MEMORY.md` | qa-tester 누적 지식 인덱스 | ❌ 수동 |
| `.claude/agent-memory/asset-prompt-crafter/MEMORY.md` | asset-prompt-crafter 누적 지식 (토픽 파일 없음 — 본문이 여기에 있다) | ❌ 수동 |
| `.claude/agent-memory/project-orchestrator/MEMORY.md` | **폐지된 에이전트의 아카이브 (2026-09-02)** — `project-orchestrator` 에이전트는 폐지됐고 조율은 메인 세션이 한다. 폴더의 토픽 파일(과거 스냅샷·3D 전환 로드맵)은 **프로젝트 이력이라 그대로 보존**한다. 폐지 경위는 이 파일 최상단 참조 | ❌ 수동 |
| `.claude/agent-memory/document-manager/MEMORY.md` | document-manager 누적 지식 인덱스 | ❌ 수동 |

---
