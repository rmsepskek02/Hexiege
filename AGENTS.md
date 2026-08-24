# Hexiege 에이전트 & 문서 인덱스

에이전트 역할 정의 + 프로젝트 전체 문서의 단일 인덱스.

---

## 전체 문서 인덱스

### 핵심 설정 파일 (루트)

| 파일 | 내용 | 자동 로드 |
|------|------|----------|
| `CLAUDE.md` | 절대 규칙 | ✅ 항상 |
| `AGENTS.md` (이 파일) | 에이전트 역할 + 문서 인덱스 | ✅ 항상 |
| `CONTEXT.md` | 프로젝트 핵심 도메인 용어집 | ❌ 수동 |

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
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md` | 유닛 이동, 전투 진입, 전투 연계, 전투 연출 동기화, 애니메이션 상태 동기화, 특수 공격 시스템(확장 5종), 방어력 데미지 감쇄 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` | 랠리포인트, 건물 철거, 방어 타워, MistShrine 물안개 힐 (구현 완료 / 싱글 실기 검증 완료 · 멀티 미검증) |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md` | 스킬 건물 3종, 쿨다운/스킬 수 규칙, 3×3 스킬 UI, 스킬 타입 3종, 모바일 지점 조준 UX (기획 확정/미구현) |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Upgrade.md` | 연구소 유닛 강화(공/방/속 + 자연회복) + 전투 스탯 ×10 스케일 + 연구 패널 UI (구현 완료 / 멀티 실기 PASS) |
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
| `Assets/_Project/Docs/BuildAssetOptimizationReport.md` | 빌드 에셋 최적화 감사/중간 리포트 |
| `Assets/_Project/Docs/UnusedAssetAudit.md` | 미사용 에셋 감사 및 정리 기록 |

### 작업 사이클 (Task)

| 파일/경로 | 내용 | 자동 로드 |
|----------|------|----------|
| `Assets/_Project/Docs/WORKFLOW.md` | 작업 사이클 운영 규칙 — **단일 권위 소스** | ❌ 수동 |
| `Tools/check_docs.py` | 문서 정합성 검사기(읽기 전용) — **7종**을 찾아 목록만 출력: 규칙 번호 결번, 깨진 파일 링크, 실재하지 않는 규칙 번호, 섹션명 없는 모호한 참조, 병기 내용과 규칙 제목 불일치, **인덱스에서 링크되지 않은 에이전트 메모리 토픽 파일**, **에이전트 메모리 폴더의 총합 행수 감소**(기준값 `.claude/agent-memory/_baseline.json` — 직접 편집 금지, 갱신은 `--update-baseline`). 문서 수정 후 `python3 Tools/check_docs.py`를 리포지토리 루트에서 실행해 **0건**을 확인한다 (WORKFLOW.md [11]) | ❌ 수동 |
| `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/` | 작업별 Research / Plan / Testcase (사용자용) | — |
| `Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_[작업명]/Log.md` | QA-Fix 반복 이터레이션 로그 (에이전트용) | — |

### 에이전트 메모리

> ## 🔴 MEMORY.md 갱신은 `Read` → `Edit`. **`Write` 금지** (2026-08-20 신설)
>
> **에이전트가 자기 `MEMORY.md` 를 갱신할 때는 반드시 아래 순서를 따른다. 예외 없음.**
>
> 1. **`Read` 로 현재 내용을 먼저 읽는다.** 비어 있다고 가정하지 않는다.
> 2. **`Edit` 로 해당 부분만 고친다.** 새로 배운 것은 알맞은 섹션에 **덧붙인다.**
> 3. **`Write` 로 파일 전체를 다시 쓰지 않는다.**
> 4. 기존 항목 **삭제는 그 내용이 틀렸다고 확인했을 때만** 하고, **지운 이유를 함께 남긴다.**
>    "정리했다" · "간결하게 줄였다" 는 삭제 사유가 되지 못한다.
>
> **왜 이 규칙이 생겼나 — 2026-08-20 실제 사고.**
> `game-programmer` 가 `MEMORY.md` 를 **`Write` 로 재작성**해 **46행 → 28행**으로 줄었고,
> 토픽 파일(`logging.md`) 참조 링크 · `LogLevel` 이 **두 네임스페이스에 있다는 함정** ·
> **host 는 서버이자 클라이언트**라 `ClientRpc` 안의 로그를 `if (IsServer) return;` **뒤**에 둔다는 규칙 ·
> 선례 파일:행 목록 · **「알려진 잔존 구멍」 섹션 전체**(미해결 항목 포함)가 통째로 사라졌다.
>
> **원인은 에이전트의 잘못이 아니라 정의 파일에 하드코딩돼 있던 두 줄이었다** —
> ① *"Your MEMORY.md is currently empty."* 라는 **거짓 문장**(파일이 실제로 비었던 시점에 굳어, 매 호출마다 그렇게 믿고 시작했다)
> ② **존재하지 않는 윈도우 절대경로**(그 경로로는 기존 파일을 찾아 ①을 반증할 수도 없었다).
> 둘 다 `.claude/agents/*.md` **6개 전부**에서 제거·교체했다. **이 표에 규칙을 함께 남기는 이유는 설정 파일이 되돌려질 수 있기 때문이다.**
>
> **⭐ 남길 교훈 한 문장:** **시스템 프롬프트에 「현재 상태」를 사실로 못 박지 말고, 「먼저 읽어서 확인하라」는 절차로 적는다.**
> 상태 서술은 시간이 지나면 거짓이 되지만 절차는 그렇지 않다.
>
> **[🔴 2026-08-21 보강 — 위 인용 블록은 그대로 두고 덧붙인다]**
> **규칙 원문(6개 항목)은 이제 `.claude/MEMORY.md` 「🔴 에이전트 메모리 갱신 규칙」 절에 있다** — 모든 에이전트가 작업 전 읽는 공용 파일이라 그쪽이 단일 소스이고, 이 인용 블록은 요약으로 남긴다.
> 위 사고(`Write` 덮어쓰기)는 **손실 경로 3가지 중 하나일 뿐이며, 나머지 둘은 성질이 다르므로 `Write` 금지만으로는 막히지 않는다** —
> ② **200행 절삭**: `MEMORY.md` 는 프롬프트에 실릴 때 **200행 뒤가 잘린다.** 초과분은 파일에 남아 있어도 **에이전트에게 보이지 않고, 커밋 이력에도 안 남아 사고가 났다는 것조차 모른다.** 2026-08-21 실측 초과 3개 — **qa-tester 502행 · project-orchestrator 325행 · game-design-lead 254행.**
> ③ **고아 토픽 파일**: 인덱스에서 링크가 끊긴 토픽 파일은 **존재하지 않는 것과 같다.** 2026-08-21 실측 — `game-programmer` 토픽 **18개 중 16개(1,839행)** 가 미링크(살아 있는 링크는 `logging.md`·`network-infra.md` 둘뿐). 원인은 ①이다 — **링크를 담고 있던 인덱스가 덮어써지면 토픽 파일이 통째로 미아가 된다.**
> 따라서 내용을 토픽으로 옮길 때는 **에이전트 폴더 전체 행수 합이 줄지 않는지**를 확인한다 — 이동과 삭제를 구분하는 유일한 검증법이다.

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

## 에이전트 역할 & 위임 기준

| 에이전트 | 담당 | 언제 사용 | 전달 필수 컨텍스트 |
|---------|------|----------|-----------------|
| **game-programmer** | 코드 구현 / 버그 수정 | 코드 변경이 필요한 모든 작업 | 관련 파일 경로, 증상, 아키텍처 규칙 |
| **game-design-lead** | 게임플레이 설계 / 밸런스 결정 | 수치·규칙·흐름 결정이 필요할 때 | 현재 구현 상태, 관련 수치 |
| **qa-tester** | 구현 검증 / 버그 체크 | 구현 완료 후 반드시 | 변경된 파일 목록, 예상 동작 |
| **asset-prompt-crafter** | 3D 모델 / UI 에셋 생성 | Meshy.ai 또는 이미지 생성 필요 시 | 에셋 스펙, FBX 파이프라인 요구사항 |
| **project-orchestrator** | 작업 분해 / 에이전트 조율 | 설계+구현 동시, 3파일 이상, 복합 작업 | 전체 컨텍스트 + 각 에이전트 MEMORY 경로 |
| **document-manager** | 모든 문서 생성·수정·동기화 | 문서 작성/업데이트가 필요한 모든 작업 | 변경 내용, 관련 문서 경로 |

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
| 문서 구조 변경 / 신규 에이전트 추가 | document-manager MEMORY.md |
| 모든 작업 완료 후 | `.claude/MEMORY.md` (에이전트 공용) |
