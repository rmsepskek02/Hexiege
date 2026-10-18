# Hexiege 문서 인덱스

**문서 역할**
- **목적·질문:** 필요한 프로젝트 문서의 위치를 찾는다.
- **포함:** 경로와 한 줄 역할 요약.
- **제외·담당:** 상세 책임은 각 문서 머리말, 공통 절차는 WORKFLOW, 절대 권한은 CLAUDE.
- **원본·참조:** 프로젝트 전체 문서 찾기의 단일 인덱스다.
- **갱신:** 경로·책임 변경 때 해당 행을 고친다. 규칙·검증 인계를 복제하지 않는다.

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
| `Assets/_Project/Docs/TechnicalDesignDocument.md` | 공통 기술 구조·개발 환경·강제 실패 메뉴 사용법 |
| `Assets/_Project/Docs/UIGuidelines.md` | UI 가이드라인 |
| `Assets/_Project/Docs/StatsReference.md` | 실제 구현 수치·단위·잠정/선택 경계의 미러 |
| `Assets/_Project/Docs/AuthSystemRules.md` | 로그인/인증 시스템 규칙 |
| `Assets/_Project/Docs/LogRules.md` | 런타임·QA-Fix 로그와 원시 로그 private 보관의 현행 규범 |
| `Assets/_Project/Docs/LogHistory.md` | 로그 정책·구현·검증의 과거 원문 |
| `Assets/_Project/Docs/GameSystemRules.md` | 도메인별 규칙 원본을 찾는 인덱스 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Map.md` | 대전 맵 전체 180도 대칭, 광산 공정성 및 정적 최단 접근거리 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_RandomMap.md` | FlatTop 11×21 무작위 대전 맵 유형별 생성·검증 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Network.md` | 역할에 무관한 제품 동작·이탈/승패 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` | 공통 UI·팝업·로딩 규칙 원본 및 기존 UI 참조 안내 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_MatchFlowUI.md` | 경기 준비 실패·결과·재경기 화면의 M/D 규칙 원본 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_BuildingPanelsUI.md` | 생산·MistShrine·건물 배치 패널 규칙 원본 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_SettingsUI.md` | 인게임·로비 설정·프로필 UI 규칙 원본 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_MatchFlow.md` | 맵 전송·실패 복구·재경기·결과 화면/재로드 이탈 운영 규칙 원본 |
| `Assets/_Project/Docs/TechnicalDesignDocument_Network.md` | 네트워크 상세 기술 — Netcode·동기화·맵 데이터 계약·이탈 판정 구조·치팅 방어 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md` | 이동·타겟·공격·피해·표현의 유닛 불변 계약 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | 서버 권위 행동 회차·타격 결과·멀티플레이 표현 동기화 계약 |
| `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` | 25종 유닛 공격 에셋·설정·구현·검증 상태 감사표 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` | 랠리포인트, 건물 철거, 방어 타워, MistShrine 물안개 힐 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md` | 스킬 공통 동작·서버 권위와 미확정 기획 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Upgrade.md` | 연구소 유닛 강화(공/방/속 + 자연회복) + 전투 스탯 ×10 스케일 + 연구 패널 UI |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_CanvasSortingOrder.md` | Canvas 계층·정렬·추가 계약 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Sound.md` | BGM 전환, SFX 정책, 볼륨 제어, AudioManager 아키텍처 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI.md` | AI 공통 구조·난이도·행동 규칙 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Human.md` | Human 종족 AI 빌드오더 시나리오 A/B/C |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md` | Spirit 종족 AI 빌드오더 시나리오 A/B/C |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md` | Transcendence 종족 AI 빌드오더 시나리오 A/B/C |
| `Assets/_Project/Docs/Skills/SKILLS_GUIDE.md` | Claude Code 스킬 사용 가이드 |

### 에셋 문서

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/Assets/AssetList.md` | 3D 모델 · UI/스프라이트 에셋 목록 (VFX/SFX는 `VFXSFXList.md`) |
| `Assets/_Project/Docs/Assets/3DAssetCreationGuide.md` | 3D 제작·팀/종족 표현·변환·명명 지침 |
| `Assets/_Project/Docs/Assets/CommonAssetGuide.md` | 공통 에셋 제작 조건·프롬프트 원칙 |
| `Assets/_Project/Docs/Assets/UIAssetGuide.md` | UI 제작·초상화·Sprite import 지침 |
| `Assets/_Project/Docs/Assets/VFXSFXGuide.md` | VFX/SFX 제작·폴더·명명 지침 |
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | VFX/SFX 에셋 목록 (보류 항목 포함) |

### 프로젝트 관리

| 파일 | 내용 |
|------|------|
| `Assets/_Project/Docs/PROJECT_STATUS.md` | 시스템별 현재 구현·검증 상태와 한계 |
| `Assets/_Project/Docs/ROADMAP.md` | 실제 미완·보류 작업의 우선순위·선행 조건 |
| `Assets/_Project/Docs/WORK_HISTORY.md` | 완료된 작업 시간순 이력 (마일스톤 표) |
| `Assets/_Project/Docs/History/ImplementationVerificationHistory.md` | 날짜별 구현·검증·인계의 원문과 출처 — 현행 사양·현재 상태는 해당 원본을 참조 |
| `Assets/_Project/Docs/_Reference/MapTemplateSource/` | 유형별 맵 템플릿 재생성 참조: FullyOpen·ObstacleOpen·Canyon·Outer·ThreeLane |
| `Assets/_Project/Docs/AABSizeOptimization.md` | Android AAB 최적화의 적용·검증·롤백 기록 |
| `Assets/_Project/Docs/UnusedAssetAudit.md` | 미사용 에셋 감사 방법·판단·후속 기록 |

### 작업 사이클 (Task)

| 파일/경로 | 내용 | 자동 로드 |
|----------|------|----------|
| `Assets/_Project/Docs/WORKFLOW.md` | 핵심 작업 사이클·갱신/병합 검토·검사·수치 보고 절차 | ❌ 수동 |
| `.claude/mistakes.md` | AI 실수 누적 기록 — 목차와 읽는 법은 이 문서 참조 | ❌ 수동 |
| `.claude/mistakes/unit-action-correction.md` | 공격·이동 교정 전용 실패 기록 — mistakes 인덱스를 거쳐 참조 | ❌ 수동 |
| `Tools/check_docs.py` | 문서·메모리 정합성 검사기 — 범위·형식·한계는 WORKFLOW [11] | ❌ 수동 |
| `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/` | 작업별 Research / Plan / Testcase (사용자용) | — |
| `Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_[작업명]/Log.md` | QA-Fix 반복 이터레이션 로그 (에이전트용) | — |

### 에이전트 메모리

> 메모리 갱신 방법은 `.claude/MEMORY.md` 「🔴 Agent Memory Management Rules」 참조.
>
> 토픽 파일은 각 에이전트 MEMORY 인덱스에서 찾는다. 관리 규칙은 공용 메모리 원본을 따른다.

| 파일 | 내용 | 자동 로드 |
|------|------|----------|
| `.claude/MEMORY.md` | 공용 제약·메모리 관리·언어·좌표계의 원본 | ❌ 수동 |
| `.claude/agent-memory/game-programmer/MEMORY.md` | game-programmer 누적 지식 인덱스 | ❌ 수동 |
| `.claude/agent-memory/game-design-lead/MEMORY.md` | game-design-lead 누적 지식 인덱스 | ❌ 수동 |
| `.claude/agent-memory/qa-tester/MEMORY.md` | qa-tester 누적 지식 인덱스 | ❌ 수동 |
| `.claude/agent-memory/asset-prompt-crafter/MEMORY.md` | asset-prompt-crafter 누적 지식 (토픽 파일 없음 — 본문이 여기에 있다) | ❌ 수동 |
| `.claude/agent-memory/project-orchestrator/MEMORY.md` | 폐지된 에이전트의 보존 아카이브 — 경위는 해당 머리말 | ❌ 수동 |
| `.claude/agent-memory/document-manager/MEMORY.md` | document-manager 누적 지식 인덱스 | ❌ 수동 |

---
