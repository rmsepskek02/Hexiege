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
| `Assets/_Project/Docs/StatsReference.md` | 유닛/건물 스탯 참조표 |
| `Assets/_Project/Docs/AuthSystemRules.md` | 로그인/인증 시스템 규칙 |
| `Assets/_Project/Docs/LogRules.md` | 런타임 로그 + QA-Fix 로그(`Log.md`) 작성 규칙 (두 축 — 심각도/존속, 분류 원칙, 파일 위치, 형식, 이벤트 키, 민감 데이터, 릴리스 스트리핑, sink 구조, 파일 누적 관리, 실기기 Logcat 캡처, 금지사항, Round 반복 구조) |
| `Assets/_Project/Docs/GameSystemRules.md` | 게임 시스템 규칙 인덱스 (세부 규칙은 `GameSystemRules/` 하위 파일 참조) |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Map.md` | 대전 맵 전체 180도 대칭, 광산 공정성 및 정적 최단 접근거리 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_RandomMap.md` | FlatTop 11×21 무작위 대전 맵 5종 생성·검증 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` | 공통 UI 규칙, 생산 패널, MistShrine 패널, 건물 배치 패널, 인게임 설정 메뉴, 로비 설정/프로필 UI |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md` | 유닛 이동·정렬·타겟·공격·피해 불변 규칙, 전투 연출·애니메이션 상태 동기화, 특수 공격 5종, 방어력 데미지 감쇄 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | 서버 권위 행동 회차·타격 결과·멀티플레이 표현 동기화 계약 |
| `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` | 25종 유닛 공격 에셋·설정·구현·검증 상태 감사표 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` | 랠리포인트, 건물 철거, 방어 타워, MistShrine 물안개 힐 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md` | 스킬 건물 3종, 쿨다운/스킬 수 규칙, 3×3 스킬 UI, 스킬 타입 3종, 모바일 지점 조준 UX |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Upgrade.md` | 연구소 유닛 강화(공/방/속 + 자연회복) + 전투 스탯 ×10 스케일 + 연구 패널 UI |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_CanvasSortingOrder.md` | Canvas SortingOrder 구조 및 씬별 Canvas 계층 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Sound.md` | BGM 전환, SFX 정책, 볼륨 제어, AudioManager 아키텍처 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI.md` | AI 난이도, 빌드오더 스크립트, 반응 시스템, 건물 배치 로직, 가드 메커니즘 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Human.md` | Human 종족 AI 빌드오더 시나리오 A/B/C (물량형·테크형·균형형) |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md` | Spirit 종족 AI 빌드오더 시나리오 (Inferno·Torrent·Quake) |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md` | Transcendence 종족 AI 빌드오더 시나리오 (Rush·Flora·Beast) |
| `Assets/_Project/Docs/Skills/SKILLS_GUIDE.md` | Claude Code 스킬 사용 가이드 |

### 에셋 문서

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/Assets/AssetList.md` | 전체 에셋 목록 |
| `Assets/_Project/Docs/Assets/3DAssetCreationGuide.md` | 3D 에셋 제작 가이드 |
| `Assets/_Project/Docs/Assets/CommonAssetGuide.md` | 공통 에셋 가이드 |
| `Assets/_Project/Docs/Assets/UIAssetGuide.md` | UI 에셋 가이드 |
| `Assets/_Project/Docs/Assets/VFXSFXGuide.md` | VFX/SFX 제작 가이드 |
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | VFX/SFX 에셋 목록 |

### 프로젝트 관리

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/PROJECT_STATUS.md` | 현재 진행 상태, 완료 항목 |
| `Assets/_Project/Docs/ROADMAP.md` | 미완성/예정 작업 우선순위 |
| `Assets/_Project/Docs/WORK_HISTORY.md` | 완료된 작업 시간순 이력 |
| `Assets/_Project/Docs/AABSizeOptimization.md` | Android AAB 용량 최적화 기록, 적용 변경, 테스트 체크리스트 |
| `Assets/_Project/Docs/UnusedAssetAudit.md` | 미사용 에셋 감사 및 정리 기록 |

### 작업 사이클 (Task)

| 파일/경로 | 내용 | 자동 로드 |
|----------|------|----------|
| `Assets/_Project/Docs/WORKFLOW.md` | 작업 사이클 운영 규칙 — **단일 권위 소스** | ❌ 수동 |
| `.claude/mistakes.md` | **AI 실수 기록** — AI가 이 프로젝트에서 저지른 실수의 누적 기록. 항목마다 **무엇을 틀렸나 / 왜 그랬나 / 어떻게 드러났나 / 교훈**. **전문을 읽지 않는다** — 상단 **목차(한 건당 한 줄)** 를 훑고 이번 작업과 성격이 비슷한 항목만 펼쳐 읽는다. 실수를 인지하면 그 자리에서 덧붙이고 **사건은 지우지 않는다**. 확인 시점은 WORKFLOW.md 「작업 시작 전 확인」 | ❌ 수동 |
| `Tools/check_docs.py` | 문서 정합성 검사기(읽기 전용) — **7종**을 찾아 목록만 출력: 규칙 번호 결번, 깨진 파일 링크, 실재하지 않는 규칙 번호, 섹션명 없는 모호한 참조, 병기 내용과 규칙 제목 불일치, **인덱스에서 링크되지 않은 에이전트 메모리 토픽 파일**, **에이전트 메모리 폴더의 총합 행수 감소**(기준값 `.claude/agent-memory/_baseline.json` — 직접 편집 금지, 갱신은 `--update-baseline`, 감소가 포함되면 `--reason` 필수). 문서 수정 후 `python3 Tools/check_docs.py`를 리포지토리 루트에서 실행해 **0건**을 확인한다. ⚠️ **검사 범위와 알려진 한계(규칙 정의를 `**규칙 N.**` 굵은 글씨 형식으로만 인식 → `GameSystemRules/` 13개 중 7개만 규칙 원본으로 읽힌다)는 [WORKFLOW.md](Assets/_Project/Docs/WORKFLOW.md) [11]이 단일 소스** — 실행 결과를 해석하기 전에 그쪽을 볼 것 | ❌ 수동 |
| `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/` | 작업별 Research / Plan / Testcase (사용자용) | — |
| `Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_[작업명]/Log.md` | QA-Fix 반복 이터레이션 로그 (에이전트용) | — |

### 에이전트 메모리

> 메모리 갱신 방법은 `.claude/MEMORY.md` 「🔴 Agent Memory Management Rules」 참조.

| 파일 | 내용 | 자동 로드 |
|------|------|----------|
| `.claude/MEMORY.md` | 에이전트 공용 컨텍스트 인덱스 | ❌ 수동 |
| `.claude/agent-memory/game-programmer/MEMORY.md` | game-programmer 누적 지식 | ❌ 수동 |
| `.claude/agent-memory/game-design-lead/MEMORY.md` | game-design-lead 누적 지식 | ❌ 수동 |
| `.claude/agent-memory/qa-tester/MEMORY.md` | qa-tester 누적 지식 | ❌ 수동 |
| `.claude/agent-memory/asset-prompt-crafter/MEMORY.md` | asset-prompt-crafter 누적 지식 | ❌ 수동 |
| `.claude/agent-memory/project-orchestrator/MEMORY.md` | project-orchestrator 누적 지식 | ❌ 수동 |
| `.claude/agent-memory/document-manager/MEMORY.md` | document-manager 누적 지식 | ❌ 수동 |

---
